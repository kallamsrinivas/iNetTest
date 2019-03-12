using System;
using System.Collections.Generic;
using System.Threading;
using ISC.iNet.DS.iNet;
using ISC.Instrument.Driver;
using ISC.WinCE.Logger;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services.Resources;


namespace ISC.iNet.DS.Services
{
    using ISC.iNet.DS.DomainModel; // puting this here avoids compiler's confusion of DomainModel.Instrument vs Instrument.Driver.

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Monitors the Interlock switch, the smart card slots, and the pressure switches, and reports changes to the server.
	/// </summary>
	public class SwitchService : Service, ISwitchService
	{
		#region Fields

        private bool _wasDocked = false;

        private string _wasAccountNum = string.Empty;
        private bool   _wasActivated = false;

        private bool[] _wasCardPresent = new bool[Configuration.DockingStation.NumGasPorts];
        private bool[] _wasPressureGood = new bool[Configuration.DockingStation.NumGasPorts];
        private bool[] _wasSwitchPresent = new bool[Configuration.DockingStation.NumGasPorts];
        private bool[] _changedSmartCards = new bool[Configuration.DockingStation.NumGasPorts];

        private bool[] _checkedCardIsPresent = new bool[Configuration.DockingStation.NumGasPorts]; // used exclusively by CheckSmartCardInsertions
        private bool[] _checkedSwitchIsPresent = new bool[Configuration.DockingStation.NumGasPorts]; // used exclusively by CheckPressureSwitches
        private bool[] _checkedIsPressureGood = new bool[Configuration.DockingStation.NumGasPorts]; // used exclusively by CheckPressureSwitches

        /// <summary>
        /// Indicates if an initial Discovery has been performed yet. (A Discovery has to be performed during bootup.)
        /// </summary>
        private bool _initialized;

        private Exception _lastCaughtException = null;

        /// <summary>
        /// When this is set to true, the docking station will display an error message indicating the upgrade
        /// failure, until the instrument is undocked.
        /// </summary>
        public bool InstrumentUpgradeError { get; set; }
        
        /// <summary>
        /// When this is set to true, then the currently docked instrument failed a calibration on this docking
        /// station that resulted in a zero span reserve.  Calibration failures with zero span reserves
        /// often indicates a problem with the hoses or cylinders causing no gas to reach the instrument.
        /// - INS-1279, 6/20/2011, JMP
        /// </summary>
        public bool BadGasHookup { get; set; }

        /// <summary>
        /// Holds the part number of the cylinder which is expected to have a hose problem
        /// A valid part number is present if BadGasHookup is set to true. - INS-8446 RHP v7.6
        /// </summary>
        public string BadGasHookUpCylinderPartNumber { get; set; }

        /// <summary>
        /// When this is set to true, that indicates that currently docked instrument failed calibration 
        /// due to insufficient flow, and pump voltage is greter than 200, and vaccumm is less than 6 inches.
        /// This scenario indicates that pump tubing is bad and needs to be corrected before another gas operation is tried.
        /// 12/15/2017 AJAY INS-8283: Docking station should try and detect when gas hosing is blocked or kinked.
        /// </summary>
        public bool BadPumpTubingDetectedDuringCal { get; set; }

        /// <summary>
        /// When this is set to true, that indicates that currently docked instrument failed bump 
        /// due to insufficient flow, and pump voltage is greter than 200, and vaccumm is less than 6 inches.
        /// This scenario indicates that pump tubing is bad and needs to be corrected before another gas operation is tried.
        /// 12/15/2017 AJAY INS-8283: Docking station should try and detect when gas hosing is blocked or kinked.
        /// </summary>
        public bool BadPumpTubingDetectedDuringBump { get; set; }

        private Instrument _dockedInstrument = new Instrument();

        /// <summary>
        /// The date/time the instrument returned by DockedInstrument property was actually docked.
        /// NullDateTime is returned if nothing known to be docked.
        /// </summary>
        public DateTime DockedTime { get; set; }

