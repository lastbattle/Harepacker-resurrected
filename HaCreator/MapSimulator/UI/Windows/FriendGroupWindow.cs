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
        private readonly Point _overlayOffset;
        private readonly Point _baseOffset;
        private readonly Point _addFriendPopupOffset;
        private readonly Point _groupWhisperPopupOffset;
        private readonly Texture2D _pixel;
        private readonly UIObject _okButton;
        private readonly UIObject _cancelButton;
        private readonly List<Rectangle> _entryBounds = new();

        private Func<FriendGroupPopupSnapshot> _snapshotProvider;
        private Action<int> _toggleEntryHandler;
        private Action<int> _scrollHandler;
        private Func<string> _confirmHandler;
        private Func<string> _cancelHandler;
        private Action<string> _feedbackHandler;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private int _previousScrollWheelValue;
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
            UIObject okButton,
            UIObject cancelButton,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _baseLayer = baseLayer;
            _addFriendPopupLayer = addFriendPopupLayer;
            _groupWhisperPopupLayer = groupWhisperPopupLayer;
            _overlayOffset = overlayOffset;
            _baseOffset = baseOffset;
            _addFriendPopupOffset = addFriendPopupOffset;
            _groupWhisperPopupOffset = groupWhisperPopupOffset;
            _okButton = okButton;
            _cancelButton = cancelButton;
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
            Func<string> confirmHandler,
            Func<string> cancelHandler,
            Action<string> feedbackHandler)
        {
            _toggleEntryHandler = toggleEntryHandler;
            _scrollHandler = scrollHandler;
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
            HandleScrollWheel(mouseState);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
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
            DrawEntryList(sprite, snapshot);
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

        private void DrawPopupLayer(
            SpriteBatch sprite,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            if ((_currentSnapshot?.Mode ?? FriendGroupPopupMode.AddFriend) == FriendGroupPopupMode.GroupWhisper)
            {
                DrawLayer(sprite, _groupWhisperPopupLayer, _groupWhisperPopupOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }
            else
            {
                DrawLayer(sprite, _addFriendPopupLayer, _addFriendPopupOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }
        }

        private void DrawEntryList(SpriteBatch sprite, FriendGroupPopupSnapshot snapshot)
        {
            _entryBounds.Clear();
            Rectangle listBounds = new Rectangle(Position.X + 17, Position.Y + 132, 228, 157);
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
