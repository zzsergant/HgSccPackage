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

using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System;
using System.Collections;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Collections.Specialized;

namespace ProcessWrapper
{
	//=============================================================================
	// This is a reimplementation of System.Diagnostics.Process, but with support of start in SUSPENDED mode.
	// The main purpose is to use for running process with redirected output and with ability
	// to assign a process to Job object.
	// It does not have all features of System.Diagnostics.Process.
	public class Process : IDisposable
	{
		bool have_process_id;
		int process_id;
		bool have_process_handle;
		SafeProcessHandle process_handle;
		bool have_main_thread_handle;
		SafeThreadHandle main_thread;
		readonly Int32 process_access;

		ProcessStartInfo start_info;

		bool exited;
		int exit_code;
		bool signaled;

		StreamReader standard_output;
		StreamWriter standard_input;
		StreamReader standard_error;
		bool disposed;

		private enum StreamReadMode
		{
			Undefined,
			SyncMode,
			AsyncMode
		}

		StreamReadMode output_stream_read_mode;
		StreamReadMode error_stream_read_mode;

		public event DataReceivedEventHandler OutputDataReceived;
		public event DataReceivedEventHandler ErrorDataReceived;

		AsyncStreamReader output;
		AsyncStreamReader error;
		bool pending_output_read;
		bool pending_error_read;

		//-----------------------------------------------------------------------------
		public Process()
		{
			output_stream_read_mode = StreamReadMode.Undefined;
			error_stream_read_mode = StreamReadMode.Undefined;
			process_access = NativeMethods.PROCESS_ALL_ACCESS;
		}

		//-----------------------------------------------------------------------------
		bool Associated
		{
			get
			{
				return have_process_id || have_process_handle;
			}
		}

		//-----------------------------------------------------------------------------
		public int ExitCode
		{
			get
			{
				EnsureState(State.Exited);
				return exit_code;
			}
		}

		//-----------------------------------------------------------------------------
		public bool HasExited
		{
			get
			{
				if (!exited)
				{
					EnsureState(State.Associated);
					SafeProcessHandle handle = null;
					try
					{
						handle = GetProcessHandle(NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.SYNCHRONIZE, false);
						if (handle.IsInvalid)
						{
							exited = true;
						}
						else
						{
							int exitCode;

							if (NativeMethods.GetExitCodeProcess(handle, out exitCode) && exitCode != NativeMethods.STILL_ACTIVE)
							{
								this.exited = true;
								this.exit_code = exitCode;
							}
							else
							{
								if (!signaled)
								{
									ProcessWaitHandle wh = null;
									try
									{
										wh = new ProcessWaitHandle(handle);
										this.signaled = wh.WaitOne(0, false);
									}
									finally
									{

										if (wh != null)
											wh.Close();
									}
								}
								if (signaled)
								{
									if (!NativeMethods.GetExitCodeProcess(handle, out exitCode))
										throw new Win32Exception();

									this.exited = true;
									this.exit_code = exitCode;
								}
							}
						}
					}
					finally
					{
						ReleaseProcessHandle(handle);
					}
				}
				return exited;
			}
		}

		//-----------------------------------------------------------------------------
		public IntPtr Handle
		{
			get
			{
				EnsureState(State.Associated);
				return OpenProcessHandle(this.process_access).DangerousGetHandle();
			}
		}

		//-----------------------------------------------------------------------------
		public int Id
		{
			get
			{
				EnsureState(State.HaveId);
				return process_id;
			}
		}

		//-----------------------------------------------------------------------------
		public ProcessStartInfo StartInfo
		{
			get
			{
				if (start_info == null)
					start_info = new ProcessStartInfo();

				return start_info;
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException("value");

				start_info = value;
			}
		}

		//-----------------------------------------------------------------------------
		public StreamWriter StandardInput
		{
			get
			{
				if (standard_input == null)
				{
					throw new InvalidOperationException("CantGetStandardIn");
				}

				return standard_input;
			}
		}

		//-----------------------------------------------------------------------------
		public StreamReader StandardOutput
		{
			get
			{
				if (standard_output == null)
				{
					throw new InvalidOperationException("CantGetStandardOut");
				}

				if (output_stream_read_mode == StreamReadMode.Undefined)
				{
					output_stream_read_mode = StreamReadMode.SyncMode;
				}
				else if (output_stream_read_mode != StreamReadMode.SyncMode)
				{
					throw new InvalidOperationException("CantMixSyncAsyncOperation");
				}

				return standard_output;
			}
		}

