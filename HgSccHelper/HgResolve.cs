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
			StringBuilder args = new StringBuilder();
			args.Append("resolve");
			args.Append(" " + file.Quote());

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		public bool MarkAsResolved(string work_dir, IEnumerable<string> files)
		{
			StringBuilder args = new StringBuilder();
			args.Append("resolve");
			args.Append(" -m");

			var cmd_line = new StringBuilder();
			cmd_line.Append(args.ToString());

			var hg = new Hg();

			foreach (string f in files)
			{
				var str = " " + f.Quote();

				if ((cmd_line.Length + str.Length) > Hg.MaxCmdLength)
				{
					if (!hg.RunHg(work_dir, cmd_line.ToString()))
						return false;

					cmd_line.Remove(0, cmd_line.Length);
					cmd_line.Append(args.ToString());
				}

				cmd_line.Append(str);
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
			StringBuilder args = new StringBuilder();
			args.Append("resolve");
			args.Append(" -m");
			args.Append(" -a");

			var hg = new Hg();
			return hg.RunHg(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		public bool MarkAsUnresolved(string work_dir, IEnumerable<string> files)
		{
			StringBuilder args = new StringBuilder();
			args.Append("resolve");
			args.Append(" -u");

			var cmd_line = new StringBuilder();
			cmd_line.Append(args.ToString());

			var hg = new Hg();

			foreach (string f in files)
			{
				var str = " " + f.Quote();

				if ((cmd_line.Length + str.Length) > Hg.MaxCmdLength)
				{
					if (!hg.RunHg(work_dir, cmd_line.ToString()))
						return false;

					cmd_line.Remove(0, cmd_line.Length);
					cmd_line.Append(args.ToString());
				}

				cmd_line.Append(str);
			}

			if (cmd_line.Length != args.Length)
			{
				return hg.RunHg(work_dir, cmd_line.ToString());
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public List<ResolveInfo> List(string work_dir)
		{
			StringBuilder args = new StringBuilder();
			args.Append("resolve");
			args.Append(" -l");

			var resolve_list = new List<ResolveInfo>();

			var hg = new Hg();
			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;

				var resolved_prefix = "R ";
				var unresolved_prefix = "U ";

				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

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
						resolve_list.Add(info);
					}
				}

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<ResolveInfo>();
			}

			return resolve_list;
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
