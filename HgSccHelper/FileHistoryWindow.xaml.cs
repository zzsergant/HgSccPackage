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

namespace HgSccHelper
{
	public partial class FileHistoryWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(FileHistoryWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffTwoRevisionsCommand = new RoutedUICommand("Diff Two Revisions",
			"DiffTwoRevisions", typeof(FileHistoryWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand FileHistoryCommand = new RoutedUICommand("File History",
			"FileHistory", typeof(FileHistoryWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand UpdateCommand = new RoutedUICommand("Update to Revision",
			"Update", typeof(FileHistoryWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand TagsCommand = new RoutedUICommand("Add/Remove Tag",
			"Tags", typeof(FileHistoryWindow));

		List<FileHistoryInfo> history;

		//------------------------------------------------------------------
		public FileHistoryWindow()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string FileName { get; set; }

		//------------------------------------------------------------------
		public string Rev { get; set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		//------------------------------------------------------------------
		public bool IsUpdated { get; private set; }

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("File History: '{0}'", FileName);

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
			IsUpdated = IsUpdated || wnd.IsUpdated;
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

			IsUpdated = IsUpdated || wnd.IsUpdated;
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
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}

	//==================================================================
	class FileHistoryInfo
	{
		public ChangeDesc ChangeDesc { get; set; }
		public RenameInfo RenameInfo { get; set; }
		public string GroupText { get; set; }
	}
}
