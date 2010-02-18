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
using System.IO;
using HgSccHelper;

namespace HgSccPackage.Vs
{
	//==================================================================
	class FilesChangeMonitor
	{
		class FileUpdateInfo
		{
			public string FileName { get; set; }
			public DateTime LastWriteTime { get; set; }
		}

		List<FileUpdateInfo> files;
		C5.HashSet<string> changed_files;

		//------------------------------------------------------------------
		public FilesChangeMonitor()
		{
			files = new List<FileUpdateInfo>();
			changed_files = new C5.HashSet<string>();
		}

		//------------------------------------------------------------------
		public bool Add(string filename)
		{
			if (!File.Exists(filename))
			{
				Logger.WriteLine("FilesChangeMonitor.Add - file {0} is not exists", filename);
				return false;
			}

			var file_info = new FileInfo(filename);
			files.Add(new FileUpdateInfo { FileName = filename, LastWriteTime = file_info.LastWriteTime });
			return true;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Checks if any of the files have been written/updated
		/// </summary>
		/// <returns>true if there is at least one file updated</returns>
		public bool CheckForUpdates()
		{
			changed_files.Clear();
			foreach (var file in files)
			{
				if (!File.Exists(file.FileName))
				{
					// file is not exists anymore
					changed_files.Add(file.FileName.ToLower());
				}
				else
				{
					var file_info = new FileInfo(file.FileName);
					if (file_info.LastWriteTime.CompareTo(file.LastWriteTime) != 0)
						changed_files.Add(file.FileName.ToLower());
				}
			}

			return changed_files.Count > 0;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Check if file was changed. The actual check is done in CheckForUpdates method.
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public bool IsFileChanged(string filename)
		{
			return changed_files.Contains(filename.ToLower());
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns changed files. Note, that file names will be in lower case.
		/// </summary>
		public IEnumerable<string> ChangedFiles
		{
			get
			{
				foreach (var filename in changed_files)
					yield return filename;
			}
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns monitored files.
		/// </summary>
		public IEnumerable<string> MonitoredFiles
		{
			get
			{
				foreach (var file in files)
					yield return file.FileName;
			}
		}
	}
}
