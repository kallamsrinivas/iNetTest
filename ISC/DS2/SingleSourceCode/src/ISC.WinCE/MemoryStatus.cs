using System;
using System.Runtime.InteropServices;


namespace ISC.WinCE
{
    /// <summary>
    /// Mimics WinCE MEMORYSTATUS structure and provides access to WinCE GlobalMemoryStatus function.
    /// Check the "Programming Microsoft Windows CE Second Edition" book, pg.352-353
    /// for memory management information and system information structures.
    /// </summary>
    [ StructLayout ( LayoutKind.Sequential ) ]
    public struct MemoryStatus
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public uint dwTotalPhysical;
        public uint dwAvailablePhysical;
        public uint dwTotalPageFile;
        public uint dwAvailablePageFile;
        public uint dwTotalVirtual;
        public uint dwAvailableVirtual;

        /// <summary>
        /// WINDOWS API: Method to return information about the system.
        /// </summary>
        [ DllImport( "coredll.dll" ) ]
        public static unsafe extern void GlobalMemoryStatus( MemoryStatus * status );

    };

    public class MemoryDivision
    {
        public const int SYSMEM_CHANGED = 0;
        public const int SYSMEM_MUSTREBOOT = 1;
        public const int SYSMEM_REBOOTPENDING = 2;
        public const int SYSMEM_FAILED = 3;

        static MemoryDivision()
        {
        }

        public MemoryDivision()
        {
        }

        [ DllImport( "coredll.dll" ) ]
        public static extern bool GetSystemMemoryDivision( out int storePages , out int ramPages, out int pageSize );

        /// <summary>
        /// Sets the specified number of pages for the object store.
        /// </summary>
        /// <param name="storePages">Specifies the number of pages to allocate for the store. </param>
        /// <returns>
        /// The function returns SYSMEM_CHANGED if it is executed successfully.
        /// If the memory division cannot be changed until a reboot, SYSMEM_MUSTREBOOT is returned.
        /// The function returns SYSMEM_FAILED if the dwStorePages parameter is out of range.
        /// If SYSMEM_FAILED is returned, you can get further information by calling GetLastError.
        /// </returns>
        [ DllImport( "coredll.dll" ) ]
        public static extern int SetSystemMemoryDivision( int storePages );
    }
}
