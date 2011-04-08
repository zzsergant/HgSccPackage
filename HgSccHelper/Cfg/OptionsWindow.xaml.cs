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

using System.Collections.ObjectModel;
using System.Windows;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for OptionsWindow.xaml
	/// </summary>
	public partial class OptionsWindow : Window
	{
		private ObservableCollection<IOptionsPage> pages;

		public const string CfgPath = @"GUI\OptionsWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public OptionsWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();
			pages = new ObservableCollection<IOptionsPage>();

			this.DataContext = pages;
		}

		//-----------------------------------------------------------------------------
		public void AddPage(IOptionsPage page)
		{
			pages.Add(page);
		}

		//-----------------------------------------------------------------------------
		private void Button_Save(object sender, RoutedEventArgs e)
		{
			foreach (var page in pages)
			{
				if (!page.Save())
				{
					listPages.SelectedItem = page;
					return;
				}
			}

			DialogResult = true;
		}
	}
}
