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
        private const int WindowWidth = 358;
        private const int WindowHeight = 286;
        private const int HeaderHeight = 28;
        private const int TextMarginX = 18;
        private const int TextTopY = 16;
        private const int ChallengeTopY = 78;
        private const int ChallengeWidth = 320;
        private const int ChallengeHeight = 116;
        private const int InputTopY = 212;
        private const int InputHeight = 24;
        private const int InputMaxLength = 12;

        private readonly string _windowName;
        private readonly bool _adminVariant;
        private readonly Texture2D _pixelTexture;

        private SpriteFont _font;
        private Texture2D _challengeTexture;
        private KeyboardState _previousKeyboardState;
        private string _inputText = string.Empty;
        private string _statusText = string.Empty;
        private string _compositionText = string.Empty;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;
        private bool _isFirstChallenge = true;
        private int _expiresAt = int.MinValue;

        public AntiMacroChallengeWindow(string windowName, bool adminVariant, GraphicsDevice graphicsDevice)
            : base(new DXObject(0, 0, CreateFrameTexture(graphicsDevice), 0))
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

        public void Configure(Texture2D challengeTexture, int expiresAt, bool isFirstChallenge, string statusText)
        {
            if (!ReferenceEquals(_challengeTexture, challengeTexture))
            {
                _challengeTexture?.Dispose();
                _challengeTexture = challengeTexture;
            }

            _expiresAt = expiresAt;
            _isFirstChallenge = isFirstChallenge;
            _statusText = statusText ?? string.Empty;
            _inputText = string.Empty;
        }

        public void ClearChallenge()
        {
            _challengeTexture?.Dispose();
            _challengeTexture = null;
            _expiresAt = int.MinValue;
            _isFirstChallenge = true;
            _statusText = string.Empty;
            _inputText = string.Empty;
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

            if (Pressed(keyboardState, Keys.Enter) && !string.IsNullOrWhiteSpace(_inputText))
            {
                SubmitRequested?.Invoke(_inputText);
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
            Rectangle bounds = new(Position.X, Position.Y, WindowWidth, WindowHeight);
            Rectangle headerBounds = new(bounds.X, bounds.Y, bounds.Width, HeaderHeight);
            Rectangle challengeBounds = new(bounds.X + 19, bounds.Y + ChallengeTopY, ChallengeWidth, ChallengeHeight);
            Rectangle inputBounds = new(bounds.X + 19, bounds.Y + InputTopY, ChallengeWidth, InputHeight);

            DrawBox(sprite, headerBounds, _adminVariant ? new Color(113, 55, 55) : new Color(55, 83, 122), Color.White);
            DrawBox(sprite, challengeBounds, new Color(18, 18, 18), new Color(176, 176, 176));
            DrawBox(sprite, inputBounds, new Color(250, 250, 250), new Color(45, 45, 45));

            if (_challengeTexture != null)
            {
                Rectangle fittedBounds = FitTexture(challengeBounds, _challengeTexture.Width, _challengeTexture.Height);
                sprite.Draw(_challengeTexture, fittedBounds, Color.White);
            }
            else
            {
                DrawFallbackMessage(sprite, "Packet challenge image unavailable.", challengeBounds, new Color(216, 216, 216));
            }

            if (_font == null)
            {
                return;
            }

            string title = _adminVariant ? "Admin Anti-Macro" : "Anti-Macro";
            string modeText = _isFirstChallenge ? "Initial challenge" : "Retry challenge";
            string timerText = $"Time left: {GetRemainingSeconds(tickCount)}s";
            string inputText = BuildVisibleInputText();

            DrawShadowedText(sprite, title, new Vector2(bounds.X + TextMarginX, bounds.Y + TextTopY), Color.White);
            DrawShadowedText(sprite, timerText, new Vector2(bounds.Right - TextMarginX - _font.MeasureString(timerText).X, bounds.Y + TextTopY), new Color(255, 236, 163));
            DrawShadowedText(sprite, modeText, new Vector2(bounds.X + TextMarginX, bounds.Y + 44), new Color(224, 224, 224));
            if (!string.IsNullOrWhiteSpace(_statusText))
            {
                DrawShadowedText(sprite, TrimText(_statusText, bounds.Width - (TextMarginX * 2)), new Vector2(bounds.X + TextMarginX, bounds.Y + 60), new Color(206, 228, 255));
            }

            DrawShadowedText(sprite, "Answer", new Vector2(bounds.X + TextMarginX, bounds.Y + 193), new Color(220, 220, 220));
            sprite.DrawString(_font, inputText, new Vector2(inputBounds.X + 8, inputBounds.Y + 4), string.IsNullOrEmpty(_inputText) ? new Color(130, 130, 130) : Color.Black);
            DrawImeCandidateWindow(sprite, inputBounds);
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

        private static Rectangle FitTexture(Rectangle bounds, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return bounds;
            }

            float widthRatio = bounds.Width / (float)width;
            float heightRatio = bounds.Height / (float)height;
            float scale = Math.Min(widthRatio, heightRatio);
            int scaledWidth = Math.Max(1, (int)Math.Round(width * scale));
            int scaledHeight = Math.Max(1, (int)Math.Round(height * scale));
            return new Rectangle(
                bounds.X + ((bounds.Width - scaledWidth) / 2),
                bounds.Y + ((bounds.Height - scaledHeight) / 2),
                scaledWidth,
                scaledHeight);
        }

        private static string TrimText(string text, int maxPixels)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length <= 64 ? text : $"{text[..61]}...";
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

        private string BuildVisibleInputText()
        {
            if (string.IsNullOrEmpty(_inputText))
            {
                return string.IsNullOrEmpty(_compositionText)
                    ? "Type answer and press Enter"
                    : _compositionText;
            }

            return $"{_inputText}{_compositionText}";
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

        private static Texture2D CreateFrameTexture(GraphicsDevice graphicsDevice)
        {
            Texture2D texture = new Texture2D(graphicsDevice, WindowWidth, WindowHeight);
            Color[] data = new Color[WindowWidth * WindowHeight];
            Color outer = new(214, 214, 214);
            Color inner = new(36, 36, 40);
            Color background = new(64, 64, 70);

            for (int y = 0; y < WindowHeight; y++)
            {
                for (int x = 0; x < WindowWidth; x++)
                {
                    int index = y * WindowWidth + x;
                    bool outerBorder = x == 0 || y == 0 || x == WindowWidth - 1 || y == WindowHeight - 1;
                    bool innerBorder = x == 1 || y == 1 || x == WindowWidth - 2 || y == WindowHeight - 2;
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
    }
}
