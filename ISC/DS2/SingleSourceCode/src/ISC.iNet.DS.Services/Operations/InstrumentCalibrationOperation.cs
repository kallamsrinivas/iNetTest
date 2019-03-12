using System;
using System.Diagnostics;
using System.Collections.Generic;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services.Resources;
using ISC.WinCE.Logger;
using System.Threading;

namespace ISC.iNet.DS.Services
{
	
////////////////////////////////////////////////////////////////////////////////////////////////////
/// <summary>
/// Provides functionality to perform an instrument calibration.
/// </summary>
public partial class InstrumentCalibrationOperation : InstrumentCalibrationAction , IOperation
{
    #region Fields
	
    /// <summary>
    /// Number of seconds to wait during calibration between readings
    /// </summary>
    protected const int _WAIT_INTERVAL = 2000; // 2 secs

	/// <summary>
	/// This cushion is added to sensor's timeout value to make sure that DS never times out before instrument times out.
	/// </summary>
	protected readonly TimeSpan _timeOutCushion = new TimeSpan(0,0,30); // seconds.

    protected InstrumentController _instrumentController;

    protected InstrumentCalibrationEvent _returnEvent;

	private Dictionary<GasEndPoint,GasEndPoint> _triedGasEndPoints;  // this dictionary is used as a "Set" collection.

	/// <summary>
	/// The GasEndPoint that was used for zeroing. Set by zerosensor routine
	/// </summary>
    protected UsedGasEndPoint _zeroingUsedGasEndPoint = null;

    protected static int _biasStateTimeout = 7200; // INS-7657 RHP v7.5.2 - 2 hours is the timeout for checking the bias state before timeout

    protected List<SensorCalibrationLimits> _testOnlySensorCalibrationLimits = null;    

    #endregion Fields
	
    #region Constructors

    private void Init()
    {
        // Create an InstrumentController object to provide command processing functionality.
		_instrumentController = Master.Instance.SwitchService.InstrumentController;

        // For keeping track of cylinders that have been tried.
		_triedGasEndPoints = new Dictionary<GasEndPoint, GasEndPoint>();

        // Create the event that will be returned.
        _returnEvent = new InstrumentCalibrationEvent( this );

#if TEST
            _testOnlySensorCalibrationLimits = new List<SensorCalibrationLimits>();
            _testOnlySensorCalibrationLimits.Add(new SensorCalibrationLimits(SensorCode.CO, 60));
            _testOnlySensorCalibrationLimits.Add(new SensorCalibrationLimits(SensorCode.H2S, 60));
            _testOnlySensorCalibrationLimits.Add(new SensorCalibrationLimits(SensorCode.O2, 60));
            _testOnlySensorCalibrationLimits.Add(new SensorCalibrationLimits(SensorCode.CombustibleLEL, 60));
#endif
        }

        /// <summary>
        /// Creates a new instance of InstrumentCalibrationOperation class.
        /// </summary>
        public InstrumentCalibrationOperation()
        {
            Init();
        }

