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
    internal sealed class AntiMacroChallengeWindow : UIWindowBase
    {
        private static readonly Point ChallengeOrigin = new(40, 122);
        private static readonly Point InputOrigin = new(42, 189);
        private static readonly Point AttemptMessageOrigin = new(222, 206);
        private static readonly Point[] CountdownDigitOrigins =
        {
            new Point(95, 39),
            new Point(115, 39),
            new Point(147, 39),
            new Point(167, 39)
        };
        private static readonly Point CountdownCommaOrigin = new(137, 39);

        private const int FallbackWindowWidth = 265;
        private const int FallbackWindowHeight = 250;
        private const int ChallengeWidth = 178;
        private const int ChallengeHeight = 53;
        private const int InputWidth = 194;
        private const int InputHeight = 16;
        private const int InputMaxLength = 12;
        private const string DefaultAttemptMessageFormat = "Attempt {0} of 2";

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
        private int _answerCount;
        private int _expiresAt = int.MinValue;

        public AntiMacroChallengeWindow(string windowName, bool adminVariant, GraphicsDevice graphicsDevice)
            : base(new DXObject(0, 0, CreateFallbackFrameTexture(graphicsDevice), 0))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _adminVariant = adminVariant;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        public event Action<string> SubmitRequested;

        public string CurrentInput => _inputText;
        public int ExpiresAt => _expiresAt;
        public bool IsAdminVariant => _adminVariant;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Hide()
        {
            base.Hide();
            ClearCompositionText();
        }

        public void ConfigureVisualAssets(
            Texture2D frameTexture,
            IReadOnlyList<Texture2D> digitTextures,
            IReadOnlyList<Point> digitOrigins,
            Texture2D commaTexture,
            Point commaOrigin,
            UIObject submitButton,
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

            if (Pressed(keyboardState, Keys.Back) && _inputText.Length > 0)
            {
                ClearCompositionText();
                _inputText = _inputText[..^1];
            }

            if (Pressed(keyboardState, Keys.Enter) && CanSubmitAnswer())
            {
                SubmitRequested?.Invoke(_inputText);
            }

            _submitButton?.SetEnabled(CanSubmitAnswer());
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
            Rectangle bounds = GetWindowBounds();
            Rectangle challengeBounds = new(
                bounds.X + ChallengeOrigin.X,
                bounds.Y + ChallengeOrigin.Y,
                ChallengeWidth,
                ChallengeHeight);
            Rectangle inputBounds = new(
                bounds.X + InputOrigin.X,
                bounds.Y + InputOrigin.Y,
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
                    _inputText += character;
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
                    Point drawOrigin = CountdownDigitOrigins[i];
                    sprite.Draw(
                        _countdownDigitTextures[digit],
                        new Vector2(bounds.X + drawOrigin.X - origin.X, bounds.Y + drawOrigin.Y - origin.Y),
                        Color.White);
                }

                sprite.Draw(
                    _countdownCommaTexture,
                    new Vector2(bounds.X + CountdownCommaOrigin.X - _countdownCommaTextureOrigin.X, bounds.Y + CountdownCommaOrigin.Y - _countdownCommaTextureOrigin.Y),
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
                new Vector2(bounds.X + AttemptMessageOrigin.X, bounds.Y + AttemptMessageOrigin.Y),
                new Color(197, 42, 26));
        }

        private void DrawInputText(SpriteBatch sprite, Rectangle inputBounds)
        {
            string inputText = BuildVisibleInputText();
            if (string.IsNullOrEmpty(inputText))
            {
                return;
            }

            sprite.DrawString(_font, inputText, new Vector2(inputBounds.X + 2, inputBounds.Y - 2), Color.Black);
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

        private bool CanSubmitAnswer()
        {
            return !string.IsNullOrWhiteSpace(_inputText) && GetRemainingSeconds(Environment.TickCount) > 0;
        }

        private string BuildVisibleInputText()
        {
            return $"{_inputText}{_compositionText}";
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
