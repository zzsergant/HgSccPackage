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
using Microsoft.Win32;
using System.IO;

namespace HgSccHelper
{
	//==================================================================
	static class Cfg
	{
		public const string CfgRoot = @"Software\Zz\HgSccPackage";

		//------------------------------------------------------------------
		static Cfg()
		{
		}

		//------------------------------------------------------------------
		static public bool Get(string path, string name, out int value, int default_value)
		{
			object v = GetObject(path, name);
			if (v is int)
			{
				value = (int)v;
				return true;
			}
			
			value = default_value;
			return false;
		}

		//------------------------------------------------------------------
		static public bool Set(string path, string name, int value)
		{
			return SetObject(path, name, value);
		}

		//------------------------------------------------------------------
		static public bool Get(string path, string name, out string value, string default_value)
		{
			string v = GetObject(path, name) as string;
			if (v != null)
			{
				value = v;
				return true;
			}

			value = default_value;
			return false;
		}

		//------------------------------------------------------------------
		static public bool Set(string path, string name, string value)
		{
			return SetObject(path, name, value);
		}

		//------------------------------------------------------------------
		static public void DeleteSection(string path)
		{
			try
			{
				var reg_path = Path.Combine(CfgRoot, path);
				Registry.CurrentUser.DeleteSubKeyTree(reg_path);
			}
			catch (System.Security.SecurityException)
			{
			}
			catch (ArgumentException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
		}

		//------------------------------------------------------------------
		static private object GetObject(string path, string name)
		{
			try
			{
				var reg_path = Path.Combine(CfgRoot, path);
				using (var hg_key = Registry.CurrentUser.OpenSubKey(reg_path, RegistryKeyPermissionCheck.ReadSubTree))
				{
					if (hg_key != null)
					{
						return hg_key.GetValue(name);
					}
				}
			}
			catch (System.Security.SecurityException)
			{
			}
			catch (ArgumentException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			return null;
		}

		//------------------------------------------------------------------
		static private bool SetObject(string path, string name, object value)
		{
			try
			{
				var reg_path = Path.Combine(CfgRoot, path);
				var hg_key = Registry.CurrentUser.CreateSubKey(reg_path);
				if (hg_key != null)
				{
					hg_key.SetValue(name, value);
					return true;
				}
			}
			catch (System.Security.SecurityException)
			{
			}
			catch (ArgumentException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			return false;
		}
	}
}
