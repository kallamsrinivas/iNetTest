using System;
using System.Runtime.InteropServices;


namespace ISC.WinCE
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Contains information used in asynchronous input and output. 
    /// </summary>
    [StructLayout( LayoutKind.Sequential )]
    public struct Overlapped
    {
        public int Internal;
        public int InternalHigh;
        public int Offset;
        public int OffsetHigh;
        public IntPtr Event;
    }
}
