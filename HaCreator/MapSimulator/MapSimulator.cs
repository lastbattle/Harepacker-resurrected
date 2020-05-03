/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

//#define FULLSCREEN

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;
using System.IO;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections;
using HaCreator.MapEditor;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Input;
using HaSharedLirary;
using HaRepacker.Utils;
using HaCreator.MapSimulator.DX;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator : Form
    {
        public int mapShiftX = 0;
        public int mapShiftY = 0;
        public Point mapCenter;
        public Point minimapPos;

        public static int RenderWidth;
        public static int RenderHeight;

        private GraphicsDevice DxDevice;
        private SpriteBatch sprite;
        private PresentationParameters pParams = new PresentationParameters();
        public List<MapItem>[] mapObjects = CreateLayersArray();
        public List<BackgroundItem> backgrounds = new List<BackgroundItem>();
        private Rectangle vr;
        private Texture2D minimap;
        private Texture2D pixel;
        private WzMp3Streamer audio;

        private static List<MapItem>[] CreateLayersArray()
        {
            List<MapItem>[] result = new List<MapItem>[8];
            for (int i = 0; i < 8; i++)
                result[i] = new List<MapItem>();
            return result;
        }

        public MapSimulator(Board mapBoard)
        {
            InitializeComponent();

            if (Program.InfoManager.BGMs.ContainsKey(mapBoard.MapInfo.bgm))
                audio = new WzMp3Streamer(Program.InfoManager.BGMs[mapBoard.MapInfo.bgm], true);

            mapCenter = mapBoard.CenterPoint;
            minimapPos = new Point((int)Math.Round((mapBoard.MinimapPosition.X + mapCenter.X) / (double)mapBoard.mag), (int)Math.Round((mapBoard.MinimapPosition.Y + mapCenter.Y) / (double)mapBoard.mag));
            if (mapBoard.VRRectangle == null) 
                vr = new Rectangle(0, 0, mapBoard.MapSize.X, mapBoard.MapSize.Y);
            else 
                vr = new Rectangle(mapBoard.VRRectangle.X + mapCenter.X, mapBoard.VRRectangle.Y + mapCenter.Y, mapBoard.VRRectangle.Width, mapBoard.VRRectangle.Height);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);


            switch (UserSettings.SimulateResolution)
            {

                case 1:  // 1024x768
                    RenderHeight = 768;
                    RenderWidth = 1024;
                    break;
                case 2: // 1280x720
                    RenderHeight = 720;
                    RenderWidth = 1280;
                    break;
                case 3:  // 1366x768
                    RenderHeight = 768;
                    RenderWidth = 1366;
                    break;
                case 4:
                    RenderHeight = 1080;
                    RenderWidth = 1920;
                    break;
                case 0: // 800x600
                default:
                    RenderHeight = 600;
                    RenderWidth = 800;
                    break;
            }
            double dpi = ScreenDPIUtil.GetScreenScaleFactor();

            // set Form window height & width
            this.Width = (int) (RenderWidth * dpi);
            this.Height = (int) (RenderHeight * dpi);

#if FULLSCREEN
            pParams.BackBufferWidth = Math.Max(Width, 1);
            pParams.BackBufferHeight = Math.Max(Height, 1);
            pParams.BackBufferFormat = SurfaceFormat.Color;
            pParams.IsFullScreen = false;
            pParams.DepthStencilFormat = DepthFormat.Depth24;
#else
            pParams.BackBufferWidth = Math.Max(RenderWidth, 1);
            pParams.BackBufferHeight = Math.Max(RenderHeight, 1);
            pParams.BackBufferFormat = SurfaceFormat.Color;
            pParams.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            pParams.DeviceWindowHandle = Handle;
            pParams.IsFullScreen = false;
#endif

            DxDevice = MultiBoard.CreateGraphicsDevice(pParams);
            this.minimap = BoardItem.TextureFromBitmap(DxDevice, mapBoard.MiniMap);
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(1, 1);
            bmp.SetPixel(0, 0, System.Drawing.Color.White);
            pixel = BoardItem.TextureFromBitmap(DxDevice, bmp);

            sprite = new SpriteBatch(DxDevice);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            DxDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0); // Clear the window to black
            sprite.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
            foreach (BackgroundItem bg in backgrounds)
            {
                if (!bg.Front)
                    bg.Draw(sprite, mapShiftX, mapShiftY, mapCenter.X, mapCenter.Y, RenderWidth, RenderHeight);
            }

            for (int i = 0; i < mapObjects.Length; i++)
            {
                foreach (MapItem item in mapObjects[i])
                    item.Draw(sprite, mapShiftX, mapShiftY, mapCenter.X, mapCenter.Y, RenderWidth, RenderHeight);
            }

            foreach (BackgroundItem bg in backgrounds)
                if (bg.Front)
                    bg.Draw(sprite, mapShiftX, mapShiftY, mapCenter.X, mapCenter.Y, RenderWidth, RenderHeight);

            if (minimap != null)
            {
                sprite.Draw(minimap, new Rectangle(minimapPos.X, minimapPos.Y, minimap.Width, minimap.Height), Color.White);
                int minimapPosX = (mapShiftX + (RenderWidth / 2)) / 16;
                int minimapPosY = (mapShiftY + (RenderHeight / 2)) / 16;
                FillRectangle(sprite, new Rectangle(minimapPosX - 4, minimapPosY - 4, 4, 4), Color.Yellow);
            }
            sprite.End();
            try
            {
                DxDevice.Present();
            }
            catch (DeviceNotResetException)
            {
                try
                {
                    ResetDevice();
                }
                catch (DeviceLostException)
                {
                }
            }
            catch (DeviceLostException)
            {
            }
            HandleKeyPresses();
            System.Threading.Thread.Sleep(10);
            Invalidate();
        }

        private void ResetDevice()
        {
            pParams.BackBufferHeight = Height;
            pParams.BackBufferWidth = Width;
            pParams.BackBufferFormat = SurfaceFormat.Color;
            pParams.DepthStencilFormat = DepthFormat.Depth24Stencil8;
            pParams.DeviceWindowHandle = Handle;

            DxDevice.Reset(DxDevice.PresentationParameters);
        }

        public void DrawLine(SpriteBatch sprite, Vector2 start, Vector2 end, Color color)
        {
            int width = (int)Vector2.Distance(start, end);
            float rotation = (float)Math.Atan2((double)(end.Y - start.Y), (double)(end.X - start.X));
            sprite.Draw(pixel, new Rectangle((int)start.X, (int)start.Y, width, UserSettings.LineWidth), null, color, rotation, new Vector2(0f, 0f), SpriteEffects.None, 1f);
        }

        public void FillRectangle(SpriteBatch sprite, Rectangle rectangle, Color color)
        {
            sprite.Draw(pixel, rectangle, color);
        }

        //int lastHotKeyPressTime = 0;

        void HandleKeyPresses()
        {
            if (!Focused)
                return;
            int offset = (InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.LShiftKey) || InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.RShiftKey)) ? 100 : 10;
            if (InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Left))
                mapShiftX = Math.Max(vr.Left, mapShiftX - offset);
            if (InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Up))
                mapShiftY = Math.Max(vr.Top, mapShiftY - offset);
            if (InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Right))
                mapShiftX = Math.Min(vr.Right - RenderWidth, mapShiftX + offset);
            if (InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Down))
                mapShiftY = Math.Min(vr.Bottom - RenderHeight, mapShiftY + offset);
            if (InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Escape))
            {
                DxDevice.Dispose();
                Close();
            }
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
                    WzVectorProperty origin = (WzVectorProperty)source["origin"];
                    return new MapItem(new DXObject(x - origin.X.Value + mapCenterX, y - origin.Y.Value + mapCenterY, texture), flip);
                } else
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
                        WzVectorProperty origin = (WzVectorProperty)frameProp["origin"];
                        frames.Add(new DXObject(x - origin.X.Value + mapCenterX, y - origin.Y.Value + mapCenterY, texture, (int)delay));
                    } else
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
        public static BackgroundItem CreateBackgroundFromProperty(WzImageProperty source, int x, int y, int rx, int ry, int cx, int cy, int a, BackgroundType type, bool front, int mapCenterX, int mapCenterY, GraphicsDevice device, ref List<WzObject> usedProps, bool flip)
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
                    WzVectorProperty origin = (WzVectorProperty)source["origin"];
                    DXObject dxobj = new DXObject(x - origin.X.Value/* - mapCenterX*/, y - origin.Y.Value/* - mapCenterY*/, texture);

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
                        WzVectorProperty origin = (WzVectorProperty)frameProp["origin"];
                        frames.Add(new DXObject(x - origin.X.Value/* - mapCenterX*/, y - origin.Y.Value/* - mapCenterY*/, texture, (int)delay));
                    }
                    else
                        throw new Exception("Texture is null for the animation");
                }
                return new BackgroundItem(cx, cy, rx, ry, type, a, front, frames, flip);
            }
            else throw new Exception("unsupported property type in map simulator");
        }


        private static string DumpFhList(List<FootholdLine> fhs)
        {
            string res = "";
            foreach (FootholdLine fh in fhs)
                res += fh.FirstDot.X + "," + fh.FirstDot.Y + " : " + fh.SecondDot.X + "," + fh.SecondDot.Y + "\r\n";
            return res;
        }

        public static MapSimulator CreateMapSimulator(Board mapBoard)
        {
            if (mapBoard.MiniMap == null) 
                mapBoard.RegenerateMinimap();

            MapSimulator result = new MapSimulator(mapBoard);
            List<WzObject> usedProps = new List<WzObject>();
            WzDirectory MapFile = Program.WzManager["map"];
            WzDirectory tileDir = (WzDirectory)MapFile["Tile"];
            GraphicsDevice device = result.DxDevice;

            foreach (LayeredItem tileObj in mapBoard.BoardItems.TileObjs)
                result.mapObjects[tileObj.LayerNumber].Add(CreateMapItemFromProperty((WzImageProperty)tileObj.BaseInfo.ParentObject, tileObj.X, tileObj.Y, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, result.DxDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));
            foreach (BackgroundInstance background in mapBoard.BoardItems.BackBackgrounds)
                result.backgrounds.Add(CreateBackgroundFromProperty((WzImageProperty)background.BaseInfo.ParentObject, background.BaseX, background.BaseY, background.rx, background.ry, background.cx, background.cy, background.a, background.type, background.front, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, result.DxDevice, ref usedProps, background.Flip));
            foreach (BackgroundInstance background in mapBoard.BoardItems.FrontBackgrounds)
                result.backgrounds.Add(CreateBackgroundFromProperty((WzImageProperty)background.BaseInfo.ParentObject, background.BaseX, background.BaseY, background.rx, background.ry, background.cx, background.cy, background.a, background.type, background.front, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, result.DxDevice, ref usedProps, background.Flip));
            foreach (WzObject obj in usedProps) obj.MSTag = null;
            usedProps.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return result;
        }

        private void MapSimulator_Resize(object sender, EventArgs e)
        {
            if (DxDevice != null)
                ResetDevice();
        }

        private void MapSimulator_Load(object sender, EventArgs e)
        {
            if (audio != null) 
                audio.Play();
        }

        private void MapSimulator_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (audio != null)
            {
                //audio.Pause();
                audio.Dispose();
            }
        }
    }
}