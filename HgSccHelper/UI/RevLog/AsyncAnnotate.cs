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
	class AsyncAnnotate : IDisposable
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private List<AnnotateLineInfo> lines;

		//-----------------------------------------------------------------------------
		private bool is_binary;

		//-----------------------------------------------------------------------------
		private PendingAnnotateArgs pending_args;

		//-----------------------------------------------------------------------------
		public Action<AsyncAnnotateResults> Complete { get; set; }

		//-----------------------------------------------------------------------------
		private AnnotateParser parser;

		//-----------------------------------------------------------------------------
		private PendingAnnotateArgs run_args;

		//-----------------------------------------------------------------------------
		public AsyncAnnotate()
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

			lines = null;
			is_binary = false;
		}

		//-----------------------------------------------------------------------------
		public void RunAsync(string work_dir, string path, string rev)
		{
			Clear();

			if (worker.IsBusy)
			{
				pending_args = new PendingAnnotateArgs
				               	{
				               		WorkingDir = work_dir,
									Path = path,
									Rev = rev
				               	};
				return;
			}

			var args = new HgArgsBuilder();
			args.Append("annotate");
			args.Append("-fn");

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendPath(path);

			run_args = new PendingAnnotateArgs
			           	{
			           		WorkingDir = work_dir,
			           		Path = path,
			           		Rev = rev
			           	};

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

			lines = new List<AnnotateLineInfo>();
			is_binary = false;
			parser = new AnnotateParser();
			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				try
				{
					var local_lines = lines;
					if (local_lines != null)
					{
						var line_info = parser.ParseLine(msg);
						local_lines.Add(line_info);
					}
				}
				catch (HgAnnotateBinaryException)
				{
					is_binary = true;
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

				RunAsync(p.WorkingDir, p.Path, p.Rev);
				return;
			}

			if (!worker.CancellationPending && lines != null)
			{
				if (Complete != null)
				{
					if (!is_binary)
					{
						if (!HgAnnotate.FixBom(run_args.WorkingDir, run_args.Rev, run_args.Path,
							                   lines))
						{
							Complete(null);
							return;
						}
					}

					var results = new AsyncAnnotateResults
										{
											Lines = lines,
											IsBinary = is_binary
										};

					lines = null;
					Complete(results);
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
	internal class PendingAnnotateArgs
	{
		public string WorkingDir { get; set; }
		public string Path { get; set; }
		public string Rev { get; set; }
	}

	//-----------------------------------------------------------------------------
	internal class AsyncAnnotateResults
	{
		public List<AnnotateLineInfo> Lines { get; set; }
		public bool IsBinary { get; set; }
	}
}
