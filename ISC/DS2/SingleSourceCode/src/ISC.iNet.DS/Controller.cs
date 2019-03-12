using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE;
using ISC.WinCE.Logger;
using Microsoft.Win32;

namespace ISC.iNet.DS
{

	//////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality for controlling docking station operation.
	/// </summary>
    public sealed class Controller
    {
        #region Fields

        /// <summary>
        /// COM port used to communicate with the docked instrument.
        /// </summary>
        public const string INSTRUMENT_COM_PORT = "COM3:";

        /// <summary>
        /// Name of network adapter on the Atmel 9G45 board.
        /// </summary>
        public const string WIRED_NIC_NAME = "EMACB1";

        /// <summary>
        /// The path to the Windows directory, including the trailing "\"
        /// </summary>
        public const string WINDOWS_PATH = @"\WINDOWS\";


        /// <summary>
        /// The full pathname to the NAND flash file system.  Includes a trailing "\".
        /// This is where the WinCE registry, our serialization info, etc. is stored.
        /// This is *not* the microSD card.
        /// </summary>
        public const string PERSISTANCE_PATH = @"\Reliance_Flash\";

        /// <summary>
        /// The full pathname to the micro-SD card.  Includes a trailing "\".
        /// </summary>
        public const string FLASHCARD_PATH = @"\Reliance_Flash2\";

        /// <summary>
        /// The full pathname to the USB thumb drive.  Includes a trailing "\".
        /// </summary>
        public const string USB_DRIVE_PATH = @"\Hard Disk\";

        /// <summary>
        /// The full pathname to the inserted USB flash drive.  Includes a trailing "\".
        /// </summary>
        private static readonly object _nandPersistenceLock = new object();

        /// <summary>
        /// Name of database file that data (settings, schedules, etc.) downloaded from iNet are stored in.
        /// </summary>
        public const string INET_DB_NAME = "iNet.db3"; // database file name

        /// <summary>
        /// Name of the database where data that's queued for upload to iNet is stored.
        /// </summary>
        public const string INETQ_DB_NAME = "iNetQueue.db3"; // database file name

        /// <summary>
        /// If the docking station loses its programmed MAC address, it reverts to this known default MAC address.
        /// </summary>
        public const string DEFAULT_MAC_ADDRESS = "00:0B:D8:00:00:00";  // "00:0B:D8" is Industrial Scientific's registered OUI.

        private static string _firmwareVersion = string.Empty;

        /// <summary>
        /// The iGas port that's dedicated for use with fresh/zero air.
        /// </summary>
        public const int FRESH_AIR_GAS_PORT = 1;

        /// <summary>
        /// An attached USB printer always appears as this printer port.
        /// </summary>
        public const string PRINTER_PORT_USB = "LPT1:";
		public const string PRINTER_REGISTRY_PATH = @"Printers\Ports";

        /// <summary>
        /// The number of iGas ports this docking station has.
        /// </summary>
        //public const int MAX_GAS_PORTS = 3;

        private const int MAX_CHARGE_TIME = 7;
        private const int NUM_KEYS = 3;

        private static State _runStatus = State.OK;

        private static DateTime _lastRebootTime;

        private static string _applicationPath;

        private static volatile bool _fastIsDockedEnabled = true;

        #endregion Fields

        #region Enums

        [Flags]
        public enum State
        {
            OK             = 0x00000000,
            Unknown        = 0x00000001,
            InetDbError    = 0x00000002,
            InetQueueError = 0x00000004,
            ConfigError    = 0x00000008,
            FlashCardError = 0x00000010
        }

        public static State RunState 
        {
            get
            {
                return _runStatus;
            }
            set
            {
                _runStatus = value;
            }
        }

        /// <summary>
        /// Enumerates the keys on the keypad.
        /// </summary>
        public enum Key
        {
            None,
            Right,
            Middle,
            Left,
            RightLeft // both right & left pressed
        }

        /// <summary>
        /// Enumerates the LEDs.
        /// </summary>
        public enum LEDState : byte
        {
            Green = 1,
            Yellow,
            Red
        }

        internal enum PeripheralBoardRevision
        {
            Reserved = -1,

            /// <summary>
            /// Version 10 or later PCB that supports the Phoenix LCD.
            /// </summary>
            Phoenix = 1,

            /// <summary>
            /// Earlier than version 10 PCB that only supports the Okay LCD.
            /// </summary>
            Okaya = 0
        }

        #endregion Enums

        #region Constructors

        /// <summary>
        /// Class constructor
        /// </summary>
        static Controller()
        {
            // Note: According to Adeneo, we don't need to call A2DInit here during startup anymore
            // like the DS2 does. The A2D driver is automatically loaded and initialized during OS bootup.

            try
            {
                Configuration.Load();

				// we pass in true to ensure log messages are sent out the serial port while starting up
				Configuration.ApplyLogSettings( true );
            }
            catch ( Exception e )
            {
                Log.Warning( "FAILED TO LOAD SERIALIZATION AND CONFIG INFO.", e );
                Log.Debug( "NOT SERIALIZED?" );
            }

            LCD.SetLanguage( Configuration.DockingStation.Language );
        }


        /// <summary>
        /// Empty private ctor to assure only static use of the class.
        /// </summary>
        private Controller() {}

        #endregion

        #region Properties

        static public string ApplicationPath
        {
            get { return _applicationPath; }
            set { _applicationPath = value; }
        }

        /// <summary>
        /// Call to obtain an exclusive lock to the persistent data store before 
        /// reading/writing from/to it.
        /// </summary>
        /// <returns></returns>
        public static object NandPersistenceLock { get { return _nandPersistenceLock; } }

        /// <summary>
        /// Returns timestamp indicating when we were was last rebooted.
        /// </summary>
        public static DateTime LastRebootTime
        {
            get
            {
                return _lastRebootTime;
            }
            set
            {
                _lastRebootTime = value;
            }
        }
        #endregion

		#region Methods

        #region DllImport externs
        
		/// <summary>
		/// BSP SDK API: Gets the battery voltage.
		/// </summary>
        [DllImport( "sdk.dll" )]
		private static extern int A2DInit();

        /// <summary>
        /// Returns true if image successfully saved to NAND
        /// </summary>
        [DllImport( "sdk.dll" )]
        public static unsafe extern bool SendImageToNand( byte[] data, uint bufferSize );

