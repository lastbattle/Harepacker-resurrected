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
    /// Skill UI window displaying skills organized by job advancement
    /// Structure: UI.wz/UIWindow.img/Skill/
    /// This is a view-only window for the simulator
    /// </summary>
    public class SkillUI : UIWindowBase
    {
        #region Constants
        private const int SKILL_ICON_SIZE = 32;
        private const int SKILL_PADDING = 8;
        private const int SKILLS_PER_ROW = 4;
        private const int VISIBLE_ROWS = 4;

        // Job advancement tabs
        private const int TAB_BEGINNER = 0;
        private const int TAB_1ST = 1;
        private const int TAB_2ND = 2;
        private const int TAB_3RD = 3;
        private const int TAB_4TH = 4;
        #endregion

        #region Fields
        private int _currentTab = TAB_BEGINNER;
        private int _scrollOffset = 0;

        // Tab buttons
        private UIObject _tabBeginner;
        private UIObject _tab1st;
        private UIObject _tab2nd;
        private UIObject _tab3rd;
        private UIObject _tab4th;

        // Empty skill slot texture
        private Texture2D _emptySlotTexture;

        // Skills organized by job advancement level
        private readonly Dictionary<int, List<SkillDisplayData>> skillsByTab;

        // Skill point display
        private readonly Dictionary<int, int> skillPoints; // tab -> SP available

        // Selected skill for description display
        private SkillDisplayData _selectedSkill;
        private int _selectedSkillIndex = -1;

        // Skill description area
        private Rectangle _descriptionRect;
        #endregion

        #region Properties
        public override string WindowName => "Skills";

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value >= TAB_BEGINNER && value <= TAB_4TH)
                {
                    _currentTab = value;
                    _scrollOffset = 0;
                    _selectedSkillIndex = -1;
                    _selectedSkill = null;
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
        public SkillUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {

            // Initialize skill data structures
            skillsByTab = new Dictionary<int, List<SkillDisplayData>>
            {
                { TAB_BEGINNER, new List<SkillDisplayData>() },
                { TAB_1ST, new List<SkillDisplayData>() },
                { TAB_2ND, new List<SkillDisplayData>() },
                { TAB_3RD, new List<SkillDisplayData>() },
                { TAB_4TH, new List<SkillDisplayData>() }
            };

            skillPoints = new Dictionary<int, int>
            {
                { TAB_BEGINNER, 0 },
                { TAB_1ST, 0 },
                { TAB_2ND, 0 },
                { TAB_3RD, 0 },
                { TAB_4TH, 0 }
            };

            // Description area at bottom of window
            _descriptionRect = new Rectangle(10, 200, 180, 60);

            // Create empty slot texture
            CreateEmptySlotTexture(device);
        }

        private void CreateEmptySlotTexture(GraphicsDevice device)
        {
            _emptySlotTexture = new Texture2D(device, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
            Color[] data = new Color[SKILL_ICON_SIZE * SKILL_ICON_SIZE];

            Color slotColor = new Color(35, 35, 55, 100);
            Color borderColor = new Color(65, 65, 85, 150);

            for (int y = 0; y < SKILL_ICON_SIZE; y++)
            {
                for (int x = 0; x < SKILL_ICON_SIZE; x++)
                {
                    if (x == 0 || x == SKILL_ICON_SIZE - 1 || y == 0 || y == SKILL_ICON_SIZE - 1)
                    {
                        data[y * SKILL_ICON_SIZE + x] = borderColor;
                    }
                    else
                    {
                        data[y * SKILL_ICON_SIZE + x] = slotColor;
                    }
                }
            }
            _emptySlotTexture.SetData(data);
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize job advancement tab buttons
        /// </summary>
        public void InitializeTabs(UIObject beginnerTab, UIObject tab1, UIObject tab2, UIObject tab3, UIObject tab4)
        {
            this._tabBeginner = beginnerTab;
            this._tab1st = tab1;
            this._tab2nd = tab2;
            this._tab3rd = tab3;
            this._tab4th = tab4;

            if (beginnerTab != null)
            {
                AddButton(beginnerTab);
                beginnerTab.ButtonClickReleased += (sender) => CurrentTab = TAB_BEGINNER;
            }
            if (tab1 != null)
            {
                AddButton(tab1);
                tab1.ButtonClickReleased += (sender) => CurrentTab = TAB_1ST;
            }
            if (tab2 != null)
            {
                AddButton(tab2);
                tab2.ButtonClickReleased += (sender) => CurrentTab = TAB_2ND;
            }
            if (tab3 != null)
            {
                AddButton(tab3);
                tab3.ButtonClickReleased += (sender) => CurrentTab = TAB_3RD;
            }
            if (tab4 != null)
            {
                AddButton(tab4);
                tab4.ButtonClickReleased += (sender) => CurrentTab = TAB_4TH;
            }

            UpdateTabStates();
        }

        private void UpdateTabStates()
        {
            _tabBeginner?.SetButtonState(_currentTab == TAB_BEGINNER ? UIObjectState.Pressed : UIObjectState.Normal);
            _tab1st?.SetButtonState(_currentTab == TAB_1ST ? UIObjectState.Pressed : UIObjectState.Normal);
            _tab2nd?.SetButtonState(_currentTab == TAB_2ND ? UIObjectState.Pressed : UIObjectState.Normal);
            _tab3rd?.SetButtonState(_currentTab == TAB_3RD ? UIObjectState.Pressed : UIObjectState.Normal);
            _tab4th?.SetButtonState(_currentTab == TAB_4TH ? UIObjectState.Pressed : UIObjectState.Normal);
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw skill window contents
        /// </summary>
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Skill slot content is rendered as part of the window background texture from UI.wz
            // Skill icons would be drawn here when skills are added
            // For now, the window frame from UI.wz already includes the slot grid visuals
        }
        #endregion

        #region Skill Management
        /// <summary>
        /// Add a skill to the skill window (for simulation)
        /// </summary>
        public void AddSkill(int jobAdvancement, int skillId, Texture2D iconTexture,
            string skillName, string description, int currentLevel = 0, int maxLevel = 10)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);

            if (skillsByTab.TryGetValue(tab, out var skills))
            {
                skills.Add(new SkillDisplayData
                {
                    SkillId = skillId,
                    IconTexture = iconTexture,
                    SkillName = skillName,
                    Description = description,
                    CurrentLevel = currentLevel,
                    MaxLevel = maxLevel
                });
            }
        }

        /// <summary>
        /// Set skill points for a job advancement tab
        /// </summary>
        public void SetSkillPoints(int jobAdvancement, int points)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);
            skillPoints[tab] = Math.Max(0, points);
        }

        /// <summary>
        /// Clear all skills
        /// </summary>
        public void ClearSkills()
        {
            foreach (var kvp in skillsByTab)
            {
                kvp.Value.Clear();
            }
            _selectedSkill = null;
            _selectedSkillIndex = -1;
        }

        /// <summary>
        /// Select a skill by index
        /// </summary>
        public void SelectSkill(int index)
        {
            if (skillsByTab.TryGetValue(_currentTab, out var skills))
            {
                if (index >= 0 && index < skills.Count)
                {
                    _selectedSkillIndex = index;
                    _selectedSkill = skills[index];
                }
                else
                {
                    _selectedSkillIndex = -1;
                    _selectedSkill = null;
                }
            }
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
            if (skillsByTab.TryGetValue(_currentTab, out var skills))
            {
                int maxScroll = Math.Max(0, (skills.Count / SKILLS_PER_ROW) - VISIBLE_ROWS + 1);
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
    /// Data for displaying a skill
    /// </summary>
    public class SkillDisplayData
    {
        public int SkillId { get; set; }
        public Texture2D IconTexture { get; set; }
        public string SkillName { get; set; }
        public string Description { get; set; }
        public int CurrentLevel { get; set; }
        public int MaxLevel { get; set; }

        // Skill properties for tooltip
        public int Damage { get; set; }
        public int MPCost { get; set; }
        public int Cooldown { get; set; }
        public int Range { get; set; }
        public int MobCount { get; set; }
    }
}
