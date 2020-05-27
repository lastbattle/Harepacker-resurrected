/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

// #define FULLSCREEN

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
using HaSharedLibrary;
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
        public static float RenderObjectScaling = 1.0f;

        private GraphicsDevice _DxDevice;
        public GraphicsDevice DxDevice
        {
            get { return _DxDevice; }
            private set { }
        }

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
            List<MapItem>[] result = new List<MapItem>[WzConstants.MaxMapLayers];
            for (int i = 0; i < WzConstants.MaxMapLayers; i++)
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


            RenderObjectScaling = 1.0f;
            switch (UserSettings.SimulateResolution)
            {

                case MapRenderResolution.Res_1024x768:  // 1024x768
                    RenderHeight = 768;
                    RenderWidth = 1024;
                    break;
                case MapRenderResolution.Res_1280x720: // 1280x720
                    RenderHeight = 720;
                    RenderWidth = 1280;
                    break;
                case MapRenderResolution.Res_1366x768:  // 1366x768
                    RenderHeight = 768;
                    RenderWidth = 1366;
                    break;

                    
                case MapRenderResolution.Res_1920x1080: // 1920x1080
                    RenderHeight = 1080;
                    RenderWidth = 1920;
                    break;
                case MapRenderResolution.Res_1920x1080_120PercScaled: // 1920x1080
                    RenderHeight = 1080;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.2f;
                    break;
                case MapRenderResolution.Res_1920x1080_150PercScaled: // 1920x1080
                    RenderHeight = 1080;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.5f;
                    break;


                case MapRenderResolution.Res_1920x1200: // 1920x1200
                    RenderHeight = 1200;
                    RenderWidth = 1920;
                    break;
                case MapRenderResolution.Res_1920x1200_120PercScaled: // 1920x1200
                    RenderHeight = 1200;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.2f;
                    break;
                case MapRenderResolution.Res_1920x1200_150PercScaled: // 1920x1200
                    RenderHeight = 1200;
                    RenderWidth = 1920;
                    RenderObjectScaling = 1.5f;
                    break;

                case MapRenderResolution.Res_800x600: // 800x600
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

            // default center
            int leftRightVRDifference = vr.Right - vr.Left;
            int topDownVRDifference = vr.Bottom - vr.Top;

            mapShiftX = ((leftRightVRDifference / 2) + vr.Left) - (RenderWidth / 2);
            mapShiftY = ((topDownVRDifference / 2) + vr.Top) - (RenderHeight / 2);


            _DxDevice = MultiBoard.CreateGraphicsDevice(pParams);
            this.minimap = BoardItem.TextureFromBitmap(_DxDevice, mapBoard.MiniMap);
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(1, 1);
            bmp.SetPixel(0, 0, System.Drawing.Color.White);
            pixel = BoardItem.TextureFromBitmap(_DxDevice, bmp);

            sprite = new SpriteBatch(_DxDevice);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            _DxDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0); // Clear the window to black
            sprite.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, null, null, null, null, Matrix.CreateScale(RenderObjectScaling) );

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
                _DxDevice.Present();
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

            _DxDevice.Reset(_DxDevice.PresentationParameters);
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

            bool bIsLeft = InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Left);
            bool bIsRight = InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Right);

            if (bIsLeft || bIsRight)
            {
                int leftRightVRDifference = (int) ((vr.Right - vr.Left) * RenderObjectScaling);
                if (leftRightVRDifference < RenderWidth) // viewing range is smaller than the render width.. keep the rendering position at the center instead (starts from left to right)
                {
                    /*
                     * Orbis Tower <20th Floor>
                     *  |____________|
                     *  |____________|
                     *  |____________|
                     *  |____________|
                     *  |____________|
                     *  |____________|
                     *  |____________|
                     *  |____________|
                     *  
                     * vr.Left = 87
                     * vr.Right = 827
                     * Difference = 740px
                     * vr.Center = ((vr.Right - vr.Left) / 2) + vr.Left
                     * 
                     * Viewing Width = 1024 
                     * Relative viewing center = vr.Center - (Viewing Width / 2)
                     */
                    mapShiftX = ((leftRightVRDifference/2) + (int) (vr.Left * RenderObjectScaling)) - (RenderWidth / 2);
                }
                else
                {
                    // System.Diagnostics.Debug.WriteLine("[{4}] VR.Right {0}, Width {1}, Relative {2}. [Scaling {3}]", 
                    //      vr.Right, RenderWidth, (int)(vr.Right - RenderWidth), (int)((vr.Right - (RenderWidth * RenderObjectScaling)) * RenderObjectScaling),
                    //     mapShiftX + offset);

                    if (bIsLeft)
                        mapShiftX = 
                            Math.Max(
                                (int)(vr.Left * RenderObjectScaling),
                                mapShiftX - offset);

                    else if (bIsRight)
                        mapShiftX = 
                            Math.Min(
                                 (int)((vr.Right - (RenderWidth / RenderObjectScaling))),
                                mapShiftX + offset);
                }
            }

            bool bIsUp = InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Up);
            bool bIsDown = InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Down);

            if (bIsUp || bIsDown)
            {
                int topDownVRDifference = (int) ((vr.Bottom - vr.Top) * RenderObjectScaling);
                if (topDownVRDifference < RenderHeight)
                {
                    mapShiftY = ((topDownVRDifference / 2) + (int) (vr.Top * RenderObjectScaling)) - (RenderHeight / 2);
                }
                else
                {
                    /*System.Diagnostics.Debug.WriteLine("[{0}] VR.Bottom {1}, Height {2}, Relative {3}. [Scaling {4}]",
                        (int)((vr.Bottom - (RenderHeight))),
                        vr.Bottom, RenderHeight, (int)(vr.Bottom - RenderHeight),
                        mapShiftX + offset);*/


                    if (bIsUp)
                        mapShiftY = 
                            Math.Max(
                                (int) (vr.Top), 
                                mapShiftY - offset);

                    else if (bIsDown)
                        mapShiftY = 
                            Math.Min(
                                (int) ((vr.Bottom - (RenderHeight / RenderObjectScaling))), 
                                mapShiftY + offset);
                }
            }

            if (InputHandler.IsKeyPushedDown(System.Windows.Forms.Keys.Escape))
            {
                _DxDevice.Dispose();
                Close();
            }
        }


        private void MapSimulator_Resize(object sender, EventArgs e)
        {
            if (_DxDevice != null)
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