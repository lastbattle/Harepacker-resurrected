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
        // From CUISkill::Draw binary analysis
        private const int SKILL_ICON_SIZE = 32;
        private const int SKILL_ROW_HEIGHT = 40;      // Binary: 40px per row
        private const int SKILL_ROW_WIDTH = 139;      // Binary: hit area width
        private const int VISIBLE_SKILLS = 4;         // Binary: 4 visible rows

        // Window dimensions (from UIWindow2.img/Skill/main/backgrnd)
        private const int WINDOW_WIDTH = 174;
        private const int WINDOW_HEIGHT = 299;

        // Job header row positioning (first row shows job icon and name)
        // Binary: first skill row at Y=112, job header is above that
        private const int JOB_HEADER_X = 12;          // Binary: icon X = 12
        private const int JOB_HEADER_Y = 72;          // 112 - 40 = job header row
        private const int JOB_ICON_SIZE = 32;

        // Content area positioning - from CUISkill::Draw
        // Binary: nTop starts at 112, increments by 40 (adjusted +20 for alignment)
        private const int SKILL_LIST_X = 12;          // Binary: icon X = 12
        private const int SKILL_LIST_Y = 132;         // Binary: 112 + 20 adjustment
        private const int ICON_OFFSET_X = 0;          // Icon at X=12 directly
        private const int ICON_OFFSET_Y = -17;        // Binary: nTop - 17
        private const int TEXT_OFFSET_X = 38;         // Binary: name at X=50, so 50-12=38 from icon
        private const int TEXT_OFFSET_Y = -18;        // Binary: nTop - 18
        private const int LEVEL_OFFSET_Y = 0;         // Binary: level at nTop (baseline)

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

        // Skill row backgrounds (alternating)
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

            // Draw foreground/labels BEFORE skill rows (backgrnd2 should be behind skills)
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Draw tab buttons
            DrawTabs(sprite, windowX, windowY);

            // Draw job header row (shows job icon and name)
            DrawJobHeader(sprite, windowX, windowY);

            // Draw skill rows
            DrawSkillRows(sprite, windowX, windowY);
        }

        /// <summary>
        /// Draw the job header row with job icon and name
        /// </summary>
        private void DrawJobHeader(SpriteBatch sprite, int windowX, int windowY)
        {
            int headerX = windowX + JOB_HEADER_X;
            int headerY = windowY + JOB_HEADER_Y;

            // Draw header row background (same as skill row)
            Texture2D rowBg = _skillRow0;
            if (rowBg != null)
            {
                sprite.Draw(rowBg, new Rectangle(headerX, headerY, SKILL_ROW_WIDTH, SKILL_ROW_HEIGHT), Color.White);
            }

            // Get job icon and name for current tab
            Texture2D jobIcon = null;
            string jobName = "Beginner";

            if (_jobIconsByTab.TryGetValue(_currentTab, out var icon))
                jobIcon = icon;
            if (_jobNamesByTab.TryGetValue(_currentTab, out var name))
                jobName = name;

            // Draw job icon
            int iconX = headerX + ICON_OFFSET_X;
            int iconY = headerY + ICON_OFFSET_Y;

            if (jobIcon != null)
            {
                sprite.Draw(jobIcon, new Rectangle(iconX, iconY, JOB_ICON_SIZE, JOB_ICON_SIZE), Color.White);
            }
            else
            {
                // Draw placeholder if no icon
                sprite.Draw(_debugPlaceholder, new Rectangle(iconX, iconY, JOB_ICON_SIZE, JOB_ICON_SIZE), new Color(80, 80, 120, 200));
            }

            // Draw job name - black text like original client
            if (_font != null)
            {
                sprite.DrawString(_font, jobName, new Vector2(headerX + TEXT_OFFSET_X, headerY + TEXT_OFFSET_Y), Color.Black);

                // Draw SP count - black text
                int sp = GetCurrentSkillPoints();
                string spText = $"SP: {sp}";
                sprite.DrawString(_font, spText, new Vector2(headerX + TEXT_OFFSET_X, headerY + LEVEL_OFFSET_Y), Color.Black);
            }
        }

        /// <summary>
        /// Draw the skill rows with icons, names, and levels
        /// </summary>
        private void DrawSkillRows(SpriteBatch sprite, int windowX, int windowY)
        {
            var skills = CurrentSkills;
            int startIndex = _scrollOffset;
            int endIndex = Math.Min(startIndex + VISIBLE_SKILLS, skills.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                int rowIndex = i - startIndex;
                int rowX = windowX + SKILL_LIST_X;
                int rowY = windowY + SKILL_LIST_Y + (rowIndex * SKILL_ROW_HEIGHT);

                // Draw alternating row background
                Texture2D rowBg = (rowIndex % 2 == 0) ? _skillRow0 : _skillRow1;
                if (rowBg != null)
                {
                    sprite.Draw(rowBg, new Rectangle(rowX, rowY, SKILL_ROW_WIDTH, SKILL_ROW_HEIGHT), Color.White);
                }

                // Draw separator line
                if (_skillRowLine != null && rowIndex > 0)
                {
                    sprite.Draw(_skillRowLine, new Rectangle(rowX, rowY, SKILL_ROW_WIDTH, 1), Color.White);
                }

                // Draw skill data
                if (i < skills.Count)
                {
                    var skill = skills[i];
                    DrawSkillEntry(sprite, skill, rowX, rowY, i == _hoveredSkillIndex);
                }
            }
        }

        /// <summary>
        /// Draw a single skill entry
        /// </summary>
        private void DrawSkillEntry(SpriteBatch sprite, SkillDisplayData skill, int x, int y, bool isHovered)
        {
            // Draw skill icon - use IconTexture directly
            Texture2D icon = skill.IconTexture;
            int iconX = x + ICON_OFFSET_X;
            int iconY = y + ICON_OFFSET_Y;

            if (icon != null)
            {
                // Draw icon using Rectangle like tabs do (to match working behavior)
                Rectangle iconRect = new Rectangle(iconX, iconY, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
                sprite.Draw(icon, iconRect, Color.White);
            }
            else
            {
                // Draw placeholder rectangle when icon is null
                if (_debugPlaceholder != null)
                {
                    sprite.Draw(_debugPlaceholder, new Rectangle(iconX, iconY, SKILL_ICON_SIZE, SKILL_ICON_SIZE), Color.Red);
                }
            }

            // Draw skill name if we have a font - use black text like original client
            if (_font != null)
            {
                string displayName = skill.SkillName.Length > 12 ? skill.SkillName.Substring(0, 12) + "..." : skill.SkillName;
                sprite.DrawString(_font, displayName, new Vector2(x + TEXT_OFFSET_X, y + TEXT_OFFSET_Y), Color.Black);

                // Draw level - also black text
                string levelText = $"Lv.{skill.CurrentLevel}/{skill.MaxLevel}";
                sprite.DrawString(_font, levelText, new Vector2(x + TEXT_OFFSET_X, y + LEVEL_OFFSET_Y), Color.Black);
            }

            // Draw SP+ button if skill can be leveled up
            if (_spUpNormal != null && skill.CurrentLevel < skill.MaxLevel)
            {
                int spBtnX = x + SKILL_ROW_WIDTH - 16;
                int spBtnY = y + (SKILL_ROW_HEIGHT - 12) / 2;
                sprite.Draw(_spUpNormal, new Rectangle(spBtnX, spBtnY, 12, 12), Color.White);
            }
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
            int relX = mouseX - Position.X - SKILL_LIST_X;
            int relY = mouseY - Position.Y - SKILL_LIST_Y;

            if (relX < 0 || relX > SKILL_ROW_WIDTH || relY < 0)
                return null;

            int rowIndex = relY / SKILL_ROW_HEIGHT;
            if (rowIndex >= VISIBLE_SKILLS)
                return null;

            int skillIndex = _scrollOffset + rowIndex;
            var skills = CurrentSkills;
            if (skillIndex >= 0 && skillIndex < skills.Count)
                return skills[skillIndex];

            return null;
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
                // Check if mouse is over skill list area
                Rectangle skillListRect = new Rectangle(
                    Position.X + SKILL_LIST_X,
                    Position.Y + SKILL_LIST_Y,
                    SKILL_ROW_WIDTH,
                    VISIBLE_SKILLS * SKILL_ROW_HEIGHT);

                if (skillListRect.Contains(mouseState.X, mouseState.Y))
                {
                    if (scrollDelta > 0)
                        ScrollUp();
                    else
                        ScrollDown();
                }
            }

            // Update hovered skill index
            _hoveredSkillIndex = -1;
            int relX = mouseState.X - Position.X - SKILL_LIST_X;
            int relY = mouseState.Y - Position.Y - SKILL_LIST_Y;
            if (relX >= 0 && relX <= SKILL_ROW_WIDTH && relY >= 0)
            {
                int rowIndex = relY / SKILL_ROW_HEIGHT;
                if (rowIndex < VISIBLE_SKILLS)
                {
                    int skillIndex = _scrollOffset + rowIndex;
                    if (skillIndex < CurrentSkills.Count)
                        _hoveredSkillIndex = skillIndex;
                }
            }

            _previousMouseState = mouseState;
        }
        #endregion
    }
}
