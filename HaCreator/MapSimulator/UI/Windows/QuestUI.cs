using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using HaSharedLibrary.Util;

namespace HaCreator.MapSimulator.UI
{
    public class QuestUI : UIWindowBase
    {
        private const int QUEST_ENTRY_HEIGHT = 24;
        private const int MIN_VISIBLE_QUESTS = 4;
        private const int FOOTER_HEIGHT = 42;
        private const int TAB_AVAILABLE = 0;
        private const int TAB_IN_PROGRESS = 1;
        private const int TAB_COMPLETED = 2;
        private const int TAB_RECOMMENDED = 3;
        private const int DETAIL_LINE_ICON_SIZE = 12;
        private const float TEXT_SCALE = 0.58f;
        private const float SMALL_TEXT_SCALE = 0.5f;

        public enum QuestState
        {
            NotStarted = 0,
            InProgress = 1,
            Completed = 2
        }

        private int _currentTab = TAB_IN_PROGRESS;
        private int _scrollOffset;
        private int _selectedQuestId = -1;
        private bool _showAllLevels;
        private UIObject _tabAvailable;
        private UIObject _tabInProgress;
        private UIObject _tabCompleted;
        private UIObject _tabRecommended;
        private UIObject _myLevelButton;
        private UIObject _allLevelButton;
        private UIObject _detailButton;
        private readonly Dictionary<int, List<QuestDisplayData>> _questsByTab;
        private Texture2D _selectionHighlight;
        private Texture2D _iconAvailable;
        private Texture2D _iconInProgress;
        private Texture2D _iconCompleted;
        private Texture2D _pixel;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private Func<QuestLogTabType, bool, QuestLogSnapshot> _questLogProvider;
        private Point _lastMousePosition;
        private HoveredQuestItemInfo _hoveredQuestItem;

        public event Action<int> QuestDetailRequested;

        public override string WindowName => "Quest";

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value < TAB_AVAILABLE || value > TAB_RECOMMENDED)
                {
                    return;
                }

