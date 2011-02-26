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
using System.Diagnostics;

namespace HgSccHelper
{
	//=============================================================================
	public class HgVersion
	{
		//-----------------------------------------------------------------------------
		public static HgVersionInfo ParseVersion(string str)
		{
			// Trying to parse a version from a string like this:
			// Mercurial Distributed SCM (version 1.7.5+126-d100702326d5)

			if (String.IsNullOrEmpty(str))
				return null;

			if (!str.StartsWith("Mercurial Distributed SCM"))
				return null;

			string version_prefix = "(version ";
			int idx = str.IndexOf(version_prefix);
			if (idx == -1)
				return null;

			str = str.Substring(idx + version_prefix.Length);

			var version = new HgVersionInfo();
			var fields = str.Split(new[] {'.', '+', '-', ')'});
			if (fields.Length < 2)
				return null;

			int release;
			int major;

			if (!int.TryParse(fields[0], out release))
				return null;

			version.Release = release;

			if (!int.TryParse(fields[1], out major))
				return null;

			version.Major = major;

			if (fields.Length > 2)
			{
				int minor;
				if (int.TryParse(fields[2], out minor))
					version.Minor = minor;
			}

			return version;
		}

		//------------------------------------------------------------------
		public HgVersionInfo VersionInfo(string work_dir)
		{
			var args = new HgArgsBuilder();
			args.Append("version");

			var hg = new Hg();
			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				var str = stream.ReadLine();

				var version = ParseVersion(str);

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return null;

				return version;
			}
		}
	}

	//------------------------------------------------------------------
	public class HgVersionInfo : IComparable<HgVersionInfo>
	{
		public int Release { get; set; }
		public int Major { get; set; }
		public int Minor { get; set; }
		
		//-----------------------------------------------------------------------------
		public int CompareTo(HgVersionInfo other)
		{
			int result = Release.CompareTo(other.Release);
			if (result != 0)
				return result;

			result = Major.CompareTo(other.Major);
			if (result != 0)
				return result;

			return Minor.CompareTo(other.Minor);
		}

		//-----------------------------------------------------------------------------
		public override string ToString()
		{
			if (Minor == 0)
				return string.Format("{0}.{1}", Release, Major);

			return string.Format("{0}.{1}.{2}", Release, Major, Minor);
		}
	}
}
