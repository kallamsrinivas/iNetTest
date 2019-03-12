using System;
using System.Collections.Generic;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet;
using ISC.WinCE.Logger;
using System.Threading;


namespace ISC.iNet.DS.Services
{
    public class MaintenanceOperation : MaintenanceAction, IOperation
    {
        #region Fields

        private const int MAX_MFG_DAYS = 30;

        #endregion

		#region Constructors

        MaintenanceEvent _maintenanceEvent;

        private void Init()
        {
            _maintenanceEvent = new MaintenanceEvent( this );
        }

		/// <summary>
        /// Creates a new instance of MaintenanceOperation class.
		/// </summary>
		public MaintenanceOperation()
        {
            Init();
        }

        public MaintenanceOperation( MaintenanceAction maintenanceAction )
            : base( maintenanceAction )
        {
            Init();
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Executes an instrument set server ip address settings operation.
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            try
            {
                RemoveOldData();

                RemoveOldMfgData();

                RemoveServiceData();

                ShrinkInetDatabase();

                ShrinkQueueDatabase();
            }
            catch ( Exception ex )
            {
                _maintenanceEvent.Errors.Add( new DockingStationError( ex.ToString(), DockingStationErrorLevel.Warning ) );
                Log.Error( ex );
            }

            return _maintenanceEvent;
        }

        /// <summary>
        /// Removes data for old equipment that is no longer associated with the account.
        /// </summary>
        private void RemoveOldData()
        {
            // Contact the iNet server to get lists (serial numbers) of equipment to delete

            Schema schema = Configuration.Schema;

            DateTime version = ( schema.EquipmentVersion == null ) ? Configuration.DockingStation.SetupDate : (DateTime)schema.EquipmentVersion;

            // Create lists to fill in with data returned by iNet.
            List<string> instruments = new List<string>();
            List<string> sensors = new List<string>();

            DateTime? equipmentVersion = null;

            using ( InetDownloader inetDownloader = new InetDownloader() )
            {
                equipmentVersion = inetDownloader.DownloadRemovedEquipment( instruments, sensors, version, _maintenanceEvent.Errors );
            }

            if ( equipmentVersion == null )
            {
                Log.Debug( string.Format( "{0}: Received null for equipmentVersion. ", Name ) );
                Log.Debug( string.Format( "{0}: Couldn't connect to iNet?  Then there's nothing more we can do.", Name ) );
                return;
            }

            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                Log.Debug( string.Format( "{0}: Deleting eventjournals for {1} instruments", Name, instruments.Count ) );
                RemoveEventJournals( instruments, trx );

                Log.Debug( string.Format( "{0}: Deleting eventjournals for {1} sensors", Name, sensors.Count ) );
                RemoveEventJournals( sensors, trx );

                //Log.Debug( string.Format( "{0}: Deleting data for {1} sensors", Name, instruments.Count ) );
                //RemoveSchedules( sensors, trx );

                new SchemaDataAccess().UpdateEquipmentVersion( equipmentVersion, trx );

                trx.Commit();

                Configuration.Schema.EquipmentVersion = equipmentVersion;

                _maintenanceEvent.Errors.AddRange( trx.Errors );
            }
        }

        /// <summary>
        /// Deletes all event journal records from the database for the specified serial numbers.
        /// </summary>
        /// <param name="serialNumbers"></param>
        /// <param name="trx"></param>
        private void RemoveEventJournals( IList<string> serialNumbers, DataAccessTransaction trx )
        {
            // Delete all EventJournals matching the serial numbers.
            // We do so in batches for better performance.

            const int batchSize = 25;

            EventJournalDataAccess eventJournalDataAccess = new EventJournalDataAccess();

            Queue<string> snQueue = new Queue<string>( serialNumbers );
            while ( snQueue.Count > 0 )
            {
                IList<string> snbatch = GetBatch( snQueue, batchSize );
                eventJournalDataAccess.DeleteBySerialNumbers( snbatch, trx );
            }
        }

