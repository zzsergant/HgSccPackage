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
using HgSccHelper.UI;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for RevertWindow.xaml
	/// </summary>
	public partial class RevertWindow : Window
	{
		ObservableCollection<RevertItem> revert_items;

		GridViewColumnSorter files_sorter;

		public const string CfgPath = @"GUI\RevertWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		private AsyncOperations async_ops;

		//-----------------------------------------------------------------------------
		private Cursor prev_cursor;

		//-----------------------------------------------------------------------------
		public RevertWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			revert_items = new ObservableCollection<RevertItem>();

			VirtualizingStackPanel.SetIsVirtualizing(listFiles, true);
			VirtualizingStackPanel.SetVirtualizationMode(listFiles, VirtualizationMode.Recycling);

			UpdateContext = new UpdateContext();

			files_sorter = new GridViewColumnSorter(listFiles);
			files_sorter.ExcludeColumn(checkColumn);
			
			diffColorizer.Complete = new Action<List<string>>(OnDiffColorizer);
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//-----------------------------------------------------------------------------
		public IEnumerable<string> FilesToRevert { set; get; }

		//------------------------------------------------------------------
		public bool CheckAllFiles { get; set; }

		//------------------------------------------------------------------
		public List<string> RevertedFiles { private set; get; }

		//-----------------------------------------------------------------------------
		private Hg Hg { get; set; }

		//------------------------------------------------------------------
		private IdentifyInfo CurrentRevision { get; set; }

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsAllCheckedProperty =
			DependencyProperty.Register("IsAllChecked", typeof(bool),
			typeof(RevertWindow));

		//------------------------------------------------------------------
		private bool IsAllChecked
		{
			get { return (bool)this.GetValue(IsAllCheckedProperty); }
			set { this.SetValue(IsAllCheckedProperty, value); }
		}

		//-----------------------------------------------------------------------------
		private AsyncOperations RunningOperations
		{
			get { return async_ops; }
			set
			{
				if (async_ops != value)
				{
					if (async_ops == AsyncOperations.None)
					{
						prev_cursor = Cursor;
						Cursor = Cursors.Wait;
					}

					async_ops = value;

					if (async_ops == AsyncOperations.None)
					{
						Cursor = prev_cursor;
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void OnDiffColorizer(List<string> obj)
		{
			RunningOperations &= ~AsyncOperations.Diff;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Revert: '{0}'", WorkingDir);

			listFilesGrid.LoadCfg(RevertWindow.CfgPath, "ListFilesGrid");

			int diff_width;
			Cfg.Get(CfgPath, DiffColorizerControl.DiffWidth, out diff_width, DiffColorizerControl.DefaultWidth);
			diffColorizer.Width = diff_width;

			int diff_visible;
			Cfg.Get(CfgPath, DiffColorizerControl.DiffVisible, out diff_visible, 1);
			expanderDiff.IsExpanded = (diff_visible != 0);

			Hg = new Hg();

			CurrentRevision = Hg.Identify(WorkingDir);
			if (CurrentRevision == null)
			{
				// error
				Close();
				return;
			}

			if (!Prepare())
			{
				Close();
				return;
			}

			if (revert_items.Count == 0)
			{
				Close();
				return;
			}

			listFiles.ItemsSource = revert_items;
			if (listFiles.Items.Count > 0)
				listFiles.SelectedIndex = 0;
		}

		//-----------------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			listFilesGrid.SaveCfg(RevertWindow.CfgPath, "ListFilesGrid");

			Cfg.Set(CfgPath, DiffColorizerControl.DiffVisible, expanderDiff.IsExpanded ? 1 : 0);
			if (!Double.IsNaN(diffColorizer.Width))
			{
				int diff_width = (int)diffColorizer.Width;
				if (diff_width > 0)
					Cfg.Set(CfgPath, DiffColorizerControl.DiffWidth, diff_width);
			}
		}

		//-----------------------------------------------------------------------------
		private void ShowFileDiff()
		{
			if (diffColorizer == null)
				return;

			if (!expanderDiff.IsExpanded)
				return;

			diffColorizer.Clear();

			if (listFiles.SelectedItems.Count == 1)
			{
				var item = (RevertItem)listFiles.SelectedItem;

				RunningOperations |= AsyncOperations.Diff;
				string rev2 = "";
				string rev1 = "";

				diffColorizer.RunHgDiffAsync(WorkingDir, item.FileInfo.File, rev1, rev2);
			}
		}

		//------------------------------------------------------------------
		private void DiffGridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			if (diffColorizer.Width > e.HorizontalChange)
				diffColorizer.Width -= e.HorizontalChange;
			else
				diffColorizer.Width = 0;
		}

		//-----------------------------------------------------------------------------
		private void expanderDiff_Expanded(object sender, RoutedEventArgs e)
		{
			ShowFileDiff();
		}

		//-----------------------------------------------------------------------------
		private void expanderDiff_Collapsed(object sender, RoutedEventArgs e)
		{
			diffColumn.Width = new GridLength(0, GridUnitType.Auto);
			diffColorizer.Clear();
		}

		//-----------------------------------------------------------------------------
		private void checkAll_Checked(object sender, RoutedEventArgs e)
		{
			foreach (var item in revert_items)
			{
				item.IsChecked = true;
			}
		}

		//-----------------------------------------------------------------------------
		private void checkAll_Unchecked(object sender, RoutedEventArgs e)
		{
			foreach (var item in revert_items)
			{
				item.IsChecked = false;
			}
		}

		//-----------------------------------------------------------------------------
		private bool Prepare()
		{
			var files_status_dict = new Dictionary<string, HgFileInfo>();

			foreach (var file_status in Hg.Status(WorkingDir))
			{
				files_status_dict.Add(file_status.File, file_status);
			}

			var dict = new Dictionary<string, HgFileStatus>();
			if (FilesToRevert != null)
			{
				foreach (var f in FilesToRevert)
				{
					string file;
					if (!Util.GetRelativePath(WorkingDir, f, out file))
						return false;

					dict.Add(file.ToLower(), HgFileStatus.NotTracked);
				}
			}

			var revert_removed = new Dictionary<string, RevertItem>();
			foreach (var tuple in files_status_dict)
			{
				var f = tuple.Value;

				switch (f.Status)
				{
					case HgFileStatus.Added:
					case HgFileStatus.Modified:
					case HgFileStatus.Removed:
					case HgFileStatus.Deleted:
						{
							var item = new RevertItem();
							string lower_f = f.File.ToLower();
							item.IsChecked = dict.ContainsKey(lower_f);
							item.FileInfo = f;

							revert_items.Add(item);
							if (f.Status == HgFileStatus.Removed)
								revert_removed.Add(f.File, item);
							break;
						}
				}
			}

			foreach (var f in revert_items)
			{
				if (f.IsChecked
					&& f.FileInfo.Status == HgFileStatus.Added
					&& !String.IsNullOrEmpty(f.FileInfo.CopiedFrom))
				{
					RevertItem item;

					if (revert_removed.TryGetValue(f.FileInfo.CopiedFrom, out item))
					{
						Logger.WriteLine("revert_removed: " + item.FileInfo.File);
						item.IsChecked = true;
					}
				}
			}

			if (CheckAllFiles)
				IsAllChecked = true;

			return true;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void btnRevert_Click(object sender, RoutedEventArgs e)
		{
			var checked_items = from item in revert_items
								where item.IsChecked
								select item;

			var checked_list = checked_items.ToList();
			if (checked_list.Count == 0)
			{
				MessageBox.Show("There are no files selected to revert", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			var to_revert_files = new List<string>();
			foreach (var revert_item in checked_list)
			{
				to_revert_files.Add(revert_item.FileInfo.File);
			}

			bool result = false;

			if (checked_list.Count == revert_items.Count)
			{
				result = Hg.Revert(WorkingDir, "", HgRevertOptions.All | HgRevertOptions.NoBackup);
			}
			else if (to_revert_files.Count > 0)
			{
				result = Hg.Revert(WorkingDir, to_revert_files);
			}
			else
			{
				return;
			}

			if (result)
			{
				RevertedFiles = new List<string>();

				foreach (var f in to_revert_files)
				{
					RevertedFiles.Add(System.IO.Path.GetFullPath(
						System.IO.Path.Combine(WorkingDir, f)));
				}

				DialogResult = true;
				Close();
			}
		}

		//------------------------------------------------------------------
		private void DiffPrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;

			if (listFiles != null && listFiles.SelectedItems.Count == 1)
			{
				var item = (RevertItem)listFiles.SelectedItem;
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
			var item = (RevertItem)listFiles.SelectedItem;

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
		private void ListFiles_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (Commands.DiffPreviousCommand != null)
			{
				if (Commands.DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					Commands.DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
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
			var item = (RevertItem)listFiles.SelectedItem;

			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = CurrentRevision.Rev.ToString();
			wnd.FileName = item.FileInfo.File;
			wnd.Owner = Window.GetWindow(this);

			// TODO: Handle updates from file history dialog
			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void Annotate_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listFiles != null && (listFiles.SelectedItems.Count == 1))
				e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Annotate_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (RevertItem)listFiles.SelectedItem;

			var wnd = new AnnotateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = CurrentRevision.Rev.ToString();
			wnd.FileName = item.FileInfo.File;
			wnd.Owner = Window.GetWindow(this);

			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void Grep_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Grep_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new GrepWindow();
			wnd.WorkingDir = WorkingDir;

			wnd.Owner = Window.GetWindow(this);
			wnd.ShowDialog();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void ViewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listFiles != null && (listFiles.SelectedItems.Count == 1))
				e.CanExecute = true;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ViewFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var item = (RevertItem)listFiles.SelectedItem;

			var hg = new Hg();
			if (	item.FileInfo.Status == HgFileStatus.Removed
				||	item.FileInfo.Status == HgFileStatus.Deleted
				)
			{
				hg.ViewFile(WorkingDir, item.FileInfo.File, CurrentRevision.Rev.ToString());
			}
			else
			{
				hg.ViewFile(WorkingDir, item.FileInfo.File, "");
			}
		}

		//------------------------------------------------------------------
		private void ListFiles_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				if (listFiles.SelectedItems.Count > 0)
				{
					int checked_count = 0;
					foreach (RevertItem item in listFiles.SelectedItems)
						if (item.IsChecked)
							checked_count++;

					// if none or some items checked, then we will check all
					bool new_checked_state = true;

					if (checked_count == listFiles.SelectedItems.Count)
					{
						// if all selected items are checked, then uncheck them
						new_checked_state = false;
					}

					foreach (RevertItem item in listFiles.SelectedItems)
						item.IsChecked = new_checked_state;
				}
			}
		}

		//------------------------------------------------------------------
		void GridViewColumnHeaderClickedHandler(object sender,
												RoutedEventArgs e)
		{
			files_sorter.GridViewColumnHeaderClickedHandler(sender, e);
		}

		//-----------------------------------------------------------------------------
		private void listViewFiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			ShowFileDiff();
		}
	}

	//==================================================================
	class RevertItem : DependencyObject
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
			typeof(RevertItem));

		public HgFileInfo FileInfo { get; set; }

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