        public bool DockProcessing { get; set; }

        // INS-2047 ("Need to upload "Next cal date" / "Next bump date")...
        private DateTime? _nextUtcCalDate = null;  // null means 'unknown' which would also be if there are no schedules for the event.
        private DateTime? _nextUtcBumpDate = null; // null means 'unknown' which would also be if there are no schedules for the event.

        /// <summary>
        /// Upon bootup, the docking station will automatically perform a Settings Read operation on itself.
        /// This property returns false once that operation has completed.
        /// </summary>
        public bool InitialReadSettingsNeeded { get; private set; }

		/// <summary>
		/// This property returns false once a docked instrument has had its Instrument Settings Read operation performed.
		/// </summary>
		public bool InitialInstrumentSettingsNeeded { get; set; }

		/// <summary>
		/// This property returns true once a docked instrument that was replaced has been disabled and no further 
		/// operations should occur on it.
		/// </summary>
		public bool IsInstrumentReplaced { get; set; }

        /// <summary>
		/// This property returns true once a docked instrument is in system alarm state.
		/// </summary>
        public bool IsInstrumentInSystemAlarm { get; set; }

        private bool ReadSettingsNeeded { get; set; }

        private bool ReadAllCards { get; set; }


		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a SwitchService class.
		/// </summary>
        public SwitchService( Master master ) : base( master ) 
        {
            IdleTime = new TimeSpan( 0, 0, 0, 0, 500 );

            _initialized = false;

            InitialReadSettingsNeeded = ReadSettingsNeeded = ReadAllCards = true;
			InitialInstrumentSettingsNeeded = true;
			IsInstrumentReplaced = false;

            DockedTime = DomainModelConstant.NullDateTime;

            DockProcessing = false;
        }

		#endregion

		#region Properties

        /// <summary>
        /// The currently docked instrument, with information filled in during Discovery.
        /// If no instrument is currently docked, then the returned instrument's
        /// serial number will be empty and its DeviceType will be Unknown.
        /// </summary>
        public Instrument Instrument
        {
            get
            {
                return _dockedInstrument;
            }
            set
            {
                _dockedInstrument = ( value != null ) ? (Instrument)value.Clone() : new Instrument();
            }
        }

        /// <summary>
        /// The date/time the currently docked instrument is next due to be calibrated, in UTC.
        /// If a this property is set to DateTime set to Kind.Unspecified, it will automatically
        /// be converted to UTC, with assumption that the DateTime is in the global Configuration's time zone.
        /// </summary>
        /// <remarks>
        /// null = Unknown, MinDate = upon docking and not yet performed, MaxDate = upon docking, and already performed.
        /// </remarks>
        public DateTime? NextUtcCalibrationDate  // INS-2047 ("Need to upload "Next cal date" / "Next bump date")
        {
            get { return _nextUtcCalDate; }
            set
            {
                if ( value != null && value.Value.Kind != DateTimeKind.Utc )
                {
                    Log.Assert( value.Value.Kind != DateTimeKind.Unspecified, "NextCalibrationDateUtc - UNSPECIFIED KIND OF DATE ENCOUNTERED! - " + Log.DateTimeToString( value ) );

                    if ( value.Value == DateTime.MaxValue )
                        _nextUtcCalDate = DateTime.SpecifyKind( value.Value, DateTimeKind.Utc );
                    else
                        _nextUtcCalDate = Configuration.ToUniversalTime( value.Value );
                }
                else
                    _nextUtcCalDate = value;
            }
        }

