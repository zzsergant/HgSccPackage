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

//=============================================================================
namespace HgSccHelper
{
	//=============================================================================
	/// <summary>
	/// Options for diff tools
	/// </summary>
	public sealed class DiffTools
	{
		public string DiffTool { get; set; }
		public string DiffArgs { get; set; }

		static readonly DiffTools instance = new DiffTools();

		// Explicit static constructor to tell C# compiler
		// not to mark type as beforefieldinit
		static DiffTools()
		{
		}

		//-----------------------------------------------------------------------------
		private DiffTools()
		{
			DiffTool = "";
			DiffArgs = "";

			Load();
		}

		//-----------------------------------------------------------------------------
		public static DiffTools Instance
		{
			get
			{
				return instance;
			}
		}

		//-----------------------------------------------------------------------------
		public static void Save()
		{
			if (!String.IsNullOrEmpty(Instance.DiffTool))
				Cfg.Set("", "DiffTool", Instance.DiffTool);

			if (!String.IsNullOrEmpty(Instance.DiffArgs))
				Cfg.Set("", "DiffArgs", Instance.DiffArgs);
		}

		//-----------------------------------------------------------------------------
		private void Load()
		{
			string diff_tool;
			string diff_args;

			if (Cfg.Get("", "DiffTool", out diff_tool, ""))
				DiffTool = diff_tool;

			if (Cfg.Get("", "DiffArgs", out diff_args, ""))
				DiffArgs = diff_args;
		}
	}
}