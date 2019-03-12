using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.DataAccess
{
    public class QueueDataAccess : DataAccess
    {
        private string _tableName;

        public QueueDataAccess( DataSource dataSourceId )
        {
            this._dataSourceId = dataSourceId;

            if ( this._dataSourceId == DataSource.iNetQueue )
                _tableName = "QUEUE";
            else
                throw new ArgumentException( "Unknown DataSourceId: " + dataSourceId.ToString() );
        }

        public QueueDataAccess() { }

        public override string TableName { get { return _tableName; } }

        /// <summary>
        /// Returns the oldest item on the queue.
        /// </summary>
        /// <returns>null is returned if the queue is empty.</returns>
        public PersistedQueueData FindOldest()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId, true ) )
            {
                return FindOldest( trx );
            }
        }

        /// <summary>
        /// Returns the oldest item on the queue
        /// </summary>
        /// <param name="trx"></param>
        /// <returns>null is returned if the queue is empty.</returns>
        public PersistedQueueData FindOldest( DataAccessTransaction trx )
        {
            using ( IDbCommand cmd = GetCommand( string.Format( "SELECT * FROM {0} WHERE ID = ( SELECT MIN(ID) FROM {1} )", TableName, TableName ), trx ) )
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    if ( !reader.Read() )
                        return null;

                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    long id = SqlSafeGetLong( reader, ordinals["ID"] );
                    string accountNum = SqlSafeGetString( reader, ordinals["ACCOUNTNUM"] );
                    DateTime timeStamp = SqlSafeGetDateTime( reader, ordinals["TIMESTAMPUTC"], DateTimeKind.Utc );
                    string label = SqlSafeGetString( reader, ordinals["LABEL"] );
                    string type = SqlSafeGetString( reader, ordinals["TYPE"] );
                    string body = SqlSafeGetString( reader, ordinals["BODY"] );

                    PersistedQueueData persistedQueueData = new PersistedQueueData( accountNum, label, type, body );
                    persistedQueueData.Id = id;
                    persistedQueueData.Timestamp = timeStamp;

                    FindProperties( persistedQueueData, trx );

                    return persistedQueueData;
                }
            }
        }

        /// <summary>
        /// Returns the ID of the oldest item on the queue
        /// </summary>
        /// <returns>DomainModelConstant.NullId is returned if the queue is empty.</returns>
        public long FindOldestId()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId, true ) )
            {
                return FindOldestId( trx );
            }
        }

        /// <summary>
        /// Returns the ID of the oldest item on the queue
        /// </summary>
        /// <returns>DomainModelConstant.NullId is returned if the queue is empty.</returns>
        public long FindOldestId( DataAccessTransaction trx )
        {
            using ( IDbCommand cmd = GetCommand( string.Format( "SELECT MIN(ID) FROM {1}", TableName, TableName ), trx ) )
            {
                object o = cmd.ExecuteScalar();

                if ( o == Convert.DBNull || o == null )
                    return DomainModelConstant.NullId;

                return (long)o;
            }
        }

        /// <summary>
        /// Returns all data on the queue (except for the Body fields).
        /// </summary>
        /// <param name="count">Ignored if zero or less.</param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public List<PersistedQueueData> FindAll( int count )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId, true ) )
            {
                List<PersistedQueueData> list = new List<PersistedQueueData>();

                string sql = string.Format( "SELECT ID, ACCOUNTNUM, TIMESTAMPUTC, LABEL, TYPE FROM {0} ORDER BY ID DESC", TableName );
                if ( count > 0 )
                    sql += string.Format( " LIMIT {0}", count );

                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        while ( reader.Read() )
                        {
                            long id = SqlSafeGetLong( reader, ordinals["ID"] );
                            string accountNum = SqlSafeGetString( reader, ordinals["ACCOUNTNUM"] );
                            DateTime timeStamp = SqlSafeGetDateTime( reader, ordinals["TIMESTAMPUTC"], DateTimeKind.Utc );
                            string label = SqlSafeGetString( reader, ordinals["LABEL"] );
                            string type = SqlSafeGetString( reader, ordinals["TYPE"] );

                            PersistedQueueData persistedQueueData = new PersistedQueueData( accountNum, label, type, string.Empty );
                            persistedQueueData.Id = id;
                            persistedQueueData.Timestamp = timeStamp;

                            FindProperties( persistedQueueData, trx );

                            list.Add( persistedQueueData );
                        }
                    }
                }

                return list;
            }
        }

        private void FindProperties( PersistedQueueData persistedQueueData, DataAccessTransaction trx )
        {
            persistedQueueData.Properties.Clear();

            string sql = string.Format( "SELECT ATTRIBUTE, VALUE FROM {0} WHERE QUEUE_ID = {1}", TableName + "PROPERTY", persistedQueueData.Id );

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    while ( reader.Read() )
                    {
                        string attribute = SqlSafeGetString( reader, ordinals["ATTRIBUTE"] );
                        string value = SqlSafeGetString( reader, ordinals["VALUE"] );

                        persistedQueueData.Properties[attribute] = value;
                    }
                }
            }
        }

        public bool Delete( PersistedQueueData persistedQueueData )
        {
            bool success = false;
            using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId ) )
            {
                success = Delete( persistedQueueData, trx );
                trx.Commit();
            }
            return success;
        }

        public bool Delete( PersistedQueueData persistedQueueData, DataAccessTransaction trx )
        {
            return Delete( persistedQueueData.Id, trx );
        }

        public bool Delete( long id )
        {
            if ( id == DataAccess.NULL_ID_FLAG )
                return false;

            using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId ) )
            {
                bool deleted = Delete( id, trx );

                trx.Commit();

                return deleted;
            }
        }

        public bool Delete( long id, DataAccessTransaction trx )
        {
            if ( id == DataAccess.NULL_ID_FLAG )
                return false;

            using ( IDbCommand cmd = GetCommand( string.Format( "DELETE FROM {0} WHERE ID = @ID", TableName ), trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@ID", id ) );

                bool result = cmd.ExecuteNonQuery() > 0;

                return result;
            }
        }

        public bool Save( PersistedQueueData persistedQueueData )
        {
            if ( persistedQueueData == null )
                throw new DataAccessException( "queueData is null" );

            // If this is a new item to be queued, it shouldn't already have an ID.
            if ( persistedQueueData.Id != DataAccess.NULL_ID_FLAG )
                throw new DataAccessException( "queueData.Id = " + persistedQueueData.Id );

            string sql = string.Format( "INSERT INTO {0} ( TIMESTAMPUTC, LABEL, ACCOUNTNUM, TYPE, BODY ) VALUES ( @TIMESTAMPUTC, @LABEL, @ACCOUNTNUM, @TYPE, @BODY ); SELECT last_insert_rowid() AS ID", TableName );

            IDbCommand cmd = null;

            using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId ) )
            {
                try
                {
                    cmd = GetCommand( sql, trx );

                    cmd.Parameters.Add( GetDataParameter( "@TIMESTAMPUTC", DateTime.UtcNow ) );
                    cmd.Parameters.Add( GetDataParameter( "@LABEL", persistedQueueData.Label ) );
                    cmd.Parameters.Add( GetDataParameter( "@TYPE", persistedQueueData.Type ) );
                    cmd.Parameters.Add( GetDataParameter( "@ACCOUNTNUM", persistedQueueData.InetAccountNum ) );
                    cmd.Parameters.Add( GetDataParameter( "@BODY", persistedQueueData.SerializedWebServiceParameter ) );

                    persistedQueueData.Id = (long)cmd.ExecuteScalar(); // "ID" is treated as an autoincrement field.

                    SaveProperties( persistedQueueData, trx );

                    trx.Commit();

                    return true;
                }
                catch ( Exception ex )
                {
                    if ( ( ex is SQLiteException ) && ( ( (SQLiteException)ex ).ErrorCode == SQLiteErrorCode.Constraint ) )
                        return false;  // assume we have a 'duplicate' error.

                    throw new DataAccessException( string.Format( "Queue Data: \"{0}\", SQL: {1}", persistedQueueData, sql ), ex );
                }
            }
        }

        private void SaveProperties( PersistedQueueData persistedQueueData, DataAccessTransaction trx )
        {
            if ( persistedQueueData.Properties.Count == 0 )
                return;

            string sql = string.Format( "INSERT INTO {0} ( QUEUE_ID, ATTRIBUTE, VALUE ) VALUES ( @QUEUE_ID, @ATTRIBUTE, @VALUE )", TableName + "PROPERTY" );

            try
            {
                IDbCommand cmd = GetCommand( sql, trx );

                foreach ( string attribute in persistedQueueData.Properties.Keys )
                {
                    string value = persistedQueueData.Properties[attribute];

                    cmd.Parameters.Clear();

                    cmd.Parameters.Add( GetDataParameter( "@QUEUE_ID", persistedQueueData.Id ) );
                    cmd.Parameters.Add( GetDataParameter( "@ATTRIBUTE", attribute ) );
                    cmd.Parameters.Add( GetDataParameter( "@VALUE", value ) );

                    int inserted = cmd.ExecuteNonQuery();
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( "SQL: " + sql, ex );
            }
        }

        /// <summary>
        /// Deletes the entire contents of the queue
        /// </summary>
        public void DeleteQueue()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId ) )
            {
                using ( IDbCommand cmd = GetCommand( "DELETE FROM " + TableName, trx ) )
                {
                    cmd.ExecuteNonQuery();
                    trx.Commit();
                }
            }
        }

        /// <summary>
        /// Returns the number of items currently on the queue.
        /// </summary>
        /// <returns></returns>
        public virtual long GetCount()
        {
            try
            {
                using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId, true ) )
                {
                    long count = (long)ExecuteScalar( "SELECT COUNT(ID) FROM " + TableName, trx );
                    return count;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( string.Format( "Could not get count from {0}. See inner exception for details.", TableName ), ex );
            }
        }

        /// <summary>
        /// Returns the 'version' of this database's schema.
        /// </summary>
        /// <remarks>
        /// During startup, the application needs to obtain this version and compare it
        /// to what version it thinks it's compatible with.
        /// </remarks>
        /// <returns></returns>
        public int GetSchemaVersion()
        {
            try
            {
                // THIS TRANSACTION MUST REMAIN WRITABLE. SINCE IT IS CALLED FIRST IT WILL ENSURE ALL 
                // PENDING DATABASE TRANSACTIONS HAVE BEEN ROLLED BACK. 
                using ( DataAccessTransaction trx = new DataAccessTransaction( this._dataSourceId, false ) )
                {
                    using ( IDbCommand cmd = GetCommand( "SELECT VERSION FROM SCHEMA", trx ) )
                    {
                        using ( IDataReader reader = cmd.ExecuteReader() )
                        {
                            DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                            // There should never be more than one row.  Just take the first.
                            if ( reader.Read() )
                            {
                                int result = SqlSafeGetInt( reader, ordinals["VERSION"] );
                                trx.Commit();
                                return result;
                            }
                            trx.Commit();
                            return 0; // return a default value of 0 for version
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( "Could not get Version from Queue Schema. See inner exception for details.", ex );
            }
        }
    }
}
