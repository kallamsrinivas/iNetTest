using System;
using System.Collections.Generic;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define an instrument.
	/// </summary>
	public class Instrument : Device
	{
		#region Fields

        /// <summary>
        /// Gets or sets the instrument's datalog recording interval.
        /// </summary>
		public int RecordingInterval { get; set; }

        /// <summary>
        /// Gets or sets the instrument TWATimeBase.
        /// </summary>
        public int TWATimeBase { get; set; }

		/// <summary>
		/// Gets or sets the instrument's Out-of-Motion (a.k.a. man down) warning interval.
		/// </summary>
		public int OomWarningInterval { get; set; }

		/// <summary>
		/// Gets or sets the instrument's dock interval.
		/// </summary>
		public int DockInterval { get; set; }

        /// <summary>
        /// Gets or sets the instrument's maintenance interval (Indicates the number of minutes between the sounding of bump, cal or dock overdue indicators).
        /// </summary>
        public int MaintenanceInterval { get; set; }

        /// <summary>
        /// Gets or sets the instrument's calibration interval.
        /// </summary>
        public short CalibrationInterval { get; set; }

        /// <summary>
        /// Gets or set the instrument's bump interval.
        /// </summary>
        public double BumpInterval { get; set; }

        /// <summary>
        /// Specifies what gas reading (% of concentration) that instrument needs to
        /// see in order for a bump test to pass.
        /// </summary>
        public int BumpThreshold { get; set; }

        /// <summary>
        /// Specifies the max duration of a bump test that instrument can have
        /// in order for a bump test to pass.
        /// </summary>
        public int BumpTimeout { get; set; }

        /// <summary>
        /// Gets or sets the instrument backlight setting.
        /// If not explictly set in the Instrument instance, then
        /// the default is BacklightSetting.Unknown.
        /// </summary>
		public BacklightSetting Backlight { get; set; }

        /// <summary>
        /// Suresh 30-SEPTEMBER-2011 INS-2277
        /// Gets or sets the instrument backlight timeout
        /// </summary>
        public int BacklightTimeout { get; set; }

        /// <summary>
        /// This property tells us that the instrument has had 
        /// components changed since the last time the server
        /// saw it.
        /// </summary>
		public bool ComponentChanged { get; set; }

        /// <summary>
        /// Gets or sets the instrument's "Magnetic Field Duration" setting.
        /// </summary>
        public int MagneticFieldDuration { get; set; }

        /// <summary>
        /// Indicates if instrument has an attached pump or not.
        /// </summary>
        public AccessoryPumpSetting AccessoryPump { get; set; }

		/// <summary>
		/// Gets or sets the time in seconds from when a teammate 
		/// goes to lost and the alarm sounds.
		/// </summary>
		public int WirelessPeerLostThreshold { get; set; }

		/// <summary>
		/// Gets or sets the time in seconds when the network 
		/// disappears and the alarm sounds.
		/// </summary>
		public int WirelessNetworkLostThreshold { get; set; }

        /// <summary>
        /// Gets or sets the timeout after network lost detection is initiated that the instrument disconnects from the network in minutes
        /// </summary>
        public int WirelessNetworkDisconnectDelay { get; set; }

		/// <summary>
		/// Gets or sets the percentage of the low alarm that the sensors
		/// have to be greater than to send the verbose message.
		/// </summary>
		public int WirelessReadingsDeadband { get; set; }

        /// <summary>
        /// Gets or sets the mask of Wireless Alarm
        /// </summary>
        public string WirelessAlarmMask { get; set; }
        
        /// <summary>
        /// Gets or sets whether the wireless feature is activated
        /// </summary>
        public bool WirelessFeatureActivated { get; set; }

        /// <summary>
        /// Gets or sets whether the inet now feature is activated
        /// </summary>
        public bool INetNowFeatureActivated { get; set; }

        /// <summary>
        /// Gets or sets the Lone Worker Ok Message Interval in seconds.
        /// </summary>
        public int LoneWorkerOkMessageInterval { get; set; }

        /// <summary>
        /// Gets or sets the Gps Reading Interval in minutes.
        /// </summary>
        public int GpsReadingInterval { get; set; }

        /// <summary>
        /// Gets or sets the instrument's Access Level Setting.
        /// </summary>
        public short? AccessLevel { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth Mac Address
        /// </summary>
        public string BluetoothMacAddress { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth Software Version
        /// </summary>
        public string BluetoothSoftwareVersion { get; set; }

        /// <summary>
        /// Gets or sets the Bluetooth Feature Activation Status
        /// </summary>
        public bool BluetoothFeatureActivated { get; set; }

        public WirelessModule WirelessModule { get; set; }

        private string _jobNumber;
		private string _countryOfOrigin;
        private string _accessCode;
        private string _status;

        private List<string> _users;
        private List<string> _sites;
		private string _activeSite;
		private string _activeUser;

		private string _companyName;
		private string _companyMessage;

        private List<string> _favoritePidFactors;
        private List<ResponseFactor> _customPidFactors;
		private List<AlarmActionMessages> _alarmActionMessages;

        private Dictionary<string, Sensor> _sensorSettings; // keyed on sensor code.

        private List<InstalledComponent> _installedComponents;

		private List<BaseUnit> _baseUnits;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of instrument class.
		/// </summary>
		public Instrument()
		{
			Initialize();
		}

		/// <summary>
		/// Creates a new instance of instrument class when its serial number is provided.
		/// </summary>
		/// <param name="serialNumber">instrument's serial number</param>
		public Instrument( string serialNumber ) : base ( serialNumber )
		{
			Initialize();
		}

		/// <summary>
		/// This method initializes local variables and is called by the constructors of the class.
		/// </summary>
		private void Initialize()
		{
            this.JobNumber = this.AccessCode = this.Status = string.Empty;
            this.RecordingInterval = DomainModelConstant.NullInt;
			this.TWATimeBase = DomainModelConstant.NullInt;
			this.Backlight = BacklightSetting.Unknown;
			this.ActiveSite = string.Empty;
			this.ActiveUser = string.Empty;
            this.Users = new List<string>();
            this.Sites = new List<string>();
			this.CompanyName = string.Empty;
			this.CompanyMessage = string.Empty;
            this.FavoritePidFactors = new List<string>();
            this.CustomPidFactors = new List<ResponseFactor>();
			this.AlarmActionMessages = new List<AlarmActionMessages>();
            this.InstalledComponents = new List<InstalledComponent>();
            this.SensorSettings = new Dictionary<string, Sensor>();
            this.CalibrationInterval = DomainModelConstant.NullShort;
            this.BumpInterval = DomainModelConstant.NullDouble;
			this.BumpThreshold = SensorGasResponse.DEFAULT_BUMP_THRESHOLD;
            this.BumpTimeout = SensorGasResponse.DEFAULT_BUMP_TIMEOUT;
            this.AccessoryPump = AccessoryPumpSetting.NotApplicable;

            //Suresh 05-JAN-2012 INS-2564
            SensorsInErrorMode = new List<InstalledComponent>();
            InstrumentInCriticalError = false; //Suresh 06-FEB-2012 INS-2622
            InstrumentCriticalErrorCode = string.Empty; // INS-8446 RHP v7.6
		}

		#endregion

		#region Properties

        public string Status
        {
            get { return _status; }
            set { _status = ( value == null ) ? string.Empty : value; }
        }

        /// <summary>
        /// The settings to be programmed in to sensors during an instrument settings update event.
        /// Keyed on sensor code.
        /// </summary>
        /// <remarks>keyed on sensor code.</remarks>
        public Dictionary<string, Sensor> SensorSettings
        {
            get { return _sensorSettings; }
            set { _sensorSettings = ( value == null ) ? new Dictionary<string,Sensor>() : value; }
        }

        /// <summary>
        /// Gets or sets the device job number.
        /// </summary>
        public string JobNumber
        {
            get { return _jobNumber; }
            set { _jobNumber = ( value == null ) ? string.Empty : value; }
        }

        /// <summary>
        /// Gets or sets the instrument's country of origin code.
        /// Will be empty for instrument types that have no country of origin.
        /// </summary>
        public string CountryOfOrigin
        {
            get
            {
                if ( _countryOfOrigin == null )
                    _countryOfOrigin = string.Empty;
                return _countryOfOrigin;
            }
            set { _countryOfOrigin = value; }
        }

		/// <summary>
		/// Gets or sets the instrument access code.
		/// An empty string indicates that the AccessCode needs to be converted 
		/// to the default access code for the current instrument type definition.
		/// </summary>
		public string AccessCode
		{
            get { return _accessCode; }
            set { _accessCode = ( value == null ) ? string.Empty : value; }
		}

		/// <summary>
		/// Gets or sets the list of users of the instrument.
		/// </summary>
        public List<string> Users
		{
			get { return _users; }
			set { _users = ( value == null ) ? new List<string>() : value; }
		}

		/// <summary>
		/// Gets or sets the list of sites of the instrument.
		/// </summary>
        public List<string> Sites
		{
            get { return _sites; }
            set { _sites = ( value == null ) ? new List<string>() : value; }
		}

		/// <summary>
		/// Specifies the name of this profile's active User.
		/// </summary>
		public string ActiveUser
		{
			get { return _activeUser; }
			set { _activeUser = ( value == null ) ? string.Empty : value; }
		}

		/// <summary>
		/// Specifies the name of this profile's active Site.
		/// </summary>
		public string ActiveSite
		{
			get { return _activeSite; }
			set { _activeSite = ( value == null ) ? string.Empty : value; }
		}

		/// <summary>
		/// Gets or sets the name of the active company.
		/// </summary>
		public string CompanyName
		{
			get { return _companyName; }
			set { _companyName = ( value == null ) ? string.Empty : value; }
		}

		// Gets or sets the company message that is shown at startup.
		public string CompanyMessage
		{
			get { return _companyMessage; }
			set { _companyMessage = ( value == null ) ? string.Empty : value; }
		}

        /// <summary>
        /// Gets or sets the list of installed components on the device.
        /// </summary>
        public List<InstalledComponent> InstalledComponents
        {
            get { return _installedComponents; }
            set { _installedComponents = ( value == null ) ? new List<InstalledComponent>() : value; }
        }

		/// <summary>
		/// List of strings naming favorite PID Response Factors.
		/// </summary>
		public List<string> FavoritePidFactors
		{
            get { return _favoritePidFactors; }
            set { _favoritePidFactors = ( value == null ) ? new List<string>() : value; }
		}

		/// <summary>
		/// List of ResponseFactors representing the
		/// 'custom response factors' for this instrument.
		/// </summary>
        public List<ResponseFactor> CustomPidFactors
		{
            get { return _customPidFactors; }
            set { _customPidFactors = ( value == null ) ? new List<ResponseFactor>() : value; }
		}

		/// <summary>
		/// Gets or sets the list of alarm action messages for this instrument.
		/// </summary>
		public List<AlarmActionMessages> AlarmActionMessages
		{
			get { return _alarmActionMessages; }
			set { _alarmActionMessages = ( value == null ) ? new List<AlarmActionMessages>() : value; }
		}

		/// <summary>
		/// Gets or sets a list of base units that the instrument module
		/// was connected to since the last time it was docked on a
		/// docking station.  Implemented for SafeCore Modules.
		/// </summary>
		public List<BaseUnit> BaseUnits
		{
			get
			{
				if ( _baseUnits == null )
					_baseUnits = new List<BaseUnit>();

				return _baseUnits;
			}
			set 
			{
				_baseUnits = value;
			}
		}

        /// <summary>
        /// List of sensors installed sensors that are reporting a SensorMode of 'Error'.
        /// i.e. sensors in 'DataFault' or 'Uninitialized' condition.
        /// </summary>
        /// <remarks>
        /// Also note: this value is NOT to be stored in a database or uploaded to iNet.  This value is kept in memory
        /// for processing only while the instrument is docked.
        /// <para>
        /// Suresh 05-JAN-2012 INS-2564...
        /// This property is changed from Boolean to List of InstalledComponents to fix the ticket INS-2564. 
        /// If the instrument has sensor with error state then docking station need to update sensor error to iNet with 
        /// position of the sensor's in error. 
        /// So, just keeping boolean property is not enough for reporting the sensor error with position to inet, so changed
        /// the property to List of sensor in error.
        /// </para>
        /// </remarks>
        public List<InstalledComponent> SensorsInErrorMode { get; set; }

        /// <summary>
        /// //Suresh 06-FEB-2012 INS-2622
        /// Gets or Sets whether instrument is critical error.
        /// Note: Instrument is set to be in critical error if any error code that are logged in the instrument 
        /// matches with critical errors downloaded from iNet
        /// </summary>
        public bool InstrumentInCriticalError { get; set; }

        /// <summary>
        /// Gets or Sets the instrument's critical error code if any.
        /// Note: Instrument is set to be in critical error if any error code that are logged in the instrument 
        /// matches with critical errors downloaded from iNet
        /// </summary>
        /// <remarks>INS-8446 RHP v7.6</remarks>
        public string InstrumentCriticalErrorCode { get; set; }

		#endregion Properties

		#region Methods

        /// <summary>
        /// Returns this instrument's serial number plus its installed components' serial numbers
        /// </summary>
        /// <returns></returns>
        public List<string> GetSerialNumbers()
        {
            List<string> snList = new List<string>();

            snList.Add( this.SerialNumber );

            foreach ( InstalledComponent installedComponent in InstalledComponents )
            {
                if ( installedComponent.Component.Uid != string.Empty )
                    snList.Add( installedComponent.Component.Uid );
            }

            return snList;
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
        /// This parameter actually needs to be am Instrument.
        /// It's defined as a Device so that we can override the base class.
        /// </param>
        protected override void DeepCopyTo( Device device )
        {
            Log.Assert( device is Instrument, "referenced passed to Instrument.DeepCopyTo must be of type Instrument" );

            // first, deep clone the base class

            base.DeepCopyTo( device );

            // now, deep-clone this subclass...

            Instrument instrument = (Instrument)device;

            // Note that we first recreate each of the array list since at this moment, both the
            // source instrument and cloned instrument are both referencing the exact
            // same ArrayLists (because of the MemberwiseClone call)

            instrument.Users = new List<string>( this.Users.Count );
            foreach ( string user in Users )
                instrument.Users.Add( user );

            instrument.Sites = new List<string>( this.Sites.Count );
            foreach ( string site in Sites )
                instrument.Sites.Add( site );

            // Loop through the contained objects calling clone for each one to fill the new lists.
            instrument.CustomPidFactors = new List<ResponseFactor>( this.CustomPidFactors.Count );
            foreach ( ResponseFactor rf in CustomPidFactors )
                instrument.CustomPidFactors.Add( (ResponseFactor)rf.Clone() );

            // We can do shallow copy here because arraylist just contains strings.
            instrument.FavoritePidFactors = new List<string>( this.FavoritePidFactors.Count );
            foreach ( string favorite in FavoritePidFactors )
                instrument.FavoritePidFactors.Add( favorite );

			instrument.AlarmActionMessages = new List<AlarmActionMessages>( this.AlarmActionMessages.Count );
			foreach ( AlarmActionMessages aam in AlarmActionMessages )
				instrument.AlarmActionMessages.Add( (AlarmActionMessages)aam.Clone() );

            instrument.InstalledComponents = new List<InstalledComponent>();
            foreach ( InstalledComponent installedComponent in this.InstalledComponents )
                instrument.InstalledComponents.Add( (InstalledComponent)installedComponent.Clone() );

            if ( this.WirelessModule != null )
                instrument.WirelessModule = (WirelessModule)this.WirelessModule.Clone();

			instrument.BaseUnits = new List<BaseUnit>();
			foreach ( BaseUnit baseUnit in this.BaseUnits )
				instrument.BaseUnits.Add( (BaseUnit)baseUnit.Clone() );
 
        }

        // TODO - THERE IS A HasDualSenseFeature PROPERTY IN THE TypeDefinitions.
        // EITHER THIS METHOD SHOULD JUST PASS THROUGH TO THAT, OR THIS METHOD SHOULD BE
        // REMOVED AND THE CALLER, INSTEAD, JUST DIRECTLY USES A TypeDefinition. - JMP,1/2016.
		/// <summary>
		/// Returns true if the docked instrument supports DualSense.
		/// </summary>
		public bool SupportsDualSense()
		{
			bool isSupported = false;

			if (this.Type == DeviceType.TX1 || this.Type == DeviceType.VPRO || this.Type == DeviceType.SC )
				isSupported = true;

			return isSupported;
		}

        /// <summary>
		/// Implementation of ICloneable::Clone.  Overrides Device.Clone().
		/// </summary>
		/// <returns>Cloned Instrument</returns>
		public override object Clone()
		{
			Instrument instrument = (Instrument)this.MemberwiseClone();

            // now deep-clone what needs to be deep cloned...

            this.DeepCopyTo( instrument );

			return instrument;
		}

		/// <summary>
		/// Find the installed component with the specified type code, or returns null if no match is found.
		/// </summary>
		/// <param name="componentTypeCode"></param>
        /// <returns></returns>
		public InstalledComponent GetInstalledComponent( string componentTypeCode )
		{
            return InstalledComponents.Find( ic => ic.Component.Type.Code == componentTypeCode );
		}

        /// <summary>
        /// Finds the installed component with the specified UID, or returns null if no match is found.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public InstalledComponent GetInstalledComponentByUid(string uid)
        {
            return InstalledComponents.Find( ic => ic.Component.Uid == uid );
        }

		#endregion

		#region DualSense Methods

		/// <summary>
		/// Returns true if "Service Instrument Soon" should be displayed on the docking station's
		/// LCD screen due to the state of the docked instrument.
		/// </summary>
		public bool ShouldServiceSoon( bool singleSensorMode )
		{
			if ( !singleSensorMode )
				return false;

			// CalibrationState.Failed = this should never happen, the DS should go into Calibration Failure before going idle
			CalibrationState calState = GetInstrumentCalibrationState( singleSensorMode, null, null );
			
			// instrument not docked
			if ( calState == CalibrationState.Unknown )
				return false;

			// we already know the instrument needs serviced so don't go farther
			if ( calState == CalibrationState.RedundantSensorPassed || calState == CalibrationState.Failed )
				return true;

			// evaluate O2 bump status for instruments capable of combining O2 sensors for DualSense
			if ( this.Type == DeviceType.VPRO || this.Type == DeviceType.SC )
			{
				foreach ( InstalledComponent ic in this.InstalledComponents )
				{
					if ( !( ic.Component is Sensor ) )
						continue;

					if ( ic.Component.Type.Code != SensorCode.O2 )
						continue;

					// Ventis Pro sensors cannot be disabled, but we check because SafeCore sensors can.
					if ( !ic.Component.Enabled )
						continue;

					Sensor sensor = (Sensor)ic.Component;
									
					// if we found a bump failed O2 sensor than just return true
					if ( sensor.BumpTestStatus == false )
						return true;
				}				
			}

			return false;
		}

		/// <summary>
		/// Returns the instrument-level calibration state based on the state of the installed sensors.
        /// <para>All passed sensors are returned in the passed-in "passedSensors" list.</para>
        /// <para>All failed sensors are returned in the passed-in "failedSensors" list.</para>
		/// </summary>
		/// <param name="passedSensors">
		/// Sensors that passed calibration will be returned in this list.
		/// A null list can be specified to indicate that passed sensors do not need returned.
		/// </param>
		/// <param name="failedSensors">
		/// Sensors that failed calibration will be returned in this list.
		/// A null list can be specified to indicate that failed sensors do not need returned.
		/// </param>
		/// <returns>RedundantSensorPassed will only be returned if SingleSensorMode is enabled and the instrument supports DualSense functionality.</returns>
		public CalibrationState GetInstrumentCalibrationState( bool singleSensorMode, List<InstalledComponent> passedSensors, List<InstalledComponent> failedSensors )
		{
			if ( this.Type == DeviceType.Unknown || this.SerialNumber.Length <= 0 )
				return CalibrationState.Unknown;

			CalibrationState instrumentCalState = CalibrationState.Passed;

			if ( this.Type == DeviceType.TX1 )
			{
				CalibrationState sensor1CalState = CalibrationState.Unknown;
				CalibrationState sensor2CalState = CalibrationState.Unknown;

				foreach ( InstalledComponent ic in this.InstalledComponents )
				{
					if ( !( ic.Component is Sensor ) )
						continue;

					// We do not need to check if the sensor is enabled, because TX1 sensors cannot be disabled.
					Sensor sensor = (Sensor)ic.Component;
					
					bool failedCal = SensorGasResponse.IsFailedCalibrationStatus( sensor.CalibrationStatus );

					if ( ( passedSensors != null ) && !failedCal )
						passedSensors.Add( ic );

					if ( ( failedSensors != null ) && failedCal )
						failedSensors.Add( ic );

					if ( ic.Position == 1 )
						sensor1CalState = failedCal ? CalibrationState.Failed : CalibrationState.Passed;
					if ( ic.Position == 2 )
						sensor2CalState = failedCal ? CalibrationState.Failed : CalibrationState.Passed;
				}

				if ( sensor1CalState == sensor2CalState )
					instrumentCalState = sensor1CalState;
				else
				{
					if ( singleSensorMode == true )
						instrumentCalState = CalibrationState.RedundantSensorPassed;
					else
						instrumentCalState = CalibrationState.Failed;
				}
			}
			else if ( this.Type == DeviceType.VPRO || this.Type == DeviceType.SC )
			{
				// The caller may not care about which sensors are passed or failed so they may pass in null for 
				// both lists which is okay.  However, we need to use the lists to determine the overall calibration 
				// state of the instrument.  Instrument cal state is defaulted to Passed above. 
				if ( passedSensors == null )
					passedSensors = new List<InstalledComponent>();

				if ( failedSensors == null )
					failedSensors = new List<InstalledComponent>();

				// Will use the dictionary like it's a HashSet.
				Dictionary<string,bool> passedDualSenseCapable = new Dictionary<string, bool>();
				foreach ( InstalledComponent ic in this.InstalledComponents )
				{
					if ( !( ic.Component is Sensor ) )
						continue;

					// Ventis Pro sensors cannot be disabled, but we check because SafeCore sensors can.
					if ( !ic.Component.Enabled )
						continue;

					Sensor sensor = (Sensor)ic.Component;
					
					// If the sensor failed its last cal or bump add it to the failedSensors list.
					if ( SensorGasResponse.IsFailedCalibrationStatus( sensor.CalibrationStatus ) )
						failedSensors.Add( ic );
					else
					{
						passedSensors.Add( ic );

						if ( sensor.IsDualSenseCapable )
						{
							// We do not use the Add() method because it will throw an exception if we have duplicate keys. 
							passedDualSenseCapable[GetDualSenseCapableKey( sensor )] = sensor.IsDualSenseCapable;
						}
					}
				}

				if ( failedSensors.Count > 0 )
				{
					if ( singleSensorMode == true )
					{
						// We will changed this to failed below if we do not find a matching DualSense capable sensor 
						// in the passed list for every failed sensor.
						instrumentCalState = CalibrationState.RedundantSensorPassed;

						for ( int i = 0; i < failedSensors.Count; i++ )
						{
							// Only sensors should be in the list.
							Sensor failedSensor = (Sensor)failedSensors[i].Component;

							if ( failedSensor.IsDualSenseCapable && passedDualSenseCapable.ContainsKey( GetDualSenseCapableKey( failedSensor ) ) )
								continue;

							// At least one failed sensor does not have a paired sensor that passed.
							instrumentCalState = CalibrationState.Failed;
							break;
						}
					}
					else
					{
						instrumentCalState = CalibrationState.Failed;
					}
				}
			}
			else // All other instrument types.
			{
				// TODO - shouldn't we be adding sensors to passedSensors and failedSensors lists?  - JMP, 6/7/2013
				foreach ( InstalledComponent ic in this.InstalledComponents )
				{
					if ( !( ic.Component is Sensor ) )
						continue;

					Sensor sensor = (Sensor)ic.Component;
					if ( !sensor.Enabled )
						continue;

					if ( SensorGasResponse.IsFailedCalibrationStatus( sensor.CalibrationStatus ) == true )
					{
						instrumentCalState = CalibrationState.Failed;
						break;
					}
				}
			}

			return instrumentCalState;
		}

		/// <summary>
		/// Returns a cal passed DualSense InstalledComponent sibling of the provided InstalledComponent if Single-Sensor mode is enabled
		/// and one exists.  Otherwise, null is returned.  GetDualSenseSibling is a helper method to be used in possible redundant 
		/// bump passed scenarios.  The calling method should determine if the sibling has a passed bump status.  This method should only 
		/// be called after determining that the instrument is NOT in cal failed state.
		/// </summary>
		public InstalledComponent GetDualSenseSibling( bool singleSensorMode, InstalledComponent bumpFailedComponent )
		{
			if ( !singleSensorMode )
			{
				Log.Debug( "Single-Sensor mode is currently disabled. A DualSense sibling will NOT be returned." );
				return null;
			}

			// validate input parameter, only sensors are expected
			if ( !( bumpFailedComponent.Component is Sensor ) )
				return null;

			Sensor bumpFailedSensor = (Sensor)bumpFailedComponent.Component;

			// an instrument must be docked and discovered
			if ( this.Type == DeviceType.Unknown || this.SerialNumber.Length <= 0 )
				return null;

			if ( this.Type == DeviceType.TX1 )
			{
				// for TX1, a sensor that has a different S/N would be a DualSense sibling
				foreach ( InstalledComponent ic in this.InstalledComponents )
				{
					if ( !( ic.Component is Sensor ) )
						continue;

					// We do not need to check if the sensor is enabled, because TX1 sensors cannot be disabled.
					Sensor sensor = (Sensor)ic.Component;

					if ( sensor.Uid == bumpFailedSensor.Uid )
						continue;

					if ( sensor.Type.Code == bumpFailedSensor.Type.Code && !SensorGasResponse.IsFailedCalibrationStatus( sensor.CalibrationStatus ) )
						return ic;
				}
			}
			else if ( this.Type == DeviceType.VPRO || this.Type == DeviceType.SC )
			{
				// if the failed sensor is not capable of DualSense, then don't search for a sibling;
				// we check the flag here because TX1 does not use it
				if ( !bumpFailedSensor.IsDualSenseCapable )
					return null;

				// For Ventis Pro Series and SafeCore, a sibling will have a different S/N, but be DualSense capable, and have
				// the same sensor part number and sensor code.  For SafeCore, we do not worry about sensor position because 
				// if it were an issue the DS would go into the Sensor Error state.
				foreach ( InstalledComponent ic in this.InstalledComponents )
				{
					if ( !( ic.Component is Sensor ) )
						continue;

					// Ventis Pro sensors cannot be disabled, but we check because SafeCore sensors can.
					if ( !ic.Component.Enabled )
						continue;

					Sensor sensor = (Sensor)ic.Component;

					if ( sensor.Uid == bumpFailedSensor.Uid )
						continue;

					if ( sensor.IsDualSenseCapable && sensor.PartNumber == bumpFailedSensor.PartNumber &&
						sensor.Type.Code == bumpFailedSensor.Type.Code && !SensorGasResponse.IsFailedCalibrationStatus( sensor.CalibrationStatus ) )
						return ic;
				}
			}

			// instrument does not support DualSense or no DualSense sibling found
			return null;
		}

		/// <summary>
		/// Returns a string to be used as a dictionary key to determine if 
		/// two sensors could run in DualSense together on a Ventis Pro Series 
		/// or SafeCore instrument.
		/// 
		/// DO NOT CALL IF THE SENSOR IS NOT DUALSENSECAPABLE.
		/// </summary>
		private string GetDualSenseCapableKey( Sensor sensor )
		{
			// SafeCore instruments also consider sensor position for DualSense.  However, the SafeCore instrument will
			// not allow two (or more) of the same sensor to be installed in the instrument unless they can be DualSensed
			// together.  So if two of the same DualSense capable sensors are not in adjacent slots (1&2, 3&4, 5&6) the  
			// second sensor will be in DataFault and the DS will go into Sensor Error until the instrument is undocked.   
			// This is good because the second side of dual gas sensors are given the next available position number (7 or
			// higher) and it would be difficult to write a positional rule for SafeCore dual gas sensors.
			return sensor.PartNumber + "#" + sensor.Type.Code;
		}

		#endregion

	}  // end-class Instrument

    #region ENUMS

    public enum CalibrationState
    {
        Unknown = -1,
        Failed,
        Passed,
        RedundantSensorPassed
    }

	public enum ChargePhase
	{
		ChargeOff = 0,
		PreCharge = 1,
		FullCharge = 2,
		TopOff = 3,
		ChargeComplete = 4,
		PreChargeFault = 5,
		ChargeFault = 6,
		ChargeTimeout = 7,
		ChargeOverTempFailure = 8,
		ChargeUnderTempFailure = 9,
		ChargeFailure =  10,
		Taper = 11
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Backlight setting.
	/// </summary>
	public enum BacklightSetting 
	{
		Manual ,		
		Timed ,
		Automatic ,
		Unknown ,
        AlwaysOn ,
        AlwaysOff
	}

    /// <summary>
    /// Accessory Pump settings
    /// </summary>
    public enum AccessoryPumpSetting
    {
        NotApplicable = 0,
        Uninstalled,
        Installed
    }
    #endregion

    



    /// <summary>
    /// Throw whenever an instrument is docked and that instrument is detected to be in a system alarm error state.
    /// This exception provides information about which instrument is in the error state, and what the error is.
    /// </summary>
    /// <remarks>
    /// SGF  Nov-23-2009  DSW-355  (DS2 v7.6)
    /// Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593
    /// </remarks>
    public class InstrumentSystemAlarmException : Exception
    {

        #region Constructors


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serialNumber">
        /// The instrument's serial number.
        /// Specify empty or null if serial number is unavailable or unknown.
        /// </param>
        /// <param name="errorCode">
        /// The instrument's system error code.
        /// Specify 0 if the error code is unavailable or unknown.
        /// </param>
        public InstrumentSystemAlarmException( string serialNumber, int errorCode )
        {
            SerialNumber = serialNumber;
            ErrorCode = errorCode;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// The instrument's serial number.  May be empty or null if serial number is unavailable or unknown.
        /// </summary>
        public String SerialNumber { get; private set; }

        /// <summary>
        /// The instrument's system error code. Will be 0 if the error code is unavailable or not unknown.
        /// </summary>
        public int ErrorCode { get ; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates and returns a string representation of the current exception.
        /// </summary>
        /// <returns>A string representation of the current exception.</returns>
        public override string ToString()
        {
            if ( ErrorCode == 0 && string.IsNullOrEmpty( SerialNumber ) )
                return "Instrument error detected. Instrument Serial number unknown; Error code unknown";

            if ( ErrorCode == 0 && !string.IsNullOrEmpty( SerialNumber ) )
                return String.Format("Instrument error detected. Instrument Serial number is {0}; Error code unknown", SerialNumber);

            if ( string.IsNullOrEmpty( SerialNumber ) && ErrorCode != 0 )
                return String.Format("Instrument error detected. Instrument Serial number is unknown; Error code is {0}", ErrorCode);

            return String.Format("Instrument error detected. Instrument Serial is {0}; Error code is {1}", SerialNumber, ErrorCode);
        }

        #endregion

    }
}