        /// <summary>
		/// BSP SDK API: Gets the battery voltage.
		/// </summary>
        /// <returns>0 if successful, 1 on error. GetLastError may hold reason for error.</returns>
        [DllImport( "sdk.dll" )]
		private static unsafe extern int GetBatteryVoltage( short *data );

		/// <summary>
		/// BSP SDK API: Gets the battery voltage.
        /// </summary>
        /// <remarks>
        /// Note that this call takes around 250ms to complete. 
        /// This is because it takes multiple voltage reading samples
        /// and then returns an average of them.
        /// </remarks>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int Get12vCurrent ( ushort *data );


		/// <summary>
		/// BSP SDK API: Gets the core voltage of the IDS.
		/// </summary>
        [DllImport( "sdk.dll" )]
		private static unsafe extern int GetCoreVoltage( ushort *data );

		/// <summary>
		/// BSP SDK API: Reads the lamp door state - VX500 only.
        /// Also used with MX6 to determine if pump adapter is installed on the cradle.
		/// </summary>
        [DllImport( "sdk.dll" )]
		private static unsafe extern int GetLampDoor( ushort *data );

		/// <summary>
		/// BSP SDK API: Reads the photo cell state - VX500 only.
		/// </summary>
        [DllImport( "sdk.dll" )]
		private static unsafe extern int GetPhotocell( ushort* data );

		/// <summary>
		/// BSP SDK API: Gets a value indicating whether an instrument is docked or not on the IDS.
        /// Also used with the MX5 to determine of the diffusion lid is lowered or not.
		/// </summary>
        [DllImport( "sdk.dll" )]
		private static unsafe extern int GetInstrumentDetect( byte *data );

        /// <summary>
        /// BSP SDK API: Gets a value indicating whether given MX4 dock has new Ventis cradle or not  - MX4 docking station only.
        /// </summary>
        [DllImport("sdk.dll")]
        private static unsafe extern int GetNewVentisCradleDetect(ushort* data);

        /// <summary>
        /// BSP SDK API: Turns on or off the speaker.  
        /// </summary>
        /// <param name="data">1 turns on speaker. 0 turns it off.</param>
        /// <returns></returns>
        [DllImport( "sdk.dll" )]
		private static extern int SetBuzzerState( byte data );

		/// <summary>
		/// BSP SDK API: Turns the LEDs on or off.
		/// </summary>
        [DllImport( "sdk.dll" )]
		private static extern int SetLEDState( byte id , byte data );

        /// <summary>
        /// BSP SDK API: Controls the solenoid in the instrument cradle that routes
        /// gas flow to either the diffusion lid or instrument pump tubing.
        /// </summary>
        /// <remarks>
        /// The name of this function is a carry over from the DS2. It is no longer
        /// used for LEDs.  It used to be used to control additional LEDs that were
        /// on the VX500 docking station.
        /// </remarks>
        /// <param name="id">Must always be 2.</param>
        /// <param name="data">
        /// 1 changes solenoid to route gas to instrument pump pump.
        /// 0 changes solenoid to route gas to diffusion lid.
        /// </param>
        /// <returns></returns>
        [DllImport( "sdk.dll" )]
		private static extern int SetInterfaceLed( byte id , byte data );

		/// <summary>
		/// BSP SDK API: Returns whether or not a given key was pressed.
		/// </summary>
        [DllImport( "sdk.dll" )]
		private static unsafe extern int GetKeypadState( byte id , byte *data );

        /// <summary>
        /// BSP SDK API: Queries the peripheral board for its revision.
        /// </summary>
        /// <param name="MSB_Status"></param>
        /// <param name="LCB_Status"></param>
        /// <returns></returns>
        [DllImport( "sdk.dll" )]
        private static unsafe extern int GetPCBRevision( byte *MSB_Status, byte *LCB_Status );

        #endregion  // DllImport externs

        static private PeripheralBoardRevision GetPeripheralBoardRevision()
        {
            byte MSB; // most significant byte
            byte LSB; // least significant byte

            unsafe
            {
                GetPCBRevision( &MSB, &LSB );
            }

            PeripheralBoardRevision revision;

            if ( MSB == 1 )
            {
                if ( LSB == 1 )
                    revision = PeripheralBoardRevision.Reserved; // Reserved PCB revision level.
                else
                    revision = PeripheralBoardRevision.Reserved; // Reserved PCB revision level.
            }
            else // MSB == 0
            {
                if ( LSB == 1 )
                    revision = PeripheralBoardRevision.Phoenix; // Version 10 (or later) PCB; PCB allows LCD type to be queried.
                else
                    revision = PeripheralBoardRevision.Okaya; // First release PCB - Supports only the Okaya LCD.
            }

            Log.Debug( string.Format( "GetPeripheralBoardRevision: MSB={0}, LSB={1}, PCB Revision is \"{2}\".", MSB, LSB, revision.ToString() ) );

            return revision;
        }



        /// <summary>
        /// Return a NetworkAdapterInfo instance representing the integrated NIC on this device. 
        /// </summary>
        /// <returns></returns>
        static public NetworkAdapterInfo GetWiredNetworkAdapter()
        {
            // Get the network adapter info
            NetworkAdapterInfo networkAdapter = NetworkAdapterInfo.GetNetworkAdapters().Find( n => n.AdapterName == WIRED_NIC_NAME );

            if ( networkAdapter == null )
                networkAdapter = new NetworkAdapterInfo();

            return networkAdapter;
        }
		
		/// <summary>
		/// Returns whether or not the docking station currently detects an attached USB drive.
		/// </summary>
		/// <param name="logLabel">Label to log debug message with. e.g. "ImageUpgrader: "</param>
		/// <returns>true - USB drive detected; false - USB drive not detected</returns>
		public static bool IsUsbDriveAttached( string logLabel )
		{
			bool isUsbDriveAttached = false;

			try
			{
				isUsbDriveAttached = Directory.Exists( USB_DRIVE_PATH );

				if ( isUsbDriveAttached )
				{
					// Log with a higher level the less common scenario of finding a USB drive
					Log.Debug( string.Format( "{0}USB drive \"{1}\" found", logLabel, USB_DRIVE_PATH ) );
				}
				else
				{
					// Log with a lower level when a USB drive is not found to not burden the system
					Log.Trace( string.Format( "{0}USB drive \"{1}\" not found", logLabel, USB_DRIVE_PATH ) );
				}
			}
			catch ( Exception ex )
			{
				Log.Error( string.Format( "{0}USB drive \"{1}\" not found - {2}", logLabel, USB_DRIVE_PATH, ex.ToString() ) );
			}		

			return isUsbDriveAttached;
		}

