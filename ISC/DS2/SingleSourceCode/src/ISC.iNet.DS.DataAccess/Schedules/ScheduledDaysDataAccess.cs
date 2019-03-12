using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public abstract class ScheduledDaysDataAccess : ScheduleDataAccess
    {
        protected bool[] GetDaysFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            bool[] days = new bool[ 7 ];

            days[ (int)DayOfWeek.Sunday ] = ( SqlSafeGetShort( reader, ordinals[ "SUNDAY" ] ) > 0 ) ? true : false;
            days[ (int)DayOfWeek.Monday ] = ( SqlSafeGetShort( reader, ordinals[ "MONDAY" ] ) > 0 ) ? true : false;
            days[ (int)DayOfWeek.Tuesday ] = ( SqlSafeGetShort( reader, ordinals[ "TUESDAY" ] ) > 0 ) ? true : false;
            days[ (int)DayOfWeek.Wednesday ] = ( SqlSafeGetShort( reader, ordinals[ "WEDNESDAY" ] ) > 0 ) ? true : false;
            days[ (int)DayOfWeek.Thursday ] = ( SqlSafeGetShort( reader, ordinals[ "THURSDAY" ] ) > 0 ) ? true : false;
            days[ (int)DayOfWeek.Friday ] = ( SqlSafeGetShort( reader, ordinals[ "FRIDAY" ] ) > 0 ) ? true : false;
            days[ (int)DayOfWeek.Saturday ] = ( SqlSafeGetShort( reader, ordinals[ "SATURDAY" ] ) > 0 ) ? true : false;

            return days;
        }

        public override bool Insert( Schedule schedule, DataAccessTransaction trx )
        {
            if ( !InsertSchedule( schedule, trx ) )
                return false;

            ScheduledDays days = (ScheduledDays)schedule;

            string sql = "INSERT INTO {0} ( SCHEDULE_ID, INTERVAL, STARTDATE, RUNATTIME, SUNDAY, MONDAY, TUESDAY, WEDNESDAY, THURSDAY, FRIDAY, SATURDAY ) VALUES ( @SCHEDULE_ID, @INTERVAL, @STARTDATE, @RUNATTIME, @SUNDAY, @MONDAY, @TUESDAY, @WEDNESDAY, @THURSDAY, @FRIDAY, @SATURDAY )";

            using ( IDbCommand cmd = GetCommand( string.Format( sql, TableName ), trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", days.Id ) );
                cmd.Parameters.Add( GetDataParameter( "@INTERVAL", days.Interval ) );
                cmd.Parameters.Add( GetDataParameter( "@STARTDATE", days.StartDate.Date ) );
                cmd.Parameters.Add( GetDataParameter( "@RUNATTIME", days.RunAtTimeToString() ) );

                cmd.Parameters.Add( GetDataParameter( "@SUNDAY", days.Days[ (int)DayOfWeek.Sunday ] == true ? 1 : 0 ) );
                cmd.Parameters.Add( GetDataParameter( "@MONDAY", days.Days[ (int)DayOfWeek.Monday ] == true ? 1 : 0 ) );
                cmd.Parameters.Add( GetDataParameter( "@TUESDAY", days.Days[ (int)DayOfWeek.Tuesday ] == true ? 1 : 0 ) );
                cmd.Parameters.Add( GetDataParameter( "@WEDNESDAY", days.Days[ (int)DayOfWeek.Wednesday ] == true ? 1 : 0 ) );
                cmd.Parameters.Add( GetDataParameter( "@THURSDAY", days.Days[ (int)DayOfWeek.Thursday ] == true ? 1 : 0 ) );
                cmd.Parameters.Add( GetDataParameter( "@FRIDAY", days.Days[ (int)DayOfWeek.Friday ] == true ? 1 : 0 ) );
                cmd.Parameters.Add( GetDataParameter( "@SATURDAY", days.Days[ (int)DayOfWeek.Saturday ] == true ? 1 : 0 ) );

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
