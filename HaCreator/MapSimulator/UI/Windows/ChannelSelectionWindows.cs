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
    public readonly struct SelectorOverlayFrame
    {
        public SelectorOverlayFrame(Texture2D texture, Point offset)
        {
            Texture = texture;
            Offset = offset;
        }

        public Texture2D Texture { get; }
        public Point Offset { get; }
        public bool IsEmpty => Texture == null;
    }

    public readonly struct SelectorAnimatedOverlayFrame
    {
        public SelectorAnimatedOverlayFrame(Texture2D texture, Point offset)
        {
            Texture = texture;
            Offset = offset;
        }

        public Texture2D Texture { get; }
        public Point Offset { get; }
        public bool IsEmpty => Texture == null;
    }

    public sealed class SelectorAnimatedOverlay
    {
        private readonly IReadOnlyList<SelectorAnimatedOverlayFrame> _frames;

        public SelectorAnimatedOverlay(IReadOnlyList<SelectorAnimatedOverlayFrame> frames, int frameDelayMs)
        {
            _frames = frames ?? Array.Empty<SelectorAnimatedOverlayFrame>();
            FrameDelayMs = frameDelayMs <= 0 ? 100 : frameDelayMs;
        }

        public int FrameDelayMs { get; }
        public bool IsEmpty => _frames.Count == 0 || _frames.All(frame => frame.IsEmpty);

        public SelectorAnimatedOverlayFrame GetFrame(int tickCount)
        {
            if (_frames.Count == 0)
            {
                return default;
            }

            int frameIndex = _frames.Count == 1
                ? 0
                : Math.Abs(tickCount / FrameDelayMs) % _frames.Count;
            return _frames[frameIndex];
        }
    }

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
            bool hasAdultChannels = false,
            bool isRecommended = false,
            bool isLatestConnected = false,
            byte worldState = 0,
            bool blocksCharacterCreation = false,
            string worldName = null)
        {
            WorldId = worldId;
            ActiveChannels = Math.Max(0, activeChannels);
            TotalChannels = Math.Max(0, totalChannels);
            OccupancyPercent = Math.Clamp(occupancyPercent, 0, 100);
            IsSelectable = isSelectable;
            HasAdultChannels = hasAdultChannels;
            IsRecommended = isRecommended;
            IsLatestConnected = isLatestConnected;
            WorldState = worldState;
            BlocksCharacterCreation = blocksCharacterCreation;
            WorldName = string.IsNullOrWhiteSpace(worldName) ? null : worldName.Trim();
        }

        public int WorldId { get; }
        public int ActiveChannels { get; }
        public int TotalChannels { get; }
        public int OccupancyPercent { get; }
        public bool IsSelectable { get; }
        public bool HasAdultChannels { get; }
        public bool IsRecommended { get; }
        public bool IsLatestConnected { get; }
        public byte WorldState { get; }
        public bool BlocksCharacterCreation { get; }
        public string WorldName { get; }

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
        public ChannelSelectionState(int channelIndex, int userCount, int capacity, bool isSelectable, bool requiresAdultAccount = false)
        {
            ChannelIndex = Math.Max(0, channelIndex);
            UserCount = Math.Max(0, userCount);
            Capacity = Math.Max(0, capacity);
            IsSelectable = isSelectable;
            RequiresAdultAccount = requiresAdultAccount;
        }

        public int ChannelIndex { get; }
        public int UserCount { get; }
        public int Capacity { get; }
        public bool IsSelectable { get; }
        public bool RequiresAdultAccount { get; }

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

        public bool CanSelect(bool hasAdultAccess)
        {
            return IsSelectable && (!RequiresAdultAccount || hasAdultAccess);
        }
    }

    public sealed class WorldSelectWindow : UIWindowBase
    {
        private const int ClientWorldSlotCount = 36;
        private const int ClientWorldSlotColumnCount = 6;
        private const int ClientWorldSlotRowCount = 6;
        private const int ClientWorldSlotStartX = 0;
        private const int ClientWorldSlotStartY = 0;
        private const int ClientWorldSlotColumnSpacing = 96;
        private const int ClientWorldSlotRowSpacing = 26;
        private const int ClientViewButtonY = 238;
        private const int ClientViewChoiceX = 78;
        private const int ClientViewAllX = 192;
        private const int ClientWorldStateIconOffsetX = 78;
        private const int ClientWorldStateIconOffsetY = 6;

        private sealed class WorldButtonEntry
        {
            public WorldButtonEntry(int worldId, UIObject button, Texture2D icon, SelectorOverlayFrame keyFocusedFrame)
            {
                WorldId = worldId;
                Button = button;
                Icon = icon;
                KeyFocusedFrame = keyFocusedFrame;
            }

            public int WorldId { get; }
            public UIObject Button { get; }
            public Texture2D Icon { get; }
            public SelectorOverlayFrame KeyFocusedFrame { get; }
        }

        private readonly List<WorldButtonEntry> _worldButtons = new List<WorldButtonEntry>();
        private readonly List<UIObject> _emptyWorldButtons = new List<UIObject>();
        private readonly Dictionary<int, WorldSelectionState> _worldStates = new Dictionary<int, WorldSelectionState>();
        private readonly List<int> _orderedWorldIds = new List<int>();
        private readonly Texture2D _frameOverlayTexture;
        private readonly Point _frameOverlayOffset;
        private readonly Texture2D _highlightTexture;
        private readonly IReadOnlyDictionary<byte, SelectorAnimatedOverlay> _worldStateAnimations;
        private readonly UIObject _viewChoiceButton;
        private readonly UIObject _viewAllButton;
        private readonly SelectorOverlayFrame _viewAllKeyFocusedFrame;
        private SpriteFont _font;
        private int _currentWorldId;
        private int _selectedWorldId;
        private bool _hasAdultAccess;
        private bool _requestAllowed = true;
        private string _statusMessage;
        private bool _viewAllEnabled;
        private KeyboardState _previousKeyboardState;
        private bool _focusViewAllButton;

        public WorldSelectWindow(
            IDXObject frame,
            Texture2D frameOverlayTexture,
            Point frameOverlayOffset,
            Texture2D highlightTexture,
            IReadOnlyDictionary<byte, SelectorAnimatedOverlay> worldStateAnimations,
            IEnumerable<(int worldId, UIObject button, Texture2D icon, SelectorOverlayFrame keyFocusedFrame)> worldButtons,
            IEnumerable<UIObject> emptySlotButtons = null,
            UIObject viewChoiceButton = null,
            UIObject viewAllButton = null,
            SelectorOverlayFrame viewAllKeyFocusedFrame = default)
            : base(frame)
        {
            _frameOverlayTexture = frameOverlayTexture;
            _frameOverlayOffset = frameOverlayOffset;
            _highlightTexture = highlightTexture;
            _worldStateAnimations = worldStateAnimations ?? new Dictionary<byte, SelectorAnimatedOverlay>();
            _viewChoiceButton = viewChoiceButton;
            _viewAllButton = viewAllButton;
            _viewAllKeyFocusedFrame = viewAllKeyFocusedFrame;

            foreach ((int worldId, UIObject button, Texture2D icon, SelectorOverlayFrame keyFocusedFrame) in worldButtons ?? Enumerable.Empty<(int, UIObject, Texture2D, SelectorOverlayFrame)>())
            {
                if (button == null || icon == null)
                {
                    continue;
                }

                int capturedWorldId = worldId;
                button.ButtonClickReleased += _ => SelectWorld(capturedWorldId);
                AddButton(button);
                _worldButtons.Add(new WorldButtonEntry(worldId, button, icon, keyFocusedFrame));
            }

            foreach (UIObject button in emptySlotButtons ?? Enumerable.Empty<UIObject>())
            {
                if (button == null)
                {
                    continue;
                }

                button.SetEnabled(false);
                AddButton(button);
                _emptyWorldButtons.Add(button);
            }

            if (_worldButtons.Count > 0)
            {
                _currentWorldId = _worldButtons[0].WorldId;
                _selectedWorldId = _currentWorldId;
                UpdateButtonStates();
            }

            if (_viewChoiceButton != null)
            {
                AddButton(_viewChoiceButton);
            }

            if (_viewAllButton != null)
            {
                _viewAllButton.ButtonClickReleased += _ =>
                {
                    if (_requestAllowed && _viewAllEnabled)
                    {
                        ViewAllRequested?.Invoke();
                    }
                };
                AddButton(_viewAllButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.WorldSelect;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        public event Action<int> WorldSelected;
        public event Action ViewAllRequested;

        public IReadOnlyList<int> WorldIds => _worldButtons.Select(entry => entry.WorldId).ToArray();

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                _focusViewAllButton = _viewAllEnabled;
                _previousKeyboardState = Keyboard.GetState();
            }
        }

        public void Configure(
            IReadOnlyDictionary<int, WorldSelectionState> worldStates,
            int currentWorldId,
            int selectedWorldId,
            bool hasAdultAccess,
            bool requestAllowed = true,
            string statusMessage = null,
            IReadOnlyList<int> orderedWorldIds = null,
            bool viewAllEnabled = false)
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
            _hasAdultAccess = hasAdultAccess;
            _requestAllowed = requestAllowed;
            _statusMessage = statusMessage;
            _viewAllEnabled = viewAllEnabled;
            _orderedWorldIds.Clear();
            if (orderedWorldIds != null)
            {
                foreach (int worldId in orderedWorldIds)
                {
                    if (_worldButtons.Any(entry => entry.WorldId == worldId) && !_orderedWorldIds.Contains(worldId))
                    {
                        _orderedWorldIds.Add(worldId);
                    }
                }
            }

            foreach (WorldButtonEntry entry in _worldButtons)
            {
                if (!_orderedWorldIds.Contains(entry.WorldId))
                {
                    _orderedWorldIds.Add(entry.WorldId);
                }
            }

            UpdateWorldButtonLayout();
            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            if (Pressed(keyboardState, Keys.Tab))
            {
                _focusViewAllButton = _viewAllEnabled && !_focusViewAllButton;
            }
            else if (Pressed(keyboardState, Keys.Left))
            {
                HandleWorldNavigation(-1);
            }
            else if (Pressed(keyboardState, Keys.Right))
            {
                HandleWorldNavigation(1);
            }
            else if (Pressed(keyboardState, Keys.Up))
            {
                HandleWorldNavigation(-ClientWorldSlotColumnCount);
            }
            else if (Pressed(keyboardState, Keys.Down))
            {
                HandleWorldNavigation(ClientWorldSlotColumnCount);
            }
            else if (Pressed(keyboardState, Keys.Enter))
            {
                if (_focusViewAllButton && _viewAllEnabled && _requestAllowed)
                {
                    ViewAllRequested?.Invoke();
                }
                else
                {
                    SelectWorld(_selectedWorldId);
                }
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
            int TickCount)
        {
            if (_frameOverlayTexture != null)
            {
                sprite.Draw(_frameOverlayTexture, new Vector2(Position.X + _frameOverlayOffset.X, Position.Y + _frameOverlayOffset.Y), Color.White);
            }

            if (_highlightTexture != null)
            {
                foreach (WorldButtonEntry entry in _worldButtons)
                {
                    if (!entry.Button.ButtonVisible)
                    {
                        continue;
                    }

                    Rectangle rect = new Rectangle(
                        Position.X + entry.Button.X - 6,
                        Position.Y + entry.Button.Y - 4,
                        entry.Button.CanvasSnapshotWidth + 12,
                        entry.Button.CanvasSnapshotHeight + 8);

                    Color fillColor = GetWorldFillColor(entry.WorldId);
                    sprite.Draw(_highlightTexture, rect, fillColor);
                }
            }

            DrawWorldStateOverlays(sprite, TickCount);
            DrawFocusedWorldOverlay(sprite);
            DrawViewAllFocusOverlay(sprite);

            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "Select World",
                new Vector2(Position.X + 24, Position.Y + 208),
                Color.White);

            if (_worldStates.TryGetValue(_selectedWorldId, out WorldSelectionState selectedState))
            {
                if (!string.IsNullOrWhiteSpace(_statusMessage))
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        _statusMessage,
                        new Vector2(Position.X + 24, Position.Y + 224),
                        _requestAllowed ? new Color(198, 198, 198) : new Color(255, 204, 107));
                }

                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Available channels: {selectedState.ActiveChannels}/{selectedState.TotalChannels}",
                    new Vector2(Position.X + 24, Position.Y + 240),
                    new Color(220, 220, 220));
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Load: {selectedState.OccupancyPercent}% ({selectedState.Availability})",
                    new Vector2(Position.X + 188, Position.Y + 240),
                    SelectorWindowDrawing.GetAvailabilityColor(selectedState.Availability));
            }

            string markerLabel = _worldStates.TryGetValue(_selectedWorldId, out WorldSelectionState markerState)
                ? SelectorWindowDrawing.BuildWorldMarkerLabel(markerState)
                : null;
            if (!string.IsNullOrWhiteSpace(markerLabel))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    markerLabel,
                    new Vector2(Position.X + 24, Position.Y + 256),
                    new Color(163, 226, 255));
            }
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
            _focusViewAllButton = false;
            UpdateButtonStates();
            WorldSelected?.Invoke(worldId);
        }

        private void DrawFocusedWorldOverlay(SpriteBatch sprite)
        {
            if (_focusViewAllButton)
            {
                return;
            }

            WorldButtonEntry focusedEntry = _worldButtons.FirstOrDefault(entry =>
                entry.WorldId == _selectedWorldId &&
                entry.Button.ButtonVisible &&
                !entry.KeyFocusedFrame.IsEmpty);
            if (focusedEntry == null)
            {
                return;
            }

            sprite.Draw(
                focusedEntry.KeyFocusedFrame.Texture,
                new Vector2(
                    Position.X + focusedEntry.Button.X + focusedEntry.KeyFocusedFrame.Offset.X,
                    Position.Y + focusedEntry.Button.Y + focusedEntry.KeyFocusedFrame.Offset.Y),
                Color.White);
        }

        private void DrawWorldStateOverlays(SpriteBatch sprite, int tickCount)
        {
            foreach (WorldButtonEntry entry in _worldButtons)
            {
                if (!entry.Button.ButtonVisible ||
                    !_worldStates.TryGetValue(entry.WorldId, out WorldSelectionState state) ||
                    state.WorldState <= 0 ||
                    !_worldStateAnimations.TryGetValue(state.WorldState, out SelectorAnimatedOverlay overlay) ||
                    overlay == null ||
                    overlay.IsEmpty)
                {
                    continue;
                }

                SelectorAnimatedOverlayFrame frame = overlay.GetFrame(tickCount);
                if (frame.IsEmpty)
                {
                    continue;
                }

                sprite.Draw(
                    frame.Texture,
                    new Vector2(
                        Position.X + entry.Button.X + ClientWorldStateIconOffsetX + frame.Offset.X,
                        Position.Y + entry.Button.Y + ClientWorldStateIconOffsetY + frame.Offset.Y),
                    Color.White);
            }
        }

        private void DrawViewAllFocusOverlay(SpriteBatch sprite)
        {
            if (!_focusViewAllButton ||
                !_viewAllEnabled ||
                _viewAllButton == null ||
                !_viewAllButton.ButtonVisible ||
                _viewAllKeyFocusedFrame.IsEmpty)
            {
                return;
            }

            sprite.Draw(
                _viewAllKeyFocusedFrame.Texture,
                new Vector2(
                    Position.X + _viewAllButton.X + _viewAllKeyFocusedFrame.Offset.X,
                    Position.Y + _viewAllButton.Y + _viewAllKeyFocusedFrame.Offset.Y),
                Color.White);
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
                entry.Button.SetVisible(_orderedWorldIds.Contains(entry.WorldId));
                entry.Button.SetEnabled(isSelectable);
                entry.Button.SetButtonState(entry.WorldId == _selectedWorldId
                    ? UIObjectState.Pressed
                    : UIObjectState.Normal);
            }

            foreach (UIObject button in _emptyWorldButtons)
            {
                button.SetEnabled(false);
                button.SetButtonState(UIObjectState.Disabled);
            }

            if (_viewChoiceButton != null)
            {
                _viewChoiceButton.SetVisible(true);
                _viewChoiceButton.SetEnabled(false);
                _viewChoiceButton.SetButtonState(UIObjectState.Pressed);
            }

            if (_viewAllButton != null)
            {
                _viewAllButton.SetVisible(true);
                _viewAllButton.SetEnabled(_requestAllowed && _viewAllEnabled);
                _viewAllButton.SetButtonState(_focusViewAllButton && _requestAllowed && _viewAllEnabled
                    ? UIObjectState.MouseOver
                    : UIObjectState.Normal);
            }
        }

        private void UpdateWorldButtonLayout()
        {
            Dictionary<int, int> orderLookup = _orderedWorldIds
                .Select((worldId, index) => new { worldId, index })
                .ToDictionary(pair => pair.worldId, pair => pair.index);

            foreach (WorldButtonEntry entry in _worldButtons)
            {
                if (!orderLookup.TryGetValue(entry.WorldId, out int index))
                {
                    entry.Button.SetVisible(false);
                    continue;
                }

                int slotIndex = Math.Clamp(index, 0, ClientWorldSlotCount - 1);
                int column = slotIndex % ClientWorldSlotColumnCount;
                int row = slotIndex / ClientWorldSlotColumnCount;
                if (row >= ClientWorldSlotRowCount)
                {
                    entry.Button.SetVisible(false);
                    continue;
                }

                entry.Button.X = ClientWorldSlotStartX + (column * ClientWorldSlotColumnSpacing);
                entry.Button.Y = ClientWorldSlotStartY + (row * ClientWorldSlotRowSpacing);
                entry.Button.SetVisible(true);
            }

            int firstEmptySlotIndex = Math.Min(_orderedWorldIds.Count, ClientWorldSlotCount);
            for (int i = 0; i < _emptyWorldButtons.Count; i++)
            {
                UIObject button = _emptyWorldButtons[i];
                int slotIndex = firstEmptySlotIndex + i;
                if (slotIndex >= ClientWorldSlotCount)
                {
                    button.SetVisible(false);
                    continue;
                }

                int column = slotIndex % ClientWorldSlotColumnCount;
                int row = slotIndex / ClientWorldSlotColumnCount;
                button.X = ClientWorldSlotStartX + (column * ClientWorldSlotColumnSpacing);
                button.Y = ClientWorldSlotStartY + (row * ClientWorldSlotRowSpacing);
                button.SetVisible(true);
            }

            if (_viewChoiceButton != null)
            {
                _viewChoiceButton.X = ClientViewChoiceX;
                _viewChoiceButton.Y = ClientViewButtonY;
            }

            if (_viewAllButton != null)
            {
                _viewAllButton.X = ClientViewAllX;
                _viewAllButton.Y = ClientViewButtonY;
            }
        }

        private void HandleWorldNavigation(int delta)
        {
            if (_focusViewAllButton)
            {
                if (delta < 0)
                {
                    _focusViewAllButton = false;
                }

                return;
            }

            List<int> visibleWorldIds = _orderedWorldIds
                .Where(worldId => _worldButtons.Any(entry => entry.WorldId == worldId && entry.Button.ButtonVisible))
                .ToList();
            if (visibleWorldIds.Count == 0)
            {
                return;
            }

            int currentIndex = visibleWorldIds.IndexOf(_selectedWorldId);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = Math.Clamp(currentIndex + delta, 0, visibleWorldIds.Count - 1);
            _selectedWorldId = visibleWorldIds[nextIndex];
            if (delta > 0 &&
                _viewAllEnabled &&
                (nextIndex / ClientWorldSlotColumnCount) >= ClientWorldSlotRowCount - 1)
            {
                _focusViewAllButton = true;
            }

            UpdateButtonStates();
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
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
        private readonly Texture2D _gaugeTexture;
        private readonly IReadOnlyList<Texture2D> _selectionFrames;
        private readonly int _selectionFrameDelayMs;
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
        private bool _hasAdultAccess;
        private bool _requestAllowed = true;
        private string _statusMessage;
        private KeyboardState _previousKeyboardState;

        public ChannelSelectWindow(
            IDXObject frame,
            Texture2D overlayTexture2,
            Point overlayOffset2,
            Texture2D overlayTexture3,
            Point overlayOffset3,
            Texture2D highlightTexture,
            Texture2D gaugeTexture,
            IReadOnlyList<Texture2D> selectionFrames,
            int selectionFrameDelayMs,
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
            _gaugeTexture = gaugeTexture;
            _selectionFrames = selectionFrames ?? Array.Empty<Texture2D>();
            _selectionFrameDelayMs = Math.Max(1, selectionFrameDelayMs);
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
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible;

        public event Action<int, int> ChangeRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                _previousKeyboardState = Keyboard.GetState();
            }
        }

        public void Configure(
            int selectedWorldId,
            int currentWorldId,
            int currentChannelIndex,
            IReadOnlyList<ChannelSelectionState> channelStates,
            bool hasAdultAccess,
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
            _hasAdultAccess = hasAdultAccess;
            _requestAllowed = requestAllowed;
            _statusMessage = statusMessage;

            _selectedChannelIndex = _selectedWorldId == _currentWorldId
                ? Math.Min(_currentChannelIndex, Math.Max(0, _channelCount - 1))
                : FindFirstSelectableChannel();

            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            if (Pressed(keyboardState, Keys.Left))
            {
                MoveSelection(-1);
            }
            else if (Pressed(keyboardState, Keys.Right))
            {
                MoveSelection(1);
            }
            else if (Pressed(keyboardState, Keys.Up))
            {
                MoveSelection(-5);
            }
            else if (Pressed(keyboardState, Keys.Down))
            {
                MoveSelection(5);
            }
            else if (Pressed(keyboardState, Keys.Enter))
            {
                if (CanApplySelection())
                {
                    ChangeRequested?.Invoke(_selectedWorldId, _selectedChannelIndex);
                }
            }
            else if (Pressed(keyboardState, Keys.Escape))
            {
                Hide();
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
                sprite.Draw(badgeTexture, new Vector2(Position.X + 34, Position.Y + 10), Color.White);
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
                        Texture2D selectionFrame = GetSelectionFrame(TickCount);
                        if (selectionFrame != null)
                        {
                            sprite.Draw(selectionFrame, new Vector2(Position.X + entry.Button.X - 2, Position.Y + entry.Button.Y - 3), Color.White);
                        }
                        else
                        {
                            Rectangle rect = new Rectangle(
                                Position.X + entry.Button.X - 2,
                                Position.Y + entry.Button.Y - 1,
                                entry.Button.CanvasSnapshotWidth + 4,
                                entry.Button.CanvasSnapshotHeight + 2);
                            sprite.Draw(_highlightTexture, rect, new Color(82, 123, 214, 130));
                        }
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
                if (selectedState.RequiresAdultAccount)
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        _hasAdultAccess ? "Adult-only channel" : "Adult-only channel: access denied",
                        new Vector2(Position.X + 104, Position.Y + 90),
                        _hasAdultAccess ? new Color(186, 236, 186) : new Color(255, 204, 107));
                }
            }

            foreach (ChannelButtonEntry entry in _channelButtons)
            {
                if (!entry.Button.ButtonVisible || !_channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState state))
                {
                    continue;
                }

                Rectangle gaugeBounds = new Rectangle(
                    Position.X + entry.Button.X + 2,
                    Position.Y + entry.Button.Y + 13,
                    _gaugeTexture?.Width ?? 56,
                    _gaugeTexture?.Height ?? 6);
                DrawGauge(sprite, gaugeBounds, state.OccupancyPercent, state.Availability);
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
                         state.RequiresAdultAccount &&
                         !_hasAdultAccess)
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        "Adult",
                        new Vector2(Position.X + entry.Button.X + 29, Position.Y + entry.Button.Y + 1),
                        new Color(255, 204, 107));
                }
                else if (_channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState availabilityState) &&
                         availabilityState.Availability != SelectorAvailability.Available)
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        availabilityState.Availability.ToString(),
                        new Vector2(Position.X + entry.Button.X + 29, Position.Y + entry.Button.Y + 1),
                        SelectorWindowDrawing.GetAvailabilityColor(availabilityState.Availability));
                }
            }
        }

        private void SelectChannel(int channelIndex)
        {
            if (!_requestAllowed)
            {
                return;
            }

            if (_channelStates.TryGetValue(channelIndex, out ChannelSelectionState state) && !state.CanSelect(_hasAdultAccess))
            {
                return;
            }

            _selectedChannelIndex = channelIndex;
            UpdateButtonStates();
        }

        private void MoveSelection(int delta)
        {
            List<int> visibleChannels = _channelButtons
                .Where(entry => entry.Button.ButtonVisible)
                .Select(entry => entry.ChannelIndex)
                .OrderBy(index => index)
                .ToList();
            if (visibleChannels.Count == 0)
            {
                return;
            }

            int currentIndex = visibleChannels.IndexOf(_selectedChannelIndex);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = Math.Clamp(currentIndex + Math.Sign(delta == 0 ? 1 : delta), 0, visibleChannels.Count - 1);
            if (Math.Abs(delta) > 1)
            {
                int targetChannel = Math.Clamp(_selectedChannelIndex + delta, visibleChannels.First(), visibleChannels.Last());
                int matchingIndex = visibleChannels.IndexOf(targetChannel);
                if (matchingIndex >= 0)
                {
                    nextIndex = matchingIndex;
                }
            }

            SelectChannel(visibleChannels[nextIndex]);
        }

        private int FindFirstSelectableChannel()
        {
            foreach (ChannelButtonEntry entry in _channelButtons.OrderBy(button => button.ChannelIndex))
            {
                if (_channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState state) && state.CanSelect(_hasAdultAccess))
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
                   state.CanSelect(_hasAdultAccess) &&
                   (_selectedWorldId != _currentWorldId || _selectedChannelIndex != _currentChannelIndex);
        }

        private void UpdateButtonStates()
        {
            foreach (ChannelButtonEntry entry in _channelButtons)
            {
                bool hasState = _channelStates.TryGetValue(entry.ChannelIndex, out ChannelSelectionState state);
                bool visible = hasState && state.Capacity > 0;
                bool isSelectable = _requestAllowed && visible && state.CanSelect(_hasAdultAccess);
                entry.Button.SetVisible(visible);
                entry.Button.SetEnabled(isSelectable);
                entry.Button.SetButtonState(entry.ChannelIndex == _selectedChannelIndex
                    ? UIObjectState.Pressed
                    : UIObjectState.Normal);
            }

            _changeButton?.SetEnabled(CanApplySelection());
        }

        private Texture2D GetSelectionFrame(int tickCount)
        {
            if (_selectionFrames == null || _selectionFrames.Count == 0)
            {
                return null;
            }

            int frameIndex = Math.Abs(tickCount / _selectionFrameDelayMs) % _selectionFrames.Count;
            return _selectionFrames[frameIndex];
        }

        private void DrawGauge(SpriteBatch sprite, Rectangle gaugeBounds, int occupancyPercent, SelectorAvailability availability)
        {
            if (_gaugeTexture != null)
            {
                sprite.Draw(_gaugeTexture, gaugeBounds, new Color(44, 50, 58, 170));

                int fillWidth = Math.Clamp(
                    (int)Math.Round(gaugeBounds.Width * Math.Clamp(occupancyPercent, 0, 100) / 100f),
                    0,
                    gaugeBounds.Width);
                if (fillWidth > 0)
                {
                    sprite.Draw(
                        _gaugeTexture,
                        new Rectangle(gaugeBounds.X, gaugeBounds.Y, fillWidth, gaugeBounds.Height),
                        new Rectangle(0, 0, fillWidth, _gaugeTexture.Height),
                        SelectorWindowDrawing.GetAvailabilityColor(availability));
                }

                return;
            }

            sprite.Draw(_highlightTexture, gaugeBounds, new Color(28, 34, 46, 180));
            int fallbackWidth = Math.Max(1, (int)Math.Round(gaugeBounds.Width * Math.Clamp(occupancyPercent, 0, 100) / 100f));
            sprite.Draw(
                _highlightTexture,
                new Rectangle(gaugeBounds.X, gaugeBounds.Y, fallbackWidth, gaugeBounds.Height),
                SelectorWindowDrawing.GetAvailabilityColor(availability));
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }
    }

    public sealed class ChannelShiftWindow : UIWindowBase
    {
        private readonly Texture2D _overlayTexture2;
        private readonly Point _overlayOffset2;
        private readonly Texture2D _overlayTexture3;
        private readonly Point _overlayOffset3;
        private readonly Dictionary<int, Texture2D> _worldBadges;
        private readonly Dictionary<int, Texture2D> _channelIcons;
        private readonly IReadOnlyList<Texture2D> _selectionFrames;
        private readonly int _selectionFrameDelayMs;
        private SpriteFont _font;
        private int _worldId;
        private int _currentChannelIndex;
        private int _channelIndex;
        private int _channelCount;
        private int _hideAtTick;

        public ChannelShiftWindow(
            IDXObject frame,
            Texture2D overlayTexture2,
            Point overlayOffset2,
            Texture2D overlayTexture3,
            Point overlayOffset3,
            Dictionary<int, Texture2D> worldBadges,
            Dictionary<int, Texture2D> channelIcons,
            IReadOnlyList<Texture2D> selectionFrames,
            int selectionFrameDelayMs)
            : base(frame)
        {
            _overlayTexture2 = overlayTexture2;
            _overlayOffset2 = overlayOffset2;
            _overlayTexture3 = overlayTexture3;
            _overlayOffset3 = overlayOffset3;
            _worldBadges = worldBadges ?? new Dictionary<int, Texture2D>();
            _channelIcons = channelIcons ?? new Dictionary<int, Texture2D>();
            _selectionFrames = selectionFrames ?? Array.Empty<Texture2D>();
            _selectionFrameDelayMs = Math.Max(1, selectionFrameDelayMs);
        }

        public override string WindowName => MapSimulatorWindowNames.ChannelShift;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void BeginShift(int worldId, int currentChannelIndex, int channelIndex, int channelCount, int durationMs = 1200)
        {
            _worldId = worldId;
            _currentChannelIndex = Math.Max(0, currentChannelIndex);
            _channelIndex = channelIndex;
            _channelCount = Math.Max(0, Math.Min(channelCount, _channelIcons.Count));
            _hideAtTick = Environment.TickCount + Math.Max(1, durationMs);
            Show();
        }

        public void BeginShift(int worldId, int channelIndex, int durationMs = 1200)
        {
            BeginShift(worldId, channelIndex, channelIndex, _channelIcons.Count, durationMs);
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

            if (_worldBadges.TryGetValue(_worldId, out Texture2D badgeTexture))
            {
                float badgeY = Position.Y + 40f - (badgeTexture.Height / 2f);
                sprite.Draw(badgeTexture, new Vector2(Position.X + 16, badgeY), Color.White);
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

            int channelCount = _channelCount > 0
                ? _channelCount
                : _channelIcons.Count;
            for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
            {
                if (!_channelIcons.TryGetValue(channelIndex, out Texture2D channelTexture) || channelTexture == null)
                {
                    continue;
                }

                Rectangle rowBounds = GetChannelRowBounds(channelIndex);
                sprite.Draw(channelTexture, new Vector2(Position.X + rowBounds.X, Position.Y + rowBounds.Y), Color.White);

                if (channelIndex == _channelIndex)
                {
                    Texture2D selectionFrame = GetSelectionFrame(TickCount);
                    if (selectionFrame != null)
                    {
                        sprite.Draw(selectionFrame, new Vector2(Position.X + rowBounds.X - 2, Position.Y + rowBounds.Y - 3), Color.White);
                    }
                }

                if (_font == null)
                {
                    continue;
                }

                if (channelIndex == _currentChannelIndex)
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        "Current",
                        new Vector2(Position.X + rowBounds.X + 29, Position.Y + rowBounds.Y + 1),
                        new Color(255, 228, 151));
                }
                else if (channelIndex == _channelIndex)
                {
                    SelectorWindowDrawing.DrawShadowedText(
                        sprite,
                        _font,
                        "Next",
                        new Vector2(Position.X + rowBounds.X + 33, Position.Y + rowBounds.Y + 1),
                        new Color(163, 226, 255));
                }
            }
        }

        private Texture2D GetSelectionFrame(int tickCount)
        {
            if (_selectionFrames == null || _selectionFrames.Count == 0)
            {
                return null;
            }

            int frameIndex = Math.Abs(tickCount / _selectionFrameDelayMs) % _selectionFrames.Count;
            return _selectionFrames[frameIndex];
        }

        private static Rectangle GetChannelRowBounds(int channelIndex)
        {
            int x = 11 + (70 * (channelIndex % 5));
            int y = 55 + (20 * (channelIndex / 5));
            return new Rectangle(x, y, 68, 20);
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

            ClientTextDrawing.DrawShadowed(sprite, text, position, color, font);
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

            string worldStateLabel = state.WorldState switch
            {
                >= 2 => "World full",
                1 => "World busy",
                _ => null,
            };

            if (state.BlocksCharacterCreation)
            {
                return !string.IsNullOrWhiteSpace(worldStateLabel)
                    ? $"{worldStateLabel}; character creation blocked"
                    : "Character creation blocked";
            }

            if (state.IsLatestConnected && state.IsRecommended)
            {
                return !string.IsNullOrWhiteSpace(worldStateLabel)
                    ? $"Latest connected and recommended; {worldStateLabel.ToLowerInvariant()}"
                    : "Latest connected and recommended";
            }

            if (state.IsLatestConnected)
            {
                if (!string.IsNullOrWhiteSpace(worldStateLabel))
                {
                    return state.HasAdultChannels
                        ? $"Latest connected; {worldStateLabel.ToLowerInvariant()}; adult channels"
                        : $"Latest connected; {worldStateLabel.ToLowerInvariant()}";
                }

                return state.HasAdultChannels
                    ? "Latest connected world with adult channels"
                    : "Latest connected world";
            }

            if (state.IsRecommended)
            {
                if (!string.IsNullOrWhiteSpace(worldStateLabel))
                {
                    return state.HasAdultChannels
                        ? $"Recommended; {worldStateLabel.ToLowerInvariant()}; adult channels"
                        : $"Recommended; {worldStateLabel.ToLowerInvariant()}";
                }

                return state.HasAdultChannels
                    ? "Recommended world with adult channels"
                    : "Recommended world";
            }

            if (!string.IsNullOrWhiteSpace(worldStateLabel))
            {
                return state.HasAdultChannels
                    ? $"{worldStateLabel}; adult channels"
                    : worldStateLabel;
            }

            if (state.HasAdultChannels)
            {
                return "Contains adult-only channels";
            }

            return null;
        }
    }
}
