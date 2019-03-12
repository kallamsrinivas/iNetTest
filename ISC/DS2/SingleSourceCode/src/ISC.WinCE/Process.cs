using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;


namespace ISC.WinCE
{
    public class ProcessInfo
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public Int32 ProcessId;
        public Int32 ThreadId;
    }






#region Process class
/// <summary>
/// The Process class provides a mechanism for obtaining information about all
/// running process on the system. This class is based on sample code obtained
/// from the MSDN whitepaper "Creating a Microsoft® .NET Compact Framework-based
/// Process Manager Application" whitepaper.
/// <seealso>http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dnnetcomp/html/CompactfxTechArt.asp</seealso>
/// </summary>
public class Process
{
	private string processName;
	private IntPtr handle;
	private int threadCount;
	private int baseAddress;

	
    #region Constructors
	/// <summary>
	/// Default ctor
	/// </summary>
	public Process()
	{
		
	}
    
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="id"></param>
    /// <param name="procname"></param>
    /// <param name="threadcount"></param>
    /// <param name="baseaddress"></param>
	private Process(IntPtr id, string procname, int threadcount, int baseaddress)
	{	
		handle = id;
		processName = procname;
		threadCount = threadcount;
		baseAddress = baseaddress;
	}

    #endregion

	//ToString implementation for ListBox binding
	public override string ToString()
	{
		return processName;
	}

    #region Properties

	public int BaseAddress
	{
		get
		{
			return baseAddress;
		}
	}

	public int ThreadCount
	{
		get
		{
			return threadCount;
		}
	}

	public IntPtr Handle
	{
		get
		{
			return handle;
		}
	}

	public string ProcessName
	{
		get
		{
			return processName;
		}
	}

    #endregion

    /// <summary>
    /// Kills (terminates) this process
    /// </summary>
	public void Kill()
	{
		IntPtr hProcess;
	
		hProcess = OpenProcess(PROCESS_TERMINATE, false, (int) handle);

		if(hProcess != (IntPtr) INVALID_HANDLE_VALUE) 
		{
			bool bRet;
			bRet = TerminateProcess(hProcess, 0);
			WinCeApi.CloseHandle(hProcess);
		}
		

	}

    /// <summary>
    /// Returns an array of Process instances representing the currently
    /// running processes.
    /// </summary>
    /// <returns></returns>
	public static Process[] GetProcesses()
	{
        List<Process> procList = new List<Process>();

		IntPtr handle = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

		if ((int)handle > 0)
		{
			try
			{
				PROCESSENTRY32 peCurrent;
				PROCESSENTRY32 pe32 = new PROCESSENTRY32();
				//Get byte array to pass to the API calls
				byte[] peBytes = pe32.ToByteArray();
				//Get the first process
				int retval = Process32First(handle, peBytes);

				while(retval == 1)
				{
					//Convert bytes to the class
					peCurrent = new PROCESSENTRY32(peBytes);
					//New instance
					Process proc = new Process(new IntPtr((int)peCurrent.PID), peCurrent.Name, (int)peCurrent.ThreadCount, (int)peCurrent.BaseAddress);
		
					procList.Add(proc);

					retval = Process32Next(handle, peBytes);
				}
			}
			catch(Exception ex)
			{
				throw new Exception("Exception: " + ex.Message);
			}
			
			//Close handle
			CloseToolhelp32Snapshot(handle); 
			
			return procList.ToArray();

		}
		else
		{
			throw new Exception("Unable to create snapshot");
		}


	}

	#endregion

	#region PROCESSENTRY32 implementation

//		typedef struct tagPROCESSENTRY32 
//		{ 
//			DWORD dwSize; 
//			DWORD cntUsage; 
//			DWORD th32ProcessID; 
//			DWORD th32DefaultHeapID; 
//			DWORD th32ModuleID; 
//			DWORD cntThreads; 
//			DWORD th32ParentProcessID; 
//			LONG pcPriClassBase; 
//			DWORD dwFlags; 
//			TCHAR szExeFile[MAX_PATH]; 
//			DWORD th32MemoryBase;
//			DWORD th32AccessKey;
//		} PROCESSENTRY32;

	private class PROCESSENTRY32
	{
		// constants for structure definition
		private const int SizeOffset = 0;
		private const int UsageOffset = 4;
		private const int ProcessIDOffset=8;
		private const int DefaultHeapIDOffset = 12;
		private const int ModuleIDOffset = 16;
		private const int ThreadsOffset = 20;
		private const int ParentProcessIDOffset = 24;
		private const int PriClassBaseOffset = 28;
		private const int dwFlagsOffset = 32;
		private const int ExeFileOffset = 36;
		private const int MemoryBaseOffset = 556;
		private const int AccessKeyOffset = 560;
		private const int Size = 564;
		private const int MAX_PATH = 260;

		// data members
		public uint dwSize; 
		public uint cntUsage; 
		public uint th32ProcessID; 
		public uint th32DefaultHeapID; 
		public uint th32ModuleID; 
		public uint cntThreads; 
		public uint th32ParentProcessID; 
		public long pcPriClassBase; 
		public uint dwFlags; 
		public string szExeFile;
		public uint th32MemoryBase;
		public uint th32AccessKey;
	
		//Default constructor
		public PROCESSENTRY32()
		{


		}

