using System;
using System.Collections.Generic;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.DataAccess;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
	public class CylinderPressureResetOperation : CylinderPressureResetAction, IOperation
	{
		#region Fields

		private const string LOG_LABEL = "PRESSURE RESET: ";

		#endregion

		#region Constructors

		public CylinderPressureResetOperation() { }

		public CylinderPressureResetOperation( CylinderPressureResetAction cylPressureResetAction ) : base( cylPressureResetAction ) { }

		#endregion

		#region Methods

		public DockingStationEvent Execute()
		{
			string funcMsg = Name + ".Execute";
			Log.Debug( funcMsg );

			// We copy the PostUpdate and SettingsRefId from the action to the event so they can 
			// be passed on to the followup SettingsRead.  See the EventProcessor.GetFollowupAction method.
			CylinderPressureResetEvent dsEvent = new CylinderPressureResetEvent( this );
			dsEvent.PostUpdate = this.PostUpdate;
			dsEvent.SettingsRefId = this.SettingsRefId;

			List<GasEndPoint> emptyManGasEndPoints = new List<GasEndPoint>();
			List<GasEndPoint> manGasEndPoints 
				= new GasEndPointDataAccess().FindAll().FindAll( m => m.InstallationType == GasEndPoint.Type.Manifold ||
														         m.InstallationType == GasEndPoint.Type.Manual );

			// We want to reset low/empty non-iGas cylinders to full.
			for ( int position = 1; position <= Configuration.DockingStation.NumGasPorts; position++ )
			{
				// We don't want to process (manual) fresh air cylinders on port 1.
				GasEndPoint man = manGasEndPoints.Find( m => m.Position == position && !(m.Position == 1 && m.Cylinder.IsFreshAir) );
				if ( man != null )
				{
					Log.Debug( string.Format( "{0}Position {1} {2} found (\"{3}\", \"{4}\") with {5} pressure.", LOG_LABEL, position,
						man.InstallationType == GasEndPoint.Type.Manifold ? "Manifold" : "Manual Cylinder",
						man.Cylinder.FactoryId, man.Cylinder.PartNumber, man.Cylinder.Pressure ) );

					if ( man.Cylinder.Pressure != PressureLevel.Full )
					{
						man.GasChangeType = GasEndPoint.ChangeType.PressureChanged;
						man.Cylinder.Pressure = PressureLevel.Full;
						emptyManGasEndPoints.Add( man );
					}
				}
			}

			if ( emptyManGasEndPoints.Count > 0 )
			{
				// Save the modified cylinders in the local database.  The followup SettingsRead with
				// ChangedSmartCards set to null will take care of updating the cylinders in memory.
				using ( DataAccessTransaction trx = new DataAccessTransaction() )
				{
					new GasEndPointDataAccess().SaveChangedCylinders( emptyManGasEndPoints, trx );
					trx.Commit();

					Log.Debug( string.Format( "{0}{1} non-iGas cylinders were reset to full.", LOG_LABEL, emptyManGasEndPoints.Count ) );
				}
			}
			else
			{
				Log.Debug( string.Format( "{0}No manifold or manual cylinders were found that needed reset to full.", LOG_LABEL ) );
			}

			return dsEvent;			
		}

		#endregion
	}
}
