//=========================================================================
// Copyright 2015 Sergey Antonov <sergant_@mail.ru>
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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Controls;

//-----------------------------------------------------------------------------
namespace HgSccHelper.UI
{
	//-----------------------------------------------------------------------------
	public sealed class ThemeManager
	{
		List<Theme> themes;
		ResourceDictionary base_dict;
		Theme current;
		HashSet<FrameworkElement> controls;

		static readonly ThemeManager instance = new ThemeManager();

		//-----------------------------------------------------------------------------
		static ThemeManager()
		{
		}

		//-----------------------------------------------------------------------------
		private ThemeManager()
		{
			// FIXME: implicit load of assembly
			var tab_layout = FirstFloor.ModernUI.Windows.Controls.TabLayout.List;
			Logger.WriteLine(tab_layout.ToString());

			base_dict = new ResourceDictionary { Source = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.xaml", UriKind.Relative) };

			controls = new HashSet<FrameworkElement>();

			themes = new List<Theme>();
			themes.Add(new Theme
			{
				Name = "Light",
				ResourceDictionary = new ResourceDictionary
				{
					Source = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.Light.xaml", UriKind.Relative)
				},
				AccentColor = Color.FromRgb(27, 191, 235)
			});

			themes.Add(new Theme
			{
				Name = "Dark",
				ResourceDictionary = new ResourceDictionary
				{
					Source = new Uri("/FirstFloor.ModernUI;component/Assets/ModernUI.Dark.xaml", UriKind.Relative)
				},
				AccentColor = Color.FromRgb(27, 121, 175)
			});

			Current = themes[0];
		}

		//-----------------------------------------------------------------------------
		public static ThemeManager Instance
		{
			get
			{
				return instance;
			}
		}

		//-----------------------------------------------------------------------------
		public ResourceDictionary BaseDictionary { get { return base_dict; } }

		//-----------------------------------------------------------------------------
		public Theme Current
		{
			get { return current; }
			set
			{
				if (current == value)
					return;

				current = value;

				foreach (var c in controls)
					ApplyTheme(c, current);
			}
		}

		//---------------------------------------------------------------------
		public IEnumerable<Theme> Themes
		{
			get { return themes; }
		}

		//---------------------------------------------------------------------
		private bool HaveModernUIBase(FrameworkElement control)
		{
			var r = GetAccentDictionary(control);
			return r != null;
		}

		//---------------------------------------------------------------------
		private ResourceDictionary GetAccentDictionary(FrameworkElement control)
		{
			var r = (from dict in control.Resources.MergedDictionaries
					 where dict.Contains("Accent")
					 select dict).FirstOrDefault<ResourceDictionary>();
			return r;
		}

		//---------------------------------------------------------------------
		private ResourceDictionary GetCurrentThemeDictionary(FrameworkElement control)
		{
			var r = (from dict in control.Resources.MergedDictionaries
					 where dict.Contains("WindowBackground")
					 select dict).FirstOrDefault<ResourceDictionary>();
			return r;
		}

		//-----------------------------------------------------------------------------
		private void ApplyAccentColor(FrameworkElement control, Color c)
		{
			var r = GetAccentDictionary(control);
			if (r == null)
				return;

			r["AccentColor"] = c;
			r["Accent"] = new SolidColorBrush(c);
		}

		//-----------------------------------------------------------------------------
		private void ApplyTheme(FrameworkElement control, Theme theme)
		{
			var r = GetCurrentThemeDictionary(control);
			if (r != null)
				control.Resources.MergedDictionaries.Remove(r);

			ApplyAccentColor(control, theme.AccentColor);
			control.Resources.MergedDictionaries.Add(theme.ResourceDictionary);

			// Set background for windows
			var wnd = control as Window;
			if (wnd != null)
			{
				wnd.SetResourceReference(Window.BackgroundProperty, "WindowBackground");
				wnd.SetResourceReference(Window.ForegroundProperty, "WindowText");

				return;
			}
		}

		//------------------------------------------------------------------
		private void wnd_Closing(object sender, EventArgs e)
		{
			var wnd = sender as Window;
			if (wnd == null)
				return;

			wnd.Closing -= wnd_Closing;
			Unsubscribe(wnd);
		}

		//-----------------------------------------------------------------------------
		public void Subscribe(FrameworkElement control)
		{
			if (!HaveModernUIBase(control))
				control.Resources.MergedDictionaries.Add(BaseDictionary);

			ApplyTheme(control, Current);

			controls.Add(control);

			var wnd = control as Window;
			if (wnd != null)
			{
				wnd.Closing += wnd_Closing;
			}

			Logger.WriteLine("Subscribe: Count = {0}", controls.Count);
		}

		//-----------------------------------------------------------------------------
		public void Unsubscribe(FrameworkElement control)
		{
			controls.Remove(control);

			Logger.WriteLine("Unsubscribe: Count = {0}", controls.Count);
		}
	}

	//-----------------------------------------------------------------------------
	public class Theme
	{
		public string Name { get; set; }
		public ResourceDictionary ResourceDictionary { get; set; }
		public Color AccentColor { get; set; } 
	}
}
