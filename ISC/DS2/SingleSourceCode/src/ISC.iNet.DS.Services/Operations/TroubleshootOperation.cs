using System;
using System.Collections.Generic;
using System.Text;
using ISC.iNet.DS.Services.Resources;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS;
using ISC.WinCE.Logger;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;


namespace ISC.iNet.DS.Services
{
	public class TroubleshootOperation : TroubleshootAction, IOperation
	{
		private enum TroubleshootStatus
		{
			None,
			NotFound,
			Failed,
			Succeeded
		}

		#region Fields

		private const string LOG_LABEL = "TROUBLESHOOT: ";

		private MemoryStream _zipMemoryStream;
		private ZipOutputStream _zipOutputStream;
		
		private string _zipFilePath = String.Empty;
		private DateTime _zipFileTimeStamp = new DateTime(2000, 1, 1);
		private long _zipLength = 0;

		private long _totalFileBytes = 0;

		private TroubleshootStatus _status = TroubleshootStatus.None;

		#endregion

		#region Constructors

		public TroubleshootOperation() { }

		public TroubleshootOperation( TroubleshootAction troubleshootAction ) : base( troubleshootAction ) { }

		#endregion

		#region Methods

		/// <summary>
		/// If a USB drive is attached, the debug log and iNet.db3 database will be zipped up and copied to it.
		/// </summary>
		/// <returns>A TroubleshootEvent is returned.</returns>
		public DockingStationEvent Execute()
		{
			string funcMsg = Name + ".Execute";
			Log.Debug( funcMsg );

			TroubleshootEvent returnEvent = new TroubleshootEvent( this );

			try
			{
				// don't start the operation if the USB drive is not still connected
				if ( Controller.IsUsbDriveAttached( LOG_LABEL ) )
				{
					Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.TROUBLESHOOT_COPYING );

					// setup streams that will be used to produce the zip file
					using ( this._zipMemoryStream = new MemoryStream() )
					{
						using ( this._zipOutputStream = new ZipOutputStream( this._zipMemoryStream ) )
						{
							// zip file name which uses current time will be set
							PrepareZipFile();

							// zip up one file at a time in memory
							ZipDebugLog();
							ZipInetDatabase();

							// write the zip file stream in memory to the usb drive
							FinalizeZipFile();
						}
					}
				}
				else
				{
					this._status = TroubleshootStatus.NotFound;
				}
			}
			catch ( Exception ex )
			{
				// could add exception to return event errors if iNet wanted informed when this happens
				this._status = TroubleshootStatus.Failed;
				Log.Error( string.Format( "{0} Caught Exception", funcMsg ), ex );
			}

