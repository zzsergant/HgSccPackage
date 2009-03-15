using System;
using System.Collections.Generic;
using System.Text;
using HgSccHelper;

namespace System.Runtime.CompilerServices
{
	class ExtensionAttribute : Attribute
	{

	}
}

namespace HgSccPackage.Tools
{
	static class Util
	{
		//-----------------------------------------------------------------------------
		public static string Quote(this string str)
		{
			return "\"" + str + "\"";
		}

		//-----------------------------------------------------------------------------
		public static string UnQuote(this string str)
		{
			if (str.Length >= 2)
			{
				if (str[0] == '\"' && str[str.Length - 1] == '\"')
					return str.Substring(1, str.Length - 2);
			}

			return str;
		}

		//-----------------------------------------------------------------------------
		public static string EscapeQuotes(this string str)
		{
			return str.Replace("\"", "\\\"");
		}

		//-----------------------------------------------------------------------------
		public static string UnEscapeQuotes(this string str)
		{
			return str.Replace("\\\"", "\"");
		}

		//-----------------------------------------------------------------------------
		public static void HandleHgDiffException()
		{
			System.Windows.Forms.MessageBox.Show("You should set the diff tool in plug-in options and retry the operation");
			using (var f = new OptionsForm())
			{
				f.ShowDialog();
			}
		}
	}
}