using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;
using ISC.iNet.DS.DataAccess;

namespace ISC.iNet.DS.Services
{
    public partial class InstrumentCalibrationOperation
    {
		/// <summary>
		/// The GasEndPoints handed to this Operation during its instantiation will be segregrated
		/// into being used for multiple calibration "passes". During each "pass", a subset
		/// of all the installed sensors will be calibrated using the GasEndPoint assigned to that pass.
		/// </summary>
		private List<GasEndPoint> _passesEndPointList = new List<GasEndPoint>();

        protected internal enum SensorCalOpStatus
        {
            NotDone = 0,
            Skipped = 1,
            Preconditioning = 2,
            PreconditionSkipped = 3,
            PreconditionFailed = 4,
            PreconditionPassed = 5,
            Calibrating = 6,
            CalibrationTimedOut = 7,
            CalibrationFailed = 8,
            CalibrationPassed = 9

        }

        protected internal class CalibrationSensorInfo
        {
            protected internal const int PASS_UNKNOWN = -1;
            protected internal const int PASS_SENSOR_FAILED_ZERO = -2;
            protected internal const int PASS_SENSOR_NOT_CAL_ENABLED = -3;
            protected internal const int PASS_SENSOR_SKIPPED = -4;
            protected internal const int PASS_CAL_GAS_UNAVAILABLE = -5;
            protected internal const int PASS_SENSOR_EXPIRED = -6;

            protected internal InstalledComponent InstalledComponent { get; private set; }
            //protected internal Sensor Sensor { get; set; }
            protected internal SensorGasResponse SGR { get; set; }
            protected internal string GasCode { get; set; }
            protected internal TimeSpan CalTimeOut { get; set; }
            protected internal double GasConcentration { get; set; }
            protected internal double MaximumReading { get; set; }
            protected internal int FlowRate { get; set; }
            protected internal int TotalReadings { get; set; }
            protected internal long PreconditionPauseTime { get; set; }
            protected internal int CalPreconditionFlowRate { get; set; }
            protected internal TimeSpan PreconditionTimeOut { get; set; }
            protected internal Status OriginalStatus { get; set; }
            protected internal int PreconditionTotalReadings { get; set; }
            protected internal int PreconditionOddReadings { get; set; }
            protected internal int PreconditionGoodReadings { get; set; }
            protected internal int PassIndex { get; set; }
            protected internal SensorCalOpStatus OpStatus { get; set; }

            protected internal CalibrationSensorInfo( InstalledComponent installedComponent )
            {
                InstalledComponent = installedComponent;
                GasCode = string.Empty;
                CalTimeOut = TimeSpan.Zero;
                PreconditionTimeOut = TimeSpan.Zero;
                PassIndex = PASS_UNKNOWN;
                OpStatus = SensorCalOpStatus.NotDone;
            }

			/// <summary>
			/// Used to sort a list of CalibrationSensorInfos. This comparer ensures that
			/// the CalibrationSensorInfos are sorted first by PassIndex, and then by 
			/// Position within each PassIndex.
			/// </summary>
			/// <param name="csi1"></param>
			/// <param name="csi2"></param>
			/// <returns></returns>
			static internal int Compare( CalibrationSensorInfo csi1, CalibrationSensorInfo csi2 )
			{
				if ( csi1.PassIndex < csi2.PassIndex ) return -1;
				if ( csi1.PassIndex > csi2.PassIndex ) return 1;

				if ( csi1.InstalledComponent.Position < csi2.InstalledComponent.Position ) return -1;
				if ( csi1.InstalledComponent.Position < csi2.InstalledComponent.Position ) return -1;

				return 0;
			}
        }

		/// <summary>
		/// Returns the "pass" that the GasEndPoint has been assigned.
		/// -1 is returned if the GasEndPoint has not yet been assigned to a pass.
		/// </summary>
		/// <param name="gasEndPoint"></param>
		/// <returns></returns>
		protected internal int GetGasEndPointPass( GasEndPoint gasEndPoint )
		{
			for ( int i = 0; i < _passesEndPointList.Count; i++ )
			{
				if ( _passesEndPointList[ i ] == gasEndPoint )
					return i + 1; // our bump passes "passes" are one-based.
			}
			return -1;
		}

		/// <summary>
		/// Returns the GasEndPoint that is to be used for the specified "pass".
		/// null is returned if no GasEndPoint has been specified for the pass.
		/// </summary>
		/// <param name="pass"></param>
		/// <returns></returns>
		protected internal GasEndPoint GetPassGasEndPoint( int pass )
		{
			pass--; // PassIndexes are one-based; we need to convert to zero-based before accessing our List.
			return ( pass < 0 || pass >= _passesEndPointList.Count ) ? null : _passesEndPointList[ pass ];
		}

