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

namespace HgSccHelper.UI.RevLog
{
	//=============================================================================
	class AsyncParents : IDisposable
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private List<RevLogChangeDesc> parents;

		//-----------------------------------------------------------------------------
		private PendingParentsArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<ParentsInfo> Complete { get; set; }

		//-----------------------------------------------------------------------------
		private RevLogChangeDescParser parser;

		//-----------------------------------------------------------------------------
		private RevLogStyleFile style;

		//-----------------------------------------------------------------------------
		public AsyncParents()
		{
			worker = new HgThread();
			style = new RevLogStyleFile();
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

			parents = null;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir)
		{
			RunAsync(work_dir, "");
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir, string rev)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingParentsArgs
				{
					WorkingDir = work_dir,
					Rev = rev
				};
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("parents");

			args.AppendDebug();

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendStyle(style.FileName);

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

			parents = new List<RevLogChangeDesc>();
			parser = new RevLogChangeDescParser();
			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				var local_changes = parents;
				if (local_changes != null)
				{
					var cs = parser.ParseLine(msg);
					if (cs != null)
						local_changes.Add(cs);
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

				RunAsync(p.WorkingDir, p.Rev);
				return;
			}

			if (!worker.CancellationPending && parents != null && parents.Count > 0)
			{
				if (Complete != null)
				{
					var parents_info = new ParentsInfo {Parents = parents};
					parents = null;
					Complete(parents_info);
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
			style.Dispose();
		}
	}

	//-----------------------------------------------------------------------------
	internal class PendingParentsArgs
	{
		public string WorkingDir { get; set; }
		public string Rev { get; set; }
	}
}
