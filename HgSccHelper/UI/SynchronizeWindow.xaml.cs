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

using System.Linq;
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Text;
using System.Windows.Threading;
using System;
using System.Collections.Generic;
using HgSccHelper.CommandServer;
using Microsoft.Win32;
using System.Windows.Controls;
using HgSccHelper.UI;
using System.Web;
using HgSccHelper.UI.RevLog;
using RestSharp.Extensions;

namespace HgSccHelper
{
	public partial class SynchronizeWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand IncomingCommand = new RoutedUICommand("Incoming",
			"Incoming", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand PullCommand = new RoutedUICommand("Pull",
			"Pull", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand OutgoingCommand = new RoutedUICommand("Outgoing",
			"Outgoing", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand PushCommand = new RoutedUICommand("Push",
			"Push", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand SynchronizeSettingsCommand = new RoutedUICommand("Settings",
			"Settings", typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		HgThread worker;

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//-----------------------------------------------------------------------------
		HgClient HgClient { get { return UpdateContext.Cache.HgClient; } }

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ShowNewestFirstProperty =
			DependencyProperty.Register("ShowNewestFirst", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool ShowNewestFirst
		{
			get { return (bool)this.GetValue(ShowNewestFirstProperty); }
			set { this.SetValue(ShowNewestFirstProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ShowPatchProperty =
			DependencyProperty.Register("ShowPatch", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool ShowPatch
		{
			get { return (bool)this.GetValue(ShowPatchProperty); }
			set { this.SetValue(ShowPatchProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty ShowNoMergesProperty =
			DependencyProperty.Register("ShowNoMerges", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool ShowNoMerges
		{
			get { return (bool)this.GetValue(ShowNoMergesProperty); }
			set { this.SetValue(ShowNoMergesProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty CompareBookmarksProperty =
			DependencyProperty.Register("CompareBookmarks", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool CompareBookmarks
		{
			get { return (bool)this.GetValue(CompareBookmarksProperty); }
			set { this.SetValue(CompareBookmarksProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty AllowNewBranchProperty =
			DependencyProperty.Register("AllowNewBranch", typeof(bool), typeof(SynchronizeWindow));

		//-----------------------------------------------------------------------------
		public bool AllowNewBranch
		{
			get { return (bool)this.GetValue(AllowNewBranchProperty); }
			set { this.SetValue(AllowNewBranchProperty, value); }
		}

		List<PathAlias> paths;
		private List<string> after_pull_actions;

		public const string CfgPath = @"GUI\SynchronizeWindow";
		CfgWindowPosition wnd_cfg;

		private bool is_revisions_initialized;

		//-----------------------------------------------------------------------------
		private AsyncOperations async_ops;

		//-----------------------------------------------------------------------------
		private Cursor prev_cursor;

		//-----------------------------------------------------------------------------
		private SynchronizeResult pending_operation;

		//------------------------------------------------------------------
		public SynchronizeWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			HgSccHelper.UI.ThemeManager.Instance.Subscribe(this);

			worker = new HgThread();
			UpdateContext = new UpdateContext();

			comboBoxPaths.Loaded += delegate
			{
				TextBox editTextBox = comboBoxPaths.Template.FindName("PART_EditableTextBox", comboBoxPaths) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboTextChanged;
				}
			};

			comboBoxPaths.Unloaded += delegate
			{
				TextBox editTextBox = comboBoxPaths.Template.FindName("PART_EditableTextBox", comboBoxPaths) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboTextChanged;
				}
			};

			after_pull_actions = new List<string>();
   			after_pull_actions.Add("Nothing");
			after_pull_actions.Add("Update");
			if (HgExtensionsCache.Instance.IsExtensionEnabled(HgExtension.Rebase))
				after_pull_actions.Add("Rebase");

			comboAfterPull.ItemsSource = after_pull_actions;
			comboAfterPull.SelectedItem = "Update";
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Synchronize: '{0}'", WorkingDir);

			textUsername.IsEnabled = false;
			passwordBox.IsEnabled = false;

			radioRevision.IsChecked = true;

			string after_pull;
			Cfg.Get(CfgPath, "AfterPull", out after_pull, "Update");
			if (after_pull_actions.Contains(after_pull))
				comboAfterPull.SelectedItem = after_pull;

			ReloadPaths();
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
		void ReloadPaths()
		{
			var hg = new Hg();
			paths = hg.GetPaths(WorkingDir);

			comboBoxPaths.DataContext = paths;

			if (paths.Count > 0)
			{
				int default_idx = 0;
				for (int i = 0; i < paths.Count; ++i)
				{
					if (paths[i].Alias == "default")
					{
						default_idx = i;
						break;
					}
				}
				comboBoxPaths.SelectedIndex = default_idx;
			}

			comboBoxPaths.Focus();
		}

		//------------------------------------------------------------------
		private string GetSelectedRepository()
		{
			var path = comboBoxPaths.Text;
			if (Util.IsValidRemoteUrl(path))
			{
				try
				{
					if (	!String.IsNullOrEmpty(textUsername.Text)
						&&	!String.IsNullOrEmpty(passwordBox.Password)
						)
					{
						var uri_builder = new UriBuilder(path);
						uri_builder.UserName = textUsername.Text.UrlEncode();
						uri_builder.Password = passwordBox.Password.UrlEncode();
						return uri_builder.Uri.AbsoluteUri;
					}
				}
				catch (UriFormatException)
				{
				}
			}

			if (comboBoxPaths.SelectedItem != null)
			{
				var path_alias = comboBoxPaths.SelectedItem as PathAlias;
				if (path_alias != null)
					return path_alias.Alias;
			}

			return comboBoxPaths.Text;
		}

		//------------------------------------------------------------------
		private string GetTargetRevision()
		{
			if (radioRevision.IsChecked != true)
				return string.Empty;

			return comboRevision.Text;
		}

		//------------------------------------------------------------------
		private string GetTargetBranch()
		{
			if (radioBranch.IsChecked != true)
				return string.Empty;

			return comboBranch.Text;
		}

		//------------------------------------------------------------------
		private string GetTargetBookmark()
		{
			if (radioBookmark.IsChecked != true)
				return string.Empty;

			return comboBookmark.Text;
		}

		//------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			Cfg.Set(CfgPath, "AfterPull", comboAfterPull.Text);

			worker.Cancel();
			worker.Dispose();
		}

		//------------------------------------------------------------------
		private void Incoming_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Incoming_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			textBox.Text = "";
			Worker_NewMsg("[Incoming started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;
			
			var builder = new HgArgsBuilder();
			builder.AppendVerbose();
			builder.Append("incoming");

			builder.AppendInteractive(false);

			if (ShowPatch)
				builder.Append("--patch");

			if (ShowNewestFirst)
				builder.Append("--newest-first");
	
			if (ShowNoMerges)
				builder.Append("--no-merges");

			if (CompareBookmarks)
			{
				builder.Append("--bookmarks");
			}

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.AppendRevision(target_revision);

			var target_branch = GetTargetBranch();
			if (!string.IsNullOrEmpty(target_branch))
			{
				builder.Append("--branch");
				builder.Append(target_branch.Quote());
			}

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(repository.Quote());

			p.Args = builder.ToString();

			RunningOperations |= AsyncOperations.Synchronize;
			worker.Run(p);

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Outgoing_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Outgoing_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			textBox.Text = "";
			Worker_NewMsg("[Outgoing started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var builder = new HgArgsBuilder();
			builder.AppendVerbose();
			builder.Append("outgoing");

			builder.AppendInteractive(false);

			if (ShowPatch)
				builder.Append("--patch");

			if (ShowNewestFirst)
				builder.Append("--newest-first");

			if (ShowNoMerges)
				builder.Append("--no-merges");

			if (CompareBookmarks)
			{
				builder.Append("--bookmarks");
			}

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.AppendRevision(target_revision);

			var target_branch = GetTargetBranch();
			if (!string.IsNullOrEmpty(target_branch))
			{
				builder.Append("--branch");
				builder.Append(target_branch.Quote());
			}

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(repository.Quote());

			p.Args = builder.ToString();

			RunningOperations |= AsyncOperations.Synchronize;
			worker.Run(p);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Pull_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Pull_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			textBox.Text = "";
			Worker_NewMsg("[Pull started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var builder = new HgArgsBuilder();
			builder.AppendVerbose();
			builder.Append("pull");

			builder.AppendInteractive(false);
			
			switch (comboAfterPull.Text)
			{
				case "Nothing":
					{
						break;
					}
				case "Update":
					{
						builder.Append("-u");
						UpdateContext.IsParentChanged = true;
						break;
					}
				case "Rebase":
					{
						builder.Append("--rebase");
						UpdateContext.IsParentChanged = true;
						break; ;
					}
			}

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.AppendRevision(target_revision);

			var target_branch = GetTargetBranch();
			if (!string.IsNullOrEmpty(target_branch))
			{
				builder.Append("--branch");
				builder.Append(target_branch.Quote());
			}

			var target_bookmark = GetTargetBookmark();
			if (!string.IsNullOrEmpty(target_bookmark))
			{
				builder.Append("--bookmark");
				builder.Append(target_bookmark.Quote());
			}

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(repository.Quote());

			p.Args = builder.ToString();

			RunningOperations |= AsyncOperations.Synchronize;
			worker.Run(p);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Push_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && !worker.IsBusy);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Push_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			textBox.Text = "";
			Worker_NewMsg("[Push started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var builder = new HgArgsBuilder();
			builder.AppendVerbose();
			builder.Append("push");

			builder.AppendInteractive(false);
			
			if (AllowNewBranch)
				builder.Append("--new-branch");

			var target_revision = GetTargetRevision();
			if (!string.IsNullOrEmpty(target_revision))
				builder.AppendRevision(target_revision);

			var target_branch = GetTargetBranch();
			if (!string.IsNullOrEmpty(target_branch))
			{
				builder.Append("--branch");
				builder.Append(target_branch.Quote());
			}

			var target_bookmark = GetTargetBookmark();
			if (!string.IsNullOrEmpty(target_bookmark))
			{
				builder.Append("--bookmark");
				builder.Append(target_bookmark.Quote());
			}

			var repository = GetSelectedRepository();
			if (!string.IsNullOrEmpty(repository))
				builder.Append(repository.Quote());

			p.Args = builder.ToString();

			//-----------------------------------------------------------------------------
			pending_operation = new SynchronizeResult
			                    	{
			                    		Operation = SynchronizeOperation.Push,
			                    		Repository = repository
			                    	};

			RunningOperations |= AsyncOperations.Synchronize;
			worker.Run(p);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Stop_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = (worker != null && worker.IsBusy && !worker.CancellationPending);
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Stop_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			RunningOperations &= ~AsyncOperations.Synchronize;
			worker.Cancel();
			e.Handled = true;
		}

		//------------------------------------------------------------------
		void Worker_NewMsg(string msg)
		{
			textBox.AppendText(msg + "\n");
			textBox.ScrollToEnd();
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			RunningOperations &= ~AsyncOperations.Synchronize;

			Worker_NewMsg("");

			switch(completed.Status)
			{
				case HgThreadStatus.Completed:
					{
						var msg = String.Format("[Operation completed. Exit code: {0}]", completed.ExitCode);
						Worker_NewMsg(msg);

						if (	completed.ExitCode == 0
							&&	pending_operation != null
							&&	pending_operation.Operation == SynchronizeOperation.Push
							&&	comboBoxPaths.Items.Count == 0
							)
						{
							var hg_paths = new HgPaths(WorkingDir);
							if (hg_paths.Count == 0)
							{
								hg_paths.Set("default", pending_operation.Repository);
								hg_paths.Save();
								ReloadPaths();
							}
						}
						break;
					}
				case HgThreadStatus.Canceled:
					{
						Worker_NewMsg("[Operation canceled]");
						break;
					}
				case HgThreadStatus.Error:
					{
						Worker_NewMsg("[Error: " + completed.ErrorMessage + "]");
						break;
					}
			}

			pending_operation = null;

			// Updating commands state (CanExecute)
			CommandManager.InvalidateRequerySuggested();
		}

		//------------------------------------------------------------------
		void Error_Handler(string msg)
		{
			var error_msg = string.Format("[Error: {0}]", msg);
			Dispatcher.Invoke(DispatcherPriority.Normal,
					new Action<string>(Worker_NewMsg), error_msg);
		}

		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			Dispatcher.Invoke(DispatcherPriority.Normal,
				new Action<string>(Worker_NewMsg), msg);
		}

		//------------------------------------------------------------------
		private void Browse_Click(object sender, RoutedEventArgs e)
		{
			using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
			{
				dlg.Description = "Browse for repository...";
				dlg.ShowNewFolderButton = false;
				dlg.SelectedPath = WorkingDir;
				
				var result = dlg.ShowDialog();
				if (result == System.Windows.Forms.DialogResult.OK)
				{
					comboBoxPaths.Text = dlg.SelectedPath;
				}
			}
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//-----------------------------------------------------------------------------
		private void Kiln_Click(object sender, RoutedEventArgs e)
		{
			if (	!Kiln.Session.Instance.IsValid
				&&	!Kiln.Session.Instance.Login(Kiln.Credentials.Instance.Site,
						Kiln.Credentials.Instance.Username, Kiln.Credentials.Instance.Password)
				)
			{
				var wnd = new Kiln.LoginWindow();
				var res = wnd.ShowDialog();

				if (res != true)
					return;
			}

			var repos_wnd = new Kiln.RepositoriesWindow();
			if (repos_wnd.ShowDialog() != true)
				return;

			var builder = new UriBuilder(repos_wnd.RepositoryUri);
			comboBoxPaths.Text = Util.RemoveUrlPassword(builder.Uri.AbsoluteUri);
			textUsername.Text = builder.UserName.UrlDecode();
			passwordBox.Password = builder.Password.UrlDecode();

			return;
		}

		//-----------------------------------------------------------------------------
		private void BitBucket_Click(object sender, RoutedEventArgs e)
		{
			if (!BitBucket.Util.CheckUser(BitBucket.Credentials.Instance.Username,
										  BitBucket.Credentials.Instance.Password))
			{
				var wnd = new BitBucket.LoginWindow();
				var res = wnd.ShowDialog();

				if (res != true)
					return;
			}

			var repos_wnd = new BitBucket.RepositoriesWindow();
			if (repos_wnd.ShowDialog() != true)
				return;

			var builder = new UriBuilder(repos_wnd.RepositoryUri);

			comboBoxPaths.Text = Util.RemoveUrlPassword(builder.Uri.AbsoluteUri);
			textUsername.Text = builder.UserName.UrlDecode();
			passwordBox.Password = builder.Password.UrlDecode();
			return;
		}

		//-----------------------------------------------------------------------------
		private void BrowseForBundle_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog();
			dlg.CheckPathExists = true;
			dlg.CheckFileExists = true;
			dlg.InitialDirectory = WorkingDir;
			dlg.Filter = String.Format("{0}|*{1}", "Mercurial bundle files (*.hg)", ".hg");
			dlg.Title = "Browse for mercurial bundle file...";
			dlg.RestoreDirectory = true;

			var result = dlg.ShowDialog(this);
			if (result == true)
			{
				comboBoxPaths.Text = dlg.FileName;
			}
		}

		//-----------------------------------------------------------------------------
		private void SynchronizeSettings_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void SynchronizeSettings_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var wnd = new SynchronizeSettingsWindow();
			wnd.WorkingDir = WorkingDir;

			if (wnd.ShowDialog() == true)
				ReloadPaths();
		}

		//------------------------------------------------------------------
		private void OnComboTextChanged(object sender, TextChangedEventArgs e)
		{
			var url = comboBoxPaths.Text;
			if (Util.IsValidRemoteUrl(url))
			{
				var builder = new UriBuilder(url);
				textUsername.Text = builder.UserName.UrlDecode();

				if (	!String.IsNullOrEmpty(builder.Password)
					&&	builder.Password != "***")
				{
					passwordBox.Password = builder.Password.UrlDecode();
					comboBoxPaths.Text = Util.RemoveUrlPassword(url);
				}

				textUsername.IsEnabled = true;
				passwordBox.IsEnabled = true;
			}
			else
			{
				textUsername.IsEnabled = false;
				passwordBox.IsEnabled = false;
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncTags(List<TagInfo> tags_list)
		{
			RunningOperations &= ~AsyncOperations.Tags;

			if (tags_list == null)
				return;

			foreach (var tag in tags_list)
			{
				var item = new UpdateComboItem();
				item.GroupText = "Tag";
				item.Name = tag.Name;
				item.Rev = tag.Rev;
				item.SHA1 = tag.SHA1;
				item.Misc = tag.IsLocal ? "Local" : "";

				comboRevision.Items.Add(item);
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncBookmarks(List<BookmarkInfo> bookmarks_list)
		{
			RunningOperations &= ~AsyncOperations.Bookmarks;

			if (bookmarks_list == null)
				return;

			foreach (var bookmark in bookmarks_list)
			{
				var item = new UpdateComboItem();
				item.GroupText = "Bookmark";
				item.Name = bookmark.Name;
				item.Rev = bookmark.Rev;
				item.SHA1 = bookmark.SHA1;
				item.Misc = bookmark.IsCurrent ? "Current" : "";

				comboRevision.Items.Add(item);
				comboBookmark.Items.Add(item);
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncBranch(List<BranchInfo> branch_list)
		{
			RunningOperations &= ~AsyncOperations.Branches;

			if (branch_list == null)
				return;

			foreach (var branch in branch_list)
			{
				var item = new UpdateComboItem();
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

				comboRevision.Items.Add(item);
				comboBranch.Items.Add(item);
			}
		}

		//-----------------------------------------------------------------------------
		private void TargetRevision_Expanded(object sender, RoutedEventArgs e)
		{
			if (!is_revisions_initialized)
			{
				is_revisions_initialized = true;

				OnAsyncBookmarks(HgClient.Bookmarks());
				OnAsyncTags(HgClient.Tags());
				OnAsyncBranch(HgClient.Branches(HgBranchesOptions.Closed));
			}
		}
	}

	//-----------------------------------------------------------------------------
	internal enum SynchronizeOperation
	{
		Pull,
		Push,
		Incoming,
		Outgoing
	}

	//-----------------------------------------------------------------------------
	internal class SynchronizeResult
	{
		public SynchronizeOperation Operation { get; set; }
		public string Repository { get; set; }
	}
}
