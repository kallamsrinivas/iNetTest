using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class ScheduledDailyDataAccess : ScheduleDataAccess
    {
        protected internal override Schedule CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return new ScheduledDaily(
                GetId( reader, ordinals ),
                GetRefId( reader, ordinals ),
                GetName( reader, ordinals ),
                GetEventCode( reader, ordinals ),
				GetEquipmentCode( reader, ordinals ),
                GetEquipmentSubTypeCode( reader, ordinals ),
                GetEnabled( reader, ordinals ),
                GetOnDocked( reader, ordinals ),
                GetInterval( reader,ordinals ),
                GetStartDate( reader,ordinals ),
                GetRunAtTime( reader, ordinals ) );
        }

        public override bool Insert( Schedule schedule, DataAccessTransaction trx )
        {
            if ( !InsertSchedule( schedule, trx ) )
                return false;

            ScheduledDaily daily = (ScheduledDaily)schedule;

            string sql = "INSERT INTO SCHEDULEDDAILY ( SCHEDULE_ID, INTERVAL, STARTDATE, RUNATTIME ) VALUES ( @SCHEDULE_ID, @INTERVAL, @STARTDATE, @RUNATTIME )";

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", daily.Id ) );
                cmd.Parameters.Add( GetDataParameter( "@INTERVAL", daily.Interval ) );
                cmd.Parameters.Add( GetDataParameter( "@STARTDATE", daily.StartDate.Date ) );
                cmd.Parameters.Add( GetDataParameter( "@RUNATTIME", daily.RunAtTimeToString() ) );

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
