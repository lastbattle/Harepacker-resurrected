using HaSharedLibrary.Wz;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaCreator.Converter
{
    public class SkillIdToSkillNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            int skillId = (int)value;
            string skillIdStr = skillId < 10000000 ? WzInfoTools.AddLeadingZeros(skillId.ToString(), 7) : skillId.ToString(); // 80001033 0001000

            if (!Program.InfoManager.SkillNameCache.ContainsKey(skillIdStr))
            {
                return string.Empty;
            }
            Tuple<string, string> npcName = Program.InfoManager.SkillNameCache[skillIdStr]; // skillName, skillDesc

            return npcName.Item1;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}