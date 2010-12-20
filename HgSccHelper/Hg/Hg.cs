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
using System.Text;
using System.Diagnostics;
using System.IO;
using HgSccHelper.Misc;
using Microsoft.Win32;

//=============================================================================
namespace HgSccHelper
{
	//=============================================================================
	public class HgDiffException : Exception
	{
		//-----------------------------------------------------------------------------
		public HgDiffException()			
		{

		}

		//-----------------------------------------------------------------------------
		public HgDiffException(string message)
			: base(message)
		{

		}
	}

	//=============================================================================
	public class HgCommandLineException : Exception
	{
		//-----------------------------------------------------------------------------
		public HgCommandLineException()
		{

		}

		//-----------------------------------------------------------------------------
		public HgCommandLineException(string message)
			: base(message)
		{

		}
	}

	//==================================================================
	public class CommitMessageFile : IDisposable
	{
		public string FileName { get; private set; }

		//------------------------------------------------------------------
		public CommitMessageFile(string msg)
		{
			FileName = Path.GetTempFileName();
			//Logger.WriteLine("Creating temp file: " + FileName);

			// Write commit message in default encoding (based on OS ANSI code page)
			// instead of UTF8.
			// TODO: Make commit encoding configurable via options
			using (var stream = new StreamWriter(File.OpenWrite(FileName), System.Text.Encoding.Default))
			{
				stream.Write(msg);
			}
		}

		//------------------------------------------------------------------
		public void Dispose()
		{
			File.Delete(FileName);
		}
	}

	//=============================================================================
	public class Hg
	{
		//------------------------------------------------------------------
		public const int MaxCmdLength = 2000 - 300;

		//------------------------------------------------------------------
		public static string CustomHgClient { get; set; }

		//------------------------------------------------------------------
		public static string DefaultClient { get; private set; }

		//------------------------------------------------------------------
		static Hg()
		{
			DefaultClient = "hg";
			var found_client = Util.FindExe("hg");
			
			if (!String.IsNullOrEmpty(found_client))
				DefaultClient = found_client;
		}

		//-----------------------------------------------------------------------------
		public Hg()
		{
		}

		//-----------------------------------------------------------------------------
		public ProcessStartInfo PrepareProcess(string work_dir, string arguments)
		{
			// TODO: Make the hg path configurable via options

			var hg_client = DefaultClient;
			if (!String.IsNullOrEmpty(CustomHgClient))
				hg_client = CustomHgClient;

			var info = new ProcessStartInfo(hg_client);
			info.Arguments = arguments;

			info.CreateNoWindow = true;
			info.WorkingDirectory = work_dir;

			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			info.UseShellExecute = false;

			return info;
		}

		//-----------------------------------------------------------------------------
		public List<ChangeDesc> ChangesFull(string work_dir, string path)
		{
			return ChangesFull(work_dir, path, "");
		}

