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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.Win32;

namespace HgSccHelper
{
	//-----------------------------------------------------------------------------
	public partial class OptionsForm : Form
	{
		//-----------------------------------------------------------------------------
		public OptionsForm()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		private void btnOK_Click(object sender, EventArgs e)
		{
			string diff_tool = hgDiffOptionsControl1.DiffToolPath;

			if (diff_tool.Length != 0)
			{
				if (!File.Exists(diff_tool))
				{
					MessageBox.Show("File: " + diff_tool + " is not exist", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
			}

			HgSccOptions.Options.DiffTool = diff_tool;
			HgSccOptions.Options.UseSccBindings = hgSettingsControl1.UseSccBindings;
			HgSccOptions.Options.CheckProjectsForMercurialRepository = hgSettingsControl1.CheckProjectsForMercurialRepository;
			HgSccOptions.Save();

			DialogResult = DialogResult.OK;
			this.Close();
		}

		//-----------------------------------------------------------------------------
		private void btnCancel_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			this.Close();
		}
	}
}
