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
	public class SccProviderStorage : IDisposable
	{
		private HgScc hgscc;
		private Dictionary<string, HgFileInfo> cache;
		public Dictionary<string, List<SccProviderStorage>> Subs { get; set; }
		public event EventHandler UpdateEvent;
		private bool disposed;

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
			Subs = new Dictionary<string, List<SccProviderStorage>>();
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
			Logger.WriteLine("Close storage: {0}", WorkingDir);

			if (hgscc != null)
			{
				hgscc.Dispose();
				hgscc = null;
			}
			
			cache.Clear();
			
			foreach (var kvp in Subs)
			{
				foreach (var subrepo in kvp.Value)
				{
					subrepo.Close();
				}
			}

			Subs.Clear();
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
				wnd.SubRepoDirs = hgscc.SubRepoDirs;
				wnd.FilesToCommit = files;

				proxy.ShowDialog();

				if (wnd.DialogResult == true)
				{
					var files_list = new List<string>(wnd.CommitedFiles);
					UpdateCache(wnd.CommitedFiles);

					foreach (var kvp in Subs)
					{
						foreach (var sub_storage in kvp.Value)
						{
							if (wnd.CommitedSubrepoFiles.ContainsKey(kvp.Key))
							{
								files_list.AddRange(wnd.CommitedSubrepoFiles[kvp.Key]);

								// FIXME: Since not all commited files are listed in CommitedSubrepoFiles
								// (for example Removed files are not listed)
								// updating only part of files leaves the file cache in wrong state.
								// So, we are reseting a subrepo cache completely

								sub_storage.ReloadCache();
/*
								sub_storage.UpdateCache(wnd.CommitedSubrepoFiles[kvp.Key]);
*/
							}
						}
					}

					commited_files = files_list;
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
				wnd.FileName = relative_path;

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
				wnd.FileName = relative_path;

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
		public List<HgFileInfo> GetStatus(IEnumerable<string> files)
		{
			if (!IsValid)
				return new List<HgFileInfo>();

			var lst = new List<HgFileInfo>();
			foreach (var f in files)
			{
				var info = new HgFileInfo {File = f, Status = HgFileStatus.NotTracked};
				lst.Add(info);
				
				Logger.WriteLine("GetStatus: {0}", f);
			}

			var info_lst = lst.ToArray();

			SccErrors error = hgscc.QueryInfo2(info_lst);
			if (error == SccErrors.Ok)
				return new List<HgFileInfo>(info_lst);

			return new List<HgFileInfo>();
		}

		//------------------------------------------------------------------
		public void SetCacheStatus(IEnumerable<HgFileInfo> files)
		{
			foreach (var info in files)
			{
				Logger.WriteLine("SetCacheStatus: {0}, {1}", info.File, info.Status);
				cache[info.File.ToLower()] = info;
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

				if (wnd.UpdateContext.IsParentChanged)
					RaiseUpdateEvent();
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

			return IsPathUnderDirectory(hgscc.WorkingDir, path);
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

		//------------------------------------------------------------------
		public bool IsStorageControlled(string path)
		{
			if (!IsValid)
			{
				return false;
			}

			//check if controlled by subrepo
			foreach (string sub in hgscc.SubRepoDirs)
			{
				var sub_dir = Path.Combine(hgscc.WorkingDir.ToLower(), sub.ToLower());
				if (IsPathUnderDirectory(sub_dir, path))
				{
					return false;
				}
			}

			return IsPathUnderDirectory(hgscc.WorkingDir, path);
		}

		//------------------------------------------------------------------
		public void AddSubrepoStorage(string work_dir, SccProviderStorage storage)
		{
			string root = hgscc.GetRootPath(work_dir);
			if (!Subs.ContainsKey(root))
			{
				Subs[root] = new List<SccProviderStorage>();
			}
			Subs[root].Add(storage);
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Checks that specified path is located under a specified directory
		/// </summary>
		/// <param name="dir"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		private static bool IsPathUnderDirectory(string dir, string path)
		{
			var dir_low = dir.ToLower();
			var path_low = path.ToLower();
			
			if (path_low.StartsWith(dir_low))
			{
				if (path_low == dir_low)
					return true;

				return path_low[dir_low.Length] == Path.DirectorySeparatorChar;
			}

			return false;
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			Close();
		}
	}
}
