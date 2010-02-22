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
		public static RoutedUICommand StopCommand = new RoutedUICommand("Stop",
			"Stop", typeof(CloneWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand CloneCommand = new RoutedUICommand("Clone",
			"Clone", typeof(CloneWindow));

		//-----------------------------------------------------------------------------
		public string SourcePath { get; set; }

		//------------------------------------------------------------------
		HgThread worker;

		//------------------------------------------------------------------
		public CloneWindow()
		{
			InitializeComponent();

			worker = new HgThread();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
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
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			if (StopCommand.CanExecute(sender, e.Source as IInputElement))
				StopCommand.Execute(sender, e.Source as IInputElement);

			worker.Dispose();
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
			
			var builder = new StringBuilder();
			builder.Append("-v clone");

			if (CloneToRevision)
			{
				builder.Append(" --rev " + textRevision.Text);
			}

			if (NoUpdate)
				builder.Append(" --noupdate");
	
			if (UsePullProtocol)
				builder.Append(" --pull");

			if (UseUncompressedTransfer)
				builder.Append(" --uncompressed");

			var source_path = textSourcePath.Text;
			if (Util.IsValidRemoteUrl(source_path))
			{
				try
				{
					var uri_builder = new UriBuilder(source_path);
					uri_builder.UserName = textUsername.Text;
					uri_builder.Password = passwordBox.Password;
					source_path = uri_builder.Uri.AbsoluteUri;
				}
				catch (UriFormatException)
				{
				}
			}

			builder.Append(" " + source_path.Quote());
			builder.Append(" " + textDestPath.Text.Quote());

			p.Args = builder.ToString();

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
			Worker_NewMsg("");

			switch(completed.Status)
			{
				case HgThreadStatus.Completed:
					{
						var msg = String.Format("[Operation completed. Exit code: {0}]", completed.ExitCode);
						Worker_NewMsg(msg);
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
		private void btnCancel_Click(object sender, RoutedEventArgs e)
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
					textUsername.Text = builder.UserName;

					if (!String.IsNullOrEmpty(builder.Password))
						passwordBox.Password = builder.Password;

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
	}
}
