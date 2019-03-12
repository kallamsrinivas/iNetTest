using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// This class is deliberately internal. All access is through the FactoryCylinderDataAccess
    /// </summary>
    internal class FactoryCylinderGasDataAccess : DataAccess
    {
        internal FactoryCylinderGasDataAccess() { }

        /// <summary>
        /// Return the GasConcentrations of the cylinder's part number.
        /// </summary>
        /// <param name="factoryCylinder"></param>
        /// <returns></returns>
        internal IList<GasConcentration> FindByFactoryCylinder( FactoryCylinder factoryCylinder )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction(true) )
            {
                return FindByFactoryCylinder( factoryCylinder, trx );
            }
        }

        /// <summary>
        /// Return the GasConcentrations of the cylinder's part number.
        /// </summary>
        /// <param name="factoryCylinder"></param>
        /// <param name="trx"></param>
        /// <returns>Returned list is empty if part number can't be found.</returns>
        internal IList<GasConcentration> FindByFactoryCylinder( FactoryCylinder factoryCylinder, DataAccessTransaction trx )
        {
            List<GasConcentration> list = new List<GasConcentration>();

            using ( IDbCommand cmd = GetCommand( "SELECT * FROM FACTORYCYLINDERGAS WHERE PARTNUMBER = @PARTNUMBER", trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@PARTNUMBER", factoryCylinder.PartNumber ) );

                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    while ( reader.Read() )
                    {
                        string gasCode = SqlSafeGetString( reader, ordinals["GASCODE"] );

                        double concentration = SqlSafeGetDouble( reader, ordinals["CONCENTRATION"], 0.0f );

                        GasConcentration gasConcentration = new GasConcentration( gasCode, concentration );

                        list.Add( gasConcentration );
                    }
                }
            }
            return list;
        }

        internal void InsertForFactoryCylinder( FactoryCylinder factoryCylinder, DataAccessTransaction trx )
        {
            using ( IDbCommand cmd = GetCommand( "INSERT INTO FACTORYCYLINDERGAS ( PARTNUMBER, GASCODE, CONCENTRATION ) VALUES ( @PARTNUMBER, @GASCODE, @CONCENTRATION )", trx ) )
            {
                foreach ( GasConcentration gasConcentration in factoryCylinder.GasConcentrations )
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.Add( GetDataParameter( "@PARTNUMBER", factoryCylinder.PartNumber ) );
                    cmd.Parameters.Add( GetDataParameter( "@GASCODE", gasConcentration.Type.Code ) );
                    cmd.Parameters.Add( GetDataParameter( "@CONCENTRATION", gasConcentration.Concentration) );

                    int inserted = cmd.ExecuteNonQuery();
                }
            }

        }
    }
}
