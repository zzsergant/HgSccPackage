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
