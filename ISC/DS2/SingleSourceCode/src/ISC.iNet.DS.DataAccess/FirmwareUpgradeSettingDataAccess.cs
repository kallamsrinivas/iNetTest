using System.Data;
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;
using System.Collections.Generic;
using System;

namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// Data access for FirmwareUpgradeSetting table.
    /// This contains the firmware upgrade details (equipment code, version, checksum, and the filename of the firmware cached in the DSX)
    /// </summary>
    public class FirmwareUpgradeSettingDataAccess : DataAccess
    {
        public FirmwareUpgradeSettingDataAccess() { }

        #region public methods

        /// <summary>
        /// Finds and returns all Firmware Upgrade Settings in the database.
        /// </summary>
        /// <returns></returns>
        public List<FirmwareUpgradeSetting> FindAll()
        {
            using (DataAccessTransaction trx = new DataAccessTransaction(true))
            {
                return FindAll(trx);
            }
        }

        /// <summary>
        /// Finds and returns all Firmware Upgrade Settings in the database.
        /// </summary>
        public List<FirmwareUpgradeSetting> FindAll(DataAccessTransaction trx)
        {
            List<FirmwareUpgradeSetting> list = new List<FirmwareUpgradeSetting>();

            using (IDbCommand cmd = GetCommand("SELECT * FROM " + TableName, trx))
            {
                using (IDataReader reader = cmd.ExecuteReader())
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals(reader);
                    while (reader.Read())
                        list.Add(CreateFromReader(reader, ordinals));
                }
            }
            return list;
        }

        /// <summary>
        /// Find the Firmware Upgrade Settings based on passed equipmentCode and version
        /// </summary>
        /// <param name="equipmentCode"></param>
        /// <param name="equipmentSubTypeCode"></param>
        /// <param name="version"></param>
        /// <returns>FirmwareUpgradeSetting</returns>
        public FirmwareUpgradeSetting Find(string equipmentCode, string equipmentSubTypeCode, string version)
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                string equipmentSubTypeClause = string.IsNullOrEmpty(equipmentSubTypeCode) ? "EQUIPMENTSUBTYPECODE IS NULL" : "EQUIPMENTSUBTYPECODE = @EQUIPMENTSUBTYPECODE";
                using ( IDbCommand cmd = GetCommand( string.Format("SELECT * FROM " + TableName + " WHERE EQUIPMENTCODE = @EQUIPMENTCODE AND VERSION = @VERSION AND {0}", equipmentSubTypeClause), trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTCODE", equipmentCode ) );
                    cmd.Parameters.Add( GetDataParameter( "@VERSION", version ) );
                    if ( !string.IsNullOrEmpty( equipmentSubTypeCode ) )
                    {
                        cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTSUBTYPECODE", equipmentSubTypeCode ) );
                    }

                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        if ( !reader.Read() )
                        {
                            Log.Debug( "No Firmware Upgrade Settings record found" );
                            return null;
                        }

                        FirmwareUpgradeSetting firmewareUpgradeSetting = new FirmwareUpgradeSetting();
                        firmewareUpgradeSetting.EquipmentCode = SqlSafeGetString( reader, ordinals["EQUIPMENTCODE"] );
                        firmewareUpgradeSetting.EquipmentSubTypeCode = SqlSafeGetString( reader, ordinals["EQUIPMENTSUBTYPECODE"] );
                        firmewareUpgradeSetting.EquipmentFullCode = SqlSafeGetString( reader, ordinals["EQUIPMENTFULLCODE"] );
                        firmewareUpgradeSetting.Version = SqlSafeGetString( reader, ordinals["VERSION"] );
                        firmewareUpgradeSetting.CheckSum = SqlSafeGetBLOB( reader, ordinals["CHECKSUM"] );
                        firmewareUpgradeSetting.FileName = SqlSafeGetString( reader, ordinals["FILENAME"] );

                        return firmewareUpgradeSetting;
                    }
                }
            }
        }

        /// <summary>
        /// Save the passed in Firmware Upgrade Setting.
        /// </summary>
        /// <param name="firmwareUpgradeSetting"></param>
        public void Save( FirmwareUpgradeSetting firmwareUpgradeSetting )
        {
            using (DataAccessTransaction trx = new DataAccessTransaction())
            {
                Save( firmwareUpgradeSetting, trx );
                trx.Commit();
            }
        }

        /// <summary>
        /// Save the passed in Firmware Upgrade Settings, delete if any available already.
        /// </summary>
        /// <param name="firmwareUpgradeSetting"></param>
        /// <param name="trx"></param>
        public void Save( FirmwareUpgradeSetting firmwareUpgradeSetting, DataAccessTransaction trx )
        {
          Delete( firmwareUpgradeSetting.EquipmentCode, firmwareUpgradeSetting.EquipmentSubTypeCode, firmwareUpgradeSetting.Version, trx );
          InsertFirmwareUpgradeSetting( firmwareUpgradeSetting, trx );
        }

        /// <summary>
        /// Delete the Firmware Upgrade Setting
        /// </summary>
        /// <param name="equipmentCode"></param>
        /// <param name="equipmentSubTypeCode"></param>
        /// <param name="trx">Data Transcation</param>
        /// <returns></returns>
        public bool Delete( string equipmentCode, string equipmentSubTypeCode, string version, DataAccessTransaction trx )
        {
            string equipmentSubTypeClause = string.IsNullOrEmpty( equipmentSubTypeCode ) ? "EQUIPMENTSUBTYPECODE IS NULL" : "EQUIPMENTSUBTYPECODE = @EQUIPMENTSUBTYPECODE";
            string deleteSql = string.Format( "DELETE FROM {0} WHERE EQUIPMENTCODE = @EQUIPMENTCODE AND VERSION = @VERSION AND {1}", TableName, equipmentSubTypeClause );
            try
            {
                using ( IDbCommand cmd = GetCommand( deleteSql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTCODE", equipmentCode ) );
                    cmd.Parameters.Add( GetDataParameter( "@VERSION", version ) );
                    if ( !string.IsNullOrEmpty( equipmentSubTypeCode ) )
                    {
                        cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTSUBTYPECODE", equipmentSubTypeCode ) );
                    }
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( deleteSql, ex );
            }
        }

        #endregion

        #region private methods

        private FirmwareUpgradeSetting CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            string equipmentCode = SqlSafeGetString( reader, ordinals["EQUIPMENTCODE"] );
            string equipmentSubTypeCode = SqlSafeGetString( reader, ordinals["EQUIPMENTSUBTYPECODE"] );
            string equipmentFullCode = SqlSafeGetString( reader, ordinals["EQUIPMENTFULLCODE"] );
            string version = SqlSafeGetString( reader, ordinals["VERSION"] );
            byte[] checksum = SqlSafeGetBLOB( reader, ordinals["CHECKSUM"] );
            string fileName = SqlSafeGetString( reader, ordinals["FILENAME"] );

            return new FirmwareUpgradeSetting ( equipmentCode, equipmentSubTypeCode, equipmentFullCode, version, checksum, fileName );
        }

        private void InsertFirmwareUpgradeSetting( FirmwareUpgradeSetting firmwareUpgradeSetting, DataAccessTransaction trx )
        {
            using ( IDbCommand cmd = GetCommand( "INSERT INTO " + TableName + " ( EQUIPMENTCODE, EQUIPMENTSUBTYPECODE, EQUIPMENTFULLCODE, VERSION, CHECKSUM, FILENAME ) VALUES ( @EQUIPMENTCODE, @EQUIPMENTSUBTYPECODE, @EQUIPMENTFULLCODE, @VERSION, @CHECKSUM, @FILENAME )", trx ) )
            {
                cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTCODE", firmwareUpgradeSetting.EquipmentCode ) );
                cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTSUBTYPECODE", firmwareUpgradeSetting.EquipmentSubTypeCode ) );
                cmd.Parameters.Add( GetDataParameter( "@EQUIPMENTFULLCODE", firmwareUpgradeSetting.EquipmentFullCode ) );
                cmd.Parameters.Add( GetDataParameter( "@VERSION", firmwareUpgradeSetting.Version ) );
                cmd.Parameters.Add( GetDataParameter( "@CHECKSUM", firmwareUpgradeSetting.CheckSum ) );
                cmd.Parameters.Add( GetDataParameter( "@FILENAME", firmwareUpgradeSetting.FileName ) );

                cmd.ExecuteNonQuery();
            }
        }

        #endregion 

    }
}