        /// <summary>
        /// Returns true if the NAND flash memory's 4-bit hardware ECC capability has been enabled.
        /// False is returned if only 1-bit software ECC is being used.
        /// </summary>
        static public bool IsNandFlashEccEnabled()
        {
            // The OS has two NAND flash drivers.  The name of the driver that is not capable of
            // hardware ECC is under HKLM\Drivers\BuiltIn\NandFlash.  The name of the driver
            // that *is* capable of hardware ECC is under HKLM\Drivers\BuiltIn\NandFlash4.
            // Only one of the two drivers can be loaded and used by the OS. During bootup,
            // the BSP determines if the NAND flash memory's hardware ECC has been enabled.
            // If so, then it loads the driver that is named under the Nandflash4 key,
            // and it changes the name of the driver stored under the NandFlash key to "null.dll"
            // so that the OS cannot find and load the DLL.
            // See also: FMD_Init() function in C:\WINCE600\PLATFORM\COMMON\SRC\SOC\ATMEL\COMMON\DRIVERS\NandFlash\Nandflashlib\NandFlash.c

            RegistryKey nandFlash = Registry.LocalMachine.OpenSubKey( @"Drivers\BuiltIn\NandFlash" );
            object o = nandFlash.GetValue( "Dll" );
            return ( o is string ) && ( (string)o == "null.dll" );
        }

        /// <summary>
        /// Returns whether or not the docking station currently detects a USB-attached printer.
        /// </summary>
        /// <returns></returns>
        static public bool IsUsbPrinterAttached()
        {
			RegistryKey printerPorts = Registry.LocalMachine.OpenSubKey( PRINTER_REGISTRY_PATH );

            if ( printerPorts == null )
                return false;

            int portNum = 1;

            while ( true )
            {
                object o = printerPorts.GetValue( "Port" + portNum++ );

                if ( o == null )
                    break;

                if ( ( o is string ) && ( (string)o == PRINTER_PORT_USB ) )
                    return true;
            }

            return false;
        }

