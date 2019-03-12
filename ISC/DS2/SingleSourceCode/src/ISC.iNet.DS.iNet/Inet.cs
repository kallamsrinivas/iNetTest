using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Services.Protocols;
using System.Xml;
using System.Xml.Serialization;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;
using ISC.Instrument.TypeDefinition;
#if INET_WS_SERIALIZE
using System.Diagnostics;
#endif

namespace ISC.iNet.DS.iNet
{
    public abstract class Inet : IDisposable
    {
        protected DockingStation _configDS;  // docking station configuration

        protected Schema _schema;

        protected SoapHttpClientProtocol _webService;

        // Default online status indicators to true.  We assume we're online until we find out otherwise.
        static private bool _isDownloadingOnline = true;
        static private bool _isUploadingOnline = true;

        /// <summary>
        /// The time that the DS was last see as going "offline".
        /// </summary>
        static private DateTime _offlineTime = DateTime.MinValue;        

        /// <summary>
        /// Static constructor.
        /// </summary>
        static Inet()
        {
            /// This must be done (one time during the application life cycle) before making any calls to an SSL'd web service.
            System.Net.ServicePointManager.CertificatePolicy = new TrustAllCertificatePolicy();
        }

        private void Init( DockingStation config, Schema schema )
        {
            // clone it so we don't have to worry about the configuration settings
            // changing behind our back.
            _configDS = (DockingStation)config.Clone();
            _schema = (Schema)schema.Clone();
        }

