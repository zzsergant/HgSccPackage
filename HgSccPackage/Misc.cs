using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace HgSccPackage
{
	//=========================================================================
	class Misc
	{
		private static readonly string log_path = @"d:\Work\Tests1\log.txt";

		static Misc()
		{
#if RELEASE_DEPLOY
#else
			File.Delete(log_path);
#endif
		}

		//-------------------------------------------------------------------------
		[Conditional("DEBUG"), Conditional("RELEASE")]
		public static void Log(string format, params object[] args)
		{
			Write(String.Format(CultureInfo.CurrentUICulture, format, args));
		}

		//-------------------------------------------------------------------------
		[Conditional("DEBUG"), Conditional("RELEASE")]
		public static void Log(string format, object arg)
		{
			Write(String.Format(CultureInfo.CurrentUICulture,
										  format, arg));
		}

		//-------------------------------------------------------------------------
		[Conditional("DEBUG"), Conditional("RELEASE")]
		private static void Write(string str)
		{
			using (var file = File.AppendText(log_path))
			{
				file.WriteLine(str);
			}
		}
	}
}
