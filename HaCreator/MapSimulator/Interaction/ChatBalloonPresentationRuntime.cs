using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum ChatBalloonPresentationEntrypoint
    {
        MakeBalloon,
        MakeScreenBalloon,
        MakeMiniRoomBalloon,
        MakeADBoardBalloon
    }

    internal sealed class ChatBalloonPresentationRuntime
    {
        private ChatBalloonPresentationLayerState _chatState;
        private ChatBalloonMiniRoomBalloonState _miniRoomState;
        private ChatBalloonADBoardState _adBoardState;

        public bool HasChatBalloon => _chatState != null;
        public bool HasMiniRoomBalloon => _miniRoomState != null;
        public bool HasADBoardBalloon => _adBoardState != null;

        public ChatBalloonPresentationLayerState ChatState => _chatState;
        public ChatBalloonMiniRoomBalloonState MiniRoomState => _miniRoomState;
        public ChatBalloonADBoardState ADBoardState => _adBoardState;

        public ChatBalloonNativeCompositionTrace MakeBalloon(
            string text,
            int balloonType,
            int skinIndex,
            bool dead,
            int adjustCoordY,
            int timeoutMs,
            int currentTickCount)
        {
            int normalizedType = balloonType <= 0 ? 1004 : balloonType;
            _chatState = new ChatBalloonPresentationLayerState(
                ChatBalloonPresentationEntrypoint.MakeBalloon,
                text ?? string.Empty,
                normalizedType,
                ResolveOrdinaryBalloonSkinPath(normalizedType, skinIndex, dead),
                adjustCoordY,
                timeoutMs,
                currentTickCount,
                usesScreenLayer: false);
            return BuildNativeCompositionTrace(ChatBalloonPresentationEntrypoint.MakeBalloon);
        }

        public ChatBalloonNativeCompositionTrace MakeScreenBalloon(
            string text,
            int chatBalloonColor,
            int timeoutMs,
            int currentTickCount)
        {
            _chatState = new ChatBalloonPresentationLayerState(
                ChatBalloonPresentationEntrypoint.MakeScreenBalloon,
                text ?? string.Empty,
                1005,
                "UI/ChatBalloon.img/0",
                adjustCoordY: 0,
                timeoutMs,
                currentTickCount,
                usesScreenLayer: true,
                chatBalloonColor);
            return BuildNativeCompositionTrace(ChatBalloonPresentationEntrypoint.MakeScreenBalloon);
        }

        public ChatBalloonNativeCompositionTrace MakeMiniRoomBalloon(
            byte miniRoomType,
            string title,
            int adjustCoordY,
            bool isPrivate,
            byte spec,
            byte maxUsers,
            byte currentUsers,
            bool isGameOn,
            Func<string, float> measureWidth,
            int currentTickCount)
        {
            ChatBalloonMiniRoomIconKind icon = ChatBalloonPresentationRules.ResolveMiniRoomIcon(miniRoomType);
            ChatBalloonMiniRoomPrivacyIconKind privacyIcon = ChatBalloonPresentationRules.ResolveMiniRoomPrivacyIcon(
                isPrivate,
                spec);
            ChatBalloonMiniRoomStatusKind status = ChatBalloonPresentationRules.ResolveMiniRoomStatus(
                currentUsers,
                maxUsers,
                isGameOn);
            IReadOnlyList<string> titleLines = ChatBalloonPresentationRules.ResolveMiniRoomTitleLines(
                title,
                measureWidth,
                ChatBalloonPresentationRules.MiniRoomTitleClientLineWidth);
            ChatBalloonMiniRoomBackground background = ResolveMiniRoomBackground(miniRoomType, spec);
            int preSharedAdjustY = ResolveMiniRoomPreSharedAdjustY(miniRoomType, spec, adjustCoordY);
            _miniRoomState = new ChatBalloonMiniRoomBalloonState(
                miniRoomType,
                title ?? string.Empty,
                titleLines,
                titleLines.Count > 1,
                icon,
                privacyIcon,
                status,
                ChatBalloonPresentationRules.FormatMiniRoomCount(currentUsers),
                ChatBalloonPresentationRules.FormatMiniRoomCount(maxUsers),
                spec,
                background,
                new Rectangle(
                    -background.Width / 2,
                    -background.Height + preSharedAdjustY,
                    background.Width,
                    background.Height),
                preSharedAdjustY,
                adjustCoordY,
                currentTickCount);
            return BuildNativeCompositionTrace(ChatBalloonPresentationEntrypoint.MakeMiniRoomBalloon);
        }

        public ChatBalloonNativeCompositionTrace MakeADBoardBalloon(
            string text,
            Rectangle layerBounds,
            Point buttonOffset,
            bool closeButton,
            int currentTickCount)
        {
            Rectangle normalizedLayerBounds = NormalizeLayerBounds(layerBounds);
            Point normalizedButtonOffset = NormalizeADBoardButtonOffset(normalizedLayerBounds, buttonOffset);
            _adBoardState = new ChatBalloonADBoardState(
                text ?? string.Empty,
                normalizedLayerBounds,
                normalizedButtonOffset,
                closeButton,
                currentTickCount);
            if (closeButton)
            {
                _adBoardState.ApplyMouseMove(false);
            }

            return BuildNativeCompositionTrace(ChatBalloonPresentationEntrypoint.MakeADBoardBalloon);
        }

        public bool RefreshTimeout(int currentTickCount)
        {
            if (_chatState == null || !_chatState.IsExpired(currentTickCount))
            {
                return false;
            }

            _chatState = null;
            return true;
        }

        public ChatBalloonADBoardButtonCanvasKind ADBoardMouseMove(Point point)
        {
            if (_adBoardState == null)
            {
                return ChatBalloonADBoardButtonCanvasKind.Normal;
            }

            bool overButton = ChatBalloonPresentationRules.MousePointCheck(
                _adBoardState.LayerBounds,
                _adBoardState.ButtonOffset,
                point);
            return _adBoardState.ApplyMouseMove(overButton);
        }

        public bool ADBoardMouseDown(Point point)
        {
            if (_adBoardState == null)
            {
                return false;
            }

            bool pressed = _adBoardState.IsPressed;
            bool handled = ChatBalloonPresentationRules.ADBoardMouseDown(
                _adBoardState.LayerBounds,
                _adBoardState.ButtonOffset,
                point,
                ref pressed);
            _adBoardState.SetPressed(pressed);
            if (handled)
            {
                _adBoardState.SetCanvas(ChatBalloonADBoardButtonCanvasKind.Pressed);
            }

            return handled;
        }

        public bool ADBoardMouseUp(Point point)
        {
            if (_adBoardState == null)
            {
                return false;
            }

            bool pressed = _adBoardState.IsPressed;
            bool clicked = ChatBalloonPresentationRules.ADBoardMouseUp(
                _adBoardState.LayerBounds,
                _adBoardState.ButtonOffset,
                point,
                ref pressed);
            _adBoardState.SetPressed(pressed);
            ADBoardMouseMove(point);
            if (clicked)
            {
                _adBoardState.MarkClicked();
            }

            return clicked;
        }

        public void DestroyMiniRoomBalloon()
        {
            _miniRoomState = null;
        }

        public Rectangle GetMiniRoomBalloonRect()
        {
            return _miniRoomState?.LayerBounds ?? Rectangle.Empty;
        }

        public void DestroyADBoardBalloon()
        {
            _adBoardState = null;
        }

        public Rectangle GetADBoardButtonRect()
        {
            return _adBoardState == null
                ? Rectangle.Empty
                : ChatBalloonPresentationRules.ResolveADBoardButtonBounds(
                    _adBoardState.LayerBounds,
                    _adBoardState.ButtonOffset);
        }

        public string DescribeStatus()
        {
            if (_adBoardState == null)
            {
                return _miniRoomState == null
                    ? "CChatBalloon inactive."
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "CChatBalloon MiniRoom active: type={0}, skin={1}, rect=({2},{3},{4},{5}), icon={6}, privacy={7}, status={8}, abbreviated={9}.",
                        _miniRoomState.MiniRoomType,
                        _miniRoomState.Background.Path,
                        _miniRoomState.LayerBounds.X,
                        _miniRoomState.LayerBounds.Y,
                        _miniRoomState.LayerBounds.Width,
                        _miniRoomState.LayerBounds.Height,
                        _miniRoomState.Icon,
                        _miniRoomState.PrivacyIcon,
                        _miniRoomState.Status,
                        _miniRoomState.IsAbbreviated);
            }

            Rectangle buttonBounds = GetADBoardButtonRect();
            return string.Format(
                CultureInfo.InvariantCulture,
                "CChatBalloon ADBoard active: type={0}, layer=({1},{2},{3},{4}), button=({5},{6},{7},{8}), canvas={9}, pressed={10}, clicked={11}.",
                ChatBalloonPresentationRules.ADBoardNativeBalloonType,
                _adBoardState.LayerBounds.X,
                _adBoardState.LayerBounds.Y,
                _adBoardState.LayerBounds.Width,
                _adBoardState.LayerBounds.Height,
                buttonBounds.X,
                buttonBounds.Y,
                buttonBounds.Width,
                buttonBounds.Height,
                _adBoardState.CurrentButtonCanvas,
                _adBoardState.IsPressed,
                _adBoardState.ClickCount);
        }

        internal static ChatBalloonNativeCompositionTrace BuildNativeCompositionTrace(ChatBalloonPresentationEntrypoint entrypoint)
        {
            return entrypoint switch
            {
                ChatBalloonPresentationEntrypoint.MakeADBoardBalloon => new ChatBalloonNativeCompositionTrace(
                    "CChatBalloon::MakeADBoardBalloon",
                    ChatBalloonPresentationRules.ADBoardNativeBalloonType,
                    "UI/ChatBalloon.img/adboard/0",
                    true,
                    false,
                    false,
                    new[]
                    {
                        "MakeBalloon(type=1003)",
                        "CreateCanvas(type=1003)",
                        "ADBoardMouseMove(initial)"
                    }),
                ChatBalloonPresentationEntrypoint.MakeScreenBalloon => new ChatBalloonNativeCompositionTrace(
                    "CChatBalloon::MakeScreenBalloon",
                    1005,
                    "UI/ChatBalloon.img/0",
                    false,
                    true,
                    false,
                    new[]
                    {
                        "CreateCanvas(type=1005)",
                        "CreateLayer(option=0xC00616FC)",
                        "InsertCanvas(0,0,alpha=255)"
                    }),
                ChatBalloonPresentationEntrypoint.MakeMiniRoomBalloon => new ChatBalloonNativeCompositionTrace(
                    "CChatBalloon::MakeMiniRoomBalloon",
                    0,
                    "UI/ChatBalloon.img/miniroom",
                    false,
                    false,
                    true,
                    new[]
                    {
                        "Load(UI/ChatBalloon.img/miniroom)",
                        "CalcLongestText(width=100)",
                        "format_string(remaining title)",
                        "AdjustCoordY"
                    }),
                _ => new ChatBalloonNativeCompositionTrace(
                    "CChatBalloon::MakeBalloon",
                    1004,
                    "UI/ChatBalloon.img/0",
                    false,
                    false,
                    false,
                    new[]
                    {
                        "CreateCanvas(type=1004)",
                        "InsertCanvas(owner layer)",
                        "AdjustCoordY"
                    })
            };
        }

        internal static ChatBalloonMiniRoomBackground ResolveMiniRoomBackgroundForTesting(byte miniRoomType, byte spec)
        {
            return ResolveMiniRoomBackground(miniRoomType, spec);
        }

        internal static int ResolveMiniRoomPreSharedAdjustYForTesting(byte miniRoomType, byte spec, int adjustCoordY)
        {
            return ResolveMiniRoomPreSharedAdjustY(miniRoomType, spec, adjustCoordY);
        }

        internal static Rectangle NormalizeLayerBoundsForTesting(Rectangle layerBounds)
        {
            return NormalizeLayerBounds(layerBounds);
        }

        internal static Point NormalizeADBoardButtonOffsetForTesting(Rectangle layerBounds, Point requestedOffset)
        {
            return NormalizeADBoardButtonOffset(layerBounds, requestedOffset);
        }

        private static Rectangle NormalizeLayerBounds(Rectangle layerBounds)
        {
            return new Rectangle(
                layerBounds.X,
                layerBounds.Y,
                Math.Max(ChatBalloonPresentationRules.ADBoardButtonWidth, layerBounds.Width),
                Math.Max(ChatBalloonPresentationRules.ADBoardButtonHeight, layerBounds.Height));
        }

        private static Point NormalizeADBoardButtonOffset(Rectangle layerBounds, Point requestedOffset)
        {
            int maxX = Math.Max(0, layerBounds.Width - ChatBalloonPresentationRules.ADBoardButtonWidth);
            int maxY = Math.Max(0, layerBounds.Height - ChatBalloonPresentationRules.ADBoardButtonHeight);
            return new Point(
                Math.Clamp(requestedOffset.X, 0, maxX),
                Math.Clamp(requestedOffset.Y, 0, maxY));
        }

        private static ChatBalloonMiniRoomBackground ResolveMiniRoomBackground(byte miniRoomType, byte spec)
        {
            if (miniRoomType is 3 or 4 or 5)
            {
                int skinIndex = Math.Clamp((int)spec, 0, 6);
                if (skinIndex <= 0)
                {
                    skinIndex = 1;
                }

                return skinIndex == 0
                    ? ChatBalloonMiniRoomBackground.Default
                    : new ChatBalloonMiniRoomBackground(
                        $"UI/ChatBalloon.img/miniroom/PSSkin/{skinIndex}",
                        skinIndex,
                        156,
                        159,
                        new Point(21, 29));
            }

            return miniRoomType == 0
                ? ChatBalloonMiniRoomBackground.Default
                : ChatBalloonMiniRoomBackground.Pointed;
        }

        private static int ResolveMiniRoomPreSharedAdjustY(byte miniRoomType, byte spec, int adjustCoordY)
        {
            return miniRoomType == 5 || (miniRoomType == 4 && spec > 0)
                ? adjustCoordY + 7
                : 0;
        }

        private static string ResolveOrdinaryBalloonSkinPath(int balloonType, int skinIndex, bool dead)
        {
            if (dead)
            {
                return "UI/ChatBalloon.img/dead";
            }

            return balloonType switch
            {
                1003 => "UI/ChatBalloon.img/adboard/0",
                1004 => $"UI/ChatBalloon.img/{Math.Max(0, skinIndex)}",
                1005 => "UI/ChatBalloon.img/0",
                _ => $"UI/ChatBalloon.img/{Math.Max(0, skinIndex)}"
            };
        }
    }

    internal sealed class ChatBalloonPresentationLayerState
    {
        internal ChatBalloonPresentationLayerState(
            ChatBalloonPresentationEntrypoint entrypoint,
            string text,
            int balloonType,
            string skinPath,
            int adjustCoordY,
            int timeoutMs,
            int createdAtTick,
            bool usesScreenLayer,
            int fontColorArgb = 0)
        {
            Entrypoint = entrypoint;
            Text = text ?? string.Empty;
            BalloonType = balloonType;
            SkinPath = skinPath ?? string.Empty;
            AdjustCoordY = adjustCoordY;
            TimeoutMs = Math.Max(0, timeoutMs);
            CreatedAtTick = currentTickNormalize(createdAtTick);
            UsesScreenLayer = usesScreenLayer;
            FontColorArgb = fontColorArgb;

            static int currentTickNormalize(int tick) => tick;
        }

        public ChatBalloonPresentationEntrypoint Entrypoint { get; }
        public string Text { get; }
        public int BalloonType { get; }
        public string SkinPath { get; }
        public int AdjustCoordY { get; }
        public int TimeoutMs { get; }
        public int CreatedAtTick { get; }
        public bool UsesScreenLayer { get; }
        public int FontColorArgb { get; }

        public bool IsExpired(int currentTickCount)
        {
            return TimeoutMs > 0 && unchecked(currentTickCount - CreatedAtTick) >= TimeoutMs;
        }
    }

    internal sealed class ChatBalloonMiniRoomBalloonState
    {
        internal ChatBalloonMiniRoomBalloonState(
            byte miniRoomType,
            string title,
            IReadOnlyList<string> titleLines,
            bool isAbbreviated,
            ChatBalloonMiniRoomIconKind icon,
            ChatBalloonMiniRoomPrivacyIconKind privacyIcon,
            ChatBalloonMiniRoomStatusKind status,
            string currentUserText,
            string maxUserText,
            byte spec,
            ChatBalloonMiniRoomBackground background,
            Rectangle layerBounds,
            int preSharedAdjustY,
            int sharedAdjustY,
            int createdAtTick)
        {
            MiniRoomType = miniRoomType;
            Title = title ?? string.Empty;
            TitleLines = (titleLines ?? Array.Empty<string>()).ToArray();
            IsAbbreviated = isAbbreviated;
            Icon = icon;
            PrivacyIcon = privacyIcon;
            Status = status;
            CurrentUserText = currentUserText ?? string.Empty;
            MaxUserText = maxUserText ?? string.Empty;
            Spec = spec;
            Background = background;
            LayerBounds = layerBounds;
            PreSharedAdjustY = preSharedAdjustY;
            SharedAdjustY = sharedAdjustY;
            CreatedAtTick = createdAtTick;
        }

        public byte MiniRoomType { get; }
        public string Title { get; }
        public IReadOnlyList<string> TitleLines { get; }
        public bool IsAbbreviated { get; }
        public ChatBalloonMiniRoomIconKind Icon { get; }
        public ChatBalloonMiniRoomPrivacyIconKind PrivacyIcon { get; }
        public ChatBalloonMiniRoomStatusKind Status { get; }
        public string CurrentUserText { get; }
        public string MaxUserText { get; }
        public byte Spec { get; }
        public ChatBalloonMiniRoomBackground Background { get; }
        public Rectangle LayerBounds { get; }
        public int PreSharedAdjustY { get; }
        public int SharedAdjustY { get; }
        public int CreatedAtTick { get; }
    }

    internal readonly record struct ChatBalloonMiniRoomBackground(
        string Path,
        int SkinIndex,
        int Width,
        int Height,
        Point? Origin)
    {
        internal static ChatBalloonMiniRoomBackground Default { get; } = new(
            "UI/ChatBalloon.img/miniroom/backgrnd",
            0,
            112,
            63,
            new Point(0, 0));

        internal static ChatBalloonMiniRoomBackground Pointed { get; } = new(
            "UI/ChatBalloon.img/miniroom/backgrnd2",
            0,
            112,
            63,
            new Point(0, 0));
    }

    internal sealed class ChatBalloonADBoardState
    {
        internal ChatBalloonADBoardState(
            string text,
            Rectangle layerBounds,
            Point buttonOffset,
            bool hasCloseButton,
            int createdAtTick)
        {
            Text = text ?? string.Empty;
            LayerBounds = layerBounds;
            ButtonOffset = buttonOffset;
            HasCloseButton = hasCloseButton;
            CreatedAtTick = createdAtTick;
            CurrentButtonCanvas = ChatBalloonADBoardButtonCanvasKind.Normal;
        }

        public string Text { get; }
        public Rectangle LayerBounds { get; }
        public Point ButtonOffset { get; }
        public bool HasCloseButton { get; }
        public int CreatedAtTick { get; }
        public bool IsPressed { get; private set; }
        public int ClickCount { get; private set; }
        public ChatBalloonADBoardButtonCanvasKind CurrentButtonCanvas { get; private set; }

        internal ChatBalloonADBoardButtonCanvasKind ApplyMouseMove(bool overButton)
        {
            if (IsPressed)
            {
                CurrentButtonCanvas = ChatBalloonADBoardButtonCanvasKind.Pressed;
                return CurrentButtonCanvas;
            }

            CurrentButtonCanvas = ChatBalloonPresentationRules.ResolveADBoardMouseMoveCanvas(overButton);
            return CurrentButtonCanvas;
        }

        internal void SetPressed(bool pressed)
        {
            IsPressed = pressed;
        }

        internal void SetCanvas(ChatBalloonADBoardButtonCanvasKind canvas)
        {
            CurrentButtonCanvas = canvas;
        }

        internal void MarkClicked()
        {
            ClickCount++;
        }
    }

    internal sealed class ChatBalloonNativeCompositionTrace
    {
        internal ChatBalloonNativeCompositionTrace(
            string entrypoint,
            int balloonType,
            string skinPath,
            bool ownsADBoardLayer,
            bool usesScreenLayer,
            bool usesMiniRoomLayer,
            IEnumerable<string> nativeLifetimeOperations)
        {
            Entrypoint = entrypoint ?? string.Empty;
            BalloonType = balloonType;
            SkinPath = skinPath ?? string.Empty;
            OwnsADBoardLayer = ownsADBoardLayer;
            UsesScreenLayer = usesScreenLayer;
            UsesMiniRoomLayer = usesMiniRoomLayer;
            NativeLifetimeOperations = (nativeLifetimeOperations ?? Enumerable.Empty<string>()).ToArray();
        }

        public string Entrypoint { get; }
        public int BalloonType { get; }
        public string SkinPath { get; }
        public bool OwnsADBoardLayer { get; }
        public bool UsesScreenLayer { get; }
        public bool UsesMiniRoomLayer { get; }
        public IReadOnlyList<string> NativeLifetimeOperations { get; }
    }
}
