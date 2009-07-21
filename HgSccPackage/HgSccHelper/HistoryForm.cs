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
using HgSccPackage.Tools;

namespace HgSccHelper
{
	public partial class HistoryForm : Form
	{
		Timer timer;

		List<ChangeDesc> changes;
		List<RenameInfo> renames;

		//-----------------------------------------------------------------------------
		public HistoryForm()
		{
			InitializeComponent();

			timer = new Timer();
			timer.Tick += new EventHandler(timer_Tick);
			timer.Interval = 1;

			changes = new List<ChangeDesc>();
			renames = new List<RenameInfo>();

			columnRev.Width = 60;
			columnDate.Width = 140;
			columnAuthor.Width = 220;
			columnComment.Width = 700;

			columnStatus.Width = 120;
			columnPath.Width = 800;

			btnDiff.Enabled = false;
			btnDiffPrev.Enabled = false;
		}

		//-----------------------------------------------------------------------------
		void timer_Tick(object sender, EventArgs e)
		{
			timer.Stop();

			btnDiffPrev.Enabled = false;

			if (listViewChanges.SelectedItems.Count != 1)
			{
				listViewChangedFiles.Items.Clear();
				return;
			}

			// FIXME:
			var change_idx = listViewChanges.SelectedIndices[0];
			var change_set = changes[change_idx];

			listViewChangedFiles.SuspendLayout();
			listViewChangedFiles.Items.Clear();
			
			foreach (var f in change_set.Files)
			{
				var item = new ListViewItem(new string[] { f.Status.ToString(), f.Path.ToString() });
				listViewChangedFiles.Items.Add(item);
			}

			listViewChangedFiles.ResumeLayout();
		}

		//-----------------------------------------------------------------------------
		private void btnOk_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
			this.Close();
		}

		//-----------------------------------------------------------------------------
		internal string Path { get; set; }

		//-----------------------------------------------------------------------------
		internal Hg Hg { set; get; }

		//-----------------------------------------------------------------------------
		internal string WorkingDir { get; set; }

/*
		//-----------------------------------------------------------------------------
		internal void SetChangesList(List<ChangeDesc> changes_list)
		{
			listViewChanges.SuspendLayout();
			listViewChanges.Items.Clear();

			changes.Clear();

			foreach (var change in changes_list)
			{
				changes.Add(change);

				var item = new ListViewItem(new string[]{ change.Rev.ToString(), change.Date.ToString(), change.Author, change.Desc});
				listViewChanges.Items.Add(item);
			}

			FindRenames();

			if (listViewChanges.Items.Count > 0)
				listViewChanges.SelectedIndices.Add(0);

			listViewChanges.ResumeLayout();
		}
*/

		//-----------------------------------------------------------------------------
		internal void SetChangesList(List<ChangeDesc> changes_list)
		{
			listViewChanges.SuspendLayout();
			listViewChanges.Items.Clear();

			FindRenames(changes_list);

			List<ListViewGroup> groups = new List<ListViewGroup>();

			foreach (var ren in renames)
			{
				var group = new ListViewGroup(ren.Path);
				group.HeaderAlignment = HorizontalAlignment.Left;

				groups.Add(group);
			}

			listViewChanges.ShowGroups = true;
			listViewChanges.Groups.AddRange(groups.ToArray());

			changes.Clear();

			int left_idx = 0;
			int right_idx = 1;

			foreach (var change in changes_list)
			{
				changes.Add(change);

				var item = new ListViewItem(new string[] { change.Rev.ToString(), change.Date.ToString(), change.Author, change.Desc });
				var current_group = groups[left_idx];

				if (right_idx < renames.Count)
				{
					var right = renames[right_idx];
					if (change.Rev <= right.Rev)
					{
						left_idx = right_idx;
						right_idx++;

						current_group = groups[left_idx];
					}
				}
				
				item.Group = current_group;
				listViewChanges.Items.Add(item);
			}


			if (listViewChanges.Items.Count > 0)
				listViewChanges.SelectedIndices.Add(0);

			listViewChanges.ResumeLayout();
		}

