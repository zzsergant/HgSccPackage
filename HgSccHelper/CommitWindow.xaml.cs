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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for CommitWindow.xaml
	/// </summary>
	public partial class CommitWindow : Window
	{
		ObservableCollection<CommitItem> commit_items;
		ObservableCollection<string> parents;

		//-----------------------------------------------------------------------------
		public CommitWindow()
		{
			InitializeComponent();

			commit_items = new ObservableCollection<CommitItem>();
			parents = new ObservableCollection<string>();
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty CommitMessageProperty =
			DependencyProperty.Register("CommitMessage", typeof(string),
			typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		private string CommitMessage
		{
			get { return (string)this.GetValue(CommitMessageProperty); }
			set { this.SetValue(CommitMessageProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public IEnumerable<string> ItemsToCheck { set; get; }

		//-----------------------------------------------------------------------------
		private Hg Hg { get; set; }

		//------------------------------------------------------------------
		private IdentifyInfo CurrentRevision { get; set; }

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsMergeActiveProperty =
			DependencyProperty.Register("IsMergeActive", typeof(bool),
			typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		private bool IsMergeActive
		{
			get { return (bool)this.GetValue(IsMergeActiveProperty); }
			set { this.SetValue(IsMergeActiveProperty, value); }
		}

		//-----------------------------------------------------------------------------
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
				IsMergeActive = true;
				checkAll.Visibility = Visibility.Collapsed;

				var grid_view = (GridView)listFiles.View;
				grid_view.Columns.RemoveAt(0);
			}
			else
			{
				var grid_view = (GridView)listFiles.View;
				grid_view.Columns.RemoveAt(2);
			}

			foreach (var parent in CurrentRevision.Parents)
				parents.Add(Hg.GetRevisionDesc(WorkingDir, parent.SHA1).GetDescription());

			if (!Prepare())
			{
				Close();
				return;
			}

			if (commit_items.Count == 0)
			{
				Close();
				return;
			}

			listFiles.ItemsSource = commit_items;
			parentsList.ItemsSource = parents;
			textCommitMessage.Focus();
		}

		//-----------------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{

		}

		//-----------------------------------------------------------------------------
		private void checkAll_Checked(object sender, RoutedEventArgs e)
		{
			foreach (var item in commit_items)
			{
				item.IsChecked = true;
			}
		}

		//-----------------------------------------------------------------------------
		private void checkAll_Unchecked(object sender, RoutedEventArgs e)
		{
			foreach (var item in commit_items)
			{
				item.IsChecked = false;
			}
		}

		//-----------------------------------------------------------------------------
		private bool Prepare()
		{
			var files_status_dict = new C5.HashDictionary<string, HgFileInfo>();

			foreach (var file_status in Hg.Status(WorkingDir))
			{
				files_status_dict.Add(file_status.File, file_status);
			}

			var dict = new C5.HashDictionary<string, HgFileStatus>();
			if (ItemsToCheck != null)
			{
				foreach (var f in ItemsToCheck)
				{
					string file;
					if (!Util.GetRelativePath(WorkingDir, f, out file))
						return false;

					dict.Add(file.ToLower(), HgFileStatus.NotTracked);
				}
			}

			var commit_removed = new C5.HashDictionary<string, CommitItem>();
			foreach (var tuple in files_status_dict)
			{
				var f = tuple.Value;

				switch (f.Status)
				{
					case HgFileStatus.Added:
					case HgFileStatus.Modified:
					case HgFileStatus.Removed:
						{
							var item = new CommitItem();
							string lower_f = f.File.ToLower();
							item.IsChecked = dict.Contains(lower_f);
							item.FileInfo = f;

							commit_items.Add(item);
							if (f.Status == HgFileStatus.Removed)
								commit_removed.Add(f.File, item);
							break;
						}
				}
			}

			foreach (var f in commit_items)
			{
				if (f.IsChecked
					&& f.FileInfo.Status == HgFileStatus.Added
					&& !String.IsNullOrEmpty(f.FileInfo.CopiedFrom))
				{
					CommitItem item;

					if (commit_removed.Find(f.FileInfo.CopiedFrom, out item))
					{
						Logger.WriteLine("commit_removed: " + item.FileInfo.File);
						item.IsChecked = true;
					}
				}
			}

			return true;

/*
				form.SetItems(commit_files);

				if (form.ShowDialog() == DialogResult.OK)
				{
					var checked_files = form.GetCheckedItems();
					if (checked_files.Count == 0)
						return SccErrors.Ok;

					Logger.WriteLine("Checked: {0}, Commit: {1}, Status: {2}",
						checked_files.Count, commit_files.Count, files_status_dict.Count);

					SccErrors error = SccErrors.UnknownError;

					var to_commit_files = new List<string>();
					foreach (var commit_item in checked_files)
					{
						to_commit_files.Add(commit_item.FileInfo.File);
					}

					if (checked_files.Count == commit_files.Count)
					{
						error = CommitAll(hwnd, form.Comment);
					}
					else if (to_commit_files.Count > 0)
					{
						error = CheckInInternal(hwnd, to_commit_files, form.Comment);
					}
					else
					{
						return SccErrors.Ok;
					}

					if (error == SccErrors.Ok)
					{
						foreach (var f in to_commit_files)
						{
							commited_files_list.Add(Path.GetFullPath(Path.Combine(WorkingDir, f)));
						}
					}

					return error;
				}
				else
				{
					return SccErrors.I_OperationCanceled;
				}
			}
*/
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}

	//==================================================================
	internal class CommitItem : DependencyObject
	{
		//-----------------------------------------------------------------------------
		public bool IsChecked
		{
			get { return (bool)this.GetValue(IsCheckedProperty); }
			set { this.SetValue(IsCheckedProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsCheckedProperty =
			DependencyProperty.Register("IsChecked", typeof(bool),
			typeof(CommitItem));

		public HgFileInfo FileInfo { get; set; }

		//-----------------------------------------------------------------------------
		public string MergeStatus
		{
			get
			{
				return "M";
			}
		}

		//-----------------------------------------------------------------------------
		public string PathViewString
		{
			get
			{
				var str = FileInfo.File;

				if (FileInfo.Status == HgFileStatus.Added
					&& !String.IsNullOrEmpty(FileInfo.CopiedFrom))
				{
					str += String.Format("\n[copied from: {0}]", FileInfo.CopiedFrom);
				}

				return str;
			}
		}
	}
}
