//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HgSccHelper.CommandServer;

namespace HgSccHelper.UI
{
	//==================================================================
	public partial class RebaseWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string DestinationRevision { get; set; }

		//------------------------------------------------------------------
		public string SourceRevision { get; set; }

		//------------------------------------------------------------------
		public UpdateContextCache UpdateContextCache { get; set; }

		//------------------------------------------------------------------
		HgClient HgClient { get { return UpdateContextCache.HgClient; }}

		DispatcherTimer timer;
		RevLogChangeDesc Destination { get; set; }
		RevLogChangeDesc Source { get; set; }

		RebaseRefreshFlags refresh_flags;

		public const string CfgPath = @"GUI\RebaseWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public RebaseWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);
			InitializeComponent();

			UpdateContextCache = new UpdateContextCache();

			// Since WPF combo box does not provide TextChanged event
			// register it from edit text box through combo box template

			comboDestRevision.Loaded += delegate
			{
				TextBox editTextBox = comboDestRevision.Template.FindName("PART_EditableTextBox", comboDestRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboDestinationTextChanged;
				}
			};

			comboDestRevision.Unloaded += delegate
			{
				TextBox editTextBox = comboDestRevision.Template.FindName("PART_EditableTextBox", comboDestRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboDestinationTextChanged;
				}
			};

			comboSourceRevision.Loaded += delegate
			{
				TextBox editTextBox = comboSourceRevision.Template.FindName("PART_EditableTextBox", comboSourceRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboSourceTextChanged;
				}
			};

			comboSourceRevision.Unloaded += delegate
			{
				TextBox editTextBox = comboSourceRevision.Template.FindName("PART_EditableTextBox", comboSourceRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboSourceTextChanged;
				}
			};
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Rebase: '{0}'", WorkingDir);

			if (!HgExtensionsCache.Instance.IsExtensionEnabled(HgExtension.Rebase))
			{
				var msg = "In order to use rebase you have to enable rebase extension in you Mercurial.ini configuration file";
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				Close();
				return;
			}

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(200);
			timer.Tick += OnTimerTick;

			var bookmarks = HgClient.Bookmarks();
			foreach (var bookmark in bookmarks)
			{
				var item = new RebaseComboItem();
				item.GroupText = "Bookmark";
				item.Name = bookmark.Name;
				item.Rev = bookmark.Rev;
				item.SHA1 = bookmark.SHA1;
				item.Misc = bookmark.IsCurrent ? "Current" : "";

				comboDestRevision.Items.Add(item);
				comboSourceRevision.Items.Add(item);
			}

			var tags = HgClient.Tags();
			foreach (var tag in tags)
			{
				var item = new RebaseComboItem();
				item.GroupText = "Tag";
				item.Name = tag.Name;
				item.Rev = tag.Rev;
				item.SHA1 = tag.SHA1;
				item.Misc = tag.IsLocal ? "Local" : "";

				comboDestRevision.Items.Add(item);
				comboSourceRevision.Items.Add(item);
			}

			var branches = HgClient.Branches(HgBranchesOptions.Closed);
			foreach (var branch in branches)
			{
				var item = new RebaseComboItem();
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

				comboDestRevision.Items.Add(item);
				comboSourceRevision.Items.Add(item);
			}

			if (!String.IsNullOrEmpty(SourceRevision))
				comboSourceRevision.Text = SourceRevision;

			if (!String.IsNullOrEmpty(DestinationRevision))
				comboDestRevision.Text = DestinationRevision;

			comboDestRevision.Focus();

			RefreshSource();
			RefreshDestination();
		}

		//------------------------------------------------------------------
		private void RefreshDestination()
		{
			var revision = comboDestRevision.Text;
			if (comboDestRevision.SelectedItem != null)
			{
				var item = (RebaseComboItem)comboDestRevision.SelectedItem;
				revision = item.SHA1;
			}

			Destination = HgClient.GetRevisionDesc(revision);
			if (Destination == null)
				targetDesc.Text = "Invalid Revision";
			else
				targetDesc.Text = Destination.GetDescription();

			UpdateRebaseButton();
		}

		//-----------------------------------------------------------------------------
		void UpdateRebaseButton()
		{
			btnRebase.IsEnabled = (Destination != null) && (Source != null) && (Source.Rev != Destination.Rev);
		}

		//------------------------------------------------------------------
		private void RefreshSource()
		{
			var revision = comboSourceRevision.Text;
			if (comboSourceRevision.SelectedItem != null)
			{
				var item = (RebaseComboItem)comboSourceRevision.SelectedItem;
				revision = item.SHA1;
			}

			Source = HgClient.GetRevisionDesc(revision);
			if (Source == null)
				sourceDesc.Text = "Invalid Revision";
			else
				sourceDesc.Text = Source.GetDescription();

			UpdateRebaseButton();
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();
			if ((refresh_flags & RebaseRefreshFlags.Destination) == RebaseRefreshFlags.Destination)
				RefreshDestination();

			if ((refresh_flags & RebaseRefreshFlags.Source) == RebaseRefreshFlags.Source)
				RefreshSource();

			refresh_flags = RebaseRefreshFlags.None;
		}

		//------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			timer.Stop();
			timer.Tick -= OnTimerTick;
		}

		//------------------------------------------------------------------
		private void btnRebase_Click(object sender, RoutedEventArgs e)
		{
			if (Destination == null)
			{
				MessageBox.Show("Invalid target revision", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (Source == null)
			{
				MessageBox.Show("Invalid base revision", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var msg = String.Format("Rebase revision {0} on top of {1} ?", Source.Rev, Destination.Rev);
			var dlg_result = MessageBox.Show(msg, "Confirm rebase",
			                                 MessageBoxButton.OKCancel,
			                                 MessageBoxImage.Question);

			if (dlg_result != MessageBoxResult.OK)
				return;

			var hg_rebase = new HgRebase();
			if (!hg_rebase.Rebase(WorkingDir, Source.Rev.ToString(), Destination.Rev.ToString()))
			{
				MessageBox.Show("An error occured while rebase", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			MessageBox.Show("Rebase was successfull", "Information", MessageBoxButton.OK, MessageBoxImage.Information);

			DialogResult = true;
			Close();
		}

		//------------------------------------------------------------------
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void OnComboDestinationTextChanged(object sender, TextChangedEventArgs e)
		{
			refresh_flags |= RebaseRefreshFlags.Destination;

			timer.Start();
			btnRebase.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void OnComboSourceTextChanged(object sender, TextChangedEventArgs e)
		{
			refresh_flags |= RebaseRefreshFlags.Source;

			timer.Start();
			btnRebase.IsEnabled = false;
		}
	}

	//------------------------------------------------------------------
	class RebaseComboItem
	{
		public string GroupText { get; set; }
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public string Misc { get; set; }
	}

	//-----------------------------------------------------------------------------
	[Flags]
	enum RebaseRefreshFlags
	{
		None = 0x00,
		Destination = 0x01,
		Source = 0x02
	}
}
