using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE;
using ISC.WinCE.Logger;
using TimeZoneInfo = ISC.iNet.DS.DomainModel.TimeZoneInfo;


namespace ISC.iNet.DS
{
    public sealed partial class Configuration
    {
        private static readonly string FILE_NAME_CONFIG_INFO = Controller.PERSISTANCE_PATH + "config.properties";

        private static readonly string FILE_NAME_FACTORY_INFO = Controller.PERSISTANCE_PATH + "factory.properties";
        private static readonly string FILE_NAME_FACTORY_BACK = Controller.PERSISTANCE_PATH + "factory.properties.bak";

        /// <summary>
        /// Part number for DSX Ventis-LS docking stations.
        /// </summary>
        public const string VENTISLS_PARTNUMBER = "1810-9328"; 

        // Factory defaults...
        public const string DEFAULT_LANGUAGE_CODE = Language.English;
        public const bool DEFAULT_MENU_LOCKED = false;
        public const bool DEFAULT_AUDIBLE_ALARM = true;
        public const bool DEFAULT_WEBAPP_ENABLED = true;
        public const string DEFAULT_WEBAPP_PASSWORD = "DSX";
        public const short DEFAULT_INET_PING_INTERVAL = 900; // 5 minutes
        /// <summary>
        /// 100 seconds is .Net's default timeout
        /// </summary>
        public const int  DEFAULT_INET_TIMEOUT_LOW = 100;
        /// <summary>
        /// 30 minutes.  Need it it long enough to be able to download ALL settings after a factory reset.
        /// <para>
        /// This is the old legacy default we used to use for ALL web service calls.  It's DS2's default, too.
        /// </para>
        /// </summary>
        public const int  DEFAULT_INET_TIMEOUT_MEDIUM = 1800;  
        /// <summary>
        /// 30 minutes.  This is the old legacy default we used to use for ALL web service calls.  It's DS2's default, too.
        /// </summary>
        public const int  DEFAULT_INET_TIMEOUT_HIGH = 1800;

#if DEBUG
        // inetdev (v7.6)
		public const string DEFAULT_INET_URL = "https://inetuploaddev01.indsci.com/UploadWeb/secureServices";
		public const string DEFAULT_INET_USER = "inetdev";
		public const string DEFAULT_INET_PASSWD = "inetdev1";

		// inetdev (v7.3)
        //public const string DEFAULT_INET_URL = "https://inetwasdev1.pitnet.com:9443/UploadWeb/secureServices";
        //public const string DEFAULT_INET_USER = "inetdev";
        //public const string DEFAULT_INET_PASSWD = "inetdev1";
		
		// Russ's Computer (v6.1)
		//public const string DEFAULT_INET_URL = "https://an2831.pitnet.com:9446/UploadWeb/secureServices";
		//public const string DEFAULT_INET_USER = "";
		//public const string DEFAULT_INET_PASSWD = "";

#elif INET_QA // QA RELEASE BUILD

        // QA (externally facing)
        // Also use this configuration for TEST images
        //public const string DEFAULT_INET_URL = "http://inetqaupload.indsci.com/UploadWeb/secureServices";
        //public const string DEFAULT_INET_USER = "inetuserqa";
        //public const string DEFAULT_INET_PASSWD = "inetuserqa1";

        // QA (internal)
        // Can also use this configuration for TEST images
        public const string DEFAULT_INET_URL = "https://inetuploadqa00.indsci.com/UploadWeb/secureServices";
        public const string DEFAULT_INET_USER = "inetuserqa";
        public const string DEFAULT_INET_PASSWD = "inetuserqa1";

#else // RELEASE BUILD

        // Production (externally facing)
        public const string DEFAULT_INET_URL = "https://inetupload.indsci.com/UploadWeb/secureServices";
        public const string DEFAULT_INET_USER = "inetuser";
        public const string DEFAULT_INET_PASSWD = "G0dz1ll@";

        // Other configurations
        //public const string DEFAULT_INET_URL = "https://inet07.pitnet.com:443/UploadWeb/secureServices"; // Production (no firewall)
#endif

        public const string DEFAULT_INET_PROXY = ""; // factory default is we don't use a proxy
        public const string DEFAULT_INET_PROXY_USER = ""; // factory default is we don't use a proxy
        public const string DEFAULT_INET_PROXY_PASSWD = ""; // factory default is we don't use a proxy
        public const int DEFAULT_LOG_CAPACITY = Log.DEFAULT_CAPACITY; // factory default is to use Log's default
        public const LogLevel DEFAULT_LOG_LEVEL = Log.DEFAULT_LEVEL; // factory default is to use Log's default
#if DEBUG		
		public const bool DEFAULT_LOG_TO_SERIALPORT = true; // for DEV log messages by default
#elif INET_QA
		public const bool DEFAULT_LOG_TO_SERIALPORT = false; // for QA log messages by default - EXCEPT IN v6.3
#else
		public const bool DEFAULT_LOG_TO_SERIALPORT = false; // factory default is to not log debug messages to the serial port
#endif
		public const bool DEFAULT_LOG_TO_FILE = false;
		public const bool DEFAULT_DHCP_ENABLED = true;
        public const string DEFAULT_SUBNET_MASK = "255.255.255.0";
        public const string DEFAULT_GATEWAY = "";
        public const string DEFAULT_IP_ADDRESS = "";
        public const string DEFAULT_DNS_PRIMARY = "";
        public const string DEFAULT_DNS_SECONDARY = "";
        public const bool DEFAULT_PRINT_PERFORMED_BY = false;
        public const bool DEFAULT_PRINT_RECEIVED_BY = false;
        public const bool DEFAULT_PURGE_AFTER_BUMP = false;
        public static readonly TimeZoneInfo DEFAULT_TIMEZONEINFO = TimeZoneInfo.GetEastern();
        // By default, Port 1 is restricted to fresh air or zero air and the other ports have no restrictions.
        public const PortRestrictions DEFAULT_PORT1RESTRICTIONS = PortRestrictions.FreshAir | PortRestrictions.ZeroAir;
        public const bool DEFAULT_CLEAR_PEAK_UPON_DOCKINGINSTRUMENT = false; 
        public const bool DEFAULT_SINGLE_SENSOR_MODE = false; 
        public const bool DEFAULT_USE_EXPIRED_CYLINDERS = false;
        public const string DEFAULT_COMBUSTIBLE_BUMP_TEST_GAS = "";
        public const double DEFAULT_SPAN_RESERVE_THRESHOLD = double.MinValue;
        public const CalStationGasSchedule DEFAULT_CALSTATION_GAS_SCHEDULE = CalStationGasSchedule.BumpUponDocking;
		public const bool DEFAULT_CALSTATION_DATALOG_SCHEDULE = true;
        public const bool DEFAULT_RESERVOIR = true;
        public const int DEFAULT_NUM_GAS_PORTS = DockingStation.LEGACY_NUM_GAS_PORTS;
        public const bool DEFAULT_STOP_ON_FAILED_BUMP_TEST = false;
        public const bool DEFAULT_UPGRADE_ON_ERROR_FAIL = false;

