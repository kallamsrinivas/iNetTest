using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;

namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// Data access for SensorCalibrationLimits table.
    /// Used to configure the sensor calibration threshold limits. As of now only sensor age is configured, which can be extended in future.
    /// </summary>
    public class SensorCalibrationLimitsDataAccess : DataAccess
    {
        public SensorCalibrationLimitsDataAccess() { }

        #region public methods

        /// <summary>
        /// Finds and returns all Sensor Calibration Limits in the database.
        /// </summary>
        /// <returns></returns>
        public List<SensorCalibrationLimits> FindAll()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return FindAll( trx );
            }
        }

        /// <summary>
        /// Finds and returns all Sensor Calibration Limits in the database.
        /// </summary>
        public List<SensorCalibrationLimits> FindAll( DataAccessTransaction trx )
        {
            List<SensorCalibrationLimits> list = new List<SensorCalibrationLimits>();

            using ( IDbCommand cmd = GetCommand("SELECT * FROM " + TableName, trx ) )
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
                    while ( reader.Read() )
                        list.Add( CreateFromReader( reader, ordinals ) );
                }
            }
            return list;
        }

        /// <summary>
        /// Delete the Sensor Calibration Limits
        /// </summary>
        /// <param name="trx">Data Transcation</param>
        /// <returns></returns>
        public bool Delete( DataAccessTransaction trx )
        {
            string deleteSql = "DELETE FROM " + TableName;
            try
            {
                using (IDbCommand cmd = GetCommand( deleteSql, trx ) )
                {
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( deleteSql, ex );
            }

        }

        /// <summary>
        /// Save the passed in Sensor Calibration Limits.
        /// Delete the old Sensor Calibration Limits before inserting the new list.
        /// </summary>
        /// <param name="sensorCalibrationLimits"></param>
        /// <param name="trx"></param>
        public void Save( List<SensorCalibrationLimits> sensorCalibrationLimits, DataAccessTransaction trx )
        {
            Delete( trx );
            InsertSensorCalibrationLimits( sensorCalibrationLimits, trx );
        }

        #endregion

        #region private methods

        private SensorCalibrationLimits CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            string sensorCode = SqlSafeGetString( reader, ordinals["SENSORCODE"] );
            int age = SqlSafeGetInt( reader, ordinals["AGE"] );

            return new SensorCalibrationLimits( sensorCode, age );
        }

        private void InsertSensorCalibrationLimits( List<SensorCalibrationLimits> sensorCalibrationLimits, DataAccessTransaction trx )
        {
            using (IDbCommand cmd = GetCommand( "INSERT INTO " + TableName + " ( SENSORCODE, AGE ) VALUES ( @SENSORCODE, @AGE )", trx ) )
            {
                foreach ( SensorCalibrationLimits sensorCalibrationLimit in sensorCalibrationLimits )
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.Add( GetDataParameter( "@SENSORCODE", sensorCalibrationLimit.SensorCode ) );
                    cmd.Parameters.Add( GetDataParameter( "@AGE", sensorCalibrationLimit.Age ) );

                    cmd.ExecuteNonQuery();
                }
            }
        }
        #endregion 

    }
}
