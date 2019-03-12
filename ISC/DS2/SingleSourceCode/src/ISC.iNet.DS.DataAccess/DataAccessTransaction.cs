using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// Encapsulates a database transaction that can be used by classes
    /// above the DataAccess layer that shouldn't need (nor be allowed)
    /// to worry about specifics of creating and managing a transaction 
    /// and its connection to the database.
    /// </summary>
    /// <remarks>
    /// A suggested pattern of usage is as follows...
    /// <code>
    /// 
    ///   using ( DataAccessTransaction trx = new DataAccessTransaction() )
    ///   {
    ///       DoDatabaseWork( trx );  // will throw on error.
    /// 
    ///       DoMoreDatabaseWork( trx ); // will throw on error.
    /// 
    ///       trx.Commit();
    ///   }
    /// 
    /// </code>
    /// Note that this class implements IDisposable. The Dispose() method will
    /// rollback any still-open transation.  The above code example is taking
    /// advantage of how the 'using' keyword implicitly calls Dispose() in 
    /// a hidden finally block at the end of the using block.
    /// /// </remarks>
    public class DataAccessTransaction : IDataAccessTransaction
    {
        private bool _isDisposed = false;

        private IDbConnection _con = null;

        private IDbTransaction _trx = null;

        private List<DockingStationError> _errors = new List<DockingStationError>();

        private DateTime _timeStampUtc = DateTime.UtcNow;

        public DataAccess.DataSource DataSourceId { get; private set; }

        public bool ReadOnly { get; private set; }

        public DataAccessHint Hint { get; private set; }

        private void Init( DataAccess.DataSource dataSourceId, bool readOnly, DataAccessHint hint )
        {
            //lock ( FlashCard.Lock )
            {
                Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.WaitOne" );
                DataAccess.Mutex.WaitOne();

                try
                {
                    DataAccess.CheckStarted();

                    _con = DataAccess.GetConnection( dataSourceId, readOnly );

                    if ( !readOnly )
                        _trx = _con.BeginTransaction();
                }
                catch ( Exception e )
                {
                    Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.ReleaseMutex" );
                    DataAccess.Mutex.ReleaseMutex();

                    Log.Error( "Error instantiating DataAccessTransaction", e );

                    throw;
                }
            }

            DataSourceId = dataSourceId;

            ReadOnly = readOnly;

            Hint = hint;
        }

        public DataAccessTransaction()
        {
            Init( DataAccess.DataSource.iNetData, false, DataAccessHint.None );
        }

        /// <summary>
        /// </summary>
        public DataAccessTransaction( DataAccess.DataSource dataSourceId )
        {
            Init( dataSourceId, false, DataAccessHint.None );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="readOnly"></param>
        public DataAccessTransaction( DataAccess.DataSource dataSourceId, bool readOnly )
        {
            Init( dataSourceId, readOnly, DataAccessHint.None );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="readOnly"></param>
        public DataAccessTransaction( bool readOnly )
        {
            Init( DataAccess.DataSource.iNetData, readOnly, DataAccessHint.None );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="readOnly"></param>
        public DataAccessTransaction( DataAccessHint hint )
        {
            Init( DataAccess.DataSource.iNetData, false, hint );
        }

        /// <summary>
		/// Finalizing destructor.
		/// Calls virtual Dispose method as part of standard IDisposable design pattern.
		/// </summary>
        /// <remarks>Rollsback transaction if it's still currently open.  Closes connection.</remarks>
        ~DataAccessTransaction()
		{
			Dispose( false );
		}

        /// <summary>
        /// Implementation of IDisposable. Rolls back transaction if it's still open.
        /// </summary>
        public void Dispose()
        {
            // Call the virtual Dispose method as part of standard IDisposable design pattern.
            Dispose( true );  // Dispose(bool) does the actual cleanup.
        }

        /// <summary>
        /// Called by public Dispose() in order
        /// to implement standard IDisposable design pattern.
        /// </summary>
        /// <param name="disposing"></param>
        private void Dispose( bool disposing )
        {
            if ( !_isDisposed ) // only dispose once
            {
                if ( disposing )
                {
                    // FREE MANAGED RESOURCES HERE IN THIS IF-BLOCK

                    try
                    {
                        Rollback();
                    }
                    catch {}

                    if ( _con != null && _con.State != ConnectionState.Closed )
                    {
                        //TraceLogger.Log( "Open connection detected in DataAccessTransaction!" );
                        try
                        {
                            _con.Close();
                        }
                        catch { }
                        _con = null;
                    }

                    Log.Trace( "Thread=" + Thread.CurrentThread.Name + "   Mutex.ReleaseMutex" );
                    DataAccess.Mutex.ReleaseMutex();

                } // end-if disposing

                // FREE UNMANAGED RESOURCES HERE.

            }
            this._isDisposed = true;
        }

        internal IDbTransaction Transaction
        {
            get { return _trx; }
        }

        internal IDbConnection Connection
        {
            get { return _con; }
        }

        /// <summary>
        /// The date/tome the transaction was started, in UTC.
        /// </summary>
        public DateTime TimestampUtc { get { return _timeStampUtc; } }

        //public DataAccessHint Hint
        //{
        //    get { return _hint; }
        //    set { _hint = value; }
        //}

        public void Rollback()
        {
            // Will be null if transaction is ReadOnly, or if transaction was already rolled back or committed.
            if ( _trx == null ) 
                return;

            try
            {
                _trx.Rollback();
                string msg = "DataAcessTransaction - ROLLED BACK";
				Log.Info( msg );
            }
            catch ( Exception e )
            {
				string msg = "DataAcessTransaction - FAILURE ROLLING BACK";
				Log.Error( msg, e );
                throw new DataAccessException( msg, e );
            }
            finally
            {
                _trx = null;
            }
        }

        public void Commit()
        {
            if ( _trx == null ) // read-only, or already rolled back or committed?
                return;

            try
            {
                _trx.Commit();
            }
            catch ( Exception e )
            {
				string msg = "DataAcessTransaction - FAILURE COMMITTING";
				Log.Error( msg, e );
                throw new DataAccessException( msg, e );
            }
            finally
            {
                _trx = null;
            }
        }

        public IList<DockingStationError> Errors
        {
            get { return _errors; }
        }
    }
}
