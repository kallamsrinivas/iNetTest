using System;
using System.Diagnostics;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;
using ISC.iNet.DS.DataAccess;
using System.Collections.Generic;

namespace ISC.iNet.DS.Services
{
    public partial class InstrumentCalibrationOperation
    {
		protected int _cumulativeCalTestResponseTime; // SGF  14-Jun-2011  INS-1732

		/// <summary>
		/// The sensor we're currently calibrating.
		/// </summary>
		protected InstalledComponent _component;
        protected GasEndPoint _usedGasEndPoint = null;

        /// <summary>
        /// Legacy / standard calibration.  i.e., Non-quick-cal.
        /// </summary>
        protected internal void CalibrateInstrumentSequential()
        {
            bool isCalibrationStarted = false;
            Exception thrownException = null;

			_cumulativeCalTestResponseTime = 0; // SGF  14-Jun-2011  INS-1732

            try
            {
                // Put instrument into calibration mode.
                // Note that we take the instrument back out of calibration
                // mode below in the 'finally' block.
                _instrumentController.BeginInstrumentCalibration();
                isCalibrationStarted = true;  // need to know to call EndInstrumentCalibration

                // Calibration each of the installed components sequentially.  That is, each sensor will be 
                // calibrated by itself; upon completion of one sensor's calibration, the next sensor will 
                // begin to be calibrated.
                foreach ( InstalledComponent ic in _returnEvent.DockedInstrument.InstalledComponents )
                {
                    if ( !( ic.Component is Sensor ) )  // Skip non-sensors.
                        continue;

                    Sensor sensor = (Sensor)ic.Component;

                    if ( !_instrumentController.IsSensorEnabled( ic.Position ) )
                    {
                        Log.Debug( "CALIBRATION (S): Skipping Disabled sensor " + ic.Position + " (" + sensor.Type.Code + ")" );
                        continue;
                    }

                    // Get the gas code for the gas being used to calibrate this sensor.
                    string calGasCode = sensor.GetGasToCal();

                    // Set the sensor's calibration gas.
                    sensor.CalibrationGas = GasType.Cache[calGasCode];

                    Log.Debug( "STARTING CALIBRATION (S) ON POSITION " + ic.Position + ", UID=" + ic.Component.Uid + ", " + ic.Component.Type.Code );
                    Log.Debug( "CALIBRATION (S): Desired gas: " + sensor.CalibrationGas.Code + " conc: " + sensor.CalibrationGasConcentration );

                    // Attempt to calibrate the sensor.
                    _component = ic;  // derived classes will look at the member component.

                    CalibrateSensor(_component, GasEndPoints, false);
                }

                // Now, wrap-up steps will be taken.
                // Step 1:  Determine whether the instrument has had all sensors that must be calibrated pass.
                bool passed = _returnEvent.GasResponses.Count > 0;

                ///////////////////////////////////////////////////////////////////////////////
                // *** IMPORTANT NOTE *** The following 'for' loop must remain a 'for' loop!
                // Do NOT change it to a 'foreach' loop!  It used to be  foreach loop prior
                // to v6.0.  But as of v6.0 (which uses Compact Framework 2.0 unlike earlier
                // versions which use CF 1.0), if the GasResponses List is empty, then the
                // finally block sometimes terminates for some unknown reason as soon as the
                // foreach-loop is finished. Meaning all the code after the foreach block never 
                // gets executed!
                // This was observed to be happening when a FlowFailedException was being
                // thrown by deeper code due to an empty cylinder.
                // This code used to work in 5.0 and earlier which used CF 1.0).
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

                // Step 2: Tell the instrument that the calibration has ended.
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

                // Step 3:  Purge the calibration gases from the gas lines in the docking station at the end of 
                // the calibration operation.
                try
                {
                    // Purge gases at the end of the calibration operation.
                    Stopwatch calFinalStopwatch = Log.TimingBegin( "CAL - PURGE(FINAL)" );
                    new InstrumentPurgeOperation( PurgeType.PostCalibration, _instrumentController, GasEndPoints, _returnEvent ).Execute();
                    Log.TimingEnd( "CAL - PURGE(FINAL)", calFinalStopwatch );
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
            }
            catch ( FlowFailedException ffe )
            {
                thrownException = ffe;
                throw;
            }
            catch ( SensorErrorModeException seme )
            {
                thrownException = seme;
                throw;
            }
            catch ( Exception e )
            {
                thrownException = e;
                throw;
            }
            finally
            {               
                // Upon completion of the calibration activities, we must still take a few actions to 
                // return the system to normal, if an exception was thrown during the calibration 
                // process.  If no exception was thrown, these steps would already have been taken, 
                // so there is no need to run these again.
                if ( thrownException != null )
                {
                    //If any pump tubing issue is detected, mark that accordingly, so appropriate error 
                    //will be displayed on LCD and uploaded to iNet as well.
                    Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal = Master.Instance.PumpWrapper.IsBadPumpTubing();

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

                    // Step 6:  Purge the calibration gases from the gas lines in the docking station at the end of 
                    // the calibration operation.
                    try
                    {
                        // Purge gases at the end of the calibration operation.
                        Stopwatch calPurgeStopwatch = Log.TimingBegin( "CAL - PURGE(FINAL, ON ERROR)" );
                        new InstrumentPurgeOperation( PurgeType.PostCalibration, _instrumentController, GasEndPoints, _returnEvent ).Execute();
                        Log.TimingEnd( "CAL - PURGE(FINAL, ON ERROR)", calPurgeStopwatch ); 
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
                }
            }

        } // end-CalibrateInstrumentSequential

        /// <summary>
        /// Calibrate the sensor.  Operates on "_component" member variable (an InstalledComponent)
        /// Note that this routine assumes that the parent class has already zeroed sensor. 
        /// </summary>
        /// <param name="instComp">Component to be calibrated</param>
        /// <param name="gasEndPoints">Gas End Points to be used</param>
        /// <param name="isO2HighBumpFailed">Whether the CalibrateSensor is called as a part of O2 High Bump Failure</param>
        /// <returns></returns>
        internal SensorGasResponse CalibrateSensor(InstalledComponent instComp, List<GasEndPoint> gasEndPoints, bool isO2HighBumpFailed)
        {

            _component = instComp;
            GasEndPoints = gasEndPoints;

            Log.Assert( this._component != null );

            bool alreadyFailed = false;
			GasEndPoint gasEndPoint;
            Sensor sensor = (Sensor)_component.Component;
            double currentConcentration = sensor.CalibrationGasConcentration;  // SGF  11-Oct-2010  INS-1189
            DateTime durationStart = DateTime.UtcNow;

            List<SensorCalibrationLimits> sensorCalibrationLimits = _testOnlySensorCalibrationLimits == null ? new SensorCalibrationLimitsDataAccess().FindAll() : _testOnlySensorCalibrationLimits;

            // SGF  14-Jun-2011  INS-1732
            SensorGasResponse response = _returnEvent.GetSensorGasResponseByUid( sensor.Uid );

            // If CalibrateSensor was called as a part of O2 High Bump Failure, the reponse 
            // object will be null and has to be initialized for calibration operation.
            if (isO2HighBumpFailed)
            {
                response = new SensorGasResponse(sensor.Uid, DateTime.UtcNow);
                response.GasConcentration = new GasConcentration(sensor.CalibrationGas, currentConcentration);
                response.GasDetected = sensor.GasDetected;
                response.Type = GasResponseType.Calibrate;
            }
            if ( response == null )
            {
                Log.Debug( "CALIBRATION: Skipping sensor " + _component.Position + " due to missing sensor gas response object!" );
                return null;
            }
            response.Position = _component.Position;

            // Add in whatever cylinder was used for zeroing.  zeroGasEndPoint 
            // will have been set by zerosensor(). Should never be null, but check just in case.
            if ( _zeroingUsedGasEndPoint != null )
                response.UsedGasEndPoints.Add( _zeroingUsedGasEndPoint );

            try
            {
                // See if Zeroing was successful.  If not, set up response to indicate
                // the failure and then we can leave. (The finally block below will handle
                // the response as we leave this method)
                if ( !_instrumentController.GetSensorZeroingStatus( _component.Position ) )
                {
                    Log.Debug( string.Format( "CALIBRATION: SKIPPING SENSOR {0} ({1}) DUE TO FAILED ZEROING!", _component.Position, _component.Component.Uid ) );

                    // Setup status with our failed zeroing and last cal status.
                    response.Status = Status.ZeroFailed;
                    response.Time = DateTime.UtcNow;
                    response.Reading = 0.0;
                    return response;
                }

                //Suresh 16-JAN-2012 INS-2480 - Begin
                if ( !_instrumentController.IsSensorCalibrationEnabled( _component ) )
                {
                    Log.Debug( "CALIBRATION: Calibration is disabled for sensor " + _component.Position + " (" + sensor.Type.Code + ")" );

                    //In the above if condition we have already checked for zeroing status, therefore we verywell know 
                    //when execution comes here then zeroing is passed for current calibration disabled sensor.

                    response.Status = Status.ZeroPassed;
                    response.Time = DateTime.UtcNow;
                    response.Reading = 0.0;

                    Log.Debug( "CALIBRATION: SKIPPING SENSOR " + _component.Position + " (" + sensor.Type.Code + ")" );

                    return response;
                }
                //Suresh 16-JAN-2012 INS-2480 - End

                // SGF  03-Nov-2010  Single Sensor Cal and Bump
                if ( ComponentCodes.Count != 0 && !ComponentCodes.Contains( sensor.Type.Code ) )
                {
                    Log.Debug( string.Format( "CALIBRATION: Skipping sensor {0} ({1}) not included in schedule's specified component list.", _component.Position, sensor.Type.Code ) );

                    // This sensor will not be calibrated.  Indicate that zeroing passed.
                    response.Status = Status.ZeroPassed;
                    response.Time = DateTime.UtcNow;
                    response.Reading = 0.0;
                    return response;
                }                
                //INS-7282 - To determine if the sensor is expired based on the configured sensor age and setup date. Applicable only to Service Account
                if ( IsSensorExpiredForServiceAccount( sensor, response, sensorCalibrationLimits ) )
                {
                    Log.Debug( string.Format( "CALIBRATION: IsSensorExpiredForServiceAccount returned TRUE for {0} at position {1}.", sensor.Type.Code, _component.Position ) );
                    Log.Debug( string.Format( "CALIBRATION: Marking {0} sensor at position {1} as {2}.", sensor.Type.Code, _component.Position, Status.Failed ) );
                    response.Status = Status.Failed;
                    response.Time = DateTime.UtcNow;
                    response.Reading = 0.0;
                    return response;
                }               

                if ( _instrumentController.TestForInstrumentReset( response, "calibrating sensor, checked zeroing status" ) == true )
                {
                    Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
                    return response;
                }

                Log.Debug( "CALIBRATION: Zeroing of sensor " + _component.Position + " determined as successful." );

                // Continue to calibrate, until there is known success, failure, or timeout
                while ( true )
                {
                    // We'll fail to get a gas end point when we've tried and failed on
                    // every available bottle of appropriate gas.
                    try
                    {
                        gasEndPoint = GetSensorGasEndPoint( _component ); // Get gas end point for calibrating this sensor.
                    }
                    catch
                    {
                        if (alreadyFailed) return response;
                        throw;
                    }

                    try
                    {
                        if ( gasEndPoint == null ) // There is no gas available.?
                            throw new CorrectCalibrationGasUnavailable( _returnEvent.DockedInstrument.SerialNumber );

                        // Purge between each passes when switching between attached cylinders during CAL to clear gases in line.
                        // INETQA-4189 RHP v7.6. Also make sure that this CYLINDER SWITCH PURGE does not happen for O2 Calibration initiated as part of Bump Test(02 sensor)
                        if (_usedGasEndPoint == null)
                            _usedGasEndPoint = (sensor.Type.Code == SensorCode.O2) ? null : gasEndPoint;
                        else if (_usedGasEndPoint != gasEndPoint && !isO2HighBumpFailed)
                        {
                            Log.Debug("CYLINDER SWITCH DETECTED : CLEAR GASES IN LINES BEFORE CALIBRATING NEXT SENSOR");
                            Stopwatch cylinderSwitchPurgeStopwatch = Log.TimingBegin("CALIBRATING - PURGE(CYLINDER-SWITCH)");
                            new InstrumentPurgeOperation(PurgeType.CylinderSwitch, _instrumentController, GasEndPoints, _returnEvent, new List<SensorGasResponse>{response} ).Execute();
                            Log.TimingEnd("CALIBRATING - PURGE(CYLINDER-SWITCH)", cylinderSwitchPurgeStopwatch);
                            _usedGasEndPoint = gasEndPoint;
                        }

                        //Suresh 18-OCT-2011 INS-2293
                        // Indicate on the console which sensor is being calibrated
                        Master.Instance.ConsoleService.UpdateState(ConsoleState.CalibratingInstrument, Master.Instance.ConsoleService.GetSensorLabel(sensor.Type.Code));

                        // Guarantee that the correct calibration gas concentration is available.
                        double availableConcentration = _instrumentController.SetCalibrationGasConcentration( _component, gasEndPoint );

                        // If we didn't find anything with the gas.
                        if ( availableConcentration == 0.0 )
                            throw new CorrectCalibrationGasUnavailable( sensor.CalibrationGas.Code );

                        // Set the gas concentration.
                        response.GasConcentration.Concentration = sensor.CalibrationGasConcentration;
                        Log.Debug( "Calibrating gas: " + sensor.CalibrationGas.Code + " conc: " + sensor.CalibrationGasConcentration );

                        // Determine the length of time to calibrate before timing out.  We add a an extra 
                        // timeout cushion so we don't want the DS to timeout before the instrument.
                        TimeSpan calTimeOut = _instrumentController.GetSensorCalibrationTimeout( _component.Position ) + _timeOutCushion;

                        if ( _instrumentController.TestForInstrumentReset( response, "calibrating sensor, getting calibration timeout" ) == true )
                        {
                            Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
                            return response;
                        }

                        // Do any preconditioning necessary.
                        Stopwatch stopwatch = Log.TimingBegin( "CAL - PRECONDITION SENSOR" );
                        TimeSpan preTime = _instrumentController.PreconditionSensor( _component, gasEndPoint, response );
                        Log.TimingEnd( "CAL - PRECONDITION SENSOR", stopwatch );

                        if ( preTime.TotalSeconds > 0 ) // will return zero if no precondition performed/needed.
                            response.UsedGasEndPoints.Add( new UsedGasEndPoint( gasEndPoint, CylinderUsage.Precondition, preTime ) );

                        if ( !Master.Instance.ControllerWrapper.IsDocked() ) // Did user undock instrument during preconditioning?
                            throw new InstrumentNotDockedException();

                        // SGF  14-Jun-2011  INS-1732
                        response.ReadingAfterPreconditioning = _instrumentController.GetSensorReading( _component.Position, sensor.Resolution );
                        response.TimeAfterPreconditioning = DateTime.UtcNow;

                        if ( _instrumentController.TestForInstrumentReset( response, "calibrating sensor, sensor preconditioned" ) == true )
                        {
                            Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
                            return response;
                        }

                        // SGF  Jan-13-2009  DSW-173
                        stopwatch = Log.TimingBegin( "CAL - PAUSE GAS FLOW" );
                        _instrumentController.PauseGasFlow( gasEndPoint, sensor, response );
                        Log.TimingEnd( "CAL - PAUSE GAS FLOW", stopwatch );

                        if ( _instrumentController.TestForInstrumentReset( response, "calibrating sensor, gas flow paused" ) == true )
                        {
                            Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
                            return response;
                        }

                        // Get the sensor's maximum reading.
                        double maximumReading = _instrumentController.GetSensorMaximumReading( _component.Position, sensor.Resolution );

                        response.BaseLine = _instrumentController.GetSensorBaseline( _component.Position );
                        response.ZeroOffset = _instrumentController.GetSensorZeroOffset( _component.Position, sensor.Resolution );
                        response.AccessoryPump = _instrumentController.AccessoryPump;

                        int calFlowRate = _instrumentController.GetSensorCalibrationFlowRate( _component );

                        _instrumentController.OpenGasEndPoint( gasEndPoint, calFlowRate );

                        DateTime startTime = DateTime.UtcNow;
                        int totalReadings = 0;

                        Log.Debug( "CALIBRATION: BEGINNING CALIBRATION ON POSITION " + _component.Position + ", UID=" + _component.Component.Uid );

                        // Send the command to begin calibrating.
                        _instrumentController.BeginSensorCalibration( new int[] { _component.Position } );

                        Log.Debug( "CALIBRATION: Taking readings every " + _WAIT_INTERVAL + " msecs" );

                        #region CALIBRATION LOOP

                        stopwatch = Log.TimingBegin( "CAL - CALIBRATE SENSOR" );

                        Status calibrationStatus = Status.InProgress;

                        bool instResetting = false;

						bool? isCalibrating = false;
						bool hasAborted = false;
                        Pump.IsBadPumpTubing = false;
                        do
                        {
                            TimeSpan calTime = DateTime.UtcNow - startTime;
                            if ( calTime > calTimeOut )
                            {
                                Log.Debug( "CALIBRATION: DS timing out calibration.  Setting status to + " + Status.Failed );
                                calibrationStatus = Status.Failed;
                                break;
                            }

#if !TEST
                            Thread.Sleep( _WAIT_INTERVAL );
#endif

                            if ( !Master.Instance.ControllerWrapper.IsDocked() ) // watch out for user undocking during the long sleep interval.
                                break;

                            //If bad pump tubing is detected, throw flow failed exception
                            //which will further down be handled to report to iNet the situation
                            //and display appropriate error on LCD.
                            if ( Master.Instance.PumpWrapper.IsBadPumpTubing() )
                            {
                                string msg = "CALIBRATION: Bad pump tubing is detected.  Stopping calibration.";
                                Log.Debug(msg);
                                throw new FlowFailedException(gasEndPoint);
                            }

                            // Get current reading.
                            response.Reading = _instrumentController.GetSensorCalibrationReading( _component.Position, sensor.Resolution );
                            response.Time = DateTime.UtcNow;

                            instResetting = _instrumentController.TestForInstrumentReset( response, "calibrating sensor, getting reading" );

                            totalReadings++;

                            Log.Debug( "CALIBRATION: (" + _component.Position + "), Reading #" + totalReadings + ": " + response.Reading + ", Span: " + response.FullSpanReserve );

							// isCalibrating will be null if the instrument reset (InstrumentAborted)
							isCalibrating = _instrumentController.IsSensorCalibrating( _component.Position );
							hasAborted = isCalibrating == null ? true : false;
						}
                        while ( isCalibrating == true
                        && Master.Instance.PumpWrapper.GetOpenValvePosition() > 0
                        && instResetting == false );

                        Log.TimingEnd( "CAL - CALIBRATE SENSOR", stopwatch );

#endregion CALIBRATION LOOP

                        // If we detect we're undocked, then assume that's why we broke out of above loop
                        // debug: Do we really need this check?
                        if ( !Master.Instance.ControllerWrapper.IsDocked() )
                        {
                            string msg = "CALIBRATION: Aborting on sensor " + _component.Position + " - Undocked instrument.";
                            Log.Debug( msg );
                            throw new InstrumentNotDockedException();
                        }

                        if ( instResetting == false )
                            instResetting = _instrumentController.TestForInstrumentReset( response, "calibrating sensor, calibration finished" );

                        bool flowFailed = Master.Instance.PumpWrapper.GetOpenValvePosition() <= 0;

                        TimeSpan elapsedTime = DateTime.UtcNow - startTime;

                        // Put info for the cylinder used during the bump into the Response object.
                        // (iNet needs to know)
                        response.UsedGasEndPoints.Add( new UsedGasEndPoint( gasEndPoint, CylinderUsage.Calibration, elapsedTime ) );

                        // If we detect flow failure, then assume that's why we broke out of above loop.
                        if ( flowFailed )
                        {
                            // TODO - Abort calibration on instrument?
                            string msg = "CALIBRATION: Aborting on sensor " + _component.Position + " - Flow failed.";
                            Log.Debug( msg );
                            throw new FlowFailedException( gasEndPoint );
                        }

                        if ( calibrationStatus == Status.Failed )  // Timed out in above loop by IDS?
                        {
                            // TODO - Tell instrument to abort calibration?
                            Log.Debug( "CALIBRATION: TIMED OUT by DS after " + elapsedTime.TotalSeconds + " seconds!" );
                        }
						else if ( instResetting == true )
						{
							Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
                            return response;
						}
						else  // Find out if instrument decided to pass or fail the calibration
						{
							calibrationStatus = _instrumentController.GetSensorCalibrationStatus( _component.Position ) ? Status.Passed : Status.SpanFailed;
							response.Time = DateTime.UtcNow;

							if ( _instrumentController.TestForInstrumentReset( response, "calibrating sensor, retrieving calibration status" ) == true )
							{
								Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
                                return response;
							}
                            
							Log.Debug( string.Format( "CALIBRATION: Instrument {0} sensor {1} after {2} seconds!",
							calibrationStatus, _component.Position, elapsedTime.TotalSeconds ) );

							// Get instrument's final span reading
							response.Reading = _instrumentController.GetSensorSpanReserve( _component.Position );
							response.SpanCoef = _instrumentController.GetSensorSpanCoeff( _component.Position );

							response.Status = calibrationStatus;                            

							// Checking for obviously screwed up sensor.
							if ( hasAborted )
							{
								// we already know the instrument reset
								response.Status = Status.InstrumentAborted;
							}                            
							else if ( response.Status == Status.Passed )
							{
								// last calibration time is only changed on the sensor if calibration passed
								response.PostCal_LastCalibrationTime = _instrumentController.GetSensorLastCalibrationTime( response.Position );

								// status should never be passed if span reserve is 0 or below
								if ( response.FullSpanReserve <= 0 )
								{
									Log.Warning( string.Format( "CALIBRATION: FullSpanReserve is {0} but status is {1}. DS overriding with a Failed status.", response.FullSpanReserve, response.Status ) );
									response.Status = Status.Failed;
								}
								// last calibration time (pre-cal vs post-cal) should have changed
								else if ( response.WasCalibrationInstrumentAborted() )
								{
									Log.Warning( string.Format( "CALIBRATION: Status is {0}, but LastCalibrationTime did not change.  DS overriding with an InstrumentAborted status.", response.Status ) );
									response.Status = Status.InstrumentAborted; // A new response object will be created so most values are cleared before uploading to iNet.
								}

                                //INS-7282 - Check the sensor span reserve and if the value is less than the configured threshold, set the sensor calibration as failed. Applicable only to repair Accounts.
                                if ( IsSensorFailedForRepairAccount( sensor, response, Configuration.DockingStation.SpanReserveThreshold ) )
                                {
                                    response.Status = Status.Failed;
                                }
							}                           
						}

                        Log.Debug( "CALIBRATION: " + response.Status
                            + " - FullSpanReserve=" + response.FullSpanReserve
                            + " SpanReading=" + response.Reading
                            + " max=" + maximumReading );

                        if ( response.Status == Status.Passed )
                            return response;

                        alreadyFailed = true;
                    }
                    finally
                    {
                        _instrumentController.CloseGasEndPoint( gasEndPoint );
                    }
                }  // end-while(true)
            }
            finally
            {
                // How long did this sensor take to calibrate?
                TimeSpan duration = DateTime.UtcNow - durationStart;
                response.Duration = Convert.ToInt32( duration.TotalSeconds );

                // SGF  14-Jun-2011  INS-1732
                _cumulativeCalTestResponseTime = _cumulativeCalTestResponseTime + response.Duration;
                response.CumulativeResponseTime = _cumulativeCalTestResponseTime;

                ResetTriedGasEndPoints();

                string msg = string.Empty;
                try
                {
                    if ( Master.Instance.ControllerWrapper.IsDocked() )
                    {
                        msg = "SetCalibrationGasConcentration";

                        //// Make certain oxygen sensors are set to ambient air concentrations.
                        ////
                        //// TODO - Prior to the calibration, the O2 sensor may have had a cal gas
                        //// concentration setting other than 20.9, so we probably shouldn't be blindly setting it back to 20.9.
                        //// This likely needs to be corrected as part of INS-1189 (which used to be DSW-156, which used to be DSZ-1305).
                        //// - JMP, 9/28/2009
                        ////
                        //if ( sensor.CalibrationGas.Code == GasCode.O2 )
                        //    // Guarantee that the correct calibration gas concentration is available.
                        //    SetCalibrationGasConcentration( GetSensorZeroAir( _component, false ) );

                        // SGF  11-Oct-2010  INS-1189
                        // Restore the concentration that was present upon entry into this method
                        _instrumentController.SetCalibrationGasConcentration( _component, currentConcentration, true );

                        msg = "GetSensorBumpStatus";

                        //Suresh 22-Feb-2012 INS-2705
                        //After calibration is completed , we need to update sensor bump test status because in scheduler we have logic
                        //to force calibration based on sensor BumpTestStatus
                        sensor.BumpTestStatus = _instrumentController.GetSensorBumpStatus( _component.Position );
                        // INETQA-4178 RHP v7.6 Update the return event BumpTestStatus as this is used the eventprocessor to update switch service instrument 
                        // this is required for the scheduler logic discussed above
                        Sensor installedSensor = (Sensor)_returnEvent.DockedInstrument.InstalledComponents.Find(ic => ic.Component.Uid == sensor.Uid).Component;
                        if (installedSensor != null)
                            installedSensor.BumpTestStatus = sensor.BumpTestStatus; 
                    }
                }
                catch ( Exception e )
                {
                    Log.Error( msg, e );
                }
            }

        }  // end-CalibrateSensor()
    }
}
