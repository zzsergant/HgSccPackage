using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Microsoft.Samples.VisualStudio.SourceControlIntegration.SccProvider
{
	//=========================================================================
	class Misc
	{
		//-------------------------------------------------------------------------
		public static void Log(string format, params object[] args)
		{
			Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture,
										  format, args));
		}

		//-------------------------------------------------------------------------
		public static void Log(string format, object arg)
		{
			Trace.WriteLine(String.Format(CultureInfo.CurrentUICulture,
										  format, arg));
		}
	}
}
