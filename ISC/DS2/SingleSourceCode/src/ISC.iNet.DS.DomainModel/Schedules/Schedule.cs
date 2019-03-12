using System;
using System.Globalization;
using System.Collections.Generic;

namespace ISC.iNet.DS.DomainModel
{
    public abstract class Schedule
    {
        // "Type.EmptyTypes" does not exist in Compact Framework, only exists 
        // in the full framework. So, we create our own.
        static private readonly Type[] EmptyTypes = new Type[ 0 ];

        private long _id = DomainModelConstant.NullId;
        private long _refId = DomainModelConstant.NullId;

        private EventCode _eventCode;

		private string _equipmentCode;
		private DeviceType _equipmentType;
        private string _equipmentSubTypeCode;

        private string _name;

        private List<string> _serialNumbers; // equipment serial numbers

        private List<string> _componentCodes;

        private DateTime _startDate = DateTime.SpecifyKind( DateTime.MinValue, DateTimeKind.Local );
        private TimeSpan _runAtTime = TimeSpan.Zero;

        private bool _enabled;

        private short _interval = DomainModelConstant.NullShort;

        private bool _uponDocking = false;

        protected static readonly Calendar _gregorianCalendar = new GregorianCalendar();

        private TimeZoneInfo _tzi;

        protected Schedule(long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled, bool uponDocking, short interval, DateTime startDate, TimeSpan runAtTime)
        {
            _id = id;
            _refId = refId;
            _name = name;
            _eventCode = eventCode;
			_equipmentCode = equipmentCode;
            _equipmentSubTypeCode = equipmentSubTypeCode;
			_equipmentType = Device.GetDeviceType( _equipmentCode );
            _uponDocking = uponDocking;
            _interval = interval;
            _enabled = enabled;
            if ( startDate != DateTime.MinValue )
                _startDate = DateTime.SpecifyKind( startDate.Date, DateTimeKind.Local );

            _runAtTime = runAtTime;
        }

        public TimeZoneInfo TimeZoneInfo
        {
            get
            {
                if ( _tzi == null )
                    _tzi = TimeZoneInfo.GetUTC();
                return _tzi;
            }
            set
            {
                _tzi = value;
            }
        }

        /// <summary>
        /// The database ID generated when the schedule was inserted in the iNetDS's local database.
        /// </summary>
        public long Id
        {
            get { return _id; }
            set { _id = value; }
        }

        /// <summary>
        /// The iNet reference ID if scheduled. NullId is returned if forced by the IDS.
        /// </summary>
        public long RefId
        {
            get { return _refId; }
        }

        public EventCode EventCode
        {
            get { return _eventCode; }
        }

		/// <summary>
		/// Gets the equipment code.  Equipment code may be null.  
		/// NOTE: This is desired so it can be inserted into the database as such.  
		/// Use the EquipmentType property instead for non-database operations. 
		/// </summary>
		public string EquipmentCode
		{
			get 
			{
				return _equipmentCode; 
			}
		}

		/// <summary>
		/// Gets the equipment type (device type) for the equipment code of the schedule.
		/// </summary>
		public DeviceType EquipmentType
		{
			get { return _equipmentType; }
		}

