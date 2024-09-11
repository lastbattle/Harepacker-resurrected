using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaCreator.Converter
{

    public class ItemIdToItemNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            int itemId = 0;
            if (value is int)
            {
                itemId = (int)value;
            } else
            {
                itemId = (int) ((long)value);
            }

            const string NO_NAME = "NO NAME";

            if (!Program.InfoManager.ItemNameCache.ContainsKey(itemId))
            {
                return NO_NAME;
            }
            Tuple<string, string, string> nameCache = Program.InfoManager.ItemNameCache[itemId]; // // itemid, <item category, item name, item desc>
            if (nameCache != null)
            {
                return nameCache.Item2;
            }
            return NO_NAME;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}