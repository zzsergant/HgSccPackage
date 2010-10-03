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

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System;

namespace HgSccHelper.UI
{
	/// <summary>
	/// Interaction logic for CreateRepositoryWindow.xaml
	/// </summary>
	public partial class CreateRepositoryWindow : Window
	{
		//------------------------------------------------------------------
		public string SolutionLocation { get; set; }

		//------------------------------------------------------------------
		public string ProjectLocation { get; set; }

		//------------------------------------------------------------------
		public string ResultLocation { get; private set; }

		//------------------------------------------------------------------
		public bool SelectProjectOnLoad { get; set; }

		public const string CfgPath = @"GUI\CreateRepositoryWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public CreateRepositoryWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);

			InitializeComponent();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (!String.IsNullOrEmpty(SolutionLocation))
			{
				textSolutionLocation.Text = SolutionLocation;
				radioSolution.IsChecked = true;
			}
			else
			{
				panelSolution.Visibility = Visibility.Collapsed;
			}

			if (!String.IsNullOrEmpty(ProjectLocation))
			{
				textProjectLocation.Text = ProjectLocation;
				if (SolutionLocation == null || SelectProjectOnLoad)
					radioProject.IsChecked = true;
			}
			else
			{
				panelProject.Visibility = Visibility.Collapsed;
			}

			if (SolutionLocation == null && ProjectLocation == null)
				Close();
		}

		//------------------------------------------------------------------
		private void btnOK_Click(object sender, RoutedEventArgs e)
		{
			if (radioSolution.IsChecked == true)
			{
				ResultLocation = textSolutionLocation.Text;
				DialogResult = true;
				return;
			}

			if (radioProject.IsChecked == true)
			{
				ResultLocation = textProjectLocation.Text;
				DialogResult = true;
				return;
			}

			if (radioCustom.IsChecked == true)
			{
				var location = textCustomLocation.Text;
				if (String.IsNullOrEmpty(location))
				{
					MessageBox.Show("You should select custom location for repository",
						"Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}

				ResultLocation = location;
				DialogResult = true;
				return;
			}
		}

		//------------------------------------------------------------------
		private void radioSolution_Click(object sender, RoutedEventArgs e)
		{

		}

		//------------------------------------------------------------------
		private void radioProject_Click(object sender, RoutedEventArgs e)
		{

		}

		//------------------------------------------------------------------
		private void radioCustom_Click(object sender, RoutedEventArgs e)
		{

		}

		//------------------------------------------------------------------
		private void btnBrowse_Click(object sender, RoutedEventArgs e)
		{
			using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
			{
				dlg.Description = "Browse for repository path...";
				dlg.ShowNewFolderButton = true;

				if (!String.IsNullOrEmpty(ProjectLocation))
					dlg.SelectedPath = ProjectLocation;
				else if (!String.IsNullOrEmpty(SolutionLocation))
					dlg.SelectedPath = SolutionLocation;

				var result = dlg.ShowDialog();
				if (result == System.Windows.Forms.DialogResult.OK)
				{
					textCustomLocation.Text = dlg.SelectedPath;
				}
			}
		}
	}
}
