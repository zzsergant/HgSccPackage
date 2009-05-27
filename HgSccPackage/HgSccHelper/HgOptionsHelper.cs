using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Win32;
using System.Windows.Forms;

namespace HgSccPackage.HgSccHelper
{
	static class HgOptionsHelper
	{
		//-----------------------------------------------------------------------------
		public static List<string> DetectDiffTools()
		{
			var lst = new List<string>();

			string path = string.Empty;

			path = DetectKDiff();
			if (File.Exists(path))
				lst.Add(path);

			path = DetectP4Diff();
			if (File.Exists(path))
				lst.Add(path);

			return lst;
		}

		//-----------------------------------------------------------------------------
		private static string DetectKDiff()
		{
			string path = null;

			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\KDiff3"))
				{
					if (key != null)
					{
						path = (string)key.GetValue("");
						if (path != null)
							path = Path.Combine(path, "KDiff3.exe");
					}
				}
			}
			catch (System.Exception)
			{
			}

			if (path == null)
				path = string.Empty;

			return path;
		}

		//-----------------------------------------------------------------------------
		private static string DetectP4Diff()
		{
			string path = null;

			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\perforce\environment"))
				{
					if (key != null)
					{
						path = (string)key.GetValue("P4INSTROOT");
						if (path != null)
							path = Path.Combine(path, "p4diff.exe");
					}
				}
			}
			catch (System.Exception)
			{
			}

			if (path == null)
				path = string.Empty;

			return path;
		}

		//------------------------------------------------------------------
		public static bool BrowseDiffTool(ref string diff_tool)
		{
			using (var f = new OpenFileDialog())
			{
				f.RestoreDirectory = true;
				f.Filter = "Executables|*.exe";
				f.CheckFileExists = true;

				if (diff_tool.Length != 0)
				{
					if (File.Exists(diff_tool))
					{
						f.InitialDirectory = Path.GetDirectoryName(diff_tool);
						f.FileName = Path.GetFileName(diff_tool);
					}
				}

				var res = f.ShowDialog();
				if (res == DialogResult.OK)
				{
					diff_tool = f.FileName;
					return true;
				}
			}

			return false;
		}
	}
}
