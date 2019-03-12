using System;
using System.Diagnostics;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// ResponseFactor.
	/// </summary>
	public class ResponseFactor : ICloneable
	{
		private string _gasCode = string.Empty;
		private double _value = double.MinValue;
        private string _name = string.Empty;

        public ResponseFactor() {}
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="gasCode">Every Custom Response Factor is tied to a particular type of gas code</param>
		public ResponseFactor( string gasCode )
		{
            Log.Assert( gasCode != null && gasCode != string.Empty );

			this.GasCode = gasCode;
		}

        public ResponseFactor( string name, string gasCode, double value )
        {
            Name = name;
            GasCode = gasCode;
            Value = value;
        }

		/// <summary>
		/// Every sensor profile is tied to a particular type of sensor
		/// </summary>
		public string GasCode
		{
			get
            {
                 return _gasCode;
			}
			set
			{
				_gasCode = value == null ? string.Empty : value.Trim();
			}
		}

        /// <summary>
        /// </summary>
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value == null ? string.Empty : value.Trim();
            }
        }

		/// <summary>
		/// 
		/// </summary>
		public double Value
		{
			get
			{
				return _value;
			}
			set
			{
				_value = value;
			}
		}

        public override string ToString()
        {
            return string.Format( "{0},{1},{2}", GasCode, Name, Value );
        }

		public object Clone()
		{
			return this.MemberwiseClone();
		}

	}

}
