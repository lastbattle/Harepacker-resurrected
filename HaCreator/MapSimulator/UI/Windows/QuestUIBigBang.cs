using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
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
    /// <summary>
    /// Quest UI window for post-Big Bang MapleStory (v100+)
    /// Structure: UI.wz/UIWindow2.img/Quest/list
    /// Window dimensions: 235x396 pixels
    /// </summary>
    public class QuestUIBigBang : UIWindowBase
    {
        #region Constants
        private const int QUEST_ENTRY_HEIGHT = 24;
        private const int VISIBLE_QUESTS = 8;

        // Window dimensions (from UIWindow2.img/Quest/list/backgrnd)
        private const int WINDOW_WIDTH = 235;
        private const int WINDOW_HEIGHT = 396;

        // Quest state tabs
        private const int TAB_IN_PROGRESS = 0;
        private const int TAB_COMPLETED = 1;
        private const int TAB_AVAILABLE = 2;
        #endregion

        #region Fields
        private int _currentTab = TAB_IN_PROGRESS;
        private int _scrollOffset = 0;

        // Foreground texture (backgrnd2 - labels/overlay)
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Tab buttons
        private UIObject _tabInProgress;
        private UIObject _tabCompleted;
        private UIObject _tabAvailable;

        // Quest lists by state
        private readonly Dictionary<int, List<QuestDisplayData>> questsByTab;

        // Selected quest for detail display
        private QuestDisplayData _selectedQuest;
        private int _selectedQuestIndex = -1;

        // Quest icon textures
        private Texture2D _iconAvailable;
        private Texture2D _iconInProgress;
        private Texture2D _iconCompleted;

        // Graphics device
        private GraphicsDevice _device;
        #endregion

        #region Properties
        public override string WindowName => "Quest";

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value >= TAB_IN_PROGRESS && value <= TAB_AVAILABLE)
                {
                    _currentTab = value;
                    _scrollOffset = 0;
                    _selectedQuestIndex = -1;
                    _selectedQuest = null;
                }
            }
        }
        #endregion

        #region Constructor
        public QuestUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;

            // Initialize quest data structures
            questsByTab = new Dictionary<int, List<QuestDisplayData>>
            {
                { TAB_IN_PROGRESS, new List<QuestDisplayData>() },
                { TAB_COMPLETED, new List<QuestDisplayData>() },
                { TAB_AVAILABLE, new List<QuestDisplayData>() }
            };
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the foreground texture (backgrnd2 - labels/overlay from UIWindow2.img/Quest/list)
        /// </summary>
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Initialize tab buttons
        /// </summary>
        public void InitializeTabs(UIObject inProgressTab, UIObject completedTab, UIObject availableTab)
        {
            this._tabInProgress = inProgressTab;
            this._tabCompleted = completedTab;
            this._tabAvailable = availableTab;

            if (inProgressTab != null)
            {
                AddButton(inProgressTab);
                inProgressTab.ButtonClickReleased += (sender) => CurrentTab = TAB_IN_PROGRESS;
            }
            if (completedTab != null)
            {
                AddButton(completedTab);
                completedTab.ButtonClickReleased += (sender) => CurrentTab = TAB_COMPLETED;
            }
            if (availableTab != null)
            {
                AddButton(availableTab);
                availableTab.ButtonClickReleased += (sender) => CurrentTab = TAB_AVAILABLE;
            }

            UpdateTabStates();
        }

        /// <summary>
        /// Set quest state icons
        /// </summary>
        public void SetQuestIcons(Texture2D available, Texture2D inProgress, Texture2D completed)
        {
            this._iconAvailable = available;
            this._iconInProgress = inProgress;
            this._iconCompleted = completed;
        }

        private void UpdateTabStates()
        {
            _tabInProgress?.SetButtonState(_currentTab == TAB_IN_PROGRESS ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCompleted?.SetButtonState(_currentTab == TAB_COMPLETED ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabAvailable?.SetButtonState(_currentTab == TAB_AVAILABLE ? UIObjectState.Pressed : UIObjectState.Normal);
        }
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            // Draw foreground (backgrnd2 - labels/overlay)
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Quest list would be drawn here when quests are added
        }
        #endregion

        #region Quest Management
        public void AddQuest(int questId, string questName, string description,
            QuestUI.QuestState state, int currentProgress = 0, int totalRequirements = 0)
        {
            int tab = state switch
            {
                QuestUI.QuestState.NotStarted => TAB_AVAILABLE,
                QuestUI.QuestState.InProgress => TAB_IN_PROGRESS,
                QuestUI.QuestState.Completed => TAB_COMPLETED,
                _ => TAB_AVAILABLE
            };

            if (questsByTab.TryGetValue(tab, out var quests))
            {
                var existing = quests.FirstOrDefault(q => q.QuestId == questId);
                if (existing != null)
                {
                    existing.QuestName = questName;
                    existing.Description = description;
                    existing.State = state;
                    existing.CurrentProgress = currentProgress;
                    existing.TotalRequirements = totalRequirements;
                }
                else
                {
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
            }
        }

        public void RemoveQuest(int questId)
        {
            foreach (var kvp in questsByTab)
            {
                var quest = kvp.Value.FirstOrDefault(q => q.QuestId == questId);
                if (quest != null)
                {
                    kvp.Value.Remove(quest);
                    if (_selectedQuest?.QuestId == questId)
                    {
                        _selectedQuest = null;
                        _selectedQuestIndex = -1;
                    }
                    break;
                }
            }
        }

        public void SelectQuest(int index)
        {
            if (questsByTab.TryGetValue(_currentTab, out var quests))
            {
                if (index >= 0 && index < quests.Count)
                {
                    _selectedQuestIndex = index;
                    _selectedQuest = quests[index];
                }
                else
                {
                    _selectedQuestIndex = -1;
                    _selectedQuest = null;
                }
            }
        }

        public void ClearQuests()
        {
            foreach (var kvp in questsByTab)
            {
                kvp.Value.Clear();
            }
            _selectedQuest = null;
            _selectedQuestIndex = -1;
        }

        public int GetQuestCount(int tab)
        {
            return questsByTab.TryGetValue(tab, out var quests) ? quests.Count : 0;
        }

        public void ScrollUp()
        {
            if (_scrollOffset > 0)
                _scrollOffset--;
        }

        public void ScrollDown()
        {
            if (questsByTab.TryGetValue(_currentTab, out var quests))
            {
                int maxScroll = Math.Max(0, quests.Count - VISIBLE_QUESTS);
                if (_scrollOffset < maxScroll)
                    _scrollOffset++;
            }
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateTabStates();
        }
        #endregion
    }
}
