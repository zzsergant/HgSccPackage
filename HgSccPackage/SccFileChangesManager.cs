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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using HgSccHelper;

//==================================================================
namespace HgSccPackage
{
	//==================================================================
	class SccFileChangesManager	: IVsFileChangeEvents, IDisposable
	{
		private readonly Dictionary<string, uint> files;
		private readonly IVsFileChangeEx file_change_service;
		private bool disposed;

		//------------------------------------------------------------------
		public SccFileChangesManager(SccProvider provider)
		{
			file_change_service = (IVsFileChangeEx) provider.GetService(typeof (SVsFileChangeEx));
			files = new Dictionary<string, uint>();
		}

		//------------------------------------------------------------------
		public bool AdviseFileChange(string file)
		{
			Logger.WriteLine("AdviseFileChange: {0}", file);

			uint flags =
					(uint)_VSFILECHANGEFLAGS.VSFILECHG_Add
				|	(uint)_VSFILECHANGEFLAGS.VSFILECHG_Del
				|	(uint)_VSFILECHANGEFLAGS.VSFILECHG_Attr
				|	(uint)_VSFILECHANGEFLAGS.VSFILECHG_Size
				|	(uint)_VSFILECHANGEFLAGS.VSFILECHG_Time;

			uint cookie;
			var err	= file_change_service.AdviseFileChange(file, flags, this, out cookie);
			if (err == VSConstants.S_OK)
			{
				var lower = file.ToLower();
				if (files.ContainsKey(lower))
				{
					Logger.WriteLine("Advise: file already monitoring, {0}", file);
					return false;
				}

				files[lower] = cookie;
				return true;
			}

			return false;
		}

		//------------------------------------------------------------------
		public bool UnadviseFileChange(string file)
		{
			Logger.WriteLine("UnadviseFileChange: {0}", file);
			var lower = file.ToLower();
			if (!files.ContainsKey(lower))
			{
				Logger.WriteLine("Unadvise: file is not monitoring, {0}", file);
				return false;
			}

			uint cookie = files[lower];
			file_change_service.UnadviseFileChange(cookie);
			files.Remove(lower);
			return true;
		}

		#region Implementation of IVsFileChangeEvents

		//------------------------------------------------------------------
		public int FilesChanged(uint cChanges, string[] rgpszFile, uint[] rggrfChange)
		{
			for(int i = 0; i < cChanges; ++i)
			{
				Logger.WriteLine("FileChanged[{0}]: {1}, {2}", i, rgpszFile[i], rggrfChange[i]);
			}

			return VSConstants.S_OK;
		}

		//------------------------------------------------------------------
		public int DirectoryChanged(string pszDirectory)
		{
			Logger.WriteLine("DirectoryChange: {0}", pszDirectory);
			return VSConstants.S_OK;
		}

		#endregion

		#region Implementation of IDisposable

		/// <summary>
		///                     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			if (!disposed)
			{
				foreach (var cookie in files.Values)
				{
					file_change_service.UnadviseFileChange(cookie);
				}

				files.Clear();
				disposed = true;
			}
		}

		#endregion
	}
}
