using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Runtime.InteropServices;


namespace ISC.WinCE
{
    /// <summary>
    /// Provides information on Windows CE's currently running "services".
    /// </summary>
    /// <remarks>
    /// Uses EnumServices API call.  Based on free code poasted here at...
    /// http://blogs.msdn.com/stevenpr/archive/2007/11/20/calling-the-enumservices-win32-api-from-your-net-compact-framework-application.aspx
    /// </remarks>
    public class ServiceInfo
    {
        public const uint SERVICE_STATE_OFF = 0; // The service is turned off.
        public const uint SERVICE_STATE_ON = 1; // The service is turned on.
        public const uint SERVICE_STATE_STARTING_UP = 2; // The service is in the process of starting up.
        public const uint SERVICE_STATE_SHUTTING_DOWN = 3; // The service is in the process of shutting down.
        public const uint SERVICE_STATE_UNLOADING = 4; // The service is in the process of unloading.
        public const uint SERVICE_STATE_UNINITIALIZED = 5; // The service is not uninitialized.
        public const uint SERVICE_STATE_UNKNOWN = 0xffffffff; // The state of the service is unknown.

        private string _prefixName;
        private string _dllName;
        private IntPtr _hServiceHandle;
        private uint _serviceState;

        private ServiceInfo( string prefixName, string dllName, IntPtr hServiceHandle, uint serviceState )
        {
            _prefixName = prefixName;
            _dllName = dllName;
            _hServiceHandle = hServiceHandle;
            _serviceState = serviceState;
        }

        /// <summary>
        /// The prefix of the device in the form XXXN:, where XXX is the prefix specified on either
        /// RegisterService or the prefix registry value read during ActivateService, and N is the
        /// index number. For example, the prefix HTP0: specifies the Web server. This is the
        /// string used to open a service using either CreateFile or to get the service handle
        /// using GetServiceHandle.
        /// </summary>
        public string PrefixName
        {
            get { return _prefixName; }
        }

        /// <summary>
        /// The name of the DLL that contains the service.
        /// </summary>
        public string DllName
        {
            get { return _dllName; }
        }

        /// <summary>
        /// Service handle
        /// </summary>
        public IntPtr ServiceHandle
        {
            get { return _hServiceHandle; }
        }

        /// <summary>
        /// Specifies the current state of the service.
        /// See this class's public constants for a list of available states.
        /// </summary>
        public uint ServiceState
        {
            get { return _serviceState; }
        }

        /// <summary>
        /// Managed definition of the native ServiceEnumInfo structure.
        /// </summary>
        /// <remarks>
        /// Here's the corresponding native definition:
        /// <code>
        /// typedef struct_ServiceEnumInfo {
        ///    WCHAR szPrefix[6];
        ///    WCHAR szDllName;
        ///    HANDLE hServiceHandle;
        ///    DWORD dwServiceState;
        /// } ServiceEnumInfo;
        /// </code>
        /// <para>
        /// EnumServices returns a buffer containing a number of structures of type ServiceEnumInfo
        /// that describe basic information about the services on a device.
        /// </para>
        /// <para>
        /// Each ServiceEnumInfo structure contains an embedded character array that represents
        /// the service's prefix and a pointer to a string that represents the name of the dll that
        /// implements the service.  The dll names corresponding to the structures are laid out in
        /// memory just after the structures themselves.
        /// </para>
        /// </remarks>
        internal struct ServiceEnumInfo
        {
            [MarshalAs( UnmanagedType.ByValTStr, SizeConst = 6 )]
            internal String prefixName;
            internal IntPtr pDllName; // this value is a pointer to the dll name - not the dll name itself.
            internal IntPtr hServiceHandle;
            internal uint dwServiceState;

            // These properties only exist to prevent the occurrence of
            // "Field 'blah blah blah' is never used" compiler warnings that occur with structs.

            internal String PrefixName
            {
                get { return prefixName; }
                set { prefixName = value; }
            }

            internal IntPtr DllName
            {
                get { return pDllName; }
                set { pDllName = value; }
            }

            internal IntPtr ServiceHandle
            {
                get { return hServiceHandle; }
                set { hServiceHandle = value; }
            }

            internal uint ServiceState
            {
                get { return dwServiceState; }
                set { dwServiceState = value; }
            }
        }

        /// <summary>
        /// This function returns information about all running services on the device.
        /// </summary>
        /// <param name="pBuffer"></param>
        /// <param name="numEntries"></param>
        /// <param name="cbBuf"></param>
        /// <returns></returns>
        [DllImport( "coredll.dll" )]
        private static extern int EnumServices( IntPtr pBuffer, ref int numEntries, ref int cbBuf );

        /// <summary>
        /// Returns a list of ServiceInfos, providing information on the currently running "services".
        /// </summary>
        /// <returns></returns>
        public static List<ServiceInfo> GetServiceInfo()
        {
            int numEntries = 0;
            int cbSize = 0;
            int structSize = Marshal.SizeOf( typeof( ServiceEnumInfo ) );

            // call once to get required buffer size
            int result = EnumServices( IntPtr.Zero, ref numEntries, ref cbSize );

            // alloc a buffer of the correct size
            IntPtr pBuffer = IntPtr.Zero;
            
            List<ServiceInfo> serviceInfos = new List<ServiceInfo>();

            try
            {
                pBuffer = Marshal.AllocHGlobal( cbSize );

                // call again to get the real stuff
                result = EnumServices( pBuffer, ref numEntries, ref cbSize );

                // loop through the structure pulling out the prefix and the dll name
                for ( int i = 0; i < numEntries; i++ )
                {
                    // move a pointer along to point to the "current" structure each time through the loop
                    IntPtr pStruct = new IntPtr( pBuffer.ToInt32() + ( i * structSize ) );

                    // "translate" the pointer into an actual structure
                    ServiceEnumInfo sei = (ServiceEnumInfo)Marshal.PtrToStructure( pStruct,
                                                                typeof( ServiceEnumInfo ) );

                    string prefix = sei.PrefixName;
                    string dllName = Marshal.PtrToStringUni( sei.DllName );
                    IntPtr serviceHandle = sei.ServiceHandle;
                    uint serviceState = sei.ServiceState;

                    serviceInfos.Add( new ServiceInfo( prefix, dllName, serviceHandle, serviceState ) );
                }
            }
            finally
            {
                if ( pBuffer != IntPtr.Zero )
                    Marshal.FreeHGlobal( pBuffer ); // remember to free the buffer that we allocated
            }
            return serviceInfos;
        }
    }
}

