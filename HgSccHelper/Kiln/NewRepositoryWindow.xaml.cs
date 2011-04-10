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

using System;
using System.Collections.Generic;
using System.Windows;

namespace HgSccHelper.Kiln
{
	/// <summary>
	/// Interaction logic for NewRepositoryWindow.xaml
	/// </summary>
	public partial class NewRepositoryWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty RepositoryNameProperty =
			DependencyProperty.Register("RepositoryName", typeof(string),
			typeof(NewRepositoryWindow));

		//-----------------------------------------------------------------------------
		public string RepositoryName
		{
			get { return (string)this.GetValue(RepositoryNameProperty); }
			set { this.SetValue(RepositoryNameProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public List<NewRepoGroupItem> Groups { get; set; }

		//-----------------------------------------------------------------------------
		public NewRepoGroupItem RepositoryGroup { get; set; }

		public const string CfgPath = @"Kiln\GUI\NewRepositoryWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public NewRepositoryWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);

			InitializeComponent();
			this.DataContext = this;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			comboRepositoryGroup.ItemsSource = Groups;
			if (RepositoryGroup != null)
				comboRepositoryGroup.SelectedItem = RepositoryGroup;
			else
				comboRepositoryGroup.SelectedIndex = 0;

			textRepositoryName.SelectAll();
			textRepositoryName.Focus();
		}

		//-----------------------------------------------------------------------------
		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(RepositoryName))
			{
				MessageBox.Show("Repository name must not be empty", "Error",
				                MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}

			var selected_group = comboRepositoryGroup.SelectedItem as NewRepoGroupItem;
			if (selected_group == null)
				return;

			RepositoryGroup = selected_group;
			DialogResult = true;
		}
	}

	//-----------------------------------------------------------------------------
	public class NewRepoGroupItem
	{
		public string DisplayName { get; set; }
		public KilnGroup Group { get; set; }
	}
}
