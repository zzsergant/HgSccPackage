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
	class AsyncChangeDescFull : IDisposable
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private List<ChangeDesc> changes;

		//-----------------------------------------------------------------------------
		private PendingChangeDescFullArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<List<ChangeDesc>> Complete { get; set; }

		//-----------------------------------------------------------------------------
		private ChangeDescParser parser;

		//-----------------------------------------------------------------------------
		private ChangeSetStyleFile style;

		//-----------------------------------------------------------------------------
		public AsyncChangeDescFull()
		{
			worker = new HgThread();
			style = new ChangeSetStyleFile();
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

			changes = null;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir, string path, string rev)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingChangeDescFullArgs
				               	{
				               		WorkingDir = work_dir,
									Path = path,
									Rev = rev
				               	};
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("log");

			args.AppendDebug();
			args.AppendVerbose();
			args.Append("--follow");

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendStyle(style.FileName);

			if (path.Length > 0)
			{
				args.AppendPath(path);
			}

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

			changes = new List<ChangeDesc>();
			parser = new ChangeDescParser();
			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				var cs = parser.ParseLine(msg);
				if (cs != null)
					changes.Add(cs);
			}
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			if (pending_args != null)
			{
				var p = pending_args;
				pending_args = null;

				RunAsync(p.WorkingDir, p.Path, p.Rev);
				return;
			}

			if (!worker.CancellationPending && changes != null)
			{
				if (Complete != null)
				{
					var new_changes = changes;
					changes = null;
					Complete(new_changes);
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
	internal class PendingChangeDescFullArgs
	{
		public string WorkingDir { get; set; }
		public string Path { get; set; }
		public string Rev { get; set; }
	}
}
