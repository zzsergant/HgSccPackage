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
		public DateTime Date { get; set; }
		public string Branch { get; set; }

		public ObservableCollection<string> Parents { get; private set; }
		public ObservableCollection<TagInfo> Tags { get; set; }
		public ObservableCollection<BookmarkInfo> Bookmarks { get; set; }

		//------------------------------------------------------------------
		public RevLogChangeDesc()
		{
			Parents = new ObservableCollection<string>();
			Tags = new ObservableCollection<TagInfo>();
			Bookmarks = new ObservableCollection<BookmarkInfo>();
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
				builder.Append(@"bookmarks: {bookmarks}\n");
				builder.Append(@"parents: {parents}\n");
				builder.Append(@"::=\n");

				stream.WriteLine(String.Format("changeset_verbose = '{0}'", builder.ToString()));
				stream.WriteLine(@"tag = '{tag}:'");
				stream.WriteLine(@"last_tag = '{tag}'");
				stream.WriteLine(@"bookmark = '{bookmark}:'");
				stream.WriteLine(@"last_bookmark = '{bookmark}'");
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
		RevLogChangeDesc cs;
		StringBuilder desc_builder;

		static readonly char[] ParentSeparators = new[] { ':', ' ' };
		static readonly char[] TagSeparator = new [] { ':' };

		//------------------------------------------------------------------
		public RevLogChangeDescParser()
		{
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
				string[] tags = tags_str.Split(TagSeparator, StringSplitOptions.RemoveEmptyEntries);
				foreach (var tag in tags)
					cs.Tags.Add(new TagInfo { Name = tag, Rev = cs.Rev, SHA1 = cs.SHA1 });
				return null;
			}

			if (str.StartsWith("bookmarks: "))
			{
				var books_str = str.Substring("bookmarks: ".Length);
				string[] books = books_str.Split(TagSeparator, StringSplitOptions.RemoveEmptyEntries);
				foreach (var book in books)
					cs.Bookmarks.Add(new BookmarkInfo { Name = book, Rev = cs.Rev, SHA1 = cs.SHA1 });
				return null;
			}

			if (str.StartsWith("parents: "))
			{
				var parents_strs = str.Substring("parents: ".Length).Split(ParentSeparators, StringSplitOptions.RemoveEmptyEntries);
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
