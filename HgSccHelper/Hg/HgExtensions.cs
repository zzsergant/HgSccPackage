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

namespace HgSccHelper
{
	//=============================================================================
	public class HgExtensions
	{
		private readonly HashSet<string> extensions;

		//-----------------------------------------------------------------------------
		public HgExtensions()
		{
			extensions = DiscoverExtensions();
		}

		//-----------------------------------------------------------------------------
		public IEnumerable<string> GetExtensions()
		{
			return extensions;
		}

		//-----------------------------------------------------------------------------
		public bool IsExtensionEnabled(HgExtension extension)
		{
			var extension_name = HgExtensionNames.GetExtensionName(extension);
			return IsExtensionEnabled(extension_name);
		}

		//-----------------------------------------------------------------------------
		public bool IsExtensionEnabled(string extension_name)
		{
			return extensions.Contains(extension_name);
		}

		//-----------------------------------------------------------------------------
		private static HashSet<string> DiscoverExtensions()
		{
			var extensions = new HashSet<string>();
			var hg = new Hg();
			var lines = hg.ShowConfig("");
			const string extensions_prefix = "extensions";

			var separator = new[] { '=' };

			foreach (var line in lines)
			{
				if (line.StartsWith(extensions_prefix))
				{
					var str = line.Substring(extensions_prefix.Length + 1);

					var parts = str.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 0)
						continue;

					var extension_name = parts[0].Trim();
					extensions.Add(extension_name);
					Logger.WriteLine("Found extension: " + extension_name);
				}
			}

			return extensions;
		}
	}

	//=============================================================================
	public enum HgExtension
	{
		Bookmarks,
		Rebase
	}

	//=============================================================================
	public static class HgExtensionNames
	{
		//-----------------------------------------------------------------------------
		public static string GetExtensionName(HgExtension extension)
		{
			switch (extension)
			{
				case HgExtension.Bookmarks:
					return "bookmarks";
				case HgExtension.Rebase:
					return "rebase";
				default:
					throw new ArgumentOutOfRangeException("extension");
			}
		}
	}

	//=============================================================================
	public static class HgExtensionsCache
	{
		private static readonly HgExtensions extensions;

		//-----------------------------------------------------------------------------
		static HgExtensionsCache()
		{
			extensions = new HgExtensions();
		}

		//-----------------------------------------------------------------------------
		public static HgExtensions Instance
		{
			get { return extensions; }
		}
	}
}
