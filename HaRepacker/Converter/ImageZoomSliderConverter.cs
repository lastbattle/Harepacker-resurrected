using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaRepacker.Converter
{
	/// <summary>
	/// Rounds a double to 2 decimal place, and an even number
	/// </summary>
	public class ImageZoomSliderConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			double zoom = (double)value;
			zoom = Math.Round(zoom, 2); // to 2 decimal place
			//System.Diagnostics.Debug.WriteLine(((zoom * 100)));
			if ((zoom * 100) % 2 != 0) // odd number
				zoom += 0.01;

			//System.Diagnostics.Debug.WriteLine("Zoom: " + zoom + " " + ((zoom * 100)));
			return zoom;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return (double)value;
		}
	}
}