		//-----------------------------------------------------------------------------
		public StreamReader StandardError
		{
			get
			{
				if (standard_error == null)
				{
					throw new InvalidOperationException("CantGetStandardError");
				}

				if (error_stream_read_mode == StreamReadMode.Undefined)
				{
					error_stream_read_mode = StreamReadMode.SyncMode;
				}
				else if (error_stream_read_mode != StreamReadMode.SyncMode)
				{
					throw new InvalidOperationException("CantMixSyncAsyncOperation");
				}

				return standard_error;
			}
		}

		//-----------------------------------------------------------------------------
		void ReleaseProcessHandle(SafeProcessHandle handle)
		{
			if (handle == null)
				return;

			if (have_process_handle && handle == process_handle)
				return;

			handle.Close();
		}

		//-----------------------------------------------------------------------------
		public void Close()
		{
			if (Associated)
			{
				if (have_process_handle)
				{
					process_handle.Close();
					process_handle = null;
					have_process_handle = false;
				}

				if (have_main_thread_handle)
				{
					main_thread.Close();
					main_thread = null;
					have_main_thread_handle = false;
				}

				have_process_id = false;

				standard_output = null;
				standard_input = null;
				standard_error = null;

				output = null;
				error = null;

				exited = false;
				signaled = false;
			}
		}

		//-----------------------------------------------------------------------------
		void EnsureState(State state)
		{
			if ((state & State.Associated) != 0)
			{
				if (!Associated)
					throw new InvalidOperationException("NoAssociatedProcess");
			}

			if ((state & State.HaveId) != 0)
			{
				if (!have_process_id)
				{
					EnsureState(State.Associated);
					throw new InvalidOperationException("ProcessIdRequired");
				}
			}

			if ((state & State.Exited) != 0)
			{
				if (!HasExited)
					throw new InvalidOperationException("WaitTillExit");

				if (!have_process_handle)
					throw new InvalidOperationException("NoProcessHandle");
			}
		}

		//-----------------------------------------------------------------------------
		SafeProcessHandle GetProcessHandle(int access, bool throw_if_exited)
		{
			if (have_process_handle)
			{
				if (throw_if_exited)
				{
					ProcessWaitHandle wait_handle = null;
					try
					{
						wait_handle = new ProcessWaitHandle(process_handle);
						if (wait_handle.WaitOne(0, false))
						{
							if (have_process_id)
								throw new InvalidOperationException("ProcessHasExited");
							else
								throw new InvalidOperationException("ProcessHasExitedNoId");
						}
					}
					finally
					{
						if (wait_handle != null)
							wait_handle.Close();
					}
				}
				return process_handle;
			}

			return SafeProcessHandle.InvalidHandle;
		}

		//-----------------------------------------------------------------------------
		SafeProcessHandle GetProcessHandle(int access)
		{
			return GetProcessHandle(access, true);
		}

		//-----------------------------------------------------------------------------
		SafeProcessHandle OpenProcessHandle()
		{
			return OpenProcessHandle(NativeMethods.PROCESS_ALL_ACCESS);
		}

		//-----------------------------------------------------------------------------
		SafeProcessHandle OpenProcessHandle(Int32 access)
		{
			if (!have_process_handle)
			{
				if (disposed)
					throw new ObjectDisposedException(GetType().Name);

				SetProcessHandle(GetProcessHandle(access));
			}
			return process_handle;
		}

		//-----------------------------------------------------------------------------
		void SetProcessHandle(SafeProcessHandle process_handle)
		{
			this.process_handle = process_handle;
			this.have_process_handle = true;
		}

		//-----------------------------------------------------------------------------
		void SetMainThread(SafeThreadHandle thread_handle)
		{
			this.main_thread = thread_handle;
			this.have_main_thread_handle = true;
		}


		//-----------------------------------------------------------------------------
		void SetProcessId(int process_id)
		{
			this.process_id = process_id;
			this.have_process_id = true;
		}

