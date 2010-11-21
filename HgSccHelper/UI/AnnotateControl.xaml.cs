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
using HgSccHelper.UI;
using HgSccHelper.UI.RevLog;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
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
		Hg Hg { get; set; }

		//------------------------------------------------------------------
		IdentifyInfo CurrentRevision { get; set; }

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
		/// SHA1 -> FileHistoryInfo map
		/// </summary>
		Dictionary<string, FileHistoryInfo> file_history_map;

		List<AnnotateLineView> annotated_lines;

		Dictionary<int, int> rev_to_change_idx_map;
		Dictionary<int, List<AnnotateLineView>> rev_to_line_view;

		GridViewColumnSorter files_sorter;

		//-----------------------------------------------------------------------------
		private bool disposed;

		//-----------------------------------------------------------------------------
		private AsyncOperations async_ops;

		//-----------------------------------------------------------------------------
		private Cursor prev_cursor;

		private AsyncChangeDescFull async_changedesc;
		private AsyncIdentify async_identify;
		private AsyncBranches async_branches;
		private AsyncTags async_tags;
		private AsyncAnnotate async_annotate;

		private ColorizeChanges colorizer;
		private ObservableCollection<EncodingItem> encodings;
		private ObservableCollection<IHighlightingDefinition> highlightings;

		//------------------------------------------------------------------
		public AnnotateControl()
		{
			InitializeComponent();

			UpdateContext = new UpdateContext();
			file_history_map = new Dictionary<string, FileHistoryInfo>();

			rev_to_change_idx_map = new Dictionary<int, int>();
			rev_to_line_view = new Dictionary<int, List<AnnotateLineView>>();

			files_sorter = new GridViewColumnSorter(listViewFiles);

			diffColorizer.Complete = new Action<List<string>>(OnDiffColorizer);

			async_changedesc = new AsyncChangeDescFull();
			async_changedesc.Complete = new Action<List<ChangeDesc>>(OnAsyncChangeDescFull);

			async_identify = new AsyncIdentify();
			async_identify.Complete = new Action<IdentifyInfo>(OnAsyncIdentify);

			async_branches = new AsyncBranches();
			async_branches.Complete = new Action<List<BranchInfo>>(OnAsyncBranch);

			async_tags = new AsyncTags();
			async_tags.Complete = new Action<List<TagInfo>>(OnAsyncTags);

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
			Hg = new Hg();

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

			int files_height;
			Cfg.Get(AnnotateWindow.CfgPath, "FilesHeight", out files_height, 200);
			gridFiles.Height = files_height;

			int files_visible;
			Cfg.Get(AnnotateWindow.CfgPath, "FilesVisible", out files_visible, 0);
			viewFilesExpander.IsExpanded = (files_visible != 0);

			Tags = new Dictionary<string, TagInfo>();
			Branches = new Dictionary<string, BranchInfo>();

			var files = Hg.Status(WorkingDir, FileName, Rev ?? "");
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
				rev_range = string.Format("{0}:0", Rev);

			RunningOperations |= AsyncOperations.ChangeDesc;
			async_changedesc.RunAsync(WorkingDir, FileName, rev_range);
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

				async_changedesc.Cancel();
				async_changedesc.Dispose();

				async_identify.Cancel();
				async_identify.Dispose();

				async_branches.Cancel();
				async_branches.Dispose();

				async_tags.Cancel();
				async_tags.Dispose();

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
		private void OnAsyncChangeDescFull(List<ChangeDesc> changes)
		{
			RunningOperations &= ~AsyncOperations.ChangeDesc;

			if (changes == null)
				return;

			if (changes.Count == 0)
			{
				Logger.WriteLine("Changes == 0");
				return;
			}

			HandleBranchChanges();
			HandleTagsChanges();
			HandleParentChange();

			var renames = Hg.FindRenames(WorkingDir, FileName, changes);
			var history = new List<FileHistoryInfo>();

			int left_idx = 0;
			int right_idx = 1;

			foreach (var change in changes)
			{
				if (right_idx < renames.Count)
				{
					var right = renames[right_idx];
					if (change.Rev <= right.Rev)
						left_idx = right_idx++;
				}

				var history_item = new FileHistoryInfo();
				history_item.ChangeDesc = change;
				history_item.RenameInfo = renames[left_idx];
				history_item.GroupText = String.Format("[{0}]: {1}", renames.Count - left_idx, history_item.RenameInfo.Path);

				if (CurrentRevision != null)
				{
					foreach (var parent in CurrentRevision.Parents)
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

				file_history_map[history_item.ChangeDesc.SHA1] = history_item;

				// the revision -> list index
				rev_to_change_idx_map[history_item.ChangeDesc.Rev] = history.Count;

				history.Add(history_item);
			}

			listChanges.ItemsSource = history;
			if (listChanges.Items.Count > 0)
				listChanges.SelectedIndex = 0;

			listChanges.Focus();

			var myView = (CollectionView)CollectionViewSource.GetDefaultView(listChanges.ItemsSource);
			var groupDescription = new PropertyGroupDescription("GroupText");
			myView.GroupDescriptions.Add(groupDescription);
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
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					change_desc.Tags.Remove(tag.Name);
				}
			}

			Tags = new_tags;

			foreach (var tag in Tags.Values)
			{
				// adding or updating tags
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(tag.SHA1, out file_history))
				{
					var change_desc = file_history.ChangeDesc;
					if (!change_desc.Tags.Contains(tag.Name))
						change_desc.Tags.Add(tag.Name);
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
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = null;
			}

			Branches = new_branches;

			foreach (var branch_info in Branches.Values)
			{
				// adding or updating branch info
				FileHistoryInfo file_history;
				if (file_history_map.TryGetValue(branch_info.SHA1, out file_history))
					file_history.BranchInfo = branch_info;
			}
		}

		//-----------------------------------------------------------------------------
		private void OnAsyncIdentify(IdentifyInfo new_current)
		{
			RunningOperations &= ~AsyncOperations.Identify;

			if (new_current == null)
				return;

			if (CurrentRevision != null)
			{
				foreach (var parent in CurrentRevision.Parents)
				{
					FileHistoryInfo file_history;
					if (file_history_map.TryGetValue(parent.SHA1, out file_history))
						file_history.IsCurrent = false;
				}
			}

			CurrentRevision = new_current;
			if (CurrentRevision != null)
			{
				foreach (var parent in CurrentRevision.Parents)
				{
					FileHistoryInfo file_history;
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

			if (listViewFiles != null && listViewFiles.SelectedItems.Count == 1)
			{
				var file_history = (FileHistoryInfo)listChanges.SelectedItem;
				var file_info = (FileInfo)listViewFiles.SelectedItem;
				var cs = file_history.ChangeDesc;

				RunningOperations |= AsyncOperations.Diff;
				string rev2 = cs.SHA1;
				string rev1 = (cs.Rev - 1).ToString();
				if (cs.Rev == 0)
					rev1 = "null";

				diffColorizer.RunHgDiffAsync(WorkingDir, file_info.Path, rev1, rev2);
			}
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

		//-----------------------------------------------------------------------------
		private void listViewFiles_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			ShowFileDiff();
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
			var f1 = (FileHistoryInfo)listChanges.Items[listChanges.SelectedIndex];
			var f2 = (FileHistoryInfo)listChanges.Items[listChanges.SelectedIndex + 1];

			DiffTwoRevisions(f1, f2);

			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void DiffTwoRevisions(FileHistoryInfo f1, FileHistoryInfo f2)
		{
			if (f1.ChangeDesc.Rev > f2.ChangeDesc.Rev)
			{
				var temp = f2;
				f2 = f1;
				f1 = temp;
			}

			try
			{
				Hg.Diff(WorkingDir, f1.RenameInfo.Path, f1.ChangeDesc.Rev, f2.RenameInfo.Path, f2.ChangeDesc.Rev);
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
			if (listViewFiles.SelectedItems.Count == 1)
			{
				var file_info = (FileInfo)listViewFiles.SelectedItem;
				if (file_info.Status == FileStatus.Modified)
					e.CanExecute = true;
			}
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FilesDiffPrevious_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo)listChanges.SelectedItem;
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			try
			{
				Hg.Diff(WorkingDir, file_info.Path, cs.Rev - 1, file_info.Path, cs.Rev);
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
		private void FileHistory_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void FileHistory_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo)listChanges.SelectedItem;
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			FileHistoryWindow wnd = new FileHistoryWindow();
			wnd.WorkingDir = WorkingDir;
			wnd.Rev = cs.Rev.ToString();
			wnd.FileName = file_info.Path;

			// FIXME:
			wnd.Owner = Window.GetWindow(this);

			wnd.ShowDialog();

			if (wnd.UpdateContext.IsParentChanged)
				HandleParentChange();

			if (wnd.UpdateContext.IsBranchChanged)
				HandleBranchChanges();

			if (wnd.UpdateContext.IsTagsChanged)
				HandleTagsChanges();

			UpdateContext.MergeWith(wnd.UpdateContext);
		}

		//------------------------------------------------------------------
		private void ViewFile_CanExecute(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = listViewFiles.SelectedItems.Count == 1;
			e.Handled = true;
		}

		//------------------------------------------------------------------
		private void ViewFile_Executed(object sender, ExecutedRoutedEventArgs e)
		{
			var file_history = (FileHistoryInfo)listChanges.SelectedItem;
			var file_info = (FileInfo)listViewFiles.SelectedItem;
			var cs = file_history.ChangeDesc;

			var hg = new Hg();
			if (file_info.Status == FileStatus.Removed)
				hg.ViewFile(WorkingDir, file_info.Path, (cs.Rev - 1).ToString());
			else
				hg.ViewFile(WorkingDir, file_info.Path, cs.Rev.ToString());
		}

		//------------------------------------------------------------------
		private void HandleParentChange()
		{
			RunningOperations |= AsyncOperations.Identify;
			async_identify.RunAsync(WorkingDir);
		}

		//------------------------------------------------------------------
		private void HandleBranchChanges()
		{
			RunningOperations |= AsyncOperations.Branches;
			async_branches.RunAsync(WorkingDir, HgBranchesOptions.Closed);
		}

		//------------------------------------------------------------------
		private void HandleTagsChanges()
		{
			RunningOperations |= AsyncOperations.Identify;
			async_tags.RunAsync(WorkingDir);
		}

		//------------------------------------------------------------------
		private void listChanges_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			listViewFiles.DataContext = null;

			if (listChanges.SelectedItems.Count == 1)
			{
				var file_history = (FileHistoryInfo)listChanges.SelectedItem;
				listViewFiles.ItemsSource = file_history.ChangeDesc.Files;
				if (listViewFiles.Items.Count > 0)
				{
					var file = file_history.ChangeDesc.Files.FirstOrDefault(
						f => f.Path == file_history.RenameInfo.Path);

					if (file != null)
						listViewFiles.SelectedItem = file;

					if (listViewFiles.SelectedIndex == -1)
						listViewFiles.SelectedIndex = 0;

					listViewFiles.ScrollIntoView(listViewFiles.SelectedItem);
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
			files_sorter.GridViewColumnHeaderClickedHandler(sender, e);
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
