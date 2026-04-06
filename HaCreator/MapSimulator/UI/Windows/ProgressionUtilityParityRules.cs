using System;
using System.Globalization;
using HaCreator.MapSimulator.Interaction;

namespace HaCreator.MapSimulator.UI
{
    internal static class ProgressionUtilityParityRules
    {
        private const string RankingLandingUrlFallbackFormat = "http://{0}.nexon.com/maplestory/page/Gnxgame.aspx?URL=webclient/totpersonrank&worldid={1}&characterid={2}";

        internal static string ResolveRankingLandingUrl(string serverHost, int templateId, int worldId, int characterId, out bool usedResolvedTemplate)
        {
            string normalizedHost = string.IsNullOrWhiteSpace(serverHost) ? "unknown" : serverHost.Trim();
            int normalizedTemplateId = Math.Max(0, templateId);
            int normalizedWorldId = Math.Max(0, worldId);
            int normalizedCharacterId = Math.Max(0, characterId);

            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                normalizedTemplateId,
                RankingLandingUrlFallbackFormat,
                maxPlaceholderCount: 3,
                out usedResolvedTemplate);

            return string.Format(
                CultureInfo.InvariantCulture,
                compositeFormat,
                normalizedHost,
                normalizedWorldId,
                normalizedCharacterId);
        }

        internal static string FormatRankingLandingSeed(string serverHost, int templateId, int worldId, int characterId, out bool usedResolvedTemplate)
        {
            string resolvedUrl = ResolveRankingLandingUrl(serverHost, templateId, worldId, characterId, out usedResolvedTemplate);
            return string.Format(
                CultureInfo.InvariantCulture,
                "StringPool[0x{0:X}] => {1}",
                Math.Max(0, templateId),
                resolvedUrl);
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