        // Attribute names for the PropertiesFile.
        //private const string PROPNAME_REF_ID = "refId";
        private const string PROPNAME_PART_NUMBER = "partNumber";
        private const string PROPNAME_SERIAL_NUMBER = "serialNumber";
        private const string PROPNAME_TYPE = "type";
        private const string PROPNAME_FLOW_OFFSET = "flowOffset";
        private const string PROPNAME_SETUP_DATE = "setupDate";
        private const string PROPNAME_SETUP_TECH = "setupTech";
        private const string PROPNAME_HARDWARE_VERSION = "hardwareVersion";
        private const string PROPNAME_LANGUAGE_CODE = "languageCode";
        private const string PROPNAME_MENU_LOCKED = "menuLocked";
        private const string PROPNAME_USE_AUDIBLE_ALARM = "useAudibleAlarm";
        private const string PROPNAME_LOG_CAPACITY = "logCapacity";
        private const string PROPNAME_LOG_LEVEL = "logLevel";
		private const string PROPNAME_LOG_TO_SERIALPORT = "logToSerialPort";
		private const string PROPNAME_LOG_TO_FILE = "logToFile";
        private const string PROPNAME_INET_URL = "inetUrl";
        private const string PROPNAME_INET_USERNAME = "inetUserName";
        private const string PROPNAME_INET_PASSWORD = "inetPassword";
        private const string PROPNAME_INET_PING_INTERVAL = "inetPingInterval";
        private const string PROPNAME_INET_TIMEOUT_LOW = "inetTimeoutLow";
        private const string PROPNAME_INET_TIMEOUT_MEDIUM = "inetTimeoutMedium";
        private const string PROPNAME_INET_TIMEOUT_HIGH = "inetTimeoutHigh";
        private const string PROPNAME_INET_OFFLINE_DEADBAND = "inetOfflineDeadband";
        private const string PROPNAME_INET_PROXY = "inetProxy";
        private const string PROPNAME_INET_PROXY_USERNAME = "inetProxyUserName";
        private const string PROPNAME_INET_PROXY_PASSWORD = "inetProxyPassword";
        private const string PROPNAME_NUM_GAS_PORTS = "numGasPorts";
        private const string PROPNAME_WEB_APP_ENABLED = "webAppEnabled";
        private const string PROPNAME_WEB_APP_PASSWORD = "webAppPassword";
        private const string PROPNAME_PORT1_RESTRICTIONS = "port1Restrictions";
        private const string PROPNAME_PRINT_PERFORMED_BY = "printPerformedBy";
        private const string PROPNAME_PRINT_RECEIVED_BY = "printReceivedBy";
        private const string PROPNAME_PURGE_AFTER_BUMP = "purgeAfterBump";
        private const string PROPNAME_RESERVOIR = "reservoir";
        private const string PROPNAME_TZI_BIAS = "tziBias";
        private const string PROPNAME_TZI_STANDARD_NAME = "tziStandardName";
        private const string PROPNAME_TZI_STANDARD_BIAS = "tziStandardBias";
        private const string PROPNAME_TZI_STANDARD_DATE_MONTH = "tziStandardDateMonth";
        private const string PROPNAME_TZI_STANDARD_DATE_DAY = "tziStandardDateDay";
        private const string PROPNAME_TZI_STANDARD_DATE_DAYOFWEEK = "tziStandardDateDayOfWeek";
        private const string PROPNAME_TZI_STANDARD_DATE_HOUR = "tziStandardDateHour";
        private const string PROPNAME_TZI_DAYLIGHT_NAME = "tziDaylightName";
        private const string PROPNAME_TZI_DAYLIGHT_BIAS = "tziDaylightBias";
        private const string PROPNAME_TZI_DAYLIGHT_DATE_MONTH = "tziDaylightDateMonth";
        private const string PROPNAME_TZI_DAYLIGHT_DATE_DAY = "tziDaylightDateDay";
        private const string PROPNAME_TZI_DAYLIGHT_DATE_DAYOFWEEK = "tziDaylightDateDayOfWeek";
        private const string PROPNAME_TZI_DAYLIGHT_DATE_HOUR = "tziDaylightDateHour";
        private const string PROPNAME_CLEAR_PEAKS_UPON_DOCKING = "clearPeakUponDockingInstrument";
        private const string PROPNAME_SINGLE_SENSOR_MODE = "allowSingleSensorMode"; 
        private const string PROPNAME_USE_EXPIRED_CYLINDERS = "useExpiredCylinders"; // "bump" is in the string in case we also add "cal" in a later enhancement.
        private const string PROPNAME_COMBUSTIBLE_BUMP_TEST_GAS = "combustibleBumpTestGas";
        private const string PROPNAME_SPAN_RESERVE_THRESHOLD = "spanReserveThreshold";
        private const string PROPNAME_CALSTATION_GAS_SCHEDULE = "calStationSchedule";
		private const string PROPNAME_CALSTATION_DATALOG_SCHEDULE = "calStationDatalogSchedule";
        private const string PROPNAME_STOP_ON_FAILED_BUMP_TEST = "stopOnFailedBumpTest";
        private const string PROPNAME_UPGRADE_ON_ERROR_FAIL = "upgradeOnErrorFail";
        private const string WEBAPP_XPATH = "configuration/WebServer";
        private const string WEBAPP_XPATH_USER = WEBAPP_XPATH + "/Authentication/Users/User";


        private static DockingStation _dockingStation = new DockingStation();

        private static Schema _schema = new Schema();

        private static bool _configError = false;
        
        public static bool ServiceMode { get; set; }

        public static void ResetToDefaults()
        {   
            // Clone the current instance instead of instantiating
            // a new one so that the new instance retains the serialization information.
            DockingStation newDockingStation = (DockingStation)_dockingStation.Clone();

            SetToFactoryDefaults( newDockingStation );

            // Now that we've set up the new instance, assign its reference to the old instance.
            _dockingStation = newDockingStation;

            SaveConfiguration();
        }

        /// <summary>
        /// Returns true if current account is a "repair" account.
        /// </summary>
        /// <returns></returns>
        public static bool IsRepairAccount()
        {
            return Schema.ServiceCode == "REPAIR";
        }

        /// <summary>
        /// Returns whether or not the docking station is currently set to "single sensor mode".
        /// <para>
        /// Always returns false for repair (service) accounts. Otherwise, returns DockingStation.SingleSensorMode property's true/false setting.
        /// </para>
        /// </summary>
        public static bool IsSingleSensorMode()
        {
            if ( IsRepairAccount() )
                return false;

            return DockingStation.SingleSensorMode;
        }

		public static void ResetInet()
		{
			// Clone the current instance instead of instantiating a new one
			// so that the new instance retains the serialization information.
			DockingStation newDockingStation = (DockingStation)_dockingStation.Clone();

			SetInetDefaults( newDockingStation );
			SetLoggingDefaults( newDockingStation );

			// Now that we've set up the new instance, assign its reference to the old instance.
			_dockingStation = newDockingStation;

			SaveConfiguration();

			// When the account or activation status changes, logging settings also get reset to their defaults.
			// So we apply the new settings here because a Settings Update will not occur when the DSX
			// is deactivated, and when the account does change, UseDockingStation will not be set to true.
			ApplyLogSettings( false );
		}

		/// <summary>
		/// This method should only be called by code that updates any of the logging settings stored
		/// in Configuration.DockingStation.  It does not need called for ResetToDefaults() since 
		/// Controller.PerformSoftReset() will be called shortly after in the FactoryResetOperation.
		/// </summary>
		public static void ApplyLogSettings( bool isStarting )
		{
			Log.Level = Configuration.DockingStation.LogLevel;
			Log.Capacity = Configuration.DockingStation.LogCapacity;

			// when the DSX is starting up, we want to log all the initialization messages
			if ( !isStarting )
				Log.LogToSerialPort = Configuration.ServiceMode ? true : DockingStation.LogToSerialPort; // always log in service mode

			Log.LogToFile = DockingStation.LogToFile;
		}

        private Configuration()
        {
			Log.Assert( "Configuration constructor should not be called." );
        }


        /// <summary
        /// Sets the passed-in DockingStation's properties that are set through the Configurator and iNet to the factory defaults.
        /// </summary>
        private static void SetToFactoryDefaults( DockingStation ds )
        {
			SetConfiguratorDefaults( ds );
			SetInetDefaults( ds );
        }

