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
using System.Windows;

//==================================================================
namespace HgSccHelper
{
	//==================================================================
	public class CfgWindowPosition : IDisposable
	{
		Window wnd;
		string cfg_path;

		//------------------------------------------------------------------
		public CfgWindowPosition(string cfg_path, Window wnd)
		{
			this.cfg_path = cfg_path;
			this.wnd = wnd;

			wnd.Loaded += wnd_Loaded;
			wnd.Initialized += wnd_Initialized;
			wnd.Closing += wnd_Closing;
		}

		//------------------------------------------------------------------
		void wnd_Closing(object sender, EventArgs e)
		{
			var bounds = wnd.RestoreBounds;
			Cfg.Set(cfg_path, "X", (int)bounds.Left);
			Cfg.Set(cfg_path, "Y", (int)bounds.Top);
			Cfg.Set(cfg_path, "Width", (int)bounds.Width);
			Cfg.Set(cfg_path, "Height", (int)bounds.Height);
			Cfg.Set(cfg_path, "IsMaximized", wnd.WindowState == WindowState.Maximized ? 1 : 0);
		}

		//------------------------------------------------------------------
		void wnd_Initialized(object sender, EventArgs e)
		{
			int x;
			int y;
			int width;
			int height;

			if (	Cfg.Get(cfg_path, "Width", out width, (int)wnd.Width)
				&&	Cfg.Get(cfg_path, "Height", out height, (int)wnd.Height)
				&&	Cfg.Get(cfg_path, "X", out x, (int)wnd.Left)
				&&	Cfg.Get(cfg_path, "Y", out y, (int)wnd.Top)
				)
			{
				wnd.Left = x;
				wnd.Top = y;
				wnd.Width = width;
				wnd.Height = height;
				wnd.WindowStartupLocation = WindowStartupLocation.Manual;
			}
		}

		//------------------------------------------------------------------
		void wnd_Loaded(object sender, RoutedEventArgs e)
		{
			int is_maximized;

			if (Cfg.Get(cfg_path, "IsMaximized", out is_maximized, 0))
			{
				if (is_maximized == 1)
					wnd.WindowState = WindowState.Maximized;
			}
		}

		//------------------------------------------------------------------
		public void Dispose()
		{
			if (wnd != null)
			{
				wnd.Loaded -= wnd_Loaded;
				wnd.Initialized -= wnd_Initialized;
				wnd.Closed -= wnd_Closing;

				wnd = null;
			}
		}
	}
}
