using System;
using System.Runtime.InteropServices;
using ISC.WinCE.Logger;
using ISC.WinCE;


namespace ISC.iNet.DS
{
	/// <summary>
	/// This class is a wrapper around Window CE's software watchdog API.
	/// </summary>
	/// <remarks>
	/// Typical and most simple usage...
	/// <code>
	/// WatchDog watchDog = new WatchDog( "My watchdog", 60000, false );
	/// watchDog.Start();
	/// 
	/// while ( true )
	/// {
	///     // do stuff here.
    ///     
	///     watchDog.Refresh(); // "pet" the watchdog.
	/// }
	/// </code>
	/// </remarks>
	public class WatchDog
	{
		private IntPtr _handle = IntPtr.Zero; // returned by CreateWatchDogTimer.
		private int _lastError;

		// Win errors seen during development 
		private const int ERROR_FILE_NOT_FOUND = 2;
		private const int ERROR_INVALID_HANDLE = 6;
		private const int ERROR_INVALID_PARAMETER = 87;
		private const int ERROR_ALREADY_EXISTS = 183;
		private const int ERROR_ALREADY_INITIALIZED = 1247;

		// we hold onto the name and period that are passed-in to the constructor, so we can view them in the debugger.
		private string _name;
		private int _period; // interval in ms for watchdog
		private int _periodSeconds; // interval in seconds for the start log message
		private bool _logSuccessMsg;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="watchDogName">
		/// Name of the watchdog timer to be created. Every watchdog must have a unique name.
		/// </param>
		/// <param name="period">
		/// Watchdog period, in milliseconds. If the watchdog is not refreshed
		/// after 'period' milliseconds is elapsed,then the watchdog will initate a reboot.
		/// </param>
		/// <see cref="https://msdn.microsoft.com/en-US/library/ee482966(v=winembedded.60).aspx"/>
		public WatchDog( string name, int period, bool logSuccessMsg )
		{
			Log.Assert( string.IsNullOrEmpty( name ) == false, "WatchDog 'name' must be specified." );
			Log.Assert( period > 0, "WatchDog 'period' must be specified." );

			_name = name;
			_period = period;
			_periodSeconds = period / 1000;
			_logSuccessMsg = logSuccessMsg;

// We can't let the watchdog run when using Visual Studio debugger, 
// because the watchdog might reboot the device when we stop at a breakpoint.
#if DEBUG
            Log.Debug( string.Format( "WATCHDOG: \"{0}\" not created because this is a Debug build.", name ) );
#else
			// A handle to the watchdog timer indicates success.
			_handle = WinCeApi.CreateWatchDogTimer( name, period, 0, WinCeApi.WDOG_RESET_DEVICE, 0, 0 );
			_lastError = Marshal.GetLastWin32Error();

			if ( _handle == IntPtr.Zero )
				Log.Error( string.Format( "WATCHDOG: Failed to create watchdog \"{0}\", GetLastWin32Error={1}.", _name, _lastError ) );
			else if ( _lastError == ERROR_ALREADY_EXISTS ) // a new handle will be returned even if a watchdog with the same name exists
				Log.Error( string.Format( "WATCHDOG: Failed to create new watchdog as \"{0}\" already exists, GetLastWin32Error={1}.", _name, _lastError ) );
			else if ( _logSuccessMsg ) // error code of 0 is expected
				Log.Debug( string.Format( "WATCHDOG: Success creating watchdog \"{0}\", GetLastWin32Error={1}.", _name, _lastError ) );
#endif
		}

		/// <summary>
		/// This function starts the watchdog. This method must be called after instantiating a watchdog
		/// in order for the watchdog to actually begin, umm, "watching". If the watchdog is paused by calling
		/// the Stop() method, then it may be resumed by calling this Start() method.
		/// </summary>
		/// <see cref="https://msdn.microsoft.com/en-us/library/ee482881(v=winembedded.60).aspx"/>
		public void Start()
		{
			if ( _handle == IntPtr.Zero )
				return;

			// TRUE indicates success.
			if ( !WinCeApi.StartWatchDogTimer( _handle, 0 ) )
			{ // Failed
				_lastError = Marshal.GetLastWin32Error(); // only seems reliable if StartWatchDogTimer returns FALSE

				if ( _lastError == ERROR_INVALID_HANDLE )
					Log.Error( string.Format( "WATCHDOG: Failed to start \"{0}\" watchdog due to invalid handle, GetLastWin32Error={1}.", _name, _lastError ) );
				else if ( _lastError == ERROR_ALREADY_INITIALIZED )
					Log.Error( string.Format( "WATCHDOG: Failed to start \"{0}\" watchdog as it was already initialized, GetLastWin32Error={1}.", _name, _lastError ) );
				else
					Log.Error( string.Format( "WATCHDOG: Failed to start \"{0}\" watchdog, GetLastWin32Error={1}.", _name, _lastError ) );
			}
			else if ( _logSuccessMsg )
			{ // Success
				Log.Debug( string.Format( "WATCHDOG: Success starting watchdog \"{0}\" with a period of {1} seconds.", _name, _periodSeconds ) );
			}			
		}

		/// <summary>
		/// This function stops (pauses) the watchdog.
		/// </summary>
		/// <see cref="https://msdn.microsoft.com/en-us/library/ee482847(v=winembedded.60).aspx"/>
		public void Stop()
		{
			if ( _handle == IntPtr.Zero )
				return;

			// Returns TRUE if the function succeeds.
			if ( !WinCeApi.StopWatchDogTimer( _handle, 0 ) )
			{ // Failed
				_lastError = Marshal.GetLastWin32Error(); // only seems reliable if StopWatchDogTimer returns FALSE
				Log.Error( string.Format( "WATCHDOG: Failed to stop watchdog \"{0}\", GetLastWin32Error={1}.", _name, _lastError ) );
			}
			else if ( _logSuccessMsg )
			{ // Success
				Log.Debug( string.Format( "WATCHDOG: Success stopping watchdog \"{0}\".", _name ) );
			}
		}

		/// <summary>
		/// This function releases the memory used by the watchdog timer.
		/// </summary>
		/// <see cref="https://msdn.microsoft.com/en-us/library/ee490442(v=winembedded.60).aspx"/>
		public void Close()
		{
			if ( _handle == IntPtr.Zero )
				return;

			// Nonzero indicates success. Zero indicates failure.
			int result = WinCeApi.CloseHandle( _handle );
			
			if ( result == 0 )
			{ // Failed
				_lastError = Marshal.GetLastWin32Error(); // only seems reliable if CloseHandle returns 0
				Log.Error( string.Format( "WATCHDOG: Failed to close watchdog \"{0}\", GetLastWin32Error={1}.", _name, _lastError ) );
			}
			else if ( _logSuccessMsg )
			{ // Success
				Log.Debug( string.Format( "WATCHDOG: Success({1}) closing watchdog \"{0}\".", _name, result ) );
			}
		}

		/// <summary>
		/// This function refreshes ("pets") the watchdog.
		/// </summary>
		public void Refresh()
		{
			if ( _handle == IntPtr.Zero )
				return;

			if ( !WinCeApi.RefreshWatchDogTimer( _handle, 0 ) )
				Log.Error( string.Format( "WATCHDOG: Failed to refresh watchdog \"{0}\", GetLastWin32Error={1}.", _name, Marshal.GetLastWin32Error() ) );
		}
	}
}
