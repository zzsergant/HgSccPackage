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
using HgSccPackage.Tools;
using System.Windows;

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

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(RevLogControl));

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
				this.revs = Hg.RevLog(WorkingDir, 0);
				this.rev_lines = new List<RevLogLinesPair>(
					RevLogLinesPair.FromV1(RevLogIterator.GetLines(revs)));

				graphView.ItemsSource = rev_lines;
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

	}
}
