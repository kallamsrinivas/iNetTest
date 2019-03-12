using System;
using System.Collections.Generic;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet.InetUpload;
using ISC.WinCE.Logger;
using Instrument_ = ISC.iNet.DS.DomainModel.Instrument;
using ISC.Instrument.TypeDefinition;
using TimeZoneInfo = ISC.iNet.DS.DomainModel.TimeZoneInfo;


namespace ISC.iNet.DS.iNet
{
	/// <summary>
	/// Summary description for WebServiceSerializer.
	/// </summary>
	internal class WebServiceSerializer
	{
		private const string CYLINDER_PURPOSE_CALIBRATION = "CALIBRATION";
		private const string CYLINDER_PURPOSE_BUMP = "BUMP";
		private const string COMPONENT_CODE_CYLINDER = "CYLINDER";
		private const string NOT_APPLICABLE_FLAG = "NA";
		private const string ERROR_CODE_EXCEPTION = "EXCEPTION";
		private const string OPTION_ENABLED_FLAG = "ENABLED";
        private const string OPTION_DISABLED_FLAG = "DISABLED";
		private const string NON_ISC_GAS_FLAG = "NON-ISC";

        private const string WSP_LOG_MESSAGE_HEADER = ">INET: ";

        private const int PURGE_GAS_OPERATION_GROUP = 999;


        private TimeZoneInfo _tzi;

