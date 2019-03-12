using System;
using System.Runtime.InteropServices;


namespace ISC.WinCE
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Used in the SetCommTimeouts function to set and query the time-out parameters for a communications
    /// device. The parameters determine the behavior of ReadFile, WriteFile, ReadFileEx, and WriteFileEx
    /// operations on the device. 
    /// </summary>
    [StructLayout( LayoutKind.Sequential )]
    public struct CommTimeOuts
    {
        public int ReadIntervalTimeout;
        public int ReadTotalTimeoutMultiplier;
        public int ReadTotalTimeoutConstant;
        public int WriteTotalTimeoutMultiplier;
        public int WriteTotalTimeoutConstant;
    }
}
