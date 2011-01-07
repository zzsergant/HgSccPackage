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
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Collections.Generic;
using System;
using System.Windows.Data;
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
		Hg Hg { get; set; }

		//------------------------------------------------------------------
		IdentifyInfo CurrentRevision { get; set; }

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
		/// SHA1 -> FileHistoryInfo map
		/// </summary>
		Dictionary<string, FileHistoryInfo> file_history_map;

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

		public const string CfgPath = @"GUI\FileHistoryWindow";
		CfgWindowPosition wnd_cfg;

		private AsyncChangeDescFull async_changedesc;
		private AsyncIdentify async_identify;
		private AsyncBranches async_branches;
		private AsyncTags async_tags;
		private AsyncBookmarks async_bookmarks;

		//------------------------------------------------------------------
		public FileHistoryWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			UpdateContext = new UpdateContext();
			file_history_map = new Dictionary<string, FileHistoryInfo>();

			files_sorter = new GridViewColumnSorter(listViewFiles);
			diffColorizer.Complete = new Action<List<string>>(OnDiffColorizer);

			async_changedesc = new AsyncChangeDescFull();
			async_changedesc.Complete = new Action<List<ChangeDesc>>(OnAsyncChangeDescFull);

			async_identify = new AsyncIdentify();
			async_identify.Complete = new Action<IdentifyInfo>(OnAsyncIdentify);

			async_branches = new AsyncBranches();
			async_branches.Complete = new Action<List<BranchInfo>>(OnAsyncBranch);

			async_tags = new AsyncTags();
			async_tags.Complete = new Action<List<TagInfo>>(OnAsyncTags);

			async_bookmarks = new AsyncBookmarks();
			async_bookmarks.Complete = new Action<List<BookmarkInfo>>(OnAsyncBookmarks);
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

			Hg = new Hg();

			var files = Hg.Status(WorkingDir, FileName, Rev ?? "");
			if (files.Count == 1
				&& files[0].Status == HgFileStatus.Added
				&& files[0].CopiedFrom != null)
			{
				var file_info = files[0];
				FileName = file_info.CopiedFrom;
			}

			var rev_range = "";
			if (!string.IsNullOrEmpty(Rev))
				rev_range = string.Format("{0}:0", Rev);

			RunningOperations |= AsyncOperations.ChangeDesc;
			async_changedesc.RunAsync(WorkingDir, FileName, rev_range);
		}

		//------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			diffColorizer.Dispose();

			async_changedesc.Cancel();
			async_changedesc.Dispose();

			async_identify.Cancel();
			async_identify.Dispose();

			async_branches.Cancel();
			async_branches.Dispose();

			async_tags.Cancel();
			async_tags.Dispose();

			async_bookmarks.Cancel();
			async_bookmarks.Dispose();

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
		private void OnAsyncChangeDescFull(List<ChangeDesc> changes)
		{
			RunningOperations &= ~AsyncOperations.ChangeDesc;

			if (changes == null)
				return;

			if (changes.Count == 0)
			{
				Logger.WriteLine("Changes == 0");
				Close();
				return;
			}

			HandleBranchChanges();
			HandleTagsChanges();
			HandleParentChange();
			HandleBookmarksChanges();

			var renames = Hg.FindRenames(WorkingDir, FileName, changes);
			var history = new List<FileHistoryInfo>();

			int left_idx = 0;
			int right_idx = 1;

			foreach (var change in changes)
			{
				if (right_idx < renames.Count)
				{
					var right = renames[right_idx];
					if (change.Rev <= right.Rev)
						left_idx = right_idx++;
				}

				var history_item = new FileHistoryInfo();
				history_item.ChangeDesc = change;
				history_item.RenameInfo = renames[left_idx];
				history_item.GroupText = String.Format("[{0}]: {1}", renames.Count - left_idx, history_item.RenameInfo.Path);

				if (CurrentRevision != null)
				{
					foreach (var parent in CurrentRevision.Parents)
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

			listChanges.ItemsSource = history;
			if (listChanges.Items.Count > 0)
				listChanges.SelectedIndex = 0;

			listChanges.Focus();

			var myView = (CollectionView)CollectionViewSource.GetDefaultView(listChanges.ItemsSource);
			var groupDescription = new PropertyGroupDescription("GroupText");
			myView.GroupDescriptions.Add(groupDescription);
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
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					change_desc.Tags.Remove(tag.Name);
				}
			}

			Tags = new_tags;

			foreach (var tag in Tags.Values)
			{
				// adding or updating tags
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
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
				// removing old bookmark
				FileHistoryInfo file_history;
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
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(bookmark.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
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
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = null;
			}

			Branches = new_branches;

			foreach (var branch_info in Branches.Values)
			{
				// adding or updating branch info
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = branch_info;
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncIdentify(IdentifyInfo new_current)
		{
			RunningOperations &= ~AsyncOperations.Identify;

			if (new_current == null)
				return;

			if (CurrentRevision != null)
			{
				foreach (var parent in CurrentRevision.Parents)
				{
					FileHistoryInfo file_history;
					if (file_history_map.TryGetValue(parent.SHA1, out file_history))
						file_history.IsCurrent = false;
				}
			}

			CurrentRevision = new_current;
			if (CurrentRevision != null)
			{
				foreach (var parent in CurrentRevision.Parents)
				{
					FileHistoryInfo file_history;
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
				var file_history = (FileHistoryInfo)listChanges.SelectedItem;
				var file_info = (FileInfo)listViewFiles.SelectedItem;
				var cs = file_history.ChangeDesc;

				RunningOperations |= AsyncOperations.Diff;
				string rev2 = cs.SHA1;
				string rev1 = (cs.Rev - 1).ToString();
				if (cs.Rev == 0)
					rev1 = "null";

				diffColorizer.RunHgDiffAsync(WorkingDir, file_info.Path, rev1, rev2);
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
			var f1 = (FileHistoryInfo)listChanges.Items[listChanges.SelectedIndex];
			var f2 = (FileHistoryInfo)listChanges.Items[listChanges.SelectedIndex + 1];

			DiffTwoRevisions(f1, f2);

			e.Handled = true;
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
				var file_info = (FileInfo)listViewFiles.SelectedItem;
				if (file_info.Status == FileStatus.Modified)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FilesDiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo)listChanges.SelectedItem;
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			try
			{
				Hg.Diff(WorkingDir, file_info.Path, cs.Rev - 1, file_info.Path, cs.Rev);
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
		private void FileHistory_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo)listChanges.SelectedItem;
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = cs.Rev.ToString();
			wnd.FileName = file_info.Path;

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
			var file_history = (FileHistoryInfo)listChanges.SelectedItem;
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			var wnd = new AnnotateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = cs.Rev.ToString();
			wnd.FileName = file_info.Path;

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
			var file_history = (FileHistoryInfo)listChanges.SelectedItem;
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			var hg = new Hg();
			if (file_info.Status == FileStatus.Removed)
				hg.ViewFile(WorkingDir, file_info.Path, (cs.Rev - 1).ToString());
			else
				hg.ViewFile(WorkingDir, file_info.Path, cs.Rev.ToString());
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
			var f1 = (FileHistoryInfo)listChanges.SelectedItems[0];
			var f2 = (FileHistoryInfo)listChanges.SelectedItems[1];

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
					var change = listChanges.SelectedItems[0] as FileHistoryInfo;
					if (change != null)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Update_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (FileHistoryInfo)listChanges.SelectedItems[0];

			UpdateWindow wnd = new UpdateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = change.ChangeDesc.Rev.ToString();

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
					var change = listChanges.SelectedItems[0] as FileHistoryInfo;
					if (change != null)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void Archive_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (FileHistoryInfo)listChanges.SelectedItems[0];

			var wnd = new ArchiveWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.ArchiveRevision = change.ChangeDesc.Rev.ToString();

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
					var change = listChanges.SelectedItems[0] as FileHistoryInfo;
					if (change != null)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Tags_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var change = (FileHistoryInfo)listChanges.SelectedItems[0];

			TagsWindow wnd = new TagsWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.TargetRevision = change.ChangeDesc.Rev.ToString();

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
			RunningOperations |= AsyncOperations.Identify;
			async_identify.RunAsync(WorkingDir);
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
		private void HandleBookmarksChanges()
		{
			if (HgExtensionsCache.Instance.IsExtensionEnabled(HgExtension.Bookmarks))
			{
				RunningOperations |= AsyncOperations.Bookmarks;
				async_bookmarks.RunAsync(WorkingDir);
			}
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void listChanges_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			listViewFiles.DataContext = null;

			if (listChanges.SelectedItems.Count == 1)
			{
				var file_history = (FileHistoryInfo)listChanges.SelectedItem;
				listViewFiles.ItemsSource = file_history.ChangeDesc.Files;
				if (listViewFiles.Items.Count > 0)
				{
					var file = file_history.ChangeDesc.Files.FirstOrDefault(
						f => f.Path == file_history.RenameInfo.Path);

					if (file != null)
						listViewFiles.SelectedItem = file;

					if (listViewFiles.SelectedIndex == -1)
						listViewFiles.SelectedIndex = 0;

					listViewFiles.ScrollIntoView(listViewFiles.SelectedItem);
				}
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
}
