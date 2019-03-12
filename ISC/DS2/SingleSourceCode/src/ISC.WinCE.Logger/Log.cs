using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.IO;
using ISC.WinCE;


namespace ISC.WinCE.Logger
{
    
    public class Log : BaseLogging
    {
        private const string DATETIME_FORMAT = "MM/dd/yy HH:mm:ss.fff";

		public const int DEFAULT_CAPACITY = 10000;  // factory default is max of 10K messages.
		// an initial capacity higher than the 10k default capacity was needed so messages from log files 
		// could be loaded and not truncated before the actual capacity was loaded and applied
		public const int INITIAL_CAPACITY = 20000;   
        public const LogLevel DEFAULT_LEVEL = LogLevel.Debug; // factory default.

		static private int _capacity = INITIAL_CAPACITY;
        static private LogLevel _logLevel = DEFAULT_LEVEL;
		static private bool _loggingEnabled = true; // enabled when starting up
		
		// log to file feature
		static private bool _logToFile = false;
		static private int _fileLineCount = 0;
		private const int FILE_MAX_LINES = 10000; // controls how many messages can be written to FILE_LOG_CURRENT
		private const string FILE_LOG_ENABLED = "\\Reliance_flash\\enable.log";
		private const string FILE_LOG_CURRENT = "\\Reliance_flash\\current.log";
		private const string FILE_LOG_OLD = "\\Reliance_flash\\old.log";		

        static private DateTime _lastWriteTime = DateTime.MinValue;
        static private string _lastWriteTimeFormatted = string.Empty;
        //static private readonly object _writeLock = new object();  // Used to synchronize calls to Write()

        // Used ONLY by Write(string) and WriteError methods.  We keep using the
        // same string builder which is kept at a level of around WRITE_STRING_BUILDER_SIZE chars to 
        // to save on memory allocations.
        private const int WRITE_STRING_BUILDER_SIZE = 300;
        static private StringBuilder _writeStringBuilder = new StringBuilder( WRITE_STRING_BUILDER_SIZE );

        static private Queue<string> _queue = new Queue<string>();

		private static object _nandFlashLock = new object();

		/// <summary>
		/// Used externally when saving firmware to the nand flash to prevent the LogToFile feature from causing corruption.
		/// </summary>
		public static object NandFlashLock { get { return _nandFlashLock; } }

		/// <summary>
		/// Static constructor initializes state of the log to file feature.
		/// </summary>
		static Log()
		{
			if ( File.Exists( FILE_LOG_ENABLED ) )
			{
				Log.LogToFile = true;
			}
			else
			{
				Log.LogToFile = false;
			}
		}

        private Log() {}  // we allow static access only

        // IDS can currently only connect using InfrastructureMode and ONLY InfrastructureMode!!!
        static public string Dashes
        {
            get
            {
                return "----------------------------------------------------------------";
            }
        }

        /// <summary>
        /// Maximum number of debug log messages
        /// </summary>
        static public int Capacity
        {
            get { return _capacity; }
            set { _capacity = value; }
        }

        static public LogLevel Level
        {
            get { return _logLevel; }
            set
            {
                _logLevel = value;
                Info( "Log.LogLevel set to " + value.ToString() ); // Regardless of the log level, always log whenever the level changes.
            }
        }

		static public bool LogToSerialPort
		{
			get
			{
				return _loggingEnabled;
			}
			set
			{
				if ( _loggingEnabled != value && value == false )
				{
					// only log below message when setting is changing from enabled to disabled
					Write( "LOG: LOGGING TO SERIAL PORT DISABLED" );
				}

				_loggingEnabled = value;
			}
		}

		/// <summary>
		/// Changing the state of LogToFile will create or delete files related to the feature.  
		/// </summary>
		static public bool LogToFile
		{
			get
			{
				return _logToFile;
			}
			set
			{	
				// don't get the lock if we don't need it
				if ( _logToFile == value )
					return;

				lock ( _queue )
				{
					// verifying they are still different now that we have the lock
					if ( _logToFile != value )
					{
						if ( value )
						{
							StartFileLogging();

							// changing feature state last so messages aren't added to the message queue twice
							_logToFile = true;
							Debug( "LOG: LOGGING TO FILE IS ENABLED" );
						}
						else
						{
							// updating feature state first so we don't write to files that are being deleted
							_logToFile = false;
							Debug( "LOG: LOGGING TO FILE IS DISABLED" );

							StopFileLogging( true );
						}
					}
				}				
			}
		}

