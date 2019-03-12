using System;
using System.Collections.Generic;
using System.Text;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// A "now" schedule is to run ASAP.  ScheduledNow instances are not stored in the
    /// database; they memory resident only (held by the Scheduler)
    /// </summary>
    public class ScheduledNow : Schedule
    {
        public ScheduledNow( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled )
            : base( id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled,
                false, // uponDocking 
                DomainModelConstant.NullShort, // interval
                DateTime.MinValue, // start date
                TimeSpan.Zero ) // runAtTime
        {
 
        }

        /// <summary>
        /// Returns a descriptive string for this schedule which includes the EventCode
        /// and scheduling info.  e.g. "BUMP Now (forced)".
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return EventCode + " Now (forced)";
        }

#if TODO // Leave this method here for now.  We may yet still need it.
        /// <summary>
        /// Determine if the schedule should run at the specified time.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public override bool IsScheduledTimeToRun( DateTime dateTime )
        {
            return true;  // return a date far in the past, so it's always overdue.
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastRunDate">This parameter is ignored for ScheduledNow instances</param>
        /// <param name="dockedTime">This parameter is ignored for ScheduledNow instances</param>
        /// <param name="tzi">This parameter is ignored for ScheduledNow instances</param>
        /// <returns>This method always returns DateTime.MinValue, since "now" schedules are always overdue.</returns>
        public override DateTime CalculateNextRunTime( DateTime lastRunDate, DateTime dockedTime, TimeZoneInfo tzi )
        {
            return DateTime.SpecifyKind( DateTime.MinValue, DateTimeKind.Local );

        } // end-CalculateNextRunTime

    }
}