		/// <summary>
		/// This method is only called by SetToFactoryDefaults.
		/// </summary>
		private static void SetConfiguratorDefaults( DockingStation ds )
		{
			ds.WebAppEnabled = DEFAULT_WEBAPP_ENABLED;
			ds.WebAppPassword = DEFAULT_WEBAPP_PASSWORD;

            ds.InetUrl = DEFAULT_INET_URL;
            ds.InetUserName = DEFAULT_INET_USER;
            ds.InetPassword = DEFAULT_INET_PASSWD;

			ds.InetProxy = DEFAULT_INET_PROXY;
			ds.InetProxyUserName = DEFAULT_INET_PROXY_USER;
			ds.InetProxyPassword = DEFAULT_INET_PROXY_PASSWD;

			ds.NetworkSettings.DhcpEnabled = DEFAULT_DHCP_ENABLED;
			ds.NetworkSettings.SubnetMask = DEFAULT_SUBNET_MASK;
			ds.NetworkSettings.Gateway = DEFAULT_GATEWAY;
			ds.NetworkSettings.IpAddress = DEFAULT_IP_ADDRESS;
			ds.NetworkSettings.DnsPrimary = DEFAULT_DNS_PRIMARY;
			ds.NetworkSettings.DnsSecondary = DEFAULT_DNS_SECONDARY;

			ds.LogToSerialPort = DEFAULT_LOG_TO_SERIALPORT;
			ds.LogToFile = DEFAULT_LOG_TO_FILE;
		}

		/// <summary>
		/// Sets the passed-in DockingStation's properties that are set by iNet to the factory defaults.
		/// </summary>
		private static void SetInetDefaults( DockingStation ds )
		{
			ds.Language.Code = DEFAULT_LANGUAGE_CODE;
			ds.MenuLocked = DEFAULT_MENU_LOCKED;
			ds.UseAudibleAlarm = DEFAULT_AUDIBLE_ALARM;
			
			ds.InetPingInterval = DEFAULT_INET_PING_INTERVAL;
			ds.InetTimeoutLow = DEFAULT_INET_TIMEOUT_LOW;
			ds.InetTimeoutMedium = DEFAULT_INET_TIMEOUT_MEDIUM;
			ds.InetTimeoutHigh = DEFAULT_INET_TIMEOUT_HIGH;
			
			ds.LogCapacity = DEFAULT_LOG_CAPACITY;
			ds.LogLevel = DEFAULT_LOG_LEVEL;
			
			ds.PrintPerformedBy = DEFAULT_PRINT_PERFORMED_BY;
			ds.PrintReceivedBy = DEFAULT_PRINT_RECEIVED_BY;
			ds.TimeZoneInfo = DEFAULT_TIMEZONEINFO;
			ds.Port1Restrictions = DEFAULT_PORT1RESTRICTIONS;
			ds.PurgeAfterBump = DEFAULT_PURGE_AFTER_BUMP;
			ds.ClearPeaksUponDocking = DEFAULT_CLEAR_PEAK_UPON_DOCKINGINSTRUMENT;

			if ( ds.Type == DeviceType.SC )
				ds.SingleSensorMode = true; // SafeCore Single-Sensor Mode default is to be enabled.
			else
				ds.SingleSensorMode = DEFAULT_SINGLE_SENSOR_MODE;
			
			ds.UseExpiredCylinders = DEFAULT_USE_EXPIRED_CYLINDERS;
			ds.CombustibleBumpTestGas = DEFAULT_COMBUSTIBLE_BUMP_TEST_GAS;
            ds.SpanReserveThreshold = DEFAULT_SPAN_RESERVE_THRESHOLD;
			ds.CalStationGasSchedule = DEFAULT_CALSTATION_GAS_SCHEDULE;
			ds.CalStationDatalogScheduleEnabled = DEFAULT_CALSTATION_DATALOG_SCHEDULE;
            ds.StopOnFailedBumpTest = DEFAULT_STOP_ON_FAILED_BUMP_TEST;
            ds.UpgradeOnErrorFail = DEFAULT_UPGRADE_ON_ERROR_FAIL;
		}

		/// <summary>
		/// This method is called when the account or activation status changes.  This method 
		/// was needed since LogToSerialPort and LogToFile are not iNet controlled settings.
		/// </summary>
		private static void SetLoggingDefaults( DockingStation ds )
		{
			ds.LogToSerialPort = DEFAULT_LOG_TO_SERIALPORT;
			ds.LogToFile = DEFAULT_LOG_TO_FILE;
		}

#region Properties
       
        /// <summary>
        /// Returns the instance of the Configuration singleton. 
		/// The returned object should not be expected to have properties
		/// set that are not stored in the configuration files.  For instance,
		/// the firmware version or network settings will not be set.  Use
		/// Controller.GetDockingStation() instead when non-configuration properties
		/// will be used.
        /// </summary>
        public static DockingStation DockingStation
        {
            get { return _dockingStation; }
            set { _dockingStation = value; }
        }

        public static Schema Schema
        {
            get { return _schema; }
            set { _schema = value; }
        }

        public static bool HasConfigurationError
        {
            get { return _configError;  }
            set { _configError = value; }
        }

#endregion Properties

        /// <summary>
        /// Determines docking station type and retrieve its information.
        /// </summary>
        public static void Load()
        {
            Log.Debug( "Loading docking station serialization & configuration..." );

            //lock ( Lock )
            {
				// First load in the serialization info.
				LoadSerialization( 1 );

				// Initialize docking station defaults.
                SetToFactoryDefaults( DockingStation );          

                // Then load in the configuration info.
                LoadConfiguration();

                // INS-7008:  If docking station is MX4, verify whether dock has new Ventis cradle
                if (DockingStation.Type == DeviceType.MX4)
                    DockingStation.HasNewVentisCradle = Controller.IsNewVentisCradle();

                Log.Debug( Log.Dashes );

                LogSerialization( DockingStation );

                LogConfiguration( DockingStation );

                Log.Debug( Log.Dashes );

                Log.Debug( "Factory Defaults..." );
                Log.Debug( "             iNet URL: " + Configuration.DEFAULT_INET_URL );
                Log.Debug( "       iNet User Name: " + Configuration.DEFAULT_INET_USER );
                Log.Debug( "        iNet Password: " + Configuration.DEFAULT_INET_PASSWD );

                Log.Debug( Log.Dashes );
            }
        }

        public static void LogSerialization( DockingStation ds )
        {
			Log.Debug( "             Serial Number: " + ds.SerialNumber );
			Log.Debug( "                      Type: " + ds.Type );
			Log.Debug( "               Part Number: " + ds.PartNumber );
			Log.Debug( "                Setup Date: " + ds.SetupDate.ToShortDateString() );
			Log.Debug( "                Setup Tech: " + ds.SetupTech );
			Log.Debug( "                 Gas Ports: " + ds.NumGasPorts );
			Log.Debug( "                 Reservoir: " + ds.Reservoir );
			Log.Debug( "               Flow Offset: " + ( ( ds.FlowOffset == int.MinValue ) ? "None" : ds.FlowOffset.ToString() ) );
        }

