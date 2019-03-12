using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common; // DataTableMapping
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.DataAccess
{
    public class FactoryCylinderDataAccess : DataAccess
    {
        public FactoryCylinderDataAccess() { }

        /// <summary>
        /// Finds and returns all FactoryCylinders in the database.
        /// </summary>
        /// <returns></returns>
        public IList<FactoryCylinder> FindAll()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return FindAll( trx );
            }
        }

        /// <summary>
        /// Finds and returns all FactoryCylinders in the database.
        /// </summary>
        public IList<FactoryCylinder> FindAll( DataAccessTransaction trx )
        {
            IList<FactoryCylinder> list = new List<FactoryCylinder>();

            using ( IDbCommand cmd = GetCommand( "SELECT * FROM FACTORYCYLINDER", trx ) )
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    while ( reader.Read() )
                        list.Add( CreateFromReader( reader, ordinals ) );
                }
            }

            // For each cylinder, load its gases

            FactoryCylinderGasDataAccess factoryCylinderGasDataAccess = new FactoryCylinderGasDataAccess();

            foreach ( FactoryCylinder cylinder in list )
                LoadFactoryCylinderGases( cylinder, factoryCylinderGasDataAccess, trx );

            return list;
        }

        /// <summary>
        /// Finds and returns a specific FactoryCylinder by its part number.
        /// </summary>
        /// <param name="partNumber"></param>
        /// <returns></returns>
        public FactoryCylinder FindByPartNumber( string partNumber )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return FindByPartNumber( partNumber, trx );
            }
        }

        /// <summary>
        /// Finds and returns a specific FactoryCylinder by its part number.
        /// </summary>
        /// <param name="partNumber"></param>
        /// <param name="trx"></param>
        /// <returns>null if no match can be found.</returns>
        public FactoryCylinder FindByPartNumber( string partNumber, DataAccessTransaction trx )
        {
            FactoryCylinder cylinder = null;

            using ( IDbCommand cmd = GetCommand( "SELECT * FROM FACTORYCYLINDER WHERE PARTNUMBER = @PARTNUMBER", trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@PARTNUMBER", partNumber ) );

                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    if ( reader.Read() )
                        cylinder = CreateFromReader( reader );
                }
            }

            if ( cylinder != null )
                LoadFactoryCylinderGases( cylinder, new FactoryCylinderGasDataAccess(), trx );

            return cylinder;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="factoryCylinder"></param>
        /// <param name="trx"></param>
        /// <returns>Number of rows deleted</returns>
        public int Delete( FactoryCylinder factoryCylinder, DataAccessTransaction trx )
        {
            using ( IDbCommand cmd = GetCommand( "DELETE FROM FACTORYCYLINDER WHERE PARTNUMBER = @PARTNUMBER", trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@PARTNUMBER", factoryCylinder.PartNumber ) );

                return cmd.ExecuteNonQuery();
            }
        }

        public void Save( FactoryCylinder factoryCylinder, DataAccessTransaction trx )
        {
            try
            {
                // We first always try and insert, under the assumption that most cylinder
                // changes are new cylinders, not modified cylinders.
                if ( Insert( factoryCylinder, trx ) )
                    return;

                // The above Insert call will return false if the cylinder is found to already
                // be in the database.  In that situation, just delete the cylinder in the database,
                // then add it as all new.
                Delete( factoryCylinder, trx );

                Insert( factoryCylinder, trx );
            }
            catch ( DataAccessException )
            {
                throw;
            }
            catch ( Exception e )
            {
                throw new DataAccessException( factoryCylinder.PartNumber, e );
            }
        }

        private bool Insert( FactoryCylinder factoryCylinder, DataAccessTransaction trx )
        {
            using ( IDbCommand cmd = GetCommand( "INSERT INTO FACTORYCYLINDER ( PARTNUMBER, RECUPDATETIMEUTC, MANUFACTURERCODE ) VALUES ( @PARTNUMBER, @RECUPDATETIMEUTC, @MANUFACTURERCODE )", trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@PARTNUMBER", factoryCylinder.PartNumber ) );
                cmd.Parameters.Add( GetDataParameter( "@RECUPDATETIMEUTC", trx.TimestampUtc ) );
                cmd.Parameters.Add( GetDataParameter( "@MANUFACTURERCODE", factoryCylinder.ManufacturerCode ) );

                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch ( SQLiteException e )
                {
                    if ( e.ErrorCode == SQLiteErrorCode.Constraint )
                        return false;  // assume we have a 'duplicate' error.

                    throw; // any other error is unexpected, so just rethrow it
                }

                // insert the child gases
                new FactoryCylinderGasDataAccess().InsertForFactoryCylinder( factoryCylinder, trx );
            }
            return true;
        }

        /// <summary>
        /// Find all the FactoryCylinderGases for the specified FactoryCylinder
        /// </summary>
        /// <param name="factoryCylinder"></param>
        /// <param name="factoryCylinderGasDataAccess"></param>
        /// <param name="trx"></param>
        private void LoadFactoryCylinderGases( FactoryCylinder factoryCylinder,
            FactoryCylinderGasDataAccess factoryCylinderGasDataAccess,
            DataAccessTransaction trx )
        {
            factoryCylinder.GasConcentrations.Clear();
            foreach ( GasConcentration gasConcentration in factoryCylinderGasDataAccess.FindByFactoryCylinder( factoryCylinder, trx ) )
            {
                factoryCylinder.GasConcentrations.Add( gasConcentration );
            }
        }


        private FactoryCylinder CreateFromReader( IDataReader reader )
        {
            return CreateFromReader( reader, new DataAccessOrdinals( reader ) );
        }

        private FactoryCylinder CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            string partNumber = SqlSafeGetString( reader, ordinals[ "PARTNUMBER" ] );

            string manufacturerCode = SqlSafeGetString( reader, ordinals[ "MANUFACTURERCODE" ] );

            return new FactoryCylinder( partNumber, manufacturerCode );
        }
    }
}
