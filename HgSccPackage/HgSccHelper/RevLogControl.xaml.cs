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

using System.Collections.Generic;
using System.Windows.Controls;

namespace HgSccHelper
{
	/// <summary>
	/// Interaction logic for RevLogControl.xaml
	/// </summary>
	public partial class RevLogControl : UserControl
	{
		List<RevLogChangeDesc> revs;
		List<RevLogLinesPair> rev_lines;

		//------------------------------------------------------------------
		public RevLogControl()
		{
			InitializeComponent();

			VirtualizingStackPanel.SetIsVirtualizing(listView1, true);
			VirtualizingStackPanel.SetVirtualizationMode(listView1, VirtualizationMode.Recycling);
		}

		//------------------------------------------------------------------
		internal void SetRevs(List<RevLogChangeDesc> rev_log)
		{
			this.revs = rev_log;
			this.rev_lines = new List<RevLogLinesPair>(
				RevLogLinesPair.FromV1(RevLogIterator.GetLines(revs)));

			listView1.ItemsSource = rev_lines;
		}
	}
}