        private static void LoadConfiguration()
        {
            LoadBootVars();

            PropertiesFile propertiesFile = new PropertiesFile( FILE_NAME_CONFIG_INFO );

            try
            {
                propertiesFile.Load();
            }
            catch ( Exception e )
            {
                // It's expected that the file won't be found. So don't log the whole stack trace.
                if ( e is FileNotFoundException )
                    Log.Warning( "Unable to load configuration file - " + e.Message );
                else
                    Log.Warning( "Unable to load configuration file.", e );

                HasConfigurationError = true;
                return;
            }

            try
            {
				// Set properties on a local DockingStation instance.  Once
				// we set them all, we assign the global instance to this local instance.
				DockingStation defaults = new DockingStation();

				SetToFactoryDefaults( defaults );

                DockingStation.Language.Code = GetString( propertiesFile, PROPNAME_LANGUAGE_CODE, defaults.Language.Code );
                DockingStation.MenuLocked = Parse( propertiesFile, PROPNAME_MENU_LOCKED, defaults.MenuLocked );
                DockingStation.UseAudibleAlarm = Parse( propertiesFile, PROPNAME_USE_AUDIBLE_ALARM, defaults.UseAudibleAlarm );

				DockingStation.WebAppPassword = GetString( propertiesFile, PROPNAME_WEB_APP_PASSWORD, defaults.WebAppPassword );
				DockingStation.WebAppEnabled = Parse( propertiesFile, PROPNAME_WEB_APP_ENABLED, defaults.WebAppEnabled );

				DockingStation.InetUrl = GetString( propertiesFile, PROPNAME_INET_URL, defaults.InetUrl );
				DockingStation.InetPingInterval = Parse( propertiesFile, PROPNAME_INET_PING_INTERVAL, defaults.InetPingInterval );
				DockingStation.InetTimeoutLow = Parse( propertiesFile, PROPNAME_INET_TIMEOUT_LOW, defaults.InetTimeoutLow );
				DockingStation.InetTimeoutMedium = Parse( propertiesFile, PROPNAME_INET_TIMEOUT_MEDIUM, defaults.InetTimeoutMedium );
				DockingStation.InetTimeoutHigh = Parse( propertiesFile, PROPNAME_INET_TIMEOUT_HIGH, defaults.InetTimeoutHigh );
				DockingStation.InetUserName = GetString( propertiesFile, PROPNAME_INET_USERNAME, defaults.InetUserName );
				DockingStation.InetPassword = GetString( propertiesFile, PROPNAME_INET_PASSWORD, defaults.InetPassword );

				DockingStation.InetProxy = GetString( propertiesFile, PROPNAME_INET_PROXY, defaults.InetProxy );
				DockingStation.InetProxyUserName = GetString( propertiesFile, PROPNAME_INET_PROXY_USERNAME, defaults.InetProxyUserName );
				DockingStation.InetProxyPassword = GetString( propertiesFile, PROPNAME_INET_PROXY_PASSWORD, defaults.InetProxyPassword );

                // In the future, we may add more LogLevels.  We therefore make the code sensitive to the
                // fact that it may be unable to parse the loglevel because it's an older version of the code.
                try
                {
                    DockingStation.LogLevel = (LogLevel)Enum.Parse( typeof( LogLevel ), propertiesFile[PROPNAME_LOG_LEVEL], true );
                }
                catch ( Exception e )
                {
                    LogPropertyError( PROPNAME_LOG_LEVEL, defaults.LogLevel, e );
                    DockingStation.LogLevel = defaults.LogLevel;
                }
				DockingStation.LogCapacity = Parse( propertiesFile, PROPNAME_LOG_CAPACITY, defaults.LogCapacity );
				DockingStation.LogToSerialPort = Parse( propertiesFile, PROPNAME_LOG_TO_SERIALPORT, defaults.LogToSerialPort );
				DockingStation.LogToFile = Parse( propertiesFile, PROPNAME_LOG_TO_FILE, defaults.LogToFile );

				try
				{
					DockingStation.CalStationGasSchedule = (CalStationGasSchedule)Enum.Parse( typeof( CalStationGasSchedule ), propertiesFile[PROPNAME_CALSTATION_GAS_SCHEDULE], true );
				}
				catch ( Exception e )
				{
					Log.Warning( "Due to parse error, defaulting CalStationGasSchedule to " + defaults.CalStationGasSchedule, e );
					DockingStation.CalStationGasSchedule = defaults.CalStationGasSchedule;
				}
				DockingStation.CalStationDatalogScheduleEnabled = Parse( propertiesFile, PROPNAME_CALSTATION_DATALOG_SCHEDULE, defaults.CalStationDatalogScheduleEnabled ); 

                try
                {
                    DockingStation.Port1Restrictions = (PortRestrictions)Enum.Parse( typeof( PortRestrictions ), propertiesFile[PROPNAME_PORT1_RESTRICTIONS], true );
                }
                catch ( Exception e )
                {
                    LogPropertyError( PROPNAME_PORT1_RESTRICTIONS, defaults.Port1Restrictions, e );
                    DockingStation.Port1Restrictions = defaults.Port1Restrictions;
                }

                DockingStation.PurgeAfterBump = Parse( propertiesFile, PROPNAME_PURGE_AFTER_BUMP, defaults.PurgeAfterBump );
                DockingStation.ClearPeaksUponDocking = Parse(propertiesFile, PROPNAME_CLEAR_PEAKS_UPON_DOCKING, defaults.ClearPeaksUponDocking);
                DockingStation.SingleSensorMode = Parse(propertiesFile, PROPNAME_SINGLE_SENSOR_MODE, defaults.SingleSensorMode); 
                DockingStation.UseExpiredCylinders = Parse( propertiesFile, PROPNAME_USE_EXPIRED_CYLINDERS, defaults.UseExpiredCylinders ); 
                DockingStation.CombustibleBumpTestGas = propertiesFile[ PROPNAME_COMBUSTIBLE_BUMP_TEST_GAS ];
                DockingStation.SpanReserveThreshold = Parse( propertiesFile, PROPNAME_SPAN_RESERVE_THRESHOLD, defaults.SpanReserveThreshold );
                
				DockingStation.PrintPerformedBy = Parse( propertiesFile, PROPNAME_PRINT_PERFORMED_BY, defaults.PrintPerformedBy );
                DockingStation.PrintReceivedBy = Parse( propertiesFile, PROPNAME_PRINT_RECEIVED_BY, defaults.PrintReceivedBy );

                DockingStation.StopOnFailedBumpTest = Parse(propertiesFile, PROPNAME_STOP_ON_FAILED_BUMP_TEST, defaults.StopOnFailedBumpTest);
                DockingStation.UpgradeOnErrorFail = Parse(propertiesFile, PROPNAME_UPGRADE_ON_ERROR_FAIL, defaults.UpgradeOnErrorFail); 

                TimeZoneInfo tzi;
                try
                {
                    SystemTime stdDate = new SystemTime();
                    stdDate.Month = short.Parse( propertiesFile[PROPNAME_TZI_STANDARD_DATE_MONTH] );
                    stdDate.Day = short.Parse( propertiesFile[PROPNAME_TZI_STANDARD_DATE_DAY] );
                    stdDate.Hour = short.Parse( propertiesFile[PROPNAME_TZI_STANDARD_DATE_HOUR] );
                    stdDate.DayOfWeek = short.Parse( propertiesFile[PROPNAME_TZI_STANDARD_DATE_DAYOFWEEK] );

                    SystemTime dstDate = new SystemTime();
                    dstDate.Month = short.Parse( propertiesFile[PROPNAME_TZI_DAYLIGHT_DATE_MONTH] );
                    dstDate.Day = short.Parse( propertiesFile[PROPNAME_TZI_DAYLIGHT_DATE_DAY] );
                    dstDate.Hour = short.Parse( propertiesFile[PROPNAME_TZI_DAYLIGHT_DATE_HOUR] );
                    dstDate.DayOfWeek = short.Parse( propertiesFile[PROPNAME_TZI_DAYLIGHT_DATE_DAYOFWEEK] );

                    tzi = new TimeZoneInfo( int.Parse( propertiesFile[PROPNAME_TZI_BIAS] ), 
                                            propertiesFile[PROPNAME_TZI_STANDARD_NAME],
                                            stdDate,
                                            int.Parse( propertiesFile[PROPNAME_TZI_STANDARD_BIAS] ),
                                            propertiesFile[PROPNAME_TZI_DAYLIGHT_NAME],
                                            dstDate, 
                                            int.Parse( propertiesFile[PROPNAME_TZI_DAYLIGHT_BIAS] ) );
                }
                catch ( Exception e )
                {
                    LogPropertyError( "TimeZoneInfo", "Eastern", e );
                    tzi = TimeZoneInfo.GetEastern();
                }
                DockingStation.TimeZoneInfo = tzi;
            }
            catch ( Exception e )
            {
                Log.Error( "Error loading configuration", e );

                HasConfigurationError = true;
            }
        }

        private static void LoadBootVars()
        {
            BootVars bootVars;

            try
            {
                bootVars = BootVars.Load();
            }
            catch ( Exception e )
            {
                Log.Error( "Error loading BootVars", e );

                HasConfigurationError = true;
                return;
            }

            DockingStation.NetworkSettings.MacAddress = bootVars.MacAddress;
            DockingStation.NetworkSettings.Gateway = bootVars.Gateway;
            DockingStation.NetworkSettings.SubnetMask = bootVars.SubnetMask;
            DockingStation.NetworkSettings.IpAddress = bootVars.IpAddress;
            DockingStation.NetworkSettings.DhcpEnabled = bootVars.DhcpEnabled;
            DockingStation.NetworkSettings.DnsPrimary = bootVars.DnsPrimary;
            DockingStation.NetworkSettings.DnsSecondary = bootVars.DnsSecondary;
        }

