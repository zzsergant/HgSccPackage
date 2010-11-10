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
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HgSccHelper.UI
{
	/// <summary>
	/// Interaction logic for DiffColorizerControl.xaml
	/// </summary>
	public partial class DiffColorizerControl
	{
		private readonly Dictionary<DiffType, Brush> type_brushes;

		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private readonly List<string> lines;

		//-----------------------------------------------------------------------------
		private int diff_start_index;

		private PendingDiffArgs pending_diff;

		//-----------------------------------------------------------------------------
		public DiffColorizerControl()
		{
			InitializeComponent();

			type_brushes = new Dictionary<DiffType, Brush>();
			foreach (DiffType type in Enum.GetValues(typeof(DiffType)))
			{
				var brush = new SolidColorBrush(DiffTypeColor(type));
				brush.Freeze();
				type_brushes[type] = brush;
			}

			richTextBox.Document.PageWidth = 1000;
			worker = new HgThread();
			
			lines = new List<string>();
			diff_start_index = 0;
		}

		//-----------------------------------------------------------------------------
		public void Clear()
		{
			Cancel();

			var all = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
			all.Text = "";

			diff_start_index = 0;
			lines.Clear();
		}

		//-----------------------------------------------------------------------------
		public void Cancel()
		{
			if (worker.IsBusy)
				worker.Cancel();
		}

		//-----------------------------------------------------------------------------
		public void SetDiffLines(IEnumerable<string> lines)
		{
			Clear();

			foreach (var line in lines)
			{
				AddDiffLine(line);
			}
		}

		//-----------------------------------------------------------------------------
		private static Color DiffTypeColor(DiffType type)
		{
			switch (type)
			{
				case DiffType.None:
					return Colors.Black;
				case DiffType.DiffHeader:
					return Colors.Maroon;
				case DiffType.Header:
					return Colors.DarkKhaki;
				case DiffType.Added:
					return Colors.Blue;
				case DiffType.Removed:
					return Colors.Red;
				case DiffType.Changed:
					return Colors.Violet;
				case DiffType.Patch:
					return Colors.Green;
				case DiffType.Info:
					return Colors.LightGreen;
				default:
					throw new ArgumentOutOfRangeException("type");
			}
		}

		//-----------------------------------------------------------------------------
		private static DiffType DiffLineType(int line_index, string text)
		{
			if (text.StartsWith("diff"))
				return DiffType.DiffHeader;
			
			if (text.StartsWith("!", StringComparison.Ordinal))
				return DiffType.Changed;
			
			if (text.StartsWith("---", StringComparison.Ordinal))
				return DiffType.Header;
			
			if (text.StartsWith("-", StringComparison.Ordinal))
				return DiffType.Removed;

			if (text.StartsWith("<", StringComparison.Ordinal))
				return DiffType.Removed;

			if (text.StartsWith("@@", StringComparison.Ordinal))
				return DiffType.Patch;

			if (text.StartsWith("+++", StringComparison.Ordinal))
				return DiffType.Header;

			if (text.StartsWith("+", StringComparison.Ordinal))
				return DiffType.Added;

			if (text.StartsWith(">", StringComparison.Ordinal))
				return DiffType.Added;

			if (text.StartsWith("***", StringComparison.Ordinal))
			{
				if (line_index < 2)
					return DiffType.Header;

				return DiffType.Info;
			}
			
			if (text.Length > 0 && !char.IsWhiteSpace(text[0]))
				return DiffType.Info;

			return DiffType.None;
		}

		//-----------------------------------------------------------------------------
		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
			worker.Cancel();
			worker.Dispose();
		}

		//-----------------------------------------------------------------------------
		public void RunHgDiffAsync(string work_dir, string path, string rev)
		{
			RunHgDiffAsync(work_dir, path, rev, "");
		}

		//-----------------------------------------------------------------------------
		public void RunHgDiffAsync(string work_dir, string path, string rev1, string rev2)
		{
			Clear();

			var args = new HgArgsBuilder();
			args.Append("diff");
			args.AppendRevision(rev1);
			
			if (!string.IsNullOrEmpty(rev2))
				args.AppendRevision(rev2);

			args.AppendPath(path);

			RunHgDiffThread(work_dir, args.ToString());
		}

		//------------------------------------------------------------------
		private void RunHgDiffThread(string work_dir, string args)
		{
			if (worker.IsBusy)
			{
				pending_diff = new PendingDiffArgs {WorkingDir = work_dir, Args = args};
				return;
			}

			var p = new HgThreadParams();
			p.CompleteHandler = Worker_Completed;
			p.OutputHandler = Output_Handler;
			p.WorkingDir = work_dir;
			p.Args = args;

			worker.Run(p);
		}
		//------------------------------------------------------------------
		void Output_Handler(string msg)
		{
			if (!worker.CancellationPending)
			{
				Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,
					new Action<string>(AddDiffLine), msg);
			}
		}

		//------------------------------------------------------------------
		void AddDiffLine(string line)
		{
			if (worker.CancellationPending)
				return;

			var type = DiffLineType(lines.Count - diff_start_index, line);
			if (type == DiffType.DiffHeader)
				diff_start_index = lines.Count;

			lines.Add(line);

			line = line.Replace("\t", "    ");

			var range = new TextRange(richTextBox.Document.ContentEnd, richTextBox.Document.ContentEnd);
			range.Text = line;
			range.End.InsertLineBreak();
			range.ApplyPropertyValue(TextElement.ForegroundProperty, type_brushes[type]);
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			// Updating commands state (CanExecute)
			CommandManager.InvalidateRequerySuggested();

			if (pending_diff != null)
			{
				var p = pending_diff;
				pending_diff = null;

				Clear();
				RunHgDiffThread(p.WorkingDir, p.Args);
			}
		}

		//-----------------------------------------------------------------------------
		public bool IsWorking { get { return worker.IsBusy; } }
	}

	//-----------------------------------------------------------------------------
	enum DiffType
	{
		None,
		DiffHeader,
		Header,
		Added,
		Removed,
		Changed,
		Patch,
		Info
	}

	internal class PendingDiffArgs
	{
		public string WorkingDir { get; set; }
		public string Args { get; set; }
	}
}
