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
		}

		//------------------------------------------------------------------
		private void btnOK_Click(object sender, RoutedEventArgs e)
		{
			if (!diffTools.Save())
				return;

			DialogResult = true;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			diffTools.Init();
		}
	}
}
