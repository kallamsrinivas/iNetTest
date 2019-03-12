using System;
using System.Globalization;
using ISC.WinCE;


namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Class which holds information about a TimeZone. Both a name and 
    /// a TimeZoneStruct are included.
    /// </summary>
    /// <remarks>
    /// <para>This class is a slightly modified version of the TimeZoneInfo class 
    /// offered that was once offered as a freeware library at crankedup.com.
    /// </para>
    /// <para>
    /// Notes from crankedup.com...
    /// </para>
    /// <para>This C# library for the Microsoft .NET Framework is based on an existing library by 
    /// Anson Goldade (original version available from GotDotNet.com" ... "Anson's library is very 
    /// capable but I needed to make a few changes to the classes to properly support my application.
    /// </para>
    /// <para>This library is necessary because neither the System.Globalization namespace nor the 
    /// System.TimeZone class support any TimeZone information except for the local machine settings
    /// No conversions between different time zones are possible with either 1.0 or 1.1 of the .NET Framework.
    /// This library makes use of Win32 API functions documented in the Microsoft Platform SDK.
    /// <para>Anson's original description:
    /// </para>
    /// "Converts local time in one time-zone to local time in another (or UTC). Resembles the TimeZone
    /// object in .NET but adds the conversion functions. Uses WindowsAPI functions on the platforms
    /// where they are available and when not available, the calculation is done manually."
    /// </para>
    /// </remarks>
    public class TimeZoneInfo : ICloneable
    {
        private int _bias;
        private string _standardName;
        private SystemTime _standardDate;
        private int _standardBias;
        private string _daylightName;
        private SystemTime _daylightDate;
        private int _daylightBias;

        public int Bias
        {
            get { return _bias; }
            private set { _bias = value; }
        }

        public string StandardName
        {
            get { return _standardName; }
            private set { _standardName = value; }
        }

        public SystemTime StandardDate
        {
            get { return _standardDate; }
            private set { _standardDate = value; }
        }

        public int StandardBias
        {
            get { return _standardBias; }
            private set { _standardBias = value; }
        }

        public string DaylightName
        {
            get { return _daylightName; }
            private set { _daylightName = value; }
        }

        public SystemTime DaylightDate
        {
            get { return _daylightDate; }
            private set { _daylightDate = value; }
        }

        public int DaylightBias
        {
            get { return _daylightBias; }
            private set { _daylightBias = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bias"></param>
        /// <param name="stdName"></param>
        /// <param name="stdDate">Month, Day, Hour, and DayOfWeek need to be filled in.</param>
        /// <param name="stdBias"></param>
        /// <param name="dstName"></param>
        /// <param name="dstDate">Month, Day, Hour, and DayOfWeek need to be filled in.</param>
        /// <param name="dstBias"></param>
        public TimeZoneInfo( int bias, string stdName, SystemTime stdDate, int stdBias, string dstName, SystemTime dstDate, int dstBias )
        {
            Bias = bias;
            StandardName = stdName;
            StandardBias = stdBias;
            StandardDate = stdDate;
            DaylightName = dstName;
            DaylightDate = dstDate;
            DaylightBias = dstBias;
        }

        public object Clone()
        {
            TimeZoneInfo tzi = (TimeZoneInfo)this.MemberwiseClone();

            // deep-clone the SystemTimes
            tzi.StandardDate = (SystemTime)this.StandardDate.Clone();
            tzi.DaylightDate = (SystemTime)this.DaylightDate.Clone();

            return tzi;
        }

        /// <summary>
        /// Return a TimeZoneInfo instance that can be
        /// used to represent UTC (Coordinated Universal Time).
        /// </summary>
        /// <returns></returns>
        public static TimeZoneInfo GetUTC()
        {
            TimeZoneInfo tzi = new TimeZoneInfo( 0, "Coordinated Universal Time", new SystemTime(), 0, "Coordinated Universal Time", new SystemTime(), 0 );
            return tzi;
        }

        /// <summary>
        /// Return a TimeZoneInfo instance that can be
        /// used to represent the U.S. Eastern time zone.
        /// </summary>
        /// <returns></returns>
        public static TimeZoneInfo GetEastern()
        {
            SystemTime stdDate = new SystemTime();
            stdDate.Day = 1;
            stdDate.DayOfWeek = 0;
            stdDate.Hour = 2;
            stdDate.Month = 11;

            SystemTime dstDate = new SystemTime();
            dstDate.Day = 2;
            dstDate.DayOfWeek = 0;
            dstDate.Hour = 2;
            dstDate.Month = 3;

            TimeZoneInfo tzi = new TimeZoneInfo( 300, "Eastern Standard Time", stdDate, 0, "Eastern Daylight Time", dstDate, -60 );

            return tzi;
        }

#if DEBUG
        // Used For testing purposes.
        public static TimeZoneInfo GetPacific()
        {
            SystemTime stdDate = new SystemTime();
            stdDate.Day = 1;
            stdDate.DayOfWeek = 0;
            stdDate.Hour = 2;
            stdDate.Month = 11;

            SystemTime dstDate = new SystemTime();
            dstDate.Day = 2;
            dstDate.DayOfWeek = 0;
            dstDate.Hour = 2;
            dstDate.Month = 3;

            TimeZoneInfo tzi = new TimeZoneInfo( 480, "Pacific Standard Time", stdDate, 0, "Pacific Daylight Time", dstDate, -60 );

            return tzi;
        }
#endif

        /// <summary>
        /// Returns a value indicating whether two TimeZoneInfo instances
        /// are equal (contain the same data values).
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override Boolean Equals( Object obj )
        {
            if ( !( obj is TimeZoneInfo ) )
                return false;

            TimeZoneInfo tzi = (TimeZoneInfo)obj;

            return ( this.Bias == tzi.Bias )
            && this.StandardDate.Equals( tzi.StandardDate )
            && ( this.StandardBias == tzi.StandardBias )
            && this.DaylightDate.Equals( tzi.DaylightDate )
            && ( this.DaylightBias == tzi.DaylightBias )
            && this.StandardName == tzi.StandardName
            && this.DaylightName == tzi.DaylightName;
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
            return Bias;
        }

        public override string ToString()
        {
            string str = string.Format( "Bias:{0},StdName:\"{1}\",StdDate:{2},StdBias:{3},DayName:\"{4}\",DayDate:{5},DayBias:{6}",
                Bias, StandardName, StandardDate, StandardBias, DaylightName, DaylightDate, DaylightBias );
            return str;
        }

        #region static conversion functions

        /// <summary>
        /// Converts the local time of the source time-zone to the local time of the
        /// destination time-zone.
        /// </summary>
        /// <param name="source">The source time-zone</param>
        /// <param name="destination">The destination time-zone</param>
        /// <param name="sourceLocalTime">The local time of the source time-zone that is
        /// to be converted to the local time in the destination time-zone</param>
        /// <returns>The local time in the destination time-zone.</returns>
        public static DateTime Convert( TimeZoneInfo source, TimeZoneInfo destination,
            DateTime sourceLocalTime )
        {
            //since we now have the UtcTime, we can forward the call to the ConvertUtcTimeZone
            //method and return that functions return value
            return TimeZoneInfo.ToLocalTime( destination,
                TimeZoneInfo.ToUniversalTime( source, sourceLocalTime ) );
        }

        /// <summary>
        /// Converts the UtcTime to the local time of the destination time-zone.
        /// </summary>
        /// <param name="destination">The destination time-zone</param>
        /// <param name="utcTime">Utc time that is to be converted to the local time of
        /// the destination time-zone.</param>
        /// <returns>DateTime that represents the local time in the destination time-zone</returns>
        public static DateTime ToLocalTime( TimeZoneInfo destination, DateTime utcTime )
        {
            // Min and Max value are treated as 'special' dates that should never need converted.
            if ( utcTime == DateTime.MinValue || utcTime == DateTime.MaxValue )
                return DateTime.SpecifyKind( utcTime, DateTimeKind.Local );

            //first we must convert the utcTime to the local time without any regard to the
            //daylight saving issues.  We'll deal with that next
            DateTime localTime = utcTime.AddMinutes( -destination.Bias );

            // Need to set it to kind of Local, because System.TimeZone.IsDaylightSavingTime
            // will return false if the time passed into it is not Local.
            localTime = DateTime.SpecifyKind( localTime, DateTimeKind.Local );

            //now we must determine if the specified local time is during the daylight saving
            //time period.  If it is, then we add the Bias and Daylight bias to the value, otherwise
            //we add the Bias and Standard bias (I believe that StandardBias is always 0)				
            if ( destination.IsDaylightSavingTime( localTime ) )
            {
                return localTime.AddMinutes( -destination.DaylightBias );
            }
            else
            {
                return localTime.AddMinutes( -destination.StandardBias );
            }
        }

		/// <summary>
		/// Converts a local time of the source time-zone to a Utc time
		/// </summary>
		/// <param name="source">The source time-zone</param>
		/// <param name="sourceLocalTime">The local time in the source time-zone</param>
		/// <returns>The Utc time that is equivalent to the local time in the source time-zone.</returns>
        public static DateTime ToUniversalTime( TimeZoneInfo source, DateTime sourceLocalTime )
        {
            // Min and Max value are treated as 'special' dates that should never need converted.
            if ( sourceLocalTime == DateTime.MinValue || sourceLocalTime == DateTime.MaxValue )
                return DateTime.SpecifyKind( sourceLocalTime, DateTimeKind.Utc );

            //first we must determine if the specified local time is during the daylight saving
            //time period.  If it is, then we add the Bias and Daylight bias to the value, otherwise
            //we add the Bias and Standard bias (I believe that StandardBias is always 0)

            DateTime utcTime;
            if ( source.IsDaylightSavingTime( sourceLocalTime ) )
            {
                utcTime = sourceLocalTime.AddMinutes( source.Bias + source.DaylightBias );
            }
            else
            {
                utcTime = sourceLocalTime.AddMinutes( source.Bias + source.StandardBias );
            }

            return DateTime.SpecifyKind( utcTime, DateTimeKind.Utc );
        }

        /// <summary>
        /// Calculates the date that a time change is going to occur given the year and
        /// the SystemTime structure that represents either the StandardDate or DaylightDate
        /// values in the TimeZoneInfo structure
        /// </summary>
        /// <param name="year">The year to calculate the change for</param>
        /// <param name="changeTime">The SystemTime structure that contains information
        /// for calculating the date a time change is to occur.</param>
        /// <returns>A DateTime object the represents when a time change will occur</returns>
        /// <remarks>Returns DateTime.MinValue when no time change is to occur</remarks>
        private static DateTime GetChangeDate( int year, SystemTime changeTime )
        {
            //if there is no change month specified, then there is no change to calculate
            //so we will return the minimun DateTime
            if ( changeTime.Month == 0 ) return DateTime.MinValue;

            DateTime changeDate;
            //if the the day value is anything less than 5, then we are going to calculate
            //from the start of the month, otherwise we are going to calculate from the beginning
            //of the next month
            if ( changeTime.Day < 5 )
            {
                //create a date that is the first date of the month when time change occurs
                changeDate = new DateTime( year, changeTime.Month, 1, changeTime.Hour, 0, 0 );
                //if the day of week of the current changeDate is less than the DayOfWeek of
                //the changeTime, then we can just subtract the two values.
                int diff = 0;
                if ( (short)changeDate.DayOfWeek <= changeTime.DayOfWeek )
                {
                    diff = changeTime.DayOfWeek - (int)changeDate.DayOfWeek;
                }
                else
                {
                    diff = 7 - ( (int)changeDate.DayOfWeek - changeTime.DayOfWeek );
                }
                //add the number of days in difference plus 7 * (Day - 1)
                changeDate = changeDate.AddDays( diff + ( 7 * ( changeTime.Day - 1 ) ) );
            }
            else
            {
                //create a date that is the first date of the month when the time change occurs
                changeDate = new DateTime( year, changeTime.Month + 1, 1, changeTime.Hour, 0, 0 );
                //if the day of week of the current changeDate is less than the DayOfWeek of
                //the changeTime, then we can just subtract the two values.
                if ( (short)changeDate.DayOfWeek > changeTime.DayOfWeek )
                {
                    //subtract whatever the last DayOfWeek value is from the current DayOfWeek value
                    changeDate = changeDate.AddDays( -( (int)changeDate.DayOfWeek -
                        changeTime.DayOfWeek ) );
                }
                else
                {
                    //get the difference in days for the DayOfWeek values and then subtract
                    //that difference from 7 to get the number of days we have to adjust
                    changeDate = changeDate.AddDays( -( 7 - ( changeTime.DayOfWeek -
                        (int)changeDate.DayOfWeek ) ) );
                }
            }

            return changeDate;
        }

        #endregion static conversion functions

        #region instance methods

        /// <summary>
        /// Converts the local time of the current time-zone to the local time of the 
        /// destination time-zone.
        /// </summary>
        /// <param name="destination">The destination time-zone</param>
        /// <param name="sourceLocalTime">The local time in the current time zone that is 
        /// to be converted to the local time in the destination time-zone</param>
        /// <returns>The local time in the destination time-zone.</returns>
        public DateTime Convert( TimeZoneInfo destination, DateTime localTime )
        {
            return TimeZoneInfo.Convert( this, destination, localTime );
        }

        /// <summary>
        /// Converts a local time of this time-zone to a Utc time
        /// </summary>
        /// <param name="localTime">The local time in the source time-zone</param>
        /// <returns>The Utc time that is equivalent to the local time in the source time-zone.</returns>
        public DateTime ToUniversalTime( DateTime localTime )
        {
            return TimeZoneInfo.ToUniversalTime( this, localTime );
        }

        /// <summary>
        /// Convert the specified UTC time stamp to this time zone's local time.
        /// </summary>
        /// <param name="utcTime"></param>
        public DateTime ToLocalTime( DateTime utcTime )
        {
            return TimeZoneInfo.ToLocalTime( this, utcTime );
        }

        /// <summary>
		/// Convert the specified UTC time stamp to this time zone's local time.
        /// </summary>
        /// <param name="utcTime"></param>
        /// <returns>if utcTime is null, then DateTime.MinValue is returned.</returns>
        public DateTime ToLocalTime( DateTime? utcTime )
        {
            if ( utcTime == null )
                return DateTime.SpecifyKind( DateTime.MinValue, DateTimeKind.Local );

            return TimeZoneInfo.ToLocalTime( this, (DateTime)utcTime );
        }

        /// <summary>
        /// Wether daylight saving time is observed in the time-zone
        /// </summary>
        public bool ObservesDaylightTime
        {
            get
            {
                //if the month value of the TimeZoneInfo structure is 0, then it doesn't
                //observer daylight savings time
                return this.DaylightDate.Month != 0;
            }
        }

        /// <summary>
        /// The name of the current TimeZoneInfo structure (Daylight or Standard) based on the date/time.
        /// </summary>
        /// <param name="time">The time to evaluate determine the correct time zone name from</param>
        /// <returns>Either the standard or daylight name of the time zone</returns>
        public string GetTimeZoneName( DateTime time )
        {
            //if the provided time is during the daylight savings time, then return
            //the DaylightName, otherwise return the StandardName
            if ( this.IsDaylightSavingTime( time ) )
            {
                return this.DaylightName;
            }
            else
            {
                return this.StandardName;
            }
        }

        /// <summary>
        /// Returns a value indicating whether the specified date and time is within a 
        /// daylight saving time period.
        /// </summary>
        /// <param name="time">DateTime to evaluate</param>
        /// <returns>True if the time value occurs during the daylight saving time
        /// period for the given year, otherwise false.</returns>
        /// <remarks>The summary description is lifted right from the MSDN docs for the
        /// same method on the TimeZone class.</remarks>
        public bool IsDaylightSavingTime( DateTime time )
        {
            DaylightTime daylightTime = this.GetDaylightChanges( time.Year );
            if ( daylightTime == null )
            {
                //if there is on daylight saving time, return false
                return false;
            }
            else
            {
                //forward the call to the overloaded methed with the daylightTime
                //class we constructed to perform the lifting for us
                return IsDaylightSavingTime( time, daylightTime );
            }
        }

        /// <summary>
        /// Returns a value indicating whether the specified date and time is within a 
        /// daylight saving time period.
        /// </summary>
        /// <param name="time">DateTime to evaluate</param>
        /// <param name="daylightTime">The DaylightTime object that represents a daylight time
        /// period.</param>
        /// <returns>True if the time value occurs during the daylight saving time
        /// period for the given year, otherwise false.</returns>
        /// <remarks>The summary description is lifted right from the MSDN docs for the
        /// same method on the TimeZone class.</remarks>
        public static bool IsDaylightSavingTime( DateTime time, DaylightTime daylightTime )
        {
            //if a null DaylightTime object was passed in, then we need to throw an
            //exception
            if ( daylightTime == null )
                throw new ArgumentNullException( "daylightTime cannot be null" );
            //determine if the date passed in is between the start and end date
            //I'm pretty sure they are just doing >= and <= comparisons, but I'll 
            //let the framework class do the work
            return ( System.TimeZone.IsDaylightSavingTime( time, daylightTime ) );
        }

        /// <summary>
        /// The date of the standard time change
        /// </summary>
        /// <param name="year">The year to calculate the standard change for</param>
        /// <returns>DateTime that represents when the standard time change occurs</returns>
        /// <remarks>Returns DateTime.MinValue if there is no time change</remarks>
        public DateTime GetStandardDateTime( int year )
        {
            //call the getChangeDate function to calculate the date of change
            return TimeZoneInfo.GetChangeDate( year, this.StandardDate );
        }

        /// <summary>
        /// The date of the daylight time change.
        /// </summary>
        /// <param name="year">The year to calculate the daylight change for</param>
        /// <returns>DateTime that represents when the daylight time change occurs</returns>
        /// <remarks>Returns DateTime.MinValue if there is no time change</remarks>
        public DateTime GetDaylightDateTime( int year )
        {
            //call the getChangeDate function to calculate the date of change
            return TimeZoneInfo.GetChangeDate( year, this.DaylightDate );
        }


        /// <summary>
        /// The daylight time changes for the current time-zone
        /// </summary>
        /// <param name="year">Year to retrieve the daylight changes for</param>
        /// <returns>A DaylightTime object that represents the daylight time for a given year</returns>
        /// <remarks>Returns null if there is no time change</remarks>
        public DaylightTime GetDaylightChanges( int year )
        {
            //if the current timezone information doesn't have adjustments
            //for daylight time, then return null
            if ( !this.ObservesDaylightTime ) return null;

            //construct a DateTime object for the DaylightDate for the current timezone
            return new DaylightTime( this.GetDaylightDateTime( year ), this.GetStandardDateTime( year ),
                TimeSpan.FromMinutes( -this.DaylightBias ) );
        }

        #endregion
    }
}
