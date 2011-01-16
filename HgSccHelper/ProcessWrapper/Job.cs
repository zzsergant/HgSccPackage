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

namespace ProcessWrapper
{
	//=============================================================================
	class Job : IDisposable
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

		[DllImport("kernel32.dll")]
		public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

		[DllImport("kernel32.dll", EntryPoint = "SetInformationJobObject")]
		public static extern bool SetInformationJobObject_Extended(IntPtr hJob, int JobObjectInfoClass, ref NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, int cbJobObjectInfoLength);

		[DllImport("kernel32.dll")]
		public static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

		[DllImport("kernel32.dll")]
		public static extern void CloseHandle(IntPtr handle);

		IntPtr job;

		//-----------------------------------------------------------------------------
		public Job()
		{
			job = CreateJobObject(IntPtr.Zero, null);
			int JobObjectExtendedLimitInformation = 9;

			// FIXME: This will not work on win2k. But, I think I can live with that.

			const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
			NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
			info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

			SetInformationJobObject_Extended(job, JobObjectExtendedLimitInformation, ref info, Marshal.SizeOf(info));
		}

		//-----------------------------------------------------------------------------
		public bool AssignProcessToJob(Process proc)
		{
			return AssignProcessToJobObject(job, proc.Handle);
		}

		//-----------------------------------------------------------------------------
		public bool TerminateProc()
		{
			// terminate the Job object, which kills all processes within it
			return TerminateJobObject(job, uint.MaxValue);
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			CloseHandle(job);
		}
	}
}
