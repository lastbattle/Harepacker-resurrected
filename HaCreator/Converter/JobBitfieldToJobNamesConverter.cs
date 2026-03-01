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
    public class JobBitfieldToJobNamesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long bitfield = (long)value;

            IEnumerable<string> classNames = Enum.GetValues(typeof(CharacterJobPreBBType))
                .Cast<CharacterJobPreBBType>()
                .Where(c => c != CharacterJobPreBBType.None)
                .Where(c => ((CharacterJobPreBBType)bitfield).HasFlag(c))
                .Select(c =>
                {
                    string jobName = c.ToString();

                    // Add spaces between words
                    string ret = string.Concat(jobName.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).Trim();
                    return ret;
                });

            if (classNames.Count() == 0)
            {
                return string.Empty;
            }
            return string.Join(", ", classNames);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

    }
}

