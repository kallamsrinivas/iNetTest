using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;
using ISC.iNet.DS.Services.Resources;

namespace ISC.iNet.DS.Services
{
	/// <summary>
	/// Provides functionality to perform a bump test on a portable instrument.
	/// </summary>
	public partial class InstrumentBumpTestOperation 
	{
        /// <summary>
        /// The GasEndPoints handed to this Operation during its instantiation will be segregrated
        /// into being used for multiple bump test "passes". During each "pass", a subset
        /// of all the installed sensors will be bump tested using the GasEndPoint assigned to that pass.
        /// </summary>
        private List<GasEndPoint> _passesEndPointList = new List<GasEndPoint>();

        protected internal enum SensorBumpOpStatus
        {
            NotDone = 0,
            Skipped = 1,
            Preconditioning = 2,
            PreconditionSkipped = 3,
            PreconditionFailed = 4,
            PreconditionPassed = 5,
            BumpTesting = 6,
            O2RecoveryFailed = 7,
            BumpTestFailedTimedOut = 8,
            BumpTestFailedPumpClosed = 9,
            BumpTestPassed = 10
        }

        protected internal class BumpTestSensorInfo
        {
            protected internal const int PASS_UNKNOWN = -1;
            protected internal const int PASS_SENSOR_NOT_BUMP_ENABLED = -2;
            protected internal const int PASS_SENSOR_SKIPPED = -3;

            protected internal BumpTestSensorInfo(InstalledComponent installedComponent)
            {
                InstalledComponent = installedComponent;
                PreconditionTimeOut = TimeSpan.Zero;
                PassIndex = PASS_UNKNOWN;
                OpStatus = SensorBumpOpStatus.NotDone;
                O2RecoveryStatus = Status.Unknown;
            }

            protected internal InstalledComponent InstalledComponent { get; set; }
            protected internal SensorGasResponse SGR { get; set; }
            protected internal double MaximumReading { get; set; }
            protected internal int FlowRate { get; set; }
            protected internal int TotalReadings { get; set; }
            protected internal int PassedReadings { get; set; }
            protected internal long PreconditionPauseTime { get; set; }
            protected internal int PreconditionFlowRate { get; set; }
            protected internal TimeSpan PreconditionTimeOut { get; set; }
            protected internal Status OriginalStatus { get; set; }
            protected internal int PreconditionTotalReadings { get; set; }
            protected internal int PreconditionOddReadings { get; set; }
            protected internal int PreconditionGoodReadings { get; set; }
            protected internal int PassIndex { get; set; }
            protected internal SensorBumpOpStatus OpStatus { get; set; }
            protected internal Status O2RecoveryStatus { get; set; }

            /// <summary>
            /// Used to sort a list of BumpTestSensorInfos. This comparer ensures that
            /// the BumpTestSensorInfos are sorted first by PassIndex, and then by 
            /// Position within each PassIndex.
            /// </summary>
            /// <param name="csi1"></param>
            /// <param name="csi2"></param>
            /// <returns></returns>
            static internal int Compare(BumpTestSensorInfo bsi1, BumpTestSensorInfo bsi2)
            {
                if (bsi1.PassIndex < bsi2.PassIndex) return -1;
                if (bsi1.PassIndex > bsi2.PassIndex) return 1;

                if (bsi1.InstalledComponent.Position < bsi2.InstalledComponent.Position) return -1;
                if (bsi1.InstalledComponent.Position < bsi2.InstalledComponent.Position) return -1;

                return 0;
            }
        }

