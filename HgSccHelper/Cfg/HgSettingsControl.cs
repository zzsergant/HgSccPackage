using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HgSccHelper
{
	public partial class HgSettingsControl : UserControl
	{
		//------------------------------------------------------------------
		public HgSettingsControl()
		{
			InitializeComponent();

			checkBoxUseSccBindings.Checked = HgSccOptions.Options.UseSccBindings;
		}

		//------------------------------------------------------------------
		public bool UseSccBindings
		{
			get
			{
				return checkBoxUseSccBindings.Checked;
			}
		}
	}
}
