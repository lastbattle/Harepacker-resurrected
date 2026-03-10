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
using System.Text;

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

        // Hover tooltip layout
        private const int TOOLTIP_FALLBACK_WIDTH = 320;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int TOOLTIP_OFFSET_X = 12;
        private const int TOOLTIP_OFFSET_Y = -4;
        private static readonly Color TOOLTIP_BACKGROUND_COLOR = new Color(28, 28, 28, 228);
        private static readonly Color TOOLTIP_BORDER_COLOR = new Color(112, 112, 112, 235);

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
        private const int SCROLLBAR_X = 153;
        private const int SCROLLBAR_Y = 93;
        private const int SCROLLBAR_WIDTH = 12;
        private const int SCROLLBAR_HEIGHT = 155;
        private const int SCROLLBAR_BUTTON_HEIGHT = 12;

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
        private int _selectedSkillIndex = -1;
        private int _lastClickedSkillIndex = -1;
        private int _lastClickTime;

        // Foreground textures
        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _skillListBackground;
        private Point _skillListBgOffset;

        // Skill row backgrounds (normal / can-level-up)
        private Texture2D _skillRow0;
        private Texture2D _skillRow1;
        private Texture2D _recommendTexture;
        private Texture2D _skillRowLine;

        // Tab textures (0=Beginner, 1-4=Job advancements)
        private Texture2D[] _tabEnabled = new Texture2D[5];
        private Texture2D[] _tabDisabled = new Texture2D[5];
        private Rectangle[] _tabRects = new Rectangle[5];
        private readonly bool[] _tabVisible = new bool[5];

        // SP Up button textures
        private Texture2D _spUpNormal;
        private Texture2D _spUpPressed;
        private Texture2D _spUpDisabled;
        private Texture2D _spUpMouseOver;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private Texture2D _scrollPrevNormal;
        private Texture2D _scrollPrevPressed;
        private Texture2D _scrollNextNormal;
        private Texture2D _scrollNextPressed;
        private Texture2D _scrollTrackEnabled;
        private Texture2D _scrollThumbNormal;
        private Texture2D _scrollThumbPressed;
        private Texture2D _scrollPrevDisabled;
        private Texture2D _scrollNextDisabled;
        private Texture2D _scrollTrackDisabled;

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
        private KeyboardState _previousKeyboardState;
        private Point _lastMousePosition;

        // Drag and drop
        private bool _isDraggingSkill;
        private int _dragSkillId;
        private SkillDisplayData _dragSkill;
        private Vector2 _dragPosition;
        private int _dragSourceIndex = -1;
        private bool _isDraggingScrollThumb;
        private int _scrollThumbDragOffsetY;

        // Placeholder texture (1x1 white pixel for drawing colored rects)
        private Texture2D _debugPlaceholder;
        #endregion

        #region Properties
        public override string WindowName => "Skills";

        public Action<int> OnSkillInvoked { get; set; }
        public Action<SkillDisplayData> OnSkillSelected { get; set; }
        public bool IsDraggingSkill => _isDraggingSkill;
        public int DraggedSkillId => _isDraggingSkill ? _dragSkillId : 0;
        public Vector2 DragPosition => _dragPosition;
        public SkillDisplayData DraggedSkill => _isDraggingSkill ? _dragSkill : null;
        public Action<int, SkillDisplayData> OnDragStart { get; set; }
        public Action OnDragEnd { get; set; }

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                ApplyCurrentTab(CoerceVisibleTab(value));
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

        public SkillDisplayData SelectedSkill
        {
            get
            {
                var skills = CurrentSkills;
                return _selectedSkillIndex >= 0 && _selectedSkillIndex < skills.Count
                    ? skills[_selectedSkillIndex]
                    : null;
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
                _tabVisible[i] = true;
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

        public void SetRecommendTexture(Texture2D recommendTexture)
        {
            _recommendTexture = recommendTexture;
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

        public void SetTooltipTextures(Texture2D[] tooltipFrames)
        {
            if (tooltipFrames == null)
                return;

            for (int i = 0; i < Math.Min(_tooltipFrames.Length, tooltipFrames.Length); i++)
            {
                _tooltipFrames[i] = tooltipFrames[i];
            }
        }

        public void SetScrollBarTextures(
            Texture2D prevNormal,
            Texture2D prevPressed,
            Texture2D nextNormal,
            Texture2D nextPressed,
            Texture2D trackEnabled,
            Texture2D thumbNormal,
            Texture2D thumbPressed,
            Texture2D prevDisabled,
            Texture2D nextDisabled,
            Texture2D trackDisabled)
        {
            _scrollPrevNormal = prevNormal;
            _scrollPrevPressed = prevPressed;
            _scrollNextNormal = nextNormal;
            _scrollNextPressed = nextPressed;
            _scrollTrackEnabled = trackEnabled;
            _scrollThumbNormal = thumbNormal;
            _scrollThumbPressed = thumbPressed;
            _scrollPrevDisabled = prevDisabled;
            _scrollNextDisabled = nextDisabled;
            _scrollTrackDisabled = trackDisabled;
        }

        public void SetVisibleTabs(IEnumerable<int> visibleTabs)
        {
            Array.Clear(_tabVisible, 0, _tabVisible.Length);

            bool hasVisibleTab = false;
            if (visibleTabs != null)
            {
                foreach (int tab in visibleTabs)
                {
                    int tabIndex = Math.Clamp(tab, TAB_BEGINNER, TAB_4TH);
                    if (_tabVisible[tabIndex])
                        continue;

                    _tabVisible[tabIndex] = true;
                    hasVisibleTab = true;
                }
            }

            if (!hasVisibleTab)
            {
                _tabVisible[TAB_BEGINNER] = true;
            }

            ApplyCurrentTab(CoerceVisibleTab(_currentTab));
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
            DrawScrollBar(sprite);

            // Draw SP count
            DrawSkillPointCount(sprite, windowX, windowY);
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            DrawHoveredSkillTooltip(sprite, windowX, windowY, renderParameters.RenderWidth, renderParameters.RenderHeight);
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
            int recommendedSkillId = ResolveRecommendedSkillId(skills, availableSp);

            for (int rowIndex = 0; rowIndex < VISIBLE_SKILLS; rowIndex++)
            {
                int skillIndex = _scrollOffset + rowIndex;
                if (skillIndex >= skills.Count)
                    break;

                int nTop = FIRST_ROW_TOP + (rowIndex * SKILL_ROW_HEIGHT);
                SkillDisplayData skill = skills[skillIndex];
                bool canLevelUp = availableSp > 0 && skill.CurrentLevel < skill.MaxLevel;

                DrawSkillEntry(
                    sprite,
                    skill,
                    windowX,
                    windowY,
                    nTop,
                    canLevelUp,
                    skill.SkillId == recommendedSkillId,
                    skillIndex == _hoveredSkillIndex,
                    skillIndex == _selectedSkillIndex);

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
            bool isRecommended,
            bool isHovered,
            bool isSelected)
        {
            Texture2D rowBg = canLevelUp ? _skillRow1 : _skillRow0;
            if (rowBg != null)
            {
                sprite.Draw(rowBg, new Vector2(windowX + ROW_BG_X, windowY + nTop + ROW_BG_Y_OFFSET), Color.White);
            }

            if (isRecommended && _recommendTexture != null)
            {
                sprite.Draw(_recommendTexture, new Vector2(windowX + ROW_BG_X, windowY + nTop + ROW_BG_Y_OFFSET), Color.White);
            }

            if (isSelected)
            {
                var selectionRect = new Rectangle(windowX + ROW_BG_X, windowY + nTop + ROW_BG_Y_OFFSET, 140, 37);
                sprite.Draw(_debugPlaceholder, selectionRect, new Color(80, 140, 255, 70));
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
                string skillName = SanitizeFontText(skill.SkillName);
                sprite.DrawString(
                    _font,
                    skillName,
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

        private static int ResolveRecommendedSkillId(IReadOnlyList<SkillDisplayData> skills, int availableSp)
        {
            if (skills == null || skills.Count == 0 || availableSp <= 0)
                return 0;

            for (int i = 0; i < skills.Count; i++)
            {
                SkillDisplayData skill = skills[i];
                if (skill == null)
                    continue;

                if (skill.CurrentLevel < skill.MaxLevel)
                    return skill.SkillId;
            }

            return 0;
        }

        private void DrawSkillPointCount(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_font == null)
                return;

            string spText = SanitizeFontText(GetCurrentSkillPoints().ToString());
            float width = _font.MeasureString(spText).X;
            sprite.DrawString(
                _font,
                spText,
                new Vector2(windowX + SP_DISPLAY_X_BASE - width, windowY + SP_DISPLAY_Y),
                Color.Black);
        }

        private void DrawScrollBar(SpriteBatch sprite)
        {
            Rectangle upButtonBounds = GetScrollUpButtonBounds();
            Rectangle downButtonBounds = GetScrollDownButtonBounds();
            Rectangle trackBounds = GetScrollTrackBounds();
            bool canScroll = CanScrollSkills();
            bool leftPressed = Mouse.GetState().LeftButton == ButtonState.Pressed;

            DrawScrollTexture(
                sprite,
                canScroll
                    ? ((leftPressed && upButtonBounds.Contains(_lastMousePosition) && !_isDraggingScrollThumb)
                        ? _scrollPrevPressed ?? _scrollPrevNormal
                        : _scrollPrevNormal)
                    : _scrollPrevDisabled ?? _scrollPrevNormal,
                upButtonBounds);

            DrawTiledTrack(sprite, canScroll ? _scrollTrackEnabled : _scrollTrackDisabled ?? _scrollTrackEnabled, trackBounds);

            if (canScroll)
            {
                Rectangle thumbBounds = GetScrollThumbBounds();
                bool thumbPressed = _isDraggingScrollThumb || (leftPressed && thumbBounds.Contains(_lastMousePosition));
                DrawScrollTexture(sprite, thumbPressed ? _scrollThumbPressed ?? _scrollThumbNormal : _scrollThumbNormal, thumbBounds);
            }

            DrawScrollTexture(
                sprite,
                canScroll
                    ? ((leftPressed && downButtonBounds.Contains(_lastMousePosition) && !_isDraggingScrollThumb)
                        ? _scrollNextPressed ?? _scrollNextNormal
                        : _scrollNextNormal)
                    : _scrollNextDisabled ?? _scrollNextNormal,
                downButtonBounds);
        }

        private void DrawTiledTrack(SpriteBatch sprite, Texture2D texture, Rectangle bounds)
        {
            if (texture == null || bounds.Width <= 0 || bounds.Height <= 0)
                return;

            int tileHeight = Math.Max(1, texture.Height);
            for (int y = bounds.Y; y < bounds.Bottom; y += tileHeight)
            {
                int height = Math.Min(tileHeight, bounds.Bottom - y);
                Rectangle destination = new Rectangle(bounds.X, y, bounds.Width, height);
                Rectangle source = new Rectangle(0, 0, texture.Width, height);
                sprite.Draw(texture, destination, source, Color.White);
            }
        }

        private void DrawScrollTexture(SpriteBatch sprite, Texture2D texture, Rectangle destination)
        {
            if (texture == null || destination.Width <= 0 || destination.Height <= 0)
                return;

            sprite.Draw(texture, destination, Color.White);
        }

        private void DrawHoveredSkillTooltip(SpriteBatch sprite, int windowX, int windowY, int renderWidth, int renderHeight)
        {
            if (_font == null || _isDraggingSkill || _hoveredSkillIndex < 0)
                return;

            var skills = CurrentSkills;
            if (_hoveredSkillIndex >= skills.Count)
                return;

            SkillDisplayData skill = skills[_hoveredSkillIndex];
            if (skill == null)
                return;

            int currentLevel = Math.Clamp(skill.CurrentLevel, 0, Math.Max(0, skill.MaxLevel));
            int previewLevel = currentLevel > 0 ? currentLevel : 1;
            int nextLevel = Math.Min(skill.MaxLevel, previewLevel + (currentLevel > 0 ? 1 : 0));
            string skillName = SanitizeFontText(skill.SkillName);
            string description = SanitizeFontText(skill.Description);
            string currentLevelHeader = currentLevel > 0 ? $"Current Level: {currentLevel}" : string.Empty;
            string currentLevelDescription = currentLevel > 0
                ? SanitizeFontText(skill.GetLevelDescription(currentLevel))
                : string.Empty;
            bool showNextLevel = nextLevel > 0 && nextLevel <= skill.MaxLevel && nextLevel != currentLevel;
            string nextLevelHeader = showNextLevel ? $"Next Level: {nextLevel}" : string.Empty;
            string nextLevelDescription = showNextLevel
                ? SanitizeFontText(skill.GetLevelDescription(nextLevel))
                : string.Empty;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SKILL_ICON_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedName = WrapTooltipText(skillName, titleWidth);
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            string[] wrappedCurrentHeader = WrapTooltipText(currentLevelHeader, sectionWidth);
            string[] wrappedCurrentDescription = WrapTooltipText(currentLevelDescription, sectionWidth);
            string[] wrappedNextHeader = WrapTooltipText(nextLevelHeader, sectionWidth);
            string[] wrappedNextDescription = WrapTooltipText(nextLevelDescription, sectionWidth);

            float titleHeight = MeasureLinesHeight(wrappedName);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float currentHeaderHeight = MeasureLinesHeight(wrappedCurrentHeader);
            float currentDescriptionHeight = MeasureLinesHeight(wrappedCurrentDescription);
            float nextHeaderHeight = MeasureLinesHeight(wrappedNextHeader);
            float nextDescriptionHeight = MeasureLinesHeight(wrappedNextDescription);
            float sectionHeight = CalculateTooltipSectionHeight(
                descriptionHeight,
                currentHeaderHeight,
                currentDescriptionHeight,
                nextHeaderHeight,
                nextDescriptionHeight);
            float iconBlockHeight = Math.Max(SKILL_ICON_SIZE, sectionHeight);

            int tooltipHeight = (int)Math.Ceiling(
                (TOOLTIP_PADDING * 2)
                + titleHeight
                + TOOLTIP_TITLE_GAP
                + iconBlockHeight);

            int tooltipX = _lastMousePosition.X + TOOLTIP_OFFSET_X;
            int tooltipY = _lastMousePosition.Y + 20;
            int tooltipFrameIndex = 1;

            if (tooltipX + tooltipWidth > renderWidth - TOOLTIP_PADDING)
            {
                tooltipX = _lastMousePosition.X - tooltipWidth - TOOLTIP_OFFSET_X;
                tooltipFrameIndex = 0;
            }

            if (tooltipX < TOOLTIP_PADDING)
                tooltipX = TOOLTIP_PADDING;

            if (tooltipY + tooltipHeight > renderHeight - TOOLTIP_PADDING)
            {
                tooltipY = Math.Max(TOOLTIP_PADDING, _lastMousePosition.Y - tooltipHeight + TOOLTIP_OFFSET_Y);
                tooltipFrameIndex = 2;
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int titleX = tooltipX + TOOLTIP_PADDING;
            int titleY = tooltipY + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedName, titleX, titleY, new Color(255, 220, 120));

            int contentY = tooltipY + TOOLTIP_PADDING + (int)Math.Ceiling(titleHeight) + TOOLTIP_TITLE_GAP;
            int iconX = tooltipX + TOOLTIP_PADDING;
            Texture2D icon = skill.GetIconForState(false, true) ?? skill.IconTexture;
            if (icon != null)
            {
                sprite.Draw(icon, new Rectangle(iconX, contentY, SKILL_ICON_SIZE, SKILL_ICON_SIZE), Color.White);
            }

            int textX = tooltipX + textLeftOffset;
            float sectionY = contentY;
            if (descriptionHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedDescription, textX, sectionY, Color.White);
                sectionY += descriptionHeight;
            }

            if (currentHeaderHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, wrappedCurrentHeader, textX, sectionY, new Color(140, 200, 255));
                sectionY += currentHeaderHeight;
            }

            if (currentDescriptionHeight > 0f)
            {
                sectionY += 2f;
                DrawTooltipLines(sprite, wrappedCurrentDescription, textX, sectionY, new Color(180, 255, 210));
                sectionY += currentDescriptionHeight;
            }

            if (nextHeaderHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, wrappedNextHeader, textX, sectionY, new Color(255, 200, 140));
                sectionY += nextHeaderHeight;
            }

            if (nextDescriptionHeight > 0f)
            {
                sectionY += 2f;
                DrawTooltipLines(sprite, wrappedNextDescription, textX, sectionY, new Color(255, 238, 196));
            }
        }

        private static float CalculateTooltipSectionHeight(
            float descriptionHeight,
            float currentHeaderHeight,
            float currentDescriptionHeight,
            float nextHeaderHeight,
            float nextDescriptionHeight)
        {
            float height = 0f;
            if (descriptionHeight > 0f)
                height += descriptionHeight;
            if (currentHeaderHeight > 0f)
                height += (height > 0f ? TOOLTIP_SECTION_GAP : 0f) + currentHeaderHeight;
            if (currentDescriptionHeight > 0f)
                height += 2f + currentDescriptionHeight;
            if (nextHeaderHeight > 0f)
                height += (height > 0f ? TOOLTIP_SECTION_GAP : 0f) + nextHeaderHeight;
            if (nextDescriptionHeight > 0f)
                height += 2f + nextDescriptionHeight;

            return height;
        }

        private int ResolveTooltipWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TOOLTIP_FALLBACK_WIDTH;
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;

            if (tooltipFrame != null)
            {
                sprite.Draw(tooltipFrame, rect, Color.White);
                return;
            }

            sprite.Draw(_debugPlaceholder, rect, TOOLTIP_BACKGROUND_COLOR);
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), TOOLTIP_BORDER_COLOR);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), TOOLTIP_BORDER_COLOR);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), TOOLTIP_BORDER_COLOR);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.Right, rect.Y, 1, rect.Height), TOOLTIP_BORDER_COLOR);
        }

        private void DrawTooltipLines(SpriteBatch sprite, string[] lines, int x, float y, Color color)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * _font.LineSpacing)), color);
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private float MeasureLongestLine(string[] lines)
        {
            float width = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    width = Math.Max(width, _font.MeasureString(lines[i]).X);
            }

            return width;
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return 0f;

            int nonEmptyLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    nonEmptyLines++;
            }

            return nonEmptyLines > 0 ? nonEmptyLines * _font.LineSpacing : 0f;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var lines = new List<string>();
            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');

            foreach (string paragraph in paragraphs)
            {
                string trimmed = paragraph.Trim();
                if (trimmed.Length == 0)
                    continue;

                string currentLine = string.Empty;
                string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);
            }

            return lines.ToArray();
        }

        private void DrawBookName(SpriteBatch sprite, int windowX, int windowY, string jobName)
        {
            if (_font == null || string.IsNullOrWhiteSpace(jobName))
                return;

            jobName = SanitizeFontText(jobName);
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

        private static string SanitizeFontText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '\r':
                    case '\n':
                    case '\t':
                    case ' ':
                        builder.Append(ch == '\t' ? ' ' : ch);
                        break;
                    case '\u2018':
                    case '\u2019':
                    case '\u2032':
                        builder.Append('\'');
                        break;
                    case '\u201C':
                    case '\u201D':
                    case '\u2033':
                        builder.Append('"');
                        break;
                    case '\u2013':
                    case '\u2014':
                    case '\u2212':
                        builder.Append('-');
                        break;
                    case '\u2026':
                        builder.Append("...");
                        break;
                    case '\u00A0':
                    case '\u3000':
                        builder.Append(' ');
                        break;
                    default:
                        if (ch >= 32 && ch <= 126)
                        {
                            builder.Append(ch);
                        }
                        else if (!char.IsControl(ch))
                        {
                            builder.Append('?');
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Draw tab buttons
        /// </summary>
        private void DrawTabs(SpriteBatch sprite, int windowX, int windowY)
        {
            for (int i = 0; i < 5; i++)
            {
                if (!_tabVisible[i])
                    continue;

                Texture2D tabTexture = (i == _currentTab) ? _tabEnabled[i] : _tabDisabled[i];
                if (tabTexture != null)
                {
                    sprite.Draw(tabTexture, GetTabBounds(windowX, windowY, i), Color.White);
                }
            }
        }

        private Rectangle GetTabBounds(int windowX, int windowY, int tabIndex)
        {
            Texture2D tabTexture = (tabIndex == _currentTab) ? _tabEnabled[tabIndex] : _tabDisabled[tabIndex];
            Rectangle tabRect = _tabRects[tabIndex];
            return new Rectangle(
                windowX + tabRect.X,
                windowY + tabRect.Y,
                tabTexture?.Width ?? tabRect.Width,
                tabTexture?.Height ?? tabRect.Height);
        }

        private void ApplyCurrentTab(int tabIndex)
        {
            _currentTab = tabIndex;
            _scrollOffset = 0;
            _hoveredSkillIndex = -1;
            _selectedSkillIndex = -1;
        }

        private int CoerceVisibleTab(int requestedTab)
        {
            int clampedTab = Math.Clamp(requestedTab, TAB_BEGINNER, TAB_4TH);
            if (_tabVisible[clampedTab])
                return clampedTab;

            for (int tab = clampedTab - 1; tab >= TAB_BEGINNER; tab--)
            {
                if (_tabVisible[tab])
                    return tab;
            }

            for (int tab = clampedTab + 1; tab <= TAB_4TH; tab++)
            {
                if (_tabVisible[tab])
                    return tab;
            }

            return TAB_BEGINNER;
        }

        private Point GetSkillIconPosition(int visibleRowIndex)
        {
            int nTop = FIRST_ROW_TOP + (visibleRowIndex * SKILL_ROW_HEIGHT);
            return new Point(
                Position.X + ICON_X,
                Position.Y + nTop + ICON_Y_OFFSET);
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

        public void UpdateSkillLevel(int skillId, int currentLevel, int maxLevel)
        {
            foreach (var tabSkills in skillsByTab.Values)
            {
                foreach (var skill in tabSkills)
                {
                    if (skill.SkillId != skillId)
                        continue;

                    skill.CurrentLevel = Math.Max(0, currentLevel);
                    skill.MaxLevel = Math.Max(skill.CurrentLevel, maxLevel);
                }
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
            ScrollBy(-1);
        }

        /// <summary>
        /// Scroll down in the skill list
        /// </summary>
        public void ScrollDown()
        {
            ScrollBy(1);
        }

        private Rectangle GetSkillListBounds()
        {
            return new Rectangle(
                Position.X + ROW_HIT_LEFT,
                Position.Y + FIRST_ROW_TOP + ROW_HIT_TOP_OFFSET,
                ROW_HIT_RIGHT - ROW_HIT_LEFT + 1,
                (VISIBLE_SKILLS * SKILL_ROW_HEIGHT) - ROW_HIT_TOP_OFFSET + 1);
        }

        private Rectangle GetScrollBarBounds()
        {
            return new Rectangle(Position.X + SCROLLBAR_X, Position.Y + SCROLLBAR_Y, SCROLLBAR_WIDTH, SCROLLBAR_HEIGHT);
        }

        private Rectangle GetScrollUpButtonBounds()
        {
            return new Rectangle(Position.X + SCROLLBAR_X, Position.Y + SCROLLBAR_Y, SCROLLBAR_WIDTH, SCROLLBAR_BUTTON_HEIGHT);
        }

        private Rectangle GetScrollDownButtonBounds()
        {
            return new Rectangle(
                Position.X + SCROLLBAR_X,
                Position.Y + SCROLLBAR_Y + SCROLLBAR_HEIGHT - SCROLLBAR_BUTTON_HEIGHT,
                SCROLLBAR_WIDTH,
                SCROLLBAR_BUTTON_HEIGHT);
        }

        private Rectangle GetScrollTrackBounds()
        {
            return new Rectangle(
                Position.X + SCROLLBAR_X,
                Position.Y + SCROLLBAR_Y + SCROLLBAR_BUTTON_HEIGHT,
                SCROLLBAR_WIDTH,
                SCROLLBAR_HEIGHT - (SCROLLBAR_BUTTON_HEIGHT * 2));
        }

        private Rectangle GetScrollThumbBounds()
        {
            Rectangle trackBounds = GetScrollTrackBounds();
            int thumbHeight = Math.Min(trackBounds.Height, Math.Max(1, _scrollThumbNormal?.Height ?? SCROLLBAR_WIDTH));
            int maxScroll = GetMaxScrollOffset();
            if (maxScroll <= 0)
                return new Rectangle(trackBounds.X, trackBounds.Y, trackBounds.Width, thumbHeight);

            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbY = trackBounds.Y;
            if (travel > 0)
            {
                float ratio = _scrollOffset / (float)maxScroll;
                thumbY += (int)Math.Round(travel * ratio);
            }

            return new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        }

        private int GetMaxScrollOffset()
        {
            return Math.Max(0, CurrentSkills.Count - VISIBLE_SKILLS);
        }

        private bool CanScrollSkills()
        {
            return GetMaxScrollOffset() > 0;
        }

        private void SetScrollOffset(int offset)
        {
            _scrollOffset = Math.Clamp(offset, 0, GetMaxScrollOffset());
        }

        private void ScrollBy(int delta)
        {
            SetScrollOffset(_scrollOffset + delta);
        }

        private void SetScrollOffsetFromThumb(int mouseY)
        {
            Rectangle trackBounds = GetScrollTrackBounds();
            int thumbHeight = Math.Min(trackBounds.Height, Math.Max(1, _scrollThumbNormal?.Height ?? SCROLLBAR_WIDTH));
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int maxScroll = GetMaxScrollOffset();
            if (travel <= 0 || maxScroll <= 0)
            {
                SetScrollOffset(0);
                return;
            }

            int thumbTop = Math.Clamp(mouseY - _scrollThumbDragOffsetY, trackBounds.Y, trackBounds.Y + travel);
            float ratio = (thumbTop - trackBounds.Y) / (float)travel;
            SetScrollOffset((int)Math.Round(ratio * maxScroll));
        }

        private bool TryHandleScrollBarMouseDown(MouseState mouseState)
        {
            if (!GetScrollBarBounds().Contains(mouseState.X, mouseState.Y))
                return false;

            if (!CanScrollSkills())
                return true;

            Rectangle upButtonBounds = GetScrollUpButtonBounds();
            if (upButtonBounds.Contains(mouseState.X, mouseState.Y))
            {
                ScrollUp();
                return true;
            }

            Rectangle downButtonBounds = GetScrollDownButtonBounds();
            if (downButtonBounds.Contains(mouseState.X, mouseState.Y))
            {
                ScrollDown();
                return true;
            }

            Rectangle thumbBounds = GetScrollThumbBounds();
            if (thumbBounds.Contains(mouseState.X, mouseState.Y))
            {
                _isDraggingScrollThumb = true;
                _scrollThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
                return true;
            }

            Rectangle trackBounds = GetScrollTrackBounds();
            if (trackBounds.Contains(mouseState.X, mouseState.Y))
            {
                ScrollBy(mouseState.Y < thumbBounds.Y ? -VISIBLE_SKILLS : VISIBLE_SKILLS);
                return true;
            }

            return false;
        }

        private void EnsureSkillVisible(int skillIndex)
        {
            if (skillIndex < 0)
                return;

            var skills = CurrentSkills;
            int maxScroll = Math.Max(0, skills.Count - VISIBLE_SKILLS);
            if (skillIndex < _scrollOffset)
            {
                _scrollOffset = skillIndex;
            }
            else if (skillIndex >= _scrollOffset + VISIBLE_SKILLS)
            {
                _scrollOffset = skillIndex - VISIBLE_SKILLS + 1;
            }

            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);
        }

        private void SelectSkillIndex(int skillIndex, bool notifySelection)
        {
            var skills = CurrentSkills;
            if (skillIndex < 0 || skillIndex >= skills.Count)
                return;

            _selectedSkillIndex = skillIndex;
            EnsureSkillVisible(skillIndex);

            if (notifySelection)
            {
                OnSkillSelected?.Invoke(skills[skillIndex]);
            }
        }

        private bool MoveSelection(int delta)
        {
            var skills = CurrentSkills;
            if (skills.Count == 0)
                return false;

            int baseIndex = _selectedSkillIndex >= 0
                ? _selectedSkillIndex
                : (delta >= 0 ? 0 : skills.Count - 1);
            int nextIndex = Math.Clamp(baseIndex + delta, 0, skills.Count - 1);
            if (nextIndex == _selectedSkillIndex)
                return false;

            SelectSkillIndex(nextIndex, true);
            return true;
        }

        private bool TryHandleSkillListKeyboardNavigation(KeyboardState keyboardState)
        {
            var skills = CurrentSkills;
            if (skills.Count == 0)
                return false;

            bool WasPressed(Keys key) => keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);

            if (WasPressed(Keys.Up))
                return MoveSelection(-1);

            if (WasPressed(Keys.Down))
                return MoveSelection(1);

            if (WasPressed(Keys.PageUp))
                return MoveSelection(-VISIBLE_SKILLS);

            if (WasPressed(Keys.PageDown))
                return MoveSelection(VISIBLE_SKILLS);

            if (WasPressed(Keys.Home))
            {
                SelectSkillIndex(0, true);
                return true;
            }

            if (WasPressed(Keys.End))
            {
                SelectSkillIndex(skills.Count - 1, true);
                return true;
            }

            return false;
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
            return GetSkillIndexAtPosition(mouseX, mouseY, out _);
        }

        private int GetSkillIndexAtPosition(int mouseX, int mouseY, out bool hitIcon)
        {
            hitIcon = false;
            int relX = mouseX - Position.X;
            int relY = mouseY - Position.Y;
            var skills = CurrentSkills;

            for (int rowIndex = 0; rowIndex < VISIBLE_SKILLS; rowIndex++)
            {
                int skillIndex = _scrollOffset + rowIndex;
                if (skillIndex >= skills.Count)
                    break;

                int nTop = FIRST_ROW_TOP + (rowIndex * SKILL_ROW_HEIGHT);

                bool isIconHit =
                    relX >= ICON_HIT_LEFT &&
                    relX <= ICON_HIT_RIGHT &&
                    relY >= nTop + ICON_HIT_TOP_OFFSET &&
                    relY <= nTop + ICON_HIT_BOTTOM_OFFSET;
                if (isIconHit)
                {
                    hitIcon = true;
                    return skillIndex;
                }

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

        public void OnSkillMouseDown(int mouseX, int mouseY)
        {
            int skillIndex = GetSkillIndexAtPosition(mouseX, mouseY, out bool hitIcon);
            if (!hitIcon || skillIndex < 0)
                return;

            SkillDisplayData skill = GetSkillAtPosition(mouseX, mouseY);
            if (skill == null || skill.CurrentLevel <= 0)
                return;

            _isDraggingSkill = true;
            _dragSkillId = skill.SkillId;
            _dragSkill = skill;
            _dragSourceIndex = skillIndex;
            _dragPosition = new Vector2(mouseX, mouseY);
            OnDragStart?.Invoke(_dragSkillId, skill);
        }

        public void OnSkillMouseMove(int mouseX, int mouseY)
        {
            if (_isDraggingSkill)
            {
                _dragPosition = new Vector2(mouseX, mouseY);
            }
        }

        public void OnSkillMouseUp()
        {
            if (!_isDraggingSkill)
                return;

            _isDraggingSkill = false;
            _dragSkillId = 0;
            _dragSkill = null;
            _dragSourceIndex = -1;
            OnDragEnd?.Invoke();
        }

        public void CancelDrag()
        {
            _isDraggingSkill = false;
            _dragSkillId = 0;
            _dragSkill = null;
            _dragSourceIndex = -1;
        }

        public void DrawDraggedSkill(SpriteBatch sprite)
        {
            if (!_isDraggingSkill || _dragSkill?.IconTexture == null)
                return;

            sprite.Draw(
                _dragSkill.IconTexture,
                new Rectangle(
                    (int)_dragPosition.X - SKILL_ICON_SIZE / 2,
                    (int)_dragPosition.Y - SKILL_ICON_SIZE / 2,
                    SKILL_ICON_SIZE,
                    SKILL_ICON_SIZE),
                Color.White * 0.7f);
        }
        #endregion

        #region Input Handling
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible)
                return;

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            Rectangle skillListBounds = GetSkillListBounds();
            Rectangle scrollBarBounds = GetScrollBarBounds();

            if (_isDraggingScrollThumb)
            {
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    SetScrollOffsetFromThumb(mouseState.Y);
                }
                else
                {
                    _isDraggingScrollThumb = false;
                }
            }

            // Handle tab clicks
            if (mouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                if (TryHandleScrollBarMouseDown(mouseState))
                {
                    _hoveredSkillIndex = GetSkillIndexAtPosition(mouseState.X, mouseState.Y);
                    _previousMouseState = mouseState;
                    _previousKeyboardState = Keyboard.GetState();
                    return;
                }

                for (int i = 0; i < 5; i++)
                {
                    if (!_tabVisible[i])
                        continue;

                    Rectangle tabRect = GetTabBounds(Position.X, Position.Y, i);

                    if (tabRect.Contains(mouseState.X, mouseState.Y))
                    {
                        CurrentTab = i;
                        break;
                    }
                }

                int clickedSkillIndex = GetSkillIndexAtPosition(mouseState.X, mouseState.Y);
                if (clickedSkillIndex >= 0)
                {
                    SelectSkillIndex(clickedSkillIndex, false);
                    var selectedSkill = GetSkillAtPosition(mouseState.X, mouseState.Y);
                    if (selectedSkill != null)
                    {
                        OnSkillSelected?.Invoke(selectedSkill);

                        int now = Environment.TickCount;
                        if (_lastClickedSkillIndex == clickedSkillIndex && now - _lastClickTime <= 350)
                        {
                            OnSkillInvoked?.Invoke(selectedSkill.SkillId);
                        }

                        _lastClickedSkillIndex = clickedSkillIndex;
                        _lastClickTime = now;
                    }
                }
            }

            if (mouseState.RightButton == ButtonState.Pressed &&
                _previousMouseState.RightButton == ButtonState.Released)
            {
                var selectedSkill = GetSkillAtPosition(mouseState.X, mouseState.Y);
                if (selectedSkill != null)
                {
                    SelectSkillIndex(GetSkillIndexAtPosition(mouseState.X, mouseState.Y), false);
                    OnSkillSelected?.Invoke(selectedSkill);
                    OnSkillInvoked?.Invoke(selectedSkill.SkillId);
                }
            }

            // Handle scroll wheel
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                if (skillListBounds.Contains(mouseState.X, mouseState.Y) || scrollBarBounds.Contains(mouseState.X, mouseState.Y))
                {
                    if (scrollDelta > 0)
                        ScrollUp();
                    else
                        ScrollDown();

                    if (_selectedSkillIndex >= 0)
                    {
                        EnsureSkillVisible(_selectedSkillIndex);
                    }
                }
            }

            // Update hovered skill index
            _hoveredSkillIndex = GetSkillIndexAtPosition(mouseState.X, mouseState.Y);

            KeyboardState keyboardState = Keyboard.GetState();
            TryHandleSkillListKeyboardNavigation(keyboardState);
            bool invokeSelected = SelectedSkill != null &&
                                  ((keyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter)) ||
                                   (keyboardState.IsKeyDown(Keys.Space) && !_previousKeyboardState.IsKeyDown(Keys.Space)));
            if (invokeSelected)
            {
                OnSkillInvoked?.Invoke(SelectedSkill.SkillId);
            }

            if (_isDraggingSkill)
            {
                _dragPosition = new Vector2(mouseState.X, mouseState.Y);
            }

            _previousMouseState = mouseState;
            _previousKeyboardState = keyboardState;
        }
        #endregion
    }
}
