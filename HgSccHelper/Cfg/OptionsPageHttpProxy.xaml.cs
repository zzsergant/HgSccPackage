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
		public string PageName
		{
			get { return "HttpProxy"; }
		}

		//-----------------------------------------------------------------------------
		public void Init()
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
			textHost.Text = ini[http_proxy][host] ?? "";
			textBypass.Text = ini[http_proxy][no] ?? "";
			textUsername.Text = ini[http_proxy][user] ?? "";
			textPassword.Password = ini[http_proxy][passwd] ?? "";

			textHost.SelectAll();
			textBypass.SelectAll();
			textUsername.SelectAll();
			textPassword.SelectAll();
		}

		//-----------------------------------------------------------------------------
		public bool Save()
		{
			var mercurial_ini = Util.GetUserMercurialIni();
			if (mercurial_ini == null)
			{
				// FIXME: Warn user about this ?
				return true;
			}

			var ini = IniFile.FromFile(mercurial_ini);

			if (	ini[http_proxy][host] != textHost.Text
				||	ini[http_proxy][no] != textBypass.Text
				||	ini[http_proxy][user] != textUsername.Text
				||	ini[http_proxy][passwd] != textPassword.Password
				)
			{
				ini[http_proxy][host] = textHost.Text;
				ini[http_proxy][no] = textBypass.Text;
				ini[http_proxy][user] = textUsername.Text;
				ini[http_proxy][passwd] = textPassword.Password;
				ini.Save(mercurial_ini);
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public ContentControl PageContent
		{
			get { return this; }
		}
	}
}
