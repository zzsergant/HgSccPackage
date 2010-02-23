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

using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Windows.Threading;
using System;
using System.Text;

namespace HgSccHelper
{
	//==================================================================
	public partial class MergeWindow : Window
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand StopCommand = new RoutedUICommand("Stop",
			"Stop", typeof(MergeWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand MergeCommand = new RoutedUICommand("Merge",
			"Merge", typeof(MergeWindow));


		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string TargetRevision { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		RevLogChangeDesc Target { get; set; }
		IdentifyInfo CurrentRevision { get; set; }
		HgThread worker;
		bool is_merge_successful;

		//------------------------------------------------------------------
		public MergeWindow()
		{
			InitializeComponent();

			UpdateContext = new UpdateContext();
			worker = new HgThread();
		}

		//------------------------------------------------------------------
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
				MessageBox.Show("There is allready active merge.\nYou should either commit it first or make a clean update",
					"Error", MessageBoxButton.OK, MessageBoxImage.Error);
				Close();
				return;
			}

			currentDesc.Text = Hg.GetRevisionDesc(WorkingDir, CurrentRevision.SHA1).GetDescription();

			Target = Hg.GetRevisionDesc(WorkingDir, TargetRevision);
			if (Target == null)
			{
				// error
				Close();
				return;
			}

			if (CurrentRevision.SHA1 == Target.SHA1)
			{
				MessageBox.Show("You can not merge to current revision", "Error",
					MessageBoxButton.OK, MessageBoxImage.Error);
				Close();
				return;
			}

			var hg_merge_tools = new HgMergeTools();
			var merge_tools = hg_merge_tools.GetMergeTools();
			var tools_list = new List<MergeToolComboItem>();
			
			tools_list.Add(new MergeToolComboItem
			{
				Description = "(Default)",
				Alias = ""
			});

			if (merge_tools.Count > 0)
			{
				foreach (var tool in merge_tools)
				{
					tools_list.Add(new MergeToolComboItem
					{
						Description = tool.Alias,
						Alias = tool.Alias
					});
				}
			}

			comboMergeTools.ItemsSource = tools_list;
			comboMergeTools.SelectedIndex = 0;
			targetDesc.Text = Hg.GetRevisionDesc(WorkingDir, Target.SHA1).GetDescription();
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
			if (StopCommand.CanExecute(sender, e.Source as IInputElement))
				StopCommand.Execute(sender, e.Source as IInputElement);

			worker.Dispose();
		}

		//------------------------------------------------------------------
		private void Close_Click(object sender, RoutedEventArgs e)
		{
			if (is_merge_successful)
				DialogResult = true;
			Close();
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}

		//------------------------------------------------------------------
		private void Merge_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.Handled = true;

			if (worker != null && !worker.IsBusy)
			{
				e.CanExecute = (Target != null) && (!UpdateContext.IsParentChanged);
			}
		}

		//------------------------------------------------------------------
		private void Merge_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			textBox.Text = "";

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.ErrorHandler = Error_Handler;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = WorkingDir;

			var builder = new StringBuilder();
			builder.Append("merge");

			bool force_merge = false;

			if (CurrentRevision.HaveUncommitedChanges)
			{
				var result = MessageBox.Show("There are uncommited changed.\nAre you sure to force merge ?",
					"Warning", MessageBoxButton.OKCancel, MessageBoxImage.Question);

				if (result != MessageBoxResult.OK)
					return;

				force_merge = true;
			}

			string merge_tool = "";

			if (radioAcceptLocal.IsChecked == true)
				merge_tool = "internal:local";

			if (radioAcceptOther.IsChecked == true)
				merge_tool = "internal:other";

			if (radioDoNotMerge.IsChecked == true)
				merge_tool = "internal:fail";

			if (radioMergeWith.IsChecked == true)
			{
				merge_tool = (comboMergeTools.SelectedItem as MergeToolComboItem).Alias;
			}

			if (merge_tool.Length > 0)
			{
				builder.Append(" --config ui.merge=" + merge_tool.Quote());
			}

			var options = force_merge ? HgMergeOptions.Force : HgMergeOptions.None;
			if ((options & HgMergeOptions.Force) == HgMergeOptions.Force)
				builder.Append(" -f");

			if (Target.SHA1.Length > 0)
				builder.Append(" -r " + Target.SHA1);

			p.Args = builder.ToString();

			Worker_NewMsg("[Merge started]\n");
			worker.Run(p);

			UpdateContext.IsParentChanged = true;
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

			switch (completed.Status)
			{
				case HgThreadStatus.Completed:
					{
						var msg = String.Format("[Operation completed. Exit code: {0}]", completed.ExitCode);
						Worker_NewMsg(msg);
						is_merge_successful = (completed.ExitCode == 0);
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
	}

	//------------------------------------------------------------------
	class MergeToolComboItem
	{
		public string Description { get; set; }
		public string Alias { get; set; }
	}
}
