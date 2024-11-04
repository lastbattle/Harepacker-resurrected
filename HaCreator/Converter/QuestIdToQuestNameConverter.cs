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
            const string NO_NAME = "NO NAME";

            if (value == null)
                return NO_NAME;

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
                return NO_NAME;
            }

            if (!Program.InfoManager.QuestInfos.ContainsKey(questIdString))
            {
                return NO_NAME;
            }

            WzSubProperty questProp = Program.InfoManager.QuestInfos[questIdString];

            string questName = (questProp["name"] as WzStringProperty)?.Value ?? NO_NAME;
            return questName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}