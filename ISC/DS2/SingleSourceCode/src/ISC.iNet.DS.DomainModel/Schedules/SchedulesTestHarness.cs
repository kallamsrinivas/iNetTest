using System;


namespace ISC.iNet.DS.DomainModel
{
    public class SchedulesTestHarness
    {
        DateTime _now = DateTime.UtcNow;
        DateTime _today = DateTime.Today;

        public SchedulesTestHarness()
        {
        }

        public void TestHourly()
        {
            ScheduledHourly hourly = new ScheduledHourly( 0, 1, "hourly", EventCode.GetCachedCode( EventCode.BumpTest ), "MX6", null,
                true, // enabled
                false, // uponDocking -- change to true if testing that 
                3, // interval
                DateTime.Today.AddDays( 3 ), // StartDate
                new TimeSpan( 2, 0, 0 ),
                new bool[] { false, false, true, false, false, false, false } );// runAtTime

            DateTime next = hourly.CalculateNextRunTime( _now, _now, TimeZoneInfo.GetEastern() );



        }
            
    }
}
