using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace HgSccPackage.Vs
{
	//------------------------------------------------------------------
	static class VsSolutionUtil
	{
		// The guid of solution folders
		private static readonly Guid guidSolutionFolderProject =
			new Guid(0x2150e333, 0x8fdc, 0x42a3, 0x94, 0x74, 0x1a, 0x39, 0x56, 0xd4, 0x6d, 0xe8);

		//------------------------------------------------------------------
		/// <summary>
		/// Checks whether the specified project is a solution folder
		/// </summary>
		public static bool IsSolutionFolderProject(IVsHierarchy pHier)
		{
			var pFileFormat = pHier as IPersistFileFormat;
			if (pFileFormat != null)
			{
				Guid guidClassID;
				if (pFileFormat.GetClassID(out guidClassID) == VSConstants.S_OK &&
					guidClassID.CompareTo(guidSolutionFolderProject) == 0)
				{
					return true;
				}
			}

			return false;
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Returns a list of solution folders projects in the solution
		/// </summary>
		public static List<IVsHierarchy> GetSolutionFolders(IVsSolution solution)
		{
			// FIXME: If there are no duplicates, then this can be changed to List
			var mapHierarchies = new HashSet<IVsHierarchy>();

			Guid rguidEnumOnlyThisType = guidSolutionFolderProject;
			IEnumHierarchies ppenum = null;
			ErrorHandler.ThrowOnFailure(
				solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION,
								   ref rguidEnumOnlyThisType, out ppenum));

			IVsHierarchy[] rgelt = new IVsHierarchy[1];
			uint pceltFetched = 0;
			while (ppenum.Next(1, rgelt, out pceltFetched) == VSConstants.S_OK &&
				   pceltFetched == 1)
			{
				mapHierarchies.Add(rgelt[0]);
			}

			return mapHierarchies.ToList();
		}
	}
}
