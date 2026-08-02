using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class FriendGroupWindow : UIWindowBase
    {
        private const int PageSize = 7;

        private readonly IDXObject _overlay;
        private readonly IDXObject _baseLayer;
        private readonly IDXObject _addFriendPopupLayer;
        private readonly IDXObject _groupWhisperPopupLayer;
        private readonly IDXObject _groupDeletePopupLayer;
        private readonly IDXObject _groupDeleteDenyPopupLayer;
        private readonly IDXObject _needMessagePopupLayer;
        private readonly Point _overlayOffset;
        private readonly Point _baseOffset;
        private readonly Point _addFriendPopupOffset;
        private readonly Point _groupWhisperPopupOffset;
        private readonly Point _groupDeletePopupOffset;
        private readonly Point _groupDeleteDenyPopupOffset;
        private readonly Point _needMessagePopupOffset;
        private readonly Texture2D _pixel;
        private readonly UIObject _okButton;
        private readonly UIObject _cancelButton;
        private readonly List<Rectangle> _entryBounds = new();
        private readonly VerticalScrollbarSkin _scrollbarSkin;

        private Func<FriendGroupPopupSnapshot> _snapshotProvider;
        private Action<int> _toggleEntryHandler;
        private Action<int> _scrollHandler;
        private Action<float> _scrollPositionHandler;
        private Action<char> _appendInputHandler;
        private Action _backspaceInputHandler;
        private Func<string> _confirmHandler;
        private Func<string> _cancelHandler;
        private Action<string> _feedbackHandler;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private int _previousScrollWheelValue;
        private bool _isDraggingScrollThumb;
        private int _scrollThumbDragOffsetY;
        private Keys _lastHeldKey = Keys.None;
        private int _keyHoldStartTime;
        private int _lastKeyRepeatTime;
        private FriendGroupPopupSnapshot _currentSnapshot = new();

        public FriendGroupWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject baseLayer,
            Point baseOffset,
            IDXObject addFriendPopupLayer,
            Point addFriendPopupOffset,
            IDXObject groupWhisperPopupLayer,
            Point groupWhisperPopupOffset,
            IDXObject groupDeletePopupLayer,
            Point groupDeletePopupOffset,
            IDXObject groupDeleteDenyPopupLayer,
            Point groupDeleteDenyPopupOffset,
            IDXObject needMessagePopupLayer,
            Point needMessagePopupOffset,
            UIObject okButton,
            UIObject cancelButton,
            VerticalScrollbarSkin scrollbarSkin,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _baseLayer = baseLayer;
            _addFriendPopupLayer = addFriendPopupLayer;
            _groupWhisperPopupLayer = groupWhisperPopupLayer;
            _groupDeletePopupLayer = groupDeletePopupLayer;
            _groupDeleteDenyPopupLayer = groupDeleteDenyPopupLayer;
            _needMessagePopupLayer = needMessagePopupLayer;
            _overlayOffset = overlayOffset;
            _baseOffset = baseOffset;
            _addFriendPopupOffset = addFriendPopupOffset;
            _groupWhisperPopupOffset = groupWhisperPopupOffset;
            _groupDeletePopupOffset = groupDeletePopupOffset;
            _groupDeleteDenyPopupOffset = groupDeleteDenyPopupOffset;
            _needMessagePopupOffset = needMessagePopupOffset;
            _okButton = okButton;
            _cancelButton = cancelButton;
            _scrollbarSkin = scrollbarSkin;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            if (_okButton != null)
            {
                AddButton(_okButton);
                _okButton.ButtonClickReleased += _ => ShowFeedback(_confirmHandler?.Invoke());
            }

            if (_cancelButton != null)
            {
                AddButton(_cancelButton);
                _cancelButton.ButtonClickReleased += _ => ShowFeedback(_cancelHandler?.Invoke());
            }
        }

        public override string WindowName => MapSimulatorWindowNames.FriendGroup;

        public override bool CapturesKeyboardInput => _currentSnapshot?.ShowTextInput == true;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        internal void SetSnapshotProvider(Func<FriendGroupPopupSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            UpdateButtonStates(RefreshSnapshot());
        }

        internal void SetHandlers(
            Action<int> toggleEntryHandler,
            Action<int> scrollHandler,
            Action<float> scrollPositionHandler,
            Action<char> appendInputHandler,
            Action backspaceInputHandler,
            Func<string> confirmHandler,
            Func<string> cancelHandler,
            Action<string> feedbackHandler)
        {
            _toggleEntryHandler = toggleEntryHandler;
            _scrollHandler = scrollHandler;
            _scrollPositionHandler = scrollPositionHandler;
            _appendInputHandler = appendInputHandler;
            _backspaceInputHandler = backspaceInputHandler;
            _confirmHandler = confirmHandler;
            _cancelHandler = cancelHandler;
            _feedbackHandler = feedbackHandler;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            FriendGroupPopupSnapshot snapshot = RefreshSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            KeyboardState keyboardState = Keyboard.GetState();
            HandleScrollWheel(mouseState);
            HandleScrollDragging(mouseState, snapshot);
            HandleKeyboardInput(keyboardState);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                if (TryHandleScrollbarClick(mouseState.Position, snapshot))
                {
                    _previousMouseState = mouseState;
                    _previousKeyboardState = keyboardState;
                    _previousScrollWheelValue = mouseState.ScrollWheelValue;
                    return;
                }

                for (int i = 0; i < _entryBounds.Count; i++)
                {
                    if (_entryBounds[i].Contains(mouseState.Position))
                    {
                        _toggleEntryHandler?.Invoke(i);
                        break;
                    }
                }
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
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
            DrawLayer(sprite, _overlay, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _baseLayer, _baseOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawPopupLayer(sprite, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            FriendGroupPopupSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            if (snapshot.ShowEntryList)
            {
                DrawEntryList(sprite, snapshot);
                DrawScrollbar(sprite, snapshot);
            }

            if (snapshot.ShowTextInput)
            {
                DrawInputField(sprite, snapshot);
            }

            DrawSummary(sprite, snapshot);
        }

        private FriendGroupPopupSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new FriendGroupPopupSnapshot();
            return _currentSnapshot;
        }

        private void UpdateButtonStates(FriendGroupPopupSnapshot snapshot)
        {
            _okButton?.SetEnabled(snapshot.CanConfirm);
            if (_okButton != null)
            {
                _okButton.ButtonVisible = snapshot.Mode != FriendGroupPopupMode.DeleteGroupDeny;
            }

            _cancelButton?.SetEnabled(true);
        }

        private void HandleScrollWheel(MouseState mouseState)
        {
            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            if (wheelDelta == 0 || !ContainsPoint(mouseState.X, mouseState.Y))
            {
                return;
            }

            int scrollRows = Math.Clamp(wheelDelta / 120, -3, 3);
            if (scrollRows != 0)
            {
                _scrollHandler?.Invoke(-scrollRows);
            }
        }

        private void HandleScrollDragging(MouseState mouseState, FriendGroupPopupSnapshot snapshot)
        {
            if (_scrollbarSkin == null || !_scrollbarSkin.IsReady || snapshot.MaxFirstVisibleIndex <= 0)
            {
                _isDraggingScrollThumb = false;
                return;
            }

            Rectangle thumbBounds = GetScrollbarThumbBounds(snapshot);
            if (mouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released &&
                thumbBounds.Contains(mouseState.Position))
            {
                _isDraggingScrollThumb = true;
                _scrollThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
            }

            if (mouseState.LeftButton == ButtonState.Released)
            {
                _isDraggingScrollThumb = false;
            }

            if (!_isDraggingScrollThumb)
            {
                return;
            }

            Rectangle trackBounds = GetScrollbarTrackBounds();
            int thumbTravel = Math.Max(1, trackBounds.Height - thumbBounds.Height);
            int desiredY = Math.Clamp(mouseState.Y - _scrollThumbDragOffsetY, trackBounds.Y, trackBounds.Bottom - thumbBounds.Height);
            float ratio = (desiredY - trackBounds.Y) / (float)thumbTravel;
            _scrollPositionHandler?.Invoke(ratio);
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                ShowFeedback(_confirmHandler?.Invoke());
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                ShowFeedback(_cancelHandler?.Invoke());
                return;
            }

            FriendGroupPopupSnapshot snapshot = _currentSnapshot;
            if (snapshot?.ShowTextInput != true)
            {
                _lastHeldKey = Keys.None;
                return;
            }

            int tickCount = Environment.TickCount;
            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (_previousKeyboardState.IsKeyDown(key) || KeyboardTextInputHelper.IsControlKey(key))
                {
                    continue;
                }

                if (key == Keys.Back)
                {
                    _backspaceInputHandler?.Invoke();
                    _lastHeldKey = key;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                    return;
                }

                char? character = KeyboardTextInputHelper.KeyToChar(key, shift);
                if (!character.HasValue)
                {
                    continue;
                }

                _appendInputHandler?.Invoke(character.Value);
                _lastHeldKey = key;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
                return;
            }

            if (_lastHeldKey != Keys.None && !keyboardState.IsKeyDown(_lastHeldKey))
            {
                _lastHeldKey = Keys.None;
                return;
            }

            if (_lastHeldKey == Keys.Back
                && KeyboardTextInputHelper.ShouldRepeatKey(_lastHeldKey, keyboardState, _keyHoldStartTime, _lastKeyRepeatTime, tickCount))
            {
                _backspaceInputHandler?.Invoke();
                _lastKeyRepeatTime = tickCount;
                return;
            }

            if (_lastHeldKey != Keys.None
                && _lastHeldKey != Keys.Back
                && KeyboardTextInputHelper.ShouldRepeatKey(_lastHeldKey, keyboardState, _keyHoldStartTime, _lastKeyRepeatTime, tickCount))
            {
                char? repeatedCharacter = KeyboardTextInputHelper.KeyToChar(_lastHeldKey, shift);
                if (repeatedCharacter.HasValue)
                {
                    _appendInputHandler?.Invoke(repeatedCharacter.Value);
                    _lastKeyRepeatTime = tickCount;
                }
            }
        }

        private void DrawPopupLayer(
            SpriteBatch sprite,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            switch (_currentSnapshot?.Mode ?? FriendGroupPopupMode.AddFriend)
            {
                case FriendGroupPopupMode.GroupWhisper:
                    DrawLayer(sprite, _groupWhisperPopupLayer, _groupWhisperPopupOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                    break;
                case FriendGroupPopupMode.DeleteGroup:
                    DrawLayer(sprite, _groupDeletePopupLayer, _groupDeletePopupOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                    break;
                case FriendGroupPopupMode.DeleteGroupDeny:
                    DrawLayer(sprite, _groupDeleteDenyPopupLayer, _groupDeleteDenyPopupOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                    break;
                case FriendGroupPopupMode.NeedMessage:
                    DrawLayer(sprite, _needMessagePopupLayer, _needMessagePopupOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                    break;
                default:
                    DrawLayer(sprite, _addFriendPopupLayer, _addFriendPopupOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
                    break;
            }
        }

        private void DrawEntryList(SpriteBatch sprite, FriendGroupPopupSnapshot snapshot)
        {
            _entryBounds.Clear();
            Rectangle listBounds = GetListBounds();
            sprite.Draw(_pixel, listBounds, new Color(18, 25, 38, 42));

            for (int i = 0; i < PageSize; i++)
            {
                Rectangle rowBounds = new Rectangle(listBounds.X + 2, listBounds.Y + 2 + (i * 22), listBounds.Width - 4, 20);
                _entryBounds.Add(rowBounds);

                bool selected = i == snapshot.SelectedVisibleIndex;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(111, 163, 219, 90) : new Color(255, 255, 255, i % 2 == 0 ? 18 : 10));
                if (i >= snapshot.Entries.Count)
                {
                    continue;
                }

                FriendGroupPopupEntrySnapshot entry = snapshot.Entries[i];
                Rectangle checkboxBounds = new Rectangle(rowBounds.X + 5, rowBounds.Y + 4, 11, 11);
                sprite.Draw(_pixel, checkboxBounds, new Color(235, 238, 243, 196));
                if (entry.IsChecked)
                {
                    sprite.Draw(_pixel, new Rectangle(checkboxBounds.X + 2, checkboxBounds.Y + 2, 7, 7), entry.IsAnchor ? new Color(255, 214, 122) : new Color(76, 176, 102));
                }

                DrawText(sprite, entry.Name, rowBounds.X + 22, rowBounds.Y + 1, entry.IsOnline ? new Color(243, 246, 251) : new Color(165, 171, 180), 0.36f);
                DrawText(sprite, entry.GroupName, rowBounds.X + 122, rowBounds.Y + 1, new Color(177, 184, 196), 0.33f);
                DrawText(sprite, entry.IsAnchor ? "Anchor" : string.Empty, rowBounds.Right - 30, rowBounds.Y + 2, new Color(255, 225, 158), 0.28f);
            }
        }

        private bool TryHandleScrollbarClick(Point mousePosition, FriendGroupPopupSnapshot snapshot)
        {
            if (_scrollbarSkin == null || !_scrollbarSkin.IsReady || !snapshot.ShowEntryList)
            {
                return false;
            }

            Rectangle scrollbarBounds = GetScrollbarBounds();
            if (!scrollbarBounds.Contains(mousePosition))
            {
                return false;
            }

            Rectangle prevBounds = GetScrollbarPrevBounds();
            Rectangle nextBounds = GetScrollbarNextBounds();
            Rectangle thumbBounds = GetScrollbarThumbBounds(snapshot);
            Rectangle trackBounds = GetScrollbarTrackBounds();

            if (prevBounds.Contains(mousePosition))
            {
                _scrollHandler?.Invoke(-1);
                return true;
            }

            if (nextBounds.Contains(mousePosition))
            {
                _scrollHandler?.Invoke(1);
                return true;
            }

            if (thumbBounds.Contains(mousePosition))
            {
                return true;
            }

            if (trackBounds.Contains(mousePosition))
            {
                _scrollHandler?.Invoke(mousePosition.Y < thumbBounds.Y ? -PageSize : PageSize);
                return true;
            }

            return false;
        }

        private void DrawScrollbar(SpriteBatch sprite, FriendGroupPopupSnapshot snapshot)
        {
            if (_scrollbarSkin == null || !_scrollbarSkin.IsReady)
            {
                return;
            }

            Rectangle prevBounds = GetScrollbarPrevBounds();
            Rectangle nextBounds = GetScrollbarNextBounds();
            Rectangle trackBounds = GetScrollbarTrackBounds();
            bool canScroll = snapshot.MaxFirstVisibleIndex > 0;

            DrawScrollbarTrack(sprite, trackBounds);
            DrawScrollbarArrow(sprite, prevBounds, _scrollbarSkin.PrevStates, _scrollbarSkin.PrevDisabled, snapshot.FirstVisibleIndex > 0);
            DrawScrollbarArrow(sprite, nextBounds, _scrollbarSkin.NextStates, _scrollbarSkin.NextDisabled, snapshot.FirstVisibleIndex < snapshot.MaxFirstVisibleIndex);

            if (!canScroll)
            {
                return;
            }

            Rectangle thumbBounds = GetScrollbarThumbBounds(snapshot);
            Texture2D thumbTexture = ResolveScrollbarStateTexture(_scrollbarSkin.ThumbStates, true, thumbBounds.Contains(Mouse.GetState().Position), _isDraggingScrollThumb);
            if (thumbTexture != null)
            {
                sprite.Draw(thumbTexture, thumbBounds, Color.White);
            }
        }

        private void DrawScrollbarTrack(SpriteBatch sprite, Rectangle trackBounds)
        {
            if (_scrollbarSkin.Base == null)
            {
                sprite.Draw(_pixel, trackBounds, new Color(22, 30, 43, 64));
                return;
            }

            int tileY = trackBounds.Y;
            while (tileY < trackBounds.Bottom)
            {
                int tileHeight = Math.Min(_scrollbarSkin.Base.Height, trackBounds.Bottom - tileY);
                Rectangle destination = new Rectangle(trackBounds.X, tileY, _scrollbarSkin.Base.Width, tileHeight);
                Rectangle? source = tileHeight == _scrollbarSkin.Base.Height
                    ? null
                    : new Rectangle(0, 0, _scrollbarSkin.Base.Width, tileHeight);
                sprite.Draw(_scrollbarSkin.Base, destination, source, Color.White);
                tileY += tileHeight;
            }
        }

        private void DrawScrollbarArrow(SpriteBatch sprite, Rectangle bounds, Texture2D[] states, Texture2D disabledTexture, bool enabled)
        {
            Texture2D texture = enabled
                ? ResolveScrollbarStateTexture(states, true, bounds.Contains(Mouse.GetState().Position), false)
                : disabledTexture;
            if (texture != null)
            {
                sprite.Draw(texture, bounds, Color.White);
            }
        }

        private Texture2D ResolveScrollbarStateTexture(Texture2D[] states, bool enabled, bool hovered, bool pressed)
        {
            if (!enabled)
            {
                return null;
            }

            if (pressed && states.Length > 2 && states[2] != null)
            {
                return states[2];
            }

            if (hovered && states.Length > 1 && states[1] != null)
            {
                return states[1];
            }

            return states.Length > 0 ? states[0] : null;
        }

        private Rectangle GetListBounds()
        {
            return new Rectangle(Position.X + 17, Position.Y + 132, 224, 157);
        }

        private Rectangle GetScrollbarBounds()
        {
            int width = _scrollbarSkin?.Width ?? 11;
            Rectangle listBounds = GetListBounds();
            return new Rectangle(listBounds.Right + 3, listBounds.Y, width, listBounds.Height);
        }

        private Rectangle GetScrollbarPrevBounds()
        {
            Rectangle bounds = GetScrollbarBounds();
            return new Rectangle(bounds.X, bounds.Y, bounds.Width, _scrollbarSkin?.PrevHeight ?? 12);
        }

        private Rectangle GetScrollbarNextBounds()
        {
            Rectangle bounds = GetScrollbarBounds();
            int height = _scrollbarSkin?.NextHeight ?? 12;
            return new Rectangle(bounds.X, bounds.Bottom - height, bounds.Width, height);
        }

        private Rectangle GetScrollbarTrackBounds()
        {
            Rectangle bounds = GetScrollbarBounds();
            Rectangle prevBounds = GetScrollbarPrevBounds();
            Rectangle nextBounds = GetScrollbarNextBounds();
            return new Rectangle(bounds.X, prevBounds.Bottom, bounds.Width, Math.Max(0, nextBounds.Y - prevBounds.Bottom));
        }

        private Rectangle GetScrollbarThumbBounds(FriendGroupPopupSnapshot snapshot)
        {
            Rectangle trackBounds = GetScrollbarTrackBounds();
            int thumbHeight = _scrollbarSkin?.ThumbHeight ?? 26;
            if (snapshot.MaxFirstVisibleIndex <= 0)
            {
                return new Rectangle(trackBounds.X, trackBounds.Y, trackBounds.Width, thumbHeight);
            }

            int thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
            float ratio = snapshot.FirstVisibleIndex / (float)snapshot.MaxFirstVisibleIndex;
            int y = trackBounds.Y + (int)Math.Round(thumbTravel * Math.Clamp(ratio, 0f, 1f));
            return new Rectangle(trackBounds.X, y, trackBounds.Width, thumbHeight);
        }

        private void DrawSummary(SpriteBatch sprite, FriendGroupPopupSnapshot snapshot)
        {
            Rectangle summaryBounds = new Rectangle(Position.X + 14, Position.Y + 300, 234, 38);
            sprite.Draw(_pixel, summaryBounds, new Color(5, 11, 18, 88));

            int y = summaryBounds.Y + 5;
            foreach (string line in snapshot.SummaryLines.Take(3))
            {
                DrawText(sprite, line, summaryBounds.X + 6, y, new Color(228, 233, 239), 0.31f);
                y += 11;
            }
        }

        private void DrawInputField(SpriteBatch sprite, FriendGroupPopupSnapshot snapshot)
        {
            Rectangle inputBounds = new Rectangle(Position.X + 13, Position.Y + 67, 239, 12);
            sprite.Draw(_pixel, inputBounds, new Color(255, 255, 255, 22));

            string text = snapshot.InputText ?? string.Empty;
            DrawText(sprite, text, inputBounds.X + 3, inputBounds.Y - 1, new Color(28, 28, 28), 0.34f);

            bool drawCaret = ((Environment.TickCount / 500) & 1) == 0;
            if (!drawCaret)
            {
                return;
            }

            Vector2 measured = _font?.MeasureString(text) ?? Vector2.Zero;
            int caretX = inputBounds.X + 3 + (int)Math.Round(measured.X * 0.34f);
            Rectangle caretBounds = new Rectangle(
                Math.Min(inputBounds.Right - 1, caretX),
                inputBounds.Y + 1,
                1,
                Math.Max(8, inputBounds.Height - 2));
            sprite.Draw(_pixel, caretBounds, new Color(32, 32, 32, 220));
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            layer?.DrawBackground(sprite, skeletonMeshRenderer, gameTime, Position.X + offset.X, Position.Y + offset.Y, Color.White, false, drawReflectionInfo);
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color, float scale)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                ClientTextDrawing.Draw(sprite, text, new Vector2(x, y), color, scale, _font);
            }
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }
    }
}
