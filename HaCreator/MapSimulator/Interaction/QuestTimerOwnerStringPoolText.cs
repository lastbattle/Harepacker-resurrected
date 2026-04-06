using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestTimerOwnerStringPoolText
    {
        internal const int QuestLogRemainTimeStringPoolId = 0x1014;
        internal const int TooltipRemainTimeStringPoolId = 0x18BD;

        // Recovered from MapleStory.exe v95 StringPool::ms_aString with
        // StringPool::GetString / Decode<char>:
        // - 0x1014 seed 0x4B raw "4B 93 4D 02 F0 0C ED 79 7D CF 33 54 15 82 90 2B 93 1A 0A"
        // - 0x18BD seed 0xF4 raw "F4 9A B8 01 BB 34 E6 0B 6E A7 16 52 46 D4 C9 06 9A 22 1B 07 91 29 95 11"
        private const string QuestLogRemainTimeFallback = "Time Left {0}:{1}:{2}";
        private const string TooltipRemainTimeFallback = "Time Remaining {0}:{1}:{2}";
        private const string QuestLogRemainTimeStringPoolPayload = "Time Left %d:%d:%d";
        private const string TooltipRemainTimeStringPoolPayload = "Time Remaining %d:%d:%d";

        public static string FormatQuestLogRemainTime(int remainingMs, bool appendFallbackSuffix = false)
        {
            return FormatString(QuestLogRemainTimeStringPoolId, QuestLogRemainTimeFallback, remainingMs, appendFallbackSuffix);
        }

        public static string FormatTooltipRemainTime(int remainingMs, bool appendFallbackSuffix = false)
        {
            return FormatString(TooltipRemainTimeStringPoolId, TooltipRemainTimeFallback, remainingMs, appendFallbackSuffix);
        }

        public static bool TryResolve(int stringPoolId, out string text)
        {
            text = stringPoolId switch
            {
                QuestLogRemainTimeStringPoolId => QuestLogRemainTimeStringPoolPayload,
                TooltipRemainTimeStringPoolId => TooltipRemainTimeStringPoolPayload,
                _ => null,
            };

            return text != null;
        }

        private static string FormatString(int stringPoolId, string fallbackFormat, int remainingMs, bool appendFallbackSuffix)
        {
            (int hours, int minutes, int seconds) = ResolveTimeParts(remainingMs);
            bool hasResolvedText = TryResolve(stringPoolId, out string resolvedFormat);
            string format = hasResolvedText
                ? ConvertPrintfTimeFormatToDotNetFormat(resolvedFormat)
                : fallbackFormat;
            string formatted = string.Format(format, hours, minutes, seconds);
            return appendFallbackSuffix && !hasResolvedText
                ? $"{formatted} (StringPool 0x{stringPoolId:X} fallback)"
                : formatted;
        }

        private static string ConvertPrintfTimeFormatToDotNetFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            int tokenIndex = 0;
            int searchStart = 0;
            while (tokenIndex < 3)
            {
                int markerIndex = format.IndexOf("%d", searchStart, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    break;
                }

                string replacement = $"{{{tokenIndex}}}";
                format = format.Remove(markerIndex, 2).Insert(markerIndex, replacement);
                searchStart = markerIndex + replacement.Length;
                tokenIndex++;
            }

            return format;
        }

        private static (int Hours, int Minutes, int Seconds) ResolveTimeParts(int remainingMs)
        {
            int remainingSeconds = Math.Max(0, (int)Math.Ceiling(remainingMs / 1000f));
            TimeSpan span = TimeSpan.FromSeconds(remainingSeconds);
            return ((int)span.TotalHours, span.Minutes, span.Seconds);
        }
    }
}
