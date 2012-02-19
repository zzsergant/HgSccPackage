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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using HgSccHelper.CommandServer;
using HgSccHelper.UI;
using HgSccHelper.UI.RevLog;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for CommitWindow.xaml
	/// </summary>
	public partial class CommitWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand CommitCommand = new RoutedUICommand("Commit",
			"Commit", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand MarkResolvedCommand = new RoutedUICommand("Mark Resolved",
			"MarkResolved", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand MarkUnresolvedCommand = new RoutedUICommand("Mark Unresolved",
			"MarkUnresolved", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand RestartMergeCommand = new RoutedUICommand("Restart Merge",
			"RestartMerge", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand SetBranchNameCommand = new RoutedUICommand("Set Branch Name",
			"SetBranchName", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ResetBranchNameCommand = new RoutedUICommand("Reset Branch Name",
			"ResetBranchName", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand CloseNamedBranchCommand = new RoutedUICommand("Close Named Branch",
			"CloseNamedBranch", typeof(CommitWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ClearPendingBranchCloseCommand = new RoutedUICommand("Clear Pending Branch Close",
			"ClearPendingBranchClose", typeof(CommitWindow));

		ObservableCollection<CommitItem> commit_items;
		ObservableCollection<RevLogChangeDesc> parents;
		List<MergeToolInfo> merge_tools;
		DeferredCommandExecutor deferred_executor;

		GridViewColumnSorter files_sorter;

		public const string CfgPath = @"GUI\CommitWindow";

		private const HgStatusOptions StatusOptions = HgStatusOptions.Added
		                                              | HgStatusOptions.Deleted | HgStatusOptions.Modified
		                                              | HgStatusOptions.Copies | HgStatusOptions.Removed;

		CfgWindowPosition wnd_cfg;

		private AsyncStatus async_status;

		private Dictionary<string, ResolveStatus> resolve_dict;

		//-----------------------------------------------------------------------------
		public CommitWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			commit_items = new ObservableCollection<CommitItem>();
			parents = new ObservableCollection<RevLogChangeDesc>();

			VirtualizingStackPanel.SetIsVirtualizing(listFiles, true);
			VirtualizingStackPanel.SetVirtualizationMode(listFiles, VirtualizationMode.Recycling);

			UpdateContext = new UpdateContext();
			SubRepoDirs = new List<string>();

			NamedBranchOp = new NamedBranchOperation();
			branchPanel.DataContext = NamedBranchOp;

			deferred_executor = new DeferredCommandExecutor();

			files_sorter = new GridViewColumnSorter(listFiles);
			files_sorter.ExcludeColumn(checkColumn);

			diffColorizer.Complete = new Action<List<string>>(OnDiffColorizer);

			async_status = new AsyncStatus();
			async_status.Complete = new Action<AsyncStatusResult>(OnAsyncStatus);

			resolve_dict = new Dictionary<string, ResolveStatus>();
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public List<string> SubRepoDirs { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//-----------------------------------------------------------------------------
		private HgClient HgClient { get { return UpdateContext.Cache.HgClient; } }

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
		public bool CheckAllFiles { get; set; }

		//------------------------------------------------------------------
		public List<string> CommitedFiles { private set; get; }

		//------------------------------------------------------------------
		public Dictionary<string, List<string>> CommitedSubrepoFiles { private set; get; }

		//------------------------------------------------------------------
		private List<string> dirty_sub_repos;

		//------------------------------------------------------------------
		private Queue<StatusTask> pending_status_tasks;

		//------------------------------------------------------------------
		private StatusTask async_status_task;

		//------------------------------------------------------------------
		private ParentsInfo ParentsInfo { get; set; }

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
		public static readonly DependencyProperty IsAllCheckedProperty =
			DependencyProperty.Register("IsAllChecked", typeof(bool),
			typeof(CommitWindow));

		//------------------------------------------------------------------
		private bool IsAllChecked
		{
			get { return (bool)this.GetValue(IsAllCheckedProperty); }
			set { this.SetValue(IsAllCheckedProperty, value); }
		}

		//------------------------------------------------------------------
		NamedBranchOperation NamedBranchOp { get; set; }

		//-----------------------------------------------------------------------------
		private AsyncOperations async_ops;

		//-----------------------------------------------------------------------------
		private Cursor prev_cursor;

		//------------------------------------------------------------------
		class StatusTask
		{
			public string Path { get; set; }
			public string SubRepoDir { get; set; }
			public bool IsSubrepo
			{
				get { return !string.IsNullOrEmpty(SubRepoDir); }
			}
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Commit: '{0}'", WorkingDir);

			listFilesGrid.LoadCfg(CommitWindow.CfgPath, "ListFilesGrid");

			int diff_width;
			Cfg.Get(CfgPath, DiffColorizerControl.DiffWidth, out diff_width, DiffColorizerControl.DefaultWidth);
			diffColorizer.Width = diff_width;

			int diff_visible;
			Cfg.Get(CfgPath, DiffColorizerControl.DiffVisible, out diff_visible, 1);
			expanderDiff.IsExpanded = (diff_visible != 0);

			dirty_sub_repos = new List<string>();

			pending_status_tasks = new Queue<StatusTask>();
			foreach (var sub_repo_dir in SubRepoDirs)
			{
				pending_status_tasks.Enqueue(new StatusTask
				                             	{
				                             		Path = System.IO.Path.Combine(WorkingDir, sub_repo_dir),
													SubRepoDir = sub_repo_dir
				                             	});
			}

			pending_status_tasks.Enqueue(new StatusTask { Path = WorkingDir });

			RunningOperations |= AsyncOperations.Status;
			async_status_task = pending_status_tasks.Dequeue();
			async_status.Run(async_status_task.Path, StatusOptions);

			textCommitMessage.Focus();
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

					CommandManager.InvalidateRequerySuggested();
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void OnDiffColorizer(List<string> obj)
		{
			RunningOperations &= ~AsyncOperations.Diff;
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncParents(ParentsInfo new_current)
		{
			RunningOperations &= ~AsyncOperations.Parents;

			ParentsInfo = new_current;
			if (ParentsInfo == null)
			{
				// If the repository is empty (have no revisions),
				// then we should use identify to get null revision

				var identify = HgClient.Identify();
				if (identify == null || identify.Parents.Count == 0 || identify.Rev != -1)
				{
					Close();
					return;
				}

				var null_parent = new RevLogChangeDesc { Rev = identify.Rev, SHA1 = identify.SHA1 };
				var parents_list = new List<RevLogChangeDesc>();
				parents_list.Add(null_parent);

				ParentsInfo = new ParentsInfo {Parents = parents_list};
			}

			if (ParentsInfo.Parents.Count == 2)
			{
				IsMergeActive = true;
				var grid_view = (GridView)listFiles.View;
				grid_view.Columns.RemoveAt(0);
			}
			else
			{
				var grid_view = (GridView)listFiles.View;
				grid_view.Columns.RemoveAt(2);
			}

			if (ParentsInfo.Rev == -1)
			{
				// no need to get change desc for 'null' revision
				OnAsyncChangeDesc(ParentsInfo.Parents[0]);
			}
			else
			{
				var desc = HgClient.GetRevisionDesc(ParentsInfo.Parents[0].SHA1);
				OnAsyncChangeDesc(desc);
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncChangeDesc(RevLogChangeDesc desc)
		{
			parents.Add(desc);
			if (parents.Count < ParentsInfo.Parents.Count)
			{
				var desc2 = HgClient.GetRevisionDesc(ParentsInfo.Parents[parents.Count].SHA1);
				parents.Add(desc2);
			}

			UpdateBranchName();

			if (IsMergeActive)
			{
				textParent1.Text = parents[0].GetDescription();
				textParent2.Text = parents[1].GetDescription();

				var hg_merge_tools = new HgMergeTools();
				merge_tools = hg_merge_tools.GetMergeTools();
				if (merge_tools.Count > 0)
				{
					menuRestartMergeWith.Items.Add(new Separator());

					foreach (var tool in merge_tools)
					{
						var item = new MenuItem();
						item.Header = tool.Alias;
						item.Command = RestartMergeCommand;
						item.CommandParameter = tool.Alias;

						menuRestartMergeWith.Items.Add(item);
					}
				}
			}
			else
			{
				parentsGrid.ColumnDefinitions.RemoveAt(1);

				// FIXME: It should be textParent1, but
				// after removing column definition from the grid
				// it leaves textParent2 textBox
				textParent2.Text = parents[0].GetDescription();
			}

			if (IsMergeActive)
			{
				OnAsyncResolveList(HgClient.GetResolveList());
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncResolveList(List<ResolveInfo> resolve_list)
		{
			RunningOperations &= ~AsyncOperations.ResolveList;

			if (resolve_list == null)
			{
				Close();
				return;
			}

			foreach (var file in resolve_list)
				resolve_dict[file.Path.ToLower()] = file.Status;

			foreach (var item in commit_items)
			{
				var lower_f = item.FileInfo.File.ToLower();
				var resolve_status = ResolveStatus.None;
				
				if (resolve_dict.TryGetValue(lower_f, out resolve_status))
					item.ResolveStatus = resolve_status;
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncStatus(AsyncStatusResult result)
		{
			RunningOperations &= ~AsyncOperations.Status;

			if (result == null)
			{
				Close();
				return;
			}

			if (async_status_task.IsSubrepo)
			{
				// Subrepo status
				if (result.Files.Count != 0)
				{
					dirty_sub_repos.Add(async_status_task.SubRepoDir);
				}

				// There is either a pending subrepo or pending WorkDir
				RunningOperations |= AsyncOperations.Status;
				async_status_task = pending_status_tasks.Dequeue();
				async_status.Run(async_status_task.Path, StatusOptions);
				return;
			}

			// This is all I can do for subrepo atm. Still thinking of better design. Should we list all changes in subrepo also?
			// The behavior of TortoiseHg is not good either (show that subrepo change, but doesn't list what changes).
			// People at mercurial mail list seems to prefer not to commit recursively.

			if (dirty_sub_repos.Count > 0)
			{
				var msg = new StringBuilder();
				msg.AppendLine("Please commit the following subrepo(s) individually:");
				msg.AppendLine();
				foreach (var sub_repo in dirty_sub_repos)
					msg.AppendLine(sub_repo);

				msg.AppendLine();
				msg.AppendLine("Do you want to force commit from main repo? [NOT RECOMMENDED]");

				var msg_result = MessageBox.Show(msg.ToString(), "One or more sub repository contain uncommitted changes",
					MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
				
				if (msg_result != MessageBoxResult.OK)
				{
					Close();
					return;
				}
			}

			RunningOperations |= AsyncOperations.Parents;
			OnAsyncParents(HgClient.Parents());

			var files_status_dict = new Dictionary<string, HgFileInfo>();
			foreach (var file_status in result.Files)
			{
				files_status_dict.Add(file_status.File, file_status);
			}

			var dict = new Dictionary<string, HgFileStatus>();
			if (FilesToCommit != null)
			{
				foreach (var f in FilesToCommit)
				{
					string file;
					if (!Util.GetRelativePath(WorkingDir, f, out file))
					{
						Close();
						return;
					}

					dict.Add(file.ToLower(), HgFileStatus.NotTracked);
				}
			}

			var commit_removed = new Dictionary<string, CommitItem>();
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
							var item = new CommitItem();
							string lower_f = f.File.ToLower();
							item.IsChecked = dict.ContainsKey(lower_f);
							item.FileInfo = f;
							item.ResolveStatus = ResolveStatus.None;

							if (IsMergeActive)
							{
								var resolve_status = ResolveStatus.None;
								if (resolve_dict.TryGetValue(lower_f, out resolve_status))
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

					if (commit_removed.TryGetValue(f.FileInfo.CopiedFrom, out item))
					{
						Logger.WriteLine("commit_removed: " + item.FileInfo.File);
						item.IsChecked = true;
					}
				}
			}

			if (CheckAllFiles)
				IsAllChecked = true;

			if (commit_items.Count == 0)
			{
				Close();
				return;
			}

			listFiles.ItemsSource = commit_items;
			if (listFiles.Items.Count > 0)
			{
				var first_checked = commit_items.FirstOrDefault(item => item.IsChecked);

				if (first_checked != null)
					listFiles.SelectedItem = first_checked;

				if (listFiles.SelectedIndex == -1)
					listFiles.SelectedIndex = 0;

				listFiles.ScrollIntoView(listFiles.SelectedItem);
			}
		}

		//------------------------------------------------------------------
		void UpdateBranchName()
		{
			string branch_name = HgClient.GetBranchName();
			if (branch_name != null)
			{
				NamedBranchOp.BranchName = branch_name;
				NamedBranchOp.IsNewBranch = false;

				// FIXME: Comparing only with first parent ?
				if (parents[0] == null && NamedBranchOp.BranchName != "default")
				{
					// If the branch name changed from the very first commit
					NamedBranchOp.IsNewBranch = true;
				}
				else
					if (parents[0] != null && parents[0].Branch != NamedBranchOp.BranchName)
					{
						NamedBranchOp.IsNewBranch = true;
					}
			}
		}

		//-----------------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			diffColorizer.Dispose();

			Cfg.Set(CfgPath, DiffColorizerControl.DiffVisible, expanderDiff.IsExpanded ? 1 : 0);
			if (!Double.IsNaN(diffColorizer.Width))
			{
				int diff_width = (int)diffColorizer.Width;
				if (diff_width > 0)
					Cfg.Set(CfgPath, DiffColorizerControl.DiffWidth, diff_width);
			}

			listFilesGrid.SaveCfg(CommitWindow.CfgPath, "ListFilesGrid");
			deferred_executor.Dispose();
		}

		//-----------------------------------------------------------------------------
		private void ShowFileDiff()
		{
			if (diffColorizer == null)
				return;

			if (!expanderDiff.IsExpanded)
				return;

			diffColorizer.Clear();

			if (listFiles != null && listFiles.SelectedItems.Count == 1)
			{
				var item = (CommitItem)listFiles.SelectedItem;

				string parent = "";
				if (parents.Count != 0)
				{
					if (parents[0] != null)
						parent = parents[0].SHA1;
				}

				RunningOperations |= AsyncOperations.Diff;
				diffColorizer.RunHgDiffAsync(HgClient, item.FileInfo.File,
					parent, "");
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

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void Commit_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (commit_items != null)
			{
				e.CanExecute = (commit_items.Count != 0) && (RunningOperations == AsyncOperations.None);
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Commit_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(CommitMessage))
			{
				MessageBox.Show("You should specify commit message", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			var options = HgCommitOptions.None;
			if (NamedBranchOp.IsClosingNamedBranch)
				options |= HgCommitOptions.CloseBranch;

			if (IsMergeActive)
			{
				if (commit_items.Any(item => item.ResolveStatus == ResolveStatus.Unresolved))
				{
					MessageBox.Show("There are files with unresolved status.\nYou must merge them or mark as resolved.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
					return;
				}


				CommitResult commit_result = HgClient.CommitAll(options, CommitMessage);
				if (commit_result.IsSuccess)
				{
					CommitedFiles = new List<string>();
					CommitedSubrepoFiles = new Dictionary<string, List<string>>();

					foreach (var f in commit_items)
					{
						CommitedFiles.Add(System.IO.Path.GetFullPath(
							System.IO.Path.Combine(WorkingDir, f.FileInfo.File)));
					}

					// FIXME: commit_result does not list all commited files, for example it does not
					// list removed files. Need the other way to get that info or reset subrepo status cache.

					foreach (var kvp in commit_result.CommitedSubrepoFiles)
					{
						CommitedSubrepoFiles.Add(kvp.Key, new List<string>());
						foreach (var file in kvp.Value)
						{
							CommitedSubrepoFiles[kvp.Key].Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(
									System.IO.Path.Combine(WorkingDir, kvp.Key), file)));
						}
					}

					UpdateContext.IsParentChanged = true;
					UpdateContext.IsCommited = true;
					UpdateContext.IsBranchChanged = true;
					UpdateContext.IsBookmarksChanged = true;

					DialogResult = true;
					Close();
				}
			}
			else
			{
				var checked_items = from item in commit_items
									where item.IsChecked
									select item;

				var checked_list = checked_items.ToList();
				if (checked_list.Count == 0)
				{
					MessageBox.Show("There are no files selected to commit", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				bool checked_deleted =
					checked_list.Any(item => item.FileInfo.Status == HgFileStatus.Deleted);

				if (checked_deleted)
				{
					var del_result =
						MessageBox.Show(
							"You are trying to commit the files which are deleted localy, but not removed in mercurial.\nAre you sure to proceed ?",
							"Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

					if (del_result != MessageBoxResult.OK)
						return;
				}

				var to_commit_files = new List<string>();
				foreach (var commit_item in checked_list)
				{
					to_commit_files.Add(commit_item.FileInfo.File);
				}

				CommitResult commit_result;

				if (checked_list.Count == commit_items.Count)
				{
					commit_result = HgClient.CommitAll(options, CommitMessage);
				}
				else if (to_commit_files.Count > 0)
				{
					try
					{
						commit_result = HgClient.Commit(options, to_commit_files, CommitMessage);
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

				if (commit_result.IsSuccess)
				{
					CommitedFiles = new List<string>();
					CommitedSubrepoFiles = new Dictionary<string, List<string>>();

					foreach (var f in to_commit_files)
					{
						CommitedFiles.Add(System.IO.Path.GetFullPath(
							System.IO.Path.Combine(WorkingDir, f)));
					}

					// FIXME: commit_result does not list all commited files, for example it does not
					// list removed files. Need the other way to get that info or reset subrepo status cache.

					foreach (var kvp in commit_result.CommitedSubrepoFiles)
					{
						string subrepo_root = System.IO.Path.Combine(WorkingDir, kvp.Key);
						CommitedSubrepoFiles.Add(subrepo_root, new List<string>());
						foreach (var file in kvp.Value)
						{
							CommitedSubrepoFiles[subrepo_root].Add(System.IO.Path.GetFullPath(
								System.IO.Path.Combine(subrepo_root, file)));
						}
					}

					UpdateContext.IsParentChanged = true;
					UpdateContext.IsCommited = true;
					UpdateContext.IsBranchChanged = true;
					UpdateContext.IsBookmarksChanged = true;

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
				HgClient.Diff(item.FileInfo.File, out is_different);
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

			if (listFiles != null && listFiles.SelectedItems.Count == 1 && ParentsInfo != null)
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
				HgClient.DiffWithRevision(item.FileInfo.File,
					ParentsInfo.Parents[0].SHA1, out is_different);
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
				HgClient.DiffWithRevision(item.FileInfo.File,
					ParentsInfo.Parents[1].SHA1, out is_different);
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
		private void RestartMerge_CanExecute(object sender, CanExecuteRoutedEventArgs e)
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
		private void RestartMerge_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var tool_name = e.Parameter as string;
			var item = (CommitItem)listFiles.SelectedItem;

			deferred_executor.QueueDefferedExecute(() =>
			{
				var hg_resolve = new HgResolve();
				if (hg_resolve.Resolve(WorkingDir, item.FileInfo.File, tool_name ?? ""))
				{
					item.ResolveStatus = hg_resolve.GetResolveStatus(WorkingDir, item.FileInfo.File);
				}
			});

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
			var item = (CommitItem)listFiles.SelectedItem;

			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = ParentsInfo.Rev.ToString();
			wnd.FileName = item.FileInfo.File;
			wnd.Owner = Window.GetWindow(this);
			wnd.UpdateContext.Cache.HgClient = HgClient;

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
			var item = (CommitItem)listFiles.SelectedItem;

			var wnd = new AnnotateWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = ParentsInfo.Rev.ToString();
			wnd.FileName = item.FileInfo.File;
			wnd.Owner = Window.GetWindow(this);
			wnd.UpdateContext.Cache.HgClient = HgClient;

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
			var item = (CommitItem)listFiles.SelectedItem;

			if (	item.FileInfo.Status == HgFileStatus.Removed
				||	item.FileInfo.Status == HgFileStatus.Deleted
				)
			{
				HgClient.ViewFile(item.FileInfo.File, ParentsInfo.Rev.ToString());
			}
			else
			{
				HgClient.ViewFile(item.FileInfo.File, "");
			}
		}

		//------------------------------------------------------------------
		private void ListFiles_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space && !IsMergeActive)
			{
				if (listFiles.SelectedItems.Count > 0)
				{
					int checked_count = 0;
					foreach (CommitItem item in listFiles.SelectedItems)
						if (item.IsChecked)
							checked_count++;

					// if none or some items checked, then we will check all
					bool new_checked_state = true;

					if (checked_count == listFiles.SelectedItems.Count)
					{
						// if all selected items are checked, then uncheck them
						new_checked_state = false;
					}

					foreach (CommitItem item in listFiles.SelectedItems)
						item.IsChecked = new_checked_state;
				}
			}
		}

		//------------------------------------------------------------------
		private void btnCancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void SetBranchName_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (NamedBranchOp != null)
			{
				e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void SetBranchName_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new BranchNameWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Owner = Window.GetWindow(this);

			if (wnd.ShowDialog() == true)
			{
				var branches = HgClient.Branches(HgBranchesOptions.Closed);

				bool is_forced = false;
				if (branches.Any(branch => branch.Name == wnd.BranchName))
				{
					var msg = String.Format("The branch with name '{0}' is allready exists.\nAre you sure to force branch name change ?", wnd.BranchName);
					var result = MessageBox.Show(msg, "Question", MessageBoxButton.YesNo, MessageBoxImage.Question);
					if (result != MessageBoxResult.Yes)
						return;

					is_forced = true;
				}

				var options = HgBranchOptions.None;
				if (is_forced)
					options = HgBranchOptions.Force;

				if (!HgClient.SetBranchName(wnd.BranchName, options))
				{
					MessageBox.Show("Unable to set branch name", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				UpdateBranchName();
			}
		}

		//------------------------------------------------------------------
		private void ResetBranchName_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (NamedBranchOp != null)
				e.CanExecute = NamedBranchOp.IsNewBranch;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ResetBranchName_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			HgClient.ResetBranchName();
			UpdateBranchName();
		}

		//------------------------------------------------------------------
		private void CloseNamedBranch_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (NamedBranchOp != null)
				e.CanExecute = !NamedBranchOp.IsClosingNamedBranch;

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void CloseNamedBranch_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			NamedBranchOp.IsClosingNamedBranch = true;
		}

		//------------------------------------------------------------------
		private void ClearPendingBranchClose_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (NamedBranchOp != null)
				e.CanExecute = NamedBranchOp.IsClosingNamedBranch;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ClearPendingBranchClose_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			NamedBranchOp.IsClosingNamedBranch = false;
		}

		//------------------------------------------------------------------
		private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			textCommitMessage.Height = commitMessageRow.Height.Value;
		}

		//------------------------------------------------------------------
		void GridViewColumnHeaderClickedHandler(object sender,
												RoutedEventArgs e)
		{
			files_sorter.GridViewColumnHeaderClickedHandler(sender, e);
		}

		//-----------------------------------------------------------------------------
		private void listFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ShowFileDiff();
		}
	}

	//==================================================================
	class CommitItem : DependencyObject
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

		//-----------------------------------------------------------------------------
		public HgFileInfo FileInfo { get; set; }

		//-----------------------------------------------------------------------------
		public HgFileStatus Status { get { return FileInfo.Status; } }

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
				return FileInfo.FileViewString;
			}
		}
	}

	//==================================================================
	class NamedBranchOperation : DependencyObject
	{
		//-----------------------------------------------------------------------------
		public bool IsNewBranch
		{
			get { return (bool)this.GetValue(IsNewBranchProperty); }
			set { this.SetValue(IsNewBranchProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsNewBranchProperty =
			DependencyProperty.Register("IsNewBranch", typeof(bool),
			typeof(NamedBranchOperation));

		//-----------------------------------------------------------------------------
		public bool IsClosingNamedBranch
		{
			get { return (bool)this.GetValue(IsClosingNamedBranchProperty); }
			set { this.SetValue(IsClosingNamedBranchProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty IsClosingNamedBranchProperty =
			DependencyProperty.Register("IsClosingNamedBranch", typeof(bool),
			typeof(NamedBranchOperation));

		//-----------------------------------------------------------------------------
		public string BranchName
		{
			get { return (string)this.GetValue(BranchNameProperty); }
			set { this.SetValue(BranchNameProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty BranchNameProperty =
			DependencyProperty.Register("BranchName", typeof(string),
			typeof(NamedBranchOperation));
	}
}
