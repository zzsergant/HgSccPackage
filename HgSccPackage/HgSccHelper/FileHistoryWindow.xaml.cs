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

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for FileHistoryWindow.xaml
	/// </summary>
	public partial class FileHistoryWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(FileHistoryWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand FileHistoryCommand = new RoutedUICommand("File History",
			"FileHistory", typeof(FileHistoryWindow));

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

				var history_item = new FileHistoryInfo {
					ChangeDesc = change, RenameInfo = renames[left_idx] };

				history.Add(history_item);
			}

			listChanges.ItemsSource = history;
			if (listChanges.Items.Count > 0)
				listChanges.SelectedIndex = 0;

			var myView = (CollectionView)CollectionViewSource.GetDefaultView(listChanges.ItemsSource);
			var groupDescription = new PropertyGroupDescription("RenameInfo.Path");
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

			try
			{
				Hg.Diff(WorkingDir, f1.RenameInfo.Path, f1.ChangeDesc.Rev, f2.RenameInfo.Path, f2.ChangeDesc.Rev);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}

			e.Handled = true;
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

			wnd.ShowDialog();
		}
	}

	//==================================================================
	class FileHistoryInfo
	{
		public ChangeDesc ChangeDesc { get; set; }
		public RenameInfo RenameInfo { get; set; }
	}
}
