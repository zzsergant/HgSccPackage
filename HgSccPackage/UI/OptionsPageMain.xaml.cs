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

using System;
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
		}

		//-----------------------------------------------------------------------------
		public string PageName
		{
			get { return "Main"; }
		}

		//-----------------------------------------------------------------------------
		public void Init()
		{
			checkUseSccBindings.IsChecked = HgSccOptions.Instance.UseSccBindings;
			checkProjectsForRepository.IsChecked = HgSccOptions.Instance.CheckProjectsForMercurialRepository;
		}

		//-----------------------------------------------------------------------------
		public bool Save()
		{
			if (	HgSccOptions.Instance.UseSccBindings != checkUseSccBindings.IsChecked
				||	HgSccOptions.Instance.CheckProjectsForMercurialRepository != checkProjectsForRepository.IsChecked
				)
			{
				HgSccOptions.Instance.UseSccBindings = (checkUseSccBindings.IsChecked == true);
				HgSccOptions.Instance.CheckProjectsForMercurialRepository = (checkProjectsForRepository.IsChecked == true);
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
