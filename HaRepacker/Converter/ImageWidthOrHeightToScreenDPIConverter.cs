using HaRepacker.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaRepacker.Converter
{
	/// <summary>
	/// Converts the image (x, y) width or height to the correct size according to the screen's DPI scaling factor
	/// </summary>
    public class ImageWidthOrHeightToScreenDPIConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			int widthOrHeight = (int)value;
			int realWidthOrHeightToDisplay = (int) ((double) widthOrHeight * ScreenDPIUtil.GetScreenScaleFactor());

			return realWidthOrHeightToDisplay;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
            int value_ = (int)value;
            int imageWidthOrHeight = (int) ((double)value_ / ScreenDPIUtil.GetScreenScaleFactor());

			return imageWidthOrHeight;
		}
	}
}
