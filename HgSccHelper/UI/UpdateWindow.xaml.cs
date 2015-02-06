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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using HgSccHelper.CommandServer;

namespace HgSccHelper
{
	//==================================================================
	public partial class UpdateWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string TargetRevision { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		HgClient HgClient { get { return UpdateContext.Cache.HgClient; } }

		DispatcherTimer timer;
		RevLogChangeDesc Target { get; set; }
		ParentsInfo ParentsInfo { get; set; }
		ObservableCollection<string> parents;

		public const string CfgPath = @"GUI\UpdateWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public UpdateWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);

			InitializeComponent();

			HgSccHelper.UI.ThemeManager.Instance.Subscribe(this);

			// Since WPF combo box does not provide TextChanged event
			// register it from edit text box through combo box template

			comboRevision.Loaded += delegate
			{
				TextBox editTextBox = comboRevision.Template.FindName("PART_EditableTextBox", comboRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboTextChanged;
				}
			};

			comboRevision.Unloaded += delegate
			{
				TextBox editTextBox = comboRevision.Template.FindName("PART_EditableTextBox", comboRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboTextChanged;
				}
			};

			parents = new ObservableCollection<string>();
			listParents.ItemsSource = parents;

			UpdateContext = new UpdateContext();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Update: '{0}'", WorkingDir);

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(200);
			timer.Tick += OnTimerTick;

			ParentsInfo = UpdateContext.Cache.ParentsInfo ?? HgClient.Parents();

			if (ParentsInfo == null)
			{
				// error
				Close();
				return;
			}

			foreach (var parent in ParentsInfo.Parents)
				parents.Add(parent.GetDescription());

			if (!string.IsNullOrEmpty(TargetRevision))
			{
				var target_desc = UpdateContext.Cache.TargetRevision;
				if (target_desc == null)
					target_desc = HgClient.GetRevisionDesc(TargetRevision);

				if (target_desc == null)
				{
					// error
					Close();
					return;
				}

				var item = new UpdateComboItem();
				item.GroupText = "Rev";
				item.Rev = target_desc.Rev;
				item.Name = target_desc.Rev.ToString();
				item.SHA1 = target_desc.SHA1;
				item.Misc = "Target";

				comboRevision.Items.Add(item);
			}

			for (int i = 0; i < ParentsInfo.Parents.Count; ++i)
			{
				var parent = ParentsInfo.Parents[i];

				var id = new UpdateComboItem();
				id.GroupText = "Rev";
				id.Rev = parent.Rev;
				id.Name = parent.Rev.ToString();
				id.SHA1 = parent.SHA1;

				var misc = new StringBuilder();
				misc.Append("Parent");

				if (ParentsInfo.Parents.Count > 1)
					misc.Append((i + 1).ToString());

				id.Misc = misc.ToString();

				comboRevision.Items.Add(id);
			}

			var bookmarks = UpdateContext.Cache.Bookmarks;
			if (bookmarks == null)
				bookmarks = HgClient.Bookmarks();

			foreach (var bookmark in bookmarks)
			{
				var item = new UpdateComboItem();
				item.GroupText = "Bookmark";
				item.Name = bookmark.Name;
				item.Rev = bookmark.Rev;
				item.SHA1 = bookmark.SHA1;
				item.Misc = bookmark.IsCurrent ? "Current" : "";

				comboRevision.Items.Add(item);
			}

			var tags = UpdateContext.Cache.Tags;
			if (tags == null)
				tags = HgClient.Tags();

			foreach (var tag in tags)
			{
				var item = new UpdateComboItem();
				item.GroupText = "Tag";
				item.Name = tag.Name;
				item.Rev = tag.Rev;
				item.SHA1 = tag.SHA1;
				item.Misc = tag.IsLocal ? "Local" : "";

				comboRevision.Items.Add(item);
			}

			var branches = UpdateContext.Cache.Branches;
			if (branches == null)
				branches = HgClient.Branches(HgBranchesOptions.Closed);

			foreach (var branch in branches)
			{
				var item = new UpdateComboItem();
				item.GroupText = "Branch";
				item.Name = branch.Name;
				item.Rev = branch.Rev;
				item.SHA1 = branch.SHA1;
				item.Misc = "";
				if (!branch.IsActive)
					item.Misc = "Not Active";
				else
					if (branch.IsClosed)
						item.Misc = "Closed";

				comboRevision.Items.Add(item);
			}

			comboRevision.SelectedIndex = 0;
			comboRevision.Focus();

			RefreshTarget();
		}

		//------------------------------------------------------------------
		private void RefreshTarget()
		{
			var revision = comboRevision.Text;
			if (comboRevision.SelectedItem != null)
			{
				var item = (UpdateComboItem)comboRevision.SelectedItem;
				revision = item.SHA1;
			}

			Target = HgClient.GetRevisionDesc(revision);
			targetDesc.Text = Target.GetDescription();
			btnUpdate.IsEnabled = (Target != null);
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();
			RefreshTarget();
		}

		//------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			timer.Stop();
			timer.Tick -= OnTimerTick;
		}

		//------------------------------------------------------------------
		private void btnOK_Update(object sender, RoutedEventArgs e)
		{
			if (Target == null)
			{
				MessageBox.Show("Invalid revision");
				return;
			}

			bool discard_changes = checkDiscardChanges.IsChecked == true;
			
			// If several bookmarks points to one changeset,
			// then we can not use SHA1 as revision

			var rev = comboRevision.Text;

			if (!discard_changes)
			{
				try
				{
					if (!HgClient.Update(rev, HgUpdateOptions.Check))
					{
						MessageBox.Show("An error occured while update", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}

					UpdateContext.IsParentChanged = true;
					UpdateContext.IsBookmarksChanged = true;
					Close();
					return;
				}
				catch (HgUncommitedChangesException)
				{
					var result = MessageBox.Show("There are uncommited changed.\nAre you sure to discard them ?",
						"Warning", MessageBoxButton.OKCancel, MessageBoxImage.Question);

					if (result != MessageBoxResult.OK)
						return;

					// pass through to clean update
				}
			}

			if (!HgClient.Update(rev, HgUpdateOptions.Clean))
			{
				MessageBox.Show("An error occured while update", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			UpdateContext.IsParentChanged = true;
			UpdateContext.IsBookmarksChanged = true;
			Close();
		}

		//------------------------------------------------------------------
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void OnComboTextChanged(object sender, TextChangedEventArgs e)
		{
			timer.Start();
			btnUpdate.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}
	
	//------------------------------------------------------------------
	class UpdateComboItem
	{
		public string GroupText { get; set; }
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public string Misc { get; set; }
	}
}
