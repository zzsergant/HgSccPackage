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
			checkProjectsForRepository.Checked = HgSccOptions.Options.CheckProjectsForMercurialRepository;
		}

		//------------------------------------------------------------------
		public bool UseSccBindings
		{
			get
			{
				return checkBoxUseSccBindings.Checked;
			}
		}

		//------------------------------------------------------------------
		public bool CheckProjectsForMercurialRepository
		{
			get
			{
				return checkProjectsForRepository.Checked;
			}
		}
	}
}
