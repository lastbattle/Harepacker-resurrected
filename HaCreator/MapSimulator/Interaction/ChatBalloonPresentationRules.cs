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
        internal const int ADBoardLayerCanvasIndex = 0;
        internal const int ADBoardHoverLayerCanvasIndex = 3;
        internal const int MiniRoomCountStringPoolId = 0x1A15;
        internal const string MiniRoomCountFallbackFormat = "%d";
        internal const int MiniRoomTitleClientLineWidth = 100;
        internal const int MiniRoomTitleFirstLineY = 8;
        internal const int MiniRoomTitleSecondLineY = 22;
        internal const int MiniRoomTitleSecondLineOffsetY = 14;
        internal const string MiniRoomRootPath = "UI/ChatBalloon.img/miniroom";
        internal const int MiniRoomShopIconX = 12;
        internal const int MiniRoomShopIconY = 83;
        internal const int MiniRoomShopPrivacyIconX = 66;
        internal const int MiniRoomShopPrivacyIconY = 83;
        internal const int MiniRoomShopCurrentCountX = 29;
        internal const int MiniRoomShopCurrentCountY = 85;
        internal const int MiniRoomShopMaxCountX = 46;
        internal const int MiniRoomShopMaxCountY = 85;
        internal const int MiniRoomShopStatusX = 97;
        internal const int MiniRoomShopStatusY = 84;
        internal const int MiniRoomLegacyIconX = 12;
        internal const int MiniRoomLegacyIconY = 34;
        internal const int MiniRoomLegacyPrivacyIconX = 66;
        internal const int MiniRoomLegacyPrivacyIconY = 34;
        internal const int MiniRoomLegacyCurrentCountX = 29;
        internal const int MiniRoomLegacyCurrentCountY = 49;
        internal const int MiniRoomLegacyMaxCountX = 46;
        internal const int MiniRoomLegacyMaxCountY = 49;
        internal const int MiniRoomLegacyStatusX = 58;
        internal const int MiniRoomLegacyStatusY = 47;
        internal const int ADBoardNativeBalloonType = 1003;
        internal const int ADBoardButtonWidth = 12;
        internal const int ADBoardButtonHeight = 12;
        internal const int ADBoardPressedAlpha = 253;
        internal const string ADBoardButtonOwnerPath = "CChatBalloon.m_pButton";
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

        internal static ChatBalloonMiniRoomComposition ResolveMiniRoomComposition(
            byte miniRoomType,
            byte spec,
            byte currentUsers,
            byte maxUsers,
            ChatBalloonMiniRoomIconKind icon,
            ChatBalloonMiniRoomPrivacyIconKind privacyIcon,
            ChatBalloonMiniRoomStatusKind status,
            ChatBalloonMiniRoomBackground background,
            IReadOnlyList<string> titleLines)
        {
            string iconPath = ResolveMiniRoomIconPath(icon, spec);
            string privacyIconPath = ResolveMiniRoomPrivacyIconPath(privacyIcon);
            string statusPath = ResolveMiniRoomStatusPath(status);
            string currentCountPath = ResolveMiniRoomCountPath("cNum", currentUsers);
            string maxCountPath = ResolveMiniRoomCountPath("mNum", maxUsers);
            string shopEffectPath = ResolveMiniRoomShopEffectPath(miniRoomType, spec);
            return new ChatBalloonMiniRoomComposition(
                background.Path,
                iconPath,
                privacyIconPath,
                statusPath,
                currentCountPath,
                maxCountPath,
                shopEffectPath,
                new Point(background.Width, background.Height),
                ResolveMiniRoomTitleYOffsets(titleLines),
                ResolveMiniRoomPastePlan(
                    miniRoomType,
                    spec,
                    currentUsers,
                    maxUsers,
                    background,
                    icon,
                    iconPath,
                    privacyIcon,
                    privacyIconPath,
                    status,
                    statusPath,
                    currentCountPath,
                    maxCountPath,
                    shopEffectPath));
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

        internal static string ResolveADBoardButtonCanvasSource(ChatBalloonADBoardButtonCanvasKind canvas)
        {
            return $"{ADBoardButtonOwnerPath}[{(int)canvas}]";
        }

        internal static ChatBalloonADBoardButtonCopyOperation ResolveADBoardButtonCopyOperation(
            ChatBalloonADBoardButtonCanvasKind canvas,
            Point buttonOffset)
        {
            return new ChatBalloonADBoardButtonCopyOperation(
                ADBoardMouseMoveUsesHoverCanvasIndex(canvas)
                    ? ADBoardHoverLayerCanvasIndex
                    : ADBoardLayerCanvasIndex,
                ResolveADBoardButtonCanvasSource(canvas),
                buttonOffset,
                ADBoardPressedAlpha);
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

        private static bool ADBoardMouseMoveUsesHoverCanvasIndex(ChatBalloonADBoardButtonCanvasKind canvas)
        {
            return canvas == ChatBalloonADBoardButtonCanvasKind.Hover;
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

        private static string ResolveMiniRoomIconPath(ChatBalloonMiniRoomIconKind icon, byte spec)
        {
            return icon switch
            {
                ChatBalloonMiniRoomIconKind.Omok => $"{MiniRoomRootPath}/Omok",
                ChatBalloonMiniRoomIconKind.MemoryGame => $"{MiniRoomRootPath}/MemoryGame/{Math.Clamp((int)spec, 0, 2)}",
                ChatBalloonMiniRoomIconKind.PersonalShop => $"{MiniRoomRootPath}/PersonalShop",
                _ => string.Empty
            };
        }

        private static string ResolveMiniRoomPrivacyIconPath(ChatBalloonMiniRoomPrivacyIconKind privacyIcon)
        {
            return privacyIcon switch
            {
                ChatBalloonMiniRoomPrivacyIconKind.Lock => $"{MiniRoomRootPath}/Lock",
                ChatBalloonMiniRoomPrivacyIconKind.Unlock => $"{MiniRoomRootPath}/Unlock",
                _ => string.Empty
            };
        }

        private static string ResolveMiniRoomStatusPath(ChatBalloonMiniRoomStatusKind status)
        {
            return status switch
            {
                ChatBalloonMiniRoomStatusKind.Able => $"{MiniRoomRootPath}/Able",
                ChatBalloonMiniRoomStatusKind.Disable => $"{MiniRoomRootPath}/Disable",
                ChatBalloonMiniRoomStatusKind.Progress => $"{MiniRoomRootPath}/Progress",
                _ => string.Empty
            };
        }

        private static string ResolveMiniRoomShopEffectPath(byte miniRoomType, byte spec)
        {
            return miniRoomType is 3 or 4 or 5
                ? $"{MiniRoomRootPath}/PSSkin/{Math.Clamp((int)spec, 0, 6)}"
                : string.Empty;
        }

        private static string ResolveMiniRoomCountPath(string nodeName, byte value)
        {
            return value is >= 1 and <= 4
                ? $"{MiniRoomRootPath}/{nodeName}/{value}"
                : string.Empty;
        }

        private static IReadOnlyList<int> ResolveMiniRoomTitleYOffsets(IReadOnlyList<string> titleLines)
        {
            if (titleLines == null || titleLines.Count == 0)
            {
                return Array.Empty<int>();
            }

            return titleLines.Count == 1
                ? new[] { MiniRoomTitleFirstLineY }
                : new[] { MiniRoomTitleFirstLineY, MiniRoomTitleSecondLineY };
        }

        private static IReadOnlyList<ChatBalloonCanvasPasteEntry> ResolveMiniRoomPastePlan(
            byte miniRoomType,
            byte spec,
            byte currentUsers,
            byte maxUsers,
            ChatBalloonMiniRoomBackground background,
            ChatBalloonMiniRoomIconKind icon,
            string iconPath,
            ChatBalloonMiniRoomPrivacyIconKind privacyIcon,
            string privacyIconPath,
            ChatBalloonMiniRoomStatusKind status,
            string statusPath,
            string currentCountPath,
            string maxCountPath,
            string shopEffectPath)
        {
            List<ChatBalloonCanvasPasteEntry> entries = new()
            {
                new("background", background.Path, Point.Zero, new Point(background.Width, background.Height), 255)
            };

            if (!string.IsNullOrEmpty(shopEffectPath))
            {
                entries.Add(new("shopSkin", shopEffectPath, Point.Zero, new Point(background.Width, background.Height), 255));
            }

            bool useShopLayout = miniRoomType is 3 or 4 or 5 && background.Width >= 156;
            AddIfPresent(entries, "icon", iconPath, ResolveMiniRoomIconDestination(useShopLayout), ResolveMiniRoomIconSize(icon));
            AddIfPresent(entries, "privacy", privacyIconPath, ResolveMiniRoomPrivacyDestination(useShopLayout), ResolveMiniRoomPrivacyIconSize(privacyIcon));
            AddIfPresent(entries, "currentCount", currentCountPath, ResolveMiniRoomCurrentCountDestination(useShopLayout), ResolveMiniRoomCountSize(currentUsers));
            AddIfPresent(entries, "maxCount", maxCountPath, ResolveMiniRoomMaxCountDestination(useShopLayout), ResolveMiniRoomCountSize(maxUsers));
            AddIfPresent(entries, "status", statusPath, ResolveMiniRoomStatusDestination(useShopLayout), ResolveMiniRoomStatusSize(status));
            return entries;
        }

        private static void AddIfPresent(
            List<ChatBalloonCanvasPasteEntry> entries,
            string role,
            string sourcePath,
            Point destination,
            Point sourceSize)
        {
            if (string.IsNullOrEmpty(sourcePath) || sourceSize == Point.Zero)
            {
                return;
            }

            entries.Add(new ChatBalloonCanvasPasteEntry(role, sourcePath, destination, sourceSize, 255));
        }

        private static Point ResolveMiniRoomIconDestination(bool useShopLayout)
        {
            return useShopLayout
                ? new Point(MiniRoomShopIconX, MiniRoomShopIconY)
                : new Point(MiniRoomLegacyIconX, MiniRoomLegacyIconY);
        }

        private static Point ResolveMiniRoomPrivacyDestination(bool useShopLayout)
        {
            return useShopLayout
                ? new Point(MiniRoomShopPrivacyIconX, MiniRoomShopPrivacyIconY)
                : new Point(MiniRoomLegacyPrivacyIconX, MiniRoomLegacyPrivacyIconY);
        }

        private static Point ResolveMiniRoomCurrentCountDestination(bool useShopLayout)
        {
            return useShopLayout
                ? new Point(MiniRoomShopCurrentCountX, MiniRoomShopCurrentCountY)
                : new Point(MiniRoomLegacyCurrentCountX, MiniRoomLegacyCurrentCountY);
        }

        private static Point ResolveMiniRoomMaxCountDestination(bool useShopLayout)
        {
            return useShopLayout
                ? new Point(MiniRoomShopMaxCountX, MiniRoomShopMaxCountY)
                : new Point(MiniRoomLegacyMaxCountX, MiniRoomLegacyMaxCountY);
        }

        private static Point ResolveMiniRoomStatusDestination(bool useShopLayout)
        {
            return useShopLayout
                ? new Point(MiniRoomShopStatusX, MiniRoomShopStatusY)
                : new Point(MiniRoomLegacyStatusX, MiniRoomLegacyStatusY);
        }

        private static Point ResolveMiniRoomIconSize(ChatBalloonMiniRoomIconKind icon)
        {
            return icon switch
            {
                ChatBalloonMiniRoomIconKind.Omok => new Point(16, 16),
                ChatBalloonMiniRoomIconKind.MemoryGame => new Point(17, 16),
                ChatBalloonMiniRoomIconKind.PersonalShop => new Point(14, 13),
                _ => Point.Zero
            };
        }

        private static Point ResolveMiniRoomPrivacyIconSize(ChatBalloonMiniRoomPrivacyIconKind privacyIcon)
        {
            return privacyIcon == ChatBalloonMiniRoomPrivacyIconKind.None
                ? Point.Zero
                : new Point(12, 15);
        }

        private static Point ResolveMiniRoomStatusSize(ChatBalloonMiniRoomStatusKind status)
        {
            return status == ChatBalloonMiniRoomStatusKind.None
                ? Point.Zero
                : new Point(48, 15);
        }

        private static Point ResolveMiniRoomCountSize(byte value)
        {
            return value is >= 1 and <= 4
                ? new Point(12, 11)
                : Point.Zero;
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

    internal readonly record struct ChatBalloonCanvasPasteEntry(
        string Role,
        string SourcePath,
        Point Destination,
        Point SourceSize,
        int Alpha);

    internal readonly record struct ChatBalloonADBoardButtonCopyOperation(
        int LayerCanvasIndex,
        string SourceCanvas,
        Point Destination,
        int Alpha);

    internal sealed class ChatBalloonMiniRoomComposition
    {
        internal ChatBalloonMiniRoomComposition(
            string backgroundPath,
            string iconPath,
            string privacyIconPath,
            string statusPath,
            string currentCountPath,
            string maxCountPath,
            string shopEffectPath,
            Point canvasSize,
            IReadOnlyList<int> titleLineYOffsets,
            IReadOnlyList<ChatBalloonCanvasPasteEntry> pastedCanvases)
        {
            BackgroundPath = backgroundPath ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
            PrivacyIconPath = privacyIconPath ?? string.Empty;
            StatusPath = statusPath ?? string.Empty;
            CurrentCountPath = currentCountPath ?? string.Empty;
            MaxCountPath = maxCountPath ?? string.Empty;
            ShopEffectPath = shopEffectPath ?? string.Empty;
            CanvasSize = canvasSize;
            TitleLineYOffsets = titleLineYOffsets ?? Array.Empty<int>();
            PastedCanvases = pastedCanvases ?? Array.Empty<ChatBalloonCanvasPasteEntry>();
        }

        public string BackgroundPath { get; }
        public string IconPath { get; }
        public string PrivacyIconPath { get; }
        public string StatusPath { get; }
        public string CurrentCountPath { get; }
        public string MaxCountPath { get; }
        public string ShopEffectPath { get; }
        public Point CanvasSize { get; }
        public IReadOnlyList<int> TitleLineYOffsets { get; }
        public IReadOnlyList<ChatBalloonCanvasPasteEntry> PastedCanvases { get; }
    }
}
