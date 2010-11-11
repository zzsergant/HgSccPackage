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
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace HgSccHelper.UI
{
	/// <summary>
	/// Interaction logic for DiffColorizerControl.xaml
	/// </summary>
	public partial class DiffColorizerControl
	{
		//------------------------------------------------------------------
		readonly HgThread worker;

		//-----------------------------------------------------------------------------
		private readonly List<string> lines;

		//-----------------------------------------------------------------------------
		private PendingDiffArgs pending_diff;

		//-----------------------------------------------------------------------------
		public DiffColorizerControl()
		{
			InitializeComponent();

			var rule_set = HighlightingManager.Instance.GetDefinition("Patch");

			// Redefining colors

			rule_set.GetNamedColor("AddedText").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Added));
			rule_set.GetNamedColor("RemovedText").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Removed));
			rule_set.GetNamedColor("UnchangedText").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.None));
			rule_set.GetNamedColor("Position").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Patch));
			rule_set.GetNamedColor("Header").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Header));
			rule_set.GetNamedColor("FileName").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.DiffHeader));

			richTextBox.SyntaxHighlighting = rule_set;

			worker = new HgThread();
			lines = new List<string>();
		}

		//-----------------------------------------------------------------------------
		public void Clear()
		{
			Cancel();

			richTextBox.Text = "";
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
				richTextBox.AppendText(line + "\n");
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
				lines.Add(msg);
			}
		}

		//------------------------------------------------------------------
		void Worker_Completed(HgThreadResult completed)
		{
			if (!worker.CancellationPending)
			{
				foreach (var line in lines)
					richTextBox.AppendText(line + "\n");
			}

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

	//-----------------------------------------------------------------------------
	internal class PendingDiffArgs
	{
		public string WorkingDir { get; set; }
		public string Args { get; set; }
	}

	//-----------------------------------------------------------------------------
	/// <summary>
	/// Highlighting brush implementation that takes a frozen brush.
	/// </summary>
	[Serializable]
	sealed class SimpleHighlightingBrush : HighlightingBrush, ISerializable
	{
		readonly SolidColorBrush brush;

		public SimpleHighlightingBrush(SolidColorBrush brush)
		{
			brush.Freeze();
			this.brush = brush;
		}

		public SimpleHighlightingBrush(Color color) : this(new SolidColorBrush(color)) { }

		public override Brush GetBrush(ITextRunConstructionContext context)
		{
			return brush;
		}

		public override string ToString()
		{
			return brush.ToString();
		}

		SimpleHighlightingBrush(SerializationInfo info, StreamingContext context)
		{
			this.brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(info.GetString("color")));
			brush.Freeze();
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("color", brush.Color.ToString(CultureInfo.InvariantCulture));
		}
	}

	/// <summary>
	/// HighlightingBrush implementation that finds a brush using a resource.
	/// </summary>
	[Serializable]
	sealed class SystemColorHighlightingBrush : HighlightingBrush, ISerializable
	{
		readonly PropertyInfo property;

		public SystemColorHighlightingBrush(PropertyInfo property)
		{
			Debug.Assert(property.ReflectedType == typeof(SystemColors));
			Debug.Assert(typeof(Brush).IsAssignableFrom(property.PropertyType));
			this.property = property;
		}

		public override Brush GetBrush(ITextRunConstructionContext context)
		{
			return (Brush)property.GetValue(null, null);
		}

		public override string ToString()
		{
			return property.Name;
		}

		SystemColorHighlightingBrush(SerializationInfo info, StreamingContext context)
		{
			property = typeof(SystemColors).GetProperty(info.GetString("propertyName"));
			if (property == null)
				throw new ArgumentException("Error deserializing SystemColorHighlightingBrush");
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("propertyName", property.Name);
		}
	}
}
