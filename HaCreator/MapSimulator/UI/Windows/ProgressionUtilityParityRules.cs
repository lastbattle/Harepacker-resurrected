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
            string normalizedHost = NormalizeRankingServerHostSeed(serverHost);
            if (string.IsNullOrWhiteSpace(normalizedHost))
            {
                normalizedHost = "unknown";
            }
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

        internal static string NormalizeRankingServerHostSeed(string serverHost)
        {
            if (string.IsNullOrWhiteSpace(serverHost))
            {
                return string.Empty;
            }

            string normalized = serverHost.Trim();
            if (!normalized.Contains("://", StringComparison.Ordinal)
                && Uri.TryCreate("http://" + normalized, UriKind.Absolute, out Uri syntheticUri))
            {
                normalized = syntheticUri.Host;
            }
            else if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri))
            {
                normalized = uri.Host;
            }
            else
            {
                int slashIndex = normalized.IndexOfAny(new[] { '/', '\\', '?' });
                if (slashIndex > 0)
                {
                    normalized = normalized[..slashIndex];
                }
            }

            const string nexonSuffix = ".nexon.com";
            if (normalized.EndsWith(nexonSuffix, StringComparison.OrdinalIgnoreCase)
                && normalized.Length > nexonSuffix.Length)
            {
                normalized = normalized[..^nexonSuffix.Length];
            }

            return normalized.Trim();
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

        internal static (Point AnchorOffset, int LaneWidth) ResolveEventStatusLaneLayout(
            Point authoredAnchorOffset,
            int authoredLaneWidth,
            Point fallbackAnchorOffset,
            int fallbackLaneWidth)
        {
            Point resolvedAnchor = authoredAnchorOffset != Point.Zero
                ? authoredAnchorOffset
                : fallbackAnchorOffset;
            resolvedAnchor = new Point(
                Math.Max(0, resolvedAnchor.X),
                Math.Max(0, resolvedAnchor.Y));

            int resolvedLaneWidth = authoredLaneWidth > 0
                ? authoredLaneWidth
                : fallbackLaneWidth;
            resolvedLaneWidth = Math.Max(40, resolvedLaneWidth);

            return (resolvedAnchor, resolvedLaneWidth);
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
            string ownerNavigateUrl = ownerState.NavigateUrl?.Trim() ?? string.Empty;
            if ((!worldId.HasValue || !characterId.HasValue || string.IsNullOrWhiteSpace(serverHost))
                && TryParseRankingLandingRequest(ownerNavigateUrl, out string parsedServerHost, out int parsedWorldId, out int parsedCharacterId))
            {
                if (string.IsNullOrWhiteSpace(serverHost))
                {
                    serverHost = parsedServerHost;
                }

                worldId ??= parsedWorldId;
                characterId ??= parsedCharacterId;
            }

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

        internal static bool TryParseRankingLandingRequest(string navigateUrl, out string serverHost, out int worldId, out int characterId)
        {
            serverHost = string.Empty;
            worldId = 0;
            characterId = 0;
            if (string.IsNullOrWhiteSpace(navigateUrl)
                || !Uri.TryCreate(navigateUrl.Trim(), UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            serverHost = NormalizeRankingServerHostSeed(uri.Host);
            string query = uri.Query;
            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                query = query[1..];
            }

            bool hasRankingPage = false;
            bool hasWorldId = false;
            bool hasCharacterId = false;
            string[] segments = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                int separatorIndex = segment.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = Uri.UnescapeDataString(segment[..separatorIndex]).Trim();
                string value = Uri.UnescapeDataString(segment[(separatorIndex + 1)..]).Trim();
                if (string.Equals(key, "URL", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(value, "webclient/totpersonrank", StringComparison.OrdinalIgnoreCase))
                {
                    hasRankingPage = true;
                }
                else if (string.Equals(key, "worldid", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedWorldId))
                {
                    worldId = Math.Max(0, parsedWorldId);
                    hasWorldId = true;
                }
                else if (string.Equals(key, "characterid", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedCharacterId))
                {
                    characterId = Math.Max(0, parsedCharacterId);
                    hasCharacterId = true;
                }
            }

            return hasRankingPage
                && hasWorldId
                && hasCharacterId
                && !string.IsNullOrWhiteSpace(serverHost);
        }

        private static string ChooseOwnerText(string ownerText, string fallbackText)
        {
            return string.IsNullOrWhiteSpace(ownerText)
                ? fallbackText ?? string.Empty
                : ownerText.Trim();
        }
    }
}