        /// <summary>
        /// Default ctor.
        /// </summary>
		internal  WebServiceSerializer( TimeZoneInfo tzi )
        {
            Log.Assert( tzi != null, "tzi cannot be null" );
            _tzi = tzi;
		}

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="instrument"></param>
        /// <param name="lastDockedDsSerialNumber">Serial number of IDS passed-in instrument
        /// was last docked on.</param>
        /// <param name="eventTime"></param>
        /// <returns></returns>
        internal INSTRUMENT GetINSTRUMENT( Instrument_ instrument, string lastDockedDsSerialNumber, DateTime lastDockedTime, DateTime eventTime )
		{
			INSTRUMENT wsInstrument = new INSTRUMENT();

			InstrumentTypeDefinition definition = Inet.CreateInstrumentDefinitionInstance( instrument.Type, instrument.SoftwareVersion );

			wsInstrument.time = eventTime;
            wsInstrument.timeSpecified = ( wsInstrument.time != null );

            // If refId is non-null, then this read event is occurring just after an update event.
            // The refId contains the inet refId of the settings used during the update.
            bool postUpdate = instrument.RefId != DomainModelConstant.NullId;

            // Event though this is an instrument, we upload SettingsUpdate/SettingsRead event codes which
            // are docking station events.  This because iNet uses these event codes for both instrument
            // and docking stations.
            wsInstrument.uploadEventCode = postUpdate ? EventCode.SettingsUpdate : EventCode.SettingsRead;

            wsInstrument.settingsRefId = AssignNullableValue( instrument.RefId );
            wsInstrument.settingsRefIdSpecified = ( wsInstrument.settingsRefId != null );

            wsInstrument.lastDSSN = lastDockedDsSerialNumber;

			// iNet wants to be sent null when the default access code (no password required) is set on the instrument
			wsInstrument.accessCode = instrument.AccessCode != definition.DefaultSecurityCode ? instrument.AccessCode : null;
			wsInstrument.alarms = NOT_APPLICABLE_FLAG;					//TODO: Not implemented

            // Legacy instruments will never have a setup version so just send over 'n/a'/
            // Others always should have a setupversion, so if it's missing (empty), send
            // the missing value to iNet so it knows it's missing
            wsInstrument.dataVersion = instrument.SetupVersion;

			wsInstrument.hardwareVersion = instrument.HardwareVersion;
			wsInstrument.instrumentCode = instrument.Type.ToString();
			wsInstrument.instrumentSubType = (int)instrument.Subtype; // subtype should never be null
			wsInstrument.instrumentSubTypeSpecified = ( wsInstrument.instrumentSubType != null );
			wsInstrument.jobNumber = instrument.JobNumber;
			wsInstrument.languageCode = instrument.Language.Code == string.Empty ? NOT_APPLICABLE_FLAG : instrument.Language.Code;

            wsInstrument.location = string.Empty;

			wsInstrument.partNumber = instrument.PartNumber;
			wsInstrument.setupTech = instrument.SetupTech;
			wsInstrument.sn = instrument.SerialNumber;
			wsInstrument.softwareVersion = instrument.SoftwareVersion;

            wsInstrument.twaTimeBase      = AssignNullableValue( instrument.TWATimeBase );
            wsInstrument.twaTimeBaseSpecified = ( wsInstrument.twaTimeBase != null );
			wsInstrument.operationMinutes = AssignNullableValue( instrument.OperationMinutes );
            wsInstrument.operationMinutesSpecified = ( wsInstrument.operationMinutes != null );

            wsInstrument.setupDate = AssignNullableValueDate( EasternToUtc( instrument.SetupDate ) );

            wsInstrument.setupDateSpecified = ( wsInstrument.setupDate != null );
            wsInstrument.lastDockedTime = AssignNullableValueDate( lastDockedTime );
            wsInstrument.lastDockedTimeSpecified = ( wsInstrument.lastDockedTime != null );

            wsInstrument.totalAlarmMinutes = null;
            wsInstrument.altitude = wsInstrument.latitude = wsInstrument.longitude = null;
            wsInstrument.altitudeSpecified = ( wsInstrument.altitude != null );

            wsInstrument.cylinder = new CYLINDER[0]; // fixed monitors can have attached cylinders; we don't support that concept in Viper.

            List<SENSOR> wsSensorList = new List<SENSOR>();
            List<BATTERY> wsBatteryList = new List<BATTERY>();

			foreach( InstalledComponent icomp in instrument.InstalledComponents )
			{
				if ( icomp.Component is Sensor )
				{
					Sensor sensor = (Sensor)icomp.Component;
                    SensorType sensorType = (SensorType)sensor.Type;

					SENSOR wsSensor = new SENSOR();

                    wsSensor.position = icomp.Position;

                    wsSensor.uid = sensor.Uid;
                    wsSensor.sn = sensor.Uid;

                    wsSensor.componentCode = sensor.Type.Code;
                    wsSensor.manufacturerCode = sensor.ManufacturerCode;
                    wsSensor.partNumber = ( sensor.PartNumber == string.Empty ) ? NOT_APPLICABLE_FLAG : sensor.PartNumber;
					wsSensor.hardwareVersion = sensor.HardwareVersion;
                    wsSensor.softwareVersion = ( sensor.SoftwareVersion == string.Empty ) ? NOT_APPLICABLE_FLAG : sensor.SoftwareVersion;

                    // Legacy instruments will never have a setup version so just send over 'n/a'/
                    // Others always should have a setupversion, so if it's missing (empty), send
                    // the missing value to iNet so it knows it's missing
                    wsSensor.dataVersion = ( sensor.SetupVersion == string.Empty ) ? NOT_APPLICABLE_FLAG : sensor.SetupVersion;

					wsSensor.displayDecimal = AssignNullableValue( GetDecimalsFromResolution( sensor.Resolution ) );
                    wsSensor.displayDecimalSpecified = ( wsSensor.displayDecimal != null );
					wsSensor.calGasCode = sensor.CalibrationGas.Code;
                    wsSensor.calTimeOut = AssignNullableValue( sensor.CalibrationTimeout );
                    wsSensor.calTimeOutSpecified = ( wsSensor.calTimeOut != null );
					wsSensor.calGasConcentration = AssignNullableValue( sensor.CalibrationGasConcentration );
                    wsSensor.calGasConcentrationSpecified = ( wsSensor.calGasConcentration != null );

                    wsSensor.unitOfMeasurement = AssignNullableValue( ( sensorType.MeasurementType == MeasurementType.Unknown) ? int.MinValue : (int)sensorType.MeasurementType );
                    wsSensor.unitOfMeasurementSpecified = ( wsSensor.unitOfMeasurement != null );
					
                    wsSensor.setupDate = AssignNullableValueDate( EasternToUtc( sensor.SetupDate ) );
                    wsSensor.setupDateSpecified = ( wsSensor.setupDate != null );

                    wsSensor.birthDate = AssignNullableValueDate( EasternToUtc( sensor.SetupDate ) );
                    wsSensor.birthDateSpecified = ( wsSensor.birthDate != null );

                    wsSensor.manufacturedDate = AssignNullableValueDate( EasternToUtc( sensor.SetupDate ) );
                    wsSensor.manufacturedDateSpecified = ( wsSensor.manufacturedDate != null );

                    wsSensor.installTime = null; // installTime will be assigned by the server
                    wsSensor.installTimeSpecified = false;

					// if the sensor's gas alert is equal to the low alarm or the instrument does not 
					// support the feature, than we want to upload null to iNet
					wsSensor.alarmAlert = !definition.HasGasAlertFeature || sensor.Alarm.GasAlert == sensor.Alarm.Low ? null : AssignNullableValue( sensor.Alarm.GasAlert );
					wsSensor.alarmAlertSpecified = ( wsSensor.alarmAlert != null );
					wsSensor.alarmLow = AssignNullableValue( sensor.Alarm.Low );
					wsSensor.alarmLowSpecified = ( wsSensor.alarmLow != null );
					wsSensor.alarmHigh = AssignNullableValue( sensor.Alarm.High );
                    wsSensor.alarmHighSpecified = ( wsSensor.alarmHigh != null );
					wsSensor.alarmSTEL = AssignNullableValue( sensor.Alarm.STEL );
                    wsSensor.alarmSTELSpecified = ( wsSensor.alarmSTEL != null );
					wsSensor.alarmTWA = AssignNullableValue( sensor.Alarm.TWA );
                    wsSensor.alarmTWASpecified = ( wsSensor.alarmTWA != null );
					
                    // Deadband in the DS2 datamodel and database is wrongly defined as an
                    // integer value type. It should originally have been defined as float/double.
                    // This was wrongly done back in the beginning of the DS2 project and
                    // cannot be changed without major compatibility problems between 
                    // older/newer docking stations and servers.
                    // This integer value is the datamodel is the raw deadband value
                    // as read from the sensor (i.e., no resolution applied).
                    // iNet needs a decimal value, though, i.e., the raw value with the
                    // resolution applied. e.g. if raw deadband from the sensor is "4",
                    // and the sensor's resolution is 0.1, then the deadband we need to upload
                    // to iNet is "0.4"
                    double deadband = double.MinValue;
                    if ( sensor.DeadBand != int.MinValue )
                        deadband = sensor.DeadBand * sensor.Resolution;

                    wsSensor.deadBand = AssignNullableValue( deadband );
                    wsSensor.deadBandSpecified = ( wsSensor.deadBand != null );

					wsSensor.filter = AssignNullableValue( sensor.Filter );
                    wsSensor.filterSpecified = ( wsSensor.filter != null );

					wsSensor.overRange = AssignNullableValue( sensor.OverRange );
                    wsSensor.overRangeSpecified = ( wsSensor.overRange != null );
					wsSensor.peakReading = AssignNullableValue( sensor.PeakReading );
                    wsSensor.peakReadingSpecified = ( wsSensor.peakReading != null );
					wsSensor.polarity = AssignNullableValue( sensor.Polarity );  // always MinValue; we can't get this
                    wsSensor.polaritySpecified = ( wsSensor.polarity != null );

					wsSensor.spanCoefMax = AssignNullableValue( sensor.SpanCoefMax );
                    wsSensor.spanCoefMaxSpecified = ( wsSensor.spanCoefMax != null );
					wsSensor.spanCoefMin = AssignNullableValue( sensor.SpanCoefMin );
                    wsSensor.spanCoefMinSpecified = ( wsSensor.spanCoefMin != null );

                    wsSensor.tempCompHigh = AssignNullableValue( sensor.TemperatureCompHigh == double.MinValue ? int.MinValue : Convert.ToInt32(sensor.TemperatureCompHigh) );
                    wsSensor.tempCompHighSpecified = ( wsSensor.tempCompHigh != null );
                    wsSensor.tempCompLow  = AssignNullableValue( sensor.TemperatureCompLow == double.MinValue ? int.MinValue : Convert.ToInt32(sensor.TemperatureCompLow) );
                    wsSensor.tempCompLowSpecified = ( wsSensor.tempCompLow != null );

                    wsSensor.tempMax = AssignNullableValue( sensor.MaxTemperature );
                    wsSensor.tempMaxSpecified = ( wsSensor.tempMax != null );
					wsSensor.tempMin = AssignNullableValue( sensor.MinTemperature );
                    wsSensor.tempMinSpecified = ( wsSensor.tempMin != null );

					wsSensor.zeroMax = AssignNullableValue( sensor.ZeroMax );
                    wsSensor.zeroMaxSpecified = ( wsSensor.zeroMax != null );
					wsSensor.zeroMin = AssignNullableValue( sensor.ZeroMin );
                    wsSensor.zeroMinSpecified = ( wsSensor.zeroMin != null );

                    ///// Response Factor / Gas Detected rules follow... /////

                    // Default it to the sensor's gas type.
                    wsSensor.gasDecting = sensor.Type.Code.Replace( 'S', 'G' );
                    
                    // 'wsSensor.responseFactor' is the value of the custom response factor
                    // (if there is one).  Default it to None here.  We may change it a few
                    // line later below if we find out the sensor has a custom response factor.
                    wsSensor.responseFactor = null;

                    if ( sensor.Type.Code == SensorCode.PID )
                    {
                        wsSensor.gasDecting = sensor.GasDetected;

                        // For custom response factors, we need to give iNet the name
                        // of that custom response factor.
                        if ( GasCode.IsCustomResponseFactor( instrument.Type, sensor.GasDetected ) )
                        {
                            foreach ( ResponseFactor rf in instrument.CustomPidFactors )
                            {
                                if ( rf.GasCode == sensor.GasDetected )
                                {
                                    wsSensor.gasDecting = rf.Name;
                                    wsSensor.responseFactor = AssignNullableValue( rf.Value );
                                    wsSensor.responseFactorSpecified = ( wsSensor.responseFactor != null );
                                    break;
                                }
                            }
                        }
                    }

                    else if ( sensor.Type.Code == SensorCode.CombustibleLEL 
                    ||        sensor.Type.Code == SensorCode.CombustiblePPM )
                    {
                        // If GasCode is filled in, this would be the correlation factor
                        // that instrument is configured to use.  Should only apply
                        // to MX6 at this time.
                        if ( sensor.GasDetected != string.Empty )
                            wsSensor.gasDecting = sensor.GasDetected;

                        // Will be empty for non-MX6 instruments such as itx.
                        // In this case, just set it to its cal gas
                        else
                            wsSensor.gasDecting = sensor.CalibrationGas.Code;
                    }
                    // GasCode for Methane is not the same as the Sensor Code,
                    // so we can't do the default 'S' to 'G' replacement.
                    else if (sensor.Type.Code == SensorCode.MethaneIR || sensor.Type.Code == SensorCode.MethaneIRLEL) //Suresh 19-OCTOBER-2011 INS-2354
                        wsSensor.gasDecting = GasCode.Methane;

                    // Additional miscellaneous sensor PROPERTIES...

                    List<PROPERTY> sensorPropertyList = new List<PROPERTY>();

                    sensorPropertyList.Add( MakeProperty( "ENABLED", sensor.Enabled.ToString() )  );

                    // Add Additional properties here

                    wsSensor.property = sensorPropertyList.ToArray();

                    wsSensorList.Add( wsSensor );
				}
				else if ( icomp.Component is Battery )
				{
					Battery battery = (Battery)icomp.Component;
					BATTERY wsBattery = new BATTERY();

					wsBattery.componentCode = battery.Type.Code;
					wsBattery.dataVersion = NOT_APPLICABLE_FLAG;
					wsBattery.hardwareVersion = NOT_APPLICABLE_FLAG;
					wsBattery.manufacturerCode = battery.ManufacturerCode;
					wsBattery.partNumber = battery.PartNumber;
					wsBattery.sn = battery.Uid;
					wsBattery.softwareVersion = battery.SoftwareVersion == string.Empty ? NOT_APPLICABLE_FLAG : battery.SoftwareVersion;
					wsBattery.uid = battery.Uid;
                    wsBattery.setupDate = AssignNullableValueDate( EasternToUtc( battery.SetupDate ) );
                    wsBattery.setupDateSpecified = ( wsBattery.setupDate != null );
                    wsBattery.manufacturedDate = AssignNullableValueDate( EasternToUtc( battery.SetupDate ) );
                    wsBattery.manufacturedDateSpecified = ( wsBattery.manufacturedDate != null );
                    wsBattery.installTime = null; // installTime will be assigned by the server
                    wsBattery.installTimeSpecified = ( wsBattery.installTime != null );
                    wsBattery.operationMinutes = AssignNullableValue( battery.OperationMinutes );
                    wsBattery.operationMinutesSpecified = ( wsBattery.operationMinutes != null );

                    // Additional miscellaneous sensor PROPERTIES go here...

                    List<PROPERTY> batteryPropertyList = new List<PROPERTY>();

                    if ( battery.SetupTech != string.Empty )
                        batteryPropertyList.Add( MakeProperty( "SETUP_TECH", battery.SetupTech )  );

                    if ( batteryPropertyList.Count > 0 )
                        wsBattery.property = batteryPropertyList.ToArray();

                    wsBatteryList.Add( wsBattery );
				}
			}

            wsInstrument.sensor = wsSensorList.ToArray();
            wsInstrument.battery = wsBatteryList.ToArray();

            // If instrument's WirelessModule exists, then upload its info.
            if ( instrument.WirelessModule != null )
            {
				WirelessModule module = instrument.WirelessModule;
                WIRELESS_MODULE wsWirelessModule = new WIRELESS_MODULE();

				if ( instrument.Type == DeviceType.MX4 ) // Ventis LS
				{
					wsWirelessModule.componentCode = "WM001";
					wsWirelessModule.uid = wsWirelessModule.sn = instrument.SerialNumber + "#" + wsWirelessModule.componentCode;
				}
				else if ( instrument.Type == DeviceType.SC || instrument.Type == DeviceType.VPRO )
				{
					wsWirelessModule.componentCode = "WM002";
					wsWirelessModule.uid = wsWirelessModule.sn = module.MacAddress + "#" + wsWirelessModule.componentCode;
				}
					
				wsWirelessModule.componentType = "WIRELESS";
                
                wsWirelessModule.macAddress = module.MacAddress;
                wsWirelessModule.softwareVersion = module.SoftwareVersion;
                wsWirelessModule.status = module.Status;
                wsWirelessModule.transmissionInterval = module.TransmissionInterval;

				if ( instrument.Type == DeviceType.SC || instrument.Type == DeviceType.VPRO )
				{
					wsWirelessModule.hardwareVersion = module.HardwareVersion;
					wsWirelessModule.radioHardwareVersion = module.RadioHardwareVersion;
					wsWirelessModule.osVersion = module.OsVersion;

					List<PROPERTY> wirelessProperties = new List<PROPERTY>( module.Options.Count + 6 );
					
					wirelessProperties.Add( MakeProperty( "WL_ENCRYPTION_KEY", module.EncryptionKey ) );
					wirelessProperties.Add( MakeProperty( "WL_MESSAGE_HOPS", module.MessageHops.ToString() ) );
					wirelessProperties.Add( MakeProperty( "WL_MAX_PEERS", module.MaxPeers.ToString() ) );
					wirelessProperties.Add( MakeProperty( "WL_PRIMARY_PUBLIC_CHANNEL", module.PrimaryChannel.ToString() ) );
					wirelessProperties.Add( MakeProperty( "WL_SECONDARY_PUBLIC_CHANNEL", module.SecondaryChannel.ToString() ) );
					wirelessProperties.Add( MakeProperty( "WL_ACTIVE_CHANNEL_MASK", module.ActiveChannelMask ) );                    
                    wirelessProperties.Add( MakeProperty( "WL_FEATURE_BITS", module.WirelessFeatureBits ) );
                    if ( module.WirelessBindingTimeout != int.MinValue )
                    {
                        wirelessProperties.Add( MakeProperty( "WL_SCRIPT_BINDING_TIMEOUT", module.WirelessBindingTimeout.ToString() ) );
                    }

                    if (definition.HasWirelessListeningPostChannelMaskFeature) // supported only by SC and VPRO v3.0 or higher
                        wirelessProperties.Add(MakeProperty("WL_LISTENING_POST_CHANNEL_MASK", module.ListeningPostChannelMask));

					foreach ( DeviceOption wirelessOption in module.Options )
						wirelessProperties.Add( MakeProperty( wirelessOption.Code, wirelessOption.Enabled ? OPTION_ENABLED_FLAG : OPTION_DISABLED_FLAG ) );				

					wsWirelessModule.property = wirelessProperties.ToArray();
				}

				wsInstrument.wirelessModule = new WIRELESS_MODULE[] { wsWirelessModule };
            }

            List<PROPERTY> propertyList = new List<PROPERTY>( instrument.Options.Count + 23 ); // allocate some extra to reserve extra room for miscellaneous

			foreach ( DeviceOption deviceOption in instrument.Options )
                propertyList.Add( MakeProperty( deviceOption.Code, deviceOption.Enabled ? OPTION_ENABLED_FLAG : OPTION_DISABLED_FLAG ) );				

            // Additional instrument PROPERTIES...

			if ( !string.IsNullOrEmpty( instrument.BootloaderVersion ) ) // for instrument which does not support bootloader version, it will be empty
				propertyList.Add( MakeProperty( "BOOTLOADER_VERSION", instrument.BootloaderVersion ) );

            propertyList.Add( MakeProperty( "DATALOG_RECORDING_INTERVAL", instrument.RecordingInterval.ToString() ) );

			if ( instrument.OomWarningInterval > 0 ) // for instrument which does not support out-of-motion warning interval, it will be zero
				propertyList.Add( MakeProperty( "MAN_DOWN_WARNING_INTERVAL", instrument.OomWarningInterval.ToString() ) );

			if ( instrument.DockInterval > 0 ) // for instrument which does not support dock overdue interval, it will be zero
				propertyList.Add( MakeProperty( "DOCK_OVERDUE_INTERVAL", instrument.DockInterval.ToString() ) );

            if ( instrument.MaintenanceInterval > 0 ) // for instrument which does not support maintenance interval, it will be zero
                propertyList.Add( MakeProperty( "MAINTENANCE_INTERVAL", instrument.MaintenanceInterval.ToString() ) );            
            
            wsInstrument.calInterval = AssignNullableValue(instrument.CalibrationInterval);
			wsInstrument.bumpInterval = AssignNullableValue(instrument.BumpInterval);

            propertyList.Add( MakeProperty( "BUMP_THRESHOLD", instrument.BumpThreshold.ToString() ) );
            propertyList.Add( MakeProperty( "BUMP_TIMEOUT", instrument.BumpTimeout.ToString() ) );

			if ( !string.IsNullOrEmpty( instrument.CompanyName ) ) // for instrument which does not support company name, it will be empty
				propertyList.Add( MakeProperty( "COMPANY_NAME", instrument.CompanyName ) );

			if ( !string.IsNullOrEmpty( instrument.CompanyMessage ) ) // for instrument which does not support company message, it will be empty
				propertyList.Add( MakeProperty( "COMPANY_MESSAGE", instrument.CompanyMessage ) ); 

			// Do not send empty alarm message strings to iNet.
			for ( int i = 0; i < instrument.AlarmActionMessages.Count; i++ )
			{
				string sensorCode = instrument.AlarmActionMessages[i].SensorCode;

				// ALARM_MESSAGE_GAS_ALERT_S9999
				if ( !string.IsNullOrEmpty( instrument.AlarmActionMessages[i].GasAlertMessage ) )
					propertyList.Add( MakeProperty( "ALARM_MESSAGE_GAS_ALERT_" + sensorCode, instrument.AlarmActionMessages[i].GasAlertMessage ) );
				
				// ALARM_MESSAGE_LOW_S9999
				if ( !string.IsNullOrEmpty( instrument.AlarmActionMessages[i].LowAlarmMessage ) )
					propertyList.Add( MakeProperty( "ALARM_MESSAGE_LOW_" + sensorCode, instrument.AlarmActionMessages[i].LowAlarmMessage ) );

				// ALARM_MESSAGE_HIGH_S9999
				if ( !string.IsNullOrEmpty( instrument.AlarmActionMessages[i].HighAlarmMessage ) )
					propertyList.Add( MakeProperty( "ALARM_MESSAGE_HIGH_" + sensorCode, instrument.AlarmActionMessages[i].HighAlarmMessage ) );

				// ALARM_MESSAGE_STEL_S9999
				if ( !string.IsNullOrEmpty( instrument.AlarmActionMessages[i].StelAlarmMessage ) )
					propertyList.Add( MakeProperty( "ALARM_MESSAGE_STEL_" + sensorCode, instrument.AlarmActionMessages[i].StelAlarmMessage ) );

				// ALARM_MESSAGE_TWA_S9999
				if ( !string.IsNullOrEmpty( instrument.AlarmActionMessages[i].TwaAlarmMessage ) )
					propertyList.Add( MakeProperty( "ALARM_MESSAGE_TWA_" + sensorCode, instrument.AlarmActionMessages[i].TwaAlarmMessage ) );
			}

            if ( instrument.CountryOfOrigin != string.Empty )
                propertyList.Add( MakeProperty( "ORIGIN_COUNTRY", instrument.CountryOfOrigin ) );

			// User(s)
			if ( definition.MaxUserCount == 1 ) // MX4, Ventis Pro, TX1, GB Pro
            {				
                // The "active user" will be the first (and only) user in the instrument's list of users.
                if ( instrument.Users.Count > 0 )
                    wsInstrument.user = instrument.Users[ 0 ].ToString();
            }
			else // MX6, SafeCore, GB Plus
            {				
                if ( instrument.ActiveUser != string.Empty )
                    wsInstrument.user = instrument.ActiveUser;

                wsInstrument.Users = instrument.Users.ToArray();
            }

			// Site(s)
			if ( definition.MaxSiteCount == 1 ) // MX4, Ventis Pro, TX1, GB Pro
			{
				// The "active site" will be the first (and only) site in the instrument's list of sites.
				if ( instrument.Sites.Count > 0 )
					propertyList.Add( MakeProperty( "ACTIVE_SITE", instrument.Sites[0].ToString() ) );
			}
			else // MX6, SafeCore, GB Plus
			{
				if ( instrument.ActiveSite != string.Empty )
					propertyList.Add( MakeProperty( "ACTIVE_SITE", instrument.ActiveSite ) );

				wsInstrument.Sites = instrument.Sites.ToArray();
			}
            
            if ( instrument.AccessoryPump != AccessoryPumpSetting.NotApplicable )
                propertyList.Add( MakeProperty( "ACCESSORY_PUMP", instrument.AccessoryPump.ToString().ToUpper() ) );

			if ( instrument.BacklightTimeout > 0 ) // for instrument which does not support backlight, timer will have 0.
				propertyList.Add( MakeProperty( "BACKLIGHT_TIMEOUT", instrument.BacklightTimeout.ToString() ) );

            if ( instrument.MagneticFieldDuration > 0 ) // for instrument which does not support mag duration, it will be zero.
                propertyList.Add( MakeProperty( "MAGNETIC_FIELD_DURATION", instrument.MagneticFieldDuration.ToString() ) );

			if ( instrument.WirelessPeerLostThreshold > 0 )
				propertyList.Add( MakeProperty( "WL_PEER_LOST_THRESHOLD", instrument.WirelessPeerLostThreshold.ToString() ) );

			if ( instrument.WirelessNetworkLostThreshold > 0 )
				propertyList.Add( MakeProperty( "WL_NETWORK_LOST_THRESHOLD", instrument.WirelessNetworkLostThreshold.ToString() ) );

			if ( instrument.WirelessReadingsDeadband > 0 )
				propertyList.Add( MakeProperty( "WL_READINGS_DEADBAND", instrument.WirelessReadingsDeadband.ToString() ) );

            if (instrument.WirelessNetworkDisconnectDelay > 0)
                propertyList.Add( MakeProperty( "WL_NETWORK_DISCONNECT_DELAY", instrument.WirelessNetworkDisconnectDelay.ToString() ) );

            if ( !string.IsNullOrEmpty(instrument.WirelessAlarmMask ) )
                propertyList.Add( MakeProperty( "WL_ALARM_MASK", instrument.WirelessAlarmMask ) );

            //Check for instrument type here. WL_FEATURE supports only on Ventis Pro 2.0
            if ( definition.HasWirelessFeature && instrument.Type == DeviceType.VPRO )
            {
                propertyList.Add( MakeProperty( "WL_FEATURE", instrument.WirelessFeatureActivated ? OPTION_ENABLED_FLAG : OPTION_DISABLED_FLAG ) );
            }

            if ( definition.HasBluetoothFeature )
            {
                propertyList.Add( MakeProperty( "INET_NOW_FEATURE", instrument.INetNowFeatureActivated ? OPTION_ENABLED_FLAG : OPTION_DISABLED_FLAG ) );

                if ( instrument.LoneWorkerOkMessageInterval > 0 )
                    propertyList.Add( MakeProperty( "LONE_WORKER_OK_MESSAGE_INTERVAL", instrument.LoneWorkerOkMessageInterval.ToString() ) );

                propertyList.Add( MakeProperty( "BLE_MAC_ADDRESS", instrument.BluetoothMacAddress ) );
                propertyList.Add( MakeProperty( "BLE_SOFTWARE_VERSION", instrument.BluetoothSoftwareVersion ) );
                propertyList.Add( MakeProperty( "BLE_FEATURE", instrument.BluetoothFeatureActivated ? OPTION_ENABLED_FLAG : OPTION_DISABLED_FLAG ) );
            }

            if ( definition.HasGpsFeature )
            {
                if ( instrument.GpsReadingInterval > 0 )
                    propertyList.Add( MakeProperty( "GPS_READING_INTERVAL", instrument.GpsReadingInterval.ToString() ) );
            }                
                        
			// For SafeCore modules, we need to upload the S/N of the last base unit it was docked in if one is available.
			// Due to time changes, the base unit with the greatest timestamp may not always be the last base.  However, the
			// module stores the list of bases in a queue format.  Regardless of the install times, the last base unit in the 
			// list should be the last base unit the module was docked.
			if ( instrument.BaseUnits.Count > 0 )
				propertyList.Add( MakeProperty( "BASE_SN", instrument.BaseUnits[ instrument.BaseUnits.Count - 1 ].SerialNumber ) );

            wsInstrument.property = propertyList.ToArray();

            WebServiceLog.LogINSTRUMENT(wsInstrument, WSP_LOG_MESSAGE_HEADER);

			return wsInstrument;
		}

