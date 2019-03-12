using System;
using System.Collections.Generic;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Services.Resources;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
	
	////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a diagnostic operation.
	/// </summary>
	public class DiagnosticOperation : DiagnosticAction , IOperation
	{
        private List<GeneralDiagnosticProperty> _gdpList = new List<GeneralDiagnosticProperty>();

		/// <summary>
		/// Creates a new instance of DiagnosticOperation class.
		/// </summary>
        public DiagnosticOperation() {}

        public DiagnosticOperation( DiagnosticAction action ) : base( action ) {}

		/// <summary>
		/// Executes a docking station diagnostic operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            // Initialize resource manager's culture to be same as current configuration culture.
            DiagnosticResources.Culture = Configuration.DockingStation.Language.Culture;
			
            // Make the return event and return diagnostic and wire them together.
            DiagnosticEvent dsEvent = new DiagnosticEvent( this );
            dsEvent.DockingStation = Controller.GetDockingStation(); // Retrieve IDS's complete information.

            // Make the builder for the details.
            DetailsBuilder details = new DetailsBuilder();

            details.AddDockingStation( dsEvent.DockingStation );

            // SGF  23-May-2011  INS-1741
            ReportDiagnosticDate(details);

            GeneralDiagnostic generalDiagnostic = new GeneralDiagnostic( Configuration.DockingStation.SerialNumber, DateTime.UtcNow );

            ExecuteDiagnostics( dsEvent, details );

			// Retrieve the details.
			dsEvent.Details = details.ToString();

			// Write the details to a log file.
            FlashCard.WriteTextFile( LAST_RUN_DETAILS_FILE_NAME, dsEvent.Details );

            dsEvent.Diagnostics.Add( generalDiagnostic );
            generalDiagnostic.Items = _gdpList.ToArray();

            return dsEvent;  // Return the event
		}

        // SGF  23-May-2011  INS-1741
        private void ReportDiagnosticDate(DetailsBuilder details)
        {
            Log.Debug(details.Add("    ", DiagnosticResources.DETAILS_DOCKINGSTATION_SELF_DIAGNOSTICS_DATE, DateTime.Now));
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// 23-Feb-2011, Originally based on following dev jiras...
        /// http://svrdevjira/browse/DSHW-120
        /// http://svrdevjira/browse/INS-3124
        /// </remarks>
        /// <param name="dsEvent"></param>
        /// <param name="details"></param>
        private void ExecuteDiagnostics( DiagnosticEvent dsEvent, DetailsBuilder details )
        {
            Log.Debug("ExecuteDiagnostics: Self-diagnostics starting.");
            InitializeEnvironment(details);

            try
            {
//#if !DEBUG
                TestSystemCurrent( details );
                TestCoreVoltage( details );
                TestEthernet( details );
                TestVacuumSensor( details );
                TestFlowSensor( details );
                TestForLeak( details );
                TestPump( details );
				//TestSolenoids( details );
                for ( int portNum = 1; portNum <= Configuration.DockingStation.NumGasPorts; portNum++ )
                    TestFlow( details, portNum );
//#endif
            }
            catch ( Exception e )
            {
                Log.Error( e );
                throw;
            }
            finally
            {
                RestoreEnvironment( details );
            }

            Log.Debug("ExecuteDiagnostics: Self-diagnostics complete.");
        }

        /// <summary>
        /// Read the iNetDS's system current.
        /// </summary>
        /// <remarks>
        /// Engineering Notes: This step is to check current through 12V main power circuit.
        /// This current varies when Docking Station works at different scenarios such as
        /// charging or not charging, solenoid on or off.
        /// If the current is too low, there is something wrong on the current sensing circuit.
        /// Too high current indicates some component is not working properly. 
        /// </remarks>
        /// <param name="details">The string to hold details.</param>
        private void TestSystemCurrent(DetailsBuilder details)
        {
            int rawCurrent = Controller.Get12VCurrent();
            int currentInMilliamps = CountToCurrent(rawCurrent);

            string currentString = BuildCountAndUnitString(rawCurrent, currentInMilliamps, DiagnosticResources.MILLIAMPS);
            _gdpList.Add(new GeneralDiagnosticProperty("DIAGNOSTIC_SYSTEM_CURRENT", rawCurrent.ToString()));
            ReportDiagnostic(details, DiagnosticResources.SYSTEM_CURRENT, currentString, ((rawCurrent < 12) || (rawCurrent > 347)));
        }

        /// <summary>
        /// Read the iNetDS's core voltage.
        /// The correct voltage for Atmel AT91SAM9G45 microprocessor used in iNetDS is 1V.
        /// </summary>
        /// <remarks>
        /// Engineering Notes: This step is to check core voltage of microprocessor.
        /// The work frequency of microprocessor is controlled by this voltage.
        /// The deviation of this voltage could affect the system frequency significantly and
        /// make all data operation and communication messed up.
        /// In order to make sure the micro works properly, this voltage has to be in a
        /// certain tolerated range. 
        /// The correct voltage for Atmel AT91SAM9G45 microprocessor used on iNetDS is 1V.
        /// </remarks>
        /// <param name="details">The string to hold details.</param>
        private void TestCoreVoltage(DetailsBuilder details)
        {
            int coreVoltageCount = Controller.GetCoreVoltage();
            int coreVoltage = CountToVoltage(coreVoltageCount);

            string voltageString = BuildCountAndUnitString(coreVoltageCount, coreVoltage, DiagnosticResources.MILLIVOLTS);
            _gdpList.Add(new GeneralDiagnosticProperty("DIAGNOSTIC_CORE_VOLTAGE", coreVoltageCount.ToString()));
            ReportDiagnostic(details, DiagnosticResources.CORE_VOLTAGE, voltageString, ((coreVoltageCount < 294 /*294==950mV*/ ) || (coreVoltageCount > 325 /*325==1050mV*/)));
        }

        /// <summary>
        /// Verifies if the MAC address is been properly programmed or not.
        /// </summary>
        /// <param name="details">The string to hold details.</param>
        private void TestEthernet(DetailsBuilder details)
        {
            string macAddress = Controller.GetWiredNetworkAdapter().MacAddress;
            string defaultIscMacAddress = Controller.DEFAULT_MAC_ADDRESS;
            string iscOUI = defaultIscMacAddress.Substring(0, 8);

            _gdpList.Add(new GeneralDiagnosticProperty("DIAGNOSTIC_ETHERNET", macAddress));
 
            // invalid MAC
            // We expect the first half of the MAC address to be "00:0B:D8", which is ISC's registered OUI.
            string macOUI = macAddress.Substring(0, 8);
            ReportDiagnostic(details, DiagnosticResources.MAC_ADDRESS_OUI, macAddress, iscOUI, (macOUI.CompareTo(iscOUI) != 0));

            // default MAC
            // We expect the MAC address to be programmed as something other than the ISC default value.
            ReportDiagnostic(details, DiagnosticResources.MAC_ADDRESS_DEFAULT, macAddress, defaultIscMacAddress, (macAddress.CompareTo(defaultIscMacAddress) == 0));
        }

        /// <summary>
        /// Check the vacuum sensor.
        /// </summary>
        /// <remarks>
        /// Engineering Notes: This step is to check if the reading of vacuum sensor is reasonable.
        /// After the initialization, the vacuum (negative pressure) should be close to zero.
        /// Failure on this test indicates possible issues on vacuum sensor, or A2D converter,
        /// or some tubing is blocked by something.
        /// </remarks>
        /// <param name="details">The string to hold details.</param>
        private void TestVacuumSensor(DetailsBuilder details)
        {
            Pump.CloseAllValves( true );

            Thread.Sleep(500); // Give the valves a chance to finish closing

            Pump.OpenValve(1, false); // Open solenoid 1
            Thread.Sleep(500); // Pause at least 500ms.

            // Read the vacuum pressure
            ushort vacuumCount = Pump.GetRawVacuum();
            int vacuumVoltage = CountToVoltage(vacuumCount);

            // Test vacuum readings
            // Vacuum count value should be in the range of 62 (200mV) to 128 (410 mV), inclusive.
            // The normal vacuum count is 77.

            // Report results
            string vacuumString = BuildCountAndUnitString(vacuumCount, vacuumVoltage, DiagnosticResources.MILLIVOLTS);
            _gdpList.Add(new GeneralDiagnosticProperty("DIAGNOSTIC_VACUUM", vacuumCount.ToString()));
            ReportDiagnostic(details, DiagnosticResources.VACUUM, vacuumString, ((vacuumCount < 62) || (vacuumCount > 128)));
        }

		/// <summary>
		/// Check if flow offset has been set.
		/// </summary>
		/// <param name="details">The string to hold details.</param>
		private void TestFlowOffset( DetailsBuilder details )
		{
			bool isFlowOffsetAssigned = true;
			int flowOffset = Configuration.DockingStation.FlowOffset;

			// if flowOffset has not been set it will default to int.MinValue;
			// 0 is a valid setting for the flowOffset
			if ( flowOffset == int.MinValue )
			{
				isFlowOffsetAssigned = false;
			}

			// report the results
			_gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_FLOW_OFFSET", flowOffset.ToString() ) );
			_gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_FLOW_OFFSET_PASSED", isFlowOffsetAssigned.ToString() ) );

			// log the results
			ReportDiagnostic( details, DiagnosticResources.FLOW_OFFSET, flowOffset, !isFlowOffsetAssigned );
		}

        /// <summary>
        /// Read the flow sensor
        /// </summary>
        /// <remarks>
        /// Engineering Notes: This step is to check if the reading of flow sensor is reasonable.
        /// With the pump closed and all valves closed, the flow should be lower than a certain level.
        /// Failure on this indicates the reading of flow rate is not correct.
        /// </remarks>
        /// <param name="details">The string to hold details.</param>
        private void TestFlowSensor( DetailsBuilder details )
        {
            Pump.CloseAllValves( true );
            Pump.Stop();
            Thread.Sleep( 500 ); // Give the valves a chance to finish closing

            // Read the flow rate.
            ushort rawFlowCount = Pump.GetRawFlow();
            ushort flowVolts = Pump.ConvertRawFlowToVolts( rawFlowCount );
            ushort vacuumCounts = Pump.GetRawVacuum();
            double vacuumInches = Pump.ConvertRawVacuumToInches( vacuumCounts );
            int flowRate = Pump.CalculateFlowRate( flowVolts, vacuumCounts );

            // Test flow reading
            string flowString = BuildFlowString( rawFlowCount, flowRate, flowVolts );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_FLOW", rawFlowCount.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_FLOW_VOLTS", flowVolts.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_FLOW_RATE", flowRate.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_FLOW_VACUUM", vacuumCounts.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_FLOW_VACUUM_INCHES", vacuumInches.ToString() ) );

            // Flow value should be in the range of 0 (0mV) to 186 (600 mV), inclusive.
            // INS-3067, 6/26/2013, JMP - changer criteria from 43/128 to 0/186.
            ReportDiagnostic( details, DiagnosticResources.FLOW, flowString, ( ( rawFlowCount < 0 ) || ( rawFlowCount > 186 ) ) );
        }

        /// <summary>
        /// Check for leaks.
        /// </summary>
        /// <remarks>
        /// This is to check if the components in the closed gas delivery system work properly including
        /// tubing, 3 solenoid valves, manifolds, check valves, vacuum sensor, flow sensor and pump.
        /// The pump failure to operate could also cause this test to fail.
        /// If a leakage is found, flow check will be meaningless.
        /// </remarks>
        /// <param name="details">The details to fill in.</param>
        private void TestForLeak(DetailsBuilder details)
        {
            Pump.CloseAllValves( true );

            Thread.Sleep( 500 ); // Give the valves a chance to finish closing

            int pumpVoltage = 80; // initial voltage for leak test
            bool leakCheck1Failed = true;
            ushort vac1Raw;
            double vac1Inches;
            string vac1String;
            const int maxPumpVoltage = 120;
            const ushort rawInches40 = 360;
            const int inches40 = 40;
            const int pumpVoltageIncrement = 10;
            //const ushort rawInches55 = 466;
            //const int inches55 = 55;

            Pump.Start( pumpVoltage ); // start pump with initial voltage

            //Suresh 19-JUNE-2012 INS-3067
            do
            {
                // After changing pump voltage, always wait 1 second before reading  
                // vacuum sensor to give the sensor time to adjust to the change.
                Thread.Sleep( 1000 );

                // Take vacuum reading (vac1)
                vac1Raw = Pump.GetRawVacuum();
                vac1Inches = Pump.ConvertRawVacuumToInches( vac1Raw );
                vac1String = BuildCountAndUnitString( vac1Raw, vac1Inches, 1, "\"" );

                Log.Debug( string.Format( "Vacuum: {0}, Pump voltage: {1}", vac1String, pumpVoltage ) );

                // Check vacuum reading against target pressure.  Pass after we reach or exceed target.
                if ( vac1Raw >= rawInches40 )
                {
                    // Pass if vac1 >= 40 inches of water
                    leakCheck1Failed = false;
                    Log.Debug( string.Format( "Leak Check 1 PASSED. (vacuum exeeds {0}\")", inches40 ) );
                    break;
                }

                if ( pumpVoltage + pumpVoltageIncrement >= maxPumpVoltage )// if the pump voltage crests above 120 
                {
                    Log.Debug( string.Format( "Leak Check 1 FAILED (pump voltage exeeds {0} but vacuum under {1}\")", maxPumpVoltage, inches40 ) );
                    break;
                }

                pumpVoltage += pumpVoltageIncrement;

                Pump.SetNewPumpVoltage( (byte)pumpVoltage ); // set the new voltage to pump

            } while ( true );

            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_LEAK_CHECK_VAC1", vac1Raw.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_LEAK_CHECK_VAC1_INCHES", vac1Inches.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_LEAK_CHECK_VAC1_PASSED", (!leakCheck1Failed).ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_LEAK_CHECK_VAC1_PUMP_VOLTAGE", pumpVoltage.ToString() ) );
            ReportDiagnostic( details, DiagnosticResources.LEAK_CHECK_1, vac1String, leakCheck1Failed );

            int vacuumError = Pump.GetVacuumErrorState(); // Check status of Vacuum Error by calling GetVacuumErrState()

            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_LEAK_CHECK_VAC_ERROR", vacuumError.ToString() ) );
            //Pass no matter what the state is [Vacuum Error Status]. The purpose is to keep the data structure of the report same as previous version. 
            ReportDiagnostic( details, DiagnosticResources.VACUUM_ERROR_STATUS, vacuumError.ToString(), false );

            // Stop pump
            Pump.Stop();

            //Open Solenoid #1 for 1 second to relieve the pressure
            Pump.OpenValve( 1, false );
            Thread.Sleep( 1000 ); // 1 sec
            Pump.CloseValve( 1 ); 
        }

        /// <summary>
        /// Check if pump works properly.
        /// </summary>
        /// <remarks>
        /// Engineering Notes: This step is to check if pump works properly.
        /// Run pump at two different control voltages, check the flow rate difference between these two cases.
        /// </remarks>
        /// <param name="details">The string to hold details.</param>
        private void TestPump(DetailsBuilder details)
        {
            // Find a port without a gas cylinder connected. 
            // If all ports are connected, skip this test.
            DockingStation ds = Controller.GetDockingStation();

            // First, see if port one is unconnected or is providing FRESH AIR
            // Note: we are trying ports in the order of 3-2-1 so as to avoid pulling air through the filter, if possible.
            int testPort = -1;
            GasEndPoint gasEndPoint = null;
            for ( int solenoid = Configuration.DockingStation.NumGasPorts; solenoid >= 1 && testPort <= 0; solenoid-- )
            {
                gasEndPoint = ds.GasEndPoints.Find(m => m.Position == solenoid);
                if (gasEndPoint == null || gasEndPoint.Cylinder.IsFreshAir)
                    testPort = solenoid;
            }

            if (testPort <= 0)
            {
                Log.Debug("TestPump:  could not find open solenoid; this test will be skipped.");
                return;
            }

            // Open solenoid valve determined to be unconnected
            Pump.CloseAllValves( true );
            Thread.Sleep(500); // Give the valves a chance to finish closing
            Pump.OpenValve(testPort, false);

            // Start pump with pump voltage of 80
            Pump.Start(80);

            // Wait 3 seconds
            Thread.Sleep(3000);

            // Read flow 1
            ushort flowCount1 = Pump.GetRawFlow();
            ushort flowVolts1 = Pump.ConvertRawFlowToVolts( flowCount1 );
            ushort vacuumCounts1 = Pump.GetRawVacuum();

            int flowRate1 = Pump.CalculateFlowRate( flowVolts1, vacuumCounts1 );
            string flowString1 = BuildFlowString(flowCount1, flowRate1, flowVolts1);

            // Increase pump voltage to 240
            Pump.SetNewPumpVoltage(240);

            // Wait 3 seconds
            Thread.Sleep(3000);

            // Check Pump Error status
            int pumpErrorState = Pump.GetPumpErrorState();

            // Fail if state is 1
            _gdpList.Add(new GeneralDiagnosticProperty("DIAGNOSTIC_PUMP_ERROR_STATUS", pumpErrorState.ToString()));
            ReportDiagnostic(details, DiagnosticResources.PUMP_ERROR_STATUS, pumpErrorState.ToString(), (pumpErrorState == 1));

            // Read flow 2
            ushort flowCount2 = Pump.GetRawFlow();
            ushort flowVolts2 = Pump.ConvertRawFlowToVolts( flowCount2 );
            ushort vacuumCounts2 = Pump.GetRawVacuum();
            int flowRate2 = Pump.CalculateFlowRate( flowVolts2, vacuumCounts2 );
            string flowString2 = BuildFlowString(flowCount2, flowRate2, flowVolts2);

            // Fail if f2 - f1 < 100 OR f2 - f1 > 450
            _gdpList.Add(new GeneralDiagnosticProperty("DIAGNOSTIC_PUMP_F1", flowCount1.ToString()));
            _gdpList.Add(new GeneralDiagnosticProperty("DIAGNOSTIC_PUMP_F2", flowCount2.ToString()));
            ReportDiagnostic(details, DiagnosticResources.PUMP, flowString1, flowString2, ( flowCount2 - flowCount1 < 100 ) || ( flowCount2 - flowCount1 > 450 ) );

            // Stop the pump and close the port used for this test
            Pump.CloseValve(testPort);
            Thread.Sleep(500);
        }

		/// <summary>
		/// Check if gas port solenoids work properly.
		/// </summary>
		/// <param name="details">The string to hold details.</param>
		private void TestSolenoids( DetailsBuilder details )
		{
			// close all valves and stop pump
			Pump.CloseAllValves( true );

			for ( int portNum = 1; portNum <= Configuration.DockingStation.NumGasPorts; portNum++ )
			{
				/* DIAGNOSTIC_SOLENOID_CURRENT */

				// give the valves a chance to finish closing
				Thread.Sleep( 500 );

				// get the closed current counts (c1)
				int countsClosed = Controller.Get12VCurrent();

				// open valve
				Pump.OpenValve( portNum, false );

				// wait at least 500 ms
				Thread.Sleep( 500 );

				// get the open current counts (c2)
				int countsOpen = Controller.Get12VCurrent();

				// close the valve that was opened
				Pump.CloseValve( portNum );

				// convert current counts to milliamps (mA)
				int currentClosed = CountToCurrent( countsClosed );
				int currentOpen = CountToCurrent( countsOpen );

				// DSX solenoids are designed to use 180 mA to remain open 
				int currentSolenoid = currentOpen - currentClosed;
				
				// counts for min limit provided by Engineering (Bryan Pavlisko); 
				// 20 counts is 160 mA which is approximately 10% below 180 mA
				int COUNTS_MIN_LIMIT = 20;
				bool solenoidCheckFailed = false;

				// fail if c2 - c1 < 20
				solenoidCheckFailed = ( countsOpen - countsClosed ) < COUNTS_MIN_LIMIT;

				// report the results
				_gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_SOLENOID_" + portNum + "_CURRENT_CLOSED", countsClosed.ToString() ) );
				_gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_SOLENOID_" + portNum + "_CURRENT_OPEN", countsOpen.ToString() ) );
				_gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_SOLENOID_" + portNum + "_CURRENT_MILLIAMPS", currentSolenoid.ToString() ) );
				_gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_SOLENOID_" + portNum + "_CURRENT_PASSED", ( !solenoidCheckFailed ).ToString() ) );

				// log the results
				ReportDiagnostic( details, details.GetText( "SOLENOID_CURRENT_" + portNum ), countsClosed, countsOpen, solenoidCheckFailed );
			}
		}

		/// <summary>
		/// Test the specified solenoid
		/// </summary>
		/// <param name="details">The string to hold details.</param>
		private void TestFlow( DetailsBuilder details, int solenoid )
        {
            // Validate the solenoid number
            if ( solenoid < 1 || solenoid > Configuration.DockingStation.NumGasPorts )
            {
                Log.Debug( "TestFlow:  Invalid solenoid value = " + solenoid.ToString() );
                return;
            }

            Log.Debug( "TestFlow: port=" + solenoid.ToString() );

            // Determine whether a cylinder is attached to this port.  
            // If there is one attached, skip this test.
            DockingStation ds = Controller.GetDockingStation();
            GasEndPoint gasEndPoint = ds.GasEndPoints.Find(m => m.Position == solenoid);
            if (gasEndPoint != null && gasEndPoint.Cylinder.IsFreshAir == false)
            {
                Log.Debug( "TestFlow: Cylinder attached to port " + solenoid.ToString() + "; SKIPPING THIS TEST." );
                return;
            }

            Pump.DoCheckFlow = true;

            // Ensure that only the specified solenoid is open.
            Pump.CloseAllValves( true );
            Thread.Sleep(500); // Give the valves a chance to finish closing
            Pump.OpenValve(solenoid, false); // Open the specified solenoid
            Thread.Sleep(500); // Pause at least 500ms.

            Pump.SetDesiredFlow( Pump.StandardFlowRate);
            Pump.Start( Pump.StandardStartVoltage );  // Turn on the pump.

            Thread.Sleep( 3000 ); // Wait for it to stabilize the flow before letting CheckFlow take its first reading.

            // CheckFlow could enter an infinite loop if it's unable to 
            // establish the desired flow rate and we don't tell it to time out.
            // We therefore give it a time out of a minute which should be more than sufficient.


            ushort rawFlowCounts;
            ushort rawVacuumCounts;
            Pump.FlowStatus flowStatus = Pump.CheckFlow( new TimeSpan( 0, 1, 0 ), out rawFlowCounts, out rawVacuumCounts );

            byte pumpVoltage = Pump.GetPumpVoltage(); // obtain and hold onto final voltage of the pump, to report it to inet.

            // Get the flow rate.
            ushort flowVolts = Pump.ConvertRawFlowToVolts( rawFlowCounts );

            int flowRate = Pump.CalculateFlowRate( flowVolts, rawVacuumCounts );  // Convert that value to mL/min

            // Report the results.
            string flowString = BuildFlowString(flowRate, flowVolts);
            // We create a property for every value used to compute the flow rate, and also the flow rate itself.
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_CHECK_FLOW_" + solenoid + "_VACUUM", rawVacuumCounts.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_CHECK_FLOW_" + solenoid + "_VACUUM_INCHES", Pump.ConvertRawVacuumToInches( rawVacuumCounts ).ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_CHECK_FLOW_" + solenoid, rawFlowCounts.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_CHECK_FLOW_" + solenoid + "_VOLTS", flowVolts.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_CHECK_FLOW_" + solenoid + "_RATE", flowRate.ToString() ) );
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_CHECK_FLOW_" + solenoid + "_PUMP_VOLTS", pumpVoltage.ToString() ) );
            // The flow is considered a failure if it's not equal to the StandardFlowRate plus/minus the standardtolerance
            bool flowFailed = flowRate < ( Pump.StandardFlowRate - Pump.FLOWRATE_TOLERANCE ) || flowRate > ( Pump.StandardFlowRate + Pump.FLOWRATE_TOLERANCE );
            // TODO - we should rename the translation string so that it's prefixed with "CHECK_FLOW" instead of "SOLENOID_FLOW"
            ReportDiagnostic( details, details.GetText( "SOLENOID_FLOW_" + solenoid ), flowString, flowFailed );

            // Check Pump Error Status -- FAIL IF STATE IS 1
            int pumpErrorState = Pump.GetPumpErrorState();

            // Report the results.
            _gdpList.Add( new GeneralDiagnosticProperty( "DIAGNOSTIC_CHECK_FLOW_" + solenoid + "_PUMP_ERROR", pumpErrorState.ToString() ) );
            // TODO - we should rename the translation string so that it's prefixed with "CHECK_FLOW" instead of "SOLENOID_FLOW"
            ReportDiagnostic(details, details.GetText("SOLENOID_PUMP_ERROR_" + solenoid), pumpErrorState.ToString(), (pumpErrorState == 1));

            // Stop the pump and close the solenoid
            Pump.CloseValve(solenoid);

            Pump.DoCheckFlow = false;
        }


        private void PrepValve(int valveId)
        {
            if ( valveId < 1 || valveId > Configuration.DockingStation.NumGasPorts )
            {
                Log.Debug("PrepValve given invalid valve value = " + valveId.ToString());
                return;
            }

            Pump.OpenValve(valveId, false);
            Thread.Sleep(500);
            Pump.CloseValve(valveId, false);
        }

        private void InitializeEnvironment(DetailsBuilder details)
        {
            //	Attempting to ensure a consistant beginning state - open all valves and
            //	close them. Stop the pump, stop the charger, turn off flow checking.
            Pump.DoCheckFlow = false;

            for ( int i = 1; i <= Configuration.DockingStation.NumGasPorts; i++ ) //Suresh 21-SEPTEMBER-2011 INS-2195
                PrepValve(i);

            Pump.Stop();

            // mx4/mx6... make sure gas goes to diffusion lid, not hose.  There might not be an attached
            // hose in which case the hose connection point will be closed off.
            Controller.SetCradleSolenoid(AccessoryPumpSetting.NotApplicable);
        }

        private void RestoreEnvironment(DetailsBuilder details)
        {
            // Restore flow checking and pressure.
            Pump.DoCheckFlow = true;

            Pump.Stop();

            for ( int i = 1; i <= Configuration.DockingStation.NumGasPorts; i++ ) //Suresh 21-SEPTEMBER-2011 INS-2195
                PrepValve( i );
        }


        private int CountToVoltage(int count)
        {
            return count * 3300 / 1023;
        }

		/// <summary>
		/// Converts the counts returned by the Controller.Get12VCurrent() method
		/// to milliamps (mA).
		/// </summary>
        private int CountToCurrent(int count)
        {
            return (int)(count * 3300 / 1023 * 2.5);
        }

        private string BuildCountAndUnitString( int countVal, double unitVal, int unitValDecimalPlaces, string unitLabel )
        {
            string outputString = countVal.ToString() + " (" + Controller.Round( unitVal, unitValDecimalPlaces ).ToString() + unitLabel + ")";
            return outputString;
        }

        private string BuildCountAndUnitString(int countVal, int unitVal, string unitLabel)
        {
            string outputString = countVal.ToString() + " (" + unitVal.ToString() + unitLabel + ")";
            return outputString;
        }

        private string BuildFlowString(ushort rawFlowCount, int flowRate, ushort flowVoltage)
        {
            string flowString = rawFlowCount.ToString() + " (" + flowRate.ToString() + " " + DiagnosticResources.MLMIN + ", " + flowVoltage + DiagnosticResources.MILLIVOLTS + ")";
            return flowString;
        }

        private string BuildFlowString(int flowRate, ushort flowVoltage)
        {
            string flowString = flowRate.ToString() + " " + DiagnosticResources.MLMIN + " (" + flowVoltage + DiagnosticResources.MILLIVOLTS + ")";
            return flowString;
        }


        /// <summary>
        /// Report the diagnostic to the debug port and the diagnostic details.
        /// </summary>
        /// <param name="details">The details to fill in</param>
        /// <param name="id">The id of the diagnostic</param>
        /// <param name="val">The value of the diagnostic</param>
        /// <param name="failed">Whether it failed</param>
        private void ReportDiagnostic(DetailsBuilder details, string id, object val, bool failed)
        {
            Log.Debug(details.Add("    ", id, val.ToString()));
            if (failed)
                Log.Debug(details.Add("    ", id, DiagnosticResources.FAILED.ToUpper()));
            else
                Log.Debug(details.Add("    ", id, DiagnosticResources.PASSED.ToUpper()));
        }

        /// <summary>
        /// Report the diagnostic to the debug port and the diagnostic details.
        /// </summary>
        /// <param name="details">The details to fill in</param>
        /// <param name="id">The id of the diagnostic</param>
        /// <param name="valOne">The first value of the diagnostic</param>
        /// <param name="valTwo">The second value of the diagnostic</param>
        /// <param name="failed">Whether it failed</param>
        private void ReportDiagnostic(DetailsBuilder details, string id, object valOne, object valTwo, bool failed)
        {
            Log.Debug(details.Add("    ", id, valOne.ToString()));
            Log.Debug(details.Add("    ", id, valTwo.ToString()));
            if (failed)
                Log.Debug(details.Add("    ", id, DiagnosticResources.FAILED.ToUpper()));
            else
                Log.Debug(details.Add("    ", id, DiagnosticResources.PASSED.ToUpper()));
        }

    } // end-class DiagnosticOperation

} // end-namespace