        /// <summary>
        /// The date/time the currently docked instrument is next due to be bump tested, in UTC
        /// If a this property is set to DateTime set to Kind.Unspecified, it will automatically
        /// be converted to UTC, with assumption that the DateTime is in the global Configuration's time zone.
        /// </summary>
        /// <remarks>
        /// null = Unknown, MinDate = upon docking and not yet performed, MaxDate = upon docking, and already performed.
        /// </remarks>
        public DateTime? NextUtcBumpDate // INS-2047 ("Need to upload "Next cal date" / "Next bump date")
        {
            get { return _nextUtcBumpDate; }
            set
            {
                if ( value != null && value.Value.Kind != DateTimeKind.Utc )
                {
                    Log.Assert( value.Value.Kind != DateTimeKind.Unspecified, "NextUtcBumpDate - UNSPECIFIED KIND OF DATE ENCOUNTERED! - " + Log.DateTimeToString( value ) );

                    if ( value.Value == DateTime.MaxValue )
                        _nextUtcBumpDate = DateTime.SpecifyKind( value.Value, DateTimeKind.Utc );
                    else
                        _nextUtcBumpDate = Configuration.ToUniversalTime( value.Value );
                }
                else
                    _nextUtcBumpDate = value;
            }
        }

		#endregion

		#region Methods

