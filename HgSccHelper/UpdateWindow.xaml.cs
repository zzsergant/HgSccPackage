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

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for UpdateWindow.xaml
	/// </summary>
	public partial class UpdateWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		//------------------------------------------------------------------
		public UpdateWindow()
		{
			InitializeComponent();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Hg = new Hg();

			IdentifyInfo info = Hg.Identify(WorkingDir);
			if (info == null)
			{
				// error
				Close();
				return;
			}

			var id = new UpdateComboItem();
			id.GroupText = "Rev";
			id.Rev = info.Rev;
			id.Name = info.Rev.ToString();
			id.SHA1 = info.SHA1;
			id.Misc = info.HaveUncommitedChanges ? "Have uncommited changes" : "";

			comboRevision.Items.Add(id);
			comboRevision.SelectedIndex = 0;
			comboRevision.Focus();

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
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void btnOK_Update(object sender, RoutedEventArgs e)
		{
			var revision = comboRevision.Text;
			var msg = "Update to: " + revision;
			if (comboRevision.SelectedItem != null)
			{
				var item = (UpdateComboItem)comboRevision.SelectedItem;
				msg += "\nSelected Revision: " + item.Rev;
			}
			MessageBox.Show(msg);
		}

		//------------------------------------------------------------------
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
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
