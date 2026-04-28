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
    internal sealed class AntiMacroNoticeWindow : UIWindowBase
    {
        private static readonly Point AvatarOrigin = new(20, 20);
        private static readonly Point TextOrigin = new(87, 43);
        private static readonly Point OkButtonOrigin = new(84, 95);
        private static readonly Point CancelButtonOrigin = new(136, 95);
        private const float ClientWrapWidth = 160f;
        private const int TextBaseY = 59;
        private const int TextVerticalCenterStride = 8;
        private const int LineSpacing = 17;

        private readonly string _windowName;
        private readonly Texture2D _frameTexture;
        private Texture2D _avatarTexture;
        private Point _avatarTextureOrigin;
        private SpriteFont _font;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private string[] _lines = Array.Empty<string>();
        private string _text = string.Empty;
        private int _stringPoolId = -1;
        private KeyboardState _previousKeyboardState;

        public AntiMacroNoticeWindow(string windowName, Texture2D frameTexture)
            : base(new DXObject(0, 0, frameTexture, 0))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _frameTexture = frameTexture;
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;
        public Point FrameSize => new(_frameTexture?.Width ?? 260, _frameTexture?.Height ?? 131);

        public event Action<int> CloseRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
            _lines = WrapText(_text);
        }

        public void ConfigureVisuals(Texture2D avatarTexture, Point avatarTextureOrigin, UIObject okButton, UIObject cancelButton)
        {
            _avatarTexture = avatarTexture;
            _avatarTextureOrigin = avatarTextureOrigin;

            if (!ReferenceEquals(_okButton, okButton))
            {
                if (_okButton != null)
                {
                    _okButton.ButtonClickReleased -= OnOkButtonReleased;
                    uiButtons.Remove(_okButton);
                }

                _okButton = okButton;
                if (_okButton != null)
                {
                    _okButton.X = OkButtonOrigin.X;
                    _okButton.Y = OkButtonOrigin.Y;
                    _okButton.ButtonClickReleased += OnOkButtonReleased;
                    AddButton(_okButton);
                }
            }

            if (!ReferenceEquals(_cancelButton, cancelButton))
            {
                if (_cancelButton != null)
                {
                    _cancelButton.ButtonClickReleased -= OnCancelButtonReleased;
                    uiButtons.Remove(_cancelButton);
                }

                _cancelButton = cancelButton;
                if (_cancelButton != null)
                {
                    _cancelButton.X = CancelButtonOrigin.X;
                    _cancelButton.Y = CancelButtonOrigin.Y;
                    _cancelButton.ButtonClickReleased += OnCancelButtonReleased;
                    AddButton(_cancelButton);
                }
            }
        }

        public void ConfigureVisuals(Texture2D avatarTexture, UIObject okButton, UIObject cancelButton)
        {
            ConfigureVisuals(avatarTexture, Point.Zero, okButton, cancelButton);
        }

        public void Configure(string text, int stringPoolId)
        {
            _text = text ?? string.Empty;
            _stringPoolId = stringPoolId;
            _lines = WrapText(_text);
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
                OnOkButtonReleased(_okButton);
            }
            else if (Pressed(keyboardState, Keys.Escape))
            {
                OnCancelButtonReleased(_cancelButton);
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
            int tickCount)
        {
            if (_avatarTexture != null)
            {
                sprite.Draw(
                    _avatarTexture,
                    ResolveClientAvatarDrawPosition(Position, _avatarTextureOrigin).ToVector2(),
                    Color.White);
            }

            if (_font == null || _lines.Length == 0)
            {
                return;
            }

            float y = Position.Y + ResolveClientNoticeTextStartY(_lines.Length);
            for (int i = 0; i < _lines.Length; i++)
            {
                ClientTextDrawing.Draw(
                    sprite,
                    _lines[i],
                    new Vector2(Position.X + TextOrigin.X, y + (i * LineSpacing)),
                    Color.White,
                    fallbackFont: _font);
            }
        }

        private string[] WrapText(string text)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return string.IsNullOrWhiteSpace(text)
                    ? Array.Empty<string>()
                    : new[] { text };
            }

            return WrapTextForClientNotice(
                text,
                ClientWrapWidth,
                candidate => ClientTextDrawing.Measure((GraphicsDevice)null, candidate, 1.0f, _font).X);
        }

        internal static string[] WrapTextForClientNotice(string text, float wrapWidth, Func<string, float> measureTextWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            if (measureTextWidth == null)
            {
                throw new ArgumentNullException(nameof(measureTextWidth));
            }

            List<string> lines = new();
            string remaining = TrimSingleLeadingSpace(text);
            while (!string.IsNullOrEmpty(remaining))
            {
                int breakIndex = FindClientWrapIndex(remaining, wrapWidth, measureTextWidth);
                lines.Add(remaining[..breakIndex].TrimEnd());
                remaining = breakIndex >= remaining.Length
                    ? string.Empty
                    : TrimSingleLeadingSpace(remaining[breakIndex..]);
            }

            return lines.ToArray();
        }

        internal static int ResolveClientNoticeTextStartY(int lineCount)
        {
            return TextBaseY - (TextVerticalCenterStride * Math.Max(1, lineCount));
        }

        internal static Point ResolveClientAvatarDrawPosition(Point windowPosition, Point avatarTextureOrigin)
        {
            // `CUIAntiMacroNotice::Draw` copies the admin avatar canvas to (20,20).
            // The WZ canvas origin is only honored when an explicit recovered origin is supplied.
            return new Point(
                windowPosition.X + AvatarOrigin.X - avatarTextureOrigin.X,
                windowPosition.Y + AvatarOrigin.Y - avatarTextureOrigin.Y);
        }

        private static int FindClientWrapIndex(string text, float wrapWidth, Func<string, float> measureTextWidth)
        {
            int bestFit = text.Length;
            for (int i = 1; i <= text.Length; i++)
            {
                if (measureTextWidth(text[..i]) >= wrapWidth)
                {
                    bestFit = i > 1 ? i - 1 : 1;
                    break;
                }
            }

            if (bestFit >= text.Length)
            {
                return text.Length;
            }

            // `CUIAntiMacroNotice::OnCreate` advances by character until the
            // small-white client font reaches 0xA0 pixels. It does not backtrack
            // to the previous word boundary; only a single leading space is
            // stripped from the next line after the split.
            return bestFit;
        }

        private static string TrimSingleLeadingSpace(string text)
        {
            return !string.IsNullOrEmpty(text) && text[0] == ' '
                ? text[1..]
                : text ?? string.Empty;
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void OnOkButtonReleased(UIObject sender)
        {
            Hide();
            CloseRequested?.Invoke(1);
        }

        private void OnCancelButtonReleased(UIObject sender)
        {
            Hide();
            CloseRequested?.Invoke(2);
        }
    }
}
