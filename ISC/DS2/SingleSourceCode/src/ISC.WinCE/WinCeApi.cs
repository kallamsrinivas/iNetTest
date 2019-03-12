using System;
using System.Runtime.InteropServices;


namespace ISC.WinCE
{
    // class used to hold various standard WinCE api calls.
    public class WinCeApi
    {
        private WinCeApi() { } // private so we can only access static methods.

        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr( -1 );

        // States for SetSystemPowerState
        public const int POWER_STATE_ON = 0x00010000;
        public const int POWER_STATE_OFF = 0x00020000;
        public const int POWER_STATE_SUSPEND = 0x00200000;
        
        public const int POWER_STATE_RESET = 0x00800000;
        // public const int POWER_STATE_BOOT = ?
        // public const int POWER_STATE_CRITICAL = ?
        // public const int POWER_STATE_IDLE = ?
        // public const int POWER_STATE_PASSWORD = ?
        public const int POWER_FORCE = 4096; // Used as 'options' argument for SetSystemPowerState

        /// <summary>
        /// sets the system power state to the requested value.
        /// </summary>
        /// <param name="psState">Names the desired system state to enter.
        /// If this parameter is not NULL, the StateFlags parameter is ignored.
        /// </param>
        /// <param name="StateFlags">Optional. If the psState parameter is NULL, it names the
        /// system power state using the POWER_STATE_XXX flags defined in the Pm.h header file.
        /// </param>
        /// <param name="Options">
        /// Uses the optional POWER_FORCE flag to indicate that the state transfer is urgent.
        /// The interpretation of this flag is platform-dependent.
        /// </param>
        /// <returns></returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern int SetSystemPowerState( string psState, int StateFlags, int Options );


        /// <summary>
        /// Prints a message to the debug port
        /// </summary>
        [DllImport( "coredll.dll" )]
        public static extern void NKDbgPrintfW( string msg );

        [DllImport( "coredll" )]
        public static extern IntPtr LocalAlloc( int flags, int cb );
        [DllImport( "coredll" )]
        public static extern IntPtr LocalFree( IntPtr p );

        /// <summary>
        /// This function provides the kernel with a generic I/O control for carrying out I/O operations.
        /// </summary>
        /// <param name="IoControlCode"></param>
        /// <param name="InputBuffer"></param>
        /// <param name="InputBufferSize"></param>
        /// <param name="OutputBuffer"></param>
        /// <param name="OutputBufferSize"></param>
        /// <param name="BytesReturned"></param>
        /// <returns>1 (true) indicates success. 0 (false) indicates failure.</returns>
        [DllImport( "coredll.dll" )]
        public static extern int KernelIoControl( Int32 IoControlCode,
                                                    IntPtr InputBuffer,
                                                    Int32 InputBufferSize,
                                                    byte[] OutputBuffer,
                                                    Int32 OutputBufferSize,
                                                    ref Int32 BytesReturned );
        //extern static int KernelIoControl(int dwIoControlCode, IntPtr lpInBuf, int nInBufSize, IntPtr lpOutBuf, int nOutBufSize , ref int lpBytesReturned );

        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern IntPtr CreateFile( String lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes,
            IntPtr hTemplateFile );

        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern bool DeviceIoControl( IntPtr hDevice, int dwIoControlCode, byte[] lpInBuffer,
            int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize, out int lpBytesReturned,
            IntPtr lpOverlapped );


        #region DeviceIoControl Related Constants

        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;

        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;

        public const uint FILE_DEVICE_UNKNOWN = 0x00000022;

        public const uint OPEN_EXISTING = 3;

        public const uint METHOD_BUFFERED = 0;
        public const uint METHOD_IN_DIRECT = 1;
        public const uint METHOD_OUT_DIRECT = 2;
        public const uint METHOD_NEITHER = 3;

        public const uint FILE_ANY_ACCESS = 0;
        public const uint FILE_READ_ACCESS = ( 0x0001 );    // file & pipe
        public const uint FILE_WRITE_ACCESS = ( 0x0002 );    // file & pipe

        #endregion

        /// <summary>
        /// This is a common window function used to shift the parameters into what is called a control code. 
        /// The control code is passed as an argument to DeviceIoControl.
        /// </summary>
        /// <param name="DeviceType">The type of the device</param>
        /// <param name="Function">The function to invoke on the device</param>
        /// <param name="Method">Indicates how data will be passed to the driver. ( buffered, direct, etc.)</param>
        /// <param name="Access">The required access level (permissions)</param>
        /// <returns></returns>
        public static uint CTL_CODE( uint DeviceType, uint Function, uint Method, uint Access )
        {
            return ( ( DeviceType ) << 16 ) | ( ( Access ) << 14 ) | ( ( Function ) << 2 ) | ( Method );
        }

