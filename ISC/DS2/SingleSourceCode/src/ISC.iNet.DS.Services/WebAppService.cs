using System;
using System.Threading;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    public class WebAppService : Service
    {
        private WebServer _webServer;

		/// <summary>
        /// Creates a new instance of a WebAppService class.
		/// </summary>
        public WebAppService( Master master ) : base( master )
        {
            IdleTime = new TimeSpan( 0, 0, 2 );
            DelayedStartTime = new TimeSpan( 0, 0, 10 );
        }

        private WebServer WebServer
        {
            get { return _webServer; }
            set { _webServer = value; }
        }

        protected override void OnStart()
        {
            if ( WebServer != null ) // This should never happen
                throw new ApplicationException( "Web server already non-null in OnStart." );

            // Since this service doesn't really do anything (the WebServer's thread(s) does all the work)
            // we can run at the absolute lowest prioroty.
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;

            //AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if ( Configuration.DockingStation.WebAppEnabled )
            {
                Log.Info( Name + ".OnStart - WebServer.Start" );

                WebServer = new WebServer();
                WebServer.Start();

                Log.Info( Name + ".OnStart - WebServer started." );
            }
            else
                Log.Warning( Name + ".OnStart - WebServer NOT STARTED! (WebAppEnabled=false)" );
        }

        protected override void OnPause( bool pausing )
        {
            if ( WebServer == null )
                return;

            if ( pausing )
            {
                if ( WebServer.Running )
                {
                    Log.Info( Name + ".OnPause - WebServer.Stop" );
                    WebServer.Stop();
                    Log.Info( Name + ".OnPause - WebServer.stopped." );
                }
            }
            else
            {
                if ( !WebServer.Running )
                {
                    Log.Info( Name + ".OnPause - WebServer Start" );
                    WebServer.Start();
                    Log.Info( Name + ".OnPause - WebServer started." );
                }
            }

            Log.Info( Name + ".OnPause - WebServer.Running=" + WebServer.Running );
        }

        protected override void OnStop()
        {
            if ( WebServer != null && WebServer.Running )
                WebServer.Stop();
        }

        //private static void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e )
        //{
        //    Exception ex = (Exception)e.ExceptionObject;

        //    // write it to a log file
        //    StringBuilder sb = new StringBuilder();
        //    sb.Append( string.Format( "{0} at {1}\r\n", ex.GetType().FullName, DateTime.UtcNow.ToString( "MM/dd/yy hh:mm:ss" ) ) );
        //    sb.Append( string.Format( "Runtime {0} terminating\r\n", e.IsTerminating ? "true" : "false" ) );
        //    sb.Append( string.Format( "Message: {0}\r\n", ex.Message ) );
        //    sb.Append( string.Format( "Stack trace: {0}\r\n", ex.StackTrace ) );

        //    Log.Error( sb.ToString() );

        //}

        /// <summary>
        /// This method implements the thread start for this service.
        /// </summary>
        protected override void Run()
        {
            if ( !Configuration.DockingStation.WebAppEnabled )
            {
                if ( WebServer != null && WebServer.Running == true && IsStarted && !Paused )
                {
                    Log.Debug( Name + ".Run - Configuration.WebAppEnabled=false" );
                    Log.Debug( Name + ".Run - WebServer.Stop" );
                    WebServer.Stop();
                    Log.Debug( Name + ".Run - WebServer stopped." );
                }
            }

            else if ( Configuration.DockingStation.WebAppEnabled )
            {
                if ( WebServer != null && WebServer.Running == false && IsStarted && !Paused )
                {
                    Log.Debug( Name + ".Run - Configuration.WebAppEnabled=true" );
                    Log.Debug( Name + ".Run - WebServer not running. Restarting it..." );

                    Log.Debug( Name + ".Run - WebServer.Stop" );
                    WebServer.Stop(); // just to be safe, deliberately stop it since even though it's not running, it may think it is.

                    Log.Debug( Name + ".Run - WebServer.Start" );
                    WebServer.Start();

                    Log.Debug( Name + ".Run - WebServer started." );
                }
            }
        }
    }
}
