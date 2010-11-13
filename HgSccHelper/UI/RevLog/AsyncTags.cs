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
using System.Linq;
using System.Text;

namespace HgSccHelper.UI.RevLog
{
	class AsyncTags
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private List<TagInfo> tags;

		//-----------------------------------------------------------------------------
		private PendingTagsArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<List<TagInfo>> Complete { get; set; }

		//-----------------------------------------------------------------------------
		public AsyncTags()
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

			tags = null;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingTagsArgs { WorkingDir = work_dir};
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("tags");
			args.AppendVerbose();
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

			tags = new List<TagInfo>();
			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				var tag = Hg.ParseTagLine(msg);
				if (tag != null)
					tags.Add(tag);
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

			if (!worker.CancellationPending && tags != null)
			{
				if (Complete != null)
				{
					var new_tags = tags;
					tags = null;
					Complete(new_tags);
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
	internal class PendingTagsArgs
	{
		public string WorkingDir { get; set; }
	}
}
