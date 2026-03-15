using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);

            // Create the inventory window
            InventoryUI inventory = new InventoryUI(frame, null, device);
            inventory.Position = new Point(screenWidth - bgTexture.Width - 20, 100);
            inventory.SetRenderAssets(
                LoadCanvasTexture(itemProperty, "activeIcon", device),
                LoadCanvasTexture(itemProperty, "disabled", device),
                LoadCanvasTexture(itemProperty, "shadow", device),
                LoadInventoryMarkerTextures(itemProperty, "Grade", device));

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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D fgTexture = fgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D slotTexture = slotBitmap.ToTexture2DAndDispose(device);
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
            LoadEquipCompanionTabLayout(equip, equipProperty, "pet", 1, device);
            LoadEquipCompanionTabLayout(equip, equipProperty, "dragon", 2, device);
            LoadEquipCompanionTabLayout(equip, equipProperty, "mechanic", 3, device);
            LoadEquipCompanionTabLayout(equip, equipProperty, "Android", 4, device);

            WzSubProperty skillMainProperty = uiWindow2Image?["Skill"]?["main"] as WzSubProperty;
            if (skillMainProperty != null)
            {
                Texture2D[] tooltipFrames = new Texture2D[3];
                tooltipFrames[0] = LoadCanvasTexture(skillMainProperty, "tip0", device);
                tooltipFrames[1] = LoadCanvasTexture(skillMainProperty, "tip1", device);
                tooltipFrames[2] = LoadCanvasTexture(skillMainProperty, "tip2", device);
                equip.SetTooltipTextures(tooltipFrames);
            }

            return equip;
        }

        private static void LoadEquipCompanionTabLayout(EquipUIBigBang equip, WzSubProperty equipProperty, string propertyName, int tabIndex, GraphicsDevice device)
        {
            if (equipProperty?[propertyName] is not WzSubProperty tabProperty)
            {
                return;
            }

            IDXObject frame = LoadCanvasObject(tabProperty, "backgrnd", device, out Point _);
            if (frame == null)
            {
                return;
            }

            IDXObject foreground = LoadCanvasObject(tabProperty, "backgrnd2", device, out Point foregroundOffset);
            IDXObject slotLabels = LoadCanvasObject(tabProperty, "backgrnd3", device, out Point slotLabelOffset);
            equip.SetCompanionTabLayout(
                tabIndex,
                frame,
                foreground,
                foregroundOffset.X,
                foregroundOffset.Y,
                slotLabels,
                slotLabelOffset.X,
                slotLabelOffset.Y);
        }

        private static IDXObject LoadCanvasObject(WzSubProperty parent, string canvasName, GraphicsDevice device, out Point offset)
        {
            offset = Point.Zero;
            if (parent?[canvasName] is not WzCanvasProperty canvas)
            {
                return null;
            }

            try
            {
                System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                Texture2D texture = bitmap.ToTexture2DAndDispose(device);
                System.Drawing.PointF? origin = canvas.GetCanvasOriginPosition();
                if (origin.HasValue)
                {
                    offset = new Point(-(int)origin.Value.X, -(int)origin.Value.Y);
                }

                return new DXObject(0, 0, texture, 0);
            }
            catch
            {
                return null;
            }
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D detailBgTexture = detailBgBitmap.ToTexture2DAndDispose(device);
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D fgTexture = fgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D detailBgTexture = detailBgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D detailFgTexture = detailFgBitmap.ToTexture2DAndDispose(device);
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D fgTexture = fgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D bg3Texture = bg3Bitmap.ToTexture2DAndDispose(device);
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
            Texture2D recommendTexture = LoadCanvasTexture(mainProperty["recommend"] as WzSubProperty, "0", device);
            Texture2D skillLine = LoadCanvasTexture(mainProperty, "line", device);
            skill.SetSkillRowTextures(skillRow0, skillRow1, skillLine);
            skill.SetRecommendTexture(recommendTexture);
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

            Texture2D[] tooltipFrames =
            {
                LoadCanvasTexture(mainProperty, "tip0", device),
                LoadCanvasTexture(mainProperty, "tip1", device),
                LoadCanvasTexture(mainProperty, "tip2", device)
            };
            skill.SetTooltipTextures(tooltipFrames);

            WzSubProperty vScrollProperty = (WzSubProperty)basicImage?["VScr"];
            if (vScrollProperty != null)
            {
                WzSubProperty enabledProperty = (WzSubProperty)vScrollProperty["enabled"];
                WzSubProperty disabledProperty = (WzSubProperty)vScrollProperty["disabled"];
                skill.SetScrollBarTextures(
                    LoadCanvasTexture(enabledProperty, "prev0", device),
                    LoadCanvasTexture(enabledProperty, "prev1", device),
                    LoadCanvasTexture(enabledProperty, "next0", device),
                    LoadCanvasTexture(enabledProperty, "next1", device),
                    LoadCanvasTexture(enabledProperty, "base", device),
                    LoadCanvasTexture(enabledProperty, "thumb0", device),
                    LoadCanvasTexture(enabledProperty, "thumb1", device),
                    LoadCanvasTexture(disabledProperty, "prev", device),
                    LoadCanvasTexture(disabledProperty, "next", device),
                    LoadCanvasTexture(disabledProperty, "base", device));
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
        /// Load skills for a character's job into a skill window.
        /// Standard jobs populate their advancement path across tabs; admin jobs stay focused on a single book.
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
                // Clear any previously loaded skills.
                skillWindow.ClearSkills();

                var pathJobIds = GetDisplayedSkillBookJobIdsForJob(jobId);
                var visibleTabs = new HashSet<int>();
                foreach (int pathJobId in pathJobIds)
                {
                    visibleTabs.Add(GetSkillTabFromJobId(pathJobId));
                }

                // `CUISkill::GetSkillRootVisible` refreshes the visible skill roots from
                // the current job path. Mirror that at the tab layer so the simulator only
                // exposes books the active job can actually browse.
                skillWindow.SetVisibleTabs(visibleTabs);

                // Seed the default beginner book so tabs without a dedicated skill book
                // can still render the same fallback icon the client uses.
                Texture2D defaultBookIcon = SkillDataLoader.LoadJobIcon(0, device);
                if (defaultBookIcon != null)
                {
                    skillWindow.SetJobInfo(0, defaultBookIcon, SkillDataLoader.GetJobName(0));
                }

                foreach (int pathJobId in pathJobIds)
                {
                    int tabIndex = GetSkillTabFromJobId(pathJobId);
                    System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Loading skills for display job {pathJobId} into tab {tabIndex} (requested job {jobId})");

                    var skillMap = new Dictionary<int, SkillDisplayData>();
                    foreach (int bookJobId in GetSkillBookAliasesForJob(pathJobId))
                    {
                        var skills = SkillDataLoader.LoadSkillsForJob(bookJobId, device);
                        foreach (var skill in skills)
                        {
                            if (skill == null)
                                continue;

                            if (!skillMap.ContainsKey(skill.SkillId))
                                skillMap[skill.SkillId] = skill;
                        }
                    }

                    var mergedSkills = skillMap.Values.ToList();
                    skillWindow.AddSkills(tabIndex, mergedSkills);
                    skillWindow.SetRecommendedSkillEntries(
                        tabIndex,
                        SkillDataLoader.LoadRecommendedSkillEntries(
                            pathJobId,
                            mergedSkills.Select(skill => skill.SkillId)));
                    System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Tab {tabIndex}: Loaded {mergedSkills.Count} skills for display job {pathJobId}");

                    // Load and set the job icon and name for the populated tab.
                    Texture2D jobIcon = SkillDataLoader.LoadJobIcon(pathJobId, device);
                    if (jobIcon == null)
                    {
                        // Fallback for jobs where the icon lives in another book (e.g. GM).
                        foreach (int bookJobId in GetSkillBookAliasesForJob(pathJobId))
                        {
                            jobIcon = SkillDataLoader.LoadJobIcon(bookJobId, device);
                            if (jobIcon != null)
                                break;
                        }
                    }

                    string jobName = SkillDataLoader.GetJobName(pathJobId);
                    skillWindow.SetJobInfo(tabIndex, jobIcon, jobName);
                }

                // Show the populated tab by default.
                skillWindow.CurrentTab = GetSkillTabFromJobId(jobId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Failed to load skills: {ex.Message}");
            }
        }

        /// <summary>
        /// Load the full skill catalog into the skill window, grouped by advancement tab.
        /// </summary>
        public static void LoadAllSkills(SkillUIBigBang skillWindow, WzFile skillWzFile, GraphicsDevice device, int focusJobId = 0)
        {
            if (skillWindow == null)
                return;

            try
            {
                if (ShouldLoadFocusedJobOnly(focusJobId))
                {
                    LoadSkillsForJob(skillWindow, focusJobId, device);
                    return;
                }

                skillWindow.ClearSkills();
                skillWindow.SetVisibleTabs(new[] { 0, 1, 2, 3, 4 });

                var skillsByTab = new Dictionary<int, Dictionary<int, SkillDisplayData>>
                {
                    { 0, new Dictionary<int, SkillDisplayData>() },
                    { 1, new Dictionary<int, SkillDisplayData>() },
                    { 2, new Dictionary<int, SkillDisplayData>() },
                    { 3, new Dictionary<int, SkillDisplayData>() },
                    { 4, new Dictionary<int, SkillDisplayData>() }
                };

                var defaultIcon = SkillDataLoader.LoadJobIcon(0, device);
                skillWindow.SetJobInfo(0, defaultIcon, "All Beginner Skills");
                skillWindow.SetJobInfo(1, defaultIcon, "All 1st Job Skills");
                skillWindow.SetJobInfo(2, defaultIcon, "All 2nd Job Skills");
                skillWindow.SetJobInfo(3, defaultIcon, "All 3rd Job Skills");
                skillWindow.SetJobInfo(4, defaultIcon, "All 4th Job Skills");

                var availableBookIds = SkillDataLoader.GetAvailableSkillBookJobIds(skillWzFile);
                if (availableBookIds.Count == 0)
                {
                    LoadSkillsForJob(skillWindow, focusJobId, device);
                    return;
                }

                foreach (int bookJobId in availableBookIds)
                {
                    int tabIndex = GetSkillTabFromJobId(bookJobId);

                    foreach (int resolvedBookJobId in GetSkillBookAliasesForJob(bookJobId))
                    {
                        var skills = SkillDataLoader.LoadSkillsForJob(resolvedBookJobId, device);
                        foreach (var skill in skills)
                        {
                            if (skill == null)
                                continue;

                            if (!skillsByTab[tabIndex].ContainsKey(skill.SkillId))
                            {
                                skillsByTab[tabIndex][skill.SkillId] = skill;
                            }
                        }
                    }
                }

                for (int tab = 0; tab <= 4; tab++)
                {
                    skillWindow.AddSkills(tab, skillsByTab[tab].Values);
                }

                int focusTab = GetSkillTabFromJobId(focusJobId);
                skillWindow.CurrentTab = focusTab;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Failed to load full skill catalog: {ex.Message}");
            }
        }

        /// <summary>
        /// Map a job id to a SkillUIBigBang tab index (0..4).
        /// This is a heuristic based on common MapleStory job id patterns:
        /// - 0 => beginner
        /// - xx00 => 1st job
        /// - xx10/xx20/... ending with 0 => 2nd job
        /// - ending with 1 => 3rd job
        /// - ending with 2 => 4th job
        /// </summary>
        private static int GetSkillTabFromJobId(int jobId)
        {
            if (jobId <= 0)
                return 0;

            // Special jobs (Manager/GM/SuperGM) should still show up on the first job tab.
            if (jobId >= 800 && jobId < 1000)
                return 1;

            // 100, 200, 300, 1100, 1200, 3000, etc.
            if (jobId % 100 == 0)
                return 1;

            return (jobId % 10) switch
            {
                0 => 2,
                1 => 3,
                2 => 4,
                _ => 1
            };
        }

        private static IReadOnlyList<int> GetSkillBookAliasesForJob(int jobId)
        {
            return jobId switch
            {
                900 => new[] { 900, 910 },
                910 => new[] { 910, 900 },
                _ => new[] { jobId }
            };
        }

        private static IReadOnlyList<int> GetDisplayedSkillBookJobIdsForJob(int jobId)
        {
            if (ShouldLoadFocusedJobOnly(jobId))
                return GetSkillBookAliasesForJob(jobId);

            var path = new List<int> { 0 };
            if (jobId <= 0)
                return path;

            int firstJob = (jobId / 100) * 100;
            if (firstJob > 0 && !path.Contains(firstJob))
                path.Add(firstJob);

            int secondJob = (jobId / 10) * 10;
            if (secondJob > firstJob && !path.Contains(secondJob))
                path.Add(secondJob);

            int thirdJob = secondJob + (jobId % 10 > 0 ? 1 : 0);
            if (thirdJob > secondJob && thirdJob < jobId && !path.Contains(thirdJob))
                path.Add(thirdJob);

            if (!path.Contains(jobId))
                path.Add(jobId);

            return path;
        }

        private static bool ShouldLoadFocusedJobOnly(int jobId)
        {
            return jobId >= 800 && jobId < 1000;
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

                Texture2D bgTexture = bgBitmap?.ToTexture2DAndDispose(device);
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
                return bitmap?.ToTexture2DAndDispose(device);
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
                return bitmap?.ToTexture2DAndDispose(device);
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D fgTexture = fgBitmap.ToTexture2DAndDispose(device);
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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D fgTexture = fgBitmap.ToTexture2DAndDispose(device);
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
                    Texture2D fullBgTexture = fullBgBitmap.ToTexture2DAndDispose(device);
                    IDXObject expandedFrame = new DXObject(0, 0, fullBgTexture, 0);

                    IDXObject expandedForeground = null;
                    int fgOffsetX = 6, fgOffsetY = 23;
                    if (fullBackgrnd2 != null)
                    {
                        System.Drawing.Bitmap fullFgBitmap = fullBackgrnd2.GetLinkedWzCanvasBitmap();
                        Texture2D fullFgTexture = fullFgBitmap.ToTexture2DAndDispose(device);
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
            inventory.SetRenderAssets(
                LoadCanvasTexture(itemProperty, "activeIcon", device),
                LoadCanvasTexture(itemProperty, "disabled", device),
                LoadCanvasTexture(itemProperty, "shadow", device),
                LoadInventoryMarkerTextures(itemProperty, "Quality", device));

            UIObject tabEquip = LoadInventoryCanvasTabButton(itemProperty, "0", btClickSound, btOverSound, device);
            UIObject tabUse = LoadInventoryCanvasTabButton(itemProperty, "1", btClickSound, btOverSound, device);
            UIObject tabSetup = LoadInventoryCanvasTabButton(itemProperty, "2", btClickSound, btOverSound, device);
            UIObject tabEtc = LoadInventoryCanvasTabButton(itemProperty, "3", btClickSound, btOverSound, device);
            UIObject tabCash = LoadInventoryCanvasTabButton(itemProperty, "4", btClickSound, btOverSound, device);
            inventory.InitializeTabs(tabEquip, tabUse, tabSetup, tabEtc, tabCash);

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
            Texture2D bgTexture = bgBitmap.ToTexture2DAndDispose(device);
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

        private static QuestDetailWindow CreateQuestDetailWindowUnified(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            bool isBigBang)
        {
            QuestDetailWindow window = isBigBang
                ? CreateQuestDetailWindowBigBang(uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight)
                : CreateQuestDetailWindowPreBigBang(uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);

            if (window != null)
            {
                return window;
            }

            return CreatePlaceholderQuestDetailWindow(device, basicImage, soundUIImage, screenWidth, screenHeight);
        }

        private static QuestDetailWindow CreateQuestDetailWindowBigBang(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty questInfoProperty = uiWindow2Image?["Quest"]?["quest_info"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = questInfoProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            QuestDetailWindow window = CreateQuestDetailWindowShell(device, frameTexture, screenWidth, screenHeight);

            Texture2D foregroundTexture = LoadCanvasTexture(questInfoProperty, "backgrnd2", device);
            if (foregroundTexture != null)
            {
                window.SetForeground(new DXObject(0, 0, foregroundTexture, 0), ResolveCanvasOffset(questInfoProperty, "backgrnd2", new Point(6, 23)));
            }

            Texture2D panelTexture = LoadCanvasTexture(questInfoProperty, "backgrnd3", device);
            if (panelTexture != null)
            {
                window.SetBottomPanel(new DXObject(0, 0, panelTexture, 0), ResolveCanvasOffset(questInfoProperty, "backgrnd3", new Point(10, 27)));
            }

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            InitializeQuestDetailButtons(window, device, frameTexture.Width, frameTexture.Height);
            return window;
        }

        private static QuestDetailWindow CreateQuestDetailWindowPreBigBang(
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty questProperty = uiWindow1Image?["Quest"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = questProperty?["backgrnd2"] as WzCanvasProperty ?? questProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            QuestDetailWindow window = CreateQuestDetailWindowShell(device, frameTexture, screenWidth, screenHeight);

            Texture2D panelTexture = LoadCanvasTexture(questProperty, "backgrnd5", device);
            if (panelTexture != null)
            {
                window.SetBottomPanel(new DXObject(0, 0, panelTexture, 0), ResolveCanvasOffset(questProperty, "backgrnd5", new Point(20, 252)));
            }

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            InitializeQuestDetailButtons(window, device, frameTexture.Width, frameTexture.Height);
            return window;
        }

        private static QuestDetailWindow CreatePlaceholderQuestDetailWindow(
            GraphicsDevice device,
            WzImage basicImage,
            WzImage soundUIImage,
            int screenWidth,
            int screenHeight)
        {
            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 296, 396, "Quest Detail");
            QuestDetailWindow window = CreateQuestDetailWindowShell(device, frameTexture, screenWidth, screenHeight);
            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            InitializeQuestDetailButtons(window, device, frameTexture.Width, frameTexture.Height);
            return window;
        }

        private static QuestDetailWindow CreateQuestDetailWindowShell(
            GraphicsDevice device,
            Texture2D frameTexture,
            int screenWidth,
            int screenHeight)
        {
            return new QuestDetailWindow(new DXObject(0, 0, frameTexture, 0), MapSimulatorWindowNames.QuestDetail)
            {
                Position = new Point(
                    Math.Max(40, (screenWidth / 2) - (frameTexture.Width / 2)),
                    Math.Max(40, (screenHeight / 2) - (frameTexture.Height / 2)))
            };
        }

        private static void InitializeQuestDetailButtons(QuestDetailWindow window, GraphicsDevice device, int frameWidth, int frameHeight)
        {
            UIObject primaryButton = UiButtonFactory.CreateSolidButton(
                device, 78, 22,
                new Color(69, 95, 122, 230),
                new Color(101, 131, 160, 240),
                new Color(82, 110, 140, 240),
                new Color(42, 42, 42, 170));
            primaryButton.X = Math.Max(16, frameWidth - 96);
            primaryButton.Y = Math.Max(16, frameHeight - 32);

            UIObject secondaryButton = UiButtonFactory.CreateSolidButton(
                device, 78, 22,
                new Color(77, 63, 63, 225),
                new Color(118, 88, 88, 235),
                new Color(98, 75, 75, 235),
                new Color(42, 42, 42, 170));
            secondaryButton.X = Math.Max(16, frameWidth - 180);
            secondaryButton.Y = primaryButton.Y;

            window.InitializeActionButtons(primaryButton, secondaryButton);
            window.InitializeNavigationButtons(device);
        }

        private static QuickSlotUI CreateQuickSlotWindow(WzImage uiWindow2Image, GraphicsDevice device, int screenWidth, int screenHeight)
        {
            const int width = 286;
            const int height = 96;

            Texture2D frameTexture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];
            Color fill = new Color(18, 24, 34, 130);
            Color border = new Color(85, 98, 120, 180);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    data[(y * width) + x] = isBorder ? border : fill;
                }
            }

            frameTexture.SetData(data);

            IDXObject frame = new DXObject(0, 0, frameTexture, 0);
            QuickSlotUI quickSlot = new QuickSlotUI(frame, device);
            quickSlot.Position = new Point((screenWidth - width) / 2, Math.Max(20, screenHeight - height - 120));

            WzSubProperty skillProperty = uiWindow2Image?["Skill"] as WzSubProperty;
            WzSubProperty mainProperty = skillProperty?["main"] as WzSubProperty;
            WzSubProperty coolTimeProperty = mainProperty?["CoolTime"] as WzSubProperty;
            if (coolTimeProperty != null)
            {
                Texture2D[] cooldownMasks = new Texture2D[16];
                for (int i = 0; i < cooldownMasks.Length; i++)
                {
                    cooldownMasks[i] = LoadCanvasTexture(coolTimeProperty, i.ToString(), device);
                }

                quickSlot.SetCooldownMasks(cooldownMasks);
            }

            if (mainProperty != null)
            {
                Texture2D[] tooltipFrames = new Texture2D[3];
                tooltipFrames[0] = LoadCanvasTexture(mainProperty, "tip0", device);
                tooltipFrames[1] = LoadCanvasTexture(mainProperty, "tip1", device);
                tooltipFrames[2] = LoadCanvasTexture(mainProperty, "tip2", device);
                quickSlot.SetTooltipTextures(tooltipFrames);
            }

            quickSlot.Show();
            return quickSlot;
        }

        private static MapTransferUI CreateMapTransferWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty teleportProperty =
                uiWindow2Image?["Teleport3"] as WzSubProperty ??
                uiWindow2Image?["Teleport2"] as WzSubProperty ??
                uiWindow2Image?["Teleport"] as WzSubProperty ??
                uiWindow1Image?["Teleport"] as WzSubProperty;
            if (teleportProperty == null)
            {
                return null;
            }

            WzCanvasProperty backgroundProperty = teleportProperty["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            IDXObject frame = new DXObject(0, 0, frameTexture, 0);
            MapTransferUI window = new MapTransferUI(
                frame,
                LoadWindowCanvasLayer(teleportProperty, "backgrnd2", device),
                LoadWindowCanvasLayer(teleportProperty, "backgrnd3", device),
                LoadCanvasTexture(teleportProperty, "select", device),
                LoadButton(teleportProperty, "BtRegister", btClickSound, btOverSound, device),
                LoadButton(teleportProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(teleportProperty, "BtMove", btClickSound, btOverSound, device),
                LoadButton(teleportProperty, "BtMap", btClickSound, btOverSound, device),
                device);

            window.Position = new Point(
                Math.Max(24, screenWidth - frameTexture.Width - 44),
                Math.Max(36, (screenHeight - frameTexture.Height) / 2));

            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            return window;
        }

        private static Texture2D LoadQuestIcon(WzSubProperty questIconProperty, string iconNum, GraphicsDevice device)
        {
            WzSubProperty iconSub = (WzSubProperty)questIconProperty[iconNum];
            if (iconSub != null)
            {
                WzCanvasProperty canvas = (WzCanvasProperty)iconSub["0"];
                if (canvas != null)
                {
                    return canvas.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
                }
            }
            return null;
        }

        private static UserInfoUI CreateCharacterInfoWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            bool isBigBang)
        {
            if (isBigBang)
            {
                UserInfoUI bigBangWindow = CreateCharacterInfoWindowBigBang(
                    uiWindow2Image,
                    basicImage,
                    soundUIImage,
                    device,
                    screenWidth,
                    screenHeight);
                if (bigBangWindow != null)
                {
                    return bigBangWindow;
                }
            }

            return CreateCharacterInfoWindowPreBigBang(
                uiWindow1Image,
                basicImage,
                soundUIImage,
                device,
                screenWidth,
                screenHeight);
        }

        private static UserInfoUI CreateCharacterInfoWindowPreBigBang(
            WzImage uiWindowImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty userInfoProperty = uiWindowImage?["UserInfo"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = userInfoProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }

            UserInfoUI window = new UserInfoUI(new DXObject(0, 0, frameTexture, 0), false)
            {
                Position = new Point(
                    Math.Max(40, (screenWidth / 2) - (frameTexture.Width / 2)),
                    Math.Max(40, (screenHeight / 2) - (frameTexture.Height / 2)))
            };

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            window.InitializeDecorativeButtons(
                LoadButton(userInfoProperty, "BtParty", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtTrade", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtItem", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtWish", clickSound, overSound, device),
                LoadButton(userInfoProperty, "BtFamily", clickSound, overSound, device));
            return window;
        }

        private static UserInfoUI CreateCharacterInfoWindowBigBang(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty userInfoProperty = uiWindow2Image?["UserInfo"] as WzSubProperty;
            WzSubProperty characterProperty = userInfoProperty?["character"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = characterProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }

            UserInfoUI window = new UserInfoUI(new DXObject(0, 0, frameTexture, 0), true)
            {
                Position = new Point(
                    Math.Max(40, (screenWidth / 2) - (frameTexture.Width / 2)),
                    Math.Max(40, (screenHeight / 2) - (frameTexture.Height / 2)))
            };

            Texture2D foregroundTexture = LoadCanvasTexture(characterProperty, "backgrnd2", device);
            if (foregroundTexture != null)
            {
                IDXObject foreground = new DXObject(0, 0, foregroundTexture, 0);
                Point foregroundOffset = ResolveCanvasOffset(characterProperty, "backgrnd2", new Point(6, 23));
                window.SetForeground(foreground, foregroundOffset.X, foregroundOffset.Y);
            }

            Texture2D nameBannerTexture = LoadCanvasTexture(characterProperty, "backgrnd3", device);
            if (nameBannerTexture != null)
            {
                IDXObject nameBanner = new DXObject(0, 0, nameBannerTexture, 0);
                Point bannerOffset = ResolveCanvasOffset(characterProperty, "backgrnd3", new Point(14, 151));
                window.SetNameBanner(nameBanner, bannerOffset.X, bannerOffset.Y);
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            window.InitializeDecorativeButtons(
                LoadButton(characterProperty, "BtParty", clickSound, overSound, device),
                LoadButton(characterProperty, "BtTrad", clickSound, overSound, device),
                LoadButton(characterProperty, "BtItem", clickSound, overSound, device),
                LoadButton(characterProperty, "BtWish", clickSound, overSound, device),
                LoadButton(characterProperty, "BtFamily", clickSound, overSound, device),
                LoadButton(characterProperty, "BtRide", clickSound, overSound, device),
                LoadButton(characterProperty, "BtPet", clickSound, overSound, device),
                LoadButton(characterProperty, "BtCollect", clickSound, overSound, device),
                LoadButton(characterProperty, "BtPersonality", clickSound, overSound, device));
            return window;
        }

        private static UIObject CreateUserInfoCloseButton(
            WzImage basicImage,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int windowWidth)
        {
            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty == null)
            {
                return null;
            }

            try
            {
                UIObject closeButton = new UIObject(closeButtonProperty, clickSound, overSound, false, Point.Zero, device);
                closeButton.X = windowWidth - closeButton.CanvasSnapshotWidth - 8;
                closeButton.Y = 7;
                return closeButton;
            }
            catch
            {
                return null;
            }
        }

        private static UIObject CreateTextureButton(Texture2D normalTexture, Texture2D pressedTexture)
        {
            if (normalTexture == null)
            {
                return null;
            }

            BaseDXDrawableItem normal = new BaseDXDrawableItem(new DXObject(0, 0, normalTexture, 0), false);
            BaseDXDrawableItem disabled = new BaseDXDrawableItem(new DXObject(0, 0, normalTexture, 0), false);
            Texture2D activeTexture = pressedTexture ?? normalTexture;
            BaseDXDrawableItem pressed = new BaseDXDrawableItem(new DXObject(0, 0, activeTexture, 0), false);
            BaseDXDrawableItem mouseOver = new BaseDXDrawableItem(new DXObject(0, 0, activeTexture, 0), false);
            return new UIObject(normal, disabled, pressed, mouseOver);
        }

        private static Point GetCanvasOffset(WzCanvasProperty canvas)
        {
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            return origin.HasValue
                ? new Point(-(int)origin.Value.X, -(int)origin.Value.Y)
                : Point.Zero;
        }

        private static Texture2D CreateSolidTexture(GraphicsDevice device, Color color)
        {
            Texture2D texture = new Texture2D(device, 1, 1);
            texture.SetData(new[] { color });
            return texture;
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

        private static UIObject LoadInventoryCanvasTabButton(WzSubProperty itemProperty, string tabIndex,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzCanvasProperty enabledCanvas = itemProperty?["Tab"]?["enabled"]?[tabIndex] as WzCanvasProperty;
            WzCanvasProperty disabledCanvas = itemProperty?["Tab"]?["disabled"]?[tabIndex] as WzCanvasProperty;
            if (enabledCanvas == null || disabledCanvas == null)
            {
                return null;
            }

            try
            {
                Texture2D enabledTexture = enabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Texture2D disabledTexture = disabledCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (enabledTexture == null || disabledTexture == null)
                {
                    return null;
                }

                Point enabledOffset = ResolveCanvasOffset(enabledCanvas, Point.Zero);
                Point disabledOffset = ResolveCanvasOffset(disabledCanvas, Point.Zero);
                BaseDXDrawableItem normalState = new BaseDXDrawableItem(new DXObject(disabledOffset.X, disabledOffset.Y, disabledTexture), false);
                BaseDXDrawableItem pressedState = new BaseDXDrawableItem(new DXObject(enabledOffset.X, disabledOffset.Y, enabledTexture), false);
                UIObject button = new UIObject(normalState, normalState, pressedState, pressedState);
                button.X = disabledOffset.X;
                button.Y = disabledOffset.Y;
                return button;
            }
            catch
            {
                return null;
            }
        }

        private static Texture2D[] LoadInventoryMarkerTextures(WzSubProperty itemProperty, string markerFamilyName, GraphicsDevice device)
        {
            Texture2D[] textures = new Texture2D[6];
            WzSubProperty markerFamily = itemProperty?[markerFamilyName] as WzSubProperty;
            if (markerFamily == null)
            {
                return textures;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = LoadCanvasTexture(markerFamily, i.ToString(), device);
            }

            return textures;
        }

        private static Point ResolveCanvasOffset(WzSubProperty parent, string name, Point fallback)
        {
            WzCanvasProperty canvas = parent?[name] as WzCanvasProperty;
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return fallback;
            }

            return new Point(-(int)origin.Value.X, -(int)origin.Value.Y);
        }

        private static Point ResolveCanvasOffset(WzCanvasProperty canvas, Point fallback)
        {
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return fallback;
            }

            return new Point(-(int)origin.Value.X, -(int)origin.Value.Y);
        }

        private static IDXObject LoadWindowCanvasLayer(WzSubProperty parent, string name, GraphicsDevice device)
        {
            WzCanvasProperty canvas = parent?[name] as WzCanvasProperty;
            if (canvas == null)
            {
                return null;
            }

            Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (texture == null)
            {
                return null;
            }

            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            return new DXObject(origin, texture, 0);
        }

        private static IDXObject LoadWindowCanvasLayerWithOffset(WzSubProperty parent, string name, GraphicsDevice device, out Point offset)
        {
            offset = Point.Zero;

            WzCanvasProperty canvas = parent?[name] as WzCanvasProperty;
            if (canvas == null)
            {
                return null;
            }

            Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (texture == null)
            {
                return null;
            }

            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            offset = new Point(-(int)origin.X, -(int)origin.Y);
            return new DXObject(0, 0, texture, 0);
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
                null, null, device, screenWidth, screenHeight, isBigBang, 900); // Default to GM book (900 in v115-style data)
        }

        /// <summary>
        /// Create and initialize a UIWindowManager with all windows and skill loading support
        /// </summary>
        public static UIWindowManager CreateUIWindowManager(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            WzFile skillWzFile, WzFile stringWzFile,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang, int jobId = 900)
        {
            UIWindowManager manager = new UIWindowManager();

            // Create windows - use unified methods that select pre-BB or post-BB based on flag
            UIWindowBase inventory = CreateInventoryWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase equip = CreateEquipWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase skill = CreateSkillWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase quest = CreateQuestWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            QuestDetailWindow questDetail = CreateQuestDetailWindowUnified(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UIWindowBase ability = CreateAbilityWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            UserInfoUI characterInfo = CreateCharacterInfoWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, isBigBang);
            QuickSlotUI quickSlot = CreateQuickSlotWindow(uiWindow2Image, device, screenWidth, screenHeight);

            // Seed the skill window with the requested job path only.
            if (skill is SkillUIBigBang skillBigBang)
            {
                System.Diagnostics.Debug.WriteLine($"[UIWindowLoader] Loading job skills into SkillUIBigBang for job {jobId}");
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
            manager.RegisterQuestDetailWindow(questDetail);
            manager.RegisterAbilityWindow(ability);
            manager.RegisterQuickSlotWindow(quickSlot);
            if (characterInfo != null)
            {
                manager.RegisterCustomWindow(characterInfo);
            }

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

            SeedStarterCraftingInventory(manager.InventoryWindow as IInventoryRuntime);

            MapTransferUI mapTransfer = CreateMapTransferWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            if (mapTransfer != null)
            {
                manager.RegisterCustomWindow(mapTransfer);
            }

            RegisterProgressionUtilityPlaceholderWindows(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);

            return manager;
        }

        private static void RegisterProgressionUtilityPlaceholderWindows(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null)
            {
                return;
            }

            RegisterChannelSelectionWindows(manager, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);

            int x = Math.Max(40, (screenWidth / 2) - 160);
            int y = Math.Max(40, (screenHeight / 2) - 120);
            const int cascade = 24;

            RegisterPlaceholderWindow(manager, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.WorldMap, "World Map",
                "Scaffold owner for the minimap full-map transition and region overlay flow.",
                new Point(x, y));
            RegisterAdminShopWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.CashShop, AdminShopServiceMode.CashShop,
                new Point(x + cascade, y + cascade));
            RegisterAdminShopWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.Mts, AdminShopServiceMode.Mts,
                new Point(x + (cascade * 2), y + (cascade * 2)));
            RegisterPlaceholderWindow(manager, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.Menu, "Menu",
                "Scaffold owner for the status-bar menu button until a client-accurate utility surface is added.",
                new Point(x + (cascade * 3), y + (cascade * 3)));
            RegisterPlaceholderWindow(manager, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.System, "System",
                "Scaffold owner for the status-bar system button until a client-accurate utility surface is added.",
                new Point(x + (cascade * 4), y + (cascade * 4)));
            RegisterItemMakerWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 5), y + (cascade * 5)));
            RegisterItemUpgradeWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 6)));
            RegisterPlaceholderWindow(manager, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.QuestAlarm, "Quest Alarm",
                "Scaffold owner for the standalone quest progress tracker surface.",
                new Point(x + (cascade * 8), y + (cascade * 8)));
        }

        private static void RegisterChannelSelectionWindows(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.WorldSelect) != null)
            {
                return;
            }

            WzSubProperty channelProperty = uiWindow2Image?["Channel"] as WzSubProperty
                ?? uiWindow1Image?["Channel"] as WzSubProperty;
            if (channelProperty == null)
            {
                return;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            Dictionary<int, Texture2D> worldBadges = new Dictionary<int, Texture2D>();
            WzSubProperty worldBadgeProperty = channelProperty["world"] as WzSubProperty;
            foreach (WzImageProperty property in worldBadgeProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!int.TryParse(property.Name, out int worldId))
                {
                    continue;
                }

                Texture2D badgeTexture = LoadCanvasTexture(worldBadgeProperty, property.Name, device);
                if (badgeTexture != null && !worldBadges.ContainsKey(worldId))
                {
                    worldBadges.Add(worldId, badgeTexture);
                }
            }

            if (worldBadges.Count == 0)
            {
                return;
            }

            WorldSelectWindow worldSelectWindow = CreateWorldSelectWindow(device, worldBadges);
            worldSelectWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 264), Math.Max(24, (screenHeight / 2) - 145));
            manager.RegisterCustomWindow(worldSelectWindow);

            ChannelSelectWindow channelSelectWindow = CreateChannelSelectWindow(channelProperty, clickSound, overSound, device, worldBadges);
            if (channelSelectWindow != null)
            {
                channelSelectWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 84), Math.Max(24, (screenHeight / 2) - 112));
                manager.RegisterCustomWindow(channelSelectWindow);
            }

            ChannelShiftWindow channelShiftWindow = CreateChannelShiftWindow(channelProperty, device, worldBadges);
            if (channelShiftWindow != null)
            {
                channelShiftWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 64), Math.Max(24, (screenHeight / 2) - 102));
                manager.RegisterCustomWindow(channelShiftWindow);
            }
        }

        private static WorldSelectWindow CreateWorldSelectWindow(GraphicsDevice device, Dictionary<int, Texture2D> worldBadges)
        {
            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 336, 214, "World Select");
            Texture2D highlightTexture = CreateSolidTexture(device, Color.White);
            List<(int worldId, UIObject button, Texture2D icon)> worldButtons = new List<(int, UIObject, Texture2D)>();

            int column = 0;
            int row = 0;
            foreach (KeyValuePair<int, Texture2D> badge in worldBadges.OrderBy(pair => pair.Key).Take(12))
            {
                UIObject button = CreateTextureButton(badge.Value, badge.Value);
                if (button == null)
                {
                    continue;
                }

                button.X = 18 + (column * 156);
                button.Y = 44 + (row * 24);
                worldButtons.Add((badge.Key, button, badge.Value));

                row++;
                if (row == 6)
                {
                    row = 0;
                    column++;
                }
            }

            return new WorldSelectWindow(new DXObject(0, 0, frameTexture, 0), highlightTexture, worldButtons);
        }

        private static ChannelSelectWindow CreateChannelSelectWindow(
            WzSubProperty channelProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            Dictionary<int, Texture2D> worldBadges)
        {
            Texture2D frameTexture = LoadCanvasTexture(channelProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            Texture2D overlayTexture2 = LoadCanvasTexture(channelProperty, "backgrnd2", device);
            Texture2D overlayTexture3 = LoadCanvasTexture(channelProperty, "backgrnd3", device);
            Point overlayOffset2 = GetCanvasOffset(channelProperty["backgrnd2"] as WzCanvasProperty);
            Point overlayOffset3 = GetCanvasOffset(channelProperty["backgrnd3"] as WzCanvasProperty);

            UIObject changeButton = LoadButton(channelProperty, "BtChange", clickSound, overSound, device);
            UIObject cancelButton = LoadButton(channelProperty, "BtCancel", clickSound, overSound, device);
            if (changeButton != null)
            {
                changeButton.X = 278;
                changeButton.Y = 20;
            }

            if (cancelButton != null)
            {
                cancelButton.X = 228;
                cancelButton.Y = 20;
            }

            Texture2D channelNormalTexture = LoadCanvasTexture(channelProperty, "channel0", device);
            Texture2D channelSelectedTexture = LoadCanvasTexture(channelProperty, "channel1", device) ?? channelNormalTexture;
            WzSubProperty channelIconProperty = channelProperty["ch"] as WzSubProperty;
            List<(int channelIndex, UIObject button, Texture2D icon)> channelButtons = new List<(int, UIObject, Texture2D)>();
            for (int channelIndex = 0; channelIndex < 20; channelIndex++)
            {
                UIObject button = CreateTextureButton(channelNormalTexture, channelSelectedTexture);
                if (button == null)
                {
                    continue;
                }

                int column = channelIndex % 4;
                int row = channelIndex / 4;
                button.X = 20 + (column * 78);
                button.Y = 58 + (row * 20);
                channelButtons.Add((channelIndex, button, LoadCanvasTexture(channelIconProperty, channelIndex.ToString(), device)));
            }

            return new ChannelSelectWindow(
                new DXObject(0, 0, frameTexture, 0),
                overlayTexture2,
                overlayOffset2,
                overlayTexture3,
                overlayOffset3,
                CreateSolidTexture(device, Color.White),
                changeButton,
                cancelButton,
                channelButtons,
                worldBadges);
        }

        private static ChannelShiftWindow CreateChannelShiftWindow(
            WzSubProperty channelProperty,
            GraphicsDevice device,
            Dictionary<int, Texture2D> worldBadges)
        {
            Texture2D frameTexture = LoadCanvasTexture(channelProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            Dictionary<int, Texture2D> channelIcons = new Dictionary<int, Texture2D>();
            WzSubProperty channelIconProperty = channelProperty["ch"] as WzSubProperty;
            for (int channelIndex = 0; channelIndex < 20; channelIndex++)
            {
                Texture2D channelTexture = LoadCanvasTexture(channelIconProperty, channelIndex.ToString(), device);
                if (channelTexture != null)
                {
                    channelIcons[channelIndex] = channelTexture;
                }
            }

            return new ChannelShiftWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadCanvasTexture(channelProperty, "backgrnd2", device),
                GetCanvasOffset(channelProperty["backgrnd2"] as WzCanvasProperty),
                LoadCanvasTexture(channelProperty, "backgrnd3", device),
                GetCanvasOffset(channelProperty["backgrnd3"] as WzCanvasProperty),
                LoadCanvasTexture(channelProperty, "channel1", device),
                worldBadges,
                channelIcons);
        }

        private static void RegisterAdminShopWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            AdminShopServiceMode defaultMode,
            Point position)
        {
            if (manager.GetWindow(windowName) != null)
            {
                return;
            }

            manager.RegisterCustomWindow(CreateAdminShopDialogWindow(
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                windowName,
                defaultMode,
                position));
        }

        private static void RegisterPlaceholderWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            string title,
            string body,
            Point position)
        {
            if (manager.GetWindow(windowName) != null)
            {
                return;
            }

            PlaceholderUtilityWindow window = CreatePlaceholderUtilityWindow(
                basicImage,
                soundUIImage,
                device,
                windowName,
                title,
                body,
                position);
            manager.RegisterCustomWindow(window);
        }

        private static void RegisterItemMakerWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.ItemMaker) != null)
            {
                return;
            }

            UIWindowBase itemMaker = CreateItemMakerWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position, manager.InventoryWindow as IInventoryRuntime);
            manager.RegisterCustomWindow(itemMaker);
        }

        private static void RegisterItemUpgradeWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.ItemUpgrade) != null)
            {
                return;
            }

            UIWindowBase itemUpgrade = CreateItemUpgradeWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            manager.RegisterCustomWindow(itemUpgrade);
        }

        private static UIWindowBase CreateItemMakerWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            IInventoryRuntime inventory)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Maker"] as WzSubProperty
                ?? uiWindow1Image?["Maker"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemMaker,
                    "Item Maker",
                    "Fallback owner for the dedicated crafting and recipe interaction window.",
                    position);
            }

            WzCanvasProperty background = sourceProperty["backgrnd"] as WzCanvasProperty;
            if (background == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemMaker,
                    "Item Maker",
                    "Fallback owner for the dedicated crafting and recipe interaction window.",
                    position);
            }

            Texture2D frameTexture = background.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
            Texture2D pixel = new Texture2D(device, 1, 1);
            pixel.SetData(new[] { Color.White });

            ItemMakerUI itemMaker = new ItemMakerUI(new DXObject(0, 0, frameTexture, 0), pixel)
            {
                Position = position
            };
            itemMaker.SetInventory(inventory);

            Texture2D overlay = LoadCanvasTexture(sourceProperty, "backgrnd2", device);
            Texture2D header = LoadCanvasTexture(sourceProperty, "backgrnd3", device);
            Texture2D innerOverlay = LoadCanvasTexture(sourceProperty, "backgrnd4", device);
            if (overlay != null)
            {
                itemMaker.AddBackgroundLayer(new DXObject(0, 0, overlay, 0), ResolveCanvasOffset(sourceProperty["backgrnd2"] as WzCanvasProperty));
            }
            if (header != null)
            {
                itemMaker.AddBackgroundLayer(new DXObject(0, 0, header, 0), ResolveCanvasOffset(sourceProperty["backgrnd3"] as WzCanvasProperty));
            }
            if (innerOverlay != null)
            {
                itemMaker.AddBackgroundLayer(new DXObject(0, 0, innerOverlay, 0), ResolveCanvasOffset(sourceProperty["backgrnd4"] as WzCanvasProperty));
            }

            WzSubProperty gaugeBarProperty = sourceProperty["GaugeBar"] as WzSubProperty;
            if (gaugeBarProperty != null)
            {
                Texture2D gaugeBar = LoadCanvasTexture(gaugeBarProperty, "bar", device);
                Texture2D gaugeFill = LoadCanvasTexture(gaugeBarProperty, "gauge", device);
                Point gaugeOffset = ResolveCanvasOffset(gaugeBarProperty["bar"] as WzCanvasProperty);
                itemMaker.SetGaugeTextures(gaugeBar, gaugeFill, gaugeOffset);
            }

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            UIObject startButton = LoadButton(sourceProperty, "BtStart", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device);
            itemMaker.InitializeControls(startButton, cancelButton);

            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    itemMaker.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            return itemMaker;
        }

        private static UIWindowBase CreateItemUpgradeWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty goldHammerProperty = uiWindow2Image?["GoldHammer"] as WzSubProperty;
            WzSubProperty viciousHammerProperty = uiWindow1Image?["ViciousHammer"] as WzSubProperty;
            WzSubProperty enchantSkillProperty = uiWindow2Image?["EnchantSkill"] as WzSubProperty
                ?? uiWindow1Image?["EnchantSkill"] as WzSubProperty;
            WzSubProperty sourceProperty = goldHammerProperty ?? viciousHammerProperty ?? enchantSkillProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemUpgrade,
                    "Item Upgrade",
                    "Fallback owner for the dedicated item enhancement flow.",
                    position);
            }

            WzCanvasProperty background = sourceProperty["backgrnd"] as WzCanvasProperty;
            if (background == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.ItemUpgrade,
                    "Item Upgrade",
                    "Fallback owner for the dedicated item enhancement flow.",
                    position);
            }

            Texture2D frameTexture = background.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
            IDXObject frame = new DXObject(0, 0, frameTexture, 0);
            ItemUpgradeUI itemUpgrade = new ItemUpgradeUI(frame)
            {
                Position = position
            };

            Texture2D overlay = LoadCanvasTexture(sourceProperty, "backgrnd2", device);
            Texture2D header = LoadCanvasTexture(sourceProperty, "backgrnd3", device);
            Point overlayOffset = ResolveCanvasOffset(sourceProperty["backgrnd2"] as WzCanvasProperty);
            Point headerOffset = ResolveCanvasOffset(sourceProperty["backgrnd3"] as WzCanvasProperty);
            itemUpgrade.SetDecorations(overlay, overlayOffset, header, headerOffset);

            WzSubProperty gaugeBarProperty = sourceProperty["GaugeBar"] as WzSubProperty;
            if (gaugeBarProperty != null)
            {
                Texture2D gaugeBar = LoadCanvasTexture(gaugeBarProperty, "bar", device);
                Texture2D gaugeFill = LoadCanvasTexture(gaugeBarProperty, "gauge", device);
                Point gaugeOffset = ResolveCanvasOffset(gaugeBarProperty["bar"] as WzCanvasProperty);
                itemUpgrade.SetGaugeTextures(gaugeBar, gaugeFill, gaugeOffset);
            }

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            UIObject startButton = LoadButton(sourceProperty, "BtStart", btClickSound, btOverSound, device);
            if (startButton == null)
            {
                WzSubProperty basicOk = basicImage?["BtOK"] as WzSubProperty;
                if (basicOk != null)
                {
                    startButton = new UIObject(basicOk, btClickSound, btOverSound, false, Point.Zero, device);
                }
            }

            UIObject cancelButton = LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device);
            if (cancelButton == null)
            {
                WzSubProperty basicCancel = basicImage?["BtCancel"] as WzSubProperty;
                if (basicCancel != null)
                {
                    cancelButton = new UIObject(basicCancel, btClickSound, btOverSound, false, Point.Zero, device);
                }
            }

            UIObject prevButton = null;
            WzSubProperty basicUp = basicImage?["BtUP"] as WzSubProperty;
            if (basicUp != null)
            {
                prevButton = new UIObject(basicUp, btClickSound, btOverSound, false, Point.Zero, device);
            }

            UIObject nextButton = null;
            WzSubProperty basicDown = basicImage?["BtDown"] as WzSubProperty;
            if (basicDown != null)
            {
                nextButton = new UIObject(basicDown, btClickSound, btOverSound, false, Point.Zero, device);
            }
            itemUpgrade.InitializeUpgradeButtons(startButton, cancelButton, prevButton, nextButton);

            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = frameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 7;
                    itemUpgrade.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            return itemUpgrade;
        }

        private static PlaceholderUtilityWindow CreatePlaceholderUtilityWindow(
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            string title,
            string body,
            Point position)
        {
            const int width = 292;
            const int height = 148;

            Texture2D bgTexture = CreatePlaceholderWindowTexture(device, width, height, title);
            IDXObject frame = new DXObject(0, 0, bgTexture, 0);
            PlaceholderUtilityWindow window = new PlaceholderUtilityWindow(frame, windowName, title, body)
            {
                Position = position
            };

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            WzSubProperty closeButtonProperty = (WzSubProperty)basicImage?["BtClose"];
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            return window;
        }

        private static UIWindowBase CreateAdminShopDialogWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            AdminShopServiceMode defaultMode,
            Point position)
        {
            WzSubProperty shopProperty = uiWindow2Image?["Shop"] as WzSubProperty;
            if (shopProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    windowName,
                    defaultMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS",
                    "Fallback utility owner because UIWindow2.img/Shop assets were unavailable.",
                    position);
            }

            WzCanvasProperty backgroundProperty = shopProperty["backgrnd"] as WzCanvasProperty;
            if (backgroundProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    windowName,
                    defaultMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS",
                    "Fallback utility owner because the shop dialog background could not be loaded.",
                    position);
            }

            Texture2D frameTexture = backgroundProperty.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    windowName,
                    defaultMode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS",
                    "Fallback utility owner because the shop dialog texture conversion failed.",
                    position);
            }

            IDXObject frame = new DXObject(0, 0, frameTexture, 0);
            IDXObject frameOverlay = LoadWindowCanvasLayerWithOffset(shopProperty, "backgrnd2", device, out Point frameOverlayOffset);
            IDXObject contentOverlay = LoadWindowCanvasLayerWithOffset(shopProperty, "backgrnd3", device, out Point contentOverlayOffset);
            Texture2D selectTexture = LoadCanvasTexture(shopProperty, "select", device);
            Texture2D mesoTexture = LoadCanvasTexture(shopProperty, "meso", device);

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject buyButton = LoadButton(shopProperty, "BtBuy", btClickSound, btOverSound, device);
            UIObject sellButton = LoadButton(shopProperty, "BtSell", btClickSound, btOverSound, device);
            UIObject exitButton = LoadButton(shopProperty, "BtExit", btClickSound, btOverSound, device);

            AdminShopDialogUI window = new AdminShopDialogUI(
                frame,
                windowName,
                defaultMode,
                frameOverlay,
                frameOverlayOffset,
                contentOverlay,
                contentOverlayOffset,
                selectTexture,
                mesoTexture,
                buyButton,
                sellButton,
                exitButton)
            {
                Position = position,
                Money = 0
            };

            return window;
        }

        private static Point ResolveCanvasOffset(WzCanvasProperty canvas)
        {
            System.Drawing.PointF? origin = canvas?.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return Point.Zero;
            }

            return new Point(-(int)origin.Value.X, -(int)origin.Value.Y);
        }

        private static void SeedStarterCraftingInventory(IInventoryRuntime inventory)
        {
            if (inventory == null || inventory.GetItemCount(InventoryType.ETC, 4010001) > 0)
            {
                return;
            }

            inventory.AddItem(InventoryType.ETC, 4010001, null, 30); // Steel Ore
            inventory.AddItem(InventoryType.ETC, 4010002, null, 20); // Mithril Ore
            inventory.AddItem(InventoryType.ETC, 4020008, null, 10); // Black Crystal Ore
        }
        #endregion
    }
}
