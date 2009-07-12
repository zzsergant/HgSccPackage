using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace HgSccHelper
{
	static class Logger
	{
		static object critical = new object();

		//-----------------------------------------------------------------------------
		private static string GetLogPath()
		{
			string appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string hgdata = Path.Combine(appdata, "HgScc");
			if (!Directory.Exists(hgdata))
				Directory.CreateDirectory(hgdata);

			string log_path = Path.Combine(hgdata, "hgscc.log");
			if (!File.Exists(log_path))
				using (File.CreateText(log_path))
				{
				}

			return log_path;
		}

		//-----------------------------------------------------------------------------
		[Conditional("LOG_ENABLED")]
		public static void WriteLine(string line)
		{
			lock (critical)
			{
				try
				{
					using (StreamWriter writer = File.AppendText(GetLogPath()))
					{
						writer.WriteLine(line);
					}
				}
				catch
				{

				}
			}
		}

		[Conditional("LOG_ENABLED")]
		public static void WriteLine(string format, params object[] parameters)
		{
			WriteLine(String.Format(format, parameters));
		}
	}
}
