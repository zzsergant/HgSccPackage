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

namespace HgSccHelper.BitBucket
{
	/// <summary>
	/// Interaction logic for LoginWindow.xaml
	/// </summary>
	public partial class LoginWindow : Window
	{
		//-----------------------------------------------------------------------------
		public LoginWindow()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		private void OK_Click(object sender, RoutedEventArgs e)
		{
			if (	!String.IsNullOrEmpty(credentials.Username)
				&&	!String.IsNullOrEmpty(credentials.Password)
				)
			{
				if (!Util.CheckUser(credentials.Username, credentials.Password))
				{
					MessageBox.Show("Login failed", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				MessageBox.Show("Login successfull", "Information", MessageBoxButton.OK, MessageBoxImage.Information);

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
				credentials.Username = Credentials.Instance.Username;
				credentials.Password = Credentials.Instance.Password;
			}
		}
	}
}
