using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AntiMacroChallengeWindow : UIWindowBase
    {
        private const int FallbackWindowWidth = 265;
        private const int FallbackWindowHeight = 250;
        private const int ChallengeWidth = 178;
        private const int ChallengeHeight = 53;
        private const int InputWidth = 194;
        private const int InputHeight = 16;
        private const int InputMaxLength = 12;
        private const string DefaultAttemptMessageFormat = "Attempt {0} of 2";
        private static readonly Color InputCaretColor = new(32, 32, 32);
        private static readonly Color InputCompositionColor = new(74, 74, 74);
        private static readonly Color InputCompositionUnderlineColor = new(74, 74, 74);
        private static readonly Color FallbackInputBackgroundColor = new(255, 255, 255);
        private static readonly Color FallbackInputBorderColor = new(114, 114, 114);

        private sealed class LayoutProfile
        {
            public LayoutProfile(
                Point challengeOrigin,
                Point inputOrigin,
                Point attemptMessageOrigin,
                IReadOnlyList<Point> countdownDigitOrigins,
                Point countdownCommaOrigin)
            {
                ChallengeOrigin = challengeOrigin;
                InputOrigin = inputOrigin;
                AttemptMessageOrigin = attemptMessageOrigin;
                CountdownDigitOrigins = countdownDigitOrigins ?? throw new ArgumentNullException(nameof(countdownDigitOrigins));
                CountdownCommaOrigin = countdownCommaOrigin;
            }

            public Point ChallengeOrigin { get; }
            public Point InputOrigin { get; }
            public Point AttemptMessageOrigin { get; }
            public IReadOnlyList<Point> CountdownDigitOrigins { get; }
            public Point CountdownCommaOrigin { get; }
        }

        private sealed class InputVisualState
        {
            public InputVisualState(
                string visibleText,
                int visibleStart,
                int visibleCaretIndex,
                int visibleCompositionStart,
                int visibleCompositionLength)
            {
                VisibleText = visibleText ?? string.Empty;
                VisibleStart = Math.Max(0, visibleStart);
                VisibleCaretIndex = Math.Clamp(visibleCaretIndex, 0, VisibleText.Length);
                VisibleCompositionStart = visibleCompositionStart < 0
                    ? -1
                    : Math.Clamp(visibleCompositionStart, 0, VisibleText.Length);
                VisibleCompositionLength = VisibleCompositionStart < 0
                    ? 0
                    : Math.Clamp(visibleCompositionLength, 0, VisibleText.Length - VisibleCompositionStart);
            }

            public string VisibleText { get; }
            public int VisibleStart { get; }
            public int VisibleCaretIndex { get; }
            public int VisibleCompositionStart { get; }
            public int VisibleCompositionLength { get; }

            public string VisibleCommittedPrefix =>
                VisibleCompositionStart < 0
                    ? VisibleText
                    : VisibleText[..VisibleCompositionStart];

            public string VisibleComposition =>
                VisibleCompositionStart < 0 || VisibleCompositionLength <= 0
                    ? string.Empty
                    : VisibleText.Substring(VisibleCompositionStart, VisibleCompositionLength);

            public string VisibleCommittedSuffix =>
                VisibleCompositionStart < 0 || VisibleCompositionLength <= 0
                    ? string.Empty
                    : VisibleText[(VisibleCompositionStart + VisibleCompositionLength)..];
        }

        private static readonly LayoutProfile NormalLayout = new(
            new Point(40, 122),
            new Point(42, 189),
            new Point(222, 206),
            new[]
            {
                new Point(95, 39),
                new Point(115, 39),
                new Point(147, 39),
                new Point(167, 39)
            },
            new Point(137, 39));

        private static readonly LayoutProfile AdminLayout = new(
            new Point(81, 153),
            new Point(83, 206),
            new Point(170, 225),
            new[]
            {
                new Point(110, 39),
                new Point(130, 39),
                new Point(162, 39),
                new Point(182, 39)
            },
            new Point(150, 39));

        private readonly string _windowName;
        private readonly bool _adminVariant;
        private readonly Texture2D _pixelTexture;
        private readonly Texture2D[] _countdownDigitTextures = new Texture2D[10];
        private readonly Point[] _countdownDigitOriginsByValue = new Point[10];

        private SpriteFont _font;
        private Texture2D _challengeTexture;
        private Texture2D _frameTexture;
        private Texture2D _countdownCommaTexture;
        private Point _countdownCommaTextureOrigin;
        private KeyboardState _previousKeyboardState;
        private string _inputText = string.Empty;
        private string _statusText = string.Empty;
        private string _compositionText = string.Empty;
        private string _attemptMessageFormat = DefaultAttemptMessageFormat;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;
        private UIObject _submitButton;
        private LayoutProfile _layout;
        private MouseState _previousMouseState;
        private int _answerCount;
        private int _expiresAt = int.MinValue;
        private int _caretIndex;
        private int _compositionCaretIndex = -1;
        private int _compositionInsertionIndex = -1;
        private int _caretBlinkTick;
        private bool _inputFocused = true;

        public AntiMacroChallengeWindow(string windowName, bool adminVariant, GraphicsDevice graphicsDevice)
            : base(new DXObject(0, 0, CreateFallbackFrameTexture(graphicsDevice), 0))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _adminVariant = adminVariant;
            _layout = adminVariant ? AdminLayout : NormalLayout;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible && _inputFocused;

        public event Action<string> SubmitRequested;

        public string CurrentInput => _inputText;
        public int ExpiresAt => _expiresAt;
        public bool IsAdminVariant => _adminVariant;
        public Point ActiveFrameSize => new(
            _frameTexture?.Width ?? FallbackWindowWidth,
            _frameTexture?.Height ?? FallbackWindowHeight);

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Hide()
        {
            base.Hide();
            _inputFocused = false;
            ClearCompositionText();
        }

        public void ConfigureVisualAssets(
            Texture2D frameTexture,
            IReadOnlyList<Texture2D> digitTextures,
            IReadOnlyList<Point> digitOrigins,
            Texture2D commaTexture,
            Point commaOrigin,
            UIObject submitButton,
            IReadOnlyList<Point> countdownDrawOrigins = null,
            Point? countdownCommaDrawOrigin = null,
            Point? challengeOrigin = null,
            Point? inputOrigin = null,
            Point? attemptMessageOrigin = null,
            string attemptMessageFormat = null)
        {
            _frameTexture = frameTexture;
            if (_frameTexture != null)
            {
                Frame = new DXObject(0, 0, _frameTexture, 0);
            }

            for (int i = 0; i < _countdownDigitTextures.Length; i++)
            {
                _countdownDigitTextures[i] = digitTextures != null && i < digitTextures.Count ? digitTextures[i] : null;
                _countdownDigitOriginsByValue[i] = digitOrigins != null && i < digitOrigins.Count ? digitOrigins[i] : Point.Zero;
            }

            _countdownCommaTexture = commaTexture;
            _countdownCommaTextureOrigin = commaOrigin;
            if (countdownDrawOrigins != null && countdownDrawOrigins.Count == 4)
            {
                _layout = new LayoutProfile(
                    challengeOrigin ?? _layout.ChallengeOrigin,
                    inputOrigin ?? _layout.InputOrigin,
                    attemptMessageOrigin ?? _layout.AttemptMessageOrigin,
                    countdownDrawOrigins,
                    countdownCommaDrawOrigin ?? _layout.CountdownCommaOrigin);
            }
            else
            {
                _layout = new LayoutProfile(
                    challengeOrigin ?? _layout.ChallengeOrigin,
                    inputOrigin ?? _layout.InputOrigin,
                    attemptMessageOrigin ?? _layout.AttemptMessageOrigin,
                    _layout.CountdownDigitOrigins,
                    countdownCommaDrawOrigin ?? _layout.CountdownCommaOrigin);
            }
            _attemptMessageFormat = string.IsNullOrWhiteSpace(attemptMessageFormat)
                ? DefaultAttemptMessageFormat
                : attemptMessageFormat;

            if (_submitButton != null)
            {
                _submitButton.ButtonClickReleased -= OnSubmitButtonReleased;
                uiButtons.Remove(_submitButton);
            }

            _submitButton = submitButton;
            if (_submitButton != null)
            {
                _submitButton.ButtonClickReleased += OnSubmitButtonReleased;
                AddButton(_submitButton);
            }
        }

        public void Configure(Texture2D challengeTexture, int expiresAt, int answerCount, string statusText)
        {
            if (!ReferenceEquals(_challengeTexture, challengeTexture))
            {
                _challengeTexture?.Dispose();
                _challengeTexture = challengeTexture;
            }

            _expiresAt = expiresAt;
            _answerCount = Math.Max(0, answerCount);
            _statusText = statusText ?? string.Empty;
            _inputText = string.Empty;
            _caretIndex = 0;
            _caretBlinkTick = Environment.TickCount;
            _inputFocused = true;
            _previousMouseState = Mouse.GetState();
            ClearCompositionText();
            _submitButton?.SetEnabled(false);
        }

        public void ClearChallenge()
        {
            _challengeTexture?.Dispose();
            _challengeTexture = null;
            _expiresAt = int.MinValue;
            _answerCount = 0;
            _statusText = string.Empty;
            _inputText = string.Empty;
            _caretIndex = 0;
            _caretBlinkTick = Environment.TickCount;
            _inputFocused = false;
            ClearCompositionText();
            _submitButton?.SetEnabled(false);
            Hide();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible)
            {
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            HandleKeyboardInput(keyboardState);

            if (Pressed(keyboardState, Keys.Enter) && CanSubmitAnswer())
            {
                SubmitRequested?.Invoke(_inputText);
            }

            _submitButton?.SetEnabled(CanSubmitAnswer());
            _previousKeyboardState = keyboardState;
            _previousMouseState = Mouse.GetState();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return false;
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (leftJustPressed && GetInputBounds().Contains(mouseState.Position))
            {
                _inputFocused = true;
                ClearCompositionText();
                _caretIndex = ResolveCaretIndexFromMouseX(mouseState.X);
                _caretBlinkTick = Environment.TickCount;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
            }

            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousMouseState = mouseState;
            return handled;
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
            Rectangle bounds = GetWindowBounds();
            Rectangle challengeBounds = new(
                bounds.X + _layout.ChallengeOrigin.X,
                bounds.Y + _layout.ChallengeOrigin.Y,
                ChallengeWidth,
                ChallengeHeight);
            Rectangle inputBounds = new(
                bounds.X + _layout.InputOrigin.X,
                bounds.Y + _layout.InputOrigin.Y,
                InputWidth,
                InputHeight);

            DrawChallengeTexture(sprite, challengeBounds);
            DrawCountdown(sprite, bounds, tickCount);

            if (_font == null)
            {
                return;
            }

            DrawAttemptMessage(sprite, bounds);
            DrawInputText(sprite, inputBounds);
            DrawImeCandidateWindow(sprite, inputBounds);
        }

        public override void HandleCommittedText(string text)
        {
            if (!CapturesKeyboardInput || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                if (_inputText.Length >= InputMaxLength)
                {
                    break;
                }

                if (!char.IsControl(character))
                {
                    InsertCharacter(character);
                }
            }
        }

        public override void HandleCompositionText(string text)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1));
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (!CapturesKeyboardInput)
            {
                ClearCompositionText();
                return;
            }

            string sanitized = state?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(sanitized))
            {
                ClearCompositionText();
                return;
            }

            int availableLength = Math.Max(0, InputMaxLength - _inputText.Length);
            if (availableLength <= 0)
            {
                ClearCompositionText();
                return;
            }

            _compositionInsertionIndex = Math.Clamp(_caretIndex, 0, _inputText.Length);
            _compositionText = sanitized.Length > availableLength
                ? sanitized[..availableLength]
                : sanitized;
            _compositionCaretIndex = Math.Clamp(state.CursorPosition, -1, _compositionText.Length);
            _caretBlinkTick = Environment.TickCount;
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            _compositionCaretIndex = -1;
            _compositionInsertionIndex = -1;
            ClearImeCandidateList();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = CapturesKeyboardInput && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
        }

        private void DrawChallengeTexture(SpriteBatch sprite, Rectangle bounds)
        {
            if (_challengeTexture != null)
            {
                Rectangle drawBounds = FitTexture(bounds, _challengeTexture.Width, _challengeTexture.Height, upscale: false);
                sprite.Draw(_challengeTexture, drawBounds, Color.White);
                return;
            }

            if (_font != null && !string.IsNullOrWhiteSpace(_statusText))
            {
                DrawFallbackMessage(sprite, _statusText, bounds, new Color(216, 216, 216));
            }
        }

        private void DrawCountdown(SpriteBatch sprite, Rectangle bounds, int tickCount)
        {
            int remainingSeconds = GetRemainingSeconds(tickCount);
            int[] digits =
            {
                Math.Clamp(remainingSeconds / 60 / 10, 0, 9),
                Math.Clamp(remainingSeconds / 60 % 10, 0, 9),
                Math.Clamp(remainingSeconds % 60 / 10, 0, 9),
                Math.Clamp(remainingSeconds % 60 % 10, 0, 9)
            };

            bool hasDigitArt = _countdownCommaTexture != null;
            for (int i = 0; i < digits.Length && hasDigitArt; i++)
            {
                hasDigitArt = _countdownDigitTextures[digits[i]] != null;
            }

            if (hasDigitArt)
            {
                for (int i = 0; i < digits.Length; i++)
                {
                    int digit = digits[i];
                    Point origin = _countdownDigitOriginsByValue[digit];
                    Point drawOrigin = _layout.CountdownDigitOrigins[i];
                    sprite.Draw(
                        _countdownDigitTextures[digit],
                        new Vector2(bounds.X + drawOrigin.X - origin.X, bounds.Y + drawOrigin.Y - origin.Y),
                        Color.White);
                }

                sprite.Draw(
                    _countdownCommaTexture,
                    new Vector2(bounds.X + _layout.CountdownCommaOrigin.X - _countdownCommaTextureOrigin.X, bounds.Y + _layout.CountdownCommaOrigin.Y - _countdownCommaTextureOrigin.Y),
                    Color.White);
                return;
            }

            if (_font == null)
            {
                return;
            }

            string timerText = $"{remainingSeconds / 60:00}:{remainingSeconds % 60:00}";
            DrawShadowedText(sprite, timerText, new Vector2(bounds.X + 95, bounds.Y + 40), new Color(255, 236, 163));
        }

        private void DrawAttemptMessage(SpriteBatch sprite, Rectangle bounds)
        {
            int attemptNumber = Math.Clamp(_answerCount <= 0 ? 1 : _answerCount, 1, 2);
            DrawShadowedText(
                sprite,
                string.Format(_attemptMessageFormat, attemptNumber),
                new Vector2(bounds.X + _layout.AttemptMessageOrigin.X, bounds.Y + _layout.AttemptMessageOrigin.Y),
                new Color(197, 42, 26));
        }

        private void DrawInputText(SpriteBatch sprite, Rectangle inputBounds)
        {
            if (_frameTexture == null)
            {
                DrawBox(sprite, inputBounds, FallbackInputBackgroundColor, FallbackInputBorderColor);
            }

            Vector2 textPosition = new(inputBounds.X + 2, inputBounds.Y - 2);
            InputVisualState visualState = BuildInputVisualState(Math.Max(1, inputBounds.Width - 4));
            if (!string.IsNullOrEmpty(visualState.VisibleText))
            {
                if (visualState.VisibleCommittedPrefix.Length > 0)
                {
                    sprite.DrawString(_font, visualState.VisibleCommittedPrefix, textPosition, Color.Black);
                }

                float committedPrefixWidth = MeasureTextWidth(visualState.VisibleCommittedPrefix);
                Vector2 compositionPosition = textPosition + new Vector2(committedPrefixWidth, 0f);
                if (visualState.VisibleComposition.Length > 0)
                {
                    sprite.DrawString(_font, visualState.VisibleComposition, compositionPosition, InputCompositionColor);
                    DrawCompositionUnderline(sprite, compositionPosition, visualState.VisibleComposition, inputBounds);
                }

                if (visualState.VisibleCommittedSuffix.Length > 0)
                {
                    Vector2 suffixPosition = compositionPosition + new Vector2(MeasureTextWidth(visualState.VisibleComposition), 0f);
                    sprite.DrawString(_font, visualState.VisibleCommittedSuffix, suffixPosition, Color.Black);
                }
            }

            if (ShouldDrawCaret())
            {
                float caretX = textPosition.X + MeasureTextWidth(visualState.VisibleText[..visualState.VisibleCaretIndex]);
                Rectangle caretBounds = new((int)Math.Round(caretX), inputBounds.Y + 2, 1, Math.Max(1, inputBounds.Height - 4));
                sprite.Draw(_pixelTexture, caretBounds, InputCaretColor);
            }
        }

        private void DrawFallbackMessage(SpriteBatch sprite, string message, Rectangle bounds, Color color)
        {
            if (_font == null)
            {
                return;
            }

            Vector2 size = _font.MeasureString(message);
            Vector2 position = new(
                bounds.X + Math.Max(0, (bounds.Width - size.X) / 2f),
                bounds.Y + Math.Max(0, (bounds.Height - size.Y) / 2f));
            DrawShadowedText(sprite, message, position, color);
        }

        private void DrawShadowedText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + new Vector2(1f, 1f), Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private void DrawBox(SpriteBatch sprite, Rectangle bounds, Color fillColor, Color borderColor)
        {
            sprite.Draw(_pixelTexture, bounds, fillColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), borderColor);
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            bool ctrlHeld = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);

            if (ctrlHeld && Pressed(keyboardState, Keys.V))
            {
                PasteClipboardText();
            }

            if (Pressed(keyboardState, Keys.Back))
            {
                if (_compositionText.Length > 0)
                {
                    ClearCompositionText();
                }
                else if (_caretIndex > 0)
                {
                    RemoveCharacterBeforeCaret();
                }

                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, Keys.Delete))
            {
                if (_compositionText.Length > 0)
                {
                    ClearCompositionText();
                }
                else if (_caretIndex < _inputText.Length)
                {
                    RemoveCharacterAtCaret();
                }

                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, Keys.Left))
            {
                ClearCompositionText();
                _caretIndex = GetPreviousCaretStop(_inputText, _caretIndex);
                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, Keys.Right))
            {
                ClearCompositionText();
                _caretIndex = GetNextCaretStop(_inputText, _caretIndex);
                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, Keys.Home))
            {
                ClearCompositionText();
                _caretIndex = 0;
                _caretBlinkTick = Environment.TickCount;
            }

            if (Pressed(keyboardState, Keys.End))
            {
                ClearCompositionText();
                _caretIndex = _inputText.Length;
                _caretBlinkTick = Environment.TickCount;
            }
        }

        private bool CanSubmitAnswer()
        {
            return !string.IsNullOrWhiteSpace(_inputText) && GetRemainingSeconds(Environment.TickCount) > 0;
        }

        private string BuildVisibleInputText()
        {
            if (_compositionText.Length == 0)
            {
                return _inputText;
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _caretIndex, 0, _inputText.Length);
            return _inputText.Insert(insertionIndex, _compositionText);
        }

        private string GetVisibleCommittedInput()
        {
            if (_compositionText.Length == 0)
            {
                return _inputText;
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _caretIndex, 0, _inputText.Length);
            return _inputText[..insertionIndex];
        }

        private string GetVisibleCompositionInput()
        {
            return _compositionText;
        }

        private float MeasureTextWidth(string text)
        {
            return string.IsNullOrEmpty(text) || _font == null
                ? 0f
                : _font.MeasureString(text).X;
        }

        private bool ShouldDrawCaret()
        {
            return CapturesKeyboardInput && ((Environment.TickCount - _caretBlinkTick) % 1000) < 500;
        }

        private void InsertCharacter(char character)
        {
            int insertionIndex = Math.Clamp(_caretIndex, 0, _inputText.Length);
            _inputText = _inputText.Insert(insertionIndex, character.ToString());
            _caretIndex = insertionIndex + 1;
            _caretBlinkTick = Environment.TickCount;
        }

        private void DrawCompositionUnderline(SpriteBatch sprite, Vector2 compositionPosition, string compositionText, Rectangle inputBounds)
        {
            float underlineWidth = MeasureTextWidth(compositionText);
            if (underlineWidth <= 0f)
            {
                return;
            }

            int underlineX = (int)Math.Floor(compositionPosition.X);
            int underlineY = Math.Min(inputBounds.Bottom - 2, inputBounds.Y + _font.LineSpacing + 1);
            Rectangle underlineBounds = new(underlineX, underlineY, Math.Max(1, (int)Math.Ceiling(underlineWidth)), 1);
            sprite.Draw(_pixelTexture, underlineBounds, InputCompositionUnderlineColor);
        }

        private static Rectangle FitTexture(Rectangle bounds, int width, int height, bool upscale)
        {
            if (width <= 0 || height <= 0)
            {
                return bounds;
            }

            float widthRatio = bounds.Width / (float)width;
            float heightRatio = bounds.Height / (float)height;
            float scale = Math.Min(widthRatio, heightRatio);
            if (!upscale)
            {
                scale = Math.Min(1f, scale);
            }

            int scaledWidth = Math.Max(1, (int)Math.Round(width * scale));
            int scaledHeight = Math.Max(1, (int)Math.Round(height * scale));
            return new Rectangle(
                bounds.X + ((bounds.Width - scaledWidth) / 2),
                bounds.Y + ((bounds.Height - scaledHeight) / 2),
                scaledWidth,
                scaledHeight);
        }

        private int GetRemainingSeconds(int tickCount)
        {
            if (_expiresAt == int.MinValue)
            {
                return 0;
            }

            int remainingMs = Math.Max(0, _expiresAt - tickCount);
            return (remainingMs + 999) / 1000;
        }

        private void DrawImeCandidateWindow(SpriteBatch sprite, Rectangle inputBounds)
        {
            if (_font == null || !_candidateListState.HasCandidates)
            {
                return;
            }

            Rectangle bounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport, inputBounds);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            DrawBox(sprite, bounds, new Color(33, 33, 41, 235), new Color(214, 214, 214, 220));
            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            int rowHeight = Math.Max(_font.LineSpacing + 1, 16);
            int numberWidth = (int)Math.Ceiling(_font.MeasureString($"{Math.Max(1, count)}.").X);
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = start + i;
                string numberText = $"{i + 1}.";
                Rectangle rowBounds = new(bounds.X + 2, bounds.Y + 2 + (i * rowHeight), bounds.Width - 4, rowHeight);
                bool selected = candidateIndex == _candidateListState.Selection;
                if (selected)
                {
                    sprite.Draw(_pixelTexture, rowBounds, new Color(89, 108, 147, 220));
                }

                sprite.DrawString(_font, numberText, new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222));
                sprite.DrawString(
                    _font,
                    _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                    new Vector2(rowBounds.X + 8 + numberWidth, rowBounds.Y),
                    selected ? Color.White : new Color(240, 235, 200));
            }
        }

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport, Rectangle inputBounds)
        {
            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0 || _font == null)
            {
                return Rectangle.Empty;
            }

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

        private Rectangle GetInputBounds()
        {
            Rectangle bounds = GetWindowBounds();
            return new Rectangle(
                bounds.X + _layout.InputOrigin.X,
                bounds.Y + _layout.InputOrigin.Y,
                InputWidth,
                InputHeight);
        }

        private InputVisualState BuildInputVisualState(int maxWidth)
        {
            string displayText = BuildVisibleInputText();
            if (string.IsNullOrEmpty(displayText))
            {
                return new InputVisualState(string.Empty, 0, 0, -1, 0);
            }

            int caretIndex = ResolveDisplayCaretIndex();
            int clampedCaretIndex = Math.Clamp(caretIndex, 0, displayText.Length);
            int compositionStart = -1;
            int compositionLength = 0;
            if (_compositionText.Length > 0)
            {
                compositionStart = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _caretIndex, 0, displayText.Length);
                compositionLength = Math.Min(_compositionText.Length, Math.Max(0, displayText.Length - compositionStart));
            }

            int visibleStart = 0;
            while (visibleStart < clampedCaretIndex)
            {
                if (MeasureTextWidth(displayText[visibleStart..clampedCaretIndex]) <= maxWidth)
                {
                    break;
                }

                visibleStart = GetNextCaretStop(displayText, visibleStart);
            }

            int visibleEnd = displayText.Length;
            while (visibleEnd > clampedCaretIndex)
            {
                if (MeasureTextWidth(displayText[visibleStart..visibleEnd]) <= maxWidth)
                {
                    break;
                }

                visibleEnd = GetPreviousCaretStop(displayText, visibleEnd);
            }

            if (visibleEnd < clampedCaretIndex)
            {
                visibleEnd = clampedCaretIndex;
            }

            string visibleText = displayText[visibleStart..visibleEnd];
            int visibleCaretIndex = clampedCaretIndex - visibleStart;
            int visibleCompositionStart = -1;
            int visibleCompositionLength = 0;
            if (compositionStart >= 0)
            {
                int compositionEnd = compositionStart + compositionLength;
                int clampedVisibleCompositionStart = Math.Clamp(compositionStart - visibleStart, 0, visibleText.Length);
                int clampedVisibleCompositionEnd = Math.Clamp(compositionEnd - visibleStart, 0, visibleText.Length);
                if (clampedVisibleCompositionEnd > clampedVisibleCompositionStart)
                {
                    visibleCompositionStart = clampedVisibleCompositionStart;
                    visibleCompositionLength = clampedVisibleCompositionEnd - clampedVisibleCompositionStart;
                }
            }

            return new InputVisualState(
                visibleText,
                visibleStart,
                visibleCaretIndex,
                visibleCompositionStart,
                visibleCompositionLength);
        }

        private int ResolveCaretIndexFromMouseX(int mouseX)
        {
            if (_font == null || string.IsNullOrEmpty(_inputText))
            {
                return Math.Clamp(mouseX < GetInputBounds().Center.X ? 0 : _inputText.Length, 0, _inputText.Length);
            }

            Rectangle inputBounds = GetInputBounds();
            InputVisualState visualState = BuildInputVisualState(Math.Max(1, inputBounds.Width - 4));
            float relativeX = mouseX - (inputBounds.X + 2);
            if (relativeX <= 0f)
            {
                return visualState.VisibleStart;
            }

            foreach (int caretStop in EnumerateCaretStops(visualState.VisibleText))
            {
                if (caretStop <= 0)
                {
                    continue;
                }

                float width = MeasureTextWidth(visualState.VisibleText[..caretStop]);
                if (relativeX < width)
                {
                    int previousCaretStop = GetPreviousCaretStop(visualState.VisibleText, caretStop);
                    float previousWidth = previousCaretStop <= 0 ? 0f : MeasureTextWidth(visualState.VisibleText[..previousCaretStop]);
                    int resolvedVisibleCaret = relativeX - previousWidth <= width - relativeX ? previousCaretStop : caretStop;
                    return visualState.VisibleStart + resolvedVisibleCaret;
                }
            }

            return _inputText.Length;
        }

        private int ResolveDisplayCaretIndex()
        {
            if (_compositionText.Length == 0)
            {
                return Math.Clamp(_caretIndex, 0, _inputText.Length);
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _caretIndex, 0, _inputText.Length);
            int compositionCaret = _compositionCaretIndex >= 0
                ? Math.Clamp(_compositionCaretIndex, 0, _compositionText.Length)
                : _compositionText.Length;
            return insertionIndex + compositionCaret;
        }

        private void RemoveCharacterBeforeCaret()
        {
            int currentCaret = Math.Clamp(_caretIndex, 0, _inputText.Length);
            int previousCaret = GetPreviousCaretStop(_inputText, currentCaret);
            if (previousCaret >= currentCaret)
            {
                return;
            }

            _inputText = _inputText.Remove(previousCaret, currentCaret - previousCaret);
            _caretIndex = previousCaret;
        }

        private void RemoveCharacterAtCaret()
        {
            int currentCaret = Math.Clamp(_caretIndex, 0, _inputText.Length);
            int nextCaret = GetNextCaretStop(_inputText, currentCaret);
            if (nextCaret <= currentCaret)
            {
                return;
            }

            _inputText = _inputText.Remove(currentCaret, nextCaret - currentCaret);
        }

        private void PasteClipboardText()
        {
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    return;
                }

                HandleCommittedText(System.Windows.Forms.Clipboard.GetText());
            }
            catch
            {
            }
        }

        private static int GetTextElementCount(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                count++;
            }

            return count;
        }

        private static IEnumerable<int> EnumerateCaretStops(string value)
        {
            yield return 0;
            if (string.IsNullOrEmpty(value))
            {
                yield break;
            }

            TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
            while (enumerator.MoveNext())
            {
                yield return enumerator.ElementIndex + enumerator.GetTextElement().Length;
            }
        }

        private static int GetPreviousCaretStop(string value, int caretIndex)
        {
            int clampedCaretIndex = Math.Clamp(caretIndex, 0, value?.Length ?? 0);
            int previous = 0;
            foreach (int stop in EnumerateCaretStops(value))
            {
                if (stop >= clampedCaretIndex)
                {
                    break;
                }

                previous = stop;
            }

            return previous;
        }

        private static int GetNextCaretStop(string value, int caretIndex)
        {
            int length = value?.Length ?? 0;
            int clampedCaretIndex = Math.Clamp(caretIndex, 0, length);
            foreach (int stop in EnumerateCaretStops(value))
            {
                if (stop > clampedCaretIndex)
                {
                    return stop;
                }
            }

            return length;
        }

        private static Texture2D CreateFallbackFrameTexture(GraphicsDevice graphicsDevice)
        {
            Texture2D texture = new Texture2D(graphicsDevice, FallbackWindowWidth, FallbackWindowHeight);
            Color[] data = new Color[FallbackWindowWidth * FallbackWindowHeight];
            Color outer = new(214, 214, 214);
            Color inner = new(36, 36, 40);
            Color background = new(64, 64, 70);

            for (int y = 0; y < FallbackWindowHeight; y++)
            {
                for (int x = 0; x < FallbackWindowWidth; x++)
                {
                    int index = y * FallbackWindowWidth + x;
                    bool outerBorder = x == 0 || y == 0 || x == FallbackWindowWidth - 1 || y == FallbackWindowHeight - 1;
                    bool innerBorder = x == 1 || y == 1 || x == FallbackWindowWidth - 2 || y == FallbackWindowHeight - 2;
                    data[index] = outerBorder
                        ? outer
                        : innerBorder
                            ? inner
                            : background;
                }
            }

            texture.SetData(data);
            return texture;
        }

        private void OnSubmitButtonReleased(UIObject sender)
        {
            if (CanSubmitAnswer())
            {
                SubmitRequested?.Invoke(_inputText);
            }
        }
    }
}
