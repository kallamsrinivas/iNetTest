using System;
using System.Collections.Generic;
using System.IO;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.DomainModel
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a docking station.
	/// </summary>
    public class DockingStation : Device
    {
        /// <summary>
        /// Part number for DSX Ventis-LS docking stations.
        /// </summary>
        public const string VENTISLS_PARTNUMBER = "1810-9328"; 

        #region Fields

        private List<GasEndPoint> _gasEndPoints;
		private List<GasEndPoint> _changedGasEndPoints;
        
        private string _webAppPassword;
 
        private string _inetUrl;
        private string _inetUserName;
        private string _inetPassword;
        private string _inetProxy;
        private string _inetProxyUserName; 
        private string _inetProxyPassword;

        private PortRestrictions _port1Restrictions;
        private TimeZoneInfo _tzi;

        /// <summary>
        /// Null will be returned if there is no installed flash card.
        /// </summary>
        public DriveInfo FlashCardInfo { get; set; }

        private NetworkInfo _networkSettings = new NetworkInfo();
        private NetworkInfo _replacedDSNetworkSettings;

        private string _combustibleBumpTestGas;
        private string _peripheralBoardRevision;
        private string _lcdType;

        public const int MAXIMUM_NUM_GAS_PORTS = 6;

        public const int LEGACY_NUM_GAS_PORTS = 3;

        #endregion

        public class NetworkInfo : ICloneable
        {
            private bool _dhcpEnabled;
            private string _macAddress;
            private string _subnetMask;
            private string _gateway;
            private string _ipAddress;
            private string _dnsPrimary;
            private string _dnsSecondary;
            
            public object Clone()
            {
                return (NetworkInfo)this.MemberwiseClone();
            }

            /// <summary>
            /// The MAC address of the integrated NIC.
            /// </summary>
            public string MacAddress
            {
                get
                {
                    if ( _macAddress == null ) _macAddress = string.Empty;
                    return _macAddress;
                }
                set
                {
                    _macAddress = value;
                }
            }

            /// <summary>
            /// Whether or not DHCP should be used.
            /// </summary>
            public bool DhcpEnabled
            {
                get { return _dhcpEnabled; }
                set { _dhcpEnabled = value; }
            }

            /// <summary>
            /// Gets or sets this device's IP Address.
            /// </summary>
            public string IpAddress
            {
                get
                {
                    return _ipAddress == null ? string.Empty : _ipAddress;
                }
                set
                {
                    _ipAddress = value;
                }
            }

            public string SubnetMask
            {
                get
                {
                    if ( _subnetMask == null ) _subnetMask = string.Empty;
                    return _subnetMask;
                }
                set
                {
                    _subnetMask = value;
                }
            }

            /// <summary>
            /// The IP address of the gateway,
            /// </summary>
            public string Gateway
            {
                get
                {
                    if ( _gateway == null ) _gateway = string.Empty;
                    return _gateway;
                }
                set
                {
                    _gateway = value;
                }
            }

            /// <summary>
            /// The IP address of the primary DNS
            /// </summary>
            public string DnsPrimary
            {
                get
                {
                    if ( _dnsPrimary == null ) _dnsPrimary = string.Empty;
                    return _dnsPrimary;
                }
                set
                {
                    _dnsPrimary = value;
                }
            }

            /// <summary>
            /// The IP address of the secondary DNS.
            /// </summary>
            public string DnsSecondary
            {
                get
                {
                    if ( _dnsSecondary == null ) _dnsSecondary = string.Empty;
                    return _dnsSecondary;
                }
                set
                {
                    _dnsSecondary = value;
                }
            }

            /// <summary>
            /// True if DNS addresses were provided by DHCP server.  False if they were provided
            /// by the user (stored in "boot vars").  Null if unknown, which is typically the case
            /// when the NetworkInfo was obtained from the OS, since the OS doesn't tell us where
            /// the DNS addresses that the NIC is is using came from.
            /// </summary>
            public bool? DnsDhcp { get; set; }

        } // end-NetworkInfo

        #region Constructors

        /// <summary>
        /// Creates a new instance of DockingStation class.
        /// All property settings will be initialized to Factory defaults.
        /// </summary>
        public DockingStation()
        {
            LogLevel = Log.DEFAULT_LEVEL;
            FlowOffset = DomainModelConstant.NullInt; // init to undefined
            LastRebootTime = DateTime.MinValue;
            PeripheralBoardRevision = LcdType = string.Empty;
            InetDatabaseTotalSize = InetDatabaseUnusedSize= InetQueueDatabaseTotalSize = InetQueueDatabaseUnusedSize = DomainModelConstant.NullLong;
            Reservoir = true; // legacy iNetDS units have a reservoir.  We default to legacy.
            NumGasPorts = LEGACY_NUM_GAS_PORTS;
        }


        #endregion

        #region Properties

        /// <summary>
        /// Indicates how many gas ports the docking station has.
        /// </summary>
        public int NumGasPorts { get; set; }

        /// <summary>
        /// Indicates whether or not the docking station has an internal reservoir.
        /// The reservoir's presence affects how long it takes to purge.
        /// </summary>
        public bool Reservoir { get; set; }

        /// <summary>
        /// The docking station's local time zone.
        /// </summary>
        public TimeZoneInfo TimeZoneInfo
        {
            get
            {
                if ( _tzi == null )
                    _tzi = TimeZoneInfo.GetUTC();
                return _tzi;
            }
            set
            {
                _tzi = value;
            }
        }

        public NetworkInfo NetworkSettings
        {
            get
            {
                return _networkSettings;
            }
            set { _networkSettings = value; }
        }

        /// <summary>
        /// Whether or not the docking station should purge at the end of a bump test.
        /// </summary>
        public bool PurgeAfterBump { get; set; }

        /// <summary>
        /// Whether or not a USB printer is attached.
        /// </summary>
        public bool PrinterAttached { get; set; }

        /// <summary>
        /// If true, then a "Performed By" signature line will be printed on Bump and Calibration certificates.
        /// </summary>
        public bool PrintPerformedBy { get; set; }

        /// <summary>
        /// If true, then a "Received By" signature line will be printed on Bump and Calibration certificates.
        /// </summary>
        public bool PrintReceivedBy { get; set; }

        /// <summary>
        /// If true, then dual-sensored instrument is allowed to operate when only one sensor has passed calibration.
        /// <para>
        /// If false, then dual-sensored instrument is NOT allowed to operate if only one sensor has passed calibration.
        /// </para>
        /// <para>
        /// NOTE: THIS PROPERTY SHOULD ONLY BE ACCESSED BY LOGIC RELATED TO LOADING OR SAVING THE SETTING
        /// TO/FROM THE DATABASE AND CONFIG.PROPERTIES FILE. ANY LOGIC THAT WANTS TO ACTUALLY KNOW IF 'SINGLE SENSOR MODE'
        /// IS CURRENTLY ENABLED SHOULD, INSTEAD, CALL Configuration.IsSingleSensorMode().
        /// </para>
        /// </summary>
        public bool SingleSensorMode { get; set; }

        /// <summary>
        /// If true, then the NAND flash memory's 4-bit hardware ECC capability has been enabled.
        /// <para>
        /// If false, then only 1-bit software ECC is being used.
        /// </para>
        /// </summary>
        public bool NandFlashEcc { get; set; }

        /// <summary>
        /// The type of instrument gas schedule this DS should enforce when it's in cal station mode.
        /// </summary>
        public CalStationGasSchedule CalStationGasSchedule { get; set; }

		/// <summary>
		/// If true, then a download datalog schedule may be created for the docked instrument when in cal station mode.
		/// </summary>
		public bool CalStationDatalogScheduleEnabled { get; set; }

        public PortRestrictions Port1Restrictions
        {
            // The intent is the array is instantated during object construction
            // to have 3 elements, and its length never changes.  Making this
            // property a 'getter' should be able to guarantee that.
            get { return _port1Restrictions; }
            set { _port1Restrictions = value; }
        }

        public bool WebAppEnabled { get; set; }

        public string WebAppPassword
        {
            get
            {
                if ( _webAppPassword == null ) _webAppPassword = string.Empty;
                return _webAppPassword;
            }
            set
            {
                _webAppPassword = value;
            }
        }

        public int FlowOffset { get; set; }

        //INS-7008
        /// <summary>
        /// In case of MX4 docking station, returns whether dock has new Ventis cradle or not.  
        /// For all other docks, this property returns always false.
        /// </summary>
        public bool HasNewVentisCradle { get; set; }

        public string InetUrl
        {
            get
            {
                if ( _inetUrl == null )
                    _inetUrl = string.Empty;

                return _inetUrl;
            }
            set
            {
                _inetUrl = value;
            }
        }

        public short InetPingInterval { get; set; }

        /// <summary>
        /// Timemout (in seconds) to use when calling web services that should have an expected "short" response time.
        /// </summary>
        public int InetTimeoutLow { get; set; }

        /// <summary>
        /// Timemout (in seconds) to use when calling web services that should have an expected "medium" response time.
        /// </summary>
        public int InetTimeoutMedium { get; set; }

        /// <summary>
        /// Timemout (in seconds) to use when calling web services that should have an expected "high" response time.
        /// </summary>
        public int InetTimeoutHigh { get; set; }

        public string InetUserName
        {
            get
            {
                if ( _inetUserName == null )
                    _inetUserName = string.Empty;

                return _inetUserName;
            }

            set
            {
                _inetUserName = value;
            }
        }

        public string InetPassword
        {
            get
            {
                if ( _inetPassword == null )
                    _inetPassword = string.Empty;

                return _inetPassword;
            }

            set
            {
                _inetPassword = value;
            }
        }


        /// <summary>
        /// Address for proxy server to upload through.
        /// </summary>
        public string InetProxy
        {
            get
            {
                if ( _inetProxy == null )
                    _inetProxy = string.Empty;

                return _inetProxy;
            }
            set
            {
                _inetProxy = value;
            }
        }

        /// <summary>
        /// User name to use to authenticate against proxy server.
        /// </summary>
        public string InetProxyUserName
        {
            get
            {
                if ( _inetProxyUserName == null )
                    _inetProxyUserName = string.Empty;

                return _inetProxyUserName;
            }

            set
            {
                _inetProxyUserName = value;
            }
        }

        /// <summary>
        /// Password to use to authenticate against proxy server.
        /// </summary>
        public string InetProxyPassword
        {
            get
            {
                if ( _inetProxyPassword == null )
                    _inetProxyPassword = string.Empty;

                return _inetProxyPassword;
            }

            set
            {
                _inetProxyPassword = value;
            }
        }

      
        /// <summary>
        /// Maximum number of debug log messages
        /// </summary>
        public int LogCapacity { get; set; }

        public LogLevel LogLevel { get; set; }

		/// <summary>
		/// True - Enable logging to serial port.
		/// <para>False - Disable logging to serial port.</para>
		/// </summary>
		public bool LogToSerialPort { get; set; }

		/// <summary>
		/// True - Logging to a file enabled so power resets do not clear the log.
		/// <para>False - Logging to a file disabled.</para>
		/// </summary>
		public bool LogToFile { get; set; }

        /// <summary>
        /// Returns timestamp indicating when this IDS was last rebooted.
        /// </summary>
        public DateTime LastRebootTime { get; set; }

        /// <summary>
        /// Gets or sets the list of GasEndPoints representing the cylinders currently attached to the docking station.
        /// </summary>
        public List<GasEndPoint> GasEndPoints
        {
            get
            {
                if ( _gasEndPoints == null )
                    _gasEndPoints = new List<GasEndPoint>();

                AssertGasEndPoints();

                return _gasEndPoints;
            }
            set
            {
                _gasEndPoints = value;

                AssertGasEndPoints();
            }
        }

		/// <summary>
        /// Will be populated with changes to the Smartcard data.
		/// It will only be supplied when a smartcard is inserted or removed.
		/// </summary>
        public List<GasEndPoint> ChangedGasEndPoints
		{
			get
			{
				if ( _changedGasEndPoints == null )
                    _changedGasEndPoints = new List<GasEndPoint>();

                AssertGasEndPoints();

				return _changedGasEndPoints;
			}
            set
            {
                _changedGasEndPoints = value;

                AssertGasEndPoints();
            }
		}

        private void AssertGasEndPoints()
        {
            Log.Assert( _gasEndPoints == null || _gasEndPoints.Count == 0 || _changedGasEndPoints == null || _changedGasEndPoints.Count == 0,
				"Error: GasEndPoints and ChangedGasEndPoints cannot both contain data." );
        }

		/// <summary>
		/// Gets or sets a value indicating whether the docking station menu is locked.
		/// </summary>
		public bool MenuLocked { get; set; }

        public bool UseAudibleAlarm { get; set; }

        /// <summary>
        /// The current size (in bytes) of the iNet database.
        /// </summary>
        public long InetDatabaseTotalSize { get; set; }


        /// <summary>
        /// The number of unused bytes in iNet database.
        /// </summary>
        public long InetDatabaseUnusedSize { get; set; }

        /// <summary>
        /// The current size (in bytes) of the iNet Queue database.
        /// </summary>
        public long InetQueueDatabaseTotalSize { get; set; }

        /// <summary>
        /// The number of unused bytes in iNet upload Queue database.
        /// </summary>
        public long InetQueueDatabaseUnusedSize { get; set; }

        //Suresh 12-SEPTEMBER-2011 INS-2248
        /// <summary>
        /// Replaced docking station network settings
        /// </summary>
        public NetworkInfo ReplacedDSNetworkSettings
        {
            get { return _replacedDSNetworkSettings; }
            set { _replacedDSNetworkSettings = value; }
        }

        /// <summary>
        /// Clear peaks upon docking the instrument
        /// </summary>
        public bool ClearPeaksUponDocking { get; set; }

        /// <summary>
        /// Indicates whether or not expired cylinders should be used for bump tests.
        /// </summary>
        public bool UseExpiredCylinders { get; set; }

        /// <summary>
        /// Indicates if a different gas should be used for bump testing LEL sensors
        /// that differes from the sensor's calibration gas setting.  May be empty.
        /// </summary>
        public string CombustibleBumpTestGas
        {
            get
            {
                if ( _combustibleBumpTestGas == null ) _combustibleBumpTestGas = string.Empty;
                return _combustibleBumpTestGas;
            }
            set
            {
                _combustibleBumpTestGas = value;
            }
        }

        /// <summary>
        /// Gets the Span Reserve threshold based on which, determine whether the sensor calibration is failed or not (Applicable to Service Accounts).
        /// </summary>
        public double SpanReserveThreshold { get; set; }

        /// <summary>
        ///  The type of peripheral board.
        ///  "Okaya" if older board that only supports the Okay LCD.
        ///  "Phoenix" if newer board that supports the Phoenix LCD.
        /// </summary>
        public string PeripheralBoardRevision
        {
            get
            {
                if ( _peripheralBoardRevision == null ) _peripheralBoardRevision = string.Empty;
                return _peripheralBoardRevision;
            }
            set
            {
                _peripheralBoardRevision = value;
            }
        }

        /// <summary>
        /// The type of installed LCD. "Okaya" or "Phoenix".
        /// </summary>
        public string LcdType
        {
            get
            {
                if ( _lcdType == null ) _lcdType = string.Empty;
                return _lcdType;
            }
            set
            {
                _lcdType = value;
            }
        }

        //Ajay - 25-Sep-2017 INS-8232
        /// <summary>
        /// Indicates whether instrument can contiue with calibration when bump test is failed.
        /// ***Only for service accounts***
        /// </summary>
        public bool StopOnFailedBumpTest { get; set; }

        /// <summary>
        /// Service indicated that they mostly receive instruments with error or fail state which can just be resolved by upgrading instrument firmware sometimes.  
        /// But as per current priority of events in DSX-i, dock will run instrument diagnostics first, and if any error condition is noticed, 
        /// dock goes red and does not proceed to upgrade instrument firmware. 
        /// This is causing service to upgrade instrument firmware using different software, and redock on DSX-i.
        /// "UpgradeOnErrorFail" is a configurable option in Admin Console that helps in rearranging priority of events for SERVICE ACCOUNTS.
        /// INS-8228 RHP v7.6
        /// </summary>
        public bool UpgradeOnErrorFail { get; set; }

        #endregion Properties

		#region Methods

        /// <summary>
        /// True if serial number is non-empty, and DeviceType is not Unknown.
        /// </summary>
        /// <returns></returns>
        public bool IsSerialized()
        {
            return SerialNumber.Length > 0 && Type != DeviceType.Unknown;
        }

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return SerialNumber;
		}

        /// <summary>
        /// Does a "deep" copy of this subclass's member variables.
        /// Helper method for Clone.  
        /// </summary>
        /// <param name="device">
        /// This parameter actually needs to be a DockingStation.
        /// It's defined as a Device so that we can override the base class.
        /// </param>
        protected override void DeepCopyTo( Device device )
        {
            Log.Assert( device is DockingStation, "referenced passed to DockingStation.CopyTo must be of type DockingStation" );

            // first, deep clone the base class
            base.DeepCopyTo( device );

            // now, deep-clone this subclass...

            DockingStation ds = (DockingStation)device;

            ds.TimeZoneInfo = (TimeZoneInfo)this.TimeZoneInfo.Clone();

            ds.GasEndPoints = new List<GasEndPoint>();
            foreach ( GasEndPoint gasEndPoint in this.GasEndPoints )
                ds.GasEndPoints.Add( (GasEndPoint)gasEndPoint.Clone() );

            ds.ChangedGasEndPoints = new List<GasEndPoint>();
            foreach ( GasEndPoint gasEndPoint in this.ChangedGasEndPoints )
                ds.ChangedGasEndPoints.Add( (GasEndPoint)gasEndPoint.Clone() );
        }

        /// <summary>
		/// Implementation of ICloneable::Clone.  Overrides Device.Clone().
		/// </summary>
		/// <returns>Cloned DockingStation</returns>
		public override object Clone()
		{
            DockingStation dockingStation = (DockingStation)this.MemberwiseClone();

            // now deep-clone what needs to be deep cloned...

            this.NetworkSettings = (NetworkInfo)dockingStation.NetworkSettings.Clone();

            this.DeepCopyTo( dockingStation );

			return dockingStation;
		}

        public bool IsDockedInstrumentSupported(Instrument dockedInstrument)
        {
            // Make sure instrument matches the type of IDS it's docked in.
            // We need to watch out for things like trying to dock a GasBadge Plus on a GBPRO IDS.)
            // A Ventis Pro Series instrument on an MX4 docking station is allowed. 
            if (dockedInstrument.Type != this.Type && !(dockedInstrument.Type == DeviceType.VPRO && this.Type == DeviceType.MX4))
            {
                return false;
            }

            //We do not allow Ventis instrument on Ventis-LS Docking station INS-6434
            if (this.Type == DeviceType.MX4 && this.PartNumber == VENTISLS_PARTNUMBER)
            {
                if (dockedInstrument.Subtype != DeviceSubType.Mx4VentisLs)
                    return false;
            }

            //INS-7008: We do not allow iQuad instrument to be docked on Ventis docking station with new cradle
            if (this.Type == DeviceType.MX4 && this.HasNewVentisCradle)
            {
                if (dockedInstrument.Subtype == DeviceSubType.Mx4iQuad)
                    return false;
            }

            return true;
        }

		#endregion

	}

    [Flags] public enum PortRestrictions
    {
        //None = 0x00, // No restriction (allow any type of cylinder)
        FreshAir = 0x01, // Allow Fresh air
        ZeroAir = 0x02, // Allow Zero air
    }

    /// <summary>
    /// Possible gas schedule types when in Cal Station mode.
    /// </summary>
    public enum CalStationGasSchedule
    {
        None,
        BumpUponDocking,
        CalUponDocking
    }

}
