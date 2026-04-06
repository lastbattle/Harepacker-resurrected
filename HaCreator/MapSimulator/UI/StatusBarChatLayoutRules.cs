using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal static class StatusBarChatLayoutRules
    {
        private const int ChatWrapIndentSpaces = 5;
        private const int ChatSpecialFirstLineWidthReduction = 38;

        public static IReadOnlyList<string> WrapClientChatText(
            string text,
            float maxWidth,
            int chatLogType,
            Func<string, float> measureWidth)
        {
            if (measureWidth == null)
            {
                throw new ArgumentNullException(nameof(measureWidth));
            }

            if (string.IsNullOrEmpty(text))
            {
                return new[] { string.Empty };
            }

            string remaining = text;
            if (string.IsNullOrWhiteSpace(remaining))
            {
                return new[] { string.Empty };
            }

            List<string> lines = new List<string>();
            bool isFirstLine = true;
            while (!string.IsNullOrWhiteSpace(remaining))
            {
                float currentMaxWidth = ResolveLineMaxWidth(maxWidth, chatLogType, isFirstLine);
                int fitLength = ResolveLongestFittingPrefixLength(remaining, currentMaxWidth, measureWidth);
                string line = remaining.Substring(0, fitLength);
                lines.Add(line);

                remaining = remaining.Substring(fitLength).TrimStart();
                if (!string.IsNullOrEmpty(remaining) && ShouldIndentWrappedContinuation(chatLogType))
                {
                    remaining = new string(' ', ChatWrapIndentSpaces) + remaining;
                }

                isFirstLine = false;
            }

            return lines;
        }

        private static int ResolveLongestFittingPrefixLength(
            string text,
            float maxWidth,
            Func<string, float> measureWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            float clampedMaxWidth = Math.Max(1f, maxWidth);
            if (measureWidth(text) <= clampedMaxWidth)
            {
                return text.Length;
            }

            int low = 1;
            int high = text.Length;
            int best = 0;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (measureWidth(text.Substring(0, mid)) <= clampedMaxWidth)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Math.Max(1, best);
        }

        private static float ResolveLineMaxWidth(float maxWidth, int chatLogType, bool isFirstLine)
        {
            if (isFirstLine && RequiresReducedFirstLineWidth(chatLogType))
            {
                return Math.Max(1f, maxWidth - ChatSpecialFirstLineWidthReduction);
            }

            return Math.Max(1f, maxWidth);
        }

        private static bool ShouldIndentWrappedContinuation(int chatLogType)
        {
            return chatLogType < 7 || chatLogType > 12;
        }

        private static bool RequiresReducedFirstLineWidth(int chatLogType)
        {
            return chatLogType == 14
                || chatLogType == 16
                || chatLogType == 19
                || chatLogType == 20;
        }
    }
}
