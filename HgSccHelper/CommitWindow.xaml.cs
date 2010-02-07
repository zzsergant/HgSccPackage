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
		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffParent1Command = new RoutedUICommand("Diff Parent 1",
			"DiffParent1", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffParent2Command = new RoutedUICommand("Diff Parent 2",
			"DiffParent2", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand FileHistoryCommand = new RoutedUICommand("File History",
			"FileHistory", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand MarkResolvedCommand = new RoutedUICommand("Mark Resolved",
			"MarkResolved", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand MarkUnresolvedCommand = new RoutedUICommand("Mark Unresolved",
			"MarkUnresolved", typeof(CommitWindow));

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
		public IEnumerable<string> FilesToCommit { set; get; }

		//------------------------------------------------------------------
		public List<string> CommitedFiles { private set; get; }

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

			if (IsMergeActive)
			{
				textParent1.Text = parents[0];
				textParent2.Text = parents[1];
			}
			else
			{
				parentsGrid.ColumnDefinitions.RemoveAt(1);
				
				// FIXME: It should be textParent1, but
				// after removing column definition from the grid
				// it leaves textParent2 textBox
				textParent2.Text = parents[0];
			}
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
			var resolve_dict = new C5.HashDictionary<string, ResolveStatus>();
			if (IsMergeActive)
			{
				var hg_resolve = new HgResolve();
				var resolve_list = hg_resolve.List(WorkingDir);
				foreach (var file in resolve_list)
					resolve_dict[file.Path.ToLower()] = file.Status;
			}

			var files_status_dict = new C5.HashDictionary<string, HgFileInfo>();

			foreach (var file_status in Hg.Status(WorkingDir))
			{
				files_status_dict.Add(file_status.File, file_status);
			}

			var dict = new C5.HashDictionary<string, HgFileStatus>();
			if (FilesToCommit != null)
			{
				foreach (var f in FilesToCommit)
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
							item.ResolveStatus = ResolveStatus.None;

							if (IsMergeActive)
							{
								var resolve_status = ResolveStatus.None;
								if (resolve_dict.Find(lower_f, out resolve_status))
									item.ResolveStatus = resolve_status;
							}

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

			// Check all items if FilesToCommit is empty
			if (dict.Count == 0)
				checkAll.IsChecked = true;

			return true;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void btnCommit_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(CommitMessage))
			{
				MessageBox.Show("You should specify commit message", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			if (IsMergeActive)
			{
				MessageBox.Show("Commit for merges is not yet supported", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			else
			{
				var checked_items = from item in commit_items
									where item.IsChecked
									select item;

				var checked_list = checked_items.ToList();
				if (checked_list.Count == 0)
				{
					DialogResult = false;
					Close();
				}

				var to_commit_files = new List<string>();
				foreach (var commit_item in checked_list)
				{
					to_commit_files.Add(commit_item.FileInfo.File);
				}

				bool result = false;

				if (checked_list.Count == commit_items.Count)
				{
					result = Hg.CommitAll(WorkingDir, CommitMessage);
				}
				else if (to_commit_files.Count > 0)
				{
					try
					{
						result = Hg.Commit(WorkingDir, to_commit_files, CommitMessage);
					}
					catch (HgCommandLineException)
					{
						MessageBox.Show("Resulted command line for hg.exe is too long. In this situation you can only commit all changed files (which is equivalent to invoking hg.exe without specified files).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}					
				}
				else
				{
					return;
				}

				if (result)
				{
					CommitedFiles = new List<string>();

					foreach (var f in to_commit_files)
					{
						CommitedFiles.Add(System.IO.Path.GetFullPath(
							System.IO.Path.Combine(WorkingDir, f)));
					}

					DialogResult = true;
					Close();
				}
			}
		}
		
		//------------------------------------------------------------------
		private void DiffPrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listFiles != null && listFiles.SelectedItems.Count == 1)
			{
				var item = (CommitItem)listFiles.SelectedItem;
				if (item.FileInfo.Status == HgFileStatus.Added
					&& !String.IsNullOrEmpty(item.FileInfo.CopiedFrom))
				{
					e.CanExecute = true;
				}
				else if (item.FileInfo.Status == HgFileStatus.Modified)
				{
					e.CanExecute = true;
				}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (CommitItem)listFiles.SelectedItem;

			bool is_different = true;

			try
			{
				Hg.Diff(WorkingDir, item.FileInfo.File, out is_different);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}

			if (!is_different)
			{
				MessageBox.Show("File: " + item.FileInfo.File + " is up to date", "Diff",
					MessageBoxButton.OK, MessageBoxImage.Information);
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffParent_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listFiles != null && listFiles.SelectedItems.Count == 1)
			{
				if (IsMergeActive)
				{
					var item = (CommitItem)listFiles.SelectedItem;
					if (item.ResolveStatus != ResolveStatus.None)
						e.CanExecute = true;
				}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffParent1_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (CommitItem)listFiles.SelectedItem;

			bool is_different = true;

			try
			{
				Hg.DiffWithRevision(WorkingDir, item.FileInfo.File,
					CurrentRevision.Parents[0].SHA1, out is_different);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}

			if (!is_different)
			{
				MessageBox.Show("File: " + item.FileInfo.File + " is up to date", "Diff",
					MessageBoxButton.OK, MessageBoxImage.Information);
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffParent2_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (CommitItem)listFiles.SelectedItem;

			bool is_different = true;

			try
			{
				Hg.DiffWithRevision(WorkingDir, item.FileInfo.File,
					CurrentRevision.Parents[1].SHA1, out is_different);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}

			if (!is_different)
			{
				MessageBox.Show("File: " + item.FileInfo.File + " is up to date", "Diff",
					MessageBoxButton.OK, MessageBoxImage.Information);
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void MarkResolved_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listFiles != null && listFiles.SelectedItems.Count == 1)
			{
				if (IsMergeActive)
				{
					var item = (CommitItem)listFiles.SelectedItem;
					if (item.ResolveStatus == ResolveStatus.Unresolved)
						e.CanExecute = true;
				}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void MarkResolved_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (CommitItem)listFiles.SelectedItem;

			var hg_resolve = new HgResolve();
			if (hg_resolve.MarkAsResolved(WorkingDir, new[] { item.FileInfo.File }))
			{
				item.ResolveStatus = ResolveStatus.Resolved;
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void MarkUnresolved_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listFiles != null && listFiles.SelectedItems.Count == 1)
			{
				if (IsMergeActive)
				{
					var item = (CommitItem)listFiles.SelectedItem;
					if (item.ResolveStatus == ResolveStatus.Resolved)
						e.CanExecute = true;
				}
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void MarkUnresolved_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (CommitItem)listFiles.SelectedItem;

			var hg_resolve = new HgResolve();
			if (hg_resolve.MarkAsUnresolved(WorkingDir, new[] { item.FileInfo.File }))
			{
				item.ResolveStatus = ResolveStatus.Unresolved;
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ListFiles_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (DiffPreviousCommand != null)
			{
				if (DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private void FileHistory_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listFiles != null && (listFiles.SelectedItems.Count == 1))
				e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (CommitItem)listFiles.SelectedItem;

			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = CurrentRevision.Rev.ToString();
			wnd.FileName = item.FileInfo.File;
			wnd.Owner = Window.GetWindow(this);

			// TODO: Handle updates from file history dialog
			wnd.ShowDialog();
			// IsUpdated = IsUpdated || wnd.IsUpdated;
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
		public ResolveStatus ResolveStatus
		{
			get { return (ResolveStatus)this.GetValue(ResolveStatusProperty); }
			set { this.SetValue(ResolveStatusProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ResolveStatusProperty =
			DependencyProperty.Register("ResolveStatus", typeof(ResolveStatus),
			typeof(CommitItem));

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