		// create a PROCESSENTRY instance based on a byte array		
		public PROCESSENTRY32(byte[] aData)
		{
			dwSize = GetUInt(aData, SizeOffset);
			cntUsage = GetUInt(aData, UsageOffset);
			th32ProcessID = GetUInt(aData, ProcessIDOffset);
			th32DefaultHeapID = GetUInt(aData, DefaultHeapIDOffset);
			th32ModuleID = GetUInt(aData, ModuleIDOffset);
			cntThreads = GetUInt(aData, ThreadsOffset);
			th32ParentProcessID = GetUInt(aData, ParentProcessIDOffset);
			pcPriClassBase = (long) GetUInt(aData, PriClassBaseOffset);
			dwFlags = GetUInt(aData, dwFlagsOffset);
			szExeFile = GetString(aData, ExeFileOffset, MAX_PATH);
			th32MemoryBase = GetUInt(aData, MemoryBaseOffset);
			th32AccessKey = GetUInt(aData, AccessKeyOffset);
		}

		#region Helper conversion functions
		// utility:  get a uint from the byte array
		private static uint GetUInt(byte[] aData, int Offset)
		{
			return BitConverter.ToUInt32(aData, Offset);
		}
	
		// utility:  set a uint int the byte array
		private static void SetUInt(byte[] aData, int Offset, int Value)
		{
			byte[] buint = BitConverter.GetBytes(Value);
			Buffer.BlockCopy(buint, 0, aData, Offset, buint.Length);
		}

		// utility:  get a ushort from the byte array
		private static ushort GetUShort(byte[] aData, int Offset)
		{
			return BitConverter.ToUInt16(aData, Offset);
		}
	
		// utility:  set a ushort int the byte array
		private static void SetUShort(byte[] aData, int Offset, int Value)
		{
			byte[] bushort = BitConverter.GetBytes((short)Value);
			Buffer.BlockCopy(bushort, 0, aData, Offset, bushort.Length);
		}
	
		// utility:  get a unicode string from the byte array
		private static string GetString(byte[] aData, int Offset, int Length)
		{
			String sReturn =  Encoding.Unicode.GetString(aData, Offset, Length);
			return sReturn;
		}
	
		// utility:  set a unicode string in the byte array
		private static void SetString(byte[] aData, int Offset, string Value)
		{
			byte[] arr = Encoding.ASCII.GetBytes(Value);
			Buffer.BlockCopy(arr, 0, aData, Offset, arr.Length);
		}
		#endregion

		// create an initialized data array
		public byte[] ToByteArray()
		{
			byte[] aData;
			aData = new byte[Size];
			//set the Size member
			SetUInt(aData, SizeOffset, Size);
			return aData;
		}

		public string Name
		{
			get
			{
				return szExeFile.Substring(0, szExeFile.IndexOf('\0'));
			}
		}

		public ulong PID
		{
			get
			{
				return th32ProcessID;
			}
		}

		public ulong BaseAddress
		{
			get
			{
				return th32MemoryBase;
			}
		}

		public ulong ThreadCount
		{
			get
			{
				return cntThreads;
			}
		}
	}
	#endregion

	#region PInvoke declarations
	private const int TH32CS_SNAPPROCESS = 0x00000002;
	[DllImport("toolhelp.dll")]
	public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processid);
	[DllImport("toolhelp.dll")]
	public static extern int CloseToolhelp32Snapshot(IntPtr handle);
	[DllImport("toolhelp.dll")]
	public static extern int Process32First(IntPtr handle, byte[] pe);
	[DllImport("toolhelp.dll")]
	public static extern int Process32Next(IntPtr handle, byte[] pe);
	[DllImport("coredll.dll")]
	private static extern IntPtr OpenProcess(int flags, bool fInherit, int PID);
	private const int PROCESS_TERMINATE = 1;
	[DllImport("coredll.dll")]
	private static extern bool TerminateProcess(IntPtr hProcess, uint ExitCode);
	private const int INVALID_HANDLE_VALUE = -1;


    [DllImport( "coredll.DLL", SetLastError = true )]
    public extern static int CreateProcess( String imageName, String cmdLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, Int32 boolInheritHandles, Int32 dwCreationFlags,
        IntPtr lpEnvironment, IntPtr lpszCurrentDir,  IntPtr lpsiStartInfo, ProcessInfo pi );
    [DllImport( "coredll.dll" )]
    public extern static Int32 GetExitCodeProcess( IntPtr hProcess, out Int32 exitcode );
    [DllImport( "coredll.dll" )]
    public extern static IntPtr ActivateDevice( string lpszDevKey, Int32 dwClientInfo );
    [DllImport( "coredll.dll" )]
    public extern static Int32 WaitForSingleObject( IntPtr Handle, Int32 Wait );

    public static bool CreateProcess( String ExeName, String CmdLine )
    {
        Int32 INFINITE;
        unchecked { INFINITE = (int)0xFFFFFFFF; }
        ProcessInfo pi = new ProcessInfo();
        if ( CreateProcess( ExeName, CmdLine, IntPtr.Zero, IntPtr.Zero,
            0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, pi ) == 0 )
        {
            return false;
        }

        WaitForSingleObject( pi.hProcess, INFINITE );
        Int32 exitCode;
        if ( GetExitCodeProcess( pi.hProcess, out exitCode ) == 0 )
        {
            //Log.Error( "Failure in GetExitCodeProcess" );
            WinCeApi.CloseHandle( pi.hThread );
            WinCeApi.CloseHandle( pi.hProcess );
            return false;
        }

        WinCeApi.CloseHandle( pi.hThread );
        WinCeApi.CloseHandle( pi.hProcess );
        if ( exitCode != 0 )
            return false;
        else
            return true;
    } 

	#endregion
}
}