        /// <summary>
        /// Removes old manufacturing data that is no longer needed in the iNet DS database.
        /// </summary>
        private void RemoveOldMfgData()
        {
            // Only purge old data if this is a manufacturing account.  If this is a docking station that belongs 
            // to a non-manufacturing account, then simply leave this method before taking any action.
            if (!Configuration.Schema.IsManufacturing)
                return;

            // Calculate the time span to apply as an age limit on the journal entries.
            TimeSpan journalAge = new TimeSpan(MAX_MFG_DAYS, 0, 0, 0);

            using (DataAccessTransaction trx = new DataAccessTransaction())
            {
                Log.Debug(string.Format("{0}: Deleting eventjournals from the manufacturing account older than {1} days, {2} hours, {3} minutes, and {4} seconds",
                                        Name, journalAge.Days, journalAge.Hours, journalAge.Minutes, journalAge.Seconds));
                RemoveEventJournals(journalAge, trx);
                trx.Commit();
            }
        }


        /// <summary>
        /// To remove the old data specific to Repair if any available, if the DSX is no longer is in a Repair account. INS-7282
        /// </summary>
        private void RemoveServiceData()
        {
            if ( Configuration.IsRepairAccount() )
                return;

            using (DataAccessTransaction trx = new DataAccessTransaction())
            {
                Log.Debug( string.Format( "Deleting Sensor Calibration Limits if any, since this is no more a service account. Account type is {0}", Configuration.Schema.ServiceCode ) );                                
                new SensorCalibrationLimitsDataAccess().Delete( trx );

                //Consider Critical Errors, since this also can be configured at the Repair accounts level
                trx.Commit();
            }
        }

        /// <summary>
        /// Deletes all event journal records from the database older than the specified amount of time.
        /// </summary>
        /// <param name="journalAge"></param>
        /// <param name="trx"></param>
        private void RemoveEventJournals(TimeSpan journalAge, DataAccessTransaction trx)
        {
            EventJournalDataAccess eventJournalDataAccess = new EventJournalDataAccess();
            eventJournalDataAccess.DeleteByTime(journalAge, trx);
        }

        private void RemoveSchedules( IList<string> serialNumbers, DataAccessTransaction trx )
        {
            /****  We do not remove schedules or settings.  They are deleted via normal
             ****  ExchangeStatusOperations when equipment is removed from an account.

            const int batchSize = 25;

            Queue<string> snQueue = new Queue<string>( serialNumbers );

            ScheduledUponDockingDataAccess uponDockDataAccess = new ScheduledUponDockingDataAccess();
            ScheduledHourlyDataAccess hourlyDataAccess = new ScheduledHourlyDataAccess();
            ScheduledDailyDataAccess dailyDataAccess = new ScheduledDailyDataAccess();
            ScheduledWeeklyDataAccess weeklyDataAccess = new ScheduledWeeklyDataAccess();
            ScheduledMonthlyDataAccess monthlyDataAccess = new ScheduledMonthlyDataAccess();
            ScheduledOnceDataAccess onceDataAccess = new ScheduledOnceDataAccess();

            // First, delete all EventJournals matching the instrument serial numbers.
            // We do so in batches.

            while ( snQueue.Count > 0 )
            {
                IList<string> snbatch = GetBatch( snQueue, batchSize );

                foreach ( string sn in snbatch )
                    Log.Debug( string.Format( "{0}: Deleting S/N \"{1}\"", Name, sn ) );

                uponDockDataAccess.DeleteBySerialNumbers( snbatch, trx );
                hourlyDataAccess.DeleteBySerialNumbers( snbatch, trx );
                dailyDataAccess.DeleteBySerialNumbers( snbatch, trx );
                weeklyDataAccess.DeleteBySerialNumbers( snbatch, trx );
                monthlyDataAccess.DeleteBySerialNumbers( snbatch, trx );
                onceDataAccess.DeleteBySerialNumbers( snbatch, trx );
            }
            ****/
        }

        private IList<string> GetBatch( Queue<string> q, int batchSize )
        {
            List<string> snList = new List<string>( batchSize );

            for ( int i = 1; i <= batchSize && q.Count > 0; i++ )
            {
                string sn = q.Dequeue();
                if ( sn == null || sn.Length == 0 ) // not sure if we have to worry about this, but ya never know.
                    continue;
                snList.Add( sn );
            }
            return snList;
        }

