//=========================================================================
// Copyright 2015 Sergey Antonov <sergant_@mail.ru>
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
using System.Windows;

namespace HgSccHelper.UI.Converters
{
	//==================================================================
	public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
	{
		//------------------------------------------------------------------
		public object Convert(object value, Type target_type, object parameter,
			System.Globalization.CultureInfo culture)
		{
			if (target_type != typeof(Visibility))
				throw new InvalidOperationException("The target must be a Visibility enum");

			return (bool)value ? Visibility.Visible : Visibility.Collapsed;
		}

		//------------------------------------------------------------------
		public object ConvertBack(object value, Type target_type, object parameter,
			System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
