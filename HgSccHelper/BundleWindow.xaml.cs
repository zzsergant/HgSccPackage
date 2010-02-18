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
using System.Collections.ObjectModel;
using Microsoft.Win32;

namespace HgSccHelper
{
	//==================================================================
	public partial class BundleWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string TargetRevision { get; set; }

		//------------------------------------------------------------------
		public string BaseRevision { get; set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		DispatcherTimer timer;
		RevLogChangeDesc Target { get; set; }
		RevLogChangeDesc Base { get; set; }

		BundleRefreshFlags refresh_flags;

		//-----------------------------------------------------------------------------
		string BundleDirPart { get; set; }

		//-----------------------------------------------------------------------------
		private string DestinationPath
		{
			get { return (string)this.GetValue(DestinationPathProperty); }
			set { this.SetValue(DestinationPathProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty DestinationPathProperty =
			DependencyProperty.Register("DestinationPath", typeof(string),
			typeof(BundleWindow));

		//------------------------------------------------------------------
		public BundleWindow()
		{
			InitializeComponent();

			// Since WPF combo box does not provide TextChanged event
			// register it from edit text box through combo box template

			comboTargetRevision.Loaded += delegate
			{
				TextBox editTextBox = comboTargetRevision.Template.FindName("PART_EditableTextBox", comboTargetRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboTargetTextChanged;
				}
			};

			comboTargetRevision.Unloaded += delegate
			{
				TextBox editTextBox = comboTargetRevision.Template.FindName("PART_EditableTextBox", comboTargetRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboTargetTextChanged;
				}
			};

			comboBaseRevision.Loaded += delegate
			{
				TextBox editTextBox = comboBaseRevision.Template.FindName("PART_EditableTextBox", comboBaseRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboBaseTextChanged;
				}
			};

			comboBaseRevision.Unloaded += delegate
			{
				TextBox editTextBox = comboBaseRevision.Template.FindName("PART_EditableTextBox", comboBaseRevision) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboBaseTextChanged;
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

			if (!string.IsNullOrEmpty(TargetRevision))
			{
				var target_desc = Hg.GetRevisionDesc(WorkingDir, TargetRevision);
				if (target_desc == null)
				{
					// error
					Close();
					return;
				}

				var item = new BundleComboItem();
				item.GroupText = "Rev";
				item.Rev = target_desc.Rev;
				item.Name = target_desc.Rev.ToString();
				item.SHA1 = target_desc.SHA1;
				item.Misc = "Target";

				comboTargetRevision.Items.Add(item);
			}

			if (!string.IsNullOrEmpty(BaseRevision))
			{
				var base_desc = Hg.GetRevisionDesc(WorkingDir, BaseRevision);
				if (base_desc == null)
				{
					// error
					Close();
					return;
				}

				var item = new BundleComboItem();
				item.GroupText = "Rev";
				item.Rev = base_desc.Rev;
				item.Name = base_desc.Rev.ToString();
				item.SHA1 = base_desc.SHA1;
				item.Misc = "Base";

				comboBaseRevision.Items.Add(item);
			}

			BundleDirPart = WorkingDir;

			var tags = Hg.Tags(WorkingDir);
			foreach (var tag in tags)
			{
				var item = new BundleComboItem();
				item.GroupText = "Tag";
				item.Name = tag.Name;
				item.Rev = tag.Rev;
				item.SHA1 = tag.SHA1;
				item.Misc = tag.IsLocal ? "Local" : "";

				comboTargetRevision.Items.Add(item);
				comboBaseRevision.Items.Add(item);
			}

			var branches = Hg.Branches(WorkingDir, HgBranchesOptions.Closed);
			foreach (var branch in branches)
			{
				var item = new BundleComboItem();
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

				comboTargetRevision.Items.Add(item);
				comboBaseRevision.Items.Add(item);
			}

			comboTargetRevision.SelectedIndex = 0;
			comboTargetRevision.Focus();

			comboBaseRevision.SelectedIndex = 0;

			RefreshTarget();
			RefreshBase();
		}

		//------------------------------------------------------------------
		private void RefreshTarget()
		{
			var revision = comboTargetRevision.Text;
			if (comboTargetRevision.SelectedItem != null)
			{
				var item = (BundleComboItem)comboTargetRevision.SelectedItem;
				revision = item.SHA1;
			}

			Target = Hg.GetRevisionDesc(WorkingDir, revision);
			if (Target == null)
				targetDesc.Text = "Invalid Revision";
			else
				targetDesc.Text = Target.GetDescription();

			UpdateBundleButton();
		}

		//-----------------------------------------------------------------------------
		void UpdateDestinationPath()
		{
			var rev_part = "";
			if (checkBundleAll.IsChecked == true)
			{
				rev_part = "all";
			}
			else
			{
				if (Base != null)
				{
					rev_part = String.Format("base{0}", Base.Rev);
					if (Target != null)
						rev_part += "_";
				}

				if (Target != null)
					rev_part += String.Format("rev{0}", Target.Rev);
			}

			DestinationPath = String.Format("{0}_{1}.hg", BundleDirPart, rev_part);
		}

		//-----------------------------------------------------------------------------
		void UpdateBundleButton()
		{
			if (checkBundleAll.IsChecked == true)
				btnBundle.IsEnabled = true;
			else
				btnBundle.IsEnabled = (Target != null) && (Base != null);

			UpdateDestinationPath();
		}

		//------------------------------------------------------------------
		private void RefreshBase()
		{
			var revision = comboBaseRevision.Text;
			if (comboBaseRevision.SelectedItem != null)
			{
				var item = (BundleComboItem)comboBaseRevision.SelectedItem;
				revision = item.SHA1;
			}

			Base = Hg.GetRevisionDesc(WorkingDir, revision);
			if (Base == null)
				baseDesc.Text = "Invalid Revision";
			else
				baseDesc.Text = Base.GetDescription();

			UpdateBundleButton();
		}

		//------------------------------------------------------------------
		private void OnTimerTick(object o, EventArgs e)
		{
			timer.Stop();
			if ((refresh_flags & BundleRefreshFlags.Target) == BundleRefreshFlags.Target)
				RefreshTarget();

			if ((refresh_flags & BundleRefreshFlags.Base) == BundleRefreshFlags.Base)
				RefreshBase();

			refresh_flags = BundleRefreshFlags.None;
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			timer.Stop();
			timer.Tick -= OnTimerTick;
		}

		//------------------------------------------------------------------
		private void btnBundle_Click(object sender, RoutedEventArgs e)
		{
			if (checkBundleAll.IsChecked == true)
			{
				var hg_bundle = new HgBundle();
				if (!hg_bundle.BundleAll(WorkingDir, DestinationPath.Quote()))
				{
					MessageBox.Show("An error occured while bundle", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}
			else
			{
				if (Target == null)
				{
					MessageBox.Show("Invalid target revision", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				if (Base == null)
				{
					MessageBox.Show("Invalid base revision", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				var hg_bundle = new HgBundle();
				if (!hg_bundle.Bundle(WorkingDir, Base.SHA1, Target.SHA1, DestinationPath.Quote()))
				{
					MessageBox.Show("An error occured while bundle", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}
			}

			MessageBox.Show("Bundle was successfull", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
			Close();
		}

		//------------------------------------------------------------------
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void OnComboTargetTextChanged(object sender, TextChangedEventArgs e)
		{
			refresh_flags |= BundleRefreshFlags.Target;

			timer.Start();
			btnBundle.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void OnComboBaseTextChanged(object sender, TextChangedEventArgs e)
		{
			refresh_flags |= BundleRefreshFlags.Base;

			timer.Start();
			btnBundle.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//-----------------------------------------------------------------------------
		private void Browse_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new SaveFileDialog();
			dlg.AddExtension = true;
			dlg.CheckPathExists = false;
			dlg.CheckFileExists = false;
			dlg.FileName = DestinationPath;
			dlg.Filter = String.Format("{0}|*{1}", "Mercurial bundle files (*.hg)", ".hg");
			dlg.Title = "Browse for Destination Path...";
			dlg.RestoreDirectory = true;

			var result = dlg.ShowDialog(this);
			if (result == true)
			{
				DestinationPath = dlg.FileName;
				BundleDirPart = System.IO.Path.GetDirectoryName(DestinationPath);
				textDestPath.SelectAll();
			}
		}

		//-----------------------------------------------------------------------------
		private void checkBundleAll_Click(object sender, RoutedEventArgs e)
		{
			UpdateBundleButton();
		}
	}

	//------------------------------------------------------------------
	class BundleComboItem
	{
		public string GroupText { get; set; }
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public string Misc { get; set; }
	}

	//-----------------------------------------------------------------------------
	[Flags]
	enum BundleRefreshFlags
	{
		None	= 0x00,
		Target	= 0x01,
		Base	= 0x02
	}
}