		//-----------------------------------------------------------------------------
		public List<ChangeDesc> ChangesFull(string work_dir, string path, string rev)
		{
			using (var style = new ChangeSetStyleFile())
			{
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

				using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
				{
					var lst = ChangeDesc.ParseChanges(proc.StandardOutput);
					proc.WaitForExit();

					return lst;
				}
			}
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// This function returns a list of revisions suitable to build a revision graph.
		/// Requires HGK extension enabled in user hgrc
		/// </summary>
		/// <param name="work_dir">Repository root</param>
		/// <param name="max_count">Limit count of revisions. 0 - unlimited</param>
		/// <returns></returns>
		public List<RevLogChangeDesc> RevLogHgk(string work_dir, int max_count)
		{
			var args = new HgArgsBuilder();
			args.Append("debug-rev-list");
			args.Append("--header");
			args.Append("--topo-order");
			args.Append("--parents");

			if (max_count > 0)
			{
				args.Append("--max-count");
				args.Append(max_count.ToString());
			}

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var lst = RevLogChangeDesc.ParseChangesHgk(proc.StandardOutput);
				proc.WaitForExit();

				return lst;
			}
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// This function returns a list of revisions suitable to build a revision graph 
		/// </summary>
		/// <param name="work_dir">Repository root</param>
		/// <param name="max_count">Limit count of revisions. 0 - unlimited</param>
		/// <returns></returns>
		public List<RevLogChangeDesc> RevLog(string work_dir, int max_count)
		{
			return RevLog(work_dir, "", max_count);
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// This function returns a list of revisions suitable to build a revision graph 
		/// </summary>
		/// <param name="work_dir">Repository root</param>
		/// <param name="rev">String that used to specify revision or revision range</param>
		/// <param name="max_count">Limit count of revisions. 0 - unlimited</param>
		/// <remarks>
		/// This function does not require an HGK extension, but unfortunately works ~2.5 times slower than HGK version.
		/// On the other side it is possible to specify revision range and it is much more flexible with templates and styles.
		/// </remarks>
		/// <returns></returns>
		public List<RevLogChangeDesc> RevLog(string work_dir, string rev, int max_count)
		{
			var args = new HgArgsBuilder();
			args.Append("log");

			args.AppendDebug();
			args.AppendVerbose();
			args.Append("--follow");

			if (max_count != 0)
			{
				args.Append("-l");
				args.Append(max_count.ToString());
			}

			if (rev.Length > 0)
				args.AppendRevision(rev);

			using (var revlog_style = new RevLogStyleFile())
			{
				args.AppendStyle(revlog_style.FileName);

				using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
				{
					var lst = RevLogChangeDesc.ParseChanges(proc.StandardOutput);
					proc.WaitForExit();

					return lst;
				}
			}
		}

		//-----------------------------------------------------------------------------
		public List<RevLogChangeDesc> Parents(string work_dir, string rev)
		{
			var args = new HgArgsBuilder();
			args.Append("parents");

			args.AppendDebug();

			if (rev.Length > 0)
				args.AppendRevision(rev);

			using (var revlog_style = new RevLogStyleFile())
			{
				args.AppendStyle(revlog_style.FileName);

				using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
				{
					var lst = RevLogChangeDesc.ParseChanges(proc.StandardOutput);
					proc.WaitForExit();

					return lst;
				}
			}
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns revision description
		/// </summary>
		/// <param name="work_dir"></param>
		/// <param name="revision"></param>
		/// <returns>null on error</returns>
		public RevLogChangeDesc GetRevisionDesc(string work_dir, string revision)
		{
			var list = RevLog(work_dir, revision, 1);
			if (list.Count == 0)
				return null;

			return list[0];
		}

		//-----------------------------------------------------------------------------
/*
		public List<FileInfo> FilesInfo(string work_dir, int revision)
		{
			string style_path = @"c:\Work\Mercurial\files.style";

			var args = new StringBuilder();
			args.Append("log");
			args.Append(" -r " + revision);
			args.Append(" -v --debug");
			args.Append(" --style " + style_path);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var lst = FileInfo.ParseFileInfo(proc.StandardOutput);
				proc.WaitForExit();

				return lst;
			}
		}
*/

		//-----------------------------------------------------------------------------
		public bool HaveRoot(string work_dir)
		{
			return Root(work_dir).Length > 0;
		}

		//-----------------------------------------------------------------------------
		public string Root(string work_dir)
		{
			string args = "root";
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				string root = proc.StandardOutput.ReadLine();
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return string.Empty;

				return root;
			}
		}

		//-----------------------------------------------------------------------------
		public bool CreateRepository(string work_dir)
		{
			string args = "init";
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public List<string> Manifest(string work_dir)
		{
			return Manifest(work_dir, "");
		}

		//-----------------------------------------------------------------------------
		public List<string> Manifest(string work_dir, string revision)
		{
			var args = new HgArgsBuilder();
			args.Append("manifest");

			if (revision.Length != 0)
				args.AppendRevision(revision);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var reader = proc.StandardOutput;
				var files = new List<string>();

				while (true)
				{
					string str = reader.ReadLine();

					if (str == null)
						break;

					files.Add(str);
				}

				proc.WaitForExit();

				return files;
			}
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string work_dir)
		{
			return Status(work_dir, "");
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string work_dir, string path)
		{
			return Status(work_dir, path, "");
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string work_dir, string path, string rev)
		{
			var options = HgStatusOptions.Added | HgStatusOptions.Clean
				| HgStatusOptions.Deleted | HgStatusOptions.Modified
				| HgStatusOptions.Copies | HgStatusOptions.Removed;

			return Status(work_dir, options, path, rev, "");
		}

		//-----------------------------------------------------------------------------
		public static HgArgsBuilder BuildStatusParams(HgStatusOptions options, string path, string rev1, string rev2)
		{
			var args = new HgArgsBuilder();
			args.Append("status");

			if (options == HgStatusOptions.All)
				args.Append("-A");
			else
			{
				var opts = new StringBuilder();
				if ((options & HgStatusOptions.Modified) == HgStatusOptions.Modified)
					opts.Append("m");
				if ((options & HgStatusOptions.Added) == HgStatusOptions.Added)
					opts.Append("a");
				if ((options & HgStatusOptions.Removed) == HgStatusOptions.Removed)
					opts.Append("r");
				if ((options & HgStatusOptions.Deleted) == HgStatusOptions.Deleted)
					opts.Append("d");
				if ((options & HgStatusOptions.Copies) == HgStatusOptions.Copies)
					opts.Append("C");
				if ((options & HgStatusOptions.Clean) == HgStatusOptions.Clean)
					opts.Append("c");
				if ((options & HgStatusOptions.Unknown) == HgStatusOptions.Unknown)
					opts.Append("u");
				if ((options & HgStatusOptions.Ignored) == HgStatusOptions.Ignored)
					opts.Append("i");

				if (opts.Length > 0)
					args.Append("-" + opts);
			}

			if (path.Length > 0)
				args.AppendPath(path);

			if (rev1.Length > 0)
				args.AppendRevision(rev1);

			if (rev2.Length > 0)
				args.AppendRevision(rev2);

			return args;
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string work_dir, HgStatusOptions options, string path, string rev1, string rev2)
		{
			var args = BuildStatusParams(options, path, rev1, rev2);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var reader = proc.StandardOutput;
				var files_info = HgFileInfo.ParseFileInfo(reader);

				proc.WaitForExit();
				if (proc.ExitCode != 0)
				{
					Logger.WriteLine(args.ToString());
					Logger.WriteLine(reader.ReadToEnd());
				}

				return files_info;
			}
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string work_dir, IEnumerable<string> files)
		{
			var args = new HgArgsBuilder();
			args.Append("status");
			args.Append("-amrdcC");

			var cmd_line = new HgArgsBuilder();
			cmd_line.Append(args.ToString());

			var files_info = new List<HgFileInfo>();

			foreach (string f in files)
			{
				if (!cmd_line.AppendFilenameWithLengthCheck(f))
				{
					using (Process proc = Process.Start(PrepareProcess(work_dir, cmd_line.ToString())))
					{
						var reader = proc.StandardOutput;
						var info = HgFileInfo.ParseFileInfo(reader);
						files_info.AddRange(info);

						proc.WaitForExit();
						if (proc.ExitCode < 0)
						{
							// FIXME: some error
							return files_info;
						}
					}

					cmd_line.Clear();
					cmd_line.Append(args.ToString());
				}

				cmd_line.AppendFilenameWithLengthCheck(f);
			}

			if (cmd_line.Length != args.Length)
			{
				using (Process proc = Process.Start(PrepareProcess(work_dir, cmd_line.ToString())))
				{
					var reader = proc.StandardOutput;
					var info = HgFileInfo.ParseFileInfo(reader);
					files_info.AddRange(info);

					proc.WaitForExit();
					if (proc.ExitCode < 0)
					{
						// FIXME: some error
						return files_info;
					}
				}
			}

			return files_info;
		}

		//-----------------------------------------------------------------------------
		public bool RunHg(string work_dir, string args)
		{
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public bool RunHg(string work_dir, string args, out string output)
		{
			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				output = proc.StandardOutput.ReadToEnd(); 
				proc.WaitForExit();
				if (proc.ExitCode < 0)
				{
					output = string.Empty;
					return false;
				}

				return true;
			}
		}

		//-----------------------------------------------------------------------------
		public bool Add(string work_dir, IEnumerable<string> files)
		{
			var args = new HgArgsBuilder();
			args.Append("add");

			var cmd_line = new HgArgsBuilder();
			cmd_line.Append(args.ToString());

			foreach (string f in files)
			{
				if (!cmd_line.AppendFilenameWithLengthCheck(f))
				{
					if (!RunHg(work_dir, cmd_line.ToString()))
						return false;

					cmd_line.Clear();
					cmd_line.Append(args.ToString());
				}

				cmd_line.AppendFilenameWithLengthCheck(f);
			}

			if (cmd_line.Length != args.Length)
			{
				return RunHg(work_dir, cmd_line.ToString());
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public CommitResult Commit(string work_dir, HgCommitOptions options, IEnumerable<string> files, string comment)
		{
			using (var commit_msg_file = new CommitMessageFile(comment))
			{
				var args = new HgArgsBuilder();
				args.Append("commit");
				args.AppendVerbose();

				if ((options & HgCommitOptions.CloseBranch) == HgCommitOptions.CloseBranch)
					args.Append("--close-branch");

				args.Append("-l");
				args.AppendPath(commit_msg_file.FileName);

				var cmd_line = new HgArgsBuilder();
				cmd_line.Append(args.ToString());

				foreach (string f in files)
				{
					if (!cmd_line.AppendFilenameWithLengthCheck(f))
						throw new HgCommandLineException();
				}

				if (cmd_line.Length == args.Length)
				{
					// To commit all changes use CommitAll() function
					throw new ArgumentException("Passing no files in Commit function is not allowed");
				}

				string commit_output;
				CommitResult result = new CommitResult();
				result.IsSuccess = RunHg(work_dir, cmd_line.ToString(), out commit_output);
				if (result.IsSuccess)
				{
					ParseCommitOutput(commit_output, result);
				}
				return result;
			}
		}

		private void ParseCommitOutput(string commit_output, CommitResult output)
		{
			const string SUBREPO_MARKER = "committing subrepository";
			const string END_MARKER = "committed changeset";

			string[] lines = commit_output.Split('\n');

			output.CommitedFiles = new List<string>();
			output.CommitedSubrepoFiles = new Dictionary<string, List<string>>();
			bool next_line_is_subrepo_files = false;
			string subrepo = null;
			foreach (string line in lines)
			{
				if (line.StartsWith(END_MARKER))
					break;

				if (line.StartsWith(SUBREPO_MARKER))
				{
					subrepo = line.Substring(SUBREPO_MARKER.Length, line.Length - SUBREPO_MARKER.Length).Trim();
					output.CommitedSubrepoFiles.Add(subrepo, new List<string>());
					next_line_is_subrepo_files = true;
				}
				else
				{
					if (next_line_is_subrepo_files)
					{
						if (line.Trim().ToLower().StartsWith(".hgsub"))
						{
							next_line_is_subrepo_files = false;
							continue;
						}
						else
						{
							output.CommitedSubrepoFiles[subrepo].Add(line.Replace('/', '\\').Trim());
							next_line_is_subrepo_files = true;
						}
					}
					else
					{
						output.CommitedFiles.Add(line.Replace('/','\\').Trim());
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		public CommitResult CommitAll(string work_dir, HgCommitOptions options, string comment)
		{
			using(var commit_msg_file = new CommitMessageFile(comment))
			{
				var args = new HgArgsBuilder();
				args.Append("commit");
				args.AppendVerbose();

				if ((options & HgCommitOptions.CloseBranch) == HgCommitOptions.CloseBranch)
					args.Append("--close-branch");

				args.Append("-l");
				args.AppendPath(commit_msg_file.FileName);

				string commit_output;
				CommitResult result = new CommitResult();
				result.IsSuccess = RunHg(work_dir, args.ToString(), out commit_output);
				if (result.IsSuccess)
				{
					ParseCommitOutput(commit_output, result);
				}
				return result;
			}
		}

		//-----------------------------------------------------------------------------
		public CommitResult CommitAll(string work_dir, HgCommitOptions options, string comment, string date_str)
		{
			using(var commit_msg_file = new CommitMessageFile(comment))
			{
				var args = new HgArgsBuilder();
				args.Append("commit");
				args.AppendVerbose();

				if ((options & HgCommitOptions.CloseBranch) == HgCommitOptions.CloseBranch)
					args.Append("--close-branch");

				args.Append("-l");
				args.AppendPath(commit_msg_file.FileName);

				args.Append("-d");
				args.Append(date_str.Quote());

				string commit_output;
				CommitResult result = new CommitResult();
				result.IsSuccess = RunHg(work_dir, args.ToString(), out commit_output);
				if (result.IsSuccess)
				{
					ParseCommitOutput(commit_output, result);
				}
				return result;
			}
		}

		//------------------------------------------------------------------
		public bool Revert(string work_dir, string revision, HgRevertOptions options)
		{
			var args = new HgArgsBuilder();
			args.Append("revert");

			if ((options & HgRevertOptions.All) == HgRevertOptions.All)
				args.Append("-a");

			if ((options & HgRevertOptions.NoBackup) == HgRevertOptions.NoBackup)
				args.Append("--no-backup");

			if (revision.Length > 0)
				args.AppendRevision(revision);

			return RunHg(work_dir, args.ToString());
		}

		//-----------------------------------------------------------------------------
		public bool Revert(string work_dir, IEnumerable<string> files)
		{
			var args = new HgArgsBuilder();
			args.Append("revert");
			args.Append("--no-backup");

			var cmd_line = new HgArgsBuilder();
			cmd_line.Append(args.ToString());

			foreach (string f in files)
			{
				if (!cmd_line.AppendFilenameWithLengthCheck(f))
				{
					if (!RunHg(work_dir, cmd_line.ToString()))
						return false;

					cmd_line.Clear();
					cmd_line.Append(args.ToString());
				}

				cmd_line.AppendFilenameWithLengthCheck(f);
			}

			if (cmd_line.Length != args.Length)
			{
				return RunHg(work_dir, cmd_line.ToString());
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool DiffSilent(string work_dir, string file, out bool is_different)
		{
			is_different = true;

			string args = "diff " + file.Quote();
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				var line = proc.StandardOutput.ReadToEnd();

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

				is_different = line.Length != 0;
				return true;
			}
		}

		//-----------------------------------------------------------------------------
/*
		public bool Diff(string work_dir, string file, out bool is_different, out string diff_text)
		{
			is_different = true;

			string args = "diff " + file.Quote();
			using (Process proc = Process.Start(PrepareProcess(work_dir, args)))
			{
				diff_text = proc.StandardOutput.ReadToEnd();

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;

				if (diff_text == null || diff_text.Length == 0)
				{
					diff_text = string.Empty;
					is_different = false;
				}
				else
					is_different = true;

				return true;
			}
		}
*/
		//------------------------------------------------------------------
		public static string GetEditor()
		{
			var hg = new Hg();
			var lines = hg.ShowConfig("");
			var editor_prefix = "ui.editor";

			var separator = new[] { '=' };
			string editor = "notepad";

			foreach (var line in lines)
			{
				if (line.StartsWith(editor_prefix))
				{
					var parts = line.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length < 1 || parts.Length > 2)
						break;

					var right = parts[1].Trim();

					if (!String.IsNullOrEmpty(right))
						editor = right;

					break;
				}
			}

			return editor;
		}

		//------------------------------------------------------------------
		public bool ViewFile(string work_dir, string file, string revision)
		{
			if (revision == "")
			{
				RunHgEditorAsync(Path.Combine(work_dir, file), DeleteFlag.Keep);
				return true;
			}

			string temp_file = Util.GetTempFileNameForFile(file);

			if (!CheckoutFile(work_dir, file, revision, temp_file))
				return false;

			RunHgEditorAsync(temp_file, DeleteFlag.Delete);
			return true;
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Call diff tool for a file in workspace to compare with parent_revision
		/// </summary>
		public bool DiffWithRevision(string work_dir, string file, string parent_revision, out bool is_different)
		{
			is_different = true;

			var files = Status(work_dir, file);
			if (files.Count != 1)
				return false;

			var file_info = files[0];
			if (String.IsNullOrEmpty(file_info.CopiedFrom))
			{
				string temp1 = Util.GetTempFileNameForFile(file_info.File);
				if (!CheckoutFile(work_dir, file_info.File, parent_revision, temp1))
					return false;

				RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(work_dir, file), DeleteFlag.Keep);
			}
			else
			{
				var hg_files = Manifest(work_dir, parent_revision);
				
				var file_manifest_compatible = file_info.File.Replace('\\', '/');
				var file_copied_from = file_info.CopiedFrom.Replace('\\', '/');
				
				string parent_filename;
				if (hg_files.Contains(file_manifest_compatible))
					parent_filename = file_info.File;
				else
					if (hg_files.Contains(file_copied_from))
						parent_filename = file_info.CopiedFrom;
					else
						return false;

				string temp1 = Util.GetTempFileNameForFile(parent_filename);
				if (!CheckoutFile(work_dir, parent_filename, parent_revision, temp1))
					return false;

				RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(work_dir, file), DeleteFlag.Keep);
			}

			return true;
		}


		//-----------------------------------------------------------------------------
		public bool Diff(string work_dir, string file, out bool is_different)
		{
			is_different = true;

			var files = Status(work_dir, file);

			if (	files.Count == 1
				&&	(files[0].Status == HgFileStatus.Added || files[0].Status == HgFileStatus.Modified)
				&&	files[0].CopiedFrom != null)
			{
				var file_info = files[0];

				string temp1 = Util.GetTempFileNameForFile(file_info.CopiedFrom);
				if (!CheckoutFile(work_dir, file_info.CopiedFrom, "", temp1))
					return false;

				RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(work_dir, file), DeleteFlag.Keep);
				
				return true;
			}

			if (	files.Count == 1
				&&	files[0].Status == HgFileStatus.Modified)
			{
				var file_info = files[0];

				string temp1 = Util.GetTempFileNameForFile(file_info.File);
				if (!CheckoutFile(work_dir, file_info.File, "", temp1))
					return false;

				RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(work_dir, file), DeleteFlag.Keep);

				return true;
			}

			//-----------------------------------------------------------------------------
			var hg_files = Manifest(work_dir);
			bool is_tracked = false;
			var lower_f = file.ToLower();

			foreach (var str in hg_files)
			{
				string f = str.ToLower().Replace('/', '\\');
				if (f == lower_f.ToLower())
				{
					Logger.WriteLine(file + " is tracked");
					is_tracked = true;
					break;
				}
			}

			if (is_tracked)
			{
				is_different = false;
				return true;
			}

			Logger.WriteLine(file + " is not tracked");
			return false;
		}

		//-----------------------------------------------------------------------------
/*
		public bool KDiff(string work_dir, string file, int rev1, int rev2)
		{
			var args = new StringBuilder();
			args.Append("kdiff");
			args.Append(" -r " + rev1.ToString() + " -r " + rev2.ToString());
			args.Append(" " + file.Quote());

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}
*/

		//-----------------------------------------------------------------------------
/*
		public bool KDiff(string work_dir, string file1, int rev1, string file2, int rev2)
		{
			var args = new StringBuilder();
			args.Append("kdiff");
			args.Append(" -r " + rev1.ToString() + " -r " + rev2.ToString());
			args.Append(" " + file1.Quote());
			args.Append(" " + file2.Quote());

//			Logger.WriteLine(args.ToString());

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}
*/
		//-----------------------------------------------------------------------------
		public bool CheckoutFile(string work_dir, string file, int rev, string temp_file)
		{
			return CheckoutFile(work_dir, file, rev.ToString(), temp_file);
		}

		//-----------------------------------------------------------------------------
		public bool CheckoutFile(string work_dir, string file, string rev, string temp_file)
		{
			var args = new HgArgsBuilder();
			args.Append("cat");
			args.Append("-o");
			args.AppendPath(temp_file);

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendPath(file);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				return true;
			}
		}

		//------------------------------------------------------------------
		private void RunHgEditorAsync(string file, DeleteFlag del)
		{
			var editor = GetEditor();
			var async_deleter = new AsyncDeleter();

			if (del == DeleteFlag.Delete)
				async_deleter.Add(file);

			// The editor specified in .hgrc/Mercurial.ini may have quotes
			// and/or command line switches, so run it using cmd.
			// TODO: Find a better way of doing this

			try
			{
				var info = new ProcessStartInfo("cmd");
				var cmd_line = editor + " " + file.Quote();

				// Forcing quote for command line
				info.Arguments = "/C \"" + cmd_line + "\"";
				info.UseShellExecute = false;
				info.CreateNoWindow = true;

				var proc = new Process();
				proc.StartInfo = info;
				proc.EnableRaisingEvents = true;
				proc.Exited += async_deleter.OnDeleteEventHandler;

				// If editor redirects file to other opened editor,
				// then we need to sleep for a while to prevent
				// file deletion, until other copy of editor opens it
				async_deleter.DeleteEventDelay = TimeSpan.FromSeconds(2);

				proc.Start();
			}
			catch (Exception)
			{
				async_deleter.Delete();
				throw;
			}
		}

		//-----------------------------------------------------------------------------
		public void RunDiffToolAsync(string file1, DeleteFlag del1, string file2, DeleteFlag del2)
		{
			var async_deleter = new AsyncDeleter();

			if (del1 == DeleteFlag.Delete)
				async_deleter.Add(file1);
	
			if (del2 == DeleteFlag.Delete)
				async_deleter.Add(file2);

			if (!File.Exists(HgSccOptions.Options.DiffTool))
			{
				async_deleter.Delete();
				throw new HgDiffException("DiffTool is not exist");
			}

			// FIXME: Change $plabel and $clabel to filename+revision

			var args = HgSccOptions.Options.DiffArgs;
			if (String.IsNullOrEmpty(args))
				args = "$parent $child";

			args = args.Replace('\'', '\"');
			args = args.Replace("\"$parent\"", "$parent");
			args = args.Replace("\"$child\"", "$child");

			args = args.Replace("\"$plabel1\"", "$plabel1");
			args = args.Replace("\"$clabel\"", "$clabel");

			args = args.Replace("$parent", file1.Quote());
			args = args.Replace("$plabel1", file1.Quote());

			args = args.Replace("$child", file2.Quote());
			args = args.Replace("$clabel", file2.Quote());

			Logger.WriteLine("Diff args: {0}", args);

			try
			{
				var info = new ProcessStartInfo(HgSccOptions.Options.DiffTool);
				info.Arguments = args;
				info.UseShellExecute = false;

				var proc = new Process();
				proc.StartInfo = info;
				proc.EnableRaisingEvents = true;
				proc.Exited += async_deleter.OnDeleteEventHandler;

				proc.Start();
			}
			catch (Exception)
			{
				async_deleter.Delete();
				throw;
			}
		}

		//-----------------------------------------------------------------------------
		public bool Diff(string work_dir, string file1, int rev1, string file2, int rev2)
		{
			return Diff(work_dir, file1, rev1.ToString(), file2, rev2.ToString());
		}

		//-----------------------------------------------------------------------------
		public bool Diff(string work_dir, string file1, string rev1, string file2, string rev2)
		{
			string temp1 = Util.GetTempFileNameForFile(file1);
			string temp2 = Util.GetTempFileNameForFile(file2);

			if (	!CheckoutFile(work_dir, file1, rev1, temp1)
				||	!CheckoutFile(work_dir, file2, rev2, temp2))
			{
				if (File.Exists(temp1))
					File.Delete(temp1);

				if (File.Exists(temp2))
					File.Delete(temp2);

				return false;
			}

			RunDiffToolAsync(temp1, DeleteFlag.Delete, temp2, DeleteFlag.Delete);
			return true;
		}

		//-----------------------------------------------------------------------------
		public bool TrackRename(string work_dir, string file, string rev, out string old_file)
		{
			var args = new HgArgsBuilder();
			args.Append("debugrename");

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendPath(file);

			old_file = string.Empty;

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				string line = proc.StandardOutput.ReadLine();

				proc.WaitForExit();
				if (proc.ExitCode < 0)
					return false;

				if (line == null)
					return false;

				string[] arr = line.Split(new[] { " renamed from ", ":" }, StringSplitOptions.RemoveEmptyEntries);
				if (arr.Length != 3)
				{
					return false;
				}

				old_file = arr[1];
				return true;
			}
		}


		//-----------------------------------------------------------------------------
		public bool Remove(string work_dir, IEnumerable<string> files)
		{
			var args = new HgArgsBuilder();
			args.Append("remove");

			var cmd_line = new HgArgsBuilder();
			cmd_line.Append(args.ToString());

			foreach (string f in files)
			{
				if (!cmd_line.AppendFilenameWithLengthCheck(f))
				{
					if (!RunHg(work_dir, cmd_line.ToString()))
						return false;

					cmd_line.Clear();
					cmd_line.Append(args.ToString());
				}

				cmd_line.AppendFilenameWithLengthCheck(f);
			}

			if (cmd_line.Length != args.Length)
			{
				return RunHg(work_dir, cmd_line.ToString());
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool Rename(string work_dir, string file, string new_file)
		{
			var args = new HgArgsBuilder();
			args.Append("rename");
			args.Append("-A");
			args.AppendPath(file);
			args.AppendPath(new_file);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//------------------------------------------------------------------
		public List<PathAlias> GetPaths(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("paths");

			var paths = new List<PathAlias>();

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					var separators = new[] { '=' };
					var path_parts = str.Split(separators, StringSplitOptions.RemoveEmptyEntries);
					if (path_parts.Length == 2)
					{
						var path_alias = new PathAlias();
						path_alias.Alias = path_parts[0].Trim();
						path_alias.Path = path_parts[1].Trim();

						paths.Add(path_alias);
					}
				}
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<PathAlias>();
			}

			return paths;
		}

		//-----------------------------------------------------------------------------
		public List<RenameInfo> FindRenames(string work_dir, string file, List<ChangeDesc> changes)
		{
			file = file.Replace('\\', '/');

			var renames = new List<RenameInfo>();
			if (changes.Count == 0)
				return renames;

			var current = new RenameInfo { Path = file, Index = 0, Rev = changes[0].Rev };
			renames.Add(current);

			while (true)
			{
				bool found_mismatch = false;
				int mismatch_index = current.Index;
				string prev_name = null;

				for (int i = current.Index; !found_mismatch && i < changes.Count; ++i)
				{
					var ch = changes[i];
					foreach (var info in ch.FilesAdded)
					{
						if (info.Path == current.Path)
						{
							if (!TrackRename(work_dir, current.Path, ch.Rev.ToString(), out prev_name))
								return renames;

							found_mismatch = true;
							mismatch_index = i;
							break;
						}
					}
				}

				if (!found_mismatch)
					break;

				bool found = false;

				for (int i = mismatch_index; !found && i < changes.Count - 1; ++i)
				{
					foreach (var f in changes[i].Files)
					{
						// Logger.WriteLine(String.Format("Comparing: {0}, {1}", f.Path, prev_name));

						if (0 == String.Compare(f.Path, prev_name, true))
						{
							found = true;
							// Logger.WriteLine("Equal");

							var prev = new RenameInfo { Path = prev_name, Index = i + 1, Rev = changes[i + 1].Rev };
							renames.Add(prev);
							current = prev;
							break;
						}
					}
				}

				if (!found)
					break;
			}

			return renames;
		}

		//-----------------------------------------------------------------------------
		public static IdentifyInfo ParseIdentifyLine(string str)
		{
			IdentifyInfo info = null;

			// Identify output looks like:
			// fb208ffc2324+ 15+
			// where '+' means, that there are uncommited changes
			//
			// If there are active merge, then output will looks like:
			// 1c12a72d2c7e+1df046e40fa9+ 1+2+

			if (str != null)
			{
				var separators = new char[] { ' ' };
				var parts = str.Split(separators, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 2)
				{
					string sha1 = parts[0];
					string rev = parts[1];
					bool have_uncommited_changes = false;

					if (sha1.EndsWith("+") && rev.EndsWith("+"))
					{
						have_uncommited_changes = true;
						sha1 = sha1.Substring(0, sha1.Length - 1);
						rev = rev.Substring(0, rev.Length - 1);
					}

					var sha1_items = sha1.Split(new char[] { '+' },
						StringSplitOptions.RemoveEmptyEntries);

					var rev_items = rev.Split(new char[] { '+' },
						StringSplitOptions.RemoveEmptyEntries);

					if (sha1_items.Length == rev_items.Length
						&& sha1_items.Length > 0
						)
					{
						var ids = new List<IdentifyData>();
						for (int i = 0; i < rev_items.Length; ++i)
						{
							int revision;
							if (int.TryParse(rev_items[i], out revision))
							{
								ids.Add(new IdentifyData { Rev = revision, SHA1 = sha1_items[i] });
							}
						}

						if (ids.Count > 0)
						{
							info = new IdentifyInfo();
							info.HaveUncommitedChanges = have_uncommited_changes;
							info.Parents = ids;
						}
					}
				}
			}

			return info;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Identify current revision and check for uncommited changes
		/// </summary>
		/// <param name="work_dir"></param>
		/// <returns>Returns null on error</returns>
		public IdentifyInfo Identify(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("identify");
			args.Append("-ni");
			args.AppendDebug();
			
			// FIXME: by default it shows short 6 bytes sha1
			// to get full sha1 the --debug arg should be specified

			IdentifyInfo info;

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				var str = stream.ReadLine();

				info = ParseIdentifyLine(str);

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return null;
			}

			return info;
		}

		//-----------------------------------------------------------------------------
		public static TagInfo ParseTagLine(string str)
		{
			var tag = new TagInfo();
			const string local_suffix = " local";

			if (str.EndsWith(local_suffix))
			{
				tag.IsLocal = true;
				str = str.Substring(0, str.Length - local_suffix.Length);
			}

			int rev_start = str.LastIndexOf(' ') + 1;
			tag.Name = str.Substring(0, rev_start).Trim();

			var separators = new char[] { ':' };
			var rev_parts = str.Substring(rev_start).Split(separators, StringSplitOptions.RemoveEmptyEntries);

			if (rev_parts.Length == 2)
			{
				int revision;
				if (int.TryParse(rev_parts[0], out revision))
				{
					tag.Rev = revision;
					tag.SHA1 = rev_parts[1];

					return tag;
				}
			}

			return null;
		}

		//------------------------------------------------------------------
		public List<TagInfo> Tags(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("tags");
			args.AppendVerbose();
			args.AppendDebug();

			var tags = new List<TagInfo>();

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					var tag = ParseTagLine(str);
					if (tag != null)
						tags.Add(tag);
				}

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<TagInfo>();
			}

			return tags;
		}

		//-----------------------------------------------------------------------------
		public static BranchInfo ParseBranchLine(string str)
		{
			var branch = new BranchInfo();
			var inactive_suffix = " (inactive)";
			var closed_suffix = " (closed)";

			if (str.EndsWith(inactive_suffix))
			{
				branch.IsActive = false;
				str = str.Substring(0, str.Length - inactive_suffix.Length);
			}
			else
			{
				branch.IsActive = true;
			}

			if (str.EndsWith(closed_suffix))
			{
				branch.IsClosed = true;
				str = str.Substring(0, str.Length - closed_suffix.Length);
			}
			else
			{
				branch.IsClosed = false;
			}

			int rev_start = str.LastIndexOf(' ') + 1;
			branch.Name = str.Substring(0, rev_start).Trim();

			var separators = new char[] { ':' };
			var rev_parts = str.Substring(rev_start).Split(separators, StringSplitOptions.RemoveEmptyEntries);

			if (rev_parts.Length == 2)
			{
				int revision;
				if (int.TryParse(rev_parts[0], out revision))
				{
					branch.Rev = revision;
					branch.SHA1 = rev_parts[1];

					return branch;
				}
			}

			return null;
		}

		//------------------------------------------------------------------
		public List<BranchInfo> Branches(string work_dir, HgBranchesOptions options)
		{
			var args = new HgArgsBuilder();
			args.Append("branches");
			args.AppendDebug();
			args.AppendVerbose();

			if ((options & HgBranchesOptions.Active) == HgBranchesOptions.Active)
				args.Append("-a");
			if ((options & HgBranchesOptions.Closed) == HgBranchesOptions.Closed)
				args.Append("-c");

			var branches = new List<BranchInfo>();

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					var branch = ParseBranchLine(str);
					if (branch != null)
						branches.Add(branch);
				}

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<BranchInfo>();
			}

