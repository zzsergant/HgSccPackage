//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
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

namespace HgSccHelper
{
	class HgRebase
	{
		//-----------------------------------------------------------------------------
		public bool Rebase(string work_dir, string source_rev, string dest_rev)
		{
			var args = new HgArgsBuilder();
			args.Append("rebase");

			if (!string.IsNullOrEmpty(source_rev))
			{
				args.Append("--source");
				args.Append(source_rev.Quote());
			}

			if (!string.IsNullOrEmpty(dest_rev))
			{
				args.Append("--dest");
				args.Append(dest_rev.Quote());
			}

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}
	}
}
