using System;
using System.Diagnostics;
using System.Globalization;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// A Monthly recurring job has the following two options...
    /// 
    /// The first allows the job to be run "Day X of every Y months, starting on SomeDate".
    /// e.g. "Day 14 of every 3 month(s).
    /// 
    /// The second allows the job to be run on the "Xth Day of every Y months, starting on SomeDate".
    /// e.g. "1st Monday of every 3 month(s)".
    /// 
    /// </summary>
    public class ScheduledMonthly : Schedule
    {
        private short _week = DomainModelConstant.NullShort;
        private short _dayOfMonth = DomainModelConstant.NullShort;
        // Note that even thought the DayOfWeek enum is an int, we treat
        // it as a short since it is persisted as a smallint.
        private DayOfWeek? _dayOfWeek = null;


        public ScheduledMonthly( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled, bool uponDocking,
            short interval, DateTime startDate, TimeSpan runAtTime, short week, DayOfWeek? dayOfWeek, short dayOfMonth )
            : base( id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled, uponDocking, interval, startDate, runAtTime )
        {
            _week = week;
            _dayOfMonth = dayOfMonth;
            _dayOfWeek = dayOfWeek;
        }


        /// <summary>
        /// If job is "Day X of every Y months", then this
        /// property specifies Day X.  Value of 1 - 31.
        /// Otherwise, will return NullShort.
        /// </summary>
        public short DayOfMonth
        {
            get { return _dayOfMonth; }
            set { _dayOfMonth = value; }
        }

        /// <summary>
        /// If job is "Xth Day of every Y months", then
        /// this property species "Xth"; which is a week number within the month.
        /// Otherwise, will return NullShort.
        /// </summary>
        /// This is a week number with a month, 1 through 5.
        /// A value of 5 means the "last week" of the month.
        /// <remarks>
        /// </remarks>
        public short Week
        {
            get { return _week; }
            set { _week = value; }
        }

        /// <summary>
        /// If job is "Day X of every Y months", this this property
        /// specifies Day X.
        /// Otherwise, will return null
        /// </summary>
        public DayOfWeek? DayOfWeek
        {
            get { return _dayOfWeek; }
            set { _dayOfWeek = value; }
        }

        /// <summary>
        /// Returns a descriptive string for this schedule which includes the EventCode
        /// and scheduling info.
        /// </summary>
        /// <remarks>
        /// Format:"Day X of every Y months, at Z hour, starting on SomeDate".
        /// e.g "Day 2 of every 3 months, at 02:30, starting on 1/8/2010".
        /// 
        /// or
        /// 
        /// "Xth Day of every Y months, at Z hour, starting on SomeDate".
        /// e.g. "4th Tuesday of every 3 months, at 02:30, starting on 1/8/2010"
        /// </remarks>
        /// <returns></returns>
        public override string ToString()
        {
            string uponDocking = UponDocking ? " (and Upon Docking)" : null;

            if ( DayOfMonth != DomainModelConstant.NullShort )
            {
				if ( Interval == 1 )
                    return string.Format( "{0}, on Day {1} of every month, at {2}, starting on {3}{4}", 
                        EventCode, DayOfMonth, RunAtTimeToString(), StartDateToString(), uponDocking );

                return string.Format( "{0}, on Day {1} of every {2} months, at {3}, starting on {4}{5}",
                    EventCode, DayOfMonth, Interval, RunAtTimeToString(), StartDateToString(), uponDocking );
            }
            
            if ( Week != DomainModelConstant.NullShort )
            {
                Log.Assert( DayOfWeek != null, "DayOfWeek found to be null when Week is not null, Schedule ID=" + Id );

                string weekString;
                if      ( Week == 1 ) weekString = "1st";
                else if ( Week == 2 ) weekString = "2nd";
                else if ( Week == 3 ) weekString = "3rd";
                else if ( Week == 4 ) weekString = "4th";
                else if ( Week == 5 ) weekString = "last";
                else                  weekString = Week.ToString();

                string dayString = "?";
                if ( DayOfWeek != null )
                    dayString = CultureInfo.CurrentCulture.DateTimeFormat.DayNames[ (int)DayOfWeek ];
                
				if ( Interval == 1 )
                    return string.Format( "{0}, {1} {2} of every month, at {3}, starting on {4}{5}", EventCode, weekString, dayString, RunAtTime, StartDateToString(), uponDocking );

                return string.Format( "{0}, {1} {2} of every {3} months, at {4}, starting on {5}{6}", EventCode, weekString, dayString, Interval, RunAtTime, StartDateToString(), uponDocking );
            }

            return "?";
        }

#if TODO // Leave this method here for now.  We may yet still need it.
        /// <summary>
        /// Determine if the schedule should run at the specified time.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public override bool IsScheduledTimeToRun( DateTime dateTime )
        {
            const string methodName = "IsScheduledTimeToRun ";

            Log.Assert( Interval > 0,
                string.Format( "Invalid Interval ({0}) encountered in {1}.{2}", Interval, GetType(), methodName ) );

            // "Day X of every Y months?
            if ( DayOfMonth != DomainModelConstant.NullShort )
            {
                // Return true if it's the correct day, and the time of day we're supposed to run is now or has passed.
                return dateTime.Day == DayOfMonth
                && dateTime.Hour >= RunAtTime.Hours
                && dateTime.Minute >= RunAtTime.Minutes;
            }

            // "Xth DayOfWeek of every Y months"
            if ( Week != DomainModelConstant.NullShort )
            {
                // If schedule's DayOfMonth exceeds the number of days in the month, then
                // use the last day of the month, instead.
                int daysInMonth = _gregorianCalendar.GetDaysInMonth( dateTime.Year, dateTime.Month );
                int day = Math.Min( daysInMonth, DayOfMonth ); //TODO: This is never used. Why? -jmp

                DateTime xthDayOfWeek = GetXthDayOfWeek( Week, DayOfWeek, dateTime );

                // Return true if it's the correct day, and the time of day we're supposed to run is now or has passed.
                return xthDayOfWeek.Day == dateTime.Day
                && dateTime.Hour >= RunAtTime.Hours
                && dateTime.Minute >= RunAtTime.Minutes;
            }

            Log.Assert( string.Format( "Invalid data encountered in {0}.{1}", GetType(), methodName ) );

            return false;
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastRunTime">Will be converted to the passed-in TimeZoneInfo's local time if Kind is UTC.</param>
        /// <param name="dockedTime">The time the instrument was docked; if more recent than the last run, and if On Docking is true, this time is returned</param>
        /// <param name="tzi">The docking station's local time zone setting.</param>
        /// <returns></returns>
        public override DateTime CalculateNextRunTime( DateTime lastRunTime, DateTime dockedTime, TimeZoneInfo tzi )
        {
            const string methodName = "CalculateNextRunTime";

            if ( lastRunTime.Kind == DateTimeKind.Utc )
                lastRunTime = tzi.ToLocalTime( lastRunTime );

            if ( UponDocking )
            {
                // The schedule has been specified to allow for the event to be run upon docking. 
                // If the docked time is more recent than the journal run time, return the docked time to run the event.
                if ( dockedTime.Kind == DateTimeKind.Utc )
                    dockedTime = tzi.ToLocalTime( dockedTime );
                Log.Trace( "ScheduledMonthly: uponDocking = true; last run time=" + lastRunTime.ToShortDateString() + ", dockedTime=" + dockedTime.ToShortDateString() );
                if (lastRunTime < dockedTime)
                    return dockedTime;
            }
            
            // Not yet the StartDateTime? Then just return StartDateTime
            if ( lastRunTime < StartDateTime )
                return StartDateTime;

            DateTime lastRunDate = lastRunTime.Date; // we're only interested in the date portion, not the time.

            Log.Assert( Interval > 0,
                string.Format( "Invalid Interval ({0}) encountered in {1}.{2}", Interval, GetType(), methodName ) );

            // The schedule has two options... "Day X of every Y months" or 
            // ""Xth DayOfWeek of every Y months".  Determine which option it's 
            // configured for and make appropriate corresponding call to calculate.

            // "Day X of every Y months"?  e.g. "Day 2 of every 3 months, at 02:30"
            if ( DayOfMonth != DomainModelConstant.NullShort )
                return CalculateNextDayXofEveryYmonths( lastRunDate );

            // "Xth DayOfWeek of every Y months".  e.g. "4th Tuesday of every 3 months, at 02:30"
            if ( Week != DomainModelConstant.NullShort )
                return CalculateXthDayOfWeekofEveryYmonths( lastRunDate );

            Log.Assert( string.Format( "Invalid data encountered in {0}.{1}", GetType(), methodName ) );

            return DateTime.SpecifyKind( DateTime.MaxValue, DateTimeKind.Local );

        } // end-CalculateNextRunTime

        /// <summary>
        /// For specified date, calculate next "Day X of every Y months".
        /// e.g. "Day 2 of every 3 months, at 02:30"
        /// </summary>
        /// <remarks>
        /// Intended for use only as helper method to CalculateNextRunTime
        /// </remarks>
        /// <param name="lastRunDate"></param>
        /// <returns></returns>
        private DateTime CalculateNextDayXofEveryYmonths( DateTime lastRunDate )
        {
			// Determine when the last run date *should* have been within the last run date's month.
			// e.g., if lastRunDate is 5/28, but our schedule's DayOfWeek is 24, then last
			// run date *should* have been 5/24.

			// If schedule's DayOfMonth exceeds the number of days in the month, then
			// use the last day of the month, instead.
			int daysInMonth = _gregorianCalendar.GetDaysInMonth( lastRunDate.Year, lastRunDate.Month );
			int day = Math.Min( daysInMonth, DayOfMonth );

			DateTime nextRunDate = new DateTime( lastRunDate.Year, lastRunDate.Month, day );

			if ( lastRunDate < nextRunDate )
				return SetToRunAtTime( nextRunDate );

			// Add on a month, then reset the day of that month to the schedule's DayOfMonth
			// (making sure that schedules DayOfMonth isn't too large)
			nextRunDate = nextRunDate.AddMonths( Interval );
			daysInMonth = _gregorianCalendar.GetDaysInMonth( nextRunDate.Year, nextRunDate.Month );
			day = Math.Min( daysInMonth, DayOfMonth );
            nextRunDate = new DateTime( nextRunDate.Year, nextRunDate.Month, day );

            return SetToRunAtTime( nextRunDate );
        }

        /// <summary>
        /// For specified date, calculate next "Xth DayOfWeek of every Y months".
        /// e.g. "4th Tuesday of every 3 months, at 02:30" 
        /// </summary>
        /// <remarks>
        /// Intended for use only as helper method to CalculateNextRunTime
        /// </remarks>
        /// <param name="lastRunDate"></param>
        /// <returns></returns>
        private DateTime CalculateXthDayOfWeekofEveryYmonths( DateTime lastRunDate )
        {
            Log.Assert( (int)DayOfWeek >= 0, string.Format( "Invalid DayOfWeek ({0}) encountered in {1}", DayOfWeek, GetType() ) );

			// Get Xth DayOfWeek for the current month
			DateTime nextRunDate = GetXthDayOfWeek( this.Week, this.DayOfWeek, lastRunDate.Month, lastRunDate.Year );

			// Is the last run time earlier than when it needs to run for the current month?
			if ( lastRunDate < nextRunDate )
				return SetToRunAtTime( nextRunDate ); // nextRunDate will be set to midnight. Adjust to scheduled starting time.

			// Advance the proper number of months
            nextRunDate = nextRunDate.AddMonths( Interval );
            // ...then adjust to the proper Xth DayOfWeek within the new month.
            nextRunDate = GetXthDayOfWeek( this.Week, this.DayOfWeek, nextRunDate.Month, nextRunDate.Year );

            return SetToRunAtTime( nextRunDate ); // nextRunDate will be set to midnight. Adjust to scheduled starting time.
        }

    } 
}
