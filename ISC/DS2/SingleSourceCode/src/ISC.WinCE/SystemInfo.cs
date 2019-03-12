using System;
using System.Runtime.InteropServices;

namespace ISC.WinCE
{
    /// <summary>
    /// Mimics WinCE SYSTEM_INFO structure and provides access to WinCE GetSystemInfo function.
    /// Check the "Programming Microsoft Windows CE Second Edition" book, pg.352-353
    /// for memory management information and system information structures.
    /// </summary>
    [ StructLayout ( LayoutKind.Sequential ) ]
    public struct SystemInfo
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public uint lpMinimumApplicationAddress;
        public uint lpMaximumApplicationAddress;
        public uint dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;

        /// <summary>
        /// WINDOWS API: Method to return information about the system.
        /// </summary>
        [ DllImport( "coredll.dll" ) ]
        public static unsafe extern void GetSystemInfo( SystemInfo* info );
    }
}
