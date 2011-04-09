using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
		public RepositoriesWindow()
		{
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

			uri_builder.UserName = HttpUtility.UrlEncode(Credentials.Instance.Username);
			uri_builder.Password = HttpUtility.UrlEncode(Credentials.Instance.Password);
			
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
	}
}
