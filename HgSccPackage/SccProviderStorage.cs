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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using HgSccHelper;
using System.Diagnostics;
using HgSccHelper.UI;

namespace HgSccPackage
{
	// This class defines basic source control status values
	public enum SourceControlStatus
	{
		scsUncontrolled = 0,
		scsClean,
		scsModified,
		scsAdded,
		scsCopied,
		scsRemoved,
		scsDeleted,
		scsIgnored
	};

	//------------------------------------------------------------------
	public class SourceControlInfo
	{
		public string File { get; set; }
		public SourceControlStatus Status { get; set; }
	};

	//==================================================================
	public class SccProviderStorage
	{
		private HgScc hgscc;
		private Dictionary<string, HgFileInfo> cache;

		public event EventHandler UpdateEvent;

		//------------------------------------------------------------------
		private void RaiseUpdateEvent()
		{
			var e = UpdateEvent;
			if (e != null)
				e(this, EventArgs.Empty);
		}

		//------------------------------------------------------------------
		public SccProviderStorage()
		{
			cache = new Dictionary<string, HgFileInfo>();
		}

		//------------------------------------------------------------------
		public bool IsValid
		{
			get
			{
				return hgscc != null && !String.IsNullOrEmpty(hgscc.WorkingDir); 
			}
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir
		{
			get
			{
				return IsValid ? hgscc.WorkingDir : string.Empty;
			}
		}

		//------------------------------------------------------------------
		public SccErrors Init(string work_dir, SccOpenProjectFlags flags)
		{
			Logger.WriteLine("SccProviderStorage: {0}", work_dir);
			if (hgscc == null)
				hgscc = new HgScc();

			var err = hgscc.OpenProject(work_dir, flags);
			if (err == SccErrors.Ok)
				ReloadCache();

			return err;
		}

		//------------------------------------------------------------------
		public bool IsAnyModifiedFiles()
		{
			if (!IsValid)
				return false;

			foreach (var file_info in cache.Values)
			{
				switch (file_info.Status)
				{
					case HgFileStatus.Added:
					case HgFileStatus.Deleted:
					case HgFileStatus.Removed:
					case HgFileStatus.Modified:
						return true;
				}
			}

			return false;
		}

		//------------------------------------------------------------------
		public void ReloadCache()
		{
			if (!IsValid)
				return;

			Logger.WriteLine("Reloading storage cache");
			
			ResetCache();
			
			foreach (var pair in hgscc.QueryInfoFullDict())
			{
				var file = pair.Key;
				var status = pair.Value;

				cache[Path.Combine(hgscc.WorkingDir, file).ToLower()] = status;
			}
		}

		//------------------------------------------------------------------
		public void Close()
		{
			hgscc = null;
			cache.Clear();
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Adds files to source control by adding them to the list of "controlled" files in the current project
		/// and changing their attributes to reflect the "checked in" status.
		/// </summary>
		public SccErrors AddFilesToStorage(IEnumerable<string> files)
		{
			if (!IsValid)
				return SccErrors.UnknownError;

			files = RemoveDuplicates(files);

			var files_list = new List<string>(files);
			var status_list = new SourceControlStatus[files_list.Count];

			var status_error = GetStatusForFiles(files_list.ToArray(), status_list);

			var lst = new List<SccAddFile>();
			for (int i = 0; i < files_list.Count; ++i)
			{
				if (status_list[i] == SourceControlStatus.scsUncontrolled)
				{
					var f = files_list[i];
					lst.Add(new SccAddFile { File = f, Flags = SccAddFlags.FileTypeAuto });
					Logger.WriteLine("Adding: {0}", f);
				}
			}

			if (lst.Count == 0)
				return SccErrors.Ok;

			var err = hgscc.Add(IntPtr.Zero, lst.ToArray(), "Adding files");
			if (err == SccErrors.Ok)
			{
/*
				// Т.к. при добавлении файлов может измениться проект,
				// то сбрасывает кэш
				ResetCache();
*/
				UpdateCache(files);
			}

			return err;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Renames a "controlled" file. If the project file is being renamed, rename the whole storage file
		/// </summary>
		public SccErrors RenameFileInStorage(string strOldName, string strNewName)
		{
			// FIXME: RenameFiles!!
			if (!IsValid)
				return SccErrors.UnknownError;

			Logger.WriteLine("Rename: {0} to {1}", strOldName, strNewName);

			var err = hgscc.Rename(IntPtr.Zero, strOldName, strNewName);
			if (err == SccErrors.Ok)
			{
/*
				// Т.к. при переименовании файлов может измениться проект,
				// то сбрасываем кэш
				ResetCache();
*/
				UpdateCache(new[]{strOldName, strNewName});
			}

			return err;
		}

		//------------------------------------------------------------------
		private static SourceControlStatus FromHgStatus(HgFileInfo file_info)
		{
			switch (file_info.Status)
			{
				case HgFileStatus.Added:
					{
						if (!String.IsNullOrEmpty(file_info.CopiedFrom))
							return SourceControlStatus.scsCopied;

						return SourceControlStatus.scsAdded;
					}
				case HgFileStatus.Clean:
					return SourceControlStatus.scsClean;
				case HgFileStatus.Deleted:
					return SourceControlStatus.scsDeleted;
				case HgFileStatus.Ignored:
					return SourceControlStatus.scsIgnored;
				case HgFileStatus.Modified:
					{
						if (!String.IsNullOrEmpty(file_info.CopiedFrom))
							return SourceControlStatus.scsCopied;

						return SourceControlStatus.scsModified;
					}
				case HgFileStatus.NotTracked:
					return SourceControlStatus.scsUncontrolled;
				case HgFileStatus.Removed:
					return SourceControlStatus.scsRemoved;
				case HgFileStatus.Tracked:
					return SourceControlStatus.scsClean;
			}

			return SourceControlStatus.scsUncontrolled;
		}

		//------------------------------------------------------------------
		public SccErrors GetStatusForFiles(SourceControlInfo[] files)
		{
			if (!IsValid)
				return SccErrors.UnknownError;

			var not_in_cache = new List<string>();

			foreach (var file in files)
			{
				if (!cache.ContainsKey(file.File.ToLower()))
					not_in_cache.Add(file.File);
			}

			if (not_in_cache.Count != 0)
			{
				//UpdateCache(not_in_cache);
			}

			foreach (var file in files)
			{
				HgFileInfo info;
				if (cache.TryGetValue(file.File.ToLower(), out info))
				{
					file.Status = FromHgStatus(info);
				}
				else
				{
					if (IsDirectory(file.File) && IsPathUnderRoot(file.File))
						file.Status = SourceControlStatus.scsClean;
				}
				//Logger.WriteLine("GetFileStatus: {0} = {1}", file.File, file.Status);
			}

			return SccErrors.Ok;
		}

		//------------------------------------------------------------------
		private bool IsDirectory(string path)
		{
			try
			{
				return Directory.Exists(path);
			}
			catch (ArgumentException)
			{
			}

			return false;
		}

		//------------------------------------------------------------------
		public SccErrors GetStatusForFiles(string[] files, SourceControlStatus[] statuses)
		{
			if (!IsValid)
			{
				for (int i = 0; i < statuses.Length; ++i)
					statuses[i] = SourceControlStatus.scsUncontrolled;
				return SccErrors.Ok;
			}

			var not_in_cache = new List<string>();

			foreach (var file in files)
			{
				if (!cache.ContainsKey(file.ToLower()))
					not_in_cache.Add(file);
			}

			if (not_in_cache.Count != 0)
			{
				//UpdateCache(not_in_cache);
			}

			for (int i = 0; i < files.Length; ++i)
			{
				HgFileInfo info;
				if (cache.TryGetValue(files[i].ToLower(), out info))
				{
					statuses[i] = FromHgStatus(info);
				}
				else
				{
					if (IsDirectory(files[i]) && IsPathUnderRoot(files[i]))
						statuses[i] = SourceControlStatus.scsClean;
				}

				//Logger.WriteLine("GetFileStatus: {0} = {1}", files[i], statuses[i]);
			}

			return SccErrors.Ok;
		}

		//------------------------------------------------------------------
		private static IEnumerable<string> RemoveDuplicates(IEnumerable<string> items)
		{
			var hash_set = new HashSet<string>();
			foreach (var item in items)
				hash_set.Add(item);
			return hash_set.ToArray();
		}

		//------------------------------------------------------------------
		public SccErrors Commit(IEnumerable<string> files, out IEnumerable<string> commited_files)
		{
			if (!IsValid)
			{
				commited_files = new List<string>();
				return SccErrors.UnknownError;
			}

			files = RemoveDuplicates(files);

			foreach (var f in files)
			{
				Logger.WriteLine("Commit: {0}", f);
			}

			using (var proxy = new WpfToWinFormsProxy<CommitWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;
				wnd.FilesToCommit = files;

				proxy.ShowDialog();

				if (wnd.DialogResult == true)
				{
					commited_files = wnd.CommitedFiles;
					UpdateCache(commited_files);
					return SccErrors.Ok;
				}
			}

			commited_files = new List<string>();
			return SccErrors.OpNotPerformed;
		}

		//------------------------------------------------------------------
		public SccErrors Revert(IEnumerable<string> files, out IEnumerable<string> reverted_files)
		{
			if (!IsValid)
			{
				reverted_files = new List<string>();
				return SccErrors.UnknownError;
			}

			files = RemoveDuplicates(files);

			foreach (var f in files)
			{
				Logger.WriteLine("Revert: {0}", f);
			}

			using (var proxy = new WpfToWinFormsProxy<RevertWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;
				wnd.FilesToRevert = files;

				proxy.ShowDialog();

				if (wnd.UpdateContext.IsParentChanged)
					RaiseUpdateEvent();

				if (wnd.DialogResult == true)
				{
					reverted_files = wnd.RevertedFiles;
					UpdateCache(reverted_files);
					return SccErrors.Ok;
				}
			}

			reverted_files = new List<string>();
			return SccErrors.OpNotPerformed;
		}

		//------------------------------------------------------------------
		public SccErrors RemoveFiles(IEnumerable<string> files)
		{
			if (!IsValid)
				return SccErrors.UnknownError;

			files = RemoveDuplicates(files);

			var error = hgscc.Remove(IntPtr.Zero, files);
			if (error != SccErrors.Ok)
				return error;

			foreach (var f in files)
			{
				Logger.WriteLine("Remove: {0}", f);
				SetCacheStatus(f, HgFileStatus.Removed);
			}
			return SccErrors.Ok;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns a source control status inferred from the file's attributes on local disk
		/// </summary>
		public SourceControlStatus GetFileStatus(string filename)
		{
			var info = new SourceControlInfo { File = filename };
			var lst = new SourceControlInfo[]{ info };

			GetStatusForFiles(lst);

			return lst[0].Status;
		}

		//------------------------------------------------------------------
		public void ViewHistory(string filename)
		{
			if (!IsValid)
				return;

			string relative_path;
			if (!hgscc.GetRelativePath(filename, out relative_path))
			{
				Logger.WriteLine("Can't get relative path: " + filename);
				return;
			}

			using (var proxy = new WpfToWinFormsProxy<FileHistoryWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;
				wnd.FileName = relative_path.Replace('\\', '/');

				proxy.ShowDialog();

				if (wnd.UpdateContext.IsParentChanged)
					RaiseUpdateEvent();
			}
		}

		//------------------------------------------------------------------
		public void Annotate(string filename)
		{
			if (!IsValid)
				return;

			string relative_path;
			if (!hgscc.GetRelativePath(filename, out relative_path))
			{
				Logger.WriteLine("Can't get relative path: " + filename);
				return;
			}

			using (var proxy = new WpfToWinFormsProxy<AnnotateWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;
				wnd.FileName = relative_path.Replace('\\', '/');

				proxy.ShowDialog();

				if (wnd.UpdateContext.IsParentChanged)
					RaiseUpdateEvent();
			}
		}

		//------------------------------------------------------------------
		public void RemoveFile(string filename)
		{
			var files = new[] {filename};
			RemoveFiles(files);
		}

		//------------------------------------------------------------------
		public void UpdateCache(IEnumerable<string> files)
		{
			if (!IsValid)
				return;

			var lst = new List<HgFileInfo>();
			foreach (var f in files)
			{
				var info = new HgFileInfo {File = f, Status = HgFileStatus.NotTracked};
				lst.Add(info);
				
				Logger.WriteLine("UpdateCache: {0}", f);
			}
			
			var info_lst = lst.ToArray();

			SccErrors error = hgscc.QueryInfo2(info_lst);
			if (error == SccErrors.Ok)
			{
				foreach (var info in info_lst)
				{
					cache[info.File.ToLower()] = info;
				}
			}
		}

		//------------------------------------------------------------------
		private void ResetCache()
		{
			cache.Clear();
		}

		//------------------------------------------------------------------
		public void UpdateFileCache(string file)
		{
			UpdateCache(new[] {file});
		}

		//------------------------------------------------------------------
		public void SetCacheStatus(string file, HgFileStatus status)
		{
			Logger.WriteLine("SetCacheStatus: {0}, {1}", file, status);
			
			HgFileInfo info;
			if (cache.TryGetValue(file.ToLower(), out info))
			{
				info.Status = status;
			}
			else
			{
				Logger.WriteLine("File not found in cache");
			}
		}

		//------------------------------------------------------------------
		public void Compare(string file)
		{
			if (!IsValid)
				return;

			hgscc.Diff(IntPtr.Zero, file, SccDiffFlags.None);
		}

		//------------------------------------------------------------------
		public void ViewChangeLog()
		{
			if (!IsValid)
				return;

			using (var proxy = new WpfToWinFormsProxy<RevLogWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;

				proxy.ShowDialog();

				if (wnd.UpdateContext.IsParentChanged)
					RaiseUpdateEvent();
			}
		}

		//------------------------------------------------------------------
		public void Grep()
		{
			if (!IsValid)
				return;

			using (var proxy = new WpfToWinFormsProxy<GrepWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;

				proxy.ShowDialog();

				if (wnd.UpdateContext.IsParentChanged)
					RaiseUpdateEvent();
			}
		}

		//------------------------------------------------------------------
		public void Synchronize()
		{
			if (!IsValid)
				return;

			using (var proxy = new WpfToWinFormsProxy<SynchronizeWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;

				proxy.ShowDialog();
			}
		}

		//------------------------------------------------------------------
		public bool Update()
		{
			if (!IsValid)
				return false;

			using (var proxy = new WpfToWinFormsProxy<UpdateWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;
				
				proxy.ShowDialog();

				if (wnd.UpdateContext.IsParentChanged)
					RaiseUpdateEvent();

				return wnd.UpdateContext.IsParentChanged;
			}
		}

		//------------------------------------------------------------------
		public void Tags()
		{
			if (!IsValid)
				return;

			using (var proxy = new WpfToWinFormsProxy<TagsWindow>())
			{
				var wnd = proxy.Wnd;
				wnd.WorkingDir = hgscc.WorkingDir;

				proxy.ShowDialog();
			}
		}

		//------------------------------------------------------------------
		public bool IsPathUnderRoot(string path)
		{
			if (!IsValid)
				return false;

			return path.ToLower().StartsWith(hgscc.WorkingDir.ToLower());
		}

		//------------------------------------------------------------------
		public bool Copy(string new_path, string old_path)
		{
			if (!IsValid)
				return false;

			bool is_after_copy_occured = true;
			var err = hgscc.Copy(IntPtr.Zero, new_path, old_path, is_after_copy_occured);
			if (err == SccErrors.Ok)
			{
				UpdateCache(new []{new_path});
			}
			return err == SccErrors.Ok;
		}
	}
}
