using System;
using System.Data;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;
using ISC.WinCE;

namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// Dataaccess for REPLACEDNETWORKSETTINGS table
    /// </summary>
    public class ReplacedNetworkSettingsDataAccess : DataAccess
    {
        #region private members

        /// <summary>
        /// List of columns in the table
        /// </summary>
        static private string[] _fields = new string[]
        {
            "DHCPENABLED",
            "IPADDRESS",
            "SUBNETMASK",
            "GATEWAY",
            "DNSPRIMARY",
            "DNSSECONDARY"
        };

        private static readonly string _insertSql;

        #endregion

        #region Constructor

        /// <summary>
        /// Static constructor which forms insert query for the table
        /// </summary>
        static ReplacedNetworkSettingsDataAccess()
        {
            _insertSql = string.Format( "INSERT INTO REPLACEDNETWORKSETTINGS ( {0} ) VALUES ( {1} )", CreateFieldList( string.Empty ), CreateFieldList( "@" ) );
        }

        #endregion

        #region private methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns>
        /// "FIELD1, "FIELD2", FIELD3", etc.
        /// </returns>
        private static string CreateFieldList( string prefix )
        {
            StringBuilder sb = new StringBuilder();

            for ( int i = 0; i < _fields.Length; i++ )
            {
                if ( sb.Length > 0 ) sb.Append( ", " );

                if ( prefix != string.Empty ) sb.Append( prefix );

                sb.Append( _fields[i] );
            }

            return sb.ToString();
        }
        /// <summary>
        /// Inserts Replaced docking station network information into the table
        /// </summary>
        /// <param name="ns">Network Settings</param>
        /// <param name="trx">Transation</param>
        /// <returns></returns>
        private bool Insert( DockingStation.NetworkInfo ns, DataAccessTransaction trx )
        {
            try
            {
                using ( IDbCommand cmd = GetCommand( _insertSql, trx ) )
                {
                    cmd.Parameters.Add( GetDataParameter( "@DHCPENABLED", ns.DhcpEnabled ? 1 : 0 ) );
                    cmd.Parameters.Add( GetDataParameter( "@IPADDRESS", ns.IpAddress ) );
                    cmd.Parameters.Add( GetDataParameter( "@SUBNETMASK", ns.SubnetMask ) );
                    cmd.Parameters.Add( GetDataParameter( "@GATEWAY", ns.Gateway ) );
                    cmd.Parameters.Add( GetDataParameter( "@DNSPRIMARY", ns.DnsPrimary ) );
                    cmd.Parameters.Add( GetDataParameter( "@DNSSECONDARY", ns.DnsSecondary ) );

                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( _insertSql, ex );
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the replaced docking station network settings.
        /// </summary>
        /// <param name="trx">The transaction</param>
        /// <returns></returns>
        public DockingStation.NetworkInfo Find( DataAccessTransaction trx )
        {
            string sql = "SELECT * FROM " + TableName;

            try
            {
                using ( IDbCommand cmd = GetCommand( sql, trx ) )
                {
                    using ( IDataReader reader = cmd.ExecuteReader() )
                    {
                        if ( !reader.Read() )
                        {
                            Log.Debug( "No REPLACED NETWORK SETTINGS record found" );
                            return null;
                        }

                        DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                        DockingStation.NetworkInfo ns = new DockingStation.NetworkInfo();

                        ns.DhcpEnabled = SqlSafeGetShort( reader, ordinals["DHCPENABLED"] ) == 1 ? true : false;
                        ns.IpAddress = SqlSafeGetString( reader, ordinals["IPADDRESS"] );
                        ns.SubnetMask = SqlSafeGetString( reader, ordinals["SUBNETMASK"] );
                        ns.Gateway = SqlSafeGetString( reader, ordinals["GATEWAY"] );
                        ns.DnsPrimary = SqlSafeGetString( reader, ordinals["DNSPRIMARY"] );
                        ns.DnsSecondary = SqlSafeGetString( reader, ordinals["DNSSECONDARY"] );

                        return ns;
                    }
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( sql, ex );
            }

        }

        /// <summary>
        /// Save the replaced docking station network settings.
        /// Before insert's the new network settings old settings will be delete
        /// </summary>
        /// <param name="ns"></param>
        /// <param name="trx"></param>
        /// <returns></returns>
        public bool Save( DockingStation.NetworkInfo ns, DataAccessTransaction trx )
        {
            Delete( trx ); //delete the network settings
            return Insert( ns, trx ); //inserts the new network settings
        }

        /// <summary>
        /// Delete the replaced docking station network settings
        /// </summary>
        /// <returns></returns>
        public bool Delete()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                Delete( trx );
                trx.Commit();
                return true;
            }
        }

        /// <summary>
        /// Delete the replaced docking station network settings.
        /// </summary>
        /// <param name="trx">The Transcation</param>
        /// <returns></returns>
        public bool Delete( DataAccessTransaction trx )
        {
            string deleteSql = "DELETE FROM " + TableName;
            try
            {
                using ( IDbCommand cmd = GetCommand( deleteSql, trx ) )
                {
                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch ( Exception ex )
            {
                throw new DataAccessException( deleteSql, ex );
            }

        }

        #endregion
    }
}
