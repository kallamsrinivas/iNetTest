using System;
using ISC.iNet.DS.iNet.InetUpload;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.iNet
{
    internal class WebServiceLog
    {
        private WebServiceLog() { }

        static private bool DoLogging { get { return Log.Level >= LogLevel.Trace; } }

        internal static void LogSpecifiedProp(object val, bool isSpecified, string logHeader, string propName)
        {
            if ( !DoLogging )
                return;

            if ( isSpecified )
            {
                Log.Trace(logHeader + propName + "=" + val.ToString());
            }
            else
            {
                Log.Trace( logHeader + propName + "=NOT SPECIFIED" );
            }
        }

        private static void LogSpecifiedDate( System.Nullable<System.DateTime> dateVal, string logHeader, string propName )
        {
            LogSpecifiedDate( dateVal, true, logHeader, propName );
        }

        private static void LogSpecifiedDate( System.Nullable<System.DateTime> dateVal, bool isSpecified, string logHeader, string propName )
        {
            if ( isSpecified )
            {
                Log.Trace( string.Format( "{0}{1}=\"{2}\"", logHeader, propName, Log.DateTimeToString( dateVal ) ) );
            }
            else
            {
                Log.Trace( logHeader + propName + "=NOT SPECIFIED" );
            }
        }

        private static string FormatDateForLog( DateTime inputDate )
        {
            return Log.DateTimeToString( inputDate );
        }

        private static void LogPROPERTIES( PROPERTY[] properties, string logHeader )
        {
            if ( properties == null )
                return;

            string propertyHeader = logHeader + "PROP ";

			for ( int i = 0; i < properties.Length; i++ )
			{
				// some property values should not be displayed in the log
				if ( properties[i].propName == "WL_ENCRYPTION_KEY" )
					LogPROPERTY( properties[i], i, propertyHeader, true );
				else
					LogPROPERTY( properties[i], i, propertyHeader, false );
			}
        }

        private static void LogPROPERTY( PROPERTY prop, int i, string propertyHeader, bool isPrivate )
        {
			string propertyValue = isPrivate ? string.Empty.PadRight( prop.propValue.Length, '*' ) : prop.propValue;
			
			Log.Trace( propertyHeader + "index=" + i.ToString() );
            Log.Trace( propertyHeader + "propName=" + prop.propName );
			Log.Trace( propertyHeader + "propValue=" + propertyValue );
            Log.Trace( propertyHeader + "propType=" + prop.propType );
            Log.Trace( propertyHeader + "propGroup=" + prop.propGroup );
        }

        private static void LogCOMPONENT( COMPONENT comp, string logHeader )
        {
            string componentHeader = logHeader + "COMPONENT ";

            Log.Trace( componentHeader + "sn=" + comp.sn );
            Log.Trace( componentHeader + "uid=" + comp.uid );
            Log.Trace( componentHeader + "componentType=" + comp.componentType );
            Log.Trace( componentHeader + "componentCode=" + comp.componentCode );
            Log.Trace( componentHeader + "partNumber=" + comp.partNumber );
            Log.Trace( componentHeader + "manufacturerCode=" + comp.manufacturerCode );
            LogSpecifiedDate(comp.manufacturedDate, comp.manufacturedDateSpecified, componentHeader, "manufacturedDate");
            LogSpecifiedDate(comp.setupDate, comp.setupDateSpecified, componentHeader, "setupDate");

            // indent properties and cylinders
            logHeader = componentHeader + "    ";

            LogPROPERTIES( comp.property, logHeader );
        }

        private static void LogCYLINDER_GAS( CYLINDER_GAS gas, int i, string logHeader )
        {
            string cylindergasHeader = logHeader + "CYLINDER GAS ";

            Log.Trace( cylindergasHeader + "index=" + i.ToString() );
            Log.Trace( cylindergasHeader + "gasCode=" + gas.gasCode );
            Log.Trace( cylindergasHeader + "concentration=" + gas.concentration.ToString() );
        }

        private static void LogCYLINDER( CYLINDER cyl, int i, string logHeader )
        {
            string cylinderHeader = logHeader + "CYLINDER ";

            Log.Trace( cylinderHeader + "index=" + i.ToString() );
            Log.Trace( cylinderHeader + "position=" + cyl.position.ToString() );
            Log.Trace( cylinderHeader + "igas=" + cyl.igas.ToString() );
            Log.Trace( cylinderHeader + "manifold=" + cyl.manifold );
            Log.Trace( cylinderHeader + "cylinderId=" + cyl.cylinderId.ToString() );
            LogCOMPONENT((COMPONENT)cyl, cylinderHeader);
            LogSpecifiedDate(cyl.expirationDate, cyl.expirationDateSpecified, cylinderHeader, "expirationDate");
            Log.Trace( cylinderHeader + "cylinderCode=" + cyl.cylinderCode );
            Log.Trace( cylinderHeader + "currentPressure=" + cyl.currentPressure );
            LogSpecifiedDate(cyl.refillDate, cyl.refillDateSpecified, cylinderHeader, "refillDate");
            LogSpecifiedDate(cyl.installTime, cyl.installTimeSpecified, cylinderHeader, "installTime");

            cylinderHeader = cylinderHeader + "    ";

            if (cyl.cylinderGas != null)
            {
                for ( int g = 0; g < cyl.cylinderGas.Length; g++ )
                    LogCYLINDER_GAS(cyl.cylinderGas[g], g, cylinderHeader);
            }
        }

        private static void LogEQUIPMENT( EQUIPMENT eq, string logHeader )
        {
            //Log.Trace(logHeader + "uploadEventCode=" + eq.uploadEventCode);
            //Log.Trace(logHeader + "sn=" + eq.sn);
            //Log.Trace(logHeader + "accountId=" + eq.accountId);
            //Log.Trace( logHeader + "settingsRefId=" + eq.settingsRefId );
            //LogSpecifiedDate( eq.time, eq.timeSpecified, logHeader, "time" );
            //Log.Trace(logHeader + "partNumber=" + eq.partNumber);
            //Log.Trace(logHeader + "jobNumber=" + eq.jobNumber);
            //LogSpecifiedDate(eq.setupDate, eq.setupDateSpecified, logHeader, "setupDate");
            //Log.Trace(logHeader + "setupTech=" + eq.setupTech);
            //Log.Trace(logHeader + "languageCode=" + eq.languageCode);
            //Log.Trace(logHeader + "location=" + eq.location);
            //Log.Trace(logHeader + "softwareVersion=" + eq.softwareVersion);
            //Log.Trace(logHeader + "hardwareVersion=" + eq.hardwareVersion);
            //LogSpecifiedProp(eq.operationMinutes, eq.operationMinutesSpecified, logHeader, "operationMinutes");
            //Log.Trace(logHeader + "dataVersion=" + eq.dataVersion);
        }


        //call as : LogDOCKING_STATION( ds, WSP_LOG_MESSAGE_HEADER );
        internal static void LogDOCKING_STATION( DOCKING_STATION ds, string logHeader )
        {
            if ( !DoLogging )
                return;

            string dsHeader = logHeader + "DOCKING_STATION ";
            //LogEQUIPMENT((EQUIPMENT) ds, dsHeader );

            Log.Trace(dsHeader + "dockingStationCode=" + ds.dockingStationCode);
            Log.Trace(dsHeader + "dockingStationType=" + ds.dockingStationType);
            Log.Trace( dsHeader + "productGeneration=" + ds.productGeneration );
            Log.Trace(dsHeader + "serverSWVersion=" + ds.serverSWVersion);
            Log.Trace(dsHeader + "connectorVersion=" + ds.connectorVersion);
            LogSpecifiedProp(ds.activityMinutes, ds.activityMinutesSpecified, dsHeader, "activityMinutes");
            LogSpecifiedDate(ds.lastRebootDate, ds.lastRebootDateSpecified, dsHeader, "lastRebootDate");
            Log.Trace(dsHeader + "cluster=" + ds.cluster);
            Log.Trace(dsHeader + "timezone=" + ds.timezone);
            Log.Trace( dsHeader + "useExpiredCylinders=" + ds.useExpiredCylinders );
            Log.Trace( dsHeader + "lelBumpTestGas=" + ds.lelBumpTestGas );

            // indent properties and cylinders
            dsHeader = dsHeader + "    ";

            LogPROPERTIES( ds.property, dsHeader );

            if (ds.cylinder != null)
            {
                for ( int i = 0; i < ds.cylinder.Length; i++ )
                    LogCYLINDER(ds.cylinder[i], i, dsHeader);
            }
        }

        //call as : LogDOCKING_STATION( ds, WSP_LOG_MESSAGE_HEADER );
        internal static void LogDEBUG_LOG( DEBUG_LOG debugLog, string header )
        {
            if ( !DoLogging )
                return;

            string logHeader = header + "DEBUG_LOG ";

            Log.Trace( logHeader + "accountId=" + debugLog.accountId );
            Log.Trace( logHeader + "referenceId=" + debugLog.referenceId );
            Log.Trace( logHeader + "sn=" + debugLog.sn );
            LogSpecifiedDate( debugLog.time, header, logHeader + "time" );
            Log.Trace( logHeader + "log.Length=" + debugLog.log.Length + " characters" );
        }

        internal static void LogDATABASE_UPLOAD( DATABASE_UPLOAD dbUpload, string header )
        {
            if ( !DoLogging )
                return;

            string logHeader = header + "DEBUG_LOG ";

            Log.Trace( logHeader + "accountId=" + dbUpload.accountId );
            Log.Trace( logHeader + "referenceId=" + dbUpload.referenceId );
            Log.Trace( logHeader + "sn=" + dbUpload.sn );
            LogSpecifiedDate( dbUpload.time, header, logHeader + "time" );
            Log.Trace( logHeader + "databaseName=" + dbUpload.databaseName );
            Log.Trace( logHeader + "databaseFile.Length=" + dbUpload.databaseFile.Length );
        }

        private static void LogSENSOR( SENSOR sensor, int i, string header )
        {
            string sensorHeader = header + "SENSOR ";

            Log.Trace(sensorHeader + "index=" + i.ToString());
            LogCOMPONENT( (COMPONENT) sensor, sensorHeader);

            Log.Trace(sensorHeader + "position=" + sensor.position.ToString());
            LogSpecifiedDate(sensor.installTime, sensor.installTimeSpecified, sensorHeader, "installTime");
            LogSpecifiedDate(sensor.birthDate, sensor.birthDateSpecified, sensorHeader, "birthDate");
			LogSpecifiedProp(sensor.alarmAlert, sensor.alarmAlertSpecified, sensorHeader, "alarmAlert");
            LogSpecifiedProp(sensor.alarmLow, sensor.alarmLowSpecified, sensorHeader, "alarmLow");
            LogSpecifiedProp(sensor.alarmHigh, sensor.alarmHighSpecified, sensorHeader, "alarmHigh");
            LogSpecifiedProp(sensor.alarmTWA, sensor.alarmTWASpecified, sensorHeader, "alarmTWA");
            LogSpecifiedProp(sensor.alarmSTEL, sensor.alarmSTELSpecified, sensorHeader, "alarmSTEL");
            LogSpecifiedProp(sensor.overRange, sensor.overRangeSpecified, sensorHeader, "overRange");
            LogSpecifiedProp(sensor.calTimeOut, sensor.calTimeOutSpecified, sensorHeader, "calTimeOut");
            LogSpecifiedProp(sensor.polarity, sensor.polaritySpecified, sensorHeader, "polarity");
            LogSpecifiedProp(sensor.tempMax, sensor.tempMaxSpecified, sensorHeader, "tempMax");
            LogSpecifiedProp(sensor.tempMin, sensor.tempMinSpecified, sensorHeader, "tempMin");
            LogSpecifiedProp(sensor.peakReading, sensor.peakReadingSpecified, sensorHeader, "peakReading");
            Log.Trace(sensorHeader + "calGasCode=" + sensor.calGasCode);
            LogSpecifiedProp(sensor.calGasConcentration, sensor.calGasConcentrationSpecified, sensorHeader, "calGasConcentration");
            LogSpecifiedProp(sensor.unitOfMeasurement, sensor.unitOfMeasurementSpecified, sensorHeader, "unitOfMeasurement");
            Log.Trace(sensorHeader + "gasDecting=" + sensor.gasDecting);
            LogSpecifiedProp(sensor.responseFactor, sensor.responseFactorSpecified, sensorHeader, "responseFactor");
            Log.Trace(sensorHeader + "softwareVersion=" + sensor.softwareVersion);
            Log.Trace(sensorHeader + "hardwareVersion=" + sensor.hardwareVersion);
            Log.Trace(sensorHeader + "dataVersion=" + sensor.dataVersion);
            LogSpecifiedProp(sensor.spanCoefMin, sensor.spanCoefMinSpecified, sensorHeader, "spanCoefMin");
            LogSpecifiedProp(sensor.spanCoefMax, sensor.spanCoefMaxSpecified, sensorHeader, "spanCoefMax");
            LogSpecifiedProp(sensor.zeroMin, sensor.zeroMinSpecified, sensorHeader, "zeroMin");
            LogSpecifiedProp(sensor.zeroMax, sensor.zeroMaxSpecified, sensorHeader, "zeroMax");
            LogSpecifiedProp(sensor.deadBand, sensor.deadBandSpecified, sensorHeader, "deadBand");
            LogSpecifiedProp(sensor.filter.ToString(), sensor.filterSpecified, sensorHeader, "filter");
            LogSpecifiedProp(sensor.tempCompLow, sensor.tempCompLowSpecified, sensorHeader, "tempCompLow");
            LogSpecifiedProp(sensor.tempCompHigh, sensor.tempCompHighSpecified, sensorHeader, "tempCompHigh");
            LogSpecifiedProp(sensor.tempCompM50, sensor.tempCompM50Specified, sensorHeader, "tempCompM50");
            LogSpecifiedProp(sensor.tempCompM40, sensor.tempCompM40Specified, sensorHeader, "tempCompM40");
            LogSpecifiedProp(sensor.tempCompM30, sensor.tempCompM30Specified, sensorHeader, "tempCompM30");
            LogSpecifiedProp(sensor.tempCompM20, sensor.tempCompM20Specified, sensorHeader, "tempCompM20");
            LogSpecifiedProp(sensor.tempCompM10, sensor.tempCompM10Specified, sensorHeader, "tempCompM10");
            LogSpecifiedProp(sensor.tempCompZ0, sensor.tempCompZ0Specified, sensorHeader, "tempCompZ0");
            LogSpecifiedProp(sensor.tempCompP10, sensor.tempCompP10Specified, sensorHeader, "tempCompP10");
            LogSpecifiedProp(sensor.tempCompP20, sensor.tempCompP20Specified, sensorHeader, "tempCompP20");
            LogSpecifiedProp(sensor.tempCompP30, sensor.tempCompP30Specified, sensorHeader, "tempCompP30");
            LogSpecifiedProp(sensor.tempCompP40, sensor.tempCompP40Specified, sensorHeader, "tempCompP40");
            LogSpecifiedProp(sensor.tempCompP50, sensor.tempCompP50Specified, sensorHeader, "tempCompP50");
            LogSpecifiedProp(sensor.tempCompP60, sensor.tempCompP60Specified, sensorHeader, "tempCompP60");
            LogSpecifiedProp(sensor.tempCompP70, sensor.tempCompP70Specified, sensorHeader, "tempCompP70");
            LogSpecifiedProp(sensor.displayDecimal, sensor.displayDecimalSpecified, sensorHeader, "displayDecimal");
        }

        private static void LogBATTERY( BATTERY bat, int i, string logHeader )
        {
            string batteryHeader = logHeader + "BATTERY ";

            Log.Trace(batteryHeader + "index=" + i.ToString());
            LogCOMPONENT( (COMPONENT) bat, batteryHeader );
            Log.Trace(batteryHeader + "position=" + bat.position.ToString());
            LogSpecifiedDate(bat.installTime, bat.installTimeSpecified, batteryHeader, "installTime");
            LogSpecifiedProp(bat.operationMinutes, bat.operationMinutesSpecified, batteryHeader, "operationMinutes");
            Log.Trace(batteryHeader + "dataVersion=" + bat.dataVersion.ToString());
            Log.Trace(batteryHeader + "softwareVersion=" + bat.softwareVersion.ToString());
            Log.Trace(batteryHeader + "hardwareVersion=" + bat.hardwareVersion.ToString());
        }

        private static void LogWIRELESS_MODULE( WIRELESS_MODULE wm, int i, string logHeader )
        {
            string wirelessHeader = logHeader + "WIRELESS_MODULE ";

            Log.Trace( wirelessHeader + "index=" + i.ToString() );
            LogCOMPONENT( (COMPONENT)wm, wirelessHeader );
            Log.Trace( wirelessHeader + "position=" + wm.position.ToString() );
            Log.Trace( wirelessHeader + "macAddress=" + wm.macAddress );
            Log.Trace( wirelessHeader + "softwareVersion=" + wm.softwareVersion );
            Log.Trace( wirelessHeader + "status=" + wm.status );
            Log.Trace( wirelessHeader + "transmissionInterval=" + wm.transmissionInterval.ToString() );
        }

		internal static void LogACCESSORY( ACCESSORY accessory, string logHeader )
		{
			if ( !DoLogging )
				return;

			string accHeader = logHeader + "ACCESSORY ";
			//LogEQUIPMENT( (EQUIPMENT)accessory, accHeader );

			Log.Trace( accHeader + "equipmentCode=" + accessory.equipmentCode );
			Log.Trace( accHeader + "lastDSSN=" + accessory.lastDSSN );

			LogPROPERTIES( accessory.property, logHeader );
		}

        //call as : LogINSTRUMENT( inst, WSP_LOG_MESSAGE_HEADER );
        internal static void LogINSTRUMENT( INSTRUMENT inst, string logHeader )
        {
            if ( !DoLogging )
                return;
			
			string instHeader = logHeader + "INSTRUMENT ";
            //LogEQUIPMENT((EQUIPMENT) inst, instHeader );

            Log.Trace(instHeader + "instrumentCode=" + inst.instrumentCode);
			Log.Trace( instHeader + "instrumentSubtype=" + inst.instrumentSubType );
#if DEBUG // don't show access codes in release builds.
            Log.Trace( instHeader + "accessCode=" + inst.accessCode );
#else
			if ( inst.accessCode != null )
			{
				Log.Trace( instHeader + "accessCode=" + string.Empty.PadRight( inst.accessCode.Length, '*' ) );
			}
			else
			{
				Log.Trace( instHeader + "accessCode=" );
			}
#endif
            Log.Trace(instHeader + "alarms=" + inst.alarms);
            //Log.Trace(instHeader + "status=" + inst.status);
            Log.Trace(instHeader + "user=" + inst.user);
            LogSpecifiedProp(inst.twaTimeBase, inst.twaTimeBaseSpecified, instHeader, "twaTimeBase");
            LogSpecifiedProp(inst.totalAlarmMinutes, inst.totalAlarmMinutesSpecified, instHeader, "totalAlarmMinutes");
            Log.Trace(instHeader + "lastDSSN=" + inst.lastDSSN);
            LogSpecifiedDate(inst.lastDockedTime, inst.lastDockedTimeSpecified, instHeader, "lastDockedTime");
            LogSpecifiedProp(inst.latitude, inst.latitudeSpecified, instHeader, "latitude");
            LogSpecifiedProp(inst.longitude, inst.longitudeSpecified, instHeader, "longitude");
            LogSpecifiedProp(inst.altitude, inst.altitudeSpecified, instHeader, "altitude");
			Log.Trace( instHeader + "calInterval=" + inst.calInterval );
			Log.Trace( instHeader + "bumpInterval=" + inst.bumpInterval );

            // indent properties and cylinders
            instHeader = instHeader + "    ";

            LogPROPERTIES( inst.property, logHeader );

            if ( inst.sensor != null )
            {
                for ( int i = 0; i < inst.sensor.Length; i++ )
                    LogSENSOR( inst.sensor[i], i, instHeader );
            }

            if ( inst.battery != null )
            {
                for ( int i = 0; i < inst.battery.Length; i++)
                    LogBATTERY( inst.battery[i], i, instHeader );
            }

            if ( inst.wirelessModule != null )
            {
                for ( int i = 0; i < inst.wirelessModule.Length; i++ )
                    LogWIRELESS_MODULE( inst.wirelessModule[i], i, instHeader );
            }

            if ( inst.cylinder != null )
            {
                for ( int i = 0; i < inst.cylinder.Length; i++)
                    LogCYLINDER( inst.cylinder[i], i, instHeader );
            }

            if ( inst.Users != null )
            {
                foreach ( string user in inst.Users )
                    Log.Trace( instHeader + "User=" + user );
            }

            if ( inst.Sites != null )
            {
                foreach ( string site in inst.Sites )
                    Log.Trace( instHeader + "Sites=" + site );
            }
        }

        private static void LogCYLINDER_USED( CYLINDER_USED cylUsed, int i, string logHeader )
        {
            string cylinderUsedHeader = logHeader + "CYLINDER_USED ";

            Log.Trace(cylinderUsedHeader + "index=" + i.ToString());
            Log.Trace(cylinderUsedHeader + "uid=" + cylUsed.uid.ToString());
            Log.Trace(cylinderUsedHeader + "gasCode=" + cylUsed.gasCode.ToString());
            LogSpecifiedProp(cylUsed.concentration, cylUsed.concentrationSpecified, cylinderUsedHeader, "concentration");
            Log.Trace(cylinderUsedHeader + "purpose=" + cylUsed.purpose.ToString());
            LogSpecifiedProp(cylUsed.secondsOn, cylUsed.secondsOnSpecified, cylinderUsedHeader, "secondsOn");
        }

        private static void LogSENSOR_CALIBRATION( SENSOR_CALIBRATION senCal, int i, string logHeader )
        {
            string sensorCalHeader = logHeader + "SENSOR_CALIBRATION ";

            Log.Trace(sensorCalHeader + "index=" + i.ToString());
            Log.Trace(sensorCalHeader + "uid=" + senCal.uid.ToString());
            LogSpecifiedDate(senCal.time, senCal.timeSpecified, sensorCalHeader, "time");
            LogSpecifiedProp(senCal.pass, senCal.passSpecified, sensorCalHeader, "pass");
            Log.Trace(sensorCalHeader + "calStatus=" + senCal.calStatus.ToString());
            LogSpecifiedProp(senCal.spanReading, senCal.spanReadingSpecified, sensorCalHeader, "spanReading");
            LogSpecifiedProp(senCal.spanCoef, senCal.spanCoefSpecified, sensorCalHeader, "spanCoef");
            LogSpecifiedProp(senCal.zeroOffset, senCal.zeroOffsetSpecified, sensorCalHeader, "zeroOffset");
            LogSpecifiedProp(senCal.baseline, senCal.baselineSpecified, sensorCalHeader, "baseline");
            LogSpecifiedProp(senCal.duration, senCal.durationSpecified, sensorCalHeader, "duration");
            LogSpecifiedProp(senCal.position, (senCal.position != null), sensorCalHeader, "position");

            LogSpecifiedProp(senCal.readingAfterZero, senCal.readingAfterZeroSpecified, sensorCalHeader, "readingAfterZero");
            LogSpecifiedDate(senCal.timeAfterZero, senCal.timeAfterZeroSpecified, sensorCalHeader, "timeAfterZero");
            LogSpecifiedProp(senCal.readingAfterPrecondition, senCal.readingAfterPreconditionSpecified, sensorCalHeader, "readingAfterPrecondition");
            LogSpecifiedDate(senCal.timeAfterPrecondition, senCal.timeAfterPreconditionSpecified, sensorCalHeader, "timeAfterPrecondition");
            LogSpecifiedProp(senCal.readingAfterPurge, senCal.readingAfterPurgeSpecified, sensorCalHeader, "readingAfterPurge");
            LogSpecifiedDate(senCal.timeAfterPurge, senCal.timeAfterPurgeSpecified, sensorCalHeader, "timeAfterPurge");
            LogSpecifiedProp(senCal.cumulativeResponseTime, senCal.cumulativeResponseTimeSpecified, sensorCalHeader, "cumulativeResponseTime");

            // indent cylinders used
            sensorCalHeader = sensorCalHeader + "    ";

            if (senCal.cylinderUsed != null)
            {
                for ( int c = 0; c < senCal.cylinderUsed.Length; c++ )
                    LogCYLINDER_USED(senCal.cylinderUsed[c], c, sensorCalHeader);
            }
        }

        //call as : LogINSTRUMENT_CALIBRATION( instCal, WSP_LOG_MESSAGE_HEADER );
        internal static void LogINSTRUMENT_CALIBRATION( INSTRUMENT_CALIBRATION instCal, string logHeader )
        {
            if ( !DoLogging )
                return;

            string instCalHeader = logHeader + "INSTRUMENT_CALIBRATION ";

            Log.Trace(instCalHeader + "accountId=" + instCal.accountId);
            Log.Trace( instCalHeader + "scheduleRefId=" + instCal.scheduleRefId );
            LogSpecifiedDate( instCal.eventTime, instCal.eventTimeSpecified, instCalHeader, "eventTime" );
            LogSpecifiedDate( instCal.time, instCal.timeSpecified, instCalHeader, "time" );
            Log.Trace(instCalHeader + "sn=" + instCal.sn);
            LogSpecifiedProp(instCal.pass, instCal.passSpecified, instCalHeader, "pass");
            Log.Trace(instCalHeader + "dsSn=" + instCal.dsSn);
            Log.Trace(instCalHeader + "trigger=" + instCal.trigger);
            LogSpecifiedProp(instCal.nextCalTime, instCal.nextCalTimeSpecified, instCalHeader, "nextCalTime"); 

            // indent properties and sensor calibrations
            instCalHeader = instCalHeader + "    ";

            LogPROPERTIES( instCal.property, instCalHeader );

            if (instCal.sensorCalibration != null)
            {
                for (int i = 0; i < instCal.sensorCalibration.Length; i++)
                    LogSENSOR_CALIBRATION(instCal.sensorCalibration[i], i, instCalHeader);
            }
        }

        private static void LogSENSOR_BUMP_TEST( SENSOR_BUMP_TEST senBump, int i, string logHeader )
        {
            string sensorBumpHeader = logHeader + "SENSOR_BUMP_TEST ";

            Log.Trace(sensorBumpHeader + "index=" + i.ToString());
            Log.Trace(sensorBumpHeader + "uid=" + senBump.uid.ToString());
            LogSpecifiedDate(senBump.time, senBump.timeSpecified, sensorBumpHeader, "time");
            LogSpecifiedProp(senBump.pass, senBump.passSpecified, sensorBumpHeader, "pass");
            Log.Trace( sensorBumpHeader + "bumpStatus=" + senBump.bumpStatus );
            Log.Trace( sensorBumpHeader + "highReading=" + senBump.highReading );

            LogSpecifiedProp(senBump.duration, senBump.durationSpecified, sensorBumpHeader, "duration");
            LogSpecifiedProp(senBump.position, (senBump.position != null), sensorBumpHeader, "position");

            LogSpecifiedProp(senBump.cumulativeResponseTime, senBump.cumulativeResponseTimeSpecified, sensorBumpHeader, "cumulativeResponseTime");

            // indent cylinders used
            sensorBumpHeader = sensorBumpHeader + "    ";

            if (senBump.cylinderUsed != null)
            {
                 for ( int c = 0; c < senBump.cylinderUsed.Length; c++ )
                    LogCYLINDER_USED(senBump.cylinderUsed[c], c, sensorBumpHeader);
            }
        }

        //call as : LogINSTRUMENT_BUMP_TEST( instBump, WSP_LOG_MESSAGE_HEADER );
        internal static void LogINSTRUMENT_BUMP_TEST( INSTRUMENT_BUMP_TEST instBump, string logHeader )
        {
            if ( !DoLogging )
                return;

            string instBumpHeader = logHeader + "INSTRUMENT_BUMP_TEST ";

            Log.Trace(instBumpHeader + "accountId=" + instBump.accountId);
            Log.Trace( instBumpHeader + "scheduleRefId=" + instBump.scheduleRefId );
            LogSpecifiedDate( instBump.eventTime, instBump.eventTimeSpecified, instBumpHeader, "eventTime" );
            LogSpecifiedDate( instBump.time, instBump.timeSpecified, instBumpHeader, "time" );
            Log.Trace(instBumpHeader + "sn=" + instBump.sn);
            LogSpecifiedProp(instBump.pass, instBump.passSpecified, instBumpHeader, "pass");
            Log.Trace(instBumpHeader + "dsSn=" + instBump.dsSn);
            Log.Trace(instBumpHeader + "trigger=" + instBump.trigger);
            LogSpecifiedProp(instBump.nextBumpTime, instBump.nextBumpTimeSpecified, instBumpHeader, "nextBumpTime");

            // indent properties and sensor bumps
            instBumpHeader = instBumpHeader + "    ";

            LogPROPERTIES( instBump.property, instBumpHeader );

            if (instBump.sensorBumpTest != null)
            {
                for ( int i = 0; i < instBump.sensorBumpTest.Length; i++ )
                    LogSENSOR_BUMP_TEST(instBump.sensorBumpTest[i], i, instBumpHeader);
            }
        }

        private static void LogERROR_DATA( ERROR_DATA errData, int i, string logHeader )
        {
            string errorDataHeader = logHeader + "ERROR_DATA ";
            Log.Trace(errorDataHeader + "index=" + i.ToString());
            LogSpecifiedDate( errData.errorTime, errData.errorTimeSpecified, errorDataHeader, "errorTime" );
            Log.Trace(errorDataHeader + "errorCode=" + errData.errorCode);
            Log.Trace(errorDataHeader + "errorDetail=" + errData.errorDetail);
            LogSpecifiedDate(errData.errorTime, errData.errorTimeSpecified, errorDataHeader, "errorTime");
        }

        //call as : LogERROR( err, WSP_LOG_MESSAGE_HEADER );
        internal static void LogERROR( ERROR err, string logHeader )
        {
            if ( !DoLogging )
                return;

            string errorHeader = logHeader + "ERROR ";

            Log.Trace(errorHeader + "accountId=" + err.accountId);
            Log.Trace( errorHeader + "dsNn=" + err.dsSn );
            Log.Trace(errorHeader + "sn=" + err.sn);
            Log.Trace(errorHeader + "type=" + err.type);
            LogSpecifiedDate(err.time, err.timeSpecified, errorHeader, "time");

            // indent error data
            errorHeader = errorHeader + "    ";

            if (err.errorData != null)
            {
                for ( int i = 0; i < err.errorData.Length; i++ )
                    LogERROR_DATA(err.errorData[i], i, errorHeader);
            }
        }

        private static void LogDIAGNOSTIC_DATA( DIAGNOSTIC_DATA diagData, int i, string logHeader )
        {
            string diagDataHeader = logHeader + "DIAGNOSTIC_DATA ";

            Log.Trace(diagDataHeader + "index=" + i.ToString());
            Log.Trace(diagDataHeader + "diagName=" + diagData.diagName);
            Log.Trace(diagDataHeader + "diagValue=" + diagData.diagValue);
        }

        //call as : LogDIAGNOSTIC( diag, WSP_LOG_MESSAGE_HEADER );
        internal static void LogDIAGNOSTIC( DIAGNOSTIC diag, string logHeader )
        {
            if ( !DoLogging )
                return;

            string diagHeader = logHeader + "DIAGNOSTIC ";

            Log.Trace(diagHeader + "accountId=" + diag.accountId);
            Log.Trace( diagHeader + "scheduleRefId=" + diag.scheduleRefId );
            Log.Trace(diagHeader + "dsSn=" + diag.dsSn);
            Log.Trace(diagHeader + "sn=" + diag.sn);
            Log.Trace(diagHeader + "type=" + diag.type);
            LogSpecifiedDate(diag.time, diag.timeSpecified, diagHeader, "time");

            // indent properties and sensor bumps
            diagHeader = diagHeader + "    ";

            LogPROPERTIES( diag.property, diagHeader );

            if (diag.diagnosticData != null)
            {
                for ( int i = 0; i < diag.diagnosticData.Length; i++ )
                    LogDIAGNOSTIC_DATA(diag.diagnosticData[i], i, diagHeader);
            }
        }

        private static void LogDATALOG_READING( DATALOG_READING dlReading, int i, string logHeader )
        {
            string datalogReadingHeader = logHeader + "DATALOG_READING ";

            Log.Trace(datalogReadingHeader + "index=" + i.ToString());
            LogSpecifiedProp(dlReading.sequence, dlReading.sequenceSpecified, datalogReadingHeader, "sequence");
            LogSpecifiedProp(dlReading.temperature, dlReading.temperatureSpecified, datalogReadingHeader, "temperature");
            LogSpecifiedProp(dlReading.rawReading, dlReading.rawReadingSpecified, datalogReadingHeader, "rawReading");
            LogSpecifiedProp(dlReading.count, dlReading.countSpecified, datalogReadingHeader, "count");
        }

        private static void LogDATALOG_PERIOD( DATALOG_PERIOD dlPeriod, int i, string logHeader )
        {
            string datalogPeriodHeader = logHeader + "DATALOG_PERIOD ";

            Log.Trace(datalogPeriodHeader + "index=" + i.ToString());
            LogSpecifiedProp(dlPeriod.period, dlPeriod.periodSpecified, datalogPeriodHeader, "period");
            LogSpecifiedDate(dlPeriod.time, dlPeriod.timeSpecified, datalogPeriodHeader, "time");
            Log.Trace(datalogPeriodHeader + "location=" + dlPeriod.location);
#if DEBUG  // never log the readings in a release build.  That's a lot of data to log.
            if ( DoLogging )
            {
                // indent datalog reading
                datalogPeriodHeader = datalogPeriodHeader + "    ";

                if ( dlPeriod.reading != null )
                {
                    for ( int r = 0; r < dlPeriod.reading.Length; r++ )
                        LogDATALOG_READING( dlPeriod.reading[r], r, datalogPeriodHeader );
                }
            }
#endif
        }

        private static void LogDATALOG_SENSOR_SESSION(DATALOG_SENSOR_SESSION dlSensorSession, int i, string logHeader)
        {
            string senSessionHeader = logHeader + "DATALOG_SENSOR_SESSION ";

            Log.Trace(senSessionHeader + "index=" + i.ToString());
            Log.Trace(senSessionHeader + "uid=" + dlSensorSession.uid);
            Log.Trace(senSessionHeader + "gasCode=" + dlSensorSession.gasCode);
            Log.Trace(senSessionHeader + "sensorCode=" + dlSensorSession.sensorCode);
            LogSpecifiedProp(dlSensorSession.alarmLow, dlSensorSession.alarmLowSpecified, senSessionHeader, "alarmLow");
            LogSpecifiedProp(dlSensorSession.alarmHigh, dlSensorSession.alarmHighSpecified, senSessionHeader, "alarmHigh");
            LogSpecifiedProp(dlSensorSession.alarmTWA, dlSensorSession.alarmTWASpecified, senSessionHeader, "alarmTWA");
            LogSpecifiedProp(dlSensorSession.alarmSTEL, dlSensorSession.alarmSTELSpecified, senSessionHeader, "alarmSTEL");
            LogSpecifiedProp(dlSensorSession.exposureSD, dlSensorSession.exposureSDSpecified, senSessionHeader, "exposureSD");
            Log.Trace(senSessionHeader + "status=" + dlSensorSession.status);
            Log.Trace(senSessionHeader + "responseFactorName=" + dlSensorSession.responseFactorName.ToString());
            LogSpecifiedProp(dlSensorSession.responseFactorValue, dlSensorSession.responseFactorValueSpecified, senSessionHeader, "responseFactorValue");

            // indent datalog period
            senSessionHeader = senSessionHeader + "    ";

            if (dlSensorSession.readingPeriod != null)
            {
                for ( int p = 0; p < dlSensorSession.readingPeriod.Length; p++ )
                    LogDATALOG_PERIOD(dlSensorSession.readingPeriod[p], p, senSessionHeader);
            }
        }

        //call as : LogDATALOG_SESSION( dlSession, WSP_LOG_MESSAGE_HEADER );
        internal static void LogDATALOG_SESSION( DATALOG_SESSION dlSession, string logHeader )
        {
            if ( !DoLogging )
                return;

            string datalogSessionHeader = logHeader + "DATALOG_SESSION ";

            Log.Trace(datalogSessionHeader + "accountId=" + dlSession.accountId);
            Log.Trace( datalogSessionHeader + "scheduleRefId=" + dlSession.scheduleRefId );
            LogSpecifiedDate( dlSession.eventTime, dlSession.eventTimeSpecified, datalogSessionHeader, "eventTime" );
            Log.Trace( datalogSessionHeader + "dsSn=" + dlSession.dsSn );
            Log.Trace(datalogSessionHeader + "sn=" + dlSession.sn);
            LogSpecifiedProp(dlSession.twaTimeBase, dlSession.twaTimeBaseSpecified, datalogSessionHeader, "twaTimeBase");
            Log.Trace(datalogSessionHeader + "user=" + dlSession.user);
            LogSpecifiedDate(dlSession.sessionDate, dlSession.sessionDateSpecified, datalogSessionHeader, "sessionDate");
            LogSpecifiedProp(dlSession.sessionNum, dlSession.sessionNumSpecified, datalogSessionHeader, "sessionNum");
            LogSpecifiedProp(dlSession.recordingInterval, dlSession.recordingIntervalSpecified, datalogSessionHeader, "recordingInterval");
            Log.Trace(datalogSessionHeader + "comments=" + dlSession.comments);
            LogPROPERTIES( dlSession.properties, datalogSessionHeader );

            // indent datalog sensor session
            datalogSessionHeader = datalogSessionHeader + "    ";

            if (dlSession.sensorSession != null)
            {
                for ( int i = 0; i < dlSession.sensorSession.Length; i++ )
                    LogDATALOG_SENSOR_SESSION(dlSession.sensorSession[i], i, datalogSessionHeader);
            }
        }

        internal static void LogALARMEVENT( ALARM_EVENT alarmEvent, string logHeader )
        {
            if ( !DoLogging )
                return;

            string header = logHeader + "ALARM_EVENT ";

            Log.Trace( header + "accountId=" + alarmEvent.accountId );
            Log.Trace( header + "scheduleRefId=" + alarmEvent.scheduleRefId );
            LogSpecifiedDate( alarmEvent.eventTime, header, "eventTime" );
            Log.Trace( header + "sn=" + alarmEvent.sn );
            Log.Trace( header + "dsSn=" + alarmEvent.dsSn );
            Log.Trace( header + "alarmLow=" + alarmEvent.alarmLow );
            Log.Trace( header + "alarmHigh=" + alarmEvent.alarmHigh );
            LogSpecifiedDate( alarmEvent.time, header, "time" );
            Log.Trace( header + "duration=" + alarmEvent.duration );
            Log.Trace( header + "peakReading=" + alarmEvent.peakReading );
            Log.Trace( header + "sensorUid=" + alarmEvent.sensorUid );
            Log.Trace( header + "gasCode=" + alarmEvent.gasCode );
            Log.Trace( header + "user=" + alarmEvent.user );
            Log.Trace( header + "site=" + alarmEvent.site );
            
            LogPROPERTIES( alarmEvent.properties, header );
        }
    } // end-class


}
