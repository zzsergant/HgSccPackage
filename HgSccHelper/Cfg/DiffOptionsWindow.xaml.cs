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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for DiffOptionsWindow.xaml
	/// </summary>
	public partial class DiffOptionsWindow : Window
	{
		//------------------------------------------------------------------
		public DiffOptionsWindow()
		{
			InitializeComponent();

			if (HgSccOptions.Options.DiffTool.Length != 0)
				textCustomDiffTool.Text = HgSccOptions.Options.DiffTool;

			var lst = HgOptionsHelper.DetectDiffTools();
			if (lst.Count == 0)
			{
				radioAutoDetect.IsEnabled = false;
				radioCustom.IsChecked = true;
			}
			else
			{
				comboDiffTools.ItemsSource = lst;
				radioAutoDetect.IsChecked = true;

				if (HgSccOptions.Options.DiffTool.Length != 0)
				{
					if (lst.Contains(HgSccOptions.Options.DiffTool))
					{
						comboDiffTools.SelectedItem = HgSccOptions.Options.DiffTool;
					}
					else
					{
						radioCustom.IsChecked = true;
					}
				}
				
				if (comboDiffTools.SelectedIndex == -1)
					comboDiffTools.SelectedIndex = 0;
			}
		}

		//------------------------------------------------------------------
		private void Browse_Click(object sender, RoutedEventArgs e)
		{
			string diff_tool = textCustomDiffTool.Text;
			if (HgOptionsHelper.BrowseDiffTool(ref diff_tool))
				textCustomDiffTool.Text = diff_tool;
		}

		//------------------------------------------------------------------
		public string DiffTool
		{
			get
			{
				if (radioAutoDetect.IsChecked == true)
					return comboDiffTools.SelectedItem as string;

				return textCustomDiffTool.Text;
			}
		}

		//------------------------------------------------------------------
		private void btnOK_Click(object sender, RoutedEventArgs e)
		{
			var diff_tool = DiffTool;

			if (diff_tool.Length != 0)
			{
				if (!File.Exists(diff_tool))
				{
					MessageBox.Show("File: " + diff_tool + " is not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}

			HgSccOptions.Options.DiffTool = diff_tool;
			HgSccOptions.Save();

			DialogResult = true;
		}
	}
}
