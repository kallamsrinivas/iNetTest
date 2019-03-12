using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;



namespace ISC.iNet.DS.DomainModel
{
    public class EventCode : IComparable
    {
        // "Type.EmptyTypes" does not exist in Compact Framework, only exists 
        // in the full framework. So, we create our own.
        static private readonly Type[] EmptyTypes = new Type[ 0 ];

        public const string SettingsRead = "SETTINGSREAD";
        public const string SettingsUpdate = "SETTINGSUPDATE";
        public const string Diagnostics = "DIAG";
        public const string InteractiveDiagnostics = "INTERACTDIAG";
		public const string InstrumentDisableReplaced = "INSTREPLACED";
        public const string InstrumentSettingsRead = "INSTSETTINGSREAD";
        public const string InstrumentSettingsUpdate = "INSTSETTINGSUPDATE";
        public const string InstrumentDiagnostics = "INSTDIAG";
        public const string BumpTest = "BUMP";
        public const string Calibration = "CAL";
        public const string DataDownloadPause = "DATADOWNLOADPAUSE"; // SGF  11-Mar-2013  INS-3962
        public const string DownloadDatalog = "DATALOGDOWNLOAD";
        public const string DownloadAlarmEvents = "ALARMEVENTSDOWNLOAD";
        public const string DownloadManualOperations = "MANUALGASOPDOWNLOAD";
        public const string UploadDebugLog = "DEBUGDOWNLOAD";
        public const string UploadDatabase = "DATABASEDOWNLOAD";
        public const string Maintenance = "REMOVEEQUIPMENT";
        public const string FirmwareUpgrade = "FIRMWAREUPGRADE";
        public const string InstrumentFirmwareUpgrade = "INSTFIRMWAREUPGRADE";
		public const string Troubleshoot = "TROUBLESHOOT";
		public const string CylinderPressureReset = "CYLPRESSURERESET";

        private static Dictionary<string, EventCode> _cache; // all known EventCodes, keyed on the code
        private static EventCode[] _orderedCache; // the same codes as in the cache, but sorted by their Priority

        // instance variables...
        private string _code;
        private short _priority;
        private string _equipmentTypeCode;
        private Type _actionType;
        private ConstructorInfo _actionCtor;

        /// <summary>
        /// class ctor
        /// </summary>
        static EventCode()
        {
            //INS-8380 12/20/2017 AJAY - Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
            //By default, set event priority for customers
            SetEventPriorityForCustomers();
        }

