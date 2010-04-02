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
	public partial class TagsWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string TargetRevision { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		DispatcherTimer tag_timer;
		DispatcherTimer rev_timer;

		RevLogChangeDesc RevDesc { get; set; }
		RevLogChangeDesc TagDesc { get; set; }

		Dictionary<string, TagInfo> tag_map;

		public const string CfgPath = @"GUI\TagsWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public TagsWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);
			InitializeComponent();

			// Since WPF combo box does not provide TextChanged event
			// register it from edit text box through combo box template

			comboTag.Loaded += delegate
			{
				TextBox editTextBox = comboTag.Template.FindName("PART_EditableTextBox", comboTag) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboTextChanged;
				}
			};

			comboTag.Unloaded += delegate
			{
				TextBox editTextBox = comboTag.Template.FindName("PART_EditableTextBox", comboTag) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboTextChanged;
				}
			};

			tag_map = new Dictionary<string, TagInfo>();
			UpdateContext = new UpdateContext();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Tags: '{0}'", WorkingDir);

			Hg = new Hg();

			tag_timer = new DispatcherTimer();
			tag_timer.Interval = TimeSpan.FromMilliseconds(200);
			tag_timer.Tick += OnTagTimerTick;

			rev_timer = new DispatcherTimer();
			rev_timer.Interval = TimeSpan.FromMilliseconds(200);
			rev_timer.Tick += OnRevTimerTick;

			string target_rev = TargetRevision;
			if (string.IsNullOrEmpty(target_rev))
			{
				var current_revision = Hg.Identify(WorkingDir);
				if (current_revision == null)
				{
					// error
					Close();
					return;
				}

				target_rev = current_revision.Rev.ToString();
			}

			textRev.Text = target_rev;

			UpdateTags();

			RefreshRev();
			if (RevDesc != null)
			{
				foreach (var tag in RevDesc.Tags)
				{
					if (tag != "tip")
					{
						// Selecting target revision tag in combo box
						for(int i = 0; i < comboTag.Items.Count; ++i)
						{
							var item = (TagsComboItem)comboTag.Items[i];
							if (item.Name == tag)
							{
								comboTag.SelectedIndex = i;
								break;
							}
						}
						break;
					}
				}
			}

			RefreshTag();
			comboTag.Focus();
		}

		//------------------------------------------------------------------
		private void UpdateTags()
		{
			var current_tag = comboTag.Text;

			tag_map.Clear();
			comboTag.Items.Clear();

			var tags = Hg.Tags(WorkingDir);
			int counter = 0;

			foreach (var tag in tags)
			{
				if (tag.Name != "tip")
				{
					var item = new TagsComboItem();
					item.GroupText = "Tag";
					item.Name = tag.Name;
					item.Rev = tag.Rev;
					item.SHA1 = tag.SHA1.ShortSHA1();
					item.Misc = tag.IsLocal ? "Local" : "";

					comboTag.Items.Add(item);
					tag_map[tag.Name] = tag;

					if (tag.Name == current_tag)
						comboTag.SelectedIndex = counter;

					counter++;
				}
			}
		}

		//------------------------------------------------------------------
		private void RefreshTag()
		{
			var tag_name = comboTag.Text;

			TagDesc = null;

			if (comboTag.SelectedItem != null)
			{
				var item = (TagsComboItem)comboTag.SelectedItem;
				if (item.Name == tag_name)
					TagDesc = Hg.GetRevisionDesc(WorkingDir, item.SHA1);
			}

			textTagDesc.Text = TagDesc.GetDescription();

			bool is_tip = (tag_name == "tip");
			btnAdd.IsEnabled = (!String.IsNullOrEmpty(tag_name)	&& !is_tip);
			btnRemove.IsEnabled = (TagDesc != null && !is_tip);
		}

		//------------------------------------------------------------------
		private void RefreshRev()
		{
			RevDesc = Hg.GetRevisionDesc(WorkingDir, textRev.Text);
			textRevDesc.Text = RevDesc.GetDescription();

			var tag_name = comboTag.Text;
			bool is_tip = (tag_name == "tip");

			btnAdd.IsEnabled = (!String.IsNullOrEmpty(tag_name) && !is_tip);
		}

		//------------------------------------------------------------------
		private void OnTagTimerTick(object o, EventArgs e)
		{
			tag_timer.Stop();
			RefreshTag();
		}

		//------------------------------------------------------------------
		private void OnRevTimerTick(object o, EventArgs e)
		{
			rev_timer.Stop();
			RefreshRev();
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			tag_timer.Stop();
			tag_timer.Tick -= OnTagTimerTick;

			rev_timer.Stop();
			rev_timer.Tick -= OnRevTimerTick;
		}

		//------------------------------------------------------------------
		private void btnAdd_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(comboTag.Text))
			{
				MessageBox.Show("Invalid tag name", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (RevDesc == null)
			{
				MessageBox.Show("Invalid revision", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			bool replace_tag = checkReplace.IsChecked == true;

			var tag_name = comboTag.Text;

			if (	tag_map.ContainsKey(tag_name)
				&& !replace_tag
				)
			{
				var msg = String.Format("A tag named '{0}' allready exists.\nAre you sure to replace it ?", tag_name);
				var result = MessageBox.Show(msg, "Question", MessageBoxButton.OKCancel, MessageBoxImage.Question);
				
				if (result != MessageBoxResult.OK)
					return;

				replace_tag = true;
			}

			var options = HgTagOptions.None;
			if (replace_tag)
				options |= HgTagOptions.Force;

			var commit_message = "";

			if (checkLocal.IsChecked == true)
			{
				options = HgTagOptions.Local;
			}
			else
			{
				if (checkCustomMessage.IsChecked == true)
					commit_message = textCommitMessage.Text;
			}

			if (!Hg.AddTag(WorkingDir, tag_name, RevDesc.SHA1, options, commit_message))
			{
				var msg = String.Format("An error occured while adding tag '{0}'", tag_name);
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			else
			{
				var msg = String.Format("Tag '{0}' has been added", tag_name);
				MessageBox.Show(msg, "Information", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

				UpdateContext.IsTagsChanged = true;
				if ((options & HgTagOptions.Local) != HgTagOptions.Local)
				{
					UpdateContext.IsParentChanged = true;
					UpdateContext.IsBranchChanged = true;
				}

				UpdateTags();
				RefreshTag();
				RefreshRev();
			}
		}

		//------------------------------------------------------------------
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void OnComboTextChanged(object sender, TextChangedEventArgs e)
		{
			tag_timer.Start();
			btnAdd.IsEnabled = false;
			btnRemove.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void btnRemove_Click(object sender, RoutedEventArgs e)
		{
			if (TagDesc == null)
			{
				MessageBox.Show("Invalid tag name", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var tag_name = comboTag.Text;
			var options = HgTagOptions.None;
			
			var tag_info = tag_map[tag_name];
			if (tag_info.IsLocal)
				options |= HgTagOptions.Local;

			if (!Hg.RemoveTag(WorkingDir, tag_name, options))
			{
				var msg = String.Format("An error occured while removing tag '{0}'", tag_name);
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			else
			{
				var msg = String.Format("Tag '{0}' has been removed", tag_name);
				MessageBox.Show(msg, "Information", MessageBoxButton.OK, MessageBoxImage.Information);

				UpdateContext.IsTagsChanged = true;
				if ((options & HgTagOptions.Local) != HgTagOptions.Local)
				{
					UpdateContext.IsParentChanged = true;
					UpdateContext.IsBranchChanged = true;
				}

				UpdateTags();
				RefreshTag();
				RefreshRev();
			}
		}

		//------------------------------------------------------------------
		private void targetRev_TextChanged(object sender, TextChangedEventArgs e)
		{
			rev_timer.Start();
			btnAdd.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}

	//------------------------------------------------------------------
	class TagsComboItem
	{
		public string GroupText { get; set; }
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public string Misc { get; set; }
	}
}