        /// <summary>
        /// Currently only for TX1 and Ventis Pro.
        /// Theoretically could work for other instruments if they supported quick cal.
        /// </summary>
        protected internal void CalibrateInstrumentParallel()
        {
            #region Variable Declarations
            bool flowFailed = false;
            Exception thrownException = null;
			GasEndPoint gasEndPoint = null;
			GasEndPoint emptyGasEndPoint = null;
            bool isCalibrationStarted = false;
            string logLabel = "ParallelCAL: ";
			#endregion Variable Declarations

			try
            {
                do
				{
					#region --------------- INITIALIZATION TASKS -----------------
				 
					gasEndPoint = null;
                    isCalibrationStarted = false;
                    int numberSensorsToCalibrate = 0;
                    int numberPasses = 0;
             
					List<CalibrationSensorInfo>  calSensorInfoList = new List<CalibrationSensorInfo>();
                    List<SensorCalibrationLimits> sensorCalibrationLimits = _testOnlySensorCalibrationLimits == null ? new SensorCalibrationLimitsDataAccess().FindAll() : _testOnlySensorCalibrationLimits;

                    // Let the initialization begin!
                    foreach ( InstalledComponent ic in _returnEvent.DockedInstrument.InstalledComponents.FindAll( c => c.Component is Sensor ) )
                    {
						Sensor sensor = (Sensor)ic.Component;
						if ( !sensor.Enabled ) continue; // Skip sensors that are not enabled.

                        // Create a CalibrationSensorInfo to record information on the current sensor.
                        CalibrationSensorInfo csi = new CalibrationSensorInfo( ic );
                        calSensorInfoList.Add( csi );

                        // Populate properties of the CalibrationSensorInfo to represent the current sensor.
                        csi.SGR = _returnEvent.GetSensorGasResponseByUid( sensor.Uid );
                        csi.GasCode = sensor.GetGasToCal();
                        csi.CalTimeOut = new TimeSpan(0,0,sensor.CalibrationTimeout);
                        csi.GasConcentration = sensor.CalibrationGasConcentration;
                        csi.PreconditionPauseTime = sensor.CalPreconditionPauseTime;
                        csi.CalPreconditionFlowRate = _instrumentController.GetSensorPreconditionFlowRate( ic );
                        csi.PreconditionTimeOut = _instrumentController.GetSensorPreconditionTimeout( ic );

                        // Get the gas end point to be used for this sensor.  Also, record which pass of the calibration will process this sensor.
                        ResetTriedGasEndPoints();
                        gasEndPoint = GetSensorGasEndPoint( ic );

						int passIndex = GetGasEndPointPass( gasEndPoint );
						if ( passIndex < 0 )
						{
							_passesEndPointList.Add( gasEndPoint );
							passIndex = _passesEndPointList.Count; // PassIndexes are one-based, so just using the Count after doing an Add should give us the proper value.
						}

                        csi.PassIndex = passIndex;

                        // Update the number of passes that will be needed for this calibration.
                        if ( numberPasses <= passIndex )
                            numberPasses = passIndex + 1;

						sensor.CalibrationGas = GasType.Cache[ csi.GasCode ]; // Record the calibration gas.
						csi.SGR.Position = ic.Position; // Record the position of the sensor in the instrument.

                        // See if Zeroing was successful.  If not, set up response to indicate the failure.
                        if ( !_instrumentController.GetSensorZeroingStatus( ic.Position ) )
                        {
                            Log.Debug( string.Format( "{0}SKIPPING SENSOR {1} ({2}) DUE TO FAILED ZEROING!", logLabel, ic.Position, ic.Component.Uid ) );
                            // Setup status with our failed zeroing and last cal status.
                            csi.SGR.Status = Status.ZeroFailed;
                            csi.SGR.Time = DateTime.UtcNow;
                            csi.SGR.Reading = 0.0;
                            csi.PassIndex = CalibrationSensorInfo.PASS_SENSOR_FAILED_ZERO;
                        }

                        // See if this sensor is enabled for calibration on the iNet DS docking station.  If not, set up response to indicate Zero Passed.
                        if ( !_instrumentController.IsSensorCalibrationEnabled( ic ) )
                        {
                            Log.Debug( logLabel + "Calibration is disabled for sensor " + ic.Position + " (" + sensor.Type.Code + ")" );
                            csi.SGR.Status = Status.ZeroPassed;
                            csi.SGR.Time = DateTime.UtcNow;
                            csi.SGR.Reading = 0.0;
                            csi.PassIndex = CalibrationSensorInfo.PASS_SENSOR_NOT_CAL_ENABLED;

                            Log.Debug( logLabel + "SKIPPING SENSOR " + ic.Position + " (" + sensor.Type.Code + ")" );
                        }

                        // See if the sensor is scheduled to be included in the calibration operation.  If not, mark it as Zero Passed, and as skipped.
                        if ( ComponentCodes.Count != 0 && !ComponentCodes.Contains( sensor.Type.Code ) )
                        {
                            Log.Debug( string.Format( "{0} Skipping sensor {1} ({2}) not included in schedule's specified component list.", logLabel, ic.Position, sensor.Type.Code ) );
                            csi.SGR.Status = Status.ZeroPassed;
                            csi.SGR.Time = DateTime.UtcNow;
                            csi.SGR.Reading = 0.0;
                            csi.PassIndex = CalibrationSensorInfo.PASS_SENSOR_SKIPPED;
                        }

                        //INS-7282 - To determine if the sensor is expired based on the configured sensor age and setup date. Applicable only to Service Account
                        if ( IsSensorExpiredForServiceAccount( sensor, csi.SGR, sensorCalibrationLimits ) )
                        {
                            Log.Debug( string.Format( "{0}IsSensorExpiredForServiceAccount returned TRUE for {1} at position {2}.", logLabel, sensor.Type.Code, ic.Position ) );
                            Log.Debug( string.Format( "{0}Marking {1} sensor at position {2} as {3}.", logLabel, sensor.Type.Code, ic.Position, Status.Failed ) );
                            csi.SGR.Status = Status.Failed;
                            csi.SGR.Time = DateTime.UtcNow;
                            csi.SGR.Reading = 0.0;
                            csi.PassIndex = CalibrationSensorInfo.PASS_SENSOR_EXPIRED;
                        }

                        // Perform additional initializations if the sensor is expected to be calibrated.
                        if ( csi.PassIndex >= 0 )
                        {
							// Add _zeroingUsedGasEndPoint to the list of cylinders used in the sensors gas response.
                            if ( _zeroingUsedGasEndPoint != null )
                                csi.SGR.UsedGasEndPoints.Add( _zeroingUsedGasEndPoint );

                            // Get the sensor's maximum reading.
                            csi.MaximumReading = _instrumentController.GetSensorMaximumReading( ic.Position, sensor.Resolution );

                            // Get the base line, the zero offset, and note if the instrumet uses an accessory pump.  Store this information in the sensor gas response.
                            csi.SGR.BaseLine = _instrumentController.GetSensorBaseline( ic.Position );
                            csi.SGR.ZeroOffset = _instrumentController.GetSensorZeroOffset( ic.Position, sensor.Resolution );
                            csi.SGR.AccessoryPump = _instrumentController.AccessoryPump;

                            // Get the flow rate required for this sensor.
                            csi.FlowRate = _instrumentController.GetSensorCalibrationFlowRate( ic );

                            // Increment the number of sensors that will be calibrated during this operation.
                            numberSensorsToCalibrate++;
                        }
                    }

                    ResetTriedGasEndPoints();

                    // If there are no sensors that will be calibrated, exit now.
                    if ( numberSensorsToCalibrate <= 0 )
                        return;

                    // Confirm that each sensor with a non-negative index has a gasEndPoint defined.  If any does not, throw exception.
                    foreach ( CalibrationSensorInfo csi in calSensorInfoList )
                    {
                        if ( csi.PassIndex >= 0 && GetPassGasEndPoint( csi.PassIndex ) == null )
                            throw new CorrectCalibrationGasUnavailable( _returnEvent.DockedInstrument.SerialNumber );
                    }

					// Sort the CalibrationSensorInfos list by PassIndex/Position, so that in all the logging
					// we do in the various foreach loops below, there is a consistent order.
					calSensorInfoList.Sort(CalibrationSensorInfo.Compare);

                    // Log information found in the calibration sensor info list.
					Log.Debug( "--------------------" );
                    foreach ( CalibrationSensorInfo csi in calSensorInfoList )
                    {
						Log.Debug( string.Format( "{0}Pass Index {1}, Sensor Position {2}, UID={3}", logLabel, csi.PassIndex, csi.InstalledComponent.Position, csi.InstalledComponent.Component.Uid ) );
						Log.Debug( string.Format( "{0}CalGasCode={1}, Concentration = {2}, TimeOut = {3}", logLabel, csi.GasCode, csi.GasConcentration, csi.CalTimeOut ) );
						Log.Debug( string.Format( "{0}Precondition FlowRate={1}, TimeOut={2}, PauseTime={3}", logLabel, csi.PreconditionPauseTime, csi.CalPreconditionFlowRate, csi.PreconditionTimeOut ) );
                        if ( csi.PassIndex >= 0 )
                        {
							Log.Debug( string.Format( "{0}AccessoryPump={1}", logLabel, csi.SGR.AccessoryPump ) );
							Log.Debug( string.Format( "{0}Calibration FlowRate={1}", logLabel, csi.FlowRate ) );
							Log.Debug( string.Format( "{0}Cal MaximumReading={1}", logLabel, csi.MaximumReading ) );
                            Log.Debug( string.Format( "{0}BaseLine = {1}, ZeroOffset = {2}", logLabel, csi.SGR.BaseLine, csi.SGR.ZeroOffset ) );
                        }
						Log.Debug( "--------------------" );
                    }
                    Log.Debug( string.Format( "{0}Total Number of Passes = {1}", logLabel, numberPasses ) );
                    Log.Debug( string.Format( "{0}Total Number of Sensors to Calibrate = {1}", logLabel, numberSensorsToCalibrate ) );

					#endregion --------------- INITIALIZATION TASKS -----------------

					#region --------------- CALIBRATION TASKS -----------------

                    // Put instrument into calibration mode.
                    // Note that we take the instrument back out of calibration
                    // mode below in the 'finally' block.
                    Log.Debug( string.Format( "{0}BEGIN INSTRUMENT CALIBRATION", logLabel ) );
                    Stopwatch calBeginStopwatch = Log.TimingBegin( "CAL - BEGIN INSTRUMENT CALIBRATION" );
                    _instrumentController.BeginInstrumentCalibration();
                    isCalibrationStarted = true;  // need to know to call EndInstrumentCalibration
                    GasEndPoint prevGasEndPoint = null;
                    Log.TimingEnd( "CAL - BEGIN INSTRUMENT CALIBRATION", calBeginStopwatch ); 

                    // Record the beginning of the calibration process to calculate cumulative times as the operation progresses.
                    DateTime cumulativeCalResponseStart = DateTime.UtcNow;

                    // Loop based on pass index values from <0> to <number of passes-1>
                    flowFailed = false;
                    for ( int currentPass = 1; currentPass < numberPasses && !flowFailed; currentPass++ )
                    {
                        Stopwatch passStopwatch = Log.TimingBegin( "CAL - PASS #" + currentPass ); 
                        Log.Debug( string.Format( "{0}CALIBRATION PASS #{1}", logLabel, currentPass ) );
                        
                        gasEndPoint = GetPassGasEndPoint( currentPass );                        

						// Extract out all CalibrationSensorInfo's for the current pass into a separate list
						// that we can iterate over in all the many foreach loops that follow.
						List<CalibrationSensorInfo> currentPassList = calSensorInfoList.FindAll( c => c.PassIndex == currentPass );

                        // Purge between each passes when switching between attached cylinders during CAL to clear gases in line.
                        // INETQA-4189 RHP v7.6
                        List<SensorGasResponse> currentPassSGR = new List<SensorGasResponse>();
                        currentPassList.ForEach(c => currentPassSGR.Add(c.SGR));

                        if (currentPass > 1 && prevGasEndPoint != null)
                        {
                            Log.Debug("CLEAR GASES IN LINES BEFORE CALIBRATING NEXT PASS");
                            Stopwatch cylinderSwitchPurgeStopwatch = Log.TimingBegin("CALIBRATING - PURGE(CYLINDER-SWITCH)");
                            new InstrumentPurgeOperation(PurgeType.CylinderSwitch, _instrumentController, GasEndPoints, _returnEvent, currentPassSGR).Execute();
                            Log.TimingEnd("CALIBRATING - PURGE(CYLINDER-SWITCH)", cylinderSwitchPurgeStopwatch);
                        }
                        prevGasEndPoint = (gasEndPoint.Cylinder.IsZeroAir || gasEndPoint.Cylinder.IsFreshAir) ? null : gasEndPoint;

                        #region Present Console Message String

                        // Build up a message containing the sensor labels for each sensor involved in this pass of the calibration.
						string message = string.Empty;
                        string gasSymbol = string.Empty;

						foreach ( CalibrationSensorInfo csi in currentPassList )
                        {
                            // If there is no gas end point for this sensor, the throw an exception
                            if ( gasEndPoint == null )
                                throw new CorrectCalibrationGasUnavailable( _returnEvent.DockedInstrument.SerialNumber );

                            // Guarantee that the correct calibration gas concentration is available.
                            Log.Debug( string.Format( "{0}Setting cal gas concentration on sensor {1} before cal", logLabel, csi.InstalledComponent.Position ) );
                            double availableConcentration = _instrumentController.SetCalibrationGasConcentration( csi.InstalledComponent, gasEndPoint );

                            // If we get no concentration, throw an exception
                            if ( availableConcentration == 0.0 )
                                throw new CorrectCalibrationGasUnavailable( ( (Sensor)csi.InstalledComponent.Component ).CalibrationGas.Code );

                            // SGF  14-Dec-2012  INS-3726 -- Missed the step of setting the concentration on the SGR
                            csi.SGR.GasConcentration.Concentration = ( (Sensor)csi.InstalledComponent.Component ).CalibrationGasConcentration;
                            Log.Debug( string.Format( "{0}Calibrating gas: {1} Concentration: {2}", logLabel,
                                ( (Sensor)csi.InstalledComponent.Component ).CalibrationGas.Code,
                                ( (Sensor)csi.InstalledComponent.Component ).CalibrationGasConcentration ) );
                            // SGF  14-Dec-2012  INS-3726 -- end of fix

                            // Add this sensor label to the message string
                            // INS-8630 RHP v9.7 - do not diplay duplicate sensor symbol incase of dual sensors
                            gasSymbol = Master.Instance.ConsoleService.GetSensorLabel(((Sensor)csi.InstalledComponent.Component).Type.Code);

                            if (!message.Contains(gasSymbol))
                            {
                                if (message.Length > 0)
                                    message += ", ";
                                message += gasSymbol;
                            }
                        }
                        Log.Debug( string.Format( "{0}Console message = {1}", logLabel, message ) );
                        // If somehow there are no sensors to process in this pass, the message string will be empty.
                        // If this is the case, skip the rest of this iteration, and proceed to the next pass.
                        if ( message.Length <= 0 )
                            continue;

                        // Indicate on the console which sensors are being calibrated
                        Master.Instance.ConsoleService.UpdateState( ConsoleState.CalibratingInstrument, message );

						#endregion Present Console Message String

						DateTime calDurationStart = DateTime.UtcNow; // Record the start time for the calibration of sensors in this pass

                        #region Preconditioning

                        // Precondition sensors to be calibrated during this pass.
                        try
                        {
                            Stopwatch preconStopwatch = Log.TimingBegin( "CAL - PRECONDITION SENSORS" );
                            TimeSpan preconditionTime = PreconditionSensor( calSensorInfoList, currentPass );
                            Log.TimingEnd( "CAL - PRECONDITION SENSORS", preconStopwatch );

                            Log.Debug( string.Format( "{0}Precondition Time = {1} seconds", logLabel, preconditionTime.TotalSeconds ) );
                            if ( preconditionTime.TotalSeconds > 0 )
                            {
								foreach ( CalibrationSensorInfo csi in currentPassList )
                                {
                                    if ( csi.OpStatus == SensorCalOpStatus.PreconditionPassed || csi.OpStatus == SensorCalOpStatus.PreconditionFailed )
                                        csi.SGR.UsedGasEndPoints.Add( new UsedGasEndPoint( gasEndPoint, CylinderUsage.Precondition, preconditionTime, (short)currentPass ) );
                                }
                            }
                        }
                        catch ( FlowFailedException ffe )
                        {
                            Log.Error( "Cylinder Empty", ffe );
                            gasEndPoint.Cylinder.Pressure = PressureLevel.Empty;
                            emptyGasEndPoint = gasEndPoint;
                            flowFailed = true;
                            continue;
                        }
                        catch ( Exception e )
                        {
                            throw e;
                        }

						if ( !Master.Instance.ControllerWrapper.IsDocked() ) // If instrument is not docked, throw InstrumentNotDockedException
                            throw new InstrumentNotDockedException();

						#endregion Preconditioning

						#region Record Preconditioning Times

						// Record the "after preconditioning readings and times.
                        // Set the preconditioning time
                        DateTime preconditioningFinishedTime = DateTime.UtcNow;
						foreach ( CalibrationSensorInfo csi in currentPassList )
                        {
                            // Get sensor reading, and store in ReadingAfterPreconditioning property of SGR
                            csi.SGR.ReadingAfterPreconditioning = _instrumentController.GetSensorReading( csi.InstalledComponent.Position, ( (Sensor)csi.InstalledComponent.Component ).Resolution );
                            // store current time in TimeAfterPreconditioning property of SGR
                            csi.SGR.TimeAfterPreconditioning = preconditioningFinishedTime;
                            Log.Debug( string.Format( "{0}Sensor position = {1}, Precondition reading = {2}, Time = {3}", logLabel,
                                csi.InstalledComponent.Position, csi.SGR.ReadingAfterPreconditioning, Log.DateTimeToString( csi.SGR.TimeAfterPreconditioning ) ) );
						}

						#endregion Record Preconditioning Times

						#region Pause Gas Flow

						// Pause gas flow following preconditioning based on the needs of sensors involved in this pass.
                        long pauseInSeconds = 0;
						foreach ( CalibrationSensorInfo csi in currentPassList )
                        {
                            if ( csi.PreconditionPauseTime > pauseInSeconds )
                                pauseInSeconds = csi.PreconditionPauseTime;
                        }
                        Log.Debug( string.Format( "{0}Precondition Pause = {1}", logLabel, pauseInSeconds ) );
                        Stopwatch pauseStopwatch = Log.TimingBegin( "CAL - PAUSE GAS FLOW" );
                        _instrumentController.PauseGasFlow( gasEndPoint, pauseInSeconds );
                        Log.TimingEnd( "CAL - PAUSE GAS FLOW", pauseStopwatch );

						#endregion Pause Gas Flow

						#region Determine Flow Rate

						// Loop through the sensors in this pass to determine the highest required flow rate.
                        int highFlowRate = 0;
						foreach ( CalibrationSensorInfo csi in currentPassList )
                        {
                            int curFlowRate = _instrumentController.GetSensorCalibrationFlowRate( csi.InstalledComponent );
                            if ( curFlowRate > highFlowRate )
                                highFlowRate = curFlowRate;
                        }
                        Log.Debug( string.Format( "{0}Calibration Flow Rate = {1}", logLabel, highFlowRate ) );

						#endregion Determine Flow Rate

						Stopwatch sensorCalStopwatch = Log.TimingBegin( "CAL - CALIBRATE SENSORS" );

                        #region Init For Sensor Calibration

                        // Instruct the instrument to calibrate each of the sensors in this pass in parallel.
                        // Also indicate the fact that the status of these sensors is now "Calibrating", and 
                        // initialize the number of readings taken.
						List<int> positions = new List<int>();
						foreach ( CalibrationSensorInfo csi in currentPassList )
                        {
							positions.Add( csi.InstalledComponent.Position );
                            csi.TotalReadings = 0;
                        }

						_instrumentController.BeginSensorCalibration( positions );

						// Instruct the instrument that each of the sensors in this pass will begin calibration.
						// Also indicate the fact that the status of these sensors is now "Calibrating", and 
						// initialize the number of readings taken.
						foreach ( CalibrationSensorInfo csi in currentPassList )
						{
							csi.TotalReadings = 0;
							csi.OpStatus = SensorCalOpStatus.Calibrating;
							Log.Debug( string.Format( "{0}Calibration begins for sensor {1}", logLabel, csi.InstalledComponent.Position ) );
						}

						#endregion Init For Sensor Calibration

						// Set the desired flow on the pump to the highest required flow rate determined above.
						// Open the gas end point for the sensors involved in this calibration pass.
						_instrumentController.OpenGasEndPoint( gasEndPoint, highFlowRate );
						DateTime calLoopStartTime = DateTime.UtcNow; // record the time the gas end point was opened

                        // Main calibration loop for the sensors involved in this pass
                        Log.Debug( string.Format( "{0}CALIBRATION LOOP BEGINS FOR PASS #{1}", logLabel, currentPass ) );
                        bool calibrationPassContinues;
                        Pump.IsBadPumpTubing = false;
                        do
                        {
                            // Calculate the time that has elapsed since the start of the calibration loop
                            TimeSpan calTime = DateTime.UtcNow - calLoopStartTime;
                            Log.Debug( string.Format( "{0}Time elapsed in calibration pass = {1}", logLabel, calTime ) );

                            #region Check For Cal Time Outs

                            // Check each sensor that is still calibrating to determine if it has timed out
							foreach ( CalibrationSensorInfo csi in currentPassList.FindAll( c => c.OpStatus == SensorCalOpStatus.Calibrating ) )
                            {
								// The VPRO instrument uses a hardcoded timeout of 5 minutes when it's told to calibrate
								// multiple sensors. (i.e., it ignores the timeout that is programmed into the sensors.
								// But, if we tell it to calibrate a single sensor, then it will actually use the timeout
								// programmed into the sensor.
								TimeSpan calTimeOut = ( _returnEvent.DockedInstrument.Type == DeviceType.VPRO && currentPassList.Count > 1 ) 
									?  new TimeSpan(0,5,0) : csi.CalTimeOut;

								if ( calTime > calTimeOut + _timeOutCushion )
								{
									// The current sensor has timed out.  Update its calibration operation status to CalibrationFailed.
									csi.OpStatus = SensorCalOpStatus.CalibrationFailed;
									csi.SGR.Status = Status.Failed; // Update the sensor gas response to Failed.
									csi.SGR.Duration = (int)calTime.TotalSeconds; // Record the duration of the calibration on this sensor.
									// Record the amount of time that has elapsed since the beginning of the overall calibration operation.
									csi.SGR.CumulativeResponseTime = (int)( (TimeSpan)( DateTime.UtcNow - cumulativeCalResponseStart ) ).TotalSeconds;
									Log.Debug( string.Format( "{0}CALIBRATION TIMED OUT FOR SENSOR {1}", logLabel, csi.InstalledComponent.Position ) );
								}
							}

                            #endregion Check For Cal Time Outs

#if !TEST
                            Thread.Sleep( _WAIT_INTERVAL ); // Sleep for an interval of time before the next inquiry to the instrument about cal status
#endif

							if ( !Master.Instance.ControllerWrapper.IsDocked() ) // Check to see if the instrument has been undocked.  If it has, exit the calibration loop.
                                break;

                            if (Master.Instance.PumpWrapper.IsBadPumpTubing())  //Check to see that no issues with pump tubing.
                                throw new FlowFailedException(gasEndPoint);

#region Get Sensor Readings

                            // Get sensor readings for each sensor still calibrating.
							foreach ( CalibrationSensorInfo csi in currentPassList.FindAll( c => c.OpStatus == SensorCalOpStatus.Calibrating ) )
                            {
                                // Get the current reading from this sensor.
                                csi.SGR.Reading = _instrumentController.GetSensorCalibrationReading( csi.InstalledComponent.Position, ( (Sensor)csi.InstalledComponent.Component ).Resolution );
								csi.SGR.Time = DateTime.UtcNow; // Record the time of the reading.
								csi.TotalReadings++; // Increment the count of readings taken for this sensor.
                                Log.Debug( string.Format( "{0}Sensor({1}): Reading({2}) = {3}, Time = {4}", logLabel,
                                    csi.InstalledComponent.Position, csi.TotalReadings, csi.SGR.Reading, Log.DateTimeToString(csi.SGR.Time) ) );
                            }

							if ( !Master.Instance.ControllerWrapper.IsDocked() ) // Check to see if the instrument has been undocked.  If it has, exit the calibration loop.
                                break;

#endregion Get Sensor Readings

#region Determine Sensor Cal Status

							// Initialize the "calibrationPassContinues"
                            calibrationPassContinues = false;

                            // Check each sensor that was still calibrating at the beginning of the loop iteration to see it's current status
							foreach ( CalibrationSensorInfo csi in currentPassList.FindAll( c => c.OpStatus == SensorCalOpStatus.Calibrating ) )
                            {
                                // Determine if this current sensor that was calibrating is still calibrating
                                if ( _instrumentController.IsSensorCalibrating( csi.InstalledComponent.Position ) == false )
                                {
									// Calibration for the current sensor is complete
									if ( _instrumentController.GetSensorCalibrationStatus( csi.InstalledComponent.Position ) == true )
                                    {
                                        // The sensor passed calibration.  Record that.
                                        csi.OpStatus = SensorCalOpStatus.CalibrationPassed;
                                        csi.SGR.Status = Status.Passed;
                                    }
                                    else
                                    {
                                        // The sensor failed calibration.  Record that.
                                        csi.OpStatus = SensorCalOpStatus.CalibrationFailed;
                                        csi.SGR.Status = Status.Failed;
                                    }

                                    // Get the sensor's final reading and its span coeffient
                                    csi.SGR.Reading = _instrumentController.GetSensorSpanReserve( csi.InstalledComponent.Position );
                                    csi.SGR.SpanCoef = _instrumentController.GetSensorSpanCoeff( csi.InstalledComponent.Position );

                                    // Checking for obviously screwed up sensor.
									if ( csi.SGR.Status == Status.Passed )
									{
										// last calibration time is only changed on the sensor if calibration passed
										csi.SGR.PostCal_LastCalibrationTime = _instrumentController.GetSensorLastCalibrationTime( csi.InstalledComponent.Position );

										// status should never be passed if span reserve is 0 or below
										if ( csi.SGR.FullSpanReserve <= 0 )
										{
											Log.Warning( string.Format( "{0}FullSpanReserve is {1} but status is {2}. DS overriding with a Failed status.", logLabel, csi.SGR.FullSpanReserve, csi.SGR.Status ) );
											csi.OpStatus = SensorCalOpStatus.CalibrationFailed;
											csi.SGR.Status = Status.Failed;
										}
										// last calibration time (pre-cal vs post-cal) should have changed
										else if ( csi.SGR.WasCalibrationInstrumentAborted() )
										{
											Log.Warning( string.Format( "{0}Status is {1}, but LastCalibrationTime did not change.  DS overriding with an InstrumentAborted status.", logLabel, csi.SGR.Status ) );
											csi.OpStatus = SensorCalOpStatus.CalibrationFailed;
											csi.SGR.Status = Status.InstrumentAborted; // A new response object will be created so most values are cleared before uploading to iNet.
										}

                                        //INS-7282 - Check the sensor span reserve and if the value is less than the configured threshold, set the sensor calibration as failed. Applicable only to repair accounts.
                                        if ( IsSensorFailedForRepairAccount( (Sensor)csi.InstalledComponent.Component, csi.SGR, Configuration.DockingStation.SpanReserveThreshold ) )
                                        {
                                            csi.SGR.Status = Status.Failed;
                                            csi.OpStatus = SensorCalOpStatus.CalibrationFailed;
                                        }
									}                                    

                                    // Calculate the duration of the calibration for this sensor, as well as the time from the start of the cal operation until this sensor's completion.
                                    TimeSpan calDuration = DateTime.UtcNow - calLoopStartTime;
                                    csi.SGR.Duration = (int)calDuration.TotalSeconds;
                                    csi.SGR.CumulativeResponseTime = ( (int)( DateTime.UtcNow - cumulativeCalResponseStart ).TotalSeconds );

                                    Log.Debug( string.Format( "{0}SENSOR CALIBRATION DONE -- Sensor({1}): Status={2}, Reading={3}, SpanCoef={4}, Duration={5}, CumRespTime={6}", logLabel,
                                        csi.InstalledComponent.Position, csi.SGR.Status, csi.SGR.Reading, csi.SGR.SpanCoef, csi.SGR.Duration, csi.SGR.CumulativeResponseTime ) );
                                }

                                // If the current sensor is still calibrating, mark the "calibrationPassContinues" flag as true to cause the calibration to continue.
                                if ( csi.OpStatus == SensorCalOpStatus.Calibrating )
                                    calibrationPassContinues = true;
                            }

                            Log.Debug( string.Format( "{0}Calibration pass continues = {1}", logLabel, calibrationPassContinues ? "TRUE" : "FALSE" ) );

							if ( !Master.Instance.ControllerWrapper.IsDocked() ) // Check to see if the instrument has been undocked.  If it has, exit the calibration loop.
                                break;

#endregion Determine Sensor Cal Status

						// Continue with the calibration loop as long as at least one sensor requires it, and as long as gas can flow from the gas end point.
						} while ( calibrationPassContinues == true && Master.Instance.PumpWrapper.GetOpenValvePosition() > 0 );
                        
                        Log.Debug( string.Format( "{0}CALIBRATION PASS #{1} COMPLETED {2}", logLabel, currentPass, ( Pump.GetOpenValvePosition() > 0 ) ? "SUCCESSFULLY" : "DUE TO FLOW FAILURE" ) );
                        Log.TimingEnd( "CAL - CALIBRATE SENSORS", sensorCalStopwatch );
                        
                        if ( !Master.Instance.ControllerWrapper.IsDocked() ) // If instrument is not docked, throw an exception
                            throw new InstrumentNotDockedException();

                        // Calculate the amount of time the gas end point has been open
                        TimeSpan elapsedTime = DateTime.UtcNow - calLoopStartTime;
                        Log.Debug( string.Format( "{0}Calibration pass elapsed time = {1}", logLabel, elapsedTime ) );

#region Set Gas Usage

                        // Add gas usage information to the sensor gas responses for the just-calibrated sensors
						foreach ( CalibrationSensorInfo csi in currentPassList )
							csi.SGR.UsedGasEndPoints.Add( new UsedGasEndPoint( gasEndPoint, CylinderUsage.Calibration, elapsedTime, (short)currentPass ) );

                        // NOTE:  Calculating cylinder usage as above will over-count gas used when multiple sensors are calibrated at the same time.
						// What do we do to adjust for this?

#endregion Set Gas Usage

						// Determine if flow failed for the gas end point used for the current calibration.
                        flowFailed = Master.Instance.PumpWrapper.GetOpenValvePosition() <= 0;
                        if ( flowFailed )
                        {
                            gasEndPoint.Cylinder.Pressure = PressureLevel.Empty;
                            emptyGasEndPoint = gasEndPoint;
                        }

						_instrumentController.CloseGasEndPoint( gasEndPoint ); // Close gas end point

						ResetTriedGasEndPoints(); // Reset information on the cylinders in preparation for the next calibration pass.

#region Perform Final Settings On Sensors

                        // Perform final wrap up work on sensors involved in this calibration pass
                        if ( Master.Instance.ControllerWrapper.IsDocked() )
                        {
							foreach ( CalibrationSensorInfo csi in currentPassList )
                            {
								// Restore the calibration gas concentration for the sensor, based on what was indicated on the sensor at the beginning of the calibration operation.
								Log.Debug( string.Format( "{0}Restoring cal gas concentration on sensor {1} after cal", logLabel, csi.InstalledComponent.Position ) );
								_instrumentController.SetCalibrationGasConcentration( csi.InstalledComponent, csi.GasConcentration, true );
								// Update the bump test status for the current sensor
                                Sensor calSensor = (Sensor)csi.InstalledComponent.Component;
                                calSensor.BumpTestStatus = _instrumentController.GetSensorBumpStatus(csi.InstalledComponent.Position); ; //Suresh 22-Feb-2012 INS-2705
                                // INETQA-4178 RHP v7.6 Update the return event BumpTestStatus as this is used the eventprocessor to update switch service instrument 
                                // this is required for the scheduler logic discussed above
                                Sensor sensor = (Sensor)_returnEvent.DockedInstrument.InstalledComponents.Find(ic => ic.Component.Uid == calSensor.Uid).Component;
                                if (sensor != null)
                                    sensor.BumpTestStatus = calSensor.BumpTestStatus; 

								Log.Debug( string.Format( "{0}Sensor cal gas concentration restored to {1}", logLabel, csi.GasConcentration ) );
								Log.Debug( string.Format( "{0}Sensor bump status = {1}", logLabel, ( (Sensor)csi.InstalledComponent.Component ).BumpTestStatus ) );
                            }
                        }

						if ( !Master.Instance.ControllerWrapper.IsDocked() ) // If instrument is not docked, throw an exception
                            throw new InstrumentNotDockedException();

#endregion Perform Final Settings On Sensors

                        Log.TimingEnd( "CAL - PASS #" + currentPass, passStopwatch );
                    }

                    if ( flowFailed )
                    {
                        // prepare to try the whole calibration again, but with a different cylinder to take the place of the cylinder that emptied
                        _instrumentController.EndInstrumentCalibration();
						_passesEndPointList.Clear();
					}

#endregion --------------- CALIBRATION TASKS -----------------
				}
                while ( flowFailed == true && !Master.Instance.PumpWrapper.IsBadPumpTubing() );

#region /-------------- CLOSING TASKS -----------------

#region Determine If Operation Passed

				// Now, wrap-up steps will be taken.
                // Step 1:  Determine whether the instrument has had all sensors that must be calibrated pass.
                bool passed = _returnEvent.GasResponses.Count > 0;

                ///////////////////////////////////////////////////////////////////////////////
                // *** IMPORTANT NOTE *** The following 'for' loop must remain a 'for' loop!
				// Do NOT change it to a 'foreach' loop!  It used to be  foreach loop prior to
                // DS2 v6.0. But as of DS2 v6.0 (which uses Compact Framework 2.0 unlike earlier
                // versions which use CF 1.0), if the GasResponses List is empty, then the
                // finally block sometimes terminates for some unknown reason as soon as the
                // foreach-loop is finished. Meaning all the code after the foreach block never 
                // gets executed!
                // This was observed to be happening when a FlowFailedException was being
                // thrown by deeper code due to an empty cylinder.
                // This code used to work in DS2 5.0 and earlier which used CF 1.0).
                // Seems to be broken in CF 2.0.  We ran into some some similar issues with
                // Compact Framework exception handling in CF 1.0; I was hoping it would be 
                // fixed in CF 2.0, but I guess that is not the case.  - jpearsall 12/6/2007
                ///////////////////////////////////////////////////////////////////////////////
                for ( int i = 0; i < _returnEvent.GasResponses.Count; i++ )
                {
                    SensorGasResponse response = (SensorGasResponse)_returnEvent.GasResponses[i];
                    if ( response.Status != Status.Passed && response.Status != Status.ZeroPassed )
                    {
                        passed = false;
                        break;
                    }
                }
                Log.Debug( string.Format( "{0} {1}", logLabel, passed ? "Calibration PASSED" : "Calibration FAILED" ) );

#endregion Determine If Operation Passed

#region End Calibration

				// Step 2: Tell the instrument that the calibration has ended.
                // (isCalibrationStarted is set when BeginInstrumentCalibration is called)
                if ( isCalibrationStarted && Master.Instance.ControllerWrapper.IsDocked() )
                {
                    try
                    {
                        _instrumentController.EndInstrumentCalibration();
                        Log.Debug( string.Format( "{0}END INSTRUMENT CALIBRATION", logLabel ) );
                    }
                    catch ( Exception e )
                    {
                        Log.Error( "EndInstrumentCalibration", e );
                    }
				}

#endregion End Calibration

#region Purge

				// Step 3:  Purge the calibration gases from the gas lines in the docking station at the end of 
                // the calibration operation.
                try
                {
                    // Purge gases at the end of the calibration operation.
                    Stopwatch purgeStopwatch = Log.TimingBegin( "CAL - PURGE(FINAL)" );
                    new InstrumentPurgeOperation( PurgeType.PostCalibration, _instrumentController, GasEndPoints, _returnEvent ).Execute();
                    Log.TimingEnd( "CAL - PURGE(FINAL)", purgeStopwatch );
                }
                catch ( Exception ex )
                {
                    // We deliberately do NOT rethrow the exception here.
                    // Particularly not FlowFailedExceptions.
                    // If FlowFailedException was thrown earlier, during calibration, zeroing, etc.,
                    // then throwing another one here would cause the earlier thrown one to be lost.
                    // It's more important that we throw the original one since it's likely for
                    // calibration gas.
                    // Also, even if we didn't already throw one earlier, if we were to throw here,
                    // the remaining code in this 'finally' block would not get executed (such as
                    // turning off the pump).
                    // It's unfortunate that we do not throw here, since it causes us
                    // to not be able to notify iNet of empty zero air cylinder detected during the purge.
                    // This whole finally block would need restructured to fix this.  - JMP, 6/28/2011.
                    Log.Warning( "Ignoring exception from Post-calibration purge operation", ex );
				}

#endregion Purge

#region Take After-Purge Readings

				// Step 4:  Take the "after-purge" gas readings, and set the time-after-purge property for
                // each sensor gas response.
                //
                // SGF  14-Jun-2011  INS-1732
                // Get the after-purge gas readings
                foreach ( InstalledComponent ic in _returnEvent.DockedInstrument.InstalledComponents )
                {
                    if ( !( ic.Component is Sensor ) )  // Skip non-sensors.
                        continue;
                    Sensor sensor = (Sensor)ic.Component;
                    if ( !sensor.Enabled ) // Skip disabled sensors
                        continue;

                    SensorGasResponse sgr = _returnEvent.GetSensorGasResponseByUid( sensor.Uid );
                    if ( sgr != null )
                    {
                        sgr.ReadingAfterPurging = _instrumentController.GetSensorReading( ic.Position, sensor.Resolution );
                        sgr.TimeAfterPurging = DateTime.UtcNow;
                    }
				}

#endregion Take After-Purge Readings

#endregion /-------------- CLOSING TASKS -----------------

			}
            catch ( Exception e )
            {
                thrownException = e;
				if ( e is CorrectCalibrationGasUnavailable && flowFailed == true && emptyGasEndPoint != null )
					throw new FlowFailedException( emptyGasEndPoint );
                throw;
            }
            finally
            {
#region ClosingTasksUponException
                
                // Upon completion of the calibration activities, we must still take a few actions to 
                // return the system to normal, if an exception was thrown during the calibration 
                // process.  If no exception was thrown, these steps would already have been taken, 
                // so there is no need to run these again.
                if ( thrownException != null )
                {
					Master.Instance.PumpWrapper.CloseGasEndPoint( gasEndPoint ); // Make sure gas flow is shut down if operation was cut short by an exception (SGF  30-Jul-2012  DEV JIRA INS-4803)
                    
                    //If any pump tubing issue is detected, mark that accordingly, so appropriate error 
                    //will be displayed on LCD and uploaded to iNet as well.
                    Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal = Master.Instance.PumpWrapper.IsBadPumpTubing();

#region End Calibration

                    // Step 5: Tell the instrument that the calibration has ended.
                    // (isCalibrationStarted is set when BeginInstrumentCalibration is called)
                    if ( isCalibrationStarted && Master.Instance.ControllerWrapper.IsDocked() )
                    {
                        try
                        {
                            _instrumentController.EndInstrumentCalibration();
                        }
                        catch ( Exception e )
                        {
                            Log.Error( "EndInstrumentCalibration", e );
                        }
					}

#endregion End Calibration

#region Purge

					// Step 6:  Purge calibration gases from gas lines at the end of the calibration operation.
                    try
                    {
                        Stopwatch purgeStopwatch = Log.TimingBegin( "CAL - PURGE(FINAL, ON ERROR)" );
                        new InstrumentPurgeOperation( PurgeType.PostCalibration, _instrumentController, GasEndPoints, _returnEvent ).Execute();
                        Log.TimingEnd( "CAL - PURGE(FINAL, ON ERROR)", purgeStopwatch );
                    }
                    catch ( Exception ex )
                    {
                        // We deliberately do NOT rethrow the exception here.
                        // Particularly not FlowFailedExceptions.
                        // If FlowFailedException was thrown earlier, during calibration, zeroing, etc.,
                        // then throwing another one here would cause the earlier thrown one to be lost.
                        // It's more important that we throw the original one since it's likely for
                        // calibration gas.
                        // Also, even if we didn't already throw one earlier, if we were to throw here,
                        // the remaining code in this 'finally' block would not get executed (such as
                        // turning off the pump).
                        // It's unfortunate that we do not throw here, since it causes us
                        // to not be able to notify iNet of empty zero air cylinder detected during the purge.
                        // This whole finally block would need restructured to fix this.  - JMP, 6/28/2011.
                        Log.Warning( "Ignoring exception from Post-calibration purge operation", ex );
					}

#endregion Purge
				}

#endregion ClosingTasksUponException
			}
        } // end-CalibrateInstrumentParallel()

