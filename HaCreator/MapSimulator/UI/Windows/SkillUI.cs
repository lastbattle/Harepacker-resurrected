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
using System.Linq;

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
        // From binary analysis of CUISkill::Draw at 0x84ed90 (v115 post-Big Bang)
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

        // SP display position
        private const int SP_DISPLAY_X_BASE = 104;    // Right-aligned from this position
        private const int SP_DISPLAY_Y = 256;         // 0x100
        private const int TOOLTIP_FALLBACK_WIDTH = 320;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int TOOLTIP_OFFSET_X = 12;
        private const int TOOLTIP_OFFSET_Y = -4;
        private const float COOLDOWN_TEXT_SCALE = 0.55f;
        private static readonly Color TOOLTIP_BACKGROUND_COLOR = new Color(28, 28, 28, 228);
        private static readonly Color TOOLTIP_BORDER_COLOR = new Color(112, 112, 112, 235);

        // Job book icon position
        private const int BOOK_ICON_X = 15;
        private const int BOOK_ICON_Y = 55;

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

        // Skill description area
        private Rectangle _descriptionRect;
        private SpriteFont _font;
        private Character.Skills.SkillManager _skillManager;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private Texture2D _debugPlaceholder;
        private int _hoveredSkillIndex = -1;
        private Point _lastMousePosition;

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

            // Draw visible skill rows using official client layout
            int rowIndex = 0;
            for (int nTop = FIRST_ROW_TOP; nTop < 287 && (rowIndex + _scrollOffset) < skills.Count; nTop += SKILL_ROW_HEIGHT)
            {
                int skillIndex = rowIndex + _scrollOffset;
                var skill = skills[skillIndex];

                // Calculate icon position: (12, nTop - 17)
                int iconX = windowX + ICON_X;
                int iconY = windowY + nTop + ICON_Y_OFFSET;

                // Draw skill icon
                if (skill.IconTexture != null)
                {
                    Rectangle iconRect = new Rectangle(iconX, iconY, SKILL_ICON_SIZE, SKILL_ICON_SIZE);
                    sprite.Draw(skill.IconTexture, iconRect, Color.White);

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

                // Note: Text rendering would go here with proper font support
                // Name position: (50, nTop - 18)
                // Level position: (50, nTop)

                // Highlight selected skill
                if (skillIndex == _selectedSkillIndex)
                {
                    // Draw selection highlight around icon
                    DrawSelectionHighlight(sprite, iconX - 2, iconY - 2, SKILL_ICON_SIZE + 4, SKILL_ICON_SIZE + 4);
                }

                rowIndex++;
            }
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawHoveredSkillTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight, TickCount);
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
        private void DrawHoveredSkillTooltip(SpriteBatch sprite, int renderWidth, int renderHeight, int currentTime)
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
            string description = SanitizeTooltipText(skill.Description);
            string cooldownLine = GetCooldownTooltipText(skill, currentTime);
            string currentLevelHeader = currentLevel > 0 ? $"Current Level: {currentLevel}" : string.Empty;
            string currentLevelDescription = currentLevel > 0 ? SanitizeTooltipText(skill.GetLevelDescription(currentLevel)) : string.Empty;
            bool showNextLevel = nextLevel > 0 && nextLevel <= skill.MaxLevel && nextLevel != currentLevel;
            string nextLevelHeader = showNextLevel ? $"Next Level: {nextLevel}" : string.Empty;
            string nextLevelDescription = showNextLevel ? SanitizeTooltipText(skill.GetLevelDescription(nextLevel)) : string.Empty;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SKILL_ICON_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            string[] wrappedCooldown = WrapTooltipText(cooldownLine, sectionWidth);
            string[] wrappedCurrentHeader = WrapTooltipText(currentLevelHeader, sectionWidth);
            string[] wrappedCurrentDescription = WrapTooltipText(currentLevelDescription, sectionWidth);
            string[] wrappedNextHeader = WrapTooltipText(nextLevelHeader, sectionWidth);
            string[] wrappedNextDescription = WrapTooltipText(nextLevelDescription, sectionWidth);

            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float cooldownHeight = MeasureLinesHeight(wrappedCooldown);
            float currentHeaderHeight = MeasureLinesHeight(wrappedCurrentHeader);
            float currentDescriptionHeight = MeasureLinesHeight(wrappedCurrentDescription);
            float nextHeaderHeight = MeasureLinesHeight(wrappedNextHeader);
            float nextDescriptionHeight = MeasureLinesHeight(wrappedNextDescription);
            float sectionHeight = CalculateTooltipSectionHeight(
                descriptionHeight,
                cooldownHeight,
                currentHeaderHeight,
                currentDescriptionHeight,
                nextHeaderHeight,
                nextDescriptionHeight);
            float iconBlockHeight = Math.Max(SKILL_ICON_SIZE, sectionHeight);

            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_TITLE_GAP + iconBlockHeight);
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
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

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

            if (cooldownHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, wrappedCooldown, textX, sectionY, new Color(255, 238, 155));
                sectionY += cooldownHeight;
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

        private bool TryGetCooldownVisualState(int skillId, int currentTime, out float remainingProgress, out string remainingText)
        {
            remainingProgress = 0f;
            remainingText = string.Empty;

            if (_skillManager == null || !_skillManager.IsOnCooldown(skillId, currentTime))
                return false;

            int remainingMs = Math.Max(0, _skillManager.GetCooldownRemaining(skillId, currentTime));
            if (remainingMs <= 0)
                return false;

            SkillDisplayData skill = skillsByTab.TryGetValue(_currentTab, out var skills)
                ? skills.FirstOrDefault(entry => entry.SkillId == skillId)
                : null;
            int durationMs = Math.Max(remainingMs, skill?.Cooldown ?? 0);
            if (durationMs <= 0)
                return false;

            remainingProgress = Math.Clamp(remainingMs / (float)durationMs, 0f, 1f);
            remainingText = Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f)).ToString();
            return true;
        }

        private string GetCooldownTooltipText(SkillDisplayData skill, int currentTime)
        {
            if (skill == null)
                return string.Empty;

            if (_skillManager == null || !_skillManager.IsOnCooldown(skill.SkillId, currentTime))
                return skill.Cooldown > 0 ? $"Cooldown: {skill.Cooldown / 1000f:0.#} sec" : "Cooldown: Ready";

            int remainingMs = Math.Max(0, _skillManager.GetCooldownRemaining(skill.SkillId, currentTime));
            return remainingMs > 0
                ? $"Cooldown: {Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f))} sec"
                : "Cooldown: Ready";
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
            float cooldownHeight,
            float currentHeaderHeight,
            float currentDescriptionHeight,
            float nextHeaderHeight,
            float nextDescriptionHeight)
        {
            float height = 0f;
            if (descriptionHeight > 0f)
                height += descriptionHeight;
            if (cooldownHeight > 0f)
                height += (height > 0f ? TOOLTIP_SECTION_GAP : 0f) + cooldownHeight;
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

            List<string> lines = new List<string>();
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

                if (!string.IsNullOrWhiteSpace(currentLine))
                    lines.Add(currentLine);
            }

            return lines.ToArray();
        }

        private static string SanitizeTooltipText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Replace('\r', ' ').Replace('\n', ' ').Trim();
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
        public int CurrentLevel { get; set; }
        public int MaxLevel { get; set; }
        public int RequiredCharacterLevel { get; set; }
        public int RequiredSkillId { get; set; }
        public int RequiredSkillLevel { get; set; }
        public Dictionary<int, string> LevelDescriptions { get; } = new Dictionary<int, string>();

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
    }
}
