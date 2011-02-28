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
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Threading;
using System;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media;
using HgSccHelper.UI;
using HgSccHelper.UI.RevLog;
using System.Linq;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for RevLogControl.xaml
	/// </summary>
	public partial class RevLogControl : IDisposable
	{
		List<RevLogChangeDesc> revs;
		ObservableCollection<RevLogLinesPair> rev_lines;

		RevLogChangeDescParser rev_log_parser;
		RevLogIteratorParser rev_log_iterator;
		RevLogLinesPairParser rev_log_lines_parser;

		const int BatchSize = 500;

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ReadNextCommand = new RoutedUICommand("Read Next",
			"ReadNext", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ReadAllCommand = new RoutedUICommand("Read All",
			"ReadAll", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ReloadCommand = new RoutedUICommand("Reload",
			"Reload", typeof(RevLogControl),  new InputGestureCollection(
				new[] { new KeyGesture(Key.F5) }));

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		HgThread worker;

		//------------------------------------------------------------------
		Cursor prev_cursor;

		//------------------------------------------------------------------
		RevLogStyleFile revlog_style;

		//------------------------------------------------------------------
		ParentsInfo ParentsInfo { get; set; }

		//-----------------------------------------------------------------------------
		private bool disposed;

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty SelectedChangesetProperty =
			DependencyProperty.Register("SelectedChangeset", typeof(RevLogLinesPair),
			typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		private RevLogLinesPair SelectedChangeset
		{
			get { return (RevLogLinesPair)this.GetValue(SelectedChangesetProperty); }
			set { this.SetValue(SelectedChangesetProperty, value); }
		}

		//------------------------------------------------------------------
		SelectedParentFile SelectedParentFile { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// SHA1 -> BranchInfo map
		/// </summary>
		Dictionary<string, BranchInfo> Branches { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// Tag Name -> TagInfo map
		/// </summary>
		Dictionary<string, TagInfo> Tags { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// Bookmark Name -> BookmarkInfo map
		/// </summary>
		Dictionary<string, BookmarkInfo> Bookmarks { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// SHA1 -> RevLogLinesPair map
		/// </summary>
		Dictionary<string, RevLogLinesPair> rev_log_hash_map;

		Dictionary<ListView, GridViewColumnSorter> files_sorter;

		private List<RevLogChangeDesc> pending_changes;

		private AsyncChangeDesc async_changedesc;

		private AsyncParents async_parents;

		private AsyncBranches async_branches;

		private AsyncTags async_tags;

		private AsyncBookmarks async_bookmarks;

		private bool first_batch;

		private AsyncOperations async_ops;

		private string sha1_to_scroll_on_reload;

		//------------------------------------------------------------------
		public RevLogControl()
		{
			InitializeComponent();

			VirtualizingStackPanel.SetIsVirtualizing(graphView, true);
			VirtualizingStackPanel.SetVirtualizationMode(graphView, VirtualizationMode.Recycling);

			worker = new HgThread();
			revlog_style = new RevLogStyleFile();

			rev_log_iterator = new RevLogIteratorParser();
			rev_log_lines_parser = new RevLogLinesPairParser();

			revs = new List<RevLogChangeDesc>();
			rev_lines = new ObservableCollection<RevLogLinesPair>();
			graphView.ItemsSource = rev_lines;

			UpdateContext = new UpdateContext();

			rev_log_hash_map = new Dictionary<string, RevLogLinesPair>();

			files_sorter = new Dictionary<ListView, GridViewColumnSorter>();
			pending_changes = new List<RevLogChangeDesc>(BatchSize);

			diffColorizer.Complete = new Action<List<string>>(OnDiffColorizer);

			async_changedesc = new AsyncChangeDesc();
			async_changedesc.Completed = new Action<AsyncChangeDescResult>(OnAsyncChangeDesc);

			async_parents = new AsyncParents();
			async_parents.Complete = new Action<ParentsInfo>(OnAsyncParents);

			async_branches = new AsyncBranches();
			async_branches.Complete = new Action<List<BranchInfo>>(OnAsyncBranch);

			async_tags = new AsyncTags();
			async_tags.Complete = new Action<List<TagInfo>>(OnAsyncTags);

			async_bookmarks = new AsyncBookmarks();
			async_bookmarks.Complete = new Action<List<BookmarkInfo>>(OnAsyncBookmarks);

			first_batch = true;
		}

		//-----------------------------------------------------------------------------
		private void OnDiffColorizer(List<string> obj)
		{
			RunningOperations &= ~AsyncOperations.Diff;
		}

		//-----------------------------------------------------------------------------
		private AsyncOperations RunningOperations
		{
			get { return async_ops; }
			set
			{
				if (async_ops != value)
				{
					if (async_ops == AsyncOperations.None)
					{
						prev_cursor = Cursor;
						Cursor = Cursors.Wait;
					}

					async_ops = value;

					if (async_ops == AsyncOperations.None)
					{
						Cursor = prev_cursor;
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncTags(List<TagInfo> tags_list)
		{
			RunningOperations &= ~AsyncOperations.Tags;

			if (tags_list == null)
				return;

			var new_tags = new Dictionary<string, TagInfo>();

			foreach (var tag in tags_list)
			{
				new_tags[tag.Name] = tag;
			}

			foreach (var tag in Tags.Values)
			{
				// removing old tags
				RevLogLinesPair lines_pair;
				if (rev_log_hash_map.TryGetValue(tag.SHA1, out lines_pair))
				{
					var change_desc = lines_pair.Current.ChangeDesc;
					change_desc.Tags.Remove(tag.Name);
				}
			}

			Tags = new_tags;

			foreach (var tag in Tags.Values)
			{
				// adding or updating tags
				RevLogLinesPair lines_pair;
				if (rev_log_hash_map.TryGetValue(tag.SHA1, out lines_pair))
				{
					var change_desc = lines_pair.Current.ChangeDesc;
					if (!change_desc.Tags.Contains(tag.Name))
						change_desc.Tags.Add(tag.Name);
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncBookmarks(List<BookmarkInfo> bookmarks_list)
		{
			RunningOperations &= ~AsyncOperations.Bookmarks;

			if (bookmarks_list == null)
				return;

			var new_bookmarks = new Dictionary<string, BookmarkInfo>();

			foreach (var bookmark in bookmarks_list)
			{
				new_bookmarks[bookmark.Name] = bookmark;
			}

			foreach (var bookmark in Bookmarks.Values)
			{
				// removing old bookmarks
				RevLogLinesPair lines_pair;
				if (rev_log_hash_map.TryGetValue(bookmark.SHA1, out lines_pair))
				{
					var change_desc = lines_pair.Current.ChangeDesc;
					var book_name = bookmark.Name;
					var book = change_desc.Bookmarks.FirstOrDefault(b => b.Name == book_name);
					if (book != null)
						change_desc.Bookmarks.Remove(book);
				}
			}

			Bookmarks = new_bookmarks;

			foreach (var bookmark in Bookmarks.Values)
			{
				// adding or updating bookmarks
				RevLogLinesPair lines_pair;
				if (rev_log_hash_map.TryGetValue(bookmark.SHA1, out lines_pair))
				{
					var change_desc = lines_pair.Current.ChangeDesc;
					var book_name = bookmark.Name;
					if (!change_desc.Bookmarks.Any(b => b.Name == book_name))
						change_desc.Bookmarks.Add(bookmark);
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncBranch(List<BranchInfo> branch_list)
		{
			RunningOperations &= ~AsyncOperations.Branches;

			if (branch_list == null)
				return;

			var new_branches = new Dictionary<string, BranchInfo>();

			foreach (var branch_info in branch_list)
			{
				new_branches[branch_info.SHA1] = branch_info;
				Branches.Remove(branch_info.SHA1);
			}

			foreach (var branch_info in Branches.Values)
			{
				// removing old branch info
				RevLogLinesPair lines_pair;
				if (rev_log_hash_map.TryGetValue(branch_info.SHA1, out lines_pair))
					lines_pair.BranchInfo = null;
			}

			Branches = new_branches;

			foreach (var branch_info in Branches.Values)
			{
				// adding or updating branch info
				RevLogLinesPair lines_pair;
				if (rev_log_hash_map.TryGetValue(branch_info.SHA1, out lines_pair))
					lines_pair.BranchInfo = branch_info;
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncParents(ParentsInfo new_parents)
		{
			RunningOperations &= ~AsyncOperations.Parents;

			if (new_parents == null)
				return;

			if (ParentsInfo != null)
			{
				foreach (var parent in ParentsInfo.Parents)
				{
					RevLogLinesPair lines_pair;
					if (rev_log_hash_map.TryGetValue(parent.SHA1, out lines_pair))
						lines_pair.IsCurrent = false;
				}
			}

			ParentsInfo = new_parents;
			
			if (ParentsInfo != null)
			{
				foreach (var parent in ParentsInfo.Parents)
				{
					RevLogLinesPair lines_pair;
					if (rev_log_hash_map.TryGetValue(parent.SHA1, out lines_pair))
						lines_pair.IsCurrent = true;
				}
			}
		}

		//------------------------------------------------------------------
		public GridView GraphViewGrid
		{
			get { return graphViewGrid; }
		}

		//------------------------------------------------------------------
		private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			Tags = new Dictionary<string, TagInfo>();
			Branches = new Dictionary<string, BranchInfo>();
			Bookmarks = new Dictionary<string, BookmarkInfo>();

			int diff_width;
			Cfg.Get(RevLogWindow.CfgPath, DiffColorizerControl.DiffWidth, out diff_width, DiffColorizerControl.DefaultWidth);
			diffColorizer.Width = diff_width;

			int diff_visible;
			Cfg.Get(RevLogWindow.CfgPath, DiffColorizerControl.DiffVisible, out diff_visible, 1);
			expanderDiff.IsExpanded = (diff_visible != 0);

			if (WorkingDir != null)
			{
				RunRevLogThread(WorkingDir, "", BatchSize);
			}

			graphView.Focus();
		}

		//-----------------------------------------------------------------------------
		void OnAsyncChangeDesc(AsyncChangeDescResult result)
		{
			RunningOperations &= ~AsyncOperations.ChangeDesc;

			if (result == null)
				return;

			if (	graphView.SelectedItems.Count == 1
				&& result.Changeset == graphView.SelectedItem)
			{
				SelectedChangeset = result.Changeset;

				var parents_diff = result.ParentFiles;

				tabParentsDiff.ItemsSource = parents_diff;
				if (parents_diff.Count > 0)
				{
					var first_parent = parents_diff[0];
					first_parent.IsSelected = true;

					foreach (var parent in parents_diff)
					{
						if (parent.Files.Count > 0)
							parent.Files[0].IsSelected = true;
					}
				}
			}
		}

		//------------------------------------------------------------------
		private void graphView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			diffColorizer.Clear(); 
			SelectedChangeset = null;
			tabParentsDiff.ItemsSource = null;

			if (graphView.SelectedItems.Count == 1)
			{
				RunningOperations |= AsyncOperations.ChangeDesc;

				var rev_pair = (RevLogLinesPair)graphView.SelectedItem;
				async_changedesc.Run(WorkingDir, rev_pair);
			}
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (!disposed)
			{
				disposed = true;

				async_changedesc.Cancel();
				async_changedesc.Dispose();

				async_parents.Cancel();
				async_parents.Dispose();

				async_branches.Cancel();
				async_branches.Dispose();

				async_tags.Cancel();
				async_tags.Dispose();

				async_bookmarks.Cancel();
				async_bookmarks.Dispose();

				worker.Cancel();
				worker.Dispose();
				revlog_style.Dispose();

				Cfg.Set(RevLogWindow.CfgPath, DiffColorizerControl.DiffVisible, expanderDiff.IsExpanded ? 1 : 0);
				if (!Double.IsNaN(diffColorizer.Width))
				{
					int diff_width = (int)diffColorizer.Width;
					if (diff_width > 0)
						Cfg.Set(RevLogWindow.CfgPath, DiffColorizerControl.DiffWidth, diff_width);
				}
			}
		}

		//------------------------------------------------------------------
		private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
		{
			Dispose();
		}

		//------------------------------------------------------------------
		private void DiffPrevious_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (SelectedParentFile != null)
			{
				if (SelectedParentFile.FileInfo.Status == HgFileStatus.Added
					&& !String.IsNullOrEmpty(SelectedParentFile.FileInfo.CopiedFrom))
				{
					e.CanExecute = true;
				}

				if (SelectedParentFile.FileInfo.Status == HgFileStatus.Modified)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffPrevious_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
		{
			e.Handled = true;

			var parent_diff = (ParentFilesDiff)tabParentsDiff.SelectedItem;
			var file_info = SelectedParentFile.FileInfo;

			try
			{
				var hg = new Hg();

				var child_file = file_info.File;
				var parent_file = file_info.File;
				if (!String.IsNullOrEmpty(file_info.CopiedFrom))
					parent_file = file_info.CopiedFrom;

				hg.Diff(WorkingDir, parent_file, parent_diff.Desc.SHA1,
					child_file, SelectedChangeset.Current.ChangeDesc.SHA1);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
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

			var args = new HgArgsBuilder();
			args.Append("log");
			args.AppendDebug();
			args.AppendVerbose();
	
			if (max_count != 0)
			{
				args.Append("-l");
				args.Append(max_count.ToString());
			}

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendStyle(revlog_style.FileName);
	
			p.Args = args.ToString();

			rev_log_parser = new RevLogChangeDescParser();
			pending_changes.Clear();

			RunningOperations |= AsyncOperations.RevLog;
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

			if (SelectedParentFile != null)
					e.CanExecute = true;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var parent_diff = (ParentFilesDiff)tabParentsDiff.SelectedItem;
			var file_info = SelectedParentFile.FileInfo;

			var wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = SelectedChangeset.Current.ChangeDesc.SHA1;
			wnd.FileName = file_info.File;
			wnd.Owner = Window.GetWindow(this);

			wnd.UpdateContext.Cache = BuildUpdateContextCache();
			wnd.UpdateContext.Cache.TargetRevision = SelectedChangeset.Current.ChangeDesc;

			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);

			if (wnd.UpdateContext.IsTagsChanged && wnd.UpdateContext.IsParentChanged)
			{
				// There was non-local tag, need to reload graph
				ReloadChangeLog();
			}
			else
			{
				if (wnd.UpdateContext.IsTagsChanged)
					HandleTagsChanges();

				if (wnd.UpdateContext.IsParentChanged)
					HandleParentChange();

				if (wnd.UpdateContext.IsBranchChanged)
					HandleBranchChanges();

				if (wnd.UpdateContext.IsBookmarksChanged)
					HandleBookmarksChanged();
			}
		}

		//------------------------------------------------------------------
		private void Annotate_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (SelectedParentFile != null)
				e.CanExecute = true;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Annotate_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var parent_diff = (ParentFilesDiff)tabParentsDiff.SelectedItem;
			var file_info = SelectedParentFile.FileInfo;

			var wnd = new AnnotateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = SelectedChangeset.Current.ChangeDesc.SHA1;
			wnd.FileName = file_info.File;
			wnd.Owner = Window.GetWindow(this);

			wnd.UpdateContext.Cache = BuildUpdateContextCache();
			wnd.UpdateContext.Cache.TargetRevision = SelectedChangeset.Current.ChangeDesc;

			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);

			if (wnd.UpdateContext.IsTagsChanged && wnd.UpdateContext.IsParentChanged)
			{
				// There was non-local tag, need to reload graph
				ReloadChangeLog();
			}
			else
			{
				if (wnd.UpdateContext.IsTagsChanged)
					HandleTagsChanges();

				if (wnd.UpdateContext.IsParentChanged)
					HandleParentChange();

				if (wnd.UpdateContext.IsBranchChanged)
					HandleBranchChanges();

				if (wnd.UpdateContext.IsBookmarksChanged)
					HandleBookmarksChanged();
			}
		}

		//------------------------------------------------------------------
		private void ViewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (SelectedParentFile != null)
				e.CanExecute = true;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ViewFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var parent_diff = (ParentFilesDiff)tabParentsDiff.SelectedItem;
			var file_info = SelectedParentFile.FileInfo;

			var hg = new Hg();
			if (file_info.Status == HgFileStatus.Removed)
				hg.ViewFile(WorkingDir, file_info.File, parent_diff.Desc.Rev.ToString());
			else
				hg.ViewFile(WorkingDir, file_info.File, SelectedChangeset.Current.ChangeDesc.SHA1);
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
			if (pending_changes.Count != 0)
			{
				var changes = pending_changes;
				pending_changes = new List<RevLogChangeDesc>(BatchSize);

				Worker_NewRevLogChangeDescBatch(changes);
			}

			if (sha1_to_scroll_on_reload != null)
			{
				sha1_to_scroll_on_reload = null;
				if (graphView.SelectedItem != null)
					graphView.ScrollIntoView(graphView.SelectedItem);
			}
			RunningOperations &= ~AsyncOperations.RevLog;

			// Updating commands state (CanExecute)
			CommandManager.InvalidateRequerySuggested();
		}

		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (worker.CancellationPending)
				return;

			var change_desc = rev_log_parser.ParseLine(msg);
			if (change_desc != null)
			{
				pending_changes.Add(change_desc);
				if (pending_changes.Count == BatchSize)
				{
					var changes = pending_changes;
					pending_changes = new List<RevLogChangeDesc>(BatchSize);

					Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,
						new Action<List<RevLogChangeDesc>>(Worker_NewRevLogChangeDescBatch), changes);
				}
			}
		}

		//------------------------------------------------------------------
		void Worker_NewRevLogChangeDescBatch(List<RevLogChangeDesc> changes)
		{
			if (first_batch)
			{
				first_batch = false;

				HandleTagsChanges();
				HandleBranchChanges();
				HandleParentChange();
				HandleBookmarksChanged();
			}

			foreach (var change_desc in changes)
			{
				Worker_NewRevLogChangeDesc(change_desc);
			}
		}


		//------------------------------------------------------------------
		void Worker_NewRevLogChangeDesc(RevLogChangeDesc change_desc)
		{
			revs.Add(change_desc);
			var new_lines_pair = rev_log_lines_parser.ParseLogLines(
				rev_log_iterator.ParseChangeDesc(change_desc));

			var sha1 = new_lines_pair.Current.ChangeDesc.SHA1;

			if (ParentsInfo != null)
			{
				foreach (var parent in ParentsInfo.Parents)
				{
					if (parent.SHA1 == sha1)
					{
						new_lines_pair.IsCurrent = true;
						break;
					}
				}
			}

			BranchInfo branch_info;
			if (Branches.TryGetValue(sha1, out branch_info))
				new_lines_pair.BranchInfo = branch_info;

			rev_lines.Add(new_lines_pair);
			rev_log_hash_map[new_lines_pair.Current.ChangeDesc.SHA1] = new_lines_pair;

			if (graphView.SelectedIndex == -1 && graphView.Items.Count > 0)
				graphView.SelectedIndex = 0;

			if (sha1 == sha1_to_scroll_on_reload)
			{
				graphView.SelectedIndex = graphView.Items.Count - 1;
			}
		}

		//------------------------------------------------------------------
		private void Update_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (SelectedChangeset != null)
				e.CanExecute = true;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private UpdateContextCache BuildUpdateContextCache()
		{
			var cache = new UpdateContextCache();

			if ((RunningOperations & AsyncOperations.Parents) != AsyncOperations.Parents)
				cache.ParentsInfo = ParentsInfo;

			if ((RunningOperations & AsyncOperations.Tags) != AsyncOperations.Tags)
				cache.Tags = new List<TagInfo>(Tags.Values);

			if ((RunningOperations & AsyncOperations.Branches) != AsyncOperations.Branches)
				cache.Branches = new List<BranchInfo>(Branches.Values);

			if ((RunningOperations & AsyncOperations.Bookmarks) != AsyncOperations.Bookmarks)
				cache.Bookmarks = new List<BookmarkInfo>(Bookmarks.Values);

			return cache;
		}

		//------------------------------------------------------------------
		private void Update_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			UpdateWindow wnd = new UpdateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = SelectedChangeset.Current.ChangeDesc.SHA1;

			wnd.UpdateContext.Cache = BuildUpdateContextCache();
			wnd.UpdateContext.Cache.TargetRevision = SelectedChangeset.Current.ChangeDesc;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanged();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void Grep_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Grep_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new GrepWindow();
			wnd.WorkingDir = WorkingDir;

			wnd.UpdateContext.Cache = BuildUpdateContextCache();
	
			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanged();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//-----------------------------------------------------------------------------
		private void Archive_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (SelectedChangeset != null)
				e.CanExecute = true;

			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void Archive_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new ArchiveWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.ArchiveRevision = SelectedChangeset.Current.ChangeDesc.Rev.ToString();

			wnd.UpdateContextCache = BuildUpdateContextCache();
			wnd.UpdateContextCache.TargetRevision = SelectedChangeset.Current.ChangeDesc;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();
		}

		//------------------------------------------------------------------
		private void HandleParentChange()
		{
			RunningOperations |= AsyncOperations.Parents;
			async_parents.RunAsync(WorkingDir, "");
		}

		//------------------------------------------------------------------
		private void HandleBranchChanges()
		{
			RunningOperations |= AsyncOperations.Branches;
			async_branches.RunAsync(WorkingDir, HgBranchesOptions.Closed);
		}

		//------------------------------------------------------------------
		private void HandleTagsChanges()
		{
			RunningOperations |= AsyncOperations.Tags;
			async_tags.RunAsync(WorkingDir);
		}

		//------------------------------------------------------------------
		private void HandleBookmarksChanged()
		{
			RunningOperations |= AsyncOperations.Bookmarks;
			async_bookmarks.RunAsync(WorkingDir);
		}

		//------------------------------------------------------------------
		private void Tags_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (SelectedChangeset != null)
				e.CanExecute = true;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Tags_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			TagsWindow wnd = new TagsWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = SelectedChangeset.Current.ChangeDesc.Rev.ToString();

			wnd.UpdateContext.Cache = BuildUpdateContextCache();
			wnd.UpdateContext.Cache.TargetRevision = SelectedChangeset.Current.ChangeDesc;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);

			if (wnd.UpdateContext.IsTagsChanged && wnd.UpdateContext.IsParentChanged)
			{
				// There was non-local tag, need to reload graph
				ReloadChangeLog();
			}
			else
			{
				if (wnd.UpdateContext.IsTagsChanged)
					HandleTagsChanges();

				if (wnd.UpdateContext.IsParentChanged)
					HandleParentChange();

				if (wnd.UpdateContext.IsBranchChanged)
					HandleBranchChanges();

				if (wnd.UpdateContext.IsBookmarksChanged)
					HandleBookmarksChanged();
			}
		}

		//------------------------------------------------------------------
		private void Bookmarks_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (SelectedChangeset != null)
				e.CanExecute = true;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Bookmarks_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new BookmarksWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = SelectedChangeset.Current.ChangeDesc.Rev.ToString();

			wnd.UpdateContext.Cache = BuildUpdateContextCache();
			wnd.UpdateContext.Cache.TargetRevision = SelectedChangeset.Current.ChangeDesc;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBranchChanged)
				HandleBranchChanges();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanged();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void Merge_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (SelectedChangeset != null && ParentsInfo != null)
			{
				if (ParentsInfo.Parents.Count == 1)
				{
					if (SelectedChangeset.Current.ChangeDesc.SHA1 != ParentsInfo.SHA1)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Merge_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new MergeWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = SelectedChangeset.Current.ChangeDesc.SHA1;

			wnd.UpdateContext.Cache = BuildUpdateContextCache();
			wnd.UpdateContext.Cache.TargetRevision = SelectedChangeset.Current.ChangeDesc;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBranchChanged)
				HandleBranchChanges();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanged();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void Bundle_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (graphView != null)
			{
				if (graphView.SelectedItems.Count == 1 || graphView.SelectedItems.Count == 2)
					e.CanExecute = true;
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Bundle_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			string base_rev = "null";
			string target_rev = "tip";

			if (graphView.SelectedItems.Count == 1)
			{
				var change = (RevLogLinesPair)graphView.SelectedItem;
				base_rev = change.Current.ChangeDesc.Rev.ToString();
			}

			if (graphView.SelectedItems.Count == 2)
			{
				var change1 = (RevLogLinesPair)graphView.SelectedItems[0];
				var change2 = (RevLogLinesPair)graphView.SelectedItems[1];

				var rev1 = change1.Current.ChangeDesc.Rev;
				var rev2 = change2.Current.ChangeDesc.Rev;

				if (rev1 < rev2)
				{
					base_rev = rev1.ToString();
					target_rev = rev2.ToString();
				}
				else
				{
					base_rev = rev2.ToString();
					target_rev = rev1.ToString();
				}
			}
			

			var wnd = new BundleWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.BaseRevision = base_rev;
			wnd.TargetRevision = target_rev;

			wnd.UpdateContextCache = BuildUpdateContextCache();
	
			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();
		}

		//------------------------------------------------------------------
		private void Rebase_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (graphView != null & worker != null && !worker.IsBusy)
			{
				if (HgExtensionsCache.Instance.IsExtensionEnabled(HgExtension.Rebase))
				{
					if (graphView.SelectedItems.Count == 1 || graphView.SelectedItems.Count == 2)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Rebase_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			string src_rev = "";
			string dest_rev = "";

			if (graphView.SelectedItems.Count == 1)
			{
				var change = (RevLogLinesPair)graphView.SelectedItem;
				src_rev = change.Current.ChangeDesc.Rev.ToString();
			}

			if (graphView.SelectedItems.Count == 2)
			{
				var change1 = (RevLogLinesPair)graphView.SelectedItems[0];
				var change2 = (RevLogLinesPair)graphView.SelectedItems[1];

				var rev1 = change1.Current.ChangeDesc.Rev;
				var rev2 = change2.Current.ChangeDesc.Rev;

				src_rev = rev1.ToString();
				dest_rev = rev2.ToString();
			}

			var wnd = new RebaseWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.SourceRevision = src_rev;
			wnd.DestinationRevision = dest_rev;

			wnd.UpdateContextCache = BuildUpdateContextCache();

			wnd.Owner = Window.GetWindow(this);
			var result = wnd.ShowDialog();

			if (result == true)
			{
				UpdateContext.IsBookmarksChanged = true;
				UpdateContext.IsParentChanged = true;
				UpdateContext.IsTagsChanged = true;
				UpdateContext.IsBranchChanged = true;
				UpdateContext.IsCommited = true;

				ReloadChangeLog();
			}
		}

		//------------------------------------------------------------------
		private void listFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SelectedParentFile = null;

			var parent_diff = tabParentsDiff.SelectedItem as ParentFilesDiff;
			var list_view = e.OriginalSource as ListView;

			if (parent_diff != null && list_view != null)
			{
				if (list_view.SelectedItems.Count == 1)
				{
					SelectedParentFile = new SelectedParentFile
					{
						FileInfo = ((ParentDiffHgFileInfo)list_view.SelectedItem).FileInfo,
						ParentFilesDiff = parent_diff
					};

					ShowFileDiff();
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void ShowFileDiff()
		{
			if (diffColorizer == null)
				return;
			
			if (!expanderDiff.IsExpanded)
				return;

			diffColorizer.Clear();

			if (SelectedParentFile == null)
				return;

			var parent_diff = (ParentFilesDiff)tabParentsDiff.SelectedItem;

			RunningOperations |= AsyncOperations.Diff;

			diffColorizer.RunHgDiffAsync(WorkingDir, SelectedParentFile.FileInfo.File,
				parent_diff.Desc.SHA1,
				SelectedChangeset.Current.ChangeDesc.SHA1);
		}

		//------------------------------------------------------------------
		private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			textChangeDesc.Height = changeDescRow.Height.Value;
		}

		//------------------------------------------------------------------
		private void DiffGridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			if (diffColorizer.Width > e.HorizontalChange)
				diffColorizer.Width -= e.HorizontalChange;
			else
				diffColorizer.Width = 0;
		}

		//------------------------------------------------------------------
		void GridViewColumnHeaderClickedHandler(object sender,
												RoutedEventArgs e)
		{
			GridViewColumnSorter column_sorter;
			ListView list_view = sender as ListView;
			if (list_view != null)
			{
				if (!files_sorter.TryGetValue(list_view, out column_sorter))
				{
					column_sorter = new GridViewColumnSorter(list_view);
					files_sorter[list_view] = column_sorter;
				}

				column_sorter.GridViewColumnHeaderClickedHandler(sender, e);
			}
		}

		//-----------------------------------------------------------------------------
		private void expanderDiff_Expanded(object sender, RoutedEventArgs e)
		{
			ShowFileDiff();
		}

		//-----------------------------------------------------------------------------
		private void expanderDiff_Collapsed(object sender, RoutedEventArgs e)
		{
			diffColumn.Width = new GridLength(0, GridUnitType.Auto);
			diffColorizer.Clear();
		}

		//------------------------------------------------------------------
		private void Reload_CanExecute(object sender, CanExecuteRoutedEventArgs e)
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
		private void Reload_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			ReloadChangeLog();
			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void ReloadChangeLog()
		{
			// TODO: Remember current revision and scroll to it after reload
			sha1_to_scroll_on_reload = null;
			if (SelectedChangeset != null)
				sha1_to_scroll_on_reload = SelectedChangeset.Current.ChangeDesc.SHA1;

			rev_log_iterator = new RevLogIteratorParser();
			rev_log_lines_parser = new RevLogLinesPairParser();

			revs.Clear();
			rev_lines.Clear();
			rev_log_hash_map.Clear();

			first_batch = true;
			RunRevLogThread(WorkingDir, "", BatchSize);
		}
	}

	//==================================================================
	class ParentFilesDiff : DependencyObject
	{
		public RevLogChangeDesc Desc { get; set; }
		public List<ParentDiffHgFileInfo> Files { get; set; }

		//-----------------------------------------------------------------------------
		public string HeaderString
		{
			get
			{
				return String.Format("Diff with Parent {0} ({1})",
					Desc.Rev, Desc.SHA1.ShortSHA1());
			}
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsSelectedProperty =
			DependencyProperty.Register("IsSelected", typeof(bool),
			typeof(ParentFilesDiff));

		//-----------------------------------------------------------------------------
		public bool IsSelected
		{
			get { return (bool)this.GetValue(IsSelectedProperty); }
			set { this.SetValue(IsSelectedProperty, value); }
		}
	}

	//=============================================================================
	class ParentDiffHgFileInfo : DependencyObject
	{
		//-----------------------------------------------------------------------------
		public HgFileInfo FileInfo { get; set; }

		//-----------------------------------------------------------------------------
		public HgFileStatus Status { get { return FileInfo.Status; } }

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsSelectedProperty =
			DependencyProperty.Register("IsSelected", typeof(bool),
			typeof(ParentDiffHgFileInfo));

		//-----------------------------------------------------------------------------
		public bool IsSelected
		{
			get { return (bool)this.GetValue(IsSelectedProperty); }
			set { this.SetValue(IsSelectedProperty, value); }
		}
	}

	//==================================================================
	class SelectedParentFile
	{
		public ParentFilesDiff ParentFilesDiff { get; set; }
		public HgFileInfo FileInfo { get; set; }
	}

	//-----------------------------------------------------------------------------
	[Flags]
	enum AsyncOperations
	{
		None			= 0x0000,
		Identify		= 0x0001,
		Tags			= 0x0002,
		Branches		= 0x0004,
		RevLog			= 0x0008,
		ChangeDesc		= 0x0010,
		Diff			= 0x0020,
		Grep			= 0x0040,
		Annotate		= 0x0080,
		Synchronize		= 0x0100,
		Clone			= 0x0200,
		Merge			= 0x0400,
		ResolveList		= 0x0800,
		Status			= 0x1000,
		BranchName		= 0x2000,
		Bookmarks		= 0x4000,
		Parents			= 0x8000
	}
}
