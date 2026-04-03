using System;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class QuestTimerOwnerStringPoolText
    {
        internal const int QuestLogRemainTimeStringPoolId = 0x1014;
        internal const int TooltipRemainTimeStringPoolId = 0x18BD;

        private const string QuestLogRemainTimeFallback = "Time left: {0:00}:{1:00}:{2:00}";
        private const string TooltipRemainTimeFallback = "{0:00}:{1:00}:{2:00}";

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
                _ => null,
            };

            return text != null;
        }

        private static string FormatString(int stringPoolId, string fallbackFormat, int remainingMs, bool appendFallbackSuffix)
        {
            (int hours, int minutes, int seconds) = ResolveTimeParts(remainingMs);
            string format = TryResolve(stringPoolId, out string resolvedFormat)
                ? resolvedFormat
                : fallbackFormat;
            string formatted = string.Format(format, hours, minutes, seconds);
            return appendFallbackSuffix && !TryResolve(stringPoolId, out _)
                ? $"{formatted} (StringPool 0x{stringPoolId:X} fallback)"
                : formatted;
        }

        private static (int Hours, int Minutes, int Seconds) ResolveTimeParts(int remainingMs)
        {
            int remainingSeconds = Math.Max(0, (int)Math.Ceiling(remainingMs / 1000f));
            TimeSpan span = TimeSpan.FromSeconds(remainingSeconds);
            return ((int)span.TotalHours, span.Minutes, span.Seconds);
        }
    }
}
