using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Equipment UI window displaying equipped items
    /// Structure: UI.wz/UIWindow.img/Equip/
    /// </summary>
    public class EquipUI : UIWindowBase
    {
        #region Constants
        private const int SLOT_SIZE = 32;
        #endregion

        #region Equipment Slot Types
        public enum EquipSlot
        {
            Ring1 = 0,
            Ring2 = 1,
            Ring3 = 2,
            Ring4 = 3,
            Pocket = 4,
            Pendant1 = 5,
            Pendant2 = 6,
            Weapon = 7,
            Belt = 8,
            Cap = 9,
            FaceAccessory = 10,
            EyeAccessory = 11,
            Top = 12,
            Bottom = 13,
            Shoes = 14,
            Earring = 15,
            Shoulder = 16,
            Glove = 17,
            Shield = 18,
            Cape = 19,
            Heart = 20,
            Badge = 21,
            Medal = 22,
            Android = 23,
            AndroidHeart = 24,
            Totem1 = 25,
            Totem2 = 26,
            Totem3 = 27
        }
        #endregion

        #region Fields
        // Equipment slot positions (relative to window)
        private readonly Dictionary<EquipSlot, Point> slotPositions;

        // Equipped items textures
        private readonly Dictionary<EquipSlot, EquipSlotData> equippedItems;

        // Empty slot texture
        private Texture2D _emptySlotTexture;

        // Character preview area
        private Texture2D _characterPreviewTexture;
        private Rectangle _characterPreviewRect;

        // Tab buttons (Pet equip, Cash equip, etc.)
        private UIObject _tabNormal;
        private UIObject _tabCash;
        private UIObject _tabPet;
        private int _currentTab = 0; // 0 = normal, 1 = cash, 2 = pet
        #endregion

        #region Properties
        public override string WindowName => "Equipment";
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="frame">Window background frame</param>
        /// <param name="device">Graphics device</param>
        public EquipUI(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {

            // Initialize slot positions (approximate positions based on typical equip window layout)
            slotPositions = new Dictionary<EquipSlot, Point>
            {
                // Left column
                { EquipSlot.Ring1, new Point(12, 55) },
                { EquipSlot.Ring2, new Point(12, 92) },
                { EquipSlot.Ring3, new Point(12, 129) },
                { EquipSlot.Ring4, new Point(12, 166) },
                { EquipSlot.Pocket, new Point(12, 203) },

                // Center-left column
                { EquipSlot.Pendant1, new Point(49, 55) },
                { EquipSlot.Pendant2, new Point(49, 92) },
                { EquipSlot.Weapon, new Point(49, 129) },
                { EquipSlot.Belt, new Point(49, 166) },

                // Center column (character preview area - skip)

                // Center-right column
                { EquipSlot.Cap, new Point(135, 55) },
                { EquipSlot.FaceAccessory, new Point(135, 92) },
                { EquipSlot.EyeAccessory, new Point(135, 129) },
                { EquipSlot.Top, new Point(135, 166) },
                { EquipSlot.Bottom, new Point(135, 203) },
                { EquipSlot.Shoes, new Point(135, 240) },

                // Right column
                { EquipSlot.Earring, new Point(172, 55) },
                { EquipSlot.Shoulder, new Point(172, 92) },
                { EquipSlot.Glove, new Point(172, 129) },
                { EquipSlot.Shield, new Point(172, 166) },
                { EquipSlot.Cape, new Point(172, 203) }
            };

            equippedItems = new Dictionary<EquipSlot, EquipSlotData>();

            // Create empty slot texture
            CreateEmptySlotTexture(device);

            // Character preview area in center
            _characterPreviewRect = new Rectangle(86, 92, 44, 100);
        }

        private void CreateEmptySlotTexture(GraphicsDevice device)
        {
            _emptySlotTexture = new Texture2D(device, SLOT_SIZE, SLOT_SIZE);
            Color[] data = new Color[SLOT_SIZE * SLOT_SIZE];

            Color slotColor = new Color(40, 40, 60, 120);
            Color borderColor = new Color(70, 70, 90, 180);

            for (int y = 0; y < SLOT_SIZE; y++)
            {
                for (int x = 0; x < SLOT_SIZE; x++)
                {
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
        /// Initialize tab buttons for equipment types
        /// </summary>
        public void InitializeTabs(UIObject normalTab, UIObject cashTab, UIObject petTab)
        {
            this._tabNormal = normalTab;
            this._tabCash = cashTab;
            this._tabPet = petTab;

            if (normalTab != null)
            {
                AddButton(normalTab);
                normalTab.ButtonClickReleased += (sender) => { _currentTab = 0; UpdateTabStates(); };
            }
            if (cashTab != null)
            {
                AddButton(cashTab);
                cashTab.ButtonClickReleased += (sender) => { _currentTab = 1; UpdateTabStates(); };
            }
            if (petTab != null)
            {
                AddButton(petTab);
                petTab.ButtonClickReleased += (sender) => { _currentTab = 2; UpdateTabStates(); };
            }

            UpdateTabStates();
        }

        private void UpdateTabStates()
        {
            _tabNormal?.SetButtonState(_currentTab == 0 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCash?.SetButtonState(_currentTab == 1 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabPet?.SetButtonState(_currentTab == 2 ? UIObjectState.Pressed : UIObjectState.Normal);
        }

        /// <summary>
        /// Set the character preview texture
        /// </summary>
        public void SetCharacterPreview(Texture2D texture)
        {
            _characterPreviewTexture = texture;
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Draw equipment window contents
        /// </summary>
        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Equipment slot content is rendered as part of the window background texture from UI.wz
            // Equipped item icons would be drawn here when items are equipped
            // For now, the window frame from UI.wz already includes the slot grid visuals
        }
        #endregion

        #region Equipment Management
        /// <summary>
        /// Equip an item to a slot (for simulation/testing)
        /// </summary>
        public void EquipItem(EquipSlot slot, int itemId, Texture2D texture, string itemName = "")
        {
            equippedItems[slot] = new EquipSlotData
            {
                ItemId = itemId,
                ItemTexture = texture,
                ItemName = itemName
            };
        }

        /// <summary>
        /// Unequip an item from a slot
        /// </summary>
        public void UnequipItem(EquipSlot slot)
        {
            if (equippedItems.ContainsKey(slot))
            {
                equippedItems.Remove(slot);
            }
        }

        /// <summary>
        /// Get equipped item data
        /// </summary>
        public EquipSlotData GetEquippedItem(EquipSlot slot)
        {
            return equippedItems.TryGetValue(slot, out var data) ? data : null;
        }

        /// <summary>
        /// Clear all equipped items
        /// </summary>
        public void ClearAllEquipment()
        {
            equippedItems.Clear();
        }
        #endregion

        #region Update
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }
        #endregion
    }

    /// <summary>
    /// Data for an equipped item
    /// </summary>
    public class EquipSlotData
    {
        public int ItemId { get; set; }
        public Texture2D ItemTexture { get; set; }
        public string ItemName { get; set; }

        // Stats (for tooltip display)
        public int STR { get; set; }
        public int DEX { get; set; }
        public int INT { get; set; }
        public int LUK { get; set; }
        public int HP { get; set; }
        public int MP { get; set; }
        public int WATK { get; set; }
        public int MATK { get; set; }
        public int WDEF { get; set; }
        public int MDEF { get; set; }
        public int Speed { get; set; }
        public int Jump { get; set; }
        public int Slots { get; set; }
        public int Stars { get; set; }
    }
}
