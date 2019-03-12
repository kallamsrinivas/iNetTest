using System;
using System.Collections.Generic;
using System.Threading;
using ISC.WinCE.Logger;



namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// Partial implemenation of the ConsoleService, containing the code for managing the 
    /// LEDs on the face plat and making the speaker beep all in a background thread owned
    /// by the ConsoleService.
    /// </summary>
    public sealed partial class ConsoleService : Service
    {
        private object _faceplateLock = new object();

        /// <summary>
        /// This flag, altered by calling EnableBeeper() method, indicates if the DS is in a mode
        /// where it should be beeping to alert to an error condition.
        /// </summary>
        private volatile bool _isBeepingEnabled;

        /// <summary>
        /// Background thread that turns on/off the LEDs or the speaker. See FaceplateThread() method.
        /// </summary>
        private Thread _faceplateThread;

        /// <summary>
        /// The background thread waits for this event to be signaled.
        /// Whenever signaled, the thread wakes up and adjust the LEDs and speaker.
        /// <summary>
        private static ManualResetEvent _faceplateEvent = new ManualResetEvent( true );

        /// </summary>
        /// Indicates which LEDs are currently supposed to be on.
        /// </summary>
        List<Controller.LEDState> _ledOnPositions = new List<Controller.LEDState>();

        /// <summary>
        /// Kicks of the background LED thread.  Intended to be called someting during construction / startup of the main console service thread.
        /// </summary>
        private void StartLEDThread()
        {
            _faceplateThread = new Thread( new ThreadStart( FaceplateThread ) );
            _faceplateThread.Name = Thread.CurrentThread.Name + "." + "Faceplate";
            _faceplateThread.Start();
        }

        private void EnableBeep( bool beep )
        {
            // if current beep mode is the same, then no reason to do anything more.
            if ( _isBeepingEnabled == beep ) 
                return;

            lock ( _faceplateLock )
            {
                _isBeepingEnabled = beep;
            }
            _faceplateEvent.Set();
        }

        private void TurnLEDOn( Controller.LEDState ledState )
        {
            lock ( _faceplateLock )
            {
                // Is this LED already the only LED that's on?  Then we don't need to do anything. Let the LED thread sleep.
                if ( _ledOnPositions.Count == 1 && _ledOnPositions[0] == ledState )
                    return;

                _ledOnPositions = new List<Controller.LEDState>() { ledState };
            }

            _faceplateEvent.Set();
        }

        private void TurnLEDOn( Controller.LEDState[] ledStates )
        {
            lock ( _faceplateLock )
            {
                if ( ledStates.Length == _ledOnPositions.Count )
                {
                    bool same = true;
                    for ( int i = 0; i < ledStates.Length; i++ )
                    {
                        if ( _ledOnPositions.FindIndex( p => p == ledStates[i] ) == -1 )
                        {
                            same = false;
                            break;
                        }
                    }
                    if ( same )
                        return;
                }
                _ledOnPositions = new List<Controller.LEDState>( ledStates );
            }

            _faceplateEvent.Set();
        }


        private void TurnLEDOn( List<Controller.LEDState> newLedStates )
        {
            lock ( _faceplateLock )
            {
                if ( newLedStates.Count == _ledOnPositions.Count )
                {
                    bool same = true;
                    foreach ( Controller.LEDState ledState in newLedStates )
                    {
                        if ( _ledOnPositions.FindIndex( p => p == ledState ) == -1 )
                        {
                            same = false;
                            break;
                        }
                    }
                    if ( same )
                        return;
                }
                _ledOnPositions = new List<Controller.LEDState>( newLedStates );
            }

            _faceplateEvent.Set();
        }

        /// <summary>
        /// Set the appropriate cradle LED based on battery charging state.
        /// </summary>
        /// <param name="chargingState"></param>
        private string SetAppropriateLEDs( ChargingService.ChargingState chargingState )
        {
            String message = string.Empty;

            // try to prevent unnecessary string allocations since this method gets called a lot.
            if ( Log.Level >= LogLevel.Trace )
                Log.Trace( Name + ": Updating battery charging state and LEDs." );

            // try to prevent unnecessary string allocations since this method gets called a lot.
            if ( Log.Level >= LogLevel.Trace )
                Log.Trace( string.Format( "{0}: Current battery charging state is \"{1}\"", Name, chargingState ) );

            List<Controller.LEDState> positionsList = new List<Controller.LEDState>();

            if ( chargingState == ChargingService.ChargingState.Charging )
            {
                positionsList.Add( Controller.LEDState.Green );
                positionsList.Add( Controller.LEDState.Yellow );

                message += GetBlankLines( 1 ) + GetMessage( chargingState.ToString() );
            }
            else if ( chargingState == ChargingService.ChargingState.TopOff )
            {
                // If instrument is in topoff mode (trickle charge), then
                // still display "Charging" message, but don't show yellow LED.
                positionsList.Add( Controller.LEDState.Green );
                message += GetBlankLines( 1 ) + GetMessage( ChargingService.ChargingState.Charging.ToString() );
            }
            else if ( chargingState == ChargingService.ChargingState.LowBattery )
            {
                positionsList.Add( Controller.LEDState.Yellow );
                message += GetBlankLines( 1 ) + GetMessage( chargingState.ToString() );
            }
            else if ( chargingState == ChargingService.ChargingState.Error )
            {
                positionsList.Add( Controller.LEDState.Red );
                message += GetBlankLines( 1 ) + GetMessage( chargingState.ToString() );
            }
            else
            {
                positionsList.Add( Controller.LEDState.Green );
            }

            TurnLEDOn( positionsList );

            return message;
        }

        private void FaceplateThread()
        {
			Log.Debug( string.Format( "{0} (ThreadId={1}) thread running.",
				Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId.ToString("x8") ) );

            while ( true )
            {
                try
                {
                    // If we need to beep, then pause one second between each beep.
                    // Otherwise, there's nothing for us to do, so we can wait forever.
                    _faceplateEvent.WaitOne( _isBeepingEnabled ? 1500 : Timeout.Infinite, false );

                    lock ( _faceplateLock )
                    {
                        if ( _isBeepingEnabled )
                        {
                            Controller.Buzz( 0.1 );
                        }

						Controller.TurnLEDsOn( _ledOnPositions );
                    }

                    _faceplateEvent.Reset();
                }
                catch ( Exception e )
                {
                    Log.Error( e );
                }
            }
        }
    }
}