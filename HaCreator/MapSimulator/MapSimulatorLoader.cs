using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.DX;
using HaCreator.MapSimulator.Objects;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaCreator.MapSimulator.Objects.UIObject;
using HaCreator.Wz;
using HaRepacker.Converter;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HaCreator.MapSimulator
{
    public class MapSimulatorLoader
    {
        /// <summary>
        /// Create map simulator board
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <param name="titleName"></param>
        /// <returns></returns>
        public static MapSimulator CreateAndShowMapSimulator(Board mapBoard, string titleName)
        {
            if (mapBoard.MiniMap == null)
                mapBoard.RegenerateMinimap();

            MapSimulator mapSimulator = null;

            Thread thread = new Thread(() =>
            {
                mapSimulator = new MapSimulator(mapBoard, titleName);
                mapSimulator.Run();
            });
            thread.Priority = ThreadPriority.Highest;

            thread.Start();
            thread.Join();

            return mapSimulator;
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
        private static List<IDXObject> LoadFrames(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, string spineAni = null)
        {
            List<IDXObject> frames = new List<IDXObject>();

            source = WzInfoTools.GetRealProperty(source);

            if (source is WzSubProperty property1 && property1.WzProperties.Count == 1)
            {
                source = property1.WzProperties[0];
            }

            if (source is WzCanvasProperty property) //one-frame
            {
                bool bLoadedSpine = LoadSpineMapObjectItem(source, source, device, spineAni);
                if (!bLoadedSpine)
                {
                    string canvasBitmapPath = property.FullPath;
                    Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                    if (textureFromCache != null)
                    {
                        source.MSTag = textureFromCache;
                    }
                    else
                    {
                        source.MSTag = BoardItem.TextureFromBitmap(device, property.GetLinkedWzCanvasBitmap());

                        // add to cache
                        texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)source.MSTag);
                    }
                }
                usedProps.Add(source);

                if (source.MSTagSpine != null)
                {
                    WzSpineObject spineObject = (WzSpineObject)source.MSTagSpine;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXSpineObject(spineObject, x, y, origin));
                }
                else if (source.MSTag != null)
                {
                    Texture2D texture = (Texture2D)source.MSTag;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
                else // fallback
                {
                    Texture2D texture = BoardItem.TextureFromBitmap(device, Properties.Resources.placeholder);
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture));
                }
            }
            else if (source is WzSubProperty) // animated
            {
                WzCanvasProperty frameProp;
                int i = 0;

                while ((frameProp = (WzCanvasProperty)WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null)
                {
                    int delay = (int)InfoTool.GetOptionalInt(frameProp["delay"], 100);

                    bool bLoadedSpine = LoadSpineMapObjectItem((WzImageProperty)frameProp.Parent, frameProp, device, spineAni);
                    if (!bLoadedSpine)
                    {
                        if (frameProp.MSTag == null)
                        {
                            string canvasBitmapPath = frameProp.FullPath;
                            Texture2D textureFromCache = texturePool.GetTexture(canvasBitmapPath);
                            if (textureFromCache != null)
                            {
                                frameProp.MSTag = textureFromCache;
                            }
                            else
                            {
                                frameProp.MSTag = BoardItem.TextureFromBitmap(device, frameProp.GetLinkedWzCanvasBitmap());

                                // add to cache
                                texturePool.AddTextureToPool(canvasBitmapPath, (Texture2D)frameProp.MSTag);
                            }
                        }
                    }
                    usedProps.Add(frameProp);

                    if (frameProp.MSTagSpine != null)
                    {
                        WzSpineObject spineObject = (WzSpineObject)frameProp.MSTagSpine;
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                        frames.Add(new DXSpineObject(spineObject, x, y, origin, delay));
                    }
                    else if (frameProp.MSTag != null)
                    {
                        Texture2D texture = (Texture2D)frameProp.MSTag;
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                        frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
                    }
                    else
                    {
                        Texture2D texture = BoardItem.TextureFromBitmap(device, Properties.Resources.placeholder);
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                        frames.Add(new DXObject(x - (int)origin.X, y - (int)origin.Y, texture, delay));
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
        public static BaseItem CreateMapItemFromProperty(TexturePool texturePool, WzImageProperty source, int x, int y, Point mapCenter, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            BaseItem mapItem = new BaseItem(LoadFrames(texturePool, source, x, y, device, ref usedProps), flip);
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
        public static BackgroundItem CreateBackgroundFromProperty(TexturePool texturePool, WzImageProperty source, BackgroundInstance bgInstance, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            List<IDXObject> frames = LoadFrames(texturePool, source, bgInstance.BaseX, bgInstance.BaseY, device, ref usedProps, bgInstance.SpineAni);
            if (frames.Count == 1)
            {
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
        private static bool LoadSpineMapObjectItem(WzImageProperty source, WzImageProperty prop, GraphicsDevice device, string spineAniPath = null)
        {
            WzImageProperty spineAtlas = null;

            bool bIsObjectLayer = source.Parent.Name == "spine";
            if (bIsObjectLayer) // load spine if the source is already the directory we need
            {
                string spineAtlasPath = ((WzStringProperty)source["spine"])?.GetString();
                if (spineAtlasPath != null)
                {
                    spineAtlas = source[spineAtlasPath + ".atlas"];
                }
            }
            else if (spineAniPath != null)
            {
                WzImageProperty spineSource = (WzImageProperty)source.Parent?.Parent["spine"]?[source.Name];

                string spineAtlasPath = ((WzStringProperty)spineSource["spine"])?.GetString();
                if (spineAtlasPath != null)
                {
                    spineAtlas = spineSource[spineAtlasPath + ".atlas"];
                }
            }

            if (spineAtlas != null)
            {
                if (spineAtlas is WzStringProperty)
                {
                    WzStringProperty stringObj = (WzStringProperty)spineAtlas;
                    if (!stringObj.IsSpineAtlasResources)
                        return false;

                    try
                    {
                        WzSpineObject spineObject = new WzSpineObject(new WzSpineAnimationItem(stringObj));

                        spineObject.spineAnimationItem.LoadResources(device); //  load spine resources (this must happen after window is loaded)
                        spineObject.skeleton = new Skeleton(spineObject.spineAnimationItem.SkeletonData);
                        //spineObject.skeleton.R =153;
                        //spineObject.skeleton.G = 255;
                        //spineObject.skeleton.B = 0;
                        //spineObject.skeleton.A = 1f;

                        // Skin
                        foreach (Skin skin in spineObject.spineAnimationItem.SkeletonData.Skins)
                        {
                            spineObject.skeleton.SetSkin(skin); // just set the first skin
                            break;
                        }

                        // Define mixing between animations.
                        spineObject.stateData = new AnimationStateData(spineObject.skeleton.Data);
                        spineObject.state = new AnimationState(spineObject.stateData);
                        if (!bIsObjectLayer)
                            spineObject.state.TimeScale = 0.1f;

                        if (spineAniPath != null)
                        {
                            spineObject.state.SetAnimation(0, spineAniPath, true);
                        }
                        else
                        {
                            int i = 0;
                            foreach (Animation animation in spineObject.spineAnimationItem.SkeletonData.Animations)
                            {
                                spineObject.state.SetAnimation(i++, animation.Name, true);
                            }
                        }
                        prop.MSTagSpine = spineObject;
                        return true;
                    }
                    catch (Exception e)
                    {
                    }
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
        public static ReactorItem CreateReactorFromProperty(TexturePool texturePool, ReactorInstance reactorInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            ReactorInfo reactorInfo = (ReactorInfo)reactorInstance.BaseInfo;
            WzImage linkedReactorImage = reactorInfo.LinkedWzImage;

            List<IDXObject> frames = new List<IDXObject>();

            WzImageProperty framesImage = (WzImageProperty) linkedReactorImage["0"]?["0"];
            if (framesImage != null)
            {
                frames = LoadFrames(texturePool, framesImage, reactorInstance.X, reactorInstance.Y, device, ref usedProps);
            }
            if (frames.Count == 0)
                return null;
            return new ReactorItem(reactorInstance, frames);
        }
        #endregion

        #region Portal       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="gameParent"></param>
        /// <param name="portalInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static PortalItem CreatePortalFromProperty(TexturePool texturePool, WzSubProperty gameParent, PortalInstance portalInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            PortalInfo portalInfo = (PortalInfo)portalInstance.BaseInfo;

            if (portalInstance.pt == PortalType.PORTALTYPE_STARTPOINT ||
                portalInstance.pt == PortalType.PORTALTYPE_INVISIBLE ||
                //portalInstance.pt == PortalType.PORTALTYPE_CHANGABLE_INVISIBLE ||
                portalInstance.pt == PortalType.PORTALTYPE_SCRIPT_INVISIBLE ||
                portalInstance.pt == PortalType.PORTALTYPE_SCRIPT ||
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION ||
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION_SCRIPT ||
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION_CUSTOM_IMPACT || // springs in Mechanical grave 350040240
                portalInstance.pt == PortalType.PORTALTYPE_COLLISION_VERTICAL_JUMP) // vertical spring actually
                return null;

            List<IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            //string portalType = portalInstance.pt;
            //int portalId = Program.InfoManager.PortalIdByType[portalInstance.pt];

            WzSubProperty portalTypeProperty = (WzSubProperty)gameParent[portalInstance.pt];
            if (portalTypeProperty == null)
            {
                portalTypeProperty = (WzSubProperty)gameParent["pv"];
            } 
            else
            {
                // Support for older versions of MapleStory where 'pv' is a subproperty for the image frame than a collection of subproperty of frames
                if (portalTypeProperty["0"] is WzCanvasProperty)
                {
                    frames.AddRange(LoadFrames(texturePool, portalTypeProperty, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    portalTypeProperty = null;
                }
            }

            if (portalTypeProperty != null)
            {
                WzSubProperty portalImageProperty = (WzSubProperty)portalTypeProperty[portalInstance.image == null ? "default" : portalInstance.image];

                if (portalImageProperty != null)
                {
                    WzSubProperty framesPropertyParent;
                    if (portalImageProperty["portalContinue"] != null)
                        framesPropertyParent = (WzSubProperty)portalImageProperty["portalContinue"];
                    else
                        framesPropertyParent = (WzSubProperty)portalImageProperty;

                    if (framesPropertyParent != null)
                    {
                        frames.AddRange(LoadFrames(texturePool, framesPropertyParent, portalInstance.X, portalInstance.Y, device, ref usedProps));
                    }
                }
            }
            if (frames.Count == 0)
                return null;
            return new PortalItem(portalInstance, frames);
        }
        #endregion

        #region Life
        /// <summary>
        /// 
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="mobInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static MobItem CreateMobFromProperty(TexturePool texturePool, MobInstance mobInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            MobInfo mobInfo = (MobInfo)mobInstance.BaseInfo;
            WzImage source = mobInfo.LinkedWzImage;

            List <IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                WzSubProperty mobStateProperty = (WzSubProperty)childProperty;
                switch (mobStateProperty.Name)
                {
                    case "info": // info/speak/0 WzStringProperty
                        {
                            break;
                        }
                    default:
                        {
                            frames.AddRange(LoadFrames(texturePool, mobStateProperty, mobInstance.X, mobInstance.Y, device, ref usedProps));
                            break;
                        }
                }
            }
            return new MobItem(mobInstance, frames);
        }

        /// <summary>
        /// NPC
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="npcInstance"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static NpcItem CreateNpcFromProperty(TexturePool texturePool, NpcInstance npcInstance, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            NpcInfo npcInfo = (NpcInfo)npcInstance.BaseInfo;
            WzImage source = npcInfo.LinkedWzImage;

            List<IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

            foreach (WzImageProperty childProperty in source.WzProperties)
            {
                WzSubProperty npcStateProperty = (WzSubProperty)childProperty;
                switch (npcStateProperty.Name)
                {
                    case "info": // info/speak/0 WzStringProperty
                        {
                            break;
                        }
                    default:
                        {
                            frames.AddRange(LoadFrames(texturePool, npcStateProperty, npcInstance.X, npcInstance.Y, device, ref usedProps));
                            break;
                        }
                }
            }
            return new NpcItem(npcInstance, frames);
        }
        #endregion

        #region UI
        /// <summary>
        /// 
        /// </summary>
        /// <param name="minimapFrameProperty">UI.wz/UIWindow2.img/MiniMap</param>
        /// <param name="mapBoard"></param>
        /// <param name="device"></param>
        /// <param name="MapName">The map name. i.e The Hill North</param>
        /// <param name="StreetName">The street name. i.e Hidden street</param>
        /// <returns></returns>
        public static MinimapItem CreateMinimapFromProperty(WzSubProperty minimapFrameProperty, Board mapBoard, GraphicsDevice device, string MapName, string StreetName)
        {
            WzSubProperty maxMapProperty = (WzSubProperty) minimapFrameProperty["MaxMap"];
            WzSubProperty miniMapProperty = (WzSubProperty)minimapFrameProperty["MinMap"];
            WzSubProperty maxMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MaxMapMirror"]; // for Zero maps
            WzSubProperty miniMapMirrorProperty = (WzSubProperty)minimapFrameProperty["MinMapMirror"]; // for Zero maps

            // Wz frames
            System.Drawing.Bitmap c = ((WzCanvasProperty)maxMapProperty?["c"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap e = ((WzCanvasProperty)maxMapProperty?["e"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap n = ((WzCanvasProperty)maxMapProperty?["n"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap s = ((WzCanvasProperty)maxMapProperty?["s"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap w = ((WzCanvasProperty)maxMapProperty?["w"])?.GetLinkedWzCanvasBitmap();
            System.Drawing.Bitmap ne = ((WzCanvasProperty)maxMapProperty?["ne"])?.GetLinkedWzCanvasBitmap(); // top right
            System.Drawing.Bitmap nw = ((WzCanvasProperty)maxMapProperty?["nw"])?.GetLinkedWzCanvasBitmap(); // top left
            System.Drawing.Bitmap se = ((WzCanvasProperty)maxMapProperty?["se"])?.GetLinkedWzCanvasBitmap(); // bottom right
            System.Drawing.Bitmap sw = ((WzCanvasProperty)maxMapProperty?["sw"])?.GetLinkedWzCanvasBitmap(); // bottom left

            // Constants
            const string TOOLTIP_FONT = "Arial";
            const float TOOLTIP_FONTSIZE = 10f;
            System.Drawing.Color color_bgFill = System.Drawing.Color.Transparent;
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;

            // Dots pixel 
            System.Drawing.Bitmap bmp_DotPixel = new System.Drawing.Bitmap(2, 4);
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_DotPixel))
            {
                graphics.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.Yellow), new System.Drawing.RectangleF(0, 0, bmp_DotPixel.Width, bmp_DotPixel.Height));
                graphics.Flush();
            }
            IDXObject dxObj_miniMapPixel = new DXObject(0, n.Height, BoardItem.TextureFromBitmap(device, bmp_DotPixel), 0);
            BaseItem item_pixelDot = new BaseItem(dxObj_miniMapPixel, false);

            // Map background image
            System.Drawing.Bitmap miniMapImage = mapBoard.MiniMap; // the original minimap image without UI frame overlay
            int effective_width = miniMapImage.Width + e.Width + w.Width;
            int effective_height = miniMapImage.Height + n.Height + s.Height;

            using (System.Drawing.Font font = new System.Drawing.Font(TOOLTIP_FONT, TOOLTIP_FONTSIZE))
            {
                System.Drawing.Bitmap miniMapUIImage = new System.Drawing.Bitmap(effective_width, effective_height);

                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(miniMapUIImage))
                {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, null, effective_width, effective_height);

                    graphics.DrawString(
                        string.Format("{0}{1}{2}", StreetName, Environment.NewLine, MapName), 
                        font, new System.Drawing.SolidBrush(color_foreGround), 50, 20);

                    // Map mark
                    if (Program.InfoManager.MapMarks.ContainsKey(mapBoard.MapInfo.mapMark))
                    {
                        System.Drawing.Bitmap mapMark = Program.InfoManager.MapMarks[mapBoard.MapInfo.mapMark];
                        graphics.DrawImage(mapMark.ToImage(), 7, 17);
                    }

                    // Map image
                    graphics.DrawImage(miniMapImage, 10, n.Height);

                    graphics.Flush();
                }
                Texture2D texturer_miniMap = BoardItem.TextureFromBitmap(device, miniMapUIImage);

                IDXObject dxObj = new DXObject(0, 0, texturer_miniMap, 0);
                MinimapItem item = new MinimapItem(dxObj, item_pixelDot);

                return item;
            }
        }

        /// <summary>
        /// Tooltip
        /// </summary>
        /// <param name="texturePool"></param>
        /// <param name="farmFrameParent"></param>
        /// <param name="tooltip"></param>
        /// <param name="device"></param>
        /// <returns></returns>
        public static TooltipItem CreateTooltipFromProperty(TexturePool texturePool, WzSubProperty farmFrameParent, ToolTipInstance tooltip, GraphicsDevice device)
        {
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

            string renderText = string.Format("{0}{1}{2}", title, Environment.NewLine, desc);

            // Constants
            const string TOOLTIP_FONT = "Arial";
            const float TOOLTIP_FONTSIZE = 9.25f; // thankie willified, ya'll be remembered forever here <3
            //System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(230, 17, 54, 82); // pre V patch (dark blue theme used post-bb), leave this here in case someone needs it
            System.Drawing.Color color_bgFill = System.Drawing.Color.FromArgb(255,17, 17, 17); // post V patch (dark black theme used), use color picker on paint via image extracted from WZ if you need to get it
            System.Drawing.Color color_foreGround = System.Drawing.Color.White;
            const int WIDTH_PADDING = 10;
            const int HEIGHT_PADDING = 6;

            // Create
            using (System.Drawing.Font font = new System.Drawing.Font(TOOLTIP_FONT, TOOLTIP_FONTSIZE))
            {
                System.Drawing.Graphics graphics_dummy = System.Drawing.Graphics.FromImage(new System.Drawing.Bitmap(1, 1)); // dummy image just to get the Graphics object for measuring string
                System.Drawing.SizeF tooltipSize = graphics_dummy.MeasureString(renderText, font);

                int effective_width = (int)tooltipSize.Width + WIDTH_PADDING;
                int effective_height = (int)tooltipSize.Height + HEIGHT_PADDING;

                System.Drawing.Bitmap bmp_tooltip = new System.Drawing.Bitmap(effective_width, effective_height);
                using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bmp_tooltip))
                {
                    // Frames and background
                    UIFrameHelper.DrawUIFrame(graphics, color_bgFill, ne, nw, se, sw, e, w, n, s, c, effective_width, effective_height);

                    // Text
                    graphics.DrawString(renderText, font, new System.Drawing.SolidBrush(color_foreGround), WIDTH_PADDING / 2, HEIGHT_PADDING / 2);
                    graphics.Flush();
                }
                IDXObject dxObj = new DXObject(tooltip.X, tooltip.Y, BoardItem.TextureFromBitmap(device, bmp_tooltip), 0);
                TooltipItem item = new TooltipItem(tooltip, dxObj);
                
                return item;
            }
        }


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
        public static MouseCursorItem CreateMouseCursorFromProperty(TexturePool texturePool, WzImageProperty source, int x, int y, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            WzSubProperty cursorCanvas = (WzSubProperty)source?["0"];
            WzSubProperty cursorPressedCanvas = (WzSubProperty)source?["1"]; // click

            List<IDXObject> frames = LoadFrames(texturePool, cursorCanvas, x, y, device, ref usedProps);

            BaseItem clickedState = CreateMapItemFromProperty(texturePool, cursorPressedCanvas, 0, 0, new Point(0, 0), device, ref usedProps, false);
            return new MouseCursorItem(frames, clickedState);
        }
        #endregion

        private static string DumpFhList(List<FootholdLine> fhs)
        {
            string res = "";
            foreach (FootholdLine fh in fhs)
                res += fh.FirstDot.X + "," + fh.FirstDot.Y + " : " + fh.SecondDot.X + "," + fh.SecondDot.Y + "\r\n";
            return res;
        }
    }
}