        private static string GetString( PropertiesFile propertiesFile, string property, string defaultValue )
        {
            string stringValue = propertiesFile[ property ];

            if ( stringValue == null )
            {
                LogPropertyError( property, defaultValue );
                stringValue = defaultValue;
            }

            return stringValue;
        }

        private static bool Parse( PropertiesFile propertiesFile, string property, bool defaultValue )
        {
            try
            {
                return bool.Parse( propertiesFile[ property ] );
            }
            catch ( Exception e )
            {
                LogPropertyError( property, defaultValue, e );
                return defaultValue;
            }
        }

        private static int Parse( PropertiesFile propertiesFile, string property, int defaultValue )
        {
            try
            {
                return int.Parse( propertiesFile[ property ] );
            }
            catch ( Exception e )
            {
                LogPropertyError( property, defaultValue, e );
                return defaultValue;
            }
        }

        private static short Parse( PropertiesFile propertiesFile, string property, short defaultValue )
        {
            try
            {
                return short.Parse( propertiesFile[ property ] );
            }
            catch ( Exception e )
            {
                LogPropertyError( property, defaultValue, e );
                return defaultValue;
            }
        }

        private static long Parse( PropertiesFile propertiesFile, string property, long defaultValue )
        {
            try
            {
                return long.Parse( propertiesFile[property] );
            }
            catch ( Exception e )
            {
                LogPropertyError( property, defaultValue, e );
                return defaultValue;
            }
        }

        private static double Parse( PropertiesFile propertiesFile, string property, double defaultValue )
        {
            try
            {
                return double.Parse( propertiesFile[property] );
            }
            catch ( Exception e )
            {
                LogPropertyError( property, defaultValue, e );
                return defaultValue;
            }
        }

        private static void LogPropertyError( string property, object defaultValue )
        {
            Log.Warning( string.Format( "Unable to load \"{0}\" property. Defaulting to \"{1}\"", property, defaultValue ) );
        }

        private static void LogPropertyError( string property, object defaultValue, Exception e )
        {
            if ( Log.Level > LogLevel.Debug )
                Log.Warning( string.Format( "Unable to load \"{0}\". Defaulting to \"{1}\"", property, defaultValue ), e );
            else
                Log.Warning( string.Format( "Unable to load \"{0}\". Defaulting to \"{1}\"", property, defaultValue ) );
        }

        public static void LogConfiguration( DockingStation ds )
        {
            //Log.Debug( "                RefId: " + ( ds.RefId == DomainModelConstant.NullId ? "NullId" : ds.RefId.ToString() ) );
			Log.Debug( "             Language Code: " + ds.Language.Code );
			Log.Debug( "               Menu Locked: " + ds.MenuLocked );
			Log.Debug( "        Use Audible Alarms: " + ds.UseAudibleAlarm );
            Log.Debug( "     Has New Ventis Cradle: " + ds.HasNewVentisCradle);
			Log.Debug( "                  iNet URL: " + ds.InetUrl );
			Log.Debug( "            iNet User Name: " + ds.InetUserName );
			Log.Debug( "             iNet Password: " + ds.InetPassword );
			Log.Debug( "        iNet Ping Interval: " + ds.InetPingInterval );
			Log.Debug( "          iNet Low Timeout: " + ds.InetTimeoutLow );
			Log.Debug( "       iNet Medium Timeout: " + ds.InetTimeoutMedium );
			Log.Debug( "         iNet High Timeout: " + ds.InetTimeoutHigh );
#if DEBUG
			Log.Debug( "                iNet Proxy: " + ds.InetProxy );
			Log.Debug( "      iNet Proxy User Name: " + ds.InetProxyUserName );
			Log.Debug( "       iNet Proxy Password: " + ds.InetProxyPassword );
#else // don't show these values in Release mode.  It's considered sensitive data.
            Log.Debug( "                iNet Proxy: " + string.Empty.PadRight( ds.InetProxy.Length, '*' ) );
            Log.Debug( "      iNet Proxy User Name: " + string.Empty.PadRight( ds.InetProxyUserName.Length, '*' ) );
            Log.Debug( "       iNet Proxy Password: " + string.Empty.PadRight( ds.InetProxyPassword.Length, '*' ) );
#endif
			Log.Debug( "                 Log Level: " + ds.LogLevel.ToString() );
			Log.Debug( "              Log Capacity: " + ds.LogCapacity );
			Log.Debug( "        Log to Serial Port: " + ds.LogToSerialPort );
			Log.Debug( "               Log to File: " + ds.LogToFile );
			Log.Debug( "           Web App enabled: " + ds.WebAppEnabled );
            // TODO - should we show it or is it a security risk?
#if DEBUG
			Log.Debug( "          Web App Password: " + ds.WebAppPassword );
#else // don't show these values in Release mode.  It's considered sensitive data.
            Log.Debug( "          Web App Password: " + string.Empty.PadRight( ds.WebAppPassword.Length, '*' ) );
#endif
			Log.Debug( "               MAC Address: " + ds.NetworkSettings.MacAddress );
			Log.Debug( "              DHCP Enabled: " + ds.NetworkSettings.DhcpEnabled );
			Log.Debug( "         Static IP Address: " + ds.NetworkSettings.IpAddress );
			Log.Debug( "               Subnet Mask: " + ds.NetworkSettings.SubnetMask );
			Log.Debug( "                   Gateway: " + ds.NetworkSettings.Gateway );
			Log.Debug( "               Primary DNS: " + ds.NetworkSettings.DnsPrimary );
			Log.Debug( "             Secondary DNS: " + ds.NetworkSettings.DnsSecondary );
			Log.Debug( "       Port 1 Restrictions: " + ds.Port1Restrictions.ToString() );
			Log.Debug( "          Purge After Bump: " + ds.PurgeAfterBump );
			Log.Debug( "      ClearPeak on Docking: " + ds.ClearPeaksUponDocking.ToString() );
			Log.Debug( "        Single Sensor Mode: " + ds.SingleSensorMode.ToString() );
			Log.Debug( "       UseExpiredCylinders: " + ds.UseExpiredCylinders );
			Log.Debug( "        CombustibleBumpGas: " + ds.CombustibleBumpTestGas );
            Log.Debug( "      SpanReserveThreshold: " + ds.SpanReserveThreshold );
			Log.Debug( "     CalStationGasSchedule: " + ds.CalStationGasSchedule );
			Log.Debug( " CalStationDatalogSchedule: " + ds.CalStationDatalogScheduleEnabled );
            Log.Debug( "      Print \"Performed By\": " + ds.PrintPerformedBy );
            Log.Debug( "       Print \"Received By\": " + ds.PrintReceivedBy );
            Log.Debug( "                 Time Zone: " + ds.TimeZoneInfo );
            Log.Debug( "  Stop On Failed Bump Test: " + ds.StopOnFailedBumpTest);
            Log.Debug( "     Upgrade On Error Fail: " + ds.UpgradeOnErrorFail);
        }