        /// <summary>
        /// Calls a device driver
        /// </summary>
        /// <param name="hDevice">A handle to the device</param>
        /// <param name="dwIoControlCode">The control code to be executed</param>
        /// <param name="inBuffer">The input to the device</param>
        /// <param name="nInBufferSize">The size of the input buffer</param>
        /// <param name="lpOutBuffer">The output</param>
        /// <param name="nOutBufferSize">The size of the output</param>
        /// <param name="lpBytesReturned">???</param>
        /// <param name="lpOverlapped">???</param>
        /// <returns></returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern bool DeviceIoControl( IntPtr hDevice, uint dwIoControlCode,
            [MarshalAs( UnmanagedType.AsAny )] object inBuffer,
            int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize, out int lpBytesReturned,
            IntPtr lpOverlapped );



        /// <summary>
        /// Activates a device driver
        /// </summary>
        /// <param name="lpszDevKey">The string that identifies the device</param>
        /// <param name="clientInfo">???</param>
        /// <param name="regEntryCnt">???</param>
        /// <param name="dvcParams">???</param>
        /// <returns></returns>
        [DllImport( "CoreDll.dll" )]
        public static extern IntPtr ActivateDeviceEx( String lpszDevKey, IntPtr clientInfo, UInt32 regEntryCnt, IntPtr dvcParams );

        /// <summary>
        /// Gets the error code of the last error
        /// </summary>
        /// <returns>The numeric error code.</returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern Int32 GetLastError();

        /// <summary>
        /// This function deletes a file from a file system
        /// </summary>
        /// <param name="filename"></param>
        /// <remarks>
        /// If the file does not exist, this function fails.
        /// This function fails if an application attempts to delete a file that is open for normal I/O or as a memory-mapped file.
        /// </remarks>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().
        /// </returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern int DeleteFile( string filename );

        /// <summary>
        /// This function closes an open object handle
        /// </summary>
        /// <param name="hObject"></param>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern int CloseHandle( IntPtr hObject /* handle to object */ );

        /// <summary>
        /// Calling this during a device reset, the OS ignores the contents of the
        /// object store and replaces the current data with the default data
        /// found in the .bin file. This information is the same as when a
        /// device is forced into a cold reset.
        /// 
        /// Essentially, this should force the registry and its current settings, etc.
        /// to be tossed away and replace with a fresh default registry.
        /// </summary>
        [DllImport( "coredll.dll" )]
        public extern static void SetCleanRebootFlag();

        /// <summary>
        /// Signals that an application has started and initialization is complete.
        /// Necessary for use with HKEY_LOCAL_MACHINE\Init keys ("Launchxx", "Dependxx", etc.)
        /// </summary>
        /// <param name="sequenceID">Specifies the sequence in which the shell is started by the system. 
        ///</param>
        [DllImport( "coredll.dll" )]
        public static extern void SignalStarted( uint sequenceID );

        /// <summary>
        /// Clears the buffers for the specified communications resource and causes all buffered data to be written to the port.
        /// </summary>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern int FlushFileBuffers( IntPtr hFile );

        /// <summary>
        /// Discards all characters from the output or input buffer of a specified communications resource. 
        /// </summary>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern int PurgeComm( IntPtr hFile, int dwFlags );

        /// <summary>
        /// This function reads data from a file, starting at the position indicated by the file pointer.
        /// After the read operation has been completed, the file pointer is adjusted by the number of bytes read.
        /// </summary>
        /// <param name="hFile"></param>
        /// <param name="lpBuffer"></param>
        /// <param name="nNumberOfBytesToRead"></param>
        /// <param name="nNumberOfReadBytes"></param>
        /// <param name="lpOverlapped"></param>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true ) ]
        public static unsafe extern int ReadFile( IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, uint* nNumberOfReadBytes, Overlapped* lpOverlapped );

        /// <summary>
        /// Writes data to the file handle
        /// </summary>
        /// </summary>
        /// <param name="hFile"></param>
        /// <param name="lpBuffer"></param>
        /// <param name="nNumberOfBytesToWrite"></param>
        /// <param name="lpNumberOfBytesWritten"></param>
        /// <param name="lpOverlapped"></param>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true ) ]
        public static unsafe extern int WriteFile( IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, uint* lpNumberOfBytesWritten, Overlapped* lpOverlapped );

        /// <summary>
        /// Configures a serial port according to specifications in the Device Control Block (DCB structure).
        /// </summary>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern int SetCommState( IntPtr hFile, ref DCB dcp );

        /// <summary>
        /// Retrieves the current control settings for a specified communication device.
        /// </summary>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static extern int GetCommState( IntPtr hFile, ref DCB dcp );

        /// <summary>
        /// Creates or opens a named or unnamed event object.
        /// </summary>
        [DllImport( "coredll.dll" )]
        public static extern IntPtr CreateEvent( int eventAttributes, bool manualReset, int initialState, char[] name );