		/// <summary>
		/// This method allows for log messages to be purged without changing 
		/// whether or not the log to file feature is enabled.  Also, the LogToFile 
		/// setter property will not attempt to delete log files if the feature is 
		/// currently disabled.
		/// </summary>
		static public void PurgeLogFiles( bool disableFeature )
		{
			lock ( _queue )
			{				
				// if we are in the middle of resetting to factory defaults,
				// we don't want to log any more to the file system;
				// also see INETQA-3528 
				if ( disableFeature )
					_logToFile = false;

				Debug( "LOG: PURGING MESSAGES IN FILES" );
				StopFileLogging( disableFeature );				
			}
		}

		/// <summary>
		/// Deletes all files related to the log to file feature.  
		/// This method assumes the caller has obtained the proper lock.
		/// </summary>
		static private void StopFileLogging( bool purgeEnabled )
		{
			lock ( Log.NandFlashLock )
			{
				try
				{
					// delete the empty file whose presence indicates file logging is enabled
					if ( purgeEnabled && File.Exists( FILE_LOG_ENABLED ) )
						File.Delete( FILE_LOG_ENABLED );

					// delete the file containing the older log messages
					if ( File.Exists( FILE_LOG_OLD ) )
						File.Delete( FILE_LOG_OLD );

					// delete the file containing the most recent log messages
					if ( File.Exists( FILE_LOG_CURRENT ) )
						File.Delete( FILE_LOG_CURRENT );

					// reset line count
					_fileLineCount = 0;

					Debug( "LOG: MESSAGES IN FILES PURGED" );
				}
				catch ( Exception ex )
				{
					WriteFileError( "LOG: UNABLE TO PURGE MESSAGES IN FILES", ex, true );
				}
			}
		}

		/// <summary>
		/// Creates the FILE_LOG_ENABLED file if needed, and loads any messages from pre-existing log files
		/// into the in-memory message queue.  This method assumes the caller has obtained the proper lock.
		/// </summary>
		static private void StartFileLogging()
		{
			lock ( Log.NandFlashLock )
			{
				try
				{
					// if needed, create the empty file whose presence indicates file logging is enabled,
					// and immediately close the underlying streams
					if ( !File.Exists( FILE_LOG_ENABLED ) )
						File.CreateText( FILE_LOG_ENABLED ).Close();

					// load the oldest messages first
					int oldLineCount = 0;
					if ( File.Exists( FILE_LOG_OLD ) )
						oldLineCount = LoadMessages( FILE_LOG_OLD );

					// load the newest messages last, and get the line count
					_fileLineCount = 0;
					if ( File.Exists( FILE_LOG_CURRENT ) )
						_fileLineCount = LoadMessages( FILE_LOG_CURRENT );

					Debug( string.Format( "LOG: {0} MESSAGES LOADED FROM FILES", _fileLineCount + oldLineCount ) );
				}
				catch ( Exception ex )
				{
					WriteFileError( "LOG: UNABLE TO LOAD MESSAGES FROM FILES", ex, true );
				}
			}
		}

		/// <summary>
		/// Reads from the provided (text) file and loads each line into memory.
		/// This method assumes the caller has obtained the proper lock.
		/// </summary>
		static private int LoadMessages( string logFilePath )
		{
			int lineCount = 0;

			if ( File.Exists( logFilePath ) )
			{
				using ( StreamReader reader = new StreamReader( logFilePath ) )
				{
					string line;
					while ( ( line = reader.ReadLine() ) != null )
					{
						lineCount++;
						_queue.Enqueue( line + Environment.NewLine );
					}
				}

				// reduce the amount of in-memory messages
				Truncate( Capacity );
			}

			// returns the count of lines read from the file
			return lineCount;
		}

		/// <summary>
		/// Writes error messages that occurred due to the log to file feature to the in-memory message
		/// queue and the serial port if enabled.  This method assumes the caller has obtained the proper lock.
		/// </summary>
		static private void WriteFileError( string msg, Exception ex, bool logFullException )
		{
			// if an error occurs with a log file, we can't log it to the filesystem
			bool state = _logToFile;
			_logToFile = false;

			if ( logFullException )
			{
				Error( msg, ex );
			}
			else
			{
				Error( msg );
				Error( ex.Message );
			}

			_logToFile = state;
		}

