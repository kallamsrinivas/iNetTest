using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.iNet.InetUpload;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.iNet
{
	/// <summary>
    /// Summary description for QueueData.
	/// </summary>
	///
	[Serializable]
    // TODO - are all of these XmlIncludes really necessary?
    // Seems to still work without them.  At least without some of them.
    [XmlInclude(typeof(ALARM_EVENT))]
    [XmlInclude(typeof(BATTERY))]
    [XmlInclude(typeof(COMPONENT))]
    [XmlInclude(typeof(CYLINDER))]
    [XmlInclude(typeof(CYLINDER_GAS))]
    [XmlInclude(typeof(CYLINDER_USED))]
    [XmlInclude(typeof(DATALOG_SESSION))]
    [XmlInclude(typeof(DATALOG_SENSOR_SESSION))]
    [XmlInclude(typeof(DATALOG_PERIOD))]
    [XmlInclude(typeof(DATALOG_READING))]
    [XmlInclude(typeof(DEBUG_LOG))]
    [XmlInclude(typeof(DATABASE_UPLOAD) )]
    [XmlInclude(typeof(DIAGNOSTIC))]
    [XmlInclude(typeof(DIAGNOSTIC_DATA))]
    [XmlInclude(typeof(DOCKING_STATION))]
    [XmlInclude(typeof(EQUIPMENT))]
    [XmlInclude(typeof(ERROR))]
    [XmlInclude(typeof(ERROR_DATA))]
    [XmlInclude(typeof(INSTRUMENT))]
    [XmlInclude(typeof(INSTRUMENT_BUMP_TEST))]
    [XmlInclude(typeof(INSTRUMENT_CALIBRATION))]
    [XmlInclude(typeof(PROPERTY))]
    [XmlInclude(typeof(SENSOR))]
    [XmlInclude(typeof(SENSOR_BUMP_TEST))]
    [XmlInclude(typeof(SENSOR_CALIBRATION))]
	public class QueueData
	{
        private string _inetAccountNum;
        private string _label;
        private string _type;
		private object _webServiceParameter;
        private string _webServiceParameterText;
        private int _webServiceParameterSize;
        private DateTime _timestamp = DomainModelConstant.NullDateTime;
        private long _id = DomainModelConstant.NullId;
        private Dictionary<string, string> _properties = new Dictionary<string, string>();

        /// <summary>
        /// Helper method for constructors
        /// </summary>
        /// <param name="accountNum"></param>
        /// <param name="wsParameter"></param>
        /// <param name="properties"></param>
        private void Init( string accountNum, object wsParameter, IDictionary<string, string> properties )
        {
            Log.Assert( accountNum != null && accountNum != string.Empty, "QueueData.ctor: accountNum cannot be null/empty" );
            Log.Assert( wsParameter != null, "QueueData.ctor: wsParameter cannot be null" );

            _inetAccountNum = accountNum;
            
            _type = wsParameter.GetType().AssemblyQualifiedName;

            _label = CreateLabel( _type );

            _webServiceParameter = wsParameter;

            // copy the dictionary contents
            if ( properties != null )
            {
                foreach ( string attribute in properties.Keys )
                    _properties[attribute] = properties[ attribute ];
            }
        }

        public QueueData() {}

		public QueueData( string accountNum, object wsParameter )
		{
            Init( accountNum, wsParameter, null );
		}

        public QueueData( string accountNum, object wsParameter, IDictionary<string,string> properties )
        {
            Init( accountNum, wsParameter, properties );
        }

        internal QueueData( PersistedQueueData persistedQueueData )
        {
            _inetAccountNum = persistedQueueData.InetAccountNum;

            _webServiceParameterText = persistedQueueData.SerializedWebServiceParameter;
            if ( _webServiceParameterText != null )
                _webServiceParameterSize = _webServiceParameterText.Length;

            _type = persistedQueueData.Type;

            _label = persistedQueueData.Label;

            _id = persistedQueueData.Id;

            _timestamp = persistedQueueData.Timestamp;

            // copy the dictionary contents
            foreach ( string attribute in persistedQueueData.Properties.Keys )
                _properties[attribute] = persistedQueueData.Properties[ attribute ];
        }

        /// <summary>
        /// If type is "A.B.C.MyType, A.B.C.MyAssembly", then this method simply returns "MyType".
        /// </summary>
        /// <returns></returns>
        private string CreateLabel( string type )
        {
            int endClassPathIndex = type.IndexOf( ',' );

            if ( endClassPathIndex == -1 ) return Label;

            int endNameSpaceIndex = type.LastIndexOf( '.', endClassPathIndex );

            if ( endNameSpaceIndex == -1 ) return Label;

            return type.Substring( endNameSpaceIndex + 1, endClassPathIndex - endNameSpaceIndex - 1 );
        }

        public IDictionary<string, string> Properties { get { return _properties; } }

        public string InetAccountNum { get { return _inetAccountNum; } }

        /// <summary>
        /// If data's Type is is "A.B.C.MyLabel, A.B.C.MyAssembly", then this method simply returns "MyLabel"
        /// </summary>
        public string Label
        {
            get
            {
                if ( _label == null ) _label = string.Empty;
                return _label;
            }
        }

        public DateTime Timestamp { get { return _timestamp; } }

        public long Id { get { return _id; } }

		public object WebServiceParameter
		{
			get
			{
                // lazy load the web service parameter so that it is not deserialized if not necessary.
                if ( _webServiceParameterText != null )
                {
                    XmlSerializer serializer = new XmlSerializer( Type.GetType( _type ) );
                    StringReader reader = new StringReader( _webServiceParameterText );
                    _webServiceParameter = serializer.Deserialize( reader );

                    // We're done with the text after we deserialize it.
                    _webServiceParameterText = null;
                }

				return _webServiceParameter;
			}
		}

        /// <summary>
        /// The size of the data when serialized to a string.
        /// <para>
        /// Will be zero if this QueueData instance was not created by 
        /// one of the constructors that don't accept the serialized string.
        /// </para>
        /// </summary>
        public int WebServiceParameterSize
        {
            get
            {
                return _webServiceParameterSize;
            }
        }

        internal ISC.iNet.DS.DomainModel.PersistedQueueData CreatePersistedQueueData()
        {

            // TODO: need exception handling around this block...
            XmlSerializer serializer = new XmlSerializer( WebServiceParameter.GetType() );
            
            TextWriter writer = new StringWriter();

            serializer.Serialize( writer, WebServiceParameter );
            writer.Close();

            PersistedQueueData persistedQueueData = new PersistedQueueData( InetAccountNum, Label, _type, ( (StringWriter)writer ).ToString()  );

            foreach ( string attribute in this.Properties.Keys )
                persistedQueueData.Properties[ attribute ] = this.Properties[ attribute ];

            return persistedQueueData;
        }

        /// <summary>
        /// Return's thie QueueData's "Label" property.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Label;
        }
    }
}
