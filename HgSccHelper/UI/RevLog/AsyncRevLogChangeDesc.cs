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

//=============================================================================
namespace HgSccHelper.UI.RevLog
{
	//=============================================================================
	class AsyncRevLogChangeDesc : IDisposable
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private RevLogChangeDesc thread_changedesc;

		//-----------------------------------------------------------------------------
		private readonly RevLogStyleFile revlog_style;

		//-----------------------------------------------------------------------------
		private RevLogChangeDescParser rev_log_parser;

		//-----------------------------------------------------------------------------
		private PendingRevLogChangeDescArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<AsyncRevLogChangeDescResult> Complete { get; set; }

		//-----------------------------------------------------------------------------
		public AsyncRevLogChangeDesc()
		{
			worker = new HgThread();
			revlog_style = new RevLogStyleFile();
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
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			worker.Cancel();
			worker.Dispose();
			revlog_style.Dispose();
		}

		//-----------------------------------------------------------------------------
		public void Run(string work_dir, string revision)
		{
			if (worker.IsBusy)
			{
				Cancel();

				pending_args = new PendingRevLogChangeDescArgs
				               	{Revision = revision, WorkDir = work_dir};
				return;
			}

			RunChangedescAsync(work_dir, revision);
		}

		//-----------------------------------------------------------------------------
		private void RunChangedescAsync(string work_dir, string rev)
		{
			rev_log_parser = new RevLogChangeDescParser();

			var args = new HgArgsBuilder();
			args.Append("log");
			args.AppendDebug();
			args.AppendVerbose();
			args.Append("--follow");

			args.Append("-l");
			const int max_count = 1;
			args.Append(max_count.ToString());

			args.AppendRevision(rev);
			args.AppendStyle(revlog_style.FileName);

			// FIXME: Tags lists bookmarks by default
			args.AppendDisableExtension(HgExtension.Bookmarks);

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

			worker.Run(p);
		}

		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				thread_changedesc = rev_log_parser.ParseLine(msg);
			}
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			if (pending_args != null)
			{
				var p = pending_args;
				pending_args = null;

				Clear();
				Run(p.WorkDir, p.Revision);
				return;
			}

			if (!worker.CancellationPending)
			{
				if (Complete != null)
				{
					Complete(new AsyncRevLogChangeDescResult { Changeset = thread_changedesc });
					return;
				}
			}

			if (Complete != null)
				Complete(null);
		}
	}

	//=============================================================================
	internal class PendingRevLogChangeDescArgs
	{
		public string WorkDir { get; set; }
		public string Revision { get; set; }
	}

	//-----------------------------------------------------------------------------
	internal class AsyncRevLogChangeDescResult
	{
		public RevLogChangeDesc Changeset { get; set; }
	}
}
