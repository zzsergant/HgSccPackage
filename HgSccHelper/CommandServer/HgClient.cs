//=========================================================================
// Copyright 2012 Sergey Antonov <sergant_@mail.ru>
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
using System.IO;
using System.Text;
using HgSccHelper.Misc;

namespace HgSccHelper.CommandServer
{
	public class HgClient : IDisposable
	{
		private HgCmdServer server;
		private volatile bool is_running_command;
		private object critical;

		//-----------------------------------------------------------------------------
		public Encoding Encoding { get; private set; }

		//-----------------------------------------------------------------------------
		public string WorkDir { get; private set; }

		//-----------------------------------------------------------------------------
		private List<string> capabilities;

		//-----------------------------------------------------------------------------
		public IEnumerable<string> Capabilities
		{
			get { return capabilities; }
		}

		//-----------------------------------------------------------------------------
		public HgClient()
		{
			critical = new object();
		}

		//-----------------------------------------------------------------------------
		public bool Open(string work_dir)
		{
			if (IsStarted)
				return false;

			WorkDir = work_dir;
			server = new HgCmdServer();

			var p = new HgCmdServerParams();
			p.Args = "serve --cmdserver pipe";
			p.ForceSystemEncoding = true;
			p.WorkingDir = work_dir;

			if (!server.Run(p))
				return false;

			// TODO: utf8 ?
			Encoding = Encoding.Default;

			return ReadHello();
		}

		//-----------------------------------------------------------------------------
		public void Close()
		{
			server.Stop();
		}

		//-----------------------------------------------------------------------------
		public bool IsStarted
		{
			get { return server != null && server.IsBusy; }
		}

		//-----------------------------------------------------------------------------
		public bool IsRunningCommand
		{
			get
			{
				return is_running_command;
			}
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (server != null)
			{
				server.Dispose();
				server = null;
			}
		}

		//-----------------------------------------------------------------------------
		private bool ReadHello()
		{
			var msg = new Message();
			server.ReadChannel(ref msg);

			if (msg.Channel != 'o')
				return false;

			var hello_msg = Encoding.ASCII.GetString(msg.Data, 0, (int)msg.Length);
			var hello_strings = hello_msg.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

			if (hello_strings.Length == 0)
				return false;

			const string caps_prefix = "capabilities: ";
			var	caps_list = hello_strings[0].Substring(caps_prefix.Length).Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
			
			capabilities = new List<string>(caps_list);
			if (!capabilities.Contains("runcommand"))
				return false;
			
			return true;
		}