		/// <summary>
		/// This method implements the thread start for this service.
		/// </summary>
		protected override void Run()
		{
            try
            {
                bool activated = Configuration.Schema.Activated;
                bool synchronized = Configuration.Schema.Synchronized;

                //Suresh 13-APR-2012 INS-4519 (DEV JIRA)
                //If the docking station is Initialized and Activated, but not Synchronized then set the console state to Synchronization.
                //Note: We cannot do this after Discover() method, because ExecuterService.Discover() method and 
                //ExecuterService.Run( ExchangeStatusOperation ) method runs on mutual-exclusion lock and that causes "Synchronization" state not to be changed
                //until ExchangeStatusOperation completely finishes but by that time Synchronization will be completed.
                if ( ( _initialized == true ) && ( activated == true || Configuration.ServiceMode ) && ( synchronized == false ) )
                {
                    bool online = Inet.IsOnline;

                    Log.Trace( "Schema is not synced with iNet. Connected=" + online );

                    // We don't bother getting the current state (which does a 'lock') until we know AllSynced is false.
                    ConsoleState state = Master.ConsoleService.CurrentState;

                    if ( state == ConsoleState.Ready
                    ||   state == ConsoleState.Discovering
                    ||   state == ConsoleState.Synchronization
                    ||   state == ConsoleState.SynchronizationError )
                    {
                        Master.ConsoleService.UpdateState( online ? ConsoleState.Synchronization : ConsoleState.SynchronizationError );
                    }
                }

                // Anytime an account number or activation changes, we need to do a re-discover and force another settings.
                // This is due to database being reset due to the account change, and the new database will no longer
                // have info regarding currenly attached cylinders.
                string curAccountNum = Configuration.Schema.AccountNum;
                if ( ( _wasAccountNum != curAccountNum ) || ( _wasActivated != activated ) )
                {
                    if ( _wasAccountNum != curAccountNum )
                        Log.Info( string.Format( "{0} detected changed Account. Old=\"{1}\", New=\"{2}\"", Name, _wasAccountNum, curAccountNum ) );

                    if (_wasActivated != activated )
                        Log.Info( string.Format( "{0} detected changed Activation. Old=\"{1}\", New=\"{2}\"", Name, _wasActivated, activated ) );

                    _wasAccountNum = curAccountNum;
                    _wasActivated = activated;

                    Log.Debug( string.Format( "{0} initiating Discovery (account/activation changed).", Name ) );

                    Master.ExecuterService.Discover();
                    _initialized = true;

                    InitialReadSettingsNeeded = ReadSettingsNeeded = ReadAllCards = true; // need to do a full readsettings

                    return; // Simply return. Yes, we need to perform a ReadSettings, but it'll happen the next time Run is called.
                }

                // Special handling for docking stations in manufacturing accounts...
                if ( Configuration.Schema.IsManufacturing )
                {
                    // If activated (not a Cal Station), and not online, then
                    // display error message saying that it needs to be online.
                    if ( Configuration.Schema.Activated && !Inet.IsOnline )
                    {
                        Master.ConsoleService.UpdateState( ConsoleState.MfgNotConnected );
                        return;
                    }

                    ConsoleState state = Master.ConsoleService.CurrentState;

                    if ( state == ConsoleState.MfgNotConnected )
                        Master.ConsoleService.UpdateState( ConsoleState.MfgConnected );

                    else if ( state == ConsoleState.MfgConnected )
                        Master.ConsoleService.UpdateState( ConsoleState.Ready );
                }

                // Until we find out that we've successfully connected to iNet
                // in order to obtain an account number, factory cylinder info, etc.,
                // don't do anything more than a one-time Discover.  The initial Discover
                // is also needed in order to jump start the ExecuterService's thread into
                // doing something. (It won't do anything on bootup as it's 'next action' is null.)
                if ( !synchronized )
                {
                    if ( !_initialized )
                    {
                        Log.Debug( string.Format( "{0} initiating initial Discovery (unsynchronized).", Name ) );

                        DockingStationEvent dsEvent = Master.ExecuterService.Discover();
                        Controller.LogDockingStation( dsEvent.DockingStation );
                        _initialized = true;

                        InitialReadSettingsNeeded = ReadSettingsNeeded = ReadAllCards = true; // need to do a full readsettings
                    }
                    return; // Simply return. Yes, we might need to perform a ReadSettings but, if needed, it'll happen the next time Run is called.
                }

                // Force a Discovery if we're just booting up.
                if ( !_initialized )
                {
                    Log.Debug( string.Format( "{0} initiating initial Discovery.", Name ) );
                    DockingStationEvent dsEvent = Master.ExecuterService.Discover();
                    Controller.LogDockingStation( dsEvent.DockingStation );
                    _initialized = true;
                    return;
                }

                // Check if instrument has been docked or undocked and force a Discovery if something has been docked or undocked.
                if ( !Master.Instance.SwitchService.InitialReadSettingsNeeded && CheckDockedStatus() )
                {
                    Log.Debug( string.Format( "{0} initiating Discovery.", Name ) );
                    Master.ExecuterService.Discover();
                    _initialized = true;
                    return;
                }

                // Check card readers for card insertions / removals, and for pressure
                // switch changes.  If any card or pressure switch changes detected,
                // then we need to force a Settings Read.
                ReadSettingsNeeded = CheckIgasPorts() || ReadSettingsNeeded;

                // When we first start up, we always want to perform a settings read operation,
                // and send the information to the server.
                if ( ReadSettingsNeeded )
                {
                    ReadSettings( ReadAllCards );
                    ReadSettingsNeeded = ReadAllCards = false;
                }

                _lastCaughtException = null; // no error encountered on this iteration of Run(), so clear the last caught error.
            }
            catch ( InstrumentSystemAlarmException systemalarmmex ) // SGF  Nov-23-2009  DSW-355  (DS2 v7.6)
            {
                Master.ConsoleService.UpdateState( ConsoleState.InstrumentSystemAlarm );
                //Suresh 06-FEB-2012 INS-2622
                Master.ExecuterService.ReportExceptionError( systemalarmmex ); //Suresh 15-SEPTEMBER-2011 INS-1593
            }
            catch ( HardwareConfigurationException hce )
            {
                // Set docked flag to false.  Otherwise, if user just reconfigures the hardware on the 
                // docking station without redocking the instrument, the IDS will think that it's already 
                // docked and won't want to do a discover.
                _wasDocked = false;
                Master.ConsoleService.UpdateState( ConsoleService.MapHardwareConfigError( hce ) );
            }
            catch ( Exception e )
            {
                Log.Error( Name, e );

				// If the menu is currently active, then we don't want to interrupt what the user is doing for an error
				// that will likely be hit again in a few seconds and can be displayed when the menu is not active.
				// Viewing the Cylinders menu screen could be helpful for troubleshooting smart card issues.
				// The first DeviceDriverException should be uploaded to iNet.
				if ( e is DeviceDriverException && Master.Instance.ConsoleService.CurrentState != ConsoleState.Menu )
				{
					// DeviceDriverExceptions that the SwitchService can encounter will be from checking smart card presence, 
					// pressure switch presence, or pressure switch state. 
					if ( ( (DeviceDriverException)e ).DeviceHardware == DeviceHardware.SmartCardPresence )
						Master.Instance.ConsoleService.UpdateState( ConsoleState.IGasError, ConsoleServiceResources.IGASERROR_SMARTCARDS );
					else
						Master.Instance.ConsoleService.UpdateState( ConsoleState.IGasError, ConsoleServiceResources.IGASERROR_PRESSURESWITCHES );

					// Give the ConsoleService time to display the error state.
					Thread.Sleep( 3000 );
				}

				// don't upload CommunicationAbortedException or InstrumentNotDockedException to iNet
				if ( e is CommunicationAbortedException || e is InstrumentNotDockedException )
				{
					Master.Instance.ConsoleService.UpdateState( ConsoleState.UndockedInstrument );
					Thread.Sleep( 3000 );
					_wasDocked = false;
					// A heartbeat is called to get the executer service to update the LCD screen (to go idle) 
					// in case the instrument remains undocked.
					Master.ExecuterService.HeartBeat();
				} 
				// Whenever we catch an exception, report it to iNet, but only if it's not
                // the same as the last error - to prevent a problem where we repeatedly get,
                // say, a device driver exception trying to check smart cards or pressure switches.
                // In that situation, we don't want to upload the error continuously to iNet.
                else if ( _lastCaughtException == null || _lastCaughtException.GetType() != e.GetType() )
                {
                    Master.ReporterService.ReportError( new DockingStationError( e, DockingStationErrorLevel.Warning ) );
                    Master.ExecuterService.HeartBeat(); //	Perform one heartbeat to force the error to upload asap.
                    _lastCaughtException = e;                    
                }
            }
		}

