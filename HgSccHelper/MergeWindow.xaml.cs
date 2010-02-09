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

using System.Windows;
using System.Windows.Input;

namespace HgSccHelper
{
	//==================================================================
	public partial class MergeWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string TargetRevision { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		RevLogChangeDesc Target { get; set; }
		IdentifyInfo CurrentRevision { get; set; }

		//------------------------------------------------------------------
		public MergeWindow()
		{
			InitializeComponent();

			UpdateContext = new UpdateContext();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Hg = new Hg();

			CurrentRevision = Hg.Identify(WorkingDir);
			if (CurrentRevision == null)
			{
				// error
				Close();
				return;
			}

			if (CurrentRevision.Parents.Count == 2)
			{
				MessageBox.Show("There is allready active merge.\nYou should either commit it first or make a clean update",
					"Error", MessageBoxButton.OK, MessageBoxImage.Error);
				Close();
				return;
			}

			currentDesc.Text = Hg.GetRevisionDesc(WorkingDir, CurrentRevision.SHA1).GetDescription();

			Target = Hg.GetRevisionDesc(WorkingDir, TargetRevision);
			if (Target == null)
			{
				// error
				Close();
				return;
			}

			if (CurrentRevision.SHA1 == Target.SHA1)
			{
				MessageBox.Show("You can not merge to current revision", "Error",
					MessageBoxButton.OK, MessageBoxImage.Error);
				Close();
				return;
			}

			targetDesc.Text = Hg.GetRevisionDesc(WorkingDir, Target.SHA1).GetDescription();
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
		}

		//------------------------------------------------------------------
		private void Merge_Click(object sender, RoutedEventArgs e)
		{
			if (Target == null)
			{
				MessageBox.Show("Invalid target revision");
				return;
			}

			bool force_merge = false;

			if (CurrentRevision.HaveUncommitedChanges)
			{
				var result = MessageBox.Show("There are uncommited changed.\nAre you sure to force merge ?",
					"Warning", MessageBoxButton.OKCancel, MessageBoxImage.Question);

				if (result != MessageBoxResult.OK)
					return;

				force_merge = true;
			}

			var options = force_merge ? HgMergeOptions.Force : HgMergeOptions.None;

			if (!Hg.Merge(WorkingDir, Target.SHA1, options))
			{
				// Merge can fail with unresolved conflicts, but parents will change
				UpdateContext.IsParentChanged = true;

				MessageBox.Show("An error occured while merge", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			UpdateContext.IsParentChanged = true;

			DialogResult = true;
			Close();
		}

		//------------------------------------------------------------------
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}
}
