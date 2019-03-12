using System;
using System.IO;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Zip;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet;
using ISC.iNet.DS.Services.Resources;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// This operation downloads new docking station firmware from iNet,
    /// then upgrades the docking station with it.
    /// </summary>
    public class FirmwareUpgradeOperation : FirmwareUpgradeAction, IOperation
    {
        private const int MAX_ATTEMPTS = 5;

        // the first file found in the zip file with this extension will be assumed to be the actual firmware
        private const string IMAGE_FILE_NAME = "nk.bin";

        private MemoryStream _firmwareFile = null;

        FirmwareUpgradeEvent _firmwareUpgradeEvent;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="firmwareUpgradeAction"></param>
        public FirmwareUpgradeOperation( FirmwareUpgradeAction firmwareUpgradeAction ) : base( firmwareUpgradeAction )
        {
            _firmwareUpgradeEvent = new FirmwareUpgradeEvent( this );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>null.</returns>
        public DockingStationEvent Execute()
        {
            string funcMsg = Name + ".Execute: ";

            try
            {
                // Contact iNet and ask if any updates are available. If so, download the update,
                // verify MD5 hashcode.  If hashcode fails, retry (multiple times).
                //
                // We'll get back a false if we successfully contact inet, but iNet purposely 
                // doesn't give us back anything. If this happens, we consider the operation as
                // finished as there's nothing more we can do.
                if ( !DownloadFirmware() )
                    return _firmwareUpgradeEvent;

                ExtractFirmware(); // Extract firmware image from downloaded zip file.

                SaveFirmware(); // Write firmware image to flash memory.
            }
            catch ( FirmwareUpgradeException fue )
            {
                Log.Error( string.Format( "{0}Caught FirmwareUpgradeException", funcMsg ), fue );
                _firmwareUpgradeEvent.Errors.Add( new DockingStationError( fue, DockingStationErrorLevel.Warning, Configuration.DockingStation.SerialNumber ) );
                return _firmwareUpgradeEvent;
            }
            catch ( Exception e )
            {
                Log.Error( string.Format( "{0}Caught Exception", funcMsg ), e );
                // Wrap all exceptions within a FirmwareUpgradeException so that we can parse for it on iNet server.
                _firmwareUpgradeEvent.Errors.Add( new DockingStationError( new FirmwareUpgradeException( Name, e ), DockingStationErrorLevel.Warning, Configuration.DockingStation.SerialNumber ) );
                return _firmwareUpgradeEvent;
            }

            _firmwareUpgradeEvent.RebootRequired = true;

            return _firmwareUpgradeEvent;
        }

        /// <summary>
        /// Download firmware zip file from iNet server.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns>
        /// true if call to iNet succeeds, and it returns us new firmware.
        /// 
        /// false if call to iNet succeeds, but iNet purposely doesn't give us back any firmware.
        /// 
        /// Throws an exception if we fail to download firmware due to download errors, etc.
        /// </returns>
        private bool DownloadFirmware()
        {
            string funcMsg = Name + ".DownloadFirmware: ";
            Log.Debug(string.Format("{0}Attempting to download firmware; the maximum number of tries is {1}", funcMsg, MAX_ATTEMPTS));  
            
            string msg = string.Empty;

            for ( int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++ )
            {
                Log.Debug( string.Format( "{0}attempt {1} of {2}", funcMsg, attempt, MAX_ATTEMPTS ) );

                Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.DOWNLOADING );

                //We don't need to consider SubType for DSX as of now, since the Firmware varies only for instrument's Subtype.
                string equipmentType = Configuration.DockingStation.Type.ToString();

                using ( InetDownloader inetDownloader = new InetDownloader() )
                {
                    FirmwareUpgrade = inetDownloader.DownloadFirmwareUpgrade(null, _firmwareUpgradeEvent.Errors, EquipmentTypeCode.VDS, equipmentType, null, equipmentType);                   
                }

                if ( FirmwareUpgrade == null )
                {
                    Log.Debug( string.Format( "{0}Nothing returned by iNet.", funcMsg ) );
                    return false;
                }

                if ( FirmwareUpgrade.Firmware == null )
                {
                    Log.Debug( string.Format( "{0}No firmware returned by iNet.", funcMsg ) );
                    return false;
                }

                Log.Debug( string.Format( "{0}Firmware DeviceType: {1}.", funcMsg, FirmwareUpgrade.EquipmentCode ) );
                Log.Debug( string.Format( "{0}Firmware Version: {1}.", funcMsg, FirmwareUpgrade.Version ) );
                Log.Debug( string.Format( "{0}Firmware Size: {1} bytes.", funcMsg, FirmwareUpgrade.Firmware.Length ) );
                Log.Debug( string.Format( "{0}Firmware iNet checksum: \"{1}\".", funcMsg, FirmwareUpgrade.MD5HashToString( FirmwareUpgrade.MD5Hash ) ) );

                if (FirmwareUpgrade.EquipmentCode != Configuration.DockingStation.Type.ToString())
                {
                    msg = string.Format("Downloaded firmware is for wrong device type (\"{0}\").  Expected \"{1}\" ", FirmwareUpgrade.EquipmentCode, Configuration.DockingStation.Type.ToString());
                    Log.Error( msg );
                    throw new FirmwareUpgradeException( msg );
                }

                Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.VERIFYING );

                byte[] hash = new MD5CryptoServiceProvider().ComputeHash( FirmwareUpgrade.Firmware );

                bool verified = VerifyHash( FirmwareUpgrade.MD5Hash, hash );

                if ( verified )
                {
                    Log.Debug( string.Format( "{0}Firmware successfully downloaded.", funcMsg ) );
                    return true;
                }

                Log.Debug( string.Format( "{0}Verification of MD5 hash failed.", funcMsg ) );
            }

            msg = string.Format( "{0}Unable to download firwmare after {1} attempts.", funcMsg, MAX_ATTEMPTS );
            Log.Error( msg );
            throw new FirmwareUpgradeException( msg );
        }

        /// <summary>
        ///  Extract firmware image from downloaded zip file.
        /// </summary>
        private void ExtractFirmware()
        {
            string funcMsg = Name + ".ExtractFirmware: ";  

            Master.Instance.ConsoleService.UpdateAction(ConsoleServiceResources.EXTRACTING);

            Log.Debug(string.Format("{0}Extracting contents of {1} byte zip file", funcMsg, FirmwareUpgrade.Firmware.Length)); 

            DateTime startTime = DateTime.UtcNow;

            try
            {
                using ( MemoryStream memStream = new MemoryStream( FirmwareUpgrade.Firmware, false ) )
                {
                    // System.IO.Packaging.ZipPackage can handle .zip files, but it is not supported in Compact Framework.
                    // We therefore use the freeware SharpZipLib to do our uncompression.
                    using ( ZipInputStream s = new ZipInputStream( memStream ) )
                    {
                        ZipEntry zipEntry;
                        while ( ( zipEntry = s.GetNextEntry() ) != null )
                        {
                            Log.Debug(string.Format("{0}ZipEntry Name=\"{1}\", Compressed size={2}, Uncompressed size={3}", funcMsg, zipEntry.Name, zipEntry.CompressedSize, zipEntry.Size));  

                            string fileName = Path.GetFileName( zipEntry.Name );

                            if ( fileName == String.Empty )
                                continue;

                            // Skip zipped files that aren't the actual OS image.
                            if ( string.Compare( fileName.ToLower(), IMAGE_FILE_NAME ) != 0 )
                                continue;

                            // Note that we set the capacity of the memorystream to EXACTLY the size of the
                            // deflated file.  This solves two problems:  1) It prevents excessive memory
                            // growth as we write the memorystream (e.g., a 2MB firmware file may cause 
                            // the capacity to grow to as much as 4MB if we didn't cap it), and 2)
                            // it allows us to call GetBuffer() on the MemoryStream later and get back 
                            // the exact contents of the extracted file instead of having to do a ToArray()
                            // call on it to get the exact file by doing a whole copy of the stream's
                            // internal buffer
                            using ( MemoryStream unzippedStream = new MemoryStream( (int)zipEntry.Size ) )
                            {
                                byte[] buf = new byte[8192];

                                while ( true )
                                {
                                    int size = s.Read( buf, 0, buf.Length );

                                    if ( size <= 0 )
                                        break;

                                    unzippedStream.Write( buf, 0, size );
                                }

                                _firmwareFile = unzippedStream;
                            }

                            break;
                        }  // end-while GetNextEntry
                    }  // end-using ZipInputStream
                }  // end-using MemoryStream
            }
            catch ( Exception e )
            {
                throw new FirmwareUpgradeException( "Error unzipping firmware upgrade.", e );
            }

            TimeSpan elapsed = DateTime.UtcNow - startTime;
            Log.Debug(string.Format("{0}Extraction finished in {1} seconds.", funcMsg, elapsed.TotalSeconds));  

            if ( _firmwareFile == null )
                throw new FirmwareUpgradeException( string.Format( "No \"{0}\" file found inside downloaded firmware upgrade file.", IMAGE_FILE_NAME ) );
        }

        /// <summary>
        /// // Write firmware image to flash memory.
        /// </summary>
        private void SaveFirmware()
        {
            string funcMsg = Name + ".SaveFirmware: ";

            Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.SAVING ); // TODO - simulate saving

            Log.Debug(string.Format("{0}Saving extracted contents of firmware file", funcMsg)); 
            for ( int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++ )
            {
                Log.Debug( string.Format( "{0}attempt {1} of {2}", funcMsg, attempt, MAX_ATTEMPTS ) );

                Log.Debug( string.Format( "{0}Saving {1} bytes to NAND", funcMsg, _firmwareFile.GetBuffer().Length ) );

				bool saved = false;

				lock ( Log.NandFlashLock )
				{
					saved = Controller.SendImageToNand( _firmwareFile.GetBuffer(), (uint)_firmwareFile.GetBuffer().Length );
				}

                if ( saved )
                {
                    Log.Debug( string.Format( "{0}Firmware successfully saved to NAND.", funcMsg ) );
                    Log.Debug( string.Format( "{0}Upgrade should complete after reboot.", funcMsg ) );
                    return;
                }

                Log.Error( string.Format( "{0}FAILED TO SAVE FIRMWARE!", funcMsg ) );
            }

            string msg = string.Format( "{0}Unable to save v{1} firmware ({2} bytes) after {3} attempts.", funcMsg, FirmwareUpgrade.Version, FirmwareUpgrade.Firmware.Length, MAX_ATTEMPTS );
            Log.Error( msg );
            throw new FirmwareUpgradeException( msg );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expected"></param>
        /// <param name="actual"></param>
        /// <returns></returns>
        private bool VerifyHash( byte[] expected, byte[] actual )
        {
            bool verified = FirmwareUpgrade.CompareMD5Hash( expected, actual );

            Log.Debug( string.Format( "VerifyHash: expected=\"{0}\"", FirmwareUpgrade.MD5HashToString( expected ) ) );
            Log.Debug( string.Format( "VerifyHash: actual  =\"{0}\"", FirmwareUpgrade.MD5HashToString( actual ) ) );
            Log.Debug( string.Format( "VerifyHash: {0}", verified ? "PASSED" : "FAILED" ) );

            return verified;
        }

    }  // end-class FirmwareUpgradeOperation

    public class FirmwareUpgradeException : ApplicationException
    {
        public FirmwareUpgradeException( string message ) : base( message ) { }

        public FirmwareUpgradeException( string message, Exception inner ) : base( message, inner ) { }
    }
}
