using System;
using System.Text;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet;
using ISC.iNet.DS.Services.Resources;
using ISC.WinCE;
using ISC.WinCE.Logger;
using System.Collections.Generic;

namespace ISC.iNet.DS.Services
{
	////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality for displaying docking station status.
	/// </summary>
    public sealed partial class ConsoleService : Service, IConsoleService
    {
        #region Fields

        /// <summary>
        /// Enumerates the states of the display console.
        /// </summary>
        private enum IdleScreen
        {
            DockingStation = 1,
            Version
        }
 
        /// <summary>
        /// Idle time for the thread when a menu is currently displayed. When menu is active,
        /// we want to continually poll it for button presses, so idle time is extremely short.
        /// </summary>
        private static readonly TimeSpan IDLE_TIME_MENU = new TimeSpan( 0, 0, 0, 0, 0 ); // millis

        /// <summary>
        /// Idle time for when the docking station is in an idle state.
        /// </summary>
        private static readonly TimeSpan IDLE_TIME_IDLE = new TimeSpan( 0, 0, 0, 0, 500 ); // millis

        /// <summary>
        ///  Idle time for when the DS is currently performing some operation. The console service
        ///  can remain idle for a longer period when the DS is busy since keypad can't really be
        ///  accessed anyways when DS is accessed.
        /// </summary>
        private static readonly TimeSpan IDLE_TIME_BUSY = new TimeSpan( 0, 0, 0, 0, 2000 ); // millis

        private static readonly string NOT_CONNECTED_ICON = DS.LCD.NOT_CONNECTED_ICON_CHAR.ToString() + " ";
        private static readonly string UPLOADING_ICON = DS.LCD.UPLOADING_ICON_CHAR.ToString() + " ";
        private static readonly string CHECKMARK_ICON = DS.LCD.CHECKMARK_ICON_LEFT_CHAR.ToString() + DS.LCD.CHECKMARK_ICON_RIGHT_CHAR.ToString();

        private char [] _trimBlanks = new char[] { ' ', ',' };
        private ConsoleState _currentState;
        private ConsoleState _preMenuState;
        private ConsoleState _lastState = ConsoleState.None;

        private string _currentLanguage = string.Empty;

        private bool _menuEnabled;

        private Menu _currentMenu;
        private DateTime _lastKeyPressTime = DateTime.MinValue;

        /// <summary>
        /// The OS/BSP will send an event with a name of "keypadevent" everytime a keypad button is pressed.
        /// (This event is something that the Adeneo consultant added for us back during original development of iNetDS,
        /// but we have never utilized it in the application.)
        /// </summary>
        //private OpenNETCF.Threading.EventWaitHandle _keypadevent
        //    = new OpenNETCF.Threading.EventWaitHandle( false, OpenNETCF.Threading.EventResetMode.AutoReset, "keypadevent" );

        /// <summary>
        /// How long the console service should block itself while it waits for a keypad event to be fired.
        /// </summary>
        TimeSpan _keypadEventWaitTime;

        private IdleScreen _idleScreen = 0;
        private string[] _actionMessages = new string[0];
        private string[] _lastMessages = new string[0];
        private string[] _preMenuMessages = new string[0];

        private readonly TimeSpan _tenSeconds = new TimeSpan( 0 , 0 , 10 );

        /// <summary>
        /// The last time the date & time were displayed to the LCD.
        /// </summary>
        DateTime _lastTime = DateTime.MinValue;

        object _stateLock = new object();

        private Thread _exchangeStatusThread = null;

        #endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a ConsoleService class.
		/// </summary>
        public ConsoleService( Master master ) : base( master )
        {
            _keypadEventWaitTime = IDLE_TIME_BUSY;

            // The console service never goes idle. Instead, the Run method runs continously
            // and "idleness" is done by waiting for keypad event
            IdleTime = TimeSpan.Zero;
        }

