using System;
using ISC.WinCE.Logger;



namespace ISC.iNet.DS.DomainModel
{

    public enum DockingStationErrorLevel
    {
        None = 0,
        Debug = 100,
        Warning = 200,
        Error = 300,
        Fatal = 400
    }

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a docking station error.
	/// </summary>
	public class DockingStationError : ICloneable
	{
		
		#region Fields

        private string _instrumentSerialNumber = string.Empty;
        private DockingStationErrorLevel _errorLevel = DockingStationErrorLevel.Error;
		private string _description;
        private DateTime _time = DateTime.UtcNow;

        private string _errorCode = null; //Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of DockingStationError class when its description is provided.
		/// </summary>
		/// <param name="description">Error description</param>
		public DockingStationError( string description )
		{
			Description = description;
		}

        public DockingStationError( string description, string instrumentSerialNumber )
        {
            Description = description;
            InstrumentSerialNumber = instrumentSerialNumber;
        }

        //Suresh 06-FEB-2012 INS-2622 && Suresh 15-SEPTEMBER-2011 INS-1593
        public DockingStationError(string description, string instrumentSerialNumber, string errorCode)
        {
            Description = description;
            InstrumentSerialNumber = instrumentSerialNumber;
            ErrorCode = errorCode;
        }

        public DockingStationError( Exception ex )
        {
            Description = ex.ToString();
        }

        public DockingStationError( Exception ex, string instrumentSerialNumber )
        {
            Description = ex.ToString();
            InstrumentSerialNumber = instrumentSerialNumber;
        }

        public DockingStationError( Exception ex, DockingStationErrorLevel errorLevel )
        {
            Description = ex.ToString();
            ErrorLevel = errorLevel;
        }

        public DockingStationError( string description, Exception ex, DockingStationErrorLevel errorLevel, string instrumentSerialNumber )
        {
            Description = description + " - " +  ex.ToString();
            ErrorLevel = errorLevel;
            InstrumentSerialNumber = instrumentSerialNumber;
        }

        public DockingStationError( Exception ex, DockingStationErrorLevel errorLevel, string instrumentSerialNumber )
        {
            Description = ex.ToString();
            ErrorLevel = errorLevel;
            InstrumentSerialNumber = instrumentSerialNumber;
        }

        public DockingStationError( string description, DockingStationErrorLevel errorLevel )
        {
            Description = description;
            ErrorLevel = errorLevel;
        }

        public DockingStationError( string description, DockingStationErrorLevel errorLevel, string instrumentSerialNumber )
        {
            Description = description;
            ErrorLevel = errorLevel;
            InstrumentSerialNumber = instrumentSerialNumber;
        }

		public DockingStationError( string description, DockingStationErrorLevel errorLevel, string instrumentSerialNumber, string errorCode )
		{
			Description = description;
			ErrorLevel = errorLevel;
			InstrumentSerialNumber = instrumentSerialNumber;
			ErrorCode = errorCode;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the error description.
		/// </summary>
		public string Description
		{
			get
			{
				if ( _description == null )
				{
					_description = string.Empty;
				}

				return _description;
			}
			private set
			{
				if ( value == null )
				{
					_description = null;
				}
				else
				{
					_description = value.Trim();
				}
			}
		}

        /// <summary>
        /// The default is DockingStationErrorLevel.Error
        /// </summary>
        public DockingStationErrorLevel ErrorLevel
        {
            get
            {
                return _errorLevel;
            }
            private set
            {
                _errorLevel = value;
            }
        }

        public string InstrumentSerialNumber
        {
            get
            {
                return _instrumentSerialNumber == null ? string.Empty : _instrumentSerialNumber;
            }
            private set
            {
                _instrumentSerialNumber = value;
            }
        }

        /// <summary>
        /// The time this error was instantiated.  Since errors are typically created
        /// at the time the problem actually occured, this time is typically
        /// the time of the problem occurred.
        /// </summary>
        public DateTime Time
        {
            get { return _time; }
        }

        /// <summary>
        /// Gets or Sets the Error Code
        /// </summary>
        public string ErrorCode
        {
            get
            {
                return _errorCode;
            }
            private set
            {
                _errorCode = value;
            }
        }

		#endregion

		#region Methods

        /// <summary>
        /// Returned string is of format "Time: Description".
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if ( InstrumentSerialNumber.Length > 0 )
                return string.Format( "{0}, {1}: {2}", Log.DateTimeToString( Time ), InstrumentSerialNumber, Description );

            return string.Format( "{0}: {1}", Log.DateTimeToString( Time ), Description );
        }

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a DockingStationError object.
		/// </summary>
		/// <returns>DockingStationError object</returns>
		public virtual object Clone()
		{
            return this.MemberwiseClone();
		}
		
		#endregion

	}
}