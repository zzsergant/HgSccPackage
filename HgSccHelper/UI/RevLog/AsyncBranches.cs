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
	class AsyncBranches
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private List<BranchInfo> branches;

		//-----------------------------------------------------------------------------
		private PendingBranchArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<List<BranchInfo>> Complete { get; set; }

		//-----------------------------------------------------------------------------
		public AsyncBranches()
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

			branches = null;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir, HgBranchesOptions options)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingBranchArgs { WorkingDir = work_dir, Options = options};
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("branches");
			args.AppendDebug();
			args.AppendVerbose();

			if ((options & HgBranchesOptions.Active) == HgBranchesOptions.Active)
				args.Append("-a");
			if ((options & HgBranchesOptions.Closed) == HgBranchesOptions.Closed)
				args.Append("-c");

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

			branches = new List<BranchInfo>();
			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				var branch = Hg.ParseBranchLine(msg);
				if (branch != null)
					branches.Add(branch);
			}
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			if (pending_args != null)
			{
				var p = pending_args;
				pending_args = null;

				RunAsync(p.WorkingDir, p.Options);
				return;
			}

			if (!worker.CancellationPending && branches != null)
			{
				if (Complete != null)
				{
					var new_branches = branches;
					branches = null;
					Complete(new_branches);
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
	internal class PendingBranchArgs
	{
		public string WorkingDir { get; set; }
		public HgBranchesOptions Options { get; set; }
	}
}
