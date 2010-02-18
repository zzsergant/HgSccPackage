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
using System.Text;
using System.Windows.Threading;
using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace HgSccHelper
{
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
		HgThread worker;

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty UpdateAfterPullProperty =
			DependencyProperty.Register("UpdateAfterPull", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool UpdateAfterPull
		{
			get { return (bool)this.GetValue(UpdateAfterPullProperty); }
			set { this.SetValue(UpdateAfterPullProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ShowNewestFirstProperty =
			DependencyProperty.Register("ShowNewestFirst", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool ShowNewestFirst
		{
			get { return (bool)this.GetValue(ShowNewestFirstProperty); }
			set { this.SetValue(ShowNewestFirstProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ShowPatchProperty =
			DependencyProperty.Register("ShowPatch", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool ShowPatch
		{
			get { return (bool)this.GetValue(ShowPatchProperty); }
			set { this.SetValue(ShowPatchProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ShowNoMergesProperty =
			DependencyProperty.Register("ShowNoMerges", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool ShowNoMerges
		{
			get { return (bool)this.GetValue(ShowNoMergesProperty); }
			set { this.SetValue(ShowNoMergesProperty, value); }
		}

		List<PathAlias> paths;

		//------------------------------------------------------------------
		public SynchronizeWindow()
		{
			InitializeComponent();

			worker = new HgThread();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Synchronize: '{0}'", WorkingDir);

			UpdateAfterPull = true;

			var hg = new Hg();
			paths = hg.GetPaths(WorkingDir);

			if (paths.Count > 0)
			{
				comboBoxPaths.IsEnabled = true;
				comboBoxPaths.DataContext = paths;
				comboBoxPaths.SelectedIndex = 0;
				comboBoxPaths.Focus();
			}
			else
			{
				comboBoxPaths.IsEnabled = false;
				textBox.Focus();
			}
		}

		//------------------------------------------------------------------
		private string GetSelectedRepository()
		{
			if (comboBoxPaths.SelectedItem != null)
			{
				var path_alias = comboBoxPaths.SelectedItem as PathAlias;
				if (path_alias != null)
					return path_alias.Alias;
			}

			return comboBoxPaths.Text;
		}

		//------------------------------------------------------------------
		private string GetTargetRevision()
		{
			return textBoxRevision.Text;
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
			textBox.Text = "";
			Worker_NewMsg("[Incoming started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;
			
			var builder = new StringBuilder();
			builder.Append("-v incoming");

			if (ShowPatch)
				builder.Append(" --patch");

			if (ShowNewestFirst)
				builder.Append(" --newest-first");
	
			if (ShowNoMerges)
				builder.Append(" --no-merges");

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.Append(" -r " + target_revision.Quote());

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(" " + repository.Quote());

			p.Args = builder.ToString();

			worker.Run(p);

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
			textBox.Text = "";
			Worker_NewMsg("[Outgoing started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var builder = new StringBuilder();
			builder.Append("-v outgoing");

			if (ShowPatch)
				builder.Append(" --patch");

			if (ShowNewestFirst)
				builder.Append(" --newest-first");

			if (ShowNoMerges)
				builder.Append(" --no-merges");

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.Append(" -r " + target_revision.Quote());

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(" " + repository.Quote());

			p.Args = builder.ToString();

			worker.Run(p);
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
			textBox.Text = "";
			Worker_NewMsg("[Pull started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var builder = new StringBuilder();
			builder.Append("-v pull");

			if (UpdateAfterPull)
				builder.Append(" -u");

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.Append(" -r " + target_revision.Quote());

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(" " + repository.Quote());

			p.Args = builder.ToString();

			worker.Run(p);
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
			textBox.Text = "";
			Worker_NewMsg("[Push started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var builder = new StringBuilder();
			builder.Append("-v push");

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.Append(" -r " + target_revision.Quote());

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(" " + repository.Quote());

			p.Args = builder.ToString();

			worker.Run(p);
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
			worker.Cancel();
			e.Handled = true;
		}

		//------------------------------------------------------------------
		void Worker_NewMsg(string msg)
		{
			textBox.AppendText(msg + "\n");
			textBox.ScrollToEnd();
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			Worker_NewMsg("");

			switch(completed.Status)
			{
				case HgThreadStatus.Completed:
					{
						var msg = String.Format("[Operation completed. Exit code: {0}]", completed.ExitCode);
						Worker_NewMsg(msg);
						break;
					}
				case HgThreadStatus.Canceled:
					{
						Worker_NewMsg("[Operation canceled]");
						break;
					}
				case HgThreadStatus.Error:
					{
						Worker_NewMsg("[Error: " + completed.ErrorMessage + "]");
						break;
					}
			}

			// Updating commands state (CanExecute)
			CommandManager.InvalidateRequerySuggested();
		}

		//------------------------------------------------------------------
		void Error_Handler(string msg)
		{
			var error_msg = string.Format("[Error: {0}]", msg);
			Dispatcher.Invoke(DispatcherPriority.Normal,
					new Action<string>(Worker_NewMsg), error_msg);
		}

		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal,
				new Action<string>(Worker_NewMsg), msg);
		}

		//------------------------------------------------------------------
		private void Browse_Click(object sender, RoutedEventArgs e)
		{
			using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
			{
				dlg.Description = "Browse for repository...";
				dlg.ShowNewFolderButton = false;
				dlg.SelectedPath = WorkingDir;
				
				var result = dlg.ShowDialog();
				if (result == System.Windows.Forms.DialogResult.OK)
				{
					comboBoxPaths.Text = dlg.SelectedPath;
				}
			}
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//-----------------------------------------------------------------------------
		private void BrowseForBundle_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog();
			dlg.CheckPathExists = true;
			dlg.CheckFileExists = true;
			dlg.InitialDirectory = WorkingDir;
			dlg.Filter = String.Format("{0}|*{1}", "Mercurial bundle files (*.hg)", ".hg");
			dlg.Title = "Browse for mercurial bundle file...";
			dlg.RestoreDirectory = true;

			var result = dlg.ShowDialog(this);
			if (result == true)
			{
				comboBoxPaths.Text = dlg.FileName;
			}
		}
	}
}
