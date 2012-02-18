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

using System.Linq;
using System.Text;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using System;
using System.Windows.Data;
using System.Windows.Threading;
using HgSccHelper.CommandServer;
using HgSccHelper.UI;
using HgSccHelper.UI.RevLog;

namespace HgSccHelper
{
	public partial class FileHistoryWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string FileName { get; set; }

		//------------------------------------------------------------------
		public string Rev { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		HgClient HgClient { get { return UpdateContext.Cache.HgClient; } }

		//------------------------------------------------------------------
		ParentsInfo ParentsInfo { get; set; }

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
		/// SHA1 -> FileHistoryInfo2 map
		/// </summary>
		Dictionary<string, FileHistoryInfo2> file_history_map;

		//------------------------------------------------------------------
		/// <summary>
		/// Bookmark Name -> BookmarkInfo map
		/// </summary>
		Dictionary<string, BookmarkInfo> Bookmarks { get; set; }

		//-----------------------------------------------------------------------------
		private AsyncOperations async_ops;

		//-----------------------------------------------------------------------------
		private Cursor prev_cursor;

		GridViewColumnSorter files_sorter;
		private DispatcherTimer timer;

		public const string CfgPath = @"GUI\FileHistoryWindow";
		CfgWindowPosition wnd_cfg;
		private System.Text.StringBuilder sb = new StringBuilder();

		//------------------------------------------------------------------
		public FileHistoryWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			UpdateContext = new UpdateContext();
			file_history_map = new Dictionary<string, FileHistoryInfo2>();

			files_sorter = new GridViewColumnSorter(listViewFiles);
			diffColorizer.Complete = new Action<List<string>>(OnDiffColorizer);

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(50);
			timer.Tick += TimerOnTick;
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
		private void OnDiffColorizer(List<string> obj)
		{
			RunningOperations &= ~AsyncOperations.Diff;
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			listChangesGrid.LoadCfg(FileHistoryWindow.CfgPath, "ListChangesGrid");
			
			Title = string.Format("File History: '{0}'", FileName);

			int diff_width;
			Cfg.Get(CfgPath, DiffColorizerControl.DiffWidth, out diff_width, DiffColorizerControl.DefaultWidth);
			diffColorizer.Width = diff_width;

			int diff_visible;
			Cfg.Get(CfgPath, DiffColorizerControl.DiffVisible, out diff_visible, 1);
			expanderDiff.IsExpanded = (diff_visible != 0);

			Tags = new Dictionary<string, TagInfo>();
			Branches = new Dictionary<string, BranchInfo>();
			Bookmarks = new Dictionary<string, BookmarkInfo>();

			var files = HgClient.Status(FileName, Rev ?? "");
			if (files.Count == 1
				&& files[0].Status == HgFileStatus.Added
				&& files[0].CopiedFrom != null)
			{
				var file_info = files[0];
				FileName = file_info.CopiedFrom;
			}

			string full_rev_range = "";
			if (!String.IsNullOrEmpty(Rev))
				full_rev_range = String.Format("{0}:0", Rev);

			var tt = Stopwatch.StartNew();
			var rename_parts = TrackRenames(HgClient, FileName, full_rev_range);

			sb.AppendFormat("Custom, time = {0} ms\n", tt.ElapsedMilliseconds);
			OnAsyncChangeDescFull(rename_parts);
		}

		//-----------------------------------------------------------------------------
		internal static List<RenameParts> TrackRenames(HgClient hg_client, string filename, string rev_range)
		{
			// Revs with follow
			var follow_revs = hg_client.RevLogPath(filename, rev_range, 0, true);

			// Map for filenames -> sha1 revlist without follow
			var file_to_revs = new Dictionary<string, HashSet<string>>();

			var parts = new List<RenameParts>();
			filename = filename.Replace('/', '\\');

			while (follow_revs.Count > 0)
			{
				var current = follow_revs[0];

				HashSet<string> no_follow;
				if (!file_to_revs.TryGetValue(filename, out no_follow))
				{
					var no_follow_list = hg_client.RevLogPathSHA1(filename,
						String.Format("{0}:0", current.SHA1),
						0, false);

					no_follow = new HashSet<string>(no_follow_list);
					file_to_revs.Add(filename, no_follow);
				}

				int mismatch_idx = -1;

				for (int i = 0; i < follow_revs.Count; ++i)
				{
					if (!no_follow.Contains(follow_revs[i].SHA1))
					{
						mismatch_idx = i;
						break;
					}

					no_follow.Remove(follow_revs[i].SHA1);
				}

				if (mismatch_idx == -1)
				{
					// No more renames
					mismatch_idx = follow_revs.Count;
				}

				if (mismatch_idx == 0)
				{
					// This should not happen
					break;
				}

				var last_rev = follow_revs[mismatch_idx - 1];

				parts.Add(new RenameParts { FileName = filename, Revs = follow_revs.GetRange(0, mismatch_idx) });
				follow_revs.RemoveRange(0, mismatch_idx);

				if (!hg_client.TrackRename(filename, last_rev.SHA1, out filename))
					break;

				// FIXME: TrackRename returns filename with '/' separator
				filename = filename.Replace('/', '\\');
			}

			return parts;
		}

		//------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			timer.Stop();
			timer.Tick -= TimerOnTick;

			diffColorizer.Dispose();

			Cfg.Set(CfgPath, DiffColorizerControl.DiffVisible, expanderDiff.IsExpanded ? 1 : 0);
			if (!Double.IsNaN(diffColorizer.Width))
			{
				int diff_width = (int)diffColorizer.Width;
				if (diff_width > 0)
					Cfg.Set(CfgPath, DiffColorizerControl.DiffWidth, diff_width);
			}

			listChangesGrid.SaveCfg(FileHistoryWindow.CfgPath, "ListChangesGrid");
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncChangeDescFull(List<RenameParts> parts)
		{
			if (parts.Count == 0)
			{
				Close();
				return;
			}

			var t2 = Stopwatch.StartNew();
			var history = new List<FileHistoryInfo2>();

			int part_idx = 0;
			foreach (var part in parts)
			{
				part_idx++;

				foreach (var change_desc in part.Revs)
				{
					var history_item = new FileHistoryInfo2();
					history_item.ChangeDesc = change_desc;
					history_item.FileName = part.FileName;
					history_item.GroupText = String.Format("[{0}]: {1}", part_idx, part.FileName);

					if (ParentsInfo != null)
					{
						foreach (var parent in ParentsInfo.Parents)
						{
							if (history_item.ChangeDesc.SHA1 == parent.SHA1)
							{
								history_item.IsCurrent = true;
								break;
							}
						}
					}

					BranchInfo branch_info;
					if (Branches.TryGetValue(history_item.ChangeDesc.SHA1, out branch_info))
						history_item.BranchInfo = branch_info;

					file_history_map[history_item.ChangeDesc.SHA1] = history_item;
					history.Add(history_item);
				}
			}

			sb.AppendFormat("Build history {0} ms\n", t2.ElapsedMilliseconds);

			var t3 = Stopwatch.StartNew();
			listChanges.ItemsSource = history;
			if (listChanges.Items.Count > 0)
				listChanges.SelectedIndex = 0;

			listChanges.Focus();
			sb.AppendFormat("Binding {0} ms\n", t3.ElapsedMilliseconds);

			var t4 = Stopwatch.StartNew();

			if (UpdateContext.Cache.Branches != null)
				OnAsyncBranch(UpdateContext.Cache.Branches);
			else
				HandleBranchChanges();

			if (UpdateContext.Cache.Tags != null)
				OnAsyncTags(UpdateContext.Cache.Tags);
			else
				HandleTagsChanges();

			if (UpdateContext.Cache.ParentsInfo != null)
				OnAsyncParents(UpdateContext.Cache.ParentsInfo);
			else
				HandleParentChange();

			if (UpdateContext.Cache.Bookmarks != null)
				OnAsyncBookmarks(UpdateContext.Cache.Bookmarks);
			else
				HandleBookmarksChanges();

			if (parts.Count > 1)
			{
				// Since grouping is effectively disable virtualization,
				// enable it only if there were file renames

				listChanges.GroupStyle.Clear();
				listChanges.GroupStyle.Add((GroupStyle)Resources["GroupStyleForRenames"]);

				var myView = (CollectionView)CollectionViewSource.GetDefaultView(listChanges.ItemsSource);
				var groupDescription = new PropertyGroupDescription("GroupText");
				myView.GroupDescriptions.Clear();
				myView.GroupDescriptions.Add(groupDescription);
			}

			sb.AppendFormat("Other {0} ms\n", t4.ElapsedMilliseconds);

			Logger.WriteLine("FileHistory Times:\n{0}\n", sb.ToString());
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
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					var tag_name = tag.Name;
					var tag_info = change_desc.Tags.FirstOrDefault(t => t.Name == tag_name);
					if (tag_info != null)
						change_desc.Tags.Remove(tag_info);
				}
			}

