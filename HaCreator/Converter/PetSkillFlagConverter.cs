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
    public class PetSkillFlagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long amount)
            {
                foreach (PetSkillFlag flag in Enum.GetValues(typeof(PetSkillFlag)))
                {
                    if (flag != PetSkillFlag.NUM_SKILL && flag.GetValue() == amount)
                    {
                        return flag;
                    }
                }
            }
            return PetSkillFlag.PickupMeso; // Default value
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PetSkillFlag flag)
            {
                return (long)flag.GetValue();
            }
            return 0; // Default value
        }
    }
}