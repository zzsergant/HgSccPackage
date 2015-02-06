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
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			HgSccHelper.UI.ThemeManager.Instance.Subscribe(this);
			this.DataContext = this;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			string last_used_group_name;
			Cfg.Get(CfgPath, "LastUsedGroupName", out last_used_group_name, "");

			comboRepositoryGroup.ItemsSource = Groups;
			
			if (RepositoryGroup != null)
			{
				comboRepositoryGroup.SelectedItem = RepositoryGroup;
			}
			else
			{
				int group_index = 0;

				if (!String.IsNullOrEmpty(last_used_group_name))
				{
					for(int i = 0; i < Groups.Count; ++i)
					{
						if (Groups[i].DisplayName == last_used_group_name)
						{
							group_index = i;
							break;
						}
					}
				}

				comboRepositoryGroup.SelectedIndex = group_index;
			}

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

			Cfg.Set(CfgPath, "LastUsedGroupName", selected_group.DisplayName);

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