		//-----------------------------------------------------------------------------
		public int RunCommand(HgArgsBuilderZero args,
			Dictionary<char, Func<int, byte[]>> in_channels,
			Dictionary<char, Action<byte[], int, int>> out_channels)
		{
			lock (critical)
			{
				if (!IsStarted)
					return -1;

				try
				{
					is_running_command = true;

					var data = Encoding.ASCII.GetBytes("runcommand\n");
					server.Stdin.Write(data, 0, data.Length);

					var str = args.ToString();
					Logger.WriteLine("[RC]: {0}", str.Replace('\0', '|'));
					server.WriteBlock(Encoding.GetBytes(str));

					var msg = new Message();
					while (true)
					{
						if (!server.ReadChannel(ref msg))
							return -2;

						if (in_channels.ContainsKey(msg.Channel))
						{
							server.WriteBlock(in_channels[msg.Channel]((int)msg.Length));
						}
						else if (out_channels.ContainsKey(msg.Channel))
						{
							out_channels[msg.Channel](msg.Data, 0, (int)msg.Length);
						}
						else if (msg.Channel == 'r')
						{
							int b0 = msg.Data[0];
							int b1 = msg.Data[1];
							int b2 = msg.Data[2];
							int b3 = msg.Data[3];

							int exit_code = (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
							return exit_code;
						}
						else if (msg.Channel == Char.ToUpperInvariant(msg.Channel))
						{
							return -3;
						}
					}
				}
				catch(Exception ex)
				{
					Logger.WriteLine(ex.Message);
					return -5;
				}
				finally
				{
					is_running_command = false;
				}
			}
		}

		//-----------------------------------------------------------------------------
		public int RawCommandStream(HgArgsBuilderZero args)
		{
			return RawCommandStream(args, null, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandStream(HgArgsBuilderZero args, Stream output)
		{
			return RawCommandStream(args, output, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandStream(HgArgsBuilderZero args, Stream output, Stream error)
		{
			var in_channels = new Dictionary<char, Func<int, byte[]>>();
			var out_channels = new Dictionary<char, Action<byte[], int, int>>();

			if (output != null)
				out_channels['o'] = output.Write;

			if (error != null)
				out_channels['e'] = error.Write;

			int ret = RunCommand(args, in_channels, out_channels);
			return ret;
		}

		//-----------------------------------------------------------------------------
		public int RawCommandString(HgArgsBuilderZero args)
		{
			string error;
			string output;
			return RawCommandString(args, out output, out error);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandString(HgArgsBuilderZero args, out string output)
		{
			string error;
			return RawCommandString(args, out output, out error);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandString(HgArgsBuilderZero args, out string output, out string error)
		{
			using (var mem_out = new MemoryStream())
			using (var mem_err = new MemoryStream())
			{
				var in_channels = new Dictionary<char, Func<int, byte[]>>();
				var out_channels = new Dictionary<char, Action<byte[], int, int>>();

				out_channels['o'] = mem_out.Write;
				out_channels['e'] = mem_err.Write;

				int ret = RunCommand(args, in_channels, out_channels);

				mem_out.Seek(0, SeekOrigin.Begin);
				mem_err.Seek(0, SeekOrigin.Begin);

				using (var reader_out = new StreamReader(mem_out, Encoding))
				using (var reader_error = new StreamReader(mem_err, Encoding))
				{
					output = reader_out.ReadToEnd();
					error = reader_error.ReadToEnd();
				}

				return ret;
			}
		}

		//-----------------------------------------------------------------------------
		public int RawCommandCallBack(HgArgsBuilderZero args)
		{
			return RawCommandCallBack(args, null, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandCallBack(HgArgsBuilderZero args, Action<string> ouput_line)
		{
			return RawCommandCallBack(args, ouput_line, null);
		}

		//-----------------------------------------------------------------------------
		public int RawCommandCallBack(HgArgsBuilderZero args, Action<string> ouput_line, Action<string> error_line)
		{
			using (var mem_out = new MemoryStream())
			using (var mem_err = new MemoryStream())
			using (var reader_out = new StreamReader(mem_out, Encoding))
			using (var reader_err = new StreamReader(mem_err, Encoding))
			{
				var in_channels = new Dictionary<char, Func<int, byte[]>>();
				var out_channels = new Dictionary<char, Action<byte[], int, int>>();

				if (ouput_line != null)
				{
					out_channels['o'] = (buffer, offset, count) =>
					{
						mem_out.SetLength(0);
						mem_out.Write(buffer, offset, count);
						mem_out.Seek(0, SeekOrigin.Begin);

						reader_err.DiscardBufferedData();

						while (true)
						{
							string str = reader_out.ReadLine();
							if (str == null)
								break;

							ouput_line(str);
						}
					};
				}

				if (error_line != null)
				{
					out_channels['e'] = (buffer, offset, count) =>
					{
						mem_err.SetLength(0);
						mem_err.Write(buffer, offset, count);
						mem_err.Seek(0, SeekOrigin.Begin);

						reader_err.DiscardBufferedData();

						while (true)
						{
							string str = reader_err.ReadLine();
							if (str == null)
								break;

							error_line(str);
						}
					};
				}

				return RunCommand(args, in_channels, out_channels);
			}
		}

		//=============================================================================
		public List<ChangeDesc> ChangesFull(string path)
		{
			return ChangesFull(path, "");
		}

		//-----------------------------------------------------------------------------
		public List<ChangeDesc> ChangesFull(string path, string rev)
		{
			using (var style = new ChangeSetStyleFile())
			{
				var args = new HgArgsBuilderZero();
				args.Append("log");

				args.AppendDebug();
				args.AppendVerbose();
				args.Append("--follow");

				if (rev.Length > 0)
					args.AppendRevision(rev);

				args.AppendStyle(style.FileName);

				if (path.Length > 0)
					args.AppendPath(path);

				using (var mem_stream = new MemoryStream())
				{
					int res = RawCommandStream(args, mem_stream);
					if (res != 0)
						return new List<ChangeDesc>();

					mem_stream.Seek(0, SeekOrigin.Begin);

					using (var output_stream = new StreamReader(mem_stream, Encoding))
					{
						var lst = ChangeDesc.ParseChanges(output_stream);
						return lst;
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// This function returns a list of revisions suitable to build a revision graph 
		/// </summary>
		/// <param name="max_count">Limit count of revisions. 0 - unlimited</param>
		/// <returns></returns>
		public List<RevLogChangeDesc> RevLog(int max_count)
		{
			return RevLog("", max_count);
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// This function returns a list of revisions suitable to build a revision graph 
		/// </summary>
		/// <param name="rev">String that used to specify revision or revision range</param>
		/// <param name="max_count">Limit count of revisions. 0 - unlimited</param>
		/// <remarks>
		/// This function does not require an HGK extension, but unfortunately works ~2.5 times slower than HGK version.
		/// On the other side it is possible to specify revision range and it is much more flexible with templates and styles.
		/// </remarks>
		/// <returns></returns>
		public List<RevLogChangeDesc> RevLog(string rev, int max_count)
		{
			var args = new HgArgsBuilderZero();
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

				using (var mem_stream = new MemoryStream())
				{
					int res = RawCommandStream(args, mem_stream);
					if (res != 0)
						return new List<RevLogChangeDesc>();

					mem_stream.Seek(0, SeekOrigin.Begin);

					using (var output_stream = new StreamReader(mem_stream, Encoding))
					{
						var lst = RevLogChangeDesc.ParseChanges(output_stream);
						return lst;
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		public ParentsInfo Parents()
		{
			return Parents("");
		}

		//-----------------------------------------------------------------------------
		public ParentsInfo Parents(string rev)
		{
			var args = new HgArgsBuilderZero();
			args.Append("parents");

			args.AppendDebug();

			if (rev.Length > 0)
				args.AppendRevision(rev);

			using (var revlog_style = new RevLogStyleFile())
			{
				args.AppendStyle(revlog_style.FileName);

				using (var mem_stream = new MemoryStream())
				{
					int res = RawCommandStream(args, mem_stream);
					if (res != 0)
						return null;

					mem_stream.Seek(0, SeekOrigin.Begin);

					using (var output_stream = new StreamReader(mem_stream, Encoding))
					{
						var lst = RevLogChangeDesc.ParseChanges(output_stream);
						if (lst.Count > 0)
							return new ParentsInfo { Parents = lst };

						return null;
					}
				}
			}
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns revision description
		/// </summary>
		/// <param name="revision"></param>
		/// <returns>null on error</returns>
		public RevLogChangeDesc GetRevisionDesc(string revision)
		{
			var list = RevLog(revision, 1);
			if (list.Count == 0)
				return null;

			return list[0];
		}

		//-----------------------------------------------------------------------------
		public List<string> Manifest()
		{
			return Manifest("");
		}

		//-----------------------------------------------------------------------------
		public List<string> Manifest(string revision)
		{
			var args = new HgArgsBuilderZero();
			args.Append("manifest");

			if (revision.Length != 0)
				args.AppendRevision(revision);

			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return new List<string>();

				mem_stream.Seek(0, SeekOrigin.Begin);

				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					var files = new List<string>();

					while (true)
					{
						string str = output_stream.ReadLine();

						if (str == null)
							break;

						files.Add(str);
					}

					return files;
				}
			}
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status()
		{
			return Status("");
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string path)
		{
			return Status(path, "");
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(string path, string rev)
		{
			var options = HgStatusOptions.Added | HgStatusOptions.Clean
				| HgStatusOptions.Deleted | HgStatusOptions.Modified
				| HgStatusOptions.Copies | HgStatusOptions.Removed;

			return Status(options, path, rev, "");
		}

		//-----------------------------------------------------------------------------
		public static HgArgsBuilderZero BuildStatusParams(HgStatusOptions options, string path, string rev1, string rev2)
		{
			var args = new HgArgsBuilderZero();
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
		public List<HgFileInfo> Status(HgStatusOptions options, string path, string rev1, string rev2)
		{
			var args = BuildStatusParams(options, path, rev1, rev2);

			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return new List<HgFileInfo>();

				mem_stream.Seek(0, SeekOrigin.Begin);

				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					var files_info = HgFileInfo.ParseFileInfo(output_stream);
					return files_info;
				}
			}
		}

		//-----------------------------------------------------------------------------
		public List<HgFileInfo> Status(IEnumerable<string> files)
		{
			var args = new HgArgsBuilderZero();
			args.Append("status");
			args.Append("-amrdcC");

			using (var list_file = new HgListFile(files))
			{
				if (!args.AppendListFile(list_file.FileName))
					throw new HgCommandLineException();

				using (var mem_stream = new MemoryStream())
				{
					int res = RawCommandStream(args, mem_stream);
					if (res != 0)
						return new List<HgFileInfo>();

					mem_stream.Seek(0, SeekOrigin.Begin);

					using (var output_stream = new StreamReader(mem_stream, Encoding))
					{
						var files_info = HgFileInfo.ParseFileInfo(output_stream);
						return files_info;
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		public bool RunHg(HgArgsBuilderZero args)
		{
			return RawCommandCallBack(args) == 0;
		}

		//-----------------------------------------------------------------------------
		public bool RunHg(HgArgsBuilderZero args, out string output)
		{
			return RawCommandString(args, out output) == 0;
		}

		//-----------------------------------------------------------------------------
		public bool Add(IEnumerable<string> files)
		{
			var args = new HgArgsBuilderZero();
			args.Append("add");

			using (var list_file = new HgListFile(files))
			{
				if (!args.AppendListFile(list_file.FileName))
					throw new HgCommandLineException();

				return RunHg(args);
			}
		}

		//-----------------------------------------------------------------------------
		public CommitResult Commit(HgCommitOptions options, IEnumerable<string> files, string comment)
		{
			using (var commit_msg_file = new CommitMessageFile(comment))
			{
				var args = new HgArgsBuilderZero();
				args.Append("commit");
				args.AppendVerbose();

				if ((options & HgCommitOptions.CloseBranch) == HgCommitOptions.CloseBranch)
					args.Append("--close-branch");

				args.Append("-l");
				args.AppendPath(commit_msg_file.FileName);

				using (var list_file = new HgListFile(files))
				{
					if (!args.AppendListFile(list_file.FileName))
						throw new HgCommandLineException();

					if (list_file.IsEmpty)
					{
						// To commit all changes use CommitAll() function
						throw new ArgumentException("Passing no files in Commit function is not allowed");
					}

					string commit_output;
					var result = new CommitResult();
					result.IsSuccess = RunHg(args, out commit_output);
					if (result.IsSuccess)
					{
						Hg.ParseCommitOutput(commit_output, result);
					}
					return result;
				}
			}
		}

		//-----------------------------------------------------------------------------
		public CommitResult CommitAll(HgCommitOptions options, string comment)
		{
			using(var commit_msg_file = new CommitMessageFile(comment))
			{
				var args = new HgArgsBuilderZero();
				args.Append("commit");
				args.AppendVerbose();

				if ((options & HgCommitOptions.CloseBranch) == HgCommitOptions.CloseBranch)
					args.Append("--close-branch");

				args.Append("-l");
				args.AppendPath(commit_msg_file.FileName);

				string commit_output;
				var result = new CommitResult();
				result.IsSuccess = RunHg(args, out commit_output);
				if (result.IsSuccess)
				{
					Hg.ParseCommitOutput(commit_output, result);
				}
				return result;
			}
		}

		//-----------------------------------------------------------------------------
		public CommitResult CommitAll(HgCommitOptions options, string comment, string date_str)
		{
			using(var commit_msg_file = new CommitMessageFile(comment))
			{
				var args = new HgArgsBuilderZero();
				args.Append("commit");
				args.AppendVerbose();

				if ((options & HgCommitOptions.CloseBranch) == HgCommitOptions.CloseBranch)
					args.Append("--close-branch");

				args.Append("-l");
				args.AppendPath(commit_msg_file.FileName);

				args.Append("-d");
				args.Append(date_str);

				string commit_output;
				var result = new CommitResult();
				result.IsSuccess = RunHg(args, out commit_output);
				if (result.IsSuccess)
				{
					Hg.ParseCommitOutput(commit_output, result);
				}
				return result;
			}
		}

		//------------------------------------------------------------------
		public bool Revert(string revision, HgRevertOptions options)
		{
			var args = new HgArgsBuilderZero();
			args.Append("revert");

			if ((options & HgRevertOptions.All) == HgRevertOptions.All)
				args.Append("-a");

			if ((options & HgRevertOptions.NoBackup) == HgRevertOptions.NoBackup)
				args.Append("--no-backup");

			if (revision.Length > 0)
				args.AppendRevision(revision);

			return RunHg(args);
		}

		//-----------------------------------------------------------------------------
		public bool Revert(IEnumerable<string> files)
		{
			var args = new HgArgsBuilderZero();
			args.Append("revert");
			args.Append("--no-backup");

			using (var list_file = new HgListFile(files))
			{
				if (!args.AppendListFile(list_file.FileName))
					throw new HgCommandLineException();

				return RunHg(args);
			}
		}

		//-----------------------------------------------------------------------------
		public bool DiffSilent(string file, out bool is_different)
		{
			is_different = true;

			var args = new HgArgsBuilderZero();
			args.Append("diff");
			args.AppendPath(file);

			string output;
			if (!RunHg(args, out output))
				return false;

			is_different = output.Length != 0;
			return true;
		}

		//------------------------------------------------------------------
		public bool ViewFile(string file, string revision)
		{
			if (revision == "")
			{
				Hg.RunHgEditorAsync(Path.Combine(WorkDir, file), DeleteFlag.Keep);
				return true;
			}

			string temp_file = Util.GetTempFileNameForFile(file);

			if (!CheckoutFile(file, revision, temp_file))
				return false;

			Hg.RunHgEditorAsync(temp_file, DeleteFlag.Delete);
			return true;
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Call diff tool for a file in workspace to compare with parent_revision
		/// </summary>
		public bool DiffWithRevision(string file, string parent_revision, out bool is_different)
		{
			is_different = true;

			var files = Status(file);
			if (files.Count != 1)
				return false;

			var file_info = files[0];
			if (String.IsNullOrEmpty(file_info.CopiedFrom))
			{
				string temp1 = Util.GetTempFileNameForFile(file_info.File);
				if (!CheckoutFile(file_info.File, parent_revision, temp1))
					return false;

				Hg.RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(WorkDir, file), DeleteFlag.Keep);
			}
			else
			{
				var hg_files = Manifest(parent_revision);
				
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
				if (!CheckoutFile(parent_filename, parent_revision, temp1))
					return false;

				Hg.RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(WorkDir, file), DeleteFlag.Keep);
			}

			return true;
		}


		//-----------------------------------------------------------------------------
		public bool Diff(string file, out bool is_different)
		{
			is_different = true;

			var files = Status(file);

			if (	files.Count == 1
				&&	(files[0].Status == HgFileStatus.Added || files[0].Status == HgFileStatus.Modified)
				&&	files[0].CopiedFrom != null)
			{
				var file_info = files[0];

				string temp1 = Util.GetTempFileNameForFile(file_info.CopiedFrom);
				if (!CheckoutFile(file_info.CopiedFrom, "", temp1))
					return false;

				Hg.RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(WorkDir, file), DeleteFlag.Keep);
				
				return true;
			}

			if (	files.Count == 1
				&&	files[0].Status == HgFileStatus.Modified)
			{
				var file_info = files[0];

				string temp1 = Util.GetTempFileNameForFile(file_info.File);
				if (!CheckoutFile(file_info.File, "", temp1))
					return false;

				Hg.RunDiffToolAsync(temp1, DeleteFlag.Delete,
					Path.Combine(WorkDir, file), DeleteFlag.Keep);

				return true;
			}

			//-----------------------------------------------------------------------------
			var hg_files = Manifest();
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
		public bool CheckoutFile(string file, int rev, string temp_file)
		{
			return CheckoutFile(file, rev.ToString(), temp_file);
		}

		//-----------------------------------------------------------------------------
		public bool CheckoutFile(string file, string rev, string temp_file)
		{
			var args = new HgArgsBuilderZero();
			args.Append("cat");
			args.Append("-o");
			args.AppendPath(temp_file);

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendPath(file);
			return RunHg(args);
		}

		//-----------------------------------------------------------------------------
		public bool Diff(string file1, int rev1, string file2, int rev2)
		{
			return Diff(file1, rev1.ToString(), file2, rev2.ToString());
		}

		//-----------------------------------------------------------------------------
		public bool Diff(string file1, string rev1, string file2, string rev2)
		{
			string temp1 = Util.GetTempFileNameForFile(file1);
			string temp2 = Util.GetTempFileNameForFile(file2);

			if (	!CheckoutFile(file1, rev1, temp1)
				||	!CheckoutFile(file2, rev2, temp2))
			{
				if (File.Exists(temp1))
					File.Delete(temp1);

				if (File.Exists(temp2))
					File.Delete(temp2);

				return false;
			}

			Hg.RunDiffToolAsync(temp1, DeleteFlag.Delete, temp2, DeleteFlag.Delete);
			return true;
		}

		//-----------------------------------------------------------------------------
		public bool TrackRename(string file, string rev, out string old_file)
		{
			var args = new HgArgsBuilderZero();
			args.Append("debugrename");

			if (rev.Length > 0)
				args.AppendRevision(rev);

			args.AppendPath(file);
			old_file = string.Empty;

			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return false;

				mem_stream.Seek(0, SeekOrigin.Begin);
				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					string line = output_stream.ReadLine();
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
		}


		//-----------------------------------------------------------------------------
		public bool Remove(IEnumerable<string> files)
		{
			var args = new HgArgsBuilderZero();
			args.Append("remove");

			using (var list_file = new HgListFile(files))
			{
				if (!args.AppendListFile(list_file.FileName))
					throw new HgCommandLineException();

				return RunHg(args);
			}
		}

		//-----------------------------------------------------------------------------
		public bool Rename(string file, string new_file)
		{
			var args = new HgArgsBuilderZero();
			args.Append("rename");
			args.Append("-A");
			args.AppendPath(file);
			args.AppendPath(new_file);

			return RunHg(args);
		}

		//------------------------------------------------------------------
		public List<PathAlias> GetPaths()
		{
			var args = new HgArgsBuilderZero();
			args.Append("paths");

			var paths = new List<PathAlias>();

			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return new List<PathAlias>();

				mem_stream.Seek(0, SeekOrigin.Begin);
				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					while (true)
					{
						var str = output_stream.ReadLine();
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
				}
			}

			return paths;
		}

		//-----------------------------------------------------------------------------
		public List<RenameInfo> FindRenames(string file, List<ChangeDesc> changes)
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
							if (!TrackRename(current.Path, ch.Rev.ToString(), out prev_name))
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

		//------------------------------------------------------------------
		/// <summary>
		/// Identify current revision and check for uncommited changes
		/// </summary>
		/// <returns>Returns null on error</returns>
		public IdentifyInfo Identify()
		{
			var args = new HgArgsBuilderZero();
			args.Append("identify");
			args.Append("-ni");
			args.AppendDebug();
			
			// FIXME: by default it shows short 6 bytes sha1
			// to get full sha1 the --debug arg should be specified

			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return null;

				mem_stream.Seek(0, SeekOrigin.Begin);

				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					var str = output_stream.ReadLine();
					return Hg.ParseIdentifyLine(str);
				}
			}
		}

		//------------------------------------------------------------------
		public List<BookmarkInfo> Bookmarks()
		{
			var args = new HgArgsBuilderZero();
			args.Append("bookmarks");
			args.AppendDebug();

			var bookmarks = new List<BookmarkInfo>();
			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return new List<BookmarkInfo>();

				mem_stream.Seek(0, SeekOrigin.Begin);

				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					while (true)
					{
						var str = output_stream.ReadLine();
						if (str == null)
							break;

						var bookmark = HgBookmarks.ParseBookmarkLine(str);
						if (bookmark != null)
							bookmarks.Add(bookmark);
					}
				}
			}

			return bookmarks;
		}



		//------------------------------------------------------------------
		public List<TagInfo> Tags()
		{
			var args = new HgArgsBuilderZero();
			args.Append("tags");
			args.AppendVerbose();
			args.AppendDebug();

			var tags = new List<TagInfo>();

			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return new List<TagInfo>();

				mem_stream.Seek(0, SeekOrigin.Begin);

				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					while (true)
					{
						var str = output_stream.ReadLine();
						if (str == null)
							break;

						var tag = Hg.ParseTagLine(str);
						if (tag != null)
							tags.Add(tag);
					}
				}
			}

			return tags;
		}

		//------------------------------------------------------------------
		public List<BranchInfo> Branches(HgBranchesOptions options)
		{
			var args = new HgArgsBuilderZero();
			args.Append("branches");
			args.AppendDebug();
			args.AppendVerbose();

			if ((options & HgBranchesOptions.Active) == HgBranchesOptions.Active)
				args.Append("-a");
			if ((options & HgBranchesOptions.Closed) == HgBranchesOptions.Closed)
				args.Append("-c");

			var branches = new List<BranchInfo>();

			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return new List<BranchInfo>();

				mem_stream.Seek(0, SeekOrigin.Begin);

				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					while (true)
					{
						var str = output_stream.ReadLine();
						if (str == null)
							break;

						var branch = Hg.ParseBranchLine(str);
						if (branch != null)
							branches.Add(branch);
					}
				}
			}

			return branches;
		}

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Update repository to specified revision
		/// </summary>
		/// <param name="work_dir"></param>
		/// <param name="revision"></param>
		/// <param name="options"></param>
		/// <exception cref="HgUncommitedChangesException">Throwed when there are uncommited changes and the check option set</exception>
		/// <returns>true on success</returns>
		public bool Update(string revision, HgUpdateOptions options)
		{
			var args = new HgArgsBuilderZero();
			args.Append("update");

			switch (options)
			{
				case HgUpdateOptions.None:
					break;
				case HgUpdateOptions.Check:
					{
						args.Append("-c");
						break;
					}
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

			string output;
			string error;
			int res = RawCommandString(args, out output, out error);
			if (res != 0)
			{
				if (options == HgUpdateOptions.Check)
				{
					if (!string.IsNullOrEmpty(error) && error.Contains("uncommitted local changes"))
						throw new HgUncommitedChangesException();
				}
				return false;
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public bool AddTag(string tag, string revision, HgTagOptions options, string commit_message)
		{
			var args = new HgArgsBuilderZero();
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
				args.Append(commit_message);
			}

			args.Append(tag);

			if (args.Length > Hg.MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			return RunHg(args);
		}

		//-----------------------------------------------------------------------------
		public bool Copy(string dest_path, string src_path, HgCopyOptions options)
		{
			var args = new HgArgsBuilderZero();
			args.Append("copy");

			if ((options & HgCopyOptions.Force) == HgCopyOptions.Force)
				args.Append("-f");

			if ((options & HgCopyOptions.After) == HgCopyOptions.After)
				args.Append("-A");

			args.AppendPath(src_path);
			args.AppendPath(dest_path);

			if (args.Length > Hg.MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			return RunHg(args);
		}

		//-----------------------------------------------------------------------------
		public bool RemoveTag(string tag, HgTagOptions option)
		{
			var args = new HgArgsBuilderZero();
			args.Append("tag");

			if ((option & HgTagOptions.Local) == HgTagOptions.Local)
				args.Append("-l");

			args.Append("--remove");
			args.Append(tag);

			if (args.Length > Hg.MaxCmdLength)
				throw new ArgumentException("Command line length is too long");

			return RunHg(args);
		}

		//-----------------------------------------------------------------------------
		public bool Merge(string revision, HgMergeOptions options)
		{
			var args = new HgArgsBuilderZero();
			args.Append("merge");

			if ((options & HgMergeOptions.Force) == HgMergeOptions.Force)
				args.Append("-f");

			if (revision.Length > 0)
				args.AppendRevision(revision);

			return RunHg(args);
		}

		//-----------------------------------------------------------------------------
		public List<string> ShowConfig()
		{
			var args = new HgArgsBuilderZero();
			args.Append("showconfig");

			var lines = new List<string>();
			using (var mem_stream = new MemoryStream())
			{
				int res = RawCommandStream(args, mem_stream);
				if (res != 0)
					return new List<string>();

				mem_stream.Seek(0, SeekOrigin.Begin);

				using (var output_stream = new StreamReader(mem_stream, Encoding))
				{
					while (true)
					{
						var str = output_stream.ReadLine();
						if (str == null)
							break;

						lines.Add(str);
					}
				}
			}

			return lines;
		}
	}
}
