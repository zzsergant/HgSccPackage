//=========================================================================
// Copyright 2009 Sergey Antonov <sergant_@mail.ru>
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
using System.Text;
using ProcessWrapper;

namespace HgSccHelper
{
	//==================================================================
	/// <summary>
	/// Executes hg in background thread
	/// </summary>
	public class HgThread : IDisposable
	{
		private const int InitialTimeoutForCancellation = 500;
		private const int CancellationTimeoutStep = 30;
		BackgroundWorker worker;
		HgThreadParams work_params;

		//-----------------------------------------------------------------------------
		public HgThread()
		{
			worker = new BackgroundWorker();
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Starts execution of hg in background
		/// </summary>
		/// <param name="param">Execution parameters</param>
		public void Run(HgThreadParams param)
		{
			if (IsBusy)
				throw new InvalidOperationException("HgThread is busy");

			if (param.WorkingDir == null || param.Args == null)
				throw new ArgumentNullException("WorkingDir and Args parameters for HgThread must not be null");

			work_params = param;

			worker.DoWork += Worker_DoWork;
			worker.RunWorkerCompleted += Worker_Completed;
			worker.WorkerSupportsCancellation = true;

			worker.RunWorkerAsync();
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Gets a value indicating whether thread is running background operation
		/// </summary>
		public bool IsBusy
		{
			get { return worker.IsBusy; }
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Gets a value indicating whether the application has requested cancellation of a background operation
		/// </summary>
		public bool CancellationPending
		{
			get { return worker.CancellationPending; }
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Send cancellation request to worker thread
		/// </summary>
		public void Cancel()
		{
			if (IsBusy && !worker.CancellationPending)
			{
				worker.CancelAsync();
			}
		}

		//------------------------------------------------------------------
		void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
		{
			worker.DoWork -= Worker_DoWork;
			worker.RunWorkerCompleted -= Worker_Completed;

			var completed = new HgThreadResult();

			if (e.Error != null)
			{
				completed.Status = HgThreadStatus.Error;
				completed.ErrorMessage = e.Error.Message;
			}
			else if (e.Cancelled)
			{
				completed.Status = HgThreadStatus.Canceled;
			}
			else
			{
				completed.Status = HgThreadStatus.Completed;
				completed.ExitCode = (int)e.Result;
			}

			var on_complete = work_params.CompleteHandler;
			work_params = null;

			if (on_complete != null)
				on_complete(completed);
		}

		//-----------------------------------------------------------------------------
		public ProcessStartInfo PrepareProcess(string work_dir, string arguments, HgProcessFlags flags)
		{
			// We need to get path to hg
			var hg = new Hg();
			var hg_info = hg.PrepareProcess(work_dir, arguments, flags);

			var info = new ProcessStartInfo();
			info.FileName = hg_info.FileName;
			
			info.Arguments = arguments;

			info.CreateNoWindow = true;
			info.WorkingDirectory = work_dir;

			if ((flags & HgProcessFlags.RedirectOutput) != 0)
			{
				info.RedirectStandardOutput = true;
				info.RedirectStandardError = true;
			}

			// Create suspended and then attach to Job !!
			info.CreateSuspended = true;

			info.EnvironmentVariables["HGPLAIN"] = "1";

			if (((flags & HgProcessFlags.ForceSystemEncoding) == 0) && Hg.UseUtf8)
			{
				info.EnvironmentVariables["HGENCODING"] = "utf8";

				if ((flags & HgProcessFlags.RedirectOutput) != 0)
				{
					info.StandardOutputEncoding = Encoding.UTF8;
					info.StandardErrorEncoding = Encoding.UTF8;
				}

				info.Arguments = Encoding.Default.GetString(Encoding.UTF8.GetBytes(info.Arguments));
			}

			return info;
		}

		//------------------------------------------------------------------
		void Worker_DoWork(object sender, DoWorkEventArgs e)
		{
			using (var job = new Job())
			using (var process = new Process())
			{
				var flags = HgProcessFlags.RedirectOutput;
				if (work_params.ForceSystemEncoding)
					flags |= HgProcessFlags.ForceSystemEncoding;

				process.StartInfo = PrepareProcess(work_params.WorkingDir, work_params.Args, flags);

				// FIXME: Put the hg in unbuffered mode for
				// redirected output and error streams
				// proc.StartInfo.EnvironmentVariables.Add("PYTHONUBUFFERED", "1");

				process.OutputDataReceived += proc_OutputDataReceived;
				process.ErrorDataReceived += proc_ErrorDataReceived;

				try
				{
					process.Start();
				}
				catch (Win32Exception ex)
				{
					process.OutputDataReceived -= proc_OutputDataReceived;
					process.ErrorDataReceived -= proc_ErrorDataReceived;
					throw ex;
				}

				if (!job.AssignProcessToJob(process))
					Logger.WriteLine("Assign process to job failed");

				process.ResumeMainThread();

				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				int cancellation_timeout = InitialTimeoutForCancellation;
				while (true)
				{
					if (process.WaitForExit(cancellation_timeout))
					{
						// Wait until all redirected output is written
						process.WaitForExit();

						process.CancelOutputRead();
						process.CancelErrorRead();

						process.OutputDataReceived -= proc_OutputDataReceived;
						process.ErrorDataReceived -= proc_ErrorDataReceived;
						break;
					}

					cancellation_timeout = CancellationTimeoutStep;

					if (worker.CancellationPending)
					{
						process.CancelOutputRead();
						process.CancelErrorRead();

						process.OutputDataReceived -= proc_OutputDataReceived;
						process.ErrorDataReceived -= proc_ErrorDataReceived;

						Logger.WriteLine("Kill PID: {0}, args = {1}", process.Id, work_params.Args);

						// The process and all subprocesses will be killed on job close
						break;
					}
				}

				if (worker.CancellationPending)
					e.Cancel = true;
				else
					e.Result = process.ExitCode;
			}
		}

		//------------------------------------------------------------------
		void proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				if (work_params.ErrorHandler != null)
					work_params.ErrorHandler(e.Data as string);
			}
		}

		//------------------------------------------------------------------
		void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				if (work_params.OutputHandler != null)
					work_params.OutputHandler(e.Data as string);
			}
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			worker.Dispose();
		}
	}

