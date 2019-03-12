using System;
using System.Collections.Generic;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Monitors docking station valves and gas flow.
    /// Also determines gas cylinders to be used for calibration & bump.
    /// </summary>
    public sealed class ResourceService : Service
    {
        #region Fields

        /// <summary>
        /// The maximum number of minutes the valve is allowed to be open.
        /// </summary>
        private const int _PERIOD_ALLOWED_OPEN = 30;

        /// <summary>
        /// The minimum number of milliseconds the valve must be open before checking.
        /// </summary>
        private const int _MIN_OPEN_PERIOD = 1000;

        /// <summary>
        /// The minimum number of times to retry opening the cylinder when
        /// flow fails.
        /// </summary>
        private const int _MIN_FLOW_FAILURES = 1;

        private readonly TimeSpan _periodAllowedOpen = new TimeSpan( 0, _PERIOD_ALLOWED_OPEN, 0 );
        private readonly TimeSpan _minOpenPeriod = new TimeSpan( 0, 0, 0, 0, _MIN_OPEN_PERIOD );
        private int _flowFailures;

        private bool _empty = false;
        private bool _expired = false;
        private bool _gasFound = false;
        private bool _gasNeeded = true; // SGF  21-May-2012  INS-3078
        private bool _freshFound = false;
        private bool _zeroFound = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of a ResourceService class.
        /// </summary>
        public ResourceService( Master master )
            : base( master )
        {
            //DelayedStartTime = new TimeSpan( 0, 0, 6 );
            IdleTime = new TimeSpan( 0, 0, 3 );
        }

        #endregion

        #region Methods

        /// <summary>
        /// Monitors the flow rate and adjustes voltage to pump as necessary in
        /// order to maintain desired flowrate.
        /// </summary>
        protected override void Run()
        {
            try
            {
                if ( !Pump.IsRunning() )
                    return;

                ConsoleState state = Master.ConsoleService.CurrentState;
                if ( state == ConsoleState.Diagnosing || state == ConsoleState.InteractiveDiagnostics )
                    return;

                int openedPosition = Pump.GetOpenValvePosition();

                if ( openedPosition > 0 )
                {
                    DateTime openTime = Pump.GetTimePumpStarted();
                    DateTime now = DateTime.UtcNow;

                    Log.Debug( string.Format( "Opened solenoid {0} at {1}", Pump.GetOpenValvePosition(), Log.DateTimeToString( openTime ) ) );

                    Pump.FlowStatus flowStatus = Pump.CheckFlow();

                    if ( flowStatus != Pump.FlowStatus.TooLow )
                    {
                        _flowFailures = 0;
                        if ( ( openTime != DateTime.MinValue ) && ( ( now - openTime ) > _periodAllowedOpen ) )
                            Pump.CloseValve( openedPosition ); // Close the valve.
                    }
                    // Else, assumption is that FlowStatus is TooLow. (empty cylinder?)
                    else if ( ( ( now - openTime ) > _minOpenPeriod )
                    && Pump.IsRunning() )
                    {
                        _flowFailures++;
                        Log.Debug( "Flow failed " + _flowFailures + " times." );

                        if ( _flowFailures >= _MIN_FLOW_FAILURES )
                        {
                            _flowFailures = 0;
                            Pump.CloseValve( openedPosition );
                        }
                        else
                        {
                            Pump.OpenValve( Pump.GetOpenValvePosition(), false );
                        }
                    }
                }
            }
            catch ( Exception e )
            {
                Log.Error( Name, e );
            }
        }

		internal List<GasEndPoint> GetGasEndPoints( string eventCode, DockingStationAction dsAction, StringBuilder explanation, List<string> explanationCodes, List<string> errorCodes )
        {
            Log.Debug( string.Format( "{0}.GetGasEndPoints, {1}", Name, eventCode ) );
            //explanationCodes.Clear();
            //errorCodes.Clear(); // SGF  20-Feb-2013  INS-3821

            ISC.iNet.DS.DomainModel.Instrument dockedInstrument = Master.Instance.SwitchService.Instrument; 
            DockingStation dockingStation = Configuration.DockingStation;

            List<GasEndPoint> gasEndPoints = new List<GasEndPoint>();

            foreach ( InstalledComponent installedComponent in dockedInstrument.InstalledComponents )
            {
                if ( !( installedComponent.Component is Sensor ) )
                    continue;

                Sensor sensor = (Sensor)installedComponent.Component;
                if ( !sensor.Enabled )
                {
                    Log.Info( string.Format( "{0}: Ignoring disabled sensor {1}", Name, sensor.Uid ) );
                    continue;
                }

                // SGF  21-May-2012  INS-3078 -- Comment out the following if statement
                //if (!GasOperationsSupported(sensor))
                //{
                //    Log.Debug( string.Format( "{0}.GasOperationsSupported returned False for sensor {1}. Ignoring sensor.", Name, sensor.SerialNumber ) );
                //    continue;
                //}

                if ( sensor.CalibrationGas.Code.Length == 0 )
                    throw new ApplicationException( "Sensor " + sensor.Uid + " has null calibration gas code." );

                // SGF  03-Nov-2010  Single Sensor Cal and Bump
                if ( dsAction is InstrumentGasAction )
                {
                    InstrumentGasAction gasAction = (InstrumentGasAction)dsAction;
                    if ( gasAction.ComponentCodes.Count != 0 && !gasAction.ComponentCodes.Contains( sensor.Type.Code ) )
                    {
                        Log.Debug( string.Format( "{0}: Component type {1} is not included in the defined list of components to test. Ignoring sensor.", Name, sensor.Type.Code ) );
                        continue;
                    }
                }

                Log.Debug( string.Format( "{0}: Looking for sensor {1}'s cal gas ({2})", Name, sensor.Uid, sensor.CalibrationGas.Code ) );

                _empty = _expired = _gasFound = _freshFound = _zeroFound = false;
                _gasNeeded = true;  // SGF  21-May-2012  INS-3078

                // INS-8630 RHP v7.5 clear the messages for every installed component to avoid confusion
                explanationCodes.Clear();
                errorCodes.Clear(); // SGF  20-Feb-2013  INS-3821

                // Loop thru the cylinders for the docking station and if the cylinder contains the gas that
                // the sensor needs, add that cylinder as a gas end point in the docking station action.
                FindInstalledCylinderGases(eventCode, gasEndPoints, dockingStation, installedComponent, explanation, explanationCodes, errorCodes);

                if ( !_freshFound && !_zeroFound )
                {
                    // Present which type of air should be, but is not, available.  If the port1 restrictions 
                    // only allow for zero air, present 'ZERO AIR'; otherwise, present 'FRESH AIR'.
                    if ( Configuration.DockingStation.Port1Restrictions == PortRestrictions.ZeroAir )
                    {
                        explanationCodes.Add( "ZEROAIR" );
                        errorCodes.Add( string.Format( "{0} ({1})", "ZEROAIR", GasCode.O2 ) ); // SGF  20-Feb-2013  INS-3821
                    }
                    else if ( Configuration.DockingStation.Port1Restrictions == PortRestrictions.FreshAir )
                    {
                        GasType gasType = GasType.Cache[GasCode.FreshAir];
                        if ( gasType != null )
                        {
                            explanationCodes.Add( gasType.Symbol );
                            errorCodes.Add( string.Format( "{0} ({1})", gasType.Symbol, gasType.Code ) ); // SGF  20-Feb-2013  INS-3821
                        }
                    }

                    else // SGF  19-Jan-2012  INS-1913 & INS-1914
                    {
                        // either fresh air or zero air is allowed; present which type is connected, if something is connected.
                        GasEndPoint gasEndPoint = dockingStation.GasEndPoints[0];
                        Cylinder cyl = gasEndPoint.Cylinder;
                        if ( cyl.IsZeroAir )
                        {
                            explanationCodes.Add( "ZEROAIR" );
                            errorCodes.Add( string.Format( "{0} ({1})", "ZEROAIR", GasCode.O2 ) ); // SGF  20-Feb-2013  INS-3821
                        }
                        else //suresh 14-Mar-2012 INS-4427 (DEV) 
                        {
                            // If port 1 cylinder is not Zero Air then we report that 'Fresh air' is unavailable
                            GasType gasType = GasType.Cache[GasCode.FreshAir];
                            if ( gasType != null )
                            {
                                explanationCodes.Add( gasType.Symbol );
                                errorCodes.Add( string.Format( "{0} ({1})", gasType.Symbol, gasType.Code ) ); // SGF  20-Feb-2013  INS-3821
                            }
                        }
                    }

                    if (_expired)
                    {
                        explanationCodes.Add("Expired");
                        errorCodes.Add("Expired");  // INS-8630 RHP v7.5 - Notify iNet on the expired state 
                    }
                    else if (_empty)
                    {
                        explanationCodes.Add(PressureLevel.Empty.ToString());
                        errorCodes.Add(PressureLevel.Empty.ToString());     // INS-8630 RHP v7.5 - Notify iNet on the empty state 
                    }
                    explanation.Append( "Fresh air not found for sensor " + sensor.Uid + '\n' );

                    Log.Debug( string.Format( "{0}: Returning nothing: gasFound={1}, freshFound={2}, expired={3}, empty={4}", Name, _gasFound, _freshFound, _expired, _empty ) );
					return new List<GasEndPoint>();
                }

                if ( _gasNeeded && !_gasFound )  // SGF  21-May-2012  INS-3078 -- add the '_gasNeeded' clause to the if-condition
                {
                    // If gas not found, IDS needs the symbol for that gas for display on its LCD.
                    // Look it up in our cache.  For Fresh Air, we just return the gas code;
                    // The IDS knows to look for that as a special case.
                    if ( sensor.CalibrationGas.Code == GasCode.FreshAir )
                    {
                        GasType gasType = GasType.Cache[GasCode.FreshAir];
                        if ( gasType != null )
                        {
                            explanationCodes.Add( gasType.Symbol );
                            errorCodes.Add( string.Format( "{0} ({1})", gasType.Symbol, gasType.Code ) ); // SGF  20-Feb-2013  INS-3821
                        }
                    }
                    // DSW-1758 RHP v9.6.1 - Added (! (_expired || _empty) ) since gas symbol has already been added to explanationCodes for Empty/Expired states.
                    else if (!(_expired || _empty))                      
                    {
                        // If we're doing a bump test, and this is a combustible sensor either in LEL or PPM mode,
                        // and docking station has a CombustibleBumpTestGas setting, then make sure we report that the
                        // gas type not found is the CombustibleBumpTestGas and not the sensor cal gas.
						string sensorGasCode = sensor.CalibrationGas.Code;
						if ( ( eventCode == EventCode.BumpTest )
                        &&   ( sensor.Type.Code == SensorCode.CombustibleLEL || sensor.Type.Code == SensorCode.CombustiblePPM )
					    &&   ( Configuration.DockingStation.CombustibleBumpTestGas.Length > 0 ) )
						{
							sensorGasCode = Configuration.DockingStation.CombustibleBumpTestGas;
						}
                        GasType gasType = GasType.Cache[sensorGasCode];
                        if ( gasType != null )
                        {                          
                            explanationCodes.Add( gasType.Symbol );
                            errorCodes.Add( string.Format( "{0} ({1})", gasType.Symbol, gasType.Code ) ); // SGF  20-Feb-2013  INS-3821
                        }
                    }

                    if (_expired)
                    {
                        explanationCodes.Add("Expired");
                        errorCodes.Add("Expired");  // INS-8630 RHP v7.5 - Notify iNet on the expired state 
                    }
                    else if (_empty)
                    {
                        explanationCodes.Add(PressureLevel.Empty.ToString());
                        errorCodes.Add(PressureLevel.Empty.ToString());     // INS-8630 RHP v7.5 - Notify iNet on the empty state 
                    }

                    explanation.Append( "Could not find cylinder needed for sensor " + sensor.Uid + ", CalGasCode=\"" + sensor.CalibrationGas.Code + "\" (" );
                    for ( int i = 0; i < explanationCodes.Count; i++ )
                    {
                        if ( i > 0 ) explanation.Append( " " );
                        explanation.Append( explanationCodes[i] );
                    }
                    explanation.Append( ")\n" );

                    Log.Debug( string.Format( "{0}: Returning nothing: gasFound={1}, freshFound={2}, expired={3}, empty={4}", Name, _gasFound, _freshFound, _expired, _empty ) );
					return new List<GasEndPoint>();
                }

                // Zero air is required for CO2 sensors; Only zero air is used to zero CO2, never fresh air.
                if ( ( sensor.CalibrationGas.Code == GasCode.CO2 ) && !_zeroFound )
                {
                    GasType gasType = GasType.Cache[GasCode.O2];
                    if ( gasType != null )
                    {
                        explanationCodes.Add( "ZEROAIR" ); // SGF  5-Feb-2013  INS-3837
                        errorCodes.Add( string.Format( "{0} ({1})", "ZEROAIR", GasCode.O2 ) ); // SGF  20-Feb-2013  INS-3821
                    }
                    if ( _expired )
                        explanationCodes.Add( "Expired" );
                    else if ( _empty )
                        explanationCodes.Add( PressureLevel.Empty.ToString() );
                    explanation.Append( "Zero air not found for CO2 sensor " + sensor.Uid + '\n' );
                    Log.Debug( string.Format( "{0}: Returning nothing: gasFound={1}, freshFound={2}, expired={3}, empty={4}", Name, _gasFound, _freshFound, _expired, _empty ) );
					return new List<GasEndPoint>(); ;
                }
            }

            Log.Debug( string.Format( "{0}.GetGasEndPoints returned {1} gas end points", Name, gasEndPoints.Count ) );
            return gasEndPoints;
        }

        // INS-8630 RHP v7.5 - Updated method signature to include errorCodes and explanationCodes to display ISC Cylinder Part Number on LCD and to send the same as Alert
		private void FindInstalledCylinderGases( string eventCode, List<GasEndPoint> gasEndPoints,
                                                DockingStation dockingStation, InstalledComponent installedComponent,
                                                StringBuilder explanation,
                                                List<string> explanationCodes,
                                                List<string> errorCodes)
        {
            Sensor sensor = installedComponent.Component as Sensor;

            // INS-8630 RHP v7.5 - Initialize outside the loop since if no gas is found on entire loop, then we still need to pass expired/empty if either are true.
            _expired = _empty = false;  

            // SGF  21-May-2012  INS-3078
            _gasNeeded = IsGasOperationSupported( eventCode, sensor );

            foreach ( GasEndPoint gasEndPoint in dockingStation.GasEndPoints )
            {
                Cylinder cyl = gasEndPoint.Cylinder;

                // If we are examining the fresh air/zero air port to possibly add a gas to the gas end points list,
                // make sure that the type of fresh air/zero air is allowed based on port 1 descriptions.  For example,
                // if the installed cylinder is fresh air, make sure the port 1 restrictions allow for fresh
                // air to be specified on this port.
                //
                if ( gasEndPoint.Position == Controller.FRESH_AIR_GAS_PORT )
                {
                    if ( cyl.IsFreshAir && ( ( dockingStation.Port1Restrictions & PortRestrictions.FreshAir ) != PortRestrictions.FreshAir ) )
                    {
                        Log.Debug( Name + ": FRESH AIR attached to Port 1; defined Port 1 Restrictions prohibit the use of FRESH AIR" );
                        continue;
                    }

                    if ( cyl.IsZeroAir && ( ( dockingStation.Port1Restrictions & PortRestrictions.ZeroAir ) != PortRestrictions.ZeroAir ) )
                    {
                        Log.Debug( Name + ": ZERO AIR attached to Port 1; defined Port 1 Restrictions prohibit the use of ZERO AIR" );
                        continue;
                    }
                }

                // Reset each time through loop. At end of loop, they'll be
                // set for the last cylinder matching desired gas that was looked at.
                //_expired = _empty = false; - INS-8630 RHP v7.5 Commented since if no gas is found on entire loop, then we still need to pass expired/empty if either are true.
                bool alreadyAdded = false;

                string msg = string.Format( "{0} Examining port {1} ({2}), fid={3},pn={4},Fresh={5},ZeroAir={6},Exp={7},Pressure={8}",
                             Name, gasEndPoint.Position, gasEndPoint.InstallationType.ToString(), cyl.FactoryId, cyl.PartNumber, cyl.IsFreshAir, cyl.IsZeroAir,
                             cyl.ExpirationDate.ToShortDateString(), cyl.Pressure.ToString() );
                Log.Debug( msg );

                msg = "......";
                foreach ( GasConcentration gasCon in gasEndPoint.Cylinder.GasConcentrations )
                {
                    msg += "[" + gasCon.Type.Code + " ";
                    msg += ( gasCon.Concentration == double.MinValue ) ? "fresh" : gasCon.Concentration.ToString();
                    msg += "]";
                }
                Log.Debug( msg );

                string sensorGasCode = sensor.CalibrationGas.Code;

                // If we're doing a bump test, we use Chlorine for the calibration gas.
                if (sensor.Type.Code == SensorCode.ClO2 && eventCode == EventCode.BumpTest)
                {
                    sensorGasCode = GasCode.Cl2;
                    Log.Debug("...Sensor is CLO2. Looking for cylinders containing Chlorine instead of cal gas.");
                }         

                bool containsGas = gasEndPoint.Cylinder.ContainsGas(sensorGasCode);

                if ( Configuration.DockingStation.UseExpiredCylinders && ( eventCode == EventCode.BumpTest ) )
                    Log.Debug( "...UseExpiredCylinders=true; Ignoring expiration date" );
                else if ( Configuration.ToLocalTime(gasEndPoint.Cylinder.ExpirationDate) <= Configuration.GetLocalTime() )
                {
                    //Log.Debug( "...Skipping expired cylinder" );
                    explanation.Append( "Cylinder has expired." + '\n' );
                    explanation.Append( "Cylinder id =" + gasEndPoint.Cylinder.FactoryId + '\n' );
                    explanation.Append( "Port number: " + gasEndPoint.Position + '\n' );

                    // INS-8630 RHP v7.5 - IDS is expected to display expired cylinder details on LCD
                    if (containsGas)
                    {
                        // INS-8630 RHP v7.5 - Multiple cylinder with the same gas code may be expired, in such scenarios IDS message 
                        // should display the first cylinder it identifies.
                        if (!(_expired || _empty))
                        {
                            // For ISC cylinder IDS should display the cylinder part NUmber, so add the part number to explanation codes
                            if (!explanationCodes.Contains(gasEndPoint.Cylinder.PartNumber) && !string.IsNullOrEmpty(gasEndPoint.Cylinder.PartNumber))
                            {
                                explanationCodes.Add(gasEndPoint.Cylinder.PartNumber);
                                errorCodes.Add(string.Format("{0} ({1})", gasEndPoint.Cylinder.PartNumber, gasEndPoint.Position)); // see INS-3821
                            }
                            else
                            {
                                // For Non ISC Pass the gas code. But I believe that both ISC and Non-ISC cylinders have their own PArt numbers.
                                // So this condition may not be required ?
                                GasType gs = GasType.Cache[GasCode.FreshAir];
                                if (!explanationCodes.Contains(gasEndPoint.Cylinder.PartNumber) && gs != null)
                                {
                                    explanationCodes.Add(gs.Symbol);
                                    errorCodes.Add(string.Format("{0} ({1})", gs.Symbol, gs.Code));
                                }
                            }
                            _expired = true;
                        }
                        continue;
                    }
                }

                if ( gasEndPoint.Cylinder.Pressure == PressureLevel.Empty )
                {
                    //Log.Debug( "...Skipping cylinder; Pressure is " + gasEndPoint.Cylinder.Pressure );
                    explanation.Append( "Cylinder is empty." + '\n' );
                    explanation.Append( "Cylinder id =" + gasEndPoint.Cylinder.FactoryId + '\n' );
                    explanation.Append( "Port number: " + gasEndPoint.Position + '\n' );

                    // INS-8630 RHP v7.5 - IDS is expected to display empty cylinder details on LCD
                    if (containsGas)
                    {
                        // INS-8630 RHP v7.5 - Multiple cylinder with the same gas code may be expired, in such scenarios IDS message 
                        // should display the first cylinder it identifies.
                        if (!(_expired || _empty))
                        {
                            // For ISC cylinder IDS should display the cylinder part NUmber, so add the part number to explanation codes
                            if (!explanationCodes.Contains(gasEndPoint.Cylinder.PartNumber) && !string.IsNullOrEmpty(gasEndPoint.Cylinder.PartNumber))
                            {
                                explanationCodes.Add(gasEndPoint.Cylinder.PartNumber);
                                errorCodes.Add(string.Format("{0} ({1})", gasEndPoint.Cylinder.PartNumber, gasEndPoint.Position));  // see INS-3821
                            }
                            else
                            {
                                // For Non ISC Pass the gas code. But I believe that both ISC and Non-ISC cylinders have their own PArt numbers.
                                // So this condition may not be required ?
                                GasType gs = GasType.Cache[GasCode.FreshAir];
                                if (!explanationCodes.Contains(gasEndPoint.Cylinder.PartNumber) && gs != null)
                                {
                                    explanationCodes.Add(gs.Symbol);
                                    errorCodes.Add(string.Format("{0} ({1})", gs.Symbol, gs.Code));
                                }
                            }
                            _empty = true;
                        }
                        continue;
                    }
                }

                //string sensorGasCode = sensor.CalibrationGas.Code;

                if ( eventCode == EventCode.BumpTest )
                {
                    // If we're doing a bump test, and this is a combustible sensor either in LEL or PPM mode,
                    // and docking station has a CombustibleBumpTestGas setting, then ignore the sensor's cal
                    // gas code and instead only look for cylinders that match the CombustibleBumpTestGas setting.
                    if ( ( sensor.Type.Code == SensorCode.CombustibleLEL || sensor.Type.Code == SensorCode.CombustiblePPM )
                    && ( Configuration.DockingStation.CombustibleBumpTestGas.Length > 0 ) )
                    {
                        sensorGasCode = Configuration.DockingStation.CombustibleBumpTestGas;
                        Log.Debug( string.Format( "...Sensor is combustible and CombustibleBumpTestGas setting is {0}.", sensorGasCode ) );
                        Log.Debug( string.Format( "...Overriding sensor cal gas. Looking for cylinders containing {0}.", sensorGasCode ) );
                    }
                    // If we're doing a bump test, we use Chlorine for the calibration gas.
                    else if ( sensor.Type.Code == SensorCode.ClO2 )
                    {
                        sensorGasCode = GasCode.Cl2;
                        Log.Debug( "...Sensor is CLO2. Looking for cylinders containing Chlorine instead of cal gas." );
                    }
                }

                containsGas = gasEndPoint.Cylinder.ContainsGas( sensorGasCode );

				// Ensure bump tests for O2 sensors use concentrations of 19% O2 or less.
				// Zero air cylinders should not be selected for gasFound, but should be selected for zeroFound.
				if ( containsGas && ( eventCode == EventCode.BumpTest && sensor.CalibrationGas.Code == GasCode.O2 ) )
				{
					// The following Find should always succeed because containsGas is true.
					GasConcentration gasConcentration = cyl.GasConcentrations.Find( gc => gc.Type.Code == sensorGasCode );
					// Parts per million (ppm) of X divided by 10,000 = percentage concentration of X
					if ( ( gasConcentration.Concentration / 10000.0 ) > 19.0 )
					{
						Log.Debug( "...Not allowed to use O2 concentration higher than 19%." );
                       
						containsGas = false;
					}
				}
				
                // Ensure cylinder concentration is not higher than 60% for potentially explosive cal gases
				double lelMultiplier = GasType.Cache[sensorGasCode].LELMultiplier;
				if ( containsGas && lelMultiplier > 0.0 )
                {
                    double cylinderPPM = -1.0;
                    foreach ( GasConcentration gasConcentration in cyl.GasConcentrations )
                    {
                        if ( gasConcentration.Type.Code == sensorGasCode )
                        {
                            cylinderPPM = gasConcentration.Concentration;
                            break;
                        }
                    }

                    if ( cylinderPPM < 0 ) // this should never happen.  Which means we better check anyways.
                    {
						Log.Debug( "...Skipping cylinder. Does not contain " + sensorGasCode );
                        continue;
                    }

					double cylinderLEL = cylinderPPM * lelMultiplier;
					if ( cylinderLEL > 60.0 )  // cylinder is higher than 60%?  Don't use it.
					{
					    Log.Debug( string.Format( "...Skipping cylinder. Contains {0} with too high LEL concentration ({1}%)",
                            sensorGasCode, Math.Round( cylinderLEL, 1 ) ) );
					    Log.Debug( "...Not allowed to use LEL concentration higher than 60%." );
					    continue;
					}
                }
				
                // The gas is found if the cylinder contains a gas with a matching code, or
                // if the cylinder is fresh air and the sensor is an O2 sensor.
                // Fresh air is not acceptable as O2 if doing a bump test. 
				// Zero air is not acceptable for an O2 bump test.
                if ( containsGas || ( ( sensor.CalibrationGas.Code == GasCode.O2 ) && !( eventCode == EventCode.BumpTest ) && gasEndPoint.Cylinder.IsFreshAir ) )
                {
                    Log.Debug( "...Gas found." );
					if ( HasCylinder( gasEndPoint, gasEndPoints ) == false )
						gasEndPoints.Add( gasEndPoint );
                    _gasFound = true;
                    alreadyAdded = true;
                }

                // Nitrogen is acceptable for doing O2 bump tests.
                if ( sensor.CalibrationGas.Code == GasCode.O2
                && ( eventCode == EventCode.BumpTest )
                && gasEndPoint.Cylinder.ContainsOnlyGas( GasCode.N2 ) == true )
                {
                    Log.Debug( "...Gas found." );
					if ( HasCylinder( gasEndPoint, gasEndPoints ) == false )
						gasEndPoints.Add( gasEndPoint );
                    _gasFound = true;
                }

                if ( gasEndPoint.Cylinder.IsFreshAir )
                {
                    Log.Debug( "...Fresh air found." );
                    if ( !alreadyAdded )
                    {
						if ( HasCylinder( gasEndPoint, gasEndPoints ) == false )
							gasEndPoints.Add( gasEndPoint );
                    }
                    _freshFound = true;
                    alreadyAdded = true;
                }

                if ( gasEndPoint.Cylinder.IsZeroAir )
                {
                    Log.Debug( "...Zero Air found." );
                    if ( !alreadyAdded )
                    {
						if ( HasCylinder( gasEndPoint, gasEndPoints ) == false )
							gasEndPoints.Add( gasEndPoint );
                    }
                    _zeroFound = true;
                    alreadyAdded = true;
                }
            }   // end-foreach  Gasendpoints

            // BEGIN INS-8630 RHP v7.5 - Loop through the entire list of installed cylinders.
            // if _gasFound is set to true, then the desired gas is found and hence we can reset _expired and _empty.
            // There may be cases where a docking station may have more than one gas code of the same kind assigned to it as a part of installed cylinders and
            // put of which one of the cylinders may be expired/empty and other may be full.
            if (_empty && _gasFound)
                _empty = false;
            else if (_expired && _gasFound)
                _expired = false;
            // END INS-8630 RHP v7.5
        }

        /// <summary>
        /// Determines if passed-in GasEndPoints list already contains the passed-in cylinder.
        /// <para>
        /// (Matches on cylinder position and part number.)
        /// </para>
        /// </summary>
        /// <param name="candidateCylinder"></param>
        /// <param name="gasEndPoints"></param>
        /// <returns></returns>
		private bool HasCylinder( GasEndPoint candidateCylinder, List<GasEndPoint> gasEndPoints )
        {
			GasEndPoint foundEndPoint = gasEndPoints.Find( g => g.Position == candidateCylinder.Position
                                                           && g.Cylinder.PartNumber == candidateCylinder.Cylinder.PartNumber );
            return foundEndPoint != null;
        }

        /// <summary>
        /// We do not support gas operations for CLO2 or O3 (ozone).
        /// <para>
        /// CLO2 bumps are supported, though, if a CL2 cylinder is attached.
        /// </para>
        /// </summary>
        /// <param name="eventCode">The operation type.</param>
        /// <param name="sensor">Sensor to check</param>
        /// <returns>true if gas ops supported on this sensor, false if not</returns>
        private bool IsGasOperationSupported( string eventCode, Sensor sensor )
        {
            // Check for gases that are not supported; if one is found, return false
            if ( sensor.CalibrationGas.Code == GasCode.ClO2 )
            {
                // For CLO2, only bump tests are supported.
                if ( eventCode != EventCode.BumpTest )
                    return false;

                // For CLO2, bump test is only supported if a Chlorine cylinder is installed.
                return Configuration.DockingStation.GasEndPoints.Find( g => g.Cylinder.ContainsGas( GasCode.Cl2 ) ) != null;
            }

            if ( sensor.CalibrationGas.Code == GasCode.O3 )
                return false;

            return true; // Gas operations are supported on this sensor; return true
        }

        #endregion

    }

}