			// display outcome on LCD
			switch ( this._status )
			{
				case TroubleshootStatus.NotFound:
					Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.TROUBLESHOOT_NOTFOUND );
					break;
				case TroubleshootStatus.Succeeded:
					Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.TROUBLESHOOT_SUCCEEDED );
					break;
				default:
					// if the usb drive was initially found, but the operation did not succeeded, assume failure
					Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.TROUBLESHOOT_FAILED );
					break;
			}

			// 10 second sleep so user can see the outcome of the troubleshoot operation on the LCD
			System.Threading.Thread.Sleep( 10000 );

			// this return event should not be reported to iNet
			return returnEvent;
		}



		private void PrepareZipFile()
		{
			// this timestamp will be used for the create and modified dates of the zip file
			this._zipFileTimeStamp = Configuration.GetLocalTime();
			
			// set zip file name - e.g. Troubleshoot_1207096-022_20140328_1320.zip
			string dateTimeFormat = this._zipFileTimeStamp.ToString("yyyyMMdd_HHmm");
			this._zipFilePath = Controller.USB_DRIVE_PATH + "Troubleshoot_" + Configuration.DockingStation.SerialNumber + "_" + dateTimeFormat + ".zip";

			Log.Debug( string.Format( "{0}Zip file path - {1}", LOG_LABEL, this._zipFilePath ) );

			// an existing file with the same name should not exist, unless two are created in the same minute
			if ( File.Exists( this._zipFilePath ) )
			{
				Log.Warning(string.Format("{0}File \"{1}\" already exists.  It will be overwritten.", LOG_LABEL, this._zipFilePath));
			}

			// Use default compression level (0-9) which may be 6.
			this._zipOutputStream.SetLevel( Deflater.DEFAULT_COMPRESSION );
		}

		private void FinalizeZipFile()
		{
			Log.Debug( string.Format( "{0}Zip compression finished.", LOG_LABEL ));

			// False stops the Close from also Closing the underlying stream.
			this._zipOutputStream.IsStreamOwner = false;
			// Must finish the ZipOutputStream before using outputMemStream.
			this._zipOutputStream.Close();

			Log.Debug( string.Format( "{0}Writing zip file to USB drive.", LOG_LABEL ) );

			// Write zip file to USB drive
			using ( FileStream fileStream = new FileStream( this._zipFilePath, FileMode.Create ) )
			{
				fileStream.Write( this._zipMemoryStream.GetBuffer(), 0, (int)this._zipMemoryStream.Length );
				fileStream.Flush();
				fileStream.Close();
			}

			Log.Debug( string.Format( "{0}Updating zip file timestamp with local time.", LOG_LABEL ) );

			// File was created with UTC timestamp, because OS's time zone is UTC.
            // We want the timstamp to be the 'local' timezone. 
            // Correct zip file timestamp from UTC to local time.
			//FileHelper.SetLastWriteTime( this._zipFilePath, this._zipFileTimeStamp );
			//FileHelper.SetCreationTime( this._zipFilePath, this._zipFileTimeStamp );

			FileInfo fileInfo = new FileInfo( this._zipFilePath );
			LogCompression( this._totalFileBytes, this._totalFileBytes, fileInfo.Length );

			// assume pass if no exception has been thrown
			this._status = TroubleshootStatus.Succeeded;
		}

		private void ZipDebugLog()
		{
			string debugLogName = "debug.log";
			int estimatedDebugLogBytes = 0;
			Queue<string> logMessageQueue = Log.GetMessages(); // getting the list of log messages should clear the original list
			Queue<byte[]> logBytesQueue = new Queue<byte[]>( logMessageQueue.Count );
			string message;
			byte[] messageBytes;

			// iterate through message list destroying it as it is processed
			while ( logMessageQueue.Count > 0 )
			{
				message = logMessageQueue.Dequeue();
				
				// calculate message bytes
				messageBytes = Encoding.UTF8.GetBytes( message );
				estimatedDebugLogBytes += messageBytes.Length;
				
				// store message bytes in a new list
				logBytesQueue.Enqueue( messageBytes );
			}

			// Notepad creates UTF-8 files that are 3 bytes long, but File.CreateText creates a
			// UTF-8 file that is 0 bytes long
			this._totalFileBytes += estimatedDebugLogBytes;

			Log.Debug( string.Format( "{0}Compressing {1}", LOG_LABEL, debugLogName ) );

			ZipEntry zipEntry = new ZipEntry( debugLogName );
			zipEntry.DateTime = this._zipFileTimeStamp;

			// assign the entry's size to what the file size is expected to be when uncompressed;
			// this is necessary to get Window's built-in extractor to work according to SharpZipLib sample code
			zipEntry.Size = estimatedDebugLogBytes;

			// add debug.log entry to zip
			this._zipOutputStream.PutNextEntry( zipEntry );

			// don't use a number that can divide into 10000 (default log size) evenly as
			// LogCompression will be called twice in a row without compressing new data 
			long logThreshold = 5001; // log compression every 5001 messages  
			if ( logBytesQueue.Count % logThreshold == 0 )
			{
				// handle non-default log sizes such as 10002
				logThreshold++;
			}

			int bytesRead = 0;
			long totalBytesRead = 0;
			byte[] buffer;
			int count = 0;

			// write bytes to ZipOutputStream
			while (logBytesQueue.Count > 0)
			{
				// dequeue message byte array which is then written to the ZipOutputStream
				buffer = logBytesQueue.Dequeue();
				bytesRead = buffer.Length;

				totalBytesRead += bytesRead;

				// copy the bytes from one log message into the ZipOutputStream; the ZipOutputStream compresses the data as it gets it
				this._zipOutputStream.Write( buffer, 0, bytesRead );
				count++;

				// log partial compression
				if ((count % logThreshold) == 0)
				{
					LogCompression(totalBytesRead, estimatedDebugLogBytes, this._zipMemoryStream.Length - this._zipLength );
				}
			}

			// close file entry in zip package
			this._zipOutputStream.CloseEntry();

			// log final file compression
			LogCompression( totalBytesRead, estimatedDebugLogBytes, this._zipMemoryStream.Length - this._zipLength );

			// update zip length for compression logging specific to the next file
			this._zipLength = this._zipOutputStream.Length;
		}

		private void ZipInetDatabase()
		{
			string iNetDb3FilePath = Controller.FLASHCARD_PATH + Controller.INET_DB_NAME;

			FileInfo fileInfo = new FileInfo( iNetDb3FilePath );
			this._totalFileBytes += fileInfo.Length;

			Log.Debug( string.Format( "{0}Compressing {1}", LOG_LABEL, Controller.INET_DB_NAME ) );

			// read iNet.db3 file into memory
			// it is assumed that nothing else could be accessing the database if the TroubleshootOperation is active
			using ( FileStream fileStream = new FileStream( iNetDb3FilePath, FileMode.Open ) )
			{
				ZipEntry zipEntry = new ZipEntry( Controller.INET_DB_NAME );
				zipEntry.DateTime = this._zipFileTimeStamp;
				// assign the entry's size to what the file size is expected to be when uncompressed;
				// this is necessary to get Window's built-in extractor to work according to SharpZipLib sample code
				zipEntry.Size = fileInfo.Length;

				// add iNet.db3 entry to zip
				this._zipOutputStream.PutNextEntry( zipEntry );

				// copy the file into the ZipOutputStream 
				const int bufferSize = 8192; // 8 KB
				long logThreshold = bufferSize * 50;
				byte[] buffer = new byte[bufferSize];
				int bytesRead = 0;
				long totalBytesRead = 0;

				// write bytes to ZipOutputStream which compresses the data as it gets it
				while ( ( bytesRead = fileStream.Read( buffer, 0, bufferSize ) ) > 0 )
				{
					this._zipOutputStream.Write( buffer, 0, bytesRead );

					totalBytesRead += bytesRead;

					// log partial file compression once every 50 passes
					if ( ( totalBytesRead % logThreshold ) == 0 )
					{
						LogCompression( totalBytesRead, fileInfo.Length, this._zipMemoryStream.Length - this._zipLength );
					}
				}

				// close file entry in zip package
				this._zipOutputStream.CloseEntry();

				// log final file compression
				LogCompression( totalBytesRead, fileInfo.Length, this._zipMemoryStream.Length - this._zipLength );

				// update zip length for compression logging specific to the next file
				this._zipLength = this._zipOutputStream.Length;
			}
		}

		/// <summary>
		/// Method for logging active file(s) compression.
		/// </summary>
		/// <param name="totalBytesRead">Bytes processed so far for the current file.</param>
		/// <param name="totalLength">Size of the file being processed in bytes.</param>
		/// <param name="zipStreamLength">Size of the zip stream in bytes so far from the current file being included.</param>
		private void LogCompression( long totalBytesRead, long totalLength, long zipStreamLength )
		{
			float percentProcessed = ( (float)totalBytesRead / (float)totalLength ) * 100f;
			float compression = ( (float)zipStreamLength / (float)totalBytesRead ) * 100f;

			Log.Debug( string.Format( "{0}Compressed {1} of {2} bytes ({3}%%) down to {4} bytes ({5}%%).",
				LOG_LABEL, totalBytesRead, totalLength, (int)percentProcessed, zipStreamLength, (int)compression ) );
		}

		/// <summary>
		/// Method for logging final zip file compression.
		/// </summary>
		/// <param name="totalLength">Total file bytes that were zipped.</param>
		/// <param name="zipFileLength">Size of the zip file in bytes.</param>
		private void LogCompression( long totalLength, long zipFileLength )
		{
			float compression = ( (float)zipFileLength / (float)totalLength ) * 100f;

			Log.Debug( string.Format( "{0}Compressed {1} bytes down to {4} bytes ({5}%%).",
				LOG_LABEL, totalLength, zipFileLength, (int)compression ) );
		}

		#endregion
	}
}
