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
                inventory.SetTooltipOrigins(new[]
                {
                    ResolveTooltipOrigin(skillMainProperty["tip0"] as WzCanvasProperty),
                    ResolveTooltipOrigin(skillMainProperty["tip1"] as WzCanvasProperty),
                    ResolveTooltipOrigin(skillMainProperty["tip2"] as WzCanvasProperty)
                });
            }

            ApplyInventoryEquipTooltipAssets(inventory, uiWindowImage, device);


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
                inventory.SetTooltipOrigins(new[]
                {
                    ResolveTooltipOrigin(skillMainProperty["tip0"] as WzCanvasProperty),
                    ResolveTooltipOrigin(skillMainProperty["tip1"] as WzCanvasProperty),
                    ResolveTooltipOrigin(skillMainProperty["tip2"] as WzCanvasProperty)
                });
            }

            ApplyInventoryEquipTooltipAssets(inventory, uiWindow2Image, device);


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

        private static void ApplyInventoryEquipTooltipAssets(InventoryUI inventory, WzImage uiWindowImage, GraphicsDevice device)
        {
            WzSubProperty equipTooltipProperty = uiWindowImage?["ToolTip"]?["Equip"] as WzSubProperty;
            if (equipTooltipProperty == null)
            {
                return;
            }

            inventory.SetEquipTooltipAssets(new EquipUIBigBang.EquipTooltipAssets
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
        #endregion
    }

}