        /// <summary>
        /// Override of base class's default OnStart (the default does nothing)
        /// </summary>
        /// <remarks>
        /// Kicks off the background thread that controls the LEDs.
        /// We could just as easily kicked it off in our constructor but doing so
        /// here allows us to give the thread a name that contains our own
        /// thread's name as part of it.
        /// </remarks>
        protected override void OnStart()
        {
            StartLEDThread();
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// </summary>
        private string LCD
        {
            set //	Write the string value to the LCD.
            {
                DS.LCD.Display( value );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>TODO was internal; updated to public modifier to avoid compilation errors</remarks>
        public ConsoleState CurrentState
        {
            get
            {
                lock ( _stateLock )
                {
                    return _currentState;
                }
            }
            set
            {
                lock ( _stateLock )
                {
                    // Reset so that the next time we go Ready, we
                    // restart with the first idle screen
                    if ( ( _currentState != ConsoleState.Ready && value == ConsoleState.Ready )
                    || ( _currentState == ConsoleState.Ready && value != ConsoleState.Ready ) )
                        _idleScreen = 0;

                    _currentState = value;
                }
            }

        }


		/// <summary>
		/// Returns true if menu is currently displayed.
		/// </summary>
		public bool IsMenuActive
        {
            get { return CurrentState == ConsoleState.Menu; }
        }


        /// <summary>
        /// Indicates if we know the IDS is currently executing a 'busy' operation.
        /// i.e., "is it in the middle of doing something"?
        /// </summary>
        internal bool IsBusyState
        {
            get
            {
                lock ( _stateLock )
                {
                    ConsoleState state = CurrentState;

                    return state == ConsoleState.CalibratingInstrument
                    || state == ConsoleState.CheckingGas
                    || state == ConsoleState.ClearingInstrumentDatalog
                    || state == ConsoleState.ClearingInstrumentAlarmEvents
                    || state == ConsoleState.ClearingInstrumentManualGasOperations
                    || state == ConsoleState.Diagnosing
                    || state == ConsoleState.DiagnosingInstrument
                    || state == ConsoleState.Reset
                    || state == ConsoleState.Discovering
                    || state == ConsoleState.DownloadingInstrumentAlarmEvents
                    || state == ConsoleState.DownloadingInstrumentManualGasOperations
                    || state == ConsoleState.DownloadingInstrumentDatalog
                    || state == ConsoleState.ReadingData
                    || state == ConsoleState.ReadingInstrumentData
                    || state == ConsoleState.Starting
                    || state == ConsoleState.BumpingInstrument
                    || state == ConsoleState.UpdatingData
                    || state == ConsoleState.UpdatingInstrumentData
                    || state == ConsoleState.UpgradingFirmware
                    || state == ConsoleState.UpgradingInstrumentFirmware
                    || state == ConsoleState.UpgradingInstrumentError
                    || state == ConsoleState.UploadingDebugLog
                    || state == ConsoleState.UploadingDatabase
                    || state == ConsoleState.LidError
                    || state == ConsoleState.FlipperAndLidError
                    || state == ConsoleState.PleaseTurnOn
                    || state == ConsoleState.InstrumentSystemAlarm
                    || state == ConsoleState.Synchronization
                    || state == ConsoleState.SynchronizationError
                    || state == ConsoleState.InteractiveDiagnostics
                    || state == ConsoleState.NotSerialized
                    || state == ConsoleState.PerformingMaintenance
                    || state == ConsoleState.NoEnabledSensors
                    || state == ConsoleState.Troubleshoot
                    || state == ConsoleState.CylinderPressureReset
                    || state == ConsoleState.ReturnInstrument;                    
                }
            }
        }

        /// <summary>
        /// Returns whether or not the specified state is one where the DS should be
        /// beeping to alert the user of the state.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        private bool IsBeepingState( ConsoleState state )
        {
            switch ( state )
            {
                case ConsoleState.UnsupportedSoftware:
                case ConsoleState.UnsupportedInstrument:
                case ConsoleState.UnserializedInstrument:
                case ConsoleState.SensorError:
                case ConsoleState.SensorMissing:
                case ConsoleState.UpgradingInstrumentError:
                case ConsoleState.NoEnabledSensors:
                case ConsoleState.Unavailable:
                case ConsoleState.CalibrationFailure:
                case ConsoleState.BumpFailure:
                case ConsoleState.BumpStoppedCheckTubing:
                case ConsoleState.CalibrationStoppedCheckTubing:
                case ConsoleState.ManualCalibrationRequired:  // SGF  24-May-2012  INS-3078
                case ConsoleState.ManualBumpTestRequired:     // SGF  24-May-2012  INS-3078
                case ConsoleState.UnregisteredInstrument:
                case ConsoleState.InstrumentSystemAlarm:    // SGF  19-Oct-2010  DSW-355  (DS2 v7.6)
                case ConsoleState.ReturnDisabledInstrument:
                case ConsoleState.ReturnDockingStation:
				case ConsoleState.PrinterError:
				case ConsoleState.IGasError:
                case ConsoleState.InstrumentNotReady:               // INS-7657 RHP v7.5.1
                case ConsoleState.CheckCylinderConnections:         // INS-8446 RHP v7.6          
                case ConsoleState.BumpFailureCheckGasConnection:    // INS-6777 RHP v7.6   
                    return true;

                default:
                    return false;
            }
        }                

        /// <summary>
        /// Returns true if either the ConsoleState has changed.
        /// </summary>
        private bool IsNewState
        {
            get
            {
                lock ( _stateLock )
                {
                    return CurrentState != _lastState;
                }
            }
        }

        /// <summary>
        /// Returns true if any action messages have changed (no check of the State is made).
        /// </summary>
        private bool IsNewActions
        {
            get
            {
                lock ( _stateLock )
                {
                    if ( _actionMessages.Length != _lastMessages.Length )
                        return true;

                    for ( int i = 0; i < _actionMessages.Length; i++ )
                        if ( _actionMessages[i] != _lastMessages[i] )
                            return true;

                    return false;
                }
            }
        }

        #endregion Properties

        #region Methods

        public static ConsoleState MapHardwareConfigError(HardwareConfigurationException hce)
        {
            ConsoleState mappedState = ConsoleState.None;

            switch (hce.ErrorType)
            {
                case HardwareConfigErrorType.LidError:
                    mappedState = ConsoleState.LidError;
                    break;
                case HardwareConfigErrorType.FlipperAndLidError:
                    mappedState = ConsoleState.FlipperAndLidError;
                    break;
                case HardwareConfigErrorType.Unknown:
                    mappedState = ConsoleState.HardwareConfigError;
                    break;
            }

            return mappedState;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        private string GetBlankLines( int count )
        {
            string blanks = string.Empty;

            for ( int i = 0; i < count; i++ )
                blanks += "<a></a>";

            return blanks;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string GetSerialNumber()
        {
            // we need to trim blanks since not all languages have a serial number
            // label (e.g. German).  i.e., sn may end up looking like " 123456-23"
            // for which we want to trim the leading blank off of.
            string snMsg = string.Empty;

            string sn = Configuration.DockingStation.SerialNumber;
            if ( sn != string.Empty )
                snMsg = string.Format( "<a>{0} {1}</a>", ConsoleServiceResources.SERIAL_ABBREV, sn );

            return snMsg;
        }

		/// <summary>
		/// The delegate that determines if the instrument menu is visible.
		/// It returns true if an instrument is docked.
		/// </summary>
		/// <returns>Whether the instrument is docked.</returns>
		private bool InstrumentMenuVisible()
		{
            return Configuration.Schema.Synchronized && Master.SwitchService.IsDocked();
		}

		/// <summary>
		/// The delegate that determines if the download datalog menu is visible.
		/// It returns true if the DS type is not for GBPLS, and is in iNet mode or a USB drive is attached (for Cal Station mode).
		/// </summary>
		/// <returns>Whether or not the download datalog menu should be visible.</returns>
		private bool DownloadDatalogMenuVisible()
		{
			// GBPLS does not have a datalog so never show the menu item for that DS type.
			// If the DS is activated we can show the menu item.
			// If in Cal Station mode we need a USB drive attached to show the menu item so the datalog can be saved somewhere.
			return Configuration.DockingStation.Type != DeviceType.GBPLS && (Configuration.Schema.Activated || Controller.IsUsbDriveAttached( "MENU: " ));
		}

		/// <summary>
		/// The delegate that determines if the troubleshoot menu is visible.
		/// It returns true if a USB drive is attached.
		/// </summary>
		/// <returns>Whether or not the troubleshoot menu should be visible.</returns>
		private bool TroubleshootMenuVisible()
		{
			return Controller.IsUsbDriveAttached( "MENU: " );
		}

		/// <summary>
		/// The delegate that determines if the force gas retry menu is visible.
		/// It returns true if a manifold/manual cylinder is assigned and empty.
		/// </summary>
		/// <returns>Whether or not the force gas retry menu should be visible.</returns>
		private bool ForceGasRetryMenuVisible()
		{
			return Controller.GetDockingStation().GasEndPoints.FindAll( m => ( m.InstallationType == GasEndPoint.Type.Manifold || 
																		m.InstallationType == GasEndPoint.Type.Manual ) &&
																		m.Cylinder.Pressure == PressureLevel.Empty ).Count > 0;
		}

        /// <summary>
        /// Typically, the menu is considered enabled as long as the keypad is not locked, and the 
        /// docking station isn't busy doing something.
        /// </summary>
        public bool MenuEnabled
        {
            get
            {
                if ( Configuration.DockingStation.MenuLocked
                ||   Master.ChargingService.State == ChargingService.ChargingState.Error  // INS-3336, 3/2013 - Don't allow menu access if in battery error state.
                ||   IsBusyState                
				||   CurrentState == ConsoleState.SensorError
				||   CurrentState == ConsoleState.SensorMissing
				||   CurrentState == ConsoleState.MfgNotConnected	// SGF  24-Jul-2012  DEV JIRA INS-4699
				||   CurrentState == ConsoleState.ReturnDisabledInstrument
				||   CurrentState == ConsoleState.ReturnDockingStation
                ||   CurrentState == ConsoleState.PrinterError
                ||   CurrentState == ConsoleState.ContactISCCode10110                  // INS-8446 RHP v7.6
                ||   CurrentState == ConsoleState.ContactISCCode1011                   // INS-8446 RHP v7.6
                ||   CurrentState == ConsoleState.ContactISCCode1012                   // INS-8446 RHP v7.6
                ||   CurrentState == ConsoleState.ContactISCCode1014                   // INS-8446 RHP v7.6
                ||   CurrentState == ConsoleState.ContactISCCode1018                   // INS-8446 RHP v7.6
                ||   CurrentState == ConsoleState.ContactISCCode10160)                  // INS-8446 RHP v7.6              
                    return false;

                return _menuEnabled;
            }
            set { _menuEnabled = value; }
        }

		/// <summary>
		/// The delegate that determines if the info menu is visible.
		/// It returns true if the code is alpha, beta, or dev.
		/// </summary>
		/// <returns>Whether the code is in testing.</returns>
		private bool InfoMenuVisible()
		{
			return true;
		}

        /// <summary>
        /// 
        /// </summary>
		private void TestCharacters()
		{
			DS.LCD.DisplayTestCharacters();
		}

        /// <summary>
        /// The delegate used to force a a reset to factory defaults.
        /// </summary>
        private void ForceFactoryReset()
        {
            UpdateState( ConsoleState.Reset );

            //	Write the message to the LCD.
            LCD = GetMessage( CurrentState.ToString() ) + GetBlankLines( 2 ) + GetSerialNumber();

            TurnLEDOn( new Controller.LEDState[] { Controller.LEDState.Yellow } );

            FactoryResetAction action = new FactoryResetAction();
            action.Trigger = TriggerType.Forced;

            //	Create and execute the action.
            Master.ExecuterService.ExecuteNow( action, false );
        }

		/// <summary>
		/// The delegate used to force a bump test on the IDS.
		/// </summary>
		private void ForceBumpTest()
		{
			UpdateState( ConsoleState.CheckingGas );
            Master.Instance.Scheduler.ForceEvent( EventCode.BumpTest, false );
		}

		/// <summary>
		/// The delegate used to force a calibration on the IDS.
		/// </summary>
		private void ForceCalibrate()
		{
			UpdateState( ConsoleState.CheckingGas );
            Master.Instance.Scheduler.ForceEvent( EventCode.Calibration, false );
		}

		/// <summary>
		/// The delegate used to force a download datalog on the IDS.
		/// </summary>
		private void ForceDownloadDatalog()
		{
			UpdateState( ConsoleState.DownloadingInstrumentDatalog );
			Master.Instance.Scheduler.ForceEvent( EventCode.DownloadDatalog, false );
		}

		/// <summary>
		/// The delegate used to the force a diagnostic on the IDS.
		/// </summary>
		private void ForceDiagnostic()
		{
            UpdateState( ConsoleState.Diagnosing );
            Master.Instance.Scheduler.ForceEvent( EventCode.Diagnostics, false );
		}

        /// <summary>
		/// The delegate used to force a diagnostic on the IDS.
		/// </summary>
		private void ForceHeartbeat()
		{
            UpdateState( ConsoleState.Synchronization );
            Master.ExecuterService.HeartBeat();
		}

		/// <summary>
		/// The delegate used to force a troubleshoot operation.
		/// </summary>
		private void ForceTroubleshoot()
		{
			UpdateState( ConsoleState.Troubleshoot );
            Master.Instance.Scheduler.ForceEvent( EventCode.Troubleshoot, false );
		}

		/// <summary>
		/// The delegate used to force a force gas retry/cylinder pressure reset operation.
		/// </summary>
		private void ForceCylinderPressureReset()
		{
			UpdateState( ConsoleState.CylinderPressureReset );
			Master.Instance.Scheduler.ForceEvent( EventCode.CylinderPressureReset, true );
		}
		

        //Suresh 17-JAN-2012 INS-2506
        private string GetCylinderString(int position)
        {
            string cylinderMsg = string.Empty;

            GasEndPoint gasEndPoint = Configuration.DockingStation.GasEndPoints.Find( delegate ( GasEndPoint gep ) { return gep.Position == position; } );

            const string fmt = "{0}: {1}"; // format specifier string
            string cylString = string.Empty; // string we will format and then return

            if ( gasEndPoint == null )
                cylString = string.Format( fmt, position, ConsoleServiceResources.NOCYLINDER );

            else if ( position == 1)
            {
                // Either zero air OR fresh air is allowed?
                if (Configuration.DockingStation.Port1Restrictions == (PortRestrictions.FreshAir | PortRestrictions.ZeroAir))
                {
                    if ( gasEndPoint.Cylinder.IsFreshAir )
                        cylString = string.Format( fmt, position, ConsoleServiceResources.FRESH_AIR );
                    else if ( gasEndPoint.Cylinder.IsZeroAir )
                        cylString = string.Format( fmt, position, ConsoleServiceResources.ZEROAIR );
                    else
                        cylString = string.Format( fmt, position, ConsoleServiceResources.NOCYLINDER );
                }
                // Only fresh air is allowed?  It's illegal to have a non-freshAir cylinder is installed.
                else if (Configuration.DockingStation.Port1Restrictions == PortRestrictions.FreshAir)
                {
                    if (gasEndPoint.Cylinder.IsFreshAir)
                        cylString = string.Format( fmt, position, ConsoleServiceResources.FRESH_AIR );
                    else
                        cylString = string.Format( fmt, position, ConsoleServiceResources.NOCYLINDER );
                }
                // Only zero air is allowed? It's illegal to have either fresh air or a non-zeroAir
                // cylinder installed.
                else if (Configuration.DockingStation.Port1Restrictions == PortRestrictions.ZeroAir)
                {
                    if (gasEndPoint.Cylinder.IsZeroAir)
                        cylString = string.Format( fmt, position, ConsoleServiceResources.ZEROAIR );
                    else
                        cylString = string.Format( fmt, position, ConsoleServiceResources.NOCYLINDER );
                }
            }

            else
                cylString = string.Format( fmt, position, gasEndPoint.Cylinder.PartNumber );

            return cylString.PadRight( DS.LCD.MAX_COLUMNS );
        }

        // SGF  1-Nov-2012  INS-2592
        // iNet DS menus have been reworked as part of implementation of INS-2592 (presenting the account number in the menu).
        // In addition, checks are made to determine if the instrument docked at the time the menu is being created is 
        // removed prior to the completion of construction of the menu.  If it is undocked, the instrument portion of the 
        // menu structure is omitted.
        //
		/// <summary>
		/// Construct the menus for the iNet DS.
		/// </summary>
		private void ConstructMenus()
		{
			Menu rootMenu = new Menu();

            if (Controller.IsDocked())
            {
                // Build and add the Instrument submenu.  Discontinue adding the Instrument submenu if
                // at any point the instrument is undocked.  This will be made known through the return value;
                // if the returned menu is null, the instrument was undocked.
                Menu instrumentMenu = ConstructInstrumentMenu(rootMenu);
                if (instrumentMenu != null)
                    rootMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_INSTRUMENT, true, new MenuItem.VisibleFunction(InstrumentMenuVisible), null, instrumentMenu));
            }

            // Build and add the Docking Station submenu
            Menu dockingStationMenu = ConstructDockingStationMenu(rootMenu);
            rootMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_DOCKINGSTATION, true, null, null, dockingStationMenu));

            // Build and add the iNet submenu
            Menu inetMenu = ConstructInetMenu( rootMenu );
            rootMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_INET, true, null, null, inetMenu ) );

			_currentMenu = rootMenu;
		}


