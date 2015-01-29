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

using System.Windows.Forms;
using HgSccHelper;

namespace HgSccPackage
{
	//=============================================================================
	class HgVersionChecker
	{
		//-----------------------------------------------------------------------------
		public static bool CheckVersion(HgVersionInfo required_version)
		{
			// Read last known hg version from HgSccPackage config

			HgVersionInfo last_known_version = KnownHgVersion.Get();

			if (	last_known_version == null
				||	last_known_version.CompareTo(required_version) < 0
				)
			{
				HgVersionInfo ver = new HgVersion().VersionInfo("");

				if (ver == null)
				{
					var msg = "HgSccPackage is unable to check a mercurial client version.\n" +
					"You must have a mercurial client (hg.exe) v" + required_version + " or higher installed.\n" + 
					"You can get it from mercurial.selenic.com.";

					MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return false;
				}
				else
				{
					Logger.WriteLine("Hg version is: {0}", ver);

					if (ver.CompareTo(required_version) < 0)
					{
						var msg = "You have mercurial v" + ver
								  + " installed, but HgSccPackage requires mercurial v"
								  + required_version + " or higher installed.\n"
								  + "Do you want to use this mercurial client anyway (not recommended) ?";

						var result = MessageBox.Show(msg, "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
						if (result != DialogResult.Yes)
							return false;
					}

					KnownHgVersion.Set(required_version);
					return true;
				}
			}

			return true;
		}
	}
}
