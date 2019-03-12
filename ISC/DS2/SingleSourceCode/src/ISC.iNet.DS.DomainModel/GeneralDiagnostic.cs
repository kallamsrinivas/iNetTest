using System;
using System.Text;


namespace ISC.iNet.DS.DomainModel
{
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// This type of diagnostic contains a collection of name/value pairs, each 
	/// containing a named diagnostic parameter and its measured value.
	/// For example, "Speaker Voltage", 200
	///				 "LCD Current", 400
	///				 
	///This type of diagnostic should be capable of replacing the strongly-typed
	///Diagnostic classes used in the legacy instruments.
	/// </summary>
	public class GeneralDiagnostic : Diagnostic //, ICloneable
	{
		private GeneralDiagnosticProperty[] _diagList = new GeneralDiagnosticProperty[0];

		/// <summary>
		/// Creates a new instance of GeneralDiagnostic class.
		/// </summary>
		public GeneralDiagnostic()
		{
		}

		/// <summary>
		/// Creates a new instance of GeneralDiagnostic class when diagnostic serial number, diagnostic type and diagnostic time are provided.
		/// </summary>
		/// <param name="serialNumber">Serial number for diagnostic</param>
		/// <param name="time">Date\Time diagnostic is performed</param>
		public GeneralDiagnostic( string serialNumber, DateTime time ) : base ( serialNumber, time )
		{
		}

		/// <summary>
		/// The items are each GeneralDiagnosticProperty objects, contained in 
		/// an arrayList.
		/// </summary>
		public GeneralDiagnosticProperty[] Items
		{
			get
			{
				return _diagList;
			}
			set
			{
                _diagList = ( value == null ) ? new GeneralDiagnosticProperty[0] : value;
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if ( _diagList == null )
                return string.Empty;
                 
            StringBuilder buf = new StringBuilder();

            foreach ( GeneralDiagnosticProperty gdp in _diagList )
            {
                buf.Append( gdp.Value.PadRight(10) );
                buf.Append( ": " );
                buf.Append( gdp.Name + " Diagnostic" );
                buf.Append( System.Environment.NewLine );
            }
            return buf.ToString();
        }

	}
}
