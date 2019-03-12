using System;
using System.Runtime.InteropServices;


namespace ISC.WinCE
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Defines control settings for a serial communications device.
    /// </summary>
    public struct DCB
    {
        public uint DCBlength;
        public uint BaudRate;
        public uint Binary;
        public ushort wReserved;
        public ushort XonLim;
        public ushort XoffLim;
        public byte ByteSize;
        public byte Parity;			// 0-4=no,odd,even,mark,space 
        public byte StopBits;		// 0,1,2 = 1, 1.5, 2 
        public char XonChar;
        public char XoffChar;
        public char ErrorChar;
        public char EofChar;
        public char EvtChar;
        public ushort wReserved1;
    }


}
