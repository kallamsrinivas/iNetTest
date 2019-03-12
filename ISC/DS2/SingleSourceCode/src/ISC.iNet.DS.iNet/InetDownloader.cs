using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using Instrument_ = ISC.iNet.DS.DomainModel.Instrument;
using ISC.iNet.DS.iNet.InetConfiguration;
using ISC.WinCE;
using ISC.WinCE.Logger;
using System.Text;
using ISC.iNet.DS.DataAccess;
using TimeZoneInfo = ISC.iNet.DS.DomainModel.TimeZoneInfo;


namespace ISC.iNet.DS.iNet
{
    public class InetDownloader : Inet
    {
        // SGF  27-Oct-2010  DSW-381 (DS2 v8.0)  INS-1622
        const string ATTR_ALLOWBUMPAFTERCAL = "ALLOWBUMPAFTERCAL";
        const string firmwareFolderPath = Controller.FLASHCARD_PATH + "\\" + "Firmware";

        public InetDownloader() : base() {}

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="config">Configuration settings needed to connect to iNet.  This parameter is cloned by the constructor.</param>
        /// <param name="schema">Schema settings needed to connect to iNet. This parameter is cloned by the constructor.</param>
        public InetDownloader( DockingStation config, Schema schema ) : base( config, schema ) {}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timeout">Number of seconds</param>
        /// <returns></returns>
        private ConfigurationService GetWebService( int timeout )
        {
            if ( _webService == null ) // if we've not yet created it, then create it now.
                _webService = new ConfigurationService();

            SetCredentials( _webService, "/Configuration", timeout, _configDS );
            return (ConfigurationService)_webService;
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
        private void LogWebServiceData(object o)
        {
#if INET_WS_SERIALIZE
            string oTypeName = o.GetType().ToString();
            oTypeName = oTypeName.Substring( oTypeName.LastIndexOf( '.' ) + 1 );
            //Log.Debug( "<INET: Serializing upload object to determine size..." ); // we log this since datalog can take a while to serialize
            double kB = (double)SerializeWebServiceData( o ).Length / 1024.0d; // convert to kilobytes
            Log.Debug(string.Format("<INET: ...{0} size: {1} KB", oTypeName, kB.ToString("f01")));
#endif
        }

		/// <summary>
		/// Calls iNet's exchangeStatusInformation web service.
		/// </summary>
		/// <param name="dockingStationStatus">
		/// String describing the current state or operaton of the docking station.
		/// This will typically by the ConsoleService's current ConsoleState.
		/// </param>
		/// <param name="instrumentSn">
		/// Serial number of currently docked instrument.  Else, empty string.
		/// </param>
		/// <param name="nextCalDate">The date/time the instruent is to be calibrated next.  Will be null if not currently known.</param>
		/// <param name="nextBumpDate">The date/time the instruent is to be bump tested next.  Will be null if not currently known.</param>
		/// <param name="returnVersions">true if web service should returned "version dates".  false if not.</param>
		/// <param name="isExchangeStatusOperation">True if web service was called by the ExchangeStatusOperation.</param>
		/// <returns>
		/// Guaranteed to return a valid InetStatus instance.
		/// Caller should check InetStatus.Error for success/failure.</returns>
		public InetStatus ExchangeStatus( string dockingStationStatus, string instrumentSn, DateTime? nextCalDate, DateTime? nextBumpDate, bool returnVersions, bool isExchangeStatusOperation )
		{
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutLow;

			bool isDownloadOnline = false;
			
			try
			{
				Log.Debug( string.Format( "<INET: Calling exchangeStatusInformation, a=\"{0}\",sn=\"{1}\",isn=\"{2}\",nc={3},nb={4},op=\"{5}\",rv={6}", _schema.AccountNum, _configDS.SerialNumber, instrumentSn, nextCalDate, nextBumpDate, dockingStationStatus, returnVersions ) );

				// start watchdog
				watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
				watchDog.Start();
                
				Stopwatch s = new Stopwatch(); s.Start();

				STATUS_INFORMATION statusInfo
						= GetWebService( timeout ).exchangeStatusInformation( _schema.AccountNum, _configDS.SerialNumber, instrumentSn, dockingStationStatus, nextCalDate, nextBumpDate, returnVersions );
				Log.Debug( string.Format( "<INET: exchangeStatusInformation call completed in {0} seconds.", s.ElapsedMilliseconds / 1000.0 ) );

				if ( statusInfo == null )
				{
					string error = "STATUS_INFO=null";
					Log.Debug( error );

					return new InetStatus( error );
				}
				else
				{
					// call to iNet was successful
					isDownloadOnline = true;
				}

				InetStatus inetStatus = new InetStatus();

				// Note that DateTimes seem to always be returned in UTC.  We don't bother converting
				// the various "utcVersion" dates to localtime; we will store them in UTC.  The Data Access
				// layer knows we're doing this this for the Schema.

				LogWebServiceData( statusInfo );
				if ( returnVersions )
				{
					Log.Debug( string.Format( "<INET:STATUS_INFO.acc={0}, act={1}, time=\"{2}\"", statusInfo.accountId, statusInfo.activated, Log.DateTimeToString( statusInfo.currentTime ) ) );
					Log.Debug( string.Format( "<INET:STATUS_INFO.cV=\"{0}\",ceV=\"{1}\",scV=\"{2}\",seV=\"{3}\",eV=\"{4}\"",
						Log.DateTimeToString( statusInfo.cylindersVersion ), Log.DateTimeToString( statusInfo.criticalErrorsVersion ),
						Log.DateTimeToString( statusInfo.schedulesVersion ), Log.DateTimeToString( statusInfo.settingsVersion ), Log.DateTimeToString( statusInfo.eventsVersion ) ) );
					// Only show this log message if this is a manufacturing account; don't show any message if not.
					if ( statusInfo.isManufacturing == true )
						Log.Debug( string.Format( "<INET:STATUS_INFO.isManufacturing={0}", statusInfo.isManufacturing ) );
				}

				inetStatus.Schema.AccountNum = statusInfo.accountId;
				inetStatus.Schema.Activated = statusInfo.activated;
				inetStatus.Schema.IsManufacturing = statusInfo.isManufacturing;
				inetStatus.CurrentTime = statusInfo.currentTime;
                inetStatus.Schema.ServiceCode = statusInfo.serviceCode;                

				if ( returnVersions )
				{
					inetStatus.Schema.CylindersVersion = statusInfo.cylindersVersion;
					inetStatus.Schema.SchedulesVersion = statusInfo.schedulesVersion;
					inetStatus.Schema.SettingsVersion = statusInfo.settingsVersion;
					inetStatus.Schema.EventJournalsVersion = statusInfo.eventsVersion;
					inetStatus.Schema.CriticalErrorsVersion = statusInfo.criticalErrorsVersion; //suresh 03-Feb-2012 INS-2622
				}
				return inetStatus;
			}
			catch ( Exception ex )
			{				
				// only create a new download error if DS was online
				// and this call originated from an ExchangeStatusOperation
				if ( Inet.IsDownloadOnline && isExchangeStatusOperation )
				{
					DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.exchangeStatusInformation, ex );
					new InetUploader().EnqueueError( dsError );
				}

				string errorMsg = HandleInetWebServiceException( ex );
				Log.Error( errorMsg );

				isDownloadOnline = false;

				return new InetStatus( errorMsg );
			}
			finally
			{
				// stop watchdog
				if ( watchDog != null )
				{
					watchDog.Stop();
					watchDog.Close();
				}

				// only toggle online status if this call originated from an ExchangeStatusOperation
				if ( isExchangeStatusOperation )
				{
					// Keep updated on whether or not we're able to connect to iNet.
					// We need to know so we can show an indicator on the LCD for whether we're online or not.
					Inet.IsDownloadOnline = isDownloadOnline;
				}
			}

		}