		/// <summary>
		/// Inspects the Printers\Ports registry path for HKEY_LOCAL_MACHINE and determines
		/// if any of the ports have values that start with LPT that are not LPT1:.
		/// </summary>
		/// <returns>True - Found 1 or more ports that have a value that starts with LPT and are not LPT1:.
		/// False - Found no ports that have a value that starts with LPT and are not LPT1:.</returns>
		static public bool IsUsbPrinterNotOnLpt1()
		{
			RegistryKey printerPorts = Registry.LocalMachine.OpenSubKey( PRINTER_REGISTRY_PATH );

			// this should not be null
			if ( printerPorts == null )
				return false;

			string[] valueNames = printerPorts.GetValueNames();
			foreach ( string name in valueNames )
			{
				object o = printerPorts.GetValue( name );

				// Port5 could be null, but Port6 could have a value
				if ( o == null )
					continue;

				if ( o is string )
				{
					string value = (string)o;
					if ( value.StartsWith( "LPT" ) && value != PRINTER_PORT_USB )
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Inspects the Printers\Ports registry path for HKEY_LOCAL_MACHINE and determines
		/// if any of the ports have values that start with LPT.
		/// </summary>
		/// <returns>True - Found 1 or more ports that have a value that starts with LPT.
		/// False - Found no ports that have a value that starts with LPT.</returns>
		static public bool IsUsbPrinterOnAnyLptPort()
		{
			RegistryKey printerPorts = Registry.LocalMachine.OpenSubKey( @"Printers\Ports" );

			// this should not be null
			if ( printerPorts == null )
				return false;

			// Port1, Port2, etc...
			string[] valueNames = printerPorts.GetValueNames();
			foreach ( string name in valueNames )
			{
				object o = printerPorts.GetValue( name );

				// Port5 could be null, but Port6 could have a value
				if ( o == null )
					continue;

				if ( o is string )
				{
					string value = (string)o;
					if ( value.StartsWith( "LPT" ) )
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Inspects the Printers\Ports registry path for HKEY_LOCAL_MACHINE and determines
		/// if only LPT1: exists and no other ports have a value that starts with LPT.
		/// </summary>
		/// <returns>True - Found exactly 1 port that has a value that starts with LPT and it was LPT1:.
		/// False - Found multiple ports that have a value that starts with LPT, or did not find LPT1:.</returns>
		static public bool IsUsbPrinterOnlyOnLpt1()
		{
			RegistryKey printerPorts = Registry.LocalMachine.OpenSubKey( PRINTER_REGISTRY_PATH );

			// this should not be null
			if ( printerPorts == null )
				return false;

			bool hasLpt1 = false;

			// Port1, Port2, etc...
			string[] valueNames = printerPorts.GetValueNames();
			foreach ( string name in valueNames )
			{
				object o = printerPorts.GetValue( name );

				// Port5 could be null, but Port6 could have a value
				if ( o == null )
					continue;

				if ( o is string )
				{
					string value = (string)o;
					if ( value == PRINTER_PORT_USB )
					{
						hasLpt1 = true;
					}
					else if ( value.StartsWith( "LPT" ) && value != PRINTER_PORT_USB )
					{
						return false;
					}
				}
			}

			return hasLpt1;
		}

		/// <summary>
		/// Inspects the Printers\Ports registry path for HKEY_LOCAL_MACHINE and logs
		/// any of the port values that start with LPT.
		/// </summary>
		static public void LogLptPorts()
		{
			RegistryKey printerPorts = Registry.LocalMachine.OpenSubKey( @"Printers\Ports" );

			if ( printerPorts == null )
			{
				Log.Debug( "USB Printer: No LPT ports found." );
				return;
			}

			string[] valueNames = printerPorts.GetValueNames();
			foreach ( string name in valueNames )
			{
				object o = printerPorts.GetValue( name );

				// Port5 could be null, but Port6 could have a value
				if ( o == null )
					continue;

				if ( o is string )
				{
					string value = (string)o;
					if ( value.StartsWith( "LPT" ) )
					{
						Log.Debug( "USB Printer: " + value );
					}
				}
			}
		}

        /// <summary>
        /// Queries for thumb drive containing service mode key.
        /// This routine is intended to be called once during bootup.
        /// </summary>
        static public bool DetermineServiceMode()
        {
            try
            {
                return new ServiceMode().IsServiceMode();
            }
            catch ( Exception e )
            {
                Log.Error( e );
            }
            
            return false;
        }

        static public void CreateServiceModeFile()
        {
            new ServiceMode().CreateServiceModeFile();
        }

        //INS-7008
        /// <summary>
        /// Returns a value indicating whether docking station has new Ventis cradle or not.
        /// This routine is intended to be called only for MX4 docking stations once during bootup.
        /// </summary>
        /// <returns>True if dock has new Ventis cradle, false otherwise</returns>
        public static bool IsNewVentisCradle() 
        {
            Log.Debug("Function : IsNewVentisCradle");
            ushort data;

            unsafe
            {
                GetNewVentisCradleDetect(&data);
            }
            
            Log.Debug("Has new Ventis cradle:" + (data == 1 ? "true " : "false"));

            return data == 1;
        }

        /// <summary>
        /// Retrieve the docking station's information.
        /// </summary>
        /// <returns>A new instance of a populated docking station.
        /// The caller may do what they wish with it.</returns>
        static public DockingStation GetDockingStation()
        {
            // Get copy of current configuration settings.
            DockingStation dockingStation = (DockingStation)Configuration.DockingStation.Clone();

            dockingStation.SoftwareVersion = FirmwareVersion;

            //dockingStation.HardwareVersion = Configuration.HardwareVersion;
            dockingStation.LastRebootTime = Controller.LastRebootTime;

            Controller.PeripheralBoardRevision pcbRevision = Controller.GetPeripheralBoardRevision();
            dockingStation.PeripheralBoardRevision = pcbRevision.ToString();
            // Can only query the board for LCD type if it's the newer board that supports the phoenix LCD...
            if ( pcbRevision == PeripheralBoardRevision.Phoenix )
                dockingStation.LcdType = LCD.GetLcdType().ToString(); // Query 
            else
                dockingStation.LcdType = LCD.Type.Okaya.ToString();

            // Network information.  Overwrite the network settings returned by 
            // Configuration.GetDockingStation, with the actual current network settings...
            // e.g., just because we're configured to use a specific gateway
            // doesn't mean we actually are able to and are doing so.
            NetworkAdapterInfo nic = GetWiredNetworkAdapter();
            dockingStation.NetworkSettings.MacAddress = nic.MacAddress;
            dockingStation.NetworkSettings.Gateway = NetworkAdapterInfo.IpAddressToString( nic.Gateway );
            dockingStation.NetworkSettings.SubnetMask = NetworkAdapterInfo.IpAddressToString( nic.SubnetMask );
            dockingStation.NetworkSettings.IpAddress = NetworkAdapterInfo.IpAddressToString( nic.IpAddress );
            dockingStation.NetworkSettings.DhcpEnabled = nic.DhcpEnabled;
            dockingStation.NetworkSettings.DnsPrimary = NetworkAdapterInfo.IpAddressToString( nic.DnsPrimary );
            dockingStation.NetworkSettings.DnsSecondary = NetworkAdapterInfo.IpAddressToString( nic.DnsSecondary );
            dockingStation.LogLevel = Log.Level;
            dockingStation.LogCapacity = Log.Capacity;

            try // Flash card information
            {
                dockingStation.FlashCardInfo = new DriveInfo( FLASHCARD_PATH.TrimEnd( new char[] { '\\' } ) );
            }
            catch ( ArgumentException ae ) // thrown when card isn't currently inserted.
            {
                Log.Error( string.Format( "UNABLE TO FIND FLASH CARD \"{0}\" - {1}", FLASHCARD_PATH, ae.Message ) );
            }
            catch ( Exception e ) // unexpected
            {
                Log.Error( string.Format( "UNABLE TO FIND FLASH CARD \"{0}\" - {1}", FLASHCARD_PATH, e.ToString() ) );
            }

            // The webapp password is stored in two places:
            // 1) the config.properties file specifies what it *should be*.  This is the Configuration.DockingStation.WebAppPassword property.
            // 2) the application's .net configuration file specifies what it *actually is*.
            // We want to know what it actually is so that's what we return.
            try
            {
                dockingStation.WebAppPassword = Configuration.GetWebAppPassword();
            }
            catch ( Exception ex )
            {
                Log.Error( string.Format( "Unable to read application configuration file", ex ) );
            }

            dockingStation.PrinterAttached = IsUsbPrinterAttached();

            dockingStation.NandFlashEcc = IsNandFlashEccEnabled();

            return dockingStation;
        }

        public static void LogDockingStation( DockingStation ds )
        {
            Configuration.LogSerialization( ds );
            Configuration.LogConfiguration( ds );
			Log.Debug( "            LastRebootTime: " + Log.DateTimeToString( ds.LastRebootTime ) );
			Log.Debug( "        PeripheralBoardRev: " + ds.PeripheralBoardRevision );
			Log.Debug( "                   LcdType: " + ds.LcdType );
			Log.Debug( "    NAND Flash ECC Enabled: " + ds.NandFlashEcc );
			Log.Debug( "                MacAddress: " + ds.NetworkSettings.MacAddress );
			Log.Debug( "                   Gateway: " + ds.NetworkSettings.Gateway );
			Log.Debug( "                SubnetMask: " + ds.NetworkSettings.SubnetMask );
			Log.Debug( "                 IpAddress: " + ds.NetworkSettings.IpAddress );
			Log.Debug( "               DhcpEnabled: " + ds.NetworkSettings.DhcpEnabled );
			Log.Debug( "                DnsPrimary: " + ds.NetworkSettings.DnsPrimary );
			Log.Debug( "              DnsSecondary: " + ds.NetworkSettings.DnsSecondary );
			//Log.Debug( "      MicroSD Manufacturer: " + ( ds.FlashCardInfo != null ? ds.FlashCardInfo.ManufacturerID : string.Empty ) );
            //Log.Debug( "               MicroSD S/N: " + ( ds.FlashCardInfo != null ? ds.FlashCardInfo.SerialNumber : string.Empty ) );
            Log.Debug( "        MicroSD Total Free: " + ( ds.FlashCardInfo != null ? ds.FlashCardInfo.TotalFreeSpace.ToString() : string.Empty ) );
            Log.Debug( "    MicroSD Available Free: " + ( ds.FlashCardInfo != null ? ds.FlashCardInfo.AvailableFreeSpace.ToString() : string.Empty ) );
			Log.Debug( "          Printer Attached: " + ds.PrinterAttached );

        }

		/// <summary>
		/// Perform a soft reset of the docking station.
		/// </summary>
		public static void PerformSoftReset()
		{
            Log.Debug( "***************  PERFORMING SOFT RESET !  ***************" );

            // Don't reboot if some other thread is in middle of updating flash
            lock ( FlashCard.Lock )
            {
				lock ( Log.NandFlashLock )
				{
					WinCeApi.SetSystemPowerState( null, WinCeApi.POWER_STATE_RESET, WinCeApi.POWER_FORCE );
				}
            }
		}


		/// <summary>
		/// Avoid the banker's rounding inherent in Math.Round()
		/// </summary>
		/// <param name="val">The value to round.</param>
		/// <param name="precision">The precision of the answer.</param>
		/// <returns></returns>
		public static double Round( double val, int precision )
		{
			if ( precision <= 0 )
			{
				return Math.Round( val , precision );
			}
			else
			{
				return Math.Round( val + Math.Pow( .1 , precision + 1 ) , precision );
			}
		}

		/// <summary>
		/// Get the local ip address for the IDS.
		/// </summary>
		/// <returns></returns>
        [Obsolete("instead, use NetworkAdapter.GetWiredNetworkAdapter().IpAddress.",true)]
        public static IPAddress GetLocalIpAddress( /*int which*/ )
		{
            // We want the first ipv4 address (there can be multiples if there are multiple network adapters)
            int which = 1;

            IPHostEntry hostEntry = null;
			try
			{
                string hostName = System.Net.Dns.GetHostName();
                hostEntry = System.Net.Dns.GetHostEntry( hostName );
            }
		    catch ( Exception e )
		    {
                //Log.Error( string.Format( "GetLocalIpAddress({0})", which ), e );
                Log.Error( "GetLocalIpAddress()", e );
                return IPAddress.None; // None is equivalent to 255.255.255.255
		    }

            int ipv4Count = 0; // Keep track of how many ipv4 addresses we see in the list.

            foreach ( IPAddress myIP in hostEntry.AddressList )
            {
                // Look for ipv4 address (the list likely contains
                // an ipv6 address, too).
                if ( myIP.AddressFamily != AddressFamily.InterNetwork )
                    continue;

                if ( ++ipv4Count == which )
                {
                    Log.Trace( "LOCAL IP ADDRESS: " + myIP.ToString() );
                    return myIP;
                }
            }
            return IPAddress.None; // None is equivalent to 255.255.255.255
		}



		/// <summary>
		/// Convert a string into the appropriate device type.
		/// </summary>
		/// <param name="type">The string to convert.</param>
		/// <returns>The appropriate device type.</returns>
		public static DeviceType ConvertDeviceType( string type )
		{
            DeviceType deviceType = DeviceType.Unknown;

            try
            {
                deviceType = (DeviceType)Enum.Parse( typeof( DeviceType ), type, true );
            }
            catch ( Exception e )
            {
                Log.Error( string.Format( "ConvertDeviceType: Type=\"{0}\"", type ), e );
            }

            return deviceType;
		}

		/// <summary>
		/// Get the 12V Current
		/// </summary>
		public static int Get12VCurrent()
		{
			ushort current = 0;

			unsafe
			{
				Get12vCurrent( &current );
			}

			return current;
		}

		/// <summary>
		/// Get the core voltage.
		/// </summary>
		public static int GetCoreVoltage()
		{
			ushort voltage = 0;

			unsafe
			{
				GetCoreVoltage( &voltage );
			}

			return voltage;
		}

        /// <summary>
        /// Determine if the docking station's diffusion lid is down lowered.
        /// </summary>
        /// <returns></returns>
        public static bool IsDiffusionLidDown()
        {
            if ( Configuration.DockingStation.Type != DeviceType.MX6 && Configuration.DockingStation.Type != DeviceType.MX4 )
                return IsDocked();

            //INS-7008: If MX4 dock has new Ventis cradle, treat that lid is down even though it does not have lid.
            if (Configuration.DockingStation.HasNewVentisCradle)
                return IsDocked();

            ushort data = 0;

            unsafe
            {
                GetPhotocell( &data );  // photocell register gives us the lid state for MX6.
            }

            // driver will return 0 if the lid is down, 1 if it is up.
            bool isDown = ( data == 0 ) ? true : false;

            return isDown;
        }

        /// <summary>
        /// For MX4 (with old cradle) and MX6 docking stations, this routine moves the solenoid inside
        /// the cradle to either route gas either to the diffusion lid, or to the hose
        /// attached to the instrument pump / pump adapter.
        /// </summary>
        /// <param name="accessoryPump">
        /// If AccessoryPumpSetting.Installed, then gas is routed to the hose
        /// that's attaced to the pump. Otherwise, it's routed to the lid.
        /// </param>
        public static void SetCradleSolenoid( AccessoryPumpSetting accessoryPump )
        {
            // It's assumed that, for Viper, if it's an MX4 docking station, then
            // it's a Ventis docking station, not an older DS2 iQuad docking station
            // (which doesn't have the cradle solenoid).

            if ( Configuration.DockingStation.Type != DeviceType.MX6
            &&   Configuration.DockingStation.Type != DeviceType.MX4 )
                return;

            //INS-7008: If MX4 dock has new Ventis cradle, skip continuing as new cradle doesn't have cradle solenoid.
            if (Configuration.DockingStation.HasNewVentisCradle)
                return;

            // LED #2 controls the solenoid that routes gasflow to either 
            // the diffusion lid or pump hose.  Switch it accordingly.

            Controller.SetInterfaceLed( (byte)2, accessoryPump == AccessoryPumpSetting.Installed ? (byte)1 : (byte)0 );
        }

        public static void PowerOnMX6( bool on )
        {
            if ( Configuration.DockingStation.Type != DeviceType.MX6 )
                return;

            // LED #1 controls the signal to the instrument that tells the 
            // instrument to turn itself on.
            Controller.SetInterfaceLed( (byte)1, on ? (byte)1 : (byte)0 );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool IsPumpAdapterAttached()
        {
            // It's assumed that, for Viper, if it's an MX4 docking station, then
            // it's a Ventis docking station, not an older DS2 iQuad docking station
            // (which doesn't accept a pump adapter).

            if ( Configuration.DockingStation.Type != DeviceType.MX6
            &&   Configuration.DockingStation.Type != DeviceType.MX4 )
                return false;

            // INS-7008: If Ventis MX4 dock has new cradle, skip verifying pump adapter as new cradle does not have pump adapter.
            if (Configuration.DockingStation.HasNewVentisCradle)
                return false;

            ushort data = 0;

            unsafe
            {
                GetLampDoor( &data );  // lamp door register gives us the pump adapter state for MX6.
            }

            // driver will return 1 if the adapter is attached, 0 if it is up.
            bool isAttached = ( data == 1 ) ? true : false;

            return isAttached;
        }

        public static bool IsFastIsDockedEnabled
        {
            get { return _fastIsDockedEnabled; }
            set { _fastIsDockedEnabled = value; }
        }

		/// <summary>
		/// Returns a value indicating whether the instrument is docked on the docking station.
		/// </summary>
		/// <returns>True if the instrument is docked, false otherwise</returns>
		public static bool IsDocked()
		{
            // By default (fastIsDockedEnabled is true), and we merely 
            // quickly return the instrument's current docked status.
            if ( IsFastIsDockedEnabled )
                return IsInstrumentDetected();

            // if FastIsDockedEnabled is false, then if instrument appears to be undocked, then 
            // we need to pause a moment, and then check again. This is to work around problem 
            // where, during a SafeCore module firmware upgrade, the module will sometimes 
            // momentarily appearing as undocked when its bootloader is invoked, or when the
            // module is rebooting. FastIsDockedEnabled is set to true by the instrument firmware
            // upgrade operation when the instrument type is safe core. That operation guarantees
            // that it will set it back to false before it finishes.

            if ( IsInstrumentDetected() )
                return true;

            // From some informal experiments I did, and the module typically appears to only 
            // be undocked for around 10ms or so. We'll pause 10X that amount as a precaution.
            // (Tim Belski has also indicate that it should be very quick.)
            Thread.Sleep(100);

            return IsInstrumentDetected();
		}

        /// <summary>
        /// Makes a P/Invoke call into the BSP to read the docked status GPIO.
        /// </summary>
        /// <returns></returns>
        private static bool IsInstrumentDetected()
        {
            byte detected;

            unsafe
            {
                GetInstrumentDetect( &detected );
            }

            return detected != 0;
        }

        /// <summary>
        /// Turns multiple LEDs on.
        /// </summary>
        /// <param name="position">
        /// Array of LEDStates representing the positions to be turned on.  All other LEDs will be turned off.
        /// </param>
        public static void TurnLEDsOn( List<LEDState> ledsOn )
        {
            StringBuilder logMsgBuilder = new StringBuilder();

            TurnLEDsOff();

            foreach ( LEDState led in ledsOn )
                SetLEDState( (byte)led, 1 );

            LogLEDs( ledsOn );
        }

        /// <summary>
        /// Turns a single LED on.  All others LEDs are turned off.
        /// </summary>
        /// <param name="position">
        /// The LED position to be switched on.
        /// </param>
        public static void TurnLEDOn( LEDState position )
        {
            TurnLEDsOff();

            SetLEDState( (byte)position, 1 );

            if ( Log.Level < LogLevel.Trace )
                return;

            Log.Trace( string.Format( "LED ON:  {0}", position.ToString() ) );
        }

        private static void LogLEDs( List<LEDState> ledList )
        {
            // try to prevent unnecessary string allocations since this method gets called alot.
            if ( Log.Level < LogLevel.Trace )
                return;

            StringBuilder logMsgBuilder = new StringBuilder();

            int ledCount = 0;
            foreach ( LEDState led in ledList )
            {
                if ( ledCount++ > 0 )
                    logMsgBuilder.Append( ", " );
                logMsgBuilder.Append( led.ToString() );
            }

            Log.Trace( string.Format( "LEDs ON: {0}", logMsgBuilder.ToString() ) );
        }

		/// <summary>
		/// Turns all the LED off.
		/// </summary>
		public static void TurnLEDsOff()
		{
            SetLEDState( (byte)LEDState.Green, 0 );
            SetLEDState( (byte)LEDState.Yellow, 0 );
            SetLEDState( (byte)LEDState.Red, 0 );
		}


		/// <summary>
		/// Turns the buzzer off.
		/// </summary>
		public static void TurnBuzzerOff() 
		{
		    SetBuzzerState( 0 );
		}

		/// <summary>
		/// Turns the buzzer on for a given number of seconds.
		/// </summary>
		/// <param name="seconds">Number of seconds the buzzer is turned on</param>
		public static void Buzz( double seconds ) 
		{
            // SGF  Feb-24-2009  DSW-136
            if ( Configuration.DockingStation.UseAudibleAlarm == true )
                SetBuzzerState(1);

			Thread.Sleep( (int)(seconds * 1000) );

		    SetBuzzerState( 0 );
		}

		/// <summary>
		/// Gets the key that was pressed.
		/// </summary>
		/// <returns>The key pressed</returns>
		public static KeyPress GetKeyPress()
		{
			bool[] keysData = new bool[ NUM_KEYS + 1 ];
			bool[] laterData = new bool[ NUM_KEYS + 1 ];
			bool keyPressed = false;
			byte state = 1; // 0 == pressed, 1 == not pressed
	
			// Check the initial state of the keys.
			for ( int i = 1 ; i <= NUM_KEYS ; i++ )
			{
				// Read the key.
				unsafe // is this necessary?
				{
					GetKeypadState( Convert.ToByte( i ) , &state );
				}

				keysData[ i ] = ( state == 0 );

                if ( state == 0 )
                {
                    keyPressed = true;

                    //Log.Debug( "keyPressed = " + keyPressed );
                }
			}

            if ( !keyPressed ) // If nothing was pressed, return none.
                return KeyPress.None;

            Thread.Sleep( 10 ); // Wait for a small pause.

			// Make certain one of the keys is still pressed.
			keyPressed = false;
			for ( int i = 1 ; i <= NUM_KEYS ; i++ )
			{
			    unsafe // is this necessary?
			    {
				    GetKeypadState( Convert.ToByte( i ) , &state );
			    }

				keysData[ i ] = ( state == 0 );

				if ( state == 0 )
					keyPressed = true;
			}

			// If nothing was pressed, return none.
			if ( ! keyPressed )
				return KeyPress.None;
            
            // Starting measuring how long the key is pressed.
            DateTime startTime = DateTime.UtcNow;

            Log.Trace( "KEYPRESS startTime = " + startTime );

            int numKeysPressed = GetNumKeysPressed( keysData );

			// Wait for completely releasing all keys.
			for ( int n = 0 ; n < 1000 ; n++ )
			{
				// Reset the key pressed variable.
				keyPressed = false;

                // If more than one key was originally pressed, then look multiple
                // times, since it make take a moment for the user to release all of them.
                for ( int tries = 1; tries <= numKeysPressed; tries++ )
                {
                    // Wait a moment for user to release the other keys before looking again.
                    // i.e., it's very rare that a user could release all keys simultaneously.
                    if ( tries > 1 )
                        Thread.Sleep( 50 );

                    for ( int i = 1; i <= NUM_KEYS; i++ )
                    {
                        // Read the key.
                        unsafe // Is this necessary?
                        {
                            GetKeypadState( Convert.ToByte( i ), &state );
                        }

                        laterData[i] = ( state == 0 );

                        if ( state == 0 )
                            keyPressed = true;
                    }
                }

				if ( ! keyPressed )
				{
                    // Special case - look for both right and left keys being 
                    // pressed at the same time.
                    if ( IsLeftRightKeysPressed( keysData ) )
                    {
                        DateTime now = DateTime.UtcNow;
                        Log.Trace( "KEYPRESS EndTime1 = " + now );
                        TimeSpan elapsed = now - startTime;
                        return new KeyPress( Key.RightLeft, elapsed );
                    }

					for ( int i = 1 ; i <= NUM_KEYS ; i++ )
					{
						if ( keysData[ i ] == true )
                        {
                            DateTime now = DateTime.UtcNow;
                            Log.Trace( "KEYPRESS EndTime2 = " + now );
                            TimeSpan elapsed = now - startTime;
                            return new KeyPress( (Key)i, elapsed );
                        }
					}
				}
				Thread.Sleep( 10 );
			}
            return KeyPress.None;
		}

        private static bool IsLeftRightKeysPressed( bool[] data )
        {
            return data[(int)Key.Left] && !data[(int)Key.Middle] && data[(int)Key.Right];
        }

        private static int GetNumKeysPressed( bool[] data )
        {
            int count = 0;

            for ( int i = 0; i < data.Length; i++ )
                if ( data[i] ) count++;

            return count;
        }

		public static void LogMemory()
		{
			MemoryStatus status = new MemoryStatus();
            unsafe { MemoryStatus.GlobalMemoryStatus( &status ); }

            Log.Info( "Memory Information..." );
            Log.Info( "Length:                   " + status.dwLength );
            Log.Info( "Memory Load:              " + status.dwMemoryLoad );
            Log.Info( "Total Physical Memory:    " + status.dwTotalPhysical );
            Log.Info( "Available Physical Memory:" + status.dwAvailablePhysical );
            Log.Info( "Total Page File:          " + status.dwTotalPageFile );
            Log.Info( "Available Page File:      " + status.dwAvailablePageFile );
            Log.Info( "Total Virtual Memory:     " + status.dwTotalVirtual );
            Log.Info( "Available Virtual Memory: " + status.dwAvailableVirtual );
            Log.Info( "NAND Flash ECC enabled:   " + IsNandFlashEccEnabled() );
		}

        public static void LogMemoryUsage()
        {
            MemoryStatus status = new MemoryStatus();
            unsafe { MemoryStatus.GlobalMemoryStatus( &status ); }

            float totalMem = (float)status.dwTotalPhysical / 1000000.0f; // convert to MB
            float availMem = (float)status.dwAvailablePhysical / 1000000.0f; // convert to MB
            float usedMem = totalMem - availMem;

            float div = availMem / totalMem;
            float percentAvail = div * 100.0f;
            
            Log.Debug( string.Format( "MEMORY USAGE..... Total: {0}MB, Used: {1}MB, Avail: {2}MB ({3}%%)",
                Math.Round( totalMem, 0 ), Math.Round( usedMem, 0 ), Math.Round( availMem, 0 ), Math.Round( percentAvail, 0 ) ) );
        }

        public static void LogProcessorInfo()
        {
            Processor proc = new Processor();
            Log.Info( "Processor Information..." );
            Log.Info( "Catalog Number:     " + proc.CatalogNumber );
            
            // Calculate base run speed for the CPU, in Mhz.
            int runSpeed = (int)proc.ClockSpeed / 1000000; 
            // Calculate the maximum (turbo) speed for the CPU.
            int turboSpeed = (int)( (double)runSpeed * proc.N_Multiplier );
            Log.Info( "Speed:              " + runSpeed + "Mhz" ); // This is "CPU speed" on the pxa255 processor but seems to be memory speed on the Atmel board.
            //Log.Info( "CPU Run Speed:      " + runSpeed + "Mhz" );
            //Log.Info( "CPU Turbo Speed:    " + turboSpeed + "Mhz" + ( proc.TurboEnabled ? " (Enabled)" : " (Disabled)" ) );
            //Log.Info( "L/M/N Multipliers:  " + proc.L_Multiplier + "/" + proc.M_Multiplier + "/" + proc.N_Multiplier );
            Log.Info( "Core Revision:      " + proc.CoreRevision );
            Log.Info( "Instruction Set:    " + proc.InstructionSet );
            Log.Info( "Processor Core:     " + proc.Core );
            Log.Info( "Processor Name:     " + proc.Name );
            Log.Info( "Processor Revision: " + proc.Revision );
            Log.Info( "Vendor:             " + proc.Vendor );
        }

		/// <summary>
		/// Prints out the information returned by GetSystemInfo();
		/// </summary>
		public static void LogSystemInfo()
		{
			SystemInfo info = new SystemInfo();
            unsafe {  SystemInfo.GetSystemInfo( &info ); }

            Log.Info( "System Information..." );
            Log.Info( "System Date/Time:            " + Log.DateTimeToString( DateTime.UtcNow ) );
            Log.Info( "Operating System:            " + System.Environment.OSVersion.ToString() );
            // CompactFramework versions are as follows...
            // 1.0.2268.0  = RTM
            // 1.0.3111.0  = SP1
            // 1.0.3226.0  = SP2 Recall
            // 1.0.3227.0  = SP2 Beta
            // 1.0.3316.0  = SP2 Final   <---- DS2 v3.0
            // 1.0.4177.0  = SP3 Beta
            // 1.0.4292.0  = SP3         <---- DS2 v4.0
            // 2.0.4037.0  = (part of VS2005 CTP May)
            // 2.0.4135.0  = (part of VS2005 Beta 1)
            // 2.0.4317.0  = (part of VS2005 CTP November)
            // 2.0.4278.0  = (part of VS2005 CTP December - yes it is older than November)
            // 2.0.5238.0  = RTM
            // 2.0.6129.0  = SP1
            // 2.0.7045.0  = SP2 (available to CE 4.2 in QFE, March 2007)
            // 3.5.7283.0  = 2008 January 25
            // 3.5.9198.0  = 2009 July 20
            // 3.5.10010.0 = Unknown (this actually might be what .7283 reports itself as?)
            Log.Info( ".NET Compact Framework:      " + System.Environment.Version.ToString() );
            Log.Info( "Firmware:                    " + FirmwareVersion );
#if DEBUG
            Log.Info( "Build Configuration:         Debug" );
#elif INET_QA
            Log.Info( "Build Configuration:         QA" );
#else
            Log.Info( "Build Configuration:         Release" );
#endif
            //Log.Info( "Instrument Driver:           " + InstrumentDriver.DriverVersion );
            Log.Info( "Processor Architecture:      " + info.wProcessorArchitecture );
            Log.Info( "Reserved:                    " + info.wReserved );
            Log.Info( "Page Size:                   " + info.dwPageSize );
            Log.Info( "Minimum Application Address: " + info.lpMinimumApplicationAddress );
            Log.Info( "Maximum Application Address: " + info.lpMinimumApplicationAddress );
            Log.Info( "Active Processor Mask:       " + info.dwActiveProcessorMask );
            Log.Info( "Number of Processors:        " + info.dwNumberOfProcessors );
            Log.Info( "Processor Type:              " + info.dwProcessorType );
            Log.Info( "Allocation Granularity:      " + info.dwAllocationGranularity );
            Log.Info( "Processor Level:             " + info.wProcessorLevel );
			Log.Info( "Processor Revision:          " + info.wProcessorRevision );
		}

        /// <summary>
        /// </summary>
        public static string FirmwareVersion
        {
            get
            {
               return _firmwareVersion;
            }
            set
            {
                _firmwareVersion = value;
            }
        }

		#endregion

	}  // end-class Controller

    /// <summary>
    /// This struct is returned by Controller.GetKeyPress.  It returns
    /// information about the key(s) that were pressed.
    /// </summary>
    public struct KeyPress
    {
        /// <summary>
        /// The key that has been pressed.
        /// </summary>
        public Controller.Key Key;

        /// <summary>
        /// The amount of time the key was pressed.  This is probably only accurate 
        /// to within a couple hundred milliseconds.
        /// </summary>
        public TimeSpan Length;

        // This static instance is returned by GetKeyPress when it's determined
        // that no key has been pressed.  Because GetKeyPress can be called every few 
        // milliseconds when idle, this static instance is returned instead of continually
        // allocating a new instance everytime GetKeyPress is called and no key has been pressed.
        public static KeyPress None = new KeyPress( Controller.Key.None, TimeSpan.Zero );

        public KeyPress( Controller.Key key, TimeSpan length )
        {
            Key = key;
            Length = length;
        }

        /// <summary>
        /// Returns a string containing the key that was pressed.  e.g. "Right".
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Key.ToString();
        }
    }

    public enum HardwareConfigErrorType
    {
        LidError,
        FlipperAndLidError,
        Unknown
    }

    public class HardwareConfigurationException : Exception
    {
        public HardwareConfigErrorType ErrorType { get; set; }

        public HardwareConfigurationException() : base("DOCKING STATION IS NOT CONFIGURED PROPERLY.")
        {
            ErrorType = HardwareConfigErrorType.Unknown;
        }

        public HardwareConfigurationException(string message) : base(message)
        {
            ErrorType = HardwareConfigErrorType.Unknown;
        }

        public HardwareConfigurationException(HardwareConfigErrorType errorType) : base("DOCKING STATION IS NOT CONFIGURED PROPERLY.")
        {
            ErrorType = errorType;
        }

        public HardwareConfigurationException(HardwareConfigErrorType errorType, string message) : base(message)
        {
            ErrorType = errorType;
        }
    }

	public enum DeviceHardware
	{
		None,
		SmartCardPresence,
		PressureSwitchPresence,
		PressureSwitchState,
		Flow
	}

    public class DeviceDriverException : Exception
    {
        private DeviceHardware _deviceHardware;
        private int _lastErrorResult;

        /// <summary>
        /// String describing what hardware was trying to be read.
        /// </summary>
        public DeviceHardware DeviceHardware { get { return _deviceHardware; } }

        /// <summary>
        /// Result of OS's "GetLastError()" function at time of error.
        /// </summary>
        public int LastErrorResult { get { return _lastErrorResult; } }

        /// <summary>
        /// </summary>
		/// <param name="deviceHardware">What hardware was trying to be read.</param>
        /// <param name="lastErrorResult">Result of OS's "GetLastError()" function at time of error.</param>
        internal DeviceDriverException( DeviceHardware deviceHardware, int lastErrorResult )
            : base( string.Format( "{0} device driver failure, GetLastError={1}", deviceHardware, lastErrorResult ) )
        {
            _deviceHardware = deviceHardware;
            _lastErrorResult = lastErrorResult;
        }

		/// <summary>
		/// </summary>
		/// <param name="deviceHardware">What hardware was trying to be read.</param>
		/// <param name="position">The position (of a port) that was being accessed when the error was encountered.</param>
		/// <param name="lastErrorResult">Result of OS's "GetLastError()" function at time of error.</param>
		internal DeviceDriverException( DeviceHardware deviceHardware, int position, int lastErrorResult )
			: base( string.Format( "{0}({1}) device driver failure, GetLastError={2}", deviceHardware, position, lastErrorResult ) )
		{
			_deviceHardware = deviceHardware;
			_lastErrorResult = lastErrorResult;
		}
    }
}
