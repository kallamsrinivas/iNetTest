using System;
using System.Collections.Generic;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.iNet;
using ISC.iNet.DS.Services.Resources;
using ISC.Instrument.TypeDefinition;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
    using ISC.iNet.DS.DomainModel; // puting this here avoids compiler's confusion of DomainModel.Instrument vs Instrument.Driver.

    internal class Scheduler
    {
        // Holds queued-up forced schedules. We use a linked list instead of a Queue
        // as we'll need to sometimes scan the list and remove schedules from the middle.
        private LinkedList<ScheduledNow> _forcedList = new LinkedList<ScheduledNow>();

        /// <summary>
        /// Contains all persisted Schedules for the current instrument and its currently installed sensors.
        /// Refreshed on every heartbeat by LoadSchedulingData.
        /// </summary>
        /// <remarks>
        /// Schedules list are loaded in the LoadSchedulingData method.
        /// </remarks>
        internal List<Schedule> _schedules;

        /// <summary>
        /// All journal entries for the docking station, docked instrument, and its sensors.
        /// Refreshed on every heartbeat by LoadSchedulingData.
        /// </summary>
        /// <remarks>
        /// Journals list is loaded in the LoadSchedulingData method.
        /// </remarks>
        internal List<EventJournal> _journals;

        private EventProcessor _eventProcessor = new EventProcessor();

        private const string _nameMsg = "Scheduler: ";

        /// <summary>
        //// Indicates whether or not a calibration is required for some internally-derived reason.
        /// </summary>
        /// <remarks>SGF  04-Mar-2011  DEV JIRAs: INS-2662, INS-2729, INS-2770, INS-3146, INS-2620 and INS-2669</remarks>
        private bool _calibrationRequired;

        /// <summary>
        /// Indicates whether or not a bump test is requiredfor some internally-derived reason.
        /// </summary>
        /// <remarks>SGF  04-Mar-2011  DEV JIRAs: INS-2662, INS-2729, INS-2770, INS-3146, INS-2620 and INS-2669</remarks>
        private bool _bumpTestRequired;

        /// <summary>
        /// Indicates whether or not a diagnostics is required for some internally-derived reason.
        /// </summary>
        /// <remarks>SGF  04-Mar-2011  DEV JIRAs: INS-2662, INS-2729, INS-2770, INS-3146, INS-2620 and INS-2669</remarks>
        private bool _instrumentDiagnosticsRequired;


        ///Below variables are only initialized during unit testing phase.  For normal execution, object is re-instantiated when needed
        private IDataAccessTransaction _dataAccessTransactionForUnitTest = null;
        private EventJournalDataAccess _eventJournalDataAccessForUnitTest = null;
        private ScheduledUponDockingDataAccess _uponDockDataAccessForUnitTest = null;
        private ScheduledOnceDataAccess _onceDataAccessForUnitTest = null;
        private ScheduledHourlyDataAccess _hourlyDataAccessForUnitTest = null;
        private ScheduledDailyDataAccess _dailyDataAccessForUnitTest = null;
        private ScheduledWeeklyDataAccess _weeklyDataAccessForUnitTest = null;
        private ScheduledMonthlyDataAccess _monthlyDataAccessForUnitTest = null;
        private QueueDataAccess _queueDataAccess = null;
        
        /// <summary>
        /// Contains the schedule that is determined to be overdue by the method that returns it,
        /// and the time the event is due to run next, whether overdue or not.
        /// </summary>
        /// <remarks>
        /// Returned by GetOverdueInstrumentSchedule, GetOverdueSensorTypeSchedule, and WillSkipEvent.
        /// </remarks>
        private struct OverdueInfo
        {
            /// <summary>
            /// The schedule that was determined to be overdue by the returning method.
            /// null if no schedule was found to be overdue.
            /// </summary>
            internal Schedule Schedule;

            /// <summary>
            /// The time the event is due to run next, in local time, whether it's overdue or not.
            /// </summary>
            /// <remarks>
            /// null means 'unknown' which would also be if there are no schedules for the event.
            /// also, remember, DateTime defaults to MinValue, with Kind of Unspecified
            /// </remarks>
            internal DateTime? NextRunTime;
        }

        /// <summary>
        /// ctor is made private as lass intended to only be access via static methods.
        /// </summary>
        internal Scheduler() { }

        internal Scheduler(IDataAccessTransaction dtx, EventJournalDataAccess eventJournalDataAccess, ScheduledUponDockingDataAccess uponDockDataAccess, ScheduledOnceDataAccess onceDataAccess,
            ScheduledHourlyDataAccess hourlyDataAccess, ScheduledDailyDataAccess dailyDataAccess, ScheduledWeeklyDataAccess weeklyDataAccess,
            ScheduledMonthlyDataAccess monthlyDataAccess, QueueDataAccess queueDataAccess)
        {
#if TEST == false
            throw new NotSupportedException("This constructor is only intended for unit testing");
#endif
            _dataAccessTransactionForUnitTest = dtx;
            _eventJournalDataAccessForUnitTest = eventJournalDataAccess;
            _uponDockDataAccessForUnitTest = uponDockDataAccess;
            _onceDataAccessForUnitTest = onceDataAccess;
            _hourlyDataAccessForUnitTest = hourlyDataAccess;
            _dailyDataAccessForUnitTest = dailyDataAccess;
            _weeklyDataAccessForUnitTest = weeklyDataAccess;
            _monthlyDataAccessForUnitTest = monthlyDataAccess;
            _queueDataAccess = queueDataAccess;
        }

        internal void QueueForcedSchedule( ScheduledNow scheduledNow )
        {
            lock ( _forcedList )
            {
                _forcedList.AddLast( scheduledNow );
            }
        }

        internal void StackForcedSchedule( ScheduledNow scheduledNow )
        {
            lock (_forcedList)
            {
                _forcedList.AddFirst( scheduledNow );
            }
        }

        /// <summary>
        /// Stacks or Queues a forced "ScheduledNow" event for the specified event code.
        /// </summary>
        /// <param name="eventCodeString"></param>
		/// <param name="isHighestPriority">True - Event will be run before all prior forced events that still need run.
		/// False - Event will be run after all prior forced events that still need run.</param>
        internal void ForceEvent( string eventCodeString, bool isHighestPriority )
        {
            EventCode eventCode = EventCode.GetCachedCode( eventCodeString );

            ScheduledNow nowSched = new ScheduledNow( DomainModelConstant.NullId, DomainModelConstant.NullId, string.Empty, eventCode, null, null, true );

            if ( eventCode.EquipmentTypeCode == EquipmentTypeCode.Instrument )
                nowSched.SerialNumbers.Add( Master.Instance.SwitchService.Instrument.SerialNumber );

			if ( isHighestPriority )
			{
				// Non-gas operations should be run immediately as nothing should be able to block them. 
				StackForcedSchedule( nowSched );
			}
			else
			{
				// Gas operations should be run last as not having the proper gas available would
				// prevent other forced events from running.
				QueueForcedSchedule( nowSched );
			}
            Master.Instance.ExecuterService.HeartBeat();// Gets the scheduler to act on the queued forced event quickly
        }

        /// <summary>
        /// Re-stacks a forced "ScheduledNow" event.
        /// </summary>
        /// <param name="actionTrigger"></param>
        /// <param name="eventCode"></param>
        internal void ReForceEvent(TriggerType actionTrigger, EventCode eventCode)
        {
            if ( actionTrigger != TriggerType.Forced )
                return;

            if ( eventCode.Code != EventCode.BumpTest && eventCode.Code != EventCode.Calibration )
                return;

            ScheduledNow nowSched = new ScheduledNow(DomainModelConstant.NullId, DomainModelConstant.NullId, string.Empty, eventCode, null, null, true);

            if (eventCode.EquipmentTypeCode == EquipmentTypeCode.Instrument)
                nowSched.SerialNumbers.Add( Master.Instance.SwitchService.Instrument.SerialNumber ); ;

            StackForcedSchedule(nowSched);
        }

        /// <summary>
        /// Re-stacks a forced "ScheduledNow" event.
        /// </summary>
        /// <param name="action"></param>
        internal void ReForceEvent(DockingStationAction action)
        {
            if (action.Trigger != TriggerType.Forced)
                return;

            //Usually dock reforces event if flow failed
            //But do not reforce event flow failed occured due to bad pump tubing.
            if (Master.Instance.SwitchService.BadPumpTubingDetectedDuringBump || Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal)
                return;

            EventCode eventCode = null;
            if (action is InstrumentBumpTestAction)
                eventCode = EventCode.GetCachedCode(EventCode.BumpTest);
            else if (action is InstrumentCalibrationAction)
                eventCode = EventCode.GetCachedCode(EventCode.Calibration);
            else
                return;

            ScheduledNow nowSched = new ScheduledNow(DomainModelConstant.NullId, DomainModelConstant.NullId, string.Empty, eventCode, null, null, true);

            if ( eventCode.EquipmentTypeCode == EquipmentTypeCode.Instrument )
                nowSched.SerialNumbers.Add( Master.Instance.SwitchService.Instrument.SerialNumber );

            StackForcedSchedule(nowSched);
        }

        internal void ClearForcedSchedules()
        {
            lock ( _forcedList )
            {
                _forcedList.Clear();
            }
        }


        // SGF  04-Mar-2011  <DEV JIRAs: INS-2662, INS-2729, INS-2770, INS-3146, INS-2620 and INS-2669>
        // Clear all knowledge of required queued actions.
        internal void ClearQueuedActions()
        {
            ClearForcedSchedules();
            _calibrationRequired = _bumpTestRequired = _instrumentDiagnosticsRequired = false;
        }

        // SGF  04-Mar-2011  <DEV JIRAs: INS-2662, INS-2729, INS-2770, INS-3146, INS-2620 and INS-2669>
        internal void QueuedEventCompleted(DockingStationEvent dsEvent)
        {
            if (dsEvent is InstrumentCalibrationEvent)
            {
                Log.Trace(string.Format("{0}Marking queued calibration event as completed.", _nameMsg));
                _calibrationRequired = false;
            }

            if (dsEvent is InstrumentBumpTestEvent)
            {
                Log.Trace(string.Format("{0}Marking queued bump test event as completed.", _nameMsg));
                _bumpTestRequired = false;
            }

            if (dsEvent is InstrumentDiagnosticEvent)
            {
                Log.Trace(string.Format("{0}Marking queued instrument diagnostics event as completed.", _nameMsg));
                _instrumentDiagnosticsRequired = false;
            }
        }

        /// <summary>
        /// Determines the next scheduled action that needs to occur.
        /// </summary>
        /// <param name="dsEvent"></param>
        /// <returns>
        /// The next scheduled action that needs to occur.
        /// If nothing is overdue, then a NothingAction is returned.
        /// This method is guaranteed to never return null.
        /// </returns>
        internal DockingStationAction GetNextAction( DockingStationEvent dsEvent )
        {
            Log.Trace(string.Format("{0}About to get next action.", _nameMsg));

            // SGF  04-Mar-2011  <DEV JIRAs: INS-2662, INS-2729, INS-2770, INS-3146, INS-2620 and INS-2669>
            // Record the fact that a queued operation has been performed and its event returned.
            QueuedEventCompleted(dsEvent);

            if ( !Configuration.Schema.Synchronized )
            {
                Log.Debug( string.Format( "{0}Not yet fully synched. Returning NothingAction", _nameMsg ) );
                return new NothingAction();
            }

            if ( Master.Instance.SwitchService.InitialReadSettingsNeeded )
            {
                Log.Debug( string.Format( "{0}Initial ReadSettings needed. Returning NothingAction", _nameMsg ) );
                return new NothingAction();
            }

            // EventProcessor.GetFollowupAction may return an action if it determines the
            // event passed to it requires a specific followup action to be executed.
            DockingStationAction dsAction = _eventProcessor.GetFollowupAction( dsEvent );

            if ( dsAction != null )
            {
                Log.Debug( string.Format( "{0}GetFollowupAction returned follow up action {1}", _nameMsg, dsAction ) );
                return dsAction;
            }

            // Get the serial numbers for the docking station, its currently
            // docked instrument, and all of its installed sensors.
            string[] serialNumbers = GetDockedSerialNumbers();
            string[] componentCodes = GetComponentCodes();

            Configuration.DockingStation.SoftwareVersion = dsEvent.DockingStation.SoftwareVersion;
            // Load all persisted Schedules and EventJournals for the current instrument and sensors
            // Loaded data is placed into _schedules and _journals member variables.
                        
            LoadSchedulingData( serialNumbers, componentCodes );
           
            Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;

            // INS-8228 RHP v7.6,  Service accounts need to perform auto-upgrade on instruments even in error/fail state
            // Display "Instrument Error" state on DSX LCD if instrument is in System Alarm state and 
            // there is NO "Instrument Firmware Upgrade" schedule.
            if (Master.Instance.SwitchService.IsInstrumentInSystemAlarm)
            {
                if (Master.Instance.ControllerWrapper.IsDocked() && !HasInstrumentUpgradeScheduleOnPriority())
                    throw new InstrumentSystemAlarmException(dockedInstrument.SerialNumber, 0);
            }

            // SGF  24-May-2012  INS-3078
            // Check the instrument to determine if there are any actions that must be taken 
            // by the operator BEFORE proceeding with any automated actions.
            dsAction = InstRequiresOperatorAction(dockedInstrument);
            if (dsAction != null)
            {
                Log.Debug(string.Format("{0}InstRequiresOperatorAction returned {1}", _nameMsg, dsAction));
                return dsAction;
            }
            Log.Trace(string.Format("{0}InstRequiresOperatorAction returned nothing to do.", _nameMsg));

            // See if the instrument requires any actions be performed before normal scheduling can take place.
            GetRequiredInstrumentAction();

            // Determine if the instrument's sensor configuration has been altered since its last docking.
            CompareSensorConfiguration();

            // See if there's any forced action (forced from either keypad or iNet Control or VDS Config).
            dsAction = GetNextForcedAction();
            if ( dsAction != null )
            {
                Log.Debug( string.Format( "{0}GetNextForcedAction returned {1}", _nameMsg, dsAction ) );
                return dsAction;
            }
            Log.Trace(string.Format("{0}GetNextForcedAction returned nothing to do.", _nameMsg));

            dsAction = ExamineJournals( dsEvent );
            if ( dsAction != null )
            {
                Log.Debug( string.Format( "{0}ExamineJournals returned {1}", _nameMsg, dsAction ) );
                return dsAction;
            }
            Log.Trace(string.Format("{0}ExamineJournals returned nothing to do.", _nameMsg));

            dsAction = GetNextScheduledAction();
            if ( dsAction != null )
            {
                Log.Debug( string.Format( "{0}GetNextScheduledAction returned {1}", _nameMsg, dsAction ) );
                return dsAction;
            }
            Log.Trace(string.Format("{0}GetNextScheduledAction returned nothing to do.", _nameMsg)); 

            Log.Trace( string.Format( "{0}There is nothing to do.", _nameMsg ) );

            if (Master.Instance.SwitchService.DockProcessing == true)
            {
                Log.TimingEnd("DOCK TO GREEN", Master.Instance.SwitchService.DockedTime);
                Master.Instance.SwitchService.DockProcessing = false;
            }
            return new NothingAction();
        }

        private bool IsDocked()
        {
            return Master.Instance.SwitchService.IsDocked();
        }



        // SGF  24-May-2012  INS-3078
        /// <summary>
        /// Determines whether the docked instrument requires actions be performed by the 
        /// operator before automated processing can begin.
        /// </summary>
        /// <returns>
        /// Returns an action if something must be performed by the operator.
        /// Null is returned if the docking station can proceed with performing operations on the instrument.
        /// </returns>
        private DockingStationAction InstRequiresOperatorAction(Instrument dockedInstrument)
        {
            // If no instrument is docked, there is no reason to proceed with the checks in this method.
            if (dockedInstrument == null)
                return null;
            if (dockedInstrument.InstalledComponents.Count == 0)
                return null;

            // INS-8380, INETQA-4215 v7.6 Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
            if (HasInstrumentUpgradeScheduleOnPriority())
                return null;

            const string funcMsg = "InstRequiresOperatorAction";
            DockingStationAction dockingStationAction = null;

            List<InstalledComponent> installedSensorComponents = dockedInstrument.InstalledComponents.FindAll( ic => ic.Component is Sensor );


            // First check to see if there are any sensors which can only be tested by manual operations are currently in calibration fault.
            foreach ( InstalledComponent installedSensor in installedSensorComponents )
            {
                Sensor sensor = installedSensor.Component as Sensor;

                // If this sensor is not enabled, ignore it...
                if (!installedSensor.Component.Enabled)
                {
                    Log.Trace(string.Format("{0}{1} ignoring disabled sensor {2}.", _nameMsg, funcMsg, installedSensor.Component.SerialNumber));
                    continue; // ignore disabled sensors.
                }

                // If this sensor can be operated on by the docking station, ignore it... 
                if ( sensor.RequiresManualOperation( EventCode.Calibration, Configuration.DockingStation.GasEndPoints ) == false )
                {
                    Log.Trace(string.Format("{0}{1} ignoring sensor {2} that can be auto-calibrated.", _nameMsg, funcMsg, sensor.Type.Code));
                    continue;
                }

                // Determine whether the sensor is in calibration fault...
                if (SensorGasResponse.IsFailedCalibrationStatus(sensor.CalibrationStatus))
                {
                    Log.Debug(string.Format("{0}{1} detected {2} Sensor (SN={3}) requires manual calibration.", _nameMsg, funcMsg, sensor.Type.Code, installedSensor.Component.SerialNumber));
                    dockingStationAction = new ManualCalibrationRequiredAction();
                    dockingStationAction.Messages.Add( Master.Instance.ConsoleService.GetSensorLabel(sensor.Type.Code));
                    return dockingStationAction;
                }
            }

            // Next check to see if there are any sensors which can only be tested by manual operations are currently in bump fault.
            foreach ( InstalledComponent installedSensor in installedSensorComponents )
            {
                Sensor sensor = installedSensor.Component as Sensor;

                // If this sensor is not enabled, ignore it...
                if ( !installedSensor.Component.Enabled )
                {
                    Log.Trace(string.Format("{0}{1} ignoring disabled sensor {2}.", _nameMsg, funcMsg, installedSensor.Component.SerialNumber));
                    continue; // ignore disabled sensors.
                }

                // If this sensor can be operated on by the docking station, ignore it...                
                if ( sensor.RequiresManualOperation( EventCode.BumpTest, Configuration.DockingStation.GasEndPoints ) == false )
                {
                    Log.Trace(string.Format("{0}{1} ignoring sensor {2} that can be auto-bumped.", _nameMsg, funcMsg, sensor.Type.Code));
                    continue;
                }

                // Determine whether the sensor is in calibration fault...
                if (sensor.BumpTestStatus == false)
                {
                    Log.Debug(string.Format("{0}{1} detected {2} Sensor (SN={3}) requires manual bump test.", _nameMsg, funcMsg, sensor.Type.Code, installedSensor.Component.SerialNumber));
                    dockingStationAction = new ManualBumpTestRequiredAction();
                    dockingStationAction.Messages.Add( Master.Instance.ConsoleService.GetSensorLabel(sensor.Type.Code));
                    return dockingStationAction;
                }
            }

            if (dockingStationAction == null)
                Log.Trace(string.Format("{0}{1} detected no required operator actions.", _nameMsg, funcMsg));

            return dockingStationAction;
        }


        // SGF  23-Nov-2010; Rework of sensor configuration detection
        /// <summary>
        /// Determines whether the docked instrument requires an action be performed before
        /// any other processing can begin.
        /// </summary>
        /// <returns>
        /// Returns an instrument action if something is required immediately.
        /// Null is returned if there are immediately required actions for the docked instrument.
        /// </returns>
        private void GetRequiredInstrumentAction() 
        {
            const string funcMsg = "GetRequiredInstrumentAction:";

            if ( !IsDocked() )
                return;

            // Don't do anything if we're not activated (i.e., if we're in cal station mode).
            if ( !Configuration.Schema.Activated )
            {
                Log.Trace(string.Format("{0}{1} Not activated.", _nameMsg, funcMsg));
                return;
            }

            // Also, there's no reason to force these checks if in service mode
            // except when account is service account and it is configured to override event priority

            // Otherwise, it affects manufacturing when they dock an instrument to test functionality of new VDS.
            if ( Configuration.ServiceMode && !( Configuration.IsRepairAccount() && Configuration.DockingStation.UpgradeOnErrorFail ) )
            {
                Log.Trace(string.Format("{0}{1} ServiceMode enabled", _nameMsg, funcMsg));
                return;
            }

            Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;

            // Check for required actions that are follow up to an instrument firmware upgrade.
            Log.Trace(string.Format("{0}{1} Checking for required actions as follow up to instrument firmware upgrade", _nameMsg, funcMsg));

            bool actionRequired = InstFirmwareUpgradeFollowUp(dockedInstrument);

            if (actionRequired == false)
            {
                Log.Trace(string.Format("{0}{1} No immediate actions required for the docked instrument; normal scheduling can take place.",
                          _nameMsg, funcMsg));
            }
            else
            {
                Log.Debug(string.Format("{0}{1} Actions have been found to be required for the docked instrument; those actions will be ensured to run later.",
                          _nameMsg, funcMsg));
            }
        }

        /// <summary>
        /// Examines whether the sensor configuration within the docked instrument 
        /// has changed.  If anything has changed, the instrument will require an action be performed before
        /// any other processing can begin.
        /// </summary>
        /// <param name="instrument"></param>
        /// <returns>
        /// Returns an instrument action if something is required immediately.
        /// Null is returned if there are immediately required actions for the docked instrument.
        /// </returns>
        private bool InstFirmwareUpgradeFollowUp(Instrument instrument)
        {
            // Look for an InstrumentDiagnostics event that ran previously for this instrument.
            // If it's non existent, or the instrument was at a different software version
            // at the time, then re-run the diagnostic.
            EventJournal journal = _journals.Find(j => j.EventCode.Code == EventCode.InstrumentDiagnostics
                                                && j.SerialNumber == instrument.SerialNumber
                                                && j.SoftwareVersion == instrument.SoftwareVersion);
            if (journal == null)
            {
                Log.Debug(string.Format("{0}No {1} found for instrument {2} with SofwareVersion={3}",
                          _nameMsg, EventCode.InstrumentDiagnostics, instrument.SerialNumber, instrument.SoftwareVersion));

                _instrumentDiagnosticsRequired = true;
                return true;
            }

            //Ajay - 25-Sep-2017 INS-8232
            //In case of service account, if account is setup to "stop on failed bump test",
            //check whether last bump on instrument is a failure.
            if (Configuration.IsRepairAccount() && Configuration.DockingStation.StopOnFailedBumpTest)
            {
                //If indeed instrument has failed last bump test, do not continue with next steps.
                //Return from here to let "FailedLastBumpTest" method to take over.
                if (HasInstrumentFailedLastBumpTest(instrument))
                    return false;
            }
            // INS-6777 RHP v7.6 - In case of non-service account, check whether last bump on instrument is a failure.
            else if (FailedLastBumpTest(instrument) != null)
                return false;

            bool actionRequired;

			actionRequired = InstFirmwareUpgradeComponentFollowUp( instrument, EventCode.Calibration );
			if ( actionRequired )
			{
				_calibrationRequired = true;
				return true;
			}

            Log.Debug(string.Format("{0}Checking for failed calibrations before attempting to issue bump test...", _nameMsg));
            if (FailedLastCalibration(instrument) == null)
            {
                // SGF  12-Dec-2012  DEV INS-5120 - Added per request of manufacturing..
                // If this is a manufacturing account, we will not cause the instrument to run a bump test 
                // as part of an initial docking.  In the manufacturing account, virtually all instrument 
                // dockings are "initial".
                if (Configuration.Schema.IsManufacturing == true)
                {
                    Log.Debug(string.Format("{0}MANUFACTURING:  Bump test will not be run during initial instrument docking.", _nameMsg));
                    return false;
                }

				actionRequired = InstFirmwareUpgradeComponentFollowUp( instrument, EventCode.BumpTest );
				if ( actionRequired )
				{
					_bumpTestRequired = true;
					return true;
				}
            }

            return false;
        }

		/// <summary>
		/// Returns false if every enabled installed sensor has a corresponding event journal in the same instrument
		/// at the instrument's current software version.  If the instrument is in redundant sensor passed, then any failed sensors
		/// do not need a corresponding journal for bump tests.  True is returned if one or more sensors do not have the expected
		/// journals and the provided event code needs run.
		/// </summary>
        private bool InstFirmwareUpgradeComponentFollowUp(Instrument instrument, string eventCode)
        {
            // Retrieve information on whether this is a situation in which the instrument is 
            // currently operating in redundant sensor passed.  This can only happen for TX1 and VPRO instruments,
            // and only if the account has single-sensor mode enabled.  Then, if those conditions are true, if the 
            // instrument has one good sensor and one bad, it is in this mode.
            CalibrationState calState = CalibrationState.Unknown;
            List<InstalledComponent> passedSensors = new List<InstalledComponent>();
            if ( Configuration.IsSingleSensorMode() )
                calState = instrument.GetInstrumentCalibrationState( Configuration.IsSingleSensorMode(), passedSensors, null );

            foreach (InstalledComponent ic in instrument.InstalledComponents)
            {
                if (!(ic.Component is Sensor)) continue; // ignore non-sensors

                if (!ic.Component.Enabled) // ignore disabled sensors. // SGF 24-Nov-2010 Correcting the if-condition to ignore DISABLED sensors
                {
                    Log.Trace(string.Format("{0}Ignoring {1} check for disabled sensor {2}", _nameMsg, eventCode, ic.Component.Uid));
                    continue;
                }

                // First of all, we are only concerned with skipping sensors in single sensor redundant 
                // mode if the current event is a bump test.
                if (eventCode == EventCode.BumpTest)
                {
                    if ( calState == CalibrationState.RedundantSensorPassed )
                    {
                        // This is a "redundant sensor" situation (see comments above about conditions).
                        //
                        // We need to determine if the current sensor is one of the passed sensors in the 
                        // instrument.  If so, we allow the sensor to be checked for a failed bump.  On the 
                        // other hand, if this is (one of) the failed sensors, we don't need to ensure that 
                        // it has had a successful bump, so we can skip it.
                        //
                        bool foundPassedSensor = false;
                        foreach (InstalledComponent pic in passedSensors)
                        {
                            if (pic.Component.SerialNumber == ic.Component.SerialNumber)
                            {
                                foundPassedSensor = true;
                                break;
                            }
                        }
                        if (!foundPassedSensor)
                        {
                            Log.Debug(string.Format("{0}Ignoring failed sensor {1} in instrument with redundant sensor for possible follow up bump.", _nameMsg, ic.Component.Uid));
                            continue;
                        }
                    }
                }

                // For each installed sensor, look for an calibration event journal for that sensor 
                // with the same instrument software version as the instrument.
                // An old event journal with a different instrument software version means the 
                // instrument's firmware has since been upgraded; we therefore need to recalibrate.

                EventJournal journal = _journals.Find(j => j.EventCode.Code == eventCode
                                                    && j.SerialNumber == ic.Component.Uid
                                                    && j.InstrumentSerialNumber == instrument.SerialNumber
                                                    && j.SoftwareVersion == instrument.SoftwareVersion);
                if (journal == null)
                {
                    Log.Debug(string.Format("{0}No {1} found for sensor {2} with SofwareVersion={3}", _nameMsg, eventCode, ic.Component.Uid, instrument.SoftwareVersion));
                    return true;
                }
            }

            return false;
        }

        //Ajay - 25-Sep-2017 INS-8232
        /// <summary>
        /// Verifies instrument's latest bump test is failed in two ways.
        /// 1.  Checks journal to see whether last bump on given sensor is fail
        /// 2.  Checks BumpTestStatus flag on sensor.  If BumpTestStatus flag is false, it indicates that last bump is a fail. 
        /// </summary>
        private bool HasInstrumentFailedLastBumpTest(Instrument instrument)
        {
            Log.Debug("Determine whether there are any sensors with failed bump test...");
            EventProcessor eventProcessor = new EventProcessor();
            InstrumentBumpTestEvent bumpTestEvent = new InstrumentBumpTestEvent();
            bumpTestEvent.DockedInstrument = instrument;
            bool hasBumpFailure = false;

            foreach (InstalledComponent ic in instrument.InstalledComponents)
            {
                Sensor sensor = ic.Component as Sensor;

                //If not a sensor, return
                if (sensor == null)
                    continue;

                //If sensor is disabled, continue
                if (!ic.Component.Enabled)
                {
                    Log.Debug(string.Format("Sensor {0} is not enabled, hence not verifying whether this sensor failed bump previously.", sensor.Uid));
                    continue;
                }

                //Retrieve if there are any BumpTest journals for sensor
                //If instrument is new to service account, no journals will be found
                //If service account saw this instrument earlier, it is possible that journals will be present
                EventJournal journal = _journals.Find(j => j.EventCode.Code == EventCode.BumpTest && j.SerialNumber == sensor.Uid);

                //If journal is found, and its Passed flag is false, last bump test on this sensor is fail
                if (journal != null && !journal.Passed)
                {
                    Log.Debug(string.Format("Sensor {0} is in bump fail state.", sensor.Uid));
                    hasBumpFailure = true;
                    continue;
                }

                //If no journal found or journal was found to be in Passed state, check BumpTestStatus property on sensor.
                //If sensor reports that it is in failed bump test status, we create a bump test journal ourselves with status as "failed"
                if (sensor.BumpTestStatus == false)
                {
                    Log.Debug(string.Format("Sensor {0} is in bump fail state.", sensor.Uid));
                    hasBumpFailure = true;
                    bumpTestEvent.GasResponses.Add(new SensorGasResponse { Uid = sensor.Uid, Time = DateTime.Now, Status = Status.Failed, Position = ic.Position });
                }
            }

            //If bump failure is identified either through journal or BumpTestStatus flag, return "true" that instrument has bump failure
            if (hasBumpFailure)
            {
                if (bumpTestEvent.GasResponses.Count > 0)
                    eventProcessor.Save(bumpTestEvent, DateTime.Now);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Examines whether the sensor configuration within the docked instrument 
        /// has changed.  If anything has changed, the instrument will require an action be performed before
        /// any other processing can begin.
        /// </summary>
        /// <returns>
        /// </returns>
        private void CompareSensorConfiguration() 
        {
            if (!IsDocked())
                return;

            // Don't do comparison if we're not activated (i.e., if we're in cal station mode).
            if ( !Configuration.Schema.Activated )
                return;

            string funcName = "CompareSensorConfiguration";
            Log.Trace(string.Format("{0}: Checking for altered sensor configurations in calibration journals...", funcName));

            Instrument instrument = Master.Instance.SwitchService.Instrument;
            string eventCode = EventCode.Calibration; 

            List<EventJournal> journals = new List<EventJournal>();

            bool configurationMatches = true;
            string instrumentSerialNumber = instrument.SerialNumber;

            using (IDataAccessTransaction trx = _dataAccessTransactionForUnitTest == null ? new DataAccessTransaction(true) : _dataAccessTransactionForUnitTest)
            {
                // obtain the current event journals for the instrument and its sensor from the EVENTJOURNAL table
                Log.Trace(string.Format("{0}: obtaining {1} event journals for instrument {2}", funcName, eventCode.ToUpper(), instrumentSerialNumber));

                if(_eventJournalDataAccessForUnitTest != null)
                    journals = _eventJournalDataAccessForUnitTest.FindLastEventByInstrumentSerialNumber(instrumentSerialNumber, eventCode, trx);
                else
                    journals = new EventJournalDataAccess().FindLastEventByInstrumentSerialNumber( instrumentSerialNumber, eventCode, trx );
            }

            // get the number of event journals for sensors for the calibration event
            Log.Trace(string.Format("{0}: EVENT JOURNALS", funcName)); 
            int journalCount = 0;
            foreach (EventJournal evJournal in journals)
            {
                if (evJournal.SerialNumber != instrumentSerialNumber)
                {
                    Log.Trace(string.Format("{0}: event journal for SN {1}", funcName, evJournal.SerialNumber));
                }
                if (evJournal.InstrumentSerialNumber == instrumentSerialNumber)
                    ++journalCount;
            }
            Log.Trace(string.Format("{0}: instrument {1} has event journals for {2} sensors", funcName, instrumentSerialNumber, journalCount.ToString()));

            if (journalCount <= 0)
            {
                // With no journal entries for this event, it is assumed this event has not run before.
                // Allow normal processing to cause the event to occur.
                Log.Debug(string.Format("{0}: No {1} event journals for instrument {2}, operation has not run; allow normal processing", funcName, eventCode.ToUpper(), instrumentSerialNumber));
                return;
            }

            // obtain a count of the sensors in the instrument
            Log.Trace(string.Format("{0}: INSTALLED SENSORS", funcName));
            int sensorCount = 0;
            foreach (InstalledComponent comp in instrument.InstalledComponents)
            {
                if (comp.Component is Sensor)
                {
                    // SGF  01-Mar-2011  INS-2631
                    Log.Trace(string.Format("{0}: installed sensor UID {1}, pos {2}, {3}", funcName,
                                            comp.Component.Uid, comp.Position, comp.Component.Enabled ? "enabled" : "disabled"));
                    ++sensorCount;
                }
            }
            Log.Trace(string.Format("{0}: instrument {1} currently has {2} sensors installed", funcName, instrumentSerialNumber, sensorCount.ToString()));

            // SGF  01-Mar-2011  INS-2631
            // Determine if any sensors have been installed or moved within the instrument
            // by comparing the existing configuration of the sensors with the event journals
            // kept for the event in question.
            foreach (InstalledComponent comp in instrument.InstalledComponents)
            {
                if (!(comp.Component is Sensor))
                    continue;

                Sensor sensor = (Sensor)comp.Component;  // SGF  01-Mar-2011  INS-2631 >> refer to 'sensor' in remainder of loop
                int sensorPosition = comp.Position;      // SGF  01-Mar-2011  INS-2631 >> refer to 'sensorPosition' in remainder of loop

                if (!sensor.Enabled) // SGF  01-Mar-2011  INS-2631 >> don't bother evaluating disabled sensors
                    continue;

                Log.Trace(string.Format("{0}: comparing event journals for sensor {1}", funcName, sensor.Uid));  // SGF  01-Mar-2011  INS-2631
                bool sensorFound = false;
                int i = 0;
                foreach (EventJournal evJournal in journals)
                {
                    i++;
                    if (sensor.Uid == evJournal.SerialNumber)  // SGF  01-Mar-2011  INS-2631
                    {
                        Log.Trace(string.Format("{0}: found match on iteration {1}", funcName, i.ToString()));
                        Log.Trace(string.Format("{0}: installed sensor position is {1}", funcName, sensorPosition.ToString()));
                        Log.Trace(string.Format("{0}: event journal sensor position is {1}", funcName, evJournal.Position.ToString()));
                        sensorFound = true;
                        if (sensorPosition != evJournal.Position)  // SGF  01-Mar-2011  INS-2631
                        {
                            Log.Debug(string.Format("{0}: Configuration match is FALSE -- SENSOR {1} HAS BEEN MOVED from position {2} to position {3}.", funcName, sensor.Uid, evJournal.Position, sensorPosition));
                            configurationMatches = false;
                        }
                        else
                        {
                            Log.Trace(string.Format("{0}: sensor and event journal positions MATCH", funcName));
                        }
                        break;
                    }
                }
                if (sensorFound == false)
                {
                    Log.Debug(string.Format("{0}: Configuration match is FALSE -- SENSOR {1} HAS BEEN INSTALLED in the instrument at position {2}.", funcName, sensor.Uid, sensorPosition));  // SGF  01-Mar-2011  INS-2631
                    configurationMatches = false;
                }
                if (configurationMatches == false)
                    break;
            }

            // SGF  01-Mar-2011  INS-2631
            // If the configuration matches so far, check to see if any sensors have been removed.
            if (configurationMatches == true)
            {
                foreach (EventJournal evJournal in journals)
                {
                    if (evJournal.InstrumentSerialNumber == instrumentSerialNumber)
                    {
                        Log.Trace(string.Format("{0}: comparing sensors for a match with event journal with sn={1}", funcName, evJournal.SerialNumber));
                        bool sensorFound = false;
                        foreach (InstalledComponent comp in instrument.InstalledComponents)
                        {
                            if (!(comp.Component is Sensor))
                                continue;
                            Sensor sensor = (Sensor)comp.Component;
                            Log.Trace(string.Format("{0}: comparing event journal to sensor with sn={1}", funcName, sensor.Uid));
                            if (sensor.Uid == evJournal.SerialNumber)  // SGF  01-Mar-2011  INS-2631
                            {
                                Log.Trace(string.Format("{0}: sensor with sn={1} FOUND for existing event journal", funcName, sensor.Uid));
                                sensorFound = true;
                                break;
                            }
                        }
                        if (sensorFound == false)
                        {
                            Log.Debug(string.Format("{0}: Configuration match is FALSE -- SENSOR {1} HAS BEEN REMOVED from the instrument.", funcName, evJournal.SerialNumber));
                            configurationMatches = false;
                            break;
                        }
                    }
                }
            }

            // At this point, we know if a configuration has changed or remains the same.
            // That determines whether a calibration must be run.  We now set the Scheduler
            // property CalibrationRequired to indicate what we found.  If true, the calibration
            // will be run at a later time.
            if (configurationMatches != true)
            {
                Log.Debug(string.Format("{0}: SENSOR CONFIGURATION CHANGE DETECTED; {1} EVENT IS REQUIRED for instrument {2}", funcName, eventCode.ToUpper(), instrumentSerialNumber));
                _calibrationRequired = true;
            }
            else
            {
                Log.Trace(string.Format("{0}: SENSOR CONFIGURATION MATCHES {1} event journals for instrument {2}", funcName, eventCode.ToUpper(), instrumentSerialNumber));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>
        /// Returns the next queued up forced schedule.
        /// Null is returned if there are no currently queued forced schedules
        /// </returns>
        private DockingStationAction GetNextForcedAction()
        {
            lock ( _forcedList )
            {
                while ( _forcedList.Count > 0 )
                {
                    // Remove item on the 'first' (oldest) side of the linked list.
                    ScheduledNow scheduledNow = _forcedList.First.Value;
                    _forcedList.RemoveFirst();

                    if ( scheduledNow.EventCode.EquipmentTypeCode == EquipmentTypeCode.VDS )
                    {
                        DockingStationAction dsAction = CreateAction( scheduledNow );

                        // will be Nothing if EventCode is not recognized.  Just skip it.
                        if ( dsAction is NothingAction )
                            continue;

                        dsAction.Trigger = TriggerType.Forced;

                        return dsAction;

                    } // end-if

                    else if ( scheduledNow.EventCode.EquipmentTypeCode == EquipmentTypeCode.Instrument )
                    {
                        if ( !IsDocked() )
                        {
                            Log.Debug( string.Format( "{0}No docked instrument. Ignoring forced {1}", _nameMsg, scheduledNow.EventCode ) );
                            continue;
                        }

                        // grab reference so that we don't have to worry about docking/undocking while
                        // in this loop changing the DockingInstrument on us
                        Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;

                        List<string> equipSerialNumbers = dockedInstrument.GetSerialNumbers();

                        List<string> matches = equipSerialNumbers.FindAll( sn => scheduledNow.SerialNumbers.Contains( sn ) );

                        // No matches will be found if the schedule is for a different instrument, or
                        // for a component not in the instrument.  Just skip it.
                        if ( matches.Count == 0 )
                        {
                            Log.Debug( string.Format( "{0}No matching serial numbers. Ignoring forced {1}", _nameMsg, scheduledNow.EventCode ) );
                            continue;
                        }

                        DockingStationAction dsAction = null;

                        switch ( scheduledNow.EventCode.Code )
                        {
                            case EventCode.Calibration:
                                break;

                            case EventCode.BumpTest:

                                // Check for a failed calibration...
                                Log.Trace(string.Format("{0}Checking for failed calibrations...", _nameMsg));
                                dsAction = FailedLastCalibration( dockedInstrument );
                                if ( dsAction != null )
                                {
                                    Log.Debug( string.Format("{0}instrument failed cal; forced bump test will not be run", _nameMsg ));
                                    return dsAction;
                                }
                                break;
                        }

                        dsAction = CreateAction( scheduledNow );

                        // will be Nothing if EventCode is not recognized.  Just skip it.
                        if ( dsAction is NothingAction )
                            continue;

                        dsAction.Trigger = TriggerType.Forced;
                        dsAction.Schedule = scheduledNow;

                        return dsAction;

                    } // end-else
                    else
                    {
                        // If event code's equipment type 
                        Log.Warning( string.Format( "{0}No equipment code in \"{1}\", meaning \"{2}\" is not unsupported. ", _nameMsg, scheduledNow, scheduledNow.EventCode ) );
                        Log.Warning( string.Format( "{0}Ignoring schedule", _nameMsg ) );
                    }

                } // end-while

                return null;  // No applicable forced schedules found.
            }
        }

        private DockingStationAction ExamineJournals( DockingStationEvent dsEvent )
        {
            const string funcMsg = "ExamineJournals";
            DockingStationAction action = null;

            /* Per request of product management, the VDS should not ever go into an "Unavailable Leaking" error state. JMP, 9/27/2010
            // Check for leak failure.
            if ( _journals.FindAll( j => j.EventCode.Code == EventCode.Diagnostics && j.Passed == false ).Count > 0 )
            {
                Log.Debug( "Failed Diagnostic detected." );
                return new LeakUnavailableAction();
            }
            */

            if ( !IsDocked() )
                return null;

            // INS-8380 v7.6 Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
            if (HasInstrumentUpgradeScheduleOnPriority())
                return null;

            if (Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal)
            {
                DockingStationAction badPumpTubingActon = new BadPumpTubingDetectedAction(Configuration.DockingStation.SerialNumber);
                badPumpTubingActon.Messages.Add(ConsoleServiceResources.CALIBRATIONSTOPPEDCHECKTUBING);
                return new BadPumpTubingDetectedAction(Configuration.DockingStation.SerialNumber) ;
            }

            if (Master.Instance.SwitchService.BadPumpTubingDetectedDuringBump)
            {
                DockingStationAction badPumpTubingActon = new BadPumpTubingDetectedAction(Configuration.DockingStation.SerialNumber);
                badPumpTubingActon.Messages.Add(ConsoleServiceResources.BUMPSTOPPEDCHECKTUBING); 
                return new BadPumpTubingDetectedAction(Configuration.DockingStation.SerialNumber);
            }

            // SGF  17-Feb-2012  INS-2451
            // Previously, it has been determined that an instrument diagnostics or a calibration is required.
            // No need to check the event journals at this time.
            if (_instrumentDiagnosticsRequired || _calibrationRequired)
                return null;

            Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;

			// Look for failed calibration
			Log.Trace( string.Format( "{0}{1} Checking for failed calibrations...", _nameMsg, funcMsg ) );
			action = FailedLastCalibration( dockedInstrument ); 
			if ( action != null ) return action;

            // Previously, it has been determined that a bump test is required.  
			// No need to perform the below checks at this time which are related to sensors in bump fault.
			if ( _bumpTestRequired )
				return null;

            // Check for failed O2/ClO2 bumps..
            Log.Trace(string.Format("{0}{1} Checking for failed bump tests...", _nameMsg, funcMsg));
            action = FailedLastBumpTest(dockedInstrument);
            if ( action != null ) return action;

            // Look for failed bump tests
            Log.Trace(string.Format("{0}{1} Checking for failed (non-O2 / non-ClO2) bump tests...", _nameMsg, funcMsg));
            bool isCalRequired = FailedLastBumpAndNotCalibratedAfter( dockedInstrument );

			// The FailedLastBumpAndNotCalibratedAfter() method used to return an action, but was changed to use  
			// the _calibrationRequired flag so the calibration action could occur in a certain order in the 
			// GetNextScheduledAction() method.  The isCalRequired flag only indicates if the rules in THIS method 
			// determined a calibration is required for the docked instrument.
			if ( !isCalRequired )
			{
				Log.Trace( string.Format( "{0}{1} No failures recorded in journal.", _nameMsg, funcMsg ) );
			}

			return null;
        }

        /// <summary>
        /// AJAY: INS-8380 Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
        /// If dock belongs to service account, and service account configured to override event priority, check whether instrument has firmware upgrade pending.
        /// If above is true, no need to check journals until instrument is upgraded.
        /// </summary>
        /// <returns></returns>
        private bool HasInstrumentUpgradeScheduleOnPriority()
        {          
            if (Configuration.IsRepairAccount() && Configuration.DockingStation.UpgradeOnErrorFail)
            {
                // we already know that the loaded schedules are for relevant serial numbers, therefore if we find an enabled 
                // instrument firmware upgrade schedule it must be for the docked instrument
                List<Schedule> upgradeSchedules = _schedules.FindAll(s => s.EventCode.Code == EventCode.InstrumentFirmwareUpgrade && s.Enabled);

                // there should never be any relevant event journals for an instrument upgrade as a ScheduledOnce schedule
                // is deleted immediately after being run by the EventProcessor.Save() method
                DateTime lastRunTime = DateTime.MinValue;
                DateTime localDockedTime = Configuration.ToLocalTime(Master.Instance.SwitchService.DockedTime);
                DateTime localNow = Configuration.GetLocalTime();

                // check for an overdue upgrade schedule
                foreach (Schedule upgradeSchedule in upgradeSchedules)
                {
                    DateTime nextRunTime = upgradeSchedule.CalculateNextRunTime(lastRunTime, localDockedTime, Configuration.DockingStation.TimeZoneInfo);

                    // at least one instrument firmware upgrade schedule is overdue
                    if (nextRunTime <= localNow)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a BumpFailureAction if an installed and enabled O2 or ClO2 sensor has a failed bump test status 
        /// or the matching event journal indicates the O2 or ClO2 sensor failed a bump test.
        /// </summary>
        //private BumpFailureAction FailedLastBumpTest( Instrument dockedInstrument )
        //{
        //    const string funcMsg = "FailedBumpTest";

        //    List<InstalledComponent> installedComponents = dockedInstrument.InstalledComponents;
        //    List<InstalledComponent> passedSensors = new List<InstalledComponent>();
        //    CalibrationState instrumentCalState = CalibrationState.Unknown;

        //    // don't waste time calling GetInstrumentCalibrationState if we know that that we can't get RedundantSensorPassed
        //    // as the current instrument cal state
        //    if ( dockedInstrument.SupportsDualSense() && Configuration.IsSingleSensorMode() )
        //        instrumentCalState = dockedInstrument.GetInstrumentCalibrationState( Configuration.IsSingleSensorMode(), passedSensors, null );

        //    // RedundantSensorPassed implies that SingleSensorMode is enabled so we don't need to check it here.
        //    if ( instrumentCalState == CalibrationState.RedundantSensorPassed )
        //    {
        //        // We need to determine if the current sensor is one of the passed sensors in the 
        //        // instrument.  If so, we allow the sensor to be checked for a failed bump.  On the 
        //        // other hand, if this is (one of) the failed sensors, we don't need to ensure that 
        //        // it has had a successful bump, so we can skip it.

        //        // If we know the instrument is in the redundant sensor passed state, then we should only check the event journals
        //        // of the sensors that are reporting they are in a passed state to make sure they too are reporting passed.
        //        Log.Debug( string.Format( "{0}{1} instrument is operating in redundant sensor mode.", _nameMsg, funcMsg ) );
        //        installedComponents = passedSensors;
        //    }

        //    foreach ( string sensorCode in new string[] { SensorCode.O2, SensorCode.ClO2 } )
        //    {
        //        bool foundSensor = false;
        //        bool foundFailedBump = false;

        //        foreach ( InstalledComponent ic in installedComponents )
        //        {
        //            if ( !( ic.Component is Sensor ) ) continue;
        //            if ( ic.Component.Type.Code != sensorCode ) continue;
        //            if ( !ic.Component.Enabled )
        //            {
        //                Log.Debug( string.Format( "{0}{1} ignoring disabled sensor {2}.", _nameMsg, funcMsg, ic.Component.Uid ) );
        //                continue;  // we do not bump test disable sensors
        //            }

        //            // We only need to check a ClO2 sensor's last bump status if there is Cl2 bump gas available.
        //            // i.e., if there is no Cl2 bump gas available, we always just ignore ClO2 sensors.
        //            if ( sensorCode == SensorCode.ClO2
        //            && Configuration.DockingStation.GasEndPoints.Find( g => g.Cylinder.ContainsGas( GasCode.Cl2 ) ) == null )
        //            {
        //                Log.Debug( string.Format( "{0}{1} ignoring ClO2 sensor {2}. No Cl2 gas available for bumping.", _nameMsg, funcMsg, ic.Component.Uid ) );
        //                continue;
        //            }

        //            Sensor sensor = (Sensor)ic.Component;
        //            foundSensor = true;

        //            if ( sensor.BumpTestStatus == false ) //Suresh 22-Feb-2012 INS-2705
        //            {
        //                Log.Debug( string.Format( "{0}{1} detected FAILED bump status for sensor {2}.", _nameMsg, funcMsg, sensor.Uid ) );
        //                foundFailedBump = true;
        //            }
        //            else
        //            {
        //                // Look for a failed bump test event journal record for the sensor.
        //                EventJournal journal = _journals.Find( j => j.Passed == false && j.EventCode.Code == EventCode.BumpTest && j.SerialNumber == sensor.Uid );
        //                if ( journal != null )
        //                {
        //                    Log.Debug( string.Format( "{0}{1} detected FAILED bump event for sensor {2}.", _nameMsg, funcMsg, sensor.Uid ) );
        //                    foundFailedBump = true;
        //                }
        //            }

        //            // if a failed O2 or ClO2 sensor was found, we can ignore it if it has a passed DualSense sibling
        //            if ( foundFailedBump && dockedInstrument.SupportsDualSense() )
        //            {
        //                InstalledComponent siblingComponent = dockedInstrument.GetDualSenseSibling( Configuration.IsSingleSensorMode(), ic );

        //                if ( siblingComponent == null )
        //                {
        //                    Log.Debug( string.Format( "{0}{1} no DualSense sibling returned for sensor {2}.", _nameMsg, funcMsg, sensor.Uid ) );
        //                }
        //                else
        //                {
        //                    // we would only get a sibling if Single-Sensor mode is enabled
        //                    Sensor siblingSensor = (Sensor)siblingComponent.Component;

        //                    if ( siblingSensor.BumpTestStatus == false )
        //                    {
        //                        Log.Debug( string.Format( "{0}{1} detected FAILED bump status for DualSense sibling {2}.", _nameMsg, funcMsg, siblingSensor.Uid ) );
        //                    }
        //                    else
        //                    {
        //                        // sibling has a passed bump status; verify against the event journals
        //                        EventJournal journal = _journals.Find( j => j.Passed == false && j.EventCode.Code == EventCode.BumpTest && j.SerialNumber == siblingSensor.Uid );
        //                        if ( journal != null )
        //                        {
        //                            Log.Debug( string.Format( "{0}{1} detected FAILED bump event for DualSense sibling {2}.", _nameMsg, funcMsg, siblingSensor.Uid ) );
        //                        }
        //                        else
        //                        {
        //                            Log.Debug( string.Format( "{0}{1} ignoring failed bump for sensor {2} as a PASSED DualSense sibling {3} was found.", _nameMsg, funcMsg, sensor.Uid, siblingSensor.Uid ) );
        //                            foundFailedBump = false;
        //                        }
        //                    }
        //                }
        //            }

        //            // once we find one sensor that requires a bump we don't have to look for any others
        //            if ( foundFailedBump )
        //                break;

        //            Log.Trace( string.Format( "{0}{1} detected no failed bump tests for sensor {2}.", _nameMsg, funcMsg, sensor.Uid ) );

        //        } // end-foreach-InstalledComponent

        //        if ( foundFailedBump )
        //        {

        //            // Found a FAILED bump test for the installed sensor; return a BumpFailureAction for that sensor.
        //            Log.Debug( string.Format( "{0}{1} returning BumpFailureAction.", _nameMsg, funcMsg ) );
        //            BumpFailureAction bumpFailureAction = new BumpFailureAction();
        //            bumpFailureAction.Messages.Add( GasType.Cache[ sensorCode.Replace( 'S', 'G' ) ].Symbol );
        //            return bumpFailureAction;
        //        }

        //        if ( foundSensor == false )
        //            Log.Trace( string.Format( "{0}{1} detected no {2} sensors installed in instrument.", _nameMsg, funcMsg, sensorCode ) );

        //    } // end-foreach-SensorCode

        //    return null;
        //}

		/// <summary>
        /// Returns a BumpFailureAction if an installed and enabled O2 or ClO2 sensor (or *any* sensor type
        /// for repair accounts)  has a failed bump test status or the matching event journal indicates
        /// the sensor failed a bump test.
		/// </summary>
        private BumpFailureAction FailedLastBumpTest(Instrument dockedInstrument)
        {
            const string funcMsg = "FailedBumpTest";

			List<InstalledComponent> installedComponents = dockedInstrument.InstalledComponents;
			List<InstalledComponent> passedSensors = new List<InstalledComponent>();
			CalibrationState instrumentCalState = CalibrationState.Unknown;
            
            //INS-8232: Checks whether service account is configured not to proceed in case bump failure is noticed
            bool stopOnFailedBumpTest = Configuration.IsRepairAccount() && Configuration.DockingStation.StopOnFailedBumpTest;

			// don't waste time calling GetInstrumentCalibrationState if we know that that we can't get RedundantSensorPassed
			// as the current instrument cal state
            if ( dockedInstrument.SupportsDualSense() && Configuration.IsSingleSensorMode() )
                instrumentCalState = dockedInstrument.GetInstrumentCalibrationState( Configuration.IsSingleSensorMode(), passedSensors, null );

			// RedundantSensorPassed implies that SingleSensorMode is enabled so we don't need to check it here.
			if ( instrumentCalState == CalibrationState.RedundantSensorPassed )
			{
				// We need to determine if the current sensor is one of the passed sensors in the 
				// instrument.  If so, we allow the sensor to be checked for a failed bump.  On the 
				// other hand, if this is (one of) the failed sensors, we don't need to ensure that 
				// it has had a successful bump, so we can skip it.

				// If we know the instrument is in the redundant sensor passed state, then we should only check the event journals
				// of the sensors that are reporting they are in a passed state to make sure they too are reporting passed.
				Log.Debug( string.Format( "{0}{1} instrument is operating in redundant sensor mode.", _nameMsg, funcMsg ) );
				installedComponents = passedSensors;
			}

            List<Sensor> failedSensorsList = new List<Sensor>();

            foreach ( InstalledComponent ic in installedComponents )
            {
                if ( !( ic.Component is Sensor ) ) continue; 
                if ( !ic.Component.Enabled )
                {
                    Log.Debug( string.Format( "{0}{1} ignoring disabled sensor {2}.", _nameMsg, funcMsg, ic.Component.Uid ) );
                    continue;  // we do not bump test disable sensors
                }

                Sensor sensor = (Sensor)ic.Component;
                bool sensorFailed = false;

                // We only need to check a ClO2 sensor's last bump status if there is Cl2 bump gas available.
                // i.e., if there is no Cl2 bump gas available, we always just ignore ClO2 sensors.
                if ( sensor.Type.Code == SensorCode.ClO2
                &&  Configuration.DockingStation.GasEndPoints.Find( g => g.Cylinder.ContainsGas( GasCode.Cl2 ) ) == null )
                {
                    Log.Debug( string.Format( "{0}{1} ignoring ClO2 sensor {2}. No Cl2 gas available for bumping.", _nameMsg, funcMsg, ic.Component.Uid ) );
                    continue;
                }
                    
                // Is installed sensor saying it's in bump fault?  
                if (!sensor.BumpTestStatus) //Suresh 22-Feb-2012 INS-2705
                {                    
                    Log.Debug(string.Format("{0}{1} detected FAILED bump status for sensor {2}.", _nameMsg, funcMsg, sensor.Uid));
                    sensorFailed = true;
                }
                // Sensor is NOT in bump fault.  So, instead, look for a failed bump test event journal record for the sensor.
                // Again, though, if we're in a repair account that is configured to stop on failed bump test, we care about failure of any sensor type (INS-8232, 7/2017).
                // For all other accounts, though, we only care about O2 and CLO2 failures. 
                else if (stopOnFailedBumpTest || (sensor.Type.Code == SensorCode.O2) || (sensor.Type.Code == SensorCode.ClO2)) 
                {
                    EventJournal journal = _journals.Find(j => j.Passed == false && j.EventCode.Code == EventCode.BumpTest && j.SerialNumber == sensor.Uid);
                    if (journal != null)
                    {
                        Log.Debug(string.Format("{0}{1} detected FAILED bump event for sensor {2}.", _nameMsg, funcMsg, sensor.Uid));
                        sensorFailed = true;
                    }
                }

				// If a failed sensor was found, we can ignore it if it has a passed DualSense sibling.
                if ( sensorFailed && dockedInstrument.SupportsDualSense() )
				{
                    // For repair accounts that are configured to stop on failed bump test, we never want to check for sibling components. i.e., for repair accounts,
                    // when we find a failed sensor of a DuelSense pair, then we want the DS notifiy the techs of the 
                    // failure instead of letting a working sibling sensor cover up the fact that the sensor failed. (INS-8232, 7/2017)
                    InstalledComponent siblingComponent = stopOnFailedBumpTest ? null : dockedInstrument.GetDualSenseSibling( Configuration.IsSingleSensorMode(), ic ); 

					if ( siblingComponent == null )
					{
						Log.Debug( string.Format( "{0}{1} no DualSense sibling returned for sensor {2}.", _nameMsg, funcMsg, sensor.Uid ) );
					}
					else
					{
						// we would only get a sibling if Single-Sensor mode is enabled
						Sensor siblingSensor = (Sensor)siblingComponent.Component;

						if ( siblingSensor.BumpTestStatus == false )
						{
							Log.Debug( string.Format( "{0}{1} detected FAILED bump status for DualSense sibling {2}.", _nameMsg, funcMsg, siblingSensor.Uid ) );
						}
						else
						{
							// sibling has a passed bump status; verify against the event journals
							EventJournal journal = _journals.Find( j => j.Passed == false && j.EventCode.Code == EventCode.BumpTest && j.SerialNumber == siblingSensor.Uid );
							if ( journal != null )
							{
								Log.Debug( string.Format( "{0}{1} detected FAILED bump event for DualSense sibling {2}.", _nameMsg, funcMsg, siblingSensor.Uid ) );
							}
							else
							{
								Log.Debug( string.Format( "{0}{1} ignoring failed bump for sensor {2} as a PASSED DualSense sibling {3} was found.", _nameMsg, funcMsg, sensor.Uid, siblingSensor.Uid ) );
                                sensorFailed = false;
							}
						}
					}
				}

                if (sensorFailed)
                    failedSensorsList.Add(sensor);

                Log.Trace( string.Format("{0}{1} detected no failed bump tests for sensor {2}.", _nameMsg, funcMsg, sensor.Uid ) );

            } // end-foreach-InstalledComponent

            if ( failedSensorsList.Count > 0 )
            {
                // Found a FAILED bump test for the installed sensor; return a BumpFailureAction for that sensor.
                Log.Debug( string.Format("{0}{1} returning BumpFailureAction.", _nameMsg, funcMsg) );
                BumpFailureAction bumpFailureAction = new BumpFailureAction();

                // INS-6777 RHP v7.6 - For more than one sensor that failed the bump, display new message "Bump Failure(gas symbol) Check gas connections".
                if (failedSensorsList.Count > 1)
                    bumpFailureAction.Messages.Add(ConsoleServiceResources.BUMPFAILURECHECKGASCONNECTION);
                // INS-6777 RHP v7.6 - For Non O2/ Non CLO2 sensor that failed the bump on a customer account or service Account with stopOnFailedBumpTest option DISABLED,
                // should automatically initiate Calibration and NOT return bumpFailureAction. 
                else if ((failedSensorsList.Find(s => s.Type.Code == SensorCode.O2 || s.Type.Code == SensorCode.ClO2) == null) && !stopOnFailedBumpTest)
                    return null;

                foreach ( Sensor sensor in failedSensorsList )
                {
                    string sensorSymbol = Master.Instance.ConsoleService.GetSensorLabel(sensor.Type.Code).Replace("(", "").Replace(")", "");
                    // Remove all parenthesis. e.g. convert "LEL (PPM)" to "LEL PPM". Because console's 'action messages' are  
                    // displayed within a set of parenthesis and "(CO, LEL PPM)" looks better on the display than "(CO, LEL (PPM))".
                    if (!bumpFailureAction.Messages.Contains(sensorSymbol)) // INS-8630 RHP v7.5 - Avoid duplicate sensor symbols
                        bumpFailureAction.Messages.Add(sensorSymbol);                    
                }
                return bumpFailureAction;
            }

            return null;
        }

		/// <summary>
		/// Returns a CalibrationFailureAction if any installed sensors that are enabled currently have a failed calibration status.
		/// Also looks for any calibration event journals matching the sensor serial number that are failed.  If the instrument is 
		/// in redundant sensor passed, then the failed sensors can be ignored.
		/// </summary>
        private CalibrationFailureAction FailedLastCalibration( Instrument dockedInstrument )
        {
            const string funcMsg = "FailedLastCalibration";

			List<InstalledComponent> installedComponents = dockedInstrument.InstalledComponents;
			List<InstalledComponent> passedSensors = new List<InstalledComponent>();
			CalibrationState instrumentCalState = CalibrationState.Unknown;
				
			// don't waste time calling GetInstrumentCalibrationState if we know that that we can't get RedundantSensorPassed
			// as the current instrument cal state
            if ( dockedInstrument.SupportsDualSense() && Configuration.IsSingleSensorMode() )
                instrumentCalState = dockedInstrument.GetInstrumentCalibrationState( Configuration.IsSingleSensorMode(), passedSensors, null );

			// RedundantSensorPassed implies that SingleSensorMode is enabled so we don't need to check it here.
			if ( instrumentCalState == CalibrationState.RedundantSensorPassed )
			{
				// If we know the instrument is in the redundant sensor passed state, then we should only check the event journals
				// of the sensors that are reporting they are in a passed state to make sure they too are reporting passed.
				Log.Debug( string.Format( "{0}{1} instrument is operating in redundant sensor mode.", _nameMsg, funcMsg ) );
				installedComponents = passedSensors;
			}

            CalibrationFailureAction calibrationFailureAction = null;
            List<string> failedSensorSymbolList = new List<string>();

            foreach ( InstalledComponent ic in installedComponents )
            {
                if ( !( ic.Component is Sensor ) ) continue;

                if ( !ic.Component.Enabled )
                {
                    Log.Trace(string.Format("{0}{1} ignoring disabled sensor {2}.", _nameMsg, funcMsg, ic.Component.Uid));
                    continue; // ignore disabled sensors.
                }

                // See if sensor itself is marked as failing calibration or zeroing.
                Sensor sensor = ic.Component as Sensor;

                // Get Sensor Cal gas symbol, Remove all parenthesis. e.g. convert "LEL (PPM)" to "LEL PPM". Because console's 'action messages' are  
                // displayed within a set of parenthesis and "(CO, LEL PPM)" looks better on the display than "(CO, LEL (PPM))".
                string sensorSymbol = Master.Instance.ConsoleService.GetSensorLabel(sensor.Type.Code).Replace("(", "").Replace(")", "");
                
                // The following listed Statuses are what InstrumentController.GetStatus currently returns for calibration failure.
                // Note: In calibrationstatus we also check whether Zeroing has failed for the sensor. //Suresh 22-Feb-2012 INS-2705
                if ( SensorGasResponse.IsFailedCalibrationStatus( sensor.CalibrationStatus ) )
                {
                    Log.Debug( string.Format( "{0}{1} detected \"{2}\" status for sensor {3}.", _nameMsg, funcMsg, sensor.CalibrationStatus.ToString(), sensor.Uid ) );
                    if (calibrationFailureAction == null)
                        calibrationFailureAction = new CalibrationFailureAction();
                    if (!failedSensorSymbolList.Contains(sensorSymbol))
                        failedSensorSymbolList.Add(sensorSymbol);
                }
                else
                {
                    // Look for a failed calibration event for the sensor.
                    EventJournal journal = _journals.Find( j => j.Passed == false && j.EventCode.Code == EventCode.Calibration && j.SerialNumber == ic.Component.Uid );

                    if ( journal != null )
                    {
                        Log.Debug( string.Format( "{0}{1} detected FAILED calibration event for sensor {2}.", _nameMsg, funcMsg, sensor.Uid ) );
                        Log.Trace(string.Format("{0}...{1}", _nameMsg, journal.ToString()));
                        if (calibrationFailureAction == null)
                            calibrationFailureAction = new CalibrationFailureAction();
                        if(!failedSensorSymbolList.Contains(sensorSymbol))
                            failedSensorSymbolList.Add(sensorSymbol);
                    }
                }
            }

            if (calibrationFailureAction == null)
                Log.Trace(string.Format("{0}{1} detected no failed calibrations.", _nameMsg, funcMsg));
            else
            {
                // If we failed the last calibration, and we know that this was likely the result of 
                // a hose/cylinder problem, then tell the ConsoleService to display an extra message
                // indicating as much. - INS-1279, 6/20/2011, JMP
                if (Master.Instance.SwitchService.BadGasHookup)
                {
                    calibrationFailureAction.Messages.Add(ConsoleServiceResources.CHECKCYLINDERCONNECTIONS);
                    // INS-8446 RHP v7.6 - Send the SwitchService's BadGasHookUpCylinderPartNumber which is holds the cylinder's part number to display on LCD
                    calibrationFailureAction.Messages.Add(Master.Instance.SwitchService.BadGasHookUpCylinderPartNumber);
                }
                // INS-8630 RHP v7.5 Add all the Failed cal gas symbols for display on LCD
                else
                    calibrationFailureAction.Messages.AddRange(failedSensorSymbolList);
                Log.Debug(string.Format("{0}{1} returning CalibrationFailureAction.", _nameMsg, funcMsg));
            }

            return calibrationFailureAction;
        }

		/// <summary>
		/// Examines sensor bump statuses and bump event journals for non-O2/non-ClO2 sensors to see if any sensor failed a bump test 
		/// and was not calibrated after.  If a sensor is found to have failed its last bump test and there is not a newer calibration 
		/// event journal, then the calibration required flag is set and the method will return true.  If the instrument is in a 
		/// redundant sensor passed state, then only the calibration passed sensors will be checked for this condition.
		/// </summary>
        private bool FailedLastBumpAndNotCalibratedAfter( Instrument dockedInstrument )
        {
            const string funcMsg = "FailedLastBumpAndNotCalibratedAfter";

			List<InstalledComponent> installedComponents = dockedInstrument.InstalledComponents;
            List<InstalledComponent> passedSensors = new List<InstalledComponent>();
			CalibrationState instrumentCalState = CalibrationState.Unknown;

			// don't waste time calling GetInstrumentCalibrationState if we know that that we can't get RedundantSensorPassed
			// as the current instrument cal state
            if ( dockedInstrument.SupportsDualSense() && Configuration.IsSingleSensorMode() )
                instrumentCalState = dockedInstrument.GetInstrumentCalibrationState( Configuration.IsSingleSensorMode(), passedSensors, null );

			// RedundantSensorPassed implies that SingleSensorMode is enabled so we don't need to check it here.
			if ( instrumentCalState == CalibrationState.RedundantSensorPassed )
			{
				// We need to determine if the current sensor is one of the passed sensors in the 
				// instrument.  If so, we allow the sensor to be checked for a failed bump.  On the 
				// other hand, if this is (one of) the failed sensors, we don't need to ensure that 
				// it has had a successful bump, so we can skip it.

				// If we know the instrument is in the redundant sensor passed state, then we should only check the event journals
				// of the sensors that are reporting they are in a passed state to make sure they too are reporting passed.
				Log.Debug( string.Format( "{0}{1} instrument is operating in redundant sensor mode.", _nameMsg, funcMsg ) );
				installedComponents = passedSensors;
			}

            foreach ( InstalledComponent ic in installedComponents )
            {
                if ( !( ic.Component is Sensor ) )
                    continue;

                // failed O2 bumps put the DS into bump failure state
                if ( ic.Component.Type.Code == SensorCode.O2 )
                    continue;

                // failed ClO2 bumps put the DS into bump failure state when Cl2 gas is attached.
                // otherwise, they're ignored when Cl2 gas is not available.
                // We don't check for failed ClO2 bumps here regardless.  If we did, then 
                // we'd try and calibrate (zero) ClO2 sensors whenever they had a failed bump
                // but Cl2 wasn't available.
                if ( ic.Component.Type.Code == SensorCode.ClO2 )
                    continue;  

                if ( !ic.Component.Enabled )
                {
                    Log.Trace( string.Format( "{0}{1} ignoring disabled sensor {2}.", _nameMsg, funcMsg, ic.Component.Uid ) ); 
                    continue;
                }

                Sensor sensor = (Sensor)ic.Component;
				bool calRequired = false;

				// SGF 13-Dec-2012 INS-3712
				// Per Jeff Martin on 28-Aug-2015, if a non-O2 sensor in bump fail is calibrated and passes calibration, 
				// the bump failure flag will be cleared by the instrument.  However, it is unlikely that the cached 
				// sensor state is updated after the calibration operation.  This is okay, because we will have an event journal 
				// for the calibration that occurred after docking.  When the instrument is redocked and rediscovered, the bump 
				// test status should now be in a passed state.
				if ( sensor.BumpTestStatus == false )
				{
					Log.Debug( string.Format( "{0}{1} detected FAILED bump status for sensor {2}.  Checking calibration journal.", _nameMsg, funcMsg, sensor.Uid ) );

					EventJournal calJournal = null;

					// Look for the most recent calibration for the sensor.
					calJournal = _journals.Find( j => j.EventCode.Code == EventCode.Calibration && j.SerialNumber == sensor.Uid );

					// If there is no record of a calibration, or if the last calibration took place before
					// the instrument was docked, issue a calibration action.
					if ( calJournal == null || calJournal.RunTime < Master.Instance.SwitchService.DockedTime )
					{
						Log.Debug( string.Format( "{0}{1} detected FAILED bump status for sensor {2} and has not been calibrated since being docked.", _nameMsg, funcMsg, sensor.Uid ) );
						Log.Debug( string.Format( "{0}{1} calibration will be marked as required.", _nameMsg, funcMsg ) );
						calRequired = true;
					}
				}

				// no need to check event journal if we already know that a cal is required
				if ( !calRequired )
				{
					// Examine the journal entries to see if the sensor failed a bump test.
					// If a non-O2 sensor was in bump fail, and we successfully calibrated on a prior docking.  The sensor will no longer be in bump fail,
					// but we will still have an old event journal record for the sensor saying it failed a bump.  This is okay, because we ignore it if
					// we see a newer cal record.  (It does not matter if the cal record indicates a pass or fail.)
					EventJournal failedBumpJournal = null;
					failedBumpJournal = _journals.Find( j => j.Passed == false && j.EventCode.Code == EventCode.BumpTest && j.SerialNumber == sensor.Uid );
					if ( failedBumpJournal != null )
					{
						// the sensor failed its last bump...initiate a calibration
						Log.Debug( string.Format( "{0}{1} detected FAILED bump event for sensor {2}.  Checking calibration journal.", _nameMsg, funcMsg, sensor.Uid ) );

						EventJournal calJournal = null;

						// Look for a failed calibration for the sensor.
						calJournal = _journals.Find( j => j.EventCode.Code == EventCode.Calibration && j.SerialNumber == sensor.Uid );

						// If there is no record of a calibration, or if the last calibration took place before
						// the failed bump, issue a calibration action.
						if ( calJournal == null || calJournal.RunTime < failedBumpJournal.RunTime )
						{
							Log.Debug( string.Format( "{0}{1} detected FAILED bump event for sensor {2} and has not been calibrated since.", _nameMsg, funcMsg, sensor.Uid ) );
							Log.Debug( string.Format( "{0}{1} calibration will be marked as required.", _nameMsg, funcMsg ) );
							calRequired = true;
						}
					}
				}	

				// once we find one sensor that requires a cal we don't have to look for any others
				if ( calRequired )
				{
					Log.Debug( string.Format( "{0}{1} calibration marked as required.", _nameMsg, funcMsg ) );
					_calibrationRequired = true;
					return true;
				}
            }

            Log.Trace(string.Format("{0}{1} detected no failed bump tests that require a calibration to occur.", _nameMsg, funcMsg));
            return false;
        }

        private DockingStationAction GetNextScheduledAction()
        {
            DateTime localNow = Configuration.GetLocalTime();
            string dockedSn = Master.Instance.SwitchService.Instrument.SerialNumber;
            Log.Debug( string.Format( "{0}NOW: \"{1}\" ({2}), INSTRUMENT: {3}{4}", _nameMsg, Log.DateTimeToString( localNow ),
                Configuration.DockingStation.TimeZoneInfo.GetTimeZoneName( localNow ),
                ( dockedSn.Length > 0 ) ? dockedSn : "None",
                Configuration.IsRepairAccount() ? ", REPAIR ACCOUNT" : string.Empty ) );

            foreach ( EventCode eventCodeCandidate in EventCode.Cache )
            {
                // Through every iteration of this loop, nextRunTime is set to the
                // time the event needs to be run next, if it's not currently overdue.
                // It is calculated for all events, but it really was only added to figure 
                // out the next future calibration date and next future bump date.
                DateTime? nextRunTime = null; // null means 'unknown' which would also be if there are no schedules for the event.

                Log.Trace(string.Format("{0}Evaluating {1} events", _nameMsg, eventCodeCandidate));

                // Skip equipment-based events if nothing is known to be docked.
                if ( eventCodeCandidate.EquipmentTypeCode == EquipmentTypeCode.Instrument && !IsDocked() )
                {
                    Log.Trace(string.Format("{0}Ignoring {1} event as nothing is docked.", _nameMsg, eventCodeCandidate));
                    continue;
                }

				// skip gas operation for instrument if firmware upgrade is pending
				if ( WillSkipEventBecauseInstrumentFirmwareUpgradeAvailable( eventCodeCandidate ) )
				{
					continue;
				}

                // If instrumentDiagnosticsRequired is true, just send back a diagnostics action. No need to check schedules.
                if ( _instrumentDiagnosticsRequired && ( eventCodeCandidate.Code == EventCode.InstrumentDiagnostics ) ) 
                    return CreateAction( EventCode.InstrumentDiagnostics );

                // If calibrationRequired is true, just send back a cal action.  No need to check schedules.
                // There is no need to check schedules.
                if ( _calibrationRequired && ( eventCodeCandidate.Code == EventCode.Calibration ) )
                    return CreateAction( EventCode.Calibration );

                // If bumpTestRequired is true, just send back a bump action.  No need to check schedules.
                // There is no need to check schedules.
                 if ( _bumpTestRequired && ( eventCodeCandidate.Code == EventCode.BumpTest ) )
                    return CreateAction( EventCode.BumpTest );

                // From the list of all schedules, pull out only those for the current event code,
                // and also don't bother with schedules for event types that the instrument type doesn't
                // even support. e.g., there's no use trying to download datalog from a GBPLS, for example.
                List<Schedule> eventSchedules = _schedules.FindAll( s => IsEventCodeSupported( s.EventCode, Master.Instance.SwitchService.Instrument ) && ( s.EventCode.Code == eventCodeCandidate.Code ) );

                // If there are no schedules for the event, then we don't need to consider
                // if or when the event needs run.
                // i.e., we don't bother running events that aren't scheduled to ever run.
                if ( eventSchedules.Count == 0 )
                {
                    Log.Trace(string.Format("{0}No known {1} schedules.", _nameMsg, eventCodeCandidate ) );
                    continue; 
                }

                // From the list of all journals, pull out only those for the current event code.
                List<EventJournal> eventJournals = _journals.FindAll( j => j.EventCode.Code == eventCodeCandidate.Code );

                // First, try to find a non-sensor-specific schedule that is overdue
                OverdueInfo overdueInfo = GetOverdueInstrumentSchedule( eventCodeCandidate, eventSchedules, eventJournals );

                nextRunTime = UpdateNextRunTime( nextRunTime, overdueInfo.NextRunTime );

                if ( overdueInfo.Schedule != null )
                    return CreateAction( overdueInfo.Schedule );

                // If we get here, we do not yet have an option for a schedule to "run".

                // Next, see if there is/are schedules that are sensor-specific.
                overdueInfo = GetOverdueSensorTypeSchedule( eventCodeCandidate, eventSchedules, eventJournals );

                nextRunTime = UpdateNextRunTime( nextRunTime, overdueInfo.NextRunTime );

                if ( overdueInfo.Schedule != null )
                    return CreateAction( overdueInfo.Schedule );

                // INS-2047 ("Need to upload "Next cal date" / "Next bump date")...
                // If we make it to here, the assumption is that the event is not currently
                // overdue.  For Calibration and BumpTests, give the switch service these
                // future dates.  They are held by the switch service for later upload to
                // iNet.
				if ( eventCodeCandidate.Code == EventCode.Calibration )
					Master.Instance.SwitchService.NextUtcCalibrationDate = nextRunTime; // will be automatically converted to UTC.
				else if ( eventCodeCandidate.Code == EventCode.BumpTest )
					Master.Instance.SwitchService.NextUtcBumpDate = nextRunTime; // will be automatically converted to UTC.
				else if ( eventCodeCandidate.Code == EventCode.InstrumentSettingsRead && dockedSn.Length > 0 )
					Master.Instance.SwitchService.InitialInstrumentSettingsNeeded = false;

            } // end-foreach-EventCode

            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="nextLocalRunTime"></param>
        private DateTime? UpdateNextRunTime( DateTime? oldNextRunTime, DateTime? newNextRunTime ) // Added a part of INS-2047 ("Need to upload "Next cal date" / "Next bump date")
        {
            Log.Assert( oldNextRunTime == null || oldNextRunTime.Value.Kind == DateTimeKind.Local, "UpdateNextRunTime - oldNextRunTime is NON-LOCAL! - " + Log.DateTimeToString( oldNextRunTime ) );
            Log.Assert( newNextRunTime == null || newNextRunTime.Value.Kind == DateTimeKind.Local, "UpdateNextRunTime - newNextRunTime is NON-LOCAL! - " + Log.DateTimeToString( newNextRunTime ) );

            if ( newNextRunTime == null || newNextRunTime == DateTime.MinValue )
                return oldNextRunTime;

            oldNextRunTime = ( oldNextRunTime == null || newNextRunTime < oldNextRunTime )
                ? newNextRunTime : oldNextRunTime;

            return oldNextRunTime;
        }

        /// <summary>
        /// Determines if there is a schedule that is overdue for the instrument, or any of 
        /// its sensors, for the specified event type.
        /// </summary>
        /// <param name="eventCode"></param>
        /// <param name="eventSchedules"></param>
        /// <param name="eventJournals"></param>
        /// <returns>
        /// An "OverdueInfo" struct is ALWAYS returned by this method.
        /// 
        /// The returned OverdueInfo will have its Schedule set to the overdue schedule, if there is one.
        /// Otherwise, if nothing overdue, then Schedule will be null.
        /// 
        /// The returned OverdueInfo struct will have it's NextRunTime set to the next schedule run time of
        /// the passed-in event type, whether it's overdue or not yet overdue. If it's determined that
        /// nothing is currently overdue, and nothing is due in the future either, then NextRunTime
        /// will be set to MinDate. This could happen, for example, when when there are no schedules
        /// for the event type, or they're all disabled.
        /// </returns>
        private OverdueInfo GetOverdueInstrumentSchedule( EventCode eventCode, List<Schedule> eventSchedules, List<EventJournal> eventJournals )
        {
            OverdueInfo overdueInfo = default(OverdueInfo);  // will be returned by this  method

            Schedule redundantBumpSchedule = CheckForRedundantSensorBump( eventCode, eventJournals );
            if ( redundantBumpSchedule != null )
            {
                overdueInfo.Schedule = redundantBumpSchedule;
                return overdueInfo;
            }

            DateTime localNow = Configuration.GetLocalTime();

            // Look for any schedule of the currently evaluating event type to be overdue,
            // excepting special schedules that have specific components defined.
            foreach ( Schedule eventSchedule in eventSchedules )
            {
                Log.Trace(string.Format("{0}Scheduled for \"{1}\".", _nameMsg, eventSchedule));

                // Determine if specific sensor types are defined for this schedule.  If so, skip to the next schedule.
                if ( eventSchedule.ComponentCodes.Count > 0 )
                {
                    Log.Trace( string.Format( "{0}Ignoring {1} since specific sensor-types are defined.", _nameMsg, eventSchedule.EventCode ) );
                    continue;
                }

                if ( !eventSchedule.Enabled )
                {
                    Log.Warning( string.Format( "{0}Ignoring disabled {1} schedule (RefId={2})", _nameMsg, eventSchedule.EventCode, eventSchedule.RefId ) );
                    continue;
                }

                // Determine if the current event should be skipped for some reason...
                bool skipEvent = WillSkipEvent( eventCode, eventSchedule, eventJournals, localNow, ref overdueInfo );
                if ( skipEvent )
                {
                    Log.Trace(string.Format("{0}SKIPPING {1} EVENT.", _nameMsg, eventCode)); 
                    continue;
                }

                DateTime localDockedTime = Configuration.ToLocalTime( Master.Instance.SwitchService.DockedTime );

                // Do we have no record of this event ever running yet? Then go ahead and run it,
                // but only as long as the current date is after the schedule's start date.
                if ( eventJournals.Count == 0 )
                {
                    DateTime nextRunTime = eventSchedule.CalculateNextRunTime( DateTime.MinValue, localDockedTime, Configuration.DockingStation.TimeZoneInfo );

                    overdueInfo.NextRunTime = UpdateNextRunTime( overdueInfo.NextRunTime, nextRunTime );

                    bool isOverdue = nextRunTime <= localNow;

                    Log.Trace( string.Format( "{0}...nextRunTime is {1} (overdue={2})", _nameMsg, overdueInfo.NextRunTime, isOverdue ) );
                    if ( isOverdue )
                    {
                        Log.Debug( string.Format( "{0}{1} ({2}) Never run, Next: {3}, OVERDUE!", _nameMsg, eventSchedule, eventSchedule.RefId, Log.DateTimeToString(nextRunTime) ) );
                        overdueInfo.Schedule = eventSchedule;
                        return overdueInfo;
                    }
                    Log.Debug( string.Format( "{0}{1} ({2})...Never run, Next: {3}, NOT Overdue.", _nameMsg, eventSchedule, eventSchedule.RefId, Log.DateTimeToString(nextRunTime) ) );
                }
                else // eventJournals.Count > 0
                {
                    foreach ( EventJournal journal in eventJournals )
                    {
                        if ( IgnoreEventJournal( journal ) )
                            continue;

                        DateTime localJournalRunTime = Configuration.ToLocalTime( journal.RunTime );

                        DateTime nextRunTime = eventSchedule.CalculateNextRunTime( localJournalRunTime, localDockedTime, Configuration.DockingStation.TimeZoneInfo );
                        overdueInfo.NextRunTime = UpdateNextRunTime( overdueInfo.NextRunTime, nextRunTime );
                        bool isOverdue = eventSchedule.IsOverdue( nextRunTime, localNow );
                        //Log.Trace( string.Format( "{0}...nextRunTime is {1} (overdue={2})", _nameMsg, overdueSchedule.NextLocalRunTime, isOverdue ) ); 
                        if ( isOverdue )
                        {
                            Log.Debug(string.Format("{0}{1} ({2})...SN#{3}{4}{5}, Last: {6}, Next: {7}, OVERDUE!",
                                _nameMsg, eventSchedule, eventSchedule.RefId, journal.SerialNumber,
                                (journal.InstrumentSerialNumber == string.Empty) ? string.Empty : " InstSN#",
                                journal.InstrumentSerialNumber,
                                Log.DateTimeToString(localJournalRunTime),
                                Log.DateTimeToString(nextRunTime) ) );
                            overdueInfo.Schedule = eventSchedule;
                            return overdueInfo;
                        }
                        Log.Debug( string.Format( "{0}{1} ({2})...SN#{3}{4}{5}, Last: {6}, Next: {7}, NOT Overdue.",
                            _nameMsg, eventSchedule, eventSchedule.RefId, journal.SerialNumber,
                            (journal.InstrumentSerialNumber == string.Empty) ? string.Empty : " InstSN#",
                            journal.InstrumentSerialNumber,
                            Log.DateTimeToString(localJournalRunTime),
                            Log.DateTimeToString(nextRunTime) ) );
                    }
                }
            }

            // No (non-individual sensor) schedule was found that resulted in an 
            // instrument being overdue for this event type.  Return 'null' to the caller.
            return overdueInfo;
        }

        /// <summary>
        /// Determines if there is a sensor-type schedule that is overdue for any of 
        /// the instrument's sensors, of the specified event type.
        /// </summary>
        /// <param name="eventCode"></param>
        /// <param name="eventSchedules"></param>
        /// <param name="eventJournals"></param>
        /// <returns>
        /// An "OverdueInfo" struct is ALWAYS returned by this method.
        /// 
        /// The returned OverdueInfo will have its Schedule set to the overdue schedule, if there is one.
        /// Otherwise, if nothing overdue, then Schedule will be null.
        /// 
        /// The returned OverdueInfo struct will have it's NextRunTime set to the next schedule run time of
        /// the passed-in event type, whether it's overdue or not yet overdue. If it's determined that
        /// nothing is currently overdue, and nothing is due in the future either, then NextRunTime
        /// will be set to MinDate. This could happen, for example, when there are no schedules
        /// for the event type, or they're all disabled.
        /// </returns>
        private OverdueInfo GetOverdueSensorTypeSchedule( EventCode eventCode, List<Schedule> eventSchedules, List<EventJournal> eventJournals )
        {
            OverdueInfo overdueInfo = default(OverdueInfo);  // will be returned by this method

            DateTime localNow = Configuration.GetLocalTime();
            Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;

            Schedule redundantBumpSchedule = CheckForRedundantSensorBump(eventCode, eventJournals);
            if ( redundantBumpSchedule != null )
            {
                overdueInfo.Schedule = redundantBumpSchedule;
                return overdueInfo;
            }

            // SGF  20-Jan-2012  INS-2241
            Schedule compositeSensorSchedule = null;

            List<String> sensorCodes = new List<string>();

            // Look for any schedule of the currently evaluating event type to be overdue
            // that have specific components defined.  Ignore all other schedules.
            foreach ( Schedule eventSchedule in eventSchedules )
            {
                Log.Trace(string.Format("{0}Scheduled for \"{1}\".", _nameMsg, eventSchedule));

                // Determine if specific sensor types are defined for this schedule.  If not, skip to the next schedule.
                if ( eventSchedule.ComponentCodes.Count == 0 )
                {
                    Log.Trace(string.Format("{0}Ignoring since it is not an individual-sensor schedule.", _nameMsg));
                    continue;
                }

                if ( !eventSchedule.Enabled )
                {
                    Log.Warning( string.Format( "{0}Ignoring disabled sensor {1} schedule", _nameMsg, eventCode.Code ) );
                    continue;
                }

                // Determine if the current event should be skipped for some reason...
                bool skipEvent = WillSkipEvent( eventCode, eventSchedule, eventJournals, localNow, ref overdueInfo );
                if ( skipEvent )
                {
                    Log.Trace(string.Format("{0}SKIPPING {1} EVENT.", _nameMsg, eventCode));
                    continue;
                }

                bool isOverdue = false;

                DateTime localDockedTime = Configuration.ToLocalTime( Master.Instance.SwitchService.DockedTime );

                // Do we have no record of this event ever running yet? Then go ahead and run it,
                // but only as long as the current date is after the schedule's start date.
                if ( eventJournals.Count == 0 )
                {
                    DateTime nextRunTime = eventSchedule.CalculateNextRunTime( DateTime.MinValue, localDockedTime, Configuration.DockingStation.TimeZoneInfo );
                    overdueInfo.NextRunTime = UpdateNextRunTime( overdueInfo.NextRunTime, nextRunTime );
                    isOverdue = nextRunTime <= localNow;
                    Log.Trace( string.Format( "{0}...nextRunTime is {1} (overdue={2})", _nameMsg, nextRunTime, isOverdue ) );
                    if ( isOverdue )
                        Log.Debug( string.Format( "{0}\"{1}\" never run, Next: {2}, OVERDUE!", _nameMsg, eventSchedule, Log.DateTimeToString(nextRunTime) ) );
                }
                else // eventJournals.Count > 0
                {
                    bool isDocked = IsDocked();

                    foreach ( EventJournal journal in eventJournals )
                    {
                        // Find out what kind of sensor was reported on with the current event journal
                        string sensorCode = GetSensorCode( journal.SerialNumber );

                        // Determine if that type of sensor is referenced by the current schedule
                        if ( eventSchedule.ComponentCodes.Contains( sensorCode ) == false )
                            continue;

                        if ( isDocked )
                        {
                            InstalledComponent ic = dockedInstrument.GetInstalledComponentByUid(journal.SerialNumber);
                            if ( ic != null && ic.Component.Enabled == false)
                            {
                                // This journal entry is for a sensor that (a) is installed in the instrument, and (b) is disabled.
                                // Skip this journal entry and continue to the next one.
                                Log.Trace(string.Format("{0}...SN#{1}, disabled, NOT Overdue.", _nameMsg, journal.SerialNumber));
                                continue;
                            }
                        }

                        // Get the next run time for the schedule, and determine if the schedule is overdue
                        DateTime localJournalRunTime = Configuration.ToLocalTime( journal.RunTime );
                        DateTime nextRunTime = eventSchedule.CalculateNextRunTime( localJournalRunTime, localDockedTime, Configuration.DockingStation.TimeZoneInfo );
                        overdueInfo.NextRunTime = UpdateNextRunTime( overdueInfo.NextRunTime, nextRunTime );

                        isOverdue = eventSchedule.IsOverdue( nextRunTime, localNow );

                        Log.Trace( string.Format( "{0}...nextRunTime is {1} (overdue={2})", _nameMsg, nextRunTime, isOverdue ) );

                        if ( isOverdue )
                        {
                            // this schedule is overdue, so break from the loop
                            Log.Debug(string.Format("{0}\"{1}\"...SN#{2}{3}{4}, Last: {5}, Next: {6}, OVERDUE!",
                                _nameMsg, eventSchedule, journal.SerialNumber,
                                (journal.InstrumentSerialNumber == string.Empty) ? string.Empty : " InstSN#",
                                journal.InstrumentSerialNumber,
                                Log.DateTimeToString(localJournalRunTime),
                                Log.DateTimeToString(nextRunTime ) ) );
                            break;
                        }

                        // this schedule is not overdue for this sensor type, so continue to the next journal
                        Log.Debug(string.Format("{0}{1}...SN#{2}{3}{4}, Last: {5}, Next: {6}, NOT Overdue.",
                            _nameMsg, eventCode, journal.SerialNumber,
                            (journal.InstrumentSerialNumber == string.Empty) ? string.Empty : " InstSN#",
                            journal.InstrumentSerialNumber,
                            Log.DateTimeToString(localJournalRunTime),
                            Log.DateTimeToString(nextRunTime) ) );
                    }
                }

                if ( isOverdue )
                {
                    // make sure we have created a composite sensor schedule
                    if ( compositeSensorSchedule == null )
                        compositeSensorSchedule = eventSchedule;

                    // add all sensor codes from the current schedule to the list of codes for the composite schedule
                    foreach ( string currSensorCode in eventSchedule.ComponentCodes )
                    {
                        if ( sensorCodes.Contains( currSensorCode ) == false )
                            sensorCodes.Add( currSensorCode );
                    }
                }

            }  // end-foreach Schedule

            if ( compositeSensorSchedule != null )
                compositeSensorSchedule.ComponentCodes = sensorCodes;

            overdueInfo.Schedule = compositeSensorSchedule;

            return overdueInfo;
        }

        /// <summary>
        /// Helper method intended for use by GetNextScheduledInstrumentAction() and GetNextScheduledSensorAction().
        /// Allows those methods to skip event journal entries for special reasons.
        /// </summary>
        /// <param name="journal"></param>
        /// <returns></returns>
        private bool IgnoreEventJournal( EventJournal journal )
        {
            // Skip event journals for sensors that are disabled.
            InstalledComponent installedComponent = Master.Instance.SwitchService.Instrument.GetInstalledComponentByUid( journal.SerialNumber );
            if ( installedComponent != null && installedComponent.Component.Enabled == false )
            {
                // This journal entry is for a sensor that (a) is installed in the instrument, and (b) is disabled.
                // Skip this journal entry and continue to the next one.
                Log.Trace( string.Format( "{0}...SN#{1}, disabled, NOT Overdue.", _nameMsg, journal.SerialNumber ) );
                return true;
            }

            // If single-sensor mode is enabled, and one of the sensors is failed, then skip bump journals for failed sensor.
            // Can't bump test sensors that are in cal fail state, bump operation will know to not
            // bump them anyways, so the'll still be overdue if we try.
            if ( Configuration.IsSingleSensorMode() && ( journal.EventCode.Code == EventCode.BumpTest ) )
            {
                List<InstalledComponent> failedSensors = new List<InstalledComponent>();
                if ( Master.Instance.SwitchService.Instrument.GetInstrumentCalibrationState( Configuration.IsSingleSensorMode(), null, failedSensors ) == CalibrationState.RedundantSensorPassed
                && failedSensors.Find( ic => ic.Component.Uid == journal.SerialNumber ) != null )
                {
                    //Log.Trace( string.Format( "{0}...SN#{1}, IsSingleSensorRedundantMode=true and CalibrationState=RedundantSensorPassed", _nameMsg, journal.SerialNumber ) );
                    Log.Trace( string.Format( "{0}...SN#{1}, Ignoring {2} journal due to failed sensor calibration", _nameMsg, journal.SerialNumber, journal.EventCode  ) );
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// TO DO - What the heck is this doing? - JMP
        /// </summary>
        /// <param name="eventCode"></param>
        /// <param name="eventJournals"></param>
        /// <returns></returns>
        private Schedule CheckForRedundantSensorBump( EventCode eventCode, List<EventJournal> eventJournals ) // // SGF  20-Nov-2012  INS-3520
        {
			// This logic is only for TX1 and does not apply to VPRO
            if ( eventCode.Code != EventCode.BumpTest || Configuration.DockingStation.Type != DeviceType.TX1 )
                return null;

            if ( !Configuration.IsSingleSensorMode() )
                return null;

            List<InstalledComponent> passedSensors = new List<InstalledComponent>();
            CalibrationState calState = Master.Instance.SwitchService.Instrument.GetInstrumentCalibrationState( Configuration.IsSingleSensorMode(), passedSensors, null );

            if ( calState != CalibrationState.RedundantSensorPassed )
                return null;

            DateTime localDockedTime = Configuration.ToLocalTime(Master.Instance.SwitchService.DockedTime);
            InstalledComponent passedComponent = passedSensors[0];
            Sensor passedSensor = (Sensor)passedComponent.Component;

            Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;
            Schedule redundantUponDockingSchedule = null;

            foreach (EventJournal journal in eventJournals)
            {
                if ( journal.SerialNumber != passedSensor.Uid )
                    continue;

                InstalledComponent ic = dockedInstrument.GetInstalledComponentByUid(journal.SerialNumber);
                if ( ic != null && ic.Component.Enabled == false )
                {
                    // This journal entry is for a sensor that (a) is installed in the instrument, and (b) is disabled.
                    // Skip this journal entry and continue to the next one.
                    Log.Trace(string.Format("{0}...SN#{1}, disabled, NOT Overdue.", _nameMsg, journal.SerialNumber));
                    continue;
                }

                DateTime localJournalRunTime = Configuration.ToLocalTime(journal.RunTime);
                if (localJournalRunTime < localDockedTime)
                {
                    Log.Debug(string.Format("{0}{1}...SN#{2}{3}{4}, Last: {5}, Redundant sensor, BUMP ON DOCKING!",
                        _nameMsg, eventCode, journal.SerialNumber,
                        (journal.InstrumentSerialNumber == string.Empty) ? string.Empty : " InstSN#",
                        journal.InstrumentSerialNumber,
                        Log.DateTimeToString(localJournalRunTime)));

                    redundantUponDockingSchedule = new ScheduledUponDocking(DomainModelConstant.NullId,
                                                                            DomainModelConstant.NullId,
                                                                            string.Empty,
                                                                            EventCode.GetCachedCode(EventCode.BumpTest),
																			null, null,
                                                                            true);
                    return redundantUponDockingSchedule;
                }
            }

            return redundantUponDockingSchedule;
        }

        private string GetSensorCode( string sensorSN )
        {
            string sensorCode = "";
            int delimiterIndex = sensorSN.IndexOf( '#' );
            if ( delimiterIndex >= 0 )
            {
                sensorCode = sensorSN.Substring( delimiterIndex + 1 );
            }
            return sensorCode;
        }


        /// <summary>
        /// Helper method intended for use by GetNextScheduledInstrumentAction() and GetNextScheduledSensorAction().
        /// Determines if the specified event is to be skipped.
        /// </summary>
        /// <param name="eventCode"></param>
        /// <param name="eventSchedule"></param>
        /// <param name="localNow">The current time in the docking station's current time zone.</param>
        /// <returns>
        /// True if the specified current event is to be skipped.
        /// </returns>
        private bool WillSkipEvent( EventCode eventCode, Schedule eventSchedule, List<EventJournal> eventJournals, DateTime localNow, ref OverdueInfo overdueInfo )
        {
            const string funcName = "WillSkipEvent: ";

            // Some events are not allowed to run if there's no current connection to iNet.
            if ( WillSkipEventBecauseNoInet( eventCode ) )
                return true;

            switch ( eventCode.Code )
            {
                case EventCode.BumpTest:
                    
                    // Since this is a bump test, it is possible to skip the event.  We must
                    // consider several issues.

                    // SGF  27-Oct-2010  DSW-381 (DS2 v8.0)  INS-1622
                    // Determine if the user has specified that they would like for scheduled
                    // bump tests to occur immediately after scheduled calibrations.  If they have,
                    // then the bump test operation will not be skipped.
                    if ( eventSchedule.ScheduleProperties.Exists( sp => sp.Attribute == ScheduleProperty.ATTR_ALLOWBUMPAFTERCAL && bool.Parse( sp.Value ) ) )
                        return false;

                    // If there are no journal entries for bump testing, do not skip the event.
                    if ( eventJournals.Count <= 0 )
                        return false;

                    // Next, we must obtain the time of instrument docking and the time of last calibration.

                    Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;
                    DateTime dockedTime = Master.Instance.SwitchService.DockedTime;
                    DateTime lastCalTime = DateTime.MinValue;

                    List<EventJournal> calEventJournals = _journals.FindAll( j => j.EventCode.Code == EventCode.Calibration );
                    foreach ( EventJournal journal in calEventJournals )
                    {
                        if ( journal.InstrumentSerialNumber != dockedInstrument.SerialNumber )
                            continue;

                        // Get the current component that corresponds to the journal serial number.
                        InstalledComponent installedComponent = dockedInstrument.GetInstalledComponentByUid(journal.SerialNumber);

                        // Ensure that this component is a sensor.
                        if (!(installedComponent.Component is Sensor))
                            continue;

                        // Ensure that this sensor is currently enabled.  We will not concern ourselves with disabled sensors.
                        if (installedComponent.Component.Enabled == false)
                            continue;

                        // At this point, we know the journal is for an enabled sensor.  We will get
                        // the last calibration date/time from this sensor.

                        lastCalTime = journal.RunTime;
                        break;
                    }
                    
                    if ( lastCalTime < dockedTime )
                    {
                        // Since we are considering a bump test, and the last calibration took place on a prior
                        // docking of the instrument, there is no need to skip the bump test.  Allow normal 
                        // scheduling to determine if a bump test is to be run.
                        //
                        Log.Debug( string.Format( "{0}{1}last cal time precedes instrument dock time -- DO NOT SKIP BUMP TEST.", _nameMsg, funcName ) ); 
                        return false;
                    }

                    // We must obtain the time the instrument was last bump tested, and compare it to 
                    // the last calibration time.
                    //
                    DateTime lastBumpTime = DateTime.MinValue;
                    foreach (EventJournal journal in eventJournals)
                    {
                        if (journal.InstrumentSerialNumber != dockedInstrument.SerialNumber)
                            continue;
                        lastBumpTime = journal.RunTime;
                        break;
                    }

                    if ( lastCalTime < lastBumpTime )
                    {
                        // Since we are considering a bump test, and the last calibration took place after
                        // the instrument was docked, we need to consider if a bump test has been run subsequent
                        // to the calibration.  If a bump test has been run, we know the instrument has been 
                        // docked for some time.  Allow normal scheduling to determine if a bump test is to be run.
                        //
                        Log.Debug( string.Format( "{0}{1}last cal time precedes last bump time -- DO NOT SKIP BUMP TEST.", _nameMsg, funcName ) );
                        return false;
                    }

                    // Since we have gotten here, we know:
                    //     a. this is a bump test
                    //     b. the last calibration took place since the instrument was docked
                    //     c. the docking station has not yet run a bump test since the last calibration
                    // In this situation, we will determine if a bump test is overdue, but using the 
                    // last calibration time for comparison.  If the bump test is overdue, then we must
                    // allow the event to run.  If the event is not overdue, then we will skip the 
                    // bump test event.

                    DateTime localLastCalTime = Configuration.ToLocalTime( lastCalTime );
                    DateTime localDockedTime = Configuration.ToLocalTime( dockedTime );

                    DateTime nextRunTime = eventSchedule.CalculateNextRunTime( localLastCalTime, localDockedTime, Configuration.DockingStation.TimeZoneInfo );
                    bool isOverdue = eventSchedule.IsOverdue( nextRunTime, localNow );
                    if ( isOverdue )
                    {
                        Log.Debug( string.Format( "{0}{1}{2} is overdue ({3}). DO NOT SKIP BUMP TEST.", _nameMsg, funcName, eventSchedule, Log.DateTimeToString( nextRunTime ) ) );
                    }
                    else  // !isOverdue
                    {
                        Log.Debug( string.Format( "{0}{1}{2} is not overdue ({3}). SKIP BUMP TEST.", _nameMsg, funcName, eventSchedule, Log.DateTimeToString(nextRunTime) ) );
                        overdueInfo.NextRunTime = UpdateNextRunTime( overdueInfo.NextRunTime, nextRunTime );
                    }

                    return !isOverdue;

                    // This break is commented out since it causes a compiler warning...
                    //break;  // end-EventCode.BumpTest

                // Do Not Allow docking station upgrade If there is Data in the iNet Queue
                case EventCode.FirmwareUpgrade:
                    PersistedQueue persistedQueue = _queueDataAccess == null ? PersistedQueue.CreateInetInstance() : PersistedQueue.CreateInetInstance(_queueDataAccess);
                    long queueCount = persistedQueue.GetCount();
                    if (queueCount > 0)
                    {
                        Log.Warning( string.Format( "Skipping {0} due to {1} items in the upload queue.", eventCode, queueCount ) );
                        return true;
                    }
                    break;
            }
            // We did not find a reason to skip the specified event.  Report false to indicate that.
            return false;
        }

        /// <summary>
        /// Helper method for WillSkipEvent().
        /// Returns true if event type should not be run because it requires a connection
        /// to iNet which currently does not exist.
        /// We don't bother running them until we know we're connected to iNet.
        /// </summary>
        /// <param name="eventCode"></param>
        private bool WillSkipEventBecauseNoInet( EventCode eventCode )
        {
            if ( eventCode.Code != EventCode.UploadDebugLog
            &&   eventCode.Code != EventCode.FirmwareUpgrade
            &&   eventCode.Code != EventCode.InstrumentFirmwareUpgrade )
                return false;
            
            if ( Inet.IsOnline )
                return false;

            Log.Warning(string.Format("Skipping {0} due to lack of required iNet connection.", eventCode));
            return true;
        }

		/// <summary>
		/// Checks to see if event candidate can be skipped from being run.  This check only applies to gas operations
		/// that would be re-run once a pending instrument firmware upgrade is performed.
		/// </summary>
		/// <param name="eventCode">The event candidate that may need skipped.</param>
		/// <returns>True if event candidate should be skipped.</returns>
		private bool WillSkipEventBecauseInstrumentFirmwareUpgradeAvailable( EventCode eventCode )
		{
			// this check is only to skip calibrations and bump tests...
			if ( eventCode.Code != EventCode.BumpTest && eventCode.Code != EventCode.Calibration  )
			{
				return false;
			}
			
			// we already know that the loaded schedules are for relevant serial numbers, therefore if we find an enabled 
			// instrument firmware upgrade schedule it must be for the docked instrument
			List<Schedule> upgradeSchedules = _schedules.FindAll( s => s.EventCode.Code == EventCode.InstrumentFirmwareUpgrade && s.Enabled );

			// there should never be any relevant event journals for an instrument upgrade as a ScheduledOnce schedule
			// is deleted immediately after being run by the EventProcessor.Save() method
			DateTime lastRunTime = DateTime.MinValue;
			DateTime localDockedTime = Configuration.ToLocalTime( Master.Instance.SwitchService.DockedTime );
			DateTime localNow = Configuration.GetLocalTime();

			// check for an overdue upgrade schedule
			bool isUpgradePending = false;
			foreach ( Schedule upgradeSchedule in upgradeSchedules )
			{
				DateTime nextRunTime = upgradeSchedule.CalculateNextRunTime( lastRunTime, localDockedTime, Configuration.DockingStation.TimeZoneInfo );

				if ( nextRunTime <= localNow )
				{
					// at least one instrument firmware upgrade schedule is overdue
					isUpgradePending = true;
					break;
				}
			}
			
			if ( !isUpgradePending )
			{
				// no firmware upgrade pending for docked instrument
				return false;
			}

			Log.Trace( string.Format( "{0}Detected pending {1} for docked instrument.", _nameMsg, EventCode.InstrumentFirmwareUpgrade ) );

			if ( !Inet.IsOnline )
			{
				// need to be online with iNet to download firmware;
				// this check could be done before checking if an upgrade is pending,
				// but it is done here for the specific log message
				Log.Trace( string.Format( "{0}Will not skip {1} for pending {2} due to lack of required iNet connection.", _nameMsg, eventCode.Code, EventCode.InstrumentFirmwareUpgrade ) );
				return false;
			}

			// this check could be done before checking if an upgrade is pending, but the assumption
			// is the majority of users will not have this enabled so do the check later to save time
			if ( _schedules.Find( s => s.EventCode.Code == EventCode.DataDownloadPause && s.Enabled ) != null )
			{
				// don't skip gas operations if the account has the pause operation enabled;
				// it does not matter if the pause is overdue or not, so there is no need to 
				// check the journals and calculate the next run time
				Log.Trace( string.Format( "{0}Will not skip {1} for pending {2} due to {3} being enabled.", _nameMsg, eventCode.Code, EventCode.InstrumentFirmwareUpgrade, EventCode.DataDownloadPause ) );
				return false;
			}
			
			// skip gas operation as it will be unnecessary once instrument firmware upgrade happens
			Log.Debug( string.Format( "{0}SKIPPING {1} DUE TO PENDING {2} FOR DOCKED INSTRUMENT!", _nameMsg, eventCode.Code, EventCode.InstrumentFirmwareUpgrade ) );
			return true;
		}

        /// <summary>
        /// Determines if the specified event is supported by the specified instrument type.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="device">
        /// </param>
        /// <returns></returns>
        private bool IsEventCodeSupported( EventCode eventCode, Device device )
        {
            // GBPLS instruments have no datalog data.  Dont' waste time trying to download it.
            if ( eventCode.Code == EventCode.DownloadDatalog && device.Type == DeviceType.GBPLS )
                return false;

            // Some instruments have implemented the feature of storing manual gas operation data.
            // Check the instrument type definition to determine if the current instrument has the feature.
            if (eventCode.Code == EventCode.DownloadManualOperations)
            {
                InstrumentTypeDefinition instTypeDef = null;
                switch (device.Type)
                {
					case DeviceType.MX4:
						instTypeDef = new Mx4Definition( device.SoftwareVersion );
						break;
					case DeviceType.VPRO:
						instTypeDef = new VentisProDefinition( device.SoftwareVersion );
						break;
					case DeviceType.MX6:
                        instTypeDef = new Mx6Definition( device.SoftwareVersion );
                        break;
					case DeviceType.SC:
						instTypeDef = new SafeCoreDefinition( device.SoftwareVersion );
						break;
					case DeviceType.TX1:
						instTypeDef = new Tx1Definition( device.SoftwareVersion );
						break;
                    case DeviceType.GBPRO: 
                        instTypeDef = new GbProDefinition( device.SoftwareVersion );
                        break;
                    default:
                        return false;
                }
                return instTypeDef.HasGasOperationsLogFeature;
            }

            return true;
        }

        private DockingStationAction CreateAction( Schedule schedule )
        {
            DockingStationAction dsAction = CreateAction( schedule.EventCode, schedule );

            // NothingAction will be returned for unsupported EventCodes.
            // In that situation, do not set the Schedule, as it makes no 
            // sense for a NothingAction to be considered scheduled.
            if ( !( dsAction is NothingAction ) )
            {
                dsAction.Schedule = schedule;

                // SGF  03-Nov-2010  Single Sensor Cal and Bump
                if ( dsAction is InstrumentGasAction )
                {
                    ((InstrumentGasAction)dsAction).ComponentCodes = schedule.ComponentCodes;
                }
                //Right now the properties are for only firmware and bump test, though we can allow them to assign for any events and can remove this check
                if ( dsAction is InstrumentFirmwareUpgradeAction || dsAction is InstrumentGasAction )
                {
                    ((InstrumentAction)dsAction).ScheduleProperties = schedule.ScheduleProperties;
                }
            }

            return dsAction;
        }

        private DockingStationAction CreateAction( string eventCodeString )
        {
            EventCode eventCode = EventCode.GetCachedCode( eventCodeString );
            return CreateAction( eventCode, null );
        }

        private DockingStationAction CreateAction( EventCode eventCode )
        {
            return CreateAction( eventCode, null );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventCode"></param>
        /// <returns>
        /// Note that the returned action as a TriggerType of Scheduled.
        /// </returns>
        private DockingStationAction CreateAction( EventCode eventCode, Schedule schedule )
        {
            DockingStationAction dsAction = eventCode.CreateAction();

            // Mark the action's trigger as follows:
            // MANUAL: a schedule "ScheduledNow" is provided
            // UNSCHEDULED: either there is no schedule or the schedule has no ID;
            // SCHEDULED: a schedule (with ID) is provided, and is any other type of schedule other than "ScheduledNow"
            //
            if (schedule == null)
            {
                dsAction.Trigger = TriggerType.Unscheduled;
                Log.Debug(string.Format("{0}CreateAction: EventCode={1}, Schedule=NULL, Trigger={2}", _nameMsg, eventCode.ToString(), dsAction.Trigger.ToString()));
            }
            else
            {
                string scheduleTypeName = schedule.GetType().Name;
                string refIdString = schedule.RefId.ToString();

                if (schedule is ScheduledNow)
                {
                    dsAction.Trigger = TriggerType.Forced;
                    Log.Debug(string.Format("{0}CreateAction: EventCode={1}, Schedule={2}, RefId={3}, Trigger={4}", _nameMsg, eventCode.ToString(), scheduleTypeName, refIdString, dsAction.Trigger.ToString()));
                }
                else if (schedule.RefId == DomainModelConstant.NullId)
                {
                    dsAction.Trigger = TriggerType.Unscheduled;
                    Log.Debug(string.Format("{0}CreateAction: EventCode={1}, Schedule={2}, RefId={3}, Trigger={4}", _nameMsg, eventCode.ToString(), scheduleTypeName, refIdString, dsAction.Trigger.ToString()));
                }
                else
                {
                    dsAction.Trigger = TriggerType.Scheduled;
                    Log.Debug(string.Format("{0}CreateAction: EventCode={1}, Schedule={2}, RefId={3}, Trigger={4}", _nameMsg, eventCode.ToString(), scheduleTypeName, refIdString, dsAction.Trigger.ToString()));
                }
            }

            return dsAction;
        }

        /// <summary>
        /// Returns the serial numbers for this docking station, its currently docked
        /// instrument and all of its installed sensors.
        /// </summary>
        /// <returns></returns>
        private string[] GetDockedSerialNumbers()
        {
            List<string> snList = new List<string>();

            // Start out with this docking station's serial number.
            snList.Add( Configuration.DockingStation.SerialNumber );

            if ( IsDocked() )
                snList.AddRange( Master.Instance.SwitchService.Instrument.GetSerialNumbers() );

            return snList.ToArray();
        }

        /// <summary>
        /// Returns the component codes for the docked instrument
        /// </summary>
        /// <returns></returns>
        private string[] GetComponentCodes()
        {
            List<string> componentCodeList = new List<string>();

            if ( IsDocked() )
            {
                foreach ( InstalledComponent installedComponent in Master.Instance.SwitchService.Instrument.InstalledComponents )
                {
                    componentCodeList.Add( installedComponent.Component.Type.Code );
                }
            }

            return componentCodeList.ToArray();
        }

        /// <summary>
        /// Creates and returns an instance of a Schedule that matches the currently configured CalStationSchedule.
        /// If current CalStationSchedule is "None", then null is returned. 
        /// </summary>
        /// <returns>
        /// If current CalStationSchedule is "None", then null is returned. 
        /// </returns>
        private Schedule CreateCalStationGasSchedule()
        {
            if ( Configuration.DockingStation.CalStationGasSchedule == CalStationGasSchedule.BumpUponDocking )
                return new ScheduledUponDocking( DomainModelConstant.NullId, DomainModelConstant.NullId, "Cal Station - Bump Upon Docking", EventCode.GetCachedCode(EventCode.BumpTest), null, null, true );

            if ( Configuration.DockingStation.CalStationGasSchedule == CalStationGasSchedule.CalUponDocking )
                return new ScheduledUponDocking( DomainModelConstant.NullId, DomainModelConstant.NullId, "Cal Station - Cal Upon Docking", EventCode.GetCachedCode( EventCode.Calibration ), null, null, true );

            return null;
        }

		/// <summary>
		/// Creates and returns an instance of a Schedule to perform a DownloadDatalog upon docking when in
		/// Cal Station mode, the schedule is currently enabled, and if a USB drive is currently attached.
		/// 
		/// GBPLS is not supported.
		/// </summary>
		/// <returns>
		/// If the schedule is not enabled, or a USB drive is not attached, or Service mode is enabled, then null is returned.
		/// </returns>
		private Schedule CreateCalStationDatalogSchedule()
		{
			// GBPLS does not support datalog. 
			if ( Configuration.DockingStation.Type != DeviceType.GBPLS )
			{
				// Schedule must be enabled and a USB drive must be attached so the datalog can be saved somewhere.  
				// Do not create schedule if currently in Service mode.
				if ( Configuration.DockingStation.CalStationDatalogScheduleEnabled && Master.Instance.ControllerWrapper.IsUsbDriveAttached( _nameMsg ) && !Configuration.ServiceMode )
				{
					return new ScheduledUponDocking( DomainModelConstant.NullId, DomainModelConstant.NullId, "Cal Station - Download Datalog Upon Docking", EventCode.GetCachedCode( EventCode.DownloadDatalog ), null, null, true );
				}
			}

			return null;
		}

        /// <summary>
        /// Load all persisted Schedules and EventJournals for the specified serial
        /// numbers. Loaded data is placed into Schedules and Journals member variables.
        /// </summary>
        /// <remarks>
        /// The Schedules and EventJournals are all loaded here at the same time in
        /// order to share the DataAccessTransation (improved performance that way versus
        /// separate transactions).
        /// </remarks>
        /// <param name="serialNumbers"></param>
        /// <param name="dsEvent"></param>
        private void LoadSchedulingData( string[] serialNumbers, string[] componentCodes )
        {
            //INS-2460 - SubType applies only at Instrument (Ventis, VPRO, iQuad etc) and not for DSX
            //Pass the Equipment Type for both Instrument and DSX, since now the Global Type Schedule applies for both of them (firmware) and we need to do it.            
            //If VPRO is the instrument type, then the DSX will be MX4, in that case we need to pass both the equipment type to get the schedules relevant to that.
            List<string> equipmentCodes = new List<string>();
            DeviceType instrumentType = Master.Instance.SwitchService.Instrument.Type;
            DeviceType dockingStationType = Configuration.DockingStation.Type;
            string instrumentEquipmentCode = instrumentType.ToString();            
            DeviceSubType equipmentSubType = Master.Instance.SwitchService.Instrument.Subtype;

            equipmentCodes.Add(dockingStationType.ToString());
            if (instrumentType != DeviceType.Unknown && dockingStationType != instrumentType)
            {
                equipmentCodes.Add(instrumentEquipmentCode);
            }

            List<Schedule> globalSchedules = new List<Schedule>();

            // If we're not activated on any account, and we're in service mode, then this
            // DS is probably being manufactured in the workcell.  Don't do any scheduling.
            if ( !Configuration.Schema.Activated && Configuration.ServiceMode )
            {
                // _schedules and _journals cannot be null, so we assign them each an empty list
				_schedules = globalSchedules;
				_journals = new List<EventJournal>();
            }

            // If we're in cal station mode, then we don't need to query the database for schedules.
            // Instead, we just use our one-and-only configured CalStationSchedule.
            // Also, we only need to use the CalStationSchedule if there's a docked instrument.
            // For here, we can figure that out by just checking if there's more than one serial number (one serial number = docking station's serial number).
            else if ( Configuration.Schema.Activated == false )
            {
                if ( serialNumbers.Length > 1 )
                {
                    Schedule calStationSchedule = CreateCalStationGasSchedule();

                    if ( calStationSchedule != null ) // will be null if currently configured CalStationSchedule is "do nothing".
                        globalSchedules.Add( calStationSchedule );

					Schedule calStationDataSchedule = CreateCalStationDatalogSchedule();

					if ( calStationDataSchedule != null ) 
						globalSchedules.Add( calStationDataSchedule );
                    
                    using (IDataAccessTransaction trx = _dataAccessTransactionForUnitTest == null ? new DataAccessTransaction(true) : _dataAccessTransactionForUnitTest )
                    {
                        EventJournalDataAccess eventJournalDataAccess = _eventJournalDataAccessForUnitTest == null ? new EventJournalDataAccess() : _eventJournalDataAccessForUnitTest;
                        // Get journal entries for the docking station, docked instrument, and its sensors
                        _journals = eventJournalDataAccess.FindBySerialNumbers( serialNumbers, trx );
                    }
                }

				// Load schedules for cal station mode
				_schedules = globalSchedules;
            }

            // else, we're activated on some account.
            // We might in service mode, we might not be. Doesn't matter.
            else
            {
                ScheduledUponDockingDataAccess uponDockDataAccess = _uponDockDataAccessForUnitTest ?? new ScheduledUponDockingDataAccess();
                ScheduledOnceDataAccess onceDataAccess = _onceDataAccessForUnitTest ?? new ScheduledOnceDataAccess();
                ScheduledHourlyDataAccess hourlyDataAccess = _hourlyDataAccessForUnitTest ?? new ScheduledHourlyDataAccess();
                ScheduledDailyDataAccess dailyDataAccess = _dailyDataAccessForUnitTest ?? new ScheduledDailyDataAccess();
                ScheduledWeeklyDataAccess weeklyDataAccess = _weeklyDataAccessForUnitTest ?? new ScheduledWeeklyDataAccess();
                ScheduledMonthlyDataAccess monthlyDataAccess = _monthlyDataAccessForUnitTest ?? new ScheduledMonthlyDataAccess();

                using ( IDataAccessTransaction trx = _dataAccessTransactionForUnitTest ?? new DataAccessTransaction(true) )
                {
                    // Get journal entries for the docking station, docked instrument, and its sensors
                    EventJournalDataAccess eventJournalDataAccess = _eventJournalDataAccessForUnitTest ?? new EventJournalDataAccess();
                    _journals = eventJournalDataAccess.FindBySerialNumbers( serialNumbers, trx );

                    // Get global schedules for the docking station and docked instrument
                    globalSchedules.AddRange( uponDockDataAccess.FindGlobalSchedules( trx ) );
                    globalSchedules.AddRange( onceDataAccess.FindGlobalSchedules( trx ) );
                    globalSchedules.AddRange( hourlyDataAccess.FindGlobalSchedules( trx ) );
                    globalSchedules.AddRange( dailyDataAccess.FindGlobalSchedules( trx ) );
                    globalSchedules.AddRange( weeklyDataAccess.FindGlobalSchedules( trx ) );
                    globalSchedules.AddRange( monthlyDataAccess.FindGlobalSchedules( trx ) );

					// Get global type-specific schedules for the docked instrument;
					// No need to query database if no instrument docked (unknown) or is not a known type (other)
					List<Schedule> globalTypeSpecificSchedules = new List<Schedule>();
                    
                    //For VPRO, the firmware is common for both the sub types,so the firmware sub type code will be null, so ignore equipment sub type code for VPRO
                    string equipmentSubTypeCode = equipmentSubType == DeviceSubType.None || equipmentSubType == DeviceSubType.VentisPro4 || equipmentSubType == DeviceSubType.VentisPro5 ? null : equipmentSubType.ToString().ToUpper();


                    if (dockingStationType != DeviceType.Unknown && dockingStationType != DeviceType.Other)
					{
                        globalTypeSpecificSchedules.AddRange(uponDockDataAccess.FindGlobalTypeSpecificSchedules(equipmentCodes.ToArray(), trx));
                        globalTypeSpecificSchedules.AddRange(onceDataAccess.FindGlobalTypeSpecificSchedules(equipmentCodes.ToArray(), trx));
                        globalTypeSpecificSchedules.AddRange(hourlyDataAccess.FindGlobalTypeSpecificSchedules(equipmentCodes.ToArray(), trx));
                        globalTypeSpecificSchedules.AddRange(dailyDataAccess.FindGlobalTypeSpecificSchedules(equipmentCodes.ToArray(), trx));
                        globalTypeSpecificSchedules.AddRange(weeklyDataAccess.FindGlobalTypeSpecificSchedules(equipmentCodes.ToArray(), trx));
                        globalTypeSpecificSchedules.AddRange(monthlyDataAccess.FindGlobalTypeSpecificSchedules(equipmentCodes.ToArray(), trx));						
					}

                    //Have a new list, since we can't loop through the existing list and we may remove the item which leads to exception, if there is no item in it
                    List<Schedule> scheduleList = new List<Schedule>();
                    scheduleList.AddRange(globalTypeSpecificSchedules);
                    foreach (Schedule globalTypeSpecific in scheduleList)
                    {
                        // For each global type-specific schedule found, remove any global schedule of the same event type.  i.e., any
                        // global type-specific schedule of a given event type overrides any global schedules of the same event type.
                        globalSchedules.RemoveAll( listSchedule => listSchedule.EventCode == globalTypeSpecific.EventCode && listSchedule.EventCode.EquipmentTypeCode == globalTypeSpecific.EventCode.EquipmentTypeCode );

                        if (globalTypeSpecific.EventCode.Code == EventCode.FirmwareUpgrade || globalTypeSpecific.EventCode.Code == EventCode.InstrumentFirmwareUpgrade)
                        {
                            // INS-8980 RHP v7.6 - DockingStation.SoftwareVersion can be empty
                            string dockingStationVersion =  String.IsNullOrEmpty(Configuration.DockingStation.SoftwareVersion.Trim()) ? Master.Instance.ControllerWrapper.FirmwareVersion : Configuration.DockingStation.SoftwareVersion;
                            //INS-2460 For the auto firmware upgrade, we need to validate whether the firmware is relevant to the DSX or the docked instrument.
                            //For instrument especially - MX4 where we can dock multiple instrument type and each of them has their own firmware, so fetch only relevant firmware to proceed, else remove it from schedule for this equipment
                            string equipmentVersion = globalTypeSpecific.EventCode.Code == EventCode.FirmwareUpgrade ? dockingStationVersion : Master.Instance.SwitchService.Instrument.SoftwareVersion;
                            string firmwareVersion = string.Empty;
                            if (globalTypeSpecific.ScheduleProperties.Exists(sp => sp.Attribute == ScheduleProperty.FirmwareUpgradeVersion))
                            {
                                firmwareVersion = globalTypeSpecific.ScheduleProperties.Find(sp => sp.Attribute == ScheduleProperty.FirmwareUpgradeVersion).Value;
                            }

                            if (globalTypeSpecific.EventCode.Code == EventCode.FirmwareUpgrade)
                            {
                                if (globalTypeSpecific.EquipmentCode != Configuration.DockingStation.Type.ToString() ||
                                    string.IsNullOrEmpty(firmwareVersion) || !string.IsNullOrEmpty(equipmentVersion) && new Version(firmwareVersion) <= new Version(equipmentVersion))
                                {
                                    globalTypeSpecificSchedules.Remove(globalTypeSpecific);
                                    Log.Debug(string.Format("LoadSchedulingData - Skipping Global Type Schedule: EventCode={0}, EquipmentCode={1}, RefId={2}", globalTypeSpecific.EventCode.Code, globalTypeSpecific.EquipmentCode, globalTypeSpecific.RefId));
                                }
                            }
                            if (globalTypeSpecific.EventCode.Code == EventCode.InstrumentFirmwareUpgrade)
                            {
                                if (globalTypeSpecific.EquipmentCode != instrumentEquipmentCode || (globalTypeSpecific.EquipmentSubTypeCode != equipmentSubTypeCode) ||
                                    string.IsNullOrEmpty(firmwareVersion) || !string.IsNullOrEmpty(equipmentVersion) && new Version(firmwareVersion) <= new Version(equipmentVersion))
                                {
                                    globalTypeSpecificSchedules.Remove(globalTypeSpecific);
                                    Log.Debug(string.Format("LoadSchedulingData - Skipping Global Type Schedule: EventCode={0}, EquipmentCode={1}, SubTypeCode={2}, RefId={3}", globalTypeSpecific.EventCode.Code, globalTypeSpecific.EquipmentCode, equipmentSubTypeCode != null ? equipmentSubTypeCode : string.Empty, globalTypeSpecific.RefId));
                                }
                            }
                        }
                    }
										
					// Load global schedules and global type-specific schedules; 
					// some may be removed later if there are special schedules for serial numbers
					_schedules = globalSchedules;
					_schedules.AddRange( globalTypeSpecificSchedules );
					
                    // Get special schedules for the docking station, docked instrument, and its sensors
					List<Schedule> specialSchedules = new List<Schedule>();
                    specialSchedules.AddRange( uponDockDataAccess.FindBySerialNumbers( serialNumbers, trx ) );
                    specialSchedules.AddRange( onceDataAccess.FindBySerialNumbers( serialNumbers, trx ) );
                    specialSchedules.AddRange( hourlyDataAccess.FindBySerialNumbers( serialNumbers, trx ) );
                    specialSchedules.AddRange( dailyDataAccess.FindBySerialNumbers( serialNumbers, trx ) );
                    specialSchedules.AddRange( weeklyDataAccess.FindBySerialNumbers( serialNumbers, trx ) );
                    specialSchedules.AddRange( monthlyDataAccess.FindBySerialNumbers( serialNumbers, trx ) );

					// For each special schedule for specific serial number(s) found, remove any global schedule or global type-specific 
					// schedule of the same event type.  Any special schedule for specific serial number(s) of a given event type 
					// overrides both global types of schedules.

                    //Refactor it in a better way to handle the below job, if possible. Right now it does the work, but it can be done in a better way
                    //Add more log info
                    scheduleList = new List<Schedule>();
                    scheduleList.AddRange(specialSchedules);
                    foreach (Schedule specialSchedule in scheduleList)
					{
                        if ( specialSchedule.EventCode.Code != EventCode.InstrumentFirmwareUpgrade && specialSchedule.EventCode.Code != EventCode.FirmwareUpgrade )
                        {
                            _schedules.RemoveAll(listSchedule => listSchedule.EventCode == specialSchedule.EventCode && listSchedule.EventCode.EquipmentTypeCode == specialSchedule.EventCode.EquipmentTypeCode);
                        }
                        else
                        {
                            //INS-2460 - If the special schedule doesn't have schedule properties, then it should be scheduled earlier than the auto firmware upgrade implementation
                            //In that case consider the auto firmware, since it's scheduled later and greater than the current software version
                            // INS-8980 RHP v7.6 - DockingStation.SoftwareVersion can be empty
                            string dockSoftwareVersion = String.IsNullOrEmpty(Configuration.DockingStation.SoftwareVersion.Trim()) ? Controller.FirmwareVersion : Configuration.DockingStation.SoftwareVersion;
                            string equipmentFirmwareVersion = specialSchedule.EventCode.Code == EventCode.FirmwareUpgrade ? dockSoftwareVersion : Master.Instance.SwitchService.Instrument.SoftwareVersion;                            

                            string specialEventFirmwareVersion = string.Empty;
                            if (specialSchedule.ScheduleProperties.Exists(sp => sp.Attribute == ScheduleProperty.FirmwareUpgradeVersion))
                            {
                                specialEventFirmwareVersion = specialSchedule.ScheduleProperties.Find(sp => sp.Attribute == ScheduleProperty.FirmwareUpgradeVersion).Value;
                            }   

                            Schedule globalTypeSchedule;
                            if ((globalTypeSchedule = globalTypeSpecificSchedules.Find(listSchedule => listSchedule.EventCode == specialSchedule.EventCode && listSchedule.EventCode.EquipmentTypeCode == specialSchedule.EventCode.EquipmentTypeCode)) != null)
                            {
                                string autoFirmwareVersion = string.Empty;
                                if (globalTypeSchedule.ScheduleProperties.Exists(sp => sp.Attribute == ScheduleProperty.FirmwareUpgradeVersion))
                                {
                                    autoFirmwareVersion = globalTypeSchedule.ScheduleProperties.Find(sp => sp.Attribute == ScheduleProperty.FirmwareUpgradeVersion).Value;
                                }

                                if (globalTypeSchedule.EventCode.Code == EventCode.FirmwareUpgrade && globalTypeSchedule.EquipmentCode == Configuration.DockingStation.Type.ToString())
                                {
                                    if (string.IsNullOrEmpty(specialEventFirmwareVersion) || new Version(specialEventFirmwareVersion) <= new Version(autoFirmwareVersion))
                                    {
                                        specialSchedules.Remove(specialSchedule);
                                        Log.Debug(string.Format("LoadSchedulingData - Skipping Special Type Schedule: EventCode={0}, EquipmentCode={1}, RefId={2}", specialSchedule.EventCode.Code, specialSchedule.EquipmentCode, specialSchedule.RefId));
                                    }
                                    else
                                    {
                                        _schedules.Remove(globalTypeSchedule);
                                        Log.Debug(string.Format("LoadSchedulingData - Skipping Global Type Schedule: EventCode={0}, EquipmentCode={1}, RefId={2}", globalTypeSchedule.EventCode.Code, globalTypeSchedule.EquipmentCode, globalTypeSchedule.RefId));
                                    }
                                }
                                else if (globalTypeSchedule.EventCode.Code == EventCode.InstrumentFirmwareUpgrade && globalTypeSchedule.EquipmentCode == instrumentEquipmentCode)
                                {
                                    if (globalTypeSchedule.EquipmentSubTypeCode == equipmentSubTypeCode)
                                    {
                                        if (string.IsNullOrEmpty(specialEventFirmwareVersion) || new Version(specialEventFirmwareVersion) <= new Version(autoFirmwareVersion))
                                        {
                                            specialSchedules.Remove(specialSchedule);
                                            Log.Debug(string.Format("LoadSchedulingData - Skipping Special Type Schedule: EventCode={0}, EquipmentCode={1}, SubTypeCode={2}, RefId={3}", specialSchedule.EventCode.Code, specialSchedule.EquipmentCode, equipmentSubTypeCode != null ? equipmentSubTypeCode : string.Empty, specialSchedule.RefId));
                                        }
                                        else
                                        {
                                            _schedules.Remove(globalTypeSchedule);
                                            Log.Debug(string.Format("LoadSchedulingData - Skipping Global Type Schedule: EventCode={0}, EquipmentCode={1}, SubTypeCode={2}, RefId={3}", globalTypeSchedule.EventCode.Code, globalTypeSchedule.EquipmentCode, equipmentSubTypeCode != null ? equipmentSubTypeCode : string.Empty, globalTypeSchedule.RefId));
                                        }
                                    }
                                    else
                                    {
                                        _schedules.Remove(globalTypeSchedule);
                                        Log.Debug(string.Format("LoadSchedulingData - Skipping Global Type Schedule: EventCode={0}, EquipmentCode={1}, SubTypeCode={2}, RefId={3}", globalTypeSchedule.EventCode.Code, globalTypeSchedule.EquipmentCode, equipmentSubTypeCode != null ? equipmentSubTypeCode : string.Empty, globalTypeSchedule.RefId));
                                    }
                                }
                            }
                            else
                            {
                                //If there is no auto firmware upgrade, but there is equipment specific upgrade which is older firmware version than the current equipment (if its got updated via Auto firmware)
                                //Then remove the schedule. TODO: We should ideally delete the schedule as well.                                
                                if (!string.IsNullOrEmpty(equipmentFirmwareVersion) && !string.IsNullOrEmpty(specialEventFirmwareVersion) && new Version(equipmentFirmwareVersion) >= new Version(specialEventFirmwareVersion))
                                {
                                    specialSchedules.Remove(specialSchedule);
                                    Log.Debug(string.Format("LoadSchedulingData - Skipping Special Type Schedule: EventCode={0}, EquipmentCode={1}, EquipmentVersion={2}, FirmwareVersion={3}, RefId={4}", specialSchedule.EventCode.Code, specialSchedule.EquipmentCode, equipmentFirmwareVersion, specialEventFirmwareVersion, specialSchedule.RefId));
                                }     
                            }
                        }
					}

                    if ( componentCodes.Length > 0 )
                    {
                        // Get special schedules for component types
                        specialSchedules.AddRange( uponDockDataAccess.FindByComponentCodes( componentCodes, trx ) );
                        specialSchedules.AddRange( onceDataAccess.FindByComponentCodes( componentCodes, trx ) );
                        specialSchedules.AddRange( hourlyDataAccess.FindByComponentCodes( componentCodes, trx ) );
                        specialSchedules.AddRange( dailyDataAccess.FindByComponentCodes( componentCodes, trx ) );
                        specialSchedules.AddRange( weeklyDataAccess.FindByComponentCodes( componentCodes, trx ) );
                        specialSchedules.AddRange( monthlyDataAccess.FindByComponentCodes( componentCodes, trx ) );
                    }

					// Load both types of special schedules
					_schedules.AddRange( specialSchedules );
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <remarks>Added for INS-4704 (INS-4475) (upload next cal/bump date to inet as part of uploaded cal/bump results)</remarks>
        /// <param name="gasResponseEvent"></param>
        /// <returns></returns>
        internal virtual DateTime? GetNextGasOperationDate( InstrumentGasResponseEvent gasResponseEvent )
        {
            const string funcName = "GetNextScheduledGasOperationDate: ";

            Log.Debug( string.Format( "{0}Calculating next {1} date...", funcName, gasResponseEvent.EventCode ) );

            // From the global list of all journals, get only those for the current event code.
            List<EventJournal> eventJournals = _journals.FindAll( j => j.EventCode.Code == gasResponseEvent.EventCode.Code );

            // From the global list of schedules, get only those for the passed-in event code.
            List<Schedule> eventSchedules = _schedules.FindAll( s => s.EventCode.Code == gasResponseEvent.EventCode.Code );

            // For all sensors that have been calibrated
            foreach ( SensorGasResponse sgr in gasResponseEvent.GasResponses )
            {
                eventJournals.RemoveAll( j => j.SerialNumber == sgr.Uid );
                eventJournals.Add( new EventJournal( gasResponseEvent.EventCode, sgr.SerialNumber, gasResponseEvent.Time, gasResponseEvent.Time, sgr.Passed, gasResponseEvent.DockedInstrument.SoftwareVersion ) );
            }

            OverdueInfo overdueInfo = GetOverdueInstrumentSchedule( gasResponseEvent.EventCode, eventSchedules, eventJournals );

            DateTime? nextLocalRunTime = UpdateNextRunTime( null, overdueInfo.NextRunTime );

            overdueInfo = GetOverdueSensorTypeSchedule( gasResponseEvent.EventCode, eventSchedules, eventJournals );
            nextLocalRunTime = UpdateNextRunTime( nextLocalRunTime, overdueInfo.NextRunTime );

            // Convert from local time to UTC.
            DateTime? nextUtcRunTime;

            if ( nextLocalRunTime != null && nextLocalRunTime.Value.Kind != DateTimeKind.Utc ) // It should just about ALWAYS be local time and never UTC.
            {
                if ( nextLocalRunTime.Value == DateTime.MaxValue )
                    nextUtcRunTime = DateTime.SpecifyKind( nextLocalRunTime.Value, DateTimeKind.Utc );
                else
                    nextUtcRunTime = Configuration.ToUniversalTime( nextLocalRunTime.Value );
            }
            else
                nextUtcRunTime = nextLocalRunTime;

            Log.Debug( string.Format( "{0}Next {1} date is {2}, {3}...", funcName, gasResponseEvent.EventCode, Log.DateTimeToString( nextLocalRunTime ), Log.DateTimeToString( nextUtcRunTime ) ) );

            return nextUtcRunTime;
        }
    }
}
