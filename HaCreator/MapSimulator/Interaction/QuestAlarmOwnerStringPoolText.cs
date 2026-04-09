using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestAlarmOwnerStringPoolText
    {
        internal const int TitleFormatStringPoolId = 0xE4C;
        internal const int DeleteNoticeStringPoolId = 0x106F;
        internal const int EmptyMaximizeNoticeStringPoolId = 0x18EC;
        internal const int RecentUpdateTooltipStringPoolId = 0x18A8;

        private const string TitleFormatFallback = "Quest Helper ({0}/5)";
        private const string DeleteNoticeFallback = "[{0}] It has been excluded from the auto alarm and it will not be automatically reigstered until you re log-on";
        private const string EmptyMaximizeNoticeFallback = "There are no quests in the quest helper.";
        private const string RecentUpdateTooltipFallback = "This quest has recent progress updates.";

        public static string FormatTitle(int count)
        {
            string format = ResolveTitleFormat();
            return string.Format(CultureInfo.InvariantCulture, format, count);
        }

        public static string FormatDeleteNotice(string questTitle, bool appendFallbackSuffix = false)
        {
            string safeQuestTitle = string.IsNullOrWhiteSpace(questTitle)
                ? "Unknown Quest"
                : questTitle.Trim();
            string format = ResolveDeleteNoticeFormat(appendFallbackSuffix);
            return string.Format(format, safeQuestTitle);
        }

        public static string GetEmptyMaximizeNotice(bool appendFallbackSuffix = false)
        {
            if (TryResolveEmptyMaximizeNotice(out string resolvedText))
            {
                return resolvedText;
            }

            return appendFallbackSuffix
                ? $"{EmptyMaximizeNoticeFallback} ({MapleStoryStringPool.FormatFallbackLabel(EmptyMaximizeNoticeStringPoolId)} fallback)"
                : EmptyMaximizeNoticeFallback;
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            return MapleStoryStringPool.TryGet(stringPoolId, out text);
        }

        public static string GetRecentUpdateTooltip(bool appendFallbackSuffix = false)
        {
            if (TryResolve(RecentUpdateTooltipStringPoolId, out string resolvedText) && IsPlausibleRecentUpdateTooltip(resolvedText))
            {
                return resolvedText.Trim();
            }

            return appendFallbackSuffix
                ? $"{RecentUpdateTooltipFallback} ({MapleStoryStringPool.FormatFallbackLabel(RecentUpdateTooltipStringPoolId)} fallback)"
                : RecentUpdateTooltipFallback;
        }

        internal static bool IsPlausibleTitleFormat(string text)
        {
            return IsPlausibleQuestAlarmText(text)
                && ContainsPrintfPlaceholder(text)
                && !text.Contains("UI/", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsPlausibleDeleteNoticeFormat(string text)
        {
            return IsPlausibleQuestAlarmText(text)
                && ContainsPrintfPlaceholder(text)
                && !text.Contains('/', System.StringComparison.Ordinal)
                && !text.Contains('\\', System.StringComparison.Ordinal);
        }

        internal static bool IsPlausibleEmptyMaximizeNotice(string text)
        {
            if (!IsPlausibleQuestAlarmText(text))
            {
                return false;
            }

            string normalized = text.Trim();
            return normalized.Contains("quest", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("alarm", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("register", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsPlausibleRecentUpdateTooltip(string text)
        {
            if (!IsPlausibleQuestAlarmText(text))
            {
                return false;
            }

            string normalized = text.Trim();
            return normalized.Contains("quest", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("update", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("progress", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("check", System.StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("detail", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveTitleFormat()
        {
            if (TryResolve(TitleFormatStringPoolId, out string resolvedFormat) && IsPlausibleTitleFormat(resolvedFormat))
            {
                return MapleStoryStringPool.GetCompositeFormatOrFallback(TitleFormatStringPoolId, TitleFormatFallback, 1, out _);
            }

            return TitleFormatFallback;
        }

        private static string ResolveDeleteNoticeFormat(bool appendFallbackSuffix)
        {
            if (TryResolve(DeleteNoticeStringPoolId, out string resolvedFormat) && IsPlausibleDeleteNoticeFormat(resolvedFormat))
            {
                return MapleStoryStringPool.GetCompositeFormatOrFallback(DeleteNoticeStringPoolId, DeleteNoticeFallback, 1, out _);
            }

            return appendFallbackSuffix
                ? $"{DeleteNoticeFallback} ({MapleStoryStringPool.FormatFallbackLabel(DeleteNoticeStringPoolId)} fallback)"
                : DeleteNoticeFallback;
        }

        private static bool TryResolveEmptyMaximizeNotice(out string text)
        {
            if (TryResolve(EmptyMaximizeNoticeStringPoolId, out string resolvedText) && IsPlausibleEmptyMaximizeNotice(resolvedText))
            {
                text = resolvedText;
                return true;
            }

            text = null;
            return false;
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackText, appendFallbackSuffix);
        }

        private static bool IsPlausibleQuestAlarmText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim();
            if (normalized.StartsWith("UI/", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("String/", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Map/", System.StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(".img", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsPrintfPlaceholder(string text)
        {
            return !string.IsNullOrWhiteSpace(text) &&
                   (text.Contains("%d", System.StringComparison.Ordinal) ||
                    text.Contains("%s", System.StringComparison.Ordinal));
        }
    }
}