		/// <summary>
		/// Preconditioning routine used only for parallel calibration.
		/// </summary>
		/// <param name="calSensorInfoList"></param>
		/// <param name="passIndex"></param>
		/// <returns></returns>
		private TimeSpan PreconditionSensor( List<CalibrationSensorInfo> calSensorInfoList, int passIndex )
		{
			DateTime startTime = DateTime.UtcNow;

			try
			{
#region Determine Preconditioning Needs

				// Determine which sensors in this calibration pass require preconditioning, and which ones can be skipped.
				  bool preconditioningNeeded = false;
				foreach ( CalibrationSensorInfo csi in calSensorInfoList )
				{
					if ( csi.PassIndex == passIndex )
					{
						if ( _instrumentController.IsSensorCalPreconditionEnabled( csi.InstalledComponent ) == true )
						{
							// This sensor requires precondioning.
							csi.OpStatus = SensorCalOpStatus.Preconditioning;

							// save the current sensor gas response status, for restoration at the end of this preconditioning pass
							csi.OriginalStatus = csi.SGR.Status;

							preconditioningNeeded = true;
						}
						else
						{
							// This sensor can be skipped.
							csi.OpStatus = SensorCalOpStatus.PreconditionSkipped;
						}
					}
				}

				// If all sensors in this pass can be skipped, then return with a precondition time span of 0.
				if ( preconditioningNeeded == false )
				{
					return new TimeSpan( 0, 0, 0 );
				}

#endregion

#region Determine Flow Rate

				// Determine the required flow rate (this will be the maximum flow rate of the sensors to precondition).
				// Log which sensors are about to be preconditioned during this calibration pass.
				int preconditionFlowRate = 0;
				foreach ( CalibrationSensorInfo csi in calSensorInfoList )
				{
					if ( csi.OpStatus != SensorCalOpStatus.Preconditioning )
						continue;

					double maximumReading = csi.MaximumReading;

					Log.Debug( "PRECOND: PRECONDITIONING Sensor " + csi.InstalledComponent.Position + ", UID=" + ( (Sensor)csi.InstalledComponent.Component ).Uid + ", " + csi.InstalledComponent.Component.Type.Code );
					Log.Debug( "PRECOND: Sensor MaximumReading: " + maximumReading + ", Resolution: " + ( (Sensor)csi.InstalledComponent.Component ).Resolution );
					Log.Debug( "PRECOND: Gas Conc: " + csi.SGR.GasConcentration.Concentration );

					int sensorPreconditionFlowRate = csi.CalPreconditionFlowRate;
					if ( sensorPreconditionFlowRate > preconditionFlowRate )
						preconditionFlowRate = sensorPreconditionFlowRate;
				}

#endregion

#region Determine Time Out

				// Determine the longest timeout value for the sensors that will be preconditioned on this pass.
				// Use this timeout value for all sensors.  SGF NOTE: We are assuming that it will be suitable 
				// to allow potentially more time for preconditioning for sensors which have shorter timeout 
				// times, since we need to allow for potentially several sensors and several timeout values.
				// If this assumption is incorrect, then we will need to adjust the following loop to test 
				// each sensor against its own timeout value.  The code will be more complex, so we need to 
				// prove that it is necessary to do that.
				TimeSpan preconditionTimeOut = TimeSpan.Zero;
				foreach ( CalibrationSensorInfo csi in calSensorInfoList )
				{
					if ( csi.OpStatus == SensorCalOpStatus.Preconditioning )
					{
						TimeSpan sensorPreconditionTimeOut = csi.PreconditionTimeOut;
						if ( sensorPreconditionTimeOut > preconditionTimeOut )
							preconditionTimeOut = sensorPreconditionTimeOut;
					}
				}

#endregion

				// Get the appropriate gas end point
				GasEndPoint gasEndPoint = GetPassGasEndPoint( passIndex );

				_instrumentController.OpenGasEndPoint( gasEndPoint, preconditionFlowRate );

				// Initialize the time "now"
				DateTime now = DateTime.UtcNow;
				startTime = now; // Set the startTime to now.

				// Ignore odd readings for the first half of the precondition time.  Set the 
				// "odd time" limit to be half way between the start time and the end time.
				DateTime oddTime = now.AddTicks( preconditionTimeOut.Ticks / 2L );
				DateTime endTime = now + preconditionTimeOut;

#region Unpause Sensors

				// Unpause each of the sensors that will be preconditioned.
				foreach ( CalibrationSensorInfo csi in calSensorInfoList )
				{
					if ( csi.OpStatus == SensorCalOpStatus.Preconditioning )
					{
						_instrumentController.PauseSensor( csi.InstalledComponent.Position, false );
						Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ") Start Time: " + startTime + " End Time: " + endTime );
					}
				}

#endregion

				// Keep iterating through this loop while the pump is running...
				while ( Master.Instance.PumpWrapper.GetOpenValvePosition() > 0 )
				{
					// Record the current time.
					now = DateTime.UtcNow;

					// Take readings from the sensors until the precondition timeout has been reached.
					if ( now >= endTime )
						break;

#if !TEST
                    // Wait a bit before each reading during preconditioning
                    Thread.Sleep( 1000 );
#endif

					// If the instrument has been undocked, exit the loop to stop the precondition operation.
					if ( !Master.Instance.ControllerWrapper.IsDocked() )
						break;

					foreach ( CalibrationSensorInfo csi in calSensorInfoList )
					{
						if ( csi.OpStatus == SensorCalOpStatus.Preconditioning )
						{
#region Get Sensor Reading

							double rawReading = _instrumentController.GetSensorReading( csi.InstalledComponent.Position, ( (Sensor)csi.InstalledComponent.Component ).Resolution );
							csi.SGR.Reading = rawReading;
							csi.PreconditionTotalReadings++;

#endregion

#region Classify Sensor Reading

							// Determine if the current reading is abnormal (that is, that it exceeds the maximum allowed reading for the sensor type).
							if ( Math.Abs( rawReading ) > csi.MaximumReading )
							{
								if ( now > oddTime )
								{
									// Record any abnormal readings for the second half of the preconditioning.
									csi.PreconditionOddReadings++;
									Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ") Odd reading seen: " + rawReading );
								}
								else
								{
									// Ignore any abnormal readings for the first half of the preconditioning.
									Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ") Odd reading ignored: " + rawReading );
								}
							}
							// Determine if the current reading is between 50% and 100% of the full span reserve.  If so, record it as a good reading.
							else if ( csi.SGR.FullSpanReserve > 50.0D )
								csi.PreconditionGoodReadings++;

							Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ")  (" + csi.PreconditionGoodReadings + "/" + csi.PreconditionTotalReadings + ") span: " + csi.SGR.FullSpanReserve + " raw: " + csi.SGR.Reading );

#endregion

#region Determine If Passed

							// Must pass a minimum number of 2 readings to pass precondition
							if ( csi.PreconditionGoodReadings >= 2 )
							{
								csi.SGR.Status = Status.Passed;
								csi.OpStatus = SensorCalOpStatus.PreconditionPassed;
								Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ")  PASSED" );
							}

#endregion

#region Determine If Odd

