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
    public enum SelectorAvailability
    {
        Available = 0,
        Busy = 1,
        Full = 2,
        Disabled = 3,
    }

    public sealed class WorldSelectionState
    {
        public WorldSelectionState(
            int worldId,
            int activeChannels,
            int totalChannels,
            int occupancyPercent,
            bool isSelectable,
            bool isRecommended = false,
            bool isLatestConnected = false)
        {
            WorldId = worldId;
            ActiveChannels = Math.Max(0, activeChannels);
            TotalChannels = Math.Max(0, totalChannels);
            OccupancyPercent = Math.Clamp(occupancyPercent, 0, 100);
            IsSelectable = isSelectable;
            IsRecommended = isRecommended;
            IsLatestConnected = isLatestConnected;
        }

        public int WorldId { get; }
        public int ActiveChannels { get; }
        public int TotalChannels { get; }
        public int OccupancyPercent { get; }
        public bool IsSelectable { get; }
        public bool IsRecommended { get; }
        public bool IsLatestConnected { get; }

        public SelectorAvailability Availability => !IsSelectable
            ? SelectorAvailability.Disabled
            : OccupancyPercent >= 95
                ? SelectorAvailability.Full
                : OccupancyPercent >= 70
                    ? SelectorAvailability.Busy
                    : SelectorAvailability.Available;
    }

    public sealed class ChannelSelectionState
    {
        public ChannelSelectionState(int channelIndex, int userCount, int capacity, bool isSelectable)
        {
            ChannelIndex = Math.Max(0, channelIndex);
            UserCount = Math.Max(0, userCount);
            Capacity = Math.Max(0, capacity);
            IsSelectable = isSelectable;
        }

        public int ChannelIndex { get; }
        public int UserCount { get; }
        public int Capacity { get; }
        public bool IsSelectable { get; }

        public int OccupancyPercent => Capacity <= 0
            ? 0
            : Math.Clamp((int)Math.Round((double)UserCount * 100d / Capacity), 0, 100);

        public SelectorAvailability Availability => !IsSelectable
            ? SelectorAvailability.Disabled
            : OccupancyPercent >= 95
                ? SelectorAvailability.Full
                : OccupancyPercent >= 70
                    ? SelectorAvailability.Busy
                    : SelectorAvailability.Available;
    }

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
        private readonly Dictionary<int, WorldSelectionState> _worldStates = new Dictionary<int, WorldSelectionState>();
        private readonly Texture2D _highlightTexture;
        private SpriteFont _font;
        private int _currentWorldId;
        private int _selectedWorldId;
        private bool _requestAllowed = true;
        private string _statusMessage;

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

        public IReadOnlyList<int> WorldIds => _worldButtons.Select(entry => entry.WorldId).ToArray();

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(
            IReadOnlyDictionary<int, WorldSelectionState> worldStates,
            int currentWorldId,
            int selectedWorldId,
            bool requestAllowed = true,
            string statusMessage = null)
        {
            _worldStates.Clear();
            if (worldStates != null)
            {
                foreach (KeyValuePair<int, WorldSelectionState> pair in worldStates)
                {
                    if (pair.Value != null)
                    {
                        _worldStates[pair.Key] = pair.Value;
                    }
                }
            }

            _currentWorldId = currentWorldId;
            _selectedWorldId = selectedWorldId;
            _requestAllowed = requestAllowed;
            _statusMessage = statusMessage;
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

                Color fillColor = GetWorldFillColor(entry.WorldId);
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

            if (_worldStates.TryGetValue(_selectedWorldId, out WorldSelectionState selectedState))
            {
                if (!string.IsNullOrWhiteSpace(_statusMessage))
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        _statusMessage,
                        new Vector2(Position.X + 18, Position.Y + 110),
                        _requestAllowed ? new Color(198, 198, 198) : new Color(255, 204, 107));
                }

                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Available channels: {selectedState.ActiveChannels}/{selectedState.TotalChannels}",
                    new Vector2(Position.X + 18, Position.Y + 142),
                    new Color(220, 220, 220));
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Load: {selectedState.OccupancyPercent}% ({selectedState.Availability})",
                    new Vector2(Position.X + 18, Position.Y + 158),
                    SelectorWindowDrawing.GetAvailabilityColor(selectedState.Availability));

                string markerLabel = SelectorWindowDrawing.BuildWorldMarkerLabel(selectedState);
                if (!string.IsNullOrWhiteSpace(markerLabel))
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        markerLabel,
                        new Vector2(Position.X + 18, Position.Y + 174),
                        new Color(163, 226, 255));
                }
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                $"Current world: {_currentWorldId}",
                new Vector2(Position.X + 18, Position.Y + 126),
                new Color(220, 220, 220));
        }

        private void SelectWorld(int worldId)
        {
            if (!_requestAllowed)
            {
                return;
            }

            if (_worldStates.TryGetValue(worldId, out WorldSelectionState state) && !state.IsSelectable)
            {
                return;
            }

            _selectedWorldId = worldId;
            UpdateButtonStates();
            WorldSelected?.Invoke(worldId);
        }

        private Color GetWorldFillColor(int worldId)
        {
            SelectorAvailability availability = _worldStates.TryGetValue(worldId, out WorldSelectionState state)
                ? state.Availability
                : SelectorAvailability.Disabled;

            if (worldId == _selectedWorldId)
            {
                return availability switch
                {
                    SelectorAvailability.Disabled => new Color(92, 92, 92, 170),
                    SelectorAvailability.Full => new Color(170, 76, 76, 180),
                    SelectorAvailability.Busy => new Color(176, 134, 66, 180),
                    _ => new Color(88, 126, 210, 180),
                };
            }

            if (worldId == _currentWorldId)
            {
                return availability == SelectorAvailability.Disabled
                    ? new Color(82, 82, 82, 150)
                    : new Color(70, 88, 120, 150);
            }

            return availability switch
            {
                SelectorAvailability.Disabled => new Color(38, 38, 38, 100),
                SelectorAvailability.Full => new Color(82, 48, 48, 120),
                SelectorAvailability.Busy => new Color(82, 70, 44, 120),
                _ => new Color(28, 34, 46, 120),
            };
        }

        private void UpdateButtonStates()
        {
            foreach (WorldButtonEntry entry in _worldButtons)
            {
                bool isSelectable = _requestAllowed &&
                                    (!_worldStates.TryGetValue(entry.WorldId, out WorldSelectionState state) || state.IsSelectable);
                entry.Button.SetEnabled(isSelectable);
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
        private readonly Dictionary<int, ChannelSelectionState> _channelStates = new Dictionary<int, ChannelSelectionState>();
        private readonly Dictionary<int, Texture2D> _worldBadges;
        private SpriteFont _font;
        private int _selectedWorldId;
        private int _currentWorldId;
        private int _currentChannelIndex;
        private int _selectedChannelIndex;
        private int _channelCount;
        private bool _requestAllowed = true;
        private string _statusMessage;

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

        public void Configure(
            int selectedWorldId,
            int currentWorldId,
            int currentChannelIndex,
            IReadOnlyList<ChannelSelectionState> channelStates,
            bool requestAllowed = true,
            string statusMessage = null)
        {
            _channelStates.Clear();
            if (channelStates != null)
            {
                foreach (ChannelSelectionState state in channelStates)
                {
                    if (state != null && !_channelStates.ContainsKey(state.ChannelIndex))
                    {
                        _channelStates.Add(state.ChannelIndex, state);
                    }
                }
            }

            _selectedWorldId = selectedWorldId;
            _currentWorldId = currentWorldId;
            _currentChannelIndex = Math.Max(0, currentChannelIndex);
            _channelCount = Math.Max(0, Math.Min(_channelStates.Count, _channelButtons.Count));
            _requestAllowed = requestAllowed;
            _statusMessage = statusMessage;

            _selectedChannelIndex = _selectedWorldId == _currentWorldId
                ? Math.Min(_currentChannelIndex, Math.Max(0, _channelCount - 1))
                : FindFirstSelectableChannel();

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

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + 104, Position.Y + 58),
                    _requestAllowed ? new Color(198, 198, 198) : new Color(255, 204, 107));
            }
            else if (_channelStates.TryGetValue(_selectedChannelIndex, out ChannelSelectionState selectedState))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Users: {selectedState.UserCount}/{selectedState.Capacity}",
                    new Vector2(Position.X + 104, Position.Y + 58),
                    new Color(220, 220, 220));
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Load: {selectedState.OccupancyPercent}% ({selectedState.Availability})",
                    new Vector2(Position.X + 104, Position.Y + 74),
                    SelectorWindowDrawing.GetAvailabilityColor(selectedState.Availability));
            }

            foreach (ChannelButtonEntry entry in _channelButtons)
            {
                if (!entry.Button.ButtonVisible || !_channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState state))
                {
                    continue;
                }

                Rectangle gaugeBounds = new Rectangle(
                    Position.X + entry.Button.X + 30,
                    Position.Y + entry.Button.Y + 8,
                    28,
                    4);
                sprite.Draw(_highlightTexture, gaugeBounds, new Color(28, 34, 46, 180));

                int fillWidth = Math.Max(1, (int)Math.Round(gaugeBounds.Width * (state.OccupancyPercent / 100f)));
                sprite.Draw(
                    _highlightTexture,
                    new Rectangle(gaugeBounds.X, gaugeBounds.Y, fillWidth, gaugeBounds.Height),
                    SelectorWindowDrawing.GetAvailabilityColor(state.Availability));
            }
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
                else if (_channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState state) &&
                         state.Availability != SelectorAvailability.Available)
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        state.Availability.ToString(),
                        new Vector2(Position.X + entry.Button.X + 29, Position.Y + entry.Button.Y + 1),
                        SelectorWindowDrawing.GetAvailabilityColor(state.Availability));
                }
            }
        }

        private void SelectChannel(int channelIndex)
        {
            if (!_requestAllowed)
            {
                return;
            }

            if (_channelStates.TryGetValue(channelIndex, out ChannelSelectionState state) && !state.IsSelectable)
            {
                return;
            }

            _selectedChannelIndex = channelIndex;
            UpdateButtonStates();
        }

        private int FindFirstSelectableChannel()
        {
            foreach (ChannelButtonEntry entry in _channelButtons.OrderBy(button => button.ChannelIndex))
            {
                if (_channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState state) && state.IsSelectable)
                {
                    return entry.ChannelIndex;
                }
            }

            return 0;
        }

        private bool CanApplySelection()
        {
            return _requestAllowed &&
                   _selectedChannelIndex >= 0 &&
                   _selectedChannelIndex < _channelCount &&
                   _channelStates.TryGetValue(_selectedChannelIndex, out ChannelSelectionState state) &&
                   state.IsSelectable &&
                   (_selectedWorldId != _currentWorldId || _selectedChannelIndex != _currentChannelIndex);
        }

        private void UpdateButtonStates()
        {
            foreach (ChannelButtonEntry entry in _channelButtons)
            {
                bool hasState = _channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState state);
                bool visible = hasState && state.Capacity > 0;
                bool isSelectable = _requestAllowed && visible && state.IsSelectable;
                entry.Button.SetVisible(visible);
                entry.Button.SetEnabled(isSelectable);
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

        public static Color GetAvailabilityColor(SelectorAvailability availability)
        {
            return availability switch
            {
                SelectorAvailability.Disabled => new Color(140, 140, 140),
                SelectorAvailability.Full => new Color(236, 105, 105),
                SelectorAvailability.Busy => new Color(255, 204, 107),
                _ => new Color(145, 232, 145),
            };
        }

        public static string BuildWorldMarkerLabel(WorldSelectionState state)
        {
            if (state == null)
            {
                return null;
            }

            if (state.IsLatestConnected && state.IsRecommended)
            {
                return "Latest connected and recommended";
            }

            if (state.IsLatestConnected)
            {
                return "Latest connected world";
            }

            if (state.IsRecommended)
            {
                return "Recommended world";
            }

            return null;
        }
    }
}
