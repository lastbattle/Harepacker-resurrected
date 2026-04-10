using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
        private readonly GraphicsDevice _graphicsDevice;

        // Slot rendering
        private Texture2D _emptySlotTexture;
        private Texture2D _slotHighlightTexture;
        private Texture2D _cooldownOverlayTexture;
        private Texture2D[] _cooldownMaskTextures = Array.Empty<Texture2D>();
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Point[] _tooltipFrameOrigins = new Point[3];
        private EquipUIBigBang.EquipTooltipAssets _equipTooltipAssets;
        private Texture2D _debugPlaceholder;

        // Hover and selection
        private int _hoveredSlot = -1;
        private Point _lastMousePosition;

        // Drag and drop
        private int _dragSourceSlot = -1;
        private InventoryType _dragItemInventoryType = InventoryType.NONE;
        private int _dragItemId = 0;
        private int _dragMacroIndex = -1;
        private int _dragSkillId = 0;
        private DragBindingType _dragType = DragBindingType.None;
        private bool _isDragging = false;
        private Vector2 _dragPosition;
        private IInventoryRuntime _inventoryRuntime;
        private Func<int, SkillMacro> _macroProvider;

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
        private readonly Dictionary<int, string[]> _barKeyLabelOverrides = new();
        private readonly QuickSlotPresentationState[] _primaryQuickSlotPresentationStates = new QuickSlotPresentationState[SkillManager.PRIMARY_SLOT_COUNT];
        private bool _hasPrimaryQuickSlotPresentationSnapshot;
        private bool _lastPrimaryQuickSlotCompareResult = true;
        private int _lastQuickSlotValidationTime = int.MinValue;
        private int _lastQuickSlotValidationBar = -1;
        private RenderTarget2D _primaryQuickSlotSurface;
        private bool _primaryQuickSlotSurfaceDirty = true;

        private enum DragBindingType
        {
            None,
            Skill,
            Macro,
            Item
        }

        private enum QuickSlotPresentationBindingType
        {
            None,
            Skill,
            Macro,
            Item
        }

        private readonly struct QuickSlotPresentationState : IEquatable<QuickSlotPresentationState>
        {
            public QuickSlotPresentationState(
                QuickSlotPresentationBindingType bindingType,
                int id,
                int quantity,
                InventoryType inventoryType = InventoryType.NONE)
            {
                BindingType = bindingType;
                Id = id;
                Quantity = quantity;
                InventoryType = inventoryType;
            }

            public QuickSlotPresentationBindingType BindingType { get; }

            public int Id { get; }

            public int Quantity { get; }

            public InventoryType InventoryType { get; }

            public bool Equals(QuickSlotPresentationState other)
            {
                return BindingType == other.BindingType &&
                       Id == other.Id &&
                       Quantity == other.Quantity &&
                       InventoryType == other.InventoryType;
            }

            public override bool Equals(object obj)
            {
                return obj is QuickSlotPresentationState other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine((int)BindingType, Id, Quantity, InventoryType);
            }
        }
        #endregion

        #region Properties
        public override string WindowName => "QuickSlot";
        public bool IsDraggingSlot => _isDragging;
        internal bool LastPrimaryQuickSlotCompareResult => _lastPrimaryQuickSlotCompareResult;

        public int CurrentBar
        {
            get => _currentBar;
            set
            {
                if (value >= BAR_PRIMARY && value <= BAR_CTRL)
                {
                    _currentBar = value;
                    InvalidateQuickSlotValidationCache();
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
            _graphicsDevice = device;
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
            if (_skillManager != null)
            {
                _skillManager.HotkeysChanged -= InvalidateQuickSlotBindingValidationCache;
            }

            _skillManager = skillManager;
            if (_skillManager != null)
            {
                _skillManager.HotkeysChanged += InvalidateQuickSlotBindingValidationCache;
            }

            InvalidateQuickSlotValidationCache();
        }

        /// <summary>
        /// Set the skill loader for loading skill icons
        /// </summary>
        public void SetSkillLoader(SkillLoader skillLoader)
        {
            _skillLoader = skillLoader;
            InvalidateQuickSlotValidationCache();
        }

        public void SetInventoryRuntime(IInventoryRuntime inventoryRuntime)
        {
            _inventoryRuntime = inventoryRuntime;
            InvalidateQuickSlotValidationCache();
        }

        public void SetMacroProvider(Func<int, SkillMacro> macroProvider)
        {
            _macroProvider = macroProvider;
            InvalidateQuickSlotValidationCache();
        }

        /// <summary>
        /// Set the font for text rendering
        /// </summary>
        public override void SetFont(SpriteFont font)
        {
            _font = font;
            InvalidateQuickSlotValidationCache();
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

        public void SetTooltipOrigins(Point[] tooltipOrigins)
        {
            if (tooltipOrigins == null)
                return;

            for (int i = 0; i < Math.Min(_tooltipFrameOrigins.Length, tooltipOrigins.Length); i++)
            {
                _tooltipFrameOrigins[i] = tooltipOrigins[i];
            }
        }

        public void SetEquipTooltipAssets(EquipUIBigBang.EquipTooltipAssets assets)
        {
            _equipTooltipAssets = assets;
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

            ValidateVisibleQuickSlotPresentation((int)gameTime.TotalGameTime.TotalMilliseconds);

            int slotCount = SlotCount;
            int slotOffset = SlotOffset;
            int slotsPerRow = _currentBar == BAR_FUNCTION ? 6 : SLOTS_PER_ROW;
            int rows = (slotCount + slotsPerRow - 1) / slotsPerRow;

            // Calculate content area (offset from window position)
            int contentX = Position.X + 5;
            int contentY = Position.Y + 5;
            bool useCachedPrimarySurface = TryDrawCachedPrimaryQuickSlotSurface(sprite, contentX, contentY);

            // Draw slots
            for (int i = 0; i < slotCount; i++)
            {
                int row = i / slotsPerRow;
                int col = i % slotsPerRow;
                int slotX = contentX + col * (SLOT_SIZE + SLOT_PADDING);
                int slotY = contentY + row * (SLOT_SIZE + SLOT_PADDING + 12); // Extra space for key labels

                int absoluteSlotIndex = slotOffset + i;

                if (!useCachedPrimarySurface)
                {
                    sprite.Draw(_emptySlotTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
                    DrawQuickSlotBinding(sprite, slotX, slotY, absoluteSlotIndex, TickCount);
                }
                else
                {
                    DrawQuickSlotBindingDynamicOverlay(sprite, slotX, slotY, absoluteSlotIndex, TickCount);
                }

                // Draw highlight on hovered slot
                if (i == _hoveredSlot)
                {
                    sprite.Draw(_slotHighlightTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
                }

                // Draw key label below slot
                string[] keyLabels = GetActiveBarKeyLabels();
                if (!useCachedPrimarySurface && _font != null && i < keyLabels.Length)
                {
                    string label = keyLabels[i];
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
            if (_isDragging)
            {
                if (_dragType == DragBindingType.Skill && _dragSkillId > 0)
                {
                    var dragIcon = GetSkillIcon(_dragSkillId);
                    if (dragIcon != null)
                    {
                        int dragX = (int)_dragPosition.X - SLOT_SIZE / 2;
                        int dragY = (int)_dragPosition.Y - SLOT_SIZE / 2;
                        dragIcon.DrawBackground(sprite, null, null, dragX, dragY, Color.White * 0.7f, false, null);
                    }
                }
                else if (_dragType == DragBindingType.Item && _dragItemId > 0)
                {
                    Texture2D itemTexture = _inventoryRuntime?.GetItemTexture(_dragItemInventoryType, _dragItemId);
                    if (itemTexture != null)
                    {
                        int dragX = (int)_dragPosition.X - SLOT_SIZE / 2;
                        int dragY = (int)_dragPosition.Y - SLOT_SIZE / 2;
                        sprite.Draw(itemTexture, new Rectangle(dragX, dragY, SLOT_SIZE, SLOT_SIZE), Color.White * 0.7f);
                    }
                }
                else if (_dragType == DragBindingType.Macro && _dragMacroIndex >= 0)
                {
                    SkillMacro macro = _macroProvider?.Invoke(_dragMacroIndex);
                    int macroSkillId = GetMacroDisplaySkillId(macro);
                    int dragX = (int)_dragPosition.X - SLOT_SIZE / 2;
                    int dragY = (int)_dragPosition.Y - SLOT_SIZE / 2;
                    IDXObject dragIcon = macroSkillId > 0 ? GetSkillIcon(macroSkillId) : null;
                    if (dragIcon != null)
                    {
                        dragIcon.DrawBackground(sprite, null, null, dragX, dragY, Color.White * 0.7f, false, null);
                    }
                    else
                    {
                        sprite.Draw(_emptySlotTexture, new Rectangle(dragX, dragY, SLOT_SIZE, SLOT_SIZE), Color.White);
                    }

                    if (_font != null)
                    {
                        DrawTextWithShadow(sprite, $"M{_dragMacroIndex + 1}",
                            new Vector2(dragX, dragY - 12),
                            Color.Yellow, Color.Black, 0.75f);
                    }
                }
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

        private void DrawQuickSlotBinding(SpriteBatch sprite, int slotX, int slotY, int absoluteSlotIndex, int currentTime)
        {
            QuickSlotPresentationState state = GetQuickSlotPresentationStateForDraw(absoluteSlotIndex);
            DrawQuickSlotBindingBase(sprite, slotX, slotY, state);
            DrawQuickSlotBindingDynamicOverlay(sprite, slotX, slotY, state, currentTime);
        }

        private void DrawQuickSlotBindingBase(SpriteBatch sprite, int slotX, int slotY, int absoluteSlotIndex)
        {
            QuickSlotPresentationState state = GetQuickSlotPresentationStateForDraw(absoluteSlotIndex);
            DrawQuickSlotBindingBase(sprite, slotX, slotY, state);
        }

        private void DrawQuickSlotBindingBase(SpriteBatch sprite, int slotX, int slotY, QuickSlotPresentationState state)
        {
            switch (state.BindingType)
            {
                case QuickSlotPresentationBindingType.Skill:
                    DrawSkillBindingBase(sprite, slotX, slotY, state.Id);
                    break;

                case QuickSlotPresentationBindingType.Macro:
                    DrawMacroBindingBase(sprite, slotX, slotY, state.Id);
                    break;

                case QuickSlotPresentationBindingType.Item:
                    DrawItemBinding(sprite, slotX, slotY, state.Id, state.InventoryType, state.Quantity);
                    break;
            }
        }

        private void DrawQuickSlotBindingDynamicOverlay(SpriteBatch sprite, int slotX, int slotY, int absoluteSlotIndex, int currentTime)
        {
            QuickSlotPresentationState state = GetQuickSlotPresentationStateForDraw(absoluteSlotIndex);
            DrawQuickSlotBindingDynamicOverlay(sprite, slotX, slotY, state, currentTime);
        }

        private void DrawQuickSlotBindingDynamicOverlay(SpriteBatch sprite, int slotX, int slotY, QuickSlotPresentationState state, int currentTime)
        {
            switch (state.BindingType)
            {
                case QuickSlotPresentationBindingType.Skill:
                    DrawSkillBindingCooldownOverlay(sprite, slotX, slotY, state.Id, currentTime);
                    break;

                case QuickSlotPresentationBindingType.Macro:
                    DrawMacroBindingCooldownOverlay(sprite, slotX, slotY, state.Id, currentTime);
                    break;
            }
        }

        private QuickSlotPresentationState GetQuickSlotPresentationStateForDraw(int absoluteSlotIndex)
        {
            if (_currentBar == BAR_PRIMARY &&
                absoluteSlotIndex >= 0 &&
                absoluteSlotIndex < _primaryQuickSlotPresentationStates.Length)
            {
                return _primaryQuickSlotPresentationStates[absoluteSlotIndex];
            }

            return BuildQuickSlotPresentationState(absoluteSlotIndex);
        }

        private void DrawSkillBinding(SpriteBatch sprite, int slotX, int slotY, int skillId, int currentTime)
        {
            DrawSkillBindingBase(sprite, slotX, slotY, skillId);
            DrawSkillBindingCooldownOverlay(sprite, slotX, slotY, skillId, currentTime);
        }

        private void DrawSkillBindingBase(SpriteBatch sprite, int slotX, int slotY, int skillId)
        {
            IDXObject icon = GetSkillIcon(skillId);
            if (icon != null)
            {
                // Use DrawBackground for IDXObject (handles origin automatically).
                icon.DrawBackground(sprite, null, null, slotX, slotY, Color.White, false, null);
            }
        }

        private void DrawSkillBindingCooldownOverlay(SpriteBatch sprite, int slotX, int slotY, int skillId, int currentTime)
        {
            if (TryGetCooldownVisualState(skillId, currentTime, out int cooldownFrameIndex, out string remainingText))
            {
                DrawCooldownMask(sprite, slotX, slotY, cooldownFrameIndex);

                if (_font != null && !string.IsNullOrWhiteSpace(remainingText))
                {
                    Vector2 textSize = _font.MeasureString(remainingText) * COOLDOWN_TEXT_SCALE;
                    Vector2 textPosition = new(
                        slotX + SLOT_SIZE - textSize.X - 2f,
                        slotY + SLOT_SIZE - textSize.Y - 1f);

                    DrawTextWithShadow(sprite, remainingText, textPosition, Color.White, Color.Black, COOLDOWN_TEXT_SCALE);
                }
            }
        }

        private void DrawMacroBinding(SpriteBatch sprite, int slotX, int slotY, int macroIndex, int currentTime)
        {
            DrawMacroBindingBase(sprite, slotX, slotY, macroIndex);
            DrawMacroBindingCooldownOverlay(sprite, slotX, slotY, macroIndex, currentTime);
        }

        private void DrawMacroBindingBase(SpriteBatch sprite, int slotX, int slotY, int macroIndex)
        {
            SkillMacro macro = _macroProvider?.Invoke(macroIndex);
            int skillId = GetMacroDisplaySkillId(macro);
            if (skillId > 0)
            {
                DrawSkillBindingBase(sprite, slotX, slotY, skillId);
            }

            if (_font != null)
            {
                DrawTextWithShadow(sprite, $"M{macroIndex + 1}",
                    new Vector2(slotX + 2, slotY + 1),
                    Color.Yellow, Color.Black, 0.75f);
            }
        }

        private void DrawMacroBindingCooldownOverlay(SpriteBatch sprite, int slotX, int slotY, int macroIndex, int currentTime)
        {
            SkillMacro macro = _macroProvider?.Invoke(macroIndex);
            int skillId = GetMacroDisplaySkillId(macro);
            if (skillId > 0)
            {
                DrawSkillBindingCooldownOverlay(sprite, slotX, slotY, skillId, currentTime);
            }
        }

        private void DrawItemBinding(SpriteBatch sprite, int slotX, int slotY, int itemId, InventoryType inventoryType, int itemCount)
        {
            Texture2D itemTexture = _inventoryRuntime?.GetItemTexture(inventoryType, itemId);
            if (itemTexture != null)
            {
                sprite.Draw(itemTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
            }

            if (_font != null && itemCount > 1)
            {
                InventoryRenderUtil.DrawSlotQuantity(sprite, _font, itemCount, slotX, slotY, SLOT_SIZE);
            }
        }

        private void DrawTextWithShadow(SpriteBatch sprite, string text, Vector2 position, Color color, Color shadowColor, float scale = 1.0f)
        {
            sprite.DrawString(_font, text, position + Vector2.One, shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawHoveredSkillTooltip(SpriteBatch sprite, int renderWidth, int renderHeight, int currentTime)
        {
            if (_font == null || _isDragging || _hoveredSlot < 0)
                return;

            Rectangle hoveredSlotRect = ResolveSlotBounds(_hoveredSlot);
            Point tooltipAnchor = new Point(hoveredSlotRect.Right + TOOLTIP_OFFSET_X, hoveredSlotRect.Bottom);

            int absoluteSlotIndex = SlotOffset + _hoveredSlot;
            int macroIndex = _skillManager?.GetHotkeyMacroIndex(absoluteSlotIndex) ?? -1;
            if (macroIndex >= 0)
            {
                DrawHoveredMacroTooltip(sprite, renderWidth, renderHeight, macroIndex);
                return;
            }

            int skillId = _skillManager?.GetHotkeySkill(absoluteSlotIndex) ?? 0;
            if (skillId <= 0)
            {
                DrawHoveredItemTooltip(sprite, renderWidth, renderHeight, absoluteSlotIndex);
                return;
            }

            SkillData skill = _skillLoader?.LoadSkill(skillId);
            if (skill == null)
                return;

            int level = Math.Max(1, _skillManager.GetSkillLevel(skillId));
            SkillLevelData levelData = skill.GetLevel(level);
            string title = SanitizeFontText(skill.Name);
            string description = SanitizeFontText(skill.Description);
            string levelLine = $"Level: {level}";
            string costLine = GetTooltipCostLineMarkup(skillId, levelData, currentTime);

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SLOT_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            string[] wrappedLevel = WrapTooltipText(levelLine, sectionWidth);
            TooltipLine[] wrappedCost = WrapTooltipText(costLine, sectionWidth, new Color(180, 255, 210));

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

            Rectangle backgroundRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                stackalloc[] { 1, 0, 2 },
                out int tooltipFrameIndex);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int titleX = backgroundRect.X + TOOLTIP_PADDING;
            int titleY = backgroundRect.Y + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = backgroundRect.Y + TOOLTIP_PADDING + (int)Math.Ceiling(titleHeight) + TOOLTIP_TITLE_GAP;
            int iconX = backgroundRect.X + TOOLTIP_PADDING;
            IDXObject icon = GetSkillIcon(skillId);
            icon?.DrawBackground(sprite, null, null, iconX, contentY, Color.White, false, null);

            int textX = backgroundRect.X + textLeftOffset;
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
                DrawTooltipLines(sprite, wrappedCost, textX, sectionY);
            }
        }

        private bool TryGetCooldownVisualState(int skillId, int currentTime, out int frameIndex, out string remainingText)
        {
            frameIndex = 0;
            remainingText = string.Empty;
            return _skillManager != null
                && _skillManager.TryGetCooldownMaskVisualState(skillId, currentTime, out frameIndex, out remainingText);
        }

        private void DrawHoveredItemTooltip(SpriteBatch sprite, int renderWidth, int renderHeight, int absoluteSlotIndex)
        {
            int itemId = _skillManager?.GetHotkeyItem(absoluteSlotIndex) ?? 0;
            if (itemId <= 0)
                return;

            Rectangle hoveredSlotRect = ResolveSlotBounds(_hoveredSlot);
            Point tooltipAnchor = new Point(hoveredSlotRect.Right + TOOLTIP_OFFSET_X, hoveredSlotRect.Bottom);

            int itemCount = _skillManager.GetHotkeyItemCount(absoluteSlotIndex);
            InventoryType inventoryType = _skillManager.GetHotkeyItemInventoryType(absoluteSlotIndex);
            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(itemId, inventoryType);
            string itemName = metadata.ItemName;
            string typeLine = metadata.TypeName;
            string countLine = itemCount > 0 ? $"Quantity: {itemCount}" : string.Empty;
            string description = metadata.Description;
            Texture2D itemTexture = ResolveQuickSlotItemTexture(itemId, inventoryType);
            Texture2D cashLabelTexture = metadata.IsCashItem ? _equipTooltipAssets?.CashLabel : null;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + SLOT_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(SanitizeFontText(itemName), titleWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);

            List<(string[] Lines, Color Color, float Height)> wrappedSections = new();
            void AddSection(string text, Color color)
            {
                string[] wrapped = WrapTooltipText(SanitizeFontText(text), sectionWidth);
                float height = MeasureLinesHeight(wrapped);
                if (height > 0f)
                {
                    wrappedSections.Add((wrapped, color, height));
                }
            }

            AddSection(typeLine, new Color(180, 220, 255));
            for (int i = 0; i < metadata.EffectLines.Count; i++)
            {
                AddSection(metadata.EffectLines[i], new Color(180, 255, 210));
            }
            AddSection(countLine, Color.White);
            for (int i = 0; i < metadata.MetadataLines.Count; i++)
            {
                AddSection(metadata.MetadataLines[i], new Color(255, 214, 156));
            }
            AddSection(description, new Color(255, 238, 196));

            float wrappedSectionHeight = 0f;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                if (wrappedSectionHeight > 0f)
                {
                    wrappedSectionHeight += TOOLTIP_SECTION_GAP;
                }

                wrappedSectionHeight += wrappedSections[i].Height;
            }

            float cashLabelHeight = cashLabelTexture?.Height ?? 0f;
            float contentHeight = wrappedSectionHeight;
            if (cashLabelHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? 2f : 0f) + cashLabelHeight;
            }
            float iconBlockHeight = Math.Max(SLOT_SIZE, contentHeight);
            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_TITLE_GAP + iconBlockHeight);

            Rectangle backgroundRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                stackalloc[] { 1, 0, 2 },
                out int tooltipFrameIndex);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int titleX = backgroundRect.X + TOOLTIP_PADDING;
            int titleY = backgroundRect.Y + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = backgroundRect.Y + TOOLTIP_PADDING + (int)Math.Ceiling(titleHeight) + TOOLTIP_TITLE_GAP;
            if (itemTexture != null)
            {
                sprite.Draw(itemTexture, new Rectangle(backgroundRect.X + TOOLTIP_PADDING, contentY, SLOT_SIZE, SLOT_SIZE), Color.White);
            }

            int textX = backgroundRect.X + textLeftOffset;
            float sectionY = contentY;
            if (cashLabelHeight > 0f)
            {
                sprite.Draw(cashLabelTexture, new Vector2(textX, sectionY), Color.White);
                sectionY += cashLabelHeight;
            }

            if (wrappedSections.Count > 0)
            {
                if (cashLabelHeight > 0f)
                {
                    sectionY += 2f;
                }

                for (int i = 0; i < wrappedSections.Count; i++)
                {
                    if (i > 0)
                    {
                        sectionY += TOOLTIP_SECTION_GAP;
                    }

                    (string[] lines, Color color, float height) = wrappedSections[i];
                    DrawTooltipLines(sprite, lines, textX, sectionY, color);
                    sectionY += height;
                }
            }
        }

        private void DrawHoveredMacroTooltip(SpriteBatch sprite, int renderWidth, int renderHeight, int macroIndex)
        {
            SkillMacro macro = _macroProvider?.Invoke(macroIndex);
            if (macro == null)
                return;

            Rectangle hoveredSlotRect = ResolveSlotBounds(_hoveredSlot);
            Point tooltipAnchor = new Point(hoveredSlotRect.Right + TOOLTIP_OFFSET_X, hoveredSlotRect.Bottom);

            int tooltipWidth = ResolveTooltipWidth();
            string[] wrappedTitle = WrapTooltipText(SanitizeFontText(macro.Name), tooltipWidth - (TOOLTIP_PADDING * 2));
            string[] wrappedNotify = WrapTooltipText(macro.NotifyParty ? "Party notice: On" : "Party notice: Off",
                tooltipWidth - (TOOLTIP_PADDING * 2));
            string[] skillLines = BuildMacroSkillLines(macro);
            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float notifyHeight = MeasureLinesHeight(wrappedNotify);
            float skillHeight = skillLines.Length * _font.LineSpacing;
            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_TITLE_GAP + notifyHeight +
                (skillHeight > 0f ? TOOLTIP_SECTION_GAP + skillHeight : 0f));

            Rectangle backgroundRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                stackalloc[] { 1, 0, 2 },
                out int tooltipFrameIndex);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int textX = backgroundRect.X + TOOLTIP_PADDING;
            float sectionY = backgroundRect.Y + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, textX, sectionY, new Color(255, 220, 120));
            sectionY += titleHeight + TOOLTIP_TITLE_GAP;
            DrawTooltipLines(sprite, wrappedNotify, textX, sectionY, new Color(180, 255, 210));
            sectionY += notifyHeight;

            if (skillLines.Length > 0)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                for (int i = 0; i < skillLines.Length; i++)
                {
                    DrawTooltipText(sprite, skillLines[i], new Vector2(textX, sectionY + (i * _font.LineSpacing)), Color.White);
                }
            }
        }

        private string GetTooltipCostLineMarkup(int skillId, SkillLevelData levelData, int currentTime)
        {
            if (levelData == null)
                return string.Empty;

            SkillManager.CooldownUiState cooldownState = default;
            bool hasCooldownState = _skillManager != null
                && _skillManager.TryGetCooldownUiState(skillId, currentTime, out cooldownState);
            return SkillCooldownTooltipText.FormatTooltipCostLineMarkup(
                levelData,
                hasCooldownState || levelData.Cooldown > 0,
                hasCooldownState ? cooldownState.RemainingMs : 0,
                hasCooldownState ? cooldownState.TooltipStateText : null);
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

            float remainingProgress = SkillManager.ResolveCooldownMaskFallbackFillRatio(frameIndex);
            int overlayHeight = (int)Math.Ceiling(SLOT_SIZE * remainingProgress);
            if (overlayHeight <= 0)
            {
                return;
            }

            sprite.Draw(_cooldownOverlayTexture,
                new Rectangle(slotX, slotY + SLOT_SIZE - overlayHeight, SLOT_SIZE, overlayHeight),
                Color.White);
        }

        private int ResolveTooltipWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TOOLTIP_FALLBACK_WIDTH;
        }

        private Rectangle ResolveSlotBounds(int slotIndex)
        {
            int contentX = Position.X + 5;
            int contentY = Position.Y + 5;
            int slotsPerRow = _currentBar == BAR_FUNCTION ? 6 : SLOTS_PER_ROW;
            int rowHeight = SLOT_SIZE + SLOT_PADDING + 12;
            int row = slotIndex / slotsPerRow;
            int col = slotIndex % slotsPerRow;
            return new Rectangle(
                contentX + col * (SLOT_SIZE + SLOT_PADDING),
                contentY + row * rowHeight,
                SLOT_SIZE,
                SLOT_SIZE);
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
                0 => new Rectangle(anchorPoint.X - tooltipWidth - TOOLTIP_OFFSET_X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight),
                2 => new Rectangle(anchorPoint.X, anchorPoint.Y + TOOLTIP_OFFSET_Y, tooltipWidth, tooltipHeight),
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

        private TooltipLine[] WrapTooltipText(string text, float maxWidth, Color baseColor)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
                return Array.Empty<TooltipLine>();

            List<TooltipToken> tokens = TokenizeTooltipText(text, baseColor);
            List<TooltipLine> lines = new();
            TooltipLine currentLine = new();
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
            List<TooltipToken> tokens = new();
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
                    while (index < segmentText.Length
                        && segmentText[index] != '\n'
                        && char.IsWhiteSpace(segmentText[index]) == whitespace)
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

            StringBuilder builder = new();
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

        private static Color ResolveTooltipMarkerColor(char marker, Color baseColor)
        {
            return marker switch
            {
                'c' => new Color(255, 214, 140),
                'b' => new Color(130, 190, 255),
                'g' => new Color(160, 255, 160),
                'r' => new Color(255, 150, 150),
                _ => baseColor
            };
        }

        private readonly struct TooltipToken
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
                InvalidateQuickSlotBindingValidationCache();
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
                    _dragMacroIndex = -1;
                    _dragItemId = 0;
                    _dragItemInventoryType = InventoryType.NONE;
                    _dragType = DragBindingType.Skill;
                    _dragPosition = new Vector2(mouseX, mouseY);
                    return;
                }

                int macroIndex = _skillManager?.GetHotkeyMacroIndex(absoluteSlot) ?? -1;
                if (macroIndex >= 0)
                {
                    _isDragging = true;
                    _dragSourceSlot = absoluteSlot;
                    _dragSkillId = 0;
                    _dragMacroIndex = macroIndex;
                    _dragItemId = 0;
                    _dragItemInventoryType = InventoryType.NONE;
                    _dragType = DragBindingType.Macro;
                    _dragPosition = new Vector2(mouseX, mouseY);
                    return;
                }

                int itemId = _skillManager?.GetHotkeyItem(absoluteSlot) ?? 0;
                if (itemId > 0)
                {
                    _isDragging = true;
                    _dragSourceSlot = absoluteSlot;
                    _dragSkillId = 0;
                    _dragMacroIndex = -1;
                    _dragItemId = itemId;
                    _dragItemInventoryType = _skillManager.GetHotkeyItemInventoryType(absoluteSlot);
                    _dragType = DragBindingType.Item;
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
                        int targetMacro = _skillManager?.GetHotkeyMacroIndex(absoluteTarget) ?? -1;
                        int targetItemId = _skillManager?.GetHotkeyItem(absoluteTarget) ?? 0;
                        InventoryType targetItemInventoryType = _skillManager?.GetHotkeyItemInventoryType(absoluteTarget) ?? InventoryType.NONE;

                        if (TryAssignDraggedBinding(absoluteTarget))
                        {
                            RestoreBinding(_dragSourceSlot, targetSkill, targetMacro, targetItemId, targetItemInventoryType);
                        }
                    }
                }

                _isDragging = false;
                _dragSourceSlot = -1;
                _dragSkillId = 0;
                _dragMacroIndex = -1;
                _dragItemId = 0;
                _dragItemInventoryType = InventoryType.NONE;
                _dragType = DragBindingType.None;
            }
        }

        public void CancelDrag()
        {
            _isDragging = false;
            _dragSourceSlot = -1;
            _dragSkillId = 0;
            _dragMacroIndex = -1;
            _dragItemId = 0;
            _dragItemInventoryType = InventoryType.NONE;
            _dragType = DragBindingType.None;
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

            InvalidateQuickSlotBindingValidationCache();
            OnSkillDropped?.Invoke(absoluteSlot, skillId);
            return true;
        }

        public bool AcceptMacroDrop(int macroIndex, int mouseX, int mouseY)
        {
            int slot = GetSlotAtPosition(mouseX, mouseY);
            if (slot < 0)
                return false;

            int absoluteSlot = SlotOffset + slot;
            if (_skillManager?.TrySetMacroHotkey(absoluteSlot, macroIndex) != true)
                return false;

            InvalidateQuickSlotBindingValidationCache();
            return true;
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            ValidateVisibleQuickSlotPresentation((int)gameTime.TotalGameTime.TotalMilliseconds);
            RefreshPrimaryQuickSlotSurfaceIfNeeded();

            if (_isDragging && !IsCurrentDragBindingStillValid())
            {
                CancelDrag();
            }

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

        public void SetPrimaryBarKeyLabels(IReadOnlyList<string> labels)
        {
            SetBarKeyLabels(BAR_PRIMARY, labels, SkillManager.PRIMARY_SLOT_COUNT);
        }

        public void SetFunctionBarKeyLabels(IReadOnlyList<string> labels)
        {
            SetBarKeyLabels(BAR_FUNCTION, labels, SkillManager.FUNCTION_SLOT_COUNT);
        }

        private void SetBarKeyLabels(int bar, IReadOnlyList<string> labels, int maxCount)
        {
            if (labels == null)
            {
                _barKeyLabelOverrides.Remove(bar);
                if (bar == BAR_PRIMARY)
                {
                    InvalidateQuickSlotValidationCache();
                }
                return;
            }

            _barKeyLabelOverrides[bar] = labels
                .Take(maxCount)
                .Select(label => string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim())
                .ToArray();

            if (bar == BAR_PRIMARY)
            {
                InvalidateQuickSlotValidationCache();
            }
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Clear cached icons
        /// </summary>
        public void ClearIconCache()
        {
            _skillIconCache.Clear();
            InvalidateQuickSlotValidationCache();
        }

        public void InvalidatePresentationCache()
        {
            InvalidateQuickSlotValidationCache();
        }

        private string[] GetActiveBarKeyLabels()
        {
            if (_barKeyLabelOverrides.TryGetValue(_currentBar, out string[] labels)
                && labels != null
                && labels.Length > 0)
            {
                return labels;
            }

            return BarKeyLabels[_currentBar];
        }

        private bool TryAssignDraggedBinding(int targetSlot)
        {
            bool assigned = _dragType switch
            {
                DragBindingType.Skill => _skillManager?.TrySetHotkey(targetSlot, _dragSkillId) == true,
                DragBindingType.Macro => _skillManager?.TrySetMacroHotkey(targetSlot, _dragMacroIndex) == true,
                DragBindingType.Item => _skillManager?.TrySetItemHotkey(targetSlot, _dragItemId, _dragItemInventoryType) == true,
                _ => false
            };

            if (assigned)
            {
                InvalidateQuickSlotBindingValidationCache();
            }

            return assigned;
        }

        private void InvalidateQuickSlotBindingValidationCache()
        {
            _lastQuickSlotValidationTime = int.MinValue;
            _lastQuickSlotValidationBar = -1;
        }

        private void InvalidateQuickSlotValidationCache()
        {
            InvalidateQuickSlotBindingValidationCache();
            _primaryQuickSlotSurfaceDirty = true;
        }

        private void ValidateVisibleQuickSlotPresentation(int currentTime)
        {
            if (_skillManager == null)
                return;

            if (_lastQuickSlotValidationTime == currentTime &&
                _lastQuickSlotValidationBar == _currentBar)
            {
                return;
            }

            if (_currentBar == BAR_PRIMARY)
            {
                _lastPrimaryQuickSlotCompareResult = CompareValidatePrimaryQuickSlotPresentation();
                if (!_lastPrimaryQuickSlotCompareResult)
                {
                    _primaryQuickSlotSurfaceDirty = true;
                }

                _lastQuickSlotValidationTime = currentTime;
                _lastQuickSlotValidationBar = _currentBar;
                return;
            }

            _skillManager.RevalidateHotkeys(SlotOffset, SlotCount);
            _lastQuickSlotValidationTime = currentTime;
            _lastQuickSlotValidationBar = _currentBar;
        }

        private bool CompareValidatePrimaryQuickSlotPresentation()
        {
            if (_skillManager == null)
            {
                bool alreadyEmpty = !_hasPrimaryQuickSlotPresentationSnapshot ||
                                   _primaryQuickSlotPresentationStates.All(static state => state.Equals(default));
                Array.Clear(_primaryQuickSlotPresentationStates, 0, _primaryQuickSlotPresentationStates.Length);
                _hasPrimaryQuickSlotPresentationSnapshot = false;
                return alreadyEmpty;
            }

            _skillManager.RevalidateHotkeys(0, SkillManager.PRIMARY_SLOT_COUNT);

            bool same = _hasPrimaryQuickSlotPresentationSnapshot;
            for (int slotIndex = 0; slotIndex < SkillManager.PRIMARY_SLOT_COUNT; slotIndex++)
            {
                QuickSlotPresentationState currentState = BuildQuickSlotPresentationState(slotIndex);
                if (!_primaryQuickSlotPresentationStates[slotIndex].Equals(currentState))
                {
                    same = false;
                    _primaryQuickSlotPresentationStates[slotIndex] = currentState;
                }

                // The client count scan marks the quick-slot cache dirty whenever a live
                // item stack is found, even if the resulting item id/count snapshot matches.
                if (currentState.BindingType == QuickSlotPresentationBindingType.Item && currentState.Quantity > 0)
                {
                    same = false;
                }
            }

            _hasPrimaryQuickSlotPresentationSnapshot = true;
            return same;
        }

        private bool TryDrawCachedPrimaryQuickSlotSurface(SpriteBatch sprite, int contentX, int contentY)
        {
            if (_currentBar != BAR_PRIMARY
                || _primaryQuickSlotSurfaceDirty
                || _primaryQuickSlotSurface == null)
            {
                return false;
            }

            sprite.Draw(_primaryQuickSlotSurface, new Vector2(contentX, contentY), Color.White);
            return true;
        }

        private void RefreshPrimaryQuickSlotSurfaceIfNeeded()
        {
            if (_currentBar != BAR_PRIMARY
                || !IsVisible
                || !_primaryQuickSlotSurfaceDirty
                || _graphicsDevice == null)
            {
                return;
            }

            int surfaceWidth = ResolvePrimaryQuickSlotSurfaceWidth();
            int surfaceHeight = ResolvePrimaryQuickSlotSurfaceHeight();
            if (surfaceWidth <= 0 || surfaceHeight <= 0)
            {
                return;
            }

            EnsurePrimaryQuickSlotSurface(surfaceWidth, surfaceHeight);
            if (_primaryQuickSlotSurface == null)
            {
                return;
            }

            RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
            Viewport previousViewport = _graphicsDevice.Viewport;

            try
            {
                _graphicsDevice.SetRenderTarget(_primaryQuickSlotSurface);
                _graphicsDevice.Clear(Color.Transparent);

                using var spriteBatch = new SpriteBatch(_graphicsDevice);
                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);

                string[] keyLabels = GetActiveBarKeyLabels();
                for (int slotIndex = 0; slotIndex < SkillManager.PRIMARY_SLOT_COUNT; slotIndex++)
                {
                    Rectangle slotBounds = ResolvePrimaryQuickSlotLocalBounds(slotIndex);
                    spriteBatch.Draw(_emptySlotTexture, slotBounds, Color.White);
                    DrawQuickSlotBindingBase(spriteBatch, slotBounds.X, slotBounds.Y, slotIndex);

                    if (_font != null && slotIndex < keyLabels.Length)
                    {
                        string label = keyLabels[slotIndex];
                        Vector2 labelSize = _font.MeasureString(label);
                        int labelX = slotBounds.X + (SLOT_SIZE - (int)labelSize.X) / 2;
                        int labelY = slotBounds.Y + SLOT_SIZE + 1;
                        spriteBatch.DrawString(_font, label, new Vector2(labelX, labelY), Color.White);
                    }
                }

                spriteBatch.End();
                _primaryQuickSlotSurfaceDirty = false;
            }
            finally
            {
                if (previousTargets.Length > 0)
                {
                    _graphicsDevice.SetRenderTargets(previousTargets);
                }
                else
                {
                    _graphicsDevice.SetRenderTarget(null);
                }

                _graphicsDevice.Viewport = previousViewport;
            }
        }

        private void EnsurePrimaryQuickSlotSurface(int width, int height)
        {
            if (_primaryQuickSlotSurface != null
                && _primaryQuickSlotSurface.Width == width
                && _primaryQuickSlotSurface.Height == height)
            {
                return;
            }

            _primaryQuickSlotSurface?.Dispose();
            _primaryQuickSlotSurface = new RenderTarget2D(
                _graphicsDevice,
                width,
                height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);
        }

        private static int ResolvePrimaryQuickSlotSurfaceWidth()
        {
            return ((SkillManager.PRIMARY_SLOT_COUNT - 1) * (SLOT_SIZE + SLOT_PADDING)) + SLOT_SIZE;
        }

        private static int ResolvePrimaryQuickSlotSurfaceHeight()
        {
            return SLOT_SIZE + 12;
        }

        private static Rectangle ResolvePrimaryQuickSlotLocalBounds(int slotIndex)
        {
            return new Rectangle(
                slotIndex * (SLOT_SIZE + SLOT_PADDING),
                0,
                SLOT_SIZE,
                SLOT_SIZE);
        }

        private QuickSlotPresentationState BuildQuickSlotPresentationState(int slotIndex)
        {
            int skillId = _skillManager?.GetHotkeySkill(slotIndex) ?? 0;
            if (skillId > 0)
                return new QuickSlotPresentationState(QuickSlotPresentationBindingType.Skill, skillId, 0);

            int macroIndex = _skillManager?.GetHotkeyMacroIndex(slotIndex) ?? -1;
            if (macroIndex >= 0)
                return new QuickSlotPresentationState(QuickSlotPresentationBindingType.Macro, macroIndex, 0);

            int itemId = _skillManager?.GetHotkeyItem(slotIndex) ?? 0;
            if (itemId > 0)
            {
                int itemCount = _skillManager?.GetHotkeyItemCount(slotIndex) ?? 0;
                InventoryType inventoryType = _skillManager?.GetHotkeyItemInventoryType(slotIndex) ?? InventoryType.NONE;
                return new QuickSlotPresentationState(QuickSlotPresentationBindingType.Item, itemId, itemCount, inventoryType);
            }

            return default;
        }

        private void RestoreBinding(int slotIndex, int skillId, int macroIndex, int itemId, InventoryType inventoryType)
        {
            if (skillId > 0)
            {
                _skillManager?.TrySetHotkey(slotIndex, skillId);
                InvalidateQuickSlotBindingValidationCache();
                return;
            }

            if (macroIndex >= 0)
            {
                _skillManager?.TrySetMacroHotkey(slotIndex, macroIndex);
                InvalidateQuickSlotBindingValidationCache();
                return;
            }

            if (itemId > 0)
            {
                _skillManager?.TrySetItemHotkey(slotIndex, itemId, inventoryType);
                InvalidateQuickSlotBindingValidationCache();
                return;
            }

            _skillManager?.ClearHotkey(slotIndex);
            InvalidateQuickSlotBindingValidationCache();
        }

        private bool IsCurrentDragBindingStillValid()
        {
            if (_dragSourceSlot < 0 || _skillManager == null)
                return false;

            return _dragType switch
            {
                DragBindingType.Skill => _skillManager.GetHotkeySkill(_dragSourceSlot) == _dragSkillId,
                DragBindingType.Macro => _skillManager.GetHotkeyMacroIndex(_dragSourceSlot) == _dragMacroIndex,
                DragBindingType.Item => _skillManager.GetHotkeyItem(_dragSourceSlot) == _dragItemId,
                _ => false
            };
        }

        private int GetMacroDisplaySkillId(SkillMacro macro)
        {
            if (macro?.SkillIds == null)
                return 0;

            for (int i = 0; i < macro.SkillIds.Length; i++)
            {
                if (macro.SkillIds[i] > 0)
                    return macro.SkillIds[i];
            }

            return 0;
        }

        private string[] BuildMacroSkillLines(SkillMacro macro)
        {
            if (macro?.SkillIds == null)
                return Array.Empty<string>();

            List<string> lines = new();
            for (int i = 0; i < macro.SkillIds.Length; i++)
            {
                int skillId = macro.SkillIds[i];
                if (skillId <= 0)
                    continue;

                SkillData skill = _skillLoader?.LoadSkill(skillId);
                string skillName = !string.IsNullOrWhiteSpace(skill?.Name)
                    ? SanitizeFontText(skill.Name)
                    : $"Skill {skillId}";
                lines.Add($"{i + 1}. {skillName}");
            }

            return lines.ToArray();
        }

        private static string ResolveItemName(int itemId)
        {
            return HaCreator.Program.InfoManager?.ItemNameCache != null &&
                   HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                   !string.IsNullOrWhiteSpace(itemInfo.Item2)
                ? itemInfo.Item2
                : $"Item {itemId}";
        }

        private static string ResolveItemDescription(int itemId)
        {
            return HaCreator.Program.InfoManager?.ItemNameCache != null &&
                   HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                ? itemInfo.Item3 ?? string.Empty
                : string.Empty;
        }

        private static string ResolveItemTypeName(int itemId, InventoryType inventoryType)
        {
            if (HaCreator.Program.InfoManager?.ItemNameCache != null &&
                HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo) &&
                !string.IsNullOrWhiteSpace(itemInfo.Item1))
            {
                return itemInfo.Item1;
            }

            return inventoryType == InventoryType.NONE ? "Item" : inventoryType.ToString();
        }

        private Texture2D ResolveQuickSlotItemTexture(int itemId, InventoryType inventoryType)
        {
            if (_inventoryRuntime == null || inventoryType == InventoryType.NONE)
            {
                return null;
            }

            IReadOnlyList<InventorySlotData> slots = _inventoryRuntime.GetSlots(inventoryType);
            if (slots == null)
            {
                return null;
            }

            InventorySlotData slot = slots.FirstOrDefault(candidate => candidate?.ItemId == itemId && candidate.ItemTexture != null);
            return slot?.ItemTexture;
        }
        #endregion
    }
}
