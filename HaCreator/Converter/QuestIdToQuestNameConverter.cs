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

            string questIdString;
            if (value is long longValue)
            {
                questIdString = longValue.ToString();
            }
            else if (value is int intValue)
            {
                questIdString = intValue.ToString();
            }
            else
            {
                return string.Empty;
            }

            if (!Program.InfoManager.QuestInfos.ContainsKey(questIdString))
            {
                return string.Empty;
            }

            WzSubProperty questProp = Program.InfoManager.QuestInfos[questIdString];

            string questName = (questProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
            return questName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}