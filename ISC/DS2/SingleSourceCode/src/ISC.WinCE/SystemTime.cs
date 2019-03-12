using System;
using System.Runtime.InteropServices;
using System.Threading;


namespace ISC.WinCE
{
    

    /// <summary>
    /// Mimics WinCE's SYSTEMTIME structure and provides access to WinCE's Get/SetSystemTime API calls.
    /// </summary>
    [ StructLayout( LayoutKind.Sequential ) ]
    public struct SystemTime : ICloneable
    {
        public short Year;
        public short Month;
        public short DayOfWeek;
        public short Day;
        public short Hour;
        public short Minute;
        public short Second;
        public short Milliseconds;

        /// <summary>
        /// returns the current system date and time. The system time is expressed in UTC.
        /// </summary>
        /// <param name="systemTime"></param>
        [DllImport( "coredll.dll" )]
        private static unsafe extern void GetSystemTime( SystemTime* systemTime );


        /// <summary>
        /// sets the current system time and date. The system time is expressed in UTC.
        /// </summary>
        /// <param name="systemTime"></param>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        private static unsafe extern int SetSystemTime( SystemTime* systemTime );

        /// <summary>
        /// returns the current local date and time.
        /// </summary>
        /// <param name="systemTime"></param>
        [DllImport( "coredll.dll" )]
        private static unsafe extern void GetLocalTime( SystemTime* systemTime );

        /// <summary>
        /// sets the current local time and date.
        /// </summary>
        /// <param name="systemTime"></param>
        /// <returns>Nonzero indicates success. Zero indicates failure. To get extended error information, call Marshal.GetLastWin32Error().</returns>
        [DllImport( "coredll.dll", SetLastError = true )]
        private static unsafe extern int SetLocalTime( SystemTime* systemTime ); 


        /// <summary>
        /// Sets all members to zero
        /// </summary>
        public void Clear()
        {
            Year = Month = DayOfWeek = Day = Hour = Minute = Second = Milliseconds = 0;
        }


        /// <summary>
        /// Returns a value indicating whether two SystemTime instances
        /// are equal (contain the same data values).
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals( object obj )
        {
            if ( !( obj is SystemTime ) )
                return false;

            SystemTime systemTime = (SystemTime)obj;

            return systemTime.Day == this.Day
            && systemTime.DayOfWeek == this.DayOfWeek
            && systemTime.Hour == this.Hour
            && systemTime.Milliseconds == this.Milliseconds
            && systemTime.Minute == this.Minute
            && systemTime.Month == this.Month
            && systemTime.Second == this.Second
            && systemTime.Year == this.Year;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// This isn't a proper hashcode.  It is only provided to suppress
        /// warning that occurs when an override of Equals() is added without
        /// a corresponding override to gethashcode
        /// </remarks>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Year;
        }

        /// <summary>
        /// Outputs the time in the format "YYYY-MM-DD HH:MM:SS:mmm, DayOfWeek x" where hours are 00-23.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format( "{0}-{1}-{2} {3}:{4}:{5}.{6}, DayOfWeek {7}",
                Year.ToString( "d4" ), Month.ToString( "d2" ), Day.ToString( "d2" ),
                Hour.ToString( "d2" ), Minute.ToString( "d2" ), Second.ToString( "d2" ), Milliseconds.ToString( "d3" ),
                DayOfWeek );
        }

        public bool IsEmpty()
        {
            return Year == 0 && Month == 0 && DayOfWeek == 0 && Day == 0 && Hour == 0 && Minute == 0 && Second == 0 && Milliseconds == 0;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        /// <summary>
        /// Returns the current system date and time in a SystemTime instance.
        /// The system time is expressed in UTC.
        /// </summary>
        /// <returns></returns>
        public static SystemTime UtcNow
        {
            get
            {
                SystemTime systemTime = new SystemTime();
                unsafe
                {
                    GetSystemTime( &systemTime );
                }
                return systemTime;
            }
        }

        /// <summary>
        /// Returns the current date and time in a DateTime instance.
        /// The system time is expressed in UTC.
        /// </summary>
        public static DateTime DateTimeUtcNow
        {
            get
            {
                return DateTime.SpecifyKind( UtcNow.ToDateTime(), DateTimeKind.Utc );
            }
        }

        /// <summary>
        /// Convert to a DateTime instance.  Note that the Kind property will be set
        /// to DateTimeKind.Utc.
        /// </summary>
        /// <returns></returns>
        private DateTime ToDateTime()
        {
            return new DateTime( Year, Month, Day, Hour, Minute, Second, Milliseconds );

        }

        /// <summary>
        /// Sets the current system time and date. The system time is expressed in UTC.
        /// </summary>
        /// <param name="systemTime"></param>
        /// <returns>
        /// Nonzero indicates success. Zero indicates failure.
        /// To get extended error information, call Marshal.GetLastWin32Error().
        /// </returns>
        public static int SetSystemTime( SystemTime systemTime )
        {
            int success = 0;
            unsafe
            {
                success = SetSystemTime( &systemTime );
            }

            // Whenever we set the clock, it can take a moment up to a few seconds to update.
			// Typically, this delay is only related to changing timezones which is not done in 
			// the DS since its clock is always in UTC.  To improve D2G, we are no longer waiting 
			// 5 seconds which isn't required when only the time is changed.

            return success;
        }

        /// <summary>
        /// Sets the current system time and date. The system time is expressed in UTC.
        /// </summary>
        /// <param name="systemDateTime"></param>
        /// <returns>
        /// Nonzero indicates success. Zero indicates failure.
        /// To get extended error information, call Marshal.GetLastWin32Error().
        /// </returns>
        public static int SetSystemTime( DateTime systemDateTime )
        {
            SystemTime systemTime = new SystemTime( systemDateTime );
            systemTime.DayOfWeek = 0; // ? what's this for?

            return SystemTime.SetSystemTime( systemTime );
        }


        public SystemTime( DateTime dateTime )
        {
            this.DayOfWeek = (short)dateTime.DayOfWeek;

            this.Year = (short)dateTime.Year;
            this.Month = (short)dateTime.Month;
            // dayofweek is ignored when setting the clock
            //this.DayOfWeek = (short)dateTime.DayOfWeek;
            this.Day = (short)dateTime.Day;
            this.Hour = (short)dateTime.Hour;
            this.Minute = (short)dateTime.Minute;
            this.Second = (short)dateTime.Second;
            this.Milliseconds = (short)dateTime.Millisecond;
        }
    }
}
