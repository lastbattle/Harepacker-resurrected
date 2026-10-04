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
    /// Shared in-field confirmation owner built from the client FadeYesNo art.
    /// Keeps CFadeWnd/CUIFadeYesNo shell state in one place while callers own payload semantics.
    /// </summary>
    public sealed class InGameConfirmDialogWindow : UIWindowBase
    {
        private const int MessengerInviteAnchorX = 389;
        private const int MessengerInviteBottomOffset = 113;
        private const int MessengerInviteStackStep = 5;
        private const int ParcelAlarmAnchorX = 440;
        private const int ParcelAlarmBottomOffset = 97;
        private const int ParcelAlarmStackStep = 5;
        private const int NativeIconOffsetX = 6;
        private const int TextOffsetX = 17;
        private const int TitleOffsetY = 13;
        private const int BodyStartY = 31;
        private const int FooterOffsetBottom = 24;
        private const int MinimumBodyWrapWidth = 140;
        private const int ButtonGap = 8;
        private const int IconOffsetX = 17;
        private const int IconOffsetY = 22;
        private const int IconTextGap = 10;
        private const int NativeFadeLineStep = 13;

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
        private readonly IReadOnlyDictionary<string, IDXObject> _fadeYesNoFrames;
        private readonly IReadOnlyDictionary<string, Texture2D> _fadeYesNoIcons;
        private readonly List<string> _wrappedLines = new();
        private readonly SharedFadeYesNoModalOwner _fadeYesNoOwner = new();
        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private string _title = "Game Menu";
        private string _body = string.Empty;
        private string _footer = string.Empty;
        private Texture2D _icon;
        private InGameConfirmDialogPresentation _presentation = InGameConfirmDialogPresentation.Default;
        private Color _titleColor = Color.White;
        private Color _bodyColor = new(232, 232, 232);
        private Color _footerColor = new(255, 228, 151);
        private int _iconOffsetX = IconOffsetX;
        private int _iconOffsetY = IconOffsetY;
        private int _lastAppliedSharedFadeCreatedTick = int.MinValue;
        private bool _usesSharedFadeYesNoNativeTextLayout;
        private SharedFadeYesNoPayloadProfile _sharedFadeYesNoPayloadProfile;

        public InGameConfirmDialogWindow(
            IDXObject frame,
            string windowName,
            UIObject confirmButton,
            UIObject cancelButton,
            Texture2D defaultIcon,
            IDXObject messengerInviteFrame,
            Texture2D messengerInviteIcon,
            Texture2D parcelAlarmIcon,
            IReadOnlyDictionary<string, IDXObject> fadeYesNoFrames,
            IReadOnlyDictionary<string, Texture2D> fadeYesNoIcons,
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
            _fadeYesNoFrames = fadeYesNoFrames ?? new Dictionary<string, IDXObject>();
            _fadeYesNoIcons = fadeYesNoIcons ?? new Dictionary<string, Texture2D>();
            ConfigureButtons();
            CenterFrame();
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;
        public override bool IsModalDialogOwner => IsVisible;

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
            _titleColor = _presentation.TitleColor ?? Color.White;
            _bodyColor = _presentation.BodyColor ?? new Color(232, 232, 232);
            _footerColor = _presentation.FooterColor ?? new Color(255, 228, 151);
            _iconOffsetX = _presentation.IconOffsetX ?? IconOffsetX;
            _iconOffsetY = _presentation.IconOffsetY ?? IconOffsetY;
            _usesSharedFadeYesNoNativeTextLayout = false;
            CenterFrame();
            ConfigureButtons();
        }

        internal void ConfigureSharedFadeYesNo(
            SharedFadeYesNoModalRequest request,
            InGameConfirmDialogPresentation fallbackPresentation = null)
        {
            if (request == null)
            {
                return;
            }

            bool activatedImmediately = _fadeYesNoOwner.Enqueue(request, Environment.TickCount);
            if (!activatedImmediately)
            {
                return;
            }

            ApplySharedFadeYesNoSnapshot(_fadeYesNoOwner.CaptureSnapshot(), fallbackPresentation);
        }

        internal SharedFadeYesNoModalSnapshot CaptureSharedFadeYesNoSnapshot()
        {
            return _fadeYesNoOwner.CaptureSnapshot();
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

        public override void Hide()
        {
            _fadeYesNoOwner.Close(Environment.TickCount);
            base.Hide();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!IsVisible)
            {
                return;
            }

            SharedFadeYesNoModalSnapshot beforeUpdateSnapshot = _fadeYesNoOwner.CaptureSnapshot();
            if (_fadeYesNoOwner.Update(Environment.TickCount))
            {
                if (beforeUpdateSnapshot.CancelAction != null)
                {
                    beforeUpdateSnapshot.CancelAction.Invoke();
                }
                else if (_fadeYesNoOwner.PendingCount == 0)
                {
                    CancelRequested?.Invoke();
                }

                return;
            }

            SharedFadeYesNoModalSnapshot snapshot = _fadeYesNoOwner.CaptureSnapshot();
            if (!snapshot.IsActive && _lastAppliedSharedFadeCreatedTick != int.MinValue)
            {
                _lastAppliedSharedFadeCreatedTick = int.MinValue;
                base.Hide();
                return;
            }

            if (snapshot.IsActive
                && snapshot.CreatedTick != _lastAppliedSharedFadeCreatedTick
                && snapshot.Phase != SharedFadeYesNoModalPhase.FadingOut)
            {
                ApplySharedFadeYesNoSnapshot(snapshot);
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (Pressed(keyboardState, Keys.Enter))
            {
                TryRaiseSharedFadeYesNoButton(SharedFadeYesNoModalOwner.OkButtonId, ConfirmRequested);
            }
            else if (Pressed(keyboardState, Keys.Escape))
            {
                TryRaiseSharedFadeYesNoButton(SharedFadeYesNoModalOwner.CancelButtonId, CancelRequested);
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

            if (_usesSharedFadeYesNoNativeTextLayout)
            {
                DrawSharedFadeYesNoContents(sprite);
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _title,
                new Vector2(Position.X + TextOffsetX, Position.Y + TitleOffsetY),
                _titleColor);

            float wrapWidth = ResolveBodyWrapWidth();
            float y = Position.Y + BodyStartY;
            if (_icon != null)
            {
                sprite.Draw(
                    _icon,
                    new Vector2(Position.X + _iconOffsetX, Position.Y + _iconOffsetY),
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
                    _bodyColor);
                y += _font.LineSpacing;
            }

            if (!string.IsNullOrWhiteSpace(_footer))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _footer,
                    new Vector2(Position.X + TextOffsetX, Position.Y + ResolveFooterY()),
                    _footerColor);
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
                    TryRaiseSharedFadeYesNoButton(SharedFadeYesNoModalOwner.OkButtonId, ConfirmRequested);
                }
                else
                {
                    TryRaiseSharedFadeYesNoButton(SharedFadeYesNoModalOwner.CancelButtonId, CancelRequested);
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

        private void ApplySharedFadeYesNoButtonLayout()
        {
            SharedFadeYesNoModalSnapshot snapshot = _fadeYesNoOwner.CaptureSnapshot();
            if (!snapshot.IsActive || _confirmButton == null || _cancelButton == null)
            {
                return;
            }

            SharedFadeYesNoButtonLayout layout = snapshot.ButtonLayout;
            PositionButton(_confirmButton, layout.ButtonX, layout.OkY);
            _confirmButton.SetEnabled(layout.ShowsOkButton);
            _confirmButton.SetVisible(layout.ShowsOkButton);
            PositionButton(_cancelButton, layout.ButtonX, layout.CancelY);
        }

        private void ApplySharedFadeYesNoSnapshot(
            SharedFadeYesNoModalSnapshot snapshot,
            InGameConfirmDialogPresentation fallbackPresentation = null)
        {
            if (snapshot == null || !snapshot.IsActive)
            {
                return;
            }

            Configure(
                snapshot.Title,
                snapshot.Body,
                snapshot.Footer,
                snapshot.Presentation ?? fallbackPresentation ?? CreateSharedFadeYesNoPresentation(snapshot));
            _sharedFadeYesNoPayloadProfile = SharedFadeYesNoModalOwner.ResolvePayloadProfile(snapshot.Type);
            _usesSharedFadeYesNoNativeTextLayout = snapshot.Type != SharedFadeYesNoModalType.Generic;
            _lastAppliedSharedFadeCreatedTick = snapshot.CreatedTick;
            ApplySharedFadeYesNoButtonLayout();
        }

        private void DrawSharedFadeYesNoContents(SpriteBatch sprite)
        {
            if (_icon != null)
            {
                sprite.Draw(
                    _icon,
                    new Vector2(Position.X + _iconOffsetX, Position.Y + _iconOffsetY),
                    Color.White);
            }

            bool centerAligned = _sharedFadeYesNoPayloadProfile.PrimaryTextX >= 100;
            DrawNativeFadeTextLine(
                sprite,
                _title,
                _sharedFadeYesNoPayloadProfile.PrimaryTextX,
                _sharedFadeYesNoPayloadProfile.PrimaryTextY,
                _titleColor,
                centerAligned);

            DrawNativeFadeTextLine(
                sprite,
                _body,
                _sharedFadeYesNoPayloadProfile.SecondaryTextX,
                _sharedFadeYesNoPayloadProfile.SecondaryTextY,
                _bodyColor,
                centerAligned);

            if (!string.IsNullOrWhiteSpace(_footer))
            {
                int footerY = _sharedFadeYesNoPayloadProfile.UsesLevelJobLine
                    ? _sharedFadeYesNoPayloadProfile.SecondaryTextY + NativeFadeLineStep
                    : ResolveFooterY();
                DrawNativeFadeTextLine(
                    sprite,
                    _footer,
                    _sharedFadeYesNoPayloadProfile.SecondaryTextX,
                    footerY,
                    _footerColor,
                    centerAligned);
            }
        }

        private void DrawNativeFadeTextLine(
            SpriteBatch sprite,
            string text,
            int x,
            int y,
            Color color,
            bool centerAligned)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                float drawX = Position.X + x;
                if (centerAligned)
                {
                    drawX -= MeasureLineWidth(line) / 2f;
                }

                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(drawX, Position.Y + y + (i * _font.LineSpacing)),
                    color);
            }
        }

        private void TryRaiseSharedFadeYesNoButton(int buttonId, Action fallbackAction)
        {
            SharedFadeYesNoModalSnapshot snapshot = _fadeYesNoOwner.CaptureSnapshot();
            if (snapshot.IsActive)
            {
                if (!_fadeYesNoOwner.TryClick(buttonId, Environment.TickCount, out SharedFadeYesNoModalButton clickedButton))
                {
                    return;
                }

                if (clickedButton == SharedFadeYesNoModalButton.Ok)
                {
                    if (snapshot.ConfirmAction != null)
                    {
                        snapshot.ConfirmAction.Invoke();
                    }
                    else
                    {
                        ConfirmRequested?.Invoke();
                    }

                    return;
                }

                if (snapshot.CancelAction != null)
                {
                    snapshot.CancelAction.Invoke();
                }
                else
                {
                    CancelRequested?.Invoke();
                }

                return;
            }

            fallbackAction?.Invoke();
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

        private InGameConfirmDialogPresentation CreateSharedFadeYesNoPresentation(SharedFadeYesNoModalSnapshot snapshot)
        {
            SharedFadeYesNoVisualProfile profile = SharedFadeYesNoModalOwner.ResolveVisualProfile(snapshot.Type, snapshot.QuickDelivery);
            IDXObject frame = ResolveFadeYesNoFrame(profile.FrameName);
            Texture2D icon = ResolveFadeYesNoIcon(profile);
            int iconOffsetY = icon == null
                ? IconOffsetY
                : Math.Max(0, (profile.IconCenterHeight - icon.Height) / 2);

            return new InGameConfirmDialogPresentation(
                InGameConfirmDialogAnchorMode.BottomLeft,
                profile.AnchorX,
                profile.BottomOffset + (Math.Max(0, snapshot.StackIndex) * SharedFadeYesNoModalOwner.StackStep),
                ShowIcon: icon != null,
                Icon: icon,
                Frame: frame,
                TitleColor: profile.UsesBlackText ? Color.Black : Color.White,
                BodyColor: profile.UsesBlackText ? Color.Black : new Color(232, 232, 232),
                FooterColor: profile.UsesBlackText ? Color.Black : new Color(255, 228, 151),
                IconOffsetX: NativeIconOffsetX,
                IconOffsetY: iconOffsetY);
        }

        private IDXObject ResolveFadeYesNoFrame(string frameName)
        {
            if (!string.IsNullOrWhiteSpace(frameName)
                && _fadeYesNoFrames.TryGetValue(frameName, out IDXObject frame)
                && frame != null)
            {
                return frame;
            }

            return _defaultFrame;
        }

        private Texture2D ResolveFadeYesNoIcon(SharedFadeYesNoVisualProfile profile)
        {
            if (profile.SuppressesIcon || string.IsNullOrWhiteSpace(profile.IconName))
            {
                return null;
            }

            if (_fadeYesNoIcons.TryGetValue(profile.IconName, out Texture2D icon)
                && icon != null)
            {
                return icon;
            }

            return _defaultIcon;
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
        IDXObject Frame = null,
        Color? TitleColor = null,
        Color? BodyColor = null,
        Color? FooterColor = null,
        int? IconOffsetX = null,
        int? IconOffsetY = null)
    {
        public static InGameConfirmDialogPresentation Default { get; } = new(InGameConfirmDialogAnchorMode.Center, 0, 0);
    }
}
