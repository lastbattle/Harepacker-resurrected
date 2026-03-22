using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.UI.Controls;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
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

            WzSubProperty equipTooltipProperty = uiWindow2Image?["ToolTip"]?["Equip"] as WzSubProperty;
            if (equipTooltipProperty != null)
            {
                equip.SetEquipTooltipAssets(new EquipUIBigBang.EquipTooltipAssets
                {
                    CanLabels = LoadCanvasTextureMap(equipTooltipProperty["Can"] as WzSubProperty, device),
                    CannotLabels = LoadCanvasTextureMap(equipTooltipProperty["Cannot"] as WzSubProperty, device),
                    PropertyLabels = LoadCanvasTextureMap(equipTooltipProperty["Property"] as WzSubProperty, device),
                    ItemCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["ItemCategory"] as WzSubProperty, device),
                    WeaponCategoryLabels = LoadCanvasTextureMap(equipTooltipProperty["WeaponCategory"] as WzSubProperty, device),
                    SpeedLabels = LoadCanvasTextureMap(equipTooltipProperty["Speed"] as WzSubProperty, device)
                });
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

        private static Rectangle ResolveCanvasBounds(
            WzCanvasProperty canvas,
            Texture2D texture,
            int fallbackX,
            int fallbackY,
            int fallbackWidth,
            int fallbackHeight)
        {
            int x = fallbackX;
            int y = fallbackY;
            if (canvas != null)
            {
                System.Drawing.PointF? origin = canvas.GetCanvasOriginPosition();
                if (origin.HasValue)
                {
                    x = -(int)origin.Value.X;
                    y = -(int)origin.Value.Y;
                }
            }

            return new Rectangle(
                x,
                y,
                texture?.Width ?? fallbackWidth,
                texture?.Height ?? fallbackHeight);
        }

        private static List<Texture2D> CreateFallbackProgressFrames(
            GraphicsDevice device,
            int width,
            int height,
            int frameCount)
        {
            List<Texture2D> frames = new List<Texture2D>();
            if (device == null || width <= 0 || height <= 0 || frameCount <= 0)
            {
                return frames;
            }

            Color trackColor = new Color(30, 36, 48, 230);
            Color fillColor = new Color(127, 196, 255, 235);
            int borderThickness = Math.Min(1, Math.Min(width, height) - 1);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float progress = frameCount == 1 ? 1f : frameIndex / (float)(frameCount - 1);
                int fillWidth = Math.Clamp((int)Math.Round(width * progress), 0, width);
                Color[] data = new Color[width * height];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (y * width) + x;
                        bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                        if (isBorder && borderThickness > 0)
                        {
                            data[index] = Color.Black;
                        }
                        else
                        {
                            data[index] = x < fillWidth ? fillColor : trackColor;
                        }
                    }
                }

                Texture2D texture = new Texture2D(device, width, height);
                texture.SetData(data);
                frames.Add(texture);
            }

            return frames;
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
                Rectangle[] tabEnabledRects = new Rectangle[5];
                Rectangle[] tabDisabledRects = new Rectangle[5];

                WzSubProperty enabledProperty = (WzSubProperty)tabProperty["enabled"];
                WzSubProperty disabledProperty = (WzSubProperty)tabProperty["disabled"];

                for (int i = 0; i < 5; i++)
                {
                    string tabIndex = i.ToString();
                    tabEnabledRects[i] = new Rectangle(10 + (i * 31), 27, 30, 20);
                    tabDisabledRects[i] = new Rectangle(10 + (i * 31), 29, 30, 18);

                    if (enabledProperty != null)
                    {
                        tabEnabled[i] = LoadCanvasTexture(enabledProperty, tabIndex, device);
                        tabEnabledRects[i] = ResolveCanvasBounds(
                            enabledProperty[tabIndex] as WzCanvasProperty,
                            tabEnabled[i],
                            10 + (i * 31),
                            27,
                            30,
                            20);
                    }
                    if (disabledProperty != null)
                    {
                        tabDisabled[i] = LoadCanvasTexture(disabledProperty, tabIndex, device);
                        tabDisabledRects[i] = ResolveCanvasBounds(
                            disabledProperty[tabIndex] as WzCanvasProperty,
                            tabDisabled[i],
                            10 + (i * 31),
                            29,
                            30,
                            18);
                    }
                }

                skill.SetTabTextures(tabEnabled, tabDisabled);
                skill.SetTabLayout(tabEnabledRects, tabDisabledRects);
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

            UIObject rideBtn = LoadButton(mainProperty, "BtRide", btClickSound, btOverSound, device);
            if (rideBtn != null)
            {
                rideBtn.X = 62;
                rideBtn.Y = 273;
            }
            skill.InitializeRideButton(rideBtn);

            UIObject guildSkillBtn = LoadButton(mainProperty, "BtGuildSkill", btClickSound, btOverSound, device);
            if (guildSkillBtn != null)
            {
                guildSkillBtn.X = 10;
                guildSkillBtn.Y = 273;
            }
            skill.InitializeGuildSkillButton(guildSkillBtn);

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

        private static Dictionary<string, Texture2D> LoadCanvasTextureMap(WzSubProperty parent, GraphicsDevice device)
        {
            var textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            if (parent == null)
            {
                return textures;
            }

            foreach (WzImageProperty property in parent.WzProperties)
            {
                Texture2D texture = LoadCanvasTexture(parent, property.Name, device);
                if (texture != null)
                {
                    textures[property.Name] = texture;
                }
            }

            return textures;
        }

        private static Texture2D[] LoadDigitTextures(WzSubProperty parent, GraphicsDevice device)
        {
            Texture2D[] textures = new Texture2D[10];
            if (parent == null)
            {
                return textures;
            }

            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = LoadCanvasTexture(parent, i.ToString(), device);
            }

            return textures;
        }

        private static Texture2D[] LoadGuildBbsEmoticonSet(WzSubProperty parent, int count, GraphicsDevice device)
        {
            Texture2D[] textures = new Texture2D[count];
            if (parent == null)
            {
                return textures;
            }

            for (int i = 0; i < count; i++)
            {
                textures[i] = LoadCanvasTexture(parent, i.ToString(), device);
            }

            return textures;
        }

        private static IReadOnlyList<VegaSpellUI.VegaAnimationFrame> LoadAnimationFrames(WzSubProperty parent, GraphicsDevice device)
        {
            var frames = new List<VegaSpellUI.VegaAnimationFrame>();
            if (parent == null || device == null)
            {
                return frames;
            }

            for (int i = 0; ; i++)
            {
                if (parent[i.ToString()] is not WzCanvasProperty canvas)
                {
                    break;
                }

                Texture2D texture = canvas.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
                Point origin = ResolveCanvasOffset(canvas, Point.Zero);
                int delay = InfoTool.GetInt(canvas["delay"], 100);
                frames.Add(new VegaSpellUI.VegaAnimationFrame(texture, new Point(-origin.X, -origin.Y), delay));
            }

            return frames;
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
            SeedStarterCompanionInventory(inventory, device);

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
            quest.InitializeDetailButton(LoadButton(questProperty, "BtDetail", btClickSound, btOverSound, device));

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
                ? CreateQuestDetailWindowBigBang(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight)
                : CreateQuestDetailWindowPreBigBang(uiWindow1Image, basicImage, soundUIImage, device, screenWidth, screenHeight);

            if (window != null)
            {
                return window;
            }

            return CreatePlaceholderQuestDetailWindow(device, basicImage, soundUIImage, screenWidth, screenHeight);
        }

        private static QuestDetailWindow CreateQuestDetailWindowBigBang(
            WzImage uiWindow1Image,
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
            WzSubProperty legacyQuestProperty = uiWindow1Image?["Quest"] as WzSubProperty;

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
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "select", device),
                LoadCanvasTexture(questInfoProperty["summary_icon"] as WzSubProperty, "prob", device));
            window.SetProgressTextures(
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questInfoProperty["Gauge"] as WzSubProperty, "spot", device),
                new Point(30, 0));
            InitializeQuestDetailNoticeArt(window, uiWindow2Image?["Quest"]?["list"] as WzSubProperty, legacyQuestProperty, device);

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            InitializeQuestDetailButtons(window, questInfoProperty, legacyQuestProperty, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, true);
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
                LoadCanvasTexture(questProperty, "select", device),
                LoadCanvasTexture(questProperty, "prob", device));
            window.SetProgressTextures(
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questProperty["Gauge"] as WzSubProperty, "spot", device),
                new Point(32, 0));
            InitializeQuestDetailNoticeArt(window, questProperty, questProperty, device);

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            window.InitializeCloseButton(closeButton);
            InitializeQuestDetailButtons(window, questProperty, questProperty, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, false);
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
            InitializeQuestDetailButtons(window, null, null, clickSound, overSound, device, frameTexture.Width, frameTexture.Height, true);
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
            WzSubProperty legacyButtonSource,
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
            UIObject markMobButton = CreateQuestDetailActionButton((legacyButtonSource?["BtMarkMob"]) as WzSubProperty, clickSound, overSound, device);
            UIObject genericNpcButton = CreateQuestDetailActionButton((isBigBang ? buttonSource?["BtNPC"] : null) as WzSubProperty, clickSound, overSound, device);
            UIObject markNpcButton = CreateQuestDetailActionButton((legacyButtonSource?["BtMarkNpc"]) as WzSubProperty, clickSound, overSound, device)
                                      ?? CreateQuestDetailActionButton((isBigBang ? buttonSource?["BtNPC"] : null) as WzSubProperty, clickSound, overSound, device);
            UIObject gotoNpcButton = CreateQuestDetailActionButton((legacyButtonSource?["BtGotoNpc"]) as WzSubProperty, clickSound, overSound, device)
                                      ?? CreateQuestDetailActionButton((isBigBang ? buttonSource?["BtNPC"] : null) as WzSubProperty, clickSound, overSound, device);

            acceptButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
            completeButton ??= CreateFallbackQuestDetailButton(device, 117, 16);
            trackButton ??= CreateFallbackQuestDetailButton(device, 86, 17);
            giveUpButton ??= CreateFallbackQuestDetailButton(device, 60, 16);
            markMobButton ??= CreateFallbackQuestDetailButton(device, 78, 16);
            genericNpcButton ??= CreateFallbackQuestDetailButton(device, 80, 16);
            markNpcButton ??= CreateFallbackQuestDetailButton(device, 81, 17);
            gotoNpcButton ??= CreateFallbackQuestDetailButton(device, 105, 16);

            PositionQuestDetailActionButton(acceptButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(completeButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(trackButton, frameWidth, frameHeight, 12, 8);
            PositionQuestDetailActionButton(giveUpButton, frameWidth, frameHeight, 16, acceptButton?.CanvasSnapshotWidth ?? completeButton?.CanvasSnapshotWidth ?? trackButton?.CanvasSnapshotWidth ?? 78);
            int npcOffset = (acceptButton?.CanvasSnapshotWidth ?? completeButton?.CanvasSnapshotWidth ?? trackButton?.CanvasSnapshotWidth ?? 78)
                + (giveUpButton?.CanvasSnapshotWidth ?? 60) + 20;
            PositionQuestDetailActionButton(genericNpcButton, frameWidth, frameHeight, 16, npcOffset);
            PositionQuestDetailActionButton(markNpcButton, frameWidth, frameHeight, 16, npcOffset);
            PositionQuestDetailActionButton(gotoNpcButton, frameWidth, frameHeight, 16, npcOffset);

            window.RegisterActionButton(QuestWindowActionKind.Accept, acceptButton, !hasAcceptArt);
            window.RegisterActionButton(QuestWindowActionKind.Complete, completeButton, !hasCompleteArt);
            window.RegisterActionButton(QuestWindowActionKind.Track, trackButton, !hasTrackArt);
            window.RegisterActionButton(QuestWindowActionKind.GiveUp, giveUpButton, !hasGiveUpArt);
            window.RegisterActionButton(QuestWindowActionKind.LocateMob, markMobButton, legacyButtonSource?["BtMarkMob"] is not WzSubProperty);
            window.RegisterNpcButton(QuestDetailNpcButtonStyle.GenericNpc, genericNpcButton, buttonSource?["BtNPC"] is not WzSubProperty);
            window.RegisterNpcButton(QuestDetailNpcButtonStyle.MarkNpc, markNpcButton, legacyButtonSource?["BtMarkNpc"] is not WzSubProperty);
            window.RegisterNpcButton(QuestDetailNpcButtonStyle.GotoNpc, gotoNpcButton, legacyButtonSource?["BtGotoNpc"] is not WzSubProperty);
            window.InitializeNavigationButtons(device);
        }

        private static void InitializeQuestDetailNoticeArt(
            QuestDetailWindow window,
            WzSubProperty noticeSource,
            WzSubProperty legacyQuestSource,
            GraphicsDevice device)
        {
            if (window == null)
            {
                return;
            }

            string[] noticeNames = { "notice0", "notice1", "notice2", "notice3" };
            Texture2D[] noticeTextures = new Texture2D[noticeNames.Length];
            Point[] noticeOffsets = new Point[noticeNames.Length];
            for (int i = 0; i < noticeNames.Length; i++)
            {
                noticeTextures[i] = LoadCanvasTexture(noticeSource, noticeNames[i], device) ?? LoadCanvasTexture(legacyQuestSource, noticeNames[i], device);
                noticeOffsets[i] = ResolveCanvasOffset(noticeSource, noticeNames[i], ResolveCanvasOffset(legacyQuestSource, noticeNames[i], new Point(118, 74)));
            }

            LoadQuestDetailNoticeAnimation(
                legacyQuestSource?["icon3"] as WzSubProperty
                ?? legacyQuestSource?["icon2"] as WzSubProperty
                ?? legacyQuestSource?["icon5"] as WzSubProperty,
                device,
                out Texture2D[] animationFrames,
                out int[] animationDelays);

            Point animationOffset = new Point(noticeOffsets[0].X + 8, noticeOffsets[0].Y + 9);
            window.SetNoticeTextures(noticeTextures, noticeOffsets, animationFrames, animationDelays, animationOffset);
        }

        private static void LoadQuestDetailNoticeAnimation(
            WzSubProperty animationSource,
            GraphicsDevice device,
            out Texture2D[] frames,
            out int[] delays)
        {
            if (animationSource?.WzProperties == null || animationSource.WzProperties.Count == 0)
            {
                frames = Array.Empty<Texture2D>();
                delays = Array.Empty<int>();
                return;
            }

            List<Texture2D> loadedFrames = new();
            List<int> loadedDelays = new();
            for (int i = 0; i < animationSource.WzProperties.Count; i++)
            {
                if (animationSource.WzProperties[i] is not WzCanvasProperty canvas)
                {
                    continue;
                }

                Texture2D frame = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (frame == null)
                {
                    continue;
                }

                loadedFrames.Add(frame);
                loadedDelays.Add(Math.Max(1, canvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 120));
            }

            frames = loadedFrames.ToArray();
            delays = loadedDelays.ToArray();
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

        private static IReadOnlyList<Texture2D> LoadButtonAnimationTextures(WzSubProperty buttonProperty, GraphicsDevice device)
        {
            WzSubProperty animationProperty = buttonProperty?["ani"] as WzSubProperty;
            if (animationProperty == null)
            {
                return Array.Empty<Texture2D>();
            }

            var frames = new List<Texture2D>();
            for (int i = 0; ; i++)
            {
                if (animationProperty[i.ToString()] is not WzCanvasProperty canvas)
                {
                    break;
                }

                try
                {
                    System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                    Texture2D texture = bitmap?.ToTexture2DAndDispose(device);
                    if (texture != null)
                    {
                        frames.Add(texture);
                    }
                }
                catch
                {
                    break;
                }
            }

            return frames;
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
            InventoryUI inventory,
            IStorageRuntime storageRuntime)
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
            window.SetStorageRuntime(storageRuntime);
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
                LoadCanvasTexture(worldMapProperty, "title", device),
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
            RegisterUserInfoItemPopup(window, userInfoProperty?["item"] as WzSubProperty, device);
            RegisterUserInfoWishPopup(window, userInfoProperty?["wish"] as WzSubProperty, clickSound, overSound, device);
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

        private static void RegisterUserInfoItemPopup(
            UserInfoUI window,
            WzSubProperty itemProperty,
            GraphicsDevice device)
        {
            if (window == null || itemProperty == null)
            {
                return;
            }

            window.InitializeItemPopup(
                CreateUserInfoPopupFrame(itemProperty, device),
                CreateUserInfoPopupLayers(itemProperty, device));
        }

        private static void RegisterUserInfoWishPopup(
            UserInfoUI window,
            WzSubProperty wishProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device)
        {
            if (window == null || wishProperty == null)
            {
                return;
            }

            window.InitializeWishPopup(
                CreateUserInfoPopupFrame(wishProperty, device),
                CreateUserInfoPopupLayers(wishProperty, device),
                LoadButton(wishProperty, "BtPresent", clickSound, overSound, device));
        }

        private static IDXObject CreateUserInfoPopupFrame(WzSubProperty popupProperty, GraphicsDevice device)
        {
            Texture2D frameTexture = LoadCanvasTexture(popupProperty, "backgrnd", device);
            return frameTexture != null ? new DXObject(0, 0, frameTexture, 0) : null;
        }

        private static IEnumerable<(IDXObject layer, Point offset)> CreateUserInfoPopupLayers(WzSubProperty popupProperty, GraphicsDevice device)
        {
            if (popupProperty == null)
            {
                yield break;
            }

            foreach (WzCanvasProperty canvas in popupProperty.WzProperties.OfType<WzCanvasProperty>())
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

                yield return (new DXObject(0, 0, layerTexture, 0), ResolveCanvasOffset(canvas, Point.Zero));
            }
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

        private static UIObject CreateCanvasButton(WzCanvasProperty normalCanvas, WzCanvasProperty pressedCanvas, GraphicsDevice device)
        {
            if (normalCanvas == null)
            {
                return null;
            }

            try
            {
                Texture2D normalTexture = normalCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Texture2D pressedTexture = (pressedCanvas ?? normalCanvas).GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device) ?? normalTexture;
                if (normalTexture == null || pressedTexture == null)
                {
                    return null;
                }

                Point normalOffset = ResolveCanvasOffset(normalCanvas, Point.Zero);
                Point pressedOffset = ResolveCanvasOffset(pressedCanvas ?? normalCanvas, normalOffset);
                BaseDXDrawableItem normal = new BaseDXDrawableItem(new DXObject(normalOffset.X, normalOffset.Y, normalTexture, 0), false);
                BaseDXDrawableItem disabled = new BaseDXDrawableItem(new DXObject(normalOffset.X, normalOffset.Y, normalTexture, 0), false);
                BaseDXDrawableItem pressed = new BaseDXDrawableItem(new DXObject(pressedOffset.X, pressedOffset.Y, pressedTexture, 0), false);
                BaseDXDrawableItem mouseOver = new BaseDXDrawableItem(new DXObject(pressedOffset.X, pressedOffset.Y, pressedTexture, 0), false);
                UIObject button = new UIObject(normal, disabled, pressed, mouseOver);
                button.X = normalOffset.X;
                button.Y = normalOffset.Y;
                return button;
            }
            catch
            {
                return null;
            }
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

        public static void RegisterLoginTitleWindow(
            UIWindowManager manager,
            WzImage loginImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.LoginTitle) != null)
            {
                return;
            }

            WzSubProperty titleProperty = loginImage?["Title"] as WzSubProperty;
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            IDXObject frame = LoadWindowCanvasLayer(titleProperty, "backFrame", device);
            IDXObject titleLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "MSTitle", device, out Point titleOffset);
            IDXObject dollsLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "MapleDolls", device, out Point dollsOffset);
            IDXObject signboardLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "signboard", device, out Point signboardOffset);
            IDXObject idFieldLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "ID", device, out Point idFieldOffset);
            IDXObject passwordFieldLayer = LoadWindowCanvasLayerWithOffset(titleProperty, "PW", device, out Point passwordFieldOffset);

            if (frame == null)
            {
                Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 800, 412, "Login");
                frame = new DXObject(0, 0, frameTexture, 0);
            }

            UIObject loginButton = LoadButton(titleProperty, "BtLogin", btClickSound, btOverSound, device);
            UIObject guestLoginButton = LoadButton(titleProperty, "BtGuestLogin", btClickSound, btOverSound, device);
            UIObject newButton = LoadButton(titleProperty, "BtNew", btClickSound, btOverSound, device);
            UIObject quitButton = LoadButton(titleProperty, "BtQuit", btClickSound, btOverSound, device);
            UIObject saveIdButton = LoadButton(titleProperty, "BtLoginIDSave", btClickSound, btOverSound, device);
            UIObject idLostButton = LoadButton(titleProperty, "BtLoginIDLost", btClickSound, btOverSound, device);
            UIObject passwordLostButton = LoadButton(titleProperty, "BtPasswdLost", btClickSound, btOverSound, device);

            const int ownerOffsetX = 334;
            const int ownerOffsetY = 225;

            if (loginButton != null)
            {
                loginButton.X = ownerOffsetX + 178;
                loginButton.Y = ownerOffsetY + 15;
            }

            if (guestLoginButton != null)
            {
                guestLoginButton.X = ownerOffsetX + 178;
                guestLoginButton.Y = ownerOffsetY + 49;
            }

            if (saveIdButton != null)
            {
                saveIdButton.X = ownerOffsetX + 27;
                saveIdButton.Y = ownerOffsetY + 68;
            }

            if (idLostButton != null)
            {
                idLostButton.X = ownerOffsetX + 99;
                idLostButton.Y = ownerOffsetY + 68;
            }

            if (passwordLostButton != null)
            {
                passwordLostButton.X = ownerOffsetX + 171;
                passwordLostButton.Y = ownerOffsetY + 68;
            }

            if (newButton != null)
            {
                newButton.X = ownerOffsetX + 15;
                newButton.Y = ownerOffsetY + 88;
            }

            if (quitButton != null)
            {
                quitButton.X = ownerOffsetX + 159;
                quitButton.Y = ownerOffsetY + 88;
            }

            LoginTitleWindow window = new LoginTitleWindow(
                frame,
                titleLayer,
                titleOffset,
                dollsLayer,
                dollsOffset,
                signboardLayer,
                signboardOffset,
                idFieldLayer,
                idFieldOffset,
                passwordFieldLayer,
                passwordFieldOffset,
                loginButton,
                guestLoginButton,
                newButton,
                quitButton,
                saveIdButton,
                idLostButton,
                passwordLostButton)
            {
                Position = new Point(
                    Math.Max(0, (screenWidth / 2) - 400),
                    Math.Max(0, (screenHeight / 2) - 300))
            };

            manager.RegisterCustomWindow(window);
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

            WzImage loginImage = global::HaCreator.Program.FindImage("UI", "Login.img");
            WzSubProperty charSelectProperty = loginImage?["CharSelect"] as WzSubProperty;
            Texture2D frameTexture = CreatePlaceholderWindowTexture(device, 618, 320, "Character Select");
            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            UIObject enterButton = LoadButton(charSelectProperty, "BtSelect", btClickSound, btOverSound, device);
            UIObject newButton = LoadButton(charSelectProperty, "BtNew", btClickSound, btOverSound, device);
            UIObject deleteButton = LoadButton(charSelectProperty, "BtDelete", btClickSound, btOverSound, device);

            if (enterButton != null)
            {
                enterButton.X = 148;
                enterButton.Y = 246;
            }

            if (newButton != null)
            {
                newButton.X = 259;
                newButton.Y = 244;
            }

            if (deleteButton != null)
            {
                deleteButton.X = 370;
                deleteButton.Y = 238;
            }

            CharacterSelectWindow window = new CharacterSelectWindow(
                new DXObject(0, 0, frameTexture, 0),
                enterButton,
                newButton,
                deleteButton)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 309), Math.Max(24, (screenHeight / 2) - 160))
            };

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

            RegisterAvatarPreviewCarouselWindow(manager, charSelectProperty, device, screenWidth, screenHeight);
        }

        private static void RegisterAvatarPreviewCarouselWindow(
            UIWindowManager manager,
            WzSubProperty charSelectProperty,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.AvatarPreviewCarousel) != null)
            {
                return;
            }

            Texture2D cardNormalTexture = LoadCanvasTexture(charSelectProperty, "charInfo", device)
                ?? CreateFilledTexture(device, 183, 115, new Color(245, 231, 206, 255), new Color(66, 44, 32, 255));
            Texture2D cardSelectedTexture = LoadCanvasTexture(charSelectProperty, "charInfo1", device)
                ?? cardNormalTexture;
            Texture2D frameTexture = CreateFilledTexture(device, 618, 238, Color.Transparent, Color.Transparent);
            WzSubProperty nameTagProperty = charSelectProperty?["nameTag"] as WzSubProperty;
            var normalNameTagStyle = LoadPreviewNameTagStyle(
                nameTagProperty?["0"] as WzSubProperty,
                device,
                new Color(153, 153, 153));
            var selectedNameTagStyle = LoadPreviewNameTagStyle(
                nameTagProperty?["1"] as WzSubProperty,
                device,
                Color.White);
            Dictionary<AvatarPreviewCarouselWindow.LoginJobDecorationStyle, AvatarPreviewCarouselWindow.PreviewCanvasFrame> jobDecorations = new()
            {
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Adventure] = LoadPreviewCanvasFrame(charSelectProperty?["adventure"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Knight] = LoadPreviewCanvasFrame(charSelectProperty?["knight"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Aran] = LoadPreviewCanvasFrame(charSelectProperty?["aran"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Evan] = LoadPreviewCanvasFrame(charSelectProperty?["evan"]?["0"] as WzCanvasProperty, device),
                [AvatarPreviewCarouselWindow.LoginJobDecorationStyle.Resistance] = LoadPreviewCanvasFrame(charSelectProperty?["resistance"]?["0"] as WzCanvasProperty, device)
            };

            List<UIObject> cardButtons = new List<UIObject>();
            Texture2D hitTexture = CreateFilledTexture(device, 183, 151, Color.Transparent, Color.Transparent);
            for (int slot = 0; slot < 3; slot++)
            {
                UIObject cardButton = CreateTextureButton(hitTexture, hitTexture);
                if (cardButton == null)
                {
                    continue;
                }

                cardButton.X = 18 + (slot * 197);
                cardButton.Y = 46;
                cardButtons.Add(cardButton);
            }

            UIObject prevPageButton = CreateCanvasButton(
                charSelectProperty?["pageL"]?["0"]?["0"] as WzCanvasProperty,
                charSelectProperty?["pageL"]?["1"]?["0"] as WzCanvasProperty,
                device);
            UIObject nextPageButton = CreateCanvasButton(
                charSelectProperty?["pageR"]?["0"]?["0"] as WzCanvasProperty,
                charSelectProperty?["pageR"]?["1"]?["0"] as WzCanvasProperty,
                device);

            if (prevPageButton != null)
            {
                prevPageButton.X = -2;
                prevPageButton.Y = 82;
            }

            if (nextPageButton != null)
            {
                nextPageButton.X = 531;
                nextPageButton.Y = 82;
            }

            AvatarPreviewCarouselWindow previewWindow = new AvatarPreviewCarouselWindow(
                new DXObject(0, 0, frameTexture, 0),
                cardNormalTexture,
                cardSelectedTexture,
                normalNameTagStyle,
                selectedNameTagStyle,
                jobDecorations,
                cardButtons,
                prevPageButton,
                nextPageButton)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - 309), Math.Max(24, (screenHeight / 2) - 160))
            };

            manager.RegisterCustomWindow(previewWindow);
        }

        private static AvatarPreviewCarouselWindow.PreviewNameTagStyle LoadPreviewNameTagStyle(
            WzSubProperty sourceProperty,
            GraphicsDevice device,
            Color textColor)
        {
            return new AvatarPreviewCarouselWindow.PreviewNameTagStyle(
                LoadCanvasTexture(sourceProperty, "0", device),
                LoadCanvasTexture(sourceProperty, "1", device),
                LoadCanvasTexture(sourceProperty, "2", device),
                textColor);
        }

        private static AvatarPreviewCarouselWindow.PreviewCanvasFrame LoadPreviewCanvasFrame(WzCanvasProperty canvas, GraphicsDevice device)
        {
            if (canvas == null || device == null)
            {
                return default;
            }

            try
            {
                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                Point origin = canvas.GetCanvasOriginPosition() is System.Drawing.PointF canvasOrigin
                    ? new Point((int)canvasOrigin.X, (int)canvasOrigin.Y)
                    : Point.Zero;
                return new AvatarPreviewCarouselWindow.PreviewCanvasFrame(texture, origin);
            }
            catch
            {
                return default;
            }
        }

        public static void RegisterConnectionNoticeWindow(
            UIWindowManager manager,
            WzImage loginImage,
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

            WzSubProperty noticeProperty = loginImage?["Notice"] as WzSubProperty;
            IDXObject noticeFrame = LoadWindowCanvasLayer(noticeProperty?["backgrnd"] as WzSubProperty, "0", device);
            IDXObject noticeCogFrame = LoadWindowCanvasLayer(noticeProperty?["backgrnd"] as WzSubProperty, "1", device);
            IDXObject noticeBarFrame = LoadWindowCanvasLayer(noticeProperty?["backgrnd"] as WzSubProperty, "2", device);
            IDXObject loadingFrame = LoadWindowCanvasLayer(noticeProperty?["Loading"] as WzSubProperty, "backgrnd", device);
            IDXObject loadingSingleGaugeFrame = LoadWindowCanvasLayer(noticeProperty?["LoadingSG"] as WzSubProperty, "backgrnd", device);

            if (noticeFrame == null)
            {
                Texture2D noticeTexture = CreatePlaceholderWindowTexture(device, 249, 142, "Connection Notice");
                noticeFrame = new DXObject(0, 0, noticeTexture, 0);
            }

            noticeCogFrame ??= noticeFrame;
            noticeBarFrame ??= noticeFrame;
            if (loadingFrame == null)
            {
                loadingFrame = noticeFrame;
            }

            loadingSingleGaugeFrame ??= noticeBarFrame ?? noticeFrame;

            List<Texture2D> progressFrames = new List<Texture2D>();
            WzSubProperty progressBarProperty = noticeProperty?["Loading"]?["bar"] as WzSubProperty;
            for (int i = 0; i <= 10; i++)
            {
                Texture2D frame = LoadCanvasTexture(progressBarProperty, i.ToString(), device);
                if (frame != null)
                {
                    progressFrames.Add(frame);
                }
            }

            if (progressFrames.Count == 0)
            {
                progressFrames = CreateFallbackProgressFrames(device, 109, 8, 11);
            }

            List<Texture2D> singleGaugeProgressFrames = new List<Texture2D>();
            WzSubProperty singleGaugeBarProperty = noticeProperty?["LoadingSG"]?["bar"] as WzSubProperty;
            for (int i = 0; i <= 9; i++)
            {
                Texture2D frame = LoadCanvasTexture(singleGaugeBarProperty, i.ToString(), device);
                if (frame != null)
                {
                    singleGaugeProgressFrames.Add(frame);
                }
            }

            if (singleGaugeProgressFrames.Count == 0)
            {
                singleGaugeProgressFrames = CreateFallbackProgressFrames(device, 137, 11, 10);
            }

            Dictionary<int, Texture2D> noticeTextTextures = LoadIndexedCanvasTextures(
                noticeProperty?["text"] as WzSubProperty,
                device);
            Dictionary<ConnectionNoticeWindowVariant, IDXObject> framesByVariant = new()
            {
                [ConnectionNoticeWindowVariant.Notice] = noticeFrame,
                [ConnectionNoticeWindowVariant.NoticeCog] = noticeCogFrame,
                [ConnectionNoticeWindowVariant.Loading] = loadingFrame,
                [ConnectionNoticeWindowVariant.LoadingSingleGauge] = loadingSingleGaugeFrame,
            };
            Dictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> progressFramesByVariant = new()
            {
                [ConnectionNoticeWindowVariant.Loading] = progressFrames,
                [ConnectionNoticeWindowVariant.LoadingSingleGauge] = singleGaugeProgressFrames,
            };

            ConnectionNoticeWindow window = new ConnectionNoticeWindow(
                framesByVariant,
                progressFramesByVariant,
                noticeTextTextures)
            {
                Position = new Point(
                    Math.Max(24, (screenWidth / 2) - ((noticeFrame.Width > 0 ? noticeFrame.Width : 249) / 2)),
                    Math.Max(24, (screenHeight / 2) - ((noticeFrame.Height > 0 ? noticeFrame.Height : 142) / 2)))
            };

            manager.RegisterCustomWindow(window);
        }

        public static void RegisterLoginUtilityDialogWindow(
            UIWindowManager manager,
            WzImage uiWindow2Image,
            WzImage loginImage,
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

            WzBinaryProperty btClickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty btOverSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty utilDlgProperty = uiWindow2Image?["UtilDlgEx"] as WzSubProperty;
            WzSubProperty loginNoticeProperty = loginImage?["Notice"] as WzSubProperty;
            Dictionary<int, Texture2D> noticeTextTextures = LoadIndexedCanvasTextures(
                loginNoticeProperty?["text"] as WzSubProperty,
                device);

            Texture2D frameTexture = LoadCanvasTexture(utilDlgProperty, "notice", device)
                                     ?? LoadCanvasTexture(loginNoticeProperty?["backgrnd"] as WzSubProperty, "0", device)
                                     ?? CreatePlaceholderWindowTexture(device, 312, 132, "Login Utility");

            UIObject okButton = LoadButton(utilDlgProperty, "BtOK", btClickSound, btOverSound, device);
            UIObject yesButton = LoadButton(utilDlgProperty, "BtYes", btClickSound, btOverSound, device)
                                 ?? LoadButton(loginNoticeProperty, "BtYes", btClickSound, btOverSound, device);
            UIObject noButton = LoadButton(utilDlgProperty, "BtNo", btClickSound, btOverSound, device)
                                ?? LoadButton(loginNoticeProperty, "BtNo", btClickSound, btOverSound, device);
            UIObject acceptButton = LoadButton(loginNoticeProperty, "BtAccept", btClickSound, btOverSound, device)
                                    ?? okButton;
            UIObject nowButton = LoadButton(loginNoticeProperty, "BtNow", btClickSound, btOverSound, device);
            UIObject laterButton = LoadButton(loginNoticeProperty, "BtLater", btClickSound, btOverSound, device);
            UIObject restartButton = LoadButton(loginNoticeProperty, "BtRestart", btClickSound, btOverSound, device);
            UIObject exitButton = LoadButton(loginNoticeProperty, "BtExit", btClickSound, btOverSound, device);

            LoginUtilityDialogWindow window = new LoginUtilityDialogWindow(
                new DXObject(0, 0, frameTexture, 0),
                okButton,
                yesButton,
                noButton,
                acceptButton,
                nowButton,
                laterButton,
                restartButton,
                exitButton,
                noticeTextTextures)
            {
                Position = new Point(Math.Max(24, (screenWidth / 2) - (frameTexture.Width / 2)), Math.Max(24, (screenHeight / 2) - (frameTexture.Height / 2)))
            };

            manager.RegisterCustomWindow(window);
        }

        private static Dictionary<int, Texture2D> LoadIndexedCanvasTextures(WzSubProperty property, GraphicsDevice device)
        {
            Dictionary<int, Texture2D> textures = new();
            if (property == null)
            {
                return textures;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (!int.TryParse(child.Name, out int index))
                {
                    continue;
                }

                Texture2D texture = LoadCanvasTexture(property, child.Name, device);
                if (texture != null)
                {
                    textures[index] = texture;
                }
            }

            return textures;
        }

        public static void RegisterLoginEntryWindows(
            UIWindowManager manager,
            WzImage loginImage,
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

            RegisterLoginTitleWindow(manager, loginImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterChannelSelectionWindows(manager, uiWindow1Image, uiWindow2Image, soundUIImage, device, screenWidth, screenHeight);
            RegisterLoginRecommendWorldWindow(manager, loginImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterLoginCharacterSelectWindow(manager, basicImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterConnectionNoticeWindow(manager, loginImage, basicImage, soundUIImage, device, screenWidth, screenHeight);
            RegisterLoginUtilityDialogWindow(manager, uiWindow2Image, loginImage, basicImage, soundUIImage, device, screenWidth, screenHeight);
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

            WzImage loginImage = global::HaCreator.Program.FindImage("UI", "Login.img");
            WzSubProperty charSelectProperty = loginImage?["CharSelect"] as WzSubProperty;

            Texture2D panelTexture = LoadCanvasTexture(charSelectProperty, "charInfo", device)
                ?? CreatePlaceholderWindowTexture(device, 183, 115, "Character Detail");
            Texture2D panelTextureWithRank = LoadCanvasTexture(charSelectProperty, "charInfo1", device)
                ?? panelTexture;
            Texture2D frameTexture = CreateFilledTexture(device, panelTextureWithRank.Width, panelTextureWithRank.Height + 66, Color.Transparent, Color.Transparent);
            WzSubProperty iconProperty = charSelectProperty?["icon"] as WzSubProperty;
            Dictionary<int, Texture2D> jobBadgeTextures = new Dictionary<int, Texture2D>();
            WzSubProperty jobBadgeProperty = iconProperty?["job"] as WzSubProperty;
            for (int index = 0; index <= 4; index++)
            {
                Texture2D badgeTexture = LoadCanvasTexture(jobBadgeProperty, index.ToString(), device);
                if (badgeTexture != null)
                {
                    jobBadgeTextures[index] = badgeTexture;
                }
            }

            CharacterDetailWindow window = new CharacterDetailWindow(
                new DXObject(0, 0, frameTexture, 0),
                panelTexture,
                panelTextureWithRank,
                LoadCanvasTexture(iconProperty, "up", device),
                LoadCanvasTexture(iconProperty, "down", device),
                LoadCanvasTexture(iconProperty, "same", device),
                jobBadgeTextures)
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
                    closeBtn.X = Math.Max(0, frameTexture.Width - closeBtn.CanvasSnapshotWidth - 6);
                    closeBtn.Y = 4;
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
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang, string storageAccountLabel = null)
        {
            return CreateUIWindowManager(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage,
                null, null, null, device, screenWidth, screenHeight, isBigBang, 900, storageAccountLabel); // Default to GM book (900 in v115-style data)
        }

        /// <summary>
        /// Create and initialize a UIWindowManager with all windows and skill loading support
        /// </summary>
        public static UIWindowManager CreateUIWindowManager(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage basicImage, WzImage soundUIImage,
            WzFile skillWzFile, WzFile stringWzFile, WzImage mapleTvImage,
            GraphicsDevice device, int screenWidth, int screenHeight, bool isBigBang, int jobId = 900, string storageAccountLabel = null)
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
            SeedStarterConsumableInventory(manager.InventoryWindow as IInventoryRuntime);
            SimulatorStorageRuntime storageRuntime = new SimulatorStorageRuntime(initialAccountLabel: storageAccountLabel);

            MapTransferUI mapTransfer = CreateMapTransferWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            if (mapTransfer != null)
            {
                manager.RegisterCustomWindow(mapTransfer);
            }

            TrunkUI trunk = CreateTrunkWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight, manager.InventoryWindow as InventoryUI, storageRuntime);
            if (trunk != null)
            {
                manager.RegisterCustomWindow(trunk);
                if (storageRuntime.GetUsedSlotCount() == 0 &&
                    storageRuntime.GetMesoCount() <= 0 &&
                    storageRuntime.GetSlotLimit() == 24)
                {
                    SeedStarterTrunkInventory(storageRuntime);
                }
            }

            WorldMapUI worldMap = CreateWorldMapWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);
            if (worldMap != null)
            {
                manager.RegisterCustomWindow(worldMap);
            }

            RegisterProgressionUtilityPlaceholderWindows(manager, uiWindow1Image, uiWindow2Image, mapleTvImage, basicImage, soundUIImage, device, screenWidth, screenHeight, storageRuntime);
            RegisterSocialRoomWindows(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, screenWidth, screenHeight);

            if (characterInfo != null)
            {
                characterInfo.PartyRequested = () => manager.ShowWindow(MapSimulatorWindowNames.SocialList);
                characterInfo.MiniRoomRequested = () => manager.ShowWindow(MapSimulatorWindowNames.MiniRoom);
                characterInfo.PersonalShopRequested = () => manager.ShowWindow(MapSimulatorWindowNames.PersonalShop);
                characterInfo.EntrustedShopRequested = () => manager.ShowWindow(MapSimulatorWindowNames.EntrustedShop);
                characterInfo.TradingRoomRequested = () => manager.ShowWindow(MapSimulatorWindowNames.TradingRoom);
                characterInfo.FamilyRequested = () => manager.ShowWindow(MapSimulatorWindowNames.FamilyChart);
            }

            return manager;
        }

        private static void RegisterProgressionUtilityPlaceholderWindows(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage mapleTvImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight,
            IStorageRuntime storageRuntime)
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
                new Point(x + cascade, y + cascade),
                storageRuntime);
            RegisterAdminShopWindow(manager, uiWindow2Image, basicImage, soundUIImage, device,
                MapSimulatorWindowNames.Mts, AdminShopServiceMode.Mts,
                new Point(x + (cascade * 2), y + (cascade * 2)),
                storageRuntime);
            RegisterSocialListWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 2), y + (cascade * 5)));
            RegisterFamilyChartWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 3), y + (cascade * 5)));
            RegisterMessengerWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 3), y + (cascade * 3)));
            RegisterMapleTvWindow(manager, uiWindow1Image, mapleTvImage, basicImage, soundUIImage, device,
                new Point(x + (cascade * 4), y + (cascade * 2)));
            RegisterItemMakerWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 5), y + (cascade * 5)));
            RegisterItemUpgradeWindow(manager, uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 6), y + (cascade * 6)));
            RegisterVegaSpellWindow(manager, uiWindow1Image, basicImage, soundUIImage, device,
                new Point(x + (cascade * 7), y + (cascade * 6)));
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
            WzImage loginImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
        {
            const int ClientLoginLayoutWidth = 800;
            const int ClientLoginLayoutHeight = 600;
            const int RecommendWorldLeft = 302;
            const int RecommendWorldTop = 152;

            if (manager.GetWindow(MapSimulatorWindowNames.RecommendWorld) != null)
            {
                return;
            }

            WzSubProperty alertProperty = loginImage?["WorldSelect"]?["alert"] as WzSubProperty;
            if (alertProperty == null)
            {
                return;
            }

            Texture2D frameTexture = LoadCanvasTexture(alertProperty, "backgrd", device);
            if (frameTexture == null)
            {
                return;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;

            UIObject prevButton = LoadButton(alertProperty, "BtArrowL", clickSound, overSound, device);
            UIObject nextButton = LoadButton(alertProperty, "BtArrowR", clickSound, overSound, device);
            UIObject selectButton = LoadButton(alertProperty, "BtChoice", clickSound, overSound, device);
            UIObject closeButton = LoadButton(alertProperty, "BtClose", clickSound, overSound, device);

            Dictionary<int, Texture2D> worldNameTextures = new();
            WzSubProperty worldProperty = loginImage?["WorldSelect"]?["world"] as WzSubProperty;
            foreach (WzImageProperty property in worldProperty?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!int.TryParse(property.Name, out int worldId))
                {
                    continue;
                }

                Texture2D worldTexture = LoadCanvasTexture(worldProperty, property.Name, device);
                if (worldTexture != null)
                {
                    worldNameTextures[worldId] = worldTexture;
                }
            }

            if (prevButton != null)
            {
                prevButton.X = 34;
                prevButton.Y = 90;
            }

            if (nextButton != null)
            {
                nextButton.X = 135;
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
                worldNameTextures,
                prevButton,
                nextButton,
                selectButton,
                closeButton)
            {
                // Match CUIRecommendWorld::CUIRecommendWorld CreateDlg(302, 152, 200, 228)
                // against the client's 800x600 login layout, then center that layout in the viewport.
                Position = new Point(
                    Math.Max(24, ((screenWidth - ClientLoginLayoutWidth) / 2) + RecommendWorldLeft),
                    Math.Max(24, ((screenHeight - ClientLoginLayoutHeight) / 2) + RecommendWorldTop))
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
            Point position,
            IStorageRuntime storageRuntime)
        {
            if (manager.GetWindow(windowName) != null)
            {
                return;
            }

            UIWindowBase window = CreateAdminShopDialogWindow(
                uiWindow2Image,
                basicImage,
                soundUIImage,
                device,
                windowName,
                defaultMode,
                position);
            if (window is AdminShopDialogUI adminShop)
            {
                adminShop.SetStorageRuntime(storageRuntime);
            }

            manager.RegisterCustomWindow(window);
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

        private static void RegisterVegaSpellWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.VegaSpell) != null)
            {
                return;
            }

            UIWindowBase vegaSpellWindow = CreateVegaSpellWindow(uiWindow1Image, basicImage, soundUIImage, device, position);
            manager.RegisterCustomWindow(vegaSpellWindow);
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

            UIWindowBase socialSearchWindow = CreateSocialSearchWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 18, position.Y + 14));
            if (socialSearchWindow != null)
            {
                manager.RegisterCustomWindow(socialSearchWindow);
            }

            UIWindowBase guildSearchWindow = CreateGuildSearchWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 26, position.Y + 8));
            if (guildSearchWindow != null)
            {
                manager.RegisterCustomWindow(guildSearchWindow);
            }

            UIWindowBase guildSkillWindow = CreateGuildSkillWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 36, Math.Max(24, position.Y - 12)));
            if (guildSkillWindow != null)
            {
                manager.RegisterCustomWindow(guildSkillWindow);
            }
        }

        private static void RegisterFamilyChartWindow(
            UIWindowManager manager,
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager.GetWindow(MapSimulatorWindowNames.FamilyChart) != null)
            {
                return;
            }

            UIWindowBase familyChartWindow = CreateFamilyChartWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, position);
            if (familyChartWindow != null)
            {
                manager.RegisterCustomWindow(familyChartWindow);
            }

            UIWindowBase familyTreeWindow = CreateFamilyTreeWindow(uiWindow1Image, uiWindow2Image, basicImage, soundUIImage, device, new Point(position.X + 120, Math.Max(24, position.Y - 36)));
            if (familyTreeWindow != null)
            {
                manager.RegisterCustomWindow(familyTreeWindow);
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
            WzImage mapleTvImage,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            if (manager == null || manager.GetWindow(MapSimulatorWindowNames.MapleTv) != null)
            {
                return;
            }

            UIWindowBase mapleTvWindow = CreateMapleTvWindow(uiWindow1Image, mapleTvImage, basicImage, soundUIImage, device, position);
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
            window.BindButton(LoadButton(shopProperty, "BtVisit", clickSound, overSound, device), () => runtime.AddPersonalShopVisitor(null, out _));
            window.BindButton(LoadButton(shopProperty, "BtBlackList", clickSound, overSound, device), () => runtime.TogglePersonalShopBlacklist(null, out _));
            window.BindButton(LoadButton(shopProperty, "BtItem", clickSound, overSound, device), () => runtime.TryAutoListPersonalShopItem(out _));
            window.BindButton(LoadButton(shopProperty, "BtBuy", clickSound, overSound, device), () => runtime.TryBuyPersonalShopItem(-1, null, out _));
            window.BindButton(LoadButton(shopProperty, "BtClose", clickSound, overSound, device), () => runtime.ClosePersonalShop(out _));
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
            window.BindButton(LoadButton(entrustedShopProperty, "BtItem", clickSound, overSound, device), () => runtime.TryAutoListEntrustedShopItem(out _));
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
            window.BindButton(LoadButton(tradeProperty, "BtTrade", clickSound, overSound, device), () => runtime.ToggleTradeLock(out _));
            window.BindButton(LoadButton(tradeProperty, "BtReset", clickSound, overSound, device), runtime.ResetTrade);
            window.BindButton(LoadButton(tradeProperty, "BtCoin", clickSound, overSound, device), runtime.IncreaseTradeOffer);
            window.BindButton(LoadButton(tradeProperty, "BtClame", clickSound, overSound, device), () => runtime.ToggleTradeAcceptance(out _));
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
            WzSubProperty questInfoProperty = uiWindow2Image?["Quest"]?["quest_info"] as WzSubProperty
                ?? uiWindow1Image?["Quest"] as WzSubProperty;

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

            window.SetQuestChromeTextures(
                LoadCanvasTexture(questInfoProperty?["summary_icon"] as WzSubProperty, "select", device),
                LoadCanvasTexture(questInfoProperty?["Gauge"] as WzSubProperty, "frame", device),
                LoadCanvasTexture(questInfoProperty?["Gauge"] as WzSubProperty, "gauge", device),
                LoadCanvasTexture(questInfoProperty?["Gauge"] as WzSubProperty, "spot", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "normal", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "pressed", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "disabled", device),
                LoadButtonStateTexture(sourceProperty?["BtDelete"] as WzSubProperty, "mouseOver", device),
                LoadButtonAnimationTextures(sourceProperty?["BtQ"] as WzSubProperty, device));

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

        private static UIWindowBase CreateFamilyChartWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["Family"] as WzSubProperty
                ?? uiWindow1Image?["Family"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = sourceProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.FamilyChart,
                    "Family",
                    "Fallback owner for the family statistics and branch-management window.",
                    position);
            }

            Texture2D[] rightIcons = new Texture2D[5];
            WzSubProperty rightIconProperty = sourceProperty?["RightIcon"] as WzSubProperty;
            rightIcons[0] = LoadCanvasTexture(rightIconProperty, "2", device);
            rightIcons[1] = LoadCanvasTexture(rightIconProperty, "3", device);
            rightIcons[2] = LoadCanvasTexture(rightIconProperty, "2", device);
            rightIcons[3] = LoadCanvasTexture(rightIconProperty, "3", device);
            rightIcons[4] = LoadCanvasTexture(rightIconProperty, "4", device);

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            FamilyChartWindow window = new FamilyChartWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(sourceProperty, "backgrnd3", device, out Point headerOffset),
                headerOffset,
                rightIcons,
                device)
            {
                Position = position
            };

            window.InitializeButtons(
                LoadButton(sourceProperty, "BtTree", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtFamilyPrecept", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtJuniorEntry", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtLeft", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtRight", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtSpecial", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtOK", clickSound, overSound, device));

            return window;
        }

        private static UIWindowBase CreateFamilyTreeWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty sourceProperty = uiWindow2Image?["FamilyTree"] as WzSubProperty
                ?? uiWindow1Image?["FamilyTree"] as WzSubProperty;
            WzCanvasProperty backgroundProperty = sourceProperty?["backgrnd"] as WzCanvasProperty;
            Texture2D frameTexture = backgroundProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
            if (frameTexture == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.FamilyTree,
                    "Family Tree",
                    "Fallback owner for the dedicated family-tree layout window.",
                    position);
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            WzSubProperty leaderPlateProperty = sourceProperty?["PlateLeader"] as WzSubProperty;
            WzSubProperty memberPlateProperty = sourceProperty?["PlateOthers"] as WzSubProperty;
            FamilyTreeWindow window = new FamilyTreeWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(sourceProperty, "selected", device, out Point selectedOffset),
                selectedOffset,
                LoadWindowCanvasLayerWithOffset(leaderPlateProperty, "0", device, out Point _),
                LoadWindowCanvasLayerWithOffset(leaderPlateProperty, "1", device, out Point _),
                LoadWindowCanvasLayerWithOffset(memberPlateProperty, "0", device, out Point _),
                LoadWindowCanvasLayerWithOffset(memberPlateProperty, "1", device, out Point _),
                device)
            {
                Position = position
            };

            window.InitializeButtons(
                LoadButton(sourceProperty, "BtClose", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtJuniorEntry", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtBye", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtLeft", clickSound, overSound, device),
                LoadButton(sourceProperty, "BtRight", clickSound, overSound, device));
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
                ("Friend.AddGroup", "BtAddGroup"),
                ("Friend.Party", "BtParty"),
                ("Friend.Chat", "BtChat"),
                ("Friend.Whisper", "BtWhisper"),
                ("Friend.GroupWhisper", "BtGroupWhisper"),
                ("Friend.Mate", "BtMate"),
                ("Friend.Message", "BtMessage"),
                ("Friend.Mod", "BtMod"),
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
                ("Guild.GradeUp", "BtGradeUp"),
                ("Guild.GradeDown", "BtGradeDown"),
                ("Guild.Kick", "BtKick"),
                ("Guild.Where", "BtWhere"),
                ("Guild.Whisper", "BtWhisper"),
                ("Guild.Info", "BtInfo"),
                ("Guild.Skill", "BtSkill"),
                ("Guild.Search", "BtSearch"),
                ("Guild.Manage", "BtManage"),
                ("Guild.Change", "BtChange"));
            RegisterSocialListActionButtons(window, SocialListTab.Alliance, mainProperty?["Union"] as WzSubProperty, clickSound, overSound, device,
                ("Alliance.Invite", "BtInvite"),
                ("Alliance.Withdraw", "BtWithdraw"),
                ("Alliance.PartyInvite", "BtPartyInvite"),
                ("Alliance.GradeUp", "BtGradeUp"),
                ("Alliance.GradeDown", "BtGradeDown"),
                ("Alliance.Kick", "BtKick"),
                ("Alliance.Change", "BtChange"),
                ("Alliance.Chat", "BtChat"),
                ("Alliance.Whisper", "BtWhisper"),
                ("Alliance.Info", "BtInfo"),
                ("Alliance.Notice", "Btnotice"));
            RegisterSocialListActionButtons(window, SocialListTab.Blacklist, mainProperty?["BlackList"] as WzSubProperty, clickSound, overSound, device,
                ("Blacklist.Add", "BtAdd"),
                ("Blacklist.Delete", "BtDelete"));

            window.InitializeCloseButton(CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width));
            return window;
        }

        private static UIWindowBase CreateSocialSearchWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty searchProperty = userListProperty?["Search"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(searchProperty, "backgrd", device);
            if (frameTexture == null)
            {
                return null;
            }

            Texture2D[] enabledTabs = new Texture2D[3];
            Texture2D[] disabledTabs = new Texture2D[3];
            WzSubProperty enabledTabProperty = searchProperty?["Tab"]?["enabled"] as WzSubProperty;
            WzSubProperty disabledTabProperty = searchProperty?["Tab"]?["disabled"] as WzSubProperty;
            for (int i = 0; i < enabledTabs.Length; i++)
            {
                enabledTabs[i] = LoadCanvasTexture(enabledTabProperty, i.ToString(), device);
                disabledTabs[i] = LoadCanvasTexture(disabledTabProperty, i.ToString(), device);
            }

            SocialSearchWindow window = new SocialSearchWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(searchProperty, "backgrd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(searchProperty, "backgrd3", device, out Point contentOffset),
                contentOffset,
                enabledTabs,
                disabledTabs,
                device)
            {
                Position = position
            };

            RegisterSocialSearchContent(window, SocialSearchTab.Party, searchProperty?["Party"] as WzSubProperty, "base", device);
            RegisterSocialSearchContent(window, SocialSearchTab.PartyMember, searchProperty?["PartyMember"] as WzSubProperty, "table", device);
            RegisterSocialSearchContent(window, SocialSearchTab.Expedition, searchProperty?["Expedition"] as WzSubProperty, "base", device);

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            window.SetFilterButtons(
                LoadButton(searchProperty, "BtAllLevel", clickSound, overSound, device),
                LoadButton(searchProperty, "BtSimilarLevel", clickSound, overSound, device));
            RegisterSocialSearchButtons(window, SocialSearchTab.Party, searchProperty?["Party"] as WzSubProperty, clickSound, overSound, device,
                ("Search.Party.Request", "BtRequest"),
                ("Search.Party.PartyLeader", "BtPartyLeader"),
                ("Search.Party.PartyLevel", "BtPartyLevel"),
                ("Search.Party.Member", "BtMember"),
                ("Search.Party.Info", "PartyInfo"));
            RegisterSocialSearchButtons(window, SocialSearchTab.PartyMember, searchProperty?["PartyMember"] as WzSubProperty, clickSound, overSound, device,
                ("Search.PartyMember.Invite", "BtInvite"),
                ("Search.PartyMember.Name", "BtName"),
                ("Search.PartyMember.Job", "BtJob"),
                ("Search.PartyMember.Level", "BtLevel"));
            RegisterSocialSearchButtons(window, SocialSearchTab.Expedition, searchProperty?["Expedition"] as WzSubProperty, clickSound, overSound, device,
                ("Search.Expedition.Start", "BtStart"),
                ("Search.Expedition.Regist", "BtRegist"),
                ("Search.Expedition.Delete", "BtDelete"),
                ("Search.Expedition.QuickJoin", "BtQuickJoin"),
                ("Search.Expedition.Request", "BtRequest"),
                ("Search.Expedition.Whisper", "BtWhisper"),
                ("Search.Expedition.Front", "BtFront"),
                ("Search.Expedition.Regist2", "BtRegist2"),
                ("Search.Expedition.Cancel", "BtCancle"));

            UIObject closeButton = LoadButton(searchProperty, "BtClose", clickSound, overSound, device)
                ?? CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }

            return window;
        }

        private static UIWindowBase CreateGuildSearchWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty guildSearchProperty = userListProperty?["GuildSearch"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(guildSearchProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            GuildSearchWindow window = new GuildSearchWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(guildSearchProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(guildSearchProperty, "base2", device, out Point contentOffset),
                contentOffset,
                device)
            {
                Position = position
            };

            foreach ((string actionKey, string buttonName) in new[]
            {
                ("GuildSearch.Add", "BtAdd"),
                ("GuildSearch.Delete", "BtDelete"),
                ("GuildSearch.Join", "BtJoin"),
                ("GuildSearch.Whisper", "BtWhisper"),
                ("GuildSearch.Renew", "BtRenew"),
                ("GuildSearch.PagePrev", "BtPagePre"),
                ("GuildSearch.PageNext", "BtPageNext")
            })
            {
                UIObject button = LoadButton(guildSearchProperty, buttonName, clickSound, overSound, device);
                if (button != null)
                {
                    window.RegisterActionButton(actionKey, button);
                }
            }

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }

            return window;
        }

        private static UIWindowBase CreateGuildSkillWindow(
            WzImage uiWindow1Image,
            WzImage uiWindow2Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty userListProperty = uiWindow2Image?["UserList"] as WzSubProperty
                ?? uiWindow1Image?["UserList"] as WzSubProperty;
            WzSubProperty guildSkillProperty = userListProperty?["GuildSkill"] as WzSubProperty;
            Texture2D frameTexture = LoadCanvasTexture(guildSkillProperty, "backgrnd", device);
            if (frameTexture == null)
            {
                return null;
            }

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            GuildSkillWindow window = new GuildSkillWindow(
                new DXObject(0, 0, frameTexture, 0),
                LoadWindowCanvasLayerWithOffset(guildSkillProperty, "backgrnd2", device, out Point overlayOffset),
                overlayOffset,
                LoadWindowCanvasLayerWithOffset(guildSkillProperty, "base", device, out Point headerOffset),
                headerOffset,
                LoadCanvasTexture(guildSkillProperty, "skill0", device),
                LoadCanvasTexture(guildSkillProperty, "skill1", device),
                LoadCanvasTexture(guildSkillProperty?["recommend"] as WzSubProperty, "0", device),
                LoadButton(guildSkillProperty, "BtRenewal", clickSound, overSound, device),
                LoadButton(guildSkillProperty, "BtUp", clickSound, overSound, device),
                device)
            {
                Position = position
            };

            UIObject closeButton = CreateUserInfoCloseButton(basicImage, clickSound, overSound, device, frameTexture.Width);
            if (closeButton != null)
            {
                window.InitializeCloseButton(closeButton);
            }

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

        private static void RegisterSocialSearchContent(
            SocialSearchWindow window,
            SocialSearchTab tab,
            WzSubProperty sourceProperty,
            string canvasName,
            GraphicsDevice device)
        {
            if (window == null || sourceProperty == null)
            {
                return;
            }

            IDXObject layer = LoadWindowCanvasLayerWithOffset(sourceProperty, canvasName, device, out Point offset);
            if (layer != null)
            {
                window.RegisterContentLayer(tab, layer, offset);
            }
        }

        private static void RegisterSocialSearchButtons(
            SocialSearchWindow window,
            SocialSearchTab tab,
            WzSubProperty sourceProperty,
            WzBinaryProperty clickSound,
            WzBinaryProperty overSound,
            GraphicsDevice device,
            params (string ActionKey, string ButtonName)[] mappings)
        {
            if (window == null || sourceProperty == null || mappings == null)
            {
                return;
            }

            foreach ((string actionKey, string buttonName) in mappings)
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
            WzSubProperty collapsedProperty = sourceProperty["Min2"] as WzSubProperty
                ?? minimizedProperty;
            WzSubProperty nameBarProperty = sourceProperty["Name"] as WzSubProperty
                ?? sourceProperty["NameBar"] as WzSubProperty;

            Texture2D maxFrameTexture = LoadCanvasTexture(maximizedProperty, "backgrnd", device);
            Texture2D minFrameTexture = LoadCanvasTexture(minimizedProperty, "backgrnd", device) ?? maxFrameTexture;
            Texture2D collapsedFrameTexture = LoadCanvasTexture(collapsedProperty, "backgrnd", device) ?? minFrameTexture;
            if (maxFrameTexture == null || minFrameTexture == null || collapsedFrameTexture == null)
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
            IDXObject collapsedFrame = new DXObject(0, 0, collapsedFrameTexture, 0);
            IDXObject maxOverlay = LoadWindowCanvasLayerWithOffset(maximizedProperty, "backgrnd2", device, out Point maxOverlayOffset);
            IDXObject maxContent = LoadWindowCanvasLayerWithOffset(maximizedProperty, "backgrnd3", device, out Point maxContentOffset);
            IDXObject minOverlay = LoadWindowCanvasLayerWithOffset(minimizedProperty, "backgrnd2", device, out Point minOverlayOffset);
            IDXObject minContent = LoadWindowCanvasLayerWithOffset(minimizedProperty, "backgrnd3", device, out Point minContentOffset);
            IDXObject collapsedOverlay = LoadWindowCanvasLayerWithOffset(collapsedProperty, "backgrnd2", device, out Point collapsedOverlayOffset);
            IDXObject collapsedContent = LoadWindowCanvasLayerWithOffset(collapsedProperty, "backgrnd3", device, out Point collapsedContentOffset);

            Texture2D[] nameBars = new Texture2D[3];
            for (int i = 0; i < nameBars.Length; i++)
            {
                nameBars[i] = LoadCanvasTexture(nameBarProperty, i.ToString(), device);
            }

            Texture2D maxStatusIcon = LoadCanvasTexture(maximizedProperty, "icon", device);
            Texture2D minStatusIcon = LoadCanvasTexture(minimizedProperty, "icon", device) ?? maxStatusIcon;
            Texture2D collapsedStatusIcon = LoadCanvasTexture(collapsedProperty, "icon", device) ?? minStatusIcon;
            Point maxStatusIconPosition = GetCanvasOffset(maximizedProperty?["icon"] as WzCanvasProperty);
            Point minStatusIconPosition = GetCanvasOffset(minimizedProperty?["icon"] as WzCanvasProperty);
            Point collapsedStatusIconPosition = GetCanvasOffset(collapsedProperty?["icon"] as WzCanvasProperty);

            Texture2D[] chatBalloonFrames = Array.Empty<Texture2D>();
            if (sourceProperty["chatBalloon"] is WzSubProperty chatBalloonProperty)
            {
                var frames = new List<Texture2D>();
                for (int i = 0; i < 8; i++)
                {
                    Texture2D frame = LoadCanvasTexture(chatBalloonProperty, i.ToString(), device);
                    if (frame == null)
                    {
                        break;
                    }

                    frames.Add(frame);
                }

                chatBalloonFrames = frames.ToArray();
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
            UIObject minEnterLayout = LoadButton(minimizedProperty, "BtEnter", btClickSound, btOverSound, device) ?? enterButton;
            UIObject minClaimLayout = LoadButton(minimizedProperty, "BtClame", btClickSound, btOverSound, device) ?? claimButton;
            UIObject collapsedMaxLayout = LoadButton(collapsedProperty, "BtMax", btClickSound, btOverSound, device) ?? maximizeButton;
            UIObject minMinLayout = LoadButton(minimizedProperty, "BtMin", btClickSound, btOverSound, device) ?? minimizeButton;
            UIObject collapsedMinLayout = LoadButton(collapsedProperty, "BtMin", btClickSound, btOverSound, device) ?? minimizeButton;

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
                collapsedFrame,
                collapsedOverlay,
                collapsedOverlayOffset,
                collapsedContent,
                collapsedContentOffset,
                nameBars,
                chatBalloonFrames,
                maxStatusIcon,
                maxStatusIconPosition,
                minStatusIcon,
                minStatusIconPosition,
                collapsedStatusIcon,
                collapsedStatusIconPosition,
                new Point(enterButton?.X ?? 0, enterButton?.Y ?? 0),
                new Point(minEnterLayout?.X ?? enterButton?.X ?? 0, minEnterLayout?.Y ?? enterButton?.Y ?? 0),
                new Point(claimButton?.X ?? 0, claimButton?.Y ?? 0),
                new Point(minClaimLayout?.X ?? claimButton?.X ?? 0, minClaimLayout?.Y ?? claimButton?.Y ?? 0),
                new Point(maximizeButton?.X ?? 0, maximizeButton?.Y ?? 0),
                new Point(collapsedMaxLayout?.X ?? maximizeButton?.X ?? 0, collapsedMaxLayout?.Y ?? maximizeButton?.Y ?? 0),
                new Point(minimizeButton?.X ?? 0, minimizeButton?.Y ?? 0),
                new Point(minMinLayout?.X ?? minimizeButton?.X ?? 0, minMinLayout?.Y ?? minimizeButton?.Y ?? 0),
                new Point(collapsedMinLayout?.X ?? minimizeButton?.X ?? 0, collapsedMinLayout?.Y ?? minimizeButton?.Y ?? 0),
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
            WzImage mapleTvImage,
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
                receiverOverlayOffset,
                LoadMapleTvVisualAssets(mapleTvImage, device))
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

        private static MapleTvVisualAssets LoadMapleTvVisualAssets(WzImage mapleTvImage, GraphicsDevice device)
        {
            if (mapleTvImage == null || device == null)
            {
                return null;
            }

            WzSubProperty mediaRoot = mapleTvImage["TVmedia"] as WzSubProperty;
            Dictionary<int, IReadOnlyList<MapleTvAnimationFrame>> mediaFrames = new();
            if (mediaRoot != null)
            {
                foreach (WzImageProperty child in mediaRoot.WzProperties)
                {
                    if (!int.TryParse(child.Name, out int mediaIndex) || child is not WzSubProperty mediaProperty)
                    {
                        continue;
                    }

                    IReadOnlyList<MapleTvAnimationFrame> frames = LoadMapleTvAnimationFrames(mediaProperty, device);
                    if (frames.Count > 0)
                    {
                        mediaFrames[mediaIndex] = frames;
                    }
                }
            }

            return new MapleTvVisualAssets(
                LoadMapleTvAnimationFrames(mapleTvImage["TVbasic"] as WzSubProperty, device),
                LoadMapleTvAnimationFrames(mapleTvImage["TVoff"] as WzSubProperty, device),
                mediaFrames,
                ResolveDefaultMapleTvMediaIndex(mediaFrames));
        }

        private static IReadOnlyList<MapleTvAnimationFrame> LoadMapleTvAnimationFrames(WzSubProperty animationProperty, GraphicsDevice device)
        {
            if (animationProperty == null)
            {
                return Array.Empty<MapleTvAnimationFrame>();
            }

            List<MapleTvAnimationFrame> frames = new();
            foreach (WzCanvasProperty canvas in animationProperty.WzProperties
                         .OfType<WzCanvasProperty>()
                         .OrderBy(p => int.TryParse(p.Name, out int frameIndex) ? frameIndex : int.MaxValue)
                         .ThenBy(p => p.Name, StringComparer.Ordinal))
            {
                Texture2D texture = canvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
                if (texture == null)
                {
                    continue;
                }

                System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
                int delayMs = canvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 120;
                frames.Add(new MapleTvAnimationFrame(
                    new DXObject(0, 0, texture, 0),
                    new Point(-(int)origin.X, -(int)origin.Y),
                    delayMs));
            }

            return frames;
        }

        private static int ResolveDefaultMapleTvMediaIndex(IReadOnlyDictionary<int, IReadOnlyList<MapleTvAnimationFrame>> mediaFrames)
        {
            if (mediaFrames == null || mediaFrames.Count == 0)
            {
                return 1;
            }

            // WZ ships branch 1 as the neutral, non-episode splash, so prefer it when present.
            if (mediaFrames.ContainsKey(1))
            {
                return 1;
            }

            return mediaFrames.Keys.OrderBy(index => index).First();
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
                LoadCanvasTexture(sourceProperty["Emoticon"] as WzSubProperty, "Select", device),
                LoadGuildBbsEmoticonSet(sourceProperty["Emoticon"]?["Basic"] as WzSubProperty, 3, device),
                LoadGuildBbsEmoticonSet(sourceProperty["Emoticon"]?["Cash"] as WzSubProperty, 7, device),
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
                LoadButton(sourceProperty, "BtReplyDelete", btClickSound, btOverSound, device),
                LoadButton(sourceProperty["MoveEmoticon"] as WzSubProperty, "BtLeft", btClickSound, btOverSound, device),
                LoadButton(sourceProperty["MoveEmoticon"] as WzSubProperty, "BtRight", btClickSound, btOverSound, device));

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

        private static UIWindowBase CreateVegaSpellWindow(
            WzImage uiWindow1Image,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            Point position)
        {
            WzSubProperty vegaSpellProperty = uiWindow1Image?["VegaSpell"] as WzSubProperty;
            WzCanvasProperty background10 = vegaSpellProperty?["backgrnd10"] as WzCanvasProperty;
            if (vegaSpellProperty == null || background10 == null)
            {
                return CreatePlaceholderUtilityWindow(
                    basicImage,
                    soundUIImage,
                    device,
                    MapSimulatorWindowNames.VegaSpell,
                    "Vega Spell",
                    "Fallback owner for the dedicated Vega enhancement flow.",
                    position);
            }

            Texture2D frame10Texture = background10.GetLinkedWzCanvasBitmap().ToTexture2DAndDispose(device);
            IDXObject frame10 = new DXObject(0, 0, frame10Texture, 0);
            Texture2D frame60Texture = LoadCanvasTexture(vegaSpellProperty, "backgrnd60", device) ?? frame10Texture;
            IDXObject frame60 = new DXObject(0, 0, frame60Texture, 0);
            VegaSpellUI window = new VegaSpellUI(frame10, device)
            {
                Position = position
            };
            window.SetFrames(frame10, frame60);
            window.SetResultTextures(
                LoadCanvasTexture(vegaSpellProperty, "SuccessWnd", device),
                LoadCanvasTexture(vegaSpellProperty, "FailWnd", device));
            window.SetDigitTextures(LoadDigitTextures(vegaSpellProperty["Count"] as WzSubProperty, device));
            window.SetEffectFrames(
                LoadAnimationFrames(vegaSpellProperty["EffectTwinkling"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectSpelling"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectArrow"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectSuccess"] as WzSubProperty, device),
                LoadAnimationFrames(vegaSpellProperty["EffectFail"] as WzSubProperty, device));

            WzBinaryProperty btClickSound = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
            WzBinaryProperty btOverSound = (WzBinaryProperty)soundUIImage?["BtMouseOver"];
            WzSubProperty basicUp = basicImage?["BtUP"] as WzSubProperty;
            WzSubProperty basicDown = basicImage?["BtDown"] as WzSubProperty;
            UIObject prevButton = basicUp != null ? new UIObject(basicUp, btClickSound, btOverSound, false, Point.Zero, device) : null;
            UIObject nextButton = basicDown != null ? new UIObject(basicDown, btClickSound, btOverSound, false, Point.Zero, device) : null;
            window.InitializeButtons(
                LoadButton(vegaSpellProperty, "BtStart", btClickSound, btOverSound, device),
                LoadButton(vegaSpellProperty, "BtOK", btClickSound, btOverSound, device),
                LoadButton(vegaSpellProperty, "BtCancel", btClickSound, btOverSound, device),
                prevButton,
                nextButton);

            return window;
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
            WzSubProperty tabBuyProperty = shopProperty["TabBuy"] as WzSubProperty;
            WzSubProperty tabBuyEnabledProperty = tabBuyProperty?["enabled"] as WzSubProperty;
            WzSubProperty tabBuyDisabledProperty = tabBuyProperty?["disabled"] as WzSubProperty;
            WzSubProperty tabSellProperty = shopProperty["TabSell"] as WzSubProperty;
            WzSubProperty tabSellEnabledProperty = tabSellProperty?["enabled"] as WzSubProperty;
            WzSubProperty tabSellDisabledProperty = tabSellProperty?["disabled"] as WzSubProperty;
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
            Texture2D[] browseEnabledTextures = new Texture2D[5];
            Texture2D[] browseDisabledTextures = new Texture2D[5];
            Point[] browseOffsets = new Point[5];
            for (int i = 0; i < browseEnabledTextures.Length; i++)
            {
                string tabKey = i.ToString();
                browseEnabledTextures[i] = LoadCanvasTexture(tabBuyEnabledProperty, tabKey, device);
                browseDisabledTextures[i] = LoadCanvasTexture(tabBuyDisabledProperty, tabKey, device);
                browseOffsets[i] = ResolveCanvasOffset(tabBuyEnabledProperty?[tabKey] as WzCanvasProperty);
            }

            Texture2D[] quickCategoryEnabledTextures = new Texture2D[5];
            Texture2D[] quickCategoryDisabledTextures = new Texture2D[5];
            Point[] quickCategoryOffsets = new Point[5];
            for (int i = 0; i < quickCategoryEnabledTextures.Length; i++)
            {
                string tabKey = i.ToString();
                quickCategoryEnabledTextures[i] = LoadCanvasTexture(tabSellEnabledProperty, tabKey, device);
                quickCategoryDisabledTextures[i] = LoadCanvasTexture(tabSellDisabledProperty, tabKey, device);
                quickCategoryOffsets[i] = ResolveCanvasOffset(tabSellEnabledProperty?[tabKey] as WzCanvasProperty);
            }

            Point[] categoryOffsets = new Point[10];
            for (int i = 0; i < categoryOffsets.Length; i++)
            {
                categoryOffsets[i] = ResolveCanvasOffset(tabShopEnabledProperty?[i.ToString()] as WzCanvasProperty);
            }

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

            window.SetBrowseTabTextures(browseEnabledTextures, browseDisabledTextures, browseOffsets);
            window.SetQuickCategoryTabTextures(quickCategoryEnabledTextures, quickCategoryDisabledTextures, quickCategoryOffsets);
            window.SetCategoryTabTextures(categoryEnabledTextures, categoryDisabledTextures, categoryOffsets);

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

        private static void SeedStarterConsumableInventory(IInventoryRuntime inventory)
        {
            if (inventory == null)
            {
                return;
            }

            if (inventory.GetItemCount(InventoryType.USE, 2050004) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2050004, null, 3); // All Cure Potion
            }

            if (inventory.GetItemCount(InventoryType.USE, 2022179) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2022179, null, 2); // Onyx Apple
            }
        }

        private static void SeedStarterTrunkInventory(IStorageRuntime storageRuntime)
        {
            if (storageRuntime == null)
            {
                return;
            }

            storageRuntime.SetMeso(1250000);
            storageRuntime.AddItem(InventoryType.EQUIP, new InventorySlotData { ItemId = 1302000, GradeFrameIndex = 0 });
            storageRuntime.AddItem(InventoryType.USE, new InventorySlotData { ItemId = 2000005, Quantity = 30 });
            storageRuntime.AddItem(InventoryType.SETUP, new InventorySlotData { ItemId = 3010002, Quantity = 1 });
            storageRuntime.AddItem(InventoryType.ETC, new InventorySlotData { ItemId = 4000019, Quantity = 120 });
            storageRuntime.AddItem(InventoryType.CASH, new InventorySlotData { ItemId = 5150040, Quantity = 1 });
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

            if (inventory.GetItemCount(InventoryType.USE, 2049402) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049402, null, 1); // Special Potential Scroll (legacy)
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049407) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049407, null, 1); // Advanced Potential Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049408) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049408, null, 1); // Potential Scroll
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049500) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049500, null, 1); // Carved Golden Seal
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049501) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049501, null, 1); // Carved Silver Seal
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049700) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049700, null, 1); // Epic Potential Scroll 100%
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049701) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049701, null, 1); // Epic Potential Scroll 80%
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049702) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049702, null, 1); // Epic Potential Scroll 100%
            }

            if (inventory.GetItemCount(InventoryType.USE, 2049703) <= 0)
            {
                inventory.AddItem(InventoryType.USE, 2049703, null, 1); // Epic Potential Scroll 100%
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5062000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062000, null, 2); // Miracle Cube
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5062001) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062001, null, 1); // Premium Miracle Cube
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5062002) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062002, null, 1); // Super Miracle Cube
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5062003) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062003, null, 1); // Revolutionary Miracle Cube
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5062004) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062004, null, 1); // Golden Miracle Cube
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5062005) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5062005, null, 1); // Enlightening Miracle Cube
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5534000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5534000, null, 1); // Urete's Time Lab
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5610000) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5610000, null, 1); // Vega's Spell(10%)
            }

            if (inventory.GetItemCount(InventoryType.CASH, 5610001) <= 0)
            {
                inventory.AddItem(InventoryType.CASH, 5610001, null, 1); // Vega's Spell(60%)
            }
        }

        private static void SeedStarterCompanionInventory(IInventoryRuntime inventory, GraphicsDevice device)
        {
            if (inventory == null)
            {
                return;
            }

            int[] starterEquipIds =
            {
                1802000, 1802001, 1802002,
                1942001, 1952001, 1962001, 1972001,
                1612001, 1622001, 1632001, 1642001, 1652001,
                1002140, 1010000, 1040000, 1050000, 1062007, 1072005, 1080000, 1100000
            };

            for (int i = 0; i < starterEquipIds.Length; i++)
            {
                int itemId = starterEquipIds[i];
                if (inventory.GetItemCount(InventoryType.EQUIP, itemId) > 0)
                {
                    continue;
                }

                inventory.AddItem(InventoryType.EQUIP, new InventorySlotData
                {
                    ItemId = itemId,
                    ItemTexture = LoadSeedInventoryItemIcon(itemId, device),
                    Quantity = 1,
                    MaxStackSize = 1,
                    GradeFrameIndex = 0
                });
            }
        }

        private static Texture2D LoadSeedInventoryItemIcon(int itemId, GraphicsDevice device)
        {
            if (device == null || !InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemText = string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase)
                ? itemId.ToString("D8")
                : itemId.ToString("D7");
            WzSubProperty infoProperty = (itemImage[itemText] as WzSubProperty)?["info"] as WzSubProperty;
            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty
                                          ?? infoProperty?["icon"] as WzCanvasProperty;
            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(device);
        }
        #endregion
    }
}
