using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class ScheduledUponDockingDataAccess : ScheduleDataAccess
    {
        protected internal override Schedule CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            return new ScheduledUponDocking(
                GetId( reader, ordinals ),
                GetRefId( reader, ordinals ),
                GetName( reader, ordinals ),
                GetEventCode( reader, ordinals ),
				GetEquipmentCode( reader, ordinals ),
                GetEquipmentSubTypeCode( reader, ordinals ),
                GetEnabled( reader, ordinals ) );
        }

        public override bool Insert( Schedule schedule, DataAccessTransaction trx )
        {
            if ( !InsertSchedule( schedule, trx ) )
                return false;

            string sql = "INSERT INTO {0} ( SCHEDULE_ID ) VALUES ( @SCHEDULE_ID )";

            using ( IDbCommand cmd = GetCommand( string.Format( sql, TableName ), trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@SCHEDULE_ID", schedule.Id ) );

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
