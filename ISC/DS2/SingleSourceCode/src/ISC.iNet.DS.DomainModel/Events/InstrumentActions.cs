using System.Collections.Generic;

namespace ISC.iNet.DS.DomainModel
{
    public class InstrumentGasAction : InstrumentAction
    {
		private List<GasEndPoint> _gasEndPoints;
        private List<string> _componentCodes; // SGF  03-Nov-2010  Single Sensor Cal and Bump

        private bool _isSensorFailureModeEnabled;
        private bool _isSCSensorFailureModeEnabled;

        /// <summary>
        /// Creates a new instance of InstrumentBumpTestAction class.
        /// </summary>
        public InstrumentGasAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentAction"></param>
        public InstrumentGasAction( InstrumentGasAction instrumentGasAction )
            : base( instrumentGasAction ) 
        {
            // Loop through the contained objects calling clone for each one to fill the empty list.
            foreach ( GasEndPoint gasEndPoint in instrumentGasAction.GasEndPoints )
				this.GasEndPoints.Add( (GasEndPoint)gasEndPoint.Clone() );

            // SGF  05-Nov-2010  Single Sensor Cal and Bump
            this.ComponentCodes = new List<string>( instrumentGasAction.ComponentCodes );
        }

        /// <summary>
        /// Gets or sets the list of gas end points associated with a docking station.
        /// </summary>
		public List<GasEndPoint> GasEndPoints
        {
            get
            {
                if ( _gasEndPoints == null )
					_gasEndPoints = new List<GasEndPoint>();

                return _gasEndPoints;
            }
            set
            {
                _gasEndPoints = value;
            }
        }

        /// <summary>
        /// Gets or sets the list of sensor types that are to be exercised in this action.
        /// </summary>
        public List<string> ComponentCodes // SGF  03-Nov-2010  Single Sensor Cal and Bump
        {
            get
            {
                if ( _componentCodes == null ) _componentCodes = new List<string>();
                return _componentCodes;
            }
            set { _componentCodes = value; }
        }
        
        // SGF  06-Jun-2011  INS-1735 -- Moving to this class to make available to inherited classes.
        // SGF  15-Jun-2010  DSW-470 -- New
        public SensorGasResponse GetSensorGasResponse(InstrumentGasResponseEvent igrEvent, Sensor sensor)
        {
            for (int i = 0; i < igrEvent.GasResponses.Count; i++)
            {
                SensorGasResponse sgr = (SensorGasResponse)igrEvent.GasResponses[i];
                if (sensor.Uid == sgr.Uid)
                    return sgr;
            }
            return null;
        }

        /// <summary>
        /// Gets or Sets the Sensor Failure Mode Status
        /// </summary>
        public bool IsSensorFailureModeEnabled
        {
            set
            {
                _isSensorFailureModeEnabled = value;
            }
            get
            {
                return _isSensorFailureModeEnabled;
            }
        }

        /// <summary>
        /// Gets or Sets the Sensor Failure Mode Status
        /// </summary>
        public bool IsSCSensorFailureModeEnabled
        {
            set
            {
                _isSCSensorFailureModeEnabled = value;
            }
            get
            {
                return _isSCSensorFailureModeEnabled;
            }
        }
    }

    public class InstrumentBumpTestAction : InstrumentGasAction
	{
		/// <summary>
		/// Creates a new instance of InstrumentBumpTestAction class.
		/// </summary>
		public InstrumentBumpTestAction() { }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentAction"></param>
        public InstrumentBumpTestAction( InstrumentBumpTestAction instrumentBumpTestAction )
            : base( instrumentBumpTestAction ) 
        {
        }

	} // end-class InstrumentBumpTestAction

    public class InstrumentDiagnosticAction : InstrumentAction
	{
		/// <summary>
		/// Creates a new instance of InstrumentDiagnosticAction class.
		/// </summary>
		public InstrumentDiagnosticAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentDiagnosticAction"></param>
        public InstrumentDiagnosticAction( InstrumentDiagnosticAction instrumentDiagnosticAction ) : base( instrumentDiagnosticAction )
        {
        }

        public List<CriticalError> criticalErrorsList { get; set; }
    }

