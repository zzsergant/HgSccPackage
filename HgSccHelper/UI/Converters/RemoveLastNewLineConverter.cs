using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;

namespace HgSccHelper.UI.Converters
{
	//==================================================================
	public class RemoveLastNewLineConverter : IValueConverter
	{
		char[] new_line = new char[] { '\r', '\n' };

		//------------------------------------------------------------------
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value != null)
			{
				var trimmed = ((string)value).TrimEnd(new_line);
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
