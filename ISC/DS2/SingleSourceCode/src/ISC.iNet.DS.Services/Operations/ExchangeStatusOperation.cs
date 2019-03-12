using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.iNet;
using ISC.WinCE;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    using ISC.iNet.DS.DomainModel; // puting this here avoids compiler's confusion of DomainModel.Instrument vs Instrument.Driver.

    public class ExchangeStatusOperation : ExchangeStatusAction, IOperation
    {
        private InetStatus _inetStatus;
        private ExchangeStatusEvent _statusEvent;

        private Schema _schema;

        string _dockingStationStatus;
        bool _returnVersions;

        /// <summary>
        /// The currently docked instrument's serial number, else empty.
        /// </summary>
        private string _instrumentSn;

		/// <summary>
		/// The currently docked instrument's device type, else Unknown.
		/// </summary>
		private DeviceType _instrumentType;

        public ExchangeStatusOperation( ExchangeStatusAction exchangeStatusActiom )
            : base( exchangeStatusActiom )
        {
            _dockingStationStatus = string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dockingStationStatus"></param>
        /// <param name="returnVersions">
        /// If true, then Execute() calls the web service to be called,
        /// and the 'version dates' are immediately returned.  The operation will not bother
        /// downloading any other data.)
        /// </param>
        public ExchangeStatusOperation( string dockingStationStatus, bool returnVersions )
            : base()
        {
            _returnVersions = returnVersions;
            _dockingStationStatus = dockingStationStatus == null ? string.Empty : dockingStationStatus;
        }

        private void DisposeOfInetDownloader( InetDownloader inetDownloader )
        {
            try
            {
                inetDownloader.Dispose();
            }
            catch ( Exception ex )
            {
                Log.Warning( "Error disposing InetDownloader", ex );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>ExchangeStatusEvent. Check ExchangeStatusEvent.InetStatus.Error for success/fail.</returns>
        public DockingStationEvent Execute()
        {
            Stopwatch stopwatch = Log.TimingBegin( "EXCHANGE STATUS OPERATION" );

            _schema = (Schema)Configuration.Schema.Clone();

            _instrumentSn = Master.Instance.SwitchService.Instrument.SerialNumber;
			_instrumentType = Master.Instance.SwitchService.Instrument.Type;

            /// INS-2047 ("Need to upload "Next cal date" / "Next bump date")...
            DateTime? nextCalDate = ( _instrumentSn == string.Empty ) ? null : Master.Instance.SwitchService.NextUtcCalibrationDate;
            DateTime? nextBumpDate = ( _instrumentSn == string.Empty ) ? null : Master.Instance.SwitchService.NextUtcBumpDate;

            // Instantiate a single Downloader instance and re-use it for the duration
            // of this call. This allows the underlying socket connection to be re-used.
            // (Opening the initial socket and connecting has proven sometimes be an expensive operation.
            InetDownloader inetDownloader = new InetDownloader();

            try
            {
                // ExchangeStatus guarantees that it will return an InetStatus.
                _inetStatus = inetDownloader.ExchangeStatus( _dockingStationStatus, _instrumentSn, nextCalDate, nextBumpDate, _returnVersions, true );

                _statusEvent = new ExchangeStatusEvent( this, _inetStatus );

                if ( _inetStatus.Error != string.Empty )
                {
                    Log.Warning( "Unable to connect to iNet to exchange status" );
                    Log.Warning( _inetStatus.Error );
                    Log.TimingEnd( "EXCHANGE STATUS OPERATION", stopwatch );
                    return _statusEvent;
                }

                if ( !_returnVersions )
                {
                    Log.TimingEnd( "EXCHANGE STATUS OPERATION", stopwatch );
                    return _statusEvent;
                }

                UpdateSystemTime();

                try
                {
                    UpdateAccount();

					// If the account changes, it could take a long time to download all the new settings.
					if ( _statusEvent.AccountModified )
					{
						Master.Instance.ConsoleService.UpdateState( ConsoleState.Synchronization );
					}

                    // If the account changed, then we need to re-instantiate
                    // our InetDownloader so that it gets a fresh copy of the modified Schema.
                    if ( _statusEvent.AccountModified || _statusEvent.ActivationModified || _statusEvent.IsManufacturingModified || _statusEvent.ServiceCodeModified )
                    {
                        DisposeOfInetDownloader( inetDownloader );

                        inetDownloader = new InetDownloader( Configuration.DockingStation, _schema );

                        //Suresh 13-APR-2012 INS-4519 (DEV JIRA)
                        //Even though we do below code in the finally block we need to do it here as well, otherwise Switch Service will not be intimated about
                        //schema version is empty. After this code we will be synchronizing data with iNet and that makes schema version to be up-to-date with iNet.
                        //If we not not updating current schema object with Configuration Schema object then docking station console will not display "synchronizing" 
                        //when account is changed.

                        Configuration.Schema = _schema;
                    }

                    UpdateFactoryCylinders( inetDownloader );  // We can update cylinders whenther activated or not activated.

                    // iNet will only let us update schedules, journals, settings when we're activated.
                    if ( _schema.Activated )
                    {
                        UpdateSchedules( inetDownloader );

                        UpdateEventJournals( inetDownloader );

                        UpdateSettings( inetDownloader );

                        UpdateCriticalErrors( inetDownloader );
                    }
                }
                finally
                {
                    // Before leaving, make sure we set our local schema (which we likely modified)
                    // to the global instance it was originally cloned from.
                    Configuration.Schema = _schema;
                }
            }
            finally
            {
                DisposeOfInetDownloader( inetDownloader );
            }

            // Hand over all 'forced' schedules returned by iNet to the Scheduler.
            foreach ( ScheduledNow scheduledNow in _statusEvent.ScheduledNowList )
            {
                Log.Debug( string.Format( "{0}.ExchangeInetStatus: Queueing \"{0}\" to Scheduler", scheduledNow ) );
                Master.Instance.Scheduler.QueueForcedSchedule( scheduledNow );
            }

            List<EventCode> todoEventCodes = new List<EventCode>();

            // Whenever the account changes, we need to make sure a re-read is performed.
            // We also need to do a re-read whenever cylinders change to re-validate the currently
            // attached cylinders against the uploaded list of cylinders.
            if ( _statusEvent.AccountModified || _statusEvent.CylindersModified || _statusEvent.ManualsModified || _statusEvent.ReplacedEquipmentModified )
            {
                Log.Debug( string.Format( "{0}.ExchangeInetStatus: Account, Cylinders, Manifolds, Manually-assigned Cylinders or Replaced Equipment modified.", Name ) );
                todoEventCodes.Add( EventCode.GetCachedCode( EventCode.SettingsRead ) );

				// if replaced equipment was modified, the list in memory needs to be cleared; 
				// the list will be reloaded by the SettingsReadOperation
				if ( _statusEvent.ReplacedEquipmentModified )
				{
					Master.Instance.ExecuterService.ReplacedEquipment.Clear();
				}
            }

            // Whenever the docking station settings change, we want to 
            // immediately get them applied.  So we need to make sure the Update event is performed.
			// Also, if the account or activation status changes we need to have a Settings Update occur to pick up the 
			// changes to LogToSerialPort and LogToFile.
            if ( _statusEvent.DockingStationModified || _statusEvent.AccountModified || _statusEvent.ActivationModified )
            {
				Log.Debug( string.Format( "{0}.ExchangeInetStatus: Docking Station Settings, Account, or Activation modified.", Name ) );
                todoEventCodes.Add( EventCode.GetCachedCode( EventCode.SettingsUpdate ) );
            }

            if ( _statusEvent.InstrumentSettingsModified )
            {
                Log.Debug( string.Format( "{0}.ExchangeInetStatus: Instrument Settings modified.", Name ) );
                todoEventCodes.Add( EventCode.GetCachedCode( EventCode.InstrumentSettingsUpdate ) );
            }

            // No need to start a database transaction if we have nothing to do.
            if ( todoEventCodes.Count == 0 )
            {
                Log.TimingEnd( "EXCHANGE STATUS OPERATION", stopwatch );
                return _statusEvent;
            }

            EventJournalDataAccess eventCodeDataAccess = new EventJournalDataAccess();
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                foreach ( EventCode eventCode in todoEventCodes )
                {
                    // For all VDS events, delete any pre-existing event journals
                    // for this docking station.
                    if ( eventCode.EquipmentTypeCode == EquipmentTypeCode.VDS )
                    {
                        string sn = Configuration.DockingStation.SerialNumber;
                        if ( sn == string.Empty ) continue; // will/should probably never happen?

                        Log.Debug( string.Format( "{0}.ExchangeInetStatus: Deleting {1} for S/N {2}", Name, eventCode, sn ) );
                        eventCodeDataAccess.DeleteBySerialNumbers( new string[] { sn }, eventCode, trx );
                    }
                    // For all Instrument events, delete any pre-existing event journals
                    // for the docked instrument
                    // TODO - sensors?
                    else if ( eventCode.EquipmentTypeCode == EquipmentTypeCode.Instrument && _instrumentSn != string.Empty )
                    {
                        Log.Debug( string.Format( "{0}.ExchangeInetStatus: Deleting {1} for S/N {2}", Name, eventCode, _instrumentSn ) );
                        eventCodeDataAccess.DeleteBySerialNumbers( new string[] { _instrumentSn }, eventCode, trx );
                    }
                }
                trx.Commit();
            }

            Log.TimingEnd( "EXCHANGE STATUS OPERATION", stopwatch );
            return _statusEvent;
        }

        /// <summary>
        /// Update the RTC with the current time returned by iNet.
        /// </summary>
        private void UpdateSystemTime()
        {
            if ( _inetStatus.CurrentTime != DomainModelConstant.NullDateTime )
            {
				// If we are in cal station mode, we want to be able to set the system clock through 
				// the web configurator without it being overridden by the time provided by iNet.
                // This is important because if not activated, then don't know the local time zone.
				// However, if the docking station is in service mode (like it would be at manufacture
				// time), we want the system clock to be initialized from iNet for new docking stations.  
                if ( !Configuration.Schema.Activated && !Configuration.ServiceMode )
					return;

                Log.Trace( Log.Dashes );

                // We assign these times to local variables so they can be examined in the debugger.

                DateTime systemUtcTime = SystemTime.DateTimeUtcNow;

                TimeSpan difference = _inetStatus.CurrentTime - systemUtcTime;

                if ( Log.Level >= LogLevel.Trace )
                {
                    Log.Trace( "Current iNet time         = " + Log.DateTimeToString( _inetStatus.CurrentTime ) );
                    Log.Trace( "Current SystemTime.UtcNow = " + Log.DateTimeToString( systemUtcTime ) );
                    // Note that we due a Log.Debug for the local time since we always want
                    // to continually log it.
                    Log.Trace( "Current Local time        = " + Log.DateTimeToString( Configuration.GetLocalTime() ) );
                    Log.Trace( "Time difference (seconds) = " + difference.TotalSeconds );
                }

                if ( Math.Abs( difference.TotalSeconds ) >= 20.0d ) // Allow a 20 second tolerance.
                {
                    Log.Debug( Log.Dashes );
                    Log.Debug( string.Format( "System time ({0}) is out of sync by {1}s.", Log.DateTimeToString( systemUtcTime ), (int)difference.TotalSeconds ) );
                    Log.Debug( string.Format( "Correcting system time to {0}", Log.DateTimeToString( _inetStatus.CurrentTime ) ) );
                    Log.Debug( Log.Dashes );

                    SystemTime.SetSystemTime( _inetStatus.CurrentTime );

                    systemUtcTime = SystemTime.DateTimeUtcNow;

                    Log.Debug( Log.Dashes );
                    Log.Debug( "    New system time: " + Log.DateTimeToString( systemUtcTime ) );
                    Log.Debug( "    New local time:  " + Log.DateTimeToString( Configuration.GetLocalTime() ) );

                    _statusEvent.SytemTimeModified = true;
                }

                Log.Trace( Log.Dashes );
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="inetStatus"></param>
        private void UpdateAccount()
        {
            const string funcName = "UpdateAccount: ";

            if ( _schema.AccountNum != _inetStatus.Schema.AccountNum )
            {
                Log.Info( string.Format( "{0}New Account Number (\"{1}\") received from iNet.", funcName, _inetStatus.Schema.AccountNum ) );
                _statusEvent.AccountModified = true;
            }

            if ( _schema.Activated != _inetStatus.Schema.Activated )
            {
                Log.Info( string.Format( "{0}New Activation flag (\"{1}\") received from iNet.", funcName, _inetStatus.Schema.Activated ) );
                _statusEvent.ActivationModified = true;
            }

            if ( _schema.IsManufacturing != _inetStatus.Schema.IsManufacturing )
            {
                Log.Info( string.Format( "{0}New IsManufacturing flag (\"{1}\") received from iNet.", funcName, _inetStatus.Schema.IsManufacturing ) );
                _statusEvent.IsManufacturingModified = true;
            }

            if ( _schema.ServiceCode != _inetStatus.Schema.ServiceCode )
            {
                Log.Info( string.Format( "{0}New ServiceCode (\"{1}\") received from iNet.", funcName, _inetStatus.Schema.ServiceCode ) );
                _statusEvent.ServiceCodeModified = true;
            }

            // Whenever the account number is changed, we need to replace the old 
            // account's  database with a new blank database.
            if ( _statusEvent.AccountModified )
            {
                //Master.Instance.ConsoleService.SetActionState( /*ConsoleServiceResources.*/"UPDATE_ACCOUNT" );

                Log.Info( string.Format( "{0}Resetting iNet database due to account number change ({1}).", funcName, _inetStatus.Schema.AccountNum ) );
                // The new blank database's schema will be given the accountnum and active flag
                // passed in here...
                _schema = DS.DataAccess.DataAccess.ResetInet( _inetStatus.Schema.AccountNum, _inetStatus.Schema.Activated, _inetStatus.Schema.IsManufacturing, _inetStatus.Schema.ServiceCode );
				Configuration.ResetInet(); // Purges log file messages to protect the privacy of the prior account and disables LogToFile
            }
            // Whenever account activation is changed, we replace the database with a new blank database..
            // If account was inactive, but is not activated, then data it had while inactive may have become 'stale'
            // over time.  Best way to get latest&greatest is to just reset and then redownload everything.
            else if ( _statusEvent.ActivationModified )
            {
                Log.Info( string.Format( "{0}Resetting iNet database due to Activation status change ({1}).", funcName, _inetStatus.Schema.Activated ) );
                _schema = DS.DataAccess.DataAccess.ResetInet( _inetStatus.Schema.AccountNum, _inetStatus.Schema.Activated, _inetStatus.Schema.IsManufacturing, _inetStatus.Schema.ServiceCode );
				Configuration.ResetInet(); // Purges log file messages and disables LogToFile
            }
            // If account type is changed to manufacturing or service code changed, then update the schema to reflect that.
            else if ( _statusEvent.IsManufacturingModified  || _statusEvent.ServiceCodeModified )
            {
                if ( _statusEvent.IsManufacturingModified )
                {
                    Log.Info( string.Format( "{0}Updating schema; setting IsManufacturing to {1}", funcName, _inetStatus.Schema.IsManufacturing ) );
                    if ( new DS.DataAccess.SchemaDataAccess().UpdateIsManufacturing( _inetStatus.Schema.IsManufacturing ) )
                        _schema.IsManufacturing = _inetStatus.Schema.IsManufacturing;
                }
                if ( _statusEvent.ServiceCodeModified )
                {
                    Log.Info( string.Format( "{0}Updating schema; setting ServiceCode to {1}", funcName, _inetStatus.Schema.ServiceCode ) );
                    if ( new DS.DataAccess.SchemaDataAccess().UpdateServiceCode( _inetStatus.Schema.ServiceCode ) )
                        _schema.ServiceCode = _inetStatus.Schema.ServiceCode;
                }
            }
        }

        /// <summary>
        /// </summary>
        private void UpdateFactoryCylinders( InetDownloader inetDownloader )
        {
            const string funcName = "UpdateFactoryCylinders: ";

            if ( _schema.CylindersVersion == _inetStatus.Schema.CylindersVersion )
            {
                Log.Trace( string.Format( string.Format( "{0} inet={1}, schema={2}, so doing nothing.",
                    funcName, Log.DateTimeToString( _inetStatus.Schema.CylindersVersion ), Log.DateTimeToString( _schema.CylindersVersion ) ) ) );
                return;
            }

            List<FactoryCylinder> updates = new List<FactoryCylinder>();
            List<FactoryCylinder> deletes = new List<FactoryCylinder>();

            Log.Debug( string.Format( "{0}iNetVersion=\"{1}\", schemaVersion=\"{2}\"", funcName,
                Log.DateTimeToString( _inetStatus.Schema.CylindersVersion ), Log.DateTimeToString( _schema.CylindersVersion ) ) );

            DateTime? cylindersVersion = inetDownloader.DownloadCylinders( updates, deletes, _schema.CylindersVersion, _statusEvent.Errors );

            // Nothing to do? then return now instead of starting a transaction for nothing
            if ( cylindersVersion == null )
            {
                Log.Error( string.Format( "{0}null CylindersVersion returned, so doing nothing.", funcName ) );
                return;
            }

            Log.Debug( string.Format( "{0}{1} new/updated cylinders returned by iNet", funcName, updates.Count ) );
            Log.Debug( string.Format( "{0}{1} deleted cylinders returned by iNet", funcName, deletes.Count ) );
            Log.Debug( string.Format( "{0}cylindersVersion={1}", funcName, Log.DateTimeToString( cylindersVersion ) ) );

            if ( _schema.CylindersVersion == cylindersVersion && deletes.Count == 0 && updates.Count == 0 )
            {
                Log.Debug( string.Format( string.Format( "{0}No changes returned, so doing nothing.", funcName ) ) );
                return;
            }

            FactoryCylinderDataAccess factoryCylinderDataAccess = new FactoryCylinderDataAccess();

            //Master.Instance.ConsoleService.SetActionState( /*ConsoleServiceResources.*/"UPDATE_CYLINDERS" );

            Log.Debug( string.Format( "{0}Updating database", funcName ) );
            Stopwatch s = new Stopwatch(); s.Start();

            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                // do deletions first, otherwise we may end up with duplicate errors when trying to save the created/updated.
                foreach ( FactoryCylinder cylinder in deletes )
                {
                    Log.Trace( "Deleting cylinder " + cylinder.PartNumber );
                    factoryCylinderDataAccess.Delete( cylinder, trx );
                }

                foreach ( FactoryCylinder cylinder in updates )
                {
                    Log.Trace( "Updating cylinder " + cylinder.PartNumber );
                    factoryCylinderDataAccess.Save( cylinder, trx );
                }


                // Don't need to update the version unless it actually changes.
                if ( cylindersVersion != _schema.CylindersVersion )
                    new SchemaDataAccess().UpdateCylindersVersion( cylindersVersion, trx );
                // If the version returned by the exchangestatus call is ever earlier, then
                // somebody probably did a manual change in the database which they shouldn't
                // have done.  When this happens, then the download call will keep returning
                // nothing, and also echoing back the schema version that we passed to it.
                // So we get into a cycle of exchangeStatus continually giving us back a version
                // that differs from our schema (making us think that something is new), but
                // the download call not actually ever giving us anything.  To avoid this, we
                // update our schema with the version returned by exchange status if it's ever
                // earlier than what we have stored.
                else if ( _inetStatus.Schema.CylindersVersion < _schema.CylindersVersion )
                    new SchemaDataAccess().UpdateCylindersVersion( _inetStatus.Schema.CylindersVersion, trx );

                _statusEvent.Errors.AddRange( trx.Errors );

                trx.Commit();

                _schema.CylindersVersion = cylindersVersion;

                _statusEvent.CylindersModified = true;
            }

            Log.Debug( string.Format( "{0}Update complete in {1} seconds", funcName, s.ElapsedMilliseconds / 1000.0 ) );
        }

        /// <summary>
        /// </summary>
        private void UpdateSchedules( InetDownloader inetDownloader )
        {
            const string funcName = "UpdateSchedules: ";

            if ( _schema.SchedulesVersion == _inetStatus.Schema.SchedulesVersion )
            {
                Log.Trace( string.Format( string.Format( "{0} inet={1}, schema={2}, so doing nothing.",
                    funcName, Log.DateTimeToString( _inetStatus.Schema.SchedulesVersion ), Log.DateTimeToString( _schema.SchedulesVersion ) ) ) );
                return;
            }

            Log.Debug( string.Format( "{0}iNetVersion=\"{1}\", schemaVersion=\"{2}\"", funcName,
                Log.DateTimeToString( _inetStatus.Schema.SchedulesVersion ), Log.DateTimeToString( _schema.SchedulesVersion ) ) );

            List<Schedule> createdList = new List<Schedule>();
            List<Schedule> updatedList = new List<Schedule>();
            List<long> deletedList = new List<long>();

            DateTime? schedulesVersion = inetDownloader.DownloadSchedules( createdList, updatedList, deletedList, _instrumentSn, _schema.SchedulesVersion, _statusEvent.Errors, Configuration.DockingStation.TimeZoneInfo );

            if ( schedulesVersion == null )
            {
                Log.Error( string.Format( string.Format( "{0}null SchedulesVersion returned, so doing nothing.", funcName ) ) );
                return;
            }

            Log.Debug( string.Format( "{0}{1} new schedules returned by iNet", funcName, createdList.Count ) );
            Log.Debug( string.Format( "{0}{1} updated schedules returned by iNet", funcName, updatedList.Count ) );
            Log.Debug( string.Format( "{0}{1} deleted schedules returned by iNet", funcName, deletedList.Count ) );
            Log.Debug( string.Format( "{0}schedulesVersion={1}", funcName, Log.DateTimeToString( schedulesVersion ) ) );

            // "Upon Docking" schedules are only valid for instruments.  If the server 
            // returns us any that are not for instruments, then toss them away.
            //int removedCount = createdList.RemoveAll( s => s is ScheduledUponDocking && s.EquipmentTypeCode != EquipmentTypeCode.Instrument );
            //if ( removedCount > 0 )
            //    Log.Warning( string.Format( "{0}{1} new ScheduledUponDockings ignored for non-instruments!", funcName, removedCount ) );

            // Pull out any ScheduledNows from the createdList. (ScheduledNows will only ever be in the
            // createdList.)  ScheduledNows aren't saved to the database and need to intead be returned
            // in the ExchangeStatusEvent.  At the same time we're pulling these out, keep track if we *only*
            // find ScheduledNows or if we find some other Schedule subclasses.  If the only thing we find
            // are ScheduleNows, then we don't need to bother starting a transaction since there's nothing
            // to save. 

            bool persistedFound = false;

            foreach ( Schedule sched in createdList )
            {
                if ( sched is ScheduledNow )
                {
                    Log.Debug( string.Format( "{0}{1}", funcName, sched.GetType() ) );
                    Log.Debug( string.Format( "{0}Returning RefId {1}", funcName, sched.RefId ) );
                    _statusEvent.ScheduledNowList.Add( (ScheduledNow)sched );
                }
                else
                    persistedFound = true;
            }

            persistedFound = persistedFound || updatedList.Count > 0 || deletedList.Count > 0;

            // Nothing to do? then return now instead of starting a transaction for nothing
            if ( ( _schema.SchedulesVersion == schedulesVersion ) && !persistedFound )
            {
                Log.Debug( string.Format( string.Format( "{0}No changes returned, so doing nothing.", funcName ) ) );
                return;
            }

            //Master.Instance.ConsoleService.SetActionState( /*ConsoleServiceResources.*/"UPDATE_SCHEDULES" );

            ScheduleDataAccess scheduleDataAccess = new ScheduleDataAccess();
            ScheduledUponDockingDataAccess uponDockingDataAccess = new ScheduledUponDockingDataAccess();
            ScheduledOnceDataAccess onceDataAccess = new ScheduledOnceDataAccess();
            ScheduledHourlyDataAccess hourlyDataAccess = new ScheduledHourlyDataAccess();
            ScheduledDailyDataAccess dailyDataAccess = new ScheduledDailyDataAccess();
            ScheduledWeeklyDataAccess weeklyDataAccess = new ScheduledWeeklyDataAccess();
            ScheduledMonthlyDataAccess monthlyDataAccess = new ScheduledMonthlyDataAccess();

            Log.Debug( string.Format( "{0}Updating database", funcName ) );
            Stopwatch s = new Stopwatch(); s.Start();

            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                ScheduleDataAccess dataAccess = null;

                // do deletions first, otherwise we may end up with duplicate errors when trying to save the created/updated.
                foreach ( long refId in deletedList )
                {
                    if ( refId == DomainModelConstant.NullId ) // if internally forced by the VDS
                        continue;

                    Log.Debug( string.Format( "{0}Deleting RefId {1}", funcName, refId ) );

                    // The deletion is a cascading delete.
                    bool deleted = scheduleDataAccess.DeleteByRefId( refId, trx );

                    Log.Trace( string.Format( "{0}Deleted={1}", funcName, deleted ) );
                }

                foreach ( Schedule sched in createdList )
                {
                    if ( sched.RefId == DomainModelConstant.NullId ) // if internally forced by the VDS
                        continue;

                    if ( sched is ScheduledUponDocking ) dataAccess = uponDockingDataAccess;
                    else if ( sched is ScheduledOnce ) dataAccess = onceDataAccess;
                    else if ( sched is ScheduledHourly ) dataAccess = hourlyDataAccess;
                    else if ( sched is ScheduledDaily ) dataAccess = dailyDataAccess;
                    else if ( sched is ScheduledWeekly ) dataAccess = weeklyDataAccess;
                    else if ( sched is ScheduledMonthly ) dataAccess = monthlyDataAccess;
                    else if ( sched is ScheduledNow ) continue;  // we don't persist forced schedules.
                    else throw new ArgumentException( "Unknown schedule class: " + sched.GetType().ToString() ); // this should never happen.

                    Log.Debug( string.Format( "{0}{1}", funcName, sched.GetType() ) );
                    Log.Debug( string.Format( "{0}Inserting RefId {1}, {2}", funcName, sched.RefId, sched ) );

                    bool inserted = dataAccess.Insert(sched, trx);
                    Log.Trace( string.Format( "{0}Inserted={1}", funcName, inserted ) );
                }

                foreach ( Schedule sched in updatedList )
                {

                    // Before doing the update, delete the current version of it, then
                    // re-insert it.
                    //
                    // Actually, we don't really need to worry about what table it's in
                    // (ScheduledDaily versus ScheduledOnce, etc.) since the deletion
                    // is a cascading delete.  But we just follow the pattern that's
                    // used for inserts and creates.

                    if ( sched.RefId == DomainModelConstant.NullId ) // if internally forced by the VDS
                        continue;

                    Log.Debug( string.Format( "{0}Deleting RefId {1} for Update, {2}", funcName, sched.RefId, sched ) );

                    bool deleted = uponDockingDataAccess.DeleteByRefId( sched.RefId, trx )
                    || hourlyDataAccess.DeleteByRefId( sched.RefId, trx )
                    || dailyDataAccess.DeleteByRefId( sched.RefId, trx )
                    || weeklyDataAccess.DeleteByRefId( sched.RefId, trx )
                    || monthlyDataAccess.DeleteByRefId( sched.RefId, trx )
                    || onceDataAccess.DeleteByRefId( sched.RefId, trx );

                    Log.Trace( string.Format( "{0}Deleted={1}", funcName, deleted ) );

                    if ( sched is ScheduledUponDocking ) dataAccess = uponDockingDataAccess;
                    else if ( sched is ScheduledOnce ) dataAccess = onceDataAccess;
                    else if ( sched is ScheduledHourly ) dataAccess = hourlyDataAccess;
                    else if ( sched is ScheduledDaily ) dataAccess = dailyDataAccess;
                    else if ( sched is ScheduledWeekly ) dataAccess = weeklyDataAccess;
                    else if ( sched is ScheduledMonthly ) dataAccess = monthlyDataAccess;
                    else if ( sched is ScheduledNow ) continue;  // we don't persist forced schedules.
                    else throw new ArgumentException( string.Format( "{0}Unknown schedule class: {1}", funcName, sched.GetType().ToString() ) ); // this should never happen.

                    Log.Debug( string.Format( "{0}{1}", funcName, sched.GetType() ) );
                    Log.Debug( string.Format( "{0}Inserting RefId {1} for update, {2}", funcName, sched.RefId, sched ) );

                    bool inserted = dataAccess.Insert( sched, trx );

                    Log.Trace( string.Format( "{0}Inserted={1}", funcName, inserted ) );
                }

                DateTime? schemaSchedulesVersion = _schema.SchedulesVersion;

                // Don't need to update the version unless it actually changes.

                if ( schedulesVersion != _schema.SchedulesVersion )
                {
                    new SchemaDataAccess().UpdateSchedulesVersion( schedulesVersion, trx );
                    schemaSchedulesVersion = schedulesVersion;
                }
                // If the version returned by the exchangestatus call is ever earlier, then
                // somebody probably did a manual change in the database which they shouldn't
                // have done.  When this happens, then the download call will keep returning
                // nothing, and also echoing back the schema version that we passed to it.
                // So we get into a cycle of exchangeStatus continually giving us back a version
                // that differs from our schema (making us think that something is new), but
                // the download call not actually ever giving us anything.  To avoid this, we
                // update our schema with the version returned by exchange status if it's ever
                // earlier than what we have stored.
                else if ( _inetStatus.Schema.SchedulesVersion < _schema.SchedulesVersion )
                {
                    new SchemaDataAccess().UpdateSchedulesVersion( _inetStatus.Schema.SchedulesVersion, trx );
                    schemaSchedulesVersion = _inetStatus.Schema.SchedulesVersion;
                }
                _statusEvent.Errors.AddRange( trx.Errors );

                trx.Commit();

                // We dont' update the memory-resident schema until we know we've safetely committed.
                // If chose to no update it just above, then it will be what it was originally.
                _schema.SchedulesVersion = schemaSchedulesVersion;

                _statusEvent.SchedulesModified = true;

            } // end-using

            Log.Debug( string.Format( "{0}Update complete in {1} seconds", funcName, s.ElapsedMilliseconds / 1000.0 ) );
        }       

        /// <summary>
        /// </summary>
        private void UpdateEventJournals( InetDownloader inetDownloader )
        {
            const string funcName = "UpdateEventJournals: ";

            if ( _schema.EventJournalsVersion == _inetStatus.Schema.EventJournalsVersion )
            {
                Log.Trace( string.Format( string.Format( "{0} inet={1}, schema={2}, so doing nothing.",
                    funcName, Log.DateTimeToString( _inetStatus.Schema.EventJournalsVersion ), Log.DateTimeToString( _schema.EventJournalsVersion ) ) ) );
                return;
            }

            Log.Debug( string.Format( "{0}iNetVersion=\"{1}\", schemaVersion=\"{2}\"", funcName,
                Log.DateTimeToString( _inetStatus.Schema.EventJournalsVersion ), Log.DateTimeToString( _schema.EventJournalsVersion ) ) );

            List<EventJournal> journalList = new List<EventJournal>();

            DateTime? eventJournalsVersion = inetDownloader.DownloadEventJournals( journalList, _schema.EventJournalsVersion, _statusEvent.Errors );

            if ( eventJournalsVersion == null )
            {
                Log.Error( string.Format( string.Format( "{0}null EventJournalsVersion returned, so doing nothing.", funcName ) ) );
                return;
            }

            Log.Debug( string.Format( "{0}{1} event journals returned by iNet", funcName, journalList.Count ) );
            Log.Debug( string.Format( "{0}eventJournalsVersion={1}", funcName, Log.DateTimeToString( eventJournalsVersion ) ) );

            // Nothing to do? then return now instead of starting a transaction for nothing
            if ( _schema.EventJournalsVersion == eventJournalsVersion && journalList.Count == 0 )
            {
                Log.Debug( string.Format( string.Format( "{0}No changes returned, so doing nothing.", funcName ) ) );
                return;
            }

            //Master.Instance.ConsoleService.SetActionState( /*ConsoleServiceResources.*/"UPDATE_JOURNALS" );

            Log.Debug( string.Format( "{0}Updating database", funcName ) );

            EventJournalDataAccess dataAccess = new EventJournalDataAccess();

            Stopwatch s = new Stopwatch(); s.Start();

            // We pass in an Insert hint if we see a null version date (which we assume means the data is all new).
            using ( DataAccessTransaction trx = new DataAccessTransaction( _schema.EventJournalsVersion == null ? DataAccessHint.Insert : DataAccessHint.None ) )
            {
                foreach ( EventJournal eventJournal in journalList )
                {
                    if ( Log.Level >= LogLevel.Trace ) // don't waste time formatting unless we have to.
                        Log.Trace( string.Format( "{0}Saving {1}", funcName, eventJournal ) );

                    if ( !dataAccess.Save( eventJournal, trx ) )
                    {
                        string msg = string.Format( "{0} Failed to save \"{1}\"", funcName, eventJournal );
                        Log.Warning( msg );

                        // SGF  05-Apr-2011  INS-1746 -- commenting out this statement as per Jon Pearsall's comment on 24-Mar-2011
                        //_statusEvent.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                    }
                }


                DateTime? schemaJournalsVersion = _schema.EventJournalsVersion;

                // Don't need to update the version unless it actually changes.
                if ( eventJournalsVersion != _schema.EventJournalsVersion )
                {
                    new SchemaDataAccess().UpdateEventJournalsVersion( eventJournalsVersion, trx );
                    schemaJournalsVersion = eventJournalsVersion;
                }
                // If the version returned by the exchangestatus call is ever earlier, then
                // somebody probably did a manual change in the database which they shouldn't
                // have done.  When this happens, then the download call will keep returning
                // nothing, and also echoing back the schema version that we passed to it.
                // So we get into a cycle of exchangeStatus continually giving us back a version
                // that differs from our schema (making us think that something is new), but
                // the download call not actually ever giving us anything.  To avoid this, we
                // update our schema with the version returned by exchange status if it's ever
                // earlier than what we have stored.
                else if ( _inetStatus.Schema.EventJournalsVersion < _schema.EventJournalsVersion )
                {
                    new SchemaDataAccess().UpdateEventJournalsVersion( _inetStatus.Schema.EventJournalsVersion, trx );
                    schemaJournalsVersion = _inetStatus.Schema.EventJournalsVersion;
                }

                _statusEvent.Errors.AddRange( trx.Errors );

                trx.Commit();

                // We dont' update the memory-resident schema until we know we've safetely committed.
                // If chose to no update it just above, then it will be what it was originally.
                _schema.EventJournalsVersion = schemaJournalsVersion;

                _statusEvent.EventJournalsModified = true;
            }

            Log.Debug( string.Format( "{0}Update complete in {1} seconds", funcName, s.ElapsedMilliseconds / 1000.0 ) );
        }


        private void UpdateSettings( InetDownloader inetDownloader )
        {
            const string funcName = "UpdateEquipmentSettings: ";

            if ( _schema.SettingsVersion == _inetStatus.Schema.SettingsVersion )
            {
                Log.Trace( string.Format( string.Format( "{0} inet={1}, schema={2}, so doing nothing.",
                    funcName, Log.DateTimeToString( _inetStatus.Schema.SettingsVersion ), Log.DateTimeToString( _schema.SettingsVersion ) ) ) );
                return;
            }

            Log.Debug( string.Format( "{0}iNetVersion=\"{1}\", schemaVersion=\"{2}\"", funcName,
                Log.DateTimeToString( _inetStatus.Schema.SettingsVersion ), Log.DateTimeToString( _schema.SettingsVersion ) ) );

            DockingStation dockingStation;
            List<string> deniedList = new List<string>();
            List<string> replacedList = new List<string>();

			List<InstrumentSettingsGroup> updatedDefaultSettingsGroups = new List<InstrumentSettingsGroup>();
            List<InstrumentSettingsGroup> updatedSettingsGroups = new List<InstrumentSettingsGroup>();
            List<long> deletedSettingsGroups = new List<long>();
            List<Instrument> updatedInstruments = new List<Instrument>();
            List<GasEndPoint> manifolds = new List<GasEndPoint>();
            List<GasEndPoint> manualCylinders = new List<GasEndPoint>();
            List<GasEndPoint> changedCylinders = new List<GasEndPoint>();
            List<SensorCalibrationLimits> sensorCalibrationLimits = new List<SensorCalibrationLimits>();

            DateTime? settingsVersion = inetDownloader.DownloadSettings( out dockingStation,
                manifolds, manualCylinders,
                deniedList, replacedList,
				updatedDefaultSettingsGroups,
				updatedSettingsGroups, deletedSettingsGroups,
                updatedInstruments, _schema.SettingsVersion, _statusEvent.Errors, sensorCalibrationLimits );

            // Nothing to do? then return now instead of starting any transactions for nothing
            if ( settingsVersion == null )
            {
                Log.Error( string.Format( string.Format( "{0}null SettingsVersion returned, so doing nothing.", funcName ) ) );
                return;
            }

            // Determine if any manifolds or manual cylinders have been added/removed
            changedCylinders = GetChangedManualCylinders( manifolds, manualCylinders );

            Log.Debug( string.Format( "{0}dockingStation={1}", funcName, ( dockingStation == null ) ? "No" : "Yes" ) );
#if DENIED
            Log.Debug( string.Format( "{0}deniedList={1}", funcName, deniedList.Count ) );
#endif
			Log.Debug( string.Format( "{0}replaced={1}", funcName, replacedList.Count ) );
			Log.Debug( string.Format( "{0}changedCylinders={1}", funcName, changedCylinders.Count ) );
			Log.Debug( string.Format( "{0}updatedDefaultSettingsGroups={1}", funcName, ( updatedDefaultSettingsGroups == null ) ? "No" : "Yes" ) );
			Log.Debug( string.Format( "{0}deletedSettingsGroups={1}", funcName, deletedSettingsGroups.Count ) );
			Log.Debug( string.Format( "{0}updatedSettingsGroups={1}", funcName, updatedSettingsGroups.Count ) );
            Log.Debug( string.Format( "{0}updatedInstruments={1}", funcName, updatedInstruments.Count ) );
            Log.Debug( string.Format( "{0}settingsVersion={1}", funcName, Log.DateTimeToString( settingsVersion ) ) );
            Log.Debug( string.Format( "{0}sensorCalibrationLimits={1}", funcName, sensorCalibrationLimits.Count ) );

#if DENIED
            DeniedInstrumentDataAccess deniedAccess = new DeniedInstrumentDataAccess();
#endif
            ReplacedEquipmentDataAccess replacedAccess = new ReplacedEquipmentDataAccess();

            // Get the currenly stored lists of denied and replaced instrument serial
            // serial numbers.  Compare them to the lists we just obtained from iNet
			// to determine if there are any differences or not.  
#if DENIED
            bool deniedDifferent = false;
#endif
			bool replacedDifferent = false;

            // Get current denied and replaced lists in a read-only transaction.
            // Then compare to what was just returned to see if anything is different.
            // We don't want to do this in the writable transaction farthur below because
            // we don't want to start that transaction unless we have to.

            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
#if DENIED
				deniedDifferent = CompareStringLists( deniedList, deniedAccess.FindAll( trx ) );
#endif
                replacedDifferent = CompareStringLists( replacedList, replacedAccess.FindAll( trx ) );
                _statusEvent.Errors.AddRange( trx.Errors ); // HOW WOULD ANY ERRORS EVER BE RECORDED ON THE TRANSACTION???
            }

            if ( _schema.SettingsVersion == settingsVersion
                && dockingStation == null
#if DENIED
                && deniedDifferent == false
#endif
				&& replacedDifferent == false 
				&& changedCylinders.Count == 0    
                && updatedDefaultSettingsGroups.Count == 0
				&& deletedSettingsGroups.Count == 0 
				&& updatedSettingsGroups.Count == 0
                && updatedInstruments.Count == 0 )
            {
                Log.Debug( string.Format( string.Format( "{0}No changes returned, so doing nothing.", funcName ) ) );
                return;
            }

            //Master.Instance.ConsoleService.SetActionState( /*ConsoleServiceResources.*/"UPDATE_SETTINGS" );

            Log.Debug( string.Format( "{0}Updating database", funcName ) );
            Stopwatch s = new Stopwatch(); s.Start();

            InstrumentDataAccess instDataAccess = new InstrumentDataAccess();

            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                if ( dockingStation != null )
                {
                    Log.Debug( string.Format( "{0}Saving docking station settings", funcName ) );
                    new DockingStationDataAccess().Save( dockingStation, trx );
                    _statusEvent.DockingStationModified = true;

                    //Suresh 12-SEPTEMBER-2011 INS-2248
                    if ( dockingStation.ReplacedDSNetworkSettings != null )
                    {
                        //insert new replaced DS network settings into database
                        Log.Debug( string.Format( "{0}Saving docking station replaced nework settings", funcName ) );
                        new ReplacedNetworkSettingsDataAccess().Save( dockingStation.ReplacedDSNetworkSettings, trx );
                    }
                }

                if ( changedCylinders.Count > 0 )
                {
                    Log.Debug( string.Format( "{0}Saving {1} manifold/manual cylinder settings", funcName, changedCylinders.Count ) );

                    SaveChangedManualCylinders( changedCylinders, trx );

                    _statusEvent.ManualsModified = true;
                }

#if DENIED
                if ( deniedDifferent ) // No need to delete then re-save if nothing has has changed.
                {
                    Log.Debug( string.Format( "{0}Saving {1} denied instruments", funcName, deniedList.Count ) );
                    deniedAccess.SaveAll( deniedList, trx );
                }
#endif
				if ( replacedDifferent ) // No need to delete then re-save if nothing has has changed.
                {
                    Log.Debug( string.Format( "{0}Saving {1} replaced equipment", funcName, replacedList.Count ) );
                    replacedAccess.SaveAll( replacedList, trx );

					_statusEvent.ReplacedEquipmentModified = true;
                }

                // do deletions first, otherwise we may end up with duplicate errors when trying to save the created/updated.
                foreach ( long refId in deletedSettingsGroups )
                {
                    // See if the settings we're deleting are for the currently docked instrument.
                    if ( !_statusEvent.InstrumentSettingsModified && ( _instrumentSn != string.Empty ) )
                    {
                        List<string> strList = instDataAccess.FindSerialNumbersByRefId( refId, trx );
                        _statusEvent.InstrumentSettingsModified = strList.Find( sn => sn == _instrumentSn ) != null;
                    }

                    Log.Debug( string.Format( "{0}Deleting settings group {1}", funcName, refId ) );
                    instDataAccess.Delete( refId, trx );
                }

                if ( updatedSettingsGroups.Count > 0 )
                    Log.Debug( string.Format( "{0}Saving {1} settings groups", funcName, updatedSettingsGroups.Count ) );

                foreach ( InstrumentSettingsGroup group in updatedSettingsGroups )
                {
                    if ( _instrumentSn != string.Empty ) // something docked?
                    {
                        // See if the settings we're updating WILL NOW apply to the currently docked instrument.
                        if ( !_statusEvent.InstrumentSettingsModified )
                            _statusEvent.InstrumentSettingsModified = group.SerialNumbers.Find( sn => sn == _instrumentSn ) != null;

                        // See if the settings we're updating USED TO be for the currently docked instrument.
                        if ( !_statusEvent.InstrumentSettingsModified )
                        {
                            List<string> strList = instDataAccess.FindSerialNumbersByRefId( group.RefId, trx );
                            _statusEvent.InstrumentSettingsModified = strList.Find( sn => sn == _instrumentSn ) != null;
                        }
                    }

                    Log.Trace( string.Format( "{0}Saving settings group {1}", funcName, group.RefId ) );
                    instDataAccess.Save( group, trx );
                }

                foreach ( Instrument instrument in updatedInstruments )
                {
                    // See if the instrument we're updating is the currently docked instrument.
                    if ( !_statusEvent.InstrumentSettingsModified && ( _instrumentSn != string.Empty ) && instrument.SerialNumber == _instrumentSn )
                        _statusEvent.InstrumentSettingsModified = true;

                    Log.Trace( string.Format( "{0}Saving instrument settings for {1}", funcName, instrument.SerialNumber ) );
                    instDataAccess.Save( instrument, trx );
                }

				foreach ( InstrumentSettingsGroup defaultGroup in updatedDefaultSettingsGroups )
				{
                    Log.Debug( string.Format( "{0}Saving default {1} instrument settings", funcName, defaultGroup.EquipmentCode ) );
                    instDataAccess.Save( defaultGroup, trx );

                    // If the default settings for the docked instrument are modified, then we only have to set InstrumentSettingsModified
                    // to true if there is no settings group for the currently docked instrument.
                    if ( !_statusEvent.InstrumentSettingsModified && _instrumentType == defaultGroup.EquipmentType && instDataAccess.FindGroupSettingsBySerialNumber( _instrumentSn, trx ) == null )
                        _statusEvent.InstrumentSettingsModified = true;
                }

                if ( sensorCalibrationLimits.Count > 0 )
                {
                    Log.Trace( string.Format( "{0} Saving Sensor Calibration Limits", funcName ) );
                    new SensorCalibrationLimitsDataAccess().Save( sensorCalibrationLimits, trx );
                }
                else
                {
                    //Delete the calibration limits if there is no limits passed in, since its no more applicable.
                    //This could happen when the calibration limits can be turned on or off at the admin console. In that case, we don't need to validate with the limits
                    new DataAccess.SensorCalibrationLimitsDataAccess().Delete( trx );
                }

                DateTime? schemaSettingsVersion = _schema.SettingsVersion;

                // Don't need to update the version unless it actually changes.
                if ( settingsVersion != _schema.SettingsVersion )
                {
                    new SchemaDataAccess().UpdateSettingsVersion( settingsVersion, trx );
                    schemaSettingsVersion = settingsVersion;
                }
                // If the version returned by the exchangestatus call is ever earlier, then
                // somebody probably did a manual change in the database which they shouldn't
                // have done.  When this happens, then the download call will keep returning
                // nothing, and also echoing back the schema version that we passed to it.
                // So we get into a cycle of exchangeStatus continually giving us back a version
                // that differs from our schema (making us think that something is new), but
                // the download call not actually ever giving us anything.  To avoid this, we
                // update our schema with the version returned by exchange status if it's ever
                // earlier than what we have stored.
                else if ( _inetStatus.Schema.SettingsVersion < _schema.SettingsVersion )
                {
                    Log.Warning( string.Format( "ExchangeStatus.SettingsVersion ({0}) is earlier than Schema! ({1})",
                        _inetStatus.Schema.SettingsVersion, _schema.SettingsVersion ) );
                    new SchemaDataAccess().UpdateSettingsVersion( _inetStatus.Schema.SettingsVersion, trx );
                    schemaSettingsVersion = _inetStatus.Schema.SettingsVersion;
                }
                else
                    Log.Trace( string.Format( "SettingsVersion not updated.  No change ({0})", Log.DateTimeToString( settingsVersion ) ) );

                _statusEvent.Errors.AddRange( trx.Errors );

                trx.Commit();

                // If chose to no update it just above, then it will be what it was originally.
                // We dont' update the memory-resident schema until we know we've safetely committed.
                _schema.SettingsVersion = schemaSettingsVersion;
            }

            Log.Debug( string.Format( "{0}Update complete in {1} seconds", funcName, s.ElapsedMilliseconds / 1000.0 ) );
        }

        private void UpdateCriticalErrors( InetDownloader inetDownloader )
        {
            const string funcName = "UpdateCriticalErrors: ";

            if ( _schema.CriticalErrorsVersion == _inetStatus.Schema.CriticalErrorsVersion )
            {
                Log.Trace( string.Format( string.Format( "{0} inet={1}, schema={2}, so doing nothing.",
                    funcName, Log.DateTimeToString( _inetStatus.Schema.CriticalErrorsVersion ), Log.DateTimeToString( _schema.CriticalErrorsVersion ) ) ) );
                return;
            }

            Log.Debug( string.Format( "{0}iNetVersion=\"{1}\", schemaVersion=\"{2}\"", funcName,
                Log.DateTimeToString( _inetStatus.Schema.CriticalErrorsVersion ), Log.DateTimeToString( _schema.CriticalErrorsVersion ) ) );


            List<CriticalError> criticalErrors = new List<CriticalError>();

            DateTime? criticalErrorsVersion = inetDownloader.DownloadCriticalErrors( criticalErrors, _schema.CriticalErrorsVersion );

            // Nothing to do? then return now instead of starting a transaction for nothing
            if ( criticalErrorsVersion == null )
            {
                Log.Error( string.Format( "{0}null CriticalErrorsVersion returned, so doing nothing.", funcName ) );
                return;
            }

            Log.Debug( string.Format( "{0}{1} instrument critical error returned by iNet", funcName, criticalErrors.Count ) );
            Log.Debug( string.Format( "{0}CriticalErrorsVersion={1}", funcName, Log.DateTimeToString( criticalErrorsVersion ) ) );

            if ( _schema.CriticalErrorsVersion == criticalErrorsVersion && criticalErrors.Count == 0 )
            {
                Log.Debug( string.Format( string.Format( "{0}No changes returned, so doing nothing.", funcName ) ) );
                return;
            }

            Log.Debug( string.Format( "{0}Updating database", funcName ) );

            Stopwatch s = new Stopwatch(); s.Start();

            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                new CriticalErrorDataAccess().Save( criticalErrors, trx );

                new SchemaDataAccess().UpdateCriticalErrorsVersion( _inetStatus.Schema.CriticalErrorsVersion, trx );

                trx.Commit();

                _schema.CriticalErrorsVersion = criticalErrorsVersion;
            }

            Log.Debug( string.Format( "{0}Update complete in {1} seconds", funcName, s.ElapsedMilliseconds / 1000.0 ) );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="manifolds">manifold assignments downloaded from iNet</param>
        /// <param name="manualCylinders">manual assignments downloaded from iNet</param>
        /// <returns></returns>
        private List<GasEndPoint> GetChangedManualCylinders( List<GasEndPoint> inetManifolds, List<GasEndPoint> inetManualCylinders )
        {
            // First, for each installed iGas cylinder, see if we received manifold information at
            // the same port position.  If so, we ignore the manifold info (iGas takes precedences).
            foreach ( GasEndPoint gasEndPoint in Configuration.DockingStation.GasEndPoints.FindAll( g => g.InstallationType == GasEndPoint.Type.iGas ) )
            {
                GasEndPoint manifold = inetManifolds.Find( m => m.Position == gasEndPoint.Position );

                if ( manifold == null )
                    continue;

                string msg = string.Format( "Manifold settings (fid=\"{0}\",pn=\"{1}\") ignored for port {2}. Port contains iGas (fid=\"{3}\",pn=\"{4}\").",
                    manifold.Cylinder.FactoryId, manifold.Cylinder.PartNumber, manifold.Position, gasEndPoint.Cylinder.FactoryId, gasEndPoint.Cylinder.PartNumber );

                Log.Warning( msg );
                _statusEvent.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );

                inetManifolds.Remove( manifold ); // by removing it, we effectly ignore it.
            }

            // Next, for each installed iGas cylinder, see if we received manual cylinder information at
            // the same port position.  If so, we ignore the manual cylinder info (iGas takes precedences).
            foreach ( GasEndPoint gasEndPoint in Configuration.DockingStation.GasEndPoints.FindAll( g => g.InstallationType == GasEndPoint.Type.iGas ) )
            {
                GasEndPoint manual = inetManualCylinders.Find( m => m.Position == gasEndPoint.Position );

                if ( manual == null )
                    continue;

                string msg = string.Format( "Manual cylinder settings (fid=\"{0}\",pn=\"{1}\") ignored for port {2}. Port contains iGas (fid=\"{3}\",pn=\"{4}\").",
                    manual.Cylinder.FactoryId, manual.Cylinder.PartNumber, manual.Position, gasEndPoint.Cylinder.FactoryId, gasEndPoint.Cylinder.PartNumber );

                Log.Warning( msg );
                _statusEvent.Errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );

                inetManualCylinders.Remove( manual ); // by removing it, we effectly ignore it.
            }

            List<GasEndPoint> changedCylinderList = new List<GasEndPoint>();
            GasEndPoint manifoldCylinder;
            GasEndPoint manualCylinder;

            // See if we know of an installed manifold that we didn't receive info for from iNet. 
            // If so, that implies that the manifold was removed in iNet. 
            foreach ( GasEndPoint manifold in Configuration.DockingStation.GasEndPoints.FindAll( m => m.InstallationType == GasEndPoint.Type.Manifold ) )
            {
                if ( inetManifolds.Find( m => m.Position == manifold.Position
                                      && m.InstallationType == GasEndPoint.Type.Manifold
                                      && m.Cylinder.FactoryId == manifold.Cylinder.FactoryId ) == null )
                {
                    manifoldCylinder = (GasEndPoint)manifold.Clone();
                    manifoldCylinder.GasChangeType = GasEndPoint.ChangeType.Uninstalled;
                    Log.Debug( string.Format( "No info from iNet for current manifold on port {0} (fid=\"{1}\",pn=\"{2}\"). Uninstalling.", manifold.Position, manifold.Cylinder.FactoryId, manifold.Cylinder.PartNumber ) );
                    changedCylinderList.Add( manifoldCylinder );
                }
            }

            // See if we know of an installed manual cylinder that we didn't receive info for from iNet. 
            // If so, that implies that the manual cylinder was removed in iNet.
            // (Don't uninstall fresh air; it appears as an installed manual cylinder.)
            foreach ( GasEndPoint manual in Configuration.DockingStation.GasEndPoints.FindAll( m => ( m.InstallationType == GasEndPoint.Type.Manual ) && !m.Cylinder.IsFreshAir ) )
            {
                if ( inetManualCylinders.Find( m => m.Position == manual.Position
                                            && m.InstallationType == GasEndPoint.Type.Manual
                                            && m.Cylinder.FactoryId == manual.Cylinder.FactoryId ) == null )
                {
                    manualCylinder = (GasEndPoint)manual.Clone();
                    manualCylinder.GasChangeType = GasEndPoint.ChangeType.Uninstalled;
                    Log.Debug( string.Format( "No info from iNet for current manual cylinder on port {0} (fid=\"{1}\",pn=\"{2}\"). Uninstalling.", manual.Position, manual.Cylinder.FactoryId, manual.Cylinder.PartNumber ) );
                    changedCylinderList.Add( manualCylinder );
                }
            }

            // See if we've received a new manifold from iNet that we don't already have know about. 
            // If so, it implies that the manifold is newly added in iNet. 
            foreach ( GasEndPoint manifold in inetManifolds )
            {
                if ( Configuration.DockingStation.GasEndPoints.Find( m => m.Position == manifold.Position
                                                                  && m.InstallationType == GasEndPoint.Type.Manifold
                                                                  && m.Cylinder.FactoryId == manifold.Cylinder.FactoryId ) == null )
                {
                    manifoldCylinder = (GasEndPoint)manifold.Clone();
                    manifoldCylinder.GasChangeType = GasEndPoint.ChangeType.Installed;
                    Log.Debug( string.Format( "New manifold info received from iNet for port {0} (fid=\"{1}\",pn=\"{2}\"). Installing.", manifoldCylinder.Position, manifoldCylinder.Cylinder.FactoryId, manifoldCylinder.Cylinder.PartNumber ) );
                    changedCylinderList.Add( manifoldCylinder );
                }
            }

            // See if we've received a new manual assignment from iNet that we don't already have know about. 
            // If so, it implies that the manual assignment is newly added in iNet. 
            foreach ( GasEndPoint manual in inetManualCylinders )
            {
                if ( Configuration.DockingStation.GasEndPoints.Find( m => m.Position == manual.Position
                                                                  && m.InstallationType == GasEndPoint.Type.Manual
                                                                  && m.Cylinder.FactoryId == manual.Cylinder.FactoryId ) == null )
                {
                    manualCylinder = (GasEndPoint)manual.Clone();
                    manualCylinder.GasChangeType = GasEndPoint.ChangeType.Installed;
                    Log.Debug( string.Format( "New manual cylinder received from iNet for port {0} (fid=\"{1}\",pn=\"{2}\"). Installing.", manualCylinder.Position, manualCylinder.Cylinder.FactoryId, manualCylinder.Cylinder.PartNumber ) );
                    changedCylinderList.Add( manualCylinder );
                }
            }

            return changedCylinderList;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="changedCylinders"></param>
        private void SaveChangedManualCylinders( List<GasEndPoint> changedCylinders, DataAccessTransaction trx )
        {
            new GasEndPointDataAccess().SaveChangedCylinders( changedCylinders, trx );
        }

        /// <summary>
        /// Compares the contents of two string lists.
        /// </summary>
        /// <param name="list1"></param>
        /// <param name="list2"></param>
        /// <returns>False if the two lists contain the same contents (order does not matter). True if they're different.</returns>
        private bool CompareStringLists( IList<string> list1, IList<string> list2 )
        {
            // If the number of items are different, then the lists are obviously different.
            if ( list1.Count != list2.Count )
                return true;

            // No need to go and allocate lists for sorting if the passed-in lists are empty.
            if ( list1.Count == 0 && list2.Count == 0 )
                return false;

            List<string> sortedList1 = new List<string>( list1 );
            sortedList1.Sort();

            List<string> sortedList2 = new List<string>( list2 );
            sortedList2.Sort();

            for ( int i = 0; i < sortedList1.Count; i++ )
            {
                if ( sortedList1[i] != sortedList2[i] )
                    return true;
            }

            return false;
        }

    }  // end-class

} // end-namespace