    public class InstrumentCalibrationAction : InstrumentGasAction
	{
		/// <summary>
		/// Creates a new instance of InstrumentCalibrationAction class.
		/// </summary>
		public InstrumentCalibrationAction()
		{
			// Do nothing
		}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentCalibrationAction"></param>
        public InstrumentCalibrationAction( InstrumentCalibrationAction instrumentCalibrationAction ) : base( instrumentCalibrationAction )
        {
        }
	}

    public class InstrumentPurgeAction : InstrumentGasAction
    {
        /// <summary>
        /// Creates a new instance of InstrumentPurgeAction class.
        /// </summary>
        public InstrumentPurgeAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="InstrumentPurgeAction"></param>
        public InstrumentPurgeAction(InstrumentPurgeAction instrumentPurgeAction)
            : base(instrumentPurgeAction)
        {
        }
    }

	public class InstrumentSettingsUpdateAction : InstrumentAction
	{
		/// <summary>
		/// Creates a new instance of InstrumentSettingsUpdateAction class.
		/// </summary>
		public InstrumentSettingsUpdateAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentSettingsUpdateAction"></param>
        public InstrumentSettingsUpdateAction( InstrumentSettingsUpdateAction instrumentSettingsUpdateAction )
            : base( instrumentSettingsUpdateAction )
        {
            //this.Instrument = (Instrument)instrumentSettingsUpdateAction.Instrument.Clone();
        }
	}

	public class InstrumentSettingsReadAction : InstrumentAction
	{
        /// <summary>
        /// If this ReadEvent is occurring after an UpdateEvent, then this property will contain
        /// the ID of the settings used during the UpdateEvent.  Otherwise, will be Nullid.
        /// </summary>
        /// <remarks>The purpose of this is for uploading of it to iNet.</remarks>
        public long SettingsRefId { get; set; }

		/// <summary>
		/// Creates a new instance of InstrumentSettingsReadAction class.
		/// </summary>
		public InstrumentSettingsReadAction()
		{
            SettingsRefId = DomainModelConstant.NullId;
		}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentSettingsReadAction"></param>
        public InstrumentSettingsReadAction( InstrumentSettingsReadAction instrumentSettingsReadAction ) : base( instrumentSettingsReadAction )
        {

            SettingsRefId = instrumentSettingsReadAction.SettingsRefId; // Copy the settings refId.
        }
    }

	public class InstrumentDatalogDownloadAction : InstrumentAction
	{
		/// <summary>
		/// Creates a new instance of InstrumentHygieneDownloadAction class.
		/// </summary>
		public InstrumentDatalogDownloadAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentHygieneDownloadAction"></param>
        public InstrumentDatalogDownloadAction( InstrumentDatalogDownloadAction instrumentHygieneDownloadAction )
            : base( instrumentHygieneDownloadAction )
        {
        }
	}

	public class InstrumentDatalogClearAction : InstrumentAction
	{
		/// <summary>
		/// Creates a new instance of InstrumentHygieneClearAction class.
		/// </summary>
		public InstrumentDatalogClearAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentHygieneClearAction"></param>
        public InstrumentDatalogClearAction( InstrumentDatalogClearAction instrumentHygieneClearAction ) : base( instrumentHygieneClearAction )
        {
        }
	}

	public class CalibrationFailureAction : InstrumentAction
	{
		/// <summary>
        /// Creates a new instance of CalibrationFailureAction class.
		/// </summary>
		public CalibrationFailureAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="calibrationFailureAction"></param>
        public CalibrationFailureAction( CalibrationFailureAction calibrationFailureAction ) : base( calibrationFailureAction )
        {
        }
	}

