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
using System.Runtime.InteropServices;

namespace HgSccPackage.Vs
{
	//==================================================================
	class RdtFilesReloader : IDisposable
	{
		List<IVsHierarchy> controlled_projects;
		SccProvider scc_provider;
		IVsFileChangeEx change;
		FilesChangeMonitor rdt_files_monitor;
		VsRunningDocumentTable rdt;
		IVsSolution solution;

		Dictionary<string, IVsPersistDocData> rdt_doc_list;

		//------------------------------------------------------------------
		public RdtFilesReloader(SccProvider scc_provider, IEnumerable<IVsHierarchy> controlled_projects)
		{
			this.scc_provider = scc_provider;
			this.controlled_projects = new List<IVsHierarchy>(controlled_projects);
			change = (IVsFileChangeEx)scc_provider.GetService(typeof(SVsFileChangeEx));
			rdt = new VsRunningDocumentTable((IVsRunningDocumentTable)scc_provider.GetService(typeof(SVsRunningDocumentTable)));
			solution = (IVsSolution)scc_provider.GetService(typeof(SVsSolution));

			rdt_files_monitor = new FilesChangeMonitor();
			rdt_doc_list = new Dictionary<string, IVsPersistDocData>();

			foreach (var doc_info in rdt.EnumDocuments())
			{
				if (doc_info.DocData == IntPtr.Zero)
					continue;

				var doc = Marshal.GetObjectForIUnknown(doc_info.DocData) as IVsPersistDocData;
				if (doc != null)
				{
					rdt_doc_list[doc_info.MkDocument.ToLower()] = doc;
					change.IgnoreFile(0, doc_info.MkDocument, 1);
					rdt_files_monitor.Add(doc_info.MkDocument);
				}
			}
		}

		#region IDisposable Members

		//------------------------------------------------------------------
		void IDisposable.Dispose()
		{
			rdt_files_monitor.CheckForUpdates();

			foreach (var filename in rdt_files_monitor.ChangedFiles)
			{
				IVsPersistDocData doc;
				if (rdt_doc_list.TryGetValue(filename.ToLower(), out doc))
				{
					doc.ReloadDocData((uint) _VSRELOADDOCDATA.RDD_IgnoreNextFileChange);
					rdt_doc_list.Remove(filename.ToLower());
				}
			}

			foreach (var filename in rdt_files_monitor.MonitoredFiles)
			{
				change.SyncFile(filename);
				change.IgnoreFile(0, filename, 0);
			}

			if (rdt != null)
			{
				rdt.Dispose();
				rdt = null;
			}
		}

		#endregion
	}
}
