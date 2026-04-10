using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const float BOOK_NAME_TEXT_SCALE = 0.42f;

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
        private const int RECOMMEND_X = 47;
        private const int LINE_X = 10;
        private const int LINE_Y_OFFSET = 18;
        private const int SP_UP_BUTTON_X = 135;
        private const int SP_UP_BUTTON_Y_OFFSET = 1;
        private const int SP_UP_BUTTON_FALLBACK_WIDTH = 18;
        private const int SP_UP_BUTTON_FALLBACK_HEIGHT = 18;
        private const int SKILL_NAME_MAX_WIDTH = 95;
        private const int SKILL_LEVEL_MAX_WIDTH = 30;
        private const float SKILL_NAME_TEXT_SCALE = 0.38f;
        private const float SKILL_LEVEL_TEXT_SCALE = 0.34f;

        // SP display
        private const int SP_DISPLAY_X_BASE = 104;
        private const int SP_DISPLAY_Y = 256;
        private const float SP_DISPLAY_TEXT_SCALE = 0.4f;

        // Hover tooltip layout
        private const int TOOLTIP_FALLBACK_WIDTH = 320;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int HOVER_TOOLTIP_CURSOR_GAP = 20;
        private const float COOLDOWN_TEXT_SCALE = 0.55f;
        private const int CLIENT_TOOLTIP_WIDTH = 320;
        private const int CLIENT_TOOLTIP_BASE_HEIGHT = 114;
        private const int CLIENT_TOOLTIP_TITLE_X = 10;
        private const int CLIENT_TOOLTIP_TITLE_Y = 10;
        private const int CLIENT_TOOLTIP_ICON_X = 10;
        private const int CLIENT_TOOLTIP_ICON_Y = 32;
        private const int CLIENT_TOOLTIP_TEXT_X = 87;
        private const int CLIENT_TOOLTIP_TEXT_Y = 32;
        private const int CLIENT_TOOLTIP_RIGHT_PADDING = 20;
        private const int CLIENT_TOOLTIP_REQUIREMENT_HEADER_X = 16;
        private const int CLIENT_TOOLTIP_REQUIREMENT_HEADER_Y_OFFSET = -3;
        private const int CLIENT_TOOLTIP_REQUIREMENT_ICON_X = 10;
        private const int CLIENT_TOOLTIP_REQUIREMENT_FIRST_ROW_Y = 15;
        private const int CLIENT_TOOLTIP_REQUIREMENT_ROW_HEIGHT = 34;
        private const int CLIENT_TOOLTIP_REQUIREMENT_NAME_X = 50;
        private const int CLIENT_TOOLTIP_REQUIREMENT_NAME_Y = 2;
        private const int CLIENT_TOOLTIP_REQUIREMENT_LEVEL_X = 50;
        private const int CLIENT_TOOLTIP_REQUIREMENT_LEVEL_Y = 14;
        private const int CLIENT_TOOLTIP_REQUIREMENT_ICON_SIZE = 34;
        private const int CLIENT_TOOLTIP_REQUIREMENT_SECTION_BASE_HEIGHT = 20;
        private static readonly Color TOOLTIP_BACKGROUND_COLOR = new Color(28, 28, 28, 228);
        private static readonly Color TOOLTIP_BORDER_COLOR = new Color(112, 112, 112, 235);
        private static readonly Color TOOLTIP_INLINE_HIGHLIGHT_COLOR = new Color(255, 214, 140);
        // `CUIToolTip::DrawReqSkill` fills each requirement row with `0xA0FFFFFF`.
        private static readonly Color TOOLTIP_REQUIREMENT_ROW_BACKGROUND_COLOR = new Color(255, 255, 255, 160);

        // Hit testing
        private const int ICON_HIT_LEFT = 13;
        private const int ICON_HIT_RIGHT = 45;
        private const int ICON_HIT_TOP_OFFSET = -31;
        private const int ICON_HIT_BOTTOM_OFFSET = 1;
        private const int ROW_HIT_LEFT = 10;
        private const int ROW_HIT_RIGHT = 149;
        private const int ROW_HIT_TOP_OFFSET = -34;
        private const int ROW_HIT_BOTTOM_OFFSET = 0;

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
        private const int TAB_DUAL_5TH = 5;
        private const int TAB_DUAL_6TH = 6;
        private const int STANDARD_TAB_COUNT = 5;
        private const int DUAL_TAB_COUNT = 7;
        private const int MAX_TAB_INDEX = DUAL_TAB_COUNT - 1;
        #endregion

        #region Fields
        private int _currentTab = TAB_BEGINNER;
        private int _characterLevel = 1;
        private int _currentJobId;
        private int _currentSubJob;
        private int _scrollOffset = 0;
        private int _hoveredSkillIndex = -1;
        private int _hoveredSpUpSkillIndex = -1;
        private bool _hoveredSkillPointDisplay;
        private int _pressedSpUpSkillIndex = -1;
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

        // Tab textures (standard 5-tab path plus Dual Blade's 7-tab strip)
        private readonly Texture2D[] _standardTabEnabled = new Texture2D[STANDARD_TAB_COUNT];
        private readonly Texture2D[] _standardTabDisabled = new Texture2D[STANDARD_TAB_COUNT];
        private readonly Rectangle[] _standardTabEnabledRects = new Rectangle[STANDARD_TAB_COUNT];
        private readonly Rectangle[] _standardTabDisabledRects = new Rectangle[STANDARD_TAB_COUNT];
        private readonly Texture2D[] _dualTabEnabled = new Texture2D[DUAL_TAB_COUNT];
        private readonly Texture2D[] _dualTabDisabled = new Texture2D[DUAL_TAB_COUNT];
        private readonly Rectangle[] _dualTabEnabledRects = new Rectangle[DUAL_TAB_COUNT];
        private readonly Rectangle[] _dualTabDisabledRects = new Rectangle[DUAL_TAB_COUNT];
        private readonly bool[] _tabVisible = new bool[DUAL_TAB_COUNT];
        private bool _useDualTabStrip;

        // SP Up button textures
        private Texture2D _spUpNormal;
        private Texture2D _spUpPressed;
        private Texture2D _spUpDisabled;
        private Texture2D _spUpMouseOver;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Point[] _tooltipFrameOrigins = new Point[3];
        private Texture2D[] _cooldownMaskTextures = Array.Empty<Texture2D>();
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
        private readonly Dictionary<int, int> _displaySkillRootByTab;

        // Macro button
        private UIObject _btnMacro;
        private UIObject _btnRide;
        private UIObject _btnGuildSkill;
        private readonly UIObject[] _aranGuideButtons = new UIObject[4];
        private int _aranGuideUnlockedGrade;

        /// <summary>
        /// Gets the macro button for external event wiring
        /// </summary>
        public UIObject MacroButton => _btnMacro;
        public UIObject RideButton => _btnRide;
        public UIObject GuildSkillButton => _btnGuildSkill;

        // Skills organized by job advancement level
        private readonly Dictionary<int, List<SkillDisplayData>> skillsByTab;

        // Skill points available per tab
        private readonly Dictionary<int, int> skillPointsByTab;
        private readonly Dictionary<int, List<SkillDataLoader.RecommendedSkillEntry>> _recommendedSkillsBySkillRootId;

        // Graphics device
        private GraphicsDevice _device;

        // Font for rendering text
        private SpriteFont _font;
        private SkillManager _skillManager;

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
        public Func<SkillDisplayData, bool> OnSkillLevelUpRequested { get; set; }
        public Action<int> OnSkillGuideRequested { get; set; }
        public Action OnRideRequested { get; set; }
        public Action OnGuildSkillRequested { get; set; }
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
                    return GetVisibleSkills(skills);
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
                { TAB_4TH, new List<SkillDisplayData>() },
                { TAB_DUAL_5TH, new List<SkillDisplayData>() },
                { TAB_DUAL_6TH, new List<SkillDisplayData>() }
            };

            skillPointsByTab = new Dictionary<int, int>
            {
                { TAB_BEGINNER, 1 },
                { TAB_1ST, 0 },
                { TAB_2ND, 0 },
                { TAB_3RD, 0 },
                { TAB_4TH, 0 },
                { TAB_DUAL_5TH, 0 },
                { TAB_DUAL_6TH, 0 }
            };
            _recommendedSkillsBySkillRootId = new Dictionary<int, List<SkillDataLoader.RecommendedSkillEntry>>
            {
                { 0, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 100, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 200, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 300, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 400, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 430, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 431, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 432, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 433, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { 434, new List<SkillDataLoader.RecommendedSkillEntry>() }
            };

            // Initialize job header data
            _jobIconsByTab = new Dictionary<int, Texture2D>
            {
                { TAB_BEGINNER, null },
                { TAB_1ST, null },
                { TAB_2ND, null },
                { TAB_3RD, null },
                { TAB_4TH, null },
                { TAB_DUAL_5TH, null },
                { TAB_DUAL_6TH, null }
            };
            _jobNamesByTab = new Dictionary<int, string>
            {
                { TAB_BEGINNER, "Beginner" },
                { TAB_1ST, "1st Job" },
                { TAB_2ND, "2nd Job" },
                { TAB_3RD, "3rd Job" },
                { TAB_4TH, "4th Job" },
                { TAB_DUAL_5TH, "5th Tab" },
                { TAB_DUAL_6TH, "6th Tab" }
            };
            _displaySkillRootByTab = new Dictionary<int, int>
            {
                { TAB_BEGINNER, 0 },
                { TAB_1ST, 0 },
                { TAB_2ND, 0 },
                { TAB_3RD, 0 },
                { TAB_4TH, 0 },
                { TAB_DUAL_5TH, 0 },
                { TAB_DUAL_6TH, 0 }
            };

            // Initialize tab rectangles (positions relative to window)
            for (int i = 0; i < STANDARD_TAB_COUNT; i++)
            {
                _standardTabEnabledRects[i] = CreateDefaultTabRect(i, enabled: true, dualTab: false);
                _standardTabDisabledRects[i] = CreateDefaultTabRect(i, enabled: false, dualTab: false);
                _tabVisible[i] = true;
            }

            for (int i = 0; i < DUAL_TAB_COUNT; i++)
            {
                _dualTabEnabledRects[i] = CreateDefaultTabRect(i, enabled: true, dualTab: true);
                _dualTabDisabledRects[i] = CreateDefaultTabRect(i, enabled: false, dualTab: true);
                if (i >= STANDARD_TAB_COUNT)
                {
                    _tabVisible[i] = false;
                }
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

        public void SetRecommendedSkillEntries(int skillRootId, IEnumerable<SkillDataLoader.RecommendedSkillEntry> entries)
        {
            int normalizedSkillRootId = Math.Max(0, skillRootId);
            if (!_recommendedSkillsBySkillRootId.TryGetValue(normalizedSkillRootId, out List<SkillDataLoader.RecommendedSkillEntry> list))
            {
                list = new List<SkillDataLoader.RecommendedSkillEntry>();
                _recommendedSkillsBySkillRootId[normalizedSkillRootId] = list;
            }

            list.Clear();
            if (entries == null)
                return;

            list.AddRange(entries
                .Where(entry => entry.SkillId > 0)
                .OrderBy(entry => entry.SpentSpThreshold)
                .ThenBy(entry => entry.SkillId));
        }

        /// <summary>
        /// Set tab button textures
        /// </summary>
        public void SetTabTextures(Texture2D[] enabled, Texture2D[] disabled)
        {
            if (enabled != null)
            {
                for (int i = 0; i < Math.Min(STANDARD_TAB_COUNT, enabled.Length); i++)
                    _standardTabEnabled[i] = enabled[i];
            }
            if (disabled != null)
            {
                for (int i = 0; i < Math.Min(STANDARD_TAB_COUNT, disabled.Length); i++)
                    _standardTabDisabled[i] = disabled[i];
            }
        }

        public void SetDualTabTextures(Texture2D[] enabled, Texture2D[] disabled)
        {
            if (enabled != null)
            {
                for (int i = 0; i < Math.Min(DUAL_TAB_COUNT, enabled.Length); i++)
                    _dualTabEnabled[i] = enabled[i];
            }

            if (disabled != null)
            {
                for (int i = 0; i < Math.Min(DUAL_TAB_COUNT, disabled.Length); i++)
                    _dualTabDisabled[i] = disabled[i];
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

        public void SetTooltipOrigins(Point[] tooltipOrigins)
        {
            if (tooltipOrigins == null)
                return;

            for (int i = 0; i < Math.Min(_tooltipFrameOrigins.Length, tooltipOrigins.Length); i++)
            {
                _tooltipFrameOrigins[i] = tooltipOrigins[i];
            }
        }

        public void SetCooldownMasks(Texture2D[] cooldownMaskTextures)
        {
            _cooldownMaskTextures = cooldownMaskTextures ?? Array.Empty<Texture2D>();
        }

        public void SetTabLayout(Rectangle[] enabledRects, Rectangle[] disabledRects)
        {
            if (enabledRects != null)
            {
                for (int i = 0; i < Math.Min(STANDARD_TAB_COUNT, enabledRects.Length); i++)
                    _standardTabEnabledRects[i] = enabledRects[i];
            }

            if (disabledRects != null)
            {
                for (int i = 0; i < Math.Min(STANDARD_TAB_COUNT, disabledRects.Length); i++)
                    _standardTabDisabledRects[i] = disabledRects[i];
            }
        }

        public void SetDualTabLayout(Rectangle[] enabledRects, Rectangle[] disabledRects)
        {
            if (enabledRects != null)
            {
                for (int i = 0; i < Math.Min(DUAL_TAB_COUNT, enabledRects.Length); i++)
                    _dualTabEnabledRects[i] = enabledRects[i];
            }

            if (disabledRects != null)
            {
                for (int i = 0; i < Math.Min(DUAL_TAB_COUNT, disabledRects.Length); i++)
                    _dualTabDisabledRects[i] = disabledRects[i];
            }
        }

        public void SetUseDualTabStrip(bool useDualTabStrip)
        {
            _useDualTabStrip = useDualTabStrip;
            ApplyCurrentTab(CoerceVisibleTab(_currentTab));
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
                    int tabIndex = Math.Clamp(tab, TAB_BEGINNER, MAX_TAB_INDEX);
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

        public void InitializeRideButton(UIObject btnRide)
        {
            _btnRide = btnRide;
            if (btnRide == null)
                return;

            btnRide.ButtonClickReleased += sender => OnRideRequested?.Invoke();
            AddButton(btnRide);
        }

        public void InitializeGuildSkillButton(UIObject btnGuildSkill)
        {
            _btnGuildSkill = btnGuildSkill;
            if (btnGuildSkill == null)
                return;

            btnGuildSkill.ButtonClickReleased += sender => OnGuildSkillRequested?.Invoke();
            AddButton(btnGuildSkill);
        }

        public void ConfigureShortcutButtons(bool canOpenRide, bool canOpenGuildSkill)
        {
            _btnRide?.SetEnabled(canOpenRide);
            _btnGuildSkill?.SetEnabled(canOpenGuildSkill);
        }

        public void InitializeAranGuideButtons(UIObject[] buttons)
        {
            if (buttons == null)
                return;

            for (int i = 0; i < _aranGuideButtons.Length && i < buttons.Length; i++)
            {
                UIObject button = buttons[i];
                _aranGuideButtons[i] = button;
                if (button == null)
                    continue;

                int grade = i + 1;
                button.ButtonClickReleased += sender => OnSkillGuideRequested?.Invoke(grade);
                AddButton(button);
            }

            ConfigureAranGuideButtons(0);
        }

        public void ConfigureAranGuideButtons(int unlockedGrade)
        {
            _aranGuideUnlockedGrade = Math.Clamp(unlockedGrade, 0, _aranGuideButtons.Length);
            bool anyVisible = _aranGuideUnlockedGrade > 0;

            for (int i = 0; i < _aranGuideButtons.Length; i++)
            {
                UIObject button = _aranGuideButtons[i];
                if (button == null)
                    continue;

                button.SetVisible(anyVisible);
                button.SetEnabled(anyVisible && i < _aranGuideUnlockedGrade);
            }
        }

        /// <summary>
        /// Set the font for rendering skill names and levels
        /// </summary>
        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetSkillManager(SkillManager skillManager)
        {
            _skillManager = skillManager;
        }

        /// <summary>
        /// Set job header info for a specific tab
        /// </summary>
        /// <param name="tab">Tab index (0=Beginner, 1-4=job advancements)</param>
        /// <param name="jobIcon">The job/skillbook icon texture</param>
        /// <param name="jobName">The job name to display</param>
        public void SetJobInfo(int tab, Texture2D jobIcon, string jobName)
        {
            int tabIndex = Math.Clamp(tab, TAB_BEGINNER, MAX_TAB_INDEX);
            _jobIconsByTab[tabIndex] = jobIcon;
            if (!string.IsNullOrEmpty(jobName))
            {
                _jobNamesByTab[tabIndex] = jobName;
            }
        }

        public void SetDisplayedSkillRootId(int tab, int skillRootId)
        {
            int tabIndex = Math.Clamp(tab, TAB_BEGINNER, MAX_TAB_INDEX);
            _displaySkillRootByTab[tabIndex] = Math.Max(0, skillRootId);
        }

        public void ResetSkillRootTab(int tab)
        {
            int tabIndex = Math.Clamp(tab, TAB_BEGINNER, MAX_TAB_INDEX);
            if (_displaySkillRootByTab.TryGetValue(tabIndex, out int previousSkillRootId) &&
                _recommendedSkillsBySkillRootId.TryGetValue(previousSkillRootId, out List<SkillDataLoader.RecommendedSkillEntry> entries))
            {
                entries.Clear();
            }

            if (skillsByTab.TryGetValue(tabIndex, out List<SkillDisplayData> skills))
            {
                skills.Clear();
            }

            skillPointsByTab[tabIndex] = 0;
            _displaySkillRootByTab[tabIndex] = 0;
            _jobIconsByTab[tabIndex] = null;
            _jobNamesByTab[tabIndex] = GetDefaultTabJobName(tabIndex);

            if (_currentTab == tabIndex)
            {
                _hoveredSkillIndex = -1;
                _hoveredSpUpSkillIndex = -1;
                _hoveredSkillPointDisplay = false;
                _pressedSpUpSkillIndex = -1;
                _selectedSkillIndex = -1;
                _scrollOffset = 0;
            }
        }

        public bool TryGetDisplayedSkillRootId(int tab, out int skillRootId)
        {
            int tabIndex = Math.Clamp(tab, TAB_BEGINNER, MAX_TAB_INDEX);
            return _displaySkillRootByTab.TryGetValue(tabIndex, out skillRootId);
        }

        public int GetLoadedSkillCount(int jobAdvancement)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, MAX_TAB_INDEX);
            return skillsByTab.TryGetValue(tab, out List<SkillDisplayData> skills)
                ? skills.Count
                : 0;
        }

        public void SynchronizeLoadedSkillLevels(Func<int, int> resolveCurrentLevel, Func<int, int> resolveMaxLevel = null)
        {
            if (resolveCurrentLevel == null)
                return;

            foreach (List<SkillDisplayData> tabSkills in skillsByTab.Values)
            {
                foreach (SkillDisplayData skill in tabSkills)
                {
                    int currentLevel = Math.Max(0, resolveCurrentLevel(skill.SkillId));
                    skill.CurrentLevel = currentLevel;

                    if (resolveMaxLevel != null)
                    {
                        int resolvedMaxLevel = resolveMaxLevel(skill.SkillId);
                        skill.MaxLevel = resolvedMaxLevel > 0
                            ? Math.Max(currentLevel, resolvedMaxLevel)
                            : Math.Max(skill.MaxLevel, currentLevel);
                    }
                    else
                    {
                        skill.MaxLevel = Math.Max(skill.MaxLevel, currentLevel);
                    }
                }
            }

            RefreshVisibleTabsFromLoadedSkillRoots();
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
            DrawSkillRows(sprite, windowX, windowY, TickCount);
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
            if (_hoveredSpUpSkillIndex >= 0)
            {
                DrawNativeHintTooltip(sprite, 1, renderParameters.RenderWidth, renderParameters.RenderHeight);
                return;
            }

            if (_hoveredSkillPointDisplay)
            {
                DrawNativeHintTooltip(sprite, 2, renderParameters.RenderWidth, renderParameters.RenderHeight);
                return;
            }

            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            DrawHoveredSkillTooltip(sprite, windowX, windowY, renderParameters.RenderWidth, renderParameters.RenderHeight, TickCount);
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
        private void DrawSkillRows(SpriteBatch sprite, int windowX, int windowY, int currentTime)
        {
            var skills = CurrentSkills;
            int availableSp = GetCurrentSkillPoints();
            int recommendedSkillId = ResolveRecommendedSkillId(skills);

            for (int rowIndex = 0; rowIndex < VISIBLE_SKILLS; rowIndex++)
            {
                int skillIndex = _scrollOffset + rowIndex;
                if (skillIndex >= skills.Count)
                    break;

                int nTop = FIRST_ROW_TOP + (rowIndex * SKILL_ROW_HEIGHT);
                SkillDisplayData skill = skills[skillIndex];
                bool canLevelUp = CanLevelUp(skill, availableSp);

                DrawSkillEntry(
                    sprite,
                    skill,
                    windowX,
                    windowY,
                    nTop,
                    currentTime,
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
            int currentTime,
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
                sprite.Draw(_recommendTexture, new Vector2(windowX + RECOMMEND_X, windowY + nTop + ROW_BG_Y_OFFSET), Color.White);
            }

            if (isSelected)
            {
                var selectionRect = new Rectangle(windowX + ROW_BG_X, windowY + nTop + ROW_BG_Y_OFFSET, 140, 37);
                sprite.Draw(_debugPlaceholder, selectionRect, new Color(80, 140, 255, 70));
            }

            bool isUnlearned = skill.CurrentLevel <= 0;
            Texture2D icon = skill.GetIconForState(isUnlearned, canLevelUp && isHovered);
            int iconX = windowX + ICON_X;
            int iconY = windowY + nTop + ICON_Y_OFFSET;

            if (icon != null)
            {
                Rectangle iconRect = new Rectangle(iconX, iconY, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
                sprite.Draw(icon, iconRect, Color.White);

                if (TryGetCooldownVisualState(skill.SkillId, currentTime, out int cooldownFrameIndex, out string remainingText))
                {
                    DrawCooldownMask(sprite, iconRect, cooldownFrameIndex);

                    if (_font != null && !string.IsNullOrWhiteSpace(remainingText))
                    {
                        Vector2 textSize = ClientTextDrawing.Measure((GraphicsDevice)null, remainingText, COOLDOWN_TEXT_SCALE, _font);
                        Vector2 textPosition = new Vector2(
                            iconRect.Right - textSize.X - 2f,
                            iconRect.Bottom - textSize.Y - 1f);
                        DrawTooltipText(sprite, remainingText, textPosition, Color.White, COOLDOWN_TEXT_SCALE);
                    }
                }
            }

            if (_font != null)
            {
                string skillName = FitTextToWidth(SanitizeFontText(skill.SkillName), SKILL_NAME_MAX_WIDTH, SKILL_NAME_TEXT_SCALE);
                DrawSkillBookText(
                    sprite,
                    skillName,
                    new Vector2(windowX + NAME_X, windowY + nTop + NAME_Y_OFFSET),
                    Color.Black,
                    SKILL_NAME_TEXT_SCALE);

                int bonusLevel = Math.Max(0, skill.BonusLevel);
                int displayLevel = Math.Max(0, skill.CurrentLevel) + bonusLevel;
                string levelText = FitTextToWidth(displayLevel.ToString(), SKILL_LEVEL_MAX_WIDTH, SKILL_LEVEL_TEXT_SCALE);
                DrawSkillBookText(
                    sprite,
                    levelText,
                    new Vector2(windowX + LEVEL_X, windowY + nTop + LEVEL_Y_OFFSET),
                    bonusLevel > 0 ? new Color(0, 102, 255) : Color.Black,
                    SKILL_LEVEL_TEXT_SCALE);

                if (bonusLevel > 0)
                {
                    string bonusText = FitTextToWidth($"(+{bonusLevel})", SKILL_LEVEL_MAX_WIDTH, SKILL_LEVEL_TEXT_SCALE);
                    DrawSkillBookText(
                        sprite,
                        bonusText,
                        new Vector2(windowX + BONUS_X, windowY + nTop + LEVEL_Y_OFFSET),
                        new Color(0, 102, 255),
                        SKILL_LEVEL_TEXT_SCALE);
                }
            }

            if (skill.CurrentLevel < skill.MaxLevel)
            {
                int visibleRowIndex = (nTop - FIRST_ROW_TOP) / SKILL_ROW_HEIGHT;
                Texture2D spUpTexture = GetSpUpTexture(canLevelUp, _scrollOffset + visibleRowIndex);
                if (spUpTexture != null)
                {
                    Rectangle spUpBounds = GetSpUpButtonBounds(visibleRowIndex);
                    sprite.Draw(spUpTexture, new Vector2(spUpBounds.X, spUpBounds.Y), Color.White);
                }
            }
        }

        private int ResolveRecommendedSkillId(IReadOnlyList<SkillDisplayData> skills)
        {
            if (skills == null || skills.Count == 0)
                return 0;

            return ResolveRecommendedSkillIdFromThresholdTable(skills);
        }

        private int ResolveRecommendedSkillIdFromThresholdTable(IReadOnlyList<SkillDisplayData> skills)
        {
            int currentSkillRootId = GetDisplayedSkillRootId(_currentTab);
            int recommendationSkillRootId = SkillRootRecommendationResolver.ResolveRecommendationSourceSkillRootId(currentSkillRootId);
            if (recommendationSkillRootId <= 0 ||
                !_recommendedSkillsBySkillRootId.TryGetValue(recommendationSkillRootId, out List<SkillDataLoader.RecommendedSkillEntry> entries) ||
                entries == null ||
                entries.Count == 0)
            {
                return 0;
            }

            int spentSp = GetRecommendedSkillSpentPoints(currentSkillRootId, recommendationSkillRootId);
            return SkillRootRecommendationResolver.ResolveRecommendedSkillId(skills, entries, spentSp);
        }

        private int GetRecommendedSkillSpentPoints(int currentSkillRootId, int recommendationSkillRootId)
        {
            if (SkillRootRecommendationResolver.UsesCombinedDualBladeSpentPoints(currentSkillRootId))
            {
                return GetSpentSkillPointsForDisplayedSkillRootId(SkillRootRecommendationResolver.DualBladeRogueSkillRootId) +
                    GetSpentSkillPointsForDisplayedSkillRootId(SkillRootRecommendationResolver.DualBladeFirstSkillRootId);
            }

            return GetSpentSkillPointsForDisplayedSkillRootId(recommendationSkillRootId);
        }

        private int GetDisplayedSkillRootId(int tab)
        {
            return _displaySkillRootByTab.TryGetValue(tab, out int skillRootId)
                ? skillRootId
                : 0;
        }

        private int FindTabByDisplayedSkillRootId(int skillRootId)
        {
            foreach (KeyValuePair<int, int> entry in _displaySkillRootByTab)
            {
                if (entry.Value == skillRootId)
                {
                    return entry.Key;
                }
            }

            return -1;
        }

        private int GetSpentSkillPointsForDisplayedSkillRootId(int skillRootId)
        {
            int tab = FindTabByDisplayedSkillRootId(skillRootId);
            return tab >= 0 ? GetSpentSkillPointsForTab(tab) : 0;
        }

        private Texture2D GetSpUpTexture(bool canLevelUp, int skillIndex)
        {
            if (!canLevelUp)
                return _spUpDisabled ?? _spUpNormal;

            if (_pressedSpUpSkillIndex == skillIndex)
                return _spUpPressed ?? _spUpMouseOver ?? _spUpNormal;

            if (_hoveredSpUpSkillIndex == skillIndex)
                return _spUpMouseOver ?? _spUpNormal;

            return _spUpNormal;
        }

        private Rectangle GetSpUpButtonBounds(int visibleRowIndex)
        {
            int nTop = FIRST_ROW_TOP + (visibleRowIndex * SKILL_ROW_HEIGHT);
            Texture2D texture = _spUpNormal ?? _spUpMouseOver ?? _spUpPressed ?? _spUpDisabled;
            int width = texture?.Width ?? SP_UP_BUTTON_FALLBACK_WIDTH;
            int height = texture?.Height ?? SP_UP_BUTTON_FALLBACK_HEIGHT;
            return new Rectangle(
                Position.X + SP_UP_BUTTON_X,
                Position.Y + nTop + SP_UP_BUTTON_Y_OFFSET,
                width,
                height);
        }

        private int GetSpUpSkillIndexAtPosition(int mouseX, int mouseY)
        {
            var skills = CurrentSkills;
            int availableSp = GetCurrentSkillPoints();

            for (int rowIndex = 0; rowIndex < VISIBLE_SKILLS; rowIndex++)
            {
                int skillIndex = _scrollOffset + rowIndex;
                if (skillIndex >= skills.Count)
                    break;

                SkillDisplayData skill = skills[skillIndex];
                if (!CanLevelUp(skill, availableSp))
                    continue;

                if (GetSpUpButtonBounds(rowIndex).Contains(mouseX, mouseY))
                    return skillIndex;
            }

            return -1;
        }

        private bool TryHandleSkillLevelUp(int skillIndex)
        {
            var skills = CurrentSkills;
            if (skillIndex < 0 || skillIndex >= skills.Count)
                return false;

            SkillDisplayData skill = skills[skillIndex];
            if (!CanLevelUp(skill, GetCurrentSkillPoints()))
                return false;

            if (OnSkillLevelUpRequested != null)
                return OnSkillLevelUpRequested(skill);

            skill.CurrentLevel = Math.Min(skill.MaxLevel, skill.CurrentLevel + 1);
            skillPointsByTab[_currentTab] = Math.Max(0, GetCurrentSkillPoints() - 1);
            return true;
        }

        private void DrawSkillPointCount(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_font == null)
                return;

            string spText = SanitizeFontText(GetCurrentSkillPoints().ToString());
            float width = MeasureSkillBookText(spText, SP_DISPLAY_TEXT_SCALE).X;
            DrawSkillBookText(
                sprite,
                spText,
                new Vector2(windowX + SP_DISPLAY_X_BASE - width, windowY + SP_DISPLAY_Y),
                Color.Black,
                SP_DISPLAY_TEXT_SCALE);
        }

        private Rectangle GetSkillPointBounds()
        {
            float textWidth = 12f;
            if (_font != null)
            {
                textWidth = Math.Max(textWidth, MeasureSkillBookText(GetCurrentSkillPoints().ToString(), SP_DISPLAY_TEXT_SCALE).X);
            }

            return new Rectangle(
                Position.X + SP_DISPLAY_X_BASE - (int)Math.Ceiling(textWidth) - 4,
                Position.Y + SP_DISPLAY_Y - 2,
                (int)Math.Ceiling(textWidth) + 8,
                _font != null
                    ? (int)Math.Ceiling(ClientTextDrawing.Measure((GraphicsDevice)null, "Ag", SP_DISPLAY_TEXT_SCALE, _font).Y) + 4
                    : 14);
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

        private void DrawHoveredSkillTooltip(SpriteBatch sprite, int windowX, int windowY, int renderWidth, int renderHeight, int currentTime)
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
            string description = SanitizeFontText(skill.FormattedDescriptionOrDefault);
            string currentLevelHeader = currentLevel > 0 ? SkillTooltipClientText.FormatCurrentLevelHeader(currentLevel) : string.Empty;
            string currentLevelDescription = currentLevel > 0
                ? SanitizeFontText(skill.GetFormattedLevelDescription(currentLevel))
                : string.Empty;
            bool showNextLevel = nextLevel > 0 && nextLevel <= skill.MaxLevel && nextLevel != currentLevel;
            string nextLevelHeader = showNextLevel ? SkillTooltipClientText.FormatNextLevelHeader(nextLevel) : string.Empty;
            string nextLevelDescription = showNextLevel
                ? SanitizeFontText(skill.GetFormattedLevelDescription(nextLevel))
                : string.Empty;
            SkillLevelData tooltipLevelData = ResolveTooltipLevelData(skill, previewLevel);

            int tooltipWidth = ResolveHoveredTooltipWidth();
            float titleWidth = tooltipWidth - CLIENT_TOOLTIP_TITLE_X - CLIENT_TOOLTIP_RIGHT_PADDING;
            float sectionWidth = tooltipWidth - CLIENT_TOOLTIP_TEXT_X - CLIENT_TOOLTIP_RIGHT_PADDING;
            string[] wrappedName = WrapTooltipText(skillName, titleWidth);
            TooltipLine[] wrappedDescription = WrapTooltipText(description, sectionWidth, Color.White);
            (string tooltipStateLineMarkup, string tooltipSecondaryLineMarkup) =
                GetTooltipCooldownMarkup(skill.SkillId, tooltipLevelData, currentTime);
            TooltipLine[] wrappedStateLine = WrapTooltipText(
                SanitizeFontText(tooltipStateLineMarkup),
                sectionWidth,
                Color.White);
            TooltipLine[] wrappedSecondaryLine = WrapTooltipText(
                SanitizeFontText(tooltipSecondaryLineMarkup),
                sectionWidth,
                TOOLTIP_INLINE_HIGHLIGHT_COLOR);
            string[] wrappedCurrentHeader = WrapTooltipText(currentLevelHeader, sectionWidth);
            TooltipLine[] wrappedCurrentDescription = WrapTooltipText(currentLevelDescription, sectionWidth, new Color(180, 255, 210));
            string[] wrappedNextHeader = WrapTooltipText(nextLevelHeader, sectionWidth);
            TooltipLine[] wrappedNextDescription = WrapTooltipText(nextLevelDescription, sectionWidth, new Color(255, 238, 196));

            float titleHeight = MeasureLinesHeight(wrappedName);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float stateLineHeight = MeasureLinesHeight(wrappedStateLine);
            float secondaryLineHeight = MeasureLinesHeight(wrappedSecondaryLine);
            float currentHeaderHeight = MeasureLinesHeight(wrappedCurrentHeader);
            float currentDescriptionHeight = MeasureLinesHeight(wrappedCurrentDescription);
            float nextHeaderHeight = MeasureLinesHeight(wrappedNextHeader);
            float nextDescriptionHeight = MeasureLinesHeight(wrappedNextDescription);
            int requirementSectionHeight = GetRequirementSectionHeight(skill);
            float sectionHeight = CalculateTooltipSectionHeight(
                descriptionHeight,
                stateLineHeight,
                secondaryLineHeight,
                currentHeaderHeight,
                currentDescriptionHeight,
                nextHeaderHeight,
                nextDescriptionHeight,
                requirementSectionHeight);
            float contentY = Math.Max(
                CLIENT_TOOLTIP_TEXT_Y,
                CLIENT_TOOLTIP_TITLE_Y + titleHeight + 2f);
            float iconBottom = contentY + SKILL_ICON_SIZE;
            float textBottom = contentY + sectionHeight;
            int tooltipHeight = Math.Max(
                CLIENT_TOOLTIP_BASE_HEIGHT,
                (int)Math.Ceiling(Math.Max(iconBottom, textBottom) + TOOLTIP_PADDING));

            Point anchorPoint = new Point(_lastMousePosition.X, _lastMousePosition.Y + HOVER_TOOLTIP_CURSOR_GAP);
            Rectangle backgroundRect = ResolveHoverTooltipRect(
                anchorPoint,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight);
            DrawHoverTooltipBackground(sprite, backgroundRect);

            int titleX = backgroundRect.X + CLIENT_TOOLTIP_TITLE_X;
            int titleY = backgroundRect.Y + CLIENT_TOOLTIP_TITLE_Y;
            DrawTooltipLines(sprite, wrappedName, titleX, titleY, new Color(255, 220, 120));

            int clientContentY = backgroundRect.Y + (int)Math.Ceiling(contentY);
            int iconX = backgroundRect.X + CLIENT_TOOLTIP_ICON_X;
            Texture2D icon = skill.GetIconForState(false, true) ?? skill.IconTexture;
            if (icon != null)
            {
                sprite.Draw(icon, new Rectangle(iconX, clientContentY, SKILL_ICON_SIZE, SKILL_ICON_SIZE), Color.White);
            }

            int textX = backgroundRect.X + CLIENT_TOOLTIP_TEXT_X;
            float sectionY = clientContentY;
            if (descriptionHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedDescription, textX, sectionY);
                sectionY += descriptionHeight;
            }

            if (stateLineHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, wrappedStateLine, textX, sectionY);
                sectionY += stateLineHeight;
            }

            if (secondaryLineHeight > 0f)
            {
                sectionY += stateLineHeight > 0f ? 2f : TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, wrappedSecondaryLine, textX, sectionY);
                sectionY += secondaryLineHeight;
            }

            if (currentHeaderHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, wrappedCurrentHeader, textX, sectionY, new Color(255, 204, 120));
                sectionY += currentHeaderHeight;
            }

            if (currentDescriptionHeight > 0f)
            {
                sectionY += 2f;
                DrawTooltipLines(sprite, wrappedCurrentDescription, textX, sectionY);
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
                DrawTooltipLines(sprite, wrappedNextDescription, textX, sectionY);
                sectionY += nextDescriptionHeight;
            }

            DrawSkillRequirementSection(sprite, skill, backgroundRect.X, sectionY + TOOLTIP_SECTION_GAP);
        }

        private SkillLevelData ResolveTooltipLevelData(SkillDisplayData skill, int level)
        {
            SkillData skillData = _skillManager?.GetSkillData(skill?.SkillId ?? 0);
            return skillData?.GetLevel(level);
        }

        private (string StateLineMarkup, string SecondaryLineMarkup) GetTooltipCooldownMarkup(int skillId, SkillLevelData levelData, int currentTime)
        {
            if (levelData == null)
            {
                return (string.Empty, string.Empty);
            }

            SkillManager.CooldownUiState cooldownState = default;
            bool hasCooldownState = _skillManager != null
                && _skillManager.TryGetCooldownUiState(skillId, currentTime, out cooldownState);
            return SkillCooldownTooltipText.BuildSkillTooltipMarkup(
                levelData,
                hasCooldownState,
                hasCooldownState ? cooldownState.RemainingMs : 0,
                hasCooldownState ? cooldownState.TooltipStateText : null);
        }

        private static float CalculateTooltipSectionHeight(
            float descriptionHeight,
            float stateLineHeight,
            float secondaryLineHeight,
            float currentHeaderHeight,
            float currentDescriptionHeight,
            float nextHeaderHeight,
            float nextDescriptionHeight,
            float requirementSectionHeight)
        {
            float height = 0f;
            if (descriptionHeight > 0f)
                height += descriptionHeight;
            if (stateLineHeight > 0f)
                height += (height > 0f ? TOOLTIP_SECTION_GAP : 0f) + stateLineHeight;
            if (secondaryLineHeight > 0f)
                height += (height > 0f ? (stateLineHeight > 0f ? 2f : TOOLTIP_SECTION_GAP) : 0f) + secondaryLineHeight;
            if (currentHeaderHeight > 0f)
                height += (height > 0f ? TOOLTIP_SECTION_GAP : 0f) + currentHeaderHeight;
            if (currentDescriptionHeight > 0f)
                height += 2f + currentDescriptionHeight;
            if (nextHeaderHeight > 0f)
                height += (height > 0f ? TOOLTIP_SECTION_GAP : 0f) + nextHeaderHeight;
            if (nextDescriptionHeight > 0f)
                height += 2f + nextDescriptionHeight;
            if (requirementSectionHeight > 0f)
                height += (height > 0f ? TOOLTIP_SECTION_GAP : 0f) + requirementSectionHeight;

            return height;
        }

        private int GetRequirementSectionHeight(SkillDisplayData skill)
        {
            int requirementCount = skill?.Requirements?.Count ?? 0;
            if (requirementCount <= 0)
                return 0;

            return CLIENT_TOOLTIP_REQUIREMENT_SECTION_BASE_HEIGHT + (requirementCount * CLIENT_TOOLTIP_REQUIREMENT_ROW_HEIGHT);
        }

        private void DrawSkillRequirementSection(SpriteBatch sprite, SkillDisplayData skill, int tooltipX, float sectionY)
        {
            if (_font == null || skill?.Requirements == null || skill.Requirements.Count == 0)
                return;

            DrawTooltipText(
                sprite,
                SkillTooltipClientText.ResolveRequiredSkillHeaderText(),
                new Vector2(tooltipX + CLIENT_TOOLTIP_REQUIREMENT_HEADER_X, sectionY + CLIENT_TOOLTIP_REQUIREMENT_HEADER_Y_OFFSET),
                new Color(255, 204, 120));

            for (int i = 0; i < skill.Requirements.Count; i++)
            {
                SkillRequirementDisplayData requirement = skill.Requirements[i];
                float rowY = sectionY + CLIENT_TOOLTIP_REQUIREMENT_FIRST_ROW_Y + (i * CLIENT_TOOLTIP_REQUIREMENT_ROW_HEIGHT);
                int rowTop = (int)Math.Round(rowY);

                if (_debugPlaceholder != null)
                {
                    sprite.Draw(
                        _debugPlaceholder,
                        new Rectangle(
                            tooltipX + CLIENT_TOOLTIP_REQUIREMENT_ICON_X,
                            rowTop - 1,
                            CLIENT_TOOLTIP_REQUIREMENT_ICON_SIZE,
                            CLIENT_TOOLTIP_REQUIREMENT_ICON_SIZE),
                        TOOLTIP_REQUIREMENT_ROW_BACKGROUND_COLOR);
                }

                if (requirement.IconTexture != null)
                {
                    sprite.Draw(
                        requirement.IconTexture,
                        new Vector2(
                            tooltipX + CLIENT_TOOLTIP_REQUIREMENT_ICON_X + 1 - requirement.IconOrigin.X,
                            rowTop - requirement.IconOrigin.Y),
                        Color.White);
                }

                DrawTooltipText(
                    sprite,
                    SanitizeFontText(requirement.SkillName),
                    new Vector2(tooltipX + CLIENT_TOOLTIP_REQUIREMENT_NAME_X, rowY + CLIENT_TOOLTIP_REQUIREMENT_NAME_Y),
                    Color.White);
                DrawTooltipText(
                    sprite,
                    SkillTooltipClientText.FormatRequiredSkillLevelText(requirement.RequiredLevel),
                    new Vector2(tooltipX + CLIENT_TOOLTIP_REQUIREMENT_LEVEL_X, rowY + CLIENT_TOOLTIP_REQUIREMENT_LEVEL_Y),
                    new Color(210, 210, 210));
            }
        }

        private int ResolveHoveredTooltipWidth()
        {
            return CLIENT_TOOLTIP_WIDTH;
        }

        private int ResolveHintTooltipBaseWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TOOLTIP_FALLBACK_WIDTH;
        }

        private float MeasureTooltipTextWidth(string text)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return 0f;

            float maxWidth = 0f;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                maxWidth = Math.Max(maxWidth, ClientTextDrawing.Measure((GraphicsDevice)null, line, 1f, _font).X);
            }

            return maxWidth;
        }

        private Rectangle CreateTooltipRectFromAnchor(Point anchorPoint, int tooltipWidth, int tooltipHeight, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;
            Point origin = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrameOrigins.Length
                ? _tooltipFrameOrigins[tooltipFrameIndex]
                : Point.Zero;

            if (tooltipFrame != null && origin != Point.Zero)
            {
                float scaleX = tooltipFrame.Width > 0 ? tooltipWidth / (float)tooltipFrame.Width : 1f;
                float scaleY = tooltipFrame.Height > 0 ? tooltipHeight / (float)tooltipFrame.Height : 1f;
                return new Rectangle(
                    anchorPoint.X - (int)Math.Round(origin.X * scaleX),
                    anchorPoint.Y - (int)Math.Round(origin.Y * scaleY),
                    tooltipWidth,
                    tooltipHeight);
            }

            return tooltipFrameIndex switch
            {
                0 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight),
                2 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y, tooltipWidth, tooltipHeight),
                _ => new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight)
            };
        }

        private static int ComputeTooltipOverflow(Rectangle rect, int renderWidth, int renderHeight)
        {
            int overflow = 0;

            if (rect.Left < TOOLTIP_PADDING)
                overflow += TOOLTIP_PADDING - rect.Left;
            if (rect.Top < TOOLTIP_PADDING)
                overflow += TOOLTIP_PADDING - rect.Top;
            if (rect.Right > renderWidth - TOOLTIP_PADDING)
                overflow += rect.Right - (renderWidth - TOOLTIP_PADDING);
            if (rect.Bottom > renderHeight - TOOLTIP_PADDING)
                overflow += rect.Bottom - (renderHeight - TOOLTIP_PADDING);

            return overflow;
        }

        private static Rectangle ClampTooltipRect(Rectangle rect, int renderWidth, int renderHeight)
        {
            int minX = TOOLTIP_PADDING;
            int minY = TOOLTIP_PADDING;
            int maxX = Math.Max(minX, renderWidth - TOOLTIP_PADDING - rect.Width);
            int maxY = Math.Max(minY, renderHeight - TOOLTIP_PADDING - rect.Height);

            return new Rectangle(
                Math.Clamp(rect.X, minX, maxX),
                Math.Clamp(rect.Y, minY, maxY),
                rect.Width,
                rect.Height);
        }

        private Rectangle ResolveHoverTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight)
        {
            Rectangle[] candidates =
            {
                new Rectangle(anchorPoint.X, anchorPoint.Y, tooltipWidth, tooltipHeight),
                new Rectangle(anchorPoint.X - tooltipWidth, anchorPoint.Y, tooltipWidth, tooltipHeight),
                new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight - HOVER_TOOLTIP_CURSOR_GAP, tooltipWidth, tooltipHeight),
                new Rectangle(anchorPoint.X - tooltipWidth, anchorPoint.Y - tooltipHeight - HOVER_TOOLTIP_CURSOR_GAP, tooltipWidth, tooltipHeight)
            };

            Rectangle bestRect = candidates[0];
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Rectangle candidate = candidates[i];
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight);
                if (overflow == 0)
                    return candidate;

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    bestRect = candidate;
                }
            }

            return ClampTooltipRect(bestRect, renderWidth, renderHeight);
        }

        private Rectangle ResolveTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            ReadOnlySpan<int> framePreference,
            out int tooltipFrameIndex)
        {
            Rectangle bestRect = Rectangle.Empty;
            int bestFrame = framePreference.Length > 0 ? framePreference[0] : 1;
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < framePreference.Length; i++)
            {
                int frameIndex = framePreference[i];
                Rectangle candidate = CreateTooltipRectFromAnchor(anchorPoint, tooltipWidth, tooltipHeight, frameIndex);
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight);

                if (overflow == 0)
                {
                    tooltipFrameIndex = frameIndex;
                    return candidate;
                }

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    bestFrame = frameIndex;
                    bestRect = candidate;
                }
            }

            tooltipFrameIndex = bestFrame;
            return ClampTooltipRect(bestRect, renderWidth, renderHeight);
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

        private void DrawHoverTooltipBackground(SpriteBatch sprite, Rectangle rect)
        {
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

        private void DrawNativeHintTooltip(SpriteBatch sprite, int tooltipFrameIndex, int renderWidth, int renderHeight)
        {
            if (tooltipFrameIndex < 0 || tooltipFrameIndex >= _tooltipFrames.Length)
                return;

            Texture2D tooltipFrame = _tooltipFrames[tooltipFrameIndex];
            if (tooltipFrame == null)
                return;

            Point anchorPoint = tooltipFrameIndex switch
            {
                1 when _hoveredSpUpSkillIndex >= _scrollOffset =>
                    new Point(
                        GetSpUpButtonBounds(_hoveredSpUpSkillIndex - _scrollOffset).Left,
                        GetSpUpButtonBounds(_hoveredSpUpSkillIndex - _scrollOffset).Bottom),
                2 => new Point(GetSkillPointBounds().Right, GetSkillPointBounds().Top),
                _ => new Point(_lastMousePosition.X, _lastMousePosition.Y + 20)
            };

            Rectangle tooltipRect = ResolveTooltipRect(
                anchorPoint,
                tooltipFrame.Width,
                tooltipFrame.Height,
                renderWidth,
                renderHeight,
                stackalloc[] { tooltipFrameIndex },
                out _);
            sprite.Draw(tooltipFrame, tooltipRect, Color.White);
        }

        private void DrawTooltipLines(SpriteBatch sprite, string[] lines, int x, float y, Color color)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * ResolveTooltipLineHeight())), color);
            }
        }

        private void DrawTooltipLines(SpriteBatch sprite, TooltipLine[] lines, int x, float y)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                float drawX = x;
                TooltipLine line = lines[i];
                if (line?.Runs == null)
                    continue;

                for (int runIndex = 0; runIndex < line.Runs.Count; runIndex++)
                {
                    TooltipTextRun run = line.Runs[runIndex];
                    if (string.IsNullOrEmpty(run.Text))
                        continue;

                    Vector2 position = new Vector2(drawX, y + (i * ResolveTooltipLineHeight()));
                    DrawTooltipText(sprite, run.Text, position, run.Color);
                    drawX += ClientTextDrawing.Measure((GraphicsDevice)null, run.Text, 1f, _font).X;
                }
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale = 1f)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            ClientTextDrawing.Draw(sprite, text, position + Vector2.One, Color.Black, scale, _font);
            ClientTextDrawing.Draw(sprite, text, position, color, scale, _font);
        }

        private bool TryGetCooldownVisualState(int skillId, int currentTime, out int frameIndex, out string remainingText)
        {
            frameIndex = 0;
            remainingText = string.Empty;
            return _skillManager != null
                && _skillManager.TryGetCooldownMaskVisualState(skillId, currentTime, out frameIndex, out remainingText);
        }

        private void DrawCooldownMask(SpriteBatch sprite, Rectangle iconRect, int frameIndex)
        {
            if (_cooldownMaskTextures.Length > 0)
            {
                int resolvedFrameIndex = Math.Clamp(frameIndex, 0, _cooldownMaskTextures.Length - 1);
                Texture2D maskTexture = _cooldownMaskTextures[resolvedFrameIndex];
                if (maskTexture != null)
                {
                    sprite.Draw(maskTexture, iconRect, Color.White);
                    return;
                }
            }

            if (_debugPlaceholder == null)
            {
                return;
            }

            float remainingProgress = SkillManager.ResolveCooldownMaskFallbackFillRatio(frameIndex);
            int overlayHeight = Math.Clamp((int)Math.Ceiling(iconRect.Height * remainingProgress), 0, iconRect.Height);
            if (overlayHeight <= 0)
            {
                return;
            }

            Rectangle overlayRect = new Rectangle(
                iconRect.X,
                iconRect.Bottom - overlayHeight,
                iconRect.Width,
                overlayHeight);
            sprite.Draw(_debugPlaceholder, overlayRect, new Color(0, 0, 0, 150));
        }

        private float MeasureLongestLine(string[] lines)
        {
            float width = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    width = Math.Max(width, ClientTextDrawing.Measure((GraphicsDevice)null, lines[i], 1f, _font).X);
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

            return nonEmptyLines > 0 ? nonEmptyLines * ResolveTooltipLineHeight() : 0f;
        }

        private float MeasureLinesHeight(TooltipLine[] lines)
        {
            if (lines == null || lines.Length == 0)
                return 0f;

            int nonEmptyLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null && !lines[i].IsEmpty)
                    nonEmptyLines++;
            }

            return nonEmptyLines > 0 ? nonEmptyLines * ResolveTooltipLineHeight() : 0f;
        }

        private float ResolveTooltipLineHeight()
        {
            if (_font == null)
                return 0f;

            float measuredHeight = ClientTextDrawing.Measure((GraphicsDevice)null, "Ag", 1f, _font).Y;
            return measuredHeight > 0f ? measuredHeight : _font.LineSpacing;
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
                    if (!string.IsNullOrEmpty(currentLine) && ClientTextDrawing.Measure((GraphicsDevice)null, candidate, 1f, _font).X > maxWidth)
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

        private TooltipLine[] WrapTooltipText(string text, float maxWidth, Color baseColor)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<TooltipLine>();

            List<TooltipToken> tokens = TokenizeTooltipText(text, baseColor);
            List<TooltipLine> lines = new List<TooltipLine>();
            TooltipLine currentLine = new TooltipLine();
            float currentWidth = 0f;

            foreach (TooltipToken token in tokens)
            {
                if (token.IsNewLine)
                {
                    lines.Add(currentLine);
                    currentLine = new TooltipLine();
                    currentWidth = 0f;
                    continue;
                }

                if (token.IsWhitespace)
                {
                    if (currentLine.Runs.Count == 0)
                        continue;

                    float whitespaceWidth = MeasureTooltipRunWidth(token.Text);
                    if (currentWidth + whitespaceWidth > maxWidth)
                    {
                        lines.Add(currentLine);
                        currentLine = new TooltipLine();
                        currentWidth = 0f;
                        continue;
                    }

                    currentLine.Append(token.Text, token.Color);
                    currentWidth += whitespaceWidth;
                    continue;
                }

                float tokenWidth = MeasureTooltipRunWidth(token.Text);
                if (currentLine.Runs.Count > 0 && currentWidth + tokenWidth > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new TooltipLine();
                    currentWidth = 0f;
                }

                currentLine.Append(token.Text, token.Color);
                currentWidth += tokenWidth;
            }

            if (currentLine.Runs.Count > 0 || lines.Count == 0)
                lines.Add(currentLine);

            while (lines.Count > 0 && lines[^1].IsEmpty)
                lines.RemoveAt(lines.Count - 1);

            return lines.ToArray();
        }

        private float MeasureTooltipRunWidth(string text)
        {
            if (_font == null || string.IsNullOrEmpty(text))
                return 0f;

            return ClientTextDrawing.Measure((GraphicsDevice)null, text, 1f, _font).X;
        }

        private List<TooltipToken> TokenizeTooltipText(string text, Color baseColor)
        {
            List<TooltipToken> tokens = new List<TooltipToken>();
            foreach ((string segmentText, Color segmentColor) in ParseTooltipSegments(text, baseColor))
            {
                int index = 0;
                while (index < segmentText.Length)
                {
                    char ch = segmentText[index];
                    if (ch == '\n')
                    {
                        tokens.Add(TooltipToken.NewLine());
                        index++;
                        continue;
                    }

                    int start = index;
                    bool whitespace = char.IsWhiteSpace(ch);
                    while (index < segmentText.Length &&
                           segmentText[index] != '\n' &&
                           char.IsWhiteSpace(segmentText[index]) == whitespace)
                    {
                        index++;
                    }

                    string tokenText = segmentText[start..index];
                    if (whitespace)
                        tokenText = tokenText.Replace('\t', ' ');

                    if (tokenText.Length > 0)
                        tokens.Add(new TooltipToken(tokenText, segmentColor, isWhitespace: whitespace, isNewLine: false));
                }
            }

            return tokens;
        }

        private IEnumerable<(string Text, Color Color)> ParseTooltipSegments(string text, Color baseColor)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            StringBuilder builder = new StringBuilder();
            int index = 0;
            while (index < text.Length)
            {
                if (text[index] == '#' && index + 1 < text.Length)
                {
                    if (text[index + 1] == '#')
                    {
                        builder.Append('#');
                        index += 2;
                        continue;
                    }

                    int closingIndex = text.IndexOf('#', index + 2);
                    if (closingIndex > index + 2)
                    {
                        if (builder.Length > 0)
                        {
                            yield return (builder.ToString(), baseColor);
                            builder.Clear();
                        }

                        char marker = char.ToLowerInvariant(text[index + 1]);
                        string segment = text.Substring(index + 2, closingIndex - index - 2);
                        yield return (segment, ResolveTooltipMarkerColor(marker, baseColor));
                        index = closingIndex + 1;
                        continue;
                    }
                }

                builder.Append(text[index]);
                index++;
            }

            if (builder.Length > 0)
                yield return (builder.ToString(), baseColor);
        }

        private Color ResolveTooltipMarkerColor(char marker, Color baseColor)
        {
            return marker switch
            {
                'c' => TOOLTIP_INLINE_HIGHLIGHT_COLOR,
                'b' => new Color(130, 190, 255),
                'g' => new Color(160, 255, 160),
                'r' => new Color(255, 150, 150),
                _ => baseColor
            };
        }

        private void DrawBookName(SpriteBatch sprite, int windowX, int windowY, string jobName)
        {
            if (_font == null || string.IsNullOrWhiteSpace(jobName))
                return;

            jobName = SanitizeFontText(jobName);
            float width = MeasureSkillBookText(jobName, BOOK_NAME_TEXT_SCALE).X;
            if (width < BOOK_NAME_MAX_WIDTH)
            {
                DrawSkillBookText(
                    sprite,
                    jobName,
                    new Vector2(windowX + BOOK_NAME_CENTER_X - (width / 2f), windowY + BOOK_NAME_SINGLE_LINE_Y),
                    Color.Black,
                    BOOK_NAME_TEXT_SCALE);
                return;
            }

            if (!TryResolveBookNameSplit(jobName, out string firstLine, out string secondLine))
            {
                string fitted = FitTextToWidth(jobName, BOOK_NAME_MAX_WIDTH, BOOK_NAME_TEXT_SCALE);
                float fittedWidth = MeasureSkillBookText(fitted, BOOK_NAME_TEXT_SCALE).X;
                DrawSkillBookText(
                    sprite,
                    fitted,
                    new Vector2(windowX + BOOK_NAME_CENTER_X - (fittedWidth / 2f), windowY + BOOK_NAME_MULTI_LINE_FIRST_Y),
                    Color.Black,
                    BOOK_NAME_TEXT_SCALE);
                return;
            }

            float firstWidth = MeasureSkillBookText(firstLine, BOOK_NAME_TEXT_SCALE).X;
            float secondWidth = MeasureSkillBookText(secondLine, BOOK_NAME_TEXT_SCALE).X;
            DrawSkillBookText(
                sprite,
                firstLine,
                new Vector2(windowX + BOOK_NAME_CENTER_X - (firstWidth / 2f), windowY + BOOK_NAME_MULTI_LINE_FIRST_Y),
                Color.Black,
                BOOK_NAME_TEXT_SCALE);
            DrawSkillBookText(
                sprite,
                secondLine,
                new Vector2(windowX + BOOK_NAME_CENTER_X - (secondWidth / 2f), windowY + BOOK_NAME_MULTI_LINE_SECOND_Y),
                Color.Black,
                BOOK_NAME_TEXT_SCALE);
        }

        private bool TryResolveBookNameSplit(string jobName, out string firstLine, out string secondLine)
        {
            firstLine = string.Empty;
            secondLine = string.Empty;

            if (string.IsNullOrWhiteSpace(jobName))
                return false;

            string[] words = jobName
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2)
                return false;

            string currentFirstLine = words[0];
            if (MeasureSkillBookText(currentFirstLine, BOOK_NAME_TEXT_SCALE).X > BOOK_NAME_MAX_WIDTH)
                return false;

            for (int wordIndex = 1; wordIndex < words.Length; wordIndex++)
            {
                string candidateFirstLine = string.Concat(currentFirstLine, " ", words[wordIndex]);
                if (MeasureSkillBookText(candidateFirstLine, BOOK_NAME_TEXT_SCALE).X > BOOK_NAME_MAX_WIDTH)
                {
                    string candidateSecondLine = string.Join(" ", words.Skip(wordIndex));
                    if (MeasureSkillBookText(candidateSecondLine, BOOK_NAME_TEXT_SCALE).X > BOOK_NAME_MAX_WIDTH)
                        return false;

                    firstLine = currentFirstLine;
                    secondLine = candidateSecondLine;
                    return true;
                }

                currentFirstLine = candidateFirstLine;
            }

            return false;
        }

        private Vector2 MeasureSkillBookText(string text, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Vector2.Zero;

            return ClientTextDrawing.Measure((GraphicsDevice)null, text, scale, _font);
        }

        private void DrawSkillBookText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            ClientTextDrawing.Draw(sprite, text, position, color, scale, _font);
        }

        private string FitTextToWidth(string text, int maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string sanitized = text.Trim();
            if (MeasureSkillBookText(sanitized, scale).X <= maxWidth)
                return sanitized;

            const string ellipsis = "...";
            string working = sanitized;
            while (working.Length > 1)
            {
                working = working[..^1].TrimEnd();
                string candidate = working + ellipsis;
                if (MeasureSkillBookText(candidate, scale).X <= maxWidth)
                    return candidate;
            }

            return ellipsis;
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
            int tabCount = _useDualTabStrip ? DUAL_TAB_COUNT : STANDARD_TAB_COUNT;
            for (int i = 0; i < tabCount; i++)
            {
                if (!_tabVisible[i])
                    continue;

                Texture2D tabTexture = GetTabTexture(i);
                if (tabTexture != null)
                {
                    sprite.Draw(tabTexture, GetTabBounds(windowX, windowY, i), Color.White);
                }
            }
        }

        private Rectangle GetTabBounds(int windowX, int windowY, int tabIndex)
        {
            Texture2D tabTexture = GetTabTexture(tabIndex);
            Rectangle tabRect = GetTabRect(tabIndex);
            return new Rectangle(
                windowX + tabRect.X,
                windowY + tabRect.Y,
                tabTexture?.Width ?? tabRect.Width,
                tabTexture?.Height ?? tabRect.Height);
        }

        private Texture2D GetTabTexture(int tabIndex)
        {
            if (_useDualTabStrip)
            {
                return tabIndex == _currentTab ? _dualTabEnabled[tabIndex] : _dualTabDisabled[tabIndex];
            }

            return tabIndex == _currentTab ? _standardTabEnabled[tabIndex] : _standardTabDisabled[tabIndex];
        }

        private Rectangle GetTabRect(int tabIndex)
        {
            if (_useDualTabStrip)
            {
                return tabIndex == _currentTab ? _dualTabEnabledRects[tabIndex] : _dualTabDisabledRects[tabIndex];
            }

            return tabIndex == _currentTab ? _standardTabEnabledRects[tabIndex] : _standardTabDisabledRects[tabIndex];
        }

        private static Rectangle CreateDefaultTabRect(int tabIndex, bool enabled, bool dualTab)
        {
            int width = 30;

            if (!dualTab)
            {
                int x = 10 + (tabIndex * 31);
                int y = enabled ? 27 : 29;
                int height = enabled ? 20 : 18;

                return new Rectangle(x, y, width, height);
            }

            // Dual-tab layout: two tabs per column
            int column = tabIndex / 2;
            bool isBottomTab = (tabIndex % 2) == 1;

            int xDual = 10 + (column * 31);
            int yDual;

            if (enabled)
                yDual = isBottomTab ? 47 : 27;
            else
                yDual = isBottomTab ? 49 : 29;

            int heightDual = enabled ? 20 : 18;

            return new Rectangle(xDual, yDual, width, heightDual);
        }

        private void ApplyCurrentTab(int tabIndex)
        {
            _currentTab = tabIndex;
            _scrollOffset = 0;
            _hoveredSkillIndex = -1;
            _hoveredSpUpSkillIndex = -1;
            _hoveredSkillPointDisplay = false;
            _pressedSpUpSkillIndex = -1;
            _selectedSkillIndex = -1;
        }

        private static string GetDefaultTabJobName(int tabIndex)
        {
            return tabIndex switch
            {
                TAB_BEGINNER => "Beginner",
                TAB_1ST => "1st Job",
                TAB_2ND => "2nd Job",
                TAB_3RD => "3rd Job",
                TAB_4TH => "4th Job",
                TAB_DUAL_5TH => "5th Tab",
                TAB_DUAL_6TH => "6th Tab",
                _ => "Beginner"
            };
        }

        private int CoerceVisibleTab(int requestedTab)
        {
            int maxTab = _useDualTabStrip ? MAX_TAB_INDEX : TAB_4TH;
            int clampedTab = Math.Clamp(requestedTab, TAB_BEGINNER, maxTab);
            if (_tabVisible[clampedTab])
                return clampedTab;

            for (int tab = clampedTab - 1; tab >= TAB_BEGINNER; tab--)
            {
                if (_tabVisible[tab])
                    return tab;
            }

            for (int tab = clampedTab + 1; tab <= maxTab; tab++)
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
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, MAX_TAB_INDEX);

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
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, MAX_TAB_INDEX);

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
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, MAX_TAB_INDEX);

            if (skillsByTab.TryGetValue(tab, out var skills))
            {
                int beforeCount = skills.Count;
                skills.AddRange(skillDataList);
                System.Diagnostics.Debug.WriteLine($"[SkillUIBigBang] Added skills to tab {tab}: was {beforeCount}, now {skills.Count}");
            }
        }

        public void RefreshVisibleTabsFromLoadedSkillRoots()
        {
            var visibleTabs = new List<int>();
            foreach (KeyValuePair<int, List<SkillDisplayData>> entry in skillsByTab.OrderBy(entry => entry.Key))
            {
                int tab = entry.Key;
                if (!_displaySkillRootByTab.TryGetValue(tab, out int skillRootId) || skillRootId < 0)
                    continue;

                List<SkillDisplayData> rootSkills = entry.Value;
                if (rootSkills == null || rootSkills.Count == 0)
                    continue;

                bool hasVisibleSkill = rootSkills.Any(skill =>
                    SkillRootVisibilityResolver.IsSkillVisible(skill, _currentJobId, _currentSubJob));
                if (!hasVisibleSkill)
                    continue;

                visibleTabs.Add(tab);
            }

            SetVisibleTabs(visibleTabs);
        }

        public void UpdateSkillLevel(int skillId, int currentLevel, int maxLevel)
        {
            bool updatedSkill = false;
            foreach (var tabSkills in skillsByTab.Values)
            {
                foreach (var skill in tabSkills)
                {
                    if (skill.SkillId != skillId)
                        continue;

                    skill.CurrentLevel = Math.Max(0, currentLevel);
                    skill.MaxLevel = Math.Max(skill.CurrentLevel, maxLevel);
                    updatedSkill = true;
                }
            }

            if (updatedSkill)
                RefreshVisibleTabsFromLoadedSkillRoots();
        }

        /// <summary>
        /// Set skill points for a job advancement tab
        /// </summary>
        public void SetSkillPoints(int jobAdvancement, int points)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, MAX_TAB_INDEX);
            skillPointsByTab[tab] = Math.Max(0, points);
        }

        public void AddSkillPoints(int jobAdvancement, int delta)
        {
            if (delta == 0)
            {
                return;
            }

            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, MAX_TAB_INDEX);
            skillPointsByTab.TryGetValue(tab, out int currentPoints);
            skillPointsByTab[tab] = Math.Max(0, currentPoints + delta);
        }

        public void SetCharacterLevel(int level)
        {
            _characterLevel = Math.Max(1, level);
        }

        public void SetCharacterJob(int jobId, int subJob = 0)
        {
            _currentJobId = Math.Max(0, jobId);
            _currentSubJob = Math.Max(0, subJob);
            RefreshVisibleTabsFromLoadedSkillRoots();
        }

        public void RecalculateSkillPointsFromCurrentLevels()
        {
            Dictionary<int, int> resolvedPoints = SkillPointParityCalculator.CalculateRemainingPointsByTab(
                _characterLevel,
                skillsByTab,
                _displaySkillRootByTab);
            foreach (KeyValuePair<int, int> entry in resolvedPoints)
                skillPointsByTab[entry.Key] = entry.Value;
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

        public bool HasAvailableSkillPoints()
        {
            foreach (KeyValuePair<int, int> entry in skillPointsByTab)
            {
                if (entry.Value > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanLevelUp(SkillDisplayData skill, int availableSp)
        {
            if (skill == null || availableSp <= 0 || skill.CurrentLevel >= skill.MaxLevel)
                return false;

            if (skill.RequiredCharacterLevel > 0 && _characterLevel < skill.RequiredCharacterLevel)
                return false;

            if (skill.RequiredSkillId > 0)
            {
                int requiredLevel = Math.Max(1, skill.RequiredSkillLevel);
                if (GetCurrentSkillLevel(skill.RequiredSkillId) < requiredLevel)
                    return false;
            }

            return true;
        }

        private int GetCurrentSkillLevel(int skillId)
        {
            if (skillId <= 0)
                return 0;

            foreach (List<SkillDisplayData> tabSkills in skillsByTab.Values)
            {
                for (int i = 0; i < tabSkills.Count; i++)
                {
                    SkillDisplayData skill = tabSkills[i];
                    if (skill?.SkillId == skillId)
                        return Math.Max(0, skill.CurrentLevel);
                }
            }

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
                skillPointsByTab[kvp.Key] = 0;
            }

            foreach (var kvp in _recommendedSkillsBySkillRootId)
            {
                kvp.Value.Clear();
            }

            foreach (int tab in _displaySkillRootByTab.Keys.ToArray())
            {
                _displaySkillRootByTab[tab] = 0;
            }

            _hoveredSkillIndex = -1;
            _hoveredSpUpSkillIndex = -1;
            _hoveredSkillPointDisplay = false;
            _pressedSpUpSkillIndex = -1;
            _selectedSkillIndex = -1;
        }

        /// <summary>
        /// Clear skills from a specific tab
        /// </summary>
        public void ClearSkills(int jobAdvancement)
        {
            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, MAX_TAB_INDEX);
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

        private int GetCurrentSpentSkillPoints()
        {
            return GetSpentSkillPointsForTab(_currentTab);
        }

        private int GetSpentSkillPointsForTab(int tab)
        {
            if (!skillsByTab.TryGetValue(tab, out List<SkillDisplayData> skills) || skills == null)
                return 0;

            int spentSp = 0;
            List<SkillDisplayData> visibleSkills = GetVisibleSkills(skills);
            for (int i = 0; i < visibleSkills.Count; i++)
            {
                spentSp += Math.Max(0, visibleSkills[i]?.CurrentLevel ?? 0);
            }

            return spentSp;
        }

        private List<SkillDisplayData> GetVisibleSkills(List<SkillDisplayData> skills)
        {
            if (skills == null || skills.Count == 0)
                return new List<SkillDisplayData>();

            var visibleSkills = new List<SkillDisplayData>(skills.Count);
            for (int i = 0; i < skills.Count; i++)
            {
                SkillDisplayData skill = skills[i];
                if (SkillRootVisibilityResolver.IsSkillVisible(skill, _currentJobId, _currentSubJob))
                    visibleSkills.Add(skill);
            }

            return visibleSkills;
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

        private bool TryHandleTabKeyboardNavigation(KeyboardState keyboardState)
        {
            bool WasPressed(Keys key) => keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);

            if (WasPressed(Keys.Left))
                return TrySwitchVisibleTab(-1);

            if (WasPressed(Keys.Right))
                return TrySwitchVisibleTab(1);

            return false;
        }

        private bool TrySwitchVisibleTab(int direction)
        {
            if (direction == 0)
                return false;

            int nextTab = FindAdjacentVisibleTab(_currentTab, direction);
            if (nextTab == _currentTab)
                return false;

            CurrentTab = nextTab;
            return true;
        }

        private int FindAdjacentVisibleTab(int startTab, int direction)
        {
            int step = Math.Sign(direction);
            if (step == 0)
                return startTab;

            int maxTab = _useDualTabStrip ? MAX_TAB_INDEX : TAB_4TH;
            for (int tab = startTab + step; tab >= TAB_BEGINNER && tab <= maxTab; tab += step)
            {
                if (_tabVisible[tab])
                    return tab;
            }

            for (int tab = step > 0 ? TAB_BEGINNER : maxTab;
                 tab >= TAB_BEGINNER && tab <= maxTab;
                 tab += step)
            {
                if (_tabVisible[tab])
                    return tab;
            }

            return startTab;
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
            _hoveredSpUpSkillIndex = GetSpUpSkillIndexAtPosition(mouseState.X, mouseState.Y);
            _hoveredSkillPointDisplay = GetSkillPointBounds().Contains(mouseState.X, mouseState.Y);

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

                int tabCount = _useDualTabStrip ? DUAL_TAB_COUNT : STANDARD_TAB_COUNT;
                for (int i = 0; i < tabCount; i++)
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

                int clickedSpUpSkillIndex = GetSpUpSkillIndexAtPosition(mouseState.X, mouseState.Y);
                if (clickedSpUpSkillIndex >= 0)
                {
                    _pressedSpUpSkillIndex = clickedSpUpSkillIndex;
                    SelectSkillIndex(clickedSpUpSkillIndex, true);
                    TryHandleSkillLevelUp(clickedSpUpSkillIndex);
                    _lastClickedSkillIndex = -1;
                    _lastClickTime = 0;
                    _previousMouseState = mouseState;
                    _previousKeyboardState = Keyboard.GetState();
                    return;
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
                int spUpSkillIndex = GetSpUpSkillIndexAtPosition(mouseState.X, mouseState.Y);
                var selectedSkill = spUpSkillIndex >= 0 ? null : GetSkillAtPosition(mouseState.X, mouseState.Y);
                if (selectedSkill != null)
                {
                    SelectSkillIndex(GetSkillIndexAtPosition(mouseState.X, mouseState.Y), false);
                    OnSkillSelected?.Invoke(selectedSkill);
                    OnSkillInvoked?.Invoke(selectedSkill.SkillId);
                }
            }

            if (mouseState.LeftButton == ButtonState.Released)
            {
                _pressedSpUpSkillIndex = -1;
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
            TryHandleTabKeyboardNavigation(keyboardState);
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
