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
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace HgSccHelper.BitBucket
{
	/// <summary>
	/// Interaction logic for OptionsPageBitBucket.xaml
	/// </summary>
	public partial class OptionsPageBitBucket : UserControl, IOptionsPage
	{
		public OptionsPageBitBucket()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		private void Login_Click(object sender, RoutedEventArgs e)
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
			}
		}

		//-----------------------------------------------------------------------------
		private void Clear_Click(object sender, RoutedEventArgs e)
		{
			credentials.Username = "";
			credentials.Password = "";
		}

		//-----------------------------------------------------------------------------
		public string PageName
		{
			get { return "BitBucket"; }
		}

		//-----------------------------------------------------------------------------
		public bool Save()
		{
			if (	Credentials.Instance.Username != credentials.Username
				||	Credentials.Instance.Password != credentials.Password
				)
			{
				Credentials.Instance.Username = credentials.Username;
				Credentials.Instance.Password = credentials.Password;
				Credentials.Save();
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public ContentControl PageContent
		{
			get { return this; }
		}

		//-----------------------------------------------------------------------------
		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			credentials.Username = Credentials.Instance.Username;
			credentials.Password = Credentials.Instance.Password;
		}
	}
}
