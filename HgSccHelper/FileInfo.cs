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
using System.IO;

//=============================================================================
namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	enum FileStatus
	{
		None,
		Added,
		Modified,
		Removed
	}

	//-----------------------------------------------------------------------------
	class CopiesInfo
	{
		public string FromFile { get; set; }
		public string ToFile { get; set; }

		//-----------------------------------------------------------------------------
		public static List<CopiesInfo> ParseCopiesInfo(StreamReader reader)
		{
			var list = new List<CopiesInfo>();
			var copy_prefix = "copy: ";
			var copy_delimiter = new string[] { " -> " };

			while (true)
			{
				var str = reader.ReadLine();
				if (str == null)
					break;

				if (!str.StartsWith(copy_prefix))
					continue;

				var files = str.Substring(copy_prefix.Length).Split(copy_delimiter, StringSplitOptions.RemoveEmptyEntries);
				if (files.Length != 2)
					continue;

				list.Add(new CopiesInfo { FromFile = files[0], ToFile = files[1] });
			}

			return list;
		}
	}


	//=============================================================================
	class FileInfo
	{
		public FileStatus Status { get;set; }
		public string Path { get; set; }

		//-----------------------------------------------------------------------------
		public static List<FileInfo> ParseFileInfo(StreamReader reader)
		{
			var list = new List<FileInfo>();

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				if (str.Length < 3)
					continue;

				var prefix = str.Substring(0, 2);
				var status = new FileStatus();

				switch (prefix)
				{
					case "A:": status = FileStatus.Added; break;
					case "M:": status = FileStatus.Modified; break;
					case "R:": status = FileStatus.Removed; break;
					default:
						throw new ApplicationException("Unknown prefix: " + prefix);
				}

				str = str.Substring(2);
				string[] files = str.Split(new char[] { ':' });
				foreach (var f in files)
					list.Add(new FileInfo { Status = status, Path = f });
			}

			return list;
		}
	}
}
