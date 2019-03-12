using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    using ISC.iNet.DS.DomainModel;

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to perform a bump test on an instrument.
	/// </summary>
	public partial class InstrumentBumpTestOperation : InstrumentBumpTestAction , IOperation
	{
        #region Fields

        /// <summary>
        /// The amount of time to wait between readings.
        /// </summary>
        protected readonly TimeSpan _sleepTimeSpan = new TimeSpan(0, 0, 2);
        protected readonly TimeSpan _min5SecondBump = new TimeSpan(0, 0, 5);
        protected const int MIN_BUMP_READINGS = 1;
        protected const double OXYGEN_FRESH_AIR_TEST_HIGH_PASS_PCT = 22.0; // INS-7625 SSAM v7.6
        protected readonly Version _MX6_v44 = new Version("4.40");

        protected InstrumentController _instrumentController;

        protected InstrumentBumpTestEvent _returnEvent;

        private Dictionary<GasEndPoint, GasEndPoint> _triedGasEndPoints;  // dictionary is used as "Set" collection.

        private int _cumulativeBumpTestResponseTime;  // SGF  14-Jun-2011  INS-1732

        /// <summary>
        /// Specifies what gas reading (% of concentration) that instrument needs to
        /// see in order for a bump test to pass.
        /// </summary>
        protected int BumpThreshold { get; private set; }

        protected int BumpTimeout { get; private set; }

        protected int _biasStateTimeout = 7200; // DSW-1675 RHP v9.6.1 2 hours is the timeout for checking teh bias state before timeout

        // DSX-L Fields 

        public const int OXYGEN_FRESH_AIR_TEST_TIME_OUT = 180; // GANA DSW-738 24-AUG-2011, Changed 60 to 180
        /// <summary>
        /// Set the Event Details
        /// </summary>
        public String EventDetails { get; set; }    // TODO SET ON SWITCH SERVICE - USED ONLY BY IDS

        //protected DetailsBuilder _detailsBuilder;

        #endregion Fields

        #region Constructors

        private void Init()
        {
            // Create an InstrumentController object to provide command processing functionality.
            _instrumentController = Master.Instance.SwitchService.InstrumentController;

            // For keeping track of cylinders that have been tried.
            _triedGasEndPoints = new Dictionary<GasEndPoint, GasEndPoint>();

            BumpThreshold = SensorGasResponse.DEFAULT_BUMP_THRESHOLD;

            /// <summary>
            /// Specifies maximum amount of time a bump test may take
            /// before it times out and fails.
            /// </summary>
            BumpTimeout = SensorGasResponse.DEFAULT_BUMP_TIMEOUT;

#if TEST
            _biasStateTimeout = 5;
#endif
        }

        /// <summary>
        /// Creates a new instance of an PortableInstrumentBumpTestOperation class.
        /// </summary>
        public InstrumentBumpTestOperation()
        {
            Init();
        }

        public InstrumentBumpTestOperation(InstrumentBumpTestAction instrumentBumpTestAction)
            : base(instrumentBumpTestAction)
        {
            Init();
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Executes an instrument bump test operation.
        /// </summary>
        /// <returns>The completed event for this bump test.</returns>
        /// <exception cref="FailedBumpTestException">
        /// If anything extraordinary happened during the bump test.
        /// </exception>
        public DockingStationEvent Execute()
        {
            //Clear if any flags before initiating calibration once again 
            Pump.IsBadPumpTubing = false;               // iNetDS
            Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal = false;    // iNetDS
            Master.Instance.SwitchService.BadPumpTubingDetectedDuringBump = false;   // iNetDS

            Stopwatch operationStopwatch = Log.TimingBegin("INSTRUMENT BUMP TEST");      // iNetDS

            _returnEvent = new InstrumentBumpTestEvent(this);
            _returnEvent.DockedInstrument = this.Instrument;       // TODO
            _returnEvent.DockingStation = this.DockingStation;     // TODO
            //_detailsBuilder = new DetailsBuilder(EventDetails);    // _detailsBuilder = new DetailsBuilder(InstrumentHelper.EventDetails);    // IDS

            //Throw Exception if Instrument is undocked
            if (!Master.Instance.ControllerWrapper.IsDocked())
            {
                throw new InstrumentNotDockedException();
            }

            _returnEvent.Trigger = Trigger;
            _returnEvent.IsSensorFailureModeEnabled = IsSensorFailureModeEnabled;   // DSW-1034 RHP Tango     TODO    IDS
            _returnEvent.IsSCSensorFailureModeEnabled = IsSCSensorFailureModeEnabled;   // DSW-1068 RHP v9.5      TODO    IDS

            Log.Debug(string.Format("{0}.Execute {1}", Name, _returnEvent.DockedInstrument.SerialNumber));

            // Add the detail header.
            //_detailsBuilder.Add("", "DETAILS_BUMP_TEST_HEADER", string.Empty);  // IDS
            //_detailsBuilder.AddNewLine();                                       // IDS
            //_detailsBuilder.Add("    ", "DETAILS_INSTRUMENT_BUMPTHRESHOLD", this.BumpThreshold);    // IDS
            //_detailsBuilder.Add("    ", "DETAILS_INSTRUMENT_BUMPTIMEOUT", this.BumpTimeout);        // IDS

            _returnEvent.DockedInstrument.InstalledComponents
                = InstrumentController.SortSensorsByBumpOrder(_returnEvent.DockedInstrument.InstalledComponents);       // 

            #region LogDebug
            Log.Debug("Candidate gases...");
            int pointCount = 0;
            foreach (GasEndPoint gasEndPoint in GasEndPoints)
            {
                string msg = "GasEndPoint #" + ++pointCount;
                Log.Debug(msg);
                Cylinder cyl = gasEndPoint.Cylinder;
                msg = "...Pos " + gasEndPoint.Position
                    + ", FactID=" + cyl.FactoryId
                    + ", Part=" + cyl.PartNumber
                    + ", Fresh=" + cyl.IsFreshAir
                    + ", ZeroAir=" + cyl.IsZeroAir
                    + ", Pressure=" + cyl.Pressure.ToString();
                if (gasEndPoint.Cylinder.Volume != DomainModelConstant.NullInt)
                    msg += ", Vol=" + gasEndPoint.Cylinder.Volume;

                Log.Debug(msg);
                msg = "......";
                foreach (GasConcentration gasCon in cyl.GasConcentrations)
                {
                    msg += "[" + gasCon.Type.Code + " ";
                    msg += (gasCon.Concentration == DomainModelConstant.NullDouble) ? "fresh" : gasCon.Concentration.ToString();
                    msg += "]";
                }
                Log.Debug(msg);
            }

            Log.Debug("Going to BUMP sensors in following ORDER...");
            int bumpOrder = 1;
            foreach (InstalledComponent ic in _returnEvent.DockedInstrument.InstalledComponents)
            {
                if (!(ic.Component is Sensor))
                    continue; // It its not a sensor, ignore it.
                Log.Debug("...#" + bumpOrder++ + ", Position " + ic.Position + ", UID=" + ic.Component.Uid + ", " + ic.Component.Type.Code);
            }
            #endregion


            // Open the serial port connection needed to communicate with the instrument.
            try
            {
                Stopwatch pingStopwatch = Log.TimingBegin("BUMP - PING");
                _instrumentController.Initialize();

                // Add a new line and section to the details.
                //_detailsBuilder.AddNewLine();

                Log.TimingEnd("BUMP - PING", pingStopwatch);

                // See if there are any already failed sensors - go to calibration if there are.
                // TODO move this section for IDS prior to initiating Bump Test, probably something similar to iNetDS Scheduler 

                // Load the BumpTimout and BumpThreshold from the database needed to perform the bump.
                LoadBumpLimits();   // TODO update for IDS, LOAD THESE INTO INSTRUMETN ACTION BEFORE BUMP TEST 

                BumpInstrument();

                // Need to determine the next time this instrument will be bumped.  
                // Put this date into the event so it can be uploaded to iNet, and also 
                // update the global next date that's held in the switch service.
                _returnEvent.NextUtcScheduledDate = Master.Instance.SwitchService.NextUtcBumpDate
                    = Master.Instance.Scheduler.GetNextGasOperationDate(_returnEvent);
            }
            finally
            {
                _instrumentController.Dispose();
            }

            Log.TimingEnd("INSTRUMENT BUMP TEST", operationStopwatch);

            // IDS Updates Sensor's Bump Test Status on Cached Instrument - TODO TO BE HANDLED IN EVENT PROCESSOR FOR IDS.

            // Send back the results.
            return _returnEvent;
        }

        /// <summary>
        /// Executes an instrument bump test operation.
        /// </summary>
        /// <returns>The completed event for this bump test.</returns>
        /// <exception cref="FailedBumpTestException">
        /// If anything extraordinary happened during the bump test.
        /// </exception>
        protected void BumpInstrument()
        {
            // SGF  14-Jun-2011  INS-1732
            _cumulativeBumpTestResponseTime = 0;

            try
            {
                List<InstalledComponent> workingSensorList = GetWorkingSensorList();

                // SGF  24-Aug-2011  INS-2314
                // Create sensor gas response objects for each sensor in the instrument
                foreach (InstalledComponent installedComponent in workingSensorList)
                {
                    if (!(installedComponent.Component is Sensor))  // Skip non-sensors.
                        continue;

                    if (!installedComponent.Component.Enabled) // Skip disabled sensors.
                        continue;

                    Sensor sensor = (Sensor)installedComponent.Component;
                    SensorGasResponse sgr = new SensorGasResponse(sensor.Uid, DateTime.UtcNow);
                    sgr.GasConcentration = new GasConcentration(sensor.CalibrationGas, sensor.CalibrationGasConcentration);
                    sgr.GasDetected = sensor.GasDetected;
                    sgr.Type = GasResponseType.Bump;

                    if (!sensor.IsBumpEnabled(GasEndPoints)) // For sensors that are not bump-enabled set the status as Skipped
                        sgr.Status = Status.Skipped; //Suresh 19-APR-2012 INS-4537 (DEV)

                    //  SGF (Suresh)  3-Oct-2012  INS-2709
                    // If this is a sensor-type specific bump test, and this sensor should be skipped during the bump 
                    // test operation, then set the Status on the Sensor Gas Response for this sensor to Skipped.
                    if (ComponentCodes.Count != 0 && !ComponentCodes.Contains(sensor.Type.Code))
                        sgr.Status = Status.Skipped;

                    _returnEvent.GasResponses.Add(sgr);
                }

                BumpTestInstrumentParallel(); // Also known as "quick bump".
            }
            finally
            {
                if (Master.Instance.SwitchService.BadPumpTubingDetectedDuringBump)
                    Master.Instance.ConsoleService.UpdateState(ConsoleState.BumpStoppedCheckTubing);
                else
                    Master.Instance.ConsoleService.UpdateState(ConsoleState.BumpingInstrument);  // Clear the reference to a step in the bump testing process
            }

        } // end-BumpInstrument


        /// <summary>
        /// Returns a list of sensors that are "working"; i.e., are not in a cal-fault
        /// or zero-fault state. These working sensors are the ones that will be bump tested.
        /// </summary>
        /// <returns></returns>
        protected List<InstalledComponent> GetWorkingSensorList()
        {
            List<InstalledComponent> workingSensorList = _returnEvent.DockedInstrument.InstalledComponents;

            if (Configuration.IsSingleSensorMode())
            {
                List<InstalledComponent> passedSensors = new List<InstalledComponent>();
                CalibrationState calState = Master.Instance.SwitchService.Instrument.GetInstrumentCalibrationState(Configuration.IsSingleSensorMode(), passedSensors, null);
                if (calState == CalibrationState.RedundantSensorPassed)
                    workingSensorList = passedSensors;
            }

            return workingSensorList;
        }

        /// <summary>
        /// Load the BumpTimout and BumpThreshold from the database needed to perform the bump.
        /// </summary>
        private void LoadBumpLimits()
        {
            // Don't need to bother querying the database unless we know we're activated.
            // Otherwise, we're in cal station mode and just using defaults.
            if (!Configuration.Schema.Activated)
            {
                Log.Warning("CalStation/Service Mode: Using default DomainModel BumpThreshold of " + this.BumpThreshold);
                Log.Warning("CalStation/Service Mode: Using default DomainModel BumpTimeout of " + this.BumpTimeout);
                return;
            }

            // Get iNet's bump threshold/timeout for this instrument from the database. // Can we Load the Bump limits from the Instrument which we pass through Action - TODO
            //Instrument settings = new InstrumentDataAccess().FindApplicableSettings(_returnEvent.DockedInstrument.SerialNumber, _returnEvent.DockedInstrument.Type);

            // If we're activated, then the inability to load settings is considered an error.
            // There should always be settings; even at least defaults.
            //if (settings == null)
            //{
            //    string msg = string.Format("No settings found for instrument \"{0}\".", _returnEvent.DockedInstrument.SerialNumber);
            //    Log.Warning(msg);
            //    throw new ApplicationException(msg);
            //}

            this.BumpThreshold = _returnEvent.DockedInstrument.BumpThreshold; //settings.BumpThreshold;
            Log.Debug("Using iNet BumpThreshold of " + this.BumpThreshold);
            this.BumpTimeout = _returnEvent.DockedInstrument.BumpTimeout; //settings.BumpTimeout;
            Log.Debug("Using iNet BumpTimeout of " + this.BumpTimeout);
        }

        protected void ResetTriedGasEndPoints()
        {
            foreach (GasEndPoint gasEndPoint in GasEndPoints)
            {
                if (_triedGasEndPoints.ContainsKey(gasEndPoint))
                {
                    if (gasEndPoint.Cylinder.Pressure != PressureLevel.Empty)
                        _triedGasEndPoints.Remove(gasEndPoint);
                }
            }
        }

        /// <summary>
        /// Get the calibration gas concentration.
        /// </summary>
        /// <param name="sensor">The sensor to get the concentration for.</param>
        /// <param name="endPoint">The gas end point that contains the gas.</param>
        protected double GetCalibrationGasConcentration(InstalledComponent installedComponent, GasEndPoint endPoint)
        {
            const string func = "GetCalibrationGasConcentration: ";

            Sensor sensor = (Sensor)installedComponent.Component;

            double availableConcentration = DomainModelConstant.NullDouble;

            string gasCode = sensor.CalibrationGas.Code;
            double lelMultiplier = GasType.Cache[gasCode].LELMultiplier;
            MeasurementType sensorMeasurementType = ((SensorType)sensor.Type).MeasurementType;

            Cylinder cylinder = endPoint.Cylinder; // Get the cylinder.

            // For nitrogen cylinder's, being used for O2 bumps, we assume 0% O2.
            if ((gasCode == GasCode.O2) && cylinder.ContainsOnlyGas(GasCode.N2))
            {
                availableConcentration = 0.0d;
            }
            else
            {
                // Determine the gas concentration of the gas to use.
                foreach (GasConcentration gasCon in cylinder.GasConcentrations)
                {
                    if (gasCon.Type.Code == gasCode)
                    {
                        availableConcentration = gasCon.Concentration;
                        break;
                    }
                    else if ((gasCode == GasCode.O2) && (gasCon.Type.Code == GasCode.FreshAir))
                    {
                        availableConcentration = 209000d;
                        break;
                    }
                }
            }

            // If we didn't find anything with the gas.
            if (availableConcentration == DomainModelConstant.NullDouble)
                throw new CorrectBumpTestGasUnavailable(gasCode);

            Log.Debug("Sensor cal gas concentration: "
                + sensor.CalibrationGasConcentration + " res: " + sensor.Resolution);
            // Check the measurement type for how to multiply the concentration.
            if (sensorMeasurementType == MeasurementType.LEL)
            {
                availableConcentration *= lelMultiplier;
                availableConcentration = Master.Instance.ControllerWrapper.Round(availableConcentration, 0);
            }
            else if (sensorMeasurementType != MeasurementType.PPM)
            {
                availableConcentration /= 10000;
            }

            if (availableConcentration == sensor.CalibrationGasConcentration)
                return sensor.CalibrationGasConcentration; // Its the correct concentration.

            availableConcentration = Master.Instance.ControllerWrapper.Round(availableConcentration, 2);

            Log.Debug("gas: " + gasCode + " new conc: " + availableConcentration);

            // INS- RHP v7.6 - For MX6v4.4 and above, Set the sensor's calibration gas concentration to 
            // match the concentration of gas end point that contains the gas. 
            //if (_returnEvent.DockedInstrument.Type == DeviceType.MX6 && new Version(_returnEvent.DockedInstrument.SoftwareVersion) >= _MX6_v44
            //    && availableConcentration > 0.0d && sensor.BumpCriterionType != CriterionType.PPMLimit)
            //{
            //    // If sensor is %vol, and it has a zero resolution, then we want to round the concentration
            //    // up to the next integer value.  e.g., if cylinder contains 2.1% gas, then we want to round 
            //    // it to 3.
            //    if (sensorMeasurementType == MeasurementType.VOL && sensor.Resolution == 1.0)
            //    {
            //        Log.Debug(string.Format("{0}Sensor is %VOL and has resolution of zero decimals. Rounding {1} up to next integer",
            //            func, availableConcentration));
            //        availableConcentration = Math.Ceiling(availableConcentration);
            //    }

            //    Log.Debug(string.Format("{0}SETTING SENSOR FROM CONCENTRATION {1} TO {2}, (res={3})", func, sensor.CalibrationGasConcentration, availableConcentration, sensor.Resolution));

            //    // Set the sensor's calibration gas concentration.
            //    _instrumentController.SetSensorCalGasConcentration(installedComponent.Position, availableConcentration, sensor.Resolution);

            //    Log.Debug(string.Format("{0}NEW CONCENTRATION: {1}", func, _instrumentController.GetSensorCalGasConcentration(installedComponent.Position, sensor.Resolution)));
            //}

            return availableConcentration;
        }

        /// <summary>
        /// Find the fresh air end point for this sensor.
        /// </summary>
        /// <param name="installedComponent">The sensor to find the fresh air for.</param>
        /// <param name="checkIfUsed">If true, check if the cylinder under consideration
        /// has already been used.</param>
        /// <returns>The correct fresh air gas end point.</returns>
        /// <exception cref="CorrectBumpTestGasUnavailable">
        /// If there are no fresh air gas end points for this sensor.
        /// </exception>
        protected GasEndPoint GetSensorFreshAir(InstalledComponent installedComponent, bool checkIfUsed)
        {
            Sensor sensor = null;

            if (installedComponent != null)
                sensor = installedComponent.Component as Sensor;

            string msg = "Finding Fresh air";
            if (sensor != null)
                msg += " for sensor " + sensor.Uid;
            msg += "...";
            Log.Debug(msg);

            // Find fresh air and zero gas end points.
            int pointCount = 0;
            foreach (GasEndPoint gasEndPoint in GasEndPoints)
            {
                #region LogDebug
                msg = "GasEndPoint #" + ++pointCount;
                Log.Debug(msg);

                Cylinder cyl = gasEndPoint.Cylinder;

                msg = "...Pos=" + gasEndPoint.Position
                    //+ ", ID=" + cyl.ID
                    + ", FactID=" + cyl.FactoryId
                    + ", Part=" + cyl.PartNumber
                    + ", Fresh=" + cyl.IsFreshAir
                    + ", ZeroAir=" + cyl.IsZeroAir
                    + ", Pressure=" + cyl.Pressure.ToString();
                if (cyl.Volume != DomainModelConstant.NullInt) msg += ", Vol=" + cyl.Volume;

                Log.Debug(msg);
                #endregion

                // Ignore non-fresh air cylinders
                if (!cyl.IsFreshAir)
                {
                    Log.Debug("...Rejected.  Not fresh air.");
                    continue;
                }

                if (cyl.Pressure == PressureLevel.Empty)
                {
                    Log.Debug("...Rejected fresh air. Cylinder empty.");
                    continue;
                }

                if (checkIfUsed == true && _triedGasEndPoints.ContainsKey(gasEndPoint))
                {
                    Log.Debug("...Rejected. Already tried cylinder.");
                    continue;
                }

                if (sensor == null)
                {
                    Log.Debug("...SELECTED GasEndPoint. Fresh air found.");
                    return gasEndPoint;
                }

                Log.Debug("...SELECTED GasEndPoint.  Fresh air found for sensor " + sensor.Uid);
                return gasEndPoint;
            }

            Log.Debug("No fresh air found.");

            throw new CorrectBumpTestGasUnavailable(GasCode.FreshAir); // No calibration gases were found.
        }

        /// <summary>
        /// Find the zero air end point for this sensor.  If zero air is not
        /// found, then fresh air may be returned instead (See notes on zeroAirOnly
        /// parameter).
        /// </summary>
        /// <param name="installedComponent">The sensor to find the zero air for.</param>
        /// <param name="zeroAirOnly">If true, then this routine will only find
        /// and return zero air cylinders.  If false, then this routine will
        /// attempt to find a zero air cylinder, but will find and return a
        /// fresh air as an alternative if zero air is not found.</param>
        /// <param name="checkIfUsed">If true, check if the cylinder under consideration
        /// has already been used.</param>
        /// <returns>The correct zero air gas end point.</returns>
        /// <exception cref="CorrectBumpTestGasUnavailable">
        /// If there are no zero air gas end points for this sensor.
        /// </exception>
        protected internal GasEndPoint GetSensorZeroAir(InstalledComponent installedComponent, bool zeroAirOnly, bool checkIfUsed)
        {
            Sensor sensor = null;

            if (installedComponent != null)
                sensor = installedComponent.Component as Sensor;

            string msg = "Finding Zero air cylinder";
            if (sensor != null)
                msg += " for sensor " + sensor.Uid;
            msg += "...";
            Log.Debug(msg);

            // Find zero air end points.

            int pointCount = 0;

            foreach (GasEndPoint gasEndPoint in GasEndPoints)
            {
                #region LogDebug
                msg = "GasEndPoint #" + ++pointCount;
                Log.Debug(msg);

                Cylinder cyl = gasEndPoint.Cylinder;

                msg = "...Pos=" + gasEndPoint.Position
                    //+ ", ID: " + cyl.ID
                    + ", FactID=" + cyl.FactoryId
                    + ", Part=" + cyl.PartNumber
                    + ", Fresh=" + cyl.IsFreshAir
                    + ", ZeroAir=" + cyl.IsZeroAir
                    + ", Pressure=" + cyl.Pressure.ToString();
                if (cyl.Volume != DomainModelConstant.NullInt) msg += ", Vol=" + cyl.Volume;

                Log.Debug(msg);
                #endregion

                if (!cyl.IsZeroAir)
                {
                    Log.Debug("...Rejected.  Not zero air.");
                    continue;
                }

                if (cyl.Pressure == PressureLevel.Empty)
                {
                    Log.Debug("...Rejected zero air. Cylinder empty.");
                    continue;
                }

                if (checkIfUsed == true && _triedGasEndPoints.ContainsKey(gasEndPoint))
                {
                    Log.Debug("...Rejected. Already tried cylinder.");
                    continue;
                }

                if (sensor == null)
                {
                    Log.Debug("...SELECTED GasEndPoint.  Zero air found.");
                    return gasEndPoint;
                }

                Log.Debug("...SELECTED GasEndPoint.  Zero air found for sensor " + sensor.Uid);
                return gasEndPoint;
            }

            if (zeroAirOnly)
            {
                Log.Debug("No zero air found.");
                throw new CorrectBumpTestGasUnavailable("Zero Air");
            }

            // No calibration gases were found, attempt to use the fresh air.
            Log.Debug("No zero air found.  Looking for alternative fresh air...");

            return GetSensorFreshAir(installedComponent, checkIfUsed);
        }

        /// <summary>
        /// Get the bump test gas end point for a sensor.
        /// </summary>
        /// <param name="sensor">The sensor to get the gas for.</param>
        /// <returns>The correct gas end point.</returns>
        /// <exception cref="CorrectBumpTestGasUnavailable">
        /// Thrown when no cylinder is provided for the sensor.
        /// </exception>
        protected GasEndPoint GetSensorGasEndPoint(Sensor sensor)
        {
            #region LogDebug
            if (sensor != null)
            {
                Log.Debug("BumpTest.GetSensorGasEndPoint");
                Log.Debug("Finding appropriate Bump gas for Sensor: " + sensor.Type
                    + ", S/N: " + sensor.Uid
                    + ", CalGas Code: " + sensor.CalibrationGas.Code
                    + ", Conc: " + sensor.CalibrationGasConcentration
                    + ", Measurement: " + ((SensorType)sensor.Type).MeasurementType);
            }
            #endregion

            GasEndPoint endPoint = null;

            Log.Debug("UseExpiredCylinders=" + Configuration.DockingStation.UseExpiredCylinders);

            // If UseExpiredCylinders is true, then we should try and use expired cylinders
            // if there are any. i.e., expired cylinders are "preferred" over non-expired cylinders.
            if (Configuration.DockingStation.UseExpiredCylinders)
            {
                // Get sub-list of available end points that are only the expired cylinders.
                DateTime localTime = Configuration.GetLocalTime();
                List<GasEndPoint> gasEndPoints = GasEndPoints.FindAll(gep => gep.Cylinder.ExpirationDate <= localTime);
                Log.Debug(string.Format("Looking for an expired cylinder to use ({0} expired candidates)....", gasEndPoints.Count));
                // See if we can find an appropriate gas to use that's in this expired end points list.
                endPoint = GetSensorGasEndPoint(sensor, gasEndPoints);
                // If we didn't find an expired cylinder to use, we need to see if there's an un-expired cylinder to use.
                if (endPoint == null)
                {
                    gasEndPoints = GasEndPoints.FindAll(gep => gep.Cylinder.ExpirationDate > localTime);
                    Log.Debug(string.Format("No expired cylinder found.  Looking for an unexpired cylinder ({0} unexpired candidates)....", gasEndPoints.Count));
                    endPoint = GetSensorGasEndPoint(sensor, gasEndPoints);
                }
            }
            else
                endPoint = GetSensorGasEndPoint(sensor, GasEndPoints);

            if (endPoint == null)
                Log.Debug("NO APPROPRIATE BUMP GAS FOUND!");

            return endPoint;
        }

        /// <summary>
        /// Private helper method for GetSensorGasEndPoint(Sensor).
        /// </summary>
        /// <param name="sensor"></param>
        /// <param name="gasEndPoints"></param>
        /// <returns></returns>
        private GasEndPoint GetSensorGasEndPoint(Sensor sensor, List<GasEndPoint> gasEndPoints)
        {
            MeasurementType sensorMeasurementType = ((SensorType)sensor.Type).MeasurementType;

            string sensorGasCode = sensor.CalibrationGas.Code;

            double sensorConcentration = sensor.CalibrationGasConcentration;

            double lelMultiplier = GasType.Cache[sensorGasCode].LELMultiplier;

            int pointNumber = 0;

            Log.Debug("SCAN 1 (find appropriate gas with desired concentration)...");
            foreach (GasEndPoint gasEndPoint in gasEndPoints)
            {
                Cylinder cyl = gasEndPoint.Cylinder;

                LogGasEndPoint(gasEndPoint, ++pointNumber);

                // Ignore already tried cylinders.
                if (_triedGasEndPoints.ContainsKey(gasEndPoint))
                {
                    Log.Debug("...Rejected. Already tried cylinder.");
                    continue;
                }

                // Ignore empty cylinders.
                if (cyl.Pressure == PressureLevel.Empty)
                {
                    _triedGasEndPoints[gasEndPoint] = gasEndPoint;
                    Log.Debug("...Rejected. Cylinder empty.");
                    continue;
                }

                // Check the measurement type for how to multiply the concentration.

                // Make sure we convert sensor's LEL 
                // concentration to PPM for matching against cylinders concentrations
                if (sensorMeasurementType == MeasurementType.LEL)
                {
                    // Convert sensor's cal gas concentration from %LEL to PPM
                    sensorConcentration = sensor.CalibrationGasConcentration / lelMultiplier;
                    // We want to round the concentration to the nearest hundred.
                    // e.g., if concentration is 3928, we want to round it to 3900.
                    //
                    // DO NOT DO THE FOLLOWING COMMENTED OUT LINE!  IT NEEDS TO BE KEPT
                    // SEPARATED AS THREE SEPARATE LINES, OTHERWISE IT MIGHT CRASHES WINCE.
                    // SOME BUG IN COMPACT FRAMEWORK?
                    // sensorConcentration = Math.Round( sensorConcentration / 100.0, 0 ) * 100.0;
                    sensorConcentration /= 100.0;
                    sensorConcentration = Math.Round(sensorConcentration, 0);
                    sensorConcentration *= 100.0;
                    Log.Debug(string.Format("...Sensor concentration of {0}%% LEL equals {1} PPM ({2} multiplier).",
                        sensor.CalibrationGasConcentration, sensorConcentration, lelMultiplier));
                }

                else if (sensorMeasurementType == MeasurementType.VOL)
                {
                    sensorConcentration = sensor.CalibrationGasConcentration * 10000;
                    Log.Debug(string.Format("...Sensor concentration of {0}%% VOL equals {1} PPM.",
                        sensor.CalibrationGasConcentration, sensorConcentration));
                }

                // For oxygen sensors, ignore 20.9 concentration end points.
                if ((sensorGasCode == GasCode.O2) && cyl.IsZeroAir)
                {
                    Log.Debug("...Rejected. Can't use ZeroAir for O2.");
                    continue;
                }

                // For oxygen sensors, ignore 20.9 concentration end points.
                if ((sensorGasCode == GasCode.O2) && cyl.IsFreshAir)
                {
                    Log.Debug("...Rejected. Can't use FreshAir for O2.");
                    continue;
                }

                // Ignore cylinders lacking the right gases.
                if (!cyl.ContainsGas(sensorGasCode, sensorConcentration, sensorMeasurementType))
                {
                    Log.Debug("...Rejected. Does not contain " + sensorGasCode + " with concentration " + sensorConcentration);
                    continue;
                }

                // Ensure bump tests for O2 sensors use concentrations of 19% O2 or less.
                if (sensorGasCode == GasCode.O2)
                {
                    // The following Find should always succeed assuming we already did a Cylinder.ContainsGas call earlier.
                    GasConcentration gasConcentration = cyl.GasConcentrations.Find(gc => gc.Type.Code == sensorGasCode);
                    // Parts per million (ppm) of X divided by 10,000 = percentage concentration of X
                    if ((gasConcentration.Concentration / 10000.0) > 19.0)
                    {
                        Log.Debug("...Rejected. Can't use concentrations above 19% for O2.");
                        continue;
                    }
                }

                // Ensure cylinder concentration is not higher than 60% for potentially explosive cal gases
                if (lelMultiplier > 0.0)
                {
                    // We now know this cylinder contains the desired gas concentration. But we don't allow
                    // calibration of combustible sensor using any cylinder that is higher than 60% LEL.
                    // So, find the gas in the cylinder and check it's LEL level.

                    double cylinderPPM = -1.0;
                    foreach (GasConcentration gasConcentration in cyl.GasConcentrations)
                    {
                        if (gasConcentration.Type.Code == sensorGasCode)
                        {
                            cylinderPPM = gasConcentration.Concentration;
                            break;
                        }
                    }

                    if (cylinderPPM < 0) // this should never happen.  Which means we better check.
                    {
                        Log.Debug("...Rejected. Does not contain " + sensorGasCode);
                        continue;
                    }

                    double cylinderLEL = cylinderPPM * lelMultiplier;
                    if (cylinderLEL > 60.0)  // cylinder is higher than 60%?  Don't use it.
                    {
                        Log.Debug(string.Format("...Rejected. Contains {0} with too high LEL concentration ({1}%%).",
                            sensorGasCode, Math.Round(cylinderLEL, 1)));
                        Log.Debug("...Will not use concentration higher than 60%% LEL.");
                        continue;
                    }
                }

                // Found a cylinder with the correct gas and concentration.
                _triedGasEndPoints[gasEndPoint] = gasEndPoint;

                Log.Debug("...SELECTED. Contains " + sensorGasCode + " with desired concentration " + sensorConcentration);

                return gasEndPoint;
            }

            Log.Debug("SCAN 2 (find appropriate gas with any concentration)...");

            pointNumber = 0;
            foreach (GasEndPoint gasEndPoint in gasEndPoints)
            {
                LogGasEndPoint(gasEndPoint, ++pointNumber);

                // Ignore already tried cylinders.
                if (_triedGasEndPoints.ContainsKey(gasEndPoint))
                {
                    Log.Debug("...Rejected. Already tried cylinder.");
                    continue;
                }

                // Ignore empty cylinders.
                if (gasEndPoint.Cylinder.Pressure == PressureLevel.Empty)
                {
                    _triedGasEndPoints[gasEndPoint] = gasEndPoint;
                    Log.Debug("...Rejected. Cylinder empty.");
                    continue;
                }

                // For oxygen sensors, ignore 20.9 concentration end points.
                if ((sensorGasCode == GasCode.O2)
                && gasEndPoint.Cylinder.IsZeroAir)
                {
                    Log.Debug("...Rejected. Can't use Zero Air for O2.");
                    continue;
                }

                // For oxygen sensors, ignore 20.9 concentration end points.
                if ((sensorGasCode == GasCode.O2)
                && gasEndPoint.Cylinder.IsFreshAir)
                {
                    Log.Debug("...Rejected. Can't use FreshAir for O2.");
                    continue;
                }

                // Ignore cylinders lack the right gases.
                if (!gasEndPoint.Cylinder.ContainsGas(sensorGasCode))
                {
                    Log.Debug("...Rejected. Does not contain " + sensorGasCode);
                    continue;
                }

                // Ensure bump tests for O2 sensors use concentrations of 19% O2 or less.
                if (sensorGasCode == GasCode.O2)
                {
                    // The following Find should always succeed assuming we already did a Cylinder.ContainsGas call earlier.
                    GasConcentration gasConcentration = gasEndPoint.Cylinder.GasConcentrations.Find(gc => gc.Type.Code == sensorGasCode);
                    // Parts per million (ppm) of X divided by 10,000 = percentage concentration of X
                    if ((gasConcentration.Concentration / 10000.0) > 19.0)
                    {
                        Log.Debug("...Rejected. Can't use concentrations above 19% for O2.");
                        continue;
                    }
                }

                // SGF  Feb-10-2009  DSW-72
                // Ensure cylinder concentration is not higher than 60% for potentially explosive cal gases

                if (lelMultiplier > 0.0)
                {
                    // We now know this cylinder contains the desired gas concentration. But we don't allow
                    // calibration of combustible sensor using any cylinder that is higher than 60% LEL.
                    // So, find the gas in the cylinder and check it's LEL level.

                    double cylinderPPM = -1.0;
                    foreach (GasConcentration gasConcentration in gasEndPoint.Cylinder.GasConcentrations)
                    {
                        if (gasConcentration.Type.Code == sensorGasCode)
                        {
                            cylinderPPM = gasConcentration.Concentration;
                            break;
                        }
                    }

                    if (cylinderPPM < 0) // this should never happen.  Which means we better check.
                    {
                        Log.Debug("...Rejected. Does not contain " + sensorGasCode);
                        continue;
                    }

                    double cylinderLEL = cylinderPPM * lelMultiplier;
                    if (cylinderLEL > 60.0)  // cylinder is higher than 60%?  Don't use it.
                    {
                        Log.Debug(string.Format("...Rejected. Contains {0} with too high LEL concentration ({1}%%).",
                            sensorGasCode, Math.Round(cylinderLEL, 1)));
                        Log.Debug("...Will not use concentration higher than 60%% LEL.");
                        continue;
                    }
                }

                // Found a cylinder with the correct gas.
                _triedGasEndPoints[gasEndPoint] = gasEndPoint;

                Log.Debug("...SELECTED. Contains " + sensorGasCode);

                return gasEndPoint;
            }

            return null;
        }

        /// <summary>
        /// Logs the gas end point.  This is a helper method for GetGasEndPoint.
        /// </summary>
        /// <param name="gasEndPoint"></param>
        /// <param name="pointCount"></param>
        private void LogGasEndPoint(GasEndPoint gasEndPoint, int pointCount)
        {
            string msg = "GasEndPoint #" + pointCount;
            Log.Debug(msg);

            Cylinder cyl = gasEndPoint.Cylinder;

            msg = "...Pos=" + gasEndPoint.Position
                //+ ", ID=" + cyl.ID
                + ", FactID=" + cyl.FactoryId
                + ", Part=" + cyl.PartNumber
                + ", Fresh=" + cyl.IsFreshAir
                + ", ZeroAir=" + cyl.IsZeroAir
                + ", Pressure=" + cyl.Pressure.ToString()
                + ", ExpDate=" + cyl.ExpirationDate;
            if (cyl.Volume != DomainModelConstant.NullInt) msg += ", Vol=" + cyl.Volume;

            Log.Debug(msg);

            msg = "......";
            foreach (GasConcentration gasCon in cyl.GasConcentrations)
            {
                msg += "[" + gasCon.Type.Code;
                msg += " ";
                msg += (gasCon.Concentration == DomainModelConstant.NullDouble) ? "fresh" : gasCon.Concentration.ToString();
                msg += "]";
            }
            Log.Debug(msg);
        }

        private bool IsBumpCriterionMet(BumpTestSensorInfo bumpTestSensorInfo, int criterion)
        {
            return IsBumpCriterionMet((Sensor)bumpTestSensorInfo.InstalledComponent.Component, bumpTestSensorInfo.SGR, criterion);
        }

        // SGF  24-Aug-2011  INS-2314 -- introducing the code for O2 tests
        // SGF  Jan-2-2009  DSW-173, DSW-174
        /// <summary>
        /// Compare the current reading with the target for the sensor.
        /// </summary>
        private bool IsBumpCriterionMet(Sensor sensor, SensorGasResponse response, int criterion)
        {
            if (sensor.BumpCriterionType == CriterionType.FullSpanValue)
                return response.FullSpanReserve >= (double)criterion;

            if (sensor.BumpCriterionType == CriterionType.PPMLimit)
                return response.Reading >= sensor.BumpCriterionPPMLimit;

            if (sensor.BumpCriterionType == CriterionType.O2)
            {
                // BEGIN INS-7625 SSAM v7.6
                // O2 low bump test: Update O2 pass criteria when 18 or 19 %VOL cylinder used to be between 15% and 19.5%.
                if (response.GasConcentration.Concentration >= 18.0 && response.GasConcentration.Concentration <= 19.0)
                    return (response.Reading > 15.0 && response.Reading < 19.5);
                // O2 low bump test: Update O2 pass criteria when N2 or less than 18 %VOL cylinder used to be between 1% and 19.5%.
                else if (response.GasConcentration.Concentration < 18.0)
                    return (response.Reading > 1.0 && response.Reading < 19.5);
                // END INS-7625
            }

            throw new ApplicationException("Unknown/unsupported BumpCriterionType: " + sensor.BumpCriterionType.ToString());
        }

        private bool IsPreconditionCriterionMet(BumpTestSensorInfo bumpTestSensorInfo)
        {
            // It doesn't make sense to look at FullSpanReserve for CLO2 sensors since we're bumping
            // with CL2.  e.g, a 0.5 reading with 10ppm CL2 == a span of 5, which makes no sense.
            // We instead just use bump ppm criteria.
            if (bumpTestSensorInfo.InstalledComponent.Component.Type.Code == SensorCode.ClO2)
            {
                Log.Assert(((Sensor)bumpTestSensorInfo.InstalledComponent.Component).BumpCriterionType == CriterionType.PPMLimit, "expected CLO2 BumpCriterionType to be PPMLimit?");
                return bumpTestSensorInfo.SGR.Reading >= ((Sensor)bumpTestSensorInfo.InstalledComponent.Component).BumpCriterionPPMLimit;
            }

            // default
            return bumpTestSensorInfo.SGR.FullSpanReserve > 50.0d;
        }

        /// <summary>
        /// This method takes one reading for all enabled O2 sensors without flowing any gas 
        /// which saves at least 4 seconds.
        /// <para>True - Run an O2 recovery purge.</para>
        /// <para>False - No O2 recovery purge needed.</para>
        /// </summary>
        /// <returns></returns>
        private bool IsFullO2RecoveryPurgeNeeded()
        {
            string funcMsg = "IsFullO2RecoveryPurgeNeeded: ";

            // Take an initial reading for all O2 sensors and only kick off a purge if one of them is not reading above 20%.
            List<InstalledComponent> o2Components = _returnEvent.DockedInstrument.InstalledComponents.FindAll(c => c.Component.Enabled && c.Component.Type.Code == SensorCode.O2);
            o2Components = o2Components.FindAll(o2 => _returnEvent.GetSensorGasResponseByUid(o2.Component.Uid) != null);

            int numO2SensorsPassed = 0;
            foreach (InstalledComponent ic in o2Components)
            {
                Sensor sensor = (Sensor)ic.Component;
                SensorGasResponse sgr = _returnEvent.GetSensorGasResponseByUid(sensor.Uid);
                sgr.Status = Status.O2RecoveryFailed;

                sgr.O2HighReading = _instrumentController.GetSensorReading(ic.Position, sensor.Resolution);
                sgr.Time = DateTime.UtcNow;
                Log.Debug(string.Format("{0}O2 sensor UID={1} O2HighReading={2}.", funcMsg, sensor.Uid, sgr.O2HighReading.ToString()));

                // SGF  24-Aug-2011  INS-2314 -- getting rid of the test for the high threshold, since we don't use cylinders with higher than normal O2 levels
                // INS-7625 SSAM v7.6 Adding test for high threshold to be max 22% VOL
                // O2 high bump test: Update O2 pass criteria to be between 20% and 22%.
                if (InstrumentPurgeOperation.OXYGEN_FRESH_AIR_TEST_LOW_PASS_PCT <= sgr.O2HighReading && sgr.O2HighReading <= OXYGEN_FRESH_AIR_TEST_HIGH_PASS_PCT)
                {
                    Log.Debug(string.Format("{0}O2 sensor UID={1} reading is within normal range.", funcMsg, sensor.Uid));
                    sgr.Status = Status.Passed;
                    sgr.IsO2HighBumpPassed = true;
                    numO2SensorsPassed++;
                }
            }

            if (numO2SensorsPassed == o2Components.Count)
                return false; // All O2 sensors pass the recovery test; time to short-circuit the purge

            return true;
        }

        #endregion Methods

    } // end-class InstrumentBumpTestOperation

    #region Exceptions

    /// <summary>
    /// The exception throw when a bump test cannot be completed.
    /// </summary>
    public class FailedBumpTestException: ApplicationException
	{
		/// <summary>
		/// Create a default instance of the exception.
		/// </summary>
		public FailedBumpTestException() : base( "Bump test failed." )
		{
			// Do Nothing
		}

		/// <summary>
		/// Create a default instance of the exception, wrapping another exception.
		/// </summary>
		/// <param name="e">The exception to wrap.</param>
		public FailedBumpTestException( Exception e ) : base( "Bump test failed." , e )
		{
			// Do Nothing
		}
	}

	/// <summary>
	/// This exception is thrown when there is no appropriate gas available to bump test.
	/// </summary>
	public class CorrectBumpTestGasUnavailable : ApplicationException
	{
		/// <summary>
		/// Creates a default instance of the exception.
		/// </summary>
		public CorrectBumpTestGasUnavailable() 
			: base( "The correct bump test gas is unavailable." )
		{
			// Do Nothing
		}

        /// <summary>
        /// Creates a default instance of the exception that wraps another exception.
        /// </summary>
        /// <param name="e">The exception to wrap.</param>
        public CorrectBumpTestGasUnavailable( string msg ) : base( "The correct bump test gas is unavailable (" + msg + ")." )
        {
            // Do Nothing
        }

		/// <summary>
		/// Creates a default instance of the exception that wraps another exception.
		/// </summary>
		/// <param name="e">The exception to wrap.</param>
		public CorrectBumpTestGasUnavailable( Exception e )
			: base( "The correct bump test gas is unavailable." , e )
		{
			// Do Nothing
		}
	}

	#endregion Exceptions

} // end-namespace
