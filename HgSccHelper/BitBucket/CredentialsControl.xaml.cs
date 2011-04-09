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

using System.Windows.Controls;

namespace HgSccHelper.BitBucket
{
	/// <summary>
	/// Interaction logic for CredentialsControl.xaml
	/// </summary>
	public partial class CredentialsControl : UserControl
	{
		public CredentialsControl()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		public string Username
		{
			get { return textUsername.Text; }
			set { textUsername.Text = value; }
		}

		//-----------------------------------------------------------------------------
		public string Password
		{
			get { return textPassword.Password; }
			set { textPassword.Password = value; }
		}
	}
}
