using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class EventJournalDataAccess : DataAccess
    {
        /// <summary>
        /// Finds all EventJournals for the specified equipment/component serial number.
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="trx"></param>
        /// <returns>An empty array is returned if no match is found.</returns>
        public List<EventJournal> FindBySerialNumber( string serialNumber, DataAccessTransaction trx )
        {
            return FindBySerialNumbers( new string[] { serialNumber }, trx );
        }

                /// <summary>
        /// Finds all EventJournals for the specified equipment/component serial numbers.
        /// </summary>
        /// <param name="serialNumbers"></param>
        /// <returns>An empty array is returned if no match is found.</returns>
        public List<EventJournal> FindBySerialNumbers( string[] serialNumbers )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return FindBySerialNumbers( serialNumbers, trx );
            }
        }

        /// <summary>
        /// Finds all EventJournals for the specified equipment/component serial numbers.
        /// </summary>
        /// <param name="serialNumbers"></param>
        /// <param name="trx"></param>
        /// <returns>An empty array is returned if no match is found.</returns>
        public virtual List<EventJournal>  FindBySerialNumbers( string[] serialNumbers, IDataAccessTransaction trx )
        {
            List<EventJournal> journalsList = new List<EventJournal>();

            string sql = string.Format( "SELECT * FROM EVENTJOURNAL WHERE SN IN ( {0} )", MakeCommaDelimitedParamNames( serialNumbers.Length, "@SN" ) );

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                for ( int i = 0; i < serialNumbers.Length; i++ )
                    cmd.Parameters.Add( GetDataParameter( "@SN" + ( i + 1 ).ToString(), serialNumbers[ i ] ) );

                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    while ( reader.Read() )
                    {
                        journalsList.Add( CreateFromReader( reader, ordinals ) );
                    }
                }
            }
            return journalsList;
        }

        /// <summary>
        /// Finds all EventJournals for the specified instrument serial number and its components.
        /// </summary>
        /// <param name="instrumentSerialNumber"></param>
        /// <param name="eventCode"></param>
        /// <param name="trx"></param>
        /// <returns>An empty array is returned if no match is found.</returns>
        public List<EventJournal> FindByInstrumentSerialNumber(string instrumentSerialNumber, string eventCode, DataAccessTransaction trx)
        {
            List<EventJournal> journalsList = new List<EventJournal>();

            string sql = "SELECT * FROM EVENTJOURNAL WHERE (SN = @SN OR INSTRUMENTSN = @SN) AND EVENTCODE = @EVENTCODE";

            using (IDbCommand cmd = GetCommand(sql, trx))
            {
                cmd.Parameters.Add(GetDataParameter("@SN", instrumentSerialNumber));
                cmd.Parameters.Add(GetDataParameter("@EVENTCODE", eventCode));

                using (IDataReader reader = cmd.ExecuteReader())
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals(reader);
                    while (reader.Read())
                    {
                        journalsList.Add(CreateFromReader(reader, ordinals));
                    }
                }
            }
            return journalsList;
        }

        /// <summary>
        /// Finds all EventJournals for the last event of the specified type for the specified instrument serial number and its components.
        /// </summary>
        /// <param name="instrumentSerialNumber"></param>
        /// <param name="eventCode"></param>
        /// <param name="trx"></param>
        /// <returns>An empty array is returned if no match is found.</returns>
        public virtual List<EventJournal> FindLastEventByInstrumentSerialNumber(string instrumentSerialNumber, string eventCode, IDataAccessTransaction trx)
        {
            List<EventJournal> journalsList = new List<EventJournal>();

            //string sql = "SELECT * FROM EVENTJOURNAL WHERE (SN = @SN OR INSTRUMENTSN = @SN) AND EVENTCODE = @EVENTCODE";
            string sql = "SELECT * FROM EVENTJOURNAL WHERE (SN = @SN OR INSTRUMENTSN = @SN) AND EVENTCODE = @EVENTCODE AND EVENTTIMEUTC = (SELECT MAX(EVENTTIMEUTC) FROM EVENTJOURNAL WHERE (SN = @SN OR INSTRUMENTSN = @SN) AND EVENTCODE = @EVENTCODE)";

            using (IDbCommand cmd = GetCommand(sql, trx))
            {
                cmd.Parameters.Add(GetDataParameter("@SN", instrumentSerialNumber));
                cmd.Parameters.Add(GetDataParameter("@EVENTCODE", eventCode));

                using (IDataReader reader = cmd.ExecuteReader())
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals(reader);
                    while (reader.Read())
                    {
                        journalsList.Add(CreateFromReader(reader, ordinals));
                    }
                }
            }
            return journalsList;
        }

        private EventJournal CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            string eventCode = SqlSafeGetString( reader, ordinals[ "EVENTCODE" ] );
            string sn = SqlSafeGetString( reader, ordinals[ "SN" ] );
            string instSn = SqlSafeGetString( reader, ordinals[ "INSTRUMENTSN" ] );
            DateTime runTime = SqlSafeGetDateTime( reader, ordinals[ "RUNTIMEUTC" ], DateTimeKind.Utc );
            DateTime eventTime = SqlSafeGetDateTime( reader, ordinals[ "EVENTTIMEUTC" ], DateTimeKind.Utc );
            bool passed = SqlSafeGetShort( reader, ordinals[ "PASSED" ] ) == 0 ? false : true;
            int position = SqlSafeGetInt(reader, ordinals["POSITION"]);
            string softwareVersion = SqlSafeGetString(reader, ordinals["SOFTWAREVERSION"]);

            return new EventJournal( eventCode, sn, instSn, runTime, eventTime, passed, position, softwareVersion );
        }

        public bool Save( EventJournal journal, DataAccessTransaction trx )
        {
#if DEBUG
            if ( journal.EventCode.Code == EventCode.Calibration || journal.EventCode.Code == EventCode.BumpTest )
                Log.Assert( journal.InstrumentSerialNumber != string.Empty, string.Format( "Found unexpected empty InstrumentSerialNumber for \"{0}\"", journal.EventCode ) );
            else
                Log.Assert( journal.InstrumentSerialNumber == string.Empty, string.Format( "Found unexpected non-empty InstrumentSerialNumber (\"{0}\") for \"{1}\"", journal.InstrumentSerialNumber, journal.EventCode ) );
            Log.Assert( journal.InstrumentSerialNumber != journal.SerialNumber, string.Format( "SerialNumber and InstrumentSerialNumber are equal (\"{0}\") for \"{1}\"", journal.SerialNumber, journal.EventCode ) );
#endif
            // If an Insert hint is passed in, they we first try and insert
            // and then do an update if the insert fails.  It's intended that
            // the caller would pass in an Insert hint when it thinks the data
            // being inserted is new (such as the very first time a docking station
            // is saving its event journals).
            if ( trx.Hint == DataAccessHint.Insert )
            {
                // If event already exists, then update it, otherwise insert as new.
                if ( Insert( journal, trx ) )
                    return true;  // must have already existed.  No need to attempt an insert.

                return Update( journal, trx );
            }

            // Default behavior: If event already exists, then update it, otherwise insert as new.
            if ( Update( journal, trx ) )
                return true;  // must have already existed.  No need to attempt an insert.

            return Insert( journal, trx );
        }

        private bool Insert( EventJournal journal )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                bool success = Insert( journal, trx );
                trx.Commit();
                return success;
            }
        }

        private bool Insert( EventJournal journal, DataAccessTransaction trx )
        {
            string sql = "INSERT INTO EVENTJOURNAL ( EVENTCODE, SN, INSTRUMENTSN, RECUPDATETIMEUTC, RUNTIMEUTC, EVENTTIMEUTC, PASSED, POSITION, SOFTWAREVERSION ) VALUES ( @EVENTCODE, @SN, @INSTRUMENTSN, @RECUPDATETIMEUTC, @RUNTIMEUTC, @EVENTTIMEUTC, @PASSED, @POSITION, @SOFTWAREVERSION )";
            IDbCommand cmd = null;

            try
            {
                cmd = GetCommand( sql, trx );

                cmd.Parameters.Add( GetDataParameter( "@EVENTCODE", journal.EventCode.Code ) );
                cmd.Parameters.Add( GetDataParameter( "@SN", journal.SerialNumber ) );
                cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
                cmd.Parameters.Add( GetDataParameter( "@INSTRUMENTSN", journal.InstrumentSerialNumber ) );
                cmd.Parameters.Add( GetDataParameter( "@EVENTTIMEUTC", journal.EventTime ) );
                cmd.Parameters.Add( GetDataParameter( "@RUNTIMEUTC", journal.RunTime ) );
                cmd.Parameters.Add( GetDataParameter( "@PASSED", ( journal.Passed == true ) ? (short)1 : (short)0 ) );
                cmd.Parameters.Add( GetDataParameter( "@POSITION", journal.Position ) );
                cmd.Parameters.Add( GetDataParameter( "@SOFTWAREVERSION", journal.SoftwareVersion ) );

                int inserted = cmd.ExecuteNonQuery();

                return inserted > 0;
            }
            catch ( Exception ex )
            {
                if ( ( ex is SQLiteException ) && ( ((SQLiteException)ex).ErrorCode == SQLiteErrorCode.Constraint ) )
                        return false;  // assume we have a 'duplicate' error.

                throw new DataAccessException( string.Format( "Journal: \"{0}\", SQL: {1}", journal, sql ), ex );
            }
        }

        private bool Update( EventJournal journal )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                bool success = Update( journal, trx );
                trx.Commit();
                return success;
            }
        }

        private bool Update( EventJournal journal, DataAccessTransaction trx )
        {
            // If the journal's LastDockedTime is set to a valid date, then update it.
            // If set to 'null', then leave whatever is stored in the database alone.
            // Note also that we only do the update if the runTime is newer than the currently stored runTime.
            string sql = "UPDATE EVENTJOURNAL SET INSTRUMENTSN = @INSTRUMENTSN, RECUPDATETIMEUTC = @RECUPDATETIMEUTC, EVENTTIMEUTC = @EVENTTIMEUTC, RUNTIMEUTC = @RUNTIMEUTC, PASSED = @PASSED, POSITION = @POSITION, SOFTWAREVERSION = @SOFTWAREVERSION WHERE SN = @SN AND EVENTCODE = @EVENTCODE AND @RUNTIMEUTC >= RUNTIMEUTC";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@SN", journal.SerialNumber ) );
                    cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
                    cmd.Parameters.Add( GetDataParameter( "@INSTRUMENTSN", journal.InstrumentSerialNumber ) );
                    cmd.Parameters.Add( GetDataParameter( "@RUNTIMEUTC", journal.RunTime ) );
                    cmd.Parameters.Add( GetDataParameter( "@EVENTTIMEUTC", journal.EventTime ) );
                    cmd.Parameters.Add( GetDataParameter( "@EVENTCODE", journal.EventCode.Code ) );
                    cmd.Parameters.Add( GetDataParameter( "@PASSED", ( journal.Passed == true ) ? (short)1 : (short)0 ) );
                    cmd.Parameters.Add( GetDataParameter( "@POSITION", journal.Position ) );
                    cmd.Parameters.Add( GetDataParameter( "@SOFTWAREVERSION", journal.SoftwareVersion ) );

                    int numRows = cmd.ExecuteNonQuery();

                    return numRows > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "Journal: \"{0}\", SQL: {1}", journal, sql ), ex );
            }
        }

        /// <summary>
        /// Deletes all EventJournal records for the specified serial numbers.
        /// </summary>
        /// <param name="serialNumbers"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public int DeleteBySerialNumbers( ICollection<string> serialNumbers, DataAccessTransaction trx )
        {
            string sql = string.Format( "DELETE FROM EVENTJOURNAL WHERE SN IN ( {0} )", MakeCommaDelimitedParamNames( serialNumbers.Count, "@SN" ) );

            try
            {
                IDbCommand cmd = GetCommand( sql, trx );

                int i = 0;
                foreach ( string sn in serialNumbers )
                    cmd.Parameters.Add( GetDataParameter( "@SN" + (++i).ToString(), sn ) );

                int delCount = cmd.ExecuteNonQuery();

                Log.Debug( string.Format( "Deleted {0} EVENTJOURNALs", delCount ) );

                return delCount;
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "SQL:{0}", sql ), ex );
            }
        }

        /// <summary>
        /// Deletes all EventJournal records for the specified serial numbers and passed-in
        /// event code.
        /// </summary>
        /// <param name="serialNumbers"></param>
        /// <param name="eventCode"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public int DeleteBySerialNumbers( ICollection<string> serialNumbers, EventCode eventCode, DataAccessTransaction trx )
        {
            return DeleteBySerialNumbers( serialNumbers, new EventCode[] { eventCode }, trx );
        }

        public int DeleteBySerialNumbers( ICollection<string> serialNumbers, ICollection<EventCode> eventCodes, DataAccessTransaction trx )
        {
            string sql = string.Format( "DELETE FROM EVENTJOURNAL WHERE SN IN ( {0} ) AND EVENTCODE IN ( {1} )",
                MakeCommaDelimitedParamNames( serialNumbers.Count, "@SN" ),
                MakeCommaDelimitedParamNames( eventCodes.Count, "@EVENTCODE" ) );

            try
            {
                IDbCommand cmd = GetCommand( sql, trx );

                int i = 0;
                foreach ( string sn in serialNumbers )
                    cmd.Parameters.Add( GetDataParameter( "@SN" + ( ++i ).ToString(), sn ) );

                i = 0;
                foreach ( EventCode eventCode in eventCodes )
                    cmd.Parameters.Add( GetDataParameter( "@EVENTCODE" + ( ++i ).ToString(), eventCode.Code ) );

                int delCount = cmd.ExecuteNonQuery();

                if ( Log.Level >= LogLevel.Debug )
                {
                    string eventCodesMsg = string.Empty;
                    foreach ( EventCode eventCode in eventCodes )
                    {
                        if ( eventCodesMsg.Length > 0 ) eventCodesMsg += ",";
                        eventCodesMsg += eventCode.Code;
                    }
                    Log.Debug( string.Format( "Deleted {0} EVENTJOURNALs of type {1}", delCount, eventCodesMsg ) );
                }
                return delCount;
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "SQL:{0}", sql ), ex );
            }
        }


        /// <summary>
        /// Deletes all EventJournal records for the specified instrument and its components.
        /// </summary>
        /// <param name="instrumentSerialNumber"></param>
        /// <param name="eventCode"></param>
        /// <returns>An integer indicating how many journal entries were deleted</returns>
        public int DeleteByInstrumentSerialNumber( string instrumentSerialNumber, string eventCode )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                int delCount = DeleteByInstrumentSerialNumber( instrumentSerialNumber, eventCode, trx );
                trx.Commit();
                return delCount;
            }

        }

        /// <summary>
        /// Deletes all EventJournal records for the specified instrument and its components.
        /// </summary>
        /// <param name="instrumentSerialNumber"></param>
        /// <param name="eventCode"></param>
        /// <param name="trx"></param>
        /// <returns>An integer indicating how many journal entries were deleted</returns>
        public int DeleteByInstrumentSerialNumber(string instrumentSerialNumber, string eventCode, DataAccessTransaction trx)
        {

            string sql = "DELETE FROM EVENTJOURNAL WHERE (SN = @SN OR INSTRUMENTSN = @SN) AND EVENTCODE = @EVENTCODE";

            try
            {
                using (IDbCommand cmd = GetCommand(sql, trx))
                {
                    cmd.Parameters.Add(GetDataParameter("@SN", instrumentSerialNumber));
                    cmd.Parameters.Add(GetDataParameter("@EVENTCODE", eventCode));

                    int delCount = cmd.ExecuteNonQuery();

                    if (Log.Level >= LogLevel.Debug)
                    {
                        Log.Debug(string.Format("Deleted {0} EVENTJOURNALs of type {1}", delCount, eventCode));
                    }
                    return delCount;
                }
            }
            catch (Exception e)
            {
                throw new DataAccessException(string.Format("SQL:{0}", sql), e);
            }
        }

        /// <summary>
        /// Deletes all EventJournal records for events older than the specified time.
        /// </summary>
        /// <param name="journalAge"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public int DeleteByTime(TimeSpan journalAge, DataAccessTransaction trx)
        {
            if (journalAge < TimeSpan.Zero)
                return -1; // The time span provided is negative; we consider a negative span to be invalid.

            DateTime deleteTimeStamp = DateTime.UtcNow - journalAge;
            string sql = string.Format("DELETE FROM EVENTJOURNAL WHERE EVENTTIMEUTC < @DELETETIMESTAMP");

            try
            {
                IDbCommand cmd = GetCommand(sql, trx);
                cmd.Parameters.Add(GetDataParameter("@DELETETIMESTAMP", deleteTimeStamp));

                int delCount = cmd.ExecuteNonQuery();

                Log.Debug(string.Format("Deleted {0} EVENTJOURNALs older than {1} days, {2} hours, {3} minutes, and {4} seconds",
                                        delCount, journalAge.Days, journalAge.Hours, journalAge.Minutes, journalAge.Seconds));
                return delCount;
            }
            catch (Exception ex)
            {
                throw new DataAccessException(string.Format("SQL:{0}", sql), ex);
            }
        }

    }
}