        /// <summary>
        /// Default constructor.  The instance will use the Configuration.Instance for
        /// to obtain its configuration settings needed to connect to iNet.
        /// </summary>
        public Inet()
        {
            Init( Configuration.DockingStation, Configuration.Schema );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="config">Configuration settings needed to connect to iNet.  This parameter is cloned by the constructor.</param>
        /// <param name="schema">Schema settings needed to connect to iNet. This parameter is cloned by the constructor.</param>
        public Inet( DockingStation config, Schema schema )
        {
            Init( config, schema );
        }

        /// <summary>
        /// Disposes of the managed web services (SoapHttpClientProtocols) used for communication with iNet.
        /// This should, in effect, cause the underlying connection to be closed.
        /// </summary>
        public void Dispose()
        {
            if ( _webService != null )
                _webService.Dispose();
        }        

        /// <summary>
        /// True if we're currently able to download data (Exchange Status) from iNet; else false;
        /// </summary>
        static public bool IsDownloadOnline
        {
            protected set
            {
                // If we're about to go offline, but we're currently offline, then mark the Now as the time we're going offline.
                if ( !value && IsOnline )
                    _offlineTime = DateTime.UtcNow;

                _isDownloadingOnline = value;
            }
            get
            {
                return _isDownloadingOnline;
            }

        }

        /// <summary>
        /// True if we're currently able to upload to iNet; false if can't connect to upload server, or server keeps rejecting data.
        /// </summary>
        static public bool IsUploadOnline
        {
            protected set
            {
                // If we're about to go offline, but we're currently offline, then mark the Now as the time we're going offline.
                if ( !value && IsOnline )
                    _offlineTime = DateTime.UtcNow;

                _isUploadingOnline = value;
            }
            get
            {
                return _isUploadingOnline;
            }
        }


        /// <summary>
        /// True if both IsDownloadOnline and IsUploadOnline are true, else false.
        /// </summary>
        static public bool IsOnline { get { return IsDownloadOnline && IsUploadOnline; } }

        /// <summary>
        /// Returns the amount of time the DS has been 'offline' from iNet.
        /// <para>
        /// TimeSpan.Zero is returned if the DS is currently online.
        /// </para>
        /// </summary>
        static public TimeSpan OfflineTime
        {
            get
            {
                if ( IsOnline )
                    return TimeSpan.Zero;

                return DateTime.UtcNow - _offlineTime;
            }
        }

        /// <summary>
        /// Gets the definition for the supported instrument type using the instrument's version. 
        /// NOTE: The definition is not cached as it is specific to a single instrument.
        /// </summary>
        internal static InstrumentTypeDefinition CreateInstrumentDefinitionInstance( DeviceType instType, string instVersion )
        {
            if ( instType == DeviceType.MX4 ) return new Mx4Definition( instVersion );
            if ( instType == DeviceType.VPRO ) return new VentisProDefinition( instVersion );
            if ( instType == DeviceType.MX6 ) return new Mx6Definition( instVersion );
            if ( instType == DeviceType.SC ) return new SafeCoreDefinition( instVersion );
            if ( instType == DeviceType.TX1 ) return new Tx1Definition( instVersion );
            if ( instType == DeviceType.GBPRO ) return new GbProDefinition( instVersion );
            if ( instType == DeviceType.GBPLS ) return new GbPlusDefinition( instVersion );

            throw new System.NotSupportedException( "\"" + instType.ToString() + "\" is not a supported instrument." );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="soapHttpClientProtocol"></param>
        /// <param name="webService"></param>
        /// <param name="config"></param>
        /// <param name="timeout">Timeout for web service call (in seconds).</param>
        protected void SetCredentials( SoapHttpClientProtocol soapHttpClientProtocol, string webService, int webServiceTimeout, DockingStation config )
        {
            if ( webService == string.Empty )
                throw new ApplicationException( "No webService specified" );
            if ( config.InetUrl == string.Empty )
                throw new ApplicationException( "InetUrl is empty" );

            // as a precaution, watch out for zere timeouts, which we may get as a timeout setting
            // from dev servers.  Use .Net default instead, if this happens.
            if ( webServiceTimeout <= 0 )
            {
                Log.Warning( "SetCredentials: Overriding zero timeout with default of " + 100 );
                webServiceTimeout = 100;
            }

            soapHttpClientProtocol.Timeout = webServiceTimeout * 1000; // convert to milliseconds.

            // The iNet upload server's URL
            soapHttpClientProtocol.Url = _configDS.InetUrl + webService;

            Log.Trace( string.Format( "INET: URL=\"{0}\", Timeout={1}", soapHttpClientProtocol.Url.ToString(), webServiceTimeout ) );

            // Basic credentials.

            NetworkCredential nc = new NetworkCredential();
            nc.UserName = config.InetUserName;
            nc.Password = config.InetPassword;
            soapHttpClientProtocol.Credentials = nc;
            Log.Trace( string.Format( "INET: Upload UserId=\"{0}\", Passwd=\"{1}\"", nc.UserName, nc.Password ) );

            // Proxy credentials

            if ( _configDS.InetProxy.Length > 0 ) // is a proxy specified?
            {
                WebProxy webProxy = new WebProxy( _configDS.InetProxy, false );

                nc = new NetworkCredential();

                // See if user name contains domain name, to.  e.g. "SomeDomainName\SomeUserName"
                int index = _configDS.InetProxyUserName.IndexOf("\\", 0);
                if (index > 0) // domain name exists?
                {
                    nc.Domain = _configDS.InetProxyUserName.Substring( 0, index ); // extract just the domain name, ignore the user name.
                    nc.UserName = _configDS.InetProxyUserName.Substring(index + 1); // extract just the use name, ignore the domain name.
                }
                else // No domain name specified.
                {
                    nc.UserName = _configDS.InetProxyUserName;
                }

                nc.Password = _configDS.InetProxyPassword;                
                webProxy.Credentials = nc;
                soapHttpClientProtocol.Proxy = webProxy;

#if DEBUG
                Log.Trace( string.Format( "INET: Using Proxy URL \"{0}\", Proxy Domain=\"{1}\", ProxyUser=\"{2}\", ProxyPassword=\"{3}\"",
                    _configDS.InetProxy,nc.Domain == null ? "" : nc.Domain, nc.UserName, nc.Password));
#else
            Log.Trace( string.Format( "INET: Using Proxy URL \"{0}\", Proxy Domain=\"{1}\", ProxyUser=\"{2}\", ProxyPassword=\"{3}\"",
                    string.Empty.PadRight( _configDS.InetProxy.Length, '*' ),
                    string.Empty.PadRight( (nc.Domain == null ? "" : nc.Domain).Length, '*' ),
                    string.Empty.PadRight( nc.UserName.Length, '*' ),
                    string.Empty.PadRight( nc.Password.Length, '*' ) ) );
#endif
            }
            else
                Log.Trace( "INET: No configured proxy" );
        }

        /// <summary>
        /// Gets the timeout in milliseconds to set on the watchdog based on the provided timeout in seconds for the web service call.
        /// </summary>
        /// <param name="webMethodTimeout">The timeout in seconds that will be used for the web service call.</param>
        protected int GetWatchdogPeriod( int webTimeoutSeconds )
        {
            // add 60 seconds to the web service call timeout and convert to ms
            return ( webTimeoutSeconds + 60 ) * 1000;
        }

        /// <summary>
        /// Gets the ID of the current thread as a string to use as the name of a watchdog monitoring a web service call.
        /// </summary>
        protected string GetWatchdogName()
        {
            // We use the thread ID as the watchdog name to ensure uniquess.  We may have multiple threads 
            // making the same web service call at one time, but a single thread will never make more than 
            // one web service call at any point in time since they are synchronous. 
            return Thread.CurrentThread.ManagedThreadId.ToString();
        }


        /// <summary>
        /// Called for exceptions thrown by iNet web service calls.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        protected string HandleInetWebServiceException( Exception ex )
        {
            // If the exception is a .net WebException (which it will probably be
            // as this is what would (should) be thrown if the web service call is unable
            // to contact the iNet server), then handle it differently.
            if ( ex is WebException )
                return HandleWebException( ex as WebException );

            // Otherwise, if log level is tracing, then return exception's full stack trace
            else if ( Log.Level >= LogLevel.Trace )
                return ex.ToString();

            // If not trace level, then just return the exception's Message string, 
            // along with any inner exceptions' Message strings.
            StringBuilder errorMsg = new StringBuilder();

            int innerCount = 0;
            for ( Exception inner = ex; inner != null; inner = inner.InnerException )
            {
                if ( ++innerCount > 1 )
                    errorMsg.AppendFormat( "{0}--> ", Environment.NewLine );
                errorMsg.AppendFormat( "{0}: {1}", inner.GetType(), inner.Message );
            }
            return errorMsg.ToString();
        }

        /// <summary>
        /// </summary>
        /// <param name="wex"></param>
        /// <returns></returns>
        private string HandleWebException( WebException wex )
        {
            StringBuilder errorMsg = new StringBuilder();

            // Log full stack trace if in trace mode. Log only the exceptions' messages if not in trace mode.
            if ( Log.Level >= LogLevel.Trace )
                errorMsg.Append( wex.ToString() );
            else
            {
                int innerCount = 0;
                for ( Exception ex = wex; ex != null; ex = ex.InnerException )
                {
                    if ( ++innerCount > 1 )
                        errorMsg.AppendFormat( "{0}--> ", Environment.NewLine );
                    errorMsg.AppendFormat( "{0}: {1}", ex.GetType(), ex.Message );
                }
            }

            // WebException includes a Status property that contains a WebExceptionStatus enumeration value.
            // e.g WebExceptionStatus.Timeout, WebExceptionStatus.ConnectFailure, etc.
            // Examination of this property allows us to figure out the error that occurred.
            errorMsg.AppendFormat( "{0}WebException.Status: {1}{2}", Environment.NewLine, wex.Status.ToString(), Environment.NewLine );

            WebResponse r = wex.Response;

            if ( wex.Response == null ) // Will probably never be null, but better safe than sorry
                errorMsg.Append( "WebException.Response is null" );

            else
            {
                errorMsg.AppendFormat( "WebException.Response.ContentType: {0}{1}", wex.Response.ContentType, Environment.NewLine );
                errorMsg.AppendFormat( "WebException.Response.ContentLength: {0}{1}", wex.Response.ContentLength, Environment.NewLine );

                if ( wex.Response.Headers != null )
                {
                    foreach ( string s in wex.Response.Headers.AllKeys )
                        errorMsg.AppendFormat( "WebException.Response.Headers.{0}: {1}{2}", s, wex.Response.Headers, Environment.NewLine );
                }

                if ( wex.Response.ResponseUri != null )
                    errorMsg.AppendFormat( "WebException.Response.ResponseUri: {0}{1}", wex.Response.ResponseUri.ToString(), Environment.NewLine );

                if ( !( wex.Response is HttpWebResponse ) ) // Will probably always be a HttpWebResponse, but better safe than sorry
                    errorMsg.AppendFormat( "WebException.Response is a {0}{1}", wex.Response.GetType(), Environment.NewLine );
                else
                {
                    HttpWebResponse httpWebResponse = wex.Response as HttpWebResponse;

                    errorMsg.AppendFormat( "HttpWebResponse.StatusCode: {0}{1}", httpWebResponse.StatusCode.ToString(), Environment.NewLine );
                    errorMsg.AppendFormat( "HttpWebResponse.StatusDescription: \"{0}{1}\"", httpWebResponse.StatusDescription, Environment.NewLine );
                }
            }

            return errorMsg.ToString();
        }

        /// <summary>
        /// Generates a formatted DockingStationError for a DOWNLOAD_ERROR or UPLOAD_ERROR.
        /// </summary>
        /// <param name="inetService">The web service being called when the network error happened.</param>
        /// <param name="webMethod">The web method being called when the network error happened.</param>
        /// <param name="ex">The exception that was caught.</param>
        /// <param name="ipAddress">The docking station's IP address after the network error happened.</param>
        /// <returns>A DockingStationError to be uploaded to iNet.</returns>
        protected DockingStationError CreateFailedInetDockingStationError( InetService inetService, string webMethod, Exception ex )
        {
            StringBuilder errorMessage = new StringBuilder();
            string errorCode;
            string errorText;
            string ipType;
            string proxyInUse;

            string ipAddress;
            bool dchpEnabled;
            string inetProxy;

            // capture network setting values included in the DockingStationError
            ISC.WinCE.NetworkAdapterInfo wiredNetworkInfo = Controller.GetWiredNetworkAdapter();
            ipAddress = wiredNetworkInfo.IpAddress;
            dchpEnabled = wiredNetworkInfo.DhcpEnabled;
            inetProxy = Configuration.DockingStation.InetProxy;

            // setup string values included in the DockingStationError based upon parameter values
            errorCode = inetService == InetService.ConfigurationService ? "DOWNLOAD_ERROR" : "UPLOAD_ERROR";
            errorText = inetService == InetService.ConfigurationService ? "download from" : "upload to";
            ipType = dchpEnabled ? "[DHCP]" : "[Static]";
            proxyInUse = inetProxy.Trim().Length > 0 ? "[Proxy in Use]" : String.Empty;

            // start building the text of the error message
            errorMessage.AppendLine( string.Format( "A network error occurred while trying to {0} iNet.", errorText ) );
            errorMessage.AppendLine();
            errorMessage.AppendLine( string.Format( "Web Method: {0}", webMethod ) );
            errorMessage.AppendLine( string.Format( "IP Address: {0} {1}{2}", ipAddress, ipType, proxyInUse ) );
            errorMessage.AppendLine();

            // show exception type, message and stack trace for the top exception
            errorMessage.AppendLine( string.Format( "[{0}]", ex.GetType() ) );
            errorMessage.AppendLine( ex.Message );
            if ( !String.IsNullOrEmpty( ex.StackTrace ) )
            {
                // trim newline characters to keep even spacing between exceptions
                errorMessage.AppendLine( ex.StackTrace.TrimEnd( Environment.NewLine.ToCharArray() ) );
            }

            string innerMostStackTrace = String.Empty;
            // show exception type and message for inner exceptions
            for ( Exception nestedException = ex.InnerException; nestedException != null; nestedException = nestedException.InnerException )
            {
                errorMessage.AppendLine();
                errorMessage.AppendLine( string.Format( "[{0}]", nestedException.GetType() ) );
                errorMessage.AppendLine( nestedException.Message );

                // nestedException will be null when the loop exits, so the inner most stack trace 
                // needs copied to another variable; the below assignment only works because
                // strings are immutable
                if ( nestedException.InnerException == null )
                {
                    innerMostStackTrace = nestedException.StackTrace;
                }
            }

            // show the stack trace also for the inner most exception
            if ( !String.IsNullOrEmpty( innerMostStackTrace ) )
            {
                // trim newline characters to keep even spacing between exceptions
                errorMessage.AppendLine( innerMostStackTrace.TrimEnd( Environment.NewLine.ToCharArray() ) );
            }

            return new DockingStationError( errorMessage.ToString(), DockingStationErrorLevel.Warning, String.Empty, errorCode );
        }

        protected enum InetService
        {
            ConfigurationService,
            UploaderService
        }

#if INET_WS_SERIALIZE
#warning INET_WS_SERIALIZE compiler directive is enabled.

        /// FOR DEBUGGING PURPOSES ONLY. USED BY SerializeWebServiceData
        public class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }
        }

