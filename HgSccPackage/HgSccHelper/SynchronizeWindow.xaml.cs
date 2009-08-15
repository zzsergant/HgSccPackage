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

			var arg = "incoming";
			worker.RunWorkerAsync(arg);
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

			var arg = "outgoing";
			worker.RunWorkerAsync(arg);
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

			if (e.Error != null)
			{
				Worker_NewMsg(e.Error.Message);
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
				proc.OutputDataReceived += proc_OutputDataReceived;
				proc.Start();

				proc.BeginOutputReadLine();
				var events = new WaitHandle[]{stop_event, new AutoResetEvent(false)};
				events[1].SafeWaitHandle = new SafeWaitHandle(proc.Handle, true);

				var result = WaitHandle.WaitAny(events);
				proc.CancelOutputRead();
				proc.OutputDataReceived -= proc_OutputDataReceived;

				if (result == 0)
				{
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
					return;
				}

				var error = proc.StandardError.ReadToEnd();
				if (!string.IsNullOrEmpty(error))
					throw new System.ApplicationException(error);
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
