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
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace HgSccHelper
{
	//==================================================================
	/// <summary>
	/// Executes hg in background thread
	/// </summary>
	class HgThread : IDisposable
	{
		BackgroundWorker worker;

		HgThreadParams work_params;

		Process work_process;
		object critical;

		//-----------------------------------------------------------------------------
		public HgThread()
		{
			worker = new BackgroundWorker();
			critical = new object();
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
				lock (critical)
				{
					if (work_process != null && !work_process.HasExited)
						KillProcess(work_process);
				}
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

		//------------------------------------------------------------------
		private void KillProcess(Process process)
		{
			if (process != null)
			{
				try
				{
					process.Kill();
				}
				catch (InvalidOperationException ex)
				{
					Logger.WriteLine("Invalid operation exception from HgThread: {0}", ex.Message);
				}
				catch (Win32Exception ex)
				{
					Logger.WriteLine("Win32Exception exception from HgThread: {0}", ex.Message);
				}
			}
		}

		//------------------------------------------------------------------
		void Worker_DoWork(object sender, DoWorkEventArgs e)
		{
			var hg = new Hg();

			using (Process process = new Process())
			{
				process.StartInfo = hg.PrepareProcess(work_params.WorkingDir, work_params.Args);

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

				process.BeginOutputReadLine();
				process.BeginErrorReadLine();

				lock (critical)
				{
					if (worker.CancellationPending)
					{
						// Work is allready cancelled
						// Need to kill process

						KillProcess(process);
					}
					else
					{
						this.work_process = process;
					}
				}

				// Wait until all redirected output is written
				process.WaitForExit();

				lock (critical)
				{
					this.work_process = null;
				}

				process.CancelOutputRead();
				process.CancelErrorRead();

				process.OutputDataReceived -= proc_OutputDataReceived;
				process.ErrorDataReceived -= proc_ErrorDataReceived;

				e.Result = process.ExitCode;
				
				if (worker.CancellationPending)
					e.Cancel = true;
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
	enum HgThreadStatus
	{
		Completed,
		Canceled,
		Error
	}

	//==================================================================
	/// <summary>
	/// Result of HgThread execution
	/// </summary>
	class HgThreadResult
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
	class HgThreadParams
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
