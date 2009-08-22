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

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for RevLogControl.xaml
	/// </summary>
	public partial class RevLogControl : UserControl
	{
		List<RevLogChangeDesc> revs;
		List<RevLogLinesPair> rev_lines;

		DispatcherTimer timer;

		const int BatchSize = 500;

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand FileHistoryCommand = new RoutedUICommand("File History",
			"FileHistory", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ReadNextCommand = new RoutedUICommand("Read Next",
			"ReadNext", typeof(RevLogControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ReadAllCommand = new RoutedUICommand("Read All",
			"ReadAll", typeof(RevLogControl));

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public RevLogControl()
		{
			InitializeComponent();

			VirtualizingStackPanel.SetIsVirtualizing(graphView, true);
			VirtualizingStackPanel.SetVirtualizationMode(graphView, VirtualizationMode.Recycling);

			VirtualizingStackPanel.SetIsVirtualizing(listViewFiles, true);
			VirtualizingStackPanel.SetVirtualizationMode(listViewFiles, VirtualizationMode.Recycling);
		}

		//------------------------------------------------------------------
		private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
		{
			Hg = new Hg();
			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(50);
			timer.Tick += OnTimerTick;

			if (WorkingDir != null)
			{
				ReadRevLog("", BatchSize);
				if (graphView.Items.Count > 0)
					graphView.SelectedIndex = 0;
			}
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();

			if (graphView.SelectedItems.Count == 1)
			{
				var rev_pair = (RevLogLinesPair)graphView.SelectedItem;
				var cs_list = Hg.ChangesFull(WorkingDir, "", rev_pair.Current.ChangeDesc.SHA1);
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
		}

		//------------------------------------------------------------------
		private void DiffPrevious_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
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
		private void DiffPrevious_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
		{
			e.Handled = true;

			var cs = (ChangeDesc)listViewFiles.DataContext;
			var file_info = (FileInfo)listViewFiles.SelectedItem;

			try
			{
				Hg.Diff(WorkingDir, file_info.Path, cs.Rev - 1, file_info.Path, cs.Rev);
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
				if (revs[revs.Count - 1].Rev != 0)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		void ReadRevLog(string revisions)
		{
			ReadRevLog(revisions, 0);
		}

		//------------------------------------------------------------------
		void ReadRevLog(string revisions, int max_count)
		{
			var prev_cursor = Cursor;
			Cursor = Cursors.Wait;

			List<RevLogChangeDesc> next_revs;
			if (revisions.Length > 0)
				next_revs = Hg.RevLog(WorkingDir, revisions, max_count);
			else
				next_revs = Hg.RevLog(WorkingDir, max_count);

			if (revs == null)
				revs = next_revs;
			else
				revs.AddRange(next_revs);

			rev_lines = new List<RevLogLinesPair>(
				RevLogLinesPair.FromV1(RevLogIterator.GetLines(revs)));

			graphView.ItemsSource = rev_lines;

			Cursor = prev_cursor;
		}

		//------------------------------------------------------------------
		private void ReadNext_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var prev_selected = graphView.SelectedIndex;

			var start_rev = revs[revs.Count - 1].Rev - 1;
			var stop_rev = Math.Max(0, start_rev - BatchSize);
			var rev = string.Format("{0}:{1}", start_rev, stop_rev);

			ReadRevLog(rev);

			if (prev_selected != -1)
			{
				graphView.SelectedIndex = prev_selected;
				graphView.ItemContainerGenerator.StatusChanged += new EventHandler(ItemContainerGenerator_StatusChanged);
			}
		}

		//------------------------------------------------------------------
		private void ReadAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			ReadNext_CanExecute(sender, e);
		}

		//------------------------------------------------------------------
		private void ReadAll_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var prev_selected = graphView.SelectedIndex;

			var start_rev = revs[revs.Count - 1].Rev - 1;
			var rev = string.Format("{0}:{1}", start_rev, 0);

			ReadRevLog(rev);

			if (prev_selected != -1)
			{
				graphView.SelectedIndex = prev_selected;
				graphView.ItemContainerGenerator.StatusChanged += new EventHandler(ItemContainerGenerator_StatusChanged);
			}
		}

		//------------------------------------------------------------------
		void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
		{
			var generator = (ItemContainerGenerator)sender;
			if (generator.Status == GeneratorStatus.ContainersGenerated)
			{
				graphView.ItemContainerGenerator.StatusChanged -= new EventHandler(ItemContainerGenerator_StatusChanged);
				var selected_item = (ListViewItem)graphView.ItemContainerGenerator.ContainerFromIndex(graphView.SelectedIndex);
				if (selected_item != null)
				{
					// if selected item is visible, set keyboard focus to it
					selected_item.Focus();
				}
				else
				{
					// otherwise we can either:
					// 1. scroll view to the selected item and focus it
					// 2. set selected index to first item
 
					// graphView.SelectedIndex = 0;
				}
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
			var change = (ChangeDesc)listViewFiles.DataContext;
			var file_info = (FileInfo)listViewFiles.SelectedItem;

			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = change.Rev.ToString();
			wnd.FileName = file_info.Path;

			wnd.ShowDialog();
		}
	}
}
