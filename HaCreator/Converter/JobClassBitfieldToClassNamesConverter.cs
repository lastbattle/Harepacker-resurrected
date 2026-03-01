using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaCreator.Converter
{
    public class JobClassBitfieldToClassNamesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int bitfield = (int)value;

            IEnumerable<string> classNames = Enum.GetValues(typeof(CharacterClassType))
                .Cast<CharacterClassType>()
                .Where(c => c != CharacterClassType.NULL && c != CharacterClassType.UltimateAdventurer)
                .Where(c => (bitfield & (1 << (int)c)) != 0)
                .Select(c => c.ToString());

            if (classNames.Count() == 0)
            {
                return "ALL CLASSES";
            }
            return string.Join(", ", classNames);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}

