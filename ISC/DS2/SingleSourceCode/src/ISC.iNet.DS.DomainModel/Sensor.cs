using System;
using System.Collections.Generic;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a sensor.
	/// </summary>
	public class Sensor : Component 
	{	
		#region Fields

        /// <summary>
        /// Returns calibration/zeroing status of sensor.
        /// </summary>
        public virtual Status CalibrationStatus { get; set; }
        /// <summary>
        /// </summary>
        public double PeakReading { get; set; }
        /// <summary>
        /// Gets or sets the sensor span value.
        /// </summary>
        public double Span { get; set; }
        /// <summary>
        /// Gets or sets the sensor span coefficient.
        /// </summary>
        public double SpanCoef { get; set; }
        /// <summary>
        /// Gets or sets the sensor minimum span coefficient value.
        /// </summary>
        public double SpanCoefMin { get; set; }
        /// <summary>
        /// Gets or sets the sensor maximum span coefficient value.
        /// </summary>
        public double SpanCoefMax { get; set; }
        /// <summary>
        /// Gets or sets the sensor zero offset value.
        /// </summary>
        public double ZeroOffset { get; set; }
        /// <summary>
        /// Gets or sets the zero minimum value.
        /// </summary>
        public double ZeroMin { get; set; }
        /// <summary>
        /// Gets or sets the zero maximum value.
        /// </summary>
        public double ZeroMax { get; set; }
        /// <summary>
        /// Gets or sets the sensor resolution value.
        /// </summary>
        public double Resolution { get; set; }
        /// <summary>
        /// Gets or sets the sensor low temperature compensation.
        /// </summary>
        public double TemperatureCompLow { get; set; }
        /// <summary>
        /// Gets or sets the sensor high temperature compensation.
        /// </summary>
        public double TemperatureCompHigh { get; set; }
        /// <summary>
        /// Gets or sets the sensor's maximum read temperature
        /// </summary>
        public int MaxTemperature { get; set; }
        /// <summary>
        /// Gets or sets the sensor's minimum read temperature
        /// </summary>
        public int MinTemperature { get; set; }
        /// <summary>
        /// Gets or sets sensor calibration gas concentration.
        /// </summary>
		public double CalibrationGasConcentration;
        /// <summary>
        /// Gets or sets the sensor over range value.
        /// </summary>
        public int OverRange { get; set; }
        /// <summary>
		/// Gets or sets the sensor dead band value.
		/// </summary>
		public int DeadBand { get; set; }
        /// <summary>
		/// Gets or sets the sensor filter level.
		/// </summary>
		public int Filter { get; set; }
    	/// <summary>
		/// Gets or sets the sensor calibration timeout value. Value is in seconds.
		/// </summary>
        public int CalibrationTimeout { get; set; }
        /// <summary>
        /// Gets or sets the sensor polarity value.
        /// </summary>
        public int Polarity { get; set; }
        /// <summary>
        /// Gets or sets the sensor calibration gas type.
        /// </summary>
        public GasType CalibrationGas { get; set; }
        /// <summary>
        /// Gets or Sets the sensor bump test status. 
        /// True represent last sensor bump test has PASSED and False represent last sensor bump test has FAILED.
        /// </summary>
        public bool BumpTestStatus { get; set; }
		/// <summary>
		/// Gets or sets if the sensor is capable of running in DualSense mode.
		/// For Ventis Pro, two DualSense capable sensors with the same (raw) sensor part number and sensor type code 
		/// should be assumed to be running in DualSense.  
		/// </summary>
		public bool IsDualSenseCapable { get; set; }

		private SensorAlarm _alarm;
        private string _gasDetected;   // For PIDs only
        private ResponseFactor _responseFactor;

		private string _technology;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a Sensor class.
		/// </summary>
		public Sensor()
		{
			Initialize();
		}

		/// <summary>
		/// Creates a new instance of a Sensor class when its serial number is provided.
		/// </summary>
		/// <param name="serialNumber">Sensor serial number</param>
		public Sensor( string serialNumber ) : base ( serialNumber )
		{
			Initialize();
		}

        /// <summary>
        /// This method initializes local variables and is called by the constructors of the class.
        /// </summary>
        private void Initialize()
        {
            this.Alarm = new SensorAlarm();
            this.Span = SpanCoef = SpanCoefMin = SpanCoefMax = DomainModelConstant.NullDouble;
            this.ZeroMax = ZeroMin = ZeroOffset = DomainModelConstant.NullDouble;
            this.Resolution = DomainModelConstant.NullDouble;
            this.TemperatureCompHigh = TemperatureCompLow = DomainModelConstant.NullDouble;
            this.MinTemperature = MaxTemperature = DomainModelConstant.NullInt;
            this.CalibrationGasConcentration = DomainModelConstant.NullDouble;
            this.Filter = DomainModelConstant.NullInt;
            this.Polarity = DomainModelConstant.NullInt;
            this.OverRange = DomainModelConstant.NullInt;
            this.DeadBand = DomainModelConstant.NullInt;
            this.CalibrationTimeout = DomainModelConstant.NullInt;
            this.GasDetected = string.Empty;
            this._responseFactor = new ResponseFactor();
            this.Type = new SensorType();
            this.CalibrationStatus = Status.Unknown;
        }

		#endregion

		#region Properties

        /// <summary>
        /// Gets the component type.
        /// </summary>
        public override ComponentType Type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = ( value == null ) ? (ComponentType)new SensorType() : value;
            }
        }

		/// <summary>
		/// The "technology" of the sensor. e.g. PID, COSH, TOX, LEL, etc.
		/// </summary>
		public string Technology
		{
			get { return _technology == null ? string.Empty : _technology; }
			set { _technology = value; }
		}

        /// <summary>
        /// Gets or sets the component UID.  The UID is the serial number and sensor
        /// code separated by a "#".  e.g. "1234567890#S0020".
        /// </summary>
        public override string Uid
        {
            get
            {
                return base.Uid;
            }
            set
            {
                if ( value == null )
                {
                    _serialNumber = null;
                    _type = null;
                    return;
                }

                int hash = value.LastIndexOf( '#' );

                if ( hash == -1 )
                {
                    _serialNumber = value.Trim().ToUpper();
                    _type = null;
                }
                else
                {
                    _serialNumber = value.Substring( 0, hash ).Trim().ToUpper();

                    string typeString = value.Substring( hash + 1 );

                    ComponentType oldType = _type;

                    _type = new SensorType( typeString.Trim().ToUpper() );

                    // The "Type" property used to be a read-only field.
                    // But now IDS will set the SensorType on a SettingsRead
                    // (it needs to upload the MeasurementType as programmed into
                    // the sensor for iNet).  A problem was occurring, though, in that
                    // on deserializing the data uploaded from the IDS, DSS was never
                    // getting the measurment type that the IDS sent.  This was traced
                    // back to the deserialization of the serial number.  i.e., the 
                    // sensortype and its measurment was getting deserialized perfectly
                    // fine, but then the serialnumber was getting deserialized afterwards 
                    // causing the sensortype that was just deserialized to get lost.
                    // (There is no way to control order of which fields gets deserialized.)
                    // Therefore, on setting the serialnumber, we hold onto and sensortype
                    // measurment that may be been set previously.
                    if ( oldType is SensorType )
                        ( (SensorType)_type ).MeasurementType = ( (SensorType)oldType ).MeasurementType;
                }
            }
        }

		/// <summary>
		/// Gets or sets the sensor alarm.
		/// </summary>
		public SensorAlarm Alarm 
		{
			get { return _alarm; }
            set { _alarm = ( value == null ) ? new SensorAlarm() : value; }
		}

        /// <summary>
        /// For PIDs only, indicates the type of gas the 
        /// sensor is currently configured to detect.
        /// 
        /// THIS PROPERTY NEEDS TO BE KEPT TO MAINTAIN BACKWARD COMPATIBILITY
        /// WITH VX500 DOCKING STATIONS RUNNING V4.1 THROUGH 4.3.
        /// 
        /// Only used by DOCKINGSTATIONS RUNNING V4.1 THROUGH 4.3.  NEWER
        /// DOCKING STATIONS USE "GasFactor" PROPERTY INSTEAD.
        /// 
        /// </summary>
