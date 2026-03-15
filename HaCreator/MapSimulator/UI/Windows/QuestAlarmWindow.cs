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
        private const int RepeatsPerEntry = 2;

        private readonly string _windowName;
        private readonly GraphicsDevice _device;
        private readonly Texture2D _maxTopTexture;
        private readonly Texture2D _centerTexture;
        private readonly Texture2D _bottomTexture;
        private readonly IDXObject _minimizedFrame;
        private readonly Dictionary<int, IDXObject> _maximizedFrames = new();
        private readonly HashSet<int> _dismissedQuestIds = new();
        private readonly List<RowLayout> _rowLayouts = new();
        private readonly Texture2D _pixel;

        private SpriteFont _font;
        private MouseState _previousMouseState;
        private Func<QuestAlarmSnapshot> _snapshotProvider;
        private UIObject _autoButton;
        private UIObject _questButton;
        private UIObject _maximizeButton;
        private UIObject _minimizeButton;
        private UIObject _deleteButton;
        private int _selectedQuestId = -1;
        private bool _isMinimized;
        private bool _autoTrackEnabled;

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

        internal void SetSnapshotProvider(Func<QuestAlarmSnapshot> provider)
        {
            _snapshotProvider = provider;
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
            _deleteButton = deleteButton;

            ConfigureButton(_autoButton, 102, 4, ToggleAutoTrack);
            ConfigureButton(_questButton, 136, 4, OpenSelectedQuest);
            ConfigureButton(_maximizeButton, 150, 4, () => SetMinimized(false));
            ConfigureButton(_minimizeButton, 150, 4, () => SetMinimized(true));
            ConfigureButton(_deleteButton, 165, 4, DismissSelectedQuest);

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
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && !_isMinimized && ContainsPoint(mouseState.X, mouseState.Y))
            {
                HandleRowSelection(mouseState.X, mouseState.Y);
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

            int y = Position.Y + 28;
            int availableWidth = (_maxTopTexture.Width - 12);
            int displayedEntries = Math.Min(MaxVisibleEntries, snapshot.Entries.Count);

            for (int i = 0; i < displayedEntries; i++)
            {
                QuestAlarmEntrySnapshot entry = snapshot.Entries[i];
                int rowHeight = 30;
                int requirementLines = Math.Min(2, entry.RequirementLines.Count);
                rowHeight += requirementLines * 12;
                if (!string.IsNullOrWhiteSpace(entry.DemandText))
                {
                    rowHeight += 12;
                }

                Rectangle rowRect = new Rectangle(Position.X + 4, y, availableWidth, rowHeight);
                _rowLayouts.Add(new RowLayout(entry.QuestId, rowRect));

                Color background = entry.QuestId == _selectedQuestId
                    ? new Color(84, 136, 201, 108)
                    : new Color(8, 16, 24, 116);
                sprite.Draw(_pixel, rowRect, background);

                Color titleColor = entry.IsReadyToComplete
                    ? new Color(150, 241, 142)
                    : new Color(255, 228, 153);
                sprite.DrawString(_font, Truncate(entry.Title, 24), new Vector2(rowRect.X + 4, rowRect.Y + 2), titleColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

                string progressText = entry.TotalProgress > 0
                    ? $"{Math.Min(entry.CurrentProgress, entry.TotalProgress)}/{entry.TotalProgress}"
                    : entry.StatusText;
                Vector2 progressSize = _font.MeasureString(progressText) * 0.42f;
                sprite.DrawString(_font, progressText, new Vector2(rowRect.Right - progressSize.X - 4, rowRect.Y + 3), new Color(214, 220, 229), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

                int lineY = rowRect.Y + 15;
                for (int requirementIndex = 0; requirementIndex < requirementLines; requirementIndex++)
                {
                    QuestLogLineSnapshot line = entry.RequirementLines[requirementIndex];
                    Color lineColor = line.IsComplete ? new Color(143, 229, 135) : new Color(236, 205, 131);
                    string lineText = $"{line.Label}: {line.Text}";
                    sprite.DrawString(_font, Truncate(lineText, 34), new Vector2(rowRect.X + 6, lineY), lineColor, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
                    lineY += 12;
                }

                if (!string.IsNullOrWhiteSpace(entry.DemandText))
                {
                    sprite.DrawString(_font, Truncate(entry.DemandText, 34), new Vector2(rowRect.X + 6, lineY), new Color(201, 207, 221), 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
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

        private void OpenSelectedQuest()
        {
            if (_selectedQuestId > 0)
            {
                QuestRequested?.Invoke(_selectedQuestId);
            }
        }

        private void DismissSelectedQuest()
        {
            if (_selectedQuestId <= 0)
            {
                return;
            }

            _dismissedQuestIds.Add(_selectedQuestId);
            _selectedQuestId = -1;
            UpdateButtonStates();
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
                if (!_rowLayouts[i].Bounds.Contains(mouseX, mouseY))
                {
                    continue;
                }

                _selectedQuestId = _rowLayouts[i].QuestId;
                _autoTrackEnabled = false;
                UpdateButtonStates();
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

            return new QuestAlarmSnapshot
            {
                Entries = snapshot.Entries
                    .Where(entry => !_dismissedQuestIds.Contains(entry.QuestId))
                    .ToList()
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
            _questButton?.SetVisible(!_isMinimized);
            _deleteButton?.SetVisible(!_isMinimized);
            _maximizeButton?.SetVisible(_isMinimized);
            _minimizeButton?.SetVisible(!_isMinimized);

            if (_autoButton != null)
            {
                _autoButton.SetButtonState(_autoTrackEnabled ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            if (_questButton != null)
            {
                _questButton.SetButtonState(_selectedQuestId > 0 ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_deleteButton != null)
            {
                _deleteButton.SetButtonState(_selectedQuestId > 0 ? UIObjectState.Normal : UIObjectState.Disabled);
            }
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
            public RowLayout(int questId, Rectangle bounds)
            {
                QuestId = questId;
                Bounds = bounds;
            }

            public int QuestId { get; }
            public Rectangle Bounds { get; }
        }
    }
}
