
using System.IO;
using System.Collections.Generic;
using System;
namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	enum HgFileStatus
	{
		Tracked,
		Modified,
		Added,
		Removed,
		Clean,
		Deleted,
		Ignored,
		NotTracked
	}

	//-----------------------------------------------------------------------------
	class HgFileInfo
	{
		public string File { get; set; }
		public string CopiedFrom { get; set; }
		public HgFileStatus Status { get; set; }

		//-----------------------------------------------------------------------------
		public static List<HgFileInfo> ParseFileInfo(StreamReader reader)
		{
			var list = new List<HgFileInfo>();
			HgFileInfo prev = null;

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				if (str.Length < 2)
					continue;

				char mod = str[0];
				string file_path = str.Substring(2);

				HgFileInfo info = new HgFileInfo();
				info.File = file_path;

				switch(mod)
				{
					case 'M': info.Status = HgFileStatus.Modified; break;
					case 'A': info.Status = HgFileStatus.Added; break;
					case 'R': info.Status = HgFileStatus.Removed; break;
					case '?': info.Status = HgFileStatus.NotTracked; break;
					case '!': info.Status = HgFileStatus.Deleted; break;
					case 'C': info.Status = HgFileStatus.Clean; break;
					case ' ':
						{
							if (prev != null && prev.Status == HgFileStatus.Added)
							{
								prev.CopiedFrom = file_path;
//								Logger.WriteLine(String.Format("File: {0} copied from {1}", prev.File, prev.CopiedFrom));
							}

							prev = null;
							continue;
						}
					default:
						continue;
				}

				list.Add(info);
				prev = info;
			}

			return list;
		}
	}
}