        public InstrumentController InstrumentController { get { return CreateInstrumentController(); } }

        /// <summary>
        /// Create an instance of an InstrumentController.  This is intended to
        /// be the only way to create an InstrumentController; all derived classes'
        /// constructors are intended to be hidden.
        /// </summary>
        /// <returns>Returns a derived class instance,
        /// the type of which is appropriate for this docking stations instrument
        /// type</returns>
        public static InstrumentController CreateInstrumentController()
		{
			
			if ( Configuration.DockingStation.Type == DeviceType.MX4 )
			{
				if ( Master.Instance.SwitchService.Instrument.Type == DeviceType.VPRO )
				{
					return new VPRO();
				}
				
				// DiscoveryOperation expects MX4 object to be returned for MX4 docking stations
				// when the docked instrument type is not yet known.
				return new MX4();
			}
			if ( Configuration.DockingStation.Type == DeviceType.MX6 ) return new MX6();
			if ( Configuration.DockingStation.Type == DeviceType.SC ) return new SC();
			if ( Configuration.DockingStation.Type == DeviceType.TX1 ) return new TX1();
			if ( Configuration.DockingStation.Type == DeviceType.GBPRO ) return new GBPRO();
			if ( Configuration.DockingStation.Type == DeviceType.GBPLS ) return new GBPLUS();

			throw new System.NotSupportedException( "\"" + Configuration.DockingStation.Type.ToString() + "\" is not a supported instrument." );
		}

