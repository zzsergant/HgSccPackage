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
	public class HgResolve
	{
		//------------------------------------------------------------------
		public HgResolve()
		{
		}

		//------------------------------------------------------------------
		public bool Resolve(string work_dir, string file)
		{
			return Resolve(work_dir, file, "");
		}

		//------------------------------------------------------------------
		public bool Resolve(string work_dir, string file, string merge_tool)
		{
			var args = new HgArgsBuilder();
			args.Append("resolve");
			
			if (merge_tool.Length > 0)
			{
				args.Append("--config");
				args.Append("ui.merge=" + merge_tool.Quote());
			}

			args.AppendPath(file);

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		public bool MarkAsResolved(string work_dir, IEnumerable<string> files)
		{
			var args = new HgArgsBuilder();
			args.Append("resolve");
			args.Append("-m");

			var cmd_line = new HgArgsBuilder();
			cmd_line.Append(args.ToString());

			var hg = new Hg();

			foreach (string f in files)
			{
				if (!cmd_line.AppendFilenameWithLengthCheck(f))
				{
					if (!hg.RunHg(work_dir, cmd_line.ToString()))
						return false;

					cmd_line.Clear();
					cmd_line.Append(args.ToString());

					cmd_line.AppendFilenameWithLengthCheck(f);
				}
			}

			if (cmd_line.Length != args.Length)
			{
				return hg.RunHg(work_dir, cmd_line.ToString());
			}

			return true;
		}

		//------------------------------------------------------------------
		public bool MarkAllAsResolved(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("resolve");
			args.Append("-m");
			args.Append("-a");

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		public bool MarkAsUnresolved(string work_dir, IEnumerable<string> files)
		{
			var args = new HgArgsBuilder();
			args.Append("resolve");
			args.Append("-u");

			var cmd_line = new HgArgsBuilder();
			cmd_line.Append(args.ToString());

			var hg = new Hg();

			foreach (string f in files)
			{
				if (!cmd_line.AppendFilenameWithLengthCheck(f))
				{
					if (!hg.RunHg(work_dir, cmd_line.ToString()))
						return false;

					cmd_line.Clear();
					cmd_line.Append(args.ToString());

					cmd_line.AppendFilenameWithLengthCheck(f);
				}
			}

			if (cmd_line.Length != args.Length)
			{
				return hg.RunHg(work_dir, cmd_line.ToString());
			}

			return true;
		}

		//------------------------------------------------------------------
		public ResolveStatus GetResolveStatus(string work_dir, string file)
		{
			var args = new HgArgsBuilder();
			args.Append("resolve");
			args.Append("-l");

			if (file.Length == 0)
				throw new ApplicationException("Resolve status, invalid filename");

			args.AppendPath(file);

			var hg = new Hg();
			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;

				var resolved_prefix = "R ";
				var unresolved_prefix = "U ";
				ResolveStatus resolve_status = ResolveStatus.None;

				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					if (str.StartsWith(unresolved_prefix))
					{
						resolve_status = ResolveStatus.Unresolved;
						break;
					}
					
					if (str.StartsWith(resolved_prefix))
					{
						resolve_status = ResolveStatus.Resolved;
						break;
					}
				}

				proc.WaitForExit();
				return resolve_status;
			}
		}

		//-----------------------------------------------------------------------------
		public List<ResolveInfo> List(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("resolve");
			args.Append("-l");

			var resolve_list = new List<ResolveInfo>();

			var hg = new Hg();
			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;

				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					ResolveInfo info = ParseResolveListLine(str);
					if (info != null)
						resolve_list.Add(info);
				}

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<ResolveInfo>();
			}

			return resolve_list;
		}

		//-----------------------------------------------------------------------------
		public static ResolveInfo ParseResolveListLine(string str)
		{
			const string resolved_prefix = "R ";
			const string unresolved_prefix = "U ";

			ResolveInfo info = null;

			if (str.StartsWith(unresolved_prefix))
			{
				info = new ResolveInfo();
				info.Status = ResolveStatus.Unresolved;
				info.Path = str.Substring(resolved_prefix.Length);
			}
			else if (str.StartsWith(resolved_prefix))
			{
				info = new ResolveInfo();
				info.Status = ResolveStatus.Resolved;
				info.Path = str.Substring(unresolved_prefix.Length);
			}

			if (info != null)
			{
				info.Path = info.Path.Replace('/', '\\');
			}

			return info;
		}
	}

	//------------------------------------------------------------------
	public enum ResolveStatus
	{
		None,
		Resolved,
		Unresolved
	}

	//-----------------------------------------------------------------------------
	public class ResolveInfo
	{
		public string Path { get; set; }
		public ResolveStatus Status { get; set; }
	}
}
