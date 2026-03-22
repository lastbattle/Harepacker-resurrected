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
    internal sealed class SocialSearchWindow : UIWindowBase
    {
        private static readonly SocialSearchTab[] TabOrder =
        {
            SocialSearchTab.Party,
            SocialSearchTab.PartyMember,
            SocialSearchTab.Expedition
        };

        private static readonly Point[] TabOrigins =
        {
            new(20, 59),
            new(88, 59),
            new(156, 59)
        };

        private readonly IDXObject _overlay;
        private readonly IDXObject _contentOverlay;
        private readonly Point _overlayOffset;
        private readonly Point _contentOverlayOffset;
        private readonly Texture2D[] _tabEnabledTextures;
        private readonly Texture2D[] _tabDisabledTextures;
        private readonly Texture2D _pixel;
        private readonly Dictionary<SocialSearchTab, HeaderLayer> _contentLayers = new();
        private readonly Dictionary<SocialSearchTab, Dictionary<string, UIObject>> _actionButtons = new();
        private readonly List<Rectangle> _entryBounds = new();

        private Func<SocialSearchSnapshot> _snapshotProvider;
        private Action<SocialSearchTab> _tabSelectionHandler;
        private Action<int> _entrySelectionHandler;
        private Action<bool> _levelFilterHandler;
        private Func<string, string> _actionHandler;
        private Action<string> _feedbackHandler;
        private UIObject _allLevelButton;
        private UIObject _similarLevelButton;
        private SpriteFont _font;
        private MouseState _previousMouseState;

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

        public SocialSearchWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            Texture2D[] tabEnabledTextures,
            Texture2D[] tabDisabledTextures,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _contentOverlay = contentOverlay;
            _contentOverlayOffset = contentOverlayOffset;
            _tabEnabledTextures = tabEnabledTextures ?? Array.Empty<Texture2D>();
            _tabDisabledTextures = tabDisabledTextures ?? Array.Empty<Texture2D>();
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.SocialSearch;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<SocialSearchSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            UpdateButtonStates(GetSnapshot());
        }

        internal void SetHandlers(
            Action<SocialSearchTab> tabSelectionHandler,
            Action<int> entrySelectionHandler,
            Action<bool> levelFilterHandler,
            Func<string, string> actionHandler,
            Action<string> feedbackHandler)
        {
            _tabSelectionHandler = tabSelectionHandler;
            _entrySelectionHandler = entrySelectionHandler;
            _levelFilterHandler = levelFilterHandler;
            _actionHandler = actionHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void RegisterContentLayer(SocialSearchTab tab, IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _contentLayers[tab] = new HeaderLayer(layer, offset);
            }
        }

        internal void RegisterActionButton(SocialSearchTab tab, string actionKey, UIObject button)
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

        internal void SetFilterButtons(UIObject allLevelButton, UIObject similarLevelButton)
        {
            _allLevelButton = allLevelButton;
            _similarLevelButton = similarLevelButton;
            ConfigureButton(_allLevelButton, () => _levelFilterHandler?.Invoke(false));
            ConfigureButton(_similarLevelButton, () => _levelFilterHandler?.Invoke(true));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            SocialSearchSnapshot snapshot = GetSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                if (TryHandleTabClick(mouseState.Position))
                {
                    _previousMouseState = mouseState;
                    return;
                }

                TryHandleEntryClick(mouseState.Position);
            }

            _previousMouseState = mouseState;
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
            DrawLayer(sprite, _contentOverlay, _contentOverlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            SocialSearchSnapshot snapshot = GetSnapshot();
            DrawTabStrip(sprite, snapshot);

            if (_contentLayers.TryGetValue(snapshot.CurrentTab, out HeaderLayer contentLayer))
            {
                DrawLayer(sprite, contentLayer.Layer, contentLayer.Offset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }

            if (_font == null)
            {
                return;
            }

            DrawEntryList(sprite, snapshot);
            DrawSummary(sprite, snapshot);
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

        private SocialSearchSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new SocialSearchSnapshot();
        }

        private void UpdateButtonStates(SocialSearchSnapshot snapshot)
        {
            foreach ((SocialSearchTab tab, Dictionary<string, UIObject> buttons) in _actionButtons)
            {
                bool tabVisible = tab == snapshot.CurrentTab;
                foreach ((string actionKey, UIObject button) in buttons)
                {
                    button.ButtonVisible = tabVisible;
                    button.SetEnabled(tabVisible && snapshot.EnabledActionKeys.Contains(actionKey));
                }
            }

            if (_allLevelButton != null)
            {
                _allLevelButton.ButtonVisible = true;
                _allLevelButton.SetButtonState(snapshot.SimilarLevelOnly ? UIObjectState.Normal : UIObjectState.Pressed);
            }

            if (_similarLevelButton != null)
            {
                _similarLevelButton.ButtonVisible = true;
                _similarLevelButton.SetButtonState(snapshot.SimilarLevelOnly ? UIObjectState.Pressed : UIObjectState.Normal);
            }
        }

        private void DrawTabStrip(SpriteBatch sprite, SocialSearchSnapshot snapshot)
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

        private void DrawEntryList(SpriteBatch sprite, SocialSearchSnapshot snapshot)
        {
            _entryBounds.Clear();
            Rectangle listBounds = new Rectangle(Position.X + 27, Position.Y + 123, 209, 168);
            sprite.Draw(_pixel, listBounds, new Color(17, 26, 39, 64));

            int rowHeight = 28;
            for (int i = 0; i < SearchPageRows; i++)
            {
                Rectangle rowBounds = new Rectangle(listBounds.X + 2, listBounds.Y + 2 + (i * rowHeight), listBounds.Width - 4, rowHeight - 2);
                _entryBounds.Add(rowBounds);

                bool selected = i == snapshot.SelectedIndex;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(113, 164, 222, 110) : new Color(255, 255, 255, i % 2 == 0 ? 24 : 14));
                if (i >= snapshot.Entries.Count)
                {
                    continue;
                }

                SocialSearchEntrySnapshot entry = snapshot.Entries[i];
                DrawText(sprite, entry.Title, rowBounds.X + 5, rowBounds.Y + 2, new Color(243, 246, 250), 0.4f);
                DrawText(sprite, entry.PrimaryText, rowBounds.X + 104, rowBounds.Y + 2, new Color(222, 228, 235), 0.36f);
                DrawText(sprite, entry.SecondaryText, rowBounds.X + 5, rowBounds.Y + 14, new Color(173, 181, 193), 0.34f);
                DrawText(sprite, entry.CapacityText, rowBounds.Right - 42, rowBounds.Y + 2, new Color(255, 231, 174), 0.33f);
                DrawText(sprite, $"CH {entry.Channel} {entry.LocationSummary}", rowBounds.X + 104, rowBounds.Y + 14, new Color(157, 165, 176), 0.32f);
            }
        }

        private void DrawSummary(SpriteBatch sprite, SocialSearchSnapshot snapshot)
        {
            Rectangle summaryBounds = new Rectangle(Position.X + 17, Position.Y + 314, 228, 43);
            sprite.Draw(_pixel, summaryBounds, new Color(6, 13, 20, 88));

            int y = summaryBounds.Y + 5;
            foreach (string line in snapshot.SummaryLines.Take(3))
            {
                DrawText(sprite, line, summaryBounds.X + 5, y, new Color(228, 233, 239), 0.34f);
                y += 12;
            }
        }

        private bool TryHandleTabClick(Point mousePosition)
        {
            for (int i = 0; i < TabOrder.Length; i++)
            {
                Rectangle bounds = GetTabBounds(i);
                if (!bounds.Contains(mousePosition))
                {
                    continue;
                }

                _tabSelectionHandler?.Invoke(TabOrder[i]);
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

        private Rectangle GetTabBounds(int index)
        {
            Texture2D enabledTexture = index < _tabEnabledTextures.Length ? _tabEnabledTextures[index] : null;
            Texture2D disabledTexture = index < _tabDisabledTextures.Length ? _tabDisabledTextures[index] : null;
            Texture2D referenceTexture = enabledTexture ?? disabledTexture;
            Point origin = TabOrigins[index];
            return new Rectangle(Position.X + origin.X, Position.Y + origin.Y, referenceTexture?.Width ?? 68, referenceTexture?.Height ?? 19);
        }

        private void ShowFeedback(string actionKey)
        {
            string message = _actionHandler?.Invoke(actionKey);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
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
                sprite.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private const int SearchPageRows = 6;
    }
}