	//==================================================================
	public enum HgThreadStatus
	{
		Completed,
		Canceled,
		Error
	}

	//==================================================================
	/// <summary>
	/// Result of HgThread execution
	/// </summary>
	public class HgThreadResult
	{
		/// <summary>
		/// Status of execution
		/// </summary>
		public HgThreadStatus Status { get; set; }

		/// <summary>
		/// Exit code.
		/// </summary>
		public int ExitCode { get; set; }

		/// <summary>
		/// Contains error message if there was execution error
		/// </summary>
		public string ErrorMessage { get; set; }
	}

	//==================================================================
	/// <summary>
	/// Parameters for HgThread
	/// </summary>
	public class HgThreadParams
	{
		/// <summary>
		/// Working directory for hg
		/// </summary>
		public string WorkingDir { get; set; }

		/// <summary>
		/// Arguments for hg
		/// </summary>
		public string Args { get; set; }

		/// <summary>
		/// Force system encoding (for commands like: diff)
		/// </summary>
		public bool ForceSystemEncoding { get; set; }

		/// <summary>
		/// Handler for redirected output stream. Runs in thread pool context.
		/// </summary>
		public Action<string> OutputHandler { get; set; }

		/// <summary>
		/// Handler for redirected error stream. Runs in thread pool context.
		/// </summary>
		public Action<string> ErrorHandler { get; set; }

		/// <summary>
		/// Completiong handler. Runs in caller thread context
		/// </summary>
		public Action<HgThreadResult> CompleteHandler { get; set; }
	}
}