		/// <summary>
		/// Creates an instance of an instrument controller with access to factory methods.  
		/// This should only be called after the docked instrument has been discovered and its type cached.
		/// </summary>
		/// <returns>Returns a factory instance, the type of which is appropriate for the docked instrument
		/// assuming it is supported by the docking station.</returns>
		public static IFactoryController CreateFactoryInstrumentController()
		{
			if ( Configuration.DockingStation.Type == DeviceType.MX4 )
			{
				if ( Master.Instance.SwitchService.Instrument.Type == DeviceType.VPRO )
				{
					return new FactoryVPRO();
				}

				return new FactoryMX4();
			}
			if ( Configuration.DockingStation.Type == DeviceType.MX6 ) return new FactoryMX6();
			if ( Configuration.DockingStation.Type == DeviceType.SC ) return new FactorySC();
			if ( Configuration.DockingStation.Type == DeviceType.TX1 ) return new FactoryTX1();
			if ( Configuration.DockingStation.Type == DeviceType.GBPRO ) return new FactoryGBPRO();
			if ( Configuration.DockingStation.Type == DeviceType.GBPLS ) return new FactoryGBPLUS();

			throw new System.NotSupportedException( "\"" + Configuration.DockingStation.Type.ToString() + "\" is not a supported instrument." );
		}

		/// <summary>
        /// Check card readers for card insertions / removals, and for pressure
        // switch changes.  If any card or pressure switch changes detected,
        // then we need to force a Settings Read.
        /// </summary>
        /// <returns>
        /// True if any changes detected, else false.
        /// </returns>
        private bool CheckIgasPorts()
        {
            bool somethingChanged = false;

            // Note: the device driver that read the igas hardware may fail and throw if
            // the CPU is under load, due to a low-level A2D timeout. (This can happen
            // if another thread, for example, is busy processing datalog or is uploading
            // large data to iNet.
            //
            // We therefore do not update the "_was" arrays until after we're done reading
            // the iGas hardware.  (The hardware is read by the below "Check" calls, and the
            // "_was" arrays are updated by the below "Handle" calls.)

            // Otherwise, we'd have problems with _was" arrays being partially updated before
            // the failure, and iNet not being notified of the iGas change because of the failure,
            // and then on next iteration of Run(), the DS would 'forget' that iNet was never
            // notified.

            // Determines which card readers have inserted cards and which have no cards.
            CheckSmartCardPresence();  // reads the hardware. will throw on device driver failure.

            // Determines which ports have pressure switches. Reads current pressure state of those that do.
            CheckPressureSwitches(); // reads the hardware. will throw on device driver failure.

            // We need to force a settings read if there have been any card insertions/removals since last check.
            if ( HandleSmartCardInsertionChanges() )  // updates the '_was' arrays, safely after we're done trying to read the hardware
                somethingChanged = true;

            // We need to force a settings read if any pressure changes have occurred since last check.
            if ( HandlePressureSwitchChanges() ) // updates the '_was' arrays, safely after we're done trying to read the hardware
                somethingChanged = true;

            return somethingChanged;
        }

        /// <summary>
        /// Determines which card readers have inserted cards and which have no cards.
        /// </summary>
        private void CheckSmartCardPresence()
        {
            // Reset these member booleans to false on every call.
            for ( int i = 0; i < _checkedCardIsPresent.Length; i++ )
                _checkedCardIsPresent[i] = false;

            for ( int i = 0; i < _checkedCardIsPresent.Length; i++ )
            {
                // Check if smart card has changed.
                // Save the state for setting later to avoid synchronization issues.
                _checkedCardIsPresent[i]  = SmartCardManager.IsCardPresent( i + 1 );
                if ( _wasCardPresent[i] != _checkedCardIsPresent[i] )
                {
                    Thread.Sleep( 500 );
                    // Try again.
                    _checkedCardIsPresent[i] = SmartCardManager.IsCardPresent( i + 1 );
                }
            }

        }

        private bool HandleSmartCardInsertionChanges()
        {
            bool readSettingsNeeded = false;

            for ( int i = 0; i < _changedSmartCards.Length; i++ )
            {
                _changedSmartCards[i] = false;
                if ( _wasCardPresent[i] != _checkedCardIsPresent[i] )
                {
                    Log.Debug( string.Format( "{0} detected {1} SmartCard on port {2}", this.Name, ( _checkedCardIsPresent[i] ? "inserted" : "removed" ), i+1 ) );

                    readSettingsNeeded = true;
                    _changedSmartCards[i] = true;
                    _wasCardPresent[i] = _checkedCardIsPresent[i]; // Set the state to the previously sampled value.
                }
                else
                    _changedSmartCards[i] = false;
            }
            return readSettingsNeeded;
        }

