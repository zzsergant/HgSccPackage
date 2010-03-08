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
	/// Interaction logic for AnnotateWindow.xaml
	/// </summary>
	public partial class AnnotateWindow : Window
	{
		public const string CfgPath = @"GUI\AnnotateWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public AnnotateWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string FileName { get; set; }

		//------------------------------------------------------------------
		public string Rev { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get { return annotateControl1.UpdateContext; } }

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Annotate: '{0}'", FileName);
			annotateControl1.WorkingDir = WorkingDir;
			annotateControl1.FileName = FileName;
			annotateControl1.Rev = Rev;

			annotateControl1.ListChangesGrid.LoadCfg(AnnotateWindow.CfgPath, "ListChangesGrid");
			annotateControl1.ListLinesGrid.LoadCfg(AnnotateWindow.CfgPath, "ListLinesGrid");
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
			annotateControl1.ListChangesGrid.SaveCfg(AnnotateWindow.CfgPath, "ListChangesGrid");
			annotateControl1.ListLinesGrid.SaveCfg(AnnotateWindow.CfgPath, "ListLinesGrid");
		}
	}
}
