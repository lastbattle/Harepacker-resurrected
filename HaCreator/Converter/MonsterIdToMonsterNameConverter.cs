using HaSharedLibrary.Wz;
using System;
using System.Globalization;
using System.Windows.Data;

namespace HaCreator.Converter
{
    public class MonsterIdToMonsterNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            int mobId = 0;
            if (value is int)
            {
                mobId = (int)value;
            }
            else
            {
                mobId = (int)((long)value);
            }

            string mobIdStr = WzInfoTools.AddLeadingZeros(mobId.ToString(), 7);

            const string NO_NAME = "NO NAME";

            if (!Program.InfoManager.MobNameCache.ContainsKey(mobIdStr))
                return NO_NAME;

            string nameCache = Program.InfoManager.MobNameCache[mobIdStr]; // // itemid, <item category, item name, item desc>
            return nameCache;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}