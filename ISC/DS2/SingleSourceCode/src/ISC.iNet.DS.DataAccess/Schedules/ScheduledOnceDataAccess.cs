using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class ScheduledOnceDataAccess : ScheduleDataAccess
    {
        protected internal override Schedule CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return new ScheduledOnce(
                GetId( reader, ordinals ),
                GetRefId( reader, ordinals ),
                GetName( reader, ordinals ),
                GetEventCode( reader, ordinals ),
				GetEquipmentCode( reader, ordinals ),
                GetEquipmentSubTypeCode( reader, ordinals ),
                GetEnabled( reader, ordinals ),
                GetStartDate( reader, ordinals ),
                GetRunAtTime( reader, ordinals ) );
        }

        public override bool Insert( Schedule schedule, DataAccessTransaction trx )
        {
            if ( !InsertSchedule( schedule, trx ) )
                return false;

            string sql = "INSERT INTO {0} ( SCHEDULE_ID, STARTDATE, RUNATTIME ) VALUES ( @SCHEDULE_ID, @STARTDATE, @RUNATTIME )";

            using ( IDbCommand cmd = GetCommand( string.Format( sql, TableName ), trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", schedule.Id ) );
                cmd.Parameters.Add( GetDataParameter( "@STARTDATE", schedule.StartDate ) );
                cmd.Parameters.Add( GetDataParameter( "@RUNATTIME", schedule.RunAtTimeToString() ) );

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch ( SQLiteException e )
                {
                    Log.Debug( string.Format( "Insert {0}, ID={1} - {2}", TableName, schedule.Id, e ) );

                    if ( e.ErrorCode == SQLiteErrorCode.Constraint )
                        return false;  // assume we have a 'duplicate' error.

                    throw new DataAccessException( string.Format( "ID:{0}, SQL:{1}", schedule.Id, sql ), e );
                }
            }

            return true;
        }
    }
}
