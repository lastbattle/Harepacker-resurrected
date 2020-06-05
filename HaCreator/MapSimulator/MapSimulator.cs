using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.DX;
using HaRepacker.Utils;
using HaSharedLibrary;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;


namespace HaCreator.MapSimulator
{
    /// <summary>
    /// http://rbwhitaker.wikidot.com/xna-tutorials
    /// </summary>
    public class MapSimulator : Microsoft.Xna.Framework.Game
    {
        public int mapShiftX = 0;
        public int mapShiftY = 0;
        public Point minimapPos;

        private int RenderWidth;
        private int RenderHeight;
        private float RenderObjectScaling = 1.0f;
        private MapRenderResolution mapRenderResolution;

        private GraphicsDeviceManager _DxDeviceManager;

        private SpriteBatch sprite;

        // Objects
        public List<MapItem>[] mapObjects;

        // Backgrounds
        public List<BackgroundItem> backgrounds_front = new List<BackgroundItem>();
        public List<BackgroundItem> backgrounds_back = new List<BackgroundItem>();

        // Boundary, borders
        private Rectangle vr;

        // Minimap
        private Texture2D pixel;

        // Audio
        private WzMp3Streamer audio;

        // Etc
        private Texture2D minimap;
        private Board mapBoard;

        // Spine
        private SkeletonMeshRenderer skeletonMeshRenderer;

        // Text
        private SpriteFont font;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <param name="titleName"></param>
        public MapSimulator(Board mapBoard, string titleName)
        {
            IsMouseVisible = true;

            this.mapBoard = mapBoard;

            this.mapRenderResolution = UserSettings.SimulateResolution;
            InitialiseMapWidthHeight();

            //RenderHeight += System.Windows.Forms.SystemInformation.CaptionHeight; // window title height

            //double dpi = ScreenDPIUtil.GetScreenScaleFactor();

            // set Form window height & width
            //this.Width = (int)(RenderWidth * dpi);
            //this.Height = (int)(RenderHeight * dpi);

            // default center
            int leftRightVRDifference = vr.Right - vr.Left;
            int topDownVRDifference = vr.Bottom - vr.Top;

            mapShiftX = ((leftRightVRDifference / 2) + vr.Left) - (RenderWidth / 2);
            mapShiftY = ((topDownVRDifference / 2) + vr.Top) - (RenderHeight / 2);

            //Window.IsBorderless = true;
            //Window.Position = new Point(0, 0);
            Window.Title = titleName;
            IsFixedTimeStep = false; // dont cap fps

            _DxDeviceManager = new GraphicsDeviceManager(this)
            {
                SynchronizeWithVerticalRetrace = false, // dont cap fps
                HardwareModeSwitch = true,
                GraphicsProfile = GraphicsProfile.HiDef,
                IsFullScreen = false,
                PreferMultiSampling = true,
                SupportedOrientations = DisplayOrientation.Default,
                PreferredBackBufferWidth = Math.Max(RenderWidth, 1),
                PreferredBackBufferHeight = Math.Max(RenderHeight, 1),
                PreferredBackBufferFormat = SurfaceFormat.Color,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            };
            _DxDeviceManager.ApplyChanges();

        }

        private void InitialiseMapWidthHeight()
        {
            RenderObjectScaling = 1.0f;
            switch (this.mapRenderResolution)
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
                    this.mapRenderResolution |= MapRenderResolution.Res_1366x768; // 1920x1080 is just 1366x768 with 150% scale.
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

                case MapRenderResolution.Res_All:
                case MapRenderResolution.Res_800x600: // 800x600
                default:
                    RenderHeight = 600;
                    RenderWidth = 800;
                    break;
            }
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            // Create map layers
            mapObjects = new List<MapItem>[WzConstants.MaxMapLayers];
            for (int i = 0; i < WzConstants.MaxMapLayers; i++)
            {
                mapObjects[i] = new List<MapItem>();
            }

            //GraphicsDevice.Viewport = new Viewport(RenderWidth / 2 - 800 / 2, RenderHeight / 2 - 600 / 2, 800, 600);

            // https://stackoverflow.com/questions/55045066/how-do-i-convert-a-ttf-or-other-font-to-a-xnb-xna-game-studio-font
            // if you're having issues building on w10, install Visual C++ Redistributable for Visual Studio 2012 Update 4
            // 
            // to build your own font: /MonoGame Font Builder/game.mgcb
            // build -> obj -> copy it over to HaRepacker-resurrected [Content]
            font = Content.Load<SpriteFont>("XnaDefaultFont");

            base.Initialize();
        }


