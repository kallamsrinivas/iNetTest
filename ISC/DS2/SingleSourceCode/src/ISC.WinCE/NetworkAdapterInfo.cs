using System;
using System.Collections.Generic;
using System.Globalization; // NumberOfStyles
using System.Net.NetworkInformation;


namespace ISC.WinCE
{
/// <summary>
/// NetworkAdapterInfo is a class that can be used to obtain information about
/// the installed (and enabled) network adapters.  For a given NetworkAdapterInfo
/// instance, you may see some of the properties of that adapter (it's IP address,
/// MAC address, etc.).  All properties are read-only; the class does not support
/// the changing of any network settings, nor is it intended for such.
/// </summary>
public class NetworkAdapterInfo : ICloneable
{
    // This name might be used when rebinding an adapter to all
    // of its protocols (when changing its IP address, for example).
    private const String DD_NDIS_DEVICE_NAME = "NDS0:";

    private const int MAC_ADDRESS_SIZE = 6;

    private static object _lock = new object();

    private string _adapterName;
    private string _macAddress;
    private string _subnetMask;
    private string _gateway;
    private string _ipAddress;
    private bool _dhcpEnabled;
    private string _dnsPrimary;
    private string _dnsSecondary;

    public NetworkAdapterInfo()
    {
    }

    private NetworkAdapterInfo( NetworkInterface nic )
    {
        //AdapterName = nic.Name;
        //MacAddress = nic.GetPhysicalAddress().ToString();
        //IpAddress = nic.CurrentIpAddress.ToString();
        //SubnetMask = nic.CurrentSubnetMask.ToString();

        //IPInterfaceProperties ipProperties = nic.GetIPProperties();

        //if ( ipProperties == null ) // will it ever be null? not sure.
        //    return;

        //if ( ipProperties.GatewayAddresses.Count > 0 )
        //    Gateway = ipProperties.GatewayAddresses[0].Address.ToString();

        //if ( ipProperties.GetIPv4Properties() != null ) // will it ever be null? not sure.
        //    DhcpEnabled = ipProperties.GetIPv4Properties().IsDhcpEnabled;

        //if ( ipProperties.DnsAddresses.Count > 0 )
        //    DnsPrimary = ipProperties.DnsAddresses[0].ToString();

        //if ( ipProperties.DnsAddresses.Count > 1 )
        //    DnsSecondary = ipProperties.DnsAddresses[1].ToString();
    }

    public object Clone()
    {
        return this.MemberwiseClone();
    }

    /// <summary>
    /// Returns whether or not the wired network adapter appears to be connected to a network device or not.
    /// </summary>
    /// <returns></returns>
    public bool IsNetworked()
    {
        return NetworkAdapterInfo.IsNetworked( this );
    }

    /// <summary>
    /// Returns whether or not the specified network adapter appears to be connected to a network device or not.
    /// </summary>
    /// <param name="networkAdapterInfo"></param>
    /// <returns></returns>
    public static bool IsNetworked( NetworkAdapterInfo networkAdapterInfo )
    {
        return networkAdapterInfo.IpAddress != string.Empty
            && networkAdapterInfo.IpAddress != "0.0.0.0"  // no IP at all ?
            && networkAdapterInfo.IpAddress.StartsWith( "127." ) == false;  // loopback ?
        //return IPAddress.IsLoopback( ip ) == false;
    }


    static public List<NetworkAdapterInfo> GetNetworkAdapters()
    {
            return null;
        // Unfortunately, the NetworkInterface class does not seem to be thread safe and / or 
        // does not work correctly when the application has multiple NetworkInterface instances
        // representing the same adapter.
        //
        // e.g., the following can fail...
        //
        // INetworkInterface nic1 = NetworkInterface.GetAllNetworkInterfaces().Find( n => n.AdapterName == "MyAdapterName" );
        // INetworkInterface nic2 = NetworkInterface.GetAllNetworkInterfaces().Find( n => n.AdapterName == "MyAdapterName" );
        // string ip1 = nic1.CurrentIpAddress;
        // string ip2 = nic2.CurrentIpAddress; <-- This can sometimes throw the following exception
        //
        // System.FormatException: An invalid IP address was specified. ---> System.Net.Sockets.SocketException: An invalid argument was supplied
        // at System.Net.IPAddress.Parse(String ipString)
        // at OpenNETCF.Net.NetworkInformation.IP_ADAPTER_INFO.get_CurrentIpAddress()
        // at OpenNETCF.Net.NetworkInformation.NetworkInterface.get_CurrentIpAddress()
        // at ISC.WinCE.NetworkAdapterInfo.get_IpAddress()
        // at ISC.iNet.DS.Services.Master.MonitorNetworkConnection()
        // at ISC.iNet.DS.Services.Master.Run(String[] args)
        // at ISC.iNet.DS.Services.Master.Main(String[] args)
        // 
        // To avoid that, we don't allow multiple calls to GetNetworkAdapters at the same time.
        // Otherwise, two calls running in parallel could end up with the above situation where 
        // both threads have references to NetworkInterface of the same adapter.
        lock ( _lock )
        {
   //         INetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();

   //         if ( nics == null ) // does it ever return null? not sure.
   //             return new List<NetworkAdapterInfo>();

   //         List<NetworkAdapterInfo> networkAdapterList = new List<NetworkAdapterInfo>( nics.Length );

			//foreach ( NetworkInterface nic in nics )
			//	networkAdapterList.Add( new NetworkAdapterInfo( nic ) );

   //         return networkAdapterList;
        }
    }

