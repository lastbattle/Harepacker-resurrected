using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.Converter
{
    public class QuestIdToQuestNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            long questId = (long)value;

            if (!Program.InfoManager.QuestInfos.ContainsKey(questId.ToString()))
            {
                return string.Empty;
            }
            WzSubProperty questProp = Program.InfoManager.QuestInfos[questId.ToString()];
            
            string questName = (questProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
            return questName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}