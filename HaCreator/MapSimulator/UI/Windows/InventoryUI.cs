using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Inventory UI window displaying item slots in tabs
    /// Structure: UI.wz/UIWindow.img/Item/
    /// </summary>
    public class InventoryUI : UIWindowBase
    {
        #region Constants
        private const int SLOT_SIZE = 32;
        private const int SLOT_PADDING = 4;
        private const int SLOTS_PER_ROW = 4;
        private const int VISIBLE_ROWS = 6;
        private const int TOTAL_SLOTS = 24; // 4x6 = 24 slots visible per page

        // Inventory tab indices
        private const int TAB_EQUIP = 0;
        private const int TAB_USE = 1;
        private const int TAB_SETUP = 2;
        private const int TAB_ETC = 3;
        private const int TAB_CASH = 4;
        #endregion

        #region Fields
        private int _currentTab = TAB_EQUIP;
        private int _scrollOffset = 0;

        // Tab buttons
        private UIObject _tabEquip;
        private UIObject _tabUse;
        private UIObject _tabSetup;
        private UIObject _tabEtc;
        private UIObject _tabCash;

        // Slot grid background
        private Texture2D _slotBackground;
        private Texture2D _emptySlotTexture;

        // Item data (for display - simulator doesn't need real items)
        private Dictionary<InventoryType, List<InventorySlotData>> inventoryData;

        // Meso display
        private long mesoCount = 0;
        private SpriteFont _mesoFont;

        // Tab textures for highlighting
        private Texture2D _tabSelectedTexture;
        private Texture2D _tabNormalTexture;
        #endregion

        #region Properties
        public override string WindowName => "Inventory";

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value >= TAB_EQUIP && value <= TAB_CASH)
                {
                    _currentTab = value;
                    _scrollOffset = 0; // Reset scroll when changing tabs
                }
            }
        }

        public long MesoCount
        {
            get => mesoCount;
            set => mesoCount = Math.Max(0, value);
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="frame">Window background frame</param>
        /// <param name="slotBg">Slot background texture</param>
        /// <param name="device">Graphics device</param>
        public InventoryUI(IDXObject frame, Texture2D slotBg, GraphicsDevice device)
            : base(frame)
        {
            this._slotBackground = slotBg;

            // Initialize empty inventory data
            inventoryData = new Dictionary<InventoryType, List<InventorySlotData>>
            {
                { InventoryType.EQUIP, new List<InventorySlotData>() },
                { InventoryType.USE, new List<InventorySlotData>() },
                { InventoryType.SETUP, new List<InventorySlotData>() },
                { InventoryType.ETC, new List<InventorySlotData>() },
                { InventoryType.CASH, new List<InventorySlotData>() }
            };

            // Create empty slot texture
            CreateEmptySlotTexture(device);
        }

        private void CreateEmptySlotTexture(GraphicsDevice device)
        {
            _emptySlotTexture = new Texture2D(device, SLOT_SIZE, SLOT_SIZE);
            Color[] data = new Color[SLOT_SIZE * SLOT_SIZE];

            // Create a semi-transparent dark slot background
            Color slotColor = new Color(50, 50, 50, 100);
            Color borderColor = new Color(80, 80, 80, 150);

            for (int y = 0; y < SLOT_SIZE; y++)
            {
                for (int x = 0; x < SLOT_SIZE; x++)
                {
                    // Border pixels
                    if (x == 0 || x == SLOT_SIZE - 1 || y == 0 || y == SLOT_SIZE - 1)
                    {
                        data[y * SLOT_SIZE + x] = borderColor;
                    }
                    else
                    {
                        data[y * SLOT_SIZE + x] = slotColor;
                    }
                }
            }
            _emptySlotTexture.SetData(data);
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize tab buttons
        /// </summary>
        public void InitializeTabs(UIObject equipTab, UIObject useTab, UIObject setupTab, UIObject etcTab, UIObject cashTab)
        {
            this._tabEquip = equipTab;
            this._tabUse = useTab;
            this._tabSetup = setupTab;
            this._tabEtc = etcTab;
            this._tabCash = cashTab;

            if (equipTab != null)
            {
                AddButton(equipTab);
                equipTab.ButtonClickReleased += (sender) => CurrentTab = TAB_EQUIP;
            }
            if (useTab != null)
            {
                AddButton(useTab);
                useTab.ButtonClickReleased += (sender) => CurrentTab = TAB_USE;
            }
            if (setupTab != null)
            {
                AddButton(setupTab);
                setupTab.ButtonClickReleased += (sender) => CurrentTab = TAB_SETUP;
            }
            if (etcTab != null)
            {
                AddButton(etcTab);
                etcTab.ButtonClickReleased += (sender) => CurrentTab = TAB_ETC;
            }
            if (cashTab != null)
            {
                AddButton(cashTab);
                cashTab.ButtonClickReleased += (sender) => CurrentTab = TAB_CASH;
            }

            UpdateTabStates();
        }

        private void UpdateTabStates()
        {
            // Update tab visual states
            _tabEquip?.SetButtonState(_currentTab == TAB_EQUIP ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabUse?.SetButtonState(_currentTab == TAB_USE ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabSetup?.SetButtonState(_currentTab == TAB_SETUP ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabEtc?.SetButtonState(_currentTab == TAB_ETC ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCash?.SetButtonState(_currentTab == TAB_CASH ? UIObjectState.Pressed : UIObjectState.Normal);
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw inventory contents
        /// Note: Content rendering uses window-relative coordinates via centerX/centerY
        /// </summary>
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Slot content is rendered as part of the window background texture from UI.wz
            // Item icons would be drawn here when items are added to the inventory
            // For now, the window frame from UI.wz already includes the slot grid visuals
        }
        #endregion

        #region Utility Methods
        private InventoryType GetInventoryTypeFromTab(int tab)
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

        /// <summary>
        /// Add an item to the inventory (for simulation/testing)
        /// </summary>
        public void AddItem(InventoryType type, int itemId, Texture2D texture, int quantity = 1)
        {
            if (inventoryData.TryGetValue(type, out var slots))
            {
                slots.Add(new InventorySlotData
                {
                    ItemId = itemId,
                    ItemTexture = texture,
                    Quantity = quantity
                });
            }
        }

        /// <summary>
        /// Clear all items from inventory
        /// </summary>
        public void ClearInventory()
        {
            foreach (var kvp in inventoryData)
            {
                kvp.Value.Clear();
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
            InventoryType invType = GetInventoryTypeFromTab(_currentTab);
            if (inventoryData.TryGetValue(invType, out var slots))
            {
                int maxScroll = Math.Max(0, (slots.Count / SLOTS_PER_ROW) - VISIBLE_ROWS + 1);
                if (_scrollOffset < maxScroll)
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
        #endregion
    }

    /// <summary>
    /// Data for a single inventory slot
    /// </summary>
    public class InventorySlotData
    {
        public int ItemId { get; set; }
        public Texture2D ItemTexture { get; set; }
        public int Quantity { get; set; } = 1;
        public bool IsEquipped { get; set; } = false;
    }
}
