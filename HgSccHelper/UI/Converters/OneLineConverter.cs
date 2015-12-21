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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HgSccHelper.UI.Converters
{
	//==================================================================
	public class OneLineConverter : IValueConverter
	{
		char[] new_line = new char[] { '\r', '\n' };

		//------------------------------------------------------------------
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value != null)
			{
				var trimmed = (string)value;
				foreach(char sep in new_line)
					trimmed = trimmed.Replace(sep, ' ');

				return trimmed;
			}

			return string.Empty;
		}

		//------------------------------------------------------------------
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new Exception("The method or operation is not implemented.");
		}
	}
}
