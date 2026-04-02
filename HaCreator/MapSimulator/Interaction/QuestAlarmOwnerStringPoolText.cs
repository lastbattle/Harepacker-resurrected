namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestAlarmOwnerStringPoolText
    {
        internal const int TitleFormatStringPoolId = 0xE4C;
        internal const int DeleteNoticeStringPoolId = 0x106F;
        internal const int EmptyMaximizeNoticeStringPoolId = 0x18EC;

        private const string TitleFormatFallback = "Quest Alarm ({0})";
        private const string DeleteNoticeFallback = "Quest Alarm removed '{0}'.";
        private const string EmptyMaximizeNoticeFallback = "There are no active quests registered in Quest Alarm.";

        public static string FormatTitle(int count)
        {
            string format = TryResolve(TitleFormatStringPoolId, out string resolvedFormat)
                ? resolvedFormat
                : TitleFormatFallback;

            return string.Format(format, count);
        }

        public static string FormatDeleteNotice(string questTitle, bool appendFallbackSuffix = false)
        {
            string safeQuestTitle = string.IsNullOrWhiteSpace(questTitle)
                ? "Unknown Quest"
                : questTitle.Trim();
            string format = GetResolvedOrFallback(DeleteNoticeStringPoolId, DeleteNoticeFallback, appendFallbackSuffix);
            return string.Format(format, safeQuestTitle);
        }

        public static string GetEmptyMaximizeNotice(bool appendFallbackSuffix = false)
        {
            return GetResolvedOrFallback(EmptyMaximizeNoticeStringPoolId, EmptyMaximizeNoticeFallback, appendFallbackSuffix);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = stringPoolId switch
            {
                _ => null,
            };

            return text != null;
        }

        private static string GetResolvedOrFallback(int stringPoolId, string fallbackText, bool appendFallbackSuffix)
        {
            if (TryResolve(stringPoolId, out string resolvedText))
            {
                return resolvedText;
            }

            if (!appendFallbackSuffix)
            {
                return fallbackText;
            }

            return $"{fallbackText} (StringPool 0x{stringPoolId:X} fallback)";
        }
    }
}
