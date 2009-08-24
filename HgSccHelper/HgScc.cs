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
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using C5;

namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	class FackedCheckoutInfo
	{
		public string File { get; set; }
	}

	//-----------------------------------------------------------------------------
	public class HgScc
	{
		public string WorkingDir { get; private set; }
		private readonly Hg hg;

		//-----------------------------------------------------------------------------
		public HgScc()
		{
			hg = new Hg();
		}

		//-----------------------------------------------------------------------------
		public bool GetRelativePath(string path, out string relative)
		{
			relative = string.Empty;
			string f = path.ToLower();
			if (!f.StartsWith(WorkingDir))
				return false;

			if (path.Length == WorkingDir.Length)
				relative = "";
			else
				relative = path.Substring(WorkingDir.Length + 1);
			return true;
		}

		//-----------------------------------------------------------------------------
		public SccErrors OpenProject(string local_proj_path, SccOpenProjectFlags flags)
		{
			string root = hg.Root(local_proj_path);
			bool is_root_exist = root.Length > 0;

			if (!is_root_exist)
			{
				if ((flags | SccOpenProjectFlags.CreateIfNew) == SccOpenProjectFlags.CreateIfNew)
				{
					if (!hg.CreateRepository(local_proj_path))
						return SccErrors.CouldNotCreateProject;

					root = local_proj_path;
				}
				else
				{
					if ((flags | SccOpenProjectFlags.SilentOpen) == SccOpenProjectFlags.SilentOpen)
						return SccErrors.UnknownProject;

					return SccErrors.NonSpecificError;
				}
			}

			WorkingDir = root.ToLower();

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors CloseProject()
		{
			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		private static SccStatus ToSccStatus(HgFileStatus status)
		{
			SccStatus scc = SccStatus.None;
			switch (status)
			{
				case HgFileStatus.Added: scc |= SccStatus.Controlled | SccStatus.CheckedOut | SccStatus.OutByUser | SccStatus.Modified; break;
				case HgFileStatus.Clean: scc |= SccStatus.Controlled; break;
				case HgFileStatus.Deleted: scc |= SccStatus.Controlled | SccStatus.Deleted; break;
				case HgFileStatus.Ignored: scc |= SccStatus.NotControlled; break;
				case HgFileStatus.Modified: scc |= SccStatus.Controlled | SccStatus.CheckedOut | SccStatus.OutByUser | SccStatus.Modified; break;
				case HgFileStatus.NotTracked: scc |= SccStatus.NotControlled; break;
				case HgFileStatus.Removed: scc |= SccStatus.Controlled | SccStatus.Deleted; break;
				case HgFileStatus.Tracked: scc |= SccStatus.Controlled; break;
				default:
					scc = SccStatus.Invalid;
					break;
			}

			return scc;
		}

/*
		//-----------------------------------------------------------------------------
		public SccErrors LookupProjects(string folder, out SccFileInfo[] files)
		{
			var dict = new HashDictionary<string, bool>();
			dict.Add(".sln", true);
			dict.Add(".atp", true);
			dict.Add(".dbp", true);
			dict.Add(".vap", true);
			dict.Add(".vsproj", true);
			dict.Add(".vbdproj", true);
			dict.Add(".vddproj", true);
			dict.Add(".dbproj", true);
			dict.Add(".vbp;*.vip;*.vbproj", true);
			dict.Add(".vcproj", true);
			dict.Add(".vdproj", true);
			dict.Add(".vmx", true);
			dict.Add(".vup", true);
			dict.Add(".csproj", true);

			string[] all_files = Directory.GetFiles(folder);
			var found_list = new List<string>();
			foreach (var f in all_files)
			{
				string ext = Path.GetExtension(f).ToLower();
				if (dict.ContainsKey(ext))
					found_list.Add(f);
			}

			string[] found_files = found_list.ToArray();
			files = new SccFileInfo[found_files.Length];
			
			for (int i = 0; i < found_files.Length; ++i)
			{
				files[i] = new SccFileInfo { File = Path.GetFullPath(found_files[i]), Status = SccStatus.None };
				Logger.WriteLine("-> " + files[i].File);
			}

			return QueryInfo(files);
		}
*/

		//-----------------------------------------------------------------------------
		public HashDictionary<string, HgFileInfo> QueryInfoFullDict()
		{
			var dict = new HashDictionary<string, HgFileInfo>();
//			var hg_files = hg.Manifest(WorkingDir);

			foreach (var file_status in hg.Status(WorkingDir))
			{
				dict.Add(file_status.File, file_status);
			}

/*
			foreach (var file in hg_files)
			{
				string f = file.Replace('/', '\\');
				if (!dict.ContainsKey(f))
				{
//					Logger.WriteLine(String.Format("Manifest File: {0}", f));
					HgFileStatus status = HgFileStatus.Tracked;
					dict.Add(f, new HgFileInfo { File = f, Status = status } );
				}
			}
*/

			return dict;
		}

		//-----------------------------------------------------------------------------
		public SccErrors QueryInfo(HgFileInfo[] files)
		{
			// TODO: Check if project is opened
			var dict = new HashDictionary<string, HgFileStatus>();

			foreach (var file_status in hg.Status(WorkingDir))
			{
				string file = file_status.File.ToLower();
				dict.Add(file, file_status.Status);
			}

			foreach (var info in files)
			{
				HgFileStatus status = HgFileStatus.NotTracked;
				string file;
				if (GetRelativePath(info.File, out file)
					&& dict.Find(file.ToLower(), out status))
				{
					info.Status = status;
				}
				else
				{
					info.Status = HgFileStatus.NotTracked;		  
				}

				// FIXME: file.ToLower() !
//				dict.Remove(file);
			}

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors QueryInfo2(HgFileInfo[] files)
		{
			// TODO: Check if project is opened
			var dict = new HashDictionary<string, HgFileStatus>();
			var lst = new List<string>();

			int length = 0;
			foreach (var f in files)
			{
				string file;
				if (GetRelativePath(f.File, out file))
				{
					lst.Add(file);
					length += file.Length;
				}
			}

			var stats = (length > Hg.MaxCmdLength) ? hg.Status(WorkingDir) : hg.Status(WorkingDir, lst);

			foreach (var file_status in stats)
			{
				string file = file_status.File.ToLower();
				dict.Add(file, file_status.Status);
			}

			foreach (var info in files)
			{
				HgFileStatus status = HgFileStatus.NotTracked;
				string file;
				if (GetRelativePath(info.File, out file)
					&& dict.Find(file.ToLower(), out status))
				{
					info.Status = status;
				}
				else
				{
					info.Status = HgFileStatus.NotTracked;
				}
			}

			return SccErrors.Ok;
		}

/*
		//-----------------------------------------------------------------------------
		public SccErrors QueryInfo(SccFileInfo [] files)
		{
			var lst = new HgFileInfo[files.Length];
			for (int i = 0; i < files.Length; ++i)
				lst[i] = new HgFileInfo { File = files[i].File };

			SccErrors error = QueryInfo(lst);

			for (int i = 0; i < files.Length; ++i)
				files[i].Status = ToSccStatus(lst[i].Status);

			return error;
		}
*/

		//-----------------------------------------------------------------------------
		public SccErrors Add(IntPtr hwnd, SccAddFile[] files, string comment)
		{
			var lst = new List<string>();
			foreach (var f in files)
				lst.Add(f.File);

			return Add(hwnd, lst);
		}

		//-----------------------------------------------------------------------------
		public SccErrors Add(IntPtr hwnd, IEnumerable<string> files)
		{
			// TODO: Check if project is opened
			var add_files = new List<string>();

			foreach (var file in files)
			{
				string f;
				if (!GetRelativePath(file, out f))
					return SccErrors.InvalidFilePath;

				add_files.Add(f);
			}

			if (add_files.Count > 0)
			{
				if (!hg.Add(WorkingDir, add_files))
					return SccErrors.OpNotPerformed;
			}
			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors CommitAll(IntPtr hwnd, string comment)
		{
			if (!hg.CommitAll(WorkingDir, comment))
				return SccErrors.OpNotPerformed;

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors CommitAll(IntPtr hwnd, string comment, string date_str)
		{
			if (!hg.CommitAll(WorkingDir, comment, date_str))
				return SccErrors.OpNotPerformed;

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		private SccErrors CommitInternal(IntPtr hwnd, IEnumerable<string> files, string comment, out IEnumerable<string> commited_files)
		{
			var dict = new HashDictionary<string, HgFileStatus>();
			var files_status_dict = QueryInfoFullDict();
			var commited_files_list = new List<string>();
			commited_files = commited_files_list;

			foreach (var f in files)
			{
				string file;
				if (!GetRelativePath(f, out file))
					return SccErrors.NonSpecificError;

				dict.Add(file.ToLower(), HgFileStatus.NotTracked);
			}

			using (var form = new CommitForm())
			{
				form.Comment = comment;
				form.Hg = hg;
				form.WorkingDir = WorkingDir;

				var commit_files = new List<CommitListItem>();
				var commit_removed = new HashDictionary<string, CommitListItem>();

				foreach (var tuple in files_status_dict)
				{
					var f = tuple.Value;

					switch (f.Status)
					{
						case HgFileStatus.Added:
						//						case HgFileStatus.Deleted:
						case HgFileStatus.Modified:
						case HgFileStatus.Removed:
							{
								var item = new CommitListItem();
								string lower_f = f.File.ToLower();
								item.Checked = dict.Contains(lower_f);
								item.FileInfo = f;

								commit_files.Add(item);
								if (f.Status == HgFileStatus.Removed)
									commit_removed.Add(f.File, item);
								break;
							}
					}
				}

				foreach (var f in commit_files)
				{
					if (	f.Checked
						&&	f.FileInfo.Status == HgFileStatus.Added
						&&	!String.IsNullOrEmpty(f.FileInfo.CopiedFrom))
					{
						CommitListItem item;
						
						if (commit_removed.Find(f.FileInfo.CopiedFrom, out item))
						{
							Logger.WriteLine("commit_removed: " + item.FileInfo.File);
							item.Checked = true;
						}
					}
				}

				if (commit_files.Count == 0)
					return SccErrors.Ok;

				form.SetItems(commit_files);

				if (form.ShowDialog() == DialogResult.OK)
				{
					var checked_files = form.GetCheckedItems();
					if (checked_files.Count == 0)
						return SccErrors.Ok;

					Logger.WriteLine("Checked: {0}, Commit: {1}, Status: {2}",
						checked_files.Count, commit_files.Count, files_status_dict.Count);

					SccErrors error = SccErrors.UnknownError;

					var to_commit_files = new List<string>();
					foreach (var commit_item in checked_files)
					{
						to_commit_files.Add(commit_item.FileInfo.File);
					}

					if (checked_files.Count == commit_files.Count)
					{
						error = CommitAll(hwnd, form.Comment);
					}
					else if (to_commit_files.Count > 0)
					{
						error = CheckInInternal(hwnd, to_commit_files, form.Comment);
					}
					else
					{
						return SccErrors.Ok;
					}

					if (error == SccErrors.Ok)
					{
						foreach (var f in to_commit_files)
						{
							commited_files_list.Add(Path.GetFullPath(Path.Combine(WorkingDir, f)));
						}
					}

					return error;
				}
				else
				{
					return SccErrors.I_OperationCanceled;
				}
			}
		}

		//-----------------------------------------------------------------------------
		public SccErrors CheckInInternal(IntPtr hwnd, IEnumerable<string> files, string comment)
		{
			// TODO: Check if project is opened
			try
			{
				if (!hg.Commit(WorkingDir, files, comment))
					return SccErrors.OpNotPerformed;
			}
			catch (HgCommandLineException)
			{
				MessageBox.Show("Resulted command line for hg.exe is too long. In this situation you can only commit all changed files (which is equivalent to invoking hg.exe without specified files).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return SccErrors.OpNotPerformed;
			}

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors Commit(IntPtr hwnd, IEnumerable<string> files, string comment, out IEnumerable<string> commited_files)
		{
			return CommitInternal(hwnd, files, comment, out commited_files);
		}

		//-----------------------------------------------------------------------------
		public SccErrors Revert(IntPtr hwnd, IEnumerable<string> files, out IEnumerable<string> reverted_files)
		{
			var local_files = new List<string>();
			var to_revert = new List<string>(files);
			reverted_files = to_revert;

			// FIXME: Add dialog with checkboxes
			foreach (var f in files)
			{
				string local_f;
				if (!GetRelativePath(f, out local_f))
					return SccErrors.InvalidFilePath;

				local_files.Add(local_f);
			}

			if (local_files.Count > 0)
			{
				if (!hg.Revert(WorkingDir, local_files))
					return SccErrors.NonSpecificError;
			}

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors Diff(IntPtr hwnd, string filename, SccDiffFlags flags)
		{
			string local_f;
			if (!GetRelativePath(filename, out local_f))
				return SccErrors.InvalidFilePath;

			switch(flags)
			{
				case SccDiffFlags.QdCheckSum:
				case SccDiffFlags.QdContents:
				case SccDiffFlags.QdTime:
				case SccDiffFlags.QuickDiff:
					{
						bool is_different;
						if (!hg.DiffSilent(WorkingDir, local_f, out is_different))
							return SccErrors.NonSpecificError;

						if (is_different)
							return SccErrors.I_FileDiffers;

						return SccErrors.Ok;
					}
				default:
					{
						bool is_different;

						try
						{
							if (!hg.Diff(WorkingDir, local_f, out is_different))
								return SccErrors.NonSpecificError;
						}
						catch (HgDiffException)
						{
							Util.HandleHgDiffException();
							return SccErrors.I_OperationCanceled;
						}

						if (!is_different)
						{
							System.Windows.Forms.MessageBox.Show("File: " + filename + " is up to date", "Diff");
							return SccErrors.Ok;
						}

						return SccErrors.Ok;
					}
			}
		}

		//-----------------------------------------------------------------------------
		public SccErrors Remove(IntPtr hwnd, IEnumerable<string> files)
		{
			// TODO: Check if project is opened
			var remove_files = new List<string>();

			foreach (var file in files)
			{
				string f;
				if (!GetRelativePath(file, out f))
					return SccErrors.InvalidFilePath;

				remove_files.Add(f);
			}

			if (remove_files.Count > 0)
			{
				if (!hg.Remove(WorkingDir, remove_files))
					return SccErrors.OpNotPerformed;
			}

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors Rename(IntPtr hwnd, string file, string new_file)
		{
			string f;
			string new_f;

			if (	!GetRelativePath(file, out f)
				||	!GetRelativePath(new_file, out new_f))
			{
				Logger.WriteLine("Can't get relative path: " + file + ", " + new_file);
				return SccErrors.InvalidFilePath;
			}

			if (!hg.Rename(WorkingDir, f, new_f))
				return SccErrors.NonSpecificError;

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors GetExtendedCapabilities(SccExCaps cap, out bool is_supported)
		{
			switch(cap)
			{
				case SccExCaps.DeleteCheckedOut:
				case SccExCaps.RenameCheckedOut:
					{
						is_supported = true;
						break;
					}
				default:
					{
						is_supported = false;
						break;
					}
			}

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors History(IntPtr hwnd, string file)
		{
			using (var form = new HistoryForm())
			{
				string relative_path;
				if (!GetRelativePath(file, out relative_path))
				{
					Logger.WriteLine("Can't get relative path: " + file);
					return SccErrors.InvalidFilePath;
				}

				var files = hg.Status(WorkingDir, relative_path);
				if (files.Count == 1
					&& files[0].Status == HgFileStatus.Added
					&& files[0].CopiedFrom != null)
				{
					var file_info = files[0];
					relative_path = file_info.CopiedFrom;
				}

				form.Path = relative_path;
	
				form.WorkingDir = WorkingDir;
				form.Hg = hg;

				var changes = hg.ChangesFull(WorkingDir, relative_path);
				if (changes.Count == 0)
				{
					Logger.WriteLine("Changes == 0");
					return SccErrors.I_OperationCanceled;
				}

				form.SetChangesList(changes);
				form.ShowDialog();
			}

			return SccErrors.Ok;
		}

		//-----------------------------------------------------------------------------
		public SccErrors GetCommandOptions(IntPtr hwnd, SccCommand command, ref IntPtr context)
		{
			switch(command)
			{
				case SccCommand.Options:
					{
						if (context == IntPtr.Zero)
						{
							return SccErrors.I_AdvSupport;
						}
						else
						{
							using (var f = new OptionsForm())
							{
								var res = f.ShowDialog();
								if (res == DialogResult.OK)
								{
//									MessageBox.Show(HgSccOptions.Options.DiffTool);
									return SccErrors.Ok;
								}

								return SccErrors.I_OperationCanceled;
							}
						}
					}
			}

			return SccErrors.Ok;
		}

	}
}