		//-----------------------------------------------------------------------------
		public bool Start()
		{
			Close();

			var process_start_info = StartInfo;
			if (process_start_info.FileName.Length == 0)
				throw new InvalidOperationException("FileNameMissing");

			return StartWithCreateProcess(process_start_info);
		}


		//-----------------------------------------------------------------------------
		private static void CreatePipeWithSecurityAttributes(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, NativeMethods.SECURITY_ATTRIBUTES lpPipeAttributes, int nSize)
		{
			bool ret = NativeMethods.CreatePipe(out hReadPipe, out hWritePipe, lpPipeAttributes, nSize);
			if (!ret || hReadPipe.IsInvalid || hWritePipe.IsInvalid)
			{
				throw new Win32Exception();
			}
		}

		//-----------------------------------------------------------------------------
		private void CreatePipe(out SafeFileHandle parent_handle, out SafeFileHandle child_handle, bool parent_inputs)
		{
			var security_attributes_parent = new NativeMethods.SECURITY_ATTRIBUTES();
			security_attributes_parent.bInheritHandle = true;

			SafeFileHandle pipe_handle = null;
			try
			{
				if (parent_inputs)
					CreatePipeWithSecurityAttributes(out child_handle, out pipe_handle, security_attributes_parent, 0);
				else
					CreatePipeWithSecurityAttributes(out pipe_handle, out child_handle, security_attributes_parent, 0);

				if (!NativeMethods.DuplicateHandle(new HandleRef(this, NativeMethods.GetCurrentProcess()),
																   pipe_handle,
																   new HandleRef(this, NativeMethods.GetCurrentProcess()),
																   out parent_handle,
																   0,
																   false,
																   NativeMethods.DUPLICATE_SAME_ACCESS))
				{
					throw new Win32Exception();
				}
			}
			finally
			{
				if (pipe_handle != null && !pipe_handle.IsInvalid)
					pipe_handle.Close();
			}
		}

		//-----------------------------------------------------------------------------
		private static StringBuilder BuildCommandLine(string executable_file_name, string arguments)
		{
			var command_line = new StringBuilder();

			string file_name = executable_file_name.Trim();
			bool file_name_is_quoted = (file_name.StartsWith("\"", StringComparison.Ordinal) && file_name.EndsWith("\"", StringComparison.Ordinal));

			if (!file_name_is_quoted)
				command_line.Append("\"");

			command_line.Append(file_name);

			if (!file_name_is_quoted)
				command_line.Append("\"");

			if (!String.IsNullOrEmpty(arguments))
			{
				command_line.Append(" ");
				command_line.Append(arguments);
			}

			return command_line;
		}

		//-----------------------------------------------------------------------------
		private bool StartWithCreateProcess(ProcessStartInfo info)
		{
			if (info.StandardOutputEncoding != null && !info.RedirectStandardOutput)
			{
				throw new InvalidOperationException("StandardOutputEncodingNotAllowed");
			}

			if (info.StandardErrorEncoding != null && !info.RedirectStandardError)
			{
				throw new InvalidOperationException("StandardErrorEncodingNotAllowed");
			}

			if (disposed)
			{
				throw new ObjectDisposedException(GetType().Name);
			}

			var command_line = BuildCommandLine(info.FileName, info.Arguments);

			var startup_info = new NativeMethods.STARTUPINFO();
			var process_info = new NativeMethods.PROCESS_INFORMATION();
			var safe_process_handle = new SafeProcessHandle();
			var safe_thread_handle = new SafeThreadHandle();

			int error_code = 0;
			
			SafeFileHandle standard_input_write_pipe_handle = null;
			SafeFileHandle standard_output_read_pipe_handle = null;
			SafeFileHandle standard_error_read_pipe_handle = null;
			var environment_handle = new GCHandle();

			try
			{
				if (info.RedirectStandardInput || info.RedirectStandardOutput || info.RedirectStandardError)
				{
					if (info.RedirectStandardInput)
						CreatePipe(out standard_input_write_pipe_handle, out startup_info.hStdInput, true);
					else
						startup_info.hStdInput = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE), false);

					if (info.RedirectStandardOutput)
						CreatePipe(out standard_output_read_pipe_handle, out startup_info.hStdOutput, false);
					else
						startup_info.hStdOutput = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE), false);

