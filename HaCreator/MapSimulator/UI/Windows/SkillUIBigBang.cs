using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
        // From v95 CUISkill::Draw / OnCreate / OnMouseMove analysis
        private const int SKILL_ICON_SIZE = 32;
        private const int SKILL_ROW_HEIGHT = 40;
        private const int VISIBLE_SKILLS = 4;

        // Window dimensions (from UIWindow2.img/Skill/main/backgrnd)
        private const int WINDOW_WIDTH = 174;
        private const int WINDOW_HEIGHT = 299;

        // Book header positioning
        private const int BOOK_ICON_X = 15;
        private const int BOOK_ICON_Y = 55;
        private const int JOB_ICON_SIZE = 32;
        private const int BOOK_NAME_CENTER_X = 104;
        private const int BOOK_NAME_SINGLE_LINE_Y = 65;
        private const int BOOK_NAME_MULTI_LINE_X = 50;
        private const int BOOK_NAME_MULTI_LINE_FIRST_Y = 55;
        private const int BOOK_NAME_MULTI_LINE_SECOND_Y = 69;
        private const int BOOK_NAME_MAX_WIDTH = 110;

        // Skill list positioning
        private const int FIRST_ROW_TOP = 112;
        private const int ICON_X = 12;
        private const int ICON_Y_OFFSET = -17;
        private const int NAME_X = 50;
        private const int NAME_Y_OFFSET = -18;
        private const int LEVEL_X = 50;
        private const int LEVEL_Y_OFFSET = 0;
        private const int BONUS_X = 65;
        private const int ROW_BG_X = 10;
        private const int ROW_BG_Y_OFFSET = -19;
        private const int LINE_X = 10;
        private const int LINE_Y_OFFSET = 18;

        // SP display
        private const int SP_DISPLAY_X_BASE = 104;
        private const int SP_DISPLAY_Y = 256;

        // Hit testing
        private const int ICON_HIT_LEFT = 13;
        private const int ICON_HIT_RIGHT = 45;
        private const int ICON_HIT_TOP_OFFSET = -31;
        private const int ICON_HIT_BOTTOM_OFFSET = 1;
        private const int ROW_HIT_LEFT = 10;
        private const int ROW_HIT_RIGHT = 149;
        private const int ROW_HIT_TOP_OFFSET = -34;
        private const int ROW_HIT_BOTTOM_OFFSET = 0;

        // Tab positioning (from WZ origin data)
        private const int TAB_Y = 27;

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
        private int _hoveredSkillIndex = -1;

        // Foreground textures
        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _skillListBackground;
        private Point _skillListBgOffset;

        // Skill row backgrounds (normal / can-level-up)
        private Texture2D _skillRow0;
        private Texture2D _skillRow1;
        private Texture2D _skillRowLine;

        // Tab textures (0=Beginner, 1-4=Job advancements)
        private Texture2D[] _tabEnabled = new Texture2D[5];
        private Texture2D[] _tabDisabled = new Texture2D[5];
        private Rectangle[] _tabRects = new Rectangle[5];

        // SP Up button textures
        private Texture2D _spUpNormal;
        private Texture2D _spUpPressed;
        private Texture2D _spUpDisabled;
        private Texture2D _spUpMouseOver;

        // Job header info (icon + name per tab)
        private readonly Dictionary<int, Texture2D> _jobIconsByTab;
        private readonly Dictionary<int, string> _jobNamesByTab;

        // Macro button
        private UIObject _btnMacro;

        /// <summary>
        /// Gets the macro button for external event wiring
        /// </summary>
        public UIObject MacroButton => _btnMacro;

        // Skills organized by job advancement level
        private readonly Dictionary<int, List<SkillDisplayData>> skillsByTab;

        // Skill points available per tab
        private readonly Dictionary<int, int> skillPointsByTab;

        // Graphics device
        private GraphicsDevice _device;

        // Font for rendering text
        private SpriteFont _font;

        // Mouse state
        private MouseState _previousMouseState;

        // Placeholder texture (1x1 white pixel for drawing colored rects)
        private Texture2D _debugPlaceholder;
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
                    _hoveredSkillIndex = -1;
                }
            }
        }

        /// <summary>
        /// Get skills for the current tab
        /// </summary>
        public List<SkillDisplayData> CurrentSkills
        {
            get
            {
                if (skillsByTab.TryGetValue(_currentTab, out var skills))
                    return skills;
                return new List<SkillDisplayData>();
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

            skillPointsByTab = new Dictionary<int, int>
            {
                { TAB_BEGINNER, 1 },
                { TAB_1ST, 0 },
                { TAB_2ND, 0 },
                { TAB_3RD, 0 },
                { TAB_4TH, 0 }
            };

            // Initialize job header data
            _jobIconsByTab = new Dictionary<int, Texture2D>
            {
                { TAB_BEGINNER, null },
                { TAB_1ST, null },
                { TAB_2ND, null },
                { TAB_3RD, null },
                { TAB_4TH, null }
            };
            _jobNamesByTab = new Dictionary<int, string>
            {
                { TAB_BEGINNER, "Beginner" },
                { TAB_1ST, "1st Job" },
                { TAB_2ND, "2nd Job" },
                { TAB_3RD, "3rd Job" },
                { TAB_4TH, "4th Job" }
            };

            // Initialize tab rectangles (positions relative to window)
            for (int i = 0; i < 5; i++)
            {
                _tabRects[i] = new Rectangle(10 + (i * 31), TAB_Y, 30, 20);
            }

            // Create debug placeholder texture (1x1 white pixel)
            _debugPlaceholder = new Texture2D(device, 1, 1);
            _debugPlaceholder.SetData(new[] { Color.White });
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
        /// Set the skill list background (backgrnd3)
        /// </summary>
        public void SetSkillListBackground(IDXObject background, int offsetX, int offsetY)
        {
            _skillListBackground = background;
            _skillListBgOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Set skill row textures (skill0, skill1 - alternating row backgrounds)
        /// </summary>
        public void SetSkillRowTextures(Texture2D row0, Texture2D row1, Texture2D line)
        {
            _skillRow0 = row0;
            _skillRow1 = row1;
            _skillRowLine = line;
        }

        /// <summary>
        /// Set tab button textures
        /// </summary>
        public void SetTabTextures(Texture2D[] enabled, Texture2D[] disabled)
        {
            if (enabled != null)
            {
                for (int i = 0; i < Math.Min(5, enabled.Length); i++)
                    _tabEnabled[i] = enabled[i];
            }
            if (disabled != null)
            {
                for (int i = 0; i < Math.Min(5, disabled.Length); i++)
                    _tabDisabled[i] = disabled[i];
            }
        }

        /// <summary>
        /// Set SP Up button textures
        /// </summary>
        public void SetSpUpTextures(Texture2D normal, Texture2D pressed, Texture2D disabled, Texture2D mouseOver)
        {
            _spUpNormal = normal;
            _spUpPressed = pressed;
            _spUpDisabled = disabled;
            _spUpMouseOver = mouseOver;
        }

        /// <summary>
        /// Initialize job advancement tab buttons (legacy method for UIObject-based tabs)
        /// </summary>
        public void InitializeTabs(UIObject beginnerTab, UIObject tab1, UIObject tab2, UIObject tab3, UIObject tab4)
        {
            // Tab buttons are now rendered as textures, but we keep this for compatibility
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

        /// <summary>
        /// Set the font for rendering skill names and levels
        /// </summary>
        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        /// <summary>
        /// Set job header info for a specific tab
        /// </summary>
        /// <param name="tab">Tab index (0=Beginner, 1-4=job advancements)</param>
        /// <param name="jobIcon">The job/skillbook icon texture</param>
        /// <param name="jobName">The job name to display</param>
        public void SetJobInfo(int tab, Texture2D jobIcon, string jobName)
        {
            int tabIndex = Math.Clamp(tab, TAB_BEGINNER, TAB_4TH);
            _jobIconsByTab[tabIndex] = jobIcon;
            if (!string.IsNullOrEmpty(jobName))
            {
                _jobNamesByTab[tabIndex] = jobName;
            }
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

            // Draw skill list background (backgrnd3)
            if (_skillListBackground != null)
            {
                _skillListBackground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _skillListBgOffset.X, windowY + _skillListBgOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Draw foreground/labels before custom skill content, matching the client
            // where the base window chrome is already present before CUISkill::Draw runs.
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Draw tab buttons
            DrawTabs(sprite, windowX, windowY);

            // Draw book header
            DrawJobHeader(sprite, windowX, windowY);

            // Draw skill rows
            DrawSkillRows(sprite, windowX, windowY);

            // Draw SP count
            DrawSkillPointCount(sprite, windowX, windowY);
        }

        /// <summary>
        /// Draw the job header row with job icon and name
        /// </summary>
        private void DrawJobHeader(SpriteBatch sprite, int windowX, int windowY)
        {
            // Get job icon and name for current tab
            Texture2D jobIcon = null;
            string jobName = "Beginner";

            if (_jobIconsByTab.TryGetValue(_currentTab, out var icon))
                jobIcon = icon;
            if (_jobNamesByTab.TryGetValue(_currentTab, out var name))
                jobName = name;

            if (jobIcon == null)
            {
                _jobIconsByTab.TryGetValue(TAB_BEGINNER, out jobIcon);
            }

            if (jobIcon != null)
            {
                sprite.Draw(
                    jobIcon,
                    new Rectangle(windowX + BOOK_ICON_X, windowY + BOOK_ICON_Y, JOB_ICON_SIZE, JOB_ICON_SIZE),
                    Color.White);
            }

            DrawBookName(sprite, windowX, windowY, jobName);
        }

        /// <summary>
        /// Draw the skill rows with icons, names, and levels
        /// </summary>
        private void DrawSkillRows(SpriteBatch sprite, int windowX, int windowY)
        {
            var skills = CurrentSkills;
            int availableSp = GetCurrentSkillPoints();

            for (int rowIndex = 0; rowIndex < VISIBLE_SKILLS; rowIndex++)
            {
                int skillIndex = _scrollOffset + rowIndex;
                if (skillIndex >= skills.Count)
                    break;

                int nTop = FIRST_ROW_TOP + (rowIndex * SKILL_ROW_HEIGHT);
                SkillDisplayData skill = skills[skillIndex];
                bool canLevelUp = availableSp > 0 && skill.CurrentLevel < skill.MaxLevel;

                DrawSkillEntry(sprite, skill, windowX, windowY, nTop, canLevelUp, skillIndex == _hoveredSkillIndex);

                if (_skillRowLine != null && rowIndex < VISIBLE_SKILLS - 1 && skillIndex + 1 < skills.Count)
                {
                    sprite.Draw(_skillRowLine, new Vector2(windowX + LINE_X, windowY + nTop + LINE_Y_OFFSET), Color.White);
                }
            }
        }

        /// <summary>
        /// Draw a single skill entry
        /// </summary>
        private void DrawSkillEntry(
            SpriteBatch sprite,
            SkillDisplayData skill,
            int windowX,
            int windowY,
            int nTop,
            bool canLevelUp,
            bool isHovered)
        {
            Texture2D rowBg = canLevelUp ? _skillRow1 : _skillRow0;
            if (rowBg != null)
            {
                sprite.Draw(rowBg, new Vector2(windowX + ROW_BG_X, windowY + nTop + ROW_BG_Y_OFFSET), Color.White);
            }

            Texture2D icon = skill.GetIconForState(canLevelUp, isHovered);
            int iconX = windowX + ICON_X;
            int iconY = windowY + nTop + ICON_Y_OFFSET;

            if (icon != null)
            {
                Rectangle iconRect = new Rectangle(iconX, iconY, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
                sprite.Draw(icon, iconRect, Color.White);
            }

            if (_font != null)
            {
                sprite.DrawString(
                    _font,
                    skill.SkillName ?? string.Empty,
                    new Vector2(windowX + NAME_X, windowY + nTop + NAME_Y_OFFSET),
                    Color.Black);

                Color levelColor = canLevelUp ? new Color(0, 102, 255) : Color.Black;
                sprite.DrawString(
                    _font,
                    skill.CurrentLevel.ToString(),
                    new Vector2(windowX + LEVEL_X, windowY + nTop + LEVEL_Y_OFFSET),
                    levelColor);
            }

            if (_spUpNormal != null && canLevelUp)
            {
                sprite.Draw(_spUpNormal, new Vector2(windowX + 135, windowY + nTop + 1), Color.White);
            }
        }

        private void DrawSkillPointCount(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_font == null)
                return;

            string spText = GetCurrentSkillPoints().ToString();
            float width = _font.MeasureString(spText).X;
            sprite.DrawString(
                _font,
                spText,
                new Vector2(windowX + SP_DISPLAY_X_BASE - width, windowY + SP_DISPLAY_Y),
                Color.Black);
        }

        private void DrawBookName(SpriteBatch sprite, int windowX, int windowY, string jobName)
        {
            if (_font == null || string.IsNullOrWhiteSpace(jobName))
                return;

            float width = _font.MeasureString(jobName).X;
            if (width <= BOOK_NAME_MAX_WIDTH)
            {
                sprite.DrawString(
                    _font,
                    jobName,
                    new Vector2(windowX + BOOK_NAME_CENTER_X - (width / 2f), windowY + BOOK_NAME_SINGLE_LINE_Y),
                    Color.Black);
                return;
            }

            int splitIndex = jobName.LastIndexOf(' ');
            if (splitIndex <= 0 || splitIndex >= jobName.Length - 1)
            {
                sprite.DrawString(
                    _font,
                    jobName,
                    new Vector2(windowX + BOOK_NAME_MULTI_LINE_X, windowY + BOOK_NAME_MULTI_LINE_FIRST_Y),
                    Color.Black);
                return;
            }

            sprite.DrawString(
                _font,
                jobName.Substring(0, splitIndex),
                new Vector2(windowX + BOOK_NAME_MULTI_LINE_X, windowY + BOOK_NAME_MULTI_LINE_FIRST_Y),
                Color.Black);
            sprite.DrawString(
                _font,
                jobName.Substring(splitIndex + 1),
                new Vector2(windowX + BOOK_NAME_MULTI_LINE_X, windowY + BOOK_NAME_MULTI_LINE_SECOND_Y),
                Color.Black);
        }

        /// <summary>
        /// Draw tab buttons
        /// </summary>
        private void DrawTabs(SpriteBatch sprite, int windowX, int windowY)
        {
            for (int i = 0; i < 5; i++)
            {
                Texture2D tabTexture = (i == _currentTab) ? _tabEnabled[i] : _tabDisabled[i];
                if (tabTexture != null)
                {
                    Rectangle tabRect = new Rectangle(
                        windowX + _tabRects[i].X,
                        windowY + _tabRects[i].Y,
                        tabTexture.Width,
                        tabTexture.Height);
                    sprite.Draw(tabTexture, tabRect, Color.White);
                }
            }
        }
        #endregion

        #region Skill Management
        /// <summary>
        /// Add a skill to the specified job advancement tab
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
        /// Add a skill display data to the specified tab
        /// </summary>
        public void AddSkill(int jobAdvancement, SkillDisplayData skillData)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);

            if (skillsByTab.TryGetValue(tab, out var skills))
            {
                skills.Add(skillData);
            }
        }

        /// <summary>
        /// Add multiple skills to the specified tab
        /// </summary>
        public void AddSkills(int jobAdvancement, IEnumerable<SkillDisplayData> skillDataList)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);

            if (skillsByTab.TryGetValue(tab, out var skills))
            {
                int beforeCount = skills.Count;
                skills.AddRange(skillDataList);
                System.Diagnostics.Debug.WriteLine($"[SkillUIBigBang] Added skills to tab {tab}: was {beforeCount}, now {skills.Count}");
            }
        }

        /// <summary>
        /// Set skill points for a job advancement tab
        /// </summary>
        public void SetSkillPoints(int jobAdvancement, int points)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);
            skillPointsByTab[tab] = Math.Max(0, points);
        }

        /// <summary>
        /// Get skill points for current tab
        /// </summary>
        public int GetCurrentSkillPoints()
        {
            if (skillPointsByTab.TryGetValue(_currentTab, out int points))
                return points;
            return 0;
        }

        /// <summary>
        /// Clear all skills from all tabs
        /// </summary>
        public void ClearSkills()
        {
            foreach (var kvp in skillsByTab)
            {
                kvp.Value.Clear();
            }
        }

        /// <summary>
        /// Clear skills from a specific tab
        /// </summary>
        public void ClearSkills(int jobAdvancement)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);
            if (skillsByTab.TryGetValue(tab, out var skills))
            {
                skills.Clear();
            }
        }

        /// <summary>
        /// Scroll up in the skill list
        /// </summary>
        public void ScrollUp()
        {
            if (_scrollOffset > 0)
                _scrollOffset--;
        }

        /// <summary>
        /// Scroll down in the skill list
        /// </summary>
        public void ScrollDown()
        {
            var skills = CurrentSkills;
            int maxScroll = Math.Max(0, skills.Count - VISIBLE_SKILLS);
            if (_scrollOffset < maxScroll)
                _scrollOffset++;
        }

        /// <summary>
        /// Get skill at a position relative to window
        /// </summary>
        public SkillDisplayData GetSkillAtPosition(int mouseX, int mouseY)
        {
            int skillIndex = GetSkillIndexAtPosition(mouseX, mouseY);
            if (skillIndex < 0)
                return null;

            var skills = CurrentSkills;
            return skillIndex < skills.Count ? skills[skillIndex] : null;
        }

        private int GetSkillIndexAtPosition(int mouseX, int mouseY)
        {
            int relX = mouseX - Position.X;
            int relY = mouseY - Position.Y;
            var skills = CurrentSkills;

            for (int rowIndex = 0; rowIndex < VISIBLE_SKILLS; rowIndex++)
            {
                int skillIndex = _scrollOffset + rowIndex;
                if (skillIndex >= skills.Count)
                    break;

                int nTop = FIRST_ROW_TOP + (rowIndex * SKILL_ROW_HEIGHT);

                bool hitIcon =
                    relX >= ICON_HIT_LEFT &&
                    relX <= ICON_HIT_RIGHT &&
                    relY >= nTop + ICON_HIT_TOP_OFFSET &&
                    relY <= nTop + ICON_HIT_BOTTOM_OFFSET;
                if (hitIcon)
                    return skillIndex;

                bool hitRow =
                    relX >= ROW_HIT_LEFT &&
                    relX <= ROW_HIT_RIGHT &&
                    relY >= nTop + ROW_HIT_TOP_OFFSET &&
                    relY <= nTop + ROW_HIT_BOTTOM_OFFSET;
                if (hitRow)
                    return skillIndex;
            }

            return -1;
        }
        #endregion

        #region Input Handling
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible)
                return;

            MouseState mouseState = Mouse.GetState();

            // Handle tab clicks
            if (mouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                for (int i = 0; i < 5; i++)
                {
                    Rectangle tabRect = new Rectangle(
                        Position.X + _tabRects[i].X,
                        Position.Y + _tabRects[i].Y,
                        _tabRects[i].Width,
                        _tabRects[i].Height);

                    if (tabRect.Contains(mouseState.X, mouseState.Y))
                    {
                        CurrentTab = i;
                        break;
                    }
                }
            }

            // Handle scroll wheel
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                if (GetSkillIndexAtPosition(mouseState.X, mouseState.Y) >= 0)
                {
                    if (scrollDelta > 0)
                        ScrollUp();
                    else
                        ScrollDown();
                }
            }

            // Update hovered skill index
            _hoveredSkillIndex = GetSkillIndexAtPosition(mouseState.X, mouseState.Y);

            _previousMouseState = mouseState;
        }
        #endregion
    }
}
