using System;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

    ///////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// For MX6 docking stations only, this thread monitors the charging status
    /// of a docked instrument.
    /// </summary>
    public sealed class ChargingService : Service
    {
        /// <summary>
        /// Enumerates the states for the charger.
        /// </summary>
        public enum ChargingState
        {
            NotCharging,
            Charging,
            TopOff,
            Error,
            LowBattery, // SGF  Feb-13-2009  DSW-223
        }

        #region Fields

#if DEBUG
        /// <summary>
        /// Poll the instrument everytime this amount of time has elapsed.
        /// </summary>
        private readonly TimeSpan _executeInterval = new TimeSpan( 0, 1, 0 ); // (h,m,s)
#else
        /// <summary>
        /// Poll the instrument everytime this amount of time has elapsed.
        /// </summary>
        private readonly TimeSpan _executeInterval = new TimeSpan( 0, 5, 0 ); // (h,m,s)
#endif
        /// <summary>
        /// Max amount of time instrument can stay in OverTempFailure or UnderTempFailure.
        /// </summary>
        private readonly TimeSpan _maxTempFailureTimeAllowed = new TimeSpan(1,0,0); // (h,m,s)
        /// <summary>
        /// Max amount of time instrument can stay in error (non-TempFailure)
        /// </summary>
        private readonly TimeSpan _maxErrorPhaseAllowed = new TimeSpan(0,15,0); // (h,m,s)

        /// <summary>
        /// How long has it been since the last execute interval occurred.
        /// </summary>
        private TimeSpan _currentInterval = TimeSpan.Zero;

        // We start out assuming the instrument is needs charged until we find out otherwise.
        private ChargingState _chargingState = ChargingState.Charging;

        /// <summary>
        /// Used to keep track of consecutively occurring phases, and when they first occur.
        /// </summary>
        private ChargePhase _phase = (ChargePhase)(-1);

        /// <summary>
        /// Used to keep track of consecutively occurring phases, and when they first occur.
        /// </summary>
        private DateTime _phaseStart = DateTime.UtcNow;

        private string _batteryCode = string.Empty;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of a WatchDogService class.
        /// </summary>
        public ChargingService( Master master ) : base( master )
        {
            IdleTime = new TimeSpan(0,0,10); // (h,m,s)
        }

        #endregion

        #region Methods

        /// <summary>
        /// The battery code of the current battery.
        /// </summary>
        public string BatteryCode
        {
            get
            {
                // We synchronize access to this property due to this return expression.
                lock ( this )
                {
                    return _batteryCode == null ? string.Empty : _batteryCode;
                }
            }
            set
            {
                lock ( this )
                {
                    _batteryCode = value;
                }
                if ( BatteryCode != string.Empty )
                    Log.Debug( string.Format( "{0} notified about battery: \"{1}\"", Name, BatteryCode ) );
                else
                    Log.Debug( string.Format( "{0} notified about absent battery", Name ) );
            }

        }

        /// <summary>
        /// Returns whether or not the docked instrument's battery is rechargeable or not.
        /// <para>
        /// False is also returned if nothing is docked or the battery code is unknown.
        /// </para>
        /// </summary>
        /// <returns>
        /// </returns>
        public bool IsBatteryRechargable()
        {
            return Controller.IsDocked() ? DomainModel.BatteryCode.IsRechargable( BatteryCode ) : false;
        }

        /// <summary>
        /// 
        /// </summary>
        public ChargingState State
        {
            get
            {
                lock ( this ) 
                {
                    if ( !Controller.IsDocked() )
                        return ChargingState.NotCharging;

                    // If we're not rechargeable, then charging state is set to 'error' / 'no error' based 
                    // on IDS's ability to ping the instrument during discovery.  Just return this state.
                    if ( !Configuration.DockingStation.IsRechargeable() )
                        return _chargingState;

                    // Checking isdocked isn't enough as it might return true before instrument has been fully discovered and a battery code is know.
                    if ( !Controller.IsDocked() || Master.SwitchService.Instrument == null || Master.SwitchService.Instrument.SerialNumber == string.Empty )
                        return _chargingState;

                    // If battery is alkaline, assume not charging.
                    // 1/11/08 JAM - add the MX4 alkaline battery pack here.
                    if ( ( BatteryCode == DomainModel.BatteryCode.MX6Alkaline ) || ( BatteryCode == DomainModel.BatteryCode.MX4Alkaline ) )
                        return ChargingState.NotCharging;

                    // These are the only battery codes that MX6 can charge.
                    if ( IsBatteryRechargable() )
                    {
                        // Whenever we're paused, then assume we're charging since
                        // we don't really know.
                        if ( Paused && _chargingState != ChargingState.Error )
                            return ChargingState.Charging;
                        
                        return _chargingState;
                    }

                    // SGF  Feb-13-2009  DSW-223
                    if ( _chargingState == ChargingState.LowBattery )
                    {
                        Log.Debug( string.Format("{0} returning {1}", Name, _chargingState.ToString() ) );
                        return _chargingState;
                    }

                    // No or unknown battery code means we don't know what the heck is going on.
                    Log.Debug( string.Format( "{0} returning Error.  Actual state is {1}, BatteryCode is \"{2}\"",Name, _chargingState.ToString(), BatteryCode ) );

                    return ChargingState.Error;
                }
            }
            set
            {
                lock ( this )
                {
                    if ( _chargingState != value )
                        Log.Debug( string.Format( "{0} changing state from {1} to {2}", Name, _chargingState, value ) );
                    _chargingState = value;
                }
            }
        }
        


        /// <summary>
        /// Unpause the service, and immediately force it to update the charge status.
        /// </summary>
        public void Restart()
        {
            Log.Debug( Name + " Restarting..." );
            lock ( this )
            {
                // Setting the interval equal to executeInterval will cause the charge status
                // to be updated on the service's next 'tick'.
                _currentInterval = new TimeSpan( _executeInterval.Ticks );

                // Whenever we restart the service, assume a docked instrument is charging (until we find out otherwise)
                if ( IsBatteryRechargable() )
                {
                    State = ChargingState.Charging;
                    _phase = ChargePhase.FullCharge;
                }
                else
                {
                    //State = ChargingState.NotCharging;
                    _phase = ChargePhase.ChargeOff;
                }

                _phaseStart = DateTime.UtcNow;

                Paused = false;
            }
        }

        /// <summary>
        /// This method implements the thread start for this service.
        /// </summary>
        protected override void Run()
        {
            if ( !Configuration.DockingStation.IsRechargeable() )
                return;

            if ( !Controller.IsDocked() )
                return;

            // Do all this within a locking of paused.  That way, if anybody tries to
            // pause this service, they'll block until this code communicating with the
            // instrument is finished.
            lock ( this )
            {
                if ( Paused )
                {
                    Log.Debug( Name + " is currently paused." );
                    return;
                }

                _currentInterval = _currentInterval + IdleTime;

                if ( _currentInterval < _executeInterval )
                {
                    Log.Trace( Name + " tick " + _currentInterval.ToString() );
                    return;
                }

  
                if ( Master.ExecuterService.DeadBatteryCharging == true )
                {
					Log.Debug( "Skipping check.  Waiting for dead battery to charge." );
                }
                else if ( IsBatteryRechargable() && ( State == ChargingState.NotCharging ) )
                    Log.Debug( Name + " Skipping check. Instrument already reported as fully charged." );
                // 1/11/08 JAM - added Alkaline MX4 battery pack here.
                else if ( ( BatteryCode == DomainModel.BatteryCode.MX6Alkaline ) || ( BatteryCode == DomainModel.BatteryCode.MX4Alkaline ) )
                    Log.Debug( Name + " Skipping check. Instrument's has an alkaline battery." );
                else
				{
					// DO NOT update the chargingstate outside of the thread lock because it's communication with the instrument
					// will interfere when the ExecuterService is trying to communicate with the instrument.
                    RunUpdateChargerState();
				}

                _currentInterval = TimeSpan.Zero; // reset
            }                
        }

        /// <summary>
        /// Helper method for Run() which communicates with instrument to determine its charging status
        /// then updates charger service status properties
        /// </summary>
		private void RunUpdateChargerState()
		{
			const string funcName = "RunUpdateChargerState";

			Log.Debug( string.Format( "{0} invoking {1} on a \"{2}\" battery", Name, funcName, BatteryCode ) );

			if ( !Controller.IsDocked() )
			{
				Log.Debug( string.Format( "{0}: instrument undocked.", funcName ) );
				return;
			}

			try
			{
				InstrumentChargingOperation op = new InstrumentChargingOperation();

				op.Execute();
				
				// Keep track of consecutively occurring phases, and when they first occur.
				if ( _phase != op.InstrumentChargePhase )
				{
					_phase = op.InstrumentChargePhase;
					_phaseStart = DateTime.UtcNow;
				}

				// If instrument is having any charging problems, report the error to server.
				// This will also allow the error to upload to iNet if they're an inet customer.
				if ( _phase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeFailure
				|| _phase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeFault
					//                ||   _phase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeOverTempFailure
					//                ||   _phase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeUnderTempFailure
					//                ||   _phase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeOff ( AND battery is lithium )
				|| _phase == ISC.iNet.DS.DomainModel.ChargePhase.PreChargeFault
				|| _phase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeTimeout )
				{
					// Do not change this message.  iNet is parsing it!
					string msg = string.Format( "Instrument \"{0}\" has reported a \"{1}\" charging error (Battery=\"{2}\", type=\"{3}\").",
						op.InstrumentSerialNumber, _phase.ToString(), op.BatterySerialNumber, op.BatteryCode );
					Master.ReporterService.ReportError( new DockingStationError( msg, DockingStationErrorLevel.Warning, op.InstrumentSerialNumber ) );
				}

				// BatteryCode should be empty if user has docked a completely dead
				// battery.  If the instrument has now charged up enough such that we
				// can now talk to it, then get the battery type.
				if ( BatteryCode == string.Empty )
					BatteryCode = op.BatteryCode;

				if ( op.ChargingState == ChargingState.Error )
				{
					// How much time has elasped between now and when we first saw the 
					// instrument go into error?

					TimeSpan errorTimeElapsed = DateTime.UtcNow - _phaseStart;

					Log.Debug( string.Format( "{0} - BATTERY IN {1} PHASE!  ELAPSED TIME: {2}", Name, _phase.ToString(), errorTimeElapsed.ToString() ) );

					// Instrument hasn't been in error too long?  Then
					// just treat it as if the intrument is normally charging; no need to
					// scare the user.  But if it's been stuck in error too
					// long, then finally just report it as an error.

					TimeSpan maxTimeAllowed = ( _phase == ChargePhase.ChargeOverTempFailure || _phase == ChargePhase.ChargeUnderTempFailure ) ? _maxTempFailureTimeAllowed : _maxErrorPhaseAllowed;

					if ( errorTimeElapsed <= maxTimeAllowed )
					{
						State = ChargingState.Charging;
					}
					else
					{
						State = ChargingState.Error;

						// Do not change this message.  iNet is parsing it!
						string msg = string.Format( "Instrument \"{0}\" has been in \"{1}\" for an overextended period of time ({2}). (Battery=\"{3}\", type=\"{4}\")",
							op.InstrumentSerialNumber, _phase.ToString(), errorTimeElapsed.ToString(), op.BatterySerialNumber, op.BatteryCode );

						Log.Error( string.Format( "{0} - {1}", Name, msg.ToUpper() ) );
						Log.Error( string.Format( "{0} - Reporting {1} to server", Name, op.InstrumentChargePhase.ToString() ) );

						Master.ReporterService.ReportError( new DockingStationError( msg, DockingStationErrorLevel.Warning, op.InstrumentSerialNumber ) );
					}
				} // end ChargingState.Error

				else // op.State != ChargingState.Error
				{
					State = op.ChargingState;
				}

				Log.Debug( Name + " - ChargingState=" + State );

				// When instrument is done charging, then turn it off!
				if ( State == ChargingState.NotCharging )
				{
					Log.Debug( Name + " - INSTRUMENT APPEARS FULLY CHARGED. TURNING OFF INSTRUMENT." );
					Log.Debug( Name + " - WILL NO LONGER POLL INSTRUMENT FOR CHARGING STATUS." );
                    new InstrumentTurnOffOperation( InstrumentTurnOffOperation.Reason.ChargingComplete ).Execute();
				}
			}
			catch ( InstrumentPingFailedException e )
			{
				Log.Error( Name, e );
				if ( !Controller.IsDocked() ) // May have got the exception because they undocked. Ignore it.
					State = ChargingState.NotCharging;
				else
					State = ChargingState.Error;  // Couldn't ping the instrument.  dead battery?
			}
			catch ( Exception e )
			{
				Log.Error( Name, e );

				// Whatever the error was, if the instrument isn't present, then it's
				// not charging.
				if ( !Controller.IsDocked() )
				{
					State = ChargingState.NotCharging;
				}
				// Not sure what the error is.  Leave charging state as is.
			}
		}

        #endregion
    }
}
