using System;
using System.Collections.Generic;
using System.Data;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.DataAccess
{
    public class GasEndPointDataAccess : DataAccess
    {

        /// <summary>
        /// Returns all of the currently known installed cylinders.
        /// </summary>
        /// <returns>The list of InstalledCylinders will be sorted by Position</returns>
        public List<GasEndPoint> FindAll()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return FindAll( trx );
            }
        }

        /// <summary>
        /// Returns all of the currently known installed cylinders.
        /// </summary>
        /// <param name="trx"></param>
        /// <returns>The list of InstalledCylinders will be sorted by Position</returns>
        public List<GasEndPoint> FindAll( DataAccessTransaction trx )
        {
            List<GasEndPoint> list = new List<GasEndPoint>();

			string sql = "SELECT * FROM GASENDPOINT ORDER BY POSITION";
            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
                    while ( reader.Read() )
                    {
                        GasEndPoint gep = MakeInstalledCylinder( reader, ordinals, trx );
                        list.Add( gep );
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Returns the cylinder currently installed at the specified position.
        /// </summary>
        /// <param name="position"></param>
        /// <returns>null is returned if no cylinder is known to be installed at the specified position.</returns>
        public GasEndPoint FindByPosition( int position )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return FindByPosition( position, trx );
            }
        }


        /// <summary>
        /// Returns the cylinder currently installed at the specified position.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="trx"></param>
        /// <returns>null is returned if no cylinder is known to be installed at the specified position.</returns>
        public GasEndPoint FindByPosition( int position, DataAccessTransaction trx )
        {
            string sql = "SELECT * FROM GASENDPOINT WHERE POSITION = " + position;

            using ( IDbCommand cmd = GetCommand( sql, trx ) )
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    if ( reader.Read() )
                        return MakeInstalledCylinder( reader, new DataAccessOrdinals( reader ), trx );
                }
            }
            return null;
        }


        private GasEndPoint MakeInstalledCylinder( IDataReader reader, DataAccessOrdinals ordinals, DataAccessTransaction trx )
        {
            short position = SqlSafeGetShort( reader, ordinals["POSITION"] );
            string partNumber = SqlSafeGetString( reader, ordinals["PARTNUMBER"] );

            // Try and get the Factory Cylinder information for the part number.
            // Note that there may not be any factory cylinder info available if the
            // part number is for a new cylinder type that's unknown to to iNet.
            FactoryCylinder factoryCylinder = null;
            if ( partNumber != string.Empty )
                factoryCylinder = new FactoryCylinderDataAccess().FindByPartNumber( partNumber, trx );

            Cylinder cylinder;
            if ( factoryCylinder != null )
                cylinder = new Cylinder( factoryCylinder );
            else
            {
                cylinder = new Cylinder();
                cylinder.PartNumber = partNumber;
            }

            string installationTypeString = SqlSafeGetString( reader, ordinals["INSTALLATIONTYPE"] );
            GasEndPoint.Type installationType = (GasEndPoint.Type)Enum.Parse( typeof(GasEndPoint.Type), installationTypeString, true );

            GasEndPoint gep = new GasEndPoint( cylinder, position, installationType );

            gep.Cylinder.FactoryId = SqlSafeGetString( reader, ordinals["FACTORYID"] );
            gep.Cylinder.ExpirationDate = SqlSafeGetDate( reader, ordinals["EXPIRATIONDATE"] );
            gep.Cylinder.RefillDate = SqlSafeGetDate( reader, ordinals["REFILLDATE"] );
            string pressure = SqlSafeGetString( reader, ordinals["PRESSURE"] );
            gep.Cylinder.Pressure = (PressureLevel)Enum.Parse( typeof(PressureLevel), pressure, true );

            return gep;
        }

        /// <summary>
        /// Deletes the InstalledCylinder from its specified Position.
        /// </summary>
		/// <param name="gep"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public bool Delete( GasEndPoint gep, DataAccessTransaction trx )
        {
            string sql = "DELETE FROM GASENDPOINT WHERE POSITION = @POSITION";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@POSITION", gep.Position ) );
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( DataAccessException )
            {
                throw;
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
        }

        /// <summary>
        /// 
        /// </summary>
		/// <param name="gep"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public bool Save( GasEndPoint gep, DataAccessTransaction trx )
        {
            string sql = string.Empty;

            try
            {
                Delete( gep, trx ); // delete any old cylinder at this position.

                sql = "INSERT INTO GASENDPOINT ( POSITION, RECUPDATETIMEUTC, FACTORYID, PARTNUMBER, PRESSURE, REFILLDATE, EXPIRATIONDATE, INSTALLATIONTYPE ) VALUES ( @POSITION, @RECUPDATETIMEUTC, @FACTORYID, @PARTNUMBER, @PRESSURE, @REFILLDATE, @EXPIRATIONDATE, @INSTALLATIONTYPE )";
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@POSITION", gep.Position ) );
                    cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
                    cmd.Parameters.Add( GetDataParameter( "@INSTALLATIONTYPE", gep.InstallationType.ToString() ) );
                    cmd.Parameters.Add( GetDataParameter( "@FACTORYID", gep.Cylinder.FactoryId ) );
                    cmd.Parameters.Add( GetDataParameter( "@PARTNUMBER", gep.Cylinder.PartNumber ) );
                    cmd.Parameters.Add( GetDataParameter( "@PRESSURE", gep.Cylinder.Pressure.ToString() ) );
                    cmd.Parameters.Add( GetDataParameter( "@REFILLDATE", gep.Cylinder.RefillDate ) );
                    cmd.Parameters.Add( GetDataParameter( "@EXPIRATIONDATE", gep.Cylinder.ExpirationDate ) );

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( DataAccessException )
            {
                throw;
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex ); 
            }
        }

        public bool UpdatePressureLevel( GasEndPoint gep, DataAccessTransaction trx )
        {
            string sql = "UPDATE GASENDPOINT SET PRESSURE = @PRESSURE, RECUPDATETIMEUTC = @RECUPDATETIMEUTC WHERE POSITION = @POSITION";

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@POSITION", gep.Position ) );
                    cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
                    cmd.Parameters.Add( GetDataParameter( "@PRESSURE", gep.Cylinder.Pressure.ToString() ) );

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( DataAccessException )
            {
                throw;
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }
        }

        /// <summary>
        /// 
        /// </summary>
		/// <param name="gasEndPoints"></param>
        /// <param name="changesOnly">
        /// If changesOnly is set, then the passed-in list only contains 
        /// changes.  e.g.., if there's been no iGas card insertion/removal on, say,
        /// position 2, then there will be no cylinder in the list at that position.
        /// </param>
        public void SaveInstalledCylinders( List<GasEndPoint> gasEndPoints, DataAccessTransaction trx )
        {
            List<GasEndPoint> persistedListed = FindAll( trx );

            List<GasEndPoint> saveList = new List<GasEndPoint>();

            for ( int position = 1; position <= Configuration.DockingStation.NumGasPorts; position++ )
            {
                GasEndPoint installed = gasEndPoints.Find( g => g.Position == position );
                GasEndPoint persisted = persistedListed.Find( g => g.Position == position );

                // Is no cylinder on the port, but database thinks there is one?
                // Then assume user has uninstalled the cylinder. We need to remove it from the database
                if ( installed == null && persisted != null )  // Not installed?  make sure it's marked as uninstalled in the DB, too.
                {
                    Log.Debug( string.Format( "Port {0} (fid=\"{1}\",pn=\"{2}\",{3}) has been uninstalled.",
                        position, persisted.Cylinder.FactoryId, persisted.Cylinder.PartNumber, persisted.InstallationType ) );

                    persisted.GasChangeType = GasEndPoint.ChangeType.Uninstalled; // mark it for deletion.
                    saveList.Add( persisted );
                }
                // Was this cylinder not known to be installed but is now installed?
                else if ( persisted == null && installed != null )
                {
                    installed.GasChangeType = GasEndPoint.ChangeType.Installed; // mark it for saving.
                    saveList.Add( installed );
                }
                // Was this cylinder already known to be installed?  Then update
                // any data that's changed.
                else if ( installed != null && persisted != null )
                {
                    // If anything has changed, then we need to update the cylinder.
                    if ( installed.Cylinder.FactoryId != persisted.Cylinder.FactoryId
                    ||   installed.Cylinder.ExpirationDate != persisted.Cylinder.ExpirationDate
                    ||   installed.Cylinder.Pressure != persisted.Cylinder.Pressure
                    ||   installed.Cylinder.PartNumber != persisted.Cylinder.PartNumber )
                    {
                        Log.Debug( string.Format( "Port {0} has changed...", position ) );
                        Log.Debug( string.Format( "......fid=\"{0}\", pn=\"{1}\", {2}, {3}, {4}",
                            installed.Cylinder.FactoryId, installed.Cylinder.PartNumber, installed.InstallationType,
                            Log.DateTimeToString(installed.Cylinder.ExpirationDate), installed.Cylinder.Pressure ) );

                        installed.GasChangeType = GasEndPoint.ChangeType.Installed; // mark it for saving.
                        saveList.Add( installed );
                    }
                    else
                    {
                        Log.Debug( string.Format( "Port {0} has not changed. (fid=\"{1}\",pn=\"{2}\",{3})",
                            position, persisted.Cylinder.FactoryId, persisted.Cylinder.PartNumber, persisted.InstallationType ) );
                    }

                }
                else if ( installed == null && persisted == null )
                {
                    Log.Debug( string.Format( "Port {0}: Nothing installed.", position ) );
                }
            }

            SaveGasEndPoints( saveList, trx );

            return;
        }

        public void SaveChangedCylinders( List<GasEndPoint> changedCylinders, DataAccessTransaction trx )
        {
            SaveGasEndPoints( changedCylinders, trx );
        }

        private void SaveGasEndPoints( IEnumerable<GasEndPoint> gasEndPoints, DataAccessTransaction trx )
        {
            foreach ( GasEndPoint gep in gasEndPoints )
            {
                if ( gep.GasChangeType == GasEndPoint.ChangeType.Uninstalled )
                {
                    Log.Debug( string.Format( "Deleting uninstalled {0} cylinder on port {1} ({2},{3})",
                        gep.InstallationType, gep.Position, gep.Cylinder.FactoryId, gep.Cylinder.PartNumber ) );
                    Delete( gep, trx );
                }

                if ( gep.GasChangeType == GasEndPoint.ChangeType.Installed )
                {
                    Log.Debug( string.Format( "Saving new/modified {0} cylinder on port {1} ({2},{3},{4})",
                        gep.InstallationType, gep.Position, gep.Cylinder.FactoryId, gep.Cylinder.PartNumber, gep.Cylinder.Pressure ) );
                    Save( gep, trx );
                }

                if ( gep.GasChangeType == GasEndPoint.ChangeType.PressureChanged )
                {
                    Log.Debug( string.Format( "Saving PressureLevel.{0} for {1} cylinder on port {2} ({3},{4})",
                        gep.Cylinder.Pressure, gep.InstallationType, gep.Position, gep.Cylinder.FactoryId, gep.Cylinder.PartNumber ) );
                    UpdatePressureLevel( gep, trx );
                }
            }
        }


    } // end-class

}  // end-namespace
