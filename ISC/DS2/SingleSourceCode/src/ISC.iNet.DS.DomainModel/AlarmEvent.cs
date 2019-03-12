using System;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Summary description for AlarmEvent.
    /// </summary>
    public class AlarmEvent : ICloneable
	{
        #region Fields

        private string _instrumentSerialNumber;

        private string _sensorSerialNumber;

		private string _sensorCode;
        private string _gasCode;

		private bool _isDualSense;

        private DateTime _time;

        private int  _duration;
        private int _ticks;

        private double  _peakReading;
        private double  _alarmHigh;
        private double  _alarmLow;

        private string _user;
        private string _site;

        //INS-8330 (INS-8624) Upload datalog proximity alarms in eventlog to iNet
        private int _userAccessLevel;
        private int _siteAccessLevel;

		private string _baseUnitSerialNumber;

        private int             _speakerVoltage = DomainModelConstant.NullInt;  
        private int             _vibratingMotorVoltage = DomainModelConstant.NullInt;
        private AlarmOperatingMode _alarmOperatingMode;
        private bool?               _isDocked = null;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        public AlarmEvent()
        {
            _instrumentSerialNumber = _baseUnitSerialNumber = _sensorSerialNumber = _sensorCode = _gasCode = _user = _site = string.Empty;

            _time = DomainModelConstant.NullDateTime;
            _duration = _ticks = DomainModelConstant.NullInt;

            _peakReading = _alarmHigh = _alarmLow = DomainModelConstant.NullDouble;

            _speakerVoltage = _vibratingMotorVoltage = 0;
            _alarmOperatingMode = AlarmOperatingMode.Running;
            _isDocked = false;
			_isDualSense = false;
        }

        /// <summary>
        /// Returns a duplicate of this AlarmEvent object.
        /// </summary>
        /// <returns>Cylinder object</returns>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }

        #endregion  // Constructors

        #region  Properties

        /// <summary>
        /// Serial number of the instrument this event occurred on.
        /// </summary>
        public string InstrumentSerialNumber
        {
            get
            {
                return _instrumentSerialNumber == null ? string.Empty : _instrumentSerialNumber;
            }
            set
            {
                _instrumentSerialNumber = value;
            }
        }

        /// <summary>
        /// Serial number of the sensor this event occurred on.
        /// </summary>
        public string SensorSerialNumber
        {
            get
            {
                return _sensorSerialNumber == null ? string.Empty : _sensorSerialNumber;
            }
            set
            {
                _sensorSerialNumber = value;
            }
        }

		/// <summary>
		/// iNet needs to match alarm events to a sensor's UID.  Not all instruments
		/// provide the sensor code in the alarm event log.  We try to map gas codes
		/// to sensor codes, but not all gas codes are supported.
		/// 
		/// Ventis, Tango and Ventis Pro instruments provide the sensor codes in the
		/// log.  All GB Plus and Pro gas codes can be mapped to sensor codes.  Only
		/// a subset of MX6 sensors can be mapped.  (LEL and PID gas codes will not 
		/// be mapped.)
		/// </summary>
		public string SensorUid
		{
			get
			{
				if ( SensorCode.Length > 0 )
				{
					return SensorSerialNumber + "#" + SensorCode;
				}

				return SensorSerialNumber;
			}
		}

        /// <summary>
        /// Time that this alarm event occurred.
        /// </summary>
        public DateTime Timestamp
        {
            get
            {
                // need to upcast to object so we don't use DateTime's operator==
                return (object)_time == null ? DomainModelConstant.NullDateTime : _time;
            }
            set
            {
                _time = value;
            }
        }

        /// <summary>
        /// Length that alarm event lasted.  Value is in seconds.
        /// </summary>
        public int Duration
        {
            get
            {
                return _duration;
            }
            set
            {
                _duration = value;
            }
        }

        /// <summary>
        /// Peak reading seen in the duration of the event.
        /// </summary>
        public double PeakReading
        {
            get
            {
                return _peakReading;
            }
            set
            {
                _peakReading = value;
            }
        }

        /// <summary>
        /// High alarm setting at time of event.
        /// </summary>
        public double AlarmHigh
        {
            get
            {
                return _alarmHigh;
            }
            set
            {
                _alarmHigh = value;
            }
        }

        /// <summary>
        /// Low alarm setting at time of event.
        /// </summary>
        public double AlarmLow
        {
            get
            {
                return _alarmLow;
            }
            set
            {
                _alarmLow = value;
            }
        }

        /// <summary>
        /// Instrument's active user at time of event.  May be empty.
        /// </summary>
        public string User
        {
            get
            {
                return _user == null ? string.Empty : _user;
            }
            set
            {
                _user = value;
            }
        }

        /// <summary>
        /// Instrument's active site at time of event.  May be empty.
        /// </summary>
        public string Site
        {
            get
            {
                return _site == null ? string.Empty : _site;
            }
            set
            {
                _site = value;
            }
        }

        /// <summary>
        /// Instrument's access level at time of event for proximity alarms.
        /// INS-8330 (INS-8624) Upload datalog proximity alarms in eventlog to iNet
        /// </summary>
        public int UserAccessLevel
        {
            get
            {
                return _userAccessLevel;
            }
            set
            {
                _userAccessLevel = value;
            }
        }

        /// <summary>
        /// Site's (a.k.a. becon's) access level at time of event for proximity alarm.
        /// INS-8330 (INS-8624) Upload datalog proximity alarms in eventlog to iNet
        /// </summary>
        public int SiteAccessLevel
        {
            get
            {
                return _siteAccessLevel;
            }
            set
            {
                _siteAccessLevel = value;
            }
        }


		/// <summary>
		/// Returns the "SensorCode" of the sensor.
		/// i.e. H2S sensor has a code of "S0002".
		/// </summary>
		public string SensorCode
		{
			get
			{
				if ( _sensorCode == null )
				{
					_sensorCode = string.Empty;
				}

				return _sensorCode;
			}
			set
			{
				_sensorCode = value;
			}
		}

        /// <summary>
        /// Returns "GasCode" of the gas type.  i.e., if GasType is "H2S",
        /// then the returned gas code is "G0002".
        /// </summary>
        public string GasCode
        {
            get
            {
                return ( _gasCode == null ) ? string.Empty : _gasCode;
            }
            set
            {
                _gasCode = value;
            }
        }

		/// <summary>
		/// True if the AlarmEvent is for a physical sensor that was in a DualSense pair 
		/// at the time of the alarm.  Sensor does not need to be working to return true for 
		/// this property.  Not used for Tango.  Should return false for virtual sensors.
		/// </summary>
		public bool IsDualSense
		{
			get
			{
				return _isDualSense;
			}
			set
			{
				_isDualSense = value;
			}
		}



        /// <summary>
        /// Number of Ticks at which the Alarm Event occured.
        /// Only used for GBPlus.
        /// </summary>
        public int Ticks
        {
            get
            {
                return _ticks;
            }
            set
            {
                _ticks = value;
            }
        }

        /// <summary>
        /// Speaker voltage, used for GBPro only.  DomainModelConstant.NullInt otherwise.
        /// </summary>
        ///
        public int SpeakerVoltage
        {
            get
            {
                return _speakerVoltage;
            }
            set
            {
                _speakerVoltage = value;
            }
        }

        /// <summary>
        /// Vibrating motor voltage, used for GBPro only.  DomainModelConstant.NullInt otherwise
        /// </summary>
        public int VibratingMotorVoltage
        {
            get
            {
                return _vibratingMotorVoltage;
            }
            set
            {
                _vibratingMotorVoltage = value;
            }
        }

        /// <summary>
        ///  Operating mode at time of alarm, used for GBPro only.
        /// </summary>
        public AlarmOperatingMode AlarmOperatingMode
        {
            get
            {
                return _alarmOperatingMode;
            }
            set
            {
                _alarmOperatingMode = value;
            }
        }

		/// <summary>
		/// The S/N of the base unit the instrument (module) was in when the alarm occurred.
		/// Only applies to SafeCore.
		/// </summary>
		public string BaseUnitSerialNumber
		{
			get
			{
				if ( _baseUnitSerialNumber == null )
					_baseUnitSerialNumber = string.Empty;

				return _baseUnitSerialNumber;
			}
			set
			{
				_baseUnitSerialNumber = value;
			}
		}

        /// <summary>
        /// Is instrument docked at the time the event occurred, used for GBPro only. Null otherwise.
        /// </summary>
        public bool? IsDocked
        {
            get
            {
                return _isDocked;
            }
            set
            {
                _isDocked = value;
            }
        }


        #endregion

        #region Methods

		/// <summary> 
		/// This is a helper method for populating the sensor code when an instrument 
		/// type does not store the sensor code in the alarm events log.
		/// 
		/// This should address all GB Plus and GB Pro sensors.  Only a subset of MX6
		/// sensors will be supported by this helper method.  It is important that this 
		/// method supports mapping gas codes that can be used on a dual-gas sensor as
		/// the Core Server will have a harder time identifying them.
		/// 
		/// Ventis, Tango and Ventis Pro should not need to use this method.
		/// </summary>
		/// <param name="gasCode">The gas code (e.g. G0001) to map to a sensor code.</param>
		/// <returns>
		/// CO   - G0001 -> S0001
		/// H2S  - G0002 -> S0002
		/// SO2  - G0003 -> S0003
		/// NO2  - G0004 -> S0004
		/// Cl2  - G0005 -> S0005
		/// ClO2 - G0006 -> S0006
		/// HCN  - G0007 -> S0007
		/// PH3	 - G0008 -> S0008
		/// H2   - G0009 -> S0009
		/// 
		/// CO2  - G0011 -> S0011
		/// NO   - G0012 -> S0012
		/// NH3  - G0013 -> S0013
		/// HCl  - G0014 -> S0014
		/// O3   - G0015 -> S0015
		/// COCl2- G0016 -> S0016
		/// HF   - G0017 -> S0017
		/// 
		/// O2   - G0020 -> S0020
		/// </returns>
		public static string GasCode2SensorCode( string gasCode )
		{
			int gasType = int.Parse( gasCode.Replace( 'G', '0' ) );

			// Most toxics and oxygen there's a one-to-one between gas code and sensor code
			if ( ( gasType > 0 && gasType <= 17 ) || gasType == 20 )
				return gasCode.Replace( 'G', 'S' );

			return string.Empty;
		}

        #endregion
	}

    #region Enums

    // The following enum, AlarmOperatingMode, contains the same values as 
    // found in the enum ISC.Instrument.Driver.OperatingMode.  Currently, 
    // only four values are expected, but the remainder are left in place
    // to maintain consistency with the driver enum.
    //
    public enum AlarmOperatingMode : ushort
    {
        Undefined = 0,
        Running = 1,         // value expected in Alarm Event data from GB Pros v2.50 or greater
        Calibrating = 2,     // value expected in Alarm Event data from GB Pros v2.50 or greater
        WarmingUp = 3,
        Setup = 4,           // value expected in Alarm Event data from GB Pros v2.50 or greater
        Bumping = 5,         // value expected in Alarm Event data from GB Pros v2.50 or greater
        Zero = 6,
        Diagnostic = 7,
        SystemAlarm = 8,
        CalibrationAlarm = 9,
        FactoryUninitialized = 10,
        FactoryInitialize = 11,
        FactoryZero = 12,
        FactoryCalibration = 13,
        FactoryRunning = 14,
        ZeroAlarm = 15,
        BatteryFailure = 16,
        FactorySleep = 17,
        FactoryBirth = 18,
        SensorMissing = 19,
        PostShutdown = 20,
        ZeroFailure = 21,
        CalibrationFailure = 22,
        Configure = 23,
        SecurityCheck = 24,
        Reawaken = 25,
        Shutdown = 26,
        PowerUp = 27,
        CalibrationComplete = 28,
        Charging = 29,
        Reset = 30
    }
    #endregion  Enums

}
