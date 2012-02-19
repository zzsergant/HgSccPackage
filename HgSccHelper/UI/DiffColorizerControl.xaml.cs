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
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Threading;
using HgSccHelper.CommandServer;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace HgSccHelper.UI
{
	/// <summary>
	/// Interaction logic for DiffColorizerControl.xaml
	/// </summary>
	public partial class DiffColorizerControl : IDisposable
	{
		//-----------------------------------------------------------------------------
		private readonly List<string> lines;

		private Encoding lines_encoding;

		//-----------------------------------------------------------------------------
		public Action<List<string>> Complete { get; set; }

		//-----------------------------------------------------------------------------
		ObservableCollection<EncodingItem> encodings;

		//-----------------------------------------------------------------------------
		private bool disposed;

		//-----------------------------------------------------------------------------
		private PendingDiffArgs pending_diff;

		//-----------------------------------------------------------------------------
		private DispatcherTimer timer;

		//-----------------------------------------------------------------------------
		public const string CfgPath = @"GUI\Diff";

		//-----------------------------------------------------------------------------
		public const string DiffVisible = "DiffVisible";

		//-----------------------------------------------------------------------------
		public const string DiffWidth = "DiffWidth";

		//------------------------------------------------------------------
		public const int DefaultWidth = 550;

		//-----------------------------------------------------------------------------
		static DiffColorizerControl()
		{
			var rule_set = HighlightingManager.Instance.GetDefinition("Patch");

			// Redefining colors

			rule_set.GetNamedColor("AddedText").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Added));
			rule_set.GetNamedColor("RemovedText").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Removed));
			rule_set.GetNamedColor("UnchangedText").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.None));
			rule_set.GetNamedColor("Position").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Patch));
			rule_set.GetNamedColor("Header").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.Header));
			rule_set.GetNamedColor("FileName").Foreground = new SimpleHighlightingBrush(DiffTypeColor(DiffType.DiffHeader));
		}

		//-----------------------------------------------------------------------------
		public DiffColorizerControl()
		{
			InitializeComponent();

			richTextBox.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Patch");
			richTextBox.IsReadOnly = true;

			encodings = new ObservableCollection<EncodingItem>();
			encodings.Add(new EncodingItem { Name = "Ansi", Encoding = Encoding.Default });
			encodings.Add(new EncodingItem { Name = "Utf8", Encoding = Encoding.UTF8 });
			comboEncodings.ItemsSource = encodings;

			lines = new List<string>();
			lines_encoding = Encoding.Default;

			timer = new DispatcherTimer(DispatcherPriority.Background);
			timer.Interval = TimeSpan.FromMilliseconds(30);
			timer.Tick += TimerOnTick;
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
			timer.Stop();
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
		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			string encoding_name;
			Cfg.Get(DiffColorizerControl.CfgPath, "encoding", out encoding_name, encodings[0].Name);

			var encoding = encodings.First(enc => enc.Name == encoding_name);
			if (encoding != null)
				comboEncodings.SelectedItem = encoding;
			else
				comboEncodings.SelectedIndex = 0;
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (!disposed)
			{
				disposed = true;

				timer.Stop();
				timer.Tick -= TimerOnTick;

				var encoding = comboEncodings.SelectedItem as EncodingItem;
				if (encoding != null)
					Cfg.Set(DiffColorizerControl.CfgPath, "encoding", encoding.Name);
			}
		}

		//-----------------------------------------------------------------------------
		private void TimerOnTick(object sender, EventArgs event_args)
		{
			timer.Stop();

			var args = new HgArgsBuilderZero();
			args.Append("diff");

			if (!string.IsNullOrEmpty(pending_diff.Rev1))
				args.AppendRevision(pending_diff.Rev1);

			if (!string.IsNullOrEmpty(pending_diff.Rev2))
				args.AppendRevision(pending_diff.Rev2);

			args.AppendPath(pending_diff.Path);

			lines.Clear();
			lines_encoding = pending_diff.HgClient.Encoding;

			using (var mem_stream = new MemoryStream())
			{
				int res = pending_diff.HgClient.RawCommandStream(args, mem_stream);
				if (res == 0)
				{
					mem_stream.Seek(0, SeekOrigin.Begin);

					using (var output_stream = new StreamReader(mem_stream, pending_diff.HgClient.Encoding))
					{
						while (true)
						{
							var str = output_stream.ReadLine();
							if (str == null)
								break;

							lines.Add(str);
						}
					}
				}
			}

			WriteLinesToColorizer();

			if (Complete != null)
				Complete(null);
		}

		//-----------------------------------------------------------------------------
		private void UserControl_Unloaded(object sender, RoutedEventArgs e)
		{
			Dispose();
		}

		//-----------------------------------------------------------------------------
		public void RunHgDiffAsync(HgClient client, string path, string rev)
		{
			RunHgDiffAsync(client, path, rev, "");
		}

		//-----------------------------------------------------------------------------
		public void RunHgDiffAsync(HgClient client, string path, string rev1, string rev2)
		{
			timer.Stop();
			Clear();
			pending_diff = new PendingDiffArgs
			{
				HgClient = client,
				Path = path,
				Rev1 = rev1,
				Rev2 = rev2
			};
			timer.Start();
		}

		//-----------------------------------------------------------------------------
		void WriteLinesToColorizer()
		{
			var encoding = comboEncodings.SelectedItem as EncodingItem;

			richTextBox.Clear();
			foreach (var line in lines)
			{
				// FIXME: Respect hg client encoding
				var text_line = line;
				if (encoding != null && encoding.Encoding != lines_encoding)
					text_line = Util.Convert(line, encoding.Encoding, lines_encoding);

				richTextBox.AppendText(text_line + "\n");
			}
		}

		//------------------------------------------------------------------
		private void comboEncodings_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var encoding = comboEncodings.SelectedItem as EncodingItem;

			if (encoding != null)
			{
				if (!IsWorking)
				{
					WriteLinesToColorizer();
				}
			}
		}


		//-----------------------------------------------------------------------------
		public bool IsWorking { get { return timer.IsEnabled; } }

		//-----------------------------------------------------------------------------
		private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (richTextBox != null && !string.IsNullOrEmpty(richTextBox.SelectedText))
				e.CanExecute = true;
			e.Handled = true;
		}

		//-----------------------------------------------------------------------------
		private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			richTextBox.Copy();
		}
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
		public HgClient HgClient { get; set; }
		public string Path { get; set; }
		public string Rev1 { get; set; }
		public string Rev2 { get; set; }
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
