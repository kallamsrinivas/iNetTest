using System;
using System.Collections.Generic;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a NothingAction on a docking station.
	/// </summary>
	public class NothingAction : DockingStationAction
	{
		/// <summary>
		/// Creates a new instance of NothingAction class.
		/// </summary>
		public NothingAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="nothingAction"></param>
        public NothingAction( NothingAction nothingAction )
            : base( nothingAction )
        {
        }
	}

	/////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a DiagnosticAction on a docking station.
	/// </summary>
	public class DiagnosticAction : DockingStationAction
	{
        public const string LAST_RUN_DETAILS_FILE_NAME = "Last-Diagnostics.txt";

		/// <summary>
		/// Creates a new instance of DiagnosticAction class.
		/// </summary>
		public DiagnosticAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="diagnosticAction"></param>
        public DiagnosticAction( DiagnosticAction diagnosticAction ) : base( diagnosticAction ) {}
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a InteractiveDiagnosticAction on a docking station.
	/// </summary>
	public class InteractiveDiagnosticAction : DockingStationAction
	{
        public const string LAST_RUN_DETAILS_FILE_NAME = "Last-Interactive-Diagnostics.txt";

        public bool DiagnoseKeypad { get; set; }  // SGF  03-Jun-2011  INS-1730
        public bool DiagnoseLcd { get; set; }  // SGF  03-Jun-2011  INS-1730
        public bool DiagnoseLeds { get; set; }
        public bool DiagnoseBuzzer { get; set; }
        public bool DiagnoseLidSwitches { get; set; }
        public bool DiagnoseIGas { get; set; }
        public bool DiagnoseCradleGasFlow { get; set; }  // SGF  03-Jun-2011  INS-1730
        public bool DiagnoseFlowRate { get; set; }  // SGF  03-Jun-2011  INS-1730
        public bool DiagnoseInstrumentDetection { get; set; }
        public bool DiagnoseInstrumentCommunication { get; set; }
        public bool DiagnoseBatteryCharging { get; set; }

		/// <summary>
		/// Creates a new instance of InteractiveDiagnosticAction class.
		/// </summary>
		public InteractiveDiagnosticAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="interactiveDiagnosticAction"></param>
        public InteractiveDiagnosticAction( InteractiveDiagnosticAction interactiveDiagnosticAction ) : base( interactiveDiagnosticAction )
        {
            this.DiagnoseKeypad = interactiveDiagnosticAction.DiagnoseKeypad; // SGF  03-Jun-2011  INS-1730
            this.DiagnoseLcd = interactiveDiagnosticAction.DiagnoseLcd; // SGF  03-Jun-2011  INS-1730
            this.DiagnoseLeds = interactiveDiagnosticAction.DiagnoseLeds;
            this.DiagnoseBuzzer = interactiveDiagnosticAction.DiagnoseBuzzer;
            this.DiagnoseLidSwitches = interactiveDiagnosticAction.DiagnoseLidSwitches;
            this.DiagnoseIGas = interactiveDiagnosticAction.DiagnoseIGas;
            this.DiagnoseCradleGasFlow = interactiveDiagnosticAction.DiagnoseCradleGasFlow; // SGF  03-Jun-2011  INS-1730
            this.DiagnoseFlowRate = interactiveDiagnosticAction.DiagnoseFlowRate; // SGF  03-Jun-2011  INS-1730
            this.DiagnoseInstrumentDetection = interactiveDiagnosticAction.DiagnoseInstrumentDetection;
            this.DiagnoseInstrumentCommunication = interactiveDiagnosticAction.DiagnoseInstrumentCommunication;
            this.DiagnoseBatteryCharging = interactiveDiagnosticAction.DiagnoseBatteryCharging;
        }
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a TroubleshootAction on a docking station.
	/// </summary>
	public class TroubleshootAction : DockingStationAction
	{
		public TroubleshootAction() {}

		/// <summary>
		/// Copy constructor.
		/// </summary>
		public TroubleshootAction( TroubleshootAction troubleshootAction )
			: base( troubleshootAction )
		{
		}
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a CylinderPressureResetAction on a docking station.
	/// </summary>
	public class CylinderPressureResetAction : DockingStationAction
	{
		/// <summary>
		/// If true, then this action is occurring due to a SettingsUpdate that was just performed.
		/// Otherwise, false (the default).
		/// This value will be passed on to the SettingsRead that will follow.
		/// </summary>
		public bool PostUpdate { get; set; }

		/// <summary>
		/// If this action is occurring after a SettingsUpdate, then this property will contain
		/// the ID of the settings used during that event.  Otherwise, will be Nullid.
		/// This value will be passed on to the ReadEvent that will follow.
		/// </summary>
		public long SettingsRefId { get; set; }

		public CylinderPressureResetAction() { }

		/// <summary>
		/// Copy constructor.
		/// </summary>
		public CylinderPressureResetAction( CylinderPressureResetAction cylPressureResetAction )
			: base( cylPressureResetAction )
		{
			this.PostUpdate = cylPressureResetAction.PostUpdate;
			this.SettingsRefId = cylPressureResetAction.SettingsRefId;
		}
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// </summary>
	public class UploadDebugLogAction : DockingStationAction
	{
		public UploadDebugLogAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="uploadDebugLogAction"></param>
        public UploadDebugLogAction( UploadDebugLogAction uploadDebugLogAction )
            : base( uploadDebugLogAction )
        {
        }
	}

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// </summary>
    public class UploadDatabaseAction : DockingStationAction
    {
        public UploadDatabaseAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="uploadDebugLogAction"></param>
        public UploadDatabaseAction( UploadDatabaseAction uploadDatabaseAction )
            : base( uploadDatabaseAction )
        {
        }
    }

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a SettingsUpdateAction on a docking station.
	/// </summary>
	public class SettingsUpdateAction : DockingStationAction
	{
        /// <summary>
        /// If set to true, then the update operation will assume Settings
        /// property is already populated with the settings that should be used.
        /// If set to false, then the update operation will load the settings from
        /// the database.
        /// </summary>
        public bool UseDockingStation { get; set; }

		private DockingStation _dockingStation;

		/// <summary>
		/// Creates a new instance of SettingsUpdateAction class.
		/// </summary>
		public SettingsUpdateAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="settingsUpdateAction"></param>
        public SettingsUpdateAction( SettingsUpdateAction settingsUpdateAction )
            : base( settingsUpdateAction )
        {
            this.UseDockingStation = settingsUpdateAction.UseDockingStation;
            this.DockingStation = (DockingStation)settingsUpdateAction.DockingStation.Clone();
        }

		/// <summary>
		/// Gets or sets the docking station settings used to do the update.
		/// </summary>
		public DockingStation DockingStation
		{
			get
			{
				if ( _dockingStation == null ) _dockingStation = new DockingStation();
				return _dockingStation;
			}
			set { _dockingStation = value; }
		}
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Defines functionality to define a SettingsReadAction on a docking station.
	/// </summary>
	public class SettingsReadAction : DockingStationAction
	{
        /// <summary>
        /// If true, then this Read is occurring due to an Update that was just performed.
        /// Otherwise, false (the default).
        /// </summary>
        public bool PostUpdate { get; set; }

        /// <summary>
        /// If this ReadEvent is occurring after an UpdateEvent, then this property will contain
        /// the ID of the settings used during the UpdateEvent.  Otherwise, will be Nullid.
        /// </summary>
        /// <remarks>The purpose of this is for uploading of it to iNet.</remarks>
        public long SettingsRefId { get; set; }

        // Explicitly set to empty to default to reading only 'changed' iGas cards.
        // Empty array denotes "uninitialized" and will cause SettingsReadOperation to correctly
        // re-initialize it to number of gas ports (3 or 6). We can do that here, as the class
        // does not have access to the information.
        // Note that we don't just default it to null, as null has a special meaning of
        // "read all cards" (see SettingsReadOperation.GetCylinders).
        private bool[] _changedSmartCards = new bool[0];

		/// <summary>
		/// Creates a new instance of SettingsReadAction class.
		/// </summary>
		public SettingsReadAction()
		{
            SettingsRefId = DomainModelConstant.NullId;
		}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="settingsReadAction"></param>
        public SettingsReadAction(SettingsReadAction settingsReadAction ) : base( settingsReadAction )
        {
            this.PostUpdate = settingsReadAction.PostUpdate;

            this.SettingsRefId = settingsReadAction.SettingsRefId;

            this.ChangedSmartCards = settingsReadAction.ChangedSmartCards;
        }

        /// <summary>
        /// If not null, then only 'changed' iGas cards should be read, as indicated by the
        /// array.  This is the default.
        /// If null, then ALL installed iGas cards should be read.
        /// </summary>
        public bool[] ChangedSmartCards
        {
            get { return _changedSmartCards; }
            set
            {
                if ( value != null )
                {
                    // Make a copy of the array
                    _changedSmartCards = new bool[value.Length];
                    for ( int i = 0; i < this.ChangedSmartCards.Length; i++ )
                        _changedSmartCards[i] = value[i];
                }
                else
                    _changedSmartCards = null;
            }
        }
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a SerializationAction on a docking station.
	/// </summary>
	public class SerializationAction : DockingStationAction
	{
        private bool           _deserialize;
		private DockingStation _dockingStation;

        private void Init()
        {
            Trigger = TriggerType.Forced; // serializations are always forced (they're never scheduled)
            ShouldSave = true; // SGF  06-May-2011  INS-3563
        }

		/// <summary>
		/// Creates a new instance of SerializationAction class.
		/// </summary>
		public SerializationAction()
		{
            Init();
		}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="serializationAction"></param>
        public SerializationAction( SerializationAction serializationAction ) : base( serializationAction )
        {
            Init();
            this.DockingStation = (DockingStation)serializationAction.DockingStation.Clone();
            this.Deserialize = serializationAction.Deserialize;
            this.ShouldSave = serializationAction.ShouldSave; // SGF  06-May-2011  INS-3563
        }

		/// <summary>
		/// Gets or sets the docking station.
		/// </summary>
		public DockingStation DockingStation
		{
			get
			{
				if ( _dockingStation == null )
					_dockingStation = new DockingStation();

				return _dockingStation;
			}
			set
			{
				_dockingStation = value;
			}
		}

        public bool Deserialize
        {
            get
            {
                return _deserialize;
            }
            set
            {
                _deserialize = value;
            }
        }

        public bool ShouldSave { get; set; } // SGF  29-Apr-2011  INS-3563  Added property
    }

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define an UnavailableAction on a docking station.
	/// </summary>
	public class UnavailableAction : DockingStationAction, INotificationAction
	{
		/// <summary>
		/// Creates a new instance of UnavailableAction class.
		/// </summary>
		public UnavailableAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="unavailableAction"></param>
        public UnavailableAction( UnavailableAction unavailableAction )
            : base( unavailableAction )
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="e">The exception that caused the docking station to go "Unavailable"</param>
        public UnavailableAction( Exception e )
        {
            this.Exception = e;
        }

        /// <summary>
        /// The exception that caused the docking station to go "Unavailable"
        /// </summary>
        public Exception Exception { get; private set; }
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define an LeakUnavailableAction on a docking station.
	/// </summary>
    public class LeakUnavailableAction : UnavailableAction, INotificationAction
	{
		/// <summary>
		/// Creates a new instance of LeakUnavailableAction class.
		/// </summary>
		public LeakUnavailableAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="leakUnavailableAction"></param>
        public LeakUnavailableAction( LeakUnavailableAction leakUnavailableAction ) : base( leakUnavailableAction )
        {
        }
	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a ResourceUnavailableAction on a docking station.
	/// </summary>
    public class ResourceUnavailableAction : DockingStationAction, INotificationAction
	{
        List<string> _consoleMessages; // SGF  20-Feb-2013  INS-3821

		/// <summary>
		/// Creates a new instance of ResourceUnavailableAction class.
		/// </summary>
		public ResourceUnavailableAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="resourceUnavailableAction"></param>
        public ResourceUnavailableAction( ResourceUnavailableAction resourceUnavailableAction ) : base( resourceUnavailableAction )
        {
        }

        /// <summary>
        /// For this constructor, the passed in message is used to initialize both the Messages and ConsoleMessages properties.
        /// </summary>
        /// <param name="message">Used for both Messages and ConsoleMessages</param>
        public ResourceUnavailableAction( string message )
        {
            Messages.Add( message );
            ConsoleMessages.Add( message );
        }

        public ResourceUnavailableAction( List<string> messages, List<string> consoleMessages )
        {
            Messages.AddRange( messages );
            ConsoleMessages.AddRange( consoleMessages );
        }

        // SGF  20-Feb-2013  INS-3821
        /// <summary>
        /// Messages determined by the iNet DS that need to be displayed
        /// on its LCD when executing this action.
        /// </summary>
        public List<string> ConsoleMessages
        {
            get
            {
                if (_consoleMessages == null)
                    _consoleMessages = new List<string>();
                return _consoleMessages;
            }
            set
            {
                _consoleMessages = value;
            }
        }
	}

    public class UnsupportedCylinderAction : DockingStationAction, INotificationAction
    {
		public GasEndPoint GasEndPoint { get; private set; }

        // We deliberately do not allow the default constructor.
        // The intent is an InstalledCylinder must ALWAYS be specified.
		/// <summary>
        /// Creates a new instance of UnsupportedCylinderAction class.
		/// </summary>
        //public UnsupportedCylinderAction()
        //{
        //    // Do nothing
        //}

        /// <summary>
        /// 
        /// </summary>
		/// <param name="gasEndPoint">This will be cloned.</param>
        public UnsupportedCylinderAction( GasEndPoint gasEndPoint )
            : base()
        {
			Log.Assert( gasEndPoint != null, "null GasEndPoint in UnsupportedCylinderAction.ctor " );
			GasEndPoint = (GasEndPoint)gasEndPoint.Clone();
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
		/// <param name="unsupportedGasAction">Its gasEndPoint will be cloned.</param>
        public UnsupportedCylinderAction( UnsupportedCylinderAction unsupportedGasAction )
            : base( unsupportedGasAction )
        {
            Log.Assert( unsupportedGasAction.GasEndPoint != null, "null InstalledCylinder in UnsupportedCylinderAction.copy_ctor " );
			GasEndPoint = (GasEndPoint)unsupportedGasAction.GasEndPoint.Clone();
        }
    }

    /// <summary>
    /// Dock returns BadPumpTubingDetectedAction when kinked pump tubing is detected
    /// </summary>
    public class BadPumpTubingDetectedAction : DockingStationAction, INotificationAction
    {
        public string DockingStationSN { get; private set; }

        // We deliberately do not allow the default constructor.
        // The intent is an Docking Station's serial number must ALWAYS be specified.
        /// <summary>
        /// Creates a new instance of BadPumpTubingDetectedAction class.
        /// </summary>
        //public BadPumpTubingDetectedAction()
        //{
        //    // Do nothing
        //}

        /// <summary>
        /// Instantiates BadPumpTubingDetectedAction with serial number of the dock
        /// </summary>
        /// <param name="dockingStationSn">Serial number of the docking station.</param>
        public BadPumpTubingDetectedAction(string dockingStationSn)
            : base()
        {
            Log.Assert(dockingStationSn != null, "null docking station's serial number in BadPumpTubingDetectedAction.ctor ");
            DockingStationSN = dockingStationSn;
        }
    }
    
    /// <summary>
    /// This action initiates the popping and disposal of the oldest message 
    /// currently stored on the iNet upload queue.
    /// </summary>
    public class PopQueueAction : DockingStationAction
    {
        public PopQueueAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="popQueueAction"></param>
        public PopQueueAction( PopQueueAction popQueueAction )
            : base( popQueueAction )
        {
        }

    } // end-class PopQueueAction

    /// <summary>
    /// This action initiates returning the docking station to its factory defaults.
    /// </summary>
    public class FactoryResetAction : DockingStationAction
    {
        /// <summary>
        /// Default constructor.  By default, FullReset is set to true.
        /// </summary>
        public FactoryResetAction()
        {
            FullReset = true; // By default, a factory reset action truly does a factory reset.
        }

        /// <summary>
        /// </summary>
        /// <param name="fullReset">
        /// If false, then doing a factory reset only deletes the databases, but does not reset the configuration settings.
        /// If true (the default), then doing a factory reset delets the databasese and also resets the configuration settings to factory defaults.
        /// </param>
        public FactoryResetAction( bool fullReset )
        {
            FullReset = fullReset; // By default, a factory reset action truly does a factory reset.
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="factoryResetAction"></param>
        public FactoryResetAction( FactoryResetAction factoryResetAction )
            : base( factoryResetAction )
        {
            FullReset = factoryResetAction.FullReset;
        }

        /// <summary>
        /// If false, then doing a factory reset only deletes the databases, but does not reset the configuration settings.
        /// If true (the default), then doing a factory reset delets the databasese and also resets the configuration settings to factory defaults.
        /// </summary>
        /// TODO: Shouldn't this be in the FactoryResetAction class and not in this parent class?  - JMP, 3/20/2014.
        public bool FullReset { get; set; }  // SGF  29-Apr-2011  INS-3563  adding property to modify behavior in execute

    } // end-class FactoryResetAction


    /// <summary>
    /// This action initiates an upgrade of the docking station.
    /// The upgrade is to be downloaded from iNet.
    /// </summary>
    public class FirmwareUpgradeAction : DockingStationAction
    {
        public FirmwareUpgradeAction( FirmwareUpgrade firmwareUpgrade )
        {
            FirmwareUpgrade = firmwareUpgrade;
        }

        public FirmwareUpgradeAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="firmwareUpgradeAction"></param>
        public FirmwareUpgradeAction( FirmwareUpgradeAction firmwareUpgradeAction )
            : base( firmwareUpgradeAction )
        {
            FirmwareUpgrade = firmwareUpgradeAction.FirmwareUpgrade;
        }

        public FirmwareUpgrade FirmwareUpgrade { get; set; }

    } // end-class FirmwareUpgradeAction


    public class MaintenanceAction : DockingStationAction
    {
        public MaintenanceAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="factoryResetAction"></param>
        public MaintenanceAction( MaintenanceAction maintenanceAction )
            : base( maintenanceAction )
        {
        }
    }

    public class ExchangeStatusAction : DockingStationAction
    {
        public ExchangeStatusAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="exchangeStatusAction"></param>
        public ExchangeStatusAction(ExchangeStatusAction exchangeStatusAction)
            : base(exchangeStatusAction)
        {
        }
    }

    public class RebootAction : DockingStationAction
    {
        public RebootAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="factoryResetAction"></param>
        public RebootAction( RebootAction rebootAction )
            : base( rebootAction )
        {
        }
    }

} // end-namespace