							// 3 odd readings may happen before exiting preconditiong.
							if ( csi.PreconditionOddReadings >= 3 )
							{
								csi.SGR.Status = Status.Failed;
								csi.OpStatus = SensorCalOpStatus.PreconditionFailed;
								Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ")  FAILED -- TOO MANY ODD READINGS." );
							}

#endregion
						}
					}

#region Determine If Done

					// As long as at least one sensor is preconditioning, continue in this loop.  If all sensors in this pass are done, then exit.
					// NOTE:  This approach has been taken rather than utilizing a counter, in order to minimize complexity.
					bool stillPreconditioning = false;
					foreach ( CalibrationSensorInfo csi in calSensorInfoList )
					{
						if ( csi.OpStatus == SensorCalOpStatus.Preconditioning )
							stillPreconditioning = true;
					}
					if ( stillPreconditioning == false )
						break;

#endregion
				}

				if ( Master.Instance.PumpWrapper.GetOpenValvePosition() > 0 )
				{
#region Mark Time Out Errors

					// Since the pump is still pumping gas, any sensors still marked as Precondition at this point have failed because the precondition timed out.
					foreach ( CalibrationSensorInfo csi in calSensorInfoList )
					{
						if ( csi.OpStatus == SensorCalOpStatus.Preconditioning )
						{
							csi.SGR.Status = Status.Failed;
							csi.OpStatus = SensorCalOpStatus.PreconditionFailed;
							Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ")  FAILED -- TIMED OUT." );
						}
					}

