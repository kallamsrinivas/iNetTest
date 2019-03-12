using System;
using System.Collections;
using System.Data;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS;
using ISC.WinCE.Logger;
using ISC.Instrument.Driver;



namespace ISC.iNet.DS.Instruments
{
    /// <summary>
    /// Summary description for MX6.
    /// </summary>
    public class MX6 : InstrumentController
    {
        // used only by AccessoryPump call. MinValue = uninitialized, i.e., need to find out
        // if instrument has a pump or not.
        private AccessoryPumpSetting _accessoryPump = (AccessoryPumpSetting)int.MinValue;

        #region Constructors

        /// <summary>
        /// Instrument controller class for MX6 instruments.
        /// </summary>
        public MX6() : base( new Mx6Driver() ) { }

		/// <summary>
		/// Used by the FactoryMX6 class.
		/// </summary>
		/// <param name="driver">The factory driver instance created by the FactoryMX6 class.</param>
		protected MX6( Mx6Driver driver )
			: base( driver )
		{

		}

        #endregion

        #region Methods

        /// <summary>
        /// Open the serial port connection needed to communicate with the instrument and ping ( wake up ) the instrument.
        /// </summary>
        /// <param name="mode"></param>
        public override void Initialize( Mode mode )
        {
            // For MX6 docking station, pull the charge pin low which is used Power up instrument here.  
            // Pulling this pin low causes the IDS to power on the instrument.  But we need to keep
            // it low since the instrument uses that to know that it's docked.
            // This should already be being done once during bootup.  We do it again here just to be sure.
            Controller.PowerOnMX6(true);

            if ( ( mode & Mode.NoPing ) != 0 ) // 'don't ping' specified? then just return.
                return;

            // If not doing batch mode, use slow baud rate.
            if ( ( mode & Mode.Batch ) == 0 )
                Driver.setPortSpeed( 9600 );

            // Before calling base.Initialize, we need to ping the instrument ourself since
            // this instrument's Ping has special logic for powering on the instrument.
            Ping( ( mode & Mode.Batch ) != 0 );
            
            // NoLid is only passed in by CheckInstrumentChargingOperation.  For this operation,
            // there's no reason to check the lid, or look for presence of pump, or swwitch the
            // lid solenoid, etc.
            if ( ( mode & Mode.NoLid ) != 0 )
                return;

            // If pump is not present, then make sure the lid is down
            if ( this.AccessoryPump != AccessoryPumpSetting.Installed )
            {
                CheckDiffusionLid();
                // user might undock while the DS is waiting for them to lower the lid.
                if ( !Controller.IsDocked() )
                    throw new InstrumentNotDockedException( "Instrument undocked while waiting for diffusion lid." );
            }
            else  // instrument has a pump
            {
                // Should we do anything special?
            }

            // LED #2 controls the solenoid that routes gasflow to either 
            // the diffusion lid or pump hose.  Switch it accordingly.
            Controller.SetCradleSolenoid( this.AccessoryPump );
        }

        /// <summary>
        /// Wait for user to lower the diffusion lid.
        /// </summary>
        /// TODO - this is an exact copy of MX4.CheckDiffusionLid().  Both methods should 
        /// just merged into one.  Probably just put the method in Controller since
        /// most of the calls this method is making are there anyways.
        private void CheckDiffusionLid()
        {
            TimeSpan lidTimeout = new TimeSpan( 0, 0, 10 ); // seconds
            TimeSpan lidSleepTime = new TimeSpan( 0, 0, 0, 0, 250 ); // millis
            TimeSpan lidWait = new TimeSpan( 0, 0, 0 );

            bool lidDown = Controller.IsDiffusionLidDown();

            while ( Controller.IsDocked() && !lidDown && ( lidWait < lidTimeout ) )
            {
                if ( ( lidWait.TotalMilliseconds % 1000 ) == 0 )
                    Log.Debug( "Waiting for Diffusion Lid to be lowered..." );
                Thread.Sleep( (int)lidSleepTime.TotalMilliseconds );
                lidWait = lidWait.Add( lidSleepTime );
                lidDown = Controller.IsDiffusionLidDown();
            }

            if ( !Controller.IsDocked() )
                return;

            if ( !Controller.IsDiffusionLidDown() )
            {
                Log.Debug( "LID IS NOT LOWERED." );
                throw new HardwareConfigurationException(HardwareConfigErrorType.LidError, "LID IS NOT LOWERED.");
            }

            Log.Debug( "Diffusion Lid is properly lowered.  Ready to go." );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// TODO - this is an exact copy of MX4.AccessoryPump.  We should just have one method.
        public override AccessoryPumpSetting AccessoryPump
        {
            get
            {
                // Try to prevent repeated calls asking if the pump is present or not.
                // as it seems to be prone to returning an error. Once we find out
                // if it has a pump, remember it.
                if ( _accessoryPump == (AccessoryPumpSetting)int.MinValue )
                    _accessoryPump = Driver.isAccessoryPumpInstalled() ? AccessoryPumpSetting.Installed : AccessoryPumpSetting.Uninstalled;

                return _accessoryPump;
            }
        }

        /// <summary>
        /// Turn off (shut down) the instrument.
        /// </summary>
        public override void TurnOff()
        {
            base.TurnOff();

            Controller.PowerOnMX6(false);
        }

        /// <summary>
        /// Enable or disable the specified sensor.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="enabled"></param>
        public override void EnableSensor( int pos, bool enabled )
        {
            Driver.enableSensor( pos, enabled );
        }

        /// <summary>
        /// Indicates if sensor is enabled or disabled.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public override bool IsSensorEnabled( int pos )
        {
            return Driver.isSensorEnabled( pos );
        }

        /// <summary>
        /// Retrieves the battery code based on battery type.
        /// </summary>
        /// <returns>Standardized battery code</returns>
        public override string GetBatteryCode()    
        {
            return Driver.getBatteryType();
        }

        /// <summary>
        /// Pause or unpause the specified sensor
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="paused"></param>
        public override void PauseSensor( int pos, bool paused )
        {
            Log.Debug( paused ? "Pausing sensor" : "Unpausing sensor" );

            Driver.pauseSensor( pos, paused );
        }

        #endregion

    }  // end-class

} // end-namespace
