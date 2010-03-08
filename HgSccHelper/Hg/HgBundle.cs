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

//=============================================================================
namespace HgSccHelper
{
	//=============================================================================
	public class HgBundle
	{
		//------------------------------------------------------------------
		public bool BundleAll(string work_dir, string destination)
		{
			StringBuilder args = new StringBuilder();
			args.Append("bundle");
			args.Append(" --all");

			args.Append(" " + destination.Quote());

			if (args.Length >= Hg.MaxCmdLength)
				throw new HgCommandLineException("Bundle");

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		public bool Bundle(string work_dir, string base_rev, string revision, string destination)
		{
			StringBuilder args = new StringBuilder();
			args.Append("bundle");

			if (revision.Length > 0)
				args.Append(" --rev " + revision.Quote());

			if (base_rev.Length > 0)
				args.Append(" --base " + base_rev.Quote());

			args.Append(" " + destination.Quote());

			if (args.Length >= Hg.MaxCmdLength)
				throw new HgCommandLineException("Bundle");

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}
	}
}
