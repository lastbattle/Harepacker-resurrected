using HaCreator.MapEditor;
using HaSharedLibrary.Wz;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaCreator.Converter
{
    public class MapIdToMapNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            int mapId = (int)value;
            string mapStr = WzInfoTools.AddLeadingZeros(mapId.ToString(), 9); // 180000000

            // street name, map name, category name
            if (!Program.InfoManager.MapsNameCache.ContainsKey(mapStr)) 
            {
                return string.Empty;
            }
            Tuple<string, string, string> map = Program.InfoManager.MapsNameCache[mapStr];
            string mapName = string.Format("{0} - {1}", map.Item1, map.Item2);

            return mapName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}