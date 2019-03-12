using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.Instrument.Driver;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// Determines current charging status of the docked instrument.
    /// </summary>
    public class InstrumentChargingOperation : NothingAction , IOperation
    {
        #region Constructors

        public InstrumentChargingOperation()
        {
            InstrumentChargePhase = ISC.iNet.DS.DomainModel.ChargePhase.ChargeOff;
            BatteryCode = BatterySerialNumber = InstrumentSerialNumber = string.Empty;
        }

        #endregion Constructors

        #region Properties

        public ChargingService.ChargingState ChargingState
        {
            get
            {
                if ( InstrumentChargePhase == ISC.iNet.DS.DomainModel.ChargePhase.PreCharge
                ||   InstrumentChargePhase == ISC.iNet.DS.DomainModel.ChargePhase.FullCharge
				||   InstrumentChargePhase == ISC.iNet.DS.DomainModel.ChargePhase.Taper )
                    return ChargingService.ChargingState.Charging;

                if ( InstrumentChargePhase == ISC.iNet.DS.DomainModel.ChargePhase.TopOff )
                    return ChargingService.ChargingState.TopOff;

                if ( InstrumentChargePhase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeComplete )
                    return ChargingService.ChargingState.NotCharging;
                
                // "ChargeOff" is intended to only occur for Alkaline batteries.
                // In reality, though, the instrument will sometimes report ChargeOff
                // when it has a Lithium battery.  Therefore, when we see a ChargeOff,
                // we just report it as an Error.

                // 1/11/08 JAM - added Alkaline MX4 battery pack here.
                if ( InstrumentChargePhase == ISC.iNet.DS.DomainModel.ChargePhase.ChargeOff &&
                    ( BatteryCode == DomainModel.BatteryCode.MX6Alkaline || BatteryCode == DomainModel.BatteryCode.MX4Alkaline ) )
                    return ChargingService.ChargingState.NotCharging;

                return ChargingService.ChargingState.Error;
            }
        }

        public ISC.iNet.DS.DomainModel.ChargePhase InstrumentChargePhase { get; private set; }

        public string BatteryCode { get; private set; }

        public string BatterySerialNumber { get; private set; }

        public string InstrumentSerialNumber { get; private set; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Determines current charging status of the docked instrument.
        /// </summary>
        /// <returns>
        /// The returned DockingStationEvent is always null.
        /// </returns>
        /// <exception cref="InstrumentNotDockedException">
        /// If instrument is undocked
        /// </exception>
        /// <exception cref="InstrumentPingFailedException">
        /// Failure to turn on the instrument.
        /// </exception>
        public DockingStationEvent Execute()
        {
            if ( Configuration.DockingStation.Type != DeviceType.MX4 && Configuration.DockingStation.Type != DeviceType.MX6 )
                return null;

            if ( !Controller.IsDocked() )
                throw new InstrumentNotDockedException();

            Log.Debug( this.Name + ".Execute" );

            using (InstrumentController instrumentController = SwitchService.CreateInstrumentController())
            {
                // Turn on the instrument, the ask it for it's charging status
				try
				{
					// Note that we deliberately do NOT use batch mode.  We're only trying to read a very
					// few number of messages.  So it's not worth the effort and time it takes to negotiate
					// faster baud rate and establish a batched connection to the instrument. It's much
					// quicker to just read the few registers at the slow baud rate.
					instrumentController.Initialize( InstrumentController.Mode.NoLid /* lid not necessary */ );

					InstrumentSerialNumber = instrumentController.GetSerialNumber();

					BatteryCode = instrumentController.GetBatteryCode();

					InstrumentChargePhase = instrumentController.GetChargePhase();

					Log.Warning( string.Format( "{0}: BatteryCode={1}, ChargePhase=\"{2}\"",
						this.Name, BatteryCode, InstrumentChargePhase ) );
				}
				catch ( InstrumentPingFailedException ipef ) // Couldn't turn on the instrument?
				{
					// will get a ping failure if undocked.
					if ( !Controller.IsDocked() )
						throw new InstrumentNotDockedException();

					Log.Error( this.Name, ipef );
					throw;
				}
				catch ( CommunicationAbortedException cae ) // thrown by driver when instrument is undocked.
				{
					Log.Error( this.Name, cae );
					throw new InstrumentNotDockedException();
				}
            }
            return null;
        }

        #endregion

    }

}