        /// <summary>
        /// Returns the "pass" that the GasEndPoint has been assigned.
        /// -1 is returned if the GasEndPoint has not yet been assigned to a pass.
        /// </summary>
        /// <param name="gasEndPoint"></param>
        /// <returns></returns>
        protected internal int GetGasEndPointPass(GasEndPoint gasEndPoint)
        {
            for (int i = 0; i < _passesEndPointList.Count; i++)
            {
                if (_passesEndPointList[i] == gasEndPoint)
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
        protected internal GasEndPoint GetPassGasEndPoint(int pass)
        {
            pass--; // PassIndexes are one-based; we need to convert to zero-based before accessing our List.
            return (pass < 0 || pass >= _passesEndPointList.Count) ? null : _passesEndPointList[pass];
        }

        protected internal void BumpTestInstrumentParallel()
        {
            #region Variable Declarations

            bool flowFailed = false;
            Exception thrownException = null;
            GasEndPoint emptyGasEndPoint = null;
            const string logLabel = "BUMP: ";
            GasEndPoint gasEndPoint = null;
            bool isBumpTestStarted = false;

            #endregion

            try
            {
                do
                {
                    #region --------------- INITIALIZATION TASKS -----------------

                    gasEndPoint = null;
                    isBumpTestStarted = false;
                    int numberSensorsToBumpTest = 0;
                    int numberPasses = 0;
                    List<BumpTestSensorInfo> bumpTestSensorInfoList = new List<BumpTestSensorInfo>();
                    List<InstalledComponent> workingSensorList = GetWorkingSensorList();

                    // Let the initialization begin!
                    foreach (InstalledComponent installedComponent in workingSensorList.FindAll(c => c.Component is Sensor))
                    {
                        Sensor sensor = (Sensor)installedComponent.Component;
                        if (!sensor.Enabled) // Skip sensors that are not enabled.
                        {
                            //_detailsBuilder.AddNewLine();                                                                           // TODO ONLY FOR IDS
                            //_detailsBuilder.Add("    ", "DETAILS_BUMP_SENSOR", string.Empty);                                       // TODO ONLY FOR IDS
                            //_detailsBuilder.Add("        ", "DETAILS_BUMP_SENSOR_POSITION", installedComponent.Position);           // TODO ONLY FOR IDS
                            //_detailsBuilder.Add("        ", "DETAILS_BUMP_STATUS", _detailsBuilder.GetText("DISABLED"));            // TODO ONLY FOR IDS
                            continue;
                        }

                        // Create a BumpTestSensorInfo to record information on the current sensor.
                        BumpTestSensorInfo bsi = new BumpTestSensorInfo(installedComponent);
                        bumpTestSensorInfoList.Add(bsi);

                        // Populate properties of the BumpTestSensorInfo to represent the current sensor.
                        bsi.SGR = _returnEvent.GetSensorGasResponseByUid(sensor.Uid);
                        bsi.SGR.GasConcentration = new GasConcentration(GasType.Cache[sensor.GetGasToCal()], sensor.CalibrationGasConcentration);   // TODO IDS CALLS _instrumentController.GetGasToCal(sensor); CHECK HERE FOR IDS
                        bsi.SGR.Position = installedComponent.Position; // Record the position of the sensor in the instrument.
                        bsi.SGR.Status = Status.Failed;
                        bsi.SGR.AccessoryPump = _instrumentController.AccessoryPump;    // FROM IDS
                        bsi.PreconditionPauseTime = sensor.BumpPreconditionPauseTime;   // TODO FOR IDS ITS _instrumentController.GetSensorBumpPreconditionPause(sensor); BUT THIS CODE COVERS IDS.
                        bsi.PreconditionFlowRate = _instrumentController.GetSensorBumpPreconditionFlowRate(installedComponent);
                        bsi.PreconditionTimeOut = _instrumentController.GetSensorBumpPreconditionTimeout(installedComponent);
                        // For CLO2 sensors, we use a hard limit of of 10% for the bump threshold that overrides the usual setting.
                        // BEGIN IDS ONLY
                        bsi.SGR.Threshold = this.BumpThreshold; // Set threshold being used for this bump test.
                        bsi.SGR.Timeout = this.BumpTimeout;
                        bsi.SGR.Type = GasResponseType.Bump;
                        // END IDS ONLY

                        bsi.OpStatus = SensorBumpOpStatus.NotDone; // Initialize the status for the bump test of this sensor.

                        bsi.O2RecoveryStatus = Status.Unknown;

                        // See if this sensor is enabled for bump testing on the iNet DS docking station.  If not, set up response to indicate Skipped.
                        if (!sensor.IsBumpEnabled(GasEndPoints))
                        {
                            bsi.OpStatus = SensorBumpOpStatus.Skipped;
                            Log.Debug("BUMP TEST: Bump is disabled for sensor " + installedComponent.Position + " (" + sensor.Type.Code + ")");
                            bsi.SGR.Status = Status.Skipped;
                            bsi.SGR.Time = DateTime.UtcNow;
                            bsi.SGR.Reading = 0.0;
                            bsi.PassIndex = BumpTestSensorInfo.PASS_SENSOR_NOT_BUMP_ENABLED;
                            Log.Debug("BUMP TEST: SKIPPING SENSOR " + installedComponent.Position + " (" + sensor.Type.Code + ")");
                        }

                        // See if the sensor is scheduled to be included in the bump test operation.  If not, mark it as Skipped.
                        if (ComponentCodes.Count != 0 && !ComponentCodes.Contains(sensor.Type.Code))
                        {
                            bsi.OpStatus = SensorBumpOpStatus.Skipped; // TODO NEW LINE INSERTED TO FIX SENSOR SPECIFIC BUMP ISSUE CHECK CCB # AND PUT IN HERE
                            Log.Debug(string.Format("BUMP TEST: Skipping sensor {0} ({1}) not included in schedule's specified component list.", installedComponent.Position, sensor.Type.Code));
                            bsi.SGR.Status = Status.Skipped;
                            bsi.SGR.Time = DateTime.UtcNow;
                            bsi.SGR.Reading = 0.0;
                            bsi.PassIndex = BumpTestSensorInfo.PASS_SENSOR_SKIPPED;
                        }

                        // Get the gas end point to be used for this sensor.  Also, record which pass of the calibration will process this sensor.
                        ResetTriedGasEndPoints();

                        // Obtain the sensor gas end point if the sensor is not to be skipped during the bump test operation
                        if (bsi.OpStatus != SensorBumpOpStatus.Skipped)
                        {
                            gasEndPoint = null;

                            //  BEGIN INETDS ONLY CODE
                            if ((sensor.Type.Code == SensorCode.CombustibleLEL || sensor.Type.Code == SensorCode.CombustiblePPM)
                            && (Configuration.DockingStation.CombustibleBumpTestGas.Length > 0))
                            {
                                Log.Debug(string.Format("BUMP TEST: Overriding sensor cal gas {0} with {1} CombustibleBumpTestGas setting.",
                                    sensor.CalibrationGas, Configuration.DockingStation.CombustibleBumpTestGas));
                                sensor.CalibrationGas = GasType.Cache[Configuration.DockingStation.CombustibleBumpTestGas];
                                bsi.SGR.GasConcentration = new GasConcentration(sensor.CalibrationGas, sensor.CalibrationGasConcentration);
                            }

                            if (sensor.Type.Code == SensorCode.ClO2 && sensor.IsBumpEnabled(GasEndPoints))
                            {
                                Log.Debug(string.Format("BUMP TEST: Overriding sensor cal gas {0} with {1}.", sensor.CalibrationGas, GasCode.Cl2));
                                sensor.CalibrationGas = GasType.Cache[GasCode.Cl2];
                                bsi.SGR.GasConcentration = new GasConcentration(sensor.CalibrationGas, sensor.CalibrationGasConcentration);
                            }

                            // For O2 sensors, if N2 is available, then it is preferred, so we first look for a cylinder containing only it.
                            //if (installedComponent.Component.Type.Code == SensorCode.O2) // TODO MAKE THIS COMMON FOR IDS TOO ???
                            //{
                            //    // to do:  Make sure we can track whether a nitrogen cylinder that is empty can be ignored on a second pass.
                            //    Sensor n2Sensor = (Sensor)sensor.Clone();
                            //    n2Sensor.CalibrationGas = new GasType(GasCode.N2, sensor.CalibrationGas.CalOrder, sensor.CalibrationGas.BumpOrder, 0.0, "N2", true, "Nitrogen");
                            //    n2Sensor.CalibrationGasConcentration = 0.0d;
                            //    gasEndPoint = GetSensorGasEndPoint(n2Sensor);
                            //}
                            //  END INETDS ONLY CODE

                            if (gasEndPoint == null)
                                gasEndPoint = GetSensorGasEndPoint(sensor);

                            if (gasEndPoint == null) // No bump gases were found?
                                throw new CorrectBumpTestGasUnavailable(string.Format("Sensor {0}, CalGas={1}({2})",
                                    sensor.Uid, sensor.CalibrationGas.Code, sensor.CalibrationGasConcentration)); // No gas end point was found.

                            //	Set the gas concentration.
                            bsi.SGR.GasConcentration.Concentration = GetCalibrationGasConcentration(installedComponent, gasEndPoint);

                            int passIndex = GetGasEndPointPass(gasEndPoint);
                            if (passIndex < 0)
                            {
                                _passesEndPointList.Add(gasEndPoint);
                                passIndex = _passesEndPointList.Count; // PassIndexes are one-based, so just using the Count after doing an Add should give us the proper value.
                            }

                            bsi.PassIndex = passIndex;

                            // Update the number of passes that will be needed for this bump test.
                            if (numberPasses <= passIndex)
                                numberPasses = passIndex;  // SGF  28-Feb-2013  INS-3934

                            // Record the calibration gas. TODO INETDS CODE CHECK GasType.Cache FOR IDS
                            sensor.CalibrationGas = GasType.Cache[bsi.SGR.GasConcentration.Type.Code];
                        }

                        // Perform additional initializations if the sensor is expected to be bump tested.
                        if (bsi.PassIndex >= 0)
                        {
                            // Get the sensor's maximum reading.
                            bsi.MaximumReading = _instrumentController.GetSensorMaximumReading(installedComponent.Position, sensor.Resolution);

                            // Get the flow rate required for this sensor.
                            bsi.FlowRate = _instrumentController.GetSensorBumpFlowRate(installedComponent);

                            // Increment the number of sensors that will be bump tested during this operation.
                            numberSensorsToBumpTest++;
                        }
                    }

                    ResetTriedGasEndPoints();

                    // If there are no sensors that will be bump tested, exit now.
                    if (numberSensorsToBumpTest <= 0)
                    {
                        //_detailsBuilder.AddNewLine();   // TODO IDS ONLY
                        //_detailsBuilder.Add("NO SENSORS TO BUMP TEST"); // TODO IDS ONLY
                        return;
                    }

                    // Confirm that each sensor with a non-negative index has a gasEndPoint defined.  If any does not, throw exception.
                    foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                    {
                        if ((bsi.PassIndex >= 0) && (GetPassGasEndPoint(bsi.PassIndex) == null))
                            throw new CorrectBumpTestGasUnavailable(_returnEvent.DockedInstrument.SerialNumber);
                    }

                    // Sort the BumpTestSensorInfos list by PassIndex/Position, so that in all the logging
                    // we do in the various foreach loops below, there is a consistent order.
                    bumpTestSensorInfoList.Sort(BumpTestSensorInfo.Compare);

                    // Log information found in the bump sensor info list.
                    foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                    {
                        Log.Debug(string.Format("{0}Sensor UID = {1}", logLabel, bsi.InstalledComponent.Component.Uid));
                        Log.Debug(string.Format("{0}Sensor Position = {1}", logLabel, bsi.InstalledComponent.Position));
                        Log.Debug(string.Format("{0}GasCode = {1}, Concentration={2}, BumpThreshold={3}",
                            logLabel, bsi.SGR.GasConcentration.Type.Code, bsi.SGR.GasConcentration.Concentration, this.BumpThreshold));
                        //Log.Debug(string.Format("{0}GasConcentration Type Symbol = {1}", _logLabel, bsi.BumpSGR.GasConcentration.Type.Symbol));  // SGF  28-Feb-2013  INS-3934

                        Log.Debug(string.Format("{0}AccessoryPump = {1}", logLabel, bsi.SGR.AccessoryPump)); // FROM IDS 
                        Log.Debug(string.Format("{0}Precondition Pause = {1}", logLabel, bsi.PreconditionPauseTime));
                        Log.Debug(string.Format("{0}Precondition Flow Rate = {1}", logLabel, bsi.PreconditionFlowRate));
                        Log.Debug(string.Format("{0}Precondition Time Out = {1}", logLabel, bsi.PreconditionTimeOut));
                        Log.Debug(string.Format("{0}Bump Pass Index = {1}", logLabel, bsi.PassIndex));

                        if (bsi.PassIndex >= 0)
                        {
                            Log.Debug(string.Format("{0}Bump Max Reading = {1}", logLabel, bsi.MaximumReading));
                            Log.Debug(string.Format("{0}Bump Flow Rate = {1}", logLabel, bsi.FlowRate));
                        }
                    }
                    Log.Debug(string.Format("{0}Total Number of Passes = {1}", logLabel, numberPasses));
                    Log.Debug(string.Format("{0}Total Number of Sensors to Bump = {1}", logLabel, numberSensorsToBumpTest));

                    #endregion  --------------- INITIALIZATION TASKS -----------------


                    #region --------------- BUMP TEST TASKS -----------------

                    // Put instrument into bump test mode.
                    // Note that we take the instrument back out of bump test mode below in the 'finally' block.
                    Log.Debug(string.Format("{0}BEGIN INSTRUMENT BUMP TEST", logLabel));

                    //throw new ApplicationException( "BUMP TEST ABORTED.  NOT READY TO INVOKE BeginInstrumentBump" );

                    Stopwatch turnOnStopwatch = Log.TimingBegin("BUMP - TURN ON SENSORS");
                    _instrumentController.BeginInstrumentBump();  // also has side effect of turning on sensors.

                    // BEIGN INS-7657 RHP v7.5.2
                    DateTime biasStateLoopStartTime = DateTime.UtcNow;
                    TimeSpan biasStateElapsedTime = TimeSpan.Zero;

                    while (!_instrumentController.GetSensorBiasStatus())
                    {
                        // Calculate the time that has elapsed since the start of the calibration loop
                        biasStateElapsedTime = DateTime.UtcNow - biasStateLoopStartTime;
                        Log.Debug(string.Format("{0} Time elapsed in Bias State pass = {1}", logLabel, (int)biasStateElapsedTime.TotalSeconds));

                        Master.Instance.ConsoleService.UpdateState(ConsoleState.BumpingInstrument, new string[] { string.Format(ConsoleServiceResources.ELAPSEDBIASSTATE, Math.Round(biasStateElapsedTime.TotalSeconds).ToString()) });

                        if (biasStateElapsedTime.TotalSeconds > _biasStateTimeout) // Have we timed out?
                        {
                            Log.Debug("Timing out Bias State Check");
                            throw new InstrumentNotReadyException("InstrumentBumpTestOperation received an Instrument NotReadyException!");
                        }
                        // Allow a ten second interval so that we give some interval before we check the sensor Bias state again.
                        Thread.Sleep(10000);
                    }
                    //END INS-7657

                    isBumpTestStarted = true;
                    Log.TimingEnd("BUMP - TURN ON SENSORS", turnOnStopwatch);

                    Stopwatch purgeStopwatch = Log.TimingBegin("BUMP - PURGE(INITIAL)");
                    new InstrumentPurgeOperation(PurgeType.PreBump, _instrumentController, GasEndPoints, _returnEvent).Execute();
                    Log.TimingEnd("BUMP - PURGE(INITIAL)", purgeStopwatch);

                    #region Oxygen Sensor Recovery Purge

                    // SGF  24-Aug-2011  INS-2314 -- initialize flag to signify that O2 recovery purge has been performed
                    bool ranO2RecoveryPurge = false;

                    foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                    {
                        Sensor sensor = (Sensor)bsi.InstalledComponent.Component;
                        if ((bsi.InstalledComponent.Component.Type.Code == SensorCode.O2)
                        //  SGF (Suresh)  3-Oct-2012  INS-2709
                        && sensor.Enabled
                        && sensor.IsBumpEnabled(GasEndPoints)
                        // TODO: INS-4619 - This logic doesn't look like it makes sense
                        && (!(ComponentCodes.Count > 0)
                        && !ComponentCodes.Contains(SensorCode.O2))) // skip the O2 recovery purge if O2 is not included in a sensor-specific bump test schedule
                        {
                            Stopwatch o2PurgeStopwatch = Log.TimingBegin("BUMP - PURGE(O2)");
                            if (IsFullO2RecoveryPurgeNeeded())
                            {
                                // At this point, if there are 1 or more O2 sensors to be bumped, the instrument should be 
                                // purged to allow the O2 sensors to recover to "normal" readings prior to their bump tests.
                                new InstrumentPurgeOperation(PurgeType.O2Recovery, _instrumentController, GasEndPoints, _returnEvent).Execute();
                            }
                            Log.TimingEnd("BUMP - PURGE(O2)", o2PurgeStopwatch);
                            ranO2RecoveryPurge = true;

                            // Breaking out of loop because the O2 recovery purge will run for all installed 
                            // O2 sensors.
                            break;
                        }
                    }


                    if (ranO2RecoveryPurge)
                    {
                        // Learn what the status was of the O2 recovery, and store that information away for later.
                        foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                        {
                            if (bsi.InstalledComponent.Component.Type.Code == SensorCode.O2)
                            {
                                bsi.O2RecoveryStatus = bsi.SGR.Status;
                                // BEGIN INS-7625 SSAM v7.6                                 
                                Sensor sensor = bsi.InstalledComponent.Component as Sensor;
                                SensorGasResponse sgr = _returnEvent.GetSensorGasResponseByUid(sensor.Uid);
                                SensorGasResponse oxygenCalResponse = new SensorGasResponse(sensor.Uid, DateTime.UtcNow);
                                // Calibration should only occur if O2 High Bump failed and O2 sensor's cal gas concentration is set to 20.9 or 21.0 %VOL. 
                                if (!sgr.IsO2HighBumpPassed && (sensor.CalibrationGasConcentration == 20.9 || sensor.CalibrationGasConcentration == 21.0))
                                {
                                    Log.Debug(string.Format("{0}O2 High Bump Test Failed for Sensor {1}", logLabel, sensor.Uid));
                                    Stopwatch o2CalStopwatch = Log.TimingBegin("BUMP - CALIBRATION(O2)");
                                    try
                                    {
                                        InstrumentCalibrationOperation O2Calibration = new InstrumentCalibrationOperation();

                                        // Pass the O2 component to be calibrated, GasEndPoints List (Since call is going from Bump Test Action, this will be empty and needs to be passed)
                                        // the Gas end points list is empty while calibrating
                                        oxygenCalResponse = O2Calibration.CalibrateSensor(bsi.InstalledComponent, GasEndPoints, true);

                                        // Add the O2 Calibration results to the bump test return event to save in database and upload to iNet  
                                        _returnEvent.HighBumpFailCalGasResponses.Add(oxygenCalResponse);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Debug(string.Format("{0}O2 calibration threw {1} for Sensor {2}", logLabel, ex.Message, sensor.Uid));
                                        throw;
                                    }
                                    finally
                                    {
                                        // BeginInstrumentBump() command was already sent before initiating O2 High Bump Test and when calibrating,
                                        // BeginSensorCalibration() command was sent. So now while continuing with BUmp Test again, IDS shows "Testing Instrument",
                                        // Whereas docked instrument shows "Calibrating". So Sending a EndInstrumentCalibration() command and then sending back a
                                        // BeginInstrumentBump() to continue with a Bump Test. Just to note O2 is the last to do a Bump Test.
                                        if (Master.Instance.ControllerWrapper.IsDocked())
                                        {
                                            _instrumentController.EndInstrumentCalibration();
                                            _instrumentController.BeginInstrumentBump();
                                        }
                                    }
                                    Log.TimingEnd("BUMP - CALIBRATION(O2)", o2CalStopwatch);
                                }
                                if (oxygenCalResponse.Status == Status.Passed)
                                {
                                    Log.Debug(string.Format("{0}O2 calibration passed for Sensor {1}", logLabel, sensor.Uid));
                                    Log.Debug(string.Format("{0}Beginning Second High Bump Test for Sensor {1}", logLabel, sensor.Uid));
                                    sgr.IsSecondO2HighBump = true;
                                    new InstrumentPurgeOperation(PurgeType.O2Recovery, _instrumentController, GasEndPoints, _returnEvent).Execute();
                                    bsi.O2RecoveryStatus = bsi.SGR.Status; // Update the O2 recovery status after second high bump
                                    // Restting the IsSecondO2HighBump flag to "False" to ensure that purge timings are being set to default values
                                    // The purge timing is overidden when this flag is set to "true".
                                    sgr.IsSecondO2HighBump = false;
                                    Log.Debug(string.Format("{0} Second High Bump Test - O2 RECOVERY PURGE COMPLETE", logLabel));
                                }
                                // END INS-7625
                            }
                        }
                        // Check if Oxygen sensor was calibrated as a part of high bump test failure. If so, create fake records for other sensors that are currently installed.
                        // DSX uses the event journals to verify if sensors are newly installed, removed or if their positions have changed.
                        // If only oxygen sensor's calibration record is found, DSX treats the rest of the sensors as newly installed and triggers a calibration.
                        // TODO ONLY FOR INETDS
                        if (_returnEvent.HasHighBumpFailCalGasResponses)
                        {
                            foreach (InstalledComponent ic in _returnEvent.DockedInstrument.InstalledComponents.FindAll(c => c.Component is Sensor))
                            {
                                Sensor sensor = (Sensor)ic.Component;
                                if (!sensor.Enabled) continue; // Skip sensors that are not enabled.
                                if (_returnEvent.HighBumpFailCalGasResponses.Exists(sgr => sgr.Uid == sensor.Uid)) continue; // Skip if O2 Calibration list already contains the sensor gas response

                                SensorGasResponse response = new SensorGasResponse(sensor.Uid, DateTime.UtcNow);
                                response.Type = GasResponseType.Calibrate;
                                response.Status = Status.Skipped;
                                response.Position = ic.Position;
                                response.GasConcentration = new GasConcentration(sensor.CalibrationGas, sensor.CalibrationGasConcentration);
                                response.GasDetected = sensor.GasDetected;
                                response.Reading = 0.0;
                                _returnEvent.HighBumpFailCalGasResponses.Add(response);
                            }
                        }
                    }
                    else
                    {
                        Log.Debug(string.Format("{0}O2 RECOVERY PURGE NOT NEEDED", logLabel));
                    }


                    // SGF  30-Jul-2012  DEV JIRA INS-4803 -- check for undocked instrument
                    // If instrument is not docked, throw InstrumentNotDockedException
                    if (!Master.Instance.ControllerWrapper.IsDocked())
                        throw new InstrumentNotDockedException();

                    #endregion Oxygen Sensor Recovery Purge


                    // Loop based on pass index values from <0> to <number of passes-1>
                    flowFailed = false;
                    for (int currentPass = 1; currentPass <= numberPasses && !flowFailed; currentPass++)  // SGF  28-Feb-2013  INS-3934
                    {
                        Stopwatch passStopwatch = Log.TimingBegin("BUMP - PASS " + currentPass);

                        Log.Debug(string.Format("{0}BUMP TEST PASS {1}", logLabel, currentPass));

                        gasEndPoint = GetPassGasEndPoint(currentPass);

                        // Extract out all BumpTestSensorInfo's for the current pass into a separate list
                        // that we can iterate over in all the many foreach loops that follow.
                        List<BumpTestSensorInfo> currentPassList = bumpTestSensorInfoList.FindAll(c => c.PassIndex == currentPass);
                        List<SensorGasResponse> currentPassSGR = new List<SensorGasResponse>();
                        currentPassList.ForEach(c => currentPassSGR.Add(c.SGR));

                        // Purge between each passes when switching between attached cylinders during bump test to clear gases in line.
                        // INETQA-4189 RHP v7.6
                        if (currentPass > 1)
                        {
                            Log.Debug("CLEAR GASES IN LINES BEFORE BUMP TESTING NEXT PASS");
                            Stopwatch cylinderSwitchPurgeStopwatch = Log.TimingBegin("BUMP - PURGE(CYLINDER-SWITCH)");
                            new InstrumentPurgeOperation(PurgeType.CylinderSwitch, _instrumentController, GasEndPoints, _returnEvent, currentPassSGR).Execute();
                            Log.TimingEnd("BUMP - PURGE(CYLINDER-SWITCH)", cylinderSwitchPurgeStopwatch);
                        }

                        // SGF  28-Feb-2013  INS-3934 -- Oxygen Sensor Recovery Purge moved to later in the loop iteration

                        #region Present Console Message String

                        // Build up a message containing the sensor labels for each sensor involved in this pass of the bump test.
                        string message = string.Empty;
                        string gasSymbol = string.Empty;

                        foreach (BumpTestSensorInfo bsi in currentPassList)
                        {
                            _instrumentController.BeginSensorBump(bsi.InstalledComponent.Position);  // INS-4953 RHP v7.6  Begin Bump Test on specified sensor.
                            Log.Debug(string.Format("BUMP - BeginSensorBump on position {0}", bsi.InstalledComponent.Position));

                            // If there is no gas end point for this sensor, the throw an exception
                            if (gasEndPoint == null)
                                throw new CorrectBumpTestGasUnavailable(_returnEvent.DockedInstrument.SerialNumber);

                            // Check to see that the selected cylinder contains the calibration gas that is 
                            // expected by the sensor.  If it is not, throw an exception.
                            if (!gasEndPoint.Cylinder.ContainsGas(((Sensor)bsi.InstalledComponent.Component).CalibrationGas.Code))
                                throw new CorrectBumpTestGasUnavailable(((Sensor)bsi.InstalledComponent.Component).CalibrationGas.Code);

                            // Add this sensor label to the message string
                            // INS-8630 RHP v9.7 - do not diplay duplicate sensor symbol incase of dual sensors
                            gasSymbol = Master.Instance.ConsoleService.GetSensorLabel(((Sensor)bsi.InstalledComponent.Component).Type.Code);
                            if (!message.Contains(gasSymbol))
                            {
                                if (message.Length > 0)
                                    message += ", ";
                                message += gasSymbol;
                            }
                        }
                        Log.Debug(string.Format("{0}Console message = {1}", logLabel, message));

                        // If somehow there are no sensors to process in this pass, the message string will be empty.
                        // If this is the case, skip the rest of this iteration, and proceed to the next pass.
                        if (message.Length == 0)
                            continue;

                        // Indicate on the console which sensors are being calibrated
                        Master.Instance.ConsoleService.UpdateState(ConsoleState.BumpingInstrument, message);

                        #endregion Present Console Message String

                        // Record the start time for the bump test of sensors in this pass
                        DateTime bumpTestDurationStart = DateTime.UtcNow;

                        #region Preconditioning

                        // Precondition sensors to be bump tested during this pass.
                        try
                        {
                            Stopwatch preconStopwatch = Log.TimingBegin("BUMP - PRECONDITION SENSOR");
                            TimeSpan preconditionTime = PreconditionSensor(bumpTestSensorInfoList, currentPass);
                            Log.TimingEnd("BUMP - PRECONDITION SENSOR", preconStopwatch);

                            Log.Debug(string.Format("{0}Precondition Time = {1} seconds", logLabel, preconditionTime.TotalSeconds));
                            if (preconditionTime.TotalSeconds > 0)
                            {
                                foreach (BumpTestSensorInfo bsi in currentPassList)
                                {
                                    if (bsi.OpStatus == SensorBumpOpStatus.PreconditionPassed || bsi.OpStatus == SensorBumpOpStatus.PreconditionFailed)
                                        bsi.SGR.UsedGasEndPoints.Add(new UsedGasEndPoint(gasEndPoint, CylinderUsage.Precondition, preconditionTime, (short)currentPass));
                                }
                            }
                        }
                        catch (FlowFailedException ffe)
                        {
                            Log.Error("Cylinder Empty", ffe);
                            gasEndPoint.Cylinder.Pressure = PressureLevel.Empty;
                            emptyGasEndPoint = gasEndPoint;
                            //_instrumentController.EndInstrumentBump();
                            //bumpTestSensorInfoList = null;
                            //bumpTestGasEndPointList = null;
                            flowFailed = true;
                            continue;
                        }

                        if (!Master.Instance.ControllerWrapper.IsDocked()) // If instrument is not docked, throw InstrumentNotDockedException
                            throw new InstrumentNotDockedException();

                        #endregion Preconditioning

                        #region Pause Gas Flow

                        // Pause gas flow following preconditioning based on the needs of sensors involved in this pass.
                        long pauseInSeconds = 0;
                        foreach (BumpTestSensorInfo bsi in currentPassList)
                        {
                            if (bsi.PreconditionPauseTime > pauseInSeconds)
                                pauseInSeconds = bsi.PreconditionPauseTime;
                        }
                        Log.Debug(string.Format("{0}Precondition Pause = {1}", logLabel, pauseInSeconds));
                        Stopwatch pauseStopwatch = Log.TimingBegin("BUMP - PAUSE GAS FLOW");
                        _instrumentController.PauseGasFlow(gasEndPoint, pauseInSeconds);
                        Log.TimingEnd("BUMP - PAUSE GAS FLOW", pauseStopwatch);

                        #endregion Pause Gas Flow

                        #region Determine Flow Rate

                        // Loop through the sensors in this pass to determine the highest required flow rate.
                        int highFlowRate = 0;
                        foreach (BumpTestSensorInfo bsi in currentPassList)
                        {
                            int curFlowRate = _instrumentController.GetSensorBumpFlowRate(bsi.InstalledComponent);
                            if (curFlowRate > highFlowRate)
                                highFlowRate = curFlowRate;
                        }
                        Log.Debug(string.Format("{0}Bump Test Flow Rate = {1}", logLabel, highFlowRate));

                        #endregion Determine Flow Rate

                        Stopwatch sensorBumpStopwatch = Log.TimingBegin("BUMP - BUMP SENSORS");

                        #region Init For Sensor Bump Tests

                        // Instruct the instrument that each of the sensors in this pass will begin bump testing.
                        // Also indicate the fact that the status of these sensors is now "Bump Testing", and 
                        // initialize the number of readings taken.
                        foreach (BumpTestSensorInfo bsi in currentPassList)
                        {
                            //_instrumentController.BeginSensorCalibration(bsi.BumpSensorPosition);
                            bsi.OpStatus = SensorBumpOpStatus.BumpTesting;
                            bsi.TotalReadings = 0;
                            bsi.PassedReadings = 0;
                            Log.Debug(string.Format("{0}Bump test begins for sensor {1}", logLabel, bsi.InstalledComponent.Position));
                        }

                        #endregion Init For Sensor Bump Tests

                        _instrumentController.OpenGasEndPoint(gasEndPoint, highFlowRate);

                        DateTime bumpLoopStartTime = DateTime.UtcNow; // record the time the gas end point was opened
                        TimeSpan bumpElapsedTime = TimeSpan.Zero;

                        // The purpose of a bump test is to determine if the
                        // sensor reaches 50% of its Span Reserve within the alloted time.
                        Log.Debug(string.Format("{0}BumpTimeout = {1} seconds", logLabel, this.BumpTimeout));

                        // Keep taking readings until we timeout or run out of gas.
                        Log.Debug(string.Format("{0}BUMP TEST LOOP BEGINS FOR PASS {1}", logLabel, currentPass));
                        bool bumpPassDone = false;

                        Pump.IsBadPumpTubing = false;

                        while (!bumpPassDone && (Master.Instance.PumpWrapper.GetOpenValvePosition() > 0))
                        {
                            if (Master.Instance.PumpWrapper.IsBadPumpTubing())
                            {
                                Log.Debug("Bad pump tubing detected, aborting bump test.");
                                throw new FlowFailedException(gasEndPoint);
                            }

                            // Calculate the time that has elapsed since the start of the bump loop
                            bumpElapsedTime = DateTime.UtcNow - bumpLoopStartTime;
                            if (bumpElapsedTime.TotalSeconds >= this.BumpTimeout) // Have we timed out?   // TODO IDS USES Math.Max(_minBumpTimeSpan.TotalSeconds, this.BumpTimeout) SO CHEKC IT OUT, BUT AGAIN SEE FEW LINES BELOW. i GUESS WE ARE COVERED.
                            {
                                Log.Debug("Timing out bump");
                                break;
                            }

                            // Sleep before taking readings for the sensors involved in this pass.
                            Thread.Sleep(Convert.ToInt32(_sleepTimeSpan.TotalMilliseconds));

                            if (!Master.Instance.ControllerWrapper.IsDocked()) // Check to see if the instrument has been undocked.  If it has, exit the bump test loop.
                                break;

                            // Update elapsed time after sleep
                            bumpElapsedTime = DateTime.UtcNow - bumpLoopStartTime;
                            Log.Debug(string.Format("{0}Time elapsed in bump test pass = {1}", logLabel, (int)bumpElapsedTime.TotalSeconds));
                            if (bumpElapsedTime <= _min5SecondBump)
                                Log.Debug(string.Format("{0}Minimum bump time not yet reached.  Readings will not be assessed.", logLabel));

                            // Get sensor readings for each sensor still bump testing.
                            foreach (BumpTestSensorInfo bsi in currentPassList.FindAll(b => b.OpStatus == SensorBumpOpStatus.BumpTesting))
                            {
                                #region Take a sensor reading

                                // Get the current reading from this sensor.
                                bsi.SGR.Reading = _instrumentController.GetSensorReading(bsi.InstalledComponent.Position, ((Sensor)bsi.InstalledComponent.Component).Resolution);
                                bsi.SGR.Time = DateTime.UtcNow; // Record the time of the reading.
                                bsi.TotalReadings++;  // Increment the count of readings taken for this sensor.

                                Log.Debug(string.Format("{0}Sensor({1}): Reading({2}) = {3}, Span = {4}, Conc = {5}, Res = {6}, Max = {7}, Thresh = {8}", logLabel,
                                    bsi.InstalledComponent.Position, bsi.TotalReadings, bsi.SGR.Reading, bsi.SGR.FullSpanReserve,
                                    bsi.SGR.GasConcentration.Concentration, ((Sensor)bsi.InstalledComponent.Component).Resolution, bsi.MaximumReading, this.BumpThreshold));

                                // INETQA-2108 Do not assess bump test readings before 5 seconds for each pass.
                                if (bumpElapsedTime <= _min5SecondBump)
                                    continue;

                                // SGF  30-Jul-2012  DEV JIRA INS-4803 -- check for undocked instrument
                                // Check to see if the instrument has been undocked.  If it has, exit the bump test loop.
                                if (!Master.Instance.ControllerWrapper.IsDocked())
                                    break;

                                #endregion Take a sensor reading

                                #region Assess the sensor reading

                                // Readings that are too large or small are considered 'odd' and are ignored.
                                if (Math.Abs(bsi.SGR.Reading) > bsi.MaximumReading)
                                    Log.Debug(string.Format("{0} SKIPPING OUT OF RANGE READING: {1}", logLabel, bsi.SGR.Reading));

                                // Changing the testing of bump criteria to allow for more than just comparison of span reserves
                                else if (IsBumpCriterionMet(bsi, this.BumpThreshold))
                                {
                                    bsi.PassedReadings++;

                                    // log how many passed readings are required when more than 1 is needed
                                    int one = 1; // we compare MIN_BUMP_READINGS to a local variable instead of literal value 1, to avoid "unreachable code detected" warning.
                                    if (MIN_BUMP_READINGS != one)
                                        Log.Debug(string.Format("{0}Sensor({1}): {2} of {3} readings required that meet bump criteria", logLabel,
                                            bsi.InstalledComponent.Position, bsi.PassedReadings, MIN_BUMP_READINGS));

                                    if (bsi.PassedReadings >= MIN_BUMP_READINGS)
                                    {
                                        bsi.OpStatus = SensorBumpOpStatus.BumpTestPassed;
                                        TimeSpan sensorElapsedTime = DateTime.UtcNow - bumpLoopStartTime;
                                        bsi.SGR.UsedGasEndPoints.Add(new UsedGasEndPoint(gasEndPoint, CylinderUsage.Bump, sensorElapsedTime, (short)currentPass));
                                        bsi.SGR.Duration = Convert.ToInt32(sensorElapsedTime.TotalSeconds);
                                        Log.Debug(string.Format("{0}Sensor({1}): PASSED", logLabel, bsi.InstalledComponent.Position));
                                    }
                                }

                                #endregion Assess the sensor reading
                            }

                            #region Assess testing status

                            // Determine if all sensors involved in this pass have passed the bump test.
                            bumpPassDone = true;
                            foreach (BumpTestSensorInfo bsi in currentPassList.FindAll(b => b.OpStatus == SensorBumpOpStatus.BumpTesting))
                            {
                                bumpPassDone = false;
                            }

                            Log.Debug(string.Format("{0}Bump test pass done = {1}", logLabel, bumpPassDone ? "TRUE" : "FALSE"));

                            // Check to see if the instrument has been undocked.  If it has, exit the bump test loop.
                            if (!Master.Instance.ControllerWrapper.IsDocked())
                                break;

                            #endregion Assess testing status

                        } // end-while

                        Log.TimingEnd("BUMP - BUMP SENSORS", sensorBumpStopwatch);

                        Log.Debug(string.Format("{0}Bump test loop exited.", logLabel));
                        Log.Debug(string.Format("{0}Bump test pass done = {1}.", logLabel, bumpPassDone ? "TRUE" : "FALSE"));
                        Log.Debug(string.Format("{0}Bump test elapsed time = {1}.", logLabel, (int)bumpElapsedTime.TotalSeconds));
                        Log.Debug(string.Format("{0}Bump test timed out = {1}.", logLabel, (bumpElapsedTime.TotalSeconds >= this.BumpTimeout) ? "TRUE" : "FALSE"));
                        Log.Debug(string.Format("{0}Bump test flow failed = {1}.", logLabel, (Pump.GetOpenValvePosition() > 0) ? "FALSE" : "TRUE"));

                        // SGF  30-Jul-2012  DEV JIRA INS-4803 -- check for undocked instrument
                        // If instrument is not docked, throw InstrumentNotDockedException
                        if (!Master.Instance.ControllerWrapper.IsDocked())
                            throw new InstrumentNotDockedException();

                        // Calculate the amount of time the gas end point has been open
                        TimeSpan elapsedTime = DateTime.UtcNow - bumpLoopStartTime;

                        #region Set Gas Usage and Failure Reasons

                        // Add gas usage information to the sensor gas responses for the just-tested sensors, and 
                        // mark those sensors that were still bump testing when either the bump test timed out or
                        // the pump closed to bump test failed.
                        foreach (BumpTestSensorInfo bsi in currentPassList.FindAll(b => b.OpStatus == SensorBumpOpStatus.BumpTesting))
                        {
                            bsi.SGR.UsedGasEndPoints.Add(new UsedGasEndPoint(gasEndPoint, CylinderUsage.Bump, elapsedTime, (short)currentPass));
                            if (Master.Instance.PumpWrapper.GetOpenValvePosition() <= 0)
                                bsi.OpStatus = SensorBumpOpStatus.BumpTestFailedPumpClosed;
                            else
                                bsi.OpStatus = SensorBumpOpStatus.BumpTestFailedTimedOut;
                            bsi.SGR.Duration = Convert.ToInt32(elapsedTime.TotalSeconds);
                            Log.Debug(string.Format("{0}Sensor({1}) Status={2}, Duration={3}", logLabel,
                                bsi.InstalledComponent.Position, bsi.OpStatus, bsi.SGR.Duration));
                        }

                        #endregion Set Gas Usage and Failure Reasons

                        #region Cumulative Response Time

                        _cumulativeBumpTestResponseTime = _cumulativeBumpTestResponseTime + Convert.ToInt32(elapsedTime.TotalSeconds);

                        foreach (BumpTestSensorInfo bsi in currentPassList)
                            bsi.SGR.CumulativeResponseTime = _cumulativeBumpTestResponseTime;

                        Log.Debug(string.Format("{0}Cumulative bump test response time = {1}", logLabel, _cumulativeBumpTestResponseTime));

                        #endregion Cumulative Response Time

                        // SGF  28-Feb-2013  INS-3934 -- Check for flow failed moved forward to here
                        flowFailed = Master.Instance.PumpWrapper.GetOpenValvePosition() <= 0;
                        if (flowFailed && Pump.IsBadPumpTubing)
                        {
                            throw new FlowFailedException(gasEndPoint);
                        }
                        else if (flowFailed)
                        {
                            gasEndPoint.Cylinder.Pressure = PressureLevel.Empty;
                            emptyGasEndPoint = gasEndPoint;
                        }

                        // SGF  28-Feb-2013  INS-3934 -- Moved forward
                        if (!Master.Instance.ControllerWrapper.IsDocked()) // If instrument is not docked, throw InstrumentNotDockedException
                            throw new InstrumentNotDockedException();

                        _instrumentController.CloseGasEndPoint(gasEndPoint);

                        #region Determine Bump Test Status

                        // The bump test loop has completed.  Determine the status for each sensor involved in 
                        // this pass of the bump test, given the status of the bump test loop and the status 
                        // of the O2 recovery, if applicable to that sensor.
                        foreach (BumpTestSensorInfo bsi in currentPassList)
                        {
                            if (bsi.O2RecoveryStatus == Status.Unknown)
                            {
                                bsi.SGR.Status = bsi.OpStatus == SensorBumpOpStatus.BumpTestPassed ? Status.Passed : Status.Failed;
                                Log.Debug(string.Format("{0}Sensor({1})  {2} sensor -- Bump: {3}, Fresh Air: N/A.",
                                    logLabel, bsi.InstalledComponent.Position, bsi.InstalledComponent.Component.Type.Code,
                                    bsi.OpStatus == SensorBumpOpStatus.BumpTestPassed ? "PASSED" : "FAILED"));
                            }
                            else
                            {
                                // Merge results from this testing with the results already stored in the SensorGasResponse object.
                                if (bsi.O2RecoveryStatus == Status.Passed)
                                {
                                    if (bsi.OpStatus == SensorBumpOpStatus.BumpTestPassed)
                                    {
                                        bsi.SGR.Status = Status.Passed;
                                        Log.Debug(string.Format("{0}Sensor({1})  O2 sensor -- Bump: PASSED, Fresh Air: PASSED.",
                                            logLabel, bsi.InstalledComponent.Position));
                                    }
                                    else
                                    {
                                        bsi.SGR.Status = Status.BumpFailedFreshAirPassed;
                                        Log.Debug(string.Format("{0}Sensor({1})  O2 sensor -- Bump: FAILED, Fresh Air: PASSED.",
                                            logLabel, bsi.InstalledComponent.Position));
                                    }
                                }
                                else
                                {
                                    if (bsi.OpStatus == SensorBumpOpStatus.BumpTestPassed)
                                    {
                                        bsi.SGR.Status = Status.BumpPassedFreshAirFailed;
                                        Log.Debug(string.Format("{0}Sensor({1})  O2 sensor -- Bump: PASSED, Fresh Air: FAILED.",
                                            logLabel, bsi.InstalledComponent.Position));
                                    }
                                    else
                                    {
                                        bsi.SGR.Status = Status.BumpFailedFreshAirFailed;
                                        Log.Debug(string.Format("{0}Sensor({1})  O2 sensor -- Bump: FAILED, Fresh Air: FAILED.",
                                            logLabel, bsi.InstalledComponent.Position));
                                    }
                                }
                            }
                        }

                        // log status for the sensors involved in this pass.
                        foreach (BumpTestSensorInfo bsi in currentPassList)
                        {
                            // IDS CHECKS FOR CONDITION MATCHING CURRENT PASS HERE WHCIH I GUESS IS NOT NECESSARY SINCE currentPassList SHOULD HAVE COVERED THAT
                            Log.Debug(string.Format("{0}BUMP STATUS -- Sensor {1}, Position {2}, Status: {3}", logLabel,
                                bsi.InstalledComponent.Component.Uid, bsi.InstalledComponent.Position, bsi.SGR.Status));
                        }

                        #endregion Determine Bump Test Status

                        // SGF  28-Feb-2013  INS-3934 -- Check for flow failed moved forward from here

                        #region Set Sensor Bump Fault
                        // refactored this region - mostly 
                        foreach (BumpTestSensorInfo bsi in currentPassList)
                        {
                            bool bumpStatus = (bsi.SGR.Status == Status.Passed);
                            // 3/11/08 JAM - adding code to set the Bump Fault flag on the sensor when a DS2 bump test fails.

                            _instrumentController.SetSensorBumpFault(bsi.InstalledComponent.Position, !bumpStatus);
                            Log.Debug(string.Format("{0}Sensor({1}) Bump Fault set to {2}", logLabel, bsi.InstalledComponent.Position, bumpStatus.ToString().ToUpper()));

                            //we need to update sensor bump test status because in scheduler we have logic
                            //to force calibration based on sensor BumpTestStatus
                            //Note: Here setting BumpTestStatus to true means bumptest has passed for the sensor
                            Sensor bumpTestedSensor = (Sensor)bsi.InstalledComponent.Component;
                            bumpTestedSensor.BumpTestStatus = bumpStatus; //Suresh 22-Feb-2012 INS-2705
                            // INETQA-4178 RHP v7.6 Update the return event BumpTestStatus as this is used the eventprocessor to update switch service instrument 
                            // this is required for the scheduler logic discussed above
                            Sensor sensor = (Sensor)_returnEvent.DockedInstrument.InstalledComponents.Find(ic => ic.Component.Uid == bumpTestedSensor.Uid).Component;
                            if (sensor != null)
                            {
                                sensor.BumpTestStatus = bumpTestedSensor.BumpTestStatus;
                                // INS- RHP v7.6 - For MX6v4.4 and above, Set the sensor's calibration gas concentration to 
                                // match the concentration of gas end point that contains the gas.
                                //if (_returnEvent.DockedInstrument.Type == DeviceType.MX6 && new Version(_returnEvent.DockedInstrument.SoftwareVersion) >= _MX6_v44
                                //    && sensor.BumpCriterionType != CriterionType.PPMLimit)
                                //    _instrumentController.SetCalibrationGasConcentration(bsi.InstalledComponent, sensor.CalibrationGasConcentration, false);
                            }
                        }

                        #endregion Set Sensor Bump Fault

                        // SGF  28-Feb-2013  INS-3934 -- Closing the gas end point moved forward from here

                        #region Restore Settings

                        // If we used a Nitrogen-only cylinder to bump an O2 sensor, then make sure the gasresponse
                        // reflects that. It probably won't at this point because during the actual bump, even though
                        // we were using the Nitrogen cylinder, we had to let the gasresponse think it was  using 0%
                        // Oxygen so that gasresponse.FullSpanReserve calculation worked correctly.
                        //foreach (BumpTestSensorInfo bsi in currentPassList)
                        //{
                        //    if (gasEndPoint != null && gasEndPoint.Cylinder.ContainsOnlyGas(GasCode.N2))
                        //        bsi.SGR.GasConcentration = gasEndPoint.Cylinder.GasConcentrations[0];
                        //}

                        #endregion Restore Settings

                        // Reset information on the cylinders in preparation for the next bump test pass.
                        ResetTriedGasEndPoints();

                        // SGF  30-Jul-2012  DEV JIRA INS-4803 -- check for undocked instrument
                        // If instrument is not docked, throw InstrumentNotDockedException
                        if (!Master.Instance.ControllerWrapper.IsDocked())
                            throw new InstrumentNotDockedException();

                        Log.TimingEnd("BUMP - PASS " + currentPass, passStopwatch);
                    }

                    if (flowFailed)
                    {
                        // prepare to try the whole bump test again, but with a different cylinder to take the place of the cylinder that emptied
                        _instrumentController.EndInstrumentBump();
                        Log.Debug(string.Format("{0}END INSTRUMENT BUMP", logLabel));
                        bumpTestSensorInfoList = null;
                        _passesEndPointList.Clear();
                    }

                    #endregion --------------- BUMP TEST TASKS -----------------


                    #region [ Write to details builder ]
                    // TODO USED ONLY BY IDS
                    try
                    {
                        // Log information found in the bump sensor info list to Details builder.
                        foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                        {
                            //_detailsBuilder.AddNewLine();
                            //_detailsBuilder.Add("    ", "DETAILS_BUMP_SENSOR", string.Empty);
                            //_detailsBuilder.Add("        ", "DETAILS_BUMP_SENSOR_POSITION", bsi.InstalledComponent.Position);
                            //_detailsBuilder.Add("        ", "DETAILS_BUMP_SENSOR_CALIBRATION_GAS", _detailsBuilder.GetText(bsi.SGR.GasConcentration.Type.Code));
                            //_detailsBuilder.Add("        ", "DETAILS_BUMP_GAS_ENDPOINT_CONCENTRATION", bsi.SGR.GasConcentration.Concentration);
                            //_detailsBuilder.Add("        ", "DETAILS_BUMP_STATUS", bsi.SGR.Status);
                        }
                    }
                    catch (Exception detailsException)
                    {
                        Log.Error("Cannot write details builder: ", detailsException);
                    }
                    #endregion
                }
                while (flowFailed == true && !Master.Instance.PumpWrapper.IsBadPumpTubing());
            }

            catch (CorrectBumpTestGasUnavailable cbtgu)
            {
                thrownException = cbtgu;
                if (flowFailed && emptyGasEndPoint != null)
                    throw new FlowFailedException(emptyGasEndPoint);
                else
                    throw;
            }
            catch (FlowFailedException ffe)
            {
                thrownException = ffe;
                Log.Error("Flow failed", ffe);
                throw;
            }
            catch (ISC.Instrument.Driver.CommunicationAbortedException cae)
            {
                thrownException = cae;
                throw new InstrumentNotDockedException(cae);
            }
            catch (ISC.Instrument.Driver.SystemAlarmException sae) // some instruments may throw this during sensor warmup.
            {
                thrownException = sae;
                throw new InstrumentSystemAlarmException(Master.Instance.SwitchService.Instrument.SerialNumber, sae.ErrorCode);
            }
            catch (InstrumentNotDockedException inde)  // deal with instrument being undocked during this operation
            {
                thrownException = inde;
                throw;
            }
            // INS-7657 RHP v7.5.2 Display Instrument Not Ready Message to be specific that the error is due to Sesnor not biased within 2 hours
            catch (InstrumentNotReadyException inr)
            {
                thrownException = inr;
                throw new InstrumentNotReadyException(inr);
            }
            catch (Exception e)
            {
                thrownException = e;
                // For unusual conditions.
                throw new FailedBumpTestException(e);
            }
            finally
            {
                if (thrownException != null)
                {
                    Master.Instance.SwitchService.BadPumpTubingDetectedDuringBump = Master.Instance.PumpWrapper.IsBadPumpTubing();
                    _instrumentController.CloseGasEndPoint(gasEndPoint);
                }

                // TODO - IDS MOVED POST BUMP PURGE AFTER ENDINSTRUMETNBUMP FOR MX6 4.4 CHNAGES
                #region Additional Purge to Clear Alarms

                try
                {
                    // SGF  06-Jun-2011  INS-1735
                    // Additional purge at the end of the bump if sensors are still in alarm.
                    Stopwatch bumpClearStopwatch = Log.TimingBegin("BUMP - PURGE(CLEAR ALARM)");
                    new InstrumentPurgeOperation(PurgeType.PostBump, _instrumentController, GasEndPoints, _returnEvent).Execute();
                    Log.TimingEnd("BUMP - PURGE(CLEAR ALARM)", bumpClearStopwatch);
                }
                catch (Exception e)
                {
                    // TODO - what if FlowFailed exception is thrown because zero air cylinder goes empty?  - JMP, 6/29/2011
                    Log.Error(string.Format("{0}, {1}", Name, PurgeType.PostBump), e);
                }

                #endregion Additional Purge to Clear Alarms

                #region End Bump Test

                // isBumpStarted is set when BeginInstrumentBump is called.
                if (isBumpTestStarted && Master.Instance.ControllerWrapper.IsDocked())
                {
                    try
                    {
                        _instrumentController.EndInstrumentBump(); // will turn the sensors back off.
                        Log.Debug(string.Format("{0}END INSTRUMENT BUMP", logLabel));
                    }
                    catch (Exception e)
                    {
                        Log.Error("EndInstrumentBump", e);
                    }
                }

                #endregion End Bump Test

                // Clear the reference to a step in the bump testing process
                Master.Instance.ConsoleService.UpdateState(ConsoleState.BumpingInstrument);
            }
        } // end-BumpTestInstrumentParallel

        private TimeSpan PreconditionSensor(List<BumpTestSensorInfo> bumpTestSensorInfoList, int passIndex)
        {
            DateTime startTime = DateTime.UtcNow;

            try
            {
                #region Determine Preconditioning Needs

                // Determine which sensors in this bump test pass require preconditioning, and which ones can be skipped.
                bool preconditioningNeeded = false;
                foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                {
                    if (bsi.PassIndex == passIndex)
                    {
                        if (_instrumentController.IsSensorBumpPreconditionEnabled(bsi.InstalledComponent) == true)
                        {
                            // This sensor requires precondioning.
                            bsi.OpStatus = SensorBumpOpStatus.Preconditioning;

                            // save the current sensor gas response status, for restoration at the end of this preconditioning pass
                            bsi.OriginalStatus = bsi.SGR.Status;

                            preconditioningNeeded = true;
                        }
                        else
                        {
                            // This sensor can be skipped.
                            bsi.OpStatus = SensorBumpOpStatus.PreconditionSkipped;
                        }
                    }
                }

                // If all sensors in this pass can be skipped, then return with a precondition time span of 0.
                if (preconditioningNeeded == false)
                {
                    return new TimeSpan(0, 0, 0);
                }

                #endregion Determine Preconditioning Needs

                #region Determine Flow Rate

                // Determine the required flow rate (this will be the maximum flow rate of the sensors to precondition).
                // Log which sensors are about to be preconditioned during this bump test pass.
                int preconditionFlowRate = 0;
                foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                {
                    if (bsi.OpStatus == SensorBumpOpStatus.Preconditioning)
                    {
                        InstalledComponent ic = bsi.InstalledComponent;
                        Sensor sensor = (Sensor)bsi.InstalledComponent.Component;
                        double maximumReading = bsi.MaximumReading;

                        Log.Debug("PRECOND: PRECONDITIONING Sensor " + ic.Position + ", UID=" + sensor.Uid + ", " + ic.Component.Type.Code);
                        Log.Debug("PRECOND: Sensor MaximumReading: " + maximumReading + ", Resolution: " + sensor.Resolution);
                        Log.Debug("PRECOND: Gas Conc: " + bsi.SGR.GasConcentration.Concentration);

                        int sensorPreconditionFlowRate = bsi.PreconditionFlowRate;
                        if (sensorPreconditionFlowRate > preconditionFlowRate)
                            preconditionFlowRate = sensorPreconditionFlowRate;
                    }
                }

                #endregion Determine Flow Rate

                #region Determine Time Out

                // Determine the longest timeout value for the sensors that will be preconditioned on this pass.
                // Use this timeout value for all sensors.  SGF NOTE: We are assuming that it will be suitable 
                // to allow potentially more time for preconditioning for sensors which have shorter timeout 
                // times, since we need to allow for potentially several sensors and several timeout values.
                // If this assumption is incorrect, then we will need to adjust the following loop to test 
                // each sensor against its own timeout value.  The code will be more complex, so we need to 
                // prove that it is necessary to do that.
                TimeSpan preconditionTimeOut = TimeSpan.Zero;
                foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                {
                    if (bsi.OpStatus == SensorBumpOpStatus.Preconditioning)
                    {
                        TimeSpan sensorPreconditionTimeOut = bsi.PreconditionTimeOut;
                        if (sensorPreconditionTimeOut > preconditionTimeOut)
                            preconditionTimeOut = sensorPreconditionTimeOut;
                    }
                }

                #endregion Determine Time Out

                // Get the appropriate gas end point
                GasEndPoint gasEndPoint = GetPassGasEndPoint(passIndex);

                _instrumentController.OpenGasEndPoint(gasEndPoint, preconditionFlowRate);

                // Initialize the time "now"
                DateTime now = DateTime.UtcNow;

                startTime = now;

                // Ignore odd readings for the first half of the precondition time.  Set the 
                // "odd time" limit to be half way between the start time and the end time.
                DateTime oddTime = now.AddTicks(preconditionTimeOut.Ticks / 2L);
                DateTime endTime = now + preconditionTimeOut;

                #region Unpause Sensors

                // Unpause each of the sensors that will be preconditioned.
                foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                {
                    if (bsi.OpStatus == SensorBumpOpStatus.Preconditioning)
                    {
                        _instrumentController.PauseSensor(bsi.InstalledComponent.Position, false);
                        Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ") Start Time: " + startTime + " End Time: " + endTime);
                    }
                }

                #endregion Unpause Sensors

                // Keep iterating through this loop while the pump is running...
                while (Master.Instance.PumpWrapper.GetOpenValvePosition() > 0)
                {
                    // Record the current time.
                    now = DateTime.UtcNow;

                    // Take readings from the sensors until the precondition timeout has been reached.
                    if (now >= endTime)
                        break;

                    // Wait a bit before each reading during preconditioning
                    Thread.Sleep(1000);

                    // If the instrument has been undocked, exit the loop to stop the precondition operation.
                    if (!Master.Instance.ControllerWrapper.IsDocked())
                        break;

                    foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                    {
                        if (bsi.OpStatus != SensorBumpOpStatus.Preconditioning)
                            continue;

                        #region Get Sensor Reading

                        double rawReading = _instrumentController.GetSensorReading(bsi.InstalledComponent.Position, ((Sensor)bsi.InstalledComponent.Component).Resolution);

                        bsi.SGR.Reading = rawReading;
                        bsi.PreconditionTotalReadings++;

                        #endregion Get Sensor Reading

                        #region Classify Sensor Reading

                        // Determine if the current reading is abnormal (that is, that it exceeds the maximum allowed reading for the sensor type).
                        if (Math.Abs(rawReading) > bsi.MaximumReading)
                        {
                            if (now > oddTime)
                            {
                                // Record any abnormal readings for the second half of the preconditioning.
                                bsi.PreconditionOddReadings++;
                                Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ") Odd reading seen: " + rawReading);
                            }
                            else
                            {
                                // Ignore any abnormal readings for the first half of the preconditioning.
                                Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ") Odd reading ignored: " + rawReading);
                            }
                        }
                        // Determine if the current reading is between 50% and 100% of the full span reserve.  If so, record it as a good reading.
                        else if (IsPreconditionCriterionMet(bsi))
                            bsi.PreconditionGoodReadings++;

                        Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ")  (" + bsi.PreconditionGoodReadings + "/" + bsi.PreconditionTotalReadings + ") span: " + bsi.SGR.FullSpanReserve + " raw: " + bsi.SGR.Reading);

                        #endregion Classify Sensor Reading

                        #region Determine If Passed

                        // Must pass a minimum number of 2 readings to pass precondition
                        if (bsi.PreconditionGoodReadings >= 2)
                        {
                            bsi.SGR.Status = Status.Passed;
                            bsi.OpStatus = SensorBumpOpStatus.PreconditionPassed;
                            Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ")  PASSED");
                        }

                        #endregion Determine If Passed

                        #region Determine If Odd

                        // 3 odd readings may happen before exiting preconditiong.
                        if (bsi.PreconditionOddReadings >= 3)
                        {
                            bsi.SGR.Status = Status.Failed;
                            bsi.OpStatus = SensorBumpOpStatus.PreconditionFailed;
                            Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ")  FAILED -- TOO MANY ODD READINGS.");
                        }

