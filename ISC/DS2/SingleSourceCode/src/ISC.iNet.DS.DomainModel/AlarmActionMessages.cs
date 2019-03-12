using System;

namespace ISC.iNet.DS.DomainModel
{
	public class AlarmActionMessages : ICloneable
	{
		#region Fields

		private string _sensorCode;
		private string _gasAlertMessage;
		private string _lowMessage;
		private string _highMessage;
		private string _stelMessage;
		private string _twaMessage;

		#endregion

		#region Constructors

		public AlarmActionMessages( string sensorCode )
		{
			this._sensorCode = sensorCode == null ? string.Empty : sensorCode;
			this._gasAlertMessage = string.Empty;
			this._lowMessage = string.Empty;
			this._highMessage = string.Empty;
			this._stelMessage = string.Empty;
			this._twaMessage = string.Empty;
		}

		public AlarmActionMessages( string sensorCode, string gasAlertMessage, string lowAlarmMessage, string highAlarmMessage, string stelAlarmMessage, string twaAlarmMessage )
		{
			this._sensorCode = sensorCode == null ? string.Empty : sensorCode;
			this._gasAlertMessage = gasAlertMessage == null ? string.Empty : gasAlertMessage;
			this._lowMessage = lowAlarmMessage == null ? string.Empty : lowAlarmMessage;
			this._highMessage = highAlarmMessage == null ? string.Empty : highAlarmMessage;
			this._stelMessage = stelAlarmMessage == null ? string.Empty : stelAlarmMessage;
			this._twaMessage = twaAlarmMessage == null ? string.Empty : twaAlarmMessage;
		}

		#endregion

		#region Properties

		public string SensorCode
		{
			get
			{
				return this._sensorCode;
			}
		}

		public string GasAlertMessage
		{
			get
			{
				return this._gasAlertMessage;
			}
			set
			{
				if ( value == null )
				{
					value = string.Empty;
				}

				this._gasAlertMessage = value;
			}
		}

		public string LowAlarmMessage
		{
			get
			{
				return this._lowMessage;
			}
			set
			{
				if ( value == null )
				{
					value = string.Empty;
				}

				this._lowMessage = value;
			}
		}

		public string HighAlarmMessage
		{
			get
			{
				return this._highMessage;
			}
			set
			{
				if ( value == null )
				{
					value = string.Empty;
				}

				this._highMessage = value;
			}
		}

		public string StelAlarmMessage
		{
			get
			{
				return this._stelMessage;
			}
			set
			{
				if ( value == null )
				{
					value = string.Empty;
				}

				this._stelMessage = value;
			}
		}

		public string TwaAlarmMessage
		{
			get
			{
				return this._twaMessage;
			}
			set
			{
				if ( value == null )
				{
					value = string.Empty;
				}

				this._twaMessage = value;
			}
		}

		#endregion

		#region Methods

		public object Clone()
		{
			return this.MemberwiseClone();
		}

		/// <summary>
		/// Called indirectly for logging purposes.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.Format( "SensorCode={0} GasAlert=\"{1}\" Low=\"{2}\" High=\"{3}\" STEL=\"{4}\" TWA=\"{5}\"", SensorCode, GasAlertMessage, LowAlarmMessage, HighAlarmMessage, StelAlarmMessage, TwaAlarmMessage );
		}

		#endregion
	}
}
