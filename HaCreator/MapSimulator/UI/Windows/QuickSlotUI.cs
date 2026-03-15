using HaCreator.MapSimulator.Character.Skills;
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
    /// Quick Slot UI - Displays skill hotkey bar with 8 primary slots
    /// Structure matches MapleStory's QuickSlot panel
    /// Supports drag-and-drop skill assignment from SkillUI
    /// </summary>
    public class QuickSlotUI : UIWindowBase
    {
        #region Constants
        private const int SLOT_SIZE = 32;
        private const int SLOT_PADDING = 2;
        private const int SLOTS_PER_ROW = 8;
        private const int VISIBLE_ROWS = 1;  // Primary bar shows 1 row of 8 slots
        private const int TOOLTIP_FALLBACK_WIDTH = 320;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int TOOLTIP_OFFSET_X = 12;
        private const int TOOLTIP_OFFSET_Y = -4;
        private const float COOLDOWN_TEXT_SCALE = 0.7f;

        // Bar types for switching between different hotkey groups
        public const int BAR_PRIMARY = 0;    // Skill1-8 (slots 0-7)
        public const int BAR_FUNCTION = 1;   // F1-F12 (slots 8-19)
        public const int BAR_CTRL = 2;       // Ctrl+1-8 (slots 20-27)
        #endregion

        #region Fields
        private int _currentBar = BAR_PRIMARY;
        private SkillManager _skillManager;
        private SkillLoader _skillLoader;
        private SpriteFont _font;

        // Slot rendering
        private Texture2D _emptySlotTexture;
        private Texture2D _slotHighlightTexture;
        private Texture2D _cooldownOverlayTexture;
        private Texture2D[] _cooldownMaskTextures = Array.Empty<Texture2D>();
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private Texture2D _debugPlaceholder;

        // Hover and selection
        private int _hoveredSlot = -1;
        private int _selectedSlot = -1;
        private Point _lastMousePosition;

        // Drag and drop
        private int _dragSourceSlot = -1;
        private int _dragSkillId = 0;
        private bool _isDragging = false;
        private Vector2 _dragPosition;

        // Skill icon cache (IDXObject from SkillData.Icon)
        private readonly Dictionary<int, IDXObject> _skillIconCache = new();

        // Key labels for slots
        private static readonly string[][] BarKeyLabels = new[]
        {
            // Primary bar (Insert, Home, PageUp, Delete, End, PageDown, 1, 2)
            new[] { "Ins", "Home", "PgUp", "Del", "End", "PgDn", "1", "2" },
            // Function bar (F1-F12) - split into two rows of 6
            new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" },
            // Ctrl bar (Ctrl+1-8)
            new[] { "^1", "^2", "^3", "^4", "^5", "^6", "^7", "^8" }
        };
        #endregion

        #region Properties
        public override string WindowName => "QuickSlot";
        public bool IsDraggingSlot => _isDragging;

        public int CurrentBar
        {
            get => _currentBar;
            set
            {
                if (value >= BAR_PRIMARY && value <= BAR_CTRL)
                {
                    _currentBar = value;
                    _selectedSlot = -1;
                }
            }
        }

        /// <summary>
        /// Gets the number of slots in the current bar
        /// </summary>
        public int SlotCount => _currentBar switch
        {
            BAR_PRIMARY => SkillManager.PRIMARY_SLOT_COUNT,
            BAR_FUNCTION => SkillManager.FUNCTION_SLOT_COUNT,
            BAR_CTRL => SkillManager.CTRL_SLOT_COUNT,
            _ => 8
        };

        /// <summary>
        /// Gets the slot offset for the current bar
        /// </summary>
        public int SlotOffset => _currentBar switch
        {
            BAR_PRIMARY => 0,
            BAR_FUNCTION => SkillManager.FUNCTION_SLOT_OFFSET,
            BAR_CTRL => SkillManager.CTRL_SLOT_OFFSET,
            _ => 0
        };

        /// <summary>
        /// Callback when a skill is dropped onto a slot
        /// </summary>
        public Action<int, int> OnSkillDropped;

        /// <summary>
        /// Callback when a slot is right-clicked (to clear)
        /// </summary>
        public Action<int> OnSlotCleared;
        #endregion

        #region Constructor
        public QuickSlotUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            CreateSlotTextures(device);
        }

        private void CreateSlotTextures(GraphicsDevice device)
        {
            // Empty slot texture (dark gray with border)
            _emptySlotTexture = new Texture2D(device, SLOT_SIZE, SLOT_SIZE);
            Color[] slotData = new Color[SLOT_SIZE * SLOT_SIZE];
            Color slotColor = new Color(30, 30, 40, 200);
            Color borderColor = new Color(60, 60, 80, 255);

            for (int y = 0; y < SLOT_SIZE; y++)
            {
                for (int x = 0; x < SLOT_SIZE; x++)
                {
                    if (x == 0 || x == SLOT_SIZE - 1 || y == 0 || y == SLOT_SIZE - 1)
                        slotData[y * SLOT_SIZE + x] = borderColor;
                    else
                        slotData[y * SLOT_SIZE + x] = slotColor;
                }
            }
            _emptySlotTexture.SetData(slotData);

            // Highlight texture (yellow tint)
            _slotHighlightTexture = new Texture2D(device, SLOT_SIZE, SLOT_SIZE);
            Color[] highlightData = new Color[SLOT_SIZE * SLOT_SIZE];
            Color highlightColor = new Color(255, 255, 100, 80);

            for (int i = 0; i < highlightData.Length; i++)
                highlightData[i] = highlightColor;
            _slotHighlightTexture.SetData(highlightData);

            // Cooldown overlay (semi-transparent dark)
            _cooldownOverlayTexture = new Texture2D(device, SLOT_SIZE, SLOT_SIZE);
            Color[] cooldownData = new Color[SLOT_SIZE * SLOT_SIZE];
            Color cooldownColor = new Color(0, 0, 0, 150);

            for (int i = 0; i < cooldownData.Length; i++)
                cooldownData[i] = cooldownColor;
            _cooldownOverlayTexture.SetData(cooldownData);

            _debugPlaceholder = new Texture2D(device, 1, 1);
            _debugPlaceholder.SetData(new[] { Color.White });
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the skill manager reference
        /// </summary>
        public void SetSkillManager(SkillManager skillManager)
        {
            _skillManager = skillManager;
        }

        /// <summary>
        /// Set the skill loader for loading skill icons
        /// </summary>
        public void SetSkillLoader(SkillLoader skillLoader)
        {
            _skillLoader = skillLoader;
        }

        /// <summary>
        /// Set the font for text rendering
        /// </summary>
        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetCooldownMasks(Texture2D[] cooldownMaskTextures)
        {
            _cooldownMaskTextures = cooldownMaskTextures ?? Array.Empty<Texture2D>();
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
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_skillManager == null)
                return;

            _skillManager.RevalidateHotkeys();

            int slotCount = SlotCount;
            int slotOffset = SlotOffset;
            int slotsPerRow = _currentBar == BAR_FUNCTION ? 6 : SLOTS_PER_ROW;
            int rows = (slotCount + slotsPerRow - 1) / slotsPerRow;

            // Calculate content area (offset from window position)
            int contentX = Position.X + 5;
            int contentY = Position.Y + 5;

            // Draw slots
            for (int i = 0; i < slotCount; i++)
            {
                int row = i / slotsPerRow;
                int col = i % slotsPerRow;
                int slotX = contentX + col * (SLOT_SIZE + SLOT_PADDING);
                int slotY = contentY + row * (SLOT_SIZE + SLOT_PADDING + 12); // Extra space for key labels

                int absoluteSlotIndex = slotOffset + i;

                // Draw slot background
                sprite.Draw(_emptySlotTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);

                // Draw skill icon if assigned
                int skillId = _skillManager.GetHotkeySkill(absoluteSlotIndex);
                if (skillId > 0)
                {
                    var icon = GetSkillIcon(skillId);
                    if (icon != null)
                    {
                        // Use DrawBackground for IDXObject (handles origin automatically)
                        icon.DrawBackground(sprite, null, null, slotX, slotY, Color.White, false, null);
                    }

                    // Draw cooldown overlay if on cooldown
                    if (TryGetCooldownVisualState(skillId, TickCount, out int cooldownFrameIndex, out string remainingText))
                    {
                        DrawCooldownMask(sprite, slotX, slotY, cooldownFrameIndex);

                        if (_font != null && !string.IsNullOrWhiteSpace(remainingText))
                        {
                            Vector2 textSize = _font.MeasureString(remainingText) * COOLDOWN_TEXT_SCALE;
                            Vector2 textPosition = new Vector2(
                                slotX + SLOT_SIZE - textSize.X - 2f,
                                slotY + SLOT_SIZE - textSize.Y - 1f);

                            DrawTextWithShadow(sprite, remainingText, textPosition, Color.White, Color.Black, COOLDOWN_TEXT_SCALE);
                        }
                    }
                }

                // Draw highlight on hovered slot
                if (i == _hoveredSlot)
                {
                    sprite.Draw(_slotHighlightTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
                }

                // Draw key label below slot
                if (_font != null && i < BarKeyLabels[_currentBar].Length)
                {
                    string label = BarKeyLabels[_currentBar][i];
                    Vector2 labelSize = _font.MeasureString(label);
                    int labelX = slotX + (SLOT_SIZE - (int)labelSize.X) / 2;
                    int labelY = slotY + SLOT_SIZE + 1;
                    sprite.DrawString(_font, label, new Vector2(labelX, labelY), Color.White);
                }
            }

            // Draw bar indicator / tab
            if (_font != null)
            {
                string barName = _currentBar switch
                {
                    BAR_PRIMARY => "Primary",
                    BAR_FUNCTION => "F1-F12",
                    BAR_CTRL => "Ctrl",
                    _ => ""
                };
                sprite.DrawString(_font, barName, new Vector2(Position.X + 5, Position.Y - 14), Color.Yellow);
            }

            // Draw dragged skill icon
            if (_isDragging && _dragSkillId > 0)
            {
                var dragIcon = GetSkillIcon(_dragSkillId);
                if (dragIcon != null)
                {
                    // Use DrawBackground with semi-transparency for drag visual
                    int dragX = (int)_dragPosition.X - SLOT_SIZE / 2;
                    int dragY = (int)_dragPosition.Y - SLOT_SIZE / 2;
                    dragIcon.DrawBackground(sprite, null, null, dragX, dragY, Color.White * 0.7f, false, null);
                }
            }
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            DrawHoveredSkillTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        private IDXObject GetSkillIcon(int skillId)
        {
            if (_skillIconCache.TryGetValue(skillId, out var cached))
                return cached;

            // Load skill icon from skill data
            var skill = _skillLoader?.LoadSkill(skillId);
            if (skill?.Icon != null)
            {
                _skillIconCache[skillId] = skill.Icon;
                return skill.Icon;
            }

            return null;
        }

        private void DrawTextWithShadow(SpriteBatch sprite, string text, Vector2 position, Color color, Color shadowColor, float scale = 1.0f)
        {
            sprite.DrawString(_font, text, position + Vector2.One, shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawHoveredSkillTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null || _isDragging || _hoveredSlot < 0)
                return;

            int skillId = _skillManager?.GetHotkeySkill(SlotOffset + _hoveredSlot) ?? 0;
            if (skillId <= 0)
                return;

            SkillData skill = _skillLoader?.LoadSkill(skillId);
            if (skill == null)
                return;

            int level = Math.Max(1, _skillManager.GetSkillLevel(skillId));
            SkillLevelData levelData = skill.GetLevel(level);
            string title = SanitizeFontText(skill.Name);
            string description = SanitizeFontText(skill.Description);
            string levelLine = $"Level: {level}";
            string costLine = levelData != null
                ? $"MP {levelData.MpCon}  Cooldown {Math.Max(0, levelData.Cooldown) / 1000f:0.#}s"
                : string.Empty;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SLOT_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            string[] wrappedLevel = WrapTooltipText(levelLine, sectionWidth);
            string[] wrappedCost = WrapTooltipText(costLine, sectionWidth);

            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float levelHeight = MeasureLinesHeight(wrappedLevel);
            float costHeight = MeasureLinesHeight(wrappedCost);
            float contentHeight = descriptionHeight;
            if (levelHeight > 0f)
                contentHeight += (contentHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + levelHeight;
            if (costHeight > 0f)
                contentHeight += (contentHeight > 0f ? 2f : 0f) + costHeight;

            float iconBlockHeight = Math.Max(SLOT_SIZE, contentHeight);
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
            IDXObject icon = GetSkillIcon(skillId);
            icon?.DrawBackground(sprite, null, null, iconX, contentY, Color.White, false, null);

            int textX = tooltipX + textLeftOffset;
            float sectionY = contentY;
            if (descriptionHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedDescription, textX, sectionY, Color.White);
                sectionY += descriptionHeight;
            }

            if (levelHeight > 0f)
            {
                sectionY += descriptionHeight > 0f ? TOOLTIP_SECTION_GAP : 0f;
                DrawTooltipLines(sprite, wrappedLevel, textX, sectionY, new Color(140, 200, 255));
                sectionY += levelHeight;
            }

            if (costHeight > 0f)
            {
                sectionY += 2f;
                DrawTooltipLines(sprite, wrappedCost, textX, sectionY, new Color(180, 255, 210));
            }
        }

        private bool TryGetCooldownVisualState(int skillId, int currentTime, out int frameIndex, out string remainingText)
        {
            frameIndex = 15;
            remainingText = string.Empty;

            if (_skillManager == null || !_skillManager.IsOnCooldown(skillId, currentTime))
            {
                return false;
            }

            int remainingMs = Math.Max(0, _skillManager.GetCooldownRemaining(skillId, currentTime));
            if (remainingMs <= 0)
            {
                return false;
            }

            SkillData skill = _skillLoader?.LoadSkill(skillId);
            int level = _skillManager.GetSkillLevel(skillId);
            int totalMs = Math.Max(0, skill?.GetLevel(level)?.Cooldown ?? 0);
            if (totalMs <= 0)
            {
                return false;
            }

            // The client advances the quick-slot CoolTime masks in 15 stepped states (0..14)
            // and uses frame 15 as the cleared surface when nothing is cooling down.
            int totalSeconds = Math.Max(1, (int)Math.Ceiling(totalMs / 1000f));
            int remainingSeconds = Math.Max(0, (int)Math.Ceiling(remainingMs / 1000f));
            int elapsedSeconds = Math.Clamp(totalSeconds - remainingSeconds, 0, totalSeconds);
            frameIndex = Math.Clamp((14 * elapsedSeconds) / totalSeconds, 0, 14);
            remainingText = Math.Max(1, (int)Math.Ceiling(remainingMs / 1000f)).ToString();
            return true;
        }

        private void DrawCooldownMask(SpriteBatch sprite, int slotX, int slotY, int frameIndex)
        {
            if (_cooldownMaskTextures.Length > 0)
            {
                frameIndex = Math.Clamp(frameIndex, 0, _cooldownMaskTextures.Length - 1);
                Texture2D maskTexture = _cooldownMaskTextures[frameIndex];
                if (maskTexture != null)
                {
                    sprite.Draw(maskTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
                    return;
                }
            }

            if (frameIndex >= 15)
            {
                return;
            }

            float remainingProgress = 1f - (frameIndex / 14f);
            int overlayHeight = (int)(SLOT_SIZE * remainingProgress);
            sprite.Draw(_cooldownOverlayTexture,
                new Rectangle(slotX, slotY + SLOT_SIZE - overlayHeight, SLOT_SIZE, overlayHeight),
                Color.White);
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

            sprite.Draw(_debugPlaceholder, rect, new Color(18, 18, 26, 235));
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            Color borderColor = new Color(214, 174, 82);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_debugPlaceholder, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
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

        private static string SanitizeFontText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("\r", string.Empty)
                .Replace("\n", " ")
                .Replace("\t", " ");
        }
        #endregion

        #region Mouse Input
        /// <summary>
        /// Get the slot index at a mouse position
        /// </summary>
        public int GetSlotAtPosition(int mouseX, int mouseY)
        {
            int contentX = Position.X + 5;
            int contentY = Position.Y + 5;
            int slotsPerRow = _currentBar == BAR_FUNCTION ? 6 : SLOTS_PER_ROW;

            int relX = mouseX - contentX;
            int relY = mouseY - contentY;

            if (relX < 0 || relY < 0)
                return -1;

            int rowHeight = SLOT_SIZE + SLOT_PADDING + 12;
            int col = relX / (SLOT_SIZE + SLOT_PADDING);
            int row = relY / rowHeight;

            // Check if within slot bounds (not in padding)
            int slotX = col * (SLOT_SIZE + SLOT_PADDING);
            int slotY = row * rowHeight;
            if (relX - slotX > SLOT_SIZE || relY - slotY > SLOT_SIZE)
                return -1;

            int index = row * slotsPerRow + col;
            if (index >= 0 && index < SlotCount)
                return index;

            return -1;
        }

        /// <summary>
        /// Handle mouse move for hover effects
        /// </summary>
        public void OnMouseMove(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);

            if (_isDragging)
            {
                _dragPosition = new Vector2(mouseX, mouseY);
            }
            _hoveredSlot = GetSlotAtPosition(mouseX, mouseY);
        }

        /// <summary>
        /// Handle mouse down for drag start
        /// </summary>
        public void OnMouseDown(int mouseX, int mouseY, bool leftButton, bool rightButton)
        {
            int slot = GetSlotAtPosition(mouseX, mouseY);
            if (slot < 0) return;

            int absoluteSlot = SlotOffset + slot;

            if (rightButton)
            {
                // Right-click to clear slot
                _skillManager?.ClearHotkey(absoluteSlot);
                OnSlotCleared?.Invoke(absoluteSlot);
            }
            else if (leftButton)
            {
                // Left-click to start drag
                int skillId = _skillManager?.GetHotkeySkill(absoluteSlot) ?? 0;
                if (skillId > 0)
                {
                    _isDragging = true;
                    _dragSourceSlot = absoluteSlot;
                    _dragSkillId = skillId;
                    _dragPosition = new Vector2(mouseX, mouseY);
                }
            }
        }

        /// <summary>
        /// Handle mouse up for drag end
        /// </summary>
        public void OnMouseUp(int mouseX, int mouseY)
        {
            if (_isDragging)
            {
                int targetSlot = GetSlotAtPosition(mouseX, mouseY);
                if (targetSlot >= 0)
                {
                    int absoluteTarget = SlotOffset + targetSlot;

                    // Swap skills if dragging within QuickSlot
                    if (_dragSourceSlot >= 0 && _dragSourceSlot != absoluteTarget)
                    {
                        int targetSkill = _skillManager?.GetHotkeySkill(absoluteTarget) ?? 0;
                        if (_skillManager?.TrySetHotkey(absoluteTarget, _dragSkillId) == true)
                        {
                            _skillManager.SetHotkey(_dragSourceSlot, targetSkill);
                        }
                    }
                }

                _isDragging = false;
                _dragSourceSlot = -1;
                _dragSkillId = 0;
            }
        }

        public void CancelDrag()
        {
            _isDragging = false;
            _dragSourceSlot = -1;
            _dragSkillId = 0;
        }

        /// <summary>
        /// Accept a skill drop from SkillUI
        /// </summary>
        public bool AcceptSkillDrop(int skillId, int mouseX, int mouseY)
        {
            int slot = GetSlotAtPosition(mouseX, mouseY);
            if (slot < 0) return false;

            int absoluteSlot = SlotOffset + slot;
            if (_skillManager?.TrySetHotkey(absoluteSlot, skillId) != true)
                return false;

            OnSkillDropped?.Invoke(absoluteSlot, skillId);
            return true;
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _skillManager?.RevalidateHotkeys();

            // Get mouse state for hover detection
            var mouseState = Mouse.GetState();
            OnMouseMove(mouseState.X, mouseState.Y);
        }
        #endregion

        #region Bar Switching
        /// <summary>
        /// Switch to next bar
        /// </summary>
        public void NextBar()
        {
            CurrentBar = (_currentBar + 1) % 3;
        }

        /// <summary>
        /// Switch to previous bar
        /// </summary>
        public void PreviousBar()
        {
            CurrentBar = (_currentBar + 2) % 3;
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Clear cached icons
        /// </summary>
        public void ClearIconCache()
        {
            _skillIconCache.Clear();
        }
        #endregion
    }
}
