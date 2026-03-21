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
        #endregion

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
        private Point _lastMousePosition;
        private InventoryType _hoveredInventoryType = InventoryType.NONE;
        private int _hoveredSlotIndex = -1;
        private MouseState _previousInteractionMouseState;
        private bool _isDraggingItem;
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
        #endregion

        #region Constructor
        public InventoryUI(IDXObject frame, Texture2D slotBg, GraphicsDevice device)
            : base(frame)
        {
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

        public bool CanAcceptItem(InventoryType type, int itemId, int quantity = 1)
        {
            if (type == InventoryType.NONE || quantity <= 0 || !_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return false;
            }

            int remainingQuantity = quantity;
            int defaultMaxStack = InventoryItemMetadataResolver.ResolveMaxStack(type);
            if (IsStackable(type, defaultMaxStack))
            {
                for (int i = 0; i < slots.Count && remainingQuantity > 0; i++)
                {
                    InventorySlotData slot = slots[i];
                    if (slot.ItemId != itemId || slot.IsDisabled)
                    {
                        continue;
                    }

                    int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, slot.MaxStackSize);
                    int capacity = maxStackSize - Math.Max(1, slot.Quantity);
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

            if (!IsStackable(type, defaultMaxStack))
            {
                return freeSlotCount >= remainingQuantity;
            }

            int neededStacks = (remainingQuantity + defaultMaxStack - 1) / defaultMaxStack;
            return freeSlotCount >= neededStacks;
        }

        public bool TryConsumeItem(InventoryType type, int itemId, int quantity)
        {
            if (quantity <= 0)
            {
                return true;
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
            if (!rightJustPressed)
            {
                return;
            }

            if (!TryGetSlotsForType(inventoryType, out List<InventorySlotData> slots) ||
                slotIndex < 0 ||
                slotIndex >= slots.Count)
            {
                return;
            }

            InventorySlotData slot = slots[slotIndex];
            if (slot == null || !ItemUpgradeUI.IsSupportedConsumable(slot.ItemId))
            {
                return;
            }

            ItemUpgradeRequested?.Invoke(slot.ItemId);
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

            string title = ResolveDisplayText(slot.ItemName, $"Item #{slot.ItemId}");
            string typeLine = ResolveDisplayText(slot.ItemTypeName, _hoveredInventoryType.ToString());
            string quantityLine = slot.Quantity > 1 ? $"Quantity: {slot.Quantity}" : string.Empty;
            string stackLine = slot.MaxStackSize.GetValueOrDefault(1) > 1 ? $"Stack Max: {slot.MaxStackSize.Value}" : string.Empty;
            string description = ResolveDisplayText(slot.Description, string.Empty);

            int tooltipWidth = ResolveTooltipWidth();
            int textLeftOffset = TOOLTIP_PADDING + TOOLTIP_ICON_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = tooltipWidth - textLeftOffset - TOOLTIP_PADDING;

            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedType = WrapTooltipText(typeLine, sectionWidth);
            string[] wrappedQuantity = WrapTooltipText(quantityLine, sectionWidth);
            string[] wrappedStack = WrapTooltipText(stackLine, sectionWidth);
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);

            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float typeHeight = MeasureLinesHeight(wrappedType);
            float quantityHeight = MeasureLinesHeight(wrappedQuantity);
            float stackHeight = MeasureLinesHeight(wrappedStack);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);

            float contentHeight = typeHeight;
            if (quantityHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + quantityHeight;
            }

            if (stackHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? 2f : 0f) + stackHeight;
            }

            if (descriptionHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + descriptionHeight;
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
            if (typeHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedType, textX, sectionY, new Color(180, 220, 255));
                sectionY += typeHeight;
            }

            if (quantityHeight > 0f)
            {
                sectionY += typeHeight > 0f ? TOOLTIP_SECTION_GAP : 0f;
                DrawTooltipLines(sprite, wrappedQuantity, textX, sectionY, Color.White);
                sectionY += quantityHeight;
            }

            if (stackHeight > 0f)
            {
                sectionY += 2f;
                DrawTooltipLines(sprite, wrappedStack, textX, sectionY, new Color(180, 255, 210));
                sectionY += stackHeight;
            }

            if (descriptionHeight > 0f)
            {
                sectionY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, wrappedDescription, textX, sectionY, new Color(255, 238, 196));
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

        private static string ResolveDisplayText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
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
        public bool IsEquipped { get; set; }
        public bool IsDisabled { get; set; }
        public bool IsActiveBullet { get; set; }
        public int? GradeFrameIndex { get; set; }
        public string ItemName { get; set; }
        public string ItemTypeName { get; set; }
        public string Description { get; set; }

        public InventorySlotData Clone()
        {
            return new InventorySlotData
            {
                ItemId = ItemId,
                ItemTexture = ItemTexture,
                Quantity = Quantity,
                MaxStackSize = MaxStackSize,
                IsEquipped = IsEquipped,
                IsDisabled = IsDisabled,
                IsActiveBullet = IsActiveBullet,
                GradeFrameIndex = GradeFrameIndex,
                ItemName = ItemName,
                ItemTypeName = ItemTypeName,
                Description = Description
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
