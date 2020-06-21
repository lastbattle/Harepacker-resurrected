using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.DX;
using HaCreator.MapSimulator.Objects;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaCreator.MapSimulator.Objects.UIObject;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
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
        /// <param name="linkedReactorImage"></param>
        /// <param name="reactorInstance"></param>
        /// <param name="reactorInfo"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static ReactorItem CreateReactorFromProperty(TexturePool texturePool, WzImage linkedReactorImage, ReactorInstance reactorInstance, ReactorInfo reactorInfo, GraphicsDevice device, ref List<WzObject> usedProps)
        {
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
        /// <param name="portalInfo"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static PortalItem CreatePortalFromProperty(TexturePool texturePool, WzSubProperty gameParent, PortalInstance portalInstance, PortalInfo portalInfo, GraphicsDevice device, ref List<WzObject> usedProps)
        {
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
        /// <param name="source"></param>
        /// <param name="mobInstance"></param>
        /// <param name="mobInfo"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static MobItem CreateMobFromProperty(TexturePool texturePool, WzImage source, MobInstance mobInstance, MobInfo mobInfo, GraphicsDevice device, ref List<WzObject> usedProps)
        {
            List<IDXObject> frames = new List<IDXObject>(); // All frames "stand", "speak" "blink" "hair", "angry", "wink" etc

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
        /// <param name="source"></param>
        /// <param name="npcInstance"></param>
        /// <param name="npcInfo"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <returns></returns>
        public static NpcItem CreateNpcFromProperty(TexturePool texturePool, WzImage source, NpcInstance npcInstance, NpcInfo npcInfo, GraphicsDevice device, ref List<WzObject> usedProps)
        {
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
