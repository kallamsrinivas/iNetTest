using System;
using ISC.iNet.DS.DomainModel;
using ISC.Instrument.Driver;
using TimeZoneInfo = ISC.iNet.DS.DomainModel.TimeZoneInfo;


namespace ISC.iNet.DS.Instruments
{
    /// <summary>
    /// IDateTimeProvider implementation passed on to all InstrumentDriver instances,
    /// for use with the CompactFramework.
    /// </summary>
    public class DateTimeProvider : IDateTimeProvider
    {
        private TimeZoneInfo _tzi;

        public DateTimeProvider( TimeZoneInfo tzi )
        {
            _tzi = (TimeZoneInfo)tzi.Clone();
        }

        /// Gets a DateTime that is set to the current date and time, expressed as local time.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public DateTime LocalNow
        {
            get
            {
                return DateTime.SpecifyKind( _tzi.ToLocalTime( DateTime.UtcNow ), DateTimeKind.Local );
            }
}

        /// <summary>
        /// Gets the current date, with the time component set to 00:00:00.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public DateTime LocalToday
        {
            get
            {
                DateTime now = this.LocalNow;
                DateTime today = new DateTime( now.Year, now.Month, now.Day, 0, 0, 0 );
                return DateTime.SpecifyKind( today, DateTimeKind.Local );
            }
        }
    }
}
