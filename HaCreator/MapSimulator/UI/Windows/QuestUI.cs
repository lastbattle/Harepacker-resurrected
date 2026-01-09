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
    /// Quest UI window displaying quest log and quest information
    /// Structure: UI.wz/UIWindow.img/Quest/
    /// </summary>
    public class QuestUI : UIWindowBase
    {
        #region Constants
        private const int QUEST_ENTRY_HEIGHT = 24;
        private const int VISIBLE_QUESTS = 8;

        // Quest state tabs
        private const int TAB_IN_PROGRESS = 0;
        private const int TAB_COMPLETED = 1;
        private const int TAB_AVAILABLE = 2;
        #endregion

        #region Quest State Enum
        public enum QuestState
        {
            NotStarted = 0,
            InProgress = 1,
            Completed = 2
        }
        #endregion

        #region Fields
        private int _currentTab = TAB_IN_PROGRESS;
        private int _scrollOffset = 0;

        // Tab buttons
        private UIObject _tabInProgress;
        private UIObject _tabCompleted;
        private UIObject _tabAvailable;

        // Quest lists by state
        private readonly Dictionary<int, List<QuestDisplayData>> questsByTab;

        // Selected quest for detail display
        private QuestDisplayData _selectedQuest;
        private int _selectedQuestIndex = -1;

        // Detail panel areas
        private Rectangle _questListRect;
        private Rectangle _questDetailRect;

        // Empty list indicator texture
        private Texture2D _emptyIndicator;

        // Selection highlight texture
        private Texture2D _selectionHighlight;

        // Quest icon textures (from UI.wz/UIWindow.img/QuestIcon)
        private Texture2D _iconAvailable;
        private Texture2D _iconInProgress;
        private Texture2D _iconCompleted;
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
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="frame">Window background frame</param>
        /// <param name="device">Graphics device</param>
        public QuestUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {

            // Initialize quest data structures
            questsByTab = new Dictionary<int, List<QuestDisplayData>>
            {
                { TAB_IN_PROGRESS, new List<QuestDisplayData>() },
                { TAB_COMPLETED, new List<QuestDisplayData>() },
                { TAB_AVAILABLE, new List<QuestDisplayData>() }
            };

            // Quest list area (left side)
            _questListRect = new Rectangle(10, 55, 150, VISIBLE_QUESTS * QUEST_ENTRY_HEIGHT);

            // Quest detail area (right side or below)
            _questDetailRect = new Rectangle(10, 260, 200, 80);

            // Create textures
            CreateSelectionHighlight(device);
        }

        private void CreateSelectionHighlight(GraphicsDevice device)
        {
            _selectionHighlight = new Texture2D(device, 1, 1);
            _selectionHighlight.SetData(new[] { new Color(100, 150, 200, 100) });
        }
        #endregion

        #region Initialization
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
        /// <summary>
        /// Draw quest window contents
        /// </summary>
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Quest list content is rendered as part of the window background texture from UI.wz
            // Quest entries would be drawn here when quests are added
            // For now, the window frame from UI.wz already includes the list area visuals
        }

        private Texture2D GetQuestStateIcon(QuestState state)
        {
            return state switch
            {
                QuestState.NotStarted => _iconAvailable,
                QuestState.InProgress => _iconInProgress,
                QuestState.Completed => _iconCompleted,
                _ => null
            };
        }
        #endregion

        #region Quest Management
        /// <summary>
        /// Add a quest to the quest log
        /// </summary>
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

            if (questsByTab.TryGetValue(tab, out var quests))
            {
                // Check if quest already exists
                var existing = quests.FirstOrDefault(q => q.QuestId == questId);
                if (existing != null)
                {
                    // Update existing quest
                    existing.QuestName = questName;
                    existing.Description = description;
                    existing.State = state;
                    existing.CurrentProgress = currentProgress;
                    existing.TotalRequirements = totalRequirements;
                }
                else
                {
                    // Add new quest
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

        /// <summary>
        /// Update quest progress
        /// </summary>
        public void UpdateQuestProgress(int questId, int currentProgress, int totalRequirements)
        {
            foreach (var kvp in questsByTab)
            {
                var quest = kvp.Value.FirstOrDefault(q => q.QuestId == questId);
                if (quest != null)
                {
                    quest.CurrentProgress = currentProgress;
                    quest.TotalRequirements = totalRequirements;

                    // Check if quest is completed
                    if (currentProgress >= totalRequirements && quest.State == QuestState.InProgress)
                    {
                        MoveQuestToTab(questId, TAB_COMPLETED);
                        quest.State = QuestState.Completed;
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Move a quest to a different tab (state change)
        /// </summary>
        public void MoveQuestToTab(int questId, int newTab)
        {
            QuestDisplayData questToMove = null;
            int sourceTab = -1;

            // Find the quest
            foreach (var kvp in questsByTab)
            {
                var quest = kvp.Value.FirstOrDefault(q => q.QuestId == questId);
                if (quest != null)
                {
                    questToMove = quest;
                    sourceTab = kvp.Key;
                    break;
                }
            }

            if (questToMove != null && sourceTab >= 0 && sourceTab != newTab)
            {
                questsByTab[sourceTab].Remove(questToMove);
                questsByTab[newTab].Add(questToMove);

                // Update state
                questToMove.State = newTab switch
                {
                    TAB_AVAILABLE => QuestState.NotStarted,
                    TAB_IN_PROGRESS => QuestState.InProgress,
                    TAB_COMPLETED => QuestState.Completed,
                    _ => questToMove.State
                };
            }
        }

        /// <summary>
        /// Remove a quest from the log
        /// </summary>
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

        /// <summary>
        /// Select a quest by index
        /// </summary>
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

        /// <summary>
        /// Clear all quests
        /// </summary>
        public void ClearQuests()
        {
            foreach (var kvp in questsByTab)
            {
                kvp.Value.Clear();
            }
            _selectedQuest = null;
            _selectedQuestIndex = -1;
        }

        /// <summary>
        /// Get quest count for a tab
        /// </summary>
        public int GetQuestCount(int tab)
        {
            return questsByTab.TryGetValue(tab, out var quests) ? quests.Count : 0;
        }

        /// <summary>
        /// Scroll up
        /// </summary>
        public void ScrollUp()
        {
            if (_scrollOffset > 0)
                _scrollOffset--;
        }

        /// <summary>
        /// Scroll down
        /// </summary>
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

    /// <summary>
    /// Data for displaying a quest in the UI
    /// </summary>
    public class QuestDisplayData
    {
        public int QuestId { get; set; }
        public string QuestName { get; set; }
        public string Description { get; set; }
        public QuestUI.QuestState State { get; set; }
        public int CurrentProgress { get; set; }
        public int TotalRequirements { get; set; }

        // Reward information
        public int ExpReward { get; set; }
        public int MesoReward { get; set; }
        public int FameReward { get; set; }
        public List<QuestItemReward> ItemRewards { get; set; } = new List<QuestItemReward>();

        // NPC information
        public int StartNpcId { get; set; }
        public int EndNpcId { get; set; }
        public string StartNpcName { get; set; }
        public string EndNpcName { get; set; }

        // Requirements
        public List<QuestRequirement> Requirements { get; set; } = new List<QuestRequirement>();
    }

    /// <summary>
    /// Quest item reward data
    /// </summary>
    public class QuestItemReward
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public Texture2D ItemIcon { get; set; }
    }

    /// <summary>
    /// Quest requirement data
    /// </summary>
    public class QuestRequirement
    {
        public string RequirementType { get; set; } // "mob", "item", "npc", "level"
        public int TargetId { get; set; }
        public int RequiredCount { get; set; }
        public int CurrentCount { get; set; }
        public string Description { get; set; }
    }
}
