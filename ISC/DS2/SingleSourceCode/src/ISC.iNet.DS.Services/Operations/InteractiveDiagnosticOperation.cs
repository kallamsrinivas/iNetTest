using System;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Services.Resources;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define an interactive diagnostic operation.
	/// </summary>
	public class InteractiveDiagnosticOperation : InteractiveDiagnosticAction , IOperation
	{
		#region Fields

		/// <summary>
		/// The number of seconds to wait before timing out on a "press any key" prompt.
		/// </summary>
		private const int PRESS_ANY_KEY_TIMEOUT = 60;

        private bool _promptedForDockedInstrument = false;

        private ISC.iNet.DS.DomainModel.Instrument _dockedInstrument = null;

        private DetailsBuilder _details = new DetailsBuilder();

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of InteractiveDiagnosticOperation class.
		/// </summary>
        public InteractiveDiagnosticOperation() {}

        public InteractiveDiagnosticOperation( InteractiveDiagnosticAction interactiveDiagnosticAction )
            : base( interactiveDiagnosticAction )
        {
        }

		#endregion

        #region Properties 

        // SGF  19-May-2011  INS-2041 -- add this property to establish pump voltages unique to the flow offset calibration
        private static int FlowOffsetStartVoltage
        {
            get
            {
                if ( Configuration.DockingStation.Reservoir )  // Viper/iNetDS
                {
					if ( Configuration.DockingStation.Type == DeviceType.MX4 )
						return 105;

					if ( Configuration.DockingStation.Type == DeviceType.MX6 )
                        return 115;

					if ( Configuration.DockingStation.Type == DeviceType.TX1 )
						return 83;

                    if ( Configuration.DockingStation.Type == DeviceType.GBPRO || Configuration.DockingStation.Type == DeviceType.GBPLS )
                        return 85;
                }
                else  // Burton/DSX
                {
					if ( Configuration.DockingStation.Type == DeviceType.MX4 )
						return ( Configuration.DockingStation.PartNumber == Configuration.VENTISLS_PARTNUMBER ) ? 110 : 123;
					
					if ( Configuration.DockingStation.Type == DeviceType.MX6 )
                        return 110;

					if ( Configuration.DockingStation.Type == DeviceType.TX1 )
						return 100;

                    if ( Configuration.DockingStation.Type == DeviceType.GBPRO || Configuration.DockingStation.Type == DeviceType.GBPLS )
                        return 120;

                    if ( Configuration.DockingStation.Type == DeviceType.SC )
                        return 95;
                }
                return ( Pump.MaxVoltage - Pump.MinVoltage ) / 2; // default is just to return midway between min and max
            }
        }

        #endregion

		#region Methods
		
		/// <summary>
		/// Executes a docking station interactive diagnostic operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            // Initialize resource manager's culture to be same as current configuration culture.
            DiagnosticResources.Culture = Configuration.DockingStation.Language.Culture;

			// Make the return event.
            InteractiveDiagnosticEvent dsEvent = new InteractiveDiagnosticEvent( this );

			// Retrieve the docking station's complete information.
            dsEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

            // Print to log the selections made by the user
            Log.Debug( Name + ": DiagnoseKeypad = " + DiagnoseKeypad.ToString() ); // SGF  03-Jun-2011  INS-1730
            Log.Debug( Name + ": DiagnoseLcd = " + DiagnoseLcd.ToString() ); // SGF  03-Jun-2011  INS-1730
            Log.Debug( Name + ": DiagnoseLeds = " + DiagnoseLeds.ToString() );
            Log.Debug( Name + ": DiagnoseBuzzer = " + DiagnoseBuzzer.ToString() );
            Log.Debug( Name + ": DiagnoseLidSwitches = " + DiagnoseLidSwitches.ToString() );
            Log.Debug( Name + ": DiagnoseIGas = " + DiagnoseIGas.ToString() );
            Log.Debug( Name + ": DiagnoseCradleGasFlow = " + DiagnoseCradleGasFlow.ToString() ); // SGF  03-Jun-2011  INS-1730
            Log.Debug( Name + ": DiagnoseFlowRate = " + DiagnoseFlowRate.ToString() ); // SGF  03-Jun-2011  INS-1730
            Log.Debug( Name + ": DiagnoseInstrumentDetection = " + DiagnoseInstrumentDetection.ToString() );
            Log.Debug( Name + ": DiagnoseInstrumentCommunication = " + DiagnoseInstrumentCommunication.ToString() );
            Log.Debug( Name + ": DiagnoseBatteryCharging = " + DiagnoseBatteryCharging.ToString() );

            _details.AddDockingStation( dsEvent.DockingStation );

            // SGF  23-May-2011  INS-1741
            ReportDiagnosticDate();

			Pump.DoCheckFlow = false;

            for ( int valve = 1; valve <= Configuration.DockingStation.NumGasPorts; valve++ )
            {
                Master.Instance.PumpWrapper.OpenValve( valve, false );
                Thread.Sleep( 500 );
                Master.Instance.PumpWrapper.CloseValve( valve, false );
            }
            Master.Instance.PumpWrapper.Stop();
            Master.Instance.ControllerWrapper.TurnLEDsOff();
            Master.Instance.LCDWrapper.Backlight(true);
            Master.Instance.LCDWrapper.Display( "<a>               <a>" );

            if (Master.Instance.ControllerWrapper.IsDocked() )
            {
                Log.Debug( "ERROR: Remove the instrument from the cradle." );
                // Pause for the tech to remove the instrument.
                Thread.Sleep( 5000 );
            }

			Log.Debug( "Beginning interactive diagnostics." );

            // SGF  NEW CODE FOR INTERACTIVE DIAGNOSTIC CONTROL
            bool continueTesting = true;

            // SGF  03-Jun-2011  INS-1730 -- split keypad testing from LCD testing
            // Keypad
            if ( continueTesting && DiagnoseKeypad )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Keypad" );
                continueTesting = TestKeypad();
            }

            // SGF  03-Jun-2011  INS-1730 -- split keypad testing from LCD testing
            // LCD
            if ( continueTesting && DiagnoseLcd )
            {
                Log.Debug( "Interactive Diagnostics:  Testing LCD" );
                continueTesting = TestLCD();
            }

            // LEDs
            if ( continueTesting && DiagnoseLeds )
            {
                Log.Debug( "Interactive Diagnostics:  Testing LEDs" );
                TestLEDs();
            }

            // Buzzer
            if ( continueTesting && DiagnoseBuzzer )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Buzzer" );
                TestBuzzer();
            }

            // Lid Switches
            if ( continueTesting && DiagnoseLidSwitches )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Lid Switches" );
                TestLidSwitches();
            }

            // iGas Connections
            if ( continueTesting && DiagnoseIGas )
            {
                Log.Debug( "Interactive Diagnostics:  Testing iGas Connections" );
                TestiGas();
            }

            // SGF  03-Jun-2011  INS-1730 -- split cradle solenoid testing from flow rate testing
            // Cradle Solenoid
            if ( continueTesting && DiagnoseCradleGasFlow )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Cradle Gas Flow" );
                TestCradleGasFlow();
            }

            // SGF  03-Jun-2011  INS-1730 -- split cradle solenoid testing from flow rate testing
            // Flow Rate
            if ( continueTesting && DiagnoseFlowRate )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Flow Rate" );
                TestFlowRate();
            }

            // Instrument Detection
            if ( continueTesting && DiagnoseInstrumentDetection )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Instrument Detection" );
                TestInstrumentDetect();
            }
        
            // Instrument Communication
            if ( continueTesting && DiagnoseInstrumentCommunication )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Instrument Communication" );
                continueTesting = TestInstrumentCommunication();               
            }

            // Battery Charging.
            if ( continueTesting && DiagnoseBatteryCharging )
            {
                Log.Debug( "Interactive Diagnostics:  Testing Battery Charging" );
                TestBatteryCharging();
            }

            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.DONE ) );

			Pump.DoCheckFlow = true;
			Master.Instance.PumpWrapper.Stop();
            for ( int valve = 1; valve <= Configuration.DockingStation.NumGasPorts; valve++ )
            {
                Master.Instance.PumpWrapper.OpenValve( valve, false );
                Thread.Sleep( 500 );
                Master.Instance.PumpWrapper.CloseValve( valve, false );
            }
            Log.Debug( "Finished interactive diagnostics." );

			// Retrieve the details.
			dsEvent.Details = _details.ToString();

			// Write the details to a log file.
            FlashCard.WriteTextFile( LAST_RUN_DETAILS_FILE_NAME, dsEvent.Details );

            return dsEvent; // Return the event.
		}

        // SGF  23-May-2011  INS-1741
        private void ReportDiagnosticDate()
        {
            Log.Debug( _details.Add( "    ", DiagnosticResources.DETAILS_DOCKINGSTATION_SERVICE_DIAGNOSTICS_DATE, DateTime.Now ) );
        }


		/// <summary>
		/// Report the diagnostic to the debug port and the diagnostic details.
		/// </summary>
        /// <param name="label">The label of the diagnostic</param>
		/// <param name="val">The value of the diagnostic</param>
		/// <param name="passed">Whether the diagnostic passed or not.</param>
		private void ReportDiagnostic( string label , object val , bool failed )
		{
            string valString = DiagnosticResources.ResourceManager.GetString( val.ToString().ToUpper(), DiagnosticResources.Culture );
            if ( valString == null || valString.Length <= 0 )
                valString = val.ToString();

            Log.Debug( _details.Add( "    ", label, valString ) );
			if ( failed )
			{
                Log.Debug( _details.Add( "    ", label, DiagnosticResources.FAILED.ToUpper() ) );
			}
			else
			{
                Log.Debug( _details.Add( "    ", label, DiagnosticResources.PASSED.ToUpper() ) );
			}
		}

		/// <summary>
		/// Report the diagnostic to the debug port and the diagnostic details.
		/// </summary>
        /// <param name="label">The label of the diagnostic</param>
		/// <param name="val">The value of the diagnostic</param>
		/// <param name="passed">Whether it passed</param>
		private void ReportDiagnostic( string label , object valOne , object valTwo , bool failed )
		{
            string valOneString = DiagnosticResources.ResourceManager.GetString( valOne.ToString().ToUpper(), DiagnosticResources.Culture );
            if ( valOneString == null || valOneString.Length <= 0 )
                valOneString = valOne.ToString();

            string valTwoString = DiagnosticResources.ResourceManager.GetString( valTwo.ToString().ToUpper(), DiagnosticResources.Culture );
            if ( valTwoString == null || valTwoString.Length <= 0 )
                valTwoString = valTwo.ToString();

            Log.Debug( _details.Add( "    ", label, valOneString ) );
            Log.Debug( _details.Add( "    ", label, valTwoString ) );
			if ( failed )
			{
                Log.Debug( _details.Add( "    ", label, DiagnosticResources.FAILED.ToUpper() ) );
			}
			else
			{
                Log.Debug( _details.Add( "    ", label, DiagnosticResources.PASSED.ToUpper() ) );
			}
		}

		/// <summary>
		/// Report the diagnostic to the debug port and the diagnostic details.
		/// </summary>
		/// <param name="label">The label of the diagnostic</param>
		private void ReportDiagnosticSkipped( string label )
		{
			Log.Debug( _details.Add( "    ", label, DiagnosticResources.NOT_APPLICABLE.ToUpper() ) );
		}

		/// <summary>
		/// Get a key within the timeout period.
		/// </summary>
		/// <returns>The key pressed.</returns>
		private Controller.Key GetKeyTimed()
		{
			DateTime endTime;
			Controller.Key keyPressed;

			// Wait for any key to be pressed.
            endTime = DateTime.UtcNow + new TimeSpan( 0, 0, PRESS_ANY_KEY_TIMEOUT );

			keyPressed = Controller.Key.None;
            while ( ( keyPressed == Controller.Key.None ) && ( DateTime.UtcNow < endTime ) )
			{
				keyPressed = Master.Instance.ControllerWrapper.GetKeyPress().Key;
			}

			return keyPressed;
		}

        /// <summary>
        /// Gets any key within the timeout period.
        /// </summary>
        /// <returns>The key pressed.</returns>
        private Controller.Key GetAnyKey(string prompt)
        {
            return GetKey(prompt, false);
        }

        /// <summary>
        /// Gets a pass/fail key within the timeout period.
        /// </summary>
        /// <returns>The key pressed.</returns>
        private Controller.Key GetPassFailKey(string prompt)
        {
            return GetKey(prompt, true);
        }

		/// <summary>
		/// Get a key within the timeout period.
		/// </summary>
		/// <returns>The key pressed.</returns>
		private Controller.Key GetKey( string prompt, bool ignoreMiddleKey )
		{
			Controller.Key keyPressed;
			DateTime nextPrompt = DateTime.MinValue;
			TimeSpan promptInterval = new TimeSpan( 0 , 0 , 0 , 2 );

			do
			{
                if ( DateTime.UtcNow >= nextPrompt )
				{
					Master.Instance.LCDWrapper.Display( prompt );
                    nextPrompt = DateTime.UtcNow + promptInterval;
				}
				keyPressed = Master.Instance.ControllerWrapper.GetKeyPress().Key;
			}
			while ( keyPressed == Controller.Key.None || ( ignoreMiddleKey &&  keyPressed == Controller.Key.Middle ) );

			return keyPressed;
		}

		/// <summary>
		/// Gets the current language's message for the id and formats it.
		/// </summary>
		/// <param name="msgID">The id to retrieve.</param>
		/// <returns>The current language's message.</returns>
		private string GetMessage( string msgID )
		{
			string returnMsg;

            string msg = _details.GetText( msgID ); // Get the basic message.

			// If its small, return it.
			if ( msg.IndexOf( ' ' ) < 0 )
			{
				returnMsg = "<a>" + msg + "</a>";
			}
			else
			{
				// Put all of the parts together.
				returnMsg = string.Empty;
                string tmpMsg = string.Empty;

				// Get all of the parts.
				string[] msgParts = msg.Split( " ".ToCharArray() );

				foreach ( string part in msgParts )
				{
					if ( ( part != null ) && ( part != string.Empty ) )
					{
						if ( ( tmpMsg.Length + part.Length + 1 ) > 16 ) // fixed == NUM_COLS
						{
							returnMsg += "<a>" + tmpMsg + "</a>";
							tmpMsg = part;
						}
						else
						{
							if ( tmpMsg != "" )
								tmpMsg += " ";
							tmpMsg += part;
						}
					}
				}
				if ( tmpMsg != "" )
					returnMsg += "<a>" + tmpMsg + "</a>";
			}
			return returnMsg;
		}

		/// <summary>
		/// Construct a multi-line prompt.
		/// </summary>
		/// <param name="lineOne">First line.</param>
		/// <param name="lineTwo">Second line.</param>
		/// <param name="lineThree">Third line.</param>
		/// <returns>The final string, formatted for LCD.Write().</returns>
		private string GetText( string lineOne , string lineTwo , string lineThree )
		{
			string message = string.Empty;

			if ( lineOne != string.Empty )
				message += GetMessage( lineOne );

			if ( lineTwo != string.Empty )
				message += GetMessage( lineTwo );

			if ( lineThree != string.Empty )
				message += GetMessage( lineThree );

			return message;
		}

        // SGF  03-Jun-2011  INS-1730 -- separate the tests for keypad and LCD -- also simplify testing of keypad
        /// <summary>
		/// Test the basic operation of the keypad.
		/// </summary>
		private bool TestKeypad()
		{
			string message;
			Controller.Key keyPressed;

			// Test the left key.
			message = GetText(DiagnosticResources.PRESS_KEY_LEFT_PROMPT , string.Empty , string.Empty);
			Master.Instance.LCDWrapper.Display(message);
			keyPressed = GetKeyTimed();
            bool leftFailed = (keyPressed != Controller.Key.Left);
            ReportDiagnostic( DiagnosticResources.PRESS_KEY_LEFT, keyPressed, leftFailed);

			// Test the middle key.
			message = GetText(DiagnosticResources.PRESS_KEY_MIDDLE_PROMPT , string.Empty , string.Empty);
            Master.Instance.LCDWrapper.Display(message);
			keyPressed = GetKeyTimed();
            bool middleFailed = (keyPressed != Controller.Key.Middle);
            ReportDiagnostic( DiagnosticResources.PRESS_KEY_MIDDLE, keyPressed, middleFailed);

			// Test the right key.
			message = GetText(DiagnosticResources.PRESS_KEY_RIGHT_PROMPT , string.Empty , string.Empty);
            Master.Instance.LCDWrapper.Display(message);
			keyPressed = GetKeyTimed();
            bool rightFailed = (keyPressed != Controller.Key.Right);
            ReportDiagnostic( DiagnosticResources.PRESS_KEY_RIGHT, keyPressed, rightFailed);

            return (!leftFailed && !middleFailed && !rightFailed);
		}

        // SGF  03-Jun-2011  INS-1730 -- separate the tests for keypad and LCD -- also simplify testing of LCD
        /// <summary>
        /// Test the basic operation of the LCD.
        /// </summary>
        private bool TestLCD()
        {
            string message;
            Controller.Key keyPressed;

            // Display a fully black test pattern for 5 seconds
            Master.Instance.LCDWrapper.BlackScreen();
            Thread.Sleep( 5000 );

            // Turn OFF the backlight for 5 seconds.  Display a message indicating this.
            Master.Instance.LCDWrapper.Backlight(false);
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.BACKLIGHT_IS_OFF ) );
            Thread.Sleep( 5000 );

            // Turn ON the backlight for 2 seconds.  Display a message indicating this.
            Master.Instance.LCDWrapper.Backlight(true);
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.BACKLIGHT_IS_ON ) );
            Thread.Sleep( 2000 );

            // Prompt the user to indicate pass/fail for the LCD tests.
            message = GetText( DiagnosticResources.LCD_AND_BACKLIGHT_PROMPT, DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT, string.Empty );
            keyPressed = GetPassFailKey( message );
            ReportDiagnostic( DiagnosticResources.LCD_AND_BACKLIGHT_TEST, keyPressed, ( keyPressed != Controller.Key.Left ) );

            return true;
        }

        /// <summary>
		/// Test the LEDs on the front panel.
		/// </summary>
		private void TestLEDs()
		{
            // Turn OFF all LEDs for 2 seconds.  Display a message indicating this.
            Master.Instance.ControllerWrapper.TurnLEDsOff();
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.LEDS_ALL_OFF ) );
            Thread.Sleep( 2000 );

            // Turn ON the just the RED LED for 2 seconds.  Display a message indicating this.
            Master.Instance.ControllerWrapper.TurnLEDOn( Controller.LEDState.Red );
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.LED_RED_ON ) );
            Thread.Sleep( 2000 );

            // Turn ON the just the YELLOW LED for 2 seconds.  Display a message indicating this.
            Master.Instance.ControllerWrapper.TurnLEDOn( Controller.LEDState.Yellow );
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.LED_YELLOW_ON ) );
            Thread.Sleep( 2000 );

            // Turn ON the just the GREEN LED for 2 seconds.  Display a message indicating this.
            Master.Instance.ControllerWrapper.TurnLEDOn( Controller.LEDState.Green );
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.LED_GREEN_ON ) );
            Thread.Sleep( 2000 );

            // Turn OFF all LEDs for 2 seconds.  Display a message indicating this.
            Master.Instance.ControllerWrapper.TurnLEDsOff();
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.LEDS_ALL_OFF ) );
            Thread.Sleep( 2000 );

            // Prompt the user to indicate pass/fail for the LED tests.
            string message = GetText( DiagnosticResources.LED_PROMPT, DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT, string.Empty );
            Controller.Key keyPressed = GetPassFailKey( message );
            ReportDiagnostic( DiagnosticResources.LED_TEST, keyPressed, ( keyPressed != Controller.Key.Left ) );
        }

        /// <summary>
		/// Test the buzzer.
		/// </summary>
		private void TestBuzzer()
		{
            // Turn ON the buzzer for 2 seconds.  Display a message indicating this.
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.BUZZER_IS_ON ) );
            Master.Instance.ControllerWrapper.Buzz( 2 );

            // Turn OFF the buzzer for 2 seconds.  Display a message indicating this.
            Master.Instance.ControllerWrapper.TurnBuzzerOff();
            Master.Instance.LCDWrapper.Display( GetMessage( DiagnosticResources.BUZZER_IS_OFF ) );
            Thread.Sleep( 2000 );

            // Prompt the user to indicate pass/fail for the buzzer tests.
            string message = GetText( DiagnosticResources.BUZZER_TEST_PROMPT , DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT, string.Empty );
            Controller.Key keyPressed = GetPassFailKey( message );
            ReportDiagnostic( DiagnosticResources.BUZZER_TEST , keyPressed, ( keyPressed != Controller.Key.Left ) );
        }

        /// <summary>
		/// Test the iGas.
		/// </summary>
		private void TestiGas( )
		{
            string message;
            bool val;

            string gasPortsPrompt = DiagnosticResources.NUM_GAS_PORTS_PROMPT.Replace( "#", Configuration.DockingStation.NumGasPorts.ToString() );
            message = GetText( gasPortsPrompt, DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT, string.Empty );
            Controller.Key keyPressed = GetPassFailKey( message );
            bool failed = keyPressed != Controller.Key.Left; // left passes, anything else fails.
            ReportDiagnostic( DiagnosticResources.NUM_GAS_PORTS_TEST, Configuration.DockingStation.NumGasPorts.ToString(), failed );

            // If DS was serialized with wrong number of ports, then don't do any more tests.
            // The DS should be re-searialized
            if ( failed )
                return;

            // Try to detect the presence of any pressure switches or iGas cards on any of the three iGas ports.
            // If anything is detected, then log a failure.

            // Check for lack of pressure switches.
            for ( int n = 1; n <= Configuration.DockingStation.NumGasPorts; n++ )
            {
                val = Master.Instance.SmartCardWrapper.IsPressureSwitchPresent( n );
                ReportDiagnostic( _details.GetText( "NO_PRESSURE_SWITCH_" + n ), val, ( val == true ) );
            }

            // Check for lack of smart card.
            for ( int n = 1; n <= Configuration.DockingStation.NumGasPorts; n++ )
            {
                val = Master.Instance.SmartCardWrapper.IsCardPresent( n );
                ReportDiagnostic( _details.GetText( "NO_SMART_CARD_" + n ), val, ( val == true ) );
            }

            // Prompt the user to attach iGas readers to all iGas ports on the iNetDS.  It is assumed that the 
            // iGas readers contain iGas cards, and that pressure switches are also attached to the iGas readers.
            message = GetText( DiagnosticResources.CONNECT_IGAS_CONNECTOR, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty );
            GetAnyKey( message );

            // The user has pressed a key pad key to indicate that the connections are complete.  So...

            // Check for the presence of iGas cards on all three ports.
            for ( int n = 1; n <= Configuration.DockingStation.NumGasPorts; n++ )
            {
                val = Master.Instance.SmartCardWrapper.IsCardPresent( n );
                ReportDiagnostic( _details.GetText( "SMART_CARD_PRESENT_" + n ), val, ( val == false ) );
            }

			// skip pressure test if pressure switch was not detected to be present
			bool[] wasPressureSwitchPresent = new bool[Configuration.DockingStation.NumGasPorts];

            // Check for the presence of pressure switches on all three ports.
            for ( int n = 1; n <= Configuration.DockingStation.NumGasPorts; n++ )
            {
                val = Master.Instance.SmartCardWrapper.IsPressureSwitchPresent( n );
                ReportDiagnostic( _details.GetText( "PRESSURE_SWITCH_PRESENT_" + n ), val, ( val == false ) );

				wasPressureSwitchPresent[n - 1] = val;
            }

            // Check for the activition of pressure switches on all three ports.
            for ( int n = 1; n <= Configuration.DockingStation.NumGasPorts; n++ )
            {
				if ( wasPressureSwitchPresent[n - 1] )
				{
					val = Master.Instance.SmartCardWrapper.CheckPressureSwitch( n );
					ReportDiagnostic( _details.GetText( "PRESSURE_SWITCH_PRESSURE_" + n ), val, ( val == false ) );
				}
				else
				{
					ReportDiagnosticSkipped( _details.GetText( "PRESSURE_SWITCH_PRESSURE_" + n ) );
				}
            }
        }

        /// <summary>
        /// Test the cradle gas flow.
        /// </summary>
        private void TestCradleGasFlow()
        {
            // For docking station types with no cradle solenoid, there's no need to perform the test.
            // INS-7008:  Skip cradle solenoid test if docking station is MX4 and has new Ventis cradle.
            if (Configuration.DockingStation.Type == DeviceType.MX6
            || (Configuration.DockingStation.Type == DeviceType.MX4 && !Configuration.DockingStation.HasNewVentisCradle))
            {
                TestCradleGasFlowWithSolenoid();
            }
            else if (Configuration.DockingStation.Type == DeviceType.SC)
            {
                TestCradleGasFlowWithoutSolenoid();
            }
        }

        /// <summary>
        /// Test the cradle gas flow without solenoid
        /// </summary>
        private void TestCradleGasFlowWithoutSolenoid()
        {
            const int gasPort = 1;

            //Prompt the user to connect the tubing adapter
            string message = GetText(DiagnosticResources.TUBING_ADAPTER_PROMPT, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty);
            GetAnyKey(message);

            //Start the pump after adapter has confirmed as connected
            Master.Instance.PumpWrapper.OpenValve(gasPort, false);
            Master.Instance.PumpWrapper.Start(Pump.MaxVoltage); // set pump to full blast for easy detection of air flow.

            Controller.Key key = Controller.Key.None;

            //Prompt the user to verify the flow through the adapter
            message = GetText(DiagnosticResources.TUBING_ADAPTER_FLOW_PROMPT, string.Empty, DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT);
            key = GetPassFailKey(message);
            ReportDiagnostic(DiagnosticResources.TUBING_ADAPTER_FLOW, key, (key != Controller.Key.Left));

            // Stop the pump.
            Master.Instance.PumpWrapper.Stop();
            Master.Instance.PumpWrapper.CloseValve(gasPort, false);
        }

        /// <summary>
        /// Test the cradle solenoid. We need to verify that the solenoid cradle can switch in both directions
        /// </summary>
        private void TestCradleGasFlowWithSolenoid()
        {
            const int gasPort = 1;

            // Ventis LS docking stations have a type of MX4, but do not have a cradle solenoid.
            if (Configuration.DockingStation.PartNumber == Configuration.VENTISLS_PARTNUMBER)
                return;

            // Prompt to start pump.
            string message = GetText(DiagnosticResources.PRESS_ANY_TO_START_PUMP, DiagnosticResources.GAS_PORT, string.Empty);
            message = message.Replace("#", gasPort.ToString());
            GetAnyKey(message);

            Controller.Key key = Controller.Key.None;

            // Open solenoid and start the pump.
            Master.Instance.PumpWrapper.OpenValve(gasPort, false);
            Master.Instance.PumpWrapper.Start(Pump.MaxVoltage); // set pump to full blast for easy detection of air flow.

            // First switch solenoid to route air to hose.  Then attempt to route 
            // it back to the diffusion lid and verify that it successfully did so.
            Master.Instance.ControllerWrapper.SetCradleSolenoid(AccessoryPumpSetting.Installed);
            Thread.Sleep(1000);
            Master.Instance.ControllerWrapper.SetCradleSolenoid(AccessoryPumpSetting.Uninstalled);

            message = GetText(DiagnosticResources.CRADLE_SOLENOID_LID_PROMPT, string.Empty, DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT);
            key = GetPassFailKey(message);
            ReportDiagnostic(DiagnosticResources.CRADLE_SOLENOID_LID, key, (key != Controller.Key.Left));

            // Throw solenoid to direct flow to pump hose.
            Master.Instance.ControllerWrapper.SetCradleSolenoid(AccessoryPumpSetting.Installed);

            message = GetText(DiagnosticResources.CRADLE_SOLENOID_PUMP_PROMPT, string.Empty, DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT);
            key = GetPassFailKey(message);
            ReportDiagnostic(DiagnosticResources.CRADLE_SOLENOID_PUMP, key, (key != Controller.Key.Left));

            // Route air back to diffusion lid before continuing on with flow offset adjustment.
            Master.Instance.ControllerWrapper.SetCradleSolenoid(AccessoryPumpSetting.Uninstalled);

            // Stop the pump and close the solenoid.
            Master.Instance.PumpWrapper.Stop();
            Master.Instance.PumpWrapper.CloseValve(gasPort, false);
        }

        /// <summary>
		/// Test the flow rate.
		/// </summary>
		private void TestFlowRate()
        {
            const int gasPort = 2;
            ushort rawFlow;
            int flowOffset;

            // Prompt to start pump.
            string message = GetText( DiagnosticResources.PRESS_ANY_TO_START_PUMP, DiagnosticResources.GAS_PORT, string.Empty );
            message = message.Replace( "#", gasPort.ToString() );
			GetAnyKey( message );

			Controller.Key key = Controller.Key.None;

			// Open solenoid.
            Master.Instance.PumpWrapper.OpenValve( gasPort, false );
           
            // SGF  19-May-2011  INS-2041 -- establish the initial pump voltage in a manner unique to the flow offset calibration
            int pumpVoltage = FlowOffsetStartVoltage;

            // Start the pump at the specified voltage.
            Master.Instance.PumpWrapper.Start( pumpVoltage );

            // For DSX, if we just turned on the pump, we need to wait a bit to give the flow 
            // sensor time to stabilize. If we don't wait, then first flow reading will be too 
            // high. 2 seconds seems to be minimum we need to wait, based on some informal 
            // experiments I did. We sleep only 1.5 seconds here, since there is an additional
            // half second wait in the loop below. Note that there's a similar initial sleep
            // in Pump's CheckFlow routine. - JMP, 10/2014
            Thread.Sleep( 1500 );

			do
			{
                Master.Instance.PumpWrapper.SetNewPumpVoltage( Convert.ToByte( pumpVoltage ) );

                // Allow some time for the pump to adjust to the new voltage.
				Thread.Sleep( 500 );

				// Check for correct flow rating.
				rawFlow = Master.Instance.PumpWrapper.GetRawFlow();
                flowOffset = rawFlow - Pump.StandardFlowCounts;

				// Diagnostic flow adjustment.
                message = GetText( DiagnosticResources.FLOW_ADJUST, 
                                   DiagnosticResources.FLOW_LABEL + " " + rawFlow.ToString(), 
                                   DiagnosticResources.OFFSET_LABEL + " " + flowOffset.ToString() );
                key = GetAnyKey(message);

                // Compute the new pump voltage.
				switch ( key )
				{
					case Controller.Key.Left:
                        pumpVoltage += 2;
                        pumpVoltage = Math.Min( pumpVoltage, Pump.MaxVoltage );
                        if ( pumpVoltage == Pump.MaxVoltage )
                            Controller.Buzz( 0.01 );
						break;

					case Controller.Key.Right:
						pumpVoltage -= 2;
                        pumpVoltage = Math.Max( pumpVoltage, Pump.MinVoltage );
                        if ( pumpVoltage == Pump.MinVoltage )
                            Controller.Buzz( 0.01 );
						break;
				}

                Log.Debug( "*** keypad=" + key.ToString() + " pumpVoltage=" + pumpVoltage + " flowOffset=" + flowOffset + " rawFlow=" + rawFlow );
			}
			while ( key != Controller.Key.Middle );

			// Report the results.
            if ( Configuration.DockingStation.Reservoir ) // Viper / iNetDS ?
			    ReportDiagnostic(  DiagnosticResources.FLOW, rawFlow , ( rawFlow < 180 || rawFlow > 300 ) );
            else  // Burton / DSX
                ReportDiagnostic( DiagnosticResources.FLOW, rawFlow, ( rawFlow < 150 || rawFlow > 500 ) );

            flowOffset = rawFlow - Pump.StandardFlowCounts; 

            Log.Debug( "*** pumpVoltage=" + pumpVoltage + " flowOffset=" + flowOffset + " rawFlow=" + rawFlow );

            Configuration.DockingStation.FlowOffset = flowOffset;
            Configuration.SaveFlowOffset();
            Log.Debug( "RawFlow: " + rawFlow + " FlowOffset: " + Configuration.DockingStation.FlowOffset );

			// Report the results.
            ReportDiagnostic( DiagnosticResources.FLOW_OFFSET, Configuration.DockingStation.FlowOffset, false );

            // Stop pump and close the solenoid.
            Master.Instance.PumpWrapper.Stop();
            Master.Instance.PumpWrapper.CloseValve( gasPort, false );
		}

        private void TestLidSwitches()
        {
            // For docking stations with no lid switches, there's no need to perform the test.
            if ( Configuration.DockingStation.Type != DeviceType.MX6
            &&   Configuration.DockingStation.Type != DeviceType.MX4 )
                return;

            //INS-7008: If MX4 dock has new Ventis cradle, skip testing lid switches or pump adapter as new cradle does not have these.
            if (Configuration.DockingStation.HasNewVentisCradle)
                return;
            
            string message;
            bool val;

            // Prompt to lower diffusion lid.
            message = GetText( DiagnosticResources.LOWER_DIFFUSION_LID, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty );
            GetAnyKey( message );

            val =  Master.Instance.ControllerWrapper.IsDiffusionLidDown();
            ReportDiagnostic(  DiagnosticResources.DIFFUSION_LID_DETECT, val , ( val == false ) );

            // Prompt to raise diffusion lid.
            message = GetText( DiagnosticResources.RAISE_DIFFUSION_LID, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty );
            GetAnyKey( message );

            val = Master.Instance.ControllerWrapper.IsDiffusionLidDown();
            ReportDiagnostic(  DiagnosticResources.NO_DIFFUSION_LID_DETECT, !val , ( val == true ) );

			// Ventis LS docking stations should run the tests for the diffusion lid, but not for the pump adapter.
			if ( Configuration.DockingStation.PartNumber == Configuration.VENTISLS_PARTNUMBER )
				return;

            // Prompt to attach pump adapter to cradle
            message = GetText( DiagnosticResources.ATTACH_PUMP_ADAPTER, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty );
            GetAnyKey( message );

            val = Master.Instance.ControllerWrapper.IsPumpAdapterAttached();
            ReportDiagnostic( DiagnosticResources.PUMP_ADAPTER_DETECT, val , ( val == false ) );

            // Prompt to remove pump adapter from cradle.
            message = GetText( DiagnosticResources.REMOVE_PUMP_ADAPTER, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty );
            GetAnyKey( message );

            val = Master.Instance.ControllerWrapper.IsPumpAdapterAttached();
            ReportDiagnostic( DiagnosticResources.NO_PUMP_ADAPTER_DETECT, !val , ( val == true ) );
        }

		/// <summary>
		/// Test instrument detect.
		/// </summary>
		private void TestInstrumentDetect()
		{
			bool val;

			// Verify that we correctly detect that NO instrument is docked.
			val = Master.Instance.ControllerWrapper.IsDocked();
			ReportDiagnostic( DiagnosticResources.NO_INSTRUMENT_DETECT, val , ( val == true ) );

            // Prompt to dock an instrument.
            string message = GetText( DiagnosticResources.DOCK_INSTRUMENT, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty );
            GetAnyKey( message );
            _promptedForDockedInstrument = true;

            // Now that instrument is supposedly docked, verify we can detect it.
			val = Master.Instance.ControllerWrapper.IsDocked();
			ReportDiagnostic( DiagnosticResources.INSTRUMENT_DETECT, val , ( val == false ) );
		}

		/// <summary>
		/// Test instrument ping.
		/// </summary>
        /// <remarks>
        /// Attempt to ping the instrument and read its settings.
        /// </remarks>
        private bool TestInstrumentCommunication()
		{
            if ( !IsInstrumentDocked() )
                return false;

            _dockedInstrument = ReadDockedInstrument();

            ReportDiagnostic( DiagnosticResources.INSTRUMENT_PING, _dockedInstrument != null, _dockedInstrument == null );

            return _dockedInstrument != null; 
		}

        /// <summary>
        /// Causes LCD to prompt user to dock an instrument if they've not yet been prompted, or it's 
        /// detected that one is not currently docked.
        /// </summary>
        private bool IsInstrumentDocked()
        {
            // If we've already prompted, then we can just return.

            if ( _promptedForDockedInstrument )
                return true;

            // Prompt to dock an instrument, if we've not already prompted for one.
            // User will not have yet been prompted if other diagnostics that require a docked instrument 
            // have been disabled.

            string message = GetText( DiagnosticResources.DOCK_INSTRUMENT, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty );
            GetAnyKey( message );
            _promptedForDockedInstrument = true;

            //if instrument is docked return true
            if ( Master.Instance.ControllerWrapper.IsDocked() )
                return true;
            
            //report the results and return false;
            ReportDiagnostic( DiagnosticResources.INSTRUMENT_DETECT, false, true );
            return false;
        }

        /// <summary>
        /// Get instrument settings off of the docked instrument.
        /// </summary>
        private ISC.iNet.DS.DomainModel.Instrument ReadDockedInstrument()
        {
            DockingStationEvent dsEvent = null;

            try
            {
                dsEvent = new DiscoveryOperation().Execute();
            }
            catch ( HardwareConfigurationException )
            {
                // For docking station with cradle lids, we may get this exception when the docking station hardware is 
                // not configured properly.  Request the user to reconfigure the cradle, then we try to discover again.
                if ( IsDiffusionLidProperlyPositioned() )
                    dsEvent = new DiscoveryOperation().Execute();
            }
            catch ( Exception e )
            {
                Log.Error( e.ToString() );
                dsEvent = null;
            }

            // InstrumentNothingEvent won't be returned by DiscoveryOperation if nothing is docked
            return ( dsEvent is InstrumentNothingEvent ) ? ( (InstrumentNothingEvent)dsEvent ).DockedInstrument : null;
        }

        /// <summary>
        /// detects whether the diffusion lid is lowered
        /// </summary>
        private bool IsDiffusionLidProperlyPositioned()
        {
            //if instrument is docked return true
            if (Controller.IsDiffusionLidDown())
                return true;


            // else Prompt to lower diffusion lid.
            string message = GetText(DiagnosticResources.LOWER_DIFFUSION_LID, DiagnosticResources.PRESS_ANY_TO_CONTINUE, string.Empty);
            GetAnyKey(message);

            LCD.Display( "" );

            // if diffusion lid is down return true
            if ( Controller.IsDiffusionLidDown() )
                return true;

            // else report the results and return false;
            ReportDiagnostic( DiagnosticResources.NO_DIFFUSION_LID_DETECT, true, true );
            return false;  
        }

		/// <summary>
		/// Test battery charging.
		/// </summary>
		private void TestBatteryCharging()
		{
            if ( !Configuration.DockingStation.IsRechargeable() )
                return;

            if ( !IsInstrumentDocked() )
                return;

            if ( _dockedInstrument == null )
                _dockedInstrument = ReadDockedInstrument();

            if ( _dockedInstrument == null )
            {
                ReportDiagnostic( DiagnosticResources.BATTERY_INSTALLED, false /*battery installed == false*/, true /*failed*/ );
                return;
            }

			// Find the battery code.
			string batteryCode = string.Empty;
            foreach ( InstalledComponent installedComponent in _dockedInstrument.InstalledComponents )
			{
				if ( installedComponent.Component.Type.Code.StartsWith( "BP" ) ) 
					batteryCode = installedComponent.Component.Type.Code;
			}

			// Determine if a battery is installed
			if ( batteryCode == string.Empty )
                ReportDiagnostic( DiagnosticResources.BATTERY_INSTALLED, false /*battery installed == false*/, true /*failed*/ );

			// Check the battery type.
			string message = GetText( DiagnosticResources.INSTRUMENT_BATTERY_CODE_PROMPT, batteryCode , DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT );
			Controller.Key keyPressed = GetPassFailKey( message );
			ReportDiagnostic( DiagnosticResources.INSTRUMENT_BATTERY_CODE, keyPressed , ( keyPressed != Controller.Key.Left ) );

			// If correct battery type, test charging.
			if ( keyPressed == Controller.Key.Left )
			{
#if !MANUAL
                InstrumentChargingOperation chargingOp = new InstrumentChargingOperation();
                chargingOp.Execute();
                ChargingService.ChargingState chState = chargingOp.ChargingState;
                // Note that "NotCharging" is a passable state. The instrument will report a
                // phase of ChargeComplete if it's receiving charge current from the IDS but
                // isn't currently using it.  This will result in a NotCharging chargingstate.
                // The instrument will report a phase of ChargeOff if it's NOT receiving a
                // charge current from the IDS.  This is what we're interested in detecting 
                // and it results in a ChargingState.Error.
                bool failed = chargingOp.ChargingState == ChargingService.ChargingState.Error;
                ReportDiagnostic( DiagnosticResources.INSTRUMENT_CHARGING,  chState.ToString(), failed );
#else
                message = GetText( DiagnosticResources.INSTRUMENT_CHARGING_PROMPT" , batteryCode , DiagnosticResources.PRESS_LEFT_SUCCESS_PROMPT" );
                keyPressed = GetPassFailKey( message );
                ReportDiagnostic( DiagnosticResources.INSTRUMENT_CHARGING" , keyPressed , ( keyPressed != Controller.Keys.Left ) );
#endif
			}
		}

		#endregion

	}
}