        /// <summary>
        /// Determines which ports have pressure switches and which do not.
        /// For those that do, it reads current pressure state (good/bad).
        /// </summary>
        private void CheckPressureSwitches()
        {
            // Reset these member booleans to false on every call.
            for ( int i = 0; i < _checkedSwitchIsPresent.Length; i++ )
                _checkedSwitchIsPresent[i] = _checkedIsPressureGood[i] = false;

            for ( int i = 0; i < _checkedSwitchIsPresent.Length; i++ )
            {
                _checkedSwitchIsPresent[i] = SmartCardManager.IsPressureSwitchPresent( i + 1 );

                if ( _wasSwitchPresent[i] != _checkedSwitchIsPresent[i] )
                {
                    Thread.Sleep( 500 );
                    // Try again.
                    _checkedSwitchIsPresent[i] = SmartCardManager.IsPressureSwitchPresent( i + 1 );
                }
                else if ( _checkedSwitchIsPresent[i] ) // If pressure switch is still present since last check, we need to then re-check the pressure.
                {
                    // Check the pressure.
                    // Save the state for setting later to avoid synchronization issues.
                    _checkedIsPressureGood[i] = SmartCardManager.CheckPressureSwitch( i + 1 );
                    if ( _wasPressureGood[i] != _checkedIsPressureGood[i] )
                    {
                        Thread.Sleep( 500 );
                        // Try again.
                        _checkedIsPressureGood[i] = SmartCardManager.CheckPressureSwitch( i + 1 );
                    }
                }
            }
        }

        private bool HandlePressureSwitchChanges()
        {
            bool readSettingsNeeded = false;

            for ( int i = 0; i < _checkedSwitchIsPresent.Length; i++ )
            {
                if ( _wasSwitchPresent[i] != _checkedSwitchIsPresent[i] )
                {
                    if ( _wasSwitchPresent[i] != _checkedSwitchIsPresent[i] )
                    {
                        Log.Debug( string.Format( "{0} detected {1} pressure switch on port {2}", this.Name, ( _checkedSwitchIsPresent[i] ? "inserted" : "removed" ), i+1 ) );
                        readSettingsNeeded = true;
                        _wasSwitchPresent[i] = _checkedSwitchIsPresent[i]; // Set the state to the previously sampled value.

                        // If we're detecting switch being inserted, then we don't need to check if pressure changed.
                        // But we need to initialize the 'wasPressureGood' for when if/when it later does change
                        if ( _checkedSwitchIsPresent[i] )
                        {
                            _wasPressureGood[i] = _checkedIsPressureGood[i];
                            Log.Debug( string.Format( "{0} detected {1} pressure on port {2}", this.Name, ( _checkedIsPressureGood[i] ? "Full" : "Low/Empty" ), i+1 ) );
                        }
                    }
                }
                else if ( _checkedSwitchIsPresent[i] ) // If pressure switch is still present since last check, we need to then re-check the pressure.
                {
                    if ( _wasPressureGood[i] != _checkedIsPressureGood[i] )
                    {
                        Log.Debug( string.Format( "{0} detected pressure change ({1}) on port {2}", this.Name, ( _checkedIsPressureGood[i] ? "Full" : "Low/Empty" ), i+1 ) );
                        readSettingsNeeded = true;
                        _wasPressureGood[i] = _checkedIsPressureGood[i]; // Set the state to the previously sampled value.
                    }
                }
            }
            return readSettingsNeeded;
        }

