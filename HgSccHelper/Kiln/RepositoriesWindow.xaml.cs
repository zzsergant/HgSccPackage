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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace HgSccHelper.Kiln
{
	/// <summary>
	/// Interaction logic for RepositoriesWindow.xaml
	/// </summary>
	public partial class RepositoriesWindow : Window
	{
		private ObservableCollection<KilnRepoListItem> repositories;
		
		private Dictionary<int, KilnProject> projects_map;
		private Dictionary<int, KilnGroup> groups_map;

		public Uri RepositoryUri { get; private set; }
		private DispatcherTimer timer;
		private KilnRepoListItem pending_new_repo;

		//-----------------------------------------------------------------------------
		public static RoutedUICommand NewRepositoryCommand = new RoutedUICommand("New remote repository",
			"NewRepository", typeof(RepositoriesWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DeleteRepositoryCommand = new RoutedUICommand("Delete remote repository",
			"DeleteRepository", typeof(RepositoriesWindow));

		//-----------------------------------------------------------------------------
		public RepositoriesWindow()
		{
			InitializeComponent();

			repositories = new ObservableCollection<KilnRepoListItem>();
			this.DataContext = repositories;

			projects_map = new Dictionary<int, KilnProject>();
			groups_map = new Dictionary<int, KilnGroup>();

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromSeconds(1);
			timer.Tick += OnStatusTimerTick;
		}

		//------------------------------------------------------------------
		private void OnStatusTimerTick(object o, EventArgs e)
		{
			var repo = Session.Instance.GetRepository(pending_new_repo.Repo.ixRepo);
			if (repo == null)
			{
				timer.Stop();
				MessageBox.Show("Unable to update repository status", "Error",
				                MessageBoxButton.OK, MessageBoxImage.Error);

				return;
			}

			if (pending_new_repo.Repo.sStatus != repo.sStatus)
			{
				pending_new_repo.Repo.sStatus = repo.sStatus;
				if (repo.sStatus == "good")
				{
					timer.Stop();
					pending_new_repo = null;
					labelNewRepository.Visibility = Visibility.Collapsed;

					CommandManager.InvalidateRequerySuggested();
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			var projects = Session.Instance.GetProjects();

			btnSelect.IsEnabled = false;

			foreach (var project in projects)
			{
				projects_map.Add(project.ixProject, project);
				foreach (var group in project.repoGroups)
				{
					var group_text = String.Format("{0}/{1}", project.sName, group.DisplayName);

					groups_map.Add(group.ixRepoGroup, group);
					foreach (var repo in group.repos)
					{
						var item = new KilnRepoListItem
						           	{
						           		Repo = repo,
						           		GroupText = group_text,
						           	};

						repositories.Add(item);
					}
				}
			}

			var myView = (CollectionView)CollectionViewSource.GetDefaultView(listRepos.ItemsSource);
			var groupDescription = new PropertyGroupDescription("GroupText");
			myView.GroupDescriptions.Add(groupDescription);
		}

		//-----------------------------------------------------------------------------
		private void Select_Click(object sender, RoutedEventArgs e)
		{
			var item = (KilnRepoListItem)listRepos.SelectedItem;
			if (item == null)
				return;

			if (item.Repo.sStatus != "good")
			{
				if (item.Repo.sStatus == "new")
				{
					MessageBox.Show("The repository is not yet created.\nPlease wait.\n",
				                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
				else
				{
					MessageBox.Show("The repository have '" + item.Repo.sStatus + "' status",
								"Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
				}

				return;
			}

			var repo = item.Repo;
			var uri_builder = new UriBuilder(Session.Instance.MakeRepoUrl(repo.sProjectSlug, repo.sGroupSlug, repo.sSlug));

			// FIXME: Use session credentials
			uri_builder.UserName = HttpUtility.UrlEncode(Credentials.Instance.Username);
			uri_builder.Password = HttpUtility.UrlEncode(Credentials.Instance.Password);
			
			RepositoryUri = uri_builder.Uri;
			DialogResult = true;
		}

		//-----------------------------------------------------------------------------
		private void listRepos_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var item = (KilnRepoListItem)listRepos.SelectedItem;
			if (item == null)
			{
				textSelectedRepo.Text = "";
			}
			else
			{
				var repo = item.Repo;

				textSelectedRepo.Text = Session.Instance.MakeRepoUrl(repo.sProjectSlug, repo.sGroupSlug, repo.sSlug);
				btnSelect.IsEnabled = true;
			}
		}

		//------------------------------------------------------------------
		private void NewRepository_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = groups_map != null && groups_map.Count != 0;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void NewRepository_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var group_list = new List<NewRepoGroupItem>();
			foreach (var group in groups_map.Values)
			{
				var project = projects_map[group.ixProject];
				var item = new NewRepoGroupItem
				           	{
				           		DisplayName =
				           			String.Format("{0}/{1}", project.sName, group.DisplayName),
				           		Group = group
				           	};
				
				group_list.Add(item);
			}

			var wnd = new NewRepositoryWindow();
			wnd.Groups = group_list;
			
			if (wnd.ShowDialog() == true)
			{
				var new_repo = Session.Instance.CreateRepository(
					wnd.RepositoryGroup.Group.ixRepoGroup,
					wnd.RepositoryName);

				if (new_repo == null)
				{
					MessageBox.Show("An error occured while creating new repository", "Error",
									MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				var new_repo_item = MakeKilnRepoListItem(new_repo);
				repositories.Add(new_repo_item);

				if (new_repo.sStatus == "new")
				{
					pending_new_repo = new_repo_item;
					labelNewRepository.Visibility = Visibility.Visible;
					timer.Start();
				}

				listRepos.SelectedItem = new_repo_item;
			}
		}

		//-----------------------------------------------------------------------------
		private KilnRepoListItem MakeKilnRepoListItem(KilnRepo repo)
		{
			var group = groups_map[repo.ixRepoGroup];
			var project = projects_map[group.ixProject];
			
			var item = new KilnRepoListItem
			{
				GroupText = String.Format("{0}/{1}", project.sName, group.DisplayName),
				Repo = repo
			};

			return item;
		}

		//------------------------------------------------------------------
		private void DeleteRepository_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			var selected_item = listRepos.SelectedItem as KilnRepoListItem;
			e.CanExecute = selected_item != null && selected_item.Repo.sStatus == "good";
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DeleteRepository_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var selected_item = (KilnRepoListItem)listRepos.SelectedItem;
			var repo = selected_item.Repo;

			var msg = String.Format("Are you sure to delete remote repository '{0}' ?", repo.sName);

			var result = MessageBox.Show(msg, "Question", MessageBoxButton.OKCancel,
			                             MessageBoxImage.Question);

			if (result == MessageBoxResult.OK)
			{
				if (!Session.Instance.DeleteRepository(repo.ixRepo))
				{
					MessageBox.Show("An error occured while deleting a repository", "Error",
					                MessageBoxButton.OK, MessageBoxImage.Error);
				}
				else
				{
					MessageBox.Show(String.Format("Repository '{0}' has deleted", repo.sName), "Information", MessageBoxButton.OK, MessageBoxImage.Information);
					int idx = listRepos.SelectedIndex;
					repositories.Remove(selected_item);

					if (listRepos.Items.Count != 0)
					{
						listRepos.SelectedIndex = Math.Min(idx, listRepos.Items.Count - 1);
					}
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			timer.Stop();
		}
	}

	//-----------------------------------------------------------------------------
	internal class KilnRepoListItem
	{
		public string GroupText { get; set; }
		public KilnRepo Repo { get; set; }
	}
}
