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
using System.Text;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace HgSccHelper
{
	//==================================================================
	class GridViewColumnSorter
	{
		GridViewColumnHeader last_header_clicked;
		ListSortDirection last_direction;
		ListView list_view;
		List<GridViewColumn> excluded_columns;

		//------------------------------------------------------------------
		public GridViewColumnSorter(ListView list_view)
		{
			this.list_view = list_view;
			this.last_direction = ListSortDirection.Ascending;
			this.excluded_columns = new List<GridViewColumn>();
		}

		//------------------------------------------------------------------
		public void ExcludeColumn(GridViewColumn column)
		{
			excluded_columns.Add(column);
		}

		//------------------------------------------------------------------
		private bool IsExcluded(GridViewColumn column)
		{
			return excluded_columns.Contains(column);
		}
		
		//------------------------------------------------------------------
		public void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
		{
			var headerClicked = e.OriginalSource as GridViewColumnHeader;
			ListSortDirection direction;

			if (headerClicked != null)
			{
				if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
				{
					if (IsExcluded(headerClicked.Column))
						return;

					if (headerClicked != last_header_clicked)
						direction = ListSortDirection.Ascending;
					else
						if (last_direction == ListSortDirection.Ascending)
							direction = ListSortDirection.Descending;
						else
							direction = ListSortDirection.Ascending;

					string sort_path = headerClicked.Column.Header as string;
					var binding = headerClicked.Column.DisplayMemberBinding as Binding;
					if (binding != null)
					{
						sort_path = binding.Path.Path;
					}
					
					Sort(sort_path, direction);

					last_header_clicked = headerClicked;
					last_direction = direction;
				}
			}
		}

		//------------------------------------------------------------------
		private void Sort(string property_name, ListSortDirection direction)
		{
			var dataView = CollectionViewSource.GetDefaultView(list_view.ItemsSource);
            if (dataView == null)
                return;

			dataView.SortDescriptions.Clear();
			SortDescription sd = new SortDescription(property_name, direction);
			dataView.SortDescriptions.Add(sd);
			dataView.Refresh();
		}
	}
}
