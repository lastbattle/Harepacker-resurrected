using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.MapObjects.UIObject;
using HaCreator.MapSimulator.Objects.FieldObject;
using HaCreator.MapSimulator.Objects.UIObject;
using MobItem = HaCreator.MapSimulator.Objects.FieldObject.MobItem;
using HaRepacker.Utils;
using HaSharedLibrary;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpDX.Direct3D9;
using Spine;
using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// 
    /// http://rbwhitaker.wikidot.com/xna-tutorials
    /// </summary>
    public class MapSimulator : Microsoft.Xna.Framework.Game
    {
        public int mapShiftX = 0;
        public int mapShiftY = 0;
        public Point minimapPos;

        private int Width;
        private int Height;
        private float UserScreenScaleFactor = 1.0f;

        private RenderParameters _renderParams;
        private Matrix matrixScale;

        private GraphicsDeviceManager _DxDeviceManager;
        private readonly TexturePool texturePool = new TexturePool();
        private bool bSaveScreenshot = false, bSaveScreenshotComplete = true; // flag for saving a screenshot file

        private SpriteBatch spriteBatch;


        // Objects, NPCs (Lists for loading, arrays for iteration)
        public List<BaseDXDrawableItem>[] mapObjects;
        private readonly List<BaseDXDrawableItem> mapObjects_NPCs = new List<BaseDXDrawableItem>();
        private readonly List<BaseDXDrawableItem> mapObjects_Mobs = new List<BaseDXDrawableItem>();
        private readonly List<BaseDXDrawableItem> mapObjects_Reactors = new List<BaseDXDrawableItem>();
        private readonly List<BaseDXDrawableItem> mapObjects_Portal = new List<BaseDXDrawableItem>(); // perhaps mapobjects should be in a single pool
        private readonly List<BaseDXDrawableItem> mapObjects_tooltips = new List<BaseDXDrawableItem>();

        // Arrays for faster iteration (converted from Lists after loading)
        private BaseDXDrawableItem[][] _mapObjectsArray;
        private NpcItem[] _npcsArray;
        private MobItem[] _mobsArray;
        private ReactorItem[] _reactorsArray;
        private PortalItem[] _portalsArray;
        private TooltipItem[] _tooltipsArray;

        // Backgrounds
        private readonly List<BackgroundItem> backgrounds_front = new List<BackgroundItem>();
        private readonly List<BackgroundItem> backgrounds_back = new List<BackgroundItem>();

        // Arrays for faster iteration (converted from Lists after loading)
        private BackgroundItem[] _backgroundsFrontArray;
        private BackgroundItem[] _backgroundsBackArray;

        // Spatial partitioning grids for efficient culling (static objects only)
        private SpatialGrid<BaseDXDrawableItem> _mapObjectsGrid;
        private SpatialGrid<PortalItem> _portalsGrid;
        private SpatialGrid<ReactorItem> _reactorsGrid;
        private const int SPATIAL_GRID_CELL_SIZE = 512; // Cell size in pixels
        private bool _useSpatialPartitioning = false; // Enabled for large maps

        // Threshold for enabling spatial partitioning (object count)
        private const int SPATIAL_PARTITIONING_THRESHOLD = 100;

        // Cached visible objects from spatial query (reused each frame)
        private BaseDXDrawableItem[] _visibleMapObjects;
        private int _visibleMapObjectsCount;
        private PortalItem[] _visiblePortals;
        private int _visiblePortalsCount;
        private ReactorItem[] _visibleReactors;
        private int _visibleReactorsCount;

        // Boundary, borders
        private Rectangle vr_fieldBoundary;
        private Rectangle vr_rectangle; // the rectangle of the VR field, used for drawing the VR border
        private const int VR_BORDER_WIDTHHEIGHT = 600; // the height or width of the VR border
        private bool bDrawVRBorderLeftRight = false;
        private Texture2D texture_vrBoundaryRectLeft, texture_vrBoundaryRectRight, texture_vrBoundaryRectTop, texture_vrBoundaryRectBottom;

        private int LBSide = 0, LBTop = 0, LBBottom = 0;
        private Texture2D texture_lbLeft, texture_lbRight, texture_lbTop, texture_lbBottom; // Left, Right, Top, Bottom LB borders
        private const int LB_BORDER_WIDTHHEIGHT = 300; // additional width or height of LB Border outside VR
        private const int LB_BORDER_UI_MENUHEIGHT = 62; // The hardcoded values of the height of the UI menu bar at the bottom of the screen (Name, Level, HP, MP, etc.)
        private const int LB_BORDER_OFFSET_X = 150; // Offset from map edge

        // Mirror bottom boundaries (Reflections in Arcane river maps)
        private Rectangle rect_mirrorBottom;
        private ReflectionDrawableBoundary mirrorBottomReflection;

        // Minimap
        private MinimapUI miniMapUi;

        // Status bar
        private StatusBarUI statusBarUi;
        private StatusBarChatUI statusBarChatUI;

        // Cursor, mouse
        private MouseCursorItem mouseCursor;

        // Audio
        private WzSoundResourceStreamer audio;
        private WzSoundResourceStreamer portalSE; // Portal teleport sound effect
        private string _currentBgmName = null; // Track current BGM to avoid reloading same BGM on map change

        // Etc
        private Board mapBoard; // Not readonly - can be replaced during seamless map transitions
        private bool bBigBangUpdate = true; // Big-Bang update
        private bool bBigBang2Update = true; // Chaos update
        private bool bIsLoginMap = false; // if the simulated map is the Login map.
        private bool bIsCashShopMap = false; 

        // Spine
        private SkeletonMeshRenderer skeletonMeshRenderer;

        // Text
        private SpriteFont font_navigationKeysHelper;
        private SpriteFont font_DebugValues;
        private SpriteFont font_chat;

        // Chat system
        private readonly MapSimulatorChat _chat = new MapSimulatorChat();

        // Debug
        private Texture2D texture_debugBoundaryRect;
        private bool bShowDebugMode = false;
        private bool bHideUIMode = false;

        // Frame counter for visibility culling (increments each frame)
        private int _frameNumber = 0;

        // Portal teleportation - seamless map transitions
        private string _spawnPortalName = null; // The portal name to spawn at (set when loading map)
        private int _lastClickTime = 0; // For double-click detection
        private const int DOUBLE_CLICK_TIME_MS = 500; // Time window for double-click
        private PortalItem _lastClickedPortal = null; // Track which visible portal was clicked
        private PortalInstance _lastClickedHiddenPortal = null; // Track which hidden portal was clicked

        // Seamless map transition support
        private bool _pendingMapChange = false; // Flag to trigger map change in Update loop
        private int _pendingMapId = -1; // Target map ID for pending change
        private string _pendingPortalName = null; // Target portal name for pending change
        private Func<int, Tuple<Board, string>> _loadMapCallback = null; // Callback to load new map

        /// <summary>
        /// Sets the callback used to load maps for portal teleportation.
        /// </summary>
        public void SetLoadMapCallback(Func<int, Tuple<Board, string>> callback)
        {
            _loadMapCallback = callback;
        }


        // Debug rendering data (collected during draw, rendered in separate pass)
        private struct DebugDrawData
        {
            public Rectangle Rect;
            public string Text;
            public bool IsValid;
        }
        private DebugDrawData[] _debugMobData;
        private DebugDrawData[] _debugNpcData;
        private DebugDrawData[] _debugPortalData;
        private DebugDrawData[] _debugReactorData;
        private int _debugMobCount;
        private int _debugNpcCount;
        private int _debugPortalCount;
        private int _debugReactorCount;

        // Cached StringBuilder for debug text to avoid GC allocations every frame
        private readonly StringBuilder _debugStringBuilder = new StringBuilder(256);

        // Cached navigation help strings to avoid string.Format every frame
        private string _navHelpTextMobOn;
        private string _navHelpTextMobOff;

        /// <summary>
        /// MapSimulator Constructor
        /// </summary>
        /// <param name="mapBoard"></param>
        /// <param name="titleName"></param>
        /// <param name="spawnPortalName">Optional portal name to spawn at (from portal teleportation)</param>
        public MapSimulator(Board mapBoard, string titleName, string spawnPortalName = null)
        {
            this.mapBoard = mapBoard;
            this._spawnPortalName = spawnPortalName;

            // Check if the simulated map is the Login map. 'MapLogin1:MapLogin1'
            string[] titleNameParts = titleName.Split(':');
            this.bIsLoginMap = titleNameParts.All(part => part.Contains("MapLogin"));
            this.bIsCashShopMap = titleNameParts.All(part => part.Contains("CashShopPreview"));

            InitialiseWindowAndMap_WidthHeight();

            //RenderHeight += System.Windows.Forms.SystemInformation.CaptionHeight; // window title height

            //double dpi = ScreenDPIUtil.GetScreenScaleFactor();

            // set Form window height & width
            //this.Width = (int)(RenderWidth * dpi);
            //this.Height = (int)(RenderHeight * dpi);

            //Window.IsBorderless = true;
            //Window.Position = new Point(0, 0);
            Window.Title = titleName;
            IsFixedTimeStep = false; // dont cap fps
            IsMouseVisible = false; // draws our own custom cursor here.. 
            Content.RootDirectory = "Content";

            Window.ClientSizeChanged += Window_ClientSizeChanged;

            _DxDeviceManager = new GraphicsDeviceManager(this)
            {
                SynchronizeWithVerticalRetrace = true,
                HardwareModeSwitch = true,
                GraphicsProfile = GraphicsProfile.HiDef,
                IsFullScreen = false,
                PreferMultiSampling = true,
                SupportedOrientations = DisplayOrientation.Default,
                PreferredBackBufferWidth = Math.Max(_renderParams.RenderWidth, 1),
                PreferredBackBufferHeight = Math.Max(_renderParams.RenderHeight, 1),
                PreferredBackBufferFormat = SurfaceFormat.Color/* | SurfaceFormat.Bgr32 | SurfaceFormat.Dxt1| SurfaceFormat.Dxt5*/,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8, 
            };
            _DxDeviceManager.DeviceCreated += graphics_DeviceCreated;
            _DxDeviceManager.ApplyChanges();
            
        }

        #region Loading and unloading
        void graphics_DeviceCreated(object sender, EventArgs e)
        {
        }

        private void InitialiseWindowAndMap_WidthHeight()
        {
            RenderResolution mapRenderResolution = UserSettings.SimulateResolution;
            float RenderObjectScaling = 1.0f;
            int RenderHeight, RenderWidth;

            switch (mapRenderResolution)
            {
                case RenderResolution.Res_1024x768:  // 1024x768
                    Height = 768;
                    Width = 1024;
                    break;
                case RenderResolution.Res_1280x720: // 1280x720
                    Height = 720;
                    Width = 1280;
                    break;
                case RenderResolution.Res_1366x768:  // 1366x768
                    Height = 768;
                    Width = 1366;
                    break;


                case RenderResolution.Res_1920x1080: // 1920x1080
                    Height = 1080;
                    Width = 1920;
                    break;
                case RenderResolution.Res_1920x1080_120PercScaled: // 1920x1080
                    Height = 1080;
                    Width = 1920;
                    RenderObjectScaling = 1.2f;
                    break;
                case RenderResolution.Res_1920x1080_150PercScaled: // 1920x1080
                    Height = 1080;
                    Width = 1920;
                    RenderObjectScaling = 1.5f;
                    mapRenderResolution |= RenderResolution.Res_1366x768; // 1920x1080 is just 1366x768 with 150% scale.
                    break;


                case RenderResolution.Res_1920x1200: // 1920x1200
                    Height = 1200;
                    Width = 1920;
                    break;
                case RenderResolution.Res_1920x1200_120PercScaled: // 1920x1200
                    Height = 1200;
                    Width = 1920;
                    RenderObjectScaling = 1.2f;
                    break;
                case RenderResolution.Res_1920x1200_150PercScaled: // 1920x1200
                    Height = 1200;
                    Width = 1920;
                    RenderObjectScaling = 1.5f;
                    break;

                case RenderResolution.Res_All:
                case RenderResolution.Res_800x600: // 800x600
                default:
                    Height = 600;
                    Width = 800;
                    break;
            }
            this.UserScreenScaleFactor = (float) ScreenDPIUtil.GetScreenScaleFactor();

            RenderHeight = (int) (Height * UserScreenScaleFactor);
            RenderWidth = (int)(Width * UserScreenScaleFactor);
            RenderObjectScaling = (RenderObjectScaling * UserScreenScaleFactor);

            this.matrixScale = Matrix.CreateScale(RenderObjectScaling);

            this._renderParams = new RenderParameters(RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution);
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            // Create map layers
            mapObjects = new List<BaseDXDrawableItem>[MapConstants.MaxMapLayers];
            for (int i = 0; i < MapConstants.MaxMapLayers; i++)
            {
                mapObjects[i] = new List<BaseDXDrawableItem>();
            }

            // Initialize debug data arrays (sized generously to avoid reallocations)
            _debugMobData = new DebugDrawData[256];
            _debugNpcData = new DebugDrawData[64];
            _debugPortalData = new DebugDrawData[64];
            _debugReactorData = new DebugDrawData[64];

            //GraphicsDevice.Viewport = new Viewport(RenderWidth / 2 - 800 / 2, RenderHeight / 2 - 600 / 2, 800, 600);

            // https://stackoverflow.com/questions/55045066/how-do-i-convert-a-ttf-or-other-font-to-a-xnb-xna-game-studio-font
            // if you're having issues building on w10, install Visual C++ Redistributable for Visual Studio 2012 Update 4
            //
            // to build your own font: /MonoGame Font Builder/game.mgcb
            // build -> obj -> copy it over to HaRepacker-resurrected [Content]
            font_navigationKeysHelper = Content.Load<SpriteFont>("XnaDefaultFont");
            font_chat = Content.Load<SpriteFont>("XnaFont_Chat");//("XnaFont_Debug");
            font_DebugValues = Content.Load<SpriteFont>("XnaDefaultFont");//("XnaFont_Debug");

            // Pre-cache navigation help text strings to avoid string.Format allocations in Draw()
            _navHelpTextMobOn = "[Left] [Right] [Up] [Down] [Shift] for navigation.\n[F5] Debug mode | [F6] Mob movement (ON)\n[Alt+Enter] Full screen | [PrintSc] Screenshot\n[H] Hide UI | [Double-click] Portal to teleport\n[Enter] Chat | /help for commands";
            _navHelpTextMobOff = "[Left] [Right] [Up] [Down] [Shift] for navigation.\n[F5] Debug mode | [F6] Mob movement (OFF)\n[Alt+Enter] Full screen | [PrintSc] Screenshot\n[H] Hide UI | [Double-click] Portal to teleport\n[Enter] Chat | /help for commands";

            base.Initialize();
        }

        /// <summary>
        /// Load game assets
        /// </summary>
        protected override void LoadContent()
        {
            WzImage mapHelperImage = Program.FindImage("Map", "MapHelper.img");
            WzImage soundUIImage = Program.FindImage("Sound", "UI.img");
            WzImage uiToolTipImage = Program.FindImage("UI", "UIToolTip.img"); // UI_003.wz
            WzImage uiBasicImage = Program.FindImage("UI", "Basic.img");
            WzImage uiWindow1Image = Program.FindImage("UI", "UIWindow.img"); //
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img"); // doesnt exist before big-bang

            WzImage uiStatusBarImage = Program.FindImage("UI", "StatusBar.img");
            WzImage uiStatus2BarImage = Program.FindImage("UI", "StatusBar2.img");

            this.bBigBangUpdate = uiWindow2Image?["BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"] != null; // different rendering for pre and post-bb, to support multiple vers
            this.bBigBang2Update = uiWindow2Image?["BigBang2!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"] != null; // chaos update

            // BGM
            if (Program.InfoManager.BGMs.ContainsKey(mapBoard.MapInfo.bgm))
            {
                _currentBgmName = mapBoard.MapInfo.bgm;
                audio = new WzSoundResourceStreamer(Program.InfoManager.BGMs[mapBoard.MapInfo.bgm], true);
                if (audio != null)
                {
                    audio.Play();
                }
            }

            // Portal sound effect
            WzImage soundGameImage = Program.FindImage("Sound", "Game.img");
            if (soundGameImage != null)
            {
                WzBinaryProperty portalSound = (WzBinaryProperty)soundGameImage["Portal"];
                if (portalSound != null)
                {
                    portalSE = new WzSoundResourceStreamer(portalSound, false);
                }
            }

            if (mapBoard.VRRectangle == null)
            {
                vr_fieldBoundary = new Rectangle(0, 0, mapBoard.MapSize.X, mapBoard.MapSize.Y);
                vr_rectangle = new Rectangle(0, 0, mapBoard.MapSize.X, mapBoard.MapSize.Y);
            }
            else
            {
                vr_fieldBoundary = new Rectangle(
                    mapBoard.VRRectangle.X + mapBoard.CenterPoint.X, 
                    mapBoard.VRRectangle.Y + mapBoard.CenterPoint.Y, 
                    mapBoard.VRRectangle.Width, 
                    mapBoard.VRRectangle.Height);
                vr_rectangle = new Rectangle(mapBoard.VRRectangle.X, mapBoard.VRRectangle.Y, mapBoard.VRRectangle.Width, mapBoard.VRRectangle.Height);
            }
            //SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);

            // test benchmark
#if DEBUG
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
#endif

            /////// Background and objects
            List<WzObject> usedProps = new List<WzObject>();
            
            // Objects
            Task t_tiles = Task.Run(() =>
            {
                foreach (LayeredItem tileObj in mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;

                    mapObjects[tileObj.LayerNumber].Add(
                        MapSimulatorLoader.CreateMapItemFromProperty(texturePool, tileParent, tileObj.X, tileObj.Y, mapBoard.CenterPoint, _DxDeviceManager.GraphicsDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));
                }
            });

            // Background
            Task t_Background = Task.Run(() =>
            {
                foreach (BackgroundInstance background in mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);

                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);

                    if (bgItem != null)
                        backgrounds_front.Add(bgItem);
                }
            });

            // Reactors
            Task t_reactor = Task.Run(() =>
            {
                foreach (ReactorInstance reactor in mapBoard.BoardItems.Reactors)
                {
                    //WzImage imageProperty = (WzImage)NPCWZFile[reactorInfo.ID + ".img"];

                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(texturePool, reactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
            });

            // NPCs
            Task t_npc = Task.Run(() =>
            {
                foreach (NpcInstance npc in mapBoard.BoardItems.NPCs)
                {
                    //WzImage imageProperty = (WzImage) NPCWZFile[npcInfo.ID + ".img"];
                    if (npc.Hide)
                        continue;

                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(texturePool, npc, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (npcItem != null)
                        mapObjects_NPCs.Add(npcItem);
                }
            });

            // Mobs
            Task t_mobs = Task.Run(() =>
            {
                foreach (MobInstance mob in mapBoard.BoardItems.Mobs)
                {
                    //WzImage imageProperty = Program.WzManager.FindMobImage(mobInfo.ID); // Mob.wz Mob2.img Mob001.wz
                    if (mob.Hide)
                        continue;

                    MobItem npcItem = MapSimulatorLoader.CreateMobFromProperty(texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);

                    mapObjects_Mobs.Add(npcItem);
                }
            });

            // Portals
            Task t_portal = Task.Run(() =>
            {
                WzSubProperty portalParent = (WzSubProperty) mapHelperImage["portal"];

                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
                //WzSubProperty editorParent = (WzSubProperty) portalParent["editor"];

                foreach (PortalInstance portal in mapBoard.BoardItems.Portals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (portalItem != null)
                        mapObjects_Portal.Add(portalItem);
                }
            });

            // Tooltips
            Task t_tooltips = Task.Run(() =>
            {
                WzSubProperty farmFrameParent = (WzSubProperty) uiToolTipImage?["Item"]?["FarmFrame"]; // not exist before V update.
                foreach (ToolTipInstance tooltip in mapBoard.BoardItems.ToolTips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(texturePool, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);

                    mapObjects_tooltips.Add(item);
                }
            });

            // Cursor
            Task t_cursor = Task.Run(() =>
            {
                WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps, false);
            });

            // Spine object
            Task t_spine = Task.Run(() =>
            {
                skeletonMeshRenderer = new SkeletonMeshRenderer(GraphicsDevice)
                {
                    PremultipliedAlpha = false,
                };
                skeletonMeshRenderer.Effect.World = this.matrixScale;
            });

            // Minimap
            Task t_minimap = Task.Run(() =>
            {
                if (!this.bIsLoginMap && !mapBoard.MapInfo.hideMinimap && !this.bIsCashShopMap)
                {
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiBasicImage, mapBoard, GraphicsDevice, UserScreenScaleFactor, mapBoard.MapInfo.strMapName, mapBoard.MapInfo.strStreetName, soundUIImage, bBigBangUpdate);
                }
            });

            // Statusbar
            Task t_statusBar = Task.Run(() => {
                if (!this.bIsLoginMap && !this.bIsCashShopMap) {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, bBigBangUpdate);
                    if (statusBar != null) {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
            });

            while (!t_tiles.IsCompleted || !t_Background.IsCompleted || !t_reactor.IsCompleted || !t_npc.IsCompleted || !t_mobs.IsCompleted || !t_portal.IsCompleted ||
                !t_tooltips.IsCompleted || !t_cursor.IsCompleted || !t_spine.IsCompleted || !t_minimap.IsCompleted || !t_statusBar.IsCompleted)
            {
                Thread.Sleep(100);
            }

            // Initialize mob foothold references after all mobs are loaded
            InitializeMobFootholds();

            // Convert lists to arrays for faster iteration
            ConvertListsToArrays();

#if DEBUG
            // test benchmark
            watch.Stop();
            Debug.WriteLine($"Map WZ files loaded. Execution Time: {watch.ElapsedMilliseconds} ms");
#endif
            //
            spriteBatch = new SpriteBatch(GraphicsDevice);

            ///////////////////////////////////////////////
            ////// Default positioning for character //////
            ///////////////////////////////////////////////
            bool spawnPositionSet = false;

            // First, check if we're spawning from a portal teleport (has target portal name)
            if (!string.IsNullOrEmpty(_spawnPortalName))
            {
                // Find the portal with the matching name
                var targetPortal = mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _spawnPortalName);
                if (targetPortal != null)
                {
                    this.mapShiftX = targetPortal.X;
                    this.mapShiftY = targetPortal.Y;
                    spawnPositionSet = true;
                }
            }

            // Fallback: Get a random portal if any exists for spawnpoint
            if (!spawnPositionSet)
            {
                var startPortals = mapBoard.BoardItems.Portals.Where(portal => portal.pt == PortalType.StartPoint).ToList();
                if (startPortals.Any())
                {
                    Random random = new Random();
                    PortalInstance randomStartPortal = startPortals[random.Next(startPortals.Count)];
                    this.mapShiftX = randomStartPortal.X;
                    this.mapShiftY = randomStartPortal.Y;
                }
            }

            SetCameraMoveX(true, false, 0); // true true to center it, in case its out of the boundary
            SetCameraMoveX(false, true, 0);

            SetCameraMoveY(true, false, 0);
            SetCameraMoveY(false, true, 0);
            ///////////////////////////////////////////////

            ///////////////////////////////////////////////
            ///////////// Border //////////////////////////
            ///////////////////////////////////////////////
            int leftRightVRDifference = (int)((vr_fieldBoundary.Right - vr_fieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth) // viewing range is smaller than the render width.. 
            {
                this.bDrawVRBorderLeftRight = true; // flag

                this.texture_vrBoundaryRectLeft = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, vr_fieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this.texture_vrBoundaryRectRight = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, vr_fieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this.texture_vrBoundaryRectTop = CreateVRBorder(vr_fieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
                this.texture_vrBoundaryRectBottom = CreateVRBorder(vr_fieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
            }
            // LB Border
            if (mapBoard.MapInfo.LBSide != null)
            {
                LBSide = (int)mapBoard.MapInfo.LBSide;
                this.texture_lbLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + LBSide, this.Height, _DxDeviceManager.GraphicsDevice);
                this.texture_lbRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + LBSide, this.Height, _DxDeviceManager.GraphicsDevice);
            }
            if (mapBoard.MapInfo.LBTop != null)
            {
                LBTop = (int)mapBoard.MapInfo.LBTop;
                this.texture_lbTop = CreateLBBorder((int) (vr_fieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + LBTop, _DxDeviceManager.GraphicsDevice); // add a little more width to the top LB border for very small maps
            }
            if (mapBoard.MapInfo.LBBottom != null) 
            {
                LBBottom = (int)mapBoard.MapInfo.LBBottom;
                this.texture_lbBottom = CreateLBBorder((int) (vr_fieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + LBBottom, _DxDeviceManager.GraphicsDevice);
            }

            ///////////////////////////////////////////////

            // mirror bottom boundaries
            //rect_mirrorBottom
            if (mapBoard.MapInfo.mirror_Bottom)
            {
                if (mapBoard.MapInfo.VRLeft != null && mapBoard.MapInfo.VRRight != null)
                {
                    int vr_width = (int)mapBoard.MapInfo.VRRight - (int)mapBoard.MapInfo.VRLeft;
                    const int obj_mirrorBottom_height = 200;

                    rect_mirrorBottom = new Rectangle((int)mapBoard.MapInfo.VRLeft, (int)mapBoard.MapInfo.VRBottom - obj_mirrorBottom_height, vr_width, obj_mirrorBottom_height);

                    mirrorBottomReflection = new ReflectionDrawableBoundary(128, 255, "mirror", true, false);
                }
            }
            /*
            DXObject leftDXVRObject = new DXObject(
                vr_fieldBoundary.Left - VR_BORDER_WIDTHHEIGHT,
                vr_fieldBoundary.Top,
                texture_vrBoundaryRectLeft);
            this.leftVRBorderDrawableItem = new BaseDXDrawableItem(leftDXVRObject, false);
            //new BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType.Regular, 255, true, leftDXVRObject, false, (int) RenderResolution.Res_All);

            // Right VR
            DXObject rightDXVRObject = new DXObject(
                vr_fieldBoundary.Right,
                vr_fieldBoundary.Top,
                texture_vrBoundaryRectRight);
            this.rightVRBorderDrawableItem = new BaseDXDrawableItem(rightDXVRObject, false);
            */
            ///////////// End Border

            // Debug items
            System.Drawing.Bitmap bitmap_debug = new System.Drawing.Bitmap(1, 1);
            bitmap_debug.SetPixel(0, 0, System.Drawing.Color.White);
            texture_debugBoundaryRect = bitmap_debug.ToTexture2D(_DxDeviceManager.GraphicsDevice);

            // Initialize chat system
            _chat.Initialize(font_chat, texture_debugBoundaryRect, Height);
            RegisterChatCommands();

            // cleanup
            // clear used items
            foreach (WzObject obj in usedProps)
            {
                if (obj == null)
                    continue; // obj copied twice in usedProps?

                // Spine events
                WzSpineObject spineObj = (WzSpineObject) obj.MSTagSpine;
                if (spineObj != null)
                {
                    spineObj.state.Start += Start;
                    spineObj.state.End += End;
                    spineObj.state.Complete += Complete;
                    spineObj.state.Event += Event;
                }

                obj.MSTag = null;
                obj.MSTagSpine = null; // cleanup
            }
            usedProps.Clear();

        }

        /// <summary>
        /// Creates the black VR Border Texture2D object
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        private static Texture2D CreateVRBorder(int width, int height, GraphicsDevice graphicsDevice)
        {
            // Create array of black pixels
            Color[] colors = new Color[width * height];
            Array.Fill(colors, Color.Black);

            Texture2D texture_vrBoundaryRect = new Texture2D(graphicsDevice, width, height);
            texture_vrBoundaryRect.SetData(colors);
            return texture_vrBoundaryRect;
        }
        /// <summary>
        /// Creates the black LB Border Texture2D object used to mask maps that are too small for larger resolutions
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="graphicsDevice"></param>
        /// <returns></returns>
        private static Texture2D CreateLBBorder(int width, int height, GraphicsDevice graphicsDevice)
        {
            // Create array of black pixels
            Color[] colors = new Color[width * height];
            Array.Fill(colors, Color.Black);

            Texture2D texture_lb = new Texture2D(graphicsDevice, width, height);
            texture_lb.SetData(colors);
            return texture_lb;
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


            mapObjects_NPCs.Clear();
            mapObjects_Mobs.Clear();
            mapObjects_Reactors.Clear();
            mapObjects_Portal.Clear();

            backgrounds_front.Clear();
            backgrounds_back.Clear();

            texturePool.Dispose();

            // clear prior mirror bottom boundary
            rect_mirrorBottom = new Rectangle();
            mirrorBottomReflection = null;
        }

        /// <summary>
        /// Unloads current map content for seamless map transitions.
        /// Does not dispose shared resources (GraphicsDevice, SpriteBatch, fonts, cursor).
        /// Audio is handled separately in LoadMapContent to allow BGM continuity.
        /// </summary>
        private void UnloadMapContent()
        {
            // Note: Audio is NOT disposed here - handled in LoadMapContent to allow same BGM to continue playing

            // Clear object lists
            mapObjects_NPCs.Clear();
            mapObjects_Mobs.Clear();
            mapObjects_Reactors.Clear();
            mapObjects_Portal.Clear();
            mapObjects_tooltips.Clear();
            backgrounds_front.Clear();
            backgrounds_back.Clear();

            // Clear layer objects
            if (mapObjects != null)
            {
                for (int i = 0; i < mapObjects.Length; i++)
                {
                    mapObjects[i]?.Clear();
                }
            }

            // Clear arrays
            _mapObjectsArray = null;
            _npcsArray = null;
            _mobsArray = null;
            _reactorsArray = null;
            _portalsArray = null;
            _tooltipsArray = null;
            _backgroundsFrontArray = null;
            _backgroundsBackArray = null;

            // Clear spatial grids
            _mapObjectsGrid = null;
            _portalsGrid = null;
            _reactorsGrid = null;
            _visibleMapObjects = null;
            _visiblePortals = null;
            _visibleReactors = null;
            _useSpatialPartitioning = false;

            // Dispose VR border textures
            texture_vrBoundaryRectLeft?.Dispose();
            texture_vrBoundaryRectRight?.Dispose();
            texture_vrBoundaryRectTop?.Dispose();
            texture_vrBoundaryRectBottom?.Dispose();
            texture_vrBoundaryRectLeft = null;
            texture_vrBoundaryRectRight = null;
            texture_vrBoundaryRectTop = null;
            texture_vrBoundaryRectBottom = null;
            bDrawVRBorderLeftRight = false;

            // Dispose LB border textures
            texture_lbLeft?.Dispose();
            texture_lbRight?.Dispose();
            texture_lbTop?.Dispose();
            texture_lbBottom?.Dispose();
            texture_lbLeft = null;
            texture_lbRight = null;
            texture_lbTop = null;
            texture_lbBottom = null;
            LBSide = 0;
            LBTop = 0;
            LBBottom = 0;

            // Clear minimap and status bar
            miniMapUi = null;
            statusBarUi = null;
            statusBarChatUI = null;

            // Clear mirror boundaries
            rect_mirrorBottom = new Rectangle();
            mirrorBottomReflection = null;

            // Clear texture pool (dispose all textures)
            texturePool.DisposeAll();

            // Reset portal click tracking
            _lastClickedPortal = null;
            _lastClickedHiddenPortal = null;
            _lastClickTime = 0;

            // Deactivate chat input (but preserve message history)
            _chat.Deactivate();
        }

        /// <summary>
        /// Loads map content for a new map during seamless transitions.
        /// </summary>
        /// <param name="newBoard">The new map board to load</param>
        /// <param name="newTitle">The new window title</param>
        /// <param name="spawnPortalName">Optional portal name to spawn at</param>
        private void LoadMapContent(Board newBoard, string newTitle, string spawnPortalName)
        {
            this.mapBoard = newBoard;
            this._spawnPortalName = spawnPortalName;

            // Update window title
            Window.Title = newTitle;

            // Update map type flags
            string[] titleNameParts = newTitle.Split(':');
            this.bIsLoginMap = titleNameParts.All(part => part.Contains("MapLogin"));
            this.bIsCashShopMap = titleNameParts.All(part => part.Contains("CashShopPreview"));

            // Regenerate minimap if needed
            if (mapBoard.MiniMap == null)
                mapBoard.RegenerateMinimap();

            // Load WZ images needed for this map
            WzImage mapHelperImage = Program.FindImage("Map", "MapHelper.img");
            WzImage soundUIImage = Program.FindImage("Sound", "UI.img");
            WzImage uiToolTipImage = Program.FindImage("UI", "UIToolTip.img");
            WzImage uiBasicImage = Program.FindImage("UI", "Basic.img");
            WzImage uiWindow1Image = Program.FindImage("UI", "UIWindow.img");
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img");
            WzImage uiStatusBarImage = Program.FindImage("UI", "StatusBar.img");
            WzImage uiStatus2BarImage = Program.FindImage("UI", "StatusBar2.img");

            // BGM - only reload if different from current BGM
            string newBgmName = mapBoard.MapInfo.bgm;
            if (_currentBgmName != newBgmName)
            {
                // Different BGM - dispose old and load new
                if (audio != null)
                {
                    audio.Dispose();
                    audio = null;
                }

                if (Program.InfoManager.BGMs.ContainsKey(newBgmName))
                {
                    _currentBgmName = newBgmName;
                    audio = new WzSoundResourceStreamer(Program.InfoManager.BGMs[newBgmName], true);
                    audio?.Play();
                }
                else
                {
                    _currentBgmName = null;
                }
            }
            // If same BGM, just keep playing - no changes needed

            // VR boundaries
            if (mapBoard.VRRectangle == null)
            {
                vr_fieldBoundary = new Rectangle(0, 0, mapBoard.MapSize.X, mapBoard.MapSize.Y);
                vr_rectangle = new Rectangle(0, 0, mapBoard.MapSize.X, mapBoard.MapSize.Y);
            }
            else
            {
                vr_fieldBoundary = new Rectangle(
                    mapBoard.VRRectangle.X + mapBoard.CenterPoint.X,
                    mapBoard.VRRectangle.Y + mapBoard.CenterPoint.Y,
                    mapBoard.VRRectangle.Width,
                    mapBoard.VRRectangle.Height);
                vr_rectangle = new Rectangle(mapBoard.VRRectangle.X, mapBoard.VRRectangle.Y, mapBoard.VRRectangle.Width, mapBoard.VRRectangle.Height);
            }

            // Initialize layer lists
            for (int i = 0; i < mapObjects.Length; i++)
            {
                mapObjects[i] = new List<BaseDXDrawableItem>();
            }

            List<WzObject> usedProps = new List<WzObject>();

            // Load map objects in parallel
            Task t_tiles = Task.Run(() =>
            {
                foreach (LayeredItem tileObj in mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;
                    mapObjects[tileObj.LayerNumber].Add(
                        MapSimulatorLoader.CreateMapItemFromProperty(texturePool, tileParent, tileObj.X, tileObj.Y, mapBoard.CenterPoint, _DxDeviceManager.GraphicsDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));
                }
            });

            Task t_Background = Task.Run(() =>
            {
                foreach (BackgroundInstance background in mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_front.Add(bgItem);
                }
            });

            Task t_reactor = Task.Run(() =>
            {
                foreach (ReactorInstance reactor in mapBoard.BoardItems.Reactors)
                {
                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(texturePool, reactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
            });

            Task t_npc = Task.Run(() =>
            {
                foreach (NpcInstance npc in mapBoard.BoardItems.NPCs)
                {
                    if (npc.Hide)
                        continue;
                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(texturePool, npc, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (npcItem != null)
                        mapObjects_NPCs.Add(npcItem);
                }
            });

            Task t_mobs = Task.Run(() =>
            {
                foreach (MobInstance mob in mapBoard.BoardItems.Mobs)
                {
                    if (mob.Hide)
                        continue;
                    MobItem mobItem = MapSimulatorLoader.CreateMobFromProperty(texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    mapObjects_Mobs.Add(mobItem);
                }
            });

            Task t_portal = Task.Run(() =>
            {
                WzSubProperty portalParent = (WzSubProperty)mapHelperImage["portal"];
                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
                foreach (PortalInstance portal in mapBoard.BoardItems.Portals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (portalItem != null)
                        mapObjects_Portal.Add(portalItem);
                }
            });

            Task t_tooltips = Task.Run(() =>
            {
                WzSubProperty farmFrameParent = (WzSubProperty)uiToolTipImage?["Item"]?["FarmFrame"];
                foreach (ToolTipInstance tooltip in mapBoard.BoardItems.ToolTips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(texturePool, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);
                    mapObjects_tooltips.Add(item);
                }
            });

            Task t_minimap = Task.Run(() =>
            {
                if (!this.bIsLoginMap && !mapBoard.MapInfo.hideMinimap && !this.bIsCashShopMap)
                {
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiBasicImage, mapBoard, GraphicsDevice, UserScreenScaleFactor, mapBoard.MapInfo.strMapName, mapBoard.MapInfo.strStreetName, soundUIImage, bBigBangUpdate);
                }
            });

            Task t_statusBar = Task.Run(() =>
            {
                if (!this.bIsLoginMap && !this.bIsCashShopMap)
                {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, bBigBangUpdate);
                    if (statusBar != null)
                    {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
            });

            // Recreate cursor (textures were disposed in UnloadMapContent)
            Task t_cursor = Task.Run(() =>
            {
                WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps, false);
            });

            // Wait for all loading tasks
            Task.WaitAll(t_tiles, t_Background, t_reactor, t_npc, t_mobs, t_portal, t_tooltips, t_minimap, t_statusBar, t_cursor);

            // Initialize mob foothold references
            InitializeMobFootholds();

            // Convert lists to arrays
            ConvertListsToArrays();

            // Set camera position
            bool spawnPositionSet = false;
            if (!string.IsNullOrEmpty(_spawnPortalName))
            {
                var targetPortal = mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _spawnPortalName);
                if (targetPortal != null)
                {
                    this.mapShiftX = targetPortal.X;
                    this.mapShiftY = targetPortal.Y;
                    spawnPositionSet = true;
                }
            }
            if (!spawnPositionSet)
            {
                var startPortals = mapBoard.BoardItems.Portals.Where(portal => portal.pt == PortalType.StartPoint).ToList();
                if (startPortals.Any())
                {
                    Random random = new Random();
                    PortalInstance randomStartPortal = startPortals[random.Next(startPortals.Count)];
                    this.mapShiftX = randomStartPortal.X;
                    this.mapShiftY = randomStartPortal.Y;
                }
            }

            SetCameraMoveX(true, false, 0);
            SetCameraMoveX(false, true, 0);
            SetCameraMoveY(true, false, 0);
            SetCameraMoveY(false, true, 0);

            // Create border textures
            int leftRightVRDifference = (int)((vr_fieldBoundary.Right - vr_fieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth)
            {
                this.bDrawVRBorderLeftRight = true;
                this.texture_vrBoundaryRectLeft = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, vr_fieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this.texture_vrBoundaryRectRight = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, vr_fieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this.texture_vrBoundaryRectTop = CreateVRBorder(vr_fieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
                this.texture_vrBoundaryRectBottom = CreateVRBorder(vr_fieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
            }

            // LB borders
            if (mapBoard.MapInfo.LBSide != null)
            {
                LBSide = (int)mapBoard.MapInfo.LBSide;
                this.texture_lbLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + LBSide, this.Height, _DxDeviceManager.GraphicsDevice);
                this.texture_lbRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + LBSide, this.Height, _DxDeviceManager.GraphicsDevice);
            }
            if (mapBoard.MapInfo.LBTop != null)
            {
                LBTop = (int)mapBoard.MapInfo.LBTop;
                this.texture_lbTop = CreateLBBorder((int)(vr_fieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + LBTop, _DxDeviceManager.GraphicsDevice);
            }
            if (mapBoard.MapInfo.LBBottom != null)
            {
                LBBottom = (int)mapBoard.MapInfo.LBBottom;
                this.texture_lbBottom = CreateLBBorder((int)(vr_fieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + LBBottom, _DxDeviceManager.GraphicsDevice);
            }

            // Mirror bottom boundaries
            if (mapBoard.MapInfo.mirror_Bottom)
            {
                if (mapBoard.MapInfo.VRLeft != null && mapBoard.MapInfo.VRRight != null)
                {
                    int vr_width = (int)mapBoard.MapInfo.VRRight - (int)mapBoard.MapInfo.VRLeft;
                    const int obj_mirrorBottom_height = 200;
                    rect_mirrorBottom = new Rectangle((int)mapBoard.MapInfo.VRLeft, (int)mapBoard.MapInfo.VRBottom - obj_mirrorBottom_height, vr_width, obj_mirrorBottom_height);
                    mirrorBottomReflection = new ReflectionDrawableBoundary(128, 255, "mirror", true, false);
                }
            }

            // Cleanup spine event handlers
            foreach (WzObject obj in usedProps)
            {
                if (obj == null)
                    continue;
                WzSpineObject spineObj = (WzSpineObject)obj.MSTagSpine;
                if (spineObj != null)
                {
                    spineObj.state.Start += Start;
                    spineObj.state.End += End;
                    spineObj.state.Complete += Complete;
                    spineObj.state.Event += Event;
                }
                obj.MSTag = null;
                obj.MSTagSpine = null;
            }
            usedProps.Clear();
        }
#endregion
     
        #region Update and Drawing
        /// <summary>
        /// On game window size changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
        }

        private int currTickCount = Environment.TickCount;
        private int lastTickCount = Environment.TickCount;
        private KeyboardState oldKeyboardState = Keyboard.GetState();
        private MouseState oldMouseState = Mouse.GetState();

        // Mob movement enabled flag
        private bool bMobMovementEnabled = true;
        /// <summary>
        /// Key, and frame update handling
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Update(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            currTickCount = Environment.TickCount;
            float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;
            KeyboardState newKeyboardState = Keyboard.GetState();  // get the newest state
            MouseState newMouseState = mouseCursor.MouseState;

            // Allows the game to exit (but not while chat is active - Escape closes chat instead)
#if !WINDOWS_STOREAPP
            if (!_chat.IsActive &&
                (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape)))
            {
                this.Exit();
                return;
            }
#endif
            // Handle full screen
            bool bIsAltEnterPressed = newKeyboardState.IsKeyDown(Keys.LeftAlt) && newKeyboardState.IsKeyDown(Keys.Enter);
            if (bIsAltEnterPressed)
            {
                _DxDeviceManager.IsFullScreen = !_DxDeviceManager.IsFullScreen;
                _DxDeviceManager.ApplyChanges();
                return;
            }

            // Handle print screen
            if (newKeyboardState.IsKeyDown(Keys.PrintScreen))
            {
                if (!bSaveScreenshot && bSaveScreenshotComplete)
                {
                    this.bSaveScreenshot = true; // flag for screenshot
                    this.bSaveScreenshotComplete = false;
                }
            }


            // Handle mouse
            mouseCursor.UpdateCursorState();

            // Handle portal double-click for teleportation
            HandlePortalDoubleClick(newMouseState);

            // Handle pending map change (seamless transition)
            if (_pendingMapChange && _loadMapCallback != null)
            {
                _pendingMapChange = false;

                // Check if teleporting within the same map - just reposition camera
                if (_pendingMapId == mapBoard.MapInfo.id && !string.IsNullOrEmpty(_pendingPortalName))
                {
                    var targetPortal = mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _pendingPortalName);
                    if (targetPortal != null)
                    {
                        this.mapShiftX = targetPortal.X;
                        this.mapShiftY = targetPortal.Y;
                        SetCameraMoveX(true, false, 0);
                        SetCameraMoveX(false, true, 0);
                        SetCameraMoveY(true, false, 0);
                        SetCameraMoveY(false, true, 0);
                    }
                }
                else
                {
                    // Different map - perform full reload
                    var result = _loadMapCallback(_pendingMapId);
                    if (result != null && result.Item1 != null)
                    {
                        // Perform seamless map transition
                        UnloadMapContent();
                        LoadMapContent(result.Item1, result.Item2, _pendingPortalName);
                    }
                }
                _pendingMapId = -1;
                _pendingPortalName = null;
                return; // Skip the rest of this frame
            }

            // Handle chat input (returns true if chat consumed the input)
            bool chatConsumedInput = _chat.HandleInput(newKeyboardState, oldKeyboardState, currTickCount);

            // Skip navigation and other key handlers if chat is active
            if (!chatConsumedInput && !_chat.IsActive)
            {
                // Navigate around the rendered object
                bool bIsShiftPressed = newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift);

                bool bIsUpKeyPressed = newKeyboardState.IsKeyDown(Keys.Up);
                bool bIsDownKeyPressed = newKeyboardState.IsKeyDown(Keys.Down);
                bool bIsLeftKeyPressed = newKeyboardState.IsKeyDown(Keys.Left);
                bool bIsRightKeyPressed = newKeyboardState.IsKeyDown(Keys.Right);

                int moveOffset = bIsShiftPressed ? (int)(3000f / frameRate) : (int)(1500f / frameRate); // move a fixed amount a second, not dependent on GPU speed
                if (bIsLeftKeyPressed || bIsRightKeyPressed)
                {
                    SetCameraMoveX(bIsLeftKeyPressed, bIsRightKeyPressed, moveOffset);
                }
                if (bIsUpKeyPressed || bIsDownKeyPressed)
                {
                    SetCameraMoveY(bIsUpKeyPressed, bIsDownKeyPressed, moveOffset);
                }

                // Minimap M
                if (newKeyboardState.IsKeyDown(Keys.M))
                {
                    if (miniMapUi != null)
                        miniMapUi.MinimiseOrMaximiseMinimap(currTickCount);
                }

                // Hide UI
                if (newKeyboardState.IsKeyUp(Keys.H) && oldKeyboardState.IsKeyDown(Keys.H)) {
                    this.bHideUIMode = !this.bHideUIMode;
                }
            }

            // Debug keys
            if (newKeyboardState.IsKeyUp(Keys.F5) && oldKeyboardState.IsKeyDown(Keys.F5))
            {
                this.bShowDebugMode = !this.bShowDebugMode;
            }

            // Toggle mob movement with F6
            if (newKeyboardState.IsKeyUp(Keys.F6) && oldKeyboardState.IsKeyDown(Keys.F6))
            {
                this.bMobMovementEnabled = !this.bMobMovementEnabled;
            }

            // Update mob movement
            UpdateMobMovement(gameTime);

            // Update NPC movement and action cycling
            UpdateNpcActions(gameTime);

            // Pre-calculate visibility for all objects (culling optimization)
            _frameNumber++;
            UpdateObjectVisibility();

            this.oldKeyboardState = newKeyboardState;  // set the new state as the old state for next time
            this.oldMouseState = newMouseState;  // set the new state as the old state for next time

            base.Update(gameTime);
        }

        /// <summary>
        /// Pre-calculates visibility for all drawable objects.
        /// This avoids redundant visibility checks during Draw phase.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateObjectVisibility()
        {
            int centerX = mapBoard.CenterPoint.X;
            int centerY = mapBoard.CenterPoint.Y;
            int viewWidth = _renderParams.RenderWidth;
            int viewHeight = _renderParams.RenderHeight;

            if (_useSpatialPartitioning)
            {
                // Use spatial partitioning for large maps
                UpdateObjectVisibilitySpatial(centerX, centerY, viewWidth, viewHeight);
            }
            else
            {
                // Use standard iteration for small maps
                UpdateObjectVisibilityStandard(centerX, centerY, viewWidth, viewHeight);
            }

            // Mobs and NPCs always visible (they handle their own position-based culling)
            // Backgrounds always visible (they tile/scroll and handle their own culling)

            // Update mirror boundaries for mobs and NPCs (cached to avoid per-frame checks)
            UpdateMirrorBoundaries();
        }

        /// <summary>
        /// Standard visibility update - iterates all objects
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateObjectVisibilityStandard(int centerX, int centerY, int viewWidth, int viewHeight)
        {
            // Update map objects visibility
            if (_mapObjectsArray != null)
            {
                for (int layer = 0; layer < _mapObjectsArray.Length; layer++)
                {
                    BaseDXDrawableItem[] layerItems = _mapObjectsArray[layer];
                    for (int i = 0; i < layerItems.Length; i++)
                    {
                        layerItems[i].UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);
                    }
                }
            }

            // Update portal visibility
            for (int i = 0; i < _portalsArray.Length; i++)
            {
                _portalsArray[i].UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);
            }

            // Update reactor visibility
            for (int i = 0; i < _reactorsArray.Length; i++)
            {
                _reactorsArray[i].UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);
            }
        }

        /// <summary>
        /// Spatial partitioning visibility update - only queries nearby cells
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateObjectVisibilitySpatial(int centerX, int centerY, int viewWidth, int viewHeight)
        {
            // Calculate view bounds in world coordinates
            Rectangle viewBounds = new Rectangle(
                mapShiftX - centerX - viewWidth / 2,
                mapShiftY - centerY - viewHeight / 2,
                viewWidth * 2,  // Expand to catch objects at edges
                viewHeight * 2
            );

            // Query and mark visible map objects
            _visibleMapObjectsCount = _mapObjectsGrid.QueryToArray(viewBounds, _visibleMapObjects);
            for (int i = 0; i < _visibleMapObjectsCount; i++)
            {
                _visibleMapObjects[i].SetVisible(true);
                _visibleMapObjects[i].UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);
            }

            // Query and mark visible portals
            _visiblePortalsCount = _portalsGrid.QueryToArray(viewBounds, _visiblePortals);
            for (int i = 0; i < _visiblePortalsCount; i++)
            {
                _visiblePortals[i].SetVisible(true);
                _visiblePortals[i].UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);
            }

            // Query and mark visible reactors
            _visibleReactorsCount = _reactorsGrid.QueryToArray(viewBounds, _visibleReactors);
            for (int i = 0; i < _visibleReactorsCount; i++)
            {
                _visibleReactors[i].SetVisible(true);
                _visibleReactors[i].UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);
            }
        }

        /// <summary>
        /// Pre-calculates mirror boundaries for mobs and NPCs.
        /// Uses caching to avoid redundant checks every frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMirrorBoundaries()
        {
            // Create a lambda to check mirror field data boundaries
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData = (x, y) =>
            {
                var mirrorData = mapBoard.BoardItems.CheckObjectWithinMirrorFieldDataBoundary(x, y, MirrorFieldDataType.mob);
                return mirrorData?.ReflectionInfo;
            };

            // Update mobs
            for (int i = 0; i < _mobsArray.Length; i++)
            {
                _mobsArray[i].UpdateMirrorBoundary(rect_mirrorBottom, mirrorBottomReflection, checkMirrorFieldData);
            }

            // Update NPCs (using npc mirror type)
            Func<int, int, ReflectionDrawableBoundary> checkNpcMirrorFieldData = (x, y) =>
            {
                var mirrorData = mapBoard.BoardItems.CheckObjectWithinMirrorFieldDataBoundary(x, y, MirrorFieldDataType.npc);
                return mirrorData?.ReflectionInfo;
            };

            for (int i = 0; i < _npcsArray.Length; i++)
            {
                _npcsArray[i].UpdateMirrorBoundary(rect_mirrorBottom, mirrorBottomReflection, checkNpcMirrorFieldData);
            }
        }

        /// <summary>
        /// Updates all mob positions based on their movement logic
        /// </summary>
        /// <param name="gameTime"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMobMovement(GameTime gameTime)
        {
            if (!bMobMovementEnabled)
                return;

            int deltaTimeMs = (int)gameTime.ElapsedGameTime.TotalMilliseconds;

            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mobItem = _mobsArray[i];
                if (mobItem == null || mobItem.MovementInfo == null)
                    continue;

                mobItem.MovementEnabled = bMobMovementEnabled;
                mobItem.UpdateMovement(deltaTimeMs);
            }
        }

        /// <summary>
        /// Updates all NPC movement and action cycling
        /// </summary>
        /// <param name="gameTime"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateNpcActions(GameTime gameTime)
        {
            int deltaTimeMs = (int)gameTime.ElapsedGameTime.TotalMilliseconds;

            for (int i = 0; i < _npcsArray.Length; i++)
            {
                _npcsArray[i].Update(deltaTimeMs);
            }
        }

        /// <summary>
        /// Handles double-click on portals for map teleportation.
        /// When a portal with a valid target map is double-clicked, sets the target map ID and exits.
        /// Also supports hidden/invisible portals that have valid map destinations.
        /// </summary>
        /// <param name="mouseState">Current mouse state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandlePortalDoubleClick(MouseState mouseState)
        {
            // Check for left mouse button click (transition from pressed to released)
            bool isLeftClick = mouseState.LeftButton == ButtonState.Released && oldMouseState.LeftButton == ButtonState.Pressed;
            if (!isLeftClick)
                return;

            // Calculate mouse position relative to map coordinates
            int mouseMapX = mouseState.X + mapShiftX - mapBoard.CenterPoint.X;
            int mouseMapY = mouseState.Y + mapShiftY - mapBoard.CenterPoint.Y;

            const int PORTAL_HIT_PADDING = 15;
            const int HIDDEN_PORTAL_HIT_SIZE = 30; // Default hit area for hidden portals

            // Find which visible portal was clicked (if any)
            PortalItem clickedPortal = null;
            for (int i = 0; i < _portalsArray.Length; i++)
            {
                PortalItem portal = _portalsArray[i];
                PortalInstance instance = portal.PortalInstance;

                // Calculate portal bounds (portals are centered on their position)
                int portalLeft = instance.X - instance.Width / 2;
                int portalRight = instance.X + instance.Width / 2;
                int portalTop = instance.Y - instance.Height;
                int portalBottom = instance.Y;

                // Expand hit area slightly for easier clicking
                if (mouseMapX >= portalLeft - PORTAL_HIT_PADDING &&
                    mouseMapX <= portalRight + PORTAL_HIT_PADDING &&
                    mouseMapY >= portalTop - PORTAL_HIT_PADDING &&
                    mouseMapY <= portalBottom + PORTAL_HIT_PADDING)
                {
                    clickedPortal = portal;
                    break;
                }
            }

            // If visible portal found, handle it
            if (clickedPortal != null)
            {
                int currentTime = currTickCount;
                if (_lastClickedPortal == clickedPortal && (currentTime - _lastClickTime) <= DOUBLE_CLICK_TIME_MS)
                {
                    // Double-click detected! Check if portal has a valid destination
                    int targetMapId = clickedPortal.PortalInstance.tm;
                    if (targetMapId != MapConstants.MaxMap && targetMapId > 0)
                    {
                        PlayPortalSE();
                        _pendingMapChange = true;
                        _pendingMapId = targetMapId;
                        _pendingPortalName = clickedPortal.PortalInstance.tn;
                        return;
                    }
                }

                // Record this click for potential double-click detection
                _lastClickedPortal = clickedPortal;
                _lastClickedHiddenPortal = null;
                _lastClickTime = currentTime;
                return;
            }

            // No visible portal found - check hidden portals from mapBoard.BoardItems.Portals
            PortalInstance clickedHiddenPortal = null;
            foreach (var portal in mapBoard.BoardItems.Portals)
            {
                // Only consider hidden portals with valid map destinations
                if (portal.tm <= 0 || portal.tm == MapConstants.MaxMap)
                    continue;

                // Use portal's range if available, otherwise use default hit area
                int halfWidth = portal.hRange ?? HIDDEN_PORTAL_HIT_SIZE / 2;
                int halfHeight = portal.vRange ?? HIDDEN_PORTAL_HIT_SIZE / 2;

                if (mouseMapX >= portal.X - halfWidth &&
                    mouseMapX <= portal.X + halfWidth &&
                    mouseMapY >= portal.Y - halfHeight &&
                    mouseMapY <= portal.Y + halfHeight)
                {
                    clickedHiddenPortal = portal;
                    break;
                }
            }

            if (clickedHiddenPortal != null)
            {
                int currentTime = currTickCount;
                if (_lastClickedHiddenPortal == clickedHiddenPortal && (currentTime - _lastClickTime) <= DOUBLE_CLICK_TIME_MS)
                {
                    // Double-click detected on hidden portal
                    PlayPortalSE();
                    _pendingMapChange = true;
                    _pendingMapId = clickedHiddenPortal.tm;
                    _pendingPortalName = clickedHiddenPortal.tn;
                    return;
                }

                // Record this click for potential double-click detection
                _lastClickedHiddenPortal = clickedHiddenPortal;
                _lastClickedPortal = null;
                _lastClickTime = currentTime;
                return;
            }

            // Clicked outside any portal, reset tracking
            _lastClickedPortal = null;
            _lastClickedHiddenPortal = null;
        }

        /// <summary>
        /// Plays the portal teleport sound effect.
        /// </summary>
        private void PlayPortalSE()
        {
            portalSE?.Play();
        }

        /// <summary>
        /// Converts Lists to arrays for faster iteration.
        /// Call this after loading is complete.
        /// </summary>
        private void ConvertListsToArrays()
        {
            // Convert map objects (tiles, objects per layer)
            if (mapObjects != null)
            {
                _mapObjectsArray = new BaseDXDrawableItem[mapObjects.Length][];
                for (int i = 0; i < mapObjects.Length; i++)
                {
                    _mapObjectsArray[i] = mapObjects[i]?.ToArray() ?? Array.Empty<BaseDXDrawableItem>();
                }
            }

            // Convert NPCs
            _npcsArray = mapObjects_NPCs.Count > 0
                ? mapObjects_NPCs.Cast<NpcItem>().ToArray()
                : Array.Empty<NpcItem>();

            // Convert Mobs
            _mobsArray = mapObjects_Mobs.Count > 0
                ? mapObjects_Mobs.Cast<MobItem>().ToArray()
                : Array.Empty<MobItem>();

            // Convert Reactors
            _reactorsArray = mapObjects_Reactors.Count > 0
                ? mapObjects_Reactors.Cast<ReactorItem>().ToArray()
                : Array.Empty<ReactorItem>();

            // Convert Portals
            _portalsArray = mapObjects_Portal.Count > 0
                ? mapObjects_Portal.Cast<PortalItem>().ToArray()
                : Array.Empty<PortalItem>();

            // Convert Tooltips
            _tooltipsArray = mapObjects_tooltips.Count > 0
                ? mapObjects_tooltips.Cast<TooltipItem>().ToArray()
                : Array.Empty<TooltipItem>();

            // Convert Backgrounds
            _backgroundsFrontArray = backgrounds_front.ToArray();
            _backgroundsBackArray = backgrounds_back.ToArray();

            // Initialize spatial partitioning if map has enough objects
            InitializeSpatialPartitioning();
        }

        /// <summary>
        /// Initializes spatial partitioning grids for large maps.
        /// Only enabled if total object count exceeds threshold.
        /// </summary>
        private void InitializeSpatialPartitioning()
        {
            // Count total static objects
            int totalMapObjects = 0;
            if (_mapObjectsArray != null)
            {
                for (int i = 0; i < _mapObjectsArray.Length; i++)
                {
                    totalMapObjects += _mapObjectsArray[i].Length;
                }
            }

            int totalStaticObjects = totalMapObjects + _portalsArray.Length + _reactorsArray.Length;

            // Only enable spatial partitioning for large maps
            if (totalStaticObjects < SPATIAL_PARTITIONING_THRESHOLD)
            {
                _useSpatialPartitioning = false;
                return;
            }

            _useSpatialPartitioning = true;

            // Get map bounds from VR rectangle or map size
            Rectangle mapBounds = mapBoard.VRRectangle != null
                ? new Rectangle(mapBoard.VRRectangle.X, mapBoard.VRRectangle.Y,
                    mapBoard.VRRectangle.Width, mapBoard.VRRectangle.Height)
                : new Rectangle(-mapBoard.CenterPoint.X, -mapBoard.CenterPoint.Y,
                    mapBoard.MapSize.X, mapBoard.MapSize.Y);

            // Expand bounds slightly to handle edge cases
            mapBounds.Inflate(SPATIAL_GRID_CELL_SIZE, SPATIAL_GRID_CELL_SIZE);

            // Initialize grids
            _mapObjectsGrid = new SpatialGrid<BaseDXDrawableItem>(mapBounds, SPATIAL_GRID_CELL_SIZE);
            _portalsGrid = new SpatialGrid<PortalItem>(mapBounds, SPATIAL_GRID_CELL_SIZE);
            _reactorsGrid = new SpatialGrid<ReactorItem>(mapBounds, SPATIAL_GRID_CELL_SIZE);

            // Populate map objects grid
            if (_mapObjectsArray != null)
            {
                for (int layer = 0; layer < _mapObjectsArray.Length; layer++)
                {
                    BaseDXDrawableItem[] layerItems = _mapObjectsArray[layer];
                    for (int i = 0; i < layerItems.Length; i++)
                    {
                        BaseDXDrawableItem item = layerItems[i];
                        IDXObject frame = item.LastFrameDrawn ?? item.Frame0;
                        if (frame != null)
                        {
                            // Use frame position as object position
                            _mapObjectsGrid.Add(item, frame.X, frame.Y);
                        }
                    }
                }
            }

            // Populate portals grid
            for (int i = 0; i < _portalsArray.Length; i++)
            {
                PortalItem portal = _portalsArray[i];
                _portalsGrid.Add(portal, portal.PortalInstance.X, portal.PortalInstance.Y);
            }

            // Populate reactors grid
            for (int i = 0; i < _reactorsArray.Length; i++)
            {
                ReactorItem reactor = _reactorsArray[i];
                _reactorsGrid.Add(reactor, reactor.ReactorInstance.X, reactor.ReactorInstance.Y);
            }

            // Allocate visible object arrays (sized to handle worst case)
            _visibleMapObjects = new BaseDXDrawableItem[totalMapObjects];
            _visiblePortals = new PortalItem[_portalsArray.Length];
            _visibleReactors = new ReactorItem[_reactorsArray.Length];

#if DEBUG
            var (totalCells, occupiedCells, maxPerCell) = _mapObjectsGrid.GetStats();
            System.Diagnostics.Debug.WriteLine($"Spatial partitioning enabled: {totalStaticObjects} objects, {totalCells} cells ({occupiedCells} occupied), max {maxPerCell}/cell");
#endif
        }

        /// <summary>
        /// Initializes foothold references for all mobs.
        /// Call this after loading is complete.
        /// </summary>
        private void InitializeMobFootholds()
        {
            var footholds = mapBoard.BoardItems.FootholdLines;

            // Use raw VR coordinates (without CenterPoint offset) since mob coordinates are in raw format
            Rectangle rawVR = mapBoard.VRRectangle != null
                ? new Rectangle(mapBoard.VRRectangle.X, mapBoard.VRRectangle.Y, mapBoard.VRRectangle.Width, mapBoard.VRRectangle.Height)
                : new Rectangle(-mapBoard.CenterPoint.X, -mapBoard.CenterPoint.Y, mapBoard.MapSize.X, mapBoard.MapSize.Y);

            foreach (MobItem mobItem in mapObjects_Mobs)
            {
                if (mobItem?.MovementInfo == null)
                    continue;

                // Set map boundaries using raw VR coordinates (mob coordinates are in raw map format)
                mobItem.SetMapBoundaries(
                    rawVR.Left,
                    rawVR.Right,
                    rawVR.Top,
                    rawVR.Bottom
                );

                // Find footholds for ground-based mobs (including jumping mobs)
                if (footholds != null && footholds.Count > 0)
                {
                    if (mobItem.MovementInfo.MoveType == Objects.FieldObject.MobMoveType.Move ||
                        mobItem.MovementInfo.MoveType == Objects.FieldObject.MobMoveType.Stand ||
                        mobItem.MovementInfo.MoveType == Objects.FieldObject.MobMoveType.Jump)
                    {
                        mobItem.MovementInfo.FindCurrentFoothold(footholds);
                    }
                }
            }
        }

        /// <summary>
        /// On frame draw
        /// </summary>
        /// <param name="gameTime"></param>
        protected override void Draw(GameTime gameTime)
        {
            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            int TickCount = currTickCount;
            //float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;

            MouseState mouseState = this.oldMouseState;
            int mouseXRelativeToMap = mouseState.X - mapShiftX;
            int mouseYRelativeToMap = mouseState.Y - mapShiftY;
            //System.Diagnostics.Debug.WriteLine("Mouse relative to map: X {0}, Y {1}", mouseXRelativeToMap, mouseYRelativeToMap);

            // The coordinates of the map's center point, obtained from mapBoard.CenterPoint
            int mapCenterX = mapBoard.CenterPoint.X;
            int mapCenterY = mapBoard.CenterPoint.Y;

            // A Vector2 that calculates the offset between the map's current position (mapShiftX, mapShiftY) and its center point:
            // This shift vector is used in various Draw methods to properly position elements relative to the map's current view position.
            var shiftCenter = new Vector2(mapShiftX - mapCenterX, mapShiftY - mapCenterY);

            //GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0); // Clear the window to black
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin(
                SpriteSortMode.Immediate, // spine :( needs to be drawn immediately to maintain the layer orders
                                          //SpriteSortMode.Deferred,
                BlendState.NonPremultiplied, 
                Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp, // Add proper sampling
                DepthStencilState.None, 
                RasterizerState.CullCounterClockwise, 
                null, 
                this.matrixScale);
            //skeletonMeshRenderer.Begin();

            DrawLayer(_backgroundsBackArray, gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, TickCount); // back background
            DrawMapObjects(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, TickCount); // tiles and objects
            DrawMobs(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, TickCount); // mobs - rendered behind portals
            DrawPortals(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, TickCount); // portals
            DrawReactors(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, TickCount); // reactors
            DrawNpcs(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, TickCount); // NPCs - rendered on top
            DrawLayer(_backgroundsFrontArray, gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, TickCount); // front background

            // Borders
            // Create any rectangle you want. Here we'll use the TitleSafeArea for fun.
            //Rectangle titleSafeRectangle = GraphicsDevice.Viewport.TitleSafeArea;
            //DrawBorder(spriteBatch, titleSafeRectangle, 1, Color.Black);

            DrawVRFieldBorder(spriteBatch);
            DrawLBFieldBorder(spriteBatch);

            // Debug overlays (separate pass - only runs when debug mode is on)
            DrawDebugOverlays(shiftCenter, TickCount);

            //////////////////// UI related here ////////////////////
            DrawTooltip(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, mouseState, TickCount); 

            // Status bar [layer below minimap]
            if (!bHideUIMode) {
                DrawUI(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, mouseState, TickCount); // status bar and minimap
            }

            if (gameTime.TotalGameTime.TotalSeconds < 5)
                spriteBatch.DrawString(font_navigationKeysHelper,
                    bMobMovementEnabled ? _navHelpTextMobOn : _navHelpTextMobOff,
                    new Vector2(20, Height - 190), Color.White);
            
            if (!bSaveScreenshot && bShowDebugMode)
            {
                _debugStringBuilder.Clear();
                _debugStringBuilder.Append("FPS: ").Append(frameRate).Append('\n');
                _debugStringBuilder.Append("Cursor: X ").Append(mouseState.X).Append(", Y ").Append(mouseState.Y).Append('\n');
                _debugStringBuilder.Append("Relative cursor: X ").Append(mouseXRelativeToMap).Append(", Y ").Append(mouseYRelativeToMap);

                spriteBatch.DrawString(font_DebugValues, _debugStringBuilder,
                    new Vector2(Width - 270, 10), Color.White); // use the original width to render text
            }

            // Draw chat messages and input box
            if (!bHideUIMode)
            {
                _chat.Draw(spriteBatch, TickCount);
            }

            // Cursor [this is in front of everything else]
            mouseCursor.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                0, 0, 0, 0, // pos determined in the class
                null,
                _renderParams,
                TickCount);

            spriteBatch.End();
            //skeletonMeshRenderer.End();
            
            // Save screenshot if render is activated
            DoScreenshot();


            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws the map layer (back background or front-background)
        /// </summary>
        /// <param name="items"></param>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawLayer(BackgroundItem[] items, GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
             int mapCenterX, int mapCenterY, int TickCount)
        {
            for (int i = 0; i < items.Length; i++)
            {
                items[i].Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                    null,
                    _renderParams,
                    TickCount);
            }
        }

        /// <summary>
        /// Draws the map object
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawMapObjects(GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
             int mapCenterX, int mapCenterY, int TickCount)
        {
            if (_mapObjectsArray == null) return;

            for (int layer = 0; layer < _mapObjectsArray.Length; layer++)
            {
                BaseDXDrawableItem[] layerItems = _mapObjectsArray[layer];
                for (int i = 0; i < layerItems.Length; i++)
                {
                    BaseDXDrawableItem item = layerItems[i];
                    // Skip objects that were pre-calculated as invisible
                    if (!item.IsVisible)
                        continue;

                    item.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                        null,
                        renderParams,
                        TickCount);
                }
            }
        }

        /// <summary>
        /// Draws the portals
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawPortals(GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
            int mapCenterX, int mapCenterY, int TickCount)
        {
            // Portals
            for (int i = 0; i < _portalsArray.Length; i++)
            {
                PortalItem portalItem = _portalsArray[i];
                // Skip portals that were pre-calculated as invisible
                if (!portalItem.IsVisible)
                    continue;

                portalItem.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                    null,
                    _renderParams,
                    TickCount);
            }
        }

        /// <summary>
        /// Draws the reactors
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawReactors(GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
            int mapCenterX, int mapCenterY, int TickCount)
        {
            // Reactors
            for (int i = 0; i < _reactorsArray.Length; i++)
            {
                ReactorItem reactorItem = _reactorsArray[i];
                // Skip reactors that were pre-calculated as invisible
                if (!reactorItem.IsVisible)
                    continue;

                reactorItem.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                    null,
                    _renderParams,
                    TickCount);
            }
        }

        /// <summary>
        /// Draws the mob objects
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawMobs(
            GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
            int mapCenterX, int mapCenterY, int TickCount)
        {
            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mobItem = _mobsArray[i];
                // Use cached mirror boundary (updated in UpdateMirrorBoundaries)
                ReflectionDrawableBoundary mirrorFieldData = mobItem.CachedMirrorBoundary;

                mobItem.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                    mirrorFieldData,
                    _renderParams,
                    TickCount);
            }
        }

        /// <summary>
        /// Draws the NPC objects
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawNpcs(
            GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
            int mapCenterX, int mapCenterY, int TickCount)
        {
            for (int i = 0; i < _npcsArray.Length; i++)
            {
                NpcItem npcItem = _npcsArray[i];
                // Use cached mirror boundary (updated in UpdateMirrorBoundaries)
                ReflectionDrawableBoundary mirrorFieldData = npcItem.CachedMirrorBoundary;

                npcItem.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                    mirrorFieldData,
                    _renderParams,
                    TickCount);
            }
        }

        /// <summary>
        /// Draws debug overlays for all objects (separate pass for optimization)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawDebugOverlays(Vector2 shiftCenter, int TickCount)
        {
            if (!bShowDebugMode)
                return;

            // Draw portal debug info
            for (int i = 0; i < _portalsArray.Length; i++)
            {
                PortalItem portalItem = _portalsArray[i];
                if (!portalItem.IsVisible)
                    continue;

                PortalInstance instance = portalItem.PortalInstance;
                Rectangle rect = new Rectangle(
                    instance.X - (int)shiftCenter.X - (instance.Width - 20),
                    instance.Y - (int)shiftCenter.Y - instance.Height,
                    instance.Width + 40,
                    instance.Height);

                DrawBorder(spriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f));

                if (portalItem.CanUpdateDebugText(TickCount, 1000))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(rect.X).Append('\n');
                    _debugStringBuilder.Append(" y: ").Append(rect.Y).Append('\n');
                    _debugStringBuilder.Append(" script: ").Append(instance.script).Append('\n');
                    _debugStringBuilder.Append(" tm: ").Append(instance.tm).Append('\n');
                    _debugStringBuilder.Append(" pt: ").Append(instance.pt).Append('\n');
                    _debugStringBuilder.Append(" pn: ").Append(instance.pt);
                    portalItem.DebugText = _debugStringBuilder.ToString();
                }
                spriteBatch.DrawString(font_DebugValues, portalItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }

            // Draw reactor debug info
            for (int i = 0; i < _reactorsArray.Length; i++)
            {
                ReactorItem reactorItem = _reactorsArray[i];
                if (!reactorItem.IsVisible)
                    continue;

                ReactorInstance instance = reactorItem.ReactorInstance;
                Rectangle rect = new Rectangle(
                    instance.X - (int)shiftCenter.X - (instance.Width - 20),
                    instance.Y - (int)shiftCenter.Y - instance.Height,
                    Math.Max(80, instance.Width + 40),
                    Math.Max(120, instance.Height));

                DrawBorder(spriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f));

                if (reactorItem.CanUpdateDebugText(TickCount, 1000))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(rect.X).Append('\n');
                    _debugStringBuilder.Append(" y: ").Append(rect.Y).Append('\n');
                    _debugStringBuilder.Append(" id: ").Append(instance.ReactorInfo.ID).Append('\n');
                    _debugStringBuilder.Append(" name: ").Append(instance.Name);
                    reactorItem.DebugText = _debugStringBuilder.ToString();
                }
                spriteBatch.DrawString(font_DebugValues, reactorItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }

            // Draw mob debug info
            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mobItem = _mobsArray[i];
                MobInstance instance = mobItem.MobInstance;
                int mobX = mobItem.CurrentX;
                int mobY = mobItem.CurrentY;

                Rectangle rect = new Rectangle(
                    mobX - (int)shiftCenter.X - (instance.Width - 20),
                    mobY - (int)shiftCenter.Y - instance.Height,
                    Math.Max(100, instance.Width + 40),
                    Math.Max(140, instance.Height));

                DrawBorder(spriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f));

                if (mobItem.CanUpdateDebugText(TickCount, 500))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(mobX).Append(", y: ").Append(mobY).Append('\n');
                    _debugStringBuilder.Append(" id: ").Append(instance.MobInfo.ID).Append('\n');
                    if (mobItem.MovementInfo != null)
                    {
                        _debugStringBuilder.Append(" type: ").Append(mobItem.MovementInfo.MoveType).Append('\n');
                        _debugStringBuilder.Append(" action: ").Append(mobItem.CurrentAction).Append('\n');
                        _debugStringBuilder.Append(" dir: ").Append(mobItem.MovementInfo.MoveDirection).Append('\n');
                    }
                    mobItem.DebugText = _debugStringBuilder.ToString();
                }
                spriteBatch.DrawString(font_DebugValues, mobItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }

            // Draw NPC debug info
            for (int i = 0; i < _npcsArray.Length; i++)
            {
                NpcItem npcItem = _npcsArray[i];
                NpcInstance instance = npcItem.NpcInstance;
                Rectangle rect = new Rectangle(
                    instance.X - (int)shiftCenter.X - (instance.Width - 20),
                    instance.Y - (int)shiftCenter.Y - instance.Height,
                    Math.Max(100, instance.Width + 40),
                    Math.Max(120, instance.Height));

                DrawBorder(spriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f));

                if (npcItem.CanUpdateDebugText(TickCount, 1000))
                {
                    _debugStringBuilder.Clear();
                    _debugStringBuilder.Append(" x: ").Append(rect.X).Append('\n');
                    _debugStringBuilder.Append(" y: ").Append(rect.Y).Append('\n');
                    _debugStringBuilder.Append(" id: ").Append(instance.NpcInfo.ID);
                    npcItem.DebugText = _debugStringBuilder.ToString();
                }
                spriteBatch.DrawString(font_DebugValues, npcItem.DebugText, new Vector2(rect.X, rect.Y), Color.White);
            }
        }

        /// <summary>
        /// Draw UI
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="mouseState"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawUI(GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
            int mapCenterX, int mapCenterY, Microsoft.Xna.Framework.Input.MouseState mouseState, int TickCount)
        {
            // Status bar [layer below minimap]
            if (statusBarUi != null)
            {
                statusBarUi.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                            mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                            null,
                            _renderParams,
                            TickCount);

                statusBarUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);

                statusBarChatUI.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                            mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                            null,
                            _renderParams,
                            TickCount);
                statusBarChatUI.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
            }

            // Minimap
            if (miniMapUi != null)
            {
                miniMapUi.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                        null,
                        _renderParams,
                TickCount);

                miniMapUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
            }
        }

        /// <summary>
        /// Draw Tooltip
        /// </summary>
        /// <param name="gameTime"></param>
        /// <param name="shiftCenter"></param>
        /// <param name="renderParams"></param>
        /// <param name="mapCenterX"></param>
        /// <param name="mapCenterY"></param>
        /// <param name="mouseState"></param>
        /// <param name="TickCount"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawTooltip(GameTime gameTime, Vector2 shiftCenter, RenderParameters renderParams,
            int mapCenterX, int mapCenterY, Microsoft.Xna.Framework.Input.MouseState mouseState, int TickCount)
        {
            if (_tooltipsArray.Length > 0)
            {
                for (int i = 0; i < _tooltipsArray.Length; i++)
                {
                    TooltipItem tooltip = _tooltipsArray[i];
                    if (tooltip.TooltipInstance.CharacterToolTip != null)
                    {
                        Rectangle tooltipRect = tooltip.TooltipInstance.CharacterToolTip.Rectangle;
                        if (tooltipRect != null) // if this is null, show it at all times
                        {
                            Rectangle rect = new Rectangle(
                                tooltipRect.X - (int)shiftCenter.X,
                                tooltipRect.Y - (int)shiftCenter.Y,
                                tooltipRect.Width, tooltipRect.Height);

                            if (bShowDebugMode)
                            {
                                DrawBorder(spriteBatch, rect, 1, Color.White, new Color(Color.Gray, 0.3f)); // test

                                if (tooltip.CanUpdateDebugText(TickCount, 1000))
                                {
                                    string text = "X: " + rect.X + ", Y: " + rect.Y;

                                    tooltip.DebugText = text;
                                }
                                spriteBatch.DrawString(font_DebugValues, tooltip.DebugText, new Vector2(rect.X, rect.Y), Color.White);
                            }
                            if (!rect.Contains(mouseState.X, mouseState.Y))
                                continue;
                        }
                    }
                    tooltip.Draw(spriteBatch, skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, mapBoard.CenterPoint.X, mapBoard.CenterPoint.Y,
                        null,
                        _renderParams,
                        TickCount);
                }
            }
        }

        /// <summary>
        /// Draws the VR border
        /// </summary>
        /// <param name="sprite"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawVRFieldBorder(SpriteBatch sprite)
        {
            if (bBigBang2Update || !bDrawVRBorderLeftRight || (vr_fieldBoundary.X == 0 && vr_fieldBoundary.Y == 0))
                return;

            Color borderColor = Color.Black;

            // Draw top line
       /*     sprite.Draw(texture_vrBoundaryRectTop, 
                new Rectangle(
                    vr_fieldBoundary.Left - (VR_BORDER_WIDTHHEIGHT + mapShiftX), 
                    vr_fieldBoundary.Top - (VR_BORDER_WIDTHHEIGHT + mapShiftY), 
                    vr_fieldBoundary.Width * 2,
                    VR_BORDER_WIDTHHEIGHT), 
                borderColor);

            // Draw bottom line
            sprite.Draw(texture_vrBoundaryRectBottom,
                new Rectangle(
                    vr_fieldBoundary.Left - (VR_BORDER_WIDTHHEIGHT + mapShiftX),
                    vr_fieldBoundary.Bottom - (mapShiftY),
                    vr_fieldBoundary.Width * 2,
                    VR_BORDER_WIDTHHEIGHT),
                borderColor);*/

            // Draw left line
            sprite.Draw(texture_vrBoundaryRectLeft, 
                new Rectangle(
                    vr_fieldBoundary.Left - (VR_BORDER_WIDTHHEIGHT + mapShiftX), 
                    vr_fieldBoundary.Top - (mapShiftY),
                    VR_BORDER_WIDTHHEIGHT, 
                    vr_fieldBoundary.Height), 
                borderColor);

            // Draw right line
            sprite.Draw(texture_vrBoundaryRectRight, 
                new Rectangle(
                    vr_fieldBoundary.Right - mapShiftX, 
                    vr_fieldBoundary.Top - (mapShiftY),
                    VR_BORDER_WIDTHHEIGHT, 
                    vr_fieldBoundary.Height), 
                borderColor);
        }

        /// <summary>
        /// Draws the LB border
        /// This code is part of the map boundary rendering system that creates a black border near the map edges (LBTop, LBSide, LBBottom) when viewing maps that are not created for > 1366x768 resolution
        /// </summary>
        /// <param name="sprite"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawLBFieldBorder(SpriteBatch sprite)
        {
            if (!bBigBang2Update // not used before the 1024x768 screen update
                || (vr_fieldBoundary.X == 0 && vr_fieldBoundary.Y == 0))
                return;

            Color borderColor = Color.Black;

            // Draw left line
            // Starting Point: The texture(a 2D image or graphic applied to a surface) begins at the left edge of the screen and extends leftward beyond the screen's left limit.
            // LBSide: This variable represents the width(in pixels) of the text
            if (texture_lbLeft != null)
            {
                // Define rectangle for left border:
                // - X: Position at left boundary (-LB_BORDER_WIDTHHEIGHT) and adjust for map shifting
                // - Y: Position at the very top (0) and adjust for map shifting
                // - Width: Use configured side border width (LBSide) plus border width
                // - Height: Use full texture height from border asset

                int distanceToVRLeft = vr_fieldBoundary.Left - mapShiftX; // distance to the left VR border
                int adjustedWidth = Math.Min(texture_lbLeft.Width, distanceToVRLeft + LB_BORDER_WIDTHHEIGHT); // ensure the width is at least LBSide
                //Debug.WriteLine("Distance to VRLeft: " + distanceToVRLeft + ", Draw width: " + adjustedWidth);

                // Ensures the border width doesn't exceed far into the map VRBoundary
                if (adjustedWidth > LB_BORDER_WIDTHHEIGHT)
                {
                    sprite.Draw(texture_lbLeft,
                        new Rectangle(
                            x: (0 - LB_BORDER_WIDTHHEIGHT), // Position fully offscreen to the left
                            y: -0, // Align to top of viewport
                            width: adjustedWidth,
                            height: texture_lbLeft.Height
                        ),
                        borderColor);
                }
            }

            // Draw right line
            // Starting Point: The texture(a 2D image or graphic applied to a surface) begins at the right edge of the screen and extends rightward beyond the screen's right limit.
            // LBSide: This variable represents the width(in pixels) of the tex
            if (texture_lbRight != null)
            {
                // Define rectangle for right border:
                // - X: Position at right boundary minus border width and adjust for map shifting
                // - Y: Position at the very top (0) and adjust for map shifting  
                // - Width: Use configured side border width (LBSide) plus border width
                // - Height: Use full texture height from border asset

                int distanceToVRRight = this.Width - vr_fieldBoundary.Right - mapShiftX; // distance to the left VR border
                int adjustedWidth = Math.Min(texture_lbRight.Width, distanceToVRRight + LB_BORDER_WIDTHHEIGHT); // ensure the width is at least LBSide
                //Debug.WriteLine("Distance to VRRight: " + distanceToVRRight + ", Draw width: " + adjustedWidth);

                // Ensures the border width doesn't exceed far into the map VRBoundary
                if (adjustedWidth > LB_BORDER_WIDTHHEIGHT)
                {
                    sprite.Draw(texture_lbRight,
                        new Rectangle(
                            x: (this.Width - LBSide), // Position fully offscreen to the left
                            y: -0, // Align to top of viewport  
                            width: adjustedWidth,
                            height: texture_lbRight.Height
                        ),
                    borderColor);
                }
            }
  

            // Draw top line
            // Starting Point: The texture(a 2D image or graphic applied to a surface) begins at the top edge of the screen and extends upward beyond the screen's upper limit.
            // LBTop: This variable represents the height(in pixels) of the texture that is contained within the screen. In other words, LBTop defines how much of the texture's height fits inside the boundary, from the top of the screen to downward.
            if (texture_lbTop != null)
            {
                // Define rectangle for top border:
                // - X: Offset 150px (LB_BORDER_OFFSET_X) left of map edge to ensure coverage of small maps
                // - Y: Position at the very top (0) and adjust for map shifting
                // - Width: Use texture width from top border asset 
                // - Height: Use configured top border height (LBTop)

                int distanceToVRTop = vr_fieldBoundary.Top - mapShiftY; // distance to the top VR border
                int adjustedHeight = Math.Min(texture_lbTop.Height, distanceToVRTop + LB_BORDER_WIDTHHEIGHT); // ensure the width is at least LBSide
                //Debug.WriteLine("Distance to VRTop: " + distanceToVRTop + ", Draw height: " + adjustedHeight);

                // Ensures the border height doesn't exceed far into the map VRBoundary
                if (adjustedHeight > LB_BORDER_WIDTHHEIGHT)
                {
                    sprite.Draw(texture_lbTop,
                        new Rectangle(
                            x: -LB_BORDER_OFFSET_X, // Start 150px left of map edge
                            y: (0 - LB_BORDER_WIDTHHEIGHT), // Extend above viewport
                            width: texture_lbTop.Width,
                            height: adjustedHeight
                        ),
                    borderColor);
                }
            }

            // Draw bottom line
            // Starting Point: The texture(a 2D image or graphic applied to a surface) begins at the bottom edge of the screen and extends downward beyond the screen's lower limit.
            // LBBottom: This variable represents the height(in pixels) of the texture that is contained within the screen. In other words, LBBottom defines how much of the texture's height fits inside the boundary, from the bottom of the screen to upward.
            if (texture_lbBottom != null)
            {
                // Define rectangle for bottom border:
                // - X: Offset 150px (LB_BORDER_OFFSET_X) left of map edge to cover small maps fully
                // - Y: Position at bottom of screen, accounting for scaled height and UI elements
                // - Width: Use texture width from bottom border asset
                // - Height: Use configured bottom border height

                int distanceToVRBottom = this.Height - vr_fieldBoundary.Bottom - mapShiftY; // distance to the bottom VR border
                int adjustedHeight = Math.Min(texture_lbBottom.Height, distanceToVRBottom + LB_BORDER_WIDTHHEIGHT); // ensure the height is at least LBBottom
                //Debug.WriteLine("Distance to VRBottom: " + distanceToVRBottom + ", Draw height: " + adjustedHeight);

                // Ensures the border height doesn't exceed far into the map VRBoundary
                if (adjustedHeight > LB_BORDER_WIDTHHEIGHT)
                {
                    sprite.Draw(texture_lbBottom,
                        new Rectangle(
                            x: -LB_BORDER_OFFSET_X, // Start 150px left of map edge
                            y: (Height - LB_BORDER_UI_MENUHEIGHT - LBBottom), // Align to bottom minus UI height
                            width: texture_lbBottom.Width,
                            height: adjustedHeight
                        ),
                    borderColor);
                }
            }
        }

        /// <summary>
        /// Draws a border
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="rectangleToDraw"></param>
        /// <param name="thicknessOfBorder"></param>
        /// <param name="borderColor"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawBorder(SpriteBatch sprite, Rectangle rectangleToDraw, int thicknessOfBorder, Color borderColor, Color backgroundColor)
        {
            // Draw top line
            sprite.Draw(texture_debugBoundaryRect, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, rectangleToDraw.Width, thicknessOfBorder), borderColor);

            // Draw left line
            sprite.Draw(texture_debugBoundaryRect, new Rectangle(rectangleToDraw.X, rectangleToDraw.Y, thicknessOfBorder, rectangleToDraw.Height), borderColor);

            // Draw right line
            sprite.Draw(texture_debugBoundaryRect, new Rectangle((rectangleToDraw.X + rectangleToDraw.Width - thicknessOfBorder),
                                            rectangleToDraw.Y,
                                            thicknessOfBorder,
                                            rectangleToDraw.Height), borderColor);
            // Draw bottom line
            sprite.Draw(texture_debugBoundaryRect, new Rectangle(rectangleToDraw.X,
                                            rectangleToDraw.Y + rectangleToDraw.Height - thicknessOfBorder,
                                            rectangleToDraw.Width,
                                            thicknessOfBorder), borderColor);

            // Draw background
            if (backgroundColor != Color.Transparent)
                // draw a black background sprite with the rectangleToDraw as area
                sprite.Draw(texture_debugBoundaryRect, rectangleToDraw, backgroundColor);
        }

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawLine(SpriteBatch sprite, Vector2 start, Vector2 end, Color color)
        {
            int width = (int)Vector2.Distance(start, end);
            float rotation = (float)Math.Atan2((double)(end.Y - start.Y), (double)(end.X - start.X));
            sprite.Draw(texture_miniMapPixel, new Rectangle((int)start.X, (int)start.Y, width, UserSettings.LineWidth), null, color, rotation, new Vector2(0f, 0f), SpriteEffects.None, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillRectangle(SpriteBatch sprite, Rectangle rectangle, Color color)
        {
            sprite.Draw(texture_miniMapPixel, rectangle, color);
        }*/
        #endregion
        
        #region Screenshot
        /// <summary>
        /// Creates a snapshot of the current Graphics Device back buffer data 
        /// and save as JPG in the local folder
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoScreenshot()
        {
            if (!bSaveScreenshotComplete)
                return;

            if (bSaveScreenshot)
            {
                bSaveScreenshot = false;

                //Pull the picture from the buffer 
                int backBufferWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
                int backBufferHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
                int[] backBuffer = new int[backBufferWidth * backBufferHeight];
                GraphicsDevice.GetBackBufferData(backBuffer);

                //Copy to texture
                using (Texture2D texture = new Texture2D(GraphicsDevice, backBufferWidth, backBufferHeight, false, SurfaceFormat.Color  /*RGBA8888*/))
                {
                    texture.SetData(backBuffer);

                    //Get a date for file name
                    DateTime dateTimeNow = DateTime.Now;
                    string fileName = String.Format("Maple_{0}{1}{2}_{3}{4}{5}.png",
                            dateTimeNow.Day.ToString("D2"), dateTimeNow.Month.ToString("D2"), (dateTimeNow.Year - 2000).ToString("D2"),
                             dateTimeNow.Hour.ToString("D2"), dateTimeNow.Minute.ToString("D2"), dateTimeNow.Second.ToString("D2")
                            ); // same naming scheme as the official client.. except that they save in JPG

                    using (MemoryStream stream_png = new MemoryStream()) // memorystream for png
                    {
                        texture.SaveAsPng(stream_png, backBufferWidth, backBufferHeight); // save to png stream

                        System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(stream_png);
                        ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

                        // Create an EncoderParameters object.
                        // An EncoderParameters object has an array of EncoderParameter
                        // objects. 
                        EncoderParameters myEncoderParameters = new EncoderParameters(1);
                        myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L); // max quality

                        bitmap.Save(fileName, jpgEncoder, myEncoderParameters);

                        /*using (MemoryStream stream_jpeg = new MemoryStream()) // memorystream for jpeg
                        {
                            var imageStream_png = System.Drawing.Image.FromStream(stream_png, true); // png Image
                            imageStream_png.Save(stream_jpeg, ImageFormat.Jpeg); // save as jpeg  - TODO: Fix System.Runtime.InteropServices.ExternalException: 'A generic error occurred in GDI+.' sometimes.. no idea

                            byte[] jpegOutStream = stream_jpeg.ToArray();

                            // Save
                            using (FileStream fs = File.Open(fileName, FileMode.OpenOrCreate))
                            {
                                fs.Write(jpegOutStream, 0, jpegOutStream.Length);
                            }
                        }*/
                    }
                }
                bSaveScreenshotComplete = true;
            }
        }
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        #endregion

        #region Boundaries
        /// <summary>
        /// Controls horizontal camera movement within the map's viewing boundaries.
        /// Move the camera X viewing range by a specific offset, & centering if needed.
        /// </summary>
        /// <param name="bIsLeftKeyPressed"></param>
        /// <param name="bIsRightKeyPressed"></param>
        /// <param name="moveOffset"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCameraMoveX(bool bIsLeftKeyPressed, bool bIsRightKeyPressed, int moveOffset)
        {
            // Calculate total viewable width in pixels after scaling
            int leftRightVRDifference = (int)((vr_fieldBoundary.Right - vr_fieldBoundary.Left) * _renderParams.RenderObjectScaling);

            // If map is narrower than screen width, center it
            if (leftRightVRDifference < _renderParams.RenderWidth) // viewing range is smaller than the render width.. keep the rendering position at the center instead (starts from left to right)
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
                 *  87px________827px
                 */
                /* Center Camera Logic Example:
                        * For a vertical map like Orbis Tower:
                        * - VR Map boundaries: Left=87px, Right=827px (Width = 740px)
                        * - Screen width: 1024px
                        * - Center point = (827-87)/2 + 87 = 457px
                        * 
                        * Since map is narrower than screen:
                        * 1. Find map center relative to its boundaries
                        * 2. Offset by half screen width to center map
                        * 3. Account for any scaling
                        */
                this.mapShiftX = ((leftRightVRDifference / 2) + (int)(vr_fieldBoundary.Left * _renderParams.RenderObjectScaling)) - (_renderParams.RenderWidth / 2);
            }
            else  // If map is wider than screen width, allow scrolling with boundaries
            {
                // System.Diagnostics.Debug.WriteLine("[{4}] VR.Right {0}, Width {1}, Relative {2}. [Scaling {3}]", 
                //      vr.Right, RenderWidth, (int)(vr.Right - RenderWidth), (int)((vr.Right - (RenderWidth * RenderObjectScaling)) * RenderObjectScaling),
                //     mapShiftX + offset);

                if (bIsLeftKeyPressed)
                {
                    // Limit leftward movement to map's left boundary
                    this.mapShiftX = Math.Max((int)(vr_fieldBoundary.Left * _renderParams.RenderObjectScaling), mapShiftX - moveOffset);

                }
                else if (bIsRightKeyPressed)
                {
                    // Limit rightward movement to keep right boundary visible
                    // Accounts for screen width and scaling to prevent showing empty space
                    this.mapShiftX = Math.Min((int)((vr_fieldBoundary.Right - (_renderParams.RenderWidth / _renderParams.RenderObjectScaling))), mapShiftX + moveOffset);
                } 
            }
        }

        /// <summary>
        /// Move the camera Y viewing range by a specific offset, & centering if needed.
        /// </summary>
        /// <param name="bIsUpKeyPressed"></param>
        /// <param name="bIsDownKeyPressed"></param>
        /// <param name="moveOffset"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetCameraMoveY(bool bIsUpKeyPressed, bool bIsDownKeyPressed, int moveOffset)
        {
            int topDownVRDifference = (int)((vr_fieldBoundary.Bottom - vr_fieldBoundary.Top) * _renderParams.RenderObjectScaling);
            if (topDownVRDifference < _renderParams.RenderHeight)
            {
                this.mapShiftY = ((topDownVRDifference / 2) + (int)(vr_fieldBoundary.Top * _renderParams.RenderObjectScaling)) - (_renderParams.RenderHeight / 2);
            }
            else
            {
                /*System.Diagnostics.Debug.WriteLine("[{0}] VR.Bottom {1}, Height {2}, Relative {3}. [Scaling {4}]",
                    (int)((vr.Bottom - (RenderHeight))),
                    vr.Bottom, RenderHeight, (int)(vr.Bottom - RenderHeight),
                    mapShiftX + offset);*/


                if (bIsUpKeyPressed)
                {
                    this.mapShiftY = Math.Max((int)(vr_fieldBoundary.Top), mapShiftY - moveOffset);
                }
                else if (bIsDownKeyPressed)
                {
                    this.mapShiftY = Math.Min((int)((vr_fieldBoundary.Bottom - (_renderParams.RenderHeight / _renderParams.RenderObjectScaling))), mapShiftY + moveOffset);
                }
            }
        }
        #endregion

        #region Chat Commands
        /// <summary>
        /// Registers all chat commands
        /// </summary>
        private void RegisterChatCommands()
        {
            // /map <id> - Change to a different map
            _chat.CommandHandler.RegisterCommand(
                "map",
                "Teleport to a map by ID",
                "/map <mapId>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /map <mapId>");
                    }

                    if (!int.TryParse(args[0], out int mapId))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid map ID: {args[0]}");
                    }

                    if (_loadMapCallback == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Map loading not available");
                    }

                    // Trigger map change
                    _pendingMapChange = true;
                    _pendingMapId = mapId;
                    _pendingPortalName = null;

                    return ChatCommandHandler.CommandResult.Ok($"Loading map {mapId}...");
                });

            // /pos - Show current camera position
            _chat.CommandHandler.RegisterCommand(
                "pos",
                "Show current camera position",
                "/pos",
                args =>
                {
                    return ChatCommandHandler.CommandResult.Info($"Camera: X={mapShiftX}, Y={mapShiftY}");
                });

            // /goto <x> <y> - Move camera to position
            _chat.CommandHandler.RegisterCommand(
                "goto",
                "Move camera to X,Y position",
                "/goto <x> <y>",
                args =>
                {
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /goto <x> <y>");
                    }

                    if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                    {
                        return ChatCommandHandler.CommandResult.Error("Invalid coordinates");
                    }

                    mapShiftX = x;
                    mapShiftY = y;
                    return ChatCommandHandler.CommandResult.Ok($"Moved to ({x}, {y})");
                });

            // /mob - Toggle mob movement
            _chat.CommandHandler.RegisterCommand(
                "mob",
                "Toggle mob movement on/off",
                "/mob",
                args =>
                {
                    bMobMovementEnabled = !bMobMovementEnabled;
                    return ChatCommandHandler.CommandResult.Ok($"Mob movement: {(bMobMovementEnabled ? "ON" : "OFF")}");
                });

            // /debug - Toggle debug mode
            _chat.CommandHandler.RegisterCommand(
                "debug",
                "Toggle debug overlay",
                "/debug",
                args =>
                {
                    bShowDebugMode = !bShowDebugMode;
                    return ChatCommandHandler.CommandResult.Ok($"Debug mode: {(bShowDebugMode ? "ON" : "OFF")}");
                });

            // /hideui - Toggle UI visibility
            _chat.CommandHandler.RegisterCommand(
                "hideui",
                "Toggle UI visibility",
                "/hideui",
                args =>
                {
                    bHideUIMode = !bHideUIMode;
                    return ChatCommandHandler.CommandResult.Ok($"UI hidden: {(bHideUIMode ? "YES" : "NO")}");
                });

            // /clear - Clear chat messages
            _chat.CommandHandler.RegisterCommand(
                "clear",
                "Clear chat messages",
                "/clear",
                args =>
                {
                    _chat.ClearMessages();
                    return ChatCommandHandler.CommandResult.Ok("Chat cleared");
                });
        }
        #endregion

        #region Spine specific
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
        #endregion
    }
}