        /// <summary>
        /// Save current configuration settings to persistent memory.
        /// This method does not touch the serialization settings file.
        /// </summary>
        public static void SaveConfiguration()
        {
            Log.Debug( "Saving docking station configuration..." );

            // clone it so we need not worry about it changing while doing the save.

            DockingStation ds = (DockingStation)DockingStation.Clone();
            Log.Debug( Log.Dashes );

            LogConfiguration( DockingStation );

            Log.Debug( Log.Dashes );

            SaveBootVars( ds );

            PropertiesFile propertiesFile = new PropertiesFile( FILE_NAME_CONFIG_INFO );

            propertiesFile[ PROPNAME_LANGUAGE_CODE ] = ds.Language.Code;
            propertiesFile[ PROPNAME_MENU_LOCKED ] = ds.MenuLocked.ToString();
            propertiesFile[ PROPNAME_USE_AUDIBLE_ALARM ] = ds.UseAudibleAlarm.ToString();

            propertiesFile[ PROPNAME_LOG_CAPACITY ] = ds.LogCapacity.ToString();
            propertiesFile[ PROPNAME_LOG_LEVEL ] = ds.LogLevel.ToString();
			propertiesFile[ PROPNAME_LOG_TO_SERIALPORT ] = ds.LogToSerialPort.ToString();
			propertiesFile[ PROPNAME_LOG_TO_FILE ] = ds.LogToFile.ToString();

            propertiesFile[ PROPNAME_WEB_APP_ENABLED ] = ds.WebAppEnabled.ToString();
            propertiesFile[ PROPNAME_WEB_APP_PASSWORD ] = ds.WebAppPassword;

            propertiesFile[ PROPNAME_INET_URL ] = ds.InetUrl;
            propertiesFile[ PROPNAME_INET_PING_INTERVAL ] = ds.InetPingInterval.ToString();
            propertiesFile[ PROPNAME_INET_USERNAME ] = ds.InetUserName;
            propertiesFile[ PROPNAME_INET_PASSWORD ] = ds.InetPassword;
            propertiesFile[ PROPNAME_INET_PING_INTERVAL ] = ds.InetPingInterval.ToString();
            propertiesFile[ PROPNAME_INET_TIMEOUT_LOW ] = ds.InetTimeoutLow.ToString();
            propertiesFile[ PROPNAME_INET_TIMEOUT_MEDIUM ] = ds.InetTimeoutMedium.ToString();
            propertiesFile[ PROPNAME_INET_TIMEOUT_HIGH ] = ds.InetTimeoutHigh.ToString();

            propertiesFile[ PROPNAME_INET_PROXY ] = ds.InetProxy;
            propertiesFile[ PROPNAME_INET_PROXY_USERNAME ] = ds.InetProxyUserName;
            propertiesFile[ PROPNAME_INET_PROXY_PASSWORD ] = ds.InetProxyPassword;

            propertiesFile[ PROPNAME_PORT1_RESTRICTIONS ] = ds.Port1Restrictions.ToString();
            propertiesFile[ PROPNAME_PURGE_AFTER_BUMP ] = ds.PurgeAfterBump.ToString();
            propertiesFile[ PROPNAME_CLEAR_PEAKS_UPON_DOCKING ] = ds.ClearPeaksUponDocking.ToString(); 
            propertiesFile[ PROPNAME_SINGLE_SENSOR_MODE ] = ds.SingleSensorMode.ToString();
            propertiesFile[ PROPNAME_USE_EXPIRED_CYLINDERS ] = ds.UseExpiredCylinders.ToString();
            propertiesFile[ PROPNAME_COMBUSTIBLE_BUMP_TEST_GAS ] = ds.CombustibleBumpTestGas;
            propertiesFile[ PROPNAME_SPAN_RESERVE_THRESHOLD ] = ds.SpanReserveThreshold.ToString();
            propertiesFile[ PROPNAME_CALSTATION_GAS_SCHEDULE ] = ds.CalStationGasSchedule.ToString();
			propertiesFile[ PROPNAME_CALSTATION_DATALOG_SCHEDULE ] = ds.CalStationDatalogScheduleEnabled.ToString();

            propertiesFile[ PROPNAME_PRINT_PERFORMED_BY ] = ds.PrintPerformedBy.ToString();
            propertiesFile[ PROPNAME_PRINT_RECEIVED_BY ] = ds.PrintReceivedBy.ToString();

            propertiesFile[ PROPNAME_STOP_ON_FAILED_BUMP_TEST ] = ds.StopOnFailedBumpTest.ToString();
            propertiesFile[ PROPNAME_UPGRADE_ON_ERROR_FAIL ] = ds.UpgradeOnErrorFail.ToString();

            TimeZoneInfo tzi = ds.TimeZoneInfo;
            propertiesFile[ PROPNAME_TZI_BIAS ] = tzi.Bias.ToString();
            propertiesFile[ PROPNAME_TZI_STANDARD_NAME ] = tzi.StandardName;
            propertiesFile[ PROPNAME_TZI_STANDARD_BIAS ] = tzi.StandardBias.ToString();
            propertiesFile[ PROPNAME_TZI_STANDARD_DATE_MONTH ] = tzi.StandardDate.Month.ToString();
            propertiesFile[ PROPNAME_TZI_STANDARD_DATE_DAY ] = tzi.StandardDate.Day.ToString();
            propertiesFile[ PROPNAME_TZI_STANDARD_DATE_HOUR ] = tzi.StandardDate.Hour.ToString();
            propertiesFile[ PROPNAME_TZI_STANDARD_DATE_DAYOFWEEK ] = tzi.StandardDate.DayOfWeek.ToString();
            propertiesFile[ PROPNAME_TZI_DAYLIGHT_NAME ] = tzi.DaylightName;
            propertiesFile[ PROPNAME_TZI_DAYLIGHT_BIAS ] = tzi.DaylightBias.ToString();
            propertiesFile[ PROPNAME_TZI_DAYLIGHT_DATE_MONTH ] = tzi.DaylightDate.Month.ToString();
            propertiesFile[ PROPNAME_TZI_DAYLIGHT_DATE_DAY ] = tzi.DaylightDate.Day.ToString();
            propertiesFile[ PROPNAME_TZI_DAYLIGHT_DATE_HOUR ] = tzi.DaylightDate.Hour.ToString();
            propertiesFile[ PROPNAME_TZI_DAYLIGHT_DATE_DAYOFWEEK ] = tzi.DaylightDate.DayOfWeek.ToString();           

            propertiesFile.Save();

            Log.Debug( string.Format( "{0} saved.", FILE_NAME_CONFIG_INFO )  );
        }

        /// <summary>
        /// Returns the current "local" time.  i.e., the current UTC time converted to the docking station's time zone.
        /// </summary>
        /// <remarks>
        /// This method is just a pass-thru to the DockingStation's TimeZoneInfo and is merely provided for convenience.
        /// </remarks>
        /// <returns>
        /// The current time, local to the DockingStation's time zone. Will also be set to DateTimeKind.Local.
        /// </returns>
        public static DateTime GetLocalTime()
        {
            return ToLocalTime( DateTime.UtcNow );
        }

        /// <summary>
        /// Converts the passed in timestamp (assumed to be in the DockingStation's time zone) to UTC.
        /// </summary>
        /// <remarks>
        /// This method is just a pass-thru to the DockingStation's TimeZoneInfo and is merely provided for convenience.
        /// </remarks>
        /// <param name="utcTime">Assumed to be in the DockingStation's time zone.</param>
        /// <returns>
        /// A time that is local to the DockingStation's time zone. Will also be set to DateTimeKind.Local.
        /// </returns>
        public static DateTime ToLocalTime( DateTime utcTime )
        {
            return DockingStation.TimeZoneInfo.ToLocalTime( utcTime );
        }

        /// <summary>
        /// Converts the passed in timestamp (assumed to be in the configuration's time zone) to UTC.
        /// </summary>
        /// <remarks>
        /// This method is just a pass-thru to the DockingStation's TimeZoneInfo and is merely provided for convenience.
        /// </remarks>
        /// <param name="localTime">
        /// Assumed to be in the configuration's time zone
        /// </param>
        /// <returns>
        /// A time in UTC.  Will also be set to DateTimeKind.Utc.
        /// </returns>
        public static DateTime ToUniversalTime( DateTime localTime )
        {
            return DockingStation.TimeZoneInfo.ToUniversalTime( localTime );
        }