                        #endregion Determine If Odd
                    }

                    #region Determine If Done

                    // As long as at least one sensor is preconditioning, continue in this loop.  If all sensors in this pass are done, then exit.
                    // NOTE:  This approach has been taken rather than utilizing a counter, in order to minimize complexity.
                    bool stillPreconditioning = false;
                    foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                    {
                        if (bsi.OpStatus == SensorBumpOpStatus.Preconditioning)
                            stillPreconditioning = true;
                    }
                    if (stillPreconditioning == false)
                        break;

                    #endregion Determine If Done
                }

                if (Master.Instance.PumpWrapper.GetOpenValvePosition() > 0)
                {
                    #region Mark Time Out Errors

                    // Since the pump is still pumping gas, any sensors still marked as Precondition at this point have failed because the precondition timed out.
                    foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                    {
                        if (bsi.OpStatus == SensorBumpOpStatus.Preconditioning)
                        {
                            bsi.SGR.Status = Status.Failed;
                            bsi.OpStatus = SensorBumpOpStatus.PreconditionFailed;
                            Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ")  FAILED -- TIMED OUT.");
                        }
                    }

                    #endregion Mark Time Out Errors
                }
                else
                {
                    #region Mark Pump Closed Errors

                    // The pump has closed. Any sensors still marked as Precondition at this point will be marked as having skipped.
                    foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                    {
                        if (bsi.OpStatus == SensorBumpOpStatus.Preconditioning)
                        {
                            bsi.SGR.Status = Status.Failed;
                            bsi.OpStatus = SensorBumpOpStatus.PreconditionFailed;
                            Log.Debug("PRECOND: (" + bsi.InstalledComponent.Position + ")  FAILED -- PUMP CLOSED.");
                        }
                    }

                    throw new FlowFailedException(gasEndPoint);

                    #endregion Mark Pump Closed Errors
                }

                Log.Debug("Open valve position: " + Pump.GetOpenValvePosition());
                Log.Debug("Now: " + DateTime.UtcNow + " End Time: " + endTime);
            }
            catch (FlowFailedException ffe)
            {
                Log.Error("PreconditionSensor", ffe);
                throw ffe;
            }
            catch (Exception e)
            {
                Log.Error("PreconditionSensor", e);
            }
            finally
            {
                //Ignore if bad pump tubing is detected during preconditioning.
                //Set Pump.IsBadPumpTubing to false.
                Pump.IsBadPumpTubing = false;

                #region Restore Status

                // Put sensor gas response's status back to what it was when this method was called.
                foreach (BumpTestSensorInfo bsi in bumpTestSensorInfoList)
                {
                    if (bsi.OpStatus == SensorBumpOpStatus.PreconditionPassed || bsi.OpStatus == SensorBumpOpStatus.PreconditionFailed)
                        bsi.SGR.Status = bsi.OriginalStatus;
                }

                #endregion Restore Status
            }
            return DateTime.UtcNow - startTime;

        } // end-PreconditionSensor

    } // end-class

} // end-namespace 
