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

namespace HgSccHelper
{
	public partial class SynchronizeWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public SynchronizeWindow()
		{
			InitializeComponent();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
 			Title = string.Format("Synchronize: '{0}'", WorkingDir);
			synchronizeControl1.WorkingDir = WorkingDir;
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}
}
