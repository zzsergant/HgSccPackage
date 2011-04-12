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
using System.Collections.ObjectModel;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HgSccHelper.Misc;
using RestSharp.Extensions;

namespace HgSccHelper.BitBucket
{
	/// <summary>
	/// Interaction logic for RepositoriesWindow.xaml
	/// </summary>
	public partial class RepositoriesWindow : Window
	{
		private ObservableCollection<BitBucketRepo> repositories;
		public Uri RepositoryUri { get; private set; }

		//-----------------------------------------------------------------------------
		public static RoutedUICommand NewRepositoryCommand = new RoutedUICommand("New remote repository",
			"NewRepository", typeof(RepositoriesWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DeleteRepositoryCommand = new RoutedUICommand("Delete remote repository",
			"DeleteRepository", typeof(RepositoriesWindow));

		public const string CfgPath = @"BitBucket\GUI\RepositoriesWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		public RepositoriesWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			repositories = new ObservableCollection<BitBucketRepo>();
			this.DataContext = repositories;
		}

		//-----------------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			var repo_list = Util.GetRepositories(Credentials.Instance.Username,
			                                     Credentials.Instance.Password);

			btnSelect.IsEnabled = false;

			foreach (var repo in repo_list)
				repositories.Add(repo);
		}

		//-----------------------------------------------------------------------------
		private void Select_Click(object sender, RoutedEventArgs e)
		{
			var repo = (BitBucketRepo)listRepos.SelectedItem;
			if (repo == null)
				return;

			var uri_builder = new UriBuilder(Util.MakeRepoUrl(repo.Owner, repo.Slug));

			uri_builder.UserName = Credentials.Instance.Username.UrlEncode();
			uri_builder.Password = Credentials.Instance.Password.UrlEncode();
			
			RepositoryUri = uri_builder.Uri;
			DialogResult = true;
		}

		//-----------------------------------------------------------------------------
		private void listRepos_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var repo = (BitBucketRepo)listRepos.SelectedItem;
			if (repo == null)
				textSelectedRepo.Text = "";
			else
			{
				textSelectedRepo.Text = Util.MakeRepoUrl(repo.Owner, repo.Slug);
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
			var wnd = new NewRepositoryWindow();
			if (wnd.ShowDialog() == true)
			{
				var new_repo = Util.NewRepository(Credentials.Instance.Username,
				                                  Credentials.Instance.Password,
				                                  wnd.RepositoryName, wnd.IsPrivate);

				if (new_repo == null)
				{
					MessageBox.Show("An error occured while creating new repository", "Error",
					                MessageBoxButton.OK, MessageBoxImage.Error);
					return;
				}

				repositories.Add(new_repo);
				listRepos.SelectedItem = new_repo;
			}
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
			var repo = (BitBucketRepo)listRepos.SelectedItem;
			var msg = String.Format(
				"Are you sure to delete remote repository '{0}' ?", repo.Name);

			var result = MessageBox.Show(msg, "Question", MessageBoxButton.OKCancel,
			                             MessageBoxImage.Question);

			if (result == MessageBoxResult.OK)
			{
				if (!Util.DeleteRepository(Credentials.Instance.Username, Credentials.Instance.Password, repo.Slug))
				{
					MessageBox.Show("An error occured while deleting a repository", "Error",
					                MessageBoxButton.OK, MessageBoxImage.Error);
				}
				else
				{
					MessageBox.Show(String.Format("Repository '{0}' has deleted", repo.Name), "Information", MessageBoxButton.OK, MessageBoxImage.Information);
					int idx = listRepos.SelectedIndex;
					repositories.Remove(repo);

					if (listRepos.Items.Count != 0)
					{
						listRepos.SelectedIndex = Math.Min(idx, listRepos.Items.Count - 1);
					}
				}
			}
		}
	}
}
