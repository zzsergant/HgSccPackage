//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
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
using System.Windows;
using HgSccHelper;

namespace HgSccPackage.UI
{
	/// <summary>
	/// Interaction logic for SelectSolutionWindow.xaml
	/// </summary>
	public partial class SelectSolutionWindow : Window
	{
		//-----------------------------------------------------------------------------
		public List<string> Solutions { get; set; }

		//-----------------------------------------------------------------------------
		public string SelectedSolution { get; private set; }

		public const string CfgPath = @"GUI\SelectSolutionWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public SelectSolutionWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			HgSccHelper.UI.ThemeManager.Instance.Subscribe(this);
		}

		//-----------------------------------------------------------------------------
		private void Select_Click(object sender, RoutedEventArgs e)
		{
			SelectedSolution = listSolutions.SelectedItem as string;
			DialogResult = true;
			Close();
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = "Select a solution to open";

			if (Solutions == null || Solutions.Count == 0)
			{
				Close();
				return;
			}

			listSolutions.ItemsSource = Solutions;
			listSolutions.SelectedIndex = 0;
			listSolutions.Focus();
		}
	}
}