		/// <summary>
		/// This method writes a single line formatted message to FILE_LOG_CURRENT.
		/// If FILE_LOG_CURRENT will exceed FILE_MAX_LINES, the log files in flash memory
		/// will be truncated.  This method assumes the caller has obtained the proper lock.
		/// </summary>
		static private void WriteToFile( string formattedMsg )
		{
			lock ( Log.NandFlashLock )
			{
				// maintain the size of the log files in flash memory
				if ( _fileLineCount >= FILE_MAX_LINES )
					TruncateFiles();

				StreamWriter sw = null;
				try
				{
					// IMPORTANT: the streamwriter is disposed of in the finally block
					sw = new StreamWriter( FILE_LOG_CURRENT, true );

					// message should already contain a trailing newline character,
					// just append the message to the current log file in flash memory
					sw.Write( formattedMsg );

					// increment line count
					_fileLineCount++;
				}
				catch ( Exception ex )
				{
					WriteFileError( "LOG: UNABLE TO WRITE MESSAGE TO FILE", ex, false );
				}
				finally
				{
					if ( sw != null )
					{
						sw.Dispose();
					}
				}
			}
		}

		/// <summary>
		/// When called, older messages are purged to maintain a reasonable flash memory footprint.
		/// This method assumes the caller has obtained the proper lock.
		/// </summary>
		static private void TruncateFiles()
		{
			try
			{
				// delete the old messages
				if ( File.Exists( FILE_LOG_OLD ) )
					File.Delete( FILE_LOG_OLD );

				// rename the current messages to now be the old messages
				File.Move( FILE_LOG_CURRENT, FILE_LOG_OLD );

				// reset the current line count so truncation does not keep occurring
				_fileLineCount = 0;
				
				Debug( "LOG: MESSAGE FILES TRUNCATED" );
			}
			catch ( Exception ex )
			{
				WriteFileError( "LOG: UNABLE TO TRUNCATE MESSAGE FILES", ex, false );
			}
		}

        static private void Truncate( int maxSize )
        {
            Log.Assert( maxSize >= 0, "maxSize cannot be negative" );

            while ( _queue.Count > maxSize )
                _queue.Dequeue();
        }

        /// <summary>
        /// Clear the log of all accumulated messages.
        /// </summary>
        static public void Clear()
        {
            lock ( _queue )
            {
                _queue.Clear();
            }
        }

        static public string DateTimeToString( DateTime dateTime, string formatSpecifier )
        {
            string s = dateTime.ToString( formatSpecifier );

            // If a custom format specifier is passed in, then DON'T format the DateTimeKind property.
            if ( Object.ReferenceEquals( formatSpecifier, DATETIME_FORMAT ) )
            {
                if ( dateTime.Kind == DateTimeKind.Local )
                    s += " (Local)";
                else if ( dateTime.Kind == DateTimeKind.Utc )
                    s += " (UTC)";
            }

            return s;
        }
        /// <summary>
        /// Does a ToString on the passed-in DateTime using a format of "MM/dd/yy HH:mm:ss"
        /// (military time).
        /// </summary>
        /// <param name="dateTime"></param>
        static public string DateTimeToString( DateTime dateTime )
        {
            return DateTimeToString( dateTime, DATETIME_FORMAT );
        }

        /// <summary>
        /// Returns "null" if value is null; otherwise does a ToString on the passed-in DateTime
        /// using a format of "MM/dd/yy HH:mm:ss".
        /// (military time).
        /// </summary>
        /// <param name="dateTime"></param>
        static public string DateTimeToString( Nullable<DateTime> dateTime )
        {
            return ( dateTime == null ) ? "(null)" : DateTimeToString( (DateTime)dateTime );
        }
		
