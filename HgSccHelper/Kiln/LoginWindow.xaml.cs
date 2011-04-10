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
using RestSharp;

namespace HgSccHelper.Kiln
{
	/// <summary>
	/// Interaction logic for LoginWindow.xaml
	/// </summary>
	public partial class LoginWindow : Window
	{
		public const string CfgPath = @"Kiln\GUI\LoginWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public LoginWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);

			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		private void OK_Click(object sender, RoutedEventArgs e)
		{
			if (	!String.IsNullOrEmpty(credentials.Site)
				&&	!String.IsNullOrEmpty(credentials.Username)
				&&	!String.IsNullOrEmpty(credentials.Password)
				)
			{
				if (!Session.Instance.Login(credentials.Site, credentials.Username, credentials.Password))
				{
					MessageBox.Show("Login failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				MessageBox.Show("Login successfull", "Information", MessageBoxButton.OK, MessageBoxImage.Information);

				Credentials.Instance.Site = credentials.Site;
				Credentials.Instance.Username = credentials.Username;
				Credentials.Instance.Password = credentials.Password;
				Credentials.Save();

				DialogResult = true;
			}
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (!String.IsNullOrEmpty(Credentials.Instance.Username))
			{
				credentials.Site = Credentials.Instance.Site;
				credentials.Username = Credentials.Instance.Username;
				credentials.Password = Credentials.Instance.Password;
			}

			credentials.Init();
		}
	}
}
