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

namespace HgSccPackage
{
	/// <summary>
	/// This class is used to expose the list of the IDs of the commands implemented
	/// by the client package. This list of IDs must match the set of IDs defined inside the
	/// VSCT file.
	/// </summary>
	public static class CommandId
	{
		// Define the list a set of public static members.

		// Define the list of menus (these include toolbars)
		public const int imnuToolWindowToolbarMenu		= 0x201;
		public const int imnuHgSccSlnMenu				= 0x202;

		public const int icmdOptions					= 0x09F;
		public const int icmdAddToSourceControl			= 0x100;
		public const int icmdClone						= 0x101;
		public const int icmdSynchronize				= 0x102;
		public const int icmdCommit						= 0x103;
		public const int icmdRevert						= 0x104;
		public const int icmdCompare					= 0x105;
		public const int icmdViewHistory				= 0x106;
        public const int icmdAnnotate                   = 0x107;
        public const int icmdGrep						= 0x108;
		public const int icmdViewChangeLog				= 0x109;
		public const int icmdUpdate						= 0x110;
        public const int icmdTags                       = 0x111;
        public const int icmdRefreshStatus              = 0x112;
        public const int icmdChangeSccBindings			= 0x113;

		public const int icmdPendingTask				= 0x150;

		// Define the list of icons (use decimal numbers here, to match the resource IDs)
		public const int iiconProductIcon				= 400;

		// Define the list of bitmaps (use decimal numbers here, to match the resource IDs)
		public const int ibmpToolbarMenusImages			= 500;
		public const int ibmpToolWindowsImages			= 501;

		// Glyph indexes in the bitmap used for tolwindows (ibmpToolWindowsImages)
		public const int iconSccProviderToolWindow		= 0;
	}
}
