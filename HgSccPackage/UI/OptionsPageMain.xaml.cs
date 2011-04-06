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
	/// Interaction logic for OptionsPageMain.xaml
	/// </summary>
	public partial class OptionsPageMain : UserControl, IOptionsPage
	{
		//-----------------------------------------------------------------------------
		public OptionsPageMain()
		{
			InitializeComponent();

			checkUseSccBindings.IsChecked = HgSccOptions.Options.UseSccBindings;
			checkProjectsForRepository.IsChecked = HgSccOptions.Options.CheckProjectsForMercurialRepository;
		}

		//-----------------------------------------------------------------------------
		public string PageName
		{
			get { return "Main"; }
		}

		//-----------------------------------------------------------------------------
		public bool Save()
		{
			if (	HgSccOptions.Options.UseSccBindings != checkUseSccBindings.IsChecked
				||	HgSccOptions.Options.CheckProjectsForMercurialRepository != checkProjectsForRepository.IsChecked
				)
			{
				HgSccOptions.Options.UseSccBindings = (checkUseSccBindings.IsChecked == true);
				HgSccOptions.Options.CheckProjectsForMercurialRepository = (checkProjectsForRepository.IsChecked == true);
				HgSccOptions.Save();
			}

			return true;
		}

		//-----------------------------------------------------------------------------
		public ContentControl PageContent
		{
			get { return this; }
		}
	}
}