//        [Obsolete("Use Sensor.GasFactor property instead", true)] 
        public string GasDetected
        {
            get { return _gasDetected; }
            set { _gasDetected = ( value == null ) ? string.Empty : value; }
        }

        /// <summary>
        /// Gets the bump criterion type.
        /// </summary>
        /// <remarks>SGF  Jan-2-2009  DSW-173, DSW-174</remarks>
        public CriterionType BumpCriterionType
        {
            get
            {
                switch ( Type.Code )
                {
                    case SensorCode.O2:
                        return CriterionType.O2;

                    case SensorCode.Cl2:
                    case SensorCode.HCl:
                    case SensorCode.ClO2:
                        return CriterionType.PPMLimit;

                    default:
                        return CriterionType.FullSpanValue;
                }
            }
        }

        /// <summary>
        /// Gets the criterion limit value (if the criterion type is PPMLimit).
        /// </summary>
        /// <remarks>SGF  Jan-2-2009  DSW-173, DSW-174, dev DSZ-828</remarks>
        public double BumpCriterionPPMLimit
        {
            get
            {
                if ( Type.Code == SensorCode.Cl2 )
                    return 1.0;

                // For CLO2 sensors, we bump using CL2 gas.  We pass CLO2 bumps if sensor can reach 0.5ppm when CL2 gas is applied.
                if ( Type.Code == SensorCode.ClO2 )
                    return 0.5;

                // SGF  Jan-16-2009  DSZ-828 (dev)  changed from 1.0ppm to 2.5 ppm 
                if ( Type.Code == SensorCode.HCl )
                    return 2.5; 

                return double.MinValue; // error case
            }
        }

        /// <summary>
        /// Returns the sensor's pause time for calibration preconditions.
        /// </summary>
        /// <returns>
        /// Number of seconds.
        /// Zero is returned if sensor should not be preconditioned.
        /// </returns>
        public long CalPreconditionPauseTime // SGF  Jan-13-2009  DSW-173
        {
            get
            {
                if ( Type.Code == SensorCode.Cl2 || Type.Code == SensorCode.HCl ) // SGF  Nov-11-2009  DSW-350 (DS2 v7.6)
                    return 60; // 60 second pause for CL2  // SGF  Jan-22-2009  DSZ-831 (dev) -- up from 30 seconds

                return 0; // default
            }
        }

        
        /// <summary>
        /// Returns the sensor's pause time for bump test preconditions.
        /// </summary>
        /// <returns>
        /// Number of seconds.
        /// Zero is returned if sensor should not be preconditioned.
        /// </returns>
        public long BumpPreconditionPauseTime // SGF  Jan-13-2009  DSW-173
        {
            get
            {
                if ( Type.Code == SensorCode.Cl2 || Type.Code == SensorCode.ClO2 )
                    return 60; // 60 second pause // SGF  Jan-22-2009  DSZ-831 (dev) -- up from 30 seconds
                else if ( Type.Code == SensorCode.HCl )
                    return 120; // SGF  Nov-30-2009  DSZ-861

                return 0; // default
            }
        }

        /// <summary>
        /// Certain sensors need to be purged for a longer time after calibration
        /// due to cross sensitivity or the gases being "sticky".
        /// </summary>
		/// <remarks>INS-6723</remarks>
        public bool RequiresExtendedPostCalibrationPurge
        {
            get 
			{ 
				return Enabled && ( 
				Type.Code == SensorCode.PID ||
				Type.Code == SensorCode.NO2 ||
				Type.Code == SensorCode.NH3 ||
				Type.Code == SensorCode.HCl || 
				Type.Code == SensorCode.Cl2 || 
				Type.Code == SensorCode.ClO2 ); 
			}
        }

        /// <summary>
        /// Determines if the sensor requires a precondition step prior to bump testing.
        /// </summary>
        /// <remarks>SGF  Nov-11-2009  DSW-173, DSW-174, DSW-350 (DS2 v7.6)</remarks>
        public bool RequiresBumpPrecondition
        {
            get { return Type.Code == SensorCode.Cl2 || Type.Code == SensorCode.HCl || Type.Code == SensorCode.ClO2; }
        }

        /// <summary>
        /// Determines if the sensor requires a purge before and after bump testing.
        /// Certain exotic sensors require this purge.
        /// </summary>
        /// <remarks>INS-6723</remarks>
		public bool RequiresBumpTestPurge( List<GasEndPoint> gasEndPoints )
        {
            if ( !Enabled )
                return false;

            if ( Type.Code == SensorCode.PID ||
				 Type.Code == SensorCode.NO2 ||
				 Type.Code == SensorCode.NH3 ||
				 Type.Code == SensorCode.HCl || 
				 Type.Code == SensorCode.Cl2 )
                return true;

            // Only return true for ClO2 if we know that we will actually be bumping
            // the sensor (i.e., if Cl2 is available).
            if ( Type.Code == SensorCode.ClO2 && IsBumpEnabled( gasEndPoints ) )
                return true;

            return false;
        }

        /// <summary>
        /// Determines if the sensor requires that all calibrations and bump tests be
        /// performed by an operator via manual operations.
        /// </summary>
        /// <remarks>SGF  24-May-2012  INS-3078</remarks>
        public virtual bool RequiresManualOperation( string eventCode, List<GasEndPoint> gasEndPoints )
        {
            // Can't calibrate nor bump O3 sensors.
            if ( Type.Code == SensorCode.O3 )
                return true;

            if ( Type.Code == SensorCode.ClO2 )
            {
                // Can't ever calibrate ClO2 sensors.
                if ( eventCode == EventCode.Calibration )
                    return true;

                // If Cl2 is currently available, then CLO2 sensor can be bumped.
                // If no Cl2 is currently available, then can't bump test a CLO2 sensor.
                if ( gasEndPoints.Find( gep => gep.Cylinder.ContainsGas( GasCode.Cl2 ) ) == null )
                    return true;
            }
            return false;
        }
        
		#endregion

		#region Methods

        /// <summary>
        /// Returns whether or not specified sensor should be bump tested or ignored.
        /// </summary>
        /// <param name="installedComponent"></param>
        /// <returns></returns>
		public bool IsBumpEnabled( List<GasEndPoint> gasEndPoints )
        {
            // Can't calibrate nor bump O3 sensors.
            if ( Type.Code == SensorCode.O3 )
                return false;

            if ( Type.Code == SensorCode.ClO2 )
            {
                // If Cl2 is currently available, then CLO2 sensor can be bumped.
                // If no Cl2 is currently available, then can't bump test a CLO2 sensor.
                if ( gasEndPoints.Find( gep => gep.Cylinder.ContainsGas( GasCode.Cl2 ) ) == null )
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Determines the gas being used to cal/bump this sensor.
        /// </summary>
        public string GetGasToCal()
        {
            if ( ( this.CalibrationGas.Code != string.Empty ) && ( this.CalibrationGas.Code != GasCode.Uninstalled ) )
                return this.CalibrationGas.Code;

            if ( this.Type.Code == SensorCode.HF )
                return GasCode.HCl; // Hydrogen Flouride uses HCl to calibrate

            if ( this.Type.Code == SensorCode.CombustibleCH4 || this.Type.Code == SensorCode.MethaneIR || this.Type.Code == SensorCode.MethaneIRLEL ) //Suresh 19-OCTOBER-2011 INS-2354
                return GasCode.Methane;

            // the Combustible-LEL can be calibrated by any of the combustible gases because
            // it is %.  It also displays any of those gases in %. It can be cal with hydrogen.
            if ( this.Type.Code == SensorCode.CombustibleLEL || this.Type.Code == SensorCode.CombustiblePPM )
                return GasCode.Pentane;

            if ( this.Type.Code == SensorCode.PID )
                // TODO: Handle all of the possibilities.
                return GasCode.Isobutylene;

            // For everything else, we assume a one-to-one relationship between gas code and sensor code.
            string tmpGasCode = this.Type.Code.Replace( 'S', 'G' );
            return tmpGasCode;
        }

        /// <summary>
        /// Returns whether or not this Sensor is a virtual sensor or not.
        /// <para>(Returns true if the UID starts with "VIRTUAL".)</para>
        /// </summary>
        /// <returns></returns>
        public bool IsVirtual
        {
            get { return Component.IsVirtualUid( Uid ); }
        }

		/// <summary>
		/// Copies the sensor to a destination component.
		/// </summary>
		/// <param name="component">The destination component.</param>
		public override void CopyTo( Component component )
		{
			base.CopyTo( component );

			Sensor sensor = (Sensor)component;

            ////////////////////////////////////////////////////////////////
            // NOTE: IF ANY FIELDS ARE ADDED HERE THAT CAN ARE CONSIDERED
            // 'SETTINGS' THAT CAN BE MODIFIED BY THE USER, THEN BE SURE TO
            // ALSO ADD THE FIELDS TO CopySettings FUNCTION FARTHER BELOW
            ////////////////////////////////////////////////////////////////

			sensor.Alarm = ( SensorAlarm ) Alarm.Clone();
			sensor.CalibrationGasConcentration = CalibrationGasConcentration;
			sensor.CalibrationGas = CalibrationGas;
			sensor.CalibrationTimeout = CalibrationTimeout;
			sensor.DeadBand = DeadBand;
			sensor.Filter = Filter;
			sensor.OverRange = OverRange;
			sensor.Polarity = Polarity;
			sensor.Resolution = Resolution;
			sensor.SetupDate = SetupDate;
			sensor.Span = Span;
			sensor.SpanCoef = SpanCoef;
			sensor.SpanCoefMax = SpanCoefMax;
			sensor.SpanCoefMin = SpanCoefMin;
			sensor.TemperatureCompHigh = TemperatureCompHigh;
			sensor.TemperatureCompLow = TemperatureCompLow;
			sensor.ZeroMax = ZeroMax;
			sensor.ZeroMin = ZeroMin;
			sensor.ZeroOffset = ZeroOffset;
            sensor.PeakReading = PeakReading;
            sensor.MaxTemperature = MaxTemperature;
            sensor.MinTemperature = MinTemperature;
            sensor.GasDetected = GasDetected;
            sensor.CalibrationStatus = CalibrationStatus;
            sensor.BumpTestStatus = BumpTestStatus;
			sensor.IsDualSenseCapable = IsDualSenseCapable;
//            sensor.GasFactor = (ResponseFactor)this.GasFactor.Clone();
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a Sensor object.
		/// </summary>
		/// <returns>Sensor object</returns>
		public override object Clone()
		{
			Sensor sensor = new Sensor();

			CopyTo( sensor );
			
			return sensor;
		}


		#endregion

	}
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a Sensor Type.
	/// </summary>
	public class SensorType : ComponentType
	{

		#region Fields

		private MeasurementType _measurementType = MeasurementType.Unknown;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a SensorType class.
		/// </summary>
		public SensorType()
		{
		}

		/// <summary>
		/// Creates a new instance of a SensorType class when its code is provided.
		/// </summary>
		/// <param name="code">Sensor type code</param>
		public SensorType( string code ) : base ( code )
		{
			// Do nothing
		}

		#endregion

		#region Properties
		
		/// <summary>
		/// Gets or sets gas sensor measurement type.
		/// </summary>
		public MeasurementType MeasurementType 
		{
			get
			{
				return _measurementType;
			}
			set
			{
				_measurementType = value;
			}
		}

		#endregion

		#region Methods

		#endregion

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides properties and methods to define a sensor alarm.
	/// </summary>
	public class SensorAlarm : ICloneable
	{

		#region Fields

		private double _gasAlert;
		private double _low;
		private double _high;
		private double _twa;
		private double _stel;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a SensorAlarm class.
		/// </summary>
		public SensorAlarm()
		{
			Intialize();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the gas alert value.
		/// </summary>
		public double GasAlert
		{
			get
			{
				return _gasAlert;
			}
			set
			{
				_gasAlert = value;
			}
		}

		/// <summary>
		/// Gets or sets the sensor low alarm value.
		/// </summary>
		public double Low 
		{
			get
			{
				return _low;
			}
			set
			{
				_low = value;
			}
		}

		/// <summary>
		/// Gets or sets the sensor high alarm value.
		/// </summary>
		public double High 
		{
			get
			{
				return _high;
			}
			set
			{
				_high = value;
			}
		}

		/// <summary>
		/// Gets or sets the sensor alarm short term exposure limit.
		/// </summary>
		public double STEL 
		{
			get
			{
				return _stel;
			}
			set
			{
				_stel = value;
			}
		}

		/// <summary>
		/// Gets or sets the sensor alarm time weighted average.
		/// </summary>
		public double TWA 
		{
			get
			{
				return _twa;
			}
			set
			{
				_twa = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// This method initializes local variables and is called by the constructors of the class.
		/// </summary>
		private void Intialize()
		{
			GasAlert = double.MinValue;
			Low = double.MinValue;
			High = double.MinValue;
			STEL = double.MinValue;
			TWA = double.MinValue;
		}

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return Low.ToString() + ", " + High.ToString() + ", " + STEL.ToString() + "," + TWA.ToString();
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a SensorAlarm object.
		/// </summary>
		/// <returns>SensorAlarm object</returns>
		public virtual object Clone()
		{
            return this.MemberwiseClone();
		}

		#endregion

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides properties and methods to define a sensor alarm setting.
	/// </summary>
	public class SensorAlarmSetting : SensorAlarm
	{

		#region Fields

		private DeviceType _instrumentType;
		private SensorType _sensorType;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a SensorAlarmSetting class.
		/// </summary>
		public SensorAlarmSetting()
		{
			// Do nothing
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the instrument type.
		/// </summary>
		public DeviceType InstrumentType
		{
			get
			{
				return _instrumentType;
			}
			set
			{
				_instrumentType = value;
			}
		}

		/// <summary>
		/// Gets or sets the sensor type.
		/// </summary>
		public SensorType SensorType
		{
			get
			{
				if ( _sensorType == null )
				{
					_sensorType = new SensorType();
				}

				return _sensorType;
			}
			set
			{
				_sensorType = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a SensorAlarmSetting object.
		/// </summary>
		/// <returns>SensorAlarmSetting object</returns>
		public override object Clone()
		{
            SensorAlarmSetting sensorAlarmSetting = (SensorAlarmSetting)base.Clone();
			sensorAlarmSetting.SensorType = (SensorType)SensorType.Clone();
			return sensorAlarmSetting;
		}

		#endregion

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Defines gas measurement types.
	/// </summary>
    /// <remarks>
    /// DO NOT CHANGE THE ORDER OF THESE.  THE INTEGER VALUES
    /// ARE, UNFORTUNATELY, BEING UPLOADED TO INET, SO CHANGING 
    /// THESE VALUES WOULD 'BREAK' INET.
    /// </remarks>
	public enum MeasurementType
	{
		Unknown , // 0 - DO NOT CHANGE THIS VALUE; IT IS UPLOADED TO INET
        PPM,      // 1 - DO NOT CHANGE THIS VALUE; IT IS UPLOADED TO INET
        VOL,      // 2 - DO NOT CHANGE THIS VALUE; IT IS UPLOADED TO INET
        LEL       // 3 - DO NOT CHANGE THIS VALUE; IT IS UPLOADED TO INET
	}

	/// <summary>
	/// NOTE: Transient statuses are not used in the datalog.
	/// </summary>
    [Flags] public enum SensorStatuses : uint
    {
        OK          = 0x0000,
        AlarmLow    = 0x0001, // bit 0 = low alarm
        AlarmHigh   = 0x0002, // bit 1 = high alarm 
        Underrange  = 0x0004, // bit 2 = underrange condition
        Overrange   = 0x0008, // bit 3 = overrange condition
        CalFault    = 0x0010, // bit 4 = calibration fault
        ZeroFault   = 0x0020, // bit 5 = zero fault
        Locked      = 0x0040, // bit 6 = instrument locked operation of this sensor (e.g., if an LEL sensor goes into overrange, it is turned off until acknowledged by the user.)
        Disabled    = 0x0080, // bit 7 = user disabled
        BumpFault   = 0x0100, // bit 8 = bump fault
		DualSense	= 0x0200, // bit 9 = sensor belongs to a DualSense pair (not supported by TX1)
        CalPastDue  = 0x0400, // bit 10 = calibration past due
        Missing     = 0x0800, // bit 11 = missing sensor (should never be seen in datalog, since uninstalled sensors aren't included in the data) 
        Failed      = 0x1000, // bit 12 = sensor failed (sensor data error) (bad checksum?)
        AlarmTWA    = 0x4000, // bit 14 = TWA alarm
        AlarmSTEL   = 0x8000  // bit 15 = STEL alarm
    }

	public enum CriterionType
	{
		FullSpanValue ,
		PPMLimit ,
        O2
	}

}
