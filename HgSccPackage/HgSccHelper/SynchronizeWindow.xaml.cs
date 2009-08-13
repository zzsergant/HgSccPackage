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
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Data;
using HgSccPackage.Tools;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for SynchronizeWindow.xaml
	/// </summary>
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
		
		//------------------------------------------------------------------
		public SynchronizeWindow()
		{
			InitializeComponent();
		}

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		Hg Hg { get; set; }

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
 			Title = string.Format("Synchronize: '{0}'", WorkingDir);
			Hg = new Hg();

			var paragraph = new Paragraph();
			paragraph.TextIndent = 0;

			var run = new Run("Some Text");
			paragraph.Inlines.Add(run);

			paragraph.Inlines.Add(new LineBreak());

			var red = new Run("Red color");
			red.Foreground = Brushes.Red;
			paragraph.Inlines.Add(red);

			richTextBox.Document.Blocks.Clear();
			richTextBox.Document.Blocks.Add(paragraph);
		}

		//------------------------------------------------------------------
		private void Window_Unloaded(object sender, RoutedEventArgs e)
		{
		}

		//------------------------------------------------------------------
		private void Incoming_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void Incoming_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			richTextBox.Document.Blocks.Clear();
			var paragraph = new Paragraph();
			var run = new Run("Incoming\n");
			paragraph.Inlines.Add(run);
			richTextBox.Document.Blocks.Add(paragraph);

			var args = new StringBuilder();
			args.Append("incoming");

			using (Process proc = Process.Start(Hg.PrepareProcess(WorkingDir, args.ToString())))
			{
				var reader = proc.StandardOutput;
				while (true)
				{
					string str = reader.ReadLine();
					if (str == null)
						break;

					paragraph.Inlines.Add(new Run(str));
					paragraph.Inlines.Add(new LineBreak());
				}

				var error = proc.StandardError.ReadToEnd();
				if (!string.IsNullOrEmpty(error))
				{
					var error_msg = new Run();
					error_msg.Foreground = Brushes.Red;
					paragraph.Inlines.Add(error_msg);
				}
				
				proc.WaitForExit();
			}

			e.Handled = true;
		}
	}
}
