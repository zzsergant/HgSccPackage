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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Gajatko.IniFiles;
using System.Net;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for OptionsPageHttpProxy.xaml
	/// </summary>
	public partial class OptionsPageHttpProxy : UserControl, IOptionsPage
	{
		private const string http_proxy = "http_proxy";
		private const string host = "host";
		private const string no = "no";
		private const string user = "user";
		private const string passwd = "passwd";

		//-----------------------------------------------------------------------------
		public OptionsPageHttpProxy()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		private string GetDefaultProxy()
		{
			var proxy = WebRequest.GetSystemWebProxy() as WebProxy;
			if (proxy == null || proxy.Address == null)
				return "";

			return proxy.Address.ToString();
		}

		//-----------------------------------------------------------------------------
		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			var mercurial_ini = Util.GetUserMercurialIni();
			if (mercurial_ini == null)
			{
				textHost.IsEnabled = false;
				textBypass.IsEnabled = false;
				textUsername.IsEnabled = false;
				textPassword.IsEnabled = false;
				return;
			}

			var ini = IniFile.FromFile(mercurial_ini);
			textHost.Text = ini[http_proxy][host];
			textBypass.Text = ini[http_proxy][no];
			textUsername.Text = ini[http_proxy][user];
			textPassword.Password = ini[http_proxy][passwd];

			if (String.IsNullOrEmpty(textHost.Text))
				textHost.Text = GetDefaultProxy();

			textHost.SelectAll();
			textBypass.SelectAll();
			textUsername.SelectAll();
			textPassword.SelectAll();
		}

		//-----------------------------------------------------------------------------
		public string PageName
		{
			get { return "HttpProxy"; }
		}

		//-----------------------------------------------------------------------------
		public bool Save()
		{
			var mercurial_ini = Util.GetUserMercurialIni();
			if (mercurial_ini == null)
				return true;

			var ini = IniFile.FromFile(mercurial_ini);

			bool have_http_proxy = ini.GetSectionNames().Contains(http_proxy);
			bool all_empty = (string.IsNullOrEmpty(textHost.Text) || (textHost.Text == GetDefaultProxy()))
			                 && string.IsNullOrEmpty(textBypass.Text)
			                 && string.IsNullOrEmpty(textUsername.Text)
			                 && string.IsNullOrEmpty(textPassword.Password);

			if (all_empty && !have_http_proxy)
				return true;

			if (all_empty)
			{
				ini[http_proxy].DeleteKey(host);
				ini[http_proxy].DeleteKey(no);
				ini[http_proxy].DeleteKey(user);
				ini[http_proxy].DeleteKey(passwd);
			}
			else
			{
				ini[http_proxy][host] = textHost.Text;
				ini[http_proxy][no] = textBypass.Text;
				ini[http_proxy][user] = textUsername.Text;
				ini[http_proxy][passwd] = textPassword.Password;
			}

			ini.Save(mercurial_ini);
			return true;
		}

		//-----------------------------------------------------------------------------
		public ContentControl PageContent
		{
			get { return this; }
		}
	}
}
