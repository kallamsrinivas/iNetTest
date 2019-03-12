using System;
using System.Collections.Generic;


namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// This class is used by the CalculateExposure method on the DatalogSensorSession class.
	/// </summary>
	public class DatalogExposure
	{
		private float _twa;
		private float _stel;

		public float TwaReading
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
		public float StelReading
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
	}
	
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to define a raw sensor reading.
    /// RawSensorReading is intended to only be used by the docking station firmware.
    /// </summary>
    public class DatalogReading : ICloneable
    {
		
        #region Fields

        private int _count = 1;
        private short _temperature = short.MinValue;
        private float _rawReading = float.MinValue;
		private List<DatalogExposure> _exposure;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of SensorReading class.
        /// </summary>
        public DatalogReading() {}

        /// <summary>
        /// Creates a new instance of SensorReading class.
        /// </summary>
        public DatalogReading( float rawReading, short temperature )
        {
            Reading = rawReading;
            Temperature = temperature;
        }

        /// <summary>
        /// Creates a new instance of SensorReading class.
        /// </summary>
        public DatalogReading(float rawReading, short temperature, int count)
            : this( rawReading, temperature )
        {
            Count = count;
        }

        #endregion

        #region Properties
 
        /// <summary>
        /// Gets or sets the count.
        /// </summary>
        public int Count
        {
            get
            {
                return _count;
            }
            set
            {
                _count = value;
            }
        }

        /// <summary>
        /// Gets or sets the raw sensor reading.
        /// </summary>
        public float Reading
        {
            get
            {
                return _rawReading;
            }
            set
            {
                _rawReading = value;
            }
        }

        /// <summary>
        /// Gets or sets the temperature reading.
        /// </summary>
        public short Temperature
        {
            get
            {
                return _temperature;
            }
            set
            {
                _temperature = value;
            }
        }

		/// <summary>
		/// Gets or sets the list of exposure (TWA/STEL) readings.  This list should be calculated
		/// by calling the CalculateExposure method on the parent DatalogSensorSession object.
		/// Once initialized, the count of items in this list should match the count of the compressed
		/// DatalogReading instance or 0 meaning that all the exposure readings were also 0.
		/// </summary>
		public List<DatalogExposure> Exposure
		{
			get
			{
				if ( _exposure == null )
				{
					_exposure = new List<DatalogExposure>();
				}

				return _exposure;
			}
			set
			{
				_exposure = value;
			}
		}

        #endregion

        #region Methods

        /// <summary>
        /// This override returns the Reading.
        /// </summary>
        /// <returns>This override returns the Reading.</returns>
        public override string ToString()
        {
            return Reading.ToString();
        }

        /// <summary>
        /// Implementation of ICloneable::Clone - Creates a duplicate of a SensorReading object.
        /// </summary>
        /// <returns>SensorReading object</returns>
        public virtual object Clone()
        {
            return this.MemberwiseClone();
        }
		
        #endregion

    } // end-class DatalogReading

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a sensor reading period.
	/// </summary>
	public class DatalogPeriod : ICloneable
	{
		
		#region Fields

		private int _period = DomainModelConstant.NullInt;
        private DateTime _time = DomainModelConstant.NullDateTime;
		private string _location;
        private List<DatalogReading> _readings = new List<DatalogReading>();

		#endregion

		#region Constructors
		
		/// <summary>
		/// Creates a new instance of SensorReadingPeriod class.
		/// </summary>
		public DatalogPeriod()
		{
		}

		/// <summary>
		/// Creates a new instance of SensorReadingPeriod class when its period is provided.
		/// </summary>
		/// <param name="period">Sensor reading period</param>
		public DatalogPeriod( int period )
		{
			Period = period;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the sensor reading period.
		/// </summary>
		public int Period
		{
			get
			{
				return _period;
			}
			set
			{
				_period = value;
			}
		}

        /// <summary>
        /// 
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
		/// Gets or sets the site location the period occurred at.
		/// </summary>
		public string Location
		{
			get
			{
				if ( _location == null )
					_location = string.Empty;

				return _location;
			}
			set
			{
				_location = value.Trim();
			}
		}

		/// <summary>
		/// Gets or sets the list of SensorReadings obtained in the period.
		/// </summary>
        public List<DatalogReading> Readings
		{
			get
			{
                if ( _readings == null )
                    _readings = new List<DatalogReading>();
                return _readings;
			}
			set
			{
                _readings = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// This override returns the Period number.
		/// </summary>
        /// <returns>This override returns the Period number.</returns>
		public override string ToString()
		{
			return Period.ToString();
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a SensorReadingPeriod object.
		/// </summary>
		/// <returns>SensorReadingPeriod object</returns>
		public virtual object Clone()
		{
            // We do NOT call MemberwiseClone here.  We don't want waste it to waste 
            // time doing  doing a shallow clone of the (possibly) huge Readings
            //  arraylist since we need to do a proper 'deep' clone below.

            DatalogPeriod sensorReadingPeriod = new DatalogPeriod( this.Period );
            sensorReadingPeriod.Location = this.Location;
            sensorReadingPeriod.Time = this.Time;

			// Loop through the contained objects calling clone for each one to fill the new lists.
            // We do a 'for' instead of a 'foreach' for better performance (the arraylist may
            // be quite large)
            sensorReadingPeriod.Readings = new List<DatalogReading>( this.Readings.Count );
            for ( int i = 0; i < this.Readings.Count; i++ )
            {
                DatalogReading reading = this.Readings[i];
                sensorReadingPeriod.Readings.Add( reading );
            }
			
			return sensorReadingPeriod;
		}

		#endregion

	} // end-class DatalogPeriod
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define an instrument session.
	/// </summary>
	public class DatalogSession : ICloneable
	{
		
		#region Fields
		
		private string _serialNumber;
		private List<DatalogSensorSession> _sensorSessions;
		private int _twaTimeBase;
		private string _user;
		private DateTime _sessionDate;
        private long _sessionNum;
		private int _recordingInterval;
		private string _comments;
		private string _baseUnitSerialNumber;

        /// <summary>
        /// Will return non-null if the session is incomplete due to corrupted data retrieved from the instrument.
        /// Will contain exception thrown when corruption was detected.
        /// The session will contain all data retrieved from the instrument up to the point of corruption.Will
        /// be null if no corruption was detected.
        /// </summary>
        public Exception CorruptionException { get; set; }

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of InstrumentSession class.
		/// </summary>
		public DatalogSession()
		{
			Intialize();
		}

		/// <summary>
		/// Creates a new instance of InstrumentSession class when serial number and session is provided.
		/// </summary>
		/// <param name="serialNumber">instrument's serial number</param>
		/// <param name="session">Download session</param>
		public DatalogSession( string serialNumber , DateTime session )
		{
			Intialize();
			SerialNumber = serialNumber;
			Session = session;
		}

		#endregion

		#region Properties
		
		/// <summary>
		/// Gets or sets the instrument's serial number.
		/// </summary>
		public string SerialNumber
		{
			get
			{
				if ( _serialNumber == null )
				{
					_serialNumber = string.Empty;
				}

				return _serialNumber;
			}
			set
			{
				if ( value == null )
				{
					_serialNumber = null;
				}
				else
				{
					_serialNumber = value.Trim().ToUpper();
				}
			}
		}

		/// <summary>
		/// Comments the user can enter on the datalog session.
		/// </summary>
		public string Comments
		{
			get
			{
				return _comments == null ? string.Empty : _comments;
			}
			set
			{
				_comments = value;
			}

		}

		/// <summary>
		/// The S/N of the base unit the instrument (module) was in when the datalog session was recorded.
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
		/// Gets or sets the list of SensorSessions contatined in the instrument session.
		/// </summary>
        public List<DatalogSensorSession> SensorSessions
		{
			get
			{
				if ( _sensorSessions == null )
                    _sensorSessions = new List<DatalogSensorSession>();

				return _sensorSessions;
			}
			set
			{
				_sensorSessions = value;
			}
		}

		/// <summary>
		/// Gets or sets the instrument TWA (time weighted average) time base for the instrument session.
		/// </summary>
		public int TWATimeBase
		{
			get
			{
				return _twaTimeBase;
			}
			set
			{
				_twaTimeBase = value;
			}
		}

		/// <summary>
		/// Gets or sets the instrument session user.
		/// </summary>
		public string User
		{
			get
			{
				if ( _user == null )
				{
					_user = string.Empty;
				}

				return _user;
			}
			set
			{
				if ( value == null )
				{
					_user = null;
				}
				else
				{
					_user = value.Trim();
				}
			}
		}

		/// <summary>
		/// Gets or sets the session timestamp (date).
		/// </summary>
		public DateTime Session
		{
			get
			{
				return _sessionDate;
			}
			set
			{
				_sessionDate = value;				
			}
		}

        /// <summary>
        /// Gets or sets the session number.
        /// Session numbers are assigned to sessions by instruments.
        /// They should not be considered unique in any way.
        /// </summary>
        public long SessionNumber
        {
            get
            {
                return _sessionNum;
            }
            set
            {
                _sessionNum = value;
            }
        }

		/// <summary>
		/// Gets or sets the instrument datalog session recording interval.
		/// </summary>
		public int RecordingInterval
		{
			get
			{
				return _recordingInterval;
			}
			set
			{
				_recordingInterval = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// This method initializes local variables and is called by the constructors of the class.
		/// </summary>
		private void Intialize()
		{
            TWATimeBase = DomainModelConstant.NullInt;
            RecordingInterval = DomainModelConstant.NullInt;
		}

		/// <summary>
		/// This override returns the instrument's serial number.
		/// </summary>
        /// <returns>This override returns the instrument's serial number.</returns>
		public override string ToString()
		{
			return SerialNumber;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of an InstrumentSession object.
		/// </summary>
		/// <returns>InstrumentSession object</returns>
		public virtual object Clone()
		{
			DatalogSession instrumentSession = (DatalogSession)this.MemberwiseClone();

            instrumentSession.SensorSessions = new List<DatalogSensorSession>( this.SensorSessions.Count );
			foreach ( DatalogSensorSession session in SensorSessions )
				instrumentSession.SensorSessions.Add( (DatalogSensorSession)session.Clone() );

			return instrumentSession;
		}

		#endregion

    } // end-class DatalogSession

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a sensor session.
	/// </summary>
	public class DatalogSensorSession : ICloneable
	{
        private GasType _gas;
        private double _alarmLow = DomainModelConstant.NullDouble;
        private double _alarmHigh = DomainModelConstant.NullDouble;
        private double _alarmTWA = DomainModelConstant.NullDouble;
        private double _alarmSTEL = DomainModelConstant.NullDouble;
        private double _exposureSD = DomainModelConstant.NullDouble;
        private List<DatalogPeriod> _readingPeriods;
		private string _serialNumber;
		private ComponentType _type;
        private ResponseFactor _responseFactor;
        private SensorStatuses _status = SensorStatuses.OK;

        /// <summary>
        /// Creates a new instance of SensorSession class.
        /// Serial number and ComponentType must be specified.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="componentType"></param>
		public DatalogSensorSession( string serialNumber, ComponentType componentType )
		{
            _serialNumber = serialNumber;
            _type = componentType;
        }
 
		/// <summary>
		/// Gets or sets the component UID.  The UID is the serial number and sensor
        /// code separated by a "#".  e.g. "1234567890#S0020".
		/// </summary>
        public virtual string Uid { get { return SerialNumber + '#' + Type; } }

        /// <summary>
        /// Returns whether or not this sensor session os for a virtual sensor or not.
        /// <para>(Returns true if the UID starts with "VIRTUAL".)</para>
        /// </summary>
        /// <returns></returns>
        public bool IsVirtual
        {
            // We can just use the Component class's call.
            get { return Component.IsVirtualUid( Uid ); }
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
		/// Gets the component type. This property is read-only.
		/// </summary>
		public ComponentType Type { get { return _type; } }

        /// <summary>
        /// Gets or sets the sensor gas type.
        /// </summary>
        public GasType Gas
        {
            get
            {
                return _gas;
            }
            set
            {
                _gas = value;
            }
        }

        /// <summary>
        /// Gets or sets the alram low.
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
        /// Gets or sets the alram high.
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
        /// Gets or sets the alram time weighted average.
        /// </summary>
        public double AlarmTWA
        {
            get
            {
                return _alarmTWA;
            }
            set
            {
                _alarmTWA = value;
            }
        }

        /// <summary>
        /// Gets or sets the sensor alarm short term exposure limit.
        /// </summary>
        public double AlarmSTEL
        {
            get
            {
                return _alarmSTEL;
            }
            set
            {
                _alarmSTEL = value;
            }
        }

        /// <summary>
        /// Gets or sets the standard deviation of exposure per session.
        /// </summary>
        public double ExposureSD
        {
            get
            {
                return _exposureSD;
            }
            set
            {
                _exposureSD = value;
            }
        }

		/// <summary>
		/// Gets or sets the response factor per sensor.
		/// </summary>
		public ResponseFactor ResponseFactor
		{
			get
			{
                if ( _responseFactor == null )
                    _responseFactor = new ResponseFactor();
				return _responseFactor;
			}
			set
			{
				_responseFactor = value;
			}
		}
		
		/// <summary>
		/// Gets or sets the list of SensorReadingPeriods contained in the sensor session.
		/// </summary>
        public List<DatalogPeriod> ReadingPeriods
		{
			get
			{
				if ( _readingPeriods == null )
                    _readingPeriods = new List<DatalogPeriod>();

				return _readingPeriods;
			}
			set
			{
				_readingPeriods = value;
			}
		}

        public SensorStatuses Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
            }
        }

		/// <summary>
		/// This override returns the sensor's serial number.
		/// </summary>
        /// <returns>Returns the sensor's serial number.</returns>
		public override string ToString()
		{
			return Uid;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a SensorSession object.
		/// </summary>
		/// <returns>SensorSession object</returns>
		public virtual object Clone()
		{
			DatalogSensorSession sensorSession = (DatalogSensorSession)this.MemberwiseClone();

			sensorSession.Gas = Gas;
            sensorSession.ResponseFactor = (ResponseFactor)ResponseFactor.Clone();
            // Can't clone Type.  Read only value. Just use assignment from memberwiseclone.
            //sensorSession.Type = (ComponentType)this.Type.Clone();  

			// Loop through the contained objects calling clone for each one to fill the new lists.
            sensorSession.ReadingPeriods = new List<DatalogPeriod>( this.ReadingPeriods.Count );
			foreach ( DatalogPeriod period in ReadingPeriods )
				sensorSession.ReadingPeriods.Add( (DatalogPeriod)period.Clone() );

			return sensorSession;
		}

		/// <summary>
		/// Calculates TWA and STEL readings for the current sensor session instance.
		/// </summary>
		/// <remarks>
		/// <para>
		/// TWA stands for Time Weighted Average. This in reference to dosages of
		/// toxic gas you may encounter in the work place. It is based on an 8
		/// hour day / 40 hour work week. TWA is a term established by the
		/// American Conference of Governmental Industrial Hygienists (ACGIH).
		/// </para>
		/// <para>
		/// STEL stands for Short Term Exposure Limit. This is the average amount
		/// of gas you can be exposed to in a 15 minute period with no long term
		/// health effects. This may occur 4 times a day.
		/// STEL is a term established by the ACGIH.
		/// </para>
		/// <para>
		/// The algorithm used in this method has been ported from iNet which was 
		/// ported from DS2.
		/// </para>
		/// <para>
		/// After calling this method, STEL and TWA readings for each DatalogReading
		/// within the period can be accessed through the DatalogReading's Exposure
		/// property.  Exposure will return a list of DatalogExposure instances
		/// representing the STEL & TWA readings computed for the compressed reading.
		/// If the Exposure list is not empty, its count should always equal the 
		/// DatalogReading's Count property.
		/// </para>
		/// <para>
		/// If the sensor does not support STEL/TWA, then the Exposure list will be 
		/// empty (which is the case with O2 and combustibles).
		/// </para>
		/// <para>
		/// The following notes regarding how the computations are performed
		/// are excerpted from the ISC Engineering Design Standards document
		/// (1710-2609, Sheet 2)
		/// </para>
		/// <para>-----------------------------------------------------------------------</para>
		/// <para>
		/// STEL values are calculated according to the following formula:
		/// </para>
		/// <para>
		/// <code>
		/// STEL = C1 + C2 + C3 ... C15 / 15 min
		/// </code>
		/// </para>
		/// <para>
		/// Where <code>C = concentration of gas during 1 minute</code>
		/// </para>
		/// <para>
		/// The formula shall be programmed according to following algorithm:
		/// </para>
		/// <para>
		/// <code>
		/// R = raw reading taken every 1 s
		/// S = R1 + R2 + ... R60   // Sum the readings for a 1 minute period
		/// A = S / 60              // Take the average for that minute
		/// T = A1 + A2 + ... A15   // Add up the averages for the last 15 minutes
		/// Result = T / 15         // Divide that total by 15
		/// </code>
		/// </para>
		/// <para>
		/// STEL is a rolling average, so the next STEL would be calculated as follows:
		/// </para>
		/// <para>
		/// <code>
		/// T = A2 + A3 ... A16
		/// Result = T / 15
		/// </code>
		/// </para>
		/// <para>-----------------------------------------------------------------------</para>
		/// <para>TWA values shall be calculated according to the following formula:</para>
		/// <para><code>TWA = ( C1 T1 + C2 T2 + ...Cn Tn ) / 8 hours</code></para>
		/// <para>Where... </para>
		/// <para><code>
		///      C = gas concentration
		///      T = amount of time
		///      8 hours = TWA time base
		/// </code></para>>      
		/// <para>TWA formula shall be implemented according to following algorithm:</para>
		/// <para>
		/// <code>
		///  R = raw reading taken every 1s
		///  T = TWA time base set by user, in minutes
		///  S = R1 + R2 + R3 + ... R60 // Sum up readings over a 1 minute period
		///  A = S / 60                 // Divide by 60 to get avg reading for that minute
		///  S2 = A1 + A2 + ... AN      // Add the averages together to get a cumulative sum
		///  Result = S2 / (  T * 60 )  // TWA at any point is S2 (running sum) divided by T (time base)
		/// </code>
		/// </para>
		/// </remarks>
		/// <param name="recordingInterval">the recording interval used for the DatalogSensorSession</param>
		/// <param name="twaTimeBase">the TWA time base used for the DatalogSensorSession</param>
		public void CalculateExposure( int recordingInterval, int twaTimeBase )
		{
			float cumulativeExposure = 0.0F;
			////float cumulativeSquares = 0.0F;
			////int totalReadings = 0;

			// First, determine if we're supposed to calculate STEL/TWA for
			// the sensor / gas type.  If not, do nothing.

			bool eligible;

			// Depending on the instrument type, the sensor type (ComponentCode)
			// will not always be available.  If it is, we let it take precedence
			// over the gas code.
			//if ( ComponentCode.Code != string.Empty )
			//{
			//    eligible = ComponentCode.IsStelTwaEligible();
			//}
			//else
			//{
			//    eligible = GasCode.IsStelTwaEligible();
			//}
			eligible = GasCode.IsStelTwaEligible( Gas.Code );

			if ( !eligible )
			{
				return;
			}

			// Don't go any further if either Recording Interval or TWA Time Base are ever
			// specified as being zero. Otherwise, we'll get a division by zero error.
			if ( recordingInterval <= 0 || twaTimeBase <= 0 )
			{
				return;
			}

			// If there's no defined STEL or TWA alarm, it make no sense to calculate stel/twa.
			if ( AlarmSTEL < 0.0 && AlarmTWA < 0.0 )
			{
				return;
			}

			// Calculate the window length for STEL calculations. 
			// This is the maximum number of readings that will be in the STEL period.    
			int stelWindowLength = (int)Math.Ceiling( ( 60.0F / recordingInterval ) * 15.0F /*minutes*/);
			float twaFactor = ( recordingInterval * 1.0F ) / ( twaTimeBase * 3600.0F );

			// Proceed through each reading within all periods doing various calculations.
			foreach ( DatalogPeriod period in ReadingPeriods )
			{
				// Need to reset STEL for every period. We do not reset TWA, though.
				float stelWindowTotal = 0.0F;
				Queue<float> stelWindow = new Queue<float>( stelWindowLength );

				// Iterate through all readings in this period
				foreach ( DatalogReading reading in period.Readings )
				{
					reading.Exposure = new List<DatalogExposure>( reading.Count );

					// As we calculate STEL/TWA, keep track of if we compute any non-zero readings.
					// If none found (the vast majority of the time), then we'll toss the exposure data
					// away after this loop in order to save memory.
					bool nonZeroFound = false;

					// For each reading, process it multiple times equal to its 'count'
					for ( int readingCount = 0; readingCount < reading.Count; readingCount++ )
					{
						// Get local raw reading to avoid indirection through the object for speed.
						// And note that for purposes of calculation (STEL, TWA, exposure), 
						// we treat any negative reading as zero.
						float rawReading = reading.Reading;
						if ( rawReading < 0.0F )
						{
							rawReading = 0.0F;
						}

						// Sometimes, whenever itx is in overrange, the reported readings are 32K.
						// Per discussion, we will treat these readings as if they're equal to the High alarm.
						//if ( rawReading == 32000.0f )
						//{
						//	rawReading = AlarmHigh;
						//}

						// Keep track of cumulative exposure for multiple uses.
						cumulativeExposure += rawReading;

						// Keep track of the cumulative squares for ExposureSD calculation.
						////cumulativeSquares += rawReading * rawReading;

						// Get local exposure reference to avoid future indirections on the array.
						DatalogExposure exposure = new DatalogExposure();
						reading.Exposure.Add( exposure );

						// Calculate and store the TWA for this reading.
						exposure.TwaReading = cumulativeExposure * twaFactor;

						// Add this reading into the total of the readings for the current window.
						// Note that negative readings are always treated as zero.
						stelWindowTotal += rawReading;

						// As we iterate through out readings, the window shifts.  Every iteration, we need to
						// subtract back out of our window the reading that was at the beginning of the window
						// (the 'window sill') on the LAST iteration.
						if ( stelWindow.Count == stelWindowLength )
						{
							float sillReading = stelWindow.Dequeue(); // Pop reading at head of queue
							if ( sillReading > 0.0F )
							{
								stelWindowTotal -= sillReading;
							}
						}

						stelWindow.Enqueue( rawReading );

						// Divide STEL window total by number of readings in the window to get the STEL value.
						float stel = stelWindowTotal / stelWindowLength;
						exposure.StelReading = stel;

						////totalReadings++; // Keep track of the total number of readings.

						// Note that we treat anything less than .01 as zero.  This is an assumption
						// that exposure data is (and always has been) only important to two decimal places.  
						nonZeroFound = nonZeroFound || !IsZero( exposure.TwaReading ) || !IsZero( exposure.StelReading );
					} // end-for reading's count

					if ( !nonZeroFound )
					{
						reading.Exposure = new List<DatalogExposure>( 0 );
					}
				}
			}
		}

		/// <summary>
		/// Determine if specified value is 'zero'. 
		/// Note that we treat anything less than .01 as zero.  This is an assumption
		/// that exposure data is (and always has been) only important to two decimal places.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private static bool IsZero( float value )
		{
			return Math.Round( value, 2 ) < 0.01D;
		}

    } // end-class DatalogSensorSession
}