        /// <summary>
        /// Serializes the passed-in object to an XML file on the flash card.
        /// 
        /// FOR DEBUGGING PURPOSES ONLY.
        /// 
        /// FOR NON-DEBUG BUILDS, WE NEVER SERIALIZE DUE TO PERFORMANCE HIT IT WOULD ENTAIL.
        /// </summary>
        /// <param name="o"></param>
        static public void SerializeWebServiceParameterToFile( Object o )
        {
            Log.Info( "SerializeWebServiceParameterToFile started" );

            Stopwatch s = new Stopwatch();

            try
            {
                Type t = o.GetType();

                string typeName = t.ToString();

                string fileName = string.Format( "{0}_{1}.xml", typeName.Substring( typeName.LastIndexOf( '.' ) + 1 ), DateTime.UtcNow.Ticks );

                Log.Info( "Saving " + fileName + "..." );

                string filePath =  Controller.FLASHCARD_PATH + fileName;
  
                XmlSerializer xs = new XmlSerializer( t );

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = false;
                settings.Encoding = Encoding.UTF8;

                s.Start();

                using ( XmlWriter writer = XmlWriter.Create( filePath, settings ) )
                {
                    xs.Serialize( writer, o );
                }
            }
            catch ( Exception e )
            {
                Log.Error( "SerializeWebServiceParameterToFile", e );
            }
            finally
            {
                s.Stop();
            }

            Log.Info( "SerializeWebServiceParameterToFile completed in " + ( s.ElapsedMilliseconds / 1000.0 ) + " seconds." );
        }

