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
using HgSccHelper.CommandServer;

namespace HgSccHelper.UI
{
	public partial class BookmarksWindow : Window
	{
		//-----------------------------------------------------------------------------
		public string WorkingDir { get; set; }

		//------------------------------------------------------------------
		public string TargetRevision { get; set; }

		//------------------------------------------------------------------
		public UpdateContext UpdateContext { get; private set; }

		//------------------------------------------------------------------
		HgClient HgClient { get { return UpdateContext.Cache.HgClient; } }

		DispatcherTimer bookmark_timer;
		DispatcherTimer rev_timer;

		RevLogChangeDesc RevDesc { get; set; }
		RevLogChangeDesc BookmarkDesc { get; set; }

		Dictionary<string, BookmarkInfo> bookmarks_map;

		public const string CfgPath = @"GUI\BookmarksWindow";
		CfgWindowPosition wnd_cfg;

		//------------------------------------------------------------------
		public BookmarksWindow()
		{
			wnd_cfg = new CfgWindowPosition(CfgPath, this, CfgWindowPositionOptions.PositionOnly);

			InitializeComponent();

			HgSccHelper.UI.ThemeManager.Instance.Subscribe(this);

			// Since WPF combo box does not provide TextChanged event
			// register it from edit text box through combo box template

			comboBookmarks.Loaded += delegate
			{
				TextBox editTextBox = comboBookmarks.Template.FindName("PART_EditableTextBox", comboBookmarks) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged += OnComboTextChanged;
				}
			};

			comboBookmarks.Unloaded += delegate
			{
				TextBox editTextBox = comboBookmarks.Template.FindName("PART_EditableTextBox", comboBookmarks) as TextBox;
				if (editTextBox != null)
				{
					editTextBox.TextChanged -= OnComboTextChanged;
				}
			};

			bookmarks_map = new Dictionary<string, BookmarkInfo>();
			UpdateContext = new UpdateContext();
		}

		//------------------------------------------------------------------
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Title = string.Format("Bookmarks: '{0}'", WorkingDir);

			bookmark_timer = new DispatcherTimer();
			bookmark_timer.Interval = TimeSpan.FromMilliseconds(200);
			bookmark_timer.Tick += OnBookmarkTimerTick;

			rev_timer = new DispatcherTimer();
			rev_timer.Interval = TimeSpan.FromMilliseconds(200);
			rev_timer.Tick += OnRevTimerTick;

			string target_rev = TargetRevision;
			if (string.IsNullOrEmpty(target_rev))
			{
				var parents_info = UpdateContext.Cache.ParentsInfo;
				if (parents_info == null)
					parents_info = HgClient.Parents(WorkingDir);

				if (parents_info == null)
				{
					// error
					Close();
					return;
				}

				target_rev = parents_info.Rev.ToString();
			}

			textRev.Text = target_rev;

			if (UpdateContext.Cache.Bookmarks != null)
				UpdateBookmarks(UpdateContext.Cache.Bookmarks);
			else
				UpdateBookmarks();

			RefreshRev();

			if (RevDesc != null)
			{
				// Selecting target revision bookmark in combo box
				for (int i = 0; i < comboBookmarks.Items.Count; ++i)
				{
					var item = (BookmarkComboItem)comboBookmarks.Items[i];
					// FIXME: combo items store short sha1
					if (item.SHA1 == RevDesc.SHA1.ShortSHA1())
					{
						comboBookmarks.SelectedIndex = i;
						break;
					}
				}
			}

