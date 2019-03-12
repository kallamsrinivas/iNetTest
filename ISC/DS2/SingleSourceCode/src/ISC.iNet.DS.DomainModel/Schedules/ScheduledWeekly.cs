using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// A 'Weekly recurring job can be scheculed to run "Every X weeks" 
    /// on one or more  particular days in the week.
    /// 
    /// e.g., "Every week on Mon, Tue, Wed, at 02:30, starting on 1/8/2010",
    /// e.g., "Every 4 weeks on Mon, Tue, Wed, at 02:30, starting on 1/8/2010"
    ///         
    /// </summary>
    public class ScheduledWeekly : ScheduledDays
    {
        public ScheduledWeekly( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode,bool enabled, bool uponDocking,
            short interval, DateTime startDate, TimeSpan runAtTime, bool[] days )
            : base( id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled, uponDocking, interval, startDate, runAtTime, days )
        {
 
        }

        /// <summary>
        /// Returns a descriptive string for this schedule which includes the EventCode
        /// and scheduling info.
        /// </summary>
        /// <remarks>
        /// Format: e.g., "Every week on Mon, Tue, Wed, at 02:30, starting on 1/8/2010"
        ///         e.g., "Every 4 weeks on Mon, Tue, Wed, at 02:30, starting on 1/8/2010"
        /// </remarks>
        /// <returns></returns>
        public override string ToString()
        {
            string uponDocking = UponDocking ? " (and Upon Docking)" : null;

			if ( Interval == 1 )
                return string.Format( "{0}, Every week on {1}, at {2}, starting on {3}{4}", EventCode, DaysToString(), RunAtTimeToString(), StartDateToString(), uponDocking );

            return string.Format( "{0}, Every {1} weeks on {2}, at {3}, starting on {4}{5}", EventCode, Interval, DaysToString(), RunAtTimeToString(), StartDateToString(), uponDocking );
        }

#if TODO // please leave this method here for now.  We may yet still need it.
        /// <summary>
        /// Determine if the schedule should run at the specified time.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public override bool IsScheduledTimeToRun( DateTime dateTime )
        {
            return Days[ (int)dateTime.DayOfWeek ];
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
            if ( lastRunTime.Kind == DateTimeKind.Utc )
                lastRunTime = tzi.ToLocalTime( lastRunTime );

            if ( UponDocking )
            {
                // The schedule has been specified to allow for the event to be run upon docking. 
                // If the docked time is more recent than the journal run time, return the docked time to run the event.
                if ( dockedTime.Kind == DateTimeKind.Utc )
                    dockedTime = tzi.ToLocalTime( dockedTime );
                Log.Trace( "ScheduledWeekly: uponDocking = true; last run time=" + lastRunTime.ToShortDateString() + ", dockedTime=" + dockedTime.ToShortDateString() );
                if (lastRunTime < dockedTime)
                    return dockedTime;
            }


            // Not yet the StartDateTime? Then just return StartDateTime
            if ( lastRunTime < StartDateTime )
                return StartDateTime;

            DateTime nextRunDate = lastRunTime.Date; // we're only interested in the date portion, not the time.

            // First, see if this schedule needs to run for any more days in the week
            int daysCount = 1;
            for ( DayOfWeek dayOfWeek = lastRunTime.DayOfWeek + 1; dayOfWeek <= DayOfWeek.Saturday; dayOfWeek++, daysCount++ )
            {
                if ( Days[ (int)dayOfWeek ] )
                    return SetToRunAtTime( nextRunDate.AddDays( daysCount ) );
            }

            // Advance Interval weeks into the future...
            nextRunDate = _gregorianCalendar.AddWeeks( nextRunDate, Interval );
            // ...and wow back up to Sunday of that future week.
            nextRunDate = nextRunDate.AddDays(  ( (int)nextRunDate.DayOfWeek - (int)DayOfWeek.Sunday ) * -1 );

            // Now, advance to the first day of the week that is marked for recurrence

            // Enum.GetValues is not supported by compact framework.  So we just hardcode in the range instead.
            // foreach ( DayOfWeek dayOfWeek in Enum.GetValues( typeof( DayOfWeek ) ) )
            for ( DayOfWeek dayOfWeek = DayOfWeek.Sunday; dayOfWeek <= DayOfWeek.Saturday; dayOfWeek++ )
            {
                if ( Days[ (int)dayOfWeek ] )
                    return SetToRunAtTime( nextRunDate.AddDays( (int)dayOfWeek ) );
            }

            Log.Assert( "No recurring DayOfWeek found in " + GetType() + ".CalculateNextRunTime" );

            return DateTime.SpecifyKind( DateTime.MaxValue, DateTimeKind.Local );
        }
    }
}
