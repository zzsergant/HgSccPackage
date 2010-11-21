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

namespace HgSccHelper.UI.RevLog
{
	class AsyncStatus : IDisposable
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private HgFileInfo.HgFileInfoParser file_info_parser;

		//-----------------------------------------------------------------------------
		private PendingStatusArgs pending_args;

		//-----------------------------------------------------------------------------
		public AsyncStatus()
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
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			worker.Cancel();
			worker.Dispose();
		}

		//-----------------------------------------------------------------------------
		public Action<AsyncStatusResult> Complete { get; set; }

		//-----------------------------------------------------------------------------
		public void Run(string work_dir)
		{
			Run(work_dir, "");
		}

		//-----------------------------------------------------------------------------
		public void Run(string work_dir, HgStatusOptions options)
		{
			Run(work_dir, options, "", "", "");
		}

		//-----------------------------------------------------------------------------
		public void Run(string work_dir, string path)
		{
			Run(work_dir, path, "");
		}

		//-----------------------------------------------------------------------------
		public void Run(string work_dir, string path, string rev)
		{
			const HgStatusOptions options = HgStatusOptions.Added | HgStatusOptions.Clean
			                                | HgStatusOptions.Deleted | HgStatusOptions.Modified
			                                | HgStatusOptions.Copies | HgStatusOptions.Removed;

			Run(work_dir, options, path, rev, "");
		}

		//-----------------------------------------------------------------------------
		public void Run(string work_dir, HgStatusOptions options, string path, string rev1, string rev2)
		{
			if (worker.IsBusy)
			{
				Cancel();

				pending_args = new PendingStatusArgs
				               	{
				               		WorkDir = work_dir,
									Options = options,
									Path = path,
									Rev1 = rev1,
									Rev2 = rev2
				               	};
				return;
			}

			RunStatusAsync(work_dir, options, path, rev1, rev2);
		}

		//-----------------------------------------------------------------------------
		private void RunStatusAsync(string work_dir, HgStatusOptions options, string path, string rev1, string rev2)
		{
			var args = Hg.BuildStatusParams(options, path, rev1, rev2);
			file_info_parser = new HgFileInfo.HgFileInfoParser();

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
				file_info_parser.AddLine(msg);
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
				Run(p.WorkDir, p.Options, p.Path, p.Rev1, p.Rev2);
				return;
			}

			if (!worker.CancellationPending)
			{
				var files = file_info_parser.Files;
				if (Complete != null)
				{
					Complete(new AsyncStatusResult { Files = files });
					return;
				}
			}

			if (Complete != null)
				Complete(null);
		}
	}

	//=============================================================================
	internal class PendingStatusArgs
	{
		public string WorkDir { get; set; }
		public string Revision { get; set; }
		public HgStatusOptions Options { get; set; }
		public string Path { get; set; }
		public string Rev1 { get; set; }
		public string Rev2 { get; set; }
}

	//-----------------------------------------------------------------------------
	internal class AsyncStatusResult
	{
		public List<HgFileInfo> Files { get; set; }
	}
}
