using HaRepacker.GUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;

namespace HaRepacker.Converter {

    /// <summary>
    /// Boolean to System.Windows.Visiblity converter.
    /// If true, return Visiblity.Collapsed
    /// otherelse return Visibility.Visible
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            bool res = (bool)value;

            if (res)
                return Visibility.Collapsed;

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            Visibility visibility = (Visibility)value;

            return visibility == Visibility.Collapsed ? true: false;
        }
    }
}
