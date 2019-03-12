using System;
using System.Runtime.InteropServices;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS
{
    /// <summary>
    /// Responsible for controlling and obtaining status of the DS2's
    /// pump and also its solenoids.
    /// </summary>
    public class Pump
    {
        #region Fields

        public const int FLOWRATE_TOLERANCE = 5;

        /// <summary>
        /// The number of milliseconds to wait before turning on/off a pump when opening
        /// a local valve.
        /// </summary>
        private const int CLOSE_VALVE_WAIT = 500;
        private const int OPEN_VALVE_WAIT = 100;

        private static volatile int _openPosition;

        /// <summary>
        /// The GasEndPoint opened via a call to OpenGasEndPoint().
        /// </summary>
        private static GasEndPoint _openEndPoint = null;

        private static volatile bool _doCheckFlow;
        private static volatile bool _isDesiredFlowRate;
        private static DateTime[] _valveOpenTime;

        private static DateTime _pumpStartedTime;
        private static DateTime _pumpLastStartedTime;

        private static volatile int _desiredFlowRate; // used in CheckFlow. Set by SetDesiredFlow.

        private static DateTime _lastCheckFlowTime = DateTime.MinValue;

        // Set to true when CheckFlow begins, then set back to false when CheckFlow ends.
        // It's important that only CheckFlow set this variable!
        private static volatile bool _checkingFlow;

        //Set to true when CheckFlow method detects that flow is inaccurate, voltage is greater than 200, and vacuum is less than 6 inches
        //Else set to false
        private static bool _isBadPumpTubing = false;

        private static object _pumpLock = new object();

        #endregion Fields

        #region Constructors

        static Pump()
        {
            _pumpStartedTime = _pumpLastStartedTime = DateTime.MinValue;
            _valveOpenTime = new DateTime[ Configuration.DockingStation.NumGasPorts ];
            _doCheckFlow = true;

            for ( int n = 0; n < _valveOpenTime.Length; n++ )
                _valveOpenTime[ n ] = DateTime.MinValue;

            SetDesiredFlow( StandardFlowRate );
        }

        /// <summary>
        /// Private constructor.  This class is intended to only be used statically.
        /// </summary>
        private Pump() { }

        #endregion Constructors

        #region Properties
        /// <summary>
        /// The standard flow rate (ml/min) to be used for gas operations.
        /// Per decision of engineering.
        /// </summary>
        /// <remarks>
        /// This value is the decision of engineering.
        /// It's to give enough flow to calibrate instrument at all temperature ranges,
        /// and to ensure that flow rate never goes under 500ml/min due to temperature or
        /// other problems.
        /// See dev jira DSHW-153.
        /// </remarks>
        public static int StandardFlowRate { get { return 550; } }

        /// <summary>
        /// The number of flow sensor A2D counts that equal the StandardFlowRate. 
        /// </summary>
        public static int StandardFlowCounts
        {
            get
            {
                if ( Configuration.DockingStation.Reservoir )  // Viper / iNetDS ?
                    return 243; // 243 A2D Counts = 793mv = 550ml/min

                return 419; // Burton only.
            }
        }  

        /// <summary>
        /// Returns a voltage for the pump that should get flow rate very close to the StandardFlowRate.
        /// CheckFlow will then increase/decrease the pump as needed to adjust the flow rate so that
        /// it's even closer to the StandardFlowRate.
        /// </summary>
        /// <remarks>
        /// The value returned is different based on DeviceType due to differences in the internal 
        /// plumbing of those various types.
        /// </remarks>
        public static int StandardStartVoltage
        {
            get
            {
                if ( Configuration.DockingStation.Reservoir )  // "Viper" board
                {                    
					if ( Configuration.DockingStation.Type == DeviceType.MX4 )
						return 83;

                    if ( Configuration.DockingStation.Type == DeviceType.MX6 )
                        return 85;

					if ( Configuration.DockingStation.Type == DeviceType.TX1 )
						return 73;   

                    if ( Configuration.DockingStation.Type == DeviceType.GBPRO || Configuration.DockingStation.Type == DeviceType.GBPLS )
                        return 78;
                }
                else  // "Burton" board
                {
					if ( Configuration.DockingStation.Type == DeviceType.MX4 )
						return ( Configuration.DockingStation.PartNumber == Configuration.VENTISLS_PARTNUMBER ) ? 89 : 103; 

                    if ( Configuration.DockingStation.Type == DeviceType.MX6 )
                        return 90;

					if ( Configuration.DockingStation.Type == DeviceType.TX1 )
						return 86;

                    if ( Configuration.DockingStation.Type == DeviceType.GBPRO || Configuration.DockingStation.Type == DeviceType.GBPLS )
                        return 100;

                    if ( Configuration.DockingStation.Type == DeviceType.SC )
                        return 95;
                }
                return ( MaxVoltage - MinVoltage ) / 3;
            }
        }

        public static int MaxFlowRate { get { return 1000; } }

        public static int MinFlowRate { get { return 0; } }

        /// <summary>
        /// </summary>
        public static int MaxVoltage { get { return 255; } }

        /// <summary>
        /// </summary>
        public static int MinVoltage { get { return 0; } }

        /// <summary>
        /// AJAY INS-8283: Docking station should try and detect when gas hosing is blocked or kinked.
        /// Returns true if inaccruate flow detected along with voltage being greater than 200 and vacuum is less than 6 inches
        /// INETQA-4166 RHP v7.6 "Viper" board always returns False
        /// </summary>
        public static bool IsBadPumpTubing
        {
            get { return Configuration.DockingStation.Reservoir ? false : _isBadPumpTubing; }
            set { _isBadPumpTubing = value; }
        }

        #endregion Properties

        #region DllImport externs

        /// <summary>
        /// IDS API: Turns the pump on or off.
        /// </summary>
        /// <param name="data">1 turns on the pump, 0 turns it off.</param>
        /// <returns>
        /// This call only returns an error (non-zero) if an invalid "pumpState" parameter is passed to it.
        /// So, as long as software calling this routine passes valid parameter, there is no reason to check for failure.
        /// </returns>
        [DllImport( "sdk.dll" )]
        private static extern int SetPumpState( byte pumpState );

        /// <summary>
        /// IDS API: Gets a value indicating whether the pump is on or not.
        /// </summary>
        /// <param name="pumpState">1 is returned through this parameter if pump is on. 0 is returned if pump is off.</param>
        /// <returns>This call always returns 0.  Therefore, there is no reason to check for failure.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetPumpState( byte* pumpState );

        /// <summary>
        /// IDS API: Gets the pump error state.
        /// </summary>
        /// <param name="errState">1 is returned through this parameter if pump is in an error state, else 0 is returned.</param>
        /// <returns>This call always returns 0.  Therefore, there is no reason to check for failure.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetPumpErrState( byte* errState );

        /// <summary>
        /// IDS API: Gets the vacuum error state.
        /// </summary>
        /// <param name="errState">0 is returned through this parameter if pressure higher than 80" (error). 1 is returned if pressure is OK (no error).</param>
        /// <returns>This call always returns 0.  Therefore, there is no reason to check for failure.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetVacuumErrState( byte* errState );

        /// <summary>
        /// IDS API: Sets the pump voltage.
        /// </summary>
        /// <param name="pumpVoltage">Valid range is 0 to 255.</param>
        /// <returns>0 for success, 1 for failure.</returns>
        [DllImport( "sdk.dll" )]
        private static extern int SetPumpVoltage( byte pumpVoltage );

        /// <summary>
        /// IDS API: Returns the pump voltage.
        /// </summary>
        /// <param name="pumpVoltage"></param>
        /// <returns>This call always returns 0.  Therefore, there is no reason to check for failure.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetPumpVoltage( byte* pumpVoltage );

        /// <summary>
        /// IDS API: Sets solenoid state.
        /// </summary>
        /// <param name="solId">The solenoid to change the state on (1 through <see cref="Controller.MAX_GAS_PORTS"/>.</param>
        /// <param name="state">1 opens the specified solenoid (all others will be automatically closed. 0 closes the specified solenoid.</param>
        /// <returns>
        /// This call only returns an error (non-zero) if invalid parameteres are passed to it.
        /// So, as long as software calling this routine passes valid parameters, there is no reason to check for failure.
        /// </returns>
        [DllImport( "sdk.dll" )]
        private static extern int SetSolenoidState( byte solId, byte state );

        /// <summary>
        /// IDS API: Returns the raw "A2D counts" reported by the flow sensor.
        /// </summary>
        /// <remarks>
        /// Note that this call takes around 850ms to complete. 
        /// This is because it takes multiple samples from the flow sensor
        /// and then returns an average of them.
        /// </remarks>
        /// <param name="reservoir">
        /// Specify 1 if the docking station has an internal reservoir (Viper/iNetDS).
        /// Specify 0 if there is no internal reservoir (Burton/DSX)</param>
        /// <param name="flowCounts"></param>
        /// <returns>0 for success.  Non-zero on error.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetFlow( short reservoir, ushort* flowCounts );

        /// <summary>
        /// IDS API: Returns the raw "A2D counts" reported by the vacuum sensor.
        /// </summary>
        /// <remarks>
        /// Note that this call takes around 850ms to complete. 
        /// This is because it takes multiple samples from the flow sensor
        /// and then returns an average of them.
        /// </remarks>
        /// <param name="vacuumCounts"></param>
        /// <returns>0 for success.  Non-zero on error.</returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetVacuum( ushort* vacuumCounts );

        #endregion DllImport externs

        #region Methods

        /// <summary>
        /// Opens the valve associated with the gas end point, and starts the pump.
        /// </summary>
        /// <param name="endPoint">The gas end point to open.</param>
        public static void OpenGasEndPoint( GasEndPoint endPoint )
        {
            Log.Assert( endPoint != null, "endPoint cannot be null" );

            lock ( _pumpLock )
            {
                // Always open the valve before starting the pump.
                OpenValve( endPoint.Position, false );

                _openEndPoint = (GasEndPoint)endPoint.Clone();

                Thread.Sleep( OPEN_VALVE_WAIT );

                Start(); // Start the pump.
            }
        }

        /// <summary>
        /// Stops the pump,then closes the valve associated with the gas end point.
        /// Does nothing if passed-in endpoint is null.
        /// </summary>
        /// <param name="endPoint">The end point to close.</param>
        public static void CloseGasEndPoint( GasEndPoint endPoint )
        {
            // To these successive hardware calls within critical section. This is
            // to prevent problems where CheckFlow routine (being called by another thread)
            // from being confused as to whether endpoint is closed or not
            // because it sees pump is stopped, but not the valve (or vice versa).
            // See the similar critical section with in CheckFlow routine.
            lock ( _pumpLock )
            {
                if ( endPoint == null )
                {
                    _openEndPoint = null;
                    return;
                }

                Stop();  //	Stop the pump.

                // Pause before closing the port, to ensure the pump stops before we close.
                Thread.Sleep( CLOSE_VALVE_WAIT );

                CloseValve( endPoint.Position, false ); // Close the valve.

                Thread.Sleep( CLOSE_VALVE_WAIT );

                _openEndPoint = null;
            }

            // Wait for CheckFlow (being executed in ResourceService thread) to finish.
            // It should ALWAYS finish in a couple seconds once it detects we've just stoppped
            // the pump and closed the valve.  As a saftely precaution, though, we wait
            // for no more than 10 seconds to prevent any sort of infinite loop.
            if ( _checkingFlow )
            {
                for ( int waitAttempts = 1; waitAttempts < 100; waitAttempts++ )
                {
                    Log.Debug( "CloseGasEndPoint: waiting for CheckFlow to finish." );
                    Thread.Sleep( 100 );
                    if ( _checkingFlow == false )
                    {
                        Log.Debug( "CloseGasEndPoint: CheckFlow appears to have finished." );
                        return;
                    }
                }
                Log.Warning( "CloseValve: The wait for CheckFlow to finish has timed out." );
            }
        }

        /// <summary>
        /// Opens a valve of a docking station with provided IP address and position ID of the valve.
        /// </summary>
        /// <param name="id">Position ID of the valve to be opened</param>
        /// <param name="startPump">Indicates whether to start the pump or not</param>
        public static void OpenValve( int id, bool startPump )
        {
            Log.Debug( string.Format( "OpenValve({0},{1})", id, startPump ) );

            // If the position ID is valid, then open the valve.
            if ( id < 1 || id > Configuration.DockingStation.NumGasPorts )
            {
                Log.Error( "OpenValue: illegal port number: " + id );
                return;
            }

            const byte PUMP_INITIAL_VOLTAGE = 30;

            lock ( _pumpLock )
            {
                _openPosition = id;

                // Open the valve before starting the pump.
                _valveOpenTime[ id - 1 ] = DateTime.UtcNow;

                // Always open the solenoid before turning on the pump.
                SetSolenoidState( Convert.ToByte( id ), 1 );
                Thread.Sleep( OPEN_VALVE_WAIT );

                if ( startPump ) // Supposed to start the pump, too?  Then start it.
                    Start( PUMP_INITIAL_VOLTAGE );
            }
        }

        /// <summary>
        /// Closes a valve of a docking station with provided position ID of the valve.
        /// The pump is also stopped.
        /// </summary>
        /// <param name="ipAddress">IP address of the docking station that its valve is to be closed</param>
        /// <param name="id">Position ID of the valve to be closed</param>
        public static void CloseValve( int id )
        {
            CloseValve( id, true ); // Close the valve and stop the pump.
        }

        /// <summary>
        /// Closes a valve of a docking station with provided position ID of the valve.
        /// </summary>
        /// <param name="id">Position ID of the valve to be closed</param>
        /// <param name="stopPump">Indicates whether to stop the pump or not</param>
        public static void CloseValve( int id, bool stopPump )
        {
            Log.Debug( string.Format( "CloseValve({0},{1})", id, stopPump ) );

            if ( id < 1 || id > Configuration.DockingStation.NumGasPorts )  // Verify valid valve ID was specified.
            {
                Log.Error( "CloseValve: illegal port number: " + id );
                return;
            }

            lock ( _pumpLock )
            {
                _openPosition = 0;

                // Always stop the pump before closing the solenoid.
                if ( stopPump )
                {
                    SetPumpState( 0 );

                    _pumpStartedTime = DateTime.MinValue;
                }

                SetSolenoidState( Convert.ToByte( id ), 0 ); // Close the valve.

                _valveOpenTime[ id - 1 ] = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Closes all of the docking station's valves.
        /// </summary>
        /// <param name="stopPump">Indicates whether to stop the pump or not</param>
        public static void CloseAllValves( bool stopPump )
        {
            lock ( _pumpLock )
            {
                for ( int i = 1; i <= Configuration.DockingStation.NumGasPorts; i++ )
                    Pump.CloseValve( i, stopPump );
            }
        }

        /// <summary>
        /// Relieves the pressure in the tubing of the docking station.
        /// </summary>
        public static void RelieveInternalPressure()
        {
            //	Open valve for a brief period to relieve pressure.
            if ( !Pump.IsRunning() && ( Pump.GetOpenValvePosition() == 0 ) )
            {
                Log.Debug( "Relieving pressure." );

                bool originalDoCheckFlow = DoCheckFlow; // save current value

                DoCheckFlow = false; // We don't want CheckFlow to do anything while relieving the pressure

                Pump.OpenValve( 1, true );

                Thread.Sleep( 2000 );

                Pump.CloseValve( 1, true );

                DoCheckFlow = originalDoCheckFlow; // restore original value
            }
        }

        /// <summary>
        /// Gets the  UTC time that the pump was started for current operation.
        /// </summary>
        /// <returns>
        /// UTC time that the pump was started.
        /// MinValue if pump has been turned off.
        /// </returns>
        public static DateTime GetTimePumpStarted()
        {
            return _pumpStartedTime;
        }

        /// <summary>
        /// Gets the  date/time that the pump was last started.
        /// </summary>
        /// <returns>UTC tme that the pump was last started</returns>
        public static DateTime GetTimePumpLastStarted()
        {
            return _pumpLastStartedTime;
        }

        /// <summary>
        /// Returns the position of the currenently opened valve (1 through MAX_GAS_PORTS).
        /// 0 is returned if all ports are closed.
        /// </summary>
        /// <returns>The position of the valve open</returns>
        public static int GetOpenValvePosition()
        {
            return _openPosition;
        }

        /// <summary>
        /// Starts the pump.
        /// </summary>
        /// <returns></returns>
        public static void Start()
        {
            lock ( _pumpLock )
            {
                int startPumpVoltage = ( _desiredFlowRate >= Pump.MaxFlowRate ) ? MaxVoltage : StandardStartVoltage;
                Start( startPumpVoltage );
            }
        }

        /// <summary>
        /// Starts the pump.
        /// </summary>
        /// <returns></returns>
        public static void Start( int voltage )
        {
            Log.Debug( "STARTING DS PUMP, voltage=" + voltage );
            lock ( _pumpLock )
            {
                _pumpStartedTime = _pumpLastStartedTime = DateTime.UtcNow;
                SetNewPumpVoltage( Convert.ToByte( voltage ) );
                SetPumpState( 1 );
            }
        }

        /// <summary>
        /// Stops the pump.
        /// </summary>
        /// <returns></returns>
        public static void Stop()
        {
            Log.Debug( "STOPPING DS PUMP" );
            lock ( _pumpLock )
            {
                _pumpStartedTime = DateTime.MinValue;
                SetPumpState( 0 );
            }
        }

        /// <summary>
        /// Gets the pump's error state.
        /// </summary>
        /// <returns>
        /// </returns>
        public static int GetPumpErrorState()
        {
            byte pump = 0;

            unsafe
            {
                GetPumpErrState( &pump );
            }

            return (int)pump;
        }

        /// <summary>
        /// Gets the vacuum error state.
        /// </summary>
        /// <returns>0 = error (pressure higher than 80")</returns>
        public static int GetVacuumErrorState()
        {
            byte vacuum = 0;

            unsafe
            {
                GetVacuumErrState( &vacuum );
            }

            return (int)vacuum;
        }

        /// <summary>
        /// Returns a value indicating whether the pump is running or not.
        /// </summary>
        /// <returns>True if the pump is running</returns>
        public static bool IsRunning()
        {
            byte pump;

            unsafe
            {
                GetPumpState( &pump );
            }

            return pump == 1 && _pumpStartedTime != DateTime.MinValue;
        }

        /// <summary>
        /// Indicates whether to check the flow.
        /// </summary>
        public static bool DoCheckFlow
        {
            get
            {
                return _doCheckFlow;
            }
            set
            {
                _doCheckFlow = value;
            }
        }

        /// <summary>
        /// Indicates whether or not pump is running at desired flow
        /// rate as specified with SetDesiredFlow and maintained
        /// by CheckFlow
        /// </summary>
        public static bool AtDesiredFlow
        {
            get
            {
                if ( !DoCheckFlow )
                    return true;
                return _isDesiredFlowRate;
            }
        }

        /// <summary>
        /// Convert a flow rate from flow sensor voltage to milliliters per minute units.
        /// </summary>
        /// <remarks>
        /// Flow rate calculation from sensor voltage based on calibration curve
        /// FlowOffset is the offset voltage when flow is zero, default is 250, unit is mV.
        /// </remarks>
        /// <param name="flowSensorVolts">This is the flow sensor's output voltage, adjusted with 
        /// the "flow offset" value determined during service diagnostics.
        /// 
        /// Value is in mV.
        /// 
        /// Flow sensor voltage
        ///     = ( Flow sensor A2D counts * 3300 / 1023 ) - ( flowOffset  * 3300 / 1023)
        /// </param>
        /// <param name="vacuumCounts">Current valcuum sensor reading (A2D counts).</param>
        /// <returns>The flow rate in mL</returns>
        public static int CalculateFlowRate( ushort flowSensorVolts, ushort vacuumCounts )
        {
            if ( Configuration.DockingStation.Reservoir )
                return CalculateViperFlowRate( flowSensorVolts, vacuumCounts );

            return CalculateBurtonFlowRate( flowSensorVolts, vacuumCounts );
        }

        /// <summary>
        /// Calculates flow rate when docking station contains "Viper" (iNetDS) hardware.
        /// </summary>
        /// <param name="flowSensorVolts"></param>
        /// <param name="vacuumCounts"></param>
        /// <returns></returns>
        private static int CalculateViperFlowRate( ushort flowSensorVolts, ushort vacuumCounts )
        {
            double dFlowRate = 0;

            if ( vacuumCounts > 100 ) // high pressure 
            {
                // Calibration curve with pressure load 
                if ( flowSensorVolts < 298 )
                    dFlowRate = ( flowSensorVolts - 235 ) * 176 / ( 298 - 235 );
                else if ( flowSensorVolts < 326 )
                    dFlowRate = ( flowSensorVolts - 298 ) * ( 220 - 176 ) / ( 326 - 298 ) + 176;
                else if ( flowSensorVolts < 411 )
                    dFlowRate = ( flowSensorVolts - 326 ) * ( 306 - 220 ) / ( 411 - 326 ) + 220;
                else if ( flowSensorVolts < 495 )
                    dFlowRate = ( flowSensorVolts - 411 ) * ( 381 - 306 ) / ( 495 - 411 ) + 306;
                else if ( flowSensorVolts < 580 )
                    dFlowRate = ( flowSensorVolts - 495 ) * ( 448 - 381 ) / ( 580 - 495 ) + 381;
                else if ( flowSensorVolts < 683 )
                    dFlowRate = ( flowSensorVolts - 580 ) * ( 505 - 448 ) / ( 683 - 580 ) + 448;
                else if ( flowSensorVolts < 786 )
                    dFlowRate = ( flowSensorVolts - 683 ) * ( 558 - 505 ) / ( 786 - 683 ) + 505;
                else if ( flowSensorVolts < 905 )
                    dFlowRate = ( flowSensorVolts - 786 ) * ( 610 - 558 ) / ( 905 - 786 ) + 558;
                else if ( flowSensorVolts < 1098 )
                    dFlowRate = ( flowSensorVolts - 905 ) * ( 697 - 610 ) / ( 1098 - 905 ) + 610;
                else if ( flowSensorVolts < 1228 )
                    dFlowRate = ( flowSensorVolts - 1098 ) * ( 751 - 697 ) / ( 1228 - 1098 ) + 697;
                else if ( flowSensorVolts < 1352 )
                    dFlowRate = ( flowSensorVolts - 1228 ) * ( 806 - 751 ) / ( 1352 - 1228 ) + 751;
                else
                    dFlowRate = ( flowSensorVolts - 1270 ) * ( 806 - 771 ) / ( 1352 - 1270 ) + 771;
            }
            else
            {
                // Calculate flow using calibrated piecewise curve fitting algorithm with no pressure load 
                if ( flowSensorVolts < 330 )
                    dFlowRate = ( flowSensorVolts - 235 ) * 177.6 / ( 330 - 235 );
                else if ( flowSensorVolts < 480 )
                    dFlowRate = ( flowSensorVolts - 330 ) * ( 350 - 177.6 ) / ( 480 - 330 ) + 177.6;
                else if ( flowSensorVolts < 709 )
                    dFlowRate = ( flowSensorVolts - 480 ) * ( 500 - 350 ) / ( 709 - 480 ) + 350;
                else if ( flowSensorVolts < 873 )
                    dFlowRate = ( flowSensorVolts - 709 ) * ( 591 - 500 ) / ( 873 - 709 ) + 500;
                else if ( flowSensorVolts < 1059 )
                    dFlowRate = ( flowSensorVolts - 873 ) * ( 674 - 591 ) / ( 1059 - 873 ) + 591;
                else if ( flowSensorVolts < 1230 )
                    dFlowRate = ( flowSensorVolts - 1059 ) * ( 750 - 674 ) / ( 1230 - 1059 ) + 674;
                else if ( flowSensorVolts < 1395 )
                    dFlowRate = ( flowSensorVolts - 1230 ) * ( 814 - 750 ) / ( 1395 - 1230 ) + 750;
                else
                    dFlowRate = ( flowSensorVolts - 1230 ) * ( 835 - 750 ) / ( 1436 - 1230 ) + 750;

            }
            return dFlowRate < 0.0d ? 0 : (int)dFlowRate;
        }

        /// <summary>
        /// Calculates flow rate when docking station contains "Burton" (DSX) hardware.
        /// </summary>
        /// <param name="flowSensorVolts"></param>
        /// <param name="vacuumCounts"></param>
        /// <returns></returns>
        private static int CalculateBurtonFlowRate( ushort flowSensorVolts, ushort vacuumCounts )
        {
            double dFlowRate = 0;

            if ( vacuumCounts > 100 ) // high pressure
            {
                //Calibration curve with pressure load 
                //PS19 new calibration curve for Burton flow rate calculations
                if ( flowSensorVolts < 383 )
                    dFlowRate = ( flowSensorVolts - 256 ) * 163 / ( 383 - 256 );
                else if ( flowSensorVolts < 721 )
                    dFlowRate = ( flowSensorVolts - 383 ) * ( 329 - 163 ) / ( 721 - 383 ) + 163;
                else if ( flowSensorVolts < 1040 )
                    dFlowRate = ( flowSensorVolts - 721 ) * ( 448 - 329 ) / ( 1040 - 721 ) + 329;
                else if ( flowSensorVolts < 1318 )
                    dFlowRate = ( flowSensorVolts - 1040) * ( 540 - 448 ) / ( 1318 - 1040 ) + 448;
                else if ( flowSensorVolts < 1549 )
                    dFlowRate = ( flowSensorVolts - 1318 ) * ( 611 - 540 ) / ( 1549 - 1318 ) + 540;
                else if ( flowSensorVolts < 1752 )
                    dFlowRate = ( flowSensorVolts - 1549 ) * ( 670 - 611 ) / ( 1752 - 1549 ) + 611;
                else if ( flowSensorVolts < 1925 )
                    dFlowRate = ( flowSensorVolts - 1752 ) * ( 717 - 670 ) / ( 1925 - 1752 ) + 670;
                else
                    dFlowRate = ( flowSensorVolts - 1925 ) * ( 735 - 717 ) / ( 1975 - 1925 ) + 717;
            }
            else
            {
                //Calculate flow using calibrated piecewise curve fitting algorithm with no pressure load
                //PS19 new calibration curve for Burton flow rate calculations
                if ( flowSensorVolts < 410 )
                    dFlowRate = ( flowSensorVolts - 256 ) * 182 / ( 410 - 256 );
                else if ( flowSensorVolts < 787 )
                    dFlowRate = ( flowSensorVolts - 410 ) * ( 363 - 182 ) / ( 787 - 410 ) + 182;
                else if ( flowSensorVolts < 1140 )
                    dFlowRate = ( flowSensorVolts - 787 ) * ( 491 - 363 ) / ( 1140 - 787 ) + 363;
                else if ( flowSensorVolts < 1449 )
                    dFlowRate = ( flowSensorVolts - 1140 ) * ( 595 - 491 ) / ( 1449 - 1140 ) + 491;
                else if ( flowSensorVolts < 1717 )
                    dFlowRate = ( flowSensorVolts - 1449 ) * ( 668 - 595 ) / ( 1717 - 1449 ) + 595;
                else if ( flowSensorVolts < 1946 )
                    dFlowRate = ( flowSensorVolts - 1717 ) * ( 724 - 668 ) / ( 1946 - 1717 ) + 668;
                else if ( flowSensorVolts < 2130 )
                    dFlowRate = ( flowSensorVolts - 1946 ) * ( 784 - 724 ) / ( 2130 - 1946 ) + 724;
                else
                    dFlowRate = ( flowSensorVolts - 2130 ) * ( 803 - 784 ) / ( 2189 - 2130 ) + 784;
            }

            return dFlowRate < 0.0d ? 0 : (int)dFlowRate;
        }

        /// <summary>
        /// Get the flow rate in milliliters per minute.
        /// </summary>
        /// <returns>The calculated flow rate in milliliters per minute.</returns>
        public static int GetFlowRate()
        {
            return CalculateFlowRate( GetFlowVolts(), GetRawVacuum() );
        }

		/// <summary>
		/// Get the flow rate in milliliters per minute.  Also, returns the raw values used to calculate 
		/// the flow rate as out parameters. 
		/// </summary>
		/// <param name="flowCounts">A2D counts reported by the flow sensor</param>
		/// <param name="flowVolts">A2D counts converted to mV</param>
		/// <param name="vacuumCounts">A2D counts reported by the vacuum sensor</param>
		/// <returns>The calculated flow rate in milliliters per minute.</returns>
		public static int GetFlowRate( out ushort flowCounts, out ushort flowVolts, out ushort vacuumCounts )
		{
			// get A2D counts reported by the flow sensor
			flowCounts = GetRawFlow();

			// convert A2D counts to mV
			flowVolts = ConvertRawFlowToVolts( flowCounts );

			// get A2D counts reported by the vacuum sensor
			vacuumCounts = GetRawVacuum();

			// return flow rate in ml/min
			return CalculateFlowRate( flowVolts, vacuumCounts );
		}

        /// <summary>
        /// Get the pump's voltage.
        /// </summary>
        /// <returns>The pump's voltage.</returns>
        public static byte GetPumpVoltage()
        {
            byte voltage;

            unsafe
            {
                GetPumpVoltage( &voltage );
            }

            return voltage;
        }

        /// <summary>
        /// Set the pump's voltage.
        /// </summary>
        /// <param name="pumpVoltage">The voltage for the pump.</param>
        public static void SetNewPumpVoltage( byte pumpVoltage )
        {
            if ( SetPumpVoltage( pumpVoltage ) != 0 )
                Log.Error( string.Format( "SDK ERROR: SetPumpVoltage({0}) failed.", pumpVoltage ) );
        }

        /// <summary>
        /// Set the pump's desired flow rate.
        /// </summary>
        /// <param name="mlMin">
        /// The rate in liters per minute.
        /// </param>
        public static void SetDesiredFlow( int mlMin )
        {
            _desiredFlowRate = mlMin;  // ml/min used by CheckFlowNew
            _isDesiredFlowRate = false;
        }

        /// <summary>
        /// Valure returned by Pump.CheckFlow routine.
        /// </summary>
        public enum FlowStatus
        {
            /// <summary>
            /// Desired flow rate couldn't be achieved without exceeding the pump's minimum voltage.
            /// </summary>
            TooHigh = -2,
            /// <summary>
            /// Target flow rate not achieved.
            /// </summary>
            Inaccurate = -1,
            /// <summary>
            /// Target flow rate achieved.
            /// </summary>
            Accurate = 0,
            /// <summary>
            /// Desired flow rate couldn't be achieved without exceeding the pump's maximum voltage.
            /// </summary>
            TooLow = 1
        }

        public static FlowStatus CheckFlow()
        {
            ushort rawFlow;
            ushort vacuumCounts;

            return CheckFlow( TimeSpan.MaxValue, out rawFlow, out vacuumCounts );
        }

        /// <summary>
        /// Returns a value indicating whether the minimum gas flow is achievable.
        /// If the gas flow is below the minimum required it will try to increase the flow by increase the pump voltage.
        /// If increasing the pump voltage does not increase the flow it will return false.
        /// </summary>
        /// <param name="timeOut"></param>
        /// <param name="rawFlow">The final reading taken from the flow sensor is returned in this parameter. This is useful for diagnostics.</param>
        /// <param name="vacuumCounts">The final reading taken from the vacuum sensor is returned in this parameter. This is useful for diagnostics.</param>
        /// <returns>
        /// 0 if desired flow was establish.
        /// 1 if couldn't reach target flow without exceeding max pump voltage.
        /// -1 if couldn't reach target flow without exceeding min pump voltage.
        /// </returns>
        public static FlowStatus CheckFlow( TimeSpan timeOut, out ushort flowA2dCounts, out ushort vacuumA2dCounts )
        {
            flowA2dCounts = 0;
            vacuumA2dCounts = 0;

            if ( !DoCheckFlow )
                return FlowStatus.Accurate;
				 
            const string funcName = "CheckFlow: ";

            DateTime lastPumpStartedTime = Pump.GetTimePumpLastStarted();

            // For DSX, if we just turned on the pump, we need to wait a bit to give the flow sensor
            // time to stabilize.
            // If we don't wait, then CheckFlow will often see the flow rate as being too high and
            // will then will waste an iteration in the loop farther below by lowering the pump
            // voltage uncessessarily.
            // 2 seconds seems to be minimum time we need to wait,
            // based on some informal experiments I did - JMP, 7/2014
            if ( Configuration.DockingStation.Reservoir == false
            && _lastCheckFlowTime != lastPumpStartedTime
            && _desiredFlowRate < MaxFlowRate )  // no reason to do this when purging
            {
                Log.Debug( "Waiting 2 seconds for flow to stabilize." );
                Thread.Sleep( 2000 );
            }

            _lastCheckFlowTime = lastPumpStartedTime;

            // For iNetDS, each call to GetRawFlow takes about 875ms.
            // This is due to it taking 41 sample readings, with 20ms sleep between samples.
            //
            // For DSX, each call to GetRawFlow takes about 175ms, due to it taking 82 sample
            // readings, with 2ms sleep between samples.
            // 
            // Due to DSX's calls to GetRawFlow being so quick, we sleep a longer time
            // between each call to give flow sensor time to react to pump voltage changes.
            // Since iNetDS's GetRawFlow calls already sleep a lot on their own, we don't
            // need to sleep as long between calls.
            //
            // (Note that for original DS2, the calls to GetRawFlow were nearly instantaneous.)
            TimeSpan sleepInterval = Configuration.DockingStation.Reservoir 
                ? new TimeSpan( 0, 0, 0, 0, 250 ) : new TimeSpan( 0, 0, 0, 0, 500 );

            const int FLOWRATE_TOLERANCE_HIGH = 50;
            const int FLOWRATE_TOLERANCE_MEDIUM = 25;
            const int FLOWRATE_TOLERANCE_MEDIUM_LOW = 10;

            const byte VOLTAGE_INCREMENT_HIGH = 10;
            const byte VOLTAGE_INCREMENT_MEDIUM = 5;
            const byte VOLTAGE_INCREMENT_MEDIUM_LOW = 2;
            const byte VOLTAGE_INCREMENT = 1;

            const int MAX_CONSECUTIVE_LOW_FLOW_VALUES = 5;   // SGF  19-Jan-2012  INS-1913 & INS-1914

            Log.Debug( string.Format( "{0}Adjusting flow rate to {1}ml/min.", funcName, _desiredFlowRate ) );

            // Read the current voltage setting.
            int oldVoltage = -1, voltage = -1;

            FlowStatus flowStatus = FlowStatus.Inaccurate;

            bool pumpRunning = true;
            int attempts = 0;
            int flowRate = 0;
            int lowZeroAirFlow = 0; // SGF  19-Jan-2012  INS-1913 & INS-1914

            TimeSpan elapsed = TimeSpan.Zero;

            _checkingFlow = true;

            // Optimization: Determine outside the loop whether we're using fresh air or zero air since calling
            // Cylinder.IsXxxAir property need to loop through the cylinder's internal lists of gases.
            // _openEndPoint will be null if pump is started without calling OpenGasEndPoint.
            bool isFreshAir = ( _openEndPoint != null ) && _openEndPoint.Cylinder.IsFreshAir;
            bool isZeroAir = ( _openEndPoint != null ) && _openEndPoint.Cylinder.IsZeroAir;

            try
            {
                while ( true )
                {
                    int openValve = 0;

                    if ( TimeSpan.Compare( elapsed, timeOut ) >= 0 ) // timed out trying to reach target flow?
                    {
                        Log.Debug( string.Format( "{0}Timed out trying to reach target flow", funcName ) );
                        break;
                    }

                    attempts++;

                    // Do ALL hardware calls together within a critical section.
                    // This is do avoid problems where in the middle of these successive calls,
                    // another thread stops the pump and/or closes the valve.
                    lock ( _pumpLock )
                    {
                        // Will return false if something turns off the pump (such as at the end of a bump or calibration).
                        if ( !IsRunning() )
                        {
                            pumpRunning = false;
                            Log.Debug( string.Format( "{0}Pump has been turned off", funcName ) );
                            break;
                        }

                        openValve = GetOpenValvePosition();

                        if ( openValve <= 0 )
                        {
                            Log.Debug( string.Format( "{0}No open valves", funcName ) );
                            break;
                        }

                        voltage = (int)GetPumpVoltage();
                        if ( oldVoltage == -1 ) // needs initialized the first time through the loop
                            oldVoltage = voltage;

                        try
                        {
                            // Read the current flow.  This call takes about 860ms
                            flowA2dCounts = GetRawFlow();

                            // Read the current raw vacuum.  Needed to calculate ml/min flow rate. This call takes about 860ms
                            vacuumA2dCounts = GetRawVacuum();
                        }
                        catch ( DeviceDriverException se )
                        {
                            // If other threads are causing high CPU load, then reading flow sensor or vacuum sensor may fail.
                            Log.Error( string.Format( "{0}Caught DeviceDriverException, Device={1}, LastError={2}", funcName, se.DeviceHardware, se.LastErrorResult ) );
                            // Best we can do is wait a moment, just as if we made it to bottom of loop, then try again.
                            Thread.Sleep( (int)sleepInterval.TotalMilliseconds );
                            elapsed = elapsed.Add( sleepInterval );
                            continue;
                        }

                    } // end-lock

                    ushort flowVolts = ConvertRawFlowToVolts( flowA2dCounts );

                    flowRate = CalculateFlowRate( flowVolts, vacuumA2dCounts );

                    double vacuumPressure = ConvertRawVacuumToInches( vacuumA2dCounts );

                    Log.Debug( string.Format( "{0}{1} vlv={2},flow={3}ml/min (a2d={4},mv={5}mv),Vac={6}\"({7}),v={8}",
                        funcName, attempts, openValve, flowRate, flowA2dCounts, flowVolts, vacuumPressure, vacuumA2dCounts, voltage ) );

                    // Is flow rate too high?
                    if ( flowRate > _desiredFlowRate + FLOWRATE_TOLERANCE )
                    {
                        // As we narrow in on the desired flow rate (get within double of
                        // of the tolerance), don't increment the voltage as much so that we
                        // don't overshoot are target flow rate.
                        if ( flowRate > _desiredFlowRate + FLOWRATE_TOLERANCE_HIGH ) voltage -= VOLTAGE_INCREMENT_HIGH;
                        else if ( flowRate > _desiredFlowRate + FLOWRATE_TOLERANCE_MEDIUM ) voltage -= VOLTAGE_INCREMENT_MEDIUM;
                        else if ( flowRate > _desiredFlowRate + FLOWRATE_TOLERANCE_MEDIUM_LOW ) voltage -= VOLTAGE_INCREMENT_MEDIUM_LOW;
                        else voltage -= VOLTAGE_INCREMENT;
                    }

                    // else, is flow rate too low?
                    else if ( flowRate < _desiredFlowRate - FLOWRATE_TOLERANCE )
                    {
                        // If trying to achive pump's maximum flow, then just 
                        // ensure the pump's voltage is max'd out.
                        if ( _desiredFlowRate == Pump.MaxFlowRate )
                            voltage = Pump.MaxVoltage;

                        // As we narrow in on the desired flow rate (get within double of
                        // of the tolerance), don't increment the voltage as much so that we
                        // don't overshoot are target flow rate.
                        else if ( flowRate < _desiredFlowRate - FLOWRATE_TOLERANCE_HIGH ) voltage += VOLTAGE_INCREMENT_HIGH;
                        else if ( flowRate < _desiredFlowRate - FLOWRATE_TOLERANCE_MEDIUM ) voltage += VOLTAGE_INCREMENT_MEDIUM;
                        else if ( flowRate < _desiredFlowRate - FLOWRATE_TOLERANCE_MEDIUM_LOW ) voltage += VOLTAGE_INCREMENT_MEDIUM_LOW;
                        else voltage += VOLTAGE_INCREMENT;
                    }

                    // else, Flow right is within tolerance.
                    else
                    {
                        flowStatus = FlowStatus.Accurate;
                        Log.Debug( string.Format( "{0}Flow adjusted to {1}ml/min (Target={2})", funcName, flowRate, _desiredFlowRate ) );
                        break;
                    }

                    // Avoid overflow/underflow of voltage
                    voltage = Math.Min( voltage, (int)byte.MaxValue );
                    voltage = Math.Max( voltage, (int)byte.MinValue );

                    // If the voltage is already set to the max, return false.
                    if ( voltage >= MaxVoltage )
                    {
                        Log.Warning( string.Format( "{0}WARNING: Can't reach target flow ({1}) without exceeding Max pump voltage.", funcName, _desiredFlowRate ) );
                        flowStatus = FlowStatus.TooLow;
                        break;
                    }

                    if ( voltage <= MinVoltage )
                    {
                        Log.Warning( string.Format( "{0}WARNING: Can't reach target flow ({1}) without exceeding Min pump voltage.", funcName, _desiredFlowRate ) );
                        flowStatus = FlowStatus.TooHigh;
                        break;
                    }

                    //Set _isBadPumpTubing to TRUE when CheckFlow method detects that flow is inaccurate, voltage is greater than 200, and vacuum is less than 6 inches
                    //Else set _isBadPumpTubing to FALSE
                    if (flowStatus == FlowStatus.Inaccurate && voltage > 200 && vacuumPressure < 6.0)
                        _isBadPumpTubing = true;
                    else
                        _isBadPumpTubing = false;

                    // If we're trying to establish Maximum flow rate (such as for a purge, etc),
                    // but we can't get any flow at all, the just give up.  The port is blocked / the cylinder is empty.
                    // 
                    // For now, we don't bother marking fresh air as 'too low'.
                    // If we did , then the port would get marked as 'empty'
                    // in the database, and there's currently no way to reset it back to
                    // Full except to reboot the docking station.
                    //
                    // If/when we have mechanism to allow an 'Empty' fresh air port to
                    // be 'reset' back to Full, then we should remove this check for IsFreshAir.
                    //
                    // SEE ALSO: The InstrumentPurgeOperation.CheckAir.
                    //
                    // - JMP, 6/30/2011
                    if ( _desiredFlowRate == Pump.MaxFlowRate
                    && voltage >= MaxVoltage
                    && flowRate < FLOWRATE_TOLERANCE_HIGH
                    && attempts >= MAX_CONSECUTIVE_LOW_FLOW_VALUES
                    && isFreshAir == false )
                    {
                        Log.Warning( string.Format( "{0}WARNING: Can't reach target flow ({1}).", funcName, _desiredFlowRate ) );
                        flowStatus = FlowStatus.TooLow;
                        break;
                    }

                    // SGF  19-Jan-2012  INS-1913 & INS-1914 -- begin
                    // ------------------------------------------------------
                    // Zero air cylinder is typically only used for zeroing and for for calibrating O2.
                    // Both operations typically finish before the normal checkflow logic (the above "if"
                    // statement) for detecting empty cylinders will deem the zero air cylinder as empty.
                    // (i.e., the operations finish before we have a chance to fully ramp up the pump
                    // which usually takes at least 40 seconds or so if cylinder is empty.).
                    // Therefore, for zero air cylinders, instead of waiting for pump to fully ramp up,
                    // we allow the cylinder to appear as empty for 5 iterations of the loop.
                    // Assuming each iteration takes around 2 seconds, we can detect a zero air cylinder
                    // in around 10 seconds, but at the expense of not enforcing the normal rule that we 
                    // should fully ramp up the pump before looking for low flow and then deeming a cylinder
                    // as empty.
                    if ( isZeroAir == true && flowRate < FLOWRATE_TOLERANCE_HIGH )
                    {
                        lowZeroAirFlow++;
                        Log.Trace( string.Format( "{0}lowZeroAirFlow = {1}", funcName, lowZeroAirFlow ) );
                    }
                    else
                        lowZeroAirFlow = 0;

                    Log.Trace( string.Format( "{0}Flow Rate = {1}, Flow Rate Tolerance = {2}, Consecutive Low ZeroAir Flows = {3}", funcName, flowRate, FLOWRATE_TOLERANCE_HIGH, lowZeroAirFlow ) );

                    if ( lowZeroAirFlow >= MAX_CONSECUTIVE_LOW_FLOW_VALUES )
                    {
                        Log.Warning( string.Format( "{0}WARNING: Zero air flow rate too low. Cylinder possibly low or empty.", funcName ) );
                        flowStatus = FlowStatus.TooLow;
                        break;
                    }
                    // SGF  19-Jan-2012  INS-1913 & INS-1914 -- end

                    // Only adjust if necessary.
                    if ( voltage != oldVoltage )
                    {
                        Log.Trace( string.Format( "{0}Setting New Pump Voltage: {1}", funcName, voltage ) );
                        SetNewPumpVoltage( (byte)voltage );
                        oldVoltage = voltage;
                    }

                    Thread.Sleep( (int)sleepInterval.TotalMilliseconds );
                    elapsed = elapsed.Add( sleepInterval );

                } // end-while

                // It's not possible for the pump to reach flow rate of 1000ml.
                // i.e., it will always be inaccurate when trying to achive such a flow rate.
                // So, if we see that we're trying the reach this high flow rate, we don't assume
                // the cylinder is empty just because we can't acheive that flow.
                // So, bascially, if we're trying to achieve a high flow rate, then assume 
                // a good enough flow rate if we're at least able to achieve *something*.
                // i.e., if if we have some sort of flow rate, then we at least know the cylinder 
                // is not empty.
                if ( _desiredFlowRate > Pump.StandardFlowRate && flowStatus != FlowStatus.Accurate ) // it will probably NEVER be Accurate
                {
                    flowStatus = ( flowRate >= FLOWRATE_TOLERANCE_HIGH ) ? FlowStatus.Inaccurate : FlowStatus.TooLow;
                }

                string msg = string.Format( "{0}Finished. FlowStatus={1}", funcName, flowStatus.ToString() );

                Log.Debug( msg );

                if ( TimeSpan.Compare( elapsed, timeOut ) >= 0 )
                    Log.Warning( funcName + "Timed out trying to reach target flow." );
                else if ( pumpRunning == true && flowStatus == FlowStatus.TooHigh )
                    Log.Warning( funcName + "Unable to reach target flow. Gas empty?" );

                return flowStatus;
            }
            finally
            {
                _checkingFlow = false;
            }
        }

        /// <summary>
        /// Returns the raw "A2D counts" from the flow sensor.
        /// </summary>
        /// <remarks>
        /// Note that this call can take nearly a second to complete due
        /// to the lock it needs to obtain and due to the underlying call
        /// to the BSP which takes several milliseconds to return.
        /// </remarks>
        /// <returns>The flow rate.</returns>
        /// <exception cref="SdkException"/>
        public static ushort GetRawFlow()
        {
            ushort flow;
            int error;

            unsafe
            {
                error = GetFlow( Configuration.DockingStation.Reservoir ? (short)1 : (short)1, &flow );
            }

            if ( error != 0 )
            {
                int lastError = WinCeApi.GetLastError();
                Log.Error( "SDK ERROR: GetFlow() failed. GetLastError=" + lastError );
                throw new DeviceDriverException( DeviceHardware.Flow, lastError );
            }

            return flow;
        }

        /// <summary>
        /// Get a reading from the flow sensor.
        /// This reading is the raw A2D counts converted to millivolts, and with the "flow offset" applied.
        /// </summary>
        /// <returns>Returned flow rate is the raw A2D counts converted to millivolts</returns>
        public static ushort GetFlowVolts()
        {
            ushort rawFlow = GetRawFlow();  // Get A2D counts reported by the flow sensor

            return ConvertRawFlowToVolts( rawFlow );
        }

        /// <summary>
        /// Convert flow sensor's raw reading to millivolts, and with the "flow offset" applied.
        /// </summary>
        /// <param name="rawFlow">The A2D counts reported by the flow sensor</param>
        /// <returns></returns>
        public static ushort ConvertRawFlowToVolts( ushort rawFlow )
        {
            int rawFlowVolts = rawFlow * 3300 / 1023;  // convert a2d counts to mv

            // If _flowOffset is MinValue, it's undefined/unknown.
            // Just use zero instead for computing the flow rate.
            int offset = Configuration.DockingStation.FlowOffset == int.MinValue ? 0 : Configuration.DockingStation.FlowOffset;

            //Log.Info( "FlowOffset=" + offset );

            int offsetVolts = offset * 3300 / 1023; // convert a2d counts to mv

            int flowVolts = rawFlowVolts - offsetVolts;

            return flowVolts < 0 ? (ushort)0 : (ushort)flowVolts;
        }


        /// <summary>
        /// Returns the raw "A2D counts" reported by vacuum sensor.
        /// </summary>
        /// <remarks>
        /// Note that this call can take nearly a second to complete due
        /// to the lock it needs to obtain and due to the underlying call
        /// to the BSP which takes several milliseconds to return.
        /// </remarks>
        /// <returns>The pressure reading</returns>
        public static ushort GetRawVacuum()
        {
            ushort vacuum;

            // Prevent multiple a2d calls at one time.
            do
            {
                int error;
                unsafe
                {
                    error = GetVacuum( &vacuum );
                }
                if ( error != 0 )
                    Log.Error( "SDK ERROR: GetVacuum() failed." );

            } while ( vacuum == 0 || vacuum == UInt16.MaxValue ); // whis is this loop here?  Could this potentially be an infinite loop if vacuum sensor dies?

            return vacuum;
        }

        /// <summary>
        /// Convert a raw vacuum reading (in A2d counts) to inches of water units.
        /// </summary>
        /// <param name="vacuum">The vacuum in counts.</param>
        /// <returns>The vacuum in inches of water.</returns>
        public static double ConvertRawVacuumToInches( ushort vacuum )
        {
            // 6.745 == 3300 / 1023 * 2.091
            return Controller.Round( ( ( ( ( (double)vacuum * 6.745 ) - 500 ) * 83 ) / 4000 ), 2 );
        }

        /// <summary>
        /// Return the vacuum sensor and returns result as inches of water. 
        /// </summary>
        /// <returns>The current vacuum pressure measured in inches of water.</returns>
        public static double GetVacuumPressure()
        {
            return ConvertRawVacuumToInches( GetRawVacuum() );
        }

        #endregion Methods

    } // end-class

} // end-namespace
