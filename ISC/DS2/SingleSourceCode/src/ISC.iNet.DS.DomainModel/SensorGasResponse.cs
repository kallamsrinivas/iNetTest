using System;
using System.Collections.Generic;


namespace ISC.iNet.DS.DomainModel
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a sensor gas response.
	/// </summary>
	public class SensorGasResponse : IPassed, ICloneable
	{
		
		#region Fields

        public const int DEFAULT_BUMP_THRESHOLD = 50;
        public const int MIN_BUMP_TRESHOLD = 50;
        public const int MAX_BUMP_THRESHOLD = 99;

		public const int DEFAULT_BUMP_TIMEOUT = 120;
		public const int MIN_BUMP_TIMEOUT = 30;	
		public const int MAX_BUMP_TIMEOUT = 300;

		private string _serialNumber;
        private DateTime _time = DateTime.MinValue;
        private int _duration = int.MinValue;
		private GasConcentration _gasConcentration = null;
		private double _reading = double.MinValue;
		private GasResponseType _type = GasResponseType.Bump;
		private string _sensorCode = string.Empty;
        private string _gasDetected = string.Empty;
		private Status _calibrationStatus = Status.Unknown;
        private int _sensorBaseLine = int.MinValue;
        private double _sensorZeroOffset = double.MinValue;
        private double _sensorSpanCoeff = double.MinValue;
		private string _cylinderSN = string.Empty;
        private DateTime _cylinderExpiration = DateTime.MinValue;
        private int _threshold = int.MinValue;
		private int _timeout = int.MinValue;
        private AccessoryPumpSetting _accessoryPump = AccessoryPumpSetting.NotApplicable;
        private int _position = DomainModelConstant.NullInt;
        private int _manualOperationId = DomainModelConstant.NullInt;  // DSW-220, 6/14/2010, JMP
        private double _readingAfterZeroing = double.MinValue;
        private DateTime _timeAfterZeroing = DateTime.MinValue;
        private double _readingAfterPreconditioning = double.MinValue;
        private DateTime _timeAfterPreconditioning = DateTime.MinValue;
        private double _readingAfterPurging = double.MinValue;
        private DateTime _timeAfterPurging = DateTime.MinValue; 
        private int _cumulativeResponseTime = int.MinValue;
		private DateTime _preCal_LastCalibrationTime = DateTime.MinValue;
		private DateTime _postCal_LastCalibrationTime = DateTime.MinValue;
																	
        private List<UsedGasEndPoint> _usedGasEndPoints;

        // INS-7625 SSAM v7.6
        private bool _isO2HighBumpPassed = false;
        private bool _isSecondO2HighBump = false;

		#endregion
		
		#region Constructors

		/// <summary>
		/// Creates a new instance of SensorGasResponse class.
		/// </summary>
        public SensorGasResponse()
        {
           //Initialize();
        }

		/// <summary>
		/// Creates a new instance of SensorGasResponse class when sensor serial number and response time are provided.
		/// </summary>
        /// <param name="uid">
        /// Sensor gas response UID.
        /// The UID is the serial number and sensor
        /// code separated by a "#".  e.g. "1234567890#S0020".
        /// </param>
		/// <param name="time">Gas response date/time</param>
		public SensorGasResponse( string uid , DateTime time )
		{
            //Initialize();
            Uid = uid;
			Time = time;			
		}

		#endregion

		#region Properties

        /// <summary>
        /// Gets or sets the component UID.  The UID is the serial number and sensor
        /// code separated by a "#".  e.g. "1234567890#S0020".
        /// </summary>
		public string Uid
		{
            get
            {
                return SerialNumber + '#' + SensorCode;
            }
            set
            {
                if ( value == null )
                {
                    _serialNumber = null;
                    _sensorCode = null;
                    return;
                }

                int hash = value.LastIndexOf( '#' );

                if ( hash == -1 )
                {
                    _serialNumber = value.Trim().ToUpper();
                    _sensorCode = null;
                }
                else
                {
                    _serialNumber = value.Substring( 0, hash ).Trim().ToUpper();
                    _sensorCode = value.Substring( hash + 1 ).Trim().ToUpper();
                }
            }		
		}

        /// <summary>
        /// Returns the base serial number. i.e., the simple serial number, minus the sensor code.
        /// </summary>
        public string SerialNumber
        {
            get
            {
                if ( _serialNumber == null ) _serialNumber = string.Empty;
                return _serialNumber;
            }
        }

		/// <summary>
		/// Gets or sets the response time.
		/// </summary>
		public DateTime Time
		{
			get
			{
				return _time;
			}
			set
			{
				_time = value;
			}
		}

        /// <summary>
        /// 
        /// </summary>
        public int Threshold
        {
            get
            {
                // If this is a bump test, and threshold is minvalue, then this
                // must be an old bump test that existed in the database prior
                // to when the threshold field was added.  In this situation,
                // we can assume the threshold was and therefor is the default.
                if ( _threshold == int.MinValue && Type == GasResponseType.Bump )
                    return SensorGasResponse.DEFAULT_BUMP_THRESHOLD;

                return _threshold;
            }
            set
            {
                _threshold = value;
            }
        }

		/// <summary>
		/// The timeout (in seconds) that was used for this bump/cal.
		/// </summary>
		public int Timeout
		{
			get
			{
				// If this is a bump test, and timeout is minvalue, then this
				// must be an old bump test that existed in the database prior
				// to when the timeout field was added.  In this situation,
				// we can assume the threshold was and therefor is the default.
//				if ( _timeout == int.MinValue && Type == GasResponseTypes.Bump )
//					return SensorGasResponse.DEFAULT_BUMP_TIMEOUT;

				return _timeout;
			}
			set
			{
				_timeout = value;
			}
		}

        /// <summary>
        /// Indicates if instrument had a pump attached during the bump/cal
        /// </summary>
        public AccessoryPumpSetting AccessoryPump
        {
            get
            {
                return _accessoryPump;
            }
            set
            {
                _accessoryPump = value;
            }
        }

		/// <summary>
		/// Gets or sets the expiration date of the cylinder used
		/// to perform this gas operation.
		/// </summary>
		public DateTime CylinderExpiration
		{
			get
			{
				// change the property to fetch the last cylinder if it is currently not set 
				if (_cylinderExpiration == DateTime.MinValue )
				{
					return GetCylinderExpForReport();
				}

				return _cylinderExpiration;
			}
			set
			{
				_cylinderExpiration = value;
			}
		}

		/// <summary>
		/// The SN of the cylinder used to cal or bump.
		/// Will contain the factoryid if iGas, else the manualSN, if any.
		/// </summary>
		public string CylinderSn
		{
			get
			{
				// change the property to fetch the last cylinder if it is currently not set 
				if ( ( _cylinderSN == null ) || ( _cylinderSN == string.Empty ) )
				{
					return GetCylinderSNForReport();
				}

				return _cylinderSN;
			}
			set
			{
				_cylinderSN = value;
			}
		}

		/// <summary>
        /// How long the operation took.
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
        /// 
        /// </summary>
        public int BaseLine
        {
            get
            {
                return _sensorBaseLine;
            }
            set
            {
                _sensorBaseLine = value;
            }
        }

        /// <summary>
        /// BaeLine * sensor's resolution
        /// </summary>
        public double ZeroOffset
        {
            get
            {
                return _sensorZeroOffset;
            }
            set
            {
                _sensorZeroOffset = value;
            }
        }

        /// <summary>
        /// Gets or sets gas concentration of the response.
        /// </summary>
        public GasConcentration GasConcentration
        {
            get
            {
                return _gasConcentration;
            }
            set
			{
				_gasConcentration = value;
			}
		}

		/// <summary>
		/// Gets or sets reading of the response.
		/// </summary>
		public double Reading
		{
			get
			{
				return _reading;
			}
			set
			{
				_reading = value;
			}
		}

        /// <summary>
        /// Gets or sets reading of the response.
        /// </summary>
        public double SpanCoef
        {
            get
            {
                return _sensorSpanCoeff;
            }
            set
            {
                _sensorSpanCoeff = value;
            }
        }

		public double FullSpanReserve
		{
            get
            {
                // If reading is undefined, then we can't calculate SpanReserve,
                // so it, too, is considered undefined.
				// Gas concentration will be undefined when status is InstrumentAborted.
                if ( _reading == double.MinValue || _gasConcentration == null)
                    return double.MinValue;

                // Bumping of O2 is special - we're trying to see if the sensor
                // can see LACK OF oxygen.
                if ( Type == GasResponseType.Bump && _sensorCode == DomainModel.SensorCode.O2 )
                {
                    double gas = _gasConcentration.Concentration;
                    double ambient = 20.9;
                    double retVal = _reading;

                    if ( gas > ambient )
                    {
                        retVal = Math.Round( Convert.ToDouble( ( _reading - ambient ) / ( gas - ambient ) * 100 ) , 2 );
                        return retVal;
                    }
                    else if ( gas < ambient )
                    {
                        retVal = Math.Round( Convert.ToDouble( ( ambient - _reading ) / ( ambient - gas ) * 100 ) , 2 );
                        return retVal;
                    }
                    return retVal;
                }
                // If we make it to here, we're either calibrating anything, or bumping non-O2.
                return Math.Round( _reading / _gasConcentration.Concentration * 100.0D, 2 );
            }
        }

        public GasResponseType Type
		{
			get
			{
				return _type;
			}

			set
			{
				_type = value;
			}
		}

		/// <summary>
		/// The sensor code for the sensor used that this gas response is from.
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
		}

        /// <summary>
        /// Indicates the type of gas the sensor was currently configured to detect.
        /// </summary>
        public string GasDetected
        {
            get { return _gasDetected; }
            set { _gasDetected = ( value == null ) ? string.Empty : value; }
        }

		/// <summary>
		/// The status, as reported by the instrument, at the end of the calibration.
		/// </summary>
		public Status Status
		{
			get
			{
				return _calibrationStatus;
			}

			set
			{
				_calibrationStatus = value;
			}
		}

		/// <summary>
		/// Did the test pass or fail, based on the status field.
		/// </summary>
		public bool Passed
		{
			get
			{
                // SGF  19-Oct-2011  INS-3973
                // Added the switch statement, as a result of adding an additional case that should report as 'true'
                switch (_calibrationStatus)
                {
                    case Status.Passed:
                    case Status.PassedManual:
                    case Status.ZeroPassed:
                    case Status.Skipped: //Suresh 19-APR-2012 INS-4537 (DEV)
                        return true;
                    default:
                        return false;
                }
			}
		}

        /// <summary>
        /// Position of a sensor in the instrument
        /// </summary>
        public int Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        // SGF  14-Jun-2011  INS-1732 - need to upload this value based on requirements of German law, Berufsgenossenschaft Chemie.
        /// <summary>
        /// Sensor reading after zeroing of the sensor is complete
        /// </summary>
        public double ReadingAfterZeroing
        {
            get
            {
                return _readingAfterZeroing;
            }
            set
            {
                _readingAfterZeroing = value;
            }
        }

        // SGF  14-Jun-2011  INS-1732 - need to upload this value based on requirements of German law, Berufsgenossenschaft Chemie.
        /// <summary>
        /// Time that the reading following the zeroing of the sensor was taken
        /// </summary>
        public DateTime TimeAfterZeroing
        {
            get
            {
                return _timeAfterZeroing;
            }
            set
            {
                _timeAfterZeroing = value;
            }
        }

        // SGF  14-Jun-2011  INS-1732 - need to upload this value based on requirements of German law, Berufsgenossenschaft Chemie.
        /// <summary>
        /// Sensor reading after preconditioning of the sensor is complete
        /// </summary>
        public double ReadingAfterPreconditioning
        {
            get
            {
                return _readingAfterPreconditioning;
            }
            set
            {
                _readingAfterPreconditioning = value;
            }
        }

        // SGF  14-Jun-2011  INS-1732 - need to upload this value based on requirements of German law, Berufsgenossenschaft Chemie.
        /// <summary>
        /// Time that the reading following the preconditioning of the sensor was taken
        /// </summary>
        public DateTime TimeAfterPreconditioning
        {
            get
            {
                return _timeAfterPreconditioning;
            }
            set
            {
                _timeAfterPreconditioning = value;
            }
        }

        // SGF  14-Jun-2011  INS-1732 - need to upload this value based on requirements of German law, Berufsgenossenschaft Chemie.
        /// <summary>
        /// Sensor reading after purging of the sensor is complete
        /// </summary>
        public double ReadingAfterPurging
        {
            get
            {
                return _readingAfterPurging;
            }
            set
            {
                _readingAfterPurging = value;
            }
        }

        // SGF  14-Jun-2011  INS-1732 - need to upload this value based on requirements of German law, Berufsgenossenschaft Chemie.
        /// <summary>
        /// Time that the reading following the purging of the sensor was taken
        /// </summary>
        public DateTime TimeAfterPurging
        {
            get
            {
                return _timeAfterPurging;
            }
            set
            {
                _timeAfterPurging = value;
            }
        }

        // SGF  14-Jun-2011  INS-1732 - need to upload this value based on requirements of German law, Berufsgenossenschaft Chemie.
        /// <summary>
        /// Cumulative response time for this sensor
        /// </summary>
        public int CumulativeResponseTime
        {
            get
            {
                return _cumulativeResponseTime;
            }
            set
            {
                _cumulativeResponseTime = value;
            }
        }

		/// <summary>
		/// Gets or sets the last calibration time of the sensor before starting a new calibration.  This
		/// is used in conjuction with the PostCal_LastCalibrationTime property.
		/// </summary>
		public DateTime PreCal_LastCalibrationTime
		{
			get
			{
				return _preCal_LastCalibrationTime;
			}
			set
			{
				_preCal_LastCalibrationTime = value;
			}
		}

		/// <summary>
		/// Gets or sets the last calibration time of the sensor after finishing a new calibration.  This
		/// is used in conjuction with the PreCal_LastCalibrationTime property.  The instrument will only
		/// update the last calibration time on the sensor after a passed calibration.
		/// </summary>
		public DateTime PostCal_LastCalibrationTime
		{
			get
			{
				return _postCal_LastCalibrationTime;
			}
			set
			{
				_postCal_LastCalibrationTime = value;
			}
		}

        /// <summary>
        /// Gets or sets the list of cylinders used.
        /// </summary>
        public List<UsedGasEndPoint> UsedGasEndPoints
        {
            get
            {
                if ( _usedGasEndPoints == null )
                    _usedGasEndPoints = new List<UsedGasEndPoint>();

                return _usedGasEndPoints;
            }
            set
            {
                _usedGasEndPoints = value;
            }
        }

        /// <summary>
        /// Bump / calibration ID for manual gas operations.
        /// Responses with same Manual ID are all part of the same manual calibration or bump test.
        /// </summary>
        /// <remarks>
        /// This property should not be persisted as it's only unique for particular download of
        /// instrument's manual gas operations log.
        /// Upon clearing the log, the IDs are reset such that up the next download, the newly
        /// downloaded gas operatons can have IDs that are the same as those on a previous download.
        /// </remarks>
        public int ManualOperationId 
        {
            get { return _manualOperationId; }
            set { _manualOperationId = value; }
        }

        /// <summary>
        /// Gets or Sets O2 senor High Bump Test Reading
        /// </summary>
        public double O2HighReading { get; set; }

        /// <summary>
        /// Gets or sets whether the O2 high bump passed. This is used to identify
        /// if calibration is required to recover the sensor. This is only set to true
        /// if O2 passes the initial high bump test or the following O2 recovery purge
        /// </summary>
        /// <remarks>INS-7625 SSAM v7.6</remarks>
        public bool IsO2HighBumpPassed
        {
            get { return _isO2HighBumpPassed; }
            set { _isO2HighBumpPassed = value; }
        }

        /// <summary>
        /// Gets or sets whether O2 sensor can undergo a second high bump test. This is only set to true
        /// if O2 sensor passes the calibration done after the initial high bump failure
        /// </summary>
        /// <remarks>INS-7625 SSAM v7.6</remarks>
        public bool IsSecondO2HighBump
        {
            get { return _isSecondO2HighBump; }
            set { _isSecondO2HighBump = value; }
        }

		#endregion
		
        #region Methods

        /// <summary>
        /// Returns whether or not this SensorGasResponse is for a virtual sensor or not.
        /// <para>(Returns true if the UID starts with "VIRTUAL".)</para>
        /// </summary>
        /// <returns></returns>
        public bool IsVirtual
        {
            // We can just use the Component class's call.
            get { return Component.IsVirtualUid( Uid ); }
        }

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return Status.ToString();
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a SensorGasResponse object.
		/// </summary>
		/// <returns>SensorGasResponse object</returns>
		public virtual object Clone()
		{
            SensorGasResponse sensorGasResponse = (SensorGasResponse)this.MemberwiseClone();

			sensorGasResponse.GasConcentration = GasConcentration;

            sensorGasResponse.UsedGasEndPoints = new List<UsedGasEndPoint>( this.UsedGasEndPoints.Capacity );
            foreach ( GasEndPoint used in UsedGasEndPoints )
                sensorGasResponse.UsedGasEndPoints.Add( (UsedGasEndPoint)used.Clone() );

			return sensorGasResponse;
		}


		/// <summary>
		/// This method finds and returns the Cylinder SN that should be
		/// shown on a calibration or bump report.  If the cylinder is
		/// iGas, the report should show factory ID.  Otherwise it should
		/// show the ManualSN.
		/// </summary>
		/// <returns></returns>
		public string GetCylinderSNForReport()
		{
			// First determine what kind of cylinder usage
			// we will look for 
			CylinderUsage targetUsage;

			if ( this._type == GasResponseType.Bump )
			{
				targetUsage = CylinderUsage.Bump;	
			}
			else if ( this._type == GasResponseType.Calibrate )
			{
				targetUsage = CylinderUsage.Calibration;
			}
			else
			{
				// Don't know what type of gas operation this is
				return String.Empty;
			}

			// Now loop through the CylindersUsed for this
			// operation, and return either the factoryID or
			// the manual Sn, depending on which we have.
			// Start at the end and go backwards since contents
			// of arraylist is sorted oldest to most recent.
			for ( int i = UsedGasEndPoints.Count - 1; i >= 0; i-- )
			{
				UsedGasEndPoint used = (UsedGasEndPoint)UsedGasEndPoints[i];
				if ( used.Usage == targetUsage )
				{
                    // Commented out because with Viper, ALL cylinders are iGas cylinders;
                    // there is no concept of "manual" cylinders.
					//if ( used.Cylinder.FactoryId == string.Empty )
                    //{
					//    return used.Cylinder.ManualSerialNumber;
                    //}
                    //else
					{
						return used.Cylinder.FactoryId;
					}
				}
			}

			return string.Empty;

		}

		/// <summary>
		/// Returns the expiration date of the cylinder used for the
		/// gas operation, or datetime.min if not found.
		/// </summary>
		/// <returns></returns>
		public DateTime GetCylinderExpForReport()
		{
			// First determine what kind of cylinder usage
			// we will look for 
			CylinderUsage targetUsage;

			if ( this._type == GasResponseType.Bump )
			{
				targetUsage = CylinderUsage.Bump;	
			}
			else if ( this._type == GasResponseType.Calibrate )
			{
				targetUsage = CylinderUsage.Calibration;
			}
			else
			{
				// Don't know what type of gas operation this is
				return DateTime.MinValue;
			}

			// Now loop through the CylindersUsed for this
			// operation, and return either the factoryID or
			// the manual Sn, depending on which we have.
			
			// Start at the end and go backwards since contents
			// of arraylist is sorted oldest to most recent.
			for ( int i = UsedGasEndPoints.Count - 1; i >= 0; i-- )
			{
				UsedGasEndPoint used = (UsedGasEndPoint)UsedGasEndPoints[i];

				if ( used.Usage == targetUsage )
				{
					return used.Cylinder.ExpirationDate;
				}
			}

			return DateTime.MinValue;

		}

		/// <summary>
		/// If the LastCalibrationTime on the sensor did not change when calibration completed, then it should
		/// be assumed that the instrument aborted the calibration and the state of the previous calibration on
		/// the sensor should have remained intact. Instruments will only update the last calibration time on a 
		/// sensor when the calibration passed for that sensor.  Therefore, do not use this method when it is  
		/// already known that the calibration has failed for a sensor.
		/// </summary>
		/// <returns>True - Status should be set to InstrumentAborted.  False - Status is still Passed.</returns>
		public bool WasCalibrationInstrumentAborted()
		{
			return this.PreCal_LastCalibrationTime == this.PostCal_LastCalibrationTime;
		}

		/// <summary>
		/// This method should be called to get a new SensorGasResponse object after WasCalibrationInstrumentAborted() returned true.  This creates a new instance
		/// object to replace the passed in object while retaining a few properties of the original object.  
		/// </summary>
		/// <param name="sgr">The SensorGasResponse object that will be replaced by a new SensorGasResponse object so that very few values
		/// will be uploaded to iNet since the calibration of this sensor was aborted by the instrument.</param>
		/// <returns>A new SensorGasResponse object that has an InstrumentAborted status and preserves a small subset of the passed in object's properties.</returns>
		public static SensorGasResponse CreateInstrumentAbortedSensorGasResponse( SensorGasResponse sgr )
		{
			// A default reading of double.MinValue is assumed for the new SensorGasResponse.
			SensorGasResponse response = new SensorGasResponse( sgr.Uid, DateTime.Now ); // should this be UtcNow? - JMP,3/2016.
						
			response.Status = Status.InstrumentAborted;
			response.Type = sgr.Type;
			response.Position = sgr.Position;
			response.AccessoryPump = sgr.AccessoryPump;
			response.GasConcentration = (GasConcentration)sgr.GasConcentration.Clone(); 
			// It would be proper to clone the CylindersUsed, but the original SensorGasResponse is going away
			// so it should not matter.
			response.UsedGasEndPoints = sgr.UsedGasEndPoints; // Clone?

			response.PreCal_LastCalibrationTime = sgr.PreCal_LastCalibrationTime;
			response.PostCal_LastCalibrationTime = sgr.PostCal_LastCalibrationTime;

			return response;
		}

        /// <summary>
        /// Returns true if the status indicates a failed zeroing or calibration.
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        public static bool IsFailedCalibrationStatus( Status status )
        {
            return status == Status.Failed || status == Status.ZeroFailed || status == Status.SpanFailedZeroFailed || status == Status.SpanPassedZeroFailed || status == Status.SpanFailed || status == Status.InstrumentAborted;
        }

		#endregion

	}


    /// <summary>
	/// The enumerated type that indicates what operation generated the gas response.
	/// </summary>
	public enum GasResponseType
	{
		Bump ,
		Calibrate
	}

	/// <summary>
	/// The enumerated type that indicates what the calibration status was, as reported
	/// by the instrument, at the end of the calibration.
	/// </summary>
	public enum Status
	{
        Unknown = DomainModelConstant.NullInt,
		InProgress = 0 ,
		Passed = 1 ,
		Failed = 2 ,
		SpanInProgressZeroFailed = 4 ,
		SpanPassedZeroFailed = 5 ,
		InProgressSpanFailed = 8 ,
		SpanFailed = 9 ,
		InProgressSpanFailedZeroFailed = 12 ,
		SpanFailedZeroFailed = 13 ,
        ZeroFailed = 20 ,  // Note that ZeroFailed is a value that's NEVER returned by any of the legacy serial instruments.
        ZeroPassed = 21 , // SGF  04-Nov-2010  Single Sensor Cal and Bump
        PassedManual = -1,
        FailedManual = -2,
        ZeroFailedManual = -3, // JMP  10-May-2010  DSW-220
        SkippedManual = -4, // JMP  10-May-2010  DSW-220
        O2RecoveryFailed = 29,  // SGF  13-May-2011  INS-1992
        BumpPassedFreshAirFailed = 30 ,  //  SGF  15-Jun-2010  DSW-470
        BumpFailedFreshAirPassed = 31 ,  //  SGF  15-Jun-2010  DSW-470
        BumpFailedFreshAirFailed = 32 ,  //  SGF  15-Jun-2010  DSW-470
        Skipped = 33, //Suresh 19-APR-2012 INS-4537 (DEV)
		InstrumentAborted = 34 // JFC 7-Mar-2014 INS-4316
    }

}
