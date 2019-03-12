using System;


namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// Summary description for GeneralDiagnosticProperty.
	/// </summary>
	public class GeneralDiagnosticProperty
	{
		private string _propertyName;
		private string _propertyValue;

		public GeneralDiagnosticProperty()
		{
			
		}

		public GeneralDiagnosticProperty( string inName, string inValue )
		{
			_propertyName = inName;
			_propertyValue = inValue;
		}

		public string Name
		{
			get
			{
				return _propertyName;
			}
			set
			{
				_propertyName = value;
			}
		}

		public string Value
		{
			get
			{
				return _propertyValue;
			}
			set
			{
				_propertyValue = value;
			}
		}
	}
}
