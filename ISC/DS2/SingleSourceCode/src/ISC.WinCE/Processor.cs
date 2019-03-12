using System;
using System.Runtime.InteropServices;
using System.Text;




namespace ISC.WinCE
{

public enum ProcessorArchitecture: ushort
{
    Intel = 0,
    MIPS  = 1,
    Alpha = 2,
    PPC   = 3,
    SHX   = 4,
    ARM   = 5,
    IA64  = 6,
    Alpha64 = 7,
    Unknown = 0xFFFF
}
public enum ProcessorType: uint
{
    INTEL_386     =386,
    INTEL_486     =486,
    INTEL_PENTIUM =586,
    INTEL_PENTIUMII =686,
    MIPS_R4000    =4000,
    ALPHA_21064   =21064,
    PPC_403       =403,
    PPC_601       =601,
    PPC_603       =603,
    PPC_604       =604,
    PPC_620       =620,
    HITACHI_SH3   =10003,
    HITACHI_SH3E  =10004,
    HITACHI_SH4   =10005,
    MOTOROLA_821  =821,
    SHx_SH3       =103,
    SHx_SH4       =104,
    STRONGARM     =2577,
    ARM720        =1824,
    ARM820        =2080,
    ARM920        =2336,
    ARM_7TDMI     =70001
}




/// <summary>
/// This class provides information describing the microprocessor.
/// Basically,it provides access to the data defined in the Windows CE
/// 'PROCESSOR_INFO' structure. 
/// </summary>
public sealed class Processor 
{
    public enum InstructionSetType: uint
    {
        FloatingPoint = 0x00000001,
        DSP           = 0x00000002,
        SixteenBit    = 0x00000004
    }

    public const string PXA255 = "PXA255";
    public const string PXA250 = "PXA250";

    private const int IOCTL_PROCESSOR_INFORMATION      = 0x01010064;
    private const int IOCTL_HAL_GET_CCCR               = 0x010120C8; // Note: This is a custom IOCTL in the DS2's BSP.
    private const int IOCTL_HAL_GET_COTULLA_TURBO_MODE = 0x0101280C;

    private byte [] _procData;
    private byte [] _cccrData;
    private byte [] _turboData;

    public Processor()
    {
        _procData = new byte[ 600 ];
        _cccrData = new byte[ 16 ];
        _turboData = new byte[ 4 ];

		Int32 bytesReturned = 0;
        //Int32 outBufSize = _procData.Length;

        // Call KernelIoControl passing the previously defined IOCTL_PROCESSOR_INFORMATION parameter.
        // We don’t need to pass any input buffers to this call so InputBuffer
        // and InputBufferSize are set to their null values
        bool retstat;
		retstat = KernelIoControl( IOCTL_PROCESSOR_INFORMATION, IntPtr.Zero, 0, _procData, _procData.Length, ref bytesReturned );
        retstat = KernelIoControl( IOCTL_HAL_GET_CCCR, IntPtr.Zero, 0, _cccrData, _cccrData.Length, ref bytesReturned );
        retstat = KernelIoControl( IOCTL_HAL_GET_COTULLA_TURBO_MODE, IntPtr.Zero, 0, _turboData, _turboData.Length, ref bytesReturned );
    }

    /// <summary>
    /// Should always be set to 1.
    /// </summary>
    public ushort Version 
    { 
        get { return (ushort)BitConverter.ToInt16( _procData, 0 ); }
        //set { BitConverter.GetBytes(value).CopyTo( _procData, 0 ); }
    }
		
    /// <summary>
    /// Name of the microprocessor core. MIPS or ARM, for example. 
    /// </summary>
    public string Core
    { 
        get
        {
            byte[] ret = new byte[80]; Buffer.BlockCopy( _procData, 2, ret, 0, 80 );
            return Encoding.Unicode.GetString( ret, 0, ret.Length ).TrimEnd('\0');
        }
        //set { Buffer.BlockCopy(Encoding.Unicode.GetBytes( value ), 0, _procData, 2, 80 ); }
    }

    /// <summary>
    /// Revision number of the microprocessor core.
    /// </summary>
    public ushort CoreRevision
    { 
        get { return (ushort)BitConverter.ToInt16( _procData, 82 ); }
        //set { BitConverter.GetBytes(value).CopyTo( _procData, 82 ); }
    }

    /// <summary>
    /// Set to the actual microprocessor name, for example, R4111.
    /// </summary>
    public string Name
    { 
        get
        {
            byte[] ret = new byte[80]; Buffer.BlockCopy( _procData, 84, ret, 0, 80 );
            return Encoding.Unicode.GetString( ret, 0, ret.Length ).TrimEnd('\0');
        }
        //set { Buffer.BlockCopy(Encoding.Unicode.GetBytes( value ), 0, _procData, 84, 80 ); }
    }

    /// <summary>
    /// Microprocessor revision number. 
    /// </summary>
    public ushort Revision
    { 
        get { return (ushort)BitConverter.ToInt16( _procData, 164 ); }
        //set { BitConverter.GetBytes(value).CopyTo( _procData, 164); }
    }

