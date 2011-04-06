using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