        /// <summary>
        /// Construct the Instrument menus for the iNet DS.
        /// </summary>
        private Menu ConstructInstrumentMenu(Menu backMenu)
        {
            Menu instrumentMenu = new Menu();

            // Ensure the instrument remains docked
            if (!Controller.IsDocked()) 
                return null;

            // Build and add the Bump Test menu.  If null is returned, discontinue menu creation.
            Menu bumpTestMenu = ConstructBumpTestMenu(instrumentMenu);
            if (bumpTestMenu != null)
                instrumentMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_BUMP_TEST, true, null, null, bumpTestMenu));
            else
                return null;

            // Build and add the Calibration menu.  If null is returned, discontinue menu creation.
            Menu calMenu = ConstructCalibrationMenu(instrumentMenu);
            if (calMenu != null)
                instrumentMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_CALIBRATE, true, null, null, calMenu));
            else
                return null;

			// Build and add the Download Datalog menu.  Visible for iNet mode or Cal Station mode when a USB drive is attached.
			Menu downloadDatalogMenu = ConstructDownloadDatalogMenu( instrumentMenu );
			if ( downloadDatalogMenu != null )
				instrumentMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_DOWNLOAD_HYGIENE, true, new MenuItem.VisibleFunction(DownloadDatalogMenuVisible), null, downloadDatalogMenu ) );
			else
				return null;
			
            // Build and add the Instrument Information menu.  If null is returned, discontinue menu creation.
            Menu instInfoMenu = ConstructInstrumentInformationMenu(instrumentMenu);
            if (instInfoMenu != null)
                instrumentMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_INFORMATION, true, null, null, instInfoMenu));
            else
                return null;

            //	Put in a blank line and a link to the previous menu.
            instrumentMenu.MenuItems.Add(new MenuItem("", false, null, null, null));
            instrumentMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu));

            // Ensure the instrument remains docked
            if (!Controller.IsDocked()) 
                return null;

            return instrumentMenu;
        }

        /// <summary>
        /// Construct the instrument bump test submenu for the iNet DS.
        /// </summary>
        private Menu ConstructBumpTestMenu(Menu backMenu)
        {
            // Build an "Are You Sure, Yes/No" prompt, and add the forced bump test action for the Yes option.
            Menu bumpMenu = new Menu();
            bumpMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_ARE_YOU_SURE, false, null, null, null));
            bumpMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_YES, true, null, new MenuItem.ActivateFunction(ForceBumpTest), null));
            bumpMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_NO, true, null, null, backMenu));
            return bumpMenu;
        }

        /// <summary>
        /// Construct the instrument calibration submenu for the iNet DS.
        /// </summary>
        private Menu ConstructCalibrationMenu(Menu backMenu)
        {
            // Build an "Are You Sure, Yes/No" prompt, and add the forced calibration action for the Yes option.
            Menu calMenu = new Menu();
            calMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_ARE_YOU_SURE, false, null, null, null));
            calMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_YES, true, null, new MenuItem.ActivateFunction(ForceCalibrate), null));
            calMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_NO, true, null, null, backMenu));
            return calMenu;
        }

		/// <summary>
		/// Construct the instrument download datalog submenu for the iNet DS.
		/// </summary>
		private Menu ConstructDownloadDatalogMenu( Menu backMenu )
		{
			// Build an "Are You Sure, Yes/No" prompt, and add the forced download datalog action for the Yes option.
			Menu datalogMenu = new Menu();
			datalogMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_ARE_YOU_SURE, false, null, null, null ) );
			datalogMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_YES, true, null, new MenuItem.ActivateFunction( ForceDownloadDatalog ), null ) );
			datalogMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_NO, true, null, null, backMenu ) );
			return datalogMenu;
		}

        /// <summary>
        /// Construct the instrument information "menu" for the iNet DS.
        /// </summary>
        private Menu ConstructInstrumentInformationMenu(Menu backMenu)
        {
            Menu instInfoMenu = new Menu();

            // Ensure the instrument remains docked
            if (!Controller.IsDocked()) 
                return null;

            // Add the "title" for Instrument, and a blank line.
            instInfoMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_INSTRUMENT, false, null, null, null));
            instInfoMenu.MenuItems.Add(new MenuItem("", false, null, null, null));

            // Ensure the instrument remains docked
            if (!Controller.IsDocked())
                return null;

            // Ensure the switch service has been created
            if (Master.SwitchService == null)
                return null;

            // Construct the serial number line and add it to the screen.
            string serialLine = string.Format("{0} {1}", ConsoleServiceResources.SERIAL_ABBREV, Master.SwitchService.Instrument.SerialNumber);
            instInfoMenu.MenuItems.Add(new MenuItem(serialLine, false, null, null, null));

            // Ensure the instrument remains docked
            if (!Controller.IsDocked())
                return null;

            // Construct the instrument firmware version line and add it to the screen.
            string versionLine = string.Format("{0}{1}", ConsoleServiceResources.VERSION_ABBREV, Master.SwitchService.Instrument.SoftwareVersion);
            instInfoMenu.MenuItems.Add(new MenuItem(versionLine, false, null, null, null));

            // Commented this below. The instrument information is showing docking station type.
            // When it supposed to change to instrument type, uncomment below lines and comment docking station type block
            //
            // Construct the instrument type/subtype information and add it to the screen
            /*string instrumentType = (Master.SwitchService.Instrument.Subtype != DeviceSubType.None && Master.SwitchService.Instrument.Subtype != DeviceSubType.Undefined)
                ? Master.SwitchService.Instrument.Subtype.ToString() : Master.SwitchService.Instrument.Type.ToString();
            string instrumentTypeLine = GetText(instrumentType, true);
            instInfoMenu.MenuItems.Add(new MenuItem(instrumentTypeLine, false, null, null, null)); */

            //Construct the docking station type information and add it to the screen
            string dockingStationTypeLine = GetDeviceTypeDisplayText(Configuration.DockingStation.Type);
            instInfoMenu.MenuItems.Add(new MenuItem(dockingStationTypeLine, false, null, null, null));

            // Add a blank line and a link back to the parent submenu.
            instInfoMenu.MenuItems.Add(new MenuItem("", false, null, null, null));
            instInfoMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu));

            // Ensure the instrument remains docked
            if (!Controller.IsDocked()) 
                return null;

            return instInfoMenu;
        }

        /// <summary>
        /// Construct the Docking Station menu for the iNet DS.
        /// </summary>
        private Menu ConstructDockingStationMenu(Menu backMenu)
        {
            Menu dockingStationMenu = new Menu();

            // If the DS is activated, or we're in service mode, then add a menu item to diagnose the docking station.
            // It doesnt make sense to offer diagnostic menu option when in cal station mode since user is not
            // informed of diagnostic failure, and results are not uploaded to iNet.
            if (Configuration.Schema.Activated || Configuration.ServiceMode )
            {
                MenuItem diagnoseMenuItem = ConstructDiagnoseMenuItem();
                dockingStationMenu.MenuItems.Add(diagnoseMenuItem);
            }

            // Build and add the docking station information submenu.
            Menu dsInfoMenu = ConstructDockingStationInformationMenu(dockingStationMenu);
            dockingStationMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_INFORMATION, true, null, null, dsInfoMenu));

            // Build and add the cylinders submenu.
            Menu cylindersMenu = ConstructCylindersPorts1To3Menu(dockingStationMenu);
            dockingStationMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_CYLINDER, true, null, null, cylindersMenu));

			// Add the force gas retry menu item if there is an assigned manifold/manual cylinder that is currently empty.
			dockingStationMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_FORCE_GAS_RETRY, true, new MenuItem.VisibleFunction(ForceGasRetryMenuVisible), new MenuItem.ActivateFunction(ForceCylinderPressureReset), null ) );

			// Add the troubleshoot menu item if a USB drive is attached.
			dockingStationMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_TROUBLESHOOT, true, new MenuItem.VisibleFunction(TroubleshootMenuVisible), new MenuItem.ActivateFunction(ForceTroubleshoot), null ) );

            //	Put in a blank line and a link to the previous menu.
            dockingStationMenu.MenuItems.Add(new MenuItem("", false, null, null, null));
            dockingStationMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu));

            return dockingStationMenu;
        }

        /// <summary>
        /// Construct the Docking Station menu for the iNet DS.
        /// </summary>
        private Menu ConstructInetMenu( Menu backMenu )
        {
            Menu inetMenu = new Menu();

            inetMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_REFRESH, true, null, new MenuItem.ActivateFunction(ForceHeartbeat), null ) );

            // Build and add the account submenu.
            Menu accountMenu = ConstructAccountMenu( inetMenu );
            inetMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_ACCOUNT, true, null, null, accountMenu ) );

            //	Put in a blank line and a link to the previous menu.
            inetMenu.MenuItems.Add( new MenuItem( "", false, null, null, null ) );
            inetMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu ) );

            return inetMenu;
        }

        /// <summary>
        /// Construct the (Docking Station) Diagnose menu item for the iNet DS.
        /// </summary>
        private MenuItem ConstructDiagnoseMenuItem()
        {
            MenuItem diagnoseMenuItem = new MenuItem(ConsoleServiceResources.MENU_DIAGNOSE, true, null, new MenuItem.ActivateFunction(ForceDiagnostic), null);
            return diagnoseMenuItem;
        }

        /// <summary>
        /// Construct the docking station information "menu" for the iNet DS.
        /// </summary>
        private Menu ConstructDockingStationInformationMenu(Menu backMenu)
        {
            Menu dsInfoMenu = new Menu();

            // Add the "title" for Docking Station.
            dsInfoMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_DOCKINGSTATION, false, null, null, null));

            string dockingStationTypeLine = GetDeviceTypeDisplayText(Configuration.DockingStation.Type);
            dsInfoMenu.MenuItems.Add(new MenuItem(dockingStationTypeLine, false, null, null, null));

            // Construct the serial number line and add it to the screen.
            string sn = string.Empty;
            if ( Configuration.DockingStation.SerialNumber != string.Empty )
                sn = string.Format( "{0} {1}", ConsoleServiceResources.SERIAL_ABBREV, Configuration.DockingStation.SerialNumber );

            dsInfoMenu.MenuItems.Add(new MenuItem(sn, false, null, null, null));

            // Construct the instrument firmware version line and add it to the screen.
            string versionLine = string.Format("{0}{1}", ConsoleServiceResources.VERSION_ABBREV, Controller.FirmwareVersion);
            dsInfoMenu.MenuItems.Add(new MenuItem(versionLine, false, null, null, null));

            // Construct the IP address line and add it to the screen.
            NetworkAdapterInfo nic = Controller.GetWiredNetworkAdapter();
            string ipAddress = nic.IpAddress;
            dsInfoMenu.MenuItems.Add(new MenuItem(ipAddress, false, null, null, null));

            // Construct the iNet status line and add it to the screen.
            string inetStatus = GetInetStatusString();
            dsInfoMenu.MenuItems.Add(new MenuItem(inetStatus, false, null, null, null));

            // Add a blank line and a link back to the parent submenu.
            dsInfoMenu.MenuItems.Add(new MenuItem("", false, null, null, null));
            dsInfoMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu));

            return dsInfoMenu;
        }

        /// <summary>
        /// Construct the Cylinders menu for the iNet DS, for ports 1 to 3.
        /// </summary>
        private Menu ConstructCylindersPorts1To3Menu(Menu backMenu)
        {
            Menu cylindersMenu = new Menu();

            // Add the "title" for Cylinders, and a blank line.
            cylindersMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_CYLINDER, false, null, null, null));
            cylindersMenu.MenuItems.Add(new MenuItem("", false, null, null, null));

            // Add the first cylinder.
            cylindersMenu.MenuItems.Add(new MenuItem(GetCylinderString(1), false, null, null, null));

            // Add the second cylinder.
            cylindersMenu.MenuItems.Add(new MenuItem(GetCylinderString(2), false, null, null, null));

            // Add the third cylinder.
            cylindersMenu.MenuItems.Add(new MenuItem(GetCylinderString(3), false, null, null, null));

            // Add a blank line and a link back to the parent submenu.
            cylindersMenu.MenuItems.Add(new MenuItem("", false, null, null, null));

            if ( Configuration.DockingStation.NumGasPorts > 3 )
            {
                // Build and add the account submenu.
                Menu moreMenu = ConstructCylindersPorts4To6Menu( backMenu );
                cylindersMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_MORE, true, null, null, moreMenu ) );
            }
            else
                cylindersMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu ) );

            return cylindersMenu;
        }

        /// <summary>
        /// Construct the Cylinders menu for the iNet DS, for ports 4 to 6.
        /// </summary>
        private Menu ConstructCylindersPorts4To6Menu( Menu backMenu )
        {
            Menu cylindersMenu = new Menu();

            // Add the "title" for Cylinders, and a blank line.
            cylindersMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_CYLINDER, false, null, null, null ) );
            cylindersMenu.MenuItems.Add( new MenuItem( "", false, null, null, null ) );

            // Add the first cylinder.
            cylindersMenu.MenuItems.Add( new MenuItem( GetCylinderString( 4 ), false, null, null, null ) );

            // Add the second cylinder.
            cylindersMenu.MenuItems.Add( new MenuItem( GetCylinderString( 5 ), false, null, null, null ) );

            // Add the third cylinder.
            cylindersMenu.MenuItems.Add( new MenuItem( GetCylinderString( 6 ), false, null, null, null ) );

            // Add a blank line and a link back to the parent submenu.
            cylindersMenu.MenuItems.Add( new MenuItem( "", false, null, null, null ) );
            cylindersMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu ) );

            return cylindersMenu;
        }

        /// <summary>
        /// Construct the account "menu" for the iNet DS.
        /// </summary>
        private Menu ConstructAccountMenu(Menu backMenu)
        {
            Menu accountMenu = new Menu();

            // Add the "title" for Account Number and a blank line.
            accountMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_ACCOUNTNUMBER, false, null, null, null));
            accountMenu.MenuItems.Add(new MenuItem("", false, null, null, null));

            // Construct the account number line and add it to the screen.
            string accountNumber = Configuration.Schema.AccountNum;
            if (accountNumber.Length <= 0)
                accountNumber = ConsoleServiceResources.NOTACTIVATED;
            accountMenu.MenuItems.Add(new MenuItem(accountNumber, false, null, null, null));

            // Add two blank lines and a link back to the parent submenu.
            accountMenu.MenuItems.Add(new MenuItem("", false, null, null, null));
            accountMenu.MenuItems.Add(new MenuItem("", false, null, null, null));
            accountMenu.MenuItems.Add(new MenuItem(ConsoleServiceResources.MENU_PREVIOUS, true, null, null, backMenu));

            return accountMenu;
        }
        
        /// <summary>
        /// Construct the factory reset for the IDS.
        /// </summary>
        private void ConstructResetMenu()
        {
            Menu rootMenu = new Menu();

            //	forced actions and a previous menu item.
            rootMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_FACTORY_RESET1, false, null, null, null ) );
            rootMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_FACTORY_RESET2, false, null, null, null ) );
            rootMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_FACTORY_RESET3, false, null, null, null ) );
            rootMenu.MenuItems.Add( new MenuItem( string.Empty, false, null, null, null ) ); // Add a blank line
            rootMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_YES, true, null, new MenuItem.ActivateFunction( ForceFactoryReset ), null ) );
            rootMenu.MenuItems.Add( new MenuItem( ConsoleServiceResources.MENU_NO, true, null, null, null ) );
            rootMenu.Selected = rootMenu.MenuItems.Count - 1;  // set MENU_NO as selected by default.

            // Set the current menu to the newly constructed menus.
            _currentMenu = rootMenu;
        }

		/// <summary>
		/// Switchs the console service's current language.
		/// </summary>
		/// <param name="code">The language code for the new language.</param>
		private void SwitchLanguage( string code )
		{
			//	Set the current language.
			Log.Trace( "Switch Language( " + code + " )" );

            lock ( ConsoleServiceResources.ResourceManager ) // // we do this in case GetText is called by another thread.
            {
                _currentLanguage = code;

                if ( ConsoleServiceResources.Culture == null // not ever set yet?
                ||   ConsoleServiceResources.Culture.Name != Configuration.DockingStation.Language.Culture.Name ) // name will "en" or "fr", etc.
                {
                    ConsoleServiceResources.Culture = Configuration.DockingStation.Language.Culture;
                    ConsoleServiceResources.ResourceManager.ReleaseAllResources();// release cached strings for previous language
                }
            }
         
			// Construct the menus in the new language.
			Log.Trace( "Constructing menus." );

			ConstructMenus();
		}

		/// <summary>
		/// Display the ready screen.
		/// </summary>
		private void DisplayReady()
		{
            Log.Trace( "DISPLAY READY" );

            if ( _idleScreen == 0 || _idleScreen >= IdleScreen.Version )
                _idleScreen = IdleScreen.DockingStation;
            else
                _idleScreen = _idleScreen + 1;

            int blanksNeeded = 3;

            StringBuilder message = new StringBuilder();

            switch ( _idleScreen )
            {
                // v1.2.3 
				// Ventis
                case IdleScreen.Version:
                    message.Append("<a>");
                    message.Append(string.Format("{0}{1}", ConsoleServiceResources.VERSION_ABBREV, Controller.FirmwareVersion));
                    message.Append("</a>");

                    message.Append("<a>");
                    message.Append(GetDeviceTypeDisplayText(Configuration.DockingStation.Type));
                    message.Append("</a>");

                    break;

                // Docking Station
                // 111.222.333.444
                default :
                    NetworkAdapterInfo nic = Controller.GetWiredNetworkAdapter();
                    message.Append( "<a>" );
                    message.Append( Configuration.Schema.Activated ? ConsoleServiceResources.DOCKINGSTATION : ConsoleServiceResources.CALSTATION );
                    message.Append( "</a>" );
                    
                    message.Append( "<a>" );
                    message.Append( nic.IpAddress );
                    message.Append( "</a>" );
                    break;
            }

            message.Append( GetBlankLines( 1 ) );

            bool shouldServiceInstrumentSoon = Master.Instance.SwitchService.Instrument.ShouldServiceSoon( Configuration.IsSingleSensorMode() );

            ChargingService.ChargingState chargingState = Master.ChargingService.State;

			if ( !shouldServiceInstrumentSoon )
            {
                // add check state of charger and adjust message accordingly
                string chargingMessage = SetAppropriateLEDs( chargingState );
                if (chargingMessage.Length > 0)
                {
                    blanksNeeded--;
                    message.Append(chargingMessage);
                }
                // Always diplay iNet status when activated or in service mode. If cal station mode,
                // display it when we're online but not offline (we don't want to constantly
                // show offline indicator when in cal station mode).
                if ( Configuration.Schema.Activated || Configuration.ServiceMode || Inet.IsOnline )
                {
                    blanksNeeded--;
                    string inetStatus = GetInetStatusString();
                    message.Append( DS.LCD.WrapString( inetStatus ) );
                }
            }
            else // This is a DualSense capable instrument with one DualSense cal-failed sensor (or one DualSense bump failed O2 sensor) that is designated to still be usable by the customer.
            {
                TurnLEDOn(new Controller.LEDState[] { Controller.LEDState.Red, Controller.LEDState.Green });
                message.Append(GetBlankLines(1));
                message.Append(DS.LCD.WrapString(ConsoleServiceResources.SERVICEINSTRUMENTSOON));
                blanksNeeded--;
                blanksNeeded--;
            }

            // always precede serial num with at least one blank line
            blanksNeeded = Math.Max( blanksNeeded, 1 );

            message.Append( GetBlankLines( blanksNeeded ) );
            message.Append( GetSerialNumber() );

            //DS.LCD.Display( message.ToString() );
            DS.LCD.DisplayReady(message.ToString());
		}

        private string GetInetStatusString()
        {
            string icon;

            // If 'connected to iNet', then show a checkmark.  Otherwise, show an 'x' mark.
            // By 'connected', we mean that (a) the docking station has a network connection, and 
            // (b) is successfully able to BOTH receive downloaded information from iNet, AND upload 
            // information to iNet. 
            if ( ReporterService.Networked && Inet.IsOnline )
            {
                // Show the uploading icon if data is pending upload, AND if the docking station is 
                // capable of uploading.  Otherwise, show the checkmark.
                icon = ( Configuration.Schema.Activated && Master.ReporterService.PendingUploads ) ? UPLOADING_ICON : CHECKMARK_ICON;
            }
            else
            {
                // Something is preventing the docking station from being fully able to communicate with iNet.
                // Display the 'x' mark.
                icon = NOT_CONNECTED_ICON;
            }

            if ( Configuration.ServiceMode )
                return icon + ConsoleServiceResources.INET + " *";

            return icon + ConsoleServiceResources.INET;
        }

        /// <summary>
        /// Intended to be called just one time during startup.
        /// </summary>
        public void InitializeState()
        {
            Log.Assert( CurrentState == ConsoleState.None, Name + ".InitializeState called after state is already initialized." );

            UpdateState( ConsoleState.Starting );

            /// Calling HandleState now will force the display to refresh and say "Starting" 
            /// right away. If we don't do this, it remains blank for a second or two.
            HandleStateChange();
        }

		/// <summary>
		/// Handle the change in states.
		/// </summary>
		private void HandleStateChange()
		{
            // Get a local copy of CurrentState since between now and the end of this method,
            // the public property may likely change.  It also lets us call the CurrentState
            // property less often which is good since it perform a 'lock' everytime it's called.
            ConsoleState currentState = CurrentState;

            bool stateChanged = currentState != _lastState;

            //	Buzz the indicated amount of time.

            bool isNewState = IsNewState;

            if ( isNewState || ( !isNewState && !IsNewActions ) ) // don't beep if state hasn't changed and only the action messages have.
            {
                if ( IsBeepingState( currentState ) )
                    EnableBeep( true );  // start beeping (if we're not already beeping)
                else
                {
                    EnableBeep( false ); // stop beeping (if we're already currently beeping).
                    Controller.Buzz( 0.01 ); // just perform a short "chirp" when the state changes.
                }
            }

            //	Precreate this string, everything below needs it.
            string stateString = GetMessage( currentState.ToString().ToUpper() );

			//	Change the state records.
			_lastState = currentState;
            _lastMessages = _actionMessages;

			//	Do the appropriate task for the new state.
            Log.Debug( "Handling " + currentState + " state." );

            string actionMsg = FormatActionMessage();

            switch ( currentState )
			{
				case ConsoleState.Starting :
									
					//	Write appropriate message to LCD.
                    LCD = string.Format("<a>{0} {1}</a><a>{2}</a>{3}<a>{4}{5}</a>{6}{7}", 
                        GetText(currentState.ToString().ToUpper(), true), // {0}
                        ConsoleServiceResources.PRODUCT_NAME, // {1}
                        GetDeviceTypeDisplayText(Configuration.DockingStation.Type), // {2}
                        GetBlankLines(1), // {3}
                        ConsoleServiceResources.VERSION_ABBREV, // {4}
                        Controller.FirmwareVersion, // {5}
                        GetBlankLines(1), // {6}
                        GetSerialNumber() /*{7}*/ );

					//	Turn the LEDs to yellow.
					TurnLEDOn( Controller.LEDState.Yellow );

					break;

                case ConsoleState.ConfigurationError : // IDS encountered error reading config files

                    //	Write appropriate message to LCD.
                    LCD = stateString + GetBlankLines( 1 ) + "<a>" + Controller.GetWiredNetworkAdapter().IpAddress + "</a>";

                    //	Set the LED to red.
                    TurnLEDOn( Controller.LEDState.Red );

                    break;

				case ConsoleState.NotSerialized :

					//	Write appropriate message to LCD.
                    LCD = stateString + actionMsg + GetBlankLines( 1 ) + "<a>" + Controller.GetWiredNetworkAdapter().IpAddress + "</a>";

					//	Set the LED to red.
					TurnLEDOn( Controller.LEDState.Red );

					break;

				case ConsoleState.Ready :

					// Enable the menus in this state.
					MenuEnabled = true;

					// Display the ready screen.
					DisplayReady();

					// Set the time that the date/time was last placed on the LCD.
                    _lastTime = DateTime.UtcNow;

					break;

                case ConsoleState.DataDownloadPause : // SGF  11-Mar-2013  INS-3962

                    // Enable the menus in this state.
                    MenuEnabled = true;

                    if (Master.Instance.SwitchService.DockProcessing == true)
                    {
                        Log.TimingEnd("DOCK TO GREEN (PAUSE)", Master.Instance.SwitchService.DockedTime);
                        Master.Instance.SwitchService.DockProcessing = false;
                    }

                    // Display the ready screen.
                    DisplayReady();

                    // Set the time that the date/time was last placed on the LCD.
                    _lastTime = DateTime.UtcNow;

                    break;

                case ConsoleState.Reset :

                    LCD = stateString + actionMsg + GetBlankLines( 2 ) + GetSerialNumber();

                    //	Set the LED to yellow.
                    TurnLEDOn( new Controller.LEDState[] { Controller.LEDState.Yellow } );

                    break;

				case ConsoleState.Discovering :
                case ConsoleState.Diagnosing :
                case ConsoleState.InteractiveDiagnostics :
                case ConsoleState.UpdatingData :
                case ConsoleState.ReadingData :
                case ConsoleState.CheckingGas :
                case ConsoleState.CalibratingInstrument :
                case ConsoleState.BumpingInstrument :
                case ConsoleState.DownloadingInstrumentDatalog :
                case ConsoleState.ClearingInstrumentDatalog :
                case ConsoleState.ClearingInstrumentAlarmEvents :
                case ConsoleState.DownloadingInstrumentAlarmEvents :
                case ConsoleState.ClearingInstrumentManualGasOperations :
                case ConsoleState.DownloadingInstrumentManualGasOperations :
                case ConsoleState.DiagnosingInstrument :
                case ConsoleState.UpdatingInstrumentData :
                case ConsoleState.ReadingInstrumentData :
                case ConsoleState.UploadingDebugLog :
                case ConsoleState.UploadingDatabase :
                case ConsoleState.PleaseTurnOn :
                case ConsoleState.Synchronization :
                case ConsoleState.PerformingMaintenance :
				case ConsoleState.CylinderPressureReset :

                    LCD = stateString + actionMsg + GetBlankLines( 2 ) + GetSerialNumber();

					//	Set the LED to yellow.
					TurnLEDOn( Controller.LEDState.Yellow );

					break;

                case ConsoleState.UpgradingFirmware:
                case ConsoleState.UpgradingInstrumentFirmware:
				case ConsoleState.Troubleshoot:


                    // We want a single blank line before the 'action' message.
                    LCD = stateString + GetBlankLines( 1 ) + actionMsg + GetBlankLines( 1 ) + GetSerialNumber();

                    //	Set the LED to yellow.
                    TurnLEDOn( Controller.LEDState.Yellow );

                    break;

				case ConsoleState.UnsupportedSoftware :
                case ConsoleState.UnsupportedInstrument :
                case ConsoleState.UnserializedInstrument :
             	case ConsoleState.SensorMissing :
                case ConsoleState.UpgradingInstrumentError :
                case ConsoleState.NoEnabledSensors :
				case ConsoleState.PrinterError :
				case ConsoleState.IGasError :
                case ConsoleState.InstrumentSystemAlarm:    // SGF  19-Oct-2010  DSW-355  (DS2 v7.6)

					//	Write the state to the LCD.
					LCD = stateString + actionMsg + GetBlankLines(2) + GetSerialNumber();

					//	Set the LED to red.
					TurnLEDOn( Controller.LEDState.Red );

					break;

                case ConsoleState.SensorError:
             
                    //	Write the state to the LCD. the action message contains "Position", " X" where 'X' is the sensor position.
                    // So here we need translation only for "Position" and FormatActionMessage() adds , between Postion and X.
                    // Hence formatting the message as below to display Sensor Error(Position X) for INS-8630 RHP v7.5.
                    LCD = stateString + (_actionMessages.Length > 1 ? "<a> (" + GetText(_actionMessages[0]) + _actionMessages[1] + ") </a>" : actionMsg) 
                        + GetBlankLines(2) + GetSerialNumber();
                    
                    //	Set the LED to red.
                    TurnLEDOn(Controller.LEDState.Red);

                    break;

				case ConsoleState.LeakUnavailable :
									
					// Enable the menus in this state so that the user can attempt to 
                    // fix the problem and then force a diagnostics.
					MenuEnabled = true;

                    // Unlocked menus (if they're currently unlocked).
                    // This is to allow the user a chance to fix the leak
                    // and manually re-run diagnostics to see if the leak is gone.
                    // (If the menus remain locked on LeakUnvail, there's no way for 
                    // the user to rerun diagnostics).
                    // Menus will remain unlocked until next reboot or until next
                    // Update Settings.
                    Configuration.DockingStation.MenuLocked = false;

					//	Write the state to the LCD.
					LCD = stateString + GetBlankLines(2) + GetSerialNumber();

					//	Turn the LED to red.
					TurnLEDOn( Controller.LEDState.Red );

                    // We initiate a single beep upon entering this state, 
                    // but the DS will not continually beep.
                    Beep();

					break;

                case ConsoleState.Unavailable:
                    
                    MenuEnabled = true;

                    stateString = stateString.Replace( "________", " " );

                    //	Write the state to the LCD.
                    LCD = stateString + actionMsg + GetBlankLines( 2 ) + GetSerialNumber();

                    //	Turn the LED to red.
                    TurnLEDOn( Controller.LEDState.Red );

                    break;
                                    
                case ConsoleState.ContactISCCode10110:                  // INS-8446 RHP v7.6
                case ConsoleState.ContactISCCode1011:                   // INS-8446 RHP v7.6
                case ConsoleState.ContactISCCode1012:                   // INS-8446 RHP v7.6
                case ConsoleState.ContactISCCode1014:                   // INS-8446 RHP v7.6
                case ConsoleState.ContactISCCode1018:                   // INS-8446 RHP v7.6
                case ConsoleState.ContactISCCode10160:                  // INS-8446 RHP v7.6

                    // If we're in serious SystemError, don't let the user try and do anything 
                    // with the menu.  
                    MenuEnabled = false;

                    stateString = stateString.Replace( "________", " " );

					//	Write the state to the LCD.
					LCD = stateString;

					//	Turn the LED to red.
					TurnLEDOn( Controller.LEDState.Red );

                    // We initiate a single beep upon entering this state, 
                    // but the DS will not continually beep. 
                    Beep();

					break;

				case ConsoleState.UnavailableGas :
                case ConsoleState.ReplaceCylinder:                      // INS-8446 RHP v7.6
                case ConsoleState.ExpiredCylinder:                      // INS-8446 RHP v7.6
                case ConsoleState.LowCylinder:                          // INS-8446 RHP v7.6
                case ConsoleState.ConnectToZeroAirCylinder:             // INS-8446 RHP v7.6

                    LCD = stateString + actionMsg + GetBlankLines(2) + GetSerialNumber(); 
                    TurnLEDOn(Controller.LEDState.Red);
                    break;

                case ConsoleState.UnsupportedCylinder1:
                case ConsoleState.UnsupportedCylinder2:
                case ConsoleState.UnsupportedCylinder3:
                case ConsoleState.ConnectFreshAirToPort1:               // INS-8446 RHP v7.6
                case ConsoleState.ConnectZeroAirToPort1:                     // INS-8446 RHP v7.6
                case ConsoleState.ConnectFreshAirOrZeroAirToPort1:      // INS-8446 RHP v7.6

                    LCD = stateString + actionMsg + GetBlankLines( 2 ) + GetSerialNumber();
					TurnLEDOn( Controller.LEDState.Red );
					break;

				case ConsoleState.SynchronizationError:
					LCD = stateString + actionMsg
						+ GetBlankLines( 1 ) + "<a>"
						+ Controller.GetWiredNetworkAdapter().IpAddress + "</a>"
						+ GetBlankLines( 2 )
						+ GetSerialNumber();
					TurnLEDOn( Controller.LEDState.Red );
					break;

                case ConsoleState.MfgNotConnected:
                    LCD = stateString + actionMsg 
                        + GetBlankLines( 1 ) + "<a>"
                        + Controller.GetWiredNetworkAdapter().IpAddress + "</a>" 
                        + GetBlankLines( 1 ) 
                        + GetSerialNumber();
                    TurnLEDOn( Controller.LEDState.Red );
                    break;


				case ConsoleState.CalibrationFailure :
                case ConsoleState.BumpFailure:
                case ConsoleState.BumpStoppedCheckTubing:
                case ConsoleState.CalibrationStoppedCheckTubing:
                case ConsoleState.ManualCalibrationRequired:  // SGF  24-May-2012  INS-3078
                case ConsoleState.ManualBumpTestRequired:     // SGF  24-May-2012  INS-3078
                case ConsoleState.CheckCylinderConnections:   // INS-8446 RHP v7.6  
					// Enable the menus in this state so that user can force a calibration
                    MenuEnabled = true;

                    LCD = stateString + actionMsg + GetBlankLines( 2 ) + GetSerialNumber();

					//	Turn the LED to red.
					TurnLEDOn( Controller.LEDState.Red );

					break;
    
                // INS-6777 RHP v7.6
                case ConsoleState.BumpFailureCheckGasConnection:
                    // Enable the menus in this state so that user can force a calibration
                    MenuEnabled = true;
                    LCD = DS.LCD.WrapString(string.Format(ConsoleServiceResources.BUMPFAILURECHECKGASCONNECTION, _actionMessages)) + GetBlankLines(1) + GetSerialNumber();

                    //	Turn the LED to red.
                    TurnLEDOn(Controller.LEDState.Red);

                    break;

				case ConsoleState.UndockedInstrument :
                case ConsoleState.LidError:
                case ConsoleState.FlipperAndLidError:
                    //	Write the state to the LCD.
                    LCD = stateString + GetBlankLines( 2 ) + GetSerialNumber();

                    //	Turn the LED to red.
                    TurnLEDOn( Controller.LEDState.Red );

                    // We initiate a single beep upon entering this state, 
                    // but the DS will not continually beep. 
                    Beep();
                    
                    break;

                case ConsoleState.UnregisteredInstrument:                
				case ConsoleState.ReturnDisabledInstrument:
				case ConsoleState.ReturnDockingStation:
                case ConsoleState.InstrumentNotReady:       // INS-7657 RHP v7.5.2 
					//	Write the state to the LCD.
					LCD = stateString + GetBlankLines(2) + GetSerialNumber();

					//	Turn the LED to red.
					TurnLEDOn( Controller.LEDState.Red );

                    break;

				case ConsoleState.Menu :

					// Display the current menu.
					_currentMenu.Display();

					break;
                                   
				default :

					break;

			} // end-switch currentState

            // Whenever the state changes, we need to notify inet.
            if ( stateChanged )
                UpdateInet( currentState );
		}

        /// <summary>
        /// Performs a "beep", to alert users that the DS is in an error state.
        /// </summary>
        private void Beep()
        {
            Controller.Buzz( 0.1 ); // Buzz the error.
            Thread.Sleep( 1000 ); // Sleep for a small period.
        }

        private string FormatActionMessage()
        {
            string actionMsg = string.Empty;

            foreach ( string msg in _actionMessages )
                actionMsg += GetText( msg ) + ", ";

            if ( actionMsg == string.Empty )
                return string.Empty;

            actionMsg = actionMsg.TrimEnd( _trimBlanks );
            if ( actionMsg != string.Empty )
                actionMsg = "(" + actionMsg + ")";
                //extraMsg = "<a>(" + extraMsg + ")</a>";

            actionMsg = DS.LCD.WrapString( actionMsg );

            return actionMsg;
        }

        protected override bool OnRun()
        {
            // The console service needs to ALWAYS be allowed to  
            // run so that it can always display state changes.
            return true; 
        }

        /// <summary>
        /// This method handles the thread start for this service.
        /// </summary>
        protected override void Run()
        {
            // If menu is not active, then wait for a keypress, by blocking and waiting for keypad event to 
            // be forced.  Once the event is fired, and we're unblocked, then we make a call to Controller.GetKeyPress
            // (see further below) to find out which button was pressed.
            // If menu is already active, though, we don't bother blocking/waiting; we just immediately just poll for a keypress.
            if ( !IsMenuActive )
            {
                //_keypadevent.Reset();
                //_keypadevent.WaitOne( _keypadEventWaitTime, false );
            }

            //	Check the current language, change if necessary.
            try
            {
                if ( Configuration.DockingStation.Language.Code != _currentLanguage )
                {
                    //	Switch the language to the new setting.
                    Log.Trace( "Switching language: " +
                        Configuration.DockingStation.Language.Code );
                    SwitchLanguage( Configuration.DockingStation.Language.Code );
                }
            }
            catch ( Exception error )
            {
                //	Report the error.
                Log.Error( Name + " - checking language." , error );

                //	Default to english if an error occurs.
                Log.Trace( "Defaulting language to english." );
                SwitchLanguage( Language.English );
            }

            // Get the next key input.. When running service diagnostics, we assume service diagnostics
            // has taken over they keypad.
            // Note that we deliberately do NOT call the CurrentState property and instead just directly
            // access the member variable. This is because we want to avoid doing the 'lock' that invoking
            // the property causes which is a performance hit if IdleTime is zero.
            // Since we're just checking for InteractiveDiagnostics, it's OK that we go behind
            // the property's back.
            KeyPress keyPress = ( _currentState != ConsoleState.InteractiveDiagnostics ) ? Controller.GetKeyPress() : KeyPress.None;

            // We must also prevent console keypad menu access in the event the docking station is defined 
            // to be associated with the ISC manufacturing account, and it has detected that it is not 
            // connected to iNet Core Server.
            if (_currentState == ConsoleState.MfgNotConnected && keyPress.Key != Controller.Key.None)
            {
                Log.Warning(string.Format("MFG docking station not connected to iNet; ignoring console keypad press ({0})", keyPress.Key.ToString()));
                keyPress = KeyPress.None;
            }

            // Watch out for clock to now to suddenly become earlier than lastTime
            // for some unknown reason. If so, then just set lastTime to equal now. 
            // Otherwise due to logic farther below, LCD clock won't update.
            DateTime now = DateTime.UtcNow;
            if ( now < _lastTime )
                _lastTime = _lastKeyPressTime = now;

            ConsoleState currentState = CurrentState;  // current state may have changed while polling for key press

            #region switch(keyPress.key)


            switch ( keyPress.Key )
            {
                case Controller.Key.None :
                    
                    if ( IsNewState || IsNewActions )
                    {
                        Log.Trace( "No key pressed." );
                        Log.Trace( "State changed:" + " current: " + currentState + " last: " + _lastState );

                        HandleStateChange();
                    }

                    else if ( currentState == ConsoleState.Ready )
                    {
                        //If we haven't updated the time within the last 10 secs, update it again.
                        if ( ( now - _lastTime ) > _tenSeconds )
                        {
                            DisplayReady(); // Display the ready screen.
                            _lastTime = now; // Set the time that we last updated the LCD.
                        }
                    }
                    //	If the menu active and its been longer than 10 secs, abort the menu mode.
                    else if ( IsMenuActive )
                    {
                        if ((_lastKeyPressTime != now) && ((now - _lastKeyPressTime) > _tenSeconds))
                        {
                            _lastKeyPressTime = DateTime.MinValue;  // Set last key press time to the default.
                            ConstructMenus();  // Reset the menus.
                            UpdateState( _preMenuState, _preMenuMessages );  // Bring us out of the menu mode.
                        }
                    }

                    break;

                case Controller.Key.RightLeft :

                    Log.Debug( string.Format( "Left & Right keys pressed for {0} seconds", (int)keyPress.Length.TotalSeconds ) );

                    // If in menu mode, handle menu commands.
                    if ( !IsMenuActive && ( keyPress.Length.TotalSeconds >= 10.0 ) )
                    {
                        // Save the state in case the menu times out.
                        _preMenuState = currentState;
                        _preMenuMessages = _actionMessages;

                        // Place us into the menu state with a single key press.
                        UpdateState( ConsoleState.Menu );

                        // Indicate the last time a key was pressed.
                        _lastKeyPressTime = now;

                        // Make the menus.
                        ConstructResetMenu();
                    }

                    break;

                case Controller.Key.Left :

                    Log.Debug( "Left key pressed." );

                    // Check if we are eligible to enter the menu mode.
                    if ( MenuEnabled || IsMenuActive )
                    {
                        // If in menu mode, handle menu commands.
                        if ( IsMenuActive )
                        {
                            // Indicate the last time a key was pressed.
                            _lastKeyPressTime = now;

                            // Move the current selection up.
                            _currentMenu.MoveSelectionUp();

                            // Display the menu in the new state.
                            _currentMenu.Display();
                        }
                        else
                        {
                            // Save the state in case the menu times out.
                            _preMenuState = currentState;
                            _preMenuMessages = _actionMessages;

                            // Place us into the menu state with a single key press.
                            UpdateState( ConsoleState.Menu );

                            // Indicate the last time a key was pressed.
                            _lastKeyPressTime = now;

                            // Make the menus.
                            ConstructMenus();
                        }
                    }

                    break;

                case Controller.Key.Middle :

                    Log.Debug( "Middle key pressed." );

                    // Check if we are eligible to enter the menu mode.
                    if ( MenuEnabled || IsMenuActive )
                    {
                        // If the menu is already active.
                        if ( IsMenuActive )
                        {
                            // Indicate the last time we pressed a menu key.
                            _lastKeyPressTime = now;

                            // Activate the current menu item's function.
                            Menu activateMenu = _currentMenu.Activate();

                            // Jump to the selected menu.
                            if ( activateMenu != null )
                            {
                                _currentMenu = activateMenu;

                                // Display the new menu state.
                                _currentMenu.Display();
                            }
                            else
                            {
                                // If the selected menu item has no destination, then
                                // immediately set the menu as being 'timed out' so that
                                // it goes away.
                                _lastKeyPressTime = DateTime.MinValue;
                            }
                        }
                        else
                        {
                            Log.Trace( "Constructing menus." );
                            ConstructMenus();

                            // Save the state in case the menu times out.
                            _preMenuState = currentState;
                            _preMenuMessages = _actionMessages;

                            // Enter the menu state.
                            UpdateState( ConsoleState.Menu );

                            // Indicate the last time a key was pressed.
                            _lastKeyPressTime = now;

                            // Make the menus.
                            ConstructMenus();
                        }
                    }

                    break;

                case Controller.Key.Right :

                    Log.Debug( "Right key pressed." );

                    // Determine menu eligibility.
                    if ( MenuEnabled || IsMenuActive )
                    {
                        // If the menu is already activated.
                        if ( IsMenuActive )
                        {
                            // Update the time the last menu key was pressed.
                            _lastKeyPressTime = now;

                            // Move the current selection down.
                            _currentMenu.MoveSelectionDown();

                            // Display the updated menu.
                            _currentMenu.Display();
                        }
                        else
                        {
                            // Save the state in case the menu times out.
                            _preMenuState = currentState;
                            _preMenuMessages = _actionMessages;

                            // Enter the menu state.
                            UpdateState( ConsoleState.Menu );

                            // Indicate the last time a key was pressed.
                            _lastKeyPressTime = now;

                            // Make the menus.
                            ConstructMenus();
                        }
                    }

                    break;
            }
            #endregion switch(key)

            // If IDS is mostly idle (default), sleep a tiny bit between
            // checks of the keypad so that we don't hammer the CPU.

            TimeSpan sleepTime;

            // But if we know the menu is active, don't sleep at all.  We
            // want the keypad to be super responsive when menu is displayed.
            if ( IsNewState || IsMenuActive )
            {
                sleepTime = IDLE_TIME_MENU;  // don't sleep

                //if ( IsNewState )
                //    Log.Debug( "**************  IDLE_TIME_MENU   **************" );
            }
            // If IDS is currently busy doing something, then sleep 
            // much longer. Otherwise thread will hog the CPU.
            else if ( IsBusyState )
            {
                sleepTime = IDLE_TIME_BUSY;
                //Log.Debug( "**************  IDLE_TIME_BUSY  **************" );
            }
            else
            {
                sleepTime = IDLE_TIME_IDLE;
                //Log.Debug( "**************  IDLE_TIME_IDLE  **************" );
            }

            if ( _keypadEventWaitTime.Ticks != sleepTime.Ticks )
                _keypadEventWaitTime = sleepTime;

            return;
        }

		/// <summary>
		/// Gets the current language's message for the id and format it for the lcd.
		/// </summary>
		/// <param name="msgID">The id to retrieve.</param>
		/// <returns>The current language's message.</returns>
        private string GetMessage( string msgID )
        {
            string msg = GetText( msgID, true ); // Get the basic message.

            // If its small, return it.
            string returnMsg;
            if ( msg.IndexOf( ' ' ) < 0  )
                returnMsg = "<a>" + msg + "</a>";
            else
                returnMsg = DS.LCD.WrapString( msg );

            return returnMsg;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msgID"></param>
        /// <returns></returns>
        private string GetText( string msgId )
        {
            return GetText( msgId, false );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msgId"></param>
        /// <param name="verbose">If true, then a debug message is logged if translation cannot be found.</param>
        /// <returns></returns>
        private string GetText( string msgId, bool verbose )
        {
            string msgIdUpper = msgId.ToUpper();
            string msg;

            lock ( ConsoleServiceResources.ResourceManager ) // we do this in case SwitchLanguage is called by another thread.
            {
                // msg = ConsoleServiceResources.ResourceManager.GetString( msgId );

                // hack to handle things like iNet part number of "FRESH AIR".  Need to convert it
                // to FRESH_AIR since resource files don't support having spaces in the name.
                msgIdUpper = msgIdUpper.Replace( ' ', '_' );

                msg = ConsoleServiceResources.ResourceManager.GetString( msgIdUpper, ConsoleServiceResources.Culture );
            }

            if ( msg == null  ) // ResourceManager returns null if a translation not found.
            {
                if ( verbose == true )
                    Log.Debug( Name + " Language Error: No translation found for \"" + msgIdUpper + "\"" );

                return msgId;
            }

            return msg;
        }

        /// <summary>
        /// Get Device Type Display Text
        /// </summary>
        /// <param name="deviceType">Device type</param>
        /// <returns>Device type display text</returns>
        private string GetDeviceTypeDisplayText(DeviceType deviceType)
        {
            if (deviceType == DeviceType.MX4)
            {
                return "Ventis";
            }

            return deviceType.ToString();
        }


        public void UpdateAction( string operationState )
        {
            _actionMessages = new string[] { operationState };
        }

        public void UpdateAction( string[] operationState )
        {
            _actionMessages = operationState;
        }

		/// <summary>
		/// Updates a given state.
		/// </summary>
		/// <param name="state">State that is to be updated</param>
		public void UpdateState( ConsoleState state )
		{
            UpdateState( state, new string[0] );
		}

        /// <summary>
        /// Updates a given state.
        /// </summary>
        /// <param name="state">State that is to be updated</param>
        /// <param name="actionMessage">Message to be displayed after the main message.</param>
        public void UpdateState(ConsoleState state, string actionMessage)
		{
            string[] actionMessages = new string[1];
            actionMessages[0] = actionMessage;
            UpdateState( state, actionMessages );
		}

		/// <summary>
		/// Updates a given state.
		/// </summary>
		/// <param name="state">State that is to be updated</param>
        /// <param name="actionMessages">Optional message(s) to be displayed after
        /// the main message.</param>
		public void UpdateState( ConsoleState state, string[] actionMessages )
		{
            lock ( _stateLock )
            {
                ConsoleState currentState = CurrentState;

                // INS-8446 RHP v7.6 Added CheckCylinderConnections
                if (currentState == ConsoleState.CheckCylinderConnections           
                   && Controller.IsDocked() == true
                   && state == ConsoleState.Ready)
                {
                    Log.Debug("Calibration Failure Check Cylinder Connections: Ignoring state change.");
                }
                else if ( currentState == ConsoleState.CalibrationFailure 
                &&   Controller.IsDocked() == true
                &&   state == ConsoleState.Ready )
                {
                    Log.Debug( "Calibration Failure: Ignoring state change." );
                }
                else if ( currentState == ConsoleState.ManualCalibrationRequired
                &&        Controller.IsDocked() && state == ConsoleState.Ready ) 
                {
                    Log.Debug("Manual Calibration Required: Ignoring state change.");
                }
                else if ( currentState == ConsoleState.ManualBumpTestRequired
                &&        Controller.IsDocked() && state == ConsoleState.Ready ) 
                {
                    Log.Debug("Manual Bump Test Required: Ignoring state change.");
                }
                else if ( currentState == ConsoleState.Unavailable
		        &&        Controller.IsDocked() && state == ConsoleState.Ready )
                {
                    Log.Debug( "Unavailable Instrument: Ignoring state change." );
                }
                else if ( (currentState == ConsoleState.InstrumentSystemAlarm) && Controller.IsDocked() && state == ConsoleState.Ready )
                {
                    Log.Debug("Instrument System Alarm OR Instrument Pump Fault: Ignoring state change.");
                }
                // Removing the test for 'state == ConsoleState.Ready' from this if-clause; 
                // must allow the state to transition to Ready and back to LidError if we 
                // eventually want to escape the LidError state.
                else if ( currentState == ConsoleState.LidError && Controller.IsDocked() == true && state == ConsoleState.Discovering )
                {
                    Log.Debug( "Lid Not Closed: Ignoring state change." );
                }
                else if (currentState == ConsoleState.FlipperAndLidError && Controller.IsDocked() == true && state == ConsoleState.Discovering)
                {
                    Log.Debug("Docking Station Hardware Not Configured Properly; Ignoring state change.");
                }
                else if (currentState == ConsoleState.MfgNotConnected && state == ConsoleState.Ready)
                {
                    Log.Debug("MFG Not Connected to iNet: Ignoring state change.");
                }
                else if ((currentState == ConsoleState.InstrumentNotReady) && Controller.IsDocked() && state == ConsoleState.Ready)
                {
                    Log.Debug("Instrument Not Ready for Gas Operation: Ignoring state change.");
                }
                else
                {
                    if ( currentState != state )
                        _actionMessages = new string[0];

                    CurrentState = state;
                }
                if ( actionMessages != null )
                    _actionMessages = ( string[] )actionMessages.Clone();
            }
		}

        /// <summary>
        /// Notifies iNet of the passed-in state.
        /// </summary>
        /// <param name="state"></param>
        private void UpdateInet( ConsoleState state )
        {
            if ( state == ConsoleState.Menu || state == ConsoleState.Synchronization || state == ConsoleState.Starting )
                return;

            // Only update iNet with our status if actived, or if in both cal station mode and service mode.
            if ( !Configuration.Schema.Activated && ( string.IsNullOrEmpty(Configuration.Schema.AccountNum) || !Configuration.ServiceMode ) )
                return;

            if ( Configuration.DockingStation.SerialNumber == string.Empty ) // not serialized for some reason?
                return;

            // No need to try and update iNet with our status if we know we're not connected.
            if ( !Inet.IsDownloadOnline )
                return;

            // We notify iNet of the new state in background thread.  That way,
            // if the web service call blocks, the console service thread doesn't get blocked.
            
            // First, see if the background thread is still running. If so, that means
            // the thread is still trying to notify inet of a previous state change.
            // In that situation, we just don't bother to notify iNet of this latest
            // state change.
            if ( _exchangeStatusThread != null ) // already a background thread running?
            {
                if ( !_exchangeStatusThread.Join( 0 ) ) // see if thread is finished or not. Join will fail if it's still running.
                {
                    Log.Debug( string.Format( "{0} thread is already running. Can't notify iNet of \"{1}\" state.", _exchangeStatusThread.Name, state.ToString() )  );
                    return;
                }
                Log.Trace( _exchangeStatusThread.Name + " thread is finished." );
            }

            // Create/start a new thread.
            Log.Trace( string.Format( "Starting {0}.ExchangeStatus thread", Name ) );
            _exchangeStatusThread = new Thread( new ThreadStart( () => ExchangeStatusInfoInBackground( state ) ) );
            _exchangeStatusThread.Name = Thread.CurrentThread.Name + ".ExchangeStatus";
            _exchangeStatusThread.Start();
        }

        private void ExchangeStatusInfoInBackground( ConsoleState state )
        {
			Log.Debug( string.Format( "{0} thread (ThreadId={1}) notifying iNet of \"{2}\" state.",
				Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId.ToString("x8"), state.ToString() ) );
            try
            {
                new ExchangeStatusOperation( state.ToString(), false ).Execute();
            }
            catch ( Exception ex )
            {
                Log.Error( Thread.CurrentThread.Name, ex );
            }
        }

        private bool ActionMessagesChanged( string[] oldActions, string[] newActions )
        {
            if ( oldActions == null && newActions == null ) return false;

            if ( oldActions == null && newActions != null ) return true;

            if ( oldActions != null && newActions == null ) return true;

            if ( oldActions.Length != newActions.Length ) return false;

            for ( int i = 0; i < oldActions.Length; i++ )
            {
                if ( oldActions[ i ] != newActions[ i ] ) return true;
            }

            return false;
        }

		/// <summary>
		/// Updates display console state to a state which corresponds to the given
		/// docking station action.
		/// </summary>
		/// <param name="dsAction">Docking station action</param>
		public void UpdateState(DockingStationAction dsAction)
		{
            // Message property contains extra message that server wants
            // IDS to display on LCD while running the action
            if (dsAction == null)
                return;

            // Have the LCD display action's messages.
            string[] actionMessages = dsAction.Messages.ToArray();

            lock (_stateLock)
            {
                if (dsAction is NothingAction)
                {
                    // Make sure docking station say's it's 'discovering' until it
                    // performs it's initial settings read.

                    // If we've not yet performed the initial settings read that's performed during bootup,
                    // and we're activated, OR if we're in service mode and not synchronized,
                    // then stay in the "Discovering" state.
                    // NOTE THAT THIS CODE IS A BIT DELICATE AND IS TIGHTLY RELATED TO THE LOGIC FOR "Synchronized"
                    // IN THE SWITCHSERVICE.  CHANGING THAT LOGIC CAN AFFECT THIS LOGIC AND VICE-VERSA.
                    if ( Master.SwitchService.InitialReadSettingsNeeded && Configuration.Schema.Activated )
                        UpdateState(ConsoleState.Discovering);
                    else
                        UpdateState(ConsoleState.Ready, actionMessages);
                }
                else if (dsAction is DiagnosticAction)
                {
                    UpdateState( ConsoleState.Diagnosing, actionMessages);
                }
                else if (dsAction is InteractiveDiagnosticAction)
                {
                    UpdateState(ConsoleState.InteractiveDiagnostics, actionMessages);
                }
                else if (dsAction is SettingsUpdateAction)
                {
                    UpdateState(ConsoleState.UpdatingData, actionMessages);
                }
                else if (dsAction is SerializationAction)
                {
                    UpdateState(ConsoleState.UpdatingData, actionMessages);
                }
                else if (dsAction is MaintenanceAction)
                {
                    UpdateState(ConsoleState.PerformingMaintenance, actionMessages);
                }
                else if (dsAction is FactoryResetAction || dsAction is RebootAction)
                {
                    UpdateState(ConsoleState.Reset, actionMessages);
                }
                else if (dsAction is FirmwareUpgradeAction)
                {
                    UpdateState(ConsoleState.UpgradingFirmware, actionMessages);
                }
                else if (dsAction is InstrumentFirmwareUpgradeAction)
                {
                    UpdateState(ConsoleState.UpgradingInstrumentFirmware, actionMessages);
                }
                else if (dsAction is PopQueueAction)
                {
                    UpdateState(ConsoleState.UpdatingData, actionMessages);
                }
                else if (dsAction is SettingsReadAction)
                {
                    UpdateState(ConsoleState.ReadingData, actionMessages);
                }
                else if (dsAction is LeakUnavailableAction)
                {
                    UpdateState(ConsoleState.LeakUnavailable, actionMessages);
                }
                else if (dsAction is UnavailableAction)
                {
                    UpdateState(ConsoleState.Unavailable, actionMessages);
                }
                else if (dsAction is InstrumentUnregisteredAction)
                {
                    UpdateState(ConsoleState.UnregisteredInstrument, actionMessages);
                }
                else if (dsAction is ResourceUnavailableAction)
                {
                    // SGF  20-Feb-2013  INS-3821
                    List<string> consoleMessages = ((ResourceUnavailableAction)dsAction).ConsoleMessages;
                    // BEGIN INS-8630 RHP v7.5
                    // Fetch the cylinder partnumber if available in case of expired or empty 
                    // Do not remove Expired or Empty text from the _actionMessages, 
                    // since these are being used to display the respective new messages as below.
                    string cylPartNumber = string.Empty;
                    cylPartNumber = consoleMessages.Find(x => !x.Equals("Expired") && !x.Equals(PressureLevel.Empty.ToString()) && !x.Equals(PressureLevel.Low.ToString()));

                    if (consoleMessages.Contains("Expired"))
                    {
                        // For UNavailable gas (Zero Air) , we need to display as "Expired Cylinder ( part number of the cylinder )"
                        UpdateState(ConsoleState.ExpiredCylinder, cylPartNumber);
                    }
                    else if (consoleMessages.Contains(PressureLevel.Empty.ToString()))
                    {
                        // For Empty Cylinder , we need to display as "Replace Cylinder ( part number of the cylinder )"
                        UpdateState(ConsoleState.ReplaceCylinder, cylPartNumber);
                    }
                    else if (consoleMessages.Contains(PressureLevel.Low.ToString()))
                    {
                        // For Low Cylinder , we need to display as "Cylinder Low( part number of the cylinder )"
                        UpdateState(ConsoleState.LowCylinder, cylPartNumber);
                    }
                    else if (consoleMessages.Contains("ZEROAIR"))
                    {
                        // For UNavailable gas (Zero Air) , we need to display as "Connect to Zero Air Cylinder"
                        // This text doesnot fit on LCD, so used Wrap string and set blank lines to 1(usually its 2).
                        UpdateState(ConsoleState.ConnectToZeroAirCylinder);
                    }   // END INS-8630
                    else
                        UpdateState(ConsoleState.UnavailableGas, consoleMessages.ToArray());
                }
				else if (dsAction is TroubleshootAction)
				{
					UpdateState( ConsoleState.Troubleshoot, actionMessages );
				}
				else if ( dsAction is CylinderPressureResetAction )
				{
					UpdateState( ConsoleState.CylinderPressureReset, actionMessages );
				}
				else if ( dsAction is UploadDebugLogAction )
				{
					UpdateState( ConsoleState.UploadingDebugLog, actionMessages );
				}
				else if ( dsAction is UploadDatabaseAction )
				{
					UpdateState( ConsoleState.UploadingDatabase, actionMessages );
				}
				else if ( dsAction is UnsupportedCylinderAction )
				{
					// The UnsupportedCylinderAction is guaranteed (or at least that's
					// the intent) to give us an GasEndPoint specifying which
					// gas port is the problem.
					UnsupportedCylinderAction ucAction = dsAction as UnsupportedCylinderAction;
					if ( ucAction.GasEndPoint.Position == 1 )
                    {
                        // INS-8446 RHP v7.6 - Display LCD messages based on port one restrictions
                        if (Configuration.DockingStation.Port1Restrictions == (PortRestrictions.FreshAir | PortRestrictions.ZeroAir))
                            UpdateState(ConsoleState.ConnectFreshAirOrZeroAirToPort1, new string[] { });
                        else if (Configuration.DockingStation.Port1Restrictions == PortRestrictions.FreshAir)
                            UpdateState(ConsoleState.ConnectFreshAirToPort1, new string[] { });
                        else if (Configuration.DockingStation.Port1Restrictions == PortRestrictions.ZeroAir)
                            UpdateState(ConsoleState.ConnectZeroAirToPort1, new string[] { });
                        else
                            UpdateState(ConsoleState.UnsupportedCylinder1, actionMessages);
                    }
					else if ( ucAction.GasEndPoint.Position == 2 )
						UpdateState( ConsoleState.UnsupportedCylinder2, actionMessages );
					else if ( ucAction.GasEndPoint.Position == 3 )
						UpdateState( ConsoleState.UnsupportedCylinder3, actionMessages );
				}
				else if ( dsAction is InstrumentBumpTestAction )
				{
					UpdateState( ConsoleState.BumpingInstrument, actionMessages );
				}
				else if ( dsAction is InstrumentDiagnosticAction )
				{
					UpdateState( ConsoleState.DiagnosingInstrument, actionMessages );
				}
				else if ( dsAction is InstrumentCalibrationAction )
				{
					UpdateState( ConsoleState.CalibratingInstrument, actionMessages );
				}
				else if ( dsAction is InstrumentSettingsUpdateAction )
				{
					UpdateState( ConsoleState.UpdatingInstrumentData, actionMessages );
				}
				else if ( dsAction is InstrumentSettingsReadAction )
				{
					UpdateState( ConsoleState.ReadingInstrumentData, actionMessages );
				}
				else if ( dsAction is DataDownloadPauseAction )
				{
					UpdateState( ConsoleState.DataDownloadPause );
				}
				else if ( dsAction is InstrumentDatalogDownloadAction )
				{
					UpdateState( ConsoleState.DownloadingInstrumentDatalog, actionMessages );
				}
				else if ( dsAction is InstrumentDatalogClearAction )
				{
					UpdateState( ConsoleState.ClearingInstrumentDatalog, actionMessages );
				}
				else if ( dsAction is InstrumentAlarmEventsClearAction )
				{
					UpdateState( ConsoleState.ClearingInstrumentAlarmEvents, actionMessages );
				}
				else if ( dsAction is InstrumentAlarmEventsDownloadAction )
				{
					UpdateState( ConsoleState.DownloadingInstrumentAlarmEvents, actionMessages );
				}
				else if ( dsAction is InstrumentManualOperationsClearAction )
				{
					UpdateState( ConsoleState.ClearingInstrumentManualGasOperations, actionMessages );
				}
				else if ( dsAction is InstrumentManualOperationsDownloadAction )
				{
					UpdateState( ConsoleState.DownloadingInstrumentManualGasOperations, actionMessages );
				}
				else if ( dsAction is CalibrationFailureAction )
				{
                    // INS-8446 RHP v7.6 - Incase of a sensor with zero Span reserve display meesage accordingly to check cylinders
                    List<string> consoleMessages = new List<string>(actionMessages);
                    if (consoleMessages.Exists(x => x.Equals(ConsoleServiceResources.CHECKCYLINDERCONNECTIONS)))
                    {
                        consoleMessages.Remove(ConsoleServiceResources.CHECKCYLINDERCONNECTIONS);
                        UpdateState(ConsoleState.CheckCylinderConnections, consoleMessages.ToArray());
                    }
                    else
                        UpdateState(ConsoleState.CalibrationFailure, actionMessages);
				}
				else if ( dsAction is BumpFailureAction )
				{
                    List<string> consoleMessages = (new List<string>(actionMessages));
                    if (consoleMessages.Exists(x => x.Equals(ConsoleServiceResources.BUMPFAILURECHECKGASCONNECTION)))
                    {
                        consoleMessages.Remove(ConsoleServiceResources.BUMPFAILURECHECKGASCONNECTION);
                        UpdateState(ConsoleState.BumpFailureCheckGasConnection, string.Join( ",", consoleMessages.ToArray()));
                    }
                    else
                        UpdateState(ConsoleState.BumpFailure, actionMessages);
				}
                else if (dsAction is BadPumpTubingDetectedAction)
                {
                    List<string> consoleMessages = new List<string>(actionMessages);
                    if(consoleMessages.Exists(x => x.Equals(ConsoleServiceResources.CALIBRATIONSTOPPEDCHECKTUBING)))
                        UpdateState(ConsoleState.CalibrationStoppedCheckTubing);
                    else if (consoleMessages.Exists(x => x.Equals(ConsoleServiceResources.BUMPSTOPPEDCHECKTUBING)))
                        UpdateState(ConsoleState.BumpStoppedCheckTubing);
                }
				else if ( dsAction is ManualCalibrationRequiredAction )  // SGF  24-May-2012  INS-3078
				{
					UpdateState( ConsoleState.ManualCalibrationRequired, actionMessages );
				}
				else if ( dsAction is ManualBumpTestRequiredAction )  // SGF  24-May-2012  INS-3078
				{
					UpdateState( ConsoleState.ManualBumpTestRequired, actionMessages );
				}
				else if ( dsAction is FirmwareUpgradeAction )
				{
					UpdateState( ConsoleState.UpdatingData, actionMessages );
				}
				else if ( dsAction is InstrumentDisableReplacedAction )
				{
					// instrument is not disabled until the action/operation runs, so 
					// setting state to ReturnInstrument instead of ReturnDisabledInstrument
					UpdateState( ConsoleState.ReturnInstrument, actionMessages );
				}
            }
        }

        /// <summary>
        /// Gets the sensor label for the given sensor code
        /// Suresh 18-OCT-2011 INS-2293
        /// </summary>
        /// <param name="sensorCode">The Sensor Code</param>
        /// <returns>Sensor Label</returns>
        public string GetSensorLabel(string sensorCode)
        {
            string sensorLabel =  ConsoleServiceResources.ResourceManager.GetString("SENSORLABEL_" + sensorCode, ConsoleServiceResources.Culture);

            if (string.IsNullOrEmpty(sensorLabel))
                return "";

            return sensorLabel;
        }
        #endregion Methods


    }

    public interface IConsoleService : IService
    {   
        bool IsMenuActive { get; }

        bool MenuEnabled { get; set; }

        ConsoleState CurrentState { get; set; }

        void UpdateState(ConsoleState state);

        string GetSensorLabel(string sensorCode);

        void UpdateState(DockingStationAction dsAction);

        void UpdateState(ConsoleState state, string[] actionMessages);

        void UpdateState(ConsoleState state, string actionMessage);

        void UpdateAction(string operationState);

        void UpdateAction(string[] operationState);

        void InitializeState();
    }
}
