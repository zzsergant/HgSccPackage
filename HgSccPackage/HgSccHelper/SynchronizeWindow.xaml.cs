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

using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Data;
using HgSccPackage.Tools;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text;
using System.ComponentModel;
using System;
using System.Windows.Threading;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for SynchronizeWindow.xaml
	/// </summary>
	public partial class SynchronizeWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand IncomingCommand = new RoutedUICommand("Incoming",
			"Incoming", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand PullCommand = new RoutedUICommand("Pull",
			"Pull", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand OutgoingCommand = new RoutedUICommand("Outgoing",
			"Outgoing", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand PushCommand = new RoutedUICommand("Push",
			"Push", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand StopCommand = new RoutedUICommand("Stop",
			"Stop", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		BackgroundWorker worker;
		EventWaitHandle stop_event;

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty UpdateAfterPullProperty =
			DependencyProperty.Register("UpdateAfterPull", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool UpdateAfterPull
		{
			get { return (bool)this.GetValue(UpdateAfterPullProperty); }
			set { this.SetValue(UpdateAfterPullProperty, value); }
		}

		//------------------------------------------------------------------
		public SynchronizeWindow()
		{
			InitializeComponent();

			worker = new BackgroundWorker();
			stop_event = new EventWaitHandle(false, EventResetMode.AutoReset);
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
 			Title = string.Format("Synchronize: '{0}'", WorkingDir);
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			if (StopCommand.CanExecute(sender, e.Source as IInputElement))
				StopCommand.Execute(sender, e.Source as IInputElement);

			worker.Dispose();
		}

		//------------------------------------------------------------------
		private void Incoming_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Incoming_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			worker.DoWork += Worker_DoWork;
			worker.RunWorkerCompleted += Worker_Completed;
			worker.WorkerSupportsCancellation = true;

			textBox.Text = "";
			Worker_NewMsg("[Incoming started]\n");

			var builder = new StringBuilder();
			builder.Append("-v incoming");

			worker.RunWorkerAsync(builder.ToString());
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Outgoing_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Outgoing_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			worker.DoWork += Worker_DoWork;
			worker.RunWorkerCompleted += Worker_Completed;
			worker.WorkerSupportsCancellation = true;

			textBox.Text = "";
			Worker_NewMsg("[Outgoing started]\n");

			var builder = new StringBuilder();
			builder.Append("-v outgoing");

			worker.RunWorkerAsync(builder.ToString());
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Pull_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Pull_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			worker.DoWork += Worker_DoWork;
			worker.RunWorkerCompleted += Worker_Completed;
			worker.WorkerSupportsCancellation = true;

			textBox.Text = "";
			Worker_NewMsg("[Pull started]\n");

			var builder = new StringBuilder();
			builder.Append("-v pull");
			if (UpdateAfterPull)
				builder.Append(" -u");

			worker.RunWorkerAsync(builder.ToString());
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Push_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Push_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			worker.DoWork += Worker_DoWork;
			worker.RunWorkerCompleted += Worker_Completed;
			worker.WorkerSupportsCancellation = true;

			textBox.Text = "";
			Worker_NewMsg("[Push started]\n");

			var builder = new StringBuilder();
			builder.Append("-v push");

			worker.RunWorkerAsync(builder.ToString());
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Stop_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && worker.IsBusy && !worker.CancellationPending);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Stop_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			worker.CancelAsync();
			stop_event.Set();
			e.Handled = true;
		}

		//------------------------------------------------------------------
		void Worker_NewMsg(string msg)
		{
			textBox.AppendText(msg + "\n");
			textBox.ScrollToEnd();
		}

		//------------------------------------------------------------------
		void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
		{
			worker.DoWork -= Worker_DoWork;
			worker.RunWorkerCompleted -= Worker_Completed;

			Worker_NewMsg("");

			if (e.Error != null)
			{
				Worker_NewMsg("[Error: " + e.Error.Message + "]");
			}
			else if (e.Cancelled)
			{
				Worker_NewMsg("[Operation canceled]");
			}
			else
			{
				Worker_NewMsg("[Operation completed]");
			}

			// Updating commands state (CanExecute)
			CommandManager.InvalidateRequerySuggested();
		}

		//------------------------------------------------------------------
		void Worker_DoWork(object sender, DoWorkEventArgs e)
		{
			var args = new StringBuilder();
			args.Append(e.Argument as string);

			var hg = new Hg();
			
			using (Process proc = new Process())
			{
				proc.StartInfo = hg.PrepareProcess(WorkingDir, args.ToString());
// 				proc.StartInfo.EnvironmentVariables.Add("PYTHONUBUFFERED", "1");
				proc.OutputDataReceived += proc_OutputDataReceived;
				proc.ErrorDataReceived += proc_ErrorDataReceived;
				proc.Start();

				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();
				var events = new WaitHandle[]{stop_event, new AutoResetEvent(false)};
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

				if (proc.ExitCode < 0 && !e.Cancel)
				{
					throw new ApplicationException(
						String.Format("[Exit code: {0}]", proc.ExitCode));
				}
			}
		}

		//------------------------------------------------------------------
		void proc_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				var error_msg = string.Format("[Error: {0}]", e.Data);
				Dispatcher.Invoke(DispatcherPriority.Normal,
					new Action<string>(Worker_NewMsg), error_msg);
			}
		}

		//------------------------------------------------------------------
		void proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				Dispatcher.Invoke(DispatcherPriority.Normal,
					new Action<string>(Worker_NewMsg), e.Data);
			}
		}
	}
}
