using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using HgSccPackage.Tools;

namespace HgSccHelper
{
	//=============================================================================
	public partial class CommitForm : Form
	{
		//-----------------------------------------------------------------------------
		public CommitForm()
		{
			InitializeComponent();

			textBoxComment.DataBindings.Add("Text", this, "Comment");
			btnDiffPrevious.Enabled = false;
		}

		//-----------------------------------------------------------------------------
		public string Comment { get; set; }

		//-----------------------------------------------------------------------------
		internal Hg Hg { set; get; }

		//-----------------------------------------------------------------------------
		internal string WorkingDir { get; set; }

		//-----------------------------------------------------------------------------
		internal List<CommitListItem> GetCheckedItems()
		{
			var items = new List<CommitListItem>();
			foreach(CommitListItem item in checkedListFiles.CheckedItems)
			{
				item.Checked = true;
				items.Add(item);
			}

			return items;
		}

		//-----------------------------------------------------------------------------
		internal void SetItems(List<CommitListItem> items)
		{
			checkedListFiles.SuspendLayout();
			checkedListFiles.Items.Clear();
			foreach (var item in items)
			{
				checkedListFiles.Items.Add(item, item.Checked);
			}
			checkedListFiles.ResumeLayout();
		}

		//-----------------------------------------------------------------------------
		private void btnOk_Click(object sender, EventArgs e)
		{
			if (Comment.Length == 0)
			{
				MessageBox.Show("Empty comments are not allowed", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else
			{
				DialogResult = DialogResult.OK;
				this.Close();
			}
		}

		//-----------------------------------------------------------------------------
		private void btnDiffPrevious_Click(object sender, EventArgs e)
		{
			CommitListItem item = checkedListFiles.SelectedItem as CommitListItem;
			string file = item.FileInfo.File;

			bool is_different;

			try
			{
				if (Hg.Diff(WorkingDir, file, out is_different))
				{
					if (!is_different)
					{
						System.Windows.Forms.MessageBox.Show("File: " + file + " is up to date", "Diff");
					}
				}
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}
		}

		//-----------------------------------------------------------------------------
		private void checkedListFiles_SelectedIndexChanged(object sender, EventArgs e)
		{
			btnDiffPrevious.Enabled = false;

			if (checkedListFiles.SelectedItems.Count != 1)
				return;

			CommitListItem item = checkedListFiles.SelectedItem as CommitListItem;
			switch (item.FileInfo.Status)
			{
				case HgFileStatus.Added:
					{
						if (!String.IsNullOrEmpty(item.FileInfo.CopiedFrom))
							btnDiffPrevious.Enabled = true;
						break;
					}
				case HgFileStatus.Modified:
					{
						btnDiffPrevious.Enabled = true;
						break;
					}
			}
		}

		//------------------------------------------------------------------
		private void checkAll_CheckedChanged(object sender, EventArgs e)
		{
			for (int i = 0; i < checkedListFiles.Items.Count; ++i)
			{
				checkedListFiles.SetItemChecked(i, checkAll.Checked);
			}
		}
	}

	//-----------------------------------------------------------------------------
	internal class CommitListItem
	{
		public bool Checked { get; set; }
		public HgFileInfo FileInfo { get; set; }

		public override string ToString()
		{
			var str = FileInfo.Status.ToString() + ": " + FileInfo.File;

			if (	FileInfo.Status == HgFileStatus.Added
				&&	!String.IsNullOrEmpty(FileInfo.CopiedFrom))
			{
				str = String.Format("{0}, [copied from: {1}]", str, FileInfo.CopiedFrom);
			}

			return str;
		}
	}
}
