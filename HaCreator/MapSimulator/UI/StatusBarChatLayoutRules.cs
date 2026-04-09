using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal static class StatusBarChatLayoutRules
    {
        internal const int ClientChatLogTextLeftInset = 9;
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

            string remaining = TrimClientChatBuffer(text);
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

                remaining = TrimClientChatBuffer(remaining.Substring(fitLength), trimRight: false);
                if (!string.IsNullOrEmpty(remaining) && ShouldIndentWrappedContinuation(chatLogType))
                {
                    remaining = new string(' ', ChatWrapIndentSpaces) + remaining;
                }

                isFirstLine = false;
            }

            return lines;
        }

        public static Rectangle ResolveChatInteractionBounds(
            Vector2 chatLogTextPos,
            int chatLogWidth,
            Rectangle chatEnterBounds,
            Rectangle chatSpace2Bounds,
            int chatEnterHeight,
            int visibleLineCount,
            int chatLogLineHeight)
        {
            int left = Math.Min(
                (int)MathF.Floor(chatLogTextPos.X) - 4,
                chatSpace2Bounds.IsEmpty ? int.MaxValue : chatSpace2Bounds.Left);
            if (left == int.MaxValue)
            {
                left = (int)MathF.Floor(chatLogTextPos.X) - 4;
            }

            int right = Math.Max(
                (int)MathF.Ceiling(chatLogTextPos.X) + chatLogWidth + 4,
                Math.Max(
                    chatEnterBounds.IsEmpty ? int.MinValue : chatEnterBounds.Right,
                    chatSpace2Bounds.IsEmpty ? int.MinValue : chatSpace2Bounds.Right));
            if (right == int.MinValue)
            {
                right = left + chatLogWidth + 8;
            }

            int bottom = Math.Max(
                chatEnterBounds.IsEmpty ? int.MinValue : chatEnterBounds.Bottom,
                chatSpace2Bounds.IsEmpty ? int.MinValue : chatSpace2Bounds.Bottom);
            if (bottom == int.MinValue)
            {
                bottom = (chatEnterBounds.IsEmpty ? 0 : chatEnterBounds.Y) + Math.Max(1, chatEnterHeight);
            }

            int safeVisibleLineCount = Math.Max(1, visibleLineCount);
            int safeChatLogLineHeight = Math.Max(1, chatLogLineHeight);
            int top = (int)MathF.Floor(chatLogTextPos.Y) - (safeVisibleLineCount * safeChatLogLineHeight) - 2;
            return new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
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

        private static string TrimClientChatBuffer(string text, bool trimRight = true)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string trimmed = trimRight ? text.TrimEnd() : text;
            return trimmed.TrimStart();
        }
    }
}
