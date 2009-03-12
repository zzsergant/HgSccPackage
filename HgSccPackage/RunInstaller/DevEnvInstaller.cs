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
}