        /// <summary>
        /// FOR DEBUGGING PURPOSES ONLY.
        /// <para>
        /// It serializes the passed-in web service object to an XML string, in order time how long
        /// it takes to do the serialization.</para>
        /// <para>
        /// FOR NON-DEBUG BUILDS, WE NEVER SERIALIZE DUE TO THE PERFORMANCE HIT IT ENTAILS.</para>
        /// </summary>
        /// <param name="o"></param>
        static public string SerializeWebServiceData( Object o )
        {
            Log.Info("SerializeWebServiceData started");

            Utf8StringWriter writer = null;

            string serialized = string.Empty;

            Stopwatch s = new Stopwatch();

            try
            {
                if ( o != null )
                {
                    XmlSerializer xs = new XmlSerializer( o.GetType() );

                    writer = new Utf8StringWriter();

                    s.Start();

                    xs.Serialize( writer, o );

                    s.Stop();

                    Log.Info( "SerializeWebServiceData completed in " + ( s.ElapsedMilliseconds / 1000.0 ) + " seconds." );

                    serialized = writer.ToString();
                }
            }
            catch ( Exception e )
            {
                Log.Error( "SerializeWebServiceData", e );
            }
            finally
            {
                if ( writer != null ) writer.Close();
                s.Stop();
            }

            return serialized;
        }

