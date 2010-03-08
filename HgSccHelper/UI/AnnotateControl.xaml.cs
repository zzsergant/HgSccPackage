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
using System.Windows.Controls;
using System.Windows.Media;

namespace HgSccHelper
{
	public partial class AnnotateControl : UserControl
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffTwoRevisionsCommand = new RoutedUICommand("Diff Two Revisions",
			"DiffTwoRevisions", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand FileHistoryCommand = new RoutedUICommand("File History",
			"FileHistory", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ArchiveCommand = new RoutedUICommand("Archive",
			"Archive", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand GotoLineCommand = new RoutedUICommand("Goto Line",
			"GotoLine", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand NextChangeCommand = new RoutedUICommand("Next Change",
			"NextChange", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand PrevChangeCommand = new RoutedUICommand("Prev Change",
			"PrevChange", typeof(AnnotateControl));

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
		List<AnnotateLineView> annotated_lines;

		C5.HashDictionary<int, int> rev_to_change_idx_map;
		C5.HashDictionary<int, List<AnnotateLineView>> rev_to_line_view;

		//------------------------------------------------------------------
		public AnnotateControl()
		{
			InitializeComponent();

			UpdateContext = new UpdateContext();
			file_history_map = new C5.HashDictionary<string, FileHistoryInfo>();

			deferred_executor = new DeferredCommandExecutor();
			rev_to_change_idx_map = new C5.HashDictionary<int, int>();
			rev_to_line_view = new C5.HashDictionary<int, List<AnnotateLineView>>();
		}

		//-----------------------------------------------------------------------------
		void ListItemContainerGenerator_StatusChanged(object sender, EventArgs e)
		{
			var generator = (ItemContainerGenerator)sender;
			if (generator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
			{
				if (listLines.SelectedIndex != -1)
				{
					var item = (ListViewItem)listLines.ItemContainerGenerator.ContainerFromIndex(listLines.SelectedIndex);
					if (item != null)
					{
						listLines.ItemContainerGenerator.StatusChanged -= ListItemContainerGenerator_StatusChanged;
						item.Focus();
					}
				}
				else
				{
					listLines.ItemContainerGenerator.StatusChanged -= ListItemContainerGenerator_StatusChanged;
				}
			}
		}

		//------------------------------------------------------------------
		public GridView ListChangesGrid
		{
			get { return listChangesGrid; }
		}

		//------------------------------------------------------------------
		public GridView ListLinesGrid
		{
			get { return listLinesGrid; }
		}

		//------------------------------------------------------------------
		private void Control_Loaded(object sender, RoutedEventArgs e)
		{
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

			if (!FillAnnotateInfo())
			{
				return;
			}


			var rev_range = "";
			if (!string.IsNullOrEmpty(Rev))
				rev_range = string.Format("{0}:0", Rev);

			var changes = Hg.ChangesFull(WorkingDir, FileName, rev_range);
			if (changes.Count == 0)
			{
				Logger.WriteLine("Changes == 0");
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

				// the revision -> list index
				rev_to_change_idx_map[history_item.ChangeDesc.Rev] = history.Count;

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
		bool FillAnnotateInfo()
		{
			var hg_annotate = new HgAnnotate();
			try
			{
				var lines_info = hg_annotate.Annotate(WorkingDir, Rev ?? "", FileName);
				if (lines_info.Count == 0)
					return false;

				int prev_rev = lines_info[0].Rev;
				var even = SystemColors.ControlBrush;
				var odd = SystemColors.ControlLightBrush;
				int counter = 0;

				annotated_lines = new List<AnnotateLineView>();
				foreach (var line_info in lines_info)
				{
					var line_view = new AnnotateLineView();
					line_view.Info = line_info;

					if (prev_rev != line_info.Rev)
					{
						counter++;
						prev_rev = line_info.Rev;
					}

					line_view.Background = ((counter & 1) == 0) ? odd : even;
					annotated_lines.Add(line_view);

					List<AnnotateLineView> rev_lines;
					if (!rev_to_line_view.Find(line_view.Info.Rev, out rev_lines))
					{
						rev_lines = new List<AnnotateLineView>();
						rev_to_line_view[line_view.Info.Rev] = rev_lines;
					}

					rev_lines.Add(line_view);
				}
			}
			catch (HgAnnotateBinaryException)
			{
				MessageBox.Show("Unable to annotate a binary file", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}

			listLines.ItemsSource = annotated_lines;
			return true;
		}

		//------------------------------------------------------------------
		private void Control_Unloaded(object sender, RoutedEventArgs e)
		{
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
			if (DiffPreviousCommand != null)
			{
				if (DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
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
			if (DiffPreviousCommand != null)
			{
				if (DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
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
		private void listChanges_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			listViewFiles.DataContext = null;

			if (listChanges.SelectedItems.Count == 1)
			{
				var file_history = (FileHistoryInfo)listChanges.SelectedItem;
				listViewFiles.ItemsSource = file_history.ChangeDesc.Files;
				if (listViewFiles.Items.Count > 0)
					listViewFiles.SelectedIndex = 0;

				List<AnnotateLineView> rev_lines;
				if (rev_to_line_view.Find(file_history.ChangeDesc.Rev, out rev_lines))
				{
					if (rev_lines.Count > 0)
					{
						int line_number = rev_lines[0].Info.Line - 1;
						if (line_number < listLines.Items.Count)
						{
							var line_view = listLines.Items[line_number] as AnnotateLineView;
							if (!line_view.IsSelected)
								ScrollSelectAndFocusLine(line_number);
						}
					}
				}
				else
				{
					listLines.SelectedItem = null;
				}
			}
		}

		//------------------------------------------------------------------
		private void listLines_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var removed = e.RemovedItems;
			foreach (AnnotateLineView removed_line in removed)
			{
				foreach (var line_view in rev_to_line_view[removed_line.Info.Rev])
					line_view.IsSelected = false;
			}

			var annotate_line = listLines.SelectedItem as AnnotateLineView;
			if (annotate_line != null)
			{
				foreach (var line_view in rev_to_line_view[annotate_line.Info.Rev])
					line_view.IsSelected = true;

				int idx;
				if (rev_to_change_idx_map.Find(annotate_line.Info.Rev, out idx))
				{
					listChanges.SelectedIndex = idx;
					listChanges.ScrollIntoView(listChanges.SelectedItem);
				}
			}
		}

		//------------------------------------------------------------------
		private void GotoLine_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (listLines != null)
			{
				int line;
				if (int.TryParse(textLine.Text, out line))
				{
					line -= 1;
					if (line >= 0 && line < listLines.Items.Count)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void GotoLine_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			int line;
			if (int.TryParse(textLine.Text, out line))
			{
				line -= 1;
				ScrollSelectAndFocusLine(line);
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void textLine_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				if (GotoLineCommand.CanExecute(sender, e.Source as IInputElement))
					GotoLineCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private void NextChange_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void NextChange_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (listLines == null)
				return;

			var line_view = listLines.SelectedItem as AnnotateLineView;
			if (line_view == null)
				return;

			int revision = line_view.Info.Rev;
			int line_number = line_view.Info.Line;

			var end_of_current_idx = annotated_lines.FindIndex(line_number - 1,
				annotated_line => annotated_line.Info.Rev != revision);

			if (end_of_current_idx == -1)
				return;

			var next_change_idx = annotated_lines.FindIndex(end_of_current_idx,
					annotated_line => annotated_line.Info.Rev == revision);

			if (next_change_idx == -1)
				return;

			ScrollSelectAndFocusLine(next_change_idx);
		}

		//------------------------------------------------------------------
		private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(obj, i);
				if (child != null && child is childItem)
				{
					return (childItem)child;
				}
				else
				{
					childItem childOfChild = FindVisualChild<childItem>(child);
					if (childOfChild != null)
					{
						return childOfChild;
					}
				}
			}
			return null;
		}  

		//------------------------------------------------------------------
		void ScrollSelectAndFocusLine(int idx)
		{
			if (idx >= 0 && idx < listLines.Items.Count)
			{
				// FIXME: Find a better way to centering on item

				var scroll_viewer = FindVisualChild<ScrollViewer>(listLines);
				if (scroll_viewer != null)
				{
					double top = Math.Max(0, idx - scroll_viewer.ViewportHeight / 2);
					scroll_viewer.ScrollToVerticalOffset(top);
				}
				else
				{
					listLines.ScrollIntoView(listLines.Items[idx]);
				}

				listLines.SelectedIndex = idx;

				var item = (ListViewItem)listLines.ItemContainerGenerator.ContainerFromIndex(listLines.SelectedIndex);
				if (item != null)
				{
					item.Focus();
				}
				else
				{
					listLines.ItemContainerGenerator.StatusChanged += ListItemContainerGenerator_StatusChanged;
				}
			}
		}

		//------------------------------------------------------------------
		private void PrevChange_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void PrevChange_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (listLines == null)
				return;

			var line_view = listLines.SelectedItem as AnnotateLineView;
			if (line_view == null)
				return;

			int revision = line_view.Info.Rev;
			int line_number = line_view.Info.Line;

			var end_of_current_idx = annotated_lines.FindLastIndex(line_number - 1,
				annotated_line => annotated_line.Info.Rev != revision);

			if (end_of_current_idx == -1)
				return;
				
			var next_change_idx = annotated_lines.FindLastIndex(end_of_current_idx,
					annotated_line => annotated_line.Info.Rev == revision);

			if (next_change_idx == -1)
				return;

			ScrollSelectAndFocusLine(next_change_idx);
		}
	}

	//==================================================================
	class AnnotateLineView : DependencyObject
	{
		public AnnotateLineInfo Info { get; set; }
		
		//-----------------------------------------------------------------------------
		public static readonly System.Windows.DependencyProperty BackgroundProperty =
			System.Windows.DependencyProperty.Register("Background", typeof(Brush),
			typeof(AnnotateLineView));

		//-----------------------------------------------------------------------------
		public Brush Background
		{
			get { return (Brush)this.GetValue(BackgroundProperty); }
			set { this.SetValue(BackgroundProperty, value); }
		}

		//------------------------------------------------------------------
		public bool IsSelected
		{
			get { return (bool)GetValue(IsSelectedProperty); }
			set { SetValue(IsSelectedProperty, value); }
		}

		//------------------------------------------------------------------
		public static readonly DependencyProperty IsSelectedProperty =
			DependencyProperty.Register("IsSelected", typeof(bool), typeof(AnnotateLineView));

		//------------------------------------------------------------------
		public bool IsCurrent
		{
			get { return (bool)GetValue(IsCurrentProperty); }
			set { SetValue(IsCurrentProperty, value); }
		}

		//------------------------------------------------------------------
		public static readonly DependencyProperty IsCurrentProperty =
			DependencyProperty.Register("IsCurrent", typeof(bool), typeof(AnnotateLineView));
	}
}