		internal ACCESSORY GetACCESSORY( BaseUnit baseUnit, string instSN, string dsSN )
		{
			ACCESSORY wsAccessory = new ACCESSORY();

			wsAccessory.time = baseUnit.InstallTime;
			wsAccessory.timeSpecified = ( wsAccessory.time != null );

			wsAccessory.lastDSSN = dsSN;

			wsAccessory.sn = baseUnit.SerialNumber;
			wsAccessory.equipmentCode = baseUnit.Type.ToString();

			wsAccessory.partNumber = baseUnit.PartNumber;
			wsAccessory.setupDate = AssignNullableValueDate( EasternToUtc( baseUnit.SetupDate ) );
			wsAccessory.setupDateSpecified = ( wsAccessory.setupDate != null );
			wsAccessory.operationMinutes = AssignNullableValue( baseUnit.OperationMinutes );
			wsAccessory.operationMinutesSpecified = ( wsAccessory.operationMinutes != null );

			List<PROPERTY> wsAccessoryProperties = new List<PROPERTY>();

			wsAccessoryProperties.Add( MakeProperty( "INST_SN", instSN ) );

			wsAccessory.property = wsAccessoryProperties.ToArray();

			WebServiceLog.LogACCESSORY( wsAccessory, WSP_LOG_MESSAGE_HEADER );

			return wsAccessory;
		}

        /// <summary>
        /// Given a sensor's Resolution (e.g. "0.01"), this routine
        /// will return the sensor's 'decimal places' (e.g. "2").
        /// </summary>
        /// <param name="resolution"></param>
        /// <returns></returns>
        static private int GetDecimalsFromResolution( double resolution )
        {
            // Resolution will be a decimal value; (e.g. "0.01")
            // We need to convert to number of 'decimal places'.
            // So, divide into 1.  (e.g., 1 / 0.1 = 100.)
            // Then convert to string and take count of number of zeros as
            // the number of decimal places.  (e.g. decimalPlaces = 2 if res = 100)
            decimal dec = 0;
            try
            {
                if ( resolution >= 0.0F )
                    dec = Convert.ToDecimal( resolution );
                dec  = 1.0M / dec;
            }
            catch  // Watch out for any todecimal() failures or Divide By Zero errors
            {
                return int.MinValue;
            }

            dec = decimal.Truncate( dec );

            // The decimal separator for a floating point number (i.e., the "." in "0.01")
            // will be different depending on the language of the OS that we're running on.
            string decimalSeparator = ".";//TODO:CF Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator;

            // Count up the zeros.  e.g., if dec is now '100', then we'll end up 
            // with a count of '2'.

            int decCount = 0;
            foreach ( char c in dec.ToString().ToCharArray() )
            {
                // Stop when we hit the decimal separtor.
                if ( c.ToString() == decimalSeparator ) break;

                if ( c == '0' ) decCount++;
            }

            return decCount;
        }

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="valueToAssign"></param>
		private static Nullable<float> AssignNullableValue( double valueToAssign )
		{
            if ( double.IsNaN( valueToAssign ) )
                valueToAssign = 0D;

			// Positive or Negative infinity is translated into zero.
			if ( Double.IsNegativeInfinity( valueToAssign )
			||   Double.IsPositiveInfinity( valueToAssign ) )
                return 0.0F;

			// Captures DomainModelConstant.NullDouble and returns null
            if ( valueToAssign <= int.MinValue )
                return null;

            if ( valueToAssign > int.MaxValue )
				return (float)int.MaxValue;

			// All is well
			return (float)valueToAssign;
		}

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="valueToAssign"></param>
        private static Nullable<float> AssignNullableValue( float valueToAssign )
        {
            if ( float.IsNaN( valueToAssign ) )
                return 0F;

			if ( valueToAssign <= int.MinValue || valueToAssign == DomainModelConstant.NullFloat )
                return null;

            if ( valueToAssign > int.MaxValue )
				return (float)int.MaxValue;

            // All is well
			return (float)valueToAssign;
        }

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="valueToAssign"></param>
		private static Nullable<DateTime> AssignNullableValueDate( DateTime valueToAssign )
		{
            if ( valueToAssign == DomainModelConstant.NullDateTime )
                return null;

			return valueToAssign;
		}

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="valueToAssign"></param>
		private static Nullable<int> AssignNullableValue( int valueToAssign )
		{
			if ( valueToAssign == DomainModelConstant.NullInt )
                return null;

            return valueToAssign;
		}

        private static Nullable<short> AssignNullableValue( short valueToAssign )
        {
            if ( valueToAssign == DomainModelConstant.NullShort )
                return null;

            return valueToAssign;
        }

        /// <summary>
        /// Assigns an int value to a destination of type short.  Needs to be
        /// named differently because of already existing assignullablevalue
        /// that accepts an 'int'.
        /// DEBUG (JMP): I don't think this is needed anymore?
        /// </summary>
        /// <param name="valueToAssign"></param>
        /// <returns></returns>
		private static Nullable<short> AssignNullableValueToShort( int valueToAssign )
		{
			if ( valueToAssign <= short.MinValue )
                return null;

            if ( valueToAssign >= short.MaxValue )
				return short.MaxValue;

            return (short)valueToAssign;
		}

