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
	//------------------------------------------------------------------
	static class VsSolutionUtil
	{
		// The guid of solution folders
		public static readonly Guid SolutionFolderGuid =
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
					guidClassID.CompareTo(SolutionFolderGuid) == 0)
				{
					return true;
				}
			}

			return false;
		}

		//------------------------------------------------------------------
		public static IEnumerable<IVsHierarchy> EnumHierarchies(this IVsSolution solution)
		{
			return solution.EnumHierarchies(new Guid());
		}

		//------------------------------------------------------------------
		public static IEnumerable<IVsHierarchy> EnumHierarchies(this IVsSolution solution, Guid only_this_type)
		{
			Guid rguidEnumOnlyThisType = only_this_type;
			IEnumHierarchies ppenum = null;
			ErrorHandler.ThrowOnFailure(
				solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION,
								   ref rguidEnumOnlyThisType, out ppenum));

			IVsHierarchy[] rgelt = new IVsHierarchy[1];
			uint pceltFetched = 0;
			while (ppenum.Next(1, rgelt, out pceltFetched) == VSConstants.S_OK &&
				   pceltFetched == 1)
			{
				yield return rgelt[0];
			}
		}
	}
}
