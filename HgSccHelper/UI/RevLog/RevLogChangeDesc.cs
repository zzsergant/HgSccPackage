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
using System.IO;
using System.Text;
using System.Collections.ObjectModel;

//==================================================================
namespace HgSccHelper
{
	//==================================================================
	public class RevLogChangeDesc
	{
		public string Author { get; set; }
		public string Desc { get; set; }
		public string OneLineDesc { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public ObservableCollection<string> Parents { get; private set; }
		public DateTime Date { get; set; }
		public string Branch { get; set; }
		public ObservableCollection<string> Tags { get; set; }
		public ObservableCollection<string> Bookmarks { get; set; }

		//------------------------------------------------------------------
		public RevLogChangeDesc()
		{
			Parents = new ObservableCollection<string>();
			Tags = new ObservableCollection<string>();
			Bookmarks = new ObservableCollection<string>();
		}

		//------------------------------------------------------------------
		private static string CutPrefix(string str, string prefix)
		{
			return str.Remove(0, prefix.Length);
		}

		//-----------------------------------------------------------------------------
		public static List<RevLogChangeDesc> ParseChangesHgk(StreamReader reader)
		{
			var list = new List<RevLogChangeDesc>();
			RevLogChangeDesc cs = null;
			bool is_start = true;
			var separator = new[]{' '};
			var desc = new StringBuilder();

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				if (str.Length == 0)
					continue;

				if (str[0] == '\0')
				{
					str = str.Substring(1);
					cs.Desc = desc.ToString();
					desc.Remove(0, desc.Length);

					list.Add(cs);
					cs = null;
					is_start = true;
				}

				if (is_start)
				{
					var sha1 = str.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					if (sha1.Length == 0)
					{
						return list;
					}

					cs = new RevLogChangeDesc();
					cs.SHA1 = sha1[0];
					is_start = false;
					continue;
				}

				if (str.StartsWith("parent "))
				{
					cs.Parents.Add(str.Substring("parent ".Length));
					continue;
				}

				if (str.StartsWith("author "))
				{
					var s = str.Substring("author ".Length);
					var split = s.Split(separator, StringSplitOptions.RemoveEmptyEntries);

					cs.Author = string.Join(" ", split, 0, split.Length - 2);
					var date_time = new[] { split[split.Length - 2], split[split.Length - 1] };

					var date = new DateTime(1970, 1, 1).AddSeconds(Int32.Parse(date_time[0]));
//					var date_time_offset = new DateTimeOffset(date, new TimeSpan(0, 0, ));
//					cs.Date = date_time_offset.LocalDateTime;
					cs.Date = DateTime.SpecifyKind(date.AddSeconds(-Int32.Parse(date_time[1])), DateTimeKind.Utc);
					continue;
				}

				if (str.StartsWith("revision "))
				{
					cs.Rev = Int32.Parse(str.Substring("revision ".Length));
					continue;
				}

				if (str.StartsWith("branch "))
				{
					cs.Branch = str.Substring("branch ".Length);
					continue;
				}

				if (str.StartsWith("    "))
				{
					var s = str.Substring(4);
					if (String.IsNullOrEmpty(cs.OneLineDesc))
						cs.OneLineDesc = s;
					desc.AppendLine(s);
				}
				//--
			}

			if (cs != null)
			{
				cs.Desc = desc.ToString();
				desc.Remove(0, desc.Length);

				list.Add(cs);
			}

			return list;
		}

		//-----------------------------------------------------------------------------
		public static List<RevLogChangeDesc> ParseChanges(StreamReader reader)
		{
			var list = new List<RevLogChangeDesc>();
			var parser = new RevLogChangeDescParser();

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				var cs = parser.ParseLine(str);
				if (cs != null)
					list.Add(cs);
			}