                if (_currentTab != value)
                {
                    _currentTab = value;
                    _scrollOffset = 0;
                    _selectedQuestId = -1;
                }
            }
        }

        public QuestUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _graphicsDevice = device;
            _questsByTab = new Dictionary<int, List<QuestDisplayData>>
            {
                { TAB_AVAILABLE, new List<QuestDisplayData>() },
                { TAB_IN_PROGRESS, new List<QuestDisplayData>() },
                { TAB_COMPLETED, new List<QuestDisplayData>() },
                { TAB_RECOMMENDED, new List<QuestDisplayData>() }
            };

            _selectionHighlight = new Texture2D(device, 1, 1);
            _selectionHighlight.SetData(new[] { new Color(88, 149, 214, 140) });

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void InitializeTabs(UIObject availableTab, UIObject inProgressTab, UIObject completedTab, UIObject recommendedTab)
        {
            _tabAvailable = availableTab;
            _tabInProgress = inProgressTab;
            _tabCompleted = completedTab;
            _tabRecommended = recommendedTab;

            if (availableTab != null)
            {
                AddButton(availableTab);
                availableTab.ButtonClickReleased += sender => CurrentTab = TAB_AVAILABLE;
            }

            if (inProgressTab != null)
            {
                AddButton(inProgressTab);
                inProgressTab.ButtonClickReleased += sender => CurrentTab = TAB_IN_PROGRESS;
            }

            if (completedTab != null)
            {
                AddButton(completedTab);
                completedTab.ButtonClickReleased += sender => CurrentTab = TAB_COMPLETED;
            }

            if (recommendedTab != null)
            {
                AddButton(recommendedTab);
                recommendedTab.ButtonClickReleased += sender => CurrentTab = TAB_RECOMMENDED;
            }

            UpdateTabStates();
        }

        public void InitializeLevelFilterButtons(UIObject myLevelButton, UIObject allLevelButton)
        {
            _myLevelButton = myLevelButton;
            _allLevelButton = allLevelButton;

            if (myLevelButton != null)
            {
                AddButton(myLevelButton);
                myLevelButton.ButtonClickReleased += _ =>
                {
                    _showAllLevels = false;
                    _scrollOffset = 0;
                    _selectedQuestId = -1;
                };
            }

            if (allLevelButton != null)
            {
                AddButton(allLevelButton);
                allLevelButton.ButtonClickReleased += _ =>
                {
                    _showAllLevels = true;
                    _scrollOffset = 0;
                    _selectedQuestId = -1;
                };
            }

            UpdateTabStates();
        }

        public void InitializeDetailButton(UIObject detailButton)
        {
            _detailButton = detailButton;
            if (detailButton == null)
            {
                return;
            }

            AddButton(detailButton);
            detailButton.ButtonClickReleased += _ =>
            {
                if (_selectedQuestId > 0)
                {
                    QuestDetailRequested?.Invoke(_selectedQuestId);
                }
            };

            UpdateTabStates();
        }

        public void SetQuestIcons(Texture2D available, Texture2D inProgress, Texture2D completed)
        {
            _iconAvailable = available;
            _iconInProgress = inProgress;
            _iconCompleted = completed;
        }

        internal void SetQuestLogProvider(Func<QuestLogTabType, bool, QuestLogSnapshot> provider)
        {
            _questLogProvider = provider;
        }

        public int? GetSelectedQuestId()
        {
            return _selectedQuestId > 0 ? _selectedQuestId : null;
        }

        public IReadOnlyList<int> GetCurrentTabQuestIds()
        {
            return GetCurrentSnapshot().Entries.Select(entry => entry.QuestId).ToList();
        }

        public bool SelectQuestById(int questId)
        {
            QuestLogSnapshot snapshot = GetCurrentSnapshot();
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                if (snapshot.Entries[i].QuestId != questId)
                {
                    continue;
                }

                _selectedQuestId = questId;
                if (i < _scrollOffset)
                {
                    _scrollOffset = i;
                }
                else
                {
                    int visibleQuestCount = GetVisibleQuestCount(GetListArea());
                    if (i >= _scrollOffset + visibleQuestCount)
                    {
                        _scrollOffset = Math.Max(0, i - (visibleQuestCount - 1));
                    }
                }

                return true;
            }

            return false;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        private void UpdateTabStates()
        {
            _tabAvailable?.SetButtonState(_currentTab == TAB_AVAILABLE ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabInProgress?.SetButtonState(_currentTab == TAB_IN_PROGRESS ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCompleted?.SetButtonState(_currentTab == TAB_COMPLETED ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabRecommended?.SetButtonState(_currentTab == TAB_RECOMMENDED ? UIObjectState.Pressed : UIObjectState.Normal);

            bool showLevelButtons = _currentTab == TAB_AVAILABLE;
            if (_myLevelButton != null)
            {
                _myLevelButton.SetVisible(showLevelButtons);
                _myLevelButton.SetButtonState(!_showAllLevels ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            if (_allLevelButton != null)
            {
                _allLevelButton.SetVisible(showLevelButtons);
                _allLevelButton.SetButtonState(_showAllLevels ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            if (_detailButton != null)
            {
                _detailButton.SetVisible(true);
                _detailButton.SetButtonState(_selectedQuestId > 0 ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_font == null)
            {
                return;
            }

            QuestLogSnapshot snapshot = GetCurrentSnapshot();
            EnsureSelection(snapshot);

            Rectangle tabRect = GetTabArea();
            Rectangle listRect = GetListArea();
            DrawTabs(sprite, tabRect);
            DrawQuestList(sprite, listRect, snapshot);
            DrawQuestFooter(sprite, GetFooterArea(), snapshot);
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            base.DrawOverlay(sprite, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);
            DrawHoveredItemTooltip(sprite);
        }

        private void DrawTabs(SpriteBatch sprite, Rectangle tabRect)
        {
            if (HasClientTabButtons())
            {
                return;
            }

            string[] labels = { "Available", "In Progress", "Completed", "Recommended" };
            int tabWidth = (tabRect.Width - 12) / 4;
            for (int i = 0; i < 4; i++)
            {
                Rectangle rect = new Rectangle(tabRect.X + (i * tabWidth), tabRect.Y, tabWidth - 2, tabRect.Height);
                Color fill = i == _currentTab ? new Color(59, 105, 160, 210) : new Color(42, 55, 74, 190);
                sprite.Draw(_pixel, rect, fill);
                DrawText(sprite, labels[i], new Vector2(rect.X + 6, rect.Y + 4), Color.White, SMALL_TEXT_SCALE);
            }

            if (_currentTab != TAB_AVAILABLE || HasClientLevelButtons())
            {
                return;
            }

            Rectangle myLevelRect = GetMyLevelButtonArea(tabRect);
            Rectangle allLevelRect = GetAllLevelButtonArea(tabRect);
            sprite.Draw(_pixel, myLevelRect, !_showAllLevels ? new Color(90, 135, 84, 210) : new Color(62, 68, 76, 180));
            sprite.Draw(_pixel, allLevelRect, _showAllLevels ? new Color(90, 135, 84, 210) : new Color(62, 68, 76, 180));
            DrawText(sprite, "My Level", new Vector2(myLevelRect.X + 6, myLevelRect.Y + 4), Color.White, SMALL_TEXT_SCALE);
            DrawText(sprite, "All", new Vector2(allLevelRect.X + 18, allLevelRect.Y + 4), Color.White, SMALL_TEXT_SCALE);
        }

        private void DrawQuestList(SpriteBatch sprite, Rectangle listRect, QuestLogSnapshot snapshot)
        {
            sprite.Draw(_pixel, listRect, new Color(10, 22, 41, 155));
            IReadOnlyList<QuestLogEntrySnapshot> entries = snapshot.Entries;
            int visibleQuestCount = GetVisibleQuestCount(listRect);
            int visibleCount = Math.Min(visibleQuestCount, Math.Max(0, entries.Count - _scrollOffset));

            for (int i = 0; i < visibleCount; i++)
            {
                QuestLogEntrySnapshot entry = entries[_scrollOffset + i];
                Rectangle rowRect = new Rectangle(listRect.X + 4, listRect.Y + 4 + (i * QUEST_ENTRY_HEIGHT), listRect.Width - 8, QUEST_ENTRY_HEIGHT - 2);
                bool isSelected = entry.QuestId == _selectedQuestId;
                if (isSelected)
                {
                    sprite.Draw(_selectionHighlight, rowRect, Color.White);
                }
                else
                {
                    sprite.Draw(_pixel, rowRect, i % 2 == 0 ? new Color(255, 255, 255, 12) : new Color(0, 0, 0, 0));
                }

                DrawQuestIcon(sprite, entry, new Rectangle(rowRect.X + 4, rowRect.Y + 4, 14, 14));
                DrawText(sprite, Truncate(entry.Name, 26), new Vector2(rowRect.X + 22, rowRect.Y + 2), GetRowTitleColor(entry), SMALL_TEXT_SCALE);
                DrawText(sprite, Truncate(entry.StatusText, 18), new Vector2(rowRect.X + 22, rowRect.Y + 12), GetRowSubtitleColor(entry), 0.44f);
            }

            if (entries.Count == 0)
            {
                DrawText(sprite, "No quests in this tab.", new Vector2(listRect.X + 8, listRect.Y + 10), new Color(218, 221, 231), SMALL_TEXT_SCALE);
                return;
            }

            if (entries.Count > visibleQuestCount)
            {
                string scrollText = $"{_scrollOffset + 1}-{Math.Min(entries.Count, _scrollOffset + visibleQuestCount)} / {entries.Count}";
                DrawText(sprite, scrollText, new Vector2(listRect.Right - 64, listRect.Bottom - 16), new Color(210, 214, 224), 0.42f);
            }
        }

        private void DrawQuestFooter(SpriteBatch sprite, Rectangle footerRect, QuestLogSnapshot snapshot)
        {
            sprite.Draw(_pixel, footerRect, new Color(9, 18, 33, 165));

            QuestLogEntrySnapshot selected = snapshot.Entries.FirstOrDefault(entry => entry.QuestId == _selectedQuestId);
            if (selected == null)
            {
                string hint = _detailButton != null
                    ? "Select a quest, then use the Detail button to inspect it."
                    : "Select a quest, then click it again to open the detail window.";
                DrawText(sprite, hint, new Vector2(footerRect.X + 8, footerRect.Y + 8), new Color(224, 228, 236), SMALL_TEXT_SCALE, footerRect.Width - 16);
                return;
            }

            int y = footerRect.Y + 7;
            DrawText(sprite, selected.Name, new Vector2(footerRect.X + 8, y), Color.White, TEXT_SCALE, footerRect.Width - 16);
            y += 16;
            DrawText(sprite, selected.StatusText, new Vector2(footerRect.X + 8, y), GetRowSubtitleColor(selected), SMALL_TEXT_SCALE);
            if (!string.IsNullOrWhiteSpace(selected.NpcText))
            {
                DrawText(sprite, selected.NpcText, new Vector2(footerRect.Right - 96, y), new Color(199, 208, 223), 0.46f, 88);
            }

            y += 14;
            DrawProgressBar(sprite, new Rectangle(footerRect.X + 8, y, footerRect.Width - 16, 8), selected.ProgressRatio);
            y += 12;

            string hintText = selected.IssueLines.Count > 0
                ? selected.IssueLines[0]
                : string.IsNullOrWhiteSpace(selected.StageText)
                    ? selected.SummaryText
                    : selected.StageText;
            if (!string.IsNullOrWhiteSpace(hintText))
            {
                DrawText(sprite, Truncate(hintText, 68), new Vector2(footerRect.X + 8, y), new Color(228, 233, 240), 0.44f, footerRect.Width - 16);
            }
        }

        private void DrawProgressBar(SpriteBatch sprite, Rectangle rect, float ratio)
        {
            sprite.Draw(_pixel, rect, new Color(43, 54, 67, 210));
            int fillWidth = Math.Max(0, (int)((rect.Width - 2) * MathHelper.Clamp(ratio, 0f, 1f)));
            if (fillWidth > 0)
            {
                sprite.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + 1, fillWidth, rect.Height - 2), new Color(103, 196, 123, 220));
            }
        }

        private void DrawQuestIcon(SpriteBatch sprite, QuestLogEntrySnapshot entry, Rectangle rect)
        {
            Texture2D icon = entry.State switch
            {
                QuestStateType.Completed => _iconCompleted,
                QuestStateType.Started => entry.CanComplete ? _iconCompleted : _iconInProgress,
                _ => entry.CanStart ? _iconAvailable : _iconInProgress
            };

            if (icon != null)
            {
                sprite.Draw(icon, rect, Color.White);
                return;
            }

            sprite.Draw(_pixel, rect, GetRowTitleColor(entry));
        }

        private Color GetRowTitleColor(QuestLogEntrySnapshot entry)
        {
            if (entry.State == QuestStateType.Completed)
            {
                return new Color(200, 214, 224);
            }

            if (entry.CanComplete || entry.CanStart)
            {
                return new Color(255, 235, 149);
            }

            return entry.State == QuestStateType.Not_Started
                ? new Color(182, 191, 206)
                : Color.White;
        }

        private Color GetRowSubtitleColor(QuestLogEntrySnapshot entry)
        {
            if (entry.State == QuestStateType.Completed)
            {
                return new Color(152, 190, 157);
            }

            if (entry.CanComplete || entry.CanStart)
            {
                return new Color(124, 219, 120);
            }

            return new Color(207, 213, 226);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateTabStates();

            QuestLogSnapshot snapshot = GetCurrentSnapshot();
            EnsureSelection(snapshot);
            ClampScroll(snapshot);

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            _hoveredQuestItem = null;

            if (ContainsPoint(mouseState.X, mouseState.Y))
            {
                if (wheelDelta > 0)
                {
                    ScrollUp();
                }
                else if (wheelDelta < 0)
                {
                    ScrollDown();
                }

                if (leftReleased)
                {
                    HandleClick(mouseState.X, mouseState.Y, snapshot);
                }
            }

            _previousMouseState = mouseState;
        }

        private void HandleClick(int mouseX, int mouseY, QuestLogSnapshot snapshot)
        {
            Rectangle tabRect = GetTabArea();
            if (!HasClientTabButtons())
            {
                int tabWidth = (tabRect.Width - 12) / 4;
                for (int i = 0; i < 4; i++)
                {
                    Rectangle rect = new Rectangle(tabRect.X + (i * tabWidth), tabRect.Y, tabWidth - 2, tabRect.Height);
                    if (rect.Contains(mouseX, mouseY))
                    {
                        CurrentTab = i;
                        return;
                    }
                }
            }

            if (_currentTab == TAB_AVAILABLE && !HasClientLevelButtons())
            {
                Rectangle myLevelRect = GetMyLevelButtonArea(tabRect);
                Rectangle allLevelRect = GetAllLevelButtonArea(tabRect);
                if (myLevelRect.Contains(mouseX, mouseY))
                {
                    _showAllLevels = false;
                    _scrollOffset = 0;
                    _selectedQuestId = -1;
                    return;
                }

                if (allLevelRect.Contains(mouseX, mouseY))
                {
                    _showAllLevels = true;
                    _scrollOffset = 0;
                    _selectedQuestId = -1;
                    return;
                }
            }

            Rectangle listRect = GetListArea();
            if (!listRect.Contains(mouseX, mouseY))
            {
                return;
            }

            int rowIndex = (mouseY - listRect.Y - 4) / QUEST_ENTRY_HEIGHT;
            int entryIndex = _scrollOffset + rowIndex;
            if (rowIndex >= 0 && entryIndex >= 0 && entryIndex < snapshot.Entries.Count)
            {
                int clickedQuestId = snapshot.Entries[entryIndex].QuestId;
                bool openDetail = _detailButton == null && clickedQuestId == _selectedQuestId;
                _selectedQuestId = clickedQuestId;
                if (openDetail)
                {
                    QuestDetailRequested?.Invoke(_selectedQuestId);
                }
            }
        }

        private Rectangle GetTabArea()
        {
            return new Rectangle(Position.X + 8, Position.Y + 24, (CurrentFrame?.Width ?? 240) - 16, 18);
        }

        private Rectangle GetMyLevelButtonArea(Rectangle tabRect)
        {
            return new Rectangle(tabRect.X, tabRect.Bottom + 4, 74, 16);
        }

        private Rectangle GetAllLevelButtonArea(Rectangle tabRect)
        {
            return new Rectangle(tabRect.X + 78, tabRect.Bottom + 4, 48, 16);
        }

        private Rectangle GetListArea()
        {
            int top = Position.Y + (_currentTab == TAB_AVAILABLE ? 66 : 48);
            int bottom = GetFooterArea().Y - 8;
            return new Rectangle(Position.X + 8, top, (CurrentFrame?.Width ?? 240) - 16, Math.Max(QUEST_ENTRY_HEIGHT + 8, bottom - top));
        }

        private bool HasClientTabButtons()
        {
            return _tabAvailable != null || _tabInProgress != null || _tabCompleted != null || _tabRecommended != null;
        }

        private bool HasClientLevelButtons()
        {
            return _myLevelButton != null || _allLevelButton != null;
        }

        private Rectangle GetFooterArea()
        {
            int windowWidth = CurrentFrame?.Width ?? 240;
            int windowHeight = CurrentFrame?.Height ?? 396;
            return new Rectangle(Position.X + 8, Position.Y + windowHeight - FOOTER_HEIGHT - 8, windowWidth - 16, FOOTER_HEIGHT);
        }

        private int GetVisibleQuestCount(Rectangle listRect)
        {
            return Math.Max(MIN_VISIBLE_QUESTS, Math.Max(1, (listRect.Height - 8) / QUEST_ENTRY_HEIGHT));
        }

        private Rectangle GetDetailArea(Rectangle listRect)
        {
            return GetFooterArea();
        }

        private void EnsureSelection(QuestLogSnapshot snapshot)
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

            _selectedQuestId = snapshot.Entries[0].QuestId;
        }

        private void ClampScroll(QuestLogSnapshot snapshot)
        {
            int maxScroll = Math.Max(0, snapshot.Entries.Count - GetVisibleQuestCount(GetListArea()));
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);
        }

        private QuestLogSnapshot GetCurrentSnapshot()
        {
            if (_questLogProvider != null)
            {
                return _questLogProvider((QuestLogTabType)_currentTab, _showAllLevels) ?? new QuestLogSnapshot();
            }

            if (!_questsByTab.TryGetValue(_currentTab, out List<QuestDisplayData> quests))
            {
                return new QuestLogSnapshot();
            }

            return new QuestLogSnapshot
            {
                Entries = quests.Select(quest => new QuestLogEntrySnapshot
                {
                    QuestId = quest.QuestId,
                    Name = quest.QuestName ?? $"Quest #{quest.QuestId}",
                    State = quest.State switch
                    {
                        QuestState.NotStarted => QuestStateType.Not_Started,
                        QuestState.Completed => QuestStateType.Completed,
                        _ => QuestStateType.Started
                    },
                    StatusText = quest.State.ToString(),
                    SummaryText = quest.Description ?? string.Empty,
                    StageText = quest.Description ?? string.Empty,
                    NpcText = quest.StartNpcName ?? quest.EndNpcName ?? string.Empty,
                    ProgressRatio = quest.TotalRequirements > 0 ? MathHelper.Clamp((float)quest.CurrentProgress / quest.TotalRequirements, 0f, 1f) : 0f,
                    RequirementLines = quest.Requirements.Select(requirement => new QuestLogLineSnapshot
                    {
                        Label = requirement.RequirementType ?? "Req",
                        Text = string.IsNullOrWhiteSpace(requirement.Description)
                            ? $"{requirement.CurrentCount}/{requirement.RequiredCount}"
                            : requirement.Description,
                        IsComplete = requirement.CurrentCount >= requirement.RequiredCount
                    }).ToList(),
                    RewardLines = BuildLegacyRewardLines(quest)
                }).ToList()
            };
        }

        private static IReadOnlyList<QuestLogLineSnapshot> BuildLegacyRewardLines(QuestDisplayData quest)
        {
            var lines = new List<QuestLogLineSnapshot>();
            if (quest.ExpReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "EXP", Text = $"+{quest.ExpReward}", IsComplete = true });
            }

            if (quest.MesoReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "Meso", Text = $"+{quest.MesoReward}", IsComplete = true });
            }

            if (quest.FameReward > 0)
            {
                lines.Add(new QuestLogLineSnapshot { Label = "Fame", Text = $"+{quest.FameReward}", IsComplete = true });
            }

            foreach (QuestItemReward reward in quest.ItemRewards)
            {
                lines.Add(new QuestLogLineSnapshot
                {
                    Label = "Item",
                    Text = $"Item #{reward.ItemId} x{reward.Quantity}",
                    IsComplete = true,
                    ItemId = reward.ItemId
                });
            }

            return lines;
        }

        private void DrawQuestLineIcon(SpriteBatch sprite, Rectangle iconRect, QuestLogLineSnapshot line, Color fallbackColor)
        {
            Texture2D icon = line.ItemId.HasValue ? ResolveItemIcon(line.ItemId.Value) : null;
            if (icon != null)
            {
                sprite.Draw(icon, iconRect, Color.White);
                return;
            }

            sprite.Draw(_pixel, iconRect, fallbackColor);
        }

        private HoveredQuestItemInfo ResolveHoveredQuestItem(int mouseX, int mouseY, QuestLogSnapshot snapshot)
        {
            if (!IsVisible || !ContainsPoint(mouseX, mouseY))
            {
                return null;
            }

            QuestLogEntrySnapshot selected = snapshot.Entries.FirstOrDefault(entry => entry.QuestId == _selectedQuestId);
            if (selected == null)
            {
                return null;
            }

            Rectangle listRect = GetListArea();
            Rectangle detailRect = GetDetailArea(listRect);
            int y = detailRect.Y + 8 + 18 + 18 + 14;

            y = AdvanceSection(detailRect, y, selected.SummaryText, 0.45f);
            y = AdvanceSection(detailRect, y, selected.StageText, 0.45f);

            HoveredQuestItemInfo hovered = TryResolveHoveredLineItem(mouseX, mouseY, detailRect, ref y, selected.RequirementLines);
            if (hovered != null)
            {
                return hovered;
            }

            return TryResolveHoveredLineItem(mouseX, mouseY, detailRect, ref y, selected.RewardLines);
        }

        private int AdvanceSection(Rectangle detailRect, int y, string text, float scale)
        {
            if (string.IsNullOrWhiteSpace(text) || y >= detailRect.Bottom - 16)
            {
                return y;
            }

            return y + 14 + (MeasureWrappedLineCount(text, detailRect.Width - 16, scale) * 12) + 4;
        }

        private HoveredQuestItemInfo TryResolveHoveredLineItem(int mouseX, int mouseY, Rectangle detailRect, ref int y, IReadOnlyList<QuestLogLineSnapshot> lines)
        {
            if (lines == null || lines.Count == 0 || y >= detailRect.Bottom - 16)
            {
                return null;
            }

            y += 14;
            for (int i = 0; i < lines.Count && y < detailRect.Bottom - 14; i++)
            {
                QuestLogLineSnapshot line = lines[i];
                if (line.ItemId.HasValue)
                {
                    Rectangle iconRect = new Rectangle(detailRect.X + 8, y, DETAIL_LINE_ICON_SIZE, DETAIL_LINE_ICON_SIZE);
                    if (iconRect.Contains(mouseX, mouseY))
                    {
                        return CreateHoveredQuestItem(line.ItemId.Value, line.Text);
                    }
                }

                y += 14;
            }

            y += 2;
            return null;
        }

        private HoveredQuestItemInfo CreateHoveredQuestItem(int itemId, string lineText)
        {
            return new HoveredQuestItemInfo
            {
                ItemId = itemId,
                Title = ResolveItemName(itemId),
                Subtitle = lineText,
                Description = ResolveItemDescription(itemId),
                Icon = ResolveItemIcon(itemId)
            };
        }

        private Texture2D ResolveItemIcon(int itemId)
        {
            if (itemId <= 0 || _graphicsDevice == null)
            {
                return null;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                _itemIconCache[itemId] = null;
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            itemImage?.ParseImage();
            string itemText = category == "Character" ? itemId.ToString("D8") : itemId.ToString("D7");
            WzSubProperty itemProperty = itemImage?[itemText] as WzSubProperty;
            WzSubProperty infoProperty = itemProperty?["info"] as WzSubProperty;
            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty
                                          ?? infoProperty?["icon"] as WzCanvasProperty;
            Texture2D texture = iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_graphicsDevice);
            _itemIconCache[itemId] = texture;
            return texture;
        }

        private void DrawHoveredItemTooltip(SpriteBatch sprite)
        {
            if (_hoveredQuestItem == null || _font == null)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(_hoveredQuestItem.Title) ? $"Item #{_hoveredQuestItem.ItemId}" : _hoveredQuestItem.Title;
            const int tooltipWidth = 220;
            const int padding = 8;
            const int iconSize = 28;
            const int gap = 8;
            float titleScale = 0.56f;
            float bodyScale = 0.44f;
            float titleWidth = tooltipWidth - (padding * 2);
            float bodyWidth = tooltipWidth - ((padding * 2) + iconSize + gap);

            string[] wrappedTitle = WrapText(title, titleWidth, titleScale);
            string[] wrappedSubtitle = WrapText(_hoveredQuestItem.Subtitle, bodyWidth, bodyScale);
            string[] wrappedDescription = WrapText(_hoveredQuestItem.Description, bodyWidth, bodyScale);

            float titleHeight = MeasureTooltipHeight(wrappedTitle, titleScale);
            float subtitleHeight = MeasureTooltipHeight(wrappedSubtitle, bodyScale);
            float descriptionHeight = MeasureTooltipHeight(wrappedDescription, bodyScale);
            float bodyHeight = subtitleHeight + (descriptionHeight > 0f ? 4f + descriptionHeight : 0f);
            int tooltipHeight = (int)Math.Ceiling((padding * 2) + titleHeight + 6f + Math.Max(iconSize, bodyHeight));

            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            int tooltipX = _lastMousePosition.X + 18;
            int tooltipY = _lastMousePosition.Y + 18;
            if (tooltipX + tooltipWidth > viewportWidth - 4)
            {
                tooltipX = Math.Max(4, _lastMousePosition.X - tooltipWidth - 18);
            }

            if (tooltipY + tooltipHeight > viewportHeight - 4)
            {
                tooltipY = Math.Max(4, _lastMousePosition.Y - tooltipHeight - 18);
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            sprite.Draw(_pixel, backgroundRect, new Color(18, 24, 37, 235));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Y, backgroundRect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Bottom - 1, backgroundRect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Y, 1, backgroundRect.Height), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.Right - 1, backgroundRect.Y, 1, backgroundRect.Height), new Color(112, 146, 201));

            float textY = tooltipY + padding;
            DrawTooltipLines(sprite, wrappedTitle, new Vector2(tooltipX + padding, textY), new Color(255, 220, 120), titleScale);
            textY += titleHeight + 6f;

            if (_hoveredQuestItem.Icon != null)
            {
                sprite.Draw(_hoveredQuestItem.Icon, new Rectangle(tooltipX + padding, (int)textY, iconSize, iconSize), Color.White);
            }

            float bodyX = tooltipX + padding + iconSize + gap;
            DrawTooltipLines(sprite, wrappedSubtitle, new Vector2(bodyX, textY), new Color(228, 233, 242), bodyScale);
            if (descriptionHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedDescription, new Vector2(bodyX, textY + subtitleHeight + 4f), new Color(199, 206, 218), bodyScale);
            }
        }

        private void DrawTooltipLines(SpriteBatch sprite, IReadOnlyList<string> lines, Vector2 position, Color color, float scale)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                DrawText(sprite, lines[i], new Vector2(position.X, position.Y + (i * (_font.LineSpacing * scale))), color, scale);
            }
        }

        private string[] WrapText(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            var lines = new List<string>();
            string[] paragraphs = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string paragraph in paragraphs)
            {
                string remaining = paragraph.Trim();
                while (!string.IsNullOrEmpty(remaining))
                {
                    int length = remaining.Length;
                    while (length > 1 && (_font.MeasureString(remaining[..length]).X * scale) > maxWidth)
                    {
                        int previousSpace = remaining.LastIndexOf(' ', length - 1, length - 1);
                        length = previousSpace > 0 ? previousSpace : length - 1;
                    }

                    string line = remaining[..Math.Max(1, length)].Trim();
                    if (line.Length == 0)
                    {
                        break;
                    }

                    lines.Add(line);
                    remaining = remaining[line.Length..].TrimStart();
                }
            }

            return lines.Count == 0 ? Array.Empty<string>() : lines.ToArray();
        }

        private float MeasureTooltipHeight(IReadOnlyList<string> lines, float scale)
        {
            return lines == null || lines.Count == 0 ? 0f : lines.Count * (_font.LineSpacing * scale);
        }

        private static string ResolveItemName(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private static string ResolveItemDescription(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item3)
                ? itemInfo.Item3
                : string.Empty;
        }

        private void DrawText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale, int maxWidth = int.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string wrapped = maxWidth == int.MaxValue ? text : WrapText(text, maxWidth, scale);
            sprite.DrawString(_font, wrapped, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private string WrapText(string text, int maxWidth, float scale)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r", string.Empty).Split('\n'))
            {
                string current = string.Empty;
                foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                    if (_font.MeasureString(candidate).X * scale <= maxWidth)
                    {
                        current = candidate;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(current))
                        {
                            lines.Add(current);
                        }

                        current = word;
                    }
                }

                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                }
            }

            return string.Join("\n", lines);
        }

        private int MeasureWrappedLineCount(string text, int maxWidth, float scale)
        {
            string wrapped = WrapText(text, maxWidth, scale);
            return Math.Max(1, wrapped.Split('\n').Length);
        }

        private string Truncate(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            {
                return text ?? string.Empty;
            }

            return $"{text.Substring(0, Math.Max(0, maxChars - 3))}...";
        }

        public void AddQuest(int questId, string questName, string description,
            QuestState state, int currentProgress = 0, int totalRequirements = 0)
        {
            int tab = state switch
            {
                QuestState.NotStarted => TAB_AVAILABLE,
                QuestState.InProgress => TAB_IN_PROGRESS,
                QuestState.Completed => TAB_COMPLETED,
                _ => TAB_AVAILABLE
            };

            if (!_questsByTab.TryGetValue(tab, out List<QuestDisplayData> quests))
            {
                return;
            }

            QuestDisplayData existing = quests.FirstOrDefault(q => q.QuestId == questId);
            if (existing != null)
            {
                existing.QuestName = questName;
                existing.Description = description;
                existing.State = state;
                existing.CurrentProgress = currentProgress;
                existing.TotalRequirements = totalRequirements;
                return;
            }

            quests.Add(new QuestDisplayData
            {
                QuestId = questId,
                QuestName = questName,
                Description = description,
                State = state,
                CurrentProgress = currentProgress,
                TotalRequirements = totalRequirements
            });
        }

        public void UpdateQuestProgress(int questId, int currentProgress, int totalRequirements)
        {
            foreach ((_, List<QuestDisplayData> quests) in _questsByTab)
            {
                QuestDisplayData quest = quests.FirstOrDefault(q => q.QuestId == questId);
                if (quest == null)
                {
                    continue;
                }

                quest.CurrentProgress = currentProgress;
                quest.TotalRequirements = totalRequirements;
                if (currentProgress >= totalRequirements && quest.State == QuestState.InProgress)
                {
                    MoveQuestToTab(questId, TAB_COMPLETED);
                    quest.State = QuestState.Completed;
                }

                break;
            }
        }

        public void MoveQuestToTab(int questId, int newTab)
        {
            QuestDisplayData questToMove = null;
            int sourceTab = -1;

            foreach ((int tab, List<QuestDisplayData> quests) in _questsByTab)
            {
                questToMove = quests.FirstOrDefault(q => q.QuestId == questId);
                if (questToMove != null)
                {
                    sourceTab = tab;
                    break;
                }
            }

            if (questToMove == null || sourceTab < 0 || sourceTab == newTab)
            {
                return;
            }

            _questsByTab[sourceTab].Remove(questToMove);
            _questsByTab[newTab].Add(questToMove);
            questToMove.State = newTab switch
            {
                TAB_AVAILABLE => QuestState.NotStarted,
                TAB_IN_PROGRESS => QuestState.InProgress,
                TAB_COMPLETED => QuestState.Completed,
                _ => questToMove.State
            };
        }

        public void RemoveQuest(int questId)
        {
            foreach ((_, List<QuestDisplayData> quests) in _questsByTab)
            {
                QuestDisplayData quest = quests.FirstOrDefault(q => q.QuestId == questId);
                if (quest == null)
                {
                    continue;
                }

                quests.Remove(quest);
                if (_selectedQuestId == questId)
                {
                    _selectedQuestId = -1;
                }

                break;
            }
        }

        public void SelectQuest(int index)
        {
            if (!_questsByTab.TryGetValue(_currentTab, out List<QuestDisplayData> quests) || index < 0 || index >= quests.Count)
            {
                _selectedQuestId = -1;
                return;
            }

            _selectedQuestId = quests[index].QuestId;
        }

        public void ClearQuests()
        {
            foreach ((_, List<QuestDisplayData> quests) in _questsByTab)
            {
                quests.Clear();
            }

            _selectedQuestId = -1;
            _scrollOffset = 0;
        }

        public int GetQuestCount(int tab)
        {
            return _questsByTab.TryGetValue(tab, out List<QuestDisplayData> quests) ? quests.Count : 0;
        }

        public void ScrollUp()
        {
            if (_scrollOffset > 0)
            {
                _scrollOffset--;
            }
        }

        public void ScrollDown()
        {
            QuestLogSnapshot snapshot = GetCurrentSnapshot();
            int maxScroll = Math.Max(0, snapshot.Entries.Count - GetVisibleQuestCount(GetListArea()));
            if (_scrollOffset < maxScroll)
            {
                _scrollOffset++;
            }
        }
    }

    public class QuestDisplayData
    {
        public int QuestId { get; set; }
        public string QuestName { get; set; }
        public string Description { get; set; }
        public QuestUI.QuestState State { get; set; }
        public int CurrentProgress { get; set; }
        public int TotalRequirements { get; set; }
        public int ExpReward { get; set; }
        public int MesoReward { get; set; }
        public int FameReward { get; set; }
        public List<QuestItemReward> ItemRewards { get; set; } = new List<QuestItemReward>();
        public int StartNpcId { get; set; }
        public int EndNpcId { get; set; }
        public string StartNpcName { get; set; }
        public string EndNpcName { get; set; }
        public List<QuestRequirement> Requirements { get; set; } = new List<QuestRequirement>();
    }

    public class QuestItemReward
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public Texture2D ItemIcon { get; set; }
    }

    public class QuestRequirement
    {
        public string RequirementType { get; set; }
        public int TargetId { get; set; }
        public int RequiredCount { get; set; }
        public int CurrentCount { get; set; }
        public string Description { get; set; }
    }

    internal sealed class HoveredQuestItemInfo
    {
        public int ItemId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public Texture2D Icon { get; init; }
    }
}
