using System;
using System.Collections.Generic;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;
using ISC.Instrument.Driver;
using ISC.Instrument.TypeDefinition;
using AlarmEvent = ISC.iNet.DS.DomainModel.AlarmEvent;
using SensorStatuses = ISC.iNet.DS.DomainModel.SensorStatuses;

namespace ISC.iNet.DS.Instruments
{
	public class SC : InstrumentController
	{
		#region Fields

		// Used only by AccessoryPump call. MinValue = uninitialized, i.e., need to find out
		// if instrument has a pump or not.
		private AccessoryPumpSetting _accessoryPump = (AccessoryPumpSetting)int.MinValue;

		#endregion

		/// <summary>
		/// Instrument controller class for SafeCore instrument modules.
		/// </summary>
		public SC() : base( new SafeCoreDriver() ) { }

		/// <summary>
		/// Used by the FactorySC class.
		/// </summary>
		protected SC( SafeCoreDriver driver ) : base( driver ) { }


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

		/// <summary>
		/// Returns the instrument's country of origin.
		/// </summary>
		/// <returns></returns>
		public override string GetCountryOfOriginCode()
		{
			return Driver.getCountryOfOrigin().ToString().ToUpper();
		}

		public override double GetSensorPeakReading( int sensorPosition, double resolution )
		{
			// this is supported by SafeCore - get the user peak reading from the sensor.
			return Driver.getPeakReading( sensorPosition );
		}

		/// <summary>
		/// Gets an empty list of base units the instrument module has been
		/// docked on by default.
		/// </summary>
		/// <returns></returns>
		public override List<BaseUnit> GetBaseUnits()
		{
			List<BaseUnit> baseUnits = new List<BaseUnit>(); 
			BaseUnitInfo[] driverBases = Driver.getBaseUnitInfos();

			foreach ( BaseUnitInfo bui in driverBases )
			{
				BaseUnit unit = new BaseUnit();

				if ( bui.EquipmentType == EquipmentType.RadiusBZ1 )
					unit.Type = DeviceType.BZ1;
				else
				{
					Log.Warning( "GetBaseUnits: Unexpected type detected: \"" + bui.EquipmentType.ToString() + "\"" );
					continue;
				}

				unit.SerialNumber = bui.SerialNumber;
				unit.PartNumber = bui.PartNumber;
				unit.SetupDate = bui.SetupDate;
				unit.InstallTime = bui.InstallTime;
				unit.OperationMinutes = Convert.ToInt32( bui.RunTime.TotalMinutes );

				baseUnits.Add( unit );
			}

			return baseUnits;
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
		/// Clears an instrument's log of base units in which it has been docked.
		/// </summary>
		public override void ClearBaseUnits() 
		{
			Log.Debug( "Clearing log of base units" );
			Driver.clearBaseUnitInfos();
		}
	}
}