        public InstrumentCalibrationOperation( InstrumentCalibrationAction instrumentCalibrationAction )
        : base( instrumentCalibrationAction )
    {
        Init();

#if TEST
            _biasStateTimeout = 2;
#endif
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// IOperating implementation. Performs an instrument calibration operation.
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
    {
        //Clear if any flags before initiating calibration once again 
        Pump.IsBadPumpTubing = false;
        Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal = false;
        Master.Instance.SwitchService.BadPumpTubingDetectedDuringBump = false;

        Stopwatch stopwatch = Log.TimingBegin("INSTRUMENT CALIBRATION");

        _returnEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
        _returnEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

        Log.Debug( string.Format( "{0}.Execute {1}", Name, _returnEvent.DockedInstrument.SerialNumber ) );

        // Sort sensors by calibration order.
        _returnEvent.DockedInstrument.InstalledComponents = InstrumentController.SortSensorsByCalibrationOrder( _returnEvent.DockedInstrument.InstalledComponents );

        #region LogDebug
        Log.Debug( "Candidate gases..." );
        int pointCount = 0;
		foreach ( GasEndPoint gasEndPoint in GasEndPoints )
        {
            string msg = "GasEndPoint #" + ++pointCount;
            Log.Debug(msg);
            Cylinder cyl = gasEndPoint.Cylinder;
            msg = "...Pos: " + gasEndPoint.Position
                //+ ", ID: " + cyl.ID
                + ", FactID: " + cyl.FactoryId
                + ", Part: " + cyl.PartNumber
                + ", Fresh: " + cyl.IsFreshAir
                + ", ZeroAir: " + cyl.IsZeroAir
                + ", Pressure: " + cyl.Pressure.ToString();

            Log.Debug( msg );
            msg = "......";
            foreach ( GasConcentration gasCon in cyl.GasConcentrations )
            {
                msg += "[" + gasCon.Type.Code + " ";
                msg += ( gasCon.Concentration == DomainModelConstant.NullDouble ) ? "fresh" : gasCon.Concentration.ToString();
                msg += "]";
            }
            Log.Debug( msg );
        }

        Log.Debug( "Going to CALIBRATE sensors in following ORDER..." );
        int calOrder = 1;
        foreach ( InstalledComponent ic in _returnEvent.DockedInstrument.InstalledComponents )
        {
            if ( !( ic.Component is Sensor ) )
                continue; // Skip non-sensors.
            Log.Debug( "...#" + calOrder++ + ", Position " + ic.Position + ", UID=" + ic.Component.Uid + ", " + ic.Component.Type.Code );

        }
        #endregion

        try
        {
            _instrumentController.Initialize();

            CalibrateInstrument();

                // Need to determine the next time this instrument will be calibrated.  
                // Put this date into the event so it can be uploaded to iNet, and also 
                // update the global next date that's held in the switch service.
                _returnEvent.NextUtcScheduledDate = Master.Instance.SwitchService.NextUtcCalibrationDate
                 = Master.Instance.Scheduler.GetNextGasOperationDate( _returnEvent );
            }
            finally
        {
            // Dispose of the operation utility.
            _instrumentController.Dispose();
        }

        Log.TimingEnd( "INSTRUMENT CALIBRATION", stopwatch );

        return _returnEvent;
    }

    protected void ResetTriedGasEndPoints()
    {
		foreach ( GasEndPoint gasEndPoint in GasEndPoints )
        {
            if (_triedGasEndPoints.ContainsKey(gasEndPoint))
            {
                Cylinder cyl = gasEndPoint.Cylinder;
                if (cyl.Pressure != PressureLevel.Empty)
                {
                    _triedGasEndPoints.Remove(gasEndPoint);
                }
            }
        }
    }

    /// <summary>
    /// Find the fresh air end point for this sensor.
    /// </summary>
    /// <param name="installedComponent">The sensor to find the fresh air for.</param>
    /// <param name="checkIfUsed">If true, check if the cylinder under consideration
    /// has already been used.</param>
    /// <returns>The correct fresh air gas end point.</returns>
    /// <exception cref="CorrectCalibrationGasUnavailable">
    /// If there are no fresh air gas end points for this sensor.
    /// </exception>
	protected GasEndPoint GetSensorFreshAir( InstalledComponent installedComponent, bool checkIfUsed ) 
    {
        Sensor sensor = null;

        if ( installedComponent != null )
            sensor = installedComponent.Component as Sensor;

        string msg = "Finding Fresh air";
        if ( sensor != null )
            msg += " for sensor " + sensor.Uid;
        msg += "...";
        Log.Debug( msg );

        // Find fresh air and zero gas end points.
        int pointCount = 0;
		foreach ( GasEndPoint gasEndPoint in GasEndPoints )
        {
            #region LogDebug
            msg = "GasEndPoint #" + ++pointCount;
            Log.Debug(msg);

            Cylinder cyl = gasEndPoint.Cylinder; 

            msg = "...Pos=" + gasEndPoint.Position
                + ", FactID=" + cyl.FactoryId
                + ", Part=" + cyl.PartNumber
                + ", Fresh=" + cyl.IsFreshAir
                + ", ZeroAir=" + cyl.IsZeroAir
                + ", Pressure=" + cyl.Pressure.ToString();
            if ( cyl.Volume != DomainModelConstant.NullInt ) msg += ", Vol=" + cyl.Volume;

            Log.Debug( msg );
            #endregion

            // Ignore non-fresh air cylinders
            if ( !cyl.IsFreshAir )
            {
                Log.Debug( "...Rejected.  Not fresh air." );
                continue;
            }

            if ( cyl.Pressure == PressureLevel.Empty )
            {
                Log.Debug( "...Rejected fresh air. Cylinder empty." );
                continue;
            }

            if (checkIfUsed == true && _triedGasEndPoints.ContainsKey(gasEndPoint))
            {
                Log.Debug("...Rejected. Already tried cylinder.");
                continue;
            }

            if ( sensor == null )
            {
                Log.Debug( "...SELECTED GasEndPoint. Fresh air found." );
                return gasEndPoint;
            }

            Log.Debug( "...SELECTED GasEndPoint.  Fresh air found for sensor " + sensor.Uid );
            return gasEndPoint;
        }
        
        Log.Debug( "No fresh air found.");

        throw new CorrectCalibrationGasUnavailable( GasCode.FreshAir ); // No calibration gases were found.
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
    /// <exception cref="CorrectCalibrationGasUnavailable">
    /// If there are no zero air gas end points for this sensor.
    /// </exception>
	protected GasEndPoint GetSensorZeroAir( InstalledComponent installedComponent, bool zeroAirOnly, bool checkIfUsed ) 
    {
        Sensor sensor = null;

        if ( installedComponent != null )
            sensor = installedComponent.Component as Sensor;

        string msg = "Finding Zero air cylinder";
        if ( sensor != null )
            msg += " for sensor " + sensor.Uid;
        msg += "...";
        Log.Debug( msg );

        // Find zero air end points.

        int pointCount = 0;

		foreach ( GasEndPoint gasEndPoint in GasEndPoints )
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
            if ( cyl.Volume != DomainModelConstant.NullInt ) msg += ", Vol=" + cyl.Volume;

            Log.Debug( msg );
            #endregion

            if ( !cyl.IsZeroAir )
            {
                Log.Debug( "...Rejected.  Not zero air." );
                continue;
            }

            if ( cyl.Pressure == PressureLevel.Empty )
            {
                Log.Debug( "...Rejected zero air. Cylinder empty." );
                continue;
            }

            if (checkIfUsed == true && _triedGasEndPoints.ContainsKey(gasEndPoint))
            {
                Log.Debug("...Rejected. Already tried cylinder.");
                continue;
            }

            if ( sensor == null )
            {
                Log.Debug( "...SELECTED GasEndPoint.  Zero air found." );
                return gasEndPoint;
            }

            Log.Debug( "...SELECTED GasEndPoint.  Zero air found for sensor " + sensor.Uid );
            return gasEndPoint;
        }

        if ( zeroAirOnly )
        {
            Log.Debug( "No zero air found.");
            throw new CorrectCalibrationGasUnavailable( "Zero Air" );
        }

        // No calibration gases were found, attempt to use the fresh air.
        Log.Debug( "No zero air found.  Looking for alternative fresh air...");

        return GetSensorFreshAir(installedComponent, checkIfUsed);
    }

    /// <summary>
    /// Get the calibration test gas end point for a sensor.
    /// </summary>
    /// <param name="installedComponent">The sensor to get the gas for.</param>
    /// <returns>The correct gas end point.</returns>
    /// <exception cref="CorrectCalibrationGasUnavailable">
    /// Thrown when no cylinder is provided for the sensor.
    /// </exception>
	protected GasEndPoint GetSensorGasEndPoint( InstalledComponent installedComponent )
    {
        Sensor sensor = null;
        MeasurementType sensorMeasurementType = MeasurementType.Unknown;

        if (installedComponent != null)
        {
            sensor = installedComponent.Component as Sensor;
            sensorMeasurementType = ((SensorType)sensor.Type).MeasurementType;
        }

        #region LogDebug
        if (sensor != null)
        {
            Log.Debug( "Calibration.GetSensorGasEndPoint" );
            Log.Debug( "Finding appropriate cal gas for Sensor: " + sensor.Type
                + ", UID: " + sensor.Uid
                + ", CalGas Code: " + sensor.CalibrationGas.Code
                + ", Conc: " + sensor.CalibrationGasConcentration
                + ", Pos: " + installedComponent.Position );
        }
        #endregion

        // If this is a sensor that uses 20.9 O2 for its calibration gas,
        // then attempt to find a cylinder that offers Zero Air; if 
        // Zero Air is not available, find Fresh Air.  If neither are
        // available, then fall out of this code and proceed to the main
        // processing loop below.
        if ( sensor.CalibrationGas.Code == GasCode.O2 && sensor.CalibrationGasConcentration == 20.9 )
        {
			GasEndPoint air = GetSensorZeroAir( installedComponent, false, true ); 
            if (air != null)
            {
                _triedGasEndPoints[air] = air;
                Cylinder cyl = air.Cylinder;
                if (cyl.IsZeroAir)
                    Log.Debug("...SELECTED GasEndPoint.  Has ZeroAir for O2 at 20.9.");
                else if (cyl.IsFreshAir)
                    Log.Debug("...SELECTED GasEndPoint.  Has FreshAir for O2 at 20.9.");
                else
                    // We do not expect to reach this line of code, but it has been placed here to 
                    // log that GetSensorZeroAir returned a cylinder that was Fresh Air or Zero Air.
                    Log.Debug(string.Format("...SELECTED GasEndPoint: {0}", cyl.ToString()));
                return air;
            }
        }

        double sensorConcentration = sensor.CalibrationGasConcentration;

        double lelMultiplier = GasType.Cache[ sensor.CalibrationGas.Code ].LELMultiplier;

        int pointCount = 0;

        Log.Debug( "SCAN 1 (Find appropriate gas with desired concentration)..." );
		foreach ( GasEndPoint gasEndPoint in GasEndPoints )
        {
            Cylinder cyl = gasEndPoint.Cylinder;

            #region LogDebug
            string msg = "GasEndPoint #" + ++pointCount;
            Log.Debug(msg);

            msg = "...Pos=" + gasEndPoint.Position
                //+ ", ID=" + cyl.ID
                + ", FactID=" + cyl.FactoryId
                + ", Part=" + cyl.PartNumber
                + ", Fresh=" + cyl.IsFreshAir
                + ", ZeroAir=" + cyl.IsZeroAir
                + ", Pressure=" + cyl.Pressure.ToString();
            if ( cyl.Volume != DomainModelConstant.NullInt ) msg += ", Vol=" + cyl.Volume;

            Log.Debug( msg );
            
            msg = "......";
            foreach ( GasConcentration gasCon in cyl.GasConcentrations )
            {
                msg += "[" + gasCon.Type.Code;
                msg += " ";
                msg += ( gasCon.Concentration == DomainModelConstant.NullDouble ) ? "fresh" : gasCon.Concentration.ToString();
                msg += "]";
            }

            Log.Debug( msg );
            #endregion

            // Ignore already tried cylinders.
            if ( _triedGasEndPoints.ContainsKey( gasEndPoint ) )
            {
                Log.Debug( "...Rejected. Already tried cylinder." );
                continue;
            }

            // Ignore empty cylinders.
            if ( cyl.Pressure == PressureLevel.Empty )
            {
                _triedGasEndPoints[ gasEndPoint ] = gasEndPoint;
                Log.Debug( "...Rejected. Cylinder empty." );
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
                // SEPARATED AS THREE SEPARATE LINES, OTHERWISE IT CRASHES WINCE.
                // SOME BUG IN COMPACT FRAMEWORK?
                // sensorConcentration = Math.Round( sensorConcentration / 100.0, 0 ) * 100.0;
                sensorConcentration /= 100.0;
                sensorConcentration = Math.Round( sensorConcentration, 0 );
                sensorConcentration *= 100.0;
                Log.Debug( string.Format( "...Sensor concentration of {0}%% LEL equals {1} PPM ({2} multiplier)",
                    sensor.CalibrationGasConcentration, sensorConcentration, lelMultiplier ) );
            }

            else if (sensorMeasurementType == MeasurementType.VOL)
            {
                sensorConcentration = sensor.CalibrationGasConcentration * 10000;
                Log.Debug( string.Format( "...Sensor concentration of {0}%% VOL equals {1} PPM",
                    sensor.CalibrationGasConcentration, sensorConcentration ) );
            }

            // Ignore cylinders lacking the right gases.
            if (!cyl.ContainsGas(sensor.CalibrationGas.Code, sensorConcentration, sensorMeasurementType))
            {
                Log.Debug( "...Rejected. Does not contain " + sensor.CalibrationGas.Code + " with desired concentration " + sensorConcentration );
                continue;
            }

            // SGF  Feb-10-2009  DSW-72
            // Ensure cylinder concentration is not higher than 60% for potentially explosive cal gases

            if ( lelMultiplier > 0.0 )
            {
                // We now know this cylinder contains the desired gas concentration. But we don't allow
                // calibration of combustible sensor using any cylinder that is higher than 60% LEL.
                // So, find the gas in the cylinder and check it's LEL level.

                double cylinderPPM = -1.0;
                foreach ( GasConcentration gasConcentration in cyl.GasConcentrations )
                {
                    if ( gasConcentration.Type.Code == sensor.CalibrationGas.Code )
                    {
                        cylinderPPM = gasConcentration.Concentration;
                        break;
                    }
                }

                if ( cylinderPPM < 0 ) // this should never happen.  Which means we better check.
                {
                    Log.Debug( "...Rejected. Does not contain " + sensor.CalibrationGas.Code );
                    continue;
                }

                double cylinderLEL = cylinderPPM * lelMultiplier;
                if ( cylinderLEL > 60.0 )  // cylinder is higher than 60%?  Don't use it.
                {
                    Log.Debug( string.Format( "...Rejected. Contains {0} with too high LEL concentration ({1}%%)",
                        sensor.CalibrationGas.Code,  Math.Round( cylinderLEL, 1 ) ) );
                    Log.Debug( "...Will not use concentration higher than 60%% LEL." );
                    continue;
                }
            }

            // Found a cylinder with the correct gas and concentration.
            _triedGasEndPoints[ gasEndPoint ] = gasEndPoint;

            Log.Debug( "...SELECTED. Contains " + sensor.CalibrationGas.Code + " with desired concentration " + sensorConcentration );

            return gasEndPoint;

        }  // end-foreach 1

        Log.Debug( "SCAN 2 (find appropriate gas with any concentration)..." );

        pointCount = 0;
		foreach ( GasEndPoint gasEndPoint in GasEndPoints )
        {
            Cylinder cyl = gasEndPoint.Cylinder;

            #region LogDebug

            string msg = "GasEndPoint #" + ++pointCount;
            Log.Debug(msg);

            msg = "...Pos=" + gasEndPoint.Position
                //+ ", ID=" + cyl.ID
                + ", FactID=" + cyl.FactoryId
                + ", Part=" + cyl.PartNumber
                + ", Fresh=" + cyl.IsFreshAir
                + ", ZeroAir=" + cyl.IsZeroAir
                + ", Pressure=" + cyl.Pressure.ToString();
            if ( cyl.Volume != DomainModelConstant.NullInt ) msg += ", Vol=" + cyl.Volume;

            Log.Debug( msg );
            
            msg = "......";
            foreach ( GasConcentration gasCon in cyl.GasConcentrations )
            {
                msg += "[" + gasCon.Type.Code;
                msg += " ";
                msg += ( gasCon.Concentration == DomainModelConstant.NullDouble ) ? "fresh" : gasCon.Concentration.ToString();
                msg += "]";
            }

            Log.Debug( msg );
            #endregion

            // Ignore already tried cylinders.
            if ( _triedGasEndPoints.ContainsKey( gasEndPoint ) )
            {
                Log.Debug( "...Rejected. Already tried cylinder." );
                continue;
            }

            // Ignore cylinders lack the right gases.
            if ( ! cyl.ContainsGas( sensor.CalibrationGas.Code ) )
            {
                Log.Debug( "...Rejected. Does not contain " + sensor.CalibrationGas.Code  );
                continue;
            }

            // SGF  Feb-10-2009  DSW-72
            // Ensure cylinder concentration is not higher than 60% for potentially explosive cal gases
            if ( lelMultiplier > 0.0 )
            {
                // We now know this cylinder contains the right gas.  But we don't allow 
                // calibration of combustible sensor using any cylinder that is higher than 60% LEL.
                // So, find the gas in the cylinder and check it's LEL level.

                double cylinderPPM = -1.0;
                foreach ( GasConcentration gasConcentration in cyl.GasConcentrations )
                {
                    if ( gasConcentration.Type.Code == sensor.CalibrationGas.Code )
                    {
                        cylinderPPM = gasConcentration.Concentration;
                        break;
                    }
                }

                if ( cylinderPPM < 0 ) // this should never happen.  Which means we better check.
                {
                    Log.Debug( "...Rejected. Does not contain " + sensor.CalibrationGas.Code );
                    continue;
                }

                double cylinderLEL = cylinderPPM * lelMultiplier;
                if ( cylinderLEL > 60.0 )  // cylinder is higher than 60%?  Don't use it.
                {
                    Log.Debug( string.Format( "...Rejected. Contains {0} with too high LEL concentration ({1}%%)",
                        sensor.CalibrationGas.Code,  Math.Round( cylinderLEL, 1 ) ) );
                    Log.Debug( "...Will not use concentration higher than 60%% LEL." );
                    continue;
                }

            } // end-if MeasurementTypes.LEL

            // Found a cylinder with the correct gas.
            _triedGasEndPoints[ gasEndPoint ] = gasEndPoint;
            
            Log.Debug( "...SELECTED. Contains " + sensor.CalibrationGas.Code );

            return gasEndPoint;

        } // end-foreach 2

		// If this is a sensor that uses O2 for its calibration gas, and no cylinder has been found 
		// yet, then attempt to find a cylinder that offers Fresh Air.  We don't try to find a Zero 
		// Air cylinder because one of the scans above would have already selected it if one was
		// available.  Also, O2 sensors with a cal gas concentration of 20.9 should have already
		// selected a Zero Air or Fresh Air cylinder so we don't need to check again.
		if ( sensor.CalibrationGas.Code == GasCode.O2 && sensor.CalibrationGasConcentration != 20.9 )
		{
			GasEndPoint air = GetSensorFreshAir( installedComponent, true );
			if ( air != null )
			{
				_triedGasEndPoints[air] = air;
				Cylinder cyl = air.Cylinder;
				if ( cyl.IsFreshAir )
					Log.Debug( "...SELECTED GasEndPoint.  Has FreshAir for O2." );
				else
					// We do not expect to reach this line of code, but it has been placed here to 
					// log that GetSensorFreshAir returned a cylinder.
					Log.Debug( string.Format( "...SELECTED GasEndPoint: {0}", cyl.ToString() ) );
				return air;
			}
		}

        Log.Debug( "NO APPROPRIATE CALIBRATION GAS FOUND!" );

        throw new CorrectCalibrationGasUnavailable( string.Format( "Sensor {0}, CalGas={1}({2})", 
            sensor.Uid, sensor.CalibrationGas.Code, sensor.CalibrationGasConcentration ) ); // No gas end point was found.
    }

    protected void CalibrateInstrument()
    {
        try
		{
			// SGF  14-Jun-2011  INS-1732
			// Create sensor gas response objects for each sensor in the instrument
			foreach ( InstalledComponent installedComponent in _returnEvent.DockedInstrument.InstalledComponents )
			{
				if ( !( installedComponent.Component is Sensor ) )  // Skip non-sensors.
					continue;
				if ( !installedComponent.Component.Enabled )
					continue;

				Sensor sensor = (Sensor)installedComponent.Component;
				SensorGasResponse sgr = new SensorGasResponse( sensor.Uid, DateTime.UtcNow );
				sgr.GasConcentration = new GasConcentration( sensor.CalibrationGas, sensor.CalibrationGasConcentration );
                sgr.GasDetected = sensor.GasDetected;
				sgr.Type = GasResponseType.Calibrate;
				// JFC 07-Mar-2014 INS-4839
				// Recording the last calibration time before calibration to ensure it will be changed by the instrument once
				// calibration of the sensor completes.  If it does not, a status of InstrumentAborted should be assumed.
				sgr.PreCal_LastCalibrationTime = _instrumentController.GetSensorLastCalibrationTime( installedComponent.Position );

				if ( _zeroingUsedGasEndPoint != null )
					sgr.UsedGasEndPoints.Add( _zeroingUsedGasEndPoint );

				_returnEvent.GasResponses.Add( sgr );
			}

			// SGF  14-Jun-2011  INS-1732
			SensorGasResponse response = null;
			if ( _returnEvent.GasResponses.Count > 0 )
				response = _returnEvent.GasResponses[ 0 ];

			if ( _instrumentController.TestForInstrumentReset( response, "calibrating instrument, start" ) == true )
			{
				Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
				//_returnEvent.GasResponses.Add( response ); // SGF  14-Jun-2011  INS-1732 -- responses already added to return event
				return;
			}

			// Prior to zeroing, preconditioning, and calibrating, we need to tell the
			// instrument to turn on its sensors.
			Stopwatch stopwatch = Log.TimingBegin( "CAL - TURN ON SENSORS" );
			_instrumentController.TurnOnSensors( true,true );
            Log.TimingEnd( "CAL - TURN ON SENSORS", stopwatch );

            // BEIGN INS-7657 RHP v7.5.2
            DateTime biasStateLoopStartTime = DateTime.UtcNow;
            TimeSpan biasStateElapsedTime = TimeSpan.Zero;

            while (!_instrumentController.GetSensorBiasStatus())
            {
                // Calculate the time that has elapsed since the start of the calibration loop
                biasStateElapsedTime = DateTime.UtcNow - biasStateLoopStartTime;
                Log.Debug(string.Format("{0} Time elapsed in Bias State pass = {1}", "CALIBRATION", (int)biasStateElapsedTime.TotalSeconds));

                Master.Instance.ConsoleService.UpdateState(ConsoleState.CalibratingInstrument, new string[] { string.Format(ConsoleServiceResources.ELAPSEDBIASSTATE, Math.Round(biasStateElapsedTime.TotalSeconds).ToString()) });

                if (biasStateElapsedTime.TotalSeconds > _biasStateTimeout) // Have we timed out?
                {
                    Log.Debug("CALIBRATION: ABORTED DUE TO TIMING OUT BIAS STATE CHECK");
                    throw new InstrumentNotReadyException();
                }
#if !TEST
                    // Allow a ten second interval so that we give some interval before we check the sensor Bias state again.
                    Thread.Sleep(10000);
#endif
            }
            //END DSW-7657

			if ( _instrumentController.TestForInstrumentReset( response, "calibrating instrument, after turning on sensors" ) == true )
			{
				Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
				//_returnEvent.GasResponses.Add( response ); // SGF  14-Jun-2011  INS-1732 -- responses already added to return event
				return;
			}

			#region Check for Sensors in Error Mode

            stopwatch = Log.TimingBegin( "CAL - CHECK SENSORS ERROR MODE" );

			// SGF  Sep-15-2008  DSZ-1501 ("DS sometimes attempts to zero/calibrate MX6 sensors in 'ERR'")
			// Prior to calibrating the sensors in the instrument, we must ensure that sensors have powered 
			// up, and then we must ensure that none of the sensors go into error. Sensors were already turned on above.

			Master.Instance.SwitchService.Instrument.SensorsInErrorMode.Clear(); //Suresh 05-JAN-2012 INS-2564
			SensorPosition[] sensorPositions = _instrumentController.GetSensorPositions();

			foreach ( SensorPosition sensorPosition in sensorPositions )
			{
				if ( sensorPosition.Mode == SensorMode.Error )
				{
					string sensorPosString = sensorPosition.Position.ToString();
					Log.Warning( "CALIBRATION: SENSOR IN ERROR MODE AT POSITION " + sensorPosString );
					InstalledComponent ic = new InstalledComponent();
					ic.Component = new Sensor();
					ic.Position = sensorPosition.Position;
					Master.Instance.SwitchService.Instrument.SensorsInErrorMode.Add( ic );
				}
			}

            Log.TimingEnd( "CAL - CHECK SENSORS ERROR MODE", stopwatch );

			if ( Master.Instance.SwitchService.Instrument.SensorsInErrorMode.Count > 0 )
				throw new SensorErrorModeException( "The calibration failed due to sensor being in error.  Instrument: " + _returnEvent.DockedInstrument.SerialNumber + " ." );

			#endregion
             
			// Clear the gases in the lines.
            stopwatch = Log.TimingBegin( "CAL - PURGE(INITIAL)" );
			new InstrumentPurgeOperation( PurgeType.PreCalibration, _instrumentController, GasEndPoints, _returnEvent ).Execute();
            Log.TimingEnd( "CAL - PURGE(INITIAL)", stopwatch );

			if ( _instrumentController.TestForInstrumentReset( response, "calibrating instrument, after clearing gases" ) == true )
			{
				Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
				//_returnEvent.GasResponses.Add(response); // SGF  14-Jun-2011  INS-1732 -- responses already added to return event
				return;
			}

			// Zero the sensor before proceeding.
            stopwatch = Log.TimingBegin( "CAL - ZEROING" );
			ZeroSensors( _returnEvent.DockedInstrument.InstalledComponents );
            Log.TimingEnd( "CAL - ZEROING", stopwatch );

			if ( _instrumentController.TestForInstrumentReset( response, "calibrating instrument, after zeroing" ) == true )
			{
				Log.Warning( "CALIBRATION: ABORTED DUE TO INSTRUMENT RESET" );
				//_returnEvent.GasResponses.Add(response); // SGF  14-Jun-2011  INS-1732 -- responses already added to return event
				return;
			}

            //Clear if any flags before initiating calibration once again since this would be set to true during Pre-Calibration Purge
            Pump.IsBadPumpTubing = false;

            if ( _returnEvent.DockedInstrument.Type == DeviceType.TX1
            ||   _returnEvent.DockedInstrument.Type == DeviceType.VPRO
            ||   _returnEvent.DockedInstrument.Type == DeviceType.SC  )
			{
				CalibrateInstrumentParallel(); // Also known as "quick cal".
			}
			else
			{
				CalibrateInstrumentSequential();
			}            

			// Wipe out most data that will be uploaded to iNet when sensor calibration was aborted by the instrument.
			for ( int i = 0; i < _returnEvent.GasResponses.Count; i++ )
			{
				if ( _returnEvent.GasResponses[ i ].Status == Status.InstrumentAborted )
				{
					_returnEvent.GasResponses[ i ] = SensorGasResponse.CreateInstrumentAbortedSensorGasResponse( _returnEvent.GasResponses[ i ] );
				}
			}
		}
		catch ( CorrectCalibrationGasUnavailable ccgu )
		{
			Log.Warning( Name + ": Calibration gas unavailable", ccgu );
			throw;
		}
		catch ( FlowFailedException ffe )
		{
			Log.Warning( Name + ": Flow failed", ffe );
			throw;
		}
		catch ( SensorErrorModeException )
		{
			Log.Warning( Name + ": SensorErrorModeException thrown." );
			throw;
		}
		catch ( ISC.Instrument.Driver.CommunicationAbortedException cae )
		{
			throw new InstrumentNotDockedException( cae );
		}
		catch ( InstrumentNotDockedException )
		{
			throw;
		}
		catch ( ISC.Instrument.Driver.SystemAlarmException sae ) // some instruments may throw this during sensor warmup.
		{
			throw new InstrumentSystemAlarmException( Master.Instance.SwitchService.Instrument.SerialNumber, sae.ErrorCode );
		}
        // INS-7657 RHP v7.5.2 Display Instrument Not Ready Message to be specific that the error is due to Sesnor not biased within 2 hours
        catch (InstrumentNotReadyException inr)
        {
            throw new InstrumentNotReadyException(inr);
        }
		catch ( Exception e )
		{
			throw new FailedCalibrationException( e );
		}
        finally
        {            
            // Make sure we turn off instrument pump before leave.  It may be still on
            // if the reason we're in this finally block is due to a thrown exception.
            _instrumentController.EnablePump( false ); // This call will first check if instrument is docked.

            if(Master.Instance.SwitchService.BadPumpTubingDetectedDuringCal)
                Master.Instance.ConsoleService.UpdateState(ConsoleState.CalibrationStoppedCheckTubing);
            else
                // Clear the reference to a step in the calibration process
                Master.Instance.ConsoleService.UpdateState( ConsoleState.CalibratingInstrument );
        }
    }

    /// Zeroes all sensors contained on the instrument.
    /// 
    /// NOTE: THIS ROUTINE LOOKS LIKE IT CAN ZERO A SPECIFIC SENSOR 
    /// BUT IN ACTUALITY, IT ALWAYS ZEROS ALL SENSORS.
    /// </summary>
    /// <param name="installedComponents">Contains InstalledComponents for the instrument being zeroed.</param>
    private void ZeroSensors( IEnumerable<InstalledComponent> installedComponents )
    {
        Log.Debug( "ZEROING: Preparing to zero" );

        // Indicate that the zeroing process is now in progress
        Master.Instance.ConsoleService.UpdateState( ConsoleState.CalibratingInstrument, ConsoleServiceResources.ZEROING );

        // See if we have a CO2 sensor.  If so, then we can only zero using zero air.
        // fresh air is NOT allowed.  If no CO2 sensor is installed, then fresh
        // air may be used as an alternative to zero air, if zero air is not found.
        bool useZeroAirOnly = false;
        foreach ( InstalledComponent installedComponent in installedComponents )
        {
            if ( installedComponent.Component.Type.Code == SensorCode.CO2 )
            {
                Sensor sensor = (Sensor)installedComponent.Component;

                if ( !sensor.Enabled ) // if it's disabled, we won't be zeroing it.
                    continue;

                Log.Debug( "ZEROING: Found CO2 sensor.  Will not use fresh air to zero." );
                useZeroAirOnly = true;
                break;
            }
        }

		GasEndPoint zeroEndPoint = null;

        DateTime startTime = DateTime.UtcNow;

        try
        {
			_zeroingUsedGasEndPoint = null;  // Reset any previous setting (if any).

            zeroEndPoint = GetSensorZeroAir( null, useZeroAirOnly, false );  // Get the zeroing gas end point for this gas.

            _instrumentController.OpenGasEndPoint( zeroEndPoint, Pump.StandardFlowRate );

            // ZeroSensor will return false if IDS times out before instrument finishes zeroing.
            // Return value does NOT indicate if zeroing was successful or not!
            if ( !_instrumentController.ZeroSensors( zeroEndPoint ) )
                throw new UnableToZeroInstrumentSensorsException();

            // SGF  14-Jun-2011  INS-1732 -- get sensor readings after zeroing has taken place
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
                    sgr.ReadingAfterZeroing = _instrumentController.GetSensorReading( ic.Position, sensor.Resolution );
                    sgr.TimeAfterZeroing = DateTime.UtcNow;
				}
            }
        }
        catch ( Exception e )
        {
            Log.Error( "Zeroing Sensor", e );
            throw;
        }
        finally
        {
            Log.Debug( "ZEROING: Finished" );

            _instrumentController.CloseGasEndPoint( zeroEndPoint );

            if ( zeroEndPoint != null )  // how could this ever be null?
                _zeroingUsedGasEndPoint = new UsedGasEndPoint( zeroEndPoint, CylinderUsage.Zero, DateTime.UtcNow - startTime, 0 );
        }
    }

