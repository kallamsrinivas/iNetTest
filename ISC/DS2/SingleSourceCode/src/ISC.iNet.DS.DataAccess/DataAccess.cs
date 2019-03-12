using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public abstract class DataAccess
    {
        public enum DataSource
        {
            iNetData = 1,
            iNetQueue = 2
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////

        // Version Trace
        internal const int INET_VERSION = 44;
        internal const int INET_VERSION_INS3017 = 19; // 07-Jun-2012  INS-3017
        internal const int INET_VERSION_INS2622 = 17; // 06-Feb-2012  INS-2622
        internal const int INET_VERSION_INS7715 = 40; // Added ServiceCode

        internal static readonly string INET_CONNECTION_STRING = "Data Source=\"" + Controller.FLASHCARD_PATH + Controller.INET_DB_NAME + "\";Pooling=False;Max Pool Size=5;FailIfMissing=True;";
        internal static readonly string INET_CONNECTION_STRING_READONLY = INET_CONNECTION_STRING + "Read Only=True;";
        
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        private const int INET_QUEUE_VERSION = 6;

        internal static readonly string INET_QUEUE_CONNECTION_STRING = "Data Source=\"" + Controller.FLASHCARD_PATH + Controller.INETQ_DB_NAME + "\";Pooling=False;Max Pool Size=5;FailIfMissing=True;";
        internal static readonly string INET_QUEUE_CONNECTION_STRING_READONLY = INET_QUEUE_CONNECTION_STRING + "Read Only=True;";

        /////////////////////////////////////////////////////////////////////////////////////////////////////

        // transaction journal file name is the name of the database plus this suffix. e.g., if database is
        // named is "MyDatabase.db3", then the journal file would be named "MyDatabase.db3-journal".
        private const string JOURNAL_EXTENSION_SUFFIX = "-journal";  

        /////////////////////////////////////////////////////////////////////////////////////////////////////

        protected const long NULL_ID_FLAG = long.MinValue;
        protected const int NULL_INT_FLAG = int.MinValue;
        protected const short NULL_SHORT_FLAG = short.MinValue;
        protected const float NULL_LONG_FLAG = long.MinValue;
        protected const float NULL_FLOAT_FLAG = float.MinValue;
        protected const double NULL_DOUBLE_FLAG = double.MinValue;
        protected const decimal NULL_DECIMAL_FLAG = decimal.MinValue;
        protected static readonly DateTime NULL_DATETIME_FLAG = DateTime.MinValue;

        private static Dictionary<Type, string> _tableNames = new Dictionary<Type, string>();
		private string _tableName;

        private static Mutex _mutex = new Mutex();

        private static string _startedError = null;

        private const string _name = "DataAccess";
        private const string firmwareFolderPath = Controller.FLASHCARD_PATH + "\\" + "Firmware";

        // We default to iNetData. For any subclass using a different datasource, the subclass
        // should override the _dataSourceId's setting in its constructor.
        protected internal DataSource _dataSourceId;

        static internal Mutex Mutex
        {
            get
            {
                return _mutex;
            }
        }

        public DataAccess()
        {
            // We default to iNetData. For any subclass using a different datasource, the subclass
            // should override the _dataSourceId's setting in its constructor.
            _dataSourceId = DataSource.iNetData;
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// Makes sure the database exists on the flash card.
        /// 
        /// ...If not found on flash card, then backup copy of empty
        /// database provided in firmware is copied to flash card.
        /// 
        /// ...If found on flash card, then the version of the database
        /// is checked. This is because the firmware version may be incompatible
        /// with the database found on flashcard which will happen when
        /// the firmware is upgraded (or downgraded) to a different version.
        /// If database version is considered incompatible, then the database
        /// is deleted from the flash card and the backup copy of empty
        /// database provided in the firmware is copied to flash card.
        /// </remarks>
        static public void StartInet()
        {
            Log.Info( string.Format( "{0}: Initializing...", _name ) );

            //lock ( FlashCard.Lock )
            {
                Controller.RunState &= ~Controller.State.InetDbError; // Clear existing DataBaseError bit.
                _startedError = null; // clear any existing error

                Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.WaitOne" );
                Mutex.WaitOne();

                string dbName = Controller.INET_DB_NAME;

                try
                {
                    
                    // make sure database exists on the flash card.  Copy blank database
                    // provided with firmware image to flash card if necessary.
                    if ( !DatabaseRestore( Controller.INET_DB_NAME ) ) // will return false if database already existed on flash
                    {
                        // If journal file is found on startup, then SchemaDataAccess.Find should cause it to be rolled back.
                        bool hotJournalExists = HotJournalExists( dbName );
                        if ( hotJournalExists )
                            Log.Warning( string.Format( "{0}: \"HOT\" JOURNAL FILE FOUND FOR {1}!", _name, dbName ) );

                        // We query the schema for two reasons: 1) We need its version number.
                        // 2) The query is done in a writable transaction, so that any hot journal
                        // file should be rolled back.
                        Schema schema = new SchemaDataAccess().Find( false );

                        // See if it was rolled back.
                        if ( hotJournalExists )
                            hotJournalExists = HotJournalExists( dbName );

                        if ( hotJournalExists )
                            Log.Error( string.Format( "{0}\"HOT\" JOURNAL FILE NOT ROLLED BACK FOR {1}!", _name, dbName ) );

                        if ( schema.Version != INET_VERSION )
                        {
                            Log.Info( string.Format( "{0}: Incompatible schema version ({1}) found for {2}.", _name, schema.Version, dbName ) );
                            Log.Info( string.Format( "{0}: Expected schema version {1}", _name, INET_VERSION ) );

                            // make sure account num in old database makes it to the new database.

                            Configuration.Schema = ResetInet( schema.AccountNum, schema.Activated, schema.IsManufacturing, schema.ServiceCode );
                        }
                        else
                        {
                            Configuration.Schema = schema;
                            Configuration.Schema.Log();
                        }
                    }
                    else
                    {
                        Configuration.Schema = new SchemaDataAccess().Find( false );
                        Configuration.Schema.Log();
                    }
                    Log.Info( string.Format( "{0}: {1} database initialized.", _name, dbName ) );
                }
                catch ( Exception e )
                {
                    _startedError = e.ToString();
                    Controller.RunState |= Controller.State.InetDbError;
                    Log.Error( string.Format( "{0}: FAILED TO INITIALIZE {1} DATABASE!", _name, dbName ), e );
                }
                finally
                {
                    try
                    {
                        Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.ReleaseMutex" );
                        Mutex.ReleaseMutex();
                    }
                    catch ( Exception e )
                    {
                        Log.Error( "Thread=" + Thread.CurrentThread.Name, e );
                        throw;
                    }

                }
            }
        }

        static private bool HotJournalExists( string dbName )
        {
            string jlFlashPath = Controller.FLASHCARD_PATH + dbName + JOURNAL_EXTENSION_SUFFIX; ////Rajesh 25-JANUARY-2012 INS-2399

            bool hotJournalExists = File.Exists( jlFlashPath );

            return hotJournalExists;
        }

        static public void StartInetQueue()
        {
            Log.Info( string.Format( "{0}: Initializing...", _name ) );

            //lock ( FlashCard.Lock )
            {
                Controller.RunState &= ~Controller.State.InetQueueError; // Clear existing InetQueueError bit.
                _startedError = null; // clear any existing error

                Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.WaitOne" );
                Mutex.WaitOne();

                try
                {
                    string dbName = Controller.INETQ_DB_NAME;

                    // make sure database exists on the flash card.  Copy blank database
                    // provided with firmware image to flash card if necessary.
                    if ( !DatabaseRestore( dbName ) ) // will return false if database already existed on flash
                    {
                        // If journal file is found on startup, then SchemaDataAccess.Find should cause it to be rolled back.
                        bool hotJournalExists = HotJournalExists( dbName );
                        if ( hotJournalExists )
                            Log.Warning( string.Format( "{0}: \"HOT\" JOURNAL FILE FOUND FOR {1}!", _name, dbName ) );

                        // We query the schema for two reasons: 1) We need its version number.
                        // 2) The query is done in a writable transaction, so that any hot journal
                        // file should be rolled back.
                        int schemaVersion = new QueueDataAccess( DataSource.iNetQueue ).GetSchemaVersion();

                        // See if it was rolled back.
                        if ( hotJournalExists )
                            hotJournalExists = HotJournalExists( dbName );

                        if ( hotJournalExists )
                            Log.Error( string.Format( "{0}\"HOT\" JOURNAL FILE NOT ROLLED BACK FOR {1}!", _name, dbName ) );

                        if ( schemaVersion != INET_QUEUE_VERSION )
                        {
                            Log.Info( string.Format( "{0}: Incompatible schema version ({1}) found for {2}.", _name, schemaVersion, dbName ) );
                            Log.Info( string.Format( "{0}: Expected schema version {1}", _name, INET_QUEUE_VERSION ) );

                            ResetInetQueue();
                        }
                    }

                    Log.Info( string.Format( "{0}: {1} database initialized.", _name, dbName ) );
                }
                catch ( Exception e )
                {
                    _startedError = e.ToString();
                    Controller.RunState |= Controller.State.InetQueueError;
                    Log.Error( string.Format( "{0}: FAILED TO INITIALIZE {1} DATABASE!", _name, Controller.INETQ_DB_NAME ), e );
                }
                finally
                {
                    try
                    {
                        Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.ReleaseMutex" );
                        Mutex.ReleaseMutex();
                    }
                    catch ( Exception e )
                    {
                        Log.Error( "Thread=" + Thread.CurrentThread.Name, e );
                        throw;
                    }

                }
            }
        }


        static public void Stop()
        {
            Log.Info( string.Format( "{0}: Stopping...", _name ) );

            //lock ( FlashCard.Lock )
            {
                Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.WaitOne" );
                Mutex.WaitOne();

                try
                {
                    // Clear out all handles to the database.
                    SQLiteConnection.ClearAllPools();

                    _startedError = _name + ".Stop() was called.";

                }
                finally
                {
                    Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.ReleaseMutex" );
                    Mutex.ReleaseMutex();
                }

                Log.Info( string.Format( "{0}: stopped.", _name ) );
            }
        }

        static public bool Started { get { return _startedError == null; } }

        static internal void CheckStarted()
        {
            if ( _startedError != null )
                throw new DataAccessException( string.Format( "{0} has not been started.\nError...\n", _name, _startedError ) );
        }

        /// <summary>
        /// Replaces the current iNet database with a new, empty database.  The "queue" database is not touched.
        /// </summary>
        static public Schema ResetInet( string accountNum, bool active, bool isManufacturing, string serviceCode )
        {
            Log.Info( string.Format( "{0}: Resetting {1} database to default provided with firmware...", _name, Controller.INET_DB_NAME ) );

            //lock ( FlashCard.Lock )
            {
                Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.WaitOne" );
                Mutex.WaitOne();

                try
                {
                    // Clear out all handles to the database.
                    SQLiteConnection.ClearAllPools();

                    DatabaseDelete( Controller.INET_DB_NAME );

                    StartInet();  // re-start.  It will automatically do a DatabaseRestore

                    //INS-2460 - When the database is reset, delete the cached firmware file directory                    
                    if ( Directory.Exists( firmwareFolderPath ) )
                    {
                        var di = new DirectoryInfo( firmwareFolderPath );
                        di.Attributes &= ~FileAttributes.ReadOnly; //Remove read only attributes if any
                        di.Delete( true );
                    }

                    new SchemaDataAccess().UpdateAccount( accountNum, active, isManufacturing, serviceCode );

                    Schema schema = new SchemaDataAccess().Find(true) ?? new Schema();

                    schema.Log();

                    Log.Info( string.Format( "{0}: Reset complete.", _name ) );

                    return schema;
                }
                finally
                {
                    Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.ReleaseMutex" );
                    Mutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Replaces the current Queue database with a new, empty database.  The "iNet" database is not touched.
        /// </summary>
        static public void ResetInetQueue()
        {
            Log.Info( string.Format( "{0}: Resetting {1} database to default provided with firmware...", _name, Controller.INETQ_DB_NAME ) );

            //lock ( FlashCard.Lock )
            {
                Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.WaitOne" );
                Mutex.WaitOne();

                try
                {
                    // Clear out all handles to the database.
                    SQLiteConnection.ClearAllPools();

                    DatabaseDelete( Controller.INETQ_DB_NAME );

                    StartInetQueue();  // re-start.  It will automatically do a DatabaseRestore

                    Log.Info( string.Format( "{0}: Reset complete.", _name ) );

                    return;
                }
                finally
                {
                    Log.Error( "Thread=" + Thread.CurrentThread.Name + "   Mutex.ReleaseMutex" );
                    Mutex.ReleaseMutex();
                }
            }
        }

        static private bool DatabaseRestore( string dbName )
        {
            string dbFlashPath = Controller.FLASHCARD_PATH + dbName;
            string jlFlashPath = Controller.FLASHCARD_PATH + dbName + JOURNAL_EXTENSION_SUFFIX;

            try
            {
                if ( File.Exists( dbFlashPath ) )
                {
                    Log.Info( string.Format( "{0}: Found database \"{1}\".", _name, dbFlashPath ) );
                    return false;
                }

                Log.Info( string.Format( "{0}: database \"{1}\" not found.", _name, dbFlashPath ) );
                Log.Info( string.Format( "{0}: Restoring database from image backup.", _name ) );
//#if !DEBUG
                string dbImagePath = Controller.WINDOWS_PATH + dbName;
                File.Copy( dbImagePath, dbFlashPath, true );
//#else
//#warning DEBUG Build - Database is restored from Flash Card, not from Image
//                string dbImagePath = dbFlashPath + ".bak";
//                Log.Info( "TODO: DEBUG MODE RESTORING FROM \"" + dbImagePath + "\"" );
//                File.Copy( dbImagePath, dbFlashPath, true );
//#endif
                ClearReadOnlyAttribute( dbFlashPath );

                Log.Info( string.Format( "{0}: Database restored from image backup.", _name ) );

                // Delete any old journal we find.
                if ( File.Exists( jlFlashPath ) )
                {
                    Log.Info( string.Format( "{0}: Deleting journal file \"{1}\".", _name, jlFlashPath ) );
                    DeleteFile( jlFlashPath );
                }

                return true;
            }
            catch ( Exception e )
            {
                Log.Error( _name + ".RestoreDatabase", e );
                throw;
            }
        }

        /// <summary>
        /// Deletes the specified database, and its journal file (it it exists).
        /// </summary>
        /// <param name="dbName"></param>
        static public void DatabaseDelete( string dbName )
        {
            string dbFlashPath = Controller.FLASHCARD_PATH + dbName;
            string jlFlashPath = Controller.FLASHCARD_PATH + dbName + JOURNAL_EXTENSION_SUFFIX;

            Log.Info( string.Format( "{0}: Deleting database \"{1}\".", _name, dbName ) );

            try
            {
                SQLiteConnection.ClearAllPools();

                // Delete any old journal we find.  It may be there if IDS was rebooted
                // rebooted while in the middle of a transaction.
                if ( File.Exists( jlFlashPath ) )
                {
                    Log.Info( string.Format( "{0}: Deleting journal file \"{1}\".", _name, jlFlashPath ) );
                    DeleteFile( jlFlashPath );
                }

                if ( !File.Exists( dbFlashPath ) )
                {
                    Log.Info( string.Format( "{0}: Database file \"{1}\"not found.  Nothing to delete.", _name, dbFlashPath ) );
                    return;
                }

                Log.Info( string.Format( "{0}: Deleting database file \"{1}\".", _name, dbFlashPath ) );
                DeleteFile( dbFlashPath );

                Log.Info( _name + ": Database deleted." );
            }
            catch ( Exception e )
            {
                Log.Error( _name + ".DeleteDatabase", e );
                throw;
            }
        }

        private static void DeleteFile( string filePath )
        {
            ClearReadOnlyAttribute( filePath );
            File.Delete( filePath );
        }

        /// <summary>
        /// Clears the file's read-only bit if it's set.
        /// </summary>
        /// <param name="filePath"></param>
        private static void ClearReadOnlyAttribute( string filePath )
        {
            FileInfo fi = new FileInfo( filePath );
            if ( ( fi.Attributes & FileAttributes.ReadOnly ) == FileAttributes.ReadOnly )
                fi.Attributes &= ~FileAttributes.ReadOnly;  
        }

        /// <summary>
        /// Formats up and logs (if using LoggerLevel.Trace) the parameterized query
        /// for the passed-in command
        /// </summary>
        /// <param name="command"></param>
		private void LogCommand( IDbCommand command )
		{
			// don't bother constructing the log message if not logging Trace messages
            if ( Configuration.DockingStation.LogLevel >= LogLevel.Trace )
			{
				StringBuilder sb = new StringBuilder( command.Parameters.Count * 25 );
				int count = 0;
				foreach ( IDbDataParameter param in command.Parameters )
				{
					if ( sb.Length > 0 ) sb.Append( "," );
					sb.Append( "p" + ( ++count ) + "=" );
					if ( param.Value == null )
						sb.Append( "(null)" );
					else
						sb.Append( param.Value.ToString() );
				}
                Log.Trace( command.CommandText );
                Log.Trace( sb.ToString() );
			}
		}
        static private void LogException( Exception e )
        {
//#if DEBUG
            Log.Error( "DATAACCESS ERROR - " + e.Message );
//#endif
        }

        protected string MakeCommaDelimitedParamNames( int count, string paramName )
        {
            StringBuilder strBuilder = new StringBuilder();

            for ( int i = 1; i <= count; i++ )
            {
                if ( strBuilder.Length > 0 )
                    strBuilder.Append( ", " );

                //strBuilder.Append( "'" );
                strBuilder.Append( paramName );
                strBuilder.Append( i.ToString() );
                //strBuilder.Append( "'" );
            }
            return strBuilder.ToString();
        }



		#region PROVIDER RELATED METHODS

        static internal IDbConnection GetConnection( DataSource dataSourceId, bool readOnly )
        {
            SQLiteConnection con = null;

            if ( dataSourceId == DataSource.iNetData )
                con = new SQLiteConnection( readOnly ? DataAccess.INET_CONNECTION_STRING_READONLY : DataAccess.INET_CONNECTION_STRING );

            else if ( dataSourceId == DataSource.iNetQueue )
                con = new SQLiteConnection( readOnly ? DataAccess.INET_QUEUE_CONNECTION_STRING_READONLY : DataAccess.INET_QUEUE_CONNECTION_STRING );

            else
                throw new ArgumentOutOfRangeException( "DataSourceId=" + dataSourceId );

            con.Open();

            // turn on foreign key support on the connection (it's off by default) before returning it.
            new SQLiteCommand( "PRAGMA foreign_keys = ON;", con ).ExecuteNonQuery();
            
            return con;
        }

        static internal IDbCommand GetCommand( string sql, IDbConnection con )
        {
            return new SQLiteCommand( sql, (SQLiteConnection)con );
        }


        static internal IDbCommand GetCommand( string sql, DataAccessTransaction trx )
		{
			return GetCommand( sql, NULL_INT_FLAG, trx );
		}

        //TODO:Ajay Duplicate this mehtod with IDataAccessTransaction method
        //Other method that is accepting DataAccessTransaction can be removed.
        static internal IDbCommand GetCommand(string sql, IDataAccessTransaction trx)
        {
            return GetCommand(sql, NULL_INT_FLAG, trx);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="commandTimeout">
        /// Number of seconds to wait for the command to complete.
        /// Ignored if NULL_INT_FLAG is specified.
        /// If not specified, then default is 30 seconds.
        /// </param>
        /// <param name="trx"></param>
        /// <returns></returns>
        static internal IDbCommand GetCommand( string sql, int commandTimeout, DataAccessTransaction trx )
		{
			IDbCommand cmd = GetCommand( sql, trx.Connection );
			cmd.Transaction = trx.Transaction;
			if ( commandTimeout != NULL_INT_FLAG )
				cmd.CommandTimeout = commandTimeout;

			return cmd;
		}


        //TODO:Ajay Duplicate this mehtod with IDataAccessTransaction method
        //Other method that is accepting DataAccessTransaction can be removed.
        static internal IDbCommand GetCommand(string sql, int commandTimeout, IDataAccessTransaction trx)
        {
            DataAccessTransaction transaction = (DataAccessTransaction)trx;
            IDbCommand cmd = GetCommand(sql, transaction.Connection);
            cmd.Transaction = transaction.Transaction;
            if (commandTimeout != NULL_INT_FLAG)
                cmd.CommandTimeout = commandTimeout;

            return cmd;
        }

        static internal IDbCommand GetCommand()
		{
			return new SQLiteCommand();
		}

		static private IDbDataAdapter GetAdapter()
        {
			return new SQLiteDataAdapter();
        }

		protected IDataParameter GetDataParameter( object value )
		{
			return GetDataParameter( string.Empty, value );
		}

		protected IDataParameter GetDataParameter( string name, object value )
		{
            if ( value == null )
                return new SQLiteParameter( name, DBNull.Value );

            if ( value is string )
            {
                if ( ( (string)value ).Length == 0 ) value = null;
            }

            else if ( value is long ) 
            {
                if ( (long)value == DomainModelConstant.NullLong ) value = null;
            }

            else if ( value is int )
            {
                if ( (int)value == DomainModelConstant.NullInt ) value = null;
            }

            else if ( value is short )
            {
                if ( (short)value == DomainModelConstant.NullShort ) value = null;
            }

            else if ( value is float )
            {
                if ( (float)value == DomainModelConstant.NullFloat ) value = null;
            }

            else if ( value is double )
            {
                if ( (double)value == DomainModelConstant.NullDouble ) value = null;
            }

            else if ( value is double )
            {
                if ( (double)value == DomainModelConstant.NullDouble ) value = null;
            }

            else if ( value is DateTime )
            {
                if ( (DateTime)value == DomainModelConstant.NullDateTime ) value = null;
            }

            else if ( value is TimeSpan )
            {
                TimeSpan ts = (TimeSpan)value;
                if ( ts == DomainModelConstant.NullTimeSpan )
                    value = null;
                else
                    value = ts.Ticks; // we store TimeStamps by persisting their ticks
            }

			return new SQLiteParameter( name, value ?? DBNull.Value );
        }

        #endregion PROVIDER RELATED METHODS


        #region SAFE GETS


        protected string SqlSafeGetString( IDataReader reader, int ordinal )
        {
            try
            {
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    return (string)o;
            }
            catch ( Exception ex )
            {
                LogException( ex );

                if ( !( ex is System.InvalidCastException || ex is IndexOutOfRangeException ) )
                {
                    string msg = string.Format( "DataAccess - Exception trying to read string from db column {0}: {1}", ordinal, ex );
                    throw new DataAccessException( msg, ex );
                }
            }
            return null;
        }

        protected string SqlSafeGetCLOB( IDataReader reader, int ordinal )
        {
            return SqlSafeGetCLOB( reader, ordinal );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="ordinal"></param>
        /// <returns></returns>
        protected byte[] SqlSafeGetBLOB( IDataReader reader, int ordinal )
        {
            object o = reader[ ordinal ];

            if ( o == Convert.DBNull )
                return new byte[ 0 ];

            long numBlobBytes = reader.GetBytes( ordinal, 0, null, 0, int.MaxValue );

            byte[] blobBytes = new byte[ numBlobBytes ];

            reader.GetBytes( ordinal, 0, blobBytes, 0, blobBytes.Length );

            return blobBytes;
        }

        /// <summary>
        /// Checks for null before trying to read value from table.
        /// Although dates are stored as UTC, this call will convert the date
        /// to Local time before returning it to the caller.
        /// </summary>
        /// <param name="reader">read from</param>
        /// <param name="ordinal">table column</param>
        /// <returns></returns>
        protected DateTime SqlSafeGetDateTime( IDataReader reader, int ordinal )
        {
            return SqlSafeGetDateTime( reader, ordinal, DateTimeKind.Local );
        } 

		/// <summary>
		/// Checks for null before trying to read value from table.
		/// </summary>
		/// <param name="reader">read from</param>
        /// <param name="ordinal">table column</param>
        /// <param name="kind"></param>
		/// <returns></returns>
        protected DateTime? SqlSafeGetNullableDateTime( IDataReader reader, int ordinal, DateTimeKind kind )
		{
			try
			{
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                {
                    DateTime date = (DateTime)o;

                    date = DateTime.SpecifyKind( date, kind );

                    return date;
                }
			}
			catch( Exception e )
			{
				LogException( e );

				if ( !( e is IndexOutOfRangeException ) )
				{
                    string msg = string.Format( "DataAccess - Exception trying to read datetime from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
				}
			}

            return null;
		}

		/// <summary>
		/// Checks for null before trying to read value from table.
		/// </summary>
		/// <param name="reader">read from</param>
        /// <param name="ordinal">table column</param>
        /// <param name="kind">If Local, then method converts the DateTime
        /// to Local time before returning the caller.
        /// Else if UTC, then it's returned as UTC (which is how it's stored)</param>
		/// <returns></returns>
        protected DateTime SqlSafeGetDateTime( IDataReader reader, int ordinal, DateTimeKind kind )
        {
            DateTime? date = SqlSafeGetNullableDateTime( reader, ordinal, kind );

            return ( date == null ) ? DomainModelConstant.NullDateTime : (DateTime)date;
        }

        /// <summary>
        /// Checks for null before trying to read value from table.
        /// </summary>
        /// <param name="reader">read from</param>
        /// <param name="ordinal">table column</param>
        /// <returns></returns>
        protected DateTime? SqlSafeGetNullableDate( IDataReader reader, int ordinal )
        {
            try
            {
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    return ( (DateTime)o ).Date;
            }
            catch ( Exception e )
            {
                LogException( e );

                if ( !( e is IndexOutOfRangeException ) )
                {
                    string msg = string.Format( "DataAccess - Exception trying to read date from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
                }
            }

            return null;
        }

        protected DateTime SqlSafeGetDate( IDataReader reader, int ordinal )
        {
            DateTime? date = SqlSafeGetNullableDate( reader, ordinal );

            return ( date == null ) ? DomainModelConstant.NullDateTime : (DateTime)date;
        }

        protected int SqlSafeGetInt( IDataReader reader, int ordinal )
		{
            object o;
			try
			{
                o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    return (int)((long)o);
			}
			catch( Exception e )
			{
				LogException( e );

				if ( !( e is IndexOutOfRangeException ) )
				{
                    string msg = string.Format( "DataAccess - Exception trying to read int from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
				}
			}
			return DomainModelConstant.NullInt;
		}

        protected short SqlSafeGetShort( IDataReader reader, int ordinal )
        {
            try
            {
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    return (short)o;
            }
            catch ( Exception e )
            {
                LogException( e );

                if ( !( e is IndexOutOfRangeException ) )
                {
                    string msg = string.Format( "DataAccess - Exception trying to read short from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
                }
            }
            return DomainModelConstant.NullShort;
        }

		protected ushort SqlSafeGetUShort( IDataReader reader, int ordinal )
		{
			try
			{
				object o = reader[ordinal];
                // Everything stored by sqlite is signed, so the reader always returns signed values.
                // In order to return an unsigned value, we need to first cast to signed, and then cast that to unsigned.
				if ( o != Convert.DBNull )
					return (ushort)(short)o;
			}
			catch ( Exception e )
			{
				LogException( e );

				if ( !( e is IndexOutOfRangeException ) )
				{
					string msg = string.Format( "DataAccess - Exception trying to read ushort from db column {0}: {1}", ordinal, e );
					throw new DataAccessException( msg, e );
				}
			}
			return DomainModelConstant.NullUShort;
		}


        protected long SqlSafeGetLong( IDataReader reader, int ordinal )
        {
            try
            {
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    return (long)o;
            }
            catch ( Exception e )
            {
                LogException( e );

                if ( !( e is IndexOutOfRangeException ) )
                {
                    string msg = string.Format( "DataAccess - Exception trying to read long from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
                }
            }
            return DomainModelConstant.NullLong;
        }

        protected double SqlSafeGetDouble( IDataReader reader, int ordinal )
		{
            return SqlSafeGetDouble( reader, ordinal, DomainModelConstant.NullDouble );
		}

        protected double SqlSafeGetDouble( IDataReader reader, int ordinal, double defaultValue )
		{
			double val = defaultValue;

			try
			{
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    val = (double)o;

                return val;
			}
			catch( Exception e )
			{
				LogException( e );

				if ( !( e is IndexOutOfRangeException ) )
				{
                    string msg = string.Format( "DataAccess - Exception trying to read double from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
				}
			}

			return val;
		}

        protected float SqlSafeGetFloat( IDataReader reader, int ordinal )
        {
            return SqlSafeGetFloat( reader, ordinal, DomainModelConstant.NullFloat );
        }

        protected float SqlSafeGetFloat( IDataReader reader, int ordinal, float defaultValue )
        {
            float val = defaultValue;

            try
            {
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    val = (float)((double)o);

                return val;
            }
            catch ( Exception e )
            {
                LogException( e );

                if ( !( e is IndexOutOfRangeException ) )
                {
                    string msg = string.Format( "DataAccess - Exception trying to read float from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
                }
            }

            return val;
        }

        protected decimal SqlSafeGetDecimal( IDataReader reader, int ordinal )
        {
            return SqlSafeGetDecimal( reader, ordinal, DomainModelConstant.NullDecimal );
        }

        protected decimal SqlSafeGetDecimal( IDataReader reader, int ordinal, decimal defaultValue )
        {
            decimal val = defaultValue;

            try
            {
                object o = reader[ ordinal ];

                if ( o != Convert.DBNull )
                    val = (decimal)o;

                return val;
            }
            catch ( Exception e )
            {
                LogException( e );

                if ( !( e is IndexOutOfRangeException ) )
                {
                    string msg = string.Format( "DataAccess - Exception trying to read decimal from db column {0}: {1}", ordinal, e );
                    throw new DataAccessException( msg, e );
                }
            }

            return val;
        }

        #endregion SAFE GETS

        #region SAFE SETS


        #endregion SAFE SETS

        #region STRING FROMs


        protected static string ToSqlString( DateTime dateTime )
        {
            string sqlDate = string.Format( "{0}-{1}-{2} {3}:{4}:{5}",
                            dateTime.Year.ToString().PadLeft( 4, '0' ),
                            dateTime.Month.ToString().PadLeft( 2, '0' ),
                            dateTime.Day.ToString().PadLeft( 2, '0' ),
                            dateTime.Hour.ToString().PadLeft( 2, '0' ),
                            dateTime.Minute.ToString().PadLeft( 2, '0' ),
                            dateTime.Second.ToString().PadLeft( 2, '0' ) );

            //if ( includeMilliseconds )
            //    sqlDate += "." + inDate.Millisecond.ToString().PadLeft( 3, '0' ).PadRight( 6, '0' );

            return sqlDate;
        }

        #endregion STRING FROMs

		#region SQL COMMAND EXECUTION

		protected object ExecuteScalar( string sql, DataAccessTransaction trx )
        {
            return ExecuteScalar( sql, trx.Connection, trx.Transaction );
        }

        private object ExecuteScalar( string sql, IDbConnection con, IDbTransaction trx )
        {
			Log.Trace( sql );

            if ( con.State != ConnectionState.Open )
                throw new DataAccessException( "executeScalar() requires an open connection." );

            Object scalarValue;
            using ( IDbCommand cmd = GetCommand( sql, con ) )
            {

                if ( trx != null )
                    cmd.Transaction = trx;

                scalarValue = cmd.ExecuteScalar();
            }
            return ( scalarValue is DBNull ) ? null : scalarValue;
        }

		protected IDataReader ExecuteQuery( DataAccessTransaction trx, string sql )
		{
			return ExecuteQuery( trx.Connection, trx.Transaction, sql );
		}

		private IDataReader ExecuteQuery( IDbConnection con, IDbTransaction trx, string sql )
		{
			Log.Trace( sql );
			IDataReader reader = null;

			try
			{
				IDbCommand cmd = GetCommand( sql, con );

				if ( trx != null )
					cmd.Transaction = trx;

				reader = cmd.ExecuteReader();
			}
			catch ( Exception e )
			{
				LogException( e );
                throw new DataAccessException( sql, e );
			}

			return reader;
		}

		protected int ExecuteNonQuery( string sql, DataAccessTransaction trx )
		{
			return ExecuteNonQuery( sql, NULL_INT_FLAG, trx );
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sql"></param>
		/// <param name="commandTimeout">
		/// Number of seconds to wait for the command to complete.
		/// Ignored if NULL_INT_FLAG is specified.
		/// If not specified, then default is 30 seconds.
		/// </param>
		/// <param name="trx"></param>
		/// <returns></returns>
		protected int ExecuteNonQuery( string sql, int commandTimeout, DataAccessTransaction trx )
		{
			IDbCommand cmd = trx.Connection.CreateCommand();
			cmd.CommandText = sql;
			cmd.Transaction = trx.Transaction;

			if ( commandTimeout != NULL_INT_FLAG )
				cmd.CommandTimeout = commandTimeout;

            LogCommand( cmd ); // log the query

            try
            {
                int numRows = cmd.ExecuteNonQuery(); // get the number of rows effected
                return numRows; // return the number of rows effected
            }
            catch ( Exception e )
            {
                Log.Error( e );

                if ( e is SQLiteException )
                {
                    SQLiteException se = (SQLiteException)e;

                    if ( se.ErrorCode == SQLiteErrorCode.Constraint )  //TODO - need to verify this
                        return (int)DataAccessErrorCode.UniqueContraintViolation;

                    // TODO - what is the code for sqlite?
                    //if ( se.ErrorCode == 2292 ) // For DB2, we were checking for error code of 23504.
                    //    return (int)DataAccessErrorCode.UpdateDeleteRuleViolation;
                }

                LogException( e );

                return (int)DataAccessErrorCode.NullId;
            }
		}

        /// <summary>
        /// Returns the total number of records in the table named by the TableName property.
        /// </summary>
        /// <param name="trx"></param>
        /// <returns></returns>
        public virtual long FindCount( DataAccessTransaction trx )
        {
            string sql = "SELECT COUNT(*) FROM " + TableName;

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                long count = (long)cmd.ExecuteScalar();
                return count;
            }
        }

        #endregion SQL COMMAND EXECUTION

		/// <summary>
        /// By default, the table name is the same as the derived class's name, minus
        /// the string "DataAccess".
        /// e.g. if class is "WidgetDataAccess". then table name is assumed to be "Widget".
        /// Subclasses are free to override this property, though, to provide
        /// a different table name if needed.
        /// </summary>
        public virtual string TableName
        {
            get
            {
				if ( _tableName == null )
				{
					Type type = this.GetType();

					lock ( _tableNames )
					{
						// See if we already have a cached table name for this class's type.
						_tableNames.TryGetValue( type, out _tableName );

						if ( _tableName == null )
						{
							// If class is "My.Name.Space.WidgetDataAccess", then table name
							// is assumed to be simply "Widget".

							string fullTypeName = type.ToString();

							int tableNameStart = fullTypeName.LastIndexOf( '.' ) + 1;
							int tableNameEnd = fullTypeName.LastIndexOf( "DataAccess" );

							_tableName = fullTypeName.Substring( tableNameStart, tableNameEnd - tableNameStart ).ToUpper();

							_tableNames[type] = _tableName;
						}
					}
				}
				return _tableName;
            }
        }

        /// <summary>
        /// Returns the total byte size of the database. This is the total number of
        /// pages (including both used and unused pages) multiplied by the page size.
        /// </summary>
        /// <returns>The size returned is in bytes.</returns>
        public static long GetTotalSize( DataSource dataSourceId )
        {
            long pageCount = 0;
            long pageSize = 0;

            //lock ( FlashCard.Lock )
            {
                Mutex.WaitOne();

                try
                {
                    using ( IDbConnection con = GetConnection( dataSourceId, false ) )
                    {
                        using ( IDbCommand cmd = GetCommand( "PRAGMA page_count", con ) )
                        {
                            pageCount = (long)cmd.ExecuteScalar();
                        }
                        using ( IDbCommand cmd = GetCommand( "PRAGMA page_size", con ) )
                        {
                            pageSize = (long)cmd.ExecuteScalar();
                        }
                    }
                }
                finally
                {
                    Mutex.ReleaseMutex();
                }
            }

            long size = pageCount * pageSize;

            return size;
        }

        /// <summary>
        /// Returns the number of unused bytes in the database. This is the total number of
        /// unused pages multiplied by the page size.
        /// </summary>
        /// <returns>The size returned is in bytes.</returns>
        public static long GetFreeSize( DataSource dataSourceId )
        {
            long pageCount = 0;
            long pageSize = 0;

            //lock ( FlashCard.Lock )
            {
                Mutex.WaitOne();

                try
                {
                    using ( IDbConnection con = GetConnection( dataSourceId, false ) )
                    {
                        using ( IDbCommand cmd = GetCommand( "PRAGMA freelist_count", con ) )
                        {
                            pageCount = (long)cmd.ExecuteScalar();
                        }
                        using ( IDbCommand cmd = GetCommand( "PRAGMA page_size", con ) )
                        {
                            pageSize = (long)cmd.ExecuteScalar();
                        }
                    }
                }
                finally
                {
                    DataAccess.Mutex.ReleaseMutex();
                }
            }

            long size = pageCount * pageSize;

            return size;
        }

        /// <summary>
        /// Removes unused space from the specified database.
        /// </summary>
        /// <param name="dataSourceId"></param>
        public static void DatabaseCompact( DataSource dataSourceId )
        {
            //lock ( FlashCard.Lock )
            {
                Mutex.WaitOne();

                try
                {
                    using ( IDbCommand cmd = GetCommand( "VACUUM", GetConnection( dataSourceId, false ) ) )
                    {
                        int success = cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    Mutex.ReleaseMutex();
                }
            }
        }
    }
}
