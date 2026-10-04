using HaCreator.MapSimulator.Assets;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Contracts;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using MapleLib.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using HaSharedLibrary.Wz;
using MapleLib.Helpers;
using SharpDX.Direct2D1.Effects;
using System.Drawing.Printing;
using HaCreator.MapSimulator.UI.Controls;
using System.Windows.Forms;
using HaCreator.MapSimulator.Loaders;
using HaSharedLibrary.Render;

namespace HaCreator.MapSimulator {
    public class MapSimulatorLoader {
        private sealed class TransparentTextureHolder {
            public Texture2D Texture;
        }

        // Cache one transparent texture per GraphicsDevice without preventing device GC.
        private static readonly ConditionalWeakTable<GraphicsDevice, TransparentTextureHolder> _transparentTextureByDevice = new();

        private static Texture2D GetTransparentTexture(GraphicsDevice device) {
            if (device == null) return null;

            TransparentTextureHolder holder = _transparentTextureByDevice.GetOrCreateValue(device);
            if (holder.Texture == null || holder.Texture.IsDisposed) {
                Texture2D tex = new Texture2D(device, 1, 1);
                // Use "transparent white" to avoid black fringing when bilinear sampling blends into transparent pixels.
                tex.SetData(new[] { new Microsoft.Xna.Framework.Color(255, 255, 255, 0) });
                holder.Texture = tex;
            }
            return holder.Texture;
        }

        private static Texture2D LoadCanvasTexture(TexturePool texturePool, WzCanvasProperty canvasProperty, GraphicsDevice device) =>
            texturePool.LoadCanvasTexture(canvasProperty, device);

        private static IDXObject CreateFrameDrawable(WzCanvasProperty canvasProperty, Texture2D texture, int x, int y, GraphicsDevice device, int? delay = null, WzSpineObject spineObject = null) {
            System.Drawing.PointF origin = canvasProperty.GetCanvasOriginPosition();

            if (spineObject != null) {
                return delay.HasValue
                    ? new DXSpineObject(spineObject, x, y, origin, delay.Value)
                    : new DXSpineObject(spineObject, x, y, origin);
            }

            texture ??= GetTransparentTexture(device);
            int drawX = x - (int)origin.X;
            int drawY = y - (int)origin.Y;

            return delay.HasValue
                ? new DXObject(drawX, drawY, texture, delay.Value)
                : new DXObject(drawX, drawY, texture);
        }

        // Constants
        private const string GLOBAL_FONT = "Arial";
        private const float TOOLTIP_FONTSIZE = 9.25f; // thankie willified, ya'll be remembered forever here <3

        private const float MINIMAP_STREETNAME_TOOLTIP_FONTSIZE = 10f;

        private static System.Drawing.Bitmap CreateTextTooltipBitmap(
            string renderText,
            System.Drawing.Font font,
            int widthPadding,
            int heightPadding,
            Action<System.Drawing.Graphics, int, int> drawBackground,
            System.Drawing.Color foregroundColor)
        {
            using var measureBitmap = new System.Drawing.Bitmap(1, 1);
            using var measureGraphics = System.Drawing.Graphics.FromImage(measureBitmap);
            System.Drawing.SizeF tooltipSize = measureGraphics.MeasureString(renderText, font);

            int effectiveWidth = (int)tooltipSize.Width + widthPadding;
            int effectiveHeight = (int)tooltipSize.Height + heightPadding;

            var tooltipBitmap = new System.Drawing.Bitmap(effectiveWidth, effectiveHeight);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(tooltipBitmap))
            using (var textBrush = new System.Drawing.SolidBrush(foregroundColor))
            {
                drawBackground(graphics, effectiveWidth, effectiveHeight);
                graphics.DrawString(renderText, font, textBrush, widthPadding / 2f, heightPadding / 2f);
                graphics.Flush();
            }

            return tooltipBitmap;
        }

