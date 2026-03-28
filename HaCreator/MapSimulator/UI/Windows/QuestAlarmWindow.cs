using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
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
    internal sealed class QuestAlarmWindow : UIWindowBase
    {
        private const int MaxVisibleEntries = 5;
        private const int DeleteButtonSize = 11;
        private const int RowDoubleClickWindowMs = 450;
        private const int HeaderActionMargin = 6;
        private const int HeaderActionSpacing = 4;
        private const int ClientTitleX = 10;
        private const int ClientTitleY = 25;
        private const int ClientTitleHeight = 18;
        private const int ClientDetailWidth = 150;
        private const int ClientDetailIndent = 10;
        private const int ClientDetailLabelWidth = 32;
        private const int ClientDetailTextInset = 6;
        private const int ClientDetailIconSize = 14;
        private const int ClientDetailValueGap = 6;
        private const int ClientDetailRowGap = 1;
        private const int ClientDetailPaddingTop = 3;
        private const int ClientDetailPaddingBottom = 4;
        private const float TitleScale = 0.5f;
        private const float DetailScale = 0.42f;
        private const float HeaderScale = 0.48f;
        private const float HeaderActionScale = 0.4f;
        private const float PageScale = 0.38f;

        private readonly string _windowName;
        private readonly GraphicsDevice _device;
        private readonly Texture2D _maxTopTexture;
        private readonly Texture2D _centerTexture;
        private readonly Texture2D _bottomTexture;
        private readonly IDXObject _minimizedFrame;
        private readonly Dictionary<int, IDXObject> _maximizedFrames = new();
        private readonly HashSet<int> _trackedQuestIds = new();
        private readonly HashSet<int> _hiddenAutoQuestIds = new();
        private readonly List<RowLayout> _rowLayouts = new();
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private readonly Texture2D _pixel;

        private SpriteFont _font;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private Func<QuestAlarmSnapshot> _snapshotProvider;
        private Func<int, Texture2D> _itemIconProvider;
        private Func<CharacterBuild> _characterBuildProvider;
        private QuestAlarmStore _stateStore;
        private UIObject _autoButton;
        private UIObject _questButton;
        private UIObject _maximizeButton;
        private UIObject _minimizeButton;
        private int _selectedQuestId = -1;
        private bool _isMinimized;
        private bool _autoTrackEnabled;
        private int _hoveredDeleteQuestId = -1;
        private int _pressedDeleteQuestId = -1;
        private bool _pressedDeleteAll;
        private int _lastRowClickQuestId = -1;
        private long _lastRowClickTick;
        private int _scrollOffset;
        private Rectangle _deleteAllBounds = Rectangle.Empty;

        private Texture2D _selectionBarTexture;
        private Texture2D _incompleteSelectionBarTexture;
        private Texture2D _progressFrameTexture;
        private Texture2D _progressGaugeTexture;
        private Texture2D _progressSpotTexture;
        private Texture2D _deleteNormalTexture;
        private Texture2D _deletePressedTexture;
        private Texture2D _deleteDisabledTexture;
        private Texture2D _deleteMouseOverTexture;
        private IReadOnlyList<Texture2D> _questButtonAnimationFrames = Array.Empty<Texture2D>();
        private string _loadedStateCharacterKey = string.Empty;
        private bool _suppressStatePersistence;

        public QuestAlarmWindow(
            string windowName,
            GraphicsDevice device,
            Texture2D maxTopTexture,
            Texture2D centerTexture,
            Texture2D bottomTexture,
            Texture2D minimizedTexture)
            : base(new DXObject(0, 0, minimizedTexture, 0))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _maxTopTexture = maxTopTexture ?? throw new ArgumentNullException(nameof(maxTopTexture));
            _centerTexture = centerTexture ?? throw new ArgumentNullException(nameof(centerTexture));
            _bottomTexture = bottomTexture ?? throw new ArgumentNullException(nameof(bottomTexture));
            _minimizedFrame = new DXObject(0, 0, minimizedTexture ?? throw new ArgumentNullException(nameof(minimizedTexture)), 0);
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });

            RefreshFrame(new QuestAlarmSnapshot());
            UpdateButtonStates();
        }

        public override string WindowName => _windowName;

        internal event Action<int> QuestRequested;
        internal event Action<int, bool> QuestLogRequested;

        internal void SetSnapshotProvider(Func<QuestAlarmSnapshot> provider)
        {
            _snapshotProvider = provider;
        }

        internal void SetItemIconProvider(Func<int, Texture2D> provider)
        {
            _itemIconProvider = provider;
        }

        internal void ConfigurePersistence(QuestAlarmStore stateStore, Func<CharacterBuild> characterBuildProvider)
        {
            _stateStore = stateStore;
            _characterBuildProvider = characterBuildProvider;
            _loadedStateCharacterKey = string.Empty;
        }

        internal void SetQuestChromeTextures(
            Texture2D selectionBarTexture,
            Texture2D incompleteSelectionBarTexture,
            Texture2D progressFrameTexture,
            Texture2D progressGaugeTexture,
            Texture2D progressSpotTexture,
            Texture2D deleteNormalTexture,
            Texture2D deletePressedTexture,
            Texture2D deleteDisabledTexture,
            Texture2D deleteMouseOverTexture,
            IReadOnlyList<Texture2D> questButtonAnimationFrames)
        {
            _selectionBarTexture = selectionBarTexture;
            _incompleteSelectionBarTexture = incompleteSelectionBarTexture;
            _progressFrameTexture = progressFrameTexture;
            _progressGaugeTexture = progressGaugeTexture;
            _progressSpotTexture = progressSpotTexture;
            _deleteNormalTexture = deleteNormalTexture;
            _deletePressedTexture = deletePressedTexture;
            _deleteDisabledTexture = deleteDisabledTexture;
            _deleteMouseOverTexture = deleteMouseOverTexture;
            _questButtonAnimationFrames = questButtonAnimationFrames ?? Array.Empty<Texture2D>();
        }

        internal void InitializeControls(
            UIObject autoButton,
            UIObject questButton,
            UIObject maximizeButton,
            UIObject minimizeButton,
            UIObject deleteButton)
        {
            _autoButton = autoButton;
            _questButton = questButton;
            _maximizeButton = maximizeButton;
            _minimizeButton = minimizeButton;

            ConfigureButton(_autoButton, 102, 4, ToggleAutoTrack);
            ConfigureButton(_questButton, 136, 4, OpenQuestLog);
            ConfigureButton(_maximizeButton, 150, 4, () => SetMinimized(false));
            ConfigureButton(_minimizeButton, 150, 4, () => SetMinimized(true));

            if (deleteButton != null)
            {
                deleteButton.SetVisible(false);
            }

            UpdateButtonStates();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void TrackQuest(int questId)
        {
            if (questId <= 0)
            {
                return;
            }

            EnsurePersistedStateLoaded();
            _trackedQuestIds.Add(questId);
            _hiddenAutoQuestIds.Remove(questId);
            _selectedQuestId = questId;
            EnsureSelectionVisible(GetFilteredSnapshot());
            SetMinimized(false);
            UpdateButtonStates();
            SavePersistedState();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            QuestAlarmSnapshot snapshot = GetFilteredSnapshot();
            EnsureSelection(snapshot);
            EnsureSelectionVisible(snapshot);
            ClampScrollOffset(snapshot);
            RefreshFrame(snapshot);
            UpdateButtonStates();

            MouseState mouseState = Mouse.GetState();
            KeyboardState keyboardState = Keyboard.GetState();
            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;

            if (!_isMinimized && wheelDelta != 0 && ContainsPoint(mouseState.X, mouseState.Y))
            {
                ScrollEntries(wheelDelta > 0 ? -1 : 1, snapshot);
            }

            if (!_isMinimized && ContainsPoint(mouseState.X, mouseState.Y))
            {
                _hoveredDeleteQuestId = GetDeleteQuestIdAtPoint(mouseState.X, mouseState.Y);
                if (leftPressed)
                {
                    _pressedDeleteAll = _deleteAllBounds.Contains(mouseState.X, mouseState.Y);
                    _pressedDeleteQuestId = _hoveredDeleteQuestId;
                }

                if (leftReleased)
                {
                    if (_pressedDeleteAll && _deleteAllBounds.Contains(mouseState.X, mouseState.Y))
                    {
                        DismissAll(snapshot);
                    }
                    else if (_pressedDeleteQuestId > 0 && _pressedDeleteQuestId == _hoveredDeleteQuestId)
                    {
                        DismissQuest(_pressedDeleteQuestId);
                    }
                    else
                    {
                        HandleRowSelection(mouseState.X, mouseState.Y);
                    }

                    _pressedDeleteQuestId = -1;
                    _pressedDeleteAll = false;
                }
            }
            else
            {
                _hoveredDeleteQuestId = -1;
                if (leftReleased)
                {
                    _pressedDeleteQuestId = -1;
                    _pressedDeleteAll = false;
                }
            }

            HandleKeyboardInput(keyboardState, snapshot);
            _previousMouseState = mouseState;
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
            if (_font == null)
            {
                return;
            }

            QuestAlarmSnapshot snapshot = GetFilteredSnapshot();
            EnsureSelection(snapshot);
            EnsureSelectionVisible(snapshot);
            ClampScrollOffset(snapshot);
            RefreshFrame(snapshot);

            Vector2 titlePosition = new Vector2(Position.X + 19, Position.Y + 5);
            string title = $"Quest Alarm ({snapshot.Entries.Count})";
            sprite.DrawString(_font, title, titlePosition, Color.White, 0f, Vector2.Zero, HeaderScale, SpriteEffects.None, 0f);
            DrawHeaderActions(sprite, snapshot);

            if (snapshot.HasAlertAnimation)
            {
                DrawQuestButtonAnimation(sprite, TickCount);
            }

            if (_isMinimized)
            {
                return;
            }

            _rowLayouts.Clear();
            if (snapshot.Entries.Count == 0)
            {
                sprite.DrawString(_font, "No active quests are being tracked.", new Vector2(Position.X + 8, Position.Y + 31), new Color(222, 224, 231), 0f, Vector2.Zero, 0.46f, SpriteEffects.None, 0f);
                return;
            }

            int y = Position.Y + ClientTitleY;
            IReadOnlyList<QuestAlarmEntrySnapshot> visibleEntries = snapshot.Entries
                .Skip(_scrollOffset)
                .Take(MaxVisibleEntries)
                .ToList();

            for (int i = 0; i < visibleEntries.Count; i++)
            {
                QuestAlarmEntrySnapshot entry = visibleEntries[i];
                bool isSelected = entry.QuestId == _selectedQuestId;
                int rowHeight = GetRowHeight(entry, isSelected);

                Rectangle rowRect = new Rectangle(Position.X + 6, y, _maxTopTexture.Width - 12, rowHeight);
                Rectangle titleRect = new Rectangle(rowRect.X, rowRect.Y, rowRect.Width, ClientTitleHeight);
                Rectangle deleteRect = new Rectangle(titleRect.Right - DeleteButtonSize - 4, titleRect.Y + 3, DeleteButtonSize, DeleteButtonSize);
                _rowLayouts.Add(new RowLayout(entry.QuestId, rowRect, deleteRect));

                DrawRowBackground(sprite, titleRect, entry, isSelected);

                Color titleColor = entry.IsReadyToComplete
                    ? new Color(150, 241, 142)
                    : entry.IsRecentlyUpdated
                        ? new Color(151, 221, 255)
                        : new Color(255, 228, 153);
                sprite.DrawString(_font, Truncate(entry.Title, 26), new Vector2(Position.X + ClientTitleX, titleRect.Y + 1), titleColor, 0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);

                string progressText = entry.TotalProgress > 0
                    ? $"{Math.Min(entry.CurrentProgress, entry.TotalProgress)}/{entry.TotalProgress}"
                    : entry.StatusText;
                Vector2 progressSize = _font.MeasureString(progressText) * DetailScale;
                sprite.DrawString(_font, progressText, new Vector2(deleteRect.X - progressSize.X - 6, titleRect.Y + 3), new Color(214, 220, 229), 0f, Vector2.Zero, DetailScale, SpriteEffects.None, 0f);

                DrawInlineDelete(sprite, deleteRect, entry.QuestId);

                if (isSelected)
                {
                    Rectangle detailRect = new Rectangle(Position.X + ClientDetailIndent, titleRect.Bottom, ClientDetailWidth, Math.Max(0, rowRect.Bottom - titleRect.Bottom));
                    DrawSelectedDetail(sprite, detailRect, entry);
                }

                y += rowHeight;
            }

            if (snapshot.Entries.Count > visibleEntries.Count)
            {
                int pageEnd = Math.Min(snapshot.Entries.Count, _scrollOffset + visibleEntries.Count);
                sprite.DrawString(
                    _font,
                    $"{_scrollOffset + 1}-{pageEnd}/{snapshot.Entries.Count}",
                    new Vector2(Position.X + 8, y + 2),
                    new Color(214, 218, 226),
                    0f,
                    Vector2.Zero,
                    DetailScale,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void ConfigureButton(UIObject button, int x, int y, Action onClick)
        {
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            AddButton(button);
            if (onClick != null)
            {
                button.ButtonClickReleased += _ => onClick();
            }
        }

        private void ToggleAutoTrack()
        {
            EnsurePersistedStateLoaded();
            _autoTrackEnabled = !_autoTrackEnabled;
            UpdateButtonStates();
            SavePersistedState();
        }

        private void OpenQuestLog()
        {
            QuestAlarmSnapshot snapshot = GetFilteredSnapshot();
            QuestLogRequested?.Invoke(_selectedQuestId, snapshot.HasAlertAnimation);
        }

        private void SetMinimized(bool minimized)
        {
            _isMinimized = minimized;
            RefreshFrame(GetFilteredSnapshot());
            UpdateButtonStates();
            SavePersistedState();
        }

        private void HandleRowSelection(int mouseX, int mouseY)
        {
            for (int i = 0; i < _rowLayouts.Count; i++)
            {
                RowLayout row = _rowLayouts[i];
                if (!row.Bounds.Contains(mouseX, mouseY) || row.DeleteBounds.Contains(mouseX, mouseY))
                {
                    continue;
                }

                bool repeatedClick = row.QuestId == _selectedQuestId
                    && row.QuestId == _lastRowClickQuestId
                    && Environment.TickCount64 - _lastRowClickTick <= RowDoubleClickWindowMs;

                _selectedQuestId = row.QuestId;
                _lastRowClickQuestId = row.QuestId;
                _lastRowClickTick = Environment.TickCount64;
                EnsureSelectionVisible(GetFilteredSnapshot());
                UpdateButtonStates();

                if (repeatedClick)
                {
                    QuestRequested?.Invoke(row.QuestId);
                }

                return;
            }
        }

        private QuestAlarmSnapshot GetFilteredSnapshot()
        {
            EnsurePersistedStateLoaded();
            QuestAlarmSnapshot snapshot = _snapshotProvider?.Invoke() ?? new QuestAlarmSnapshot();
            if (snapshot.Entries.Count == 0)
            {
                if (_trackedQuestIds.Count > 0 || _hiddenAutoQuestIds.Count > 0)
                {
                    _trackedQuestIds.Clear();
                    _hiddenAutoQuestIds.Clear();
                    SavePersistedState();
                }

                return snapshot;
            }

            HashSet<int> activeQuestIds = snapshot.Entries.Select(entry => entry.QuestId).ToHashSet();
            bool stateChanged = false;
            stateChanged |= _trackedQuestIds.RemoveWhere(questId => !activeQuestIds.Contains(questId)) > 0;
            stateChanged |= _hiddenAutoQuestIds.RemoveWhere(questId => !activeQuestIds.Contains(questId)) > 0;

            List<QuestAlarmEntrySnapshot> entries = snapshot.Entries
                .Where(entry => ShouldDisplayEntry(entry.QuestId))
                .ToList();

            if (stateChanged)
            {
                SavePersistedState();
            }

            return new QuestAlarmSnapshot
            {
                Entries = entries,
                HasAlertAnimation = entries.Any(entry => entry.IsRecentlyUpdated)
            };
        }

        private void EnsureSelection(QuestAlarmSnapshot snapshot)
        {
            if (snapshot.Entries.Count == 0)
            {
                _selectedQuestId = -1;
                _scrollOffset = 0;
                return;
            }

            if (snapshot.Entries.Any(entry => entry.QuestId == _selectedQuestId))
            {
                return;
            }

            QuestAlarmEntrySnapshot preferredEntry = snapshot.Entries.FirstOrDefault(entry => entry.IsReadyToComplete)
                ?? snapshot.Entries.FirstOrDefault(entry => entry.IsRecentlyUpdated)
                ?? snapshot.Entries[0];
            _selectedQuestId = preferredEntry.QuestId;
        }

        private void EnsureSelectionVisible(QuestAlarmSnapshot snapshot)
        {
            if (_selectedQuestId <= 0 || snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                return;
            }

            int selectedIndex = -1;
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                if (snapshot.Entries[i].QuestId == _selectedQuestId)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                return;
            }

            if (selectedIndex < _scrollOffset)
            {
                _scrollOffset = selectedIndex;
            }
            else if (selectedIndex >= _scrollOffset + MaxVisibleEntries)
            {
                _scrollOffset = selectedIndex - MaxVisibleEntries + 1;
            }
        }

        private void RefreshFrame(QuestAlarmSnapshot snapshot)
        {
            if (_isMinimized)
            {
                Frame = _minimizedFrame;
                return;
            }

            snapshot ??= new QuestAlarmSnapshot();
            ClampScrollOffset(snapshot);
            int displayedEntries = Math.Max(1, Math.Min(MaxVisibleEntries, snapshot.Entries.Count));
            int contentHeight = 16;
            if (snapshot.Entries.Count == 0)
            {
                contentHeight += 20;
            }
            else
            {
                IReadOnlyList<QuestAlarmEntrySnapshot> visibleEntries = snapshot.Entries
                    .Skip(_scrollOffset)
                    .Take(displayedEntries)
                    .ToList();
                for (int i = 0; i < visibleEntries.Count; i++)
                {
                    contentHeight += GetRowHeight(visibleEntries[i], visibleEntries[i].QuestId == _selectedQuestId);
                }

                if (snapshot.Entries.Count > visibleEntries.Count)
                {
                    contentHeight += 18;
                }
            }

            int bodyHeight = Math.Max(_centerTexture.Height, contentHeight + 8);
            int centerRepeats = Math.Max(1, (int)Math.Ceiling(bodyHeight / (double)_centerTexture.Height));
            if (!_maximizedFrames.TryGetValue(centerRepeats, out IDXObject frame))
            {
                Texture2D texture = BuildFrameTexture(_device, _maxTopTexture, _centerTexture, _bottomTexture, centerRepeats);
                frame = new DXObject(0, 0, texture, 0);
                _maximizedFrames[centerRepeats] = frame;
            }

            Frame = frame;
        }

        private void UpdateButtonStates()
        {
            _autoButton?.SetVisible(!_isMinimized);
            _questButton?.SetVisible(true);
            _maximizeButton?.SetVisible(_isMinimized);
            _minimizeButton?.SetVisible(!_isMinimized);

            if (_autoButton != null)
            {
                _autoButton.SetButtonState(_autoTrackEnabled ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            if (_questButton != null)
            {
                _questButton.SetButtonState(UIObjectState.Normal);
            }
        }

        private void DrawRowBackground(SpriteBatch sprite, Rectangle rowRect, QuestAlarmEntrySnapshot entry, bool isSelected)
        {
            Color background = isSelected
                ? new Color(34, 63, 101, 208)
                : new Color(7, 14, 20, 196);
            sprite.Draw(_pixel, rowRect, background);

            if (_selectionBarTexture != null && isSelected)
            {
                DrawTextureStrip(sprite, _selectionBarTexture, new Rectangle(rowRect.X + 1, rowRect.Y + 3, Math.Min(rowRect.Width - 2, ClientDetailWidth), _selectionBarTexture.Height), Color.White * 0.82f);
            }

            Color borderColor = entry.IsReadyToComplete
                ? new Color(115, 197, 104, 170)
                : entry.IsRecentlyUpdated
                    ? new Color(83, 154, 219, 170)
                    : new Color(77, 90, 102, 150);
            DrawBorder(sprite, rowRect, borderColor);
        }

        private void DrawHeaderActions(SpriteBatch sprite, QuestAlarmSnapshot snapshot)
        {
            if (_font == null)
            {
                _deleteAllBounds = Rectangle.Empty;
                return;
            }

            string deleteAllLabel = "Delete all";
            Vector2 labelSize = _font.MeasureString(deleteAllLabel) * HeaderActionScale;
            int x = Position.X + _maxTopTexture.Width - HeaderActionMargin - (int)Math.Ceiling(labelSize.X);
            int y = Position.Y + 7;
            _deleteAllBounds = new Rectangle(x - 2, y - 1, (int)Math.Ceiling(labelSize.X) + 4, (int)Math.Ceiling(labelSize.Y) + 2);

            Color deleteAllColor = snapshot.Entries.Count == 0
                ? new Color(114, 121, 133)
                : _pressedDeleteAll
                    ? new Color(255, 205, 137)
                    : new Color(223, 228, 238);
            sprite.DrawString(_font, deleteAllLabel, new Vector2(x, y), deleteAllColor, 0f, Vector2.Zero, HeaderActionScale, SpriteEffects.None, 0f);

            if (_scrollOffset > 0 || snapshot.Entries.Count > MaxVisibleEntries)
            {
                string pageLabel = $"{_scrollOffset + 1}-{Math.Min(snapshot.Entries.Count, _scrollOffset + MaxVisibleEntries)}";
                Vector2 pageSize = _font.MeasureString(pageLabel) * PageScale;
                float pageX = x - HeaderActionSpacing - pageSize.X;
                sprite.DrawString(_font, pageLabel, new Vector2(pageX, Position.Y + 7), new Color(166, 176, 192), 0f, Vector2.Zero, PageScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawSelectedDetail(SpriteBatch sprite, Rectangle detailRect, QuestAlarmEntrySnapshot entry)
        {
            if (detailRect.Height <= 0)
            {
                return;
            }

            sprite.Draw(_pixel, detailRect, new Color(4, 9, 13, 172));
            DrawBorder(sprite, detailRect, entry.IsReadyToComplete
                ? new Color(115, 197, 104, 128)
                : entry.IsRecentlyUpdated
                    ? new Color(83, 154, 219, 128)
                    : new Color(77, 90, 102, 112));

            int currentY = detailRect.Y + ClientDetailPaddingTop;
            DrawProgressGauge(sprite, detailRect.X + 1, currentY, Math.Min(ClientDetailWidth - 2, 150), entry.ProgressRatio);
            currentY += Math.Max(ClientTitleHeight, _progressFrameTexture?.Height ?? 13) + ClientDetailRowGap;

            foreach (QuestLogLineSnapshot line in entry.RequirementLines ?? Array.Empty<QuestLogLineSnapshot>())
            {
                currentY = DrawRequirementLine(sprite, detailRect.X, detailRect.Right, currentY, line);
            }

            foreach (string line in WrapText(entry.DemandText, ClientDetailWidth - 6, DetailScale))
            {
                sprite.DrawString(_font, line, new Vector2(detailRect.X + 2, currentY), new Color(201, 207, 221), 0f, Vector2.Zero, DetailScale, SpriteEffects.None, 0f);
                currentY += ClientTitleHeight;
            }
        }

        private int DrawRequirementLine(SpriteBatch sprite, int left, int right, int y, QuestLogLineSnapshot line)
        {
            if (line == null)
            {
                return y;
            }

            Texture2D rowTexture = line.IsComplete
                ? _selectionBarTexture
                : _incompleteSelectionBarTexture ?? _selectionBarTexture;
            int detailLeft = left + ClientDetailLabelWidth;
            int availableWidth = Math.Max(48, right - detailLeft);
            int stripWidth = Math.Min(availableWidth, rowTexture?.Width ?? availableWidth);
            if (rowTexture != null)
            {
                DrawTextureStrip(sprite, rowTexture, new Rectangle(detailLeft, y + 3, stripWidth, rowTexture.Height), Color.White);
            }

            Color labelColor = line.IsComplete ? new Color(168, 224, 173) : new Color(255, 190, 137);
            Color textColor = line.IsComplete ? new Color(219, 239, 219) : new Color(255, 218, 189);
            sprite.DrawString(_font, line.Label ?? string.Empty, new Vector2(left, y + 1), labelColor, 0f, Vector2.Zero, DetailScale, SpriteEffects.None, 0f);

            int iconOffset = 0;
            if (line.ItemId.HasValue)
            {
                Texture2D icon = GetItemIcon(line.ItemId.Value);
                if (icon != null)
                {
                    Rectangle iconBounds = new Rectangle(detailLeft + ClientDetailTextInset, y + 2, ClientDetailIconSize, ClientDetailIconSize);
                    sprite.Draw(icon, iconBounds, Color.White);
                    iconOffset = ClientDetailIconSize + 4;
                }
            }

            string bodyText = Truncate(line.Text, 28);
            Vector2 valueSize = string.IsNullOrWhiteSpace(line.ValueText)
                ? Vector2.Zero
                : _font.MeasureString(line.ValueText) * DetailScale;
            float bodyX = detailLeft + ClientDetailTextInset + iconOffset;
            float bodyRight = detailLeft + stripWidth - ClientDetailTextInset - valueSize.X - (valueSize.X > 0 ? ClientDetailValueGap : 0);
            float maxBodyWidth = Math.Max(28f, bodyRight - bodyX);
            bodyText = TruncateToWidth(bodyText, maxBodyWidth, DetailScale);
            sprite.DrawString(_font, bodyText, new Vector2(bodyX, y + 1), textColor, 0f, Vector2.Zero, DetailScale, SpriteEffects.None, 0f);

            if (!string.IsNullOrWhiteSpace(line.ValueText))
            {
                float valueX = Math.Max(bodyX, detailLeft + stripWidth - ClientDetailTextInset - valueSize.X);
                sprite.DrawString(_font, line.ValueText, new Vector2(valueX, y + 1), textColor, 0f, Vector2.Zero, DetailScale, SpriteEffects.None, 0f);
            }

            return y + ClientTitleHeight;
        }

        private void DrawProgressGauge(SpriteBatch sprite, int x, int y, int width, float ratio)
        {
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            if (_progressFrameTexture != null)
            {
                DrawTextureStrip(sprite, _progressFrameTexture, new Rectangle(x, y, Math.Min(width, _progressFrameTexture.Width), _progressFrameTexture.Height), Color.White);
            }

            if (_progressGaugeTexture != null)
            {
                int fillWidth = Math.Max(0, width - 3);
                int innerWidth = Math.Max(1, (int)Math.Round(fillWidth * ratio));
                sprite.Draw(_progressGaugeTexture, new Rectangle(x + 2, y + 1, innerWidth, Math.Max(1, _progressGaugeTexture.Height)), Color.White);
                if (_progressSpotTexture != null && innerWidth > 0)
                {
                    sprite.Draw(_progressSpotTexture, new Vector2(Math.Min(x + width - _progressSpotTexture.Width, x + 1 + innerWidth), y + 1), Color.White);
                }
            }
            else
            {
                sprite.Draw(_pixel, new Rectangle(x, y, width, 6), new Color(27, 31, 37, 220));
                sprite.Draw(_pixel, new Rectangle(x + 1, y + 1, Math.Max(1, (int)((width - 2) * ratio)), 4), new Color(132, 195, 99));
            }
        }

        private void DrawInlineDelete(SpriteBatch sprite, Rectangle bounds, int questId)
        {
            Texture2D texture = _deleteNormalTexture;
            if (_pressedDeleteQuestId == questId && _hoveredDeleteQuestId == questId)
            {
                texture = _deletePressedTexture ?? _deleteNormalTexture;
            }
            else if (_hoveredDeleteQuestId == questId)
            {
                texture = _deleteMouseOverTexture ?? _deleteNormalTexture;
            }
            else if (_selectedQuestId != questId)
            {
                texture = _deleteDisabledTexture ?? _deleteNormalTexture;
            }

            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
                return;
            }

            sprite.Draw(_pixel, bounds, new Color(44, 52, 61, 220));
            DrawBorder(sprite, bounds, new Color(170, 170, 170, 180));
        }

        private void DrawQuestButtonAnimation(SpriteBatch sprite, int tickCount)
        {
            if (_questButton == null || !_questButton.ButtonVisible || _questButtonAnimationFrames.Count == 0)
            {
                return;
            }

            Texture2D frame = _questButtonAnimationFrames[(tickCount / 180) % _questButtonAnimationFrames.Count];
            if (frame == null)
            {
                return;
            }

            sprite.Draw(frame, new Vector2(Position.X + _questButton.X, Position.Y + _questButton.Y), Color.White);
        }

        private int GetDeleteQuestIdAtPoint(int mouseX, int mouseY)
        {
            for (int i = 0; i < _rowLayouts.Count; i++)
            {
                if (_rowLayouts[i].DeleteBounds.Contains(mouseX, mouseY))
                {
                    return _rowLayouts[i].QuestId;
                }
            }

            return -1;
        }

        private void DismissQuest(int questId)
        {
            if (questId <= 0)
            {
                return;
            }

            EnsurePersistedStateLoaded();
            _trackedQuestIds.Remove(questId);
            if (_autoTrackEnabled)
            {
                _hiddenAutoQuestIds.Add(questId);
            }

            if (_selectedQuestId == questId)
            {
                _selectedQuestId = -1;
            }

            ClampScrollOffset(GetFilteredSnapshot());
            UpdateButtonStates();
            SavePersistedState();
        }

        private void DismissAll(QuestAlarmSnapshot snapshot)
        {
            if (snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                return;
            }

            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                _trackedQuestIds.Remove(snapshot.Entries[i].QuestId);
                if (_autoTrackEnabled)
                {
                    _hiddenAutoQuestIds.Add(snapshot.Entries[i].QuestId);
                }
            }

            _selectedQuestId = -1;
            _scrollOffset = 0;
            UpdateButtonStates();
            SavePersistedState();
        }

        private void HandleKeyboardInput(KeyboardState keyboardState, QuestAlarmSnapshot snapshot)
        {
            if (_isMinimized || snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                return;
            }

            if (WasPressed(keyboardState, Keys.Up))
            {
                MoveSelection(-1, snapshot);
            }

            if (WasPressed(keyboardState, Keys.Down))
            {
                MoveSelection(1, snapshot);
            }

            if (WasPressed(keyboardState, Keys.PageUp))
            {
                ScrollEntries(-MaxVisibleEntries, snapshot);
            }

            if (WasPressed(keyboardState, Keys.PageDown))
            {
                ScrollEntries(MaxVisibleEntries, snapshot);
            }

            if (WasPressed(keyboardState, Keys.Enter) && _selectedQuestId > 0)
            {
                QuestRequested?.Invoke(_selectedQuestId);
            }

            if (WasPressed(keyboardState, Keys.Delete))
            {
                if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
                {
                    DismissAll(snapshot);
                }
                else if (_selectedQuestId > 0)
                {
                    DismissQuest(_selectedQuestId);
                }
            }
        }

        private void MoveSelection(int direction, QuestAlarmSnapshot snapshot)
        {
            if (snapshot?.Entries == null || snapshot.Entries.Count == 0 || direction == 0)
            {
                return;
            }

            int currentIndex = 0;
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                if (snapshot.Entries[i].QuestId == _selectedQuestId)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = Math.Clamp(currentIndex + direction, 0, snapshot.Entries.Count - 1);
            _selectedQuestId = snapshot.Entries[nextIndex].QuestId;
            EnsureSelectionVisible(snapshot);
            UpdateButtonStates();
        }

        private void ScrollEntries(int delta, QuestAlarmSnapshot snapshot)
        {
            if (snapshot?.Entries == null)
            {
                return;
            }

            int maxOffset = Math.Max(0, snapshot.Entries.Count - MaxVisibleEntries);
            _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, maxOffset);
        }

        private void ClampScrollOffset(QuestAlarmSnapshot snapshot)
        {
            int maxOffset = Math.Max(0, (snapshot?.Entries?.Count ?? 0) - MaxVisibleEntries);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxOffset);
        }

        private int GetRowHeight(QuestAlarmEntrySnapshot entry, bool isSelected)
        {
            int rowHeight = ClientTitleHeight;
            if (!isSelected || entry == null)
            {
                return rowHeight;
            }

            rowHeight += ClientDetailPaddingTop;
            rowHeight += Math.Max(ClientTitleHeight, _progressFrameTexture?.Height ?? 13) + ClientDetailRowGap;
            rowHeight += (entry.RequirementLines?.Count ?? 0) * ClientTitleHeight;
            rowHeight += WrapText(entry.DemandText, ClientDetailWidth - 6, DetailScale).Count() * ClientTitleHeight;
            rowHeight += ClientDetailPaddingBottom;
            return rowHeight;
        }

        private Texture2D GetItemIcon(int itemId)
        {
            if (itemId <= 0)
            {
                return null;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cachedIcon))
            {
                return cachedIcon;
            }

            Texture2D icon = _itemIconProvider?.Invoke(itemId);
            if (icon != null)
            {
                _itemIconCache[itemId] = icon;
            }

            return icon;
        }

        private void DrawBorder(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            sprite.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private void DrawTextureStrip(SpriteBatch sprite, Texture2D texture, Rectangle destination, Color color)
        {
            if (sprite == null || texture == null || destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            Rectangle source = new Rectangle(0, 0, Math.Min(texture.Width, destination.Width), Math.Min(texture.Height, destination.Height));
            Rectangle clippedDestination = new Rectangle(destination.X, destination.Y, source.Width, source.Height);
            sprite.Draw(texture, clippedDestination, source, color);
        }

        private static Texture2D BuildFrameTexture(GraphicsDevice device, Texture2D top, Texture2D center, Texture2D bottom, int centerRepeats)
        {
            int width = top.Width;
            int height = top.Height + (center.Height * centerRepeats) + bottom.Height;

            Color[] topData = new Color[top.Width * top.Height];
            Color[] centerData = new Color[center.Width * center.Height];
            Color[] bottomData = new Color[bottom.Width * bottom.Height];
            top.GetData(topData);
            center.GetData(centerData);
            bottom.GetData(bottomData);

            Color[] data = new Color[width * height];
            Blit(data, width, 0, topData, top.Width, top.Height);
            for (int i = 0; i < centerRepeats; i++)
            {
                Blit(data, width, top.Height + (i * center.Height), centerData, center.Width, center.Height);
            }

            Blit(data, width, height - bottom.Height, bottomData, bottom.Width, bottom.Height);

            Texture2D texture = new Texture2D(device, width, height);
            texture.SetData(data);
            return texture;
        }

        private static void Blit(Color[] destination, int destinationWidth, int destinationY, Color[] source, int sourceWidth, int sourceHeight)
        {
            for (int y = 0; y < sourceHeight; y++)
            {
                Array.Copy(source, y * sourceWidth, destination, (destinationY + y) * destinationWidth, sourceWidth);
            }
        }

        private static string Truncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text ?? string.Empty;
            }

            return $"{text.Substring(0, Math.Max(0, maxChars - 3))}...";
        }

        private string TruncateToWidth(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null || maxWidth <= 0f)
            {
                return string.Empty;
            }

            if (_font.MeasureString(text).X * scale <= maxWidth)
            {
                return text;
            }

            string ellipsis = "...";
            for (int length = Math.Max(0, text.Length - 1); length > 0; length--)
            {
                string candidate = text.Substring(0, length) + ellipsis;
                if (_font.MeasureString(candidate).X * scale <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            foreach (string block in text.Replace("\r", string.Empty).Split('\n'))
            {
                string[] words = block.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    continue;
                }

                string currentLine = string.Empty;
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X * scale > maxWidth)
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
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private bool ShouldDisplayEntry(int questId)
        {
            if (questId <= 0)
            {
                return false;
            }

            if (_trackedQuestIds.Contains(questId))
            {
                return true;
            }

            return _autoTrackEnabled && !_hiddenAutoQuestIds.Contains(questId);
        }

        private void EnsurePersistedStateLoaded()
        {
            string currentCharacterKey = ResolveCharacterKey(_characterBuildProvider?.Invoke());
            if (string.Equals(_loadedStateCharacterKey, currentCharacterKey, StringComparison.Ordinal))
            {
                return;
            }

            _suppressStatePersistence = true;
            try
            {
                QuestAlarmPersistedState state = _stateStore?.GetState(_characterBuildProvider?.Invoke()) ?? new QuestAlarmPersistedState();
                _trackedQuestIds.Clear();
                foreach (int questId in state.TrackedQuestIds)
                {
                    _trackedQuestIds.Add(questId);
                }

                _hiddenAutoQuestIds.Clear();
                foreach (int questId in state.HiddenAutoQuestIds)
                {
                    _hiddenAutoQuestIds.Add(questId);
                }

                _autoTrackEnabled = state.AutoRegisterEnabled;
                _isMinimized = state.IsMinimized;
            }
            finally
            {
                _loadedStateCharacterKey = currentCharacterKey;
                _suppressStatePersistence = false;
            }
        }

        private void SavePersistedState()
        {
            if (_suppressStatePersistence || _stateStore == null)
            {
                return;
            }

            _stateStore.Save(
                _characterBuildProvider?.Invoke(),
                new QuestAlarmPersistedState
                {
                    AutoRegisterEnabled = _autoTrackEnabled,
                    IsMinimized = _isMinimized,
                    TrackedQuestIds = _trackedQuestIds.ToArray(),
                    HiddenAutoQuestIds = _hiddenAutoQuestIds.ToArray()
                });
        }

        private static string ResolveCharacterKey(CharacterBuild build)
        {
            if (build == null)
            {
                return "session:default";
            }

            if (build.Id > 0)
            {
                return $"id:{build.Id}";
            }

            if (!string.IsNullOrWhiteSpace(build.Name))
            {
                return $"name:{build.Name.Trim().ToLowerInvariant()}";
            }

            return "session:default";
        }

        private readonly struct RowLayout
        {
            public RowLayout(int questId, Rectangle bounds, Rectangle deleteBounds)
            {
                QuestId = questId;
                Bounds = bounds;
                DeleteBounds = deleteBounds;
            }

            public int QuestId { get; }
            public Rectangle Bounds { get; }
            public Rectangle DeleteBounds { get; }
        }
    }
}