        /// <summary>
        /// Removes unused space from the iNet database.
        /// </summary>
        private void ShrinkInetDatabase()
        {
            long totalSize = DataAccess.DataAccess.GetTotalSize( DataAccess.DataAccess.DataSource.iNetData );
            Log.Debug( string.Format( "{0}: Total size of iNet database is {1} bytes", Name, totalSize ) );

            long freeSize = DataAccess.DataAccess.GetFreeSize( DataAccess.DataAccess.DataSource.iNetData );
            int percent = (int)( (float)freeSize / (float)totalSize * 100.0f );
            Log.Debug( string.Format( "{0}: Unused size of iNet database is {1} bytes ({2}%%)", Name, freeSize, percent ) );

            // Only compact if more that 15% of the database is unused.
            // Not really sure what percentage we should use. 10% seemed to small.  20% seemed to large.  
            if ( percent > 15 )
            {
                Log.Debug( string.Format( "{0}: Unused space in iNet exceeds threshold.  COMPACTING.", Name ) );
                DataAccess.DataAccess.DatabaseCompact( DataAccess.DataAccess.DataSource.iNetData );
                Log.Debug( string.Format( "{0}: Compaction complete.", Name ) );
            }
        }

        /// <summary>
        /// Removes unused space from the Queue database.
        /// </summary>
        private void ShrinkQueueDatabase()
        {
            const long compactionThreshold = 1000000; // bytes

            PersistedQueue queue = PersistedQueue.CreateInetInstance();

            // We only compact the queue database when it's totally empty.
            long count = queue.GetCount();
            if ( count > 0 )
            {
                Log.Debug( string.Format( "{0}: Queue database will not be compacted because it is non-empty.", Name ) );
                return;
            }

            // Get the total size of the database.
            long queueDatabaseSize = DataAccess.DataAccess.GetTotalSize( DataAccess.DataAccess.DataSource.iNetQueue );

            Log.Debug( string.Format( "{0}: Queue database is {1} bytes", Name, queueDatabaseSize ) );
            Log.Debug( string.Format( "{0}: Compaction threshold is {1} bytes", Name, compactionThreshold ) );

            if ( queueDatabaseSize <= compactionThreshold )
            {
                Log.Debug( string.Format( "{0}: Queue database not compacted.  It does not exceed threshold.", Name ) );
                return;
            }

            Log.Debug( string.Format( "{0}: Queue database size exceeds threshold. COMPACTING.", Name ) );

            // NOTE: To compact, we do a "ResetInetQueue which replaces the current
            // database with a new empty database.
            // WE DO THIS INSTEAD OF USING SQLITE'S "VACUUM' COMMAND (i.e. DataAccess.DatabaseCompact)
            // BECAUSE THE QUEUE DATABASE FILE CAN GROW QUITE LARGE, AND VACUUM TAKES A 
            // VERY LONG TIME ON LARGE FILES.

            // The intent of pausing the Reporter service is to stop it from trying to read from the queue database while
            // we're in the middle of deleing it. inet uploads from being queued or deqeueued.
            // Note that pausing the ReporterService has a side effect of also 'pausing' the upload queue.
            Log.Warning( string.Format( "{0}Pausing {1}", Name, Master.Instance.ReporterService.Name ) );
            Master.Instance.ReporterService.Paused = true;

            // Although we've paused the reporter service, it may have been in the middle of a web service call
            // when paused so it doesn't know about it's changed status yet.
            for ( int i = 180; i > 0; i-- )
            {
                Log.Warning( string.Format( "{0}Waiting for {1} to stop. (tries left: {2})...", Name, Master.Instance.ReporterService.Name, i ) );
                Thread.Sleep( 1000 );

                if ( !Master.Instance.ReporterService.Running() )
                {
                    Log.Warning( string.Format( "{0}{1} appears to be successfully paused.", Name, Master.Instance.ReporterService.Name ) );
                    break;
                }
            }

            if ( Master.Instance.ReporterService.Running() ) // still not stopped?
            {
                Log.Warning( string.Format( "{0}{1} unsuccessfully stopped!", Name, Master.Instance.ReporterService.Name ) );
                return;
            }

            DataAccess.DataAccess.ResetInetQueue();

            Log.Warning( string.Format( "{0}Unpausing{1}", Name, Master.Instance.ReporterService.Name ) );
            Master.Instance.ReporterService.Paused = false;

            Log.Debug( string.Format( "{0}: Compaction complete.", Name ) );
        }

        #endregion
    }
}
