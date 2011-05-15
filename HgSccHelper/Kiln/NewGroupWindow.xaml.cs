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
	/// Interaction logic for NewGroupWindow.xaml
	/// </summary>
	public partial class NewGroupWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty GroupNameProperty =
			DependencyProperty.Register("GroupName", typeof(string),
			typeof(NewRepositoryWindow));

		//-----------------------------------------------------------------------------
		public string GroupName
		{
			get { return (string)this.GetValue(GroupNameProperty); }
			set { this.SetValue(GroupNameProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public List<KilnProject> Projects { get; set; }

		//-----------------------------------------------------------------------------
		public KilnProject SelectedProject { get; private set; }

		public const string CfgPath = @"Kiln\GUI\NewGroupWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public NewGroupWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();
			this.DataContext = this;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			string last_used_project;
			Cfg.Get(CfgPath, "LastUsedProject", out last_used_project, "");

			comboProjects.ItemsSource = Projects;

			int project_index = 0;

			if (!String.IsNullOrEmpty(last_used_project))
			{
				for (int i = 0; i < Projects.Count; ++i)
				{
					if (Projects[i].sName == last_used_project)
					{
						project_index = i;
						break;
					}
				}
			}

			comboProjects.SelectedIndex = project_index;

			textGroupName.SelectAll();
			textGroupName.Focus();
		}

		//-----------------------------------------------------------------------------
		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(GroupName))
			{
				MessageBox.Show("Group name must not be empty", "Error",
								MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}

			var selected_project = comboProjects.SelectedItem as KilnProject;
			if (selected_project == null)
				return;

			Cfg.Set(CfgPath, "LastUsedProject", selected_project.sName);

			SelectedProject = selected_project;
			DialogResult = true;
		}
	}
}
