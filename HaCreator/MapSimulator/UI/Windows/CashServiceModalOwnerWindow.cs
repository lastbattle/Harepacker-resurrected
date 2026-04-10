using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class CashServiceModalOwnerWindow : UIWindowBase
    {
        internal sealed class ActionButtonState
        {
            public string Label { get; init; } = string.Empty;
            public bool IsPrimary { get; init; }
        }

        private const int DefaultWidth = 266;
        private const int DefaultHeight = 158;
        private const int TitleOffsetX = 16;
        private const int TitleOffsetY = 14;
        private const int BodyOffsetX = 16;
        private const int BodyOffsetY = 34;
        private const int FooterPadding = 16;
        private const int ButtonHeight = 20;
        private const int ButtonWidth = 78;
        private const int ButtonGap = 10;
        private const int ListTopPadding = 8;
        private const int ListRowHeight = 18;
        private const int InputHeight = 20;
        private const int InputPadding = 4;

        private readonly Texture2D _pixelTexture;
        private readonly string _windowName;
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly List<string> _detailLines = new();
        private readonly List<string> _worldNames = new();
        private readonly List<string> _giftRows = new();
        private readonly Dictionary<int, Rectangle> _buttonBounds = new();
        private readonly Action<int> _buttonHandler;
        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private string _title = string.Empty;
        private string _body = string.Empty;
        private string _footer = string.Empty;
        private string _inputValue = string.Empty;
        private string _inputPlaceholder = string.Empty;
        private bool _inputActive;
        private int _inputMaxLength = 32;
        private int _selectedGiftIndex;
        private bool _showGiftRows;
        private bool _showWorldRows;
        private Rectangle _inputBounds;

        internal CashServiceModalOwnerWindow(
            string windowName,
            Texture2D frameTexture,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            Action<int> buttonHandler)
            : base(new DXObject(0, 0, frameTexture ?? CreateFallbackFrame(device), 0))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _buttonHandler = buttonHandler;
            _pixelTexture = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
            SupportsDragging = false;
            UpdateLayout();
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        internal IReadOnlyList<ActionButtonState> Buttons { get; private set; } = Array.Empty<ActionButtonState>();

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        internal void Configure(
            string title,
            string body,
            IEnumerable<string> detailLines,
            IEnumerable<ActionButtonState> buttons,
            string footer = null,
            string inputValue = null,
            string inputPlaceholder = null,
            bool inputActive = false,
            int inputMaxLength = 32,
            IEnumerable<string> giftRows = null,
            int selectedGiftIndex = 0,
            IEnumerable<string> worldNames = null)
        {
            _title = title ?? string.Empty;
            _body = body ?? string.Empty;
            _footer = footer ?? string.Empty;
            _detailLines.Clear();
            if (detailLines != null)
            {
                _detailLines.AddRange(detailLines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
            }

            _giftRows.Clear();
            if (giftRows != null)
            {
                _giftRows.AddRange(giftRows.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
            }

            _worldNames.Clear();
            if (worldNames != null)
            {
                _worldNames.AddRange(worldNames.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));
            }

            Buttons = buttons?.Where(button => button != null).ToArray() ?? Array.Empty<ActionButtonState>();
            _footer = footer ?? string.Empty;
            _inputValue = inputValue ?? string.Empty;
            _inputPlaceholder = inputPlaceholder ?? string.Empty;
            _inputActive = inputActive;
            _inputMaxLength = Math.Max(0, inputMaxLength);
            _selectedGiftIndex = Math.Clamp(selectedGiftIndex, 0, Math.Max(0, _giftRows.Count - 1));
            _showGiftRows = _giftRows.Count > 0;
            _showWorldRows = _worldNames.Count > 0;
            UpdateLayout();
        }

        internal string InputValue => _inputValue;
        internal int SelectedGiftIndex => _selectedGiftIndex;

        public override void Show()
        {
            CenterOnScreen();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
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
            MouseState mouseState = Mouse.GetState();

            if (Pressed(keyboardState, Keys.Escape))
            {
                TriggerLastButton();
            }
            else if (_showGiftRows && Pressed(keyboardState, Keys.Up))
            {
                _selectedGiftIndex = Math.Max(0, _selectedGiftIndex - 1);
            }
            else if (_showGiftRows && Pressed(keyboardState, Keys.Down))
            {
                _selectedGiftIndex = Math.Min(Math.Max(0, _giftRows.Count - 1), _selectedGiftIndex + 1);
            }
            else if (Pressed(keyboardState, Keys.Enter))
            {
                TriggerPrimaryButton();
            }

            if (_inputActive)
            {
                if (Pressed(keyboardState, Keys.Back) && _inputValue.Length > 0)
                {
                    _inputValue = _inputValue[..^1];
                }
                else
                {
                    AppendTypedCharacters(keyboardState);
                }
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            foreach (KeyValuePair<int, Rectangle> buttonBounds in _buttonBounds)
            {
                if (!buttonBounds.Value.Contains(mouseState.Position))
                {
                    continue;
                }

                mouseCursor?.SetMouseCursorMovedToClickableItem();
                if (Released(mouseState))
                {
                    _buttonHandler?.Invoke(buttonBounds.Key);
                    _previousMouseState = mouseState;
                    return true;
                }

                _previousMouseState = mouseState;
                return true;
            }

            if (_showGiftRows)
            {
                for (int i = 0; i < _giftRows.Count; i++)
                {
                    Rectangle rowBounds = GetGiftRowBounds(i);
                    if (!rowBounds.Contains(mouseState.Position))
                    {
                        continue;
                    }

                    if (Released(mouseState))
                    {
                        _selectedGiftIndex = i;
                    }

                    _previousMouseState = mouseState;
                    return true;
                }
            }

            if (_inputActive && _inputBounds.Contains(mouseState.Position))
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                _previousMouseState = mouseState;
                return true;
            }

            _previousMouseState = mouseState;
            return base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
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

            Vector2 cursor = new(Position.X + TitleOffsetX, Position.Y + TitleOffsetY);
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, _title, cursor, Color.White);

            cursor = new Vector2(Position.X + BodyOffsetX, Position.Y + BodyOffsetY);
            foreach (string line in WrapText(_body, ResolveContentWidth()))
            {
                SelectorWindowDrawing.DrawShadowedText(sprite, _font, line, cursor, new Color(236, 236, 236));
                cursor.Y += _font.LineSpacing;
            }

            foreach (string detailLine in _detailLines)
            {
                SelectorWindowDrawing.DrawShadowedText(sprite, _font, detailLine, cursor, new Color(255, 226, 158));
                cursor.Y += _font.LineSpacing;
            }

            if (_showGiftRows)
            {
                cursor.Y += ListTopPadding;
                for (int i = 0; i < _giftRows.Count; i++)
                {
                    Rectangle bounds = GetGiftRowBounds(i);
                    if (i == _selectedGiftIndex)
                    {
                        sprite.Draw(_pixelTexture, bounds, new Color(255, 208, 120, 48));
                    }

                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        _giftRows[i],
                        new Vector2(bounds.X + 4, bounds.Y + 2),
                        i == _selectedGiftIndex ? Color.White : new Color(214, 214, 214));
                }

                cursor.Y += _giftRows.Count * ListRowHeight;
            }

            if (_showWorldRows)
            {
                cursor.Y += ListTopPadding;
                foreach (string worldName in _worldNames.Take(5))
                {
                    SelectorWindowDrawing.DrawShadowedText(sprite, _font, worldName, cursor, new Color(191, 223, 255));
                    cursor.Y += _font.LineSpacing;
                }
            }

            if (_inputActive)
            {
                sprite.Draw(_pixelTexture, _inputBounds, new Color(24, 24, 24, 220));
                DrawBorder(sprite, _inputBounds, new Color(163, 163, 163));
                string inputText = string.IsNullOrWhiteSpace(_inputValue) ? _inputPlaceholder : _inputValue;
                Color inputColor = string.IsNullOrWhiteSpace(_inputValue) ? new Color(144, 144, 144) : Color.White;
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    inputText,
                    new Vector2(_inputBounds.X + InputPadding, _inputBounds.Y + InputPadding),
                    inputColor);
            }

            if (!string.IsNullOrWhiteSpace(_footer))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _footer,
                    new Vector2(Position.X + BodyOffsetX, Position.Y + ResolveFrameHeight() - FooterPadding - _font.LineSpacing),
                    new Color(255, 226, 158));
            }

            DrawButtons(sprite);
        }

        private void DrawButtons(SpriteBatch sprite)
        {
            foreach (KeyValuePair<int, Rectangle> buttonBounds in _buttonBounds)
            {
                ActionButtonState button = Buttons[buttonBounds.Key];
                Color fill = button.IsPrimary ? new Color(110, 75, 22, 220) : new Color(48, 48, 48, 220);
                sprite.Draw(_pixelTexture, buttonBounds.Value, fill);
                DrawBorder(sprite, buttonBounds.Value, button.IsPrimary ? new Color(255, 221, 141) : new Color(180, 180, 180));

                Vector2 labelSize = _font.MeasureString(button.Label);
                Vector2 labelPosition = new(
                    buttonBounds.Value.X + Math.Max(0, (buttonBounds.Value.Width - labelSize.X) / 2f),
                    buttonBounds.Value.Y + Math.Max(0, (buttonBounds.Value.Height - labelSize.Y) / 2f));
                SelectorWindowDrawing.DrawShadowedText(sprite, _font, button.Label, labelPosition, Color.White);
            }
        }

        private Rectangle GetGiftRowBounds(int index)
        {
            int contentStartY = Position.Y + BodyOffsetY + (_font?.LineSpacing ?? 14) * (Math.Max(1, WrapText(_body, ResolveContentWidth()).Count()) + _detailLines.Count) + ListTopPadding;
            return new Rectangle(Position.X + BodyOffsetX, contentStartY + (index * ListRowHeight), ResolveContentWidth(), ListRowHeight - 2);
        }

        private void UpdateLayout()
        {
            CenterOnScreen();
            int frameWidth = ResolveFrameWidth();
            int frameHeight = ResolveFrameHeight();
            _buttonBounds.Clear();
            int buttonCount = Buttons.Count;
            int totalButtonWidth = buttonCount > 0
                ? (buttonCount * ButtonWidth) + ((buttonCount - 1) * ButtonGap)
                : 0;
            int startX = Position.X + Math.Max(0, (frameWidth - totalButtonWidth) / 2);
            int buttonY = Position.Y + frameHeight - FooterPadding - ButtonHeight;
            for (int i = 0; i < buttonCount; i++)
            {
                _buttonBounds[i] = new Rectangle(startX + (i * (ButtonWidth + ButtonGap)), buttonY, ButtonWidth, ButtonHeight);
            }

            _inputBounds = new Rectangle(
                Position.X + BodyOffsetX,
                Position.Y + frameHeight - FooterPadding - ButtonHeight - InputHeight - 8,
                Math.Max(120, frameWidth - (BodyOffsetX * 2)),
                InputHeight);
        }

        private void CenterOnScreen()
        {
            int frameWidth = ResolveFrameWidth();
            int frameHeight = ResolveFrameHeight();
            Position = new Point(
                Math.Max(24, (_screenWidth / 2) - (frameWidth / 2)),
                Math.Max(24, (_screenHeight / 2) - (frameHeight / 2)));
        }

        private int ResolveFrameWidth()
        {
            return Frame?.Width > 0 ? Frame.Width : DefaultWidth;
        }

        private int ResolveFrameHeight()
        {
            return Frame?.Height > 0 ? Frame.Height : DefaultHeight;
        }

        private int ResolveContentWidth()
        {
            return Math.Max(140, ResolveFrameWidth() - (BodyOffsetX * 2));
        }

        private IEnumerable<string> WrapText(string text, int maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            List<string> lines = new();
            string currentLine = string.Empty;
            foreach (string word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (_font.MeasureString(candidate).X <= maxWidth || string.IsNullOrEmpty(currentLine))
                {
                    currentLine = candidate;
                    continue;
                }

                lines.Add(currentLine);
                currentLine = word;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private void DrawBorder(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private void TriggerPrimaryButton()
        {
            if (Buttons.Count == 0)
            {
                return;
            }

            int index = Buttons.Select((button, idx) => new { button, idx }).FirstOrDefault(entry => entry.button.IsPrimary)?.idx ?? 0;
            _buttonHandler?.Invoke(index);
        }

        private void TriggerLastButton()
        {
            if (Buttons.Count == 0)
            {
                return;
            }

            _buttonHandler?.Invoke(Buttons.Count - 1);
        }

        private void AppendTypedCharacters(KeyboardState keyboardState)
        {
            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (_previousKeyboardState.IsKeyDown(key))
                {
                    continue;
                }

                char? appended = TranslateKey(key, shift);
                if (appended == null || _inputValue.Length >= _inputMaxLength)
                {
                    continue;
                }

                _inputValue += appended.Value;
            }
        }

        private static char? TranslateKey(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char value = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpperInvariant(value) : value;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            if (key == Keys.Space)
            {
                return ' ';
            }

            return null;
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private bool Released(MouseState mouseState)
        {
            return mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
        }

        private static Texture2D CreateFallbackFrame(GraphicsDevice device)
        {
            Texture2D texture = new(device, DefaultWidth, DefaultHeight);
            Color[] pixels = Enumerable.Repeat(new Color(34, 34, 34, 240), DefaultWidth * DefaultHeight).ToArray();
            texture.SetData(pixels);
            return texture;
        }
    }
}
