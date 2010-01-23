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
		public RevLogWindow()
		{
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
		public bool IsUpdated { get { return revLogControl1.IsUpdated; } }

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("ChangeLog: '{0}'", WorkingDir);
			revLogControl1.WorkingDir = WorkingDir;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}
}
