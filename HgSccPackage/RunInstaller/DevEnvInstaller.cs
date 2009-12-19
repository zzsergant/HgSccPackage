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

using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using Microsoft.Win32;


namespace HgSccPackage.RunInstaller
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
		public override void Install(IDictionary stateSaver)
		{
			base.Install(stateSaver);

			using (var vs2008_hgpkg = Registry.LocalMachine.OpenSubKey(
				@"SOFTWARE\Microsoft\VisualStudio\9.0\Packages\{a7f26ca1-2000-4729-896e-0bbe9e380635}"))
			{
				if (vs2008_hgpkg != null)
				{
					using (RegistryKey setupKey = Registry.LocalMachine.OpenSubKey(
						  @"SOFTWARE\Microsoft\VisualStudio\9.0\Setup\VS"))
					{
						if (setupKey != null)
						{
							string devenv = setupKey.GetValue("EnvironmentPath").ToString();
							if (!string.IsNullOrEmpty(devenv))
							{
								Process.Start(devenv, "/setup /nosetupvstemplates").WaitForExit();
							}
						}
					}
				}
			}

			using (var vs2010_hgpkg = Registry.LocalMachine.OpenSubKey(
				@"SOFTWARE\Microsoft\VisualStudio\10.0\Packages\{a7f26ca1-2000-4729-896e-0bbe9e380635}"))
			{
				if (vs2010_hgpkg != null)
				{
					using (RegistryKey setupKey = Registry.LocalMachine.OpenSubKey(
					  @"SOFTWARE\Microsoft\VisualStudio\10.0\Setup\VS"))
					{
						if (setupKey != null)
						{
							string devenv = setupKey.GetValue("EnvironmentPath").ToString();
							if (!string.IsNullOrEmpty(devenv))
							{
								Process.Start(devenv, "/setup /nosetupvstemplates").WaitForExit();
							}
						}
					}
				}
			}
		}

	}
}
