using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.DX;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                if (source.MSTag == null)
                {
                    source.MSTag = BoardItem.TextureFromBitmap(device, ((WzCanvasProperty)source).GetLinkedWzCanvasBitmap());
                    usedProps.Add(source);
                }
                Texture2D texture = (Texture2D)source.MSTag;
                if (texture != null)
                {
                    System.Drawing.PointF origin = ((WzCanvasProperty)source).GetCanvasOriginPosition();
                    return new MapItem(new DXObject(x - (int)origin.X + mapCenter.X, y - (int)origin.Y + mapCenter.Y, texture), flip);
                }
                else
                {
                    throw new Exception("Texture is null for the map item.");
                }
            }
            else if (source is WzSubProperty) //animooted
            {
                WzCanvasProperty frameProp;
                int i = 0;
                List<DXObject> frames = new List<DXObject>();
                while ((frameProp = (WzCanvasProperty)WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null)
                {
                    int? delay = InfoTool.GetOptionalInt(frameProp["delay"]);
                    if (delay == null)
                    {
                        delay = 100;
                    }

                    if (frameProp.MSTag == null)
                    {
                        frameProp.MSTag = BoardItem.TextureFromBitmap(device, frameProp.GetLinkedWzCanvasBitmap());
                        usedProps.Add(frameProp);
                    }

                    Texture2D texture = (Texture2D)frameProp.MSTag;
                    if (texture != null)
                    {
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();
                        frames.Add(new DXObject(x - (int)origin.X + mapCenter.X, y - (int)origin.Y + mapCenter.Y, texture, (int)delay));
                    }
                    else
                    {
                        throw new Exception("Texture is null for the animated map item");
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
                if (source.MSTag == null)
                {
                    source.MSTag = BoardItem.TextureFromBitmap(device, ((WzCanvasProperty)source).GetLinkedWzCanvasBitmap());
                    usedProps.Add(source);
                }

                Texture2D texture = (Texture2D)source.MSTag;
                if (texture != null)
                {
                    System.Drawing.PointF origin = ((WzCanvasProperty) source).GetCanvasOriginPosition();
                    DXObject dxobj = new DXObject(bgInstance.BaseX - (int)origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, texture);

                    return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, dxobj, flip, bgInstance.screenMode);
                } else
                {
                    throw new Exception("Texture is null for the background property.");
                }
            }
            else if (source is WzSubProperty) //animooted
            {
                WzCanvasProperty frameProp;
                int i = 0;
                List<DXObject> frames = new List<DXObject>();
                while ((frameProp = (WzCanvasProperty)WzInfoTools.GetRealProperty(source[(i++).ToString()])) != null)
                {
                    int? delay = InfoTool.GetOptionalInt(frameProp["delay"], 100);

                    if (frameProp.MSTag == null)
                    {
                        frameProp.MSTag = BoardItem.TextureFromBitmap(device, frameProp.GetLinkedWzCanvasBitmap());
                        usedProps.Add(frameProp);
                    }

                    Texture2D texture = (Texture2D)frameProp.MSTag;
                    if (texture != null)
                    {
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();
                        frames.Add(new DXObject(bgInstance.BaseX - (int) origin.X/* - mapCenterX*/, bgInstance.BaseY - (int)origin.Y/* - mapCenterY*/, texture, (int)delay));
                    }
                    else
                        throw new Exception("Texture is null for the animation");
                }
                return new BackgroundItem(bgInstance.cx, bgInstance.cy, bgInstance.rx, bgInstance.ry, bgInstance.type, bgInstance.a, bgInstance.front, frames, flip, bgInstance.screenMode);
            }
            else throw new Exception("Unsupported property type in map simulator");
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