    public class BumpFailureAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of BumpFailureAction class.
        /// </summary>
        public BumpFailureAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="bumpFailureAction"></param>
        public BumpFailureAction( BumpFailureAction bumpFailureAction )
            : base( bumpFailureAction )
        {
        }
    }

	public class ManualCalibrationRequiredAction : InstrumentAction
	{
		/// <summary>
        /// Creates a new instance of ManualCalibrationRequiredAction class.
		/// </summary>
		public ManualCalibrationRequiredAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="manualCalibrationRequiredAction"></param>
        public ManualCalibrationRequiredAction( ManualCalibrationRequiredAction manualCalibrationRequiredAction ) : base( manualCalibrationRequiredAction )
        {
        }
	}

    public class ManualBumpTestRequiredAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of ManualBumpTestRequiredAction class.
        /// </summary>
        public ManualBumpTestRequiredAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="manualBumpTestRequiredAction"></param>
        public ManualBumpTestRequiredAction( ManualBumpTestRequiredAction manualBumpTestRequiredAction )
            : base( manualBumpTestRequiredAction )
        {
        }
    }

    public class InstrumentAlarmEventsDownloadAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of InstrumentAlarmEventsDownloadAction class.
        /// </summary>
        public InstrumentAlarmEventsDownloadAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentAlarmEventsDownloadAction"></param>
        public InstrumentAlarmEventsDownloadAction( InstrumentAlarmEventsDownloadAction instrumentAlarmEventsDownloadAction ) : base( instrumentAlarmEventsDownloadAction )
        {
        }
    }

    public class InstrumentAlarmEventsClearAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of InstrumentAlarmEventsClearAction class.
        /// </summary>
        public InstrumentAlarmEventsClearAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentHygieneClearAction"></param>
        public InstrumentAlarmEventsClearAction( InstrumentAlarmEventsClearAction instrumentAlarmEventsClearAction )
            : base( instrumentAlarmEventsClearAction )
        {
        }
    }

    public class InstrumentUnregisteredAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of InstrumentUnregisteredAction class.
        /// </summary>
        public InstrumentUnregisteredAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentUnregisteredAction"></param>
        public InstrumentUnregisteredAction( InstrumentUnregisteredAction instrumentUnregisteredAction ) : base( instrumentUnregisteredAction )
        {
        }
    }

    /// <summary>
    /// This action initiates an upgrade of the docked instrument.
    /// The upgrade is to be downloaded from iNet.
    /// </summary>
    public class InstrumentFirmwareUpgradeAction : InstrumentAction
    {
        public InstrumentFirmwareUpgradeAction() {}

        public InstrumentFirmwareUpgradeAction( FirmwareUpgrade firmwareUpgrade )
        {
            FirmwareUpgrade = firmwareUpgrade;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="firmwareUpgradeAction"></param>
        public InstrumentFirmwareUpgradeAction( InstrumentFirmwareUpgradeAction instrumentFirmwareUpgradeAction )
            : base( instrumentFirmwareUpgradeAction )
        {
            FirmwareUpgrade = instrumentFirmwareUpgradeAction.FirmwareUpgrade;
        }

        public FirmwareUpgrade FirmwareUpgrade { get; set; }

    } // end-class FirmwareUpgradeAction

    public class InstrumentManualOperationsDownloadAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of InstrumentManualOperationsDownloadAction class.
        /// </summary>
        public InstrumentManualOperationsDownloadAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentAlarmEventsDownloadAction"></param>
        public InstrumentManualOperationsDownloadAction( InstrumentManualOperationsDownloadAction instrumentManualOperationsDownloadAction )
            : base( instrumentManualOperationsDownloadAction )
        {
        }
    }

    public class InstrumentManualOperationsClearAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of InstrumentManualOperationsClearAction class.
        /// </summary>
        public InstrumentManualOperationsClearAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="instrumentAlarmEventsDownloadAction"></param>
        public InstrumentManualOperationsClearAction( InstrumentManualOperationsClearAction instrumentManualOperationsClearAction )
            : base( instrumentManualOperationsClearAction )
        {
        }
    }

	/// <summary>
	/// Provides functionality to define a DockingStationReplacedAction on a docking station.
	/// </summary>
	public class InstrumentDisableReplacedAction : InstrumentAction
	{
		public InstrumentDisableReplacedAction() {}

		public InstrumentDisableReplacedAction( InstrumentDisableReplacedAction instrumentReplacedAction )
			: base( instrumentReplacedAction )
		{
			this.ReplacedSerialNumber = instrumentReplacedAction.ReplacedSerialNumber;
		}
		public string ReplacedSerialNumber { get; set; }
	}

    public class DataDownloadPauseAction : InstrumentAction
    {
        /// <summary>
        /// Creates a new instance of DataDownloadPauseAction class.
        /// </summary>
        public DataDownloadPauseAction() {}

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="dataDownloadPauseAction"></param>
        public DataDownloadPauseAction(DataDownloadPauseAction dataDownloadPauseAction)
            : base(dataDownloadPauseAction)
        {
        }
    }
}
