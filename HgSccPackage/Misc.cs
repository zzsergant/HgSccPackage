using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;

namespace HgSccPackage
{
	//=========================================================================
	class Misc
	{
		private static readonly string log_path = @"d:\Work\Tests1\log.txt";

		static Misc()
		{
			File.Delete(log_path);
		}

		//-------------------------------------------------------------------------
		public static void Log(string format, params object[] args)
		{
			Write(String.Format(CultureInfo.CurrentUICulture, format, args));
		}

		//-------------------------------------------------------------------------
		public static void Log(string format, object arg)
		{
			Write(String.Format(CultureInfo.CurrentUICulture,
										  format, arg));
		}

		//-------------------------------------------------------------------------
		private static void Write(string str)
		{
			using (var file = File.AppendText(log_path))
			{
				file.WriteLine(str);
			}
		}
	}
}
