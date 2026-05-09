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
        PersonalShop
    }

    internal enum ChatBalloonMiniRoomPrivacyIconKind
    {
        None = 0,
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
        internal static ChatBalloonCanvasSkinMetrics OrdinarySkinMetrics { get; } = new(
            "UI/ChatBalloon.img/0",
            CornerWidth: 6,
            CornerHeight: 6,
            CenterTileWidth: 12,
            CenterTileHeight: 14,
            ArrowWidth: 13,
            ArrowHeight: 13,
            ArrowOrigin: new Point(1, 0),
            TextPaddingX: 6,
            TextPaddingY: 6);

        internal static ChatBalloonCanvasSkinMetrics ADBoardSkinMetrics { get; } = new(
            "UI/ChatBalloon.img/adboard/0",
            CornerWidth: 18,
            CornerHeight: 19,
            CenterTileWidth: 14,
            CenterTileHeight: 14,
            ArrowWidth: 14,
            ArrowHeight: 23,
            ArrowOrigin: Point.Zero,
            TextPaddingX: 18,
            TextPaddingY: 19);

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

        internal static ChatBalloonMiniRoomIconKind ResolveMiniRoomIcon(byte miniRoomType)
        {
            return miniRoomType switch
            {
                1 => ChatBalloonMiniRoomIconKind.Omok,
                2 => ChatBalloonMiniRoomIconKind.MemoryGame,
                3 or 4 or 5 => ChatBalloonMiniRoomIconKind.PersonalShop,
                _ => ChatBalloonMiniRoomIconKind.None
            };
        }

        internal static ChatBalloonMiniRoomPrivacyIconKind ResolveMiniRoomPrivacyIcon(bool isPrivate, byte spec)
        {
            if (isPrivate)
            {
                return ChatBalloonMiniRoomPrivacyIconKind.Lock;
            }

            return spec > 0
                ? ChatBalloonMiniRoomPrivacyIconKind.Unlock
                : ChatBalloonMiniRoomPrivacyIconKind.None;
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
            Rectangle buttonBounds = ResolveADBoardButtonBounds(layerBounds, buttonOffset);
            if (buttonBounds == Rectangle.Empty)
            {
                return false;
            }

            return buttonBounds.Contains(point);
        }

        internal static Rectangle ResolveADBoardButtonBounds(Rectangle layerBounds, Point buttonOffset)
        {
            if (layerBounds.Width <= 0 || layerBounds.Height <= 0)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                layerBounds.X + buttonOffset.X,
                layerBounds.Y + buttonOffset.Y,
                ADBoardButtonWidth,
                ADBoardButtonHeight);
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

        internal static ChatBalloonCanvasComposition ResolveCreateCanvasComposition(
            ChatBalloonCanvasSkinMetrics skin,
            int contentWidth,
            int contentHeight,
            bool includeArrow)
        {
            int bodyWidth = Math.Max(
                skin.CenterTileWidth,
                contentWidth + (skin.TextPaddingX * 2));
            int bodyHeight = Math.Max(
                skin.CenterTileHeight,
                contentHeight + (skin.TextPaddingY * 2));
            int fullWidth = bodyWidth + (skin.CornerWidth * 2);
            int fullHeight = bodyHeight + (skin.CornerHeight * 2) + (includeArrow ? skin.ArrowHeight : 0);
            Rectangle bodyBounds = new(
                skin.CornerWidth,
                skin.CornerHeight,
                bodyWidth,
                bodyHeight);
            Point arrowPosition = includeArrow
                ? new Point((fullWidth / 2) - skin.ArrowOrigin.X, bodyBounds.Bottom)
                : Point.Zero;

            return new ChatBalloonCanvasComposition(
                skin.Path,
                new Point(fullWidth, fullHeight),
                bodyBounds,
                arrowPosition,
                includeArrow);
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

    internal readonly record struct ChatBalloonCanvasSkinMetrics(
        string Path,
        int CornerWidth,
        int CornerHeight,
        int CenterTileWidth,
        int CenterTileHeight,
        int ArrowWidth,
        int ArrowHeight,
        Point ArrowOrigin,
        int TextPaddingX,
        int TextPaddingY);

    internal readonly record struct ChatBalloonCanvasComposition(
        string SkinPath,
        Point CanvasSize,
        Rectangle BodyBounds,
        Point ArrowPosition,
        bool IncludesArrow);
}
