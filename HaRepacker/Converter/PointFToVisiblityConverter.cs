using HaRepacker.GUI.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace HaRepacker.Converter
{
	/// <summary>
	/// PointF to System.Windows.Visiblity converter.
	/// Returns Visiblity.Visible if the X and Y coordinates of PointF is not 0,
	/// otherwise Visiblity.Collapsed
	/// </summary>
    public class PointFToVisiblityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
            NotifyPointF point = (NotifyPointF)value;

			if (point.X == 0 && point.Y == 0)
				return Visibility.Collapsed;

			return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return new NotifyPointF(0, 0); // anyway wtf
		}
	}
}
