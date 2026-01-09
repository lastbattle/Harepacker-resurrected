using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Skill UI window for post-Big Bang MapleStory (v100+)
    /// Structure: UI.wz/UIWindow2.img/Skill/main
    /// Window dimensions: 174x299 pixels
    /// </summary>
    public class SkillUIBigBang : UIWindowBase
    {
        #region Constants
        private const int SKILL_ICON_SIZE = 32;
        private const int SKILL_PADDING = 8;
        private const int SKILLS_PER_ROW = 4;
        private const int VISIBLE_ROWS = 4;

        // Window dimensions (from UIWindow2.img/Skill/main/backgrnd)
        private const int WINDOW_WIDTH = 174;
        private const int WINDOW_HEIGHT = 299;

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

        // Foreground texture (backgrnd2 - labels/overlay)
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Tab buttons
        private UIObject _tabBeginner;
        private UIObject _tab1st;
        private UIObject _tab2nd;
        private UIObject _tab3rd;
        private UIObject _tab4th;

        // Macro button
        private UIObject _btnMacro;

        // Skills organized by job advancement level
        private readonly Dictionary<int, List<SkillDisplayData>> skillsByTab;

        // Graphics device
        private GraphicsDevice _device;
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
                }
            }
        }
        #endregion

        #region Constructor
        public SkillUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;

            // Initialize skill data structures
            skillsByTab = new Dictionary<int, List<SkillDisplayData>>
            {
                { TAB_BEGINNER, new List<SkillDisplayData>() },
                { TAB_1ST, new List<SkillDisplayData>() },
                { TAB_2ND, new List<SkillDisplayData>() },
                { TAB_3RD, new List<SkillDisplayData>() },
                { TAB_4TH, new List<SkillDisplayData>() }
            };
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the foreground texture (backgrnd2 - labels/overlay from UIWindow2.img/Skill/main)
        /// </summary>
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

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

        /// <summary>
        /// Initialize macro button
        /// </summary>
        public void InitializeMacroButton(UIObject btnMacro)
        {
            _btnMacro = btnMacro;
            if (btnMacro != null)
            {
                AddButton(btnMacro);
            }
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

            // Skill icons would be drawn here when skills are added
        }
        #endregion

        #region Skill Management
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

        public void ClearSkills()
        {
            foreach (var kvp in skillsByTab)
            {
                kvp.Value.Clear();
            }
        }

        public void ScrollUp()
        {
            if (_scrollOffset > 0)
                _scrollOffset--;
        }

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
}
