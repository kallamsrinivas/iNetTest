using System;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
    internal class EventProcessor
    {
        internal EventProcessor() {}

        /// <summary>
        /// Saves/updates in the database whatever needs to be saved/updated based
        /// on the results of the passed-in event.
        /// Includes saving the event, updating installed cylinders, deleting ScheduledOnces.
        /// </summary>
        /// <param name="dsEvent"></param>
        /// <param name="lastDockedTime"></param>
        internal void Save( DockingStationEvent dsEvent, DateTime lastDockedTime )
        {
            // We don't want to start a transaction unless we know we're going to update
            // the database, since merely just beginning a writable transaction causes a write.
            // Not every event type will have a code.  For those that don't, we don't 
            // need the last run time saved (such as Nothing events or interactive diagnostics)
            if ( dsEvent.EventCode == null )
                return;

            // Save event journals and installed cylinder updates all in a single transaction.
            // If this event was invoked by a run-once schedule, then that schedule is also
            // deleted in the same transaction.
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                // Save/update EventJournals for the event.
                bool saved = SaveEventJournals( dsEvent, lastDockedTime, trx );

                Log.Debug( string.Format( "SaveEventJournals({0})={1}", dsEvent, saved ) );

                // If this event was invoked by a run-once schedule, then delete the schedule
                // since, by definition, "run once" schedules are run once, then deleted.
                if ( ( dsEvent.Schedule != null ) && ( dsEvent.Schedule is ScheduledOnce ) )
                {
                    bool deleted = new ScheduledOnceDataAccess().DeleteById( dsEvent.Schedule.Id, trx );
                    Log.Debug( string.Format( "ScheduledOnceDataAccess.Delete({0})={1}", dsEvent.Schedule.Id, deleted ) );
                }

				// If event is a SettingsRead, then SaveGasEndPoints will update the GasEndPoints
				// in the database based on what what cylinders the SettingsRead found attached to
                // the docking station.
                if ( dsEvent is SettingsReadEvent )
                {
                    SaveGasEndPoints( (SettingsReadEvent)dsEvent, trx );

					// If this is a SettingsReadEvent, reload the GasEndPoints from the database 
					// now that  they've been saved and cache them in our global dockingstation.
                    // This also has the side effect of setting ech of their 'Supported' properties
                    // to null which will force a revalidation of their part numbers against the 
                    // known FactoryCylinder part numbers.
                    Configuration.DockingStation.GasEndPoints = new GasEndPointDataAccess().FindAll( trx );
                }
                else if ( dsEvent is InstrumentSettingsReadEvent )
                {
                    // If this is a InstrumentSettingsReadEvent, then refresh the switch'service's
                    // docked instrument information with the information from the event.
                    // If we don't do this, then the switch service's information can become out of
                    // date after Instrument Settings Updates are performed, or instrument firmware
                    // is updated, etc.
                    //
                    // TODO: the following is probably not necessary anymore do to changes made for INS-3825.
                    // InstrumentSettingsRead operation is now just returning a clone of SwitchService.Instrument.
                    // So setting the SwitchService.Instrument to be a clone of what InstrumentSettingsRead
                    // returned is pretty much just setting it to a clone of an exact copy of itself.
                    // -- JMP, 2/13/2013.
                    Master.Instance.SwitchService.Instrument = (DomainModel.Instrument)( (InstrumentSettingsReadEvent)dsEvent ).DockedInstrument.Clone();

                    //Suresh 05-JAN-2012 INS-2564
                    //During instrument setting read if we find any sensor in error state then we want to report it to iNet
                    Master.Instance.ExecuterService.ReportDiscoveredInstrumentErrors((InstrumentEvent)dsEvent); 
                }
                else if ( ( dsEvent is InstrumentCalibrationEvent )
                && ( Master.Instance.SwitchService.Instrument.SerialNumber != string.Empty ) ) // can this happen here?
                {
                    // Keep the Sensors in the cached instrument object updated with the status
                    // of what happened during the calibration, for use by the scheduler.

                    bool cylinderHoseProblem = false;
                    UsedGasEndPoint gasEndPointUsedForCal = null;       // INS-8446 RHP v7.6

                    foreach ( SensorGasResponse sgr in ( dsEvent as InstrumentCalibrationEvent ).GasResponses )
                    {
                        // If any sensor failed calibration with a zero span reserve, then set the SwitchService's CylinderHoseProblem to true.
                        // We don't do this for O2 sensors, though, as a O2 calibration failure with a zero span reserve would not indicate a 
                        // a problem with the hoses. - INS-1279, 6/20/2011, JMP // INETQA-4131 RHP v7.6 - Added Status.Failed condition check below.
                        if ( sgr.SensorCode != SensorCode.O2 && ( sgr.Status == Status.SpanFailed || sgr.Status == Status.SpanFailedZeroFailed || sgr.Status == Status.Failed)
                            && sgr.FullSpanReserve == 0.0 )
                        {
                            cylinderHoseProblem = true;
                            // INS-8446 RHP v7.6 - Save the Cylinder details which is expected to have hose problems. 
                            // Do a NULL to ensure that we identify the first gas response that failed a calibration with Zero Span reserve and fetch its gas end point being used.
                            if (gasEndPointUsedForCal == null)  
                                gasEndPointUsedForCal = sgr.UsedGasEndPoints.Find(uge => uge.Usage == CylinderUsage.Calibration && !uge.Cylinder.IsFreshAir && !uge.Cylinder.IsZeroAir);
                        }

                        Sensor sensor = (Sensor)Master.Instance.SwitchService.Instrument.InstalledComponents.Find( ic => ic.Component.Uid == sgr.Uid ).Component;
                        if ( sensor != null )
                            sensor.CalibrationStatus = sgr.Status;
                    }

                    //Suresh 22-Feb-2012 INS-2705
                    //After calibration is completed , we need to update sensor bump test status because in scheduler we have logic
                    //to force calibration based on sensor BumpTestStatus
                    foreach (InstalledComponent installedComponent in (dsEvent as InstrumentCalibrationEvent).DockedInstrument.InstalledComponents)
                    {
                        if (!(installedComponent.Component is Sensor))  // Skip non-sensors.
                            continue;

                        if (!installedComponent.Component.Enabled) // Skip disabled sensors.
                            continue;

                        Sensor bumpTestedSensor = (Sensor)installedComponent.Component;

                        Sensor sensor = (Sensor)Master.Instance.SwitchService.Instrument.InstalledComponents.Find(ic => ic.Component.Uid == bumpTestedSensor.Uid).Component;
                        if (sensor != null)
                            sensor.BumpTestStatus = bumpTestedSensor.BumpTestStatus;
                    }

					// If at least one sensor had a status of InstrumentAborted, then we know the instrument reset.
					// The check cylinders message should not be shown on the LCD.
					foreach ( SensorGasResponse sgr in ( dsEvent as InstrumentCalibrationEvent ).GasResponses )
					{
						if ( sgr.Status == Status.InstrumentAborted )
						{
							cylinderHoseProblem = false;
                            gasEndPointUsedForCal = null;
							break;
						}
					}

                    Master.Instance.SwitchService.BadGasHookup = cylinderHoseProblem;
                    // INS-8446 RHP v7.6 - Set the SwitchService's BadGasHookUpCylinderPartNumber which is expected to have hose problems to display the same on LCD
                    Master.Instance.SwitchService.BadGasHookUpCylinderPartNumber = gasEndPointUsedForCal == null ? string.Empty : gasEndPointUsedForCal.Cylinder.PartNumber;
                    Log.Debug(string.Format("EventProcessor : BadGasHookUpCylinderPartNumber identified cylinder with Part Number {0}", Master.Instance.SwitchService.BadGasHookUpCylinderPartNumber) );
                }
                //Suresh 22-Feb-2012 INS-2705
                else if ((dsEvent is InstrumentBumpTestEvent)
                && (Master.Instance.SwitchService.Instrument.SerialNumber != string.Empty)) // can this happen here?
                {
                    // Keep the Sensors in the cached instrument object updated with the status
                    // of what happened during the bumptest, for use by the scheduler.

                    foreach (InstalledComponent installedComponent in (dsEvent as InstrumentBumpTestEvent).DockedInstrument.InstalledComponents)
                    {
                        if (!(installedComponent.Component is Sensor))  // Skip non-sensors.
                            continue;

                        if (!installedComponent.Component.Enabled) // Skip disabled sensors.
                            continue;

                        Sensor bumpTestedSensor = (Sensor)installedComponent.Component;

                        Sensor sensor = (Sensor)Master.Instance.SwitchService.Instrument.InstalledComponents.Find(ic => ic.Component.Uid == bumpTestedSensor.Uid).Component;
                        if (sensor != null)
                            sensor.BumpTestStatus = bumpTestedSensor.BumpTestStatus; 
                    }
                }

                trx.Commit();
            }
        }

        private bool SaveEventJournals( DockingStationEvent dsEvent, DateTime lastDockedTime, DataAccessTransaction trx )
        {
            if ( dsEvent.EventCode == null )
                return true;

            EventJournalDataAccess ejDataAccess = new EventJournalDataAccess();

            EventJournal eventJournal = null;

            // If the event implements IPassed, then use the result of its 
            // Passed property.  Otherwise, just default to true.
            bool passed = ( ( dsEvent is IPassed ) ) ? ( (IPassed)dsEvent ).Passed : true;

            if ( dsEvent is InstrumentEvent )
            {
                // special case for // bump & cals... need to save a separate
                // event journal for every sensor involved in gas operation.
                // Note that in this situation, we also do NOT save an entry for the instrument itself.
                if ( dsEvent is InstrumentGasResponseEvent && ! (dsEvent is InstrumentManualOperationsDownloadEvent ) ) 
                {
                    InstrumentGasResponseEvent gasResponseTestEvent = (InstrumentGasResponseEvent)dsEvent;
                    if (gasResponseTestEvent.GasResponses.Count <= 0)
                        return true;

                    bool allSaved = true;
                    foreach ( SensorGasResponse sgr in gasResponseTestEvent.GasResponses )
                    {
                        eventJournal = new EventJournal( gasResponseTestEvent.EventCode.Code, sgr.Uid,
                                                         gasResponseTestEvent.DockedInstrument.SerialNumber, 
                                                         sgr.Time, dsEvent.Time, sgr.Passed,
                                                         sgr.Position, gasResponseTestEvent.DockedInstrument.SoftwareVersion );
                        allSaved &= ejDataAccess.Save( eventJournal, trx );
                    }
                    // If gasResponseEvent is a InstrumentBumpTestEvent, it might have calibration gas responses due to O2 high bump test failure.
                    // If there are any gas responses in HighBumpFailCalGasResponses, store them to the event journals.
                    foreach (SensorGasResponse sgr in gasResponseTestEvent.HighBumpFailCalGasResponses)
                    {
                        eventJournal = new EventJournal(EventCode.Calibration, sgr.Uid,
                                                        gasResponseTestEvent.DockedInstrument.SerialNumber,
                                                        sgr.Time, dsEvent.Time, sgr.Passed,
                                                        sgr.Position, gasResponseTestEvent.DockedInstrument.SoftwareVersion);
                        allSaved &= ejDataAccess.Save(eventJournal, trx);
                    }
                    return allSaved;
                }
                else
                    eventJournal = new EventJournal( dsEvent.EventCode, ( (InstrumentEvent)dsEvent ).DockedInstrument.SerialNumber, dsEvent.Time, dsEvent.Time, passed, ((InstrumentEvent)dsEvent).DockedInstrument.SoftwareVersion );
            }
            else // DockingStationEvent
            {
                eventJournal = new EventJournal( dsEvent.EventCode, dsEvent.DockingStation.SerialNumber, dsEvent.Time, dsEvent.Time, passed, dsEvent.DockingStation.SoftwareVersion );
            }

            return ejDataAccess.Save( eventJournal, trx ); // Update/insert EventJournal record for the event.
        }

        /// <summary>
        /// If passed-in event is a SettingsReadEvent, then this routine updates the database with the GasEndPoints.
        /// </summary>
        /// <param name="dsEvent"></param>
        private void SaveGasEndPoints( SettingsReadEvent settingsReadEvent, DataAccessTransaction trx )
        {
			// Either settingsReadEvent.DockingStation.GasEndPoints will have 
			// contents or else ChangedGasEndPoints will.  Never both.
            Log.Assert( ( settingsReadEvent.DockingStation.GasEndPoints.Count > 0 && settingsReadEvent.DockingStation.ChangedGasEndPoints.Count > 0 ) == false,
				"Both GasEndPoints and ChangedGasEndPoints cannot contain data." );

			// If GasEndPoints has contents, then the list contains all known attached
            // cylinders.
			// If ChangedGasEndPoints has contents, then the list contains known changes:
            // e.g.., if there's been no iGas card insertion/removal on, say,
            // position 2, then there will be no cylinder in the list at that position.

            GasEndPointDataAccess gepDataAccess = new GasEndPointDataAccess();

            if ( settingsReadEvent.DockingStation.GasEndPoints.Count > 0 )
            {
				Log.Debug( string.Format( "Calling SaveInstalledCylinders (GasEndPoints.Count={0})", settingsReadEvent.DockingStation.GasEndPoints.Count ) );
                gepDataAccess.SaveInstalledCylinders( settingsReadEvent.DockingStation.GasEndPoints, trx );
            }
			// Note that we don't check the ChangedGasEndPoints to see if it's empty or not.
			// If GasEndPoints is empty, then we assume we're to use ChangedGasEndPoints
            // If it's empty, then it means there are no known changed cylinders. The call
            // to SaveInstalledCylinders will handle that.
            else
            {
                Log.Debug( "Calling SaveChangedCylinders" );
                gepDataAccess.SaveChangedCylinders( settingsReadEvent.DockingStation.ChangedGasEndPoints, trx );
            }
        }

        /// <summary>
        /// Determines if some specific action needs to be performed based
        /// on the event passed to it. (Some events require a specific
        /// followup action to be executed.)
        /// </summary>
        /// <param name="dsEvent"></param>
        /// <returns></returns>
        internal DockingStationAction GetFollowupAction( DockingStationEvent dsEvent )
        {
            // A SettingsUpdate is ALWAYS followed by a SettingsRead.
            if ( dsEvent is SettingsUpdateEvent )
            {
				// If the SettingsUpdate was Scheduled, we want to inject a CylinderPressureReset before the SettingsRead.
				if ( dsEvent.Trigger == TriggerType.Scheduled )
				{
					Log.Trace( "EventProcessor returning CylinderPressureReset as followup to SettingsUpdate" );
					CylinderPressureResetAction cylPressureResetAction = new CylinderPressureResetAction();
					
					// These values will be propogated to a followup SettingsRead.
					cylPressureResetAction.PostUpdate = true;
					cylPressureResetAction.SettingsRefId = ( (SettingsUpdateEvent)dsEvent ).DockingStation.RefId;

					return cylPressureResetAction;
				}
				
                Log.Trace("EventProcessor returning SettingsRead as followup to SettingsUpdate");
                SettingsReadAction settingsReadAction = new SettingsReadAction();
                // Set the refId to indicate that this Read is occurring due to an Update that was just performed.
                settingsReadAction.PostUpdate = true;
                settingsReadAction.SettingsRefId = ((SettingsUpdateEvent)dsEvent).DockingStation.RefId;

                // Explicitly set the ChangedSmartCards to all falses so that no smart cards are read.
                settingsReadAction.ChangedSmartCards = new bool[ Configuration.DockingStation.NumGasPorts ];

                return settingsReadAction;
            }

			if ( dsEvent is CylinderPressureResetEvent )
			{
				Log.Trace( "EventProcessor returning SettingsRead as followup to CylinderPressureReset" );
				SettingsReadAction settingsReadAction = new SettingsReadAction();
				
				// Copy the PostUpdate and SettingsRefId so it can be determined if the SettingsRead is 
				// being run due to a SettingsUpdate that would have occurred before the CylinderPressureReset.
				settingsReadAction.PostUpdate = ( (CylinderPressureResetEvent)dsEvent ).PostUpdate;
				settingsReadAction.SettingsRefId = ( (CylinderPressureResetEvent)dsEvent ).SettingsRefId;

				// Explicitly set the ChangedSmartCards to null so all positions are read similar to startup.
				settingsReadAction.ChangedSmartCards = null;

				return settingsReadAction;
			}

            // After downloading datalog, we clear it.
            if ( dsEvent is InstrumentDatalogDownloadEvent )
            {
				InstrumentDatalogDownloadEvent datalogEvent = (InstrumentDatalogDownloadEvent)dsEvent;

				// D2G: Only clear the log if there is something to clear, or corruption was detected.
				if ( datalogEvent.InstrumentSessions.Count > 0 || datalogEvent.Errors.Count > 0 )
				{
					Log.Trace( "EventProcessor returning InstrumentHygieneClearAction as followup to InstrumentHygieneDownloadEvent" );
					return new InstrumentDatalogClearAction();
				}
				else
				{
					Log.Debug( "NO DATALOG TO CLEAR" );
					return null;
				}
            }

            // After downloading alarm events, we clear them.
            if ( dsEvent is InstrumentAlarmEventsDownloadEvent )
            {
				InstrumentAlarmEventsDownloadEvent alarmsEvent = (InstrumentAlarmEventsDownloadEvent)dsEvent;

				// D2G: Only clear the log if there is something to clear, or corruption was detected.
				if ( alarmsEvent.AlarmEvents.Length > 0 || alarmsEvent.Errors.Count > 0)
				{
					Log.Trace( "EventProcessor returning InstrumentAlarmEventsClearAction as followup to InstrumentAlarmEventsDownloadEvent" );
					return new InstrumentAlarmEventsClearAction();
				}
				else
				{
					Log.Debug( "NO ALARM EVENTS TO CLEAR" );
					return null;
				}
            }

            // After downloading alarm events, we clear them.
            if ( dsEvent is InstrumentManualOperationsDownloadEvent )
            {
				InstrumentManualOperationsDownloadEvent manualOpsEvent = (InstrumentManualOperationsDownloadEvent)dsEvent;

				// D2G: Only clear the log if there is something to clear, or corruption was detected.
				if ( manualOpsEvent.GasResponses.Count > 0 || manualOpsEvent.Errors.Count > 0 )
				{
					Log.Trace( "EventProcessor returning InstrumentManualOperationsDownloadAction as followup to InstrumentManualOperationsDownloadEvent" );
					return new InstrumentManualOperationsClearAction();
				}
				else
				{
					Log.Debug( "NO MANUAL GAS OPERATIONS TO CLEAR" );
					return null;
				}
            }

            if ( dsEvent is InstrumentFirmwareUpgradeEvent )
            {
                if ( ( (InstrumentFirmwareUpgradeEvent)dsEvent ).UpgradeFailure )
                {
                    Log.Trace("EventProcessor returning NothingAction as followup to InstrumentFirmwareUpgradeEvent due to an upgrade failure");

                    // Setting this to true will cause the docking station to go
                    // into its UpgradingInstrumentError state.
                    Master.Instance.SwitchService.InstrumentUpgradeError = true;

                    // Return an action just to prevent further processing (there's no use 
                    // next letting the scheduler figure out what needs to be done next since
                    // we know we're about to into the UpgradingInstrumentError state.)
                    return new NothingAction();
                }
            }

            //Suresh 06-FEB-2012 INS-2622
            //Check whether instrument is in critical error.
            if ( dsEvent is InstrumentDiagnosticEvent)
            {
                InstrumentDiagnosticEvent diagnosticEvent = (InstrumentDiagnosticEvent)dsEvent;

                if (diagnosticEvent.InstrumentInCriticalError == true)
                {
                    Log.Trace("EventProcessor returning NothingAction as followup to InstrumentDiagnosticEvent due to instrument having a critical error");
                    Master.Instance.SwitchService.Instrument.InstrumentInCriticalError = true;
                    // INS-8446 RHP v7.6 - Set the SwitchService's InstrumentCriticalErrorCode to display the critical error code on LCD
                    Log.Trace("EventProcessor identfied InstrumentDiagnosticEvent having a critical error code of " + diagnosticEvent.InstrumentCriticalErrorCode);
                    Master.Instance.SwitchService.Instrument.InstrumentCriticalErrorCode = diagnosticEvent.InstrumentCriticalErrorCode;
                    return new NothingAction();
                }
            }
            return null;
        }
    }
}