        #region Common
        /// <summary>
        /// Load frames from WzSubProperty or WzCanvasProperty
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="spineAni">Spine animation path</param>
        /// <returns></returns>
        public static List<IDXObject> LoadFrames(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ConcurrentBag<WzObject> usedProps, string spineAni = null, int fallbackDelay = 100) {
            List<IDXObject> frames = new List<IDXObject>();

            source = WzInfoTools.GetRealProperty(source);

            if (source is WzSubProperty property1 && property1.WzProperties.Count == 1) {
                source = property1.WzProperties[0];
            }

            if (source is WzRawDataProperty rawProperty && rawProperty.Name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase)) {
                if (DXSpine41Object.TryLoadRawSkeleton(rawProperty, device, spineAni, out DXSpine41Object.Spine41Object spine41Object)) {
                    texturePool.OwnAnimationResource(spine41Object);
                    usedProps.Add(source);
                    frames.Add(new DXSpine41Object(spine41Object, x, y));
                }
                else if (LoadSpineMapObjectItem(source, device, spineAni) is WzSpineObject spineObject) {
                    usedProps.Add(source);
                    texturePool.RegisterSpine(spineObject);
                    frames.Add(new DXSpineObject(spineObject, x, y, System.Drawing.PointF.Empty));
                }
                return frames;
            }

            if (TryLoadDirectSpine41Frames(texturePool, source, x, y, device, spineAni, usedProps, frames)) {
                return frames;
            }

            if (source is WzCanvasProperty property) //one-frame
            {
                WzSpineObject spineObject = LoadSpineMapObjectItem(source, device, spineAni);
                texturePool.RegisterSpine(spineObject);
                Texture2D texture = spineObject != null ? null : LoadCanvasTexture(texturePool, property, device);
                usedProps.Add(source);
                int delay = (int)InfoTool.GetOptionalInt(property["delay"], fallbackDelay);
                frames.Add(CreateFrameDrawable(property, texture, x, y, device, delay, spineObject));
            }
            else if (source is WzSubProperty) // animated
            {
                WzImageProperty _frameProp;
                int i = 0;

                while ((_frameProp = WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null) {
                    if (_frameProp is WzSubProperty) // issue with 867119250
                    {
                        frames.AddRange(LoadFrames(texturePool, _frameProp, x, y, device, usedProps, null, fallbackDelay));
                    }
                    else {
                        WzCanvasProperty frameProp;

                        if (_frameProp is WzUOLProperty) // some could be UOL. Ex: 321100000 Mirror world: [Mirror World] Leafre
                        {
                            WzObject linkVal = ((WzUOLProperty)_frameProp).LinkValue;
                            if (linkVal is WzCanvasProperty linkCanvas) {
                                frameProp = linkCanvas;
                            }
                            else
                                continue;
                        }
                        else {
                            frameProp = (WzCanvasProperty)_frameProp;
                        }

                        int delay = (int)InfoTool.GetOptionalInt(frameProp["delay"], fallbackDelay);

                        WzSpineObject spineObject = LoadSpineMapObjectItem((WzImageProperty)frameProp.Parent, device, spineAni);
                        texturePool.RegisterSpine(spineObject);
                        Texture2D frameTexture = spineObject != null ? null : LoadCanvasTexture(texturePool, frameProp, device);
                        usedProps.Add(frameProp);
                        frames.Add(CreateFrameDrawable(frameProp, frameTexture, x, y, device, delay, spineObject));
                    }
                }
            }
            return frames;
        }

        public static List<IDXObject> LoadFrames(TexturePool texturePool, WzImageProperty source, int x, int y,
            GraphicsDevice device, ref List<WzObject> usedProps, string spineAni = null, int fallbackDelay = 100) {
            var concurrentUsedProps = new ConcurrentBag<WzObject>(usedProps ?? Enumerable.Empty<WzObject>());
            List<IDXObject> frames = LoadFrames(texturePool, source, x, y, device, concurrentUsedProps, spineAni, fallbackDelay);
            usedProps = concurrentUsedProps.ToList();
            return frames;
        }
        #endregion

