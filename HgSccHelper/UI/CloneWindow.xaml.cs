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
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using HgSccHelper.UI;
using System.Web;
using RestSharp.Extensions;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for CloneWindow.xaml
	/// </summary>
	public partial class CloneWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty CloneToRevisionProperty =
			DependencyProperty.Register("CloneToRevision", typeof(bool), typeof(CloneWindow));

		//-----------------------------------------------------------------------------
		public bool CloneToRevision
		{
			get { return (bool)this.GetValue(CloneToRevisionProperty); }
			set { this.SetValue(CloneToRevisionProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty NoUpdateProperty =
			DependencyProperty.Register("NoUpdate", typeof(bool), typeof(CloneWindow));

		//-----------------------------------------------------------------------------
		public bool NoUpdate
		{
			get { return (bool)this.GetValue(NoUpdateProperty); }
			set { this.SetValue(NoUpdateProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty UsePullProtocolProperty =
			DependencyProperty.Register("UsePullProtocol", typeof(bool), typeof(CloneWindow));

		//-----------------------------------------------------------------------------
		public bool UsePullProtocol
		{
			get { return (bool)this.GetValue(UsePullProtocolProperty); }
			set { this.SetValue(UsePullProtocolProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public static readonly DependencyProperty UseUncompressedTransferProperty =
			DependencyProperty.Register("UseUncompressedTransfer", typeof(bool), typeof(CloneWindow));

		//-----------------------------------------------------------------------------
		public bool UseUncompressedTransfer
		{
			get { return (bool)this.GetValue(UseUncompressedTransferProperty); }
			set { this.SetValue(UseUncompressedTransferProperty, value); }
		}

		//-----------------------------------------------------------------------------
		public string SourcePath { get; set; }

		//-----------------------------------------------------------------------------
		public CloneResult CloneResult { get; private set; }

		//------------------------------------------------------------------
		HgThread worker;

		private CloneResult pending_clone;

		public const string CfgPath = @"GUI\CloneWindow";
		CfgWindowPosition wnd_cfg;

		//-----------------------------------------------------------------------------
		private AsyncOperations async_ops;

		//-----------------------------------------------------------------------------
		private Cursor prev_cursor;

		//------------------------------------------------------------------
		public CloneWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this);

			InitializeComponent();

			worker = new HgThread();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			if (!String.IsNullOrEmpty(SourcePath))
				Title = string.Format("Clone: '{0}'", SourcePath);
			else
				Title = "Clone";

			textUsername.IsEnabled = false;
			passwordBox.IsEnabled = false;

			if (!String.IsNullOrEmpty(SourcePath))
			{
				textSourcePath.Text = SourcePath;
				textDestPath.Text = SourcePath;

				textSourcePath.SelectAll();
				textDestPath.SelectAll();
			}

			textSourcePath.Focus();
		}

		//------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			worker.Cancel();
			worker.Dispose();
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

		//------------------------------------------------------------------
		private void Clone_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.Handled = true;

			if (worker != null && !worker.IsBusy)
			{
				if (	String.IsNullOrEmpty(textSourcePath.Text)
					||	String.IsNullOrEmpty(textDestPath.Text)
					)
				{
					return;
				}

				// TODO: Make some more checks
				if (textSourcePath.Text == textDestPath.Text)
					return;

				if (CloneToRevision)
				{
					if (String.IsNullOrEmpty(textRevision.Text))
						return;
				}

				e.CanExecute = true;
			}
		}

		//------------------------------------------------------------------
		private void Clone_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			textBox.Text = "";
			Worker_NewMsg("[Clone started]\n");

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = "";
			
			var builder = new HgArgsBuilder();
			builder.AppendVerbose();
			builder.Append("clone");

			if (CloneToRevision)
			{
				builder.AppendRevision(textRevision.Text);
			}

			if (NoUpdate)
				builder.Append("--noupdate");
	
			if (UsePullProtocol)
				builder.Append("--pull");

			if (UseUncompressedTransfer)
				builder.Append("--uncompressed");

			var source_path = textSourcePath.Text;
			if (Util.IsValidRemoteUrl(source_path))
			{
				try
				{
					var uri_builder = new UriBuilder(source_path);
					uri_builder.UserName = textUsername.Text.UrlEncode();
					uri_builder.Password = passwordBox.Password.UrlEncode();
					source_path = uri_builder.Uri.AbsoluteUri;
				}
				catch (UriFormatException)
				{
				}
			}

			builder.AppendPath(source_path);
			builder.AppendPath(textDestPath.Text);

			pending_clone = new CloneResult
			                	{Source = source_path, Destination = textDestPath.Text};

			p.Args = builder.ToString();

			RunningOperations |= AsyncOperations.Clone;
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
			RunningOperations &= ~AsyncOperations.Clone;
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
			RunningOperations &= ~AsyncOperations.Clone;
			Worker_NewMsg("");

			switch(completed.Status)
			{
				case HgThreadStatus.Completed:
					{
						var msg = String.Format("[Operation completed. Exit code: {0}]", completed.ExitCode);
						Worker_NewMsg(msg);

						CloneResult = pending_clone;
						pending_clone = null;
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

			pending_clone = null;

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
		private void sourceBrowse_Click(object sender, RoutedEventArgs e)
		{
			using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
			{
				dlg.Description = "Browse for Source Repository...";
				dlg.ShowNewFolderButton = false;

				if (!String.IsNullOrEmpty(textSourcePath.Text))
					dlg.SelectedPath = textSourcePath.Text;
				
				var result = dlg.ShowDialog();
				if (result == System.Windows.Forms.DialogResult.OK)
				{
					// TODO: Check if there is an actual repository here
					textSourcePath.Text = dlg.SelectedPath;
				}
			}
		}

		//------------------------------------------------------------------
		private void destBrowse_Click(object sender, RoutedEventArgs e)
		{
			using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
			{
				dlg.Description = "Browse for Destination Path...";
				dlg.ShowNewFolderButton = true;

				if (!String.IsNullOrEmpty(textDestPath.Text))
					dlg.SelectedPath = textDestPath.Text;

				var result = dlg.ShowDialog();
				if (result == System.Windows.Forms.DialogResult.OK)
				{
					// TODO: Check if there is not a repository here
					textDestPath.Text = dlg.SelectedPath;
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
		private void btnClose_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void textSourcePath_TextChanged(object sender, TextChangedEventArgs e)
		{
			var url = textSourcePath.Text;
			if (Util.IsValidRemoteUrl(url))
			{
				try
				{
					var builder = new UriBuilder(url);
					textUsername.Text = builder.UserName.UrlDecode();

					if (	!String.IsNullOrEmpty(builder.Password)
						&&	builder.Password != "***"
						)
					{
						passwordBox.Password = builder.Password.UrlDecode();
						textSourcePath.Text = Util.RemoveUrlPassword(url);
					}

					textUsername.IsEnabled = true;
					passwordBox.IsEnabled = true;
					return;
				}
				catch (UriFormatException)
				{
				}
			}

			textUsername.IsEnabled = false;
			passwordBox.IsEnabled = false;
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
			textSourcePath.Text = Util.RemoveUrlPassword(builder.Uri.AbsoluteUri);
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
			textSourcePath.Text = Util.RemoveUrlPassword(builder.Uri.AbsoluteUri);
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
			dlg.InitialDirectory = SourcePath;
			dlg.Filter = String.Format("{0}|*{1}", "Mercurial bundle files (*.hg)", ".hg");
			dlg.Title = "Browse for mercurial bundle file...";
			dlg.RestoreDirectory = true;

			var result = dlg.ShowDialog(this);
			if (result == true)
			{
				textSourcePath.Text = dlg.FileName;
			}
		}
	}

	//=============================================================================
	public class CloneResult
	{
		public string Source { get; set; }
		public string Destination { get; set; }
	}
}