			return branches;
		}

		//-----------------------------------------------------------------------------
		public bool Update(string work_dir, string revision, HgUpdateOptions options)
		{
			var args = new HgArgsBuilder();
			args.Append("update");

			switch (options)
			{
				case HgUpdateOptions.None:
					break;
				case HgUpdateOptions.Clean:
					{
						args.Append("-C");
						break;
					}
				default:
					{
						throw new ArgumentException("Unknown update option"); 
					}
			}

			if (revision.Length != 0)
				args.AppendRevision(revision);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool AddTag(string work_dir, string tag, string revision, HgTagOptions options, string commit_message)
		{
			var args = new HgArgsBuilder();
			args.Append("tag");

			if ((options & HgTagOptions.Force) == HgTagOptions.Force)
				args.Append("-f");

			if ((options & HgTagOptions.Local) == HgTagOptions.Local)
				args.Append("-l");

			if (revision.Length != 0)
				args.AppendRevision(revision);

			if (commit_message.Length != 0)
			{
				args.Append("-m");
				args.Append(commit_message.Quote());
			}

			args.Append(tag.Quote());

			if (args.Length > MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool Copy(string work_dir, string dest_path, string src_path, HgCopyOptions options)
		{
			var args = new HgArgsBuilder();
			args.Append("copy");

			if ((options & HgCopyOptions.Force) == HgCopyOptions.Force)
				args.Append("-f");

			if ((options & HgCopyOptions.After) == HgCopyOptions.After)
				args.Append("-A");

			args.AppendPath(src_path);
			args.AppendPath(dest_path);

			if (args.Length > MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool RemoveTag(string work_dir, string tag, HgTagOptions option)
		{
			var args = new HgArgsBuilder();
			args.Append("tag");

			if ((option & HgTagOptions.Local) == HgTagOptions.Local)
				args.Append("-l");

			args.Append("--remove");
			args.Append(tag.Quote());

			if (args.Length > MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool Merge(string work_dir, string revision, HgMergeOptions options)
		{
			var args = new HgArgsBuilder();
			args.Append("merge");

			if ((options & HgMergeOptions.Force) == HgMergeOptions.Force)
				args.Append("-f");

			if (revision.Length > 0)
				args.AppendRevision(revision);

			using (Process proc = Process.Start(PrepareProcess(work_dir, args.ToString())))
			{
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public List<string> ShowConfig(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("showconfig");

			var lines = new List<string>();

			var hg = new Hg();

			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					lines.Add(str);
				}
				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<string>();
			}

			return lines;
		}
	}

	//------------------------------------------------------------------
	public enum HgUpdateOptions
	{
		None,
		Clean
	}

	//------------------------------------------------------------------
	public enum HgMergeOptions
	{
		None,
		Force
	}

	//------------------------------------------------------------------
	[Flags]
	public enum HgCommitOptions
	{
		None			= 0x00,
		CloseBranch		= 0x01
	}

	//------------------------------------------------------------------
	[Flags]
	public enum HgTagOptions
	{
		None	= 0x00,
		Force	= 0x01,
		Local	= 0x02
	}

	//------------------------------------------------------------------
	[Flags]
	public enum HgCopyOptions
	{
		None = 0x00,
		Force = 0x01,
		After = 0x02
	}

	//------------------------------------------------------------------
	[Flags]
	public enum HgRevertOptions
	{
		None = 0x00,
		All = 0x01,
		NoBackup = 0x02
	}

	//------------------------------------------------------------------
	[Flags]
	public enum HgBranchesOptions
	{
		None = 0x00,
		Active = 0x01,
		Closed = 0x02,
	}

	//------------------------------------------------------------------
	[Flags]
	public enum HgStatusOptions
	{
		None = 0x00,
		All = 0x01,
		Modified = 0x02,
		Added = 0x04,
		Removed = 0x08,
		Deleted = 0x10,
		Clean = 0x20,
		Unknown = 0x40,
		Ignored = 0x80,
		Copies = 0x100,
	}

	//-----------------------------------------------------------------------------
	public class RenameInfo
	{
		public string Path { get; set; }
		public int Rev { get; set; }
		public int Index { get; set; }
	}

	//------------------------------------------------------------------
	public class PathAlias
	{
		public string Alias { get; set; }
		public string Path { get; set; }
	}

	//------------------------------------------------------------------
	public class IdentifyData
	{
		public int Rev { get; set; }
		public string SHA1 { get; set; }
	}

	//------------------------------------------------------------------
	public class IdentifyInfo
	{
		public List<IdentifyData> Parents { get; set; }
		public bool HaveUncommitedChanges { get; set; }
		
		//-----------------------------------------------------------------------------
		public int Rev
		{
			get
			{
				return Parents[0].Rev;
			}
		}

		//-----------------------------------------------------------------------------
		public string SHA1
		{
			get
			{
				return Parents[0].SHA1;
			}
		}
	}

	//------------------------------------------------------------------
	public class TagInfo
	{
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public bool IsLocal { get; set; }
	}

	//------------------------------------------------------------------
	public class BranchInfo
	{
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public bool IsActive { get; set; }
		public bool IsClosed { get; set; }
	}

	//------------------------------------------------------------------
	public class CommitResult
	{
		public bool IsSuccess { get; set; }
		public List<string> CommitedFiles { get; set; }
		public Dictionary<string, List<string>> CommitedSubrepoFiles { get; set; }
	}
}