    /// <summary>
    /// Set to the catalog number for the processor. 
    /// </summary>
    public string CatalogNumber/*[100]*/
    { 
        get
        {
            byte[] ret = new byte[200]; Buffer.BlockCopy( _procData, 166, ret, 0, 200 ); 
            return Encoding.Unicode.GetString( ret, 0, ret.Length ).TrimEnd('\0');
        }
        //set { Buffer.BlockCopy(Encoding.Unicode.GetBytes( value ), 0, _procData, 166, 200 ); }
    }

    /// <summary>
    /// Set to the name of the microprocessor vendor. 
    /// </summary>
    public string Vendor /*[100];*/
    { 
        get
        {
            byte[] ret = new byte[200]; Buffer.BlockCopy( _procData, 366, ret, 0, 200 );
            return Encoding.Unicode.GetString(ret, 0, ret.Length).TrimEnd('\0');
        }
        //set { Buffer.BlockCopy(Encoding.Unicode.GetBytes( value ), 0, _procData, 366, 200 ); }
    }

    /// <summary>
    /// The following shows the possible flags for this microprocessor. 
    /// 
    /// InstructionSetType.FloatingPoint - The microprocessor has hardware floating point enabled. This flag
    /// is set if the WprocessorLevel parameter of the SYSTEM_INFO structure indicates that the 
    /// current microprocessor is a MIPS R4300 or x86 with hardware floating point. If the target 
    /// CPU for your OS image is R4300 or i486 with hardware floating point then this flag must 
    /// be set to report hardware floating point support. On i486 images that do not have hardware 
    /// floating point, such as the 486SX, this bit is not set. 
    /// 
    /// InstructionSetType.DSP - The microprocessor has DSP support enabled. If your device has a DSP
    /// that can be utilized by an application, this should be reported by setting this flag.
    /// 
    /// InstructionSetType.SixteenBit - The microprocessor supports a 16-bit instruction set.
    /// If the WprocessorLevel parameter of the SYSTEM_INFO structure indicates that the current 
    /// MIPS microprocessor is a MIPS R4000, this signifies that the microprocessor also support 
    /// the MIPS16 instruction set. If the target CPU for your OS image is R4111, this flag must 
    /// be set if MIPS16 support needs to be reported. 
    /// </summary>
    public uint InstructionSet
    { 
        get { return (uint)BitConverter.ToInt32( _procData, 566 ); }
        //set { BitConverter.GetBytes(value).CopyTo( _procData, 566 ); }
    }

    /// <summary>
    /// Maximum clock speed of the CPU.
    /// </summary>
    public uint ClockSpeed
    { 
 
        get 
        { 
            return BitConverter.ToUInt32( _procData, 572 );
        }
        //set { BitConverter.GetBytes(value).CopyTo( _procData, 570 ); }
    }


    /// <summary>
    /// Crystal Frequency to Memory Frequency Multiplier.
    /// For PXA25x CPU, this value will be 27, 36, or 45.
    /// </summary>
    public int L_Multiplier
    {
        get
        {
            return BitConverter.ToInt32( _cccrData, 0 );
        }
    }

    /// <summary>
    /// Memory Frequency to Run Mode Frequency Multiplier.
    /// For PX25x CPU, this value will be 1, 2, or 4.
    /// </summary>
    public int M_Multiplier
    {
        get
        {
            return BitConverter.ToInt32( _cccrData, 4 );
        }
    }

    /// <summary>
    /// Run Mode Frequency to Turbo Mode Frequency Multiplier.  This multipler determines
    /// the maximum speed of the CPU.  If CPU speed is 200Mhz, and N Multiplier is 1.5, then
    /// the Maximum speed of the CPU is 300Mhz.
    /// For PX25x CPU, this value will be 1.0, 1.5, 2.0, or 3.0.
    /// </summary>
    public double N_Multiplier
    {
        get
        {
            return BitConverter.ToDouble( _cccrData, 8 );
        }
    }

    /// <summary>
    /// Returns whether or not the CPU's turbo mode is enabled.
    /// </summary>
    public bool TurboEnabled
    {
        get
        {
            int enabled = BitConverter.ToInt32( _turboData, 0 );
            return ( enabled != 0 ) ? true : false;
        }
    }

    /// <summary>
    /// Access into WinCE KernelIoControl function.
    /// </summary>
    /// <param name="IoControlCode"></param>
    /// <param name="InputBuffer"></param>
    /// <param name="InputBufferSize"></param>
    /// <param name="OutputBuffer"></param>
    /// <param name="OutputBufferSize"></param>
    /// <param name="BytesReturned"></param>
    /// <returns></returns>
    [DllImport("coredll.dll")]
    private static extern bool KernelIoControl( Int32 IoControlCode,
                                                IntPtr InputBuffer,
                                                Int32 InputBufferSize,
                                                byte[] OutputBuffer,
                                                Int32 OutputBufferSize,
                                                ref Int32 BytesReturned );
	

}  // end-class ProcessorInfo


}  // end-namespace
