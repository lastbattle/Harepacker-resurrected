using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal static class StatusBarChatLayoutRules
    {
        internal const int ClientChatLogTextLeftInset = 9;
        internal const int ClientWhisperPickerModalWidth = 260;
        internal const int ClientWhisperPickerModalComboLeft = 21;
        internal const int ClientWhisperPickerModalComboWidth = 222;
        internal const int ClientWhisperPickerModalComboBottomOffset = 63;
        internal const int ClientWhisperPickerModalListGap = 6;
        internal const int ClientWhisperPickerModalOkButtonLeft = 157;
        internal const int ClientWhisperPickerModalCloseButtonLeft = 198;
        internal const int ClientWhisperPickerModalButtonBottomOffset = 31;
        private const int ChatWrapIndentSpaces = 5;
        private const int ChatSpecialFirstLineWidthReduction = 38;
        private const int WhisperPickerModalContentPadding = 16;

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

        public static Rectangle ResolveWhisperPickerModalComboBounds(
            Rectangle modalBounds,
            int comboHeight)
        {
            int resolvedHeight = Math.Max(18, comboHeight);
            return new Rectangle(
                modalBounds.X + ClientWhisperPickerModalComboLeft,
                modalBounds.Bottom - ClientWhisperPickerModalComboBottomOffset,
                Math.Min(ClientWhisperPickerModalComboWidth, Math.Max(1, modalBounds.Width - ClientWhisperPickerModalComboLeft - WhisperPickerModalContentPadding)),
                resolvedHeight);
        }

        public static Rectangle ResolveWhisperPickerModalComboBounds(
            Rectangle modalBounds,
            int contentY,
            int comboHeight,
            int dividerWidth)
        {
            Rectangle comboBounds = ResolveWhisperPickerModalComboBounds(modalBounds, comboHeight);
            if (dividerWidth > 0)
            {
                int centeredLeft = modalBounds.X + Math.Max(0, (modalBounds.Width - dividerWidth) / 2);
                comboBounds = new Rectangle(
                    Math.Max(comboBounds.X, centeredLeft),
                    comboBounds.Y,
                    comboBounds.Width,
                    comboBounds.Height);
            }

            if (contentY > 0)
            {
                comboBounds = new Rectangle(
                    comboBounds.X,
                    Math.Max(contentY, comboBounds.Y),
                    comboBounds.Width,
                    comboBounds.Height);
            }

            return comboBounds;
        }

        public static Rectangle ResolveWhisperPickerModalListBounds(
            Rectangle modalBounds,
            Rectangle comboBounds,
            int rowHeight,
            int visibleRowCount,
            int minimumRowWidth,
            float maxMeasuredTextWidth)
        {
            int safeRowHeight = Math.Max(1, rowHeight);
            int safeVisibleRowCount = Math.Max(1, visibleRowCount);
            int measuredWidth = Math.Max(
                Math.Max(Math.Max(1, minimumRowWidth), comboBounds.Width),
                (int)Math.Ceiling(Math.Max(0f, maxMeasuredTextWidth)) + 14);
            int maximumWidth = Math.Max(comboBounds.Width, modalBounds.Width - (WhisperPickerModalContentPadding * 2));
            int resolvedWidth = Math.Clamp(measuredWidth, comboBounds.Width, maximumWidth);
            int listHeight = safeRowHeight * safeVisibleRowCount;
            int listX = comboBounds.X;
            int listY = comboBounds.Y - ClientWhisperPickerModalListGap - listHeight;
            if (listY < modalBounds.Y + WhisperPickerModalContentPadding)
            {
                listY = modalBounds.Y + WhisperPickerModalContentPadding;
            }

            return new Rectangle(listX, listY, resolvedWidth, listHeight);
        }

        public static Rectangle ResolveWhisperPickerModalListBounds(
            Rectangle modalBounds,
            int contentY,
            int rowHeight,
            int visibleRowCount,
            int minimumRowWidth,
            float maxMeasuredTextWidth,
            int dividerWidth)
        {
            Rectangle comboBounds = ResolveWhisperPickerModalComboBounds(
                modalBounds,
                contentY,
                Math.Max(18, Math.Max(1, rowHeight)),
                dividerWidth);

            return ResolveWhisperPickerModalListBounds(
                modalBounds,
                comboBounds,
                rowHeight,
                visibleRowCount,
                minimumRowWidth,
                maxMeasuredTextWidth);
        }

        public static Rectangle ResolveWhisperPickerModalVisibleRowBounds(
            Rectangle listBounds,
            int rowHeight,
            int visibleRowIndex)
        {
            int safeRowHeight = Math.Max(1, rowHeight);
            int safeRowIndex = Math.Max(0, visibleRowIndex);
            return new Rectangle(
                listBounds.X,
                listBounds.Y + (safeRowIndex * safeRowHeight),
                listBounds.Width,
                safeRowHeight);
        }

        public static bool CanPageWhisperPickerBackward(int firstVisibleIndex)
        {
            return firstVisibleIndex > 0;
        }

        public static bool CanPageWhisperPickerForward(
            int firstVisibleIndex,
            int candidateCount,
            int visibleRowCount)
        {
            int safeCandidateCount = Math.Max(0, candidateCount);
            int safeVisibleRowCount = Math.Max(1, visibleRowCount);
            int safeFirstVisibleIndex = Math.Max(0, firstVisibleIndex);
            return safeFirstVisibleIndex + safeVisibleRowCount < safeCandidateCount;
        }

        public static int ResolveWhisperPickerButtonSlotWidth(int minimumWidth, params int[] widths)
        {
            int resolvedWidth = Math.Max(1, minimumWidth);
            if (widths == null)
            {
                return resolvedWidth;
            }

            for (int i = 0; i < widths.Length; i++)
            {
                resolvedWidth = Math.Max(resolvedWidth, widths[i]);
            }

            return resolvedWidth;
        }

        public static int ResolveWhisperPickerButtonSlotHeight(int minimumHeight, params int[] heights)
        {
            int resolvedHeight = Math.Max(1, minimumHeight);
            if (heights == null)
            {
                return resolvedHeight;
            }

            for (int i = 0; i < heights.Length; i++)
            {
                resolvedHeight = Math.Max(resolvedHeight, heights[i]);
            }

            return resolvedHeight;
        }

        public static Rectangle ResolveCenteredWhisperPickerButtonBounds(
            Rectangle slotBounds,
            int textureWidth,
            int textureHeight)
        {
            int resolvedWidth = Math.Max(1, textureWidth);
            int resolvedHeight = Math.Max(1, textureHeight);
            int offsetX = Math.Max(0, (slotBounds.Width - resolvedWidth) / 2);
            int offsetY = Math.Max(0, (slotBounds.Height - resolvedHeight) / 2);
            return new Rectangle(
                slotBounds.X + offsetX,
                slotBounds.Y + offsetY,
                resolvedWidth,
                resolvedHeight);
        }

        public static Rectangle ResolveWhisperPickerButtonVisualBounds(
            Rectangle slotBounds,
            int textureWidth,
            int textureHeight,
            Point baseOrigin,
            Point stateOrigin)
        {
            Rectangle centeredBounds = ResolveCenteredWhisperPickerButtonBounds(
                slotBounds,
                textureWidth,
                textureHeight);
            Point originDelta = ResolveWhisperPickerRowOriginDelta(baseOrigin, stateOrigin);
            return new Rectangle(
                centeredBounds.X + originDelta.X,
                centeredBounds.Y + originDelta.Y,
                centeredBounds.Width,
                centeredBounds.Height);
        }

        public static Point ResolveWhisperPickerRowOriginDelta(
            Point baseOrigin,
            Point stateOrigin)
        {
            return new Point(
                stateOrigin.X - baseOrigin.X,
                stateOrigin.Y - baseOrigin.Y);
        }

        public static int ResolveWhisperPickerButtonSlotLeft(
            int clientNormalLeft,
            int normalWidth,
            int slotWidth)
        {
            int safeSlotWidth = Math.Max(1, slotWidth);
            int safeNormalWidth = Math.Max(1, normalWidth);
            return clientNormalLeft - Math.Max(0, (safeSlotWidth - safeNormalWidth) / 2);
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
