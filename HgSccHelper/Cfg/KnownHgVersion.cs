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

namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	public class KnownHgVersion
	{
		private const string RegPath = "KnownHgVersion";

		//-----------------------------------------------------------------------------
		/// <summary>
		/// Read last known hg version from settings
		/// </summary>
		/// <returns>Returns null if failed</returns>
		public static HgVersionInfo Get()
		{
			int release;
			int major;
			int minor;

			if (!Cfg.Get(RegPath, "Release", out release, 0))
				return null;

			if (!Cfg.Get(RegPath, "Major", out major, 0))
				return null;

			if (!Cfg.Get(RegPath, "Minor", out minor, 0))
				return null;

			return new HgVersionInfo { Release = release, Major = major, Minor = minor };
		}

		//-----------------------------------------------------------------------------
		public static void Set(HgVersionInfo ver)
		{
			Cfg.Set(RegPath, "Release", ver.Release);
			Cfg.Set(RegPath, "Major", ver.Major);
			Cfg.Set(RegPath, "Minor", ver.Minor);
		}
	}
}
