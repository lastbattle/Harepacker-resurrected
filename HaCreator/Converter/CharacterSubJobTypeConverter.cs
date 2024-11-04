using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HaCreator.Converter
{
    public class CharacterSubJobTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long subJob)
            {
                return CharacterSubJobFlagTypeExt.ToEnum((int) subJob);
            }
            return CharacterSubJobFlagType.Adventurer.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CharacterSubJobFlagType jobType)
            {
                return (long)jobType;
            }
            return (long) CharacterSubJobFlagType.Adventurer;
        }
    }
}
