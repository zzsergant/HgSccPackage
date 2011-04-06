//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
// 
// This software may be used and distributed according to the terms of the
// GNU General Public License version 2 as published by the Free Software
// Foundation.
// 
// See the file COPYING.TXT for the full text of the license, or see
// http://www.gnu.org/licenses/gpl-2.0.txt
// 
//=========================================================================

using System.Windows.Controls;
using HgSccHelper;

namespace HgSccPackage.UI
{
	/// <summary>
	/// Interaction logic for OptionsPageAbout.xaml
	/// </summary>
	public partial class OptionsPageAbout : UserControl, IOptionsPage
	{
		//-----------------------------------------------------------------------------
		public OptionsPageAbout()
		{
			InitializeComponent();
		}

		//------------------------------------------------------------------
		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			System.Diagnostics.Process.Start(e.Uri.ToString());
		}

		//-----------------------------------------------------------------------------
		public string PageName
		{
			get { return "About"; }
		}

		//-----------------------------------------------------------------------------
		public bool Save()
		{
			return true;
		}

		//-----------------------------------------------------------------------------
		public ContentControl PageContent
		{
			get { return this; }
		}
	}
}
