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
using Microsoft.Win32;
using System.Linq;
using System.IO;

namespace HgSccHelper
{
	static public class Util
	{
		//-----------------------------------------------------------------------------
		public static string Quote(this string str)
		{
			if (str.Length == 0)
				return str;

			if (str.Length >= 2)
			{
				if (str[0] == '\"' && str[str.Length - 1] == '\"')
					return str;
			}

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

		//------------------------------------------------------------------
		public static string GetTempFileNameForFile(string file)
		{
			string temp_folder = Path.GetTempPath();
			string temp_name = Path.GetRandomFileName() + "_" + Path.GetFileName(file);
			string temp_file = Path.Combine(temp_folder, temp_name);

			return temp_file;
		}

		//------------------------------------------------------------------
		public static bool FindOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
			TKey key, ref TValue v)
		{
			TValue found;
			if (dictionary.TryGetValue(key, out found))
			{
				v = found;
				return true;
			}

			dictionary[key] = v;
			return false;
		}

		//------------------------------------------------------------------
		public static T[] ToArray<T>(this HashSet<T> hash_set)
		{
			var array = new T[hash_set.Count];
			hash_set.CopyTo(array);
			return array;
		}

		//-----------------------------------------------------------------------------
		public static bool GetRelativePath(string working_dir, string path, out string relative)
		{
			relative = string.Empty;
			string f = path.ToLower();
			if (!f.StartsWith(working_dir.ToLower()))
				return false;

			if (path.Length == working_dir.Length)
				relative = "";
			else
				relative = path.Substring(working_dir.Length + 1);
			return true;
		}

		public delegate void HgDiffExceptionHandler();
		public static HgDiffExceptionHandler DiffExceptionHandler { get; set; }

		//-----------------------------------------------------------------------------
		public static void HandleHgDiffException()
		{
			if (DiffExceptionHandler != null)
			{
				DiffExceptionHandler();
			}
			else
			{
				throw new HgDiffException();
			}
		}

		//-----------------------------------------------------------------------------
		public static int FirstIndexOf<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
		{
			int pos = 0;
			foreach (var v in enumerable)
			{
				if (predicate(v))
					return pos;

				pos++;
			}

			return -1;
		}

		//------------------------------------------------------------------
		public static string GetDescription(this RevLogChangeDesc change_desc)
		{
			if (change_desc == null)
				return String.Empty;

			var sha1_short = change_desc.SHA1.ShortSHA1();
			var desc = String.Format("Rev:\t{0} ({1})", change_desc.Rev, sha1_short);

			if (change_desc.Branch != null)
				desc += String.Format("\nBranch:\t{0}", change_desc.Branch);

			if (change_desc.Tags.Count > 0)
			{
				var tag_names = change_desc.Tags.Select(t => t.Name).ToArray();
				desc += String.Format("\nTags:\t{0}", string.Join(", ", tag_names));
			}

			if (change_desc.Bookmarks.Count > 0)
			{
				var book_names = change_desc.Bookmarks.Select(b => b.Name).ToArray();
				desc += String.Format("\nBooks:\t{0}", string.Join(", ", book_names));
			}

			if (!String.IsNullOrEmpty(change_desc.OneLineDesc))
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

		//-----------------------------------------------------------------------------
		static public string LookupRegistry(string reg_key_name, string val_name)
		{
			var reg_key = Registry.CurrentUser.OpenSubKey(reg_key_name, RegistryKeyPermissionCheck.ReadSubTree);
			if (reg_key == null)
				reg_key = Registry.LocalMachine.OpenSubKey(reg_key_name, RegistryKeyPermissionCheck.ReadSubTree);

			if (reg_key != null)
			{
				var val = reg_key.GetValue(val_name);
				if (val != null)
					return val.ToString();
			}

			return "";
		}

		//-----------------------------------------------------------------------------
		static public string FindExisting(string pathcommand)
		{
			return FindExisting(pathcommand, new[] { "" });
		}

		//-----------------------------------------------------------------------------
		static public string FindExisting(string pathcommand, string[] pathexts)
		{
			foreach (var ext in pathexts)
			{
				var exectutable = pathcommand + ext;
				if (System.IO.File.Exists(exectutable))
					return exectutable;
			}

			return "";
		}

		//-----------------------------------------------------------------------------
		static public string FindExe(string command)
		{
			command = command.UnQuote();

			var pathext = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
			var path_sep = new[] { ';' };
			var pathexts = pathext.ToLower().Split(path_sep, StringSplitOptions.RemoveEmptyEntries);

			var command_ext = System.IO.Path.GetExtension(command).ToLower();
			if (pathexts.Contains(command_ext))
				pathexts = new string[] { "" };

			if (command.Contains('/') || command.Contains('\\'))
				return FindExisting(command, pathexts);

			var path_env = Environment.GetEnvironmentVariable("PATH") ?? "";
			foreach (var path in path_env.Split(path_sep, StringSplitOptions.RemoveEmptyEntries))
			{
				var executable = FindExisting(System.IO.Path.Combine(path.UnQuote(), command), pathexts);
				if (!String.IsNullOrEmpty(executable))
					return executable;
			}

			return FindExisting(Environment.ExpandEnvironmentVariables(command), pathexts);
		}

		//------------------------------------------------------------------
		public static void QueueThreadPoolFn(DeferredThreadExecuteDelegate fn)
		{
			System.Threading.ThreadPool.QueueUserWorkItem(DeferredThreadExecute, fn);
		}

		//------------------------------------------------------------------
		public delegate void DeferredThreadExecuteDelegate();

		//------------------------------------------------------------------
		private static void DeferredThreadExecute(object fn)
		{
			var function = fn as DeferredThreadExecuteDelegate;
			function();
		}

		//------------------------------------------------------------------
		public static string Convert(string value, Encoding src, Encoding trg)
		{
			Decoder dec = src.GetDecoder();
			byte[] ba = trg.GetBytes(value);
			int len = dec.GetCharCount(ba, 0, ba.Length);
			char[] ca = new char[len];
			dec.GetChars(ba, 0, ba.Length, ca, 0);
			return new string(ca);
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