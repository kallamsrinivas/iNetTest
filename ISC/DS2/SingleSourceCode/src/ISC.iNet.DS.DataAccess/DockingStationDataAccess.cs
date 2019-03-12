using System;
using System.Data;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE;
using ISC.WinCE.Logger;
using TimeZoneInfo = ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.DataAccess
{
    public class DockingStationDataAccess : DataAccess
    {
        static private string[] _fields = new string[]
        {
            "REFID",
            "RECUPDATETIMEUTC",
            "LANGUAGE",
            "MENULOCKED",
            "SPEAKER",
            "LOGLEVEL",
            "LOGCAPACITY",
            "INETURL",
            "INETPINGINTERVAL",
            "INETTIMEOUTLOW",
            "INETTIMEOUTMEDIUM",
            "INETTIMEOUTHIGH",
            "INETUSERNAME",
            "INETPASSWORD",
            "PRINTPERFORMEDBY",
            "PRINTRECEIVEDBY",
            "PORT1RESTRICTION",
            "PURGEAFTERBUMP",
            "TZBIAS",
            "TZSTANDARDNAME",
            "TZSTANDARDDATEMONTH",
            "TZSTANDARDDATEDAYOFWEEK",
            "TZSTANDARDDATEDAY",
            "TZSTANDARDDATEHOUR",
            "TZSTANDARDBIAS",
            "TZDAYLIGHTNAME",
            "TZDAYLIGHTDATEMONTH",
            "TZDAYLIGHTDATEDAYOFWEEK",
            "TZDAYLIGHTDATEDAY",
            "TZDAYLIGHTDATEHOUR",
            "TZDAYLIGHTBIAS",
            "CLEARPEAKSUPONDOCKING",
            "SINGLESENSORMODE",
            "USEEXPIREDCYLINDERS",
            "COMBUSTIBLEBUMPTESTGAS",
            "SPANRESERVETHRESHOLD",
            "STOPONFAILEDBUMPTEST",
            "UPGRADEONERRORFAIL"
        };

        private static readonly string _updateSql;
        private static readonly string _insertSql;

        static DockingStationDataAccess()
        {
            StringBuilder sqlBuilder = new StringBuilder( "UPDATE DOCKINGSTATION SET " );
			// REFID should get updated also if docking station settings change
			for ( int i = 0; i < _fields.Length; i++ )
            {
                if ( i > 0 ) sqlBuilder.Append( ", " );
                sqlBuilder.Append( _fields[ i ] );
                sqlBuilder.Append( " = @" );
                sqlBuilder.Append( _fields[ i ] );
            }
            _updateSql = sqlBuilder.ToString();

            _insertSql = string.Format( "INSERT INTO DOCKINGSTATION ( {0} ) VALUES ( {1} )", CreateFieldList( string.Empty ), CreateFieldList( "@" ) );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns>
        /// "FIELD1, "FIELD2", FIELD3", etc.
        /// </returns>
        private static string CreateFieldList( string prefix )
        {
            StringBuilder sb = new StringBuilder();

            for ( int i = 0; i < _fields.Length; i++ )
            {
                if ( sb.Length > 0 ) sb.Append( ", " );

                if ( prefix != string.Empty ) sb.Append( prefix );

                sb.Append( _fields[ i ] );
            }

            return sb.ToString();
        }

        private void AddSaveParameters( IDbCommand cmd, DockingStation ds, DataAccessTransaction trx )
        {
            cmd.Parameters.Add( GetDataParameter( "@REFID", ds.RefId ) );
            cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
            cmd.Parameters.Add( GetDataParameter( "@LANGUAGE", ds.Language.Code ) );
            cmd.Parameters.Add( GetDataParameter( "@MENULOCKED", ds.MenuLocked ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@SPEAKER", ds.UseAudibleAlarm ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@LOGLEVEL", ds.LogLevel.ToString() ) );
            cmd.Parameters.Add( GetDataParameter( "@LOGCAPACITY", ds.LogCapacity ) );
            cmd.Parameters.Add( GetDataParameter( "@INETURL", ds.InetUrl ) );
            cmd.Parameters.Add( GetDataParameter( "@INETPINGINTERVAL", ds.InetPingInterval ) );
            cmd.Parameters.Add( GetDataParameter( "@INETTIMEOUTLOW", ds.InetTimeoutLow ) );
            cmd.Parameters.Add( GetDataParameter( "@INETTIMEOUTMEDIUM", ds.InetTimeoutMedium ) );
            cmd.Parameters.Add( GetDataParameter( "@INETTIMEOUTHIGH", ds.InetTimeoutHigh ) );
            cmd.Parameters.Add( GetDataParameter( "@INETUSERNAME", ds.InetUserName ) );
            cmd.Parameters.Add( GetDataParameter( "@INETPASSWORD", ds.InetPassword ) );
            cmd.Parameters.Add( GetDataParameter( "@PRINTPERFORMEDBY", ds.PrintPerformedBy ? 1 : 0 ) );            
            cmd.Parameters.Add( GetDataParameter( "@PRINTRECEIVEDBY", ds.PrintReceivedBy ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@PORT1RESTRICTION", (int)ds.Port1Restrictions ) );
            cmd.Parameters.Add( GetDataParameter( "@PURGEAFTERBUMP", ds.PurgeAfterBump ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@TZBIAS", ds.TimeZoneInfo.Bias ) );
            cmd.Parameters.Add( GetDataParameter( "@TZSTANDARDNAME", ds.TimeZoneInfo.StandardName ) );
            cmd.Parameters.Add( GetDataParameter( "@TZSTANDARDDATEMONTH", ds.TimeZoneInfo.StandardDate.Month ) );
            cmd.Parameters.Add( GetDataParameter( "@TZSTANDARDDATEDAYOFWEEK", ds.TimeZoneInfo.StandardDate.DayOfWeek ) );
            cmd.Parameters.Add( GetDataParameter( "@TZSTANDARDDATEDAY", ds.TimeZoneInfo.StandardDate.Day ) );
            cmd.Parameters.Add( GetDataParameter( "@TZSTANDARDDATEHOUR", ds.TimeZoneInfo.StandardDate.Hour ) );
            cmd.Parameters.Add( GetDataParameter( "@TZSTANDARDBIAS", ds.TimeZoneInfo.StandardBias ) );
            cmd.Parameters.Add( GetDataParameter( "@TZDAYLIGHTNAME", ds.TimeZoneInfo.DaylightName ) );
            cmd.Parameters.Add( GetDataParameter( "@TZDAYLIGHTDATEMONTH", ds.TimeZoneInfo.DaylightDate.Month ) );
            cmd.Parameters.Add( GetDataParameter( "@TZDAYLIGHTDATEDAYOFWEEK", ds.TimeZoneInfo.DaylightDate.DayOfWeek ) );
            cmd.Parameters.Add( GetDataParameter( "@TZDAYLIGHTDATEDAY", ds.TimeZoneInfo.DaylightDate.Day ) );
            cmd.Parameters.Add( GetDataParameter( "@TZDAYLIGHTDATEHOUR", ds.TimeZoneInfo.DaylightDate.Hour ) );
            cmd.Parameters.Add( GetDataParameter( "@TZDAYLIGHTBIAS", ds.TimeZoneInfo.DaylightBias ) );
            cmd.Parameters.Add( GetDataParameter( "@CLEARPEAKSUPONDOCKING", ds.ClearPeaksUponDocking ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@SINGLESENSORMODE", ds.SingleSensorMode ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@USEEXPIREDCYLINDERS", ds.UseExpiredCylinders ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@COMBUSTIBLEBUMPTESTGAS", ds.CombustibleBumpTestGas ) );
            cmd.Parameters.Add( GetDataParameter( "@SPANRESERVETHRESHOLD", ds.SpanReserveThreshold ) );
            cmd.Parameters.Add( GetDataParameter( "@STOPONFAILEDBUMPTEST", ds.StopOnFailedBumpTest ? 1 : 0 ) );
            cmd.Parameters.Add( GetDataParameter( "@UPGRADEONERRORFAIL", ds.UpgradeOnErrorFail ? 1 : 0 ) );

            Log.Assert( cmd.Parameters.Count == _fields.Length, "Number of SQL parameters does not equal number of strings in \"_fields\"" );
        }

        public DockingStation Find()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return Find( trx );
            }
        }

        public DockingStation Find( DataAccessTransaction trx )
        {
            string sql = "SELECT * FROM DOCKINGSTATION";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        if ( !reader.Read() )
                        {
                            Log.Debug( "No DOCKINGSTATION record found" );
                            return null;
                        }

                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        DockingStation ds = new DockingStation();

                        ds.RefId = SqlSafeGetLong( reader, ordinals["REFID"] );
                        ds.Language.Code = SqlSafeGetString( reader, ordinals[ "LANGUAGE" ] );
                        ds.MenuLocked = SqlSafeGetShort( reader, ordinals[ "MENULOCKED" ] ) == 1 ? true : false;
                        ds.UseAudibleAlarm = SqlSafeGetShort( reader, ordinals[ "SPEAKER" ] ) == 1 ? true : false;
                        ds.LogLevel = (LogLevel)Enum.Parse( typeof( LogLevel ), SqlSafeGetString( reader, ordinals[ "LOGLEVEL" ] ), true );
                        ds.LogCapacity = SqlSafeGetInt( reader, ordinals[ "LOGCAPACITY" ] );
                        ds.InetUrl = SqlSafeGetString( reader, ordinals[ "INETURL" ] );
                        ds.InetPingInterval = SqlSafeGetShort( reader, ordinals[ "INETPINGINTERVAL" ] );
                        ds.InetTimeoutLow = SqlSafeGetInt( reader, ordinals["INETTIMEOUTLOW"] );
                        ds.InetTimeoutMedium = SqlSafeGetInt( reader, ordinals["INETTIMEOUTMEDIUM"] );
                        ds.InetTimeoutHigh = SqlSafeGetInt( reader, ordinals["INETTIMEOUTHIGH"] );
                        ds.InetUserName = SqlSafeGetString( reader, ordinals[ "INETUSERNAME" ] );
                        ds.InetPassword = SqlSafeGetString( reader, ordinals[ "INETPASSWORD" ] );
                        ds.PrintPerformedBy = SqlSafeGetShort( reader, ordinals[ "PRINTPERFORMEDBY" ] ) == 1 ? true : false;
                        ds.PrintReceivedBy = SqlSafeGetShort( reader, ordinals[ "PRINTRECEIVEDBY" ] ) == 1 ? true : false;
                        ds.Port1Restrictions = (PortRestrictions)SqlSafeGetInt( reader, ordinals[ "PORT1RESTRICTION" ] );
                        ds.PurgeAfterBump = SqlSafeGetShort( reader, ordinals["PURGEAFTERBUMP"] ) == 1 ? true : false;
                        ds.ClearPeaksUponDocking = SqlSafeGetShort( reader, ordinals["CLEARPEAKSUPONDOCKING"] ) == 1 ? true : false;
                        ds.SingleSensorMode = SqlSafeGetShort(reader, ordinals["SINGLESENSORMODE"]) == 1 ? true : false;
                        ds.UseExpiredCylinders = SqlSafeGetShort( reader, ordinals["USEEXPIREDCYLINDERS"] ) == 1 ? true : false;
                        ds.CombustibleBumpTestGas = SqlSafeGetString( reader, ordinals["COMBUSTIBLEBUMPTESTGAS"] );
                        ds.SpanReserveThreshold = SqlSafeGetDouble( reader, ordinals["SPANRESERVETHRESHOLD"] );
                        ds.StopOnFailedBumpTest = SqlSafeGetShort( reader, ordinals["STOPONFAILEDBUMPTEST"] ) == 1 ? true : false;
                        ds.UpgradeOnErrorFail = SqlSafeGetShort( reader, ordinals["UPGRADEONERRORFAIL"] ) == 1 ? true : false;

                        int bias = SqlSafeGetInt( reader, ordinals[ "TZBIAS" ] );
                        string stdName = SqlSafeGetString( reader, ordinals[ "TZSTANDARDNAME" ] );
                        SystemTime stdDate = new SystemTime();
                        stdDate.DayOfWeek = SqlSafeGetShort( reader, ordinals["TZSTANDARDDATEDAYOFWEEK"] );
                        stdDate.Day = SqlSafeGetShort( reader, ordinals["TZSTANDARDDATEDAY"] );
                        stdDate.Month = SqlSafeGetShort( reader, ordinals["TZSTANDARDDATEMONTH"] );
                        stdDate.Hour = SqlSafeGetShort( reader, ordinals["TZSTANDARDDATEHOUR"] );
                        int stdBias = SqlSafeGetInt( reader, ordinals[ "TZSTANDARDBIAS" ] );
                        string dstName = SqlSafeGetString( reader, ordinals[ "TZDAYLIGHTNAME" ] );
                        SystemTime dstDate = new SystemTime();
                        dstDate.DayOfWeek = SqlSafeGetShort( reader, ordinals[ "TZDAYLIGHTDATEDAYOFWEEK" ] );
                        dstDate.Day = SqlSafeGetShort( reader, ordinals["TZDAYLIGHTDATEDAY"] );
                        dstDate.Month = SqlSafeGetShort( reader, ordinals["TZDAYLIGHTDATEMONTH"] );
                        dstDate.Hour = SqlSafeGetShort( reader, ordinals["TZDAYLIGHTDATEHOUR"] );
                        int dstBias = SqlSafeGetInt( reader, ordinals[ "TZDAYLIGHTBIAS" ] );
                        ds.TimeZoneInfo = new ISC.iNet.DS.DomainModel.TimeZoneInfo( bias, stdName, stdDate, stdBias, dstName, dstDate, dstBias );

                        return ds;
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }

        }  // end-FindBySerialNumber


        public void Save( DockingStation ds )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                Save( ds, trx );
                trx.Commit();
            }
        }


        public bool Save( DockingStation ds, DataAccessTransaction trx )
        {
            // First try and update.  If that fails, then it's likely because
            // the record doesn't already exist.  So then try and insert.
            if ( Update( ds, trx ) )
                return true;

            return Insert( ds, trx );
        }

        private bool Update( DockingStation ds, DataAccessTransaction trx )
        {
            try
            {
                using ( IDbCommand cmd = GetCommand( _updateSql, trx ) )
                {
                    AddSaveParameters( cmd, ds, trx );

                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( _updateSql, ex );
            }

        }

        private bool Insert( DockingStation ds, DataAccessTransaction trx )
        {
            try
            {
                using ( IDbCommand cmd = GetCommand( _insertSql, trx ) )
                {
                    AddSaveParameters( cmd, ds, trx );

                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( _insertSql, ex );
            }
        }
    }
}
