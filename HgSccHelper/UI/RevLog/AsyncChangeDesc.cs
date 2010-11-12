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
using System.Collections.ObjectModel;

namespace HgSccHelper.UI.RevLog
{
	class AsyncChangeDesc : IDisposable
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private string work_dir;

		//-----------------------------------------------------------------------------
		private RevLogLinesPair changeset;

		//-----------------------------------------------------------------------------
		private List<ParentFilesDiff> parents_diff;

		//-----------------------------------------------------------------------------
		private ObservableCollection<string> parents;

		//-----------------------------------------------------------------------------
		private AsyncChangeDescStates state;

		//-----------------------------------------------------------------------------
		private RevLogChangeDesc thread_changedesc;

		//-----------------------------------------------------------------------------
		private HgFileInfo.HgFileInfoParser file_info_parser;

		//-----------------------------------------------------------------------------
		private readonly RevLogStyleFile revlog_style;

		//-----------------------------------------------------------------------------
		private RevLogChangeDescParser rev_log_parser;

		//-----------------------------------------------------------------------------
		private PendingChangeDescArgs pending_args;

		//-----------------------------------------------------------------------------
		public AsyncChangeDesc()
		{
			worker = new HgThread();
			revlog_style = new RevLogStyleFile();
		}

		//-----------------------------------------------------------------------------
		public void Clear()
		{
			Cancel();

			changeset = null;
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
		public Action<AsyncChangeDescResult> Completed { get; set; }

		//-----------------------------------------------------------------------------
		public void Run(string work_dir, RevLogLinesPair changeset)
		{
			if (worker.IsBusy)
			{
				Cancel();

				pending_args = new PendingChangeDescArgs
				               	{Changeset = changeset, WorkDir = work_dir};
				return;
			}

			this.work_dir = work_dir;
			this.changeset = changeset;

			parents_diff = new List<ParentFilesDiff>();
			parents = changeset.Current.ChangeDesc.Parents;
			if (parents.Count == 0)
				parents = new ObservableCollection<string>(new[] { "null" });

			RunStatusAsync(work_dir, parents[parents_diff.Count], changeset.Current.ChangeDesc.SHA1);
		}

		//-----------------------------------------------------------------------------
		public void RunStatusAsync(string work_dir, string parent, string rev)
		{
			var options = HgStatusOptions.Added | HgStatusOptions.Deleted
				| HgStatusOptions.Modified
				| HgStatusOptions.Copies | HgStatusOptions.Removed;

			state = AsyncChangeDescStates.Status;
			file_info_parser = new HgFileInfo.HgFileInfoParser();

			var args = Hg.BuildStatusParams(options, "", parent, rev);
			RunHgThread(work_dir, args.ToString());
		}

		//-----------------------------------------------------------------------------
		public void RunChangedescAsync(string work_dir, string parent)
		{
			state = AsyncChangeDescStates.Changedesc;

			rev_log_parser = new RevLogChangeDescParser();

			var args = new HgArgsBuilder();
			args.Append("log");
			args.AppendDebug();
			args.AppendVerbose();
			args.Append("--follow");

			args.Append("-l");
			const int max_count = 1;
			args.Append(max_count.ToString());

			args.AppendRevision(parent);
			args.AppendStyle(revlog_style.FileName);

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
				switch (state)
				{
					case AsyncChangeDescStates.None:
						break;
					case AsyncChangeDescStates.Status:
						{
							file_info_parser.AddLine(msg);
							break;
						}
					case AsyncChangeDescStates.Changedesc:
						{
							thread_changedesc = rev_log_parser.ParseLine(msg);
						}
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			if (!worker.CancellationPending)
			{
				switch (state)
				{
					case AsyncChangeDescStates.None:
						break;
					case AsyncChangeDescStates.Status:
						{
							RunChangedescAsync(work_dir, parents[parents_diff.Count]);
							return;
						}
					case AsyncChangeDescStates.Changedesc:
						{
							var files = file_info_parser.Files;
							var desc = thread_changedesc;

							if (desc != null)
							{
								var parent_diff = new ParentFilesDiff();
								parent_diff.Desc = desc;
								parent_diff.Files = new List<ParentDiffHgFileInfo>();

								foreach (var file in files)
									parent_diff.Files.Add(new ParentDiffHgFileInfo { FileInfo = file });

								parents_diff.Add(parent_diff);

								if (parents_diff.Count < parents.Count)
								{
									RunStatusAsync(work_dir, parents[parents_diff.Count], changeset.Current.ChangeDesc.SHA1);
								}
								else
								{
									if (Completed != null)
									{
										Completed(new AsyncChangeDescResult { Changeset = changeset, ParentFiles = parents_diff });
									}
								}
							}
						}
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}
			}

			if (pending_args != null)
			{
				var p = pending_args;
				pending_args = null;

				Clear();
				Run(p.WorkDir, p.Changeset);
			}
		}
	}

	//=============================================================================
	internal class PendingChangeDescArgs
	{
		public string WorkDir { get; set; }
		public RevLogLinesPair Changeset { get; set; }
	}

	//-----------------------------------------------------------------------------
	enum AsyncChangeDescStates
	{
		None,
		Status,
		Changedesc
	}

	//-----------------------------------------------------------------------------
	internal class AsyncChangeDescResult
	{
		public RevLogLinesPair Changeset { get; set; }
		public List<ParentFilesDiff> ParentFiles { get; set; }
	}
}
