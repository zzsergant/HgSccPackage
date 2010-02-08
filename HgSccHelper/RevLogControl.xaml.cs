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

using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Threading;
using System;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Text;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for RevLogControl.xaml
	/// </summary>
	public partial class RevLogControl : UserControl
	{
		List<RevLogChangeDesc> revs;
		ObservableCollection<RevLogLinesPair> rev_lines;

		RevLogChangeDescParser rev_log_parser;
		RevLogIteratorParser rev_log_iterator;
		RevLogLinesPairParser rev_log_lines_parser;

		DispatcherTimer timer;

		const int BatchSize = 500;

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand FileHistoryCommand = new RoutedUICommand("File History",
			"FileHistory", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand UpdateCommand = new RoutedUICommand("Update to Revision",
			"Update", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand TagsCommand = new RoutedUICommand("Add/Remove Tag",
			"Tags", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand MergeCommand = new RoutedUICommand("Merge",
			"Merge", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ReadNextCommand = new RoutedUICommand("Read Next",
			"ReadNext", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ReadAllCommand = new RoutedUICommand("Read All",
			"ReadAll", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand StopCommand = new RoutedUICommand("Stop",
			"Stop", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		HgThread worker;

		//------------------------------------------------------------------
		Cursor prev_cursor;

		//------------------------------------------------------------------
		public bool IsUpdated { get; private set; }

		//------------------------------------------------------------------
		RevLogStyleFile revlog_style;

		//------------------------------------------------------------------
		IdentifyInfo CurrentRevision { get; set; }

		//------------------------------------------------------------------
		C5.HashDictionary<string, BranchInfo> Branches { get; set; }

		//------------------------------------------------------------------
		public RevLogControl()
		{
			InitializeComponent();

			VirtualizingStackPanel.SetIsVirtualizing(graphView, true);
			VirtualizingStackPanel.SetVirtualizationMode(graphView, VirtualizationMode.Recycling);

			VirtualizingStackPanel.SetIsVirtualizing(listViewFiles, true);
			VirtualizingStackPanel.SetVirtualizationMode(listViewFiles, VirtualizationMode.Recycling);

			worker = new HgThread();
			revlog_style = new RevLogStyleFile();

			rev_log_iterator = new RevLogIteratorParser();
			rev_log_lines_parser = new RevLogLinesPairParser();

			revs = new List<RevLogChangeDesc>();
			rev_lines = new ObservableCollection<RevLogLinesPair>();
			graphView.ItemsSource = rev_lines;
		}

		//------------------------------------------------------------------
		private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			var hg = new Hg();
			CurrentRevision = hg.Identify(WorkingDir);
			if (CurrentRevision == null)
				return;

			Branches = new C5.HashDictionary<string, BranchInfo>();
			foreach (var branch in hg.Branches(WorkingDir))
			{
				Branches[branch.SHA1] = branch;
			}

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(50);
			timer.Tick += OnTimerTick;

			if (WorkingDir != null)
			{
				RunRevLogThread(WorkingDir, "", BatchSize);
			}

			graphView.Focus();
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();

			if (graphView.SelectedItems.Count == 1)
			{
				var rev_pair = (RevLogLinesPair)graphView.SelectedItem;
				var hg = new Hg();
				var cs_list = hg.ChangesFull(WorkingDir, "", rev_pair.Current.ChangeDesc.SHA1);
				if (cs_list.Count == 1)
					listViewFiles.DataContext = cs_list[0];

				return;
			}
		}

		//------------------------------------------------------------------
		private void graphView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			listViewFiles.DataContext = null;
			timer.Stop();

			if (graphView.SelectedItems.Count == 1)
			{
				timer.Start();
			}
		}

		//------------------------------------------------------------------
		private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
		{
			timer.Tick -= OnTimerTick;

			if (StopCommand.CanExecute(sender, e.Source as IInputElement))
				StopCommand.Execute(sender, e.Source as IInputElement);

			worker.Dispose();
			revlog_style.Dispose();
		}

		//------------------------------------------------------------------
		private void DiffPrevious_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listViewFiles != null && listViewFiles.SelectedItems.Count == 1)
			{
				var file_info = (FileInfo)listViewFiles.SelectedItem;
				if (file_info.Status == FileStatus.Modified)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffPrevious_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
		{
			e.Handled = true;

			var cs = (ChangeDesc)listViewFiles.DataContext;
			var file_info = (FileInfo)listViewFiles.SelectedItem;

			try
			{
				var hg = new Hg();
				hg.Diff(WorkingDir, file_info.Path, cs.Rev - 1, file_info.Path, cs.Rev);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}
		}

		//------------------------------------------------------------------
		private void ListViewFiles_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (DiffPreviousCommand != null)
			{
				if (DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private void ReadNext_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (WorkingDir != null && revs != null && revs.Count > 0)
			{
				if (worker != null && !worker.IsBusy)
					if (revs[revs.Count - 1].Rev != 0)
					{
						e.CanExecute = true;
					}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ReadNext_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var start_rev = revs[revs.Count - 1].Rev - 1;
			var stop_rev = Math.Max(0, start_rev - BatchSize);
			var rev = string.Format("{0}:{1}", start_rev, stop_rev);

			RunRevLogThread(WorkingDir, rev, 0);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void RunRevLogThread(string work_dir, string rev, int max_count)
		{
			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var args = new StringBuilder();
			args.Append("log");
			args.Append(" --debug");
			args.Append(" -v");
	
			if (max_count != 0)
				args.Append(" -l " + max_count);

			if (rev.Length > 0)
				args.Append(" -r " + rev);

			args.Append(" --style " + revlog_style.FileName.Quote());

			p.Args = args.ToString();

			rev_log_parser = new RevLogChangeDescParser();
			
			prev_cursor = Cursor;
			Cursor = Cursors.Wait;
			
			worker.Run(p);
		}

		//------------------------------------------------------------------
		private void ReadAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			ReadNext_CanExecute(sender, e);
		}

		//------------------------------------------------------------------
		private void ReadAll_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var start_rev = revs[revs.Count - 1].Rev - 1;
			var rev = string.Format("{0}:{1}", start_rev, 0);

			RunRevLogThread(WorkingDir, rev, 0);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listViewFiles != null && (listViewFiles.SelectedItems.Count == 1))
				e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (ChangeDesc)listViewFiles.DataContext;
			var file_info = (FileInfo)listViewFiles.SelectedItem;

			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = change.Rev.ToString();
			wnd.FileName = file_info.Path;
			wnd.Owner = Window.GetWindow(this);

			wnd.ShowDialog();
			IsUpdated = IsUpdated || wnd.IsUpdated;
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
		void Worker_Completed(HgThreadResult completed)
		{
			Cursor = prev_cursor;

			// Updating commands state (CanExecute)
			CommandManager.InvalidateRequerySuggested();
		}

		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			var change_desc = rev_log_parser.ParseLine(msg);
			if (change_desc != null)
			{
				Dispatcher.Invoke(DispatcherPriority.Normal,
					new Action<RevLogChangeDesc>(Worker_NewRevLogChangeDesc), change_desc);
			}
		}

		//------------------------------------------------------------------
		void Worker_NewRevLogChangeDesc(RevLogChangeDesc change_desc)
		{
			revs.Add(change_desc);
			var new_lines_pair = rev_log_lines_parser.ParseLogLines(
				rev_log_iterator.ParseChangeDesc(change_desc));

			var sha1 = new_lines_pair.Current.ChangeDesc.SHA1;

			foreach (var parent in CurrentRevision.Parents)
			{
				if (parent.SHA1 == sha1)
				{
					new_lines_pair.IsCurrent = true;
					break;
				}
			}

			BranchInfo branch_info;
			if (Branches.Find(sha1, out branch_info))
				new_lines_pair.BranchInfo = branch_info;

			rev_lines.Add(new_lines_pair);

			if (graphView.SelectedIndex == -1 && graphView.Items.Count > 0)
				graphView.SelectedIndex = 0;
		}

		//------------------------------------------------------------------
		private void Update_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listViewFiles != null)
			{
				var change = listViewFiles.DataContext as ChangeDesc;
				if (change != null)
					e.CanExecute = true;
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Update_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (ChangeDesc)listViewFiles.DataContext;

			UpdateWindow wnd = new UpdateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = change.Rev.ToString();

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			IsUpdated = IsUpdated || wnd.IsUpdated;
		}

		//------------------------------------------------------------------
		private void Tags_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listViewFiles != null)
			{
				var change = listViewFiles.DataContext as ChangeDesc;
				if (change != null)
					e.CanExecute = true;
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Tags_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (ChangeDesc)listViewFiles.DataContext;

			TagsWindow wnd = new TagsWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = change.Rev.ToString();

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();
		}

		//------------------------------------------------------------------
		private void Merge_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listViewFiles != null)
			{
				var change = listViewFiles.DataContext as ChangeDesc;
				if (change != null)
				{
					if (CurrentRevision.Parents.Count == 1)
					{
						if (change.SHA1 != CurrentRevision.SHA1)
							e.CanExecute = true;
					}
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Merge_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (ChangeDesc)listViewFiles.DataContext;

			var wnd = new MergeWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = change.Rev.ToString();

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();
		}
	}
}
