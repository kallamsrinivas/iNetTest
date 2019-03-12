using System;

namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// Used by the <see cref="ErrorDiagnostic"/> class's <see cref="ErrorDiagnostic.Category"/> property.
	/// </summary>
	public enum ErrorCategory
	{
		/// <summary>
		/// The error is an instrument error.
		/// </summary>
		Instrument,

		/// <summary>
		/// The error occurred in a base unit.
		/// </summary>
		BaseUnit
	}

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to define an Error diagnostic.
    /// </summary>
    public class ErrorDiagnostic : Diagnostic
    {
        #region Fields

        private int _code = DomainModelConstant.NullInt;
        private DateTime _errorTime = DomainModelConstant.NullDateTime;
		private ErrorCategory _category = ErrorCategory.Instrument;
		private string _baseUnitSerialNumber = string.Empty;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of ErrorDiagnostic class.
        /// </summary>
        public ErrorDiagnostic()
        {
        }


        /// <summary>
        /// Creates a new instance of ErrorDiagnostic class.
        /// </summary>
		/// <param name="code">error code</param>
		/// <param name="errorTime">the time the error occurred on the instrument</param>
		/// <param name="category">was the error logged for the instrument or its base unit</param>
		/// <param name="baseUnitSerialNumber">the S/N of the base unit the instrument was on when the error occurred (if applicable)</param>
        public ErrorDiagnostic( int code, DateTime errorTime, ErrorCategory category, string baseUnitSerialNumber  )
        {
            _code = code;
            _errorTime = errorTime;
			_category = category;
			_baseUnitSerialNumber = baseUnitSerialNumber;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the error code number.
        /// </summary>
        public int Code
        {
            get
            {
                return _code;
            }
        }


        /// <summary>
        /// The date and time this error occurred
        /// </summary>
        public DateTime ErrorTime
        {
            get
            {
                return _errorTime;
            }
        }

		public ErrorCategory Category
		{
			get
			{
				return _category;
			}
		}

		public string BaseUnitSerialNumber
		{
			get
			{
				if ( _baseUnitSerialNumber == null )
					_baseUnitSerialNumber = string.Empty;

				return _baseUnitSerialNumber;
			}
		}

        #endregion

        #region Methods

        public override string ToString()
        {
            string buf = this.Code.ToString().PadRight( 10 ) + ": Diagnostic Error Code, " + this.ErrorTime + '\n';
            return buf;
        }

        #endregion

    }
}
