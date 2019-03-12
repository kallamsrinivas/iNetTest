using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;

namespace ISC.iNet.DS.DomainModel
{
	public class NothingEvent : DockingStationEvent
	{
        public NothingEvent() {}
        public NothingEvent( IOperation operation ) : base( operation ) {}
	}

    public class DiagnosticEvent : DockingStationEvent, IDiagnosticEvent
	{
        private List<Diagnostic> _diagnostics;

        public DiagnosticEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.Diagnostics );
        }

		/// <summary>
		/// Gets or sets the list of diagnostics events.
		/// </summary>
        /// <remarks>
        /// Implements IDiagnosticEvent's Diagnostics property.
        /// </remarks>
		public List<Diagnostic> Diagnostics
		{
			get
			{
				if ( _diagnostics == null ) _diagnostics = new List<Diagnostic>();
				return _diagnostics;
			}
			set
			{
				_diagnostics = value;
			}
		}

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dsEvent"></param>
        protected override void CopyTo( DockingStationEvent dsEvent )
        {
            base.CopyTo( dsEvent );

            DiagnosticEvent diagnosticEvent = (DiagnosticEvent)dsEvent;

            diagnosticEvent.Diagnostics = new List<Diagnostic>( this.Diagnostics.Count );
            // Loop through the docking station diagnostics calling clone for each one to fill the empty list.
            foreach ( Diagnostic diagnostic in this.Diagnostics )
                diagnosticEvent.Diagnostics.Add( (Diagnostic)diagnostic.Clone() );
        }
    } // end-class DiagnosticEvent

	public class InteractiveDiagnosticEvent : DockingStationEvent
	{
        public InteractiveDiagnosticEvent( IOperation operation ) : base( operation ) {}
	}

	public class SettingsReadEvent : DockingStationEvent
	{
        /// <summary>
        /// If true, then this Read is occurring due to an Update that was just performed.
        /// Otherwise, false (the default).
        /// </summary>
        public bool PostUpdate { get; set; }

        public SettingsReadEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.SettingsRead );
        }

    }  // end-class SettingsReadEvent

    public class SettingsUpdateEvent : DockingStationEvent, IRebootableEvent
	{
        /// <summary>
        /// SettingsUpdateOperation will set this to true if the update it performs
        /// requires a reboot (the operation does not perform the reboot itself).
        /// </summary>
        public bool RebootRequired { get; set; }

        /// <summary>
        /// SettingsUpdateOperation will set this to true if the update it performs
        /// requires resetting the database (the operation does not perform the database reset itself).
        /// </summary>
        public bool ResetDatabaseRequired { get; set; }

        public bool UseDockingStation { get; set; }

        public SettingsUpdateEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.SettingsUpdate );
        }
	}

	public class CylinderPressureResetEvent : DockingStationEvent
	{
		public bool PostUpdate { get; set; }
		public long SettingsRefId { get; set; }

		public CylinderPressureResetEvent( IOperation operation )
			: base( operation )
		{
			EventCode = EventCode.GetCachedCode( EventCode.CylinderPressureReset );
		}
	}

    public class ExchangeStatusEvent : DockingStationEvent
    {
        /// <summary>
        /// Indicates whether or not any factory cylinder information was modified.
        /// </summary>
        public bool CylindersModified { get; set; }

        /// <summary>
        /// Indicates whether or not any manifold or manually-assigned cylinder information was modified.
        /// </summary>
        public bool ManualsModified { get; set; }

        /// <summary>
        /// Indicates whether the docking station's account number was modified.
        /// </summary>
        public bool AccountModified { get; set; }

        /// <summary>
        /// Indicates whether the docking station's account is an ISC manufacturing account.
        /// </summary>
        public bool IsManufacturingModified { get; set; }

        /// <summary>
        /// Indicates whether the docking station's iNet activation flag has changed.
        /// </summary>
        public bool ActivationModified { get; set; }

        /// <summary>
        /// Indicates whether or not any Event Journals were added or modified.
        /// </summary>
        public bool EventJournalsModified { get; set; }

        /// <summary>
        /// Indicates whether or not any of the docking station's settings were modified.
        /// </summary>
        public bool DockingStationModified { get; set; }

		/// <summary>
		/// Indicates whether or not the list of replaced equipment was modified. 
		/// </summary>
		public bool ReplacedEquipmentModified { get; set; }

        /// <summary>
        /// Indicates whether or not any settings were modified
        /// that affect the currently docked instrument.
        /// </summary>
        public bool InstrumentSettingsModified { get; set; }

        /// <summary>
        /// Indicates whether or not any event Schedules were added
        /// or modified modified.
        /// </summary>
        public bool SchedulesModified { get; set; }

        public bool SytemTimeModified { get; set; }

        public bool ServiceCodeModified { get; set; }

        private InetStatus _inetStatus = new InetStatus();

        private List<ScheduledNow> _scheduledNowList;

        public ExchangeStatusEvent( IOperation operation, InetStatus inetStatus ) : base( operation )
        {
            this._inetStatus = (InetStatus)inetStatus.Clone();
        }

        public InetStatus InetStatus { get { return _inetStatus; } }

        /// <summary>
        /// The 'forced events' returned by the server.
        /// </summary>
        public List<ScheduledNow> ScheduledNowList
        {
            get
            {
                if ( _scheduledNowList == null ) _scheduledNowList = new List<ScheduledNow>();
                return _scheduledNowList;
            }
            set { _scheduledNowList = value; }
        }

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dsEvent"></param>
        protected override void CopyTo( DockingStationEvent dsEvent )
        {
            base.CopyTo( dsEvent );

            ExchangeStatusEvent exchangeStatusEvent = (ExchangeStatusEvent)dsEvent;

            if ( this.InetStatus != null )
                exchangeStatusEvent._inetStatus = (InetStatus)this.InetStatus.Clone();

            exchangeStatusEvent.ScheduledNowList = new List<ScheduledNow>( this.ScheduledNowList.Count );
            foreach ( ScheduledNow scheduledNow in this.ScheduledNowList )
                exchangeStatusEvent.ScheduledNowList.Add( scheduledNow );
        }
    }

    public class FirmwareUpgradeEvent : DockingStationEvent, IRebootableEvent
    {
        private bool _rebootRequired;

        public FirmwareUpgradeEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.FirmwareUpgrade );
        }

        public bool RebootRequired
        {
            get { return _rebootRequired; }
            set { _rebootRequired = value; }
        }
    }

    public class MaintenanceEvent : DockingStationEvent
    {
        public MaintenanceEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.Maintenance );
        }
    }

	public class TroubleshootEvent : DockingStationEvent
	{
		public TroubleshootEvent( IOperation operation ) : base( operation )
		{
			this.EventCode = EventCode.GetCachedCode( EventCode.Troubleshoot );
		}
	}

    public class UploadDebugLogEvent : DockingStationEvent
    {
        private string _logText;

        public UploadDebugLogEvent( IOperation operation )
            : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.UploadDebugLog );
        }

        /// <summary>
        /// Gets or sets the list of the instrument's Alarm Events
        /// </summary>
        public string LogText
        {
            get
            {
                if ( _logText == null ) _logText = string.Empty;
                return _logText;
            }
            set
            {
                _logText = value;
            }
        }

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dockingStationEvent"></param>
        protected override void CopyTo( DockingStationEvent dockingStationEvent )
        {
            base.CopyTo( dockingStationEvent );

            UploadDebugLogEvent uploadDebugLogAction = (UploadDebugLogEvent)dockingStationEvent;

            uploadDebugLogAction.LogText = this.LogText;  // is this necessary? I think the memberwise clone handles this.
        }
    }

    public class UploadDatabaseEvent : DockingStationEvent
    {
        public UploadDatabaseEvent( IOperation operation )
            : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.UploadDatabase );
        }

        /// <summary>
        /// Gets or sets the list of the instrument's Alarm Events
        /// </summary>
        public byte[] File { get; set; }

        public string FileName { get; set; }

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dockingStationEvent"></param>
        protected override void CopyTo( DockingStationEvent dockingStationEvent )
        {
            base.CopyTo( dockingStationEvent );

            UploadDatabaseEvent uploadDatabaseEvent = (UploadDatabaseEvent)dockingStationEvent;

            if ( ( (UploadDatabaseEvent)dockingStationEvent ).File != null )
                uploadDatabaseEvent.File = (byte[])( (UploadDatabaseEvent)dockingStationEvent ).File.Clone();
        }
    }
}
