using System;
using System.Collections.Generic;
using System.Text;



namespace ISC.iNet.DS.DomainModel
{
    public class ScheduledUponDocking : Schedule
    {
        public ScheduledUponDocking( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled )
            : base( id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled,
                true, // uponDocking
                DomainModelConstant.NullShort, // interval
                DateTime.MinValue, // start date
                TimeSpan.MinValue ) // runAtTime
        {
        }

        /// <summary>
        /// Returns a descriptive string for this schedule which includes the EventCode
        /// and scheduling info.  e.g. "BUMP Upon Docking"
        /// </summary>
        public override string ToString()
        {
            return EventCode + " Upon Docking";
        }

#if TODO // Leave this method here for now.  We may yet still need it.
        public override bool IsScheduledTimeToRun( DateTime dateTime )
        {
            throw new NotImplementedException();
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lastRunTime">Will be converted to the passed-in TimeZoneInfo's local time if Kind is UTC.</param>
        /// <param name="dockedTime">Will be converted to the passed-in TimeZoneInfo's local time if Kind is UTC.</param>
        /// <param name="tzi">The docking station's local time zone setting.</param>
        /// <returns>
        /// If the last time it was run is prior to when it was docked, then it's overdue,
        /// so MinValue is returned to ensure it's considered overdue.
        /// Otherwise, it's already been run since being docked,
        /// so MaxValue is returned to ensure it doesn't run again.
        /// </returns>
        public override DateTime CalculateNextRunTime( DateTime lastRunTime, DateTime dockedTime, TimeZoneInfo tzi )
        {
            if ( lastRunTime.Kind == DateTimeKind.Utc )
                lastRunTime = tzi.ToLocalTime( lastRunTime );

            if ( dockedTime.Kind == DateTimeKind.Utc )
                dockedTime = tzi.ToLocalTime( dockedTime );

            // If the last time it was run is prior to the docked time, then it's overdue.
            // So return MinValue to ensure it's seen as overdue.
            // Otherwise, it's already been run since it was last docked,
            // so return MaxValue to ensure it doesn't run again.
            return ( lastRunTime < dockedTime )
                ? DateTime.SpecifyKind( DateTime.MinValue, DateTimeKind.Local )
                : DateTime.SpecifyKind( DateTime.MaxValue, DateTimeKind.Local );
        }
    }
}
