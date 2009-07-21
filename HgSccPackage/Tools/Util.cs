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
			System.Windows.Forms.MessageBox.Show("You should set the diff tool in Tools->Options->Source Control->Mercurial Options Page and retry the operation",
				"Information", System.Windows.Forms.MessageBoxButtons.OK,
				System.Windows.Forms.MessageBoxIcon.Information);

			using (var f = new OptionsForm())
			{
				f.ShowDialog();
			}
		}
	}
}