    /// <summary>
    /// INS-7282 - Check the sensor age based on the setup date and the configured sensor age
    /// This is applicable only for Repair account type.
    /// </summary>
    /// <param name="sensor"></param>
    /// <param name="response"></param>
    /// <returns>bool</returns>
    private bool IsSensorExpiredForServiceAccount( Sensor sensor, SensorGasResponse response, List<SensorCalibrationLimits> sensorCalibrationLimits )
    {
        // Ideally the sensorCalibrationLimits will be loaded only for repair Account, but its better to validate that.
        // Possible cases where the calibration limits are configured for the service account and then changed the service type of the account to another.
        if ( Configuration.IsRepairAccount() && ( sensorCalibrationLimits.Count > 0 ) )
        {            
            SensorCalibrationLimits sensorCalLimits = sensorCalibrationLimits.Find(sc => sc.SensorCode == sensor.Type.Code);
            if (sensorCalLimits != null)
            {
                int sensorLifeInMonths = sensorCalLimits.Age;
                DateTime sensorAge;
                try
                {
                    //Some of the LEL sensors are not having the setup date, in that case get the first four digits which determines the sensor setup year and month 
                    if (sensor.SetupDate == DomainModelConstant.NullDateTime)
                    {
                        int year = Convert.ToInt32(sensor.SerialNumber.Substring(0, 2));
                        int month = Convert.ToInt32(sensor.SerialNumber.Substring(2, 2));
                        DateTime date = new DateTime(year, month, 01);
                        sensorAge = date.AddMonths(sensorLifeInMonths);
                    }
                    else
                    {
                        sensorAge = sensor.SetupDate.AddMonths(sensorLifeInMonths);
                    }

                    bool expired = DateTime.Now > sensorAge;
                    Log.Debug( string.Format( "Expired sensor check: {0}, SetupDate:{1}, Expiry Age:{2}, AgeLimit:{3} (Expired={4})", 
                        sensor.Type.Code, Log.DateTimeToString(sensor.SetupDate), Log.DateTimeToString(sensorAge), sensorLifeInMonths, expired ) );
                    return expired;
                }
                catch (Exception ex)
                {
                    Log.Error( string.Format( "Skipping sensor age check for {0} sensor. Invalid SetupDate \"{1}\".",
                        sensor.Type.Code, Log.DateTimeToString(sensor.SetupDate) ), ex );
                    return false;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// INS-7282 - Check the sensor span reserve and if the value is less than the configured threshold, set the sensor calibration as Failed.
    /// This check is applicable only for Repair account type.
    /// </summary>
    /// <param name="sensor"></param>
    /// <param name="response"></param>
    /// <returns>bool</returns>
    private bool IsSensorFailedForRepairAccount( Sensor sensor, SensorGasResponse response, double spanReserveThreshold )
    {
        if ( Configuration.IsRepairAccount() && ( spanReserveThreshold > 0 ) )
        {            
            double spanReserve = ( response.Reading / sensor.CalibrationGasConcentration ) * 100;
            Log.Debug( string.Format( "Checking if sensor span reserve is below the threshold. Sensor:{0}, Span Reserve Threshold:{1}, Span Reserve:{2}", sensor.Type.Code, spanReserveThreshold, spanReserve ) );
            if ( spanReserve < spanReserveThreshold )
            {
                Log.Warning( string.Format( "Sensor span reserve is less than the configured span reserve threshold for Sensor:{0}, Span Reserve Threshold:{1}, Span Reserve:{2}", sensor.Type.Code, spanReserveThreshold, spanReserve ) );
                return true;
            }
        }
        return false;
    }

    #endregion Methods

} // end-class InstrumentCalibration Operation

#region Exceptions

/// <summary>
/// Exception thrown when the correct calibration gas is unavailable.
/// </summary>
public class CorrectCalibrationGasUnavailable : ApplicationException
{
    /// <summary>
    /// Create an instance of the exception.
    /// </summary>
    public CorrectCalibrationGasUnavailable( string instrumentSerial ) 
        : base( "The correct calibration gas is unavailable. (Instrument " + instrumentSerial + ")." ) {}

    /// <summary>
    /// Create an instance of the exception using the specified error message.
    /// </summary>
    public CorrectCalibrationGasUnavailable( string instrumentSerial, string gasCode )
        : base( "The correct calibration gas is unavailable. (Instrument " + instrumentSerial + ", Gas " + gasCode + ")." ) { }

    /// <summary>
    /// Create an instance of the exception that wraps another exception.
    /// </summary>
    /// <param name="e">The exception to wrap.</param>
    public CorrectCalibrationGasUnavailable( string instrumentSerial, Exception e ) : base( "The correct calibration gas is unavailable. (Instrument " + instrumentSerial + ").", e ) { }
}

/// <summary>
/// Exception thrown when the calibration failed.
/// </summary>
public class FailedCalibrationException : ApplicationException
{
    /// <summary>
    /// Create an instance of the exception.
    /// </summary>
    public FailedCalibrationException() : base( "The calibration failed." ) {}

    /// <summary>
    /// Create an instance of the exception using the specified error message.
    /// </summary>
    public FailedCalibrationException( string msg ) : base( msg ) {}

    /// <summary>
    /// Create an instance of the exception that wraps another exception.
    /// </summary>
    /// <param name="e">The exception to wrap.</param>
    public FailedCalibrationException( Exception e ) : base( "The calibration failed." , e ) {}
}


////////////////////////////////////////////////////////////////////////////////////////////////////
///<summary>
/// Exception thrown when error is encountered when attempting to zero the instrument sensors.
///</summary>	
public class UnableToZeroInstrumentSensorsException : ApplicationException
{

	/// <summary>
	/// Creates a new instance of the UnableToZeroInstrumentSensorsException class. 
	/// </summary>		
    public UnableToZeroInstrumentSensorsException() : base( "Unable to zero the instrument sensors!" ) {}

    /// <summary>
    /// Create an instance of the exception using the specified error message.
    /// </summary>
    public UnableToZeroInstrumentSensorsException( string msg ) : base( msg ) {}

	/// <summary>
	/// Creates a new instance of the UnableToZeroInstrumentSensorsException class by reporting the source of the error.
	/// </summary>
	///<param name="e">Source</param>
    public UnableToZeroInstrumentSensorsException( Exception e ) : base( "Unable to zero the instrument sensors!" , e ) {}
}


#endregion Exceptions

}  // end-namespace