			Tags = new_tags;

			foreach (var tag in Tags.Values)
			{
				// adding or updating tags
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					var tag_name = tag.Name;

					int pos = change_desc.Tags.FirstIndexOf(t => t.Name == tag_name);
					if (pos != -1)
						change_desc.Tags[pos] = tag;
					else
						change_desc.Tags.Add(tag);
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
				// removing old bookmark
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(bookmark.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
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
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(bookmark.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					var book_name = bookmark.Name;

					int pos = change_desc.Bookmarks.FirstIndexOf(b => b.Name == book_name);
					if (pos != -1)
						change_desc.Bookmarks[pos] = bookmark;
					else
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
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = null;
			}

			Branches = new_branches;

			foreach (var branch_info in Branches.Values)
			{
				// adding or updating branch info
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = branch_info;
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncParents(ParentsInfo new_current)
		{
			RunningOperations &= ~AsyncOperations.Parents;

			if (new_current == null)
				return;

			if (ParentsInfo != null)
			{
				foreach (var parent in ParentsInfo.Parents)
				{
					FileHistoryInfo2 file_history;
					if (file_history_map.TryGetValue(parent.SHA1, out file_history))
						file_history.IsCurrent = false;
				}
			}

			ParentsInfo = new_current;
			if (ParentsInfo != null)
			{
				foreach (var parent in ParentsInfo.Parents)
				{
					FileHistoryInfo2 file_history;
					if (file_history_map.TryGetValue(parent.SHA1, out file_history))
						file_history.IsCurrent = true;
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

			if (listViewFiles.SelectedItems.Count == 1)
			{
				var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
				var file_info = (HgFileInfo)listViewFiles.SelectedItem;
				var cs = file_history.ChangeDesc;

				RunningOperations |= AsyncOperations.Diff;
				string rev2 = cs.SHA1;
				string rev1 = (cs.Rev - 1).ToString();
				if (cs.Rev == 0)
					rev1 = "null";

				diffColorizer.RunHgDiffAsync(WorkingDir, file_info.File, rev1, rev2);
			}
		}

		//------------------------------------------------------------------
		private void DiffGridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			if (diffColorizer.Width > e.HorizontalChange)
				diffColorizer.Width -= e.HorizontalChange;
			else
				diffColorizer.Width = 0;
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
		private void HistoryDiffPrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listChanges.SelectedItems.Count == 1)
			{
				if (listChanges.SelectedIndex != (listChanges.Items.Count - 1))
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void HistoryDiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var f1 = (FileHistoryInfo2)listChanges.Items[listChanges.SelectedIndex];
			var f2 = (FileHistoryInfo2)listChanges.Items[listChanges.SelectedIndex + 1];

			DiffTwoRevisions(f1, f2);

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffTwoRevisions(FileHistoryInfo2 f1, FileHistoryInfo2 f2)
		{
			if (f1.ChangeDesc.Rev > f2.ChangeDesc.Rev)
			{
				var temp = f2;
				f2 = f1;
				f1 = temp;
			}

			try
			{
				HgClient.Diff(f1.FileName, f1.ChangeDesc.Rev, f2.FileName, f2.ChangeDesc.Rev);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}
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
		private void FilesDiffPrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listViewFiles.SelectedItems.Count == 1)
			{
				var file_info = (HgFileInfo)listViewFiles.SelectedItem;
				if (file_info.Status == HgFileStatus.Modified)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FilesDiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var file_info = (HgFileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			try
			{
				HgClient.Diff(file_info.File, cs.Rev - 1, file_info.File, cs.Rev);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}

			e.Handled = true;
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
		private UpdateContextCache BuildUpdateContextCache()
		{
			var cache = new UpdateContextCache();
			cache.HgClient = UpdateContext.Cache.HgClient;

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
		private void FileHistory_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var file_info = (HgFileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			var wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = cs.Rev.ToString();
			wnd.FileName = file_info.File;

			wnd.UpdateContext.Cache = BuildUpdateContextCache();

			// FIXME:
			wnd.Owner = Window.GetWindow(this);

			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBranchChanged)
				HandleBranchChanges();

			if (wnd.UpdateContext.IsTagsChanged)
				HandleTagsChanges();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanges();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void Annotate_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Annotate_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var file_info = (HgFileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			var wnd = new AnnotateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = cs.Rev.ToString();
			wnd.FileName = file_info.File;

			wnd.UpdateContext.Cache = BuildUpdateContextCache();

			wnd.Owner = Window.GetWindow(this);

			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBranchChanged)
				HandleBranchChanges();

			if (wnd.UpdateContext.IsTagsChanged)
				HandleTagsChanges();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanges();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void ViewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ViewFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var file_info = (HgFileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			if (file_info.Status == HgFileStatus.Removed)
				HgClient.ViewFile(file_info.File, (cs.Rev - 1).ToString());
			else
				HgClient.ViewFile(file_info.File, cs.Rev.ToString());
		}

		//------------------------------------------------------------------
		private void HistoryDiffTwoRevisions_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (listChanges.SelectedItems.Count == 2);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void HistoryDiffTwoRevisions_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var f1 = (FileHistoryInfo2)listChanges.SelectedItems[0];
			var f2 = (FileHistoryInfo2)listChanges.SelectedItems[1];

			DiffTwoRevisions(f1, f2);

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Update_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listChanges != null)
			{
				if (listChanges.SelectedItems.Count == 1)
				{
					var change = listChanges.SelectedItems[0] as FileHistoryInfo2;
					if (change != null)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Update_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (FileHistoryInfo2)listChanges.SelectedItems[0];

			var wnd = new UpdateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = change.ChangeDesc.Rev.ToString();

			wnd.UpdateContext.Cache = BuildUpdateContextCache();

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanges();

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
				HandleBookmarksChanges();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//-----------------------------------------------------------------------------
		private void Archive_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listChanges != null)
			{
				if (listChanges.SelectedItems.Count == 1)
				{
					var change = listChanges.SelectedItems[0] as FileHistoryInfo2;
					if (change != null)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void Archive_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (FileHistoryInfo2)listChanges.SelectedItems[0];

			var wnd = new ArchiveWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.ArchiveRevision = change.ChangeDesc.Rev.ToString();

			wnd.UpdateContextCache = BuildUpdateContextCache();

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();
		}


		//------------------------------------------------------------------
		private void Tags_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listChanges != null)
			{
				if (listChanges.SelectedItems.Count == 1)
				{
					var change = listChanges.SelectedItems[0] as FileHistoryInfo2;
					if (change != null)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Tags_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (FileHistoryInfo2)listChanges.SelectedItems[0];

			TagsWindow wnd = new TagsWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = change.ChangeDesc.Rev.ToString();

			wnd.UpdateContext.Cache = BuildUpdateContextCache();

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBranchChanged)
				HandleBranchChanges();

			if (wnd.UpdateContext.IsTagsChanged)
				HandleTagsChanges();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanges();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void HandleParentChange()
		{
			RunningOperations |= AsyncOperations.Parents;
			var parents = UpdateContext.Cache.HgClient.Parents();
			OnAsyncParents(parents);
		}

		//------------------------------------------------------------------
		private void HandleBranchChanges()
		{
			RunningOperations |= AsyncOperations.Branches;
			var branches = UpdateContext.Cache.HgClient.Branches(HgBranchesOptions.Closed);
			OnAsyncBranch(branches);
		}

		//------------------------------------------------------------------
		private void HandleTagsChanges()
		{
			RunningOperations |= AsyncOperations.Tags;
			var tags = UpdateContext.Cache.HgClient.Tags();
			OnAsyncTags(tags);
		}

		//------------------------------------------------------------------
		private void HandleBookmarksChanges()
		{
			RunningOperations |= AsyncOperations.Bookmarks;
			var books = UpdateContext.Cache.HgClient.Bookmarks();
			OnAsyncBookmarks(books);
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//-----------------------------------------------------------------------------
		private void TimerOnTick(object sender, EventArgs event_args)
		{
			RunningOperations &= ~AsyncOperations.ChangeDesc;
			timer.Stop();

			if (listChanges.SelectedItems.Count == 1)
			{
				var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
				var options = HgStatusOptions.Added | HgStatusOptions.Deleted
					| HgStatusOptions.Modified
					| HgStatusOptions.Copies | HgStatusOptions.Removed;

				// TODO: Show both parents for merge
				var parent = file_history.ChangeDesc.Parents.FirstOrDefault() ?? "null";
				var files = HgClient.Status(options, "", parent, file_history.ChangeDesc.SHA1);

				listViewFiles.ItemsSource = files;
				if (listViewFiles.Items.Count > 0)
				{
					var file = files.FirstOrDefault(f => f.File == file_history.FileName);

					if (file != null)
						listViewFiles.SelectedItem = file;

					if (listViewFiles.SelectedIndex == -1)
						listViewFiles.SelectedIndex = 0;

					listViewFiles.ScrollIntoView(listViewFiles.SelectedItem);
				}
			}
		}

		//------------------------------------------------------------------
		private void listChanges_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			listViewFiles.DataContext = null;
			timer.Stop();

			if (listChanges.SelectedItems.Count == 1)
			{
				RunningOperations |= AsyncOperations.ChangeDesc;
				timer.Start();
			}
		}

		//------------------------------------------------------------------
		private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			textChangeDesc.Height = changeDescRow.Height.Value;
		}

		//------------------------------------------------------------------
		void GridViewColumnHeaderClickedHandler(object sender,
												RoutedEventArgs e)
		{
			files_sorter.GridViewColumnHeaderClickedHandler(sender, e);
		}

		//-----------------------------------------------------------------------------
		private void listViewFiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			ShowFileDiff();
		}
	}

	//==================================================================
	class FileHistoryInfo : DependencyObject
	{
		public ChangeDesc ChangeDesc { get; set; }
		public RenameInfo RenameInfo { get; set; }
		public string GroupText { get; set; }

		//-----------------------------------------------------------------------------
		public static readonly System.Windows.DependencyProperty IsCurrentProperty =
			System.Windows.DependencyProperty.Register("IsCurrent", typeof(bool),
			typeof(FileHistoryInfo));

		//-----------------------------------------------------------------------------
		public bool IsCurrent
		{
			get { return (bool)this.GetValue(IsCurrentProperty); }
			set { this.SetValue(IsCurrentProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly System.Windows.DependencyProperty BranchInfoProperty =
			System.Windows.DependencyProperty.Register("BranchInfo", typeof(BranchInfo),
			typeof(FileHistoryInfo));

		//-----------------------------------------------------------------------------
		internal BranchInfo BranchInfo
		{
			get { return (BranchInfo)this.GetValue(BranchInfoProperty); }
			set { this.SetValue(BranchInfoProperty, value); }
		}

	}

	//==================================================================
	class FileHistoryInfo2 : DependencyObject
	{
		public RevLogChangeDesc ChangeDesc { get; set; }
		public string FileName { get; set; }
		public string GroupText { get; set; }

		//-----------------------------------------------------------------------------
		public static readonly System.Windows.DependencyProperty IsCurrentProperty =
			System.Windows.DependencyProperty.Register("IsCurrent", typeof(bool),
			typeof(FileHistoryInfo2));

		//-----------------------------------------------------------------------------
		public bool IsCurrent
		{
			get { return (bool)this.GetValue(IsCurrentProperty); }
			set { this.SetValue(IsCurrentProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly System.Windows.DependencyProperty BranchInfoProperty =
			System.Windows.DependencyProperty.Register("BranchInfo", typeof(BranchInfo),
			typeof(FileHistoryInfo2));

		//-----------------------------------------------------------------------------
		internal BranchInfo BranchInfo
		{
			get { return (BranchInfo)this.GetValue(BranchInfoProperty); }
			set { this.SetValue(BranchInfoProperty, value); }
		}

	}

	//-----------------------------------------------------------------------------
	class RenameParts
	{
		public string FileName { get; set; }
		public List<RevLogChangeDesc> Revs { get; set; }
	}
}
