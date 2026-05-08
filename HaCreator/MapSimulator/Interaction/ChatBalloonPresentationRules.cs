using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum ChatBalloonMiniRoomIconKind
    {
        None = 0,
        Omok,
        MemoryGame,
        PersonalShop,
        Lock,
        Unlock
    }

    internal enum ChatBalloonMiniRoomStatusKind
    {
        None = 0,
        Able,
        Disable,
        Progress
    }

    internal enum ChatBalloonADBoardButtonCanvasKind
    {
        Normal = 0,
        Pressed = 1,
        Hover = 3
    }

    internal static class ChatBalloonPresentationRules
    {
        internal const int MiniRoomCountStringPoolId = 0x1A15;
        internal const string MiniRoomCountFallbackFormat = "%d";
        internal const int MiniRoomTitleClientLineWidth = 100;
        internal const int MiniRoomTitleSecondLineOffsetY = 14;
        internal const int ADBoardNativeBalloonType = 1003;
        internal const int ADBoardButtonWidth = 12;
        internal const int ADBoardButtonHeight = 12;
        internal const int ADBoardPressedAlpha = 253;

        internal static string FormatMiniRoomCount(byte value)
        {
            string format = MapleStoryStringPool.GetOrFallback(
                MiniRoomCountStringPoolId,
                MiniRoomCountFallbackFormat);
            if (string.IsNullOrWhiteSpace(format))
            {
                format = MiniRoomCountFallbackFormat;
            }

            string compositeFormat = format.Replace("%d", "{0}", StringComparison.Ordinal);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, compositeFormat, value);
            }
            catch (FormatException)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static ChatBalloonMiniRoomIconKind ResolveMiniRoomIcon(byte miniRoomType, byte spec, bool isPrivate)
        {
            if (isPrivate)
            {
                return ChatBalloonMiniRoomIconKind.Lock;
            }

            return miniRoomType switch
            {
                1 => ChatBalloonMiniRoomIconKind.Omok,
                2 => ChatBalloonMiniRoomIconKind.MemoryGame,
                3 or 4 or 5 => ChatBalloonMiniRoomIconKind.PersonalShop,
                _ => spec > 0 ? ChatBalloonMiniRoomIconKind.Unlock : ChatBalloonMiniRoomIconKind.None
            };
        }

        internal static ChatBalloonMiniRoomStatusKind ResolveMiniRoomStatus(
            byte currentUsers,
            byte maxUsers,
            bool isGameOn)
        {
            if (isGameOn)
            {
                return ChatBalloonMiniRoomStatusKind.Progress;
            }

            return maxUsers > 0 && currentUsers >= maxUsers
                ? ChatBalloonMiniRoomStatusKind.Disable
                : ChatBalloonMiniRoomStatusKind.Able;
        }

        internal static IReadOnlyList<string> ResolveMiniRoomTitleLines(
            string title,
            Func<string, float> measureWidth,
            float maxLineWidth)
        {
            string normalizedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return Array.Empty<string>();
            }

            if (measureWidth == null || maxLineWidth <= 0f || measureWidth(normalizedTitle) <= maxLineWidth)
            {
                return new[] { normalizedTitle };
            }

            int firstLineLength = ResolveLongestTitlePrefixLength(normalizedTitle, measureWidth, maxLineWidth);
            if (firstLineLength <= 0 || firstLineLength >= normalizedTitle.Length)
            {
                return new[] { normalizedTitle };
            }

            string firstLine = normalizedTitle[..firstLineLength].TrimEnd();
            string remainingTitle = normalizedTitle[firstLineLength..].TrimStart();
            if (string.IsNullOrWhiteSpace(remainingTitle))
            {
                return new[] { firstLine };
            }

            int secondLineLength = ResolveLongestTitlePrefixLength(remainingTitle, measureWidth, maxLineWidth);
            string secondLine = secondLineLength > 0 && secondLineLength < remainingTitle.Length
                ? remainingTitle[..secondLineLength].TrimEnd()
                : remainingTitle;
            return string.IsNullOrWhiteSpace(secondLine)
                ? new[] { firstLine }
                : new[] { firstLine, secondLine };
        }

        internal static bool MousePointCheck(Rectangle layerBounds, Point buttonOffset, Point point)
        {
            if (layerBounds.Width <= 0 || layerBounds.Height <= 0)
            {
                return false;
            }

            Rectangle buttonBounds = new(
                layerBounds.X + buttonOffset.X,
                layerBounds.Y + buttonOffset.Y,
                ADBoardButtonWidth,
                ADBoardButtonHeight);
            return buttonBounds.Contains(point);
        }

        internal static ChatBalloonADBoardButtonCanvasKind ResolveADBoardMouseMoveCanvas(bool isMouseOverButton)
        {
            return isMouseOverButton
                ? ChatBalloonADBoardButtonCanvasKind.Hover
                : ChatBalloonADBoardButtonCanvasKind.Normal;
        }

        internal static bool ADBoardMouseDown(Rectangle layerBounds, Point buttonOffset, Point point, ref bool pressed)
        {
            if (!MousePointCheck(layerBounds, buttonOffset, point))
            {
                return false;
            }

            pressed = true;
            return true;
        }

        internal static bool ADBoardMouseUp(Rectangle layerBounds, Point buttonOffset, Point point, ref bool pressed)
        {
            if (!pressed)
            {
                return false;
            }

            pressed = false;
            return MousePointCheck(layerBounds, buttonOffset, point);
        }

        private static int ResolveLongestTitlePrefixLength(
            string text,
            Func<string, float> measureWidth,
            float maxLineWidth)
        {
            int bestLength = 0;
            for (int length = 1; length <= text.Length; length++)
            {
                string candidate = text[..length];
                if (measureWidth(candidate) > maxLineWidth)
                {
                    break;
                }

                bestLength = length;
            }

            return bestLength;
        }
    }
}
