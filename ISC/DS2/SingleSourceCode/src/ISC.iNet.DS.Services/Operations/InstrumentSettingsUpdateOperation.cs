using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    using ISC.iNet.DS.DomainModel; // puting this here avoids compiler's confusion of DomainModel.Instrument vs Instrument.Driver.

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to update instrument settings.
	/// </summary>
    public class InstrumentSettingsUpdateOperation : InstrumentSettingsUpdateAction, IOperation
    {
        #region Fields

        private InstrumentSettingsUpdateEvent _instrumentSettingsUpdateEvent;

        private InstrumentController _instCtrlr;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of InstrumentSettingsUpdateOperation class.
        /// </summary>
        public InstrumentSettingsUpdateOperation() { }

        public InstrumentSettingsUpdateOperation( InstrumentSettingsUpdateAction instrumentSettingsUpdateAction )
            : base( instrumentSettingsUpdateAction ) { }

		#endregion

        #region Methods

        /// <summary>
        /// Executes an instrument settings update operation.
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            Stopwatch stopwatch = Log.TimingBegin("INSTRUMENT SETTINGS UPDATE");

            // Check for a docked instrument.
            if ( !Master.Instance.ControllerWrapper.IsDocked() )
                throw new InstrumentNotDockedException();

            string serialNumber = Master.Instance.SwitchService.Instrument.SerialNumber;
			DeviceType instrumentType = Master.Instance.SwitchService.Instrument.Type;

            if ( serialNumber == string.Empty || instrumentType == DeviceType.Unknown )
                throw new InstrumentNotDockedException();

            // Create the return event.
            _instrumentSettingsUpdateEvent = new InstrumentSettingsUpdateEvent( this );

            // Retrieve the docking station's information.
            _instrumentSettingsUpdateEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();
			_instrumentSettingsUpdateEvent.DockedTime = Master.Instance.SwitchService.DockedTime;

            // Get the settings from the database to update the instrument with.
            Instrument settings = new InstrumentDataAccess().FindApplicableSettings( serialNumber, instrumentType );

            // If no instrument settings found, then there's nothing more we can do.
            // since we can't update the instrument's settings if we have no settings.
            if ( settings == null )
            {
                string errMsg = string.Format( "Unable to find instrument settings for S/N \"{0}\"", serialNumber );
                _instrumentSettingsUpdateEvent.Errors.Add( new DockingStationError( errMsg, DockingStationErrorLevel.Warning ) );
                return _instrumentSettingsUpdateEvent;
            }

			// The settings loaded only apply to the discovered instrument.
			// We don't know if the settings loaded are the defaults are not 
			// so we just set the type to what we searched for above.
			settings.Type = instrumentType;

            // Merge the loaded settings with our Instrument. 

            using ( _instCtrlr = SwitchService.CreateInstrumentController() )
            {
                // Open the serial port connection needed to communicate with the instrument.
                _instCtrlr.Initialize( InstrumentController.Mode.Batch );

				// After an instrument settings read, we need to send a command to the instrument
				// to have it clear its base unit log.  Only SafeCore needs this command.
				_instCtrlr.ClearBaseUnits();

                // Update the docked instrument.
                _instrumentSettingsUpdateEvent.DockedInstrument = UpdateInstrument( settings, serialNumber );

                // Settings RefId will contain the ID of the settings used during the Update Event.  Otherwise, will be Nullid.
                // We place it into the event's Instrument in order to upload it to iNet.
                _instrumentSettingsUpdateEvent.DockedInstrument.RefId = settings.RefId;

                if (_instrumentSettingsUpdateEvent.DockingStation.ClearPeaksUponDocking)
                {
                    _instCtrlr.ClearInstrumentSensorPeaks(_instrumentSettingsUpdateEvent.DockedInstrument.InstalledComponents);
                }

            } // end-using

            Log.TimingEnd("INSTRUMENT SETTINGS UPDATE",stopwatch);

            return _instrumentSettingsUpdateEvent;
        }

        /// <summary>
        /// Set the docked instrument's values.
        /// </summary>
        /// <param name="settings">The values to place on the docked instrument.</param>
        private Instrument UpdateInstrument( Instrument settings, string expectedSerialNumber )
        {
            Log.Debug( string.Format( "{0}.UpdateInstrument", Name ) );

            if ( !Controller.IsDocked() ) // Determine if there is an instrument to read.
                return null;

            string tmpString = _instCtrlr.GetSerialNumber();
            if ( tmpString == string.Empty )
                Log.Warning( "GetSerialNumber returned empty string!" );

            Log.Debug( string.Format( "Updating with {0} settings (RefId={1})",
                ( settings.SerialNumber == string.Empty ) ? "default instrument" : "instrument-specific", settings.RefId ) );

            // Check to see if we are modifying the correct instrument.
            if ( expectedSerialNumber != tmpString )
            {
                string msg = string.Format( "Instrument S/N mismatch. expected=\"{0}\" actual=\"{1}\"", expectedSerialNumber, tmpString );
                Log.Error( msg );
                throw new ApplicationException( msg );
            }

			// Check to see if we are modifying the correct instrument type.
			DeviceType tmpType = Master.Instance.SwitchService.Instrument.Type;
			if ( settings.Type != tmpType )
			{
				string msg = string.Format( "Instrument Type mismatch. expected=\"{0}\" actual=\"{1}\"", settings.Type.ToString(), tmpType.ToString() );
				Log.Error( msg );
				throw new ApplicationException( msg );
			}

			// We already verified that the type of the settings match the type of the docked instrument. 
			// Now we verify that the type is supported by this type of docking station.
			if ( settings.Type != Configuration.DockingStation.Type )
			{
				// VPRO instruments are supported by MX4 docking stations.
				if ( !( settings.Type == DeviceType.VPRO && Configuration.DockingStation.Type == DeviceType.MX4 ) )
				{
					string msg = string.Format( "Instrument {0} is of the wrong type (\"{1}\")", tmpString, settings.Type.ToString() );
					Log.Error( msg );
					throw new ApplicationException( msg );
				}
			}

            // Temp variables
            int tmpInt;
            short tmpShort;
            double tmpDouble;
            
			// AccessCode will be an empty string if it should be set to the default.
			if ( settings.AccessCode == string.Empty )
				settings.AccessCode = _instCtrlr.Driver.Definition.DefaultSecurityCode;

			// Instrument security or access code setting.
			tmpString = Master.Instance.SwitchService.Instrument.AccessCode;
            if ( settings.AccessCode != tmpString )
            {
#if DEBUG // don't show access codes in release builds.
                LogUpdate( "AccessCode", settings.AccessCode, tmpString );
#else
                LogUpdate( "AccessCode", string.Empty.PadRight( settings.AccessCode.Length, '*' ), string.Empty.PadRight( tmpString.Length, '*' ) );
#endif
                _instCtrlr.SetAccessCode( settings.AccessCode );
                Master.Instance.SwitchService.Instrument.AccessCode = settings.AccessCode;
            }

            // Recording interval setting (in seconds) for datalog.
            // Note that RecordingIntervalIncrement will return zero if instrument
            // doesn't allow its recording interval to be changed.
            if ( _instCtrlr.HasDataLoggingFeature && _instCtrlr.RecordingIntervalIncrement > 0 )
            {
                tmpInt = Master.Instance.SwitchService.Instrument.RecordingInterval;
                if ( settings.RecordingInterval != tmpInt )
                {
                    LogUpdate( "RecordingInterval", settings.RecordingInterval, tmpInt );
                    _instCtrlr.SetRecordingInterval( settings.RecordingInterval );
                    Master.Instance.SwitchService.Instrument.RecordingInterval = settings.RecordingInterval;
                }
            }

            // TWA Time Base setting.
            if ( _instCtrlr.HasTwaFeature )
            {
                tmpInt = Master.Instance.SwitchService.Instrument.TWATimeBase;
                if ( settings.TWATimeBase != tmpInt )
                {
                    LogUpdate("TWATimeBase", settings.TWATimeBase, tmpInt);
                    _instCtrlr.SetTwaTimeBase( settings.TWATimeBase );
                    Master.Instance.SwitchService.Instrument.TWATimeBase = settings.TWATimeBase;
                }
            }

			// Out-of-Motion (Man Down) Warning Interval setting
			if ( _instCtrlr.HasOomWarningIntervalFeature )
			{
				tmpInt = Master.Instance.SwitchService.Instrument.OomWarningInterval;
				if ( settings.OomWarningInterval != tmpInt )
				{
					LogUpdate( "OomWarningInterval", settings.OomWarningInterval, tmpInt );
					_instCtrlr.SetOomWarningInterval( settings.OomWarningInterval );
					Master.Instance.SwitchService.Instrument.OomWarningInterval = settings.OomWarningInterval;
				}
			}

			// Dock Overdue Interval setting
			if ( _instCtrlr.HasDockIntervalFeature )
			{
				tmpInt = Master.Instance.SwitchService.Instrument.DockInterval;
				if ( settings.DockInterval != tmpInt )
				{
					LogUpdate( "DockInterval", settings.DockInterval, tmpInt );
					_instCtrlr.SetDockInterval( settings.DockInterval );
					Master.Instance.SwitchService.Instrument.DockInterval = settings.DockInterval;
				}
			}

            // Maintenance Interval setting
            if ( _instCtrlr.HasMaintenanceIntervalFeature )
            {
                tmpInt = Master.Instance.SwitchService.Instrument.MaintenanceInterval;
                if ( settings.MaintenanceInterval != tmpInt )
                {
                    LogUpdate( "MaintenanceInterval", settings.MaintenanceInterval, tmpInt );
                    _instCtrlr.SetMaintenanceInterval( settings.MaintenanceInterval );
                    Master.Instance.SwitchService.Instrument.MaintenanceInterval = settings.MaintenanceInterval;
                }
            }

            // Calibration Interval setting
            if ( _instCtrlr.HasCalibrationIntervalFeature )
            {
                tmpShort = Master.Instance.SwitchService.Instrument.CalibrationInterval;
                if ( settings.CalibrationInterval != tmpShort )
                {
                    LogUpdate( "CalibrationInterval", settings.CalibrationInterval, tmpShort );
                    _instCtrlr.SetCalibrationInterval( settings.CalibrationInterval );
                    Master.Instance.SwitchService.Instrument.CalibrationInterval = settings.CalibrationInterval;
                }
            }

            // Bump Interval setting
            if ( _instCtrlr.HasBumpIntervalFeature )
            {
				tmpDouble = Master.Instance.SwitchService.Instrument.BumpInterval;
                if ( settings.BumpInterval != tmpDouble )
                {
                    LogUpdate( "BumpInterval", settings.BumpInterval, tmpDouble );
                    _instCtrlr.SetBumpInterval( settings.BumpInterval );
                    Master.Instance.SwitchService.Instrument.BumpInterval = settings.BumpInterval;
                }
            }

            // instrument controller will return -1 if instrument does not support bump thresholding.
            if ( _instCtrlr.HasBumpThresholdFeature )
            {
				tmpInt = Master.Instance.SwitchService.Instrument.BumpThreshold;
				if ( settings.BumpThreshold != tmpInt )
                {
                    LogUpdate( "BumpThreshold", settings.BumpThreshold, tmpInt );
                    _instCtrlr.SetBumpThreshold( settings.BumpThreshold );
                    Master.Instance.SwitchService.Instrument.BumpThreshold = settings.BumpThreshold;
                }
            }

            if ( _instCtrlr.HasBumpTimeoutFeature )
            {
				tmpInt = Master.Instance.SwitchService.Instrument.BumpTimeout;
                if ( settings.BumpTimeout != tmpInt )
                {
                    LogUpdate( "BumpTimeout", settings.BumpTimeout, tmpInt );
                    _instCtrlr.SetBumpTimeout( settings.BumpTimeout );
                    Master.Instance.SwitchService.Instrument.BumpTimeout = settings.BumpTimeout;
                }
            }

			UpdateLanguage( settings, Master.Instance.SwitchService.Instrument );

            if ( _instCtrlr.HasMagneticFieldDurationFeature )
            {
				tmpInt = Master.Instance.SwitchService.Instrument.MagneticFieldDuration;
                if ( settings.MagneticFieldDuration != tmpInt )
                {
                    LogUpdate( "MagneticFieldDuration", settings.MagneticFieldDuration, tmpInt );
                    _instCtrlr.SetMagneticFieldDuration( settings.MagneticFieldDuration );
                    Master.Instance.SwitchService.Instrument.MagneticFieldDuration = settings.MagneticFieldDuration;
                }
            }

			if ( _instCtrlr.HasCompanyNameFeature )
			{
				tmpString = Master.Instance.SwitchService.Instrument.CompanyName;
				if ( settings.CompanyName != tmpString )
				{
					LogUpdate( "CompanyName", settings.CompanyName, tmpString );
					_instCtrlr.SetCompanyName( settings.CompanyName );
					Master.Instance.SwitchService.Instrument.CompanyName = settings.CompanyName;
				}
			}

			if ( _instCtrlr.HasCompanyMessageFeature )
			{
				tmpString = Master.Instance.SwitchService.Instrument.CompanyMessage;
				if ( settings.CompanyMessage != tmpString )
				{
					LogUpdate( "CompanyMessage", settings.CompanyMessage, tmpString );
					_instCtrlr.SetCompanyMessage( settings.CompanyMessage );
					Master.Instance.SwitchService.Instrument.CompanyMessage = settings.CompanyMessage;
				}
			}

			UpdateAlarmActionMessages( settings );
			
            // The GBPRO has no backlight settings
            // 11/13/07 - GBPlus also has no backlight settings.
            // 4/30/10 - Currently, None of the modbus instrument have a backlight setting.
            if ( settings.Backlight != BacklightSetting.Unknown )
            {
                // Retrieve instrument's current backlight setting.
				BacklightSetting tmpBacklightSettings = Master.Instance.SwitchService.Instrument.Backlight;
                if ( settings.Backlight != tmpBacklightSettings )
                {
                    LogUpdate( "Backlight", settings.Backlight.ToString(), tmpBacklightSettings.ToString() );
                    _instCtrlr.SetBacklightSetting( settings.Backlight );
                    Master.Instance.SwitchService.Instrument.Backlight = settings.Backlight;
                }
            }

            //Suresh 30-SEPTEMBER-2011 INS-2277
            if (_instCtrlr.HasBacklightTimeoutConfigFeature)
            {
				tmpInt = Master.Instance.SwitchService.Instrument.BacklightTimeout;
                if ( settings.BacklightTimeout != tmpInt )
                {
                    LogUpdate("BackLightTimeout", settings.BacklightTimeout, tmpInt);
                    _instCtrlr.SetBacklightTimeout(settings.BacklightTimeout);
                    Master.Instance.SwitchService.Instrument.BacklightTimeout = settings.BacklightTimeout;
                }
            }

            // Synch instrument's clock with docking station's clock.
            if ( settings.Type != DeviceType.GBPLS ) // GBPlus has no clock.
            {
                DateTime instTime = _instCtrlr.GetTime();
                DateTime localNow = Configuration.GetLocalTime();

                // Round times to nearest second.
                localNow = new DateTime( localNow.Year, localNow.Month, localNow.Day, localNow.Hour, localNow.Minute, localNow.Second );
                instTime = new DateTime( instTime.Year, instTime.Month, instTime.Day, instTime.Hour, instTime.Minute, instTime.Second );

                if ( localNow != instTime )
                {
                    const string dateTimeFormat = "HH:mm:ss MM/dd/yyyy";
                    LogUpdate( "Time", localNow.ToString( dateTimeFormat ), instTime.ToString( dateTimeFormat ) );
                    _instCtrlr.SetTime( localNow );
                }
            }

            // Custom response factors NEED to be set here BEFORE we update the sensors.

            _instCtrlr.SetCustomPidFactors( settings.CustomPidFactors );
            Master.Instance.SwitchService.Instrument.CustomPidFactors = settings.CustomPidFactors;

            _instCtrlr.SetFavoritePidFactors( settings.FavoritePidFactors );
            Master.Instance.SwitchService.Instrument.FavoritePidFactors = settings.FavoritePidFactors;

			for ( int pos = 1; pos <= _instCtrlr.Driver.Definition.MaxSensorCapacity; pos++ )
			{
				InstalledComponent component = Master.Instance.SwitchService.Instrument.InstalledComponents.Find( ic => ic.Position == pos );

				if ( component == null || !( component.Component is Sensor ) )
				{
					Log.Warning( "No sensor installed at position " + pos );
				}
				else
				{
					UpdateSensor( pos, (Sensor)component.Component, settings.SensorSettings );
				}
			}           

            UpdateUsersAndSites( settings );
                      
            // Set the instrument's enabled options.  Although we pass into the method only those
            // options that should be enabled, it returns the state (enabled or disabled) of ALL options.
            // We update the switch service's cached instrument with the list of All options.
            List<DeviceOption> deviceOptions = _instCtrlr.SetInstrumentOptions(settings.Options);
            Master.Instance.SwitchService.Instrument.Options = deviceOptions;

			// instrument level wireless settings
			if ( _instCtrlr.Driver.Definition.HasWirelessFeature && ( settings.Type == DeviceType.SC || settings.Type == DeviceType.VPRO) )
			{
				tmpInt = Master.Instance.SwitchService.Instrument.WirelessPeerLostThreshold;
				if ( settings.WirelessPeerLostThreshold != tmpInt )
				{
					LogUpdate( "WirelessPeerLostThreshold", settings.WirelessPeerLostThreshold, tmpInt );
					_instCtrlr.Driver.setWirelessPeerLostThreshold( settings.WirelessPeerLostThreshold );
					Master.Instance.SwitchService.Instrument.WirelessPeerLostThreshold = settings.WirelessPeerLostThreshold;
				}

				tmpInt = Master.Instance.SwitchService.Instrument.WirelessNetworkLostThreshold;
				if ( settings.WirelessNetworkLostThreshold != tmpInt )
				{
					LogUpdate( "WirelessNetworkLostThreshold", settings.WirelessNetworkLostThreshold, tmpInt );
					_instCtrlr.Driver.setWirelessNetworkLostThreshold( settings.WirelessNetworkLostThreshold );
					Master.Instance.SwitchService.Instrument.WirelessNetworkLostThreshold = settings.WirelessNetworkLostThreshold;
				}

                if (_instCtrlr.Driver.Definition.HasWirelessNetworkDisconnectDelayConfigFeature)
                {
                    tmpInt = Master.Instance.SwitchService.Instrument.WirelessNetworkDisconnectDelay;
                    if (settings.WirelessNetworkDisconnectDelay != tmpInt)
                    {
                        LogUpdate( "WirelessNetworkDisconnectDelay", settings.WirelessNetworkDisconnectDelay, tmpInt );
                        _instCtrlr.Driver.setWirelessNetworkDisconnectDelay( settings.WirelessNetworkDisconnectDelay );
                        Master.Instance.SwitchService.Instrument.WirelessNetworkDisconnectDelay = settings.WirelessNetworkDisconnectDelay;
                    }
                }

				tmpInt = Master.Instance.SwitchService.Instrument.WirelessReadingsDeadband;
				if ( settings.WirelessReadingsDeadband != tmpInt )
				{
					LogUpdate( "WirelessReadingsDeadband", settings.WirelessReadingsDeadband, tmpInt );
					_instCtrlr.Driver.setWirelessReadingsDeadband( settings.WirelessReadingsDeadband );
					Master.Instance.SwitchService.Instrument.WirelessReadingsDeadband = settings.WirelessReadingsDeadband;
				}

                if ( _instCtrlr.Driver.Definition.HasBluetoothFeature )
                {                    
                    tmpInt = Master.Instance.SwitchService.Instrument.LoneWorkerOkMessageInterval;
                    if ( settings.LoneWorkerOkMessageInterval != tmpInt )
                    {
                        LogUpdate( "BluetoothLoneWorkerOkMessageInterval", settings.LoneWorkerOkMessageInterval, tmpInt );
                        _instCtrlr.Driver.setBluetoothLoneWorkerOkMessageInterval( settings.LoneWorkerOkMessageInterval );
                        Master.Instance.SwitchService.Instrument.LoneWorkerOkMessageInterval = settings.LoneWorkerOkMessageInterval;
                    }

                    //INS-7908 -- To handle for the Ventis Pro Instrument, which has already upgraded to latest firmware, but the bluetooth feature has not been enabled in it.
                    //As of now, only VPRO instrument has bluetooth feature from v2.0 and above versions (HasBluetoothFeature determines that).
                    //The below check for VPRO is done specifically, because in future if any instrument has bluetooth feature, we may need to handle that in a better way.                    
                    if ( settings.Type == DeviceType.VPRO && !_instCtrlr.Driver.isBluetoothFeatureEnabled() )
                    {
                        //Note: Do we need to check the BluetoothFeatureActivated in settings and allow to override here? as of now, ignoring the settings value for the bluetooth, since by default its disabled now but we need to make enable by default.
                        //Why we are not enabling the feature at the factory/account/settings/instrument level enable by default and set the value based on that??
                        LogUpdate( "BluetoothFeatureEnabled", true, Master.Instance.SwitchService.Instrument.BluetoothFeatureActivated );
                        _instCtrlr.Driver.enableBluetoothFeature( true );
                        Master.Instance.SwitchService.Instrument.BluetoothFeatureActivated = true;
                    }
                }
			}

			UpdateWirelessModule( settings, Master.Instance.SwitchService.Instrument.WirelessModule );

            if (_instCtrlr.Driver.Definition.HasGpsFeature)
            {
                tmpInt = Master.Instance.SwitchService.Instrument.GpsReadingInterval;
                if ( settings.GpsReadingInterval != tmpInt )
                {
                    LogUpdate( "GpsReadingInterval", settings.GpsReadingInterval, tmpInt );
                    _instCtrlr.Driver.setGpsReadingInterval(settings.GpsReadingInterval);
                    Master.Instance.SwitchService.Instrument.GpsReadingInterval = settings.GpsReadingInterval;
                }
            }

			// After updating all the instrument settings we need to send a command to the instrument
			// to have it persist the settings so they will be retained after power is lost.
			// Only SafeCore needs this command.
			_instCtrlr.Driver.saveInstrumentSettings();

			// Read all settings fresh from the docked instrument to ensure accurate instrument  
			// settings are cached as well as uploaded to iNet.  Reading all settings during the 
			// instrument settings update operation does not incur an extra time penalty for  
			// re-establishing communication with the docked instrument like it would in a 
			// follow-up instrument settings read operation.
			Instrument returnInstrument = _instCtrlr.DiscoverDockedInstrument( false ); 
            
			if ( returnInstrument == null || returnInstrument.SerialNumber.Length == 0 )
			{
				throw new InstrumentNotDockedException();
			}

			Master.Instance.SwitchService.Instrument = (Instrument)returnInstrument.Clone();

            return returnInstrument;
        }

        private void UpdateLanguage(Instrument settings, Instrument returnInstrument)
        {
			string instLang = Master.Instance.SwitchService.Instrument.Language.Code;

            returnInstrument.Language.Code = instLang;

            // JFC  26-Sep-2013  INS-4248
            // New logic to support setting the docked instrument's language from what 
            // was provided by iNet.  The language code will be an empty string at this point
            // if the pre-v5.7 logic is supposed to be used which will try to set the 
            // instrument to the docking station's language.
            if ( settings.Language.Code != String.Empty )
            {
                // Instrument already set to what iNet wants.
                if ( instLang == settings.Language.Code )
                    return;

                // If code execution reaches this point, we know the language on the instrument 
                // differs from what iNet wants.  Set the instrument's language to match what iNet provided.
                LogUpdate( "Language", settings.Language.Code, instLang );
				if ( _instCtrlr.SetLanguage( settings.Language.Code ) )
				{
					Master.Instance.SwitchService.Instrument.Language.Code = settings.Language.Code;
					returnInstrument.Language.Code = settings.Language.Code;
				}
            }
            // Below block contains the untouched logic to set the instrument's language
            // to the docking station's language.
            else
            {
                string idsLang = Configuration.DockingStation.Language.Code.ToUpper();

                // If instrument language differs from IDS's language, but is a supported language,
                // then make the language of the instrument equal the language of the IDS.  e.g.,
                // if instrument is English, but IDS is French, then make the instrument French, too.
                //
                // But, if instrument language differs from IDS's language, but is NOT a supported
                // langauge, then leave the instrument's language alone. e.g., if instrument is
                // Russian, but IDS is English, then leave just the instrument's language alone.

                if ( instLang == idsLang )
                    return;

                // SGF  1-Oct-2012  INS-1656
                // Starting here, code has been reorganized as a result of the addition of Portuguese (Brazil) to the iNet DS.

                if ( instLang != Language.English
                    && instLang != Language.French
                    && instLang != Language.German
                    && instLang != Language.Spanish
                    && instLang != Language.PortugueseBrazil )
                {
                    // The instrument language is not supported by the iNet DS, so leave it set to that language.
                    return;
                }

                // If code execution reaches this point, we know the language on the instrument 
                // differs from the iNet DS, and the current language on the instrument is one of the 
                // languages the iNet DS supports.  Set the instrument's language to match the iNet DS language.
                LogUpdate( "Language", instLang, idsLang );
				if ( _instCtrlr.SetLanguage( idsLang ) )
				{
					Master.Instance.SwitchService.Instrument.Language.Code = idsLang;
					returnInstrument.Language.Code = idsLang;
				}
            }
        }

		private void UpdateWirelessModule( Instrument settings, WirelessModule installedModule )
		{
			if ( !_instCtrlr.Driver.Definition.HasWirelessFeature )
				return;
			
			// only SafeCore and Ventis Pro supports changing wireless module settings/options
			if ( settings.Type != DeviceType.SC && settings.Type != DeviceType.VPRO )
				return;

			// ensure a wireless module is installed and we have settings to apply before continuing
			if ( installedModule == null || settings.WirelessModule == null )
				return;

			Log.Debug( string.Format( "{0}.UpdateWirelessModule", Name ) );

			WirelessModule moduleSettings = settings.WirelessModule;

			if ( moduleSettings.TransmissionInterval != installedModule.TransmissionInterval )
			{
				LogUpdate( "TransmissionInterval", moduleSettings.TransmissionInterval, installedModule.TransmissionInterval );

				_instCtrlr.Driver.setWirelessTransmissionInterval( moduleSettings.TransmissionInterval );
				installedModule.TransmissionInterval = moduleSettings.TransmissionInterval;
			} 

			if ( moduleSettings.EncryptionKey != installedModule.EncryptionKey )
			{
#if DEBUG // don't show encryption keys in release builds
				LogUpdate( "CustomEncryptionKey", moduleSettings.EncryptionKey, installedModule.EncryptionKey );
#else
				LogUpdate( "CustomEncryptionKey", string.Empty.PadRight( moduleSettings.EncryptionKey.Length, '*' ), string.Empty.PadRight( installedModule.EncryptionKey.Length, '*' ) );
#endif
				_instCtrlr.Driver.setWirelessCustomEncryptionKey( moduleSettings.EncryptionKey );
				installedModule.EncryptionKey = moduleSettings.EncryptionKey;
			}

			if ( moduleSettings.MessageHops != installedModule.MessageHops )
			{
				LogUpdate( "MessageHops", moduleSettings.MessageHops, installedModule.MessageHops );

				_instCtrlr.Driver.setWirelessMessageHops( moduleSettings.MessageHops );
				installedModule.MessageHops = moduleSettings.MessageHops;
			}

			if ( moduleSettings.MaxPeers != installedModule.MaxPeers )
			{
				LogUpdate( "MaxPeers", moduleSettings.MaxPeers, installedModule.MaxPeers );

				_instCtrlr.Driver.setWirelessMaximumPeers( moduleSettings.MaxPeers );
				installedModule.MaxPeers = moduleSettings.MaxPeers;
			}

			if ( moduleSettings.PrimaryChannel != installedModule.PrimaryChannel )
			{
				LogUpdate( "PrimaryChannel", moduleSettings.PrimaryChannel, installedModule.PrimaryChannel );

				_instCtrlr.Driver.setWirelessPrimaryChannel( moduleSettings.PrimaryChannel );
				installedModule.PrimaryChannel = moduleSettings.PrimaryChannel;
			}

			if ( moduleSettings.SecondaryChannel != installedModule.SecondaryChannel )
			{
				LogUpdate( "SecondaryChannel", moduleSettings.SecondaryChannel, installedModule.SecondaryChannel );

				_instCtrlr.Driver.setWirelessSecondaryChannel( moduleSettings.SecondaryChannel );
				installedModule.SecondaryChannel = moduleSettings.SecondaryChannel;
			}

			if ( moduleSettings.ActiveChannelMask != installedModule.ActiveChannelMask )
			{
				LogUpdate( "ActiveChannelMask", moduleSettings.ActiveChannelMask, installedModule.ActiveChannelMask );

				_instCtrlr.SetWirelessActiveChannelMask( moduleSettings.ActiveChannelMask );
				installedModule.ActiveChannelMask = moduleSettings.ActiveChannelMask;
			}

            if (moduleSettings.WirelessFeatureBits != installedModule.WirelessFeatureBits)
            {
                LogUpdate( "WirelessFeatureBits", moduleSettings.WirelessFeatureBits, installedModule.WirelessFeatureBits );

                _instCtrlr.SetWirelessFeatureBits( moduleSettings.WirelessFeatureBits );
                installedModule.WirelessFeatureBits = moduleSettings.WirelessFeatureBits;
            }


            if (_instCtrlr.Driver.Definition.HasWirelessBindingTimeoutConfigFeature)
            {
                if (moduleSettings.WirelessBindingTimeout != installedModule.WirelessBindingTimeout)
                {
                    LogUpdate( "WirelessBindingTimeout", moduleSettings.WirelessBindingTimeout, installedModule.WirelessBindingTimeout );

                    _instCtrlr.SetWirelessBindingTimeout( moduleSettings.WirelessBindingTimeout );
                    installedModule.WirelessBindingTimeout = moduleSettings.WirelessBindingTimeout;
                }
            }

            if (_instCtrlr.Driver.Definition.HasWirelessListeningPostChannelMaskFeature)
            {
                if (moduleSettings.ListeningPostChannelMask != installedModule.ListeningPostChannelMask)
                {
                    LogUpdate("ListeningPostChannelMask", moduleSettings.ListeningPostChannelMask, installedModule.ListeningPostChannelMask);

                    _instCtrlr.SetWirelessListeningPostChannelMask(moduleSettings.ListeningPostChannelMask);
                    installedModule.ListeningPostChannelMask = moduleSettings.ListeningPostChannelMask;
                }
            }
                       
			// wireless module options
			installedModule.Options = _instCtrlr.SetWirelessModuleOptions( moduleSettings.Options );
		}

		private void UpdateAlarmActionMessages( Instrument settings )
		{
			if ( !_instCtrlr.HasAlarmActionMessagesFeature )
			{
				return;
			}

			// put installed sensor codes in dictionary for faster look-up
			Dictionary<string, bool> sensorDictionary = new Dictionary<string, bool>();

			// we will only write relevant action messages in the instrument   
			// if there is an installed sensor with a matching sensor code
			foreach ( InstalledComponent component in Master.Instance.SwitchService.Instrument.InstalledComponents )
			{
				Sensor sensor = component.Component as Sensor;
				if ( sensor != null )
				{
					sensorDictionary[sensor.Type.Code] = false;
				}
			}

			// make a list of just alarm messages that will be written to the instrument
			List<AlarmActionMessages> aamSettingsList = new List<AlarmActionMessages>();
			foreach ( AlarmActionMessages aam in settings.AlarmActionMessages )
			{
				if ( sensorDictionary.ContainsKey( aam.SensorCode ) && sensorDictionary[aam.SensorCode] == false )
				{
					// there should only be one object per sensor code, but we check here anyway
					sensorDictionary[aam.SensorCode] = true;
					aamSettingsList.Add( aam );
				}
			}

			// the driver will handle if the list is too small or too big
			_instCtrlr.SetAlarmActionMessages( aamSettingsList );
		}

        private void UpdateUsersAndSites( Instrument settings )
        {
            if ( settings.Type == DeviceType.GBPLS ) // GBPlus also has no sites or users.
                return;

            // Set the instrument's users.
            _instCtrlr.SetUsers( settings.Users );
            Master.Instance.SwitchService.Instrument.Users = settings.Users;

            if ( settings.Type == DeviceType.MX6 || settings.Type == DeviceType.SC )
            {
                // Calling setUsers on an MX6 instrument (tested with v4.10.02) will clear the active  
				// user once the instrument is undocked if it is not reset.  Therefore, the active user 
				// must always be set to avoid having to check if the users need to change on the 
				// instrument in the first place.
				LogUpdate( "ActiveUser", settings.ActiveUser, Master.Instance.SwitchService.Instrument.ActiveUser );
                _instCtrlr.SetActiveUser( settings.ActiveUser );
                Master.Instance.SwitchService.Instrument.ActiveUser = settings.ActiveUser;
            }

            // Set the instrument's sites.
            _instCtrlr.SetSites( settings.Sites );
            Master.Instance.SwitchService.Instrument.Sites = settings.Sites;

			if ( settings.Type == DeviceType.MX6 || settings.Type == DeviceType.SC )
            {
				// Calling setSites on an MX6 instrument (tested with v4.10.02) will clear the active  
				// site once the instrument is undocked if it is not reset.  Therefore, the active site 
				// must always be set to avoid having to check if the sites need to change on the 
				// instrument in the first place.
				LogUpdate( "ActiveSite", settings.ActiveSite, Master.Instance.SwitchService.Instrument.ActiveSite );
                _instCtrlr.SetActiveSite( settings.ActiveSite );
                Master.Instance.SwitchService.Instrument.ActiveSite = settings.ActiveSite;
            }

            // INS-8548 RHP v7.5 - override the instrument specific "User Security Level" multioption with instrument's Activer User's security level
            if (settings.Type == DeviceType.VPRO)
            {
                if (settings.AccessLevel >= 0)
                {
                    DeviceOption option = settings.Options.Find(op => op.Code.StartsWith("USL") && op.Enabled);
                    if (option != null)
                    {
                        settings.Options.Remove(option);
                        // settings.AccessLevel is a nullable short and input may varry from 0 to 10. 
                        // The Multi-option code mapptings are 0->USL0, 1 -> USL1 , and so on til 10->USLA. Hence the conversion ToString("X")
                        settings.Options.Add(new DeviceOption("USL" + (settings.AccessLevel ?? 0).ToString("X"), true));
                        LogUpdate("UserSecurityLevel", settings.AccessLevel, option.Code);
                    }
                }
            }

        }

		/// <summary>
		/// Set a sensor's information.
		/// </summary>
		/// <param name="position">The position of the sensor to be updated.</param>
		/// <param name="sensor">The cached values for the sensor to be updated that corresponds with the position.</param>
		/// <param name="sensorSettings">Keyed by sensor code.</param>
		private void UpdateSensor( int position, Sensor sensor, Dictionary<string, Sensor> sensorSettings )
		{
			string sensorCode = sensor.Type.Code;
            // Get the sensor's maximum reading. - INS-6584 RHP v7.6
            double maximumReading = _instCtrlr.GetSensorMaximumReading(position, sensor.Resolution);

			string updateMsg = string.Format( " Sensor{0}({1}) ", position, sensorCode );

			Log.Debug( string.Format( "{0}.UpdateSensor - {1}", Name, updateMsg ) );
            Log.Debug( string.Format( "{0}. Sensor's Maximum Reading is  {1}.", updateMsg, maximumReading ) );

			Sensor settings = null;
			if ( !sensorSettings.TryGetValue( sensorCode, out settings ) )
			{
				string msg = string.Format( "Sensor \"{0}\" not updated. No settings available for sensor code \"{1}\"", position, sensorCode );
				Log.Warning( msg );
				_instrumentSettingsUpdateEvent.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
				return;
			}

			// Enable/disable the sensor as needed.
			bool tmpEnabled = sensor.Enabled;
			if ( settings.Enabled != tmpEnabled )
			{
				LogUpdate( updateMsg + "Enabled", settings.Enabled, tmpEnabled );
				_instCtrlr.EnableSensor( position, settings.Enabled );
				sensor.Enabled = settings.Enabled;
			}

			// Although VX500's allow their response factors to be changed, DS2 does not support it.
			if ( _instCtrlr.IsSensorGasCodeConfigurable( sensorCode ) )
			{
				string tmpGasCode = sensor.GasDetected;
				if ( settings.GasDetected != tmpGasCode )
				{
					if ( settings.GasDetected != string.Empty )
					{
						LogUpdate( updateMsg + "GasDetected", settings.GasDetected, tmpGasCode );
						_instCtrlr.SetSensorGasCode( position, settings.GasDetected );
						sensor.GasDetected = settings.GasDetected;
					}
					else  // GasDetected == String.Empty
						Log.Error( "SetSensorGasCode cannot be called. No gas code specified!!" );
				}
			}

			// Update calibration gas code.
			string tmpString = sensor.CalibrationGas.Code;
			if ( settings.CalibrationGas.Code != tmpString )
			{
				LogUpdate( updateMsg + "CalGasCode", settings.CalibrationGas.Code, tmpString );
				_instCtrlr.SetSensorCalGasCode( position, settings.CalibrationGas.Code );
				sensor.CalibrationGas = GasType.Cache[settings.CalibrationGas.Code];
			}

			// Need to get the sensor's resolution for the following setttings.
			double resolution = sensor.Resolution;

			// Sensor Calibration Gas Concentration.
            double tmpDouble = settings.CalibrationGasConcentration;
            if ( tmpDouble != sensor.CalibrationGasConcentration )
			{
                // INS-6584 RHP v7.6 - If the Sensor setting received form iNet has a value higher than the sensor's maximum value allowed, 
                // then set the maximum allowed value.
                tmpDouble = (Math.Abs(tmpDouble) > maximumReading) ? maximumReading : tmpDouble;

                LogUpdate(updateMsg + "CalGasConc", tmpDouble, sensor.CalibrationGasConcentration);
                _instCtrlr.SetSensorCalGasConcentration( position, tmpDouble, resolution );
                sensor.CalibrationGasConcentration = tmpDouble;
			}

            // Sensor low alarm.
            tmpDouble = settings.Alarm.Low;
            if (tmpDouble != DomainModelConstant.NullDouble && tmpDouble != sensor.Alarm.Low)
            {
                // INS-6584 RHP v7.6 - If the Sensor setting received form iNet has a value higher than the sensor's maximum value allowed, 
                // then set the maximum allowed value.
                tmpDouble = (Math.Abs(tmpDouble) > maximumReading) ? maximumReading : tmpDouble;
              
                LogUpdate(updateMsg + "AlarmLow", tmpDouble, sensor.Alarm.Low);
                _instCtrlr.SetSensorLowAlarm(position, tmpDouble, resolution);
                sensor.Alarm.Low = tmpDouble;
            }

            // Sensor high alarm.
            tmpDouble = settings.Alarm.High;
            if (tmpDouble != DomainModelConstant.NullDouble && tmpDouble != sensor.Alarm.High)
            {
                // INS-6584 RHP v7.6 - If the Sensor setting received form iNet has a value higher than the sensor's maximum value allowed, 
                // then set the maximum allowed value.
                tmpDouble = (Math.Abs(tmpDouble) > maximumReading) ? maximumReading : tmpDouble;

                LogUpdate(updateMsg + "AlarmHigh", tmpDouble, sensor.Alarm.High);
                _instCtrlr.SetSensorHighAlarm(position, tmpDouble, resolution);
                sensor.Alarm.High = tmpDouble;
            }

			// Sensor gas alert.
			if ( _instCtrlr.Driver.Definition.HasGasAlertFeature )
			{			
				// iNet will send null if the value is to be set to the low alarm value so the gas alert is effectively disabled.
				double tmpGasAlert = settings.Alarm.GasAlert == DomainModelConstant.NullDouble ? settings.Alarm.Low : settings.Alarm.GasAlert;
				if ( tmpGasAlert != sensor.Alarm.GasAlert )
				{
                    // INS-6584 RHP v7.6 - If the Sensor setting received form iNet has a value higher than the sensor's Alarm Low, 
                    // then set the Sensor's Alarm Low for Non-O2 sensors. For O2 the maximum permissable level is Alam High.
                    if (sensorCode.Equals(SensorCode.O2))
                        tmpGasAlert = (Math.Abs(tmpGasAlert) > sensor.Alarm.High) ? sensor.Alarm.High : tmpGasAlert;
                    else
                        tmpGasAlert = (Math.Abs(tmpGasAlert) > sensor.Alarm.Low) ? sensor.Alarm.Low : tmpGasAlert;
                   
					LogUpdate( updateMsg + "AlarmGasAlert", tmpGasAlert, sensor.Alarm.GasAlert );
					_instCtrlr.SetSensorGasAlert( position, tmpGasAlert, resolution );
					sensor.Alarm.GasAlert = tmpGasAlert;
				}
			}						

			// Sensor TWA alarm.
			if ( sensor.Alarm.TWA != double.MinValue )
			{
                tmpDouble = settings.Alarm.TWA;
                if ( tmpDouble != DomainModelConstant.NullDouble && tmpDouble != sensor.Alarm.TWA )
				{
                    // INS-6584 RHP v7.6 - If the Sensor setting received form iNet has a value higher than the sensor's maximum value allowed, 
                    // then set the maximum allowed value.
                    tmpDouble = (Math.Abs(tmpDouble) > maximumReading) ? maximumReading : tmpDouble;
                  
                    LogUpdate( updateMsg + "AlarmTWA", tmpDouble, sensor.Alarm.TWA );
                    _instCtrlr.SetSensorTwaAlarm( position, tmpDouble, resolution );
                    sensor.Alarm.TWA = tmpDouble;
				}
			}
			// Sensor STEL alarm.
			if ( sensor.Alarm.STEL != double.MinValue )
			{
                tmpDouble = settings.Alarm.STEL;
                if ( tmpDouble != DomainModelConstant.NullDouble && tmpDouble != sensor.Alarm.STEL )
				{
                    // INS-6584 RHP v7.6 - If the Sensor setting received form iNet has a value higher than the sensor's maximum value allowed, 
                    // then set the maximum allowed value.
                    tmpDouble = (Math.Abs(tmpDouble) > maximumReading) ? maximumReading : tmpDouble;

                    LogUpdate( updateMsg + "AlarmSTEL", tmpDouble, sensor.Alarm.STEL );
                    _instCtrlr.SetSensorStelAlarm( position, tmpDouble, resolution );
                    sensor.Alarm.STEL = tmpDouble;
				}
			}
		}

        private bool LogUpdate( string label, object newValue, object oldValue )
        {
            Log.Debug( string.Format( "{0}: UPDATING {1}: \"{2}\" (\"{3}\")", Name, label, newValue.ToString(), oldValue.ToString() ) );
            return true;
        }

        #endregion Methods

    }  // end-class
	

}
