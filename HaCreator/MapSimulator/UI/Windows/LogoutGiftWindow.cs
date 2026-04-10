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
    internal sealed class LogoutGiftWindow : UIWindowBase
    {
        private const int DefaultWidth = 250;
        private const int DefaultHeight = 236;
        private const int ClientIconTop = 182;
        private const int ClientIconLeft = 25;
        private const int ClientSelectButtonTop = 196;
        private const int ClientSelectButtonLeft = 21;
        private const int ClientColumnStride = 66;
        private const int ClientSelectButtonBaseId = 1000;
        private const int ClientItemIconSize = 40;
        private const int ClientSelectButtonWidth = 52;
        private const int ClientSelectButtonHeight = 18;
        private const int ClientSelectionHighlightPadding = 4;
        private const int CloseButtonSize = 16;

        private readonly Texture2D _pixel;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private MouseState _currentMouseState;
        private Func<LogoutGiftOwnerSnapshot> _snapshotProvider;
        private Func<int, string> _selectHandler;
        private Func<string> _closeHandler;
        private Action<string> _feedbackHandler;
        private Func<int, Texture2D> _itemIconProvider;
        private LogoutGiftOwnerSnapshot _snapshot = new();
        private bool _hasAuthoredFrameTexture;
        private LogoutGiftButtonSkin _buttonSkin;
        private int _pendingSelectionIndex;

        internal LogoutGiftWindow(GraphicsDevice device)
            : base(new DXObject(0, 0, CreateFrameTexture(device), 0))
        {
            _pixel = CreatePixelTexture(device);
            SupportsDragging = false;
        }

        public override string WindowName => MapSimulatorWindowNames.LogoutGift;
        public override bool CapturesKeyboardInput => IsVisible;
        internal Point ActiveFrameSize => new(CurrentFrame?.Width ?? DefaultWidth, CurrentFrame?.Height ?? DefaultHeight);

        internal void ConfigureVisualAssets(Texture2D frameTexture, LogoutGiftButtonSkin buttonSkin)
        {
            _hasAuthoredFrameTexture = frameTexture != null;
            Frame = frameTexture == null
                ? null
                : new DXObject(0, 0, frameTexture, 0);
            _buttonSkin = buttonSkin;
        }

        internal void SetSnapshotProvider(Func<LogoutGiftOwnerSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _snapshot = snapshotProvider?.Invoke() ?? new LogoutGiftOwnerSnapshot();
            _pendingSelectionIndex = ClampSelectionIndex(_snapshot.SelectedIndex);
        }

        internal void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
        }

        internal void SetActionHandlers(Func<int, string> selectHandler, Func<string> closeHandler, Action<string> feedbackHandler)
        {
            _selectHandler = selectHandler;
            _closeHandler = closeHandler;
            _feedbackHandler = feedbackHandler;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _snapshot = _snapshotProvider?.Invoke() ?? new LogoutGiftOwnerSnapshot();
            _pendingSelectionIndex = ClampSelectionIndex(_snapshot.SelectedIndex);
            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            _currentMouseState = mouseState;

            if (IsVisible)
            {
                if (Pressed(keyboardState, Keys.Escape))
                {
                    ShowFeedback(_closeHandler?.Invoke());
                }
                else if (Pressed(keyboardState, Keys.Left))
                {
                    NavigateSelection(-1);
                }
                else if (Pressed(keyboardState, Keys.Right))
                {
                    NavigateSelection(1);
                }
                else if (Pressed(keyboardState, Keys.Enter))
                {
                    ActivateSelection(_pendingSelectionIndex);
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

            Rectangle closeBounds = GetCloseBounds();
            if (closeBounds.Contains(mouseState.Position))
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                if (Released(mouseState))
                {
                    ShowFeedback(_closeHandler?.Invoke());
                    _previousMouseState = mouseState;
                    return true;
                }

                _previousMouseState = mouseState;
                return true;
            }

            for (int i = 0; i < _snapshot.Entries.Count; i++)
            {
                Rectangle slotBounds = GetSlotBounds(i);
                Rectangle buttonBounds = GetClientSelectButtonBounds(Position, i, _buttonSkin);
                if (buttonBounds.Contains(mouseState.Position))
                {
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    if (Released(mouseState))
                    {
                        ActivateSelection(i);
                        _previousMouseState = mouseState;
                        return true;
                    }

                    _previousMouseState = mouseState;
                    return true;
                }

                if (slotBounds.Contains(mouseState.Position))
                {
                    if (Released(mouseState))
                    {
                        _pendingSelectionIndex = i;
                    }

                    _previousMouseState = mouseState;
                    return true;
                }
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
            int TickCount)
        {
            if (!_hasAuthoredFrameTexture)
            {
                DrawPanel(sprite);
            }

            if (!CanDrawWindowText)
            {
                return;
            }

            if (!_hasAuthoredFrameTexture)
            {
                DrawWindowText(sprite, _snapshot.Title, new Vector2(Position.X + 16, Position.Y + 14), new Color(63, 40, 23), 0.48f);
                DrawMissingArtNotice(sprite);
            }

            for (int i = 0; i < _snapshot.Entries.Count; i++)
            {
                DrawEntry(sprite, _snapshot.Entries[i], i);
            }
        }

        private void DrawPanel(SpriteBatch sprite)
        {
            Rectangle bounds = GetWindowBounds();
            sprite.Draw(_pixel, bounds, new Color(246, 236, 213, 250));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 26), new Color(229, 211, 181));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(121, 87, 56));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(121, 87, 56));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(121, 87, 56));
            sprite.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(121, 87, 56));
            sprite.Draw(_pixel, new Rectangle(bounds.X + 12, bounds.Y + 84, bounds.Width - 24, 1), new Color(188, 166, 141));

            Rectangle closeBounds = GetCloseBounds();
            sprite.Draw(_pixel, closeBounds, new Color(181, 110, 90));
            sprite.Draw(_pixel, new Rectangle(closeBounds.X + 3, closeBounds.Y + 7, closeBounds.Width - 6, 2), Color.White);
            sprite.Draw(_pixel, new Rectangle(closeBounds.X + 7, closeBounds.Y + 3, 2, closeBounds.Height - 6), Color.White);
        }

        private void DrawEntry(SpriteBatch sprite, LogoutGiftEntrySnapshot entry, int index)
        {
            Rectangle slotBounds = GetSlotBounds(index);
            Rectangle iconBounds = GetClientIconBounds(Position, index);
            Rectangle buttonBounds = GetClientSelectButtonBounds(Position, index, _buttonSkin);
            bool selected = index == _pendingSelectionIndex;
            bool buttonHovered = buttonBounds.Contains(_currentMouseState.Position);
            bool buttonPressed = buttonHovered && _currentMouseState.LeftButton == ButtonState.Pressed;
            Color fill = selected ? new Color(255, 241, 194) : new Color(238, 228, 207);
            Color border = selected ? new Color(198, 142, 76) : new Color(163, 134, 103);

            if (!_hasAuthoredFrameTexture || selected)
            {
                sprite.Draw(_pixel, slotBounds, selected ? new Color(255, 244, 212, 210) : new Color(238, 228, 207, 110));
                sprite.Draw(_pixel, new Rectangle(slotBounds.X, slotBounds.Y, slotBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(slotBounds.X, slotBounds.Bottom - 1, slotBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(slotBounds.X, slotBounds.Y, 1, slotBounds.Height), border);
                sprite.Draw(_pixel, new Rectangle(slotBounds.Right - 1, slotBounds.Y, 1, slotBounds.Height), border);
            }

            Texture2D icon = ResolveItemIcon(entry.ItemId);
            if (icon != null)
            {
                sprite.Draw(icon, iconBounds, Color.White);
            }
            else
            {
                sprite.Draw(_pixel, iconBounds, new Color(228, 214, 188));
                sprite.Draw(_pixel, new Rectangle(iconBounds.X, iconBounds.Y, iconBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(iconBounds.X, iconBounds.Bottom - 1, iconBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(iconBounds.X, iconBounds.Y, 1, iconBounds.Height), border);
                sprite.Draw(_pixel, new Rectangle(iconBounds.Right - 1, iconBounds.Y, 1, iconBounds.Height), border);
            }

            if (!CanDrawWindowText)
            {
                return;
            }

            Texture2D buttonTexture = ResolveButtonTexture(selected, buttonHovered, buttonPressed);
            if (buttonTexture != null)
            {
                sprite.Draw(buttonTexture, buttonBounds, Color.White);
            }
            else
            {
                sprite.Draw(_pixel, buttonBounds, fill);
                sprite.Draw(_pixel, new Rectangle(buttonBounds.X, buttonBounds.Y, buttonBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(buttonBounds.X, buttonBounds.Bottom - 1, buttonBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(buttonBounds.X, buttonBounds.Y, 1, buttonBounds.Height), border);
                sprite.Draw(_pixel, new Rectangle(buttonBounds.Right - 1, buttonBounds.Y, 1, buttonBounds.Height), border);
                DrawCenteredText(sprite, "Select", buttonBounds, new Color(70, 49, 31), 0.27f);
            }
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string value = text.Length > 18 ? $"{text[..15]}..." : text;
            Vector2 size = MeasureWindowText(null, value, scale);
            float x = bounds.X + Math.Max(0f, (bounds.Width - size.X) / 2f);
            float y = bounds.Y + Math.Max(0f, (bounds.Height - size.Y) / 2f);
            DrawWindowText(sprite, value, new Vector2(x, y), color, scale);
        }

        private void NavigateSelection(int delta)
        {
            if (_snapshot.Entries.Count == 0)
            {
                return;
            }

            _pendingSelectionIndex = Math.Clamp(_pendingSelectionIndex + delta, 0, _snapshot.Entries.Count - 1);
        }

        private void ActivateSelection(int index)
        {
            if (index < 0 || index >= _snapshot.Entries.Count)
            {
                return;
            }

            ShowFeedback(_selectHandler?.Invoke(index));
        }

        private Texture2D ResolveItemIcon(int itemId)
        {
            if (itemId <= 0 || _itemIconProvider == null)
            {
                return null;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cached))
            {
                return cached;
            }

            Texture2D icon = _itemIconProvider(itemId);
            _itemIconCache[itemId] = icon;
            return icon;
        }

        private Rectangle GetSlotBounds(int index)
        {
            Rectangle iconBounds = GetClientIconBounds(Position, index);
            Rectangle buttonBounds = GetClientSelectButtonBounds(Position, index, _buttonSkin);
            int left = Math.Min(iconBounds.Left, buttonBounds.Left) - ClientSelectionHighlightPadding;
            int top = Math.Min(iconBounds.Top, buttonBounds.Top) - ClientSelectionHighlightPadding;
            int right = Math.Max(iconBounds.Right, buttonBounds.Right) + ClientSelectionHighlightPadding;
            int bottom = Math.Max(iconBounds.Bottom, buttonBounds.Bottom) + ClientSelectionHighlightPadding;
            return new Rectangle(left, top, right - left, bottom - top);
        }

        internal static Rectangle GetClientIconBounds(Point origin, int index)
        {
            return new Rectangle(
                origin.X + ClientIconLeft + (index * ClientColumnStride),
                origin.Y + ClientIconTop,
                ClientItemIconSize,
                ClientItemIconSize);
        }

        internal static Rectangle GetClientSelectButtonBounds(Point origin, int index, LogoutGiftButtonSkin skin = null)
        {
            Point buttonAnchor = GetClientSelectButtonAnchor(index);
            Point buttonSize = LogoutGiftButtonSkin.ResolveFrameSize(skin);
            return new Rectangle(
                origin.X + buttonAnchor.X,
                origin.Y + buttonAnchor.Y,
                buttonSize.X > 0 ? buttonSize.X : ClientSelectButtonWidth,
                buttonSize.Y > 0 ? buttonSize.Y : ClientSelectButtonHeight);
        }

        internal static Point GetClientSelectButtonAnchor(int index)
        {
            return new Point(
                ClientSelectButtonLeft + (index * ClientColumnStride),
                ClientSelectButtonTop);
        }

        internal static int GetClientSelectButtonId(int index)
        {
            return ClientSelectButtonBaseId + index;
        }

        private Rectangle GetCloseBounds()
        {
            Rectangle bounds = GetWindowBounds();
            return new Rectangle(bounds.Right - 22, bounds.Y + 5, CloseButtonSize, CloseButtonSize);
        }

        private int ClampSelectionIndex(int selectionIndex)
        {
            return _snapshot.Entries.Count == 0
                ? 0
                : Math.Clamp(selectionIndex, 0, _snapshot.Entries.Count - 1);
        }

        private void DrawMissingArtNotice(SpriteBatch sprite)
        {
            const float noteScale = 0.31f;
            Rectangle noteBounds = new(Position.X + 18, Position.Y + 96, DefaultWidth - 36, 62);
            foreach (string line in WrapText("Authored LogoutGift art is not present in the current UI export.", noteBounds.Width, noteScale))
            {
                Vector2 textSize = MeasureWindowText(null, line, noteScale);
                float x = noteBounds.X + Math.Max(0f, (noteBounds.Width - textSize.X) / 2f);
                float y = noteBounds.Y + Math.Max(0f, (noteBounds.Height - textSize.Y) / 2f);
                DrawWindowText(sprite, line, new Vector2(x, y), new Color(121, 94, 68), noteScale);
                noteBounds.Y += 14;
            }
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private bool Released(MouseState mouseState)
        {
            return mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
        }

        private Texture2D ResolveButtonTexture(bool selected, bool hovered, bool pressed)
        {
            return _buttonSkin?.ResolveTexture(selected, hovered, pressed);
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrWhiteSpace(text) || !CanDrawWindowText)
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate, scale).X > maxWidth)
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

        private static Texture2D CreateFrameTexture(GraphicsDevice device)
        {
            Texture2D texture = new(device ?? throw new ArgumentNullException(nameof(device)), DefaultWidth, DefaultHeight);
            Color[] pixels = new Color[DefaultWidth * DefaultHeight];
            texture.SetData(pixels);
            return texture;
        }

        private static Texture2D CreatePixelTexture(GraphicsDevice device)
        {
            Texture2D texture = new(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            texture.SetData(new[] { Color.White });
            return texture;
        }
    }

    internal sealed class LogoutGiftOwnerSnapshot
    {
        public string Title { get; init; } = "Logout Gift";
        public string Subtitle { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public int SelectedIndex { get; init; }
        public IReadOnlyList<LogoutGiftEntrySnapshot> Entries { get; init; } = Array.Empty<LogoutGiftEntrySnapshot>();
    }

    internal sealed class LogoutGiftEntrySnapshot
    {
        public int CommoditySerialNumber { get; init; }
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public long Price { get; init; }
        public int Count { get; init; }
        public bool OnSale { get; init; }
    }

    internal sealed class LogoutGiftButtonSkin
    {
        private const int DefaultSelectButtonWidth = 52;
        private const int DefaultSelectButtonHeight = 18;

        internal LogoutGiftButtonSkin(
            Texture2D normal,
            Texture2D hovered,
            Texture2D pressed,
            Texture2D disabled,
            Texture2D keyFocused)
        {
            Normal = normal;
            Hovered = hovered;
            Pressed = pressed;
            Disabled = disabled;
            KeyFocused = keyFocused;
            Size = ResolveFrameSize(this);
        }

        internal Texture2D Normal { get; }
        internal Texture2D Hovered { get; }
        internal Texture2D Pressed { get; }
        internal Texture2D Disabled { get; }
        internal Texture2D KeyFocused { get; }
        internal Point Size { get; }

        internal Texture2D ResolveTexture(bool selected, bool hovered, bool pressed)
        {
            if (pressed)
            {
                return Pressed ?? Hovered ?? KeyFocused ?? Normal;
            }

            if (hovered)
            {
                return Hovered ?? KeyFocused ?? Normal;
            }

            if (selected)
            {
                return KeyFocused ?? Hovered ?? Normal;
            }

            return Normal ?? Hovered ?? Pressed ?? Disabled ?? KeyFocused;
        }

        internal static Point ResolveFrameSize(LogoutGiftButtonSkin skin)
        {
            Texture2D texture = skin?.Normal
                ?? skin?.Hovered
                ?? skin?.Pressed
                ?? skin?.Disabled
                ?? skin?.KeyFocused;
            return texture == null
                ? new Point(DefaultSelectButtonWidth, DefaultSelectButtonHeight)
                : new Point(texture.Width, texture.Height);
        }
    }
}
