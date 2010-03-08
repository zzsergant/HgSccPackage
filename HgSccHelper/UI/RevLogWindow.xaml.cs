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

using System.Windows;
using System.Windows.Input;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for RevLogWindow.xaml
	/// </summary>
	public partial class RevLogWindow : Window
	{
		public const string CfgPath = @"GUI\RevLogWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public RevLogWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();


			// FIXME: This hack inform wpf to render DateTime using local culture info
/*
			FrameworkElement.LanguageProperty.OverrideMetadata(
				typeof(FrameworkElement),
				new FrameworkPropertyMetadata(
					XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
*/
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get { return revLogControl1.UpdateContext; } }

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("ChangeLog: '{0}'", WorkingDir);
			revLogControl1.WorkingDir = WorkingDir;

			revLogControl1.GraphViewGrid.LoadCfg(RevLogWindow.CfgPath, "GraphViewGrid");
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			revLogControl1.GraphViewGrid.SaveCfg(RevLogWindow.CfgPath, "GraphViewGrid");
		}
	}
}
