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
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;

namespace HgSccInstaller.RunInstaller
{
	//==================================================================
	[RunInstaller(true)]
	public partial class DevEnvInstaller : Installer
	{
		//------------------------------------------------------------------
		public DevEnvInstaller()
		{
			InitializeComponent();
		}

		//------------------------------------------------------------------
		public override void Install(IDictionary state_saver)
		{
			base.Install(state_saver);

			bool install_vs2008 = false;
			if (Context.Parameters.ContainsKey("ci_vs2008"))
			{
				int ci_vs2008;
				if (int.TryParse(Context.Parameters["ci_vs2008"], out ci_vs2008))
					install_vs2008 = ci_vs2008 == 1;
			}

			if (install_vs2008)
			{
				var ci_devenv2008 = Context.Parameters["ci_devenv2008"];
				if (ci_devenv2008 != null && ci_devenv2008.EndsWith("devenv.exe", StringComparison.InvariantCultureIgnoreCase))
				{
					Process.Start(ci_devenv2008, "/setup /nosetupvstemplates").WaitForExit();
				}
			}

			bool install_vs2010 = false;
			if (Context.Parameters.ContainsKey("ci_vs2010"))
			{
				int ci_vs2010;
				if (int.TryParse(Context.Parameters["ci_vs2010"], out ci_vs2010))
					install_vs2010 = ci_vs2010 == 1;
			}

			if (install_vs2010)
			{
				var ci_devenv2010 = Context.Parameters["ci_devenv2010"];
				if (ci_devenv2010 != null && ci_devenv2010.EndsWith("devenv.exe", StringComparison.InvariantCultureIgnoreCase))
				{
					Process.Start(ci_devenv2010, "/setup /nosetupvstemplates").WaitForExit();
				}
			}
		}
	}
}
