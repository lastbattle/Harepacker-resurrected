/* Copyright (C) 2020 LastBattle
* https://github.com/lastbattle/Harepacker-resurrected
* 
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace HaRepacker.Converter
{
	/// <summary>
	/// Convert ticks to human readable relative time
	/// </summary>
	public class TicksToRelativeTimeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			long ticks = (long)value;

            const int SECOND = 1;
            const int MINUTE = 60 * SECOND;
            const int HOUR = 60 * MINUTE;
            const int DAY = 24 * HOUR;
            const int MONTH = 30 * DAY;

            var ts = new TimeSpan(ticks);
            double delta = Math.Abs(ts.TotalSeconds);

            string.Format(HaRepacker.Properties.Resources.RelativeTime_SecondsAgo, ts.Seconds);
            if (delta < 1 * MINUTE)
                return string.Format(HaRepacker.Properties.Resources.RelativeTime_SecondsAgo, ts.Seconds);

            //if (delta < 2 * MINUTE)
            //    return "a minute ago";

            if (delta < 45 * MINUTE)
                return string.Format(HaRepacker.Properties.Resources.RelativeTime_MinutesAgo, ts.Minutes);

            //if (delta < 90 * MINUTE)
            //    return "an hour ago";

            if (delta < 24 * HOUR)
                return string.Format(HaRepacker.Properties.Resources.RelativeTime_HoursAgo, ts.Hours);

            //if (delta < 48 * HOUR)
            //    return "yesterday";

            if (delta < 30 * DAY)
                return string.Format(HaRepacker.Properties.Resources.RelativeTime_DaysAgo, ts.Days);

            if (delta < 12 * MONTH)
            {
                int months = (int) (Math.Floor((double)ts.Days / 30));
                return string.Format(HaRepacker.Properties.Resources.RelativeTime_MonthsAgo, months);
            }
            else
            {
                int years = (int) (Math.Floor((double)ts.Days / 365));
                return string.Format(HaRepacker.Properties.Resources.RelativeTime_YearsAgo, years);
            }
        }

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return 0; // faek value
		}
	}
}
