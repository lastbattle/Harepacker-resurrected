using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaRepacker.Converter
{
    /// <summary>
    /// Converts the double value of an image size to integer
    /// </summary>
    public class ImageSizeDoubleToIntegerConverter : IValueConverter
    {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			double value_ = (double)value;

			return (int)value_;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			// cant really convert back here anyway
			double value_ = (double)value;

			return (int)value_;
		}
	}
}
