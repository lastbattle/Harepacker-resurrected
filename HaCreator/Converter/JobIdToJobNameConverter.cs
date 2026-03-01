using HaSharedLibrary.Wz;
using MapleLib.Helpers;
using MapleLib.WzLib.WzStructure.Data.CharacterStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaCreator.Converter
{
    public class JobIdToJobNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int jobId)
            {
                if (Enum.IsDefined(typeof(CharacterJob), jobId))
                {
                    CharacterJob jobClass = (CharacterJob)jobId;
                    return jobClass.GetFormattedJobName(false);
                }

                string error = string.Format("[JobIdToJobNameConverter] Missing job name. Id='{0}'.\r\nPlease add it to MapleLib.. CharacterJob.cs", jobId);
                ErrorLogger.Log(ErrorLevel.MissingFeature, error);
            }
            return "Unknown Job";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}