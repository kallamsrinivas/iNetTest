using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ICSharpCode.SharpZipLib.Zip;
using ISC.iNet.DS.DataAccess;
using ISC.iNet.DS.DomainModel;
using Instrument_ = ISC.iNet.DS.DomainModel.Instrument;
using ISC.iNet.DS.iNet;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services.Resources;
using ISC.Instrument.Driver;
using ISC.Instrument.Update;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// <para>To upgrade the instrument the docking station needs an "instrument upgrade package" that it
    /// downloads from the iNet server.  An upgrade package is a zip file containing a hex file of the
    /// firmware, and an XML file of Modbus registery information used the upgrade logic.</para>
    /// <para>
    /// It doesn't matter what the name of the zip file is called.</para>
    /// <para>
    /// It also doesn’t matter what the name of the hex file is called. The docking station just assumes
    /// the first file it finds in the zip file that contains a ".hex" extension is the firmware.</para>
    /// <para>
    /// The XML file must be named "registers.xml".
    /// See ISC.Instrument.Update.LoaderBase comments for details of that file.</para>
    /// <para>
    /// The zip file can contain other files, too, such as README’s, etc.
    /// The docking station just ignores them.
    /// </para>
    /// </remarks>
    public class InstrumentFirmwareUpgradeOperation : InstrumentFirmwareUpgradeAction, IOperation, IModbusTracer
    {
        private const int MAX_DOWNLOAD_TRIES = 5;

        // the first file found in the zip file with this extension will be assumed to be the actual firmware
        private const string FIRMWARE_FILE_EXTENSION = ".hex"; 

        // The zip file must contain this file.  Minimally, the xml file needs to contain a root "</registers>" node. 
        private const string REGISTERS_FILE_NAME = "registers.xml";

        const string firmwareFolderPath = Controller.FLASHCARD_PATH + "\\" + "Firmware";

        InstrumentFirmwareUpgradeEvent _firmwareUpgradeEvent;

        private MemoryStream _firmwareFile = null;
        private string _firmwareRegistersXml = null;

        /// <summary>
        /// The docked instrument's serial number
        /// </summary>
        private string _instSerialNumber;

        /// <summary>
        /// The docked instrument's firmware version (prior to upgrade)
        /// </summary>
        private string _instSoftwareVersion;

        /// <summary>
        /// The docked instrument's hardware version (prior to upgrade)
        /// </summary>
        private string _instHardwareVersion;

        private int _displayedPercentComplete = -1;

        private enum UpgradeState
        {
            None,
            BackupStarted,
            BackupCompleted,
            BackupError,
            UpdateStarted,
            UpdateCompleted,
            UpdateError,
            RestoreStarted,
            RestoreCompleted,
            RestoreError
        }

        private LoaderBase _upgrader = null;

        private static UpgradeState _upgraderState = UpgradeState.None;

        // This flag will be set during upgrade or restore phases if user undocks the instrument
        // while in the middle of those phases.
        private bool _undockedError = false;

        private static object _stateLocker = new object();  // TODO: Is this necessary?

        InstrumentData backupData = null;

        #region Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instrumentFirmwareUpgradeAction"></param>
        public InstrumentFirmwareUpgradeOperation( InstrumentFirmwareUpgradeAction instrumentFirmwareUpgradeAction )
            : base( instrumentFirmwareUpgradeAction )
        {
            _firmwareUpgradeEvent = new InstrumentFirmwareUpgradeEvent( this );

            EquipmentType instType = EquipmentType.Undefined;
			if ( Configuration.DockingStation.Type == DeviceType.MX4 )
			{
				if ( Master.Instance.SwitchService.Instrument.Type == DeviceType.VPRO )
					instType = EquipmentType.VentisPro;
				else
					instType = EquipmentType.MX4;
			}
			else if ( Configuration.DockingStation.Type == DeviceType.MX6 )
				instType = EquipmentType.MX6;

			else if ( Configuration.DockingStation.Type == DeviceType.TX1 )
				instType = EquipmentType.TX1;

            else if ( Configuration.DockingStation.Type == DeviceType.SC )
                instType = EquipmentType.SafeCore;

			else if ( Configuration.DockingStation.Type == DeviceType.GBPRO )
				instType = EquipmentType.GasBadgePro;

			else if ( Configuration.DockingStation.Type == DeviceType.GBPLS )
				instType = EquipmentType.GasBadgePlus;

			else
			{
				throw new InstrumentFirmwareUpgradeException( string.Format( "Unsupported instrument type: \"{0}\"", Configuration.DockingStation.Type.ToString() ) );
			}

            _upgrader = UpdateFactory.GetInstrumentUpdater( instType, Controller.INSTRUMENT_COM_PORT, CommunicationModuleTypes.DSX, new DateTimeProvider( Configuration.DockingStation.TimeZoneInfo ), this/*IModbusTracer*/, new AbortRequest( IsNotDocked ) );

            _upgrader.BackupStarted += new BackupStartedEventHandler( Updater_Start );
            _upgrader.BackupComplete += new BackupCompleteEventHandler( Updater_Complete );
            _upgrader.BackupError += new BackupErrorEventHandler( Updater_Error );
            _upgrader.BackupProgress += new BackupProgressEventHandler( Updater_Progress );

            _upgrader.UpdateStarted += new UpdateStartedEventHandler( Updater_Start );
            _upgrader.UpdateComplete += new UpdateCompleteEventHandler( Updater_Complete );
            _upgrader.UpdateError += new UpdateErrorEventHandler( Updater_Error );
            _upgrader.UpdateProgress += new UpdateProgressEventHandler( Updater_Progress );

            _upgrader.RestoreStarted += new RestoreStartedEventHandler( Updater_Start );
            _upgrader.RestoreComplete += new RestoreCompleteEventHandler( Updater_Complete );
            _upgrader.RestoreError += new RestoreErrorEventHandler( Updater_Error );
            _upgrader.RestoreProgress += new RestoreProgressEventHandler( Updater_Progress );
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public DockingStationEvent Execute()
        {
            Stopwatch stopwatch = Log.TimingBegin("INSTRUMENT FIRMWARE UPGRADE");

            string funcMsg = Name + ".Execute: ";

            backupData = null;

            // First, verify instrument can be and is turned on, then download 
            // the firmware, and backup the instrument's settings.
            try
            {
                // Execute a discovery operation.  We really only need to do this in order to
                // make sure the instrument can be and is turned on prior to going any further.
                Log.Debug( string.Format( "{0}Perform instrument discovery", funcMsg ) );
                InstrumentNothingEvent instNothingEvent = (InstrumentNothingEvent)new DiscoveryOperation().Execute();

                // Copy the basic information to the return event.
				_firmwareUpgradeEvent.DockedInstrument = instNothingEvent.DockedInstrument;
                _firmwareUpgradeEvent.DockingStation = instNothingEvent.DockingStation;
                Log.Debug( string.Format( "{0}Upgrade to be performed on docking station with SN={1}", funcMsg, _firmwareUpgradeEvent.DockingStation.SerialNumber ) );

                // Set serialNumber member variable so we can use it in the code instead of
                // continually referencing the SwitchService.Instrument which is unwieldy.
                _instSerialNumber = instNothingEvent.DockedInstrument.SerialNumber;
                _instSoftwareVersion = instNothingEvent.DockedInstrument.SoftwareVersion;
                _instHardwareVersion = instNothingEvent.DockedInstrument.HardwareVersion;

                VerifyDockedInstrument();

                // Contact iNet and ask if any updates are available. If so, download the update,
                // verify MD5 hashcode.  If hashcode fails, retry (multiple times).
                // We'll get back a false if we successfully contact inet, but iNet purposely 
                // doesn't give us back anything. If this happens, we consider the operation as
                // finished as there's nothing more we can do.
                if ( !DownloadFirmware() )
                    return _firmwareUpgradeEvent;

                // Did user undock the instrument during the download?
                // We need to check, because if they do undock during the download, it won't interrupt the download.
                if ( !Controller.IsDocked() )
                    throw new InstrumentNotDockedException();

                ExtractFirmware();

                // Did user undock the instrument during the extraction?
                // We need to check, because if they do undock during the extraction, it won't interrupt the extraction.
                if ( !Controller.IsDocked() )
                    throw new InstrumentNotDockedException();

                // If we make it to here, then we're done with the FirmwareUpgrade.  
                // We set it to null to allow it to be garbage collected.
                FirmwareUpgrade = null;

                backupData = BackupSettings();
            }
            catch ( InstrumentNotDockedException )
            {
                // If user undocks during downloading or backing up of settings, then no harm done.
                // ust rethrow and let upper level logic worry about displaying an undocked instrument error.
                Log.Warning( string.Format( "{0}Caught InstrumentNotDockedException", funcMsg ) );
                throw;
            }
            catch ( CommunicationAbortedException cae )  // undocked during discovery?
            {
                // If user undocks during downloading or backing up of settings, then no harm done.
                // just rethrow and let upper level logic worry about displaying an undocked instrument error.
                Log.Warning( string.Format( "{0}Caught CommunicationAbortedException", funcMsg ) );
                throw new InstrumentNotDockedException( cae );
            }
            catch ( InstrumentFirmwareUpgradeException ifue )
            {
                Log.Error( string.Format( "{0}Caught InstrumentFirmwareUpgradeException", funcMsg ), ifue );
                _firmwareUpgradeEvent.Errors.Add( new DockingStationError( ifue, DockingStationErrorLevel.Warning, _instSerialNumber ) );
                return _firmwareUpgradeEvent;
            }
            catch ( Exception e )
            {
                // Wrap all exceptions within a InstrumentFirmwareUpgradeException so that we can parse for it on iNet server.
                Log.Error( string.Format( "{0}Caught Exception \"{1}\"", funcMsg, e.GetType() ), e );
                _firmwareUpgradeEvent.Errors.Add( new DockingStationError( new InstrumentFirmwareUpgradeException( Name, e ), DockingStationErrorLevel.Warning, _instSerialNumber ) );
                return _firmwareUpgradeEvent;
            }

            // Next, update the instrument's firmware, then restore the instrument's 
            // settings that we backed up just above.
            try
            {
                // We make appropriate calls to do the actual firmware upgrade here.

                if ( _upgraderState != UpgradeState.BackupCompleted )
                {
                    string msg = string.Format( "FAILURE UPGRADING INSTRUMENT, Ended with UpgradeState of \"{0}\"", _upgraderState.ToString() );
                    Log.Error( msg );
                    throw new InstrumentFirmwareUpgradeException( msg );
                }

                Controller.IsFastIsDockedEnabled = false;

                UpdateFirmware();

                if ( _upgraderState != UpgradeState.UpdateCompleted )
                {
                    string msg = string.Format( "FAILURE UPGRADING INSTRUMENT, Ended with UpgradeState of \"{0}\"", _upgraderState.ToString() );
                    Log.Error( msg );
                    throw new InstrumentFirmwareUpgradeException( msg );
                }

                PromptForTurnOn();

                RestoreSettings( backupData );

                if ( _upgraderState != UpgradeState.RestoreCompleted )
                {
                    string msg = string.Format( "FAILURE UPGRADING INSTRUMENT, Ended with UpgradeState of \"{0}\"", _upgraderState.ToString() );
                    Log.Error( msg );
                    throw new InstrumentFirmwareUpgradeException( msg );
                }

                _firmwareUpgradeEvent.UpgradeFailure = false;

                // SGF  17-Feb-2012  INS-2451
                try
                {
                    // Until an instrument settings read can be performed, the instrument information retained 
                    // within the Switch Service will hold the version of the firmware in use prior to the upgrade.
                    // Obtain the new firmware version from the instrument and store it in that retained version
                    // of the instrument information.
                    using ( InstrumentController instrumentController = SwitchService.CreateInstrumentController() )
                    {
                        instrumentController.Initialize( InstrumentController.Mode.Batch );

						// To detect issues when settings are restored, do a full discover and update the cache.
						Instrument_ returnInstrument = instrumentController.DiscoverDockedInstrument( true );

						if ( returnInstrument == null || returnInstrument.SerialNumber.Length == 0 )
						{
							throw new InstrumentNotDockedException();
						}

                        _firmwareUpgradeEvent.DockedInstrument = returnInstrument;
						Master.Instance.SwitchService.Instrument = (Instrument_)returnInstrument.Clone();
                    }
                }
                catch ( Exception e )
                {
                    // We don't expect to get here; if RestoreSettings works, obtaining the software version from 
                    // the instrument should work, too.
                    Log.Debug( e.Message );
                }

                UpdateAction(); // remove action messages.

                Log.Debug( "INSTRUMENT SUCCESSFULLY UPGRADED!" );

            } // end-try
            catch ( InstrumentFirmwareUpgradeException ifue )
            {
                Log.Debug( string.Format( "{0}Caught InstrumentFirmwareUpgradeException", funcMsg ) );
                _firmwareUpgradeEvent.Errors.Add( new DockingStationError( ifue, DockingStationErrorLevel.Warning, _instSerialNumber ) );
            }
            catch ( Exception e )
            {
                // Wrap any aborted exception caused by undocking within a NotDockedException so that we can parse for it on iNet server.
                Log.Debug( string.Format( "{0}Caught Exception={1}", funcMsg, e.ToString() ) );
                if ( e is CommunicationAbortedException )
                    e = new InstrumentNotDockedException( e );
                // Wrap all exceptions within a InstrumentFirmwareUpgradeException so that we can parse for it on iNet server.
                _firmwareUpgradeEvent.Errors.Add( new DockingStationError( new InstrumentFirmwareUpgradeException( Name, e ), DockingStationErrorLevel.Warning, _instSerialNumber ) );

                // show the instrument upgrade error message for 10 seconds as the instrument was undocked
                if ( e is InstrumentNotDockedException )
                {
                    Master.Instance.ConsoleService.UpdateState( ConsoleState.UpgradingInstrumentError );
                    Thread.Sleep( 10000 );
                }
            }
            finally
            {
                Controller.IsFastIsDockedEnabled = true;
            }

            // If instrument successfully upgraded, it likely rebooted at the end.  
            // Wait a moment for warmup before returning back to caller and resuming operations
            // If upgrade is successful, then we need to followup this operation
            // with settings read and settings update operations.
            HandleUpgradeFailure( funcMsg );

            Log.Debug(string.Format("{0}Instrument firmware update process complete", funcMsg));

            Log.TimingEnd( "INSTRUMENT FIRMWARE UPGRADE", stopwatch );

            return _firmwareUpgradeEvent;
        }

        private void HandleUpgradeFailure( string funcMsg )
        {
            // If instrument successfully upgraded, it likely rebooted at the end.  
            // Wait a moment for warmup before returning back to caller and resuming operations.
            if ( _firmwareUpgradeEvent.UpgradeFailure == false
            &&   Configuration.DockingStation.Type != DeviceType.GBPLS
            &&   Configuration.DockingStation.Type != DeviceType.GBPRO
            &&   Configuration.DockingStation.Type != DeviceType.SC )
            {
                // Sleep at end in order to let instrument 'calm down'.
                for ( int sleep = 20; sleep >= 1; sleep-- )
                {
                    Log.Debug( string.Format( "{0}Sleeping for {1} seconds at end of InstrumentFirmwareUpdateOperation", funcMsg, sleep ) );
                    Thread.Sleep( 1000 );
                    if ( !Controller.IsDocked() ) break;

                    _upgraderState = UpgradeState.None;
                    UpdateAction();
                }
            }

            // If upgrade is successful, then we need to followup this operation
            // with settings read and settings update operations.
            if ( _firmwareUpgradeEvent.UpgradeFailure == false )
            {
                try
                {
                    // Deleting the event journals for instrument settings read (and update) as a more reliable alternative to forcing
                    // the events which will not work if the iGas connections were changed during the firmware upgrade operation.
                    EventJournalDataAccess eventCodeDataAccess = new EventJournalDataAccess();
                    using ( DataAccessTransaction trx = new DataAccessTransaction() )
                    {
                        EventCode eventCodeRead = EventCode.GetCachedCode( EventCode.InstrumentSettingsRead );
                        Log.Debug( string.Format( "{0}Deleting {1} for S/N {2} to guarantee it will be run.", funcMsg, eventCodeRead, _instSerialNumber ) );
                        eventCodeDataAccess.DeleteBySerialNumbers( new string[] { _instSerialNumber }, eventCodeRead, trx );

                        EventCode eventCodeUpdate = EventCode.GetCachedCode( EventCode.InstrumentSettingsUpdate );
                        Log.Debug( string.Format( "{0}Deleting {1} for S/N {2} to guarantee it will be run.", funcMsg, eventCodeUpdate, _instSerialNumber ) );
                        eventCodeDataAccess.DeleteBySerialNumbers( new string[] { _instSerialNumber }, eventCodeUpdate, trx );

                        trx.Commit();
                        Log.Debug( string.Format( "{0}Journal deletion committed.", funcMsg ) );
                    }
                }
                catch ( Exception e )
                {
                    // If the journals are not deleted, an instrument settings read will not be run and iNet will not know the
                    // upgraded instrument's new firmware version.  This results in iNet saving new instrument diagnostics, cals 
                    // and bump tests against the old firmware version.  After each heartbeat while the instrument remains docked,
                    // these events will be re-run unnecessarily.
                    Log.Error( string.Format( "{0}JOURNALS COULD NOT BE DELETED!", funcMsg ) );
                    // Wrap all exceptions within a InstrumentFirmwareUpgradeException so that we can parse for it on iNet server.
                    _firmwareUpgradeEvent.Errors.Add( new DockingStationError( new InstrumentFirmwareUpgradeException( Name, e ), DockingStationErrorLevel.Warning, _instSerialNumber ) );
                }
            }
        }

        /// <summary>
        /// Verifies that the docked instrument is allowed to be upgraded.
        /// Intended to be called before we do anything, even before downloading firwmare.
        /// </summary>
        private void VerifyDockedInstrument()
        {
            switch ( Configuration.DockingStation.Type )
            {
                case DeviceType.MX6:

                    // For MX6 instruments, a bootloader version of 3.20 or newer is required for a successful upgrade.
                    // Note that the VDS is not able to query the instrument for the version of its bootloader, so it
                    // cannot enforce this.  The assumption, though, is that if instrument is running firmware 3.20 or
                    // later, then it also has a 3.20 or later bootloader.  Therefore, if docked instrument has firmware
                    // older than 3.20 the docking station must refuse to do the upgrade.
                    if ( _instSoftwareVersion.CompareTo( "3.20.00" ) < 0 )
                    {
                        string msg = string.Format( "Instrument {0} has firmware v{1}, which does not support being upgraded by the docking station.",
                            _instSerialNumber, _instSoftwareVersion );
                        Log.Error( msg );
                        throw new InstrumentFirmwareUpgradeException( msg );
                    }
                    break;

                // TODO - put other instrument types here as needed.

                default:
                    break;

            } // end-switch

            Log.Debug("VerifyDockedInstrument: Instrument has been verified for upgrade.");  
        }

        /// <summary>
        /// 
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
            const string funcMsg = "DownloadFirmware: ";
            Log.Debug(string.Format("{0}Attempting to download instrument firmware; the maximum number of tries is {1}", funcMsg, MAX_DOWNLOAD_TRIES));  

            for ( int tries = 1; tries <= MAX_DOWNLOAD_TRIES; tries++ )
            {
                Log.Debug( string.Format( "{0}try {1} of {2}", funcMsg, tries, MAX_DOWNLOAD_TRIES ) );

                Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.DOWNLOADING );

                //INS-2460 - To retrieve the cached Instrument Firmware upgrade based on equipment type, equipment code and the firmware version
                string firmwareVersion = null;
                FirmwareUpgradeSetting firmwareUpgradeSetting;

                ScheduleProperty scheduleProperty = ScheduleProperties.Find( sp => sp.Attribute == ScheduleProperty.FirmwareUpgradeVersion );
                if ( scheduleProperty != null )
                {
                    firmwareVersion = scheduleProperty.Value;
                }

                string equipmentType = _firmwareUpgradeEvent.DockedInstrument.Type.ToString();
                string equipmentSubType = _firmwareUpgradeEvent.DockedInstrument.Subtype == DeviceSubType.None || _firmwareUpgradeEvent.DockedInstrument.Subtype == DeviceSubType.VentisPro4 ||
                                          _firmwareUpgradeEvent.DockedInstrument.Subtype == DeviceSubType.VentisPro5 ? null : _firmwareUpgradeEvent.DockedInstrument.Subtype.ToString().ToUpper();
                string equipmentFullCode = string.IsNullOrEmpty(equipmentSubType) ? equipmentType : equipmentType + "_" + equipmentSubType;

                string firmwareFilePath = string.Format( "{0}\\{1}_{2}.zip", firmwareFolderPath, equipmentFullCode, firmwareVersion );
                
                if ( !string.IsNullOrEmpty(firmwareVersion)
                    && IsFirmwareCached(firmwareFilePath)
                    && ( ( firmwareUpgradeSetting = GetFirmwareUpgradeSetting( equipmentType, equipmentSubType, firmwareVersion) ) != null )
                    && ( firmwareUpgradeSetting.FileName == firmwareFilePath ) )
                {
                    byte[] firmwareFile = GetFirmwareFromCache(firmwareFilePath);
                    if ( firmwareFile != null )
                        Log.Debug( string.Format( string.Format( "Found cached firmware file \"{0}\", {1} bytes.", firmwareFilePath, firmwareFile.Length ) ) );
                    byte[] checkSum = firmwareUpgradeSetting.CheckSum;
                    FirmwareUpgrade = new FirmwareUpgrade( equipmentType, firmwareVersion, firmwareFile, checkSum, equipmentSubType, equipmentFullCode );
                }
                //This is to check specifically if the firmwareFile is not read properly from the cache (null), then we need to download the firmware and cache it again.
                if (FirmwareUpgrade == null || FirmwareUpgrade.Firmware == null)
                {
                    using (InetDownloader inetDownloader = new InetDownloader())
                    {
                        FirmwareUpgrade = inetDownloader.DownloadFirmwareUpgrade( _instSerialNumber, _firmwareUpgradeEvent.Errors, EquipmentTypeCode.Instrument, equipmentType, equipmentSubType, equipmentFullCode );
                    }
                }
                // iNet server will deliberately not return firmware if the version the instrument is currently
                // at is the same as what was scheduled to be downloaed (instrument's are not allowed to be upgraded
                // to the same version they're already running).
                if ( FirmwareUpgrade == null )
                {
                    Log.Debug( string.Format( "{0}Nothing returned by iNet", funcMsg ) );
                    return false;
                }

                if ( FirmwareUpgrade.Firmware == null )
                {
                    Log.Debug( string.Format( "{0}No firmware returned by iNet", funcMsg ) );
                    _firmwareUpgradeEvent.UpgradeFailure = false;
                    return false;
                }

                Log.Debug( string.Format( "{0}Firmware DeviceType: {1}.", funcMsg, FirmwareUpgrade.EquipmentCode) );
                Log.Debug( string.Format( "{0}Firmware Version: {1}.", funcMsg, FirmwareUpgrade.Version ) );
                Log.Debug( string.Format( "{0}Firmware Size: {1} bytes.", funcMsg, FirmwareUpgrade.Firmware.Length ) );
                Log.Debug( string.Format( "{0}Firmware iNet checksum: \"{1}\".", funcMsg, FirmwareUpgrade.MD5HashToString( FirmwareUpgrade.MD5Hash ) ) );

                if ( FirmwareUpgrade.EquipmentCode != equipmentType || FirmwareUpgrade.EquipmentSubTypeCode != equipmentSubType )
                {
                    string msg = string.Format( "Downloaded firmware is for wrong device type (\"{0}\").  Expected \"{1}\". SubType (\"{2}\"). Expected SubType (\"{3}\")",
                        FirmwareUpgrade.EquipmentCode, equipmentType, FirmwareUpgrade.EquipmentSubTypeCode, equipmentSubType );
                    Log.Error( msg );
                    throw new InstrumentFirmwareUpgradeException( msg );
                }

                Master.Instance.ConsoleService.UpdateAction( ConsoleServiceResources.VERIFYING );

                // Sleep a moment to make sure verification last long enough that it's displayed.
                // (sometimes it's too fast, causing the 'verifying' message to be displayed after-the-fact.)
                Thread.Sleep( 2000 );

                byte[] hash = new MD5CryptoServiceProvider().ComputeHash( FirmwareUpgrade.Firmware );
                
                bool verified = VerifyHash( FirmwareUpgrade.MD5Hash, hash );

                if ( verified )
                {
                    Log.Debug( string.Format( "{0}Firmware successfully downloaded.", funcMsg ) );
                    return true;
                }

                Log.Debug( string.Format( "{0}Verification of MD5 hash failed.", funcMsg ) );
            }

            string errorMsg = string.Format( "{0}Unable to download firwmare after {1} tries", funcMsg, MAX_DOWNLOAD_TRIES );
            Log.Error( errorMsg );
            throw new InstrumentFirmwareUpgradeException( errorMsg );
        }

        #region Get Firmware From Cache

        private bool IsFirmwareCached(string firmwareFilePath)
        {
            if (File.Exists(firmwareFilePath))
            {
                return true;
            }
            return false;
        }

        private FirmwareUpgradeSetting GetFirmwareUpgradeSetting( string equipmentCode, string equipmentSubTypeCode, string version )
        {
            return new FirmwareUpgradeSettingDataAccess().Find(equipmentCode, equipmentSubTypeCode, version);
        }

        private byte[] GetFirmwareFromCache(string firmwareFilePath)
        {
            try
            {
                if (!File.Exists(firmwareFilePath))
                {
                    return null;
                }

                byte[] firmwareBytes;
                using (FileStream fileStream = new FileStream(firmwareFilePath, FileMode.Open, FileAccess.Read))
                {
                    // Read the source file into a byte array.
                    firmwareBytes = new byte[fileStream.Length];
                    int numBytesToRead = (int)fileStream.Length;
                    int numBytesRead = 0;
                    while (numBytesToRead > 0)
                    {
                        // Read may return anything from 0 to numBytesToRead.
                        int numBytes = fileStream.Read(firmwareBytes, numBytesRead, numBytesToRead);

                        // Break when the end of the file is reached.
                        if (numBytes == 0)
                            break;

                        numBytesRead += numBytes;
                        numBytesToRead -= numBytes;
                    }
                }
                return firmwareBytes;
            }
            catch (Exception ex)
            {
                Log.Warning(string.Format("Error while retrieving the firmware file from the SD Card Memory - {0}. Exception: {1} ", firmwareFilePath, ex));
                return null;
            }
        }
        #endregion

        /// <summary>
        /// Looks for the firmware (i.e. hex file) in already-downloaded zip file, and extracts it when found.
        /// </summary>
        private void ExtractFirmware()
        {
            const string funcMsg = "ExtractFirmware: ";  

            Master.Instance.ConsoleService.UpdateAction(ConsoleServiceResources.EXTRACTING);

            Log.Debug(string.Format("{0}Extracting contents of {1} byte zip file", funcMsg, FirmwareUpgrade.Firmware.Length));

            // As we find firmware files in the zip file (there may be more than one), we
            // place them into this dictionary, keyed on their file name. After we extract
            // them all into the dictionary, we'll choose the one we need out of the dictionary.
            Dictionary<string,MemoryStream> firmwares = new Dictionary<string,MemoryStream>();

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
                            Log.Debug(string.Format("{0}ZipEntry Name=\"{1}\", Compressed size={2}, Uncompressed size={3}", funcMsg, zipEntry.Name, zipEntry.CompressedSize, zipEntry.Size ) );  

                            string fileName = Path.GetFileName( zipEntry.Name );

                            if ( fileName == String.Empty )
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
                                    int size = s.Read( buf, 0, (int)buf.Length );

                                    if ( size <= 0 )
                                        break;

                                    unzippedStream.Write( buf, 0, size );
                                }

                                // We assume it's actual firmware if the file has the expected extension for a firmware file.
                                if ( fileName.ToLower().EndsWith( FIRMWARE_FILE_EXTENSION ) )
                                    firmwares[ fileName ] = unzippedStream;

                                // The file containing the registers info is expected to have a specific name.
                                else if ( string.Compare( fileName, REGISTERS_FILE_NAME, true ) == 0 )
                                    _firmwareRegistersXml = Encoding.ASCII.GetString( unzippedStream.GetBuffer(), 0, (int)unzippedStream.Length );
                            }
                        }
                    }
                }
            }
            catch ( Exception e )
            {
                throw new InstrumentFirmwareUpgradeException( "Error unzipping firmware upgrade", e );
            }

            Log.Debug( "Extraction finished." );

            if ( _firmwareRegistersXml == null )
                throw new InstrumentFirmwareUpgradeException( string.Format( "File \"{0}\" not found inside of downloaded upgrade package.", REGISTERS_FILE_NAME ) );

            if ( firmwares.Count == 0 )
                throw new InstrumentFirmwareUpgradeException( string.Format( "No \"{0}\" file found inside of downloaded upgrade package.", FIRMWARE_FILE_EXTENSION ) );

            // Note that ChooseFirmware should not return null.  If it doesn't find what 
            // it needs in the dictionary, it's supposed to throw. We still check the return
            // value just in case future code changes code breaks it.
            _firmwareFile = ChooseFirmware( firmwares );
            if ( _firmwareFile == null )
                throw new InstrumentFirmwareUpgradeException( "Failed to find applicable firmware file in the upgrade package." );
        }

        private MemoryStream ChooseFirmware( Dictionary<string, MemoryStream> firmwares )
        {
            return ( Configuration.DockingStation.Type == DeviceType.MX6 )
                ? ChooseMx6Firmware( firmwares ) : ChooseSingleFirmware( firmwares );
        }

        private MemoryStream ChooseSingleFirmware( Dictionary<string, MemoryStream> firmwares )
        {
            if ( firmwares.Count > 1 )
                throw new InstrumentFirmwareUpgradeException( string.Format( "Multiple \"{0}\" files found inside of downloaded upgrade package, but only a single file was expected.", FIRMWARE_FILE_EXTENSION ) );

            // Return the single firmware that's in the dictionary.
            IEnumerator<string> en = firmwares.Keys.GetEnumerator();
            en.MoveNext();
            Log.Debug( string.Format( "Selected \"{0}\" for upgrading the instrument.", en.Current ) );
            return firmwares[ en.Current ];
        }

        /// <summary>
        /// Determine which .hex file to flash an MX6 instrument with.
        /// </summary>
        /// <remarks>
        /// Starting with MX6 v4.0, there is support for two different LCD displays.
        /// The old, original CSTN display is still supported, and a new TFT display
        /// is also supported.  The zip files for v4.0 and late will alway contain
        /// two hex files... one for the old LCD and one for the new. They are
        /// differentiated by file name:  the hex file for the old LCD will have "CSTN" 
        /// somewhere in the file name. The hex file for the new LCD will contain "TFT"
        /// somewhere in it's name.
        /// </remarks>
        /// <param name="firmwares"></param>
        /// <returns></returns>
        private MemoryStream ChooseMx6Firmware( Dictionary<string, MemoryStream> firmwares ) // INS-3598
        {
            // To determine which type of LCD the instrument has, we look at the hardware
            // version of the instrument.  Empty or "0" is old LCD, and "1" (or higher) is assumed to
            // be the new LCD.
            int hardwareVersion = 0;

            // It should actually be empty for old MX6s, which equates to a version of zero.
            // If non-empty, then parse it to an integer.
            if ( _instHardwareVersion != string.Empty )
            {
                try
                {
                    hardwareVersion = int.Parse( _instHardwareVersion );
                }
                catch ( Exception e )
                {
                    throw new InstrumentFirmwareUpgradeException( string.Format( "Invalid hardware version - \"{0}\".", _instHardwareVersion ), e );
                }
            }

            if ( hardwareVersion < 0 )
                throw new InstrumentFirmwareUpgradeException( string.Format( "Invalid hardware version - \"{0}\".", _instHardwareVersion ) );

            const string CSTN = "CSTN"; // old LCD type
            const string TFT = "TFT";  // new LCD type

            string neededFileName = hardwareVersion > 0 ? TFT : CSTN;

            Log.Debug( "Need to use " + neededFileName + " firmware." );

            bool foundCSTN = false;
            bool foundTFT = false;

            foreach ( string key in firmwares.Keys )
            {
                string fileName = key.ToUpper();

                // If/when we find what we want, we can just return it.
                if ( fileName.Contains( neededFileName ) )
                {
                    Log.Debug( string.Format( "Selected \"{0}\" for upgrading the instrument.", key ) );
                    return firmwares[ key ];
                }

                if ( fileName.Contains( CSTN ) )
                    foundCSTN = true;
                else if ( fileName.Contains( TFT ) )
                    foundTFT = true;
            }

            if ( neededFileName == CSTN )
            {
                // If we find no CSTN or TFT firmware, then perhaps we downloaded an old MX6 upgrade
                // package (pre-4.0) that contains just a single firmware.  If so, the assumption is
                // that file is only CSTN compatible.
                if ( !foundCSTN && !foundTFT )
                    return ChooseSingleFirmware( firmwares );

                throw new InstrumentFirmwareUpgradeException( string.Format( "No {0} file provided in downloaded upgrade package.", CSTN ) );
            }

            // else, neededFileName == TFT
            throw new InstrumentFirmwareUpgradeException( string.Format( "No {0} file provided in downloaded upgrade package.", TFT ) );
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

        private InstrumentData BackupSettings()
        {
            const string funcMsg = "BackupSettings: ";  
            Log.Debug(string.Format("{0}Begin back up of instrument settings", funcMsg));  

            const int maxAttempts = 5;
            int currentAttempt = 0;

            InstrumentData backupData = null;

            _upgraderState = UpgradeState.None;

            while ( currentAttempt++ < maxAttempts && _upgraderState != UpgradeState.BackupCompleted )
            {
                _upgraderState = UpgradeState.None;

                Log.Debug(string.Format("{0}attempt {1} of {2}", funcMsg, currentAttempt, maxAttempts));  

                ThreadPriority originalPriority = Thread.CurrentThread.Priority;
                try
                {
                    // When communicating with instrument, we do so at the highest priority. Otherwise, busy
                    // background threads (such as one uploading to iNet) can cause communications to fail.
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    backupData = _upgrader.BackupSettings( _firmwareRegistersXml ); // this call should not throw.
                }
                finally
                {
                    Thread.CurrentThread.Priority = originalPriority;
                }

                // Did user undock the instrument during the backup? If so,  
                // then undockedError will be set to true by backuperrorHandler.
                if ( _undockedError )
                    throw new InstrumentNotDockedException();

                if ( backupData == null )
                {
                    Log.Error( "BackupSettings returned null" );
                    continue;
                }

                if (_upgraderState == UpgradeState.BackupCompleted)
                {
                    Log.Debug(string.Format("{0}Back up of instrument settings complete", funcMsg));  
                    return backupData;
                }

                Log.Debug(string.Format("{0}_upgraderState.{1}", funcMsg, _upgraderState.ToString()));  
            }

            // If we make it to here, then we failed to backup, even with retries.  throw, including
            // the last exception encountered when trying to do the backup.
            throw new InstrumentFirmwareUpgradeException( string.Format( "Failure backing up settings after {0} attempts", maxAttempts ) );
        }

        private void UpdateFirmware()
        {
            const string funcMsg = "UpdateFirmware: ";  
            Log.Debug(string.Format("{0}Begin instrument firmware update", funcMsg));  

            const int maxAttempts = 5;
            int currentAttempt = 0;

            UpgradeState initialState = _upgraderState;

            while ( ++currentAttempt <= maxAttempts )
            {
                _upgraderState = initialState; // reset on every update attempt.
                _displayedPercentComplete = -1; // reset on every update attempt.

                Log.Debug(string.Format("{0}attempt {1} of {2}", funcMsg, currentAttempt, maxAttempts)); 

                // Note that we pass in new copy of the memorystream.  We need to do this because  
                // the upgrader's update logic has a side effect of Closing the passed-in stream.
                // So, if we were to pass in our member variable ("_firmwareFile") to the update
                // logic, it would end up being unusable on a subsequent attempt.

                ThreadPriority originalPriority = Thread.CurrentThread.Priority;
                try
                {
                    // When communicating with instrument, we do so at the highest priority. Otherwise, busy
                    // background threads (such as one uploading to iNet) can cause communications to fail.
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    _upgrader.UpdateFirmware( new MemoryStream( _firmwareFile.GetBuffer() ) );  // this call should not throw.
                }
                finally
                {
                    Thread.CurrentThread.Priority = originalPriority;
                }

                // Did user undock the instrument during the update? If so,  
                // then undockedError will be set to true by updateerrorHandler.
                if ( _undockedError )
                    throw new InstrumentNotDockedException();

                if (_upgraderState == UpgradeState.UpdateCompleted)
                {
                    Log.Debug(string.Format("{0}Instrument firmware update complete", funcMsg));  
                    return;
                }

                Log.Debug(string.Format("{0}_upgraderState.{1}", funcMsg, _upgraderState.ToString()));  
            }
            throw new InstrumentFirmwareUpgradeException( string.Format( "Failure updating firmware after {0} attempts", maxAttempts ) );
        }

        private void RestoreSettings( InstrumentData backupData )
        {
            const string funcMsg = "RestoreSettings: ";  
            Log.Debug(string.Format("{0}Begin restore of instrument settings", funcMsg));  

            const int maxAttempts = 5;
            int currentAttempt = 0;

            while ( currentAttempt++ < maxAttempts )
            {
                _upgraderState = UpgradeState.UpdateCompleted;

                Log.Debug(string.Format("{0}attempt {1} of {2}", funcMsg, currentAttempt, maxAttempts));  

                ThreadPriority originalPriority = Thread.CurrentThread.Priority;
                try
                {
                    // When communicating with instrument, we do so at the highest priority. Otherwise, busy
                    // background threads (such as one uploading to iNet) can cause communications to fail.
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    _upgrader.RestoreSettings( backupData, _firmwareRegistersXml );  // this call should not throw.
                }
                finally
                {
                    Thread.CurrentThread.Priority = originalPriority;
                }

                // Did user undock the instrument during the restore? If so,  
                // then undockedError will be set to true by restoreerrorHandler.
                if ( _undockedError )
                    throw new InstrumentNotDockedException();

                if (_upgraderState == UpgradeState.RestoreCompleted)
                {
                    Log.Debug(string.Format("{0}Instrument settings restore complete", funcMsg));  
                    return;
                }

                Log.Debug(string.Format("{0}_upgraderState.{1}", funcMsg, _upgraderState.ToString()));  
            }
            throw new InstrumentFirmwareUpgradeException( string.Format( "Failure restoring settings after {0} attempts", maxAttempts ) );
        }

        private void PromptForTurnOn()
        {
            if ( Configuration.DockingStation.Type != DeviceType.GBPLS )
                return;

            const string funcMsg = "PromptForTurnOn: ";  
            Log.Debug(string.Format("{0}Connect to instrument", funcMsg));  

            while ( true )
            {
                Master.Instance.ConsoleService.UpdateState( ConsoleState.PleaseTurnOn );

                using ( InstrumentController instCtrlr = SwitchService.CreateInstrumentController() )
                {
                    const int maxPingAttempts = 5;
                    int pingAttempt = 0;

                    try
                    {
                        instCtrlr.Initialize( InstrumentController.Mode.Batch );

                        // Keep reading the instrument type until it successfully responds 5 times in a row.
                        for ( pingAttempt = 1; pingAttempt <= maxPingAttempts; pingAttempt++ )
                        {
                            Thread.Sleep( 1000 );

                            if ( !Controller.IsDocked() ) // monitor for user undocking the instrument during sleeps
                                throw new InstrumentNotDockedException();

                            instCtrlr.GetInstrumentType();

                            Log.Debug( string.Format( "{0}GetInstrumentType {1} of {2} successful.", funcMsg, pingAttempt, maxPingAttempts ) );  
                        }

                        Master.Instance.ConsoleService.UpdateState( ConsoleState.UpgradingInstrumentFirmware );

                        return;
                    }
                    catch ( InstrumentNotDockedException )
                    {
                        Log.Debug(string.Format("{0}Caught InstrumentNotDockedException", funcMsg));  
                        Master.Instance.ConsoleService.UpdateState(ConsoleState.UpgradingInstrumentFirmware);
                        throw;
                    }
                    catch ( CommunicationAbortedException )
                    {
                        Log.Debug(string.Format("{0}Caught CommunicationAbortedException", funcMsg));  
                        Master.Instance.ConsoleService.UpdateState(ConsoleState.UpgradingInstrumentFirmware);
                        throw;
                    }
                    catch ( Exception ex )
                    {
                        Log.Error( ex );
                        if ( pingAttempt >= maxPingAttempts )
                            throw;
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether or not an instrument can be "safely" upgraded if
        /// it has a battery of the specified type that's that at the specified.
        /// current voltage level.
        /// </summary>
        /// <param name="batteryCode">An iNet battery code, e.g. "BP006"</param>
        /// <param name="voltage">Battery's current voltage, in millivolts.</param>
        /// <returns></returns>
        //static public bool IsAboveMinimumVoltage( string batteryCode, int voltage )
        //{
        //    // Engineering (Steve Tidd) has recommended the 
        //    // following minimum voltages (see INS-2640)...
        //    // MX6 Lithium:  3.80 V
        //    // MX6 Alkaline: 3.70 V
        //    // MX4 Lithium:  3.80 V
        //    // MX4 Alkaline: 3.75 V
        //    // GBPro:        2.00 V
        //    // GBPlus:       2.70 V

        //    switch ( batteryCode )
        //    {
        //        case BatteryCode.MX4Alkaline:
        //            return voltage >= 3750;

        //        // MX4 rechargeables...
        //        case BatteryCode.MX4Lithium1:
        //        case BatteryCode.MX4Lithium2:
        //        case BatteryCode.MX4Lithium3:
        //        case BatteryCode.MX4Lithium4:
        //        case BatteryCode.MX4Lithium5:
        //        case BatteryCode.MX4Lithium6:
        //        case BatteryCode.MX4Lithium7:
        //        case BatteryCode.MX4Lithium8:
        //            return voltage >= 3800;

        //        case BatteryCode.MX6Alkaline:
        //            return voltage >= 3700;

        //        // MX6 rechargeables...
        //        case BatteryCode.MX6Lithium2Cell:
        //        case BatteryCode.MX6Lithium3Cell:
        //            return voltage >= 3800;

        //        default:

        //            switch ( Configuration.DockingStation.Type )
        //            {
        //                case DeviceType.GBPLS: 
        //                    return voltage >= 2700;
        //                case DeviceType.GBPRO:
        //                    return voltage >= 2000;
        //                case DeviceType.TX1:
        //                    return true; // TODO ?? what's the threshold?
        //            }
        //            break;
        //    }

        //    // If we have no specific rules for a particular instrument & battery combo,
        //    // we can do nothing more except return true to indicate "go for it, and good luck!"
        //    return true;
        //}

        #endregion Methods

        #region Event handlers

        private void Updater_Start( object sender, EventArgs e )
        {
            lock ( _stateLocker )
            {
                Log.Debug( "Updater_Start: " + _upgraderState.ToString() );

                switch ( _upgraderState )
                {
                    case UpgradeState.None:
                    case UpgradeState.RestoreCompleted:
                    case UpgradeState.RestoreError:
                        _upgraderState = UpgradeState.BackupStarted;
                        Log.Debug( "Updater_Start: Backup started" );
                        break;
                    case UpgradeState.BackupCompleted:
                    case UpgradeState.BackupError:
                        _upgraderState = UpgradeState.UpdateStarted;
                        Log.Debug( "Updater_Start: Update started" );
                        break;
                    case UpgradeState.UpdateCompleted:
                    case UpgradeState.UpdateError:
                        _upgraderState = UpgradeState.RestoreStarted;
                        Log.Debug( "Updater_Start: Restore started" );
                        break;
                }

                UpdateAction();
            }
        }

        private void Updater_Complete( object sender, UpdateProgressEventArgs e )
        {
            lock ( _stateLocker )
            {
                Log.Debug( "Updater_Complete: " + _upgraderState.ToString() );
                switch ( _upgraderState )
                {
                    case UpgradeState.BackupStarted:
                        _upgraderState = UpgradeState.BackupCompleted;
                        //_backupCompleted = true;
                        Log.Debug( "Updater_Complete: Backup Complete" );
                        break;
                    case UpgradeState.UpdateStarted:
                        _upgraderState = UpgradeState.UpdateCompleted;
                        //_updateCompleted = true;
                        Log.Debug( "Updater_Complete: Update Complete" );
                        break;
                    case UpgradeState.RestoreStarted:
                        _upgraderState = UpgradeState.RestoreCompleted;
                        //_restoreCompleted = true;
                        Log.Debug( "Updater_Complete: Restore Complete" );
                        break;
                }
                UpdateAction(e);
            }
        }


        private void Updater_Progress( object sender, UpdateProgressEventArgs e )
        {
            UpdateAction( e );
        }

        private void Updater_Error( object sender, UpdateErrorEventArgs e )
        {
            string message = string.Empty;

            lock ( _stateLocker )
            {
                Log.Debug( "Updater_Error: " + _upgraderState.ToString() );

                switch ( _upgraderState )
                {
                    case UpgradeState.RestoreStarted:
                        _upgraderState = UpgradeState.RestoreError;
                        message = "RESTORE ERROR: ";
                        break;
                    case UpgradeState.BackupStarted:
                        _upgraderState = UpgradeState.BackupError;
                        message = "BACKUP ERROR: ";
                        break;
                    case UpgradeState.UpdateStarted:
                        _upgraderState = UpgradeState.UpdateError;
                        message = "UPDATE ERROR: ";
                        break;
                }
            }
            string errorMessage = message + e.Exception.ToString();
            Log.Error( errorMessage );

            if ( e.Exception is CommunicationAbortedException )
                _undockedError = true;

            // Make sure inet gets notified of the error.  We wrap all errors within a
            // InstrumentFirmwareUpgradeException so that we can parse for it on iNet server.
            _firmwareUpgradeEvent.Errors.Add( new DockingStationError( new InstrumentFirmwareUpgradeException( errorMessage ), DockingStationErrorLevel.Warning, _instSerialNumber ) );
        }

        private void UpdateAction() { UpdateAction( null ); }

        private void UpdateAction( UpdateProgressEventArgs e )
        {
            List<string> actionMsgs = null;
            string action = string.Empty;

            switch ( _upgraderState )
            {
                case UpgradeState.BackupStarted:
                case UpgradeState.BackupCompleted:
                case UpgradeState.BackupError:
                    action = ConsoleServiceResources.BACKING_UP_SETTINGS;
                    break;

                case UpgradeState.RestoreStarted:
                case UpgradeState.RestoreError: // Made it to the very end very end with an error?  Remove action messages.
                case UpgradeState.RestoreCompleted: // Made it to the very end with success?  Remove action messages.
                    action = ConsoleServiceResources.RESTORING_SETTINGS;
                    break;

                case UpgradeState.UpdateStarted:
                case UpgradeState.UpdateCompleted:
                case UpgradeState.UpdateError: // Error trying to do the actual upgrade?  Then remove action messages; we're about to abort and not bother with a restore.
                    action = ConsoleServiceResources.PROGRAMMING;
                    break;
            }

            if ( e == null ) // null when first started.
            {
                actionMsgs = new List<string>();
                actionMsgs.Add( action );
            }
            else
            {
                // HandleUpdateProgress will handle determining the percent complete
                // we need to show.
                actionMsgs = HandleUpdateProgress( action, e );
            }
            if ( actionMsgs != null )
                Master.Instance.ConsoleService.UpdateAction( actionMsgs.ToArray() );
        }

        private List<string> HandleUpdateProgress( string action, UpdateProgressEventArgs e )
        {
            int roundedPercentComplete = _displayedPercentComplete;

            List<string> actionMsgs = null;

            // Handle Backups and Restores separately.
            if ( _upgraderState != UpgradeState.UpdateStarted )
            {
                // If e is null, then back/restore must just be starting.
                if ( e != null )
                {
                    actionMsgs = new List<string>();
                    actionMsgs.Add( action );
                    actionMsgs.Add( string.Format( "{0}%", e.PercentComplete ) );
                }
            }
            else // upgraderState == UpgradeState.UpdateStarted )
            {
                if ( e != null )
                {
                    // Round percent complete to lowest integer.  e.g 12%, 15%, 18% all get rounded to down to 10%.
                    // We do this so that we don't constantly update the LCD for every percentage change (or, worse
                    // call for LCD update when the percentage hasn't even yet changed), as updating the LCD
                    // 'action' messages slows the overall update speed down.
                    roundedPercentComplete = (int)( Math.Floor( e.PercentComplete * 0.1 ) * 10 );

                    if ( _displayedPercentComplete != roundedPercentComplete )
                    {
                        actionMsgs = new List<string>();
                        actionMsgs.Add( action );
                        actionMsgs.Add( string.Format( "{0}%", roundedPercentComplete ) );
                    }
                }
            }

            // The following logic is just to only bother formatting & logging messages when logging is set to Debug.
            if ( Log.Level >= LogLevel.Debug )
            {
                // Are we in the middle of programming the instrument?
                bool updatingFirmware = ( _upgraderState == UpgradeState.UpdateStarted )
                    && ( e != null )
                    && ( e.StatusMessage == MessageEnum.WritingBluetooth || e.StatusMessage == MessageEnum.WritingWireless || e.StatusMessage == MessageEnum.WritingFirmware );

                // When in the middle of programming the instrument, we only log whenever our _loggedPercentComplete changes. This should be 
                // only every 10% increment so that we don't log too much.  e.g., log when when hit 10%, then not again until 20%, etc.
                if ( !updatingFirmware || ( updatingFirmware && ( roundedPercentComplete != _displayedPercentComplete ) ) )
                {
                    string actionsAppended = ( e != null ) ? e.StatusMessage.ToString() : string.Empty;

                    if ( actionMsgs != null )
                    {
                        foreach ( string msg in actionMsgs )
                        {
                            if ( actionsAppended.Length > 0 ) actionsAppended += ", ";
                            actionsAppended += msg;
                        }
                    }
                    Log.Debug( string.Format( "UPDATEACTION: {0}, {1}", _upgraderState.ToString(), actionsAppended ) );
                }
            }

            _displayedPercentComplete = roundedPercentComplete;

            return actionMsgs;
        }

        #endregion Event handlers

        #region IModbusTracer

        /// <summary>
        /// Delegate for AbortRequest.
        /// </summary>
        /// <returns></returns>
        private bool IsNotDocked() { return Controller.IsDocked() == false; }

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.DebugLevel property.
        /// If tracing isn't enabled, then set return a debuglevel of Warning.
        /// That will trigger the low level driver to not bother with the effort
        /// of formatting up all of it's debug messages just to not have them 
        /// even outputted.
        /// </summary>
        public DebugLevel DebugLevel
        {
            get { return Log.Level >= LogLevel.Trace ? DebugLevel.Debug : DebugLevel.Warning; }
        }

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.WriteError
        /// </summary>
        /// <param name="msg"></param>
        public void WriteError( string msg )
        {
            if ( DebugLevel >= DebugLevel.Error )
                Log.Error( msg );
        }

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.WriteDebug
        /// </summary>
        /// <param name="msg"></param>
        public void WriteWarning( string msg )
        {
            if ( DebugLevel >= DebugLevel.Warning )
                Log.Warning( msg );
        }

        /// <summary>
        /// Implementation of ISC.Instrument.Driver.IModbusTracer.WriteDebug
        /// </summary>
        /// <param name="msg"></param>
        public void WriteDebug( string msg )
        {
            if ( DebugLevel >= DebugLevel.Debug )
                Log.Debug( msg );
        }

        #endregion IModbusTracer 

    }  // end-class FirmwareUpgradeOperation

   

    public class InstrumentFirmwareUpgradeException : ApplicationException
    {
        public InstrumentFirmwareUpgradeException( string message ) : base( message ) {}

        public InstrumentFirmwareUpgradeException( string message, Exception inner ) : base( message, inner ) {}
    }
}
