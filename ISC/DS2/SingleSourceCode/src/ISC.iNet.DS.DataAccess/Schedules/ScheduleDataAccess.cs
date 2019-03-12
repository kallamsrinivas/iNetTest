using System;
using System.Collections.Generic;
using System.Globalization;
using System.Data;
using System.Text;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.DataAccess
{
    public /*abstract*/ class ScheduleDataAccess : DataAccess  // TODO: Make this abstract (but then must make DataAccess class abstract, too.
    {
        /// <summary>
        /// Returns all global schedules (those not associated with either any equipment serial numbers
        /// nor any component codes).  True global schedules also have a null equipment code.
        /// </summary>
        /// <param name="trx"></param>
        /// <returns></returns>
        public virtual IList<Schedule> FindGlobalSchedules( IDataAccessTransaction trx )
        {
            List<Schedule> list = new List<Schedule>();

            StringBuilder sqlBuilder = new StringBuilder( string.Format( "SELECT SCHEDULE.*, {0}.* FROM SCHEDULE, {1}", TableName, TableName ), 500 );
			sqlBuilder.Append( " WHERE SCHEDULE.EQUIPMENTCODE IS NULL" );
			sqlBuilder.Append( " AND NOT EXISTS ( SELECT SCHEDULE_ID FROM SCHEDULEDEQUIPMENT WHERE SCHEDULE_ID = SCHEDULE.ID )" );
            sqlBuilder.Append( " AND NOT EXISTS ( SELECT SCHEDULE_ID FROM SCHEDULEDCOMPONENTCODE WHERE SCHEDULE_ID = SCHEDULE.ID )" );
            sqlBuilder.AppendFormat( " AND {0}.SCHEDULE_ID = SCHEDULE.ID", TableName );
            string sql = sqlBuilder.ToString();

            try
            {
                using ( IDbCommand cmd = GetCommand( string.Format( sql, TableName, TableName ), trx ) )
                {
                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
                        while ( reader.Read() )
                        {
                            Schedule sched = CreateFromReader( reader, ordinals );
                            list.Add( sched );

                            LoadScheduleProperties(sched, trx);
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
            return list;
        }

		/// <summary>
		/// Returns all global type-specific schedules (those not associated with either any equipment serial numbers
		/// nor any component codes).  Global type-specific schedules do not have a null equipment code and can be assumed
		/// to only be for instrument events.
		/// </summary>
        /// <param name="equipmentCode">The device types</param>
		/// <param name="trx"></param>
		/// <returns>A list of global type-specific schedules for the equipment</returns>
		public virtual IList<Schedule> FindGlobalTypeSpecificSchedules( string[] equipmentCodes, IDataAccessTransaction trx )
		{
			List<Schedule> list = new List<Schedule>();

			StringBuilder sqlBuilder = new StringBuilder( string.Format( "SELECT SCHEDULE.*, {0}.* FROM SCHEDULE, {1}", TableName, TableName ), 500 );
			sqlBuilder.Append( string.Format (" WHERE SCHEDULE.EQUIPMENTCODE IN ( {0} ) ", MakeCommaDelimitedParamNames( equipmentCodes.Length, "@EQUIPMENTCODE" ) ) );
			sqlBuilder.Append( " AND NOT EXISTS ( SELECT SCHEDULE_ID FROM SCHEDULEDEQUIPMENT WHERE SCHEDULE_ID = SCHEDULE.ID )" );
			sqlBuilder.Append( " AND NOT EXISTS ( SELECT SCHEDULE_ID FROM SCHEDULEDCOMPONENTCODE WHERE SCHEDULE_ID = SCHEDULE.ID )" );
            sqlBuilder.AppendFormat(" AND {0}.SCHEDULE_ID = SCHEDULE.ID", TableName );
			string sql = sqlBuilder.ToString();

			try
			{
				using ( IDbCommand cmd = GetCommand( sql, trx ) )
				{
                    for (int i = 0; i < equipmentCodes.Length; i++)
                        cmd.Parameters.Add(GetDataParameter("@EQUIPMENTCODE" + (i + 1).ToString(), equipmentCodes[i]));

					using ( IDataReader reader = cmd.ExecuteReader() )
					{
						DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
						while ( reader.Read() )
						{
							Schedule sched = CreateFromReader( reader, ordinals );
							
							list.Add( sched );
							LoadScheduleProperties( sched, trx );							
						}
					}
				}
			}
			catch ( Exception ex )
			{
				throw new DataAccessException( sql, ex );
			}

			return list;
		}

        public IList<Schedule> FindBySerialNumber( string serialNumber, DataAccessTransaction trx )
        {
            return FindBySerialNumbers( new string[] { serialNumber }, trx );
        }

        public virtual IList<Schedule> FindBySerialNumbers( string[] serialNumbers, IDataAccessTransaction trx )
        {
            return FindByScheduledItems( serialNumbers, "SCHEDULEDEQUIPMENT", "SN", trx );
        }

        public IList<Schedule> FindByComponentCode( string componentCode, DataAccessTransaction trx )
        {
            return FindByComponentCodes( new string[] { componentCode }, trx );
        }

        public virtual IList<Schedule> FindByComponentCodes( string[] componentCodes, IDataAccessTransaction trx )
        {
            return FindByScheduledItems( componentCodes, "SCHEDULEDCOMPONENTCODE", "COMPONENTCODE", trx );
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// "Items" is either one or more pieces of equipment (specified by serial number)
        /// or one or more component (sensor) types.
        /// </remarks>
        /// <param name="items">serial numbers or component type codes</param>
        /// <param name="itemTableName">SCHEDULEDCOMPONENTCODE or SCHEDULEDEQUIPMENT</param>
        /// <param name="stringFieldName">COMPONENTCODE or SN</param>
        /// <param name="trx"></param>
        /// <returns></returns>
        private IList<Schedule> FindByScheduledItems( string[] items, string itemTableName, string itemFieldName, IDataAccessTransaction trx )
        {
            List<Schedule> list = new List<Schedule>();

            StringBuilder sqlBuilder = new StringBuilder( string.Format( "SELECT SCHEDULE.*, {0}.* FROM SCHEDULE, {1}, {2}", TableName, TableName, itemTableName ), 500 );
            sqlBuilder.AppendFormat( " WHERE {0}.{1} IN ( {2} )", itemTableName, itemFieldName, MakeCommaDelimitedParamNames(items.Length, "@PARAM")); // SGF  05-Nov-2010  Single Sensor Cal and Bump -- corrected bug in statement
            sqlBuilder.AppendFormat( " AND {0}.SCHEDULE_ID = SCHEDULE.ID", itemTableName);
            sqlBuilder.AppendFormat( " AND {0}.SCHEDULE_ID = SCHEDULE.ID", TableName );
            string sql = sqlBuilder.ToString();
            try
            {
                using ( IDbCommand cmd = GetCommand( string.Format( sql, TableName, TableName ), trx ) )
                {
                    for ( int i = 0; i < items.Length; i++ )
                        cmd.Parameters.Add( GetDataParameter( "@PARAM" + ( i + 1 ).ToString(), items[ i ] ) );

                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        while ( reader.Read() )
                        {
                            Schedule sched = CreateFromReader( reader, ordinals );

                            sched.ComponentCodes = new List<string>( FindScheduledForItems( sched.Id, "SCHEDULEDCOMPONENTCODE", "COMPONENTCODE", trx ) );
                            sched.SerialNumbers = new List<string>( FindScheduledForItems( sched.Id, "SCHEDULEDEQUIPMENT", "SN", trx ) );

                            LoadScheduleProperties(sched, trx);

                            list.Add( sched );
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
            return list;
        }

        /// <summary>
        /// </summary>
        /// <param name="id"></param>
        /// <param name="tableName">Must be either SCHEDULEDCOMPONENTCODE or SCHEDULEDEQUIPMENT </param>
        /// <param name="stringFieldName">COMPONENTCODE or SN</param>
        /// <param name="trx"></param>
        /// <returns></returns>
        private string[] FindScheduledForItems( long id, string tableName, string stringFieldName, IDataAccessTransaction trx )
        {
            List<string> strList = new List<string>();

            string sql = string.Format( string.Format( "SELECT {0} FROM {1} WHERE SCHEDULE_ID = @SCHEDULE_ID", stringFieldName, tableName ) );

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", id ) );

                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        while ( reader.Read() )
                        {
                            string str = SqlSafeGetString( reader, ordinals[ stringFieldName ] );
                            if ( str != null && str.Length > 0 )
                                strList.Add( str );
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
            return strList.ToArray(); ;
        }

        /// <summary>
        /// </summary>
        /// <param name="schedule"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        private void LoadScheduleProperties(Schedule schedule, IDataAccessTransaction trx)
        {
            string sql = "SELECT * FROM SCHEDULEPROPERTY WHERE SCHEDULE_ID = @SCHEDULE_ID ORDER BY ATTRIBUTE, SEQUENCE";

            try
            {               
                List<ScheduleProperty> list = new List<ScheduleProperty>();
                using (IDbCommand cmd = GetCommand(sql, trx))
                {
                    cmd.Parameters.Add(GetDataParameter("@SCHEDULE_ID", schedule.Id));

                    using (IDataReader reader = cmd.ExecuteReader())
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals(reader);

                        while (reader.Read())
                            list.Add(CreateSchedulePropertiesFromReader(schedule, reader, ordinals));                       
                    }
                }                
                schedule.ScheduleProperties = list;
            }
            catch (Exception ex)
            {
                throw new DataAccessException(sql, ex);
            }
        }

        // TODO: make this abstract if we change class to be abstract.
        protected internal virtual /*abstract*/ Schedule CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            throw new NotSupportedException( "Only supported in derived classes" );
        }

        protected long GetId(IDataReader reader, DataAccessOrdinals ordinals)
        {
            return SqlSafeGetLong( reader, ordinals["ID"]);
        }

        protected long GetRefId( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return SqlSafeGetLong( reader, ordinals["REFID"] );
        }

        protected string GetName( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return SqlSafeGetString(reader, ordinals["NAME"]);
        }

        protected EventCode GetEventCode( IDataReader reader, DataAccessOrdinals ordinals )
        {
            string eventCode = SqlSafeGetString( reader, ordinals[ "EVENTCODE" ] );

            return EventCode.GetCachedCode( eventCode );
        }

		protected string GetEquipmentCode( IDataReader reader, DataAccessOrdinals ordinals )
		{
			return SqlSafeGetString( reader, ordinals[ "EQUIPMENTCODE" ] );
		}

        protected string GetEquipmentSubTypeCode( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return SqlSafeGetString( reader, ordinals["EQUIPMENTSUBTYPECODE"] );
        }

        protected bool GetEnabled( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return SqlSafeGetShort( reader, ordinals["ENABLED"] ) != 0;
        }

        protected bool GetOnDocked(IDataReader reader, DataAccessOrdinals ordinals) 
        {
            return SqlSafeGetShort( reader, ordinals[ "UPONDOCKING" ] ) != 0;
        }

        protected short GetInterval(IDataReader reader, DataAccessOrdinals ordinals)
        {
            return SqlSafeGetShort( reader, ordinals[ "INTERVAL" ] );
        }

        protected DateTime GetStartDate( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return SqlSafeGetDate( reader, ordinals[ "STARTDATE" ] );
        }

        protected TimeSpan GetRunAtTime( IDataReader reader, DataAccessOrdinals ordinals )
        {
            string runAtTime = SqlSafeGetString( reader, ordinals[ "RUNATTIME" ] );
            TimeSpan runAtTimeSpan = ( runAtTime == null ) ? TimeSpan.Zero : TimeSpan.Parse( runAtTime );
            return runAtTimeSpan;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refId"></param>
        /// <param name="trx"></param>
        /// <returns>True if there was a SCHEDULE to delete; else false.</returns>
        public bool DeleteByRefId( long refId, DataAccessTransaction trx )
        {
            if ( refId == DomainModelConstant.NullId )
                return false;

            string sql = string.Empty;

            bool deleted = false;

            try
            {
                sql = "DELETE FROM SCHEDULE WHERE REFID = @REFID";
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );
                    deleted = cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "REFID:{0}, SQL:{1}", refId, sql ), ex );
            }
            return deleted;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="trx"></param>
        /// <returns>True if there was a SCHEDULE to delete; else false.</returns>
        public bool DeleteById( long id, DataAccessTransaction trx )
        {
            if ( id == DomainModelConstant.NullId )
                return false;

            string sql = string.Empty;

            bool deleted = false;

            try
            {
                sql = "DELETE FROM SCHEDULE WHERE ID = @ID";
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@ID", id ) );
                    deleted = cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "ID:{0}, SQL:{1}", id, sql ), ex );
            }
            return deleted;
        }

        private long FindIdByRefId( long refId, DataAccessTransaction trx )
        {
            using ( IDbCommand cmd = GetCommand( "SELECT ID FROM SCHEDULE WHERE REFID = @REFID", trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@REFID", refId ) );

                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    // There should never be more than one row.  Just take the first.
                    if ( reader.Read() )
                        return SqlSafeGetLong( reader, ordinals["ID"] );
                }
            }
            return DomainModelConstant.NullId;
        }


        /// <summary>
        /// Deletes all scheduling data related to the specified serial numbers.
        /// </summary>
        /// <param name="snList"></param>
        /// <param name="trx">True if there was a SCHEDULE to delete; else false.</param>
        public bool DeleteBySerialNumbers( IList<string> snList, DataAccessTransaction trx )
        {
            const string paramName = "@SN";

            string paramsString = MakeCommaDelimitedParamNames( snList.Count, paramName );

            string sql = string.Empty;

            try
            {
                sql = "DELETE FROM SCHEDULE WHERE ID IN ( SELECT DISTINCT SCHEDULE_ID FROM SCHEDULEDEQUIPMENT WHERE SN IN ( {0} ) )";
                using ( IDbCommand cmd = GetCommand( string.Format( sql, paramsString ), trx ) )
                {
                    for ( int i = 0; i < snList.Count; i++ )
                        cmd.Parameters.Add( GetDataParameter( paramName + (i + 1).ToString(), snList[ i ] ) );
                    int delCount = cmd.ExecuteNonQuery();
                    Log.Debug( string.Format( "Deleted {0} SCHEDULEs", delCount ) );
                    return delCount > 0;
                }
            }
            catch ( Exception ex )
            {
                Log.Debug( ex.ToString() );
                throw new DataAccessException( string.Format( "SQL:{0}", sql ), ex );
            }
        }


        private bool DeleteFromScheduleIdTable(string tableName, long scheduleId, DataAccessTransaction trx)
        {
            string sql = string.Format("DELETE FROM {0} WHERE SCHEDULE_ID = @SCHEDULE_ID", tableName);

            try
            {
                using (IDbCommand cmd = GetCommand(sql, trx))
                {
                    cmd.Parameters.Add(GetDataParameter("@SCHEDULE_ID", scheduleId));
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                throw new DataAccessException(string.Format("table={0}, scheduleId={1}, {2}", tableName, scheduleId, sql), ex);
            }
        }


        public virtual bool Insert( Schedule schedule, DataAccessTransaction trx )
        {
            throw new NotSupportedException( "Only supported in derived classes" );
        }


        /// <summary>
        /// Insert's the passed-in Schedule into the database.
        /// </summary>
        /// <remarks>
        /// The insert assigns a unique database ID the inserted record.
        /// That passed-in schedule's "Id" property is set to that assigned ID upon successful insertion.
        /// </remarks>
        /// <param name="schedule"></param>
        /// <param name="trx"></param>
        /// <returns>false if failure do to constraint (duplicate).  else true</returns>
        protected /*abstract*/ bool InsertSchedule( Schedule schedule, DataAccessTransaction trx )  // TODO: make this abstract if we change class to be abstract.
        {
            // "ID" is treated as an autoincrement field.  "last_insert_rowid" function will return us
            // the ID assigned during the insert.  Note that to run the function, we call executescalar
            // instead of executenonquery.
            string sql = "INSERT INTO SCHEDULE ( RECUPDATETIMEUTC, REFID, EVENTCODE, EQUIPMENTCODE, EQUIPMENTSUBTYPECODE, NAME, ENABLED, UPONDOCKING ) VALUES ( @RECUPDATETIMEUTC, @REFID, @EVENTCODE, @EQUIPMENTCODE, @EQUIPMENTSUBTYPECODE, @NAME, @ENABLED, @UPONDOCKING ); SELECT last_insert_rowid() AS ID";
            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {

                cmd.Parameters.Add( GetDataParameter( "@REFID", schedule.RefId ) );
                cmd.Parameters.Add( GetDataParameter( "@EVENTCODE", schedule.EventCode ) );
				cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTCODE", schedule.EquipmentCode ) );
                cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTSUBTYPECODE", schedule.EquipmentSubTypeCode ) );
                cmd.Parameters.Add( GetDataParameter( "@NAME", schedule.Name ) );
                cmd.Parameters.Add( GetDataParameter( "@ENABLED", schedule.Enabled ? 1 : 0 ) );
                cmd.Parameters.Add( GetDataParameter( "@UPONDOCKING", schedule.UponDocking ? 1 : 0 ) );
                // We stamp each schedule with the time it was saved, for debugging/diagnostic purposes.
                cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
                try
                {
                    schedule.Id = (long)cmd.ExecuteScalar(); // "ID" is treated as an autoincrement field.
                }
                catch ( SQLiteException e )
                {
                    Log.Debug( string.Format( "Insert Schedule, RefId={0} - {1}", schedule.RefId, e ) );

                    if ( e.ErrorCode == SQLiteErrorCode.Constraint )
                        return false;  // assume we have a 'duplicate' error.

                    string errMsg = string.Format( "REFID:{0}, SQL:{1}\r\n{2}", schedule.RefId, sql, e.ToString() );
                    trx.Errors.Add( new DockingStationError( errMsg, DockingStationErrorLevel.Warning ) );
                    throw new DataAccessException( errMsg ); // any other error is unexpected, so just rethrow it
                }

                InsertScheduledComponentCodes( schedule, trx );
                InsertScheduledEquipment( schedule, trx );

                InsertScheduleProperties(schedule, trx);
            }
            return true;
        }


        private void InsertScheduledComponentCodes( Schedule schedule, DataAccessTransaction trx )
        {
            if ( schedule.ComponentCodes.Count == 0 || ( schedule.ComponentCodes.Count == 1 && schedule.ComponentCodes[ 0 ].Length == 0 ) )
                return;

            string sql = "INSERT INTO SCHEDULEDCOMPONENTCODE ( COMPONENTCODE, SCHEDULE_ID ) VALUES ( @COMPONENTCODE, @SCHEDULE_ID )";
            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    foreach ( string code in schedule.ComponentCodes )
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add( GetDataParameter( "@COMPONENTCODE", code ) );
                        cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", schedule.Id ) );
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch ( Exception e )
            {
                Log.Debug( e.ToString() );
                throw new DataAccessException( string.Format( "ID:{0}, SQL:{1}", schedule.Id, sql ), e );
            }
        }

        private void InsertScheduledEquipment( Schedule schedule, DataAccessTransaction trx )
        {
            if ( schedule.SerialNumbers.Count == 0 )
                return;

            string sql = "INSERT INTO SCHEDULEDEQUIPMENT ( SN, SCHEDULE_ID ) VALUES ( @SN, @SCHEDULE_ID )";
            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    foreach ( string sn in schedule.SerialNumbers )
                    {
                        cmd.Parameters.Clear();
                        cmd.Parameters.Add( GetDataParameter( "@SN", sn ) );
                        cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", schedule.Id ) );
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch ( Exception e )
            {
                Log.Debug( e.ToString() );
                throw new DataAccessException( string.Format( "ID:{0}, SQL:{1}", schedule.Id, sql ), e );
            }
        }

        private void InsertScheduleProperties(Schedule schedule, DataAccessTransaction trx)
        {
            // delete any old properties for the SCHEDULE_ID (we don't need to care if we're inserting
            // new data versus updating old data; we just try and always delete the old data)
            DeleteFromScheduleIdTable("SCHEDULEPROPERTY", schedule.Id, trx);           
            foreach (ScheduleProperty scheduleProperty in schedule.ScheduleProperties)
            {
                InsertScheduleProperty( schedule.Id, scheduleProperty.Attribute, scheduleProperty.Sequence, scheduleProperty.Value, trx );                                           
            }
        }

        private bool InsertScheduleProperty(long scheduleId, string attribute, int sequence, string value, DataAccessTransaction trx)
        {
            string sql = "INSERT INTO SCHEDULEPROPERTY ( SCHEDULE_ID, ATTRIBUTE, SEQUENCE, VALUE ) VALUES ( @SCHEDULE_ID, @ATTRIBUTE, @SEQUENCE, @VALUE )";
            try
            {
                using (IDbCommand cmd = GetCommand(sql, trx))
                {
                    cmd.Parameters.Add(GetDataParameter("@SCHEDULE_ID", scheduleId));
                    cmd.Parameters.Add(GetDataParameter("@ATTRIBUTE", attribute));
                    cmd.Parameters.Add(GetDataParameter("@SEQUENCE", (short)sequence));
                    cmd.Parameters.Add(GetDataParameter("@VALUE", value));

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                throw new DataAccessException(string.Format("scheduleId={0}, attribute={1}, sequence={2}, value={3}, {4}", scheduleId, attribute, sequence, value, sql), ex);
            }
        }

        private ScheduleProperty CreateSchedulePropertiesFromReader(Schedule schedule, IDataReader reader, DataAccessOrdinals ordinals)
        {
            long scheduleId = SqlSafeGetLong(reader, ordinals["SCHEDULE_ID"]);
            string attribute = SqlSafeGetString(reader, ordinals["ATTRIBUTE"]);
            short sequence = SqlSafeGetShort(reader, ordinals["SEQUENCE"]);
            string value = SqlSafeGetString(reader, ordinals["VALUE"]);
           
            return new ScheduleProperty(scheduleId, attribute, sequence, value);
        }
    }
}
