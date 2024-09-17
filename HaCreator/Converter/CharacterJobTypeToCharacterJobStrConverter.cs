using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace HaCreator.Converter
{
    public class CharacterJobTypeToCharacterJobStrConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CharacterClassType job)
            {
                return job.ToString();
            }
            return CharacterClassType.NULL.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string jobName)
            {
                CharacterClassType job = (CharacterClassType)Enum.Parse(typeof(CharacterClassType), jobName);

                return job;
            }
            return CharacterClassType.NULL;
        }
    }
}