        /// <summary>
        /// Returns a list of all accumulated messages.
        /// </summary>
        /// <remarks>
        /// 
        /// </remarks>
        /// <returns>A queue is returned so that the caller may opt to easily consume
        /// the messages from the queue as they process them, in order to save memory.
        /// i.e...
        /// <code>
        /// Queue messages = Log.GetMessages()
        /// while ( messages.Count > 0 )
        /// {
        ///     DoSomethingWithMessages( messages.Deqeue() );
        /// }
        /// </code>
        /// </returns>
        static public Queue<string> GetMessages()
        {
            lock ( _queue )
            {
                // Copy the messages from our static queue to a local queue
                // that we will return to the caller.
                return new Queue<string>( _queue );
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="ex"></param>
        static private void Write( string msg, Exception ex )
        {
            lock ( _queue )
            {
                _writeStringBuilder.Length = 0;

                _writeStringBuilder.Append( msg );
                if ( _writeStringBuilder.Length > 0 )
                    _writeStringBuilder.Append( " - EXCEPTION CAUGHT..." );
                _writeStringBuilder.Append( Environment.NewLine );
                _writeStringBuilder.Append( "..." );
                _writeStringBuilder.Append( ex.ToString() );

                Write( _writeStringBuilder.ToString() );
            }
        }

        /// <summary>
        /// Write a message to the debug terminal.
        /// </summary>
        /// <param name="msg">The message to output.</param>
        static private void Write( string msg )
        {
            string[] msgs = msg.ToString().Split( new char[] { '\r', '\n' } );

            // Don't let multiple threads spew out messages at same time.
            lock ( _queue )
            {
                // Write() gets called *A LOT*.  Try and suppress datetime formatting until we really 
                // have to.  (Many calls to Write() can be done within a single second).
                DateTime now = DateTime.UtcNow;
                if ( now.Second != _lastWriteTime.Second
                || now.Minute != _lastWriteTime.Minute
                || now.Hour != _lastWriteTime.Hour )
                {
                    _lastWriteTime = now;
                    _lastWriteTimeFormatted = Log.DateTimeToString( now, "MM/dd/yy HH:mm:ss" );
                }

                for ( int i = 0; i < msgs.Length; i++ )
                {
                    string msgLine = msgs[ i ];

                    if ( msgLine == string.Empty )
                        continue;

                    _writeStringBuilder.Length = 0;

                    _writeStringBuilder.Append( _lastWriteTimeFormatted );
                    _writeStringBuilder.Append( " " );

                    // Always display threadname when debugging.
                    // Otherwise, only display if 'Tracing'.
#if DEBUG 
                    LogThreadName();
#else
                    if ( Level >= LogLevel.Trace )
                        LogThreadName();
#endif
                    if ( i > 0 )
                        _writeStringBuilder.Append( "..." );
                    _writeStringBuilder.Append( msgLine );
                    _writeStringBuilder.Append( Environment.NewLine );

                    string formattedMsg = _writeStringBuilder.ToString();

					if ( _logToFile )
						WriteToFile( formattedMsg );
					
                    _queue.Enqueue( formattedMsg );

                    Truncate( Capacity );

      //              if ( _loggingEnabled ) // sending messages to the serial port can be disabled to improve D2G
						//WinCeApi.NKDbgPrintfW( formattedMsg );
                } // end-for

                // If stringbuilder grows too large, toss it away and start a fresh one.
                if ( _writeStringBuilder.Length > WRITE_STRING_BUILDER_SIZE )
                    _writeStringBuilder = new StringBuilder( WRITE_STRING_BUILDER_SIZE );

            } // end-lock
        }

        static private void LogThreadName()
        {
            string name = Thread.CurrentThread.Name;
            if ( name != null )
            {
                _writeStringBuilder.Append( "(" );
                if ( name.Length > 10 )
                    _writeStringBuilder.Append( name.Substring( 0, 10 ) );
                else
                    _writeStringBuilder.Append( name.PadRight( 10, ' ' ) );
                _writeStringBuilder.Append( ") " );
            }
        }

        /// <summary>
        /// Prints an warning message to the debug COM port.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void Fatal( string msg )
        {
            if ( Level >= LogLevel.Fatal ) Write( msg );
        }

        /// <summary>
        /// Print an exception's message to the debug COM port.
        /// </summary>
        /// <param name="ex">The exception to print.</param>
        static public void Fatal( Exception ex )
        {
            if ( Level >= LogLevel.Fatal ) Write( string.Empty, ex );
        }

        /// <summary>
        /// Print an exception's message to the debug COM port.
        /// </summary>
        /// <param name="msg">An extra message helping to indicate the origin of the exception</param>
        /// <param name="ex">The exception to print.</param>
        static public void Fatal( string msg, Exception ex )
        {
            if ( Level >= LogLevel.Fatal ) Write( msg, ex );
        }

        /// <summary>
        /// Prints an warning message to the debug COM port.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void Error( string msg )
        {
            if ( Level >= LogLevel.Error ) Write( msg );
        }

        /// <summary>
        /// Print an exception's message to the debug COM port.
        /// </summary>
        /// <param name="ex">The exception to print.</param>
        static public void Error( Exception ex )
        {
            Error( string.Empty, ex );
        }

        /// <summary>
        /// Print an exception's message to the debug COM port.
        /// </summary>
        /// <param name="msg">An extra message helping to indicate the origin of the exception</param>
        /// <param name="ex">The exception to print.</param>
        static public void Error( string msg, Exception ex )
        {
            if ( Level >= LogLevel.Error ) Write( msg, ex );
        }

        /// <summary>
        /// Prints an warning message to the debug COM port.
        /// </summary>
        /// <param name="msg">The message to print.</param
        /// <param name="ex">The exception to print.</param>
        static public void Warning( string msg, Exception ex )
        {
            if ( Level >= LogLevel.Warning ) Write( msg, ex );
        }

        /// <summary>
        /// Prints an warning message to the debug COM port.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void Warning( string msg )
        {
            if ( Level >= LogLevel.Warning ) Write( msg );
        }

        /// <summary>
        /// Prints a debug message to the debug COM port.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void Info( string msg )
        {
            if ( Level >= LogLevel.Info ) Write( msg );
        }

        /// <summary>
        /// Prints a debug message to the debug COM port.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void Debug( string msg )
        {
            if ( Level >= LogLevel.Debug ) Write( msg );
        }

        /// <summary>
        /// Prints an information message to the debug COM port.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void Trace( string msg )
        {
            if ( Level >= LogLevel.Trace ) Write( msg );
        }

        /// <summary>
        /// Log the message and stack trace and then throws an AssertException.
        /// This method is a replacement for Debug.Assert.
        /// </summary>
        /// <param name="message">A message to log.</param>
        /// <exception cref="AssertException"/>
        [Conditional( "DEBUG" )]
        static public void Assert( string message )
        {
            Assert( false, message );
        }

        /// <summary>
        /// Logs the message and stack trace if the specified condition fails and
        /// then throws an AssertException.
        /// This method is a replacement for Debug.Assert.
        /// </summary>
        /// <param name="condition">true to prevent a message being logged; otherwise, false.</param>
        /// <exception cref="AssertException"/>
        [Conditional( "DEBUG" )]
        static public void Assert( bool condition )
        {
            Assert( condition, null );
        }

        /// <summary>
        /// Logs the message and stack trace if the specified condition fails and
        /// then throws an AssertException.
        /// </summary>
        /// <remarks>
        /// This replacement for Debug.Assert is necessary due to the fact that in
        /// Compact Framework, Debug.Assert seems to want to display a message box
        /// even though we have a headless device.
        /// Removing nor replacing the DefaultTraceListener had no effect.
        /// </remarks>
        /// <param name="condition">true to prevent a message being logged; otherwise, false.</param>
        /// <param name="message">A message to log.</param>
        /// <exception cref="AssertException"/>
        [Conditional( "DEBUG" )]
        static public void Assert( bool condition, string message )
        {
            if ( condition ) return;

            try
            {
                // throw an exception so we can get a stack trace. (we need to do
                // this because StackTrace class is not supported in compact framework).
                throw new AssertException( message );
            }
            catch ( AssertException ae )
            {
                Write( "************************************************************" );
                Write( "******************   ASSERTION FAILED!!   ******************" );
                if ( message != null )
                    Write( message );
                Write( ae.ToString() );
                Write( "************************************************************" );

                throw; // re-throw it to the caller.
            }
        }

        #region Timing Log Messages

        /// <summary>
        /// Prints a message to the debug COM port which indicates the beginning of a timing measurement.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public Stopwatch TimingBegin(string id)
        {
            if (Level >= LogLevel.Info)
            {
                Write(string.Format("---  TIMING BEGIN <{0}>  ---", id));
            }
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            return stopwatch;
        }

        /// <summary>
        /// Prints a message to the debug COM port which indicates the ending of a timing measurement.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void TimingEnd(string id, DateTime startTime)
        {
            if ( Level >= LogLevel.Info )
            {
                TimeSpan elapsedTime = DateTime.UtcNow - startTime;
                Write(string.Format("---  TIMING END <{0}> Elapsed Time = {1} seconds  ---", id, elapsedTime.TotalSeconds.ToString("f03") ) );
            }
        }

        /// <summary>
        /// Prints a message to the debug COM port which indicates the ending of a timing measurement.
        /// </summary>
        /// <param name="msg">The message to print.</param>
        static public void TimingEnd( string id, Stopwatch stopWatch )
        {
            if ( Level >= LogLevel.Info )
            {
                long ms = stopWatch.ElapsedMilliseconds;
                Write( string.Format( "---  TIMING END <{0}> Elapsed Time = {1} seconds  ---", id, ( ms / 1000.0).ToString("f03") ) );
            }
        }

        #endregion


    } // end-class Log

    internal class AssertException : Exception
    {
        internal AssertException( string message ) : base( message ) {}
    }
}
