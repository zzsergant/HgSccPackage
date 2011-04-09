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
namespace HgSccHelper.BitBucket
{
	//=============================================================================
	/// <summary>
	/// Credentials
	/// </summary>
	public sealed class Credentials
	{
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
			if (Instance.Username != null)
				Cfg.Set("BitBucket", "Username", Instance.Username);

			if (String.IsNullOrEmpty(Instance.Password))
			{
				Cfg.Set("BitBucket", "Password", "");
			}
			else
			{
				byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(Instance.Password),
					null, DataProtectionScope.CurrentUser);

				Cfg.Set("BitBucket", "Password", Convert.ToBase64String(enc));
			}
		}

		//-----------------------------------------------------------------------------
		private void Load()
		{
			string username;
			string password;

			if (Cfg.Get("BitBucket", "Username", out username, ""))
				Username = username;

			if (Cfg.Get("BitBucket", "Password", out password, ""))
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