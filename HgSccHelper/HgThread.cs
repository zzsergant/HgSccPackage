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
		EventWaitHandle stop_event;

		HgThreadParams work_params;

		//-----------------------------------------------------------------------------
		public HgThread()
		{
			worker = new BackgroundWorker();
			stop_event = new EventWaitHandle(false, EventResetMode.AutoReset);
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
				stop_event.Set();
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
		void Worker_DoWork(object sender, DoWorkEventArgs e)
		{
			var hg = new Hg();

			using (Process proc = new Process())
			{
				proc.StartInfo = hg.PrepareProcess(work_params.WorkingDir, work_params.Args);

				// FIXME: Put the hg in unbuffered mode for
				// redirected output and error streams
				// proc.StartInfo.EnvironmentVariables.Add("PYTHONUBUFFERED", "1");

				proc.OutputDataReceived += proc_OutputDataReceived;
				proc.ErrorDataReceived += proc_ErrorDataReceived;

				try
				{
					proc.Start();
				}
				catch (Win32Exception ex)
				{
					proc.OutputDataReceived -= proc_OutputDataReceived;
					proc.ErrorDataReceived -= proc_ErrorDataReceived;
					throw ex;
				}

				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();

				var events = new WaitHandle[] { stop_event, new AutoResetEvent(false) };
				events[1].SafeWaitHandle = new SafeWaitHandle(proc.Handle, true);

				var result = WaitHandle.WaitAny(events);

				if (result == 0)
				{
					// stop event raised

					try
					{
						proc.Kill();
						proc.WaitForExit();
					}
					catch (InvalidOperationException)
					{
					}
					catch (Win32Exception)
					{
					}

					e.Cancel = true;
				}
				else
				{
					// Wait until all redirected output is written
					proc.WaitForExit();
				}

				proc.CancelOutputRead();
				proc.CancelErrorRead();

				proc.OutputDataReceived -= proc_OutputDataReceived;
				proc.ErrorDataReceived -= proc_ErrorDataReceived;

				e.Result = proc.ExitCode;
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
			stop_event.Close();
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
