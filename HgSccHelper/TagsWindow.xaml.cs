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
using System.Diagnostics;
using System.Windows.Input;

namespace HgSccHelper
{
	public partial class TagsWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir
		{
			get { return tagsControl1.WorkingDir; }
			set { tagsControl1.WorkingDir = value; }
		}

		//------------------------------------------------------------------
		public string TargetRevision
		{
			get { return tagsControl1.TargetRevision; }
			set { tagsControl1.TargetRevision = value; }
		}

		//------------------------------------------------------------------
		public TagsWindow()
		{
			InitializeComponent();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			tagsControl1.CloseEvent += tagsControl1_CloseEvent;
		}

		//------------------------------------------------------------------
		void tagsControl1_CloseEvent(object sender, System.EventArgs e)
		{
 			Close();
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			tagsControl1.CloseEvent -= tagsControl1_CloseEvent;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}
}