        /// <summary>
        /// Map item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static BaseDXDrawableItem CreateMapItemFromProperty(TexturePool texturePool, 
            WzImageProperty source, 
            int x, int y, 
            Point mapCenter, GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool flip) {
            return new BaseDXDrawableItem(LoadFrames(texturePool, source, x, y, device, usedProps), flip);
        }

        /// <summary>
        /// Background
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="bgInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static BackgroundItem CreateBackgroundFromProperty(
            TexturePool texturePool,
            WzImageProperty source,
            RuntimeBackgroundDefinition background,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps)
        {
            List<IDXObject> frames = LoadFrames(
                texturePool, source, background.X, background.Y, device, usedProps, background.SpineAnimation);
            if (frames.Count == 0)
            {
                ErrorLogger.Log(ErrorLevel.IncorrectStructure,
                    $"[MapSimulatorLoader] 0 frames loaded for background texture from src: '{source?.FullPath}'");
                return null;
            }

            return frames.Count == 1
                ? new BackgroundItem(background.Cx, background.Cy, background.Rx, background.Ry,
                    (BackgroundType)background.Type, background.Alpha, background.Front, background.Page,
                    frames[0], background.Flip, background.ScreenMode)
                : new BackgroundItem(background.Cx, background.Cy, background.Rx, background.Ry,
                    (BackgroundType)background.Type, background.Alpha, background.Front, background.Page,
                    frames, background.Flip, background.ScreenMode);
        }

        #region Spine
        /// <summary>
        /// Load spine object from WzImageProperty (bg, map item)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="prop"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static WzSpineObject LoadSpineMapObjectItem(WzImageProperty source, GraphicsDevice device, string spineAniPath = null) {
            WzImageProperty spineAtlas = null;

            bool bIsObjectLayer = source.Parent?.Name == "spine";
            if (source is WzRawDataProperty && source.Name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase)) {
                spineAtlas = source.Parent is WzImageProperty parentProperty
                    ? parentProperty.WzProperties.FirstOrDefault(wzprop => wzprop is WzStringProperty property && property.IsSpineAtlasResources)
                    : null;
                bIsObjectLayer = true;
            }
            else if (bIsObjectLayer) // load spine if the source is already the directory we need
            {
                string spineAtlasPath = ((WzStringProperty)source["spine"])?.GetString();
                if (spineAtlasPath != null) {
                    spineAtlas = source[spineAtlasPath + ".atlas"];
                }
            }
            else if (spineAniPath != null) {
                WzImageProperty spineSource = (WzImageProperty)source.Parent?.Parent["spine"]?[source.Name];

                string spineAtlasPath = ((WzStringProperty)spineSource["spine"])?.GetString();
                if (spineAtlasPath != null) {
                    spineAtlas = spineSource[spineAtlasPath + ".atlas"];
                }
            }
            else // simply check if 'spine' WzStringProperty exist, fix for Adele town
            {
                string spineAtlasPath = ((WzStringProperty)source["spine"])?.GetString();
                if (spineAtlasPath != null) {
                    spineAtlas = source[spineAtlasPath + ".atlas"];
                    bIsObjectLayer = true;
                }
            }

            if (spineAtlas != null) {
                if (spineAtlas is WzStringProperty stringObj) {
                    if (!stringObj.IsSpineAtlasResources)
                        return null;

                    string skeletonPropertyName = source is WzRawDataProperty ? source.Name : null;
                    WzSpineObject spineObject = new WzSpineObject(new WzSpineAnimationItem(stringObj, skeletonPropertyName));
                    try
                    {
                    spineObject.spineAnimationItem.LoadResources(device); //  load spine resources (this must happen after window is loaded)
                    if (spineObject.spineAnimationItem.SkeletonData == null)
                        return null;

                    spineObject.skeleton = new Skeleton(spineObject.spineAnimationItem.SkeletonData);
                    //spineObject.skeleton.R =153;
                    //spineObject.skeleton.G = 255;
                    //spineObject.skeleton.B = 0;
                    //spineObject.skeleton.A = 1f;

                    // Skin
                    foreach (Skin skin in spineObject.spineAnimationItem.SkeletonData.Skins) {
                        spineObject.skeleton.SetSkin(skin); // just set the first skin
                        break;
                    }

                    // Define mixing between animations.
                    spineObject.stateData = new AnimationStateData(spineObject.skeleton.Data);
                    spineObject.state = new AnimationState(spineObject.stateData);
                    if (!bIsObjectLayer)
                        spineObject.state.TimeScale = 0.1f;

                    if (spineAniPath != null) {
                        spineObject.state.SetAnimation(0, spineAniPath, true);
                    }
                    else {
                        int i = 0;
                        foreach (Spine.Animation animation in spineObject.spineAnimationItem.SkeletonData.Animations) {
                            spineObject.state.SetAnimation(i++, animation.Name, true);
                        }
                    }
                    return spineObject;
                    }
                    catch
                    {
                        spineObject.spineAnimationItem.Dispose();
                        throw;
                    }
                }
            }
            return null;
        }

        private static bool TryLoadDirectSpine41Frames(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device,
            string spineAniPath, ConcurrentBag<WzObject> usedProps, List<IDXObject> frames) {
            if (source is not WzSubProperty spineContainer ||
                !spineContainer.WzProperties.Any(wzprop => wzprop is WzStringProperty property && property.IsSpineAtlasResources)) {
                return false;
            }

            WzRawDataProperty skeletonProperty = SelectDirectSpineSkeleton(spineContainer, spineAniPath);
            if (skeletonProperty == null ||
                !DXSpine41Object.TryLoadRawSkeleton(skeletonProperty, device, spineAniPath, out DXSpine41Object.Spine41Object spine41Object)) {
                return false;
            }

            usedProps.Add(skeletonProperty);
            texturePool.OwnAnimationResource(spine41Object);
            frames.Add(new DXSpine41Object(spine41Object, x, y));
            return true;
        }

        private static WzRawDataProperty SelectDirectSpineSkeleton(WzImageProperty spineContainer, string spineAniPath) {
            if (!string.IsNullOrWhiteSpace(spineAniPath)) {
                WzRawDataProperty namedSkeleton = spineContainer.WzProperties
                    .OfType<WzRawDataProperty>()
                    .FirstOrDefault(property => property.Name.Equals(spineAniPath, StringComparison.OrdinalIgnoreCase));
                if (namedSkeleton != null)
                    return namedSkeleton;
            }

            return spineContainer.WzProperties
                .OfType<WzRawDataProperty>()
                .FirstOrDefault(property => property.Name.EndsWith(".skel", StringComparison.OrdinalIgnoreCase));
        }
        #endregion

        #region Reactor
        /// <summary>
        /// Create reactor item
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="reactorInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static ReactorItem CreateReactorFromProperty(TexturePool texturePool, RuntimeReactor runtimeReactor, IRuntimeAssetSource assetSource, GraphicsDevice device, ConcurrentBag<WzObject> usedProps) {
            return EffectLoader.CreateReactorFromProperty(texturePool, runtimeReactor, device, usedProps);
        }
        #endregion

        #region Portal
        /// <summary>
        /// Create portal item from Map.wz/MapHelper.img/portal/game
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="gameParent"></param>
        /// <param name="portalInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static PortalItem CreatePortalFromProperty(TexturePool texturePool, WzSubProperty gameParent, RuntimePortal portalInstance, GraphicsDevice device, ConcurrentBag<WzObject> usedProps) {
            return EffectLoader.CreatePortalFromProperty(texturePool, gameParent, portalInstance, device, usedProps);
        }
        #endregion

        #region Life
        /// <summary>
        /// Creates a MobItem with separate animations for each action (stand, move, fly, etc.)
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="mobInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static MobItem CreateMobFromProperty(TexturePool texturePool, RuntimeLife runtimeLife, IRuntimeAssetSource assetSource, float UserScreenScaleFactor, GraphicsDevice device, SoundManager soundManager, ConcurrentBag<WzObject> usedProps) {
            return LifeLoader.CreateMobFromProperty(texturePool, runtimeLife, assetSource, UserScreenScaleFactor, device, soundManager, usedProps);
        }

        /// <summary>
        /// NPC
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="npcInstance"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static NpcItem CreateNpcFromProperty(
            TexturePool texturePool,
            RuntimeLife runtimeLife,
            IRuntimeAssetSource assetSource,
            float UserScreenScaleFactor,
            GraphicsDevice device,
            ConcurrentBag<WzObject> usedProps,
            CharacterGender? localPlayerGender = null,
            bool hasQuestCheckContext = false,
            Func<int, QuestStateType> questStateProvider = null,
            Func<int, string> questRecordValueProvider = null) {
            return LifeLoader.CreateNpcFromProperty(
                texturePool,
                runtimeLife,
                assetSource,
                UserScreenScaleFactor,
                device,
                usedProps,
                localPlayerGender: localPlayerGender,
                hasQuestCheckContext: hasQuestCheckContext,
                questStateProvider: questStateProvider,
                questRecordValueProvider: questRecordValueProvider);
        }
        #endregion

        #region UI
        /// <summary>
        /// Draws the status bar UI (Character health, level, name)
        /// </summary>
        /// <param name="uiStatusBar">UI.wz/StatusBar.img</param>
        /// <param name="uiStatusBar2">UI.wz/StatusBar2.img</param>
        /// <param name="device"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="renderParams"></param>
        /// <param name="soundUIImage"></param>
        /// <param name="bBigBang"></param>
        /// <returns></returns>
        public static Tuple<StatusBarUI, StatusBarChatUI> CreateStatusBarFromProperty(WzImage uiStatusBar, WzImage uiStatusBar2, WzImage uiStatusBar3, WzImage uiBasic, WzImage uiBuffIcon, GraphicsDevice device, float UserScreenScaleFactor, RenderParameters renderParams, WzImage soundUIImage, bool bBigBang, IRuntimeAssetSource assetSource, UILoaderResourceCache resourceCache) {
            return UILoader.CreateStatusBarFromProperty(uiStatusBar, uiStatusBar2, uiStatusBar3, uiBasic, uiBuffIcon, device, UserScreenScaleFactor, renderParams, soundUIImage, bBigBang, assetSource, resourceCache);
        }

        /// <summary>
        /// Draws the frame and the UI of the minimap.
        /// TODO: This whole thing needs to be dramatically simplified via further abstraction to keep it noob-proof :(
        /// </summary>
        /// <param name="uiWindow1Image">UI.wz/UIWindow1.img pre-bb</param>
        /// <param name="uiWindow2Image">UI.wz/UIWindow2.img post-bb</param>
        /// <param name="uiBasicImage">UI.wz/Basic.img</param>
        /// <param name="minimapData"></param>
        /// <param name="device"></param>
        /// <param name="UserScreenScaleFactor">The scale factor of the window (DPI)</param>
        /// <param name="soundUIImage">Sound.wz/UI.img</param>
        /// <param name="bBigBang">Big bang update</param>
        /// <returns></returns>
        public static MinimapUI CreateMinimapFromProperty(WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiBasicImage, RuntimeMinimapData minimapData, GraphicsDevice device, float UserScreenScaleFactor, WzImage soundUIImage, bool bBigBang, UILoaderResourceCache resourceCache) {
            return CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, null, uiBasicImage, minimapData, device, UserScreenScaleFactor, soundUIImage, bBigBang, resourceCache);
        }

        public static MinimapUI CreateMinimapFromProperty(WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiMapImage, WzImage uiBasicImage, RuntimeMinimapData minimapData, GraphicsDevice device, float UserScreenScaleFactor, WzImage soundUIImage, bool bBigBang, UILoaderResourceCache resourceCache) {
            return UILoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiMapImage, uiBasicImage, minimapData, device, UserScreenScaleFactor, soundUIImage, bBigBang, resourceCache);
        }

        /// <summary>
        /// Creates mouse cursor from UI.wz/Basic.img/Cursor
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static MouseCursorItem CreateMouseCursorFromProperty(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ConcurrentBag<WzObject> usedProps, bool flip) {
            return UILoader.CreateMouseCursorFromProperty(texturePool, source, x, y, device, usedProps, flip);
        }
        #endregion

        #region Tooltip
        /// <summary>
        /// Tooltip
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="UserScreenScaleFactor">The scale factor of the window (DPI)</param>
        /// <param name="farmFrameParent"></param>
        /// <param name="tooltip"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static TooltipItem CreateTooltipFromProperty(TexturePool texturePool, float UserScreenScaleFactor, WzSubProperty farmFrameParent, RuntimeTooltipDefinition tooltip, GraphicsDevice device) {
            // Wz frames
            using System.Drawing.Bitmap c = ((WzCanvasProperty)farmFrameParent?["c"])?.GetLinkedWzCanvasBitmap();
            using System.Drawing.Bitmap e = ((WzCanvasProperty)farmFrameParent?["e"])?.GetLinkedWzCanvasBitmap();
            using System.Drawing.Bitmap n = ((WzCanvasProperty)farmFrameParent?["n"])?.GetLinkedWzCanvasBitmap();
            using System.Drawing.Bitmap s = ((WzCanvasProperty)farmFrameParent?["s"])?.GetLinkedWzCanvasBitmap();
            using System.Drawing.Bitmap w = ((WzCanvasProperty)farmFrameParent?["w"])?.GetLinkedWzCanvasBitmap();
            using System.Drawing.Bitmap ne = ((WzCanvasProperty)farmFrameParent?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            using System.Drawing.Bitmap nw = ((WzCanvasProperty)farmFrameParent?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            using System.Drawing.Bitmap se = ((WzCanvasProperty)farmFrameParent?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            using System.Drawing.Bitmap sw = ((WzCanvasProperty)farmFrameParent?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left


            // tooltip property
            string title = tooltip.Title;
            string desc = tooltip.Description;
            if (desc != null)
            {
                desc = desc.Replace("\\n\\n", "\n \n"); // Add a space between consecutive newlines, due to how the DrawString method handles newline characters in the Graphics class
            }

            string renderText = string.Format("{0}{1}{2}", title, Environment.NewLine, desc);

            //System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(230, 17, 54, 82); // pre V patch (dark blue theme used post-bb), leave this here in case someone needs it
            System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(255, 17, 17, 17); // post V patch (dark black theme used), use color picker on paint via image extracted from WZ if you need to get it
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;
            const int WIDTH_PADDING = 10;
            const int HEIGHT_PADDING = 6;

            // Create
            using (System.Drawing.Font font = new System.Drawing.Font(GLOBAL_FONT, TOOLTIP_FONTSIZE / UserScreenScaleFactor)) {
                using System.Drawing.Bitmap bmp_tooltip = CreateTextTooltipBitmap(
                    renderText,
                    font,
                    WIDTH_PADDING,
                    HEIGHT_PADDING,
                    (graphics, effectiveWidth, effectiveHeight) =>
                        UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, c, 0, effectiveWidth, effectiveHeight),
                    color_foreGround);
                Texture2D tooltipTexture;
                using (GraphicsResourceCreationScope.Begin(texturePool.OwnGraphicsResource))
                {
                    tooltipTexture = bmp_tooltip.ToTexture2D(device);
                }
                IDXObject dxObj = new DXObject(tooltip.Bounds.X, tooltip.Bounds.Y, tooltipTexture, 0);
                TooltipItem item = new TooltipItem(tooltip, dxObj);

                return item;
            }
        }

        /// <summary>
        /// Draws the name tooltip for NPC and mobs
        /// </summary>
        /// <param name="renderText"></param>
        /// <param name="x">The life object's X position.</param>
        /// <param name="y">The life object's Y position.</param>
        /// <param name="color_foreGround"></param>
        /// <param name="texturePool"></param>
        /// <param name="UserScreenScaleFactor"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static NameTooltipItem CreateNPCMobNameTooltip(string renderText, int x, int y, System.Drawing.Color color_foreGround,
            TexturePool texturePool, float UserScreenScaleFactor, GraphicsDevice device)
        {
            //System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(230, 17, 54, 82); // pre V patch (dark blue theme used post-bb), leave this here in case someone needs it
            System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(200, 17, 17, 17); // post V patch (dark black theme used), use color picker on paint via image extracted from WZ if you need to get it

            const int WIDTH_PADDING = 6; // use even numbers or it gets odd
            const int HEIGHT_PADDING = 2;

            // Create
            using (System.Drawing.Font font = new System.Drawing.Font(GLOBAL_FONT, TOOLTIP_FONTSIZE / UserScreenScaleFactor))
            {
                using System.Drawing.Bitmap bmp_tooltip = CreateTextTooltipBitmap(
                    renderText,
                    font,
                    WIDTH_PADDING,
                    HEIGHT_PADDING,
                    (graphics, effectiveWidth, effectiveHeight) =>
                        UIFrameHelper.DrawUIFrame(graphics, color_bgFill, effectiveWidth, effectiveHeight),
                    color_foreGround);

                int tooltipShiftX = x - (bmp_tooltip.Width / 2);

                Texture2D tooltipTexture;
                using (GraphicsResourceCreationScope.Begin(texturePool.OwnGraphicsResource))
                {
                    tooltipTexture = bmp_tooltip.ToTexture2D(device);
                }
                IDXObject dxObj = new DXObject(tooltipShiftX, y, tooltipTexture, 0);
                NameTooltipItem item = new NameTooltipItem(dxObj);

                return item;
            }
        }
        #endregion
    }
}
