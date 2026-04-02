using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI.Controls;
using HaCreator.MapSimulator.Companions;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Inventory UI window displaying item slots in tabs.
    /// Structure: UI.wz/UIWindow.img/Item/
    /// </summary>
    public class InventoryUI : UIWindowBase, IInventoryRuntime
    {
        #region Constants
        protected const int SLOT_SIZE = 32;
        protected const int SLOTS_PER_ROW = 4;
        protected const int VISIBLE_ROWS = 6;
        protected const int TOTAL_SLOTS = 24;
        protected const int SLOT_PITCH = SLOT_SIZE + 1;
        protected const int MAX_SLOT_LIMIT = 96;
        protected const int SLOT_EXPANSION_STEP = 4;

        protected const int TAB_EQUIP = 0;
        protected const int TAB_USE = 1;
        protected const int TAB_SETUP = 2;
        protected const int TAB_ETC = 3;
        protected const int TAB_CASH = 4;

        private const int SLOT_ORIGIN_X = 10;
        private const int SLOT_ORIGIN_Y = 50;
        private const int MESO_TEXT_RIGHT_X = 152;
        private const int MESO_TEXT_Y = 266;
        private const float INVENTORY_TEXT_SCALE = 0.72f;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_SIZE = 32;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_OFFSET_X = 18;
        private const int TOOLTIP_OFFSET_Y = 14;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int TOOLTIP_FALLBACK_WIDTH = 214;
        private const int TOOLTIP_BITMAP_GAP = 1;
        #endregion

        private readonly struct TooltipSection
        {
            public TooltipSection(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }

        private readonly struct TooltipLabeledValueRow
        {
            public TooltipLabeledValueRow(
                Texture2D labelTexture,
                string fallbackLabel,
                string valueText,
                Color valueColor,
                IReadOnlyList<TooltipValueSegment> valueSegments = null)
            {
                LabelTexture = labelTexture;
                FallbackLabel = fallbackLabel;
                ValueText = valueText;
                ValueColor = valueColor;
                ValueSegments = valueSegments;
            }

            public Texture2D LabelTexture { get; }
            public string FallbackLabel { get; }
            public string ValueText { get; }
            public Color ValueColor { get; }
            public IReadOnlyList<TooltipValueSegment> ValueSegments { get; }
        }

        private readonly struct TooltipValueSegment
        {
            public TooltipValueSegment(Texture2D texture, string text = null)
            {
                Texture = texture;
                Text = text;
            }

            public Texture2D Texture { get; }
            public string Text { get; }
        }

        #region Fields
        protected int _currentTab = TAB_EQUIP;
        protected int _scrollOffset;

        private UIObject _tabEquip;
        private UIObject _tabUse;
        private UIObject _tabSetup;
        private UIObject _tabEtc;
        private UIObject _tabCash;
        private UIObject _btnGather;
        private UIObject _btnSort;
        private UIObject _btnCashShop;

        private readonly Dictionary<InventoryType, List<InventorySlotData>> _inventoryData;
        private readonly Dictionary<InventoryType, int> _inventorySlotLimits;

        private long _mesoCount;
        private SpriteFont _font;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Texture2D _debugTooltipTexture;
        private readonly GraphicsDevice _graphicsDevice;
        private EquipUIBigBang.EquipTooltipAssets _equipTooltipAssets;
        private CharacterLoader _characterLoader;
        private CompanionEquipmentLoader _companionTooltipLoader;
        private Point _lastMousePosition;
        private InventoryType _hoveredInventoryType = InventoryType.NONE;
        private int _hoveredSlotIndex = -1;
        private MouseState _previousInteractionMouseState;
        private bool _isDraggingItem;
        private Func<InventoryType, int, int, string> _itemConsumptionGuard;
        private InventoryType _draggedInventoryType = InventoryType.NONE;
        private int _draggedSlotIndex = -1;
        private InventorySlotData _draggedSlotData;
        private Point _draggedItemPosition;

        protected Texture2D ActiveIconTexture;
        protected Texture2D DisabledSlotTexture;
        protected Texture2D SlotShadowTexture;
        protected readonly Texture2D[] GradeFrameTextures = new Texture2D[6];
        #endregion

        #region Properties
        public override string WindowName => "Inventory";
        public Action<int> ItemUpgradeRequested { get; set; }
        public Action CashShopRequested { get; set; }
        public Action<string> ItemConsumptionBlocked { get; set; }
        public Func<int, InventoryType, bool> ItemUseRequested { get; set; }

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value < TAB_EQUIP || value > TAB_CASH)
                {
                    return;
                }

                _currentTab = value;
                _scrollOffset = 0;
            }
        }

        public long MesoCount
        {
            get => _mesoCount;
            set => _mesoCount = Math.Max(0, value);
        }

        public bool IsDraggingItem => _isDraggingItem;
        public InventoryType DraggedInventoryType => _draggedInventoryType;
        public int DraggedSlotIndex => _draggedSlotIndex;
        public InventorySlotData DraggedSlotData => _draggedSlotData?.Clone();
        #endregion

        #region Constructor
        public InventoryUI(IDXObject frame, Texture2D slotBg, GraphicsDevice device)
            : base(frame)
        {
            _graphicsDevice = device;
            _inventoryData = new Dictionary<InventoryType, List<InventorySlotData>>
            {
                { InventoryType.EQUIP, new List<InventorySlotData>() },
                { InventoryType.USE, new List<InventorySlotData>() },
                { InventoryType.SETUP, new List<InventorySlotData>() },
                { InventoryType.ETC, new List<InventorySlotData>() },
                { InventoryType.CASH, new List<InventorySlotData>() }
            };

            _inventorySlotLimits = new Dictionary<InventoryType, int>
            {
                { InventoryType.EQUIP, TOTAL_SLOTS },
                { InventoryType.USE, TOTAL_SLOTS },
                { InventoryType.SETUP, TOTAL_SLOTS },
                { InventoryType.ETC, TOTAL_SLOTS },
                { InventoryType.CASH, TOTAL_SLOTS }
            };

            if (device != null)
            {
                _debugTooltipTexture = new Texture2D(device, 1, 1);
                _debugTooltipTexture.SetData(new[] { Color.White });
            }
        }
        #endregion

        #region Initialization
        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetRenderAssets(Texture2D activeIconTexture, Texture2D disabledSlotTexture, Texture2D slotShadowTexture, Texture2D[] gradeFrameTextures)
        {
            ActiveIconTexture = activeIconTexture;
            DisabledSlotTexture = disabledSlotTexture;
            SlotShadowTexture = slotShadowTexture;

            if (gradeFrameTextures == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(GradeFrameTextures.Length, gradeFrameTextures.Length); i++)
            {
                GradeFrameTextures[i] = gradeFrameTextures[i];
            }
        }

        public void SetTooltipTextures(Texture2D[] tooltipFrames)
        {
            if (tooltipFrames == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_tooltipFrames.Length, tooltipFrames.Length); i++)
            {
                _tooltipFrames[i] = tooltipFrames[i];
            }
        }

        public void SetEquipTooltipAssets(EquipUIBigBang.EquipTooltipAssets assets)
        {
            _equipTooltipAssets = assets;
        }

        public void SetCharacterLoader(CharacterLoader characterLoader)
        {
            _characterLoader = characterLoader;
        }

        public void InitializeTabs(UIObject equipTab, UIObject useTab, UIObject setupTab, UIObject etcTab, UIObject cashTab)
        {
            _tabEquip = equipTab;
            _tabUse = useTab;
            _tabSetup = setupTab;
            _tabEtc = etcTab;
            _tabCash = cashTab;

            AttachTabButton(_tabEquip, TAB_EQUIP);
            AttachTabButton(_tabUse, TAB_USE);
            AttachTabButton(_tabSetup, TAB_SETUP);
            AttachTabButton(_tabEtc, TAB_ETC);
            AttachTabButton(_tabCash, TAB_CASH);

            UpdateTabStates();
        }

        public void InitializeUtilityButtons(UIObject btnGather, UIObject btnSort, UIObject btnCashShop = null)
        {
            _btnGather = btnGather;
            _btnSort = btnSort;
            _btnCashShop = btnCashShop;

            if (_btnGather != null)
            {
                AddButton(_btnGather);
                _btnGather.ButtonClickReleased += sender => GatherCurrentTab();
            }

            if (_btnSort != null)
            {
                AddButton(_btnSort);
                _btnSort.ButtonClickReleased += sender => SortCurrentTab();
            }

            if (_btnCashShop != null)
            {
                AddButton(_btnCashShop);
                _btnCashShop.ButtonClickReleased += sender => CashShopRequested?.Invoke();
            }
        }

        private void AttachTabButton(UIObject button, int tabIndex)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += sender => CurrentTab = tabIndex;
        }

        protected void UpdateTabStates()
        {
            _tabEquip?.SetButtonState(_currentTab == TAB_EQUIP ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabUse?.SetButtonState(_currentTab == TAB_USE ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabSetup?.SetButtonState(_currentTab == TAB_SETUP ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabEtc?.SetButtonState(_currentTab == TAB_ETC ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCash?.SetButtonState(_currentTab == TAB_CASH ? UIObjectState.Pressed : UIObjectState.Normal);
        }
        #endregion

        #region Drawing
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            if (!_inventoryData.TryGetValue(inventoryType, out List<InventorySlotData> slots))
            {
                return;
            }

            int windowX = Position.X;
            int windowY = Position.Y;

            DrawMesoText(sprite, windowX, windowY, MESO_TEXT_RIGHT_X, MESO_TEXT_Y);
            DrawSlotGrid(sprite, windowX, windowY, inventoryType, slots, SLOT_ORIGIN_X, SLOT_ORIGIN_Y, _scrollOffset, TOTAL_SLOTS);
        }

        protected void DrawMesoText(SpriteBatch sprite, int windowX, int windowY, int rightAnchorX, int textY)
        {
            if (_font == null)
            {
                return;
            }

            string mesoText = _mesoCount.ToString("N0", CultureInfo.InvariantCulture);
            Vector2 size = _font.MeasureString(mesoText) * INVENTORY_TEXT_SCALE;
            Vector2 position = new Vector2(windowX + rightAnchorX - size.X, windowY + textY);
            InventoryRenderUtil.DrawOutlinedText(sprite, _font, mesoText, position, Color.White, INVENTORY_TEXT_SCALE);
        }

        protected void DrawSlotGrid(SpriteBatch sprite, int windowX, int windowY, InventoryType inventoryType, List<InventorySlotData> slots,
            int originX, int originY, int rowOffset, int visibleSlotCount)
        {
            int slotLimit = GetSlotLimit(inventoryType);
            int startSlot = rowOffset * SLOTS_PER_ROW;

            for (int displayIndex = 0; displayIndex < visibleSlotCount; displayIndex++)
            {
                int slotIndex = startSlot + displayIndex;
                int column = displayIndex % SLOTS_PER_ROW;
                int row = displayIndex / SLOTS_PER_ROW;
                int slotX = windowX + originX + (column * SLOT_PITCH);
                int slotY = windowY + originY + (row * SLOT_PITCH);

                if (slotIndex < slots.Count)
                {
                    DrawInventorySlot(sprite, slots[slotIndex], slotX, slotY);
                }
                else if (slotIndex >= slotLimit)
                {
                    DrawDisabledOverlay(sprite, slotX, slotY);
                }
            }
        }

        protected void DrawInventorySlot(SpriteBatch sprite, InventorySlotData slotData, int slotX, int slotY)
        {
            if (slotData == null)
            {
                return;
            }

            if (SlotShadowTexture != null && slotData.ItemTexture != null)
            {
                InventoryRenderUtil.DrawSlotShadow(sprite, SlotShadowTexture, slotX, slotY, SLOT_SIZE);
            }

            if (slotData.ItemTexture != null)
            {
                sprite.Draw(slotData.ItemTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
            }

            InventoryRenderUtil.DrawGradeMarker(sprite, GradeFrameTextures, slotData.GradeFrameIndex, slotX, slotY);

            if (slotData.IsActiveBullet && ActiveIconTexture != null)
            {
                sprite.Draw(ActiveIconTexture, new Vector2(slotX - 2, slotY - 2), Color.White);
            }

            if (slotData.IsDisabled)
            {
                DrawDisabledOverlay(sprite, slotX, slotY);
            }

            if (_font != null && slotData.Quantity > 1)
            {
                InventoryRenderUtil.DrawSlotQuantity(sprite, _font, slotData.Quantity, slotX, slotY, SLOT_SIZE);
            }
        }

        protected void DrawDisabledOverlay(SpriteBatch sprite, int slotX, int slotY)
        {
            if (DisabledSlotTexture == null)
            {
                return;
            }

            sprite.Draw(DisabledSlotTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), Color.White);
        }
        #endregion

        #region Inventory Management
        protected InventoryType GetInventoryTypeFromTab(int tab)
        {
            return tab switch
            {
                TAB_EQUIP => InventoryType.EQUIP,
                TAB_USE => InventoryType.USE,
                TAB_SETUP => InventoryType.SETUP,
                TAB_ETC => InventoryType.ETC,
                TAB_CASH => InventoryType.CASH,
                _ => InventoryType.NONE
            };
        }

        protected bool TryGetSlotsForType(InventoryType type, out List<InventorySlotData> slots)
        {
            return _inventoryData.TryGetValue(type, out slots);
        }

        protected virtual bool TryGetSlotAtPosition(int mouseX, int mouseY, out InventoryType inventoryType, out int slotIndex)
        {
            inventoryType = GetInventoryTypeFromTab(_currentTab);
            return TryResolveSlotAtPosition(
                mouseX,
                mouseY,
                Position.X + SLOT_ORIGIN_X,
                Position.Y + SLOT_ORIGIN_Y,
                SLOTS_PER_ROW,
                VISIBLE_ROWS,
                _scrollOffset,
                out slotIndex);
        }

        protected bool TryResolveSlotAtPosition(
            int mouseX,
            int mouseY,
            int originX,
            int originY,
            int slotsPerRow,
            int visibleRows,
            int rowOffset,
            out int slotIndex)
        {
            slotIndex = -1;

            if (mouseX < originX || mouseY < originY)
            {
                return false;
            }

            int relativeX = mouseX - originX;
            int relativeY = mouseY - originY;
            int column = relativeX / SLOT_PITCH;
            int row = relativeY / SLOT_PITCH;
            if (column < 0 || column >= slotsPerRow || row < 0 || row >= visibleRows)
            {
                return false;
            }

            if ((relativeX % SLOT_PITCH) >= SLOT_SIZE || (relativeY % SLOT_PITCH) >= SLOT_SIZE)
            {
                return false;
            }

            slotIndex = (rowOffset * SLOTS_PER_ROW) + (row * slotsPerRow) + column;
            return true;
        }

        public int GetSlotLimit(InventoryType type)
        {
            return _inventorySlotLimits.TryGetValue(type, out int value)
                ? Math.Clamp(value, TOTAL_SLOTS, MAX_SLOT_LIMIT)
                : TOTAL_SLOTS;
        }

        public void SetSlotLimit(InventoryType type, int slotLimit)
        {
            if (type == InventoryType.NONE)
            {
                return;
            }

            _inventorySlotLimits[type] = Math.Clamp(slotLimit, TOTAL_SLOTS, MAX_SLOT_LIMIT);
        }

        public bool CanExpandSlotLimit(InventoryType type, int amount = SLOT_EXPANSION_STEP)
        {
            if (type == InventoryType.NONE || amount <= 0)
            {
                return false;
            }

            int normalizedAmount = NormalizeSlotExpansionAmount(amount);
            return GetSlotLimit(type) + normalizedAmount <= MAX_SLOT_LIMIT;
        }

        public bool TryExpandSlotLimit(InventoryType type, int amount = SLOT_EXPANSION_STEP)
        {
            if (!CanExpandSlotLimit(type, amount))
            {
                return false;
            }

            SetSlotLimit(type, GetSlotLimit(type) + NormalizeSlotExpansionAmount(amount));
            return true;
        }

        public void SetItemConsumptionGuard(Func<InventoryType, int, int, string> itemConsumptionGuard)
        {
            _itemConsumptionGuard = itemConsumptionGuard;
        }

        public void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1)
        {
            AddItem(type, new InventorySlotData
            {
                ItemId = itemId,
                ItemTexture = texture,
                Quantity = Math.Max(1, quantity),
                MaxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type),
                ItemName = ResolveItemName(itemId),
                ItemTypeName = ResolveItemTypeName(type, itemId),
                Description = ResolveItemDescription(itemId)
            });
        }

        public void AddItem(InventoryType type, InventorySlotData slotData)
        {
            if (slotData == null || !_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return;
            }

            if (!slotData.PreferredInventoryType.HasValue || slotData.PreferredInventoryType.Value == InventoryType.NONE)
            {
                slotData.PreferredInventoryType = type;
            }

            if (string.IsNullOrWhiteSpace(slotData.ItemName))
            {
                slotData.ItemName = ResolveItemName(slotData.ItemId);
            }

            if (string.IsNullOrWhiteSpace(slotData.ItemTypeName))
            {
                slotData.ItemTypeName = ResolveItemTypeName(type, slotData.ItemId);
            }

            if (string.IsNullOrWhiteSpace(slotData.Description))
            {
                slotData.Description = ResolveItemDescription(slotData.ItemId);
            }

            int remainingQuantity = Math.Max(1, slotData.Quantity);
            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, slotData.MaxStackSize);

            if (IsStackable(type, maxStackSize))
            {
                remainingQuantity = FillExistingStacks(type, slotData, remainingQuantity, maxStackSize);
            }

            while (remainingQuantity > 0 && slots.Count < GetSlotLimit(type))
            {
                int stackQuantity = IsStackable(type, maxStackSize)
                    ? Math.Min(remainingQuantity, maxStackSize)
                    : 1;

                InventorySlotData newSlot = slotData.Clone();
                newSlot.Quantity = stackQuantity;
                newSlot.MaxStackSize = maxStackSize;
                slots.Add(newSlot);
                remainingQuantity -= stackQuantity;
            }
        }

        private int FillExistingStacks(InventoryType type, InventorySlotData incoming, int remainingQuantity, int maxStackSize)
        {
            if (incoming == null || incoming.Quantity <= 0 || !_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return remainingQuantity;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData existing = slots[i];
                if (existing.ItemId != incoming.ItemId || existing.IsDisabled)
                {
                    continue;
                }

                int existingMaxStack = InventoryItemMetadataResolver.ResolveMaxStack(type, existing.MaxStackSize);
                int capacity = existingMaxStack - Math.Max(1, existing.Quantity);
                if (capacity <= 0)
                {
                    continue;
                }

                int quantityToMerge = Math.Min(capacity, remainingQuantity);
                existing.Quantity += quantityToMerge;
                existing.MaxStackSize = existingMaxStack;

                if (incoming.ItemTexture != null)
                {
                    existing.ItemTexture = incoming.ItemTexture;
                }

                existing.ItemName = string.IsNullOrWhiteSpace(existing.ItemName) ? incoming.ItemName : existing.ItemName;
                existing.ItemTypeName = string.IsNullOrWhiteSpace(existing.ItemTypeName) ? incoming.ItemTypeName : existing.ItemTypeName;
                existing.Description = string.IsNullOrWhiteSpace(existing.Description) ? incoming.Description : existing.Description;
                existing.TooltipPart ??= incoming.TooltipPart?.Clone();
                existing.GradeFrameIndex = incoming.GradeFrameIndex ?? existing.GradeFrameIndex;
                existing.IsActiveBullet = incoming.IsActiveBullet || existing.IsActiveBullet;
                remainingQuantity -= quantityToMerge;

                if (remainingQuantity <= 0)
                {
                    break;
                }
            }

            return remainingQuantity;
        }

        private void GatherCurrentTab()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            if (!_inventoryData.TryGetValue(inventoryType, out List<InventorySlotData> slots) || slots.Count == 0)
            {
                return;
            }

            List<InventorySlotData> snapshot = slots
                .Where(slot => slot != null)
                .Select(slot => slot.Clone())
                .ToList();

            slots.Clear();
            foreach (InventorySlotData slot in snapshot)
            {
                AddItem(inventoryType, slot);
            }
        }

        private void SortCurrentTab()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            if (!_inventoryData.TryGetValue(inventoryType, out List<InventorySlotData> slots) || slots.Count <= 1)
            {
                return;
            }

            List<InventorySlotData> ordered = slots
                .Where(slot => slot != null)
                .Select(slot => slot.Clone())
                .OrderBy(slot => slot.IsDisabled)
                .ThenBy(slot => slot.ItemId)
                .ThenBy(slot => slot.ItemName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(slot => slot.Quantity)
                .ToList();

            slots.Clear();
            slots.AddRange(ordered);
        }

        public void ClearInventory()
        {
            foreach (KeyValuePair<InventoryType, List<InventorySlotData>> kvp in _inventoryData)
            {
                kvp.Value.Clear();
            }
        }

        public IReadOnlyList<InventorySlotData> GetSlots(InventoryType type)
        {
            return _inventoryData.TryGetValue(type, out List<InventorySlotData> slots)
                ? new ReadOnlyCollection<InventorySlotData>(slots)
                : Array.Empty<InventorySlotData>();
        }

        public IReadOnlyDictionary<int, int> GetItems(InventoryType type)
        {
            if (!_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return new ReadOnlyDictionary<int, int>(new Dictionary<int, int>());
            }

            Dictionary<int, int> counts = new();
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot == null || slot.ItemId <= 0)
                {
                    continue;
                }

                int quantity = Math.Max(1, slot.Quantity);
                counts[slot.ItemId] = counts.TryGetValue(slot.ItemId, out int existing)
                    ? existing + quantity
                    : quantity;
            }

            return new ReadOnlyDictionary<int, int>(counts);
        }

        public bool TryRemoveSlotAt(InventoryType type, int slotIndex, out InventorySlotData removedSlot)
        {
            removedSlot = null;
            if (!_inventoryData.TryGetValue(type, out List<InventorySlotData> slots) ||
                slotIndex < 0 ||
                slotIndex >= slots.Count)
            {
                return false;
            }

            removedSlot = slots[slotIndex]?.Clone();
            slots.RemoveAt(slotIndex);
            return removedSlot != null;
        }

        public bool TrySetPendingRequestState(InventoryType type, int slotIndex, int requestId, bool isPending)
        {
            if (requestId <= 0
                || !_inventoryData.TryGetValue(type, out List<InventorySlotData> slots)
                || slotIndex < 0
                || slotIndex >= slots.Count
                || slots[slotIndex] == null)
            {
                return false;
            }

            InventorySlotData slot = slots[slotIndex];
            slot.PendingRequestId = isPending ? requestId : 0;
            slot.IsDisabled = isPending;
            return true;
        }

        public bool TryClearPendingRequestState(int requestId)
        {
            if (requestId <= 0)
            {
                return false;
            }

            foreach (KeyValuePair<InventoryType, List<InventorySlotData>> inventoryEntry in _inventoryData)
            {
                List<InventorySlotData> slots = inventoryEntry.Value;
                for (int i = 0; i < slots.Count; i++)
                {
                    InventorySlotData slot = slots[i];
                    if (slot?.PendingRequestId != requestId)
                    {
                        continue;
                    }

                    slot.PendingRequestId = 0;
                    slot.IsDisabled = false;
                    return true;
                }
            }

            return false;
        }

        public bool TryRemovePendingRequestSlot(int requestId, out InventorySlotData removedSlot)
        {
            removedSlot = null;
            if (requestId <= 0)
            {
                return false;
            }

            foreach (KeyValuePair<InventoryType, List<InventorySlotData>> inventoryEntry in _inventoryData)
            {
                List<InventorySlotData> slots = inventoryEntry.Value;
                for (int i = 0; i < slots.Count; i++)
                {
                    InventorySlotData slot = slots[i];
                    if (slot?.PendingRequestId != requestId)
                    {
                        continue;
                    }

                    slot.PendingRequestId = 0;
                    slot.IsDisabled = false;
                    removedSlot = slot.Clone();
                    slots.RemoveAt(i);
                    return removedSlot != null;
                }
            }

            return false;
        }

        public void SortSlots(InventoryType type)
        {
            if (!_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return;
            }

            slots.Sort((left, right) =>
            {
                int leftId = left?.ItemId ?? int.MaxValue;
                int rightId = right?.ItemId ?? int.MaxValue;
                int idComparison = leftId.CompareTo(rightId);
                if (idComparison != 0)
                {
                    return idComparison;
                }

                int leftQuantity = left?.Quantity ?? 0;
                int rightQuantity = right?.Quantity ?? 0;
                return rightQuantity.CompareTo(leftQuantity);
            });
        }

        public int GetItemCount(InventoryType type, int itemId)
        {
            if (!_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot.ItemId == itemId)
                {
                    total += Math.Max(1, slot.Quantity);
                }
            }

            return total;
        }

        public bool CanAcceptItem(InventoryType type, int itemId, int quantity = 1, int? maxStackSize = null)
        {
            if (type == InventoryType.NONE || quantity <= 0 || !_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return false;
            }

            int remainingQuantity = quantity;
            int resolvedMaxStack = InventoryItemMetadataResolver.ResolveMaxStack(type, maxStackSize);
            if (IsStackable(type, resolvedMaxStack))
            {
                for (int i = 0; i < slots.Count && remainingQuantity > 0; i++)
                {
                    InventorySlotData slot = slots[i];
                    if (slot.ItemId != itemId || slot.IsDisabled)
                    {
                        continue;
                    }

                    int maxStackSize_slot = InventoryItemMetadataResolver.ResolveMaxStack(type, slot.MaxStackSize);
                    int capacity = maxStackSize_slot - Math.Max(1, slot.Quantity);
                    if (capacity > 0)
                    {
                        remainingQuantity -= capacity;
                    }
                }
            }

            if (remainingQuantity <= 0)
            {
                return true;
            }

            int freeSlotCount = GetSlotLimit(type) - slots.Count;
            if (freeSlotCount <= 0)
            {
                return false;
            }

            if (!IsStackable(type, resolvedMaxStack))
            {
                return freeSlotCount >= remainingQuantity;
            }

            int neededStacks = (remainingQuantity + resolvedMaxStack - 1) / resolvedMaxStack;
            return freeSlotCount >= neededStacks;
        }

        public bool TryConsumeItem(InventoryType type, int itemId, int quantity)
        {
            if (quantity <= 0)
            {
                return true;
            }

            string restrictionMessage = _itemConsumptionGuard?.Invoke(type, itemId, quantity);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                ItemConsumptionBlocked?.Invoke(restrictionMessage);
                return false;
            }

            if (!_inventoryData.TryGetValue(type, out List<InventorySlotData> slots) || GetItemCount(type, itemId) < quantity)
            {
                return false;
            }

            int remaining = quantity;
            for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                InventorySlotData slot = slots[i];
                if (slot.ItemId != itemId)
                {
                    continue;
                }

                int stackQuantity = Math.Max(1, slot.Quantity);
                if (stackQuantity <= remaining)
                {
                    remaining -= stackQuantity;
                    slots.RemoveAt(i);
                }
                else
                {
                    slot.Quantity = stackQuantity - remaining;
                    remaining = 0;
                }
            }

            return remaining == 0;
        }

        public bool TryConsumeItemAtSlot(InventoryType type, int slotIndex, int itemId, int quantity)
        {
            if (quantity <= 0)
            {
                return true;
            }

            string restrictionMessage = _itemConsumptionGuard?.Invoke(type, itemId, quantity);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                ItemConsumptionBlocked?.Invoke(restrictionMessage);
                return false;
            }

            if (!_inventoryData.TryGetValue(type, out List<InventorySlotData> slots)
                || slotIndex < 0
                || slotIndex >= slots.Count)
            {
                return false;
            }

            InventorySlotData slot = slots[slotIndex];
            if (slot == null
                || slot.IsDisabled
                || slot.ItemId != itemId)
            {
                return false;
            }

            int stackQuantity = Math.Max(1, slot.Quantity);
            if (stackQuantity < quantity)
            {
                return false;
            }

            if (stackQuantity == quantity)
            {
                slots.RemoveAt(slotIndex);
                return true;
            }

            slot.Quantity = stackQuantity - quantity;
            return true;
        }

        public Texture2D GetItemTexture(InventoryType type, int itemId)
        {
            if (!_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return null;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i];
                if (slot.ItemId == itemId && slot.ItemTexture != null)
                {
                    return slot.ItemTexture;
                }
            }

            return null;
        }

        public long GetMesoCount()
        {
            return _mesoCount;
        }

        public void AddMeso(long amount)
        {
            if (amount <= 0)
            {
                return;
            }

            MesoCount += amount;
        }

        public bool TryConsumeMeso(long amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (_mesoCount < amount)
            {
                return false;
            }

            _mesoCount -= amount;
            return true;
        }

        public bool HandlesInventoryInteractionPoint(int mouseX, int mouseY)
        {
            return TryGetSlotAtPosition(mouseX, mouseY, out _, out int slotIndex)
                && slotIndex >= 0;
        }

        public void OnInventoryMouseDown(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);

            if (!TryGetSlotAtPosition(mouseX, mouseY, out InventoryType inventoryType, out int slotIndex)
                || !TryGetSlotsForType(inventoryType, out List<InventorySlotData> slots)
                || slotIndex < 0
                || slotIndex >= slots.Count)
            {
                return;
            }

            InventorySlotData slot = slots[slotIndex];
            if (slot == null || slot.IsDisabled)
            {
                return;
            }

            _isDraggingItem = true;
            _draggedInventoryType = inventoryType;
            _draggedSlotIndex = slotIndex;
            _draggedSlotData = slot.Clone();
            _draggedItemPosition = _lastMousePosition;
            _hoveredInventoryType = InventoryType.NONE;
            _hoveredSlotIndex = -1;
        }

        public void OnInventoryMouseMove(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);

            if (_isDraggingItem)
            {
                _draggedItemPosition = _lastMousePosition;
            }
        }

        public bool OnInventoryMouseUp(int mouseX, int mouseY)
        {
            _lastMousePosition = new Point(mouseX, mouseY);
            _draggedItemPosition = _lastMousePosition;

            bool moved = false;
            if (_isDraggingItem)
            {
                moved = TryMoveSlot(_draggedInventoryType, _draggedSlotIndex, mouseX, mouseY);
            }

            CancelInventoryDrag();
            return moved;
        }

        public void CancelInventoryDrag()
        {
            _isDraggingItem = false;
            _draggedInventoryType = InventoryType.NONE;
            _draggedSlotIndex = -1;
            _draggedSlotData = null;
        }

        public bool TryMoveSlot(InventoryType sourceType, int sourceIndex, int mouseX, int mouseY)
        {
            if (!TryGetSlotAtPosition(mouseX, mouseY, out InventoryType targetType, out int targetIndex))
            {
                return false;
            }

            return TryMoveSlot(sourceType, sourceIndex, targetType, targetIndex);
        }

        public bool TryMoveSlot(InventoryType sourceType, int sourceIndex, InventoryType targetType, int targetIndex)
        {
            if (sourceType == InventoryType.NONE
                || targetType == InventoryType.NONE
                || sourceType != targetType
                || !TryGetSlotsForType(sourceType, out List<InventorySlotData> slots)
                || sourceIndex < 0
                || sourceIndex >= slots.Count
                || targetIndex < 0
                || targetIndex >= GetSlotLimit(targetType))
            {
                return false;
            }

            InventorySlotData sourceSlot = slots[sourceIndex];
            if (sourceSlot == null || sourceSlot.IsDisabled)
            {
                return false;
            }

            if (targetIndex == sourceIndex)
            {
                return true;
            }

            if (targetIndex >= slots.Count)
            {
                InventorySlotData movedSlot = sourceSlot;
                slots.RemoveAt(sourceIndex);
                slots.Add(movedSlot);
                return true;
            }

            InventorySlotData targetSlot = slots[targetIndex];
            if (targetSlot == null)
            {
                InventorySlotData movedSlot = sourceSlot;
                slots.RemoveAt(sourceIndex);
                slots.Insert(Math.Min(targetIndex, slots.Count), movedSlot);
                return true;
            }

            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(sourceType, sourceSlot.MaxStackSize ?? targetSlot.MaxStackSize);
            if (IsStackable(sourceType, maxStackSize)
                && !targetSlot.IsDisabled
                && sourceSlot.ItemId == targetSlot.ItemId)
            {
                int targetQuantity = Math.Max(1, targetSlot.Quantity);
                int sourceQuantity = Math.Max(1, sourceSlot.Quantity);
                int capacity = maxStackSize - targetQuantity;
                if (capacity > 0)
                {
                    int quantityToMove = Math.Min(capacity, sourceQuantity);
                    targetSlot.Quantity = targetQuantity + quantityToMove;
                    sourceQuantity -= quantityToMove;

                    if (sourceQuantity <= 0)
                    {
                        slots.RemoveAt(sourceIndex);
                    }
                    else
                    {
                        sourceSlot.Quantity = sourceQuantity;
                    }

                    targetSlot.MaxStackSize = maxStackSize;
                    sourceSlot.MaxStackSize = maxStackSize;
                    return true;
                }
            }

            slots[sourceIndex] = targetSlot;
            slots[targetIndex] = sourceSlot;
            return true;
        }

        public void ScrollUp()
        {
            if (_scrollOffset > 0)
            {
                _scrollOffset--;
            }
        }

        public void ScrollDown()
        {
            InventoryType invType = GetInventoryTypeFromTab(_currentTab);
            if (!_inventoryData.TryGetValue(invType, out List<InventorySlotData> slots))
            {
                return;
            }

            int visibleRows = VISIBLE_ROWS;
            int maxRows = Math.Max(visibleRows, (int)Math.Ceiling(GetSlotLimit(invType) / (float)SLOTS_PER_ROW));
            int maxScroll = Math.Max(0, maxRows - visibleRows);
            if (_scrollOffset < maxScroll)
            {
                _scrollOffset++;
            }
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateTabStates();

            MouseState mouseState = Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);

            if (TryGetSlotAtPosition(mouseState.X, mouseState.Y, out InventoryType inventoryType, out int slotIndex))
            {
                _hoveredInventoryType = inventoryType;
                _hoveredSlotIndex = slotIndex;
            }
            else
            {
                _hoveredInventoryType = InventoryType.NONE;
                _hoveredSlotIndex = -1;
            }
        }

        private static bool IsStackable(InventoryType type, int maxStackSize)
        {
            return type != InventoryType.EQUIP && maxStackSize > 1;
        }

        private static int NormalizeSlotExpansionAmount(int amount)
        {
            int normalized = Math.Max(SLOT_EXPANSION_STEP, amount);
            int remainder = normalized % SLOT_EXPANSION_STEP;
            return remainder == 0
                ? normalized
                : normalized + (SLOT_EXPANSION_STEP - remainder);
        }
        #endregion

        #region Interaction
        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                _previousInteractionMouseState = mouseState;
                return false;
            }

            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            foreach (UIObject uiBtn in uiButtons)
            {
                bool handled = uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, Position.X, Position.Y, mouseState);
                if (handled)
                {
                    _previousInteractionMouseState = mouseState;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }

            if (TryGetSlotAtPosition(mouseState.X, mouseState.Y, out InventoryType inventoryType, out int slotIndex))
            {
                _hoveredInventoryType = inventoryType;
                _hoveredSlotIndex = slotIndex;
                TryRequestItemUpgrade(mouseState, inventoryType, slotIndex);
                _previousInteractionMouseState = mouseState;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            bool baseHandled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousInteractionMouseState = mouseState;
            return baseHandled;
        }

        private void TryRequestItemUpgrade(MouseState mouseState, InventoryType inventoryType, int slotIndex)
        {
            bool rightJustPressed = mouseState.RightButton == ButtonState.Pressed &&
                                    _previousInteractionMouseState.RightButton == ButtonState.Released;
            if (!rightJustPressed
                || !TryGetSlotsForType(inventoryType, out List<InventorySlotData> slots)
                || slotIndex < 0
                || slotIndex >= slots.Count)
            {
                return;
            }

            InventorySlotData slot = slots[slotIndex];
            if (slot == null)
            {
                return;
            }

            if (ItemUpgradeUI.IsSupportedConsumable(slot.ItemId))
            {
                ItemUpgradeRequested?.Invoke(slot.ItemId);
                return;
            }

            ItemUseRequested?.Invoke(slot.ItemId, inventoryType);
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            base.DrawOverlay(sprite, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);
            DrawHoveredSlotTooltip(sprite);
            DrawDraggedItemOverlay(sprite);
        }

        private void DrawHoveredSlotTooltip(SpriteBatch sprite)
        {
            if (_isDraggingItem
                || _font == null
                || _hoveredInventoryType == InventoryType.NONE
                || _hoveredSlotIndex < 0
                || !TryGetSlotsForType(_hoveredInventoryType, out List<InventorySlotData> slots)
                || _hoveredSlotIndex >= slots.Count)
            {
                return;
            }

            InventorySlotData slot = slots[_hoveredSlotIndex];
            if (slot == null)
            {
                return;
            }

            if (_hoveredInventoryType == InventoryType.EQUIP
                && TryResolveEquipTooltipPart(slot, out CharacterPart tooltipPart)
                && _equipTooltipAssets != null)
            {
                DrawEquipTooltip(sprite, slot, tooltipPart);
                return;
            }

            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(slot.ItemId, _hoveredInventoryType);
            string title = ResolveDisplayText(slot.ItemName, metadata.ItemName);
            string typeLine = ResolveDisplayText(slot.ItemTypeName, ResolveDisplayText(metadata.TypeName, _hoveredInventoryType.ToString()));
            string quantityLine = slot.Quantity > 1 ? $"Quantity: {slot.Quantity}" : string.Empty;
            string stackLine = slot.MaxStackSize.GetValueOrDefault(1) > 1 ? $"Stack Max: {slot.MaxStackSize.Value}" : string.Empty;
            string description = ResolveDisplayText(slot.Description, metadata.Description);
            Texture2D cashLabelTexture = metadata.IsCashItem ? _equipTooltipAssets?.CashLabel : null;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + TOOLTIP_ICON_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;

            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);
            List<TooltipSection> sections = new();
            if (!string.IsNullOrWhiteSpace(typeLine))
            {
                sections.Add(new TooltipSection(typeLine, new Color(180, 220, 255)));
            }

            for (int i = 0; i < metadata.EffectLines.Count; i++)
            {
                sections.Add(new TooltipSection(metadata.EffectLines[i], new Color(180, 255, 210)));
            }

            if (!string.IsNullOrWhiteSpace(quantityLine))
            {
                sections.Add(new TooltipSection(quantityLine, Color.White));
            }

            if (!string.IsNullOrWhiteSpace(stackLine))
            {
                sections.Add(new TooltipSection(stackLine, new Color(180, 255, 210)));
            }

            for (int i = 0; i < metadata.MetadataLines.Count; i++)
            {
                sections.Add(new TooltipSection(metadata.MetadataLines[i], new Color(255, 214, 156)));
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                sections.Add(new TooltipSection(description, new Color(255, 238, 196)));
            }

            List<(string[] Lines, Color Color, float Height)> wrappedSections = BuildWrappedTooltipSections(sections);
            float wrappedSectionHeight = MeasureWrappedSectionHeight(wrappedSections);
            float cashLabelHeight = cashLabelTexture?.Height ?? 0f;
            float contentHeight = wrappedSectionHeight;
            if (cashLabelHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? 2f : 0f) + cashLabelHeight;
            }

            float iconBlockHeight = Math.Max(TOOLTIP_ICON_SIZE, contentHeight);
            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_SECTION_GAP + iconBlockHeight);

            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            int tooltipX = _lastMousePosition.X + TOOLTIP_OFFSET_X;
            int tooltipY = _lastMousePosition.Y + 20;
            int tooltipFrameIndex = 1;

            if (tooltipX + tooltipWidth > viewportWidth - TOOLTIP_PADDING)
            {
                tooltipX = _lastMousePosition.X - tooltipWidth - TOOLTIP_OFFSET_X;
                tooltipFrameIndex = 0;
            }

            if (tooltipX < TOOLTIP_PADDING)
            {
                tooltipX = TOOLTIP_PADDING;
            }

            if (tooltipY + tooltipHeight > viewportHeight - TOOLTIP_PADDING)
            {
                tooltipY = Math.Max(TOOLTIP_PADDING, _lastMousePosition.Y - tooltipHeight + TOOLTIP_OFFSET_Y);
                tooltipFrameIndex = 2;
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int titleX = tooltipX + TOOLTIP_PADDING;
            int titleY = tooltipY + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = tooltipY + TOOLTIP_PADDING + (int)Math.Ceiling(titleHeight) + TOOLTIP_SECTION_GAP;
            if (slot.ItemTexture != null)
            {
                sprite.Draw(slot.ItemTexture, new Rectangle(tooltipX + TOOLTIP_PADDING, contentY, TOOLTIP_ICON_SIZE, TOOLTIP_ICON_SIZE), Color.White);
            }

            int textX = tooltipX + textLeftOffset;
            float sectionY = contentY;
            if (cashLabelHeight > 0f)
            {
                sprite.Draw(cashLabelTexture, new Vector2(textX, sectionY), Color.White);
                sectionY += cashLabelHeight;
            }

            if (wrappedSectionHeight > 0f)
            {
                if (cashLabelHeight > 0f)
                {
                    sectionY += 2f;
                }

                DrawWrappedSections(sprite, textX, sectionY, wrappedSections);
            }
        }

        private bool TryResolveEquipTooltipPart(InventorySlotData slot, out CharacterPart tooltipPart)
        {
            tooltipPart = slot?.TooltipPart;
            if (tooltipPart != null)
            {
                return true;
            }

            if (slot == null || slot.ItemId <= 0)
            {
                return false;
            }

            tooltipPart = _characterLoader?.LoadEquipment(slot.ItemId);
            if (tooltipPart == null)
            {
                _companionTooltipLoader ??= new CompanionEquipmentLoader(_graphicsDevice);
                CompanionEquipItem companionItem = _companionTooltipLoader.LoadCompanionEquipment(slot.ItemId);
                tooltipPart = CompanionEquipmentTooltipPartFactory.CreateTooltipPart(companionItem);
            }

            if (tooltipPart == null)
            {
                return false;
            }

            slot.TooltipPart = tooltipPart;
            return true;
        }

        private void DrawEquipTooltip(SpriteBatch sprite, InventorySlotData slot, CharacterPart part)
        {
            string title = ResolveDisplayText(slot.ItemName, ResolveDisplayText(part.Name, $"Equip {slot.ItemId}"));
            string description = ResolveDisplayText(slot.Description, ResolveDisplayText(part.Description, string.Empty));
            Texture2D itemTexture = slot.ItemTexture;
            IDXObject itemIcon = part.IconRaw ?? part.Icon;

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + TOOLTIP_ICON_SIZE + TOOLTIP_ICON_GAP;
            int contentWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            int sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, contentWidth);
            float titleHeight = MeasureLinesHeight(wrappedTitle);

            Texture2D categoryTexture = ResolveCategoryTexture(part);
            string categoryFallback = categoryTexture == null ? ResolveCategoryFallbackText(part) : string.Empty;
            string[] wrappedCategory = WrapTooltipText(categoryFallback, sectionWidth);
            float categoryHeight = categoryTexture?.Height ?? MeasureLinesHeight(wrappedCategory);
            Texture2D cashLabelTexture = part.IsCash ? _equipTooltipAssets.CashLabel : null;
            float cashLabelHeight = cashLabelTexture?.Height ?? 0f;
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);

            float topTextHeight = categoryHeight;
            if (cashLabelHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? 2f : 0f) + cashLabelHeight;
            }

            if (descriptionHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + descriptionHeight;
            }

            float topBlockHeight = Math.Max(TOOLTIP_ICON_SIZE, topTextHeight);
            List<TooltipLabeledValueRow> statRows = BuildTooltipStatRows(part);
            List<TooltipLabeledValueRow> requirementRows = BuildTooltipRequirementRows(part);
            List<Texture2D> jobBadges = BuildTooltipJobBadges(part.RequiredJobMask);
            List<(string[] Lines, Color Color, float Height)> wrappedFooters = BuildWrappedTooltipSections(
                BuildTooltipFooterSections(part, slot.Quantity, slot.MaxStackSize));

            float contentHeight = topBlockHeight;
            float statHeight = MeasureLabeledValueRowsHeight(statRows);
            float requirementHeight = MeasureLabeledValueRowsHeight(requirementRows);
            float jobBadgeHeight = jobBadges.Count > 0 ? 13f : 0f;
            float footerHeight = MeasureWrappedSectionHeight(wrappedFooters);

            if (statHeight > 0f)
            {
                contentHeight += TOOLTIP_SECTION_GAP + statHeight;
            }

            if (requirementHeight > 0f)
            {
                contentHeight += TOOLTIP_SECTION_GAP + requirementHeight;
            }

            if (jobBadgeHeight > 0f)
            {
                contentHeight += TOOLTIP_SECTION_GAP + jobBadgeHeight;
            }

            if (footerHeight > 0f)
            {
                contentHeight += TOOLTIP_SECTION_GAP + footerHeight;
            }

            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_SECTION_GAP + contentHeight);

            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            int tooltipX = _lastMousePosition.X + TOOLTIP_OFFSET_X;
            int tooltipY = _lastMousePosition.Y + 20;
            int tooltipFrameIndex = 1;

            if (tooltipX + tooltipWidth > viewportWidth - TOOLTIP_PADDING)
            {
                tooltipX = _lastMousePosition.X - tooltipWidth - TOOLTIP_OFFSET_X;
                tooltipFrameIndex = 0;
            }

            if (tooltipX < TOOLTIP_PADDING)
            {
                tooltipX = TOOLTIP_PADDING;
            }

            if (tooltipY + tooltipHeight > viewportHeight - TOOLTIP_PADDING)
            {
                tooltipY = Math.Max(TOOLTIP_PADDING, _lastMousePosition.Y - tooltipHeight + TOOLTIP_OFFSET_Y);
                tooltipFrameIndex = 2;
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int titleX = tooltipX + TOOLTIP_PADDING;
            int titleY = tooltipY + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = tooltipY + TOOLTIP_PADDING + (int)Math.Ceiling(titleHeight) + TOOLTIP_SECTION_GAP;
            int iconX = tooltipX + TOOLTIP_PADDING;
            if (itemIcon != null)
            {
                itemIcon.DrawBackground(sprite, null, null, iconX, contentY, Color.White, false, null);
            }
            else if (itemTexture != null)
            {
                sprite.Draw(itemTexture, new Rectangle(iconX, contentY, TOOLTIP_ICON_SIZE, TOOLTIP_ICON_SIZE), Color.White);
            }

            int textX = tooltipX + textLeftOffset;
            float topY = contentY;
            if (categoryTexture != null)
            {
                sprite.Draw(categoryTexture, new Vector2(textX, topY), Color.White);
                topY += categoryTexture.Height;
            }
            else if (categoryHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedCategory, textX, topY, new Color(181, 224, 255));
                topY += categoryHeight;
            }

            if (cashLabelHeight > 0f)
            {
                if (topY > contentY)
                {
                    topY += 2f;
                }

                sprite.Draw(cashLabelTexture, new Vector2(textX, topY), Color.White);
                topY += cashLabelHeight;
            }

            if (descriptionHeight > 0f)
            {
                if (topY > contentY)
                {
                    topY += TOOLTIP_SECTION_GAP;
                }

                DrawTooltipLines(sprite, wrappedDescription, textX, topY, new Color(216, 216, 216));
            }

            float sectionY = contentY + topBlockHeight;
            if (statHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                sectionY = DrawLabeledValueRows(sprite, tooltipX + TOOLTIP_PADDING, sectionY, statRows);
            }

            if (requirementHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                sectionY = DrawLabeledValueRows(sprite, tooltipX + TOOLTIP_PADDING, sectionY, requirementRows);
            }

            if (jobBadgeHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                sectionY = DrawJobBadgeRow(sprite, tooltipX + TOOLTIP_PADDING, sectionY, jobBadges);
            }

            if (footerHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                DrawWrappedSections(sprite, tooltipX + TOOLTIP_PADDING, sectionY, wrappedFooters);
            }
        }

        private void DrawDraggedItemOverlay(SpriteBatch sprite)
        {
            if (!_isDraggingItem || _draggedSlotData?.ItemTexture == null)
            {
                return;
            }

            int slotX = _draggedItemPosition.X - (SLOT_SIZE / 2);
            int slotY = _draggedItemPosition.Y - (SLOT_SIZE / 2);
            Color tint = Color.White * 0.85f;

            if (SlotShadowTexture != null)
            {
                InventoryRenderUtil.DrawSlotShadow(sprite, SlotShadowTexture, slotX, slotY, SLOT_SIZE);
            }

            sprite.Draw(_draggedSlotData.ItemTexture, new Rectangle(slotX, slotY, SLOT_SIZE, SLOT_SIZE), tint);
            InventoryRenderUtil.DrawGradeMarker(sprite, GradeFrameTextures, _draggedSlotData.GradeFrameIndex, slotX, slotY);

            if (_draggedSlotData.IsActiveBullet && ActiveIconTexture != null)
            {
                sprite.Draw(ActiveIconTexture, new Vector2(slotX - 2, slotY - 2), tint);
            }

            if (_font != null && _draggedSlotData.Quantity > 1)
            {
                InventoryRenderUtil.DrawSlotQuantity(sprite, _font, _draggedSlotData.Quantity, slotX, slotY, SLOT_SIZE);
            }
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

            if (_debugTooltipTexture == null)
            {
                return;
            }

            sprite.Draw(_debugTooltipTexture, rect, new Color(24, 30, 44, 235));
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            if (_debugTooltipTexture == null)
            {
                return;
            }

            Color borderColor = new Color(87, 100, 128);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_debugTooltipTexture, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
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
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (_font == null || lines == null || lines.Length == 0)
            {
                return 0f;
            }

            int nonEmptyLineCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    nonEmptyLineCount++;
                }
            }

            return nonEmptyLineCount > 0 ? nonEmptyLineCount * _font.LineSpacing : 0f;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            List<string> lines = new List<string>();
            string[] paragraphs = text.Replace("\r", string.Empty).Split('\n');
            foreach (string paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    continue;
                }

                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    continue;
                }

                string currentLine = words[0];
                for (int i = 1; i < words.Length; i++)
                {
                    string candidate = currentLine + " " + words[i];
                    if (_font.MeasureString(candidate).X <= maxWidth)
                    {
                        currentLine = candidate;
                    }
                    else
                    {
                        lines.Add(currentLine);
                        currentLine = words[i];
                    }
                }

                lines.Add(currentLine);
            }

            return lines.ToArray();
        }

        private static string BuildEquipmentSummaryLine(CharacterPart part)
        {
            var segments = new List<string>();
            if (part.ItemId > 0)
            {
                segments.Add($"Item ID: {part.ItemId}");
            }

            if (!string.IsNullOrWhiteSpace(part.ItemCategory))
            {
                segments.Add(part.ItemCategory);
            }

            AppendStatSegment(segments, "STR", part.BonusSTR);
            AppendStatSegment(segments, "DEX", part.BonusDEX);
            AppendStatSegment(segments, "INT", part.BonusINT);
            AppendStatSegment(segments, "LUK", part.BonusLUK);
            AppendStatSegment(segments, "HP", part.BonusHP);
            AppendStatSegment(segments, "MP", part.BonusMP);
            AppendStatSegment(segments, "ATT", part.BonusWeaponAttack);
            AppendStatSegment(segments, "M.ATT", part.BonusMagicAttack);
            AppendStatSegment(segments, "DEF", part.BonusWeaponDefense);
            AppendStatSegment(segments, "M.DEF", part.BonusMagicDefense);
            AppendStatSegment(segments, "ACC", part.BonusAccuracy);
            AppendStatSegment(segments, "AVOID", part.BonusAvoidability);
            AppendStatSegment(segments, "Hands", part.BonusHands);
            AppendStatSegment(segments, "Speed", part.BonusSpeed);
            AppendStatSegment(segments, "Jump", part.BonusJump);

            int upgradeSlots = ResolveTooltipUpgradeSlotCount(part);
            if (upgradeSlots > 0)
            {
                if (part.TotalUpgradeSlotCount.HasValue && part.TotalUpgradeSlotCount.Value > 0)
                {
                    segments.Add($"Slots {upgradeSlots}/{part.TotalUpgradeSlotCount.Value}");
                }
                else
                {
                    segments.Add($"Slots {upgradeSlots}");
                }
            }

            if (part is WeaponPart weapon && weapon.AttackSpeed > 0)
            {
                segments.Add($"Speed {weapon.AttackSpeed}");
            }

            return string.Join("  ", segments);
        }

        private static string BuildEquipmentRequirementLine(CharacterPart part)
        {
            var segments = new List<string>();
            if (part.RequiredLevel > 0)
            {
                segments.Add($"Req Lv {part.RequiredLevel}");
            }

            AppendRequirementSegment(segments, "STR", part.RequiredSTR);
            AppendRequirementSegment(segments, "DEX", part.RequiredDEX);
            AppendRequirementSegment(segments, "INT", part.RequiredINT);
            AppendRequirementSegment(segments, "LUK", part.RequiredLUK);

            if (part.RequiredFame > 0)
            {
                segments.Add($"Req Fame {part.RequiredFame}");
            }

            return string.Join("  ", segments);
        }

        private static string BuildDetailedRequirementLine(CharacterPart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            string requiredJobs = ResolveRequiredJobNames(part.RequiredJobMask);
            return string.IsNullOrWhiteSpace(requiredJobs)
                ? string.Empty
                : $"Req Job {requiredJobs}";
        }

        private string BuildEquipmentEligibilityLine(CharacterPart part)
        {
            if (part == null || CharacterBuild == null)
            {
                return string.Empty;
            }

            if (MeetsEquipRequirements(part, CharacterBuild))
            {
                return "Can equip";
            }

            var failures = new List<string>();
            if (part.RequiredLevel > 0 && CharacterBuild.Level < part.RequiredLevel)
            {
                failures.Add($"Lv {part.RequiredLevel}");
            }

            if (part.RequiredSTR > 0 && CharacterBuild.TotalSTR < part.RequiredSTR)
            {
                failures.Add($"STR {part.RequiredSTR}");
            }

            if (part.RequiredDEX > 0 && CharacterBuild.TotalDEX < part.RequiredDEX)
            {
                failures.Add($"DEX {part.RequiredDEX}");
            }

            if (part.RequiredINT > 0 && CharacterBuild.TotalINT < part.RequiredINT)
            {
                failures.Add($"INT {part.RequiredINT}");
            }

            if (part.RequiredLUK > 0 && CharacterBuild.TotalLUK < part.RequiredLUK)
            {
                failures.Add($"LUK {part.RequiredLUK}");
            }

            if (part.RequiredFame > 0 && CharacterBuild.Fame < part.RequiredFame)
            {
                failures.Add($"Fame {part.RequiredFame}");
            }

            if (part.RequiredJobMask != 0 && !MatchesRequiredJobMask(part.RequiredJobMask, CharacterBuild.Job))
            {
                string requiredJobs = ResolveRequiredJobNames(part.RequiredJobMask);
                failures.Add(string.IsNullOrWhiteSpace(requiredJobs) ? "job" : requiredJobs);
            }

            return failures.Count == 0
                ? "Cannot equip"
                : $"Cannot equip: {string.Join(", ", failures)}";
        }

        private static string BuildAdditionalEquipmentMetadataLine(CharacterPart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            if (part.TradeAvailable > 0)
            {
                segments.Add($"Trade available {part.TradeAvailable} time{(part.TradeAvailable == 1 ? string.Empty : "s")}");
            }

            if (part.IsTradeBlocked)
            {
                segments.Add("Untradeable");
            }

            if (part.IsEquipTradeBlocked)
            {
                segments.Add("Untradeable after equip");
            }

            if (part.IsOneOfAKind)
            {
                segments.Add("One-of-a-kind item");
            }

            if (part.IsUniqueEquipItem)
            {
                segments.Add("Can only be equipped once");
            }

            if (part.IsNotForSale)
            {
                segments.Add("Not for sale");
            }

            if (part.IsAccountSharable)
            {
                segments.Add("Account-sharable");
            }

            if (part.HasAccountShareTag)
            {
                segments.Add("Account-share tagged");
            }

            if (part.IsNoMoveToLocker)
            {
                segments.Add("Cannot be moved to storage");
            }

            if (part.KnockbackRate > 0)
            {
                segments.Add($"Knockback resistance {part.KnockbackRate}%");
            }

            if (part.IsTimeLimited)
            {
                segments.Add("Time-limited item");
            }

            if (part.Durability.HasValue)
            {
                if (part.MaxDurability.HasValue && part.MaxDurability.Value > 0)
                {
                    segments.Add($"Durability {Math.Max(0, part.Durability.Value)}/{part.MaxDurability.Value}");
                }
                else
                {
                    segments.Add($"Durability {Math.Max(0, part.Durability.Value)}");
                }
            }

            return string.Join("  ", segments);
        }

        private IReadOnlyList<TooltipSection> BuildTooltipFooterSections(CharacterPart part, int quantity, int? maxStackSize)
        {
            var sections = new List<TooltipSection>();

            string summaryLine = BuildEquipmentSummaryLine(part);
            if (!string.IsNullOrWhiteSpace(summaryLine))
            {
                sections.Add(new TooltipSection(summaryLine, new Color(181, 224, 255)));
            }

            string requirementLine = BuildEquipmentRequirementLine(part);
            if (!string.IsNullOrWhiteSpace(requirementLine))
            {
                sections.Add(new TooltipSection(requirementLine, Color.White));
            }

            string detailedRequirementLine = BuildDetailedRequirementLine(part);
            if (!string.IsNullOrWhiteSpace(detailedRequirementLine))
            {
                sections.Add(new TooltipSection(detailedRequirementLine, new Color(255, 232, 176)));
            }

            string metadataLine = BuildAdditionalEquipmentMetadataLine(part);
            if (!string.IsNullOrWhiteSpace(metadataLine))
            {
                sections.Add(new TooltipSection(metadataLine, new Color(255, 214, 156)));
            }

            AppendPotentialTooltipSections(sections, part);

            string expirationLine = BuildExpirationLine(part);
            if (!string.IsNullOrWhiteSpace(expirationLine))
            {
                sections.Add(new TooltipSection(expirationLine, new Color(255, 214, 156)));
            }

            if (quantity > 1)
            {
                string quantityLine = $"Quantity: {quantity}";
                if (maxStackSize.GetValueOrDefault(1) > 1)
                {
                    quantityLine += $"  Stack Max: {maxStackSize.Value}";
                }

                sections.Add(new TooltipSection(quantityLine, Color.White));
            }

            string eligibilityLine = BuildEquipmentEligibilityLine(part);
            if (!string.IsNullOrWhiteSpace(eligibilityLine))
            {
                sections.Add(new TooltipSection(
                    eligibilityLine,
                    eligibilityLine.StartsWith("Can equip", StringComparison.Ordinal)
                        ? new Color(176, 255, 176)
                        : new Color(255, 186, 186)));
            }

            return sections;
        }

        private List<TooltipLabeledValueRow> BuildTooltipStatRows(CharacterPart part)
        {
            var rows = new List<TooltipLabeledValueRow>();
            AppendStatRow(rows, "STR:", null, part.BonusSTR, new Color(176, 255, 176), true);
            AppendStatRow(rows, "DEX:", null, part.BonusDEX, new Color(176, 255, 176), true);
            AppendStatRow(rows, "INT:", null, part.BonusINT, new Color(176, 255, 176), true);
            AppendStatRow(rows, "LUK:", null, part.BonusLUK, new Color(176, 255, 176), true);
            AppendStatRow(rows, "HP:", null, part.BonusHP, new Color(176, 255, 176), true);
            AppendStatRow(rows, "MP:", null, part.BonusMP, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("6"), part.BonusWeaponAttack, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("7"), part.BonusMagicAttack, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("8"), part.BonusWeaponDefense, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("9"), part.BonusMagicDefense, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("10"), part.BonusAccuracy, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("11"), part.BonusAvoidability, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("12"), part.BonusHands, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("13"), part.BonusSpeed, new Color(176, 255, 176), true);
            AppendStatRow(rows, null, ResolvePropertyLabel("14"), part.BonusJump, new Color(176, 255, 176), true);
            AppendEnhancementStarRow(rows, part.EnhancementStarCount);
            AppendSellPriceRow(rows, part.SellPrice);
            AppendUpgradeSlotRow(rows, part);
            if (part is WeaponPart weapon)
            {
                AppendAttackSpeedRow(rows, weapon.AttackSpeed);
            }

            AppendGrowthRows(rows, part);

            return rows;
        }

        private List<TooltipLabeledValueRow> BuildTooltipRequirementRows(CharacterPart part)
        {
            var rows = new List<TooltipLabeledValueRow>();
            AppendRequirementRow(rows, "reqLEV", part.RequiredLevel, CharacterBuild?.Level ?? int.MaxValue);
            AppendRequirementRow(rows, "reqSTR", part.RequiredSTR, CharacterBuild?.TotalSTR ?? int.MaxValue);
            AppendRequirementRow(rows, "reqDEX", part.RequiredDEX, CharacterBuild?.TotalDEX ?? int.MaxValue);
            AppendRequirementRow(rows, "reqINT", part.RequiredINT, CharacterBuild?.TotalINT ?? int.MaxValue);
            AppendRequirementRow(rows, "reqLUK", part.RequiredLUK, CharacterBuild?.TotalLUK ?? int.MaxValue);
            AppendRequirementRow(rows, "reqPOP", part.RequiredFame, CharacterBuild?.Fame ?? int.MaxValue);
            if (part.Durability.HasValue)
            {
                bool canUse = !part.MaxDurability.HasValue || part.Durability.Value > 0;
                string value = part.MaxDurability.HasValue && part.MaxDurability.Value > 0
                    ? $"{Math.Max(0, part.Durability.Value)}/{part.MaxDurability.Value}"
                    : Math.Max(0, part.Durability.Value).ToString(CultureInfo.InvariantCulture);
                rows.Add(new TooltipLabeledValueRow(
                    ResolveRequirementLabel(canUse, "durability"),
                    "Durability:",
                    value,
                    canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                    BuildTooltipValueSegments(value, canUse, false)));
            }

            return rows;
        }

        private static string ResolveDisplayText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private void AppendStatRow(
            List<TooltipLabeledValueRow> rows,
            string fallbackLabel,
            Texture2D labelTexture,
            int value,
            Color color,
            bool includePlusPrefix)
        {
            if (value <= 0)
            {
                return;
            }

            string valueText = includePlusPrefix ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                labelTexture,
                fallbackLabel,
                valueText,
                color,
                BuildTooltipValueSegments(valueText, true, true)));
        }

        private void AppendUpgradeSlotRow(List<TooltipLabeledValueRow> rows, CharacterPart part)
        {
            int upgradeSlots = ResolveTooltipUpgradeSlotCount(part);
            if (upgradeSlots <= 0)
            {
                return;
            }

            string valueText = part.TotalUpgradeSlotCount.HasValue && part.TotalUpgradeSlotCount.Value > 0
                ? $"{upgradeSlots}/{part.TotalUpgradeSlotCount.Value}"
                : upgradeSlots.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                ResolvePropertyLabel("16"),
                "Upgrades Available:",
                valueText,
                new Color(255, 232, 176),
                BuildTooltipValueSegments(valueText, true, false)));
        }

        private void AppendGrowthRows(List<TooltipLabeledValueRow> rows, CharacterPart part)
        {
            if (!part?.HasGrowthInfo ?? true)
            {
                return;
            }

            int currentLevel = Math.Max(1, part.GrowthLevel);
            int maxLevel = Math.Max(currentLevel, part.GrowthMaxLevel);
            bool growthEnabled = currentLevel < maxLevel;
            rows.Add(new TooltipLabeledValueRow(
                ResolveGrowthLabel(growthEnabled, "itemLEV"),
                "Item Level:",
                currentLevel.ToString(CultureInfo.InvariantCulture),
                growthEnabled ? new Color(181, 224, 255) : new Color(192, 192, 192),
                BuildTooltipValueSegments(currentLevel.ToString(CultureInfo.InvariantCulture), growthEnabled, true)));

            string expValue = growthEnabled
                ? $"{Math.Clamp(part.GrowthExpPercent, 0, 99)}%"
                : "MAX";
            rows.Add(new TooltipLabeledValueRow(
                ResolveGrowthLabel(growthEnabled, "itemEXP"),
                "Item EXP:",
                expValue,
                growthEnabled ? new Color(181, 224, 255) : new Color(192, 192, 192),
                BuildTooltipValueSegments(expValue, growthEnabled, true)));
        }

        private void AppendEnhancementStarRow(List<TooltipLabeledValueRow> rows, int enhancementStarCount)
        {
            if (enhancementStarCount <= 0)
            {
                return;
            }

            string valueText = enhancementStarCount.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                _equipTooltipAssets?.StarLabel,
                "Stars:",
                valueText,
                new Color(255, 232, 176),
                BuildTooltipValueSegments(valueText, true, false)));
        }

        private void AppendSellPriceRow(List<TooltipLabeledValueRow> rows, int sellPrice)
        {
            if (sellPrice <= 0)
            {
                return;
            }

            string valueText = sellPrice.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipLabeledValueRow(
                _equipTooltipAssets?.MesosLabel,
                "Mesos:",
                valueText,
                new Color(255, 244, 186),
                BuildTooltipValueSegments(valueText, true, false)));
        }

        private void AppendAttackSpeedRow(List<TooltipLabeledValueRow> rows, int attackSpeed)
        {
            if (attackSpeed < 0)
            {
                return;
            }

            Texture2D speedTexture = ResolveSpeedTexture(attackSpeed);
            rows.Add(new TooltipLabeledValueRow(
                ResolvePropertyLabel("4"),
                "Attack Speed:",
                ResolveAttackSpeedText(attackSpeed),
                new Color(181, 224, 255),
                speedTexture != null ? new[] { new TooltipValueSegment(speedTexture) } : null));
        }

        private void AppendRequirementRow(List<TooltipLabeledValueRow> rows, string labelKey, int requiredValue, int actualValue)
        {
            if (requiredValue <= 0)
            {
                return;
            }

            bool canUse = actualValue >= requiredValue;
            rows.Add(new TooltipLabeledValueRow(
                ResolveRequirementLabel(canUse, labelKey),
                labelKey + ":",
                requiredValue.ToString(CultureInfo.InvariantCulture),
                canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                BuildTooltipValueSegments(requiredValue.ToString(CultureInfo.InvariantCulture), canUse, false)));
        }

        private IReadOnlyList<TooltipValueSegment> BuildTooltipValueSegments(string valueText, bool enabled, bool preferGrowthDigits)
        {
            if (string.IsNullOrWhiteSpace(valueText) || _equipTooltipAssets == null)
            {
                return null;
            }

            IReadOnlyDictionary<string, Texture2D> source = preferGrowthDigits
                ? (enabled ? _equipTooltipAssets.GrowthEnabledLabels : _equipTooltipAssets.GrowthDisabledLabels)
                : (enabled ? _equipTooltipAssets.CanLabels : _equipTooltipAssets.CannotLabels);
            if (source == null)
            {
                return null;
            }

            var segments = new List<TooltipValueSegment>(valueText.Length);
            if (string.Equals(valueText, "MAX", StringComparison.OrdinalIgnoreCase))
            {
                Texture2D maxTexture = TryResolveTooltipAsset(source, "max");
                return maxTexture == null ? null : new[] { new TooltipValueSegment(maxTexture) };
            }

            for (int i = 0; i < valueText.Length; i++)
            {
                char character = valueText[i];
                if (character == '+')
                {
                    continue;
                }

                string key = character switch
                {
                    '%' => "percent",
                    _ => char.IsDigit(character) ? character.ToString() : null
                };
                if (key == null)
                {
                    if (character == '/' || character == '-' || character == '.' || character == ',')
                    {
                        segments.Add(new TooltipValueSegment(null, character.ToString()));
                        continue;
                    }

                    return null;
                }

                Texture2D texture = TryResolveTooltipAsset(source, key);
                if (texture == null)
                {
                    return null;
                }

                segments.Add(new TooltipValueSegment(texture));
            }

            return segments.Count == 0 ? null : segments;
        }

        private float MeasureTooltipValueSegmentsHeight(IReadOnlyList<TooltipValueSegment> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0f;
            }

            int height = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Texture != null)
                {
                    height = Math.Max(height, segments[i].Texture.Height);
                }
                else if (!string.IsNullOrEmpty(segments[i].Text) && _font != null)
                {
                    height = Math.Max(height, _font.LineSpacing);
                }
            }

            return height;
        }

        private void DrawTooltipValueSegments(SpriteBatch sprite, IReadOnlyList<TooltipValueSegment> segments, int x, float y, Color color)
        {
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            int drawX = x;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Texture != null)
                {
                    sprite.Draw(segments[i].Texture, new Vector2(drawX, y), Color.White);
                    drawX += segments[i].Texture.Width + TOOLTIP_BITMAP_GAP;
                }
                else if (!string.IsNullOrEmpty(segments[i].Text))
                {
                    DrawTooltipText(sprite, segments[i].Text, new Vector2(drawX, y), color);
                    drawX += (int)Math.Ceiling(_font.MeasureString(segments[i].Text).X) + TOOLTIP_BITMAP_GAP;
                }
            }
        }

        private float MeasureLabeledValueRowsHeight(IReadOnlyList<TooltipLabeledValueRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                height += MeasureLabeledValueRowHeight(rows[i]);
                if (i < rows.Count - 1)
                {
                    height += 2f;
                }
            }

            return height;
        }

        private float MeasureLabeledValueRowHeight(TooltipLabeledValueRow row)
        {
            float labelHeight = row.LabelTexture?.Height ?? (_font?.LineSpacing ?? 0);
            float valueHeight = MeasureTooltipValueSegmentsHeight(row.ValueSegments);
            return Math.Max(labelHeight, Math.Max(valueHeight, _font?.LineSpacing ?? 0));
        }

        private float DrawLabeledValueRows(SpriteBatch sprite, int x, float y, IReadOnlyList<TooltipLabeledValueRow> rows)
        {
            if (rows == null)
            {
                return y;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                y = DrawLabeledValueRow(sprite, x, y, rows[i]);
                if (i < rows.Count - 1)
                {
                    y += 2f;
                }
            }

            return y;
        }

        private float DrawLabeledValueRow(SpriteBatch sprite, int x, float y, TooltipLabeledValueRow row)
        {
            int valueX = x;
            if (row.LabelTexture != null)
            {
                sprite.Draw(row.LabelTexture, new Vector2(x, y), Color.White);
                valueX = x + row.LabelTexture.Width + 6;
            }
            else if (!string.IsNullOrWhiteSpace(row.FallbackLabel))
            {
                DrawTooltipText(sprite, row.FallbackLabel, new Vector2(x, y), new Color(181, 224, 255));
                valueX = x + (int)Math.Ceiling(_font.MeasureString(row.FallbackLabel).X) + 6;
            }

            if (row.ValueSegments != null && row.ValueSegments.Count > 0)
            {
                DrawTooltipValueSegments(sprite, row.ValueSegments, valueX, y, row.ValueColor);
            }
            else if (!string.IsNullOrWhiteSpace(row.ValueText))
            {
                DrawTooltipText(sprite, row.ValueText, new Vector2(valueX, y), row.ValueColor);
            }

            return y + MeasureLabeledValueRowHeight(row);
        }

        private List<Texture2D> BuildTooltipJobBadges(int requiredJobMask)
        {
            var textures = new List<Texture2D>(6);
            AppendJobBadgeTexture(textures, requiredJobMask, 1, "beginner");
            AppendJobBadgeTexture(textures, requiredJobMask, 2, "warrior");
            AppendJobBadgeTexture(textures, requiredJobMask, 4, "magician");
            AppendJobBadgeTexture(textures, requiredJobMask, 8, "bowman");
            AppendJobBadgeTexture(textures, requiredJobMask, 16, "thief");
            AppendJobBadgeTexture(textures, requiredJobMask, 32, "pirate");
            return textures;
        }

        private void AppendJobBadgeTexture(List<Texture2D> textures, int requiredJobMask, int maskBit, string key)
        {
            if ((requiredJobMask & maskBit) == 0)
            {
                return;
            }

            Texture2D texture = ResolveRequirementLabel(true, key);
            if (texture != null)
            {
                textures.Add(texture);
            }
        }

        private float DrawJobBadgeRow(SpriteBatch sprite, int x, float y, IReadOnlyList<Texture2D> textures)
        {
            int drawX = x;
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(drawX, y), Color.White);
                drawX += texture.Width + 4;
            }

            return y + 13f;
        }

        private List<(string[] Lines, Color Color, float Height)> BuildWrappedTooltipSections(IReadOnlyList<TooltipSection> sections)
        {
            var wrappedSections = new List<(string[] Lines, Color Color, float Height)>();
            if (sections == null)
            {
                return wrappedSections;
            }

            int tooltipWidth = ResolveTooltipWidth();
            int contentWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            for (int i = 0; i < sections.Count; i++)
            {
                string[] lines = WrapTooltipText(sections[i].Text, contentWidth);
                wrappedSections.Add((lines, sections[i].Color, MeasureLinesHeight(lines)));
            }

            return wrappedSections;
        }

        private float MeasureWrappedSectionHeight(IReadOnlyList<(string[] Lines, Color Color, float Height)> sections)
        {
            if (sections == null || sections.Count == 0)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].Height <= 0f)
                {
                    continue;
                }

                if (height > 0f)
                {
                    height += TOOLTIP_SECTION_GAP;
                }

                height += sections[i].Height;
            }

            return height;
        }

        private void DrawWrappedSections(SpriteBatch sprite, int x, float y, IReadOnlyList<(string[] Lines, Color Color, float Height)> sections)
        {
            if (sections == null)
            {
                return;
            }

            float sectionY = y;
            for (int i = 0; i < sections.Count; i++)
            {
                (string[] lines, Color color, float height) = sections[i];
                if (height <= 0f)
                {
                    continue;
                }

                if (sectionY > y)
                {
                    sectionY += TOOLTIP_SECTION_GAP;
                }

                DrawTooltipLines(sprite, lines, x, sectionY, color);
                sectionY += height;
            }
        }

        private static string ResolveItemName(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? itemInfo.Item2
                : $"Item #{itemId}";
        }

        private static string ResolveItemTypeName(InventoryType type, int itemId)
        {
            if (global::HaCreator.Program.InfoManager?.ItemNameCache != null
                && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                && !string.IsNullOrWhiteSpace(itemInfo?.Item1))
            {
                return itemInfo.Item1;
            }

            return type.ToString();
        }

        private static string ResolveItemDescription(int itemId)
        {
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item3)
                ? itemInfo.Item3
                : string.Empty;
        }

        private Texture2D ResolveRequirementLabel(bool canUse, string key)
        {
            if (_equipTooltipAssets == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            IReadOnlyDictionary<string, Texture2D> source = canUse
                ? _equipTooltipAssets.CanLabels
                : _equipTooltipAssets.CannotLabels;
            return TryResolveTooltipAsset(source, key);
        }

        private Texture2D ResolvePropertyLabel(string key)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.PropertyLabels, key);
        }

        private Texture2D ResolveGrowthLabel(bool enabled, string key)
        {
            IReadOnlyDictionary<string, Texture2D> source = enabled
                ? _equipTooltipAssets?.GrowthEnabledLabels
                : _equipTooltipAssets?.GrowthDisabledLabels;
            return TryResolveTooltipAsset(source, key);
        }

        private Texture2D ResolveSpeedTexture(int attackSpeed)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.SpeedLabels, Math.Clamp(attackSpeed, 0, 6).ToString(CultureInfo.InvariantCulture));
        }

        private Texture2D ResolveCategoryTexture(CharacterPart part)
        {
            if (_equipTooltipAssets == null || part == null || part.ItemId <= 0)
            {
                return null;
            }

            int itemCategory = part.ItemId / 10000;
            if (part is WeaponPart)
            {
                Texture2D weaponTexture = TryResolveTooltipAsset(
                    _equipTooltipAssets.WeaponCategoryLabels,
                    (itemCategory - 100).ToString(CultureInfo.InvariantCulture));
                if (weaponTexture != null)
                {
                    return weaponTexture;
                }
            }

            string categoryKey = itemCategory switch
            {
                100 => "1",
                101 => "2",
                102 => "3",
                103 => "4",
                104 => "5",
                105 => "21",
                106 => "6",
                107 => "7",
                108 => "8",
                109 => "10",
                110 => "9",
                111 => "12",
                _ => null
            };

            return categoryKey == null
                ? null
                : TryResolveTooltipAsset(_equipTooltipAssets.ItemCategoryLabels, categoryKey);
        }

        private static Texture2D TryResolveTooltipAsset(IReadOnlyDictionary<string, Texture2D> assets, string key)
        {
            if (assets == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return assets.TryGetValue(key, out Texture2D texture) ? texture : null;
        }

        private static string ResolveCategoryFallbackText(CharacterPart part)
        {
            if (part is WeaponPart weapon && !string.IsNullOrWhiteSpace(weapon.WeaponType))
            {
                return weapon.WeaponType;
            }

            return part?.ItemCategory ?? string.Empty;
        }

        private static string ResolveAttackSpeedText(int attackSpeed)
        {
            return Math.Clamp(attackSpeed, 0, 6) switch
            {
                0 => "Fastest",
                1 => "Faster",
                2 => "Fast",
                3 => "Normal",
                4 => "Slow",
                5 => "Slower",
                6 => "Slowest",
                _ => string.Empty
            };
        }

        private static bool MeetsEquipRequirements(CharacterPart part, CharacterBuild build)
        {
            if (part == null || build == null)
            {
                return true;
            }

            return (part.RequiredLevel <= 0 || build.Level >= part.RequiredLevel)
                   && (part.RequiredSTR <= 0 || build.TotalSTR >= part.RequiredSTR)
                   && (part.RequiredDEX <= 0 || build.TotalDEX >= part.RequiredDEX)
                   && (part.RequiredINT <= 0 || build.TotalINT >= part.RequiredINT)
                   && (part.RequiredLUK <= 0 || build.TotalLUK >= part.RequiredLUK)
                   && (part.RequiredFame <= 0 || build.Fame >= part.RequiredFame)
                   && (part.RequiredJobMask == 0 || MatchesRequiredJobMask(part.RequiredJobMask, build.Job));
        }

        private static int ResolveTooltipUpgradeSlotCount(CharacterPart part)
        {
            if (part == null)
            {
                return 0;
            }

            if (part.RemainingUpgradeSlotCount.HasValue)
            {
                return Math.Max(0, part.RemainingUpgradeSlotCount.Value);
            }

            return Math.Max(0, part.UpgradeSlots);
        }

        private static void AppendPotentialTooltipSections(List<TooltipSection> sections, CharacterPart part)
        {
            if (sections == null || part == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(part.PotentialTierText))
            {
                sections.Add(new TooltipSection(part.PotentialTierText, new Color(214, 190, 255)));
            }

            if (part.PotentialLines == null)
            {
                return;
            }

            for (int i = 0; i < part.PotentialLines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(part.PotentialLines[i]))
                {
                    sections.Add(new TooltipSection(part.PotentialLines[i], new Color(236, 224, 255)));
                }
            }
        }

        private static string BuildExpirationLine(CharacterPart part)
        {
            if (!part?.ExpirationDateUtc.HasValue ?? true)
            {
                return string.Empty;
            }

            return $"Expires {part.ExpirationDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}";
        }

        private static bool MatchesRequiredJobMask(int requiredJobMask, int jobId)
        {
            if (requiredJobMask == 0)
            {
                return true;
            }

            int jobGroup = Math.Abs(jobId) / 100;
            return jobGroup switch
            {
                0 => (requiredJobMask & 1) != 0,
                1 => (requiredJobMask & 2) != 0,
                2 => (requiredJobMask & 4) != 0,
                3 => (requiredJobMask & 8) != 0,
                4 => (requiredJobMask & 16) != 0,
                5 => (requiredJobMask & 32) != 0,
                _ => false
            };
        }

        private static string ResolveRequiredJobNames(int requiredJobMask)
        {
            if (requiredJobMask == 0)
            {
                return string.Empty;
            }

            var jobNames = new List<string>();
            AppendRequiredJobName(jobNames, requiredJobMask, 1, "Beginner");
            AppendRequiredJobName(jobNames, requiredJobMask, 2, "Warrior");
            AppendRequiredJobName(jobNames, requiredJobMask, 4, "Magician");
            AppendRequiredJobName(jobNames, requiredJobMask, 8, "Bowman");
            AppendRequiredJobName(jobNames, requiredJobMask, 16, "Thief");
            AppendRequiredJobName(jobNames, requiredJobMask, 32, "Pirate");
            return string.Join("/", jobNames);
        }

        private static void AppendRequiredJobName(List<string> jobNames, int requiredJobMask, int maskBit, string jobName)
        {
            if ((requiredJobMask & maskBit) != 0)
            {
                jobNames.Add(jobName);
            }
        }

        private static void AppendStatSegment(List<string> segments, string label, int value)
        {
            if (value > 0)
            {
                segments.Add($"{label} +{value}");
            }
        }

        private static void AppendRequirementSegment(List<string> segments, string label, int value)
        {
            if (value > 0)
            {
                segments.Add($"{label} {value}");
            }
        }
        #endregion
    }

    /// <summary>
    /// Data for a single inventory slot.
    /// </summary>
    public class InventorySlotData
    {
        public int ItemId { get; set; }
        public Texture2D ItemTexture { get; set; }
        public int Quantity { get; set; } = 1;
        public int? MaxStackSize { get; set; }
        public InventoryType? PreferredInventoryType { get; set; }
        public bool IsEquipped { get; set; }
        public bool IsDisabled { get; set; }
        public bool IsActiveBullet { get; set; }
        public int? GradeFrameIndex { get; set; }
        public string ItemName { get; set; }
        public string ItemTypeName { get; set; }
        public string Description { get; set; }
        public CharacterPart TooltipPart { get; set; }
        public int PendingRequestId { get; set; }

        public InventorySlotData Clone()
        {
            return new InventorySlotData
            {
                ItemId = ItemId,
                ItemTexture = ItemTexture,
                Quantity = Quantity,
                MaxStackSize = MaxStackSize,
                PreferredInventoryType = PreferredInventoryType,
                IsEquipped = IsEquipped,
                IsDisabled = IsDisabled,
                IsActiveBullet = IsActiveBullet,
                GradeFrameIndex = GradeFrameIndex,
                ItemName = ItemName,
                ItemTypeName = ItemTypeName,
                Description = Description,
                TooltipPart = TooltipPart?.Clone(),
                PendingRequestId = PendingRequestId
            };
        }
    }

    internal static class InventoryRenderUtil
    {
        private const float SlotQuantityScale = 0.62f;

        public static void DrawOutlinedText(SpriteBatch sprite, SpriteFont font, string text, Vector2 position, Color textColor, float scale)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            Color outlineColor = Color.Black;
            sprite.DrawString(font, text, position + new Vector2(-1f, 0f), outlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(font, text, position + new Vector2(1f, 0f), outlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(font, text, position + new Vector2(0f, -1f), outlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(font, text, position + new Vector2(0f, 1f), outlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        public static void DrawSlotQuantity(SpriteBatch sprite, SpriteFont font, int quantity, int slotX, int slotY, int slotSize)
        {
            string quantityText = quantity.ToString(CultureInfo.InvariantCulture);
            Vector2 textSize = font.MeasureString(quantityText) * SlotQuantityScale;
            Vector2 position = new Vector2(slotX + slotSize - textSize.X - 1f, slotY + slotSize - textSize.Y - 1f);
            DrawOutlinedText(sprite, font, quantityText, position, Color.White, SlotQuantityScale);
        }

        public static void DrawSlotShadow(SpriteBatch sprite, Texture2D shadowTexture, int slotX, int slotY, int slotSize)
        {
            if (shadowTexture == null)
            {
                return;
            }

            int drawX = slotX + ((slotSize - shadowTexture.Width) / 2);
            int drawY = slotY + slotSize - shadowTexture.Height - 1;
            sprite.Draw(shadowTexture, new Vector2(drawX, drawY), Color.White);
        }

        public static void DrawGradeMarker(SpriteBatch sprite, Texture2D[] gradeTextures, int? gradeFrameIndex, int slotX, int slotY)
        {
            if (!gradeFrameIndex.HasValue || gradeTextures == null)
            {
                return;
            }

            int frameIndex = Math.Max(0, Math.Min(gradeFrameIndex.Value, gradeTextures.Length - 1));
            Texture2D texture = gradeTextures[frameIndex];
            if (texture == null)
            {
                return;
            }

            sprite.Draw(texture, new Vector2(slotX + 2, slotY - Math.Max(0, texture.Height - 2)), Color.White);
        }
    }
}
