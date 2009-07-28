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
		public List<string> Parents { get; private set; }
		public DateTime Date { get; set; }
		public string Branch { get; set; }

		//------------------------------------------------------------------
		public RevLogChangeDesc()
		{
			Parents = new List<string>();
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
			RevLogChangeDesc cs = null;
			var parent_sep = new char[] { ':', ' ' };

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				if (str.StartsWith("==:"))
				{
					if (cs != null)
					{
						list.Add(cs);
					}

					cs = new RevLogChangeDesc();
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

				if (str.StartsWith("desc: "))
				{
					cs.Desc = str.Substring("desc: ".Length);
					cs.OneLineDesc = cs.Desc;
					continue;
				}

				if (str.StartsWith("branch: "))
				{
					cs.Branch = str.Substring("branch: ".Length);
					if (cs.Branch.Length == 0)
						cs.Branch = "default";

					continue;
				}

				if (str.StartsWith("parents: "))
				{
					var parents_strs = str.Substring("parents: ".Length).Split(parent_sep);
					if (parents_strs[0] != "-1")
						cs.Parents.Add(parents_strs[1]);
					if (parents_strs[2] != "-1")
						cs.Parents.Add(parents_strs[3]);
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

				//--
			}

			if (cs != null)
			{
				list.Add(cs);
			}

			return list;
		}
	}
}
