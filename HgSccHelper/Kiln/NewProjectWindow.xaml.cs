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

namespace HgSccHelper.Kiln
{
	/// <summary>
	/// Interaction logic for NewProjectWindow.xaml
	/// </summary>
	public partial class NewProjectWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ProjectNameProperty =
			DependencyProperty.Register("ProjectName", typeof(string),
			typeof(NewRepositoryWindow));

		//-----------------------------------------------------------------------------
		public string ProjectName
		{
			get { return (string)this.GetValue(ProjectNameProperty); }
			set { this.SetValue(ProjectNameProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty DescriptionProperty =
			DependencyProperty.Register("Description", typeof(string),
			typeof(NewRepositoryWindow));

		//-----------------------------------------------------------------------------
		public string Description
		{
			get { return (string)this.GetValue(DescriptionProperty); }
			set { this.SetValue(DescriptionProperty, value); }
		}

		//-----------------------------------------------------------------------------
//		public string Permission { get; set; }

		public const string CfgPath = @"Kiln\GUI\NewProjectWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public NewProjectWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);
			InitializeComponent();

			this.DataContext = this;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
/*
			string last_used_permission;
			Cfg.Get(CfgPath, "LastUsedPermission", out last_used_permission, "none");

			var permissions = new[] {"none", "read", "write"};
			comboPermissions.ItemsSource = permissions;

			int permission_index = 0;

			if (!String.IsNullOrEmpty(last_used_permission))
			{
				for (int i = 0; i < permissions.Length; ++i)
				{
					if (permissions[i] == last_used_permission)
					{
						permission_index = i;
						break;
					}
				}
			}

			comboPermissions.SelectedIndex = permission_index;
*/

			textProjectName.SelectAll();
			textProjectName.Focus();
		}

		//-----------------------------------------------------------------------------
		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(ProjectName))
			{
				MessageBox.Show("Project name must not be empty", "Error",
								MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}

//			Permission = comboPermissions.SelectedItem as string;
//			Cfg.Set(CfgPath, "LastUsedPermission", Permission);

			DialogResult = true;
		}
	}
}
