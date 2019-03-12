using System;
using System.Collections.Generic;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
    /// This class is used to store data in the database that will be sent to iNet.
    /// 
    /// Note: There is a QueueData class in the Uploader project. That class cannot be used with the 
    /// data access layer, but must be used for MSMQ. The first preference would have been to use that 
    /// class to transfer data between the uploader and data access layer. Since that was not possible,
    /// due mostly to depenedencies, this class will be used instead. 
	/// </summary>
	///
	public class PersistedQueueData
	{
        private string _inetAccountNum;
        private string _label;
        private string _type;
		private String _serializedWsParameter;
        private long _id = long.MinValue;
        public TimeZoneInfo _tzi { get; set; }

        private Dictionary<string, string> _properties = new Dictionary<string,string>();

        public DateTime Timestamp { get; set; }

        public PersistedQueueData() 
        {}

        public PersistedQueueData( string accountNum, string label, string type, string serializedWsParameter )
		{
            Log.Assert( accountNum != null && accountNum != string.Empty, "PersistedQueueData.ctor: accountNum cannot be null/empty" );
            Log.Assert( serializedWsParameter != null, "PersistedQueueData.ctor: serializedWsParameter cannot be null" );

            _inetAccountNum = accountNum;

            _label = label;

            _type = type;

            _serializedWsParameter = serializedWsParameter;
		}

        public long Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public string InetAccountNum { get { return _inetAccountNum; } }

        public string Label
        {
            get
            {
                if ( _label == null ) _label = string.Empty;
                return _label;
            }
        }

        public string Type
        {
            get
            {
                if ( _type == null ) _type = string.Empty;
                return _type;
            }

        }
		public String SerializedWebServiceParameter
		{
			get
			{
                if ( _serializedWsParameter == null )
                    _serializedWsParameter = string.Empty;

				return _serializedWsParameter;
			}
		}

        public IDictionary<string, string> Properties { get { return _properties; } }

        public override string ToString()
        {
            return Label;
        }
	}
}
