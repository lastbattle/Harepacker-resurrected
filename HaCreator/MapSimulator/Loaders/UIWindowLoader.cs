using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Handles loading of UI windows (Inventory, Equipment, Skills, Quest) from UI.wz
    /// </summary>
    public static class UIWindowLoader
    {
        #region Inventory Window
        /// <summary>
        /// Create the Inventory window from UI.wz/UIWindow.img/Item
        /// </summary>
        public static InventoryUI CreateInventoryWindow(
            WzImage uiWindowImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty itemProperty = (WzSubProperty)uiWindowImage?["Item"];
            if (itemProperty == null)
            {
                // Try UIWindow2.img for newer clients
                return CreatePlaceholderInventory(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)itemProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderInventory(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            // Create the inventory window
            InventoryUI inventory = new InventoryUI(frame, null, device);
            inventory.Position = new Point(screenWidth - bgTexture.Width - 20, 100);

            // Load tab buttons if available
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            UIObject tabEquip = LoadTabButton(itemProperty, "Tab0", btClickSound, btOverSound, device);
            UIObject tabUse = LoadTabButton(itemProperty, "Tab1", btClickSound, btOverSound, device);
            UIObject tabSetup = LoadTabButton(itemProperty, "Tab2", btClickSound, btOverSound, device);
            UIObject tabEtc = LoadTabButton(itemProperty, "Tab3", btClickSound, btOverSound, device);
            UIObject tabCash = LoadTabButton(itemProperty, "Tab4", btClickSound, btOverSound, device);

            inventory.InitializeTabs(tabEquip, tabUse, tabSetup, tabEtc, tabCash);

            // Load close button
            UIObject closeBtn = LoadButton(itemProperty, "BtClose", btClickSound, btOverSound, device);
            inventory.InitializeCloseButton(closeBtn);

            return inventory;
        }

        private static InventoryUI CreatePlaceholderInventory(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            // Create a simple placeholder window
            int width = 170;
            int height = 335;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Inventory");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            InventoryUI inventory = new InventoryUI(frame, null, device);
            inventory.Position = new Point(screenWidth - width - 20, 100);

            return inventory;
        }
        #endregion

        #region Equipment Window
        /// <summary>
        /// Create the Equipment window - selects pre-BB or post-BB version based on isBigBang
        /// </summary>
        public static UIWindowBase CreateEquipWindowUnified(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            if (isBigBang && uiWindow2Image != null)
            {
                return CreateEquipWindowBigBang(uiWindow2Image, uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            }
            return CreateEquipWindow(uiWindow1Image, soundUIImage, device, screenWidth, screenHeight);
        }

        /// <summary>
        /// Create the Equipment window from UI.wz/UIWindow.img/Equip (Pre-Big Bang)
        /// </summary>
        public static EquipUI CreateEquipWindow(
            WzImage uiWindowImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty equipProperty = (WzSubProperty)uiWindowImage?["Equip"];
            if (equipProperty == null)
            {
                return CreatePlaceholderEquip(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)equipProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderEquip(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            EquipUI equip = new EquipUI(frame, device);
            equip.Position = new Point(screenWidth - bgTexture.Width - 200, 100);

            // Load buttons
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            UIObject closeBtn = LoadButton(equipProperty, "BtClose", btClickSound, btOverSound, device);
            equip.InitializeCloseButton(closeBtn);

            return equip;
        }

        /// <summary>
        /// Create the Equipment window from UI.wz/UIWindow2.img/Equip/character (Post-Big Bang)
        /// </summary>
        public static EquipUIBigBang CreateEquipWindowBigBang(
            WzImage uiWindow2Image, WzImage uiWindow1Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty equipProperty = (WzSubProperty)uiWindow2Image?["Equip"];
            WzSubProperty characterProperty = (WzSubProperty)equipProperty?["character"];
            if (characterProperty == null)
            {
                return CreatePlaceholderEquipBigBang(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)characterProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderEquipBigBang(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            EquipUIBigBang equip = new EquipUIBigBang(frame, device);
            equip.Position = new Point(screenWidth - bgTexture.Width - 200, 100);

            // Load foreground (backgrnd2 - grid overlay)
            WzCanvasProperty backgrnd2 = (WzCanvasProperty)characterProperty["backgrnd2"];
            if (backgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap fgBitmap = backgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D fgTexture = fgBitmap.ToTexture2D(device);
                    IDXObject foreground = new DXObject(0, 0, fgTexture, 0);
                    System.Drawing.PointF? origin = backgrnd2.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 6;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 22;
                    equip.SetForeground(foreground, offsetX, offsetY);
                }
                catch { }
            }

            // Load slot labels and character silhouette (backgrnd3)
            WzCanvasProperty backgrnd3 = (WzCanvasProperty)characterProperty["backgrnd3"];
            if (backgrnd3 != null)
            {
                try
                {
                    System.Drawing.Bitmap slotBitmap = backgrnd3.GetLinkedWzCanvasBitmap();
                    Texture2D slotTexture = slotBitmap.ToTexture2D(device);
                    IDXObject slotLabels = new DXObject(0, 0, slotTexture, 0);
                    System.Drawing.PointF? slotOrigin = backgrnd3.GetCanvasOriginPosition();
                    // Origin is (-10, -27), so offset is (10, 27)
                    int slotOffsetX = slotOrigin.HasValue ? -(int)slotOrigin.Value.X : 10;
                    int slotOffsetY = slotOrigin.HasValue ? -(int)slotOrigin.Value.Y : 27;
                    equip.SetSlotLabels(slotLabels, slotOffsetX, slotOffsetY);
                }
                catch { }
            }

            // Load button sounds
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUIEquip constructor: (162, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 162;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            equip.InitializeCloseButton(closeBtn);

            // Load tab buttons
            UIObject btnPet = LoadButton(characterProperty, "BtPet", btClickSound, btOverSound, device);
            UIObject btnDragon = LoadButton(characterProperty, "BtDragon", btClickSound, btOverSound, device);
            UIObject btnMechanic = LoadButton(characterProperty, "BtMechanic", btClickSound, btOverSound, device);
            UIObject btnAndroid = LoadButton(characterProperty, "BtAndroid", btClickSound, btOverSound, device);
            UIObject btnSlot = LoadButton(characterProperty, "BtSlot", btClickSound, btOverSound, device);
            equip.InitializeTabButtons(btnPet, btnDragon, btnMechanic, btnAndroid, btnSlot);

            return equip;
        }

        private static EquipUI CreatePlaceholderEquip(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 210;
            int height = 290;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Equipment");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            EquipUI equip = new EquipUI(frame, device);
            equip.Position = new Point(screenWidth - width - 200, 100);

            return equip;
        }

        private static EquipUIBigBang CreatePlaceholderEquipBigBang(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 184;
            int height = 290;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Equipment");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            EquipUIBigBang equip = new EquipUIBigBang(frame, device);
            equip.Position = new Point(screenWidth - width - 200, 100);

            return equip;
        }
        #endregion

        #region Skill Window
        /// <summary>
        /// Create the Skill window from UI.wz/UIWindow.img/Skill
        /// </summary>
        public static SkillUI CreateSkillWindow(
            WzImage uiWindowImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty skillProperty = (WzSubProperty)uiWindowImage?["Skill"];
            if (skillProperty == null)
            {
                return CreatePlaceholderSkill(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)skillProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderSkill(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            SkillUI skill = new SkillUI(frame, device);
            skill.Position = new Point(50, 100);

            // Load buttons
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            UIObject closeBtn = LoadButton(skillProperty, "BtClose", btClickSound, btOverSound, device);
            skill.InitializeCloseButton(closeBtn);

            return skill;
        }

        private static SkillUI CreatePlaceholderSkill(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 200;
            int height = 300;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Skills");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            SkillUI skill = new SkillUI(frame, device);
            skill.Position = new Point(50, 100);

            return skill;
        }
        #endregion

        #region Ability/Stat Window
        /// <summary>
        /// Create the Ability/Stat window - selects pre-BB or post-BB version based on isBigBang
        /// </summary>
        public static UIWindowBase CreateAbilityWindow(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            if (isBigBang && uiWindow2Image != null)
            {
                return CreateAbilityWindowBigBang(uiWindow2Image, uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            }
            return CreateAbilityWindowPreBB(uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
        }

        /// <summary>
        /// Create the Ability/Stat window from UI.wz/UIWindow.img/Stat (Pre-Big Bang)
        /// </summary>
        public static AbilityUI CreateAbilityWindowPreBB(
            WzImage uiWindowImage, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty statProperty = (WzSubProperty)uiWindowImage?["Stat"];
            if (statProperty == null)
            {
                return CreatePlaceholderAbility(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)statProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderAbility(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            AbilityUI ability = new AbilityUI(frame, device);
            ability.Position = new Point(50, 50);

            // Load button sounds
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUIStat constructor: (150, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 150;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            ability.InitializeCloseButton(closeBtn);

            // Y offset compensator to align buttons with background (same as AbilityUI.Y_OFFSET)
            const int Y_OFFSET = 18;

            // Client Y positions from IDA Pro analysis of CUIStat::Draw
            const int CLIENT_STR_Y = 227;
            const int CLIENT_DEX_Y = 245;
            const int CLIENT_INT_Y = 263;
            const int CLIENT_LUK_Y = 281;
            const int CLIENT_DETAIL_BTN_Y = 230;
            const int CLIENT_AUTO_BTN_Y = 300;

            // Stat increase buttons - use BtApUp from Stat property
            UIObject btnIncSTR = LoadButton(statProperty, "BtApUp", btClickSound, btOverSound, device);
            UIObject btnIncDEX = LoadButton(statProperty, "BtApUp", btClickSound, btOverSound, device);
            UIObject btnIncINT = LoadButton(statProperty, "BtApUp", btClickSound, btOverSound, device);
            UIObject btnIncLUK = LoadButton(statProperty, "BtApUp", btClickSound, btOverSound, device);

            // Position stat buttons - right side of stat values (client Y + compensator)
            int statButtonX = 155;
            if (btnIncSTR != null) { btnIncSTR.X = statButtonX; btnIncSTR.Y = CLIENT_STR_Y + Y_OFFSET; }
            if (btnIncDEX != null) { btnIncDEX.X = statButtonX; btnIncDEX.Y = CLIENT_DEX_Y + Y_OFFSET; }
            if (btnIncINT != null) { btnIncINT.X = statButtonX; btnIncINT.Y = CLIENT_INT_Y + Y_OFFSET; }
            if (btnIncLUK != null) { btnIncLUK.X = statButtonX; btnIncLUK.Y = CLIENT_LUK_Y + Y_OFFSET; }

            ability.InitializeStatButtons(btnIncSTR, btnIncDEX, btnIncINT, btnIncLUK);

            // Auto-assign button - between info section (Fame Y=158) and stats section (STR Y=227)
            // BtAuto is 73x35, positioned at right side of window
            UIObject autoAssignBtn = LoadButton(statProperty, "BtAuto", btClickSound, btOverSound, device);
            if (autoAssignBtn != null)
            {
                autoAssignBtn.X = 96;
                autoAssignBtn.Y = 198;
            }
            ability.InitializeAutoAssignButton(autoAssignBtn);

            // Detail button (expand/collapse detailed stats)
            // BtDetail is 47x18, positioned at bottom of window (347 - 18 - 10 = 319)
            UIObject detailBtn = LoadButton(statProperty, "BtDetail", btClickSound, btOverSound, device);
            if (detailBtn != null)
            {
                detailBtn.X = 122;  // Right side: 175 - 47 - 6 margin
                detailBtn.Y = 322;  // Bottom of window
            }
            ability.InitializeDetailButton(detailBtn);

            // Load detail background (backgrnd3) for expanded stats view
            WzCanvasProperty backgrnd3 = (WzCanvasProperty)statProperty["backgrnd3"];
            if (backgrnd3 != null)
            {
                try
                {
                    System.Drawing.Bitmap detailBgBitmap = backgrnd3.GetLinkedWzCanvasBitmap();
                    Texture2D detailBgTexture = detailBgBitmap.ToTexture2D(device);
                    IDXObject detailFrame = new DXObject(0, 0, detailBgTexture, 0);
                    ability.SetDetailBackground(detailFrame);
                }
                catch { }
            }

            return ability;
        }

        private static AbilityUI CreatePlaceholderAbility(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 200;
            int height = 320;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Ability");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            AbilityUI ability = new AbilityUI(frame, device);
            ability.Position = new Point(50, 50);

            return ability;
        }

        /// <summary>
        /// Create the Ability/Stat window from UI.wz/UIWindow2.img/Stat (Post-Big Bang)
        /// </summary>
        public static AbilityUIBigBang CreateAbilityWindowBigBang(
            WzImage uiWindow2Image, WzImage uiWindow1Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty statProperty = (WzSubProperty)uiWindow2Image?["Stat"];
            WzSubProperty mainProperty = (WzSubProperty)statProperty?["main"];
            if (mainProperty == null)
            {
                return CreatePlaceholderAbilityBigBang(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)mainProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderAbilityBigBang(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            AbilityUIBigBang ability = new AbilityUIBigBang(frame, device);
            ability.Position = new Point(50, 50);

            // Load foreground (backgrnd2 - labels/overlay)
            WzCanvasProperty backgrnd2 = (WzCanvasProperty)mainProperty["backgrnd2"];
            if (backgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap fgBitmap = backgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D fgTexture = fgBitmap.ToTexture2D(device);
                    IDXObject foreground = new DXObject(0, 0, fgTexture, 0);
                    // Origin is (-6, -22), so offset is (6, 22)
                    System.Drawing.PointF? origin = backgrnd2.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 6;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 22;
                    ability.SetForeground(foreground, offsetX, offsetY);
                }
                catch { }
            }

            // Load button sounds
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUIStat constructor: (150, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 150;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            ability.InitializeCloseButton(closeBtn);

            // Button positions from WZ origins (negated values)
            const int STAT_BTN_X = 147;
            const int BTN_STR_Y = 244;
            const int BTN_DEX_Y = 262;
            const int BTN_INT_Y = 280;
            const int BTN_LUK_Y = 298;
            const int BTN_AUTO_X = 94;
            const int BTN_AUTO_Y = 198;
            const int BTN_DETAIL_X = 92;
            const int BTN_DETAIL_Y = 325;

            // HP/MP increase buttons (Big Bang feature)
            UIObject btnIncHP = LoadButton(mainProperty, "BtHpUp", btClickSound, btOverSound, device);
            UIObject btnIncMP = LoadButton(mainProperty, "BtMpUp", btClickSound, btOverSound, device);
            if (btnIncHP != null) { btnIncHP.X = STAT_BTN_X; btnIncHP.Y = 110; }
            if (btnIncMP != null) { btnIncMP.X = STAT_BTN_X; btnIncMP.Y = 128; }
            ability.InitializeHpMpButtons(btnIncHP, btnIncMP);

            // Stat increase buttons - individual buttons for Big Bang
            UIObject btnIncSTR = LoadButton(mainProperty, "BtStrUp", btClickSound, btOverSound, device);
            UIObject btnIncDEX = LoadButton(mainProperty, "BtDexUp", btClickSound, btOverSound, device);
            UIObject btnIncINT = LoadButton(mainProperty, "BtIntUp", btClickSound, btOverSound, device);
            UIObject btnIncLUK = LoadButton(mainProperty, "BtLukUp", btClickSound, btOverSound, device);

            if (btnIncSTR != null) { btnIncSTR.X = STAT_BTN_X; btnIncSTR.Y = BTN_STR_Y; }
            if (btnIncDEX != null) { btnIncDEX.X = STAT_BTN_X; btnIncDEX.Y = BTN_DEX_Y; }
            if (btnIncINT != null) { btnIncINT.X = STAT_BTN_X; btnIncINT.Y = BTN_INT_Y; }
            if (btnIncLUK != null) { btnIncLUK.X = STAT_BTN_X; btnIncLUK.Y = BTN_LUK_Y; }

            ability.InitializeStatButtons(btnIncSTR, btnIncDEX, btnIncINT, btnIncLUK);

            // Auto-assign button
            UIObject autoAssignBtn = LoadButton(mainProperty, "BtAuto", btClickSound, btOverSound, device);
            if (autoAssignBtn != null)
            {
                autoAssignBtn.X = BTN_AUTO_X;
                autoAssignBtn.Y = BTN_AUTO_Y;
            }
            ability.InitializeAutoAssignButton(autoAssignBtn);

            // Detail buttons (Open/Close for Big Bang)
            UIObject detailOpenBtn = LoadButton(mainProperty, "BtDetailOpen", btClickSound, btOverSound, device);
            UIObject detailCloseBtn = LoadButton(mainProperty, "BtDetailClose", btClickSound, btOverSound, device);
            if (detailOpenBtn != null) { detailOpenBtn.X = BTN_DETAIL_X; detailOpenBtn.Y = BTN_DETAIL_Y; }
            if (detailCloseBtn != null) { detailCloseBtn.X = BTN_DETAIL_X; detailCloseBtn.Y = BTN_DETAIL_Y; }
            ability.InitializeDetailButtons(detailOpenBtn, detailCloseBtn);

            // Load detail background from Stat/detail
            WzSubProperty detailProperty = (WzSubProperty)statProperty["detail"];
            WzCanvasProperty detailBackgrnd = (WzCanvasProperty)detailProperty?["backgrnd"];
            if (detailBackgrnd != null)
            {
                try
                {
                    System.Drawing.Bitmap detailBgBitmap = detailBackgrnd.GetLinkedWzCanvasBitmap();
                    Texture2D detailBgTexture = detailBgBitmap.ToTexture2D(device);
                    IDXObject detailFrame = new DXObject(0, 0, detailBgTexture, 0);
                    ability.SetDetailBackground(detailFrame);
                }
                catch { }
            }

            // Load detail foreground (backgrnd2 from Stat/detail)
            WzCanvasProperty detailBackgrnd2 = (WzCanvasProperty)detailProperty?["backgrnd2"];
            if (detailBackgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap detailFgBitmap = detailBackgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D detailFgTexture = detailFgBitmap.ToTexture2D(device);
                    IDXObject detailForeground = new DXObject(0, 0, detailFgTexture, 0);
                    // Origin is (-6, -7), so offset is (6, 7)
                    System.Drawing.PointF? detailOrigin = detailBackgrnd2.GetCanvasOriginPosition();
                    int detailOffsetX = detailOrigin.HasValue ? -(int)detailOrigin.Value.X : 6;
                    int detailOffsetY = detailOrigin.HasValue ? -(int)detailOrigin.Value.Y : 7;
                    ability.SetDetailForeground(detailForeground, detailOffsetX, detailOffsetY);
                }
                catch { }
            }

            return ability;
        }

        private static AbilityUIBigBang CreatePlaceholderAbilityBigBang(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 172;
            int height = 355;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Ability");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            AbilityUIBigBang ability = new AbilityUIBigBang(frame, device);
            ability.Position = new Point(50, 50);

            return ability;
        }
        #endregion

        #region Skill Window (Big Bang)
        /// <summary>
        /// Create the Skill window - selects pre-BB or post-BB version based on isBigBang
        /// </summary>
        public static UIWindowBase CreateSkillWindowUnified(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            if (isBigBang && uiWindow2Image != null)
            {
                return CreateSkillWindowBigBang(uiWindow2Image, uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            }
            return CreateSkillWindow(uiWindow1Image, soundUIImage, device, screenWidth, screenHeight);
        }

        /// <summary>
        /// Create the Skill window from UI.wz/UIWindow2.img/Skill/main (Post-Big Bang)
        /// </summary>
        public static SkillUIBigBang CreateSkillWindowBigBang(
            WzImage uiWindow2Image, WzImage uiWindow1Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty skillProperty = (WzSubProperty)uiWindow2Image?["Skill"];
            WzSubProperty mainProperty = (WzSubProperty)skillProperty?["main"];
            if (mainProperty == null)
            {
                return CreatePlaceholderSkillBigBang(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)mainProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderSkillBigBang(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            SkillUIBigBang skill = new SkillUIBigBang(frame, device);
            skill.Position = new Point(50, 100);

            // Load foreground (backgrnd2 - labels/overlay)
            WzCanvasProperty backgrnd2 = (WzCanvasProperty)mainProperty["backgrnd2"];
            if (backgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap fgBitmap = backgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D fgTexture = fgBitmap.ToTexture2D(device);
                    IDXObject foreground = new DXObject(0, 0, fgTexture, 0);
                    System.Drawing.PointF? origin = backgrnd2.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 6;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 22;
                    skill.SetForeground(foreground, offsetX, offsetY);
                }
                catch { }
            }

            // Load skill list background (backgrnd3)
            WzCanvasProperty backgrnd3 = (WzCanvasProperty)mainProperty["backgrnd3"];
            if (backgrnd3 != null)
            {
                try
                {
                    System.Drawing.Bitmap bg3Bitmap = backgrnd3.GetLinkedWzCanvasBitmap();
                    Texture2D bg3Texture = bg3Bitmap.ToTexture2D(device);
                    IDXObject skillListBg = new DXObject(0, 0, bg3Texture, 0);
                    System.Drawing.PointF? origin = backgrnd3.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 7;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 47;
                    skill.SetSkillListBackground(skillListBg, offsetX, offsetY);
                }
                catch { }
            }

            // Load skill row textures (skill0, skill1 - alternating row backgrounds)
            Texture2D skillRow0 = LoadCanvasTexture(mainProperty, "skill0", device);
            Texture2D skillRow1 = LoadCanvasTexture(mainProperty, "skill1", device);
            Texture2D skillLine = LoadCanvasTexture(mainProperty, "line", device);
            skill.SetSkillRowTextures(skillRow0, skillRow1, skillLine);
            System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Skill row textures: row0={skillRow0 != null}, row1={skillRow1 != null}, line={skillLine != null}");

            // Load tab textures
            WzSubProperty tabProperty = (WzSubProperty)mainProperty["Tab"];
            if (tabProperty != null)
            {
                Texture2D[] tabEnabled = new Texture2D[5];
                Texture2D[] tabDisabled = new Texture2D[5];

                WzSubProperty enabledProperty = (WzSubProperty)tabProperty["enabled"];
                WzSubProperty disabledProperty = (WzSubProperty)tabProperty["disabled"];

                for (int i = 0; i < 5; i++)
                {
                    string tabIndex = i.ToString();
                    if (enabledProperty != null)
                        tabEnabled[i] = LoadCanvasTexture(enabledProperty, tabIndex, device);
                    if (disabledProperty != null)
                        tabDisabled[i] = LoadCanvasTexture(disabledProperty, tabIndex, device);
                }

                skill.SetTabTextures(tabEnabled, tabDisabled);
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Tab textures loaded: enabled[0]={tabEnabled[0] != null}, disabled[0]={tabDisabled[0] != null}");
            }

            // Load SP Up button textures
            WzSubProperty spUpProperty = (WzSubProperty)mainProperty["BtSpUp"];
            if (spUpProperty != null)
            {
                Texture2D spUpNormal = LoadButtonStateTexture(spUpProperty, "normal", device);
                Texture2D spUpPressed = LoadButtonStateTexture(spUpProperty, "pressed", device);
                Texture2D spUpDisabled = LoadButtonStateTexture(spUpProperty, "disabled", device);
                Texture2D spUpMouseOver = LoadButtonStateTexture(spUpProperty, "mouseOver", device);
                skill.SetSpUpTextures(spUpNormal, spUpPressed, spUpDisabled, spUpMouseOver);
            }

            // Load button sounds
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUISkill constructor: (153, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 153;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            skill.InitializeCloseButton(closeBtn);

            // Load macro button - position from WZ origin (-114, -273) means X=114, Y=273
            UIObject macroBtn = LoadButton(mainProperty, "BtMacro", btClickSound, btOverSound, device);
            if (macroBtn != null)
            {
                macroBtn.X = 114;
                macroBtn.Y = 273;
            }
            skill.InitializeMacroButton(macroBtn);

            return skill;
        }

        /// <summary>
        /// Load beginner skills into a skill window (legacy method for compatibility)
        /// </summary>
        public static void LoadBeginnerSkills(SkillUIBigBang skillWindow, WzFile skillWzFile, WzFile stringWzFile, GraphicsDevice device)
        {
            // Default to beginner job
            LoadSkillsForJob(skillWindow, 0, device);
        }

        /// <summary>
        /// Load skills for a character's job into a skill window
        /// This loads all skills in the job progression path
        /// </summary>
        /// <param name="skillWindow">The skill window to populate</param>
        /// <param name="jobId">The character's current job ID (e.g., 212 for Bishop)</param>
        /// <param name="device">Graphics device for texture creation</param>
        public static void LoadSkillsForJob(SkillUIBigBang skillWindow, int jobId, GraphicsDevice device)
        {
            if (skillWindow == null)
                return;

            try
            {
                // Get the job path (all jobs from beginner to current job)
                var jobPath = GetJobPath(jobId);
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Loading skills for job {jobId}, path: [{string.Join(", ", jobPath)}]");

                // Load skills for each job in the path
                for (int tabIndex = 0; tabIndex < jobPath.Count && tabIndex < 5; tabIndex++)
                {
                    int job = jobPath[tabIndex];
                    var skills = SkillDataLoader.LoadSkillsForJob(job, device);
                    skillWindow.AddSkills(tabIndex, skills);
                    System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Tab {tabIndex}: Loaded {skills.Count} skills for job {job}");

                    // Load and set the job icon and name
                    Texture2D jobIcon = SkillDataLoader.LoadJobIcon(job, device);
                    string jobName = SkillDataLoader.GetJobName(job);
                    skillWindow.SetJobInfo(tabIndex, jobIcon, jobName);
                    System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Tab {tabIndex}: Set job info for {jobName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Failed to load skills: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the job path from beginner to the specified job
        /// </summary>
        private static List<int> GetJobPath(int job)
        {
            var path = new List<int> { 0 }; // Always include beginner

            if (job == 0)
                return path;

            // Add first job (e.g., 200 for Magician)
            int firstJob = (job / 100) * 100;
            if (firstJob > 0)
                path.Add(firstJob);

            // Add second job (e.g., 210 for Cleric)
            int secondJob = (job / 10) * 10;
            if (secondJob > firstJob)
                path.Add(secondJob);

            // Add third job (e.g., 211 for Priest)
            int thirdJob = secondJob + 1;
            if (thirdJob > secondJob && thirdJob < job)
                path.Add(thirdJob);

            // Add current job (e.g., 212 for Bishop)
            if (!path.Contains(job))
                path.Add(job);

            return path;
        }

        /// <summary>
        /// Create the Skill Macro window for post-Big Bang
        /// Structure: UI.wz/UIWindow2.img/Skill/macro
        /// </summary>
        public static SkillMacroUI CreateSkillMacroWindowBigBang(
            WzImage uiWindow2Image, WzImage soundUIImage, GraphicsDevice device, int screenWidth, int screenHeight)
        {
            if (uiWindow2Image == null)
                return null;

            try
            {
                // Get the Skill/macro property
                WzSubProperty skillProperty = (WzSubProperty)uiWindow2Image["Skill"];
                if (skillProperty == null)
                    return null;

                WzSubProperty macroProperty = (WzSubProperty)skillProperty["macro"];
                if (macroProperty == null)
                    return null;

                // Load background - handle both direct canvas and linked canvas
                WzObject backgrndObj = macroProperty["backgrnd"];
                if (backgrndObj == null)
                    return null;

                System.Drawing.Bitmap bgBitmap = null;
                if (backgrndObj is WzCanvasProperty canvasProp)
                {
                    bgBitmap = canvasProp.GetLinkedWzCanvasBitmap();
                }
                else if (backgrndObj is WzSubProperty subProp)
                {
                    // Try to find canvas inside sub-property (might be named "0" or direct child)
                    WzCanvasProperty innerCanvas = (WzCanvasProperty)subProp["0"] ?? (WzCanvasProperty)subProp.WzProperties.FirstOrDefault(p => p is WzCanvasProperty);
                    if (innerCanvas != null)
                        bgBitmap = innerCanvas.GetLinkedWzCanvasBitmap();
                }

                Texture2D bgTexture = bgBitmap?.ToTexture2D(device);
                if (bgTexture == null)
                    return null;

                IDXObject frame = new DXObject(0, 0, bgTexture, 0);

                // Create the macro window
                SkillMacroUI macroUI = new SkillMacroUI(frame, device);

                // Position window in center of screen
                macroUI.Position = new Point(
                    (screenWidth - bgTexture.Width) / 2,
                    (screenHeight - bgTexture.Height) / 2);

                // Load button sounds
                WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
                WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

                // Load OK button
                UIObject btnOK = LoadButton(macroProperty, "BtOK", btClickSound, btOverSound, device);
                if (btnOK != null)
                {
                    macroUI.InitializeButtons(btnOK, null, null);
                }

                // Load selection highlight texture
                Texture2D selectTexture = LoadCanvasTexture(macroProperty, "select", device);
                if (selectTexture != null)
                {
                    macroUI.SetSelectionTexture(selectTexture);
                }

                // Load macro slot icons from Macroicon
                WzSubProperty macroIconProp = (WzSubProperty)macroProperty["Macroicon"];
                if (macroIconProp != null)
                {
                    Texture2D[] macroIcons = new Texture2D[5];
                    for (int i = 0; i < 5; i++)
                    {
                        macroIcons[i] = LoadCanvasTexture(macroIconProp, i.ToString(), device);
                    }
                    macroUI.SetMacroSlotIcons(macroIcons);
                }

                System.Diagnostics.Debug.WriteLine("[UIWindowLoader] Created SkillMacroUI");
                return macroUI;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Failed to create SkillMacroUI: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Load a canvas texture from a property
        /// </summary>
        private static Texture2D LoadCanvasTexture(WzSubProperty parent, string name, GraphicsDevice device)
        {
            WzObject obj = parent?[name];
            if (obj == null)
                return null;

            try
            {
                System.Drawing.Bitmap bitmap = null;
                if (obj is WzCanvasProperty canvas)
                {
                    bitmap = canvas.GetLinkedWzCanvasBitmap();
                }
                else if (obj is WzSubProperty subProp)
                {
                    // Try to find canvas inside sub-property
                    WzCanvasProperty innerCanvas = subProp["0"] as WzCanvasProperty
                        ?? subProp.WzProperties.FirstOrDefault(p => p is WzCanvasProperty) as WzCanvasProperty;
                    if (innerCanvas != null)
                        bitmap = innerCanvas.GetLinkedWzCanvasBitmap();
                }
                return bitmap?.ToTexture2D(device);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Load a button state texture (normal/pressed/disabled/mouseOver has sub-property "0")
        /// </summary>
        private static Texture2D LoadButtonStateTexture(WzSubProperty buttonProperty, string stateName, GraphicsDevice device)
        {
            WzSubProperty stateProperty = (WzSubProperty)buttonProperty?[stateName];
            if (stateProperty == null)
                return null;

            WzCanvasProperty canvas = (WzCanvasProperty)stateProperty["0"];
            if (canvas == null)
                return null;

            try
            {
                System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                return bitmap?.ToTexture2D(device);
            }
            catch
            {
                return null;
            }
        }

        private static SkillUIBigBang CreatePlaceholderSkillBigBang(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 174;
            int height = 299;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Skills");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            SkillUIBigBang skill = new SkillUIBigBang(frame, device);
            skill.Position = new Point(50, 100);

            return skill;
        }
        #endregion

        #region Quest Window (Big Bang)
        /// <summary>
        /// Create the Quest window - selects pre-BB or post-BB version based on isBigBang
        /// </summary>
        public static UIWindowBase CreateQuestWindowUnified(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            if (isBigBang && uiWindow2Image != null)
            {
                return CreateQuestWindowBigBang(uiWindow2Image, uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            }
            return CreateQuestWindow(uiWindow1Image, soundUIImage, device, screenWidth, screenHeight);
        }

        /// <summary>
        /// Create the Quest window from UI.wz/UIWindow2.img/Quest/list (Post-Big Bang)
        /// </summary>
        public static QuestUIBigBang CreateQuestWindowBigBang(
            WzImage uiWindow2Image, WzImage uiWindow1Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty questProperty = (WzSubProperty)uiWindow2Image?["Quest"];
            WzSubProperty listProperty = (WzSubProperty)questProperty?["list"];
            if (listProperty == null)
            {
                return CreatePlaceholderQuestBigBang(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)listProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderQuestBigBang(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            QuestUIBigBang quest = new QuestUIBigBang(frame, device);
            quest.Position = new Point(50, 100);

            // Load foreground (backgrnd2 - labels/overlay)
            WzCanvasProperty backgrnd2 = (WzCanvasProperty)listProperty["backgrnd2"];
            if (backgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap fgBitmap = backgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D fgTexture = fgBitmap.ToTexture2D(device);
                    IDXObject foreground = new DXObject(0, 0, fgTexture, 0);
                    System.Drawing.PointF? origin = backgrnd2.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 6;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 23;
                    quest.SetForeground(foreground, offsetX, offsetY);
                }
                catch { }
            }

            // Load button sounds
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUIQuestInfo constructor: (214, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 214;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            quest.InitializeCloseButton(closeBtn);

            // Load quest icons from UIWindow2.img/QuestIcon
            WzSubProperty questIconProperty = (WzSubProperty)uiWindow2Image["QuestIcon"];
            if (questIconProperty != null)
            {
                Texture2D iconAvailable = LoadQuestIcon(questIconProperty, "0", device);
                Texture2D iconInProgress = LoadQuestIcon(questIconProperty, "1", device);
                Texture2D iconCompleted = LoadQuestIcon(questIconProperty, "2", device);
                quest.SetQuestIcons(iconAvailable, iconInProgress, iconCompleted);
            }

            return quest;
        }

        private static QuestUIBigBang CreatePlaceholderQuestBigBang(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 235;
            int height = 396;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Quest");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            QuestUIBigBang quest = new QuestUIBigBang(frame, device);
            quest.Position = new Point(50, 100);

            return quest;
        }
        #endregion

        #region Inventory Window (Big Bang)
        /// <summary>
        /// Create the Inventory window - selects pre-BB or post-BB version based on isBigBang
        /// </summary>
        public static UIWindowBase CreateInventoryWindowUnified(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            if (isBigBang && uiWindow2Image != null)
            {
                return CreateInventoryWindowBigBang(uiWindow2Image, uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            }
            return CreateInventoryWindow(uiWindow1Image, soundUIImage, device, screenWidth, screenHeight);
        }

        /// <summary>
        /// Create the Inventory window from UI.wz/UIWindow2.img/Item (Post-Big Bang)
        /// </summary>
        public static InventoryUIBigBang CreateInventoryWindowBigBang(
            WzImage uiWindow2Image, WzImage uiWindow1Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty itemProperty = (WzSubProperty)uiWindow2Image?["Item"];
            if (itemProperty == null)
            {
                return CreatePlaceholderInventoryBigBang(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)itemProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderInventoryBigBang(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            InventoryUIBigBang inventory = new InventoryUIBigBang(frame, device);
            inventory.Position = new Point(screenWidth - bgTexture.Width - 20, 100);

            // Load foreground (backgrnd2 - labels/overlay)
            WzCanvasProperty backgrnd2 = (WzCanvasProperty)itemProperty["backgrnd2"];
            if (backgrnd2 != null)
            {
                try
                {
                    System.Drawing.Bitmap fgBitmap = backgrnd2.GetLinkedWzCanvasBitmap();
                    Texture2D fgTexture = fgBitmap.ToTexture2D(device);
                    IDXObject foreground = new DXObject(0, 0, fgTexture, 0);
                    System.Drawing.PointF? origin = backgrnd2.GetCanvasOriginPosition();
                    int offsetX = origin.HasValue ? -(int)origin.Value.X : 6;
                    int offsetY = origin.HasValue ? -(int)origin.Value.Y : 23;
                    inventory.SetForeground(foreground, offsetX, offsetY);
                }
                catch { }
            }

            // Load expanded view textures (FullBackgrnd, FullBackgrnd2)
            WzCanvasProperty fullBackgrnd = (WzCanvasProperty)itemProperty["FullBackgrnd"];
            WzCanvasProperty fullBackgrnd2 = (WzCanvasProperty)itemProperty["FullBackgrnd2"];
            if (fullBackgrnd != null)
            {
                try
                {
                    System.Drawing.Bitmap fullBgBitmap = fullBackgrnd.GetLinkedWzCanvasBitmap();
                    Texture2D fullBgTexture = fullBgBitmap.ToTexture2D(device);
                    IDXObject expandedFrame = new DXObject(0, 0, fullBgTexture, 0);

                    IDXObject expandedForeground = null;
                    int fgOffsetX = 6, fgOffsetY = 23;
                    if (fullBackgrnd2 != null)
                    {
                        System.Drawing.Bitmap fullFgBitmap = fullBackgrnd2.GetLinkedWzCanvasBitmap();
                        Texture2D fullFgTexture = fullFgBitmap.ToTexture2D(device);
                        expandedForeground = new DXObject(0, 0, fullFgTexture, 0);
                        System.Drawing.PointF? fullOrigin = fullBackgrnd2.GetCanvasOriginPosition();
                        fgOffsetX = fullOrigin.HasValue ? -(int)fullOrigin.Value.X : 6;
                        fgOffsetY = fullOrigin.HasValue ? -(int)fullOrigin.Value.Y : 23;
                    }

                    inventory.SetExpandedView(expandedFrame, expandedForeground, fgOffsetX, fgOffsetY);
                }
                catch { }
            }

            // Load button sounds
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            // Close button - use BtClose from Basic.img (12x12 small X button)
            // Position from CUIItem constructor: (150, 6)
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            UIObject closeBtn = null;
            if (closeButtonProperty != null)
            {
                try
                {
                    closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 150;
                    closeBtn.Y = 6;
                }
                catch { }
            }
            inventory.InitializeCloseButton(closeBtn);

            // Load Big Bang specific buttons
            UIObject btnGather = LoadButton(itemProperty, "BtGather", btClickSound, btOverSound, device);
            UIObject btnSort = LoadButton(itemProperty, "BtSort", btClickSound, btOverSound, device);
            UIObject btnFull = LoadButton(itemProperty, "BtFull", btClickSound, btOverSound, device);
            UIObject btnSmall = LoadButton(itemProperty, "BtSmall", btClickSound, btOverSound, device);
            inventory.InitializeBigBangButtons(btnGather, btnSort, btnFull, btnSmall);

            // Load tab buttons
            WzSubProperty tabProperty = (WzSubProperty)itemProperty["Tab"];
            // Tab buttons would be loaded here if available

            return inventory;
        }

        private static InventoryUIBigBang CreatePlaceholderInventoryBigBang(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 172;
            int height = 293;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Inventory");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            InventoryUIBigBang inventory = new InventoryUIBigBang(frame, device);
            inventory.Position = new Point(screenWidth - width - 20, 100);

            return inventory;
        }
        #endregion

        #region Quest Window
        /// <summary>
        /// Create the Quest window from UI.wz/UIWindow.img/Quest
        /// </summary>
        public static QuestUI CreateQuestWindow(
            WzImage uiWindowImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight)
        {
            WzSubProperty questProperty = (WzSubProperty)uiWindowImage?["Quest"];
            if (questProperty == null)
            {
                return CreatePlaceholderQuest(device, screenWidth, screenHeight);
            }

            // Get main background
            WzCanvasProperty backgrnd = (WzCanvasProperty)questProperty["backgrnd"];
            if (backgrnd == null)
            {
                return CreatePlaceholderQuest(device, screenWidth, screenHeight);
            }

            System.Drawing.Bitmap bgBitmap = backgrnd.GetLinkedWzCanvasBitmap();
            Texture2D bgTexture = bgBitmap.ToTexture2D(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            QuestUI quest = new QuestUI(frame, device);
            quest.Position = new Point(50, 150);

            // Load buttons
            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

            UIObject closeBtn = LoadButton(questProperty, "BtClose", btClickSound, btOverSound, device);
            quest.InitializeCloseButton(closeBtn);

            // Load quest icons
            WzSubProperty questIconProperty = (WzSubProperty)uiWindowImage["QuestIcon"];
            if (questIconProperty != null)
            {
                Texture2D iconAvailable = LoadQuestIcon(questIconProperty, "0", device);
                Texture2D iconInProgress = LoadQuestIcon(questIconProperty, "1", device);
                Texture2D iconCompleted = LoadQuestIcon(questIconProperty, "2", device);
                quest.SetQuestIcons(iconAvailable, iconInProgress, iconCompleted);
            }

            return quest;
        }

        private static QuestUI CreatePlaceholderQuest(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            int width = 220;
            int height = 350;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, "Quest Log");
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            QuestUI quest = new QuestUI(frame, device);
            quest.Position = new Point(50, 150);

            return quest;
        }

        private static Texture2D LoadQuestIcon(WzSubProperty questIconProperty, string iconNum, GraphicsDevice device)
        {
            WzSubProperty iconSub = (WzSubProperty)questIconProperty[iconNum];
            if (iconSub != null)
            {
                WzCanvasProperty canvas = (WzCanvasProperty)iconSub["0"];
                if (canvas != null)
                {
                    return canvas.GetLinkedWzCanvasBitmap().ToTexture2D(device);
                }
            }
            return null;
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Load a standard button from WZ property
        /// </summary>
        private static UIObject LoadButton(WzSubProperty parent, string buttonName,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzSubProperty buttonProperty = (WzSubProperty)parent?[buttonName];
            if (buttonProperty == null)
                return null;

            try
            {
                return new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Load a tab button from WZ property
        /// </summary>
        private static UIObject LoadTabButton(WzSubProperty parent, string tabName,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzSubProperty tabProperty = (WzSubProperty)parent?[tabName];
            if (tabProperty == null)
                return null;

            try
            {
                return new UIObject(tabProperty, clickSound, overSound, false, Point.Zero, device);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create a placeholder window texture when WZ assets aren't available
        /// </summary>
        private static Texture2D CreatePlaceholderWindowTexture(GraphicsDevice device, int width, int height, string title)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];

            // Window background color
            Color bgColor = new Color(40, 40, 60, 230);
            Color titleBarColor = new Color(60, 60, 90, 255);
            Color borderColor = new Color(80, 80, 120, 255);

            int titleBarHeight = 25;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;

                    // Border
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    {
                        data[index] = borderColor;
                    }
                    // Title bar
                    else if (y < titleBarHeight)
                    {
                        data[index] = titleBarColor;
                    }
                    // Title bar bottom border
                    else if (y == titleBarHeight)
                    {
                        data[index] = borderColor;
                    }
                    // Background
                    else
                    {
                        data[index] = bgColor;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }
        #endregion

        #region UIWindowManager Factory
        /// <summary>
        /// Create and initialize a UIWindowManager with all windows
        /// </summary>
        public static UIWindowManager CreateUIWindowManager(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang)
        {
            return CreateUIWindowManager(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage,
                null, null, device, screenWidth, screenHeight, isBigBang, 212); // Default to Arch Mage F/P (job 212)
        }

        /// <summary>
        /// Create and initialize a UIWindowManager with all windows and skill loading support
        /// </summary>
        public static UIWindowManager CreateUIWindowManager(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            WzFile skillWzFile, WzFile stringWzFile,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang, int jobId = 212)
        {
            UIWindowManager manager = new UIWindowManager();

            // Create windows - use unified methods that select pre-BB or post-BB based on flag
            UIWindowBase inventory = CreateInventoryWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase equip = CreateEquipWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase skill = CreateSkillWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase quest = CreateQuestWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase ability = CreateAbilityWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);

            // Load skills for the character's job into skill window
            // This loads all skills from beginner through current job
            if (skill is SkillUIBigBang skillBigBang)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Loading skills for job {jobId} into SkillUIBigBang");
                LoadSkillsForJob(skillBigBang, jobId, device);
            }

            // Create skill macro window (post-BB only)
            SkillMacroUI skillMacro = null;
            if (isBigBang)
            {
                skillMacro = CreateSkillMacroWindowBigBang(uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);
            }

            // Register with manager
            manager.RegisterInventoryWindow(inventory);
            manager.RegisterEquipWindow(equip);
            manager.RegisterSkillWindow(skill);
            manager.RegisterQuestWindow(quest);
            manager.RegisterAbilityWindow(ability);

            if (skillMacro != null)
            {
                manager.RegisterSkillMacroWindow(skillMacro);

                // Wire up the MACRO button in skill window to open the macro window
                if (skill is SkillUIBigBang skillBB && skillBB.MacroButton != null)
                {
                    var macroWindow = skillMacro;
                    skillBB.MacroButton.ButtonClickReleased += (sender) =>
                    {
                        if (macroWindow != null)
                        {
                            macroWindow.Show();
                            manager.BringToFront(macroWindow);
                        }
                    };
                }
            }

            return manager;
        }
        #endregion
    }
}
