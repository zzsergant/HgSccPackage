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
		CfgWindowPositionOptions options;

		//------------------------------------------------------------------
		public CfgWindowPosition(string cfg_path, Window wnd)
			: this(cfg_path, wnd, CfgWindowPositionOptions.PositionAndSize)
		{
		}

		//------------------------------------------------------------------
		public CfgWindowPosition(string cfg_path, Window wnd, CfgWindowPositionOptions options)
		{
			this.cfg_path = cfg_path;
			this.wnd = wnd;
			this.options = options;

			wnd.Loaded += wnd_Loaded;
			wnd.Initialized += wnd_Initialized;
			wnd.Closing += wnd_Closing;
		}

		//------------------------------------------------------------------
		void wnd_Closing(object sender, EventArgs e)
		{
			var bounds = wnd.RestoreBounds;
			if (	Double.IsInfinity(bounds.Left)
				||	Double.IsInfinity(bounds.Top)
				)
			{
				return;
			}

			if (options == CfgWindowPositionOptions.PositionAndSize)
			{
				if (	Double.IsInfinity(bounds.Width)
					||	Double.IsInfinity(bounds.Height)
					)
				{
					return;
				}

				Cfg.Set(cfg_path, "Width", (int)bounds.Width);
				Cfg.Set(cfg_path, "Height", (int)bounds.Height);
				Cfg.Set(cfg_path, "IsMaximized", wnd.WindowState == WindowState.Maximized ? 1 : 0);
			}

			Cfg.Set(cfg_path, "X", (int)bounds.Left);
			Cfg.Set(cfg_path, "Y", (int)bounds.Top);
		}

		//------------------------------------------------------------------
		void wnd_Initialized(object sender, EventArgs e)
		{
			switch (options)
			{
				case CfgWindowPositionOptions.PositionOnly:
					{
						int x;
						int y;

						if (	Cfg.Get(cfg_path, "X", out x, (int)wnd.Left)
							&&	Cfg.Get(cfg_path, "Y", out y, (int)wnd.Top)
							)
						{
							if (	x != int.MinValue
								&&	y != int.MinValue
								)
							{
								wnd.Left = x;
								wnd.Top = y;
								wnd.WindowStartupLocation = WindowStartupLocation.Manual;
							}
						}
						break;
					}
				case CfgWindowPositionOptions.PositionAndSize:
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
							if (	x != int.MinValue
								&&	y != int.MinValue
								&&	width != int.MinValue
								&&	height != int.MinValue)
							{
								wnd.Left = x;
								wnd.Top = y;
								wnd.Width = width;
								wnd.Height = height;
								wnd.WindowStartupLocation = WindowStartupLocation.Manual;
							}
						}
						break;
					}
			}
		}

		//------------------------------------------------------------------
		void wnd_Loaded(object sender, RoutedEventArgs e)
		{
			if (options == CfgWindowPositionOptions.PositionAndSize)
			{
				int is_maximized;

				if (Cfg.Get(cfg_path, "IsMaximized", out is_maximized, 0))
				{
					if (is_maximized == 1)
						wnd.WindowState = WindowState.Maximized;
				}
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

	//------------------------------------------------------------------
	public enum CfgWindowPositionOptions
	{
		PositionAndSize,
		PositionOnly
	}
}