        private static void SaveBootVars( DockingStation ds )
        {
            BootVars bootVars = BootVars.Load();

            // We never want to rewrite the BootVars unless something actually needs modified.

            bool modified = false;

            if ( bootVars.IpAddress != ds.NetworkSettings.IpAddress )
            {
                bootVars.IpAddress = ds.NetworkSettings.IpAddress;
                modified = true;
            }

            if ( bootVars.SubnetMask != ds.NetworkSettings.SubnetMask )
            {
                bootVars.SubnetMask = ds.NetworkSettings.SubnetMask;
                modified = true;
            }

            if ( bootVars.Gateway != ds.NetworkSettings.Gateway )
            {
                bootVars.Gateway = ds.NetworkSettings.Gateway;
                modified = true;
            }

            if ( bootVars.DhcpEnabled != ds.NetworkSettings.DhcpEnabled )
            {
                bootVars.DhcpEnabled = ds.NetworkSettings.DhcpEnabled;
                modified = true;
            }

            if ( bootVars.DnsPrimary != ds.NetworkSettings.DnsPrimary )
            {
                bootVars.DnsPrimary = ds.NetworkSettings.DnsPrimary;
                modified = true;
            }

            if ( bootVars.DnsSecondary != ds.NetworkSettings.DnsSecondary )
            {
                bootVars.DnsSecondary = ds.NetworkSettings.DnsSecondary;
                modified = true;
            }

            // We do not (currently) support WINS. So if the addresses are not empty 
            // for some reason, then we make them empty. Otherwise, WinCE could end 
            // up trying to use whatever the addresses have been set to.
            if ( bootVars.WinsAddress1 != string.Empty )
            {
                Log.Warning( string.Format( "Clearing WinsAddress1 which was found set to \"{0}\".", bootVars.WinsAddress1 ) ); 
                bootVars.WinsAddress1 = string.Empty;
                modified = true;
            }
            if ( bootVars.WinsAddress2 != string.Empty )
            {
                Log.Warning( string.Format( "Clearing WinsAddress2 which was found set to \"{0}\".", bootVars.WinsAddress2 ) ); 
                bootVars.WinsAddress2 = string.Empty;
                modified = true;
            }

            if ( !modified )
            {
                Log.Debug( "No BootVars modified." );
                return;
            }

            Log.Debug( "Saving BootVars" );

            BootVars.Save( bootVars );

            Thread.Sleep( 1000 ); // don't trust the save call to return before the flash is fully saved

            Log.Debug( "BootVars saved." );
        }

        /// <summary>
        /// Save current FlowOffset to serialization file.  
        /// </summary>
        public static void SaveFlowOffset()
        {
            Log.Debug( "Saving FlowOffset " + DockingStation.FlowOffset + " to serialization file" );

            // re-serialize, which should only change the flow temp base.
            Serialize( DockingStation );
        }

        public static void Serialize( DockingStation serializationInfo )
        {
            Serialize( serializationInfo, DockingStation.FlowOffset );
        }

        /// <summary>
        /// Save current serialization settings to serialization file.
        /// </summary>
        /// <param name="newSettings"></param>
        private static void Serialize( DockingStation serializationInfo, int flowOffset )
        {
            DockingStation config = (DockingStation)DockingStation.Clone();

            // Serialization information...
            config.SerialNumber = serializationInfo.SerialNumber;
            config.Reservoir = serializationInfo.Reservoir;
            config.Type = serializationInfo.Type;
            config.NumGasPorts = serializationInfo.NumGasPorts;
            config.PartNumber = serializationInfo.PartNumber;
            config.SetupDate = serializationInfo.SetupDate;
            config.SetupTech = serializationInfo.SetupTech;

            Log.Debug( Log.Dashes );
            Log.Debug( "Saving serialization info..." );
            Log.Debug( "          Part Number: " + config.PartNumber );
            Log.Debug( "        Serial Number: " + config.SerialNumber );
            Log.Debug( "                 Type: " + config.Type );
            Log.Debug( "           Setup Date: " + config.SetupDate );
            Log.Debug( "           Setup Tech: " + config.SetupTech );
            Log.Debug( "            Gas Ports: " + config.NumGasPorts );
            Log.Debug( "            Reservoir: " + config.Reservoir );
            Log.Debug( "          Flow Offset: " + ( ( flowOffset == int.MinValue ) ? "None" : flowOffset.ToString() ) );
            Log.Debug( Log.Dashes );

            Dictionary<string,string> properties = new Dictionary<string,string>();

            properties[ PROPNAME_PART_NUMBER ] = config.PartNumber;
            properties[ PROPNAME_SERIAL_NUMBER ] = config.SerialNumber;
            properties[ PROPNAME_TYPE ] = config.Type.ToString();
            // If equal to MinValue then flow offset is unknown/undefined so don't save it.
            if ( flowOffset != int.MinValue )
                properties[ PROPNAME_FLOW_OFFSET ] = flowOffset.ToString();
            // If equal to MinValue then flow offset is unknown/undefined so don't save it.
            properties[ PROPNAME_SETUP_DATE ] = config.SetupDate.Date.Year.ToString() + "-" + config.SetupDate.Month.ToString() + "-" + serializationInfo.SetupDate.Date.Day.ToString();
            properties[ PROPNAME_SETUP_TECH ] = config.SetupTech;
            properties[ PROPNAME_NUM_GAS_PORTS ] = config.NumGasPorts.ToString();
            properties[ PROPNAME_RESERVOIR ] = config.Reservoir.ToString();

            try
            {
                PropertiesFile propertiesFile = new PropertiesFile( FILE_NAME_FACTORY_INFO, properties );

                // Save and verify the properties file.
                propertiesFile.Save();

                // Save and verify a backup copy, too.
                PropertiesFile backupFile = new PropertiesFile( FILE_NAME_FACTORY_BACK, properties );
                backupFile.Save();
            }
            catch ( Exception e )
            {
                Log.Error( "Error saving serialization info.", e );
                HasConfigurationError = true;
            }
            // Now that we saved the local copy, assign it to the global instance.
            DockingStation = config;
            DockingStation.FlowOffset = flowOffset;
        }