			RefreshBookmark();
			comboBookmarks.Focus();
		}

		//------------------------------------------------------------------
		private void UpdateBookmarks(List<BookmarkInfo> bookmarks)
		{
			var current_bookmark = comboBookmarks.Text;

			bookmarks_map.Clear();
			comboBookmarks.Items.Clear();

			int counter = 0;

			foreach (var bookmark in bookmarks)
			{
				var item = new BookmarkComboItem();
				item.GroupText = "Bookmark";
				item.Name = bookmark.Name;
				item.Rev = bookmark.Rev;
				item.SHA1 = bookmark.SHA1.ShortSHA1();
				item.Misc = bookmark.IsCurrent ? "Current" : "";

				comboBookmarks.Items.Add(item);
				bookmarks_map[bookmark.Name] = bookmark;

				if (bookmark.Name == current_bookmark)
					comboBookmarks.SelectedIndex = counter;

				counter++;
			}
		}

		//------------------------------------------------------------------
		private void UpdateBookmarks()
		{
			var bookmarks = HgClient.Bookmarks();
			UpdateBookmarks(bookmarks);
		}

		//-----------------------------------------------------------------------------
		private void UpdateButtonsState()
		{
			btnAdd.IsEnabled = (BookmarkDesc == null && !String.IsNullOrEmpty(comboBookmarks.Text));
			btnRemove.IsEnabled = (BookmarkDesc != null);
			btnMove.IsEnabled = (BookmarkDesc != null && RevDesc != null && BookmarkDesc.SHA1 != RevDesc.SHA1);
		}

		//------------------------------------------------------------------
		private void RefreshBookmark()
		{
			var bookmark_name = comboBookmarks.Text;

			BookmarkDesc = null;

			if (comboBookmarks.SelectedItem != null)
			{
				var item = (BookmarkComboItem)comboBookmarks.SelectedItem;
				if (item.Name == bookmark_name)
					BookmarkDesc = HgClient.GetRevisionDesc(item.SHA1);
			}

			textBookmarkDesc.Text = BookmarkDesc.GetDescription();
			UpdateButtonsState();
		}

		//------------------------------------------------------------------
		private void RefreshRev()
		{
			RevDesc = HgClient.GetRevisionDesc(textRev.Text);
			textRevDesc.Text = RevDesc.GetDescription();

			UpdateButtonsState();
		}

		//------------------------------------------------------------------
		private void OnBookmarkTimerTick(object o, EventArgs e)
		{
			bookmark_timer.Stop();
			RefreshBookmark();
		}

		//------------------------------------------------------------------
		private void OnRevTimerTick(object o, EventArgs e)
		{
			rev_timer.Stop();
			RefreshRev();
		}

		//------------------------------------------------------------------
		private void Window_Closed(object sender, EventArgs e)
		{
			bookmark_timer.Stop();
			bookmark_timer.Tick -= OnBookmarkTimerTick;

			rev_timer.Stop();
			rev_timer.Tick -= OnRevTimerTick;
		}

		//------------------------------------------------------------------
		private void btnAdd_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(comboBookmarks.Text))
			{
				MessageBox.Show("Bookmark name can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (RevDesc == null)
			{
				MessageBox.Show("Invalid target revision", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var bookmark_name = comboBookmarks.Text;

			var hg_bookmarks = new HgBookmarks();
			if (!hg_bookmarks.Add(WorkingDir, bookmark_name, RevDesc.SHA1))
			{
				var msg = String.Format("An error occured while adding bookmark '{0}'", bookmark_name);
				msg += "\nMake sure that bookmarks extension is enabled in your Mercurial.ini configuration file.";
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			else
			{
				var msg = String.Format("Bookmark '{0}' has been added", bookmark_name);
				MessageBox.Show(msg, "Information", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
			}

			UpdateContext.IsBookmarksChanged = true;

			UpdateBookmarks();
			RefreshBookmark();
			RefreshRev();
		}

		//------------------------------------------------------------------
		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		//------------------------------------------------------------------
		private void OnComboTextChanged(object sender, TextChangedEventArgs e)
		{
			bookmark_timer.Start();
			btnAdd.IsEnabled = false;
			btnRemove.IsEnabled = false;
			btnMove.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void btnMove_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(comboBookmarks.Text))
			{
				MessageBox.Show("Bookmark name can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (BookmarkDesc == null)
			{
				MessageBox.Show("Bookmark with that name is not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (RevDesc == null)
			{
				MessageBox.Show("Invalid target revision", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var bookmark_name = comboBookmarks.Text;

			var hg_bookmarks = new HgBookmarks();
			if (	!hg_bookmarks.Remove(WorkingDir, bookmark_name)
				||	!hg_bookmarks.Add(WorkingDir, bookmark_name, RevDesc.SHA1))
			{
				var msg = String.Format("An error occured while moving bookmark '{0}'", bookmark_name);
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			else
			{
				var msg = String.Format("Bookmark '{0}' has been moved", bookmark_name);
				MessageBox.Show(msg, "Information", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

			}

			UpdateContext.IsBookmarksChanged = true;

			UpdateBookmarks();
			RefreshBookmark();
			RefreshRev();
		}

		//------------------------------------------------------------------
		private void btnRemove_Click(object sender, RoutedEventArgs e)
		{
			if (String.IsNullOrEmpty(comboBookmarks.Text))
			{
				MessageBox.Show("Bookmark name can not be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (BookmarkDesc == null)
			{
				MessageBox.Show("Bookmark with that name is not exist", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			var bookmark_name = comboBookmarks.Text;

			var hg_bookmarks = new HgBookmarks();
			if (!hg_bookmarks.Remove(WorkingDir, bookmark_name))
			{
				var msg = String.Format("An error occured while removing bookmark '{0}'", bookmark_name);
				MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			else
			{
				var msg = String.Format("Bookmark '{0}' has been removed", bookmark_name);
				MessageBox.Show(msg, "Information", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

			}

			UpdateContext.IsBookmarksChanged = true;

			UpdateBookmarks();
			RefreshBookmark();
			RefreshRev();
		}

		//------------------------------------------------------------------
		private void targetRev_TextChanged(object sender, TextChangedEventArgs e)
		{
			rev_timer.Start();
			btnAdd.IsEnabled = false;
			btnRemove.IsEnabled = false;
			btnMove.IsEnabled = false;
		}

		//------------------------------------------------------------------
		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
				Close();
		}
	}

	//------------------------------------------------------------------
	class BookmarkComboItem
	{
		public string GroupText { get; set; }
		public string Name { get; set; }
		public int Rev { get; set; }
		public string SHA1 { get; set; }
		public string Misc { get; set; }
	}
}
