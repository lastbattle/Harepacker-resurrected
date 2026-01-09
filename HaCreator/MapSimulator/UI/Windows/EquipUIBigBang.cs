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
    /// Equipment UI window for post-Big Bang MapleStory (v100+)
    /// Structure: UI.wz/UIWindow2.img/Equip/character
    /// Window dimensions: 184x290 pixels
    /// </summary>
    public class EquipUIBigBang : UIWindowBase
    {
        #region Constants
        private const int SLOT_SIZE = 32;

        // Window dimensions (from UIWindow2.img/Equip/character/backgrnd)
        private const int WINDOW_WIDTH = 184;
        private const int WINDOW_HEIGHT = 290;

        // Tab indices
        private const int TAB_CHARACTER = 0;
        private const int TAB_PET = 1;
        private const int TAB_DRAGON = 2;
        private const int TAB_MECHANIC = 3;
        private const int TAB_ANDROID = 4;
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
        private int _currentTab = TAB_CHARACTER;

        // Foreground texture (backgrnd2 - grid overlay)
        private IDXObject _foreground;
        private Point _foregroundOffset;

        // Slot labels and character silhouette (backgrnd3)
        private IDXObject _slotLabels;
        private Point _slotLabelsOffset;

        // Tab buttons
        private UIObject _btnPet;
        private UIObject _btnDragon;
        private UIObject _btnMechanic;
        private UIObject _btnAndroid;
        private UIObject _btnSlot;

        // Equipment slot positions (relative to window)
        private readonly Dictionary<EquipSlot, Point> slotPositions;

        // Equipped items data
        private readonly Dictionary<EquipSlot, EquipSlotData> equippedItems;

        // Graphics device
        private GraphicsDevice _device;
        #endregion

        #region Properties
        public override string WindowName => "Equipment";

        public int CurrentTab
        {
            get => _currentTab;
            set
            {
                if (value >= TAB_CHARACTER && value <= TAB_ANDROID)
                {
                    _currentTab = value;
                }
            }
        }
        #endregion

        #region Constructor
        public EquipUIBigBang(IDXObject frame, GraphicsDevice device)
            : base(frame)
        {
            _device = device;

            // Initialize slot positions for Big Bang layout
            // Grid formula from IDA: X = 33*col + 10, Y = 33*row + 27 (for nType=0)
            // Col 0=10, Col 1=43, Col 2=76, Col 3=109, Col 4=142
            // Row 0=27, Row 1=60, Row 2=93, Row 3=126, Row 4=159, Row 5=192, Row 6=225
            slotPositions = new Dictionary<EquipSlot, Point>
            {
                // Row 0
                { EquipSlot.Badge, new Point(10, 27) },      // Col 0, Row 0
                { EquipSlot.Cap, new Point(43, 27) },        // Col 1, Row 0
                { EquipSlot.Android, new Point(109, 27) },   // Col 3, Row 0
                { EquipSlot.Heart, new Point(142, 27) },     // Col 4, Row 0

                // Row 1
                { EquipSlot.Medal, new Point(10, 60) },      // Col 0, Row 1
                { EquipSlot.FaceAccessory, new Point(43, 60) }, // Col 1, Row 1 (Forehead)
                { EquipSlot.Ring1, new Point(109, 60) },     // Col 3, Row 1
                { EquipSlot.Ring2, new Point(142, 60) },     // Col 4, Row 1

                // Row 2
                { EquipSlot.EyeAccessory, new Point(43, 93) }, // Col 1, Row 2
                { EquipSlot.Shoulder, new Point(142, 93) },  // Col 4, Row 2

                // Row 3
                { EquipSlot.Cape, new Point(10, 126) },      // Col 0, Row 3 (Mantle/Cape)
                { EquipSlot.Top, new Point(43, 126) },       // Col 1, Row 3 (Clothes)
                { EquipSlot.Pendant1, new Point(76, 126) },  // Col 2, Row 3
                { EquipSlot.Weapon, new Point(142, 126) },   // Col 4, Row 3

                // Row 4
                { EquipSlot.Glove, new Point(10, 159) },     // Col 0, Row 4
                { EquipSlot.Belt, new Point(76, 159) },      // Col 2, Row 4
                { EquipSlot.Ring3, new Point(142, 159) },    // Col 4, Row 4

                // Row 5
                { EquipSlot.Bottom, new Point(43, 192) },    // Col 1, Row 5 (Pants)
                { EquipSlot.Shoes, new Point(76, 192) },     // Col 2, Row 5
                { EquipSlot.Pocket, new Point(142, 192) },   // Col 4, Row 5 (Pet slot)

                // Row 6
                { EquipSlot.Totem1, new Point(10, 225) },    // Col 0, Row 6 (Taming Mob)
                { EquipSlot.Totem2, new Point(43, 225) },    // Col 1, Row 6 (Saddle)
                { EquipSlot.Totem3, new Point(76, 225) },    // Col 2, Row 6 (Mob Equip)

                // Additional slots (off main grid or secondary positions)
                { EquipSlot.Pendant2, new Point(109, 126) }, // Col 3, Row 3 (secondary pendant)
                { EquipSlot.Ring4, new Point(109, 159) },    // Col 3, Row 4
                { EquipSlot.Shield, new Point(109, 93) },    // Col 3, Row 2 (Sub-weapon)
                { EquipSlot.Earring, new Point(10, 93) },    // Col 0, Row 2
            };

            equippedItems = new Dictionary<EquipSlot, EquipSlotData>();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Set the foreground texture (backgrnd2 - grid overlay)
        /// </summary>
        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Set the slot labels and character silhouette (backgrnd3)
        /// </summary>
        public void SetSlotLabels(IDXObject slotLabels, int offsetX, int offsetY)
        {
            _slotLabels = slotLabels;
            _slotLabelsOffset = new Point(offsetX, offsetY);
        }

        /// <summary>
        /// Initialize equipment tab buttons
        /// </summary>
        public void InitializeTabButtons(UIObject btnPet, UIObject btnDragon, UIObject btnMechanic, UIObject btnAndroid, UIObject btnSlot)
        {
            _btnPet = btnPet;
            _btnDragon = btnDragon;
            _btnMechanic = btnMechanic;
            _btnAndroid = btnAndroid;
            _btnSlot = btnSlot;

            if (btnPet != null)
            {
                AddButton(btnPet);
                btnPet.ButtonClickReleased += (sender) => CurrentTab = TAB_PET;
            }
            if (btnDragon != null)
            {
                AddButton(btnDragon);
                btnDragon.ButtonClickReleased += (sender) => CurrentTab = TAB_DRAGON;
            }
            if (btnMechanic != null)
            {
                AddButton(btnMechanic);
                btnMechanic.ButtonClickReleased += (sender) => CurrentTab = TAB_MECHANIC;
            }
            if (btnAndroid != null)
            {
                AddButton(btnAndroid);
                btnAndroid.ButtonClickReleased += (sender) => CurrentTab = TAB_ANDROID;
            }
            if (btnSlot != null)
            {
                AddButton(btnSlot);
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
            int windowX = this.Position.X;
            int windowY = this.Position.Y;

            // Draw foreground (backgrnd2 - grid overlay) z=0
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _foregroundOffset.X, windowY + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Draw slot labels and character silhouette (backgrnd3) z=1
            if (_slotLabels != null)
            {
                _slotLabels.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    windowX + _slotLabelsOffset.X, windowY + _slotLabelsOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            // Equipped item icons would be drawn here when items are equipped
        }
        #endregion

        #region Equipment Management
        /// <summary>
        /// Equip an item to a slot
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
}