        /// <summary>
        /// Sets the time-out parameters for all read and write operations on a specified communications device. 
        /// </summary>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        public static unsafe extern int SetCommTimeouts( IntPtr hFile, CommTimeOuts* lpCommTimeouts );

		#region WatchDogTimer

		/***********************************************************************************/
		/*                                                                                 */
		/*  WinCE's kernel watchdog code can be viewed if you installed PlatformBuilder's  */
		/*  shared source code. (C:\WINCE600\PRIVATE\WINCEOS\COREOS\NK\KERNEL\watchdog.c)  */
		/*                                                                                 */
		/***********************************************************************************/

		/// <summary>
		/// Tells the watchdog created by CreateWatchDogTimer() to take no default action.
		/// </summary>
		public const int WDOG_NO_DFLT_ACTION = 0;
		/// <summary>
		/// Tells the watchdog created by CreateWatchDogTimer() to terminate the process.
		/// </summary>
		public const int WDOG_KILL_PROCESS = 1;
		/// <summary>
		/// Tells the watchdog created by CreateWatchDogTimer() to reset the device .
		/// </summary>
		public const int WDOG_RESET_DEVICE = 2;

		/// <summary>
		/// This function creates a watchdog timer.
		/// </summary>
		/// <param name="watchDogName">Name of the watchdog timer to be created.</param>
		/// <param name="period">Watchdog period, in milliseconds.</param>
		/// <param name="wait">Time to wait, in milliseconds, when the watchdog timer
		/// is not refreshed within the watchdog period before the default action is taken.
		/// This is useful when handling false alarms or saving important information
		/// before system reset.
		/// </param>
		/// <param name="dfltAction">Default action to be taken when the watchdog timer is not refreshed within dwPeriod.
		/// WDOG_KILL_PROCESS or WDOG_NO_DFLT_ACTION or WDOG_RESET_DEVICE.
		/// </param>
		/// <param name="param">Parameter to be passed to IOCTL_HAL_REBOOT.
		/// The OEM adaptation layer (OAL) uses this information to determine whether an immediate system reboot is needed,
		/// or to perform a delayed system reboot.
		/// </param>
		/// <param name="flags">Reserved; must be set to zero.</param>
		/// <returns>A handle to the watchdog timer indicates success. IntPtr.Zero indicates failure.</returns>
		[DllImport( "coredll.dll", SetLastError = true )]
		public static extern IntPtr CreateWatchDogTimer( string watchDogName, int period, int wait, int dfltAction, int param, int flags );

		/// <summary>
		/// This function starts a watchdog timer.
		/// </summary>
		/// <param name="watchDog">Handle to the watchdog timer to start.</param>
		/// <param name="flags">Reserved; must be set to zero.</param>
		/// <returns>
		/// True indicates success. ERROR_INVALID_HANDLE indicates that an invalid handle to the watchdog timer is received.
		/// Otherwise, False is returned. Call GetLastError to get extended error information.
		/// </returns>
		[DllImport( "coredll.dll", SetLastError = true )]
		public static extern Boolean StartWatchDogTimer( IntPtr watchDogHandle, int flags );

		/// <summary>
		/// This function opens an existing watchdog timer.
		/// </summary>
		/// <param name="watchDogName">Name of the watchdog timer to open.</param>
		/// <param name="flags">Reserved; must be set to zero.</param>
		/// <returns>A handle to the watchdog timer indicates success. NULL indicates failure.</returns>
		[DllImport( "coredll.dll", SetLastError = true )]
		public static extern IntPtr OpenWatchDogTimer( string watchDogName, int flags );
		
		/// <summary>
		/// This function stops a watchdog timer.
		/// </summary>
		/// <param name="watchDog">Handle to the watchdog timer to stop.</param>
		/// <param name="flags">Reserved; must be set to zero.</param>
		/// <returns>
		/// Returns True if the function succeeds. ERROR_INVALID_HANDLE indicates
		/// that the handle to the watchdog timer was invalid.
		/// Otherwise, False is returned. Call GetLastError to get extended error information.
		/// </returns>
		[DllImport( "coredll.dll", SetLastError = true )]
		public static extern Boolean StopWatchDogTimer( IntPtr watchDogHandle, int flags );

		/// <summary>
		/// This function refreshes a watchdog timer.
		/// </summary>
		/// <param name="watchDog">Handle to the watchdog timer to refresh.</param>
		/// <param name="flags">Reserved; must be set to zero.</param>
		/// <returns>
		/// True indicates success. Returns ERROR_INVALID_HANDLE if an invalid handle to the watchdog timer is received.
		/// Otherwise, False is returned. Call GetLastError to get extended error information.
		/// </returns>
		[DllImport( "coredll.dll", SetLastError = true )]
		public static extern Boolean RefreshWatchDogTimer( IntPtr watchDogHandle, int flags );

		#endregion WatchDogTimer
	}
}
