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

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for BranchNameWindow.xaml
	/// </summary>
	public partial class BranchNameWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//-----------------------------------------------------------------------------
		public string BranchName { get; private set; }

		public const string CfgPath = @"GUI\BranchNameWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public BranchNameWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);
			InitializeComponent();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			textBranchName.Focus();
		}

		//------------------------------------------------------------------
		private void btnOK_Click(object sender, RoutedEventArgs e)
		{
			BranchName = textBranchName.Text;

			if (String.IsNullOrEmpty(BranchName))
			{
				MessageBox.Show("Branch name must not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			DialogResult = true;
			Close();
		}
	}
}