		private static Nullable<long> AssignNullableValue( long valueToAssign )
		{
			if ( valueToAssign == DomainModelConstant.NullLong )
                return null;

		    return valueToAssign;
		}

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="idds">The IDS to send</param>
        /// <param name="eventTime"></param>
        /// <param name="postUpdate">True if upload is occurring as a followup to a SettingsUpdate. Else false.</param>
        /// <returns></returns>
        internal DOCKING_STATION GetDOCKING_STATION( DockingStation ds, DateTime eventTime, bool postUpdate )
		{
            // Software version is one of the fields that should ALWAYS be present.  If it's not, it's likely
            // because the passed-in dockings station is a copy of Configuration.DockingStation instead of
            // Controller.GetDockingStation.
            Log.Assert( !string.IsNullOrEmpty( ds.SoftwareVersion ), "DockingStation.SoftwareVersion is null or empty." );

			// Build up the object that needs to be sent to iNet
			DOCKING_STATION wsDockingStation = new DOCKING_STATION();

            wsDockingStation.uploadEventCode = postUpdate ? EventCode.SettingsUpdate : EventCode.SettingsRead;

            wsDockingStation.settingsRefId = AssignNullableValue( ds.RefId ) ;
            wsDockingStation.settingsRefIdSpecified = ( wsDockingStation.settingsRefId != null );
            wsDockingStation.time = eventTime;
            wsDockingStation.timeSpecified = ( wsDockingStation.time != null );
            wsDockingStation.lastRebootDate = AssignNullableValueDate( ds.LastRebootTime );
            wsDockingStation.lastRebootDateSpecified = ( wsDockingStation.lastRebootDate != null );

            wsDockingStation.setupDate = AssignNullableValueDate( EasternToUtc( ds.SetupDate ) );

            wsDockingStation.setupDateSpecified = (wsDockingStation.setupDate != null );
            wsDockingStation.setupTech = ds.SetupTech;
            wsDockingStation.activityMinutes = wsDockingStation.operationMinutes = null; // we never upload these fields.
            wsDockingStation.sn = ds.SerialNumber;
            wsDockingStation.dockingStationType = EquipmentTypeCode.VDS;
            wsDockingStation.productGeneration = ds.Reservoir ? 1 /*iNetDS*/ : 2 /*DSX*/ ;
            wsDockingStation.productGenerationSpecified = ( wsDockingStation.productGeneration != null );
			wsDockingStation.accountId = null; // This is supplied at actual time of upload.
            wsDockingStation.cluster = null; // Viper does not have clusters.
            wsDockingStation.connectorVersion = null; // not applicable to Viper.
			wsDockingStation.dataVersion = null; // Not implemented.
			wsDockingStation.dockingStationCode = ds.Type.ToString();
			wsDockingStation.hardwareVersion = ds.HardwareVersion;
			wsDockingStation.softwareVersion = ds.SoftwareVersion;
            wsDockingStation.jobNumber = NOT_APPLICABLE_FLAG;
            wsDockingStation.useExpiredCylinders = ds.UseExpiredCylinders;
            wsDockingStation.languageCode = ds.Language.Code;
            wsDockingStation.lelBumpTestGas = ds.CombustibleBumpTestGas == string.Empty ? null : ds.CombustibleBumpTestGas;
            wsDockingStation.location = null;
			wsDockingStation.partNumber = ds.PartNumber;
			wsDockingStation.serverSWVersion = null; // not applicable to Viper.
            wsDockingStation.timezone = ds.TimeZoneInfo.GetTimeZoneName( ds.TimeZoneInfo.ToLocalTime( DateTime.UtcNow ) );

            wsDockingStation.cylinder =  MakeCylinders( ds ); // Cylinders attached to docking station.

            List<PROPERTY> props = new List<PROPERTY>();
 
            props.Add( MakeProperty( "MENU_LOCKED", ds.MenuLocked.ToString() ) );
            props.Add( MakeProperty( "USE_AUDIBLE_ALARM", ds.UseAudibleAlarm.ToString() ) );

            if ( ds.PeripheralBoardRevision.Length > 0 ) props.Add( MakeProperty( "PERIPH_PCB_REV", ds.PeripheralBoardRevision ) );
            if ( ds.LcdType.Length > 0 ) props.Add( MakeProperty( "LCD_TYPE", ds.LcdType ) );
            props.Add( MakeProperty( "NAND_FLASH_ECC_ENABLED", ds.NandFlashEcc.ToString() ) );

            props.Add( MakeProperty( "NUM_GAS_PORTS", ds.NumGasPorts.ToString() ) );

            props.Add( MakeProperty( "FLOW_OFFSET", ds.FlowOffset.ToString() ) );

            //INS-7008  If dock is MX4, upload to iNet whether MX4 dock has new cradle or not.
            if(ds.Type == DeviceType.MX4)
                props.Add(MakeProperty("NEW_CRADLE", ds.HasNewVentisCradle.ToString() ) );  

            // Networking
            if ( ds.NetworkSettings.IpAddress != string.Empty ) props.Add( MakeProperty( "IP_ADDRESS", ds.NetworkSettings.IpAddress ) );
            if ( ds.NetworkSettings.SubnetMask != string.Empty ) props.Add( MakeProperty( "SUBNET_MASK", ds.NetworkSettings.SubnetMask ) );
            if ( ds.NetworkSettings.Gateway != string.Empty ) props.Add( MakeProperty( "GATEWAY", ds.NetworkSettings.Gateway ) );
            if ( ds.NetworkSettings.MacAddress != string.Empty ) props.Add( MakeProperty( "MAC_ADDRESS", ds.NetworkSettings.MacAddress ) );
            if ( ds.NetworkSettings.DnsPrimary != string.Empty ) props.Add( MakeProperty( "DNS1", ds.NetworkSettings.DnsPrimary ) );
            if ( ds.NetworkSettings.DnsSecondary != string.Empty ) props.Add( MakeProperty( "DNS2", ds.NetworkSettings.DnsSecondary ) );
            //Log.Assert( ds.NetworkSettings.DnsDhcp != null, "DockingStation.NetworkSettings.DnsDhcp should not be null." );
            if ( ds.NetworkSettings.DnsDhcp != null ) props.Add( MakeProperty( "DNS_DHCP", ds.NetworkSettings.DnsDhcp.ToString() ) );
            props.Add( MakeProperty( "DHCP_ENABLED", ds.NetworkSettings.DhcpEnabled.ToString() ) );
            props.Add( MakeProperty( "PROXYINUSE", ( ds.InetProxy.Trim().Length > 0 ).ToString() ) ); //Suresh 09-SEP-2011 INS-2248
            // SSAM INS-7926 v7.6
            // If DSX is using an HTTPS proxy, upload this information to iNet.
            // This information is relevant in identifying whether the DSX uses 
            // the Rebex certificate policy or TrustAllCertificatePolicy.
            if (ds.InetProxy.Trim().Length > 0)
                props.Add( MakeProperty( "HTTPS_PROXY", ds.InetProxy.Trim().ToUpper().StartsWith("HTTPS").ToString() ) );

            // Logging and tech support options
            props.Add( MakeProperty( "LOG_CAPACITY", ds.LogCapacity.ToString() ) );
            props.Add( MakeProperty( "LOG_LEVEL", ds.LogLevel.ToString().ToUpper() ) );
			props.Add( MakeProperty( "LOG_SERIAL_PORT", ds.LogToSerialPort.ToString() ) );
			props.Add( MakeProperty( "LOG_TO_FILE", ds.LogToFile.ToString() ) );

            props.Add( MakeProperty( "WEBAPP_ENABLED", ds.WebAppEnabled.ToString() ) );
            // We do not upload the web app password as then it could be viewable in iNet which is a security concern.
            //if ( ds.WebAppPassword != string.Empty ) props.Add( MakeProperty( "WEBAPP_PASSWORD", ds.WebAppPassword ) );

            if ( ds.InetUrl != string.Empty ) props.Add( MakeProperty( "INET_URL", ds.InetUrl ) );
            props.Add( MakeProperty( "INET_USERNAME", ds.InetUserName ) );
            props.Add( MakeProperty( "INET_PASSWORD", ds.InetPassword) );
            props.Add( MakeProperty( "INET_PING_INTERVAL", ds.InetPingInterval.ToString() ) );
            props.Add( MakeProperty( "INET_TIMEOUT_LOW", ds.InetTimeoutLow.ToString() ) );
            props.Add( MakeProperty( "INET_TIMEOUT_MEDIUM", ds.InetTimeoutMedium.ToString() ) );
            props.Add( MakeProperty( "INET_TIMEOUT_HIGH", ds.InetTimeoutHigh.ToString() ) );

            // We do not upload the proxy info as then it could be viewable in iNet which is a security concern.
            //if ( ds.InetProxy != string.Empty ) props.Add( MakeProperty( "INET_PROXY", ds.InetProxy ) );
            //if ( ds.InetProxyUserName != string.Empty ) props.Add( MakeProperty( "INET_PROXY_USERNAME", ds.InetProxyUserName ) );
            //if ( ds.InetProxyPassword != string.Empty ) props.Add( MakeProperty( "INET_PROXY_PASSWORD", ds.InetProxyPassword ) );

            if ( ds.FlashCardInfo != null )
            {
                //props.Add( MakeProperty( "FLASHCARD_MANUFACTURER_ID", ds.FlashCardInfo.ManufacturerID ) );
                //props.Add( MakeProperty( "FLASHCARD_SERIAL_NUMBER", ds.FlashCardInfo.SerialNumber ) );
                props.Add( MakeProperty( "FLASHCARD_TOTAL_SIZE", ds.FlashCardInfo.TotalSize.ToString() ) );
                props.Add( MakeProperty( "FLASHCARD_TOTAL_FREE_SPACE", ds.FlashCardInfo.TotalFreeSpace.ToString() ) );
                props.Add( MakeProperty( "FLASHCARD_AVAIL_FREE_SPACE", ds.FlashCardInfo.AvailableFreeSpace.ToString() ) );
            }

            if ( ds.InetDatabaseTotalSize != DomainModelConstant.NullLong )
                props.Add( MakeProperty( "INET_DB_SIZE", ds.InetDatabaseTotalSize.ToString() ) );

            if ( ds.InetDatabaseUnusedSize != DomainModelConstant.NullLong )
                props.Add( MakeProperty( "INET_DB_UNUSED_SIZE", ds.InetDatabaseUnusedSize.ToString() ) );

            if ( ds.InetQueueDatabaseTotalSize != DomainModelConstant.NullLong )
                props.Add( MakeProperty( "INET_QUEUE_SIZE", ds.InetQueueDatabaseTotalSize.ToString() ) );

            if ( ds.InetQueueDatabaseUnusedSize != DomainModelConstant.NullLong )
                props.Add( MakeProperty( "INET_QUEUE_UNUSED_SIZE", ds.InetQueueDatabaseUnusedSize.ToString() ) );

            // Need to upload the port1 properties as boolean "true"/"false"
            bool freshAir = ( ds.Port1Restrictions & PortRestrictions.FreshAir ) == PortRestrictions.FreshAir;
            props.Add( MakeProperty( "PORT1_ALLOW_FRESH_AIR", freshAir.ToString() ) );
            bool zeroAir = ( ds.Port1Restrictions & PortRestrictions.ZeroAir ) == PortRestrictions.ZeroAir;
            props.Add( MakeProperty( "PORT1_ALLOW_ZERO_AIR", zeroAir.ToString() ) );

            props.Add( MakeProperty( "PURGE_AFTER_BUMP", ds.PurgeAfterBump.ToString() ) );
            props.Add( MakeProperty( "CLEAR_PEAKS_UPON_DOCKING", ds.ClearPeaksUponDocking.ToString() ) ) ;
            props.Add( MakeProperty( "SINGLE_SENSOR_MODE", ds.SingleSensorMode.ToString() ) );
            props.Add( MakeProperty( "PRINT_PERFORMED_BY", ds.PrintPerformedBy.ToString() ) );
            props.Add( MakeProperty( "PRINT_RECEIVED_BY", ds.PrintReceivedBy.ToString() ) );
            props.Add( MakeProperty( "USB_PRINTER", ds.PrinterAttached.ToString() ) );
            props.Add( MakeProperty( "SPAN_RESERVE_THRESHOLD", ds.SpanReserveThreshold.ToString() ) );
            props.Add( MakeProperty( "STOP_ON_FAILED_BUMP_TEST", ds.StopOnFailedBumpTest.ToString()));
            props.Add( MakeProperty( "UPGRADE_ON_ERROR_FAIL", ds.UpgradeOnErrorFail.ToString()));

            wsDockingStation.property = props.ToArray();

            WebServiceLog.LogDOCKING_STATION( wsDockingStation, WSP_LOG_MESSAGE_HEADER );

            return wsDockingStation;
		}

        /// <summary>
        /// Return an array of webservice CYLINDERS representing all
        /// installed cylinders for the instrument.
        /// </summary>
        /// <param name="ds"></param>
        /// <returns></returns>
        private CYLINDER[] MakeCylinders( DockingStation ds )
        {
            List<CYLINDER> wsCylinders = new List<CYLINDER>();
            foreach ( GasEndPoint gep in ds.GasEndPoints )
            {
                CYLINDER wsCyl = MakeCylinder( gep );
                wsCylinders.Add( wsCyl );
            }
            return wsCylinders.ToArray();
        }

        private CYLINDER MakeCylinder( GasEndPoint gasEndPoint )
        {
            CYLINDER wsCyl = new CYLINDER();

            wsCyl.componentCode = COMPONENT_CODE_CYLINDER;
            wsCyl.currentPressure = gasEndPoint.Cylinder.Pressure.ToString();
            wsCyl.igas = gasEndPoint.InstallationType == GasEndPoint.Type.iGas ? 1 : 0;
            wsCyl.manifold = gasEndPoint.InstallationType == GasEndPoint.Type.Manifold;
            wsCyl.position = gasEndPoint.Position;
            wsCyl.sn = gasEndPoint.Cylinder.FactoryId;

            wsCyl.expirationDate = AssignNullableValueDate( _tzi.ToUniversalTime( gasEndPoint.Cylinder.ExpirationDate ) );
            wsCyl.expirationDateSpecified = wsCyl.expirationDate != null;
            wsCyl.installTime = null; // installTime will be assigned by the server
            wsCyl.installTimeSpecified = wsCyl.installTime != null;
            wsCyl.refillDate = AssignNullableValueDate( _tzi.ToUniversalTime( gasEndPoint.Cylinder.RefillDate ) );
            wsCyl.refillDateSpecified = wsCyl.refillDate != null;

            //wsCyl.maufacturedDate = DateTime.MinValue; //TODO: not implemented
            if ( gasEndPoint.Cylinder.IsFreshAir )
            {
                wsCyl.partNumber = FactoryCylinder.FRESH_AIR_PART_NUMBER;
                wsCyl.cylinderCode = FactoryCylinder.FRESH_AIR_PART_NUMBER;
                wsCyl.manufacturerCode = NOT_APPLICABLE_FLAG;
            }
            else
            {
                if ( gasEndPoint.Cylinder.PartNumber != null && gasEndPoint.Cylinder.PartNumber != string.Empty )
                {
                    wsCyl.partNumber = gasEndPoint.Cylinder.PartNumber;
                    wsCyl.cylinderCode = gasEndPoint.Cylinder.PartNumber;
                }
                else
                {
                    wsCyl.partNumber = NON_ISC_GAS_FLAG;
                    wsCyl.cylinderCode = NON_ISC_GAS_FLAG;
                }

                wsCyl.manufacturerCode = gasEndPoint.Cylinder.ManufacturerCode;
                if ( wsCyl.manufacturerCode == string.Empty )
                    wsCyl.manufacturerCode = NOT_APPLICABLE_FLAG;

                wsCyl.uid = gasEndPoint.Cylinder.FactoryId;  
            }

            //// UID
            //// The UID for the cylinder is determined based on cylinder installation type.
            ////     iGas:  iGas cylinders have a unique id, and it's the Serial number.
            ////     Manual:  Manual cylinders may be defined with a serial number; if so, set the UID as that serial number; if not, generate a UID.
            ////     Manifold:  Manifold cylinders will need a generated UID.
            ////
            //// Generated UIDs
            //// We make generated UIDS from the IDS SN and the DS2 cylinder id, plus the part number of the cylinder.  
            //// That should make it close to being unique.  It will not allow us to know when a manually-entered 
            //// cylinder is replaced with the same kind, though.
            //if (wsCyl.igas == 1 || (ic.InstallationType == InstalledCylinder.Type.Manual && ic.Cylinder.FactoryId.Length > 0)) 
            //{
            //    wsCyl.uid = ic.Cylinder.FactoryId;
            //}
            //else
            //{
            //    //wsCyl.uid = string.Format( "{0}_{1}_{2}", Configuration.DockingStation.SerialNumber, wsCyl.partNumber.Replace( " ", "" ), ic.Cylinder.ID );
            //    // Note that it's assumed that in the Viper docking station, ALL cylinders are iGas cylinders.
            //    // Well, except for fresh air.
            //    // We should only ever really encounter fresh air on port 1.  In case in the future
            //    // we allow fresh air on other ports, we always just append the port number to the end of the uid
            //    // to differentiate the different fresh air 'cylinders' from each other.
            //    wsCyl.uid = string.Format( "{0}_{1}_{2}", Configuration.DockingStation.SerialNumber, wsCyl.partNumber.Replace( " ", "" ), ic.Position );
            //}
            if (gasEndPoint.Cylinder.IsFreshAir || (wsCyl.uid == null || wsCyl.uid.Length <= 0)) 
            {
                wsCyl.uid = string.Format("{0}_{1}_{2}", Configuration.DockingStation.SerialNumber, wsCyl.partNumber.Replace(" ", ""), gasEndPoint.Position);
            }


            List<CYLINDER_GAS> wsGases = new List<CYLINDER_GAS>();

            foreach ( GasConcentration gc in gasEndPoint.Cylinder.GasConcentrations )
            {
                CYLINDER_GAS wsCylGas = new CYLINDER_GAS();

                if ( gc.Concentration >= 0 )
                    wsCylGas.concentration = (float)gc.Concentration;

                wsCylGas.gasCode = gc.Type.Code.ToString();

                wsGases.Add( wsCylGas );
            }

            // Cylinder properties
            List<PROPERTY> cylProperties = new List<PROPERTY>();

            if ( gasEndPoint.Cylinder.Volume != int.MinValue )
                cylProperties.Add( MakeProperty( "VOLUME", gasEndPoint.Cylinder.Volume.ToString() ) );

            wsCyl.property = cylProperties.ToArray();

            wsCyl.cylinderGas = wsGases.ToArray();

            return wsCyl;
        }

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="error"></param>
        /// <param name="dockingStationSn"></param>
        /// <returns></returns>
		internal ERROR GetERROR( DockingStationError error, DockingStation dockingStation, DateTime eventTime )
		{
			// Build up the object that needs to be sent to iNet
			ERROR wsError = new ERROR();

            wsError.dsSn = dockingStation.SerialNumber;

            // If error contains an instrument serial number, then the error  
            // is trying to refer to being a problem with the instrument.
            // Otherwise, assume the error is with the docking station.
            if ( error.InstrumentSerialNumber == null || error.InstrumentSerialNumber == string.Empty )
            {
                wsError.sn = dockingStation.SerialNumber;
                wsError.type = EquipmentTypeCode.VDS;
            }
            else
            {
                wsError.sn = error.InstrumentSerialNumber;
                wsError.type = EquipmentTypeCode.Instrument;
            }

            wsError.time = AssignNullableValueDate( eventTime );
            wsError.timeSpecified = true;

			ERROR_DATA wsErrorData = new ERROR_DATA();


            //Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593
            if (error.ErrorCode == null || error.ErrorCode == string.Empty)
                wsErrorData.errorCode = ERROR_CODE_EXCEPTION;
            else
                wsErrorData.errorCode = error.ErrorCode;
            
            //wsErrorData.errorCode = ERROR_CODE_EXCEPTION;  --> SGF: Commenting out for INS-5001 in iNet DS v5.3.9

			wsErrorData.errorDetail = error.Description;

            wsErrorData.errorTime = AssignNullableValueDate( eventTime );
            wsErrorData.errorTimeSpecified = true;

            wsError.errorData = new ERROR_DATA[] { wsErrorData };

            WebServiceLog.LogERROR( wsError, WSP_LOG_MESSAGE_HEADER );

            return wsError;

		}

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="errorDiag"></param>
        /// <param name="dsEvent"></param>
        /// <returns></returns>
		internal ERROR GetERROR( ErrorDiagnostic errorDiag, DateTime eventTimeStamp, InstrumentEvent dsEvent )
		{
			// Build up the object that needs to be sent to iNet
			ERROR wsError = new ERROR();

			List<PROPERTY> propertyList = new List<PROPERTY>(1); 
			if ( errorDiag.Category == ErrorCategory.BaseUnit )
			{
				// error is for the base unit, and we upload the instrument's S/N as a property
				wsError.sn = errorDiag.BaseUnitSerialNumber;
				propertyList.Add( MakeProperty( "INST_SN", dsEvent.DockedInstrument.SerialNumber ) );
			}
			else
			{
				// error is for the instrument, and we upload the base unit's S/N as a property if one is available
				wsError.sn = dsEvent.DockedInstrument.SerialNumber;
				if ( !string.IsNullOrEmpty( errorDiag.BaseUnitSerialNumber ) )
					propertyList.Add( MakeProperty( "BASE_SN", errorDiag.BaseUnitSerialNumber ) );
			}
			if ( propertyList.Count > 0 )
				wsError.properties = propertyList.ToArray();
			
			wsError.type = EquipmentTypeCode.Instrument;

            wsError.time = AssignNullableValueDate( eventTimeStamp );
            wsError.timeSpecified = (wsError.time != null );

			ERROR_DATA wsErrorData = new ERROR_DATA();

			wsErrorData.errorCode = errorDiag.Code.ToString();
			wsErrorData.errorDetail = "An error of type " + errorDiag.Code.ToString() + " was downloaded from this instrument during diagnostics.  \nIt occurred sometime between now and the last diagnostics event.";	// We have no details for the instrument error codes

            wsErrorData.errorTime = AssignNullableValueDate( _tzi.ToUniversalTime( errorDiag.ErrorTime ) );
            wsErrorData.errorTimeSpecified = ( wsErrorData.errorTime != null );

			wsError.errorData = new ERROR_DATA[ 1 ];
			wsError.errorData[ 0 ] = wsErrorData;

            WebServiceLog.LogERROR( wsError, WSP_LOG_MESSAGE_HEADER );

            return wsError;
		}


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ds2Instrument"></param>
        /// <param name="dsEvent"></param>
        /// <returns>The returned array contains a mixture of INSTRUMENT_CALIBRATION and INSTRUMENT_BUMP_TEST objects.</returns>
        public object[] GetGasOperations( InstrumentManualOperationsDownloadEvent instEvent, long? scheduleRefId )
        {
            // The event's array list will contain a mixture of calibrations and bumps.
            // We need to divide those all out to multiple separate lists of just calibrations and
            // just bumps, with each list containing only responses for a specific calibration or bump.
            // We can identify specific cals and bumps by looking at the ManualId property.
            // Sensors that were all calibrated or bumped together will all have the same 
            // ManualId.
            //
            // Dictionary is keyed on ManualId.  Each keyed value is a ArrayList of SensorGasResponses
            // all containing the same ManualId, meaning they were all calibrated (or bumped) together.
            Dictionary<int, List<SensorGasResponse>> sgrDictionary = new Dictionary<int, List<SensorGasResponse>>();

