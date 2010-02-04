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
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

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
		public bool IsUpdated { get; set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		DispatcherTimer timer;
		RevLogChangeDesc Target { get; set; }
		IdentifyInfo CurrentRevision { get; set; }

		//------------------------------------------------------------------
		public UpdateWindow()
		{
			InitializeComponent();

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
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Hg = new Hg();

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(200);
			timer.Tick += OnTimerTick;

			CurrentRevision = Hg.Identify(WorkingDir);
			if (CurrentRevision == null)
			{
				// error
				Close();
				return;
			}

			if (!string.IsNullOrEmpty(TargetRevision))
			{
				var target_dec = Hg.GetRevisionDesc(WorkingDir, TargetRevision);
				if (target_dec == null)
				{
					// error
					Close();
					return;
				}

				var item = new UpdateComboItem();
				item.GroupText = "Rev";
				item.Rev = target_dec.Rev;
				item.Name = target_dec.Rev.ToString();
				item.SHA1 = target_dec.SHA1;
				item.Misc = "Target";

				comboRevision.Items.Add(item);
			}

			for (int i = 0; i < CurrentRevision.Parents.Count; ++i)
			{
				var parent = CurrentRevision.Parents[i];

				var id = new UpdateComboItem();
				id.GroupText = "Rev";
				id.Rev = parent.Rev;
				id.Name = parent.Rev.ToString();
				id.SHA1 = parent.SHA1;

				var misc = new StringBuilder();
				misc.Append("Parent");

				if (CurrentRevision.Parents.Count > 1)
					misc.Append((i + 1).ToString());

				if (CurrentRevision.HaveUncommitedChanges)
					misc.Append(" (Have uncommited changes)");

				id.Misc = misc.ToString();

				comboRevision.Items.Add(id);
			}

			var tags = Hg.Tags(WorkingDir);
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

			var branches = Hg.Branches(WorkingDir);
			foreach (var branch in branches)
			{
				var item = new UpdateComboItem();
				item.GroupText = "Branch";
				item.Name = branch.Name;
				item.Rev = branch.Rev;
				item.SHA1 = branch.SHA1;
				item.Misc = branch.IsActive ? "" : "Not Active";

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

			Target = Hg.GetRevisionDesc(WorkingDir, revision);
			targetDesc.Text = GetTargetDescription();
			btnUpdate.IsEnabled = (Target != null);
		}

		//------------------------------------------------------------------
		public string GetTargetDescription()
		{
			if (Target == null)
				return String.Empty;

			var sha1_short = Target.SHA1.Substring(0, 12);
			var desc = String.Format("Rev:\t{0} ({1})", Target.Rev, sha1_short);

			if (!String.IsNullOrEmpty(Target.Branch))
				desc += String.Format("\nBranch:\t{0}", Target.Branch);

			foreach (var tag in Target.Tags)
			{
				desc += String.Format("\nTag:\t{0}", tag);
			}

			desc += String.Format("\nDesc:\t{0}", Target.OneLineDesc);
			return desc;
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();
			RefreshTarget();
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
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

			if (CurrentRevision.HaveUncommitedChanges
				&& !discard_changes)
			{
				var result = MessageBox.Show("There are uncommited changed.\nAre you sure to discard them ?",
					"Warning", MessageBoxButton.OKCancel, MessageBoxImage.Question);
				
				if (result != MessageBoxResult.OK)
					return;

				discard_changes = true;
			}

			var options = discard_changes ? HgUpdateOptions.Clean : HgUpdateOptions.None;

			if (!Hg.Update(WorkingDir, Target.SHA1, options))
			{
				MessageBox.Show("An error occured while update", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			IsUpdated = true;
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
