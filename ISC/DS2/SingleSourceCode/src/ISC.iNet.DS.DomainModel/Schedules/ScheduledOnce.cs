using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// A "once" schedule runs one time, then the schedule is deleted from the database.
    /// e.g. "at 02:30, on 1/8/2010".
    /// </summary>
    public class ScheduledOnce : Schedule
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="eventCode"></param>
        /// <param name="startDate"></param>
        /// <param name="runAtTime"></param>
        public ScheduledOnce( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled, DateTime startDate, TimeSpan runAtTime )
            : base( id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled,
                false, // uponDocking
                DomainModelConstant.NullShort, // interval
                startDate,
                runAtTime )
        {

        }

        /// <summary>
        /// Returns a descriptive string for this schedule which includes the EventCode
        /// and scheduling info.  e.g. "BUMP Now (forced)".
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // For a "once" schedule, UponDocking will (should?) probably 
            // never be set. We handle it just in case,though.
            string uponDocking = UponDocking ? " (and Upon Docking)" : null;
            return string.Format( "{0}, At {1}, on {2}{3}", EventCode, RunAtTimeToString(), StartDateToString(), uponDocking );
        }

#if TODO // Leave this method here for now.  We may yet still need it.
        /// <summary>
        /// Determine if the schedule should run at the specified time.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public override bool IsScheduledTimeToRun( DateTime dateTime )
        {
            // TODO
            return true;  // return a date far in the past, so it's always overdue.
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastRunTime">This parameter is ignored for ScheduledOnce instances</param>
        /// <param name="dockedTime">This parameter is ignored for ScheduledOnce instances</param>
        /// <param name="tzi">The docking station's time zone setting.</param>
        /// <returns></returns>
        public override DateTime CalculateNextRunTime( DateTime lastRunTime, DateTime dockedTime, TimeZoneInfo tzi )
        {
            // SGF  27-Jan-2012  INS-2307  -- removed conversion to local time, as the DateTime is already supplied in local time
            return this.StartDateTime;
        }

    }
}
