using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Text;


namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// Base class for tables that contain just serial numbers and record update times.
    /// e.g. DeniedInstruments and ReplacedEquipment.
    /// </summary>
    public abstract class SerialNumberDataAccess : DataAccess
    {
        /// <summary>
        /// Return all the serial numbers in the table.
        /// </summary>
        /// <param name="trx"></param>
        /// <returns>
        /// The order of the returned strings in undefined (they may be in any order).
        /// </returns>
        public List<string> FindAll( DataAccessTransaction trx )
        {
            List<string> snList = new List<string>();

            using ( IDbCommand cmd = GetCommand( string.Format( "SELECT * FROM {0}", TableName ), trx ) )
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {

                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    while ( reader.Read() )
                        snList.Add( SqlSafeGetString( reader, ordinals[ "SN" ] ) );
                }
            }
            return snList;
        }

        /// <summary>
        /// Replaces entire contents of the table with the passed-in serial numbers.
        /// </summary>
        /// <param name="serialNumbers"></param>
        /// <param name="trx"></param>
        /// <returns>Number of serial numbers saved.</returns>
        public int SaveAll( IEnumerable<string> serialNumbers, DataAccessTransaction trx )
        {
            // First, we always delete the current contents of the table.
            DeleteAll( trx );

            int insertedCount = 0;

			using ( IDbCommand cmd = GetCommand( string.Format( "INSERT INTO {0} ( SN, RECUPDATETIMEUTC ) VALUES ( @SN, @RECUPDATETIMEUTC )", TableName ), trx ) )
            {
                foreach ( string sn in serialNumbers )
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.Add( GetDataParameter( "@SN", sn ) );
					cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );

                    try
                    {
                        insertedCount += cmd.ExecuteNonQuery();
                    }
                    catch ( Exception ex )
                    {
                        throw new DataAccessException( string.Format( "Failure saving \"{0}\" to {1}", sn, TableName ), ex );
                    }
                }
                
            }
            return insertedCount;
        }

        /// <summary>
        /// Deletes the entire contents of the table
        /// </summary>
        /// <param name="trx"></param>
        /// <returns>Number of records deleted</returns>
        public int DeleteAll( DataAccessTransaction trx )
        {
            int deletedCount = 0;
            using ( IDbCommand cmd = GetCommand( string.Format( "DELETE FROM {0}", TableName ), trx ) )
            {
                try
                {
                    deletedCount = cmd.ExecuteNonQuery();
                }
                catch ( Exception ex )
                {
                    throw new DataAccessException( string.Format( "Failure deleting all contents of {1}", TableName ), ex );
                }
            }
            return deletedCount;
        }
    }
}
