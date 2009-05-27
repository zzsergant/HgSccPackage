using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace HgSccPackage.HgSccHelper
{
	public partial class HgAboutControl : UserControl
	{
		public HgAboutControl()
		{
			InitializeComponent();
		}

		private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			System.Diagnostics.Process.Start("http://www.newsupaplex.pp.ru");
		}
	}
}
