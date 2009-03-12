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

			radioCustom.Checked = true;

			var lst = DetectDiffTools();
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

		//-----------------------------------------------------------------------------
		private void btnOK_Click(object sender, EventArgs e)
		{
			if (radioAutoDetect.Checked)
			{
				HgSccOptions.Options.DiffTool = comboDiffTools.SelectedItem.ToString();
			}
			else
			{
				if (textDiffTool.Text.Length == 0)
				{
					MessageBox.Show("You should browse for Diff tool", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				if (!File.Exists(textDiffTool.Text))
				{
					MessageBox.Show("File: " + textDiffTool.Text + " is not exist", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				HgSccOptions.Options.DiffTool = textDiffTool.Text;
			}

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

		//-----------------------------------------------------------------------------
		private static List<string> DetectDiffTools()
		{
			var lst = new List<string>();

			string path = string.Empty;

			path = DetectKDiff();
			if (File.Exists(path))
				lst.Add(path);

			path = DetectP4Diff();
			if (File.Exists(path))
				lst.Add(path);

			return lst;
		}

		//-----------------------------------------------------------------------------
		private static string DetectKDiff()
		{
			string path = null;

			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\KDiff3"))
				{
					if (key != null)
					{
						path = (string)key.GetValue("");
						if (path != null)
							path = Path.Combine(path, "KDiff3.exe");
					}
				}
			}
			catch (System.Exception)
			{
			}

			if (path == null)
				path = string.Empty;

			return path;
		}

		//-----------------------------------------------------------------------------
		private static string DetectP4Diff()
		{
			string path = null;

			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\perforce\environment"))
				{
					if (key != null)
					{
						path = (string)key.GetValue("P4INSTROOT");
						if (path != null)
							path = Path.Combine(path, "p4diff.exe");
					}
				}
			}
			catch (System.Exception)
			{
			}

			if (path == null)
				path = string.Empty;

			return path;
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
			using (var f = new OpenFileDialog())
			{
				f.RestoreDirectory = true;
				f.Filter = "Executables|*.exe";
				f.CheckFileExists = true;

				if (textDiffTool.Text.Length != 0)
				{
					if (File.Exists(textDiffTool.Text))
					{
						f.InitialDirectory = Path.GetDirectoryName(textDiffTool.Text);
						f.FileName = Path.GetFileName(textDiffTool.Text);
					}
				}

				var res = f.ShowDialog();
				if (res == DialogResult.OK)
				{
					textDiffTool.Text = f.FileName;
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			System.Diagnostics.Process.Start("http://www.newsupaplex.pp.ru");
		}
	}
}
