using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    //////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to send events to the server and
    /// synchronize the docking station's time with the server.
    /// </summary>
    public sealed class ReporterService : Service
    {
        #region Fields

        private Queue<DockingStationError> _errorsQueue = new Queue<DockingStationError>();

        /// <summary>
        /// True if we know there is queued data pending upload to iNet; else false.
        /// </summary>
        public bool PendingUploads { get; private set; }

        /// <summary>
        /// True if we know we're connected to a network (i.e., we have an IP address; else false.
        /// </summary>
        /// <remarks>
        /// This is only valid for DHCP.  If we have a static IP, then it will look like we're on a network.
        /// </remarks>
        public static bool Networked { get; set; }

        private EventProcessor _eventProcessor = new EventProcessor();

        /// <summary>
        /// The last CONSECUTIVE NotificationAction that ProcessNotifcationAction has seen.
        /// </summary>
        private DockingStationError _lastNotificationError = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of a ReporterService class.
        /// </summary>
        public ReporterService( Master master )
            : base( master )
        {
#if DEBUG
            IdleTime = new TimeSpan( 0, 1, 0 ); // minutes
#else
            IdleTime = new TimeSpan( 0, 5, 0 ); // minutes
#endif

        }

        #endregion

        #region Properties

        #endregion

        #region Methods

        protected override void OnStart()
        {
            // Upon startup, see if there's anything already queued.
            try
            {
                bool isEmpty = PersistedQueue.CreateInetInstance().IsEmpty();
                Log.Info( string.Format( "{0}.OnStart: IsEmpty={1}", Name, isEmpty ) );
                PendingUploads = isEmpty == false;
            }
            catch ( Exception e )
            {
                Log.Error( e );
            }

            // Since we pull from the message queue and upload to iNet in the 'background', 
            // there's no reason to run at a high/normal priority.
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        }

        /// <summary>
        /// Pausing the Reporter service disables the message queue (messages can't be queued or deqeueued).
        /// Unpausing re-enables the message queue.
        /// </summary>
        /// <param name="pausing"></param>
        protected override void OnPause( bool pausing )
        {
            // Whenver this service is paused, then pause the message queue, too.
            PersistedQueue.Paused = pausing;
        }

        private bool Runnable()
        {
            if ( ( Controller.RunState & Controller.State.FlashCardError ) != 0
            ||   ( Controller.RunState & Controller.State.InetQueueError ) != 0 )
            {
                Log.Error( ">INET: Can't access iNet queue due to Controller.State 0x" + Controller.RunState.ToString() );
                return false;
            }

            // if Not activated, and not in service mode or no account, then do nothing.
            if ( !Configuration.Schema.Activated && ( !Configuration.ServiceMode || string.IsNullOrEmpty( Configuration.Schema.AccountNum ) ) )
            {
                Log.Debug( ">INET: " + Name + " is not activated." );
                return false;
            }

            if ( Paused )
            {
                Log.Debug( ">INET: " + Name + " is paused." );
                return false;
            }

            if ( Configuration.DockingStation.InetUrl == string.Empty )
            {
                Log.Error( ">INET: No iNet URL." );
                return false;
            }

            if ( PersistedQueue.Paused )
            {
                Log.Debug( ">INET: Upload queue paused." );
                return false;
            }

            if ( !ReporterService.Networked )
            {
                Log.Debug( ">INET: No network." );
                return false;
            }

            return true;
        }

        /// <summary>
        /// This method implements the thread start for this service.
        /// </summary>
        protected override void Run()
        {
            // For logging, keep track of number of items found on queue, which of 
            // those successfully uploaded, and which that were ignored/skipped.
            int queuedCount = 0, uploadedCount = 0, ignoredCount = 0;

            InetUploader inetUploader = null;

            try
            {
                PersistedQueue inetQueue = PersistedQueue.CreateInetInstance();

                while ( true )
                {
                    if ( !Runnable() ) // Keep sending until the message queue is empty or we're stopped/paused.
                        break;

                    try
                    {
                        Log.Trace( ">INET: Checking for non-empty queue." );

                        bool isEmpty = inetQueue.IsEmpty();

                        if ( isEmpty )
                        {
                            Log.Trace( ">INET: No data in upload queue." );
                            PendingUploads = false;
                            break;
                        }

                        Log.Debug( ">INET: Retrieving queued item...." );

                        // Get the oldest object on the queue (the object is not removed).
                        object queObject = inetQueue.Peek();

                        if ( queObject == null )
                        {
                            Log.Debug( ">INET: No data in upload queue." );
                            PendingUploads = false;
                            break;
                        }

                        PendingUploads = true;

                        string deviceLocation = string.Empty;

                        object wsParmObject = null;

                        queuedCount++;

                        QueueData queueData = null;
                        try
                        {
                            queueData = (QueueData)queObject;

                            double kB = (double)queueData.WebServiceParameterSize / 1024.0d; // convert bytes to kilobytes
                            Log.Debug( string.Format( ">INET: {0} ({1},\"{2}\",{3}), {4} KB)...",
                                queueData, queueData.Id, Log.DateTimeToString( queueData.Timestamp ), queueData.InetAccountNum, kB.ToString( "f1" ) ) );

                            wsParmObject = queueData.WebServiceParameter;
                        }
                        catch ( Exception e )
                        {
                            // If this failed, then something was on the queue which 
                            // was not the right type.  Could have been old data.
                            // Just ignore it.
                            ignoredCount++;
                            Log.Debug( ">INET: " + e.ToString() );

                            // If debug build, DO NOT delete poison data. We need to keep 
                            // it so that we can investigate what's wrong with it.
#if DEBUG
                            Log.Debug( ">INET: Found non-conforming data on queue: " );
                            Log.Debug( ">INET: " + queObject.ToString() );
                            break;
#else
                        Log.Debug( ">INET: Found non-conforming data on queue. Purging it:" );
                        Log.Debug( ">INET: " + queObject.ToString() );
                        //_msmqTransaction.Commit();
                        inetQueue.Delete( queueData.Id );
                        continue;
#endif
                        }

                        // We don't instantiate iNet until we know we need it And, if we make it
                        // to here, then that's now.  We then will continue to re-use it for the duration
                        // that we remain in the loop.
                        if ( inetUploader == null )
                            inetUploader = new InetUploader();

                        string errorCode = inetUploader.Upload( wsParmObject, queueData.Label, queueData.InetAccountNum );

                        // On any error, just break out and we'll retry on next iteration of Run().
                        // The intent is that if there's some network issue, or we're just offline,
                        // then we watn to let some time pass for the issue to clear up or to go back online.
                        if ( errorCode != string.Empty )
                            break;

                        // Now that we've successfully uploaded it to iNet, we can remove it from the queue
                        Log.Debug( string.Format( ">INET: Deleting uploaded {0} from queue.", queueData ) );
                        inetQueue.Delete( queueData.Id );

                        uploadedCount++;
                    }
                    // catch
                    catch ( Exception ex1 )
                    {
                        Log.Debug( ">INET: Exception caught, - " + ex1.ToString() );
                    }

                } // end-while !empty queue
            }
            finally
            {
                if ( inetUploader != null ) inetUploader.Dispose();
            }

            if ( queuedCount > 0 )
                Log.Debug( ">INET: Finished. " + queuedCount + " found in queue, " + uploadedCount + " uploaded, " + ignoredCount + " ignored" );
        }

        /// <summary>
        /// Reports a docking station event to the server.
        /// </summary>
        /// <param name="dockingStationEvent">
        /// The event to be reported
        /// </param>
        /// <returns>
        /// A docking station action indicating the next task
        /// the docking station should perform.
        /// </returns>
        public DockingStationAction ReportEvent( DockingStationEvent dsEvent )
        {
            if ( dsEvent == null )
                return null;

            if ( dsEvent.DockingStation.SerialNumber == string.Empty )
                return null;

            LogEventDetails(dsEvent);

            DockingStationAction dsAction = null;

            // We don't yet instantiate the Uploader instance.  We wait until we know for sure
            // we're going to use it. Note that it's Disposed of in the finally block below.
            InetUploader inetUploader = null;

            try
            {
                try
                {
                    // It's now finally safe to log the event.

                    _eventProcessor.Save( dsEvent, Master.Instance.SwitchService.DockedTime );

                    // These upload calls won't actually try and upload if we're not associated with any account.
                    // or even if we DO have an account number, it won't upload if we're not activated on iNet.
                    // Yet it WILL upload if we're in Service mode, regardless if activated or not.
                    // Confusing, eh?
                    inetUploader = new InetUploader();
                    inetUploader.UploadEvent( dsEvent, Configuration.DockingStation.TimeZoneInfo );

					if ( dsEvent is InstrumentGasResponseEvent )
					{
						// Print automated bumps and cals
						new PrintManager().Print( (InstrumentGasResponseEvent)dsEvent );
					}

					// Only save event to USB drive when in Cal Station mode and not Service mode
					if ( !Configuration.Schema.Activated && !Configuration.ServiceMode )
					{
						if ( dsEvent is InstrumentDatalogDownloadEvent )
						{
							// if the full datalog can not be saved to the USB drive an exception will 
							// be thrown to make the docking station go unavailable; this is to prevent
							// the DS from erasing the datalog when it wasn't able to save it
							new CsvFileManager().Save( (InstrumentDatalogDownloadEvent)dsEvent );
						}
						else if ( dsEvent is InstrumentGasResponseEvent )
						{
							// only bumps and cals are saved
							new CsvFileManager().Save( (InstrumentGasResponseEvent)dsEvent );
						}
					}

                    ReportQueuedErrors( inetUploader );

                    // See if docking station needs to take any special action for any of the event's errors.
                    dsAction = ExamineErrors( dsEvent, inetUploader );

                    if ( dsAction != null )
                    {
                        Log.Debug( string.Format( "{0}: ExamineErrors returned {1}", Name, dsAction.Name ) );
                        return dsAction;
                    }
                }
                catch ( Exception e )
                {
                    Log.Error( Name, e );

                    dsAction = new UnavailableAction( e );
                    // DO NOT report the error.  If we have an error uploading, then it makes
                    // no sense to try and upload and error notifying of a problem trying to upload.
                    //ProcessNotificationAction( dsAction, inet );

                    return dsAction;
                }

                try
                {
                    // Determine if the event that just transpired requires a followed up RebootAction.
                    dsAction = CheckRebootableEvent( dsEvent );

                    if ( dsAction == null )
                    {
                        // Before determining our next action, make sure we have the most up-to-date
                        // schedules and eventjournals, etc., from iNet.
                        ExchangeInetStatus( dsEvent );

                        // Find out what we're supposed to do next.
                        dsAction = Master.Scheduler.GetNextAction( dsEvent );

                        if ( dsAction is InstrumentAction )
                        {
                            InstrumentAction instAction = (InstrumentAction)dsAction;                            

                            if ( instAction is InstrumentGasAction )
                            {
                                // Get the resources from the resource manager. 
                                StringBuilder explanation = new StringBuilder();
                                List<string> consoleMessages = new List<string>(); // SGF  20-Feb-2013  INS-3821
                                List<string> errorCodes = new List<string>(); // SGF  20-Feb-2013  INS-3821

                                InstrumentGasAction gasAction = instAction as InstrumentGasAction;

                                string eventCode = null;
                                if ( gasAction is InstrumentBumpTestAction )
                                    eventCode = EventCode.BumpTest;
                                else if ( gasAction is InstrumentCalibrationAction )
                                    eventCode = EventCode.Calibration;
                                else
                                    throw new ArgumentOutOfRangeException( "Unrecognized GasAction: " + gasAction.GetType().ToString() );

                                // SGF  20-Feb-2013  INS-3821
                                // SGF  03-Nov-2010  Single Sensor Cal and Bump
                                gasAction.GasEndPoints = Master.Instance.ResourceService.GetGasEndPoints( eventCode, gasAction, explanation, consoleMessages, errorCodes );

                                if ( gasAction.GasEndPoints.Count == 0 )
                                {
                                    Log.Warning( string.Format( "No gases for {0}. {1}", eventCode, explanation.ToString() ) );
                                    // Maintain the empty cylinder error state resulting from forced actions by re-forcing the action
                                    Master.Instance.Scheduler.ReForceEvent( instAction );

                                    dsAction = new ResourceUnavailableAction(errorCodes, consoleMessages); // SGF  20-Feb-2013  INS-3821
                                }
                            }

                            // For instrument firmware upgrades, we don't want to allow the upgrade to take place
                            // if there's no gas to both calibrate and bump the instrument.  This is because at the
                            // end of the upgrade, the VDS will automatically calibrate then bump test the instrument.
                            if ( instAction is InstrumentFirmwareUpgradeAction )
                            {
                                StringBuilder explanation = new StringBuilder();
                                List<string> consoleMessages = new List<string>(); // SGF  20-Feb-2013  INS-3821
                                List<string> errorCodes = new List<string>(); // SGF  20-Feb-2013  INS-3821

                                // SGF  20-Feb-2013  INS-3821
                                // SGF  03-Nov-2010  Single Sensor Cal and Bump
                                if ( Master.Instance.ResourceService.GetGasEndPoints( EventCode.Calibration, instAction, explanation, consoleMessages, errorCodes ).Count == 0 )
                                {
                                    Log.Warning( string.Format( "No gases for firmware upgrade {0}. {1}", EventCode.Calibration, explanation.ToString() ) );
                                   
                                    dsAction = new ResourceUnavailableAction( errorCodes, consoleMessages ); // SGF  20-Feb-2013  INS-3821
                                }
                                else
                                {
                                    explanation = new StringBuilder();
                                    consoleMessages.Clear(); // SGF  20-Feb-2013  INS-3821
                                    errorCodes.Clear(); // SGF  20-Feb-2013  INS-3821

                                    // SGF  20-Feb-2013  INS-3821
                                    // SGF  03-Nov-2010  Single Sensor Cal and Bump
                                    if ( Master.Instance.ResourceService.GetGasEndPoints( EventCode.BumpTest, instAction, explanation, consoleMessages, errorCodes ).Count == 0 )
                                    {
                                        Log.Warning( string.Format( "No gases for firmware upgrade {0}. {1}", EventCode.BumpTest, explanation.ToString() ) );
                                      
                                        dsAction = new ResourceUnavailableAction( errorCodes, consoleMessages ); // SGF  20-Feb-2013  INS-3821
                                    }
                                }
                            }
                        }
                    }
                }
                // INS-8228 RHP v7.6 Log and report InstrumentSystemAlarmException thrown from Scheduler
                catch (InstrumentSystemAlarmException e)
                {
                    Log.Error(Name, e);
                    Master.ConsoleService.UpdateState(ConsoleState.InstrumentSystemAlarm);
                    Master.ExecuterService.ReportExceptionError(e); 
                }
                catch ( Exception e )
                {
                    Log.Error( Name, e );
                    dsAction = new UnavailableAction( e );
                }


                // If the scheduler says there's currently nothing to do, then check
                // if we have an illegal cylinder or not.  Report an unsupported cylinder if so.
                if ( dsAction is NothingAction )
                {
                    DockingStationAction cylinderAction = ExamineGasEndPoints();

                    if ( cylinderAction != null ) dsAction = cylinderAction;
                }

                // If the action we're about to return is telling the VDS to display an error message, 
                // then we should tell iNet the error too. We pass in our Uploader instance to re-use the 
                // socket which should give a couple seconds performance benefit.
                ProcessNotificationAction( dsAction, inetUploader );
            }
            finally
            {
                if ( inetUploader != null ) inetUploader.Dispose();
            }

            return dsAction;
        }

        /// <summary>
        /// Logs the event's formatted Details string.
        /// </summary>
        /// <param name="dsEvent"></param>
        private void LogEventDetails( DockingStationEvent dsEvent )
        {
            // Keep the cached instrument info up to date.
            //if ( dsEvent is InstrumentSettingsReadEvent )
            //    SwitchService.DockedInstrument = (Instrument)( (InstrumentSettingsReadEvent)dsEvent ).DockedInstrument.Clone();

            // Parse out details so we can send to debug port below
            string[] details = dsEvent.Details.Split( new char[] { '\n' } );

            Log.Debug( string.Format( "{0}.Report uploading Event \"{1}\")", Name, dsEvent ) );
            foreach ( DockingStationError err in dsEvent.Errors )
                Log.Debug( string.Format( "{0}.Report attaching queued Error to event: \"{1}\"", Name, err.Description ) );

            foreach ( string detail in details )
                Log.Debug( detail );
        }

		public DockingStationAction ReportFlowFailedError( GasEndPoint gasEndPoint )
        {
            Log.Debug( string.Format( "Empty Cylinder was reported on position {0} during gas operation.", gasEndPoint.Position ) );

            using ( InetUploader inetUploader = new InetUploader() )
            {
                DockingStationAction dsAction = ProcessEmptyGasEndPoint( gasEndPoint, inetUploader );
                return dsAction;
            }
        }

        /// <summary>
        /// When bad pump tubing is detected, dock uploads it as a docking station error to iNet
        /// and sends BadPumpTubingDetectedAction.
        /// </summary>
        /// <returns></returns>
        public DockingStationAction ReportBadCradleTubingError()
        {
            string message = string.Format("Bad pump tubing was detected on docking station {0}.", Configuration.DockingStation.SerialNumber);
            Log.Debug(string.Format(message));

            //Report kinked tubing error to iNet
            DockingStationError error = new DockingStationError(message, DockingStationErrorLevel.Error, Configuration.DockingStation.SerialNumber, "KINKED_TUBING_DETECTED");
            ReportError(error);

            //Return action to show error on dock's LCD
            DockingStationAction dsAction = new BadPumpTubingDetectedAction(Configuration.DockingStation.SerialNumber);
            return dsAction;
        }

        private DockingStationAction ExamineErrors( DockingStationEvent dsEvent, InetUploader inet )
        {
            DockingStationErrorLevel highestError = DockingStationErrorLevel.None;

            DockingStationAction dsAction = null;

            foreach (DockingStationError error in dsEvent.Errors)
            {
                highestError = (DockingStationErrorLevel)Math.Max( (int)error.ErrorLevel, (int)highestError );
            }

            if (dsAction != null)
                return dsAction;

            if (highestError >= DockingStationErrorLevel.Error)
            {
                dsAction = new UnavailableAction();
                Log.Debug(string.Format("{0}Returning {1}", Name, dsAction.Name));
                return dsAction;
            }

            return null;
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// 1) Updates the cylinder's pressure in the database (change it from Full to Low, or Low to Empty).
        /// 2) Update cached cylinder (in Configuration.DockingStation.InstalledCylinders) with the pressure
        /// change.
        /// 3) Also forces an upload of a SettingsReadEvent in order to notify iNet of the pressure change.
        /// </remarks>
        /// <param name="emptyCylinderError"></param>
        /// <param name="inetUploader"></param>
        /// <returns></returns>
		private DockingStationAction ProcessEmptyGasEndPoint( GasEndPoint emptyEndPoint, InetUploader inetUploader )
        {
            Log.Debug( string.Format( "Empty Cylinder was reported on position {0}", emptyEndPoint.Position ) );

            GasEndPoint gasEndPoint = null;

            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                GasEndPointDataAccess gepDataAccess = new GasEndPointDataAccess();

                gasEndPoint = gepDataAccess.FindByPosition( emptyEndPoint.Position, trx );

                if ( gasEndPoint == null )
                    return null; // should we display an error?

                if ( gasEndPoint.Cylinder.Pressure == PressureLevel.Full )
                {
                    gasEndPoint.Cylinder.Pressure = PressureLevel.Low;
                    Log.Warning( "Low pressure warning, position " + emptyEndPoint.Position );
                }
                else
                {
                    gasEndPoint.Cylinder.Pressure = PressureLevel.Empty;
                    Log.Warning( "Empty pressure warning, position " + emptyEndPoint.Position );
                }

                Log.Debug( string.Format( "Changing pressure to \"{0}\" for cylinder on position {1}", gasEndPoint.Cylinder.Pressure.ToString(), gasEndPoint.Position ) );
                Log.Debug( string.Format( "...PartNumber={0}, FactoryId={1}", gasEndPoint.Cylinder.PartNumber, gasEndPoint.Cylinder.FactoryId ) );

                gepDataAccess.UpdatePressureLevel( gasEndPoint, trx );

                trx.Commit();
            }

            // After updating the database, we need to update the cached copy of the 
            // installed cylinder, too, which is kept in the global DockingStation.
            GasEndPoint cachedGasEndPoint = Configuration.DockingStation.GasEndPoints.Find( g => g.Position == gasEndPoint.Position );
            if ( cachedGasEndPoint != null )
                cachedGasEndPoint.Cylinder.Pressure = gasEndPoint.Cylinder.Pressure;

            // We need to inform iNet whenever a cylinder's pressure changes. We can only do this
            // by uploading a full docking station with the cylinder information.  Which means we
            // need to immediately do a SettingsRead to get the full docking station info, and then
            // upload that info along with the currently installed cylinder info.

            SettingsReadOperation settingsReadOperation = new SettingsReadOperation();
            // Explicitly set the ChangedSmartCards to all falses so that no smart cards are read.
            settingsReadOperation.ChangedSmartCards = new bool[ Configuration.DockingStation.NumGasPorts ];

            SettingsReadEvent settingsReadEvent = (SettingsReadEvent)settingsReadOperation.Execute();

            // Copy currently installed cylinder info to the docking station object we're going to upload.
            settingsReadEvent.DockingStation.GasEndPoints.Clear();
            settingsReadEvent.DockingStation.ChangedGasEndPoints.Clear();

            foreach ( GasEndPoint gep in Configuration.DockingStation.GasEndPoints )
                settingsReadEvent.DockingStation.GasEndPoints.Add( (GasEndPoint)gep.Clone() );

            inetUploader.UploadEvent( settingsReadEvent, Configuration.DockingStation.TimeZoneInfo );

            // BEGIN INS-8630 RHP v7.5
            List<string> consoleMessages = new List<string>();
            consoleMessages.Add(gasEndPoint.Cylinder.Pressure.ToString());

            if (!string.IsNullOrEmpty(gasEndPoint.Cylinder.PartNumber)) // For ISC Pass the Cylinder Part Number
                consoleMessages.Add(gasEndPoint.Cylinder.PartNumber);
            // For Non ISC Pass the gas code. But I believe that both ISC and Non-ISC cylinders have their own PArt numbers.
            // So below condition may not be required ?
            else if (gasEndPoint.Cylinder.GasConcentrations.Count > 0)
                consoleMessages.Add(gasEndPoint.Cylinder.GasConcentrations[0].Type.Symbol);

            Log.Info("Sending IDS ReplaceCylinderAction");
            DockingStationAction dsAction = new ResourceUnavailableAction(new List<string>() { gasEndPoint.Cylinder.Pressure.ToString() }, consoleMessages);
            // END INS-8630
                       
            return dsAction;
        }


        private DockingStationAction ExamineGasEndPoints()
        {
            // First, for each attached cylinder, make sure it's a valid part number.
            // We try to not hit the database over and over to do this check
			// The GasEndPoints.Supported property helps us do that. If it's null,
            // then we've not checked the cylinder, so we query the database to do so.
			// We then set it to true or false appropiately.  Once all GasEndPoints' Supported
            // properties are non-null, we don't have to hit the database anymore.
            // Note that there's no need to check the 'part number' for fresh air.

            List<GasEndPoint> unverifiedGasEndPoints
                = Configuration.DockingStation.GasEndPoints.FindAll( g => g.Supported == null && g.Cylinder.PartNumber != FactoryCylinder.FRESH_AIR_PART_NUMBER );

            if ( unverifiedGasEndPoints.Count > 0 )
            {
                // Next, for every currently installed cylinder, verify that we recognize
                // the part number.  We check because the user may attach a cylinder
                // that has a new part number not yet known to iNet.
                FactoryCylinderDataAccess fcDataAccess = new FactoryCylinderDataAccess();

                using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
                {
                    foreach ( GasEndPoint g in unverifiedGasEndPoints )
                    {
                        g.Supported = fcDataAccess.FindByPartNumber( g.Cylinder.PartNumber, trx ) != null;

						Log.Debug( string.Format( "Verified GasEndPoint {0} on port {1}. Supported={2}", g.Cylinder.PartNumber, g.Position, g.Supported ) );

                        if ( g.Supported == false ) // no match found? Must be an unknown part number.
                            return CreateUnsupportedCylinderAction( g );
                    }
                }
            }

            // At this point, we expect that cylinders have been verified.
            // We now check to see that each cylinder is supported by checking the 
            // InstalledCylinder.Supported property.
            List<GasEndPoint> unsupportedGasEndPoints = Configuration.DockingStation.GasEndPoints.FindAll( g => g.Supported != null && g.Supported == false );
            if ( unsupportedGasEndPoints.Count > 0 )
            {
                GasEndPoint gep = unsupportedGasEndPoints[0];
				Log.Debug( string.Format( "GasEndPoint {0} on port {1}. Supported={2}", gep.Cylinder.PartNumber, gep.Position, gep.Supported ) );
                return CreateUnsupportedCylinderAction( gep );
            }

            // Next, verify that the cylinder on port 1 is legal for whatever the 
            // current Fresh/Zero air restriction for that port.

            GasEndPoint gasEndPoint = Configuration.DockingStation.GasEndPoints.Find( g => g.Position == 1 );

            // Either zero air OR fresh air is allowed?
            if ( Configuration.DockingStation.Port1Restrictions == ( PortRestrictions.FreshAir | PortRestrictions.ZeroAir ) )
            {
                if ( gasEndPoint != null && !gasEndPoint.Cylinder.IsFreshAir && !gasEndPoint.Cylinder.IsZeroAir )
                {
                    Log.Debug( string.Format( "Port1Restriction={0}, but cylinder is {1}", Configuration.DockingStation.Port1Restrictions.ToString(), gasEndPoint.Cylinder.PartNumber ) );
                    return CreateUnsupportedCylinderAction( gasEndPoint );
                }
            }

            // Only fresh air is allowed?  It's illegal to have a non-freshAir cylinder is installed.
            if ( Configuration.DockingStation.Port1Restrictions == PortRestrictions.FreshAir )
            {
                if ( gasEndPoint != null && !gasEndPoint.Cylinder.IsFreshAir )
                {
                    Log.Debug( string.Format( "Port1Restriction is FreshAir, but cylinder is {0}", gasEndPoint.Cylinder.PartNumber ) );
                    return CreateUnsupportedCylinderAction( gasEndPoint );
                }
            }

            // Only zero air is allowed? It's illegal to have either fresh air or a non-zeroAir
            // cylinder installed.
            else if ( Configuration.DockingStation.Port1Restrictions == PortRestrictions.ZeroAir )
            {
                // It's required that we have a zero-air cylinder on port1.  So return
                // Unsupported gas if there is no cylinder, or if the cylinder is not zero-air.
                if ( gasEndPoint == null )
                {
                    Log.Debug( "Port1Restriction is ZeroAir, but no cylinder is installed" );
                    return CreateUnsupportedCylinderAction( GasEndPoint.CreateFreshAir( Controller.FRESH_AIR_GAS_PORT ) );
                }

                if ( !gasEndPoint.Cylinder.IsZeroAir )
                {
                    Log.Debug( string.Format( "Port1Restriction is ZeroAir, but cylinder is {0}", gasEndPoint.Cylinder.PartNumber ) );
                    return CreateUnsupportedCylinderAction( gasEndPoint );
                }
            }

            return null;
        }

        private UnsupportedCylinderAction CreateUnsupportedCylinderAction( GasEndPoint gasEndPoint )
        {
            UnsupportedCylinderAction action = new UnsupportedCylinderAction( gasEndPoint );
            action.Messages.Add( gasEndPoint.Cylinder.PartNumber );
            return action;
        }

        /// <summary>
        /// For certain 'actions' that the VDS decides to do, iNet needs to be
        /// notified of.  e.g. failed Leak check, Unavailable Gas, etc.
        /// </summary>
        /// <param name="dsAction"></param>
        /// <param name="inetUploader">May be null.
        /// If null is passed, the method will instantiate an Inet object to use.</param>
        private void ProcessNotificationAction( DockingStationAction dsAction, InetUploader inetUploader )
        {
            // _lastNotificationError is the last CONSECUTIVE NotificationAction.
            // This the passed-in action is not a NotificationAction, then that
            // breaks the current series of consecutive NotificationActions.
            // So, we set it to null to denote that.
            if ( !( dsAction is INotificationAction ) )
            {
                _lastNotificationError = null;
                return;
            }

            const string funcMsg = "ProcessNotificationAction: ";

            StringBuilder errMsg = new StringBuilder( dsAction.Name );

            foreach ( string m in dsAction.Messages )
                errMsg.AppendFormat( "\r\n{0}", m );
  
            if ( dsAction is UnsupportedCylinderAction )
                errMsg.AppendFormat( "\r\non Port {0}", ( (UnsupportedCylinderAction)dsAction ).GasEndPoint.Position );

            // If it's an UnavailableAction, then the content of the error should
            // be the Exception's error (if there is an Exception).
            if ( ( dsAction is UnavailableAction ) && ( ( (UnavailableAction)dsAction ).Exception != null ) )
                errMsg.AppendFormat( "\r\n{0}", ( (UnavailableAction)dsAction ).Exception.ToString() );

            DockingStationError dsError;
            //Suresh 02-Feb-2012 INS-2392
            if (Master.SwitchService.Instrument == null)
                dsError = new DockingStationError(errMsg.ToString());
            else
                dsError = new DockingStationError(errMsg.ToString(), Master.SwitchService.Instrument.SerialNumber);

            // If this NotificationError's detail is the exact same as the last NotificationError's 
            // detail, then we assume it's a duplicate.  We don't want to upload duplicates.
            if ( _lastNotificationError != null && _lastNotificationError.Description == dsError.Description )
            {
                Log.Debug( string.Format( "{0}Ignoring duplicate: {1}", funcMsg, dsAction.ToString() ) );
                return;
            }

            _lastNotificationError = dsError;

            // We upload the error immediately (don't just queue it to our "Errors" List).
            if ( inetUploader != null )
            {
                Log.Debug( string.Format( "{0}Uploading ", funcMsg, dsAction.Name ) );
                inetUploader.UploadError( dsError, Configuration.DockingStation.TimeZoneInfo );
            }
            else
            {
                // if an uploader wasn't passed in to us to be re-used, then we need to create our own local one.
                using ( InetUploader localUploader = new InetUploader() )
                {
                    Log.Debug( string.Format( "{0}Uploading {1}", funcMsg, dsAction.Name ) );
                    localUploader.UploadError( dsError, Configuration.DockingStation.TimeZoneInfo );
                }
            }
        }

        internal void ReportQueuedErrors()
        {
            lock ( _errorsQueue )
            {
                if ( _errorsQueue.Count == 0 )
                    return;

                using ( InetUploader localUploader = new InetUploader() )
                {
                    ReportQueuedErrors( localUploader );
                }
            }
        }

        private void ReportQueuedErrors( InetUploader inetUploader )
        {
            lock ( _errorsQueue )
            {
                while ( _errorsQueue.Count > 0 )
                {
                    Log.Debug( "ReportQueuedErrors: uploading error" );

                    DockingStationError dsError = _errorsQueue.Peek();

                    Log.Debug( dsError.ToString() );

                    inetUploader.UploadError( dsError, Configuration.DockingStation.TimeZoneInfo );

                    _errorsQueue.Dequeue();
                }
                _errorsQueue.TrimExcess();
            }
        }

        /// <summary>
        /// Queues a docking station error to list.  The list is
        /// uploaded along with the next uploaded event.
        /// </summary>
        /// <param name="error">
        /// Error that is to be added to the list.
        /// </param>
        public void ReportError( DockingStationError error )
        {
            lock ( _errorsQueue )
            {
                // Check to see if its already been reported.
                // Don't report it if so.
                foreach ( DockingStationError err in _errorsQueue )
                {
                    if ( err.Description == error.Description )
                        return;
                }

                Log.Debug( string.Format( "Queuing error for upload to server: \"{0}\"", error.Description ) );

                _errorsQueue.Enqueue( error );
            }
        }

        /// <summary>
        /// Returns a RebootAction if the passed in event is IRebootableEvent
        /// and its RebootRequired is true.
        /// </summary>
        /// <param name="dsEvent"></param>
        /// <returns></returns>
        private DockingStationAction CheckRebootableEvent( DockingStationEvent dsEvent )
        {
            if ( !( dsEvent is IRebootableEvent ) )
                return null;

            if ( ( (IRebootableEvent)dsEvent ).RebootRequired )
            {
                Log.Info( string.Format( "\"{0}\" requires a reboot!", dsEvent ) );
                return new RebootAction();
            }

            Log.Info( string.Format( "\"{0}\" does not require a reboot.", dsEvent ) );
            return null;
        }

        private void ExchangeInetStatus( DockingStationEvent dsEvent )
        {
            if ( Configuration.DockingStation.SerialNumber == string.Empty ) // not serialized for some reason?
                return;

            // SGF  18-Feb-2013  INS-3824
            // In order to improve performance when processing a number of operations on instruments,
            // we will now limit the exchange status downloads so that they are performed as part of the 
            // heart beats.  This will prevent the repeated downloads that occur when several operations 
            // are performed in sequence after an instrument has been docked.
            if (!(dsEvent is InstrumentNothingEvent || dsEvent is NothingEvent))
            {
                Log.Debug(string.Format("\"{0}\" does not require an exchange status download.", dsEvent));
                return;
            }
            else
            {
                Log.Debug(string.Format("Exchange status download will be performed for \"{0}\".", dsEvent));
            }

            // Need to tell iNet what the current state of the docking station.
            string dsStatus = Master.Instance.ConsoleService.CurrentState.ToString();

            ExchangeStatusEvent exchangeStatusEvent = new ExchangeStatusOperation( dsStatus, true ).Execute() as ExchangeStatusEvent;

            // If the exchangeStatus operation encountered any errors with the data returned by iNet,
            // then make sure we inform iNet of those errors.
            foreach ( DockingStationError error in exchangeStatusEvent.Errors )
                ReportError( error );

            return;
        }

        #endregion

    }

}
