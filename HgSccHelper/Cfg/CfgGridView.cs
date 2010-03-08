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
using System.Windows.Controls;
using System.Windows;
using System.IO;

namespace HgSccHelper
{
	//==================================================================
	public static class CfgGridView
	{
		//------------------------------------------------------------------
		public static void LoadCfg(this GridView grid_view, string wnd_cfg_path, string grid_name)
		{
			var cfg_path = Path.Combine(wnd_cfg_path, grid_name);

			foreach (var column in grid_view.Columns)
			{
				if (!Double.IsNaN(column.Width))
				{
					var header = column.Header.ToString();
					var cfg_name = String.Format("{0}.{1}", header, "Width");
					int width;
					if (Cfg.Get(cfg_path, cfg_name, out width, (int)column.Width))
						column.Width = width;
				}
			}
		}

		//------------------------------------------------------------------
		public static void SaveCfg(this GridView grid_view, string wnd_cfg_path, string grid_name)
		{
			var cfg_path = Path.Combine(wnd_cfg_path, grid_name);

			foreach (var column in grid_view.Columns)
			{
				if (!Double.IsNaN(column.Width))
				{
					var header = column.Header.ToString();
					var cfg_name = String.Format("{0}.{1}", header, "Width");
					Cfg.Set(cfg_path, cfg_name, (int)column.Width);
				}
			}
		}
	}
}