			return list;
		}
	}

	//==================================================================
	public class RevLogStyleFile : IDisposable
	{
		public string FileName { get; private set; }

		//------------------------------------------------------------------
		public RevLogStyleFile()
		{
			FileName = Path.GetTempFileName();
			//Logger.WriteLine("Creating temp file: " + FileName);

			using (var stream = new StreamWriter(File.OpenWrite(FileName)))
			{
				var builder = new StringBuilder();
				builder.Append(@"==:\n");
				builder.Append(@"date: {date|isodate}\n");
				builder.Append(@"author: {author}\n");
				builder.Append(@"desc: {desc|strip|tabindent}\n");
				builder.Append(@"rev: {rev}\n");
				builder.Append(@"node: {node}\n");
				builder.Append(@"branch: {branches}\n");
				builder.Append(@"tags: {tags}\n");
				builder.Append(@"parents: {parents}\n");
				builder.Append(@"::=\n");

				stream.WriteLine(String.Format("changeset_verbose = '{0}'", builder.ToString()));
				stream.WriteLine(@"tag = '{tag}:'");
				stream.WriteLine(@"last_tag = '{tag}'");
			}
		}

		//------------------------------------------------------------------
		public void Dispose()
		{
			File.Delete(FileName);
		}
	}


	//------------------------------------------------------------------
	public class RevLogChangeDescParser
	{
		RevLogChangeDesc cs = null;
		char[] parent_sep;
		StringBuilder desc_builder;

		//------------------------------------------------------------------
		public RevLogChangeDescParser()
		{
			parent_sep = new char[] { ':', ' ' };
			desc_builder = new StringBuilder();
		}

		//-----------------------------------------------------------------------------
		public RevLogChangeDesc ParseLine(string str)
		{
			// str may be broken, if background process stopped by user,
			// so we need to use TryParse() intead of Parse methods

			if (str == null)
				return null;

			if (str.StartsWith("==:"))
			{
				cs = new RevLogChangeDesc();
				return null;
			}

			if (str.StartsWith("::="))
			{
				if (cs != null)
				{
					cs.Desc = desc_builder.ToString();
					return cs;
				}
				return null;
			}

			if (str.StartsWith("date: "))
			{
				DateTime date_time;
				if (DateTime.TryParse(str.Substring("date: ".Length), out date_time))
					cs.Date = date_time;
				return null;
			}

			if (str.StartsWith("author: "))
			{
				cs.Author = str.Substring("author: ".Length);
				return null;
			}

			if (str.StartsWith("rev: "))
			{
				int rev;
				if (Int32.TryParse(str.Substring("rev: ".Length), out rev))
					cs.Rev = rev;
				return null;
			}

			if (str.StartsWith("node: "))
			{
				cs.SHA1 = str.Substring("node: ".Length);
				return null;
			}

			if (str.StartsWith("desc: "))
			{
				cs.OneLineDesc = str.Substring("desc: ".Length);
				desc_builder.Remove(0, desc_builder.Length);
				desc_builder.AppendLine(cs.OneLineDesc);
				return null;
			}

			if (str.StartsWith("branch: "))
			{
				cs.Branch = str.Substring("branch: ".Length);
				if (cs.Branch.Length == 0)
					cs.Branch = "default";

				return null;
			}

			if (str.StartsWith("tags: "))
			{
				var tags_str = str.Substring("tags: ".Length);
				string[] tags = tags_str.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var tag in tags)
					cs.Tags.Add(tag);
				return null;
			}

			if (str.StartsWith("parents: "))
			{
				var parents_strs = str.Substring("parents: ".Length).Split(parent_sep, StringSplitOptions.RemoveEmptyEntries);
				if (parents_strs.Length == 4)
				{
					if (parents_strs[0] != "-1")
						cs.Parents.Add(parents_strs[1]);
					if (parents_strs[2] != "-1")
						cs.Parents.Add(parents_strs[3]);
				}
				return null;
			}

			if (str.Length > 0)
			{
				if (str[0] == '\t')
				{
					desc_builder.AppendLine(str.Substring(1));
					return null;
				}
			}

			return null;
		}
	}
}
