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
	static public class Logger
	{
		//------------------------------------------------------------------
		static Logger()
		{
			InitLog();
		}

		//------------------------------------------------------------------
		[Conditional("LOG_ENABLED")]
		static void InitLog()
		{
			string appdata = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string hgdata = Path.Combine(appdata, "HgSccPackage");
			if (!Directory.Exists(hgdata))
				Directory.CreateDirectory(hgdata);

			string log_path = Path.Combine(hgdata, "hgsccpkg.log");
			if (File.Exists(log_path))
			{
				try
				{
					File.Delete(log_path);
				}
				catch(System.IO.IOException)
				{

				}
			}

			var file_listener = new TextWriterTraceListener(log_path);
			Debug.Listeners.Add(file_listener);
			Debug.AutoFlush = true;
		}

		//-----------------------------------------------------------------------------
		//-----------------------------------------------------------------------------
		[Conditional("LOG_ENABLED")]
		public static void WriteLine(string line)
		{
			Debug.WriteLine(line);
		}

		[Conditional("LOG_ENABLED")]
		public static void WriteLine(string format, params object[] parameters)
		{
			WriteLine(String.Format(format, parameters));
		}
	}
}
