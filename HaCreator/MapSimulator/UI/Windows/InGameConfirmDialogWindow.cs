using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Compact in-field confirmation owner built from the shared FadeYesNo art.
    /// Keeps status-bar utility confirmations off the login utility dialog seam.
    /// </summary>
    public sealed class InGameConfirmDialogWindow : UIWindowBase
    {
        private const int MessengerInviteAnchorX = 389;
        private const int MessengerInviteBottomOffset = 113;
        private const int MessengerInviteStackStep = 5;
        private const int ParcelAlarmAnchorX = 440;
        private const int ParcelAlarmBottomOffset = 97;
        private const int ParcelAlarmStackStep = 5;
        private const int TextOffsetX = 17;
        private const int TitleOffsetY = 13;
        private const int BodyStartY = 31;
        private const int FooterOffsetBottom = 24;
        private const int MinimumBodyWrapWidth = 140;
        private const int ButtonGap = 8;
        private const int IconOffsetX = 17;
        private const int IconOffsetY = 22;
        private const int IconTextGap = 10;

        private readonly string _windowName;
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly UIObject _confirmButton;
        private readonly UIObject _cancelButton;
        private readonly IDXObject _defaultFrame;
        private readonly Texture2D _defaultIcon;
        private readonly IDXObject _messengerInviteFrame;
        private readonly Texture2D _messengerInviteIcon;
        private readonly Texture2D _parcelAlarmIcon;
        private readonly List<string> _wrappedLines = new();
        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private string _title = "Game Menu";
        private string _body = string.Empty;
        private string _footer = string.Empty;
        private Texture2D _icon;
        private InGameConfirmDialogPresentation _presentation = InGameConfirmDialogPresentation.Default;

        public InGameConfirmDialogWindow(
            IDXObject frame,
            string windowName,
            UIObject confirmButton,
            UIObject cancelButton,
            Texture2D defaultIcon,
            IDXObject messengerInviteFrame,
            Texture2D messengerInviteIcon,
            Texture2D parcelAlarmIcon,
            int screenWidth,
            int screenHeight)
            : base(frame ?? throw new ArgumentNullException(nameof(frame)))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _confirmButton = RegisterButton(confirmButton, isConfirm: true);
            _cancelButton = RegisterButton(cancelButton, isConfirm: false);
            _defaultFrame = frame;
            _defaultIcon = defaultIcon;
            _messengerInviteFrame = messengerInviteFrame ?? frame;
            _messengerInviteIcon = messengerInviteIcon ?? defaultIcon;
            _parcelAlarmIcon = parcelAlarmIcon ?? defaultIcon;
            ConfigureButtons();
            CenterFrame();
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        public event Action ConfirmRequested;
        public event Action CancelRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        public void Configure(string title, string body, string footer = null, InGameConfirmDialogPresentation presentation = null)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Game Menu" : title.Trim();
            _body = body ?? string.Empty;
            _footer = footer ?? string.Empty;
            _presentation = presentation ?? InGameConfirmDialogPresentation.Default;
            Frame = _presentation.Frame ?? _defaultFrame;
            _icon = _presentation.Icon ?? (_presentation.ShowIcon ? _defaultIcon : null);
            CenterFrame();
            ConfigureButtons();
        }

        public InGameConfirmDialogPresentation CreateMessengerInvitePresentation(int stackIndex = 0)
        {
            int resolvedStackIndex = Math.Max(0, stackIndex);
            return new InGameConfirmDialogPresentation(
                InGameConfirmDialogAnchorMode.BottomLeft,
                MessengerInviteAnchorX,
                MessengerInviteBottomOffset + (resolvedStackIndex * MessengerInviteStackStep),
                ShowIcon: true,
                Icon: _messengerInviteIcon ?? _defaultIcon,
                Frame: _messengerInviteFrame ?? _defaultFrame);
        }

        public InGameConfirmDialogPresentation CreateParcelAlarmPresentation(int stackIndex = 0)
        {
            int resolvedStackIndex = Math.Max(0, stackIndex);
            return new InGameConfirmDialogPresentation(
                InGameConfirmDialogAnchorMode.BottomLeft,
                ParcelAlarmAnchorX,
                ParcelAlarmBottomOffset + (resolvedStackIndex * ParcelAlarmStackStep),
                ShowIcon: true,
                Icon: _parcelAlarmIcon ?? _defaultIcon,
                Frame: _messengerInviteFrame ?? _defaultFrame);
        }

        public override void Show()
        {
            _previousKeyboardState = Keyboard.GetState();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!IsVisible)
            {
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (Pressed(keyboardState, Keys.Enter))
            {
                ConfirmRequested?.Invoke();
            }
            else if (Pressed(keyboardState, Keys.Escape))
            {
                CancelRequested?.Invoke();
            }

            _previousKeyboardState = keyboardState;
        }

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _title,
                new Vector2(Position.X + TextOffsetX, Position.Y + TitleOffsetY),
                Color.White);

            float wrapWidth = ResolveBodyWrapWidth();
            float y = Position.Y + BodyStartY;
            if (_icon != null)
            {
                sprite.Draw(
                    _icon,
                    new Vector2(Position.X + IconOffsetX, Position.Y + IconOffsetY),
                    Color.White);
            }

            int bodyTextOffsetX = ResolveBodyTextOffsetX();
            foreach (string line in WrapText(_body, wrapWidth))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(Position.X + bodyTextOffsetX, y),
                    new Color(232, 232, 232));
                y += _font.LineSpacing;
            }

            if (!string.IsNullOrWhiteSpace(_footer))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _footer,
                    new Vector2(Position.X + TextOffsetX, Position.Y + ResolveFooterY()),
                    new Color(255, 228, 151));
            }
        }

        private UIObject RegisterButton(UIObject button, bool isConfirm)
        {
            if (button == null)
            {
                return null;
            }

            button.ButtonClickReleased += _ =>
            {
                if (isConfirm)
                {
                    ConfirmRequested?.Invoke();
                }
                else
                {
                    CancelRequested?.Invoke();
                }
            };
            AddButton(button);
            return button;
        }

        private void ConfigureButtons()
        {
            if (_confirmButton == null || _cancelButton == null)
            {
                return;
            }

            int frameWidth = Frame?.Width > 0 ? Frame.Width : 206;
            int frameHeight = Frame?.Height > 0 ? Frame.Height : 60;
            int confirmWidth = _confirmButton.CanvasSnapshotWidth > 0 ? _confirmButton.CanvasSnapshotWidth : 40;
            int confirmHeight = _confirmButton.CanvasSnapshotHeight > 0 ? _confirmButton.CanvasSnapshotHeight : 16;
            int cancelWidth = _cancelButton.CanvasSnapshotWidth > 0 ? _cancelButton.CanvasSnapshotWidth : 40;
            int totalWidth = confirmWidth + cancelWidth + ButtonGap;
            int startX = Math.Max(0, (frameWidth - totalWidth) / 2);
            int buttonY = Math.Max(18, frameHeight - confirmHeight - 6);

            PositionButton(_confirmButton, startX, buttonY);
            PositionButton(_cancelButton, startX + confirmWidth + ButtonGap, buttonY);
        }

        private void CenterFrame()
        {
            int width = Frame?.Width > 0 ? Frame.Width : 206;
            int height = Frame?.Height > 0 ? Frame.Height : 60;

            if (_presentation.AnchorMode == InGameConfirmDialogAnchorMode.BottomLeft)
            {
                Position = new Point(
                    Math.Clamp(_presentation.AnchorX, 0, Math.Max(0, _screenWidth - width)),
                    Math.Clamp(_screenHeight - height - _presentation.BottomOffset, 0, Math.Max(0, _screenHeight - height)));
                return;
            }

            Position = new Point(
                Math.Max(24, (_screenWidth / 2) - (width / 2)),
                Math.Max(24, (_screenHeight / 2) - (height / 2)));
        }

        private float ResolveBodyWrapWidth()
        {
            int frameWidth = Frame?.Width > 0 ? Frame.Width : 206;
            return Math.Max(MinimumBodyWrapWidth, frameWidth - ResolveBodyTextOffsetX() - TextOffsetX);
        }

        private int ResolveFooterY()
        {
            int frameHeight = Frame?.Height > 0 ? Frame.Height : 60;
            int footerY = frameHeight - FooterOffsetBottom;
            return Math.Max(BodyStartY + _font.LineSpacing, footerY);
        }

        private IEnumerable<string> WrapText(string text, float maxLineWidth)
        {
            _wrappedLines.Clear();
            if (string.IsNullOrWhiteSpace(text))
            {
                return _wrappedLines;
            }

            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    _wrappedLines.Add(string.Empty);
                    continue;
                }

                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string currentLine = string.Empty;
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (MeasureLineWidth(candidate) <= maxLineWidth || string.IsNullOrEmpty(currentLine))
                    {
                        currentLine = candidate;
                        continue;
                    }

                    _wrappedLines.Add(currentLine);
                    currentLine = word;
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    _wrappedLines.Add(currentLine);
                }
            }

            return _wrappedLines;
        }

        private float MeasureLineWidth(string text)
        {
            return string.IsNullOrEmpty(text) || _font == null
                ? 0f
                : ClientTextDrawing.Measure((GraphicsDevice)null, text, 1.0f, _font).X;
        }

        private int ResolveBodyTextOffsetX()
        {
            if (_icon == null)
            {
                return TextOffsetX;
            }

            return TextOffsetX + _icon.Width + IconTextGap;
        }

        private bool Pressed(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private static void PositionButton(UIObject button, int x, int y)
        {
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            button.SetEnabled(true);
            button.SetVisible(true);
        }
    }

    public enum InGameConfirmDialogAnchorMode
    {
        Center = 0,
        BottomLeft = 1
    }

    public sealed record InGameConfirmDialogPresentation(
        InGameConfirmDialogAnchorMode AnchorMode,
        int AnchorX,
        int BottomOffset,
        bool ShowIcon = false,
        Texture2D Icon = null,
        IDXObject Frame = null)
    {
        public static InGameConfirmDialogPresentation Default { get; } = new(InGameConfirmDialogAnchorMode.Center, 0, 0);
    }
}
