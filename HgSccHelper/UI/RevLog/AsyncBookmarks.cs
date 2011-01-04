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
using System.Linq;
using System.Text;

namespace HgSccHelper.UI.RevLog
{
	class AsyncBookmarks
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private List<BookmarkInfo> bookmarks;

		//-----------------------------------------------------------------------------
		private PendingBookmarksArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<List<BookmarkInfo>> Complete { get; set; }

		//-----------------------------------------------------------------------------
		public AsyncBookmarks()
		{
			worker = new HgThread();
		}

		//-----------------------------------------------------------------------------
		public void Clear()
		{
			Cancel();
		}

		//-----------------------------------------------------------------------------
		public void Cancel()
		{
			if (worker.IsBusy)
				worker.Cancel();

			bookmarks = null;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingBookmarksArgs { WorkingDir = work_dir};
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("bookmarks");
			args.AppendDebug();

			RunHgThread(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		private void RunHgThread(string work_dir, string args)
		{
			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = work_dir;
			p.Args = args;

			bookmarks = new List<BookmarkInfo>();
			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				var local_bookmarks = bookmarks;
				if (local_bookmarks != null)
				{
					var tag = HgBookmarks.ParseBookmarkLine(msg);
					if (tag != null)
						local_bookmarks.Add(tag);
				}
			}
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			if (pending_args != null)
			{
				var p = pending_args;
				pending_args = null;

				RunAsync(p.WorkingDir);
				return;
			}

			Logger.WriteLine("AsyncBookmarks exit code: {0}", completed.ExitCode);

			if (!worker.CancellationPending && bookmarks != null && completed.ExitCode == 0)
			{
				if (Complete != null)
				{
					var new_bookmarks = bookmarks;
					bookmarks = null;
					Complete(new_bookmarks);
					return;
				}
			}

			if (Complete != null)
				Complete(null);
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			worker.Cancel();
			worker.Dispose();
		}
	}

	//-----------------------------------------------------------------------------
	internal class PendingBookmarksArgs
	{
		public string WorkingDir { get; set; }
	}
}
