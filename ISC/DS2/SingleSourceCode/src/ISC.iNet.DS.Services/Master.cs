using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using ISC.WinCE;
using ISC.WinCE.Logger;
using ISC.iNet.DS.iNet;


namespace ISC.iNet.DS.Services
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality for managing and monitoring status of IDS services.
    /// </summary>
    public sealed class Master : IMaster
    {
        static private Master _instance;  // Reference to the singleton.

        private IConsoleService    _consoleService;
        private ResourceService   _resourceService;
        private ReporterService   _reporterService;
        private IExecuterService   _executerService;
        private ISwitchService     _switchService;
        private ChargingService   _chargingService;
        private WebAppService     _webAppService;

        private Scheduler _scheduler = new Scheduler();

        private NetworkAdapterInfo _networkAdapterInfo;

#region Properties

       internal Scheduler Scheduler { get { return _scheduler; } set { _scheduler = value; } }

        public ReporterService ReporterService { get { return _reporterService; } }

        public IConsoleService ConsoleService { get { return _consoleService; } internal set { _consoleService = value; } }

        public ResourceService ResourceService { get { return _resourceService; } }

        public IExecuterService ExecuterService { get { return _executerService; } internal set { _executerService = value; } }

        public ISwitchService SwitchService { get { return _switchService; } internal set { _switchService = value; } }

        public ChargingService ChargingService { get { return _chargingService; } }

        public WebAppService WebAppService { get { return _webAppService; } }

        ControllerWrapper _controllerWrapper;

        PumpManager _pumpWrapper;

        LCDWrapper _lcdWrapper;

        SmartCardWrapper _smartCardWrapper;

        public ControllerWrapper ControllerWrapper { get { return _controllerWrapper; } internal set { _controllerWrapper = value; } }

        public PumpManager PumpWrapper { get { return _pumpWrapper; } internal set { _pumpWrapper = value; } }

        public LCDWrapper LCDWrapper { get { return _lcdWrapper; } internal set { _lcdWrapper = value; } }

        public SmartCardWrapper SmartCardWrapper { get { return _smartCardWrapper; } internal set { _smartCardWrapper = value; } }
        
        #endregion Properties

        /// <summary>
        /// This method initializes, starts and wires up all necessary services and then monitors their status.
        /// </summary>
        static void Main( string[] args )
        {
            _instance = new Master();

            try 
            {
                _instance.Run( args );
            }
            catch ( Exception e )
            {
                Log.Error( "Master.Main", e );
            }
        }

        /// <summary>
        ///  Constructor
        /// </summary>
        private Master()
        {
        }

        internal static Master CreateMaster()
        {
            _instance = new Master();

            try
            {
                _instance.Run(new string[] { });
            }
            catch (Exception e)
            {
                Log.Error("Master.Main", e);
            }

            return _instance;
        }
        
        /// <summary>
        /// Returns the one and only master of the services (a singleton).
        /// Individual services can only be accessed through this master.
        /// </summary>
        public static Master Instance { get { return _instance; } }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        private void Run( string[] args )
        {
            Thread.CurrentThread.Name = "Master";

            string applicationPath = Assembly.GetExecutingAssembly().GetName().CodeBase;

            Log.Info( Log.Dashes );
            Log.Info( "STARTING " + applicationPath );
            foreach ( string arg in args ) Log.Info( string.Format( "arg: \"{0}\"", arg ) );
			Log.Debug( Thread.CurrentThread.Name + " ThreadId=" + Thread.CurrentThread.ManagedThreadId.ToString("x8") );
            Log.Info( Log.Dashes );

            // We want the Controller class to load now, so that it's static
            // constructor reads the config file.  It's important to do this
            // before starting ANY of the service threads. Calling any 
            // random property of the Controller class will cause the class to load.

            Controller.ApplicationPath = applicationPath;

            // Stop the pump and speaker on startup.  This is primarily done because if
            // the debugger is stopped then restarted while the pump or speaker are on, then they're left on.
            // if we don't do this...
            Controller.TurnBuzzerOff();
            Pump.Stop();

            // Instantiate the console service immediately so that we can display
            // the "Starting" message ASAP. Note that we don't yet start the thread,
            // though, since we don't need (yet), to monitor for keypad input.
            LCD.Clear(); // This is the first time we're accessing the LCD class, so it's static constructor will be called here.
            LCD.SetLanguage( Configuration.DockingStation.Language );

            Controller.LastRebootTime = DateTime.UtcNow;

            Controller.FirmwareVersion = string.Format( "{0}.{1}.{2}.{3}",
                Assembly.GetExecutingAssembly().GetName().Version.Major,
                Assembly.GetExecutingAssembly().GetName().Version.Minor,
                Assembly.GetExecutingAssembly().GetName().Version.Build,
                Assembly.GetExecutingAssembly().GetName().Version.Revision );

            _consoleService = new ConsoleService( this );
            Log.Debug( ConsoleService.Name + " has been created." );

            ConsoleService.Start();
            // Calling InitializeState now will force the display to refresh and say "Starting" 
            // right away. If we don't do this, it remains blank for a second or two.
            ConsoleService.InitializeState();

            try
            {
                Configuration.ServiceMode = Controller.DetermineServiceMode();
            }
            catch ( Exception e )
            {
                Log.Error( e );
            }
			            
            // Did something go wrong trying to read the configuration data from the flash memory?
            if ( Configuration.HasConfigurationError && Configuration.DockingStation.IsSerialized() )
                Controller.RunState |= Controller.State.ConfigError;            

            // Verify that we have a flash card.  We can't do much of anything without one.
            bool haveFlashCard = FlashCard.WaitForMount();
            if ( haveFlashCard )
                Log.Info( "Found a mounted flash card" );
            else
            {
                Log.Fatal( "ERROR: FLASH CARD NOT FOUND." );
                Controller.RunState |= Controller.State.FlashCardError;
            }

            Log.Info( Log.Dashes );
            Log.Info( "Firmware Version: " + Controller.FirmwareVersion );
            Log.Info( Log.Dashes );
            Controller.LogSystemInfo();
            Log.Info( Log.Dashes );
            Controller.LogProcessorInfo();
            Log.Info( Log.Dashes );
            Controller.LogMemory();
            Log.Info( Log.Dashes );
            if ( haveFlashCard )
            {
                FlashCard.Log();
                Log.Info( Log.Dashes );
            }
            Log.Info( "MAC Address: " + Controller.GetWiredNetworkAdapter().MacAddress );
            Log.Info( Log.Dashes );

            if ( haveFlashCard )
            {
                DataAccess.DataAccess.StartInet();
                DataAccess.DataAccess.StartInetQueue();
            }

            // Before we can start the WebAppService, we need to initialize
            // the password used to login to it.
            // Note that for debug builds, we configure it to not use SLL.
            // There are two reasons for this...
            // 1) SSL slows done iNet DS Configurator, which is annoying
            // when we developers are constantly logging into it to use it.
            // 2) There is some sort of problem when Padarn is using SSL
            // that causes the Visual Studio debugger to lose its connections
            // to the device.
#if DEBUG
            Configuration.ConfigureWebApp( Configuration.DockingStation.WebAppPassword, false );
#else
            Configuration.ConfigureWebApp( Configuration.DockingStation.WebAppPassword, true );
#endif

            if ( Controller.RunState == Controller.State.OK )
                InitializeWinsock();

            // AJAY: INS-8380 Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
            // If service account is configured to override event priority in admin console, 
            // this method reorders events that needs to be executed on docking station.
            if (Configuration.IsRepairAccount() && Configuration.DockingStation.UpgradeOnErrorFail)
                ISC.iNet.DS.DomainModel.EventCode.SetEventPriorityForService();

            // Initialize, wire up and start all necessary services.
            // Note that this will be iterated every time any of the
            // services gets interrupted.

            // Create the services.
            Log.Debug( "Creating services..." );

            _resourceService = new ResourceService( this );
            Log.Debug( ResourceService.Name + " has been created." );

            _reporterService = new ReporterService( this );
            Log.Debug( ReporterService.Name + " has been created." );

            _executerService = new ExecuterService( this );
            Log.Debug( ExecuterService.Name + " has been created." );

            _switchService = new SwitchService( this );
            Log.Debug( SwitchService.Name + " has been created." );

            _chargingService = new ChargingService( this );
            Log.Debug( ChargingService.Name + " has been created." );

            _webAppService = new WebAppService( this );
            Log.Debug( WebAppService.Name + " has been created." );

            _controllerWrapper = ControllerWrapper.Instance;

            _pumpWrapper = PumpManager.Instance;

            _lcdWrapper = LCDWrapper.Instance;

            _smartCardWrapper = SmartCardWrapper.Instance;

            // Start the services.
            Log.Debug( "Starting service threads..." );

            if ( Controller.RunState == Controller.State.OK )
                ResourceService.Start();

			// Don't start the Switch, Charging, or Executer service if the DS has
			// not been serialized.  Only the Configurator web app will be functional.
			if ( Configuration.DockingStation.IsSerialized() )
			{
				ExecuterService.Start();

				if ( Controller.RunState == Controller.State.OK )
					SwitchService.Start();

				if ( Controller.RunState == Controller.State.OK )
					ChargingService.Start();
			}
			else
			{
				// update the LCD
				ConsoleService.UpdateState( ConsoleState.NotSerialized );
			}

            if ( Controller.RunState == Controller.State.OK )
                ReporterService.Start();

			WebAppService.Start();

            Log.Debug( "Service threads started." );
			
			// the controller's static constructor will initialize the other logging settings, 
			// but we always want to log all the initialization stuff above out the serial port
			Log.LogToSerialPort = Configuration.ServiceMode ? true : Configuration.DockingStation.LogToSerialPort; // always log in service mode
			
            // Determine if sequenceId passed in as argument. This is number that will be automatically
            // passed in as an argument to the process when Windows CE boots the device.  If process
            // is manually started, then no sequence ID will be passed.  If sequenceId is passed in,
            // then process MUST call SignalStarted when it feels that it's safely up and running 
            // in order to continue with the proper boot sequence.
            if ( args.Length > 0 )
            {
                uint sequenceId = 0;
                try
                {
                    sequenceId = UInt32.Parse( args[0] );
                }
                catch
                {
                    Log.Debug( "Invalid sequenceId (" + args[0] + ") found.  SignalStarted not called." );
                }
                if ( sequenceId > 0 )
                {
                    Log.Debug( "SignalStarted(" + sequenceId + ")" );
                    WinCeApi.SignalStarted( sequenceId );
                }
            }

			// INS-6183, 6/8/2015 - This watchdog is used in the below while-loop. The  while-loop
			// periodically calls MonitorNetworkConnection(), which calls GetWiredNetworkAdapter(),
			// which calls GetNetworkAdapters().  GetNetworkAdapters() makes a call to OpenNetCF
			// SDF's GetAllNetworkInterfaces() to get the networking info from the OS.
			// Sometimes that call throws a Data Abort (unmanaged exception).  Unmanaged exceptions
			// cannot be caught in .Net, so the exception is thrown outside and above the application,
			// which causes OS to display a message box informing of the Data Abort.  The message box
			// waits for a user to press its OK button, so the thread that threw the Data Abort is
			// effectly hung waiting for somebody to press an OK button which is never going to happen.
			// The data abort may be thrown by a thread other this Master thread. But this watchdog
			// for the master thread should still work in that situation.  This is because the
			// aforementioned GetNetworkAdapters has a "lock" block in it. If another thread calls 
			// GetNetworkAdapters, it will a obtain a lock, then call into SDF's GetAllNetworkInterfaces.
			// If the data abort is then thrown, then GetNetworkAdapters will not have a chance to
			// release its lock. Then, next time master thread trys to call GetNetworkAdapters, it will
			// hang trying to obtain a lock, which it will never be able to do, so the watchdog timer
			// will eventually expire, causing a reboot, as desired.
			WatchDog watchDog = new WatchDog( "MasterWatchDog", 60000, Log.LogToFile ); // reboot if not refreshed in 60 seconds.
			watchDog.Start();

            ///////////////////////////////////////////////////////////////////////
            // Used for smart card debugging.  Please leave this code here for now.
            ///////////////////////////////////////////////////////////////////////
            //            I2CTestThread i2cTest= new I2CTestThread();
            //            i2cTest.StartWork();

            MonitorNetworkConnection();

            Controller.LogMemoryUsage();

            // Now that we've fired off the child worker threads, we have nothing more to do
            // except hang out and stay alive.  Lower our priority and take a nap.
			//Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
            int sleepTime = 5000;
            int lastCheck = 0;

            while ( true )
            {
                Thread.Sleep( sleepTime );

                lastCheck += sleepTime;

				if ( lastCheck >= 300000 ) // 5 minutes
                {
                    MonitorNetworkConnection();

                    //Logger.Debug( "Total Up Time: " + ( runTime / 1000 ) + " seconds" );
                    //Controller.LogMemoryUsage();
                    lastCheck = 0;
                }

                if ( Controller.RunState != Controller.State.OK )
                {
                    string errNum = ( (int)Controller.RunState ).ToString("x8");
                    // If a menu is active, skip the operation for now. (the only menu that should
                    // possibly ever be active is the factory reset confirmation menu).
                    if ( !ConsoleService.IsMenuActive )
                    {
                        Log.Trace("SYSTEM ERROR ENCOUNTERED with error number " + errNum );

                        if (errNum.Equals("00000001"))
                            ConsoleService.UpdateState(ConsoleState.ContactISCCode1011);
                        else if (errNum.Equals("00000002"))
                            ConsoleService.UpdateState(ConsoleState.ContactISCCode1012);
                        else if (errNum.Equals("00000004"))
                            ConsoleService.UpdateState(ConsoleState.ContactISCCode1014);
                        else if (errNum.Equals("00000008"))
                            ConsoleService.UpdateState(ConsoleState.ContactISCCode1018);
                        else if (errNum.Equals("00000010"))
                            ConsoleService.UpdateState(ConsoleState.ContactISCCode10110);
                        else
                            ConsoleService.UpdateState(ConsoleState.ContactISCCode10160);
                    }                     
                }

				if ( lastCheck == 0 || lastCheck >= 10000 )
					watchDog.Refresh();
            }

        } // end-Run

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// <para></para>
        /// Web services (and all connections made via sockets) use the IP address and a
        /// client side 'ephemeral' socket port number as an identifier. The firewall uses
        /// this identifier in order to route traffic through to the destination (in our 
        /// case, the iNet Core server).
        /// </para>
        /// <para>
        /// In the case of iNetDS, internally and for customers, all units' connections
        /// look to the firewall as if they came from the internet gateway of the customer
        /// (there is a similar situation for internal traffic).  i.e., multple docking
        /// stations from one customer site all look to the server as if they have the same
        /// IP addreses; i.e., they all look like the same client.
        /// </para>
        /// <para>
        /// Windows CE's Winsock DLL (which is used by the .Net web services) assigns
        /// the client-side ephermeral port and there is a problem in how winsock chooses
        /// the port numbers.  The current implementation seems to always assign an initial 
        /// port of 49152, and every time a new port is needed, it increments, going all the 
        /// way up to max range 65535 (cycling back around to 49152 once the max range is
        /// exceeded). It will try each port in consective order.
        /// </para>
        /// <para>
        /// Therefore, using this behavior of winsock unchecked, if multiple iNetDS units
        /// all boot up and try to connect to iNet in unision, they will all try and use
        /// a port number of 49152.  The server can't correctly communicate to multple
        /// docking stations all using the same port number because to it, they're all
        /// the same client due to the aforemetioned mapping of the gateway IP address.
        /// </para>
        /// <para>
        /// To get around this problem, this routine tries to initialize winsock to using
        /// an initial a random port number instead of its built-in behavior of using
        /// initial port of 49152.  To do that, it calculutes a random port number for
        /// the docking station to use. It then initializes winsock to use that port number
        /// by repeatedly calling winsock's 'bind()' call.  (Each call to bind() causes
        /// winsock to increment the ephemeral port it will use next).
        /// </para>
        /// <note>
        /// NOTE, JMP, 3/2/2016 - I suspect this is not needed anymore.  DSX-L is not doing
        /// this and has never exhibited any of the problems that iNetDS did prior to us
        /// implementing this logic. I suspect that Microsoft may have corrected winsock 
        /// issues in some update to Windows CE that we've incorporated.
        /// </note>
        /// </remarks>
        private void InitializeWinsock() // Added for fix of INS-1766 ("After booting up, the first attempt to connect to iNet always fails"
        {
            // We need to try and seed the random number generator with a unique value.
            // Using the Random class's default constructor is not good enough since it
            // seeds using the number of milliseconds since bootup - Docking stations can
            // easily bootup in the same exact number of milliseconds making that not
            // a good enough seed.  But they'll commonly bootup in varying amounts of time
            // to make that value at least a good basis for a seed. We improve on it by
            // using the docking station's MAC address, which should be unique, and we
            // also use the number of seconds that have elapsed 'today' since midnight.
            const string funcName = "InitializeWinsock: ";

            // We start with a 'base seed' thhat is the same value that the Ramdom class's default constructor uses.
            int randomSeed = System.Environment.TickCount;

            Log.Trace( string.Format( "{0}System.Environment.TickCount={1}", funcName, randomSeed ) );

            // Determine number of seconds that have elapsed today, since midnight,
            // and add that to our base seed.

            DateTime now = DateTime.UtcNow;
            DateTime midnight = new DateTime( now.Year, now.Month, now.Day );
            int secondsToday = (int)( (TimeSpan)( now - midnight ) ).TotalSeconds;  // Number of seconds that have elapsed today
            Log.Trace( string.Format( "{0}todaySeconds={1}", funcName, secondsToday ) );

            randomSeed += secondsToday;

            // Try and get unique value from docking station's MAC address.  We ignore the first 
            // half of the MAC sinc that contains Industrial Scientific's OID which is is the same 
            // on each docking station.  We instead just take the second half of the MAC, which should
            // be unique to the docking station, and parse it out to an integer, and add it to our seed.
            try
            {
                // Convert MAC from mac address format to pure hexidecimal format that we can then parse into an integer.
                string mac = Configuration.DockingStation.NetworkSettings.MacAddress.Replace( ":", string.Empty );
                if ( mac.Length > 6 )
                {
                    // Get last half of MAC (ignore the OID which is the 1st half)
                    mac = mac.Substring( mac.Length - 6, 6 );
                    int macInt = int.Parse( mac, System.Globalization.NumberStyles.AllowHexSpecifier );
                    Log.Trace( string.Format( "{0}macInt={1}", funcName, macInt ) );
                    randomSeed += macInt;
                }
            }
            catch ( Exception e )
            {
                Log.Error( "Failed to parse MAC for random seed.", e );
            }

            try
            {
                Log.Trace( string.Format( "{0}randomSeed={1}", funcName, randomSeed ) );

#if !DEBUG
                // Normal range for ephemeral ports is 49152 to 65535, which is 16,383 ports.
                // Therefore, we generate a random number of 0 to 16383
                int randomPorts = new Random( randomSeed ).Next( 0, 16383 );
#else
                // Because the loop below can be nearly 15 seconds or so if randomPorts is
                // large, we decrease the maximum range that Next can return for Debug builds.
                // This is so that when trying to debug, we don't have to wait for the loop to iterate.
                // This may cause more socket port collision when debugging, but experience has shown
                // this is relatively rare for us.
                int randomPorts = new Random( randomSeed ).Next( 0, 100 );
#endif
                Log.Debug( string.Format( "{0}Seeding winsock's ephemeral port (randomPorts={1})", funcName, randomPorts ) );

                // Each time we call 'bind', we cause Winsock to increment its internal ephemeral
                // port counter by one.  It always starts at 49152.  So, if randomPorts is, for example,
                // 6543, then by calling 'bind' 6,543 times, we cause winsock to set it's internal
                // ephemeral port counter to 55965.\
                // Note that when running on the iNetDS hardware, the speed of this loop seems
                // to be close to (but under) 1 second per 1000 iterations.
                for ( int i = 1; i <= randomPorts; i++ )
                {
                    Socket sock = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
                    sock.Bind( new IPEndPoint( IPAddress.Loopback, 0 ) );
                    if ( i == randomPorts ) // last iteration through the loop?
                        Log.Debug( string.Format( "{0}Winsock's ephemeral port seeded with {1}", funcName, ( (IPEndPoint)sock.LocalEndPoint ).Port ) );
                    sock.Close();
                }
            }
            catch ( Exception e )
            {
                Log.Error( e );
                return;
            }


            /* FOR DEV TESTING PURPOSES...
            //for ( int attempt = 1; attempt <= 5; attempt++ )
            {
                ISC.iNet.DS.Services.ExchangeStatusOperation eso = new ISC.iNet.DS.Services.ExchangeStatusOperation( string.Empty, false );
                try
                {
                    ISC.iNet.DS.DomainModel.ExchangeStatusEvent ese = (ISC.iNet.DS.DomainModel.ExchangeStatusEvent)eso.Execute();

                    if ( ese == null )
                        Log.Debug( "ExchangeStatusOperation FAILED - null" );

                    if ( ese.InetStatus.Error != string.Empty )
                        Log.Debug( string.Format( funcName + "ExchangeStatusOperation FAILED - \"{0}\"", ese.InetStatus.Error ) );
                    else
                        Log.Debug( "ExchangeStatusOperation SUCCESS" );
                }
                catch ( Exception e )
                {
                    Log.Error( "ExchangeStatusOperation FAILED", e );
                }
            }
            Log.Debug( "Sleeping until reboot..." );
            Thread.Sleep( 10000 );
            Controller.PerformSoftReset();
            */

        }

        /// <summary>
        /// Intended to be called periodically, this method looks to see if
        /// network connection (IP address) has changed since the last time
        /// this method was called.
        /// </summary>
        /// <remarks>
        /// It would probalby be a better way of doing this if we used 
        /// OpenNetCF's NetworkInterfaceWatcher class.
        /// </remarks>
        private void MonitorNetworkConnection()
        {

            try
            {
                NetworkAdapterInfo nic = Controller.GetWiredNetworkAdapter(); // will this ever return null? not sure.

                if ( nic == null && _networkAdapterInfo == null )
                {
                    ReporterService.Networked = false;
                    return;
                }

                if ( nic == null && _networkAdapterInfo != null
                || nic != null && _networkAdapterInfo == null )
                {
                    _networkAdapterInfo = nic;
                    LogNetworkAdapterInfo();
                    ReporterService.Networked = _networkAdapterInfo.IsNetworked();
                }
                else //  nic != null && nic != null
                {
                    // Only log something if we detect a network change.
                    if ( nic.DhcpEnabled != _networkAdapterInfo.DhcpEnabled
                    ||   nic.IpAddress != _networkAdapterInfo.IpAddress
                    ||   nic.SubnetMask != _networkAdapterInfo.SubnetMask
                    ||   nic.Gateway != _networkAdapterInfo.Gateway
                    ||   nic.DnsPrimary != _networkAdapterInfo.DnsPrimary
                    ||   nic.DnsSecondary != _networkAdapterInfo.DnsSecondary )
                    {
                        _networkAdapterInfo = nic;
                        LogNetworkAdapterInfo();
                        ReporterService.Networked = _networkAdapterInfo.IsNetworked();
                    }
                }
            }
            catch ( Exception ex )
            {
                Log.Error( ex );
            }

        }

        private void LogNetworkAdapterInfo()
        {
            if ( _networkAdapterInfo == null )
                return;

            //Log.Info( "OperationalStatus=" + _networkAdapterInfo._nic.OperationalStatus.ToString() );
            //Log.Info( "InterfaceOperationalStatus=" + _networkAdapterInfo._nic.InterfaceOperationalStatus.ToString() );

            string msg = string.Format( "NetworkAdapterInfo [{0}]:\r\nIP Address...... : {1} ({2})\r\nSubnet Mask..... : {3}\r\nGateway......... : {4}\r\nPrimary DNS..... : {5}\r\nSecondary DNS... : {6}",
                _networkAdapterInfo.AdapterName,
                _networkAdapterInfo.IpAddress, _networkAdapterInfo.DhcpEnabled ? "DHCP" : "Static",
                _networkAdapterInfo.SubnetMask, _networkAdapterInfo.Gateway,
                _networkAdapterInfo.DnsPrimary, _networkAdapterInfo.DnsSecondary );

            Log.Info( msg );
        }

        /// <summary>
        /// Should be called prior to performing a reboot.
        /// Pauses services and components that write to flash memory
        /// so that they're not writing at time of reboot.
        /// </summary>
        public void PrepareForReset()
        {
            const string func = "PrepareForReset: ";

            // The intent of pausing the Reporter service is to stop inet uploads from being queued or deqeueued.

            Log.Warning( string.Format( "{0}Pausing {1}", func, Master.Instance.ReporterService.Name ) );
            Master.Instance.ReporterService.Paused = true;

            Log.Warning( string.Format( "{0}Stopping DataAccess", func ) );
            ISC.iNet.DS.DataAccess.DataAccess.Stop();

            // Although we've paused the reporter service, it may have been in the middle of a web service call
            // when paused so doesn't know about it's changed status yet.
            for ( int i = 180; i > 0; i-- )
            {
                Log.Warning( string.Format( "{0}Waiting for {1} to stop. (tries left: {2})...", func, Master.Instance.ReporterService.Name, i ) );
                Thread.Sleep( 1000 );

                // Note that pausing the ReporterService has a side effect of also 'pausing' the upload queue.
                if ( !Master.Instance.ReporterService.Running() )
                {
                    Log.Warning( string.Format( "{0}{1} appears to be successfully paused.", func, Master.Instance.ReporterService.Name ) );
                    break;
                }
            }

            if ( Master.Instance.ReporterService.Running() ) // still not stopped?
            {
                Log.Warning( string.Format( "{0}{1} unsuccessfully stopped!", func, Master.Instance.ReporterService.Name ) );
            }
        }

    }  // end-class Master

}  // end-namespace