					if (info.RedirectStandardError)
						CreatePipe(out standard_error_read_pipe_handle, out startup_info.hStdError, false);
					else
						startup_info.hStdError = new SafeFileHandle(NativeMethods.GetStdHandle(NativeMethods.STD_ERROR_HANDLE), false);

					startup_info.dwFlags = NativeMethods.STARTF_USESTDHANDLES;
				}

				int creation_flags = 0;
				if (info.CreateNoWindow)
					creation_flags |= NativeMethods.CREATE_NO_WINDOW;

				IntPtr environment_ptr = IntPtr.Zero;
				if (info.HaveEnvironmentVariables)
				{
					bool unicode = true;
					creation_flags |= NativeMethods.CREATE_UNICODE_ENVIRONMENT;

					byte[] environment_bytes = EnvironmentBlock.ToByteArray(info.EnvironmentVariables, unicode);
					environment_handle = GCHandle.Alloc(environment_bytes, GCHandleType.Pinned);
					environment_ptr = environment_handle.AddrOfPinnedObject();
				}

				string working_directory = info.WorkingDirectory;
				if (working_directory == string.Empty)
					working_directory = Environment.CurrentDirectory;

				if (info.CreateSuspended)
					creation_flags |= NativeMethods.CREATE_SUSPENDED;

				bool result = NativeMethods.CreateProcess(
							null,
							command_line,
							null,
							null,
							true,
							creation_flags,
							environment_ptr,
							working_directory,
							startup_info,
							process_info
						);

				if (!result)
					error_code = Marshal.GetLastWin32Error();

				if (process_info.hProcess != IntPtr.Zero && process_info.hProcess != NativeMethods.INVALID_HANDLE_VALUE)
					safe_process_handle.InitialSetHandle(process_info.hProcess);

				if (process_info.hThread != IntPtr.Zero && process_info.hThread != NativeMethods.INVALID_HANDLE_VALUE)
					safe_thread_handle.InitialSetHandle(process_info.hThread);

				if (!result)
				{
					if (error_code == NativeMethods.ERROR_BAD_EXE_FORMAT || error_code == NativeMethods.ERROR_EXE_MACHINE_TYPE_MISMATCH)
						throw new Win32Exception(error_code, "InvalidApplication");

					throw new Win32Exception(error_code);
				}
			}
			finally
			{
				if (environment_handle.IsAllocated)
					environment_handle.Free();

				startup_info.Dispose();
			}

			if (info.RedirectStandardInput)
			{
				standard_input = new StreamWriter(new FileStream(standard_input_write_pipe_handle, FileAccess.Write, 4096, false), Console.InputEncoding, 4096);
				standard_input.AutoFlush = true;
			}

			if (info.RedirectStandardOutput)
			{
				Encoding enc = info.StandardOutputEncoding ?? Console.OutputEncoding;
				standard_output = new StreamReader(new FileStream(standard_output_read_pipe_handle, FileAccess.Read, 4096, false), enc, true, 4096);
			}
	
			if (info.RedirectStandardError)
			{
				Encoding enc = info.StandardErrorEncoding ?? Console.OutputEncoding;
				standard_error = new StreamReader(new FileStream(standard_error_read_pipe_handle, FileAccess.Read, 4096, false), enc, true, 4096);
			}

			bool ret = false;
			if (!safe_process_handle.IsInvalid)
			{
				SetProcessHandle(safe_process_handle);

				if (info.CreateSuspended)
					SetMainThread(safe_thread_handle);
				else
					safe_thread_handle.Close();

				SetProcessId(process_info.dwProcessId);
				ret = true;
			}

			return ret;
		}

		//-----------------------------------------------------------------------------
		public bool ResumeMainThread()
		{
			if (!have_main_thread_handle)
				throw new InvalidOperationException("Main thread");

			return NativeMethods.ResumeThread(main_thread) != UInt32.MaxValue;
		}

		//-----------------------------------------------------------------------------
		public static Process Start(ProcessStartInfo start_info)
		{
			var process = new Process();
			if (start_info == null)
				throw new ArgumentNullException("start_info");

			process.StartInfo = start_info;
	
			if (process.Start())
				return process;

			return null;
		}

		//-----------------------------------------------------------------------------
		public void Kill()
		{
			SafeProcessHandle handle = null;
			try
			{
				handle = GetProcessHandle(NativeMethods.PROCESS_TERMINATE);
				if (!NativeMethods.TerminateProcess(handle, -1))
					throw new Win32Exception();
			}
			finally
			{
				ReleaseProcessHandle(handle);
			}
		}

		//-----------------------------------------------------------------------------
		public bool WaitForExit(int milliseconds)
		{
			SafeProcessHandle handle = null;
			ProcessWaitHandle process_wait_handle = null;
			bool exited;

			try
			{
				handle = GetProcessHandle(NativeMethods.SYNCHRONIZE, false);
				if (handle.IsInvalid)
				{
					exited = true;
				}
				else
				{
					process_wait_handle = new ProcessWaitHandle(handle);
					if (process_wait_handle.WaitOne(milliseconds, false))
					{
						exited = true;
						signaled = true;
					}
					else
					{
						exited = false;
						signaled = false;
					}
				}
			}
			finally
			{
				if (process_wait_handle != null)
					process_wait_handle.Close();

				if (output != null && milliseconds == -1)
					output.WaitUtilEof();

				if (error != null && milliseconds == -1)
					error.WaitUtilEof();

				ReleaseProcessHandle(handle);
			}

			return exited;
		}

		//-----------------------------------------------------------------------------
		public void WaitForExit()
		{
			WaitForExit(-1);
		}

		//-----------------------------------------------------------------------------
		public void BeginOutputReadLine()
		{
			if (output_stream_read_mode == StreamReadMode.Undefined)
				output_stream_read_mode = StreamReadMode.AsyncMode;
			else if (output_stream_read_mode != StreamReadMode.AsyncMode)
				throw new InvalidOperationException("CantMixSyncAsyncOperation");

			if (pending_output_read)
				throw new InvalidOperationException("PendingAsyncOperation");

			pending_output_read = true;

			if (output == null)
			{
				if (standard_output == null)
					throw new InvalidOperationException("CantGetStandardOut");

				Stream s = standard_output.BaseStream;
				output = new AsyncStreamReader(s, OutputReadNotifyUser, standard_output.CurrentEncoding);
			}

			output.BeginReadLine();
		}

		//-----------------------------------------------------------------------------
		public void BeginErrorReadLine()
		{
			if (error_stream_read_mode == StreamReadMode.Undefined)
				error_stream_read_mode = StreamReadMode.AsyncMode;
			else if (error_stream_read_mode != StreamReadMode.AsyncMode)
				throw new InvalidOperationException("CantMixSyncAsyncOperation");

			if (pending_error_read)
				throw new InvalidOperationException("PendingAsyncOperation");

			pending_error_read = true;

			if (error == null)
			{
				if (standard_error == null)
					throw new InvalidOperationException("CantGetStandardError");

				Stream s = standard_error.BaseStream;
				error = new AsyncStreamReader(s, ErrorReadNotifyUser, standard_error.CurrentEncoding);
			}

			error.BeginReadLine();
		}

		//-----------------------------------------------------------------------------
		public void CancelOutputRead()
		{
			if (output != null)
				output.CancelOperation();
			else
				throw new InvalidOperationException("NoAsyncOperation");

			pending_output_read = false;
		}

		//-----------------------------------------------------------------------------
		public void CancelErrorRead()
		{
			if (error != null)
				error.CancelOperation();
			else
				throw new InvalidOperationException("NoAsyncOperation");

			pending_error_read = false;
		}

		//-----------------------------------------------------------------------------
		internal void OutputReadNotifyUser(string data)
		{
			var output_data_received = OutputDataReceived;
			if (output_data_received != null)
			{
				var e = new DataReceivedEventArgs(data);
				output_data_received(this, e);
			}
		}

		//-----------------------------------------------------------------------------
		internal void ErrorReadNotifyUser(string data)
		{
			var error_data_received = ErrorDataReceived;
			if (error_data_received != null)
			{
				var e = new DataReceivedEventArgs(data);
				error_data_received(this, e);
			}
		}

		//-----------------------------------------------------------------------------
		[Flags]
		enum State
		{
			HaveId = 0x1,
			Exited = 0x10,
			Associated = 0x20,
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (!disposed)
			{
				Close();
				disposed = true;
			}
		}
	}

	//=============================================================================
	internal static class EnvironmentBlock
	{
		public static byte[] ToByteArray(StringDictionary env_dictionary, bool unicode)
		{
			var keys = new string[env_dictionary.Count];
			env_dictionary.Keys.CopyTo(keys, 0);

			var values = new string[env_dictionary.Count];
			env_dictionary.Values.CopyTo(values, 0);

			Array.Sort(keys, values, OrdinalCaseInsensitiveComparer.Default);

			var str_buff = new StringBuilder();
			for (int i = 0; i < env_dictionary.Count; ++i)
			{
				str_buff.Append(keys[i]);
				str_buff.Append('=');
				str_buff.Append(values[i]);
				str_buff.Append('\0');
			}

			str_buff.Append('\0');

			byte[] env_block;
			if (unicode)
				env_block = Encoding.Unicode.GetBytes(str_buff.ToString());
			else
				env_block = Encoding.Default.GetBytes(str_buff.ToString());

			if (env_block.Length > UInt16.MaxValue)
				throw new InvalidOperationException("EnvironmentBlockTooLong");

			return env_block;
		}
	}

	//-----------------------------------------------------------------------------
	internal class OrdinalCaseInsensitiveComparer : IComparer
	{
		internal static readonly OrdinalCaseInsensitiveComparer Default = new OrdinalCaseInsensitiveComparer();

		//-----------------------------------------------------------------------------
		public int Compare(Object a, Object b)
		{
			var str_a = a as String;
			var str_b = b as String;

			if (str_a != null && str_b != null)
				return String.Compare(str_a, str_b, StringComparison.OrdinalIgnoreCase);

			return Comparer.Default.Compare(a, b);
		}
	}

	//-----------------------------------------------------------------------------
	internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal static SafeProcessHandle InvalidHandle = new SafeProcessHandle(IntPtr.Zero);

		internal SafeProcessHandle()
			: base(true)
		{
		}

		//-----------------------------------------------------------------------------
		internal SafeProcessHandle(IntPtr handle)
			: base(true)
		{
			SetHandle(handle);
		}

		//-----------------------------------------------------------------------------
		internal void InitialSetHandle(IntPtr h)
		{
			handle = h;
		}

		//-----------------------------------------------------------------------------
		override protected bool ReleaseHandle()
		{
			return NativeMethods.CloseHandle(handle);
		}

	}

	//-----------------------------------------------------------------------------
	internal class ProcessWaitHandle : WaitHandle
	{
		//-----------------------------------------------------------------------------
		internal ProcessWaitHandle(SafeProcessHandle process_handle)
		{
			SafeWaitHandle wait_handle;
			bool succeeded = NativeMethods.DuplicateHandle(
				new HandleRef(this, NativeMethods.GetCurrentProcess()),
				process_handle,
				new HandleRef(this, NativeMethods.GetCurrentProcess()),
				out wait_handle,
				0,
				false,
				NativeMethods.DUPLICATE_SAME_ACCESS);

			if (!succeeded)
			{
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			}

			SafeWaitHandle = wait_handle;
		}
	}

	//-----------------------------------------------------------------------------
	internal sealed class SafeThreadHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal SafeThreadHandle()
			: base(true)
		{
		}

		//-----------------------------------------------------------------------------
		internal void InitialSetHandle(IntPtr h)
		{
			SetHandle(h);
		}

		//-----------------------------------------------------------------------------
		override protected bool ReleaseHandle()
		{
			return NativeMethods.CloseHandle(handle);
		}
	}

	//=============================================================================
	internal sealed class SafeLocalMemHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal SafeLocalMemHandle()
			: base(true)
		{
		}

		//-----------------------------------------------------------------------------
		internal SafeLocalMemHandle(IntPtr existing_handle, bool owns_handle)
			: base(owns_handle)
		{
			SetHandle(existing_handle);
		}

		//-----------------------------------------------------------------------------
		override protected bool ReleaseHandle()
		{
			return NativeMethods.LocalFree(handle) == IntPtr.Zero;
		}
	}

	//-----------------------------------------------------------------------------
	public delegate void DataReceivedEventHandler(object sender, DataReceivedEventArgs e);

	//=============================================================================
	public class DataReceivedEventArgs : EventArgs
	{
		//-----------------------------------------------------------------------------
		public DataReceivedEventArgs(string data)
		{
			Data = data;
		}

		//-----------------------------------------------------------------------------
		public string Data { get; private set; }
	}
}
