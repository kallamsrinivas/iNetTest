using System;
using System.Collections.Generic;


namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// instrument idle event.	
	/// </summary>
	public class InstrumentNothingEvent : InstrumentEvent
	{
        public InstrumentNothingEvent( IOperation operation ) : base( operation ) {}
	}

    public abstract class InstrumentGasResponseEvent : InstrumentEvent, IPassed
    {
        private List<SensorGasResponse> _gasResponses;
        private List<UsedGasEndPoint> _usedGasEndPoints;
        private List<SensorGasResponse> _highBumpFailCalGasResponses;

        private bool _isSensorFailureModeEnabled;
        private bool _isSCSensorFailureModeEnabled;
        private Instrument _dockedInstrument = new Instrument();

        /// <summary>
        /// For a calibration event, this is the date/time of the next scheduled calibration.
        /// For a bump event, this is the date/time of the next scheduled bump.
        /// </summary>
        public DateTime? NextUtcScheduledDate { get; set; }

        public InstrumentGasResponseEvent() { }

        public InstrumentGasResponseEvent( IOperation operation ) : base( operation ) {}

        /// <summary>
        /// Gets or sets the list of SensorGasResponse objects.
        /// </summary>
        public List<SensorGasResponse> GasResponses
        {
            get
            {
                if ( _gasResponses == null )
                    _gasResponses = new List<SensorGasResponse>();

                return _gasResponses;
            }
            set
            {
                _gasResponses = value;
            }
        }

            /// <summary>
		/// Gets or sets the list of UsedGasEndPoint instances.
        /// </summary>
        /// <remarks>
		/// Unlike SensorGasResponse.UsedGasEndPoints, this
        /// list of Cylinders are cylinders that were not used for any
        /// particular sensor (such as for purging gas).
        /// </remarks>
        public List<UsedGasEndPoint> UsedGasEndPoints
        {
            get
            {
                if ( _usedGasEndPoints == null )
                    _usedGasEndPoints = new List<UsedGasEndPoint>();

                return _usedGasEndPoints;
            }
            set
            {
                _usedGasEndPoints = value;
            }
        }

        /// <summary>
        /// The instrument is considered passed only if each
        /// sensor has passed.
        /// </summary>
        public bool Passed
        {
            get
            {
                foreach ( SensorGasResponse sgr in this.GasResponses )
                {
                    if ( !sgr.Passed ) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// This is used to check if any calibration gas responses
        /// are recorded due to failing the O2 high bump test
        /// </summary>
        /// <remarks>INS-7625 SSAM v7.6</remarks>
        public bool HasHighBumpFailCalGasResponses
        {
            get
            {
                return this.HighBumpFailCalGasResponses.Count > 0;
            }
        }

        /// <summary>
        /// In case an O2 fails high bump test, calibration is 
        /// performed and the resultant gas responses are stored in this. 
        /// </summary>
        /// <remarks>INS-7625 SSAM v7.6</remarks>
        public List<SensorGasResponse> HighBumpFailCalGasResponses
        {
            get
            {
                if (_highBumpFailCalGasResponses == null)
                    _highBumpFailCalGasResponses = new List<SensorGasResponse>();

                return _highBumpFailCalGasResponses;
            }
            set
            {
                _highBumpFailCalGasResponses = value;
            }
        }

        /// <summary>
        /// Gets or Sets the Sensor Failure Mode Status
        /// </summary>
        /// <remarks> DSW-1034 RHP Tango</remarks>
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
        /// <remarks>DSW-1068 RHP v9.5</remarks>
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

        /// <summary>
        /// The currently docked instrument, with information filled in during Discovery.
        /// If no instrument is currently docked, then the returned instrument's
        /// serial number will be empty and its DeviceType will be Unknown.
        /// </summary>
        public Instrument Instrument
        {
            get
            {
                return _dockedInstrument;
            }
            set
            {
                _dockedInstrument = (value != null) ? (Instrument)value.Clone() : new Instrument();
            }
        }

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dsEvent"></param>
        protected override void CopyTo( DockingStationEvent dsEvent )
        {
            // First, deep copy the base class.
            base.CopyTo( dsEvent );

            // Next, deep copy this subclass.
            InstrumentGasResponseEvent instrumentGasResponseEvent = (InstrumentGasResponseEvent)dsEvent;

            instrumentGasResponseEvent.GasResponses = new List<SensorGasResponse>();
            foreach ( SensorGasResponse sgr in this.GasResponses )
                instrumentGasResponseEvent.GasResponses.Add( (SensorGasResponse)sgr.Clone() );

            instrumentGasResponseEvent.UsedGasEndPoints = new List<UsedGasEndPoint>();
            foreach ( UsedGasEndPoint u in this.UsedGasEndPoints )
                instrumentGasResponseEvent.UsedGasEndPoints.Add( (UsedGasEndPoint)u.Clone() );

            instrumentGasResponseEvent.HighBumpFailCalGasResponses = new List<SensorGasResponse>();
            foreach (SensorGasResponse sgr in this.HighBumpFailCalGasResponses)
                instrumentGasResponseEvent.HighBumpFailCalGasResponses.Add((SensorGasResponse)sgr.Clone());
        }

        /// <summary>
        /// Searches GasResponses list for a SensorGasResponse that matches the specifed UID. Returns null if not found.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public SensorGasResponse GetSensorGasResponseByUid( string uid )
        {
            return this.GasResponses.Find( sgr => sgr.Uid == uid );
        }

    } // end-class InstrumentGasResponseEvent

	public class InstrumentBumpTestEvent : InstrumentGasResponseEvent
	{
        private void Init()
        {
            EventCode = EventCode.GetCachedCode( EventCode.BumpTest );
        }

        public InstrumentBumpTestEvent()
        {
            Init();
        }

        public InstrumentBumpTestEvent( IOperation operation ) : base( operation )
        {
            Init();
        }

    } // end-class InstrumentBumpTestEvent

	public class InstrumentDiagnosticEvent : InstrumentEvent, IDiagnosticEvent
	{
        private List<Diagnostic> _diagnostics;

        public InstrumentDiagnosticEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.InstrumentDiagnostics );
        }
		
		/// <summary>
		/// Gets or sets the diagnostic event's diagnostics.
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
        /// Gets or Sets whether instrument is critical error.
        /// Note: Instrument is set to be in critical error if any error code that are logged in the instrument 
        /// matches with critical errors downloaded from iNet
        /// </summary>
		public bool InstrumentInCriticalError { get; set; } //Suresh 06-FEB-2012 INS-2622

        /// <summary>
        /// Gets or Sets the instrument's critical error code if any.
        /// Note: Instrument is set to be in critical error if any error code that are logged in the instrument 
        /// matches with critical error codes downloaded from iNet
        /// </summary>
        /// <remarks>INS-8446 RHP v7.6</remarks>
        public string InstrumentCriticalErrorCode { get; set; }

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dockingStationEvent"></param>
        protected override void CopyTo( DockingStationEvent dockingStationEvent )
        {
            base.CopyTo( dockingStationEvent );

            InstrumentDiagnosticEvent instrumentDiagnosticEvent = (InstrumentDiagnosticEvent)dockingStationEvent;

            instrumentDiagnosticEvent.Diagnostics = new List<Diagnostic>();
            foreach ( Diagnostic diagnostic in this.Diagnostics )
                instrumentDiagnosticEvent.Diagnostics.Add( (Diagnostic)diagnostic.Clone() );
        }
    } // end-class InstrumentDiagnosticEvent

	public class InstrumentCalibrationEvent : InstrumentGasResponseEvent
	{
        public InstrumentCalibrationEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.Calibration );
        }
	}

	public class InstrumentDisableReplacedEvent : InstrumentEvent, IPassed
	{
		public InstrumentDisableReplacedEvent( IOperation operation ) : base( operation )
		{
			EventCode = EventCode.GetCachedCode( EventCode.InstrumentDisableReplaced );

			this.DetectedReplacedSerialNumber = String.Empty;
			this.SerialNumberBefore = String.Empty;
			this.SerialNumberAfter = String.Empty;
			this.Passed = false;
		}

		// if this event is ever uploaded to iNet in the future these properties will be useful
		public string DetectedReplacedSerialNumber { get; set; } // the serial number of the instrument intended to be disabled
		public string SerialNumberBefore { get; set; } // before instrument is disabled
		public string SerialNumberAfter { get; set; } // after instrument is disabled
		public bool Passed { get; set; } // was the intended instrument disabled
	}
	
	public class InstrumentSettingsUpdateEvent : InstrumentEvent
	{
		public InstrumentSettingsUpdateEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.InstrumentSettingsUpdate );
            DockedTime = DomainModelConstant.NullDateTime;
        }

		/// <summary>
		/// The date/time the instrument returned by DockedInstrument property was actually docked.
		/// </summary>
		public DateTime DockedTime { get; set; }
	}

	public class InstrumentSettingsReadEvent : InstrumentEvent
	{
        public InstrumentSettingsReadEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.InstrumentSettingsRead );
            DockedTime = DomainModelConstant.NullDateTime;
        }

 		/// <summary>
		/// The date/time the instrument returned by DockedInstrument property was actually docked.
		/// </summary>
		public DateTime DockedTime { get; set; }
    }

	public class InstrumentDatalogClearEvent : InstrumentEvent
	{
        public InstrumentDatalogClearEvent( IOperation operation ) : base( operation ) {}

		/// <summary>
		/// Gets or sets the indicator whether the session was cleared.
		/// </summary>
		public int SessionsCleared { get; set; }
	}

    public class InstrumentAlarmEventsDownloadEvent : InstrumentEvent
    {
        private AlarmEvent[] _alarmEvents;

        public InstrumentAlarmEventsDownloadEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.DownloadAlarmEvents );
        }
		
        /// <summary>
        /// Gets or sets the list of the instrument's Alarm Events
        /// </summary>
        public AlarmEvent[] AlarmEvents
        {
            get
            {
                if ( _alarmEvents == null )
                    _alarmEvents = new AlarmEvent[0];

                return _alarmEvents;
            }
            set
            {
                _alarmEvents = value;
            }
        }

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dockingStationEvent"></param>
        protected override void CopyTo( DockingStationEvent dockingStationEvent )
        {
            base.CopyTo( dockingStationEvent );

            InstrumentAlarmEventsDownloadEvent instrumentAlarmEventsDownloadEvent
                = (InstrumentAlarmEventsDownloadEvent)dockingStationEvent;

            List<AlarmEvent> alarmEventList = new List<AlarmEvent>();
            foreach ( AlarmEvent alarmEvent in this.AlarmEvents )
                alarmEventList.Add( (AlarmEvent)alarmEvent.Clone() );

            // Finally, any needed deep cloning of this subclass goes here...
            instrumentAlarmEventsDownloadEvent.AlarmEvents = alarmEventList.ToArray();
        }
    }

    public class InstrumentAlarmEventsClearEvent : InstrumentEvent
    {
        public InstrumentAlarmEventsClearEvent( IOperation operation ) : base( operation ) {}
    }

    public class InstrumentDatalogDownloadEvent : InstrumentEvent
    {
        private List<DatalogSession> _instrumentSessions;

        private void Init()
        {
            EventCode = EventCode.GetCachedCode( EventCode.DownloadDatalog );
        }

        /// <summary>
        /// Creates a new instance of an InstrumentHygieneDownloadEvent class.
        /// </summary>
        public InstrumentDatalogDownloadEvent()
        {
            Init();
        }

        public InstrumentDatalogDownloadEvent( IOperation operation ) : base( operation )
        {
            Init();
        }

        /// <summary>
        /// Gets or sets the list of datalog sessions.
        /// </summary>
        public List<DatalogSession> InstrumentSessions
        {
            get
            {
                if ( _instrumentSessions == null )
                    _instrumentSessions = new List<DatalogSession>();

                return _instrumentSessions;
            }
            set
            {
                _instrumentSessions = value;
            }
        }

        /// <summary>
        ///This method returns the string representation of this class.
        /// </summary>
        /// <returns>The string representation of this class</returns>
        public override string ToString()
        {
            return "Instrument Datalog Download";
        }

        /// <summary>
        /// Makes copies of reference-type members. This is a helper method for Cloning.
        /// </summary>
        /// <param name="dockingStationEvent"></param>
        protected override void CopyTo( DockingStationEvent dockingStationEvent )
        {
            base.CopyTo( dockingStationEvent );

            InstrumentDatalogDownloadEvent instrumentHygieneDownloadEvent
                = (InstrumentDatalogDownloadEvent)dockingStationEvent;

            // Loop through instrument sessions calling clone for each one to fill the empty list.
            instrumentHygieneDownloadEvent.InstrumentSessions = new List<DatalogSession>();
            foreach ( DatalogSession session in this.InstrumentSessions )
                instrumentHygieneDownloadEvent.InstrumentSessions.Add( (DatalogSession)session.Clone() );
        }

    }  // end-class InstrumentDatalogDownloadEvent

    public class InstrumentFirmwareUpgradeEvent : InstrumentEvent
    {
        /// <summary>
        /// This property will be set to false if no error occurred that prevented the successful upgrade of the instrument.
        /// By default, it's true.
        /// </summary>
        public bool UpgradeFailure { get; set; }

        public InstrumentFirmwareUpgradeEvent( IOperation operation )
            : base( operation )
        {
            UpgradeFailure = true; // by default, we start out with an assumed error status.
            EventCode = EventCode.GetCachedCode( EventCode.InstrumentFirmwareUpgrade );
        }

    }

    public class InstrumentManualOperationsDownloadEvent : InstrumentGasResponseEvent
    {
        public InstrumentManualOperationsDownloadEvent( IOperation operation ) : base( operation )
        {
            EventCode = EventCode.GetCachedCode( EventCode.DownloadManualOperations );
        }
    }

    public class InstrumentManualOperationsClearEvent : InstrumentGasResponseEvent
    {
        public InstrumentManualOperationsClearEvent( IOperation operation ) : base( operation ) {}
    }

    public class DataDownloadPauseEvent : InstrumentEvent  // SGF  11-Mar-2013  INS-3962
    {
        public DataDownloadPauseEvent(IOperation operation)
            : base(operation)
        {
            EventCode = EventCode.GetCachedCode(EventCode.DataDownloadPause);
        }
    }

    public enum TurnOffAction
    {
        None,
        TurnOffSensors,
        Shutdown,
		Postponed,
        NotSupported
    }

    public class InstrumentTurnOffEvent : InstrumentEvent
    {
        public InstrumentTurnOffEvent( IOperation operation ) : base( operation ) {}

        public TurnOffAction TurnOffAction { get; set; }

    }

} // end-namespace
