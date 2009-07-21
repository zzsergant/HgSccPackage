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
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace HgSccPackage.HgSccHelper
{
	public partial class HgDiffOptionsControl : UserControl
	{
		public HgDiffOptionsControl()
		{
			InitializeComponent();

			Activate();
		}

		//-----------------------------------------------------------------------------
		private void radioAutoDetect_CheckedChanged(object sender, EventArgs e)
		{
			if (radioAutoDetect.Checked)
			{
				comboDiffTools.Enabled = true;
			}
			else
			{
				comboDiffTools.Enabled = false;
			}
		}

		//-----------------------------------------------------------------------------
		private void radioCustom_CheckedChanged(object sender, EventArgs e)
		{
			if (radioCustom.Checked)
			{
				btnBrowseCustom.Enabled = true;
			}
			else
			{
				btnBrowseCustom.Enabled = false;
			}
		}

		//-----------------------------------------------------------------------------
		private void btnBrowseCustom_Click(object sender, EventArgs e)
		{
			string diff_tool = textDiffTool.Text;
			if (HgOptionsHelper.BrowseDiffTool(ref diff_tool))
				textDiffTool.Text = diff_tool;
		}

		//------------------------------------------------------------------
		public void Activate()
		{
			radioCustom.Checked = true;

			comboDiffTools.Items.Clear();

			var lst = HgOptionsHelper.DetectDiffTools();
			if (lst.Count == 0)
			{
				radioAutoDetect.Enabled = false;
				comboDiffTools.Enabled = false;

				radioCustom.Checked = true;
			}
			else
			{
				comboDiffTools.Items.AddRange(lst.ToArray());

				// TODO: Select the old
				comboDiffTools.SelectedIndex = 0;
				radioAutoDetect.Checked = true;

				if (HgSccOptions.Options.DiffTool.Length != 0)
				{
					foreach (var item in comboDiffTools.Items)
					{
						if (String.Compare(HgSccOptions.Options.DiffTool, item.ToString(), true) == 0)
						{
							comboDiffTools.SelectedItem = item;
							break;
						}
					}
				}
			}

			if (HgSccOptions.Options.DiffTool.Length != 0)
				textDiffTool.Text = HgSccOptions.Options.DiffTool;
		}

		//------------------------------------------------------------------
		public string DiffToolPath
		{
			get
			{
				if (radioAutoDetect.Checked)
				{
					return comboDiffTools.SelectedItem.ToString();
				}
				else
				{
					return textDiffTool.Text;
				}
			}
		}
	}
}
