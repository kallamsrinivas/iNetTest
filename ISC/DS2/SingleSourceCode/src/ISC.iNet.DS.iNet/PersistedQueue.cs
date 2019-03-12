using System;
using System.Collections.Generic;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.iNet 
{
    public class PersistedQueue //: IMessageQueue
    {
        static private bool _paused = false;
        private const string _name = "PersistedQueue";

        private ISC.iNet.DS.DataAccess.DataAccess.DataSource _queueDataSource;

        private QueueDataAccess _queueDataAccess = null;

        private QueueDataAccess queueDataAccess => _queueDataAccess ?? new QueueDataAccess(_queueDataSource);

        /// <summary>
        /// If Paused, then calls to Send do nothing, and calls to Peek and Receive return nothing.
        /// The default is UnPaused.  PersistedQueue should be paused then shutdown before doing a reboot.
        /// </summary>
        static public bool Paused
        {
            get { return _paused; }

            set
            {
                Log.Debug( "Setting PersistedQueue.Paused = " + value );

                // we lock, so that if called, but queue is currently doing something, we'll
                // block until it's done.  i.e., if we want to pause queue, then a call to 'Paused = true'
                // won't return until we know that queue isn't doing anything any longer.
                lock ( FlashCard.Lock )
                {
                    _paused = value;
                }

                Log.Debug( "PersistedQueue.Paused = " + _paused );
            }
        }

        #region IMessageQueue Members

        private PersistedQueue( ISC.iNet.DS.DataAccess.DataAccess.DataSource dataSourceId )
        {
            _queueDataSource = dataSourceId;
        }

        public PersistedQueue(QueueDataAccess queueDataAccess)
        {
#if !TEST
            throw new NotSupportedException("This constructor is only intended for unit testing");
#endif
            _queueDataAccess = queueDataAccess;
        }

        public static PersistedQueue CreateInetInstance()
        {
            return new PersistedQueue( ISC.iNet.DS.DataAccess.DataAccess.DataSource.iNetQueue );
        }

        /// <summary>
        /// Only intended for unit testing
        /// </summary>
        /// <param name="queueDataAccess"></param>
        /// <returns></returns>
        public static PersistedQueue CreateInetInstance(QueueDataAccess queueDataAccess)
        {
            return new PersistedQueue(queueDataAccess);
        }

        /// <summary>
        /// Deletes the item on the front of the queue.
        /// </summary>
        /// <returns>true or false based on success/failure of the operation.</returns>
        public bool Delete()
        {
            bool deleted = false;
            try
            {
                using ( DataAccessTransaction trx = new DataAccessTransaction( _queueDataSource, false ) )
                {
                    QueueDataAccess dataAccess = queueDataAccess;

                    long id = dataAccess.FindOldestId( trx );

                    if ( id != DomainModelConstant.NullId )
                        deleted = dataAccess.Delete( id, trx );

                    trx.Commit();
                }
            }
            catch ( Exception ex )
            {
                Log.Error( "Could not delete oldest item from the queue!" );
                Log.Error( ex.ToString() );
            }
            return deleted;
        }

        public bool Delete( long id )
        {
            try
            {
                return queueDataAccess.Delete( id );
            }
            catch ( Exception ex )
            {
                Log.Error( string.Format( "Could not delete item {0} from the queue!", id ) );
                Log.Error( ex.ToString() );
                return false;
            }
        }

        /// <summary>
        /// Deletes the entire contents of the queue.
        /// </summary>
        /// <remarks>
        /// THIS CAN TAKE A LONG TIME IF THE QUEUE CONTAINS MAIN ITEMS.
        /// NOT RECOMMENDED TO BE CALLED BY INETDS'S APPLICATION SOFTWARE.
        /// USE FOR TESTING ONLY.
        /// </remarks>
        public void DeleteQueue()
        {
            try
            {
                queueDataAccess.DeleteQueue();
            }
            catch ( Exception ex )
            {
                Log.Error( "Could not delete all items from the queue!" );
                Log.Error( ex.ToString() );
            }
        }

        public long GetCount()
        {
            return queueDataAccess.GetCount();   
        }

        public bool IsEmpty()
        {
            long id = queueDataAccess.FindOldestId();

            return id == DomainModelConstant.NullId;
        }

        public object Peek()
        {
            QueueDataAccess dataAccess = queueDataAccess;
            // receive gets the object on the top of the queue and a deletes it.Only delete if something is returned.
            PersistedQueueData persistedQueueData = dataAccess.FindOldest();

            if ( persistedQueueData != null )
                return new QueueData( persistedQueueData );
            else
                return null;
        }

        public void Enqueue( QueueData queueData )
        {
            if ( Paused )
            {
                Log.Debug( "PersistedQueue.Send is DISABLED" );
                return;
            }

            PersistedQueueData persistedQueueData = queueData.CreatePersistedQueueData();

            double kB = (double)persistedQueueData.SerializedWebServiceParameter.Length / 1024.0d; // convert bytes to kilobytes
            Log.Debug( string.Format( "{0}: Queueing {1} ({2} KB)", _name, queueData, kB.ToString( "f1" ) ) );

            DateTime startTime = DateTime.UtcNow;

            // Save returns a false on a constraint violation (duplicate).
            if (queueDataAccess.Save( persistedQueueData ) == false )
                throw new Exception( string.Format( "{0}: Could not queue {1}. Save returned false.", _name, queueData ) );

            TimeSpan elapsed = DateTime.UtcNow - startTime;

            Log.Debug( string.Format( "{0}: {1} KB {2} successfully queued in {3} seconds.", _name, kB.ToString( "f1" ), queueData, elapsed.TotalSeconds ) );
        }

        public IEnumerable<QueueData> List( int count )
        {
            if ( Paused )
            {
                Log.Debug( "PersistedQueue.List is DISABLED" );
                return new List<QueueData>(); ;
            }

            QueueDataAccess dataAccess = queueDataAccess;

            // receive gets the object on the top of the queue and a deletes it.Only delete if something is returned.
            Queue<PersistedQueueData> q = new Queue<PersistedQueueData>( dataAccess.FindAll( count ) );

            List<QueueData> qdList = new List<QueueData>( q.Count );

            while ( q.Count > 0 )
                qdList.Add( new QueueData( q.Dequeue() ) );

            return qdList;          
        }

        public int GetSchemaVersion()
        {
            return queueDataAccess.GetSchemaVersion();
        }

#endregion
    }
}
