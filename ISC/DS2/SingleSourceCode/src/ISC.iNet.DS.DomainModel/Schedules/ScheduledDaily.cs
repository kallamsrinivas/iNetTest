using System;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// A recurring schedule that runs every so many days at a specific time of day..
    /// 
    /// i.e., "Every X Days at Y time, starting on SomeDate".
    /// 
    /// e.g., "Every day at 02:30, starting on 1/8/2010",
    ///       "Every 7 days at 02:30, starting on 1/8/2010"
    ///      
    /// </summary>
    /// <remarks>
    /// If specifying 'every weekday', then the Interval and StartDate properties
    /// are ignored.
    /// </remarks>
    public class ScheduledDaily : Schedule
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="refId"></param>
        /// <param name="name"></param>
        /// <param name="eventCode"></param>
        /// <param name="enabled"></param>
        /// <param name="uponDocking"></param>
        /// <param name="interval">How often (in days) to run. i.e., run every 'interval' days.</param>
        /// <param name="startDate">Date that it's first allowed to run.</param>
        /// <param name="runAtTime">Time of day it should run.</param>
        public ScheduledDaily( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled, bool onDocked, short interval, DateTime startDate, TimeSpan runAtTime )
            : base(id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled, onDocked, interval, startDate, runAtTime)
        {

        }

        /// <summary>
        /// Returns a descriptive string for this schedule which includes the EventCode
        /// and scheduling info.
        /// </summary>
        /// <returns></returns>
        /// </summary>
        /// <remarks>
        /// e.g. "Every day at 02:30, starting on 1/8/2010",
        ///      "Every 7 days at 02:30, starting on 1/8/2010",
        /// </remarks>
        /// <returns></returns>
        public override string ToString()
        {
            string uponDocking = UponDocking ? " (and Upon Docking)" : null;

            if ( Interval == 1 )
                return string.Format( "{0}, Every day at {1}, starting on {2}{3}", EventCode, RunAtTimeToString(), StartDateToString(), uponDocking );

            return string.Format( "{0}, Every {1} days at {2}, starting on {3}{4}", EventCode, Interval, RunAtTimeToString(), StartDateToString(), uponDocking );
        }

#if TODO // Leave this method here for now.  We may yet still need it.
        /// <summary>
        /// Determine if the schedule should run at the specified time.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public override bool IsScheduledTimeToRun( DateTime dateTime )
        {
            throw new ApplicationException( "TODO: not fully converted" );

            // TODO
            // "Every X days, Starting on SomeDate"  e.g. "Every 7 days at 02:30",

            return dateTime.Date >= StartDate;
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
                Log.Trace( "ScheduledDaily: UponDocking = true; last run time=" + lastRunTime.ToShortDateString() + ", dockedTime=" + dockedTime.ToShortDateString() );
                if (lastRunTime < dockedTime)
                    return dockedTime;
            }

            // Not yet the StartDateTime? Then just return StartDateTime
            if ( lastRunTime < StartDateTime )
                return StartDateTime;

            DateTime lastRunDate = lastRunTime.Date; // we're only interested in the date portion, not the time.

            // SGF  26-Jan-2012  INS-2390 -- Revised the calculations for next run date for Every X Days
            DateTime nextRunDate = lastRunDate.AddDays(Interval);
            Log.Trace("ScheduledDaily: interval=" + Interval.ToString() + " days, last run date=" + lastRunDate.ToShortDateString() + ", next run date=" + nextRunDate.ToShortDateString());
            return SetToRunAtTime(nextRunDate);
        }

    }
}
