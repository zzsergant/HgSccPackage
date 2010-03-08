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
using System.Collections.Generic;
using System;
using System.Windows.Data;
using HgSccHelper.UI;

namespace HgSccHelper
{
	public partial class FileHistoryWindow : Window
	{

		List<FileHistoryInfo> history;

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
		C5.HashDictionary<string, BranchInfo> Branches { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// Tag Name -> TagInfo map
		/// </summary>
		C5.HashDictionary<string, TagInfo> Tags { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// SHA1 -> FileHistoryInfo map
		/// </summary>
		C5.HashDictionary<string, FileHistoryInfo> file_history_map;

		DeferredCommandExecutor deferred_executor;

		public const string CfgPath = @"GUI\FileHistoryWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public FileHistoryWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			UpdateContext = new UpdateContext();
			file_history_map = new C5.HashDictionary<string, FileHistoryInfo>();

			deferred_executor = new DeferredCommandExecutor();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			listChangesGrid.LoadCfg(FileHistoryWindow.CfgPath, "ListChangesGrid");
			
			Title = string.Format("File History: '{0}'", FileName);

			Hg = new Hg();

			CurrentRevision = Hg.Identify(WorkingDir);
			if (CurrentRevision == null)
				return;

			Branches = new C5.HashDictionary<string, BranchInfo>();
			foreach (var branch in Hg.Branches(WorkingDir, HgBranchesOptions.Closed))
			{
				Branches[branch.SHA1] = branch;
			}

			Tags = new C5.HashDictionary<string, TagInfo>();
			foreach (var tag in Hg.Tags(WorkingDir))
			{
				Tags[tag.Name] = tag;
			}


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

			var changes = Hg.ChangesFull(WorkingDir, FileName, rev_range);
			if (changes.Count == 0)
			{
				Logger.WriteLine("Changes == 0");
				Close();
				return;
			}

			var renames = Hg.FindRenames(WorkingDir, FileName, changes);

			history = new List<FileHistoryInfo>();

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

				foreach (var parent in CurrentRevision.Parents)
				{
					if (history_item.ChangeDesc.SHA1 == parent.SHA1)
					{
						history_item.IsCurrent = true;
						break;
					}
				}

				BranchInfo branch_info;
				if (Branches.Find(history_item.ChangeDesc.SHA1, out branch_info))
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

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			listChangesGrid.SaveCfg(FileHistoryWindow.CfgPath, "ListChangesGrid");

			deferred_executor.Dispose();
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

			deferred_executor.QueueDefferedExecute(() =>
			{
				DiffTwoRevisions(f1, f2);
			});

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

			deferred_executor.QueueDefferedExecute(() =>
			{
				var hg = new Hg();
				if (file_info.Status == FileStatus.Removed)
					hg.ViewFile(WorkingDir, file_info.Path, (cs.Rev - 1).ToString());
				else
					hg.ViewFile(WorkingDir, file_info.Path, cs.Rev.ToString());
			});
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

			deferred_executor.QueueDefferedExecute(() =>
			{
				DiffTwoRevisions(f1, f2);
			});

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

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void HandleParentChange()
		{
			var hg = new Hg();
			var new_current = hg.Identify(WorkingDir);

			foreach (var parent in CurrentRevision.Parents)
			{
				FileHistoryInfo file_history;
				if (file_history_map.Find(parent.SHA1, out file_history))
					file_history.IsCurrent = false;
			}

			CurrentRevision = new_current;
			foreach (var parent in CurrentRevision.Parents)
			{
				FileHistoryInfo file_history;
				if (file_history_map.Find(parent.SHA1, out file_history))
					file_history.IsCurrent = true;
			}
		}

		//------------------------------------------------------------------
		private void HandleBranchChanges()
		{
			var hg = new Hg();
			var new_branches = new C5.HashDictionary<string, BranchInfo>();
			var branch_list = hg.Branches(WorkingDir, HgBranchesOptions.Closed);

			foreach (var branch_info in branch_list)
			{
				new_branches[branch_info.SHA1] = branch_info;
				Branches.Remove(branch_info.SHA1);
			}

			foreach (var branch_info in Branches.Values)
			{
				// removing old branch info
				FileHistoryInfo file_history;
				if (file_history_map.Find(branch_info.SHA1, out file_history))
					file_history.BranchInfo = null;
			}

			Branches = new_branches;

			foreach (var branch_info in Branches.Values)
			{
				// adding or updating branch info
				FileHistoryInfo file_history;
				if (file_history_map.Find(branch_info.SHA1, out file_history))
					file_history.BranchInfo = branch_info;
			}
		}

		//------------------------------------------------------------------
		private void HandleTagsChanges()
		{
			var hg = new Hg();
			var new_tags = new C5.HashDictionary<string, TagInfo>();
			var tags_list = hg.Tags(WorkingDir);

			foreach (var tag in tags_list)
			{
				new_tags[tag.Name] = tag;
			}

			foreach (var tag in Tags.Values)
			{
				// removing old tags
				FileHistoryInfo file_history;
				if (file_history_map.Find(tag.SHA1, out file_history))
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
				if (file_history_map.Find(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					if (!change_desc.Tags.Contains(tag.Name))
						change_desc.Tags.Add(tag.Name);
				}
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
					listViewFiles.SelectedIndex = 0;
			}
		}

		//------------------------------------------------------------------
		private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			textChangeDesc.Height = changeDescRow.Height.Value;
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
