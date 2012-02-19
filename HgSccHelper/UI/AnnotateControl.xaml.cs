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

using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Collections.Generic;
using System;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using HgSccHelper.CommandServer;
using HgSccHelper.UI;
using HgSccHelper.UI.RevLog;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;

namespace HgSccHelper
{
	public partial class AnnotateControl : IDisposable
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand GotoLineCommand = new RoutedUICommand("Goto Line",
			"GotoLine", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand NextChangeCommand = new RoutedUICommand("Next Change",
			"NextChange", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand PrevChangeCommand = new RoutedUICommand("Prev Change",
			"PrevChange", typeof(AnnotateControl));

		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string FileName { get; set; }

		//------------------------------------------------------------------
		public string Rev { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		HgClient HgClient { get { return UpdateContext.Cache.HgClient; } }

		//------------------------------------------------------------------
		ParentsInfo ParentsInfo { get; set; }

		//------------------------------------------------------------------
		SelectedParentFile SelectedParentFile { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// SHA1 -> BranchInfo map
		/// </summary>
		Dictionary<string, BranchInfo> Branches { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// Tag Name -> TagInfo map
		/// </summary>
		Dictionary<string, TagInfo> Tags { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// Bookmark Name -> BookmarkInfo map
		/// </summary>
		Dictionary<string, BookmarkInfo> Bookmarks { get; set; }

		//------------------------------------------------------------------
		/// <summary>
		/// SHA1 -> FileHistoryInfo2 map
		/// </summary>
		Dictionary<string, FileHistoryInfo2> file_history_map;

		List<AnnotateLineView> annotated_lines;

		Dictionary<int, int> rev_to_change_idx_map;
		Dictionary<int, List<AnnotateLineView>> rev_to_line_view;

		Dictionary<ListView, GridViewColumnSorter> files_sorter;

		//-----------------------------------------------------------------------------
		private bool disposed;

		//-----------------------------------------------------------------------------
		private AsyncOperations async_ops;

		//-----------------------------------------------------------------------------
		private Cursor prev_cursor;

		private AsyncAnnotate async_annotate;
		private DispatcherTimer timer;

		private ColorizeChanges colorizer;
		private ObservableCollection<EncodingItem> encodings;
		private ObservableCollection<IHighlightingDefinition> highlightings;

		static AnnotateControl()
		{
			try
			{
				using (var stream = new MemoryStream(HgSccHelper.Properties.Resources.Python))
				{
					var highlight = HighlightingLoader.Load(XmlReader.Create(stream), HighlightingManager.Instance);
					if (highlight != null)
						HighlightingManager.Instance.RegisterHighlighting("Python", new []{".py"}, highlight);
				}
			}
			catch(Exception e)
			{
				Logger.WriteLine("AnnotateControl Error: {0}", e.Message);
			}
		}

		//------------------------------------------------------------------
		public AnnotateControl()
		{
			InitializeComponent();

			UpdateContext = new UpdateContext();
			file_history_map = new Dictionary<string, FileHistoryInfo2>();

			rev_to_change_idx_map = new Dictionary<int, int>();
			rev_to_line_view = new Dictionary<int, List<AnnotateLineView>>();

			files_sorter = new Dictionary<ListView, GridViewColumnSorter>();

			diffColorizer.Complete = new Action<List<string>>(OnDiffColorizer);

			async_annotate = new AsyncAnnotate();
			async_annotate.Complete = new Action<AsyncAnnotateResults>(OnAsyncAnnotate);

			textEditor.IsReadOnly = true;
			textEditor.ShowLineNumbers = true;

			encodings = new ObservableCollection<EncodingItem>();
			encodings.Add(new EncodingItem { Name = "Ansi", Encoding = Encoding.Default });
			encodings.Add(new EncodingItem { Name = "Utf8", Encoding = Encoding.UTF8 });
			comboEncodings.ItemsSource = encodings;

			var defs = HighlightingManager.Instance.HighlightingDefinitions;
			highlightings = new ObservableCollection<IHighlightingDefinition>(
				defs.OrderBy(h => h.Name));

			comboHighlighting.ItemsSource = highlightings;

			timer = new DispatcherTimer();
			timer.Interval = TimeSpan.FromMilliseconds(50);
			timer.Tick += TimerOnTick;
		}

		//------------------------------------------------------------------
		void Caret_PositionChanged(object sender, EventArgs e)
		{
			UpdateLineSelection(textEditor.TextArea.Caret.Line);
		}

		//------------------------------------------------------------------
		void UpdateLineSelection(int editor_line)
		{
			if (annotated_lines.Count > 0)
			{
				if (editor_line > annotated_lines.Count)
					return;

				if (colorizer != null)
					colorizer.CurrentLine = editor_line - 1;

				if (!annotated_lines[editor_line - 1].IsSelected)
				{
					foreach (var line in annotated_lines)
					{
						if (line.IsSelected)
							line.IsSelected = false;
					}

					var annotate_line = annotated_lines[editor_line - 1];
					if (annotate_line != null)
					{
						foreach (var line_view in rev_to_line_view[annotate_line.Info.Rev])
							line_view.IsSelected = true;

						int idx;
						if (rev_to_change_idx_map.TryGetValue(annotate_line.Info.Rev, out idx))
						{
							listChanges.SelectedIndex = idx;
							listChanges.ScrollIntoView(listChanges.SelectedItem);
						}
					}
				}
			}
			textEditor.TextArea.TextView.Redraw();
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

		//-----------------------------------------------------------------------------
		private void OnDiffColorizer(List<string> obj)
		{
			RunningOperations &= ~AsyncOperations.Diff;
		}

		//------------------------------------------------------------------
		public GridView ListChangesGrid
		{
			get { return listChangesGrid; }
		}

		//------------------------------------------------------------------
		private void Control_Loaded(object sender, RoutedEventArgs e)
		{
			string encoding_name;
			Cfg.Get(AnnotateWindow.CfgPath, "encoding", out encoding_name, encodings[0].Name);

			var encoding = encodings.First(enc => enc.Name == encoding_name);
			if (encoding != null)
				comboEncodings.SelectedItem = encoding;
			else
				comboEncodings.SelectedIndex = 0;

			int diff_width;
			Cfg.Get(AnnotateWindow.CfgPath, DiffColorizerControl.DiffWidth, out diff_width, DiffColorizerControl.DefaultWidth);
			diffColorizer.Width = diff_width;

			int diff_visible;
			Cfg.Get(AnnotateWindow.CfgPath, DiffColorizerControl.DiffVisible, out diff_visible, 1);
			expanderDiff.IsExpanded = (diff_visible != 0);

			int changes_height;
			Cfg.Get(AnnotateWindow.CfgPath, "ChangesHeight", out changes_height, 150);
			changesRow.Height = new GridLength(changes_height, GridUnitType.Pixel);

			int files_height;
			Cfg.Get(AnnotateWindow.CfgPath, "FilesHeight", out files_height, 200);
			gridFiles.Height = files_height;

			int files_visible;
			Cfg.Get(AnnotateWindow.CfgPath, "FilesVisible", out files_visible, 0);
			viewFilesExpander.IsExpanded = (files_visible != 0);

			Tags = new Dictionary<string, TagInfo>();
			Branches = new Dictionary<string, BranchInfo>();
			Bookmarks = new Dictionary<string, BookmarkInfo>();

			var files = HgClient.Status(FileName, Rev ?? "");
			if (files.Count == 1
				&& files[0].Status == HgFileStatus.Added
				&& files[0].CopiedFrom != null)
			{
				var file_info = files[0];
				FileName = file_info.CopiedFrom;
			}

			RunningOperations |= AsyncOperations.Annotate;
			async_annotate.RunAsync(WorkingDir, FileName, Rev ?? "");
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncAnnotate(AsyncAnnotateResults results)
		{
			RunningOperations &= ~AsyncOperations.Annotate;

			if (results == null)
				return;

			if (results.IsBinary)
			{
				MessageBox.Show("Unable to annotate binary file", "Error",
				                MessageBoxButton.OK, MessageBoxImage.Error);

				return;
			}

			var lines_info = results.Lines;
			if (lines_info.Count == 0)
				return;

			annotated_lines = new List<AnnotateLineView>();
			foreach (var line_info in lines_info)
			{
				var line_view = new AnnotateLineView();
				line_view.Info = line_info;

				annotated_lines.Add(line_view);

				List<AnnotateLineView> rev_lines;
				if (!rev_to_line_view.TryGetValue(line_view.Info.Rev, out rev_lines))
				{
					rev_lines = new List<AnnotateLineView>();
					rev_to_line_view[line_view.Info.Rev] = rev_lines;
				}

				rev_lines.Add(line_view);
			}

			SetLines();
			SetSyntaxHighlighting();

			var rev_range = "";
			if (!string.IsNullOrEmpty(Rev))
			{
				rev_range = String.Format("reverse(::{0})", Rev);
			}

			var rename_parts = FileHistoryWindow.TrackRenames(HgClient, FileName, rev_range);
			OnAsyncChangeDescFull(rename_parts);
		}

		//------------------------------------------------------------------
		private void SetSyntaxHighlighting()
		{
			var highlighting = HighlightingManager.Instance.
				GetDefinitionByExtension(
					System.IO.Path.GetExtension(FileName));

			textEditor.SyntaxHighlighting = highlighting;
			if (highlighting != null)
				comboHighlighting.SelectedItem = highlighting;

			colorizer = new ColorizeChanges(annotated_lines);
			textEditor.TextArea.TextView.LineTransformers.Add(colorizer);

			textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
		}

		//------------------------------------------------------------------
		private void SetLines()
		{
			if (annotated_lines != null)
			{
				var encoding = comboEncodings.SelectedItem as EncodingItem;
				int caret_pos = textEditor.TextArea.Caret.Line;

				var builder = new StringBuilder();

				foreach (var line in annotated_lines)
				{
					var text_line = line.Info.Source;
					if (encoding != null && encoding.Encoding != Encoding.Default)
						 text_line = Util.Convert(text_line, encoding.Encoding, Encoding.Default);

					builder.AppendLine(text_line);
				}

				bool save_caret_pos = textEditor.Document.LineCount ==
				                      (annotated_lines.Count + 1);
				var vertical_offset = textEditor.TextArea.TextView.VerticalOffset;

				textEditor.Text = builder.ToString();

				if (save_caret_pos)
				{
					textEditor.TextArea.Caret.Line = caret_pos;
					textEditor.ScrollToVerticalOffset(vertical_offset);
				}
			}
		}

		//------------------------------------------------------------------
		private void comboEncodings_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SetLines();
		}

		//------------------------------------------------------------------
		private void comboHighlightings_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (comboHighlighting.SelectedItem != null)
			textEditor.SyntaxHighlighting = comboHighlighting.SelectedItem as IHighlightingDefinition;
		}

		//-----------------------------------------------------------------------------
		public void Dispose()
		{
			if (!disposed)
			{
				disposed = true;

				timer.Stop();
				timer.Tick -= TimerOnTick;

				async_annotate.Cancel();
				async_annotate.Dispose();

				Cfg.Set(AnnotateWindow.CfgPath, DiffColorizerControl.DiffVisible, expanderDiff.IsExpanded ? 1 : 0);
				if (!Double.IsNaN(diffColorizer.Width))
				{
					int diff_width = (int)diffColorizer.Width;
					if (diff_width > 0)
						Cfg.Set(AnnotateWindow.CfgPath, DiffColorizerControl.DiffWidth, diff_width);
				}

				Cfg.Set(AnnotateWindow.CfgPath, "FilesVisible", viewFilesExpander.IsExpanded ? 1 : 0);
				if (!Double.IsNaN(gridFiles.Height))
				{
					int files_height = (int)gridFiles.Height;
					if (files_height > 0)
						Cfg.Set(AnnotateWindow.CfgPath, "FilesHeight", files_height);
				}

				if (!Double.IsNaN(changesRow.Height.Value))
				{
					int changes_height = (int)changesRow.Height.Value;
					if (changes_height > 0)
						Cfg.Set(AnnotateWindow.CfgPath, "ChangesHeight", changes_height);
				}

				var encoding = comboEncodings.SelectedItem as EncodingItem;
				if (encoding != null)
					Cfg.Set(AnnotateWindow.CfgPath, "encoding", encoding.Name);

				if (colorizer != null)
				{
					textEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
				}

				diffColorizer.Dispose();
			}
		}

		//------------------------------------------------------------------
		private void Control_Unloaded(object sender, RoutedEventArgs e)
		{
			Dispose();
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncChangeDescFull(List<RenameParts> parts)
		{
			if (parts.Count == 0)
			{
				return;
			}

			var history = new List<FileHistoryInfo2>();

			int part_idx = 0;
			foreach (var part in parts)
			{
				part_idx++;

				foreach (var change_desc in part.Revs)
				{
					var history_item = new FileHistoryInfo2();
					history_item.ChangeDesc = change_desc;
					history_item.FileName = part.FileName;
					history_item.GroupText = String.Format("[{0}]: {1}", part_idx, part.FileName);

					if (ParentsInfo != null)
					{
						foreach (var parent in ParentsInfo.Parents)
						{
							if (history_item.ChangeDesc.SHA1 == parent.SHA1)
							{
								history_item.IsCurrent = true;
								break;
							}
						}
					}

					BranchInfo branch_info;
					if (Branches.TryGetValue(history_item.ChangeDesc.SHA1, out branch_info))
						history_item.BranchInfo = branch_info;

					// the revision -> list index
					rev_to_change_idx_map[history_item.ChangeDesc.Rev] = history.Count;

					file_history_map[history_item.ChangeDesc.SHA1] = history_item;
					history.Add(history_item);
				}
			}

			listChanges.ItemsSource = history;
			if (listChanges.Items.Count > 0)
				listChanges.SelectedIndex = 0;

			listChanges.Focus();

			if (UpdateContext.Cache.Branches != null)
				OnAsyncBranch(UpdateContext.Cache.Branches);
			else
				HandleBranchChanges();

			if (UpdateContext.Cache.Tags != null)
				OnAsyncTags(UpdateContext.Cache.Tags);
			else
				HandleTagsChanges();

			if (UpdateContext.Cache.ParentsInfo != null)
				OnAsyncParents(UpdateContext.Cache.ParentsInfo);
			else
				HandleParentChange();

			if (UpdateContext.Cache.Bookmarks != null)
				OnAsyncBookmarks(UpdateContext.Cache.Bookmarks);
			else
				HandleBookmarksChanges();

			if (parts.Count > 1)
			{
				// Since grouping is effectively disable virtualization,
				// enable it only if there were file renames

				listChanges.GroupStyle.Clear();
				listChanges.GroupStyle.Add((GroupStyle)Resources["GroupStyleForRenames"]);

				var myView = (CollectionView)CollectionViewSource.GetDefaultView(listChanges.ItemsSource);
				var groupDescription = new PropertyGroupDescription("GroupText");
				myView.GroupDescriptions.Clear();
				myView.GroupDescriptions.Add(groupDescription);
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncTags(List<TagInfo> tags_list)
		{
			RunningOperations &= ~AsyncOperations.Tags;

			if (tags_list == null)
				return;

			var new_tags = new Dictionary<string, TagInfo>();

			foreach (var tag in tags_list)
			{
				new_tags[tag.Name] = tag;
			}

			foreach (var tag in Tags.Values)
			{
				// removing old tags
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					var tag_name = tag.Name;
					var tag_info = change_desc.Tags.FirstOrDefault(t => t.Name == tag_name);
					if (tag_info != null)
						change_desc.Tags.Remove(tag_info);
				}
			}

			Tags = new_tags;

			foreach (var tag in Tags.Values)
			{
				// adding or updating tags
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					var tag_name = tag.Name;

					int pos = change_desc.Tags.FirstIndexOf(t => t.Name == tag_name);
					if (pos != -1)
						change_desc.Tags[pos] = tag;
					else
						change_desc.Tags.Add(tag);
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncBookmarks(List<BookmarkInfo> bookmarks_list)
		{
			RunningOperations &= ~AsyncOperations.Bookmarks;

			if (bookmarks_list == null)
				return;

			var new_bookmarks = new Dictionary<string, BookmarkInfo>();

			foreach (var bookmark in bookmarks_list)
			{
				new_bookmarks[bookmark.Name] = bookmark;
			}

			foreach (var bookmark in Bookmarks.Values)
			{
				// removing old bookmark
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(bookmark.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					var book_name = bookmark.Name;
					var book = change_desc.Bookmarks.FirstOrDefault(b => b.Name == book_name);
					if (book != null)
						change_desc.Bookmarks.Remove(book);
				}
			}

			Bookmarks = new_bookmarks;

			foreach (var bookmark in Bookmarks.Values)
			{
				// adding or updating bookmarks
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(bookmark.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					var book_name = bookmark.Name;

					int pos = change_desc.Bookmarks.FirstIndexOf(b => b.Name == book_name);
					if (pos != -1)
						change_desc.Bookmarks[pos] = bookmark;
					else
						change_desc.Bookmarks.Add(bookmark);
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncBranch(List<BranchInfo> branch_list)
		{
			RunningOperations &= ~AsyncOperations.Branches;

			if (branch_list == null)
				return;

			var new_branches = new Dictionary<string, BranchInfo>();

			foreach (var branch_info in branch_list)
			{
				new_branches[branch_info.SHA1] = branch_info;
				Branches.Remove(branch_info.SHA1);
			}

			foreach (var branch_info in Branches.Values)
			{
				// removing old branch info
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = null;
			}

			Branches = new_branches;

			foreach (var branch_info in Branches.Values)
			{
				// adding or updating branch info
				FileHistoryInfo2 file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = branch_info;
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncParents(ParentsInfo new_current)
		{
			RunningOperations &= ~AsyncOperations.Parents;

			if (new_current == null)
				return;

			if (ParentsInfo != null)
			{
				foreach (var parent in ParentsInfo.Parents)
				{
					FileHistoryInfo2 file_history;
					if (file_history_map.TryGetValue(parent.SHA1, out file_history))
						file_history.IsCurrent = false;
				}
			}

			ParentsInfo = new_current;
			if (ParentsInfo != null)
			{
				foreach (var parent in ParentsInfo.Parents)
				{
					FileHistoryInfo2 file_history;
					if (file_history_map.TryGetValue(parent.SHA1, out file_history))
						file_history.IsCurrent = true;
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void ShowFileDiff()
		{
			if (diffColorizer == null)
				return;

			if (!expanderDiff.IsExpanded)
				return;

			diffColorizer.Clear();

			var parent_diff = GetSelectedParentDiff();
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;

			RunningOperations |= AsyncOperations.Diff;

			if (SelectedParentFile != null)
			{
				diffColorizer.RunHgDiffAsync(HgClient, SelectedParentFile.FileInfo.File,
					parent_diff.Desc.SHA1,
					file_history.ChangeDesc.SHA1);
			}
		}

		//-----------------------------------------------------------------------------
		private ParentFilesDiff GetSelectedParentDiff()
		{
			var tab = tabParentsDiff.SelectedItem as TabItem;
			if (tab == null)
				return null;

			return tab.DataContext as ParentFilesDiff;
		}

		//------------------------------------------------------------------
		private void DiffGridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			if (diffColorizer.Width > e.HorizontalChange)
				diffColorizer.Width -= e.HorizontalChange;
			else
				diffColorizer.Width = 0;
		}

		//-----------------------------------------------------------------------------
		private void expanderDiff_Expanded(object sender, RoutedEventArgs e)
		{
			ShowFileDiff();
		}

		//-----------------------------------------------------------------------------
		private void expanderDiff_Collapsed(object sender, RoutedEventArgs e)
		{
			diffColumn.Width = new GridLength(0, GridUnitType.Auto);
			diffColorizer.Clear();
		}

		//------------------------------------------------------------------
		private void HistoryDiffPrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (listChanges.SelectedItems.Count == 1)
			{
				if (listChanges.SelectedIndex != (listChanges.Items.Count - 1))
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void HistoryDiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var f1 = (FileHistoryInfo2)listChanges.Items[listChanges.SelectedIndex];
			var f2 = (FileHistoryInfo2)listChanges.Items[listChanges.SelectedIndex + 1];

			DiffTwoRevisions(f1, f2);

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffTwoRevisions(FileHistoryInfo2 f1, FileHistoryInfo2 f2)
		{
			if (f1.ChangeDesc.Rev > f2.ChangeDesc.Rev)
			{
				var temp = f2;
				f2 = f1;
				f1 = temp;
			}

			try
			{
				HgClient.Diff(f1.FileName, f1.ChangeDesc.Rev, f2.FileName, f2.ChangeDesc.Rev);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}
		}

		//------------------------------------------------------------------
		private void ListChanges_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (Commands.DiffPreviousCommand != null)
			{
				if (Commands.DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					Commands.DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private void FilesDiffPrevious_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = false;
			if (SelectedParentFile != null)
			{
				if (SelectedParentFile.FileInfo.Status == HgFileStatus.Added
					&& !String.IsNullOrEmpty(SelectedParentFile.FileInfo.CopiedFrom))
				{
					e.CanExecute = true;
				}

				if (SelectedParentFile.FileInfo.Status == HgFileStatus.Modified)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FilesDiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var parent_diff = GetSelectedParentDiff();
			var file_info = SelectedParentFile.FileInfo;

			try
			{
				var child_file = file_info.File;
				var parent_file = file_info.File;

				if (!String.IsNullOrEmpty(file_info.CopiedFrom))
					parent_file = file_info.CopiedFrom;

				HgClient.Diff(parent_file, parent_diff.Desc.SHA1,
					child_file, file_history.ChangeDesc.SHA1);
			}
			catch (HgDiffException)
			{
				Util.HandleHgDiffException();
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ListViewFiles_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (Commands.DiffPreviousCommand != null)
			{
				if (Commands.DiffPreviousCommand.CanExecute(sender, e.Source as IInputElement))
					Commands.DiffPreviousCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private UpdateContextCache BuildUpdateContextCache()
		{
			var cache = new UpdateContextCache();
			cache.HgClient = UpdateContext.Cache.HgClient;

			if ((RunningOperations & AsyncOperations.Parents) != AsyncOperations.Parents)
				cache.ParentsInfo = ParentsInfo;

			if ((RunningOperations & AsyncOperations.Tags) != AsyncOperations.Tags)
				cache.Tags = new List<TagInfo>(Tags.Values);

			if ((RunningOperations & AsyncOperations.Branches) != AsyncOperations.Branches)
				cache.Branches = new List<BranchInfo>(Branches.Values);

			if ((RunningOperations & AsyncOperations.Bookmarks) != AsyncOperations.Bookmarks)
				cache.Bookmarks = new List<BookmarkInfo>(Bookmarks.Values);

			return cache;
		}

		//------------------------------------------------------------------
		private void FileHistory_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = SelectedParentFile != null;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var file_info = SelectedParentFile.FileInfo;

			var wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = SelectedParentFile.ParentFilesDiff.Desc.SHA1;
			wnd.FileName = file_info.File;

			wnd.UpdateContext.Cache = BuildUpdateContextCache();

			// FIXME:
			wnd.Owner = Window.GetWindow(this);

			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBranchChanged)
				HandleBranchChanges();

			if (wnd.UpdateContext.IsTagsChanged)
				HandleTagsChanges();

			if (wnd.UpdateContext.IsBookmarksChanged)
				HandleBookmarksChanges();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void ViewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = SelectedParentFile != null;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ViewFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var file_info = SelectedParentFile.FileInfo;
			var cs = file_history.ChangeDesc;

			if (file_info.Status == HgFileStatus.Removed)
				HgClient.ViewFile(file_info.File, SelectedParentFile.ParentFilesDiff.Desc.Rev.ToString());
			else
				HgClient.ViewFile(file_info.File, cs.Rev.ToString());
		}

		//------------------------------------------------------------------
		private void HistoryViewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = SelectedParentFile != null;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void HistoryViewFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
			var cs = file_history.ChangeDesc;

			HgClient.ViewFile(file_history.FileName, cs.Rev.ToString());
		}

		//------------------------------------------------------------------
		private void HandleParentChange()
		{
			RunningOperations |= AsyncOperations.Parents;
			var parents = UpdateContext.Cache.HgClient.Parents();
			OnAsyncParents(parents);
		}

		//------------------------------------------------------------------
		private void HandleBranchChanges()
		{
			RunningOperations |= AsyncOperations.Branches;
			var branches = UpdateContext.Cache.HgClient.Branches(HgBranchesOptions.Closed);
			OnAsyncBranch(branches);
		}

		//------------------------------------------------------------------
		private void HandleTagsChanges()
		{
			RunningOperations |= AsyncOperations.Tags;
			var tags = UpdateContext.Cache.HgClient.Tags();
			OnAsyncTags(tags);
		}

		//------------------------------------------------------------------
		private void HandleBookmarksChanges()
		{
			RunningOperations |= AsyncOperations.Bookmarks;
			var books = UpdateContext.Cache.HgClient.Bookmarks();
			OnAsyncBookmarks(books);
		}

		//-----------------------------------------------------------------------------
		private void TimerOnTick(object sender, EventArgs event_args)
		{
			RunningOperations &= ~AsyncOperations.ChangeDesc;
			timer.Stop();

			if (listChanges.SelectedItems.Count == 1)
			{
				var file_history = (FileHistoryInfo2)listChanges.SelectedItem;
				var options = HgStatusOptions.Added | HgStatusOptions.Deleted
					| HgStatusOptions.Modified
					| HgStatusOptions.Copies | HgStatusOptions.Removed;

				var parents = file_history.ChangeDesc.Parents;
				if (parents.Count == 0)
					parents = new ObservableCollection<string>(new[] { "null" });

				var parents_diff = new List<ParentFilesDiff>();

				foreach (var parent in parents)
				{
					var files = HgClient.Status(options, "", parent, file_history.ChangeDesc.SHA1);

					var desc = HgClient.GetRevisionDesc(parent);
					if (desc != null)
					{
						var parent_diff = new ParentFilesDiff();
						parent_diff.Desc = desc;
						parent_diff.Files = new List<ParentDiffHgFileInfo>();

						foreach (var file in files)
							parent_diff.Files.Add(new ParentDiffHgFileInfo { FileInfo = file });

						parents_diff.Add(parent_diff);
					}
				}

				var tabs = new[] { tab1, tab2 };
				var lists = new[] { tabList1, tabList2 };
				for (int i = parents.Count; i < tabs.Length; ++i)
				{
					tabs[i].Visibility = Visibility.Collapsed;
					tabs[i].DataContext = null;
				}

				for (int i = 0; i < parents_diff.Count; ++i)
				{
					var tab = tabs[i];
					var list = lists[i];
					var parent = parents_diff[i];
					if (i == 0)
						parent.IsSelected = true;

					tab.DataContext = parent;
					tab.Visibility = Visibility.Visible;

					var file = parent.Files.FirstOrDefault(f => f.FileInfo.File == file_history.FileName);

					if (file != null)
					{
						file.IsSelected = true;
					}
					else
					{
						if (parent.Files.Count > 0)
						{
							parent.Files[0].IsSelected = true;
							file = parent.Files[0];
						}
					}

					if (file != null)
					{
						Logger.WriteLine("Scrolling list {0} to item: {1}", parent.Desc.Rev, file.FileInfo.File);
						list.ScrollIntoView(file);
					}
				}

				List<AnnotateLineView> rev_lines;
				if (rev_to_line_view.TryGetValue(file_history.ChangeDesc.Rev, out rev_lines))
				{
					if (rev_lines.Count > 0)
					{
						int line_number = rev_lines[0].Info.Line - 1;
						if (line_number < annotated_lines.Count)
						{
							var line_view = annotated_lines[line_number];
							if (!line_view.IsSelected)
							{
								if (textEditor.TextArea.Caret.Line != (line_number + 1))
									textEditor.TextArea.Caret.Line = line_number + 1;
								else
									UpdateLineSelection(line_number + 1);

								textEditor.ScrollToLine(line_number + 1);
							}
						}
					}
				}
				else
				{
					foreach (var line in annotated_lines)
					{
						line.IsSelected = false;
					}
				}

				textEditor.TextArea.TextView.Redraw();
			}
		}


		//------------------------------------------------------------------
		private void listChanges_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			tab1.DataContext = null;
			tab2.DataContext = null;
			tab2.Visibility = Visibility.Collapsed;
			tab1.Visibility = Visibility.Collapsed;
			diffColorizer.Clear();

			timer.Stop();

			if (listChanges.SelectedItems.Count == 1)
			{
				RunningOperations |= AsyncOperations.ChangeDesc; 
				timer.Start();
			}
		}

		//------------------------------------------------------------------
		private void GotoLine_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			if (textEditor != null)
			{
				int line;
				if (int.TryParse(textLine.Text, out line))
				{
					if (line > 0 && line <= annotated_lines.Count)
						e.CanExecute = true;
				}
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void GotoLine_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			int line;
			if (int.TryParse(textLine.Text, out line))
			{
				textEditor.ScrollToLine(line);
			}

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void textLine_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				if (GotoLineCommand.CanExecute(sender, e.Source as IInputElement))
					GotoLineCommand.Execute(sender, e.Source as IInputElement);
			}
		}

		//------------------------------------------------------------------
		private void NextChange_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void NextChange_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (textEditor == null)
				return;

			int caret_line = textEditor.TextArea.Caret.Line;
			if (caret_line < 1 || caret_line > annotated_lines.Count)
				return;

			var line_view = annotated_lines[caret_line - 1];

			int revision = line_view.Info.Rev;
			int line_number = line_view.Info.Line;

			var end_of_current_idx = annotated_lines.FindIndex(line_number - 1,
				annotated_line => annotated_line.Info.Rev != revision);

			if (end_of_current_idx == -1)
				return;

			var next_change_idx = annotated_lines.FindIndex(end_of_current_idx,
					annotated_line => annotated_line.Info.Rev == revision);

			if (next_change_idx == -1)
				return;

			textEditor.TextArea.Caret.Line = next_change_idx + 1;
			textEditor.ScrollToLine(next_change_idx + 1);
		}

		//------------------------------------------------------------------
		private childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(obj, i);
				if (child != null && child is childItem)
				{
					return (childItem)child;
				}
				else
				{
					childItem childOfChild = FindVisualChild<childItem>(child);
					if (childOfChild != null)
					{
						return childOfChild;
					}
				}
			}
			return null;
		}  

		//------------------------------------------------------------------
		private void PrevChange_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void PrevChange_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			if (textEditor == null)
				return;

			int caret_line = textEditor.TextArea.Caret.Line;
			if (caret_line < 1 || caret_line > annotated_lines.Count)
				return;

			var line_view = annotated_lines[caret_line - 1];

			int revision = line_view.Info.Rev;
			int line_number = line_view.Info.Line;

			var end_of_current_idx = annotated_lines.FindLastIndex(line_number - 1,
				annotated_line => annotated_line.Info.Rev != revision);

			if (end_of_current_idx == -1)
				return;
				
			var next_change_idx = annotated_lines.FindLastIndex(end_of_current_idx,
					annotated_line => annotated_line.Info.Rev == revision);

			if (next_change_idx == -1)
				return;

			textEditor.TextArea.Caret.Line = next_change_idx + 1;
			textEditor.ScrollToLine(next_change_idx + 1);
		}

		//------------------------------------------------------------------
		void GridViewColumnHeaderClickedHandler(object sender,
												RoutedEventArgs e)
		{
			GridViewColumnSorter column_sorter;
			ListView list_view = sender as ListView;
			if (list_view != null)
			{
				if (!files_sorter.TryGetValue(list_view, out column_sorter))
				{
					column_sorter = new GridViewColumnSorter(list_view);
					files_sorter[list_view] = column_sorter;
				}

				column_sorter.GridViewColumnHeaderClickedHandler(sender, e);
			}
		}

		//------------------------------------------------------------------
		private void GridFilesSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
		{
			if (gridFiles.Height > e.VerticalChange)
				gridFiles.Height -= e.VerticalChange;
			else
				gridFiles.Height = 0;
		}

		//-----------------------------------------------------------------------------
		private void viewFilesExpander_Collapsed(object sender, RoutedEventArgs e)
		{
			filesRow.Height = new GridLength(0, GridUnitType.Auto);
		}

		//-----------------------------------------------------------------------------
		private void listViewFiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			SelectedParentFile = null;

			var parent_diff = GetSelectedParentDiff();
			var list_view = e.OriginalSource as ListView;

			if (parent_diff != null && list_view != null)
			{
				if (list_view.SelectedItems.Count == 1)
				{
					SelectedParentFile = new SelectedParentFile
					{
						FileInfo = ((ParentDiffHgFileInfo)list_view.SelectedItem).FileInfo,
						ParentFilesDiff = parent_diff
					};

					ShowFileDiff();
				}
				e.Handled = true;
			}
		}

		//-----------------------------------------------------------------------------
		private void tabParentsDiff_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count == 1)
			{
				SelectedParentFile = null;

				var parent_diff = GetSelectedParentDiff();
				if (parent_diff == null)
					return;

				var lists = new[] { tabList1, tabList2 };
				var list_view = lists.FirstOrDefault(l => l.DataContext == parent_diff);

				if (list_view != null)
				{
					if (list_view.SelectedItems.Count == 1)
					{
						SelectedParentFile = new SelectedParentFile
						{
							FileInfo = ((ParentDiffHgFileInfo)list_view.SelectedItem).FileInfo,
							ParentFilesDiff = parent_diff
						};

						Logger.WriteLine("Show file diff after tab select");
						ShowFileDiff();
					}
					e.Handled = true;
				}
			}
		}

		//-----------------------------------------------------------------------------
		private void listViewFiles_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			Logger.WriteLine("Visible changed {0}, e {1}", sender, e.NewValue);
			if ((bool)e.NewValue)
			{
				var list_view = sender as ListView;
				if (list_view == null)
					return;

				if (list_view.SelectedItem != null)
					return;

				var parent_diff = list_view.DataContext as ParentFilesDiff;
				if (parent_diff == null)
					return;

				var file = parent_diff.Files.FirstOrDefault(f => f.IsSelected);
				if (file != null)
				{
					Logger.WriteLine("OnVisibleChanged, Scrolling list {0}, {1}", list_view.Items.Count, file.FileInfo.File);
					list_view.ScrollIntoView(file);
				}
			}
		}

	}

	//==================================================================
	class AnnotateLineView
	{
		public AnnotateLineInfo Info { get; set; }
		public bool IsSelected { get; set; }
	}

	//------------------------------------------------------------------
	internal class ColorizeChanges : DocumentColorizingTransformer
	{
		private readonly Brush selected_brush;
		private readonly Brush current_brush;
		private readonly List<AnnotateLineView> lines;

		//------------------------------------------------------------------
		public ColorizeChanges(List<AnnotateLineView> lines)
		{
			this.lines = lines;
			
			var selected_color = Colors.Blue;
			selected_color.A = 25;

			selected_brush = new SolidColorBrush(selected_color);
			selected_brush.Freeze();

			var current_color = Colors.Red;
			current_color.A = 50;

			current_brush = new SolidColorBrush(current_color);
			current_brush.Freeze();
		}

		//------------------------------------------------------------------
		public int CurrentLine { get; set; }

		//------------------------------------------------------------------
		protected override void ColorizeLine(DocumentLine line)
		{
			int line_zero_based = line.LineNumber - 1;
			if (line_zero_based >= lines.Count)
				return;

			var log_line = lines[line_zero_based];
			if (log_line.IsSelected)
			{
				var brush = selected_brush;
				bool is_current = false;
				
				if (line_zero_based == CurrentLine)
				{
					brush = current_brush;
					is_current = true;
				}

				ChangeLinePart(line.Offset, line.EndOffset,
						(VisualLineElement element) =>
						{
							element.TextRunProperties.SetBackgroundBrush(brush);
							if (is_current)
							{
								Typeface tf = element.TextRunProperties.Typeface;

								// Replace the typeface with a modified version of
								// the same typeface
								element.TextRunProperties.SetTypeface(new Typeface(
									tf.FontFamily,
									tf.Style,
									FontWeights.Bold,
									tf.Stretch
								));
							}
						});
			}
		}
	}
}
