using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.DX;
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


        /// <summary>
        /// Map item
        /// </summary>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static MapItem CreateMapItemFromProperty(WzImageProperty source, int x, int y, Point mapCenter, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            source = WzInfoTools.GetRealProperty(source);

            if (source is WzSubProperty && ((WzSubProperty)source).WzProperties.Count == 1)
            {
                source = ((WzSubProperty)source).WzProperties[0];
            }

            if (source is WzCanvasProperty) //one-frame
            {
                bool bLoadedSpine = LoadSpineMapObjectItem(source, source, device, null);
                if (!bLoadedSpine)
                {
                    if (source.MSTag == null)
                        source.MSTag = BoardItem.TextureFromBitmap(device, ((WzCanvasProperty)source).GetLinkedWzCanvasBitmap());
                }
                usedProps.Add(source);

                if (source.MSTagSpine != null)
                {
                    WzSpineObject spineObject = (WzSpineObject)source.MSTagSpine;
                    System.Drawing.PointF origin = ((WzCanvasProperty)source).GetCanvasOriginPosition();

                    return new MapItem(new DXSpineObject(spineObject, x + mapCenter.X, y + mapCenter.Y, origin), flip);
                }
                else if (source.MSTag != null)
                {
                    Texture2D texture = (Texture2D)source.MSTag;
                    System.Drawing.PointF origin = ((WzCanvasProperty)source).GetCanvasOriginPosition();

                    return new MapItem(new DXObject(x - (int)origin.X + mapCenter.X, y - (int)origin.Y + mapCenter.Y, texture), flip);
                }
                else // fallback
                {
                    Texture2D texture = BoardItem.TextureFromBitmap(device, Properties.Resources.placeholder);
                    System.Drawing.PointF origin = ((WzCanvasProperty)source).GetCanvasOriginPosition();

                    return new MapItem(new DXObject(x - (int)origin.X + mapCenter.X, y - (int)origin.Y + mapCenter.Y, texture), flip);
                }
            }
            else if (source is WzSubProperty) // animated
            {
                WzCanvasProperty frameProp;
                int i = 0;
                List<IDXObject> frames = new List<IDXObject>();
                while ((frameProp = (WzCanvasProperty)WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null)
                {
                    int delay = (int)InfoTool.GetOptionalInt(frameProp["delay"], 100);

                    bool bLoadedSpine = LoadSpineMapObjectItem((WzImageProperty)frameProp.Parent, frameProp, device, null);
                    if (!bLoadedSpine)
                    {
                        if (frameProp.MSTag == null)
                            frameProp.MSTag = BoardItem.TextureFromBitmap(device, frameProp.GetLinkedWzCanvasBitmap());
                    }
                    usedProps.Add(frameProp);

                    if (frameProp.MSTagSpine != null)
                    {
                        WzSpineObject spineObject = (WzSpineObject)frameProp.MSTagSpine;
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                        frames.Add(new DXSpineObject(spineObject, x  + mapCenter.X, y + mapCenter.Y, origin, delay));
                    }
                    else if (frameProp.MSTag != null)
                    {
                        Texture2D texture = (Texture2D)frameProp.MSTag;
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                        frames.Add(new DXObject(x - (int)origin.X + mapCenter.X, y - (int)origin.Y + mapCenter.Y, texture, delay));
                    }
                    else
                    {
                        Texture2D texture = BoardItem.TextureFromBitmap(device, Properties.Resources.placeholder);
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();

                        frames.Add(new DXObject(x - (int)origin.X + mapCenter.X, y - (int)origin.Y + mapCenter.Y, texture, delay));
                    }
                }
                return new MapItem(frames, flip);
            }
            else throw new Exception("unsupported property type in map simulator");
        }

        /// <summary>
        /// Background
        /// </summary>
        /// <param name="source"></param>
        /// <param name="bgInstance"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public static BackgroundItem CreateBackgroundFromProperty(WzImageProperty source, BackgroundInstance bgInstance, int mapCenterX, int mapCenterY, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
        {
            source = WzInfoTools.GetRealProperty(source);
            if (source is WzSubProperty && ((WzSubProperty)source).WzProperties.Count == 1)
                source = ((WzSubProperty)source).WzProperties[0];

            if (source is WzCanvasProperty) //one-frame
            {

                bool bLoadedSpine = LoadSpineMapObjectItem(source, source, device, bgInstance.SpineAni);
                if (!bLoadedSpine)
                {
                    if (source.MSTag == null)
                        source.MSTag = BoardItem.TextureFromBitmap(device, ((WzCanvasProperty)source).GetLinkedWzCanvasBitmap());
                }
                usedProps.Add(source);

                if (source.MSTagSpine != null)
                {
                    WzSpineObject spineObject = (WzSpineObject)source.MSTagSpine;
                    System.Drawing.PointF origin = ((WzCanvasProperty)source).GetCanvasOriginPosition();
                    DXSpineObject dxobj = new DXSpineObject(spineObject, bgInstance.BaseX - (int)origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, origin);

                    return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, dxobj, flip, bgInstance.screenMode);
                }
                else if (source.MSTag != null)
                {
                    Texture2D texture = (Texture2D)source.MSTag;
                    System.Drawing.PointF origin = ((WzCanvasProperty)source).GetCanvasOriginPosition();
                    DXObject dxobj = new DXObject(bgInstance.BaseX - (int)origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, texture);

                    return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, dxobj, flip, bgInstance.screenMode);
                }
                else  // default fallback if all things fail
                {
                    Texture2D texture = BoardItem.TextureFromBitmap(device, Properties.Resources.placeholder);
                    System.Drawing.PointF origin = ((WzCanvasProperty)source).GetCanvasOriginPosition();
                    DXObject dxobj = new DXObject(bgInstance.BaseX - (int)origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, texture);

                    return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, dxobj, flip, bgInstance.screenMode);
                }
            }
            else if (source is WzSubProperty) // animated
            {
                WzCanvasProperty frameProp;
                int i = 0;
                List<IDXObject> frames = new List<IDXObject>();
                while ((frameProp = (WzCanvasProperty)WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null)
                {
                    int delay = (int)InfoTool.GetOptionalInt(frameProp["delay"], 100);

                    bool bLoadedSpine = LoadSpineMapObjectItem((WzImageProperty)frameProp.Parent, frameProp, device, bgInstance.SpineAni);
                    if (!bLoadedSpine)
                    {
                        if (frameProp.MSTag == null)
                        {
                            frameProp.MSTag = BoardItem.TextureFromBitmap(device, frameProp.GetLinkedWzCanvasBitmap());
                        }
                    }
                    usedProps.Add(source);

                    if (frameProp.MSTagSpine != null)
                    {
                        WzSpineObject spineObject = (WzSpineObject)frameProp.MSTagSpine;
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();
                        DXSpineObject dxobj = new DXSpineObject(spineObject, bgInstance.BaseX - (int)origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, origin, delay);

                        frames.Add(dxobj);
                    }
                    else if (frameProp.MSTag != null)
                    {
                        Texture2D texture = (Texture2D)frameProp.MSTag;
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();
                        DXObject dxObj = new DXObject(bgInstance.BaseX - (int)origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, texture, delay);

                        frames.Add(dxObj);
                    }
                    else // default fallback if all things fail
                    {
                        Texture2D texture = BoardItem.TextureFromBitmap(device, Properties.Resources.placeholder);
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();
                        DXObject dxObj = new DXObject(bgInstance.BaseX - (int)origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, texture, delay);

                        frames.Add(dxObj);
                    }
                }
                return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames, flip, bgInstance.screenMode);
            }
            else throw new Exception("Unsupported property type in map simulator");
        }


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

        private static string DumpFhList(List<FootholdLine> fhs)
        {
            string res = "";
            foreach (FootholdLine fh in fhs)
                res += fh.FirstDot.X + "," + fh.FirstDot.Y + " : " + fh.SecondDot.X + "," + fh.SecondDot.Y + "\r\n";
            return res;
        }
    }
}