		//-----------------------------------------------------------------------------
		private void FindRenames(List<ChangeDesc> changes)
		{
			renames.Clear();
			if (changes.Count == 0)
				return;

			var current = new RenameInfo { Path = this.Path.Replace('\\', '/'), Index = 0, Rev = changes[0].Rev };
			renames.Add(current);

			while (true)
			{
				bool found_mismatch = false;
				int mismatch_index = current.Index;
				string prev_name = null;

				for (int i = current.Index; !found_mismatch && i < changes.Count; ++i)
				{
					var ch = changes[i];
					foreach (var info in ch.FilesAdded)
					{
						if (info.Path == current.Path)
						{
							if (!Hg.TrackRename(WorkingDir, current.Path, ch.Rev.ToString(), out prev_name))
								return;

							found_mismatch = true;
							mismatch_index = i;
							break;
						}
					}
				}

				if (!found_mismatch)
					break;

				bool found = false;

				for(int i = mismatch_index; !found && i < changes.Count - 1; ++i)
				{
					foreach (var f in changes[i].Files)
					{
//						Logger.WriteLine(String.Format("Comparing: {0}, {1}", f.Path, prev_name));

						if (0 == String.Compare(f.Path, prev_name, true))
						{
							found = true;
//							Logger.WriteLine("Equal");

							var prev = new RenameInfo { Path = prev_name, Index = i + 1, Rev = changes[i + 1].Rev };
							renames.Add(prev);
							current = prev;
							break;
						}
					}
				}

				if (!found)
					break;
			}			
		}

		//-----------------------------------------------------------------------------
		private void listViewChanges_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (listViewChanges.SelectedItems.Count == 2)
				btnDiff.Enabled = true;
			else
				btnDiff.Enabled = false;

//			timer_Tick(sender, EventArgs.Empty);
			bool pending = timer.Enabled;
			timer.Stop();

			if (!pending)
			{
				//				var lst = new FileInfo[] { new FileInfo { Status = FileStatus.None, Path = "Please wait..." } };
				// 				listFileInfo.ItemsSource = lst;
				btnDiffPrev.Enabled = false;
				listViewChangedFiles.Items.Clear();
			}
			timer.Start();
		}

		//-----------------------------------------------------------------------------
		private void btnDiff_Click(object sender, EventArgs e)
		{
			int rev1 = changes[listViewChanges.SelectedIndices[0]].Rev;
			int rev2 = changes[listViewChanges.SelectedIndices[1]].Rev;
			if (rev1 > rev2)
			{
				int tmp = rev2;
				rev2 = rev1;
				rev1 = tmp;
			}

			if (renames.Count == 0)
				return;

			string f1 = null;
			string f2 = null;

			foreach (var ren in renames)
			{
//				Logger.WriteLine(String.Format("Path = {0}, Rev = {1}, Index = {2}", ren.Path, ren.Rev, ren.Index));

				if (rev1 <= ren.Rev)
					f1 = ren.Path;

				if (rev2 <= ren.Rev)
					f2 = ren.Path;
			}

			try
			{
				Hg.Diff(WorkingDir, f1, rev1, f2, rev2);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}
		}

		//-----------------------------------------------------------------------------
		private void btnDiffPrev_Click(object sender, EventArgs e)
		{
			var cs = changes[listViewChanges.SelectedIndices[0]];
			var item = listViewChangedFiles.SelectedItems[0];

			string file = item.SubItems[1].Text;

			try
			{
				Hg.Diff(WorkingDir, file, cs.Rev - 1, file, cs.Rev);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}
		}

		//-----------------------------------------------------------------------------
		private void listViewChangedFiles_SelectedIndexChanged(object sender, EventArgs e)
		{
			btnDiffPrev.Enabled = false;

			if (	(listViewChangedFiles.SelectedIndices.Count == 1)
				&&	(listViewChanges.SelectedIndices.Count == 1) )
			{
				var cs = changes[listViewChanges.SelectedIndices[0]];
				var item = listViewChangedFiles.SelectedItems[0];
				
				string status = item.SubItems[0].Text;
				string file = item.SubItems[1].Text;

				HgFileStatus state = (HgFileStatus)Enum.Parse(typeof(HgFileStatus), status);
				if (state == HgFileStatus.Modified)
					btnDiffPrev.Enabled = true;
			}
		}
	}

	//-----------------------------------------------------------------------------
	class RenameInfo
	{
		public string Path { get; set; }
		public int Rev { get; set; }
		public int Index { get; set; }
	}
}
