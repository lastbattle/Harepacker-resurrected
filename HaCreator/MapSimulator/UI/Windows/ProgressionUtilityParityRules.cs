using System;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    internal static class ProgressionUtilityParityRules
    {
        internal static string FormatRankingLandingSeed(string serverHost, int templateId, int worldId, int characterId)
        {
            string normalizedHost = string.IsNullOrWhiteSpace(serverHost) ? "unknown" : serverHost.Trim();
            int normalizedTemplateId = Math.Max(0, templateId);
            int normalizedWorldId = Math.Max(0, worldId);
            int normalizedCharacterId = Math.Max(0, characterId);
            return string.Format(
                CultureInfo.InvariantCulture,
                "StringPool[0x{0:X}](server={1}, nWorldID={2}, dwCharacterID={3})",
                normalizedTemplateId,
                normalizedHost,
                normalizedWorldId,
                normalizedCharacterId);
        }

        internal static int ResolveCalendarBackgroundVariant(DateTime month)
        {
            return GetCalendarWeekRowCount(month) >= 6 ? 1 : 0;
        }

        internal static int GetCalendarWeekRowCount(DateTime month)
        {
            DateTime normalizedMonth = new(month.Year, month.Month, 1);
            int leadingDays = (int)normalizedMonth.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(normalizedMonth.Year, normalizedMonth.Month);
            int totalCells = leadingDays + daysInMonth;
            return (int)Math.Ceiling(totalCells / 7d);
        }
    }
}