        /// <summary>
        /// Calls iNet's exchangeStatusInformation web service.
        /// </summary>
        /// <param name="dockingStationStatus">
        /// String describing the current state or operaton of the docking station.
        /// This will typically by the ConsoleService's current ConsoleState.
        /// </param>
        /// <param name="instrumentSn">
        /// Serial number of currently docked instrument.  Else, empty string.
        /// </param>
        /// <param name="nextCalDate">The date/time the instruent is to be calibrated next.  Will be null if not currently known.</param>
        /// <param name="nextBumpDate">The date/time the instruent is to be bump tested next.  Will be null if not currently known.</param>
        /// <param name="returnVersions">true if web service should returned "version dates".  false if not.</param>
        /// <returns>
        /// Guaranteed to return a valid InetStatus instance.
        /// Caller should check InetStatus.Error for success/failure.</returns>
        public InetStatus ExchangeStatus( string dockingStationStatus, string instrumentSn, DateTime? nextCalDate, DateTime? nextBumpDate, bool returnVersions )
        {
			return ExchangeStatus( dockingStationStatus, instrumentSn, nextCalDate, nextBumpDate, returnVersions, false );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updatedList"></param>
        /// <param name="deletedList"></param>
        /// <param name="utcVersion"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public DateTime? DownloadCylinders( IList<FactoryCylinder> updatedList, IList<FactoryCylinder> deletedList,
                                                 DateTime? utcVersion, List<DockingStationError> errors )
        {
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutMedium;

            updatedList.Clear();
            deletedList.Clear();

            try
            {
                Log.Debug( string.Format( "<INET: Calling ConfigurationService.downloadKnownCylinders({0},{1},{2})",
                    _schema.AccountNum, _configDS.SerialNumber, Log.DateTimeToString( utcVersion ) ) );

				CYLINDERS cylinders = null;
				Stopwatch s = new Stopwatch();

				try
				{
					// start watchdog
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					// start stopwatch
					s.Start();

					cylinders = GetWebService( timeout ).downloadKnownCylinders( _schema.AccountNum, _configDS.SerialNumber, utcVersion, Controller.FirmwareVersion );
				}
				catch ( Exception ex )
				{
					// only create a new download error if DS was online
					if ( Inet.IsDownloadOnline )
					{
						DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.downloadKnownCylinders, ex );
						new InetUploader().EnqueueError( dsError );
					}

					Log.Error( HandleInetWebServiceException( ex ) );
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

                Log.Debug( string.Format( "<INET: downloadKnownCylinders call completed in {0} seconds", s.ElapsedMilliseconds / 1000.0 ) );

                if ( cylinders == null )  // will likely be null if can't connect to inet.
                {
                    Log.Debug( "<INET: ...CYLINDERS=null" );
                    return null;
                }

                LogWebServiceData( cylinders );
                Log.Debug( string.Format( "<INET: ...{0} CYLINDER_CHANGES", cylinders.cylinderChanges.Length ) );
                Log.Debug( string.Format( "<INET: ...cylindersVersion=\"{0}\"", Log.DateTimeToString( cylinders.cylindersVersion ) ) );

                for ( int i = 0; i < cylinders.cylinderChanges.Length; i++ )
                {
                    CYLINDER_CHANGE change = cylinders.cylinderChanges[i];
                    cylinders.cylinderChanges[i] = null; // done with this array element. Release it for garbage collection.

                    object changeType = change.changeType;

                    // Ignore 'non-isc' cylinders.
                    if ( !Char.IsDigit( change.cylinder.partNumber, 0 ) && ( change.cylinder.partNumber != FactoryCylinder.FRESH_AIR_PART_NUMBER ) )
                    {
                        Log.Warning( "<INET: Ignoring cylinder " + change.cylinder.partNumber );
                        continue;
                    }

                    FactoryCylinder factoryCylinder = new FactoryCylinder( change.cylinder.partNumber, change.cylinder.manufacturerCode );

                    // Load the gas contents into the FactoryCylinder.
                    foreach ( InetConfiguration.CYLINDER_GAS changeGas in change.cylinder.cylinderGases )
                    {
                        GasConcentration gasConcentration = new GasConcentration( changeGas.gasCode, changeGas.concentration );
                        factoryCylinder.GasConcentrations.Add( gasConcentration );
                    }

                    if ( change.changeType == CHANGE_TYPE.DELETE )
                        deletedList.Add( factoryCylinder );
                    else
                        // We don't care about updates versus creates.  The logic that 
                        // updates the database will figure out what to do on its own.
                        updatedList.Add( factoryCylinder );
                }

                return cylinders.cylindersVersion;
            }
            catch ( Exception ex )
            {
                Log.Error( ex );
                throw new InetDataException( "DownloadCylinders", ex );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="createdList"></param>
        /// <param name="updatedList"></param>
        /// <param name="deletedList"></param>
        /// <param name="instrumentSn"></param>
        /// <param name="utcVersion"></param>
        /// <param name="errors"></param>
        /// <param name="tzi">The docking station's time zone setting</param>
        /// <returns></returns>
        public DateTime? DownloadSchedules( IList<Schedule> createdList, IList<Schedule> updatedList, IList<long> deletedList, string instrumentSn,
                                                 DateTime? utcVersion, List<DockingStationError> errors, TimeZoneInfo tzi )
        {
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutMedium;

            const string funcMsg = "DownloadScheduleUpdates: ";

            try
            {
                Log.Debug( string.Format( "<INET: Calling ConfigurationService.downloadEventSchedules({0},{1},{2},{3})",
					_schema.AccountNum, _configDS.SerialNumber, Log.DateTimeToString( utcVersion ), Controller.FirmwareVersion ) );
				
				EVENT_SCHEDULES eventSchedules = null;
                Stopwatch s = new Stopwatch();                

				try
				{
					// start watchdog
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					// start stopwatch
					s.Start();

					eventSchedules = GetWebService( timeout ).downloadEventSchedules( _schema.AccountNum, _configDS.SerialNumber, instrumentSn, utcVersion, Controller.FirmwareVersion );
				}
				catch ( Exception ex )
				{
					// only create a new download error if DS was online
					if ( Inet.IsDownloadOnline )
					{
						DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.downloadEventSchedules, ex );
						new InetUploader().EnqueueError( dsError );
					}

					Log.Error( HandleInetWebServiceException( ex ) );
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

                Log.Debug( string.Format( "<INET: downloadEventSchedules call completed in {0} seconds.", s.ElapsedMilliseconds / 1000.0 ) );

                if ( eventSchedules == null ) // will likely be null if can't connect to inet.
                {
                    Log.Debug( "<INET: ...EVENT_SCHEDULES=null" );
                    return null;
                }

                LogWebServiceData( eventSchedules );
                Log.Debug( string.Format( "<INET: ...{0} EVENT_SCHEDULE_CHANGEs", eventSchedules.eventScheduleChanges.Length ) );
                Log.Debug( string.Format( "<INET: ...schedulesVersion=\"{0}\"", Log.DateTimeToString( eventSchedules.schedulesVersion ) ) );

                foreach ( EVENT_SCHEDULE_CHANGE eventScheduleChange in eventSchedules.eventScheduleChanges )
                {
                    if ( eventScheduleChange.eventSchedule == null )
                    {
                        string msg = string.Format( "{0}Ignored null eventScheduleChange.eventSchedule (changeType=\"{1}\").", funcMsg, eventScheduleChange.changeType.ToString() );
                        errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                        Log.Warning( "<INET: " + msg );
                        continue;
                    }
					
					// Ignore schedules for instrument types that can't be docked on this docking station.
					// Schedules that don't have an equipment code are okay.  If the schedule has an 
					// equipment code, then it should match the type of the docking station as only 
					// instruments of that type will be supported.  However, a VPRO schedule on an MX4 
					// docking station is also okay.
					if ( eventScheduleChange.eventSchedule.equipmentCode != null )
					{
						DeviceType scheduleDeviceType = Device.GetDeviceType( eventScheduleChange.eventSchedule.equipmentCode );
						// This check will prevent schedules for future instrument device types (other) from being stored in the local database.
						// When the docking station is upgraded to support the future instrument device type it may not have all the proper schedules.
						// One way to prevent this is to always increment the database schema when supporting a new instrument type on a pre-existing
						// type of docking station.
						if ( scheduleDeviceType != Configuration.DockingStation.Type )
						{
							if ( !( scheduleDeviceType == DeviceType.VPRO && Configuration.DockingStation.Type == DeviceType.MX4 ) )
							{
								string msg = string.Format( "{0}Ignored invalid type for eventScheduleChange.eventSchedule.equipmentCode (referenceID={1},changeType={2},equipmentCode={3}).", funcMsg, eventScheduleChange.eventSchedule.referenceID, eventScheduleChange.changeType.ToString(), eventScheduleChange.eventSchedule.equipmentCode );
								errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
								Log.Warning( "<INET: " + msg );
								continue;
							}
						}
					}

                    if ( eventScheduleChange.changeType == CHANGE_TYPE.DELETE )
                    {
                        deletedList.Add( eventScheduleChange.eventSchedule.referenceID );
                        continue;
                    }

                    if ( eventScheduleChange.eventSchedule.schedule == null )
                    {
                        string msg = string.Format( "{0}Ignored null eventScheduleChange.eventSchedule.schedule (referenceID={1},changeType={2}).", funcMsg, eventScheduleChange.eventSchedule.referenceID, eventScheduleChange.changeType.ToString() );
                        errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                        Log.Warning( "<INET: " + msg );
                        continue;
                    }

					// global type-specific schedules should only be for instruments; special schedules will never have an equipment code
                    // Exception to the above case is the Firmware Upgrade, since global level firmware can be scheduled.
                    if ( eventScheduleChange.eventSchedule.equipmentType != EquipmentTypeCode.Instrument && eventScheduleChange.eventSchedule.equipmentCode != null && eventScheduleChange.eventSchedule.eventType != EventCode.FirmwareUpgrade )
					{
						string msg = string.Format( "{0}Ignored non-instrument eventScheduleChange.eventSchedule with non-null equipment code (referenceID={1},changeType={2},equipmentCode={3}).", funcMsg, eventScheduleChange.eventSchedule.referenceID, eventScheduleChange.changeType.ToString(),eventScheduleChange.eventSchedule.equipmentCode );
						errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
						Log.Warning( "<INET: " + msg );
						continue;
					}
                                        
                    Schedule sched = null;

                    EVENT_SCHEDULE eventSchedule = eventScheduleChange.eventSchedule;

                    EventCode eventCode = MapInetEventCodeToVdsEventCode( eventSchedule.eventType, eventSchedule.equipmentType );

                    if ( eventScheduleChange.eventSchedule.schedule is FORCED_SCHEDULE )
                    {
                        FORCED_SCHEDULE fs = (FORCED_SCHEDULE)eventScheduleChange.eventSchedule.schedule;
                        sched = new ScheduledNow(
                            DomainModelConstant.NullId,
                            eventSchedule.referenceID,
                            eventSchedule.name,
                            eventCode,
							eventSchedule.equipmentCode,
                            eventSchedule.equipmentSubTypeCode,
                            eventSchedule.enabled );

                    }
                    else if ( eventSchedule.schedule is HOURLY_SCHEDULE )
                    {
                        HOURLY_SCHEDULE hs = (HOURLY_SCHEDULE)eventSchedule.schedule;
                        sched = new ScheduledHourly(
                            DomainModelConstant.NullId,
                            eventSchedule.referenceID,
                            eventSchedule.name,
                            eventCode,
							eventSchedule.equipmentCode,
                            eventSchedule.equipmentSubTypeCode,
                            eventSchedule.enabled,
                            hs.ondocking != null ? (bool)hs.ondocking : false,
                            hs.interval,
                            hs.startDate.Date,
                            TimeSpan.Parse( hs.runTime ),
                            new bool[] { hs.sunday, hs.monday, hs.tuesday, hs.wednesday, hs.thursday, hs.friday, hs.saturday }
                            );
                    }
                    else if ( eventSchedule.schedule is DAILY_SCHEDULE )
                    {
                        DAILY_SCHEDULE ds = (DAILY_SCHEDULE)eventSchedule.schedule;
                        sched = new ScheduledDaily(
                            DomainModelConstant.NullId,
                            eventSchedule.referenceID,
                            eventSchedule.name,
                            eventCode,
							eventSchedule.equipmentCode,
                            eventSchedule.equipmentSubTypeCode,
                            eventSchedule.enabled,
                            ds.ondocking != null ? (bool)ds.ondocking : false,
                            ds.interval,
                            ds.startDate.Date,
                            TimeSpan.Parse( ds.runTime )
                            );
                    }
                    else if ( eventSchedule.schedule is WEEKLY_SCHEDULE )
                    {
                        WEEKLY_SCHEDULE ws = (WEEKLY_SCHEDULE)eventSchedule.schedule;
                        sched = new ScheduledWeekly(
                            DomainModelConstant.NullId,
                            eventSchedule.referenceID,
                            eventSchedule.name,
                            eventCode,
							eventSchedule.equipmentCode,
                            eventSchedule.equipmentSubTypeCode,
                            eventSchedule.enabled,
                            ws.ondocking != null ? (bool)ws.ondocking : false,
                            ws.interval,
                            ws.startDate.Date,
                            TimeSpan.Parse( ws.runTime ),
                            new bool[] { ws.sunday, ws.monday, ws.tuesday, ws.wednesday, ws.thursday, ws.friday, ws.saturday }
                            );
                    }
                    else if ( eventSchedule.schedule is MONTHLY_SCHEDULE )
                    {
                        MONTHLY_SCHEDULE ms = (MONTHLY_SCHEDULE)eventSchedule.schedule;
                        sched = new ScheduledMonthly(
                            DomainModelConstant.NullId,
                            eventSchedule.referenceID,
                            eventSchedule.name,
                            eventCode,
							eventSchedule.equipmentCode,
                            eventSchedule.equipmentSubTypeCode,
                            eventSchedule.enabled,
                            ms.ondocking != null ? (bool)ms.ondocking : false,
                            ms.interval,
                            ms.startDate.Date,
                            TimeSpan.Parse( ms.runTime ),
                            ms.week ?? DomainModelConstant.NullShort,
                            (DayOfWeek?)ms.dayOfWeek,
                            ms.dayOfMonth ?? DomainModelConstant.NullShort
                            );
                    }
                    else if ( eventSchedule.schedule is UPON_DOCKING_SCHEDULE )
                    {
                        UPON_DOCKING_SCHEDULE uds = (UPON_DOCKING_SCHEDULE)eventSchedule.schedule;
                        sched = new ScheduledUponDocking(
                            DomainModelConstant.NullId,
                            eventSchedule.referenceID,
                            eventSchedule.name,
                            eventCode,
							eventSchedule.equipmentCode,
                            eventSchedule.equipmentSubTypeCode,
                            eventSchedule.enabled );

                    }
                    else if ( eventSchedule.schedule is ONE_TIME_SCHEDULE )
                    {
                        ONE_TIME_SCHEDULE ots = (ONE_TIME_SCHEDULE)eventSchedule.schedule;
                        DateTime runAfter = Configuration.ToLocalTime( ots.runAfter ); // all web service times are in UTC but all schedule times need to be in local time.
                        sched = new ScheduledOnce(
                            DomainModelConstant.NullId,
                            eventSchedule.referenceID,
                            eventSchedule.name,
                            eventCode,
							eventSchedule.equipmentCode,
                            eventSchedule.equipmentSubTypeCode,
                            eventSchedule.enabled,
                            runAfter.Date,
                            runAfter.TimeOfDay );

                    } // end-else-if's

                    if ( sched == null )
                    {
                        string msg = string.Format( "{0}Ignoring unknown schedule type ({1}).", funcMsg, eventSchedule.schedule.GetType() );
                        errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                        Log.Warning( "<INET: " + msg );
                        continue;
                    }

                    if ( eventSchedule.componentTypes != null )
                        sched.ComponentCodes = new List<string>( eventSchedule.componentTypes );

                    if ( sched.SerialNumbers != null )
                        sched.SerialNumbers = new List<string>( eventSchedule.equipmentSerialNumbers );

                    //INS-2460 - Make the eventschedule property more generic and use this for determining the version of the firmware upgrade the equipment should upgrade to
                    if ( eventSchedule.properties != null && eventSchedule.properties.Length > 0 )
                    {
                        short item = 1;
                        foreach ( PROPERTY property in eventSchedule.properties )
                        {
                            ScheduleProperty scheduleProperty = new ScheduleProperty();
                            scheduleProperty.ScheduleId = eventSchedule.referenceID;
                            scheduleProperty.Attribute = property.name;
                            scheduleProperty.Value = property.value;
                            //What is the purpose of the sequence here, Web Service doesn't pass this and there is no purpose AFAIK ??
                            scheduleProperty.Sequence = item++;

                            sched.ScheduleProperties.Add( scheduleProperty );
                        }                       
                    }                   

                    Log.Warning( "<INET: " + string.Format( "{0}{1} RefId={2}, {3}", funcMsg, eventScheduleChange.changeType.ToString(), sched.RefId, sched ) );

                    if ( eventScheduleChange.changeType == CHANGE_TYPE.CREATE )
                        createdList.Add(sched);

                    else if (eventScheduleChange.changeType == CHANGE_TYPE.UPDATE)
                        updatedList.Add(sched);

                } // end-for 
                
                return eventSchedules.schedulesVersion;
            }
            catch ( Exception ex )
            {
                Log.Error( ex );
                throw new InetDataException( "DownloadSchedules", ex );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="journalList"></param>
        /// <param name="utcVersion"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public DateTime? DownloadEventJournals( List<EventJournal> journalList, DateTime? utcVersion, List<DockingStationError> errors )
        {
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutMedium;

            try
            {
                Log.Debug( string.Format( "<INET: Calling ConfigurationService.downloadRecentEvents({0},{1},{2})",
                    _schema.AccountNum, _configDS.SerialNumber, Log.DateTimeToString( utcVersion ) ) );

				RECENT_EVENTS recentEvents = null;
				Stopwatch s = new Stopwatch();                

				try
				{
					// start watchdog
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					// start stopwatch
					s.Start();

					recentEvents = GetWebService( timeout ).downloadRecentEvents( _schema.AccountNum, _configDS.SerialNumber, utcVersion );
				}
				catch ( Exception ex )
				{
					// only create a new download error if DS was online
					if ( Inet.IsDownloadOnline )
					{
						DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.downloadRecentEvents, ex );
						new InetUploader().EnqueueError( dsError );
					}

					Log.Error( HandleInetWebServiceException( ex ) );
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

                Log.Debug( string.Format( "<INET: downloadRecentEvents call completed in {0} seconds.", s.ElapsedMilliseconds / 1000.0 ) );

                if ( recentEvents == null ) // will likely be null if can't connect to inet.
                {
                    Log.Debug( "<INET: ...RECENT_EVENTS = null" );
                    return null;
                }

                LogWebServiceData(recentEvents);
                Log.Debug( string.Format( "<INET: ...{0} INSTRUMENT_EVENTs", recentEvents.equipmentEvents.Length ) );

				// For large iNet accounts there will be too much data to log all the time,  
				// so don't even format messages if the current log level isn't set to trace.
				if ( Log.Level >= LogLevel.Trace )
				{
					for ( int i = 0; i < recentEvents.equipmentEvents.Length; i++ )
					{
						Log.Trace( string.Format( "       ... {0}, {1}, {2}, {3}, {4}", recentEvents.equipmentEvents[i].serialNumber, recentEvents.equipmentEvents[i].eventType, recentEvents.equipmentEvents[i].passed, recentEvents.equipmentEvents[i].equipmentVersion, Log.DateTimeToString( recentEvents.equipmentEvents[i].time ) ) );
					}
				}

                Log.Debug( string.Format( "<INET: ...{0} COMPONENT_EVENTs", recentEvents.componentEvents.Length ) );

				// For large iNet accounts there will be too much data to log all the time,  
				// so don't even format messages if the current log level isn't set to trace.
				if ( Log.Level >= LogLevel.Trace )
				{
					for ( int i = 0; i < recentEvents.componentEvents.Length; i++ )
					{
						Log.Trace( string.Format( "       ... {0}, {1}, {2}, {3}, {4}, {5}", recentEvents.componentEvents[i].serialNumber, recentEvents.componentEvents[i].eventType, recentEvents.componentEvents[i].passed, recentEvents.componentEvents[i].equipmentSerialNumber, recentEvents.componentEvents[i].equipmentVersion, Log.DateTimeToString( recentEvents.componentEvents[i].time ) ) );
					}
				}

                Log.Debug( string.Format( "<INET: ...eventsVersion=\"{0}\"", Log.DateTimeToString( recentEvents.eventsVersion ) ) );
                
                // There's a potential that the returned arrays may sometimes be rather large.  
                // Therefore, we move their contents to queues so that as we allocate EventJournal 
                // instances, we're also consuming the queues which releases memory that we're done with.
                Queue<INSTRUMENT_EVENT> instEvents = new Queue<INSTRUMENT_EVENT>( recentEvents.equipmentEvents );
                recentEvents.equipmentEvents = null;
                Queue<COMPONENT_EVENT> compEvents = new Queue<COMPONENT_EVENT>( recentEvents.componentEvents );
                recentEvents.componentEvents = null;

                MakeJournalsFromINSTRUMENT_EVENTs( journalList, instEvents );

                MakeJournalsFromCOMPONENT_EVENTs( journalList, compEvents );

                return recentEvents.eventsVersion;
            }
            catch ( Exception ex )
            {
                Log.Error( ex );
                throw new InetDataException( "DownloadEventJournals", ex );
            }
        }

        public DateTime? DownloadCriticalErrors(List<CriticalError> criticalErrors, DateTime? utcVersion)
        {
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutLow;

            try
            {
                Log.Debug( string.Format( "<INET: Calling ConfigurationService.downloadCriticalErrors({0},{1},{2})",
                    _schema.AccountNum, _configDS.SerialNumber, Log.DateTimeToString( utcVersion ) ) );

				CRITICAL_ERRORS downloadedCriticalErrors = null;
                Stopwatch s = new Stopwatch();                

				try
				{
					// start watchdog
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					// start stopwatch
					s.Start();

					downloadedCriticalErrors = GetWebService( timeout ).downloadCriticalErrors( _schema.AccountNum, _configDS.SerialNumber, utcVersion );
				}
				catch ( Exception ex )
				{
					// only create a new download error if DS was online
					if ( Inet.IsDownloadOnline )
					{
						DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.downloadCriticalErrors, ex );
						new InetUploader().EnqueueError( dsError );
					}

					Log.Error( HandleInetWebServiceException( ex ) );
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

                Log.Debug( string.Format( "<INET: downloadCriticalErrors call completed in {0} seconds", s.ElapsedMilliseconds / 1000.0 ) );

                if ( downloadedCriticalErrors == null )  // will likely be null if can't connect to inet.
                {
                    Log.Debug( "<INET: ...errors=null" );
                    return null;
                }
 
                LogWebServiceData( downloadedCriticalErrors );
                Log.Debug( string.Format( "<INET: ...{0} CRITICAL_ERRORS", downloadedCriticalErrors.criticalError.Length ) );
                Log.Debug( string.Format( "<INET: ...criticalErrorsVersion=\"{0}\"", Log.DateTimeToString( downloadedCriticalErrors.criticalErrorsVersion ) ) );

                if ( criticalErrors == null )
                    criticalErrors = new List<CriticalError>();
                else
                    criticalErrors.Clear();

                foreach ( CRITICAL_ERROR cerror in downloadedCriticalErrors.criticalError )
                {
                    int errorcode;
                    try
                    {
                        errorcode = int.Parse( cerror.code );
                    }
                    catch ( Exception )
                    {
                        errorcode = 0;
                    }

                    criticalErrors.Add( new CriticalError( errorcode, cerror.description ) );
                }

                return downloadedCriticalErrors.criticalErrorsVersion;

            }
            catch ( Exception ex )
            {
                Log.Error( ex );
                throw new InetDataException( "DownloadCriticalErrors", ex );
            }
        }

        private void MakeJournalsFromINSTRUMENT_EVENTs( List<EventJournal> journalList, Queue<INSTRUMENT_EVENT> instEvents )
        {
            while ( instEvents.Count > 0 )
            {
                INSTRUMENT_EVENT instEvent = instEvents.Dequeue();

                EventCode eventCode = MapInetEventCodeToVdsEventCode( instEvent.eventType, EquipmentTypeCode.Instrument );

                string swVersion = instEvent.equipmentVersion;

                // If the returned event doesn't have a pass/fail flag, then just default to passed.
                // Note that we make the EventTime and RunTime be the same for instrument-level events.
                journalList.Add( new EventJournal( eventCode, instEvent.serialNumber, instEvent.time, instEvent.time, instEvent.passed ?? true, swVersion ) );
            }
        }

        private void MakeJournalsFromCOMPONENT_EVENTs( List<EventJournal> journalList, Queue<COMPONENT_EVENT> compEvents )
        {
            while ( compEvents.Count > 0 )
            {
                COMPONENT_EVENT compEvent = compEvents.Dequeue();

                if (compEvent.serialNumber == null || compEvent.equipmentSerialNumber == null)
                    continue;

                EventCode eventCode = MapInetEventCodeToVdsEventCode( compEvent.eventType, EquipmentTypeCode.Instrument );

                DateTime timeStamp = compEvent.equipmentEventTime;
                DateTime compTimeStamp = compEvent.time;

                string swVersion = compEvent.equipmentVersion;

                // SGF  01-Dec-2011  DEV INS-3973 -- updated reference to 'compEvent.passed' to allow for null values; if encountered, submit 'true'
                journalList.Add(new EventJournal(eventCode.Code, compEvent.serialNumber, compEvent.equipmentSerialNumber, compTimeStamp, timeStamp, compEvent.passed ?? true, compEvent.position ?? DomainModelConstant.NullInt, swVersion));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instrumentSerialNumbers"></param>
        /// <param name="sensorSerialNumbers"></param>
        /// <param name="utcVersion"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public DateTime? DownloadRemovedEquipment( List<string> instrumentSerialNumbers, List<string> sensorSerialNumbers,
                                                  DateTime? utcVersion, List<DockingStationError> errors )
        {
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutMedium;

            instrumentSerialNumbers.Clear();
            sensorSerialNumbers.Clear();

            try
            {
                Log.Debug( string.Format( "<INET: Calling ConfigurationService.downloadRemovedEquipment({0},{1},{2})",
                    _schema.AccountNum, _configDS.SerialNumber, Log.DateTimeToString( utcVersion ) ) );

				REMOVED_EQUIPMENT removedEquipment = null; 
				Stopwatch s = new Stopwatch();     

				try
				{
					// start watchdog
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					// start stopwatch
					s.Start(); 

					removedEquipment = GetWebService( timeout ).downloadRemovedEquipment( _schema.AccountNum, _configDS.SerialNumber, utcVersion );
				}
				catch ( Exception ex )
				{
					// only create a new download error if DS was online
					if ( Inet.IsDownloadOnline )
					{
						DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.downloadRemovedEquipment, ex );
						new InetUploader().EnqueueError( dsError );
					}

					Log.Error( HandleInetWebServiceException( ex ) );
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

                Log.Debug( string.Format( "<INET: downloadRemovedEquipment call completed in {0} seconds.", s.ElapsedMilliseconds / 1000.0 ) );

                if ( removedEquipment == null ) // will likely be null if can't connect to inet.
                {
                    Log.Debug( "<INET: ...REMOVED_EQUIPMENT = null" );
                    return null;
                }

                LogWebServiceData( removedEquipment );
                Log.Debug( string.Format( "<INET: ...REMOVED_EQUIPMENT.removedInstruments={0}", ( removedEquipment.removedInstruments == null ) ? "null" : removedEquipment.removedInstruments.Length.ToString() ) );
                Log.Debug( string.Format( "<INET: ...REMOVED_EQUIPMENT.removedSensors={0}", ( removedEquipment.removedSensors == null ) ? "null" : removedEquipment.removedSensors.Length.ToString() ) );
                Log.Debug( string.Format( "<INET: ...REMOVED_EQUIPMENT.equipmentVersion=\"{0}\"", Log.DateTimeToString( removedEquipment.equipmentVersion ) ) );

                if ( removedEquipment.removedInstruments != null )
                    instrumentSerialNumbers.AddRange( removedEquipment.removedInstruments );

                if ( removedEquipment.removedSensors != null )
                    sensorSerialNumbers.AddRange( removedEquipment.removedSensors );

                return removedEquipment.equipmentVersion;
            }
            catch ( Exception ex )
            {
                Log.Error( "DownloadRemovedEquipment", ex );
            }
            return null;
        }


        /// <summary>
        /// Download firmware for either the docking station or an instrument.
        /// </summary>
        /// <param name="instrumentSn">null should be passed if downloading docking station firmware.</param>
        /// <param name="errors">null should be passed if downloading docking station firmware.</param>
        /// <returns>
        /// Gauranteed to not return null.
        /// If iNet fails to return an upgrade because it's no longer available, then the FirmwareUpgrade
        /// instance returned will have a null Firmware property.
        /// </returns>
        /// <exception cref="">
        /// This method only throws InetDataException.
        /// The caller should check the inner exception for a better reason why the throw occurred.</exception>
        public FirmwareUpgrade DownloadFirmwareUpgrade( string instrumentSn, List<DockingStationError> errors, string equipmentType, string equipmentCode, string equipmentSubTypeCode, string equipmentFullCode )
        {
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutHigh;

            MemoryStream memory = new MemoryStream();

            const int MAX_CHUNK_ATTEMPTS = 10;
            int chunkNumber = 0;
            int chunkAttempt = 0;
            byte[] checkSum = null;
            DeviceType deviceType = DeviceType.Other;
            string version = string.Empty;
            FIRMWARE_UPDATE firmwareUpdate;
            Stopwatch stopwatch = new Stopwatch(); stopwatch.Start();

			try
			{
				// create the watchdog outside the while loop so the same one can be started and stopped several times
				watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );

				while ( true )
				{
					try
					{
						chunkAttempt++;

						Log.Debug( string.Format( "<INET:Calling ConfigurationService.downloadFirmwareUpdate({0},{1},{2},{3}), attempt {4}/{5}",
							_schema.AccountNum, _configDS.SerialNumber, instrumentSn ?? "(null)", chunkNumber, chunkAttempt, MAX_CHUNK_ATTEMPTS ) );

						// start watchdog
						watchDog.Start();

						Stopwatch chuckStopwatch = new Stopwatch(); chuckStopwatch.Start();

						firmwareUpdate = GetWebService( timeout ).downloadFirmwareUpdate( _schema.AccountNum, _configDS.SerialNumber, instrumentSn, chunkNumber );

						chunkAttempt = 0; // reset after successfully making the download call

						Log.Debug( string.Format( "<INET: downloadFirmwareUpdate call completed in {0} seconds.", chuckStopwatch.ElapsedMilliseconds / 1000.0 ) );

						if ( firmwareUpdate == null )
						{
							errors.Add( new DockingStationError( "downloadFirmwareUpdate returned null FIRMWARE_UPDATE", DockingStationErrorLevel.Warning, instrumentSn ) );
							Log.Debug( "<INET: FIRMWARE_UPDATE=null" );
							return new FirmwareUpgrade();
						}

						if ( firmwareUpdate.firmwareFile == null ) // last chunk?
						{
							Log.Debug( "<INET: FIRMWARE_UPDATE.firmwareFile=null." );
							Log.Debug( string.Format( "<INET: Assuming chunkNumber {0} is the last chunk", chunkNumber ) );
							break;
						}

						if ( firmwareUpdate.checkSum == null )
						{
							errors.Add( new DockingStationError( "downloadFirmwareUpdate returned null checkSum", DockingStationErrorLevel.Warning, instrumentSn ) );
							Log.Debug( "<INET: FIRMWARE_UPDATE.checkSum=null" );
							return new FirmwareUpgrade();
						}

						Log.Debug( string.Format( "<INET: ...FIRMWARE_UPDATE.version = \"{0}\"", firmwareUpdate.version ) );
						Log.Debug( string.Format( "<INET: ...FIRMWARE_UPDATE.equipmentCode = \"{0}\"", firmwareUpdate.equipmentCode ) );
						Log.Debug( string.Format( "<INET: ...FIRMWARE_UPDATE.firmwareFile length = {0} bytes", firmwareUpdate.firmwareFile.Length ) );
						Log.Debug( string.Format( "<INET: ...FIRMWARE_UPDATE.checkSum = \"{0}\"", FirmwareUpgrade.MD5HashToString( firmwareUpdate.checkSum ) ) );
						Log.Debug( string.Format( "<INET: ...chunkNumber = {0}", chunkNumber ) );

						checkSum = firmwareUpdate.checkSum;
						version = firmwareUpdate.version;

						deviceType = DeviceType.Other;
						try
						{
							deviceType = (DeviceType)Enum.Parse( typeof( DeviceType ), firmwareUpdate.equipmentCode, true );
						}
						catch ( Exception e )
						{
							string msg = string.Format( "Error converting equipmentCode \"{0}\" to DeviceType", firmwareUpdate.equipmentCode );
							Log.Error( "<INET: " + msg, e );
							errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning, instrumentSn ) );
						}

						memory.Write( firmwareUpdate.firmwareFile, 0, firmwareUpdate.firmwareFile.Length );
					}
					catch ( Exception ex )
					{
						// only create a new download error if DS was online
						// and all retry attempts failed
						if ( Inet.IsDownloadOnline && chunkAttempt == MAX_CHUNK_ATTEMPTS )
						{
							DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.downloadFirmwareUpdate, ex );
							new InetUploader().EnqueueError( dsError );
						}

						Log.Error( HandleInetWebServiceException( ex ) );

						// If any network glitch occurs, then retry. We pause for a moment to give the 
						// network glitch  a chance to correct.
						if ( chunkAttempt < MAX_CHUNK_ATTEMPTS )
						{
							Log.Debug( "Waiting for retry" );
							Thread.Sleep( 5000 );
							continue;
						}

						throw new InetDataException( "DownloadFirmwareUpgrade", ex );
					}
					finally
					{
						// stop the watchdog, but don't close it so we can restart it for the next firmware chunk
						if ( watchDog != null )
							watchDog.Stop();
					}

					chunkNumber++;
				} // end-while
			}
			finally
			{
				// close the watchdog now that we are done trying to download the firmware
				if ( watchDog != null )
					watchDog.Close();
			}

            Log.Debug( string.Format( "<INET: Download completed in {0} seconds.", stopwatch.ElapsedMilliseconds / 1000.0 ) );

            if ( memory.Length == 0 )
                return new FirmwareUpgrade();

            byte[] firmwareFile = memory.ToArray();
            FirmwareUpgrade firmwareUpgrade = new FirmwareUpgrade(deviceType.ToString(), version, firmwareFile, checkSum, equipmentSubTypeCode, equipmentFullCode); 

            //INS-2460 We need to cache only for Instrument           
            if ( equipmentType == EquipmentTypeCode.Instrument && firmwareUpdate != null && firmwareFile != null )
            {
                string firmwareFilePath = string.Format( "{0}\\{1}_{2}.zip", firmwareFolderPath, equipmentFullCode, firmwareUpgrade.Version );
                CacheFirmwareIfLatest( firmwareFilePath, firmwareUpgrade );
            }
            
            memory = null;

            return firmwareUpgrade;
        }

        #region Cache Firmware
        /// <summary>
        /// Gets the firmware upgrade setting based on passed equipmentCode and version
        /// </summary>
        /// <param name="equipmentCode"></param>
        /// <param name="version"></param>
        /// <returns>FirmwareUpgradeSetting</returns>
        private FirmwareUpgradeSetting GetFirmwareUpgradeSetting( string equipmentCode, string equipmentSubTypeCode, string version )
        {
            return new FirmwareUpgradeSettingDataAccess().Find( equipmentCode, equipmentSubTypeCode, version );
        }

        /// <summary>
        /// Cache the firmware to the DSX SD card, if its not cached already and if its the latest version
        /// </summary>
        /// <param name="firmwareFilePath"></param>
        /// <param name="firmwareUpgrade"></param>
        private void CacheFirmwareIfLatest( string firmwareFilePath, FirmwareUpgrade firmwareUpgrade )
        {
            try
            {
                //CreateDirectory will return the existing DirectoryInfo, if folder already exists.
                Directory.CreateDirectory( firmwareFolderPath );

                FirmwareUpgradeSetting firmwareUpgradeSetting = GetFirmwareUpgradeSetting( firmwareUpgrade.EquipmentCode, firmwareUpgrade.EquipmentSubTypeCode, firmwareUpgrade.Version );
                //If the firmware already cached and the firmware file still exists, skip the caching process.
                if ( firmwareUpgradeSetting != null && File.Exists( firmwareFilePath ) )
                {
                    Log.Debug( string.Format( "Firmware already cached and the firmware file exists. Firmware File Path {0}, Version {1}", firmwareFilePath, firmwareUpgrade.Version ) );
                    return;
                }
                else
                {          
                    //Check if the firmware is the latest version, if not then cache the firmware, delete the existing old firmware and store the firmware details to the db
                    List<FirmwareUpgradeSetting> firmwareUpgradeSettings = new FirmwareUpgradeSettingDataAccess().FindAll();
                    Version firmwareVersion = new Version( firmwareUpgrade.Version );
                    if ( firmwareUpgradeSettings.Count == 0 || !File.Exists( firmwareFilePath ) || !firmwareUpgradeSettings.Exists( fw => new Version( fw.Version ) >= firmwareVersion && fw.EquipmentCode == firmwareUpgrade.EquipmentCode && fw.EquipmentSubTypeCode == firmwareUpgrade.EquipmentSubTypeCode ) )
                    {
                        //Cache the firmware as firmware file
                        using ( FileStream fileStream = new FileStream( firmwareFilePath, FileMode.Create ) )
                        {
                            using ( BinaryWriter binaryWriter = new BinaryWriter( fileStream ) )
                            {
                                binaryWriter.Write( firmwareUpgrade.Firmware );
                                fileStream.Close();
                                binaryWriter.Close();
                            }
                        }

                        //Delete the existing old firmware files, if any
                        DirectoryInfo di = new DirectoryInfo( firmwareFolderPath );
                        FileInfo[] files = di.GetFiles( "*.zip" );
                        foreach ( FileInfo file in files )
                        {
                            //Skip the firmware file which is cached now
                            if ( file.Name != firmwareFilePath.Substring( firmwareFilePath.LastIndexOf( "\\" ) + 1 ) )
                            {
                                //For MX4 DSX, which can be used for multiple instrument types like Ventis, iQuad and Ventis Pro
                                //So in those cases, just delete the older files specific to the docked instrument type and ignore the other instrument type
                                if ( Configuration.DockingStation.Type == DeviceType.MX4 && !file.Name.Substring(0, file.Name.LastIndexOf( "_" ) ).StartsWith( firmwareUpgrade.EquipmentFullCode ) )
                                {
                                    continue;
                                }
                                file.Attributes = FileAttributes.Normal; //To remove the read only attributes if any set
                                File.Delete( file.FullName );
                            }
                        }

                        //Once the firmware is cached as a file, store the firmware details in the table for future retrieval/validating purpose of the caching
                        firmwareUpgradeSetting = new FirmwareUpgradeSetting();
                        firmwareUpgradeSetting.FileName = firmwareFilePath;
                        firmwareUpgradeSetting.CheckSum = firmwareUpgrade.MD5Hash;
                        firmwareUpgradeSetting.EquipmentCode = firmwareUpgrade.EquipmentCode;
                        firmwareUpgradeSetting.EquipmentSubTypeCode = firmwareUpgrade.EquipmentSubTypeCode;
                        firmwareUpgradeSetting.EquipmentFullCode = firmwareUpgrade.EquipmentFullCode;
                        firmwareUpgradeSetting.Version = firmwareUpgrade.Version;
                        new FirmwareUpgradeSettingDataAccess().Save( firmwareUpgradeSetting );
                    }
                    else
                    {
                        Log.Debug( string.Format( "Firmware version is not the latest, skipping the caching of firmware. Version {0}, Equipment Code {1}", firmwareUpgrade.Version, firmwareUpgrade.EquipmentFullCode ) );
                    }
                }
            }
            catch ( Exception ex )
            {
                Log.Warning( string.Format( "Error while caching the firmware file to the SD Card Memory - {0}. Equipment Code: {1}, Version: {2}, Exception: {3} ", firmwareFilePath, firmwareUpgrade.EquipmentFullCode, firmwareUpgrade.Version, ex ) );                
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dockingStation"></param>
        /// <param name="denied"></param>
        /// <param name="replaced"></param>
        /// <param name="updatedSettingsGroups"></param>
        /// <param name="deletedSettingsGroups"></param>
        /// <param name="updatedInstruments">Instrument Specific Settings</param>
        /// <param name="utcVersion"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public DateTime? DownloadSettings( out DockingStation dockingStation,
            List<GasEndPoint> manifolds, List<GasEndPoint> manualCylinders,
            List<string> denied, List<string> replaced,
			List<InstrumentSettingsGroup> updatedDefaultSettingsGroups,
			List<InstrumentSettingsGroup> updatedSettingsGroups, List<long> deletedSettingsGroups,
            List<Instrument_> updatedInstruments, DateTime? utcVersion, List<DockingStationError> errors, List<SensorCalibrationLimits> sensorCalibrationLimits ) 
        {
			WatchDog watchDog = null;
			int timeout = _configDS.InetTimeoutMedium;

            denied.Clear();
            replaced.Clear();

			updatedDefaultSettingsGroups.Clear();
            updatedSettingsGroups.Clear();
            deletedSettingsGroups.Clear();
            updatedInstruments.Clear();

            dockingStation = null;

            try
            {
                Log.Debug( string.Format( "<INET: Calling ConfigurationService.downloadSettings({0},{1},{2},{3})",
                    _schema.AccountNum, _configDS.SerialNumber, Log.DateTimeToString( utcVersion ), Controller.FirmwareVersion ) );
								
				SETTINGS settings = null;
				Stopwatch s = new Stopwatch();
				                
				try
				{
					// start watchdog
					watchDog = new WatchDog( GetWatchdogName(), GetWatchdogPeriod( timeout ), Log.LogToFile );
					watchDog.Start();

					// start stopwatch
					s.Start();

					settings = GetWebService( timeout ).downloadSettings( _schema.AccountNum, _configDS.SerialNumber, utcVersion, Controller.FirmwareVersion );
				}
				catch ( Exception ex )
				{
					// only create a new download error if DS was online
					if ( Inet.IsDownloadOnline )
					{
						DockingStationError dsError = CreateFailedDownloadDockingStationError( DownloaderWebMethod.downloadSettings, ex );
						new InetUploader().EnqueueError( dsError );
					}

					string errorMsg = HandleInetWebServiceException( ex );
					Log.Error( errorMsg );
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

                Log.Debug( string.Format( "<INET: downloadSettings call completed in {0} seconds.", s.ElapsedMilliseconds / 1000.0 ) );

                if ( settings == null ) // will likely be null if can't connect to inet.
                {
                    Log.Debug( "<INET: ...SETTINGS = null" );
                    return null;
                }

                LogWebServiceData( settings );
                Log.Debug( string.Format( "<INET: ...SETTINGS.dockingStationSettings is {0}", ( settings.dockingStationSettings == null ) ? "null" : settings.dockingStationSettings.ReferenceID.ToString() ) );
                Log.Debug( string.Format( "<INET: ...SETTINGS.instrumentsToDeny={0}", ( settings.instrumentsToDeny == null ) ? "null" : settings.instrumentsToDeny.Length.ToString() ) );
				Log.Debug( string.Format( "<INET: ...SETTINGS.isDockingStationReplaced={0}", settings.isDockingStationReplaced.ToString() ) );
                Log.Debug( string.Format( "<INET: ...SETTINGS.replacedInstruments={0}", ( settings.replacedInstruments == null ) ? "null" : settings.replacedInstruments.Length.ToString() ) );
                Log.Debug( string.Format( "<INET: ...SETTINGS.manifoldsAttached={0}", ( settings.manifoldsAttached == null ) ? "null" : settings.manifoldsAttached.Length.ToString() ) );
                Log.Debug( string.Format( "<INET: ...SETTINGS.manualCylinders={0}", ( settings.manualCylinders == null ) ? "null" : settings.manualCylinders.Length.ToString() ) ); // SGF  20-Mar-2013  INS-3812
				Log.Debug( string.Format( "<INET: ...SETTINGS.defaultInstrumentSettings={0}", ( settings.defaultInstrumentSettings == null ) ? "null" : settings.defaultInstrumentSettings.Length.ToString() ) );
				Log.Debug( string.Format( "<INET: ...SETTINGS.instrumentSettings={0}", ( settings.instrumentSettings == null ) ? "null" : settings.instrumentSettings.Length.ToString() ) );
                Log.Debug( string.Format( "<INET: ...SETTINGS.instrumentSpecificSettings={0}", ( settings.instrumentSpecificSettings == null ) ? "null" : settings.instrumentSpecificSettings.Length.ToString() ) );
                Log.Debug( string.Format( "<INET: ...SETTINGS.settingsVersion={0}", Log.DateTimeToString( settings.settingsVersion ) ) );

                dockingStation = MakeDockingStationSettings( settings, errors );

                manifolds.AddRange( MakeManifolds( settings, errors ) );

                manualCylinders.AddRange( MakeManualCylinders( settings, errors ) );
                
				// default setting group(s) for specific equipment code(s)
				if ( settings.defaultInstrumentSettings != null )
				{
					// Place settings into a queue that can be consumed by MakeDefaultInstrumentSettings.
					// This allows the memory allocated by the webservice to be returned back to
					// the garbage collector as we process the data.
					Queue<DEFAULT_INSTRUMENT_SETTINGS> defaultSettingsGroupsQueue = new Queue<DEFAULT_INSTRUMENT_SETTINGS>( settings.defaultInstrumentSettings );
					settings.defaultInstrumentSettings = null;
					MakeDefaultInstrumentSettingsGroups( defaultSettingsGroupsQueue, updatedDefaultSettingsGroups, errors );
				}

				// setting groups for specific instrument serial numbers
                if ( settings.instrumentSettings != null )
                {
                    // Place settings into a queue that can be consumed by MakeInstrumentSettings.
                    // This allows the memory allocated by the webservice to be returned back to
                    // the garbage collector as we process the data.
                    Queue<INSTRUMENT_SETTINGS_GROUP> settingsGroupsQueue = new Queue<INSTRUMENT_SETTINGS_GROUP>( settings.instrumentSettings );
                    settings.instrumentSettings = null;
                    MakeInstrumentSettingsGroups( settingsGroupsQueue, updatedSettingsGroups, deletedSettingsGroups, errors );
                }

                Dictionary<string, Instrument_> instrumentsDict = new Dictionary<string, Instrument_>();

				// users and sites for specific instrument serial numbers
                if ( settings.instrumentSpecificSettings != null )
                {
                    Queue<INSTRUMENT_SPECIFIC_SETTINGS> instSpecificSettingsQueue = new Queue<INSTRUMENT_SPECIFIC_SETTINGS>( settings.instrumentSpecificSettings );
                    settings.instrumentSpecificSettings = null;
                    MakeInstrumentSpecificSettings( instSpecificSettingsQueue, instrumentsDict );
                }

                updatedInstruments.AddRange( instrumentsDict.Values );

                // Although we have the ability to save the denied instruments list to the DB,
                // we do not yet have any logic in place that actually does something with saved
                // list.  So, ignore it for. 
                if ( settings.instrumentsToDeny != null && settings.instrumentsToDeny.Length > 0 )
                {
#if DENIED
                    denied.AddRange( settings.instrumentsToDeny );
#else
                    Log.Warning( "<INET: Ignoring SETTINGS.instrumentsToDeny." );
#endif
                }

				if ( settings.isDockingStationReplaced )
				{
					replaced.Add( Configuration.DockingStation.SerialNumber );
				}

                if ( settings.replacedInstruments != null && settings.replacedInstruments.Length > 0 )
                {
					// replaced instrument serial numbers for the account group
                    replaced.AddRange( settings.replacedInstruments );
                }

                MakeSensorCalibrationLimits( sensorCalibrationLimits, settings );

				return settings.settingsVersion;
            }
            catch ( Exception ex )
            {
                Log.Error( ex );
                throw new InetDataException( "DownloadSettings", ex );
            }
        }

		private DockingStationError CreateFailedDownloadDockingStationError( DownloaderWebMethod webMethod, Exception ex )
		{
			return CreateFailedInetDockingStationError( InetService.ConfigurationService, webMethod.ToString(), ex ); 
		}
		
		private List<GasEndPoint> MakeManifolds( SETTINGS settings, List<DockingStationError> errors )
        {
            List<GasEndPoint> manifolds = new List<GasEndPoint>();

            if ( settings == null || settings.manifoldsAttached == null )
                return manifolds;

            foreach ( ATTACHED_MANIFOLD am in settings.manifoldsAttached )
            {
                if ( am.port <= 0 || am.port > Configuration.DockingStation.NumGasPorts )
                {
                    string msg = string.Format( "Invalid port ({0}) in manifold \"{1}\" (\"{2}\")", am.port, am.uid, am.partNumber );
                    Log.Error( "<INET: " + msg );
                    errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                    continue;
                }

                // we don't validate the part number here against the database of FactoryCylinders.
                // That is done later by the caller.  Here, we're merely responsible for returning the downloaded data.
                Cylinder cyl = new Cylinder( am.partNumber, string.Empty /*manufacturer*/ );
                cyl.FactoryId = am.uid;
                cyl.ExpirationDate = am.expirationDate;

                manifolds.Add( new GasEndPoint( cyl, am.port, GasEndPoint.Type.Manifold ) );
            }

            return manifolds;
        }

        private List<GasEndPoint> MakeManualCylinders(SETTINGS settings, List<DockingStationError> errors)
        {
            List<GasEndPoint> manualCylinders = new List<GasEndPoint>();

            if (settings == null || settings.manualCylinders == null)
                return manualCylinders;

            foreach (MANUAL_CYLINDERS mc in settings.manualCylinders)
            {
                if ( mc.port <= 0 || mc.port > Configuration.DockingStation.NumGasPorts )
                {
                    string msg = string.Format("Invalid port ({0}) in manual gas cylinder assignment \"{1}\" (\"{2}\")", mc.port, mc.uid, mc.partNumber);
                    Log.Error("<INET: " + msg);
                    errors.Add(new DockingStationError(msg, DockingStationErrorLevel.Warning));
                    continue;
                }

                // we don't validate the part number here against the database of FactoryCylinders.
                // That is done later by the caller.  Here, we're merely responsible for returning the downloaded data.
                Cylinder cyl = new Cylinder(mc.partNumber, string.Empty /*manufacturer*/ );
                cyl.FactoryId = mc.uid;
                cyl.ExpirationDate = mc.expirationDate;

                manualCylinders.Add(new GasEndPoint(cyl, mc.port, GasEndPoint.Type.Manual));
            }

            return manualCylinders;
        }

		private void MakeDefaultInstrumentSettingsGroups( Queue<DEFAULT_INSTRUMENT_SETTINGS> defaultSettingsGroupsQueue,
			IList<InstrumentSettingsGroup> updatedDefaultSettingsGroups,
			List<DockingStationError> errors )
        {
			const string funcMsg = "MakeDefaultInstrumentSettings: ";

			while ( defaultSettingsGroupsQueue.Count > 0 )
			{
				DEFAULT_INSTRUMENT_SETTINGS defaultGroup = defaultSettingsGroupsQueue.Dequeue();

				if ( defaultGroup == null )
				{
					string msg = string.Format( "{0}Ignored null element in defaultInstrumentSettings.", funcMsg );
					errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
					Log.Warning( "<INET: " + msg );
					continue;
				}

				// For v6.2 and newer docking stations, the Core Server should always be sending 
				// the instrumentSettings as an INSTRUMENT_SETTINGS_HEADER.
				INSTRUMENT_SETTINGS_HEADER defaultSettingsHeader = defaultGroup.instrumentSettings as INSTRUMENT_SETTINGS_HEADER;
				// Checking for null here so later we can safely report errors with the ReferenceID.
				if ( defaultSettingsHeader == null )
				{
					string msg = string.Format( "{0}Ignored defaultInstrumentSettings.instrumentSettings as it is null as an INSTRUMENT_SETTINGS_HEADER.", funcMsg );
					errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
					Log.Warning( "<INET: " + msg );
					continue;
				}

				// Equipment code should never be null for default instrument settings.
				if ( defaultGroup.equipmentCode == null )
				{
					string msg = string.Format( "{0}Ignored null defaultInstrumentSettings.equipmentCode (ReferenceID={1}).", funcMsg, defaultSettingsHeader.ReferenceID );
					errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
					Log.Warning( "<INET: " + msg );
					continue;
				}

				// The Core Server will only send UPDATE changes with the expectation that there 
				// always was and always will be default settings for each supported equipment code 
				// for the current docking station.  There will only be one default group per
				// equipment code.  If the Core Server gave us one or more default groups, a 
				// pre-existing default group with a matching equipment code will need to be deleted 
				// from the local database before the new one is inserted in the same transaction.
				if ( defaultGroup.changeType != CHANGE_TYPE.UPDATE )
				{
					string msg = string.Format( "{0}CHANGE_TYPE.{1} is unsupported for defaultInstrumentSettings.changeType (ReferenceID={2},equipmentCode={3}).", funcMsg, defaultGroup.changeType.ToString(), defaultSettingsHeader.ReferenceID, defaultGroup.equipmentCode );
					Log.Warning( "<INET: " + msg );
					errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
					continue;
				}
				
				// Ignore settings for instrument types that can't be docked on this docking station.
				// VPRO settings on an MX4 docking station is also okay.
				DeviceType defaultGroupDeviceType = Device.GetDeviceType( defaultGroup.equipmentCode );
				// This check will prevent settings for future instrument device types (other) from being stored in the local database.
				// When the docking station is upgraded to support the future instrument device type it may not have all the proper settings.
				// One way to prevent this is to always increment the database schema when supporting a new instrument type on a pre-existing
				// type of docking station.
				if ( defaultGroupDeviceType != Configuration.DockingStation.Type )
				{
					if ( !( defaultGroupDeviceType == DeviceType.VPRO && Configuration.DockingStation.Type == DeviceType.MX4 ) )
					{
						string msg = string.Format( "{0}Ignored invalid type for defaultInstrumentSettings.equipmentCode (ReferenceID={1},equipmentCode={2}).", funcMsg, defaultGroup.instrumentSettings.ReferenceID, defaultGroup.equipmentCode );
						errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
						Log.Warning( "<INET: " + msg );
						continue;
					}
				}

				// Ensure that iNet doesn't send more than one default group for any equipment code.
				foreach ( InstrumentSettingsGroup isg in updatedDefaultSettingsGroups )
				{
					if ( isg.EquipmentCode == defaultGroup.equipmentCode )
					{
						string msg = string.Format( "{0}Ignored duplicate defaultInstrumentSettings.equipmentCode (ReferenceID1={1},ReferenceID2={2},equipmentCode={3}).", funcMsg, isg.RefId, defaultGroup.instrumentSettings.ReferenceID, defaultGroup.equipmentCode );
						errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
						Log.Warning( "<INET: " + msg );
						continue;
					}
				}

				Instrument_ instrument = MakeInstrument( defaultSettingsHeader );	

				updatedDefaultSettingsGroups.Add( new InstrumentSettingsGroup( defaultSettingsHeader.ReferenceID, defaultGroup.equipmentCode, null, instrument ) );
			}
        }

        private void MakeInstrumentSettingsGroups( Queue<INSTRUMENT_SETTINGS_GROUP> settingsGroupsQueue,
            IList<InstrumentSettingsGroup> updatedSettingsGroups,
            IList<long> deletedSettingsGroups,
            List<DockingStationError> errors )
        {
			const string funcMsg = "MakeInstrumentSettingsGroups: ";

            while ( settingsGroupsQueue.Count > 0 )
            {
                INSTRUMENT_SETTINGS_GROUP group = settingsGroupsQueue.Dequeue();

				if ( group == null )
				{
					string msg = string.Format( "{0}Ignored null element in instrumentSettings.", funcMsg );
					errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
					Log.Warning( "<INET: " + msg );
					continue;
				}

				// For v6.2 and newer docking stations, the Core Server should always be sending 
				// the instrumentSettings as an INSTRUMENT_SETTINGS_HEADER.
				INSTRUMENT_SETTINGS_HEADER settingsHeader = group.instrumentSettings as INSTRUMENT_SETTINGS_HEADER;
				// Checking for null here so later we can safely report errors with the ReferenceID.
				if ( settingsHeader == null )
				{
					string msg = string.Format( "{0}Ignored instrumentSettings.instrumentSettings as it is null as an INSTRUMENT_SETTINGS_HEADER.", funcMsg );
					errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
					Log.Warning( "<INET: " + msg );
					continue;
				}

                // For settings that are deleted, only the reference ID is provided by the server.
                if ( group.changeType == CHANGE_TYPE.DELETE )
                {
                    deletedSettingsGroups.Add( group.instrumentSettings.ReferenceID );
                    continue;
                }

				// Non-default instrument group settings need to be linked to one or more instrument serial numbers.
				// This check should not be done if the changeType is DELETE.
				if ( group.instrumentSerialNumbers == null || group.instrumentSerialNumbers.Length == 0 )
				{
					string msg = string.Format( "{0}Ignored null/empty instrumentSettings.instrumentSerialNumbers (ReferenceID={1}).", funcMsg, settingsHeader.ReferenceID );
					errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
					Log.Warning( "<INET: " + msg );
					continue;
				}

                Instrument_ instrument = MakeInstrument( settingsHeader );

                InstrumentSettingsGroup instSettingsGroup = new InstrumentSettingsGroup( group.instrumentSettings.ReferenceID, null, group.instrumentSerialNumbers, instrument );

                // As far as we're concerned, updates and creates are the same thing
                // (we treat them the same).  Theoretically, the server is only supposed to 
                // return CREATES and not UPDATES.  We check for both, though, just in case.
				if ( group.changeType == CHANGE_TYPE.CREATE || group.changeType == CHANGE_TYPE.UPDATE )
                    updatedSettingsGroups.Add( instSettingsGroup );

                else
                {
                    string msg = string.Format( "{0}CHANGE_TYPE.{1} is unsupported.", funcMsg, group.changeType.ToString() );
                    Log.Warning( "<INET: " + msg );
                    errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                }
            }
        }

		private void SetAlarmActionMessageProperty( AlarmActionMessages alarmActionMessages, string keyName, string value )
		{
			if ( keyName.StartsWith( "ALARM_MESSAGE_GAS_ALERT_" ) )
			{
				alarmActionMessages.GasAlertMessage = value;
			}
			else if ( keyName.StartsWith( "ALARM_MESSAGE_LOW_" ) )
			{
				alarmActionMessages.LowAlarmMessage = value;
			}
			else if ( keyName.StartsWith( "ALARM_MESSAGE_HIGH_" ) )
			{
				alarmActionMessages.HighAlarmMessage = value;
			}
			else if ( keyName.StartsWith( "ALARM_MESSAGE_STEL_" ) )
			{
				alarmActionMessages.StelAlarmMessage = value;
			}
			else if ( keyName.StartsWith( "ALARM_MESSAGE_TWA_" ) )
			{
				alarmActionMessages.TwaAlarmMessage = value;
			}
		}

        private Instrument_ MakeInstrument( INSTRUMENT_SETTINGS_HEADER settingsHeader )
        {
            Instrument_ instrument = new Instrument_();

			// Below dictionary will be used to store alarm action message INSTRUMENT_SETTINGs downloaded from iNet.
			Dictionary<string, AlarmActionMessages> alarmMessagesSettings = new Dictionary<string, AlarmActionMessages>();
			// Below dictionary will be used to store all other INSTRUMENT_SETTINGs downloaded from iNet.
			Dictionary<string, INSTRUMENT_SETTING> instSettings = new Dictionary<string, INSTRUMENT_SETTING>();
			
			// Process all INSTRUMENT_SETTINGs sent by iNet.
			for ( int i = 0; i < settingsHeader.instrumentSettings.Length; i++ )
			{
				// e.g. BUMP_TIMEOUT, ALARM_MESSAGE_LOW_ALARM_S0001
				string keyName = settingsHeader.instrumentSettings[i].name;

				// Alarm action messages are processed independently of other instrument level 
				// settings and ALWAYS start with "ALARM_MESSAGE_".
				if ( keyName.StartsWith( "ALARM_MESSAGE_" ) )
				{
					// e.g. ALARM_MESSAGE_LOW_S0001, ALARM_MESSAGE_GAS_ALERT_S0002
					string sensorCode = keyName.Substring( keyName.LastIndexOf( '_' ) + 1 );

					// If the alarm message is null, we want to let the object property stay as an empty string.
					INSTRUMENT_SETTING_STRING messageSetting = settingsHeader.instrumentSettings[i] as INSTRUMENT_SETTING_STRING;
					if ( messageSetting == null || messageSetting.value == null )
					{
						Log.Debug( keyName + " WAS SENT BY INET AS A NULL VALUE!" );
						continue;
					}

					// Set the message on the correct property of an AlarmActionMessages object.
					if ( alarmMessagesSettings.ContainsKey( sensorCode ) )
					{
						// Pull the pre-existing AlarmActionMessages object out of the dictionary to set a different message property.
						SetAlarmActionMessageProperty( alarmMessagesSettings[sensorCode], keyName, messageSetting.value );
					}
					else
					{
						// An AlarmActionMessages object has not yet been created for the current sensorCode.
						// Create it, set one of the message properties and then store it in the dictionary.
						AlarmActionMessages aam = new AlarmActionMessages( sensorCode );

						SetAlarmActionMessageProperty( aam, keyName, messageSetting.value );

						alarmMessagesSettings[sensorCode] = aam;
					}
				}
				else 
				{
					// Other instrument level settings.
					instSettings[keyName] = settingsHeader.instrumentSettings[i];
				}
			}

			// Assign all alarm action messages to the return instrument.
			foreach ( KeyValuePair<string, AlarmActionMessages> aam in alarmMessagesSettings )
			{
				instrument.AlarmActionMessages.Add( aam.Value );
			}

			// Set the other INSTRUMENT_SETTINGs on the return instrument.
			INSTRUMENT_SETTING baseSetting;
			INSTRUMENT_SETTING_FLOAT floatSetting;
			INSTRUMENT_SETTING_INTEGER integerSetting;
			INSTRUMENT_SETTING_STRING stringSetting;

			// iNet will provide null when the default access code (no password required) should be set on the instrument.
			// For instrument setting groups linked to specific serial numbers, iNet does not provide an equipment code.
			// To protect against NullReferenceExceptions, the access code property will be set to an empty string instead.  
			// The Instrument Settings Update operation will need to handle converting the empty string to the default acccess code.
			stringSetting = instSettings.TryGetValue( "ACCESS_CODE", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_STRING : null;
			if ( stringSetting != null )
				instrument.AccessCode = stringSetting.value;

			integerSetting = instSettings.TryGetValue( "DATALOG_RECORDING_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.RecordingInterval = integerSetting.value;

			integerSetting = instSettings.TryGetValue( "TWA_TIME_BASE", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.TWATimeBase = integerSetting.value;

			integerSetting = instSettings.TryGetValue( "MAN_DOWN_WARNING_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.OomWarningInterval = integerSetting.value;

			integerSetting = instSettings.TryGetValue( "DOCK_OVERDUE_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.DockInterval = integerSetting.value;

            integerSetting = instSettings.TryGetValue( "MAINTENANCE_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
            if ( integerSetting != null )
                instrument.MaintenanceInterval = integerSetting.value;

			// there is the possibility of an overflow exception, but it should never happen
			floatSetting = instSettings.TryGetValue( "CAL_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_FLOAT : null;
			if ( floatSetting != null )
				instrument.CalibrationInterval = (short)floatSetting.value;

			floatSetting = instSettings.TryGetValue( "BUMP_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_FLOAT : null;
			if ( floatSetting != null )
				instrument.BumpInterval = (double)(decimal)floatSetting.value;

			integerSetting = instSettings.TryGetValue( "BUMP_THRESHOLD", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.BumpThreshold = integerSetting.value;

			integerSetting = instSettings.TryGetValue( "BUMP_TIMEOUT", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.BumpTimeout = integerSetting.value;

			integerSetting = instSettings.TryGetValue( "BACKLIGHT_TIMEOUT", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.BacklightTimeout = integerSetting.value;

			stringSetting = instSettings.TryGetValue( "LANGUAGE_CODE", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_STRING : null;
			if ( stringSetting != null )
				instrument.Language.Code = stringSetting.value;

			integerSetting = instSettings.TryGetValue( "MAGNETIC_FIELD_DURATION", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.MagneticFieldDuration = integerSetting.value;

			stringSetting = instSettings.TryGetValue( "COMPANY_NAME", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_STRING : null;
			if ( stringSetting != null )
				instrument.CompanyName = stringSetting.value;

			// Even though company message can be a multiline value, iNet will send it as a single string where lines are separated by the '|' character.
			stringSetting = instSettings.TryGetValue( "COMPANY_MESSAGE", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_STRING : null;
			if ( stringSetting != null )
				instrument.CompanyMessage = stringSetting.value;

			integerSetting = instSettings.TryGetValue( "WL_PEER_LOST_THRESHOLD", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.WirelessPeerLostThreshold = integerSetting.value;

			integerSetting = instSettings.TryGetValue( "WL_NETWORK_LOST_THRESHOLD", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.WirelessNetworkLostThreshold = integerSetting.value;

            integerSetting = instSettings.TryGetValue( "WL_NETWORK_DISCONNECT_DELAY", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
            if (integerSetting != null)
                instrument.WirelessNetworkDisconnectDelay = integerSetting.value;

			integerSetting = instSettings.TryGetValue( "WL_READINGS_DEADBAND", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
			if ( integerSetting != null )
				instrument.WirelessReadingsDeadband = integerSetting.value;

            integerSetting = instSettings.TryGetValue( "LONE_WORKER_OK_MESSAGE_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
            if ( integerSetting != null )
                instrument.LoneWorkerOkMessageInterval = integerSetting.value;

            integerSetting = instSettings.TryGetValue( "GPS_READING_INTERVAL", out baseSetting ) ? baseSetting as INSTRUMENT_SETTING_INTEGER : null;
            if ( integerSetting != null )
                instrument.GpsReadingInterval = integerSetting.value;

            if ( settingsHeader.favoritePIDFactors != null )
                instrument.FavoritePidFactors.AddRange( settingsHeader.favoritePIDFactors );

            if ( settingsHeader.customPIDFactors != null )
            {
                foreach ( CUSTOM_PID_FACTOR custom in settingsHeader.customPIDFactors )
                {
                    // Note the cast from float to decimal then assignment to double.  This is to prevent rounding errors
                    // that occur when trying to assign from a float directly to a double.  e.g. if float F
                    // with value 20.9 is assigned directly to double D, then D will have end up with a value
                    // of something like 20.8999996185303. Doing the pre-cast to a decimal prevents this 
                    // from happening.
                    instrument.CustomPidFactors.Add( new ResponseFactor( custom.name, custom.gasCode, (double)(decimal)custom.factor ) );
                }
            }

            if ( settingsHeader.options != null )
            {
                foreach ( string option in settingsHeader.options )
                {
                    DeviceOption deviceOption = new DeviceOption( option, true );
                    deviceOption.Enabled = true;
                    instrument.Options.Add( deviceOption );
                }
            }

            if ( settingsHeader.sensorSettings != null )
            {
                foreach ( SENSOR_SETTINGS sensorSettings in settingsHeader.sensorSettings )
                {
                    Sensor sensor = MakeSensor( sensorSettings );
                    instrument.SensorSettings[ sensor.Type.Code ] = sensor;
                }
            }

			if ( settingsHeader.wirelessSettings != null )
			{
				instrument.WirelessModule = MakeWirelessModule( settingsHeader.wirelessSettings, settingsHeader.wirelessOptions );
			}

            return instrument;
        }

        private void MakeInstrumentSpecificSettings( Queue<INSTRUMENT_SPECIFIC_SETTINGS> instSpecificSettingsQueue, Dictionary<string,Instrument_> instrumentDict )
        {
            foreach ( INSTRUMENT_SPECIFIC_SETTINGS instSpecificSettings in instSpecificSettingsQueue )
            {
                Instrument_ instrument = null;
                instrumentDict.TryGetValue( instSpecificSettings.serialNumber, out instrument );

                if ( instrument == null )
                {
                    instrument = new Instrument_();
                    instrument.SerialNumber = instSpecificSettings.serialNumber;

                    instrumentDict[ instrument.SerialNumber ] = instrument;
                }

                instrument.ActiveUser = instSpecificSettings.activeUser;
                instrument.ActiveSite = instSpecificSettings.activeSite;
                instrument.AccessLevel = instSpecificSettings.accessLevel;

                if ( instSpecificSettings.users != null )
                    instrument.Users = new List<string>( instSpecificSettings.users );

                if ( instSpecificSettings.sites != null )
                    instrument.Sites = new List<string>( instSpecificSettings.sites );
            }
        }

        private Sensor MakeSensor( SENSOR_SETTINGS sensorSettings )
        {
            Sensor sensor = new Sensor();

            // Note the casts from float to decimal then assignment to double. This is to prevent rounding errors
            // that occur when trying to assign from a float directly to a double.  e.g. if float F
            // with value 20.9 is assigned directly to double D, then D will have end up with a value
            // of something like 20.8999996185303. Doing the pre-cast to a decimal prevents this 
            // from happening.

            sensor.Type.Code = sensorSettings.sensorCode;
            sensor.Enabled = sensorSettings.enabled;
			if ( sensorSettings.alertSpecified )
				sensor.Alarm.GasAlert = (double)(decimal)sensorSettings.alert;
            sensor.Alarm.Low = (double)(decimal)sensorSettings.low;
            sensor.Alarm.High = (double)(decimal)sensorSettings.high;
            sensor.Alarm.STEL = (double)(decimal)sensorSettings.STEL;
            sensor.Alarm.TWA = (double)(decimal)sensorSettings.TWA;
            sensor.GasDetected = sensorSettings.gasDetected;
            if ( !string.IsNullOrEmpty( sensorSettings.calibrationGas ) )
                sensor.CalibrationGas = GasType.Cache[ sensorSettings.calibrationGas ];
            sensor.CalibrationGasConcentration = (double)(decimal)sensorSettings.calibrationGasConcentration;

            return sensor;
        }

		private WirelessModule MakeWirelessModule( COMPONENT_SETTING[] wirelessSettings, string[] wirelessOptions )
		{
			Dictionary<string, COMPONENT_SETTING> wlSettings = new Dictionary<string, COMPONENT_SETTING>();

			// Process all wireless COMPONENT_SETTINGs sent by iNet.
			for ( int i = 0; i < wirelessSettings.Length; i++ )
			{
				// e.g. WL_MESSAGE_HOPS, WL_MAX_PEERS, etc
				string keyName = wirelessSettings[i].name;

				// Add settings to dictionary for easier lookup.
				wlSettings[keyName] = wirelessSettings[i];
			}

			WirelessModule module = new WirelessModule();

			COMPONENT_SETTING wlBaseSetting;
			//COMPONENT_SETTING_FLOAT wlFloatSetting;
			COMPONENT_SETTING_INTEGER wlIntegerSetting;
			COMPONENT_SETTING_STRING wlStringSetting;

			// wireless settings
			wlIntegerSetting = wlSettings.TryGetValue( "WL_TRANSMISSION_INTERVAL", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_INTEGER : null;
			if ( wlIntegerSetting != null )
				module.TransmissionInterval = wlIntegerSetting.value;

			// iNet will NOT prefix encryption key "0x", e.g. "79054025255FB1A26E4BC422AEF54EB4"
			wlStringSetting = wlSettings.TryGetValue( "WL_ENCRYPTION_KEY", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_STRING : null;
			if ( wlStringSetting != null )
				module.EncryptionKey = wlStringSetting.value;

			wlIntegerSetting = wlSettings.TryGetValue( "WL_MESSAGE_HOPS", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_INTEGER : null;
			if ( wlIntegerSetting != null )
				module.MessageHops = wlIntegerSetting.value;

			wlIntegerSetting = wlSettings.TryGetValue( "WL_MAX_PEERS", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_INTEGER : null;
			if ( wlIntegerSetting != null )
				module.MaxPeers = wlIntegerSetting.value;

			wlIntegerSetting = wlSettings.TryGetValue( "WL_PRIMARY_PUBLIC_CHANNEL", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_INTEGER : null;
			if ( wlIntegerSetting != null )
				module.PrimaryChannel = (ushort)wlIntegerSetting.value;

			wlIntegerSetting = wlSettings.TryGetValue( "WL_SECONDARY_PUBLIC_CHANNEL", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_INTEGER : null;
			if ( wlIntegerSetting != null )
				module.SecondaryChannel = (ushort)wlIntegerSetting.value;

			// iNet will prefix encryption key "0x", e.g. "0xFFFF"
			wlStringSetting = wlSettings.TryGetValue( "WL_ACTIVE_CHANNEL_MASK", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_STRING : null;
			if ( wlStringSetting != null )
				module.ActiveChannelMask = wlStringSetting.value;

            wlStringSetting = wlSettings.TryGetValue( "WL_FEATURE_BITS", out wlBaseSetting ) ? wlBaseSetting as COMPONENT_SETTING_STRING : null;
            if (wlStringSetting != null)
                module.WirelessFeatureBits = wlStringSetting.value;

            wlIntegerSetting = wlSettings.TryGetValue("WL_SCRIPT_BINDING_TIMEOUT", out wlBaseSetting) ? wlBaseSetting as COMPONENT_SETTING_INTEGER : null;
            if (wlIntegerSetting != null)
                module.WirelessBindingTimeout = wlIntegerSetting.value;

            // iNet will prefix encryption key "0x", e.g. "0xFFFF"
            wlStringSetting = wlSettings.TryGetValue("WL_LISTENING_POST_CHANNEL_MASK", out wlBaseSetting) ? wlBaseSetting as COMPONENT_SETTING_STRING : null;
            if (wlStringSetting != null)
                module.ListeningPostChannelMask = wlStringSetting.value;

			// wireless options
			if ( wirelessOptions != null )
			{
				module.Options = new List<DeviceOption>( wirelessOptions.Length );

				foreach ( string wlOption in wirelessOptions )
				{
					DeviceOption deviceOption = new DeviceOption( wlOption, true );
					deviceOption.Enabled = true;
					module.Options.Add( deviceOption );
				}
			}

			return module;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        private DockingStation MakeDockingStationSettings( SETTINGS settings, List<DockingStationError> errors )
        {
            if ( settings == null || settings.dockingStationSettings == null )
                return null;

            DOCKING_STATION_SETTINGS dsSettings = settings.dockingStationSettings;

            DockingStation ds = new DockingStation();

            ds.RefId = dsSettings.ReferenceID;

            ds.SerialNumber = _configDS.SerialNumber;
            ds.Language.Code = dsSettings.language;
            ds.MenuLocked = dsSettings.menuLocked;
            ds.UseAudibleAlarm = dsSettings.useAudibleAlarm;

            ds.LogLevel = (LogLevel)Enum.Parse( typeof( LogLevel ), dsSettings.logLevel, true );
            ds.LogCapacity = dsSettings.logCapacity;

            // commented out because we don't allow the webapp security to be changed by iNet.
            // it can only be changed in Configurator.
            //ds.WebAppEnabled = dsSettings.webAppEnabled;
            //ds.WebAppPassword = dsSettings.webAppPassword;

            ds.InetUrl = dsSettings.iNetURL;
            ds.InetPingInterval = (short)dsSettings.iNetPingInterval;
            ds.InetTimeoutLow =  dsSettings.lowTimeout;
            ds.InetTimeoutMedium = dsSettings.mediumTimeout;
            ds.InetTimeoutHigh = dsSettings.highTimeout;
            ds.InetUserName = dsSettings.iNetUserName;
            ds.InetPassword = dsSettings.iNetPassword;

            ds.PrintPerformedBy = dsSettings.printPerformedBy;
            ds.PrintReceivedBy = dsSettings.printReceivedBy;
            ds.ClearPeaksUponDocking = dsSettings.clearPeaksOnDocking;
            ds.SingleSensorMode = dsSettings.allowSingleSensorMode;

            ds.PurgeAfterBump = dsSettings.purgeGasesAfterBump;

            ds.UseExpiredCylinders = dsSettings.useExpiredCylinders;
            ds.CombustibleBumpTestGas = dsSettings.lelBumpTestGas;

            ds.StopOnFailedBumpTest = dsSettings.stopOnFailedBumptest;
            ds.UpgradeOnErrorFail = dsSettings.upgradeOnErrorFail;

            if ( dsSettings.sensorCalibrationLimits != null )
                ds.SpanReserveThreshold = (double)(decimal)dsSettings.sensorCalibrationLimits.spanReserveThreshold;

            if ( dsSettings.port1AllowFreshAir && dsSettings.port1AllowZeroAir )
                ds.Port1Restrictions = PortRestrictions.FreshAir | PortRestrictions.ZeroAir;
            else if ( dsSettings.port1AllowFreshAir )
                ds.Port1Restrictions = PortRestrictions.FreshAir;
            else if ( dsSettings.port1AllowZeroAir )
                ds.Port1Restrictions = PortRestrictions.ZeroAir;
            else // if neither is specified, then default to Fresh
            {
                PortRestrictions defaultPortRestriction = PortRestrictions.FreshAir;
                string msg = string.Format( "Invalid Port1Restrictions ({0}) specified. Defaulting to {1}", ds.Port1Restrictions, defaultPortRestriction.ToString() );
                Log.Warning( "<INET: " + msg );
                errors.Add( new DockingStationError( msg, DockingStationErrorLevel.Warning ) );
                ds.Port1Restrictions = defaultPortRestriction;
            }

            if ( dsSettings.timezone != null )
            {
                int bias = dsSettings.timezone.bias;
                string stdName = dsSettings.timezone.standardName;
                SystemTime stdDate = new SystemTime();
                stdDate.DayOfWeek = (short)dsSettings.timezone.standardDate.dayOfWeek;
                stdDate.Day = (short)dsSettings.timezone.standardDate.day;
                stdDate.Month = (short)dsSettings.timezone.standardDate.month;
                stdDate.Hour = (short)dsSettings.timezone.standardDate.hour;
                int stdBias = dsSettings.timezone.standardBias;
                string dstName = dsSettings.timezone.daylightName;
                SystemTime dstDate = new SystemTime();
                dstDate.DayOfWeek = (short)dsSettings.timezone.daylightDate.dayOfWeek;
                dstDate.Day = (short)dsSettings.timezone.daylightDate.day;
                dstDate.Month = (short)dsSettings.timezone.daylightDate.month;
                dstDate.Hour = (short)dsSettings.timezone.daylightDate.hour;
                int dstBias = dsSettings.timezone.daylightBias;

                ds.TimeZoneInfo = new TimeZoneInfo( bias, stdName, stdDate, stdBias, dstName, dstDate, dstBias );
            }

            //Suresh 12-SEPTEMBER-2011 INS-2248
            // Server will only return network settings whenever this docking station is
            // being configured to replace another docking station.  In this situation,
            // this replacement docking station is supposed to adopt the settings of
            // the docking station it's replacing.
            if (dsSettings.networkSettings != null)
            {
                ds.ReplacedDSNetworkSettings = new DockingStation.NetworkInfo();
                ds.ReplacedDSNetworkSettings.DhcpEnabled = dsSettings.networkSettings.dhcpEnabled;
                ds.ReplacedDSNetworkSettings.IpAddress = dsSettings.networkSettings.ipAddress;
                ds.ReplacedDSNetworkSettings.SubnetMask = dsSettings.networkSettings.subnetMask;
                ds.ReplacedDSNetworkSettings.Gateway = dsSettings.networkSettings.gateway;
                ds.ReplacedDSNetworkSettings.DnsPrimary = dsSettings.networkSettings.dnsPrimary;
                ds.ReplacedDSNetworkSettings.DnsSecondary = dsSettings.networkSettings.dnsSecondary;
            }

            return ds;
        }

        /// <summary>
        /// Given an iNet event type code and an equipment type code, this
        /// method will return the event type code that the VDS instead use
        /// instead of the iNet event code.  The returned code may or may not
        /// be different than the passed in code. See 'remarks' section for why.
        /// </summary>
        /// <remarks>
        /// It's much easier for the VDS's business logic if each possible event type
        /// has a unique code.  e.g. it's difficult for the business logic to handle
        /// the fact that a Docking Station Diagnostic Event and an Instrument
        /// Diagnostic Event both have a code of "DIAG".  Therefore, the VDS itself
        /// uses unique event code.
        /// </remarks>
        /// <param name="inetEventCode"></param>
        /// <param name="equipmentTypeCode"></param>
        /// <returns></returns>
        private EventCode MapInetEventCodeToVdsEventCode( string inetEventCode, string equipmentTypeCode )
        {
            if ( equipmentTypeCode == EquipmentTypeCode.VDS )
                return EventCode.GetCachedCode( inetEventCode );

            if ( inetEventCode == EventCode.SettingsRead )
                return EventCode.GetCachedCode( EventCode.InstrumentSettingsRead );

            if ( inetEventCode == EventCode.SettingsUpdate )
                return EventCode.GetCachedCode( EventCode.InstrumentSettingsUpdate );

            if ( inetEventCode == EventCode.Diagnostics )
                return EventCode.GetCachedCode( EventCode.InstrumentDiagnostics );

            if ( inetEventCode == EventCode.FirmwareUpgrade )
                return EventCode.GetCachedCode( EventCode.InstrumentFirmwareUpgrade );

            return EventCode.GetCachedCode( inetEventCode );
        }

        private List<SensorCalibrationLimits> MakeSensorCalibrationLimits( List<SensorCalibrationLimits> sensorCalibrationLimits, SETTINGS settings )
        {
            if ( settings == null || settings.dockingStationSettings == null || settings.dockingStationSettings.sensorCalibrationLimits == null )
                return sensorCalibrationLimits;

            foreach ( SENSOR_AGE_SETTINGS sensorCalibrationLimit in settings.dockingStationSettings.sensorCalibrationLimits.sensorAgeSettings )
            {
                sensorCalibrationLimits.Add( new SensorCalibrationLimits( sensorCalibrationLimit.code, sensorCalibrationLimit.age ) );
            }

            return sensorCalibrationLimits;
        }

		// enum names should match web method names from the ConfigurationService
		private enum DownloaderWebMethod
		{
			Unknown,
			downloadCriticalErrors,
			downloadEventSchedules,
			downloadFirmwareUpdate,
			downloadKnownCylinders,
			downloadRecentEvents,
			downloadRemovedEquipment,
			downloadSettings,
			exchangeStatusInformation
		}

        //private EventCode MapVdsEventCodeToInetEventCode( string vdsEventCode, string equipmentTypeCode )
        //{
        //    if ( vdsEventCode == EventCode.InstrumentSettingsRead )
        //        return EventCode.GetCachedCode( EventCode.SettingsRead );

        //    if ( vdsEventCode == EventCode.InstrumentSettingsUpdate )
        //        return EventCode.GetCachedCode( EventCode.SettingsUpdate );

        //    if ( vdsEventCode == EventCode.InstrumentDiagnostics )
        //        return EventCode.GetCachedCode( EventCode.Diagnostics );

        //    if ( vdsEventCode == EventCode.InstrumentFirmwareUpgrade )
        //        return EventCode.GetCachedCode( EventCode.FirmwareUpgrade );

        //    return EventCode.GetCachedCode( vdsEventCode );
        //}

    } // end-class
}
