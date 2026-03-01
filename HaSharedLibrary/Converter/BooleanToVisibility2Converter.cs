using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace HaSharedLibrary.Converter
{
    /// <summary>
    /// Boolean to System.Windows.Visiblity converter.
    /// If true, return Visiblity.Visible
    /// otherelse return Visibility.Collapsed
    /// </summary>
    public class BooleanToVisibility2Converter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool res = (bool)value;

            if (res)
                return Visibility.Visible;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;

            return visibility == Visibility.Visible ? true : false;
        }
    }
}
