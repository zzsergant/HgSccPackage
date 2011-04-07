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
using System.Diagnostics;

namespace HgSccHelper
{
	//=============================================================================
	class HgBookmarks
	{
		//-----------------------------------------------------------------------------
		public static BookmarkInfo ParseBookmarkLine(string str)
		{
			if (String.IsNullOrEmpty(str))
				return null;

			var bookmark = new BookmarkInfo();
			const string current_prefix = " * ";

			if (str.StartsWith(current_prefix))
			{
				bookmark.IsCurrent = true;
			}

			str = str.Substring(3);

			int rev_start = str.LastIndexOf(' ') + 1;
			bookmark.Name = str.Substring(0, rev_start).Trim();

			var separators = new char[] { ':' };
			var rev_parts = str.Substring(rev_start).Split(separators, StringSplitOptions.RemoveEmptyEntries);

			if (rev_parts.Length == 2)
			{
				int revision;
				if (int.TryParse(rev_parts[0], out revision))
				{
					bookmark.Rev = revision;
					bookmark.SHA1 = rev_parts[1];

					return bookmark;
				}
			}

			return null;
		}

		//------------------------------------------------------------------
		public List<BookmarkInfo> Bookmarks(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("bookmarks");
			args.AppendDebug();

			var bookmarks = new List<BookmarkInfo>();

			var hg = new Hg();
			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					var bookmark = ParseBookmarkLine(str);
					if (bookmark != null)
						bookmarks.Add(bookmark);
				}

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<BookmarkInfo>();
			}

			return bookmarks;
		}

		//-----------------------------------------------------------------------------
		public bool Add(string work_dir, string bookmark, string revision)
		{
			var args = new HgArgsBuilder();
			args.Append("bookmarks");

			if (revision.Length != 0)
				args.AppendRevision(revision);

			args.Append(bookmark.Quote());

			if (args.Length > Hg.MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			var hg = new Hg();

			var info = hg.PrepareProcess(work_dir, args.ToString());
			info.RedirectStandardOutput = false;
			info.RedirectStandardError = false;

			using (Process proc = Process.Start(info))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool Remove(string work_dir, string bookmark)
		{
			var args = new HgArgsBuilder();
			args.Append("bookmarks");
			args.Append("--delete");

			args.Append(bookmark.Quote());

			if (args.Length > Hg.MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			var hg = new Hg();

			var info = hg.PrepareProcess(work_dir, args.ToString());
			info.RedirectStandardOutput = false;
			info.RedirectStandardError = false;

			using (Process proc = Process.Start(info))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool Rename(string work_dir, string old_bookmark, string new_bookmark)
		{
			var args = new HgArgsBuilder();
			args.Append("bookmarks");
			args.Append("--rename");

			args.Append(old_bookmark.Quote());
			args.Append(new_bookmark.Quote());

			if (args.Length > Hg.MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			var hg = new Hg();

			var info = hg.PrepareProcess(work_dir, args.ToString());
			info.RedirectStandardOutput = false;
			info.RedirectStandardError = false;

			using (Process proc = Process.Start(info))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}
	}

	//------------------------------------------------------------------
	public class BookmarkInfo
	{
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public bool IsCurrent { get; set; }
	}
}
