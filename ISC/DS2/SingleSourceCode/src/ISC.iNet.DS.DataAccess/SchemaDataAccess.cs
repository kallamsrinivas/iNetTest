using System;
using System.Data;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class SchemaDataAccess : DataAccess
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="readOnly">We allow performing the 'find' using a writable transaction.
        /// We do allow this because when we're starting up the device, this is the very first
        /// query on the database.  If there's a sqlite "hot journal" file in existence, when
        /// the first query is issued, then sqlite will want to roll back the unclosed transaction.
        /// But it's unable to do so if the transaction is read-only.</param>
        /// <returns></returns>
        public Schema Find( bool readOnly )
        {
            Schema schema = null;

            using ( DataAccessTransaction trx = new DataAccessTransaction( readOnly ) )
            {
                using ( IDbCommand cmd = GetCommand( "SELECT * FROM " + TableName, trx ) )
                {
                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        // There should never be more than one row.  Just take the first.
                        if ( reader.Read() )
                        {
                            schema = new Schema();

                            schema.Version = SqlSafeGetInt( reader, ordinals[ "VERSION" ] );
                            schema.AccountNum = SqlSafeGetString( reader, ordinals[ "ACCOUNTNUM" ] );
                            schema.Activated = ( SqlSafeGetShort( reader, ordinals[ "ACTIVE" ] ) == 1 ) ? true : false;
                            if (schema.Version >= INET_VERSION_INS3017) // SGF  08-Jun-2012  Changed constant to reflect which version this data was added
                                schema.IsManufacturing = (SqlSafeGetShort(reader, ordinals["ISMANUFACTURING"]) == 1) ? true : false;                            

                            // It's assumed the dates are all stored in UTC.  So pass in DateTimeKind.Utc 
                            // to ensure they're not converted to local time when retrieved
                            schema.CylindersVersion = SqlSafeGetNullableDateTime( reader, ordinals[ "FACTORYCYLINDERSUTCVERSION" ], DateTimeKind.Utc  );
                            schema.SchedulesVersion = SqlSafeGetNullableDateTime( reader, ordinals[ "SCHEDULESUTCVERSION" ], DateTimeKind.Utc );
                            schema.EventJournalsVersion = SqlSafeGetNullableDateTime( reader, ordinals[ "EVENTJOURNALSUTCVERSION" ], DateTimeKind.Utc );
                            schema.SettingsVersion = SqlSafeGetNullableDateTime( reader, ordinals[ "SETTINGSUTCVERSION" ], DateTimeKind.Utc );
                            schema.EquipmentVersion = SqlSafeGetNullableDateTime( reader, ordinals[ "EQUIPMENTUTCVERSION" ], DateTimeKind.Utc );

                            //Suresh 06-FEB-2012 INS-2622
                            //Column 'CRITICALTERRORSUTCVERSION' is newly added to database so it won't be available in older version of database.
                            if (schema.Version >= INET_VERSION_INS2622) // SGF  08-Jun-2012  Changed constant to reflect which version this data was added
                                schema.CriticalErrorsVersion = SqlSafeGetNullableDateTime( reader, ordinals["CRITICALINSTRUMENTERRORSUTCVERSION"], DateTimeKind.Utc );

                            //INS-7715/7282 - To determine the account's Service type, to do checks based on Service accounts
                            if (schema.Version >= INET_VERSION_INS7715) //The version in which SERVICECODE is added
                                schema.ServiceCode = SqlSafeGetString(reader, ordinals["SERVICECODE"]);                           

                            if ( !trx.ReadOnly )
                                trx.Commit();
                        }
                    }
                }
            }
            return schema;
        }


        /// <summary>
        /// Queries the schema table for just the database version.
        /// </summary>
        /// <returns>0 is returned if table is empty (which should probably never happen).</returns>
        public int FindVersion()
        {
            int version = 0;

            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                using ( IDbCommand cmd = GetCommand( "SELECT VERSION FROM " + TableName, trx ) )
                {
                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        // There should never be more than one row.  Just take the first.
                        if ( reader.Read() )
                            version = SqlSafeGetInt( reader, ordinals["VERSION"] );
                    }
                }
            }
            return version;
        }

        internal bool UpdateAccount( string accountNum, bool active, bool isManufacturing, string serviceCode )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                // There should never be more than one record in this database, but if there are, just select the highest.
                using (IDbCommand cmd = GetCommand("UPDATE " + TableName + " SET ACCOUNTNUM = @ACCOUNTNUM, ACTIVE = @ACTIVE, ISMANUFACTURING = @ISMANUFACTURING, SERVICECODE = @SERVICECODE", trx))
                {
                    cmd.Parameters.Add( GetDataParameter( "@ACCOUNTNUM", accountNum == string.Empty ? Convert.DBNull : accountNum ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACTIVE", active ? 1 : 0 ) );
                    cmd.Parameters.Add( GetDataParameter( "@ISMANUFACTURING", isManufacturing ? 1 : 0 ) );
                    cmd.Parameters.Add( GetDataParameter( "@SERVICECODE", serviceCode == string.Empty ? Convert.DBNull : serviceCode ) );

                    int rows = cmd.ExecuteNonQuery();

                    trx.Commit();

                    return rows > 0;
                }
            }
        }

        public bool UpdateIsManufacturing( bool isManufacturing )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                // There should never be more than one record in this database, but if there are, just select the highest.
                using ( IDbCommand cmd = GetCommand( "UPDATE " + TableName + " SET ISMANUFACTURING = @ISMANUFACTURING", trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@ISMANUFACTURING", isManufacturing ? 1 : 0 ) );

                    int rows = cmd.ExecuteNonQuery();

                    trx.Commit();

                    return rows > 0;
                }
            }
        }

        public bool UpdateServiceCode( string serviceCode )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                using ( IDbCommand cmd = GetCommand("UPDATE " + TableName + " SET SERVICECODE = @SERVICECODE", trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SERVICECODE", serviceCode == string.Empty ? Convert.DBNull : serviceCode ) );

                    int rows = cmd.ExecuteNonQuery();

                    trx.Commit();

                    return rows > 0;
                }
            }
        }

        public bool UpdateCylindersVersion( DateTime? date, DataAccessTransaction trx )
        {
            return UpdateDateTime( "FACTORYCYLINDERSUTCVERSION", date, trx );
        }

        public bool UpdateSchedulesVersion( DateTime? date, DataAccessTransaction trx )
        {
            return UpdateDateTime( "SCHEDULESUTCVERSION", date, trx );
        }

        public bool UpdateSettingsVersion( DateTime? date, DataAccessTransaction trx )
        {
            return UpdateDateTime( "SETTINGSUTCVERSION", date, trx );
        }

        public bool UpdateEventJournalsVersion( DateTime? date, DataAccessTransaction trx )
        {
            return UpdateDateTime( "EVENTJOURNALSUTCVERSION", date, trx );
        }

        public bool UpdateEquipmentVersion( DateTime? date, DataAccessTransaction trx )
        {
            return UpdateDateTime( "EQUIPMENTUTCVERSION", date, trx );
        }

        public bool UpdateCriticalErrorsVersion(DateTime? date, DataAccessTransaction trx)
        {
            return UpdateDateTime( "CRITICALINSTRUMENTERRORSUTCVERSION", date, trx );
        }

        private bool UpdateDateTime( string dateColumnName, DateTime? dateTime, DataAccessTransaction trx )
        {
            Log.Trace( string.Format( "SchemaDataAccess.UpdateDateTime: {0}={1}", dateColumnName, Log.DateTimeToString(dateTime) ) );

            using ( IDbCommand cmd = GetCommand( string.Format( "UPDATE SCHEMA SET {0} = @{1}", dateColumnName, dateColumnName ), trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@" + dateColumnName, dateTime == null ? Convert.DBNull : dateTime ) );

                int rows = cmd.ExecuteNonQuery();

                return rows > 0;
            }
        }

    }
}
