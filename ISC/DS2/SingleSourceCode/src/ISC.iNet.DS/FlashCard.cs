using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using ISC.WinCE.Logger;
using ISC.WinCE;


namespace ISC.iNet.DS
{
    /// <summary>
    /// 
    /// TODO - why is this class P/Inovking to WinCE API calls instead of just
    /// using the Compact Framework classes that do file I/O ?
    /// 
    /// </summary>
    public class FlashCard
    {
        /// Used to allow exclusive access to flash card, to try
        /// and prevent multiple threads from writing concurrently.
        private static readonly object _lock = new object();

        /// <summary>
        /// Waits for the OS to mount (and autoformat) the flash card.
        /// </summary>
        /// <returns>
        /// false if wait reaches times out and no card is mounted.
        /// true if card is found to be mounted.
        /// </returns>
        public static bool WaitForMount()
        {
            DriveInfo di = null;

            // On bootup, the card will not always been seen right away because
            // the OS may have not mounted it yet.  So we retry.

            for ( int i = 1; i <= 60; i++ )  // 60 retries @ 1s per retry = 60 seconds, which should be plenty for a 16gb card.
            {                
                try
                {
                    di = new DriveInfo( Controller.FLASHCARD_PATH.TrimEnd( new char[] { '\\' } ) );
                    break;
                }
                catch ( ArgumentException ae ) // should throw argumentexception if card is missing.
                {
                    ISC.WinCE.Logger.Log.Warning( "\"" + Controller.FLASHCARD_PATH + "\" - " + ae.Message );
                    ISC.WinCE.Logger.Log.Warning( "Waiting for OS to mount & autoformat it" );

					// wait a short amount of time before checking again
					Thread.Sleep(1000);
                }
                catch ( Exception e )
                {
                    ISC.WinCE.Logger.Log.Fatal( "\"" + Controller.FLASHCARD_PATH + "\" - ERROR! - " + e.ToString() );
                    return false;
                }
            }

            if ( di == null )
            {
                ISC.WinCE.Logger.Log.Fatal( "\"" + Controller.FLASHCARD_PATH + "\" NOT FOUND!" );
                return false;
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// Intended to only be called ONCE on bootup.
        /// </remarks>
        /// <returns>true if flash card found, else false.</returns>
        public static void Log()
        {
            DriveInfo di = null;

            try
            {
                di = new DriveInfo( Controller.FLASHCARD_PATH.TrimEnd( new char[] { '\\' } ) );
            }
            catch ( ArgumentException ae ) // should throw argumentexception if card is missing.
            {
                ISC.WinCE.Logger.Log.Error( "\"" + Controller.FLASHCARD_PATH + "\" - " + ae.Message );
            }
            catch ( Exception e )
            {
                ISC.WinCE.Logger.Log.Error( "\"" + Controller.FLASHCARD_PATH + "\" - ERROR! - " + e.ToString() );
                return;
            }

            if ( di == null )
            {
                ISC.WinCE.Logger.Log.Error( "\"" + Controller.FLASHCARD_PATH + "\" NOT FOUND!" );
                return;
            }

            ISC.WinCE.Logger.Log.Info( "Flash Card Information..." );

            long usedSpace = di.TotalSize - di.TotalFreeSpace;
            decimal totalPercentFree = ( (decimal)di.TotalFreeSpace / (decimal)di.TotalSize ) * 100.0m;
            totalPercentFree = Math.Round( totalPercentFree, 1 );

            //ISC.WinCE.Logger.Log.Info( "Manufacturer ID:  \"" + di.ManufacturerID + "\"" );
            //ISC.WinCE.Logger.Log.Info( "Serial Number:    \"" + di.SerialNumber + "\"" );
            ISC.WinCE.Logger.Log.Info( "Root Directory:   \"" + di.RootDirectory + "\"" );
            ISC.WinCE.Logger.Log.Info( "Used Space:       " + usedSpace );
            ISC.WinCE.Logger.Log.Info( "Total Size:       " + di.TotalSize );
            ISC.WinCE.Logger.Log.Info( "Total Free Space: " + di.TotalFreeSpace + string.Format( " ({0}%% available)", totalPercentFree ) );
            ISC.WinCE.Logger.Log.Info( "Avail Free Space: " + di.AvailableFreeSpace );

            return;
        }

        /// <summary>
        /// Used to allow exclusive access to flash card, to try
        /// and prevent multiple threads from writing concurrently.
        /// </summary>
        /// <returns></returns>
        public static object Lock { get { return _lock; } }

        /// <summary>
        /// Write the passed-in text to a file with the specified name.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="text"></param>
        public static void WriteTextFile( string fileName, string text )
        {
            fileName = Controller.FLASHCARD_PATH + fileName;

            lock ( FlashCard.Lock )
            {
                try
                {
                    using ( StreamWriter logFile = new StreamWriter( fileName, false ) )
                    {
                        logFile.Write( text );
                    }
                }
                catch ( Exception e )
                {
                    ISC.WinCE.Logger.Log.Error( string.Format( "Error writing text file \"{0}\"", fileName ), e );
                }
            }
        }

        /// <summary>
        /// Read text out of text file with the specified name and returns
        /// the text as a string.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="text"></param>
        public static string ReadTextFile( string fileName )
        {
            StringBuilder logFileContents = new StringBuilder();

            fileName = Controller.FLASHCARD_PATH + fileName;

            lock ( FlashCard.Lock )
            {
                try
                {
                    string line;
                    using ( StreamReader logFile = new StreamReader( fileName ) )
                    {
                        while ( ( line = logFile.ReadLine() ) != null )
                        {
                            logFileContents.Append( line );
                            logFileContents.Append( "\n" );
                        }
                    }
                }
                catch ( Exception e )
                {
                    ISC.WinCE.Logger.Log.Error( string.Format( "Error reading text file \"{0}\"...", fileName ), e );
                    return ""; // SGF  05-Apr-2011  INS-1752
                }

                return logFileContents.ToString();
            }
        }
    }
}
