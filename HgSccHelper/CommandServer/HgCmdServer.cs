//=========================================================================
// Copyright 2012 Sergey Antonov <sergant_@mail.ru>
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
using System.ComponentModel;
using System.IO;
using ProcessWrapper;

namespace HgSccHelper.CommandServer
{
	//==================================================================
	public class HgCmdServer : IDisposable
	{
		private Job job;
		private Process process;
		private Stream stdin;
		private Stream stdout;

		//-----------------------------------------------------------------------------
		public HgCmdServer()
		{
		}

		//-----------------------------------------------------------------------------
		public Stream Stdin
		{
			get { return stdin; }
		}

		//-----------------------------------------------------------------------------
		public Stream Stdout
		{
			get { return stdout; }
		}

		//-----------------------------------------------------------------------------
		public bool Run(HgCmdServerParams param)
		{
			if (IsBusy)
				throw new InvalidOperationException("HgCmdServer is allready ran");

			if (param.WorkingDir == null || param.Args == null)
				throw new ArgumentNullException("WorkingDir and Args parameters for HgCmdServer must not be null");

			job = new Job();
			process = new Process();

			// TODO: utf8
			process.StartInfo = PrepareProcess(param.WorkingDir, param.Args);

			try
			{
				process.Start();
			}
			catch (Win32Exception ex)
			{
				Logger.WriteLine("Error: {0}", ex.Message);
				return false;
			}

			if (!job.AssignProcessToJob(process))
			{
				Logger.WriteLine("Assign process to job failed");
			}

			process.ResumeMainThread();
			stdin = process.StandardInput.BaseStream;
			stdout = process.StandardOutput.BaseStream;

			return true;
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Gets a value indicating whether thread is running background operation
		/// </summary>
		public bool IsBusy
		{
			get { return job != null; }
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Send cancellation request to worker thread
		/// </summary>
		public void Stop()
		{
			Dispose();
		}

		//-----------------------------------------------------------------------------
		public bool ReadChannel(ref Message msg)
		{
			msg.Channel = '\0';
			msg.Length = 0;

			msg.Channel = (char)Stdout.ReadByte();
			uint b0 = (uint)Stdout.ReadByte();
			uint b1 = (uint)Stdout.ReadByte();
			uint b2 = (uint)Stdout.ReadByte();
			uint b3 = (uint)Stdout.ReadByte();
			msg.Length = (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
			msg.Reserve(msg.Length);

			if (msg.IsInputChannel)
				return true;

			uint total_bytes = 0;
			while (total_bytes != msg.Length)
			{
				int bytes_read = Stdout.Read(msg.Data, (int)total_bytes, (int)(msg.Length - total_bytes));
				total_bytes += (uint)bytes_read;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool WriteBlock(byte[] data)
		{
			byte b0 = (byte)(data.Length >> 24);
			byte b1 = (byte)(data.Length >> 16);
			byte b2 = (byte)(data.Length >> 8);
			byte b3 = (byte)(data.Length >> 0);

			Stdin.WriteByte(b0);
			Stdin.WriteByte(b1);
			Stdin.WriteByte(b2);
			Stdin.WriteByte(b3);

			Stdin.Write(data, 0, data.Length);
			Stdin.Flush();
			return true;
		}

		//-----------------------------------------------------------------------------
		public ProcessStartInfo PrepareProcess(string work_dir, string arguments)
		{
			var hg_client = Hg.DefaultClient;
			if (!String.IsNullOrEmpty(Hg.CustomHgClient))
				hg_client = Hg.CustomHgClient;

			var info = new ProcessStartInfo();
			info.FileName = hg_client;
			info.Arguments = arguments;

			info.CreateNoWindow = true;
			info.WorkingDirectory = work_dir;

			info.RedirectStandardOutput = true;
			info.RedirectStandardInput = true;
			info.RedirectStandardError = true;

			// Create suspended and then attach to Job !!
			info.CreateSuspended = true;
			info.CreateBreakAwayFromJob = true;

			info.EnvironmentVariables["HGPLAIN"] = "1";

			Logger.WriteLine("Creating new process:");
			Logger.WriteLine("- Filename         = {0}", info.FileName);
			Logger.WriteLine("- WorkingDirectory = {0}", work_dir);
			Logger.WriteLine("- Arguments        = {0}", info.Arguments);

			if (Hg.UseUtf8)
				info.EnvironmentVariables["HGENCODING"] = "utf8";

			return info;
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (job != null)
			{
				if (process != null)
				{
					process.Dispose();
					process = null;
				}

				job.Dispose();
				job = null;
			}
		}
	}

	//==================================================================
	/// <summary>
	/// Parameters for HgCmdServer
	/// </summary>
	public class HgCmdServerParams
	{
		/// <summary>
		/// Working directory for hg
		/// </summary>
		public string WorkingDir { get; set; }

		/// <summary>
		/// Arguments for hg
		/// </summary>
		public string Args { get; set; }
	}

	//=============================================================================
	public class Message
	{
		public char Channel { get; set; }
		public uint Length { get; set; }
		public byte[] Data { get; private set; }

		//-----------------------------------------------------------------------------
		public Message()
		{
			Data = new byte[4096];
		}

		//-----------------------------------------------------------------------------
		public bool IsInputChannel
		{
			get { return Channel == 'I' || Channel == 'L'; }
		}

		//-----------------------------------------------------------------------------
		public void Reserve(uint size)
		{
			if (Data.Length < size)
				Data = new byte[size];
		}
	}
}
