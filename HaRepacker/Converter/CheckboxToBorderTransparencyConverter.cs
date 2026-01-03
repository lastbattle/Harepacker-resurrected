using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HaRepacker.Converter
{
	/// <summary>
	/// Converts CheckBox IsChecked to Visiblity (Transparency)
	/// </summary>
	public class CheckboxToBorderTransparencyConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			bool? isChecked = (bool?)value;

			if (isChecked == true)
			{
				return new SolidColorBrush(Colors.Gray);
			}
			return new SolidColorBrush(Colors.Transparent);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return false; // faek value
		}
	}
}
