using HaCreator.MapSimulator.Interaction;
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

            UIObject btnGather = LoadButton(itemProperty, "BtGather", btClickSound, btOverSound, device);
            UIObject btnSort = LoadButton(itemProperty, "BtSort", btClickSound, btOverSound, device);
            UIObject btnCashShop = LoadButton(itemProperty, "BtCashshop", btClickSound, btOverSound, device);
            inventory.InitializeUtilityButtons(btnGather, btnSort, btnCashShop);

            WzSubProperty skillMainProperty = uiWindowImage?["Skill"]?["main"] as WzSubProperty;
            if (skillMainProperty != null)
            {
                // Inventory art does not ship dedicated tip frames, so reuse the shared client tooltip surfaces.
                Texture2D[] tooltipFrames = new Texture2D[3];
                tooltipFrames[0] = LoadCanvasTexture(skillMainProperty, "tip0", device);
                tooltipFrames[1] = LoadCanvasTexture(skillMainProperty, "tip1", device);
                tooltipFrames[2] = LoadCanvasTexture(skillMainProperty, "tip2", device);
                inventory.SetTooltipTextures(tooltipFrames);
            }

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
            equip.SetCompanionPanes(
                LoadCanvasObject(equipProperty, "pet", device, out Point _),
                LoadCanvasObject(equipProperty, "DragonEquip", device, out Point _));
            equip.InitializeCompanionButtons(
                LoadButton(equipProperty, "BtPetEquipShow", btClickSound, btOverSound, device),
                LoadButton(equipProperty, "BtPetEquipHide", btClickSound, btOverSound, device),
                LoadButton(equipProperty, "BtDragonEquip", btClickSound, btOverSound, device),
                LoadButton(equipProperty, "BtPet1", btClickSound, btOverSound, device),
                LoadButton(equipProperty, "BtPet2", btClickSound, btOverSound, device),
                LoadButton(equipProperty, "BtPet3", btClickSound, btOverSound, device));

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

            WzSubProperty aranButtonProperty = (WzSubProperty)tabProperty?["AranButton"];
            if (aranButtonProperty != null)
            {
                UIObject[] guideButtons = new UIObject[4];
                for (int i = 0; i < guideButtons.Length; i++)
                {
                    guideButtons[i] = LoadButton(aranButtonProperty, $"Bt{i + 1}", btClickSound, btOverSound, device);
                }

                skill.InitializeAranGuideButtons(guideButtons);
            }

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
                skillWindow.ConfigureAranGuideButtons(GetAranGuideUnlockedGrade(jobId));

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

        private static int GetAranGuideUnlockedGrade(int jobId)
        {
            return jobId switch
            {
                2000 => 1,
                2100 => 1,
                2110 => 2,
                2111 => 3,
                2112 => 4,
                _ => 0
            };
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

            UIObject tabAvailable = LoadQuestCanvasTabButton(listProperty, "0", btClickSound, btOverSound, device);
            UIObject tabInProgress = LoadQuestCanvasTabButton(listProperty, "1", btClickSound, btOverSound, device);
            UIObject tabCompleted = LoadQuestCanvasTabButton(listProperty, "2", btClickSound, btOverSound, device);
            UIObject tabRecommended = LoadQuestCanvasTabButton(listProperty, "3", btClickSound, btOverSound, device);
            quest.InitializeTabs(tabAvailable, tabInProgress, tabCompleted, tabRecommended);

            UIObject myLevelButton = LoadButton(listProperty, "BtMyLevel", btClickSound, btOverSound, device);
            UIObject allLevelButton = LoadButton(listProperty, "BtAllLevel", btClickSound, btOverSound, device);
            quest.InitializeLevelFilterButtons(myLevelButton, allLevelButton);

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
            UIObject btnCashShop = LoadButton(itemProperty, "BtCashshop", btClickSound, btOverSound, device);
            UIObject btnFull = LoadButton(itemProperty, "BtFull", btClickSound, btOverSound, device);
            UIObject btnSmall = LoadButton(itemProperty, "BtSmall", btClickSound, btOverSound, device);
            inventory.InitializeUtilityButtons(btnGather, btnSort, btnCashShop);
            inventory.InitializeBigBangButtons(btnGather, btnSort, btnFull, btnSmall);
            inventory.SetRenderAssets(
                LoadCanvasTexture(itemProperty, "activeIcon", device),
                LoadCanvasTexture(itemProperty, "disabled", device),
                LoadCanvasTexture(itemProperty, "shadow", device),
                LoadInventoryMarkerTextures(itemProperty, "Quality", device));

            WzSubProperty skillMainProperty = uiWindow2Image?["Skill"]?["main"] as WzSubProperty;
            if (skillMainProperty != null)
            {
                Texture2D[] tooltipFrames = new Texture2D[3];
                tooltipFrames[0] = LoadCanvasTexture(skillMainProperty, "tip0", device);
                tooltipFrames[1] = LoadCanvasTexture(skillMainProperty, "tip1", device);
                tooltipFrames[2] = LoadCanvasTexture(skillMainProperty, "tip2", device);
                inventory.SetTooltipTextures(tooltipFrames);
            }

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

            UIObject tabAvailable = LoadQuestCanvasTabButton(questProperty, "0", btClickSound, btOverSound, device);
            UIObject tabInProgress = LoadQuestCanvasTabButton(questProperty, "1", btClickSound, btOverSound, device);
            UIObject tabCompleted = LoadQuestCanvasTabButton(questProperty, "2", btClickSound, btOverSound, device);
            UIObject tabRecommended = LoadQuestCanvasTabButton(questProperty, "3", btClickSound, btOverSound, device);
            quest.InitializeTabs(tabAvailable, tabInProgress, tabCompleted, tabRecommended);

            UIObject myLevelButton = LoadButton(questProperty, "BtMyLevel", btClickSound, btOverSound, device);
            UIObject allLevelButton = LoadButton(questProperty, "BtAllLevel", btClickSound, btOverSound, device);
            quest.InitializeLevelFilterButtons(myLevelButton, allLevelButton);

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

            window.SetSectionTextures(
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "summary", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "basic", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "reward", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "select", device));
            window.SetProgressTextures(
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "spot", device),
                new Point(30, 0));

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            InitializeQuestDetailButtons(window, questInfoProperty, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, true);
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

            window.SetSectionTextures(
                LoadCanvasTexture(questProperty, "summary", device),
                LoadCanvasTexture(questProperty, "basic", device),
                LoadCanvasTexture(questProperty, "reward", device),
                LoadCanvasTexture(questProperty, "select", device));
            window.SetProgressTextures(
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "spot", device),
                new Point(32, 0));

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            InitializeQuestDetailButtons(window, questProperty, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, false);
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
            InitializeQuestDetailButtons(window, null, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, true);
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

        private static void InitializeQuestDetailButtons(
            QuestDetailWindow window,
            WzSubProperty buttonSource,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            int frameWidth,
            int frameHeight,
            bool isBigBang)
        {
            bool hasAcceptArt = isBigBang
                ? buttonSource?["BtQuestDeliveryAccept"] is WzSubProperty
                : buttonSource?["BtOK"] is WzSubProperty;
            bool hasCompleteArt = isBigBang
                ? buttonSource?["BtQuestDeliveryComplete"] is WzSubProperty
                : buttonSource?["BtOK"] is WzSubProperty;
            bool hasTrackArt = (isBigBang ? buttonSource?["BtArlim"] : buttonSource?["BtAlert"]) is WzSubProperty;
            bool hasGiveUpArt = buttonSource?["BtGiveup"] is WzSubProperty;

            UIObject acceptButton = isBigBang
                ? CreateQuestDetailActionButton(buttonSource?["BtQuestDeliveryAccept"] as WzSubProperty, clickSound, overSound, device)
                : CreateQuestDetailActionButton(buttonSource?["BtOK"] as WzSubProperty, clickSound, overSound, device);
            UIObject completeButton = isBigBang
                ? CreateQuestDetailActionButton(buttonSource?["BtQuestDeliveryComplete"] as WzSubProperty, clickSound, overSound, device)
                : CreateQuestDetailActionButton(buttonSource?["BtOK"] as WzSubProperty, clickSound, overSound, device);
            UIObject trackButton = CreateQuestDetailActionButton(
                (isBigBang ? buttonSource?["BtArlim"] : buttonSource?["BtAlert"]) as WzSubProperty,
                clickSound, overSound, device);
            UIObject giveUpButton = CreateQuestDetailActionButton(buttonSource?["BtGiveup"] as WzSubProperty, clickSound, overSound, device);

            acceptButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
            completeButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
            trackButton ??= CreateFallbackQuestDetailButton(device, 86, 17);
            giveUpButton ??= CreateFallbackQuestDetailButton(device, 60, 16);

            PositionQuestDetailActionButton(acceptButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(completeButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(trackButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(giveUpButton, frameWidth, frameHeight, 16, acceptButton?.CanvasSnapshotWidth ?? completeButton?.CanvasSnapshotWidth ?? trackButton?.CanvasSnapshotWidth ?? 78);

            window.RegisterActionButton(QuestWindowActionKind.Accept, acceptButton, !hasAcceptArt);
            window.RegisterActionButton(QuestWindowActionKind.Complete, completeButton, !hasCompleteArt);
            window.RegisterActionButton(QuestWindowActionKind.Track, trackButton, !hasTrackArt);
            window.RegisterActionButton(QuestWindowActionKind.GiveUp, giveUpButton, !hasGiveUpArt);
            window.InitializeNavigationButtons(device);
        }

        private static UIObject CreateQuestDetailActionButton(
            WzSubProperty buttonProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device)
        {
            if (buttonProperty == null)
            {
                return null;
            }

            try
            {
                return new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
            }
            catch
            {
                return null;
            }
        }

        private static UIObject CreateFallbackQuestDetailButton(GraphicsDevice device, int width, int height)
        {
            return UiButtonFactory.CreateSolidButton(
                device, width, height,
                new Color(69, 95, 122, 230),
                new Color(101, 131, 160, 240),
                new Color(82, 110, 140, 240),
                new Color(42, 42, 42, 170));
        }

        private static void PositionQuestDetailActionButton(UIObject button, int frameWidth, int frameHeight, int rightMargin, int slotOffset)
        {
            if (button == null)
            {
                return;
            }

            int buttonWidth = Math.Max(1, button.CanvasSnapshotWidth);
            int buttonHeight = Math.Max(1, button.CanvasSnapshotHeight);
            button.X = Math.Max(12, frameWidth - buttonWidth - rightMargin - slotOffset);
            button.Y = Math.Max(16, frameHeight - buttonHeight - 10);
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
            WzSubProperty teleport3Property = uiWindow2Image?["Teleport3"] as WzSubProperty;
            WzSubProperty teleportProperty =
                teleport3Property ??
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
                teleportProperty == teleport3Property ? 10 : 5,
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

        private static TrunkUI CreateTrunkWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            InventoryUI inventory)
        {
            WzSubProperty trunkProperty = uiWindow2Image?["Trunk"] as WzSubProperty
                ?? uiWindow1Image?["Trunk"] as WzSubProperty;
            if (trunkProperty == null)
            {
                return null;
            }

            WzCanvasProperty backgroundProperty = trunkProperty["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            TrunkUI window = new TrunkUI(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(trunkProperty, "backgrnd2", device, out Point foregroundOffset),
                foregroundOffset,
                LoadWindowCanvasLayerWithOffset(trunkProperty, "backgrnd3", device, out Point contentOffset),
                contentOffset,
                LoadCanvasTexture(trunkProperty, "select", device),
                LoadButton(trunkProperty, "BtGet", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtPut", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtSort", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtExit", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtOutCoin", btClickSound, btOverSound, device),
                LoadButton(trunkProperty, "BtInCoin", btClickSound, btOverSound, device),
                device)
            {
                Position = new Point(
                    Math.Max(24, (screenWidth - frameTexture.Width) / 2),
                    Math.Max(36, (screenHeight - frameTexture.Height) / 2))
            };

            window.InitializeTabs(
                LoadInventoryCanvasTabButton(trunkProperty, "0", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "1", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "2", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "3", btClickSound, btOverSound, device),
                LoadInventoryCanvasTabButton(trunkProperty, "4", btClickSound, btOverSound, device));
            window.SetInventory(inventory);
            return window;
        }

        private static WorldMapUI CreateWorldMapWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty worldMapProperty = uiWindow2Image?["WorldMap"] as WzSubProperty;
            if (worldMapProperty == null)
            {
                return null;
            }

            Texture2D frameTexture = LoadCanvasTexture(worldMapProperty["Border"] as WzSubProperty, "0", device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            WzSubProperty worldMapSearchProperty = worldMapProperty["WorldMapSearch"] as WzSubProperty;
            Texture2D sidePanelTexture = LoadCanvasTexture(worldMapSearchProperty, "backgrnd", device);
            Point sidePanelOffset = ResolveCanvasOffset(worldMapSearchProperty, "backgrnd", new Point(507, 0));
            Texture2D searchNoticeTexture = LoadCanvasTexture(worldMapSearchProperty, "notice", device);
            Point searchNoticeOffset = ResolveCanvasOffset(worldMapSearchProperty, "notice", new Point(535, 220));

            Texture2D selectionTexture = new Texture2D(device, 1, 1);
            selectionTexture.SetData(new[] { Color.White });

            List<(string regionCode, UIObject button)> regionButtons = new List<(string, UIObject)>();
            WzSubProperty anotherWorldProperty = worldMapProperty["BtAnother"]?["AnotherWorld"] as WzSubProperty;
            foreach (WzImageProperty property in anotherWorldProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!property.Name.StartsWith("Map", StringComparison.Ordinal))
                {
                    continue;
                }

                UIObject button = LoadButton(anotherWorldProperty, property.Name, clickSound, overSound, device);
                if (button == null)
                {
                    continue;
                }

                regionButtons.Add((property.Name.Substring(3), button));
            }

            WorldMapUI window = new WorldMapUI(
                new DXObject(0, 0, frameTexture, 0),
                sidePanelTexture,
                sidePanelOffset,
                searchNoticeTexture,
                searchNoticeOffset,
                selectionTexture,
                LoadButton(worldMapProperty, "BtAll", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtAnother", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtSearch", clickSound, overSound, device),
                LoadButton(worldMapSearchProperty, "BtAllsearch", clickSound, overSound, device),
                LoadButton(worldMapSearchProperty, "BtLevelMob", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtBefore", clickSound, overSound, device),
                LoadButton(worldMapProperty, "BtNext", clickSound, overSound, device),
                LoadCanvasTexture(worldMapSearchProperty?["resultField"] as WzSubProperty, "mouseOverBase", device),
                LoadCanvasTexture(worldMapSearchProperty?["resultField"] as WzSubProperty, "icon", device),
                LoadCanvasTexture(worldMapSearchProperty?["resultNpc"] as WzSubProperty, "mouseOverBase", device),
                LoadCanvasTexture(worldMapSearchProperty?["resultNpc"] as WzSubProperty, "icon", device),
                LoadCanvasTexture(worldMapSearchProperty?["resultMob"] as WzSubProperty, "mouseOverBase", device),
                LoadCanvasTexture(worldMapSearchProperty?["resultMob"] as WzSubProperty, "icon", device),
                regionButtons,
                device)
            {
                Position = new Point(
                    Math.Max(12, (screenWidth - frameTexture.Width) / 2),
                    Math.Max(12, (screenHeight - frameTexture.Height) / 2))
            };

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
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
            window.InitializePrimaryButtons(
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
            window.InitializePrimaryButtons(
                LoadButton(characterProperty, "BtParty", clickSound, overSound, device),
                LoadButton(characterProperty, "BtTrad", clickSound, overSound, device),
                LoadButton(characterProperty, "BtItem", clickSound, overSound, device),
                LoadButton(characterProperty, "BtWish", clickSound, overSound, device),
                LoadButton(characterProperty, "BtFamily", clickSound, overSound, device));
            window.InitializePageButtons(
                LoadButton(characterProperty, "BtRide", clickSound, overSound, device),
                LoadButton(characterProperty, "BtPet", clickSound, overSound, device),
                LoadButton(characterProperty, "BtCollect", clickSound, overSound, device),
                LoadButton(characterProperty, "BtPersonality", clickSound, overSound, device));
            window.InitializePageActionButtons(
                LoadButton(userInfoProperty?["pet"] as WzSubProperty, "BtException", clickSound, overSound, device),
                LoadButton(userInfoProperty?["collect"] as WzSubProperty, "BtArrayName", clickSound, overSound, device),
                LoadButton(userInfoProperty?["collect"] as WzSubProperty, "BtArrayGet", clickSound, overSound, device));

            RegisterUserInfoSubPage(window, "ride", userInfoProperty?["ride"] as WzSubProperty, device);
            RegisterUserInfoSubPage(window, "pet", userInfoProperty?["pet"] as WzSubProperty, device);
            RegisterUserInfoSubPage(window, "collect", userInfoProperty?["collect"] as WzSubProperty, device);
            RegisterUserInfoSubPage(window, "personality", userInfoProperty?["personality"] as WzSubProperty, device);
            RegisterUserInfoExceptionPopup(window, userInfoProperty?["exception"] as WzSubProperty, clickSound, overSound, device);
            return window;
        }

        private static void RegisterUserInfoSubPage(UserInfoUI window, string pageName, WzSubProperty pageProperty, GraphicsDevice device)
        {
            if (window == null || pageProperty == null)
            {
                return;
            }

            Texture2D frameTexture = LoadCanvasTexture(pageProperty, "backgrnd", device);
            if (frameTexture != null)
            {
                window.RegisterPageFrame(pageName, new DXObject(0, 0, frameTexture, 0));
            }

            foreach (WzCanvasProperty canvas in pageProperty.WzProperties.OfType<WzCanvasProperty>())
            {
                if (string.Equals(canvas.Name, "backgrnd", StringComparison.Ordinal))
                {
                    continue;
                }

                Texture2D layerTexture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (layerTexture == null)
                {
                    continue;
                }

                Point offset = ResolveCanvasOffset(canvas, Point.Zero);
                if (string.Equals(pageName, "collect", StringComparison.Ordinal) && string.Equals(canvas.Name, "icon1", StringComparison.Ordinal))
                {
                    window.SetPageIcon(pageName, new DXObject(0, 0, layerTexture, 0), offset.X, offset.Y);
                    continue;
                }

                window.AddPageLayer(pageName, new DXObject(0, 0, layerTexture, 0), offset.X, offset.Y);
            }
        }

        private static void RegisterUserInfoExceptionPopup(
            UserInfoUI window,
            WzSubProperty exceptionProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device)
        {
            if (window == null || exceptionProperty == null)
            {
                return;
            }

            Texture2D frameTexture = LoadCanvasTexture(exceptionProperty, "backgrnd", device);
            IDXObject frame = frameTexture != null ? new DXObject(0, 0, frameTexture, 0) : null;
            List<(IDXObject layer, Point offset)> layers = new List<(IDXObject, Point)>();

            foreach (WzCanvasProperty canvas in exceptionProperty.WzProperties.OfType<WzCanvasProperty>())
            {
                if (string.Equals(canvas.Name, "backgrnd", StringComparison.Ordinal))
                {
                    continue;
                }

                Texture2D layerTexture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (layerTexture == null)
                {
                    continue;
                }

                layers.Add((new DXObject(0, 0, layerTexture, 0), ResolveCanvasOffset(canvas, Point.Zero)));
            }

            window.InitializeExceptionPopup(
                frame,
                layers,
                LoadButton(exceptionProperty, "BtRegist", clickSound, overSound, device),
                LoadButton(exceptionProperty, "BtDelete", clickSound, overSound, device),
                LoadButton(exceptionProperty, "BtMeso", clickSound, overSound, device));
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

        private static Texture2D CreateFilledTexture(GraphicsDevice device, int width, int height, Color fillColor, Color borderColor)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    data[(y * width) + x] = isBorder ? borderColor : fillColor;
                }
            }

            texture.SetData(data);
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

        private static UIObject LoadQuestCanvasTabButton(WzSubProperty questProperty, string tabIndex,
            WzBinaryProperty clickSound, WzBinaryProperty overSound, GraphicsDevice device)
        {
            WzCanvasProperty enabledCanvas = questProperty?["Tab"]?["enabled"]?[tabIndex] as WzCanvasProperty;
            WzCanvasProperty disabledCanvas = questProperty?["Tab"]?["disabled"]?[tabIndex] as WzCanvasProperty;
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
                BaseDXDrawableItem pressedState = new BaseDXDrawableItem(new DXObject(enabledOffset.X, enabledOffset.Y, enabledTexture), false);
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

        public static void RegisterLoginCharacterSelectWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.CharacterSelect) != null)
            {
                return;
            }

            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 618, 320, "Character Select");
            Texture2D cardNormalTexture = CreateFilledTexture(device, 183, 151, new Color(34, 42, 59, 228), new Color(87, 101, 135, 255));
            Texture2D cardPressedTexture = CreateFilledTexture(device, 183, 151, new Color(62, 88, 147, 238), new Color(153, 186, 255, 255));
            Texture2D buttonNormalTexture = CreateFilledTexture(device, 88, 24, new Color(52, 58, 74, 235), new Color(113, 128, 162, 255));
            Texture2D buttonPressedTexture = CreateFilledTexture(device, 88, 24, new Color(84, 108, 170, 245), new Color(168, 194, 255, 255));
            Texture2D pageButtonNormalTexture = CreateFilledTexture(device, 40, 24, new Color(50, 57, 73, 230), new Color(116, 131, 162, 255));
            Texture2D pageButtonPressedTexture = CreateFilledTexture(device, 40, 24, new Color(80, 104, 162, 245), new Color(169, 194, 255, 255));

            List<UIObject> cardButtons = new List<UIObject>();
            for (int slot = 0; slot < 3; slot++)
            {
                UIObject cardButton = CreateTextureButton(cardNormalTexture, cardPressedTexture);
                if (cardButton == null)
                {
                    continue;
                }

                cardButton.X = 18 + (slot * 197);
                cardButton.Y = 46;
                cardButtons.Add(cardButton);
            }

            UIObject prevPageButton = CreateTextureButton(pageButtonNormalTexture, pageButtonPressedTexture);
            UIObject nextPageButton = CreateTextureButton(pageButtonNormalTexture, pageButtonPressedTexture);
            UIObject enterButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);
            UIObject newButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);
            UIObject deleteButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);

            if (prevPageButton != null)
            {
                prevPageButton.X = 220;
                prevPageButton.Y = 248;
            }

            if (nextPageButton != null)
            {
                nextPageButton.X = 358;
                nextPageButton.Y = 248;
            }

            if (enterButton != null)
            {
                enterButton.X = 154;
                enterButton.Y = 248;
            }

            if (newButton != null)
            {
                newButton.X = 264;
                newButton.Y = 248;
            }

            if (deleteButton != null)
            {
                deleteButton.X = 374;
                deleteButton.Y = 248;
            }

            CharacterSelectWindow window = new CharacterSelectWindow(
                new DXObject(0, 0, frameTexture, 0),
                cardButtons,
                prevPageButton,
                nextPageButton,
                enterButton,
                newButton,
                deleteButton)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 309), Math.Max(24, (screenHeight / 2) - 160))
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 590;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            manager.RegisterCustomWindow(window);
        }

        public static void RegisterConnectionNoticeWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.ConnectionNotice) != null)
            {
                return;
            }

            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 300, 120, "Connection Notice");
            Texture2D progressTrackTexture = CreateSolidTexture(device, Color.White);
            Texture2D progressFillTexture = CreateSolidTexture(device, Color.White);

            ConnectionNoticeWindow window = new ConnectionNoticeWindow(
                new DXObject(0, 0, frameTexture, 0),
                progressTrackTexture,
                progressFillTexture)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 150), Math.Max(24, (screenHeight / 2) - 60))
            };

            manager.RegisterCustomWindow(window);
        }

        public static void RegisterLoginUtilityDialogWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) != null)
            {
                return;
            }

            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 282, 154, "Login Utility");
            Texture2D buttonNormalTexture = CreateFilledTexture(device, 88, 24, new Color(52, 58, 74, 235), new Color(113, 128, 162, 255));
            Texture2D buttonPressedTexture = CreateFilledTexture(device, 88, 24, new Color(84, 108, 170, 245), new Color(168, 194, 255, 255));
            UIObject primaryButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);
            UIObject secondaryButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);

            if (primaryButton != null)
            {
                primaryButton.X = 64;
                primaryButton.Y = 114;
            }

            if (secondaryButton != null)
            {
                secondaryButton.X = 160;
                secondaryButton.Y = 114;
            }

            LoginUtilityDialogWindow window = new LoginUtilityDialogWindow(
                new DXObject(0, 0, frameTexture, 0),
                primaryButton,
                secondaryButton)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 141), Math.Max(24, (screenHeight / 2) - 77))
            };

            manager.RegisterCustomWindow(window);
        }

        public static void RegisterLoginEntryWindows(
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
            RegisterLoginRecommendWorldWindow(manager, device, screenWidth, screenHeight);
            RegisterLoginCharacterSelectWindow(manager, basicImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterConnectionNoticeWindow(manager, basicImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterLoginUtilityDialogWindow(manager, basicImage, soundUIImage, device, screenWidth, screenHeight);
        }

        public static void RegisterLoginCharacterDetailWindow(
            UIWindowManager manager,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.CharacterDetail) != null)
            {
                return;
            }

            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 208, 236, "Character Detail");
            CharacterDetailWindow window = new CharacterDetailWindow(new DXObject(0, 0, frameTexture, 0))
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) + 194), Math.Max(24, (screenHeight / 2) - 118))
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = 180;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            manager.RegisterCustomWindow(window);
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
            AranSkillGuideUI aranSkillGuide = null;
            if (isBigBang)
            {
                skillMacro = CreateSkillMacroWindowBigBang(uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);
                aranSkillGuide = CreateAranSkillGuideWindowBigBang(uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);
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

            if (aranSkillGuide != null)
            {
                manager.RegisterCustomWindow(aranSkillGuide);

                if (skill is SkillUIBigBang skillBB)
                {
                    skillBB.OnSkillGuideRequested = grade =>
                    {
                        aranSkillGuide.SetPage(grade);
                        aranSkillGuide.Show();
                        manager.BringToFront(aranSkillGuide);
                    };
                }
            }

            SeedStarterCraftingInventory(manager.InventoryWindow as IInventoryRuntime);

            MapTransferUI mapTransfer = CreateMapTransferWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            if (mapTransfer != null)
            {
                manager.RegisterCustomWindow(mapTransfer);
            }

            TrunkUI trunk = CreateTrunkWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, manager.InventoryWindow as InventoryUI);
            if (trunk != null)
            {
                manager.RegisterCustomWindow(trunk);
                SeedStarterTrunkInventory(trunk);
            }

            WorldMapUI worldMap = CreateWorldMapWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            if (worldMap != null)
            {
                manager.RegisterCustomWindow(worldMap);
            }

            RegisterProgressionUtilityPlaceholderWindows(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterSocialRoomWindows(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);

            if (characterInfo != null)
            {
                characterInfo.PartyRequested = () => manager.ShowWindow(MapSimulatorWindowNames.SocialList);
                characterInfo.MiniRoomRequested = () => manager.ShowWindow(MapSimulatorWindowNames.MiniRoom);
                characterInfo.TradingRoomRequested = () => manager.ShowWindow(MapSimulatorWindowNames.TradingRoom);
            }

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

            RegisterAdminShopWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.CashShop, AdminShopServiceMode.CashShop,
                new Point(x + cascade, y + cascade));
            RegisterAdminShopWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.Mts, AdminShopServiceMode.Mts,
                new Point(x + (cascade * 2), y + (cascade * 2)));
            RegisterSocialListWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 2), y + (cascade * 5)));
            RegisterMessengerWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 3), y + (cascade * 3)));
            RegisterMapleTvWindow(manager, uiWindow1Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 4), y + (cascade * 2)));
            RegisterItemMakerWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 5), y + (cascade * 5)));
            RegisterItemUpgradeWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 6)));
            RegisterMemoMailboxWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 7), y + (cascade * 4)));
            RegisterQuestAlarmWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 8), y + (cascade * 8)));
        }

        private static void RegisterSocialRoomWindows(
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

            int x = Math.Max(24, (screenWidth / 2) - 260);
            int y = Math.Max(24, (screenHeight / 2) - 196);
            const int cascade = 24;

            RegisterSocialRoomWindow(manager, CreateMiniRoomWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x, y)));
            RegisterSocialRoomWindow(manager, CreatePersonalShopWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + cascade, y + cascade)));
            RegisterSocialRoomWindow(manager, CreateEntrustedShopWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 2), y + (cascade * 2))));
            RegisterSocialRoomWindow(manager, CreateTradingRoomWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(x + (cascade * 3), y + (cascade * 3))));
        }

        private static void RegisterSocialRoomWindow(UIWindowManager manager, UIWindowBase window)
        {
            if (manager == null || window == null || manager.GetWindow(window.WindowName) != null)
            {
                return;
            }

            manager.RegisterCustomWindow(window);
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
            worldSelectWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 282), Math.Max(24, (screenHeight / 2) - 88));
            manager.RegisterCustomWindow(worldSelectWindow);

            ChannelSelectWindow channelSelectWindow = CreateChannelSelectWindow(channelProperty, clickSound, overSound, device, worldBadges);
            if (channelSelectWindow != null)
            {
                channelSelectWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 185), Math.Max(24, (screenHeight / 2) - 84));
                manager.RegisterCustomWindow(channelSelectWindow);
            }

            ChannelShiftWindow channelShiftWindow = CreateChannelShiftWindow(channelProperty, device, worldBadges);
            if (channelShiftWindow != null)
            {
                channelShiftWindow.Position = new Point(Math.Max(24, (screenWidth / 2) - 185), Math.Max(24, (screenHeight / 2) - 84));
                manager.RegisterCustomWindow(channelShiftWindow);
            }
        }

        private static void RegisterLoginRecommendWorldWindow(
            UIWindowManager manager,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.RecommendWorld) != null)
            {
                return;
            }

            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 200, 220, "Recommend World");
            Texture2D highlightTexture = CreateSolidTexture(device, Color.White);
            Texture2D buttonNormalTexture = CreateFilledTexture(device, 48, 22, new Color(52, 58, 74, 235), new Color(113, 128, 162, 255));
            Texture2D buttonPressedTexture = CreateFilledTexture(device, 48, 22, new Color(84, 108, 170, 245), new Color(168, 194, 255, 255));

            UIObject prevButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);
            UIObject nextButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);
            UIObject selectButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);
            UIObject closeButton = CreateTextureButton(buttonNormalTexture, buttonPressedTexture);

            if (prevButton != null)
            {
                prevButton.X = 34;
                prevButton.Y = 90;
            }

            if (nextButton != null)
            {
                nextButton.X = 118;
                nextButton.Y = 90;
            }

            if (selectButton != null)
            {
                selectButton.X = 47;
                selectButton.Y = 185;
            }

            if (closeButton != null)
            {
                closeButton.X = 104;
                closeButton.Y = 185;
            }

            RecommendWorldWindow window = new RecommendWorldWindow(
                new DXObject(0, 0, frameTexture, 0),
                highlightTexture,
                prevButton,
                nextButton,
                selectButton,
                closeButton)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 100), Math.Max(24, (screenHeight / 2) - 160))
            };

            manager.RegisterCustomWindow(window);
        }

        private static WorldSelectWindow CreateWorldSelectWindow(GraphicsDevice device, Dictionary<int, Texture2D> worldBadges)
        {
            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 564, 177, "World Select");
            Texture2D highlightTexture = CreateSolidTexture(device, Color.White);
            List<(int worldId, UIObject button, Texture2D icon)> worldButtons = new List<(int, UIObject, Texture2D)>();

            int column = 0;
            int row = 0;
            foreach (KeyValuePair<int, Texture2D> badge in worldBadges.OrderBy(pair => pair.Key))
            {
                UIObject button = CreateTextureButton(badge.Value, badge.Value);
                if (button == null)
                {
                    continue;
                }

                button.X = 24 + (column * 132);
                button.Y = 32 + (row * 22);
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

                int column = channelIndex % 5;
                int row = channelIndex / 5;
                button.X = 23 + (column * 66);
                button.Y = 93 + (row * 29);
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

            SeedStarterEnhancementInventory(manager.InventoryWindow as IInventoryRuntime);
            UIWindowBase itemUpgrade = CreateItemUpgradeWindow(
                uiWindow1Image,
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                position,
                manager.InventoryWindow as IInventoryRuntime);
            manager.RegisterCustomWindow(itemUpgrade);
        }

        private static void RegisterQuestAlarmWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.QuestAlarm) != null)
            {
                return;
            }

            UIWindowBase questAlarm = CreateQuestAlarmWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            manager.RegisterCustomWindow(questAlarm);
        }

        private static void RegisterMemoMailboxWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.MemoMailbox) != null)
            {
                return;
            }

            UIWindowBase memoMailboxWindow = CreateMemoMailboxWindow(uiWindow1Image, basicImage, soundUIImage, device, position);
            manager.RegisterCustomWindow(memoMailboxWindow);

            UIWindowBase memoSendWindow = CreateMemoSendWindow(uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 24, position.Y + 18));
            if (memoSendWindow != null)
            {
                manager.RegisterCustomWindow(memoSendWindow);
            }

            UIWindowBase memoGetWindow = CreateMemoGetWindow(uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 18, position.Y + 10));
            if (memoGetWindow != null)
            {
                manager.RegisterCustomWindow(memoGetWindow);
            }
        }

        private static void RegisterSocialListWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.SocialList) != null)
            {
                return;
            }

            UIWindowBase socialListWindow = CreateSocialListWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (socialListWindow != null)
            {
                manager.RegisterCustomWindow(socialListWindow);
            }
        }

        private static void RegisterMessengerWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.Messenger) != null)
            {
                return;
            }

            UIWindowBase messengerWindow = CreateMessengerWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            manager.RegisterCustomWindow(messengerWindow);
        }

        public static void RegisterGuildBbsWindow(
            UIWindowManager manager,
            WzImage guildBbsImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.GuildBbs) != null)
            {
                return;
            }

            manager.RegisterCustomWindow(CreateGuildBbsWindow(guildBbsImage, basicImage, soundUIImage, device, position));
        }

        private static void RegisterMapleTvWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.MapleTv) != null)
            {
                return;
            }

            UIWindowBase mapleTvWindow = CreateMapleTvWindow(uiWindow1Image, basicImage, soundUIImage, device, position);
            if (mapleTvWindow != null)
            {
                manager.RegisterCustomWindow(mapleTvWindow);
            }
        }

        private static UIWindowBase CreateMiniRoomWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty minigameRoot = uiWindow2Image?["Minigame"] as WzSubProperty
                ?? uiWindow1Image?["Minigame"] as WzSubProperty;
            WzSubProperty omokProperty = minigameRoot?["Omok"] as WzSubProperty;
            if (omokProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MiniRoom,
                    "Mini Room",
                    "Fallback owner for Omok and Match Cards social-room parity.",
                    position);
            }

            SocialRoomRuntime runtime = SocialRoomRuntime.CreateMiniRoomSample();
            SocialRoomWindow window = CreateSocialRoomWindow(omokProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.MiniRoom, runtime);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MiniRoom,
                    "Mini Room",
                    "Fallback owner for Omok and Match Cards social-room parity.",
                    position);
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty commonProperty = minigameRoot?["Common"] as WzSubProperty;
            window.BindButton(LoadButton(commonProperty, "btReady", clickSound, overSound, device), runtime.ToggleMiniRoomGuestReady);
            window.BindButton(LoadButton(commonProperty, "btStart", clickSound, overSound, device), runtime.StartMiniRoomSession);
            window.BindButton(LoadButton(commonProperty, "btDraw", clickSound, overSound, device), runtime.CycleMiniRoomMode);
            window.BindButton(LoadButton(commonProperty, "btExit", clickSound, overSound, device), window.Hide);
            return window;
        }

        private static UIWindowBase CreatePersonalShopWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty personalShopRoot = uiWindow2Image?["PersonalShop"] as WzSubProperty
                ?? uiWindow1Image?["PersonalShop"] as WzSubProperty;
            WzSubProperty shopProperty = personalShopRoot?["main"] as WzSubProperty ?? personalShopRoot;
            if (shopProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.PersonalShop,
                    "Personal Shop",
                    "Fallback owner for personal-shop social-room parity.",
                    position);
            }

            SocialRoomRuntime runtime = SocialRoomRuntime.CreatePersonalShopSample();
            SocialRoomWindow window = CreateSocialRoomWindow(shopProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.PersonalShop, runtime);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.PersonalShop,
                    "Personal Shop",
                    "Fallback owner for personal-shop social-room parity.",
                    position);
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.BindButton(LoadButton(shopProperty, "BtStart", clickSound, overSound, device), runtime.TogglePersonalShopOpen);
            window.BindButton(LoadButton(shopProperty, "BtArrange", clickSound, overSound, device), runtime.ArrangePersonalShopInventory);
            window.BindButton(LoadButton(shopProperty, "BtClame", clickSound, overSound, device), runtime.ClaimPersonalShopEarnings);
            window.BindButton(LoadButton(shopProperty, "BtVisit", clickSound, overSound, device), runtime.TogglePersonalShopOpen);
            window.BindButton(LoadButton(shopProperty, "BtExit", clickSound, overSound, device), window.Hide);
            return window;
        }

        private static UIWindowBase CreateEntrustedShopWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty entrustedShopProperty = uiWindow2Image?["EntrustedShop"] as WzSubProperty
                ?? uiWindow1Image?["EntrustedShop"] as WzSubProperty;
            WzSubProperty memberShopProperty = uiWindow2Image?["MemberShop"] as WzSubProperty
                ?? uiWindow1Image?["MemberShop"] as WzSubProperty;
            if (memberShopProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.EntrustedShop,
                    "Entrusted Shop",
                    "Fallback owner for entrusted-shop social-room parity.",
                    position);
            }

            SocialRoomRuntime runtime = SocialRoomRuntime.CreateEntrustedShopSample();
            SocialRoomWindow window = CreateSocialRoomWindow(memberShopProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.EntrustedShop, runtime);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.EntrustedShop,
                    "Entrusted Shop",
                    "Fallback owner for entrusted-shop social-room parity.",
                    position);
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.BindButton(LoadButton(entrustedShopProperty, "BtArrange", clickSound, overSound, device), runtime.ArrangeEntrustedShop);
            window.BindButton(LoadButton(entrustedShopProperty, "BtCoin", clickSound, overSound, device), runtime.ClaimEntrustedShopEarnings);
            window.BindButton(LoadButton(memberShopProperty, "BtOK", clickSound, overSound, device), runtime.ToggleEntrustedLedgerMode);
            window.BindButton(LoadButton(memberShopProperty, "BtCancel", clickSound, overSound, device), window.Hide);
            return window;
        }

        private static UIWindowBase CreateTradingRoomWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty tradeProperty = uiWindow2Image?["TradingRoom"] as WzSubProperty
                ?? uiWindow1Image?["TradingRoom"] as WzSubProperty;
            if (tradeProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.TradingRoom,
                    "Trading Room",
                    "Fallback owner for trading-room social parity.",
                    position);
            }

            SocialRoomRuntime runtime = SocialRoomRuntime.CreateTradingRoomSample();
            SocialRoomWindow window = CreateSocialRoomWindow(tradeProperty, basicImage, soundUIImage, device, position, MapSimulatorWindowNames.TradingRoom, runtime);
            if (window == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.TradingRoom,
                    "Trading Room",
                    "Fallback owner for trading-room social parity.",
                    position);
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.BindButton(LoadButton(tradeProperty, "BtTrade", clickSound, overSound, device), runtime.ConfirmTradeLock);
            window.BindButton(LoadButton(tradeProperty, "BtReset", clickSound, overSound, device), runtime.ResetTrade);
            window.BindButton(LoadButton(tradeProperty, "BtCoin", clickSound, overSound, device), runtime.IncreaseTradeOffer);
            window.BindButton(LoadButton(tradeProperty, "BtCancel", clickSound, overSound, device), window.Hide);
            window.BindButton(LoadButton(tradeProperty, "BtEnter", clickSound, overSound, device), window.Hide);
            window.BindButton(LoadButton(tradeProperty, "BtClame", clickSound, overSound, device), runtime.ConfirmTradeLock);
            return window;
        }

        private static SocialRoomWindow CreateSocialRoomWindow(
            WzSubProperty sourceProperty,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            string windowName,
            SocialRoomRuntime runtime)
        {
            WzCanvasProperty backgroundProperty = sourceProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return null;
            }

            SocialRoomWindow window = new SocialRoomWindow(
                new DXObject(0, 0, frameTexture, 0),
                windowName,
                CreateSolidTexture(device, Color.White),
                runtime)
            {
                Position = position
            };

            AttachCanvasLayer(window, sourceProperty, "backgrnd2", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd3", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd4", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd5", device);
            AttachCanvasLayer(window, sourceProperty, "backgrnd6", device);

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            return window;
        }

        private static void AttachCanvasLayer(SocialRoomWindow window, WzSubProperty sourceProperty, string canvasName, GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }

            IDXObject layer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (layer != null)
            {
                window.AddLayer(layer, offset);
            }
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
            UIObject pageCycleButton = LoadButton(sourceProperty, "BtDown1", btClickSound, btOverSound, device);
            itemMaker.InitializeControls(startButton, cancelButton, pageCycleButton);

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

        private static UIWindowBase CreateQuestAlarmWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["QuestAlarm"] as WzSubProperty
                ?? uiWindow1Image?["QuestAlarm"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.QuestAlarm,
                    "Quest Alarm",
                    "Fallback owner for the standalone quest progress tracker surface.",
                    position);
            }

            Texture2D maxTexture = LoadCanvasTexture(sourceProperty, "backgrndmax", device);
            Texture2D centerTexture = LoadCanvasTexture(sourceProperty, "backgrndcenter", device);
            Texture2D bottomTexture = LoadCanvasTexture(sourceProperty, "backgrndbottom", device);
            Texture2D minTexture = LoadCanvasTexture(sourceProperty, "backgrndmin", device);
            if (maxTexture == null || centerTexture == null || bottomTexture == null || minTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.QuestAlarm,
                    "Quest Alarm",
                    "Fallback owner for the standalone quest progress tracker surface.",
                    position);
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            QuestAlarmWindow window = new QuestAlarmWindow(
                MapSimulatorWindowNames.QuestAlarm,
                device,
                maxTexture,
                centerTexture,
                bottomTexture,
                minTexture)
            {
                Position = position
            };

            window.InitializeControls(
                LoadButton(sourceProperty, "BtAuto", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtQ", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtMax", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtMin", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtDelete", btClickSound, btOverSound, device));

            return window;
        }

        private static UIWindowBase CreateMemoMailboxWindow(
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty memoProperty = uiWindow1Image?["Memo"] as WzSubProperty;
            WzCanvasProperty background = memoProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = background?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MemoMailbox,
                    "Memo",
                    "Fallback owner for the simulator memo inbox and mailbox flow.",
                    position);
            }

            Texture2D unreadTexture = LoadCanvasTexture(memoProperty, "check0", device);
            Texture2D readTexture = LoadCanvasTexture(memoProperty, "check1", device);
            MemoMailboxWindow window = new MemoMailboxWindow(
                new DXObject(0, 0, frameTexture, 0),
                MapSimulatorWindowNames.MemoMailbox,
                device,
                unreadTexture,
                readTexture)
            {
                Position = position
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.InitializeButtons(
                LoadButton(memoProperty, "BtSave", btClickSound, btOverSound, device),
                LoadButton(memoProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(memoProperty, "BtOpen", btClickSound, btOverSound, device));

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

        private static UIWindowBase CreateMemoSendWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty memoProperty = uiWindow2Image?["Memo"]?["Send"] as WzSubProperty;
            if (memoProperty == null)
            {
                return null;
            }

            Texture2D frameTexture = LoadCanvasTexture(memoProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            MemoSendWindow window = new MemoSendWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd3", device, out Point headerOffset),
                headerOffset)
            {
                Position = position
            };

            window.InitializeControls(
                LoadButton(memoProperty, "BtOK", btClickSound, btOverSound, device),
                LoadButton(memoProperty, "BtCancle", btClickSound, btOverSound, device));

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

        private static UIWindowBase CreateMemoGetWindow(
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty memoProperty = uiWindow2Image?["Memo"]?["Get"] as WzSubProperty;
            if (memoProperty == null)
            {
                return null;
            }

            Texture2D frameTexture = LoadCanvasTexture(memoProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty sheetProperty = memoProperty["sheet"] as WzSubProperty;
            IDXObject sheetLayer = LoadWindowCanvasLayerWithOffset(sheetProperty, "innerCenter", device, out Point sheetOffset)
                ?? LoadWindowCanvasLayerWithOffset(sheetProperty, "innerTop", device, out sheetOffset);

            MemoGetWindow window = new MemoGetWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(memoProperty, "backgrnd3", device, out Point headerOffset),
                headerOffset,
                sheetLayer,
                sheetOffset,
                LoadWindowCanvasLayerWithOffset(memoProperty, "line", device, out Point lineOffset),
                lineOffset)
            {
                Position = position
            };

            window.InitializeControls(
                LoadButton(memoProperty, "BtOK", btClickSound, btOverSound, device),
                LoadButton(memoProperty, "BtClame", btClickSound, btOverSound, device));

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

        private static UIWindowBase CreateSocialListWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty mainProperty = userListProperty?["Main"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = mainProperty?["backgrnd"] as WzCanvasProperty
                ?? userListProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.SocialList,
                    "Social",
                    "Fallback owner for friend, party, guild, alliance, and blacklist windows.",
                    position);
            }

            Texture2D[] enabledTabs = new Texture2D[5];
            Texture2D[] disabledTabs = new Texture2D[5];
            WzSubProperty tabProperty = mainProperty?["Tab"] as WzSubProperty ?? userListProperty?["Tab"] as WzSubProperty;
            WzSubProperty enabledTabProperty = tabProperty?["enabled"] as WzSubProperty;
            WzSubProperty disabledTabProperty = tabProperty?["disabled"] as WzSubProperty;
            for (int i = 0; i < enabledTabs.Length; i++)
            {
                enabledTabs[i] = LoadCanvasTexture(enabledTabProperty, i.ToString(), device);
                disabledTabs[i] = LoadCanvasTexture(disabledTabProperty, i.ToString(), device);
            }

            SocialListWindow window = new SocialListWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(mainProperty ?? userListProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                enabledTabs,
                disabledTabs,
                device)
            {
                Position = position
            };

            RegisterSocialListHeader(window, SocialListTab.Friend, mainProperty?["Friend"] as WzSubProperty, "title", device);
            RegisterSocialListHeader(window, SocialListTab.Party, mainProperty?["Party"] as WzSubProperty, "partyOn", device);
            RegisterSocialListHeader(window, SocialListTab.Guild, mainProperty?["Guild"] as WzSubProperty, "guildOn", device);
            RegisterSocialListHeader(window, SocialListTab.Alliance, mainProperty?["Union"] as WzSubProperty, "guildName", device);
            RegisterSocialListHeader(window, SocialListTab.Blacklist, mainProperty?["BlackList"] as WzSubProperty, "base", device);

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.SetPageButtons(
                LoadButton(userListProperty, "BtPagePre", clickSound, overSound, device),
                LoadButton(userListProperty, "BtPageNext", clickSound, overSound, device));
            window.SetFriendFilterButtons(
                LoadButton(mainProperty?["Friend"] as WzSubProperty, "TapShowAll", clickSound, overSound, device),
                LoadButton(mainProperty?["Friend"] as WzSubProperty, "TapShowOnline", clickSound, overSound, device));

            RegisterSocialListActionButtons(window, SocialListTab.Friend, mainProperty?["Friend"] as WzSubProperty, clickSound, overSound, device,
                ("Friend.AddFriend", "BtAddFriend"),
                ("Friend.Party", "BtParty"),
                ("Friend.Whisper", "BtWhisper"),
                ("Friend.Message", "BtMessage"),
                ("Friend.Delete", "BtDelete"),
                ("Friend.Block", "BtBlock"),
                ("Friend.UnBlock", "BtUnBlock"));
            RegisterSocialListActionButtons(window, SocialListTab.Party, mainProperty?["Party"] as WzSubProperty, clickSound, overSound, device,
                ("Party.Create", "BtCreate"),
                ("Party.Invite", "BtInvite"),
                ("Party.Kick", "BtKick"),
                ("Party.Withdraw", "BtWithdraw"),
                ("Party.Whisper", "BtWhisper"),
                ("Party.Chat", "BtChat"),
                ("Party.ChangeBoss", "BtChangeBoss"),
                ("Party.Search", "BtSearch"));
            RegisterSocialListActionButtons(window, SocialListTab.Guild, mainProperty?["Guild"] as WzSubProperty, clickSound, overSound, device,
                ("Guild.Board", "BtBoard"),
                ("Guild.Invite", "BtInvite"),
                ("Guild.Withdraw", "BtWithdraw"),
                ("Guild.PartyInvite", "BtPartyInvite"),
                ("Guild.Kick", "BtKick"),
                ("Guild.Where", "BtWhere"),
                ("Guild.Whisper", "BtWhisper"),
                ("Guild.Info", "BtInfo"));
            RegisterSocialListActionButtons(window, SocialListTab.Alliance, mainProperty?["Union"] as WzSubProperty, clickSound, overSound, device,
                ("Alliance.Invite", "BtInvite"),
                ("Alliance.Withdraw", "BtWithdraw"),
                ("Alliance.PartyInvite", "BtPartyInvite"),
                ("Alliance.Kick", "BtKick"),
                ("Alliance.Change", "BtChange"),
                ("Alliance.Whisper", "BtWhisper"),
                ("Alliance.Info", "BtInfo"));
            RegisterSocialListActionButtons(window, SocialListTab.Blacklist, mainProperty?["BlackList"] as WzSubProperty, clickSound, overSound, device,
                ("Blacklist.Add", "BtAdd"),
                ("Blacklist.Delete", "BtDelete"));

            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            return window;
        }

        private static void RegisterSocialListHeader(
            SocialListWindow window,
            SocialListTab tab,
            WzSubProperty sourceProperty,
            string canvasName,
            GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }

            IDXObject headerLayer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (headerLayer != null)
            {
                window.RegisterHeaderLayer(tab, headerLayer, offset);
            }
        }

        private static void RegisterSocialListActionButtons(
            SocialListWindow window,
            SocialListTab tab,
            WzSubProperty sourceProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            params (string ActionKey, string ButtonName)[] buttonMappings)
        {
            if (window == null || sourceProperty == null || buttonMappings == null)
            {
                return;
            }

            foreach ((string actionKey, string buttonName) in buttonMappings)
            {
                UIObject button = LoadButton(sourceProperty, buttonName, clickSound, overSound, device);
                if (button != null)
                {
                    window.RegisterActionButton(tab, actionKey, button);
                }
            }
        }

        private static UIWindowBase CreateMessengerWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Messenger"] as WzSubProperty
                ?? uiWindow1Image?["Messenger"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Messenger,
                    "Messenger",
                    "Fallback owner for the dedicated Messenger presence and invite surface.",
                    position);
            }

            WzSubProperty maximizedProperty = sourceProperty["Max"] as WzSubProperty ?? sourceProperty;
            WzSubProperty minimizedProperty = sourceProperty["Min"] as WzSubProperty
                ?? sourceProperty["Min2"] as WzSubProperty
                ?? maximizedProperty;
            WzSubProperty nameBarProperty = sourceProperty["Name"] as WzSubProperty
                ?? sourceProperty["NameBar"] as WzSubProperty;

            Texture2D maxFrameTexture = LoadCanvasTexture(maximizedProperty, "backgrnd", device);
            Texture2D minFrameTexture = LoadCanvasTexture(minimizedProperty, "backgrnd", device) ?? maxFrameTexture;
            if (maxFrameTexture == null || minFrameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.Messenger,
                    "Messenger",
                    "Fallback owner for the dedicated Messenger presence and invite surface.",
                    position);
            }

            IDXObject maxFrame = new DXObject(0, 0, maxFrameTexture, 0);
            IDXObject minFrame = new DXObject(0, 0, minFrameTexture, 0);
            IDXObject maxOverlay = LoadWindowCanvasLayerWithOffset(maximizedProperty, "backgrnd2", device, out Point maxOverlayOffset);
            IDXObject maxContent = LoadWindowCanvasLayerWithOffset(maximizedProperty, "backgrnd3", device, out Point maxContentOffset);
            IDXObject minOverlay = LoadWindowCanvasLayerWithOffset(minimizedProperty, "backgrnd2", device, out Point minOverlayOffset);
            IDXObject minContent = LoadWindowCanvasLayerWithOffset(minimizedProperty, "backgrnd3", device, out Point minContentOffset);

            Texture2D[] nameBars = new Texture2D[3];
            for (int i = 0; i < nameBars.Length; i++)
            {
                nameBars[i] = LoadCanvasTexture(nameBarProperty, i.ToString(), device);
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject enterButton = LoadButton(maximizedProperty, "BtEnter", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtEnter", btClickSound, btOverSound, device);
            UIObject claimButton = LoadButton(maximizedProperty, "BtClame", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtClame", btClickSound, btOverSound, device);
            UIObject maximizeButton = LoadButton(minimizedProperty, "BtMax", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtMax", btClickSound, btOverSound, device);
            UIObject minimizeButton = LoadButton(maximizedProperty, "BtMin", btClickSound, btOverSound, device)
                ?? LoadButton(sourceProperty, "BtMin", btClickSound, btOverSound, device);

            MessengerWindow window = new MessengerWindow(
                maxFrame,
                minFrame,
                maxOverlay,
                maxOverlayOffset,
                maxContent,
                maxContentOffset,
                minOverlay,
                minOverlayOffset,
                minContent,
                minContentOffset,
                nameBars,
                device)
            {
                Position = position
            };
            window.InitializeControls(enterButton, claimButton, maximizeButton, minimizeButton);

            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = maxFrameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            return window;
        }

        private static UIWindowBase CreateMapleTvWindow(
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow1Image?["MapleTV"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MapleTv,
                    "MapleTV",
                    "Fallback owner for the MapleTV send board and timed broadcast surface.",
                    position);
            }

            Texture2D selfFrameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            Texture2D receiverFrameTexture = LoadCanvasTexture(sourceProperty, "backgrnd3", device) ?? selfFrameTexture;
            if (selfFrameTexture == null || receiverFrameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MapleTv,
                    "MapleTV",
                    "Fallback owner for the MapleTV send board and timed broadcast surface.",
                    position);
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject okButton = LoadButton(sourceProperty, "BtOk", btClickSound, btOverSound, device);
            UIObject cancelButton = LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device);
            UIObject toButton = LoadButton(sourceProperty, "BtTo", btClickSound, btOverSound, device);
            if (okButton == null || cancelButton == null || toButton == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.MapleTv,
                    "MapleTV",
                    "MapleTV controls were unavailable in this UI dataset, so the simulator is using a placeholder window instead.",
                    position);
            }

            MapleTvWindow window = new MapleTvWindow(
                new DXObject(0, 0, selfFrameTexture, 0),
                new DXObject(0, 0, receiverFrameTexture, 0),
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point selfOverlayOffset),
                selfOverlayOffset,
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd4", device, out Point receiverOverlayOffset),
                receiverOverlayOffset)
            {
                Position = position
            };

            window.InitializeControls(okButton, cancelButton, toButton);

            WzSubProperty closeButtonProperty = basicImage?["BtClose"] as WzSubProperty;
            if (closeButtonProperty != null)
            {
                try
                {
                    UIObject closeBtn = new UIObject(closeButtonProperty, btClickSound, btOverSound, false, Point.Zero, device);
                    closeBtn.X = selfFrameTexture.Width - closeBtn.CanvasSnapshotWidth - 8;
                    closeBtn.Y = 8;
                    window.InitializeCloseButton(closeBtn);
                }
                catch
                {
                }
            }

            return window;
        }

        private static UIWindowBase CreateGuildBbsWindow(
            WzImage guildBbsImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = guildBbsImage?["GuildBBS"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(sourceProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.GuildBbs,
                    "Guild BBS",
                    "Fallback owner for the dedicated guild board thread and reply surface.",
                    position);
            }

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            GuildBbsWindow window = new GuildBbsWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point contentOffset),
                contentOffset,
                device)
            {
                Position = position
            };

            window.InitializeButtons(
                LoadButton(sourceProperty, "BtRegister", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtCancel", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtNotice", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtWrite", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtRetouch", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtDelete", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtQuit", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtReply", btClickSound, btOverSound, device),
                LoadButton(sourceProperty, "BtReplyDelete", btClickSound, btOverSound, device));

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, btClickSound, btOverSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }

            return window;
        }

        private static UIWindowBase CreateItemUpgradeWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position,
            IInventoryRuntime inventory)
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
            itemUpgrade.SetInventory(inventory);

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
            WzSubProperty tabShopProperty = shopProperty["TabShop"] as WzSubProperty;
            WzSubProperty tabShopEnabledProperty = tabShopProperty?["enabled"] as WzSubProperty;
            WzSubProperty tabShopDisabledProperty = tabShopProperty?["disabled"] as WzSubProperty;
            WzSubProperty fadeYesNoProperty = uiWindow2Image?["FadeYesNo"] as WzSubProperty;
            WzSubProperty basicYesProperty = basicImage?["BtYes"] as WzSubProperty;
            WzSubProperty basicNoProperty = basicImage?["BtNo"] as WzSubProperty;

            Texture2D[] categoryEnabledTextures = new Texture2D[10];
            Texture2D[] categoryDisabledTextures = new Texture2D[10];
            for (int i = 0; i < categoryEnabledTextures.Length; i++)
            {
                string tabKey = i.ToString();
                categoryEnabledTextures[i] = LoadCanvasTexture(tabShopEnabledProperty, tabKey, device);
                categoryDisabledTextures[i] = LoadCanvasTexture(tabShopDisabledProperty, tabKey, device);
            }

            Texture2D modalTexture = LoadCanvasTexture(fadeYesNoProperty, "backgrnd7", device);

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject buyButton = LoadButton(shopProperty, "BtBuy", btClickSound, btOverSound, device);
            UIObject sellButton = LoadButton(shopProperty, "BtSell", btClickSound, btOverSound, device);
            UIObject exitButton = LoadButton(shopProperty, "BtExit", btClickSound, btOverSound, device);
            UIObject rechargeButton = LoadButton(shopProperty, "BtRecharge", btClickSound, btOverSound, device);
            UIObject modalConfirmButton = LoadButton(fadeYesNoProperty, "BtOK", btClickSound, btOverSound, device);
            if (modalConfirmButton == null && basicYesProperty != null)
            {
                modalConfirmButton = new UIObject(basicYesProperty, btClickSound, btOverSound, false, Point.Zero, device);
            }

            UIObject modalCancelButton = LoadButton(fadeYesNoProperty, "BtCancel", btClickSound, btOverSound, device);
            if (modalCancelButton == null && basicNoProperty != null)
            {
                modalCancelButton = new UIObject(basicNoProperty, btClickSound, btOverSound, false, Point.Zero, device);
            }

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
                exitButton,
                rechargeButton,
                modalTexture,
                modalConfirmButton,
                modalCancelButton,
                device)
            {
                Position = position,
                Money = 0
            };

            window.SetCategoryTabTextures(categoryEnabledTextures, categoryDisabledTextures);

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

        private static AranSkillGuideUI CreateAranSkillGuideWindowBigBang(
            WzImage uiWindow2Image,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            WzSubProperty aranSkillGuideProperty = uiWindow2Image?["AranSkillGuide"] as WzSubProperty;
            if (aranSkillGuideProperty == null)
            {
                return null;
            }

            IDXObject[] pages = new IDXObject[4];
            int pageWidth = 0;
            int pageHeight = 0;
            for (int i = 0; i < pages.Length; i++)
            {
                WzCanvasProperty pageProperty = aranSkillGuideProperty[i.ToString()] as WzCanvasProperty;
                Texture2D pageTexture = pageProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (pageTexture == null)
                {
                    continue;
                }

                pageWidth = Math.Max(pageWidth, pageTexture.Width);
                pageHeight = Math.Max(pageHeight, pageTexture.Height);
                pages[i] = new DXObject(0, 0, pageTexture, 0);
            }

            if (Array.TrueForAll(pages, page => page == null))
            {
                return null;
            }

            AranSkillGuideUI window = new AranSkillGuideUI(pages)
            {
                Position = new Point(
                    Math.Max(0, (screenWidth - pageWidth) / 2),
                    Math.Max(0, (screenHeight - pageHeight) / 2))
            };

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            UIObject closeBtn = LoadButton(aranSkillGuideProperty, "BtClose", btClickSound, btOverSound, device);
            if (closeBtn != null)
            {
                int closeWidth = Math.Max(0, closeBtn.CanvasSnapshotWidth);
                closeBtn.X = Math.Max(0, pageWidth - closeWidth - 7);
                closeBtn.Y = 7;
                window.InitializeCloseButton(closeBtn);
            }

            return window;
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
            inventory.AddItem(InventoryType.ETC, 4130018, null, 3); // Basic catalyst
            inventory.AddMeso(250000);
        }

        private static void SeedStarterTrunkInventory(TrunkUI trunk)
        {
            if (trunk == null)
            {
                return;
            }

            trunk.SetStorageMeso(1250000);
            trunk.AddStoredItem(InventoryType.EQUIP, new InventorySlotData { ItemId = 1302000, GradeFrameIndex = 0 });
            trunk.AddStoredItem(InventoryType.USE, new InventorySlotData { ItemId = 2000005, Quantity = 30 });
            trunk.AddStoredItem(InventoryType.SETUP, new InventorySlotData { ItemId = 3010002, Quantity = 1 });
            trunk.AddStoredItem(InventoryType.ETC, new InventorySlotData { ItemId = 4000019, Quantity = 120 });
            trunk.AddStoredItem(InventoryType.CASH, new InventorySlotData { ItemId = 5150040, Quantity = 1 });
        }

        private static void SeedStarterEnhancementInventory(IInventoryRuntime inventory)
        {
            if (inventory == null)
            {
                return;
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049301) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049301, null, 12); // Equipment Enhancement Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049300) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049300, null, 4); // Advanced Equipment Enhancement Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049309) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049309, null, 2); // 2-Star Enhancement Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049304) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049304, null, 1); // 3 Star Enhancement Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049305) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049305, null, 1); // 4 Star Enhancement Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049308) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049308, null, 1); // 5 Star Enhancement Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049401) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049401, null, 2); // Potential Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049400) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049400, null, 1); // Advanced Potential Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049406) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049406, null, 1); // Special Potential Scroll
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5062000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062000, null, 2); // Miracle Cube
            }
        }
        #endregion
    }
}
