using System;
using System.Globalization;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal enum EventAlarmInteractionKind
    {
        RowSelection = 0,
        FilterControl = 1,
        CalendarToggle = 2,
        CalendarDateSelection = 3,
        CalendarMonthNavigation = 4,
    }

    internal static class ProgressionUtilityParityRules
    {
        private const string RankingLandingUrlFallbackFormat = "http://{0}.nexon.com/maplestory/page/Gnxgame.aspx?URL=webclient/totpersonrank&worldid={1}&characterid={2}";
        private const string RankingLandingTemplateFallbackFormat = "http://%s.nexon.com/maplestory/page/Gnxgame.aspx?URL=webclient/totpersonrank&worldid=%d&characterid=%d";

        internal static string ResolveRankingLandingTemplate(int templateId, out bool usedResolvedTemplate)
        {
            string resolvedTemplate = MapleStoryStringPool.GetOrFallback(
                Math.Max(0, templateId),
                RankingLandingTemplateFallbackFormat);
            usedResolvedTemplate = !string.Equals(
                resolvedTemplate,
                RankingLandingTemplateFallbackFormat,
                StringComparison.Ordinal);
            return resolvedTemplate;
        }

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

        internal static string FormatRankingLandingTemplateSeed(int templateId, out bool usedResolvedTemplate)
        {
            string resolvedTemplate = ResolveRankingLandingTemplate(templateId, out usedResolvedTemplate);
            return string.Format(
                CultureInfo.InvariantCulture,
                "StringPool[0x{0:X}] => {1}",
                Math.Max(0, templateId),
                resolvedTemplate);
        }

        internal static string FormatRankingRequestParameters(int worldId, int characterId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "webclient/totpersonrank worldid={0}, characterid={1}",
                Math.Max(0, worldId),
                Math.Max(0, characterId));
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

        internal static bool ShouldKeepEventAlarmOwnerVisible(EventAlarmInteractionKind interactionKind)
        {
            return interactionKind is EventAlarmInteractionKind.FilterControl
                or EventAlarmInteractionKind.CalendarToggle
                or EventAlarmInteractionKind.CalendarDateSelection
                or EventAlarmInteractionKind.CalendarMonthNavigation;
        }

        internal static Point ResolveEventAlarmFallbackCloseButtonPosition(int frameWidth)
        {
            int normalizedWidth = Math.Max(0, frameWidth);
            return new Point(Math.Max(8, normalizedWidth - 22), 10);
        }

        internal static RankingWindowSnapshot ApplyPacketOwnedRankingOwnerState(
            RankingWindowSnapshot fallback,
            PacketOwnedRankingOwnerStateSnapshot ownerState)
        {
            fallback ??= new RankingWindowSnapshot();
            if (ownerState?.HasAnyState != true)
            {
                return fallback;
            }

            string serverHost = ownerState.ServerHost?.Trim() ?? string.Empty;
            int templateId = ownerState.TemplateId > 0 ? ownerState.TemplateId : 0xAA2;
            int? worldId = ownerState.WorldId;
            int? characterId = ownerState.CharacterId;
            string composedNavigateUrl = !string.IsNullOrWhiteSpace(ownerState.NavigateUrl)
                ? ownerState.NavigateUrl.Trim()
                : !string.IsNullOrWhiteSpace(serverHost) && worldId.HasValue && characterId.HasValue
                    ? ResolveRankingLandingUrl(serverHost, templateId, worldId.Value, characterId.Value, out _)
                    : string.Empty;
            string composedCaption = !string.IsNullOrWhiteSpace(ownerState.NavigationCaption)
                ? ownerState.NavigationCaption.Trim()
                : templateId > 0
                    ? FormatRankingLandingTemplateSeed(templateId, out _)
                    : string.Empty;
            string composedSeedText = !string.IsNullOrWhiteSpace(ownerState.NavigationSeedText)
                ? ownerState.NavigationSeedText.Trim()
                : !string.IsNullOrWhiteSpace(composedNavigateUrl)
                    ? $"NavigateUrl => {composedNavigateUrl}"
                    : string.Empty;
            string composedHostText = !string.IsNullOrWhiteSpace(ownerState.NavigationHostText)
                ? ownerState.NavigationHostText.Trim()
                : !string.IsNullOrWhiteSpace(serverHost)
                    ? $"get_server_string_0 => {serverHost}"
                    : string.Empty;
            string composedRequestText = !string.IsNullOrWhiteSpace(ownerState.NavigationRequestText)
                ? ownerState.NavigationRequestText.Trim()
                : worldId.HasValue && characterId.HasValue
                    ? FormatRankingRequestParameters(worldId.Value, characterId.Value)
                    : string.Empty;

            return new RankingWindowSnapshot
            {
                Title = fallback.Title,
                Subtitle = ChooseOwnerText(ownerState.Subtitle, fallback.Subtitle),
                StatusText = ChooseOwnerText(ownerState.StatusText, fallback.StatusText),
                NavigationCaption = ChooseOwnerText(composedCaption, fallback.NavigationCaption),
                NavigationSeedText = ChooseOwnerText(composedSeedText, fallback.NavigationSeedText),
                NavigationHostText = ChooseOwnerText(composedHostText, fallback.NavigationHostText),
                NavigationRequestText = ChooseOwnerText(composedRequestText, fallback.NavigationRequestText),
                NavigationStateText = ChooseOwnerText(ownerState.NavigationStateText, fallback.NavigationStateText),
                IsLoading = ownerState.IsLoading ?? fallback.IsLoading,
                LoadingStartTick = ownerState.LoadingStartTick != int.MinValue
                    ? ownerState.LoadingStartTick
                    : fallback.LoadingStartTick,
                Entries = fallback.Entries
            };
        }

        private static string ChooseOwnerText(string ownerText, string fallbackText)
        {
            return string.IsNullOrWhiteSpace(ownerText)
                ? fallbackText ?? string.Empty
                : ownerText.Trim();
        }
    }
}
