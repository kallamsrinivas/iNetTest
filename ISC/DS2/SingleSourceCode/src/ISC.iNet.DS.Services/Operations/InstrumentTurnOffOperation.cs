using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.Instrument.Driver;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to turn off an instrument.
	/// </summary>
	public class InstrumentTurnOffOperation : NothingAction, IOperation
	{
        public enum Reason
        {
            Idle,
            ChargingComplete
        }

        private Reason _reason;

		/// <summary>
		/// Creates a new instance of InstrumentTurnOffOperation class.
		/// </summary>
		public InstrumentTurnOffOperation( Reason reason )
        {
            _reason = reason;
        }

        public InstrumentTurnOffOperation( NothingAction nothingAction ) : base( nothingAction )
        {
            _reason = Reason.Idle; // TODO: Is this a reasonable default?
        }

		/// <summary>
		/// Executes a turn off instrument operation.
		/// </summary>
        /// <returns>A InstrumentTurnOffEvent, which contains the TurnOffAction 
        /// indicating what exactly the Execute() decided to do.
        /// </returns>
		/// <exception cref="InstrumentNotDockedException">If an instrument is not docked.</exception>
		public DockingStationEvent Execute()
		{
            string funcName =  Name + ".Execute";

			Log.Debug(funcName + ", Reason=" + _reason );

            InstrumentTurnOffEvent _returnEvent = new InstrumentTurnOffEvent( this );
#if DEBUG
			// When debugging, don't turn off the GBPROs since they annoyingly take too long to turn back on.
			if ( Configuration.DockingStation.Type == DeviceType.GBPRO )
			{
                _returnEvent.TurnOffAction = TurnOffAction.None;
				Log.Info( string.Format( "{0}.Execute doing nothing due to DEBUG directive", Name ) );
                return _returnEvent;
			}
#endif
            _returnEvent.TurnOffAction = GetTurnOffAction();

            if ( _returnEvent.TurnOffAction == TurnOffAction.NotSupported )
			{
                Log.Warning( string.Format( "{0}: Docked {1}'s cannot be turned off.", funcName, Configuration.DockingStation.Type ) );
                return _returnEvent;
			}
            else if ( _returnEvent.TurnOffAction == TurnOffAction.None )
			{
                Log.Warning( string.Format( "{0}: Docked {1} should already be off.", funcName, Configuration.DockingStation.Type ) );
                return _returnEvent;
			}
			else 
			{
				if ( !Controller.IsDocked() )
					throw new InstrumentNotDockedException();

				using ( InstrumentController instrumentController = SwitchService.CreateInstrumentController() )
				{
					// Note that we specify NoPing.  This causes the instrument controller to *not* call the
					// driver's connect method.  
					instrumentController.Initialize( InstrumentController.Mode.NoPing );
                    try
                    {
                        if ( _returnEvent.TurnOffAction == TurnOffAction.Shutdown )
                        {
                            // Even the instruments which cannot shutdown will have this called
                            // so their sensors get turned off.
                            instrumentController.TurnOff();
                        }
                        else if ( _returnEvent.TurnOffAction == TurnOffAction.TurnOffSensors )
                        {
							if ( instrumentController.GetOperatingMode() == OperatingMode.WarmingUp )
							{
								// MX6 instrument does not support going to Charging mode when it is currently WarmingUp.
								// So we need to try again later after the instrument has transitioned to Running.
								_returnEvent.TurnOffAction = TurnOffAction.Postponed;
								Log.Debug( funcName + ", WAITING FOR WARM UP TO COMPLETE.  TURN OFF POSTPONED." );
							}
							else
							{
								// This should only need to be called for MX6 rechargeable instruments.
								instrumentController.TurnOnSensors( false, false );
							}
                        }
                    }
                    catch ( CommunicationException ce )
                    {
                        Log.Debug( funcName + " caught CommunicationException: " + ( Log.Level >= LogLevel.Trace ? ce.ToString() : ce.Message ) );
                        Log.Debug( funcName + " FAILED. Instrument might already be in OFF state." );
                    }
				}
			}

            return _returnEvent;
		}

        private TurnOffAction GetTurnOffAction()
		{
			// GB Plus instruments cannot be shutdown and their sensors do not power off.
			if ( Configuration.DockingStation.Type == DeviceType.GBPLS )
                return TurnOffAction.NotSupported;

			// This should be the reason provided by the ExecuterService.
            if ( _reason == Reason.Idle )
			{
				if ( Configuration.DockingStation.Type != DeviceType.MX6 || Master.Instance.ChargingService.BatteryCode == BatteryCode.MX6Alkaline )
				{
					// For non-MX6 rechargeable instruments they should be told to shutdown.  
					// For alkaline instruments (GB Pro, TX1, MX6 Alkaline) this should put the
					// instrument in a low power state as well as turning off their sensors.
					// For the other instruments (MX4, VLS, VPRO, SC), they are not designed to 
					// power off completely, but this will have the affect of turning off their
					// sensors.
                    return TurnOffAction.Shutdown;
				}
				else // MX6 Rechargeable
				{
					// We want to leave rechargeable MX6 instruments on while they charge, 
					// but we want to turn off their sensors when there are no more operations
					// to execute so the battery charges faster.
                    return TurnOffAction.TurnOffSensors;
				}
			}

			// This should be the reason provided by the ChargingService.
            if ( _reason == Reason.ChargingComplete )
			{
				if ( Configuration.DockingStation.Type == DeviceType.MX6 && Master.Instance.ChargingService.BatteryCode != BatteryCode.MX6Alkaline )
				{
					// The rechargeable MX6 is now fully charged so it can be shutdown.
                    return TurnOffAction.Shutdown;
				}
			}

            return TurnOffAction.None; // Non-MX6 rechargeable instruments should already be in an off state.
		}

        /// <summary>
        ///This method returns the string representation of this class.
        /// </summary>
        /// <returns>The string representation of this class</returns>
        public override string ToString()
        {
            return "Instrument Turn Off (" + _reason + ")";
        }
	}
}