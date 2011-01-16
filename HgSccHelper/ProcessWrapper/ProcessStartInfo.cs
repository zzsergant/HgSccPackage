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
using System.Collections.Specialized;
using System.Collections;
using System.Text;

namespace ProcessWrapper
{
	//=============================================================================
	public sealed class ProcessStartInfo
	{
		public string FileName { get; set; }
		public string Arguments { get; set; }
		public string WorkingDirectory { get; set; }

		public bool CreateNoWindow { get; set; }
		public bool CreateSuspended { get; set; }

		public bool RedirectStandardInput { get; set; }
		public bool RedirectStandardOutput { get; set; }
		public bool RedirectStandardError { get; set; }
		public Encoding StandardOutputEncoding { get; set; }
		public Encoding StandardErrorEncoding { get; set; }

		private StringDictionary environment_variables;

		//-----------------------------------------------------------------------------
		public ProcessStartInfo()
		{
			FileName = "";
			Arguments = "";
			WorkingDirectory = "";
		}

		//-----------------------------------------------------------------------------
		public ProcessStartInfo(string file_name, string arguments)
			: this()
		{
			FileName = file_name;
			Arguments = arguments;
		}

		//-----------------------------------------------------------------------------
		public StringDictionary EnvironmentVariables
		{
			get
			{
				if (environment_variables == null)
				{
					environment_variables = new StringDictionary();

					foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
						environment_variables.Add((string)entry.Key, (string)entry.Value);
				}

				return environment_variables;
			}
		}

		//-----------------------------------------------------------------------------
		public bool HaveEnvironmentVariables
		{
			get { return environment_variables != null; }
		}
	}
}
