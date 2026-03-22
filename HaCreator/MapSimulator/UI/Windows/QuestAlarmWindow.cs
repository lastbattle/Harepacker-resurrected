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
    internal sealed class QuestAlarmWindow : UIWindowBase
    {
        private const int MaxVisibleEntries = 3;
        private const int HeaderRepeats = 1;
        private const int RepeatsPerEntry = 3;
        private const int DeleteButtonSize = 11;
        private const int RowDoubleClickWindowMs = 450;

        private readonly string _windowName;
        private readonly GraphicsDevice _device;
        private readonly Texture2D _maxTopTexture;
        private readonly Texture2D _centerTexture;
        private readonly Texture2D _bottomTexture;
        private readonly IDXObject _minimizedFrame;
        private readonly Dictionary<int, IDXObject> _maximizedFrames = new();
        private readonly HashSet<int> _dismissedQuestIds = new();
        private readonly List<RowLayout> _rowLayouts = new();
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private readonly Texture2D _pixel;

        private SpriteFont _font;
        private MouseState _previousMouseState;
        private Func<QuestAlarmSnapshot> _snapshotProvider;
        private Func<int, Texture2D> _itemIconProvider;
        private UIObject _autoButton;
        private UIObject _questButton;
        private UIObject _maximizeButton;
        private UIObject _minimizeButton;
        private int _selectedQuestId = -1;
        private bool _isMinimized;
        private bool _autoTrackEnabled;
        private int _hoveredDeleteQuestId = -1;
        private int _pressedDeleteQuestId = -1;
        private int _lastRowClickQuestId = -1;
        private long _lastRowClickTick;

        private Texture2D _selectionBarTexture;
        private Texture2D _progressFrameTexture;
        private Texture2D _progressGaugeTexture;
        private Texture2D _progressSpotTexture;
        private Texture2D _deleteNormalTexture;
        private Texture2D _deletePressedTexture;
        private Texture2D _deleteDisabledTexture;
        private Texture2D _deleteMouseOverTexture;
        private IReadOnlyList<Texture2D> _questButtonAnimationFrames = Array.Empty<Texture2D>();

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

            RefreshFrame(0);
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

        internal void SetQuestChromeTextures(
            Texture2D selectionBarTexture,
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

            _dismissedQuestIds.Remove(questId);
            _selectedQuestId = questId;
            _autoTrackEnabled = false;
            SetMinimized(false);
            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            QuestAlarmSnapshot snapshot = GetFilteredSnapshot();
            EnsureSelection(snapshot);
            RefreshFrame(snapshot.Entries.Count);
            UpdateButtonStates();

            MouseState mouseState = Mouse.GetState();
            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;

            if (!_isMinimized && ContainsPoint(mouseState.X, mouseState.Y))
            {
                _hoveredDeleteQuestId = GetDeleteQuestIdAtPoint(mouseState.X, mouseState.Y);
                if (leftPressed)
                {
                    _pressedDeleteQuestId = _hoveredDeleteQuestId;
                }

                if (leftReleased)
                {
                    if (_pressedDeleteQuestId > 0 && _pressedDeleteQuestId == _hoveredDeleteQuestId)
                    {
                        DismissQuest(_pressedDeleteQuestId);
                    }
                    else
                    {
                        HandleRowSelection(mouseState.X, mouseState.Y);
                    }

                    _pressedDeleteQuestId = -1;
                }
            }
            else
            {
                _hoveredDeleteQuestId = -1;
                if (leftReleased)
                {
                    _pressedDeleteQuestId = -1;
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
            if (_font == null)
            {
                return;
            }

            QuestAlarmSnapshot snapshot = GetFilteredSnapshot();
            EnsureSelection(snapshot);
            RefreshFrame(snapshot.Entries.Count);

            Vector2 titlePosition = new Vector2(Position.X + 8, Position.Y + 5);
            string title = $"Quest Alarm ({snapshot.Entries.Count})";
            sprite.DrawString(_font, title, titlePosition, Color.White, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);

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

            int y = Position.Y + 27;
            int availableWidth = _maxTopTexture.Width - 8;
            int displayedEntries = Math.Min(MaxVisibleEntries, snapshot.Entries.Count);

            for (int i = 0; i < displayedEntries; i++)
            {
                QuestAlarmEntrySnapshot entry = snapshot.Entries[i];
                int rowHeight = 36;
                int requirementLines = Math.Min(2, entry.RequirementLines.Count);
                rowHeight += requirementLines * 16;
                if (!string.IsNullOrWhiteSpace(entry.DemandText))
                {
                    rowHeight += 14;
                }

                Rectangle rowRect = new Rectangle(Position.X + 4, y, availableWidth, rowHeight);
                Rectangle deleteRect = new Rectangle(rowRect.Right - DeleteButtonSize - 4, rowRect.Y + 4, DeleteButtonSize, DeleteButtonSize);
                _rowLayouts.Add(new RowLayout(entry.QuestId, rowRect, deleteRect));

                DrawRowBackground(sprite, rowRect, entry);

                Color titleColor = entry.IsReadyToComplete
                    ? new Color(150, 241, 142)
                    : entry.IsRecentlyUpdated
                        ? new Color(151, 221, 255)
                        : new Color(255, 228, 153);
                sprite.DrawString(_font, Truncate(entry.Title, 24), new Vector2(rowRect.X + 6, rowRect.Y + 2), titleColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

                string progressText = entry.TotalProgress > 0
                    ? $"{Math.Min(entry.CurrentProgress, entry.TotalProgress)}/{entry.TotalProgress}"
                    : entry.StatusText;
                Vector2 progressSize = _font.MeasureString(progressText) * 0.42f;
                sprite.DrawString(_font, progressText, new Vector2(deleteRect.X - progressSize.X - 6, rowRect.Y + 3), new Color(214, 220, 229), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

                DrawInlineDelete(sprite, deleteRect, entry.QuestId);
                DrawProgressGauge(sprite, rowRect.X + 6, rowRect.Y + 17, Math.Min(90, rowRect.Width - 30), entry.ProgressRatio);

                int lineY = rowRect.Y + 31;
                for (int requirementIndex = 0; requirementIndex < requirementLines; requirementIndex++)
                {
                    DrawRequirementLine(sprite, rowRect, entry.RequirementLines[requirementIndex], ref lineY);
                }

                if (!string.IsNullOrWhiteSpace(entry.DemandText))
                {
                    sprite.DrawString(_font, Truncate(entry.DemandText, 32), new Vector2(rowRect.X + 6, lineY), new Color(201, 207, 221), 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
                }

                y += rowHeight + 4;
            }

            if (snapshot.Entries.Count > displayedEntries)
            {
                int hiddenCount = snapshot.Entries.Count - displayedEntries;
                sprite.DrawString(_font, $"+{hiddenCount} more active quest(s)", new Vector2(Position.X + 8, y + 2), new Color(214, 218, 226), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
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
            _autoTrackEnabled = !_autoTrackEnabled;
            UpdateButtonStates();
        }

        private void OpenQuestLog()
        {
            QuestAlarmSnapshot snapshot = GetFilteredSnapshot();
            QuestLogRequested?.Invoke(_selectedQuestId, snapshot.HasAlertAnimation);
        }

        private void SetMinimized(bool minimized)
        {
            _isMinimized = minimized;
            RefreshFrame(GetFilteredSnapshot().Entries.Count);
            UpdateButtonStates();
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
                _autoTrackEnabled = false;
                _lastRowClickQuestId = row.QuestId;
                _lastRowClickTick = Environment.TickCount64;
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
            QuestAlarmSnapshot snapshot = _snapshotProvider?.Invoke() ?? new QuestAlarmSnapshot();
            if (snapshot.Entries.Count == 0)
            {
                _dismissedQuestIds.Clear();
                return snapshot;
            }

            HashSet<int> activeQuestIds = snapshot.Entries.Select(entry => entry.QuestId).ToHashSet();
            _dismissedQuestIds.RemoveWhere(questId => !activeQuestIds.Contains(questId));

            List<QuestAlarmEntrySnapshot> entries = snapshot.Entries
                .Where(entry => !_dismissedQuestIds.Contains(entry.QuestId))
                .ToList();

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
                return;
            }

            if (_autoTrackEnabled)
            {
                QuestAlarmEntrySnapshot autoEntry = snapshot.Entries.FirstOrDefault(entry => entry.IsReadyToComplete)
                    ?? snapshot.Entries.FirstOrDefault(entry => entry.IsRecentlyUpdated)
                    ?? snapshot.Entries[0];
                _selectedQuestId = autoEntry.QuestId;
                return;
            }

            if (snapshot.Entries.Any(entry => entry.QuestId == _selectedQuestId))
            {
                return;
            }

            _selectedQuestId = snapshot.Entries[0].QuestId;
        }

        private void RefreshFrame(int entryCount)
        {
            if (_isMinimized)
            {
                Frame = _minimizedFrame;
                return;
            }

            int visibleEntries = Math.Max(1, Math.Min(MaxVisibleEntries, entryCount));
            if (!_maximizedFrames.TryGetValue(visibleEntries, out IDXObject frame))
            {
                int centerRepeats = HeaderRepeats + (visibleEntries * RepeatsPerEntry);
                Texture2D texture = BuildFrameTexture(_device, _maxTopTexture, _centerTexture, _bottomTexture, centerRepeats);
                frame = new DXObject(0, 0, texture, 0);
                _maximizedFrames[visibleEntries] = frame;
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

        private void DrawRowBackground(SpriteBatch sprite, Rectangle rowRect, QuestAlarmEntrySnapshot entry)
        {
            Color background = entry.QuestId == _selectedQuestId
                ? new Color(34, 63, 101, 208)
                : new Color(7, 14, 20, 196);
            sprite.Draw(_pixel, rowRect, background);

            if (_selectionBarTexture != null && entry.QuestId == _selectedQuestId)
            {
                sprite.Draw(_selectionBarTexture, new Vector2(rowRect.X + 2, rowRect.Y), Color.White * 0.82f);
            }

            Color borderColor = entry.IsReadyToComplete
                ? new Color(115, 197, 104, 170)
                : entry.IsRecentlyUpdated
                    ? new Color(83, 154, 219, 170)
                    : new Color(77, 90, 102, 150);
            DrawBorder(sprite, rowRect, borderColor);
        }

        private void DrawRequirementLine(SpriteBatch sprite, Rectangle rowRect, QuestLogLineSnapshot line, ref int lineY)
        {
            Color lineColor = line.IsComplete ? new Color(143, 229, 135) : new Color(236, 205, 131);
            int lineX = rowRect.X + 6;
            if (line.ItemId.HasValue)
            {
                Texture2D icon = GetItemIcon(line.ItemId.Value);
                if (icon != null)
                {
                    sprite.Draw(icon, new Rectangle(lineX, lineY - 1, 14, 14), Color.White);
                    lineX += 18;
                }
            }

            string lineText = $"{line.Label}: {line.Text}";
            sprite.DrawString(_font, Truncate(lineText, 30), new Vector2(lineX, lineY), lineColor, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
            lineY += 16;
        }

        private void DrawProgressGauge(SpriteBatch sprite, int x, int y, int width, float ratio)
        {
            ratio = MathHelper.Clamp(ratio, 0f, 1f);
            if (_progressFrameTexture != null)
            {
                sprite.Draw(_progressFrameTexture, new Vector2(x, y), Color.White);
            }

            if (_progressGaugeTexture != null)
            {
                int innerWidth = Math.Max(1, (int)Math.Round(width * ratio));
                sprite.Draw(_progressGaugeTexture, new Rectangle(x + 2, y + 1, innerWidth, Math.Max(1, _progressGaugeTexture.Height)), Color.White);
                if (_progressSpotTexture != null && innerWidth > 0)
                {
                    sprite.Draw(_progressSpotTexture, new Vector2(x + 1 + innerWidth, y + 1), Color.White);
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

            _dismissedQuestIds.Add(questId);
            if (_selectedQuestId == questId)
            {
                _selectedQuestId = -1;
            }

            UpdateButtonStates();
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
