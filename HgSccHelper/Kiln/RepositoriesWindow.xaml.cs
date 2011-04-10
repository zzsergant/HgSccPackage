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
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void NewRepository_Executed(object sender, ExecutedRoutedEventArgs e)
		{
		}

		//------------------------------------------------------------------
		private void DeleteRepository_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = listRepos.SelectedItem != null;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DeleteRepository_Executed(object sender, ExecutedRoutedEventArgs e)
		{
		}
	}

	//-----------------------------------------------------------------------------
	internal class KilnRepoListItem
	{
		public string GroupText { get; set; }
/*
		public string ProjectName { get; set; }
		public string GroupName { get; set; }
*/
		public KilnRepo Repo { get; set; }
	}
}