        /// <summary>
        /// Sets event priority for customers.
        /// INS-8380 12/20/2017 AJAY - Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
        /// </summary>
        public static void SetEventPriorityForCustomers()
        {
            _cache = new Dictionary<string, EventCode>();

            _cache.Add(InstrumentDisableReplaced, new EventCode(InstrumentDisableReplaced, 10, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentDisableReplacedAction)));
            _cache.Add(Troubleshoot, new EventCode(Troubleshoot, 90, DomainModel.EquipmentTypeCode.VDS, typeof(TroubleshootAction)));
            _cache.Add(UploadDebugLog, new EventCode(UploadDebugLog, 100, DomainModel.EquipmentTypeCode.VDS, typeof(UploadDebugLogAction)));
            _cache.Add(UploadDatabase, new EventCode(UploadDatabase, 110, DomainModel.EquipmentTypeCode.VDS, typeof(UploadDatabaseAction)));
            _cache.Add(SettingsRead, new EventCode(SettingsRead, 200, DomainModel.EquipmentTypeCode.VDS, typeof(SettingsReadAction)));
            _cache.Add(SettingsUpdate, new EventCode(SettingsUpdate, 300, DomainModel.EquipmentTypeCode.VDS, typeof(SettingsUpdateAction)));
            _cache.Add(CylinderPressureReset, new EventCode(CylinderPressureReset, 350, DomainModel.EquipmentTypeCode.VDS, typeof(CylinderPressureResetAction)));
            _cache.Add(Diagnostics, new EventCode(Diagnostics, 400, DomainModel.EquipmentTypeCode.VDS, typeof(DiagnosticAction)));
            _cache.Add(InstrumentSettingsRead, new EventCode(InstrumentSettingsRead, 500, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentSettingsReadAction)));
            _cache.Add(InstrumentSettingsUpdate, new EventCode(InstrumentSettingsUpdate, 600, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentSettingsUpdateAction)));
            _cache.Add(InstrumentDiagnostics, new EventCode(InstrumentDiagnostics, 700, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentDiagnosticAction)));
            _cache.Add(Calibration, new EventCode(Calibration, 800, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentCalibrationAction)));
            _cache.Add(BumpTest, new EventCode(BumpTest, 900, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentBumpTestAction)));
            _cache.Add(DataDownloadPause, new EventCode(DataDownloadPause, 950, DomainModel.EquipmentTypeCode.Instrument, typeof(DataDownloadPauseAction)));  // SGF  11-Mar-2013  INS-3962
            _cache.Add(DownloadManualOperations, new EventCode(DownloadManualOperations, 1000, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentManualOperationsDownloadAction)));
            _cache.Add(DownloadAlarmEvents, new EventCode(DownloadAlarmEvents, 1100, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentAlarmEventsDownloadAction)));
            _cache.Add(DownloadDatalog, new EventCode(DownloadDatalog, 1200, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentDatalogDownloadAction)));
            _cache.Add(InteractiveDiagnostics, new EventCode(InteractiveDiagnostics, short.MinValue, DomainModel.EquipmentTypeCode.VDS, typeof(InteractiveDiagnosticAction)));
            _cache.Add(FirmwareUpgrade, new EventCode(FirmwareUpgrade, 1300, DomainModel.EquipmentTypeCode.VDS, typeof(FirmwareUpgradeAction)));
            _cache.Add(InstrumentFirmwareUpgrade, new EventCode(InstrumentFirmwareUpgrade, 1400, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentFirmwareUpgradeAction)));
            _cache.Add(Maintenance, new EventCode(Maintenance, short.MaxValue, DomainModel.EquipmentTypeCode.VDS, typeof(MaintenanceAction)));

            _orderedCache = new EventCode[_cache.Count];
            _cache.Values.CopyTo(_orderedCache, 0);
        }

        /// <summary>
        /// AJAY: INS-8380 Service accounts need to perform auto-upgrade on instruments even in error/fail state - DSX
        /// If service account is configured to override event priority in admin console, 
        /// this method reorders events that needs to be executed on docking station.
        /// </summary>
        public static void SetEventPriorityForService()
        {
            _cache = new Dictionary<string, EventCode>();

            _cache.Add(InstrumentDisableReplaced, new EventCode(InstrumentDisableReplaced, 10, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentDisableReplacedAction)));
            _cache.Add(Troubleshoot, new EventCode(Troubleshoot, 90, DomainModel.EquipmentTypeCode.VDS, typeof(TroubleshootAction)));
            _cache.Add(UploadDebugLog, new EventCode(UploadDebugLog, 100, DomainModel.EquipmentTypeCode.VDS, typeof(UploadDebugLogAction)));
            _cache.Add(UploadDatabase, new EventCode(UploadDatabase, 110, DomainModel.EquipmentTypeCode.VDS, typeof(UploadDatabaseAction)));
            _cache.Add(SettingsRead, new EventCode(SettingsRead, 200, DomainModel.EquipmentTypeCode.VDS, typeof(SettingsReadAction)));
            _cache.Add(SettingsUpdate, new EventCode(SettingsUpdate, 300, DomainModel.EquipmentTypeCode.VDS, typeof(SettingsUpdateAction)));
            _cache.Add(CylinderPressureReset, new EventCode(CylinderPressureReset, 350, DomainModel.EquipmentTypeCode.VDS, typeof(CylinderPressureResetAction)));
            _cache.Add(Diagnostics, new EventCode(Diagnostics, 400, DomainModel.EquipmentTypeCode.VDS, typeof(DiagnosticAction)));
            _cache.Add(InstrumentSettingsRead, new EventCode(InstrumentSettingsRead, 500, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentSettingsReadAction)));
            _cache.Add(InstrumentSettingsUpdate, new EventCode(InstrumentSettingsUpdate, 600, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentSettingsUpdateAction)));

            _cache.Add(DataDownloadPause, new EventCode(DataDownloadPause, 700, DomainModel.EquipmentTypeCode.Instrument, typeof(DataDownloadPauseAction)));  // SGF  11-Mar-2013  INS-3962
            _cache.Add(DownloadManualOperations, new EventCode(DownloadManualOperations, 800, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentManualOperationsDownloadAction)));
            _cache.Add(DownloadAlarmEvents, new EventCode(DownloadAlarmEvents, 900, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentAlarmEventsDownloadAction)));
            _cache.Add(DownloadDatalog, new EventCode(DownloadDatalog, 950, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentDatalogDownloadAction)));
            _cache.Add(FirmwareUpgrade, new EventCode(FirmwareUpgrade, 1000, DomainModel.EquipmentTypeCode.VDS, typeof(FirmwareUpgradeAction)));
            _cache.Add(InstrumentFirmwareUpgrade, new EventCode(InstrumentFirmwareUpgrade, 1100, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentFirmwareUpgradeAction)));

            //TODO: Do not call or bump if diagnostics failed
            _cache.Add(InstrumentDiagnostics, new EventCode(InstrumentDiagnostics, 1200, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentDiagnosticAction)));
            _cache.Add(Calibration, new EventCode(Calibration, 1300, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentCalibrationAction)));
            _cache.Add(BumpTest, new EventCode(BumpTest, 1400, DomainModel.EquipmentTypeCode.Instrument, typeof(InstrumentBumpTestAction)));
            _cache.Add(InteractiveDiagnostics, new EventCode(InteractiveDiagnostics, short.MinValue, DomainModel.EquipmentTypeCode.VDS, typeof(InteractiveDiagnosticAction)));
            _cache.Add(Maintenance, new EventCode(Maintenance, short.MaxValue, DomainModel.EquipmentTypeCode.VDS, typeof(MaintenanceAction)));

            _orderedCache = new EventCode[_cache.Count];
            _cache.Values.CopyTo(_orderedCache, 0);
        }

        public static EventCode GetCachedCode( string code )
        {
            EventCode eventCode = null;

            if ( _cache.TryGetValue( code, out eventCode ) )
                return eventCode;

            return new EventCode( code, short.MinValue, string.Empty, null ); // unknown code.  Set priority to null.
        }

        /// <summary>
        /// Returns all cached event codes, ordered by their Priority.
        /// </summary>
        public static IEnumerable<EventCode> Cache
        {
            get { return EventCode._orderedCache; }
        }

        /// <summary>
        /// Returns the "Code".
        /// </summary>
        /// <returns>Returns the "Code".</returns>
        public override string ToString() { return Code; }

        public EventCode( string code, short priority, string equipmentTypeCode, Type actionType )
        {
            _code = code;
            _priority = priority;
            _equipmentTypeCode = equipmentTypeCode;
            _actionType = actionType;
#if DEBUG
            // Ensure that only DockingStationAction types are passed in.
            if ( _actionType != null )
            {
                bool baseFound = false;
                Type baseType = _actionType.BaseType;
                while ( baseType != typeof(Object) )
                {
                    if ( baseType == typeof(DockingStationAction) ) { baseFound = true; break; }
                    baseType = baseType.BaseType;
                }
                if ( !baseFound )
                    throw new ArgumentException( "EventCode ctor: actionType parameter must derive from " + typeof(DockingStationAction).ToString() );
            }
#endif
            
        }

        public int CompareTo( object obj )
        {
            EventCode otherEventCode = (EventCode)obj;
            return this.Priority.CompareTo( otherEventCode.Priority );
        }

        public string Code
        {
            get { return _code; }
        }


        public short Priority
        {
            get { return _priority; }
        }
        
        /// <summary>
        /// e.g. "Instrument" or "VDS".
        /// </summary>
        public string EquipmentTypeCode
        {
            get { return _equipmentTypeCode; }
        }

        /// <summary>
        /// The type of action that is always performed for this event.
        /// e.g., if the event is a DownloadDatalog event, then the action
        /// type would always be typeof(InstrumentHygieneDownloadAction).
        /// </summary>
        public Type ActionType
        {
            get { return _actionType; }
        }

        /// <summary>
        /// Creates a new DockingStationAction subclass instance appropriate
        /// for the event code.
        /// </summary>
        /// <returns></returns>
        public DockingStationAction CreateAction()
        {
            if ( ActionType == null )
                return new NothingAction();

            // Return new instance of appropriate DockingStationAction subclass
            // by calling the class's default constructor.  Once we obtain a constructor,
            // we hold onto for future Creates since obtaining finding them via
            // reflection is an expensive operation.
            lock ( this )
            {
                if ( _actionCtor == null )
                {
                    _actionCtor = ActionType.GetConstructor( /*Type.*/EmptyTypes );

                    if ( _actionCtor == null )
                        throw new ApplicationException( ActionType + ": No default ctor found." );
                }
                return (DockingStationAction)_actionCtor.Invoke( new object[ 0 ] );
            }
        }
    }
}
