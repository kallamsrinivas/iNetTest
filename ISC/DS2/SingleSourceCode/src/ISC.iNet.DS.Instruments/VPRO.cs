using System;
using System.Collections.Generic;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;
using ISC.Instrument.Driver;
using ISC.Instrument.TypeDefinition;
using AlarmEvent = ISC.iNet.DS.DomainModel.AlarmEvent;
using SensorStatuses = ISC.iNet.DS.DomainModel.SensorStatuses;

namespace ISC.iNet.DS.Instruments
{
	/// <summary>
	/// InstrumentController subclass for Ventis Pro Series instruments.
	/// </summary>
	public class VPRO : InstrumentController
	{
		#region Fields
		
		// Used only by AccessoryPump call. MinValue = uninitialized, i.e., need to find out
		// if instrument has a pump or not.
		private AccessoryPumpSetting _accessoryPump = (AccessoryPumpSetting)int.MinValue;

		#endregion

		#region Constructors

		/// <summary>
		/// Instrument controller class for Ventis Pro Series instruments.
		/// </summary>
		public VPRO() : base( new VentisProDriver() ) { }

		/// <summary>
		/// Used by the FactoryVPRO class.
		/// </summary>
		/// <param name="driver"></param>
		protected VPRO( VentisProDriver driver ) : base( driver ) { }
		
		#endregion

		#region Properties

		/// <summary>
		/// TODO - this is an exact copy of MX6.AccessoryPump.  We should just have one method.
		/// </summary>
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

		#endregion

		#region Methods

		/// <summary>
		/// Open the serial port connection needed to communicate with the instrument and ping ( wake up ) the instrument.
		/// </summary>
		/// <param name="mode"></param>
		public override void Initialize( Mode mode )
		{
			_mode = mode;

            if ( ( mode & Mode.NoPing ) != 0 ) // 'don't ping' specified? then just return.
                return;

            Ping( ( mode & Mode.Batch ) != 0 );

			if ( this.AccessoryPump != AccessoryPumpSetting.Installed )
			{
				CheckDiffusionLid();
                // user might undock while the DS is waiting for them to lower the lid.
				if ( !Controller.IsDocked() )
                    throw new InstrumentNotDockedException( "Instrument undocked while waiting for diffusion lid." );
			}
			else // instrument has a pump
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
		/// TODO - this is an exact copy of MX6.CheckDiffusionLid().  Both methods should 
		/// just merged into one.  Propaby just put the method in Controller since
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
				Log.Debug( "DOCKING STATION IS NOT CONFIGURED PROPERLY." );
				throw new HardwareConfigurationException( HardwareConfigErrorType.FlipperAndLidError );
			}

			Log.Debug( "Diffusion Lid is properly lowered.  Ready to go." );
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
		/// Get a list of a all of the users on an instrument, except the active one.
		/// </summary>
		/// <returns>An array list with all of the users, duplicates removed.</returns>
		public override List<string> GetUsers()
		{
			List<string> list = new List<string>();

			// Ventis Pro Series has only one user - the 'Active' user.
			string user = GetActiveUser();
			if ( user.Length > 0 )
				list.Add( user );

			return list;
		}

		/// <summary>
		/// Sets the instrument users to the appropriate values.
		/// </summary>
		/// <param name="users">The list of users.</param>
		public override void SetUsers( List<string> users )
		{
			string oldUser = GetActiveUser();

			if ( users.Count == 0 && oldUser == string.Empty )
				return;

			if ( users.Count > 1 )
				Log.Error( "WARNING: detected attempt to set " + users.Count + " users for VPRO" );

			// set active user only if it's different than what is current in the instrument
			string newUser = ( users.Count > 0 ) ? (string)users[0] : string.Empty;
			if ( oldUser != newUser )
				SetActiveUser( newUser );

			return;
		}

		/// <summary>
		/// Get a list of a all of the sites on an instrument, except the active one.
		/// </summary>
		/// <returns>An array list with all of the sites, duplicates removed.</returns>
		public override List<string> GetSites()
		{
			List<string> list = new List<string>();

			// Ventis Pro Series has only one site - the 'Active' site.
			string site = GetActiveSite();

			if ( site.Length > 0 )
				list.Add( site );

			return list;
		}

		/// <summary>
		/// Sets the instrument sites to the appropriate values.
		/// </summary>
		/// <param name="sites">The list of sites.</param>
		/// <param name="details">Where to record the details.</param>
		public override void SetSites( List<string> sites )
		{
			string oldSite = GetActiveSite();

			if ( sites.Count == 0 && oldSite == string.Empty )
				return;

			if ( sites.Count > 1 )
				Log.Debug( "WARNING: detected attempt to set " + sites.Count + " sites for VPRO" );

			// set active site if it's different than what is current only the instrument

			string newSite = ( sites.Count > 0 ) ? (string)sites[0] : string.Empty;
			if ( oldSite != newSite )
				SetActiveSite( newSite );

			return;
		}

		public override double GetSensorPeakReading( int sensorPosition, double resolution )
		{
			// this is supported by Ventis Pro Series - get the user peak reading from the sensor.
			return Driver.getPeakReading( sensorPosition );
		}

		/// <summary>
		/// Retrieves sensor gas responses for manual gas operations performed on the docked instrument.
		/// </summary>
		/// <returns>An array of sensor gas responses with responses for virtual sensors removed.</returns>
		public override SensorGasResponse[] GetManualGasOperations()
		{
			SensorGasResponse[] gasResponses = base.GetManualGasOperations();

			// Nobody (iNet server, nor iNet DS) is interested in SGR's for "virtual" sensors;
			// so, we need to just throw them away.
			List<SensorGasResponse> sgrList = new List<SensorGasResponse>( gasResponses.Length );

			foreach ( SensorGasResponse sgr in gasResponses )
			{
				if ( !InstrumentTypeDefinition.IsVirtualSerialNumber( sgr.SerialNumber ) )
					sgrList.Add( sgr );
			}

			return sgrList.ToArray();
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

		/// <summary>
		/// Get status of the current calibration operation.
		/// </summary>
		/// <param name="position">The position of the sensor to check.</param>
		/// <returns>The cal status of the sensor. 
		/// true - sensor is calibrating; 
		/// false - sensor is not calibrating; 
		/// null - InstrumentAborted</returns>
		public override bool? IsSensorCalibrating( int pos )
		{
			bool? isCalibrating = false;

			// instruments dockable on MX4 docking stations have been losing contact with the 
			// charging pins on the DS  which causes the instrument to leave the Calibrating mode.  
			// The instrument  likely goes to Charging mode when this happens.  However, we need to read the 
			// instrument operating mode before the sensor mode to prevent timing issues.  
			// These can occur when the sensor finishes a normal calibration and both the 
			// sensor and the instrument transition back to Running mode.
			OperatingMode opMode = Driver.getOperatingMode();

			// If the sensor reports it is still calibrating, verify that the instrument
			// is on the same page.
			if ( Driver.isSensorCalibrating( pos ) )
			{
				if ( opMode == OperatingMode.Calibrating )
				{
					isCalibrating = true;
				}
				else
				{
					Log.Debug( "******************************************" );
					Log.Debug( string.Format( "* INSTRUMENT IS NOT IN CALIBRATING MODE! *  Instrument is in \"{0}\" mode.", opMode.ToString() ) );
					Log.Debug( "******************************************" );

					// return null to indicate that the instrument has reset
					isCalibrating = null;
				}
			}

			return isCalibrating;
		}

		#endregion
	}
}
