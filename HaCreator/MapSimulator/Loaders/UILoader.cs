using HaCreator.MapEditor;
using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character.Skills;
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        private static readonly Point DefaultMinimapWindowPosition = new Point(10, 10);
        private static readonly ConcurrentDictionary<string, Tuple<StatusBarUI, StatusBarChatUI>> _statusBarCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, MinimapUI> _minimapCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, Dictionary<string, Texture2D>> _buffIconTextureCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, BuffIconCatalogEntry>> _buffIconCatalogCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, Texture2D[]> _skillTooltipTextureCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, Dictionary<string, StatusBarKeyDownBarTextures>> _keyDownBarTextureCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, StatusBarWarningAnimation> _warningAnimationCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, (Dictionary<MapSimulatorChatTargetType, Texture2D> Textures, Dictionary<MapSimulatorChatTargetType, Point> Origins)> _chatTargetTextureCache = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, StatusBarChatUI.StatusBarPointNotificationAnimation> _pointNotificationAnimationCache = new(StringComparer.Ordinal);
        private static Point _sharedMinimapWindowPosition = DefaultMinimapWindowPosition;

        private static string GetDeviceCachePrefix(GraphicsDevice device)
        {
            return device == null
                ? "nodevice"
                : $"device:{RuntimeHelpers.GetHashCode(device)}";
        }

        private static string BuildStatusBarCacheKey(GraphicsDevice device, RenderParameters renderParams, bool isBigBang)
        {
            return $"{GetDeviceCachePrefix(device)}|statusbar|bb:{isBigBang}|rw:{renderParams.RenderWidth}|rh:{renderParams.RenderHeight}|scale:{renderParams.RenderObjectScaling}";
        }

        private static string BuildMinimapCacheKey(GraphicsDevice device, Board mapBoard, float userScreenScaleFactor, bool isBigBang)
        {
            int mapId = mapBoard?.MapInfo?.id ?? 0;
            bool zeroMirror = mapBoard?.MapInfo?.zeroSideOnly ?? false;
            int miniMapWidth = mapBoard?.MiniMap?.Width ?? 0;
            int miniMapHeight = mapBoard?.MiniMap?.Height ?? 0;
            return $"{GetDeviceCachePrefix(device)}|minimap|map:{mapId}|bb:{isBigBang}|zero:{zeroMirror}|scale:{userScreenScaleFactor}|w:{miniMapWidth}|h:{miniMapHeight}";
        }

        private static string BuildWarningAnimationCacheKey(GraphicsDevice device, WzSubProperty warningProperty)
        {
            return $"{GetDeviceCachePrefix(device)}|warn:{warningProperty?.FullPath ?? string.Empty}";
        }

        private static string BuildChatTargetTextureCacheKey(GraphicsDevice device, WzSubProperty chatTargetProperty)
        {
            return $"{GetDeviceCachePrefix(device)}|chatTarget:{chatTargetProperty?.FullPath ?? string.Empty}";
        }

        private static string BuildPointNotificationCacheKey(GraphicsDevice device, WzSubProperty notificationProperty)
        {
            return $"{GetDeviceCachePrefix(device)}|pointNotify:{notificationProperty?.FullPath ?? string.Empty}";
        }

        private static void ApplySharedMinimapWindowPosition(MinimapUI minimap)
        {
            if (minimap == null)
            {
                return;
            }

            minimap.WindowPositionChanged = position => _sharedMinimapWindowPosition = position;
            minimap.SetWindowPosition(_sharedMinimapWindowPosition);
        }

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
            string statusBarCacheKey = BuildStatusBarCacheKey(device, renderParams, bBigBang);
            if (_statusBarCache.TryGetValue(statusBarCacheKey, out Tuple<StatusBarUI, StatusBarChatUI> cachedStatusBar))
            {
                return cachedStatusBar;
            }

            // Pre-big bang maplestory status bar
            if (bBigBang)
            {
                WzSubProperty mainBarProperties = (uiStatusBar2?["mainBar"] as WzSubProperty);
                if (mainBarProperties != null)
                {
                    WzCanvasProperty backgrndCanvas = ResolveBigBangStatusBarBackgroundCanvas(mainBarProperties, renderParams);
                    WzCanvasProperty lvBacktrndCanvas = mainBarProperties?["lvBacktrnd"] as WzCanvasProperty;
                    WzCanvasProperty lvCoverCanvas = mainBarProperties?["lvCover"] as WzCanvasProperty;
                    WzCanvasProperty gaugeBackgrdCanvas = mainBarProperties?["gaugeBackgrd"] as WzCanvasProperty;
                    WzCanvasProperty gaugeCoverCanvas = mainBarProperties?["gaugeCover"] as WzCanvasProperty;

                    System.Drawing.Bitmap backgrnd = LoadCanvasBitmap(backgrndCanvas);

                    const int UI_PADDING_PX = 2;
                    Point mainBarFrameOrigin = GetCanvasOrigin(backgrndCanvas);

                    System.Drawing.Bitmap bitmap_lvBacktrnd = LoadCanvasBitmap(lvBacktrndCanvas);
                    System.Drawing.Bitmap bitmap_lvCover = LoadCanvasBitmap(lvCoverCanvas);

                    // Draw HP, MP, EXP area
                    System.Drawing.Bitmap bitmap_gaugeBackgrd = LoadCanvasBitmap(gaugeBackgrdCanvas);
                    System.Drawing.Bitmap bitmap_gaugeCover = LoadCanvasBitmap(gaugeCoverCanvas);
                    System.Drawing.Bitmap composedMainBar = ComposeBigBangStatusBarFrame(
                        mainBarProperties,
                        backgrndCanvas,
                        backgrnd,
                        bitmap_lvBacktrnd,
                        bitmap_lvCover,
                        bitmap_gaugeBackgrd,
                        bitmap_gaugeCover);

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
                                var hpBitmap = LoadCanvasBitmap(hpCanvas);
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
                                var mpBitmap = LoadCanvasBitmap(mpCanvas);
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
                                var expBitmap = LoadCanvasBitmap(expCanvas);
                                if (expBitmap != null)
                                {
                                    expGaugeTexture = expBitmap.ToTexture2DAndDispose(device);
                                }
                            }
                        }
                    }

                    // Cash shop, MTS, menu, system, channel UI
                    WzBinaryProperty binaryProp_BtMouseClickSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseClick"];
                    WzBinaryProperty binaryProp_BtMouseOverSoundProperty = (WzBinaryProperty)soundUIImage["BtMouseOver"];

                    WzSubProperty subProperty_BtCashShop = (WzSubProperty)mainBarProperties?["BtCashShop"]; // cash shop
                    UIObject obj_Ui_BtCashShop = new UIObject(subProperty_BtCashShop, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device);
                    PositionStatusBarButton(obj_Ui_BtCashShop, subProperty_BtCashShop, mainBarFrameOrigin);

                    WzSubProperty subProperty_BtMTS = (WzSubProperty)mainBarProperties?["BtMTS"]; // MTS
                    if (subProperty_BtMTS == null)
                        subProperty_BtMTS = (WzSubProperty)mainBarProperties?["BtNPT"]; // MapleStory Japan uses a different name
                    UIObject obj_Ui_BtMTS = null;
                    if (subProperty_BtMTS != null)
                    {
                        obj_Ui_BtMTS = new UIObject(subProperty_BtMTS, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                            false,
                            new Point(0, 0), device);
                        PositionStatusBarButton(obj_Ui_BtMTS, subProperty_BtMTS, mainBarFrameOrigin);
                    }
                    WzSubProperty subProperty_BtMenu = (WzSubProperty)mainBarProperties?["BtMenu"]; // Menu
                    UIObject obj_Ui_BtMenu = new UIObject(subProperty_BtMenu, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device);
                    PositionStatusBarButton(obj_Ui_BtMenu, subProperty_BtMenu, mainBarFrameOrigin);

                    WzSubProperty subProperty_BtSystem = (WzSubProperty)mainBarProperties?["BtSystem"]; // System
                    UIObject obj_Ui_BtSystem = new UIObject(subProperty_BtSystem, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device);
                    PositionStatusBarButton(obj_Ui_BtSystem, subProperty_BtSystem, mainBarFrameOrigin);

                    WzSubProperty subProperty_BtChannel = (WzSubProperty)mainBarProperties?["BtChannel"]; // System
                    UIObject obj_Ui_BtChannel = new UIObject(subProperty_BtChannel, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device);
                    PositionStatusBarButton(obj_Ui_BtChannel, subProperty_BtChannel, mainBarFrameOrigin);


                    // Draw Chat UI
                    WzCanvasProperty chatSpaceCanvas = mainBarProperties?["chatSpace"] as WzCanvasProperty;
                    WzCanvasProperty chatSpace2Canvas = mainBarProperties?["chatSpace2"] as WzCanvasProperty;
                    WzCanvasProperty chatCoverCanvas = mainBarProperties?["chatCover"] as WzCanvasProperty;
                    WzCanvasProperty noticeCanvas = mainBarProperties?["notice"] as WzCanvasProperty;
                    WzCanvasProperty chatEnterCanvas = mainBarProperties?["chatEnter"] as WzCanvasProperty;

                    System.Drawing.Bitmap bitmap_chatSpace = LoadCanvasBitmap(chatSpaceCanvas);
                    System.Drawing.Bitmap bitmap_chatSpace2 = LoadCanvasBitmap(chatSpace2Canvas);
                    System.Drawing.Bitmap bitmap_chatCover = LoadCanvasBitmap(chatCoverCanvas);
                    System.Drawing.Bitmap bitmap_notice = LoadCanvasBitmap(noticeCanvas);

                    Point chatFrameAnchorOrigin = ResolveBigBangChatFrameAnchorOrigin(chatSpace2Canvas, chatSpaceCanvas);
                    System.Drawing.Bitmap composedChatBar = ComposeBigBangStatusBarChatFrame(
                        chatFrameAnchorOrigin,
                        chatSpaceCanvas,
                        bitmap_chatSpace,
                        chatSpace2Canvas,
                        bitmap_chatSpace2,
                        chatCoverCanvas,
                        bitmap_chatCover,
                        noticeCanvas,
                        bitmap_notice);

                    int composedChatBarHeight = composedChatBar.Height;
                    Texture2D texture_chatUI = composedChatBar.ToTexture2DAndDispose(device);
                    IDXObject dxObj_chatUI = new DXObject(
                        UI_PADDING_PX,
                        (int)(renderParams.RenderHeight / renderParams.RenderObjectScaling) - composedChatBarHeight - 36,
                        texture_chatUI,
                        0);

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
                    PositionStatusBarButton(obj_Ui_chatTarget, subProperty_chatTargetBase, chatFrameAnchorOrigin);

                    WzSubProperty subProperty_chatOpen = (WzSubProperty)mainBarProperties?["chatOpen"];
                    WzSubProperty subProperty_chatClose = (WzSubProperty)mainBarProperties?["chatClose"];
                    UIObject obj_Ui_chatOpen = new UIObject(subProperty_chatOpen, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_chatOpen, subProperty_chatOpen, chatFrameAnchorOrigin);
                    UIObject obj_Ui_chatClose = new UIObject(subProperty_chatClose, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_chatClose, subProperty_chatClose, chatFrameAnchorOrigin);
                    obj_Ui_chatClose.SetVisible(false);

                    // chat scroll up/ down
                    WzSubProperty subProperty_scrollUp = (WzSubProperty)mainBarProperties?["scrollUp"];
                    WzSubProperty subProperty_scrollDown = (WzSubProperty)mainBarProperties?["scrollDown"];
                    UIObject obj_Ui_scrollUp = new UIObject(subProperty_scrollUp, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_scrollUp, subProperty_scrollUp, chatFrameAnchorOrigin);
                    UIObject obj_Ui_scrollDown = new UIObject(subProperty_scrollDown, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_scrollDown, subProperty_scrollDown, chatFrameAnchorOrigin);

                    // chat
                    WzSubProperty subProperty_BtChat = (WzSubProperty)mainBarProperties?["BtChat"];
                    UIObject obj_Ui_BtChat = new UIObject(subProperty_BtChat, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtChat, subProperty_BtChat, chatFrameAnchorOrigin);

                    // report
                    WzSubProperty subProperty_BtClaim = (WzSubProperty)mainBarProperties?["BtClaim"]; // report
                    UIObject obj_Ui_BtClaim = new UIObject(subProperty_BtClaim, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtClaim, subProperty_BtClaim, chatFrameAnchorOrigin);

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
                    PositionStatusBarButton(obj_Ui_BtCharacter, subProperty_BtCharacter, chatFrameAnchorOrigin);

                    // stat
                    WzSubProperty subProperty_BtStat = (WzSubProperty)mainBarProperties?["BtStat"];
                    UIObject obj_Ui_BtStat = new UIObject(subProperty_BtStat, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtStat, subProperty_BtStat, chatFrameAnchorOrigin);

                    // quest
                    WzSubProperty subProperty_BtQuest = (WzSubProperty)mainBarProperties?["BtQuest"];
                    UIObject obj_Ui_BtQuest = new UIObject(subProperty_BtQuest, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtQuest, subProperty_BtQuest, chatFrameAnchorOrigin);

                    // inventory
                    WzSubProperty subProperty_BtInven = (WzSubProperty)mainBarProperties?["BtInven"];
                    UIObject obj_Ui_BtInven = new UIObject(subProperty_BtInven, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtInven, subProperty_BtInven, chatFrameAnchorOrigin);

                    // equipment
                    WzSubProperty subProperty_BtEquip = (WzSubProperty)mainBarProperties?["BtEquip"];
                    UIObject obj_Ui_BtEquip = new UIObject(subProperty_BtEquip, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtEquip, subProperty_BtEquip, chatFrameAnchorOrigin);

                    // skill
                    WzSubProperty subProperty_BtSkill = (WzSubProperty)mainBarProperties?["BtSkill"];
                    UIObject obj_Ui_BtSkill = new UIObject(subProperty_BtSkill, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtSkill, subProperty_BtSkill, chatFrameAnchorOrigin);

                    // key setting
                    WzSubProperty subProperty_BtKeysetting = (WzSubProperty)mainBarProperties?["BtKeysetting"];
                    UIObject obj_Ui_BtKeysetting = new UIObject(subProperty_BtKeysetting, binaryProp_BtMouseClickSoundProperty, binaryProp_BtMouseOverSoundProperty,
                        false,
                        new Point(0, 0), device)
                    {
                    };
                    PositionStatusBarButton(obj_Ui_BtKeysetting, subProperty_BtKeysetting, chatFrameAnchorOrigin);

                    backgrnd?.Dispose();
                    bitmap_lvBacktrnd?.Dispose();
                    bitmap_lvCover?.Dispose();
                    bitmap_gaugeBackgrd?.Dispose();
                    bitmap_gaugeCover?.Dispose();
                    bitmap_chatSpace?.Dispose();
                    bitmap_chatSpace2?.Dispose();
                    bitmap_chatCover?.Dispose();
                    bitmap_notice?.Dispose();

                    int composedMainBarHeight = composedMainBar.Height;
                    Texture2D texture_backgrnd = composedMainBar.ToTexture2DAndDispose(device);
                    IDXObject dxObj_backgrnd = new DXObject(
                        0,
                        (int)(renderParams.RenderHeight / renderParams.RenderObjectScaling) - composedMainBarHeight,
                        texture_backgrnd,
                        0);
                    StatusBarUI statusBar = new StatusBarUI(dxObj_backgrnd, obj_Ui_BtCashShop, obj_Ui_BtMTS, obj_Ui_BtMenu, obj_Ui_BtSystem, obj_Ui_BtChannel,
                        new Point(dxObj_backgrnd.X, dxObj_backgrnd.Y),
                        new List<UIObject> { });
                    statusBar.InitializeButtons();
                    Point leftBaseOffset = ResolveCanvasPosition(mainBarFrameOrigin, lvBacktrndCanvas);
                    Point gaugeBaseOffset = ResolveCanvasPosition(mainBarFrameOrigin, gaugeBackgrdCanvas);
                    statusBar.SetLayoutMetrics(
                        leftBaseOffset,
                        gaugeBaseOffset);
                    statusBar.SetLeftClusterWidth(ResolveBigBangStatusBarClusterWidth(
                        mainBarFrameOrigin,
                        leftBaseOffset,
                        (lvBacktrndCanvas, bitmap_lvBacktrnd),
                        (lvCoverCanvas, bitmap_lvCover)));
                    statusBar.SetGaugeTextAnchors(
                        new Vector2(163, 4),
                        new Vector2(332, 4),
                        new Vector2(332, 20));

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
                             obj_Ui_chatClose,
                             obj_Ui_scrollUp, obj_Ui_scrollDown,
                             obj_Ui_BtChat, obj_Ui_BtClaim,
                             obj_Ui_MemoIcon,
                             obj_Ui_BtCharacter, obj_Ui_BtStat, obj_Ui_BtQuest, obj_Ui_BtInven, obj_Ui_BtEquip, obj_Ui_BtSkill, obj_Ui_BtKeysetting
                           }
                        );
                    chatUI.InitializeButtons();
                    Texture2D chatEnterTexture = LoadCanvasTexture(chatEnterCanvas, device);
                    chatUI.SetChatEnterTexture(chatEnterTexture);
                    WzImage uiWindow2DialogImage = Program.FindImage("UI", "UIWindow2.img");
                    (Dictionary<MapSimulatorChatTargetType, Texture2D> chatTargetTextures,
                        Dictionary<MapSimulatorChatTargetType, Point> chatTargetOrigins) =
                        LoadChatTargetTextures(subProperty_chatTarget, device);
                    chatUI.SetChatTargetTextures(chatTargetTextures, chatTargetOrigins);
                    chatUI.SetWhisperPickerTextures(
                        LoadCanvasTexture(uiWindow2DialogImage?["UtilDlgEx"]?["list5"] as WzCanvasProperty, device),
                        LoadCanvasTexture(uiWindow2DialogImage?["UtilDlgEx"]?["list4"] as WzCanvasProperty, device));
                    Vector2 chatTargetLabelPos = ResolveCanvasPosition(chatFrameAnchorOrigin, subProperty_chatTarget?["all"] as WzCanvasProperty).ToVector2();
                    Vector2 chatEnterPos = ResolveCanvasPosition(chatFrameAnchorOrigin, chatEnterCanvas).ToVector2();
                    Rectangle chatEnterBounds = ResolveCanvasBounds(chatFrameAnchorOrigin, chatEnterCanvas);
                    Rectangle chatSpace2Bounds = ResolveCanvasBounds(chatFrameAnchorOrigin, chatSpace2Canvas);
                    Vector2 chatTargetButtonPos = ResolveCanvasPosition(
                        chatFrameAnchorOrigin,
                        subProperty_chatTargetBase?["normal"]?["0"] as WzCanvasProperty).ToVector2();
                    Vector2 chatInputPos = new Vector2(
                        chatTargetButtonPos.X + obj_Ui_chatTarget.CanvasSnapshotWidth + 4,
                        chatEnterPos.Y);
                    Vector2 chatLogTextPos = new Vector2(ResolveCanvasPosition(chatFrameAnchorOrigin, chatSpace2Canvas).X + 4, -16);
                    int chatLogRightEdge = Math.Min(
                        chatEnterBounds.Right > 0 ? chatEnterBounds.Right : int.MaxValue,
                        chatSpace2Bounds.Right > 0 ? chatSpace2Bounds.Right : int.MaxValue);
                    if (chatLogRightEdge == int.MaxValue)
                    {
                        chatLogRightEdge = (int)MathF.Ceiling(chatLogTextPos.X) + Math.Max(1, (chatEnterTexture?.Width ?? 457) - 5);
                    }

                    int chatLogWidth = Math.Max(
                        1,
                        chatLogRightEdge - (int)MathF.Floor(chatLogTextPos.X) - 1);
                    Rectangle chatInteractionBounds = ResolveChatInteractionBounds(
                        chatLogTextPos,
                        chatLogWidth,
                        chatEnterBounds,
                        chatSpace2Bounds,
                        chatEnterTexture?.Height ?? 21);
                    chatUI.SetLayoutMetrics(
                        chatFrameAnchorOrigin,
                        chatTargetLabelPos,
                        chatEnterPos,
                        chatInputPos,
                        chatLogTextPos,
                        chatLogWidth,
                        chatInteractionBounds);
                    chatUI.SetPointNotificationAnimations(
                        LoadPointNotificationAnimation(mainBarProperties?["ApNotify"] as WzSubProperty, device),
                        LoadPointNotificationAnimation(mainBarProperties?["SpNotify"] as WzSubProperty, device));
                    chatUI.BindControls(obj_Ui_chatTarget, obj_Ui_chatOpen, obj_Ui_chatClose, obj_Ui_scrollUp, obj_Ui_scrollDown, obj_Ui_BtCharacter, obj_Ui_MemoIcon);

                    var result = new Tuple<StatusBarUI, StatusBarChatUI>(statusBar, chatUI);
                    _statusBarCache[statusBarCacheKey] = result;
                    return result;
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
                    System.Drawing.Bitmap backgrnd = LoadCanvasBitmap((WzCanvasProperty)baseProperties?["backgrnd"]);

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
                            var barBitmap = LoadCanvasBitmap(barCanvas);
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
                    var result = new Tuple<StatusBarUI, StatusBarChatUI>(statusBar, null);
                    _statusBarCache[statusBarCacheKey] = result;
                    return result;
                }
            }
            return null;
        }

        private static Dictionary<string, Texture2D> LoadBuffIconTextures(WzImage uiBuffIcon, GraphicsDevice device)
        {
            if (uiBuffIcon == null)
            {
                return new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            }

            if (device == null)
            {
                return new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            }

            string cacheKey = $"{GetDeviceCachePrefix(device)}|buffIcons:{uiBuffIcon.Name}";
            if (_buffIconTextureCache.TryGetValue(cacheKey, out Dictionary<string, Texture2D> cachedTextures))
            {
                return cachedTextures;
            }

            var buffIconTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            LoadBuffIconsRecursive(buffIconTextures, uiBuffIcon, device, string.Empty);
            _buffIconTextureCache[cacheKey] = buffIconTextures;
            return buffIconTextures;
        }

        public static IReadOnlyDictionary<string, BuffIconCatalogEntry> LoadBuffIconCatalogEntries(WzImage uiBuffIcon)
        {
            if (uiBuffIcon == null)
            {
                return new Dictionary<string, BuffIconCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            }

            string cacheKey = $"buffCatalog:{uiBuffIcon.Name}";
            if (_buffIconCatalogCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, BuffIconCatalogEntry> cachedCatalogEntries))
            {
                return cachedCatalogEntries;
            }

            var catalogEntries = new Dictionary<string, BuffIconCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            int sortOrder = 0;
            LoadBuffIconCatalogEntriesRecursive(catalogEntries, uiBuffIcon, string.Empty, ref sortOrder);
            _buffIconCatalogCache[cacheKey] = catalogEntries;
            return catalogEntries;
        }

        private static Texture2D[] LoadSkillTooltipTextures(GraphicsDevice device)
        {
            if (device == null)
            {
                return new Texture2D[3];
            }

            string cacheKey = $"{GetDeviceCachePrefix(device)}|skillTooltip";
            if (_skillTooltipTextureCache.TryGetValue(cacheKey, out Texture2D[] cachedFrames))
            {
                return cachedFrames;
            }

            Texture2D[] tooltipFrames = new Texture2D[3];
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
            _skillTooltipTextureCache[cacheKey] = tooltipFrames;
            return tooltipFrames;
        }

        private static WzCanvasProperty ResolveBigBangStatusBarBackgroundCanvas(WzSubProperty mainBarProperties, RenderParameters renderParams)
        {
            WzCanvasProperty defaultBackgroundCanvas = mainBarProperties?["backgrnd"] as WzCanvasProperty;
            WzCanvasProperty widescreenBackgroundCanvas = mainBarProperties?["backgrnd_BAK2"] as WzCanvasProperty;
            if (widescreenBackgroundCanvas == null)
            {
                return defaultBackgroundCanvas;
            }

            int viewportWidth = (int)Math.Ceiling(renderParams.RenderWidth / Math.Max(0.01f, renderParams.RenderObjectScaling));
            int defaultWidth = GetCanvasBitmapWidth(defaultBackgroundCanvas);
            int widescreenWidth = GetCanvasBitmapWidth(widescreenBackgroundCanvas);
            if (widescreenWidth <= 0)
            {
                return defaultBackgroundCanvas;
            }

            if (viewportWidth > defaultWidth && widescreenWidth > defaultWidth)
            {
                return widescreenBackgroundCanvas;
            }

            return defaultBackgroundCanvas ?? widescreenBackgroundCanvas;
        }

        private static int GetCanvasBitmapWidth(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return 0;
            }

            using (System.Drawing.Bitmap bitmap = LoadCanvasBitmap(canvas))
            {
                return bitmap?.Width ?? 0;
            }
        }

        private static Dictionary<string, StatusBarKeyDownBarTextures> LoadKeyDownBarTextures(WzImage uiBasic, GraphicsDevice device)
        {
            if (uiBasic == null || device == null)
            {
                return new Dictionary<string, StatusBarKeyDownBarTextures>(StringComparer.OrdinalIgnoreCase);
            }

            string cacheKey = $"{GetDeviceCachePrefix(device)}|keyDown:{uiBasic.Name}";
            if (_keyDownBarTextureCache.TryGetValue(cacheKey, out Dictionary<string, StatusBarKeyDownBarTextures> cachedTextures))
            {
                return cachedTextures;
            }

            var keyDownBarTextures = new Dictionary<string, StatusBarKeyDownBarTextures>(StringComparer.OrdinalIgnoreCase);
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

            _keyDownBarTextureCache[cacheKey] = keyDownBarTextures;
            return keyDownBarTextures;
        }

        private static System.Drawing.Bitmap ComposeBigBangStatusBarFrame(
            WzSubProperty mainBarProperties,
            WzCanvasProperty backgrndCanvas,
            System.Drawing.Bitmap backgrnd,
            System.Drawing.Bitmap lvBacktrnd,
            System.Drawing.Bitmap lvCover,
            System.Drawing.Bitmap gaugeBackgrd,
            System.Drawing.Bitmap gaugeCover)
        {
            if (backgrnd == null)
            {
                return new System.Drawing.Bitmap(1, 1);
            }

            Point frameOrigin = GetCanvasOrigin(backgrndCanvas);
            var layers = new List<(int Z, WzCanvasProperty Canvas, System.Drawing.Bitmap Bitmap)>
            {
                (GetCanvasZ(mainBarProperties?["lvBacktrnd"] as WzCanvasProperty), mainBarProperties?["lvBacktrnd"] as WzCanvasProperty, lvBacktrnd),
                (GetCanvasZ(mainBarProperties?["gaugeBackgrd"] as WzCanvasProperty), mainBarProperties?["gaugeBackgrd"] as WzCanvasProperty, gaugeBackgrd),
                (GetCanvasZ(mainBarProperties?["lvCover"] as WzCanvasProperty), mainBarProperties?["lvCover"] as WzCanvasProperty, lvCover),
                (GetCanvasZ(mainBarProperties?["gaugeCover"] as WzCanvasProperty), mainBarProperties?["gaugeCover"] as WzCanvasProperty, gaugeCover)
            };

            var composed = new System.Drawing.Bitmap(backgrnd.Width, backgrnd.Height);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(composed))
            {
                graphics.Clear(System.Drawing.Color.Transparent);
                graphics.DrawImage(backgrnd, 0, 0);

                foreach ((WzCanvasProperty canvas, System.Drawing.Bitmap bitmap) in EnumerateOrderedRenderableCanvasLayers(layers))
                {
                    Point layerOrigin = GetCanvasOrigin(canvas);
                    int drawX = frameOrigin.X - layerOrigin.X;
                    int drawY = frameOrigin.Y - layerOrigin.Y;
                    graphics.DrawImage(bitmap, drawX, drawY);
                }
            }

            return composed;
        }

        private static Point ResolveBigBangChatFrameAnchorOrigin(WzCanvasProperty chatSpace2Canvas, WzCanvasProperty chatSpaceCanvas)
        {
            if (chatSpace2Canvas != null)
            {
                return GetCanvasOrigin(chatSpace2Canvas);
            }

            return GetCanvasOrigin(chatSpaceCanvas);
        }

        private static int ResolveBigBangStatusBarClusterWidth(
            Point frameOrigin,
            Point clusterBaseOffset,
            params (WzCanvasProperty Canvas, System.Drawing.Bitmap Bitmap)[] layers)
        {
            int maxWidth = 0;

            foreach ((WzCanvasProperty Canvas, System.Drawing.Bitmap Bitmap) layer in layers)
            {
                if (layer.Canvas == null || layer.Bitmap == null)
                {
                    continue;
                }

                if (!TryGetBitmapDimensions(layer.Bitmap, out int width, out _))
                {
                    continue;
                }

                Point layerOffset = ResolveCanvasPosition(frameOrigin, layer.Canvas);
                int relativeRight = (layerOffset.X - clusterBaseOffset.X) + width;
                maxWidth = Math.Max(maxWidth, relativeRight);
            }

            return maxWidth;
        }

        private static System.Drawing.Bitmap ComposeBigBangStatusBarChatFrame(
            Point anchorOrigin,
            WzCanvasProperty chatSpaceCanvas,
            System.Drawing.Bitmap chatSpace,
            WzCanvasProperty chatSpace2Canvas,
            System.Drawing.Bitmap chatSpace2,
            WzCanvasProperty chatCoverCanvas,
            System.Drawing.Bitmap chatCover,
            WzCanvasProperty noticeCanvas,
            System.Drawing.Bitmap notice)
        {
            var layers = new List<(int Z, WzCanvasProperty Canvas, System.Drawing.Bitmap Bitmap)>
            {
                (GetCanvasZ(chatSpaceCanvas), chatSpaceCanvas, chatSpace),
                (GetCanvasZ(chatSpace2Canvas), chatSpace2Canvas, chatSpace2),
                (GetCanvasZ(chatCoverCanvas), chatCoverCanvas, chatCover),
                (GetCanvasZ(noticeCanvas), noticeCanvas, notice)
            };

            int minX = 0;
            int minY = 0;
            int maxX = 1;
            int maxY = 1;

            foreach ((int _, WzCanvasProperty canvas, System.Drawing.Bitmap bitmap) in layers)
            {
                if (!TryGetRenderableCanvasLayerDimensions(canvas, bitmap, out int width, out int height))
                {
                    continue;
                }

                Point drawPosition = ResolveCanvasPosition(anchorOrigin, canvas);
                minX = Math.Min(minX, drawPosition.X);
                minY = Math.Min(minY, drawPosition.Y);
                maxX = Math.Max(maxX, drawPosition.X + width);
                maxY = Math.Max(maxY, drawPosition.Y + height);
            }

            var composed = new System.Drawing.Bitmap(Math.Max(1, maxX - minX), Math.Max(1, maxY - minY));
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(composed))
            {
                graphics.Clear(System.Drawing.Color.Transparent);
                foreach ((WzCanvasProperty canvas, System.Drawing.Bitmap bitmap) in EnumerateOrderedRenderableCanvasLayers(layers))
                {
                    Point drawPosition = ResolveCanvasPosition(anchorOrigin, canvas);
                    graphics.DrawImage(bitmap, drawPosition.X - minX, drawPosition.Y - minY);
                }
            }

            return composed;
        }

        private static void PositionStatusBarButton(UIObject button, WzSubProperty buttonProperty, Point anchorOrigin)
        {
            if (button == null || buttonProperty == null)
            {
                return;
            }

            button.X = ResolveCanvasPosition(anchorOrigin, buttonProperty?["normal"]?["0"] as WzCanvasProperty).X;
            button.Y = ResolveCanvasPosition(anchorOrigin, buttonProperty?["normal"]?["0"] as WzCanvasProperty).Y;
        }

        private static IEnumerable<(WzCanvasProperty Canvas, System.Drawing.Bitmap Bitmap)> EnumerateOrderedRenderableCanvasLayers(
            IEnumerable<(int Z, WzCanvasProperty Canvas, System.Drawing.Bitmap Bitmap)> layers)
        {
            foreach ((int _, WzCanvasProperty canvas, System.Drawing.Bitmap bitmap) in layers.OrderBy(layer => layer.Z))
            {
                if (!TryGetRenderableCanvasLayerDimensions(canvas, bitmap, out _, out _))
                {
                    continue;
                }

                yield return (canvas, bitmap);
            }
        }

        private static bool TryGetRenderableCanvasLayerDimensions(
            WzCanvasProperty canvas,
            System.Drawing.Bitmap bitmap,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;
            return canvas != null && bitmap != null && TryGetBitmapDimensions(bitmap, out width, out height);
        }

        private static Point ResolveCanvasPosition(Point anchorOrigin, WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return Point.Zero;
            }

            Point canvasOrigin = GetCanvasOrigin(canvas);
            return new Point(
                anchorOrigin.X - canvasOrigin.X,
                anchorOrigin.Y - canvasOrigin.Y);
        }

        private static Rectangle ResolveCanvasBounds(Point anchorOrigin, WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return Rectangle.Empty;
            }

            Point position = ResolveCanvasPosition(anchorOrigin, canvas);
            using System.Drawing.Bitmap bitmap = LoadCanvasBitmap(canvas);
            if (!TryGetBitmapDimensions(bitmap, out int width, out int height))
            {
                return Rectangle.Empty;
            }

            return new Rectangle(position.X, position.Y, width, height);
        }

        private static Rectangle ResolveChatInteractionBounds(
            Vector2 chatLogTextPos,
            int chatLogWidth,
            Rectangle chatEnterBounds,
            Rectangle chatSpace2Bounds,
            int chatEnterHeight)
        {
            const int ChatMaxVisibleLines = 8;
            const int DefaultChatLogLineHeight = 14;
            int left = Math.Min(
                (int)MathF.Floor(chatLogTextPos.X) - 4,
                chatSpace2Bounds.IsEmpty ? int.MaxValue : chatSpace2Bounds.Left);
            if (left == int.MaxValue)
            {
                left = (int)MathF.Floor(chatLogTextPos.X) - 4;
            }

            int right = Math.Max(
                (int)MathF.Ceiling(chatLogTextPos.X) + chatLogWidth + 4,
                Math.Max(
                    chatEnterBounds.IsEmpty ? int.MinValue : chatEnterBounds.Right,
                    chatSpace2Bounds.IsEmpty ? int.MinValue : chatSpace2Bounds.Right));
            if (right == int.MinValue)
            {
                right = left + chatLogWidth + 8;
            }

            int bottom = Math.Max(
                chatEnterBounds.IsEmpty ? int.MinValue : chatEnterBounds.Bottom,
                chatSpace2Bounds.IsEmpty ? int.MinValue : chatSpace2Bounds.Bottom);
            if (bottom == int.MinValue)
            {
                bottom = (chatEnterBounds.IsEmpty ? 0 : chatEnterBounds.Y) + Math.Max(1, chatEnterHeight);
            }

            int top = (int)MathF.Floor(chatLogTextPos.Y) - (ChatMaxVisibleLines * DefaultChatLogLineHeight) - 2;
            return new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
        }

        private static StatusBarWarningAnimation LoadStatusBarWarningAnimation(WzSubProperty warningProperty, GraphicsDevice device)
        {
            if (warningProperty == null || device == null)
            {
                return new StatusBarWarningAnimation();
            }

            string cacheKey = BuildWarningAnimationCacheKey(device, warningProperty);
            if (_warningAnimationCache.TryGetValue(cacheKey, out StatusBarWarningAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            var animation = new StatusBarWarningAnimation();
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
            _warningAnimationCache[cacheKey] = animation;
            return animation;
        }

        private static (Dictionary<MapSimulatorChatTargetType, Texture2D> Textures, Dictionary<MapSimulatorChatTargetType, Point> Origins)
            LoadChatTargetTextures(WzSubProperty chatTargetProperty, GraphicsDevice device)
        {
            if (chatTargetProperty == null || device == null)
            {
                return (new Dictionary<MapSimulatorChatTargetType, Texture2D>(), new Dictionary<MapSimulatorChatTargetType, Point>());
            }

            string cacheKey = BuildChatTargetTextureCacheKey(device, chatTargetProperty);
            if (_chatTargetTextureCache.TryGetValue(cacheKey, out var cachedTextures))
            {
                return cachedTextures;
            }

            var textures = new Dictionary<MapSimulatorChatTargetType, Texture2D>();
            var origins = new Dictionary<MapSimulatorChatTargetType, Point>();
            AddChatTargetTexture(textures, origins, chatTargetProperty, "all", MapSimulatorChatTargetType.All, device);
            AddChatTargetTexture(textures, origins, chatTargetProperty, "friend", MapSimulatorChatTargetType.Friend, device);
            AddChatTargetTexture(textures, origins, chatTargetProperty, "party", MapSimulatorChatTargetType.Party, device);
            AddChatTargetTexture(textures, origins, chatTargetProperty, "guild", MapSimulatorChatTargetType.Guild, device);
            AddChatTargetTexture(textures, origins, chatTargetProperty, "association", MapSimulatorChatTargetType.Association, device);
            AddChatTargetTexture(textures, origins, chatTargetProperty, "expedition", MapSimulatorChatTargetType.Expedition, device);
            var result = (textures, origins);
            _chatTargetTextureCache[cacheKey] = result;
            return result;
        }

        private static StatusBarChatUI.StatusBarPointNotificationAnimation LoadPointNotificationAnimation(
            WzSubProperty notificationProperty,
            GraphicsDevice device)
        {
            if (notificationProperty == null || device == null)
            {
                return new StatusBarChatUI.StatusBarPointNotificationAnimation();
            }

            string cacheKey = BuildPointNotificationCacheKey(device, notificationProperty);
            if (_pointNotificationAnimationCache.TryGetValue(cacheKey, out StatusBarChatUI.StatusBarPointNotificationAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            var animation = new StatusBarChatUI.StatusBarPointNotificationAnimation();
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
            _pointNotificationAnimationCache[cacheKey] = animation;
            return animation;
        }

        private static Point GetCanvasOrigin(WzCanvasProperty canvas)
        {
            if (!(canvas?["origin"] is WzVectorProperty origin))
            {
                return Point.Zero;
            }

            return new Point(origin.X.Value, origin.Y.Value);
        }

        private static int GetCanvasZ(WzCanvasProperty canvas)
        {
            if (!(canvas?["z"] is WzIntProperty z))
            {
                return 0;
            }

            return z.Value;
        }

        private static void AddChatTargetTexture(
            Dictionary<MapSimulatorChatTargetType, Texture2D> textures,
            Dictionary<MapSimulatorChatTargetType, Point> origins,
            WzSubProperty chatTargetProperty,
            string propertyName,
            MapSimulatorChatTargetType targetType,
            GraphicsDevice device)
        {
            WzCanvasProperty chatTargetCanvas = chatTargetProperty?[propertyName] as WzCanvasProperty;
            Texture2D texture = LoadCanvasTexture(chatTargetCanvas, device);
            if (texture != null)
            {
                textures[targetType] = texture;
                origins[targetType] = GetCanvasOrigin(chatTargetCanvas);
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

        private static void LoadBuffIconCatalogEntriesRecursive(
            Dictionary<string, BuffIconCatalogEntry> catalogEntries,
            WzObject currentNode,
            string currentPath,
            ref int sortOrder)
        {
            if (currentNode == null)
            {
                return;
            }

            if (TryCreateBuffIconCatalogEntry(currentNode, currentPath, ref sortOrder, out BuffIconCatalogEntry entry)
                && !string.IsNullOrWhiteSpace(entry.IconKey)
                && !catalogEntries.ContainsKey(entry.IconKey))
            {
                catalogEntries[entry.IconKey] = entry;
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
                LoadBuffIconCatalogEntriesRecursive(catalogEntries, child, childPath, ref sortOrder);
            }
        }

        private static bool TryCreateBuffIconCatalogEntry(
            WzObject currentNode,
            string currentPath,
            ref int sortOrder,
            out BuffIconCatalogEntry entry)
        {
            entry = null;
            if (!(currentNode is IPropertyContainer container))
            {
                return false;
            }

            string normalizedPath = NormalizeBuffIconKey(currentPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            WzStringProperty nameProperty = container["name"] as WzStringProperty;
            WzCanvasProperty iconCanvas = container["0"] as WzCanvasProperty;
            string displayName = nameProperty?.GetString();
            if (iconCanvas == null || string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            int entrySortOrder = 0;
            if (normalizedPath.StartsWith("buff/", StringComparison.OrdinalIgnoreCase))
            {
                sortOrder += 10;
                entrySortOrder = sortOrder;
            }

            entry = new BuffIconCatalogEntry
            {
                IconKey = normalizedPath,
                DisplayName = displayName.Trim(),
                SortOrder = entrySortOrder
            };
            return true;
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

            var bitmap = LoadCanvasBitmap(canvas);
            return bitmap?.ToTexture2DAndDispose(device);
        }

        private static System.Drawing.Bitmap LoadCanvasBitmap(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            try
            {
                System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (!TryGetBitmapDimensions(bitmap, out int width, out int height)
                    || width <= 0
                    || height <= 0)
                {
                    bitmap?.Dispose();
                    return null;
                }

                return bitmap;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        private static bool TryGetBitmapDimensions(System.Drawing.Bitmap bitmap, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (bitmap == null)
            {
                return false;
            }

            try
            {
                width = bitmap.Width;
                height = bitmap.Height;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        private enum MinimapMarkerAnchorProfile
        {
            StandingPoint,
            PortalPoint
        }

        private static System.Drawing.PointF ResolveMinimapMarkerOrigin(
            WzCanvasProperty canvas,
            System.Drawing.Bitmap bitmap,
            MinimapMarkerAnchorProfile profile)
        {
            System.Drawing.PointF origin = canvas?.GetCanvasOriginPosition() ?? new System.Drawing.PointF(0f, 0f);
            if (origin.X != 0f || origin.Y != 0f || bitmap == null)
            {
                return origin;
            }

            return profile switch
            {
                MinimapMarkerAnchorProfile.PortalPoint => new System.Drawing.PointF(
                    -((bitmap.Width - 1) / 2) - 1,
                    -Math.Max(0, bitmap.Height - 2)),
                _ => new System.Drawing.PointF(
                    -((bitmap.Width - 1) / 2) - 2,
                    -Math.Max(0, bitmap.Height - 1))
            };
        }

        private static Point ResolveMinimapImageOffset(
            System.Drawing.Bitmap container,
            System.Drawing.Bitmap content,
            Point fallbackOffset)
        {
            return TryFindEmbeddedBitmapOffset(container, content, out Point offset)
                ? offset
                : fallbackOffset;
        }

        private static bool TryFindEmbeddedBitmapOffset(
            System.Drawing.Bitmap container,
            System.Drawing.Bitmap content,
            out Point offset)
        {
            offset = Point.Zero;
            if (!TryGetBitmapDimensions(container, out int containerWidth, out int containerHeight) ||
                !TryGetBitmapDimensions(content, out int contentWidth, out int contentHeight) ||
                contentWidth <= 0 || contentHeight <= 0 ||
                contentWidth > containerWidth || contentHeight > containerHeight)
            {
                return false;
            }

            using System.Drawing.Bitmap normalizedContainer = EnsureArgbBitmap(container);
            using System.Drawing.Bitmap normalizedContent = EnsureArgbBitmap(content);

            byte[] containerBytes = CopyBitmapBytes(normalizedContainer, out int containerStride);
            byte[] contentBytes = CopyBitmapBytes(normalizedContent, out int contentStride);
            if (containerBytes == null || contentBytes == null)
            {
                return false;
            }

            int maxX = containerWidth - contentWidth;
            int maxY = containerHeight - contentHeight;
            const int bytesPerPixel = 4;

            int sampleTopLeft = 0;
            int sampleCenter = ((contentHeight / 2) * contentStride) + ((contentWidth / 2) * bytesPerPixel);
            int sampleBottomRight = ((contentHeight - 1) * contentStride) + ((contentWidth - 1) * bytesPerPixel);

            for (int y = 0; y <= maxY; y++)
            {
                for (int x = 0; x <= maxX; x++)
                {
                    int containerBase = (y * containerStride) + (x * bytesPerPixel);
                    if (!PixelEquals(containerBytes, containerBase + sampleTopLeft, contentBytes, sampleTopLeft) ||
                        !PixelEquals(containerBytes, containerBase + sampleCenter, contentBytes, sampleCenter) ||
                        !PixelEquals(containerBytes, containerBase + sampleBottomRight, contentBytes, sampleBottomRight))
                    {
                        continue;
                    }

                    bool matched = true;
                    for (int row = 0; row < contentHeight && matched; row++)
                    {
                        int containerRow = ((y + row) * containerStride) + (x * bytesPerPixel);
                        int contentRow = row * contentStride;
                        for (int col = 0; col < contentWidth * bytesPerPixel; col++)
                        {
                            if (containerBytes[containerRow + col] != contentBytes[contentRow + col])
                            {
                                matched = false;
                                break;
                            }
                        }
                    }

                    if (matched)
                    {
                        offset = new Point(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        private static System.Drawing.Bitmap EnsureArgbBitmap(System.Drawing.Bitmap bitmap)
        {
            if (bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            {
                return (System.Drawing.Bitmap)bitmap.Clone();
            }

            System.Drawing.Rectangle bounds = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            return bitmap.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        }

        private static byte[] CopyBitmapBytes(System.Drawing.Bitmap bitmap, out int stride)
        {
            stride = 0;
            if (bitmap == null)
            {
                return null;
            }

            System.Drawing.Rectangle bounds = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            System.Drawing.Imaging.BitmapData data = null;
            try
            {
                data = bitmap.LockBits(bounds, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                stride = data.Stride;
                int byteCount = Math.Abs(stride) * bitmap.Height;
                byte[] bytes = new byte[byteCount];
                Marshal.Copy(data.Scan0, bytes, 0, byteCount);
                return bytes;
            }
            finally
            {
                if (data != null)
                {
                    bitmap.UnlockBits(data);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PixelEquals(byte[] left, int leftIndex, byte[] right, int rightIndex)
        {
            return left[leftIndex] == right[rightIndex] &&
                   left[leftIndex + 1] == right[rightIndex + 1] &&
                   left[leftIndex + 2] == right[rightIndex + 2] &&
                   left[leftIndex + 3] == right[rightIndex + 3];
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

            string minimapCacheKey = BuildMinimapCacheKey(device, mapBoard, UserScreenScaleFactor, bBigBang);
            if (_minimapCache.TryGetValue(minimapCacheKey, out MinimapUI cachedMinimap))
            {
                ApplySharedMinimapWindowPosition(cachedMinimap);
                return cachedMinimap;
            }

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

            WzSubProperty compactFrameProperty = useFrameMinMap ?? useFrameMaxMap;
            WzSubProperty expandedFrameProperty = useFrameMaxMap ?? useFrameMinMap;

            // Wz frames
            System.Drawing.Bitmap compactC = ((WzCanvasProperty)compactFrameProperty?["c"])?.GetLinkedWzCanvasBitmap(); // the bg color
            System.Drawing.Bitmap compactE = ((WzCanvasProperty)compactFrameProperty?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap compactN = ((WzCanvasProperty)compactFrameProperty?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap compactS = ((WzCanvasProperty)compactFrameProperty?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap compactW = ((WzCanvasProperty)compactFrameProperty?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap compactNe = ((WzCanvasProperty)compactFrameProperty?["ne"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap compactNw = ((WzCanvasProperty)compactFrameProperty?["nw"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap compactSe = ((WzCanvasProperty)compactFrameProperty?["se"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap compactSw = ((WzCanvasProperty)compactFrameProperty?["sw"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedC = ((WzCanvasProperty)expandedFrameProperty?["c"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedE = ((WzCanvasProperty)expandedFrameProperty?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedN = ((WzCanvasProperty)expandedFrameProperty?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedS = ((WzCanvasProperty)expandedFrameProperty?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedW = ((WzCanvasProperty)expandedFrameProperty?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedNe = ((WzCanvasProperty)expandedFrameProperty?["ne"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedNw = ((WzCanvasProperty)expandedFrameProperty?["nw"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedSe = ((WzCanvasProperty)expandedFrameProperty?["se"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap expandedSw = ((WzCanvasProperty)expandedFrameProperty?["sw"])?.GetLinkedWzCanvasBitmap();

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
            HaUIStackPanel collapsedMapNameStackPanel = new HaUIStackPanel(HaUIStackOrientation.Horizontal, new HaUIInfo()
            {
                Margins = new HaUIMargin() { Top = 1, Left = MAPMARK_MAPNAME_LEFT_MARGIN, Bottom = 0, Right = 0 },
            });

            if (mapMark != null)
            {
                // minimap map-mark image
                HaUIImage mapNameMarkImage = new HaUIImage(new HaUIInfo()
                {
                    Bitmap = mapMark,
                });
                mapNameMarkStackPanel.AddRenderable(mapNameMarkImage);

                collapsedMapNameStackPanel.AddRenderable(new HaUIImage(new HaUIInfo()
                {
                    Bitmap = mapMark,
                    Margins = new HaUIMargin() { Right = 2 }
                }));
            }
            // Minimap name, and street name
            string renderText = string.Format("{0}{1}{2}", StreetName, Environment.NewLine, MapName);
            string collapsedRenderText = !string.IsNullOrWhiteSpace(MapName) ? MapName : StreetName;
            HaUIText haUITextMapNameStreetName = new HaUIText(renderText, color_foreGround, GLOBAL_FONT, MINIMAP_STREETNAME_TOOLTIP_FONTSIZE, UserScreenScaleFactor);
            haUITextMapNameStreetName.GetInfo().Margins.Top = 3;
            haUITextMapNameStreetName.GetInfo().Margins.Left = MAP_IMAGE_TEXT_PADDING;
            haUITextMapNameStreetName.GetInfo().Margins.Right = MAP_IMAGE_TEXT_PADDING;
            HaUIText collapsedTitleText = new HaUIText(collapsedRenderText, color_foreGround, GLOBAL_FONT, MINIMAP_STREETNAME_TOOLTIP_FONTSIZE, UserScreenScaleFactor);
            collapsedTitleText.GetInfo().Margins.Top = 1;
            collapsedTitleText.GetInfo().Margins.Left = MAP_IMAGE_TEXT_PADDING;
            collapsedTitleText.GetInfo().Margins.Right = MAP_IMAGE_TEXT_PADDING;

            mapNameMarkStackPanel.AddRenderable(haUITextMapNameStreetName);
            collapsedMapNameStackPanel.AddRenderable(collapsedTitleText);
            fullMiniMapStackPanel.AddRenderable(mapNameMarkStackPanel);

            WzSubProperty collapsedBarProperty = minimapFrameProperty["Min"] as WzSubProperty;
            System.Drawing.Bitmap collapsedBarLeft = ((WzCanvasProperty)collapsedBarProperty?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap collapsedBarCenter = ((WzCanvasProperty)collapsedBarProperty?["c"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap collapsedBarRight = ((WzCanvasProperty)collapsedBarProperty?["e"])?.GetLinkedWzCanvasBitmap();
            HaUIStackPanel collapsedMiniMapStackPanel = new HaUIStackPanel(HaUIStackOrientation.Vertical);
            collapsedMiniMapStackPanel.AddRenderable(collapsedMapNameStackPanel);
            System.Drawing.Bitmap finalMininisedMinimapBitmap =
                collapsedBarLeft != null && collapsedBarCenter != null && collapsedBarRight != null
                    ? HaUIHelper.RenderAndMergeMinimapCollapsedBar(collapsedMiniMapStackPanel, color_bgFill, collapsedBarLeft, collapsedBarCenter, collapsedBarRight)
                    : HaUIHelper.RenderAndMergeMinimapUIFrame(fullMiniMapStackPanel, color_bgFill, compactNe, compactNw, compactSe, compactSw, compactE, compactW, compactN, compactS,
                        compactC, mapMark != null ? mapMark.Height : 0);

            HaUIGrid minimapUiGrid = new HaUIGrid(1, 1);
            minimapUiGrid.GetInfo().Margins.Top = 10;
            minimapUiGrid.GetInfo().HorizontalAlignment = HaUIAlignment.Center;
            minimapUiGrid.GetInfo().VerticalAlignment = HaUIAlignment.Center;
            minimapUiGrid.AddRenderable(minimapUiImage);
            fullMiniMapStackPanel.AddRenderable(minimapUiGrid);

            // Render compact and expanded minimap bitmaps with the client-owned option frames.
            System.Drawing.Bitmap finalCompactMinimapBitmap = HaUIHelper.RenderAndMergeMinimapUIFrame(fullMiniMapStackPanel, color_bgFill, compactNe, compactNw, compactSe, compactSw, compactE, compactW, compactN, compactS,
                compactC, mapMark != null ? mapMark.Height : 0);
            System.Drawing.Bitmap finalExpandedMinimapBitmap = HaUIHelper.RenderAndMergeMinimapUIFrame(fullMiniMapStackPanel, color_bgFill, expandedNe, expandedNw, expandedSe, expandedSw, expandedE, expandedW, expandedN, expandedS,
                expandedC, mapMark != null ? mapMark.Height : 0);

            Texture2D texturer_miniMapMinimised = finalMininisedMinimapBitmap.ToTexture2DAndDispose(device);
            Texture2D texturer_miniMapCompact = finalCompactMinimapBitmap.ToTexture2DAndDispose(device);
            Texture2D texturer_miniMapExpanded = finalExpandedMinimapBitmap.ToTexture2DAndDispose(device);

            // Dots pixel
            System.Drawing.Bitmap bmp_DotPixel = new System.Drawing.Bitmap(2, 4);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_DotPixel))
            {
                graphics.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Yellow), new System.Drawing.RectangleF(0, 0, bmp_DotPixel.Width, bmp_DotPixel.Height));
                graphics.Flush();
            }
            IDXObject dxObj_miniMapPixel = new DXObject(0, 0, bmp_DotPixel.ToTexture2DAndDispose(device), 0);

            // Map
            IDXObject dxObj_miniMap_Minimised = new DXObject(0, 0, texturer_miniMapMinimised, 0);
            IDXObject dxObj_miniMap = new DXObject(0, 0, texturer_miniMapCompact, 0);
            IDXObject dxObj_miniMapExpanded = new DXObject(0, 0, texturer_miniMapExpanded, 0);

            // need to calculate how much x position, where the map is shifted to the center by HorizontalAlignment
            // to compensate for in the character dot position indicator
            HaUISize fullMiniMapStackPanelSize = fullMiniMapStackPanel.GetSize();
            int alignmentXOffset = HaUIHelper.CalculateAlignmentOffset(fullMiniMapStackPanelSize.Width, minimapUiImage.GetInfo().Bitmap.Width, minimapUiGrid.GetInfo().HorizontalAlignment);

            Point compactFallbackOffset = new Point(MAP_IMAGE_TEXT_PADDING + alignmentXOffset, compactN?.Height ?? 0);
            Point expandedFallbackOffset = new Point(MAP_IMAGE_TEXT_PADDING + alignmentXOffset, expandedN?.Height ?? 0);
            Point compactMinimapImageOffset = ResolveMinimapImageOffset(finalCompactMinimapBitmap, miniMapImage, compactFallbackOffset);
            Point expandedMinimapImageOffset = ResolveMinimapImageOffset(finalExpandedMinimapBitmap, miniMapImage, expandedFallbackOffset);
            BaseDXDrawableItem userMarker = null;
            BaseDXDrawableItem npcMarker = null;
            BaseDXDrawableItem questStartNpcMarker = null;
            BaseDXDrawableItem questEndNpcMarker = null;
            BaseDXDrawableItem npcListPanel = null;
            BaseDXDrawableItem portalMarker = null;
            Dictionary<MinimapUI.DirectionArrow, BaseDXDrawableItem> directionMarkers = new Dictionary<MinimapUI.DirectionArrow, BaseDXDrawableItem>();
            Dictionary<MinimapUI.HelperMarkerType, BaseDXDrawableItem> helperMarkers = new Dictionary<MinimapUI.HelperMarkerType, BaseDXDrawableItem>();

            WzSubProperty minimapSimpleModeProperty = uiWindow2Image?["MiniMapSimpleMode"] as WzSubProperty;
            WzSubProperty defaultHelperProperty = minimapSimpleModeProperty?["DefaultHelper"] as WzSubProperty;

            WzCanvasProperty userCanvas = defaultHelperProperty?["user"] as WzCanvasProperty;
            if (userCanvas != null)
            {
                System.Drawing.Bitmap userMarkerBitmap = userCanvas.GetLinkedWzCanvasBitmap();
                if (userMarkerBitmap != null)
                {
                    System.Drawing.PointF userMarkerOrigin = ResolveMinimapMarkerOrigin(
                        userCanvas,
                        userMarkerBitmap,
                        MinimapMarkerAnchorProfile.StandingPoint);
                    IDXObject dxObjUserMarker = new DXObject(userMarkerOrigin, userMarkerBitmap.ToTexture2DAndDispose(device), 0);
                    userMarker = new BaseDXDrawableItem(dxObjUserMarker, false)
                    {
                        Position = compactMinimapImageOffset
                    };
                }
            }

            WzCanvasProperty iconNpcCanvas =
                defaultHelperProperty?["npc"] as WzCanvasProperty ??
                (bBigBang ? (WzCanvasProperty)minimapFrameProperty["iconNpc"]?["0"] : null);
            BaseDXDrawableItem animatedNpcMarker = bBigBang
                ? LoadAnimatedMinimapMarker(minimapFrameProperty["iconNpc"] as WzSubProperty, device, compactMinimapImageOffset, 120)
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
                        Position = compactMinimapImageOffset
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
                        Position = compactMinimapImageOffset
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
                        Position = compactMinimapImageOffset
                    };
                }
            }

            WzCanvasProperty portalCanvas = defaultHelperProperty?["portal"] as WzCanvasProperty;
            if (portalCanvas != null)
            {
                System.Drawing.Bitmap portalBitmap = portalCanvas.GetLinkedWzCanvasBitmap();
                if (portalBitmap != null)
                {
                    System.Drawing.PointF portalMarkerOrigin = ResolveMinimapMarkerOrigin(
                        portalCanvas,
                        portalBitmap,
                        MinimapMarkerAnchorProfile.PortalPoint);
                    IDXObject dxObjPortalMarker = new DXObject(portalMarkerOrigin, portalBitmap.ToTexture2DAndDispose(device), 0);
                    portalMarker = new BaseDXDrawableItem(dxObjPortalMarker, false)
                    {
                        Position = compactMinimapImageOffset
                    };
                }
            }

            var helperCanvasMap = new Dictionary<MinimapUI.HelperMarkerType, string>
            {
                { MinimapUI.HelperMarkerType.Another, "another" },
                { MinimapUI.HelperMarkerType.Friend, "friend" },
                { MinimapUI.HelperMarkerType.Guild, "guild" },
                { MinimapUI.HelperMarkerType.GuildMaster, "guildmaster" },
                { MinimapUI.HelperMarkerType.Match, "match" },
                { MinimapUI.HelperMarkerType.Party, "party" },
                { MinimapUI.HelperMarkerType.PartyMaster, "partymaster" },
                { MinimapUI.HelperMarkerType.UserTrader, "usertrader" },
                { MinimapUI.HelperMarkerType.AnotherTrader, "anothertrader" }
            };

            foreach (var helperEntry in helperCanvasMap)
            {
                WzCanvasProperty helperCanvas = defaultHelperProperty?[helperEntry.Value] as WzCanvasProperty;
                if (helperCanvas == null)
                    continue;

                System.Drawing.Bitmap helperBitmap = helperCanvas.GetLinkedWzCanvasBitmap();
                if (helperBitmap == null)
                    continue;

                System.Drawing.PointF helperOrigin = ResolveMinimapMarkerOrigin(
                    helperCanvas,
                    helperBitmap,
                    MinimapMarkerAnchorProfile.StandingPoint);
                IDXObject dxObjHelper = new DXObject(helperOrigin, helperBitmap.ToTexture2DAndDispose(device), 0);
                helperMarkers[helperEntry.Key] = new BaseDXDrawableItem(dxObjHelper, false)
                {
                    Position = compactMinimapImageOffset
                };
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
                    animatedArrow = LoadAnimatedMinimapMarker(minimapFrameProperty["iconDirection"]?[ToLegacyArrowKey(arrowEntry.Key)] as WzSubProperty, device, compactMinimapImageOffset, 120);
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
                    Position = compactMinimapImageOffset
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
                            Position = new Point(Math.Max(0, texturer_miniMapExpanded.Width - dxObjNpcList.Width), texturer_miniMapExpanded.Height + 4)
                        };
                    }
                }
            }

            MinimapUI minimapItem = new MinimapUI(dxObj_miniMap,
                new BaseDXDrawableItem(dxObj_miniMapPixel, false)
                {
                    Position = compactMinimapImageOffset // map image origin in compact mode
                },
                new BaseDXDrawableItem(dxObj_miniMapExpanded, false)
                {
                    Position = new Point(MAP_IMAGE_TEXT_PADDING, 0)
                },
                new BaseDXDrawableItem(dxObj_miniMap_Minimised, false)
                {
                    Position = new Point(MAP_IMAGE_TEXT_PADDING, 0)
                },
                miniMapImage.Width,
                miniMapImage.Height,
                compactMinimapImageOffset,
                expandedMinimapImageOffset,
                userMarker,
                npcMarker,
                questStartNpcMarker,
                questEndNpcMarker,
                npcListPanel,
                portalMarker,
                directionMarkers,
                helperMarkers);

            ApplySharedMinimapWindowPosition(minimapItem);

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
                objUIBtMap.X = texturer_miniMapCompact.Width - objUIBtMap.CanvasSnapshotWidth - 8;

                UIObject objUIBtBig = null;
                if (BtBig != null)
                {
                    objUIBtBig = new UIObject(BtBig, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                    objUIBtBig.X = objUIBtMap.X - objUIBtBig.CanvasSnapshotWidth;
                }

                WzSubProperty BtSmall = (WzSubProperty)minimapFrameProperty["BtSmall"];
                UIObject objUIBtSmall = null;
                if (BtSmall != null)
                {
                    objUIBtSmall = new UIObject(BtSmall, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                    objUIBtSmall.X = objUIBtMap.X - objUIBtSmall.CanvasSnapshotWidth;
                    objUIBtSmall.SetVisible(false);
                }

                UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMax.X = (objUIBtBig ?? objUIBtSmall ?? objUIBtMap).X - objUIBtMax.CanvasSnapshotWidth;

                UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth;

                UIObject objUIBtNpc = null;
                if (BtNpc != null)
                {
                    objUIBtNpc = new UIObject(BtNpc, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                        false,
                        new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                    objUIBtNpc.X = (objUIBtBig ?? objUIBtSmall ?? objUIBtMap).X - objUIBtNpc.CanvasSnapshotWidth;
                    objUIBtMax.X = objUIBtNpc.X - objUIBtMax.CanvasSnapshotWidth;
                    objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth;
                    objUIBtNpc.SetVisible(false);
                }

                minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, objUIBtBig, objUIBtSmall, objUIBtMap, objUIBtNpc);
            }
            else
            {
                WzSubProperty BtMin = (WzSubProperty)uiBasicImage["BtMin"]; // mininise button
                WzSubProperty BtMax = (WzSubProperty)uiBasicImage["BtMax"]; // maximise button
                WzSubProperty BtMap = (WzSubProperty)minimapFrameProperty["BtMap"]; // world button

                UIObject objUIBtMap = new UIObject(BtMap, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMap.X = texturer_miniMapCompact.Width - objUIBtMap.CanvasSnapshotWidth - 8;

                UIObject objUIBtMax = new UIObject(BtMax, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMax.X = objUIBtMap.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                UIObject objUIBtMin = new UIObject(BtMin, BtMouseClickSoundProperty, BtMouseOverSoundProperty,
                    false,
                    new Point(MAP_IMAGE_TEXT_PADDING, MAP_IMAGE_TEXT_PADDING), device);
                objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

                // BaseClickableUIObject objUINpc = new BaseClickableUIObject(BtNpc, false, new Point(objUIBtMap.CanvasSnapshotWidth + objUIBtBig.CanvasSnapshotWidth + objUIBtMax.CanvasSnapshotWidth + objUIBtMin.CanvasSnapshotWidth, MAP_IMAGE_PADDING), device);

                minimapItem.InitializeMinimapButtons(objUIBtMin, objUIBtMax, null, null, objUIBtMap);
            }
            _minimapCache[minimapCacheKey] = minimapItem;
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

            return new MouseCursorItem(frames, null, clickableButtonState, npcHoverState, holdState, forbiddenState, busyState);
        }
        #endregion
    }
}
