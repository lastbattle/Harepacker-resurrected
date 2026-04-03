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
        private const int CATEGORY_ROW_HEIGHT = 18;
        private const int CATEGORY_COLUMN_GAP = 4;
        private const int CATEGORY_PANEL_PADDING = 6;
        private const int ClientCategoryRowTop = 56;
        private const int ClientCategoryRowStride = 22;
        private const int ClientCategoryRowWidth = 192;
        private const int ClientCategoryButtonLeft = 15;
        private const int ClientCategoryButtonTopInset = 5;
        private const int ClientCategoryTextLeft = 31;
        private const int ClientCategoryCountRight = 188;
        private const int TAB_AVAILABLE = 0;
        private const int TAB_IN_PROGRESS = 1;
        private const int TAB_COMPLETED = 2;
        private const int TAB_RECOMMENDED = 3;
        private const int ClientSuppressedQuestCategoryCode = 51;
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
        private UIObject _showCategoryButton;
        private UIObject _hideCategoryButton;
        private readonly Dictionary<int, List<QuestDisplayData>> _questsByTab;
        private readonly Dictionary<QuestLogTabType, HashSet<int>> _hiddenAreaCodesByTab = new();
        private readonly List<CategoryButtonSlot> _categoryButtonSlots = new();
        private Texture2D _selectionHighlight;
        private Texture2D _iconAvailable;
        private Texture2D _iconInProgress;
        private Texture2D _iconCompleted;
        private Texture2D _categoryLegendTexture;
        private Texture2D _categoryLegendInnerTexture;
        private Texture2D[] _categoryLegendSheetTextures = Array.Empty<Texture2D>();
        private Texture2D[] _categoryExpandButtonTextures = Array.Empty<Texture2D>();
        private Texture2D[] _categoryCollapseButtonTextures = Array.Empty<Texture2D>();
        private Texture2D _pixel;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private Func<QuestLogTabType, bool, QuestLogSnapshot> _questLogProvider;
        private Func<QuestLogTabType, bool, int?> _preferredQuestIdProvider;
        private Point _lastMousePosition;
        private HoveredQuestItemInfo _hoveredQuestItem;
        private bool _categoryPanelExpanded;
        private bool _categoryLegendVisible;
        private int _categoryScrollOffset;
        private bool _categoryPanelDockToBottom;

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
                    _categoryLegendVisible = false;
                    _categoryScrollOffset = 0;
                    UpdateTabStates();
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

        public void InitializeCategoryFilterButtons(UIObject showCategoryButton, UIObject hideCategoryButton, UIObject categoryLegendButton)
        {
            _showCategoryButton = showCategoryButton;
            _hideCategoryButton = hideCategoryButton;
            EnsureCategoryButtonSlots();

            if (_showCategoryButton != null)
            {
                AddButton(_showCategoryButton);
                _showCategoryButton.ButtonClickReleased += _ =>
                {
                    _categoryPanelExpanded = true;
                    _categoryLegendVisible = false;
                    _categoryScrollOffset = 0;
                    UpdateTabStates();
                };
            }

            if (_hideCategoryButton != null)
            {
                AddButton(_hideCategoryButton);
                _hideCategoryButton.ButtonClickReleased += _ =>
                {
                    _categoryPanelExpanded = false;
                    _categoryScrollOffset = 0;
                    UpdateTabStates();
                };
            }

            if (categoryLegendButton != null)
            {
                AddButton(categoryLegendButton);
                categoryLegendButton.ButtonClickReleased += _ =>
                {
                    _categoryLegendVisible = !_categoryLegendVisible;
                    if (_categoryLegendVisible)
                    {
                        _categoryPanelExpanded = false;
                    }
                };
            }

            UpdateTabStates();
        }

        public void SetCategoryPanelDockToBottom(bool dockToBottom)
        {
            _categoryPanelDockToBottom = dockToBottom;
            UpdateTabStates();
        }

        public void SetCategoryLegendTextures(Texture2D categoryLegendTexture, Texture2D categoryLegendInnerTexture, params Texture2D[] categoryLegendSheetTextures)
        {
            _categoryLegendTexture = categoryLegendTexture;
            _categoryLegendInnerTexture = categoryLegendInnerTexture;
            _categoryLegendSheetTextures = categoryLegendSheetTextures?
                .Where(texture => texture != null)
                .ToArray()
                ?? Array.Empty<Texture2D>();
        }

        public void SetCategoryLegendTexture(Texture2D categoryLegendTexture)
        {
            SetCategoryLegendTextures(categoryLegendTexture, null);
        }

        public void SetCategoryButtonTextures(Texture2D[] expandButtonTextures, Texture2D[] collapseButtonTextures)
        {
            _categoryExpandButtonTextures = expandButtonTextures?.Where(static texture => texture != null).ToArray() ?? Array.Empty<Texture2D>();
            _categoryCollapseButtonTextures = collapseButtonTextures?.Where(static texture => texture != null).ToArray() ?? Array.Empty<Texture2D>();
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

        internal void SetQuestPreferredSelectionProvider(Func<QuestLogTabType, bool, int?> provider)
        {
            _preferredQuestIdProvider = provider;
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
            base.SetFont(font);
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

            bool showCategoryControls = HasCategoryFilterControls();
            if (_showCategoryButton != null)
            {
                _showCategoryButton.SetVisible(showCategoryControls && !_categoryPanelExpanded);
            }

            if (_hideCategoryButton != null)
            {
                _hideCategoryButton.SetVisible(showCategoryControls && _categoryPanelExpanded);
            }

            RefreshCategoryButtonSlots();
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
            RefreshCategoryButtonSlots();

            Rectangle tabRect = GetTabArea();
            Rectangle listRect = GetListArea();
            DrawTabs(sprite, tabRect);
            DrawCategoryFilterPanel(sprite, tabRect);
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
            DrawCategoryButtons(sprite);
            DrawCategoryButtonLabels(sprite);
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
            RefreshCategoryButtonSlots();

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            _hoveredQuestItem = null;

            if (ContainsPoint(mouseState.X, mouseState.Y))
            {
                if (HandleCategoryWheel(mouseState.X, mouseState.Y, wheelDelta))
                {
                }
                else if (wheelDelta > 0)
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

            if (HandleCategoryClick(mouseX, mouseY))
            {
                return;
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
            if (_categoryPanelDockToBottom)
            {
                bottom -= GetCategoryPanelHeight();
            }
            else
            {
                top += GetCategoryPanelHeight();
            }

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

            int? preferredQuestId = _preferredQuestIdProvider?.Invoke((QuestLogTabType)_currentTab, _showAllLevels);
            if (preferredQuestId.HasValue && snapshot.Entries.Any(entry => entry.QuestId == preferredQuestId.Value))
            {
                _selectedQuestId = preferredQuestId.Value;
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
                return ApplyCategoryFilters(_questLogProvider((QuestLogTabType)_currentTab, _showAllLevels) ?? new QuestLogSnapshot());
            }

            if (!_questsByTab.TryGetValue(_currentTab, out List<QuestDisplayData> quests))
            {
                return new QuestLogSnapshot();
            }

            return ApplyCategoryFilters(new QuestLogSnapshot
            {
                Entries = quests.Select(quest => new QuestLogEntrySnapshot
                {
                    QuestId = quest.QuestId,
                    Name = quest.QuestName ?? $"Quest #{quest.QuestId}",
                    AreaCode = 0,
                    AreaName = "General",
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
            });
        }

        private QuestLogSnapshot GetUnfilteredSnapshot()
        {
            return _questLogProvider?.Invoke((QuestLogTabType)_currentTab, _showAllLevels) ?? new QuestLogSnapshot();
        }

        private QuestLogSnapshot ApplyCategoryFilters(QuestLogSnapshot snapshot)
        {
            if (snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                return snapshot ?? new QuestLogSnapshot();
            }

            HashSet<int> hiddenAreaCodes = GetHiddenAreaCodes((QuestLogTabType)_currentTab);
            if (hiddenAreaCodes.Count == 0)
            {
                return snapshot;
            }

            return new QuestLogSnapshot
            {
                Entries = snapshot.Entries.Where(entry => !hiddenAreaCodes.Contains(entry.AreaCode)).ToList()
            };
        }

        private HashSet<int> GetHiddenAreaCodes(QuestLogTabType tab)
        {
            if (!_hiddenAreaCodesByTab.TryGetValue(tab, out HashSet<int> hiddenAreaCodes))
            {
                hiddenAreaCodes = new HashSet<int>();
                _hiddenAreaCodesByTab[tab] = hiddenAreaCodes;
            }

            return hiddenAreaCodes;
        }

        private void DrawCategoryFilterPanel(SpriteBatch sprite, Rectangle tabRect)
        {
            if (!HasCategoryFilterControls())
            {
                return;
            }

            Rectangle panelRect = GetCategoryPanelArea(tabRect);
            if (panelRect.Height <= 0)
            {
                return;
            }

            if (_categoryLegendVisible && _categoryLegendTexture != null)
            {
                sprite.Draw(_categoryLegendTexture, panelRect, Color.White);
                DrawCategoryLegendContents(sprite, panelRect);
                return;
            }

            if (!HasClientCategoryButtonArt())
            {
                sprite.Draw(_pixel, panelRect, new Color(7, 16, 29, 188));
            }

            IReadOnlyList<QuestAreaFilterEntry> areaFilters = GetVisibleAreaFilters();
            if (areaFilters.Count == 0)
            {
                DrawText(sprite, "No category filters for this tab.", new Vector2(panelRect.X + 6, panelRect.Y + 4), new Color(214, 218, 228), SMALL_TEXT_SCALE);
                return;
            }
        }

        private bool HandleCategoryClick(int mouseX, int mouseY)
        {
            if (!HasCategoryFilterControls())
            {
                return false;
            }

            Rectangle panelRect = GetCategoryPanelArea(GetTabArea());
            if (!panelRect.Contains(mouseX, mouseY))
            {
                return false;
            }

            if (_categoryLegendVisible)
            {
                _categoryLegendVisible = false;
                return true;
            }

            for (int i = 0; i < _categoryButtonSlots.Count; i++)
            {
                CategoryButtonSlot slot = _categoryButtonSlots[i];
                if (!slot.IsVisible || slot.Entry == null)
                {
                    continue;
                }

                if (slot.ButtonBounds.Contains(mouseX, mouseY) || slot.Bounds.Contains(mouseX, mouseY))
                {
                    ToggleCategoryButton(i);
                    return true;
                }
            }

            return true;
        }

        private bool HandleCategoryWheel(int mouseX, int mouseY, int wheelDelta)
        {
            if (!HasCategoryFilterControls() || wheelDelta == 0)
            {
                return false;
            }

            Rectangle panelRect = GetCategoryPanelArea(GetTabArea());
            if (!panelRect.Contains(mouseX, mouseY) || _categoryLegendVisible)
            {
                return false;
            }

            int visibleRows = GetVisibleCategoryRowCount(panelRect);
            int columnCount = GetCategoryColumnCount(panelRect);
            int totalRows = (int)Math.Ceiling(GetVisibleAreaFilters().Count / (float)columnCount);
            int maxOffset = Math.Max(0, totalRows - visibleRows);
            _categoryScrollOffset = wheelDelta > 0
                ? Math.Max(0, _categoryScrollOffset - 1)
                : Math.Min(maxOffset, _categoryScrollOffset + 1);
            return true;
        }

        private bool HasCategoryFilterControls()
        {
            return _showCategoryButton != null || _hideCategoryButton != null;
        }

        private void EnsureCategoryButtonSlots()
        {
            if (_categoryButtonSlots.Count > 0)
            {
                return;
            }

            Rectangle panelRect = GetPrototypeCategoryPanelRectangle();
            int columnCount = GetCategoryColumnCount(panelRect);
            int visibleRows = GetVisibleCategoryRowCount(panelRect);
            int slotCount = Math.Max(1, visibleRows * columnCount);

            for (int i = 0; i < slotCount; i++)
            {
                _categoryButtonSlots.Add(new CategoryButtonSlot());
            }
        }

        private void RefreshCategoryButtonSlots()
        {
            if (_categoryButtonSlots.Count == 0)
            {
                return;
            }

            foreach (CategoryButtonSlot slot in _categoryButtonSlots)
            {
                slot.Clear();
            }

            if (!HasCategoryFilterControls() || !_categoryPanelExpanded || _categoryLegendVisible)
            {
                return;
            }

            IReadOnlyList<QuestAreaFilterEntry> areaFilters = GetVisibleAreaFilters();
            if (areaFilters.Count == 0)
            {
                return;
            }

            Rectangle panelRect = GetCategoryPanelArea(GetTabArea());
            if (panelRect.Height <= 0)
            {
                return;
            }

            int columnCount = GetCategoryColumnCount(panelRect);
            int visibleRows = GetVisibleCategoryRowCount(panelRect);
            int pageSize = Math.Max(1, visibleRows * columnCount);
            int totalRows = (int)Math.Ceiling(areaFilters.Count / (float)columnCount);
            int maxOffset = Math.Max(0, totalRows - visibleRows);
            _categoryScrollOffset = Math.Clamp(_categoryScrollOffset, 0, maxOffset);

            for (int i = 0; i < pageSize && i < _categoryButtonSlots.Count; i++)
            {
                int entryIndex = (_categoryScrollOffset * columnCount) + i;
                if (entryIndex >= areaFilters.Count)
                {
                    break;
                }

                QuestAreaFilterEntry entry = areaFilters[entryIndex];
                Rectangle rowRect = GetCategoryCellRectangle(panelRect, i / columnCount, i % columnCount, columnCount);
                CategoryButtonSlot slot = _categoryButtonSlots[i];
                slot.Assign(entry, rowRect, GetCategoryButtonRectangle(rowRect));
            }
        }

        private void ToggleCategoryButton(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _categoryButtonSlots.Count)
            {
                return;
            }

            QuestAreaFilterEntry entry = _categoryButtonSlots[slotIndex].Entry;
            if (entry == null)
            {
                return;
            }

            HashSet<int> hiddenAreaCodes = GetHiddenAreaCodes((QuestLogTabType)_currentTab);
            if (!hiddenAreaCodes.Add(entry.AreaCode))
            {
                hiddenAreaCodes.Remove(entry.AreaCode);
            }

            _scrollOffset = 0;
            _selectedQuestId = -1;
            RefreshCategoryButtonSlots();
        }

        private int GetCategoryPanelHeight()
        {
            Rectangle panelRect = GetCategoryPanelArea(GetTabArea());
            return panelRect.Height > 0 ? panelRect.Height + 6 : 0;
        }

        private Rectangle GetCategoryPanelArea(Rectangle tabRect)
        {
            if (!HasCategoryFilterControls() || (!_categoryPanelExpanded && !_categoryLegendVisible))
            {
                return Rectangle.Empty;
            }

            int frameWidth = CurrentFrame?.Width ?? 240;
            int width = Math.Max(80, frameWidth - 12);
            int x = Position.X + 6;
            int y = GetCategoryPanelTop(tabRect);
            if (_categoryLegendVisible && _categoryLegendTexture != null)
            {
                width = _categoryLegendTexture.Width;
                x = Position.X + Math.Max(0, ((CurrentFrame?.Width ?? width) - width) / 2);
                int legendHeight = _categoryLegendTexture.Height;
                if (_categoryPanelDockToBottom)
                {
                    y = GetCategoryPanelBottom(tabRect) - legendHeight;
                }

                return new Rectangle(x, y, width, legendHeight);
            }

            int columnCount = GetCategoryColumnCount(width);
            int rowCount = Math.Max(1, Math.Min(4, (int)Math.Ceiling(GetVisibleAreaFilters().Count / (float)columnCount)));
            int height = HasClientCategoryButtonArt()
                ? rowCount * ClientCategoryRowStride
                : (rowCount * CATEGORY_ROW_HEIGHT) + (CATEGORY_PANEL_PADDING * 2);
            if (HasClientCategoryButtonArt())
            {
                y = Position.Y + ClientCategoryRowTop;
            }
            if (_categoryPanelDockToBottom)
            {
                y = GetCategoryPanelBottom(tabRect) - height;
            }

            return new Rectangle(x, y, width, height);
        }

        private Rectangle GetPrototypeCategoryPanelRectangle()
        {
            int windowWidth = CurrentFrame?.Width ?? 240;
            int width = Math.Max(80, windowWidth - 12);
            int height = HasClientCategoryButtonArt()
                ? 4 * ClientCategoryRowStride
                : (4 * CATEGORY_ROW_HEIGHT) + (CATEGORY_PANEL_PADDING * 2);
            return new Rectangle(0, 0, width, height);
        }

        private int GetCategoryPanelTop(Rectangle tabRect)
        {
            int y = (_currentTab == TAB_AVAILABLE && HasClientLevelButtons()) ? tabRect.Bottom + 24 : tabRect.Bottom + 4;
            y = Math.Max(y, GetButtonBottom(_showCategoryButton) + 4);
            y = Math.Max(y, GetButtonBottom(_hideCategoryButton) + 4);
            return y;
        }

        private int GetCategoryPanelBottom(Rectangle tabRect)
        {
            if (!_categoryPanelDockToBottom)
            {
                Rectangle panelRect = GetCategoryPanelArea(tabRect);
                return panelRect.Bottom;
            }

            int footerTop = GetFooterArea().Y - 4;
            int buttonTop = GetButtonTop(_showCategoryButton);
            if (buttonTop > 0)
            {
                footerTop = Math.Min(footerTop, buttonTop - 4);
            }

            buttonTop = GetButtonTop(_hideCategoryButton);
            if (buttonTop > 0)
            {
                footerTop = Math.Min(footerTop, buttonTop - 4);
            }

            return footerTop;
        }

        private int GetButtonTop(UIObject button)
        {
            return button == null ? 0 : Position.Y + button.Y;
        }

        private int GetButtonBottom(UIObject button)
        {
            if (button == null)
            {
                return 0;
            }

            int height = Math.Max(0, button.CanvasSnapshotHeight);
            return Position.Y + button.Y + height;
        }

        private void DrawCategoryButtonLabels(SpriteBatch sprite)
        {
            if (_font == null || _categoryButtonSlots.Count == 0)
            {
                return;
            }

            HashSet<int> hiddenAreaCodes = GetHiddenAreaCodes((QuestLogTabType)_currentTab);
            IReadOnlyList<CategoryButtonSlot> visibleSlots = _categoryButtonSlots
                .Where(slot => slot.IsVisible && slot.Entry != null)
                .OrderBy(slot => slot.Bounds.Y)
                .ThenBy(slot => slot.Bounds.X)
                .ToArray();

            foreach (CategoryButtonSlot slot in _categoryButtonSlots)
            {
                if (!slot.IsVisible || slot.Entry == null)
                {
                    continue;
                }

                bool enabled = !hiddenAreaCodes.Contains(slot.Entry.AreaCode);
                Rectangle bounds = slot.Bounds;
                string countText = slot.Entry.Count.ToString();
                float countScale = 0.38f;
                Vector2 countMeasure = ClientTextDrawing.Measure((GraphicsDevice)null, countText, countScale, _font);
                Color labelColor = enabled ? new Color(70, 45, 24) : new Color(106, 98, 88);
                Color countColor = enabled ? new Color(108, 76, 42) : new Color(128, 120, 108);
                int textX = HasClientCategoryButtonArt() ? bounds.X + ClientCategoryTextLeft : bounds.X + 6;
                int textY = HasClientCategoryButtonArt()
                    ? bounds.Y + GetCategoryRowTextTopOffset(visibleSlots, slot)
                    : bounds.Y + 3;
                int countRight = HasClientCategoryButtonArt() ? bounds.X + ClientCategoryCountRight : bounds.Right - 5;

                DrawText(
                    sprite,
                    Truncate(slot.Entry.AreaName, 14),
                    new Vector2(textX, textY),
                    labelColor,
                    0.38f,
                    Math.Max(16, countRight - textX - 6 - (int)countMeasure.X));
                DrawText(
                    sprite,
                    countText,
                    new Vector2(countRight - countMeasure.X, textY),
                    countColor,
                    countScale);
            }
        }

        private void DrawCategoryButtons(SpriteBatch sprite)
        {
            if (_categoryLegendVisible)
            {
                return;
            }

            HashSet<int> hiddenAreaCodes = GetHiddenAreaCodes((QuestLogTabType)_currentTab);
            IReadOnlyList<CategoryButtonSlot> visibleSlots = _categoryButtonSlots
                .Where(slot => slot.IsVisible && slot.Entry != null)
                .OrderBy(slot => slot.Bounds.Y)
                .ThenBy(slot => slot.Bounds.X)
                .ToArray();

            foreach (CategoryButtonSlot slot in _categoryButtonSlots)
            {
                if (!slot.IsVisible || slot.Entry == null)
                {
                    continue;
                }

                bool hidden = hiddenAreaCodes.Contains(slot.Entry.AreaCode);
                bool hovered = slot.ButtonBounds.Contains(_lastMousePosition);
                if (HasClientCategoryButtonArt())
                {
                    Texture2D rowTexture = ResolveCategoryRowTexture(visibleSlots, slot);
                    if (rowTexture != null)
                    {
                        int rowY = slot.Bounds.Y + Math.Max(0, (slot.Bounds.Height - rowTexture.Height) / 2);
                        sprite.Draw(rowTexture, new Vector2(slot.Bounds.X, rowY), Color.White);
                    }
                }

                Texture2D buttonTexture = ResolveCategoryButtonTexture(hidden, hovered);
                if (buttonTexture != null)
                {
                    sprite.Draw(buttonTexture, new Vector2(slot.ButtonBounds.X, slot.ButtonBounds.Y), Color.White);
                    continue;
                }

                Color fill = hidden ? new Color(244, 228, 194) : new Color(228, 205, 166);
                Color border = hidden ? new Color(160, 118, 72) : new Color(149, 105, 59);
                sprite.Draw(_pixel, slot.ButtonBounds, fill);
                sprite.Draw(_pixel, new Rectangle(slot.ButtonBounds.X, slot.ButtonBounds.Y, slot.ButtonBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(slot.ButtonBounds.X, slot.ButtonBounds.Bottom - 1, slot.ButtonBounds.Width, 1), border);
                sprite.Draw(_pixel, new Rectangle(slot.ButtonBounds.X, slot.ButtonBounds.Y, 1, slot.ButtonBounds.Height), border);
                sprite.Draw(_pixel, new Rectangle(slot.ButtonBounds.Right - 1, slot.ButtonBounds.Y, 1, slot.ButtonBounds.Height), border);
            }
        }

        private void DrawCategoryLegendContents(SpriteBatch sprite, Rectangle panelRect)
        {
            Rectangle innerRect = ResolveCategoryLegendInnerRectangle(panelRect);
            if (_categoryLegendInnerTexture != null)
            {
                sprite.Draw(_categoryLegendInnerTexture, innerRect, Color.White);
            }

            if (_categoryLegendSheetTextures.Length == 0)
            {
                int fallbackRowHeight = 18;
                int fallbackStartY = innerRect.Y + Math.Max(0, (innerRect.Height - (fallbackRowHeight * 3)) / 2);
                for (int i = 0; i < 3; i++)
                {
                    Rectangle rowRect = new(
                        innerRect.X,
                        fallbackStartY + (i * fallbackRowHeight),
                        innerRect.Width,
                        fallbackRowHeight);
                    DrawCategoryLegendRow(sprite, i, rowRect);
                }

                return;
            }

            int totalSheetHeight = _categoryLegendSheetTextures.Sum(texture => texture.Height);
            int gap = 4;
            int totalGapHeight = gap * Math.Max(0, _categoryLegendSheetTextures.Length - 1);
            int startY = innerRect.Y + Math.Max(0, (innerRect.Height - totalSheetHeight - totalGapHeight) / 2);

            for (int i = 0; i < _categoryLegendSheetTextures.Length; i++)
            {
                Texture2D sheetTexture = _categoryLegendSheetTextures[i];
                if (sheetTexture == null)
                {
                    continue;
                }

                Rectangle rowRect = new(
                    innerRect.X + Math.Max(0, (innerRect.Width - sheetTexture.Width) / 2),
                    startY,
                    sheetTexture.Width,
                    sheetTexture.Height);
                sprite.Draw(sheetTexture, rowRect, Color.White);
                startY += sheetTexture.Height + gap;
            }
        }

        private int GetCategoryColumnCount(Rectangle panelRect)
        {
            return GetCategoryColumnCount(panelRect.Width);
        }

        private int GetCategoryColumnCount(int panelWidth)
        {
            if (HasClientCategoryButtonArt())
            {
                return 1;
            }

            int contentWidth = Math.Max(1, panelWidth - (CATEGORY_PANEL_PADDING * 2));
            return contentWidth >= 180 ? 2 : 1;
        }

        private Rectangle GetCategoryCellRectangle(Rectangle panelRect, int rowIndex, int columnIndex, int columnCount)
        {
            if (HasClientCategoryButtonArt())
            {
                int rowWidth = Math.Min(panelRect.Width, ClientCategoryRowWidth);
                int rowX = panelRect.X + Math.Max(0, (panelRect.Width - rowWidth) / 2);
                return new Rectangle(rowX, panelRect.Y + (rowIndex * ClientCategoryRowStride), rowWidth, ClientCategoryRowStride);
            }

            int availableWidth = panelRect.Width - (CATEGORY_PANEL_PADDING * 2) - ((columnCount - 1) * CATEGORY_COLUMN_GAP);
            int cellWidth = Math.Max(56, availableWidth / columnCount);
            int x = panelRect.X + CATEGORY_PANEL_PADDING + (columnIndex * (cellWidth + CATEGORY_COLUMN_GAP));
            int y = panelRect.Y + CATEGORY_PANEL_PADDING + (rowIndex * CATEGORY_ROW_HEIGHT);
            return new Rectangle(x, y, cellWidth, CATEGORY_ROW_HEIGHT - 2);
        }

        private Rectangle GetCategoryButtonRectangle(Rectangle rowRect)
        {
            if (HasClientCategoryButtonArt())
            {
                Texture2D buttonTexture = ResolveCategoryButtonTexture(hidden: false, hovered: false)
                    ?? ResolveCategoryButtonTexture(hidden: true, hovered: false);
                int width = buttonTexture?.Width ?? 13;
                int height = buttonTexture?.Height ?? 12;
                return new Rectangle(
                    rowRect.X + ClientCategoryButtonLeft,
                    rowRect.Y + ClientCategoryButtonTopInset,
                    width,
                    height);
            }

            return rowRect;
        }

        private Rectangle ResolveCategoryLegendInnerRectangle(Rectangle panelRect)
        {
            if (_categoryLegendInnerTexture == null)
            {
                return new Rectangle(
                    panelRect.X + CATEGORY_PANEL_PADDING,
                    panelRect.Y + CATEGORY_PANEL_PADDING,
                    Math.Max(1, panelRect.Width - (CATEGORY_PANEL_PADDING * 2)),
                    Math.Max(1, panelRect.Height - (CATEGORY_PANEL_PADDING * 2)));
            }

            int offsetX = _categoryLegendTexture != null
                ? Math.Max(0, (_categoryLegendTexture.Width - _categoryLegendInnerTexture.Width) / 2)
                : CATEGORY_PANEL_PADDING;
            int offsetY = _categoryLegendTexture != null
                ? Math.Max(0, (_categoryLegendTexture.Height - _categoryLegendInnerTexture.Height) / 2)
                : CATEGORY_PANEL_PADDING;
            return new Rectangle(
                panelRect.X + offsetX,
                panelRect.Y + offsetY,
                _categoryLegendInnerTexture.Width,
                _categoryLegendInnerTexture.Height);
        }

        private void DrawCategoryLegendRow(SpriteBatch sprite, int rowIndex, Rectangle rowRect)
        {
            Texture2D iconTexture = rowIndex switch
            {
                0 => _iconAvailable,
                1 => _iconInProgress,
                2 => _iconCompleted,
                _ => null
            };

            if (iconTexture != null)
            {
                int iconX = rowRect.X + 6;
                int iconY = rowRect.Y + Math.Max(0, (rowRect.Height - iconTexture.Height) / 2);
                sprite.Draw(iconTexture, new Vector2(iconX, iconY), Color.White);
            }

            string label = rowIndex switch
            {
                0 => "Available quests",
                1 => "In-progress quests",
                2 => "Completed quests",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            DrawText(
                sprite,
                label,
                new Vector2(rowRect.X + 34, rowRect.Y + 3),
                new Color(49, 49, 49),
                SMALL_TEXT_SCALE,
                rowRect.Width - 40);
        }

        private int GetVisibleCategoryRowCount(Rectangle panelRect)
        {
            if (HasClientCategoryButtonArt())
            {
                return Math.Max(1, panelRect.Height / ClientCategoryRowStride);
            }

            return Math.Max(1, (panelRect.Height - (CATEGORY_PANEL_PADDING * 2)) / CATEGORY_ROW_HEIGHT);
        }

        private bool HasClientCategoryButtonArt()
        {
            return _categoryExpandButtonTextures.Length > 0 || _categoryCollapseButtonTextures.Length > 0;
        }

        private Texture2D ResolveCategoryButtonTexture(bool hidden, bool hovered)
        {
            Texture2D[] textures = hidden ? _categoryExpandButtonTextures : _categoryCollapseButtonTextures;
            if (textures.Length == 0)
            {
                return null;
            }

            int index = hovered && textures.Length > 3 ? 3 : 0;
            return textures[Math.Clamp(index, 0, textures.Length - 1)];
        }

        private Texture2D ResolveCategoryRowTexture(IReadOnlyList<CategoryButtonSlot> visibleSlots, CategoryButtonSlot slot)
        {
            if (_categoryLegendSheetTextures.Length == 0 || visibleSlots == null || visibleSlots.Count == 0)
            {
                return null;
            }

            int slotIndex = FindCategorySlotIndex(visibleSlots, slot);
            if (slotIndex < 0)
            {
                return _categoryLegendSheetTextures[Math.Clamp(Math.Min(1, _categoryLegendSheetTextures.Length - 1), 0, _categoryLegendSheetTextures.Length - 1)];
            }

            int textureIndex = visibleSlots.Count switch
            {
                <= 1 => Math.Min(1, _categoryLegendSheetTextures.Length - 1),
                2 => slotIndex == 0 ? 0 : Math.Min(2, _categoryLegendSheetTextures.Length - 1),
                _ => slotIndex == 0
                    ? 0
                    : slotIndex == visibleSlots.Count - 1
                        ? Math.Min(2, _categoryLegendSheetTextures.Length - 1)
                        : Math.Min(1, _categoryLegendSheetTextures.Length - 1)
            };

            return _categoryLegendSheetTextures[Math.Clamp(textureIndex, 0, _categoryLegendSheetTextures.Length - 1)];
        }

        private static int GetCategoryRowTextTopOffset(IReadOnlyList<CategoryButtonSlot> visibleSlots, CategoryButtonSlot slot)
        {
            if (visibleSlots == null || visibleSlots.Count == 0)
            {
                return 4;
            }

            int slotIndex = FindCategorySlotIndex(visibleSlots, slot);
            return slotIndex == visibleSlots.Count - 1 ? 3 : 4;
        }

        private static int FindCategorySlotIndex(IReadOnlyList<CategoryButtonSlot> visibleSlots, CategoryButtonSlot slot)
        {
            if (visibleSlots == null)
            {
                return -1;
            }

            for (int i = 0; i < visibleSlots.Count; i++)
            {
                if (ReferenceEquals(visibleSlots[i], slot))
                {
                    return i;
                }
            }

            return -1;
        }

        private IReadOnlyList<QuestAreaFilterEntry> GetVisibleAreaFilters()
        {
            QuestLogSnapshot snapshot = GetUnfilteredSnapshot();
            if (snapshot?.Entries == null || snapshot.Entries.Count == 0)
            {
                return Array.Empty<QuestAreaFilterEntry>();
            }

            return snapshot.Entries
                .Where(entry => entry.AreaCode != ClientSuppressedQuestCategoryCode)
                .GroupBy(entry => new { entry.AreaCode, entry.AreaName })
                .Select(group => new QuestAreaFilterEntry(
                    group.Key.AreaCode,
                    string.IsNullOrWhiteSpace(group.Key.AreaName) ? "General" : group.Key.AreaName,
                    group.Count()))
                .OrderBy(entry => entry.AreaCode)
                .ThenBy(entry => entry.AreaName, StringComparer.OrdinalIgnoreCase)
                .ToList();
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
            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(
                _hoveredQuestItem.ItemId,
                InventoryItemMetadataResolver.ResolveInventoryType(_hoveredQuestItem.ItemId));

            string[] wrappedTitle = WrapText(title, titleWidth, titleScale);
            float titleHeight = MeasureTooltipHeight(wrappedTitle, titleScale);
            var wrappedSections = new List<(string[] Lines, Color Color, float Height)>();

            void AddSection(string text, Color color)
            {
                string[] wrapped = WrapText(text, bodyWidth, bodyScale);
                float height = MeasureTooltipHeight(wrapped, bodyScale);
                if (height > 0f)
                {
                    wrappedSections.Add((wrapped, color, height));
                }
            }

            AddSection(_hoveredQuestItem.Subtitle, new Color(228, 233, 242));
            AddSection(metadata.TypeName, new Color(180, 220, 255));
            for (int i = 0; i < metadata.EffectLines.Count; i++)
            {
                AddSection(metadata.EffectLines[i], new Color(180, 255, 210));
            }

            for (int i = 0; i < metadata.MetadataLines.Count; i++)
            {
                AddSection(metadata.MetadataLines[i], new Color(255, 214, 156));
            }

            AddSection(_hoveredQuestItem.Description, new Color(199, 206, 218));

            float bodyHeight = 0f;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                if (bodyHeight > 0f)
                {
                    bodyHeight += 4f;
                }

                bodyHeight += wrappedSections[i].Height;
            }

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
            float sectionY = textY;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                if (i > 0)
                {
                    sectionY += 4f;
                }

                DrawTooltipLines(sprite, wrappedSections[i].Lines, new Vector2(bodyX, sectionY), wrappedSections[i].Color, bodyScale);
                sectionY += wrappedSections[i].Height;
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
                    while (length > 1 && ClientTextDrawing.Measure((GraphicsDevice)null, remaining[..length], scale, _font).X > maxWidth)
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
            ClientTextDrawing.Draw(sprite, wrapped, position, color, scale, _font);
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
                    if (ClientTextDrawing.Measure((GraphicsDevice)null, candidate, scale, _font).X <= maxWidth)
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

    internal sealed class QuestAreaFilterEntry
    {
        public QuestAreaFilterEntry(int areaCode, string areaName, int count)
        {
            AreaCode = areaCode;
            AreaName = areaName;
            Count = count;
        }

        public int AreaCode { get; }
        public string AreaName { get; }
        public int Count { get; }
    }

    internal sealed class CategoryButtonSlot
    {
        public bool IsVisible => Entry != null;
        public QuestAreaFilterEntry Entry { get; private set; }
        public Rectangle Bounds { get; private set; }
        public Rectangle ButtonBounds { get; private set; }

        public void Assign(QuestAreaFilterEntry entry, Rectangle bounds, Rectangle buttonBounds)
        {
            Entry = entry;
            Bounds = bounds;
            ButtonBounds = buttonBounds;
        }

        public void Clear()
        {
            Entry = null;
            Bounds = Rectangle.Empty;
            ButtonBounds = Rectangle.Empty;
        }
    }
}
