using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

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
        #endregion

        #region Fields
        protected int _currentTab = TAB_EQUIP;
        protected int _scrollOffset;

        private UIObject _tabEquip;
        private UIObject _tabUse;
        private UIObject _tabSetup;
        private UIObject _tabEtc;
        private UIObject _tabCash;

        private readonly Dictionary<InventoryType, List<InventorySlotData>> _inventoryData;
        private readonly Dictionary<InventoryType, int> _inventorySlotLimits;

        private long _mesoCount;
        private SpriteFont _font;

        protected Texture2D ActiveIconTexture;
        protected Texture2D DisabledSlotTexture;
        protected Texture2D SlotShadowTexture;
        protected readonly Texture2D[] GradeFrameTextures = new Texture2D[6];
        #endregion

        #region Properties
        public override string WindowName => "Inventory";

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

        protected int GetSlotLimit(InventoryType type)
        {
            return _inventorySlotLimits.TryGetValue(type, out int value)
                ? Math.Max(TOTAL_SLOTS, value)
                : TOTAL_SLOTS;
        }

        public void SetSlotLimit(InventoryType type, int slotLimit)
        {
            if (type == InventoryType.NONE)
            {
                return;
            }

            _inventorySlotLimits[type] = Math.Max(TOTAL_SLOTS, slotLimit);
        }

        public void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1)
        {
            AddItem(type, new InventorySlotData
            {
                ItemId = itemId,
                ItemTexture = texture,
                Quantity = Math.Max(1, quantity),
                MaxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type)
            });
        }

        public void AddItem(InventoryType type, InventorySlotData slotData)
        {
            if (slotData == null || !_inventoryData.TryGetValue(type, out List<InventorySlotData> slots))
            {
                return;
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

        public void ClearInventory()
        {
            foreach (KeyValuePair<InventoryType, List<InventorySlotData>> kvp in _inventoryData)
            {
                kvp.Value.Clear();
            }
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
        }

        private static bool IsStackable(InventoryType type, int maxStackSize)
        {
            return type != InventoryType.EQUIP && maxStackSize > 1;
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
                ItemName = ItemName
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
