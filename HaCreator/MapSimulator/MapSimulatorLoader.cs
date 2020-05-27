using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.DX;
using HaCreator.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator
{
    public class MapSimulatorLoader
    {

        /// <summary>
        /// Create map simulator board
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <returns></returns>
        public static MapSimulator CreateMapSimulator(Board mapBoard)
        {
            if (mapBoard.MiniMap == null)
                mapBoard.RegenerateMinimap();

            MapSimulator result = new MapSimulator(mapBoard);
            List<WzObject> usedProps = new List<WzObject>();
            WzDirectory MapFile = Program.WzManager["map"]; // Map.wz
            WzDirectory tileDir = (WzDirectory)MapFile["Tile"];
            GraphicsDevice device = result.DxDevice;

            foreach (LayeredItem tileObj in mapBoard.BoardItems.TileObjs)
                result.mapObjects[tileObj.LayerNumber].Add(CreateMapItemFromProperty((WzImageProperty)tileObj.BaseInfo.ParentObject, tileObj.X, tileObj.Y, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, result.DxDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));

            foreach (BackgroundInstance background in mapBoard.BoardItems.BackBackgrounds)
                result.backgrounds.Add(CreateBackgroundFromProperty((WzImageProperty)background.BaseInfo.ParentObject, background.BaseX, background.BaseY, background.rx, background.ry, background.cx, background.cy, background.a, background.type, background.front, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, result.DxDevice, ref usedProps, background.Flip));

            foreach (BackgroundInstance background in mapBoard.BoardItems.FrontBackgrounds)
                result.backgrounds.Add(CreateBackgroundFromProperty((WzImageProperty)background.BaseInfo.ParentObject, background.BaseX, background.BaseY, background.rx, background.ry, background.cx, background.cy, background.a, background.type, background.front, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, result.DxDevice, ref usedProps, background.Flip));

            foreach (WzObject obj in usedProps)
                obj.MSTag = null;

            usedProps.Clear();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return result;
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
        private static MapItem CreateMapItemFromProperty(WzImageProperty source, int x, int y, int mapCenterX, int mapCenterY, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
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
                    return new MapItem(new DXObject(x - (int)origin.X + mapCenterX, y - (int)origin.Y + mapCenterY, texture), flip);
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
                        frames.Add(new DXObject(x - (int)origin.X + mapCenterX, y - (int)origin.Y + mapCenterY, texture, (int)delay));
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
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="rx"></param>
        /// <param name="ry"></param>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="a"></param>
        /// <param name="type"></param>
        /// <param name="front"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="device"></param>
        /// <param name="usedProps"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        private static BackgroundItem CreateBackgroundFromProperty(WzImageProperty source, int x, int y, int rx, int ry, int cx, int cy, int a, BackgroundType type, bool front, int mapCenterX, int mapCenterY, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
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
                    DXObject dxobj = new DXObject(x - (int)origin.X/* - mapCenterX*/, y - (int)origin.Y/* - mapCenterY*/, texture);

                    return new BackgroundItem(cx, cy, rx, ry, type, a, front, dxobj, flip);
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
                    int? delay = InfoTool.GetOptionalInt(frameProp["delay"]);
                    if (delay == null) 
                        delay = 100;

                    if (frameProp.MSTag == null)
                    {
                        frameProp.MSTag = BoardItem.TextureFromBitmap(device, frameProp.GetLinkedWzCanvasBitmap());
                        usedProps.Add(frameProp);
                    }

                    Texture2D texture = (Texture2D)frameProp.MSTag;
                    if (texture != null)
                    {
                        System.Drawing.PointF origin = frameProp.GetCanvasOriginPosition();
                        frames.Add(new DXObject(x - (int) origin.X/* - mapCenterX*/, y - (int)origin.Y/* - mapCenterY*/, texture, (int)delay));
                    }
                    else
                        throw new Exception("Texture is null for the animation");
                }
                return new BackgroundItem(cx, cy, rx, ry, type, a, front, frames, flip);
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
