﻿//=========================================================================
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
using System.Collections.ObjectModel;

namespace HgSccHelper
{
	//=============================================================================
	public class ChangeDesc
	{
		public string Author { get; set; }
		public string Desc { get; set; }
		public string OneLineDesc { get; set; }
		public int Rev { get; set; }
		public DateTime Date { get; set; }
		public string SHA1 { get; set; }
		public List<FileInfo> FilesAdded { get; set; }
		public List<FileInfo> FilesModified { get; set; }
		public List<FileInfo> FilesRemoved { get; set; }
		public ObservableCollection<string> Tags { get; set; }

		public ChangeDesc()
		{
			FilesAdded = new List<FileInfo>();
			FilesModified = new List<FileInfo>();
			FilesRemoved = new List<FileInfo>();
			Tags = new ObservableCollection<string>();
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

		//------------------------------------------------------------------
		private static void RemoveDuplicates(Dictionary<string, FileInfo> dict, List<FileInfo> files)
		{
			foreach (var file in files)
			{
				dict.Remove(file.Path);
			}
		}

		//-----------------------------------------------------------------------------
		public static List<ChangeDesc> ParseChanges(StreamReader reader)
		{
			var list = new List<ChangeDesc>();
			var modified_files = new Dictionary<string, FileInfo>();
			ChangeDesc cs = null;
			var desc_builder = new StringBuilder();

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				if (str.StartsWith("==:"))
				{
					cs = new ChangeDesc();
					continue;
				}

				if (str.StartsWith("::="))
				{
					if (cs != null)
					{
						RemoveDuplicates(modified_files, cs.FilesAdded);
						RemoveDuplicates(modified_files, cs.FilesRemoved);
						foreach (var info in modified_files.Values)
						{
							cs.FilesModified.Add(info);
						}

						cs.Desc = desc_builder.ToString();

						list.Add(cs);
						modified_files.Clear();
					}

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
				
				if (str.StartsWith("node: "))
				{
					cs.SHA1 = str.Substring("node: ".Length);
					continue;
				}

				if (str.StartsWith("tags: "))
				{
					var tags_str = str.Substring("tags: ".Length);
					string[] tags = tags_str.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var tag in tags)
						cs.Tags.Add(tag);
					continue;
				}

				if (str.StartsWith("desc: "))
				{
					cs.OneLineDesc = str.Substring("desc: ".Length);
					desc_builder.Remove(0, desc_builder.Length);
					desc_builder.AppendLine(cs.OneLineDesc);
					continue;
				}

				if (str.Length > 0)
				{
					if (str[0] == '\t')
					{
						desc_builder.AppendLine(str.Substring(1));
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
					string[] files = str.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
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
									// cs.FilesModified.Add(new FileInfo { Status = status, Path = f });
									modified_files.Add(f, new FileInfo { Status = status, Path = f });
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

			return list;
		}
	}

	//==================================================================
	public class ChangeSetStyleFile : IDisposable
	{
		public string FileName { get; private set; }

		//------------------------------------------------------------------
		public ChangeSetStyleFile()
		{
			FileName = Path.GetTempFileName();
			//Logger.WriteLine("Creating temp file: " + FileName);

			using (var stream = new StreamWriter(File.OpenWrite(FileName)))
			{
				stream.WriteLine(@"changeset_verbose = '==:\ndate: {date|isodate}\nauthor: {author}\ndesc: {desc|strip|tabindent}\nrev: {rev}\nnode: {node}\ntags: {tags}\nA:{file_adds}\nR:{file_dels}\nM:{files}\n::=\n'");
				stream.WriteLine(@"file = '{file}:'");
				stream.WriteLine(@"last_file = '{file}'");
				stream.WriteLine(@"file_add = '{file_add}:'");
				stream.WriteLine(@"last_file_add = '{file_add}'");
				stream.WriteLine(@"file_del = '{file_del}:'");
				stream.WriteLine(@"last_file_del = '{file_del}'");
				stream.WriteLine(@"tag = '{tag}:'");
				stream.WriteLine(@"last_tag = '{tag}'");
				// stream.WriteLine(@"branches = '{branches}:'");
			}
		}

		//------------------------------------------------------------------
		public void Dispose()
		{
			File.Delete(FileName);
		}
	}
}
