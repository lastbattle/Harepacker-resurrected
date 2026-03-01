using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaCreator.Converter
{
    public class EnumToIntValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return 0;

            Type enumType = value.GetType();
            if (!enumType.IsEnum)
                throw new ArgumentException("Value must be an enum", nameof(value));

            Type underlyingType = Enum.GetUnderlyingType(enumType);
            if (underlyingType == typeof(int))
            {
                return System.Convert.ToInt32(value);
            }

            // If the enum is not based on int, return the original value
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Implement if needed, or throw NotImplementedException
            throw new NotImplementedException();
        }
    }
}