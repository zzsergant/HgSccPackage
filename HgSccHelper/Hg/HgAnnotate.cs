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
using System.Linq;
using System.Text;
using HgSccHelper;
using System.Diagnostics;
using System.IO;

//==================================================================
namespace HgSccHelper
{
	//==================================================================
	class HgAnnotate
	{
		//-----------------------------------------------------------------------------
		public List<AnnotateLineInfo> Annotate(string work_dir, string rev, string file)
		{
			var args = new StringBuilder();
			args.Append("annotate");
			args.Append(" -fn");

			if (rev.Length > 0)
				args.Append(" --rev " + rev);

			args.Append(" " + file);

			var lines = new List<AnnotateLineInfo>();

			var space_sep = new[] { ' ' };
			var hg = new Hg();
			using (Process proc = Process.Start(hg.PrepareProcess(work_dir, args.ToString())))
			{
				var stream = proc.StandardOutput;
				bool is_first_line = true;

				while (true)
				{
					var str = stream.ReadLine();
					if (str == null)
						break;

					if (is_first_line)
					{
						if (str.EndsWith("binary file"))
							throw new HgAnnotateBinaryException();

						is_first_line = false;
					}

					str = str.TrimStart(space_sep);

					int rev_end = str.IndexOf(' ');
					int revision = 0;
					if (!int.TryParse(str.Substring(0, rev_end), out revision))
						throw new ApplicationException("Unable to parse a revision");

					var line_info = new AnnotateLineInfo();
					line_info.Rev = revision;

					str = str.Substring(rev_end).TrimStart(space_sep);

					int file_end = str.IndexOf(':');
					line_info.Path = str.Substring(0, file_end);

					lines.Add(line_info);
				}

				proc.WaitForExit();
				if (proc.ExitCode != 0)
					return new List<AnnotateLineInfo>();
			}

			if (lines.Count > 0)
			{
				// retreive lines with respect for BOM
				string temp1 = Path.GetTempFileName();
				try
				{
					if (!hg.CheckoutFile(work_dir, file, rev, temp1))
					{
						return new List<AnnotateLineInfo>();
					}

					using (var stream = new StreamReader(File.OpenRead(temp1), System.Text.Encoding.Default))
					{
						int counter = 0;
						while (true)
						{
							var str = stream.ReadLine();
							if (str == null)
								break;

							if (counter >= lines.Count)
								return new List<AnnotateLineInfo>();

							lines[counter].Source = str;
							lines[counter].Line = ++counter;
						}
					}
				}
				finally
				{
					File.Delete(temp1);
				}
			}

			return lines;
		}
	}

	//==================================================================
	public class AnnotateLineInfo
	{
		public int Rev { get; set; }
		public int Line { get; set; }
		public string Source { get; set; }
		public string Path { get; set; }
	}

	//=============================================================================
	public class HgAnnotateBinaryException : Exception
	{
		//-----------------------------------------------------------------------------
		public HgAnnotateBinaryException()
		{

		}

		//-----------------------------------------------------------------------------
		public HgAnnotateBinaryException(string message)
			: base(message)
		{

		}
	}
}
