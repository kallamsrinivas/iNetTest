using System;
using System.Threading;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides base functionality to specific service related classes.
	/// </summary>
	public abstract class Service
	{
		#region Fields

		private Master _master;
		private Thread _serviceThread;
		private bool _started;
		private string _name;
		private bool _paused = true;
		private TimeSpan _idleTime = TimeSpan.Zero;
		private TimeSpan _delayStart = TimeSpan.Zero;
		private bool _running = false;
		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of a Service class.
		/// </summary>
		public Service( Master master )
		{
			_master = master;

			// Determine service's name.  It's the class's name minues the namespace info.
			string className = this.GetType().ToString();
			_name = className.Substring( className.LastIndexOf( '.' ) + 1 );

			// Create/start the thread.
			_serviceThread = new Thread( new ThreadStart( RunThread ) );
			_serviceThread.Name = Name.Replace( "Service", string.Empty );
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the name of this service.  The name of a service is the same as the class name,
		/// minus the namespace.  e.g. if class of service is "A.B.MyService",
		/// then returned name of service is "MyService".
		/// </summary>
		public string Name
		{
			get
			{
				return _name;
			}
		}

		/// <summary>
		/// Gets a value indicating status of the ServiceMain method.
		/// </summary>
		protected bool IsStarted
		{
			get
			{
				return _started;
			}
		}

		/// <summary>
		/// Returns true if the service's 'Run()' method is currently in the middle of executing.  Else false.
		/// </summary>
		/// <returns></returns>
		public bool Running() { return _running; }

		/// <summary>
		/// Returns the parent Master instance
		/// </summary>
		protected Master Master
		{
			get
			{
				return _master;
			}
		}

		/// <summary>
		/// Amount of time to wait between each call to Run()
		/// </summary>
		protected TimeSpan IdleTime
		{
			get
			{
				return _idleTime;
			}
			set
			{
				_idleTime = value;
			}
		}

		/// <summary>
		/// Amount of time to wait before the very first call to Run()
		/// </summary>
		protected TimeSpan DelayedStartTime
		{
			get
			{
				return _delayStart;
			}
			set
			{
				_delayStart = value;
			}
		}

		//public string ThreadPriorityString
		//{
		//    get
		//    {
		//        return _serviceThread.Priority.ToString();
		//    }
		//}

		#endregion

		#region Methods

		private string GetManagedThreadId()
		{
			return Thread.CurrentThread.ManagedThreadId.ToString("x8");
		}

		/// <summary>
		/// This method is to be implemented by the inheriting classes and it is the method that is used by thread start.
		/// </summary>
		protected abstract void Run();

		private void RunThread()
		{
			Log.Debug( string.Format( "{0} (ThreadId={1}) thread running in {2}ms",
				Name, GetManagedThreadId(), (int)DelayedStartTime.TotalMilliseconds ) );

			Thread.Sleep( (int)DelayedStartTime.TotalMilliseconds );

			Log.Debug( string.Format( "{0} (ThreadId={1}) thread running", Name, GetManagedThreadId() ) );

			try
			{
				Log.Debug( string.Format( "{0} (ThreadId={1}) OnStart", Name, GetManagedThreadId() ) );

				OnStart();
			}
			catch ( Exception e )
			{
				Log.Error( Name + ".OnStart", e );
			}

			while ( _started )
			{
				if ( !Paused )
				{
					try
					{
						if ( OnRun() )
						{
							_running = true;
							Run();
						}
					}
					catch ( Exception e )
					{
						Log.Error( string.Format( "{0}.Run (ThreadId={1}) Run() - {2}", Name, GetManagedThreadId(), e ) );
					}
					finally
					{
						_running = false;
					}
				}

				if ( IdleTime.Ticks > 0 )
					Thread.Sleep( (int)IdleTime.TotalMilliseconds );
			}

			try
			{
				OnStop();
			}
			catch ( Exception e )
			{
				Log.Error( Name + ".OnStop", e );
			}
		}

		/// <summary>
		/// Starts worker thread for the service.
		/// </summary>
		public void Start()
		{
			if ( _started ) // already started?
				return;

			_started = true;

			_paused = false;

			_serviceThread.Start();

			Log.Debug( "Thread started for " + this.Name + " (ThreadId=" + _serviceThread.ManagedThreadId.ToString("x8") + ")" );
		}

		/// <summary>
		/// This method stops the service and lets the worker thread terminate.
		/// </summary>
		public void Stop()
		{
			_started = false;
		}

		/// <summary>
		/// </summary>
		public virtual bool Paused
		{
			set
			{
				Log.Debug( Name + ( ( value == true ) ? " Pausing..." : " Unpausing..." ) );

				OnPause( value );

				lock ( this )
				{
					if ( value == true )
					{
						if ( _paused == true )
						{
							Log.Debug( Name + " already Paused" );
							return;
						}

						_paused = true;
						Log.Debug( Name + " has been Paused" );
					}
					else
					{
						if ( _paused == false )
						{
							Log.Debug( Name + " already Running" );
							return;
						}

						_paused = false;

						Log.Debug( Name + " is now Running" );
					}
				}
			}
			get
			{
				lock ( this )
				{
					return _paused;
				}
			}
		}

		/// <summary>
		/// Called just prior to every call to the service's Run() method.
		/// 
		/// </summary>
		/// <returns>
		/// If the return value is false, then the Run() method is not called, effectively
		/// disabling the service.
		/// This base class implementation returns false if the Controller indicates some
		/// sort of system error is in effect.
		/// </returns>
		protected virtual bool OnRun()
		{
			return Controller.RunState == Controller.State.OK;
		}

		/// <summary>
		/// This method is called immediately after the service is paused or unpaused.
		/// If a service wishes to take some sort of action when paused or unpaused,
		/// then they should override this default implementation and do whatever they
		/// need to do in their overridden version.  This default implementation
		/// does nothing.
		/// </summary>
		/// <param name="pausing"></param>
		protected virtual void OnPause( bool pausing )
		{
		}

		/// <summary>
		/// This method is called once whenever the service is first started (i.e.,
		/// it's worker thread has just started.)
		/// If a service wishes to take some sort of one-time action when the
		/// worker thread is started, they should override this default 
		/// implementation and do that specialized work in the override.
		/// </summary>
		protected virtual void OnStart()
		{
		}

		/// <summary>
		/// This method is called whenever the service is stopped (i.e., its
		/// worker thread has terminated).
		/// If a service wishes to take some sort of action when the worker thread is stopped,
		/// then they should override this default implementation and do whatever they
		/// need to do in their overridden version.  This default implementation
		/// does nothing.
		/// </summary>
		/// <param name="pausing"></param>
		protected virtual void OnStop()
		{
		}

		protected void HandleFatalError( Exception fatalError )
		{
			Log.Error( "Fatal Exception" );
			Log.Error( fatalError );
			Controller.PerformSoftReset();
		}

		#endregion

	}

}