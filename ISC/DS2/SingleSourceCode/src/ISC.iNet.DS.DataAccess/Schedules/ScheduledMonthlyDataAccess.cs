﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class ScheduledMonthlyDataAccess : ScheduleDataAccess
    {
        protected internal override Schedule CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            short dayOfMonth = SqlSafeGetShort( reader, ordinals[ "DAYOFMONTH" ] );

            short week = SqlSafeGetShort( reader, ordinals[ "WEEK" ] );

            DayOfWeek? dayOfWeek = null;
            short rawDayOfWeek = SqlSafeGetShort( reader, ordinals[ "DAYOFWEEK" ] );
            if ( rawDayOfWeek != DomainModelConstant.NullShort )
                dayOfWeek = (DayOfWeek)rawDayOfWeek;

            return new ScheduledMonthly( 
                GetId( reader, ordinals ),
                GetRefId( reader, ordinals ),
                GetName( reader, ordinals ),
                GetEventCode( reader, ordinals ),
				GetEquipmentCode( reader, ordinals ),
                GetEquipmentSubTypeCode( reader, ordinals ),
                GetEnabled( reader, ordinals ),
                GetOnDocked( reader, ordinals ),
                GetInterval( reader, ordinals ),
                GetStartDate( reader, ordinals ),
                GetRunAtTime( reader, ordinals ),
                week, dayOfWeek, dayOfMonth );
        }

        public override bool Insert( Schedule schedule, DataAccessTransaction trx )
        {
            if ( !InsertSchedule( schedule, trx ) )
                return false;

            ScheduledMonthly monthly = (ScheduledMonthly)schedule;

            string sql = "INSERT INTO SCHEDULEDMONTHLY ( SCHEDULE_ID, INTERVAL, STARTDATE, RUNATTIME, DAYOFMONTH, WEEK, DAYOFWEEK ) VALUES ( @SCHEDULE_ID, @INTERVAL, @STARTDATE, @RUNATTIME, @DAYOFMONTH, @WEEK, @DAYOFWEEK )";
            bool inserted = false;
            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", monthly.Id ) );
                cmd.Parameters.Add( GetDataParameter( "@INTERVAL", monthly.Interval ) );
                cmd.Parameters.Add( GetDataParameter( "@STARTDATE", monthly.StartDate.Date ) );
                cmd.Parameters.Add( GetDataParameter( "@RUNATTIME", monthly.RunAtTimeToString() ) );

                cmd.Parameters.Add( GetDataParameter( "@DAYOFMONTH", monthly.DayOfMonth ) );
                cmd.Parameters.Add( GetDataParameter( "@WEEK", monthly.Week ) );
                cmd.Parameters.Add( GetDataParameter( "@DAYOFWEEK", monthly.DayOfWeek == null ? DomainModelConstant.NullShort : (short)monthly.DayOfWeek ) );

                try
                {
                    inserted = cmd.ExecuteNonQuery() > 0;
                }
                catch ( SQLiteException e )
                {
                    Log.Debug( string.Format( "Insert {0}, ID={1} - {2}", TableName, schedule.Id, e ) );

                    if ( e.ErrorCode == SQLiteErrorCode.Constraint )
                        return false;  // assume we have a 'duplicate' error.

                    throw new DataAccessException( string.Format( "ID:{0}, SQL:{1}", schedule.Id, sql ), e );
                }
            }
            return inserted;
        }
    }
}
