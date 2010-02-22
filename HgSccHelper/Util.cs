//=========================================================================
// Copyright 2009 Sergey Antonov <sergant_@mail.ru>
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
using System.Text;

namespace HgSccHelper
{
	static public class Util
	{
		//-----------------------------------------------------------------------------
		public static string Quote(this string str)
		{
			return "\"" + str + "\"";
		}

		//-----------------------------------------------------------------------------
		public static string UnQuote(this string str)
		{
			if (str.Length >= 2)
			{
				if (str[0] == '\"' && str[str.Length - 1] == '\"')
					return str.Substring(1, str.Length - 2);
			}

			return str;
		}

		//-----------------------------------------------------------------------------
		public static string EscapeQuotes(this string str)
		{
			return str.Replace("\"", "\\\"");
		}

		//-----------------------------------------------------------------------------
		public static string UnEscapeQuotes(this string str)
		{
			return str.Replace("\\\"", "\"");
		}

		//------------------------------------------------------------------
		public static string ShortSHA1(this string str)
		{
			return str.Substring(0, 12);
		}

		//-----------------------------------------------------------------------------
		public static bool GetRelativePath(string working_dir, string path, out string relative)
		{
			relative = string.Empty;
			string f = path.ToLower();
			if (!f.StartsWith(working_dir))
				return false;

			if (path.Length == working_dir.Length)
				relative = "";
			else
				relative = path.Substring(working_dir.Length + 1);
			return true;
		}


		//-----------------------------------------------------------------------------
		public static void HandleHgDiffException()
		{
			System.Windows.Forms.MessageBox.Show("You should set the diff tool in Tools->Options->Source Control->Mercurial Options Page and retry the operation",
				"Information", System.Windows.Forms.MessageBoxButtons.OK,
				System.Windows.Forms.MessageBoxIcon.Information);

			using (var f = new OptionsForm())
			{
				f.ShowDialog();
			}
		}

		//------------------------------------------------------------------
		public static string GetDescription(this RevLogChangeDesc change_desc)
		{
			if (change_desc == null)
				return String.Empty;

			var sha1_short = change_desc.SHA1.ShortSHA1();
			var desc = String.Format("Rev:\t{0} ({1})", change_desc.Rev, sha1_short);

			if (!String.IsNullOrEmpty(change_desc.Branch))
				desc += String.Format("\nBranch:\t{0}", change_desc.Branch);

			foreach (var tag in change_desc.Tags)
			{
				desc += String.Format("\nTag:\t{0}", tag);
			}

			desc += String.Format("\nDesc:\t{0}", change_desc.OneLineDesc);
			return desc;
		}

		//-----------------------------------------------------------------------------
		public static bool IsValidRemoteUrl(string url)
		{
			try
			{
				var uri = new Uri(url);
				if (!uri.IsFile)
				{
					if (!String.IsNullOrEmpty(uri.Host)
						&& !String.IsNullOrEmpty(uri.Scheme)
						)
					{
						return true;
					}
				}
			}
			catch (UriFormatException)
			{
			}

			return false;
		}

		//------------------------------------------------------------------
		public static string MakeRemoteUrl(string url, string username, string password)
		{
			try
			{
				var uri = new Uri(url);
				if (!uri.IsFile)
				{
					if (!String.IsNullOrEmpty(uri.Host)
						&& !String.IsNullOrEmpty(uri.Scheme)
						)
					{
						var builder = new UriBuilder(url);

						if (!String.IsNullOrEmpty(username))
							builder.UserName = username;
						if (!String.IsNullOrEmpty(password))
							builder.Password = password;

						return builder.Uri.AbsoluteUri;
					}
				}
			}
			catch (UriFormatException)
			{
			}

			return url;
		}

		//------------------------------------------------------------------
		public static string RemoveUrlPassword(string path)
		{
			try
			{
				var uri = new Uri(path);
				if (!uri.IsFile)
				{
					if (!String.IsNullOrEmpty(uri.Host)
						&& !String.IsNullOrEmpty(uri.Scheme)
						)
					{
						var builder = new UriBuilder(path);
						if (!String.IsNullOrEmpty(builder.Password))
						{
							builder.Password = "";
							return builder.Uri.AbsoluteUri;
						}
					}
				}
			}
			catch (UriFormatException)
			{
			}

			return path;
		}

		//-----------------------------------------------------------------------------
		public static string HideUrlPassword(string path)
		{
			try
			{
				var uri = new Uri(path);
				if (!uri.IsFile)
				{
					if (!String.IsNullOrEmpty(uri.Host)
						&& !String.IsNullOrEmpty(uri.Scheme)
						)
					{
						var builder = new UriBuilder(path);
						if (!String.IsNullOrEmpty(builder.Password))
						{
							builder.Password = "***";
							return builder.Uri.AbsoluteUri;
						}
					}
				}
			}
			catch (UriFormatException)
			{
			}

			return path;
		}
	}

	//==================================================================
	public class BoolToOppositeBoolConverter : System.Windows.Data.IValueConverter
	{
		//------------------------------------------------------------------
		public object Convert(object value, Type targetType, object parameter,
			System.Globalization.CultureInfo culture)
		{
			if (targetType != typeof(bool))
				throw new InvalidOperationException("The target must be a boolean");

			return !(bool)value;
		}

		//------------------------------------------------------------------
		public object ConvertBack(object value, Type targetType, object parameter,
			System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}