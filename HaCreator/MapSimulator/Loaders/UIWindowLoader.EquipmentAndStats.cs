using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
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
    public static partial class UIWindowLoader
    {
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


            // only authors dynamic character-pane chrome for pendant slot expansion
            // and the charm pocket unlock. Load them from WZ instead of duplicating layout constants.
            TryLoadSpecialSlotChrome(characterProperty, "cashPendant", device, equip);
            TryLoadSpecialSlotChrome(characterProperty, "charmPocket", device, equip);

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
                    SpeedLabels = LoadCanvasTextureMap(equipTooltipProperty["Speed"] as WzSubProperty, device),
                    GrowthEnabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthEnabled"] as WzSubProperty, device),
                    GrowthDisabledLabels = LoadCanvasTextureMap(equipTooltipProperty["GrowthDisabled"] as WzSubProperty, device),
                    CashLabel = LoadCanvasTexture(equipTooltipProperty, "cash", device),
                    MesosLabel = LoadCanvasTexture(equipTooltipProperty, "mesos", device),
                    StarLabel = LoadCanvasTexture(equipTooltipProperty["Star"] as WzSubProperty, "Star", device)
                });
            }


            return equip;

        }



        private static void TryLoadSpecialSlotChrome(WzSubProperty characterProperty, string propertyName, GraphicsDevice device, EquipUIBigBang equip)
        {
            WzCanvasProperty chromeProperty = characterProperty?[propertyName] as WzCanvasProperty;
            if (chromeProperty == null)
            {
                return;
            }


            try
            {
                System.Drawing.Bitmap chromeBitmap = chromeProperty.GetLinkedWzCanvasBitmap();
                Texture2D chromeTexture = chromeBitmap.ToTexture2DAndDispose(device);
                IDXObject chrome = new DXObject(0, 0, chromeTexture, 0);
                System.Drawing.PointF? origin = chromeProperty.GetCanvasOriginPosition();
                int offsetX = origin.HasValue ? -(int)origin.Value.X : 0;
                int offsetY = origin.HasValue ? -(int)origin.Value.Y : 0;
                equip.SetSpecialSlotChrome(propertyName, chrome, offsetX, offsetY);
            }
            catch
            {
            }
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


        private static Point ResolveTooltipOrigin(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return Point.Zero;
            }

            System.Drawing.PointF? origin = canvas.GetCanvasOriginPosition();
            if (!origin.HasValue)
            {
                return Point.Zero;
            }

            return new Point((int)origin.Value.X, (int)origin.Value.Y);
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

            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img");
            WzSubProperty postBigBangSkillMain = uiWindow2Image?["Skill"]?["main"] as WzSubProperty;
            if (postBigBangSkillMain != null)
            {
                Texture2D[] tooltipFrames =
                {
                    LoadCanvasTexture(postBigBangSkillMain, "tip0", device),
                    LoadCanvasTexture(postBigBangSkillMain, "tip1", device),
                    LoadCanvasTexture(postBigBangSkillMain, "tip2", device)
                };
                skill.SetTooltipTextures(tooltipFrames);
                Point[] tooltipOrigins =
                {
                    ResolveTooltipOrigin(postBigBangSkillMain["tip0"] as WzCanvasProperty),
                    ResolveTooltipOrigin(postBigBangSkillMain["tip1"] as WzCanvasProperty),
                    ResolveTooltipOrigin(postBigBangSkillMain["tip2"] as WzCanvasProperty)
                };
                skill.SetTooltipOrigins(tooltipOrigins);
            }

            skill.SetCooldownMasks(UIWindowLoader.LoadCooldownMasks(skillProperty["CoolTime"] as WzSubProperty, device));


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
    }
}
