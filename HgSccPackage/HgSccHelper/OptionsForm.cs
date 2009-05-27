using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using HgSccPackage.HgSccHelper;
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

			if (diff_tool.Length == 0)
			{
				MessageBox.Show("You should browse for Diff tool", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			if (!File.Exists(diff_tool))
			{
				MessageBox.Show("File: " + diff_tool + " is not exist", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			HgSccOptions.Options.DiffTool = diff_tool;
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