            foreach ( SensorGasResponse sgr in instEvent.GasResponses )
            {
                List<SensorGasResponse> sgrList = null;

                if ( !sgrDictionary.TryGetValue( sgr.ManualOperationId, out sgrList ) )
                {
                    sgrList = new List<SensorGasResponse>();
                    sgrDictionary[sgr.ManualOperationId] = sgrList;
                }

                sgrList.Add( sgr );
            }

            // Contains the list INSTRUMENT_CALIBRATIONs and INSTRUMENT_BUMP_TESTs we'll return at the end.
            List<object> webServiceParameters = new List<object>();

            INSTRUMENT_BUMP_TEST mostRecentManualBumpTest = null;
            INSTRUMENT_CALIBRATION mostRecentManualCalibration = null;

            foreach ( List<SensorGasResponse> sgrList in sgrDictionary.Values )
            {
                if ( sgrList.Count == 0 )
                    continue;

                InstrumentManualOperationsDownloadEvent gasResponseEvent = (InstrumentManualOperationsDownloadEvent)instEvent.Clone();

                gasResponseEvent.GasResponses = sgrList;

                gasResponseEvent.Trigger = instEvent.Trigger;

                gasResponseEvent.DockingStation = instEvent.DockingStation;
                gasResponseEvent.DockedInstrument = instEvent.DockedInstrument;
                gasResponseEvent.GasResponses = sgrList;

                // Depending on the type of operaton of the first response in the list,
                // create either a Bump or Calibration

                SensorGasResponse sgr = gasResponseEvent.GasResponses[ 0 ];

                if (sgr.Type == GasResponseType.Bump)
                {
                    //In this loop last value set to this variable mostRecentManualBumpTest is logically the 
                    //most rececent manual bump test record in the collection of manual gas operation
                    mostRecentManualBumpTest = GetINSTRUMENT_BUMP_TEST(gasResponseEvent, scheduleRefId);
                    webServiceParameters.Add(mostRecentManualBumpTest );
                }
                else if (sgr.Type == GasResponseType.Calibrate)
                {
                    //In this loop last value set to this variable mostRecentManualCalibration is logically the 
                    //most rececent manual cal record in the collection of manual gas operation
                    mostRecentManualCalibration = GetINSTRUMENT_CALIBRATION(gasResponseEvent, scheduleRefId);
                    webServiceParameters.Add(mostRecentManualCalibration);
                }
                else
                {
                    Log.Warning(string.Format("Unknown type ({0}) found in InstrumentManualOperationsDownloadEvent", sgr.Type.ToString()));
                    continue;
                }
            }

            if (mostRecentManualBumpTest != null && mostRecentManualBumpTest.property != null)
            {
               foreach(PROPERTY prop in mostRecentManualBumpTest.property)
               {
                   if (prop.propName == "ISMOSTRECENT")
                       prop.propValue = true.ToString();
               }
            }

            if (mostRecentManualCalibration != null && mostRecentManualCalibration.property != null)
            {
               foreach(PROPERTY prop in mostRecentManualCalibration.property)
               {
                   if (prop.propName == "ISMOSTRECENT")
                       prop.propValue = true.ToString();
               }
            }

            return webServiceParameters.ToArray();
        }

