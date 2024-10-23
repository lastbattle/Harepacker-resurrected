using System;
using System.Collections.Generic;

namespace HaCreator.GUI.Quest
{
    public enum QuestEditorCheckDayOfWeekType
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday
    }

    public static class QuestEditorCheckDayOfWeekTypeExt
    {
        private static readonly Dictionary<string, QuestEditorCheckDayOfWeekType> StringToEnum = new(StringComparer.OrdinalIgnoreCase)
        {
            {"Monday", QuestEditorCheckDayOfWeekType.Monday},
            {"Tuesday", QuestEditorCheckDayOfWeekType.Tuesday},
            {"Wednesday", QuestEditorCheckDayOfWeekType.Wednesday},
            {"Thursday", QuestEditorCheckDayOfWeekType.Thursday},
            {"Friday", QuestEditorCheckDayOfWeekType.Friday},
            {"Saturday", QuestEditorCheckDayOfWeekType.Saturday},
            {"Sunday", QuestEditorCheckDayOfWeekType.Sunday},
        };

        /// <summary>
        /// Converts the string name in wz to QuestEditorCheckDayOfWeekType
        /// </summary>
        /// <param name="dayString"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static QuestEditorCheckDayOfWeekType FromWzString(string dayString)
        {
            if (StringToEnum.TryGetValue(dayString, out var result))
            {
                return result;
            }
            throw new ArgumentException($"Invalid dayOfWeek type: {dayString}", nameof(dayString));
        }

        /// <summary>
        /// Converts QuestEditorActInfoPotentialType to its string name in Wz
        /// </summary>
        /// <param name="dayOfWeek"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ToWzString(this QuestEditorCheckDayOfWeekType dayOfWeek)
        {
            foreach (var kvp in StringToEnum)
            {
                if (kvp.Value == dayOfWeek)
                {
                    return kvp.Key;
                }
            }
            throw new ArgumentException($"Invalid dayOfWeek type: {dayOfWeek}", nameof(dayOfWeek));
        }
    }
}