using HaCreator.MapEditor;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HaCreator.MapSimulator.UI.Controls;

namespace HaCreator.MapSimulator.Loaders
{
    /// <summary>
    /// Handles loading of UI elements for MapSimulator.
    /// Extracted from MapSimulatorLoader for better code organization.
    /// </summary>
    public static class UILoader
    {
        // Constants
        private const string GLOBAL_FONT = "Arial";
        private const float MINIMAP_STREETNAME_TOOLTIP_FONTSIZE = 10f;

        #region StatusBar
        /// <summary>
        /// Draws the status bar UI (Character health, level, name)
        /// </summary>
        /// <param name="uiStatusBar">UI.wz/StatusBar.img</param>
        /// <param name="uiStatusBar2">UI.wz/StatusBar2.img</param>
        /// <param name="mapBoard"></param>
        /// <param name="device"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="renderParams"></param>
        /// <param name="soundUIImage"></param>
        /// <param name="bBigBang"></param>
        /// <returns></returns>
        public static Tuple<StatusBarUI, StatusBarChatUI> CreateStatusBarFromProperty(
            WzImage uiStatusBar, WzImage uiStatusBar2, WzImage uiBasic, WzImage uiBuffIcon, Board mapBoard, GraphicsDevice device,
            float UserScreenScaleFactor, RenderParameters renderParams, WzImage soundUIImage, bool bBigBang)
        {
            // Pre-big bang maplestory status bar
            if (bBigBang)
            {
                WzSubProperty mainBarProperties = (uiStatusBar2?["mainBar"] as WzSubProperty);
                if (mainBarProperties != null)
                {
                    HaUIGrid grid = new HaUIGrid(1, 1);

                    System.Drawing.Bitmap backgrnd = ((WzCanvasProperty)mainBarProperties?["backgrnd"])?.GetLinkedWzCanvasBitmap();

                    grid.AddRenderable(0, 0, new HaUIImage(new HaUIInfo()
                    {
                        Bitmap = backgrnd,
                        VerticalAlignment = HaUIAlignment.Start,
                        HorizontalAlignment = HaUIAlignment.Start
                    }));

                    const int UI_PADDING_PX = 2;

                    // Draw level, name, job area
                    HaUIStackPanel stackPanel_charStats = new HaUIStackPanel(HaUIStackOrientation.Horizontal, new HaUIInfo()
                    {
                        VerticalAlignment = HaUIAlignment.End
                    });

                    System.Drawing.Bitmap bitmap_lvBacktrnd = ((WzCanvasProperty)mainBarProperties?["lvBacktrnd"])?.GetLinkedWzCanvasBitmap();
                    System.Drawing.Bitmap bitmap_lvCover = ((WzCanvasProperty)mainBarProperties?["lvCover"])?.GetLinkedWzCanvasBitmap();

                    HaUIGrid grid_charInfo = new HaUIGrid(1, 1);
                    if (bitmap_lvBacktrnd != null)
                    {
                        grid_charInfo.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() { Bitmap = bitmap_lvBacktrnd }));
                    }
                    if (bitmap_lvCover != null)
                    {
                        grid_charInfo.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() { Bitmap = bitmap_lvCover }));
                    }

                    stackPanel_charStats.AddRenderable(grid_charInfo);

                    // Draw HP, MP, EXP area
                    System.Drawing.Bitmap bitmap_gaugeBackgrd = ((WzCanvasProperty)mainBarProperties?["gaugeBackgrd"])?.GetLinkedWzCanvasBitmap();
                    System.Drawing.Bitmap bitmap_gaugeCover = ((WzCanvasProperty)mainBarProperties?["gaugeCover"])?.GetLinkedWzCanvasBitmap();

