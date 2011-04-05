//=========================================================================
// Copyright 2011 Sergey Antonov <sergant_@mail.ru>
// 
// This software may be used and distributed according to the terms of the
// GNU General Public License version 2 as published by the Free Software
// Foundation.
// 
// See the file COPYING.TXT for the full text of the license, or see
// http://www.gnu.org/licenses/gpl-2.0.txt
// 
//=========================================================================

using System.Windows.Controls;

namespace HgSccHelper
{
	public interface IOptionsPage
	{
		string PageName { get; }
		bool Save();
		ContentControl PageContent { get; }
	}
}