        /// <summary>
        /// Load game assets
        /// </summary>
        protected override void LoadContent()
        {
            // BGM
            if (Program.InfoManager.BGMs.ContainsKey(mapBoard.MapInfo.bgm))
            {
                audio = new WzMp3Streamer(Program.InfoManager.BGMs[mapBoard.MapInfo.bgm], true);
                if (audio != null)
                {
                    audio.Volume = 0.3f;
                    audio.Play();
                }
            }
            if (mapBoard.VRRectangle == null)
                vr = new Rectangle(0, 0, mapBoard.MapSize.X, mapBoard.MapSize.Y);
            else
                vr = new Rectangle(mapBoard.VRRectangle.X + mapBoard.CenterPoint.X, mapBoard.VRRectangle.Y + mapBoard.CenterPoint.Y, mapBoard.VRRectangle.Width, mapBoard.VRRectangle.Height);
            //SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);

            // Background and objects
            List<WzObject> usedProps = new List<WzObject>();
            //WzDirectory MapFile = Program.WzManager["map"]; // Map.wz
            //WzDirectory tileDir = (WzDirectory)MapFile["Tile"];

            foreach (LayeredItem tileObj in mapBoard.BoardItems.TileObjs)
            {
                WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;

                mapObjects[tileObj.LayerNumber].Add(
                    MapSimulatorLoader.CreateMapItemFromProperty(tileParent, tileObj.X, tileObj.Y, mapBoard.CenterPoint, _DxDeviceManager.GraphicsDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));
            }
            foreach (BackgroundInstance background in mapBoard.BoardItems.BackBackgrounds)
            {
                WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;

                backgrounds_back.Add(
                    MapSimulatorLoader.CreateBackgroundFromProperty(bgParent, background, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip));
            }
            foreach (BackgroundInstance background in mapBoard.BoardItems.FrontBackgrounds)
            {
                WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;

                backgrounds_front.Add(
                    MapSimulatorLoader.CreateBackgroundFromProperty(bgParent, background, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip));
            }
            foreach (WzObject obj in usedProps)
            {
                obj.MSTag = null;
                obj.MSTagSpine = null; // cleanup
            }
            usedProps.Clear();

            // Spine object
            skeletonMeshRenderer = new SkeletonMeshRenderer(GraphicsDevice);
            skeletonMeshRenderer.PremultipliedAlpha = false;

            // Minimap
            minimapPos = new Point((int)Math.Round((mapBoard.MinimapPosition.X + mapBoard.CenterPoint.X) / (double)mapBoard.mag), (int)Math.Round((mapBoard.MinimapPosition.Y + mapBoard.CenterPoint.Y) / (double)mapBoard.mag));
            this.minimap = BoardItem.TextureFromBitmap(GraphicsDevice, mapBoard.MiniMap);

            //
            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(1, 1);
            bmp.SetPixel(0, 0, System.Drawing.Color.White);
            pixel = BoardItem.TextureFromBitmap(GraphicsDevice, bmp);

            sprite = new SpriteBatch(GraphicsDevice);
        }

        protected override void UnloadContent()
        {
            if (audio != null)
            {
                //audio.Pause();
                audio.Dispose();
            }

            skeletonMeshRenderer.End();

            _DxDeviceManager.EndDraw();
            _DxDeviceManager.Dispose();
        }

        /// <summary>
        /// Key handling
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime)
        {

            // Allows the game to exit
#if !WINDOWS_STOREAPP
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                this.Exit();
                return;
            }
