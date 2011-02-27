//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
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
using System.IO;
using System.Text;

namespace HgSccHelper
{
	//==================================================================
	public class HgListFile : IDisposable
	{
		public string FileName { get; private set; }
		public bool IsEmpty { get; private set; }

		//------------------------------------------------------------------
		public HgListFile(IEnumerable<string> files)
		{
			FileName = Path.GetTempFileName();
			IsEmpty = true;

			using (var stream = new StreamWriter(File.OpenWrite(FileName), Encoding.Default))
			{
				foreach (var file in files)
				{
					stream.WriteLine(file);
					IsEmpty = false;
				}
			}
		}

		//------------------------------------------------------------------
		public void Dispose()
		{
			File.Delete(FileName);
		}
	}
}
