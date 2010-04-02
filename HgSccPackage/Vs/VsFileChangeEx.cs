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
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace HgSccPackage.Vs
{
	//==================================================================
	class VsFileChangeEx : IDisposable, IVsFileChangeEvents
	{
		Dictionary<string, uint> files_cookies;
		// Dictionary<string, uint> dirs_cookies;

		//------------------------------------------------------------------
		public IVsFileChangeEx Interface { get; private set; }

		//------------------------------------------------------------------
		public VsFileChangeEx(IVsFileChangeEx file_change_ex)
		{
			if (file_change_ex == null)
				throw new ArgumentNullException("file_change_ex");

			Interface = file_change_ex;
			files_cookies = new Dictionary<string, uint>();
			// dirs_cookies = new List<uint>();
		}

		//------------------------------------------------------------------
		public bool AdviseFileChange(string filename, _VSFILECHANGEFLAGS flags)
		{
			var lower_filename = filename.ToLower();

			if (files_cookies.ContainsKey(lower_filename))
				throw new ArgumentException("File allready advised for changes: " + filename);

			uint cookie;
			var error = Interface.AdviseFileChange(filename, (uint)flags, this, out cookie);

			if (error != VSConstants.S_OK)
				return false;

			files_cookies[lower_filename] = cookie;
			return true;
		}

		//------------------------------------------------------------------
		public bool UnAdviseFileChange(string filename)
		{
			var lower_filename = filename.ToLower();

			uint cookie;
			if (!files_cookies.TryGetValue(lower_filename, out cookie))
				return false;

			Interface.UnadviseFileChange(cookie);
			files_cookies.Remove(lower_filename);
			return true;
		}

		#region IDisposable Members

		//------------------------------------------------------------------
		public void Dispose()
		{
			if (Interface != null)
			{
				foreach (var cookie in files_cookies.Values)
					Interface.UnadviseFileChange(cookie);

				//foreach (var cookie in dirs_cookies)
				//    Interface.UnadviseDirChange(cookie);

				Interface = null;
			}
		}

		#endregion

		#region IVsFileChangeEvents Members

		//------------------------------------------------------------------
		public event EventHandler<DirChangeEventArgs> DirectoryChangedEvent;

		//------------------------------------------------------------------
		public int DirectoryChanged(string pszDirectory)
		{
			var handler = DirectoryChangedEvent;
			if (handler != null)
				handler(this, new DirChangeEventArgs { Directory = pszDirectory });

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public event EventHandler<FileChangeEventArgs> FileChangedEvent;

		//------------------------------------------------------------------
		public int FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange)
		{
			var handler = FileChangedEvent;
			if (handler != null && cChanges != 0)
			{
				var changes = new FileChangeEventArgs();

				for(uint i = 0; i < cChanges; ++i)
				{
					var file_info = new FileChangeInfo();
					file_info.Filename = rgpszFile[i];
					file_info.Flags = (_VSFILECHANGEFLAGS)rggrfChange[i];

					changes.FilesChanged.Add(file_info);
				}

				handler(this, changes);
			}

			return VSConstants.S_OK;
		}

		#endregion
	}

	//==================================================================
	class DirChangeEventArgs : EventArgs
	{
		public string Directory { get; set; }
	}

	//==================================================================
	class FileChangeInfo
	{
		public string Filename { get; set; }
		public _VSFILECHANGEFLAGS Flags { get; set; }
	}

	//==================================================================
	class FileChangeEventArgs : EventArgs
	{
		public FileChangeEventArgs()
		{
			FilesChanged = new List<FileChangeInfo>();
		}

		public List<FileChangeInfo> FilesChanged { get; private set; }
	}
}
