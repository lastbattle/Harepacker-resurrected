using HaRepacker.GUI.Controls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaRepacker.Converter
{
	/// <summary>
	/// Converts PointF X and Y coordinates to homosapien-ape readable string
	/// </summary>
	public class PointFOriginToStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
            NotifyPointF point = (PointF)value;

			return string.Format("X {0}, Y {1}", point.X, point.Y);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return new NotifyPointF(0,0); // anyway wtf
		}
	}
}
