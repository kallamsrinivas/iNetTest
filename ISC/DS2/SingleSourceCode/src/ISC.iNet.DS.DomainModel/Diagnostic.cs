using System;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides base functionality to classes that define diagnostic tests.
	/// </summary>
	public class Diagnostic : ICloneable
	{

		#region Fields

		private string _serialNumber;		
		private DateTime _time;
		private bool _passed;

		protected const int DIAG_NAME_WIDTH = 45;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of Diagnostic class.
		/// </summary>
		public Diagnostic()
		{
			// Do nothing
		}

		/// <summary>
		/// Creates a new instance of Diagnostic class when diagnostic serial number, diagnostic type and diagnostic time are provided.
		/// </summary>
		/// <param name="serialNumber">Serial number for diagnostic</param>
		/// <param name="time">Date\Time diagnostic is performed</param>
		public Diagnostic( string serialNumber, DateTime time )
		{
			SerialNumber = serialNumber;
			Time = time;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the serial number for the diagnostic.
		/// </summary>
		public string SerialNumber
		{
			get
			{
				if ( _serialNumber == null )
				{
					_serialNumber = string.Empty;
				}
				
				return _serialNumber;
			}
			set
			{
				if ( value == null )
				{
					_serialNumber = null;
				}
				else
				{
					_serialNumber = value.Trim().ToUpper();
				}
			}
		}

		/// <summary>
		/// Gets or sets the diagnostic date/time.
		/// </summary>
		public DateTime Time
		{
			get
			{	
				return _time;
			}
			set
			{
				_time = value;
			}
		}

		/// <summary>
		/// Gets or sets the diagnostic result.
		/// </summary>
		public bool Passed
		{
			get
			{
				return _passed;
			}
			set
			{
				_passed = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return Passed.ToString();
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a Diagnostic object.
		/// </summary>
		/// <returns>Diagnostic object</returns>
		public virtual object Clone()
		{
            return this.MemberwiseClone();
		}

		#endregion
	}
}
