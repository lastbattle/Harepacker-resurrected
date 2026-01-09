using HaCreator.MapEditor;
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
            WzImage uiStatusBar, WzImage uiStatusBar2, Board mapBoard, GraphicsDevice device,
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

                    stackPanel_charStats.AddRenderable(new HaUIImage(new HaUIInfo() { Bitmap = bitmap_lvBacktrnd }));

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
                                    hpGaugeTexture = hpBitmap.ToTexture2D(device);
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
                                    mpGaugeTexture = mpBitmap.ToTexture2D(device);
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
                                    expGaugeTexture = expBitmap.ToTexture2D(device);
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

                    Texture2D texture_chatUI = grid_chat.Render().ToTexture2D(device);
                    IDXObject dxObj_chatUI = new DXObject(UI_PADDING_PX, (int)(renderParams.RenderHeight / renderParams.RenderObjectScaling) - grid_chat.GetSize().Height - 36, texture_chatUI, 0);

                    // Scroll up+down, Chat, report/ claim, notice, stat, quest, inventory, equip, skill, key set
                    System.Drawing.Bitmap bitmap_lvNumber1 = ((WzCanvasProperty)mainBarProperties?["lvNumber/1"])?.GetLinkedWzCanvasBitmap();

                    // chat
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

                    Texture2D texture_backgrnd = grid.Render().ToTexture2D(device);

                    IDXObject dxObj_backgrnd = new DXObject(0, (int)(renderParams.RenderHeight / renderParams.RenderObjectScaling) - grid.GetSize().Height, texture_backgrnd, 0);
                    StatusBarUI statusBar = new StatusBarUI(dxObj_backgrnd, obj_Ui_BtCashShop, obj_Ui_BtMTS, obj_Ui_BtMenu, obj_Ui_BtSystem, obj_Ui_BtChannel,
                        new Point(dxObj_backgrnd.X, dxObj_backgrnd.Y),
                        new List<UIObject> { });
                    statusBar.InitializeButtons();

                    // Set gauge textures if loaded from WZ files
                    if (hpGaugeTexture != null || mpGaugeTexture != null || expGaugeTexture != null) {
                        statusBar.SetGaugeTextures(hpGaugeTexture, mpGaugeTexture, expGaugeTexture);
                    }

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

                        // Load digits 0-9 with origins
                        for (int i = 0; i < 10; i++) {
                            WzCanvasProperty digitCanvas = gaugeNumberProp[i.ToString()] as WzCanvasProperty;
                            if (digitCanvas != null) {
                                var bitmap = digitCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    digitTextures[i] = bitmap.ToTexture2D(device);
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
                                    bracketLeftTexture = bitmap.ToTexture2D(device);
                                    bracketLeftOrigin = GetCanvasOrigin(lbCanvas);
                                }
                            }

                            // Right bracket ] - origin Y=1 for proper alignment
                            WzCanvasProperty rbCanvas = gaugeNumberProp["]"] as WzCanvasProperty;
                            if (rbCanvas != null) {
                                var bitmap = rbCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    bracketRightTexture = bitmap.ToTexture2D(device);
                                    bracketRightOrigin = GetCanvasOrigin(rbCanvas);
                                }
                            }

                            // Slash \ (backslash used as divider) - origin Y=1 for proper alignment
                            WzCanvasProperty slashCanvas = gaugeNumberProp["\\"] as WzCanvasProperty;
                            if (slashCanvas != null) {
                                var bitmap = slashCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    slashTexture = bitmap.ToTexture2D(device);
                                    slashOrigin = GetCanvasOrigin(slashCanvas);
                                }
                            }

                            // Percent % - origin Y=0
                            WzCanvasProperty percentCanvas = gaugeNumberProp["%"] as WzCanvasProperty;
                            if (percentCanvas != null) {
                                var bitmap = percentCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    percentTexture = bitmap.ToTexture2D(device);
                                    percentOrigin = GetCanvasOrigin(percentCanvas);
                                }
                            }

                            // Dot . - origin Y=-6 (sits at bottom of line)
                            WzCanvasProperty dotCanvas = gaugeNumberProp["."] as WzCanvasProperty;
                            if (dotCanvas != null) {
                                var bitmap = dotCanvas.GetLinkedWzCanvasBitmap();
                                if (bitmap != null) {
                                    dotTexture = bitmap.ToTexture2D(device);
                                    dotOrigin = GetCanvasOrigin(dotCanvas);
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

                    StatusBarChatUI chatUI = new StatusBarChatUI(dxObj_chatUI, new Point(dxObj_chatUI.X, dxObj_chatUI.Y),
                         new List<UIObject> {
                             obj_Ui_chatOpen,
                             obj_Ui_scrollUp, obj_Ui_scrollDown,
                             obj_Ui_BtChat, obj_Ui_BtClaim,
                             obj_Ui_BtCharacter, obj_Ui_BtStat, obj_Ui_BtQuest, obj_Ui_BtInven, obj_Ui_BtEquip, obj_Ui_BtSkill, obj_Ui_BtKeysetting
                         }
                        );
                    chatUI.InitializeButtons();

                    return new Tuple<StatusBarUI, StatusBarChatUI>(statusBar, chatUI);
                }
            }
            return null;
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

            Texture2D texturer_miniMapMinimised = finalMininisedMinimapBitmap.ToTexture2D(device);
            Texture2D texturer_miniMap = finalFullMinimapBitmap.ToTexture2D(device);

            // Dots pixel
            System.Drawing.Bitmap bmp_DotPixel = new System.Drawing.Bitmap(2, 4);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_DotPixel))
            {
                graphics.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Yellow), new System.Drawing.RectangleF(0, 0, bmp_DotPixel.Width, bmp_DotPixel.Height));
                graphics.Flush();
            }
            IDXObject dxObj_miniMapPixel = new DXObject(0, n.Height, bmp_DotPixel.ToTexture2D(device), 0);

            // Map
            IDXObject dxObj_miniMap_Minimised = new DXObject(0, 0, texturer_miniMapMinimised, 0);
            IDXObject dxObj_miniMap = new DXObject(0, 0, texturer_miniMap, 0); // starting position of the minimap in the map

            // need to calculate how much x position, where the map is shifted to the center by HorizontalAlignment
            // to compensate for in the character dot position indicator
            HaUISize fullMiniMapStackPanelSize = fullMiniMapStackPanel.GetSize();
            int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(fullMiniMapStackPanelSize.Width, minimapUiImage.GetInfo().Bitmap.Width, minimapUiGrid.GetInfo().HorizontalAlignment);

            MinimapUI minimapItem = new MinimapUI(dxObj_miniMap,
                new BaseDXDrawableItem(dxObj_miniMapPixel, false)
                {
                    Position = new Point(MAP_IMAGE_TEXT_PADDING + alignmentXOffset, 0) // map is on the center
                },
                new BaseDXDrawableItem(dxObj_miniMap_Minimised, false)
                {
                    Position = new Point(MAP_IMAGE_TEXT_PADDING, 0)
                },
                texturer_miniMap.Width, texturer_miniMap.Height);

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

                // BaseClickableUIObject objUINpc = new BaseClickableUIObject(BtNpc, false, new Point(objUIBtMap.CanvasSnapshotWidth + objUIBtBig.CanvasSnapshotWidth + objUIBtMax.CanvasSnapshotWidth + objUIBtMin.CanvasSnapshotWidth, MAP_IMAGE_PADDING), device);

                minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, objUIBtBig, objUIBtMap);
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
        #endregion

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
            GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
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

            List<IDXObject> frames = MapSimulatorLoader.LoadFrames(texturePool, cursorCanvas, x, y, device, ref usedProps);

            // Mouse hold state
            BaseDXDrawableItem holdState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorHold, 0, 0, new Point(0, 0), device, ref usedProps, false);

            // Mouse clicked item state
            BaseDXDrawableItem clickableButtonState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, cursorClickable, 0, 0, new Point(0, 0), device, ref usedProps, false);

            // NPC hover cursor state (uses style 4 - alternate clickable, or fallback to style 1)
            WzSubProperty npcHoverSource = cursorClickable2 ?? cursorClickable;
            BaseDXDrawableItem npcHoverState = MapSimulatorLoader.CreateMapItemFromProperty(texturePool, npcHoverSource, 0, 0, new Point(0, 0), device, ref usedProps, false);

            return new MouseCursorItem(frames, holdState, clickableButtonState, npcHoverState);
        }
        #endregion
    }
}
