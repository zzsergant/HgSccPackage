//=========================================================================
// Copyright 2010 Sergey Antonov <sergant_@mail.ru>
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
using System.Windows.Data;
using System.Windows.Media;

namespace HgSccHelper.UI.Converters
{
	class HgStatusToColorConverter : IValueConverter
	{
		private Dictionary<HgFileStatus, Color> status_colors;
		private Dictionary<HgFileStatus, Brush> status_brush;

		//-----------------------------------------------------------------------------
		public HgStatusToColorConverter()
		{
			// TODO: Read colors from configuration

			status_colors = new Dictionary<HgFileStatus, Color>();

			var theme = HgSccHelper.UI.ThemeManager.Instance.Current;
			if (theme.Name == "Dark")
			{
				status_colors[HgFileStatus.Added] = Colors.LightBlue;
				status_colors[HgFileStatus.Removed] = Colors.LightSalmon;
				status_colors[HgFileStatus.Modified] = Colors.LightGreen;
				status_colors[HgFileStatus.Deleted] = Colors.Salmon;
				status_colors[HgFileStatus.Ignored] = Colors.LightGray;
				status_colors[HgFileStatus.Clean] = Colors.White;
			}
			else
			{
				status_colors[HgFileStatus.Added] = Colors.Blue;
				status_colors[HgFileStatus.Removed] = Colors.Red;
				status_colors[HgFileStatus.Modified] = Colors.Green;
				status_colors[HgFileStatus.Deleted] = Colors.Maroon;
				status_colors[HgFileStatus.Ignored] = Colors.Gray;
				status_colors[HgFileStatus.Clean] = Colors.Black;
			}

			status_brush = new Dictionary<HgFileStatus, Brush>();
			foreach (var pair in status_colors)
			{
				var brush = new SolidColorBrush(pair.Value);
				brush.Freeze();

				status_brush[pair.Key] = brush;
			}
		}

		//-----------------------------------------------------------------------------
		private HgFileStatus ToHgFileStatus(FileStatus file_status)
		{
			switch (file_status)
			{
				case FileStatus.Added:
					return HgFileStatus.Added;
				case FileStatus.Modified:
					return HgFileStatus.Modified;
				case FileStatus.Removed:
					return HgFileStatus.Removed;
				default:
					throw new ArgumentOutOfRangeException("file_status");
			}
		}

		//------------------------------------------------------------------
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			HgFileStatus status;
			if (Equals(parameter, "FileStatus"))
				status = ToHgFileStatus((FileStatus)value);
			else 
				status = (HgFileStatus)value;
			

			if (targetType == typeof(Color))
			{
				Color c;
				if (status_colors.TryGetValue(status, out c))
					return c;

				return Colors.Black;
			}

			if (targetType == typeof(Brush))
			{
				Brush b;
				if (status_brush.TryGetValue(status, out b))
					return b;

				return Brushes.Black;
			}

			throw new ArgumentException(String.Format("Unsupported type: {0}", targetType));
		}

		//------------------------------------------------------------------
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new Exception("The method or operation is not implemented.");
		}
	}
}
