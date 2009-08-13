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

		//------------------------------------------------------------------
		public SynchronizeWindow()
		{
			InitializeComponent();

			worker = new BackgroundWorker();
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		//------------------------------------------------------------------
		BackgroundWorker worker;

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
 			Title = string.Format("Synchronize: '{0}'", WorkingDir);
			Hg = new Hg();

			var paragraph = new Paragraph();
			paragraph.TextIndent = 0;

			var run = new Run("Some Text");
			paragraph.Inlines.Add(run);

			paragraph.Inlines.Add(new LineBreak());

			var red = new Run("Red color");
			red.Foreground = Brushes.Red;
			paragraph.Inlines.Add(red);

			richTextBox.Document.Blocks.Clear();
			richTextBox.Document.Blocks.Add(paragraph);
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
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
			worker.DoWork += new DoWorkEventHandler(Incoming_DoWork);
//			worker.ProgressChanged += new ProgressChangedEventHandler(Incoming_ProgressChanged);
			worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Incoming_RunWorkerCompleted);
//			worker.WorkerReportsProgress = true;
			worker.WorkerSupportsCancellation = true;

			richTextBox.Document.Blocks.Clear();
			worker.RunWorkerAsync();
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
			e.Handled = true;
		}

		//------------------------------------------------------------------
		void Incoming_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (e.UserState is string)
			{
				Incoming_NewMsg(e.UserState as string);
			}
		}

		//------------------------------------------------------------------
		void Incoming_NewMsg(string msg)
		{
			if (richTextBox.Document.Blocks.Count == 0)
				richTextBox.Document.Blocks.Add(new Paragraph());

			var paragraph = richTextBox.Document.Blocks.FirstBlock as Paragraph;
			paragraph.Inlines.Add(new Run(msg));
			paragraph.Inlines.Add(new LineBreak());

			richTextBox.ScrollToEnd();
		}

		//------------------------------------------------------------------
		void Incoming_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			worker.DoWork -= new DoWorkEventHandler(Incoming_DoWork);
//			worker.ProgressChanged -= new ProgressChangedEventHandler(Incoming_ProgressChanged);
			worker.RunWorkerCompleted -= new RunWorkerCompletedEventHandler(Incoming_RunWorkerCompleted);

			var msg = new Run();
			if (e.Error != null)
			{
				msg.Text = e.Error.Message;
				msg.Foreground = Brushes.Red;
			}
			else if (e.Cancelled)
			{
				msg.Text = "[Operation canceled]";
				msg.Foreground = Brushes.Red;
			}
			else
			{
				msg.Text = "[Operation completed]";
				msg.Foreground = Brushes.Green;
			}

			if (richTextBox.Document.Blocks.Count == 0)
				richTextBox.Document.Blocks.Add(new Paragraph());

			var paragraph = richTextBox.Document.Blocks.FirstBlock as Paragraph;
			paragraph.Inlines.Add(msg);
			richTextBox.ScrollToEnd();
		}

		//------------------------------------------------------------------
		void Incoming_DoWork(object sender, DoWorkEventArgs e)
		{
			var args = new StringBuilder();
			args.Append("incoming");

			using (Process proc = Process.Start(Hg.PrepareProcess(WorkingDir, args.ToString())))
			{
				var reader = proc.StandardOutput;
				while (true)
				{
					if (worker.CancellationPending)
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

					string str = reader.ReadLine();
					if (str == null)
						break;

					// FIXME: Tried report progress, but it quickly
					// fill message queue
					//worker.ReportProgress(0, str);

					// Trying to update one line at time (invoke synchronously)
					Dispatcher.Invoke(DispatcherPriority.Normal,
						new Action<string>(Incoming_NewMsg), str);
				}

				var error = proc.StandardError.ReadToEnd();
				if (!string.IsNullOrEmpty(error))
					throw new System.ApplicationException(error);
			}
		}
	}
}
