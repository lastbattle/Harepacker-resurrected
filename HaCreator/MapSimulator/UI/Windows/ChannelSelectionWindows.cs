using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class WorldSelectWindow : UIWindowBase
    {
        private sealed class WorldButtonEntry
        {
            public WorldButtonEntry(int worldId, UIObject button, Texture2D icon)
            {
                WorldId = worldId;
                Button = button;
                Icon = icon;
            }

            public int WorldId { get; }
            public UIObject Button { get; }
            public Texture2D Icon { get; }
        }

        private readonly List<WorldButtonEntry> _worldButtons = new List<WorldButtonEntry>();
        private readonly Texture2D _highlightTexture;
        private SpriteFont _font;
        private int _currentWorldId;
        private int _selectedWorldId;

        public WorldSelectWindow(IDXObject frame, Texture2D highlightTexture, IEnumerable<(int worldId, UIObject button, Texture2D icon)> worldButtons)
            : base(frame)
        {
            _highlightTexture = highlightTexture ?? throw new ArgumentNullException(nameof(highlightTexture));

            foreach ((int worldId, UIObject button, Texture2D icon) in worldButtons ?? Enumerable.Empty<(int, UIObject, Texture2D)>())
            {
                if (button == null || icon == null)
                {
                    continue;
                }

                int capturedWorldId = worldId;
                button.ButtonClickReleased += _ => SelectWorld(capturedWorldId);
                AddButton(button);
                _worldButtons.Add(new WorldButtonEntry(worldId, button, icon));
            }

            if (_worldButtons.Count > 0)
            {
                _currentWorldId = _worldButtons[0].WorldId;
                _selectedWorldId = _currentWorldId;
                UpdateButtonStates();
            }
        }

        public override string WindowName => MapSimulatorWindowNames.WorldSelect;

        public event Action<int> WorldSelected;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetCurrentWorld(int worldId)
        {
            _currentWorldId = worldId;
            _selectedWorldId = worldId;
            UpdateButtonStates();
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
            foreach (WorldButtonEntry entry in _worldButtons)
            {
                Rectangle rect = new Rectangle(
                    Position.X + entry.Button.X - 6,
                    Position.Y + entry.Button.Y - 4,
                    entry.Button.CanvasSnapshotWidth + 12,
                    entry.Button.CanvasSnapshotHeight + 8);

                Color fillColor = entry.WorldId == _selectedWorldId
                    ? new Color(88, 126, 210, 180)
                    : entry.WorldId == _currentWorldId
                        ? new Color(70, 88, 120, 150)
                        : new Color(28, 34, 46, 120);
                sprite.Draw(_highlightTexture, rect, fillColor);
            }

            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "Select World",
                new Vector2(Position.X + 18, Position.Y + 14),
                Color.White);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                $"Current world: {_currentWorldId}",
                new Vector2(Position.X + 18, Position.Y + 174),
                new Color(220, 220, 220));
        }

        private void SelectWorld(int worldId)
        {
            _selectedWorldId = worldId;
            UpdateButtonStates();
            WorldSelected?.Invoke(worldId);
        }

        private void UpdateButtonStates()
        {
            foreach (WorldButtonEntry entry in _worldButtons)
            {
                entry.Button.SetButtonState(entry.WorldId == _selectedWorldId
                    ? UIObjectState.Pressed
                    : UIObjectState.Normal);
            }
        }
    }

    public sealed class ChannelSelectWindow : UIWindowBase
    {
        private sealed class ChannelButtonEntry
        {
            public ChannelButtonEntry(int channelIndex, UIObject button, Texture2D icon)
            {
                ChannelIndex = channelIndex;
                Button = button;
                Icon = icon;
            }

            public int ChannelIndex { get; }
            public UIObject Button { get; }
            public Texture2D Icon { get; }
        }

        private readonly Texture2D _overlayTexture2;
        private readonly Point _overlayOffset2;
        private readonly Texture2D _overlayTexture3;
        private readonly Point _overlayOffset3;
        private readonly Texture2D _highlightTexture;
        private readonly UIObject _changeButton;
        private readonly UIObject _cancelButton;
        private readonly List<ChannelButtonEntry> _channelButtons = new List<ChannelButtonEntry>();
        private readonly Dictionary<int, Texture2D> _worldBadges;
        private SpriteFont _font;
        private int _selectedWorldId;
        private int _currentWorldId;
        private int _currentChannelIndex;
        private int _selectedChannelIndex;
        private int _channelCount;

        public ChannelSelectWindow(
            IDXObject frame,
            Texture2D overlayTexture2,
            Point overlayOffset2,
            Texture2D overlayTexture3,
            Point overlayOffset3,
            Texture2D highlightTexture,
            UIObject changeButton,
            UIObject cancelButton,
            IEnumerable<(int channelIndex, UIObject button, Texture2D icon)> channelButtons,
            Dictionary<int, Texture2D> worldBadges)
            : base(frame)
        {
            _overlayTexture2 = overlayTexture2;
            _overlayOffset2 = overlayOffset2;
            _overlayTexture3 = overlayTexture3;
            _overlayOffset3 = overlayOffset3;
            _highlightTexture = highlightTexture;
            _changeButton = changeButton;
            _cancelButton = cancelButton;
            _worldBadges = worldBadges ?? new Dictionary<int, Texture2D>();

            foreach ((int channelIndex, UIObject button, Texture2D icon) in channelButtons ?? Enumerable.Empty<(int, UIObject, Texture2D)>())
            {
                if (button == null)
                {
                    continue;
                }

                int capturedChannel = channelIndex;
                button.ButtonClickReleased += _ => SelectChannel(capturedChannel);
                AddButton(button);
                _channelButtons.Add(new ChannelButtonEntry(channelIndex, button, icon));
            }

            if (_changeButton != null)
            {
                _changeButton.ButtonClickReleased += _ =>
                {
                    if (CanApplySelection())
                    {
                        ChangeRequested?.Invoke(_selectedWorldId, _selectedChannelIndex);
                    }
                };
                AddButton(_changeButton);
            }

            if (_cancelButton != null)
            {
                _cancelButton.ButtonClickReleased += _ => Hide();
                AddButton(_cancelButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.ChannelSelect;

        public event Action<int, int> ChangeRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(int selectedWorldId, int currentWorldId, int currentChannelIndex, int channelCount)
        {
            _selectedWorldId = selectedWorldId;
            _currentWorldId = currentWorldId;
            _currentChannelIndex = Math.Max(0, currentChannelIndex);
            _channelCount = Math.Max(0, Math.Min(channelCount, _channelButtons.Count));

            _selectedChannelIndex = _selectedWorldId == _currentWorldId
                ? Math.Min(_currentChannelIndex, Math.Max(0, _channelCount - 1))
                : 0;

            UpdateButtonStates();
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
            if (_overlayTexture2 != null)
            {
                sprite.Draw(_overlayTexture2, new Vector2(Position.X + _overlayOffset2.X, Position.Y + _overlayOffset2.Y), Color.White);
            }

            if (_overlayTexture3 != null)
            {
                sprite.Draw(_overlayTexture3, new Vector2(Position.X + _overlayOffset3.X, Position.Y + _overlayOffset3.Y), Color.White);
            }

            if (_worldBadges.TryGetValue(_selectedWorldId, out Texture2D badgeTexture))
            {
                sprite.Draw(badgeTexture, new Vector2(Position.X + 18, Position.Y + 18), Color.White);
            }

            if (_highlightTexture != null)
            {
                foreach (ChannelButtonEntry entry in _channelButtons)
                {
                    if (!entry.Button.ButtonVisible)
                    {
                        continue;
                    }

                    if (entry.ChannelIndex == _selectedChannelIndex)
                    {
                        Rectangle rect = new Rectangle(
                            Position.X + entry.Button.X - 2,
                            Position.Y + entry.Button.Y - 1,
                            entry.Button.CanvasSnapshotWidth + 4,
                            entry.Button.CanvasSnapshotHeight + 2);
                        sprite.Draw(_highlightTexture, rect, new Color(82, 123, 214, 130));
                    }
                    else if (_selectedWorldId == _currentWorldId && entry.ChannelIndex == _currentChannelIndex)
                    {
                        Rectangle rect = new Rectangle(
                            Position.X + entry.Button.X - 2,
                            Position.Y + entry.Button.Y - 1,
                            entry.Button.CanvasSnapshotWidth + 4,
                            entry.Button.CanvasSnapshotHeight + 2);
                        sprite.Draw(_highlightTexture, rect, new Color(68, 86, 118, 120));
                    }
                }
            }

            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "Select Channel",
                new Vector2(Position.X + 104, Position.Y + 20),
                Color.White);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                $"Current: Channel {_currentChannelIndex + 1}",
                new Vector2(Position.X + 104, Position.Y + 42),
                new Color(220, 220, 220));
        }

        protected override void DrawOverlay(
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
            foreach (ChannelButtonEntry entry in _channelButtons)
            {
                if (!entry.Button.ButtonVisible)
                {
                    continue;
                }

                if (entry.Icon != null)
                {
                    sprite.Draw(entry.Icon, new Vector2(Position.X + entry.Button.X + 8, Position.Y + entry.Button.Y + 5), Color.White);
                }

                if (_font == null)
                {
                    continue;
                }

                if (_selectedWorldId == _currentWorldId && entry.ChannelIndex == _currentChannelIndex)
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        "Current",
                        new Vector2(Position.X + entry.Button.X + 32, Position.Y + entry.Button.Y + 2),
                        new Color(255, 228, 151));
                }
            }
        }

        private void SelectChannel(int channelIndex)
        {
            _selectedChannelIndex = channelIndex;
            UpdateButtonStates();
        }

        private bool CanApplySelection()
        {
            return _selectedChannelIndex >= 0 &&
                   _selectedChannelIndex < _channelCount &&
                   (_selectedWorldId != _currentWorldId || _selectedChannelIndex != _currentChannelIndex);
        }

        private void UpdateButtonStates()
        {
            foreach (ChannelButtonEntry entry in _channelButtons)
            {
                bool visible = entry.ChannelIndex < _channelCount;
                entry.Button.SetVisible(visible);
                entry.Button.SetEnabled(visible);
                entry.Button.SetButtonState(entry.ChannelIndex == _selectedChannelIndex
                    ? UIObjectState.Pressed
                    : UIObjectState.Normal);
            }

            _changeButton?.SetEnabled(CanApplySelection());
        }
    }

    public sealed class ChannelShiftWindow : UIWindowBase
    {
        private readonly Texture2D _overlayTexture2;
        private readonly Point _overlayOffset2;
        private readonly Texture2D _overlayTexture3;
        private readonly Point _overlayOffset3;
        private readonly Texture2D _rowTexture;
        private readonly Dictionary<int, Texture2D> _worldBadges;
        private readonly Dictionary<int, Texture2D> _channelIcons;
        private SpriteFont _font;
        private int _worldId;
        private int _channelIndex;
        private int _hideAtTick;

        public ChannelShiftWindow(
            IDXObject frame,
            Texture2D overlayTexture2,
            Point overlayOffset2,
            Texture2D overlayTexture3,
            Point overlayOffset3,
            Texture2D rowTexture,
            Dictionary<int, Texture2D> worldBadges,
            Dictionary<int, Texture2D> channelIcons)
            : base(frame)
        {
            _overlayTexture2 = overlayTexture2;
            _overlayOffset2 = overlayOffset2;
            _overlayTexture3 = overlayTexture3;
            _overlayOffset3 = overlayOffset3;
            _rowTexture = rowTexture;
            _worldBadges = worldBadges ?? new Dictionary<int, Texture2D>();
            _channelIcons = channelIcons ?? new Dictionary<int, Texture2D>();
        }

        public override string WindowName => MapSimulatorWindowNames.ChannelShift;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void BeginShift(int worldId, int channelIndex, int durationMs = 1200)
        {
            _worldId = worldId;
            _channelIndex = channelIndex;
            _hideAtTick = Environment.TickCount + Math.Max(1, durationMs);
            Show();
        }

        public override void Update(GameTime gameTime)
        {
            if (IsVisible && unchecked(Environment.TickCount - _hideAtTick) >= 0)
            {
                Hide();
            }
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
            if (_overlayTexture2 != null)
            {
                sprite.Draw(_overlayTexture2, new Vector2(Position.X + _overlayOffset2.X, Position.Y + _overlayOffset2.Y), Color.White);
            }

            if (_overlayTexture3 != null)
            {
                sprite.Draw(_overlayTexture3, new Vector2(Position.X + _overlayOffset3.X, Position.Y + _overlayOffset3.Y), Color.White);
            }

            if (_font != null)
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    "Changing Channel",
                    new Vector2(Position.X + 108, Position.Y + 18),
                    Color.White);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Moving to channel {_channelIndex + 1}",
                    new Vector2(Position.X + 108, Position.Y + 40),
                    new Color(220, 220, 220));
            }

            if (_worldBadges.TryGetValue(_worldId, out Texture2D badgeTexture))
            {
                sprite.Draw(badgeTexture, new Vector2(Position.X + 18, Position.Y + 18), Color.White);
            }

            if (_rowTexture != null)
            {
                Vector2 rowPosition = new Vector2(Position.X + 148, Position.Y + 88);
                sprite.Draw(_rowTexture, rowPosition, Color.White);

                if (_channelIcons.TryGetValue(_channelIndex, out Texture2D channelTexture))
                {
                    sprite.Draw(channelTexture, rowPosition + new Vector2(8f, 5f), Color.White);
                }
            }
        }
    }

    internal static class SelectorWindowDrawing
    {
        public static void DrawShadowedText(SpriteBatch sprite, SpriteFont font, string text, Vector2 position, Color color)
        {
            if (sprite == null || font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(font, text, position, color);
        }
    }
}
