using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaCreator.Converter
{

    public class NpcIdToNpcNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            long npcId = (long)value;

            if (!Program.InfoManager.NpcNameCache.ContainsKey(npcId.ToString()))
            {
                return string.Empty;
            }
            string npcName = Program.InfoManager.NpcNameCache[npcId.ToString()].Item1;

            return npcName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}