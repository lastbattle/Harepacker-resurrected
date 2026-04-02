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
    public sealed class LoginUtilityDialogWindow : UIWindowBase, ISoftKeyboardHost
    {
        private const int TextOffsetX = 17;
        private const int TextOffsetY = 13;
        private const int NoticeBodySpacingY = 6;
        private const float BodyWrapWidth = 248f;
        private const float InputWrapWidth = 230f;
        private const int ClientInputOffsetX = 130;
        private const int ClientInputOffsetY = 103;
        private const int ClientInputWidth = 150;
        private const int ClientInputHeight = 15;
        private const int DialogButtonY = 106;
        private const int OkButtonX = 100;
        private const int YesButtonX = 70;
        private const int YesTightButtonX = 65;
        private const int NoButtonX = 129;
        private const int NowButtonX = 59;
        private const int InputPaddingX = 4;
        private const int InputPaddingY = 1;

        private readonly UIObject _okButton;
        private readonly UIObject _yesButton;
        private readonly UIObject _noButton;
        private readonly UIObject _acceptButton;
        private readonly UIObject _nowButton;
        private readonly UIObject _laterButton;
        private readonly UIObject _restartButton;
        private readonly UIObject _exitButton;
        private readonly UIObject _nexonButton;
        private readonly IReadOnlyDictionary<int, Texture2D> _noticeTextTextures;
        private readonly Texture2D _pixelTexture;
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
        private bool _drawPrimaryButtonLabel;
        private bool _drawSecondaryButtonLabel;
        private LoginUtilityDialogButtonLayout _buttonLayout = LoginUtilityDialogButtonLayout.Ok;
        private KeyboardState _previousKeyboardState;
        private bool _inputMasked;
        private int _inputMaxLength;
        private SoftKeyboardKeyboardType _softKeyboardType = SoftKeyboardKeyboardType.AlphaNumeric;
        private bool _softKeyboardActive;
        private string _compositionText = string.Empty;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;

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
            UIObject nexonButton,
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
            _nexonButton = RegisterButton(nexonButton, true);
            _noticeTextTextures = noticeTextTextures ?? new Dictionary<int, Texture2D>();
            if (frame?.Texture?.GraphicsDevice != null)
            {
                _pixelTexture = new Texture2D(frame.Texture.GraphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
        }

        public override string WindowName => MapSimulatorWindowNames.LoginUtilityDialog;

        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => true;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && HasInputField && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => _softKeyboardType;
        int ISoftKeyboardHost.SoftKeyboardTextLength => _inputValue?.Length ?? 0;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => _inputMaxLength;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => HasInputField && _drawPrimaryButtonLabel;

        public event Action PrimaryRequested;
        public event Action SecondaryRequested;
        public string InputValue => _inputValue;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Hide()
        {
            base.Hide();
            _softKeyboardActive = false;
            ClearCompositionText();
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
            string inputValue = null,
            SoftKeyboardKeyboardType softKeyboardType = SoftKeyboardKeyboardType.AlphaNumeric)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Login Utility" : title;
            _body = body ?? string.Empty;
            _primaryLabel = primaryLabel ?? string.Empty;
            _secondaryLabel = secondaryLabel ?? string.Empty;
            _drawPrimaryButtonLabel = !string.IsNullOrWhiteSpace(primaryLabel);
            _drawSecondaryButtonLabel = !string.IsNullOrWhiteSpace(secondaryLabel);
            _buttonLayout = buttonLayout;
            _noticeTextIndex = noticeTextIndex;
            _inputLabel = inputLabel ?? string.Empty;
            _inputPlaceholder = inputPlaceholder ?? string.Empty;
            _inputMasked = inputMasked;
            _inputMaxLength = Math.Max(0, inputMaxLength);
            _inputValue = inputValue ?? string.Empty;
            _softKeyboardType = softKeyboardType;
            if (!HasInputField)
            {
                _softKeyboardActive = false;
                ClearCompositionText();
            }
            ConfigureButtons();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible || !HasInputField)
            {
                ClearCompositionText();
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
                ClearCompositionText();
                RemoveLastCharacter();
            }

            _previousKeyboardState = keyboardState;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            if (handled || !IsVisible || !HasInputField)
            {
                return handled;
            }

            bool leftClicked = mouseState.LeftButton == ButtonState.Pressed;
            if (leftClicked && GetInputBounds().Contains(mouseState.X, mouseState.Y))
            {
                _softKeyboardActive = true;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
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

            float y;
            if (_noticeTextIndex.HasValue &&
                _noticeTextTextures.TryGetValue(_noticeTextIndex.Value, out Texture2D noticeTextTexture) &&
                noticeTextTexture != null)
            {
                sprite.Draw(
                    noticeTextTexture,
                    new Vector2(Position.X + TextOffsetX, Position.Y + TextOffsetY),
                    Color.White);

                if (string.IsNullOrWhiteSpace(_body) && !HasInputField)
                {
                    return;
                }

                y = Position.Y + TextOffsetY + noticeTextTexture.Height + NoticeBodySpacingY;
            }
            else
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _title,
                    new Vector2(Position.X + TextOffsetX, Position.Y + TextOffsetY),
                    Color.White);

                y = Position.Y + TextOffsetY + _font.LineSpacing + 6;
            }
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
                DrawInputField(sprite, y);
                DrawImeCandidateWindow(sprite);
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

            if (_drawPrimaryButtonLabel)
            {
                DrawButtonLabel(sprite, _activePrimaryButton, _primaryLabel);
            }

            if (_drawSecondaryButtonLabel)
            {
                DrawButtonLabel(sprite, _activeSecondaryButton, _secondaryLabel);
            }
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
            HideButton(_nexonButton);

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
                case LoginUtilityDialogButtonLayout.Nexon:
                    _activePrimaryButton = _nexonButton ?? _okButton;
                    break;
                default:
                    _activePrimaryButton = _okButton;
                    break;
            }

            if (_activePrimaryButton != null && _activeSecondaryButton != null)
            {
                (int primaryX, int secondaryX) = ResolveTwoButtonLayoutPositions();
                PositionButton(_activePrimaryButton, primaryX, DialogButtonY);
                PositionButton(_activeSecondaryButton, secondaryX, DialogButtonY);
            }
            else if (_activePrimaryButton != null)
            {
                PositionButton(_activePrimaryButton, OkButtonX, DialogButtonY);
            }
        }

        private (int PrimaryX, int SecondaryX) ResolveTwoButtonLayoutPositions()
        {
            return _buttonLayout switch
            {
                LoginUtilityDialogButtonLayout.NowLater => (NowButtonX, NoButtonX),
                LoginUtilityDialogButtonLayout.YesNo when _drawPrimaryButtonLabel || _drawSecondaryButtonLabel => (YesTightButtonX, NoButtonX),
                _ => (YesButtonX, NoButtonX),
            };
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

        public override void HandleCommittedText(string text)
        {
            if (!HasInputField || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                AppendCharacter(character);
            }
        }

        public override void HandleCompositionText(string text)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1));
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (!HasInputField)
            {
                ClearCompositionText();
                return;
            }

            string sanitized = SanitizeCompositionText(state?.Text);
            if (sanitized.Length == 0)
            {
                ClearCompositionText();
                return;
            }

            int availableLength = _inputMaxLength > 0
                ? Math.Max(0, _inputMaxLength - _inputValue.Length)
                : sanitized.Length;
            if (availableLength <= 0)
            {
                ClearCompositionText();
                return;
            }

            _compositionText = sanitized.Length > availableLength
                ? sanitized[..availableLength]
                : sanitized;
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            ClearImeCandidateList();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = HasInputField && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
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

            if (!CanAcceptCharacter(c))
            {
                return;
            }

            _inputValue += c;
        }

        private void RemoveLastCharacter()
        {
            if (!SoftKeyboardUI.CanBackspace(_inputValue?.Length ?? 0))
            {
                return;
            }

            _inputValue = _inputValue.Substring(0, _inputValue.Length - 1);
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => GetInputBounds();

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!HasInputField)
            {
                errorMessage = "This dialog has no editable field.";
                return false;
            }

            if (_inputMaxLength > 0 && _inputValue.Length >= _inputMaxLength)
            {
                errorMessage = "The input field is full.";
                return false;
            }

            if (!CanAcceptCharacter(character))
            {
                errorMessage = "That key is disabled for this field.";
                return false;
            }

            AppendCharacter(character);
            return true;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrEmpty(_inputValue))
            {
                errorMessage = "Nothing to remove.";
                return false;
            }

            RemoveLastCharacter();
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!HasInputField || !_drawPrimaryButtonLabel)
            {
                errorMessage = "This dialog cannot be submitted.";
                return false;
            }

            PrimaryRequested?.Invoke();
            return true;
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
        }

        private bool CanAcceptCharacter(char character)
        {
            return SoftKeyboardUI.CanAcceptCharacter(
                _softKeyboardType,
                _inputValue?.Length ?? 0,
                _inputMaxLength,
                character);
        }

        private string BuildVisibleInputText(string visibleValue)
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return visibleValue;
            }

            string composition = _inputMasked ? new string('*', _compositionText.Length) : _compositionText;
            return $"{visibleValue}{composition}";
        }

        private string SanitizeCompositionText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            List<char> acceptedCharacters = new(text.Length);
            int textLength = _inputValue?.Length ?? 0;
            foreach (char character in text)
            {
                if (!CanAcceptCharacter(character))
                {
                    continue;
                }

                if (_inputMaxLength > 0 && textLength + acceptedCharacters.Count >= _inputMaxLength)
                {
                    break;
                }

                acceptedCharacters.Add(character);
            }

            return acceptedCharacters.Count == 0
                ? string.Empty
                : new string(acceptedCharacters.ToArray());
        }

        private void DrawInputField(SpriteBatch sprite, float bodyBottomY)
        {
            if (_font == null)
            {
                return;
            }

            Rectangle inputBounds = GetInputBounds();
            float labelX = UsesClientNoticeInputLane ? inputBounds.X : Position.X + TextOffsetX;
            float labelY = UsesClientNoticeInputLane ? inputBounds.Y - _font.LineSpacing - 2f : bodyBottomY + 8f;
            if (!string.IsNullOrWhiteSpace(_inputLabel))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _inputLabel,
                    new Vector2(labelX, labelY),
                    new Color(255, 248, 223));
            }

            if (_pixelTexture != null)
            {
                Color fillColor = _softKeyboardActive
                    ? new Color(66, 59, 46, 230)
                    : new Color(36, 32, 27, 215);
                Color borderColor = _softKeyboardActive
                    ? new Color(255, 218, 123)
                    : new Color(173, 158, 122);
                sprite.Draw(_pixelTexture, inputBounds, fillColor);
                sprite.Draw(_pixelTexture, new Rectangle(inputBounds.X, inputBounds.Y, inputBounds.Width, 1), borderColor);
                sprite.Draw(_pixelTexture, new Rectangle(inputBounds.X, inputBounds.Bottom - 1, inputBounds.Width, 1), borderColor);
                sprite.Draw(_pixelTexture, new Rectangle(inputBounds.X, inputBounds.Y, 1, inputBounds.Height), borderColor);
                sprite.Draw(_pixelTexture, new Rectangle(inputBounds.Right - 1, inputBounds.Y, 1, inputBounds.Height), borderColor);
            }

            string visibleValue = string.IsNullOrEmpty(_inputValue)
                ? _inputPlaceholder
                : (_inputMasked ? new string('*', _inputValue.Length) : _inputValue);
            Color valueColor = string.IsNullOrEmpty(_inputValue)
                ? new Color(164, 164, 164)
                : new Color(232, 232, 232);
            string inputText = BuildVisibleInputText(visibleValue);
            if (string.IsNullOrWhiteSpace(inputText))
            {
                inputText = "_";
            }

            float textY = inputBounds.Y + InputPaddingY;
            foreach (string line in WrapText(inputText, Math.Max(1f, inputBounds.Width - (InputPaddingX * 2))))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(inputBounds.X + InputPaddingX, textY),
                    valueColor);
                textY += _font.LineSpacing;
                if (textY > inputBounds.Bottom - _font.LineSpacing)
                {
                    break;
                }
            }
        }

        private Rectangle GetInputBounds()
        {
            if (UsesClientNoticeInputLane)
            {
                return new Rectangle(
                    Position.X + ClientInputOffsetX,
                    Position.Y + ClientInputOffsetY,
                    ClientInputWidth,
                    ClientInputHeight);
            }

            float bodyLines = Math.Max(1, WrapText(_body, BodyWrapWidth).Count());
            int lineSpacing = _font?.LineSpacing ?? 18;
            int inputY = Position.Y + TextOffsetY + (lineSpacing * 2) + 6 + (int)((bodyLines - 1) * lineSpacing) + 8 + lineSpacing;
            return new Rectangle(
                Position.X + TextOffsetX,
                inputY,
                (int)Math.Ceiling(InputWrapWidth),
                Math.Max(lineSpacing, 18));
        }

        private bool UsesClientNoticeInputLane => HasInputField && _noticeTextIndex.HasValue;

        private void DrawImeCandidateWindow(SpriteBatch sprite)
        {
            if (_font == null || !_candidateListState.HasCandidates)
            {
                return;
            }

            Rectangle bounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            Texture2D pixel = _pixelTexture;
            if (pixel == null)
            {
                return;
            }
            sprite.Draw(pixel, bounds, new Color(33, 33, 41, 235));
            sprite.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));
            sprite.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            int rowHeight = Math.Max(_font.LineSpacing + 1, 16);
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = start + i;
                string numberText = $"{i + 1}.";
                Rectangle rowBounds = new(bounds.X + 2, bounds.Y + 2 + (i * rowHeight), bounds.Width - 4, rowHeight);
                bool selected = candidateIndex == _candidateListState.Selection;
                if (selected)
                {
                    sprite.Draw(pixel, rowBounds, new Color(89, 108, 147, 220));
                }

                sprite.DrawString(_font, numberText, new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222));
                sprite.DrawString(
                    _font,
                    _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                    new Vector2(rowBounds.X + 8 + (int)Math.Ceiling(_font.MeasureString($"{count}.").X), rowBounds.Y),
                    selected ? Color.White : new Color(240, 235, 200));
            }
        }

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport)
        {
            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0 || _font == null)
            {
                return Rectangle.Empty;
            }

            Rectangle inputBounds = GetInputBounds();
            int widestEntryWidth = 0;
            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = Math.Clamp(_candidateListState.PageStart + i, 0, _candidateListState.Candidates.Count - 1);
                string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                int entryWidth = (int)Math.Ceiling(_font.MeasureString($"{i + 1}.").X + _font.MeasureString(candidateText).X) + 16;
                widestEntryWidth = Math.Max(widestEntryWidth, entryWidth);
            }

            int width = Math.Max(96, widestEntryWidth + 14);
            int height = (visibleCount * Math.Max(_font.LineSpacing + 1, 16)) + 4;
            int x = Math.Clamp(inputBounds.X, 0, Math.Max(0, viewport.Width - width));
            int y = inputBounds.Bottom + 2;
            if (y + height > viewport.Height)
            {
                y = Math.Max(0, inputBounds.Y - height - 2);
            }

            return new Rectangle(x, y, width, height);
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int pageSize = _candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count;
            return Math.Max(0, Math.Min(pageSize, _candidateListState.Candidates.Count - start));
        }
    }
}
