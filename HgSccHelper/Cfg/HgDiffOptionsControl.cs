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
using System.Windows.Forms;
using System.IO;

namespace HgSccHelper
{
	//=============================================================================
	public partial class HgDiffOptionsControl : UserControl
	{
		private List<MergeToolInfo> merge_tools;

		public HgDiffOptionsControl()
		{
			InitializeComponent();

			Activate();
		}

		//-----------------------------------------------------------------------------
		public void Activate()
		{
			comboTool.Items.Clear();

			merge_tools = new HgMergeTools().GetMergeTools();
			foreach (var tool in merge_tools)
			{
				comboTool.Items.Add(new DiffComboItem
				                    	{
				                    		DiffTool = tool.ExecutableFilename,
											DiffArgs = tool.DiffArgs
				                    	});
			}

			if (HgSccOptions.Options.DiffTool.Length != 0)
				AddDiffTool(HgSccOptions.Options.DiffTool, HgSccOptions.Options.DiffArgs);

			if (comboTool.SelectedIndex == -1 && comboTool.Items.Count > 0)
				comboTool.SelectedIndex = 0;
		}

		//-----------------------------------------------------------------------------
		private void BtnBrowseClick(object sender, EventArgs e)
		{
			string diff_tool = comboTool.Text;
			if (HgOptionsHelper.BrowseDiffTool(ref diff_tool))
			{
				AddDiffTool(diff_tool, "");
			}
		}

		//-----------------------------------------------------------------------------
		private void AddDiffTool(string diff_tool, string diff_args)
		{
			var new_tool = merge_tools.Find(tool =>
											  String.Compare(tool.ExecutableFilename, diff_tool, true) == 0);

			if (new_tool == null)
			{
				new_tool = new MergeToolInfo(Path.GetFileNameWithoutExtension(diff_tool));
				new_tool.Executable = diff_tool;
				if (!string.IsNullOrEmpty(diff_args))
					new_tool.DiffArgs = diff_args;

				if (!new_tool.FindExecutable())
					return;
			}

			foreach (DiffComboItem item in comboTool.Items)
			{
				if (String.Compare(item.DiffTool, new_tool.ExecutableFilename, true) == 0)
				{
					if (!string.IsNullOrEmpty(diff_args))
						item.DiffArgs = diff_args;
					else
						item.DiffArgs = new_tool.DiffArgs;

					comboTool.SelectedItem = item;
					textArgs.Text = item.DiffArgs;
					return;
				}
			}

			comboTool.Items.Add(new DiffComboItem {DiffTool = new_tool.ExecutableFilename, DiffArgs = new_tool.DiffArgs});
			comboTool.SelectedIndex = comboTool.Items.Count - 1;
		}

		//------------------------------------------------------------------
		public string DiffToolPath
		{
			get { return comboTool.Text; }
		}

		//------------------------------------------------------------------
		public string DiffToolArgs
		{
			get { return textArgs.Text; }
		}

		//-----------------------------------------------------------------------------
		private void ComboToolSelectedIndexChanged(object sender, EventArgs e)
		{
			var item = comboTool.SelectedItem as DiffComboItem;
			if (item != null)
				textArgs.Text = item.DiffArgs;
		}
	}

	//=============================================================================
	internal class DiffComboItem
	{
		//-----------------------------------------------------------------------------
		public string DiffTool { get; set; }
		public string DiffArgs { get; set; }
		
		//-----------------------------------------------------------------------------
		public override string ToString()
		{
			return DiffTool;
		}
	}
}