        private static void LoadSerialization( int attempt )
        {
            try
            {
                PropertiesFile propertiesFile = new PropertiesFile( FILE_NAME_FACTORY_INFO );

                try
                {
                    propertiesFile.Load();
                }
                catch ( Exception e )
                {
                    // It's expected that the file won't be found. So don't log the whole stack trace.
                    if ( e is FileNotFoundException )
                        Log.Warning( string.Format( "UNABLE TO LOAD SERIALIZATION FILE \"{0}\" - {1}", FILE_NAME_FACTORY_INFO, e.Message ) );
                    else
                        Log.Warning( string.Format( "UNABLE TO LOAD SERIALIZATION FILE \"{0}\".", FILE_NAME_FACTORY_INFO ), e );

                    if ( attempt > 1 )
                    {
                        HasConfigurationError = true;
                        return;
                    }

                    string s = e.Message;

                    Log.Warning( string.Format( "ATTEMPTING TO RESTORE SERIALIZATION FROM BACKUP COPY (\"{0}\").", FILE_NAME_FACTORY_BACK ) );

                    if ( !File.Exists( FILE_NAME_FACTORY_BACK ) )
                    {
                        HasConfigurationError = true;
                        return;
                    }
                    else
                    {
                        Log.Info( string.Format( "RESTORING SERIALIZATION FILE \"{0}\" FROM BACKUP COPY \"{1}\".", FILE_NAME_FACTORY_INFO, FILE_NAME_FACTORY_BACK ) );

                        File.Copy( FILE_NAME_FACTORY_BACK, FILE_NAME_FACTORY_INFO, true );

                        Thread.Sleep( 1000 ); // Don't trust the file system to flush before returning.

                        Log.Info( "SERIALIZATION RESTORED FROM BACKUP" );
                        Log.Info( "ATTEMPTING TO RELOAD SERIALIZATION" );

                        LoadSerialization( ++attempt );
                        return;
                    }

                } // end-propertiesFile.Load

                // If we make it to here,then we successfully loaded the serialization file.
                // Always make sure we still have a backup, too.
                if ( !File.Exists( FILE_NAME_FACTORY_BACK ) )
                {
                    Log.Info( string.Format( "BACKING UP SERIALIZATION FILE \"{0}\" TO \"{1}\".", FILE_NAME_FACTORY_INFO, FILE_NAME_FACTORY_BACK ) );

                    File.Copy( FILE_NAME_FACTORY_INFO, FILE_NAME_FACTORY_BACK, true );
                    Thread.Sleep( 1000 ); // Don't trust the file system to flush before returning.

                    Log.Info( "SERIALIZATION BACKED UP" );
                }

                DockingStation.PartNumber = propertiesFile[ PROPNAME_PART_NUMBER ];

                DockingStation.SerialNumber = propertiesFile[ PROPNAME_SERIAL_NUMBER ];

                string propertyValue = propertiesFile[ PROPNAME_TYPE ];
                try
                {
                    DockingStation.Type = (DeviceType)Enum.Parse( typeof( DeviceType ), propertyValue, false );
                }
                catch ( Exception e )
                {
                    Log.Error( string.Format( "Error parsing docking station {0} \"{1}\"", PROPNAME_TYPE, propertyValue ), e );
                    DockingStation.Type = DeviceType.Unknown;
                    Log.Error( "Defaulting to " + DockingStation.Type.ToString() );
                }

                propertyValue = propertiesFile[PROPNAME_FLOW_OFFSET];
                if ( !string.IsNullOrEmpty( propertyValue ) )
                {
                    try
                    {
                        DockingStation.FlowOffset = int.Parse( propertyValue );
                    }
                    catch ( Exception e )
                    {
                        Log.Error( string.Format( "Error parsing {0} string \"{1}\"", PROPNAME_FLOW_OFFSET, propertyValue ), e );
                    }
                }
                else
                    DockingStation.FlowOffset = int.MinValue;

                propertyValue = propertiesFile[PROPNAME_SETUP_DATE];
                try
                {
                    DockingStation.SetupDate = Convert.ToDateTime( propertyValue );
                }
                catch ( Exception e )
                {
                    Log.Error( string.Format( "Error parsing {0} string \"{1}\"", PROPNAME_SETUP_DATE, propertyValue ), e );
                    DockingStation.SetupDate = DateTime.MinValue;
                    Log.Error( "Defaulting to " + DockingStation.SetupDate );
                }

                DockingStation.SetupTech = propertiesFile[ PROPNAME_SETUP_TECH ];

                // Reservoir property won't exist in older docking stations.  Just quietly use default if it doesn't exist.
                // If it does exist, though, we expect to be able to parse it.
                propertyValue = propertiesFile[PROPNAME_RESERVOIR];
                if ( !string.IsNullOrEmpty( propertyValue ) )
                {
                    // If we can't parse this string, then we just let the Parse failure throw.
                    // We do not just assume any default, since we don't really know whether it should be true or false.
                    DockingStation.Reservoir = bool.Parse( propertyValue );
                }
                else
                {
                    // If no reservoir property at all, then we assume this is an original iNetDS
                    // that was serialized before that property existed.  We, therefore default to true
                    // since all iNetDS units have the reservoir.
                    Log.Warning( string.Format( "No \"{0}\" property found in serialization info.", PROPNAME_RESERVOIR ) );
                    Log.Warning( string.Format( "Assuming old iNetDS, and using a default of {0}", DockingStation.Reservoir ) );
                    
                }

                // NumGasPorts property won't exist in older docking stations. Just quietly use default if it doesn't exist.
                // If it does exist, though, we expect to be able to parse it.
                propertyValue = propertiesFile[PROPNAME_NUM_GAS_PORTS];
                if ( !string.IsNullOrEmpty( propertyValue ) )
                {
                    try
                    {
                        DockingStation.NumGasPorts = int.Parse( propertyValue );
                    }
                    catch ( Exception e )
                    {
                        Log.Error( string.Format( "Error parsing {0} string \"{1}\". Using default of {2}.", PROPNAME_NUM_GAS_PORTS, propertyValue, DockingStation.NumGasPorts ), e );
                        DockingStation.SetupDate = DateTime.MinValue;
                    }
                }
                else
                {
                    // If no number of oorts property at all, then we assume this is an original iNetDS
                    // that was serialized before that property existed.  We, therefore default to 
                    // DockingStation class's default of 3, since since all iNetDS units just 3 ports.
                    Log.Warning( string.Format( "No \"{0}\" property found in serialization info.", PROPNAME_NUM_GAS_PORTS ) );
                    Log.Warning( string.Format( "Assuming old iNetDS, and using a default of {0}.", DockingStation.NumGasPorts ) );
                }
            }
            catch ( Exception e )
            {
                Log.Error( "LoadSerialization", e );

                HasConfigurationError = true;
            }
        }



        /// <summary>
        /// Deserializes the docking station.  The properties files
        /// containing the serialization and configuration info are
        /// deleted and then the docking station is rebooted.
        /// </summary>
        public static void Deserialize()
        {
            try
            {
                Log.Debug( "Removing all serialization and configuration information" );

                PropertiesFile.Delete( FILE_NAME_CONFIG_INFO );

                PropertiesFile.Delete( FILE_NAME_FACTORY_INFO );

                PropertiesFile.Delete( FILE_NAME_FACTORY_BACK );
            }
            catch ( Exception e )
            {
                Log.Error( "Deserialize", e );
            }
        }

        public static string GetWebAppPassword()
        {
            return "DSX";
            //string configFile = Controller.ApplicationPath + ".config";

            //ConfigXmlDocument configDoc = new ConfigXmlDocument();
            //configDoc.Load( configFile );

            //XmlNode userNode = configDoc.SelectSingleNode( WEBAPP_XPATH_USER );

            //XmlAttribute passwordAttribute = userNode.Attributes["Password"];

            //return passwordAttribute.InnerText;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="applicationPath"></param>
        /// <param name="password"></param>
        /// <returns>true if the routine updated the config file because the password in the file was different than the passed-in
        /// password. 
        /// false is returned if the routine detected that no change was necessary.</returns>
        public static bool ConfigureWebApp( string password, bool useSsl )
        {
#if DEBUG // never log the password in Release builds
            Log.Info( string.Format( "Setting WebAppPassword to \"{0}\"", Configuration.DockingStation.WebAppPassword ) );
#else
            Log.Info( "Setting WebAppPassword" );
#endif

            string configFile = string.Empty;

            //try
            //{
            //    configFile = Controller.ApplicationPath + ".config";

            //    ConfigXmlDocument configDoc = new ConfigXmlDocument();
            //    configDoc.Load( configFile );


            //    bool modified = false;

            //    XmlNodeList userNodes = configDoc.SelectNodes( WEBAPP_XPATH_USER );

            //    foreach ( XmlNode userNode in userNodes )
            //    {
            //        XmlAttribute passwordAttribute = userNode.Attributes["Password"];

            //        if ( passwordAttribute.InnerText != password )
            //        {
            //            passwordAttribute.InnerText = password;
            //            modified = true;
            //        }
            //    }

            //    XmlNode webAppNode = configDoc.SelectSingleNode( WEBAPP_XPATH );

            //    XmlAttribute useSslAttribute = webAppNode.Attributes["UseSsl"];

            //    if ( bool.Parse( useSslAttribute.InnerText ) != useSsl )
            //    {
            //        useSslAttribute.InnerText = useSsl.ToString().ToLower();

            //        webAppNode.Attributes["DefaultPort"].InnerText = useSsl ? "443" : "80";

            //        modified = true;
            //    }


            //    if ( modified )
            //    {
            //        configDoc.Save( configFile );

            //        return true;
            //    }
            //}
            //catch ( Exception ex )
            //{
            //    Log.Error( string.Format( "Unable to update WebApp password in file \"{0}\"", configFile ), ex );
            //}

            return false;
        }
    }

    public class ConfigurationException : ApplicationException
    {
        public ConfigurationException( string msg ) : base( msg ) { }

        public ConfigurationException( string msg, Exception e ) : base( msg, e ) { }
    }

}
