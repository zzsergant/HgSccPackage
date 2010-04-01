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
using HgSccHelper;
using Microsoft.VisualStudio;
using System.Diagnostics;

namespace HgSccPackage.Vs
{
	class SlnOrProjectReloader : IDisposable
	{
		List<IVsHierarchy> projects;
		SccProvider scc_provider;
		IVsFileChangeEx change;
		FilesChangeMonitor sln_prj_monitor;
		VsRunningDocumentTable rdt;
		IVsSolution solution;

		//------------------------------------------------------------------
		public SlnOrProjectReloader(SccProvider scc_provider, IEnumerable<IVsHierarchy> projects)
		{
			this.scc_provider = scc_provider;
			this.projects = new List<IVsHierarchy>(projects);
			change = (IVsFileChangeEx)scc_provider.GetService(typeof(SVsFileChangeEx));
			rdt = new VsRunningDocumentTable((IVsRunningDocumentTable)scc_provider.GetService(typeof(SVsRunningDocumentTable)));
			solution = (IVsSolution)scc_provider.GetService(typeof(SVsSolution));

			sln_prj_monitor = new FilesChangeMonitor();

			var sln_name = scc_provider.GetSolutionFileName();

			// TODO: check if solution or project was renamed

			foreach (var project_hier in projects)
			{
				var project = project_hier as IVsProject;
				if (project != null)
				{
					var project_path = scc_provider.GetProjectFileName(project);
					if (project_path != null)
					{
						if (sln_prj_monitor.Add(project_path))
							change.IgnoreFile(0, project_path, 1);
					}
				}
				else
				{
					var scc_solution = project_hier as IVsSolution;
					if (scc_solution != null)
					{
						if (sln_prj_monitor.Add(sln_name))
							change.IgnoreFile(0, sln_name, 1);
					}
				}
			}
		}

		//------------------------------------------------------------------
		private void ReloadIfNeed()
		{
			if (sln_prj_monitor.CheckForUpdates())
			{
				// either solution or some projects changed
				var sln_name = scc_provider.GetSolutionFileName();

				if (sln_prj_monitor.IsFileChanged(sln_name))
				{
					Logger.WriteLine("+-+-: Solution changed, reloading");

					IVsPersistHierarchyItem2 phi2 = solution as IVsPersistHierarchyItem2;
					if (phi2 != null)
					{
						Logger.WriteLine("Reloading solution");
						phi2.ReloadItem(VSConstants.VSITEMID_ROOT, 0);
					}
				}
				else
				{
					var proj_map = new C5.HashDictionary<string, IVsProject>();

					foreach (var project_hier in projects)
					{
						var scc_project = project_hier as IVsProject;
						if (scc_project != null)
						{
							var project_path = scc_provider.GetProjectFileName(scc_project);
							if (project_path != null)
							{
								proj_map[project_path.ToLower()] = scc_project;
							}
						}
					}

					IVsPersistHierarchyItem2 phi2 = solution as IVsPersistHierarchyItem2;
					if (phi2 != null)
					{
						foreach (var filename in sln_prj_monitor.ChangedFiles)
						{
							Logger.WriteLine("Reloading project: {0}", filename);
							IVsProject scc_project;
							if (proj_map.Find(filename.ToLower(), out scc_project))
							{
								var doc_info = rdt.FindAndLockDocument(filename, _VSRDTFLAGS.RDT_NoLock);
								if (doc_info != null)
								{
									var err = phi2.ReloadItem(doc_info.ItemId, 0);
									Logger.WriteLine("err = {0}", err);
								}
							}
						}
					}
				}
			}
		}

		#region IDisposable Members

		//------------------------------------------------------------------
		void IDisposable.Dispose()
		{
			ReloadIfNeed();

			foreach (var filename in sln_prj_monitor.MonitoredFiles)
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
