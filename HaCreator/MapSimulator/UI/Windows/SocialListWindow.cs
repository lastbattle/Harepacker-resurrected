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
    internal sealed class SocialListWindow : UIWindowBase
    {
        private static readonly SocialListTab[] TabOrder =
        {
            SocialListTab.Friend,
            SocialListTab.Party,
            SocialListTab.Guild,
            SocialListTab.Alliance,
            SocialListTab.Blacklist
        };

        private static readonly Point[] TabOrigins =
        {
            new(14, 28),
            new(45, 28),
            new(76, 28),
            new(117, 28),
            new(148, 28)
        };

        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly Dictionary<SocialListTab, HeaderLayer> _headerLayers = new();
        private readonly Texture2D[] _tabEnabledTextures;
        private readonly Texture2D[] _tabDisabledTextures;
        private readonly Texture2D _pixel;
        private readonly Dictionary<SocialListTab, Dictionary<string, UIObject>> _actionButtons = new();
        private readonly List<Rectangle> _entryBounds = new();
        private readonly VerticalScrollbarSkin _scrollbarSkin;

        private Func<SocialListSnapshot> _snapshotProvider;
        private Action<SocialListTab> _tabSelectionHandler;
        private Action<int> _entrySelectionHandler;
        private Action<int> _pageMoveHandler;
        private Action<int> _scrollMoveHandler;
        private Action<float> _scrollPositionHandler;
        private Action<bool> _friendFilterHandler;
        private Func<string, string> _actionHandler;
        private Action<string> _feedbackHandler;
        private UIObject _pagePrevButton;
        private UIObject _pageNextButton;
        private UIObject _showAllButton;
        private UIObject _showOnlineButton;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private int _previousScrollWheelValue;
        private bool _isDraggingScrollThumb;
        private int _scrollThumbDragOffsetY;

        private readonly struct HeaderLayer
        {
            public HeaderLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        public SocialListWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            Texture2D[] tabEnabledTextures,
            Texture2D[] tabDisabledTextures,
            VerticalScrollbarSkin scrollbarSkin,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _tabEnabledTextures = tabEnabledTextures ?? Array.Empty<Texture2D>();
            _tabDisabledTextures = tabDisabledTextures ?? Array.Empty<Texture2D>();
            _scrollbarSkin = scrollbarSkin;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.SocialList;

        internal void SetSnapshotProvider(Func<SocialListSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            UpdateButtonStates(GetSnapshot());
        }

        internal void SetHandlers(
            Action<SocialListTab> tabSelectionHandler,
            Action<int> entrySelectionHandler,
            Action<int> pageMoveHandler,
            Action<int> scrollMoveHandler,
            Action<float> scrollPositionHandler,
            Action<bool> friendFilterHandler,
            Func<string, string> actionHandler,
            Action<string> feedbackHandler)
        {
            _tabSelectionHandler = tabSelectionHandler;
            _entrySelectionHandler = entrySelectionHandler;
            _pageMoveHandler = pageMoveHandler;
            _scrollMoveHandler = scrollMoveHandler;
            _scrollPositionHandler = scrollPositionHandler;
            _friendFilterHandler = friendFilterHandler;
            _actionHandler = actionHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void ShowTab(SocialListTab tab)
        {
            _tabSelectionHandler?.Invoke(tab);
            UpdateButtonStates(GetSnapshot());
        }

        internal void RegisterHeaderLayer(SocialListTab tab, IDXObject layer, Point offset)
        {
            if (layer == null)
            {
                return;
            }

            _headerLayers[tab] = new HeaderLayer(layer, offset);
        }

        internal void RegisterActionButton(SocialListTab tab, string actionKey, UIObject button)
        {
            if (button == null || string.IsNullOrWhiteSpace(actionKey))
            {
                return;
            }

            if (!_actionButtons.TryGetValue(tab, out Dictionary<string, UIObject> buttons))
            {
                buttons = new Dictionary<string, UIObject>(StringComparer.OrdinalIgnoreCase);
                _actionButtons[tab] = buttons;
            }

            buttons[actionKey] = button;
            AddButton(button);
            button.ButtonClickReleased += _ => ShowFeedback(actionKey);
        }

        internal void SetPageButtons(UIObject pagePrevButton, UIObject pageNextButton)
        {
            _pagePrevButton = pagePrevButton;
            _pageNextButton = pageNextButton;
            ConfigureButton(_pagePrevButton, () => _pageMoveHandler?.Invoke(-1));
            ConfigureButton(_pageNextButton, () => _pageMoveHandler?.Invoke(1));
        }

        internal void SetFriendFilterButtons(UIObject showAllButton, UIObject showOnlineButton)
        {
            _showAllButton = showAllButton;
            _showOnlineButton = showOnlineButton;
            ConfigureButton(_showAllButton, () => _friendFilterHandler?.Invoke(false));
            ConfigureButton(_showOnlineButton, () => _friendFilterHandler?.Invoke(true));
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            SocialListSnapshot snapshot = GetSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            HandleScrollWheel(mouseState);
            HandleScrollDragging(mouseState, snapshot);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                if (TryHandleTabClick(mouseState.Position))
                {
                    _previousMouseState = mouseState;
                    return;
                }

                if (TryHandleScrollbarClick(mouseState.Position, snapshot))
                {
                    _previousMouseState = mouseState;
                    return;
                }

                TryHandleEntryClick(mouseState.Position);
            }

            _previousMouseState = mouseState;
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

            SocialListSnapshot snapshot = GetSnapshot();
            DrawTabStrip(sprite, snapshot);
            DrawHeader(sprite, snapshot, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            DrawEntries(sprite, snapshot);
            DrawSummaryPanel(sprite, snapshot);
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private void ShowFeedback(string actionKey)
        {
            string message = _actionHandler?.Invoke(actionKey);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private SocialListSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new SocialListSnapshot();
        }

        private void UpdateButtonStates(SocialListSnapshot snapshot)
        {
            foreach ((SocialListTab tab, Dictionary<string, UIObject> buttons) in _actionButtons)
            {
                bool tabVisible = tab == snapshot.CurrentTab;
                foreach ((string actionKey, UIObject button) in buttons)
                {
                    button.ButtonVisible = tabVisible;
                    button.SetEnabled(tabVisible && snapshot.EnabledActionKeys.Contains(actionKey));
                }
            }

            if (_pagePrevButton != null)
            {
                _pagePrevButton.ButtonVisible = true;
                _pagePrevButton.SetEnabled(snapshot.CanPageBackward);
            }

            if (_pageNextButton != null)
            {
                _pageNextButton.ButtonVisible = true;
                _pageNextButton.SetEnabled(snapshot.CanPageForward);
            }

            if (_showAllButton != null)
            {
                _showAllButton.ButtonVisible = snapshot.CurrentTab == SocialListTab.Friend;
                _showAllButton.SetEnabled(snapshot.CurrentTab == SocialListTab.Friend && snapshot.FriendOnlineOnly);
            }

            if (_showOnlineButton != null)
            {
                _showOnlineButton.ButtonVisible = snapshot.CurrentTab == SocialListTab.Friend;
                _showOnlineButton.SetEnabled(snapshot.CurrentTab == SocialListTab.Friend && !snapshot.FriendOnlineOnly);
            }
        }

        private void DrawTabStrip(SpriteBatch sprite, SocialListSnapshot snapshot)
        {
            for (int i = 0; i < TabOrder.Length; i++)
            {
                Texture2D texture = TabOrder[i] == snapshot.CurrentTab
                    ? (i < _tabEnabledTextures.Length ? _tabEnabledTextures[i] : null)
                    : (i < _tabDisabledTextures.Length ? _tabDisabledTextures[i] : null);
                if (texture == null)
                {
                    continue;
                }

                Point origin = TabOrigins[i];
                sprite.Draw(texture, new Vector2(Position.X + origin.X, Position.Y + origin.Y), Color.White);
            }
        }

        private void DrawHeader(
            SpriteBatch sprite,
            SocialListSnapshot snapshot,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            if (_headerLayers.TryGetValue(snapshot.CurrentTab, out HeaderLayer header))
            {
                DrawLayer(sprite, header.Layer, header.Offset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }
        }

        private void DrawEntries(SpriteBatch sprite, SocialListSnapshot snapshot)
        {
            _entryBounds.Clear();

            Rectangle listBounds = new Rectangle(Position.X + 14, Position.Y + 76, 236, 190);
            listBounds = GetListBounds();
            sprite.Draw(_pixel, listBounds, new Color(10, 18, 28, 56));

            int rowHeight = 22;
            for (int i = 0; i < 8; i++)
            {
                Rectangle rowBounds = new Rectangle(listBounds.X + 2, listBounds.Y + 2 + (i * rowHeight), listBounds.Width - 4, rowHeight - 1);
                _entryBounds.Add(rowBounds);

                bool selected = i == snapshot.SelectedVisibleIndex;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(110, 164, 220, 120) : new Color(255, 255, 255, i % 2 == 0 ? 28 : 16));

                if (i >= snapshot.Entries.Count)
                {
                    continue;
                }

                SocialListEntrySnapshot entry = snapshot.Entries[i];
                Color indicatorColor = entry.IsOnline ? new Color(88, 186, 112) : new Color(132, 140, 149);
                sprite.Draw(_pixel, new Rectangle(rowBounds.X + 4, rowBounds.Y + 7, 7, 7), indicatorColor);

                Color nameColor = entry.IsLocalPlayer ? new Color(255, 240, 188) : new Color(248, 250, 253);
                DrawText(sprite, entry.Name, rowBounds.X + 16, rowBounds.Y + 2, nameColor, 0.45f);
                DrawText(sprite, entry.PrimaryText, rowBounds.X + 98, rowBounds.Y + 2, new Color(197, 209, 220), 0.41f);

                string rightText = entry.IsLeader ? "Leader" : $"CH {entry.Channel}";
                Vector2 rightSize = _font.MeasureString(rightText) * 0.38f;
                DrawText(sprite, rightText, rowBounds.Right - (int)rightSize.X - 6, rowBounds.Y + 3, new Color(175, 226, 188), 0.38f);
                DrawText(sprite, entry.LocationSummary, rowBounds.X + 16, rowBounds.Y + 11, new Color(164, 172, 183), 0.36f);
                DrawText(sprite, entry.SecondaryText, rowBounds.X + 98, rowBounds.Y + 11, new Color(164, 172, 183), 0.36f);
            }

            DrawText(sprite, snapshot.HeaderTitle, listBounds.X + 2, listBounds.Y - 16, new Color(81, 59, 28), 0.5f);
            string pageText = $"{snapshot.Page}/{snapshot.TotalPages}  {snapshot.TotalEntries} entries";
            DrawText(sprite, pageText, listBounds.Right - 70, listBounds.Bottom + 6, new Color(109, 118, 132), 0.35f);

            DrawScrollbar(sprite, snapshot);
        }

        private void DrawSummaryPanel(SpriteBatch sprite, SocialListSnapshot snapshot)
        {
            Rectangle summaryBounds = new Rectangle(Position.X + 14, Position.Y + 285, 236, 60);
            sprite.Draw(_pixel, summaryBounds, new Color(5, 10, 18, 96));

            int y = summaryBounds.Y + 6;
            foreach (string line in snapshot.SummaryLines)
            {
                DrawText(sprite, line, summaryBounds.X + 6, y, new Color(225, 229, 236), 0.38f);
                y += 14;
            }
        }

        private bool TryHandleTabClick(Point mousePosition)
        {
            for (int i = 0; i < TabOrder.Length; i++)
            {
                SocialListTab tab = TabOrder[i];
                Rectangle bounds = GetTabBounds(i);
                if (!bounds.Contains(mousePosition))
                {
                    continue;
                }

                _tabSelectionHandler?.Invoke(tab);
                return true;
            }

            return false;
        }

        private void TryHandleEntryClick(Point mousePosition)
        {
            for (int i = 0; i < _entryBounds.Count; i++)
            {
                if (_entryBounds[i].Contains(mousePosition))
                {
                    _entrySelectionHandler?.Invoke(i);
                    return;
                }
            }
        }

        private void HandleScrollWheel(MouseState mouseState)
        {
            if (_scrollbarSkin == null || !_scrollbarSkin.IsReady)
            {
                return;
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            if (wheelDelta == 0)
            {
                return;
            }

            Rectangle interactionBounds = Rectangle.Union(GetListBounds(), GetScrollbarBounds());
            if (!interactionBounds.Contains(mouseState.Position))
            {
                return;
            }

            int scrollRows = Math.Clamp(wheelDelta / 120, -3, 3);
            if (scrollRows != 0)
            {
                _scrollMoveHandler?.Invoke(-scrollRows);
            }
        }

        private void HandleScrollDragging(MouseState mouseState, SocialListSnapshot snapshot)
        {
            if (_scrollbarSkin == null || !_scrollbarSkin.IsReady || snapshot.MaxFirstVisibleIndex <= 0)
            {
                _isDraggingScrollThumb = false;
                return;
            }

            Rectangle thumbBounds = GetScrollbarThumbBounds(snapshot);
            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && _previousMouseState.LeftButton == ButtonState.Released;

            if (leftJustPressed && thumbBounds.Contains(mouseState.Position))
            {
                _isDraggingScrollThumb = true;
                _scrollThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
            }

            if (!_isDraggingScrollThumb)
            {
                return;
            }

            if (!leftPressed)
            {
                _isDraggingScrollThumb = false;
                return;
            }

            Rectangle trackBounds = GetScrollbarTrackBounds();
            int thumbTravel = Math.Max(0, trackBounds.Height - _scrollbarSkin.ThumbHeight);
            if (thumbTravel <= 0)
            {
                _scrollPositionHandler?.Invoke(0f);
                return;
            }

            int thumbTop = Math.Clamp(mouseState.Y - _scrollThumbDragOffsetY, trackBounds.Y, trackBounds.Bottom - _scrollbarSkin.ThumbHeight);
            float ratio = (thumbTop - trackBounds.Y) / (float)thumbTravel;
            _scrollPositionHandler?.Invoke(ratio);
        }

        private bool TryHandleScrollbarClick(Point mousePosition, SocialListSnapshot snapshot)
        {
            if (_scrollbarSkin == null || !_scrollbarSkin.IsReady)
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
                _scrollMoveHandler?.Invoke(-1);
                return true;
            }

            if (nextBounds.Contains(mousePosition))
            {
                _scrollMoveHandler?.Invoke(1);
                return true;
            }

            if (thumbBounds.Contains(mousePosition))
            {
                return true;
            }

            if (trackBounds.Contains(mousePosition))
            {
                _pageMoveHandler?.Invoke(mousePosition.Y < thumbBounds.Y ? -1 : 1);
                return true;
            }

            return false;
        }

        private void DrawScrollbar(SpriteBatch sprite, SocialListSnapshot snapshot)
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
            DrawScrollbarArrow(sprite, prevBounds, _scrollbarSkin.PrevStates, _scrollbarSkin.PrevDisabled, snapshot.CanPageBackward);
            DrawScrollbarArrow(sprite, nextBounds, _scrollbarSkin.NextStates, _scrollbarSkin.NextDisabled, snapshot.CanPageForward);

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
            return new Rectangle(Position.X + 14, Position.Y + 76, 221, 190);
        }

        private Rectangle GetScrollbarBounds()
        {
            int width = _scrollbarSkin?.Width ?? 11;
            return new Rectangle(Position.X + 239, Position.Y + 78, width, 186);
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

        private Rectangle GetScrollbarThumbBounds(SocialListSnapshot snapshot)
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

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            if (layer == null)
            {
                return;
            }

            layer.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private Rectangle GetTabBounds(int index)
        {
            Texture2D enabledTexture = index < _tabEnabledTextures.Length ? _tabEnabledTextures[index] : null;
            Texture2D disabledTexture = index < _tabDisabledTextures.Length ? _tabDisabledTextures[index] : null;
            Texture2D referenceTexture = enabledTexture ?? disabledTexture;
            Point origin = TabOrigins[index];
            return new Rectangle(
                Position.X + origin.X,
                Position.Y + origin.Y,
                referenceTexture?.Width ?? 30,
                referenceTexture?.Height ?? 18);
        }
    }

    internal sealed class VerticalScrollbarSkin
    {
        public Texture2D[] PrevStates { get; init; } = Array.Empty<Texture2D>();
        public Texture2D[] NextStates { get; init; } = Array.Empty<Texture2D>();
        public Texture2D[] ThumbStates { get; init; } = Array.Empty<Texture2D>();
        public Texture2D PrevDisabled { get; init; }
        public Texture2D NextDisabled { get; init; }
        public Texture2D Base { get; init; }

        public bool IsReady =>
            Base != null &&
            PrevStates.FirstOrDefault() != null &&
            NextStates.FirstOrDefault() != null &&
            ThumbStates.FirstOrDefault() != null;
        public int Width => Base?.Width ?? PrevStates.FirstOrDefault()?.Width ?? 11;
        public int PrevHeight => PrevStates.FirstOrDefault()?.Height ?? PrevDisabled?.Height ?? 12;
        public int NextHeight => NextStates.FirstOrDefault()?.Height ?? NextDisabled?.Height ?? 12;
        public int ThumbHeight => ThumbStates.FirstOrDefault()?.Height ?? 26;
    }
}