        /// <summary>
        /// The event's UsedGasEndPoints contains cylinders used for purging. We need to find
        /// them and clone/copy them to each of the SensorGasResponse's list of UsedGasEndPoints
        /// because when updating UsedGasEndPoints to iNet, the web services only allow them 
        /// to be attached to the SensorGasResponse. INS-6259, 5/24/2017
        /// </summary>
        /// <param name="dsEvent"></param>
        private void CopyPurgeCylindersToSensorGasResponses( InstrumentGasResponseEvent dsEvent )
        {
            // From the event's list of UsedGasEndPoints, extract all zero air cylinders out it that were used for purging.
            // (Actually, at this time (5/2017), the only cylinders in that list were used for purging.)
            List<UsedGasEndPoint> purgeEndPoints = dsEvent.UsedGasEndPoints.FindAll( u => ( u.Usage == CylinderUsage.Purge ) && u.Cylinder.IsZeroAir );

            if ( purgeEndPoints.Count == 0 )
                return;

            // Take the first cylinder in the list and clone it.
            UsedGasEndPoint mergedPurgeEndPoint = (UsedGasEndPoint)purgeEndPoints[ 0 ].Clone();
            mergedPurgeEndPoint.GasOperationGroup = PURGE_GAS_OPERATION_GROUP;

            // Now, iterate through the rest of the list (note that this for-loop skips the first cylinder!),
            // and for each cylinder, add its Duration to the mergedPurgeEndPoint created above.
            for ( int i = 1; i < purgeEndPoints.Count; i++ )
            {
                mergedPurgeEndPoint.DurationInUse += purgeEndPoints[i].DurationInUse;

                // Note that this routine only supports the usage of a single zero-air cylinder for purging.
                // If, by chance, the has more than on zero-air cylinder attached, and it the DS uses both of them
                // (which would probably only happen if one of the cylinders were to go empty), then this routine
                // doesn't differentiate between the two, and just combines them all.  We at least log when this 
                // happens, by fact that with multiple cylinders, each would have a different Position.
                if ( mergedPurgeEndPoint.Position != purgeEndPoints[i].Position)
                    Log.Warning( string.Format( "MakePurgeCylinders: Position mismatch ({0} vs {1}", mergedPurgeEndPoint.Position, purgeEndPoints[i].Position ) );

                //commented the below out, since FlowRate is never actually set by anything when 
                // the UsedGasEndPoints are originally created.
                //if ( mergedPurgeEndPoint.FlowRate != purgeEndPoints[i].FlowRate )
                //    Log.Warning( string.Format( "MakePurgeCylinders: FlowRate mismatch ({0} vs {1}", mergedPurgeEndPoint.FlowRate, purgeEndPoints[i].FlowRate ) );
            }

            // For each SensorGasResponse, add a cloned copy of the mergedPurgeEndPoint we created above.
            foreach ( SensorGasResponse sgr in dsEvent.GasResponses )
            {
                sgr.UsedGasEndPoints.Add( (UsedGasEndPoint)mergedPurgeEndPoint.Clone() );
            }
        }

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="dsEvent"></param>
        /// <returns></returns>
		internal INSTRUMENT_CALIBRATION GetINSTRUMENT_CALIBRATION( InstrumentGasResponseEvent dsEvent, long? scheduleRefId )
		{
            // This method can be invoked either as a part of calibration event 
            // or a bump test event where O2 high bump failed and calibration was triggered.
            // Check if dsEvent has HasHighBumpFailCalGasResponses set to false.
            // If so, proceed as a normal calibration event. If not, treat this as a bump test event
            // and upload the calibration gas responses in HighBumpFailCalGasResponses list
            bool isHighBumpTestCalibration = dsEvent.HasHighBumpFailCalGasResponses;
            if (!isHighBumpTestCalibration)
            {
                Log.Assert( dsEvent is InstrumentCalibrationEvent || dsEvent is InstrumentManualOperationsDownloadEvent, "Invalid event type" );
                Log.Assert( dsEvent.GasResponses.Count > 0, "InstrumentGasResponseEvent.GasResponses must not be zero." );
            }
            else
            {
                // Since this is called only from InstrumentBumpTestEvent and only when
                // HighBumpFailCalGasResponses holds gas responses, throw exception if this is not the case.
                Log.Assert(dsEvent is InstrumentBumpTestEvent, "Invalid event type");
                Log.Assert(isHighBumpTestCalibration, "InstrumentGasResponseEvent.HighBumpFailCalGasResponses must not be zero.");
            }

			INSTRUMENT_CALIBRATION wsCal = new INSTRUMENT_CALIBRATION();

			wsCal.dsSn = dsEvent.DockingStation.SerialNumber;
			wsCal.sn = dsEvent.DockedInstrument.SerialNumber;
            wsCal.scheduleRefId = scheduleRefId;
            wsCal.scheduleRefIdSpecified = ( scheduleRefId != null );            
            // iNet server team has requested that we only set the eventTime for manual cals / bumps
            // to the time the download occurred. Otherwise, we should just set it to null.
            wsCal.eventTime = ( dsEvent is InstrumentManualOperationsDownloadEvent ) ? AssignNullableValueDate( dsEvent.Time ) : null;
            wsCal.eventTimeSpecified = ( wsCal.eventTime != null );
            wsCal.pass = true; // default to true; we may override it in the for-loop below
            wsCal.passSpecified = (wsCal.pass != null);

            // Trigger and next cal time will only be available if this a calibration event.
            // If isHighBumpTestCalibration is set to false, retrieve and add this information to the wsCal object/
            // Else, set these to defaults.
            if (!isHighBumpTestCalibration)
            {                
                wsCal.trigger = dsEvent is InstrumentManualOperationsDownloadEvent ? TriggerType.Manual.ToString() : dsEvent.Trigger.ToString();
                wsCal.nextCalTime = dsEvent.NextUtcScheduledDate;
            }
            else
            {
                wsCal.trigger = TriggerType.Unscheduled.ToString();                
                wsCal.nextCalTime = null;
            }
            wsCal.trigger = wsCal.trigger.ToUpper();
            wsCal.nextCalTimeSpecified = wsCal.nextCalTime != null;

            int count = !isHighBumpTestCalibration ? dsEvent.GasResponses.Count : dsEvent.HighBumpFailCalGasResponses.Count;
			wsCal.sensorCalibration = new SENSOR_CALIBRATION[ count ];

            if (!isHighBumpTestCalibration)
                CopyPurgeCylindersToSensorGasResponses( dsEvent );

            for ( int i = 0; i < count; i++ )
            {
                SensorGasResponse gasResponse = !isHighBumpTestCalibration ? dsEvent.GasResponses[i] : dsEvent.HighBumpFailCalGasResponses[i];

                SENSOR_CALIBRATION wsSensorCal = new SENSOR_CALIBRATION();

                wsSensorCal.spanReading = AssignNullableValue( gasResponse.Reading );
                wsSensorCal.spanReadingSpecified = ( wsSensorCal.spanReading != null );

                wsSensorCal.baseline = AssignNullableValue( gasResponse.BaseLine );
                wsSensorCal.baselineSpecified = ( wsSensorCal.baseline != null );
                wsSensorCal.duration = AssignNullableValue( gasResponse.Duration );
                wsSensorCal.durationSpecified = ( wsSensorCal.duration != null );
                wsSensorCal.spanCoef = AssignNullableValue( gasResponse.SpanCoef );  // TODO: implement
                wsSensorCal.spanCoefSpecified = ( wsSensorCal.spanCoef != null );
                wsSensorCal.zeroOffset = AssignNullableValue( gasResponse.ZeroOffset );
                wsSensorCal.zeroOffsetSpecified = ( wsSensorCal.zeroOffset != null );
                wsSensorCal.calStatus = gasResponse.Status.ToString();

                // If the HighBumpCalibration flag is set to true and sensor gas response indicates
                // that the sensor skipped calibration, we need to return 'null' for wsSensorCal.pass.
                // In the case that the sensor gas response indicates that the sensor passed zeroing 
                // (and therefore also was NOT subjected to calibration), we need to return 'null' for 
                // wsSensorCal.pass.  This is to allow for proper handling by other components in iNet.
                if (isHighBumpTestCalibration)
                {
                    if (gasResponse.Status != Status.Skipped)
                    {
                        wsSensorCal.pass = gasResponse.Passed;
                        wsSensorCal.passSpecified = (wsSensorCal.pass != null);
                    }
                }
                else if (gasResponse.Status != Status.ZeroPassed)
                {
                    wsSensorCal.pass = gasResponse.Passed;
                    wsSensorCal.passSpecified = (wsSensorCal.pass != null);
                }

                wsSensorCal.uid = gasResponse.Uid;

                if ( gasResponse.Position != DomainModelConstant.NullInt )
                    wsSensorCal.position = gasResponse.Position;

                // Need to provide combustible sensors' correlation factor to iNet. It prints them on cal/bump certificates.
                if ( SensorCode.IsCombustible( gasResponse.SensorCode ) )
                    wsSensorCal.gasDetecting = gasResponse.GasDetected;

                wsSensorCal.time = AssignNullableValueDate( gasResponse.Time );
                wsSensorCal.timeSpecified = ( wsSensorCal.time != null );

                // SGF  22-Jun-2011  INS-1732 -- new values to upload based on requirements of German law, Berufsgenossenschaft Chemie.
                wsSensorCal.readingAfterZero = AssignNullableValue(gasResponse.ReadingAfterZeroing);
                wsSensorCal.readingAfterZeroSpecified = (wsSensorCal.readingAfterZero != null);
                wsSensorCal.timeAfterZero = AssignNullableValueDate(gasResponse.TimeAfterZeroing);
                wsSensorCal.timeAfterZeroSpecified = (wsSensorCal.timeAfterZero != null);
                wsSensorCal.readingAfterPrecondition = AssignNullableValue(gasResponse.ReadingAfterPreconditioning);
                wsSensorCal.readingAfterPreconditionSpecified = (wsSensorCal.readingAfterPrecondition != null);
                wsSensorCal.timeAfterPrecondition = AssignNullableValueDate(gasResponse.TimeAfterPreconditioning);
                wsSensorCal.timeAfterPreconditionSpecified = (wsSensorCal.timeAfterPrecondition != null);
                wsSensorCal.readingAfterPurge = AssignNullableValue(gasResponse.ReadingAfterPurging);
                wsSensorCal.readingAfterPurgeSpecified = (wsSensorCal.readingAfterPurge != null);
                wsSensorCal.timeAfterPurge = AssignNullableValueDate(gasResponse.TimeAfterPurging);
                wsSensorCal.timeAfterPurgeSpecified = (wsSensorCal.timeAfterPurge != null);
                wsSensorCal.cumulativeResponseTime = AssignNullableValue(gasResponse.CumulativeResponseTime);
                wsSensorCal.cumulativeResponseTimeSpecified = (wsSensorCal.cumulativeResponseTime != null);

                // Time of manual events that are downloaded are assumed to be in the docking stations 'local time zone'
                // (as specified in the Configuration.DockingStation.TimeZone).  Need to convert it to UTC before uploading.
                if ( ( dsEvent is InstrumentManualOperationsDownloadEvent ) && ( wsSensorCal.time != null ) )
                    wsSensorCal.time = _tzi.ToUniversalTime( (DateTime)wsSensorCal.time );
            
                if ( wsSensorCal.pass == false ) // Any one failure means the whole thing failed
                    wsCal.pass = false;

                // If the HighBumpCalibration flag is set to true and sensor gas response indicates
                // that the sensor did not skip calibration, populate the used cylinder information.
                if (isHighBumpTestCalibration)
                {
                    if (gasResponse.Status != Status.Skipped)
                        wsSensorCal.cylinderUsed = MakeGasOperationCylinders(dsEvent, gasResponse, CYLINDER_PURPOSE_CALIBRATION);
                }
                else
                    wsSensorCal.cylinderUsed = MakeGasOperationCylinders(dsEvent, gasResponse, CYLINDER_PURPOSE_CALIBRATION);

                wsCal.sensorCalibration[i] = wsSensorCal;
            }

            // For manual calibrations, the 'time' field should contain the time the manual cal occurred on the instrument.
            // We can just use the time of the first sensor that was calibrated for that.
            // For automated calibrations, we just assign the dsEvent's time, which is the time the docking station
            // started the calibration event.
            if ( ( dsEvent is InstrumentManualOperationsDownloadEvent ) && ( wsCal.sensorCalibration.Length > 0 ) )
                wsCal.time = AssignNullableValueDate( (DateTime)wsCal.sensorCalibration[0].time );
            else
                wsCal.time = AssignNullableValueDate( dsEvent.Time );

            wsCal.timeSpecified = (wsCal.time != null);

            if (dsEvent is InstrumentManualOperationsDownloadEvent)
            {
                //By default add this property with the value of false, we will change this value later based 
                //on whether this cal is last in the collection of manual calibration records
                List<PROPERTY> propertyList = new List<PROPERTY>();
                propertyList.Add(MakeProperty("ISMOSTRECENT", false.ToString()));
                wsCal.property = propertyList.ToArray();
            }

            WebServiceLog.LogINSTRUMENT_CALIBRATION( wsCal, WSP_LOG_MESSAGE_HEADER );

            return wsCal;
        }

        internal DATALOG_SESSION GetDATALOG_SESSION( DatalogSession dsSession, DateTime timeStamp, long? scheduleRefId, long? ticks )
		{
			DATALOG_SESSION wsSession = new DATALOG_SESSION();

			// Instrument Session
            wsSession.dsSn = Configuration.DockingStation.SerialNumber;
            wsSession.sn = dsSession.SerialNumber;
            wsSession.scheduleRefId = scheduleRefId;
            wsSession.scheduleRefIdSpecified = ( scheduleRefId != null );
            wsSession.eventTime = timeStamp;
            wsSession.eventTimeSpecified = ( wsSession.eventTime != null );
			wsSession.user = dsSession.User;
			wsSession.comments = dsSession.Comments;
			wsSession.recordingInterval = AssignNullableValue( dsSession.RecordingInterval );
            wsSession.recordingIntervalSpecified = wsSession.recordingInterval != null;

            wsSession.sessionDate = AssignNullableValueDate( _tzi.ToUniversalTime( dsSession.Session ) );
            wsSession.sessionDateSpecified = wsSession.sessionDate != null;
			wsSession.sessionNum = AssignNullableValue( dsSession.SessionNumber );
            wsSession.sessionNumSpecified = wsSession.sessionNum != null;
			wsSession.twaTimeBase = AssignNullableValue( dsSession.TWATimeBase );
            wsSession.twaTimeBaseSpecified = wsSession.twaTimeBase != null;
            
			wsSession.sensorSession = new DATALOG_SENSOR_SESSION[ dsSession.SensorSessions.Count ];
			long ssNumber = 0;

			foreach( DatalogSensorSession sensorSession in dsSession.SensorSessions )
			{
				DATALOG_SENSOR_SESSION wsSensorSession = new DATALOG_SENSOR_SESSION();

				wsSensorSession.gasCode = sensorSession.Gas.Code;
				wsSensorSession.uid = sensorSession.Uid;

				wsSensorSession.alarmHigh = AssignNullableValue( sensorSession.AlarmHigh );
                wsSensorSession.alarmHighSpecified = wsSensorSession.alarmHigh != null;
				wsSensorSession.alarmLow = AssignNullableValue( sensorSession.AlarmLow );
                wsSensorSession.alarmLowSpecified = wsSensorSession.alarmLow != null;
				wsSensorSession.alarmSTEL = AssignNullableValue( sensorSession.AlarmSTEL );
                wsSensorSession.alarmSTELSpecified = wsSensorSession.alarmSTEL != null;
				wsSensorSession.alarmTWA = AssignNullableValue( sensorSession.AlarmTWA );
                wsSensorSession.alarmTWASpecified = wsSensorSession.alarmTWA != null;
				wsSensorSession.exposureSD = AssignNullableValue( sensorSession.ExposureSD );
                wsSensorSession.exposureSDSpecified = wsSensorSession.exposureSD != null;

                wsSensorSession.responseFactorValue = AssignNullableValue( sensorSession.ResponseFactor.Value );
                wsSensorSession.responseFactorValueSpecified = wsSensorSession.responseFactorValue != null;
                wsSensorSession.responseFactorName = sensorSession.ResponseFactor.Name;

                wsSensorSession.sensorCode = sensorSession.Type.Code;
                
                wsSensorSession.status = sensorSession.Status.ToString();

				wsSensorSession.readingPeriod = new DATALOG_PERIOD[ sensorSession.ReadingPeriods.Count ];
				long periodNumber = 0;

				foreach( DatalogPeriod period in sensorSession.ReadingPeriods )
				{
					DATALOG_PERIOD wsPeriod = new DATALOG_PERIOD();

					wsPeriod.period = AssignNullableValue( period.Period );
                    wsPeriod.periodSpecified = wsPeriod.period != null;
                    wsPeriod.time = AssignNullableValueDate( _tzi.ToUniversalTime( period.Time ) );
                    wsPeriod.timeSpecified = wsPeriod.time != null;
					wsPeriod.location = period.Location;

					wsPeriod.reading = new DATALOG_READING[ period.Readings.Count ];

                    for ( int i = 0; i < period.Readings.Count; i++ )
					{
                        DatalogReading reading = (DatalogReading)period.Readings[ i ];

						DATALOG_READING wsReading = new DATALOG_READING();

                        wsReading.sequence = AssignNullableValue( i + 1  /*one-based*/ );
                        wsReading.sequenceSpecified = wsReading.sequence != null;
						wsReading.rawReading = AssignNullableValue( reading.Reading );
                        wsReading.rawReadingSpecified = wsReading.rawReading != null;
						wsReading.temperature = AssignNullableValue( reading.Temperature );
                        wsReading.temperatureSpecified = wsReading.temperature != null;
                        wsReading.count = AssignNullableValue( reading.Count );
                        wsReading.countSpecified = wsReading.count != null;

						wsPeriod.reading[ i ] = wsReading;
					}

					wsSensorSession.readingPeriod[ periodNumber ] = wsPeriod;
					periodNumber++;
				}
				wsSession.sensorSession[ ssNumber ] = wsSensorSession;
				ssNumber++;
			}

			List<PROPERTY> properties = new List<PROPERTY>();

            if ( ticks != null )
				properties.Add( MakeProperty( "TICKS", ticks.ToString() ) );

			if ( dsSession.BaseUnitSerialNumber != string.Empty )
				properties.Add( MakeProperty( "BASE_SN", dsSession.BaseUnitSerialNumber ) );

			if ( properties.Count > 0 )
				wsSession.properties = properties.ToArray();            

            WebServiceLog.LogDATALOG_SESSION( wsSession, WSP_LOG_MESSAGE_HEADER );

            return wsSession;
		}

