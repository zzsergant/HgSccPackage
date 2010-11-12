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

using System.IO;
using System.Collections.Generic;
using System;
namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	public enum HgFileStatus
	{
		Added,
		Clean,
		Deleted,
		Ignored,
		Modified,
		NotTracked,
		Removed,
		Tracked,
	}

	//-----------------------------------------------------------------------------
	public class HgFileInfo
	{
		public string File { get; set; }
		public string CopiedFrom { get; set; }
		public HgFileStatus Status { get; set; }

		public string FileViewString
		{
			get
			{
				var str = File;

				if (Status == HgFileStatus.Added || Status == HgFileStatus.Modified)
				{
					if (!String.IsNullOrEmpty(CopiedFrom))
						str += String.Format("\n[copied from: {0}]", CopiedFrom);
				}

				return str;
			}
		}


		//-----------------------------------------------------------------------------
		public static List<HgFileInfo> ParseFileInfo(StreamReader reader)
		{
			var parser = new HgFileInfoParser();

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				parser.AddLine(str);
			}

			return parser.Files;
		}

		//=============================================================================
		public class HgFileInfoParser
		{
			public List<HgFileInfo> Files { get; private set; }
			private HgFileInfo prev;

			//-----------------------------------------------------------------------------
			public HgFileInfoParser()
			{
				Files = new List<HgFileInfo>();
			}

			//-----------------------------------------------------------------------------
			public void Clear()
			{
				Files.Clear();
				prev = null;
			}

			//-----------------------------------------------------------------------------
			public void AddLine(string str)
			{
				if (str == null)
					return;

				if (str.Length < 2)
					return;

				char mod = str[0];
				if (str[1] != ' ')
					return;

				string file_path = str.Substring(2);

				HgFileInfo info = new HgFileInfo();
				info.File = file_path;

				switch (mod)
				{
					case 'M': info.Status = HgFileStatus.Modified; break;
					case 'A': info.Status = HgFileStatus.Added; break;
					case 'R': info.Status = HgFileStatus.Removed; break;
					case '?': info.Status = HgFileStatus.NotTracked; break;
					case '!': info.Status = HgFileStatus.Deleted; break;
					case 'C': info.Status = HgFileStatus.Clean; break;
					case 'I': info.Status = HgFileStatus.Ignored; break;
					case ' ':
						{
							if (prev != null)
							{
								if (prev.Status == HgFileStatus.Added
									|| prev.Status == HgFileStatus.Modified
									)
								{
									prev.CopiedFrom = file_path;
									// Logger.WriteLine(String.Format("File: {0} copied from {1}", prev.File, prev.CopiedFrom));
								}
							}

							prev = null;
							return;
						}
					default:
						return;
				}

				Files.Add(info);
				prev = info;
			}
		}
	}
}