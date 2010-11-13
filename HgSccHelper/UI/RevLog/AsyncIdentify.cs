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

namespace HgSccHelper.UI.RevLog
{
	class AsyncIdentify : IDisposable
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private IdentifyInfo identify_info;

		//-----------------------------------------------------------------------------
		private PendingIdentifyArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<IdentifyInfo> Complete { get; set; }

		//-----------------------------------------------------------------------------
		public AsyncIdentify()
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

			identify_info = null;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingIdentifyArgs { WorkingDir = work_dir };
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("identify");
			args.Append("-ni");
			args.AppendDebug();

			RunHgDiffThread(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		private void RunHgDiffThread(string work_dir, string args)
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
				identify_info = Hg.ParseIdentifyLine(msg);
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

			if (!worker.CancellationPending && identify_info != null)
			{
				if (Complete != null)
				{
					Complete(identify_info);
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
	internal class PendingIdentifyArgs
	{
		public string WorkingDir { get; set; }
	}
}
