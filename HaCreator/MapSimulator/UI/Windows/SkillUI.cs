using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaCreator.MapSimulator.Loaders;
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
    /// Skill UI window displaying skills organized by job advancement
    /// Structure: UI.wz/UIWindow.img/Skill/
    /// Supports drag-and-drop skill assignment to QuickSlotUI
    /// </summary>
    public class SkillUI : UIWindowBase
    {
        #region Constants
        // From binary analysis of CUISkill::Draw at 0x84ed90 (post-Big Bang)
        // The skill window uses a list layout with one skill per row
        private const int SKILL_ICON_SIZE = 32;

        // Skill row layout constants (from decompiled CUISkill::Draw)
        private const int SKILL_ROW_HEIGHT = 40;      // nTop increments by 40
        private const int FIRST_ROW_TOP = 112;        // First row Y position (0x70)
        private const int LAST_ROW_TOP = 272;         // Last row Y position
        private const int VISIBLE_ROWS = 4;           // 4 skills visible at once

        // Skill icon position (relative to window position)
        private const int ICON_X = 12;                // Fixed X for all icons
        private const int ICON_Y_OFFSET = -17;        // Y offset from row top

        // Skill name text position
        private const int NAME_X = 50;                // X position for skill name
        private const int NAME_Y_OFFSET = -18;        // Y offset from row top

        // Skill level text position
        private const int LEVEL_X = 50;               // X position for level text
        private const int LEVEL_Y_OFFSET = 0;         // At row baseline

        // Bonus level text position (from items/buffs)
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
        private const int SCROLLBAR_X = 153;
        private const int SCROLLBAR_Y = 93;
        private const int SCROLLBAR_WIDTH = 12;
        private const int SCROLLBAR_HEIGHT = 155;
        private const int SCROLLBAR_BUTTON_HEIGHT = 12;

        // SP display position
        private const int SP_DISPLAY_X_BASE = 104;    // Right-aligned from this position
        private const int SP_DISPLAY_Y = 256;         // 0x100
        private const float SP_DISPLAY_TEXT_SCALE = 0.4f;
        private const int TOOLTIP_FALLBACK_WIDTH = 320;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int TOOLTIP_ANCHOR_GAP = 8;
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

        // Job book icon position
        private const int BOOK_ICON_X = 15;
        private const int BOOK_ICON_Y = 55;
        private const int JOB_ICON_SIZE = 32;
        private const int BOOK_NAME_CENTER_X = 104;
        private const int BOOK_NAME_SINGLE_LINE_Y = 65;
        private const int BOOK_NAME_MULTI_LINE_FIRST_Y = 55;
        private const int BOOK_NAME_MULTI_LINE_SECOND_Y = 69;
        private const int BOOK_NAME_MAX_WIDTH = 110;
        private const float BOOK_NAME_TEXT_SCALE = 0.42f;
        private const int SKILL_NAME_MAX_WIDTH = 95;
        private const int SKILL_LEVEL_MAX_WIDTH = 30;
        private const float SKILL_NAME_TEXT_SCALE = 0.38f;
        private const float SKILL_LEVEL_TEXT_SCALE = 0.34f;

        // Hit detection constants (from CUISkill::GetSkillIndexFromPoint at 0x84b390)
        private const int ICON_HIT_LEFT = 13;
        private const int ICON_HIT_TOP_OFFSET = -31;  // Relative to row top
        private const int ICON_HIT_RIGHT = 45;
        private const int ICON_HIT_BOTTOM_OFFSET = 1; // Relative to row top

        private const int ROW_HIT_LEFT = 10;
        private const int ROW_HIT_TOP_OFFSET = -34;   // Relative to row top
        private const int ROW_HIT_RIGHT = 149;
        private const int ROW_HIT_BOTTOM_OFFSET = 0;  // At row top

        // Legacy grid-based layout (deprecated, kept for reference)
        private const int SKILL_PADDING = 8;
        private const int SKILLS_PER_ROW = 1;         // Changed from 4 - client uses list, not grid

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
        private int _hoveredSpUpSkillIndex = -1;
        private bool _hoveredSkillPointDisplay;
        private int _pressedSpUpSkillIndex = -1;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private int _characterLevel = 1;

        // Skill description area
        private Rectangle _descriptionRect;
        private SpriteFont _font;
        private Character.Skills.SkillManager _skillManager;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Point[] _tooltipFrameOrigins = new Point[3];
        private Texture2D _debugPlaceholder;
        private int _hoveredSkillIndex = -1;
        private Point _lastMousePosition;
        private readonly Dictionary<int, Texture2D> _jobIconsByTab;
        private readonly Dictionary<int, string> _jobNamesByTab;
        private readonly Dictionary<int, int> _displaySkillRootByTab;
        private readonly Dictionary<int, List<SkillDataLoader.RecommendedSkillEntry>> _recommendedSkillsByTab;
        private Texture2D _skillRow0;
        private Texture2D _skillRow1;
        private Texture2D _recommendTexture;
        private Texture2D _skillRowLine;
        private Texture2D _spUpNormal;
        private Texture2D _spUpPressed;
        private Texture2D _spUpDisabled;
        private Texture2D _spUpMouseOver;
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
        private bool _isDraggingScrollThumb;
        private int _scrollThumbDragOffsetY;

        // Drag and drop
        private bool _isDragging = false;
        private int _dragSkillId = 0;
        private SkillDisplayData _dragSkill = null;
        private Vector2 _dragPosition;
        private int _dragSourceIndex = -1;
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

        /// <summary>
        /// Whether a skill is currently being dragged from this window
        /// </summary>
        public bool IsDraggingSkill => _isDragging;

        /// <summary>
        /// The skill ID being dragged (0 if not dragging)
        /// </summary>
        public int DraggedSkillId => _isDragging ? _dragSkillId : 0;

        /// <summary>
        /// Current drag position (for QuickSlotUI to render)
        /// </summary>
        public Vector2 DragPosition => _dragPosition;

        /// <summary>
        /// The skill data being dragged
        /// </summary>
        public SkillDisplayData DraggedSkill => _isDragging ? _dragSkill : null;

        /// <summary>
        /// Callback when drag starts
        /// </summary>
        public Action<int, SkillDisplayData> OnDragStart;

        /// <summary>
        /// Callback when drag ends (outside of QuickSlotUI)
        /// </summary>
        public Action OnDragEnd;
        public Action<int> OnSkillInvoked { get; set; }
        public Action<SkillDisplayData> OnSkillSelected { get; set; }
        public Func<SkillDisplayData, bool> OnSkillLevelUpRequested { get; set; }
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
            _debugPlaceholder = new Texture2D(device, 1, 1);
            _debugPlaceholder.SetData(new[] { Color.White });

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
            _displaySkillRootByTab = new Dictionary<int, int>
            {
                { TAB_BEGINNER, 0 },
                { TAB_1ST, 0 },
                { TAB_2ND, 0 },
                { TAB_3RD, 0 },
                { TAB_4TH, 0 }
            };
            _recommendedSkillsByTab = new Dictionary<int, List<SkillDataLoader.RecommendedSkillEntry>>
            {
                { TAB_BEGINNER, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { TAB_1ST, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { TAB_2ND, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { TAB_3RD, new List<SkillDataLoader.RecommendedSkillEntry>() },
                { TAB_4TH, new List<SkillDataLoader.RecommendedSkillEntry>() }
            };
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
        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetSkillManager(Character.Skills.SkillManager skillManager)
        {
            _skillManager = skillManager;
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
        /// Uses layout constants from CUISkill::Draw (0x84ed90)
        /// </summary>
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (!skillsByTab.TryGetValue(_currentTab, out var skills))
                return;

            int windowX = Position.X;
            int windowY = Position.Y;

            DrawJobHeader(sprite, windowX, windowY);

            // Draw visible skill rows using official client layout
            int rowIndex = 0;
            for (int nTop = FIRST_ROW_TOP; nTop < 287 && (rowIndex + _scrollOffset) < skills.Count; nTop += SKILL_ROW_HEIGHT)
            {
                int skillIndex = rowIndex + _scrollOffset;
                var skill = skills[skillIndex];
                bool isUnlearned = skill.CurrentLevel <= 0;

                // Calculate icon position: (12, nTop - 17)
                int iconX = windowX + ICON_X;
                int iconY = windowY + nTop + ICON_Y_OFFSET;

                // Draw skill icon
                Texture2D iconTexture = skill.GetIconForState(isUnlearned, skillIndex == _hoveredSkillIndex);
                if (iconTexture != null)
                {
                    Rectangle iconRect = new Rectangle(iconX, iconY, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
                    sprite.Draw(iconTexture, iconRect, Color.White);

                    if (TryGetCooldownVisualState(skill.SkillId, TickCount, out float remainingProgress, out string remainingText))
                    {
                        DrawCooldownOverlay(sprite, iconRect, remainingProgress);

                        if (_font != null && !string.IsNullOrWhiteSpace(remainingText))
                        {
                            Vector2 textSize = _font.MeasureString(remainingText) * COOLDOWN_TEXT_SCALE;
                            Vector2 textPosition = new Vector2(
                                iconRect.Right - textSize.X - 2f,
                                iconRect.Bottom - textSize.Y - 1f);
                            DrawTooltipText(sprite, remainingText, textPosition, Color.White, COOLDOWN_TEXT_SCALE);
                        }
                    }
                }
                else if (_emptySlotTexture != null)
                {
                    sprite.Draw(_emptySlotTexture,
                        new Rectangle(iconX, iconY, SKILL_ICON_SIZE, SKILL_ICON_SIZE),
                        Color.White);
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

                // Highlight selected skill
                if (skillIndex == _selectedSkillIndex)
                {
                    // Draw selection highlight around icon
                    DrawSelectionHighlight(sprite, iconX - 2, iconY - 2, SKILL_ICON_SIZE + 4, SKILL_ICON_SIZE + 4);
                }

                rowIndex++;
            }

            DrawSkillPointCount(sprite, windowX, windowY);
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawHoveredSkillTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        /// <summary>
        /// Draw the job header row with job icon and name.
        /// </summary>
        private void DrawJobHeader(SpriteBatch sprite, int windowX, int windowY)
        {
            Texture2D jobIcon = null;
            string jobName = "Beginner";

            if (_jobIconsByTab.TryGetValue(_currentTab, out Texture2D icon))
                jobIcon = icon;
            if (_jobNamesByTab.TryGetValue(_currentTab, out string name))
                jobName = name;

            if (jobIcon == null)
                _jobIconsByTab.TryGetValue(TAB_BEGINNER, out jobIcon);

            if (jobIcon != null)
            {
                sprite.Draw(
                    jobIcon,
                    new Rectangle(windowX + BOOK_ICON_X, windowY + BOOK_ICON_Y, JOB_ICON_SIZE, JOB_ICON_SIZE),
                    Color.White);
            }

            DrawBookName(sprite, windowX, windowY, jobName);
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

        /// <summary>
        /// Draw a selection highlight rectangle
        /// </summary>
        private void DrawSelectionHighlight(SpriteBatch sprite, int x, int y, int width, int height)
        {
            // Simple highlight using the empty slot texture as a reference
            // In production, this would use a proper highlight texture
            if (_emptySlotTexture != null)
            {
                Color highlightColor = new Color(255, 200, 100, 100);
                sprite.Draw(_emptySlotTexture,
                    new Rectangle(x, y, width, height),
                    highlightColor);
            }
        }

        /// <summary>
        /// Get the screen position for a skill icon at a given row index
        /// Based on CUISkill::Draw layout constants
        /// </summary>
        public Point GetSkillIconPosition(int visibleRowIndex)
        {
            int nTop = FIRST_ROW_TOP + (visibleRowIndex * SKILL_ROW_HEIGHT);
            return new Point(
                Position.X + ICON_X,
                Position.Y + nTop + ICON_Y_OFFSET
            );
        }

        /// <summary>
        /// Get the screen position for skill name text at a given row index
        /// </summary>
        public Point GetSkillNamePosition(int visibleRowIndex)
        {
            int nTop = FIRST_ROW_TOP + (visibleRowIndex * SKILL_ROW_HEIGHT);
            return new Point(
                Position.X + NAME_X,
                Position.Y + nTop + NAME_Y_OFFSET
            );
        }

        /// <summary>
        /// Get the screen position for skill level text at a given row index
        /// </summary>
        public Point GetSkillLevelPosition(int visibleRowIndex)
        {
            int nTop = FIRST_ROW_TOP + (visibleRowIndex * SKILL_ROW_HEIGHT);
            return new Point(
                Position.X + LEVEL_X,
                Position.Y + nTop + LEVEL_Y_OFFSET
            );
        }
        #endregion

        #region Tooltip
        private void DrawHoveredSkillTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null || _isDragging || _hoveredSkillIndex < 0)
                return;

            if (!skillsByTab.TryGetValue(_currentTab, out var skills) || _hoveredSkillIndex >= skills.Count)
                return;

            SkillDisplayData skill = skills[_hoveredSkillIndex];
            if (skill == null)
                return;

            int currentLevel = Math.Clamp(skill.CurrentLevel, 0, Math.Max(0, skill.MaxLevel));
            int previewLevel = currentLevel > 0 ? currentLevel : 1;
            int nextLevel = Math.Min(skill.MaxLevel, previewLevel + (currentLevel > 0 ? 1 : 0));
            string title = SanitizeTooltipText(skill.SkillName);
            string description = SanitizeTooltipText(skill.FormattedDescriptionOrDefault);
            string currentLevelHeader = currentLevel > 0 ? $"Current Level: {currentLevel}" : string.Empty;
            string currentLevelDescription = currentLevel > 0 ? SanitizeTooltipText(skill.GetFormattedLevelDescription(currentLevel)) : string.Empty;
            bool showNextLevel = nextLevel > 0 && nextLevel <= skill.MaxLevel && nextLevel != currentLevel;
            string nextLevelHeader = showNextLevel ? $"Next Level: {nextLevel}" : string.Empty;
            string nextLevelDescription = showNextLevel ? SanitizeTooltipText(skill.GetFormattedLevelDescription(nextLevel)) : string.Empty;

            int tooltipWidth = ResolveHoveredTooltipWidth();
            float titleWidth = tooltipWidth - CLIENT_TOOLTIP_TITLE_X - CLIENT_TOOLTIP_RIGHT_PADDING;
            float sectionWidth = tooltipWidth - CLIENT_TOOLTIP_TEXT_X - CLIENT_TOOLTIP_RIGHT_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            TooltipLine[] wrappedDescription = WrapTooltipText(description, sectionWidth, Color.White);
            string[] wrappedCurrentHeader = WrapTooltipText(currentLevelHeader, sectionWidth);
            TooltipLine[] wrappedCurrentDescription = WrapTooltipText(currentLevelDescription, sectionWidth, new Color(180, 255, 210));
            string[] wrappedNextHeader = WrapTooltipText(nextLevelHeader, sectionWidth);
            TooltipLine[] wrappedNextDescription = WrapTooltipText(nextLevelDescription, sectionWidth, new Color(255, 238, 196));

            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float currentHeaderHeight = MeasureLinesHeight(wrappedCurrentHeader);
            float currentDescriptionHeight = MeasureLinesHeight(wrappedCurrentDescription);
            float nextHeaderHeight = MeasureLinesHeight(wrappedNextHeader);
            float nextDescriptionHeight = MeasureLinesHeight(wrappedNextDescription);
            int requirementSectionHeight = GetRequirementSectionHeight(skill);
            float sectionHeight = CalculateTooltipSectionHeight(
                descriptionHeight,
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
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

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

        private bool TryGetCooldownVisualState(int skillId, int currentTime, out float remainingProgress, out string remainingText)
        {
            remainingProgress = 0f;
            remainingText = string.Empty;

            if (_skillManager == null || !_skillManager.IsOnCooldown(skillId, currentTime))
                return false;

            int remainingMs = Math.Max(0, _skillManager.GetCooldownRemaining(skillId, currentTime));
            if (remainingMs <= 0)
                return false;

            int durationMs = Math.Max(remainingMs, _skillManager.GetCooldownDuration(skillId, currentTime));
            if (durationMs <= 0)
                return false;

            remainingProgress = Math.Clamp(remainingMs / (float)durationMs, 0f, 1f);
            remainingText = Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f)).ToString();
            return true;
        }

        private void DrawCooldownOverlay(SpriteBatch sprite, Rectangle iconRect, float remainingProgress)
        {
            if (_debugPlaceholder == null)
                return;

            int overlayHeight = Math.Clamp((int)Math.Round(iconRect.Height * remainingProgress), 0, iconRect.Height);
            if (overlayHeight <= 0)
                return;

            Rectangle overlayRect = new Rectangle(iconRect.X, iconRect.Bottom - overlayHeight, iconRect.Width, overlayHeight);
            sprite.Draw(_debugPlaceholder, overlayRect, new Color(0, 0, 0, 150));
        }

        private static float CalculateTooltipSectionHeight(
            float descriptionHeight,
            float currentHeaderHeight,
            float currentDescriptionHeight,
            float nextHeaderHeight,
            float nextDescriptionHeight,
            float requirementSectionHeight)
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
                skill.Requirements.Count == 1 ? "Required Skill" : "Required Skills",
                new Vector2(tooltipX + CLIENT_TOOLTIP_REQUIREMENT_HEADER_X, sectionY + CLIENT_TOOLTIP_REQUIREMENT_HEADER_Y_OFFSET),
                new Color(255, 204, 120));

            for (int i = 0; i < skill.Requirements.Count; i++)
            {
                SkillRequirementDisplayData requirement = skill.Requirements[i];
                float rowY = sectionY + CLIENT_TOOLTIP_REQUIREMENT_FIRST_ROW_Y + (i * CLIENT_TOOLTIP_REQUIREMENT_ROW_HEIGHT);

                if (requirement.IconTexture != null)
                {
                    sprite.Draw(
                        requirement.IconTexture,
                        new Rectangle(
                            tooltipX + CLIENT_TOOLTIP_REQUIREMENT_ICON_X,
                            (int)Math.Round(rowY),
                            CLIENT_TOOLTIP_REQUIREMENT_ICON_SIZE,
                            CLIENT_TOOLTIP_REQUIREMENT_ICON_SIZE),
                        Color.White);
                }

                DrawTooltipText(
                    sprite,
                    SanitizeFontText(requirement.SkillName),
                    new Vector2(tooltipX + CLIENT_TOOLTIP_REQUIREMENT_NAME_X, rowY + CLIENT_TOOLTIP_REQUIREMENT_NAME_Y),
                    Color.White);
                DrawTooltipText(
                    sprite,
                    $"Level {Math.Max(1, requirement.RequiredLevel)}+",
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

                maxWidth = Math.Max(maxWidth, _font.MeasureString(line).X);
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

        private void DrawTooltipLines(SpriteBatch sprite, string[] lines, int x, float y, Color color)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * _font.LineSpacing)), color);
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

                    Vector2 position = new Vector2(drawX, y + (i * _font.LineSpacing));
                    DrawTooltipText(sprite, run.Text, position, run.Color);
                    drawX += _font.MeasureString(run.Text).X;
                }
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale = 1f)
        {
            if (string.IsNullOrWhiteSpace(text) || _font == null)
                return;

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (_font == null || lines == null || lines.Length == 0)
                return 0f;

            return lines.Length * _font.LineSpacing;
        }

        private float MeasureLinesHeight(TooltipLine[] lines)
        {
            if (_font == null || lines == null || lines.Length == 0)
                return 0f;

            return lines.Length * _font.LineSpacing;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            List<string> lines = new List<string>();
            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            for (int paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
            {
                string paragraph = paragraphs[paragraphIndex];
                string trimmed = paragraph.Trim();
                if (trimmed.Length == 0)
                {
                    if (lines.Count > 0 && !string.IsNullOrEmpty(lines[^1]))
                        lines.Add(string.Empty);
                    continue;
                }

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

                if (!string.IsNullOrWhiteSpace(currentLine))
                    lines.Add(currentLine);

                if (paragraphIndex < paragraphs.Length - 1 && lines.Count > 0 && !string.IsNullOrEmpty(lines[^1]))
                    lines.Add(string.Empty);
            }

            while (lines.Count > 0 && string.IsNullOrEmpty(lines[^1]))
                lines.RemoveAt(lines.Count - 1);

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

            return _font.MeasureString(text).X;
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
                        tokenText = Regex.Replace(tokenText, "\\s+", " ");

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

        private static string SanitizeTooltipText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? string.Empty
                : text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private Vector2 MeasureSkillBookText(string text, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Vector2.Zero;

            return _font.MeasureString(text) * scale;
        }

        private void DrawSkillBookText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return;

            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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

        private bool TryResolveBookNameSplit(string jobName, out string firstLine, out string secondLine)
        {
            firstLine = string.Empty;
            secondLine = string.Empty;

            if (string.IsNullOrWhiteSpace(jobName))
                return false;

            int splitIndex = jobName.IndexOf(' ', Math.Min(10, Math.Max(0, jobName.Length - 1)));
            if (splitIndex <= 0 || splitIndex >= jobName.Length - 1)
                splitIndex = jobName.LastIndexOf(' ');

            if (splitIndex <= 0 || splitIndex >= jobName.Length - 1)
                return false;

            while (splitIndex > 0 && splitIndex < jobName.Length - 1)
            {
                string candidateSecondLine = jobName[(splitIndex + 1)..];
                if (MeasureSkillBookText(candidateSecondLine, BOOK_NAME_TEXT_SCALE).X < BOOK_NAME_MAX_WIDTH)
                    break;

                int nextSplit = jobName.IndexOf(' ', splitIndex + 1);
                if (nextSplit <= 0 || nextSplit >= jobName.Length - 1)
                    return false;

                splitIndex = nextSplit;
            }

            firstLine = jobName[..splitIndex].Trim();
            secondLine = jobName[(splitIndex + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(firstLine) && !string.IsNullOrWhiteSpace(secondLine);
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

        public void AddSkill(int jobAdvancement, SkillDisplayData skillData)
        {
            if (skillData == null)
                return;

            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);
            if (skillsByTab.TryGetValue(tab, out var skills))
            {
                skills.Add(skillData);
            }
        }

        public void SetJobInfo(int tab, Texture2D jobIcon, string jobName)
        {
            int tabIndex = Math.Clamp(tab, TAB_BEGINNER, TAB_4TH);
            _jobIconsByTab[tabIndex] = jobIcon;
            if (!string.IsNullOrWhiteSpace(jobName))
            {
                _jobNamesByTab[tabIndex] = jobName;
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

        public void AddSkillPoints(int jobAdvancement, int delta)
        {
            if (delta == 0)
            {
                return;
            }

            int tab = Math.Clamp(jobAdvancement, TAB_BEGINNER, TAB_4TH);
            skillPoints.TryGetValue(tab, out int currentPoints);
            skillPoints[tab] = Math.Max(0, currentPoints + delta);
        }

        public int GetCurrentSkillPoints()
        {
            if (skillPoints.TryGetValue(_currentTab, out int points))
                return points;

            return 0;
        }

        public void UpdateSkillLevel(int skillId, int currentLevel, int maxLevel)
        {
            foreach (var tabSkills in skillsByTab.Values)
            {
                foreach (SkillDisplayData skill in tabSkills)
                {
                    if (skill.SkillId != skillId)
                        continue;

                    skill.CurrentLevel = Math.Max(0, currentLevel);
                    skill.MaxLevel = Math.Max(skill.CurrentLevel, maxLevel);
                }
            }
        }

        public void RecalculateSkillPointsFromCurrentLevels()
        {
            foreach (var entry in skillsByTab)
            {
                int remainingPoints = 0;
                List<SkillDisplayData> skills = entry.Value;
                for (int i = 0; i < skills.Count; i++)
                {
                    SkillDisplayData skill = skills[i];
                    if (skill == null)
                        continue;

                    remainingPoints += Math.Max(0, skill.MaxLevel - skill.CurrentLevel);
                }

                skillPoints[entry.Key] = remainingPoints;
            }
        }

        /// <summary>
        /// Clear all skills
        /// </summary>
        public void ClearSkills()
        {
            foreach (var kvp in skillsByTab)
            {
                kvp.Value.Clear();
                skillPoints[kvp.Key] = 0;
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
                // Client uses list layout (1 skill per row), not grid
                int maxScroll = Math.Max(0, skills.Count - VISIBLE_ROWS);
                if (_scrollOffset < maxScroll)
                    _scrollOffset++;
            }
        }
        #endregion

        #region Drag and Drop
        /// <summary>
        /// Get skill index at a mouse position within the skill list
        /// Uses hit detection logic from CUISkill::GetSkillIndexFromPoint (0x84b390)
        /// </summary>
        /// <param name="mouseX">Mouse X in screen coordinates</param>
        /// <param name="mouseY">Mouse Y in screen coordinates</param>
        /// <param name="hitIcon">Output: true if hit was on the icon specifically</param>
        public int GetSkillIndexAtPosition(int mouseX, int mouseY, out bool hitIcon)
        {
            hitIcon = false;

            if (!skillsByTab.TryGetValue(_currentTab, out var skills))
                return -1;

            // Convert to window-relative coordinates
            int relX = mouseX - Position.X;
            int relY = mouseY - Position.Y;

            // Iterate through visible rows using official client logic
            // Starting at nTop = 112, increment by 40, break when nTop >= 287
            int rowIndex = 0;
            for (int nTop = FIRST_ROW_TOP; nTop < 287; nTop += SKILL_ROW_HEIGHT)
            {
                int skillIndex = rowIndex + _scrollOffset;
                if (skillIndex >= skills.Count)
                    break;

                // Check icon hit area first (more specific)
                // rc.left = 13; rc.top = nTop-31; rc.right = 45; rc.bottom = nTop+1;
                if (relX >= ICON_HIT_LEFT &&
                    relX <= ICON_HIT_RIGHT &&
                    relY >= nTop + ICON_HIT_TOP_OFFSET &&
                    relY <= nTop + ICON_HIT_BOTTOM_OFFSET)
                {
                    hitIcon = true;
                    return skillIndex;
                }

                // Check full row hit area
                // rc.left = 10; rc.top = nTop-34; rc.right = 149; rc.bottom = nTop;
                if (relX >= ROW_HIT_LEFT &&
                    relX <= ROW_HIT_RIGHT &&
                    relY >= nTop + ROW_HIT_TOP_OFFSET &&
                    relY <= nTop + ROW_HIT_BOTTOM_OFFSET)
                {
                    return skillIndex;
                }

                rowIndex++;
            }

            return -1;
        }

        /// <summary>
        /// Get skill index at a mouse position (simplified overload)
        /// </summary>
        public int GetSkillIndexAtPosition(int mouseX, int mouseY)
        {
            return GetSkillIndexAtPosition(mouseX, mouseY, out _);
        }

        /// <summary>
        /// Get skill at a position
        /// </summary>
        public SkillDisplayData GetSkillAtPosition(int mouseX, int mouseY)
        {
            int index = GetSkillIndexAtPosition(mouseX, mouseY);
            if (index < 0) return null;

            if (skillsByTab.TryGetValue(_currentTab, out var skills) && index < skills.Count)
                return skills[index];

            return null;
        }

        /// <summary>
        /// Handle mouse down for drag start
        /// </summary>
        public void OnSkillMouseDown(int mouseX, int mouseY)
        {
            var skill = GetSkillAtPosition(mouseX, mouseY);
            if (skill != null && skill.CurrentLevel > 0)
            {
                _isDragging = true;
                _dragSkillId = skill.SkillId;
                _dragSkill = skill;
                _dragSourceIndex = GetSkillIndexAtPosition(mouseX, mouseY);
                _dragPosition = new Vector2(mouseX, mouseY);

                OnDragStart?.Invoke(_dragSkillId, skill);
            }
        }

        /// <summary>
        /// Handle mouse move during drag
        /// </summary>
        public void OnSkillMouseMove(int mouseX, int mouseY)
        {
            if (_isDragging)
            {
                _dragPosition = new Vector2(mouseX, mouseY);
            }
        }

        /// <summary>
        /// Handle mouse up to end drag
        /// </summary>
        public void OnSkillMouseUp()
        {
            if (_isDragging)
            {
                _isDragging = false;
                _dragSkillId = 0;
                _dragSkill = null;
                _dragSourceIndex = -1;

                OnDragEnd?.Invoke();
            }
        }

        /// <summary>
        /// Cancel drag operation
        /// </summary>
        public void CancelDrag()
        {
            _isDragging = false;
            _dragSkillId = 0;
            _dragSkill = null;
            _dragSourceIndex = -1;
        }

        /// <summary>
        /// Draw dragged skill icon at current position
        /// </summary>
        public void DrawDraggedSkill(SpriteBatch sprite)
        {
            if (!_isDragging || _dragSkill?.IconTexture == null)
                return;

            sprite.Draw(_dragSkill.IconTexture,
                new Rectangle(
                    (int)_dragPosition.X - SKILL_ICON_SIZE / 2,
                    (int)_dragPosition.Y - SKILL_ICON_SIZE / 2,
                    SKILL_ICON_SIZE,
                    SKILL_ICON_SIZE),
                Color.White * 0.7f);
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateTabStates();

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            _hoveredSkillIndex = _isDragging ? -1 : GetSkillIndexAtPosition(mouseState.X, mouseState.Y);

            // Update drag position if dragging
            if (_isDragging)
            {
                _dragPosition = new Vector2(mouseState.X, mouseState.Y);
            }
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
        public Texture2D IconDisabledTexture { get; set; }
        public Texture2D IconMouseOverTexture { get; set; }
        public string SkillName { get; set; }
        public string Description { get; set; }
        public string FormattedDescription { get; set; }
        public int CurrentLevel { get; set; }
        public int BonusLevel { get; set; }
        public int MaxLevel { get; set; }
        public int RequiredCharacterLevel { get; set; }
        public int RequiredSkillId { get; set; }
        public int RequiredSkillLevel { get; set; }
        public List<SkillRequirementDisplayData> Requirements { get; } = new List<SkillRequirementDisplayData>();
        public Dictionary<int, string> LevelDescriptions { get; } = new Dictionary<int, string>();
        public Dictionary<int, string> FormattedLevelDescriptions { get; } = new Dictionary<int, string>();
        public Dictionary<int, int> RequiredGuildLevels { get; } = new Dictionary<int, int>();
        public Dictionary<int, int> GuildActivationCosts { get; } = new Dictionary<int, int>();
        public Dictionary<int, int> GuildRenewalCosts { get; } = new Dictionary<int, int>();
        public Dictionary<int, int> GuildDurationsMinutes { get; } = new Dictionary<int, int>();
        public int GuildPriceUnit { get; set; } = 1;

        // Skill properties for tooltip
        public int Damage { get; set; }
        public int MPCost { get; set; }
        public int Cooldown { get; set; }
        public int Range { get; set; }
        public int MobCount { get; set; }

        /// <summary>
        /// Gets the appropriate icon texture based on skill state
        /// </summary>
        public Texture2D GetIconForState(bool isDisabled, bool isMouseOver)
        {
            if (isDisabled && IconDisabledTexture != null)
                return IconDisabledTexture;
            if (isMouseOver && IconMouseOverTexture != null)
                return IconMouseOverTexture;
            return IconTexture;
        }

        public string GetLevelDescription(int level)
        {
            if (LevelDescriptions.Count == 0)
                return string.Empty;

            int resolvedLevel = Math.Clamp(level, 1, Math.Max(1, MaxLevel));
            if (LevelDescriptions.TryGetValue(resolvedLevel, out string description))
                return description ?? string.Empty;

            if (LevelDescriptions.TryGetValue(1, out description))
                return description ?? string.Empty;

            foreach (var entry in LevelDescriptions)
                return entry.Value ?? string.Empty;

            return string.Empty;
        }

        public string FormattedDescriptionOrDefault =>
            !string.IsNullOrWhiteSpace(FormattedDescription)
                ? FormattedDescription
                : Description;

        public string GetFormattedLevelDescription(int level)
        {
            if (FormattedLevelDescriptions.Count == 0)
                return GetLevelDescription(level);

            int resolvedLevel = Math.Clamp(level, 1, Math.Max(1, MaxLevel));
            if (FormattedLevelDescriptions.TryGetValue(resolvedLevel, out string description))
                return description ?? string.Empty;

            if (FormattedLevelDescriptions.TryGetValue(1, out description))
                return description ?? string.Empty;

            foreach (var entry in FormattedLevelDescriptions)
                return entry.Value ?? string.Empty;

            return GetLevelDescription(level);
        }

        public int GetGuildActivationCost(int level)
        {
            return GetGuildLevelValue(GuildActivationCosts, level);
        }

        public int GetGuildRenewalCost(int level)
        {
            return GetGuildLevelValue(GuildRenewalCosts, level);
        }

        public int GetGuildDurationMinutes(int level)
        {
            return GetGuildLevelValue(GuildDurationsMinutes, level);
        }

        private int GetGuildLevelValue(Dictionary<int, int> valuesByLevel, int level)
        {
            if (valuesByLevel == null || valuesByLevel.Count == 0)
                return 0;

            int resolvedLevel = Math.Clamp(level, 1, Math.Max(1, MaxLevel));
            if (valuesByLevel.TryGetValue(resolvedLevel, out int value))
                return Math.Max(0, value);

            if (valuesByLevel.TryGetValue(1, out value))
                return Math.Max(0, value);

            foreach (KeyValuePair<int, int> pair in valuesByLevel)
                return Math.Max(0, pair.Value);

            return 0;
        }
    }

    public sealed class SkillRequirementDisplayData
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public int RequiredLevel { get; set; }
        public Texture2D IconTexture { get; set; }
    }

    internal sealed class TooltipLine
    {
        public List<TooltipTextRun> Runs { get; } = new List<TooltipTextRun>();
        public bool IsEmpty => Runs.Count == 0 || Runs.All(run => string.IsNullOrEmpty(run.Text));

        public void Append(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (Runs.Count > 0 && Runs[^1].Color == color)
            {
                TooltipTextRun previous = Runs[^1];
                Runs[^1] = new TooltipTextRun(previous.Text + text, color);
                return;
            }

            Runs.Add(new TooltipTextRun(text, color));
        }
    }

    internal readonly struct TooltipTextRun
    {
        public TooltipTextRun(string text, Color color)
        {
            Text = text;
            Color = color;
        }

        public string Text { get; }
        public Color Color { get; }
    }

    internal readonly struct TooltipToken
    {
        public TooltipToken(string text, Color color, bool isWhitespace, bool isNewLine)
        {
            Text = text;
            Color = color;
            IsWhitespace = isWhitespace;
            IsNewLine = isNewLine;
        }

        public string Text { get; }
        public Color Color { get; }
        public bool IsWhitespace { get; }
        public bool IsNewLine { get; }

        public static TooltipToken NewLine()
        {
            return new TooltipToken(string.Empty, Color.White, isWhitespace: false, isNewLine: true);
        }
    }
}