        /// <summary>
        /// Returns true if instrument is docked and serial number is non-empty.
        /// </summary>
        /// <remarks>
        /// I think maybe the intent of the serial number check is to not consider the
        /// instrument as fully docked until after we've read its settings?? JMP - 8/2013.
        /// </remarks>
        /// <returns></returns>
        public bool IsDocked()
        {
            return Controller.IsDocked() && Instrument.SerialNumber.Length > 0;
        }

        /// <summary>
        /// Checks if instrument has been docked or undocked.
        /// Also saves the state for setting later to avoid synchronization issues.
        /// </summary>
        private bool CheckDockedStatus()
        {
			// prevent discovering of instruments if the docking station should be returned to ISC
			if ( Master.Instance.ConsoleService.CurrentState == ConsoleState.ReturnDockingStation )
				return false;

			// prevent discovering of instruments if settings read needed
			if ( InitialReadSettingsNeeded )
				return false;

            bool currentSample = Controller.IsDocked();

            if ( _wasDocked != currentSample )
            {
                Thread.Sleep( 500 );

                currentSample = Controller.IsDocked();
                if ( _wasDocked != currentSample )
                {
                    Log.Debug( string.Format( "{0} detected {1} instrument.", this.Name, ( currentSample ? "docked" : "undocked" ) ) );

                    // Set the state the previously sampled value.
                    _wasDocked = currentSample;

                    return true;
                }
            }
            return false;
        }

        public void DockInstrument( Instrument dockedInstrument )
        {
            InstrumentUpgradeError = false;
            BadGasHookup = false;
            BadPumpTubingDetectedDuringBump = false;
            BadPumpTubingDetectedDuringCal = false;
			IsInstrumentReplaced = false;

            if ( dockedInstrument != null ) // docked
			{
				Instrument = (Instrument)dockedInstrument.Clone();

				DockedTime = DateTime.UtcNow;
				DockProcessing = true;

				Log.TimingBegin( "DOCK TO GREEN" );
			}
			else // undocked
			{
				Instrument = new Instrument();

				InitialInstrumentSettingsNeeded = true;
				NextUtcCalibrationDate = NextUtcBumpDate = null;
                IsInstrumentInSystemAlarm = false;

				DockedTime = DateTime.SpecifyKind( DomainModelConstant.NullDateTime, DateTimeKind.Utc );
				DockProcessing = false;
			}
        }

        public void UndockInstrument()
        {
            DockInstrument( null );
        }

        private void ReadSettings( bool readAllCards )
        {
            Log.Debug( this.Name + " initiating a SettingsRead" );

            SettingsReadAction settingsReadAction = new SettingsReadAction();

            // If readAllCards is true, then explicitly set ChangedSmartCards to null
            // in order to force a read of ALL smart cards, not just 'changed' ones.
            settingsReadAction.ChangedSmartCards = readAllCards ? null : _changedSmartCards;

            Master.ExecuterService.ExecuteNow(settingsReadAction, true);

            InitialReadSettingsNeeded = false;

            Master.ExecuterService.HeartBeat();
        }

        #endregion Methods

    }  // end-class

    public interface ISwitchService : IService
    {
        bool BadGasHookup { get; set; }

        string BadGasHookUpCylinderPartNumber { get; set; }

        bool BadPumpTubingDetectedDuringCal { get; set; }

        bool BadPumpTubingDetectedDuringBump { get; set; }

        DateTime DockedTime { get; set; }

        bool DockProcessing { get; set; }

        void UndockInstrument();

        void DockInstrument(Instrument dockedInstrument);

        bool InitialInstrumentSettingsNeeded { get; set; }

        Instrument Instrument { get; set; }

        bool InitialReadSettingsNeeded { get;  }

        bool InstrumentUpgradeError { get; set; }

        bool IsDocked();

        bool IsInstrumentInSystemAlarm { get; set; }

        bool IsInstrumentReplaced { get; set; }

        DateTime? NextUtcBumpDate { get; set; }

        DateTime? NextUtcCalibrationDate { get; set; }

        InstrumentController InstrumentController { get; }
    }
}  // end-namespace