#endregion
				}
				else
				{
#region Mark Pump Closed Errors

					// The pump has closed. Any sensors still marked as Precondition at this point will be marked as having skipped.
					foreach ( CalibrationSensorInfo csi in calSensorInfoList )
					{
						if ( csi.OpStatus == SensorCalOpStatus.Preconditioning )
						{
							csi.SGR.Status = Status.Failed;
							csi.OpStatus = SensorCalOpStatus.PreconditionFailed;
							Log.Debug( "PRECOND: (" + csi.InstalledComponent.Position + ")  FAILED -- PUMP CLOSED." );
						}
					}

					throw new FlowFailedException( gasEndPoint );

#endregion
				}

				Log.Debug( "Open valve position: " + Pump.GetOpenValvePosition() );
				Log.Debug( "Now: " + DateTime.UtcNow + " End Time: " + endTime );
			}
			catch ( FlowFailedException ffe )
			{
				Log.Error( "PreconditionSensor", ffe );
				throw ffe;
			}
			catch ( Exception e )
			{
				Log.Error( "PreconditionSensor", e );
			}
			finally
			{
                //Ignore if bad pump tubing is detected during preconditioning.
                //Set Pump.IsBadPumpTubing to false.
                Pump.IsBadPumpTubing = false;

#region Restore Status

				// Put sensor gas response's status back to what it was when this method was called.
				foreach ( CalibrationSensorInfo csi in calSensorInfoList )
				{
					if ( csi.OpStatus == SensorCalOpStatus.PreconditionPassed || csi.OpStatus == SensorCalOpStatus.PreconditionFailed )
						csi.SGR.Status = csi.OriginalStatus;
				}

#endregion
			}
			return DateTime.UtcNow - startTime;
		}

    } // end partial class
}
