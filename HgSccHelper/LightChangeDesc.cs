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
using System.Collections.ObjectModel;

//=============================================================================
namespace HgSccHelper
{

	//-----------------------------------------------------------------------------
	class LightChangeDesc
	{
		public string Author { get; set; }
		public string Desc { get; set; }
		public int Rev { get; set; }
		public DateTime Date { get; set; }

		private static string CutPrefix(string str, string prefix)
		{
			return str.Remove(0, prefix.Length);
		}

		//-----------------------------------------------------------------------------
		public static List<LightChangeDesc> ParseChanges(StreamReader reader)
		{
			var list = new List<LightChangeDesc>();
			LightChangeDesc cs = null;

			while (true)
			{
				string str = reader.ReadLine();
				if (str == null)
					break;

				if (str.StartsWith("==:"))
				{
					if (cs != null)
						list.Add(cs);

					cs = new LightChangeDesc();
					continue;
				}

				if (str.StartsWith("date: "))
				{
					cs.Date = DateTime.Parse(str.Substring("date: ".Length));
					continue;
				}

				if (str.StartsWith("author: "))
				{
					cs.Author = str.Substring("author: ".Length);
					continue;
				}

				if (str.StartsWith("rev: "))
				{
					cs.Rev = Int32.Parse(str.Substring("rev: ".Length));
					continue;
				}

				if (str.StartsWith("desc: "))
				{
					cs.Desc = str.Substring("desc: ".Length);
					continue;
				}
				//--
			}

			if (cs != null)
				list.Add(cs);

			return list;
		}
	}
}