        /// <summary>
        /// Gets the equipment sub type (Ventis, iQuad etc) of the schedule.
        /// </summary>
        public string EquipmentSubTypeCode
        {
            get { return _equipmentSubTypeCode; }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public string Name
        {
            get { return _name; }
        }

        public List<string> SerialNumbers
        {
            get
            {
                if ( _serialNumbers == null ) _serialNumbers = new List<string>();
                return _serialNumbers;
            }
            set { _serialNumbers = value; }
        }

        public List<string> ComponentCodes
        {
            get
            {
                if ( _componentCodes == null ) _componentCodes = new List<string>();
                return _componentCodes;
            }
            set { _componentCodes = value; }
        }

        public bool UponDocking
        {
            get { return _uponDocking; }
            set { _uponDocking = value; }
        }

        public short Interval
        {
            get { return _interval; }
        }

        private List<ScheduleProperty> _scheduleProperties;
        /// <summary>
        /// Event Schedule properties applicable for all event types
        /// </summary>
        public List<ScheduleProperty> ScheduleProperties
        {
            get
            {
                if ( _scheduleProperties == null )
                    _scheduleProperties = new List<ScheduleProperty>();
                return _scheduleProperties;
            }
            set { _scheduleProperties = value; }
        }
 
        /// <summary>
        /// The Date (month/day/year) the schedule is supposed to first start running. (The hours,minutes,seconds components will
        /// all be zero.)
        /// </summary>
        public DateTime StartDate { get { return _startDate; } }

        /// <summary>
        /// The the time of day the schedule is supposed to run, offset from midnight.  Value will never reach 24 hours.
        /// </summary>
        public TimeSpan RunAtTime { get { return _runAtTime; } }

        /// <summary>
        /// The date AND time the schedule is supposed to first start running. (i.e., the StartDate plus the RunAtTime.)
        /// The returned value is set to DateTimeKind.Local
        /// </summary>
        public DateTime StartDateTime { get { return  _startDate.Add( RunAtTime ); } }

        /// <summary>
        /// Returns RunAtTime minutes and seconds in "00:00" format.
        /// </summary>
        /// <returns></returns>
        public string RunAtTimeToString() { return RunAtTime.Hours.ToString("d2") + ":" +RunAtTime.Minutes.ToString( "d2" ); }

        /// <summary>
        /// Returns StartDateToString formatted as a ShortDateString.
        /// </summary>
        /// <returns></returns>
        public string StartDateToString() { return StartDate.ToShortDateString(); }

        /// <summary>
        /// Returns the specified date (year,month,day) also set to the hours and minutes defined by the StartTime property.
        /// and set to DateTimeKind.Local.
        /// </summary>
        /// <param name="date">The date's hours and days will be ignored (replaced by StartTime's hours and minutes)</param>
        protected DateTime SetToRunAtTime( DateTime date )
        {
            return DateTime.SpecifyKind( new DateTime( date.Year, date.Month, date.Day, RunAtTime.Hours, RunAtTime.Minutes, RunAtTime.Seconds ), DateTimeKind.Local );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastRunTime">
        /// The  last time the schedule ran, in local time.
        /// If its "Kind" is UTC, the subclasses' overrides will conver it to the passed-in TimeZoneInfo's local time.</param>
        /// <param name="dockedTime">
        /// The time of docking, in local time.
        /// If its "Kind" is UTC, the subclasses' overrides will conver it to the passed-in TimeZoneInfo's local time.
        /// </param>
        /// <param name="tzi">The docking station's local time zone setting.</param>
        /// <returns>
        /// The time the schedule is next due to run in local time, as per TimeZoneInfo parameter.
        /// </returns>
        public abstract DateTime CalculateNextRunTime( DateTime lastRunTime, DateTime dockedTime, TimeZoneInfo tzi );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="nextRunTime">Assumed to already be converted to the docking station's local time zone.</param>
        /// <param name="now">Assumed to already be converted to the docking station's local time zone.</param>
        /// <returns></returns>
        public bool IsOverdue( DateTime nextRunTime, DateTime now )
        {
            bool isOverdue = nextRunTime <= now;

            return isOverdue;
        }

#if TODO // Leave this method here for now.  We may yet still need it.
        /// <summary>
        /// Determine if the schedule should run at the specified time.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public abstract bool IsScheduledTimeToRun( DateTime dateTime );
#endif
        /// <summary>
        /// Given a DayOfWeek, and week number, this method determines the date
        /// that will occur in the specified month.
        /// i.e., determines the date when the Xth DayOfMonth of some month/year occurs.
        /// </summary>
        /// <param name="week">1 through 5.  5 means "the last week of the month"</param>
        /// <param name="dayOfWeek"></param>
        /// <param name="month">Set to a desired month.</param>
        /// <param name="year">Set to a desired year.</param>
        /// <returns></returns>
        protected DateTime GetXthDayOfWeek( short week, DayOfWeek? dayOfWeek, int month, int year )
        {
            // Set to the 1st of the current month
            DateTime dateTime = new DateTime( year, month, 1 );

            // Now, keep adding days until the current day of the week equals desired DayOfWeek.
            // This effectively sets us to the 1st Day Of Week for the month.
            while ( dateTime.DayOfWeek != dayOfWeek )
                dateTime = dateTime.AddDays( 1 );

            // We should now be at the first DayOfWeek for the month.  Now advance
            // to the same day in the proper Week (2st week, 3rd, week, last week, etc.)
            if ( week <= 4 )
            {
                // Subtract one, since the current week is already the first week.
                dateTime = _gregorianCalendar.AddWeeks( dateTime, week - 1 );
            }
            else // Week == 5 means the very last week
            {
                while ( true )
                {
                    DateTime futureDate = _gregorianCalendar.AddWeeks( dateTime, 1 );

                    // If we go so far that the month changes, then we know
                    // we're currently at the last week.
                    if ( futureDate.Month != dateTime.Month )
                        break;

                    dateTime = futureDate;
                }
            }

            return dateTime;
        }
    }
}
