﻿//=========================================================================
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
using System.Windows.Forms;
using Microsoft.Win32;

//=============================================================================
namespace HgSccHelper
{
	public class HgPkgOptions
	{
		public string DiffTool { get; set; }

		public HgPkgOptions()
		{
			DiffTool = "";
		}
	}

	public sealed class HgSccOptions
	{
		static readonly HgSccOptions instance = new HgSccOptions();

		HgPkgOptions options;

		// Explicit static constructor to tell C# compiler
		// not to mark type as beforefieldinit
		static HgSccOptions()
		{
// 			instance.options = new HgPkgOptions();
		}

		//-----------------------------------------------------------------------------
		private HgSccOptions()
		{
			var new_options = Load();
			if (new_options != null)
				options = new_options;
			else
				options = new HgPkgOptions();
		}

		//-----------------------------------------------------------------------------
		public static HgPkgOptions Options
		{
			get
			{
				return instance.options;
			}
		}

		private static string RegistryPath
		{
			get
			{
				return "Software\\Zz\\HgSccPackage";
			}
		}

		private static string RegKey_DiffTool
		{
			get
			{
				return "DiffTool";
			}
		}

		//-----------------------------------------------------------------------------
		public static void Save()
		{
			try
			{
				var hg_key = Registry.CurrentUser.CreateSubKey(RegistryPath);
				if (hg_key != null)
				{
					hg_key.SetValue(RegKey_DiffTool, Options.DiffTool, RegistryValueKind.String);
					hg_key.Close();
				}
			}
			catch (System.Exception e)
			{
				System.Windows.Forms.MessageBox.Show(e.Message, "Error in Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		//-----------------------------------------------------------------------------
		private static HgPkgOptions Load()
		{
			try
			{
				HgPkgOptions options = new HgPkgOptions();

				var hg_key = Registry.CurrentUser.OpenSubKey(RegistryPath);
				if (hg_key != null)
				{
					string diff_tool = (string)hg_key.GetValue(RegKey_DiffTool, options.DiffTool);
					if (diff_tool != null)
						options.DiffTool = diff_tool;
					hg_key.Close();
				}

				return options;
			}
			catch (System.Exception e)
			{
				System.Windows.Forms.MessageBox.Show(e.Message, "Error in Load", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}

			return new HgPkgOptions();
		}
	}
}