                    HaUIGrid grid_hpMpExp = new HaUIGrid(1, 1);
                    grid_hpMpExp.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() { Bitmap = bitmap_gaugeCover }));
                    grid_hpMpExp.AddRenderable(0, 0, new HaUIImage(new HaUIInfo() { Bitmap = bitmap_gaugeBackgrd }));

                    // Load gauge fill images for HP, MP, EXP bars
                    // WZ structure: mainBar/gauge/hp/0, mainBar/gauge/mp/0, mainBar/gauge/exp/0
                    // Each gauge has frames 0, 1, 2 - we use frame 0 for the fill image
                    WzSubProperty gaugeProperty = mainBarProperties?["gauge"] as WzSubProperty;
                    Texture2D hpGaugeTexture = null, mpGaugeTexture = null, expGaugeTexture = null;

                    if (gaugeProperty != null)
                    {
                        // HP gauge: gauge/hp/0
                        WzSubProperty hpGaugeProp = gaugeProperty["hp"] as WzSubProperty;
                        if (hpGaugeProp != null)
                        {
                            WzCanvasProperty hpCanvas = hpGaugeProp["0"] as WzCanvasProperty;
                            if (hpCanvas != null)
                            {
                                var hpBitmap = hpCanvas.GetLinkedWzCanvasBitmap();
                                if (hpBitmap != null)
                                {
                                    hpGaugeTexture = hpBitmap.ToTexture2DAndDispose(device);
                                }
                            }
                        }

                        // MP gauge: gauge/mp/0
                        WzSubProperty mpGaugeProp = gaugeProperty["mp"] as WzSubProperty;
                        if (mpGaugeProp != null)
                        {
                            WzCanvasProperty mpCanvas = mpGaugeProp["0"] as WzCanvasProperty;
                            if (mpCanvas != null)
                            {
                                var mpBitmap = mpCanvas.GetLinkedWzCanvasBitmap();
                                if (mpBitmap != null)
                                {
                                    mpGaugeTexture = mpBitmap.ToTexture2DAndDispose(device);
                                }
                            }
                        }

                        // EXP gauge: gauge/exp/0
                        WzSubProperty expGaugeProp = gaugeProperty["exp"] as WzSubProperty;
                        if (expGaugeProp != null)
                        {
                            WzCanvasProperty expCanvas = expGaugeProp["0"] as WzCanvasProperty;
                            if (expCanvas != null)
                            {
                                var expBitmap = expCanvas.GetLinkedWzCanvasBitmap();
                                if (expBitmap != null)
                                {
                                    expGaugeTexture = expBitmap.ToTexture2DAndDispose(device);
                                }
                            }
                        }
                    }

                    // add HP, MP, EXP area to the [level, name, job area stackpanel]
                    stackPanel_charStats.AddRenderable(grid_hpMpExp);

                    // Cash shop, MTS, menu, system, channel UI
                    WzBinaryProperty binaryProp_BtMouseClickSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseClick"];
                    WzBinaryProperty binaryProp_BtMouseOverSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseOver"];

                    WzSubProperty subProperty_BtCashShop = (WzSubProperty)mainBarProperties?["BtCashShop"]; // cash shop
                    UIObject obj_Ui_BtCashShop = new UIObject(subProperty_BtCashShop, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                        X = 9 + bitmap_lvBacktrnd.Width + bitmap_gaugeBackgrd.Width + UI_PADDING_PX,
                    };
                    obj_Ui_BtCashShop.Y += backgrnd.Height;

                    WzSubProperty subProperty_BtMTS = (WzSubProperty)mainBarProperties?["BtMTS"]; // MTS
                    if (subProperty_BtMTS == null)
                        subProperty_BtMTS = (WzSubProperty)mainBarProperties?["BtNPT"]; // MapleStory Japan uses a different name
                    UIObject obj_Ui_BtMTS = null;
                    if (subProperty_BtMTS != null)
                    {
                        obj_Ui_BtMTS = new UIObject(subProperty_BtMTS, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                            false,
                            new Point(0, 0), device)
                        {
                        };
                        obj_Ui_BtMTS.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                        obj_Ui_BtMTS.Y += backgrnd.Height;
                    }
                    WzSubProperty subProperty_BtMenu = (WzSubProperty)mainBarProperties?["BtMenu"]; // Menu
                    UIObject obj_Ui_BtMenu = new UIObject(subProperty_BtMenu, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtMenu.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                    obj_Ui_BtMenu.Y += backgrnd.Height;

                    WzSubProperty subProperty_BtSystem = (WzSubProperty)mainBarProperties?["BtSystem"]; // System
                    UIObject obj_Ui_BtSystem = new UIObject(subProperty_BtSystem, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtSystem.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                    obj_Ui_BtSystem.Y += backgrnd.Height;

                    WzSubProperty subProperty_BtChannel = (WzSubProperty)mainBarProperties?["BtChannel"]; // System
                    UIObject obj_Ui_BtChannel = new UIObject(subProperty_BtChannel, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtChannel.X += obj_Ui_BtCashShop.X - obj_Ui_BtCashShop.CanvasSnapshotWidth;
                    obj_Ui_BtChannel.Y += backgrnd.Height;


                    // Draw Chat UI
                    System.Drawing.Bitmap bitmap_chatSpace = ((WzCanvasProperty)mainBarProperties?["chatSpace"])?.GetLinkedWzCanvasBitmap(); // chat foreground
                    System.Drawing.Bitmap bitmap_chatSpace2 = ((WzCanvasProperty)mainBarProperties?["chatSpace2"])?.GetLinkedWzCanvasBitmap(); // chat background

                    HaUIGrid grid_chat = new HaUIGrid(1, 1, new HaUIInfo()
                    {
                        Margins = new HaUIMargin()
                        {
                            //Bottom = 50, // Add this line to move it lower
                        }
                    });
                    grid_chat.AddRenderable(0, 0, new HaUIImage(new HaUIInfo()
                    {
                        Bitmap = bitmap_chatSpace2,
                        VerticalAlignment = HaUIAlignment.Center,
                        HorizontalAlignment = HaUIAlignment.Start,
                        Margins = new HaUIMargin()
                        {
                            Left = 4,
                        }
                    }));
                    grid_chat.AddRenderable(0, 0, new HaUIImage(new HaUIInfo()
                    {
                        Bitmap = bitmap_chatSpace,
                        VerticalAlignment = HaUIAlignment.Center,
                        HorizontalAlignment = HaUIAlignment.Center,
                        Padding = new HaUIMargin()
                        {
                        }
                    }));

                    // notice
                    System.Drawing.Bitmap bitmap_notice = ((WzCanvasProperty)mainBarProperties?["notice"])?.GetLinkedWzCanvasBitmap();
                    HaUIImage uiImage_notice = new HaUIImage(new HaUIInfo()
                    {
                        Bitmap = bitmap_notice,
                        VerticalAlignment = HaUIAlignment.Start,
                        HorizontalAlignment = HaUIAlignment.End,
                        Margins = new HaUIMargin()
                        {
                            //Left= grid_chat.GetSize().Width,
                        }
                    });
                    grid_chat.AddRenderable(0, 0, uiImage_notice);

                    Texture2D texture_chatUI = grid_chat.Render().ToTexture2DAndDispose(device);
                    IDXObject dxObj_chatUI = new DXObject(UI_PADDING_PX, (int)(renderParams.RenderHeight / renderParams.RenderObjectScaling) - grid_chat.GetSize().Height - 36, texture_chatUI, 0);

                    // Scroll up+down, Chat, report/ claim, notice, stat, quest, inventory, equip, skill, key set
                    System.Drawing.Bitmap bitmap_lvNumber1 = ((WzCanvasProperty)mainBarProperties?["lvNumber/1"])?.GetLinkedWzCanvasBitmap();

                    // chat
                    WzSubProperty subProperty_chatTarget = (WzSubProperty)mainBarProperties?["chatTarget"];
                    WzSubProperty subProperty_chatTargetBase = subProperty_chatTarget?["base"] as WzSubProperty;
                    UIObject obj_Ui_chatTarget = new UIObject(subProperty_chatTargetBase, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_chatTarget.X = 2;
                    obj_Ui_chatTarget.Y = 0;

                    WzSubProperty subProperty_chatOpen = (WzSubProperty)mainBarProperties?["chatOpen"];
                    WzSubProperty subProperty_chatClose = (WzSubProperty)mainBarProperties?["chatClose"];
                    UIObject obj_Ui_chatOpen = new UIObject(subProperty_chatOpen, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_chatOpen.X = dxObj_chatUI.Width - obj_Ui_chatOpen.CanvasSnapshotWidth - 5;
                    obj_Ui_chatOpen.Y -= obj_Ui_chatOpen.Y - 4;

                    // chat scroll up/ down
                    WzSubProperty subProperty_scrollUp = (WzSubProperty)mainBarProperties?["scrollUp"];
                    WzSubProperty subProperty_scrollDown = (WzSubProperty)mainBarProperties?["scrollDown"];
                    UIObject obj_Ui_scrollUp = new UIObject(subProperty_scrollUp, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_scrollUp.X = obj_Ui_chatOpen.X + obj_Ui_scrollUp.CanvasSnapshotWidth + 8;
                    obj_Ui_scrollUp.Y = obj_Ui_chatOpen.Y - 2;
                    UIObject obj_Ui_scrollDown = new UIObject(subProperty_scrollDown, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_scrollDown.X = obj_Ui_scrollUp.X;
                    obj_Ui_scrollDown.Y = obj_Ui_scrollUp.Y + obj_Ui_scrollDown.CanvasSnapshotHeight + UI_PADDING_PX;

                    // chat
                    WzSubProperty subProperty_BtChat = (WzSubProperty)mainBarProperties?["BtChat"];
                    UIObject obj_Ui_BtChat = new UIObject(subProperty_BtChat, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtChat.X = obj_Ui_chatOpen.X + obj_Ui_BtChat.CanvasSnapshotWidth + 4;
                    obj_Ui_BtChat.Y = obj_Ui_chatOpen.Y - 2;

                    // report
                    WzSubProperty subProperty_BtClaim = (WzSubProperty)mainBarProperties?["BtClaim"]; // report
                    UIObject obj_Ui_BtClaim = new UIObject(subProperty_BtClaim, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtClaim.X = obj_Ui_BtChat.X + obj_Ui_BtClaim.CanvasSnapshotWidth;
                    obj_Ui_BtClaim.Y = obj_Ui_BtChat.Y;

                    // notice
                    // this is rendered above

                    UIObject obj_Ui_MemoIcon = null;
                    WzCanvasProperty memoIconProperty = uiStatusBar?["base"]?["iconMemo"] as WzCanvasProperty;
                    if (memoIconProperty != null)
                    {
                        var memoBitmap = memoIconProperty.GetLinkedWzCanvasBitmap();
                        if (memoBitmap != null)
                        {
                            Texture2D memoTexture = memoBitmap.ToTexture2DAndDispose(device);
                            IDXObject memoFrame = new DXObject(0, 0, memoTexture, 0);
                            BaseDXDrawableItem memoDrawable = new BaseDXDrawableItem(memoFrame, false);
                            obj_Ui_MemoIcon = new UIObject(memoDrawable, memoDrawable, memoDrawable, memoDrawable)
                            {
                                X = obj_Ui_BtClaim.X + 20,
                                Y = obj_Ui_BtClaim.Y + 2
                            };
                        }
                    }


                    // character
                    WzSubProperty subProperty_BtCharacter = (WzSubProperty)mainBarProperties?["BtCharacter"];
                    UIObject obj_Ui_BtCharacter = new UIObject(subProperty_BtCharacter, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtCharacter.X = obj_Ui_BtClaim.X + obj_Ui_BtCharacter.CanvasSnapshotWidth + bitmap_notice.Width - 7;
                    obj_Ui_BtCharacter.Y = obj_Ui_BtClaim.Y;

                    // stat
                    WzSubProperty subProperty_BtStat = (WzSubProperty)mainBarProperties?["BtStat"];
                    UIObject obj_Ui_BtStat = new UIObject(subProperty_BtStat, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtStat.X = obj_Ui_BtCharacter.X + obj_Ui_BtStat.CanvasSnapshotWidth;
                    obj_Ui_BtStat.Y = obj_Ui_BtCharacter.Y;

                    // quest
                    WzSubProperty subProperty_BtQuest = (WzSubProperty)mainBarProperties?["BtQuest"];
                    UIObject obj_Ui_BtQuest = new UIObject(subProperty_BtQuest, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtQuest.X = obj_Ui_BtStat.X + obj_Ui_BtQuest.CanvasSnapshotWidth;
                    obj_Ui_BtQuest.Y = obj_Ui_BtStat.Y;

                    // inventory
                    WzSubProperty subProperty_BtInven = (WzSubProperty)mainBarProperties?["BtInven"];
                    UIObject obj_Ui_BtInven = new UIObject(subProperty_BtInven, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtInven.X = obj_Ui_BtQuest.X + obj_Ui_BtInven.CanvasSnapshotWidth;
                    obj_Ui_BtInven.Y = obj_Ui_BtQuest.Y;

                    // equipment
                    WzSubProperty subProperty_BtEquip = (WzSubProperty)mainBarProperties?["BtEquip"];
                    UIObject obj_Ui_BtEquip = new UIObject(subProperty_BtEquip, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtEquip.X = obj_Ui_BtInven.X + obj_Ui_BtEquip.CanvasSnapshotWidth;
                    obj_Ui_BtEquip.Y = obj_Ui_BtInven.Y;

                    // skill
                    WzSubProperty subProperty_BtSkill = (WzSubProperty)mainBarProperties?["BtSkill"];
                    UIObject obj_Ui_BtSkill = new UIObject(subProperty_BtSkill, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtSkill.X = obj_Ui_BtEquip.X + obj_Ui_BtSkill.CanvasSnapshotWidth;
                    obj_Ui_BtSkill.Y = obj_Ui_BtEquip.Y;

                    // key setting
                    WzSubProperty subProperty_BtKeysetting = (WzSubProperty)mainBarProperties?["BtKeysetting"];
                    UIObject obj_Ui_BtKeysetting = new UIObject(subProperty_BtKeysetting, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    obj_Ui_BtKeysetting.X = obj_Ui_BtSkill.X + obj_Ui_BtSkill.CanvasSnapshotWidth + 4;/* + obj_Ui_BtKeysetting.CanvasSnapshotWidth*/;
                    obj_Ui_BtKeysetting.Y = obj_Ui_BtSkill.Y;

                    // Add all items to the main grid
                    grid.AddRenderable(0, 0, stackPanel_charStats);

                    Texture2D texture_backgrnd = grid.Render().ToTexture2DAndDispose(device);

                    IDXObject dxObj_backgrnd = new DXObject(0, (int)(renderParams.RenderHeight / renderParams.RenderObjectScaling) - grid.GetSize().Height, texture_backgrnd, 0);
                    StatusBarUI statusBar = new StatusBarUI(dxObj_backgrnd, obj_Ui_BtCashShop, obj_Ui_BtMTS, obj_Ui_BtMenu, obj_Ui_BtSystem, obj_Ui_BtChannel,
                        new Point(dxObj_backgrnd.X, dxObj_backgrnd.Y),
                        new List<UIObject> { });
                    statusBar.InitializeButtons();

                    // Set gauge textures if loaded from WZ files
                    if (hpGaugeTexture != null || mpGaugeTexture != null || expGaugeTexture != null) {
                    statusBar.SetGaugeTextures(hpGaugeTexture, mpGaugeTexture, expGaugeTexture);
                    statusBar.SetBuffIconTextures(LoadBuffIconTextures(uiBuffIcon, device));
                    }
                    statusBar.SetTooltipTextures(LoadSkillTooltipTextures(device));
                    statusBar.SetWarningAnimations(
                        LoadStatusBarWarningAnimation(mainBarProperties?["aniHPGauge"] as WzSubProperty, device),
                        LoadStatusBarWarningAnimation(mainBarProperties?["aniMPGauge"] as WzSubProperty, device));
                    statusBar.SetKeyDownBarTextures(LoadKeyDownBarTextures(uiBasic, device));

                    // Load bitmap font digit textures from StatusBar2.img/mainBar/gauge/number
                    // This is the correct source for HP/MP/EXP display with proper origin alignment
                    // The origin.Y values are critical for vertical alignment:
                    //   - Brackets [ ] have origin Y=1 (taller than digits, shift up 1px)
                    //   - Slash \ has origin Y=1 (shift up 1px)
                    //   - Dot . has origin Y=-6 (small, sits at bottom of line)
                    //   - Digits 0-9 have origin Y=0 (baseline)
                    WzSubProperty gaugeNumberProp = gaugeProperty?["number"] as WzSubProperty;
                    if (gaugeNumberProp != null) {
                        Texture2D[] digitTextures = new Texture2D[10];
                        Point[] digitOrigins = new Point[10];
                        Texture2D[] levelDigitTextures = new Texture2D[10];
                        Point[] levelDigitOrigins = new Point[10];
                        bool hasDigits = false;

                        // Helper to get origin from canvas
                        Point GetCanvasOrigin(WzCanvasProperty canvas) {
                            if (canvas == null) return Point.Zero;
                            var origin = canvas["origin"] as WzVectorProperty;
                            if (origin != null) {
                                return new Point(origin.X.Value, origin.Y.Value);
                            }
                            return Point.Zero;
                        }

                        WzSubProperty levelNumberProp = mainBarProperties?["lvNumber"] as WzSubProperty;
                        if (levelNumberProp != null) {
                            for (int i = 0; i < 10; i++) {
                                WzCanvasProperty levelDigitCanvas = levelNumberProp[i.ToString()] as WzCanvasProperty;
                                if (levelDigitCanvas == null) {
                                    continue;
                                }

                                var levelBitmap = levelDigitCanvas.GetLinkedWzCanvasBitmap();
                                if (levelBitmap == null) {
                                    continue;
                                }

                                levelDigitTextures[i] = levelBitmap.ToTexture2DAndDispose(device);
                                levelDigitOrigins[i] = GetCanvasOrigin(levelDigitCanvas);
                            }
                        }

                        // Load digits 0-9 with origins
                        for (int i = 0; i < 10; i++) {
                            WzCanvasProperty digitCanvas = gaugeNumberProp[i.ToString()] as WzCanvasProperty;
                            if (digitCanvas != null) {
                                var bitmap = digitCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    digitTextures[i] = bitmap.ToTexture2DAndDispose(device);
                                    digitOrigins[i] = GetCanvasOrigin(digitCanvas);
                                    hasDigits = true;
                                }
                            }
                        }

                        if (hasDigits) {
                            // Load special characters with origins from gauge/number
                            // These have proper origin.Y values for alignment
                            Texture2D slashTexture = null, percentTexture = null;
                            Texture2D bracketLeftTexture = null, bracketRightTexture = null;
                            Texture2D dotTexture = null;
                            Point slashOrigin = Point.Zero, percentOrigin = Point.Zero;
                            Point bracketLeftOrigin = Point.Zero, bracketRightOrigin = Point.Zero;
                            Point dotOrigin = Point.Zero;

                            // Left bracket [ - origin Y=1 for proper alignment
                            WzCanvasProperty lbCanvas = gaugeNumberProp["["] as WzCanvasProperty;
                            if (lbCanvas != null) {
                                var bitmap = lbCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    bracketLeftTexture = bitmap.ToTexture2DAndDispose(device);
                                    bracketLeftOrigin = GetCanvasOrigin(lbCanvas);
                                }
                            }

                            // Right bracket ] - origin Y=1 for proper alignment
                            WzCanvasProperty rbCanvas = gaugeNumberProp["]"] as WzCanvasProperty;
                            if (rbCanvas != null) {
                                var bitmap = rbCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    bracketRightTexture = bitmap.ToTexture2DAndDispose(device);
                                    bracketRightOrigin = GetCanvasOrigin(rbCanvas);
                                }
                            }

                            // Slash \ (backslash used as divider) - origin Y=1 for proper alignment
                            WzCanvasProperty slashCanvas = gaugeNumberProp["\\"] as WzCanvasProperty;
                            if (slashCanvas != null) {
                                var bitmap = slashCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    slashTexture = bitmap.ToTexture2DAndDispose(device);
                                    slashOrigin = GetCanvasOrigin(slashCanvas);
                                }
                            }

                            // Percent % - origin Y=0
                            WzCanvasProperty percentCanvas = gaugeNumberProp["%"] as WzCanvasProperty;
                            if (percentCanvas != null) {
                                var bitmap = percentCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    percentTexture = bitmap.ToTexture2DAndDispose(device);
                                    percentOrigin = GetCanvasOrigin(percentCanvas);
                                }
                            }

                            // Dot . - origin Y=-6 (sits at bottom of line)
                            WzCanvasProperty dotCanvas = gaugeNumberProp["."] as WzCanvasProperty;
                            if (dotCanvas != null) {
                                var bitmap = dotCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    dotTexture = bitmap.ToTexture2DAndDispose(device);
                                    dotOrigin = GetCanvasOrigin(dotCanvas);
                                }
                            }

                            statusBar.SetDigitTextures(digitTextures, digitOrigins,
                                slashTexture, slashOrigin,
                                percentTexture, percentOrigin,
                                bracketLeftTexture, bracketLeftOrigin,
                                bracketRightTexture, bracketRightOrigin,
                                dotTexture, dotOrigin);

                            if (levelDigitTextures[0] != null) {
                                statusBar.SetLevelDigitTextures(levelDigitTextures, levelDigitOrigins);
                            }
                        }
                    }

                    StatusBarChatUI chatUI = new StatusBarChatUI(dxObj_chatUI, new Point(dxObj_chatUI.X, dxObj_chatUI.Y),
                         new List<UIObject> {
                             obj_Ui_chatTarget,
                             obj_Ui_chatOpen,
                             obj_Ui_scrollUp, obj_Ui_scrollDown,
                             obj_Ui_BtChat, obj_Ui_BtClaim,
                             obj_Ui_MemoIcon,
                             obj_Ui_BtCharacter, obj_Ui_BtStat, obj_Ui_BtQuest, obj_Ui_BtInven, obj_Ui_BtEquip, obj_Ui_BtSkill, obj_Ui_BtKeysetting
                          }
                        );
                    chatUI.InitializeButtons();
                    chatUI.SetChatEnterTexture(LoadCanvasTexture(mainBarProperties?["chatEnter"] as WzCanvasProperty, device));
                    chatUI.SetChatTargetTextures(LoadChatTargetTextures(subProperty_chatTarget, device));
                    chatUI.SetPointNotificationAnimations(
                        LoadPointNotificationAnimation(mainBarProperties?["ApNotify"] as WzSubProperty, device),
                        LoadPointNotificationAnimation(mainBarProperties?["SpNotify"] as WzSubProperty, device));
                    chatUI.BindControls(obj_Ui_chatTarget, obj_Ui_chatOpen, obj_Ui_scrollUp, obj_Ui_scrollDown, obj_Ui_BtCharacter, obj_Ui_MemoIcon);

                    return new Tuple<StatusBarUI, StatusBarChatUI>(statusBar, chatUI);
                }
            }
            else
            {
                // Pre-BigBang and Beta MapleStory status bar (uses StatusBar.img instead of StatusBar2.img)
                // This handles both pre-BB (v82 etc.) and beta (v15 etc.) since they share similar structure:
                // - base/backgrnd (800x71 main background)
                // - gauge/bar (gauge fill texture)
                // - number/0-9, Lbracket, Rbracket, slash, percent (digit textures)
                // - BtShop, BtMenu, BtShort (common buttons, some may be missing in beta)
                WzSubProperty baseProperties = (uiStatusBar?["base"] as WzSubProperty);
                WzSubProperty gaugeProperties = (uiStatusBar?["gauge"] as WzSubProperty);
                WzSubProperty numberProperties = (uiStatusBar?["number"] as WzSubProperty);

                if (baseProperties != null)
                {
                    HaUIGrid grid = new HaUIGrid(1, 1);

                    // Main background - 800x71 in pre-BB
                    System.Drawing.Bitmap backgrnd = ((WzCanvasProperty)baseProperties?["backgrnd"])?.GetLinkedWzCanvasBitmap();

                    if (backgrnd != null)
                    {
                        grid.AddRenderable(0, 0, new HaUIImage(new HaUIInfo()
                        {
                            Bitmap = backgrnd,
                            VerticalAlignment = HaUIAlignment.Start,
                            HorizontalAlignment = HaUIAlignment.Start
                        }));
                    }

                    const int UI_PADDING_PX = 2;

                    // Load gauge textures for HP, MP, EXP bars
                    Texture2D hpGaugeTexture = null, mpGaugeTexture = null, expGaugeTexture = null;

                    if (gaugeProperties != null)
                    {
                        // Pre-BB uses gauge/bar for the gauge fill, gauge/hpFlash and gauge/mpFlash for animations
                        // The gauge/bar is the main gauge texture
                        WzCanvasProperty barCanvas = gaugeProperties["bar"] as WzCanvasProperty;
                        if (barCanvas != null)
                        {
                            var barBitmap = barCanvas.GetLinkedWzCanvasBitmap();
                            if (barBitmap != null)
                            {
                                // Pre-BB uses the same gauge bar texture for all gauges
                                try
                                {
                                    hpGaugeTexture = barBitmap.ToTexture2D(device);
                                    mpGaugeTexture = barBitmap.ToTexture2D(device);
                                    expGaugeTexture = barBitmap.ToTexture2D(device);
                                }
                                finally
                                {
                                    barBitmap.Dispose();
                                }
                            }
                        }
                    }

                    // Sound properties for buttons
                    WzBinaryProperty binaryProp_BtMouseClickSoundProperty = (WzBinaryProperty)soundUIImage?["BtMouseClick"];
                    WzBinaryProperty binaryProp_BtMouseOverSoundProperty = (WzBinaryProperty)soundUIImage?["BtMouseOver"];

                    // Pre-BB buttons: BtShop (CashShop), BtMenu, BtNPT (MTS equivalent), BtShort
                    WzSubProperty subProperty_BtShop = (WzSubProperty)uiStatusBar?["BtShop"];
                    UIObject obj_Ui_BtCashShop = null;
                    if (subProperty_BtShop != null)
                    {
                        obj_Ui_BtCashShop = new UIObject(subProperty_BtShop, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                            false,
                            new Point(0, 0), device);
                        obj_Ui_BtCashShop.X = backgrnd?.Width ?? 800 - obj_Ui_BtCashShop.CanvasSnapshotWidth - UI_PADDING_PX;
                        obj_Ui_BtCashShop.Y = backgrnd?.Height ?? 71;
                    }

                    WzSubProperty subProperty_BtNPT = (WzSubProperty)uiStatusBar?["BtNPT"];
                    UIObject obj_Ui_BtMTS = null;
                    if (subProperty_BtNPT != null)
                    {
                        obj_Ui_BtMTS = new UIObject(subProperty_BtNPT, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                            false,
                            new Point(0, 0), device);
                        if (obj_Ui_BtCashShop != null)
                        {
                            obj_Ui_BtMTS.X = obj_Ui_BtCashShop.X - obj_Ui_BtMTS.CanvasSnapshotWidth;
                        }
                        obj_Ui_BtMTS.Y = backgrnd?.Height ?? 71;
                    }

                    WzSubProperty subProperty_BtMenu = (WzSubProperty)uiStatusBar?["BtMenu"];
                    UIObject obj_Ui_BtMenu = null;
                    if (subProperty_BtMenu != null)
                    {
                        obj_Ui_BtMenu = new UIObject(subProperty_BtMenu, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                            false,
                            new Point(0, 0), device);
                        if (obj_Ui_BtMTS != null)
                        {
                            obj_Ui_BtMenu.X = obj_Ui_BtMTS.X - obj_Ui_BtMenu.CanvasSnapshotWidth;
                        }
                        else if (obj_Ui_BtCashShop != null)
                        {
                            obj_Ui_BtMenu.X = obj_Ui_BtCashShop.X - obj_Ui_BtMenu.CanvasSnapshotWidth;
                        }
                        obj_Ui_BtMenu.Y = backgrnd?.Height ?? 71;
                    }

                    WzSubProperty subProperty_BtShort = (WzSubProperty)uiStatusBar?["BtShort"];
                    UIObject obj_Ui_BtSystem = null;
                    if (subProperty_BtShort != null)
                    {
                        obj_Ui_BtSystem = new UIObject(subProperty_BtShort, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                            false,
                            new Point(0, 0), device);
                        if (obj_Ui_BtMenu != null)
                        {
                            obj_Ui_BtSystem.X = obj_Ui_BtMenu.X - obj_Ui_BtSystem.CanvasSnapshotWidth;
                        }
                        obj_Ui_BtSystem.Y = backgrnd?.Height ?? 71;
                    }

                    // Pre-BB doesn't have BtChannel, create a dummy null
                    UIObject obj_Ui_BtChannel = null;

                    Texture2D texture_backgrnd = grid.Render().ToTexture2DAndDispose(device);

                    IDXObject dxObj_backgrnd = new DXObject(0, (int)(renderParams.RenderHeight / renderParams.RenderObjectScaling) - grid.GetSize().Height, texture_backgrnd, 0);
                    StatusBarUI statusBar = new StatusBarUI(dxObj_backgrnd,
                        obj_Ui_BtCashShop,
                        obj_Ui_BtMTS,
                        obj_Ui_BtMenu,
                        obj_Ui_BtSystem,
                        obj_Ui_BtChannel,
                        new Point(dxObj_backgrnd.X, dxObj_backgrnd.Y),
                        new List<UIObject> { });
                    statusBar.InitializeButtons();

                    // Set gauge textures if loaded
                    if (hpGaugeTexture != null || mpGaugeTexture != null || expGaugeTexture != null)
                    {
                    statusBar.SetGaugeTextures(hpGaugeTexture, mpGaugeTexture, expGaugeTexture);
                    statusBar.SetBuffIconTextures(LoadBuffIconTextures(uiBuffIcon, device));
                    }
                    statusBar.SetTooltipTextures(LoadSkillTooltipTextures(device));
                    statusBar.SetWarningAnimations(
                        LoadStatusBarWarningAnimation(gaugeProperties?["hpFlash"] as WzSubProperty, device),
                        LoadStatusBarWarningAnimation(gaugeProperties?["mpFlash"] as WzSubProperty, device));
                    statusBar.SetKeyDownBarTextures(LoadKeyDownBarTextures(uiBasic, device));

                    // Load bitmap font digit textures from StatusBar.img/number
                    if (numberProperties != null)
                    {
                        Texture2D[] digitTextures = new Texture2D[10];
                        Point[] digitOrigins = new Point[10];
                        bool hasDigits = false;

                        // Helper to get origin from canvas
                        Point GetCanvasOrigin(WzCanvasProperty canvas)
                        {
                            if (canvas == null) return Point.Zero;
                            var origin = canvas["origin"] as WzVectorProperty;
                            if (origin != null)
                            {
                                return new Point(origin.X.Value, origin.Y.Value);
                            }
                            return Point.Zero;
                        }

                        // Load digits 0-9 with origins
                        for (int i = 0; i < 10; i++)
                        {
                            WzCanvasProperty digitCanvas = numberProperties[i.ToString()] as WzCanvasProperty;
                            if (digitCanvas != null)
                            {
                                var bitmap = digitCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null)
                                {
                                    digitTextures[i] = bitmap.ToTexture2DAndDispose(device);
                                    digitOrigins[i] = GetCanvasOrigin(digitCanvas);
                                    hasDigits = true;
                                }
                            }
                        }

                        if (hasDigits)
                        {
                            // Load special characters - Pre-BB uses Lbracket, Rbracket, slash, percent
                            Texture2D slashTexture = null, percentTexture = null;
                            Texture2D bracketLeftTexture = null, bracketRightTexture = null;
                            Texture2D dotTexture = null;
                            Point slashOrigin = Point.Zero, percentOrigin = Point.Zero;
                            Point bracketLeftOrigin = Point.Zero, bracketRightOrigin = Point.Zero;
                            Point dotOrigin = Point.Zero;

                            // Left bracket [
                            WzCanvasProperty lbCanvas = numberProperties["Lbracket"] as WzCanvasProperty;
                            if (lbCanvas != null)
                            {
                                var bitmap = lbCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null)
                                {
                                    bracketLeftTexture = bitmap.ToTexture2DAndDispose(device);
                                    bracketLeftOrigin = GetCanvasOrigin(lbCanvas);
                                }
                            }

                            // Right bracket ]
                            WzCanvasProperty rbCanvas = numberProperties["Rbracket"] as WzCanvasProperty;
                            if (rbCanvas != null)
                            {
                                var bitmap = rbCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null)
                                {
                                    bracketRightTexture = bitmap.ToTexture2DAndDispose(device);
                                    bracketRightOrigin = GetCanvasOrigin(rbCanvas);
                                }
                            }

                            // Slash /
                            WzCanvasProperty slashCanvas = numberProperties["slash"] as WzCanvasProperty;
                            if (slashCanvas != null)
                            {
                                var bitmap = slashCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null)
                                {
                                    slashTexture = bitmap.ToTexture2DAndDispose(device);
                                    slashOrigin = GetCanvasOrigin(slashCanvas);
                                }
                            }

                            // Percent %
                            WzCanvasProperty percentCanvas = numberProperties["percent"] as WzCanvasProperty;
                            if (percentCanvas != null)
                            {
                                var bitmap = percentCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null)
                                {
                                    percentTexture = bitmap.ToTexture2DAndDispose(device);
                                    percentOrigin = GetCanvasOrigin(percentCanvas);
                                }
                            }

                            statusBar.SetDigitTextures(digitTextures, digitOrigins,
                                slashTexture, slashOrigin,
                                percentTexture, percentOrigin,
                                bracketLeftTexture, bracketLeftOrigin,
                                bracketRightTexture, bracketRightOrigin,
                                dotTexture, dotOrigin);
                        }
                    }

                    // Pre-BB doesn't have separate chat UI, return null for chatUI
                    return new Tuple<StatusBarUI, StatusBarChatUI>(statusBar, null);
                }
            }
            return null;
        }

        private static Dictionary<string, Texture2D> LoadBuffIconTextures(WzImage uiBuffIcon, GraphicsDevice device)
        {
            var buffIconTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            if (uiBuffIcon == null || device == null)
            {
                return buffIconTextures;
            }

            LoadBuffIconsRecursive(buffIconTextures, uiBuffIcon, device, string.Empty);
            return buffIconTextures;
        }

        private static Texture2D[] LoadSkillTooltipTextures(GraphicsDevice device)
        {
            Texture2D[] tooltipFrames = new Texture2D[3];
            if (device == null)
            {
                return tooltipFrames;
            }

            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img");
            if (uiWindow2Image == null)
            {
                return tooltipFrames;
            }

            WzSubProperty skillProperty = uiWindow2Image["Skill"] as WzSubProperty;
            WzSubProperty mainProperty = skillProperty?["main"] as WzSubProperty;
            if (mainProperty == null)
            {
                return tooltipFrames;
            }

            tooltipFrames[0] = LoadCanvasTexture(mainProperty["tip0"] as WzCanvasProperty, device);
            tooltipFrames[1] = LoadCanvasTexture(mainProperty["tip1"] as WzCanvasProperty, device);
            tooltipFrames[2] = LoadCanvasTexture(mainProperty["tip2"] as WzCanvasProperty, device);
            return tooltipFrames;
        }

        private static Dictionary<string, StatusBarKeyDownBarTextures> LoadKeyDownBarTextures(WzImage uiBasic, GraphicsDevice device)
        {
            var keyDownBarTextures = new Dictionary<string, StatusBarKeyDownBarTextures>(StringComparer.OrdinalIgnoreCase);
            if (uiBasic == null || device == null)
            {
                return keyDownBarTextures;
            }

            static Point GetCanvasOrigin(WzCanvasProperty canvas)
            {
                if (!(canvas?["origin"] is WzVectorProperty origin))
                {
                    return Point.Zero;
                }

                return new Point(origin.X.Value, origin.Y.Value);
            }

            foreach (string skinKey in new[] { "KeyDownBar", "KeyDownBar1", "KeyDownBar2", "KeyDownBar3", "KeyDownBar4" })
            {
                if (!(uiBasic[skinKey] is WzSubProperty skinProperty))
                {
                    continue;
                }

                WzCanvasProperty barCanvas = skinProperty["bar"] as WzCanvasProperty;

                var textures = new StatusBarKeyDownBarTextures
                {
                    Bar = LoadCanvasTexture(barCanvas, device),
                    Gauge = LoadCanvasTexture(skinProperty["gauge"] as WzCanvasProperty, device),
                    Graduation = LoadCanvasTexture(skinProperty["graduation"] as WzCanvasProperty, device),
                    BarOrigin = GetCanvasOrigin(barCanvas)
                };

                if (textures.Bar != null || textures.Gauge != null || textures.Graduation != null)
                {
                    keyDownBarTextures[skinKey] = textures;
                }
            }

            return keyDownBarTextures;
        }

        private static StatusBarWarningAnimation LoadStatusBarWarningAnimation(WzSubProperty warningProperty, GraphicsDevice device)
        {
            var animation = new StatusBarWarningAnimation();
            if (warningProperty == null || device == null)
            {
                return animation;
            }

            var frames = new List<Texture2D>();
            int frameDelayMs = animation.FrameDelayMs;

            for (int frameIndex = 0; ; frameIndex++)
            {
                if (!(warningProperty[frameIndex.ToString()] is WzCanvasProperty warningCanvas))
                {
                    break;
                }

                Texture2D frameTexture = LoadCanvasTexture(warningCanvas, device);
                if (frameTexture == null)
                {
                    continue;
                }

                frames.Add(frameTexture);
                if (warningCanvas["delay"] is WzIntProperty delayProperty && delayProperty.Value > 0)
                {
                    frameDelayMs = delayProperty.Value;
                }
            }

            animation.Frames = frames.ToArray();
            animation.FrameDelayMs = frameDelayMs;
            animation.FlashDurationMs = 500;
            return animation;
        }

        private static Dictionary<MapSimulatorChatTargetType, Texture2D> LoadChatTargetTextures(WzSubProperty chatTargetProperty, GraphicsDevice device)
        {
            var textures = new Dictionary<MapSimulatorChatTargetType, Texture2D>();
            if (chatTargetProperty == null || device == null)
            {
                return textures;
            }

            AddChatTargetTexture(textures, chatTargetProperty, "all", MapSimulatorChatTargetType.All, device);
            AddChatTargetTexture(textures, chatTargetProperty, "friend", MapSimulatorChatTargetType.Friend, device);
            AddChatTargetTexture(textures, chatTargetProperty, "party", MapSimulatorChatTargetType.Party, device);
            AddChatTargetTexture(textures, chatTargetProperty, "guild", MapSimulatorChatTargetType.Guild, device);
            AddChatTargetTexture(textures, chatTargetProperty, "association", MapSimulatorChatTargetType.Association, device);
            AddChatTargetTexture(textures, chatTargetProperty, "expedition", MapSimulatorChatTargetType.Expedition, device);
            return textures;
        }

        private static StatusBarChatUI.StatusBarPointNotificationAnimation LoadPointNotificationAnimation(
            WzSubProperty notificationProperty,
            GraphicsDevice device)
        {
            var animation = new StatusBarChatUI.StatusBarPointNotificationAnimation();
            if (notificationProperty == null || device == null)
            {
                return animation;
            }

            var frames = new List<Texture2D>();
            var origins = new List<Point>();
            var frameDelays = new List<int>();

            static Point GetCanvasOrigin(WzCanvasProperty canvas)
            {
                if (!(canvas?["origin"] is WzVectorProperty origin))
                {
                    return Point.Zero;
                }

                return new Point(origin.X.Value, origin.Y.Value);
            }

            for (int frameIndex = 0; ; frameIndex++)
            {
                if (!(notificationProperty[frameIndex.ToString()] is WzCanvasProperty notificationCanvas))
                {
                    break;
                }

                Texture2D frameTexture = LoadCanvasTexture(notificationCanvas, device);
                if (frameTexture == null)
                {
                    continue;
                }

                frames.Add(frameTexture);
                origins.Add(GetCanvasOrigin(notificationCanvas));
                int delayMs = 120;
                if (notificationCanvas["delay"] is WzIntProperty delayProperty && delayProperty.Value > 0)
                {
                    delayMs = delayProperty.Value;
                }

                frameDelays.Add(delayMs);
            }

            animation.Frames = frames.ToArray();
            animation.Origins = origins.ToArray();
            animation.FrameDelaysMs = frameDelays.ToArray();
            return animation;
        }

        private static void AddChatTargetTexture(
            Dictionary<MapSimulatorChatTargetType, Texture2D> textures,
            WzSubProperty chatTargetProperty,
            string propertyName,
            MapSimulatorChatTargetType targetType,
            GraphicsDevice device)
        {
            Texture2D texture = LoadCanvasTexture(chatTargetProperty?[propertyName] as WzCanvasProperty, device);
            if (texture != null)
            {
                textures[targetType] = texture;
            }
        }

        private static void LoadBuffIconsRecursive(
            Dictionary<string, Texture2D> buffIconTextures,
            WzObject currentNode,
            GraphicsDevice device,
            string currentPath)
        {
            if (currentNode == null || device == null)
            {
                return;
            }

            if (currentNode is WzCanvasProperty iconCanvas)
            {
                if (!string.Equals(currentNode.Name, "0", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                string normalizedPath = NormalizeBuffIconKey(currentPath);
                if (string.IsNullOrWhiteSpace(normalizedPath) || buffIconTextures.ContainsKey(normalizedPath))
                {
                    return;
                }

                var bitmap = iconCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap == null)
                {
                    return;
                }

                Texture2D texture = bitmap.ToTexture2DAndDispose(device);
                buffIconTextures[normalizedPath] = texture;
                buffIconTextures[$"{normalizedPath}/0"] = texture;
                return;
            }

            if (!(currentNode is IPropertyContainer container))
            {
                return;
            }

            foreach (WzImageProperty child in container.WzProperties)
            {
                string childPath = string.IsNullOrWhiteSpace(currentPath)
                    ? child.Name
                    : $"{currentPath}/{child.Name}";
                LoadBuffIconsRecursive(buffIconTextures, child, device, childPath);
            }
        }

        private static string NormalizeBuffIconKey(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.EndsWith("/0", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - 2)
                : path;
        }

        private static Texture2D LoadCanvasTexture(WzCanvasProperty canvas, GraphicsDevice device)
        {
            if (canvas == null || device == null)
            {
                return null;
            }

            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(device);
        }

        #endregion

        #region Minimap
        /// <summary>
        /// Draws the frame and the UI of the minimap.
        /// TODO: This whole thing needs to be dramatically simplified via further abstraction to keep it noob-proof :(
        /// </summary>
        /// <param name="uiWindow1Image">UI.wz/UIWindow1.img pre-bb</param>
        /// <param name="uiWindow2Image">UI.wz/UIWindow2.img post-bb</param>
        /// <param name="uiBasicImage">UI.wz/Basic.img</param>
        /// <param name="mapBoard"></param>
        /// <param name="device"></param>
        /// <param name="UserScreenScaleFactor">The scale factor of the window (DPI)</param>
        /// <param name="MapName">The map name. i.e The Hill North</param>
        /// <param name="StreetName">The street name. i.e Hidden street</param>
        /// <param name="soundUIImage">Sound.wz/UI.img</param>
        /// <param name="bBigBang">Big bang update</param>
        /// <returns></returns>
        public static MinimapUI CreateMinimapFromProperty(
            WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiBasicImage, Board mapBoard,
            GraphicsDevice device, float UserScreenScaleFactor, string MapName, string StreetName,
            WzImage soundUIImage, bool bBigBang)
        {
            if (mapBoard.MiniMap == null)
                return null;

            WzSubProperty minimapFrameProperty = (WzSubProperty)uiWindow2Image?["MiniMap"];
            if (minimapFrameProperty == null) // UIWindow2 not available pre-BB.
            {
                minimapFrameProperty = (WzSubProperty)uiWindow1Image["MiniMap"];
            }

            WzSubProperty maxMapProperty = (WzSubProperty)minimapFrameProperty["MaxMap"];
            WzSubProperty minMapProperty = (WzSubProperty)minimapFrameProperty["MinMap"];
            WzSubProperty maxMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MaxMapMirror"]; // for Zero maps
            WzSubProperty minMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MinMapMirror"]; // for Zero maps


            WzSubProperty useFrameMaxMap;
            WzSubProperty useFrameMinMap;
            if (mapBoard.MapInfo.zeroSideOnly || MapConstants.IsZerosTemple(mapBoard.MapInfo.id)) // zero's temple
            {
                useFrameMaxMap = maxMapMirrorProperty;
                useFrameMinMap = minMapMirrorProperty;
            }
            else
            {
                useFrameMaxMap = maxMapProperty;
                useFrameMinMap = minMapProperty;
            }

            // Wz frames
            System.Drawing.Bitmap c = ((WzCanvasProperty)useFrameMaxMap?["c"])?.GetLinkedWzCanvasBitmap(); // the bg color
            System.Drawing.Bitmap e = ((WzCanvasProperty)useFrameMaxMap?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap n = ((WzCanvasProperty)useFrameMaxMap?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap s = ((WzCanvasProperty)useFrameMaxMap?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap w = ((WzCanvasProperty)useFrameMaxMap?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap ne = ((WzCanvasProperty)useFrameMaxMap?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            System.Drawing.Bitmap nw = ((WzCanvasProperty)useFrameMaxMap?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            System.Drawing.Bitmap se = ((WzCanvasProperty)useFrameMaxMap?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            System.Drawing.Bitmap sw = ((WzCanvasProperty)useFrameMaxMap?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left

            // Constants
            const int MAPMARK_MAPNAME_LEFT_MARGIN = 4;
            const int MAPMARK_MAPNAME_TOP_MARGIN = 17;
            const int MAP_IMAGE_TEXT_PADDING = 2; // the number of pixels from the left to draw the minimap image
            System.Drawing.Color color_bgFill = System.Drawing.Color.Transparent;
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;


            // Map background image
            // Using HaUIGrid and HaUIStackPanel
            System.Drawing.Bitmap miniMapImage = mapBoard.MiniMap; // the original minimap image without UI frame overlay


            // Create Map mark
            System.Drawing.Bitmap mapMark = null;
            if (Program.InfoManager.MapMarks.ContainsKey(mapBoard.MapInfo.mapMark))
            {
                mapMark = Program.InfoManager.MapMarks[mapBoard.MapInfo.mapMark];
            }

            // Create map minimap image
            HaUIImage minimapUiImage = new HaUIImage(new HaUIInfo()
            {
                Bitmap = miniMapImage,
                HorizontalAlignment = HaUIAlignment.Center,
                Margins = new HaUIMargin() { Left = MAP_IMAGE_TEXT_PADDING + 10, Right = MAP_IMAGE_TEXT_PADDING + 10, Top = 10, Bottom = 0 },
                //Padding = new HaUIPadding() { Bottom = 10, Left = 10, Right = 10 }
            });

            // Create BitmapStackPanel for text and minimap
            HaUIStackPanel fullMiniMapStackPanel = new HaUIStackPanel(HaUIStackOrientation.Vertical, new HaUIInfo()
            {
                MinWidth = 150 // set a min width, so the MapName and StreetName is not cut off if the map image is too thin
            });
            HaUIStackPanel mapNameMarkStackPanel = new HaUIStackPanel(HaUIStackOrientation.Horizontal, new HaUIInfo()
            {
                Margins = new HaUIMargin() { Top = MAPMARK_MAPNAME_TOP_MARGIN, Left = MAPMARK_MAPNAME_LEFT_MARGIN, Bottom = 0, Right = 0 },
            });

            if (mapMark != null)
            {
                // minimap map-mark image
                HaUIImage mapNameMarkImage = new HaUIImage(new HaUIInfo()
                {
                    Bitmap = mapMark,
                });
                mapNameMarkStackPanel.AddRenderable(mapNameMarkImage);
            }
            // Minimap name, and street name
            string renderText = string.Format("{0}{1}{2}", StreetName, Environment.NewLine, MapName);
            HaUIText haUITextMapNameStreetName = new HaUIText(renderText, color_foreGround, GLOBAL_FONT, MINIMAP_STREETNAME_TOOLTIP_FONTSIZE, UserScreenScaleFactor);
            haUITextMapNameStreetName.GetInfo().Margins.Top = 3;
            haUITextMapNameStreetName.GetInfo().Margins.Left = MAP_IMAGE_TEXT_PADDING;
            haUITextMapNameStreetName.GetInfo().Margins.Right = MAP_IMAGE_TEXT_PADDING;

            mapNameMarkStackPanel.AddRenderable(haUITextMapNameStreetName);
            fullMiniMapStackPanel.AddRenderable(mapNameMarkStackPanel);

            System.Drawing.Bitmap finalMininisedMinimapBitmap = HaUIHelper.RenderAndMergeMinimapUIFrame(fullMiniMapStackPanel, color_bgFill, ne, nw, se, sw, e, w, n, s,
                c, mapMark != null ? mapMark.Height : 0);

            HaUIGrid minimapUiGrid = new HaUIGrid(1, 1);
            minimapUiGrid.GetInfo().Margins.Top = 10;
            minimapUiGrid.GetInfo().HorizontalAlignment = HaUIAlignment.Center;
            minimapUiGrid.GetInfo().VerticalAlignment = HaUIAlignment.Center;
            minimapUiGrid.AddRenderable(minimapUiImage);
            fullMiniMapStackPanel.AddRenderable(minimapUiGrid);

            // Render final minimap Bitmap with UI frames
            System.Drawing.Bitmap finalFullMinimapBitmap = HaUIHelper.RenderAndMergeMinimapUIFrame(fullMiniMapStackPanel, color_bgFill, ne, nw, se, sw, e, w, n, s,
                c, mapMark != null ? mapMark.Height : 0);

            Texture2D texturer_miniMapMinimised = finalMininisedMinimapBitmap.ToTexture2DAndDispose(device);
            Texture2D texturer_miniMap = finalFullMinimapBitmap.ToTexture2DAndDispose(device);

            // Dots pixel
            System.Drawing.Bitmap bmp_DotPixel = new System.Drawing.Bitmap(2, 4);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_DotPixel))
            {
                graphics.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Yellow), new System.Drawing.RectangleF(0, 0, bmp_DotPixel.Width, bmp_DotPixel.Height));
                graphics.Flush();
            }
            IDXObject dxObj_miniMapPixel = new DXObject(0, n.Height, bmp_DotPixel.ToTexture2DAndDispose(device), 0);

            // Map
            IDXObject dxObj_miniMap_Minimised = new DXObject(0, 0, texturer_miniMapMinimised, 0);
            IDXObject dxObj_miniMap = new DXObject(0, 0, texturer_miniMap, 0); // starting position of the minimap in the map

            // need to calculate how much x position, where the map is shifted to the center by HorizontalAlignment
            // to compensate for in the character dot position indicator
            HaUISize fullMiniMapStackPanelSize = fullMiniMapStackPanel.GetSize();
            int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(fullMiniMapStackPanelSize.Width, minimapUiImage.GetInfo().Bitmap.Width, minimapUiGrid.GetInfo().HorizontalAlignment);

            Point minimapImageOffset = new Point(MAP_IMAGE_TEXT_PADDING + alignmentXOffset, 0);
            BaseDXDrawableItem userMarker = null;
            BaseDXDrawableItem npcMarker = null;
            BaseDXDrawableItem questStartNpcMarker = null;
            BaseDXDrawableItem questEndNpcMarker = null;
            BaseDXDrawableItem npcListPanel = null;
            BaseDXDrawableItem portalMarker = null;
            Dictionary<MinimapUI.DirectionArrow, BaseDXDrawableItem> directionMarkers = new Dictionary<MinimapUI.DirectionArrow, BaseDXDrawableItem>();

            WzSubProperty minimapSimpleModeProperty = uiWindow2Image?["MiniMapSimpleMode"] as WzSubProperty;
            WzSubProperty defaultHelperProperty = minimapSimpleModeProperty?["DefaultHelper"] as WzSubProperty;

            WzCanvasProperty userCanvas = defaultHelperProperty?["user"] as WzCanvasProperty;
            if (userCanvas != null)
            {
                System.Drawing.Bitmap userMarkerBitmap = userCanvas.GetLinkedWzCanvasBitmap();
                if (userMarkerBitmap != null)
                {
                    IDXObject dxObjUserMarker = new DXObject(userCanvas.GetCanvasOriginPosition(), userMarkerBitmap.ToTexture2DAndDispose(device), 0);
                    userMarker = new BaseDXDrawableItem(dxObjUserMarker, false)
                    {
                        Position = minimapImageOffset
                    };
                }
            }

            WzCanvasProperty iconNpcCanvas =
                defaultHelperProperty?["npc"] as WzCanvasProperty ??
                (bBigBang ? (WzCanvasProperty)minimapFrameProperty["iconNpc"]?["0"] : null);
            BaseDXDrawableItem animatedNpcMarker = bBigBang
                ? LoadAnimatedMinimapMarker(minimapFrameProperty["iconNpc"] as WzSubProperty, device, minimapImageOffset, 120)
                : null;
            if (animatedNpcMarker != null)
            {
                npcMarker = animatedNpcMarker;
            }
            else if (iconNpcCanvas != null)
            {
                System.Drawing.Bitmap npcMarkerBitmap = iconNpcCanvas.GetLinkedWzCanvasBitmap();
                if (npcMarkerBitmap != null)
                {
                    IDXObject dxObjNpcMarker = new DXObject(iconNpcCanvas.GetCanvasOriginPosition(), npcMarkerBitmap.ToTexture2DAndDispose(device), 0);
                    npcMarker = new BaseDXDrawableItem(dxObjNpcMarker, false)
                    {
                        Position = minimapImageOffset
                    };
                }
            }

            WzCanvasProperty questStartNpcCanvas = defaultHelperProperty?["startnpc"] as WzCanvasProperty;
            if (questStartNpcCanvas != null)
            {
                System.Drawing.Bitmap questStartNpcBitmap = questStartNpcCanvas.GetLinkedWzCanvasBitmap();
                if (questStartNpcBitmap != null)
                {
                    IDXObject dxObjQuestStartNpc = new DXObject(questStartNpcCanvas.GetCanvasOriginPosition(), questStartNpcBitmap.ToTexture2DAndDispose(device), 0);
                    questStartNpcMarker = new BaseDXDrawableItem(dxObjQuestStartNpc, false)
                    {
                        Position = minimapImageOffset
                    };
                }
            }

            WzCanvasProperty questEndNpcCanvas = defaultHelperProperty?["endnpc"] as WzCanvasProperty;
            if (questEndNpcCanvas != null)
            {
                System.Drawing.Bitmap questEndNpcBitmap = questEndNpcCanvas.GetLinkedWzCanvasBitmap();
                if (questEndNpcBitmap != null)
                {
                    IDXObject dxObjQuestEndNpc = new DXObject(questEndNpcCanvas.GetCanvasOriginPosition(), questEndNpcBitmap.ToTexture2DAndDispose(device), 0);
                    questEndNpcMarker = new BaseDXDrawableItem(dxObjQuestEndNpc, false)
                    {
                        Position = minimapImageOffset
                    };
                }
            }

            WzCanvasProperty portalCanvas = defaultHelperProperty?["portal"] as WzCanvasProperty;
            if (portalCanvas != null)
            {
                System.Drawing.Bitmap portalBitmap = portalCanvas.GetLinkedWzCanvasBitmap();
                if (portalBitmap != null)
                {
                    IDXObject dxObjPortalMarker = new DXObject(portalCanvas.GetCanvasOriginPosition(), portalBitmap.ToTexture2DAndDispose(device), 0);
                    portalMarker = new BaseDXDrawableItem(dxObjPortalMarker, false)
                    {
                        Position = minimapImageOffset
                    };
                }
            }

            var arrowCanvasMap = new Dictionary<MinimapUI.DirectionArrow, string>
            {
                { MinimapUI.DirectionArrow.NorthWest, "arrowupleft" },
                { MinimapUI.DirectionArrow.North, "arrowup" },
                { MinimapUI.DirectionArrow.NorthEast, "arrowupright" },
                { MinimapUI.DirectionArrow.West, "arrowleft" },
                { MinimapUI.DirectionArrow.East, "arrowright" },
                { MinimapUI.DirectionArrow.SouthWest, "arrowdownleft" },
                { MinimapUI.DirectionArrow.South, "arrowdown" },
                { MinimapUI.DirectionArrow.SouthEast, "arrowdownright" }
            };

            foreach (var arrowEntry in arrowCanvasMap)
            {
                BaseDXDrawableItem animatedArrow = null;
                if (bBigBang)
                {
                    animatedArrow = LoadAnimatedMinimapMarker(minimapFrameProperty["iconDirection"]?[ToLegacyArrowKey(arrowEntry.Key)] as WzSubProperty, device, minimapImageOffset, 120);
                }

                if (animatedArrow != null)
                {
                    directionMarkers[arrowEntry.Key] = animatedArrow;
                    continue;
                }

                WzCanvasProperty arrowCanvas = defaultHelperProperty?[arrowEntry.Value] as WzCanvasProperty;
                if (arrowCanvas == null)
                    continue;

                System.Drawing.Bitmap arrowBitmap = arrowCanvas.GetLinkedWzCanvasBitmap();
                if (arrowBitmap == null)
                    continue;

                IDXObject dxObjArrow = new DXObject(arrowCanvas.GetCanvasOriginPosition(), arrowBitmap.ToTexture2DAndDispose(device), 0);
                directionMarkers[arrowEntry.Key] = new BaseDXDrawableItem(dxObjArrow, false)
                {
                    Position = minimapImageOffset
                };
            }

            if (bBigBang)
            {
                WzSubProperty listNpcProperty = (WzSubProperty)minimapFrameProperty["ListNpc"];
                if (listNpcProperty != null)
                {
                    var npcRows = new List<string>();
                    var seenNpcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var npc in mapBoard.BoardItems.NPCs)
                    {
                        string npcName = npc?.NpcInfo?.StringName;
                        if (string.IsNullOrWhiteSpace(npcName) || !seenNpcNames.Add(npcName))
                            continue;

                        npcRows.Add(npcName.Trim());
                    }

                    if (npcRows.Count > 0)
                    {
                        HaUIStackPanel npcListContent = new HaUIStackPanel(HaUIStackOrientation.Vertical, new HaUIInfo()
                        {
                            MinWidth = 150,
                            Margins = new HaUIMargin() { Left = 6, Top = 6, Right = 6, Bottom = 6 }
                        });

                        foreach (string npcName in npcRows)
                        {
                            HaUIStackPanel npcRow = new HaUIStackPanel(HaUIStackOrientation.Horizontal, new HaUIInfo()
                            {
                                Margins = new HaUIMargin() { Left = 4, Top = 2, Right = 4, Bottom = 2 }
                            });

                            if (iconNpcCanvas != null)
                            {
                                System.Drawing.Bitmap npcIconBitmap = iconNpcCanvas.GetLinkedWzCanvasBitmap();
                                if (npcIconBitmap != null)
                                {
                                    npcRow.AddRenderable(new HaUIImage(new HaUIInfo()
                                    {
                                        Bitmap = npcIconBitmap,
                                        Margins = new HaUIMargin() { Right = 4 }
                                    }));
                                }
                            }

                            npcRow.AddRenderable(new HaUIText(npcName, color_foreGround, GLOBAL_FONT, MINIMAP_STREETNAME_TOOLTIP_FONTSIZE, UserScreenScaleFactor));
                            npcListContent.AddRenderable(npcRow);
                        }

                        System.Drawing.Bitmap listC = ((WzCanvasProperty)listNpcProperty["c"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listE = ((WzCanvasProperty)listNpcProperty["e"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listN = ((WzCanvasProperty)listNpcProperty["n"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listS = ((WzCanvasProperty)listNpcProperty["s"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listW = ((WzCanvasProperty)listNpcProperty["w"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listNe = ((WzCanvasProperty)listNpcProperty["ne"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listNw = ((WzCanvasProperty)listNpcProperty["nw"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listSe = ((WzCanvasProperty)listNpcProperty["se"])?.GetLinkedWzCanvasBitmap();
                        System.Drawing.Bitmap listSw = ((WzCanvasProperty)listNpcProperty["sw"])?.GetLinkedWzCanvasBitmap();

                        System.Drawing.Bitmap npcListBitmap = HaUIHelper.RenderAndMergeMinimapUIFrame(
                            npcListContent,
                            color_bgFill,
                            listNe,
                            listNw,
                            listSe,
                            listSw,
                            listE,
                            listW,
                            listN,
                            listS,
                            listC,
                            0);

                        IDXObject dxObjNpcList = new DXObject(0, 0, npcListBitmap.ToTexture2DAndDispose(device), 0);
                        npcListPanel = new BaseDXDrawableItem(dxObjNpcList, false)
                        {
                            Position = new Point(Math.Max(0, texturer_miniMap.Width - dxObjNpcList.Width), texturer_miniMap.Height + 4)
                        };
                    }
                }
            }

            MinimapUI minimapItem = new MinimapUI(dxObj_miniMap,
                new BaseDXDrawableItem(dxObj_miniMapPixel, false)
                {
                    Position = minimapImageOffset // map is on the center
                },
                new BaseDXDrawableItem(dxObj_miniMap_Minimised, false)
                {
                    Position = new Point(MAP_IMAGE_TEXT_PADDING, 0)
                },
                miniMapImage.Width,
                miniMapImage.Height,
                userMarker,
                npcMarker,
                questStartNpcMarker,
                questEndNpcMarker,
                npcListPanel,
                portalMarker,
                directionMarkers);

            minimapItem.Position = new Point(10, 10); // default position

            ////////////// Minimap buttons////////////////////
            // This must be in order.
            // >>> If aligning from the left to the right. Items at the left must be at the top of the code
            // >>> If aligning from the right to the left. Items at the right must be at the top of the code with its (x position - parent width).
            // TODO: probably a wrapper class in the future, such as HorizontalAlignment and VerticalAlignment, or Grid/ StackPanel
            WzBinaryProperty BtMouseClickSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseClick"];
            WzBinaryProperty BtMouseOverSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseOver"];

            if (bBigBang)
            {
                WzSubProperty BtNpc = (WzSubProperty)minimapFrameProperty["BtNpc"]; // npc button
                WzSubProperty BtMin = (WzSubProperty)minimapFrameProperty["BtMin"]; // mininise button
                WzSubProperty BtMax = (WzSubProperty)minimapFrameProperty["BtMax"]; // maximise button
                WzSubProperty BtBig = (WzSubProperty)minimapFrameProperty["BtBig"]; // big button
                WzSubProperty BtMap = (WzSubProperty)minimapFrameProperty["BtMap"]; // world button

                UIObject objUIBtMap = new UIObject(BtMap, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMap.X = texturer_miniMap.Width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)

                UIObject objUIBtBig = null;
                if (BtBig != null)
                {
                    objUIBtBig = new UIObject(BtBig, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                    objUIBtBig.X = objUIBtMap.X - objUIBtBig.CanvasSnapshotWidth; // render at the (width of minimap - obj width)*/
                }

                UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMax.X = objUIBtBig.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                UIObject objUIBtNpc = null;
                if (BtNpc != null)
                {
                    objUIBtNpc = new UIObject(BtNpc, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                    objUIBtNpc.X = objUIBtBig.X - objUIBtNpc.CanvasSnapshotWidth;
                    objUIBtMax.X = objUIBtNpc.X - objUIBtMax.CanvasSnapshotWidth;
                    objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth;
                    objUIBtNpc.SetVisible(false);
                }

                minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, objUIBtBig, objUIBtMap, objUIBtNpc);
            }
            else
            {
                WzSubProperty BtMin = (WzSubProperty)uiBasicImage["BtMin"]; // mininise button
                WzSubProperty BtMax = (WzSubProperty)uiBasicImage["BtMax"]; // maximise button
                WzSubProperty BtMap = (WzSubProperty)minimapFrameProperty["BtMap"]; // world button

                UIObject objUIBtMap = new UIObject(BtMap, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMap.X = texturer_miniMap.Width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)

                UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMax.X = objUIBtMap.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                // BaseClickableUIObject objUINpc = new BaseClickableUIObject(BtNpc, false, new Point(objUIBtMap.CanvasSnapshotWidth + objUIBtBig.CanvasSnapshotWidth + objUIBtMax.CanvasSnapshotWidth + objUIBtMin.CanvasSnapshotWidth, MAP_IMAGE_PADDING), device);

                minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, null, objUIBtMap);
            }
            return minimapItem;
        }

        private static string ToLegacyArrowKey(MinimapUI.DirectionArrow arrow)
        {
            return arrow switch
            {
                MinimapUI.DirectionArrow.NorthWest => "nw",
                MinimapUI.DirectionArrow.North => "n",
                MinimapUI.DirectionArrow.NorthEast => "ne",
                MinimapUI.DirectionArrow.West => "w",
                MinimapUI.DirectionArrow.East => "e",
                MinimapUI.DirectionArrow.SouthWest => "sw",
                MinimapUI.DirectionArrow.South => "s",
                MinimapUI.DirectionArrow.SouthEast => "se",
                _ => string.Empty
            };
        }

        private static BaseDXDrawableItem LoadAnimatedMinimapMarker(WzSubProperty sourceProperty, GraphicsDevice device, Point position, int fallbackDelay)
        {
            if (sourceProperty == null)
            {
                return null;
            }

            List<IDXObject> frames = new List<IDXObject>();
            for (int i = 0; ; i++)
            {
                if (sourceProperty[i.ToString()] is not WzCanvasProperty frameCanvas)
                {
                    break;
                }

                System.Drawing.Bitmap frameBitmap = frameCanvas.GetLinkedWzCanvasBitmap();
                if (frameBitmap == null)
                {
                    continue;
                }

                int delay = frameCanvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? fallbackDelay;
                frames.Add(new DXObject(frameCanvas.GetCanvasOriginPosition(), frameBitmap.ToTexture2DAndDispose(device), delay));
            }

            if (frames.Count == 0)
            {
                return null;
            }

            return new BaseDXDrawableItem(frames, false)
            {
                Position = position
            };
        }
        #endregion

        public static StatusBarPopupMenuWindow CreateStatusBarPopupMenuWindow(
            WzImage uiStatusBar2,
            WzImage basicImage,
            WzImage soundUIImage,
            GraphicsDevice device,
            string windowName,
            Point position)
        {
            if (uiStatusBar2 == null || device == null || string.IsNullOrWhiteSpace(windowName))
            {
                return null;
            }

            bool isMenu = string.Equals(windowName, MapSimulatorWindowNames.Menu, StringComparison.OrdinalIgnoreCase);
            WzSubProperty sourceProperty = uiStatusBar2["mainBar"]?[isMenu ? "Menu" : "System"] as WzSubProperty;
            if (sourceProperty == null)
            {
                return null;
            }

            string[] buttonNames = isMenu
                ? new[] { "BtItem", "BtEquip", "BtStat", "BtSkill", "BtCommunity", "BtQuest", "BtMSN", "BtRank", "BtEvent" }
                : new[] { "BtChannel", "BtKeySetting", "BtGameOption", "BtSystemOption", "BtGameQuit", "BtJoyPad", "BtOption" };

            WzBinaryProperty clickSound = soundUIImage?["BtMouseClick"] as WzBinaryProperty;
            WzBinaryProperty overSound = soundUIImage?["BtMouseOver"] as WzBinaryProperty;
            List<(string EntryName, UIObject Button)> buttons = new List<(string, UIObject)>();
            foreach (string buttonName in buttonNames)
            {
                WzSubProperty buttonProperty = sourceProperty[buttonName] as WzSubProperty;
                if (buttonProperty == null)
                {
                    continue;
                }

                UIObject button = new UIObject(buttonProperty, clickSound, overSound, false, Point.Zero, device);
                buttons.Add((buttonName, button));
            }

            if (buttons.Count == 0)
            {
                return null;
            }

            Texture2D frameTexture = CreateStatusBarPopupFrameTexture(sourceProperty["backgrnd"] as WzSubProperty, buttons.Count, device);
            if (frameTexture == null)
            {
                return null;
            }

            StatusBarPopupMenuWindow popupWindow = new StatusBarPopupMenuWindow(new DXObject(0, 0, frameTexture, 0), windowName, position);
            const int sidePadding = 8;
            const int topPadding = 5;

            int y = topPadding;
            foreach ((string entryName, UIObject button) in buttons)
            {
                button.X = sidePadding;
                button.Y = y;
                y += button.CanvasSnapshotHeight;
                popupWindow.AddEntry(entryName, button);
            }

            return popupWindow;
        }

        private static Texture2D CreateStatusBarPopupFrameTexture(WzSubProperty backgroundProperty, int buttonCount, GraphicsDevice device)
        {
            if (backgroundProperty == null || buttonCount <= 0)
            {
                return null;
            }

            System.Drawing.Bitmap top = ((WzCanvasProperty)backgroundProperty["0"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap middle = ((WzCanvasProperty)backgroundProperty["1"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap bottom = ((WzCanvasProperty)backgroundProperty["2"])?.GetLinkedWzCanvasBitmap();
            if (top == null || middle == null || bottom == null)
            {
                return null;
            }

            int middleHeight = Math.Max(0, (buttonCount * 25) - top.Height);
            int totalHeight = top.Height + middleHeight + bottom.Height;
            using (System.Drawing.Bitmap composed = new System.Drawing.Bitmap(top.Width, totalHeight))
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(composed))
            {
                graphics.DrawImage(top, 0, 0);
                int y = top.Height;
                while (y < top.Height + middleHeight)
                {
                    graphics.DrawImage(middle, 0, y, top.Width, Math.Min(middle.Height, (top.Height + middleHeight) - y));
                    y += middle.Height;
                }

                graphics.DrawImage(bottom, 0, totalHeight - bottom.Height);
                graphics.Flush();
                return composed.ToTexture2DAndDispose(device);
            }
        }

        #region MouseCursor
        /// <summary>
        /// Creates mouse cursor item from UI.wz/Basic.img/Cursor
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static MouseCursorItem CreateMouseCursorFromProperty(
            TexturePool texturePool, WzImageProperty source, int x, int y,
            GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool flip)
        {
            WzSubProperty cursorCanvas = (WzSubProperty)source?["0"]; // normal
            WzSubProperty cursorClickable = (WzSubProperty)source?["1"]; // click-able item
            WzSubProperty cursorClickableOmok = (WzSubProperty)source?["2"]; // click-able item
            WzSubProperty cursorClickableHouse = (WzSubProperty)source?["3"]; // click-able item
            WzSubProperty cursorClickable2 = (WzSubProperty)source?["4"]; // click-able item
            WzSubProperty cursorPickable = (WzSubProperty)source?["5"]; // pickable inventory
            WzSubProperty cursorGift = (WzSubProperty)source?["6"]; //
            WzSubProperty cursorVerticalScrollable = (WzSubProperty)source?["7"]; //
            WzSubProperty cursorHorizontalScrollable = (WzSubProperty)source?["8"]; //
            WzSubProperty cursorVerticalScrollable2 = (WzSubProperty)source?["9"]; //
            WzSubProperty cursorHorizontalScrollable2 = (WzSubProperty)source?["10"]; //
            WzSubProperty cursorPickable2 = (WzSubProperty)source?["11"]; // pickable inventory
            WzSubProperty cursorHold = (WzSubProperty)source?["12"]; // pickable inventory
            WzSubProperty cursorForbidden = (WzSubProperty)source?["13"]; // forbidden hand cursor
            WzSubProperty cursorBusy = (WzSubProperty)source?["16"]; // busy / pending cursor

            List<IDXObject> frames = MapSimulatorLoader.LoadFrames(texturePool, cursorCanvas, x, y, device, usedProps);

            // Mouse hold state (style 12 - may not exist in beta MapleStory)
            BaseDXDrawableItem holdState = null;
            if (cursorHold != null)
            {
                holdState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorHold, 0, 0, new Point(0, 0), device, usedProps, false);
            }

            // Mouse clicked item state
            BaseDXDrawableItem clickableButtonState = null;
            if (cursorClickable != null)
            {
                clickableButtonState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorClickable, 0, 0, new Point(0, 0), device, usedProps, false);
            }

            // NPC hover cursor state (uses style 4 - alternate clickable, or fallback to style 1)
            BaseDXDrawableItem npcHoverState = null;
            if (cursorClickable2 != null)
            {
                npcHoverState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorClickable2, 0, 0, new Point(0, 0), device, usedProps, false);
            }
            else if (cursorClickable != null)
            {
                npcHoverState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorClickable, 0, 0, new Point(0, 0), device, usedProps, false);
            }

            BaseDXDrawableItem forbiddenState = null;
            if (cursorForbidden != null)
            {
                forbiddenState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorForbidden, 0, 0, new Point(0, 0), device, usedProps, false);
            }

            BaseDXDrawableItem busyState = null;
            if (cursorBusy != null)
            {
                busyState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorBusy, 0, 0, new Point(0, 0), device, usedProps, false);
            }

            return new MouseCursorItem(frames, holdState, clickableButtonState, npcHoverState, holdState, forbiddenState, busyState);
        }
        #endregion
    }
}
