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
    internal sealed class GuildSearchWindow : UIWindowBase
    {
        private readonly IDXObject _overlay;
        private readonly IDXObject _contentOverlay;
        private readonly Point _overlayOffset;
        private readonly Point _contentOverlayOffset;
        private readonly Texture2D _pixel;
        private readonly List<Rectangle> _entryBounds = new();
        private readonly Dictionary<string, UIObject> _buttons = new(StringComparer.OrdinalIgnoreCase);

        private Func<GuildSearchSnapshot> _snapshotProvider;
        private Action<int> _entrySelectionHandler;
        private Action<int> _pageMoveHandler;
        private Func<string, string> _actionHandler;
        private Action<string> _feedbackHandler;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private GuildSearchSnapshot _currentSnapshot = new();

        public GuildSearchWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _contentOverlay = contentOverlay;
            _contentOverlayOffset = contentOverlayOffset;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.GuildSearch;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<GuildSearchSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            UpdateButtonStates(RefreshSnapshot());
        }

        internal void SetHandlers(
            Action<int> entrySelectionHandler,
            Action<int> pageMoveHandler,
            Func<string, string> actionHandler,
            Action<string> feedbackHandler)
        {
            _entrySelectionHandler = entrySelectionHandler;
            _pageMoveHandler = pageMoveHandler;
            _actionHandler = actionHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void RegisterActionButton(string actionKey, UIObject button)
        {
            if (button == null || string.IsNullOrWhiteSpace(actionKey))
            {
                return;
            }

            _buttons[actionKey] = button;
            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                string message = _actionHandler?.Invoke(actionKey);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _feedbackHandler?.Invoke(message);
                }
            };
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GuildSearchSnapshot snapshot = RefreshSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                for (int i = 0; i < _entryBounds.Count; i++)
                {
                    if (_entryBounds[i].Contains(mouseState.Position))
                    {
                        _entrySelectionHandler?.Invoke(i);
                        break;
                    }
                }
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

            if (_font == null)
            {
                return;
            }

            GuildSearchSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            DrawEntryList(sprite, snapshot);
            DrawSummary(sprite, snapshot);
        }

        private GuildSearchSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new GuildSearchSnapshot();
            return _currentSnapshot;
        }

        private void UpdateButtonStates(GuildSearchSnapshot snapshot)
        {
            foreach ((string actionKey, UIObject button) in _buttons)
            {
                button.ButtonVisible = true;
                button.SetEnabled(snapshot.EnabledActionKeys.Contains(actionKey));
            }

            if (_buttons.TryGetValue("GuildSearch.PagePrev", out UIObject prevButton))
            {
                prevButton.SetEnabled(snapshot.CanPageBackward);
            }

            if (_buttons.TryGetValue("GuildSearch.PageNext", out UIObject nextButton))
            {
                nextButton.SetEnabled(snapshot.CanPageForward);
            }
        }

        private void DrawEntryList(SpriteBatch sprite, GuildSearchSnapshot snapshot)
        {
            _entryBounds.Clear();
            Rectangle listBounds = new Rectangle(Position.X + 19, Position.Y + 102, 277, 176);
            sprite.Draw(_pixel, listBounds, new Color(17, 25, 38, 54));

            int rowHeight = 25;
            for (int i = 0; i < GuildSearchPageRows; i++)
            {
                Rectangle rowBounds = new Rectangle(listBounds.X + 2, listBounds.Y + 2 + (i * rowHeight), listBounds.Width - 4, rowHeight - 2);
                _entryBounds.Add(rowBounds);

                bool selected = i == snapshot.SelectedVisibleIndex;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(110, 164, 220, 110) : new Color(255, 255, 255, i % 2 == 0 ? 22 : 12));
                if (i >= snapshot.Entries.Count)
                {
                    continue;
                }

                GuildSearchEntrySnapshot entry = snapshot.Entries[i];
                DrawText(sprite, entry.GuildName, rowBounds.X + 6, rowBounds.Y + 2, new Color(244, 247, 251), 0.4f);
                DrawText(sprite, entry.MasterName, rowBounds.X + 118, rowBounds.Y + 2, new Color(223, 229, 236), 0.35f);
                DrawText(sprite, entry.LevelRange, rowBounds.X + 184, rowBounds.Y + 2, new Color(255, 231, 174), 0.33f);
                DrawText(sprite, entry.MemberSummary, rowBounds.X + 6, rowBounds.Y + 13, new Color(171, 179, 191), 0.32f);
                DrawText(sprite, entry.IsWatched ? "Watched" : string.Empty, rowBounds.Right - 40, rowBounds.Y + 13, new Color(152, 209, 164), 0.3f);
            }

            DrawText(sprite, $"{snapshot.Page}/{snapshot.TotalPages}", listBounds.Right - 26, listBounds.Bottom + 7, new Color(102, 111, 123), 0.33f);
        }

        private void DrawSummary(SpriteBatch sprite, GuildSearchSnapshot snapshot)
        {
            Rectangle summaryBounds = new Rectangle(Position.X + 20, Position.Y + 316, 274, 42);
            sprite.Draw(_pixel, summaryBounds, new Color(5, 11, 18, 86));

            int y = summaryBounds.Y + 5;
            foreach (string line in snapshot.SummaryLines.Take(3))
            {
                DrawText(sprite, line, summaryBounds.X + 6, y, new Color(228, 233, 239), 0.33f);
                y += 12;
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

        private const int GuildSearchPageRows = 7;
    }
}
