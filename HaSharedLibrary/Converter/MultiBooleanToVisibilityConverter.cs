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
    /// All must be false to be visible.
    /// </summary>
    public class MultiBooleanToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values == null || values.Any(v => v == DependencyProperty.UnsetValue))
                return Visibility.Collapsed;

            // Check if all values are boolean and true
            bool visible = values.All(v => v is bool && (bool)v);

            // Invert logic if parameter is provided and true
            if (parameter is bool invertLogic && invertLogic)
            {
                /*<MultiBinding.ConverterParameter>
                 * <sys:Boolean>true</sys:Boolean>
                 * </MultiBinding.ConverterParameter>*/
                visible = !visible;
            }

            return visible ? Visibility.Collapsed : Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}