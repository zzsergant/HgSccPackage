//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
// 
// This software may be used and distributed according to the terms of the
// GNU General Public License version 2 as published by the Free Software
// Foundation.
// 
// See the file COPYING.TXT for the full text of the license, or see
// http://www.gnu.org/licenses/gpl-2.0.txt
// 
//=========================================================================

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Runtime.ConstrainedExecution;
using Microsoft.Win32.SafeHandles;

//=============================================================================
namespace ProcessWrapper
{
	//=============================================================================
	internal static class NativeMethods
	{
		public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

		public const int STARTF_USESTDHANDLES = 0x00000100;

		public const int STD_INPUT_HANDLE = -10;
		public const int STD_OUTPUT_HANDLE = -11;
		public const int STD_ERROR_HANDLE = -12;

		public const int STILL_ACTIVE = 0x00000103;
		public const int SW_HIDE = 0;

		public const int ERROR_CLASS_ALREADY_EXISTS = 1410;
		public const int ERROR_NONE_MAPPED = 1332;
		public const int ERROR_INSUFFICIENT_BUFFER = 122;
		public const int ERROR_INVALID_NAME = 123;
		public const int ERROR_PROC_NOT_FOUND = 127;
		public const int ERROR_BAD_EXE_FORMAT = 193;
		public const int ERROR_EXE_MACHINE_TYPE_MISMATCH = 216;
		public const int MAX_PATH = 260;

		[StructLayout(LayoutKind.Sequential)]
		internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
		{
			public long PerProcessUserTimeLimit;
			public long PerJobUserTimeLimit;
			public uint LimitFlags;
			public UIntPtr MinimumWorkingSetSize;
			public UIntPtr MaximumWorkingSetSize;
			public uint ActiveProcessLimit;
			public UIntPtr Affinity;
			public uint PriorityClass;
			public uint SchedulingClass;
		};

		[StructLayout(LayoutKind.Sequential)]
		internal struct IO_COUNTERS
		{
			public ulong ReadOperationCount;
			public ulong WriteOperationCount;
			public ulong OtherOperationCount;
			public ulong ReadTransferCount;
			public ulong WriteTransferCount;
			public ulong OtherTransferCount;
		};

		[StructLayout(LayoutKind.Sequential)]
		internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
		{
			public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
			public IO_COUNTERS IoInfo;
			public UIntPtr ProcessMemoryLimit;
			public UIntPtr JobMemoryLimit;
			public UIntPtr PeakProcessMemoryUsed;
			public UIntPtr PeakJobMemoryUsed;
		};

		[StructLayout(LayoutKind.Sequential)]
		internal class STARTUPINFO
		{
			public int cb;
			public IntPtr lpReserved = IntPtr.Zero;
			public IntPtr lpDesktop = IntPtr.Zero;
			public IntPtr lpTitle = IntPtr.Zero;
			public int dwX;
			public int dwY;
			public int dwXSize;
			public int dwYSize;
			public int dwXCountChars;
			public int dwYCountChars;
			public int dwFillAttribute;
			public int dwFlags;
			public short wShowWindow;
			public short cbReserved2;
			public IntPtr lpReserved2 = IntPtr.Zero;
			public SafeFileHandle hStdInput = new SafeFileHandle(IntPtr.Zero, false);
			public SafeFileHandle hStdOutput = new SafeFileHandle(IntPtr.Zero, false);
			public SafeFileHandle hStdError = new SafeFileHandle(IntPtr.Zero, false);

			//-----------------------------------------------------------------------------
			public STARTUPINFO()
			{
				cb = Marshal.SizeOf(this);
			}

