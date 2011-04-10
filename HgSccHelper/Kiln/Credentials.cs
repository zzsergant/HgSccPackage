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
using System.Security.Cryptography;
using System.Text;

//=============================================================================
namespace HgSccHelper.Kiln
{
	//=============================================================================
	/// <summary>
	/// Credentials
	/// </summary>
	public sealed class Credentials
	{
		public string Site { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }

		static readonly Credentials instance = new Credentials();

		// Explicit static constructor to tell C# compiler
		// not to mark type as beforefieldinit
		static Credentials()
		{
		}

		//-----------------------------------------------------------------------------
		private Credentials()
		{
			Site = "";
			Username = "";
			Password = "";

			Load();
		}

		//-----------------------------------------------------------------------------
		public static Credentials Instance
		{
			get
			{
				return instance;
			}
		}

		//-----------------------------------------------------------------------------
		public static void Save()
		{
			if (Instance.Site != null)
				Cfg.Set("Kiln", "Site", Instance.Site);

			if (Instance.Username != null)
				Cfg.Set("Kiln", "Username", Instance.Username);

			if (String.IsNullOrEmpty(Instance.Password))
			{
				Cfg.Set("Kiln", "Password", "");
			}
			else
			{
				byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(Instance.Password),
					null, DataProtectionScope.CurrentUser);

				Cfg.Set("Kiln", "Password", Convert.ToBase64String(enc));
			}
		}

		//-----------------------------------------------------------------------------
		private void Load()
		{
			string site;
			string username;
			string password;

			if (Cfg.Get("Kiln", "Site", out site, ""))
				Site = site;

			if (Cfg.Get("Kiln", "Username", out username, ""))
				Username = username;

			if (Cfg.Get("Kiln", "Password", out password, ""))
			{
				if (String.IsNullOrEmpty(password))
				{
					Password = password;
				}
				else
				{
					byte[] dec = ProtectedData.Unprotect(Convert.FromBase64String(password), null,
						DataProtectionScope.CurrentUser);

					Password = Encoding.UTF8.GetString(dec);
				}
			}
		}
	}
}
