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
using System.Windows.Input;

namespace HgSccHelper.UI
{
	static class Commands
	{
		//-----------------------------------------------------------------------------
		public static RoutedUICommand AnnotateCommand = new RoutedUICommand("Annotate",
			"Annotate", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ArchiveCommand = new RoutedUICommand("Archive",
			"Archive", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand BundleCommand = new RoutedUICommand("Bundle",
			"Bundle", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand CloneCommand = new RoutedUICommand("Clone",
			"Clone", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand FileHistoryCommand = new RoutedUICommand("File History",
			"FileHistory", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffParent1Command = new RoutedUICommand("Diff Parent 1",
			"DiffParent1", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffParent2Command = new RoutedUICommand("Diff Parent 2",
			"DiffParent2", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffPreviousCommand = new RoutedUICommand("Diff Previous",
			"DiffPrevious", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand DiffTwoRevisionsCommand = new RoutedUICommand("Diff Two Revisions",
			"DiffTwoRevisions", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand GrepCommand = new RoutedUICommand("Grep",
			"Grep", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand MergeCommand = new RoutedUICommand("Merge",
			"Merge", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand StopCommand = new RoutedUICommand("Stop",
			"Stop", typeof(CloneWindow));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand TagsCommand = new RoutedUICommand("Add/Remove Tag",
			"Tags", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand BookmarksCommand = new RoutedUICommand("Bookmarks",
			"Bookmarks", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand UpdateCommand = new RoutedUICommand("Update to Revision",
			"Update", typeof(Commands));

		//-----------------------------------------------------------------------------
		public static RoutedUICommand ViewFileCommand = new RoutedUICommand("View File",
			"ViewFile", typeof(Commands));
	}
}
