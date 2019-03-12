using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class InstrumentDataAccess : DataAccess
    {
        static private string[] _groupSettingsFields = new string[]
        {
	        "REFID",
            "RECUPDATETIMEUTC",
			"EQUIPMENTCODE",
	        "ACCESSCODE",
	        "RECORDINGINTERVAL",
	        "TWATIMEBASE",
			"OOMWARNINGINTERVAL",
			"DOCKINTERVAL",
            "MAINTENANCEINTERVAL",
            "CALINTERVAL",
            "BUMPINTERVAL",
	        "BUMPTHRESHOLD",
	        "BUMPTIMEOUT",
	        "BACKLIGHT",
            "BACKLIGHTTIMEOUT",
	        "LANGUAGE",
            "MAGNETICFIELDDURATION",
			"COMPANYNAME",
			"COMPANYMESSAGE",
			"WLPEERLOSTTHRESHOLD",
			"WLNETWORKLOSTTHRESHOLD",
			"WLREADINGSDEADBAND",
            "WLNETWORKDISCONNECTDELAY",
            "LONEWORKEROKMESSAGEINTERVAL",
            "GPSREADINGINTERVAL"
        };

        private const string ATTR_USER = "USER";
        private const string ATTR_SITE = "SITE";
        private const string ATTR_OPTION = "OPTION";
        private const string ATTR_FAVRF = "FAVRF"; // Favorite Response Factor
		private const string ATTR_WLOPTION = "WLOPTION";

        private static readonly string _updateGroupSettingsSql;
        private static readonly string _insertGroupSettingsSql;

        static InstrumentDataAccess()
        {
            // Build static update and insert queries that are kept for re-use.

            _insertGroupSettingsSql = string.Format( "INSERT INTO INSTRUMENTGROUPSETTINGS ( {0} ) VALUES ( {1} )",
                CreateGroupSettingsFieldList( string.Empty ), CreateGroupSettingsFieldList( "@" ) );

            StringBuilder sqlBuilder = new StringBuilder( "UPDATE INSTRUMENTGROUPSETTINGS SET " );
            for ( int i = 1 /*we skip the "REFID" field*/; i < _groupSettingsFields.Length; i++ )
            {
                if ( i > 1 ) sqlBuilder.Append( ", " );
                sqlBuilder.Append( _groupSettingsFields[ i ] );
                sqlBuilder.Append( " = @" );
                sqlBuilder.Append( _groupSettingsFields[ i ] );
            }
            sqlBuilder.Append( " WHERE REFID = @REFID" );
            _updateGroupSettingsSql = sqlBuilder.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns>
        /// "FIELD1, "FIELD2", FIELD3", etc.
        /// </returns>
        private static string CreateGroupSettingsFieldList( string prefix )
        {
            StringBuilder sb = new StringBuilder();
            for ( int i = 0; i < _groupSettingsFields.Length; i++ )
            {
                if ( sb.Length > 0 ) sb.Append( ", " );
                if ( prefix != string.Empty ) sb.Append( prefix );
                sb.Append( _groupSettingsFields[ i ] );
            }
            return sb.ToString();
        }

        private void AddGroupSettingsSaveParameters( IDbCommand cmd, InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            // The PK doesn't enforce that only one null record can exist.  As far at the database is concerned, 
			// NULL values aren't equal to anything, including other NULL values.
            cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );
            cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
			// EquipmentCode should NOT be null for a default settings group.  It should be null for a settings group for specific instrument serial numbers.
			cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTCODE", instSettingsGroup.EquipmentCode ) );
            cmd.Parameters.Add( GetDataParameter( "@ACCESSCODE", instSettingsGroup.Instrument.AccessCode ) );
            cmd.Parameters.Add( GetDataParameter( "@RECORDINGINTERVAL", instSettingsGroup.Instrument.RecordingInterval ) );
            cmd.Parameters.Add( GetDataParameter( "@TWATIMEBASE", instSettingsGroup.Instrument.TWATimeBase ) );
			cmd.Parameters.Add( GetDataParameter( "@OOMWARNINGINTERVAL", instSettingsGroup.Instrument.OomWarningInterval ) );
			cmd.Parameters.Add( GetDataParameter( "@DOCKINTERVAL", instSettingsGroup.Instrument.DockInterval ) );
            cmd.Parameters.Add( GetDataParameter( "@MAINTENANCEINTERVAL", instSettingsGroup.Instrument.MaintenanceInterval ) );
            cmd.Parameters.Add( GetDataParameter( "@CALINTERVAL", instSettingsGroup.Instrument.CalibrationInterval ) );
            cmd.Parameters.Add( GetDataParameter( "@BUMPINTERVAL", instSettingsGroup.Instrument.BumpInterval ) );
            cmd.Parameters.Add( GetDataParameter( "@BUMPTHRESHOLD", instSettingsGroup.Instrument.BumpThreshold ) );
            cmd.Parameters.Add( GetDataParameter( "@BUMPTIMEOUT", instSettingsGroup.Instrument.BumpTimeout ) );
            cmd.Parameters.Add( GetDataParameter( "@BACKLIGHT", instSettingsGroup.Instrument.Backlight.ToString() ) );
			cmd.Parameters.Add( GetDataParameter( "@BACKLIGHTTIMEOUT", instSettingsGroup.Instrument.BacklightTimeout.ToString() ) );
            cmd.Parameters.Add( GetDataParameter( "@LANGUAGE", instSettingsGroup.Instrument.Language.Code ) );
            cmd.Parameters.Add( GetDataParameter( "@MAGNETICFIELDDURATION", instSettingsGroup.Instrument.MagneticFieldDuration ) );
			cmd.Parameters.Add( GetDataParameter( "@COMPANYNAME", instSettingsGroup.Instrument.CompanyName ) );
			cmd.Parameters.Add( GetDataParameter( "@COMPANYMESSAGE", instSettingsGroup.Instrument.CompanyMessage ) );
			cmd.Parameters.Add( GetDataParameter( "@WLPEERLOSTTHRESHOLD", instSettingsGroup.Instrument.WirelessPeerLostThreshold ) );
			cmd.Parameters.Add( GetDataParameter( "@WLNETWORKLOSTTHRESHOLD", instSettingsGroup.Instrument.WirelessNetworkLostThreshold ) );
			cmd.Parameters.Add( GetDataParameter( "@WLREADINGSDEADBAND", instSettingsGroup.Instrument.WirelessReadingsDeadband ) );
            cmd.Parameters.Add( GetDataParameter( "@WLNETWORKDISCONNECTDELAY", instSettingsGroup.Instrument.WirelessNetworkDisconnectDelay ) );
            cmd.Parameters.Add( GetDataParameter( "@LONEWORKEROKMESSAGEINTERVAL", instSettingsGroup.Instrument.LoneWorkerOkMessageInterval ) );
            cmd.Parameters.Add( GetDataParameter( "@GPSREADINGINTERVAL", instSettingsGroup.Instrument.GpsReadingInterval ) );
        }

        public Instrument FindDefaultSettings( string equipmentCode, DataAccessTransaction trx )
        {
			if ( equipmentCode == null )
			{
				// This method should not be called and provided a null equipmentCode.
				// Default settings must have an equipment code set.
				Log.Error( "There are no default INSTRUMENT records for a null equipment code." );
				return null;
			}

			// A default settings group should not have any child records in the INSTRUMENTGROUP 
			// table.  Not even one where the SN is null.

            // although there is only one default allowed, our database allows multiples (because  
            // we don't trust the server to not return multiples).  Therefore, we sort any potential 
            // multiples by  REFID in descending order (assuming the largest REFID is the newest).
            string sql = "SELECT * FROM INSTRUMENTGROUPSETTINGS WHERE EQUIPMENTCODE = @EQUIPMENTCODE ORDER BY REFID DESC";

            IList<InstrumentSettingsGroup> groupList = null;

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
				cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTCODE", equipmentCode ) );
				
                groupList = FindGroupSettings( cmd, trx );
            }

            if ( groupList == null || groupList.Count == 0 )
            {
                Log.Error(  "No default INSTRUMENT record found." );
                return null;
            }

            if ( groupList.Count > 1 )
            {
                string msg = string.Format( "Multiple default settings groups found for equipment code \"{0}\": ", equipmentCode );
                for ( int i = 0; i < groupList.Count; i++ )
                {
                    if ( i > 0 ) msg += ",";
                    msg += groupList[ i ].RefId;
                };
                Log.Warning( msg );
                trx.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
            }

            InstrumentSettingsGroup instSettingsGroup = groupList[ 0 ];

            LoadSettingsGroup( instSettingsGroup, trx );

            return instSettingsGroup.Instrument;
        }

        /// <summary>
        /// For a specific GroupId, find all serial numbers belonging to that group.
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public List<string> FindSerialNumbersByRefId( long refId, DataAccessTransaction trx )
        {
            List<string> strList = new List<string>();

            string sql = "SELECT SN FROM INSTRUMENTGROUP WHERE REFID = @REFID";

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );

                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    try
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
                        while ( reader.Read() )
                        {
                            strList.Add( SqlSafeGetString( reader, ordinals[ "SN" ] ) );
                        }
                    }
                    catch ( Exception ex )
                    {
                        throw new DataAccessException( sql, ex );
                    }
                }
            }
            return strList;
        }

        /// <summary>
        /// Find group settings for a group that the specified instrument belongs to.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="trx"></param>
        /// <returns>Also returns null if the passed-in serial number is null or empty.</returns>
        public Instrument FindGroupSettingsBySerialNumber( string serialNumber, DataAccessTransaction trx )
        {
            if ( string.IsNullOrEmpty( serialNumber ) )
                return null;

            // Although there is only one settings group allowed per instrument, our database allows multiples
            // (because we don't trust the server to not return multiples).  Therefore, we sort any 
            // potential multiples by REFID in descending order (assuming the largest REFID is the newest).
            string sql = "SELECT INSTRUMENTGROUPSETTINGS.* FROM INSTRUMENTGROUPSETTINGS, INSTRUMENTGROUP WHERE INSTRUMENTGROUPSETTINGS.REFID = INSTRUMENTGROUP.REFID AND INSTRUMENTGROUP.SN = @SN ORDER BY REFID DESC";

            IList<InstrumentSettingsGroup> groupList = null;

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@SN", serialNumber ) );

                groupList = FindGroupSettings( cmd, trx );
            }

            if ( groupList == null || groupList.Count == 0 )
            {
                Log.Error( string.Format( "No INSTRUMENTGROUPSETTINGS record found for S/N \"{0}\".", serialNumber ) );
                return null;
            }

            if ( groupList.Count > 1 )
            {
                string msg = string.Format( "Multiple settings groups found for S/N \"{0}\": ", serialNumber );
                for ( int i = 0; i < groupList.Count; i++ )
                {
                    if ( i > 0 ) msg += ",";
                    msg += groupList[ i ].RefId;
                };
                Log.Warning( msg );
                trx.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
            }

            InstrumentSettingsGroup instSettingsGroup = groupList[ 0 ];

            LoadSettingsGroup( instSettingsGroup, trx );

            return instSettingsGroup.Instrument;
        }

        private void LoadSettingsGroup( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
			// We call LoadWirelessModuleSettings first so the WirelessModule can be created on the Instrument
			// if needed which will then be used by LoadGroupProperties for WLOPTIONs.
			LoadWirelessModuleSettings( instSettingsGroup, trx );

            LoadGroupProperties( instSettingsGroup, trx );

            LoadCustomResponseFactors( instSettingsGroup, trx );

			LoadAlarmActionMessages( instSettingsGroup, trx );

            LoadSensorSettings( instSettingsGroup, trx );
        }

        private IList<InstrumentSettingsGroup> FindGroupSettings( IDbCommand cmd, DataAccessTransaction trx )
        {
            List<InstrumentSettingsGroup> groupList = new List<InstrumentSettingsGroup>();

            try
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    while ( reader.Read() )
                    {
                        Instrument instrument = new Instrument();

                        instrument.RefId = SqlSafeGetLong( reader, ordinals["REFID"] );
						string equipmentCode = SqlSafeGetString( reader, ordinals["EQUIPMENTCODE"] );
						instrument.Type = Device.GetDeviceType( equipmentCode ); // Type will be Unknown if settings are for specific serial numbers.
                        instrument.AccessCode = SqlSafeGetString( reader, ordinals[ "ACCESSCODE" ] );
                        instrument.RecordingInterval = SqlSafeGetInt( reader, ordinals[ "RECORDINGINTERVAL" ] );
                        instrument.TWATimeBase = SqlSafeGetInt( reader, ordinals[ "TWATIMEBASE" ] );
						instrument.OomWarningInterval = SqlSafeGetInt( reader, ordinals["OOMWARNINGINTERVAL"] );
						instrument.DockInterval = SqlSafeGetInt( reader, ordinals["DOCKINTERVAL"] );
                        instrument.MaintenanceInterval = SqlSafeGetInt( reader, ordinals["MAINTENANCEINTERVAL"] );
                        instrument.CalibrationInterval = SqlSafeGetShort( reader, ordinals[ "CALINTERVAL" ] );
                        instrument.BumpInterval = SqlSafeGetDouble( reader, ordinals[ "BUMPINTERVAL" ] );
                        instrument.BumpThreshold = SqlSafeGetInt( reader, ordinals[ "BUMPTHRESHOLD" ] );
                        instrument.BumpTimeout = SqlSafeGetInt( reader, ordinals[ "BUMPTIMEOUT" ] );
                        string backlightString = SqlSafeGetString( reader, ordinals[ "BACKLIGHT" ] );
                        try
                        {
                            instrument.Backlight = (BacklightSetting)Enum.Parse( typeof( BacklightSetting ), backlightString, true );
                        }
                        catch ( ArgumentException ex ) // will get ArgumentException if unable to Parse due to illegal value.
                        {
                            string msg = string.Format( "Unable to parse Backlight setting \"{0}\".", backlightString );
                            Log.Error( msg, ex );
                            trx.Errors.Add( new DockingStationError( msg + " - " + ex.ToString(), DockingStationErrorLevel.Warning ) );
                            instrument.Backlight = BacklightSetting.Unknown;
                        }
                        instrument.BacklightTimeout = SqlSafeGetInt( reader, ordinals[ "BACKLIGHTTIMEOUT" ] ); //Suresh 30-SEPTEMBER-2011 INS-2277
                        instrument.Language.Code = SqlSafeGetString( reader, ordinals[ "LANGUAGE" ] );
                        instrument.MagneticFieldDuration = SqlSafeGetInt( reader, ordinals[ "MAGNETICFIELDDURATION" ] );
						instrument.CompanyName = SqlSafeGetString( reader, ordinals[ "COMPANYNAME" ] );
						instrument.CompanyMessage = SqlSafeGetString( reader, ordinals["COMPANYMESSAGE"] );
						instrument.WirelessPeerLostThreshold = SqlSafeGetInt( reader, ordinals["WLPEERLOSTTHRESHOLD"] );
						instrument.WirelessNetworkLostThreshold = SqlSafeGetInt( reader, ordinals["WLNETWORKLOSTTHRESHOLD"] );
						instrument.WirelessReadingsDeadband = SqlSafeGetInt( reader, ordinals["WLREADINGSDEADBAND"] );
                        instrument.WirelessNetworkDisconnectDelay = SqlSafeGetInt( reader, ordinals["WLNETWORKDISCONNECTDELAY"] );
                        instrument.LoneWorkerOkMessageInterval = SqlSafeGetInt( reader, ordinals["LONEWORKEROKMESSAGEINTERVAL"] );
                        instrument.GpsReadingInterval = SqlSafeGetInt( reader, ordinals["GPSREADINGINTERVAL"] );
                     
                        // This InstrumentSettingsGroup will only be used internally and won't be returned back to the public
                        // caller. There's no need for us to fill in the serial numbers array.
                        InstrumentSettingsGroup instSettingsGroup = new InstrumentSettingsGroup( instrument.RefId, equipmentCode, new string[0], instrument );

                        groupList.Add( instSettingsGroup );
                    }

                    return groupList;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( cmd.CommandText, ex );
            }

        }

        private void LoadGroupProperties( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            string sql = "SELECT * FROM INSTRUMENTGROUPPROPERTY WHERE REFID = @REFID ORDER BY ATTRIBUTE, SEQUENCE";
            
            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );

                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        while ( reader.Read() )
                        {
                            string attribute = SqlSafeGetString( reader, ordinals[ "ATTRIBUTE" ] );

                            string value = SqlSafeGetString( reader, ordinals[ "VALUE" ] );

							if ( attribute == ATTR_OPTION )
							{
								DeviceOption deviceOption = new DeviceOption( value, true );
								instSettingsGroup.Instrument.Options.Add( deviceOption );
							}

							else if ( attribute == ATTR_WLOPTION )
							{
								DeviceOption wirelessOption = new DeviceOption( value, true );

								// WirelessModule should have been created by LoadWirelessModuleSettings.
								if ( instSettingsGroup.Instrument.WirelessModule != null )
									instSettingsGroup.Instrument.WirelessModule.Options.Add( wirelessOption );
							}

							else if ( attribute == ATTR_FAVRF )
								instSettingsGroup.Instrument.FavoritePidFactors.Add( value );
							
							else
							{
								string msg = string.Format( "Unrecognized InstrumentProperty: attr=\"{0}\", value=\"{1}\"", attribute, value );
								Log.Error( msg );
								trx.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
							}

                        }  // end-while

                    } // end-using reader
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
        }

        /// <summary>
        /// Find settings specific to a particular instrument.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public Instrument FindSettingsBySerialNumber( string serialNumber, DataAccessTransaction trx )
        {
            if ( serialNumber == null )
                return null;

            Instrument instrument = null;

            // SN is primary key, so we shouldn't have to worry about different 
            // instrumentiettings records with the same serial number.
            string sql = "SELECT * FROM INSTRUMENTSETTINGS WHERE SN = @SN";

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@SN", serialNumber ) );

                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
                    if ( reader.Read() )
                    {
                        instrument = new Instrument();

                        instrument.SerialNumber = serialNumber;
                        instrument.ActiveUser = SqlSafeGetString( reader, ordinals["ACTIVEUSER"] );
                        instrument.ActiveSite = SqlSafeGetString( reader, ordinals["ACTIVESITE"] );
                        instrument.AccessLevel = SqlSafeGetShort( reader, ordinals["ACCESSLEVEL"] );
                    }
                }
            }

            if ( instrument == null )
                return null;

            LoadInstrumentProperties( instrument, trx );

            return instrument;
        }

        /// <summary>
        /// Find both global and instrument-specific settings.
        /// </summary>
        /// <param name="serialNumber"></param>
		/// <param name="instrumentType"></param>
        /// <returns></returns>
        public Instrument FindApplicableSettings(string serialNumber, DeviceType instrumentType )
        {
            using (DataAccessTransaction trx = new DataAccessTransaction(true))
            {
				return FindApplicableSettings( serialNumber, instrumentType, trx );
            }
        }

        /// <summary>
        /// Find both global and instrument-specific settings.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public Instrument FindApplicableSettings(string serialNumber, DeviceType instrumentType, DataAccessTransaction trx)
        {
            InstrumentDataAccess dataAccess = new InstrumentDataAccess();

			// default settings will have an equipment code
			string equipmentCode = instrumentType.ToString();

            // First, try and find settings specific to this instrument.
            Instrument groupSettings = dataAccess.FindGroupSettingsBySerialNumber(serialNumber, trx);

            // If no instrument-specific settings found, then use the default settings.
            if (groupSettings == null)
                groupSettings = dataAccess.FindDefaultSettings(equipmentCode, trx);

            if (groupSettings == null)
                return groupSettings;

            // Look for settings specific to this specific instrument.
            Instrument instrument = dataAccess.FindSettingsBySerialNumber(serialNumber, trx);

            // If we find instrument-specific settings, then merge them into the group settings.
            if (instrument != null)
            {
                groupSettings.ActiveUser = instrument.ActiveUser;
                groupSettings.ActiveSite = instrument.ActiveSite;
                groupSettings.Users = instrument.Users;
                groupSettings.Sites = instrument.Sites;
                groupSettings.AccessLevel = instrument.AccessLevel;
            }

            return groupSettings;
        }

        /// <summary>
        /// Find and load properties for a specific instrument.
        /// </summary>
        /// <param name="instrument"></param>
        /// <param name="trx"></param>
        private void LoadInstrumentProperties( Instrument instrument, DataAccessTransaction trx )
        {
            string sql = "SELECT * FROM INSTRUMENTPROPERTY WHERE SN = @SN ORDER BY ATTRIBUTE, SEQUENCE";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SN", instrument.SerialNumber ) );

                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        while ( reader.Read() )
                        {
                            string attribute = SqlSafeGetString( reader, ordinals["ATTRIBUTE"] );

                            string value = SqlSafeGetString( reader, ordinals["VALUE"] );

                            if ( attribute == ATTR_USER )
                                instrument.Users.Add( value );

                            else if ( attribute == ATTR_SITE )
                                instrument.Sites.Add( value );

                            else
                            {
                                string msg = string.Format( "Unrecognized InstrumentProperty: attr=\"{0}\", value=\"{1}\"", attribute, value );
                                Log.Error( msg );
                                trx.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                            }

                        }  // end-while

                    } // end-using reader
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
        }

        /// <summary>
        /// Delete the InstrumentSettingsGroup with the specified RefId, plus all its child table records.
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public bool Delete( long refId, DataAccessTransaction trx )
        {
            // We only have to explicitly delete the INSTRUMENTGROUPSETTINGS record.
            // All the children will be deleted via a cascading delete.
            return DeleteFromIdTable( "INSTRUMENTGROUPSETTINGS", refId, trx );
        }

        /// <summary>
        /// Save the passed-in InstrumentSettingsGroup. Data is stored in the InstrumentGroupSettings
        /// table and its child tables such as InstrumentGroup, and InstrumentGroupProperty, etc.
        /// </summary>
        /// <param name="instSettingsGroup"></param>
        /// <param name="trx"></param>
        public void Save( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
			// If this is a default settings group, then delete any old default settings groups with the
			// same equipment code because there should always be exactly one default per equipment code.
            if ( instSettingsGroup.Default )
            {
				DeleteDefaultSettingsGroup( instSettingsGroup.EquipmentCode, trx );
            }

            // There should be no reason to bother with the Hint since, theorectically,
            /// the server only ever gives us new data (i.e., needs inserted), and never
            // data that needs updated.

            // If an Insert hint is passed in, they we first try and insert
            // and then do an update if the insert fails.  It's intended that
            // the caller would pass in an Insert hint when it thinks the data
            // being inserted is likely new (such as the very first time a docking station
            // is saving its event journals).
            //if ( trx.Hint == DataAccessHint.Insert )
            {
                // First try and insert.  If that fails, then it's likely because
                // the record already exists.  So then try and update it instead.
                if ( !InsertGroupSettings( instSettingsGroup, trx ) )
                    UpdateGroupSettings( instSettingsGroup, trx );
            }

            //else
            //{
            //    // First try and update.  If that fails, then it's likely because
            //    // the record doesn't already exist.  So then try and insert.
            //    if ( !UpdateGroupSettings( instSettingsGroup, trx ) )
            //        InsertGroupSettings( instSettingsGroup, trx );
            //}

            // The following series of 'Save' calls will first delete any old records
            // with the refId, then re-insert as all new records...
            SaveInstrumentGroup( instSettingsGroup, trx );
            SaveInstrumentGroupProperties( instSettingsGroup, trx );
			SaveAlarmActionMessages( instSettingsGroup, trx );
            SaveCustomResponseFactors( instSettingsGroup, trx );
            SaveSensors( instSettingsGroup, trx );
			SaveWirelessModuleSettings( instSettingsGroup, trx );
        }

        private bool UpdateGroupSettings( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            try
            {
                using ( IDbCommand cmd = GetCommand( _updateGroupSettingsSql, trx ) )
                {
                    AddGroupSettingsSaveParameters( cmd, instSettingsGroup, trx ); 

                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( _updateGroupSettingsSql, ex );
            }
        }

        /// <summary>
        /// Saves the passed-in Instrument. Data is stored in the InstrumentSettings table
        /// table and its child InstrumentProperty table.
        /// </summary>
        /// <param name="instrument"></param>
        /// <param name="trx"></param>
        public void Save( Instrument instrument, DataAccessTransaction trx )
        {
            // There should be no reason to bother with the Hint since, theorectically,
            // the server only ever gives us new data (i.e., needs inserted), and never
            // data that needs updated.

            // If an Insert hint is passed in, they we first try and insert
            // and then do an update if the insert fails.  It's intended that
            // the caller would pass in an Insert hint when it thinks the data
            // being inserted is likely new (such as the very first time a docking station
            // is saving its event journals).
            //if ( trx.Hint == DataAccessHint.Insert )
            {
                // First try and insert.  If that fails, then it's likely because
                // the record already exists.  So then try and update it instead.
                if ( !InsertInstrument( instrument, trx ) )
                    UpdateInstrument( instrument, trx );
            }

            //else
            //{
            //    // First try and update.  If that fails, then it's likely because
            //    // the record doesn't already exist.  So then try and insert.
            //    if ( !UpdateInstrument( instrument, trx ) )
            //        InsertInstrument( instrument, trx );

            //}

            SaveInstrumentProperties( instrument, trx );
        }

        private bool UpdateInstrument( Instrument instrument, DataAccessTransaction trx )
        {
            string sql = "UPDATE INSTRUMENTSETTINGS SET ACTIVEUSER = @ACTIVEUSER, ACTIVESITE = @ACTIVESITE, ACCESSLEVEL = @ACCESSLEVEL WHERE SN = @SN";
            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SN", instrument.SerialNumber ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACTIVEUSER", instrument.ActiveUser ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACTIVESITE", instrument.ActiveSite ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACCESSLEVEL", instrument.AccessLevel ) );

                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
        }


        private bool InsertInstrument( Instrument instrument, DataAccessTransaction trx )
        {
            string sql = "INSERT INTO INSTRUMENTSETTINGS ( SN, ACTIVEUSER, ACTIVESITE, ACCESSLEVEL ) VALUES ( @SN, @ACTIVEUSER, @ACTIVESITE, @ACCESSLEVEL )";
            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SN", instrument.SerialNumber ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACTIVEUSER", instrument.ActiveUser ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACTIVESITE", instrument.ActiveSite ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACCESSLEVEL", instrument.AccessLevel ) );

                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                if ( ( ex is SQLiteException ) && ( ( (SQLiteException)ex ).ErrorCode == SQLiteErrorCode.Constraint ) )
                    return false;  // assume we have a 'duplicate' error.

                throw new DataAccessException( sql, ex );
            }
        }

        private bool InsertGroupSettings( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            try
            {
                using ( IDbCommand cmd = GetCommand( _insertGroupSettingsSql, trx ) )
                {
                    AddGroupSettingsSaveParameters( cmd, instSettingsGroup, trx ); // 

                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                if ( ( ex is SQLiteException ) && ( ( (SQLiteException)ex ).ErrorCode == SQLiteErrorCode.Constraint ) )
                    return false;  // assume we have a 'duplicate' error.

                throw new DataAccessException( _insertGroupSettingsSql, ex );
            }
        }

        private void SaveInstrumentGroup( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            // Delete any old record for the refId, then re-insert as a new record.

            DeleteFromIdTable( "INSTRUMENTGROUP", instSettingsGroup.RefId, trx );

            InsertInstrumentGroup( instSettingsGroup, trx );
        }

        private bool DeleteFromIdTable( string tableName, long refId, DataAccessTransaction trx )
        {
            string sql = string.Format( "DELETE FROM {0} WHERE REFID = @REFID", tableName );

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "table={0}, refId={1}, {2}", tableName, refId, sql ), ex );
            }
        }

		/// <summary>
		/// Deletes default setting groups for a specific equipment code.
		/// </summary>
		/// <param name="tableName"></param>
		/// <param name="equipmentCode"></param>
		/// <param name="trx"></param>
		/// <returns></returns>
		public bool DeleteDefaultSettingsGroup( string equipmentCode, DataAccessTransaction trx )
		{
			string tableName = "INSTRUMENTGROUPSETTINGS";

			string sql = string.Format( "DELETE FROM {0} WHERE EQUIPMENTCODE = @EQUIPMENTCODE", tableName );

			try
			{
				using ( IDbCommand cmd = GetCommand( sql, trx ) )
				{
					cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTCODE", equipmentCode ) );
					return cmd.ExecuteNonQuery() > 0;
				}
			}
			catch ( Exception ex )
			{
				throw new DataAccessException( string.Format( "table={0}, equipmentCode={1}, {2}", tableName, equipmentCode, sql ), ex );
			}
		}

        private bool DeleteFromSnTable( string tableName, string sn, DataAccessTransaction trx )
        {
            string sql = string.Format( "DELETE FROM {0} WHERE SN = @SN", tableName );

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SN", sn ) );
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "table={0}, SN={1}, {2}", tableName, sn, sql ), ex );
            }
        }

        private void InsertInstrumentGroup( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx  )
        {
            string sql = "INSERT INTO INSTRUMENTGROUP ( REFID, SN ) VALUES ( @REFID, @SN )";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    if ( instSettingsGroup.SerialNumbers.Count > 0 )
                    {
                        foreach ( string sn in instSettingsGroup.SerialNumbers )
                        {
                            cmd.Parameters.Clear();
                            cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );
                            cmd.Parameters.Add( GetDataParameter( "@SN", sn ) ); // sn should never be null
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
						// AS OF V6.2, DEFAULT SETTINGS HAVE AN EQUIPMENT CODE SO WE DON'T NEED TO INSTERT A NULL
						// RECORD INTO THE INSTRUMENTGROUP TABLE ANYMORE.

                        // Store a special record containing a null serial number that identifies it as being the 
                        // 'default groupsettings.
                        //cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );
                        //cmd.Parameters.Add( GetDataParameter( "@SN", null ) );
                        //cmd.ExecuteNonQuery();
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
        }

        private void SaveInstrumentGroupProperties( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            // delete any old properties for the REFID (we don't need to care if we're inserting
            // new data versus updating old data; we just try and always delete the old data)
            DeleteFromIdTable( "INSTRUMENTGROUPPROPERTY", instSettingsGroup.RefId, trx );

            // Insert options. It's assumed that all options in the list are the Enabled options.
            for ( int i = 0; i < instSettingsGroup.Instrument.Options.Count; i++ )
                InsertInstrumentGroupProperty( instSettingsGroup.RefId, ATTR_OPTION, i + 1, instSettingsGroup.Instrument.Options[ i ].Code, trx );

             // Insert Favorite Response Factors
            for ( int i = 0; i < instSettingsGroup.Instrument.FavoritePidFactors.Count; i++ )
                InsertInstrumentGroupProperty( instSettingsGroup.RefId, ATTR_FAVRF, i + 1, instSettingsGroup.Instrument.FavoritePidFactors[ i ], trx );

			// Insert Wireless Options
			WirelessModule wirelessModule = instSettingsGroup.Instrument.WirelessModule;
			if ( wirelessModule != null && wirelessModule.Options.Count > 0 )
			{
                for ( int i = 0; i < wirelessModule.Options.Count; i++ )
				{
                    InsertInstrumentGroupProperty( instSettingsGroup.RefId, ATTR_WLOPTION, i + 1, wirelessModule.Options[ i ].Code, trx );
				}
			}
        }

		private bool InsertInstrumentGroupProperty( long refId, string attribute, int sequence, string value, DataAccessTransaction trx )
        {
            string sql = "INSERT INTO INSTRUMENTGROUPPROPERTY ( REFID, ATTRIBUTE, SEQUENCE, VALUE ) VALUES ( @REFID, @ATTRIBUTE, @SEQUENCE, @VALUE )";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );
                    cmd.Parameters.Add( GetDataParameter( "@ATTRIBUTE", attribute ) );
                    cmd.Parameters.Add( GetDataParameter( "@SEQUENCE", (short)sequence ) );
                    cmd.Parameters.Add( GetDataParameter( "@VALUE", value ) );

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "refId={0}, attr={1}, seq={2}, value={3}, {4}", refId, attribute, sequence, value, sql ), ex );
            }
        }


        private void SaveInstrumentProperties( Instrument instrument, DataAccessTransaction trx )
        {
            // delete any old properties for the REFID (we don't need to care if we're inserting
            // new data versus updating old data; we just try and always delete the old data)
            DeleteFromSnTable( "INSTRUMENTPROPERTY", instrument.SerialNumber, trx );

            // Insert users
            for ( int i = 0; i < instrument.Users.Count; i++ )
                InsertInstrumentProperty( instrument.SerialNumber, ATTR_USER, i + 1, instrument.Users[i], trx );

            // Insert sites
            for ( int i = 0; i < instrument.Sites.Count; i++ )
                InsertInstrumentProperty( instrument.SerialNumber, ATTR_SITE, i + 1, instrument.Sites[i], trx );
        }



        private bool InsertInstrumentProperty( string sn, string attribute, int sequence, string value, DataAccessTransaction trx )
        {
            string sql = "INSERT INTO INSTRUMENTPROPERTY ( SN, ATTRIBUTE, SEQUENCE, VALUE ) VALUES ( @SN, @ATTRIBUTE, @SEQUENCE, @VALUE )";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SN", sn ) );
                    cmd.Parameters.Add( GetDataParameter( "@ATTRIBUTE", attribute ) );
                    cmd.Parameters.Add( GetDataParameter( "@SEQUENCE", (short)sequence ) );
                    cmd.Parameters.Add( GetDataParameter( "@VALUE", value ) );

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "sn={0}, attr={1}, seq={2}, value={3}, {4}", sn, attribute, sequence, value, sql ), ex );
            }
        }

		private void SaveAlarmActionMessages( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
		{
			DeleteFromIdTable( "ALARMACTIONMESSAGES", instSettingsGroup.RefId, trx );

			for ( int i = 0; i < instSettingsGroup.Instrument.AlarmActionMessages.Count; i++ )
				InsertAlarmActionMessages( instSettingsGroup.RefId, instSettingsGroup.Instrument.AlarmActionMessages[i], trx );
		}

		private bool InsertAlarmActionMessages( long refId, AlarmActionMessages aam, DataAccessTransaction trx )
		{
			string sql = "INSERT INTO ALARMACTIONMESSAGES ( REFID, SENSORCODE, MESSAGEGASALERT, MESSAGELOWALARM, MESSAGEHIGHALARM, MESSAGESTELALARM, MESSAGETWAALARM ) VALUES ( @REFID, @SENSORCODE, @MESSAGEGASALERT, @MESSAGELOWALARM, @MESSAGEHIGHALARM, @MESSAGESTELALARM, @MESSAGETWAALARM )";

			try
			{
				using ( IDbCommand cmd = GetCommand( sql, trx ) )
				{
					cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );
					cmd.Parameters.Add( GetDataParameter( "@SENSORCODE", aam.SensorCode ) );
					cmd.Parameters.Add( GetDataParameter( "@MESSAGEGASALERT", aam.GasAlertMessage ) );
					cmd.Parameters.Add( GetDataParameter( "@MESSAGELOWALARM", aam.LowAlarmMessage ) );
					cmd.Parameters.Add( GetDataParameter( "@MESSAGEHIGHALARM", aam.HighAlarmMessage ) );
					cmd.Parameters.Add( GetDataParameter( "@MESSAGESTELALARM", aam.StelAlarmMessage ) );
					cmd.Parameters.Add( GetDataParameter( "@MESSAGETWAALARM", aam.TwaAlarmMessage ) );
					return cmd.ExecuteNonQuery() > 0;
				}
			}
			catch ( Exception ex )
			{
				throw new DataAccessException( string.Format( "refId={0}, aam={1}, {2}", refId, aam.SensorCode, sql ), ex );
			}
		}

		private void SaveWirelessModuleSettings( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
		{
			DeleteFromIdTable( "WIRELESSMODULESETTINGS", instSettingsGroup.RefId, trx );

			if ( instSettingsGroup.Instrument.WirelessModule != null )
				InsertWirelessModuleSettings( instSettingsGroup.RefId, instSettingsGroup.Instrument.WirelessModule, trx );
		}

		private bool InsertWirelessModuleSettings( long refId, WirelessModule module, DataAccessTransaction trx )
		{
            string sql = "INSERT INTO WIRELESSMODULESETTINGS ( REFID, TRANSMISSIONINTERVAL, ENCRYPTIONKEY, MESSAGEHOPS, MAXPEERS, PRIMARYCHANNEL, SECONDARYCHANNEL, ACTIVECHANNELMASK, WIRELESSBINDINGTIMEOUT, WIRELESSFEATUREBITS, LISTENINGPOSTCHANNELMASK ) " +
                         "VALUES ( @REFID, @TRANSMISSIONINTERVAL, @ENCRYPTIONKEY, @MESSAGEHOPS, @MAXPEERS, @PRIMARYCHANNEL, @SECONDARYCHANNEL, @ACTIVECHANNELMASK, @WIRELESSBINDINGTIMEOUT, @WIRELESSFEATUREBITS, @LISTENINGPOSTCHANNELMASK )";

			try
			{
				using ( IDbCommand cmd = GetCommand( sql, trx ) )
				{
					cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );
					cmd.Parameters.Add( GetDataParameter( "@TRANSMISSIONINTERVAL", module.TransmissionInterval ) );
					cmd.Parameters.Add( GetDataParameter( "@ENCRYPTIONKEY", module.EncryptionKey ) );
					cmd.Parameters.Add( GetDataParameter( "@MESSAGEHOPS", module.MessageHops ) );
					cmd.Parameters.Add( GetDataParameter( "@MAXPEERS", module.MaxPeers ) );
					cmd.Parameters.Add( GetDataParameter( "@PRIMARYCHANNEL", module.PrimaryChannel ) );
					cmd.Parameters.Add( GetDataParameter( "@SECONDARYCHANNEL", module.SecondaryChannel ) );
					cmd.Parameters.Add( GetDataParameter( "@ACTIVECHANNELMASK", module.ActiveChannelMask ) );
                    cmd.Parameters.Add( GetDataParameter( "@WIRELESSBINDINGTIMEOUT", module.WirelessBindingTimeout ) );
                    cmd.Parameters.Add( GetDataParameter( "@WIRELESSFEATUREBITS", module.WirelessFeatureBits ) );
                    cmd.Parameters.Add( GetDataParameter( "@LISTENINGPOSTCHANNELMASK", module.ListeningPostChannelMask));
					return cmd.ExecuteNonQuery() > 0;
				}
			}
			catch ( Exception ex )
			{
				throw new DataAccessException( string.Format( "refId={0}, {1}", refId, sql ), ex );
			}
		}

        private void SaveCustomResponseFactors( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            DeleteFromIdTable( "CUSTOMRESPONSEFACTOR", instSettingsGroup.RefId, trx );

            for ( int i = 0; i < instSettingsGroup.Instrument.CustomPidFactors.Count; i++ )
                InsertCustomResponseFactor( instSettingsGroup.RefId, instSettingsGroup.Instrument.CustomPidFactors[i], i + 1, trx );
        }

        private bool InsertCustomResponseFactor( long refId, ResponseFactor rf, int sequence, DataAccessTransaction trx  )
        {
            string sql = "INSERT INTO CUSTOMRESPONSEFACTOR ( REFID, SEQUENCE, NAME, GASCODE, VALUE ) VALUES ( @REFID, @SEQUENCE, @NAME, @GASCODE, @VALUE )";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );
                    cmd.Parameters.Add( GetDataParameter( "@SEQUENCE", sequence ) );
                    cmd.Parameters.Add( GetDataParameter( "@NAME", rf.Name ) );
                    cmd.Parameters.Add( GetDataParameter( "@GASCODE", rf.GasCode ) );
                    cmd.Parameters.Add( GetDataParameter( "@VALUE", rf.Value ) );
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "refId={0}, rf={1}, seq={2}, {3}", refId, rf, sequence, sql ), ex );
            }
        }

		private void LoadWirelessModuleSettings( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
		{
			string sql = "SELECT * FROM WIRELESSMODULESETTINGS WHERE REFID = @REFID";

			try
			{
				List<WirelessModule> wirelessModuleList = new List<WirelessModule>(); 

				using ( IDbCommand cmd = GetCommand( sql, trx ) )
				{
					cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );
										
					using ( IDataReader reader = cmd.ExecuteReader() )
					{
						DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
												
						while ( reader.Read() )
						{
							WirelessModule module = new WirelessModule();
							module.TransmissionInterval = SqlSafeGetInt( reader, ordinals["TRANSMISSIONINTERVAL"] );
							module.EncryptionKey = SqlSafeGetString( reader, ordinals["ENCRYPTIONKEY"] );
							module.MessageHops = SqlSafeGetInt( reader, ordinals["MESSAGEHOPS"] );
							module.MaxPeers = SqlSafeGetInt( reader, ordinals["MAXPEERS"] );
							module.PrimaryChannel = SqlSafeGetUShort( reader, ordinals["PRIMARYCHANNEL"] );
							module.SecondaryChannel = SqlSafeGetUShort( reader, ordinals["SECONDARYCHANNEL"] );
							module.ActiveChannelMask = SqlSafeGetString( reader, ordinals["ACTIVECHANNELMASK"] );
                            module.WirelessBindingTimeout = SqlSafeGetInt( reader, ordinals["WIRELESSBINDINGTIMEOUT"] );
                            module.WirelessFeatureBits = SqlSafeGetString( reader, ordinals["WIRELESSFEATUREBITS"] );
                            module.ListeningPostChannelMask = SqlSafeGetString(reader, ordinals["LISTENINGPOSTCHANNELMASK"]);

							wirelessModuleList.Add( module );
						}
					}
				}

				if ( wirelessModuleList.Count > 1 )
				{
					string msg = string.Format( "Multiple wireless module settings groups found for refId={1}\"", instSettingsGroup.RefId );
					Log.Error( msg );
					trx.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
				}

				if ( wirelessModuleList.Count > 0 )
					instSettingsGroup.Instrument.WirelessModule = wirelessModuleList[0];				
			}
			catch ( Exception ex )
			{
				throw new DataAccessException( string.Format( "REFID={0}, {1}", instSettingsGroup.RefId, sql ), ex );
			}
		}

		private void LoadAlarmActionMessages( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
		{
			string sql = "SELECT * FROM ALARMACTIONMESSAGES WHERE REFID = @REFID";

			try
			{
				using ( IDbCommand cmd = GetCommand( sql, trx ) )
				{
					cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );

					using ( IDataReader reader = cmd.ExecuteReader() )
					{
						DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

						while ( reader.Read() )
						{
							string sensorCode = SqlSafeGetString( reader, ordinals["SENSORCODE"] );
							AlarmActionMessages aam = new AlarmActionMessages( sensorCode );

							aam.GasAlertMessage = SqlSafeGetString( reader, ordinals["MESSAGEGASALERT"] );
							aam.LowAlarmMessage = SqlSafeGetString( reader, ordinals["MESSAGELOWALARM"] );
							aam.HighAlarmMessage = SqlSafeGetString( reader, ordinals["MESSAGEHIGHALARM"] );
							aam.StelAlarmMessage = SqlSafeGetString( reader, ordinals["MESSAGESTELALARM"] );
							aam.TwaAlarmMessage = SqlSafeGetString( reader, ordinals["MESSAGETWAALARM"] );

							instSettingsGroup.Instrument.AlarmActionMessages.Add( aam );
						}
					}
				}
			}
			catch ( Exception ex )
			{
				throw new DataAccessException( string.Format( "REFID={0}, {1}", instSettingsGroup.RefId, sql ), ex );
			}
		}

        private void LoadSensorSettings( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            string sql = "SELECT * FROM SENSOR WHERE REFID = @REFID";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );

                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        while ( reader.Read() )
                        {
                            Sensor sensor = new Sensor();

                            sensor.Type.Code = SqlSafeGetString( reader, ordinals["SENSORCODE"] );
							sensor.Alarm.GasAlert = SqlSafeGetDouble( reader, ordinals["GASALERT"] );
                            sensor.Alarm.Low = SqlSafeGetDouble( reader, ordinals["ALARMLOW"] );
                            sensor.Alarm.High = SqlSafeGetDouble( reader, ordinals["ALARMHIGH"] );
                            sensor.Alarm.STEL = SqlSafeGetDouble( reader, ordinals["ALARMSTEL"] );
                            sensor.Alarm.TWA = SqlSafeGetDouble( reader, ordinals["ALARMTWA"] );
                            string calGasCode = SqlSafeGetString( reader, ordinals["CALGASCODE"] );
                            GasType gasType;
                            if ( !GasType.Cache.TryGetValue( calGasCode, out gasType ) )
                            {
                                string msg = string.Format( "InstrumentSettingsGroup, REFID={0}: Invalid CalGasCode \"{1}\"", instSettingsGroup.RefId, calGasCode );
                                trx.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                            }
                            else
                                sensor.CalibrationGas = gasType;
                            sensor.CalibrationGasConcentration = SqlSafeGetDouble( reader, ordinals[ "CALGASCONC" ] );
                            sensor.GasDetected = SqlSafeGetString( reader, ordinals[ "DETECTEDGASCODE" ] );
                            sensor.Enabled = SqlSafeGetShort( reader, ordinals[ "ENABLED" ] ) == 0 ? false : true;

                            instSettingsGroup.Instrument.SensorSettings[ sensor.Type.Code ] = sensor;
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "REFID={0}, {1}", instSettingsGroup.RefId, sql ), ex );
            }
        }

        private void LoadCustomResponseFactors( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            string sql = "SELECT * FROM CUSTOMRESPONSEFACTOR WHERE REFID = @REFID ORDER BY SEQUENCE";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );

                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        while ( reader.Read() )
                        {
                            ResponseFactor rf = new ResponseFactor();

                            rf.Name = SqlSafeGetString( reader, ordinals[ "NAME" ] );
                            rf.GasCode = SqlSafeGetString( reader, ordinals[ "GASCODE" ] );
                            rf.Value = SqlSafeGetDouble( reader, ordinals["VALUE"] );

                            instSettingsGroup.Instrument.CustomPidFactors.Add( rf );
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "INSTRUMENT_ID={0}, ", instSettingsGroup.RefId, sql ), ex );
            }
        }


        private void SaveSensors( InstrumentSettingsGroup instSettingsGroup, DataAccessTransaction trx )
        {
            DeleteFromIdTable( "SENSOR", instSettingsGroup.RefId, trx );

			string sql = "INSERT INTO SENSOR ( REFID, SENSORCODE, GASALERT, ALARMLOW, ALARMHIGH, ALARMSTEL, ALARMTWA, CALGASCODE, CALGASCONC, DETECTEDGASCODE, ENABLED ) VALUES ( @REFID, @SENSORCODE, @GASALERT, @ALARMLOW, @ALARMHIGH, @ALARMSTEL, @ALARMTWA, @CALGASCODE, @CALGASCONC, @DETECTEDGASCODE, @ENABLED )";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    foreach ( Sensor sensor in instSettingsGroup.Instrument.SensorSettings.Values )
                    {
                        cmd.Parameters.Clear();

                        cmd.Parameters.Add( GetDataParameter( "@REFID", instSettingsGroup.RefId ) );
                        cmd.Parameters.Add( GetDataParameter( "@SENSORCODE", sensor.Type.Code ) );
						cmd.Parameters.Add( GetDataParameter( "@GASALERT", sensor.Alarm.GasAlert ) );
                        cmd.Parameters.Add( GetDataParameter( "@ALARMLOW", sensor.Alarm.Low ) );
                        cmd.Parameters.Add( GetDataParameter( "@ALARMHIGH", sensor.Alarm.High ) );
                        cmd.Parameters.Add( GetDataParameter( "@ALARMSTEL", sensor.Alarm.STEL ) );
                        cmd.Parameters.Add( GetDataParameter( "@ALARMTWA", sensor.Alarm.TWA ) );
                        cmd.Parameters.Add( GetDataParameter( "@CALGASCODE", sensor.CalibrationGas.Code ) );
                        cmd.Parameters.Add( GetDataParameter( "@CALGASCONC", sensor.CalibrationGasConcentration ) );
                        cmd.Parameters.Add( GetDataParameter( "@DETECTEDGASCODE", sensor.GasDetected ) );
                        cmd.Parameters.Add( GetDataParameter( "@ENABLED", sensor.Enabled ? (short)1 : (short)0 ) );

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
        }

    }
}
