using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using Instrument_ = ISC.iNet.DS.DomainModel.Instrument;
using ISC.iNet.DS.iNet.InetUpload;
using ISC.WinCE.Logger;
using TimeZoneInfo = ISC.iNet.DS.DomainModel.TimeZoneInfo;


namespace ISC.iNet.DS.iNet
{
    public class InetUploader : Inet
    {
        private const string WSP_LOG_MESSAGE_HEADER = ">INET: ";

        private PersistedQueue _inetQueue;

        public InetUploader() : base()
        {
            Init();
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="config">Configuration settings needed to connect to iNet.  This parameter is cloned by the constructor.</param>
        /// <param name="schema">Schema settings needed to connect to iNet. This parameter is cloned by the constructor.</param>
        public InetUploader( DockingStation config, Schema schema ) : base( config, schema )
        {
            Init();
        }

        private void Init()
        {
            _inetQueue = PersistedQueue.CreateInetInstance();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout">Number of seconds</param>
        /// <returns></returns>
        private UploaderService GetWebService( int timeout )
        {
            if ( _webService == null ) // if we've not yet created it, then create it now.
                _webService = new UploaderService();

            SetCredentials( _webService, "/Uploader", timeout, _configDS );
            return (UploaderService)_webService;
        }

        /// <summary>
        /// Uploads to the passed-in event (and any errors it contains) to iNet.
        /// If the event can't be uploaded because server cannot be reached,
        /// then the event is, instead, queued to sqlite database for later upload.
        /// </summary>
        /// <param name="dsEvent"></param>
        /// <param name="tzi"></param>
        public void UploadEvent( DockingStationEvent dsEvent, TimeZoneInfo tzi )
        {
            if ( IgnoreUpload( dsEvent ) )
                return;

            List<QueueData> queueDataList = new List<QueueData>();

            long? scheduleRefId = null;
            if ( dsEvent.Schedule != null && dsEvent.Schedule.RefId != DomainModelConstant.NullId )
                scheduleRefId = dsEvent.Schedule.RefId;

            ThreadPriority prevPriority = Thread.CurrentThread.Priority;

            WebServiceSerializer wss = new WebServiceSerializer( tzi );

            try
            {
				if ( dsEvent is SettingsReadEvent )
					CreateQueueData( (SettingsReadEvent)dsEvent, queueDataList, wss );

				else if ( dsEvent is IDiagnosticEvent )
					CreateQueueData( (IDiagnosticEvent)dsEvent, queueDataList, wss, scheduleRefId );

				else if ( dsEvent is InstrumentSettingsReadEvent )
					CreateQueueData( (InstrumentSettingsReadEvent)dsEvent, queueDataList, wss );

				else if ( dsEvent is InstrumentSettingsUpdateEvent )
					CreateQueueData( (InstrumentSettingsUpdateEvent)dsEvent, queueDataList, wss );

				else if ( dsEvent is InstrumentCalibrationEvent )
					CreateQueueData( (InstrumentCalibrationEvent)dsEvent, queueDataList, wss, scheduleRefId );

				else if ( dsEvent is InstrumentBumpTestEvent )
					CreateQueueData( (InstrumentBumpTestEvent)dsEvent, queueDataList, wss, scheduleRefId );

				else if ( dsEvent is InstrumentAlarmEventsDownloadEvent )
					CreateQueueData( (InstrumentAlarmEventsDownloadEvent)dsEvent, queueDataList, wss, scheduleRefId );

				else if ( dsEvent is InstrumentDatalogDownloadEvent )
					CreateQueueData( (InstrumentDatalogDownloadEvent)dsEvent, queueDataList, wss, scheduleRefId );

				else if ( dsEvent is InstrumentManualOperationsDownloadEvent )
					CreateQueueData( (InstrumentManualOperationsDownloadEvent)dsEvent, queueDataList, wss, scheduleRefId );

				else if ( dsEvent is UploadDebugLogEvent )
					CreateQueueData( (UploadDebugLogEvent)dsEvent, queueDataList, wss, scheduleRefId );

				else if ( dsEvent is UploadDatabaseEvent )
					CreateQueueData( (UploadDatabaseEvent)dsEvent, queueDataList, wss, scheduleRefId );

                Exception lastExceptionEncountered = null;
                for ( int i = 0; i < queueDataList.Count; i++ )
                {
                    QueueData queueData = queueDataList[i];

					// Printing has been moved to the ReporterService so it will work in Cal Station mode.

                    try
                    {
                        UploadOrQueue( queueData );
                    }
                    catch ( Exception ex )
                    {
                        Log.Error( ">INET: UNABLE TO UPLOAD " + queueData.ToString(), ex );
                        // Don't rethrow (otherwise, we'd fail to upload anything else in the queuedatalist.
                        // But remember the exception for when we're finished.
                        lastExceptionEncountered = ex;
                    }
                    queueDataList[i] = null; // we're done with it, so release the memory ASAP. This especially matters for Datalog.
                }
                // If encountered any error when uploading, then exceptionEncountered
                // will be referencing the last error encountered.  Throw it.
                if ( lastExceptionEncountered != null )
                    throw lastExceptionEncountered;
            }
            finally
            {
                // Restore thread priority (it may or may not have been changed, depending on what was uploaded.)
                if ( Thread.CurrentThread.Priority != prevPriority )
                    Thread.CurrentThread.Priority = prevPriority;
            }

            // Finally, upload, individually, each error attached to the event.
            foreach ( DockingStationError error in dsEvent.Errors )
                UploadError( error, tzi );
        }

        private void CreateQueueData( SettingsReadEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss )
        {
            // The SettingsRead may only have cylinders that have changed, not *all* currently installed cylinders.
            // We can get those from the global Configuration.DockingStation.
            //
            // We clone the docking station before modifiying it since we need to modify it, but we 
            // don't know what if something else is going to use it after this upload operation completes.
            DockingStation dockingStation = (DockingStation)dsEvent.DockingStation.Clone();
            dockingStation.ChangedGasEndPoints.Clear(); // WebServiceSerializer is only interested in the Installed cylinders, not the Changed.
            dockingStation.GasEndPoints.Clear();

			// Make a copy of the GasEndPoints in the global DockingStation object.
            // These cylinders represent the currently installed cylinders and are what need uploaded to iNet.
            // Note that _configDS is a clone of Configuration.DockingStation.  We don't use
			// __configDS's GasEndPoints since they may not be up to date!
            foreach ( GasEndPoint gep in Configuration.DockingStation.GasEndPoints )
                dockingStation.GasEndPoints.Add( (GasEndPoint)gep.Clone() );

            bool postUpdate = ( (SettingsReadEvent)dsEvent ).PostUpdate;

            object dockingStationObject = wss.GetDOCKING_STATION( dockingStation, dsEvent.Time, postUpdate );
            LogWebServiceData( dockingStationObject );
            queueDataList.Add( new QueueData( _schema.AccountNum, dockingStationObject ) );
        }

        private void CreateQueueData( IDiagnosticEvent diagEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            DockingStationEvent dsEvent = (DockingStationEvent)diagEvent;

            // We need to make the upload time for each uploaded object unique, otherwise the 
            // upload server will refuse the data. To to do, we just take the time of event, 
            // and add one millisecond for each uploaded object session.
            DateTime timeStamp = dsEvent.Time;

            // Loop through and queue up the ErrorDiagnostics separately.
            foreach ( Diagnostic diagnostic in diagEvent.Diagnostics )
            {
                if ( diagnostic is ErrorDiagnostic )
                {
                    timeStamp = timeStamp.AddMilliseconds( 1 );
                    object errorObject = wss.GetERROR( (ErrorDiagnostic)diagnostic, timeStamp, (InstrumentEvent)diagEvent );
                    LogWebServiceData( errorObject );
                    queueDataList.Add( new QueueData( _schema.AccountNum, errorObject ) );
                }
            }

            // After pulling out the error diagnostic, send the others all together.
            object diagnosticObject = wss.GetDIAGNOSTIC( diagEvent, scheduleRefId );
            LogWebServiceData( diagnosticObject );
            queueDataList.Add( new QueueData( _schema.AccountNum, diagnosticObject ) );
        }

        private void CreateQueueData( InstrumentSettingsReadEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss )
        {
            // SGF  4-Oct-2012  INS-3314
            // Should we encounter an InstrumentSettingsReadEvent with a docked instrument that has no serial number,
            // log it and leave; otherwise, create an INSTRUMENT object and add it to the upload queue.
            // This portion of the bug fix is intended to be the method of dealing with an error condition
            // that we have not yet identified the cause.  As we encounter these situations, we will attempt to find 
            // causes and implement fixes.
            if ( dsEvent.DockedInstrument.SerialNumber.Length > 0 )
            {
				// Base units should be uploaded first before the instrument.  Other instrument data will reference the base units
				// so the base units must already exist in iNet.
				foreach ( BaseUnit baseUnit in dsEvent.DockedInstrument.BaseUnits )
				{
					object baseUnitObject = wss.GetACCESSORY( baseUnit, dsEvent.DockedInstrument.SerialNumber, dsEvent.DockingStation.SerialNumber );
					LogWebServiceData( baseUnitObject );
					queueDataList.Add( new QueueData( _schema.AccountNum, baseUnitObject ) );
				}
								
                object instObject = wss.GetINSTRUMENT( dsEvent.DockedInstrument, dsEvent.DockingStation.SerialNumber, dsEvent.DockedTime, dsEvent.Time );
                LogWebServiceData( instObject );
                queueDataList.Add(new QueueData(_schema.AccountNum, instObject));
                return;
            }

            // Log an error to the docking station's log.  Also, since no INSTRUMENT is queued, 
            // two errors will be uploaded to iNet (unserialized instrument AND instrument has no installed errors).
            Log.Error(">INET: UNABLE TO UPLOAD EMPTY INSTRUMENT!");
        }

		private void CreateQueueData( InstrumentSettingsUpdateEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss )
		{
			// INS-3314
			// Should we encounter an InstrumentSettingsUpdateEvent with a docked instrument that has no serial number,
			// log it and leave; otherwise, create an INSTRUMENT object and add it to the upload queue.
			// This portion of the bug fix is intended to be the method of dealing with an error condition
			// that we have not yet identified the cause.  As we encounter these situations, we will attempt to find 
			// causes and implement fixes.
			if ( dsEvent.DockedInstrument.SerialNumber.Length > 0 )
			{
				object instObject = wss.GetINSTRUMENT( dsEvent.DockedInstrument, dsEvent.DockingStation.SerialNumber, dsEvent.DockedTime, dsEvent.Time );
				LogWebServiceData( instObject );
				queueDataList.Add( new QueueData( _schema.AccountNum, instObject ) );
				return;
			}

			// Log an error to the docking station's log.  Also, since no INSTRUMENT is queued, 
			// two errors will be uploaded to iNet (unserialized instrument AND instrument has no installed errors).
			Log.Error( ">INET: UNABLE TO UPLOAD EMPTY INSTRUMENT!" );
		}

        private void CreateQueueData( InstrumentCalibrationEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            // SGF:  DS2 (EventReceiver.Process) -- sensor gas response information was persisted to the database here
            // SGF:  DS2 (EventReceiver.Process) -- cylinder volumes were decremented here
            object instCalObject = wss.GetINSTRUMENT_CALIBRATION( dsEvent, scheduleRefId );

            Dictionary<string, string> properties = new Dictionary<string, string>();

            Instrument_ instrument = dsEvent.DockedInstrument;

            // Add instrument properties
            properties.Add( "PARTNUMBER", instrument.PartNumber );
            properties.Add( "JOBNUMBER", instrument.JobNumber );
            properties.Add( "SETUPDATE", instrument.SetupDate.ToShortDateString() );
            properties.Add( "SETUPTECH", instrument.SetupTech );

            string battery = string.Empty;
            foreach ( InstalledComponent ic in instrument.InstalledComponents )
            {
                if ( ic.Component is Battery )
                {
                    battery = ic.Component.ToString();
                    break;
                }
            }
            properties.Add( "BATTERY", battery );

            // Add sensor properties
            foreach ( InstalledComponent ic in instrument.InstalledComponents )
            {
                if ( ic.Component is Sensor )
                {
                    Sensor sensor = ic.Component as Sensor;

                    // Add Alarm High, Alarm Low, Alarm TWA, and Alarm STEL properties for the current sensor
                    properties.Add( sensor.Uid + "_ALARMHIGH", sensor.Alarm.High.ToString() );
                    properties.Add( sensor.Uid + "_ALARMLOW", sensor.Alarm.Low.ToString() );
                    if ( sensor.Alarm.TWA >= 0.0 )
                        properties.Add( sensor.Uid + "_ALARMTWA", sensor.Alarm.TWA.ToString() );
                    if ( sensor.Alarm.STEL >= 0.0 )
                        properties.Add( sensor.Uid + "_ALARMSTEL", sensor.Alarm.STEL.ToString() );

                    // Add CylinderExp property for the current sensor gas response
                    string cylinderExp = string.Empty;
                    string zeroCylinderExp = string.Empty; // SGF  19-Jan-2010  INS-1204
                    foreach ( SensorGasResponse sgr in dsEvent.GasResponses )
                    {
                        if ( sensor.Uid == sgr.Uid )
                        {
                            // SGF  19-Jan-2010  INS-1204
                            //if (sgr.CylinderExpiration.Year < DateTime.MaxValue.Year )
                            //    cylinderExp = sgr.CylinderExpiration.ToShortDateString();
                            foreach ( UsedGasEndPoint used in sgr.UsedGasEndPoints )
                            {
                                if ( used.Usage == CylinderUsage.Calibration )
                                {
                                    if ( used.Cylinder.ExpirationDate.Year < DateTime.MaxValue.Year )
                                        cylinderExp = used.Cylinder.ExpirationDate.ToShortDateString();
                                }
                                else if ( used.Usage == CylinderUsage.Zero )
                                {
                                    if ( used.Cylinder.ExpirationDate.Year < DateTime.MaxValue.Year )
                                        zeroCylinderExp = used.Cylinder.ExpirationDate.ToShortDateString();
                                }
                            }
                            break;
                        }
                    }
                    if ( cylinderExp.Length > 0 )
                        properties.Add( sensor.Uid + "_CYLINDEREXP", cylinderExp );
                    // SGF  19-Jan-2010  INS-1204
                    if ( zeroCylinderExp.Length > 0 )
                        properties.Add( sensor.Uid + "_ZEROCYLINDEREXP", zeroCylinderExp );
                }
            }
            LogWebServiceData( instCalObject );
            queueDataList.Add( new QueueData( _schema.AccountNum, instCalObject, properties ) );
        }

        private void CreateQueueData( InstrumentBumpTestEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            // SGF:  DS2 (EventReceiver.Process) -- sensor gas response information was persisted to the database here
            // SGF:  DS2 (EventReceiver.Process) -- cylinder volumes were decremented here
            object instBumpObject = wss.GetINSTRUMENT_BUMP_TEST( dsEvent, scheduleRefId );

            Dictionary<string, string> properties = new Dictionary<string, string>();

            Instrument_ instrument = dsEvent.DockedInstrument;

            // Add instrument properties
            properties.Add( "PARTNUMBER", instrument.PartNumber );
            properties.Add( "JOBNUMBER", instrument.JobNumber );
            properties.Add( "SETUPDATE", instrument.SetupDate.ToShortDateString() );
            properties.Add( "SETUPTECH", instrument.SetupTech );

            string battery = string.Empty;
            foreach ( InstalledComponent ic in instrument.InstalledComponents )
            {
                if ( ic.Component is Battery )
                {
                    battery = ic.Component.ToString();
                    break;
                }
            }
            properties.Add( "BATTERY", battery );

            properties.Add( "BUMPTHRESHOLD", instrument.BumpThreshold.ToString() );
            properties.Add( "BUMPTIMEOUT", instrument.BumpTimeout.ToString() );
            properties.Add( "PUMPACCESSORY", instrument.AccessoryPump.ToString() );

            // Add sensor properties
            foreach ( InstalledComponent ic in instrument.InstalledComponents )
            {
                if ( ic.Component is Sensor )
                {
                    Sensor sensor = ic.Component as Sensor;

                    // Add Alarm High, Alarm Low, Alarm TWA, and Alarm STEL properties for the current sensor
                    properties.Add( sensor.Uid + "_ALARMHIGH", sensor.Alarm.High.ToString() );
                    properties.Add( sensor.Uid + "_ALARMLOW", sensor.Alarm.Low.ToString() );
                    if ( sensor.Alarm.TWA >= 0.0 )
                        properties.Add( sensor.Uid + "_ALARMTWA", sensor.Alarm.TWA.ToString() );
                    if ( sensor.Alarm.STEL >= 0.0 )
                        properties.Add( sensor.Uid + "_ALARMSTEL", sensor.Alarm.STEL.ToString() );

                    // Add CylinderExp property for the current sensor gas response
                    string cylinderExp = string.Empty;
                    foreach ( SensorGasResponse sgr in dsEvent.GasResponses )
                    {
                        if ( sensor.Uid == sgr.Uid )
                        {
                            // SGF  19-Jan-2011  INS-1204
                            //if (sgr.CylinderExpiration.Year < DateTime.MaxValue.Year)
                            //    cylinderExp = sgr.CylinderExpiration.ToShortDateString();
                            foreach ( UsedGasEndPoint used in sgr.UsedGasEndPoints )
                            {
                                if ( used.Usage == CylinderUsage.Bump )
                                {
                                    if ( used.Cylinder.ExpirationDate.Year < DateTime.MaxValue.Year )
                                        cylinderExp = used.Cylinder.ExpirationDate.ToShortDateString();
                                }
                            }
                            break;
                        }
                    }
                    if ( cylinderExp.Length > 0 )
                        properties.Add( sensor.Uid + "_CYLINDEREXP", cylinderExp );
                }
            }
            LogWebServiceData( instBumpObject );
            queueDataList.Add( new QueueData( _schema.AccountNum, instBumpObject, properties ) );

            // In case of any O2 High Bump failure, calibration is performed and the gas responses
            // are stored in HighBumpFailCalGasResponses. If any such gas responses exists, upload them to iNet.
            if (dsEvent.HasHighBumpFailCalGasResponses)
            {
                // Reusing the properties dictionary. Clearing any existing key values and adding what's needed for calibration event.
                properties.Clear();
                properties.Add("PARTNUMBER", instrument.PartNumber);
                properties.Add("JOBNUMBER", instrument.JobNumber);
                properties.Add("SETUPDATE", instrument.SetupDate.ToShortDateString());
                properties.Add("SETUPTECH", instrument.SetupTech);
                properties.Add("BATTERY", battery);

                object instCalObject = wss.GetINSTRUMENT_CALIBRATION(dsEvent, null);

                // Add sensor properties
                foreach (InstalledComponent ic in instrument.InstalledComponents.FindAll(c => c.Component is Sensor))
                {
                    Sensor sensor = ic.Component as Sensor;

                    // Add Alarm High, Alarm Low, Alarm TWA and Alarm STEL properties for the current sensor
                    // No check for cylinder expiration date is required as calibration only uses Fresh Air which doesn't have an expiration date.
                    properties.Add(sensor.Uid + "_ALARMHIGH", sensor.Alarm.High.ToString());
                    properties.Add(sensor.Uid + "_ALARMLOW", sensor.Alarm.Low.ToString());
                    if (sensor.Alarm.TWA >= 0.0)
                        properties.Add(sensor.Uid + "_ALARMTWA", sensor.Alarm.TWA.ToString());
                    if (sensor.Alarm.STEL >= 0.0)
                        properties.Add(sensor.Uid + "_ALARMSTEL", sensor.Alarm.STEL.ToString());

                    // We only use gas cylinder for calibrating oxygen sensor
                    // If sensor is not O2, skip populating the cylinder expiration date information.
                    if (sensor.Type.Code != SensorCode.O2) continue;

                    // Add CylinderExp property for the current sensor gas response
                    string cylinderExp = string.Empty;
                    string zeroCylinderExp = string.Empty;
                    foreach (SensorGasResponse sgr in dsEvent.HighBumpFailCalGasResponses)
                    {
                        if (sensor.Uid == sgr.Uid)
                        {
                            foreach (UsedGasEndPoint used in sgr.UsedGasEndPoints)
                            {
                                if (used.Usage == CylinderUsage.Calibration)
                                {
                                    if (used.Cylinder.ExpirationDate.Year < DateTime.MaxValue.Year)
                                        cylinderExp = used.Cylinder.ExpirationDate.ToShortDateString();
                                }
                                else if (used.Usage == CylinderUsage.Zero)
                                {
                                    if (used.Cylinder.ExpirationDate.Year < DateTime.MaxValue.Year)
                                        zeroCylinderExp = used.Cylinder.ExpirationDate.ToShortDateString();
                                }
                            }
                            break;
                        }
                    }
                    if (cylinderExp.Length > 0)
                        properties.Add(sensor.Uid + "_CYLINDEREXP", cylinderExp);

                    if (zeroCylinderExp.Length > 0)
                        properties.Add(sensor.Uid + "_ZEROCYLINDEREXP", zeroCylinderExp);
                }
                LogWebServiceData(instCalObject);
                queueDataList.Add(new QueueData(_schema.AccountNum, instCalObject, properties));
            }
        }

        private void CreateQueueData( InstrumentAlarmEventsDownloadEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            // We need to make the upload time for each uploaded object unique, otherwise the 
            // upload server will refuse the data. To to do, we just take the time of event, 
            // and add one millisecond for each uploaded object session.
            DateTime timeStamp = dsEvent.Time;

            foreach ( AlarmEvent alarmEvent in dsEvent.AlarmEvents )
            {
                timeStamp = timeStamp.AddMilliseconds( 1 );

                ALARM_EVENT alarmEventObject = wss.GetALARM_EVENT( alarmEvent, timeStamp, scheduleRefId );
                LogWebServiceData( alarmEventObject );

                queueDataList.Add( new QueueData( _schema.AccountNum, alarmEventObject ) );

                // For GBPLS instruments, we need to ensure that a 'quasi' 
                // datalog session is uploaded for each alarm event.
                if ( _configDS.Type == DeviceType.GBPLS )
                {
                    timeStamp = timeStamp.AddMilliseconds( 1 );
                    DATALOG_SESSION sessionObject = wss.GetGbPlusDATALOG_SESSION( alarmEvent, timeStamp );
                    queueDataList.Add( new QueueData( _schema.AccountNum, sessionObject ) );
                }
            }
        }

        private void CreateQueueData( InstrumentDatalogDownloadEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            // Large sessions can take a LONG time to uploaded or place onto the upload queue. Put the thread to a
            // lower priority (if not already) to ensure other threads aren't starved out during the upload.
            if ( dsEvent.InstrumentSessions.Count > 0 && Thread.CurrentThread.Priority > ThreadPriority.BelowNormal )
                Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            DateTime timeStamp = dsEvent.Time;
            for ( int i = 0; i < dsEvent.InstrumentSessions.Count; i++ )
            {
                // We need to make the upload time for each session unique, otherwise the upload
                // server will refuse the data. To to do, we just take the time of event, and add
                // one millisecond for each datalog session.
                timeStamp = timeStamp.AddMilliseconds( i );

                DATALOG_SESSION sessionObject = wss.GetDATALOG_SESSION( dsEvent.InstrumentSessions[i], timeStamp, scheduleRefId, null );
                LogWebServiceData( sessionObject );

                queueDataList.Add( new QueueData( _schema.AccountNum, sessionObject ) );

                dsEvent.InstrumentSessions[i] = null; // we're done with it, so release the session's memory ASAP.
            }
            return;
        }

        private void CreateQueueData( InstrumentManualOperationsDownloadEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            foreach ( object gasOperation in wss.GetGasOperations( dsEvent, scheduleRefId ) )
                queueDataList.Add( new QueueData( _schema.AccountNum, gasOperation ) );
        }

        private void CreateQueueData( UploadDebugLogEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            // Large debug logs can take a LONG time to uploaded or place onto the upload queue. Put the thread to a
            // lower priority (if not already) to ensure other threads aren't starved out during the upload.
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            DEBUG_LOG debugLogObject = wss.GetDEBUG_LOG( dsEvent.LogText, _configDS.SerialNumber, scheduleRefId, dsEvent.Time );
            LogWebServiceData( debugLogObject );

            queueDataList.Add( new QueueData( _schema.AccountNum, debugLogObject ) );
        }

        private void CreateQueueData( UploadDatabaseEvent dsEvent, List<QueueData> queueDataList, WebServiceSerializer wss, long? scheduleRefId )
        {
            // Large databases can take a long time to uploaded or place onto the upload queue. Put the thread to a
            // lower priority (if not already) to ensure other threads aren't starved out during the upload.
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            DATABASE_UPLOAD dbUploadObject = wss.GetDATABASE_UPLOAD( dsEvent.File, dsEvent.FileName, _configDS.SerialNumber, scheduleRefId, dsEvent.Time );
            LogWebServiceData( dbUploadObject );

            queueDataList.Add( new QueueData( _schema.AccountNum, dbUploadObject ) );
        }

        /// <summary>
        /// Serialize passed in object and log its size.
        /// </summary>
        /// <remarks>
        /// This method is intended for debugging/development purposes only.
        /// <para>
        /// Sometimes, we want to know the size of the data we're uploading to iNet.
        /// This method takes the passed-in object, serializes to a string, then logs
        /// the size of the string.
        /// </para>
        /// </remarks>
        /// <param name="o"></param>
        private void LogWebServiceData( object o )
        {
#if INET_WS_SERIALIZE
            string oTypeName = o.GetType().ToString();
            oTypeName = oTypeName.Substring( oTypeName.LastIndexOf( '.' ) + 1 );

            // Enable this call to serialize the object to a string of XML, to determine how long it takes to serialize it, and how big the XML is.
            //Log.Debug( "<INET: Serializing upload object to determine size..." ); // we log this since datalog can take a while to serialize
            double kB = (double)SerializeWebServiceData( o ).Length / 1024.0d; // convert to kilobytes
            Log.Debug( string.Format( ">INET: Serialized {0} size: {1} KB", oTypeName, kB.ToString( "f01" ) ) );

            // Enable this call to serialize the object to XML and saves the string to the SD card.
            // The amount of time the method takes includes both serializing the XML, and saving to the SD Card.
            //SerializeWebServiceParameterToFile( o );
#endif
        }

        public void UploadError( DockingStationError error, TimeZoneInfo tzi )
        {
            if ( IgnoreUpload( error ) )
                return;

            WebServiceSerializer wss = new WebServiceSerializer( tzi );
            object wsParameter = wss.GetERROR( error, _configDS, DateTime.UtcNow );
            QueueData queueData = new QueueData( _schema.AccountNum, wsParameter );

            UploadOrQueue( queueData );
        }

		internal void EnqueueError( DockingStationError error )
		{
			if ( IgnoreUpload( error ) )
			{
				Log.Debug( ">INET: Ignoring enqueue of error. No active account. No Service Mode." );
				return;
			}

			WebServiceSerializer wss = new WebServiceSerializer( Configuration.DockingStation.TimeZoneInfo );
			object wsParameter = wss.GetERROR( error, _configDS, DateTime.UtcNow );
			QueueData queueData = new QueueData( _schema.AccountNum, wsParameter );

			// Note that we do NOT call queue.GetCount() since that can be extremely slow if queue is large.
			if ( _inetQueue.IsEmpty() )
			{
				Log.Debug(  string.Format( ">INET: Queue is currently empty. Queueing new {0}.", queueData.Label ) );
			}
			else
			{
				Log.Debug( string.Format( ">INET: Queue already contains uploads. Queueing new {0} behind them.", queueData.Label ) );
			}
			_inetQueue.Enqueue( queueData );
		}

		private DockingStationError CreateFailedUploadDockingStationError( UploaderWebMethod webMethod, Exception ex )
		{
			return CreateFailedInetDockingStationError( InetService.UploaderService, webMethod.ToString(), ex ); 
		}

        /// <summary>
        /// Returns false if...
        /// <para>
        /// 1) we have no account number, 
        /// </para>
        /// <para>
        /// 2) or we're not serialized, 
        /// </para>
        /// <para>
        /// 3) or we're in cal station mode and not service mode.
        /// </para>
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
		private bool IgnoreUpload( object o )
		{
            if ( o == null ) // this probably never happens, but it's just a safety net, just in case.
            {
                Log.Debug( string.Format( ">INET: Ignoring upload of null object." ) );
                return true;
            }

			// We don't want to ever upload if we're not associated to any account.
            if ( _schema.AccountNum == string.Empty )
            {
                Log.Debug( string.Format( ">INET: Ignoring upload. No active account." ) );
                return true;
            }

			// We can't upload if we don't have a serial number to identify ourselves when we do the upload.
			// This shouldn't happen if we have an account number, but we'll check for sanity anyways.
            if ( _configDS.SerialNumber == string.Empty )
            {
                Log.Debug( string.Format( ">INET: Ignoring upload. No serial number." ) );
                return true;
            }

			// Even if we DO have an account number, we don't want to upload if we're not activated on iNet.
			// Yet we WILL upload if we're in Service mode, regardless if activated or not.
			// Confusing, eh?
            if ( !_schema.Activated && !Configuration.ServiceMode )
            {
                Log.Debug( string.Format( ">INET: Ignoring upload. No active account. No Service Mode." ) );
                return true;
            }

			return false;
		}

        /// <summary>
        /// Attempts to upload passed-in data directly to iNet.
        /// <para>
        /// If upload queue is already backed-up, then data is queued instead of uploaded.
        /// </para>
        /// If upload attempt fails, then data is queued for later.
        /// <para>
        /// If not currently activated on iNet, then this routine will only
        /// attempt a direct upload and will *never* queue data.
        /// </para>
        /// </summary>
        /// <param name="queueData"></param>
        private void UploadOrQueue( QueueData queueData )
        {
            if ( queueData == null )
                return;

           // Note that we do NOT call queue.GetCount() since that can be extremely slow if queue is large.
           bool isEmpty = _inetQueue.IsEmpty();

            // If the queue isn't empty, then just queue our data.  We want
            // whatever is already queued to be uploaded first.
           if ( !isEmpty )
            {
                Log.Debug( string.Format( ">INET: Queue already contains uploads. Queueing new {0} behind them.", queueData ) );

                _inetQueue.Enqueue( queueData );

                return;
            }
            else
                Log.Debug( ">INET: Queue is empty." );

            // Otherwise, if the queue is empty, then try and immediately 
            // upload to iNet.  If we can't upload immediately, we'll instead queue it.

            // No use trying to upload if we know upfront that we're not currently network connected.
            if ( !Controller.GetWiredNetworkAdapter().IsNetworked() )
            {
                Log.Debug( ">INET: Not connected to iNet. Queueing upload for later" );
                _inetQueue.Enqueue( queueData );
                return;
            }

            // Try to upload.  If we can't, then queue for later attempt.

            // for debugging uncomment this line, and comment out the call to Upload
            // to temporarily disable successful uploading to iNet....
            //string uploadError = "UploadingDisabled";
            string uploadError = Upload( queueData.WebServiceParameter, queueData.Label, queueData.InetAccountNum );

            if ( ( uploadError != string.Empty ) && _schema.Activated )
            {
                Log.Debug( string.Format( ">INET: Queuing failed {0} upload in order to retry later.", queueData ) );
                _inetQueue.Enqueue( queueData );
            }
        }

        /// <summary>
        /// Used during manufacturing serialization to force an immediate upload of the docking station.
        /// </summary>
        /// <param name="dockingStation"></param>
        /// <param name="accountNum"></param>
        /// <returns>empty string if successfully uploaded; else an error string.</returns>
        public string UploadDockingStation( DockingStation dockingStation, DateTime time, string accountNum, TimeZoneInfo tzi )
        {
            Log.Assert( Configuration.ServiceMode == true, "UploadDockingStation is intended to only be called when in Service Mode" );

            // No use trying to upload if we know upfront that we're not currently network connected.
            if ( !Controller.GetWiredNetworkAdapter().IsNetworked() )
                return "No network available";

            DOCKING_STATION ds = new WebServiceSerializer( tzi ).GetDOCKING_STATION( dockingStation, time, false );
            ds.accountId = accountNum;
            return Upload( ds, "DOCKING_STATION", accountNum );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="webServiceParamObject"></param>
        /// <param name="label"></param>
        /// <param name="accountNum"></param>
        /// <returns>
        /// An empty string is returned if data is successfully uploaded;
        /// otherwise an error string is returned.
        /// <para>
        /// This method should not throw any exceptions on an upload failure.
        /// </para>
        /// </returns>
        public string Upload( object webServiceParamObject, string label, string accountNum )
        {
            if ( webServiceParamObject == null )  // this should never happen except by developer error.
            {
                Log.Debug( ">INET: wsParmObject was NULL" );
                return string.Empty;
            }

			WatchDog watchDog = null;

            int wsReturnCode = 0;
            string returnError = string.Empty;
            string accountId = string.Empty;
			UploaderWebMethod webMethod = UploaderWebMethod.Unknown;

            Log.Debug( string.Format( ">INET: Uploading {0} to account {1}...", label, accountNum ) );

            Stopwatch stopwatch = new Stopwatch();

			try
			{
				if ( webServiceParamObject is DOCKING_STATION )
				{
					webMethod = UploaderWebMethod.uploadDockingStation;
					DOCKING_STATION ds = (DOCKING_STATION)webServiceParamObject;
					ds.accountId = accountNum;
					WebServiceLog.LogDOCKING_STATION( ds, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadDockingStation( ds );
				}
				else if ( webServiceParamObject is INSTRUMENT )
				{
					webMethod = UploaderWebMethod.uploadInstrument;
					INSTRUMENT inst = (INSTRUMENT)webServiceParamObject;
					inst.accountId = accountNum;
					WebServiceLog.LogINSTRUMENT( inst, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadInstrument( inst );
				}
				else if ( webServiceParamObject is INSTRUMENT_CALIBRATION )
				{
					webMethod = UploaderWebMethod.uploadCalibration;
					INSTRUMENT_CALIBRATION cal = (INSTRUMENT_CALIBRATION)webServiceParamObject;
					cal.accountId = accountNum;
					WebServiceLog.LogINSTRUMENT_CALIBRATION( cal, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadCalibration( cal );
				}
				else if ( webServiceParamObject is INSTRUMENT_BUMP_TEST )
				{
					webMethod = UploaderWebMethod.uploadBumpTest;
					INSTRUMENT_BUMP_TEST bump = (INSTRUMENT_BUMP_TEST)webServiceParamObject;
					bump.accountId = accountNum;
					WebServiceLog.LogINSTRUMENT_BUMP_TEST( bump, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadBumpTest( bump );
				}
				else if ( webServiceParamObject is DIAGNOSTIC )
				{
					webMethod = UploaderWebMethod.uploadDiagnostic;
					DIAGNOSTIC diag = (DIAGNOSTIC)webServiceParamObject;
					diag.accountId = accountNum;
					WebServiceLog.LogDIAGNOSTIC( diag, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadDiagnostic( diag );
				}
				else if ( webServiceParamObject is ERROR )
				{
					webMethod = UploaderWebMethod.uploadError;
					ERROR error = (ERROR)webServiceParamObject;
					error.accountId = accountNum;
					WebServiceLog.LogERROR( error, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadError( error );
				}
				else if ( webServiceParamObject is DATALOG_SESSION )
				{
					webMethod = UploaderWebMethod.uploadDataLogging;
					DATALOG_SESSION session = (DATALOG_SESSION)webServiceParamObject;
					session.accountId = accountNum;
					WebServiceLog.LogDATALOG_SESSION( session, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutHigh;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadDataLogging( session );
				}
				else if ( webServiceParamObject is ALARM_EVENT )
				{
					webMethod = UploaderWebMethod.uploadAlarmEvent;
					ALARM_EVENT alarmEvent = (ALARM_EVENT)webServiceParamObject;
					alarmEvent.accountId = accountNum;
					WebServiceLog.LogALARMEVENT( alarmEvent, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadAlarmEvent( alarmEvent );
				}
				else if ( webServiceParamObject is ACCESSORY )
				{
					webMethod = UploaderWebMethod.uploadAccessory;
					ACCESSORY accessory = (ACCESSORY)webServiceParamObject;
					accessory.accountId = accountNum;
					WebServiceLog.LogACCESSORY( accessory, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutLow;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadAccessory( accessory );
				}
				else if ( webServiceParamObject is DEBUG_LOG )
				{
					webMethod = UploaderWebMethod.uploadDebugLog;
					DEBUG_LOG debugLog = (DEBUG_LOG)webServiceParamObject;
					debugLog.accountId = accountNum;
					WebServiceLog.LogDEBUG_LOG( debugLog, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutHigh;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadDebugLog( debugLog );
				}
				else if ( webServiceParamObject is DATABASE_UPLOAD )
				{
					webMethod = UploaderWebMethod.uploadDockingStationDatabase;
					DATABASE_UPLOAD dbUpload = (DATABASE_UPLOAD)webServiceParamObject;
					dbUpload.accountId = accountNum;
					WebServiceLog.LogDATABASE_UPLOAD( dbUpload, WSP_LOG_MESSAGE_HEADER );

					// start watchdog
					int timeout = _configDS.InetTimeoutHigh;
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					stopwatch.Start();
					wsReturnCode = (int)GetWebService( timeout ).uploadDockingStationDatabase( dbUpload );
				}
				else
				{
					Log.Warning( ">INET: webServiceParamObject is not a known type: " + webServiceParamObject.GetType().ToString() );
					return string.Empty; // success
				}
				stopwatch.Stop();
			}
			// The web services are calling SoapHttpClientProtocol.Invoke.  MSDN says this
			// method will throw a SoapException, but it actually seems to throw WebExceptions.
			// So, we'll just check for both...
			catch ( Exception ex )
			{
				stopwatch.Stop();

				// only create a new upload error if upload is online
				if ( Inet.IsUploadOnline )
				{
					DockingStationError dsError = CreateFailedUploadDockingStationError( webMethod, ex );
					EnqueueError( dsError );
				}

				returnError = HandleInetWebServiceException( ex );
			}
			finally
			{
				// stop watchdog
				if ( watchDog != null )
				{
					watchDog.Stop();
					watchDog.Close();
				}
			}

            if ( wsReturnCode != 0 )
                returnError = wsReturnCode.ToString();

            if ( returnError != string.Empty )
			{
				Inet.IsUploadOnline = false;
                Log.Warning( string.Format( ">INET: UPLOAD of {0} FAILED after {1} seconds, AccountNum={2}, Error={3}", label, stopwatch.ElapsedMilliseconds / 1000.0, accountNum, returnError ) );
			}
            else
			{
				Inet.IsUploadOnline = true;
                Log.Debug( string.Format( ">INET: UPLOAD of {0} SUCCEEDED in {1} seconds, AccountNum={2}", label, stopwatch.ElapsedMilliseconds / 1000.0, accountNum ) );
			}

            return returnError;
        }

		// enum names should match web method names from the UploaderService
		private enum UploaderWebMethod
		{
			Unknown,
			uploadDockingStation,
			uploadInstrument,
			uploadCalibration,
			uploadBumpTest,
			uploadDiagnostic,
			uploadError,
			uploadDataLogging,
			uploadAlarmEvent,
			uploadDebugLog,
			uploadDockingStationDatabase,
			uploadAccessory
		}
    }
}
