//=========================================================================
// Copyright 2010 Sergey Antonov <sergant_@mail.ru>
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
	class AsyncResolveList
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private List<ResolveInfo> resolve_list;

		//-----------------------------------------------------------------------------
		private PendingResolveListArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<List<ResolveInfo>> Complete { get; set; }

		//-----------------------------------------------------------------------------
		public AsyncResolveList()
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

			resolve_list = null;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingResolveListArgs { WorkingDir = work_dir };
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("resolve");
			args.Append("-l");

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

			resolve_list = new List<ResolveInfo>();
			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				var info = HgResolve.ParseResolveListLine(msg);
				if (info != null)
					resolve_list.Add(info);
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

			if (!worker.CancellationPending && resolve_list != null)
			{
				if (Complete != null)
				{
					var new_resolve = resolve_list;
					resolve_list = null;
					Complete(new_resolve);
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
	internal class PendingResolveListArgs
	{
		public string WorkingDir { get; set; }
	}
}