    /// <summary>
    /// The name of this network adapter
    /// </summary>
    public string AdapterName
    {
        get { return _adapterName == null ? string.Empty : _adapterName; }
        private set { _adapterName = value; }
    }

    /// <summary>
    /// The MAC Address of this network adapter
    /// </summary>
    public string MacAddress
    {
        get { return _macAddress == null ? string.Empty : _macAddress; }
        private set { _macAddress = value; }
    }

    /// <summary>
    /// The IP Address associated with this adapter
    /// </summary>
    public string IpAddress
    {
        get { return _ipAddress == null ? string.Empty : _ipAddress; }
        private set { _ipAddress = value; }
    }

    /// <summary>
    /// Subnet mask
    /// </summary>
    public string SubnetMask
    {
        get { return _subnetMask == null ? string.Empty : _subnetMask; }
        private set { _subnetMask = value; }
    }

    /// <summary>
    /// The IP Address of the default gateway for this adapter.
    /// </summary>
    public string Gateway
    {
        get { return _gateway == null ? string.Empty : _gateway; }
        private set { _gateway = value; }
    }

    /// <summary>
    /// Whether DHCP is enabled or not for this adapter.
    /// </summary>
    public bool DhcpEnabled
    {
        get { return _dhcpEnabled; }
        private set { _dhcpEnabled = value; }
    }

    /// <summary>
    /// The address of the primary DNS.
    /// </summary>
    public string DnsPrimary
    {
        get { return _dnsPrimary == null ? string.Empty : _dnsPrimary; }
        private set { _dnsPrimary = value; }
    }

    /// <summary>
    /// The address of the secondary DNS.
    /// </summary>
    public string DnsSecondary
    {
        get { return _dnsSecondary == null ? string.Empty : _dnsSecondary; }
        private set { _dnsSecondary = value; }
    }

    /// <summary>
    /// Returns an empty string if specified IP is "0.0.0.0".  Otherwise, it just returns the specified IP.
    /// </summary>
    /// <param name="ipAddress"></param>
    /// <returns></returns>
    public static string IpAddressToString( string ipAddress )
    {
        if ( ipAddress == null )
            return string.Empty;
        if ( ipAddress == "0.0.0.0" )
            return string.Empty;

        return ipAddress;
    }

    /// <summary>
    /// Turn a byte array representation of a MAC Address into a properly
    /// formatted string representation of format: "00:00:00:00:00:00".
    /// </summary>
    /// <param name="address">The byte array to format.</param>
    /// <returns>The formatted string.</returns>
    public static string MacAddressToString( byte[] macAddress )
    {
        if ( macAddress == null )
            return string.Empty;

        string address = "";
        int length = Math.Min( 6 , macAddress.Length ); // The mac address cannot be longer than 6 bytes.
        for ( int n = 0 ; n < length; n++ )
        {
            if ( n != 0 )
                address += ":";

            address += macAddress[ n ].ToString( "X" ).PadLeft( 2 , '0' );
        }
        return address.Trim();
    }

    /// <summary>
    /// Create a binary (byte array) MAC address from a MAC address string of the following
    /// formats:
    /// "00:00:00:00:00:00" or "00 00 00 00 00 00" or "00-00-00-00-00-00"
    /// </summary>
    /// <param name="address">A properly formatted address.</param>
    /// <returns>The parsed byte array.</returns>
    public static byte[] ParseMacAddress( string address )
    {
        if ( address == string.Empty )
            return null;

        // MAC Addresses are always 6 bytes in length.
        byte[]macAddress = new byte[ MAC_ADDRESS_SIZE ];
        string[]parts = address.Split( ":- ".ToCharArray() );

        // Extract each portion.
        for ( int n = 0 ; n < Math.Min( MAC_ADDRESS_SIZE , parts.Length ) ; n++ )
            macAddress[ n ] = byte.Parse( parts[ n ] , NumberStyles.HexNumber );

        return macAddress;
    }

} // end-class NetworkAdapterInfo




} // end-namespace
