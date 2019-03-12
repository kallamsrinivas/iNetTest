using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    public class EventJournal
    {
        #region Fields

        private const string DATETIME_FORMAT = "MM/dd/yy HH:mm:ss";

        private EventCode _eventCode;
        private string _sn;
        private string _instrumentSn;
        private DateTime _runTime = DateTime.MinValue;
        private DateTime _eventTime = DateTime.MinValue;
        private bool _passed;
        private int _position;
        private string _softwareVersion;

        #endregion

        #region Constructors

        private void Init( EventCode eventCode, string serialNumber, string instrumentSerialNumber, DateTime runTime, DateTime eventTime, bool passed, int position, string softwareVersion )
        {
            _eventCode = eventCode;

            _sn = serialNumber;
            _instrumentSn = instrumentSerialNumber;
            _runTime = runTime;
            _eventTime = eventTime;
            _passed = passed;
            _position = position;
            _softwareVersion = softwareVersion;
        }

        /// <summary>
        /// Helper method for the overlaoded constructors to prevent code duplication.
        /// </summary>
        /// <param name="code"></param>
        private void Init( string eventCodeString, string serialNumber, string instrumentSerialNumber, DateTime runTime, DateTime eventTime, bool passed, int position, string softwareVersion )
        {
            // the passed-in event code might be unknown to this docking station. especially if it's 
            // a newer event code and the docking station is running older firmware that doesnb't know 
            // if the code.

            EventCode eventCode = EventCode.GetCachedCode( eventCodeString );

            Init( eventCode, serialNumber, instrumentSerialNumber, runTime, eventTime, passed, position, softwareVersion );
        }

        public EventJournal( EventCode eventCode, string serialNumber, DateTime runTime, DateTime eventTime, bool passed, string softwareVersion )
        {
            Init( eventCode, serialNumber, null, runTime, eventTime, passed, DomainModelConstant.NullInt, softwareVersion );
        }

        public EventJournal( string eventCodeString, string serialNumber, string instrumentSerialNumber, DateTime runTime, DateTime eventTime, bool passed, int position, string softwareVersion )
        {
            Init( eventCodeString, serialNumber, instrumentSerialNumber, runTime, eventTime, passed, position, softwareVersion );
        }

        #endregion

        #region Properties

        public EventCode EventCode
        {
            get { return _eventCode; }
            set { _eventCode = value; }
        }

        /// <summary>
        /// This is a sensor UID for bumps and cals.  This is docking station or instrument for all other events.
        /// </summary>
        public string SerialNumber
        {
            get
            {
                if ( _sn == null ) _sn = string.Empty;
                return _sn;
            }
            set { _sn = value; }
        }

        /// <summary>
        /// This is serial number of instrument for bumps and cals; empty for all other events.
        /// </summary>
        public string InstrumentSerialNumber
        {
            get
            {
                if ( _instrumentSn == null ) _instrumentSn = string.Empty;
                return _instrumentSn;
            }
            set { _instrumentSn = value; }
        }

        public bool Passed
        {
            get { return _passed; }
            set { _passed = value; }
        }

        /// <summary>
        /// The date/time the event was invoked.  For instrument/docking station events, this 
        /// is the same as the RunTime.  For sensors, it's the time the event was begun on the instrument,
        /// but which should be before the RunTime of the sensor.
        /// </summary>
        public DateTime EventTime
        {
            get { return _eventTime; }
            set { _eventTime = value; }
        }

        /// <summary>
        /// The date/time the event was run.  For instrument/docking station events, this 
        /// is the same as the EventTime.  For sensors, it's the time after the EventTime
        /// that the operation was performed on the sensor.
        /// </summary>
        public DateTime RunTime
        {
            get { return _runTime; }
            set { _runTime = value; }
        }

        /// <summary>
        /// The position of the component in the instrument
        /// </summary>
        public int Position
        {
            get { return _position; }
            set { _position = value; }
        }

        /// <summary>
        /// The software version of the Docking Station or Instrument at the time of the event.
        /// Thi is NOT the component version.
        /// </summary>
        /// <remarks>
        /// If the event is for a component, this is the version of the instrument at the
        /// time of the event, not the component version!
        /// </remarks>
        public string SoftwareVersion
        {
            get
            {
                if (_softwareVersion == null)
                    _softwareVersion = string.Empty;
                return _softwareVersion;
            }
            set
            {
                _softwareVersion = value; 
            }
        }

        #endregion

        #region Methods

        public override string ToString()
        {
            if (InstrumentSerialNumber == string.Empty)
                return string.Format( "{0},SN={1},LastRun={2},EventTime={3},{4},Pos={5},SWV={6}",
                    EventCode.Code, SerialNumber,
                    RunTime.ToString(DATETIME_FORMAT), EventTime.ToString(DATETIME_FORMAT), Passed ? "Passed" : "Failed",
                    Position, SoftwareVersion);
            else
                return string.Format( "{0},SN={1},Inst={2},LastRun={3},LastDock={4},{5},Pos={6},SWV={7}",
                    EventCode.Code, SerialNumber, InstrumentSerialNumber,
                    RunTime.ToString( DATETIME_FORMAT ), EventTime.ToString( DATETIME_FORMAT ), Passed ? "Passed" : "Failed",
                    Position, SoftwareVersion);
        }

        #endregion
    }
}
