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
        private ChatBalloonADBoardState _adBoardState;

        public bool HasADBoardBalloon => _adBoardState != null;

        public ChatBalloonADBoardState ADBoardState => _adBoardState;

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
                return "CChatBalloon ADBoard inactive.";
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
