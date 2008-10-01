using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace HgSccHelper
{
	//=============================================================================
	class ChangeDesc
	{
		public string Author { get; set; }
		public string Desc { get; set; }
		public int Rev { get; set; }
		public DateTime Date { get; set; }
		public List<FileInfo> FilesAdded { get; set; }
		public List<FileInfo> FilesModified { get; set; }
		public List<FileInfo> FilesRemoved { get; set; }

		public ChangeDesc()
		{
			FilesAdded = new List<FileInfo>();
			FilesModified = new List<FileInfo>();
			FilesRemoved = new List<FileInfo>();
		}

		//-----------------------------------------------------------------------------
		public IEnumerable<FileInfo> Files
		{
			get
			{
				foreach (var file in FilesAdded)
					yield return file;

				foreach (var file in FilesModified)
					yield return file;

				foreach (var file in FilesRemoved)
					yield return file;

			}
		}

		//-----------------------------------------------------------------------------
		public static List<ChangeDesc> ParseChanges(StreamReader reader)
		{
			var list = new List<ChangeDesc>();
			ChangeDesc cs = null;

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				if (str.StartsWith("==:"))
				{
					if (cs != null)
						list.Add(cs);

					cs = new ChangeDesc();
					continue;
				}

				if (str.StartsWith("date: "))
				{
					cs.Date = DateTime.Parse(str.Substring("date: ".Length));
					continue;
				}

				if (str.StartsWith("author: "))
				{
					cs.Author = str.Substring("author: ".Length);
					continue;
				}

				if (str.StartsWith("rev: "))
				{
					cs.Rev = Int32.Parse(str.Substring("rev: ".Length));
					continue;
				}

				if (str.StartsWith("desc: "))
				{
					cs.Desc = str.Substring("desc: ".Length);
					continue;
				}

				if (str.Length > 0)
				{
					if (str[0] == '\t')
					{
						cs.Desc += str.Substring(1);
						continue;
					}
				}

				if (str.Length > 2)
				{
					var prefix = str.Substring(0, 2);
					var status = new FileStatus();

					switch (prefix)
					{
						case "A:": status = FileStatus.Added; break;
						case "M:": status = FileStatus.Modified; break;
						case "R:": status = FileStatus.Removed; break;
						default:
							throw new ApplicationException("Unknown prefix: " + prefix + ", " + str);
					}

					str = str.Substring(2);
					string[] files = str.Split(new char[] { ':' });
					foreach (var f in files)
					{
						switch(status)
						{
							case FileStatus.Added:
								{
									cs.FilesAdded.Add(new FileInfo { Status = status, Path = f });
									break;
								}
							case FileStatus.Modified:
								{
									cs.FilesModified.Add(new FileInfo { Status = status, Path = f });
									break;
								}
							case FileStatus.Removed:
								{
									cs.FilesRemoved.Add(new FileInfo { Status = status, Path = f });
									break;
								}
						}
					}
				}
				//--
			}

			if (cs != null)
				list.Add(cs);

			return list;
		}
	}
}
