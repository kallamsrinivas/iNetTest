using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.Instrument.Driver;
using ISC.Instrument.TypeDefinition;
using ISC.WinCE;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Instruments
{
    using GasType = ISC.iNet.DS.DomainModel.GasType;
    // putting these here avoids compiler's confusion between DomainModel and Instrument.Driver classes.
    using Instrument = ISC.iNet.DS.DomainModel.Instrument;
    using MeasurementType = ISC.iNet.DS.DomainModel.MeasurementType;
    using SensorType = ISC.iNet.DS.DomainModel.SensorType;
	using AlarmEvent = ISC.iNet.DS.DomainModel.AlarmEvent;


    /// <summary>
    /// Provides functionality to communicate with instruments.
    /// </summary>
    /// <remarks>
    /// This base class and all of its subclasses purposely have internal constructors so that
    /// they cannot be explicity instantiate.
    /// To create an Instance of an InstrumentController, call InstrumentController.CreateInstance().
    /// That method will construct and return instance that's appropriate to the docking station's type.
    /// </remarks>
    public abstract class InstrumentController : IDisposable, IModbusTracer
    {
        #region Fields

        private ThreadPriority _originalThreadPriority = ThreadPriority.Normal;

        /// <summary>
        /// Flags for Initialize().
        /// Multiples can be passed by bit-ORing them together.
        /// </summary>
        [Flags] public enum Mode
        {
            /// <summary>Default mode is to Ping the instrument awake, to not use batch mode, and to not prepare for gas operation.</summary>
            Default = 0x00,
            /// <summary>Tell Initialize() to NOT ping awake the instrument. (The default is that Initialize() will ping the instrument.)</summary>
            NoPing = 0x01,
            /// <summary>Use 'batch mode' when communicating with modbus instrument.(this mode is ignored by legacy serial instruments).</summary>
            Batch = 0x02,
            /// <summary>Indicates if instrument is going to be used for a gas operation.
            /// (specifying this mode may result in instrument pump being turned on and/or waiting for a PID lamp to warm up, etc.)</summary>
            //Gas   = 0x04,
            /// <summary>If specified, there's no reason to check the lid, or look for presence of pump, or switch the lid solenoid, etc.
            /// Used by CheckInstrumentChargingOperation.</summary>
            NoLid = 0x08
        }

        /// <summary>
        /// The pseudo-position of the battery.
        /// </summary>
        protected const int BATTERY_POSITION = 20;	

        protected Mode _mode;

        protected readonly string _dateTimeFormat;

        private InstrumentDriver _driver;

        // used to optimize memory usage during datalog downloading...
        private string _lastDatalogLocation;
        private string _lastDatalogUser;

        private bool _isDisposed; // don't make protected. Class should have its own disposed flag.
        private bool _isInstrumentInSystemAlarm;

        #endregion Fields

        #region Construction / Destruction

        protected internal InstrumentController()
        {

        }

        /// <summary>
        /// Constructor for this abstract base class.  Deriving classes'
        /// constructors should call this constructor.
        /// </summary>
        protected internal InstrumentController( InstrumentDriver instrumentDriver )
        {
            if ( instrumentDriver == null )
            {
                Log.Debug( "null InstrumentDriver in ModbusInstrumentController" );
                throw new ArgumentNullException( "ModbusInstrumentController(instrumentDriver)" );
            }

            Driver = instrumentDriver;

            _mode = Mode.Default;  // may be reset by overridden Initialize methods

            // Set date/time format based on IDS's language setting.
            _dateTimeFormat = ( Configuration.DockingStation.Language.Code == Language.English ) ? "HH:mm MM/dd/yyyy" : "HH:mm dd/MM/yyyy";

            Driver.setCommunicationModuleType( CommunicationModuleTypes.DSX );

            // Dont' enable modbus tracing unless it's been enabled.  By leaving it disabled,
            // the driver won't waste time formatting up messages that will never be output.
            Driver.SetModbusTracer( this ); // Configure driver to call our WriteDebug methods.
            Driver.DateTimeProvider = new DateTimeProvider( Configuration.DockingStation.TimeZoneInfo ); // Configure driver to call us for datetimes so that SystemTime class is used.
            Driver.setSerialPort( Controller.INSTRUMENT_COM_PORT );

            // 11/14/07 - don't try to set the baud rate of a GBPlus - it is fixed at 9600.
            // 1/26/2016 - no need to set the baud rate of SafeCore - it is fixed at 115200.
            if ( Configuration.DockingStation.Type != DeviceType.GBPLS && Configuration.DockingStation.Type != DeviceType.SC )
                Driver.setPortSpeed( 115200 );

            Driver.SetAbortRequester( new AbortRequest( IsNotDocked ) );

            _lastDatalogLocation = _lastDatalogUser = string.Empty; // used to optimize memory usage during datalog downloading...

            // When communicating with instrument, make sure we do so at the highest priority
            // to prevent communication problems.  Before raising the thread's priority, we
            // hold onto it's current priority so that we can set it back in the dispose()
            // method.
            _originalThreadPriority = Thread.CurrentThread.Priority;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }


        /// <summary>
        /// Open the connection needed to communicate with the instrument and
        /// ping ( wake up ) the instrument.
        /// <para>This paramterless implemenation defaults to non-Batch mode.</para>
        /// </summary>
        /// <returns>Docking station event</returns>
        public virtual void Initialize()
        {
            Initialize( Mode.Default );
        }

        /// <summary>
        /// Open the connection needed to communicate with the instrument and
        /// ping ( wake up ) the instrument.
        /// </summary>
        /// <param name="mode"></param>
        public virtual void Initialize( Mode mode )
        {
            Log.Debug( this.GetType() + ".Initialize(Mode = " + mode + ")" );

            _mode = mode;

            // todo: Add override of Initialize in GBPRO class to handle baud rate like we did with MX6?
            // The constructor likely raised it to 115200 under assumption that batch mode will be used 
            // since that's the usual mode. Here, we lower it back down to 9600 for non-batch mode, for these instrument types.
            // Non-batch mode is intended to be used when just reading a few registers.  It's not worth
            // the extra time needed to negotiate a higher baud rate just to read a few registers.
            if ( ( ( mode & Mode.Batch ) == 0 ) // batch mode not specified?
            && ( Configuration.DockingStation.Type == DeviceType.GBPRO ) )
                Driver.setPortSpeed( 9600 );

            if ( ( mode & Mode.NoPing ) != 0 ) // If 'dont ping' is specified, then we can just return.
                return;

			Ping( ( mode & Mode.Batch ) != 0 );
        }

        /// <summary>
        /// Finalizing Constructor.
        /// Calls virtual Dispose method as part of standard IDisposable design pattern.
        /// </summary>
        ~InstrumentController()
        {
            Dispose( false );
        }

        /// <summary>
        /// Implementation of IDisposable. Call the virtual Dispose method. Suppress Finalization.
        /// </summary>
        public virtual void Dispose()
        {
            Thread.CurrentThread.Priority = _originalThreadPriority;

            GC.SuppressFinalize( true );
            Dispose( true );
        }

        /// <summary>
        /// Called by constructor and public Dispose in order
        /// to implement standard IDisposable design pattern.
        /// 
        /// This implementation ensures that the driver's disconnnect
        /// method is called an then that the driver itself is Disposed.
        /// </summary>
        /// <param name="disposing">
        /// true if being called explicitly by parameterless Dispose.
        /// false if being called by finalizing descructor.
        /// </param>
        protected void Dispose( bool disposing )
        {
            if ( !_isDisposed ) // only dispose once!
            {
                if ( disposing )
                {
                    // FREE MANAGED RESOURCES HERE.

                    Driver.Dispose();
                }
                // FREE UNMANAGED RESOURCES HERE.
            }
            this._isDisposed = true;
        }

        #endregion  Construction / Destruction

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// THE ONLY REASON THIS IS PUBLIC INSTEAD OF PROTECTED IS TO GRANT ACCESS TO DS.TESTER 
        /// UTILITY WHICH NEEDS TO DO LOW-LEVEL GETREGISTER AND SETREGISTER CALLS.
        /// </remarks>
        public InstrumentDriver Driver
        {
            get { return _driver; }
            private set { _driver = value; }
        }

        /// <summary>
        /// Indicates whether or not instrument type supports datalogging.
        /// </summary>
        public virtual bool HasDataLoggingFeature { get { return Driver.Definition.HasDataLoggingFeature; } }
         
        /// <summary>
        /// Indicates whether or not instrument type supports TWA calculation.
        /// </summary>
        public virtual bool HasTwaFeature { get { return Driver.Definition.HasTwaFeature; } }

		public virtual bool HasOomWarningIntervalFeature { get { return Driver.Definition.HasOomWarningIntervalConfigFeature; } }

		public virtual bool HasDockIntervalFeature { get { return Driver.Definition.HasDockIntervalConfigFeature; } }

        public virtual bool HasMaintenanceIntervalFeature { get { return Driver.Definition.HasMaintenanceIndicatorIntervalConfigFeature; } }

        /// <summary>
        /// Indicates whether or not instrument type supports calibration interval.
        /// </summary>
        public virtual bool HasCalibrationIntervalFeature { get { return Driver.Definition.HasCalIntervalConfigFeature; } }

        public virtual bool HasBumpIntervalFeature { get { return Driver.Definition.HasBumpIntervalConfigFeature; } }

        public virtual bool HasBumpThresholdFeature { get { return Driver.Definition.HasBumpThresholdConfigFeature; } }

        public virtual bool HasBumpTimeoutFeature { get { return Driver.Definition.HasBumpTimeoutConfigFeature; } }

        public virtual bool HasSmartBatteryFeature { get {  return Driver.Definition.HasSmartBatteryFeature; } }

        public virtual bool HasBatteryInformation { get { return Driver.Definition.HasBatteryInformation; } }

        /// <summary>
        /// Indicates whether or not backlight timeout interval can be configured for the instrument.
        /// </summary>
        /// <returns></returns>
        public virtual bool HasBacklightTimeoutConfigFeature { get { return Driver.Definition.HasBacklightTimeoutConfigFeature; } }

        /// <summary>
	    /// Get increment allowed for recording interval.
        /// Zero should be returned if recording
        /// interval cannot be changed.
	    /// </summary>
	    /// <returns></returns>
	    public virtual int RecordingIntervalIncrement {  get { return Driver.Definition.RecordingIntervalIncrement; } }

        public virtual bool HasMagneticFieldDurationFeature { get { return Driver.Definition.HasMagneticFieldDurationFeature; } }
        public virtual int GetMagneticFieldDuration() { return Driver.getMagneticFieldDuration(); }
        public virtual void SetMagneticFieldDuration( int duration ) { Driver.setMagneticFieldDuration( duration ); }

		public virtual bool HasCompanyNameFeature { get { return Driver.Definition.MaxCompanyLength > 0; } }
		public virtual bool HasCompanyMessageFeature { get { return Driver.Definition.MaxCompanyMessageLines > 0; } }

		public virtual bool HasAlarmActionMessagesFeature { get { return Driver.Definition.MaxAlarmActionMessages > 0; } }

        public virtual bool IsInstrumentInSystemAlarm { get { return _isInstrumentInSystemAlarm; } }

        #endregion Properties

        #region Methods

        public virtual void Ping( bool batchConnect )
        {
			Log.Warning( "PINGING INSTRUMENT..." );
			try
			{
				Driver.connect( batchConnect );
			}
			catch ( CommunicationAbortedException cae )
			{
				throw new InstrumentNotDockedException( cae );
			}
			catch ( Exception e )
			{
				Log.Error( "PING: Failure establishing communication with instrument", e );
				throw new InstrumentPingFailedException( e );
			}
        }

        /// <summary>
        /// Retrieve the docked instrument's information.
        /// </summary>
        /// <returns>A populated instrument.</returns>
        public Instrument DiscoverDockedInstrument( bool warmup )
        {
            // Determine if there is an instrument to read.
            if ( ! Controller.IsDocked() )
                return null;

            // Make the instrument.
            Instrument instrument = new Instrument();

            //Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593
            CheckInstrumentInSystemAlarm(); // will throw InstrumentSystemAlarmException if instrument has a system error

			if ( warmup )
				Driver.turnOnSensors( true, false );

            // Get the Instrument serial number.
            instrument.SerialNumber = GetSerialNumber();
            Log.Debug( "SerialNumber=" + instrument.SerialNumber );

            // Retrieve and assign the instrument type.
            instrument.Type = GetInstrumentType();
            Log.Debug( "Type=" + instrument.Type.ToString() );

            instrument.Subtype = GetInstrumentSubtype();
            Log.Debug("Subtype=" + instrument.Subtype.ToString());

            // Make sure instrument matches the type of IDS it's docked in.
            // We need to watch out for things like trying to dock a GasBadge Plus on a GBPRO IDS.)
            // A Ventis Pro Series instrument on an MX4 docking station is allowed. 
            // We do not allow Ventis instrument on Ventis-LS Docking station INS-6434
            if (!Configuration.DockingStation.IsDockedInstrumentSupported(instrument))
            {
                return instrument;
            }

            instrument.PartNumber = GetPartNumber();
            Log.Debug( "PartNumber=" + instrument.PartNumber );

            instrument.JobNumber = GetJobNumber();
            Log.Debug( "JobNumber=" + instrument.JobNumber );

            // Instrument software version.
            instrument.SoftwareVersion = GetSoftwareVersion();
            Log.Debug( "SoftwareVersion=" + instrument.SoftwareVersion );

			instrument.BootloaderVersion = GetBootloaderVersion();
			Log.Debug( "BootloaderVersion=" + instrument.BootloaderVersion );

            instrument.HardwareVersion = GetHardwareVersion();
            if (instrument.HardwareVersion != string.Empty)
                Log.Debug("HardwareVersion=" + instrument.HardwareVersion);

            instrument.Language.Code = GetLanguage();
            Log.Debug( "Language.Code=" + instrument.Language.Code );

            instrument.SetupTech = GetSetupTech(); // Instrument setup - technician initials.
            Log.Debug("SetupTech=" + instrument.SetupTech);

            instrument.SetupDate = GetSetupDate();  // Instrument setup date.
            Log.Debug("SetupDate=" + Log.DateTimeToString(instrument.SetupDate) );

            instrument.SetupVersion = GetSetupVersion();  // Configuration Software Version
            Log.Debug("SetupVersion=" + instrument.SetupVersion);

            instrument.CountryOfOrigin = GetCountryOfOriginCode();
            Log.Debug("CountryOfOriginCode=" + instrument.CountryOfOrigin);

            // Instrument security or access code setting.
            instrument.AccessCode = GetAccessCode();
#if DEBUG // don't show access codes in release builds.
            Log.Debug("AccessCode=" + instrument.AccessCode);
#else
            Log.Debug("AccessCode=" + string.Empty.PadRight(instrument.AccessCode.Length, '*'));
#endif

            // If instrument doesnt support totalruntime, we'll get back ts.MinValue.
            TimeSpan runTime = GetTotalRunTime();
            instrument.OperationMinutes = (runTime == TimeSpan.MinValue) ? DomainModelConstant.NullInt : Convert.ToInt32(runTime.TotalMinutes);
            Log.Debug("OperationMinutes=" + instrument.OperationMinutes);

            // Retrieve instrument's current backlight setting.
            instrument.Backlight = GetBacklightSetting();
            Log.Debug("Backlight=" + instrument.Backlight.ToString());

            //Suresh 03-October-2011 INS-2277
            //Retrieve instrument's backlight timout
            if (HasBacklightTimeoutConfigFeature)
            {
                instrument.BacklightTimeout = GetBacklightTimeout();
                Log.Debug("BacklightTimeout=" + instrument.BacklightTimeout);
            }

            // Read the time as stored in the instrument (NOTE: this should not be done for GBPlus instruments)
            if (instrument.Type != DeviceType.GBPLS)
            {
                DateTime instrumentTime = GetTime();
                Log.Debug("Time=" + Log.DateTimeToString(instrumentTime));
            }

            // Recording interval setting (in seconds) for datalog
            if (HasDataLoggingFeature)
            {
                instrument.RecordingInterval = GetRecordingInterval();
                Log.Debug("RecordingInterval=" + instrument.RecordingInterval);
            }

            // TWA Time Base setting
            if (HasTwaFeature)
            {
                instrument.TWATimeBase = GetTwaTimeBase();
                Log.Debug("TWATimeBase=" + instrument.TWATimeBase);
            }

			// Out-of-Motion Warning Interval setting (in seconds)
			if ( HasOomWarningIntervalFeature )
			{
				instrument.OomWarningInterval = GetOomWarningInterval();
				Log.Debug( "OomWarningInterval=" + instrument.OomWarningInterval );
			}

			// Dock Interval setting (in days)
			if ( HasDockIntervalFeature )
			{
				instrument.DockInterval = GetDockInterval();
				Log.Debug( "DockInterval=" + instrument.DockInterval );
			}

            // Maintenance Interval setting (in days)
            if ( HasMaintenanceIntervalFeature )
            {
                instrument.MaintenanceInterval = GetMaintenanceInterval();
                Log.Debug( "MaintenanceInterval=" + instrument.MaintenanceInterval );
            }

            // Calibration Interval setting (in days)
            if ( HasCalibrationIntervalFeature )
            {
                instrument.CalibrationInterval = GetCalibrationInterval();
                Log.Debug( "CalibrationInterval=" + instrument.CalibrationInterval );
            }

            // Bump Interval setting (in days)
            if ( HasBumpIntervalFeature )
            {
                instrument.BumpInterval = GetBumpInterval();
                Log.Debug( "BumpInterval=" + instrument.BumpInterval );
            }
			
            // Bump Threshold setting
            if (HasBumpThresholdFeature)
            {
                instrument.BumpThreshold = GetBumpThreshold();
                Log.Debug( "BumpThreshold=" + instrument.BumpThreshold );
            }

            // Bump Timeout setting
            if (HasBumpTimeoutFeature)
            {
                instrument.BumpTimeout = GetBumpTimeout(); 
                Log.Debug("BumpTimeout=" + instrument.BumpTimeout);
            }

            if (HasMagneticFieldDurationFeature)
            {
                instrument.MagneticFieldDuration = GetMagneticFieldDuration();
                Log.Debug("MagneticFieldDuration=" + instrument.MagneticFieldDuration);
            }

            // Needs to be called BEFORE readsensor() so that readsensor has access
            // to the instrument's response factors.
            instrument.FavoritePidFactors = GetFavoritePidFactors();
            instrument.CustomPidFactors = GetCustomPidFactors();

            // After getting the sensor capacity - get the information related to each installed sensor.
            int sensors = GetSensorCapacity();
            
            InstalledComponent installedComponent = null;

            SensorPosition[] sensorPositions = GetSensorPositions();

            //Suresh 05-JAN-2012 INS-2564
            instrument.SensorsInErrorMode.Clear();

            foreach ( SensorPosition sensorPosition in sensorPositions )
            {		
                installedComponent = null;

	            if ( sensorPosition.Mode == SensorMode.Uninstalled )
                    continue;

                if ( sensorPosition.Mode == SensorMode.Error )
                {
                    installedComponent = new InstalledComponent();
                    installedComponent.Component = new Sensor();
                    installedComponent.Position = sensorPosition.Position;
                    instrument.SensorsInErrorMode.Add(installedComponent);
                    continue;
                }
                else
                    // Add sensor information for each installed sensor.
                    installedComponent = DiscoverSensor( sensorPosition );

                // Add the component just created to the instrument components collection.
                if ( installedComponent != null )
                    instrument.InstalledComponents.Add( installedComponent );
            }

            // Get the instrument's battery.
            installedComponent = DiscoverBattery();

            // If it exists, add it.
            if ( installedComponent != null )
                instrument.InstalledComponents.Add( installedComponent );

            // See if instrument has an accessory pump that's currently attached.
            instrument.AccessoryPump = AccessoryPump;
            Log.Debug("AccessoryPump=" + instrument.AccessoryPump.ToString());

            // Get the instrument's options.
            instrument.Options = GetOptions();

            // Get the instrument's users.
            instrument.Users = GetUsers();
            Log.Debug(string.Format("Users ({0}):", instrument.Users.Count));
            for (int i = 0; i < instrument.Users.Count; i++)
                Log.Debug(string.Format("    {0}", instrument.Users[i]));

            if (instrument.Type == DeviceType.MX6 || instrument.Type == DeviceType.SC)
            {
                instrument.ActiveUser = GetActiveUser();
                Log.Debug(string.Format("ActiveUser={0}", instrument.ActiveUser));
            }

            // Get the instrument's sites.
            instrument.Sites = GetSites();
            Log.Debug(string.Format("Sites ({0}):", instrument.Sites.Count));
            for (int i = 0; i < instrument.Sites.Count; i++)
                Log.Debug(string.Format("    {0}", instrument.Sites[i]));

            if (instrument.Type == DeviceType.MX6 || instrument.Type == DeviceType.SC)
            {
                instrument.ActiveSite = GetActiveSite();
                Log.Debug(string.Format("ActiveSite={0}", instrument.ActiveSite));
            }

			if ( HasCompanyNameFeature )
			{
				instrument.CompanyName = GetCompanyName();
				Log.Debug( string.Format( "CompanyName={0}",instrument.CompanyName ) );
			}

			if ( HasCompanyMessageFeature )
			{
				instrument.CompanyMessage = GetCompanyMessage();
				Log.Debug( string.Format( "CompanyMessage={0}", instrument.CompanyMessage ) );
			}

			instrument.AlarmActionMessages = GetAlarmActionMessages();

			// instrument level wireless settings supported by Whisper instruments (not Ventis LS)
            if ( Driver.Definition.HasWirelessFeature && ( instrument.Type == DeviceType.SC || instrument.Type == DeviceType.VPRO ) )
            {
                instrument.WirelessPeerLostThreshold = Driver.getWirelessPeerLostThreshold();
                Log.Debug( string.Format( "WirelessPeerLostThreshold={0}", instrument.WirelessPeerLostThreshold ) );

                instrument.WirelessNetworkLostThreshold = Driver.getWirelessNetworkLostThreshold();
                Log.Debug( string.Format( "WirelessNetworkLostThreshold={0}", instrument.WirelessNetworkLostThreshold ) );
                
                instrument.WirelessReadingsDeadband = Driver.getWirelessReadingsDeadband();
                Log.Debug( string.Format( "WirelessReadingsDeadband={0}", instrument.WirelessReadingsDeadband ) );

                if (Driver.Definition.HasWirelessNetworkDisconnectDelayConfigFeature)
                {
                    instrument.WirelessNetworkDisconnectDelay = Driver.getWirelessNetworkDisconnectDelay();
                    Log.Debug(string.Format("WirelessNetworkDisconnectDelay={0}", instrument.WirelessNetworkDisconnectDelay));
                }

                if (Driver.Definition.HasWirelessAlarmMaskFeature)
                {
                    //Read only
                    instrument.WirelessAlarmMask = GetWirelessAlarmMask();
                    Log.Debug( string.Format( "WirelessAlarmMask={0}", instrument.WirelessAlarmMask ) );
                }

                if (instrument.Type == DeviceType.VPRO)
                {
                    //Add activation data as readonly instrument properties
                    instrument.WirelessFeatureActivated = Driver.isWirelessFeatureEnabled();
                    Log.Debug( string.Format( "WirelessFeatureEnabled={0}", instrument.WirelessFeatureActivated ) );

                    instrument.INetNowFeatureActivated = Driver.isiNetNowFeatureEnabled();
                    Log.Debug( string.Format( "INetNowFeatureEnabled={0}", instrument.INetNowFeatureActivated ) );
                }
            }

            if (Driver.Definition.HasBluetoothFeature)
            {
                instrument.BluetoothMacAddress = Driver.getBluetoothMacAddress();
                Log.Debug( string.Format( "BluetoothMacAddress={0}", instrument.BluetoothMacAddress ) );

                instrument.BluetoothSoftwareVersion = Driver.getBluetoothSoftwareVersion();
                Log.Debug( string.Format( "BluetoothMacAddress={0}", instrument.BluetoothSoftwareVersion ) );

                instrument.BluetoothFeatureActivated = Driver.isBluetoothFeatureEnabled();
                Log.Debug( string.Format( "BluetoothFeatureActivated={0}", instrument.BluetoothFeatureActivated ) );

                instrument.LoneWorkerOkMessageInterval = Driver.getBluetoothLoneWorkerOkMessageInterval();
                Log.Debug( string.Format( "LoneWorkerOkMessageInterval={0}", instrument.LoneWorkerOkMessageInterval ) );
            }

            if (Driver.Definition.HasGpsFeature)
            {
                instrument.GpsReadingInterval = Driver.getGpsReadingInterval();
                Log.Debug( string.Format( "GpsReadingInterval={0}", instrument.GpsReadingInterval ) );
            }

            instrument.WirelessModule = DiscoverWirelessModule( instrument.Type );

			if ( Driver.Definition.HasBaseInfoLogFeature ) // it's not really necessary to call this, as GetBaseUnits will merely return empty array for non-supported instrument types.
			{
				instrument.BaseUnits = GetBaseUnits();
				Log.Debug( string.Format( "Base Units ({0}):", instrument.BaseUnits.Count ) );
				foreach ( BaseUnit baseUnit in instrument.BaseUnits )
					Log.Debug( string.Format( "    {0} {1}", baseUnit.SerialNumber, Log.DateTimeToString( baseUnit.InstallTime ) ) );
			}

            return instrument;
        }

        /// <summary>
        /// Get the instrument's battery.
        /// </summary>
        /// <returns>A battery with serial number and code filled, null if no battery.</returns>
        public InstalledComponent DiscoverBattery()
        {
            if (HasSmartBatteryFeature)
            {
                string serialNumber = GetBatterySerialNumber();
                string batteryCode = GetBatteryCode();

                // If there is no battery, return null.
                if (serialNumber == string.Empty || batteryCode == string.Empty)
                {
                    Log.Error(string.Format("NO INSTALLED BATTERY PACK FOUND. (SERIAL=\"{0}\", CODE=\"{1}\")", serialNumber, batteryCode));
                    return null;
                }

                // Make the battery.
                Battery battery = new Battery();

                // Get the battery serial number.
                battery.Uid = serialNumber + '#' + batteryCode;

                // Get the battery code.
                // battery.Type.Code = batteryCode;

                // Make the installed component to hold the battery.
                InstalledComponent installedComponent = new InstalledComponent();
                installedComponent.Position = BATTERY_POSITION;
                installedComponent.Component = battery;

                // Get the other information about the battery..

                battery.PartNumber = GetBatteryPackPartNumber();
                battery.ManufacturerCode = "ISC";
                battery.SetupDate = GetBatterySetupDate();
                battery.SetupTech = GetBatterySetupTech();
                battery.SoftwareVersion = GetBatterySoftwareVersion();
                battery.OperationMinutes = Convert.ToInt32( GetBatteryRunTime().TotalMinutes );

                Log.Debug("Battery.Uid=" + battery.Uid);
                Log.Debug("Battery.Type=" + battery.Type);
                Log.Debug("Battery.PartNumber=" + battery.PartNumber);
                Log.Debug("Battery.Manufacturer=" + battery.ManufacturerCode);
                Log.Debug("Battery.SetupDate=" + ( battery.SetupDate == DomainModelConstant.NullDateTime ? "none" : Log.DateTimeToString(battery.SetupDate) ) );
                Log.Debug("Battery.SetupTech=\"" + battery.SetupTech + "\"");
                Log.Debug("Battery.SoftwareVersion=\"" + battery.SoftwareVersion + "\"");
                Log.Debug("Battery.OperationMinutes=" + battery.OperationMinutes);

                return installedComponent;
            }
            else // SGF  Feb-12-2009  dev DSZ-795 & DSW-215 ("Add battery types for MX4 instrument")
            {
                // Not a smart battery.  Get what you can
                if ( HasBatteryInformation )
                {
                    string batteryCode = GetBatteryCode();

                    // If there is no battery, return null.
                    if (batteryCode == string.Empty)
                    {
                        Log.Error(string.Format("NO INSTALLED BATTERY PACK FOUND. (CODE=\"{0}\")", batteryCode));
                        return null;
                    }

                    // Create the battery object.
                    Battery battery = new Battery();

                    // Use instrument serial number for the battery serial number
                    battery.Uid = GetSerialNumber() + '#' + batteryCode;

                    // Get the battery code.
                    battery.Type.Code = batteryCode;

                    // Make the installed component to hold the battery.
                    InstalledComponent installedComponent = new InstalledComponent();
                    installedComponent.Position = BATTERY_POSITION;
                    installedComponent.Component = battery;

                    // Provide default values for other battery properties
                    battery.OperationMinutes = DomainModelConstant.NullInt;
                    battery.PartNumber = string.Empty;
                    battery.ManufacturerCode = string.Empty;
                    battery.SetupDate = DomainModelConstant.NullDateTime;
                    battery.SetupTech = string.Empty;
                    battery.SoftwareVersion = string.Empty;

                    Log.Debug("Battery.Uid=" + battery.Uid);
                    Log.Debug("Battery.Type=" + battery.Type);
                    Log.Debug("Battery.PartNumber=" + battery.PartNumber);

                    return installedComponent;
                }
            }

            return null; // If battery doesn't meet any above criteria, send back null
        }

	    /// <summary>
	    /// Discover Wireless module based on the device Type
	    /// </summary>
	    /// <param name="deviceType">Device Type</param>
	    /// <returns>The Wirless Module</returns>      
		public WirelessModule DiscoverWirelessModule( DeviceType deviceType )
		{
			if ( !Driver.Definition.HasWirelessFeature )
			{
				return null;
			}

			string mac = Driver.getWirelessMacAddress();
			if ( mac == string.Empty )
			{
				// instrument type supports a wireless module, but one is not currently installed
				return null;
			}
			Log.Debug( "WirelessModule:" );
            Log.Debug( string.Format( "    MacAddress={0}", mac ) );

            string status = Driver.getWirelessStatus().ToString();
			Log.Debug( string.Format( "    Status={0}", status ) );

            string version = Driver.getWirelessSoftwareVersion().ToString();
			Log.Debug( string.Format( "    Version={0}", version ) );

            int txInterval = Driver.getWirelessTransmissionInterval();
			Log.Debug( string.Format( "    TransmissionInterval={0}", txInterval ) );

            WirelessModule module = new WirelessModule( mac, version, status, txInterval );

			if ( deviceType == DeviceType.SC  || deviceType == DeviceType.VPRO )
			{
				module.HardwareVersion = Driver.getWirelessHardwareVersion();
				Log.Debug( string.Format( "    HardwareVersion={0}", module.HardwareVersion ) );

				module.RadioHardwareVersion = Driver.getWirelessRadioHardwareVersion();
				Log.Debug( string.Format( "    RadioHardwareVersion={0}", module.RadioHardwareVersion ) );

				module.OsVersion = Driver.getWirelessOsVersion();
				Log.Debug( string.Format( "    OsVersion={0}", module.OsVersion ) );

				module.EncryptionKey = Driver.getWirelessCustomEncryptionKey();
#if DEBUG // don't show encryption keys in release builds
				Log.Debug( string.Format( "    CustomEncryptionKey={0}", module.EncryptionKey ) );
#else
				Log.Debug( string.Format( "    CustomEncryptionKey={0}", string.Empty.PadRight(module.EncryptionKey.Length, '*') ) );
#endif

				module.MessageHops = Driver.getWirelessMessageHops();
				Log.Debug( string.Format( "    MessageHops={0}", module.MessageHops ) );

				module.MaxPeers = Driver.getWirelessMaximumPeers();
				Log.Debug( string.Format( "    MaximumPeers={0}", module.MaxPeers ) );

				module.PrimaryChannel = Driver.getWirelessPrimaryChannel();
				Log.Debug( string.Format( "    PrimaryChannel={0}", module.PrimaryChannel ) );

				module.SecondaryChannel = Driver.getWirelessSecondaryChannel();
				Log.Debug( string.Format( "    SecondaryChannel={0}", module.SecondaryChannel ) );

				module.ActiveChannelMask = GetWirelessActiveChannelMask();
				Log.Debug( string.Format( "    ActiveChannelMask={0}", module.ActiveChannelMask ) );

                if (Driver.Definition.HasWirelessBindingTimeoutConfigFeature)
                {
                    module.WirelessBindingTimeout = Driver.getWirelessBindingTimeout();
                    Log.Debug( string.Format( "    WirelessBindingTimeout={0}", module.WirelessBindingTimeout ) );
                }

                if (Driver.Definition.HasWirelessListeningPostChannelMaskFeature)
                {
                    module.ListeningPostChannelMask = GetWirelessListeningPostChannelMask();
                    Log.Debug(string.Format("    ListeningPostChannelMask={0}", module.ListeningPostChannelMask));
                }
                               
                module.WirelessFeatureBits = GetWirelessFeatureBits();
                Log.Debug( string.Format( "    WirelessFeatureBits={0}", module.WirelessFeatureBits ) );
                
				// wireless options
				const string func = "WirelessModuleOptions: ";
				Log.Debug( string.Format( "{0}", func ) );

				List<DeviceOption> wirelessOptions = new List<DeviceOption>();
				Hashtable supportedOptions = Driver.Definition.getSupportedInstrumentOptions();
				foreach ( InstrumentOption supportedOption in supportedOptions.Values )
				{
					// Options uploaded to iNet on the wireless module component should have the WirelessModule
					// OptionGroup assigned in the driver.
					if ( supportedOption.Group != OptionGroup.WirelessModule )
						continue;

					InstrumentOption driverOption = Driver.getInstrumentOption( supportedOption.Code );

					if ( driverOption is InstrumentBooleanOption )
					{
						InstrumentBooleanOption boolOption = (InstrumentBooleanOption)driverOption;

						Log.Debug( string.Format( "    {0} boolean option {1} - \"{2}\"",
							( boolOption.Enabled ? "Enabled" : "Disabled" ), boolOption.Code, boolOption.DisplayName ) );
						wirelessOptions.Add( new DeviceOption( boolOption.Code, boolOption.Enabled ) );
					}
					else if ( driverOption is InstrumentMultiOption )
					{
						InstrumentMultiOption multiOption = (InstrumentMultiOption)driverOption;

						Log.Debug( string.Format( "    Enabled multi option {0} - \"{1}\"", multiOption.EnabledCode, multiOption.DisplayName ) );
						// Add the code for the suboption that's currently enabled
						wirelessOptions.Add( new DeviceOption( multiOption.EnabledCode, true ) );
					}
					else
					{
						Log.Error( string.Format( "{0}Ignoring unsupported option {1} ({2})", func, driverOption.GetType().ToString(), driverOption.Code ) );
					}
				}
				module.Options = wirelessOptions;
			}

			return module;
		}

        /// <summary>
        /// Get a sensor at a specified position.
        /// </summary>
        /// <param name="position">The position to retrieve.</param>
        /// <param name="details">details</param>
        /// <returns>The installed component containing the sensor.</returns>
        public InstalledComponent DiscoverSensor( SensorPosition sensorPosition )
        {
			int position = sensorPosition.Position;

            // Sensor component code.
            string componentCode = GetSensorCode( position );

            string serialNumber = GetSensorSerialNumber(position);

            // If there is no sensor, return null.
            if ( ( componentCode == string.Empty ) || ( serialNumber == string.Empty ) )
            {
                Log.Warning( "No Sensor at position " + position + " (serial=\"" + serialNumber + "\",code=\"" + componentCode + "\")" );
                return null;
            }

            // Fill out the sensor.
            Sensor sensor = new Sensor();

            // Sensor serial number & sensor component code.
            sensor.Uid = serialNumber + '#' + componentCode;

            // Sensor component code.
            // sensor.Type.Code = componentCode;
            SensorType st = (SensorType)sensor.Type;
            st.MeasurementType = GetSensorMeasurementType(position);

            sensor.Resolution = GetSensorResolution( position );
            sensor.CalibrationGas = GasType.Cache[GetSensorCalGasCode(position)];
            sensor.CalibrationGasConcentration = GetSensorCalGasConcentration(position, sensor.Resolution);
            sensor.SoftwareVersion = GetSensorSoftwareVersion( position );
            sensor.Enabled = IsSensorEnabled( position );
            sensor.CalibrationStatus = GetStatus( position );
            sensor.BumpTestStatus = GetSensorBumpStatus(position);
			sensor.IsDualSenseCapable = sensorPosition.IsDualSenseCapable;
			sensor.Technology = GetSensorTechnology( position ); // only obtained from instrument in order to log it below.
            DateTime lastCalDate = GetSensorLastCalibrationTime(position); // obtained so we can log it just below.

            Log.Debug(string.Format("Sensor at Position={0}", position));
			Log.Debug(string.Format("    Uid={0}, Tech={1}, Type={2}, DualSenseCapable={3}", sensor.Uid, sensor.Technology, sensor.Type, sensor.IsDualSenseCapable ) );
            Log.Debug(string.Format("    MeasurementType={0}, Resolution={1}", st.MeasurementType, sensor.Resolution));
            Log.Debug(string.Format("    CalibrationGas={0}, CalibrationGasConcentration={1}", sensor.CalibrationGas, sensor.CalibrationGasConcentration));
            Log.Debug(string.Format("    SoftwareVersion={0}, Enabled={1}", sensor.SoftwareVersion, sensor.Enabled));
            Log.Debug(string.Format("    CalibrationStatus={0} ({1}), BumpTestStatus={2}", sensor.CalibrationStatus, Log.DateTimeToString(lastCalDate), sensor.BumpTestStatus ) );

            // Make the installed component to hold the sensor.
            InstalledComponent installedComponent = new InstalledComponent();
            installedComponent.Position = position;
            installedComponent.Component = sensor;

            sensor.PartNumber = GetSensorPartNumber(position);
            sensor.SetupDate = GetSensorSetupDate(position);
            sensor.SetupVersion = GetSensorSetupVersion(position);  // Configuration Software Version
            sensor.HardwareVersion = GetSensorHardwareVersion(position);
            sensor.ManufacturerCode = GetSensorManufacturerCode(position);

            sensor.DeadBand = GetSensorDeadBandValue(position, sensor.Resolution);
            sensor.Filter = GetSensorFilterLevel(position);
            sensor.CalibrationTimeout = Convert.ToInt32(GetSensorCalibrationTimeout(position).TotalSeconds);

            sensor.Span = GetSensorSpanReserve(position);
            sensor.SpanCoef = GetSensorSpanCoeff(position);
            sensor.SpanCoefMin = GetSensorSpanCoeffMin(position);
            sensor.SpanCoefMax = GetSensorSpanCoeffMax(position);

            sensor.PeakReading = GetSensorPeakReading(position, sensor.Resolution);

            sensor.TemperatureCompLow = GetSensorTemperatureLowCompensation(position);
            sensor.TemperatureCompHigh = GetSensorTemperatureHighCompensation(position);

            sensor.MinTemperature = GetSensorMinTemp(position);
            sensor.MaxTemperature = GetSensorMaxTemp(position);

            sensor.ZeroOffset = GetSensorZeroOffset(position, sensor.Resolution);
            sensor.ZeroMax = GetSensorZeroLimit(position, sensor.Resolution);
            sensor.ZeroMin = sensor.ZeroMax * -1.0;

			sensor.Alarm.GasAlert = GetSensorGasAlert( position, sensor.Resolution );
            sensor.Alarm.Low = GetSensorLowAlarm(position, sensor.Resolution);
            sensor.Alarm.High = GetSensorHighAlarm(position, sensor.Resolution);
			sensor.Alarm.TWA = GetSensorTwaEnabled( position ) ? GetSensorTwaAlarm( position, sensor.Resolution ) : double.MinValue;
			sensor.Alarm.STEL = GetSensorStelEnabled( position ) ? GetSensorStelAlarm( position, sensor.Resolution ): double.MinValue;
			
            Log.Debug(string.Format("    PartNumber={0}, SetupDate={1}, SetupVersion={2}", sensor.PartNumber, Log.DateTimeToString(sensor.SetupDate), sensor.SetupVersion));
            Log.Debug(string.Format("    HardwareVersion={0}, Manufacturer={1}", sensor.HardwareVersion, sensor.ManufacturerCode));
            Log.Debug(string.Format("    DeadBand={0}, Filter={1}, CalibrationTimeout={2}", sensor.DeadBand, sensor.Filter, sensor.CalibrationTimeout));
            Log.Debug(string.Format("    Span={0}, SpanCoef={1}, SpanCoefMin={2}, SpanCoefMax={3}", sensor.Span, sensor.SpanCoef, sensor.SpanCoefMin, sensor.SpanCoefMax));
            Log.Debug(string.Format("    PeakReading={0}", sensor.PeakReading));
            Log.Debug(string.Format("    TemperatureCompLow={0}, TemperatureCompHigh={1}", sensor.TemperatureCompLow, sensor.TemperatureCompHigh));
            Log.Debug(string.Format("    MinTemperature={0}, MaxTemperature={1}", sensor.MinTemperature, sensor.MaxTemperature));
            Log.Debug(string.Format("    ZeroOffset={0}, ZeroMax={1}, ZeroMin={2}", sensor.ZeroOffset, sensor.ZeroMax, sensor.ZeroMin));
			if ( this.Driver.Definition.HasGasAlertFeature )
				Log.Debug(string.Format("    GasAlert={0}", sensor.Alarm.GasAlert));
            Log.Debug(string.Format("    AlarmLow={0}, AlarmHigh={1}, AlarmTWA={2}, AlarmSTEL={3}", sensor.Alarm.Low, sensor.Alarm.High, sensor.Alarm.TWA, sensor.Alarm.STEL));

            // PIDs have a response factor associated with them. MX6 LELs have a 
            // gas correlation factor associated with them.
            if (IsSensorGasCodeConfigurable(sensor.Type.Code))
            {
                sensor.GasDetected = GetSensorGasCode(position);
                Log.Debug(string.Format("    GasDetected={0}", sensor.GasDetected));
            }

            return installedComponent;
        }

        /// <summary>
        /// Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593
        /// Checks whether instrument in system alaram. If instrument in system alarm throws InstrumentSystemAlarmException
        /// </summary>
        private void CheckInstrumentInSystemAlarm()
        {
            string instrumentSerialNumber = null;

            if (!InSystemAlarm())
                return; //not in system alarm return from here

            try
            {
                instrumentSerialNumber = GetSerialNumber();
                if ( instrumentSerialNumber.Trim().Length == 0 || instrumentSerialNumber.Trim() == "0" )
                    instrumentSerialNumber = null;
            }
            catch ( Exception e )
            {
                // Since instrument is in system alarm we are not sure we will be able to get serial number. 
                // If not, we dont' worry about it, but we at least log why.
                Log.Warning( e.ToString() );
            }

            int errorCode = GetInstrumentSystemAlarmErrorCode();
            Log.Warning( "INSTRUMENT IS IN SYSTEM ALARM! ErrorCode=" + errorCode );

            // UpgradeOnErrorFail is a configurable option applicable ONLY for service acounts to re-arrange priority of events
            if (!Configuration.DockingStation.UpgradeOnErrorFail)
                throw new InstrumentSystemAlarmException(instrumentSerialNumber, errorCode);

            // INS-8228 RHP v7.6, Service accounts need to perform auto-upgrade on instruments even in error/fail state
            // Incase "Instrument Firmware Upgrade schedule" is NOT avaiable, then we throw "InstrumentSystemAlarmException" on scheduler.
            _isInstrumentInSystemAlarm = true;
            Log.Warning("REPAIR ACCOUNT IGNORING INSTRUMENT IN SYSTEM ALARM TO ALLOW FIRMWARE UPGRADE! ErrorCode=" + errorCode);
        }

        /// <summary>
        /// Get a list of all of the sites on an instrument, except the active one.
        /// </summary>
        /// <returns>An array list with all of the sites, duplicates removed.</returns>
        public virtual List<string> GetUsers()
        {
            // Does instrument support having users?
            if ( Driver.Definition.MaxUserCount == 0 )
                return new List<string>();

            Dictionary<string, string> dict = new Dictionary<string, string>();

            string[] users = Driver.getUsers();

            // Strip out duplicates by putting into a hashtable
            foreach ( string user in users )
                dict[ user ] = user;  // if duplicate already in dictionary, it will be tossed and replaced.

            // Copy contents to a List that we can return.
            List<string> list = new List<string>( dict.Count );
            foreach ( string user in dict.Values )
                list.Add( user );

            return list;
        }

        /// <summary>
        /// Sets the instrument users to the appropriate values.
        /// </summary>
        /// <param name="newUsers">The list of users.</param>
        public virtual void SetUsers( List<string> newUsers )
        {
            // Does instrument support having users?
            if ( Driver.Definition.MaxUserCount == 0 )
                return;

            Driver.setUsers( newUsers.ToArray() );
        }


        /// <summary>
        /// Get a list of a all of the sites on an instrument, except the active one.
        /// </summary>
        /// <returns>An array list with all of the sites, duplicates removed.</returns>
        public virtual List<string> GetSites()
        {
            // Does instrument support having sites?
            if ( Driver.Definition.MaxSiteCount == 0 )
                return new List<string>();

            Dictionary<string, string> dict = new Dictionary<string, string>();

            string[] sites = Driver.getSites();

            // Strip out duplicates by putting into a hashtable
            foreach ( string site in sites )
                dict[ site ] = site;  // if duplicate already in hashtable, it will be tossed and replaced.

            // Copy contents to a List that we can return.
            List<string> list = new List<string>( dict.Count );
            foreach ( string site in dict.Values )
                list.Add( site);

            return list;
        }


        /// <summary>
        /// Sets the instrument sites to the appropriate values.
        /// </summary>
        /// <param name="newSites">The list of sites.</param>
        public virtual void SetSites( List<string> newSites )
        {
            // Does instrument support having sites?
            if ( Driver.Definition.MaxSiteCount == 0 ) return;

            Driver.setSites( newSites.ToArray() );
        }

        /// <summary>
        /// Set this instrument's list of favorite PID Response Factors
        /// </summary>
        /// <param name="pidFactors">List of strings</param>
        public virtual void SetFavoritePidFactors( List<string> pidFactors )
        {
            // Does this instrument support favorite PID factors?
            if ( Driver.Definition.MaxFavoriteFactorsCount == 0 )
                return;

            int count = 0;
            foreach ( string pidFactor in pidFactors )
                Log.Debug( string.Format( "SetFavoritePidFactors #{0}: \"{1}\"", ++count, pidFactor ) );

            if ( count == 0 ) Log.Debug( "SetFavoritePidFactors: None" );

            Driver.setFavoritePidFactors( pidFactors.ToArray() );
        }

        /// <summary>
        /// Return this instrument's list of favorite PID Response Factors
        /// </summary>
        /// <returns>List of strings</returns>
        public virtual List<string> GetFavoritePidFactors()
        {
            // Does this instrument support favorite PID factors?
            if ( Driver.Definition.MaxFavoriteFactorsCount == 0 )
                return new List<string>();

            string[] pidFactors = Driver.getFavoritePidFactors();

            int count = 0;
            foreach ( string pidFactor in pidFactors )
                Log.Debug( string.Format( "GetFavoritePidFactors #{0}: \"{1}\"", ++count, pidFactor ) );

            if ( count == 0 ) Log.Debug( "GetFavoritePidFactors: None" );

            return new List<string>( pidFactors );
        }

        /// <summary>
        /// Set this instrument's list of custom PID Response Factors
        /// </summary>
        /// <param name="customFactors">List of ISC.iNet.DS.DomainModel.CustomResponseFactors</param>
        public virtual void SetCustomPidFactors( List<ResponseFactor> customFactors )
        {
            // Does this instrument support PID factors?
            if ( Driver.Definition.MaxCustomFactorsCount == 0 )
                return;

            ISC.Instrument.Driver.CustomResponseFactor[] driverFactors = new ISC.Instrument.Driver.CustomResponseFactor[ customFactors.Count ];

            int count = 0;
            for ( int i = 0; i < customFactors.Count; i++ )
            {
                ISC.iNet.DS.DomainModel.ResponseFactor customFactor = (ISC.iNet.DS.DomainModel.ResponseFactor)customFactors[i];

                ISC.Instrument.Driver.CustomResponseFactor driverFactor = new ISC.Instrument.Driver.CustomResponseFactor();

                Log.Debug( string.Format( "SetCustomPidFactors #{0}: {1} \"{2}\"={3}",
                    ++count, customFactor.GasCode, customFactor.Name, customFactor.Value ) );

                driverFactor.GasCode = customFactor.GasCode;
                driverFactor.Name = customFactor.Name;
                driverFactor.Value = customFactor.Value;

                driverFactors[i] = driverFactor;
            }

            if ( count == 0 ) Log.Debug( "SetCustomPidFactors: None" );

            Driver.setCustomPidFactors( driverFactors );
        }

        /// <summary>
        /// Return this instrument's list of custom PID Response Factors
        /// </summary>
        /// <returns></returns>
        public virtual List<ResponseFactor> GetCustomPidFactors()
        {
            // Does this instrument support PID factors?
            if ( Driver.Definition.MaxCustomFactorsCount == 0 )
                return new List<ResponseFactor>();

            ISC.Instrument.Driver.CustomResponseFactor[] driverFactors = Driver.getCustomPidFactors();

            List<ResponseFactor> customFactors = new List<ResponseFactor>( driverFactors.Length );

            int count = 0;
            foreach ( ISC.Instrument.Driver.CustomResponseFactor driverFactor in driverFactors )
            {
                ISC.iNet.DS.DomainModel.ResponseFactor customFactor = new ISC.iNet.DS.DomainModel.ResponseFactor( driverFactor.GasCode );

                customFactor.Name = driverFactor.Name;
                customFactor.Value = driverFactor.Value;

                Log.Debug( string.Format( "GetCustomPidFactors #{0}: {1} \"{2}\"={3}",
                    ++count, customFactor.GasCode, customFactor.Name, customFactor.Value ) );

                customFactors.Add( customFactor );
            }

            if ( count == 0 ) Log.Debug( "GetCustomPidFactors: None" );

            return customFactors;
        }

		/// <summary>
		/// Gets this instrument's list of alarm action messages.
		/// </summary>
		/// <returns></returns>
		public virtual List<AlarmActionMessages> GetAlarmActionMessages()
		{
			if ( !HasAlarmActionMessagesFeature )
			{
				return new List<AlarmActionMessages>();
			}

			AlarmActionMessageGroup[] messageGroups = Driver.getAlarmActionMessages();
			
			List<AlarmActionMessages> aamList = new List<AlarmActionMessages>();
			AlarmActionMessages aam;

			for ( int i = 0; i < messageGroups.Length; i++ )
			{
				aam = new AlarmActionMessages(
					messageGroups[i].SensorCode,
					Utility.JoinStrings( messageGroups[i].GasAlertActionMessage ),
					Utility.JoinStrings( messageGroups[i].LowAlarmActionMessage ),
					Utility.JoinStrings( messageGroups[i].HighAlarmActionMessage ),
					Utility.JoinStrings( messageGroups[i].StelAlarmActionMessage ),
					Utility.JoinStrings( messageGroups[i].TwaAlarmActionMessage )
					);

				aamList.Add( aam );

				Log.Debug( string.Format( "GetAlarmActionMessages #{0}: {1}", i+1, aam ) );
			}

			return aamList;
		}

		/// <summary>
		/// Sets the instrument's list of alarm action messages.
		/// </summary>
		/// <param name="aamList"></param>
		public void SetAlarmActionMessages( List<AlarmActionMessages> aamList )
		{
			if ( !HasAlarmActionMessagesFeature )
			{
				return;
			}

			// convert to instrument driver object
			AlarmActionMessageGroup aamg;
			AlarmActionMessageGroup[] messageGroups = new AlarmActionMessageGroup[aamList.Count];
			for ( int i = 0; i < aamList.Count; i++ )
			{
				aamg = new AlarmActionMessageGroup(
					aamList[i].SensorCode,
					Utility.SplitString( aamList[i].GasAlertMessage ),
					Utility.SplitString( aamList[i].LowAlarmMessage ),
					Utility.SplitString( aamList[i].HighAlarmMessage ),
					Utility.SplitString( aamList[i].StelAlarmMessage ),
					Utility.SplitString( aamList[i].TwaAlarmMessage )
				);

				messageGroups[i] = aamg;
			}

			// the driver will handle if the list of messages is too small or too big
			Driver.setAlarmActionMessages( messageGroups );
		}


		/// <summary>
		/// Only call for Whisper enabled instruments.
		/// </summary>
		/// <returns>
		/// A hex string, e.g. 0xFFFF.
		/// </returns>
		private string GetWirelessActiveChannelMask()
		{
			return "0x" + Driver.getWirelessActiveChannelMask().ToString( "X4" );
		}

        /// <summary>
        /// Only call for Whisper enabled instruments
        /// </summary>
        /// <returns>
        /// A hex string, e.g. 0xFFFF.
        /// </returns>
        private string GetWirelessFeatureBits ()
        {
            return "0x" + Driver.getWirelessFeatures().ToString( "X4" );
        }

        /// <summary>
        /// Only call for HasWirelessAlarmMaskFeature instruments
        /// </summary>
        /// <returns></returns>
        private string GetWirelessAlarmMask ()
        {
            return "0x" + Driver.getWirelessAlarmMask().ToString( "X4" );
        }

		/// <summary>
		/// Only call for Whisper enabled instruments.
		/// </summary>
		/// <param name="mask">A hex string, e.g. 0xFFFF.</param>
		public void SetWirelessActiveChannelMask( string mask )
		{
			ushort value = Convert.ToUInt16( mask, 16 );

			Driver.setWirelessActiveChannelMask( value );
		}

        /// <summary>
        /// Only call for Whisper enabled instruments.
        /// </summary>
        /// <param name="mask">A hex string, e.g. 0xFFFF.</param>
        public void SetWirelessFeatureBits ( string mask )
        {
            if (string.IsNullOrEmpty( mask ))
                return;

            ushort value = Convert.ToUInt16( mask, 16 );

            Driver.setWirelessFeatures( value );
        }

        /// <summary>
        /// Only call for Wireless enabled instruments
        /// </summary>
        /// <param name="value"></param>
        public void SetWirelessBindingTimeout( int value )
        {
            Driver.setWirelessBindingTimeout(value);
        }
        
        /// <summary>
        /// Only call for Whisper enabled instruments.
        /// </summary>
        /// <returns>
        /// A hex string, e.g. 0xFFFF.
        /// </returns>
        private string GetWirelessListeningPostChannelMask()
        {
            return "0x" + Driver.getWirelessListeningPostChannelMask().ToString("X4");
        }

        /// <summary>
        /// Only call for Whisper enabled instruments.
        /// </summary>
        /// <param name="mask">A hex string, e.g. 0xFFFF.</param>
        public void SetWirelessListeningPostChannelMask(string mask)
        {
            ushort value = Convert.ToUInt16(mask, 16);

            Driver.setWirelessListeningPostChannelMask(value);
        }


		/// <summary>
		/// Get list of DeviceOptions for all of the options on the instrument and 
		/// whether they're enabled or disabled.
		/// </summary>
		/// <returns>
		/// <para>For all "boolean options", the list will contain the code of each, plus
		/// whether it's enabled or disabled.</para>
		/// <para>For all "multi options", the list will contain an enabled DeviceOption
		/// with the code of the multiOption's enabled subcode.
		/// </para>
		/// </returns>
		public virtual List<DeviceOption> GetOptions()
        {
            const string func = "GetInstrumentOptions: ";

            List<DeviceOption> deviceOptions = new List<DeviceOption>();

            Hashtable supportedOptions = Driver.Definition.getSupportedInstrumentOptions();

            Log.Debug(string.Format("{0}", func));

            foreach ( InstrumentOption supportedOption in supportedOptions.Values )
            {
				// Options uploaded to iNet on the instrument should NOT have the WirelessModule
				// OptionGroup assigned in the driver.
				if ( supportedOption.Group == OptionGroup.WirelessModule )
					continue;

				InstrumentOption driverOption = Driver.getInstrumentOption( supportedOption.Code );

                if ( driverOption is InstrumentBooleanOption )
                {
                    InstrumentBooleanOption boolOption = (InstrumentBooleanOption)driverOption;

                    Log.Debug(string.Format("    {0} boolean option {1} - \"{2}\"",
                        (boolOption.Enabled ? "Enabled" : "Disabled"), boolOption.Code, boolOption.DisplayName));
                    deviceOptions.Add( new DeviceOption( boolOption.Code, boolOption.Enabled ) );
                }
                else if ( driverOption is InstrumentMultiOption )
                {
                    InstrumentMultiOption multiOption = (InstrumentMultiOption)driverOption;

                    Log.Debug(string.Format("    Enabled multi option {0} - \"{1}\"", multiOption.EnabledCode, multiOption.DisplayName));
                    // Add the code for the suboption that's currently enabled
                    deviceOptions.Add( new DeviceOption( multiOption.EnabledCode, true ) );
                }
                else
                {
                    Log.Error( string.Format( "{0}Ignoring unsupported option {1} ({2})", func, driverOption.GetType().ToString(), driverOption.Code ) );
                }
            }

            // Hygiene (a.k.a. 'Datalog') option (which merely indicates if instrument supports 
            // datalogging or not) needs to be treated specially... if instrument supports datalog, 
            // we need to tell the server so by returning an Enabled datalog option.
            AddHyginOption( deviceOptions );

            return deviceOptions;
        }

        /// <summary>
        /// Set the instruments options to the desired values.
        /// </summary>
        /// <param name="enabledOptionsList">
        /// This method will enable all options that are in this list.
        /// All other options will be disabled.
		/// WirelessModule options will be skipped.
        /// </param>
        /// <returns>
        /// The returned list returns the same DeviceOptions as if GetOptions was called after SetOptions
        /// was finished with its work (although GetOptions is not actually called. 
        /// So, the returned DeviceOptions list contains ALL of the options on the instrument and 
        /// whether they're enabled or disabled.  Specfically....
        /// <para>For all "boolean options", the list will contain the code of each, plus
        /// whether it's enabled or disabled.</para>
        /// <para>For all "multi options", the list will contain an enabled DeviceOption
        /// with the code of the multiOption's enabled subcode.
        /// </para>
        /// </returns>
        public List<DeviceOption> SetInstrumentOptions( List<DeviceOption> enabledDeviceOptionsList )
        {
            const string func = "SetInstrumentOptions: ";
            const string LCALA = "LCALA", LCALB = "LCALB", FBUMP = "FBUMP", BLE = "BLE", BLEM = "BLEM", BLEO = "BLEO", BLEC = "BLEC", BLELC = "BLELC";
            bool lcalOptionSpecified = false;
            bool isMx6 = Configuration.DockingStation.Type == DeviceType.MX6; // is this an MX6 docking station?
            bool isGB = Configuration.DockingStation.Type == DeviceType.GBPLS || Configuration.DockingStation.Type == DeviceType.GBPRO;
            bool isVentisPro = GetInstrumentType() == DeviceType.VPRO;

            // First, put all the enabled (desired) options into a hashtable for easy lookup
            Dictionary<string, DeviceOption> enabledOptions = new Dictionary<string, DeviceOption>();
            foreach ( DeviceOption deviceOption in enabledDeviceOptionsList )
            {
                // Since the passed-in list is specifying all the options that should be enabled, we check that they're actually all enabled.
                Log.Assert( deviceOption.Enabled, "SetInstrumentOptions(): enabledDeviceOptionsList contains disabled option! - " + deviceOption.Code );
                enabledOptions[deviceOption.Code] = deviceOption;
            }

            // Next, get all of instrument's CURRENTLY ENABLED options.  We split them into
            // three hashtables: 1st is for BooleanOptions, a 2nd for MultiOptions, and a
            // 3rd which is each MultiOption keyed once on each of its sub options.
            Dictionary<string,InstrumentBooleanOption> boolOptions = new Dictionary<string,InstrumentBooleanOption>();
            Dictionary<string,InstrumentMultiOption> multiOptions  = new Dictionary<string, InstrumentMultiOption>();
            Dictionary<string,InstrumentMultiOption> subMultiOptions  = new Dictionary<string,InstrumentMultiOption>();
            Log.Debug( "Retrieving instrument's current options" );
            foreach ( InstrumentOption supportedOption in Driver.Definition.getSupportedInstrumentOptions().Values ) // <- current options (both enabled/disabled)
            {
				// WirelessModule options will not be set through this method.
				if ( supportedOption.Group == OptionGroup.WirelessModule )
					continue;

				InstrumentOption driverOption = Driver.getInstrumentOption( supportedOption.Code );

                if ( driverOption is InstrumentBooleanOption )
                    boolOptions[ driverOption.Code ] = (InstrumentBooleanOption)driverOption;
                else if ( driverOption is InstrumentMultiOption )
                {
                    InstrumentMultiOption multiOption = (InstrumentMultiOption)driverOption;
                    multiOptions[ driverOption.Code ] = multiOption;
                    // Add the MultiOption to the hashable multiple times - once
                    // keyed on its main code, and again for each subcode.
                    foreach ( InstrumentMultiOption.SubOptionInfo subOption in multiOption.SubOptions )
                        subMultiOptions[ subOption.Code ] = (InstrumentMultiOption)multiOption;
                }
            }


            // Per decision of Product Management, VPro's bluetooth radio should always be turned on. (INS-8322/INS-8429,July 2017)
            // This is done by enabling the "BLE" BooleanOption. We ensure this happens by first checking
            // boolOptions dictionary which tells us if the instrument version  supports the BLE option.
            // Next, we add option to the enabledDeviceOptionsList if it's not already there.
            if ( isVentisPro && boolOptions.ContainsKey( BLE ) && ( enabledDeviceOptionsList.Find( dop => dop.Code == BLE ) == null ) )
            {
                DeviceOption bleDeviceOption = new DeviceOption( BLE, true );
                enabledDeviceOptionsList.Add( bleDeviceOption );
                enabledOptions[BLE] = bleDeviceOption;
            }

            // INS-2873, 6/19/2012 - For GasBadges, we need to make sure that Field Bump
            // option is always enabled before Bump Overdue option, due to bug in instrument.
            // i.e., the bug is that Bump Overdue can't be enabled on the instrument unless
            // Field Bump is enabled first.
            // So, for gasbadges, if we're being told to enable FBUMP, and the instrument supports
            // FBUMP (older instruments won't support it), then do the following...
            if ( isGB && ( enabledDeviceOptionsList.Find( o => o.Code == FBUMP ) != null ) && boolOptions.ContainsKey( FBUMP ) )
            {
                InstrumentBooleanOption fbump = boolOptions[ FBUMP ];

                if ( fbump.Enabled == false ) // No need to enable it if it's already enabled.
                {
                    fbump.Enabled = true;
                    Log.Debug( string.Format( "{0}Enabling boolean option {1} - \"{2}\"", func, fbump.Code, fbump.DisplayName ) );
                    Driver.setInstrumentOption( fbump );
                }
                // Remove it from the hash table so we don't process it again in 
                // any of the logic remaining in this method.
                boolOptions.Remove( FBUMP );
            }

            //If its ventis pro, set bluetooth monitoring options BLEC, BLELC only if iNetNowFeature is enabled
            if ( isVentisPro && Driver.Definition.HasBluetoothFeature )
            {
                if ( !Driver.isiNetNowFeatureEnabled() && multiOptions.ContainsKey(BLEM) )
                {
                    DeviceOption option = enabledDeviceOptionsList.FirstOrDefault( o => o.Code == BLEC || o.Code == BLELC );
                    if (option != null)
                    {   
                        InstrumentMultiOption bluetoothMonitoring = multiOptions[BLEM];                        
                        Log.Debug( string.Format( "{0} iNetNowFeature - {1}, {2} - {3} - Not supported", func, Driver.isiNetNowFeatureEnabled().ToString(),
                            bluetoothMonitoring.Code, option.Code ) );

                        bluetoothMonitoring.EnabledCode = BLEO;

                        Log.Debug( string.Format( "{0}Resetting multi option {1} - {2} \"{3}\"", func, bluetoothMonitoring.Code,
                            bluetoothMonitoring.EnabledCode, bluetoothMonitoring.DisplayName ) );

                        Driver.setInstrumentOption( bluetoothMonitoring );

                        // Remove it from the hash table so we don't process it again in 
                        // any of the logic remaining in this method.
                        if (subMultiOptions.ContainsKey( option.Code ))
                            subMultiOptions.Remove( option.Code );
                    }
                }
            }
          
            // Now, iterate through the passed-in enabledDeviceOptionsList and look for those 
            // that are currently set in the instrument differently from its desired
            // setting.  For each set differently, set it on the instrument to be equal.
            foreach ( DeviceOption deviceOption in enabledDeviceOptionsList ) // <- the device options to enable
            {
                // Figure out if the DeviceOption is represented by the driver as a boolean option or multi option.

                InstrumentOption driverOption = null;

                InstrumentBooleanOption boolOption = null;
                if ( boolOptions.TryGetValue( deviceOption.Code, out boolOption ) )
                {
                    driverOption = boolOption;
                }
                else
                {
                    InstrumentMultiOption multiOption = null;
                    subMultiOptions.TryGetValue( deviceOption.Code, out multiOption );
                    driverOption = multiOption;
                }

                if ( driverOption == null )
                {
                    Log.Debug( string.Format( "{0}Ignoring unsupported instrument option {1}", func, deviceOption.Code ) );
                    continue;
                }

                // Is the option on the instrument a simple boolean option?
                if ( driverOption is InstrumentBooleanOption )
                {
                    Log.Debug( string.Format( "{0}Enabling boolean option {1} - \"{2}\"", func, deviceOption.Code, driverOption.DisplayName ) );
                    SetBooleanOption( deviceOption, (InstrumentBooleanOption)driverOption );
                }
                // If not a BooleanOption, then check if its implemented on the instrument as a sub-option 
                // within a MultiOption
                else if ( driverOption is InstrumentMultiOption )
                {
                    Log.Debug( string.Format( "{0}Enabling {1} in multi option {2} - \"{3}\"", func, deviceOption.Code, driverOption.Code, driverOption.DisplayName ) );
                    SetMultiOption( deviceOption, (InstrumentMultiOption)driverOption );
                }

                // INS-2290/IID-25 -- For MX6, LCALB used to be a stand-alone BooleanOption.  But in v5.2, it was 
                // changed to be a suboption of a MultiOption. (The other sub-option being LCALA".)  We need to 
                // be sensitive to the fact that because of old instruments / data in database, the new LCALA 
                // option won't always be specified.  We therefore assume that if neither suboption is specified, 
                // then the IDS should implicitly choose LCALA option.
                lcalOptionSpecified = isMx6 && ( lcalOptionSpecified || ( deviceOption.Code == LCALB || deviceOption.Code == LCALA ) );

            }  // end-foreach DeviceOption

            // INS-2290/IID-25 - If no LCAL (LCALA nor LCALB) option specified, then we assme LCALA.
            if ( isMx6 && !lcalOptionSpecified )
            {
                InstrumentMultiOption multiOption = null;
                // Get the MultiOption that LCALA is a suboption of.  We know from the code above that the parent multiOption
                // has been added to the hashtable keyed on all its suboptions, so we can confidently find it using LCALA code.
                subMultiOptions.TryGetValue( LCALA, out multiOption );
                // If we found the option (probably always will), and it's not already set to LCALA, then set it to LCALA.
                if ( multiOption != null && multiOption.EnabledCode != LCALA )
                {
                    multiOption.EnabledCode = LCALA;
                    Driver.setInstrumentOption( multiOption );
                }
            }

            // Passed-in enabledOptions only contains those options that need to be turned on.
            // So boolean options not in enabledOptions need to be turned off.
            foreach ( InstrumentBooleanOption boolOption in boolOptions.Values )
            {
                if ( !enabledOptions.ContainsKey( boolOption.Code ) ) 
                {
                    DeviceOption deviceOption = new DeviceOption( boolOption.Code, false );
                    Log.Debug( string.Format( "{0}Disabling option {1} - \"{2}\"", func, boolOption.Code, boolOption.DisplayName ) );
                    SetBooleanOption( deviceOption, boolOption );
                }
            }

            // We need to create and then return a DeviceOption list containing ALL of the instrument options
            List<DeviceOption> deviceOptions = new List<DeviceOption>( boolOptions.Count + subMultiOptions.Count );
            foreach ( InstrumentBooleanOption bo in boolOptions.Values )
            {
                Log.Trace( string.Format( "    {0} boolean option {1} - \"{1}\"", ( bo.Enabled ? "Enabled" : "Disabled" ), bo.Code, bo.DisplayName ) );
                deviceOptions.Add( new DeviceOption( bo.Code, bo.Enabled ) );
            }
            foreach ( InstrumentMultiOption mo in multiOptions.Values )
            {
                Log.Trace( string.Format( "    Enabled multi option {0} - \"{1}\"", mo.EnabledCode, mo.DisplayName ) );
                deviceOptions.Add( new DeviceOption( mo.EnabledCode, true ) ); // Add the code for the suboption that's currently enabled
            }
            // Hygiene (a.k.a. 'Datalog') option (which merely indicates if instrument
            // supports datalogging or not) needs to be treated specially...
            // if instrument supports datalog, we need to tell the server so by returning
            // an Enabled datalog option.
            AddHyginOption( deviceOptions );

            return deviceOptions;
        }

		/// <summary>
        /// Hygiene (a.k.a. 'Datalog') option (which merely indicates if instrument 
        /// supports datalogging or not) needs to be treated specially... if instrument 
        /// supports datalog, we need to tell the server so by returning an Enabled datalog option.
        /// </summary>
        /// <param name="deviceOptionsList"></param>
        private void AddHyginOption( List<DeviceOption> deviceOptionsList )
        {
            if ( Driver.Definition.HasDataLoggingFeature )
                deviceOptionsList.Add( new DeviceOption( "HYGIN", true ) );
        }

		/// <summary>
		/// Set the WirelessModule options to the desired values.
		/// </summary>
		/// <param name="enabledOptionsList">
		/// This method will enable all options that are in this list.
		/// All other options will be disabled.
		/// Instrument options will be skipped.
		/// </param>
		/// <returns>
		/// The returned list returns the same DeviceOptions as if DiscoverWirelessModule was called after 
		/// SetWirelessModuleOptions was finished with its work (although DiscoverWirelessModule is not actually called. 
		/// So, the returned DeviceOptions list contains ALL of the options on the WirelessModule and 
		/// whether they're enabled or disabled.  Specfically....
		/// <para>For all "boolean options", the list will contain the code of each, plus
		/// whether it's enabled or disabled.</para>
		/// <para>For all "multi options", the list will contain an enabled DeviceOption
		/// with the code of the multiOption's enabled subcode.
		/// </para>
		/// </returns>
		public List<DeviceOption> SetWirelessModuleOptions( List<DeviceOption> enabledWirelessOptionsList )
		{
			const string func = "SetWirelessModuleOptions: ";
	
			// First, put all the enabled (desired) options into a hashtable for easy lookup
			Dictionary<string, DeviceOption> enabledOptions = new Dictionary<string, DeviceOption>();
			foreach ( DeviceOption wlOption in enabledWirelessOptionsList )
			{
				// Since the passed-in list is specifying all the options that should be enabled, we check that they're actually all enabled.
				Log.Assert( wlOption.Enabled, "SetWirelessModuleOptions(): enabledWirelessOptionsList contains disabled option! - " + wlOption.Code );
				enabledOptions[wlOption.Code] = wlOption;
			}

			// Next, get all of wireless module's CURRENTLY ENABLED options.  We split them into
			// three hashtables:  one for BooleanOptions a 2nd for for MultiOptions, and a
			// 3rd which is each MultiOption keyed once on each of its sub options.
			Dictionary<string,InstrumentBooleanOption> boolOptions = new Dictionary<string, InstrumentBooleanOption>();
			Dictionary<string,InstrumentMultiOption> multiOptions  = new Dictionary<string, InstrumentMultiOption>();
			Dictionary<string,InstrumentMultiOption> subMultiOptions  = new Dictionary<string, InstrumentMultiOption>();
			Log.Debug( "Retrieving wireless module's current options" );
			foreach ( InstrumentOption supportedOption in Driver.Definition.getSupportedInstrumentOptions().Values ) // <- supported options (both enabled/disabled)
			{
				// Only WirelessModule options will be set through this method.
				if ( supportedOption.Group != OptionGroup.WirelessModule )
					continue;

				InstrumentOption driverOption = Driver.getInstrumentOption( supportedOption.Code );

				if ( driverOption is InstrumentBooleanOption )
					boolOptions[driverOption.Code] = (InstrumentBooleanOption)driverOption;
				else if ( driverOption is InstrumentMultiOption )
				{
					InstrumentMultiOption multiOption = (InstrumentMultiOption)driverOption;
					multiOptions[driverOption.Code] = multiOption;
					// Add the MultiOption to the hashable multiple times - once
					// keyed on its main code, and again for each subcode.
					foreach ( InstrumentMultiOption.SubOptionInfo subOption in multiOption.SubOptions )
						subMultiOptions[subOption.Code] = (InstrumentMultiOption)multiOption;
				}
			}


			// Now, iterate through the passed-in enabledWirelessOptionsList and look for those 
			// that are currently set in the wireless module differently from its desired
			// setting.  For each set differently, set it on the wireless module to be equal.
			foreach ( DeviceOption deviceOption in enabledWirelessOptionsList ) // <- the device options to enable
			{
				// Figure out if the DeviceOption is represented by the driver as a
				// boolean option or multi option.
				InstrumentOption driverOption = null;

				InstrumentBooleanOption boolOption = null;
				if ( boolOptions.TryGetValue( deviceOption.Code, out boolOption ) )
				{
					driverOption = boolOption;
				}
				else
				{
					InstrumentMultiOption multiOption = null;
					subMultiOptions.TryGetValue( deviceOption.Code, out multiOption );
					driverOption = multiOption;
				}

				if ( driverOption == null )
				{
					Log.Debug( string.Format( "{0}Ignoring unsupported wireless module option {1}", func, deviceOption.Code ) );
					continue;
				}

				// Is the option on the wireless module a simple boolean option?
				if ( driverOption is InstrumentBooleanOption )
				{
					Log.Debug( string.Format( "{0}Enabling boolean option {1} - \"{2}\"", func, deviceOption.Code, driverOption.DisplayName ) );
					SetBooleanOption( deviceOption, (InstrumentBooleanOption)driverOption );
				}
				// If not a BooleanOption, then check if its implemented on the wireless module as a sub-option 
				// within a MultiOption
				else if ( driverOption is InstrumentMultiOption )
				{
					Log.Debug( string.Format( "{0}Enabling {1} in multi option {2} - \"{3}\"", func, deviceOption.Code, driverOption.Code, driverOption.DisplayName ) );
					SetMultiOption( deviceOption, (InstrumentMultiOption)driverOption );
				}
			}  // end-foreach DeviceOption


			// Passed-in enabledOptions only contains those options that need to be turned on.
			// So boolean options not in enabledOptions need to be turned off.
			foreach ( InstrumentBooleanOption boolOption in boolOptions.Values )
			{
				if ( !enabledOptions.ContainsKey( boolOption.Code ) )
				{
					DeviceOption deviceOption = new DeviceOption( boolOption.Code, false );
					Log.Debug( string.Format( "{0}Disabling option {1} - \"{2}\"", func, boolOption.Code, boolOption.DisplayName ) );
					SetBooleanOption( deviceOption, boolOption );
				}
			}

			// We need to create and then return a DeviceOption list containing ALL of the wireless module options
			List<DeviceOption> wirelessOptions = new List<DeviceOption>( boolOptions.Count + subMultiOptions.Count );
			foreach ( InstrumentBooleanOption bo in boolOptions.Values )
			{
				Log.Trace( string.Format( "    {0} boolean option {1} - \"{1}\"", ( bo.Enabled ? "Enabled" : "Disabled" ), bo.Code, bo.DisplayName ) );
				wirelessOptions.Add( new DeviceOption( bo.Code, bo.Enabled ) );
			}
			foreach ( InstrumentMultiOption mo in multiOptions.Values )
			{
				Log.Trace( string.Format( "    Enabled multi option {0} - \"{1}\"", mo.EnabledCode, mo.DisplayName ) );
				wirelessOptions.Add( new DeviceOption( mo.EnabledCode, true ) ); // Add the code for the suboption that's currently enabled
			}


			return wirelessOptions;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="desiredDeviceOption"></param>
        /// <param name="currentBooleanOption"></param>
        private void SetBooleanOption( DeviceOption desiredDeviceOption, InstrumentBooleanOption currentBooleanOption )
        {
            bool currentValue = currentBooleanOption.Enabled;

            bool desiredValue = desiredDeviceOption.Enabled;

            // If current value on the instrument is different than the
            // desired value passed in, we need to toggle it.
            if ( currentValue != desiredValue )
            {
                currentBooleanOption.Enabled = desiredValue;
                Driver.setInstrumentOption( currentBooleanOption );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="desiredDeviceOption"></param>
        /// <param name="currentMultiOption"></param>
        private void SetMultiOption( DeviceOption desiredDeviceOption, InstrumentMultiOption currentMultiOption )
        {
            // Ignore 'off' settings.  We only want 'on' settings.
            if ( !desiredDeviceOption.Enabled )
                return;

            string currentSubOption = currentMultiOption.EnabledCode;
            string desiredSubOption = desiredDeviceOption.Code;

    // Even if the current option looks to be the same as the option we need to set, always write
    // the option anyways to ensure that the other disabled suboptions are cleared (there have
    // been instances where more than one suboption gets enabled on the instrument)
    //        if ( currentSubOption == desiredSubOption ) return;

            currentMultiOption.EnabledCode = desiredSubOption;

            Driver.setInstrumentOption( currentMultiOption ); // write new option setting to instrument.
        }

        /// <summary>
        /// Used by Datalog processing.  Seems to be used to 'reverse engineer'
        /// the sensor type based on the gascode that's in the session?
        /// </summary>
        /// <param name="gasCode"></param>
        /// <returns></returns>
        static public string GasCode2SensorCode( string gasCode )
        {
            if ( Configuration.DockingStation.Type != DeviceType.GBPRO )
            {
                Log.Error( string.Format( "GasCode2SensorCode is not supported (nor necessary!) for {0} docking stations", Configuration.DockingStation.Type ) );
                return string.Empty;
            }

            string tmpSensorCode = gasCode.Replace( 'G' , 'S' );
            int gasType = int.Parse( gasCode.Replace( 'G' , '0' ) );

            // Most toxics, or Oxygen?  There's a one-to-one between sensor and gas
            if ( ( gasType > 0 ) && ( gasType <= 21 ) )
                return tmpSensorCode;
		
            if ( gasType <= 27 ) // Combustible gas?
                return SensorCode.CombustibleLEL;

            // ETHYLENE OXIDE is a special case - the gas code is in the PID gas range.
            // Therefore, we need to 'special case' it here, before checking for the PID
            // gas range below.  This would only apply to GBPRO instrument.  Anything
            // else seeing this gas would have seen it with a PID sensor.
            if ( Configuration.DockingStation.Type == DeviceType.GBPRO && gasCode == GasCode.EthyleneOxide )
                return SensorCode.EtO;

            if ( gasType >= 28 ) // PID gas?
                return SensorCode.PID;

            return tmpSensorCode;
        }

        /// <summary>
        /// Retrieves the sensor zero offset setting.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor zero offset setting</returns>
        public virtual double GetSensorZeroOffset( int position , double resolution )
        {
            int baseline = GetSensorBaseline( position );
            if ( baseline != int.MinValue )
                return baseline * resolution;

            return double.MinValue;
        }						
	
        /// <summary></summary>
        /// <param name="components">List of unsorted components</param>
        /// <returns>Sorted components</returns>
        static public List<InstalledComponent> SortSensorsByCalibrationOrder( List<InstalledComponent> components )
        {
            // Sort by calibration order.
            components.Sort( new CalibrationOrderComparer() );
		
            // Return the sorted array.
            return components;
        }

        /// <summary></summary>
        /// <param name="components">List of unsorted components</param>
        /// <returns>Sorted components</returns>
        static public List<InstalledComponent> SortSensorsByBumpOrder(List<InstalledComponent> components)
        {
            // Sort by calibration order.
            components.Sort(new BumpOrderComparer());

            // Return the sorted array.
            return components;
        }

        /// <summary>
        /// Check for the condition where an instrument has reset.
        /// </summary>
        /// <param name="response">The response to signify an error condition.</param>
        /// <remarks>
        /// SGF Aug-04-2009  DSW-331 (rev. 2)  ("GBPlus may reset when docked and under test; DS needs to be able to detect this")
        /// </remarks>
        public virtual bool TestForInstrumentReset(SensorGasResponse response, string statusMsg)
        {
            if (IsInstrumentResetting() == true)
            {
                Log.Debug("INSTRUMENT RESETTING AT: " + statusMsg);

                if (response != null)
                {
                    // We do not want to set the Cal Fault bit if this is (a) a bump, and (b) this is an OXYGEN sensor.
                    // Otherwise, set the Cal Fault bit.
                    if ( !(response.Type == GasResponseType.Bump && response.GasConcentration.Type.Code == "G0020") )
                        SetCalibrationFault(true);

                    response.Status = Status.Failed;  // Setting the status to 'Failed'.
                }
                else
                {
                    SetCalibrationFault(true);

                    // Reset encountered without a response object available; throw an exception.
                    throw new Exception("INSTRUMENT RESETTING AT: " + statusMsg);
                }

                return true;
            }
            return false;
        }


        /// <summary>
        /// Attempt to pre-condition difficult sensors.
        /// 
        /// This proceeds similar to a bump, but with a much longer timeout. Attempt to read
        /// multiple good readings from the sensor before finishing the pre-condition.
        /// </summary>
        /// <param name="installedComponent">The component to precondition.</param>
        /// <param name="gasEndPoint">The gas end point to use.</param>
        /// <param name="response">The response to place the readings in.</param>
		/// <returns>Returns the TimeSpan of gas used to precondition the sensor.  TimeSpan is zero if no precondition occurred.</returns>
        public virtual TimeSpan PreconditionSensor( InstalledComponent installedComponent , GasEndPoint gasEndPoint , SensorGasResponse response )
        {
            TimeSpan timeElapsed;
            Sensor sensor;
            int numReadings = 0;
            int numTotalReadings = 0;
            int numOddReadings = 0;
            double maximumReading;

            Status originalStatus = response.Status;

            // Only precondition sensors.
            if ( ! ( installedComponent.Component is Sensor ) )
            {
                Log.Debug( "Error: Can only precondition sensors." );
                return new TimeSpan( 0, 0, 0 );
            }

            // default start to now.  Will be reset to when/if we turn on the pump.
            DateTime startTime = DateTime.UtcNow;

            try
            {
                sensor = (Sensor)installedComponent.Component;

                // Only precondition certain sensors.
                Log.Debug( "Checking for PRECONDITION" );

                // SGF  Jan-2-2009  DSW-173, DSW-174
                // Adding code to test whether this is a precondition for calibration or bump
                if (response.Type == GasResponseType.Calibrate)
                {
					if ( Configuration.DockingStation.Type == DeviceType.MX4 )
					{
						// Ventis instruments have been losing contact with the charging pins
						// on the DS which causes the instrument to leave the Calibrating mode.
						// The instrument likely goes to Charging mode when this happens.
						// There is no reason to precondition the sensor when the calibration
						// will inevitably fail.
						OperatingMode opMode = Driver.getOperatingMode();

						// After zeroing, the instrument is told to go into Running and then Calibrating mode.  
						// However, testing has revealed that MX4 instruments are not guaranteed to always 
						// make it into the Calibrating operating mode by this point.
						if ( opMode != OperatingMode.Calibrating && opMode != OperatingMode.Running )
						{
							Log.Debug( "******************************************" );
							Log.Debug( string.Format( "* INSTRUMENT IS NOT IN CALIBRATING MODE! *  Instrument is in \"{0}\" mode.", opMode.ToString() ) );
							Log.Debug( "******************************************" );
							Log.Debug( "PRECOND: SKIPPING sensor " + installedComponent.Position + ", UID=" + sensor.Uid );
							Log.Debug( "Cal Gas: " + sensor.CalibrationGas.Code + " Sensor Type: " + sensor.Type.Code );
							return TimeSpan.Zero;
						}
					}


                    if (!IsSensorCalPreconditionEnabled(installedComponent))
                    {
                        Log.Debug("PRECOND: SKIPPING sensor " + installedComponent.Position + ", UID=" + sensor.Uid);
                        Log.Debug("Cal Gas: " + sensor.CalibrationGas.Code + " Sensor Type: " + sensor.Type.Code);
                        return new TimeSpan(0, 0, 0);
                    }
                }
                else // GasResponseType.Bump
                {
                    if (!IsSensorBumpPreconditionEnabled(installedComponent))
                    {
                        Log.Debug("BUMP PRECOND: SKIPPING sensor " + installedComponent.Position + ", UID=" + sensor.Uid);
                        Log.Debug("Cal Gas: " + sensor.CalibrationGas.Code + " Sensor Type: " + sensor.Type.Code);
                        return new TimeSpan(0, 0, 0);
                    }
                }


                // Get the sensor's maximum reading.
                maximumReading = GetSensorMaximumReading( installedComponent.Position , sensor.Resolution );

                Log.Debug( "PRECOND: PRECONDITIONING Sensor " + installedComponent.Position + ", UID=" + sensor.Uid + ", " + installedComponent.Component.Type.Code );
                Log.Debug( "PRECOND: Sensor MaximumReading: " + maximumReading + ", Resolution: " + sensor.Resolution );
                Log.Debug( "PRECOND: Gas Conc: " + response.GasConcentration.Concentration );

                // Set the proper flow rate.
                // SGF  Jan-2-2009  DSW-173, DSW-174
                // Adding code to test whether this is a precondition for calibration or bump
                int desiredFlowRate;
                if (response.Type == GasResponseType.Calibrate)
                    desiredFlowRate = GetSensorPreconditionFlowRate( installedComponent );
                else
                    desiredFlowRate = GetSensorBumpPreconditionFlowRate( installedComponent );

                startTime = DateTime.UtcNow;  // reset starttime to be equal to when we turn on the pump.

                OpenGasEndPoint( gasEndPoint, desiredFlowRate );

                DateTime now = DateTime.UtcNow;

                // SGF  Jan-2-2009  DSW-173, DSW-174
                // Adding code to test whether this is a precondition for calibration or bump
                TimeSpan timeOut;
                if (response.Type == GasResponseType.Calibrate)
                {
                    timeOut = GetSensorPreconditionTimeout(installedComponent);
                }
                else
                {
                    timeOut = GetSensorBumpPreconditionTimeout(installedComponent);
                }

                // We ignore odd readings for the first half of the max precondition time.
                // So as we take readings, we ignore them until datetime.Now exceeds oddTime
                DateTime oddTime = now.AddTicks( timeOut.Ticks / 2L );
                DateTime endTime = now + timeOut;

                PauseSensor( installedComponent.Position, false );

                Log.Debug( "PRECOND: (" + installedComponent.Position + ") Start Time: " + Log.DateTimeToString(DateTime.UtcNow) + " End Time: " + Log.DateTimeToString(endTime) );
			
                while ( Pump.GetOpenValvePosition() > 0 )
                {
                    now = DateTime.UtcNow;

                    // Attempt to get a valid reading within the time allowed.
                    if ( now >= endTime )
                    {
                        Log.Debug( "Precondition timing out." );
                        response.Status = Status.Failed;
                        break;
                    }

                    // Wait a bit before each reading during preconditioning
                    Thread.Sleep( 1000 );

                    if ( !Controller.IsDocked() )
                        throw new InstrumentNotDockedException();

                    //                  try
                    //                  {
                    double rawReading = GetSensorReading( installedComponent.Position , sensor.Resolution );
                    
                    numTotalReadings++;

                    // Set response reading.
                    response.Reading = rawReading;

                    if ( Math.Abs( rawReading ) > maximumReading )
                    {
                        // Ignore any abnormal readings for the first half of the preconditioning.
                        if ( now > oddTime )
                        {
                            numOddReadings++;
                            Log.Debug( "PRECOND: (" + installedComponent.Position + ") Odd reading seen: " + rawReading );
                        }
                        else
                            Log.Debug( "PRECOND: (" + installedComponent.Position + ") Odd reading ignored: " + rawReading );
                    }

                    else if ( response.FullSpanReserve > 50.0D )
                        numReadings++;

                    Log.Debug( "PRECOND: (" + installedComponent.Position + ")  (" + numReadings + "/" + numTotalReadings + ") span: " + response.FullSpanReserve + " raw: " + rawReading );

                    // Must pass a minimum number of 2 readings to pass precondition
                    if ( numReadings >= 2 )
                    {
                        response.Status = Status.Passed;
                        break;  // When we succeed, drop out of the loop.
                    }

                    if ( numOddReadings >= 3 ) // 3 odd readings may happen before exiting preconditiong.
                    {
                        response.Status = Status.Failed;
                        Log.Debug( "PRECOND: (" + installedComponent.Position + ")  Aborting due to too many odd readings." );
                        break;
                    }


                }  // end-while
            
                Log.Debug( "PRECONDITION of sensor " + installedComponent.Position + " " + response.Status.ToString().ToUpper() );

                Log.Debug( "Open valve position: " + Pump.GetOpenValvePosition() );
                Log.Debug( "Now: " + Log.DateTimeToString(DateTime.UtcNow) + " End Time: " + Log.DateTimeToString(endTime) );

                // Print out the elapsed time.
                timeElapsed = DateTime.UtcNow - ( endTime - timeOut );
            }
            catch ( Exception e )
            {           
                Log.Error( "PreconditionSensor", e );
            }
            finally
            {
                //Ignore if bad pump tubing is detected during preconditioning.
                //Set Pump.IsBadPumpTubing to false.
                Pump.IsBadPumpTubing = false;

                // Put response's status back to what it was when this method was called.
                response.Status = originalStatus;
            }
            return DateTime.UtcNow - startTime;
        }

        // SGF  Jan-13-2009  DSW-173
        /// <summary>
        /// Pause gas flow for an amount of time dependent on the sensor type.
        /// </summary>
		public void PauseGasFlow( GasEndPoint gasEndPoint, Sensor sensor, SensorGasResponse response )
        {
            // Adding code to test whether this is a precondition for calibration or bump.  Then,
            // determine what is the appropriate pause between preconditioning and gas test, and
            // then pause for that period of time.
            long pauseInSeconds =  ( response.Type == GasResponseType.Calibrate )
                ? sensor.CalPreconditionPauseTime : sensor.BumpPreconditionPauseTime;
            PauseGasFlow(gasEndPoint, pauseInSeconds);
        }

        /// <summary>
        /// Pause gas flow for a specified amount of time.
        /// </summary>
        /// <remarks>
        /// The pause is typically a minute or two depending on sensor type.
        /// </remarks>
		public virtual void PauseGasFlow( GasEndPoint gasEndPoint, long pauseInSeconds )
        {
            if ( pauseInSeconds <= 0 )
            {
                if ( !Controller.IsDocked() )
                    throw new InstrumentNotDockedException();
                return;
            }

            Log.Debug( "PauseGasFlow: " + pauseInSeconds + " seconds" );

            if ( AccessoryPump == AccessoryPumpSetting.Installed )
            {
                EnablePump( false ); // Turn off instrument pump before closing valve, to prevent Pump Fault.
                Thread.Sleep( 2000 ); // give the instrument a moment to turn off the pump. (There is often a slight delay with MX6)
            }
            Pump.SetDesiredFlow(0);
            Pump.CloseGasEndPoint(gasEndPoint);

            // while we pause, keep polling the cradle in case user undocks the instrument.
            for ( int i = 1; i <= pauseInSeconds; i++ )
            {
                Thread.Sleep( 1000 );
                if ( !Controller.IsDocked() )
                    throw new InstrumentNotDockedException();
            }
        }

        /// <summary>
        /// clear the instrument sensor peak reading.
        /// For oxygen sensor it will be set to 20.9
        /// </summary>
        /// <param name="installedComponents">List of sensors to instrument</param>
        public void ClearInstrumentSensorPeaks(List<InstalledComponent> installedComponents)
        {
            // For each sensor find its calibration order.
            foreach (InstalledComponent comp in installedComponents)
            {
                if (!(comp.Component is Sensor))
                    continue;

                Sensor sensor = (Sensor)comp.Component;

                if (!(sensor.Enabled))
                    continue;

                ClearSensorPeaks((Sensor)comp.Component, comp.Position);
            }
        }

        /// <summary>
        /// Set the sensor's calibration gas concentration.
        /// </summary>
        /// <param name="installedComponent">.</param>
        /// <param name="endPoint">The gas end point that contains the gas.</param>
		public virtual double SetCalibrationGasConcentration( InstalledComponent installedComponent, GasEndPoint endPoint )
        {
            const string func = "SetCalibrationGasConcentration: ";

            Sensor sensor = (Sensor)installedComponent.Component;
            string gasCode = sensor.CalibrationGas.Code;
            MeasurementType sensorMeasurementType = ((SensorType)sensor.Type).MeasurementType;

            double lelMultiplier = GasType.Cache[gasCode].LELMultiplier;

            double availableConcentration = 0.0;

            // Determine the gas concentration of the gas to use.
			foreach ( GasConcentration gasCon in endPoint.Cylinder.GasConcentrations )
            {
                if (gasCon.Type.Code == gasCode)
                {
                    availableConcentration = gasCon.Concentration;
                    break;
                }
                if (gasCode == GasCode.O2 && gasCon.Type.Code == GasCode.FreshAir)
                {
                    availableConcentration = 209000;
                    break;
                }
            }

            // If we didn't find anything with the gas.
            if (availableConcentration == 0.0)
                return availableConcentration;

            Log.Debug(string.Format("{0}Sensor \"{1}\" ({2}) CalGas={3}, Conc={4}, Res={5}",
                func, installedComponent.Component.Uid, installedComponent.Component.Type.Code,
                gasCode, sensor.CalibrationGasConcentration, sensor.Resolution));

            // Check the measurement type for how to multiply the concentration.
            if (sensorMeasurementType == MeasurementType.LEL)  // %lel
            {
                Log.Debug(string.Format("{0}Converting {1} cylinder concentration from {2}PPM to %%LEL ({3} * {4})",
                    func, gasCode, availableConcentration, availableConcentration, lelMultiplier));
                availableConcentration *= lelMultiplier;
                availableConcentration = Controller.Round(availableConcentration, 0);
            }
            else if (sensorMeasurementType == MeasurementType.VOL)  // %vol
            {
                Log.Debug(string.Format("{0}Converting {1} cylinder concentration from {2}PPM to %%VOL ({3} / 10000)",
                    func, gasCode, availableConcentration, availableConcentration));
                availableConcentration /= 10000;
            }
            // else, endPoint.MeasurementType == PPM

            Log.Debug(string.Format("{0}{1} Cylinder has concentration of {2}", func, gasCode, availableConcentration ));

            // If sensor is %vol, and it has a zero resolution, then we want to round the concentration
            // up to the next integer value.  e.g., if cylinder contains 2.1% gas, then we want to round 
            // it to 3.
            if (sensorMeasurementType == MeasurementType.VOL && sensor.Resolution == 1.0)
            {
                Log.Debug(string.Format("{0}Sensor is %VOL and has resolution of zero decimals. Rounding {1} up to next integer",
                    func, availableConcentration));
                availableConcentration = Math.Ceiling(availableConcentration);
            }
            else
                availableConcentration = Controller.Round(availableConcentration, 2);

            if (availableConcentration == sensor.CalibrationGasConcentration)
            {
                Log.Debug(string.Format("{0}Sensor already set to proper concentration ({1})", func, availableConcentration));
                return availableConcentration; // Its the correct concentration.
            }

            Log.Debug(string.Format("{0}SETTING SENSOR FROM CONCENTRATION {1} TO {2}, (res={3})", func, sensor.CalibrationGasConcentration, availableConcentration, sensor.Resolution));

            // Set the sensor's calibration gas concentration.
            SetSensorCalGasConcentration(installedComponent.Position, availableConcentration, sensor.Resolution);

            Log.Debug(string.Format("{0}NEW CONCENTRATION: {1}", func, GetSensorCalGasConcentration(installedComponent.Position, sensor.Resolution)));

            // Update the sensor.
            //sensor.CalibrationGas.LELMultiplier = lelMultiplier;
            sensor.CalibrationGasConcentration = availableConcentration;

            return availableConcentration;
        }

        // SGF  11-Oct-2010  INS-1189
        /// <summary>
        /// Set the sensor's calibration gas concentration.
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <param name="concentration">The concentration value to assign to the sensor.</param>
        public virtual void SetCalibrationGasConcentration(InstalledComponent installedComponent,double concentration, bool isInstrumentCalEvent)
        {
            const string func = "SetCalibrationGasConcentration(double): ";

            Sensor sensor = (Sensor)installedComponent.Component;

            // Do not check for the below condition if this is a Bump event - INETQA-4189 v7.6
            if (isInstrumentCalEvent && concentration == sensor.CalibrationGasConcentration)
            {
                Log.Debug(string.Format("{0}Sensor already set to proper concentration ({1})", func, concentration));
                return; // Its the correct concentration.
            }

            // Set the sensor's calibration gas concentration.
            Log.Debug(string.Format("{0}SETTING SENSOR FROM CONCENTRATION {1} TO {2}, (res={3})", func, isInstrumentCalEvent ? sensor.CalibrationGasConcentration 
                : GetSensorCalGasConcentration(installedComponent.Position, sensor.Resolution) , concentration, sensor.Resolution));
            SetSensorCalGasConcentration(installedComponent.Position, concentration, sensor.Resolution);

            // Update the sensor.
            Log.Debug(string.Format("{0}NEW CONCENTRATION: {1}", func, GetSensorCalGasConcentration(installedComponent.Position, sensor.Resolution)));
            sensor.CalibrationGasConcentration = concentration;
        }

		/// <summary>
		/// Gets the current operating mode of the instrument.
		/// </summary>
		/// <returns></returns>
		public OperatingMode GetOperatingMode()
		{
			return Driver.getOperatingMode();
		}

		/// <summary>
		/// Indicates the amount of time that will elapse from when no motion is first detected, until the OOM 
		/// ("Out-of-Motion") Warning is initiated.
		/// </summary>
		/// <returns>int.MinValue if the instrument does not support the "OOM</returns>
		public int GetOomWarningInterval()
		{
			return Driver.Definition.HasOomWarningIntervalConfigFeature ? Driver.getOomWarningInterval() : int.MinValue;
		}

		/// <summary>
		/// Sets the frequency at which the instrument will go into alarm if it remains still.
		/// </summary>
		/// <param name="oomWarningInterval">The interval in seconds.</param>
		public void SetOomWarningInterval( int oomWarningInterval )
		{
			if ( Driver.Definition.HasOomWarningIntervalConfigFeature )
			{
				Driver.setOomWarningInterval( oomWarningInterval );
			}
		}

		/// <summary>
		/// Indicates the frequency at which the instrument wants to be docked so its datalog can be downloaded.
		/// </summary>
		/// <returns>int.MinValue if the instrument does not support "dock interval"</returns>
		public int GetDockInterval()
		{
			return Driver.Definition.HasDockIntervalConfigFeature ? Driver.getDockInterval() : int.MinValue;
		}

		/// <summary>
		/// Indicates the frequency at which the instrument wants its datalog to be cleared.
		/// </summary>
		/// <param name="dockInterval">The interval in days.</param>
		public void SetDockInterval( int dockInterval )
		{
			if ( Driver.Definition.HasDockIntervalConfigFeature )
			{
				Driver.setDockInterval( dockInterval );
			}
		}

        /// <summary>
        /// Indicates the number of minutes between the sounding of bump, cal or dock overdue indicators
        /// </summary>
        /// <returns>Maintenance interval in minutes, int.MinValue if the instrument does not support "maintenance interval"</returns>
        public int GetMaintenanceInterval()
        {
            return Driver.Definition.HasMaintenanceIndicatorIntervalConfigFeature ? Driver.getMaintenanceIndicatorInterval() : int.MinValue;
        }

        /// <summary>
        /// Sets the number of minutes between the sounding of bump, cal or dock overdue indicators
        /// </summary>
        /// <param name="maintenanceInterval">The interval in days.</param>
        public void SetMaintenanceInterval( int maintenanceInterval )
        {
            if ( Driver.Definition.HasMaintenanceIndicatorIntervalConfigFeature )
            {
                Driver.setMaintenanceIndicatorInterval( maintenanceInterval );
            }
        }

        /// <summary>
        /// Indicates the frequency at which the instrument wants to be calibrated.
        /// </summary>
        /// <returns>short.MinValue if the instrument does not support "calibration interval"</returns>
        public short GetCalibrationInterval()
        {
            return Driver.Definition.HasCalIntervalConfigFeature ? Driver.getCalibrationInterval() : short.MinValue;
        }

        /// <summary>
        /// Sets the frequency at which the instrument wants to be calibrated.
        /// </summary>
        /// <param name="calInterval">The interval in days of when the instrument will want to be calibrated next.</param>
        public void SetCalibrationInterval( short calInterval )
        {
            if ( Driver.Definition.HasCalIntervalConfigFeature )
            {
                Driver.setCalibrationInterval( calInterval );
            }
        }

        /// <summary>
        /// Indicates the frequency at which the instrument wants to be bumped.
        /// </summary>
        /// <returns>double.MinValue if the instrument does not support "bump interval"</returns>
        public double GetBumpInterval()
        {
            return Driver.Definition.HasBumpIntervalConfigFeature ? Driver.getBumpInterval() : double.MinValue;
        }

        /// <summary>
        /// Sets the frequency at which the instrument wants to be bumped.
        /// </summary>
        /// <param name="bumpInterval">The interval in days of when the instrument will want to be bumped next.</param>
        public void SetBumpInterval( double bumpInterval )
        {
            if ( Driver.Definition.HasBumpIntervalConfigFeature )
            {
                Driver.setBumpInterval( bumpInterval );
            }
        }

        /// <summary>
        /// Indicates what gas reading (% of concentration) that instrument needs to
        /// see in order for a bump test to pass.
        /// </summary>
        /// <returns>int.MinValue if instrument does not support the concept of "bump threshold"; </returns>
        public int GetBumpThreshold()
        {
            return Driver.Definition.HasBumpThresholdConfigFeature ? Driver.getBumpThreshold() : int.MinValue;
        }

        /// <summary>
        /// Specifies what gas reading (% of concentration) that instrument needs to
        /// see in order for a bump test to pass.
        /// </summary>
        /// <param name="threshold"></param>
        public void SetBumpThreshold( int threshold )
        {
            if ( Driver.Definition.HasBumpThresholdConfigFeature )
                Driver.setBumpThreshold( threshold );
        }

        /// <summary>
        /// Indicates maximum amount of time that a bump test can run
        /// before it times out and fails.
        /// </summary>
        /// <returns>int.MinValue if instrument does not support the concept of "bump timeout"</returns>
        public int GetBumpTimeout()
        {
            return Driver.Definition.HasBumpTimeoutConfigFeature ? Driver.getBumpTimeoutSeconds() : int.MinValue;
        }

        /// <summary>
        /// Specifies maximum amount of time that a bump test can run
        /// before it times out and fails.
        /// </summary>
        /// <param name="timeout"></param>
        public void SetBumpTimeout( int timeout )
        {
            if ( Driver.Definition.HasBumpTimeoutConfigFeature )
                Driver.setBumpTimeoutSeconds( timeout );
        }

        /// <summary>
        /// Indicates if sensor is enabled or disabled. By default, all sensors 
        /// are enabled and cannot be disabled.  Therefor this base class
        /// implementation does nothing.
        /// Other instruments, namely mx6, may override this, though, if they
        /// support enabling/disabling of sensors.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>
        /// This base class implementation always returns true.
        /// ....Because by default, all sensors are enabled and cannot be disabled.
        /// Other instruments, namely mx6, may override this, though, if they
        /// support enabling/disabling of sensors.</returns>
        public virtual void EnableSensor( int pos, bool enabled ) {}

        /// <summary>
        /// Returns whether or not this instrument has an accessory pump that's current attached.
        /// </summary>
        public virtual AccessoryPumpSetting AccessoryPump
        { 
            get 
            { 
                // By default, we return Unknown.  Instrument types
                // that actually have accessory pumps can override this
                // default behavior to return something more appropriate.
                return AccessoryPumpSetting.NotApplicable;
            } 
        }

        public virtual void TurnOnSensors( bool turnOn, bool wait )
        {
            Driver.turnOnSensors( turnOn, wait );
        }

        /// <summary>
        /// Pause or unpause the specified sensor.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="paused"></param>
        public virtual void PauseSensor( int pos, bool paused )
        {
        }

        /// <summary>
        /// Indicates if sensor is enabled or disabled.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns>
        /// This base class implementaiton always returns true.
        /// By default, all sensors are enabled and cannot be disabled.
        /// Other instruments, namely mx6, may override this, though, if they
        /// support enabling/disabling of sensors.</returns>
        public virtual bool IsSensorEnabled( int pos )
        {
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="gasEndPoint">
        /// The end point providing the fresh air / zero air.</param>
        /// <returns>
        /// True if instrument finished zeroing before IDS times out waiting.
        /// False if instrument hasn't finished when IDS times out waiting.
        /// DEBUG: THIS IS NOT INDICATING IF ZEROING WAS SUCCESSFUL OR NOT.
        /// IT'S IS MERELY FLAGGING WHETHER THE INSTRUMENT 'FINISHED'
        /// IT'S ZEROING TASK OR NOT.  BECAUSE IT'S 'FINISHED' DOES NOT MEAN THAT
        /// THAT ZEROING WAS SUCCESSFUL.  CALLER NEEDS TO CHECK ZEROING STATUS
        /// TO FIND OUT WHAT REALLY HAPPENED.
        /// </returns>
		public virtual bool ZeroSensors( GasEndPoint gasEndPoint )
        {
            if ( !Controller.IsDocked() )
            {
                Log.Debug( "ZEROING: Can't begin zeroing. Instrument Undocked" );
                return false;
            }

            TimeSpan zeroingTimeoutPeriod = new TimeSpan( 0 , 0 , 90 ); // 90 seconds

            Log.Debug( "ZEROING: Beginning instrument zeroing" );

            try
            {

                this.BeginInstrumentZero();

                DateTime startTime = DateTime.UtcNow;

                int polls = 0;

                while ((DateTime.UtcNow - startTime) < zeroingTimeoutPeriod)
                {
                    int sleepInterval = (++polls == 1) ? 15 : 5; // seconds

                    Log.Debug(string.Format("ZEROING: Waiting {0} seconds.", sleepInterval));

                    Thread.Sleep(sleepInterval * 1000); // Sleep first.

                    if ( Pump.GetOpenValvePosition() <= 0 )
                        throw new FlowFailedException( gasEndPoint );

                    Log.Debug("ZEROING: Checking zeroing status...");

                    // watch for user undocking while we're sleeping.
                    if (!Controller.IsDocked())
                    {
                        Log.Debug("ZEROING: Instrument Undocked!");
                        return false;
                    }

                    if (!this.IsInstrumentZeroing()) // Monitor the zeroing status.
                    {
                        Log.Debug("ZEROING: Instrument is finished.");
                        return true;
                    }
                }

                Log.Debug("ZEROING: TIMED OUT by IDS after " + zeroingTimeoutPeriod.TotalSeconds + " seconds!");
                return false;
            }
            finally // SGF  14-Jun-2011  INS-1732
            {
                try
                {
                    if ( Controller.IsDocked() )
                    {
                        this.EndInstrumentZero();
                        Thread.Sleep( 1000 ); // Do we really need this? 
                    }
                }
                catch ( Exception e )
                {
                    Log.Debug( e.ToString() );
                }
            }
        }

        /// <summary>
        /// Returns the instrument's country of origin.
        /// </summary>
        /// <returns>
        /// This defualt implementation, intended for instrument that have no "origin country",
        /// simply returns returns an empty string to denote that.
        /// </returns>
        public virtual string GetCountryOfOriginCode() { return string.Empty; }

     
        /// <summary>
        /// Delegate for AbortRequest.
        /// </summary>
        /// <returns></returns>
        private bool IsNotDocked()
        {
            // We're OK as lo
            return Controller.IsDocked() == false;
        }

        /// <summary>
        /// Returns type of instrument.
        /// </summary>
        public DeviceType GetInstrumentType()
        {
            EquipmentType instType = Driver.getEquipmentType();
			
			if (Configuration.DockingStation.Type == DeviceType.MX4 && instType == EquipmentType.MX4)
                return DeviceType.MX4;

			if ( Configuration.DockingStation.Type == DeviceType.MX4 && instType == EquipmentType.VentisPro )
				return DeviceType.VPRO;

			if ( Configuration.DockingStation.Type == DeviceType.MX6 && instType == EquipmentType.MX6 )
				return DeviceType.MX6;

			if ( Configuration.DockingStation.Type == DeviceType.SC && instType == EquipmentType.SafeCore )
				return DeviceType.SC;

            if (Configuration.DockingStation.Type == DeviceType.TX1 && instType == EquipmentType.TX1)
                return DeviceType.TX1;

			if ( Configuration.DockingStation.Type == DeviceType.GBPRO && instType == EquipmentType.GasBadgePro )
				return DeviceType.GBPRO;

			if ( Configuration.DockingStation.Type == DeviceType.GBPLS && instType == EquipmentType.GasBadgePlus )
				return DeviceType.GBPLS;

            Log.Warning( "GetInstrumentType: Unexpected type detected: \"" + instType.ToString() + "\"" );

            return DeviceType.Other;
        }

		/// <summary>
		/// Returns sub type of instrument.  If instrument does not have sub types, DeviceSubType.NONE should be returned.
		/// </summary>
		public DeviceSubType GetInstrumentSubtype()
		{
			EquipmentSubType instSubType = Driver.getEquipmentSubType();

			if ( instSubType == EquipmentSubType.None )
				return DeviceSubType.None;

			if ( instSubType == EquipmentSubType.VentisPro5 )
				return DeviceSubType.VentisPro5;

			if ( instSubType == EquipmentSubType.VentisPro4 )
				return DeviceSubType.VentisPro4;

			if ( instSubType == EquipmentSubType.Mx4Ventis )
				return DeviceSubType.Mx4Ventis;

			if ( instSubType == EquipmentSubType.Mx4iQuad )
				return DeviceSubType.Mx4iQuad;

			if ( instSubType == EquipmentSubType.Mx4VentisLs || instSubType == EquipmentSubType.Mx4Scout )
				return DeviceSubType.Mx4VentisLs;

			Log.Warning( "GetInstrumentSubtype: Unexpected subtype detected: \"" + instSubType.ToString() + "\"" );

			return DeviceSubType.Undefined;
		}

        #region IModbusTracer implementation

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.DebugLevel property.
        /// If tracing isn't enabled, then set return a debuglevel of Warning.
        /// That will trigger the low level driver to not bother with the effort
        /// of formatting up all of it's debug messages just to not have them 
        /// even outputted.
        /// </summary>
        public DebugLevel DebugLevel
        {
            get
            {
                return Log.Level >= LogLevel.Trace ? DebugLevel.Debug : DebugLevel.Warning;
            }
        }

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.WriteError
        /// </summary>
        /// <param name="msg"></param>
        public void WriteError( string msg )
        {
            if ( DebugLevel >= DebugLevel.Error )
                Log.Error( msg );
        }

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.WriteDebug
        /// </summary>
        /// <param name="msg"></param>
        public void WriteWarning( string msg )
        {
            if ( DebugLevel >= DebugLevel.Warning )
                Log.Warning( msg );
        }

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.WriteDebug
        /// </summary>
        /// <param name="msg"></param>
        public void WriteDebug( string msg )
        {
            if ( DebugLevel >= DebugLevel.Debug )
                Log.Debug( msg );
        }

        #endregion IModbusTracer implementation

        /// <summary>
        /// Turn off (shut down) the instrument.
        /// </summary>
        public virtual void TurnOff()
        {
            try
            {
                Driver.shutdown();
            }
            catch ( Exception e )
            {
                Log.Error( "TurnOff - ignoring exception", e );
            }
        }

        /// <summary>
        /// Returns this instrument's part number.
        /// </summary>
        public string GetPartNumber()
        {
            return Driver.getInstrumentPartNumber();
        }

        /// <summary>
        /// Retrieve the instrument serial number.
        /// </summary>
        /// <returns>Instrument serial number</returns>
        public string GetSerialNumber()
        {
            // If instrument is unserialized, it will come back with a
            // serial number full of tildes.  Strip these out as
            // other logic in IDS code looks for empty serial number to 
            // denote unserialized instrument.
            string serial = Driver.getInstrumentSerialNumber();

            // If serial number has never been programmed into the instrument, then the 
            // memory locations containing the characters for it will/may be uninitialized
            // to 0xFF.  In this case, we need to return an empty string since that's
            // how DS2 identifies an unserialized instrument.

            bool uninitialized = true;
            for ( int i = 0; uninitialized == true && i < serial.Length; i++ )
                uninitialized = ( serial[i] == 0xff );

            return uninitialized ? string.Empty : serial.ToUpper();
        }

        /// <summary>
        /// Returns this instrument's job number.
        /// </summary>
        public string GetJobNumber()
        {
            return Driver.getJobNumber();
        }

        /// <summary>
        /// Retrieves the instrument software version.
        /// </summary>
        /// <returns>Formatted software version</returns>
        public string GetSoftwareVersion()
        {
            return Driver.getSoftwareVersion();
        }

		/// <summary>
		/// Retrieves the instrument's bootloader version.
		/// </summary>
		/// <returns>Formatted software version.</returns>
		public string GetBootloaderVersion()
		{
			// Driver will return an empty string if not supported by the instrument.
			return Driver.getBootloaderVersion();
		}

        /// <summary>
        /// Retrieves the instrument hardware version.
        /// </summary>
        /// <returns>Formatted hardware version</returns>
        public string GetHardwareVersion()
        {
            string ver = Driver.getHardwareVersion().ToString();

            // driver will return zero if instrument has no programmed setupversion. So,
            // if it has no programmed setupversion, then we should return nothing, not "0".
            return ver == "0" ? string.Empty : ver;
        }

        /// <summary>
        /// Retrieves the instrument's set-up technician's initials.
        /// </summary>
        /// <returns>Setup tech</returns>
        public string GetSetupTech()
        {
            // If instrument is unserialized, it will come back with a
            // serial number full of tildes. 
            return Driver.getSetupTech();
        }

        /// <summary>
        /// Retrieves the instrument's set-up date as mmddyy.
        /// </summary>
        /// <returns>Setup date</returns>
        public DateTime GetSetupDate()
        {
            // SetupDate uploaded to ds2/iNet is expected to be the original manufacture
            // date which never changes.
            return Driver.getMfgDate();
        }

        /// <summary>
        /// Retrieves the configuration software version used to setup this instrument.
        /// </summary>
        /// <returns>Setup date</returns>
        public string GetSetupVersion()
        {	
	        string setupVersion = Driver.getConfigurationVersion().ToString();

            // driver will return zero if instrument has no programmed setupversion. So,
            // if it has no programmed setupversion, then we should return nothing, not "0".
            return setupVersion == "0" ? string.Empty : setupVersion;
        }

        /// <summary>
        /// Retrieves the current time stored in the instrument.
        /// </summary>
        /// <returns>A DateTime holding the current instrument time.</returns>
        public DateTime GetTime()
        {
            return Driver.getTime();
        }

        /// <summary>
        /// Sets the instrument's current time to the supplied time.
        /// </summary>
        /// <param name="newTime">The time to set the instrument's clock to.</param>
        public void SetTime( DateTime newTime )
        {
            Driver.setTime( newTime );
        }

        /// <summary>
        /// Get the instrument's active site.
        /// </summary>
        /// <returns>The active site.</returns>
        public string GetActiveSite()
        {
            return Driver.getActiveSite();
        }

		/// <summary>
		/// Gets the instrument's company name. (a.k.a. active company)
		/// </summary>
		/// <returns>An empty string if not supported.</returns>
		public string GetCompanyName()
		{
			if ( HasCompanyNameFeature )
			{
				return Driver.getActiveCompany(); 
			}

			return String.Empty;
		}

		/// <summary>
		/// Sets the instrument's company name. (a.k.a. active company)
		/// </summary>
		/// <param name="companyName"></param>
		public void SetCompanyName( string companyName )
		{
			if ( HasCompanyNameFeature )
			{
				Driver.setActiveCompany( companyName );
			}
		}

		/// <summary>
		/// Gets the instrument's company message.
		/// </summary>
		/// <returns>A single company message string where lines will be separated by a '|' character.</returns>
		public string GetCompanyMessage()
		{
			if ( HasCompanyMessageFeature )
			{
				// .ToList() is an extension method provided by System.Linq
				return Utility.JoinStrings( Driver.getCompanyMessage().ToList() );
			}

			return string.Empty;
		}

		/// <summary>
		/// Sets the instrument's company message.
		/// </summary>
		/// <param name="companyMessage">The lines of company message to set on the instrument.</param>
		public void SetCompanyMessage( string companyMessage )
		{
			if ( HasCompanyMessageFeature )
			{
				// The driver will blank out if our list is not big enough.
				// The driver will ignore lines if our list is too big.
				Driver.setCompanyMessage( Utility.SplitString( companyMessage ) );
			}
		}

        /// <summary>
        /// Set the instrument's active site.
        /// </summary>
        /// <param name="site"></param>
        public void SetActiveSite( string site )
        {
            Driver.setActiveSite( site );
        }

        /// <summary>
        /// Get the user at a specified position.
        /// </summary>
        /// <returns>The user at a specified position.</returns>
        public string GetActiveUser()    
        {
            return Driver.getActiveUser(); 
        }

        /// <summary>
        /// Set an instrument's user at a specific position.
        /// </summary>
        /// <param name="user">The user to set the position to.</param>
        public void SetActiveUser( string user )    
        {
            Driver.setActiveUser( user );
        }

        /// <summary>
        /// Retrieves the instrument's security code (Access Code).
        /// </summary>
        /// <returns>Access code</returns>
        public string GetAccessCode()    
        {
            // Does instrument support having security codes?
            if ( Driver.Definition.SupportedSecurityCodeTypes == SecurityCodeTypes.None )
                return string.Empty;

            return Driver.getSecurityCode();
        }

        /// <summary>
        /// Sets the instrument's security code (Access Code).
        /// </summary>
        /// <param name="accessCode">Access code</param>
        public void SetAccessCode( string accessCode )    
        {
            // Does instrument support having security codes?
            if ( Driver.Definition.SupportedSecurityCodeTypes == SecurityCodeTypes.None )
                return;

            Driver.setSecurityCode( accessCode );
        }

        /// <summary>
        /// Retrieves the instrument's total operation time.
        /// </summary>
        /// <returns>Total operation time</returns>
        public TimeSpan GetTotalRunTime()    
        {
            if ( !Driver.Definition.HasTotalRunTimeFeature )
                return TimeSpan.MinValue;

            return Driver.getTotalRunTime();
        }

        /// <summary>
        /// Retrieve the number of sensors the instrument is capable of holding.
        /// </summary>
        /// <returns>Sensor capacity</returns>
        public int GetSensorCapacity()    
        {
            return Driver.Definition.MaxSensorCapacity;
        }

        /// <summary>
        /// Retrieves the instrument's current TWA Time Base.
        /// </summary>
        /// <returns>TWA Time Base (hours)</returns>
        public int GetTwaTimeBase()    
        {
            return Driver.Definition.HasTwaFeature ? Driver.getTWATimeBase() : int.MinValue;
        }

        /// <summary>
        /// Sets the instrument's TWA Time Base.
        /// </summary>
        /// <param name="sensorPos"></param>
        /// <param name="twaValue">TWA value (hours)</param>
        public void SetTwaTimeBase( int twaBaseHours )    
        {
            if ( Driver.Definition.HasTwaFeature )
                Driver.setTWATimeBase( twaBaseHours );
        }

        /// <summary>
        /// </summary>
        /// <returns>Array will be empty if instrument performed zero diagnostics.  This method 
        /// will never return null.</returns>
        public virtual GeneralDiagnosticProperty[] GetGeneralDiagnosticProperties()
        {
            InstrumentDiagnostic[] driverDiagnostics = Driver.getInstrumentDiagnostics();

            GeneralDiagnosticProperty[] generalDiagnosticProperties = new GeneralDiagnosticProperty[ driverDiagnostics.Length ];

            for ( int i = 0; i < driverDiagnostics.Length; i++ )
            {
                InstrumentDiagnostic instrumentDiagnostic = driverDiagnostics[i];
                generalDiagnosticProperties[i]
                    = new GeneralDiagnosticProperty( instrumentDiagnostic.Code.ToString(),
                                                     instrumentDiagnostic.Result.ToString() );
            }  // end-for

            return generalDiagnosticProperties;
        }

        /// <summary>
        /// Get the Instrument's error codes
        /// </summary>
        /// <returns>
		/// Array of ErrorDiagnostic objects represents all stored errors
        /// on the instrument. Each ErrorDiagnostic object will have both an error code
        /// and timestamp of when the error occurred.
        /// An empty array (not null!) is returned if no errors found on the instrument.
		/// </returns>
        public virtual ErrorDiagnostic[] GetInstrumentErrors()    
        {
            ISC.Instrument.Driver.InstrumentError[] driverErrors = Driver.getInstrumentErrors();

            Log.Debug( string.Format( "{0} InstrumentErrors downloaded", driverErrors.Length ) );

            ErrorDiagnostic[] errors = new ErrorDiagnostic[ driverErrors.Length ];

			ErrorCategory category;
            for ( int i = 0; i < driverErrors.Length; i++ )
            {
				// the category describes if the error code applies to the instrument or the base unit (if base units are applicable)
				category = driverErrors[i].Category == InstrumentErrorCategory.BaseUnit ? ErrorCategory.BaseUnit : ErrorCategory.Instrument;

				errors[i] = new ErrorDiagnostic( driverErrors[i].Code, driverErrors[i].Timestamp, category, driverErrors[i].BaseUnitSerialNumber );
            }

            return errors;
        }

        /// <summary>
        /// Clears all the instrument errors 
        /// </summary>
        public virtual void ClearInstrumentErrors()
        {
            Log.Debug("Clearing instrument errors");

            Driver.clearInstrumentErrors();
        }

        /// <summary>
        /// Retrieve the number of 'new' (non-downloaded) datalog sessions
        /// currently stored on the instrument.
        /// </summary>
        /// <returns>The number of datalog sessions</returns>
        protected int GetDatalogSessionCount()
        {
            return Driver.getHygieneSessionCount();
        }

        /// <summary>
        /// Retrieves all datalog sessions from the docked instrument.
        /// </summary>
        /// <param name="corruptDatalogDetected">Set to true by this method if corrupted data was detected for any of the sessions.</param>
        /// <returns></returns>
        public virtual List<DatalogSession> GetDatalog( out bool corruptDatalogDetected )    
        {
            const int MAX_ATTEMPTS = 3;
            int communicationAttempt = 1;
            DateTime startTime = DateTime.UtcNow;
            ArrayList driverSessionList = null;

            while ( true )
            {
                corruptDatalogDetected = false;

                try
                {
                    Log.Debug( string.Format( "DATALOG: Downloading from instrument. (Attempt {0} of {1})...", communicationAttempt, MAX_ATTEMPTS ) );
                    driverSessionList = Driver.getHygiene();

                    if ( Driver.HygieneCorrupt == true )  // INS-3282 - has driver flagged any datalog as corrupt?
                        corruptDatalogDetected = true;

                    break;
                }
                catch ( CorruptHygieneException chex )
                {
                    corruptDatalogDetected = true;
                    Log.Error( "DATALOG: Aborting due to CorruptHygieneException", chex );
                    return new List<DatalogSession>();
                }
                catch ( CommunicationAbortedException cae )
                {
                    throw new InstrumentNotDockedException( cae );
                }
                catch ( Exception e )
                {
                    // was the error caused by undocking the instrument?
                    // Can this ever happen without getting CommunicationAbortedException ? 
                    if ( !Controller.IsDocked() ) 
                        throw new InstrumentNotDockedException();

                    Log.Error( string.Format( "DATALOG: Error downloading from instrument during attempt {0} of {1}!", communicationAttempt, MAX_ATTEMPTS ), e );

                    // if we get CommunicationException, then retry the download all over again.
                    // If it's not a CommunicationException, then something else is wrong and we don't bother
                    // to retry, under assumption that same exception will be thrown.
                    if ( ( ( e is CommunicationException ) && ( ++communicationAttempt > MAX_ATTEMPTS ) )
                    || !( e is CommunicationException ) )
                    {
                        // As shown in tech support issue INS-3603, sometimes a GasBadge will fail to communicate 
                        // due to corrupt datalog and a CommunicationException will be thrown, and not a CorruptHygiene exception.
                        if ( this is GBPRO )
                        {
                            Log.Error( "DATALOG: Assuming GBPRO datalog is corrupt!" );
                            corruptDatalogDetected = true;
                            driverSessionList = new ArrayList(); // need to initialize as empty so go below doesn't throw accessing it.
                            break;
                        }
                        // Otherwise, we're not a GBPRO, so we just rethrow. The datalog
                        // shouldn't be corrupt, but for some reason we can't download it.
                        throw; 
                    }

                } // end-catch Exception

            } // end-while

            Log.Debug( "DATALOG: Downloaded & Parsed " + driverSessionList.Count + " sessions in " + ( DateTime.UtcNow - startTime ) );

            List<DatalogSession> sessionList = new List<DatalogSession>( driverSessionList.Count );

            int totalSessionCount = driverSessionList.Count;

            // Processsing of the data returned by the driver could take more than a moment 
            // if there's a significant amount of it.  So, we lower the thread priority to 
            // prevent the processing  of the data from hogging the CPU.
            ThreadPriority originalPriority = Thread.CurrentThread.Priority;
            if ( driverSessionList.Count > 0 && originalPriority > ThreadPriority.BelowNormal )
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            try
            {
                while ( driverSessionList.Count > 0 )
                {
                    Log.Debug( "DATALOG: Converting data model for Session " + ( sessionList.Count + 1 ) + "/" + totalSessionCount );

                    DateTime convertStartTime = DateTime.UtcNow;

                    // There can potentially be a hundreds of sessions in in the arraylist so it behooves
                    // us to remove them from the arraylist and toss them away as we process them.  
                    DriverInstrumentDatalogSession driverSession = (DriverInstrumentDatalogSession)driverSessionList[0];
                    driverSessionList.RemoveAt( 0 );

                    if ( driverSession.SerialNumber == string.Empty )
                    {
                        Log.Warning( "DATALOG: Session with no serial number encountered.  Assuming instrument had a corrupted session header." );
                        corruptDatalogDetected = true;
                        continue;
                    }

                    DatalogSession session
                        = new DatalogSession( driverSession.SerialNumber.ToUpper(), driverSession.Timestamp /* <-- 'session' is a datetime */ );

                    session.CorruptionException = driverSession.CorruptionException;

                    corruptDatalogDetected = corruptDatalogDetected || ( driverSession.CorruptionException != null );

                    if ( driverSession.CorruptionException != null && session.SensorSessions.Count == 0 )
                        Log.Warning( "Corrupt session has no SensorSessions." );
					
					session.SessionNumber = driverSession.SessionNumber;
					session.RecordingInterval = driverSession.RecordingInterval;
					session.TWATimeBase = driverSession.TwaTimeBase;
					session.Comments = driverSession.Comments;
					session.BaseUnitSerialNumber = driverSession.BaseUnitSerialNumber;

                    // Locations will often be duplicated.  Since there's a potential to have a few thousand
                    // sessions, try and make user string references point to the same string to save memory.
                    if ( _lastDatalogUser != driverSession.User )
                        _lastDatalogUser = driverSession.User;
                    session.User = _lastDatalogUser;

                    Log.Debug( "DATALOG: Session Num=" + session.SessionNumber + " (" + Log.DateTimeToString(session.Session) + ")" );
                    Log.Debug( "DATALOG: RecInterval=" + session.RecordingInterval + ", TwaTimeBase=" + session.TWATimeBase + ", User=\"" + session.User + "\"" );
                    Log.Debug( "DATALOG: Comments=\"" + session.Comments + "\"" );

                    for ( int sensorSessionIndex = 0; sensorSessionIndex < driverSession.SensorSessions.Length; sensorSessionIndex++ )
                    {
                        DriverInstrumentSensorSession driverSensorSession = driverSession.SensorSessions[sensorSessionIndex];

                        GasType gasType = DomainModel.GasType.Cache[ driverSensorSession.GasCode ];

                        // SensorCode will be empty for gbpro.
                        string sensorCode = ( driverSensorSession.SensorCode == string.Empty ) ? GasCode2SensorCode(gasType.Code) : driverSensorSession.SensorCode;

                        DatalogSensorSession sensorSession = new DatalogSensorSession( driverSensorSession.serialNumber, new ComponentType(sensorCode) );

                        sensorSession.Gas = gasType;

                        sensorSession.AlarmLow = driverSensorSession.AlarmLow;
                        sensorSession.AlarmHigh = driverSensorSession.AlarmHigh;
                        sensorSession.AlarmSTEL = driverSensorSession.AlarmSTEL;
                        sensorSession.AlarmTWA = driverSensorSession.AlarmTWA;
                        // Right now, there is a one-to-one mapping between the driver's SensorStatuses
                        // and ds.type's SensorStatuses, so we can do a simple assignment.
                        sensorSession.Status = (ISC.iNet.DS.DomainModel.SensorStatuses)driverSensorSession.Status;

                        if ( driverSensorSession.CustomResponseValue > 0.0 )
                        {
                            sensorSession.ResponseFactor.Name = driverSensorSession.CustomResponseName;
                            sensorSession.ResponseFactor.Value = driverSensorSession.CustomResponseValue;
                            sensorSession.ResponseFactor.GasCode = sensorSession.Gas.Code;
                        }

                        // Preset capacity of readingperiods arraylist. There's a potential for a few thousand
                        // periods per session, so by presetting, we prevent reallocation of the list during
                        // the processing loop that follows.
                        sensorSession.ReadingPeriods.Capacity = driverSensorSession.ReadingPeriods.Count;

                        Log.Debug( "DATALOG: SensorSession " + ( sensorSessionIndex + 1 ) + "/" + driverSession.SensorSessions.Length

                            + ": UID=" + sensorSession.Uid
                            + ", Type=" + sensorSession.Type.Code
                            + ", Gas=" + sensorSession.Gas.Code
                            + ", Status=" + sensorSession.Status
                            + ", AlarmLow=" + sensorSession.AlarmLow
                            + ", AlarmHigh=" + sensorSession.AlarmHigh
                            + ", AlarmSTEL=" + sensorSession.AlarmSTEL
                            + ", AlarmTWA=" + sensorSession.AlarmTWA
                            + ", RfName=\"" + sensorSession.ResponseFactor.Name + "\", Rf=" + sensorSession.ResponseFactor.Value
                            + ", Periods=" + sensorSession.ReadingPeriods.Count );

                        // Proceses all ReadingPeriods returned from the driver.  We consume them as we process
                        // by removing each one from the SensorSession, processing it, then moving to the next.
                        // This is to free up memory for garbage collection ASAP.
                        while ( driverSensorSession.ReadingPeriods.Count > 0 )
                        {
                            // There can potentially be a few thousand periods in the array so it behooves
                            // us to free them back to garbage collector as soon as we know we don't need them anymore.                              
                            DriverInstrumentSensorReadingPeriod driverReadingPeriod
                        = (DriverInstrumentSensorReadingPeriod)driverSensorSession.ReadingPeriods[0];
                            driverSensorSession.ReadingPeriods.RemoveAt( 0 );

                            DatalogPeriod readingPeriod = new DatalogPeriod( driverReadingPeriod.Period );

                            readingPeriod.Time = driverReadingPeriod.Timestamp;

                            // locations will often be duplicated.  Since there's a potential to have a few thousand
                            // periods per session, try and make location string references point to the same string
                            // to save memory.
                            if ( _lastDatalogLocation != driverReadingPeriod.Location )
                                _lastDatalogLocation = driverReadingPeriod.Location;
                            readingPeriod.Location = _lastDatalogLocation;

                            // Preset capacity of readings arraylist. There's a potential for thousands and 
                            // thousands of periods per period, so by presetting, we prevent reallocation of the 
                            // the arraylist during the following processing loop.
                            readingPeriod.Readings.Capacity = driverReadingPeriod.Readings.Length;

                            for ( int readingIndex = 0; readingIndex < driverReadingPeriod.Readings.Length; readingIndex++ )
                            {
                                DriverInstrumentSensorReading driverReading = driverReadingPeriod.Readings[readingIndex];

                                // Sever reading from driver data to free up memory for garbage collection ASAP.
                                // There can potentially be thousands and thousands of readings in the array so it
                                // behooves us to free them back to the garbage collector as soon as we know we don't
                                // need them anymore.
                                driverReadingPeriod.Readings[readingIndex] = null;

                                if ( driverReading == null ) continue; // This has happened.

                                // INS-1793 - The value of this check is questionable, as this has only ever happened ONCE in production...
                                if ( driverReading.Count == 0 )
                                {
                                    session.CorruptionException = new ApplicationException( "Reading encountered with zero counts. Remaining readings in the period discarded." );
                                    Log.Warning( "DATALOG: " + session.CorruptionException );
                                    driverReading.Count = 1; // change to 1 so that at least one instance of the actual reading value gets uploaded.
                                    // advance index to end of array to ignore the remaining readings.
                                    readingIndex = driverReadingPeriod.Readings.Length;
                                }

                                DatalogReading reading = new DatalogReading( driverReading.RawReading, driverReading.Temperature, driverReading.Count );

                                readingPeriod.Readings.Add( reading );

                            }  // end-for Readings

                            Log.Debug( "DATALOG: Period " + readingPeriod.Period + " (" + Log.DateTimeToString(readingPeriod.Time) + "), Location=\"" + readingPeriod.Location + "\", readings: " + readingPeriod.Readings.Count );

                            //if ( readingPeriod.Readings.Count > 0 ) // Ignore periods that have no readings.
                            sensorSession.ReadingPeriods.Add( readingPeriod );

                        }  // end-for sensor periods

                        // Sever period from driver data to free up memory for garbage collection ASAP.
                        // For GBPRO, there can potentially be a few thousand periods in the array so it behooves
                        // us to free them back to garbage collector as soon as we know we don't need them anymore.
                        driverSession.SensorSessions[sensorSessionIndex] = null;

                        // Ignore sessions that have no periods. This can happen if all the periods
                        // had no readings and were thus themselves ingored.
                        //if ( sensorSession.ReadingPeriods.Count > 0 )
                        session.SensorSessions.Add( sensorSession );

                    }  // end-for Sensor Sessions

                    sessionList.Add( session );

                    Log.Debug( "DATALOG: Session " + sessionList.Count + " converted in " + ( DateTime.UtcNow - convertStartTime ) );

                } // end-while Sessions

            } // end-try
            finally
            {
                // Before leaving, restore thhe thread priority to what it was when this method was first called.
                if ( Thread.CurrentThread.Priority != originalPriority )
                    Thread.CurrentThread.Priority = originalPriority;
            }

            Log.Debug( "DATALOG: All sessions downloaded, parsed, & converted in " + ( DateTime.UtcNow - startTime ) );

            return sessionList;
        }

         /// <summary>
        /// Retrieves the instrument's datalog recording interval.
        /// </summary>
        /// <returns>Recording interval, in seconds.</returns>
        public int GetRecordingInterval()    
        {
            return Driver.Definition.HasDataLoggingFeature ? Driver.getRecordingInterval() : int.MinValue;
        }

        /// <summary>
        /// Sets the instrument's datalog recording interval.
        /// </summary>
        /// <param name="recordingInterval">Datalig recording interval in seconds.</param>
        public void SetRecordingInterval( int recordingInterval )    
        {
            if ( Driver.Definition.HasDataLoggingFeature )
                Driver.setRecordingInterval( recordingInterval );
        }

		/// <summary>
		/// Gets an empty list of base units by default as this is 
		/// not supported by most instrument types.
		/// </summary>
		/// <returns></returns>
		public virtual List<BaseUnit> GetBaseUnits()
		{
			return new List<BaseUnit>();
		}

        /// <summary>
        /// Retrieves instrument's alarm events.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns></returns>
        public virtual SensorGasResponse[] GetManualGasOperations()
        {
            GasOperation[] gasOperations = Driver.getGasOperations();

            List<SensorGasResponse> sgrList = new List<SensorGasResponse>( gasOperations.Length );

            foreach ( GasOperation go in gasOperations )
            {
                SensorGasResponse sgr = new SensorGasResponse( go.SerialNumber + "#" + go.SensorCode, go.Timestamp );

                if ( go.OperationType == GasOperation.Type.Bump )
                    sgr.Type = GasResponseType.Bump;
                else if ( go.OperationType == GasOperation.Type.Calibration || go.OperationType == GasOperation.Type.Zero )
                    sgr.Type = GasResponseType.Calibrate;
                else
                {
                    Log.Error( string.Format( "Sensor \"{0}\": Unknown OperationType: {1}", sgr.Uid, go.OperationType.ToString() ) );
                    continue;
                }

                if ( sgr.Type == GasResponseType.Bump )
                {
                    if ( go.OperationStatus == GasOperation.Status.Pass )
                        sgr.Status = Status.PassedManual;
                    else //if ( go.OperationStatus == GasOperation.Status.Fail )
                        sgr.Status = Status.FailedManual;
                }
                else if ( sgr.Type == GasResponseType.Calibrate )
                {
                    if ( go.ZeroStatus == GasOperation.Status.Fail )
                        sgr.Status = Status.ZeroFailedManual;
                    else if ( go.OperationStatus == GasOperation.Status.Pass )
                        sgr.Status = Status.PassedManual;
                    else
                        sgr.Status = Status.FailedManual;
                }
                else
                {
                    Log.Error( string.Format( "Sensor \"{0}\": Unknown OperationStatus: {1}", sgr.Uid, go.OperationStatus.ToString() ) );
                    continue;
                }

                if ( go.OperationStatus == GasOperation.Status.Pass || go.OperationStatus == GasOperation.Status.Fail )
                    sgr.Reading = go.Reading;

                try
                {
                    sgr.GasConcentration = new GasConcentration( DomainModel.GasType.Cache[ go.GasCode ], go.GasConcentration );
                }
                catch ( Exception ex ) // this will happen if instrument returns an invalid gas code which can't be found in the GasType dictionary cache
                {
                    Log.Error( ex );
                    Log.Error( string.Format( "Invalid gas code: \"{0}\", OperationType={1}, OperationStatus={2}, Timestamp={3}",
                        go.GasCode, go.OperationType, go.OperationStatus, Log.DateTimeToString( go.Timestamp ) ) );
                    throw;
                }

                sgr.BaseLine = go.Baseline;
                sgr.SpanCoef = go.Sensitivity;
                sgr.ZeroOffset = go.Resolution.Apply( go.Baseline );  // zero offset = baseline with resolution applied
                sgr.Duration = go.Duration;

                if ( go.OperationType == GasOperation.Type.Bump )
                {
                    sgr.Timeout = go.BumpTimeout;
                    sgr.Threshold = go.BumpThreshold;
                }
                else
                    sgr.Timeout = go.CalibrationTimeout;

                sgr.AccessoryPump = go.IsAccessoryPumpInstalled ? AccessoryPumpSetting.Installed : AccessoryPumpSetting.Uninstalled;

                sgr.ManualOperationId = go.Index;

                sgrList.Add( sgr );
            }
            return sgrList.ToArray();
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// </remarks>
        public virtual void ClearManualGasOperations() { Driver.clearGasOperations(); }

        /// <summary>
        /// Retrieves instrument's alarm events.
        /// </summary>
        /// <returns></returns>
        public virtual ISC.iNet.DS.DomainModel.AlarmEvent[] GetAlarmEvents()
        {
            // get events from instrument.
            List<ISC.Instrument.Driver.AlarmEvent> driverEventsList = new List<ISC.Instrument.Driver.AlarmEvent>( Driver.getAlarmEvents() );

            // Now convert instrument's events into DS2 events...
            List<ISC.iNet.DS.DomainModel.AlarmEvent> alarmEventsList = new List<ISC.iNet.DS.DomainModel.AlarmEvent>( driverEventsList.Count );

            for ( int i = 0; i < driverEventsList.Count; i++ )
            {
                ISC.Instrument.Driver.AlarmEvent driverEvent = driverEventsList[i];

                ISC.iNet.DS.DomainModel.AlarmEvent alarmEvent    = new ISC.iNet.DS.DomainModel.AlarmEvent();

                // can leave InstrumentSerialNumber empty. Caller will deal with it.
                alarmEvent.SensorSerialNumber     = driverEvent.SensorSerialNumber.ToUpper();
				
				// SensorCode is needed for instruments that could have multiple virtual sensors.
				alarmEvent.SensorCode             = driverEvent.SensorCode;
				if ( alarmEvent.SensorCode == string.Empty )
					alarmEvent.SensorCode = AlarmEvent.GasCode2SensorCode( driverEvent.GasCode );

                alarmEvent.GasCode                = driverEvent.GasCode;
				alarmEvent.IsDualSense            = driverEvent.IsDualSense;
                alarmEvent.Timestamp              = driverEvent.Timestamp;
                // The Duration in ISC.iNet.DS.DomainModel.AlarmEvent needs to be an integer instead
                // of TimeSpan since .NET doesn't support XML serialization of TimeSpans.
                alarmEvent.Duration               = Convert.ToInt32( driverEvent.Duration.TotalSeconds );
                alarmEvent.PeakReading            = driverEvent.PeakReading;
                alarmEvent.AlarmLow               = driverEvent.AlarmLow;
                alarmEvent.AlarmHigh              = driverEvent.AlarmHigh;
                alarmEvent.User                   = driverEvent.User;
                alarmEvent.Site                   = driverEvent.Site;
                alarmEvent.UserAccessLevel        = driverEvent.UserAccessLevel;
                alarmEvent.SiteAccessLevel        = driverEvent.SiteAccessLevel;
                alarmEvent.Ticks                  = driverEvent.Ticks;
				alarmEvent.BaseUnitSerialNumber   = driverEvent.BaseUnitSerialNumber;

                // Leave the alarmEvent's properties set to their defaults if the instrument
                // isn't a gbpro.  That way, we don't end up uploading the properties to inet
                // for non-gpro instruments.
                if ( this is GBPRO )
                {
                    alarmEvent.SpeakerVoltage = driverEvent.SpeakerVoltage;
                    alarmEvent.VibratingMotorVoltage = driverEvent.VibratingMotorVoltage;
                    alarmEvent.AlarmOperatingMode = (AlarmOperatingMode)driverEvent.AlarmOperatingMode;
                    alarmEvent.IsDocked = driverEvent.IsDocked;
                }

                Log.Debug( string.Format( "AlarmEvent {0}/{1}: SN={2},Gas={3},DualSense={4},Time={5},Dur={6},Peak={7}",
                    i+1, driverEventsList.Count, alarmEvent.SensorSerialNumber, alarmEvent.GasCode, alarmEvent.IsDualSense, Log.DateTimeToString( alarmEvent.Timestamp ), alarmEvent.Duration, alarmEvent.PeakReading ) );
                Log.Debug( string.Format( "AlarmEvent {0}/{1}: ...AlarmLow={2},AlarmHigh={3},UserAccessLevel={4},SiteAccessLevel={5},User=\"{6}\",Site=\"{7}\"",
                    i + 1, driverEventsList.Count, alarmEvent.AlarmLow, alarmEvent.AlarmHigh, alarmEvent.UserAccessLevel, alarmEvent.SiteAccessLevel, alarmEvent.User, alarmEvent.Site));

                alarmEventsList.Add( alarmEvent );
            }

            return alarmEventsList.ToArray();
        }

        /// <summary>
        /// Clear all datalog sessions currently stored on the instrument.
        /// </summary>
        /// <returns>The number of sessions cleared.</returns>
        public virtual int ClearDatalog()
        {
            int numToClear = GetDatalogSessionCount(); // Get the number to be cleared.

            Driver.clearHygiene();

            // Get the remaining number of sessions.  subtract one since the clearing
            // always causes a new session to be created; we need to ignore that one.
            int notCleared = GetDatalogSessionCount() - 1;

            int numCleared = numToClear - notCleared;

            Log.Debug( "CLEAR: numToClear=" + numToClear + ", numCleared=" + numCleared + ", notCleared=" + notCleared );

            // Return the number of datalog sessions cleared.
            return numCleared;
        }

		/// <summary>
		/// Clears an instrument's log of base units in which it has been docked.
		/// </summary>
		public virtual void ClearBaseUnits() { }

        /// <summary>
        /// Clears an instrument's alarm events.
        /// </summary>
        public virtual void ClearAlarmEvents()
        {
            Driver.clearAlarmEvents();
        }

        /// <summary>
        /// Begin zeroing of a sensor.
        /// </summary>
        /// <param name="position">The position of the sensor to zero. 0 for all.</param>
        protected void BeginInstrumentZero()    
        {
            Driver.beginInstrumentZeroing();
        }

        /// <summary>
        /// End instrument zeroing
        /// </summary>
        protected void EndInstrumentZero()
        {
            Driver.endInstrumentZeroing();
        }

        /// <summary>
        /// Get status of the current zeroing operation.
        /// </summary>
        /// <param name="position">The position of the sensor to check, 0 for all.</param>
        /// <returns>The zero status of the sensor.</returns>
        protected bool IsInstrumentZeroing()    
        {
            return Driver.isInstrumentZeroing();
        }

        ///// <summary>
        ///// Intended to put instrument into warm up mode.
        ///// This method does nothing for this instrument type.
        ///// </summary>
        //public void BeginInstrumentWarmUp()
        //{
        //    Log.Debug("BeginInstrumentWarmUp");
        //    Driver.beginInstrumentWarmUp();
        //}

        ///// <summary>
        ///// Intended to End/abort warm up mode on instrument.
        ///// This method does nothing for this instrument type.
        ///// </summary>
        //public void EndInstrumentWarmUp()
        //{
        //    Log.Debug("EndInstrumentWarmUp");
        //    Driver.endInstrumentWarmUp();
        //}

        /// <summary>
        /// Begin Bump Test on specified sensor. This is used ONLY by MX6 v4.4 and above.
        /// INS-4953 RHP v7.6
        /// </summary>
        public virtual void BeginSensorBump(int position )
        {
            Log.Debug("BeginSensorBump");
            Driver.beginSensorBump(position);
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void BeginInstrumentBump()
        {
            Log.Debug( "BeginInstrumentBump" );
            Driver.turnOnSensors( true, true );
            Driver.beginInstrumentBump();
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void EndInstrumentBump()
        {
            Log.Debug( "EndInstrumentBump" );
            Driver.endInstrumentBump();
        }

        /// <summary>
        /// Intended to put instrument into calibration mode.
        /// This method needs to be called prior to calling
        /// BeginSensorCalibration(sensorPos).
        /// </summary>
        public virtual void BeginInstrumentCalibration()
        {
            Log.Debug( "BeginInstrumentCalibration" );
            Driver.beginInstrumentCalibration();
        }

        /// <summary>
        /// Intended to End/abort calibration mode on instrument.
        /// </summary>
        public virtual void EndInstrumentCalibration()
        {
            Log.Debug( "EndInstrumentCalibration" );
            Driver.endInstrumentCalibration();
        }

        /// <summary>
        /// Begin to calibrate a sensor.
        /// </summary>
		/// <param name="positions">The position(s) of the sensor to calibrate.</param>
        public virtual void BeginSensorCalibration( IEnumerable<int> positions )    
        {
			string positionsMsg = string.Empty;
			foreach ( int position in positions )
			{
				if ( positionsMsg.Length > 0 ) positionsMsg += ",";
				positionsMsg += position;
			}
			Log.Debug( "BeginSensorCalibration( positions=" + positionsMsg + " )" );
			Driver.beginSensorCalibration( positions );
        }

        /// <summary>
        /// Get status of the current calibration operation.
        /// </summary>
        /// <param name="position">The position of the sensor to check.</param>
		/// <returns>The cal status of the sensor. 
		/// true - sensor is calibrating; 
		/// false - sensor is not calibrating; 
		/// null - InstrumentAborted</returns>
        public virtual bool? IsSensorCalibrating( int pos )    
        {
            return Driver.isSensorCalibrating( pos );
        }

        /// <summary>
        /// Get status of the current zeroing operation.
        /// </summary>
        /// <param name="position">The position of the sensor to check.</param>
        /// <returns>The zero status of the sensor.</returns>
        public bool IsSensorZeroing( int pos )    
        {
            return Driver.isSensorZeroing( pos );
        }

        /// <summary>
        /// Determine the current status of zeroing/calibration operation.
        /// </summary>
        /// <param name="position">The position of the sensor to check.</param>
        /// <returns>The status of that sensor's calibration.</returns>
        public Status GetStatus( int pos )    
        {
            bool inProgress = Driver.isSensorCalibrating( pos );
            bool calStatus  = Driver.getSensorCalibrationStatus( pos );
            bool zerStatus  = Driver.getSensorZeroingStatus( pos );
            
            Status status;

            if ( inProgress )
            {
                if ( !zerStatus )
                {
                    if ( !calStatus )
                        status = Status.InProgressSpanFailedZeroFailed;
                    else
                        status = Status.SpanInProgressZeroFailed;
                }
                else
                {
                    if ( !calStatus )
                        status = Status.InProgressSpanFailed;
                    else
                        status = Status.InProgress;
                }
            }
            else  // !inProgress
            {
                if ( !zerStatus )
                {
                    if ( !calStatus )
                        status = Status.SpanFailedZeroFailed;
                    else
                        status = Status.SpanPassedZeroFailed;
                }
                else
                {
                    if ( !calStatus )
                        status = Status.SpanFailed;
                    else
                        status = Status.Passed;
                }
            }

    #if DEBUG
            Log.Debug( "GetStatus: isCaling=" + inProgress + ", calStat=" + calStatus + ", zerStat=" + zerStatus + ", Status=" + status );
    #endif

            return status;
        }

        /// <summary>
        /// Determine the status of last zeroing operation.
        /// </summary>
        /// <param name="position">The position of the sensor to check.</param>
        /// <returns>
        /// The status of that sensor's last zeroing:
        /// True if successful, False if failure
        /// </returns>
        public virtual bool GetSensorZeroingStatus( int position )
        {
            bool zerStatus  = Driver.getSensorZeroingStatus( position );
            return zerStatus;
        }

        /// <summary>
        /// Determine the status of last calibration operation.
        /// </summary>
        /// <param name="position">The position of the sensor to check.</param>
        /// <returns>
        /// The status of that sensor's last calibration:
        /// True if successful, False if failure
        /// </returns>
        public virtual bool GetSensorCalibrationStatus( int position )
        {
            bool calStatus  = Driver.getSensorCalibrationStatus( position );
            return calStatus;
        }

        // INS-2463
        /// <summary>
        /// Determine the status of last bump operation.
        /// </summary>
        /// <param name="position">The position of the sensor to check.</param>
        /// <returns>
        /// The status of that sensor's last bump:
        /// True if successful, False if failure
        /// </returns>
        public virtual bool GetSensorBumpStatus(int position)
        {
            bool bumpStatus = Driver.getSensorBumpStatus(position);
            return bumpStatus;
        }

        /// <summary>
        /// Get Calibration timeout for specified sensor
        /// </summary>
        /// <param name="position">The position of the sensor to check.</param>
        /// <returns>Number of seconds</returns>
        public virtual TimeSpan GetSensorCalibrationTimeout( int position )    
        {
            short timeoutSecs = Driver.getCalTimeoutSeconds( position );
            return new TimeSpan( 0, 0, timeoutSecs );
        }

        /// <summary>
        /// Gets a sensor's last calibration date/time.
        /// </summary>
        public virtual DateTime GetSensorLastCalibrationTime( int position )    
        {
			return Driver.getLastCalibrationTime( position );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns></returns>
        public bool IsSensorCalPreconditionEnabled( InstalledComponent installedComponent )
        {
            Sensor sensor = (Sensor)installedComponent.Component;

            // ALWAYS skip preconditioning of O2, regardless of programmed timeout.
            if ( sensor.CalibrationGas.Code == GasCode.O2 )
            {
                Log.Debug( "SKIPPING PRECONDITION OF OXYGEN" );
                return false;
            }

            // read precondition timeout value from sensor.
            // A timeout of zero means do not precondition the sensor.
            // INS-6317,10/2015 - increased check from zero to 60 seconds, as a docked-to-green enhancement.
            const int MinPreconditionTimeout = 60;
            int timeoutTotalSeconds = (int)GetSensorPreconditionTimeout( installedComponent ).TotalSeconds;
            bool enabled = timeoutTotalSeconds > MinPreconditionTimeout; 
            if ( !enabled )
                Log.Debug( string.Format( "SKIPPING PRECONDITION of {0}, Sensor timeout value ({1}) does not exceed {2}.", sensor.Type.Code, timeoutTotalSeconds, MinPreconditionTimeout ) );

            return enabled;
        }

        /// <summary>
        /// Determines whether a sensor needs to be preconditioned.
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns></returns>
        public bool IsSensorBumpPreconditionEnabled( InstalledComponent installedComponent ) // SGF  Jan-2-2009  DSW-173, DSW-174
        {
            Sensor sensor = (Sensor)installedComponent.Component;

            if (sensor.RequiresBumpPrecondition == true) // SGF  Nov-11-2009  DSW-350 (DS2 v7.6)
            {
                Log.Debug("PERFORMING BUMP PRECONDITION OF SENSOR");
                return true;
            }

            Log.Debug("SKIPPING BUMP PRECONDITION OF SENSOR");
            return false;
        }

        /// <summary>
        /// Returns false if calibration FlowRate programmed into the sensor is zero,
        /// to indicate that the sensor should never be calibrated.
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns></returns>
        public virtual bool IsSensorCalibrationEnabled( InstalledComponent installedComponent )
        {
            // A zero flowrate means we don't calibrate this sensor type.
            return GetSensorCalibrationFlowRate( installedComponent ) > 0;
        }

        /// <summary>
        /// Returns the Precondition FlowRate programmed into the sensor.
        /// <para>
        /// If AccessoryPump is installed, and programmed flow rate is non-zero,
        /// then Pump.StandardFlowRate is returned instead of the sensor's programmed value.
        /// </para>
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns>ml/min</returns>
        public virtual int GetSensorPreconditionFlowRate( InstalledComponent installedComponent )
        {
            int flowRate = Driver.getSensorPreconditionFlowrate( installedComponent.Position );
            Log.Debug( "Sensor has Precondition Flow Rate of " + flowRate + "ml/min" );

            if ( flowRate == 0 ) // zero means don't precondition.
                return flowRate;

            // Per decision of engineering, the iNet should use a standard flow rate
            // of 550.  If sensor specifies a smaller value, then override the sensor.
            flowRate = Math.Max( Pump.StandardFlowRate, flowRate );

            // If instrument has a pump, then ignore DAS value (which could be too high) and use a special flow rate
            if ( AccessoryPump == AccessoryPumpSetting.Installed )
            {
                flowRate = Pump.StandardFlowRate;
                Log.Debug( string.Format( "Overriding flowrate to {0}ml/min because of pump", flowRate ) );
            }

            return flowRate;
        }

        /// <summary>
        /// Provide the flow rate for properly preconditioning the sensor.
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns>ml/min</returns>
        public int GetSensorBumpPreconditionFlowRate( InstalledComponent installedComponent ) // SGF  Jan-2-2009  DSW-173, DSW-174
        {
            int flowRate;
            Sensor sensor = (Sensor)installedComponent.Component;

            if (sensor.RequiresBumpPrecondition == true) // SGF  Nov-11-2009  DSW-350 (DS2 v7.6)
            {
                flowRate = Pump.MaxFlowRate; // Heavy gases require a greater flow.
            }
            else
            {
                flowRate = Pump.MinFlowRate;  // No other gases require preconditioning at this time.
            }

            Log.Debug("Sensor has Precondition Flow Rate of " + flowRate + "ml/min");

            if (flowRate == 0) // zero means don't precondition.
                return flowRate;

            // Per decision of engineering, the iNet should use a standard flow rate
            // of 550.  If sensor specifies a smaller value, then override the sensor.
            flowRate = Math.Max( Pump.StandardFlowRate, flowRate );

            // If instrument has a pump, then ignore DAS value (which could be too high) and use a special flow rate
            if (AccessoryPump == AccessoryPumpSetting.Installed)
            {
                flowRate = Pump.StandardFlowRate;
                Log.Debug(string.Format("Overriding flowrate to {0}ml/min because of pump", flowRate));
            }

            return flowRate;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns>ml/Min</returns>
        public virtual int GetSensorCalibrationFlowRate( InstalledComponent installedComponent )
        {
            int flowRate = Driver.getSensorCalibrationFlowrate( installedComponent.Position );
            Log.Debug( "Sensor has Calibration Flow Rate of " + flowRate + "ml/min" );

            if ( flowRate == 0 ) // zero means don't calibrate.
                return flowRate;

            // Per decision of engineering, the iNet should use a standard flow rate
            // of 550.  If sensor specifies a smaller value, then override the sensor.
            flowRate = Math.Max( Pump.StandardFlowRate, flowRate );

            // If instrument has a pump, then ignore DAS value (which could be too high) and use a special flow rate
            if ( AccessoryPump == AccessoryPumpSetting.Installed )
            {
                flowRate = Pump.StandardFlowRate;
                Log.Debug( string.Format( "Overriding flowrate to {0}ml/min because of pump", flowRate ) );
            }

            return flowRate;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns>ml/min</returns>
        public virtual int GetSensorBumpFlowRate( InstalledComponent installedComponent )
        {
            int flowRate;

            if ( installedComponent.Component.Type.Code == SensorCode.ClO2 )
            {
                flowRate = Pump.StandardFlowRate;
                Log.Debug( "Using BumpTest Flow Rate of " + flowRate + "ml/min" );
                return flowRate;
            }

            flowRate = Driver.getSensorBumpFlowrate( installedComponent.Position );
            Log.Debug( "Sensor has programmed BumpTest Flow Rate of " + flowRate + "ml/min" );

            if ( flowRate == 0 ) // zero means don't bump.
                return flowRate;

            // Per decision of engineering, the iNet should use a standard flow rate
            // of 550.  If sensor specifies a smaller value, then override the sensor.
            flowRate = Math.Max( Pump.StandardFlowRate, flowRate );

            // If instrument has a pump, then ignore DAS value (which could be too high) and use a special flow rate
            if ( AccessoryPump == AccessoryPumpSetting.Installed )
            {
                flowRate = Pump.StandardFlowRate;
                Log.Debug( string.Format( "Overriding flowrate to {0}ml/min because of pump", flowRate ) );
            }

            return flowRate;
        }

        /// <summary>
        /// Get the Precondition Timeout for sensor.
        /// </summary>
        /// <returns>
        /// Number of seconds.
        /// Zero is returned if sensor should not be preconditioned.
        /// </returns>
        public virtual TimeSpan GetSensorPreconditionTimeout( InstalledComponent installedComponent )
        {
            TimeSpan timeout = new TimeSpan( 0, 0, Driver.getSensorPreconditionTime( installedComponent.Position ) );
            Log.Debug( "Sensor has Precondition Timeout of " + timeout.TotalSeconds + " seconds" );
            return timeout;
        }

        // SGF  Jan-2-2009  DSW-173, DSW-174
        /// <summary>
        /// Get the Precondition Timeout for sensor.
        /// </summary>
        /// <returns>
        /// Number of seconds.
        /// Zero is returned if sensor should not be preconditioned.
        /// </returns>
        public TimeSpan GetSensorBumpPreconditionTimeout( InstalledComponent installedComponent )
        {
            TimeSpan timeout;
            Sensor sensor = (Sensor)installedComponent.Component;

            if ( sensor.RequiresBumpPrecondition == true ) // SGF  Nov-11-2009  DSW-350 (DS2 v7.6)
            {
                timeout = new TimeSpan( 0, 5, 0 ); // 5 minutes
            }
            else
                // question: if RequiresBumpPrecondition returns false (meaning "Doesn't need preconditioned"?),
                // then shouldn't precondition timeout be ZERO minutes?  - JMP, 9/2013.
                timeout = new TimeSpan( 0, 1, 0 ); // 1 minute for all others

            Log.Debug( "Sensor has Bump Precondition Timeout of " + timeout.TotalSeconds + " seconds" );
            return timeout;
        }

        /// <summary>
        /// Retrieves the instrument's current backlight setting.
        /// Note: 0=Manual, 1=Timed, 2=Automatic		
        /// </summary>
        /// <returns>Back light setting</returns>
        public BacklightSetting GetBacklightSetting()    
        {
            ISC.Instrument.TypeDefinition.BackLightOption blo = Driver.getBacklightOption();

            if ( blo == BackLightOption.Manual )
                return BacklightSetting.Manual;

            if ( blo ==  BackLightOption.Timed )
                return BacklightSetting.Timed;

            if ( blo ==  BackLightOption.PhotoCell )
                return BacklightSetting.Automatic;

            if ( blo ==  BackLightOption.AlwaysOn )
                return BacklightSetting.AlwaysOn;

            if (blo == BackLightOption.AlwaysOff)
                return BacklightSetting.AlwaysOff;

            return BacklightSetting.Unknown;
        }

        /// <summary>
        /// Sets the instrument's current backlight setting.
        /// </summary>
        /// <param name="backlightSetting">Backlight setting value</param>
        public void SetBacklightSetting( BacklightSetting backlightSetting )    
        {
            if ( !Driver.Definition.HasBacklightConfigFeature )
                return;

            ////////////////////////////////////////////////////////////////////
            // Not currently supported.  We have no dockable modbus instrument 
            // that we can configure yet and no need to support yet.
            ////////////////////////////////////////////////////////////////////

            Log.Debug( "SetBacklightSetting: Not implemented." );
            BackLightOption blo;

            if (backlightSetting == BacklightSetting.Manual)
                blo = BackLightOption.Manual;

            else if (backlightSetting == BacklightSetting.Timed)
                blo = BackLightOption.Timed;

            else if (backlightSetting == BacklightSetting.Automatic)
                blo = BackLightOption.PhotoCell;

            else if (backlightSetting == BacklightSetting.AlwaysOff)
                blo = BackLightOption.AlwaysOff;

            else if (backlightSetting == BacklightSetting.AlwaysOn)
                blo = BackLightOption.AlwaysOn;

            // If we have some 'unknown' setting, just set to automatic.
            else // backLightSetting == BacklightSettings.Unknown
            {
                Log.Error("'Unknown' backlight option in SetBacklightSetting.  Defaulting to Manual");
                blo = BackLightOption.Manual;
            }

            Driver.setBackLightOption(blo);
        }

		internal bool IsSupportedInstrumentLanguage( LanguageId languageId )
		{
			LanguageId[] supportedLanguages = Driver.Definition.SupportedLanguages;

			foreach ( LanguageId supportedLanguageId in supportedLanguages )
			{
				if ( supportedLanguageId == languageId )
				{
					return true;
				}
			}

			return false;
		}

        /// <summary>
        /// Set the docked instrument to the provided language code.
        /// </summary>
        /// <returns>True - if instrument supports the provided language code and was set to it; 
		/// False - if the instrument does not support the provided language code.</returns>
        public bool SetLanguage( string languageCode )
        {
			LanguageId language = LanguageId.None;

			// Need to map from DS2 codes to driver codes.
			switch ( languageCode )
			{
				case Language.English:
					language = LanguageId.English;
					break;
				case Language.French:
					language = LanguageId.French;
					break;
				case Language.German:
					language = LanguageId.German;
					break;
				case Language.Spanish:
					language = LanguageId.Spanish;
					break;
				case Language.PortugueseBrazil: // SGF  1-Oct-2012  INS-1656
					language = LanguageId.PortugueseBrazil;
					break;
				// JFC  26-Sep-2013  INS-4248
				// Added mappings to existing languages defined in the both
				// iNet and the Instrument Driver.
				case Language.BahasaIndonesia:
					language = LanguageId.BahasaIndonesia;
					break;
				case Language.Czech:
					language = LanguageId.Czech;
					break;
				case Language.Dutch:
					language = LanguageId.Dutch;
					break;
				case Language.FrenchCanada:
					language = LanguageId.FrenchCanada;
					break;
				case Language.Italian:
					language = LanguageId.Italian;
					break;
				case Language.Chinese:
					language = LanguageId.ChineseSimplified;
					break;
				case Language.Polish:
					language = LanguageId.Polish;
					break;
				case Language.Russian:
					language = LanguageId.Russian;
					break;
				default:
					Log.Warning( "Unsupported language in SetLanguage: \"" + languageCode + "\". Reverting to English." );
					language = LanguageId.English; // default to english
					break;
			}

            if (IsSupportedInstrumentLanguage(language))
			{            
                Driver.setLanguage(language); 
				return true;
			}

			Log.Debug( "Unsupported language for docked instrument in SetLanguage: \"" + languageCode + "\". Instrument language will not be changed." );
			return false;            
        }

        /// <summary>
        /// Return a language code that this instrument is configured for.
        /// </summary>
        /// <returns></returns>
        public string GetLanguage()
        {
            LanguageId languageId = Driver.getLanguage();

            // Need to map from driver codes to DS2 codes.
            if ( languageId == LanguageId.English )
                return Language.English;

            if ( languageId == LanguageId.French )
                return Language.French;

            if ( languageId == LanguageId.German )
                return Language.German;

            if ( languageId == LanguageId.Spanish )
                return Language.Spanish;

            if ( languageId == LanguageId.PortugueseBrazil ) // SGF  1-Oct-2012  INS-1656
                return Language.PortugueseBrazil;

            // JFC  26-Sep-2013  INS-4248
            // Added mappings to existing languages defined in the both
            // iNet and the Instrument Driver.
            if ( languageId == LanguageId.BahasaIndonesia )
                return Language.BahasaIndonesia;

            if ( languageId == LanguageId.Czech )
                return Language.Czech;

            if ( languageId == LanguageId.Dutch )
                return Language.Dutch;

            if ( languageId == LanguageId.FrenchCanada )
                return Language.FrenchCanada;

            if ( languageId == LanguageId.Italian )
                return Language.Italian;

            if ( languageId == LanguageId.ChineseSimplified )
                return Language.Chinese;

            if ( languageId == LanguageId.Polish )
                return Language.Polish;

            if ( languageId == LanguageId.Russian )
                return Language.Russian;

            if ( languageId == LanguageId.None )
                return string.Empty;

            Log.Warning( "Unsupported language detected in instrument: \"" + languageId + "\"." );

            return languageId.ToString().ToUpper();
        }

        /// <summary>
        /// Turn instrument's pump on or off
        /// </summary>
        /// <param name="on"></param>
        public virtual void EnablePump( bool on )
        {
            if ( !Controller.IsDocked() )
            {
                Log.Warning( "EnablePump: IsDocked=False" );
                return;
            }
            try
            {
                // Only spend effort to turn on/off the pump if we know there is a pump.
                if ( AccessoryPump == AccessoryPumpSetting.Installed )
                    Driver.enablePump( on );
            }
            catch ( Exception e )
            {
                Log.Warning( "EnablePump", e );
            }
        }

        /// <summary>
        /// 1) Opens the specified gas end point.
        /// <para>
        /// 2) Turns on the DS pump to establish the specified flow rate.
        /// </para>
        /// <para>
        /// 3) Turns on the instrument's pump.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The intention is to ensure that DS pump is always turned on before instrument's pump,
        /// to prevent instrument pump from going into pump fault.
        /// </remarks>
        /// <param name="gasEndPoint"></param>
        /// <param name="flowRate"></param>
		public virtual void OpenGasEndPoint( GasEndPoint gasEndPoint, int flowRate )
        {
            Pump.SetDesiredFlow( flowRate ); // Set the desired flow on the pump to the required flow rate.
            Pump.OpenGasEndPoint( gasEndPoint );
            if ( AccessoryPump == AccessoryPumpSetting.Installed )
            {
                Thread.Sleep( 1000 ); // get the pump a chance to start a flow
                EnablePump( true ); // Now that gas is flowing, we can turn on instrument pump without fear of Pump Fault.
                Thread.Sleep( 2000 ); // give a chance for instrument to get the pump's flow going. (There is often a slight delay with MX6)
            }
        }

        /// <summary>
        /// 1) Turns off the instrument's pump.
        /// <para>
        /// 2) Stops the DS pump.
        /// </para>
        /// <para>
        /// 3) Closes the specified gas end point.
        /// </para>
        /// </summary>
        /// <remarks>
        /// The intention is to ensure that instrument pump is always turned off before DS pump,
        /// to prevent instrument pump from going into pump fault.
        /// </remarks>
        /// <param name="gasEndPoint"></param>
		public virtual void CloseGasEndPoint( GasEndPoint gasEndPoint )
        {
            // some callers may pass in a null. don't ask yourself why.
            // in that situation, it's assumed no end point is open, and that instrument's pump was never turned on.
            if ( gasEndPoint == null )
                return;
            if ( AccessoryPump == AccessoryPumpSetting.Installed )
            {
                EnablePump( false ); // Turn off instrument pump before closing valve, to prevent instrument Pump Fault.
                Thread.Sleep( 2000 ); // give the instrument a moment to turn off the pump. (There is often a slight delay with MX6)
            }
            Pump.CloseGasEndPoint( gasEndPoint ); // Close gas end point
        }

        /// <summary>
        /// Retrieve the instrument battery serial number.
        /// </summary>
        /// <returns>Battery serial number</returns>
        public string GetBatterySerialNumber()    
        {
            return Driver.getBatterySerialNumber();
        }

        /// <summary>
        /// Retrieves the battery code based on battery type.
        /// </summary>
        /// <returns>Standardized battery code</returns>
        public virtual string GetBatteryCode()    
        {
            return string.Empty;
        }

        /// <summary>
        /// Retrieves amount of time of operation for the battery pack.
        /// </summary>
        /// <returns>Amount of time battery has been in operation</returns>
        public TimeSpan GetBatteryRunTime()    
        {
            try
            {
                return Driver.getBatteryRunTime();
            }
            catch ( NotSupportedException )
            {
                return TimeSpan.MinValue;
            }
        }

        /// <summary>
        /// Retrieves the battery pack part number.
        /// </summary>
        /// <returns>Battery pack part number</returns>
        public string GetBatteryPackPartNumber()    
        {
            return Driver.getBatteryPartNumber();
        }

        public string GetBatterySetupTech()
        {
           return Driver.getBatterySetupTech();
        }

        public DateTime GetBatterySetupDate()
        {
            return Driver.getBatterySetupDate();
        }

        public string GetBatterySoftwareVersion()
        {
            return Driver.getBatterySoftwareVersion();
        }

        /// <summary>
        /// Returns an array of SensorPositions that indicate the installation
        /// status (istalled or not) of all sensor positions in the instrument.
        /// </summary>
        /// <returns>
        /// </returns>
        public virtual SensorPosition[] GetSensorPositions()
        {
            ISC.Instrument.Driver.SensorPosition[] driverSensorPositions = Driver.getSensorPositions();

            List<SensorPosition> sensorPositions = new List<SensorPosition>( driverSensorPositions.Length );

            foreach ( ISC.Instrument.Driver.SensorPosition driverSensorPosition in driverSensorPositions )
            {
                Log.Warning( string.Format( "GetSensorPositions(): Position {0}: Type={1}, Mode={2}, DualSenseCapable={3}", driverSensorPosition.Position, driverSensorPosition.Type, driverSensorPosition.Mode, driverSensorPosition.IsDualSenseCapable ) );

                SensorMode mode = SensorMode.Installed;

                if ( driverSensorPosition.Mode == ISC.Instrument.Driver.SensorMode.Uninstalled )
                    mode = SensorMode.Uninstalled;

                if ( driverSensorPosition.Mode == ISC.Instrument.Driver.SensorMode.DataFault 
                ||   driverSensorPosition.Mode == ISC.Instrument.Driver.SensorMode.Uninitialized
                ||   driverSensorPosition.Mode == ISC.Instrument.Driver.SensorMode.Undefined )
                    mode = SensorMode.Error;
               
                sensorPositions.Add( new SensorPosition( driverSensorPosition.Position, mode, driverSensorPosition.IsDualSenseCapable ) );
            }

            return sensorPositions.ToArray();
        }

        /// <summary>
        /// Retrieves the sensor code for a specified sensor.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Standardized sensor code.  An empty string is returned if
        /// no sensor is installed for the specified position</returns>
        public string GetSensorCode( int sensorPosition )    
        {
            return Driver.getSensorCode( sensorPosition );
        }

        /// <summary>
        /// Retrieves the type of gas this sensor detects.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public string GetSensorGasCode( int pos )
        {
            return Driver.getSensorGasCode( pos );
        }

		/// <summary>
		/// Returns the type of "technology" for the specified sensor. e.g. LEL, PID, COSH, TOX, etc.
		/// </summary>
		/// <param name="pos"></param>
		/// <returns></returns>
		public string GetSensorTechnology( int pos )
		{
			return Driver.getSensorType( pos ).ToString();
		}

        /// <summary>
        /// Configures the type of gas the sensor should detect.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="gasCode"></param>
        public void SetSensorGasCode( int pos, string gasCode )
        {
            Driver.setSensorGasCode( pos, gasCode );
        }

        /// <summary>
        /// Retrieves the sensor part number.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor part number</returns>
        public string GetSensorPartNumber( int sensorPosition )    
        {
            return Driver.getSensorPartNumber( sensorPosition );
        }

        /// <summary>
        /// Retrieve a specific instrument sensor serial number based on the sensor position.
        /// </summary>
        /// <param name="sensorPosition">Sensor position on the instrument</param>
        /// <returns>Sensor serial number</returns>
        public string GetSensorSerialNumber( int sensorPosition )    
        {
            return Driver.getSensorSerialNumber( sensorPosition ).ToUpper();
        }


        /// <summary>
        /// Retrieves the sensor manufacturer code.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor manufacturer code</returns>
        public string GetSensorManufacturerCode( int sensorPosition )    
        {
            ISC.Instrument.Driver.Manufacturer manufacturerId = Driver.getSensorManufacturer( sensorPosition );
            
            switch ( manufacturerId )
            {
                case ISC.Instrument.Driver.Manufacturer.CityTech:
                    return "CTYTC";
                case ISC.Instrument.Driver.Manufacturer.Sensoric:
                    return "SNSRC";
                case ISC.Instrument.Driver.Manufacturer.IndustrialScientific:
                    return "ISC";
                case ISC.Instrument.Driver.Manufacturer.Alphasense:
                    return "ALPHA";
                case ISC.Instrument.Driver.Manufacturer.Dynament:
                    return "DYNA";
                case ISC.Instrument.Driver.Manufacturer.BaselineMOCON:
                    return "BASLN";
                default:
                    Log.Error( "GetSensorManufacturerCode: sensor " + sensorPosition + " has unexpected ID: " + manufacturerId );
                    return "ISC";  // default
            }
        }


        /// <summary>
        /// Get the maximum possible reading from the sensor.
        /// </summary>
        /// <param name="sensorPosition">The position of the sensor.</param>
        /// <param name="resolution">The resolution of the sensor.  Ignored for modbus instruments</param>
        /// <returns>The sensor maximum possible reading.</returns>
        public virtual double GetSensorMaximumReading( int sensorPosition, double resolution )    
        {
            return Driver.getMeasurementRange( sensorPosition );
        }

        /// <summary>
        /// Sensor max temp recorded.
        /// </summary>
        /// <param name="sensorPosition">The position of the sensor.</param>
        /// <returns></returns>
        public virtual int GetSensorMaxTemp( int sensorPosition )    
        {
            return Driver.getSensorMaxTemperature( sensorPosition );
        }

        /// <summary>
        /// Sensor min temp recorded.
        /// </summary>
        /// <param name="sensorPosition">The position of the sensor.</param>
        /// <returns></returns>
        public virtual int GetSensorMinTemp( int sensorPosition )    
        {
            return Driver.getSensorMinTemperature( sensorPosition );
        }

        /// <summary>
        /// Get a reading from the sensor.
        /// </summary>
        /// <param name="sensorPosition">The position of the sensor.</param>
        /// <param name="resolution">The resolution of the sensor. Ignored for modbus instruments</param>
        /// <returns></returns>
        public virtual double GetSensorReading( int sensorPosition, double resolution )    
        {
            return Driver.getSensorGasReading( sensorPosition );
        }

        /// <summary>
        /// Get a reading from the sensor.
        /// </summary>
        /// <param name="sensorPosition">The position of the sensor.</param>
        /// <param name="resolution">The resolution of the sensor.  Ignored for modbus instruments.</param>
        public virtual double GetSensorCalibrationReading( int sensorPosition, double resolution )    
        {
            return Driver.getSensorCalibrationReading( sensorPosition ); //Driver.getCalibrationReading( sensorPosition );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public virtual double GetSensorSpanReserve( int pos )
        {
            return Driver.getSensorSpanReserve( pos );
        }

        /// <summary>
        /// Sensor peak reading recorded.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns></returns>
        public virtual double GetSensorPeakReading( int sensorPosition, double resolution )    
        {
            // todo - shouldn't this get the low reading if its an o2 sensor? - jmp
            return Driver.getSensorHighReading( sensorPosition );
        }

        /// <summary>
        /// Retrieves the sensor calibration gas concentration value.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <param name="resolution">Ignored for modbus instruments</param>
        /// <returns>Calibration gas concentration for the specific sensor</returns>
        /// <returns></returns>
        public double GetSensorCalGasConcentration( int sensorPosition, double resolution )    
        {
            return Driver.getSensorCalGasConcentration( sensorPosition );
        }

        /// <summary>
        /// Set a sensor's calibration gas concentration.
        /// </summary>
        /// <param name="sensorPosition">The position of the sensor.</param>
        /// <param name="concentration">The concentration.</param>
        /// <param name="resolution">The sensor's resolution.  Not used.</param>
        public virtual void SetSensorCalGasConcentration( int sensorPosition, double concentration, double resolution )    
        {
            Driver.setSensorCalGasConcentration( sensorPosition, concentration );
        }

        /// <summary>
        /// Retrieves the sensor calibration gas code.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Calibration gas code for the specific sensor</returns>
        public string GetSensorCalGasCode( int sensorPosition )    
        {
            return Driver.getSensorCalGasCode( sensorPosition );
        }

        /// <summary>
        /// Sets the sensor calibration gas code.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Calibration gas code for the specific sensor</returns>
        public void SetSensorCalGasCode( int sensorPosition, string gasCode )    
        {
            Driver.setSensorCalGasCode( sensorPosition, gasCode );
        }

        /// <summary>
        /// Retrieves the sensor resolution.
        /// </summary>
        /// <param name="sensorPosition">Sensor position.</param>
        /// <returns>Sensor resolution.  e.g. 0.001</returns>
        public double GetSensorResolution( int sensorPosition )    
        {
            return Driver.getSensorResolution( sensorPosition ).Decimals;
        }

        /// <summary>
        /// Retrieves the sensor setup date.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor setup date</returns>
        public virtual DateTime GetSensorSetupDate( int sensorPosition )    
        {
            return Driver.getSensorSetupDate( sensorPosition );
        }

        /// <summary>
        /// Retrieves the configuration software version this sensor was programmed with.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns></returns>
        public string GetSensorSetupVersion( int sensorPosition )
        {
            string ver = Driver.getSensorConfigVersion( sensorPosition ).ToString();

            // driver will return zero if instrument has no programmed setupversion. So,
            // if it has no programmed setupversion, then we should return nothing, not "0".
            return ver == "0" ? string.Empty : ver;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sensorPosition"></param>
        /// <returns></returns>
        public string GetSensorHardwareVersion( int sensorPosition )
        {
            string ver = Driver.getSensorHardwareVersion( sensorPosition ).ToString();

            // driver will return zero if instrument has no programmed setupversion. So,
            // if it has no programmed setupversion, then we should return nothing, not "0".
            return ver == "0" ? string.Empty : ver;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sensorPosition"></param>
        /// <returns></returns>
        public string GetSensorSoftwareVersion( int sensorPosition )
        {
			return String.Empty;
        }

        /// <summary>
        /// Retrieves the sensor maximum span coefficient.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor span maximum coefficient</returns>
        public double GetSensorSpanCoeffMax( int sensorPosition )    
        {
            return Driver.getSensitivityHiLimit( sensorPosition );
        }

        /// <summary>
        /// Retrieves the sensor minimum span coefficient.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor span minimum coefficient</returns>
        public double GetSensorSpanCoeffMin( int sensorPosition )    
        {
            return Driver.getSensitivityLoLimit( sensorPosition );
        }

        /// <summary>
        /// Retrieves the sensor span coefficient.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor Span Coefficient</returns>
        public virtual double GetSensorSpanCoeff( int sensorPosition )    
        {
            return Driver.getSensorSensitivity( sensorPosition );
        }

        /// <summary>
        /// Sensor baseline.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns></returns>
        public virtual int GetSensorBaseline( int sensorPosition )    
        {
            return Driver.getSensorBaseline( sensorPosition );
        }

        /// <summary>
        /// Retrieves the sensor dead band value setting.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor dead band value setting</returns>
        public int GetSensorDeadBandValue( int sensorPosition, double resolution )  
        {
            double deadband = Driver.getDeadbandValue( sensorPosition );

            // The driver returns the deadband to us with the resolution applied to it.
            // (we get a decimal number; e.g. "0.4").  The original DS2 developers defined the field
            // as being an int, though, so we need to 'remove' the resolution.
            // e.g., if deadband is "0.4", and resolution is "0.1", we need to return "4".

            int ideadband = ( resolution == 0 ) ? (int)deadband : (int)( deadband / resolution );
            return ideadband;
        }

        /// <summary>
        /// Retrieves the sensor filtering magnitude value setting.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Sensor filter level value setting</returns>
        public int GetSensorFilterLevel( int sensorPosition )    
        {
            return Driver.getFilterLevel( sensorPosition );
        }

        /// <summary>
        /// Retrieves the sensor temperature low compensation value.
        /// GBPRO instrument has a whole array of temperature compensation
        /// values. DS2/iNet only supports one compensation value per instrument, though.
        /// To get around that (sort of), this routine will return a summation of the instrument's
        /// temperature compensation values.  This can at least be used in iNet as sort of a CRC
        /// to help determine if any of the temperature compensation values have become corrupted.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Summation of all values in instrument's temperature compensation table.</returns>
        public double GetSensorTemperatureLowCompensation( int sensorPosition )    
        {
            short compensationSummation = 0;
            
            for ( int i = 0; i < Driver.TemperatureCompensationTableSize; i++ )
                compensationSummation += Driver.getTemperatureCompensation( sensorPosition, i );

            return (double)compensationSummation;
        }

        /// <summary>
        /// Retrieves the sensor temperature high compensation value.
        /// GBPRO instrument has a whole array of temperature compensation
        /// values. DS2/iNet only supports one compensation value per instrument, though.
        /// To get around that (sort of), this routine will return a summation of the instrument's
        /// temperature compensation values.  This can at least be used in iNet as sort of a CRC
        /// to help determine if any of the temperature compensation values have become corrupted.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <returns>Summation of all values in instrument's temperature compensation table.</returns>
        public double GetSensorTemperatureHighCompensation( int sensorPosition )    
        {
            // just call LowCompensation routine which will do the summation work.
            return GetSensorTemperatureLowCompensation( sensorPosition);
        }

		/// <summary>
		/// Retrieves the sensor gas alert setting.
		/// </summary>
		/// <param name="sensorPosition">Sensor position</param>
		/// <param name="resolution">Alarm resolution.  Not used</param>
		/// <returns>Sensor gas alert setting</returns>
		public double GetSensorGasAlert( int sensorPosition, double resolution )
		{
			return Driver.Definition.HasGasAlertFeature ? Driver.getSensorAlarmGasAlert( sensorPosition ) : double.MinValue;
		}

		/// <summary>
		/// Set a sensor's gas alert setting.
		/// </summary>
		/// <param name="sensorPosition">The sensor's position.</param>
		/// <param name="setting">The setting for the sensor's gas alert.</param>
		/// <param name="resolution">The sensor's resolution.  Not used</param>
		public void SetSensorGasAlert( int sensorPosition, double setting, double resolution )
		{
			if ( Driver.Definition.HasGasAlertFeature )
			{
				Driver.setSensorAlarmGasAlert( sensorPosition, setting );
			}
		}

        /// <summary>
        /// Retrieves the sensor low alarm setting.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <param name="resolution">Alarm resolution.  Not used</param>
        /// <returns>Sensor low alarm setting</returns>
        public virtual double GetSensorLowAlarm( int sensorPosition, double resolution )    
        {
            return Driver.getSensorAlarmLow( sensorPosition );
        }

        /// <summary>
        /// Set a sensor's low alarm setting.
        /// </summary>
        /// <param name="sensorPosition">The sensor's position.</param>
        /// <param name="setting">The setting for the sensor's low alarm.</param>
        /// <param name="resolution">The sensor's resolution.  Not used</param>
        public void SetSensorLowAlarm( int sensorPosition, double setting, double resolution )    
        {
            Driver.setSensorAlarmLow( sensorPosition, setting );
        }

        /// <summary>
        /// Retrieves the sensor high alarm setting.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <param name="resolution">Alarm resolution.  Not used</param>
        /// <returns>Sensor high alarm setting</returns>
        public double GetSensorHighAlarm( int sensorPosition, double resolution )    
        {
            return Driver.getSensorAlarmHi( sensorPosition );
        }

        /// <summary>
        /// Set a sensor's high alarm setting.
        /// </summary>
        /// <param name="sensorPosition">The sensor's position.</param>
        /// <param name="setting">The setting for the sensor's high alarm.</param>
        /// <param name="resolution">The sensor's resolution. Not used</param>
        public void SetSensorHighAlarm( int sensorPosition, double setting, double resolution )    
        {
            Driver.setSensorAlarmHi( sensorPosition, setting );
        }

		/// <summary>
		/// Gets if the provided sensor position supports TWA functionality.
		/// </summary>
		/// <param name="sensorPosition">The sensor position to query.</param>
		/// <returns>True - if the sensor supports TWA functionality; False - if the sensor does not support TWA functionality</returns>
		public bool GetSensorTwaEnabled( int sensorPosition )
		{
			return Driver.getSensorTWAEnabled( sensorPosition );
		}

        /// <summary>
		/// Retrieves the sensor TWA alarm setting.  Call GetSensorTwaEnabled first, to ensure
		/// the sensor supports TWA functionality.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <param name="resolution">Alarm resolution. Not used</param>
        /// <returns>Sensor TWA alarm setting</returns>
        public double GetSensorTwaAlarm( int sensorPosition, double resolution )    
        {
			return Driver.getSensorAlarmTWA( sensorPosition );           
        }

        /// <summary>
        /// Set a sensor's TWA alarm setting. Call GetSensorTwaEnabled first, to ensure
		/// the sensor supports TWA functionality.
        /// </summary>
        /// <param name="sensorPosition">The sensor's position.</param>
        /// <param name="setting">The setting for the sensor's TWA alarm.</param>
        /// <param name="resolution">The sensor's resolution. Not used</param>
        public void SetSensorTwaAlarm( int sensorPosition, double setting, double resolution )    
        {
			Driver.setSensorAlarmTWA( sensorPosition, setting );
        }

		/// <summary>
		/// Gets if the provided sensor position supports STEL functionality.
		/// </summary>
		/// <param name="sensorPosition">The sensor position to query.</param>
		/// <returns>True - if the sensor supports STEL functionality; False - if the sensor does not support STEL functionality</returns>
		public bool GetSensorStelEnabled( int sensorPosition )
		{
			return Driver.getSensorSTELEnabled( sensorPosition );
		}

        /// <summary>
		/// Retrieves the sensor STEL alarm setting.  Call GetSensorTwaEnabled first, to ensure
		/// the sensor supports TWA functionality.
        /// </summary>
        /// <param name="sensorPosition">Sensor position</param>
        /// <param name="resolution">Alarm resolution. Not used</param>
        /// <returns>Sensor STEL alarm setting</returns>
        public double GetSensorStelAlarm( int sensorPosition, double resolution )    
        {
			return Driver.getSensorAlarmSTEL( sensorPosition );
        }

        /// <summary>
		/// Set a sensor's STEL alarm setting.  Call GetSensorStelEnabled first, to ensure
		/// the sensor supports STEL functionality.
        /// </summary>
        /// <param name="sensorPosition">The sensor's position.</param>
        /// <param name="setting">The setting for the sensor's STEL alarm.</param>
        /// <param name="resolution">The sensor's resolution. Not used</param>
        public void SetSensorStelAlarm( int sensorPosition, double setting, double resolution )    
        {
			Driver.setSensorAlarmSTEL( sensorPosition, setting );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public DomainModel.MeasurementType GetSensorMeasurementType( int pos )
        {
            // right now, there's a one-to-one mapping between the driver's
            // "MeasurementType" and the DS2's "MeasurementTypes" (note the plural).
            // Therefore, we can just cast from one to the other.
            return (DomainModel.MeasurementType)Driver.getSensorMeasurementType( pos );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="resolution"></param>
        /// <returns></returns>
        public double GetSensorZeroLimit( int pos, double resolution )
        {
            return Driver.getZeroLimit( pos );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ISC.iNet.DS.DomainModel.ChargePhase GetChargePhase()
        {
            // For now, a one-to-one relationship is assumed between the
            // ISC.Instrument.Driver.ChargePhase and ISC.iNet.DS.DomainModel.ChargePhase
            // enumerations.
            return (ISC.iNet.DS.DomainModel.ChargePhase)Driver.getChargePhase();
        }

        /// <summary>
        /// Determines whether or not the specified sensor's gas code can be configured
        /// Only PID and LEL sensors may have their gas code configured.  (Response factors
        /// and Correlation factors.)
        /// </summary>
        /// <param name="sensorCode"></param>
        /// <returns></returns>
        public bool IsSensorGasCodeConfigurable( string sensorCode )
        {
            return Driver.Definition.HasSensorGasCodeConfigFeature(sensorCode);
        }

        // SGF  Aug-04-2009  DSW-331 (rev. 2)
        public bool IsInstrumentResetting()
        {
            bool isResetting = Driver.isInstrumentResetting();
            if (isResetting == true)
                Log.Debug( "INSTRUMENT IS RESETTING" );
            return isResetting;
        }

        // SGF  Aug-04-2009  DSW-331 (rev. 2)
        public void SetCalibrationFault( bool isFault )
        {
            Log.Debug( "SetCalibrationFault: " + isFault.ToString() );
            Driver.setCalibrationFault( isFault );
        }

        // SGF  Nov-23-2009  DSW-355  (DS2 v7.6)
        public bool InSystemAlarm()
        {
            OperatingMode opMode = Driver.getOperatingMode();
            
            if ( opMode == OperatingMode.SystemAlarm )
            {
                Log.Debug("INSTRUMENT IS IN SYSTEM ALARM MODE");
                return true;
            }

            Log.Debug("Instrument is not in system alarm");

            // INS-3662 (dev) - Also look for GBPLS instruments in "CALL ISC" mode...
            if ( Configuration.DockingStation.Type == DeviceType.GBPLS
            &&   opMode == OperatingMode.PostShutdown )
            {
                Log.Debug( "INSTRUMENT IS IN POSTSHUTDOWN ('CALL ISC') MODE" );
                return true;
            }

            return false;
        }

        //Suresh 22-AUGUST-2011 INS-2179
        public bool IsInstrumentPumpInFault()
        {
            if (Driver.getInstrumentPumpStatus() == InstrumentPumpStatus.Fault)
                return true;
            return false; 
        }

        /// <summary>
        /// Suresh 15-SEPTEMBER-2011 INS-1593
        /// Gets the instrument current error code
        /// </summary>
        /// <returns>Instrument Current Error Code</returns>
        public int GetInstrumentSystemAlarmErrorCode()
        {
             return Driver.getSystemAlarmErrorCode();
        }

        /// <summary>
        /// Suresh 19-SEPTEMBER-2011 INS-1299
        /// Set's the instrument sensor bump fault flag
        /// </summary>
        /// <param name="pos">The Position of the sensor in the instrument</param>
        /// <param name="isFault">True if sensor is in bump fault, otherwise False</param>
        public virtual void SetSensorBumpFault(int pos, bool isFault)
        {
            Driver.setSensorBumpFault(pos, isFault);
        }

        /// <summary>
        /// clear the sensor's peaks.
        /// </summary>
        /// <param name="sensor">The sensor.</param>
        /// <param name="position">The position of the sensor.</param>
        public void ClearSensorPeaks(Sensor sensor, int position)
        {
            if (sensor.Type.Code == SensorCode.O2)
            {
                Driver.setPeakReading(position, 20.9);
            }
            else
            {
                Driver.setPeakReading(position, 0.0);
            }
        }

        /// <summary>\
        /// //Suresh 30-SEPTEMBER-2011 INS-2277
        /// Gets the instrument backlight timer interval
        /// </summary>
        /// <returns>Timeout in seconds</returns>
        public int GetBacklightTimeout()
        {
            if (!Driver.Definition.HasBacklightTimeoutConfigFeature)
                return 0;
            return Driver.getBacklightTimeout();
        }

        /// <summary>
        /// //Suresh 30-SEPTEMBER-2011 INS-2277
        /// Sets the instrument backlight timer timeout
        /// </summary>
        /// <param name="timeout">Timeout in seconds</param>
        public void SetBacklightTimeout(int timeout)
        {
            if (!Driver.Definition.HasBacklightTimeoutConfigFeature)
                return;
            Driver.setBacklightTimeout(timeout);
        }

        /// <summary>
        /// Determine the BIAS status of sensor
        /// </summary>
        /// <returns>true is all sensors in the instrument are ready for Bump / Cal</returns>
        /// INS-7657 RHP v7.5.2
        public virtual bool GetSensorBiasStatus()
        {
            return Driver.getSensorBiasStatus() == SensorBiasStatus.Ready ;
        }

        #endregion Methods

    }  // end-class-InstrumentController

}  // end-namespace
