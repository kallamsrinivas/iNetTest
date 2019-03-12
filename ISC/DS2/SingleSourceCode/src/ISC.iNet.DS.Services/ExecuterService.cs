using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.Instrument.Driver;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to execute docking station actions.
    /// </summary>
    public sealed class ExecuterService : Service, IExecuterService
    {
        #region Fields

        private const int _INSTRUMENT_ON_PERIOD = 10;

        /// <summary>
        /// The number of milliseconds to open the gas ports for relieving internal pressure.
        /// </summary>
        private const int _RELIEVE_PRESSURE_PUMP_TIME = 2000;	// milliseconds.

        /// <summary>
        /// The port to open to relieve gas pressure.
        /// </summary>
        private const int _RELIEVE_PRESSURE_PORT = 1;

        private DockingStationAction _nextAction = null;
        private DockingStationEvent _defaultEvent = null;
        private DockingStationEvent _dsEvent = null;
        private DockingStationAction _executingAction = null;

        private object _lock = new object();

        private bool _instrumentOff;
        int _instrumentOnCount = 0;
        private bool _deadBatteryCharging = false; 
        private bool _deadBatteryChargingPeriodDone = false;
        private DateTime _deadMxCheckTime = DateTime.MinValue;  // time we last tried to ping a dead MX4/MX6.
        private object _deadBatteryLock = new object();  // Need an object for thread sychronization.
        private DateTime _lastBeat = DateTime.MinValue;

        static private readonly string OPERATIONS_ASSEMBLY_FULL_NAME;
        static private readonly string OPERATIONS_NAMESPACE;

        private bool _unserialized = false;

        private DockingStationError _lastReportedError;

        private bool _isInstrumentDiscovered;

		// Dictionary used to perform fast search on keys with O(1) efficiency 
		private Dictionary<string, string> _replacedEquipment = new Dictionary<string, string>();

        #endregion

        #region Constructors

        /// <summary>
        /// Class constructor
        /// </summary>
        static ExecuterService()
        {
            // We need the full assembly name of the Operations assembly, for use in the 
            // CreateOperation method.  e.g. "ISC.iNet.DS.Services, Version=5.0.0.1, Culture=neutral, PublicKeyToken=null".
            // Although we use the DiscoveryOperation to do that, any class in that assembly would work.
            OPERATIONS_ASSEMBLY_FULL_NAME = typeof( DiscoveryOperation ).Assembly.FullName;

            string typeString = typeof( DiscoveryOperation ).ToString();

            OPERATIONS_NAMESPACE = typeString.Substring( 0, typeString.LastIndexOf( '.' )  + 1 ); // add 1, so the '.' is included, too.
        }

        /// <summary>
        /// Creates a new instance of an ExecuterService class.
        /// </summary>
        public ExecuterService( Master master )
            : base( master )
        {
            IdleTime = new TimeSpan( 0, 0, 0, 0, 500 ); // millis
        }

        #endregion Constructors

		#region Properties

		/// <summary>
		/// Returns the list of replaced instrument/docking station serial numbers
		/// for the current account group.
		/// </summary>
		public Dictionary<string, string> ReplacedEquipment
		{
			get
			{
				if ( _replacedEquipment == null )
				{
					_replacedEquipment = new Dictionary<string, string>();
				}

				return _replacedEquipment;
			}
			set
			{
				_replacedEquipment = value;
			}
		}

		#endregion

		#region Methods

		private DateTime LastHeartBeat
        {
            get { return _lastBeat; }
            set
            {
                _lastBeat = value;
                Log.Trace( "LASTHEARTBEAT SET TO " + Log.DateTimeToString( _lastBeat ) );
            }
        }

        public bool DeadBatteryCharging
        {
            get
            {
                lock ( _deadBatteryLock )
                {
                    return _deadBatteryCharging;
                }
            }
            set
            {
                lock ( _deadBatteryLock )
                {
                    _deadBatteryCharging = value;

                    if (value == false)
                        _deadBatteryChargingPeriodDone = false;
                }
            }
        }

        public bool DeadBatteryChargingPeriodDone
        {
            get
            {
                lock (_deadBatteryLock)
                {
                    return _deadBatteryChargingPeriodDone;
                }
            }
            set
            {
                lock (_deadBatteryLock)
                {
                    _deadBatteryChargingPeriodDone = value;
                }
            }
        }

        /// <summary>
        /// Returned value is number of minutes.
        /// </summary>
        private int DeadBatteryChargePeriod
        {
            get
            {
                // INS-1255 JMP 8/20/2010 - Changed to return 1 minute for MX4.
                return ( Configuration.DockingStation.Type == DeviceType.MX4 ) ? 1 : 20;
            }
        }

        /// <summary>
        /// This method Implements the thread.start for this service.
        /// </summary>
        protected override void Run()
        {
            lock ( _lock )
            {
                // If ExecuteRun returns success, and if it actually did something
                // (i.e, executingAction is not null), then clear our last error.
                if ( ExecuteRun() && _executingAction != null )
                    _lastReportedError = null;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// THIS METHOD IS INTENDED TO ONLY BE CALLED BY Run().
        /// </remarks>
        /// <returns>
        /// false is only returned if an exexpected exception is caught.
        /// Otherwise true is returned.
        /// </returns>
        private bool ExecuteRun()
        {
            _executingAction = null;

            bool success = true;

            try
            {
                // If the menu is active, skip the operation for now.
                if ( Master.ConsoleService.IsMenuActive )
                    return true;

                // Hold the action to process in a local variable; clear _nextAction for
                // use by Execute() and Discover(), preventing the other methods from
                // changing the action that is being processed.
                _executingAction = _nextAction;
                _nextAction = null;

                // Update the LCD.
                if ( !Configuration.DockingStation.IsSerialized() )
                {
                    _unserialized = true; // once we detect we're unserialized, we remember that until we're rebooted.
                    Master.ConsoleService.UpdateState( ConsoleState.NotSerialized ); // Update the state.
					return success;
                }
                // IDS encountered error reading config files?  display an error unless we're about to reboot.
                else if ( Configuration.HasConfigurationError && !_unserialized )
                {
                    Controller.RunState |= Controller.State.ConfigError;
                    Master.ConsoleService.UpdateState( ConsoleState.ConfigurationError ); // Update the state.
                }
				else if ( HandleReplaced() )
				{
					// continue to call iNet periodically
					HandleHeartBeat();
					return success;
				}
				else
				{
                    // If the SafeCore module is on, turn it off before going idle since there is nothing for it to do.
                    // (See http://svrdevjira2/browse/INETQA-1951) for details to what led to this "if" block.)
					if ( Configuration.DockingStation.Type == DeviceType.SC
                    && Master.Instance.SwitchService.IsDocked() && ( _executingAction is NothingAction ) && !_instrumentOff )
					{
                        ExecuteInstrumentTurnOff( "TURNING OFF MODULE DUE TO INACTIVITY" );
					}
					Master.ConsoleService.UpdateState( _executingAction ); // Update the state with the appropriate action.
				}

                // Check the instrument software version, and serial number, etc.
                if ( HandleDockedInstrument() ) // returns true if handled (we don't need go any further)
                {
                    if ( !_instrumentOff )
                        ExecuteInstrumentTurnOff( "TURNING OFF INSTRUMENT AFTER HANDLING INSTRUMENT ERROR" );
                    // If we're to go no further (due to sensor error, etc., we need to at least
                    // have the reporter service still upload any queued errors which may be indicating the problem
                    // preventing us from going further.
                    Master.ReporterService.ReportQueuedErrors();
                    return success;
                }

                // If there is an action to execute, execute it.
                if ( HandleExecutingAction() )
                    return success;

                // If no operation has happened for 1 minute, send a heartbeat.
                if ( HandleHeartBeat() )
                    return success; // returns true if heartbeat performed.

                // For dead rechargeables, attempt to ping the instrument every 20 minutes.
                if ( HandleDeadBatteryCharging() )
                {
                    //Suresh 03-JAN-2012 INS-2253
                    // If we're to go no further (due to battery error (dead battery), etc., we need to at least
                    // have the reporter service still upload any queued errors which may be indicating the problem
                    // preventing us from going further.
                    Master.ReporterService.ReportQueuedErrors(); 

                    return success;
                }

                // If not rechargeable instrument, and we've been unable to turn the instrument on,
                // (ChargingState == Error), then we keep retrying to discover the instrument.
                // This doesn't make sense:  if instrument is not rechargeable, and can't be turned
                // on then assumption is that battery is dead.  So there's no real reason to continue
                // to retry.  Functional spec says we should, though, so we do.
                if ( !Configuration.DockingStation.IsRechargeable() )
                {
                    DeadBatteryCharging = false;

                    // If the instrument docked needs charging and is not charging start charging it.
                    if ( Controller.IsDocked() && ( Master.ChargingService.State == ChargingService.ChargingState.Error ) )
                    {
                        // Attempt to turn on the instrument.  We need to call the SwitchService's Discover 
                        // and not the ExecuterService's Discover since the SwitchService caches/clears
                        // some important state information whenever an instrument is successfully 'discovered'
                        Discover();
                    }
                }

                // If the instrument is on, turn it off since there is nothing for it to do.
                if ( Configuration.DockingStation.Type != DeviceType.SC && Controller.IsDocked() && !_instrumentOff )
                {
                    if ( ++_instrumentOnCount > _INSTRUMENT_ON_PERIOD )
                    {
                        // Do not physically turn off instrument if it's an MX6 with a rechargable battery.
                        // We want to leave it on so the chargingservice can poll the instrument periodically
                        // to see when the instrument is finished charging.  When the instrument reports that
                        // it's fully charged, the chargingservice will then turn off the instrument.
                        //
                        // Note that we still clear the instrumentOff and instrumentOnCount flags here.
                        // Even though we're not turning off the mx6 instrument, for our purposes we can
                        // treat it as being off.
                        ExecuteInstrumentTurnOff( "TURNING OFF INSTRUMENT DUE TO INACTIVITY" );
                    }
                }
                else if ( !Controller.IsDocked() )
                {
                    _instrumentOff = true;
                    _instrumentOnCount = 0;
                }
            }
            catch ( Exception e )
            {
                // Enable the menus.
                Master.ConsoleService.MenuEnabled = true;

                // If this is an instrument action and the instrument is undocked, skip it.
                if ( ( e is CommunicationAbortedException ) || ( e is InstrumentNotDockedException ) || ( ( _executingAction is InstrumentAction ) && !Controller.IsDocked() ) )
                {
                    // Remove the action, it is never needed.
                    Master.ConsoleService.UpdateState( ConsoleState.UndockedInstrument );
                    return success;
                }

                if (e is SensorErrorModeException) 
                {
                    //Suresh 05-JAN-2012 INS-2564
                    if (Master.SwitchService.Instrument.SensorsInErrorMode.Count > 0)
                    {
                        // INS-8631 RHP v7.5 - Ignore displaying position if sensor Position is 0
                        if (Master.SwitchService.Instrument.SensorsInErrorMode[0].Position > 0)
                            Master.ConsoleService.UpdateState(ConsoleState.SensorError, new string[] {"POSITION", " " + Master.SwitchService.Instrument.SensorsInErrorMode[0].Position.ToString()} );    
                        else
                            Master.ConsoleService.UpdateState(ConsoleState.SensorError);
                        //Suresh 05-JAN-2012 INS-2564
                        ReportInstrumentSensorErrors(Master.SwitchService.Instrument.SerialNumber, Master.SwitchService.Instrument.SensorsInErrorMode);
                    }
                    return success;
                }

                if ( e is InstrumentSystemAlarmException ) // SGF  Nov-23-2009  DSW-355  (DS2 v7.6)
                {
                    Master.ConsoleService.UpdateState( ConsoleState.InstrumentSystemAlarm );
                    Master.ExecuterService.ReportExceptionError(e); //Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593
                    return success;
                }

                if (e is HardwareConfigurationException)
                {
                    HardwareConfigurationException hce = (HardwareConfigurationException)e;
                    Master.ConsoleService.UpdateState(ConsoleService.MapHardwareConfigError(hce));
                    return success;
                }

                // INS-7657 RHP v7.5.2 Display Instrument Not Ready Message to be specific that the error is due to Sesnor not biased within 2 hours
                if (e is InstrumentNotReadyException)
                {
                    Master.ConsoleService.UpdateState(ConsoleState.InstrumentNotReady);
                    //Master.ExecuterService.ReportExceptionError(e);
                    return success;
                }

                success = false;

                Log.Error( Name, e );
                Master.ConsoleService.UpdateState( ConsoleState.Unavailable );

                ReportExceptionError( e );

                // Speed up the process of getting the error to the server.
                //HeartBeat();
            }

            return success;
        }

        /// <summary>
        /// Helper method for ExecuteRun().
        /// </summary>
        /// <param name="msg"></param>
        private void ExecuteInstrumentTurnOff( string msg )
        {
            Master.ChargingService.Paused = true;

            Log.Debug( Name + ": " + msg );

			// If the instrument's sensors are turned off because the DS went idle,
			// then the ChargingService should also be restarted so that it will immediately 
			// determine the current charging status of the docked instrument.
			bool needsRestarted = false;
            InstrumentTurnOffEvent turnOffEvent = null;

            try
            {
                // TODO #1: It would be nice if ChargingService's ChargingState is NotCharging, then we don't 
                // execute the InstrumentTurnOffOperation, under the assumption that NotCharging means the
                // instrument has already been turned off by the ChargingService because it detected the intrument
                // was fully charged. Caveat:previous statement mostly applies to MX6. Not sure about other
                // instrument instrument types; particularly non-rechargeables in which ChargingState is, perhaps,
                // *always* NotCharging.
                InstrumentTurnOffOperation turnOffOperation = new InstrumentTurnOffOperation( InstrumentTurnOffOperation.Reason.Idle );
                turnOffEvent = (InstrumentTurnOffEvent)turnOffOperation.Execute();

				// Postponed is returned when we want to turn off MX6 sensors, but the instrument is still warming up.
				// The MX6 does not support going from WarmingUp to Charging.
				if ( turnOffEvent.TurnOffAction != TurnOffAction.Postponed )
				{
					_instrumentOff = true;
					needsRestarted = true; 
				}

				// We reset the instrument on count even if the turn off action is postponed
				// so that we will try again in 5 seconds instead of 500ms.
                _instrumentOnCount = 0;
            }
            finally
            {
				if ( needsRestarted )
				{
					Log.Debug( Name + ": RESTARTING ChargingService DUE TO INACTIVITY" );
					// TODO #2: Currently, calling Restart() just sets the ChargingState to Charging. What we should
					// try and do is use the TurnOffReason returned in the turnOffEvent too "seed" the ChargingState
					// whenever we do a Restart. i.e., if TurnOffAction is TurnOffSensors, then have Restart() set
					// the ChargingState to Charging (like it currently does), but if action is Shutdown, then have
					// Restart() set the ChargingState to NotCharging.
					// ChargingService.ChargingState state = Master.ChargingService.State;
					// Master.ChargingService.Restart( NotCharging || Charging ? );
					Master.ChargingService.Restart();
				}
				else
				{
					Master.ChargingService.Paused = false;
				}
            }
        }

        /// <summary>
        /// Reports the exception to iNet, but only if the last exception
        /// wasn't the same.
        /// </summary>
        /// <param name="ex"></param>
        public void ReportExceptionError(Exception ex)
        {
            DockingStationError dsError = new DockingStationError( ex.ToString() );

            //Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593
            if (ex is InstrumentSystemAlarmException)
            {
                InstrumentSystemAlarmException instsysex = (InstrumentSystemAlarmException)ex;
                dsError = new DockingStationError(instsysex.ToString(), instsysex.SerialNumber, instsysex.ErrorCode.ToString());
            }

            // INS-7657 RHP v7.5.2 Display Instrument Not Ready Message to be specific that the error is due to Sesnor not biased within 2 hours
            //if (ex is InstrumentNotReadyException)
            //{
            //    InstrumentNotReadyException instnotreadyex = (InstrumentNotReadyException)ex;
            //    dsError = new DockingStationError(instnotreadyex);
            //}

            if ( _lastReportedError != null && _lastReportedError.Description == dsError.Description )
            {
                Log.Debug( "ReportException: Ignoring duplicate " + ex.GetType().ToString() );
                return;
            }

            _lastReportedError = dsError;

            // Log the error to be sent in the next heartbeat by the reporter.
            Master.ReporterService.ReportError(dsError); //Suresh 15-SEPTEMBER-2011 INS-1593
        }

		/// <summary>
		/// Helper method for Run().  Check to see if the docked instrument 
		/// or docking station should be returned to ISC.
		/// </summary>
		/// <returns>true - if replaced equipment detected</returns>
		private bool HandleReplaced()
		{
			// let the docking station initialize before doing any replaced checks
			if ( Master.Instance.SwitchService.InitialReadSettingsNeeded )
			{
				return false;
			}

			// handle disabled replaced instrument
			if ( Master.Instance.SwitchService.IsInstrumentReplaced )
			{
				// instrument was already disabled, but is still docked
				Master.Instance.ConsoleService.UpdateState( ConsoleState.ReturnDisabledInstrument );

				return true;
			}

			// handle enabled replaced instrument
			if ( !Master.Instance.SwitchService.InitialInstrumentSettingsNeeded )
			{
				// separate copy of serial number is to ensure the wrong instrument is not accidently disabled
				string serialNumber = Master.Instance.SwitchService.Instrument.SerialNumber;
				
				if ( ReplacedEquipment.ContainsKey( serialNumber ) )
				{
					Master.Instance.ConsoleService.UpdateState( ConsoleState.ReturnInstrument );
					
					InstrumentDisableReplacedAction action = new InstrumentDisableReplacedAction();
					action.ReplacedSerialNumber = serialNumber;

					_executingAction = action;
					HandleExecutingAction();

					// Assume that the operation was successfully completed, if it wasn't we would still 
					// want the next ExecuteRun pass to get caught in this block to try and disable the
					// instrument again.
					return true;
				}

			}

			// handle replaced docking station
			if (ReplacedEquipment.ContainsKey(Configuration.DockingStation.SerialNumber))
			{
				Master.Instance.ConsoleService.UpdateState( ConsoleState.ReturnDockingStation );

				return true;
			}

			// no equipment detected that was replaced
			return false;
		}

        /// <summary>
        /// Helper method for Run(). Check the instrument software version and serial number, etc.
        /// </summary>
        /// <returns>
        /// true if handled. (The calling Run() method doesn't need to do anything further.)
        /// </returns>
        private bool HandleDockedInstrument()
        {
            if ( !Controller.IsDocked() || !_isInstrumentDiscovered )
                return false;

            DomainModel.Instrument dockedInstrument = Master.Instance.SwitchService.Instrument;

            // Make sure instrument matches the type of IDS it's docked in.
            // We need to watch out for things like trying to dock a GasBadge Plus on a GBPRO IDS.)
			// A Ventis Pro Series instrument on an MX4 docking station is allowed. 
             // We do not allow Ventis instrument on Ventis-LS Docking station INS-6434
            if (!Configuration.DockingStation.IsDockedInstrumentSupported(dockedInstrument))
            {
                _executingAction = null;
                //change the console state and log the message only when it is not already in required state
                if ( Master.ConsoleService.CurrentState != ConsoleState.UnsupportedInstrument )
                {
                    Log.Error( "Wrong instrument type! (" + dockedInstrument.Type.ToString() + ")" );
                    Master.ConsoleService.UpdateState( ConsoleState.UnsupportedInstrument );
                }
                return true;
            }

            // The instrument must have a serial number.
            if ( dockedInstrument.SerialNumber == string.Empty )
            {
                _executingAction = null;
                //change the console state and log the message only when it is not already in required state
                if (Master.ConsoleService.CurrentState != ConsoleState.UnserializedInstrument)
                {
                    Log.Error("Instrument has no Serial Number!");
                    Master.ConsoleService.UpdateState(ConsoleState.UnserializedInstrument);
                }
                return true;
            }
 
            // This instrument must not have any sensors in error mode.
            if (Master.SwitchService.Instrument.SensorsInErrorMode.Count > 0) //Suresh 05-JAN-2012 INS-2564
            {
                _executingAction = null;
                //change the console state and log the message only when it is not already in required state
                if (Master.ConsoleService.CurrentState != ConsoleState.SensorError)
                {
                    Log.Error(string.Format("SENSOR IN ERROR MODE!"));
                    // INS-8631 RHP v7.5 - Ignore displaying position if sensor Position is 0
                    if (Master.SwitchService.Instrument.SensorsInErrorMode[0].Position > 0)
                        Master.ConsoleService.UpdateState(ConsoleState.SensorError, new string[] { "POSITION", " " + Master.SwitchService.Instrument.SensorsInErrorMode[0].Position.ToString()} );
                    else
                        Master.ConsoleService.UpdateState(ConsoleState.SensorError);
                }
                return true;
            }

            // This instrument must have sensors installed, and at least one must be enabled.
            int sensorCount = 0;
            int enabledSensorCount = 0;
            for ( int i = 0; i < dockedInstrument.InstalledComponents.Count; i++ )
            {
                InstalledComponent installedComponent = (InstalledComponent)dockedInstrument.InstalledComponents[i];
                if (!(installedComponent.Component is Sensor))
                    continue;

                sensorCount++;

                if (installedComponent.Component.Enabled)
                    enabledSensorCount++;
            }
            if (sensorCount == 0)
            {
                _executingAction = null;
                //change the console state and log the message only when it is not already in required state
                if (Master.ConsoleService.CurrentState != ConsoleState.SensorError)
                {
                    Log.Error("NO INSTALLED SENSORS FOUND!");
                    Master.ConsoleService.UpdateState(ConsoleState.SensorError);    
                }
                return true;
            }
            if (enabledSensorCount == 0)
            {
                _executingAction = null;
                //change the console state and log the message only when it is not already in required state
                if (Master.ConsoleService.CurrentState != ConsoleState.NoEnabledSensors)
                {
                    Log.Error("NO ENABLED SENSORS FOUND!");
                    Master.ConsoleService.UpdateState(ConsoleState.NoEnabledSensors);
                }
                return true;
            }

            // For TX1 (Aurora) instruments, make sure that there are two sensors installed.  Anything less is to be rejected.
			// Allows instrument settings read to occur so iNet can also detect the issue and send out an alert.  The instrument 
			// settings read is only needed when the DS is not currently in Cal Station mode.
            if ( dockedInstrument.Type == DeviceType.TX1 && ( !Master.Instance.SwitchService.InitialInstrumentSettingsNeeded || !Configuration.Schema.Activated ) )
            {
                if (sensorCount < 2)
                {
                    _executingAction = null;

                    // Display an appropriate error state on the console
                    if (Master.ConsoleService.CurrentState != ConsoleState.SensorMissing)
                    {
                        Log.Error("TX1 MUST HAVE TWO SENSORS INSTALLED");
                        Master.ConsoleService.UpdateState(ConsoleState.SensorMissing);
                    }
                    return true;
                }
            }

            if ( Master.SwitchService.InstrumentUpgradeError )
            {
                _executingAction = null;
                //change the console state and log the message only when it is not already in required state
                if (Master.ConsoleService.CurrentState != ConsoleState.UpgradingInstrumentError)
                {
                    Log.Error("Upgrading Instrument Error!"); 
                    Master.ConsoleService.UpdateState(ConsoleState.UpgradingInstrumentError);
                }
                return true;
            }

            //Suresh 06-FEB-2012 INS-2622
            if (Master.SwitchService.Instrument.InstrumentInCriticalError == true)
            {
                _executingAction = null;
                //change the console state and log the message only when it is not already in required state
                if (Master.ConsoleService.CurrentState != ConsoleState.InstrumentSystemAlarm)
                {
                    Log.Error(string.Format("INSTRUMENT IN CRITICAL ERROR!"));
                    // INS-8446 RHP v7.6 - Display the critical error code identified on the instrument 
                    Master.ConsoleService.UpdateState(ConsoleState.InstrumentSystemAlarm, Master.SwitchService.Instrument.InstrumentCriticalErrorCode);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Helper method for Run().
        /// </summary>
        /// <returns>
        /// true if handled. (The calling Run() method doesn't need to do anything further.)
        /// false is returned if there is no action to execute (which would happen when scheduler
        /// says there is nothing to do), or if action is an InstrumentAction but instrument has been undocked.
        /// </returns>
        private bool HandleExecutingAction()
        {
            // If there is an action to execute, execute it.
            if ( _executingAction == null || _executingAction is NothingAction )
                return false;

            // If this is an instrument action and the instrument is undocked, skip it.
            if ( ( _executingAction is InstrumentAction ) && ( !Controller.IsDocked() ) )
            {
                _executingAction = null;
                Master.ConsoleService.UpdateState( ConsoleState.UndockedInstrument ); // or ConsoleState.CalibrationFailure or ConsoleState.Ready
                return true;  // Remove the action, it is never needed.
            }

            IOperation operation = MapOperation( _executingAction ); // Get the corresponding operation for the action.

            if ( operation != null )
            {
                Master.ConsoleService.MenuEnabled = false;  // Lock the menus.

                // Execute operation and get the resulting event.
                _dsEvent = null;

                DateTime startTime = DateTime.MinValue;  // Time the operation.

				try
				{
					// We don't want the charging service trying to talk to the instrument if
					// at the same time the operation we're about to execute is talking to the instrument.
					// So, that probably means we could/should be more selective here and not pause
					// if the operation is a non-instrument operation? - JMP, 5/9/2013.
					Master.ChargingService.Paused = true;

					Log.Debug( Name + "   " + _executingAction.ToString() );

					_dsEvent = ExecuteOperation( operation );  // Execute the event.
				}
				catch ( InstrumentUndockedDuringPauseException ) // SGF  11-Mar-2013  INS-3962
				{
					_instrumentOff = true;
					_executingAction = null;
					Master.ConsoleService.UpdateState( ConsoleState.Ready );
					return true;
				}
				catch ( InstrumentUndockedDuringDisableReplacedException )
				{
					_instrumentOff = true;
					_executingAction = null;

					// if the instrument was undocked, ensure the red led and return to ISC
					// message has some time to appear on the docking station
					Master.ConsoleService.UpdateState( ConsoleState.ReturnDisabledInstrument );
					System.Threading.Thread.Sleep( 10000 );
					
					return true;
				}
				catch ( FlowFailedException ffe )
				{
					Log.Error( "Flow failed", ffe );

                    Master.Instance.Scheduler.ReForceEvent( _executingAction );

                    if (Master.SwitchService.BadPumpTubingDetectedDuringBump || Master.SwitchService.BadPumpTubingDetectedDuringCal)
                        _nextAction = Master.ReporterService.ReportBadCradleTubingError();
                    else
                        _nextAction = Master.ReporterService.ReportFlowFailedError( ffe.GasEndPoint );
				}
				catch ( Exception e )
				{
					Log.Error( "ExecuterService.operation.Execute", e );

					// If this is an instrument action and the instrument is undocked, skip it.
					if ( ( _executingAction is InstrumentAction ) && ( !Controller.IsDocked() ) )
					{
						// Remove the action, it is never needed.
						Master.ConsoleService.UpdateState( ConsoleState.UndockedInstrument );
						return true;
					}

					if ( e is InstrumentSystemAlarmException ) //  SGF  Nov-23-2009  DSW-355  (DS2 v7.6)
						Master.ConsoleService.UpdateState( ConsoleState.InstrumentSystemAlarm );

					if ( e is HardwareConfigurationException )
					{
						HardwareConfigurationException hce = (HardwareConfigurationException)e;
						Master.ConsoleService.UpdateState( ConsoleService.MapHardwareConfigError( hce ) );
					}

                    // INS-7657 RHP v7.5.2 Display Instrument Not Ready Message to be specific that the error is due to Sesnor not biased within 2 hours
                    if (e is InstrumentNotReadyException)
                        Master.ConsoleService.UpdateState(ConsoleState.InstrumentNotReady);
                       
					// Must do a 'throw e' and not just a 'throw'!!!
					// Otherwise, the outer catch at the end of this function catches a
					// NullReferenceException for some unknown reason. A bug in CF perhaps?
					//
					// TODO - This was a definite problem in Compact Framework 1.0.
					// Maybe the problem no longer exists in Compact Framework 2.0? If not
					// we could just revert back to a simple "throw;"  - JMP 9/14/2007
					throw e;
				}
                finally
                {
                    // If we just performed an instrument-related operation, then assume instrument is now on.
                    // We'd prefer to just check dsEvent, but it may not be returned if the executed operation
                    // throws. (It may throw AFTER turning on the instrument.) Therefore, we do a secondary
                    // check on the action. (todo: we may be able to get away with ONLY checking the action?
                    // i.e., does every InstrumentAction result in an InstrumentEvent? I'm not sure.) - JMP, dev 5353, 3/4/2013
                    if ( ( ( _dsEvent is InstrumentEvent ) || ( _executingAction is InstrumentAction ) ) && Controller.IsDocked() )
                        _instrumentOff = false;

                    RelieveInternalPressure( _dsEvent ); // Relieve any internal pressure that may have built up.

					Master.ChargingService.Paused = false;
                }

                // If there is an event, report the event and get the next action.
                if ( _dsEvent != null )
                {
                    _dsEvent.DockingStation.SerialNumber = Configuration.DockingStation.SerialNumber;

                    operation = null;

                    Log.Debug( Name + ":  reporting event " + _dsEvent.ToString() );
                    Stopwatch stopWatch = Log.TimingBegin("REPORTING EVENT");

                    _nextAction = Master.ReporterService.ReportEvent( _dsEvent );

                    Log.TimingEnd( "REPORTING EVENT", stopWatch );
                }
            }

            Master.ConsoleService.MenuEnabled = true;  // Unlock the menus.
            LastHeartBeat = DateTime.UtcNow;  // Reset last operation time.

            return true;
        }

        /// <summary>
        /// Helper method for Run().
        /// <para>
        /// If no operation has happened for 1 minute, send a heartbeat.
        /// </para>
        /// </summary>
        private bool HandleHeartBeat()
        {
            DateTime utcNow = DateTime.UtcNow;

            TimeSpan elapsed = utcNow - LastHeartBeat;

            // If time that's elapsed since last heartbeat exceeds 'silentperiod', then it's time
            // to do a heartbeat.
            // Also, note that elapsedSeconds may be negative if the clock jumps backwards in time.
            // This can happen if the clock is re-synched with iNet or due to daylight saving time
            // suddenly kicking in or out. If we run into this situation, we just do a heartbeat immediately.
            // Otherwise, it might be a long time before a heartbeat would occur, because since time
            // has gone backwards, we'd have to then wait for it to "catch up".
            long secondsElapsed = (long)elapsed.TotalSeconds;

            if ( secondsElapsed < 0L || secondsElapsed > Configuration.DockingStation.InetPingInterval )
            {
                Log.Debug( "HEARTBEAT!!! (elapsed = " + (long)elapsed.TotalSeconds + "s)" );

                _nextAction = Master.ReporterService.ReportEvent( _defaultEvent );

                LastHeartBeat = utcNow; // Reset last operation time.

                return true;
            }

            return false;
        }

        /// <summary>
        /// Helper method for Run().
        /// </summary>
        /// <returns>true if able to successfully 'discover' the instrument.  false otherwise.</returns>
        private bool HandleDeadBatteryCharging()
        {
            if ( Configuration.DockingStation.IsRechargeable() == true
            && Controller.IsDocked() == true
            && DeadBatteryCharging == true
            && ( ( DateTime.UtcNow - _deadMxCheckTime ).TotalMinutes >= DeadBatteryChargePeriod ) )
            {
                DeadBatteryChargingPeriodDone = true;
                // Attempt to turn on the instrument.  We need to call the SwitchService's Discover 
                // and not the ExecuterService's Discover since the SwitchService caches/clears
                // some important state information whenever an instrument is successfully 'discovered'
                Discover();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Relieves the pressure in the tubing of the docking station.
        /// </summary>
        private void RelieveInternalPressure( DockingStationEvent dsEvent )
        {
            // Only bother to relieve the pressure when we detect pump was used at some point
            // after the event was created.
            if ( dsEvent != null && dsEvent.Time != DateTime.MinValue && Pump.GetTimePumpLastStarted() > dsEvent.Time )
                Pump.RelieveInternalPressure();
        }

        /// <summary>
        /// </summary>
        public void ReportDiscoveredInstrumentErrors( InstrumentEvent instrumentEvent )
        {
            if ( instrumentEvent.DockedInstrument.SerialNumber.Length == 0 )
                Master.ReporterService.ReportError( new DockingStationError( "Unserialized instrument detected.", DockingStationErrorLevel.Warning ) );

            ConfirmEnabledSensors(instrumentEvent.DockedInstrument);
            ReportInstrumentSensorErrors(instrumentEvent.DockedInstrument.SerialNumber, instrumentEvent.DockedInstrument.SensorsInErrorMode); //Suresh 05-JAN-2012 INS-2564
        }

        private void ConfirmEnabledSensors(ISC.iNet.DS.DomainModel.Instrument dockedInstrument)
        {
            int numSensors = 0;
            int numEnabledSensors = 0;
            foreach (InstalledComponent ic in dockedInstrument.InstalledComponents)
            {
                if (!(ic.Component is Sensor))
                    continue;

                numSensors++;

                if (ic.Component.Enabled)
                    numEnabledSensors++;
            }
            if (numSensors == 0)
            {
                Master.ReporterService.ReportError(new DockingStationError("Instrument has no installed sensors.", DockingStationErrorLevel.Warning, dockedInstrument.SerialNumber));
            }
            else if (numEnabledSensors == 0)
            {
                string msg = string.Format("Instrument has no enabled sensors");
                Master.ReporterService.ReportError(new DockingStationError(msg, DockingStationErrorLevel.Warning, dockedInstrument.SerialNumber));
            }
        }

        //Suresh 05-JAN-2012 INS-2564
        private void ReportInstrumentSensorErrors(string instrumentSerialNumber, List<InstalledComponent> sensorsInErrorMode)
        {
            // Report on sensors that are in error mode. Lower level code that reads the sensors will have set
            // the serial number to empty if the instrument is reporting there's some error with the sensor.
            foreach (InstalledComponent ic in sensorsInErrorMode) //Suresh 05-JAN-2012 INS-2564
            {
                string msg = string.Format("Sensor Error encountered (DataFault, Uninitialized, or Undefined), Sensor position={0}", ic.Position);
                Master.ReporterService.ReportError(new DockingStationError(msg, DockingStationErrorLevel.Warning, instrumentSerialNumber));
            }
        }

        /// <summary>
        /// Performs a discovery operation.
        /// </summary>
        /// <returns>The resulting event</returns>
        public DockingStationEvent Discover()
        {
            // We log the thread name so we know which service is calling for a discover: executor or switch.
            Log.Debug( string.Format( "{0} service has invoked a Discover()", Thread.CurrentThread.Name ) );

            Log.Assert( Thread.CurrentThread.Name == Master.SwitchService.Name.Replace( "Service", string.Empty )
            ||          Thread.CurrentThread.Name == Master.ExecuterService.Name.Replace( "Service", string.Empty ),
                "ONLY THE EXECUTER AND SWITCH SERCVICES ARE ALLOWED TO CALL DISCOVER()" );

            lock ( _lock )
            {
                // Make sure we clear the forced event queue when an instrument is docked, 
                // just in case there are events left on the queue from the last time an
                // instrument was docked.
                Master.Instance.Scheduler.ClearQueuedActions();

		        //Suresh 05-JAN-2012 INS-2564
                Master.SwitchService.Instrument.SensorsInErrorMode.Clear();

                Master.SwitchService.Instrument.InstrumentInCriticalError = false; //Suresh 06-FEB-2012 INS-2622

                Master.ConsoleService.MenuEnabled = false; // Disable the menus.

                Log.Debug( Name + ".Discover" );

                 //Suresh 03-JAN-2012 INS-2253
                //if we are discovering the instrument for DeadBattery scenario then we don't want to change
                //the console to "discoverying" instead we want to retain the battery status error/warning message in the console.
                //Suresh 29-Feb-2012 INS-4344 // docking station is tries to re-discovering GBPro/GBPlus as it has not responded to its previous ping
                if ( !Controller.IsDocked()
                || ( !DeadBatteryCharging && !( !Configuration.DockingStation.IsRechargeable() && ( Master.ChargingService.State == ChargingService.ChargingState.Error ) ) ) )
                {
                    Master.ChargingService.State = ChargingService.ChargingState.NotCharging;
                    Master.ConsoleService.UpdateState( ConsoleState.Discovering );
                }

                // Pause the charging service so it doesn't try to query the instrument while we're discovering.
                Master.ChargingService.Paused = true;

                DiscoveryOperation discoveryOperation = new DiscoveryOperation(); // Create new discovery operation.

                try
                {
                    _defaultEvent = ExecuteOperation( discoveryOperation ); // Execute the event.

                    DeadBatteryCharging = false;

                    // Everytime a new instrument is seen, make sure we update the charger service
                    // with its battery type so it knows whether to monitor its charging or not
                    // (lithium versus alkaline)

                    // SGF  Feb-17-2009  DSW-225
                    // Reimplemented if-statement to first test the default event, and then add the MX4 to the 
                    // subsequent if-statement in order to set the BatteryCode and the ChargingState.

                    if ( _defaultEvent is InstrumentEvent ) // SGF  Feb-17-2009  DSW-225
                    {
                        if ( Configuration.DockingStation.IsRechargeable() )
                        {
                            Master.ChargingService.BatteryCode = string.Empty;

                            DomainModel.Instrument instr = ( (InstrumentEvent)_defaultEvent ).DockedInstrument;
                            InstalledComponent installedBattery = instr.InstalledComponents.Find( ic => ic.Component is Battery );
                            if ( installedBattery != null )
                            {
                                Battery battery = (Battery)installedBattery.Component;
                                Master.ChargingService.BatteryCode = battery.Type.Code;

                                // Everytime we see a new instrument with a rechargeable battery,
                                // assume its charging until we find out otherwise later.
                                if ( Master.ChargingService.IsBatteryRechargable() )
                                    Master.ChargingService.State = ChargingService.ChargingState.Charging;
                            }
                        }

                        // Else, Non-rechargeable instrument type, and instrument can't be turned on
                        // (which is why ChargingState is Error.
                        else if ( Master.ChargingService.State == ChargingService.ChargingState.Error )
                        {
                            Master.ChargingService.State = ChargingService.ChargingState.NotCharging;
                            Master.ConsoleService.UpdateState( ConsoleState.Discovering, (string[])null );
                        }
                    }
                    else // non-InstrumentEvent (nothing is docked)
                    {
                        Master.ChargingService.BatteryCode = string.Empty;
                    }

                    if ( _defaultEvent is InstrumentEvent )
                        ReportDiscoveredInstrumentErrors( _defaultEvent as InstrumentEvent );
                }
                catch ( InstrumentSystemAlarmException )
                {
                    Master.ConsoleService.UpdateState( ConsoleState.InstrumentSystemAlarm );
                    throw;
                }
                catch ( HardwareConfigurationException hce )
                {
                    Master.ConsoleService.UpdateState( ConsoleService.MapHardwareConfigError( hce ) );
                    throw;
                }
                // INS-7657 RHP v7.5.1 Display Instrument Not Ready Message to be specific that the error is due to Sesnor not biased within 2 hours
                catch (InstrumentNotReadyException)
                {
                    Master.ConsoleService.UpdateState(ConsoleState.InstrumentNotReady);
                    throw;
                }
                catch ( Exception e )
                {
                    Log.Error( Name + ".Discover", e );

                    // If there is an instrument docked and it failed to turn on,
                    // attempt to charge the battery.

                    if ( Controller.IsDocked() )
                    {
                        if ( Configuration.DockingStation.IsRechargeable() )
                        {
                            DeadBatteryCharging = true;
                            _deadMxCheckTime = DateTime.UtcNow;
                            Master.ChargingService.BatteryCode = string.Empty;
                            // SGF  Feb-13-2009  DSW-223  Now setting state to either LowBattery or Error based on value of DeadBatteryChargingPeriodDone
							if ( DeadBatteryChargingPeriodDone == false )
							{
								Master.ChargingService.State = ChargingService.ChargingState.LowBattery;
							}
							else if ( Master.ChargingService.State != ChargingService.ChargingState.Error )
							{
								Master.ChargingService.State = ChargingService.ChargingState.Error;

								// Do not change this message.  iNet is parsing it!
								string msg = "Battery error: could not ping instrument after dead battery charge period.";
								Log.Error( string.Format( "{0} - {1}", Name, msg.ToUpper() ) );
								DockingStationError dsError = new DockingStationError( msg, DockingStationErrorLevel.Warning );
								Master.ReporterService.ReportError( dsError );
							}
                        }
                        else  // non-rechargeable instrument.  And it's not responding?  Dead battery.
                        {
                            Master.ChargingService.State = ChargingService.ChargingState.Error;
                        }

                    }
                    else // No instrument docked anymore, but exception thrown. Unknown problem, but might just be result of undocking in the middle of discovery
                    {
                        Log.Warning( "Docking station is having problems." );
                        throw;
                    }
                }
                finally
                {
                    _instrumentOff = Controller.IsDocked() ? false : discoveryOperation.InstrumentOff;
                    Master.SwitchService.UndockInstrument(); // Clear info on previous docked instrument
                    Master.ChargingService.Paused = false;
                }

                if ( _defaultEvent is InstrumentEvent )
                {
                    Master.SwitchService.DockInstrument( ( (InstrumentEvent)_defaultEvent ).DockedInstrument ); // Cache info on newly docked instrument
                    _isInstrumentDiscovered = true;
                }
                else
                {
                    _isInstrumentDiscovered = false;
                    Master.SwitchService.UndockInstrument(); // Clear info on previous docked instrument
                }

				// When an instrument is docked (or undocked) we want _nextAction in the ExecuterService
				// to contain the appropriate action to perform next.  So we call HandleHeartBeat as well
				// to invoke the scheduling logic.  This should prevent the DS from briefly going idle 
				// after a discovery.
				Master.ExecuterService.HeartBeat();
				HandleHeartBeat();
            } // end-lock

            return _defaultEvent; // Return the next event.
        } // end-Discover()

        /// <summary>
        /// Force a heart beat if possible.
        /// </summary>
        public void HeartBeat()
        {
            LastHeartBeat = DateTime.MinValue;
        }

        /// <summary>
        /// Executes a given DockingStationAction. 
        /// Note that this method does not execute InstrumentActions.
        /// Unlike Execute() method, this method is synchronous - it will not return until execution is finished.
        /// </summary>
        /// <param name="report">If true, it will report the resulting event to the server.</param>
        /// <param name="action">The action to be performed.</param>
        /// <returns>The resulting event returned by the operation that executes.</returns>
        public DockingStationEvent ExecuteNow( DockingStationAction action, bool report )
        {
            Log.Debug( string.Format( "ExecuteNow({0},{1})", action.Name, report ) );

            lock ( _lock )
            {
                // If there's a pending reboot, then don't override it.  Immediately
                // return to let the reboot happen.   - INS-3431, 2/27/2013
                if ( _nextAction is RebootAction )
                {
                    Log.Warning( "ExecuteNow: Ignoring " + action + " due to " + _nextAction );
                    return null;
                }

                _dsEvent = null;

                // If it is not an instrument action, process it.
                if ( action is InstrumentAction )
                    return _dsEvent;

                try
                {
                    // we don't want the charging service trying to talk to the instrument if
                    // at the same time the operation we're about to execute is talking to the instrument.
                    // So, that probably means we could/should be more selective here and not pause
                    // if the operation is a non-instrument operation? - JMP, 5/9/2013.
                    Master.ChargingService.Paused = true;

                    // Get the corresponding operation for the action.
                    IOperation operation = MapOperation( action );

                    if ( operation != null )
                    {
                        Log.Debug( Name + ":  Execute( " + action.ToString() + " )" );

                        // Lock the menus.
                        Master.ConsoleService.MenuEnabled = false;

                        // Update the LCD.
                        Master.ConsoleService.UpdateState( action );

                        // Execute the operation.
                        _dsEvent = ExecuteOperation( operation );

                        //	Relieve any internal pressure that may have built up.
                        RelieveInternalPressure( _dsEvent );
                        // If report is passed in as true, report the action to the server.
                        if ( report == true && _dsEvent != null )
                        {
                            _dsEvent.DockingStation.SerialNumber = Configuration.DockingStation.SerialNumber;

                            // Report the event to reporter service and get the next action.
                            _nextAction = Master.ReporterService.ReportEvent( _dsEvent );
                        }
                        else if ( report == false )
                        {
                            // We need to force a heartbeat so that LCD gets cleared/updated
                            // call to Execute NOw
                            HeartBeat();
                        }
                    }
                    else
                    {
                        // Update the LCD.
                        Log.Debug( Name + "   Unknown action: " + action.ToString() );

                        // TODO: This state should not be hard coded, what should it be.
                        // Master.ConsoleService.UpdateState( ConsoleStates.Ready );
                    }
                }
                finally
                {
                    // If we just performed an instrument-related operation, then assume instrument is now on.
                    // We'd prefer to just check dsEvent, but it may not be returned if the executed operation
                    // throws. (It may throw AFTER turning on the instrument.) Therefore, we do a secondary
                    // check on the action. (todo: we may be able to get away with ONLY checking the action?
                    // i.e., does every InstrumentAction result in an InstrumentEvent? I'm not sure.) - JMP, dev 5353, 3/4/2013
                    if ( ( ( _dsEvent is InstrumentEvent ) || ( action is InstrumentAction ) ) && Controller.IsDocked() )
                        _instrumentOff = false;

                    Master.ChargingService.Paused = false;
                }

                // Unlock the menus.
                Master.ConsoleService.MenuEnabled = true;

                return _dsEvent;

            } // end-lock
        }

        /// <summary>
        /// Maps a docking station Action to its corresponding Operation.
        /// </summary>
        /// <remarks>
        /// Normally, Actions are automatically mapped to appropriate Operations
        /// by using their name.  e.g. a "FooBarAction" is assumed to be mapped to
        /// to a "FooBarOperation".  (See CreateAction method that this method calls.
        /// If necessary, this operation can override th automatic mapping and 
        /// instead map an action on it's own.  
        /// </remarks>
        /// <param name="dockingStationAction">Docking station action</param>
        /// <returns>Corresponding docking station operation</returns>
        private IOperation MapOperation( DockingStationAction action )
        {
            if ( action is NothingAction )
                return new InstrumentTurnOffOperation( (NothingAction)action ); // ???

            return CreateOperation( action );
        }

        /// <summary>
        /// Return an instance of the appropriate IOperation for the passed in
        /// action.
        /// </summary>
        /// <remarks>
        /// Operations are assumed to have the same base name as the actions.
        /// 
        /// e.g. if a FooBarAction" is passed in, then this routine attempts
        /// to instantiate and return a FooBarOperation.
        /// 
        /// It also assumes that all operations are in the same assembly and
        /// same namespace as the Discovery operation.
        /// 
        /// </remarks>
        /// <param name="action"></param>
        /// <returns></returns>
        private IOperation CreateOperation( DockingStationAction action )
        {
            // Get the simple name of the action.
            // e.g. if the type's full name is "ISC.A.B.C.FooBarAction", then we want simply "FooBarAction".
            string actionName = action.GetType().ToString();
            actionName = actionName.Substring( actionName.LastIndexOf( '.' ) + 1 );

            // Replace "Action" with "Operation".  e.g. "FooBarAction" becomes "FooBarOperation".
            string operationName = actionName.Replace( "Action", "Operation" );

            string fullOperationName = OPERATIONS_NAMESPACE + operationName + ", " + OPERATIONS_ASSEMBLY_FULL_NAME;
            Type operationType = Type.GetType( fullOperationName );

            // Not every action has a corresponding operation.  That's OK; just return null.
            if ( operationType == null )
            {
                Log.Trace( string.Format( "CreateOperation: No {0} found for {1}", operationName, actionName ) );
                return null;
            }

            // For each Operation, it's assumed that its parent Action class has 
            // a copy constructor.  We need to find this copy constructor.
            ConstructorInfo operationCtor = operationType.GetConstructor( new Type[] { action.GetType() } );
            Log.Assert( operationCtor != null, string.Format( "{0} class is missing a copy ctor accepting {1} argument.", operationName, actionName ) );


            if (action is DockingStationAction)
                action.DockingStation = Controller.GetDockingStation();

            if (action is InstrumentAction)
                ((InstrumentAction)action).Instrument = (ISC.iNet.DS.DomainModel.Instrument)Master.SwitchService.Instrument.Clone();

            // instantiate the Operation, passing in the action as an argument.
            return (IOperation)operationCtor.Invoke( new object[] { action } );
        }

        /// <summary>
        /// </summary>
        /// <param name="operation"></param>
        /// <returns></returns>
        private DockingStationEvent ExecuteOperation( IOperation operation )
        {
            PreExecuteOperation( operation );

            DockingStationEvent dsEvent = operation.Execute();

            PostExecuteOperation( operation, dsEvent );

            return dsEvent;
        }

        private void PreExecuteOperation( IOperation operation )
        {
            // TODO - do specific things based on specific operation types.
        }

        /// <summary>
        /// </summary>
        /// <param name="operation">The operation that was just executed</param>
        /// <param name="dsEvent">The event that was returned by the operation's Execute().</param>
        private void PostExecuteOperation( IOperation operation, DockingStationEvent dsEvent )
        {
            // TODO - do specific things based on specific operation/event types.
        }

        #endregion

    }

}
