﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace HgSccHelper.UI
{
	/// <summary>
	/// Interaction logic for GrepWindow.xaml
	/// </summary>
	public partial class GrepWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		private bool IsFollowRenames
		{
			get { return (bool)GetValue(IsFollowRenamesProperty); }
			set { SetValue(IsFollowRenamesProperty, value); }
		}

		//------------------------------------------------------------------
		public static readonly DependencyProperty IsFollowRenamesProperty =
			DependencyProperty.Register("IsFollowRenames", typeof(bool), typeof(GrepWindow));

		//------------------------------------------------------------------
		private bool IsIgnoreCase
		{
			get { return (bool)GetValue(IsIgnoreCaseProperty); }
			set { SetValue(IsIgnoreCaseProperty, value); }
		}

		//------------------------------------------------------------------
		public static readonly DependencyProperty IsIgnoreCaseProperty =
			DependencyProperty.Register("IsIgnoreCase", typeof(bool), typeof(GrepWindow));

		//------------------------------------------------------------------
		private bool IsShowAllRevisions
		{
			get { return (bool)GetValue(IsShowAllRevisionsProperty); }
			set { SetValue(IsShowAllRevisionsProperty, value); }
		}

		//------------------------------------------------------------------
		public static readonly DependencyProperty IsShowAllRevisionsProperty =
			DependencyProperty.Register("IsShowAllRevisions", typeof(bool), typeof(GrepWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand SearchCommand = new RoutedUICommand("Search",
			"Search", typeof(GrepWindow));

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		//------------------------------------------------------------------
		IdentifyInfo CurrentRevision { get; set; }

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty SelectedChangesetProperty =
			DependencyProperty.Register("SelectedChangeset", typeof(ChangeDesc),
			typeof(GrepWindow));

		//-----------------------------------------------------------------------------
		private ChangeDesc SelectedChangeset
		{
			get { return (ChangeDesc)this.GetValue(SelectedChangesetProperty); }
			set { this.SetValue(SelectedChangesetProperty, value); }
		}

		DeferredCommandExecutor deferred_executor;
		ObservableCollection<GrepLineInfo> grep_lines;
		HgThread worker;
		DispatcherTimer timer;
		Cursor prev_cursor;

		public const string CfgPath = @"GUI\GrepWindow";
		CfgWindowPosition wnd_cfg;

		ObservableCollection<EncodingItem> encodings;

		//------------------------------------------------------------------
		public GrepWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			UpdateContext = new UpdateContext();
			deferred_executor = new DeferredCommandExecutor();

			grep_lines = new ObservableCollection<GrepLineInfo>();
			listLines.ItemsSource = grep_lines;
			worker = new HgThread();

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(50);
			timer.Tick += OnTimerTick;

			encodings = new ObservableCollection<EncodingItem>();
			encodings.Add(new EncodingItem { Name = "Ansi", Encoding = Encoding.Default });
			encodings.Add(new EncodingItem { Name = "Utf8", Encoding = Encoding.UTF8 });
			comboEncodings.ItemsSource = encodings;
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();

			ChangeDesc changeset = null;

			if (listLines.SelectedItems.Count == 1)
			{
				var line_info = (GrepLineInfo)listLines.SelectedItem;
				var changes = Hg.ChangesFull(WorkingDir, line_info.File, line_info.Rev.ToString());
				if (changes.Count == 1)
					changeset = changes[0];
			}

			SelectedChangeset = changeset;
			SynchronizeMatchWithFilesList();
		}

		//------------------------------------------------------------------
		private void SynchronizeMatchWithFilesList()
		{
			var line_info = listLines.SelectedItem as GrepLineInfo;
			if (line_info == null)
				return;

			// Selecting file in the files listview
			if (SelectedChangeset == null)
				return;

			foreach (var file_info in SelectedChangeset.Files)
			{
				if (file_info.Path == line_info.File)
				{
					listViewFiles.SelectedItem = file_info;
					listViewFiles.ScrollIntoView(file_info);
					break;
				}
			}
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			listLinesGrid.LoadCfg(GrepWindow.CfgPath, "ListLinesGrid");
			string encoding_name;
			Cfg.Get(GrepWindow.CfgPath, "encoding", out encoding_name, encodings[0].Name);

			Title = string.Format("Grep: '{0}'", WorkingDir);

			Hg = new Hg();

			CurrentRevision = Hg.Identify(WorkingDir);
			if (CurrentRevision == null)
				return;

			var encoding = encodings.First(enc => enc.Name == encoding_name);
			if (encoding != null)
				comboEncodings.SelectedItem = encoding;
			else
				comboEncodings.SelectedIndex = 0;

			textRegexp.Focus();
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			listLinesGrid.SaveCfg(GrepWindow.CfgPath, "ListLinesGrid");
			var encoding = comboEncodings.SelectedItem as EncodingItem;
			if (encoding != null)
				Cfg.Set(GrepWindow.CfgPath, "encoding", encoding.Name);

			if (Commands.StopCommand.CanExecute(sender, e.Source as IInputElement))
				Commands.StopCommand.Execute(sender, e.Source as IInputElement);

			worker.Dispose();
			deferred_executor.Dispose();
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
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
		private void Search_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (WorkingDir != null)
			{
				if (worker != null && !worker.IsBusy)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Search_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			RunGrepThread(WorkingDir);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void RunGrepThread(string work_dir)
		{
			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var args = new StringBuilder();
			args.Append("grep");
			args.Append(" --line-number");

			if (checkFollowRenames.IsEnabled && checkFollowRenames.IsChecked == true)
				args.Append(" --follow");

			if (IsIgnoreCase)
				args.Append(" --ignore-case");

			if (IsShowAllRevisions)
				args.Append(" --all");

			if (!String.IsNullOrEmpty(textIncludes.Text))
				args.Append(" -I " + textIncludes.Text.Quote());

			if (!String.IsNullOrEmpty(textExcludes.Text))
				args.Append(" -X " + textExcludes.Text.Quote());

			if (!String.IsNullOrEmpty(textRegexp.Text))
				args.Append(" " + textRegexp.Text.Quote());

			p.Args = args.ToString();

			grep_lines.Clear();
			SelectedChangeset = null;

			prev_cursor = Cursor;
			Cursor = Cursors.Wait;

			worker.Run(p);
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
			var parts = msg.Split(new[] { ':' }, 4);
			if (parts.Length == 4)
			{
				int rev;
				if (!int.TryParse(parts[1], out rev))
					return;

				int line;
				if (!int.TryParse(parts[2], out line))
					return;

				// FIXME: Fix unicode byte order mark, that can appear
				// in the first line of unicode file

				var line_info = new ThreadGrepLineInfo
				{
					 Rev = rev,
					 File = parts[0],
					 Line = line,
					 Match = parts[3]
				};

				Dispatcher.Invoke(DispatcherPriority.Normal,
					new Action<ThreadGrepLineInfo>(Worker_NewGrepLine), line_info);
			}
		}

		//------------------------------------------------------------------
		void Worker_NewGrepLine(ThreadGrepLineInfo thread_line)
		{
			var match = thread_line.Match;

			var encoding = comboEncodings.SelectedItem as EncodingItem;
			if (encoding != null && encoding.Encoding != Encoding.Default)
				match = Util.Convert(match, encoding.Encoding, Encoding.Default);

			var line_info = new GrepLineInfo
			{
				Line = thread_line.Line,
				File = thread_line.File,
				Rev = thread_line.Rev,
				MatchInDefaultEncoding = thread_line.Match,
				Match = match
			};

			grep_lines.Add(line_info);

			if (listLines.SelectedIndex == -1 && listLines.Items.Count > 0)
				listLines.SelectedIndex = 0;
		}

		//------------------------------------------------------------------
		private void DiffTwoRevisions(FileHistoryInfo f1, FileHistoryInfo f2)
		{
			if (f1.ChangeDesc.Rev > f2.ChangeDesc.Rev)
			{
				var temp = f2;
				f2 = f1;
				f1 = temp;
			}

			try
			{
				Hg.Diff(WorkingDir, f1.RenameInfo.Path, f1.ChangeDesc.Rev, f2.RenameInfo.Path, f2.ChangeDesc.Rev);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}
		}

		//------------------------------------------------------------------
		private void FilesDiffPrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listViewFiles != null && listViewFiles.SelectedItems.Count == 1)
			{
				var file_info = (FileInfo)listViewFiles.SelectedItem;
				if (file_info.Status == FileStatus.Modified)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FilesDiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = SelectedChangeset;

			deferred_executor.QueueDefferedExecute(() =>
			{
				try
				{
					Hg.Diff(WorkingDir, file_info.Path, cs.Rev - 1, file_info.Path, cs.Rev);
				}
				catch (HgDiffException)
				{
					Util.HandleHgDiffException();
				}
			});

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ListChanges_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (Commands.DiffPreviousCommand != null)
			{
				if (Commands.DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					Commands.DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private void ListViewFiles_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (Commands.DiffPreviousCommand != null)
			{
				if (Commands.DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					Commands.DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private void FileHistory_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listViewFiles != null)
				e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ViewFileHistory(string file, string revision)
		{
			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = revision;
			wnd.FileName = file;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void Annotate_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listViewFiles != null)
				e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Annotate_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_info = (FileInfo)listViewFiles.SelectedItem;

			var wnd = new AnnotateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = SelectedChangeset.Rev.ToString();
			wnd.FileName = file_info.Path;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			ViewFileHistory(file_info.Path, SelectedChangeset.Rev.ToString());
		}

		//------------------------------------------------------------------
		private void ViewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listViewFiles != null)
				e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ViewFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = SelectedChangeset;

			deferred_executor.QueueDefferedExecute(() =>
			{
				var hg = new Hg();
				if (file_info.Status == FileStatus.Removed)
					hg.ViewFile(WorkingDir, file_info.Path, (cs.Rev - 1).ToString());
				else
					hg.ViewFile(WorkingDir, file_info.Path, cs.Rev.ToString());
			});
		}

		//-----------------------------------------------------------------------------
		private void Archive_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = SelectedChangeset != null;
			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void Archive_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new ArchiveWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.ArchiveRevision = SelectedChangeset.Rev.ToString();

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();
		}

		//------------------------------------------------------------------
		private void listLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var line_info = listLines.SelectedItem as GrepLineInfo;
			if (line_info == null)
			{
				SelectedChangeset = null;
				return;
			}

			if (SelectedChangeset != null)
			{
				if (SelectedChangeset.Rev == line_info.Rev)
				{
					SynchronizeMatchWithFilesList();
					return;
				}
			}

			timer.Stop();
			timer.Start();
		}

		//------------------------------------------------------------------
		private void textBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
		    if (e.Key == Key.Return)
		    {
		        if (SearchCommand.CanExecute(sender, e.Source as IInputElement))
					SearchCommand.Execute(sender, e.Source as IInputElement);
		    }
		}

		//------------------------------------------------------------------
		private void textExcludes_TextChanged(object sender, TextChangedEventArgs e)
		{
			checkFollowRenames.IsEnabled = String.IsNullOrEmpty(textExcludes.Text);
		}

		//------------------------------------------------------------------
		private void textIncludes_TextChanged(object sender, TextChangedEventArgs e)
		{
			checkFollowRenames.IsEnabled = String.IsNullOrEmpty(textIncludes.Text);
		}

		//------------------------------------------------------------------
		private void comboEncodings_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var encoding = comboEncodings.SelectedItem as EncodingItem;

			if (encoding != null)
			{
				foreach (var line_info in grep_lines)
					line_info.Match = Util.Convert(line_info.MatchInDefaultEncoding,
						encoding.Encoding, Encoding.Default);
			}
		}
	}

	//==================================================================
	class ThreadGrepLineInfo
	{
		public int Rev { get; set; }
		public string File { get; set; }
		public int Line { get; set; }
		public string Match { get; set; }
	}

	//==================================================================
	class GrepLineInfo : DependencyObject
	{
		public int Rev { get; set; }
		public string File { get; set; }
		public int Line { get; set; }
		public string MatchInDefaultEncoding { get; set; }

		//------------------------------------------------------------------
		public string Match
		{
			get { return (string)GetValue(MatchProperty); }
			set { SetValue(MatchProperty, value); }
		}

		//------------------------------------------------------------------
		public static readonly DependencyProperty MatchProperty =
			DependencyProperty.Register("Match", typeof(string), typeof(GrepLineInfo));
	}

	//==================================================================
	class EncodingItem
	{
		public string Name { get; set; }
		public Encoding Encoding { get; set; }
	}
}