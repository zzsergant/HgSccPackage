//=========================================================================
// Copyright 2015 Sergey Antonov <sergant_@mail.ru>
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
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HgSccHelper.UI;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for OptionsPageTheme.xaml
	/// </summary>
	public partial class OptionsPageTheme : UserControl, IOptionsPage
	{
		public OptionsPageTheme()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		public string PageName
		{
			get { return "Theme"; }
		}

		//-----------------------------------------------------------------------------
		public void Init()
		{
			comboTheme.ItemsSource = ThemeManager.Instance.Themes;
			comboTheme.SelectedItem = ThemeManager.Instance.Current;
		}

		//-----------------------------------------------------------------------------
		private void comboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var item = comboTheme.SelectedItem as Theme;
			if (item != null)
				ThemeManager.Instance.Current = item;
		}

		//-----------------------------------------------------------------------------
		public bool Save()
		{
			ThemeManager.Save();
			return true;
		}

		//-----------------------------------------------------------------------------
		public ContentControl PageContent
		{
			get { return this; }
		}
	}
}