        internal ALARM_EVENT GetALARM_EVENT( AlarmEvent alarmEvent, DateTime eventTimeStamp, long? scheduleRefId )
        {
            ALARM_EVENT wsAlarmEvent = new ALARM_EVENT();

            wsAlarmEvent.alarmLow = (float)alarmEvent.AlarmLow;
            wsAlarmEvent.alarmHigh = (float)alarmEvent.AlarmHigh;
            wsAlarmEvent.dsSn = Configuration.DockingStation.SerialNumber;
            wsAlarmEvent.duration = alarmEvent.Duration;
            wsAlarmEvent.eventTime = eventTimeStamp;
            wsAlarmEvent.gasCode = alarmEvent.GasCode;
            wsAlarmEvent.peakReading = (float)alarmEvent.PeakReading;
            wsAlarmEvent.sensorUid = alarmEvent.SensorUid;
            wsAlarmEvent.site = alarmEvent.Site;
            wsAlarmEvent.user = alarmEvent.User;
            // Time of the alarm event that was downloaded is assumed to be in the docking stations 'local time zone'
            // (as specified in the Configuration.DockingStation.TimeZone).  Need to convert it to UTC before uploading.
            wsAlarmEvent.time = _tzi.ToUniversalTime( alarmEvent.Timestamp );
            wsAlarmEvent.sn = alarmEvent.InstrumentSerialNumber;
            wsAlarmEvent.scheduleRefId = scheduleRefId;
            wsAlarmEvent.scheduleRefIdSpecified = ( scheduleRefId != null );

            List<PROPERTY> properties = new List<PROPERTY>();

            if ( alarmEvent.SpeakerVoltage != DomainModelConstant.NullInt )
                properties.Add( MakeProperty( "SPEAKER_VOLTAGE", alarmEvent.SpeakerVoltage.ToString() ) );

            if ( alarmEvent.VibratingMotorVoltage != DomainModelConstant.NullInt )
                properties.Add( MakeProperty( "VIBRATING_MOTOR_VOLTAGE", alarmEvent.VibratingMotorVoltage.ToString() ) );
            
            if ( alarmEvent.IsDocked != null )
                properties.Add( MakeProperty( "DOCKED", alarmEvent.IsDocked.ToString() ) );
            
            if ( alarmEvent.AlarmOperatingMode != AlarmOperatingMode.Undefined )
                properties.Add( MakeProperty( "ALARM_OPERATING_MODE", alarmEvent.AlarmOperatingMode.ToString() ) );

            // INS-2593, 10/31/2012 - Need to upload ticks to server so that server can determine if alarm
            // event is a duplicate or not. This is due to alarm event timestamps not being accurate on GBPlus.
            if ( Configuration.DockingStation.Type == DeviceType.GBPLS )
                properties.Add( MakeProperty( "TICKS", alarmEvent.Ticks.ToString() ) );

			if ( alarmEvent.BaseUnitSerialNumber != string.Empty )
				properties.Add( MakeProperty( "BASE_SN", alarmEvent.BaseUnitSerialNumber ) );

            //INS-8330 (INS-8624) Upload datalog proximity alarms in eventlog to iNet
            if (alarmEvent.GasCode == GasCode.PROXIMITY)
                properties.Add(MakeProperty("USER_ACCESS_LEVEL", alarmEvent.UserAccessLevel.ToString()));

            //INS-8330 (INS-8624) Upload datalog proximity alarms in eventlog to iNet
            if (alarmEvent.GasCode == GasCode.PROXIMITY)
                properties.Add(MakeProperty("SITE_ACCESS_LEVEL", alarmEvent.SiteAccessLevel.ToString()));

            wsAlarmEvent.properties = properties.ToArray();

            WebServiceLog.LogALARMEVENT( wsAlarmEvent, WSP_LOG_MESSAGE_HEADER );

            return wsAlarmEvent;
        }

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="dsAlarmEvent"></param>
        /// <param name="dsEvent"></param>
        /// <returns></returns>
        internal DATALOG_SESSION GetGbPlusDATALOG_SESSION( AlarmEvent alarmEvent, DateTime timeStamp )
		{
            DatalogSession session = CreateInstrumentSession( alarmEvent );
            return GetDATALOG_SESSION( session, timeStamp, null, alarmEvent.Ticks );
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alarmEvent"></param>
        /// <returns></returns>
        private DatalogSession CreateInstrumentSession( AlarmEvent alarmEvent )
        {
            // SGF Dec-26-07 DSZ-1539

            // The following properties are currently (as of Dec-26-07) found in the AlarmEvent object.
            //     string InstrumentSerialNumber
            //     string SensorSerialNumber
            //     DateTime Timestamp
            //     int Duration
            //     double PeakReading
            //     double AlarmHigh
            //     double AlarmLow
            //     string GasCode
            //     string User (not presented in the adapter)
            //     string Site (not presented in the adapter)
            //     int Ticks (not presented in the adapter)

            // INSTRUMENT SESSION
            //
            // Create a new DatalogSession object (and associated component objects) 
            // to serve as an adapter for the AlarmEvent object.
            DatalogSession datalogSession = new DatalogSession();

            // The following DatalogSession properties are set based on property values in the AlarmEvent object.
            datalogSession.SerialNumber = alarmEvent.InstrumentSerialNumber;
            datalogSession.RecordingInterval = 1; // assume 1 second
            datalogSession.Session = alarmEvent.Timestamp;

            // The following DatalogSession properties are considered to be empty or undefined.
            datalogSession.User = string.Empty;
            datalogSession.Comments = string.Empty;
            datalogSession.SessionNumber = 1; // Changed based on feedback from Russ Timko and Kavita Iyengar Dec-28-07
            //datalogSession.TWATimeBase = 8; // Changed based on feedback from Russ Timko and Kavita Iyengar Dec-28-07
            datalogSession.TWATimeBase = int.MinValue; // Changed reversed based on feedback from Jon Pearsall Jan-03-08

            // SENSOR SESSION
            //
            // Add a DatalogSensorSession object to the list of SensorSessions in the DatalogSession object.

            string sensorCode = "S" + alarmEvent.GasCode.Substring(1);
            DatalogSensorSession sensorSession = new DatalogSensorSession( alarmEvent.SensorSerialNumber, new ComponentType(sensorCode) );
            datalogSession.SensorSessions.Add(sensorSession);

            // The following DatalogSensorSession properties are set based on property values in the AlarmEvent object.
            sensorSession.AlarmHigh = alarmEvent.AlarmHigh;
            sensorSession.AlarmLow = alarmEvent.AlarmLow;
            sensorSession.Gas = GasType.Cache[ alarmEvent.GasCode ];
            sensorSession.Status = SensorStatuses.OK; // Removed conditional assignments based on feedback from Russ Timko Dec-28-07

            // The following DatalogSensorSession properties are considered to be empty or undefined.
            //sensorSession.AlarmSTEL = defaultSTEL; // Temporarily changed based on feedback from Russ Timko Dec-28-07
            sensorSession.AlarmSTEL = double.MinValue; // Changed reversed based on feedback from Jon Pearsall Jan-03-08
            //sensorSession.AlarmTWA = defaultTWA; // Temporarily changed based on feedback from Russ Timko Dec-28-07
            sensorSession.AlarmTWA = double.MinValue; // Changed reversed based on feedback from Jon Pearsall Jan-03-08
            sensorSession.ExposureSD = double.MinValue;
            sensorSession.ResponseFactor = null;

            // SENSOR READING PERIOD
            //
            // Add a DatalogPeriod object to the list of ReadingPeriods in the DatalogSensorSession object.
            DatalogPeriod datalogPeriod = new DatalogPeriod();
            sensorSession.ReadingPeriods.Add(datalogPeriod);

            // The following DatalogPeriod properties are set based on property values in the AlarmEvent object.
            datalogPeriod.Period = 1;
            datalogPeriod.Time = alarmEvent.Timestamp;

            // The following DatalogPeriod properties are considered to be empty or undefined.
            datalogPeriod.Location = string.Empty;

            // SENSOR READING
            //
            // Add a DatalogReading object to the list of Readings in the DatalogPeriod object.
            DatalogReading datalogReading = new DatalogReading();
            datalogPeriod.Readings.Add(datalogReading);

            // The following DatalogReading properties are set based on property values in the AlarmEvent object.
            datalogReading.Reading = (float)alarmEvent.PeakReading;
            datalogReading.Count = alarmEvent.Duration;

            // The following DatalogReading properties are considered to be empty or undefined.
            datalogReading.Temperature = short.MinValue;

            // Return the newly-created DatalogSession object.
            return datalogSession;
        }


        /// <summary>
        /// ???
        /// </summary>
        /// <param name="dsEvent"></param>
        /// <returns></returns>
		internal INSTRUMENT_BUMP_TEST GetINSTRUMENT_BUMP_TEST( InstrumentBumpTestEvent dsEvent, long? scheduleRefId )
		{
            Log.Assert( dsEvent.GasResponses.Count > 0, "InstrumentGasResponseEvent.GasResponses must not be zero." );

			INSTRUMENT_BUMP_TEST wsBump = new INSTRUMENT_BUMP_TEST();

			wsBump.dsSn = dsEvent.DockingStation.SerialNumber;
            wsBump.sn = dsEvent.DockedInstrument.SerialNumber;
            wsBump.scheduleRefId = scheduleRefId;
            wsBump.scheduleRefIdSpecified = ( scheduleRefId != null );
            wsBump.eventTime = null; // iNet server team has requested that we always set the eventTime for automatic cals / bumps to null.
            wsBump.eventTimeSpecified = false;
            wsBump.trigger = dsEvent.Trigger.ToString().ToUpper();
            wsBump.pass = true; // default to true; we may override it in the for-loop below.
            wsBump.passSpecified = true;
            wsBump.nextBumpTime = dsEvent.NextUtcScheduledDate;
            wsBump.nextBumpTimeSpecified = wsBump.nextBumpTime != null;

			wsBump.sensorBumpTest = new SENSOR_BUMP_TEST[ dsEvent.GasResponses.Count ];

            CopyPurgeCylindersToSensorGasResponses( dsEvent );

            for ( int i = 0; i < dsEvent.GasResponses.Count; i++ )
            {
                wsBump.sensorBumpTest[i] = GetSENSOR_BUMP_TEST( dsEvent.GasResponses[i], dsEvent );
                if ( wsBump.sensorBumpTest[i].pass == false ) // Any one failure means the whole thing failed
                    wsBump.pass = false;
            }

            List<PROPERTY> propertyList = new List<PROPERTY>();
            propertyList.Add( MakeProperty( "BUMP_THRESHOLD", dsEvent.DockedInstrument.BumpThreshold.ToString() ) );
            propertyList.Add( MakeProperty( "BUMP_TIMEOUT", dsEvent.DockedInstrument.BumpTimeout.ToString() ) );
            propertyList.Add( MakeProperty( "ACCESSORY_PUMP", dsEvent.DockedInstrument.AccessoryPump.ToString().ToUpper() ) );

            wsBump.property = propertyList.ToArray();

            wsBump.time = AssignNullableValueDate( dsEvent.Time );
            wsBump.timeSpecified = ( wsBump.time != null );

            WebServiceLog.LogINSTRUMENT_BUMP_TEST( wsBump, WSP_LOG_MESSAGE_HEADER );

            return wsBump;
		}

        private INSTRUMENT_BUMP_TEST GetINSTRUMENT_BUMP_TEST( InstrumentManualOperationsDownloadEvent dsEvent, long? scheduleRefId )
        {
            Log.Assert( dsEvent.GasResponses.Count > 0, "InstrumentGasResponseEvent.GasResponses must not be zero." );

            INSTRUMENT_BUMP_TEST wsBump = new INSTRUMENT_BUMP_TEST();

            wsBump.dsSn = dsEvent.DockingStation.SerialNumber;
            wsBump.sn = dsEvent.DockedInstrument.SerialNumber;
            wsBump.scheduleRefId = scheduleRefId;
            wsBump.scheduleRefIdSpecified = ( scheduleRefId != null );
            // iNet server team has requested that we always set the eventTime for manual
            // cals / bumps to the time that the download occurred.
            wsBump.eventTime = AssignNullableValueDate( dsEvent.Time );
            wsBump.eventTimeSpecified = ( wsBump.eventTime != null );
            wsBump.trigger = TriggerType.Manual.ToString().ToUpper();  // We always set the trigger type to 'Manual' for manual gas operations.
            wsBump.pass = true; // default to true; we may override it in the for-loop below.
            wsBump.passSpecified = true;

            wsBump.sensorBumpTest = new SENSOR_BUMP_TEST[dsEvent.GasResponses.Count];
            for ( int i = 0; i < dsEvent.GasResponses.Count; i++ )
            {
                wsBump.sensorBumpTest[i] = GetSENSOR_BUMP_TEST( dsEvent.GasResponses[i], dsEvent );

                // Time of the manual event that was downloaded is assumed to be in the docking stations 'local time zone'
                // (as specified in the Configuration.DockingStation.TimeZone).  Need to convert it to UTC before uploading.
                if ( wsBump.sensorBumpTest[i].time != null )
                    wsBump.sensorBumpTest[i].time = _tzi.ToUniversalTime( (DateTime)wsBump.sensorBumpTest[i].time );

                if ( wsBump.sensorBumpTest[i].pass == false ) // Any one failure means the whole thing failed
                    wsBump.pass = false;
            }

            if ( wsBump.sensorBumpTest.Length > 0 ) // will probably never be zero, but we check just in case.
            {
                SensorGasResponse sgr = dsEvent.GasResponses[0];

                List<PROPERTY> propertyList = new List<PROPERTY>();
                propertyList.Add( MakeProperty( "BUMP_THRESHOLD", sgr.Threshold.ToString() ) );
                propertyList.Add( MakeProperty( "BUMP_TIMEOUT", sgr.Timeout.ToString() ) );
                propertyList.Add( MakeProperty( "ACCESSORY_PUMP", sgr.AccessoryPump.ToString() ) );

                //By default add this property with the value of false, we will change this value later based 
                //on whether this bump test is last in the collection of manual bump test 
                propertyList.Add( MakeProperty( "ISMOSTRECENT", false.ToString()));
                wsBump.property = propertyList.ToArray();

                // 'time' field should contain the time the manual bump occurred on the instrument.
                // We can just use the time of the first sensor that was bumped for that.
                wsBump.time = wsBump.sensorBumpTest[0].time;
                wsBump.timeSpecified = ( wsBump.time != null );
            }
            else
            {
                wsBump.time = AssignNullableValueDate( dsEvent.Time );
                wsBump.timeSpecified = ( wsBump.time != null );
            }

            WebServiceLog.LogINSTRUMENT_BUMP_TEST( wsBump, WSP_LOG_MESSAGE_HEADER );

            return wsBump;
        }

        private SENSOR_BUMP_TEST GetSENSOR_BUMP_TEST( SensorGasResponse gasResponse, InstrumentGasResponseEvent dsEvent )
        {
            SENSOR_BUMP_TEST wsSensorBump = new SENSOR_BUMP_TEST();

            wsSensorBump.duration = AssignNullableValue( gasResponse.Duration );
            wsSensorBump.durationSpecified = ( wsSensorBump.duration != null );
            wsSensorBump.time = AssignNullableValueDate( gasResponse.Time );
            wsSensorBump.timeSpecified = ( wsSensorBump.time != null );

            //Suresh 19-APR-2012 INS-4537 (DEV)
            // In the case that the sensor gas response indicates that the sensor skipped the bump test due to 
            // bump test disabled for the sensor, we need to return 'null' for wsSensorCal.pass. This is to allow 
            // for proper handling by other components in iNet.
            if (gasResponse.Status != Status.Skipped)
                wsSensorBump.pass = gasResponse.Passed;

            wsSensorBump.passSpecified = ( wsSensorBump.pass != null );
            wsSensorBump.bumpStatus = gasResponse.Status.ToString(); // SGF  08-Jun-2011  INS-1734
            wsSensorBump.uid = gasResponse.Uid;
            if ( gasResponse.Position != DomainModelConstant.NullInt )
                wsSensorBump.position = gasResponse.Position;
            // Need to provide combustible sensors' correlation factor to iNet. It prints them on cal/bump certificates.
            if ( SensorCode.IsCombustible( gasResponse.SensorCode ) )
                wsSensorBump.gasDetecting = gasResponse.GasDetected;
            
            // Cast to decimal then to float is to prevent rounding errors 
            // that occur when trying to assign directly from a double to a float.
            wsSensorBump.reading = (float)(decimal)gasResponse.Reading;

            if(gasResponse.SensorCode == SensorCode.O2)
                wsSensorBump.highReading = (float)(decimal)gasResponse.O2HighReading;

            // SGF  22-Jun-2011  INS-1732 -- new values to upload based on requirements of German law, Berufsgenossenschaft Chemie.
            wsSensorBump.cumulativeResponseTime = AssignNullableValue(gasResponse.CumulativeResponseTime);
            wsSensorBump.cumulativeResponseTimeSpecified = (wsSensorBump.cumulativeResponseTime != null);

            wsSensorBump.cylinderUsed = MakeGasOperationCylinders( dsEvent, gasResponse, CYLINDER_PURPOSE_BUMP );

            return wsSensorBump;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gasResponse"></param>
        /// <returns></returns>
        internal CYLINDER_USED[] MakeGasOperationCylinders( InstrumentGasResponseEvent gasEvent, SensorGasResponse gasResponse, string defaultPurpose )
        {
            List<CYLINDER_USED> cylindersUsed = new List<CYLINDER_USED>( gasResponse.UsedGasEndPoints.Count );

            // CylindersUsed.Count will be zero for manual cal/bump.
            // We still need to upload a 'fake cylinder' to iNet since that's the only way it can
            // be told what type/concentration gas gas was used in the manual cal/bump.
			if ( gasResponse.UsedGasEndPoints.Count < 1 )
			{
				CYLINDER_USED cyl = new CYLINDER_USED();

				cyl.purpose = defaultPurpose;
				cyl.uid = NOT_APPLICABLE_FLAG;
				cyl.gasCode = gasResponse.GasConcentration.Type.Code;

				cyl.secondsOn = AssignNullableValue( int.MinValue );
                cyl.secondsOnSpecified = (cyl.secondsOn != null);
				cyl.concentration = AssignNullableValue( gasResponse.GasConcentration.Concentration );
                cyl.concentrationSpecified = (cyl.concentration != null);

                cyl.gasOperationGroup = null;

				cylindersUsed.Add( cyl );
			}
			else
			{
				// IDS will return ALL cylinders used even if multiple were used for a given operation.
				// i.e., if the IDS used three cylinders to calibrate (because the calibration failed 
				// on the first two), then all three are returned by the IDS in the arraylist, each with
				// it's own Duration.  Order in the arraylist is first used, to last (most recent) used.
				//
				// iNet is only interested (at least at the moment) in the final cylinder used.
				// Therefore, we loop backwards through the array to only look at the most recent 
				// cylinder for any given usage, ignoring cylinders in the ArrayList that were
				// used earlier for the same usage.

                Dictionary<CylinderUsage, UsedGasEndPoint> usages = new Dictionary<CylinderUsage,UsedGasEndPoint>();

                for ( int cylIdx = gasResponse.UsedGasEndPoints.Count - 1; cylIdx >= 0; cylIdx-- )
				{
                    UsedGasEndPoint used = gasResponse.UsedGasEndPoints[ cylIdx ] as UsedGasEndPoint;

					// If we've already seen a cylinder for this usage, then this cylinder
					// was a failed attempt.  iNet isn't interested in it, so ignore it.
					if ( usages.ContainsKey( used.Usage ) )
						continue;

					// Keep track of which usages we've seen so far.
					usages[ used.Usage ] = used;

					CYLINDER_USED wsCylinderUsed = new CYLINDER_USED();

					wsCylinderUsed.purpose = used.Usage.ToString().ToUpper();

					// UID: Only iGas cylinders have a unique id, and it's the Serial number.
					// Non-igas cylinders need a "fake" UID, which we make from the IDS Sn
					// and the DS2 cylinder id, plus the part number of the cylinder.  
					// That should make it close to being unique.  It will not allow us
					// to know when a manually-entered cylinder is replaced with the same 
					// kind, though.

                    if ( used.Cylinder.FactoryId == string.Empty && used.Cylinder.IsFreshAir == false )
					{
                        // FactoryId would would only be empty for non-iGas.
                        // Viper doesn't support non-iGas.
                        continue; 

						// Note: If this gas came from another IDS in a cluster,
						// we do not know that and this IDS serial number will
						// not match what iNet has.  That cannot be helped and this
						// is still the best guess we can make. See INS-376.
						//                        string partNumber = useGasEndPoint.Cylinder.IsFreshAir ? FactoryCylinder.FRESH_AIR_PART_NUMBER : useGasEndPoint.Cylinder.PartNumber;
//                        partNumber = partNumber.Replace( " ", string.Empty );

//                        #warning CYLINDER_USED.uid - how would we make this unique for Viper?
						//                        wsCylinderUsed.uid = string.Format( "{0}_{1}_{2}", gasEvent.DockingStation.SerialNumber, partNumber, "TODO"/*TODO useGasEndPoint.Cylinder.ID.ToString()*/ );
					}
                    else if ( used.Cylinder.FactoryId == string.Empty && used.Cylinder.IsFreshAir == true )
                    {
                        // We need to provide a unique id for the FRESH AIR that has been used.  The
                        // formula we have settled on for now is the concatenation of the docking 
                        // station SN, the "part number"--which is actually the words FRESH AIR, and
                        // the port number, all separated by underscore characters.
                        wsCylinderUsed.uid = string.Format( "{0}_{1}_{2}", gasEvent.DockingStation, used.Cylinder.PartNumber.Replace( " ", "" ), used.Position );
                    }
                    else
                    {
                        wsCylinderUsed.uid = used.Cylinder.FactoryId;
                    }

					// rgilmore 14Feb2006 DSZ-1075
					// Here we try to send the gasCode of the gas used for this
					// operation.  Problem: the IDS returns the cylinder used.
					// It may contain more than one gas.  For all usages except
					// zeroing, we can assume that the cal gas on the sensor
					// is the gas that was used.  
					if ( used.Usage != CylinderUsage.Zero
                    &&   used.Usage != CylinderUsage.PreZero
                    &&   used.Usage != CylinderUsage.Purge
					&&   used.Usage != CylinderUsage.BumpHigh )
					{
						wsCylinderUsed.gasCode = gasResponse.GasConcentration.Type.Code;
						wsCylinderUsed.concentration = AssignNullableValue( gasResponse.GasConcentration.Concentration );
                        wsCylinderUsed.concentrationSpecified = (wsCylinderUsed.concentration != null);
                    }
					else
					{
						// For zeroing and O2 bump high, we can check if the cylinder 
						// contains only one kind of gas, hopefully fresh air or zero air, 
						// and use that.
						if ( used.Cylinder.GasConcentrations.Count == 1 )
						{
                            GasConcentration gc = used.Cylinder.GasConcentrations[ 0 ];
							wsCylinderUsed.gasCode = gc.Type.Code;

							// Fresh Air will not upload a concentration
							if ( !used.Cylinder.IsFreshAir)
							{
								wsCylinderUsed.concentration = AssignNullableValue( gc.Concentration );
								wsCylinderUsed.concentrationSpecified = ( wsCylinderUsed.concentration != null );
							}
							else
							{
								wsCylinderUsed.concentration = null;
								wsCylinderUsed.concentrationSpecified = false;
							}
						}
						else
						{
							// In this case, the cylinder contains more than one gas
							// this is all we can do.
							wsCylinderUsed.gasCode = "MULTIPLE";
                            wsCylinderUsed.concentration = null;
                            wsCylinderUsed.concentrationSpecified = (wsCylinderUsed.concentration != null);
                        }
					}

					wsCylinderUsed.secondsOn = AssignNullableValue( used.DurationInUse );
                    wsCylinderUsed.secondsOnSpecified = (wsCylinderUsed.secondsOn != null);

                    wsCylinderUsed.gasOperationGroup = null;
                    if (used.GasOperationGroup >= 0)
                        wsCylinderUsed.gasOperationGroup = used.GasOperationGroup;

					//if ( useGasEndPoint.VolumeUsed != int.MinValue )
                    //{
					//    PROPERTY prop = MakeProperty( "VOLUME_USED", useGasEndPoint.ToString() );
                    //}

					cylindersUsed.Add( wsCylinderUsed );
				}
			}

            return cylindersUsed.ToArray();
        }

        /// <summary>
        /// Uploads a DiagnosticEvent or InstrumentDiagnosticEvent
        /// </summary>
        /// <param name="diagnosticEvent"></param>
        /// <returns></returns>
		internal DIAGNOSTIC GetDIAGNOSTIC( IDiagnosticEvent diagnosticEvent, long? scheduleRefId )
		{
            DIAGNOSTIC wsDiag = new DIAGNOSTIC(); // Create the DIAG object to pass to iNet

			// Get the basic properties from the parent class
			if ( diagnosticEvent is InstrumentDiagnosticEvent )
			{
				InstrumentDiagnosticEvent instDiagEvent = (InstrumentDiagnosticEvent)diagnosticEvent;
                wsDiag.dsSn = instDiagEvent.DockingStation.SerialNumber;
				wsDiag.sn = instDiagEvent.DockedInstrument.SerialNumber;
                wsDiag.type = EquipmentTypeCode.Instrument;
                wsDiag.time = AssignNullableValueDate( instDiagEvent.Time );
			}
			else
			{
				DiagnosticEvent dsDiagEvent = (DiagnosticEvent)diagnosticEvent;
				wsDiag.dsSn = wsDiag.sn = dsDiagEvent.DockingStation.SerialNumber;
                wsDiag.type = EquipmentTypeCode.VDS;
                wsDiag.time = AssignNullableValueDate( dsDiagEvent.Time );
			}
            wsDiag.timeSpecified = ( wsDiag.time == null ) ? false : true;
            wsDiag.scheduleRefId = scheduleRefId;
            wsDiag.scheduleRefIdSpecified = ( scheduleRefId != null );

            List<DIAGNOSTIC_DATA> wsDiagData = new List<DIAGNOSTIC_DATA>();

            foreach ( Diagnostic diag in diagnosticEvent.Diagnostics )
			{
				if ( diag is GeneralDiagnostic )
					AddGeneralDiagData( wsDiagData, (GeneralDiagnostic)diag );
			}

			wsDiag.diagnosticData = wsDiagData.ToArray();

            WebServiceLog.LogDIAGNOSTIC( wsDiag, WSP_LOG_MESSAGE_HEADER );

            return wsDiag;
		}

        /// <summary>
        /// ???
        /// </summary>
        /// <param name="wsDiagData"></param>
        /// <param name="generalDiagnostic"></param>
        private void AddGeneralDiagData( List<DIAGNOSTIC_DATA> wsDiagData, GeneralDiagnostic generalDiagnostic )
		{
            foreach ( GeneralDiagnosticProperty property in generalDiagnostic.Items )
			{
				DIAGNOSTIC_DATA wsData = new DIAGNOSTIC_DATA();

				wsData.diagName = property.Name;
				wsData.diagValue = property.Value;

				wsDiagData.Add( wsData );
			}
		}

        /// <summary>
        /// Converts the passed in DateTime from Eastern time zone to UTC time zone.
        /// </summary>
        /// <param name="dateTime">Assumed to be Eastern</param>
        /// <returns></returns>
        DateTime EasternToUtc( DateTime dateTime )
        {
            return TimeZoneInfo.Convert( TimeZoneInfo.GetEastern(), TimeZoneInfo.GetUTC(), dateTime );
        }

        private PROPERTY MakeProperty( string propName, string propValue )
        {
            PROPERTY prop = new PROPERTY();
            prop.propName = propName;
            prop.propValue = propValue;
            return prop;
        }

        internal DEBUG_LOG GetDEBUG_LOG( string logText, string dsSn, long? refId, DateTime eventTime )
        {
            DEBUG_LOG wsDebugLog = new DEBUG_LOG();
            wsDebugLog.log = logText;
            wsDebugLog.sn = dsSn;
            wsDebugLog.time = eventTime;
            wsDebugLog.referenceId = refId;
            return wsDebugLog;
        }

        internal DATABASE_UPLOAD GetDATABASE_UPLOAD( byte[] file, string fileName, string dsSn, long? refId, DateTime eventTime )
        {
            DATABASE_UPLOAD wsDatabaseUpload = new DATABASE_UPLOAD();

            wsDatabaseUpload.sn = dsSn;
            wsDatabaseUpload.time = eventTime;
            wsDatabaseUpload.referenceId = refId;

            wsDatabaseUpload.databaseFile = file;
            wsDatabaseUpload.databaseName = fileName;

            return wsDatabaseUpload;
        }
	}
}