        /// <summary>
        /// Deserializes from an XML file on the flash card.
        /// 
        /// FOR DEBUGGING PURPOSES ONLY.
        /// 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        static public object DeserializeWebServiceParameter( Type t )
        {
            Log.Info( "DeserializeWebServiceParameter started" );

            Object o = null;

            FileStream fs = null;

            ThreadPriority oldPriority = Thread.CurrentThread.Priority;
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            Stopwatch s = new Stopwatch();

            try
            {
                // Create an instance of the XmlSerializer specifying type and namespace.
                XmlSerializer serializer = new XmlSerializer( t );

                string typeName = t.ToString();

                string fileName = string.Format( "{0}.xml", typeName.Substring( typeName.LastIndexOf( '.' ) + 1 ) );

                // A FileStream is needed to read the XML document.
                fs = new FileStream( Controller.FLASHCARD_PATH + fileName, FileMode.Open );

                s.Start();

                XmlReader reader = XmlReader.Create( fs );

                o = serializer.Deserialize( reader );
            }
            catch ( Exception e )
            {
                Log.Error( "DeserializeWebServiceParameter", e );
            }
            finally
            {
                if ( fs != null ) fs.Close();
                s.Stop();
                Thread.CurrentThread.Priority = oldPriority;
            }

            Log.Info( "DeserializeWebServiceParameter completed in " + ( s.ElapsedMilliseconds / 1000.0 ) + " seconds" );

            return o;
        }

#endif // INET_WS_SERIALIZE

    }  // end-class-Inet


    public class InetDataException : ApplicationException
    {
        public InetDataException( string msg ) : base( msg ) { }

        public InetDataException( string msg, Exception innerException ) : base( msg, innerException ) { }
    }
}  // end-namespace
