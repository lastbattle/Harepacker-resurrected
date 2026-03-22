using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using HaCreator.MapSimulator;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class LoginUtilityDialogWindow : UIWindowBase
    {
        private const int TextOffsetX = 17;
        private const int TextOffsetY = 13;
        private const float BodyWrapWidth = 248f;
        private const float InputWrapWidth = 230f;
        private const int ButtonBottomMargin = 14;
        private const int ButtonGap = 12;

        private readonly UIObject _okButton;
        private readonly UIObject _yesButton;
        private readonly UIObject _noButton;
        private readonly UIObject _acceptButton;
        private readonly UIObject _nowButton;
        private readonly UIObject _laterButton;
        private readonly UIObject _restartButton;
        private readonly UIObject _exitButton;
        private readonly IReadOnlyDictionary<int, Texture2D> _noticeTextTextures;
        private SpriteFont _font;
        private string _title = "Login Utility";
        private string _body = string.Empty;
        private string _primaryLabel = "OK";
        private string _secondaryLabel = "Cancel";
        private string _inputLabel = string.Empty;
        private string _inputPlaceholder = string.Empty;
        private string _inputValue = string.Empty;
        private int? _noticeTextIndex;
        private UIObject _activePrimaryButton;
        private UIObject _activeSecondaryButton;
        private LoginUtilityDialogButtonLayout _buttonLayout = LoginUtilityDialogButtonLayout.Ok;
        private KeyboardState _previousKeyboardState;
        private bool _inputMasked;
        private int _inputMaxLength;

        private static readonly Keys[] AlphaKeys = Enumerable.Range((int)Keys.A, 26).Select(value => (Keys)value).ToArray();
        private static readonly Keys[] NumberKeys = Enumerable.Range((int)Keys.D0, 10).Select(value => (Keys)value).ToArray();
        private static readonly Keys[] NumPadKeys = Enumerable.Range((int)Keys.NumPad0, 10).Select(value => (Keys)value).ToArray();

        public LoginUtilityDialogWindow(
            IDXObject frame,
            UIObject okButton,
            UIObject yesButton,
            UIObject noButton,
            UIObject acceptButton,
            UIObject nowButton,
            UIObject laterButton,
            UIObject restartButton,
            UIObject exitButton,
            IReadOnlyDictionary<int, Texture2D> noticeTextTextures)
            : base(frame)
        {
            _okButton = RegisterButton(okButton, true);
            _yesButton = RegisterButton(yesButton, true);
            _noButton = RegisterButton(noButton, false);
            _acceptButton = RegisterButton(acceptButton, true);
            _nowButton = RegisterButton(nowButton, true);
            _laterButton = RegisterButton(laterButton, false);
            _restartButton = RegisterButton(restartButton, true);
            _exitButton = RegisterButton(exitButton, false);
            _noticeTextTextures = noticeTextTextures ?? new Dictionary<int, Texture2D>();
        }

        public override string WindowName => MapSimulatorWindowNames.LoginUtilityDialog;

        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => true;

        public event Action PrimaryRequested;
        public event Action SecondaryRequested;
        public string InputValue => _inputValue;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(
            string title,
            string body,
            string primaryLabel,
            string secondaryLabel,
            LoginUtilityDialogButtonLayout buttonLayout,
            int? noticeTextIndex = null,
            string inputLabel = null,
            string inputPlaceholder = null,
            bool inputMasked = false,
            int inputMaxLength = 0,
            string inputValue = null)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Login Utility" : title;
            _body = body ?? string.Empty;
            _primaryLabel = string.IsNullOrWhiteSpace(primaryLabel) ? "OK" : primaryLabel;
            _secondaryLabel = string.IsNullOrWhiteSpace(secondaryLabel) ? "Cancel" : secondaryLabel;
            _buttonLayout = buttonLayout;
            _noticeTextIndex = noticeTextIndex;
            _inputLabel = inputLabel ?? string.Empty;
            _inputPlaceholder = inputPlaceholder ?? string.Empty;
            _inputMasked = inputMasked;
            _inputMaxLength = Math.Max(0, inputMaxLength);
            _inputValue = inputValue ?? string.Empty;
            ConfigureButtons();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible || !HasInputField)
            {
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            if (Pressed(keyboardState, Keys.Enter))
            {
                PrimaryRequested?.Invoke();
            }

            if (Pressed(keyboardState, Keys.Escape))
            {
                SecondaryRequested?.Invoke();
            }

            if (Pressed(keyboardState, Keys.Back))
            {
                RemoveLastCharacter();
            }

            if (Pressed(keyboardState, Keys.Space))
            {
                AppendCharacter(' ');
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in AlphaKeys)
            {
                if (Pressed(keyboardState, key))
                {
                    char c = (char)('a' + (key - Keys.A));
                    AppendCharacter(shift ? char.ToUpperInvariant(c) : c);
                }
            }

            foreach (Keys key in NumberKeys)
            {
                if (Pressed(keyboardState, key))
                {
                    AppendCharacter((char)('0' + (key - Keys.D0)));
                }
            }

            foreach (Keys key in NumPadKeys)
            {
                if (Pressed(keyboardState, key))
                {
                    AppendCharacter((char)('0' + (key - Keys.NumPad0)));
                }
            }

            if (Pressed(keyboardState, Keys.OemMinus) || Pressed(keyboardState, Keys.Subtract))
            {
                AppendCharacter(shift ? '_' : '-');
            }

            if (Pressed(keyboardState, Keys.OemPeriod) || Pressed(keyboardState, Keys.Decimal))
            {
                AppendCharacter('.');
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

            if (_noticeTextIndex.HasValue &&
                _noticeTextTextures.TryGetValue(_noticeTextIndex.Value, out Texture2D noticeTextTexture) &&
                noticeTextTexture != null)
            {
                sprite.Draw(
                    noticeTextTexture,
                    new Vector2(Position.X + TextOffsetX, Position.Y + TextOffsetY),
                    Color.White);
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _title,
                new Vector2(Position.X + TextOffsetX, Position.Y + TextOffsetY),
                Color.White);

            float y = Position.Y + TextOffsetY + _font.LineSpacing + 6;
            foreach (string line in WrapText(_body, BodyWrapWidth))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(Position.X + TextOffsetX, y),
                    new Color(232, 232, 232));
                y += _font.LineSpacing;
            }

            if (HasInputField)
            {
                y += 8f;
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _inputLabel,
                    new Vector2(Position.X + TextOffsetX, y),
                    new Color(255, 248, 223));
                y += _font.LineSpacing;

                string visibleValue = string.IsNullOrEmpty(_inputValue)
                    ? _inputPlaceholder
                    : (_inputMasked ? new string('*', _inputValue.Length) : _inputValue);
                Color valueColor = string.IsNullOrEmpty(_inputValue)
                    ? new Color(164, 164, 164)
                    : new Color(232, 232, 232);

                foreach (string line in WrapText(string.IsNullOrWhiteSpace(visibleValue) ? "_" : visibleValue, InputWrapWidth))
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        line,
                        new Vector2(Position.X + TextOffsetX, y),
                        valueColor);
                    y += _font.LineSpacing;
                }
            }
        }

        protected override void DrawOverlay(
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

            DrawButtonLabel(sprite, _activePrimaryButton, _primaryLabel);
            DrawButtonLabel(sprite, _activeSecondaryButton, _secondaryLabel);
        }

        private void DrawButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (_font == null || button == null || !button.ButtonVisible || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text);
            float x = Position.X + button.X + ((button.CanvasSnapshotWidth - size.X) / 2f);
            float y = Position.Y + button.Y + ((button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, text, new Vector2(x, y), Color.White);
        }

        private UIObject RegisterButton(UIObject button, bool primaryButton)
        {
            if (button == null)
            {
                return null;
            }

            button.SetVisible(false);
            button.SetEnabled(false);
            button.ButtonClickReleased += _ =>
            {
                if (primaryButton)
                {
                    PrimaryRequested?.Invoke();
                }
                else
                {
                    SecondaryRequested?.Invoke();
                }
            };
            AddButton(button);
            return button;
        }

        private void ConfigureButtons()
        {
            _activePrimaryButton = null;
            _activeSecondaryButton = null;

            HideButton(_okButton);
            HideButton(_yesButton);
            HideButton(_noButton);
            HideButton(_acceptButton);
            HideButton(_nowButton);
            HideButton(_laterButton);
            HideButton(_restartButton);
            HideButton(_exitButton);

            switch (_buttonLayout)
            {
                case LoginUtilityDialogButtonLayout.YesNo:
                    _activePrimaryButton = _yesButton ?? _okButton;
                    _activeSecondaryButton = _noButton;
                    break;
                case LoginUtilityDialogButtonLayout.Accept:
                    _activePrimaryButton = _acceptButton ?? _okButton;
                    break;
                case LoginUtilityDialogButtonLayout.NowLater:
                    _activePrimaryButton = _nowButton ?? _okButton;
                    _activeSecondaryButton = _laterButton;
                    break;
                case LoginUtilityDialogButtonLayout.RestartExit:
                    _activePrimaryButton = _restartButton ?? _okButton;
                    _activeSecondaryButton = _exitButton;
                    break;
                default:
                    _activePrimaryButton = _okButton;
                    break;
            }

            if (_activePrimaryButton != null && _activeSecondaryButton != null)
            {
                int totalWidth = _activePrimaryButton.CanvasSnapshotWidth + ButtonGap + _activeSecondaryButton.CanvasSnapshotWidth;
                int startX = Math.Max(0, ((CurrentFrame?.Width ?? 312) - totalWidth) / 2);
                int buttonY = Math.Max(0, (CurrentFrame?.Height ?? 132) - Math.Max(_activePrimaryButton.CanvasSnapshotHeight, _activeSecondaryButton.CanvasSnapshotHeight) - ButtonBottomMargin);

                PositionButton(_activePrimaryButton, startX, buttonY);
                PositionButton(_activeSecondaryButton, startX + _activePrimaryButton.CanvasSnapshotWidth + ButtonGap, buttonY);
            }
            else if (_activePrimaryButton != null)
            {
                int buttonX = Math.Max(0, ((CurrentFrame?.Width ?? 312) - _activePrimaryButton.CanvasSnapshotWidth) / 2);
                int buttonY = Math.Max(0, (CurrentFrame?.Height ?? 132) - _activePrimaryButton.CanvasSnapshotHeight - ButtonBottomMargin);
                PositionButton(_activePrimaryButton, buttonX, buttonY);
            }
        }

        private static void HideButton(UIObject button)
        {
            if (button == null)
            {
                return;
            }

            button.SetVisible(false);
            button.SetEnabled(false);
        }

        private static void PositionButton(UIObject button, int x, int y)
        {
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            button.SetVisible(true);
            button.SetEnabled(true);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }

        private bool HasInputField => !string.IsNullOrWhiteSpace(_inputLabel);

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void AppendCharacter(char c)
        {
            if (!HasInputField)
            {
                return;
            }

            if (_inputMaxLength > 0 && _inputValue.Length >= _inputMaxLength)
            {
                return;
            }

            _inputValue += c;
        }

        private void RemoveLastCharacter()
        {
            if (string.IsNullOrEmpty(_inputValue))
            {
                return;
            }

            _inputValue = _inputValue.Substring(0, _inputValue.Length - 1);
        }
    }
}
