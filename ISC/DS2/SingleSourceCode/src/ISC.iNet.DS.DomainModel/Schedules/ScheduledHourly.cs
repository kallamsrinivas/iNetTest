using System;
using System.Globalization;
using ISC.WinCE.Logger;



namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// A recurring schedule that's supposed to run on specific days of the week,
    /// at a specific hours interval on those days.
    /// 
    /// e.g. "Every 3 hours, on Mon, Wed, Fri, at 02:30, starting on 1/8/2010"
    /// 
    /// </summary>
    public class ScheduledHourly : ScheduledDays
    {
        public ScheduledHourly( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled, bool uponDocking, short interval, DateTime startDate, TimeSpan runAtTime, bool[] days )
            : base( id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled, uponDocking, interval, startDate, runAtTime, days )
        {
 
        }

        /// <summary>
        /// Returns a descriptive string for this schedule which includes the EventCode
        /// and scheduling info.
        /// </summary>
        /// <remarks>
        /// e.g. "Every 3 hours, on Mon, Wed, Fri, at 02:30, starting on 1/8/2010"
        /// </remarks>
        /// <returns></returns>
        public override string ToString()
        {
            string uponDocking = UponDocking ? " (and Upon Docking)" : null;
            return string.Format( "{0}, Every {1} hours, on {2}, at {3}, starting on {4}{5}",
                EventCode, Interval, DaysToString(), RunAtTimeToString(), StartDateToString(), uponDocking );
        }

#if TODO // Leave this method here for now.  We may yet still need it.
        public override bool IsScheduledTimeToRun( DateTime dateTime )
        {
            throw new NotImplementedException();
        }
#endif

        private DateTime GetNextRunDay( DateTime dateTime )
        {
            dateTime = dateTime.Date;

            // Enum.GetValues is not supported by compact framework.  So we just hardcode in the range instead.
            // foreach ( DayOfWeek dayOfWeek in Enum.GetValues( typeof( DayOfWeek ) ) )
            for ( DayOfWeek day = dateTime.DayOfWeek; day <= DayOfWeek.Saturday; day++ )
            {
                // If we encounter a next day of the week that we're supposed to run on, then 
                if ( Days[ (int)day ] == true )
                    return SetToRunAtTime( dateTime );
            }

            // If we make it to here, then there were no more days in the current week that are marked for recurrence.
            // So instead, find the first day of the following week.

            // Advance 1 weeks into the future.
            DateTime nextRunDate = new GregorianCalendar().AddWeeks( dateTime.Date, 1 );

            // Now back up to Sunday of that next week.
            nextRunDate = nextRunDate.AddDays( ( (int)nextRunDate.DayOfWeek - (int)DayOfWeek.Sunday ) * -1 );

            // Now, advance to the first day of the week that is marked for recurrence

            // Enum.GetValues is not supported by compact framework.  So we just hardcode in the range instead.
            // foreach ( DayOfWeek dayOfWeek in Enum.GetValues( typeof( DayOfWeek ) ) )
            for ( DayOfWeek dayOfWeek = DayOfWeek.Sunday; dayOfWeek <= DayOfWeek.Saturday; dayOfWeek++ )
            {
                if ( Days[ (int)dayOfWeek ] )
                    return SetToRunAtTime( nextRunDate.AddDays( (int)dayOfWeek ) );
            }

            return StartDateTime;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastRunTime">Will be converted to the passed-in TimeZoneInfo's local time if Kind is UTC.</param>
        /// <param name="dockedTime">The time the instrument was docked; if more recent than the last run, and if On Docking is true, this time is returned</param>
        /// <param name="tzi">The docking station's local time zone setting.</param>
        /// <returns></returns>
        public override DateTime CalculateNextRunTime( DateTime lastRunTime, DateTime dockedTime, TimeZoneInfo tzi )
        {
            if ( lastRunTime.Kind == DateTimeKind.Utc )
                lastRunTime = tzi.ToLocalTime( lastRunTime );

            if ( UponDocking )
            {
                // The schedule has been specified to allow for the event to be run upon docking. 
                // If the docked time is more recent than the journal run time, return the docked time to run the event.
                if ( dockedTime.Kind == DateTimeKind.Utc )
                    dockedTime = tzi.ToLocalTime( dockedTime );
                Log.Trace( "ScheduledHourly: uponDocking = true; last run time=" + lastRunTime.ToShortDateString() + ", dockedTime=" + dockedTime.ToShortDateString() ); 
                if (lastRunTime < dockedTime)
                    return dockedTime;
            }

            Log.Trace("ScheduledHourly: start date time=" + StartDateTime.ToLongDateString() + " interval=" + Interval.ToString() + " days, last run time=" + lastRunTime.ToLongDateString() + ", docked time=" + dockedTime.ToShortDateString()); 

            // Not yet the StartDateTime? Then just return StartDateTime
            if ( lastRunTime < StartDateTime )
                return GetNextRunDay( StartDateTime );

            // SGF  5-Feb-2013  INS-3655 -- Corrected to begin checking for the next run time on the day prior to the last run,
            // and then advance the current time based on the interval defined for that event schedule. When a time has been
            // found that is later than the last run time, and that time occurs on a day that has been selected as a valid run day,
            // that time is returned to the caller. 
            DateTime nextRunTime = new DateTime( lastRunTime.Date.Year, lastRunTime.Date.Month, lastRunTime.Date.Day, 0, 0, 0, DateTimeKind.Local );
            nextRunTime = nextRunTime.AddDays(-1);
            nextRunTime = nextRunTime.Add(RunAtTime);

            while (nextRunTime < lastRunTime)
            {
                nextRunTime = nextRunTime.AddHours(Interval);
                DayOfWeek nextRunDayOfWeek = (DayOfWeek)nextRunTime.DayOfWeek;
                if (!Days[(int)nextRunDayOfWeek])
                    continue;
                if (nextRunTime > lastRunTime)
                    return nextRunTime;
            }

            // if we make it to here, then lastRunDate is in the future (?)

            return DateTime.SpecifyKind( DateTime.MaxValue, DateTimeKind.Local );
        }

    }
}
