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
