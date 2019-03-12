using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;


namespace ISC.iNet.DS.DomainModel
{

    /// <summary>
    /// An abstract class used as a subclass for schedules that are
    /// specified to run only on specific days of the week.
    /// </summary>
    public abstract class ScheduledDays : Schedule
    {
        private readonly bool[] _days = new bool[ 7 ];

        protected ScheduledDays( long id, long refId, string name, EventCode eventCode, string equipmentCode, string equipmentSubTypeCode, bool enabled, bool uponDocking,
            short interval, DateTime startDate, TimeSpan runAtTime, bool[] days )
            : base( id, refId, name, eventCode, equipmentCode, equipmentSubTypeCode, enabled, uponDocking, interval, startDate, runAtTime )
        {
            _days = days;
        }

        /// <summary>
        /// Array indicating which days of the week are scheduled to recur.
        /// Element 0 is Sunday, Element 1 is Monday, etc. (A suggested convenience,
        /// is to access the array using the DayOfWeek enumeration; e.g.
        /// "Days[ DayOfWeek.Monday ]").
        /// </summary>
        public bool[] Days { get { return _days; } }

        protected string DaysToString()
        {
            StringBuilder daysBuilder = new StringBuilder();

            // Generate a comma seperated list of day names.
            // Enum.GetValues is not supported by compact framework.  So we just hardcode in the range instead.
            // foreach ( DayOfWeek dayOfWeek in Enum.GetValues( typeof( DayOfWeek ) ) )
            for ( DayOfWeek dayOfWeek = DayOfWeek.Sunday; dayOfWeek <= DayOfWeek.Saturday; dayOfWeek++ )
            {
                if ( !Days[ (int)dayOfWeek ] )
                    continue;

                if ( daysBuilder.Length > 0 )
                    daysBuilder.Append( ", " );

                // Use the current culture to make sure the day name is localized.
                string abbreviatedDayName = CultureInfo.InvariantCulture.DateTimeFormat.ShortestDayNames[ (int)dayOfWeek ];

                daysBuilder.Append( abbreviatedDayName );
            }

            string daysOfWeek = ( daysBuilder.Length > 0 ) ? daysBuilder.ToString() : "?";

            return daysOfWeek;
        }
    }
}
