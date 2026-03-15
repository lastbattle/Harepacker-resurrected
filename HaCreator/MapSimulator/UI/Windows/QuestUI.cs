using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public class QuestUI : UIWindowBase
    {
        private const int QUEST_ENTRY_HEIGHT = 24;
        private const int VISIBLE_QUESTS = 7;
        private const int TAB_IN_PROGRESS = 0;
        private const int TAB_COMPLETED = 1;
        private const int TAB_AVAILABLE = 2;
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
        private UIObject _tabInProgress;
        private UIObject _tabCompleted;
        private UIObject _tabAvailable;
        private readonly Dictionary<int, List<QuestDisplayData>> _questsByTab;
        private Texture2D _selectionHighlight;
        private Texture2D _iconAvailable;
        private Texture2D _iconInProgress;
        private Texture2D _iconCompleted;
        private Texture2D _pixel;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private Func<QuestLogTabType, bool, QuestLogSnapshot> _questLogProvider;

        public event Action<int> QuestDetailRequested;

        public override string WindowName => "Quest";

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value < TAB_IN_PROGRESS || value > TAB_AVAILABLE)
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
            _questsByTab = new Dictionary<int, List<QuestDisplayData>>
            {
                { TAB_IN_PROGRESS, new List<QuestDisplayData>() },
                { TAB_COMPLETED, new List<QuestDisplayData>() },
                { TAB_AVAILABLE, new List<QuestDisplayData>() }
            };

            _selectionHighlight = new Texture2D(device, 1, 1);
            _selectionHighlight.SetData(new[] { new Color(88, 149, 214, 140) });

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void InitializeTabs(UIObject inProgressTab, UIObject completedTab, UIObject availableTab)
        {
            _tabInProgress = inProgressTab;
            _tabCompleted = completedTab;
            _tabAvailable = availableTab;

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

            if (availableTab != null)
            {
                AddButton(availableTab);
                availableTab.ButtonClickReleased += sender => CurrentTab = TAB_AVAILABLE;
            }

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
                else if (i >= _scrollOffset + VISIBLE_QUESTS)
                {
                    _scrollOffset = Math.Max(0, i - (VISIBLE_QUESTS - 1));
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
            _tabInProgress?.SetButtonState(_currentTab == TAB_IN_PROGRESS ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCompleted?.SetButtonState(_currentTab == TAB_COMPLETED ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabAvailable?.SetButtonState(_currentTab == TAB_AVAILABLE ? UIObjectState.Pressed : UIObjectState.Normal);
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
            Rectangle detailRect = GetDetailArea(listRect);

            DrawTabs(sprite, tabRect);
            DrawQuestList(sprite, listRect, snapshot);
            DrawQuestDetail(sprite, detailRect, snapshot);
        }

        private void DrawTabs(SpriteBatch sprite, Rectangle tabRect)
        {
            string[] labels = { "In Progress", "Completed", "Available" };
            int tabWidth = (tabRect.Width - 8) / 3;
            for (int i = 0; i < 3; i++)
            {
                Rectangle rect = new Rectangle(tabRect.X + (i * tabWidth), tabRect.Y, tabWidth - 2, tabRect.Height);
                Color fill = i == _currentTab ? new Color(59, 105, 160, 210) : new Color(42, 55, 74, 190);
                sprite.Draw(_pixel, rect, fill);
                DrawText(sprite, labels[i], new Vector2(rect.X + 6, rect.Y + 4), Color.White, SMALL_TEXT_SCALE);
            }

            if (_currentTab != TAB_AVAILABLE)
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
            int visibleCount = Math.Min(VISIBLE_QUESTS, Math.Max(0, entries.Count - _scrollOffset));

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

            if (entries.Count > VISIBLE_QUESTS)
            {
                string scrollText = $"{_scrollOffset + 1}-{Math.Min(entries.Count, _scrollOffset + VISIBLE_QUESTS)} / {entries.Count}";
                DrawText(sprite, scrollText, new Vector2(listRect.Right - 64, listRect.Bottom - 16), new Color(210, 214, 224), 0.42f);
            }
        }

        private void DrawQuestDetail(SpriteBatch sprite, Rectangle detailRect, QuestLogSnapshot snapshot)
        {
            sprite.Draw(_pixel, detailRect, new Color(9, 18, 33, 165));

            QuestLogEntrySnapshot selected = snapshot.Entries.FirstOrDefault(entry => entry.QuestId == _selectedQuestId);
            if (selected == null)
            {
                DrawText(sprite, "Select a quest to inspect its conditions and rewards.", new Vector2(detailRect.X + 8, detailRect.Y + 8), new Color(224, 228, 236), SMALL_TEXT_SCALE, detailRect.Width - 16);
                return;
            }

            int y = detailRect.Y + 8;
            DrawText(sprite, selected.Name, new Vector2(detailRect.X + 8, y), Color.White, TEXT_SCALE, detailRect.Width - 16);
            y += 18;
            DrawText(sprite, selected.StatusText, new Vector2(detailRect.X + 8, y), GetRowSubtitleColor(selected), SMALL_TEXT_SCALE);
            if (!string.IsNullOrWhiteSpace(selected.NpcText))
            {
                DrawText(sprite, selected.NpcText, new Vector2(detailRect.Right - 96, y), new Color(199, 208, 223), 0.46f, 88);
            }

            y += 18;
            DrawProgressBar(sprite, new Rectangle(detailRect.X + 8, y, detailRect.Width - 16, 8), selected.ProgressRatio);
            y += 14;

            y = DrawSection(sprite, detailRect, y, "Summary", selected.SummaryText);
            y = DrawSection(sprite, detailRect, y, "Details", selected.StageText);
            y = DrawLineSection(sprite, detailRect, y, "Requirements", selected.RequirementLines);
            y = DrawLineSection(sprite, detailRect, y, "Rewards", selected.RewardLines);

            if (selected.IssueLines.Count > 0 && y < detailRect.Bottom - 24)
            {
                DrawText(sprite, "Outstanding", new Vector2(detailRect.X + 8, y), new Color(255, 221, 126), SMALL_TEXT_SCALE);
                y += 14;
                for (int i = 0; i < selected.IssueLines.Count && y < detailRect.Bottom - 14; i++)
                {
                    DrawText(sprite, $"- {selected.IssueLines[i]}", new Vector2(detailRect.X + 14, y), new Color(255, 230, 172), 0.44f, detailRect.Width - 24);
                    y += 14;
                }
            }
        }

        private int DrawSection(SpriteBatch sprite, Rectangle detailRect, int y, string title, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || y >= detailRect.Bottom - 16)
            {
                return y;
            }

            DrawText(sprite, title, new Vector2(detailRect.X + 8, y), new Color(160, 210, 255), SMALL_TEXT_SCALE);
            y += 14;
            DrawText(sprite, text, new Vector2(detailRect.X + 8, y), new Color(229, 233, 240), 0.45f, detailRect.Width - 16);
            return y + (MeasureWrappedLineCount(text, detailRect.Width - 16, 0.45f) * 12) + 4;
        }

        private int DrawLineSection(SpriteBatch sprite, Rectangle detailRect, int y, string title, IReadOnlyList<QuestLogLineSnapshot> lines)
        {
            if (lines == null || lines.Count == 0 || y >= detailRect.Bottom - 16)
            {
                return y;
            }

            DrawText(sprite, title, new Vector2(detailRect.X + 8, y), new Color(160, 210, 255), SMALL_TEXT_SCALE);
            y += 14;
            for (int i = 0; i < lines.Count && y < detailRect.Bottom - 14; i++)
            {
                QuestLogLineSnapshot line = lines[i];
                Color iconColor = line.IsComplete ? new Color(114, 209, 108) : new Color(214, 146, 90);
                sprite.Draw(_pixel, new Rectangle(detailRect.X + 8, y + 2, 6, 6), iconColor);
                DrawText(sprite, $"{line.Label}: {line.Text}", new Vector2(detailRect.X + 18, y - 2), new Color(229, 233, 240), 0.44f, detailRect.Width - 28);
                y += 14;
            }

            return y + 2;
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
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;

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
            int tabWidth = (tabRect.Width - 8) / 3;
            for (int i = 0; i < 3; i++)
            {
                Rectangle rect = new Rectangle(tabRect.X + (i * tabWidth), tabRect.Y, tabWidth - 2, tabRect.Height);
                if (rect.Contains(mouseX, mouseY))
                {
                    CurrentTab = i;
                    return;
                }
            }

            if (_currentTab == TAB_AVAILABLE)
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
                _selectedQuestId = snapshot.Entries[entryIndex].QuestId;
                QuestDetailRequested?.Invoke(_selectedQuestId);
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
            return new Rectangle(Position.X + 8, top, (CurrentFrame?.Width ?? 240) - 16, VISIBLE_QUESTS * QUEST_ENTRY_HEIGHT + 8);
        }

        private Rectangle GetDetailArea(Rectangle listRect)
        {
            int windowWidth = CurrentFrame?.Width ?? 240;
            int windowHeight = CurrentFrame?.Height ?? 396;
            return new Rectangle(Position.X + 8, listRect.Bottom + 8, windowWidth - 16, windowHeight - (listRect.Bottom - Position.Y) - 16);
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
            int maxScroll = Math.Max(0, snapshot.Entries.Count - VISIBLE_QUESTS);
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
                    IsComplete = true
                });
            }

            return lines;
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
            int maxScroll = Math.Max(0, snapshot.Entries.Count - VISIBLE_QUESTS);
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
}
