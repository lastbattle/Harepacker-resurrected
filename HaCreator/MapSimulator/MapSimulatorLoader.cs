using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.UI;
using HaCreator.Wz;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // Constants
        private const string GLOBAL_FONT = "Arial";
        private const float TOOLTIP_FONTSIZE = 9.25f; // thankie willified, ya'll be remembered forever here <3

        private const float MINIMAP_STREETNAME_TOOLTIP_FONTSIZE = 10f;

        /// <summary>
        /// Create map simulator board with seamless map transitions.
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <param name="titleName"></param>
        /// <param name="loadMapCallback">Optional callback to load a map by ID. Returns (Board, titleName) tuple or null if map not found. Enables seamless portal teleportation.</param>
        /// <param name="onComplete">Optional callback invoked on the UI thread when the simulator exits.</param>
        /// <returns></returns>
        public static void CreateAndShowMapSimulator(Board mapBoard, string titleName, Func<int, Tuple<Board, string>> loadMapCallback = null, Action onComplete = null) {
            if (mapBoard.MiniMap == null)
                mapBoard.RegenerateMinimap();

            Thread thread = new Thread(() => {
                var mapSimulator = new MapSimulator(mapBoard, titleName);

                // Set the callback for seamless map transitions
                if (loadMapCallback != null)
                {
                    mapSimulator.SetLoadMapCallback(loadMapCallback);
                }

                mapSimulator.Run();

                // Signal completion on the UI thread
                onComplete?.Invoke();
            }) {
                Priority = ThreadPriority.Highest
            };
            thread.Start();
            // Don't Join() - let the game run independently to avoid deadlock with Dispatcher.Invoke
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
        internal static List<IDXObject> LoadFrames(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, string spineAni = null) {
            List<IDXObject> frames = new List<IDXObject>();

            source = WzInfoTools.GetRealProperty(source);

            if (source is WzSubProperty property1 && property1.WzProperties.Count == 1) {
                source = property1.WzProperties[0];
            }

            if (source is WzCanvasProperty property) //one-frame
            {
                bool bLoadedSpine = LoadSpineMapObjectItem(source, source, device, spineAni);
                if (!bLoadedSpine) {
                    string canvasBitmapPath = property.FullPath;
                    Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                    if (textureFromCache != null) {
                        source.MSTag = textureFromCache;
                    }
                    else {
                        var bitmap = property.GetLinkedWzCanvasBitmap();
                        if (bitmap != null)
                        {
                            source.MSTag = bitmap.ToTexture2D(device);
                            // add to cache
                            texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)source.MSTag);
                        }
                    }
                }
                usedProps.Add(source);

                if (source.MSTagSpine != null) {
                    WzSpineObject spineObject = (WzSpineObject)source.MSTagSpine;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXSpineObject(spineObject, x, y, origin));
                }
                else if (source.MSTag != null) {
                    Texture2D texture = (Texture2D)source.MSTag;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
                else // fallback
                {
                    Texture2D texture = Properties.Resources.placeholder.ToTexture2D(device);
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
            }
            else if (source is WzSubProperty) // animated
            {
                WzImageProperty _frameProp;
                int i = 0;

                while ((_frameProp = WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null) {
                    if (_frameProp is WzSubProperty) // issue with 867119250
                    {
                        frames.AddRange(LoadFrames(texturePool, _frameProp, x, y, device, ref usedProps, null));
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

                        int delay = (int)InfoTool.GetOptionalInt(frameProp["delay"], 100);

                        bool bLoadedSpine = LoadSpineMapObjectItem((WzImageProperty)frameProp.Parent, frameProp, device, spineAni);
                        if (!bLoadedSpine) {
                            if (frameProp.MSTag == null) {
                                string canvasBitmapPath = frameProp.FullPath;
                                Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                                if (textureFromCache != null) {
                                    frameProp.MSTag = textureFromCache;
                                }
                                else {
                                    frameProp.MSTag = frameProp.GetLinkedWzCanvasBitmap().ToTexture2D(device);

                                    // add to cache
                                    texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)frameProp.MSTag);
                                }
                            }
                        }
                        usedProps.Add(frameProp);

                        if (frameProp.MSTagSpine != null) {
                            WzSpineObject spineObject = (WzSpineObject)frameProp.MSTagSpine;
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXSpineObject(spineObject, x, y, origin, delay));
                        }
                        else if (frameProp.MSTag != null) {
                            Texture2D texture = (Texture2D)frameProp.MSTag;
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
                        }
                        else {
                            Texture2D texture = Properties.Resources.placeholder.ToTexture2D(device);
                            System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                            frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
                        }
                    }
                }
            }
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
            Point mapCenter, GraphicsDevice device, ref List<WzObject> usedProps, bool flip) {

            BaseDXDrawableItem mapItem = new BaseDXDrawableItem(LoadFrames(texturePool, source, x, y, device, ref usedProps), flip);
            return mapItem;
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
        public static BackgroundItem CreateBackgroundFromProperty(TexturePool texturePool, WzImageProperty source, BackgroundInstance bgInstance, GraphicsDevice device, ref List<WzObject> usedProps, bool flip) {
            List<IDXObject> frames = LoadFrames(texturePool, source, bgInstance.BaseX, bgInstance.BaseY, device, ref usedProps, bgInstance.SpineAni);
            if (frames.Count == 0) {
                string error = string.Format("[MapSimulatorLoader] 0 frames loaded for bg texture from src: '{0}'", source.FullPath); // Back_003.wz\\BM3_3.img\\spine\\0

                ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                return null;
            }

            if (frames.Count == 1) {
                return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames[0], flip, bgInstance.screenMode);
            }
            return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames, flip, bgInstance.screenMode);
        }

        #region Spine
        /// <summary>
        /// Load spine object from WzImageProperty (bg, map item)
        /// </summary>
        /// <param name="source"></param>
        /// <param name="prop"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        private static bool LoadSpineMapObjectItem(WzImageProperty source, WzImageProperty prop, GraphicsDevice device, string spineAniPath = null) {
            WzImageProperty spineAtlas = null;

            bool bIsObjectLayer = source.Parent.Name == "spine";
            if (bIsObjectLayer) // load spine if the source is already the directory we need
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
                        return false;

                    WzSpineObject spineObject = new WzSpineObject(new WzSpineAnimationItem(stringObj));

                    spineObject.spineAnimationItem.LoadResources(device); //  load spine resources (this must happen after window is loaded)
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
                    prop.MSTagSpine = spineObject;
                    return true;
                }
            }
            return false;
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
        public static ReactorItem CreateReactorFromProperty(TexturePool texturePool, ReactorInstance reactorInstance, GraphicsDevice device, ref List<WzObject> usedProps) {
            return EffectLoader.CreateReactorFromProperty(texturePool, reactorInstance, device, ref usedProps);
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
        public static PortalItem CreatePortalFromProperty(TexturePool texturePool, WzSubProperty gameParent, PortalInstance portalInstance, GraphicsDevice device, ref List<WzObject> usedProps) {
            return EffectLoader.CreatePortalFromProperty(texturePool, gameParent, portalInstance, device, ref usedProps);
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
        public static MobItem CreateMobFromProperty(TexturePool texturePool, MobInstance mobInstance, float UserScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps) {
            return LifeLoader.CreateMobFromProperty(texturePool, mobInstance, UserScreenScaleFactor, device, ref usedProps);
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
        public static NpcItem CreateNpcFromProperty(TexturePool texturePool, NpcInstance npcInstance, float UserScreenScaleFactor, GraphicsDevice device, ref List<WzObject> usedProps) {
            return LifeLoader.CreateNpcFromProperty(texturePool, npcInstance, UserScreenScaleFactor, device, ref usedProps);
        }
        #endregion

        #region UI
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
        public static Tuple<StatusBarUI, StatusBarChatUI> CreateStatusBarFromProperty(WzImage uiStatusBar, WzImage uiStatusBar2, Board mapBoard, GraphicsDevice device, float UserScreenScaleFactor, RenderParameters renderParams, WzImage soundUIImage, bool bBigBang) {
            return UILoader.CreateStatusBarFromProperty(uiStatusBar, uiStatusBar2, mapBoard, device, UserScreenScaleFactor, renderParams, soundUIImage, bBigBang);
        }

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
        public static MinimapUI CreateMinimapFromProperty(WzImage uiWindow1Image, WzImage uiWindow2Image, WzImage uiBasicImage, Board mapBoard, GraphicsDevice device, float UserScreenScaleFactor, string MapName, string StreetName, WzImage soundUIImage, bool bBigBang) {
            return UILoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiBasicImage, mapBoard, device, UserScreenScaleFactor, MapName, StreetName, soundUIImage, bBigBang);
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
        public static MouseCursorItem CreateMouseCursorFromProperty(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, bool flip) {
            return UILoader.CreateMouseCursorFromProperty(texturePool, source, x, y, device, ref usedProps, flip);
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
        public static TooltipItem CreateTooltipFromProperty(TexturePool texturePool, float UserScreenScaleFactor, WzSubProperty farmFrameParent, ToolTipInstance tooltip, GraphicsDevice device) {
            // Wz frames
            System.Drawing.Bitmap c = ((WzCanvasProperty)farmFrameParent?["c"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap cover = ((WzCanvasProperty)farmFrameParent?["cover"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap e = ((WzCanvasProperty)farmFrameParent?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap n = ((WzCanvasProperty)farmFrameParent?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap s = ((WzCanvasProperty)farmFrameParent?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap w = ((WzCanvasProperty)farmFrameParent?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap ne = ((WzCanvasProperty)farmFrameParent?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            System.Drawing.Bitmap nw = ((WzCanvasProperty)farmFrameParent?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            System.Drawing.Bitmap se = ((WzCanvasProperty)farmFrameParent?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            System.Drawing.Bitmap sw = ((WzCanvasProperty)farmFrameParent?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left


            // tooltip property
            string title = tooltip.Title;
            string desc = tooltip.Desc;
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
                System.Drawing.Graphics graphics_dummy = System.Drawing.Graphics.FromImage(new System.Drawing.Bitmap(1, 1)); // dummy image just to get the Graphics object for measuring string
                System.Drawing.SizeF tooltipSize = graphics_dummy.MeasureString(renderText, font);

                int effective_width = (int)tooltipSize.Width + WIDTH_PADDING;
                int effective_height = (int)tooltipSize.Height + HEIGHT_PADDING;

                System.Drawing.Bitmap bmp_tooltip = new System.Drawing.Bitmap(effective_width, effective_height);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_tooltip)) {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, c, 0, effective_width, effective_height);

                    // Text
                    graphics.DrawString(renderText, font, new System.Drawing.SolidBrush(color_foreGround), WIDTH_PADDING / 2, HEIGHT_PADDING / 2);
                    graphics.Flush();
                }
                IDXObject dxObj = new DXObject(tooltip.X, tooltip.Y, bmp_tooltip.ToTexture2D(device), 0);
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
                System.Drawing.Graphics graphics_dummy = System.Drawing.Graphics.FromImage(new System.Drawing.Bitmap(1, 1)); // dummy image just to get the Graphics object for measuring string
                System.Drawing.SizeF tooltipSize = graphics_dummy.MeasureString(renderText, font);

                int effective_width = (int)tooltipSize.Width + WIDTH_PADDING;
                int effective_height = (int)tooltipSize.Height + HEIGHT_PADDING;

                System.Drawing.Bitmap bmp_tooltip = new System.Drawing.Bitmap(effective_width, effective_height);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_tooltip))
                {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, effective_width, effective_height);

                    // Text
                    graphics.DrawString(renderText, font, new System.Drawing.SolidBrush(color_foreGround), (WIDTH_PADDING / 2), HEIGHT_PADDING / 2);
                    graphics.Flush();
                }

                int tooltipShiftX = (x - (effective_width / 2));

                IDXObject dxObj = new DXObject(tooltipShiftX, y, bmp_tooltip.ToTexture2D(device), 0);
                NameTooltipItem item = new NameTooltipItem(dxObj);

                return item;
            }
        }
        #endregion

        private static string DumpFhList(List<FootholdLine> fhs) {
            string res = "";
            foreach (FootholdLine fh in fhs)
                res += fh.FirstDot.X + "," + fh.FirstDot.Y + " : " + fh.SecondDot.X + "," + fh.SecondDot.Y + "\r\n";
            return res;
        }
    }
}
