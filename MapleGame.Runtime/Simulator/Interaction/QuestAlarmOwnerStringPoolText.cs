using System.Globalization;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestAlarmOwnerStringPoolText
    {
        internal const int TitleFormatStringPoolId = 0xE4C;
        internal const int DeleteNoticeStringPoolId = 0x106F;
        internal const int NotRegisteredNoticeStringPoolId = 0x1070;
        internal const int AutoRegisterEnabledNoticeStringPoolId = 0x107A;
        internal const int AutoRegisterDisabledNoticeStringPoolId = 0x107B;
        internal const int AutoRegisterEnabledTooltipStringPoolId = 0x107C;
        internal const int AutoRegisterDisabledTooltipStringPoolId = 0x107D;
        internal const int EmptyMaximizeNoticeStringPoolId = 0x18EC;
        internal const int RecentUpdateTooltipStringPoolId = 0x18A8;

        private const string TitleFormatFallback = "Quest Helper ({0}/5)";
        private const string DeleteNoticeFallback = "[{0}] It has been excluded from the auto alarm and it will not be automatically reigstered until you re log-on";
        private const string NotRegisteredNoticeFallback = "[{0}] The quest is in progress but it has not been registered in the alarm.";
        private const string AutoRegisterEnabledNoticeFallback = "Auto Alarm on";
        private const string AutoRegisterDisabledNoticeFallback = "Auto Alarm off";
        private const string AutoRegisterEnabledTooltipFallback = "When you click it, quests in progress will register automatically and if it is not in progress for 10 minutes, it will disappear.";
        private const string AutoRegisterDisabledTooltipFallback = "When you click it, the quest will not register automatically even when the quest is in progress.";
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

        public static string FormatNotRegisteredNotice(string questTitle, bool appendFallbackSuffix = false)
        {
            string safeQuestTitle = string.IsNullOrWhiteSpace(questTitle)
                ? "Unknown Quest"
                : questTitle.Trim();
            string format = ResolveNotRegisteredNoticeFormat(appendFallbackSuffix);
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
                return NormalizeQuestAlarmText(resolvedText);
            }

            return appendFallbackSuffix
                ? $"{RecentUpdateTooltipFallback} ({MapleStoryStringPool.FormatFallbackLabel(RecentUpdateTooltipStringPoolId)} fallback)"
                : RecentUpdateTooltipFallback;
        }

        public static string GetAutoRegisterToggleNotice(bool enabled, bool appendFallbackSuffix = false)
        {
            return GetResolvedPlainTextOrFallback(
                enabled ? AutoRegisterEnabledNoticeStringPoolId : AutoRegisterDisabledNoticeStringPoolId,
                enabled ? AutoRegisterEnabledNoticeFallback : AutoRegisterDisabledNoticeFallback,
                appendFallbackSuffix);
        }

        public static string GetAutoRegisterTooltip(bool enabled, bool appendFallbackSuffix = false)
        {
            return GetResolvedPlainTextOrFallback(
                enabled ? AutoRegisterEnabledTooltipStringPoolId : AutoRegisterDisabledTooltipStringPoolId,
                enabled ? AutoRegisterEnabledTooltipFallback : AutoRegisterDisabledTooltipFallback,
                appendFallbackSuffix);
        }

        internal static string NormalizePacketEscapedText(string text)
        {
            return NormalizeQuestAlarmText(text);
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

        internal static bool IsPlausibleNotRegisteredNoticeFormat(string text)
        {
            return IsPlausibleQuestAlarmText(text)
                && ContainsPrintfPlaceholder(text)
                && text.Contains("quest", System.StringComparison.OrdinalIgnoreCase)
                && text.Contains("alarm", System.StringComparison.OrdinalIgnoreCase)
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
                return NormalizeQuestAlarmText(MapleStoryStringPool.GetCompositeFormatOrFallback(TitleFormatStringPoolId, TitleFormatFallback, 1, out _));
            }

            return TitleFormatFallback;
        }

        private static string ResolveDeleteNoticeFormat(bool appendFallbackSuffix)
        {
            if (TryResolve(DeleteNoticeStringPoolId, out string resolvedFormat) && IsPlausibleDeleteNoticeFormat(resolvedFormat))
            {
                return NormalizeQuestAlarmText(MapleStoryStringPool.GetCompositeFormatOrFallback(DeleteNoticeStringPoolId, DeleteNoticeFallback, 1, out _));
            }

            return appendFallbackSuffix
                ? $"{DeleteNoticeFallback} ({MapleStoryStringPool.FormatFallbackLabel(DeleteNoticeStringPoolId)} fallback)"
                : DeleteNoticeFallback;
        }

        private static string ResolveNotRegisteredNoticeFormat(bool appendFallbackSuffix)
        {
            if (TryResolve(NotRegisteredNoticeStringPoolId, out string resolvedFormat) && IsPlausibleNotRegisteredNoticeFormat(resolvedFormat))
            {
                return NormalizeQuestAlarmText(MapleStoryStringPool.GetCompositeFormatOrFallback(NotRegisteredNoticeStringPoolId, NotRegisteredNoticeFallback, 1, out _));
            }

            return appendFallbackSuffix
                ? $"{NotRegisteredNoticeFallback} ({MapleStoryStringPool.FormatFallbackLabel(NotRegisteredNoticeStringPoolId)} fallback)"
                : NotRegisteredNoticeFallback;
        }

        private static bool TryResolveEmptyMaximizeNotice(out string text)
        {
            if (TryResolve(EmptyMaximizeNoticeStringPoolId, out string resolvedText) && IsPlausibleEmptyMaximizeNotice(resolvedText))
            {
                text = NormalizeQuestAlarmText(resolvedText);
                return true;
            }

            text = null;
            return false;
        }

        private static string GetResolvedPlainTextOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            if (TryResolve(stringPoolId, out string resolvedText) && IsPlausibleQuestAlarmText(resolvedText))
            {
                return NormalizeQuestAlarmText(resolvedText);
            }

            return appendFallbackSuffix
                ? $"{fallbackText} ({MapleStoryStringPool.FormatFallbackLabel(stringPoolId)} fallback)"
                : fallbackText;
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

        private static string NormalizeQuestAlarmText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            StringBuilder builder = new(trimmed.Length);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char current = trimmed[i];
                if (current == '%' &&
                    i + 2 < trimmed.Length &&
                    IsHexDigit(trimmed[i + 1]) &&
                    IsHexDigit(trimmed[i + 2]))
                {
                    int value = ParseHexDigit(trimmed[i + 1]) * 16 + ParseHexDigit(trimmed[i + 2]);
                    builder.Append((char)value);
                    i += 2;
                    continue;
                }

                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool IsHexDigit(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'A' && value <= 'F') ||
                   (value >= 'a' && value <= 'f');
        }

        private static int ParseHexDigit(char value)
        {
            if (value >= '0' && value <= '9')
            {
                return value - '0';
            }

            if (value >= 'A' && value <= 'F')
            {
                return value - 'A' + 10;
            }

            return value - 'a' + 10;
        }
    }
}