			//-----------------------------------------------------------------------------
			public void Dispose()
			{
				if (hStdInput != null && !hStdInput.IsInvalid)
				{
					hStdInput.Close();
					hStdInput = null;
				}

				if (hStdOutput != null && !hStdOutput.IsInvalid)
				{
					hStdOutput.Close();
					hStdOutput = null;
				}

				if (hStdError != null && !hStdError.IsInvalid)
				{
					hStdError.Close();
					hStdError = null;
				}
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		internal class SECURITY_ATTRIBUTES
		{
			public int nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
			public SafeLocalMemHandle lpSecurityDescriptor = new SafeLocalMemHandle(IntPtr.Zero, false);
			public bool bInheritHandle;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool GetExitCodeProcess(SafeProcessHandle hProcess, out int lpExitCode);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern uint ResumeThread(SafeThreadHandle hThread);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		public static extern IntPtr GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, SECURITY_ATTRIBUTES lpPipeAttributes, int nSize);

		[DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		public static extern bool CloseHandle(IntPtr handle);

		[DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool CloseHandle(HandleRef handle);

		[StructLayout(LayoutKind.Sequential)]
		internal class PROCESS_INFORMATION
		{
			public IntPtr hProcess = IntPtr.Zero;
			public IntPtr hThread = IntPtr.Zero;
			public int dwProcessId;
			public int dwThreadId;
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
		public static extern bool CreateProcess(
			[MarshalAs(UnmanagedType.LPTStr)]
            string lpApplicationName,
			StringBuilder lpCommandLine,
			SECURITY_ATTRIBUTES lpProcessAttributes,
			SECURITY_ATTRIBUTES lpThreadAttributes,
			bool bInheritHandles,
			int dwCreationFlags,
			IntPtr lpEnvironment,
			[MarshalAs(UnmanagedType.LPTStr)]
            string lpCurrentDirectory,
			STARTUPINFO lpStartupInfo,
			PROCESS_INFORMATION lpProcessInformation
		);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool TerminateProcess(SafeProcessHandle hProcess, int uExitCode);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern int GetCurrentProcessId();

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		public static extern IntPtr GetCurrentProcess();

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern SafeProcessHandle OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern SafeThreadHandle OpenThread(int dwDesiredAccess, bool bInheritHandle, int dwThreadId);

		[DllImport("kernel32.dll")]
		public static extern IntPtr LocalFree(IntPtr hMem);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		public static extern bool DuplicateHandle(
			HandleRef hSourceProcessHandle,
			SafeHandle hSourceHandle,
			HandleRef hTargetProcess,
			out SafeFileHandle targetHandle,
			int dwDesiredAccess,
			bool bInheritHandle,
			int dwOptions
		);

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		public static extern bool DuplicateHandle(
			HandleRef hSourceProcessHandle,
			SafeHandle hSourceHandle,
			HandleRef hTargetProcess,
			out SafeWaitHandle targetHandle,
			int dwDesiredAccess,
			bool bInheritHandle,
			int dwOptions
		);

		public const int PROCESS_TERMINATE = 0x0001;
		public const int PROCESS_CREATE_THREAD = 0x0002;
		public const int PROCESS_SET_SESSIONID = 0x0004;
		public const int PROCESS_VM_OPERATION = 0x0008;
		public const int PROCESS_VM_READ = 0x0010;
		public const int PROCESS_VM_WRITE = 0x0020;
		public const int PROCESS_DUP_HANDLE = 0x0040;
		public const int PROCESS_CREATE_PROCESS = 0x0080;
		public const int PROCESS_SET_QUOTA = 0x0100;
		public const int PROCESS_SET_INFORMATION = 0x0200;
		public const int PROCESS_QUERY_INFORMATION = 0x0400;
		public const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;
		public const int SYNCHRONIZE = 0x00100000;
		public const int PROCESS_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0xFFF;

		public const int DUPLICATE_CLOSE_SOURCE = 1;
		public const int DUPLICATE_SAME_ACCESS = 2;

		public const int CREATE_NO_WINDOW = 0x08000000;
		public const int CREATE_SUSPENDED = 0x00000004;
		public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;

		[StructLayout(LayoutKind.Sequential)]
		internal struct MEMORY_BASIC_INFORMATION
		{
			internal IntPtr BaseAddress;
			internal IntPtr AllocationBase;
			internal uint AllocationProtect;
			internal UIntPtr RegionSize;
			internal uint State;
			internal uint Protect;
			internal uint Type;
		}
	}
}
