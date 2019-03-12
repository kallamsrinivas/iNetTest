using System.Threading;
// for registry access
using System.Collections.Generic;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to update settings.
	/// </summary>
	public class SettingsUpdateOperation : SettingsUpdateAction , IOperation
	{
        private SettingsUpdateEvent _settingsUpdateEvent;
        

		#region Fields

		#endregion
		
		#region Constructors
		
		/// <summary>
		/// Creates a new instance of SettingsUpdateOperation class.
		/// </summary>
        public SettingsUpdateOperation()  {}

        public SettingsUpdateOperation( SettingsUpdateAction settingsUpdateAction )
            : base( settingsUpdateAction )
        {
        }

		#endregion

		#region Methods

		/// <summary>
		/// Executes an instrument update settings operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            // We clone it so we don't have to worry about some other thread changing the Configuration behind our back.
            DockingStation config = (DockingStation)Configuration.DockingStation.Clone();

            // Make the return event.
            _settingsUpdateEvent = new SettingsUpdateEvent( this );

            DockingStation loadedSettings = LoadSettings( config.SerialNumber );

            if ( loadedSettings == null )
            {
                Log.Error( string.Format( "{0}: No settings. No update performed", Name ) );
                return _settingsUpdateEvent;
            }

            _settingsUpdateEvent.DockingStation = this.DockingStation = loadedSettings;

            bool modified = UpdateInetSettings( config );

            // If UseDockingStation is false, then settings that can only be updated in Configurator app
            // but not in iNet will not be filled in the DockingStation because they're not present
            // in our iNet database.  Therefore we don't bother to try and set them.
            if ( UseDockingStation )
            {
                modified = UpdateLocalOnlySettings( config ) || modified;

                modified = UpdateNetworkSettings( config, DockingStation.NetworkSettings ) || modified;
            }
			
            //Suresh 12-SEPTEMBER-2011 INS-2248
            //if ReplaceDSNetworkSettings is not null means, there is new new network settings (from replace DS) received from inet which need 
            //be applied to docking station.
            if (_settingsUpdateEvent.DockingStation.ReplacedDSNetworkSettings != null)
            {
                modified = UpdateNetworkSettings(config, DockingStation.ReplacedDSNetworkSettings) || modified;
                //New network settings should be applied to DS ONLY once. 
                //So, after applying network settings we will delete it from the database so that it will not be applied again. 
                new ReplacedNetworkSettingsDataAccess().Delete(); 
            }

            // Only need to update the configuration if something changed.
            if ( modified || Configuration.HasConfigurationError )
            {
                Log.Debug( string.Format( "{0}: Saving modified configuration.", Name ) );
                Configuration.DockingStation = config;
                Configuration.SaveConfiguration();
            }
            else
            {
                Log.Debug( string.Format( "{0}: OLD SETTINGS SAME AS NEW SETTINGS. NOTHING MODIFIED.", Name ) );
            }

            if ( _settingsUpdateEvent.RebootRequired )
                Log.Assert( modified, "RebootRequired is set, but and modified flag is not!" );

            if ( _settingsUpdateEvent.RebootRequired )
                Log.Warning( string.Format( "{0}: Modified configuration requires a reboot!.", Name ) );

            // Boy, what a hack...  Deliberately sleep 3 seconds to give the LCD time to display
            // (and the user time to read) the 'reading settings' message.
            Thread.Sleep(3000);

			return _settingsUpdateEvent;
		}

        /// <summary>
        /// Update settings that are configurable in iNet.
        /// Some of these settings can also be changed (temporarily) in Configurator.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        private bool UpdateInetSettings( DockingStation config )
        {
            bool modified = false;

            if ( DockingStation.Language.Code != config.Language.Code )
            {
                modified = LogUpdate( "Language", DockingStation.Language.Code, config.Language.Code );
                config.Language.Code = DockingStation.Language.Code;
            }

            if ( DockingStation.MenuLocked != config.MenuLocked )
            {
                modified = LogUpdate( "MenuLocked", DockingStation.MenuLocked, config.MenuLocked );
                config.MenuLocked = DockingStation.MenuLocked;
            }

            if ( DockingStation.UseAudibleAlarm != config.UseAudibleAlarm )
            {
                modified = LogUpdate( "UseAudibleAlarm", DockingStation.UseAudibleAlarm, config.UseAudibleAlarm );
                config.UseAudibleAlarm = DockingStation.UseAudibleAlarm;
            }

            if ( DockingStation.LogLevel != config.LogLevel )
            {
                modified = LogUpdate( "LogLevel", DockingStation.LogLevel.ToString(), config.LogLevel.ToString() );
                config.LogLevel = Log.Level = DockingStation.LogLevel;
            }

            if ( DockingStation.LogCapacity != config.LogCapacity )
            {
                modified = LogUpdate( "LogCapacity", DockingStation.LogCapacity, config.LogCapacity );
                config.LogCapacity = Log.Capacity = DockingStation.LogCapacity;
            }

            if ( DockingStation.InetUrl != config.InetUrl )
            {
                modified = LogUpdate( "InetUrl", DockingStation.InetUrl, config.InetUrl );
                config.InetUrl = DockingStation.InetUrl;
            }

            if ( DockingStation.InetPingInterval != config.InetPingInterval )
            {
                modified = LogUpdate( "InetPingInterval", DockingStation.InetPingInterval, config.InetPingInterval );
                config.InetPingInterval = DockingStation.InetPingInterval;
            }

            if ( DockingStation.InetTimeoutLow != config.InetTimeoutLow )
            {
                modified = LogUpdate( "InetTimeoutLow", DockingStation.InetTimeoutLow, config.InetTimeoutLow );
                config.InetTimeoutLow = DockingStation.InetTimeoutLow;
            }

            if ( DockingStation.InetTimeoutMedium != config.InetTimeoutMedium )
            {
                modified = LogUpdate( "InetTimeoutMedium", DockingStation.InetTimeoutMedium, config.InetTimeoutMedium );
                config.InetTimeoutMedium = DockingStation.InetTimeoutMedium;
            }

            if ( DockingStation.InetTimeoutHigh != config.InetTimeoutHigh )
            {
                modified = LogUpdate( "InetTimeoutHigh", DockingStation.InetTimeoutHigh, config.InetTimeoutHigh );
                config.InetTimeoutHigh = DockingStation.InetTimeoutHigh;
            }

            if ( DockingStation.InetUserName != config.InetUserName )
            {
                modified = LogUpdate( "InetUserName", DockingStation.InetUserName, config.InetUserName );
                config.InetUserName = DockingStation.InetUserName;
            }

            if ( DockingStation.InetPassword != config.InetPassword )
            {
                modified = LogUpdate( "InetPassword", DockingStation.InetPassword, config.InetPassword );
                config.InetPassword = DockingStation.InetPassword;
            }

            if ( !DockingStation.TimeZoneInfo.Equals( config.TimeZoneInfo ) )
            {
                modified = LogUpdate( "TimeZoneInfo", DockingStation.TimeZoneInfo, config.TimeZoneInfo );
                config.TimeZoneInfo = (TimeZoneInfo)DockingStation.TimeZoneInfo.Clone();
            }

            if ( DockingStation.PrintPerformedBy != config.PrintPerformedBy )
            {
                modified = LogUpdate( "PrintPerformedBy", DockingStation.PrintPerformedBy, config.PrintPerformedBy );
                config.PrintPerformedBy = DockingStation.PrintPerformedBy;
            }

            if ( DockingStation.PrintReceivedBy != config.PrintReceivedBy )
            {
                modified = LogUpdate( "PrintReceivedBy", DockingStation.PrintReceivedBy, config.PrintReceivedBy );
                config.PrintReceivedBy = DockingStation.PrintReceivedBy;
            }
           
            if ( DockingStation.Port1Restrictions != config.Port1Restrictions )
            {
                modified = LogUpdate( "Port1Restrictions", DockingStation.Port1Restrictions, config.Port1Restrictions );
                config.Port1Restrictions = DockingStation.Port1Restrictions;
            }

            if ( DockingStation.PurgeAfterBump != config.PurgeAfterBump )
            {
                modified = LogUpdate( "PurgeAfterBump", DockingStation.PurgeAfterBump, config.PurgeAfterBump );
                config.PurgeAfterBump = DockingStation.PurgeAfterBump;
            }

            if (DockingStation.ClearPeaksUponDocking != config.ClearPeaksUponDocking)
            {
                modified = LogUpdate("ClearPeaksUponDocking", DockingStation.ClearPeaksUponDocking, config.ClearPeaksUponDocking);
                config.ClearPeaksUponDocking = DockingStation.ClearPeaksUponDocking;
            }

            if (DockingStation.SingleSensorMode != config.SingleSensorMode)
            {
                modified = LogUpdate( "SingleSensorMode", DockingStation.SingleSensorMode, config.SingleSensorMode);
                config.SingleSensorMode = DockingStation.SingleSensorMode;
            }

            if ( DockingStation.UseExpiredCylinders != config.UseExpiredCylinders )
            {
                modified = LogUpdate( "UseExpiredCylinders", DockingStation.UseExpiredCylinders, config.UseExpiredCylinders );
                config.UseExpiredCylinders = DockingStation.UseExpiredCylinders;
            }

            if ( DockingStation.CombustibleBumpTestGas != config.CombustibleBumpTestGas )
            {
                modified = LogUpdate( "CombustibleBumpTestGas", DockingStation.CombustibleBumpTestGas, config.CombustibleBumpTestGas );
                config.CombustibleBumpTestGas = DockingStation.CombustibleBumpTestGas;
            }

            if ( DockingStation.SpanReserveThreshold != config.SpanReserveThreshold )
            {
                modified = LogUpdate( "SpanReserveThreshold", DockingStation.SpanReserveThreshold, config.SpanReserveThreshold );
                config.SpanReserveThreshold = DockingStation.SpanReserveThreshold;
            }

            if (DockingStation.StopOnFailedBumpTest != config.StopOnFailedBumpTest)
            {
                modified = LogUpdate("StopOnFailedBumpTest", DockingStation.StopOnFailedBumpTest, config.StopOnFailedBumpTest);
                config.StopOnFailedBumpTest = DockingStation.StopOnFailedBumpTest;
            }

            if (DockingStation.UpgradeOnErrorFail != config.UpgradeOnErrorFail)
            {
                modified = LogUpdate("UpgradeOnErrorFail", DockingStation.UpgradeOnErrorFail, config.UpgradeOnErrorFail);
                config.UpgradeOnErrorFail = DockingStation.UpgradeOnErrorFail;

                //AJAY: INS-8380 Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
                //If service account enabled UpgradeOnErrorFail, event priority needs to be changed for service
                //If service account disabled UpgradeOnErrorFail, we can continue to have customer's event priority.
                if (DockingStation.UpgradeOnErrorFail)
                    EventCode.SetEventPriorityForService();
                else
                    EventCode.SetEventPriorityForCustomers();
            }

            return modified;
        }


        /// <summary>
        /// Updates settings that can only be updated in Configurator and that
        /// cannot be updated in iNet.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        private bool UpdateLocalOnlySettings( DockingStation config )
        {
            Log.Assert( UseDockingStation, "UseDockingStation should be true!" );

            bool modified = false;

            // Note that we don't have to worry about actually disabling the web app here.
            // When the WebAppService thread sees the setting change, it will start/stop
            // the app appropiately
            if ( DockingStation.WebAppEnabled != config.WebAppEnabled )
            {
                modified = LogUpdate( "WebAppEnabled", DockingStation.WebAppEnabled, config.WebAppEnabled );
                config.WebAppEnabled = DockingStation.WebAppEnabled;

                _settingsUpdateEvent.RebootRequired = true; // need to reboot for it to take effect.
            }

            if ( DockingStation.WebAppPassword != config.WebAppPassword )
            {
#if DEBUG
                modified = LogUpdate( "WebAppPassword",
                    DockingStation.WebAppPassword,
                    config.WebAppPassword );
#else
                modified = LogUpdate( "WebAppPassword",
                    string.Empty.PadRight( DockingStation.WebAppPassword.Length, '*' ),
                    string.Empty.PadRight( config.WebAppPassword.Length, '*' ) );
#endif
                config.WebAppPassword = DockingStation.WebAppPassword;

                _settingsUpdateEvent.RebootRequired = true; // need to reboot for it to take effect.
            }

            if ( DockingStation.InetProxy != config.InetProxy )
            {
#if DEBUG
                modified = LogUpdate( "InetProxy",
                    DockingStation.InetProxy,
                    config.InetProxy );
#else
                // never log customer's actual proxy info, due to security concerns.
                // Especially since the log can be uploaded and viewed in iNet.
                modified = LogUpdate( "InetProxy",
                       string.Empty.PadRight( DockingStation.InetProxy.Length, '*' ),
                       string.Empty.PadRight( config.InetProxy.Length, '*' ) );
#endif
                config.InetProxy = DockingStation.InetProxy;
            }

            if ( DockingStation.InetProxyUserName != config.InetProxyUserName )
            {
#if DEBUG
                modified = LogUpdate( "InetProxyUserName",
                       DockingStation.InetProxyUserName,
                       config.InetProxyUserName );
#else
                modified = LogUpdate( "InetProxyUserName", 
                        // never log customer's actual proxy info, due to security concerns.
                        // Especially since the log can be uploaded and viewed in iNet.
                       string.Empty.PadRight( DockingStation.InetProxyUserName.Length, '*' ),
                       string.Empty.PadRight( config.InetProxyUserName.Length, '*' ) );    
#endif
                config.InetProxyUserName = DockingStation.InetProxyUserName;
            }

            if ( DockingStation.InetProxyPassword != config.InetProxyPassword )
            {
#if DEBUG
                modified = LogUpdate( "InetProxyPassword",
                    DockingStation.InetProxyPassword,
                    config.InetProxyPassword );
#else
                modified = LogUpdate( "InetProxyPassword",
                    // never log customer's actual proxy info, due to security concerns.
                    // Especially since the log can be uploaded and viewed in iNet.
                    string.Empty.PadRight( DockingStation.InetProxyPassword.Length, '*' ),
                    string.Empty.PadRight( config.InetProxyPassword.Length, '*' ) );
#endif
                config.InetProxyPassword = DockingStation.InetProxyPassword;
            }

			if ( DockingStation.LogToSerialPort != config.LogToSerialPort )
			{
				Log.LogToSerialPort = Configuration.ServiceMode ? true : DockingStation.LogToSerialPort; // always log in service mode
				modified = LogUpdate( "LogToSerialPort", DockingStation.LogToSerialPort, config.LogToSerialPort );
				config.LogToSerialPort = DockingStation.LogToSerialPort;
			}

			if (DockingStation.LogToFile != config.LogToFile)
			{
				Log.LogToFile = DockingStation.LogToFile;
				modified = LogUpdate( "LogToFile", DockingStation.LogToFile, config.LogToFile);
				config.LogToFile = DockingStation.LogToFile;
			}

			if ( DockingStation.CalStationGasSchedule != config.CalStationGasSchedule )
			{
				modified = LogUpdate( "CalStationGasSchedule", DockingStation.CalStationGasSchedule, config.CalStationGasSchedule );
				config.CalStationGasSchedule = DockingStation.CalStationGasSchedule;
			}

			if ( DockingStation.CalStationDatalogScheduleEnabled != config.CalStationDatalogScheduleEnabled )
			{
				modified = LogUpdate( "CalStationDatalogScheduleEnabled", DockingStation.CalStationDatalogScheduleEnabled, config.CalStationDatalogScheduleEnabled );
				config.CalStationDatalogScheduleEnabled = DockingStation.CalStationDatalogScheduleEnabled;
			}

            return modified;
        }

        //Suresh 12-SEPTEMBER-2011 INS-2248
        /// <summary>
        /// Update local network settings.  It's assumed these can 
        /// only be edited in Configurator and not in iNet.
        /// </summary>
        /// <param name="config"></param>
        /// <returns>true if any of the network settings are modified. Otherwise, false.</returns>
        private bool UpdateNetworkSettings(DockingStation config, DockingStation.NetworkInfo networksettings) 
        {
            //Log.Assert(UseDockingStation, "UseDockingStation should be true!");

            bool modified = false;

            if (networksettings.IpAddress != config.NetworkSettings.IpAddress)
            {
                modified = LogUpdate("IpAddress", networksettings.IpAddress, config.NetworkSettings.IpAddress);
                config.NetworkSettings.IpAddress = networksettings.IpAddress;
            }

            if (networksettings.SubnetMask != config.NetworkSettings.SubnetMask)
            {
                modified = LogUpdate("SubnetMask", networksettings.SubnetMask, config.NetworkSettings.SubnetMask);
                config.NetworkSettings.SubnetMask = networksettings.SubnetMask;
            }

            if (networksettings.Gateway != config.NetworkSettings.Gateway)
            {
                modified = LogUpdate("Gateway", networksettings.Gateway, config.NetworkSettings.Gateway);
                config.NetworkSettings.Gateway = networksettings.Gateway;
            }

            if (networksettings.DhcpEnabled != config.NetworkSettings.DhcpEnabled)
            {
                modified = LogUpdate("DhcpEnabled", networksettings.DhcpEnabled, config.NetworkSettings.DhcpEnabled);
                config.NetworkSettings.DhcpEnabled = networksettings.DhcpEnabled;
            }

            if (networksettings.DnsPrimary != config.NetworkSettings.DnsPrimary)
            {
                modified = LogUpdate("DnsPrimary", networksettings.DnsPrimary, config.NetworkSettings.DnsPrimary);
                config.NetworkSettings.DnsPrimary = networksettings.DnsPrimary;
            }

            if (networksettings.DnsSecondary != config.NetworkSettings.DnsSecondary)
            {
                modified = LogUpdate("DnsSecondary", networksettings.DnsSecondary, config.NetworkSettings.DnsSecondary);
                config.NetworkSettings.DnsSecondary = networksettings.DnsSecondary;
            }

            if (modified)
                _settingsUpdateEvent.RebootRequired = true;

            return modified;
        }

        /// <summary>
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <returns></returns>
        private DockingStation LoadSettings( string serialNumber )
        {
            Log.Debug( string.Format( "{0}: UseDockingStation={1}", Name, UseDockingStation ) );

            // Assume settings were already given to us (via the UpdateAction) if
            // UseDockingStation has been set.  This would be the case for an update of some
            // setting from VDS.Config.
            if ( this.UseDockingStation == true )
            {
                Log.Debug( string.Format( "{0}: UseDockingStation=true, Using refId={1}", Name, ( this.DockingStation.RefId == DomainModelConstant.NullId ? "NullId" : this.DockingStation.RefId.ToString() ) ) );
                return this.DockingStation;
            }
            // Otherwise, load settings from the database.

            //Suresh 12-SEPTEMBER-2011 INS-2248
            using (DataAccessTransaction trx = new DataAccessTransaction(true))
            {
                // For the SettingsUpdate, find the settings we've previously received from 
                // the iNet server that are kept persisted in the database.
                DockingStation ds = new DockingStationDataAccess().Find(trx);

                if (ds == null)
                {
                    string errMsg = string.Format("{0}: NO DOCKINGSTATION SETTINGS FOUND.", Name);
                    Log.Error(errMsg);
#if DEBUG
                    Log.Debug("DO YOU NEED TO SET UseDockingStation TO TRUE?");
#endif
                    _settingsUpdateEvent.Errors.Add(new DockingStationError(errMsg, DockingStationErrorLevel.Warning));
                    return null;
                }

                //Suresh 12-SEPTEMBER-2011 INS-2248
                ds.ReplacedDSNetworkSettings = new ReplacedNetworkSettingsDataAccess().Find(trx); 

                ds.SerialNumber = serialNumber;

                Log.Debug(string.Format("{0}: Loaded DockingStation Settings, refId={1}", Name, ds.RefId));

                return ds;
            }
            
        }

        private bool LogUpdate( string label, object newValue, object oldValue )
        {
            Log.Debug( string.Format( "{0}: UPDATED {1}: \"{2}\" (\"{3}\")", Name, label, newValue.ToString(), oldValue.ToString() ) );
            return true;
        }

		#endregion

	} // end-class SettingsUpdateOperation

}