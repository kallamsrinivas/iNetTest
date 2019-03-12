using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ISC.WinCE;
using System.Net;

namespace ISC.iNet.DS
{
    public sealed partial class Configuration
    {
        /// <summary>
        /// Allows access to the "boot variables" (a.k.a. "Boot Vars") that contain
        /// the network settings that are stored in special area of flash memory in order
        /// to be retained during firmware upgrade.
        /// </summary>
        [StructLayout( LayoutKind.Sequential )]
        private struct BootVars
        {
            private uint _ipAddress;
            private uint _subnetMask;
            private uint _dhcpEnabled;
            private uint _defaultGateway;
            private uint _dnsServer1;
            private uint _dnsServer2;
            private uint _winsAddr1;
            private uint _winsAddr2;
            [MarshalAs( UnmanagedType.ByValArray, SizeConst = 6 ) ]
            private byte[] _macAddress;

            /// <summary>
            /// The device's static IP address setting.
            /// This setting is ignored by the device if DHCP is enabled.
            /// </summary>
            public string IpAddress
            {
                get { return ConvertAddress2String( _ipAddress ); }
                set { _ipAddress = ConvertString2Address( value ); }
            }

            /// <summary>
            /// The device's subnet mask setting.
            /// This setting is ignored by the device if DHCP is enabled.
            /// </summary>
            public string SubnetMask
            {
                get { return ConvertAddress2String( _subnetMask ); }
                set { _subnetMask = ConvertString2Address( value ); }
            }

            /// <summary>
            /// Specifies whether DHCP is enabled or disabled on the device.
            /// If enabled, then the device ignores the other IpAddress,
            /// SubnetMask, and Gateway settings.
            /// </summary>
            public bool DhcpEnabled
            {
                get { return _dhcpEnabled == 1; }
                set { _dhcpEnabled = ( value == true ) ? (uint)1 : (uint)0; }
            }

            /// <summary>
            /// The device's default gateway setting.
            /// This setting is ignored by the device if DHCP is enabled.
            /// </summary>
            public string Gateway
            {
                get { return ConvertAddress2String( _defaultGateway ); }
                set { _defaultGateway = ConvertString2Address( value ); }
            }

            /// <summary>
            /// The device's Primary DNS Server setting.
            /// </summary>
            public string DnsPrimary
            {
                get { return ConvertAddress2String( _dnsServer1 ); }
                set { _dnsServer1 = ConvertString2Address( value ); }
            }

            /// <summary>
            /// The device's Secondary DNS Server setting.
            /// </summary>
            public string DnsSecondary
            {
                get { return ConvertAddress2String( _dnsServer2 ); }
                set { _dnsServer2 = ConvertString2Address( value ); }
            }

            public string WinsAddress1
            {
                get { return ConvertAddress2String( _winsAddr1 ); }
                set { _winsAddr1 = ConvertString2Address( value ); }
            }

            public string WinsAddress2
            {
                get { return ConvertAddress2String( _winsAddr2 ); }
                set { _winsAddr2 = ConvertString2Address( value ); }

            }

            /// <summary>
            /// The device's configured MAC address. Read-only.
            /// Format is "00:00:00:00:00:00".
            /// </summary>
            public string MacAddress
            {
                get
                {
                    return NetworkAdapterInfo.MacAddressToString( _macAddress );
                }
                // We dont' allow modification of the MAC address
                //set
                //{
                //    _macAddress = NetworkAdapterInfo.ParseMacAddress( value );
                //}
            }

            [DllImport( "sdk.dll", SetLastError = true )]
            private static unsafe extern bool bootvars_read( ref BootVars bootVars );

            [DllImport( "sdk.dll", SetLastError = true )]
            private static unsafe extern bool bootvars_write( ref BootVars bootVars );

            [DllImport( "sdk.dll" )]
            private static unsafe extern bool bootvars_deinit();

            private string ConvertAddress2String( uint address )
            {
                try
                {
                    if ( address == 0 )
                        return string.Empty;

                    return new IPAddress( (long)address ).ToString();
                }
                catch ( Exception ex )
                {
                    throw new ConfigurationException( "Invalid address", ex );
                }
            }

            private uint ConvertString2Address( string address )
            {
                if (address.Length == 0)
                    return 0;

                try
                {
                    IPAddress ipAddress = IPAddress.Parse( address );
                    byte[] addressBytes = ipAddress.GetAddressBytes();
                    if ( addressBytes == null || addressBytes.Length != 4 )
                        throw new ConfigurationException( "Invalid address length" );
                    return ToUint32( ipAddress.GetAddressBytes(), 0 );
                }
                catch ( ConfigurationException )
                {
                    throw;
                }
                catch ( Exception ex )
                {
                    throw new ConfigurationException( "Invalid address", ex );
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="bytes"></param>
            /// <param name="offset"></param>
            /// <returns></returns>
            private uint ToUint32( byte[] bytes, int offset )
            {
                // bytes from instrument are in big-endian order.  Since intel machines are
                // in little-endian order, we can't simply use BitConverter and instead must
                // swapp the bytes to make are ushort.

                // Probably will ALWAYS be little endian since Windows is Intel-based.
                // But if not, we can just use framework's BitConverter.
                if ( !BitConverter.IsLittleEndian )
                    return BitConverter.ToUInt32( bytes, offset );

                uint val = 0;

                for ( int i = 3; i >= 0; i-- )
                {
                    val <<= 8;
                    val |= (uint)bytes[offset + i];  // high byte
                }

                return val;
            }

            /// <summary>
            /// Return the system's current "boot vars"
            /// </summary>
            /// <returns></returns>
            static internal BootVars Load()
            {
                BootVars bootVars = new BootVars();

                bool success = false;

                try
                {
                    unsafe
                    {
                        success = bootvars_read( ref bootVars );  // Will set LastError on failure.
                    }

                    if ( !success )
                    {
                        int lastError = WinCeApi.GetLastError();
                        throw new ConfigurationException( string.Format( "Error {0} reading BootVars", lastError ) );
                    }
                }
                catch ( ConfigurationException )
                {
                    throw;
                }
                catch ( Exception ex )
                {
                    throw new ConfigurationException( "Error reading BootVars", ex );
                }
                finally
                {
                    bootvars_deinit();
                }
                return bootVars;
            }

            /// <summary>
            /// Store the passed in "boot vars" as the system's current "boot vars".
            /// </summary>
            /// <param name="bootVars"></param>
            internal static void Save( BootVars bootVars )
            {
                bool success = false;

                try
                {
                    unsafe
                    {
                        success = bootvars_write( ref bootVars ); // Will set LastError on failure.
                    }

                    if ( !success )
                    {
                        int lastError = WinCeApi.GetLastError();
                        throw new ConfigurationException( string.Format( "Error {0} writing BootVars", lastError ) );
                    }
                }
                catch ( ConfigurationException )
                {
                    throw;
                }
                catch ( Exception ex )
                {
                    throw new ConfigurationException( "Error writing BootVars", ex );
                }
                finally
                {
                    bootvars_deinit();
                }
            }

        } // end-struct BootVars

    } // end-class Configuration

} // end-namespace
