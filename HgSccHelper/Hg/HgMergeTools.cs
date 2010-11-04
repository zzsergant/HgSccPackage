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
using System.Diagnostics;

namespace HgSccHelper
{
	class HgMergeTools
	{
		Dictionary<string, MergeToolInfo> merge_tools;

		//-----------------------------------------------------------------------------
		public HgMergeTools()
		{
			merge_tools = DiscoverMergeTools();
		}

		//-----------------------------------------------------------------------------
		public List<MergeToolInfo> GetMergeTools()
		{
			return merge_tools.Values.Where(tool => tool.IsExecutableFound).ToList<MergeToolInfo>();
		}

		//-----------------------------------------------------------------------------
		private Dictionary<string, MergeToolInfo> DiscoverMergeTools()
		{
			var hg = new Hg();
			var lines = hg.ShowConfig("");
			var merge_tools_prefix = "merge-tools";

			var separator = new[] { '=' };
			var merge_tools = new Dictionary<string, MergeToolInfo>();

			foreach (var line in lines)
			{
				if (line.StartsWith(merge_tools_prefix))
				{
					var str = line.Substring(merge_tools_prefix.Length + 1);

					var parts = str.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length < 1 || parts.Length > 2)
						continue;

					var left = parts[0].Trim();
					if (parts.Length == 1)
					{
						// Merge tool the same as alias
						var tool = new MergeToolInfo(left);
						merge_tools.FindOrAdd(left, ref tool);
						continue;
					}

					var right = parts[1].Trim();

					var last_point = left.LastIndexOf('.');
					if (last_point == -1)
					{
						// Merge tool without any opts
						var tool = new MergeToolInfo(left);
						merge_tools.FindOrAdd(left, ref tool);
					}
					else
					{
						var tool_alias = left.Substring(0, last_point);
						var tool_opt = left.Substring(last_point + 1);

						MergeToolInfo tool;
						if (!merge_tools.TryGetValue(tool_alias, out tool))
						{
							tool = new MergeToolInfo(tool_alias);
							merge_tools[tool_alias] = tool;
						}

						tool.SetOption(tool_opt, right);
					}

					Trace.WriteLine(str);
				}
			}

			foreach (var tool in merge_tools.Values)
				tool.FindExecutable();

			return merge_tools;
		}

		//-----------------------------------------------------------------------------
		static public string FindTool(MergeToolInfo tool)
		{
			if (tool.RegKey.Length > 0)
			{
				var p = Util.LookupRegistry(tool.RegKey, tool.RegName);
				if (!String.IsNullOrEmpty(p))
				{
					p = Util.FindExe(p + tool.RegAppend);
					if (!String.IsNullOrEmpty(p))
						return p;
				}
			}

			return Util.FindExe(tool.Executable);
		}
	}

	//-----------------------------------------------------------------------------
	class MergeToolInfo
	{
		private Dictionary<string, string> map;

		//-----------------------------------------------------------------------------
		public MergeToolInfo(string alias)
		{
			map = new Dictionary<string, string>();
			Alias = alias;
			ExecutableFilename = string.Empty;
		}

		//-----------------------------------------------------------------------------
		public string Alias { get; private set; }

		//-----------------------------------------------------------------------------
		public bool FindExecutable()
		{
			ExecutableFilename = HgMergeTools.FindTool(this);
			return !String.IsNullOrEmpty(ExecutableFilename);
		}

		//-----------------------------------------------------------------------------
		public string ExecutableFilename { get; private set; }

		//-----------------------------------------------------------------------------
		public bool IsExecutableFound { get { return !String.IsNullOrEmpty(ExecutableFilename); } }

		//-----------------------------------------------------------------------------
		public void SetOption(string key, string value)
		{
			if (!String.IsNullOrEmpty(value))
				map[key] = value;
		}

		//-----------------------------------------------------------------------------
		public bool IsOptionExists(string key)
		{
			return map.ContainsKey(key);
		}

		//-----------------------------------------------------------------------------
		public string GetOption(string key, string default_value)
		{
			string value;
			if (map.TryGetValue(key, out value))
				return value;

			return default_value;
		}

		//-----------------------------------------------------------------------------
		private bool GetBoolOption(string key, bool default_value)
		{
			bool val = default_value;
			bool.TryParse(GetOption(key, default_value.ToString()), out val);
			return val;
		}

		//-----------------------------------------------------------------------------
		private void SetBoolOption(string key, bool value)
		{
			SetOption(key, value.ToString());
		}

		//-----------------------------------------------------------------------------
		public string Executable
		{
			get { return GetOption("executable", Alias); }
			set { SetOption("executable", value); }
		}

		//-----------------------------------------------------------------------------
		public string Args
		{
			get { return GetOption("args", "$local $base $other"); }
			set { SetOption("args", value); }
		}

		//-----------------------------------------------------------------------------
		public string DiffArgs
		{
			get { return GetOption("diffargs", "$parent $child"); }
			set { SetOption("diffargs", value); }
		}

		//-----------------------------------------------------------------------------
		public int Priority
		{
			get
			{
				int priority = 0;
				int.TryParse(GetOption("priority", priority.ToString()), out priority);
				return priority;
			}
			set { SetOption("priority", value.ToString()); }
		}

		//-----------------------------------------------------------------------------
		public bool PreMerge
		{
			get { return GetBoolOption("premerge", true); }
			set { SetBoolOption("premerge", value); }
		}

		//-----------------------------------------------------------------------------
		public bool CheckConflicts
		{
			get { return GetBoolOption("checkconflicts", false); }
			set { SetBoolOption("checkconflicts", value); }
		}

		//-----------------------------------------------------------------------------
		public bool CheckChanged
		{
			get { return GetBoolOption("checkchanged", false); }
			set { SetBoolOption("checkchanged", value); }
		}

		//-----------------------------------------------------------------------------
		public bool FixEol
		{
			get { return GetBoolOption("fixeol", false); }
			set { SetBoolOption("fixeol", value); }
		}

		//-----------------------------------------------------------------------------
		public bool Gui
		{
			get { return GetBoolOption("gui", false); }
			set { SetBoolOption("gui", value); }
		}

		//-----------------------------------------------------------------------------
		public string RegKey
		{
			get { return GetOption("regkey", ""); }
			set { SetOption("regkey", value); }
		}

		//-----------------------------------------------------------------------------
		public string RegName
		{
			get { return GetOption("regname", ""); }
			set { SetOption("regname", value); }
		}

		//-----------------------------------------------------------------------------
		public string RegAppend
		{
			get { return GetOption("regappend", ""); }
			set { SetOption("regappend", value); }
		}
	}
}
