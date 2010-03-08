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
using System.Diagnostics;

namespace HgSccHelper
{
	//------------------------------------------------------------------
	public enum HgBranchOptions
	{
		None,
		Force
	}

	//==================================================================
	public class HgBranch
	{
		/// <summary>
		/// Returns a branch name to commit or null
		/// </summary>
		/// <param name="work_dir"></param>
		/// <returns></returns>
		public string GetBranchName(string work_dir)
		{
			StringBuilder args = new StringBuilder();
			args.Append("branch");

			var hg = new Hg();
			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;

				string branch_name = stream.ReadLine();

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return null;

				return branch_name;
			}
		}

		//------------------------------------------------------------------
		/// <summary>
		/// Reset branch name to parent branch name
		/// </summary>
		/// <param name="work_dir"></param>
		/// <returns></returns>
		public bool ResetBranchName(string work_dir)
		{
			StringBuilder args = new StringBuilder();
			args.Append("branch");
			args.Append(" --clean");

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		public bool SetBranchName(string work_dir, string branch_name, HgBranchOptions options)
		{
			StringBuilder args = new StringBuilder();
			args.Append("branch");

			if (options == HgBranchOptions.Force)
				args.Append(" --force");

			args.Append(" " + branch_name.Quote());

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}
	}
}