#endif
            // Handle full screen
            bool bIsAltEnterPressed = Keyboard.GetState().IsKeyDown(Keys.LeftAlt) && Keyboard.GetState().IsKeyDown(Keys.Enter);
            if (bIsAltEnterPressed)
            {
                _DxDeviceManager.IsFullScreen = !_DxDeviceManager.IsFullScreen;
                _DxDeviceManager.ApplyChanges();
            }


            // Navigate around the rendered object
            bool bIsShiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);

            bool bIsUpKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Up);
            bool bIsDownKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Down);
            bool bIsLeftKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Left);
            bool bIsRightKeyPressed = Keyboard.GetState().IsKeyDown(Keys.Right);

            int offset = bIsShiftPressed ? 8 : 2;

            if (bIsLeftKeyPressed || bIsRightKeyPressed)
            {
                int leftRightVRDifference = (int)((vr.Right - vr.Left) * RenderObjectScaling);
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
                    mapShiftX = ((leftRightVRDifference / 2) + (int)(vr.Left * RenderObjectScaling)) - (RenderWidth / 2);
                }
                else
                {
                    // System.Diagnostics.Debug.WriteLine("[{4}] VR.Right {0}, Width {1}, Relative {2}. [Scaling {3}]", 
                    //      vr.Right, RenderWidth, (int)(vr.Right - RenderWidth), (int)((vr.Right - (RenderWidth * RenderObjectScaling)) * RenderObjectScaling),
                    //     mapShiftX + offset);

                    if (bIsLeftKeyPressed)
                        mapShiftX =
                            Math.Max(
                                (int)(vr.Left * RenderObjectScaling),
                                mapShiftX - offset);

                    else if (bIsRightKeyPressed)
                        mapShiftX =
                            Math.Min(
                                 (int)((vr.Right - (RenderWidth / RenderObjectScaling))),
                                mapShiftX + offset);
                }
            }


            if (bIsUpKeyPressed || bIsDownKeyPressed)
            {
                int topDownVRDifference = (int)((vr.Bottom - vr.Top) * RenderObjectScaling);
                if (topDownVRDifference < RenderHeight)
                {
                    mapShiftY = ((topDownVRDifference / 2) + (int)(vr.Top * RenderObjectScaling)) - (RenderHeight / 2);
                }
                else
                {
                    /*System.Diagnostics.Debug.WriteLine("[{0}] VR.Bottom {1}, Height {2}, Relative {3}. [Scaling {4}]",
                        (int)((vr.Bottom - (RenderHeight))),
                        vr.Bottom, RenderHeight, (int)(vr.Bottom - RenderHeight),
                        mapShiftX + offset);*/


                    if (bIsUpKeyPressed)
                        mapShiftY =
                            Math.Max(
                                (int)(vr.Top),
                                mapShiftY - offset);

                    else if (bIsDownKeyPressed)
                        mapShiftY =
                            Math.Min(
                                (int)((vr.Bottom - (RenderHeight / RenderObjectScaling))),
                                mapShiftY + offset);
                }
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            int TickCount = Environment.TickCount;
            float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;

            //GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0); // Clear the window to black
            GraphicsDevice.Clear(Color.Black);

            sprite.Begin(
                SpriteSortMode.Immediate, // spine :(
                //SpriteSortMode.Deferred, // drawing right away causes pixelation and image tearing it seems
                BlendState.NonPremultiplied, null, null, null, null, Matrix.CreateScale(RenderObjectScaling));
            //skeletonMeshRenderer.Begin();

            // Back Backgrounds
            backgrounds_back.ForEach(bg =>
            {
                bg.Draw(sprite, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            });

            // Map objects
            foreach (List<MapItem> mapItem in mapObjects)
            {
                foreach (MapItem item in mapItem)
                {
                    item.Draw(sprite, skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                        RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                        TickCount);
                }
            }

            // Front Backgrounds
            backgrounds_front.ForEach(bg =>
            {
                bg.Draw(sprite, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            });

            // Borders
            // Create any rectangle you want. Here we'll use the TitleSafeArea for fun.
            Rectangle titleSafeRectangle = GraphicsDevice.Viewport.TitleSafeArea;
            DrawBorder(sprite, titleSafeRectangle, 1, Color.Black);

            // Minimap
            if (minimap != null)
            {
                sprite.Draw(minimap, new Rectangle(minimapPos.X, minimapPos.Y, minimap.Width, minimap.Height), Color.White);
                int minimapPosX = (mapShiftX + (RenderWidth / 2)) / 16;
                int minimapPosY = (mapShiftY + (RenderHeight / 2)) / 16;

                FillRectangle(sprite, new Rectangle(minimapPosX - 4, minimapPosY - 4, 4, 4), Color.Yellow);
            }


            if (gameTime.TotalGameTime.TotalSeconds < 3)
                sprite.DrawString(font, "Press [Left] [Right] [Up] [Down] [Shift] [Alt+Enter] for navigation.", new Vector2(20, 10), Color.White);

            sprite.End();
           //skeletonMeshRenderer.End();

            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws a border
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="rectangleToDraw"></param>
        /// <param name="thicknessOfBorder"></param>
        /// <param name="borderColor"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawBorder(SpriteBatch sprite, Rectangle rectangleToDraw, int thicknessOfBorder, Color borderColor)
        {
            // Draw top line
            sprite.Draw(pixel, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, rectangleToDraw.Width, thicknessOfBorder), borderColor);

            // Draw left line
            sprite.Draw(pixel, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);

            // Draw right line
            sprite.Draw(pixel, new Rectangle((rectangleToDraw.X + rectangleToDraw.Width - thicknessOfBorder),
                                            rectangleToDraw.Y,
                                            thicknessOfBorder,
                                            rectangleToDraw.Height), borderColor);
            // Draw bottom line
            sprite.Draw(pixel, new Rectangle(rectangleToDraw.X,
                                            rectangleToDraw.Y + rectangleToDraw.Height - thicknessOfBorder,
                                            rectangleToDraw.Width,
                                            thicknessOfBorder), borderColor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawLine(SpriteBatch sprite, Vector2 start, Vector2 end, Color color)
        {
            int width = (int)Vector2.Distance(start, end);
            float rotation = (float)Math.Atan2((double)(end.Y - start.Y), (double)(end.X - start.X));
            sprite.Draw(pixel, new Rectangle((int)start.X, (int)start.Y, width, UserSettings.LineWidth), null, color, rotation, new Vector2(0f, 0f), SpriteEffects.None, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FillRectangle(SpriteBatch sprite, Rectangle rectangle, Color color)
        {
            sprite.Draw(pixel, rectangle, color);
        }



        public void Start(AnimationState state, int trackIndex)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": start");
#endif
        }

        public void End(AnimationState state, int trackIndex)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": end");
#endif
        }

        public void Complete(AnimationState state, int trackIndex, int loopCount)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": complete " + loopCount);
#endif
        }

        public void Event(AnimationState state, int trackIndex, Event e)
        {
#if !WINDOWS_STOREAPP
            Console.WriteLine(trackIndex + " " + state.GetCurrent(trackIndex) + ": event " + e);
#endif
        }
    }
}
