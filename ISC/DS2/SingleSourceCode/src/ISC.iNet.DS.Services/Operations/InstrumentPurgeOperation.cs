using System;
using System.Collections.Generic;
using System.Threading;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services.Resources;
using ISC.Instrument.Driver;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    using ISC.iNet.DS.DomainModel; // puting this here avoids compiler's confusion of DomainModel.Instrument vs Instrument.Driver.

    public enum PurgeType
    {
        Unknown,

        /// <summary>
        /// Purge performed prior to zeroing all sensors.
        /// </summary>
        PreCalibration,

        /// <summary>
        /// Purge performed at the very end of a calibration operation.
        /// </summary>
        PostCalibration,

        /// <summary>
        /// "Smart purge" performed prior to bumping instrument.
        /// <para>
        /// Performed when instrument has a pump or contains certain "exotic" sensors.
        /// </para>
        /// </summary>
        PreBump,

        /// <summary>
        /// Purge that is performed at the end of a bump.
        /// <para>
		/// Performed if instrument contains certain "exotic" sensors or the docking station's "Purge After Bump" setting is enabled.
		/// </para>
        /// </summary>
        PostBump,  // SGF  06-Jun-2011  INS-1735 -- new type of purge

        /// <summary>
        /// At the beginning of a bump test, a purge is performed if there are O2 sensors installed.
        /// During the purge, the O2 sensors are checked to see if they are returning readings within normal range.
        /// When normal readings by the O2 sensor have been attained, the purge ends, and will be followed by the
        /// remainder of the bump test actions for the sensor.
        /// If any O2 sensor does not attain normal readings by a maximum purge time, the bump test for the sensor
        /// will be flagged as Failed and its bump fault flag will be set to True.
        /// </summary>
        O2Recovery,

        /// <summary>
        /// Purge that is performed between each passes of Bump/Cal wherein we switch between Cylinders.
        /// This is done to clear gases in line. INETQA-4189 RHP v7.6
        /// </summary>
        CylinderSwitch
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to perform an instrument purge.
    /// </summary>
    public class InstrumentPurgeOperation : InstrumentPurgeAction, IOperation
    {
        #region Fields

        public const double OXYGEN_FRESH_AIR_TEST_LOW_PASS_PCT = 20.0;

        private InstrumentController _instrumentController;
        private PurgeType _purgeType = PurgeType.Unknown;
        private InstrumentGasResponseEvent _returnGasResponseEvent = null;

        private int _purge1Seconds = 0;
		private int _purge2Seconds = 0; // Only set when one PurgeType requires two distinct purges.
        private int _desiredFlow = Pump.MaxFlowRate;
		private GasEndPoint _airEndPoint = null;
        private List<SensorGasResponse> _currentPassSGRList = new List<SensorGasResponse>();

        #endregion Fields

        #region Constructors

		private void ConstructorInit( PurgeType purgeType, InstrumentController instrumentController, List<GasEndPoint> gasEndPoints, InstrumentGasResponseEvent gasResponseEvent )
        {
            Log.Assert( instrumentController != null, "instrumentController cannot be null" );

            _instrumentController = instrumentController;
            _purgeType = purgeType;

            _returnGasResponseEvent = gasResponseEvent;

            // clone the supplied GasEndPoints
			foreach ( GasEndPoint currentGasEndPoint in gasEndPoints )
            {
				GasEndPoint purgeGasEndPoint = (GasEndPoint)currentGasEndPoint.Clone();
                GasEndPoints.Add( purgeGasEndPoint );
            }
        }

        /// <summary>
        /// Creates a new instance of an InstrumentPurgeOperation class.
        /// </summary>
		public InstrumentPurgeOperation( PurgeType purgeType, InstrumentController instrumentController, List<GasEndPoint> gasEndPoints, InstrumentGasResponseEvent gasResponseEvent )
        {
            ConstructorInit( purgeType, instrumentController, gasEndPoints, gasResponseEvent );
        }

        /// <summary>
        /// Creates a new instance of an InstrumentPurgeOperation class.
        /// </summary>
        public InstrumentPurgeOperation(PurgeType purgeType, InstrumentController instrumentController, List<GasEndPoint> gasEndPoints, InstrumentGasResponseEvent gasResponseEvent, List<SensorGasResponse> currentPassSGRList)
        {
            _currentPassSGRList = currentPassSGRList;
            ConstructorInit(purgeType, instrumentController, gasEndPoints, gasResponseEvent);
        }

        #endregion Constructors


        #region Methods

        /// <summary>
        /// Executes an instrument bump test operation.
        /// </summary>
        /// <returns>The completed event for this bump test.</returns>
        /// <exception cref="FailedBumpTestException">
        /// If anything extraordinary happened during the bump test.
        /// </exception>
        public DockingStationEvent Execute()
        {
            if ( !Prepare() )
                return null;

            bool sensorOK = Purge();

            return sensorOK ? _returnGasResponseEvent : null;
        }

        private bool Prepare()
        {
            const string funcMsg = "PreparePurge: ";

            Log.Debug( string.Format( "{0}Starting ({1})", funcMsg, _purgeType ) );

            // SGF  06-Jun-2011  INS-1735
            // Do not perform a purge if the type is Unknown or Invalid
            if ( _purgeType == PurgeType.Unknown )
                return false;

            if ( Master.Instance.ControllerWrapper.IsDocked() == false )
            {
                Log.Debug( string.Format( "{0}No instrument is docked -- NO PURGE NECESSARY", funcMsg ) );
                return false;
            }

            bool instHasAccessoryPump = false;
            bool isPumpAdapterAttached = false;

            if ( _instrumentController != null )
            {
                instHasAccessoryPump = ( _instrumentController.AccessoryPump == AccessoryPumpSetting.Installed );
                isPumpAdapterAttached = Master.Instance.ControllerWrapper.IsPumpAdapterAttached();

                Log.Debug( string.Format( "{0}PurgeType={1}", funcMsg, _purgeType ) );
                Log.Debug( string.Format( "{0}AccessoryPump={1}, PumpAdapter={2}", funcMsg, instHasAccessoryPump, isPumpAdapterAttached ) );

                // Normal flow rate for purging is the docking station's maximum flow rate.
                // Except if instrument has a pump and there is no pump adapter, then we purge
                // using a lower flow rate to avoid damaging the instrument pump.  
                if ( instHasAccessoryPump && !isPumpAdapterAttached )
                    _desiredFlow = Pump.StandardFlowRate;
            }
            else
                _desiredFlow = Pump.MaxFlowRate;

			bool purgeRequired = true; // helper variable for multiple case blocks
            bool isDSX = !Configuration.DockingStation.Reservoir;
            Log.Debug( string.Format( "{0}Reservoir={1}", funcMsg, Configuration.DockingStation.Reservoir ) );

            int PUMP_PURGE_20SECONDS = (instHasAccessoryPump) ? 20 : 0;

            // Determine if we really have to purge or not (based on instrument's sensor 
            // configuration) and, if so, how long we should purge.
            switch ( _purgeType )
            {
                case PurgeType.PreCalibration:
                    // INS-6723: DSX - 10 seconds (30 if aspirated)
                    //       iNet DS - 40 seconds (60 if aspirated)         
#if TEST
                    _purge1Seconds = 0; // TODO: not a fix. Will have to be revisited
#else
                    _purge1Seconds = isDSX ? 10 + PUMP_PURGE_20SECONDS : 40 + PUMP_PURGE_20SECONDS;
#endif
                    break;

                case PurgeType.PostCalibration:
					// INS-6723: DSX / iNet DS - 30 seconds for enabled exotic sensor (50 if aspirated), followed by
					//           DSX / iNet DS - 60 seconds smart purge regardless
					foreach ( InstalledComponent ic in _returnGasResponseEvent.DockedInstrument.InstalledComponents )
					{
						if ( ( ic.Component is Sensor ) && ( (Sensor)ic.Component ).RequiresExtendedPostCalibrationPurge )
						{
							Log.Debug( string.Format( "{0}Purge required for {1} sensor.", funcMsg, ic.Component.Type.Code ) );
							_purge1Seconds = 30 + PUMP_PURGE_20SECONDS;
							break;
						}
					}
#if !TEST
                    _purge2Seconds = 60; // follow-up smart purge
#else
                    _purge2Seconds = 0;
#endif

                    break;

                case PurgeType.PreBump:
					// INS-6723: DSX / iNet DS - 30 seconds smart purge if aspirated or for enabled exotic sensor
					purgeRequired = instHasAccessoryPump;

					if ( purgeRequired )
                    {
						Log.Debug( string.Format( "{0}Purge required for aspirated instrument.", funcMsg ) );
					}
					else
					{
						// if instrument does not have a pump, see if there are enabled exotic sensors
                        foreach ( InstalledComponent ic in _returnGasResponseEvent.DockedInstrument.InstalledComponents )
                        {
                            if ( ( ic.Component is Sensor ) && ( (Sensor)ic.Component ).RequiresBumpTestPurge( GasEndPoints ) == true )
                            {
                                Log.Debug( string.Format( "{0}Purge required for {1} sensor.", funcMsg, ic.Component.Type.Code ) );
                                purgeRequired = true;
								break;
                            }
                        }
                    }

					if ( purgeRequired )
					{
#if TEST
                        _purge1Seconds = 0; // TODO: not a fix. Will have to be revisited
#else
                        _purge1Seconds = 30;
#endif
					} 
					else 
					{
						Log.Debug( string.Format( "{0}PurgeType={1} -- NO PURGE NECESSARY.", funcMsg, _purgeType ) );
						return false;
					}

                    break;

                case PurgeType.PostBump:
					// INS-6723: DSX / iNet DS - 30 seconds for enabled exotic sensor (50 if aspirated), followed by
					//           DSX / iNet DS - 60 seconds smart purge when "Purge After Bump" setting enabled
					purgeRequired = false;

					foreach ( InstalledComponent ic in _returnGasResponseEvent.DockedInstrument.InstalledComponents.FindAll( c => c.Component is Sensor ) )
					{
						if ( ( (Sensor)ic.Component ).RequiresBumpTestPurge( GasEndPoints ) == true )
						{
							Log.Debug( string.Format( "{0}Purge required for {1} sensor.", funcMsg, ic.Component.Type.Code ) );
							purgeRequired = true;
#if TEST
                            _purge1Seconds = 0; // TODO: not a fix. Will have to be revisited
#else
							_purge1Seconds = 30 + PUMP_PURGE_20SECONDS;
#endif
							break;
						}
					}

					if ( Configuration.DockingStation.PurgeAfterBump )
					{	
						purgeRequired = true;
						_purge2Seconds = 60;
						Log.Debug( string.Format( "{0}Purge required as PurgeAfterBump={1}.", funcMsg, Configuration.DockingStation.PurgeAfterBump ) );
					}
					
					
					if (!purgeRequired)
					{
						Log.Debug( string.Format( "{0}PurgeType={1} -- NO PURGE NECESSARY", funcMsg, _purgeType ) );
						return false;
					}
					
                    break;

                case PurgeType.O2Recovery:
					// INS-6684: DSX / iNet DS - until 20% vol seen with 120 seconds maximum 
                    purgeRequired = false;
                    bool isSecondHighBump = false;

                    foreach ( InstalledComponent ic in _returnGasResponseEvent.DockedInstrument.InstalledComponents)
                    {
                        if ( !( ic.Component is Sensor ) ) continue;
                        if ( !( (Sensor)ic.Component ).Enabled ) continue;
                        if ( ic.Component.Type.Code == SensorCode.O2 )
                        {
                            Log.Debug( string.Format( "{0}Purge required for {1} sensor.", funcMsg, ic.Component.Type.Code));
                            purgeRequired = true;
                            // INS-7625 SSAM v7.6
                            // Second high bump test should flow gas and have a much shorter timeout of 30 seconds.
                            // If the purge is done as a part of second high bump test, set the purge timeout to 30 seconds.
                            // Else, set it to 120 seconds.                           
                            SensorGasResponse sgr = _returnGasResponseEvent.GetSensorGasResponseByUid(ic.Component.Uid);
                            if (sgr != null && sgr.IsSecondO2HighBump)
                                isSecondHighBump = true;
#if !TEST
                            _purge1Seconds = isSecondHighBump ? 30 : 120; // See INS-6684 (and INS-2314)
#else
                            _purge1Seconds = 0;
#endif
                        }
                    }

                    if ( !purgeRequired )
                    {
                        Log.Debug( string.Format( "{0}PurgeType={1} -- NO PURGE NECESSARY", funcMsg, _purgeType ) );
                        return false;
                    }
					
                    break;

                case PurgeType.CylinderSwitch:
#if !TEST
                    // 40 seconds (60 if aspirated)         
                    _purge1Seconds = 40 + PUMP_PURGE_20SECONDS;
                    // For Calibration, 30 seconds (45 if aspirated)         
                    if (_returnGasResponseEvent is InstrumentCalibrationEvent)
                        _purge1Seconds = instHasAccessoryPump ? 45 : 30;
#else
                    _purge1Seconds = 0;
#endif
                    break;
            }
 
            Log.Debug( string.Format( "{0}Purge seconds={1}, Desired flow={2}", funcMsg, _purge1Seconds, _desiredFlow ) );

			if (_purge2Seconds > 0)
				Log.Debug( string.Format( "{0}Secondary purge seconds={1}", funcMsg, _purge2Seconds ) );

            // First try to find fresh air.  If found, then use it.
            try
            {
                _airEndPoint = GetFreshAir(); // Find the fresh air valve
                Log.Debug( string.Format( "{0}Found fresh air for purging.", funcMsg ) );
            }
            catch ( CorrectCalibrationGasUnavailable )
            {
                Log.Debug( string.Format( "{0}No fresh air found for purging.  Looking for zero air instead.", funcMsg ) );
            }

            // If no fresh air found, then look for zero air.  Note that we pass a true
            // to GetSensorZeroAir causing it to NOT look for a fresh air alternative.
            // We just looked for fresh air above.
            if ( _airEndPoint == null )
            {
                _airEndPoint = GetZeroAir( true );
                Log.Debug( string.Format( "{0}Found zero air for purging.", funcMsg ) );
            }

            if ( _airEndPoint == null )
            {
                Log.Debug( string.Format( "{0}FOUND NO AIR FOR PURGING.", funcMsg ) );
                return false;
            }

            return true;
        } // end-Prepare

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool Purge()
        {
            const string funcMsg = "Purge: ";

            bool purgeDone = false;
			int purgeStepTime = 2; // 2 seconds is a guess; configure as necessary
            TimeSpan maxPurgeLength = new TimeSpan( 0, 0, _purge1Seconds );
            TimeSpan elapsedTime = new TimeSpan( 0, 0, 0 );
            DateTime purgeStartTime; // initialized immediately prior to each loop which this is used
            DateTime cylinderStartTime = DateTime.UtcNow;

            try
            {
                // Indicate that the purging process is now in progress
                ConsoleState consoleState;
                if ( _purgeType == PurgeType.PreCalibration || _purgeType == PurgeType.PostCalibration )
                    consoleState = ConsoleState.CalibratingInstrument;
                else if (_purgeType == PurgeType.CylinderSwitch)                   
                    consoleState = (_returnGasResponseEvent is InstrumentCalibrationEvent) ? ConsoleState.CalibratingInstrument : ConsoleState.BumpingInstrument;
                else
                    consoleState = ConsoleState.BumpingInstrument;

                Master.Instance.ConsoleService.UpdateState( consoleState, ConsoleServiceResources.PURGING );

                _instrumentController.OpenGasEndPoint( _airEndPoint, _desiredFlow );

                switch ( _purgeType )
                {
					// Constant purge only
                    case PurgeType.PreCalibration: // prior to zeroing, we do a fixed-time purge.
                        Log.Debug( string.Format( "{0}Purging for {1} seconds.", funcMsg, _purge1Seconds ) );

						purgeStartTime = DateTime.UtcNow;
						while ( elapsedTime < maxPurgeLength )
                        {
							if ( !Master.Instance.ControllerWrapper.IsDocked() )
								throw new InstrumentNotDockedException();

                            Thread.Sleep( 1000 ); // Wait for the purge.

                            // See if ResourceService determined that cylinder was empty while we slept.
                            CheckAir( _airEndPoint ); // throws if empty cylinder is detected
                            
							elapsedTime = DateTime.UtcNow - purgeStartTime;
                        }
                        purgeDone = true;
                        break;

                    // Constant purge followed by a "smart purge"
                    case PurgeType.PostCalibration:
                    case PurgeType.PostBump:
						// constant purge
						if ( _purge1Seconds > 0 )
						{
							Log.Debug( string.Format( "{0}Purging for {1} seconds.", funcMsg, _purge1Seconds ) );

							purgeStartTime = DateTime.UtcNow;
							while ( elapsedTime < maxPurgeLength )
							{
								if ( !Master.Instance.ControllerWrapper.IsDocked() )
									throw new InstrumentNotDockedException();

								Thread.Sleep( 1000 ); // Wait for the purge.

								// See if ResourceService determined that cylinder was empty while we slept.
								CheckAir( _airEndPoint ); // throws if empty cylinder is detected

								elapsedTime = DateTime.UtcNow - purgeStartTime;
							}
						}

						// smart purge
						if ( _purge2Seconds > 0 )
						{
							Log.Debug( string.Format( "{0}Purging for a maximum of {1} seconds.", funcMsg, _purge2Seconds ) );

							// reset as constant purge above may have used these
							elapsedTime = new TimeSpan( 0, 0, 0 );
							maxPurgeLength = new TimeSpan( 0, 0, _purge2Seconds );

							purgeStartTime = DateTime.UtcNow;
							while ( elapsedTime < maxPurgeLength && purgeDone == false )
							{
								// See if the instrument is in alarm.  If it is not, the purge can end early.
								if ( IsInstrumentInAlarm( _returnGasResponseEvent.DockedInstrument, null ) == false )
									purgeDone = true;
								else
								{
									CheckAir( _airEndPoint ); // throws if empty cylinder is detected
									Log.Debug( string.Format( "{0}Purging for {1} seconds.", funcMsg, purgeStepTime ) );
									Thread.Sleep( purgeStepTime * 1000 );
								}
								elapsedTime = DateTime.UtcNow - purgeStartTime;
							}

							if ( purgeDone )
								Log.Debug( string.Format( "{0}Instrument is NOT in alarm after {1} seconds.", funcMsg, elapsedTime.TotalSeconds ) );
							else
								Log.Debug( string.Format( "{0}Instrument is IN ALARM after {1} seconds.  MAXIMUM PURGE TIME EXCEEDED.", funcMsg, elapsedTime.TotalSeconds ) );

							if ( !purgeDone && _purgeType == PurgeType.PostBump )
							{
								// If we got here, that must mean that the PurgeAfterBump setting is enabled.  
								Log.Debug( string.Format( "{0}PUTTING SENSORS INTO BUMP FAULT THAT ARE STILL IN ALARM.", funcMsg ) );
								// See if the instrument is still in alarm.  Report a purgeDone of 'false' if the instrument is still in alarm.
								// Put sensors into bump fault that are still in alarm.
								purgeDone = !IsInstrumentInAlarm( _returnGasResponseEvent.DockedInstrument, _returnGasResponseEvent as InstrumentBumpTestEvent );

								if ( purgeDone )
									Log.Debug( string.Format( "{0}Instrument was NOT in alarm for the final check.", funcMsg ) );
							}
						}
						else
						{
							// If PurgeAfterBump setting is disabled than we can report that the purge completed successfully.
							purgeDone = true;
						}

                        break; 

					// Smart purge only
					case PurgeType.PreBump:
						Log.Debug( string.Format( "{0}Purging for a maximum of {1} seconds.", funcMsg, _purge1Seconds ) );
                        
						purgeStartTime = DateTime.UtcNow;
						while ( elapsedTime < maxPurgeLength && purgeDone == false )
						{
							// See if the instrument is in alarm.  If it is not, the purge can end early.
							if ( IsInstrumentInAlarm( _returnGasResponseEvent.DockedInstrument, null ) == false )
								purgeDone = true;
							else
							{
								CheckAir( _airEndPoint ); // throws if empty cylinder is detected
								Log.Debug( string.Format( "{0}Purging for {1} seconds.", funcMsg, purgeStepTime ) );
								Thread.Sleep( purgeStepTime * 1000 );
							}
							elapsedTime = DateTime.UtcNow - purgeStartTime;
						}

						if ( purgeDone )
							Log.Debug( string.Format( "{0}Instrument is NOT in alarm after {1} seconds.", funcMsg, elapsedTime.TotalSeconds ) );
						else
							Log.Debug( string.Format( "{0}Instrument is IN ALARM after {1} seconds.  MAXIMUM PURGE TIME EXCEEDED.", funcMsg, elapsedTime.TotalSeconds ) );

						break;  

                    case PurgeType.O2Recovery:
                        Log.Debug( string.Format( "{0}Purging O2 sensors for recovery from depravation for a maximum of {1} seconds.", funcMsg, _purge1Seconds ) );

                        // From all the InstalledComponents, get a sub-list of just the O2 sensors.
                        // Then, from the list of O2 sensors, whittle it down to just O2 sensors that we have SGRs for.
                        // SGRs might be missing, for example, if we just did a bump test, but one or more sensors were 
                        // already in a cal-fault state. (Which could happen with dualsense sensors where one in the pair 
                        // is not working.) Those failed sensors would not have been bump tested, so there will be no SGR, 
                        // and since the sensoris not "working", we don't have to worry about getting its reading.
                        List<InstalledComponent> o2Components = _returnGasResponseEvent.DockedInstrument.InstalledComponents.FindAll( c => c.Component.Enabled && c.Component.Type.Code == SensorCode.O2 );
                        o2Components = o2Components.FindAll( o2 => _returnGasResponseEvent.GetSensorGasResponseByUid( o2.Component.Uid ) != null );

                        purgeStartTime = DateTime.UtcNow;
                        while ( elapsedTime < maxPurgeLength && purgeDone == false )
                        {
                            Thread.Sleep( 1000 );

                            CheckAir( _airEndPoint ); // throws if empty cylinder is detected

                            // SGF  24-Aug-2011  INS-2314 -- check O2 sensors for their current readings
                            int numO2SensorsPassed = 0;
                            foreach ( InstalledComponent ic in o2Components )
                            {
                                Sensor sensor = (Sensor)ic.Component;
                                SensorGasResponse sgr = _returnGasResponseEvent.GetSensorGasResponseByUid( sensor.Uid );
                                sgr.Status = Status.O2RecoveryFailed;

                                sgr.O2HighReading = _instrumentController.GetSensorReading( ic.Position, sensor.Resolution );
                                sgr.Time = DateTime.UtcNow;
                                Log.Debug( string.Format( "{0}O2 sensor UID={1} O2HighReading={2}.", funcMsg, sensor.Uid, sgr.O2HighReading.ToString() ) );

                                // SGF  24-Aug-2011  INS-2314 -- getting rid of the test for the high threshold, since we don't use cylinders with higher than normal O2 levels
                                if ( OXYGEN_FRESH_AIR_TEST_LOW_PASS_PCT <= sgr.O2HighReading )
                                {
                                    Log.Debug( string.Format( "{0}O2 sensor UID={1} reading is within normal range.", funcMsg, sensor.Uid ) );
                                    // INETQA-4149 INS-7625 SSAM v7.6 IsO2HighBumpPassed flag is set to true if O2 sensor passes the recovery purge.
                                    // Else if recovery fails, calibration is initiated to recover the O2 sensor.
                                    sgr.IsO2HighBumpPassed = true; 
                                    sgr.Status = Status.Passed;
                                    numO2SensorsPassed++;
                                }
                            }

                            if ( numO2SensorsPassed == o2Components.Count )
                                purgeDone = true; // All O2 sensors pass the recovery test; time to short-circuit the purge

                            elapsedTime = DateTime.UtcNow - purgeStartTime;
                        }

                        // For any O2 sensors that failed to recover above, mark the SGR status as O2RecoveryFailed
                        foreach ( InstalledComponent ic in o2Components )
                        {
                            SensorGasResponse sgr = _returnGasResponseEvent.GetSensorGasResponseByUid( ic.Component.Uid );
                            sgr.SpanCoef = _instrumentController.GetSensorSpanCoeff( ic.Position );
                            sgr.UsedGasEndPoints.Add( new UsedGasEndPoint( _airEndPoint, CylinderUsage.BumpHigh, elapsedTime ) );
                            if ( sgr.Status == Status.O2RecoveryFailed )
                                Log.Warning( string.Format( "{0} O2 SENSOR (UID={1}) FAILED TO RECOVER FROM DEPRAVATION.", funcMsg, ic.Component.Uid ) );
                        }

                        GasType gasType = GasType.Cache[GasCode.O2];
                        Master.Instance.ConsoleService.UpdateState( ConsoleState.BumpingInstrument, gasType.Symbol );

                        break; // end-PurgeType.O2Recovery

                    // Purge between use of different gas endpoints
                    case PurgeType.CylinderSwitch: 
                        Log.Debug(string.Format("{0}Purging for {1} seconds.", funcMsg, _purge1Seconds));

                        purgeStartTime = DateTime.UtcNow;
                        while (elapsedTime < maxPurgeLength && purgeDone == false)
                        {                         
                            // See if the sensor readings have met the purge complete criterion. If it has, the purge can end early.  
                            // During a calibration, this is a Constant purge only
                            if (_returnGasResponseEvent is InstrumentBumpTestEvent)
                                purgeDone = IsInstrumentPurgeCriterionMet();

                            Thread.Sleep(1000); // Wait for the purge.

                            // See if ResourceService determined that cylinder was empty while we slept.
                            CheckAir(_airEndPoint); // throws if empty cylinder is detected                                
                            elapsedTime = DateTime.UtcNow - purgeStartTime;
                        }

                        if (_returnGasResponseEvent is InstrumentCalibrationEvent)
                            purgeDone = true;   // For Calibration, its fixed time purge

                        Log.Debug(string.Concat("CYLINDER-SWITCH purge ", purgeDone ? "PASSED" : "FAILED"));

                        break;

                } // end-switch
            }
            catch ( CommunicationAbortedException cae ) // undocked?
            {
                throw new InstrumentNotDockedException( cae );
            }
            catch ( InstrumentNotDockedException )
            {
                throw;
            }
            catch ( FlowFailedException ffe ) // ran out of gas during the purge?
            {
                Log.Warning( Name + " throwing FlowFailedException for position " + ffe.GasEndPoint.Position );
                throw;
            }
            catch ( Exception ex )
            {
                throw new UnableToPurgeException( ex );
            }
            finally
            {
                _instrumentController.CloseGasEndPoint( _airEndPoint );

                //Purge is alway run at max voltage which satisfies condition of bad kink tubing even
                //there is no issue with tubing.  So, explicitly set "Pump.IsBadPumpTubing" to false
                //once purge operation is complete.
                Pump.IsBadPumpTubing = false;

                if ( _returnGasResponseEvent != null )
                {
                    // SGF  06-Jun-2011  INS-1735
					// Add a new UsedGasEndPoint object to the return event if the duration is greater than 0.
                    TimeSpan durationInUse = DateTime.UtcNow - cylinderStartTime;
                    if ( durationInUse.CompareTo( TimeSpan.MinValue ) > 0 )
                    {
                        _returnGasResponseEvent.UsedGasEndPoints.Add( new UsedGasEndPoint( _airEndPoint, CylinderUsage.Purge, durationInUse ) );
                    }
                }

                Log.Debug( string.Format( "{0}Finished", funcMsg ) );
            }

            return purgeDone;
        }

        /// <summary>
        /// INETQA-4189	RHP v7.6 - For Cylinder Switch Purge Type
        /// Purge is considered to be complete if sensor(s) in current pass reads less than 50% of its bump threshold value
        /// </summary>
        /// <returns>True if all the sensors in the "current pass" meet the criterion as above</returns>
        private bool IsInstrumentPurgeCriterionMet()
        {
            int numOfSensorsPassedPurge = 0;
            const string funcMsg = "IsInstrumentPurgeCriterionMet: ";

            foreach (InstalledComponent ic in _returnGasResponseEvent.DockedInstrument.InstalledComponents)
            {
                // We need to access ONLY the sensors taking part in this current Pass, check the sensor code in case of dual gas sensors
                if (!_currentPassSGRList.Exists(c => c.SerialNumber == ic.Component.SerialNumber && c.SensorCode == ic.Component.Type.Code))
                    continue;

                Sensor sensor = (Sensor)ic.Component;
                double sensorGasConc = (new GasConcentration(GasType.Cache[sensor.GetGasToCal()], sensor.CalibrationGasConcentration)).Concentration;

                double reading = _instrumentController.GetSensorReading(ic.Position, sensor.Resolution);
                double targetValue = 0.0;

                switch (sensor.BumpCriterionType)
                {
                    case CriterionType.O2:  //For O2 sensor, reading should be greatere than 20.
                        targetValue = OXYGEN_FRESH_AIR_TEST_LOW_PASS_PCT;
                        Log.Debug(String.Format("{0}: Checking sensor={1}, position={2}, reading={3}, targetValue={4}",
                            funcMsg, sensor.Type.Code.ToString(), ic.Position.ToString(), reading.ToString(), targetValue));
                        if (reading >= targetValue)
                            numOfSensorsPassedPurge++;
                        break;
                    case CriterionType.PPMLimit:  // For CLO2 sensors, HCL and Cl2 sensors, we pass the purge if the reading is less than 50% of its BumpPassCriterionPPMLimit
                        targetValue = (double)(sensor.BumpCriterionPPMLimit / 2);
                        Log.Debug(String.Format("{0}: Checking sensor={1}, position={2}, reading={3}, targetValue={4}",
                            funcMsg, sensor.Type.Code.ToString(), ic.Position.ToString(), reading.ToString(), targetValue));
                        if (reading < targetValue)
                            numOfSensorsPassedPurge++;
                        break;
                    default:  //For other sensors
                        double fullSpanReserve = Math.Round(reading / sensorGasConc * 100.0D, 2);
                        targetValue = (double)(_returnGasResponseEvent.DockedInstrument.BumpThreshold / 2);
                        Log.Debug(String.Format("{0}: Checking sensor={1}, position={2}, reading={3}, fullSpanReserve={4}, targetValue={5}",
                            funcMsg, sensor.Type.Code.ToString(), ic.Position.ToString(), reading.ToString(), fullSpanReserve, targetValue));
                        if (fullSpanReserve < targetValue)
                            numOfSensorsPassedPurge++;
                        break;
                }
            }

            Log.Debug(String.Format("{0}returned {1}", funcMsg, _currentPassSGRList.Count == numOfSensorsPassedPurge ? "Purge Completed Successfully" : "Purge Continues"));
            return _currentPassSGRList.Count == numOfSensorsPassedPurge;
        }

        // SGF 13-May-2011  INS-1992
        // 
        // TODO (CYLINDERS)
        //
        // Need to find a way to move the following two methods (GetSensorFreshAir and GetSensorZeroAir)
        // to another location in code that is useful for InstrumentPurgeOperation, InstrumentBumpTestOperation,
        // and InstrumentCalibrationOperation.
        //
        // Alternatively, we could simplify the following two methods for use here.
        //
        // ADDRESS THIS v5.1

        /// <summary>
        /// Find the fresh air end point for this sensor.
        /// </summary>
        /// <returns>The correct fresh air gas end point.</returns>
        /// <exception cref="CorrectCalibrationGasUnavailable">
        /// If there are no fresh air gas end points.
        /// </exception>
		protected GasEndPoint GetFreshAir()
        {
            Log.Debug( "Finding Fresh air for purge..." );

            // Find fresh air and zero gas end points.
            int pointCount = 0;
			foreach ( GasEndPoint gasEndPoint in GasEndPoints )
            {
#region LogDebug
                string msg = "GasEndPoint #" + ++pointCount;
                Log.Debug( msg );

                Cylinder cyl = gasEndPoint.Cylinder;

                msg = "...Pos=" + gasEndPoint.Position
                    //+ ", ID=" + cyl.ID
                    + ", FactID=" + cyl.FactoryId
                    + ", Part=" + cyl.PartNumber
                    + ", Fresh=" + cyl.IsFreshAir
                    + ", ZeroAir=" + cyl.IsZeroAir
                    + ", Pressure=" + cyl.Pressure.ToString();
                if ( cyl.Volume != DomainModelConstant.NullInt ) msg += ", Vol=" + cyl.Volume;

                Log.Debug( msg );
#endregion

                // Ignore non-fresh air cylinders
                if ( !cyl.IsFreshAir )
                {
                    Log.Debug( "...Rejected.  Not fresh air." );
                    continue;
                }

                if ( cyl.Pressure == PressureLevel.Empty )
                {
                    Log.Debug( "...Rejected fresh air. Cylinder empty." );
                    continue;
                }

                Log.Debug( "...SELECTED GasEndPoint. Fresh air found." );
                return gasEndPoint;
            }

            Log.Debug( "No fresh air found." );

            throw new CorrectCalibrationGasUnavailable( GasCode.FreshAir ); // No calibration gases were found.
        }

        /// <summary>
        /// Find the zero air end point for this sensor.  If zero air is not
        /// found, then fresh air may be returned instead (See notes on zeroAirOnly
        /// parameter).
        /// </summary>
        /// <param name="zeroAirOnly">
        /// If true, then this routine will only find and return zero air cylinders.
        /// If false, then this routine will attempt to find a zero air cylinder, but
        /// will find and return a fresh air as an alternative if zero air is not found.
        /// </param>
        /// <returns>
        /// The correct zero air gas end point.
        /// </returns>
        /// <exception cref="CorrectCalibrationGasUnavailable">
        /// If there are no zero air gas end points.
        /// </exception>
        protected internal GasEndPoint GetZeroAir( bool zeroAirOnly )
        {
            Log.Debug( "Finding Zero air cylinder for purge..." );

            // Find zero air end points.

            int pointCount = 0;

			foreach ( GasEndPoint gasEndPoint in GasEndPoints )
            {
#region LogDebug
                string msg = "GasEndPoint #" + ++pointCount;
                Log.Debug( msg );

                Cylinder cyl = gasEndPoint.Cylinder;

                msg = "...Pos=" + gasEndPoint.Position
                    //+ ", ID: " + cyl.ID
                    + ", FactID=" + cyl.FactoryId
                    + ", Part=" + cyl.PartNumber
                    + ", Fresh=" + cyl.IsFreshAir
                    + ", ZeroAir=" + cyl.IsZeroAir
                    + ", Pressure=" + cyl.Pressure.ToString();
                if ( cyl.Volume != DomainModelConstant.NullInt ) msg += ", Vol=" + cyl.Volume;

                Log.Debug( msg );
#endregion

                if ( !cyl.IsZeroAir )
                {
                    Log.Debug( "...Rejected.  Not zero air." );
                    continue;
                }

                if ( cyl.Pressure == PressureLevel.Empty )
                {
                    Log.Debug( "...Rejected zero air. Cylinder empty." );
                    continue;
                }

                Log.Debug( "...SELECTED GasEndPoint.  Zero air found." );
                return gasEndPoint;
            }

            if ( zeroAirOnly )
            {
                Log.Debug( "No zero air found." );
                throw new CorrectCalibrationGasUnavailable( "Zero Air" );
            }

            // No calibration gases were found, attempt to use the fresh air.
            Log.Debug( "No zero air found.  Looking for alternative fresh air..." );

            return GetFreshAir();
        }

		/// <summary>Determines if any enabled sensors are currently in alarm.</summary>
		/// <param name="returnEvent">The event should only be provided for the final PostBump (PurgeAfterBump) check as sensors will be put into bump fault.</param>
		/// <remarks>INS-6723, INS-1735</remarks>
        private bool IsInstrumentInAlarm( Instrument instrument, InstrumentBumpTestEvent returnEvent )
        {
            bool instrumentInAlarm = false;
			const string funcMsg = "IsInstrumentInAlarm: ";

            foreach ( InstalledComponent ic in instrument.InstalledComponents )
            {
                if ( !( ic.Component is Sensor ) )
                    continue;

                Sensor sensor = (Sensor)ic.Component;

                if ( sensor.IsBumpEnabled( GasEndPoints ) == false ) // why do we check this? - JMP, 8/2015
                {
                    Log.Debug( String.Format( "{0}Sensor in position={1} is not bump enabled", funcMsg, ic.Position ) );
                    continue;
                }

                if ( sensor.Enabled == false )
                {
                    Log.Debug( String.Format( "{0}Sensor in position={1} is not enabled", funcMsg, ic.Position ) );
                    continue;
                }

                // If sensor is in a cal/zero-failure state, then it's not reading gas.
                if ( SensorGasResponse.IsFailedCalibrationStatus( sensor.CalibrationStatus ) )
                {
                    Log.Debug( String.Format( "{0}Sensor in position={1} has {2} status", funcMsg, ic.Position, sensor.CalibrationStatus ) );
                    continue;
                }

                if ( IsSensorInAlarm( sensor, ic.Position ) == true )
                {                    
					// This method is only intended to be called once when returnEvent is not null, 
					// and only for the PostBump (PurgeAfterBump) purge.
                    if ( returnEvent != null )
                    {
                        SensorGasResponse sgr = GetSensorGasResponse( returnEvent, sensor );
                        // If instrument is still in alarm after purge is finishes, then we are setting the
                        // sensor's bump fault flag.  
                        if ( sgr != null && sgr.Status == Status.Passed )
                        {
                            Log.Debug( String.Format( "{0}Setting sensor to FAILED", funcMsg ) );
                            sgr.Status = Status.Failed;
                            _instrumentController.SetSensorBumpFault( ic.Position, true );
                        }
                    }

                    instrumentInAlarm = true;
                }
            }

            Log.Debug( String.Format( "{0}returned {1}", funcMsg, instrumentInAlarm ? "IN ALARM" : "NOT in alarm" ) );
            return instrumentInAlarm;
        }

        // SGF  06-Jun-2011  INS-1735
        private bool IsSensorInAlarm( Sensor sensor, int pos )
        {
            double reading = _instrumentController.GetSensorReading( pos, sensor.Resolution );
            Log.Debug( String.Format( "IsSensorInAlarm: Checking sensor={0}, position={1}, reading={2}", sensor.Type.Code.ToString(), pos.ToString(), reading.ToString() ) );

            if ( sensor.Type.Code == SensorCode.O2 )
            {
                // O2
                // We must check the sensor reading against both the high alarm value and the low alarm value.
                if ( reading >= _instrumentController.GetSensorHighAlarm( pos, sensor.Resolution ) )
                {
                    Log.Debug( String.Format( "IsSensorInAlarm:  Sensor={0}, position={1} is in HIGH ALARM", sensor.Type.Code.ToString(), pos.ToString() ) );
                    return true;
                }
                if ( reading <= _instrumentController.GetSensorLowAlarm( pos, sensor.Resolution ) )
                {
                    Log.Debug( String.Format( "IsSensorInAlarm:  Sensor={0}, position={1} is in LOW ALARM", sensor.Type.Code.ToString(), pos.ToString() ) );
                    return true;
                }

            }           
            else
            {
                // All other sensors
                // We must check the sensor reading against the low alarm value.
                if ( reading >= _instrumentController.GetSensorLowAlarm( pos, sensor.Resolution ) )
                {
                    Log.Debug( String.Format( "IsSensorInAlarm:  Sensor={0}, position={1} is in ALARM", sensor.Type.Code.ToString(), pos.ToString() ) );
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Throws a FlowFailedException if cylinder is detected as going empty
        /// during the purge.
        /// </summary>
        /// <param name="airEndPoint"></param>
		private void CheckAir( GasEndPoint airEndPoint )
        {
            

            if ( Pump.GetOpenValvePosition() > 0 )
                return;

            // For now, we don't bother checking fresh air to be 'empty'.
            // If we did check fresh air, then the port would get marked as 'empty'
            // in the database, and there's currently no way to reset it back to
            // Full except to reboot the docking station.
            //
            // If/when we have mechanism to allow an 'Empty' fresh air port to
            // be 'reset' back to Full, then we should remove this check for IsFreshAir.
            //
            // SEE ALSO: The IsFreshAir check in Pump.CheckFlow's loop.
            //
            // - JMP, 6/30/2011
            if ( airEndPoint.Cylinder.IsFreshAir )
                return;            

            throw new FlowFailedException( airEndPoint ); // empty
        }

#endregion Methods

    } // end-class InstrumentPurgeOperation

#region Exceptions

    /////////////////////////////////////////////////////////////////////////////////////
    ///<summary>
    /// Exception thrown when an error is encountered when attempting to purge the system.
    ///</summary>	
    public class UnableToPurgeException : ApplicationException
    {
        /// <summary>
        /// </summary>
        ///<param name="e">Source</param>
        public UnableToPurgeException( Exception e )
            : base( "Gas operation was unable to purge the system!", e )
        {
            // Do Nothing
        }
    }

#endregion Exceptions

} // end-namespace
