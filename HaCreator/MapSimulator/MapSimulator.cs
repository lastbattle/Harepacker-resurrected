using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Misc;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Loaders;
using HaSharedLibrary.Wz;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Animation;
using MobItem = HaCreator.MapSimulator.Entities.MobItem;
using HaRepacker.Utils;
using HaSharedLibrary;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Spine;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Core;

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
        private int _mapCenterX = 0;
        private int _mapCenterY = 0;

        private int Width;
        private int Height;
        private float UserScreenScaleFactor = 1.0f;

        private RenderParameters _renderParams;
        private Matrix _matrixScale;

        private GraphicsDeviceManager _DxDeviceManager;
        private readonly TexturePool _texturePool = new TexturePool();
        private readonly ScreenshotManager _screenshotManager = new ScreenshotManager();

        private SpriteBatch _spriteBatch;


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

        // Mob pool for spawn/despawn management
        private MobPool _mobPool;
        public MobPool MobPool => _mobPool;

        // Drop pool for item/meso drops
        private DropPool _dropPool;
        public DropPool DropPool => _dropPool;

        // Portal pool for hidden portal support and portal properties
        private PortalPool _portalPool;
        public PortalPool PortalPool => _portalPool;

        // Reactor pool for reactor spawning and touch detection
        private ReactorPool _reactorPool;
        public ReactorPool ReactorPool => _reactorPool;

        // Meso animation frames (indexed by denomination: 0=small, 1=medium, 2=large, 3=bag)
        // Each list contains animation frames for that denomination
        private List<IDXObject>[] _mesoAnimFrames;

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
        private Rectangle _vrFieldBoundary;
        private Rectangle _vrRectangle; // the rectangle of the VR field, used for drawing the VR border
        private const int VR_BORDER_WIDTHHEIGHT = 600; // the height or width of the VR border
        private bool _drawVRBorderLeftRight = false;
        private Texture2D _vrBoundaryTextureLeft, _vrBoundaryTextureRight, _vrBoundaryTextureTop, _vrBoundaryTextureBottom;

        private int _lbSide = 0, _lbTop = 0, _lbBottom = 0;
        private Texture2D _lbTextureLeft, _lbTextureRight, _lbTextureTop, _lbTextureBottom; // Left, Right, Top, Bottom LB borders
        private const int LB_BORDER_WIDTHHEIGHT = 300; // additional width or height of LB Border outside VR
        private const int LB_BORDER_UI_MENUHEIGHT = 62; // The hardcoded values of the height of the UI menu bar at the bottom of the screen (Name, Level, HP, MP, etc.)
        private const int LB_BORDER_OFFSET_X = 150; // Offset from map edge

        // Mirror bottom boundaries (Reflections in Arcane river maps)
        private Rectangle _mirrorBottomRect;
        private ReflectionDrawableBoundary _mirrorBottomReflection;

        // Consolidated UI management
        private readonly UIManager _uiManager = new UIManager();

        // Compatibility accessors for UI elements (during transition)
        private MinimapUI miniMapUi { get => _uiManager.Minimap; set => _uiManager.Minimap = value; }
        private StatusBarUI statusBarUi { get => _uiManager.StatusBar; set => _uiManager.StatusBar = value; }
        private StatusBarChatUI statusBarChatUI { get => _uiManager.StatusBarChat; set => _uiManager.StatusBarChat = value; }
        private UIWindowManager uiWindowManager { get => _uiManager.WindowManager; set => _uiManager.WindowManager = value; }
        private MouseCursorItem mouseCursor { get => _uiManager.MouseCursor; set => _uiManager.MouseCursor = value; }

        // Audio
        private WzSoundResourceStreamer _audio;
        private SoundManager _soundManager; // Manages sound effects with concurrent playback support
        private string _currentBgmName = null; // Track current BGM to avoid reloading same BGM on map change

        // Etc
        private Board _mapBoard; // Not readonly - can be replaced during seamless map transitions
        // Map type flags moved to _gameState (IsLoginMap, IsCashShopMap, IsBigBangUpdate, IsBigBang2Update) 

        // Spine
        private SkeletonMeshRenderer _skeletonMeshRenderer;

        // Text
        private SpriteFont _fontNavigationKeysHelper;
        private SpriteFont _fontDebugValues;
        private SpriteFont _fontChat;

        // Chat system
        private readonly MapSimulatorChat _chat = new MapSimulatorChat();

        // Pickup notice UI (displays meso/item pickup messages at bottom right)
        private readonly PickupNoticeUI _pickupNoticeUI = new PickupNoticeUI();

        // Consolidated effect management
        private readonly EffectManager _effectManager = new EffectManager();

        // Direct accessors for effect subsystems (for compatibility during transition)
        private ScreenEffects _screenEffects => _effectManager.Screen;
        private AnimationEffects _animationEffects => _effectManager.Animation;
        private CombatEffects _combatEffects => _effectManager.Combat;
        private ParticleSystem _particleSystem => _effectManager.Particles;
        private FieldEffects _fieldEffects => _effectManager.Field;
        private readonly DynamicFootholdSystem _dynamicFootholds = new DynamicFootholdSystem();
        private readonly TransportationField _transportField = new TransportationField();
        private readonly LimitedViewField _limitedViewField = new LimitedViewField();

        // Camera controller for smooth scrolling and zoom
        private readonly CameraController _cameraController = new CameraController();

        // Centralized game state management
        private readonly GameStateManager _gameState = new GameStateManager();

        // Map state cache for maintaining entity positions across map transitions
        private readonly MapStateCache _mapStateCache = new MapStateCache();

        // Input management
        private readonly InputManager _inputManager = new InputManager();

        // Rendering management - consolidates all draw operations
        private RenderingManager _renderingManager;

        // Player character system
        private PlayerManager _playerManager;
        public PlayerManager PlayerManager => _playerManager;

        // Tombstone animation (Effect.wz/Tomb.img)
        private List<IDXObject> _tombFallFrames; // fall/0..19 animation
        private IDXObject _tombLandFrame; // land/0 (final resting frame)
        private int _tombAnimationStartTime; // When the death occurred
        private bool _tombAnimationComplete; // Whether fall animation has finished

        // Tombstone falling physics
        private float _tombCurrentY; // Current Y position during fall
        private float _tombVelocityY; // Current fall velocity
        private float _tombTargetY; // Ground Y position (death position)
        private bool _tombHasLanded; // Whether tombstone has hit ground
        private const float TOMB_GRAVITY = 1200f; // Gravity acceleration (px/s²)
        private const float TOMB_START_HEIGHT = 300f; // Height above death position to start falling

        // Debug
        private Texture2D _debugBoundaryTexture;

        // Frame counter for visibility culling (increments each frame)
        private int _frameNumber = 0;

        // Portal teleportation - seamless map transitions
        private string _spawnPortalName = null; // The portal name to spawn at (set when loading map)
        private int _lastClickTime = 0; // For double-click detection
        private const int DOUBLE_CLICK_TIME_MS = 500; // Time window for double-click
        private PortalItem _lastClickedPortal = null; // Track which visible portal was clicked
        private PortalInstance _lastClickedHiddenPortal = null; // Track which hidden portal was clicked

        // Portal fade effect constants (from IDA Pro analysis of CStage::FadeIn/FadeOut)
        // CStage::FadeIn uses 600ms normally, 300ms for fast travel mode
        private const int PORTAL_FADE_DURATION_MS = 600;       // Normal fade duration
        private const int PORTAL_FADE_DURATION_FAST_MS = 300;  // Fast travel fade duration

        // Portal fade state tracking
        private enum PortalFadeState
        {
            None,           // No fade in progress
            FadingOut,      // Fading to black before map change
            FadingIn        // Fading from black after map change
        }
        private PortalFadeState _portalFadeState = PortalFadeState.None;

        // Same-map portal teleport delay (no fade, just delay before teleport)
        // Default delay is 1000ms (1 second) if portal doesn't specify its own delay
        private const int SAME_MAP_PORTAL_DEFAULT_DELAY_MS = 1000;
        private bool _sameMapTeleportPending = false;
        private int _sameMapTeleportStartTime = 0;
        private int _sameMapTeleportDelay = 0;
        private PortalInstance _sameMapTeleportTarget = null;

        // Seamless map transition support (state managed by _gameState)
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
        /// <param name="_mapBoard"></param>
        /// <param name="titleName"></param>
        /// <param name="spawnPortalName">Optional portal name to spawn at (from portal teleportation)</param>
        public MapSimulator(Board _mapBoard, string titleName, string spawnPortalName = null)
        {
            this._mapBoard = _mapBoard;
            this._spawnPortalName = spawnPortalName;

            // Check if the simulated map is the Login map. 'MapLogin1:MapLogin1'
            string[] titleNameParts = titleName.Split(':');
            _gameState.IsLoginMap = titleNameParts.All(part => part.Contains("MapLogin"));
            _gameState.IsCashShopMap = titleNameParts.All(part => part.Contains("CashShopPreview"));

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

            // Initialize rendering manager
            _renderingManager = new RenderingManager(
                () => _mapBoard,
                () => Width,
                () => Height);
            _renderingManager.Initialize(_effectManager, _gameState, _dynamicFootholds, _transportField, _limitedViewField);
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

            this._matrixScale = Matrix.CreateScale(RenderObjectScaling);

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
            _fontNavigationKeysHelper = Content.Load<SpriteFont>("XnaDefaultFont");
            _fontChat = Content.Load<SpriteFont>("XnaFont_Chat");//("XnaFont_Debug");
            _fontDebugValues = Content.Load<SpriteFont>("XnaDefaultFont");//("XnaFont_Debug");

            // Set fonts on rendering manager
            _renderingManager.SetFonts(_fontDebugValues, _fontChat, _fontNavigationKeysHelper);

            // Pre-cache navigation help text strings to avoid string.Format allocations in Draw()
            _navHelpTextMobOn = "[Arrows/WASD] Move | [Alt] Jump | [Ctrl] Attack | [Tab] Toggle Camera | [R] Respawn\n[F5] Debug | [F6] Mob movement (ON) | [F7] Shake | [F8] Knockback\n[F9] Motion Blur | [F10] Explosion | [F11] Lightning | [F12] Sparks\n[1] Rain | [2] Snow | [3] Leaves | [4] Fear | [5] Weather Msg | [0] Clear\n[6] H-Platform | [7] V-Platform | [8] Timed | [9] Waypoint | [Space] Sparkle\n[-] Ship Voyage | [=] Balrog/Skip/Reset | [ScrollWheel] Zoom | [Home] Reset Zoom | [C] Smooth Cam";
            _navHelpTextMobOff = "[Arrows/WASD] Move | [Alt] Jump | [Ctrl] Attack | [Tab] Toggle Camera | [R] Respawn\n[F5] Debug | [F6] Mob movement (OFF) | [F7] Shake | [F8] Knockback\n[F9] Motion Blur | [F10] Explosion | [F11] Lightning | [F12] Sparks\n[1] Rain | [2] Snow | [3] Leaves | [4] Fear | [5] Weather Msg | [0] Clear\n[6] H-Platform | [7] V-Platform | [8] Timed | [9] Waypoint | [Space] Sparkle\n[-] Ship Voyage | [=] Balrog/Skip/Reset | [ScrollWheel] Zoom | [Home] Reset Zoom | [C] Smooth Cam";

            base.Initialize();
        }

        /// <summary>
        /// Load game assets
        /// </summary>
        protected override void LoadContent()
        {
            // Load physics constants from Map.wz/Physics.img
            LoadPhysicsConstants();

            WzImage mapHelperImage = Program.FindImage("Map", "MapHelper.img");
            WzImage soundUIImage = Program.FindImage("Sound", "UI.img");
            WzImage uiToolTipImage = Program.FindImage("UI", "UIToolTip.img"); // UI_003.wz
            WzImage uiBasicImage = Program.FindImage("UI", "Basic.img");
            WzImage uiWindow1Image = Program.FindImage("UI", "UIWindow.img"); //
            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img"); // doesnt exist before big-bang

            WzImage uiStatusBarImage = Program.FindImage("UI", "StatusBar.img");
            WzImage uiStatus2BarImage = Program.FindImage("UI", "StatusBar2.img");

            _gameState.IsBigBangUpdate = uiWindow2Image?["BigBang!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"] != null; // different rendering for pre and post-bb, to support multiple vers
            _gameState.IsBigBang2Update = uiWindow2Image?["BigBang2!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"] != null; // chaos update

            // BGM
            if (Program.InfoManager.BGMs.ContainsKey(_mapBoard.MapInfo.bgm))
            {
                _currentBgmName = _mapBoard.MapInfo.bgm;
                _audio = new WzSoundResourceStreamer(Program.InfoManager.BGMs[_mapBoard.MapInfo.bgm], true);
                if (_audio != null)
                {
                    _audio.Play();
                }
            }

            // Sound effects from Sound.wz/Game.img - using SoundManager for concurrent playback
            _soundManager = new SoundManager();
            WzImage soundGameImage = Program.FindImage("Sound", "Game.img");
            if (soundGameImage != null)
            {
                // Portal teleport sound
                WzBinaryProperty portalSound = (WzBinaryProperty)soundGameImage["Portal"];
                if (portalSound != null)
                {
                    _soundManager.RegisterSound("Portal", portalSound);
                }

                // Jump sound
                WzBinaryProperty jumpSound = (WzBinaryProperty)soundGameImage["Jump"];
                if (jumpSound != null)
                {
                    _soundManager.RegisterSound("Jump", jumpSound);
                }

                // Drop item sound (played on mob death)
                WzBinaryProperty dropItemSound = (WzBinaryProperty)soundGameImage["DropItem"];
                if (dropItemSound != null)
                {
                    _soundManager.RegisterSound("DropItem", dropItemSound);
                }

                // Pick up item sound
                WzBinaryProperty pickUpItemSound = (WzBinaryProperty)soundGameImage["PickUpItem"];
                if (pickUpItemSound != null)
                {
                    _soundManager.RegisterSound("PickUpItem", pickUpItemSound);
                }
            }

            // Load meso icons from Item.wz/Special/0900.img
            LoadMesoIcons();

            // Load tombstone animation from Effect.wz/Tomb.img
            LoadTombstoneAnimation();

            if (_mapBoard.VRRectangle == null)
            {
                _vrFieldBoundary = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
                _vrRectangle = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
            }
            else
            {
                _vrFieldBoundary = new Rectangle(
                    _mapBoard.VRRectangle.X + _mapBoard.CenterPoint.X, 
                    _mapBoard.VRRectangle.Y + _mapBoard.CenterPoint.Y, 
                    _mapBoard.VRRectangle.Width, 
                    _mapBoard.VRRectangle.Height);
                _vrRectangle = new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y, _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height);
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
                foreach (LayeredItem tileObj in _mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;

                    mapObjects[tileObj.LayerNumber].Add(
                        MapSimulatorLoader.CreateMapItemFromProperty(_texturePool, tileParent, tileObj.X, tileObj.Y, _mapBoard.CenterPoint, _DxDeviceManager.GraphicsDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));
                }
            });

            // Background
            Task t_Background = Task.Run(() =>
            {
                foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);

                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in _mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);

                    if (bgItem != null)
                        backgrounds_front.Add(bgItem);
                }
            });

            // Reactors
            Task t_reactor = Task.Run(() =>
            {
                foreach (ReactorInstance reactor in _mapBoard.BoardItems.Reactors)
                {
                    //WzImage imageProperty = (WzImage)NPCWZFile[reactorInfo.ID + ".img"];

                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(_texturePool, reactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
            });

            // NPCs
            Task t_npc = Task.Run(() =>
            {
                foreach (NpcInstance npc in _mapBoard.BoardItems.NPCs)
                {
                    //WzImage imageProperty = (WzImage) NPCWZFile[npcInfo.ID + ".img"];
                    if (npc.Hide)
                        continue;

                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(_texturePool, npc, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (npcItem != null)
                        mapObjects_NPCs.Add(npcItem);
                }
            });

            // Mobs
            Task t_mobs = Task.Run(() =>
            {
                foreach (MobInstance mob in _mapBoard.BoardItems.Mobs)
                {
                    //WzImage imageProperty = Program.WzManager.FindMobImage(mobInfo.ID); // Mob.wz Mob2.img Mob001.wz
                    if (mob.Hide)
                        continue;

                    MobItem npcItem = MapSimulatorLoader.CreateMobFromProperty(_texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);

                    mapObjects_Mobs.Add(npcItem);
                }
            });

            // Portals
            Task t_portal = Task.Run(() =>
            {
                WzSubProperty portalParent = (WzSubProperty) mapHelperImage["portal"];

                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
                //WzSubProperty editorParent = (WzSubProperty) portalParent["editor"];

                foreach (PortalInstance portal in _mapBoard.BoardItems.Portals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(_texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (portalItem != null)
                        mapObjects_Portal.Add(portalItem);
                }
            });

            // Tooltips
            Task t_tooltips = Task.Run(() =>
            {
                WzSubProperty farmFrameParent = (WzSubProperty) uiToolTipImage?["Item"]?["FarmFrame"]; // not exist before V update.
                foreach (ToolTipInstance tooltip in _mapBoard.BoardItems.ToolTips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(_texturePool, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);

                    mapObjects_tooltips.Add(item);
                }
            });

            // Cursor
            Task t_cursor = Task.Run(() =>
            {
                WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(_texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps, false);
            });

            // Spine object
            Task t_spine = Task.Run(() =>
            {
                _skeletonMeshRenderer = new SkeletonMeshRenderer(GraphicsDevice)
                {
                    PremultipliedAlpha = false,
                };
                _skeletonMeshRenderer.Effect.World = this._matrixScale;
            });

            // Minimap
            Task t_minimap = Task.Run(() =>
            {
                if (!_gameState.IsLoginMap && !_mapBoard.MapInfo.hideMinimap && !_gameState.IsCashShopMap)
                {
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiBasicImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _mapBoard.MapInfo.strMapName, _mapBoard.MapInfo.strStreetName, soundUIImage, _gameState.IsBigBangUpdate);
                }
            });

            // Statusbar
            Task t_statusBar = Task.Run(() => {
                if (!_gameState.IsLoginMap && !_gameState.IsCashShopMap) {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate);
                    if (statusBar != null) {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
            });

            // UI Windows (Inventory, Equipment, Skills, Quest)
            Task t_uiWindows = Task.Run(() => {
                if (!_gameState.IsLoginMap && !_gameState.IsCashShopMap) {
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate);
                }
            });

            while (!t_tiles.IsCompleted || !t_Background.IsCompleted || !t_reactor.IsCompleted || !t_npc.IsCompleted || !t_mobs.IsCompleted || !t_portal.IsCompleted ||
                !t_tooltips.IsCompleted || !t_cursor.IsCompleted || !t_spine.IsCompleted || !t_minimap.IsCompleted || !t_statusBar.IsCompleted || !t_uiWindows.IsCompleted)
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
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            ///////////////////////////////////////////////
            ////// Default positioning for character //////
            ///////////////////////////////////////////////
            bool spawnPositionSet = false;
            float spawnX = 0, spawnY = 0;

            // First, check if we're spawning from a portal teleport (has target portal name)
            if (!string.IsNullOrEmpty(_spawnPortalName))
            {
                // Find the portal with the matching name
                var targetPortal = _mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _spawnPortalName);
                if (targetPortal != null)
                {
                    this.mapShiftX = targetPortal.X;
                    this.mapShiftY = targetPortal.Y;
                    spawnX = targetPortal.X;
                    spawnY = targetPortal.Y;
                    spawnPositionSet = true;
                }
            }

            // Fallback: Get a random portal if any exists for spawnpoint
            if (!spawnPositionSet)
            {
                var startPortals = _mapBoard.BoardItems.Portals.Where(portal => portal.pt == PortalType.StartPoint).ToList();
                if (startPortals.Any())
                {
                    Random random = new Random();
                    PortalInstance randomStartPortal = startPortals[random.Next(startPortals.Count)];
                    this.mapShiftX = randomStartPortal.X;
                    this.mapShiftY = randomStartPortal.Y;
                    spawnX = randomStartPortal.X;
                    spawnY = randomStartPortal.Y;
                    spawnPositionSet = true;
                }
            }

            // Fallback to map center if no portals
            if (!spawnPositionSet)
            {
                spawnX = _vrFieldBoundary.Center.X;
                spawnY = _vrFieldBoundary.Center.Y;
            }

            SetCameraMoveX(true, false, 0); // true true to center it, in case its out of the boundary
            SetCameraMoveX(false, true, 0);

            SetCameraMoveY(true, false, 0);
            SetCameraMoveY(false, true, 0);

            ///////////////////////////////////////////////
            ///////// Initialize Player Manager ///////////
            ///////////////////////////////////////////////
            // Store map center point for camera calculations
            _mapCenterX = _mapBoard.CenterPoint.X;
            _mapCenterY = _mapBoard.CenterPoint.Y;
            // Spawn at portal spawn point (spawnX, spawnY set above from StartPoint portal)
            InitializePlayerManager(spawnX, spawnY);

            // Initialize camera controller
            _cameraController.Initialize(
                _vrFieldBoundary,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                _mapCenterX,
                _mapCenterY,
                _renderParams.RenderObjectScaling);
            _cameraController.SetPosition(spawnX, spawnY);
            ///////////////////////////////////////////////

            ///////////////////////////////////////////////
            ///////////// Border //////////////////////////
            ///////////////////////////////////////////////
            int leftRightVRDifference = (int)((_vrFieldBoundary.Right - _vrFieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth) // viewing range is smaller than the render width.. 
            {
                this._drawVRBorderLeftRight = true; // flag

                this._vrBoundaryTextureLeft = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureRight = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureTop = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureBottom = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
            }
            // LB Border
            if (_mapBoard.MapInfo.LBSide != null)
            {
                _lbSide = (int)_mapBoard.MapInfo.LBSide;
                this._lbTextureLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
                this._lbTextureRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapBoard.MapInfo.LBTop != null)
            {
                _lbTop = (int)_mapBoard.MapInfo.LBTop;
                this._lbTextureTop = CreateLBBorder((int) (_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbTop, _DxDeviceManager.GraphicsDevice); // add a little more width to the top LB border for very small maps
            }
            if (_mapBoard.MapInfo.LBBottom != null)
            {
                _lbBottom = (int)_mapBoard.MapInfo.LBBottom;
                this._lbTextureBottom = CreateLBBorder((int) (_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbBottom, _DxDeviceManager.GraphicsDevice);
            }

            // Set border data on RenderingManager
            _renderingManager.SetVRBorderData(_vrFieldBoundary, _drawVRBorderLeftRight, _vrBoundaryTextureLeft, _vrBoundaryTextureRight);
            _renderingManager.SetLBBorderData(_lbTextureLeft, _lbTextureRight);

            ///////////////////////////////////////////////

            // mirror bottom boundaries
            //_mirrorBottomRect
            if (_mapBoard.MapInfo.mirror_Bottom)
            {
                if (_mapBoard.MapInfo.VRLeft != null && _mapBoard.MapInfo.VRRight != null)
                {
                    int vr_width = (int)_mapBoard.MapInfo.VRRight - (int)_mapBoard.MapInfo.VRLeft;
                    const int obj_mirrorBottom_height = 200;

                    _mirrorBottomRect = new Rectangle((int)_mapBoard.MapInfo.VRLeft, (int)_mapBoard.MapInfo.VRBottom - obj_mirrorBottom_height, vr_width, obj_mirrorBottom_height);

                    _mirrorBottomReflection = new ReflectionDrawableBoundary(128, 255, "mirror", true, false);
                }
            }
            /*
            DXObject leftDXVRObject = new DXObject(
                _vrFieldBoundary.Left - VR_BORDER_WIDTHHEIGHT,
                _vrFieldBoundary.Top,
                _vrBoundaryTextureLeft);
            this.leftVRBorderDrawableItem = new BaseDXDrawableItem(leftDXVRObject, false);
            //new BackgroundItem(int cx, int cy, int rx, int ry, BackgroundType.Regular, 255, true, leftDXVRObject, false, (int) RenderResolution.Res_All);

            // Right VR
            DXObject rightDXVRObject = new DXObject(
                _vrFieldBoundary.Right,
                _vrFieldBoundary.Top,
                _vrBoundaryTextureRight);
            this.rightVRBorderDrawableItem = new BaseDXDrawableItem(rightDXVRObject, false);
            */
            ///////////// End Border

            // Debug items
            System.Drawing.Bitmap bitmap_debug = new System.Drawing.Bitmap(1, 1);
            bitmap_debug.SetPixel(0, 0, System.Drawing.Color.White);
            _debugBoundaryTexture = bitmap_debug.ToTexture2D(_DxDeviceManager.GraphicsDevice);

            // Initialize chat system
            _chat.Initialize(_fontChat, _debugBoundaryTexture, Height);
            RegisterChatCommands();

            // Initialize pickup notice UI (bottom right corner messages)
            _pickupNoticeUI.Initialize(_fontChat, _debugBoundaryTexture, Width, Height);

            // Initialize limited view field (fog of war)
            _limitedViewField.Initialize(_DxDeviceManager.GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight);

            // Initialize combat effects (damage numbers, hit effects)
            _combatEffects.Initialize(_DxDeviceManager.GraphicsDevice, _fontDebugValues);

            // Load boss HP bar textures from WZ files (UI.wz/UIWindow.img/MobGage)
            // Try UIWindow2 first (newer clients), then UIWindow1 (older clients)
            if (uiWindow2Image != null)
            {
                _combatEffects.LoadBossHPBarFromWz(uiWindow2Image);
            }
            if (!_combatEffects.HasWzBossHPBar && uiWindow1Image != null)
            {
                _combatEffects.LoadBossHPBarFromWz(uiWindow1Image);
            }

            // Load damage number sprites from Effect.wz/BasicEff.img
            // This enables authentic MapleStory digit sprites for damage numbers
            var basicEffImage = Program.FindImage("Effect", "BasicEff.img");
            if (basicEffImage != null)
            {
                _combatEffects.LoadDamageNumbersFromWz(basicEffImage);
            }

            // Initialize status bar character stats display
            // Positions derived from IDA Pro analysis of CUIStatusBar::SetNumberValue and CUIStatusBar::SetStatusValue
            if (statusBarUi != null)
            {
                statusBarUi.SetCharacterStatsProvider(_fontDebugValues, GetCharacterStatsData);
                statusBarUi.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
            }

            // Initialize Ability/Stat window with player's CharacterBuild
            // This connects the stat window to the player's actual stats (STR, DEX, INT, LUK, etc.)
            if (uiWindowManager?.AbilityWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.AbilityWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.AbilityWindow.SetFont(_fontDebugValues);
            }

            // Start fade-in effect on initial map load (matching CField::Init behavior)
            // This creates the classic MapleStory "fade in from black" effect when entering a map
            _portalFadeState = PortalFadeState.FadingIn;
            _screenEffects.FadeIn(PORTAL_FADE_DURATION_MS, Environment.TickCount);

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
            if (_audio != null)
            {
                //_audio.Pause();
                _audio.Dispose();
            }

            _soundManager?.Dispose();

            _skeletonMeshRenderer.End();

            _DxDeviceManager.EndDraw();
            _DxDeviceManager.Dispose();


            mapObjects_NPCs.Clear();
            mapObjects_Mobs.Clear();
            mapObjects_Reactors.Clear();
            mapObjects_Portal.Clear();

            backgrounds_front.Clear();
            backgrounds_back.Clear();

            _texturePool.Dispose();

            // clear prior mirror bottom boundary
            _mirrorBottomRect = new Rectangle();
            _mirrorBottomReflection = null;
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

            // Clear mob pool
            _mobPool?.Clear();

            // Clear drop pool
            _dropPool?.Clear();

            // Clear portal pool
            _portalPool?.Clear();

            // Clear reactor pool
            _reactorPool?.Clear();

            // Clear combat effects (only map-specific effects like mob HP bars)
            _combatEffects?.ClearMapState();

            // Prepare player manager for map change (preserves character, caches, skill levels)
            _playerManager?.PrepareForMapChange();

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
            _vrBoundaryTextureLeft?.Dispose();
            _vrBoundaryTextureRight?.Dispose();
            _vrBoundaryTextureTop?.Dispose();
            _vrBoundaryTextureBottom?.Dispose();
            _vrBoundaryTextureLeft = null;
            _vrBoundaryTextureRight = null;
            _vrBoundaryTextureTop = null;
            _vrBoundaryTextureBottom = null;
            _drawVRBorderLeftRight = false;

            // Dispose LB border textures
            _lbTextureLeft?.Dispose();
            _lbTextureRight?.Dispose();
            _lbTextureTop?.Dispose();
            _lbTextureBottom?.Dispose();
            _lbTextureLeft = null;
            _lbTextureRight = null;
            _lbTextureTop = null;
            _lbTextureBottom = null;
            _lbSide = 0;
            _lbTop = 0;
            _lbBottom = 0;

            // Clear minimap and status bar (but NOT mouse cursor - it's preserved)
            miniMapUi = null;
            statusBarUi = null;
            statusBarChatUI = null;
            // Note: mouseCursor is intentionally NOT cleared here - same cursor used across all maps

            // Clear mirror boundaries
            _mirrorBottomRect = new Rectangle();
            _mirrorBottomReflection = null;

            // Note: Don't call _texturePool.DisposeAll() here - it would dispose textures
            // still in use by preserved components (mouse cursor). The TexturePool has
            // TTL-based cleanup that will automatically dispose unused textures after 5 minutes.

            // Reset portal click tracking
            _lastClickedPortal = null;
            _lastClickedHiddenPortal = null;
            _lastClickTime = 0;

            // Reset same-map teleport state
            _sameMapTeleportPending = false;
            _sameMapTeleportTarget = null;

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
            this._mapBoard = newBoard;
            this._spawnPortalName = spawnPortalName;

            // Update window title
            Window.Title = newTitle;

            // Update map type flags
            string[] titleNameParts = newTitle.Split(':');
            _gameState.IsLoginMap = titleNameParts.All(part => part.Contains("MapLogin"));
            _gameState.IsCashShopMap = titleNameParts.All(part => part.Contains("CashShopPreview"));

            // Regenerate minimap if needed
            if (_mapBoard.MiniMap == null)
                _mapBoard.RegenerateMinimap();

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
            string newBgmName = _mapBoard.MapInfo.bgm;
            if (_currentBgmName != newBgmName)
            {
                // Different BGM - dispose old and load new
                if (_audio != null)
                {
                    _audio.Dispose();
                    _audio = null;
                }

                if (Program.InfoManager.BGMs.ContainsKey(newBgmName))
                {
                    _currentBgmName = newBgmName;
                    _audio = new WzSoundResourceStreamer(Program.InfoManager.BGMs[newBgmName], true);
                    _audio?.Play();
                }
                else
                {
                    _currentBgmName = null;
                }
            }
            // If same BGM, just keep playing - no changes needed

            // VR boundaries
            if (_mapBoard.VRRectangle == null)
            {
                _vrFieldBoundary = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
                _vrRectangle = new Rectangle(0, 0, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);
            }
            else
            {
                _vrFieldBoundary = new Rectangle(
                    _mapBoard.VRRectangle.X + _mapBoard.CenterPoint.X,
                    _mapBoard.VRRectangle.Y + _mapBoard.CenterPoint.Y,
                    _mapBoard.VRRectangle.Width,
                    _mapBoard.VRRectangle.Height);
                _vrRectangle = new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y, _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height);
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
                foreach (LayeredItem tileObj in _mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;
                    mapObjects[tileObj.LayerNumber].Add(
                        MapSimulatorLoader.CreateMapItemFromProperty(_texturePool, tileParent, tileObj.X, tileObj.Y, _mapBoard.CenterPoint, _DxDeviceManager.GraphicsDevice, ref usedProps, tileObj is IFlippable ? ((IFlippable)tileObj).Flip : false));
                }
            });

            Task t_Background = Task.Run(() =>
            {
                foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in _mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, ref usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_front.Add(bgItem);
                }
            });

            Task t_reactor = Task.Run(() =>
            {
                foreach (ReactorInstance reactor in _mapBoard.BoardItems.Reactors)
                {
                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(_texturePool, reactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (reactorItem != null)
                        mapObjects_Reactors.Add(reactorItem);
                }
            });

            Task t_npc = Task.Run(() =>
            {
                foreach (NpcInstance npc in _mapBoard.BoardItems.NPCs)
                {
                    if (npc.Hide)
                        continue;
                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(_texturePool, npc, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (npcItem != null)
                        mapObjects_NPCs.Add(npcItem);
                }
            });

            Task t_mobs = Task.Run(() =>
            {
                foreach (MobInstance mob in _mapBoard.BoardItems.Mobs)
                {
                    if (mob.Hide)
                        continue;
                    MobItem mobItem = MapSimulatorLoader.CreateMobFromProperty(_texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    mapObjects_Mobs.Add(mobItem);
                }
            });

            Task t_portal = Task.Run(() =>
            {
                WzSubProperty portalParent = (WzSubProperty)mapHelperImage["portal"];
                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
                foreach (PortalInstance portal in _mapBoard.BoardItems.Portals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(_texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    if (portalItem != null)
                        mapObjects_Portal.Add(portalItem);
                }
            });

            Task t_tooltips = Task.Run(() =>
            {
                WzSubProperty farmFrameParent = (WzSubProperty)uiToolTipImage?["Item"]?["FarmFrame"];
                foreach (ToolTipInstance tooltip in _mapBoard.BoardItems.ToolTips)
                {
                    TooltipItem item = MapSimulatorLoader.CreateTooltipFromProperty(_texturePool, UserScreenScaleFactor, farmFrameParent, tooltip, _DxDeviceManager.GraphicsDevice);
                    mapObjects_tooltips.Add(item);
                }
            });

            Task t_minimap = Task.Run(() =>
            {
                if (!_gameState.IsLoginMap && !_mapBoard.MapInfo.hideMinimap && !_gameState.IsCashShopMap)
                {
                    miniMapUi = MapSimulatorLoader.CreateMinimapFromProperty(uiWindow1Image, uiWindow2Image, uiBasicImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _mapBoard.MapInfo.strMapName, _mapBoard.MapInfo.strStreetName, soundUIImage, _gameState.IsBigBangUpdate);
                }
            });

            Task t_statusBar = Task.Run(() =>
            {
                if (!_gameState.IsLoginMap && !_gameState.IsCashShopMap)
                {
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate);
                    if (statusBar != null)
                    {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
            });

            // Recreate UI Windows (Inventory, Equipment, Skills, Quest)
            Task t_uiWindows = Task.Run(() =>
            {
                if (!_gameState.IsLoginMap && !_gameState.IsCashShopMap)
                {
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate);
                }
            });

            // Reuse existing cursor if available (cursor is preserved across map changes)
            Task t_cursor = Task.Run(() =>
            {
                if (this.mouseCursor == null)
                {
                    WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                    this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(_texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps, false);
                }
            });

            // Wait for all loading tasks
            Task.WaitAll(t_tiles, t_Background, t_reactor, t_npc, t_mobs, t_portal, t_tooltips, t_minimap, t_statusBar, t_uiWindows, t_cursor);

            // Initialize status bar character stats display after map change
            if (statusBarUi != null)
            {
                statusBarUi.SetCharacterStatsProvider(_fontDebugValues, GetCharacterStatsData);
                statusBarUi.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
            }

            // Reconnect Ability/Stat window to player's CharacterBuild after map change
            if (uiWindowManager?.AbilityWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.AbilityWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.AbilityWindow.SetFont(_fontDebugValues);
            }

            // Reload boss HP bar textures from WZ files (UI.wz/UIWindow.img/MobGage)
            // Try UIWindow2 first (newer clients), then UIWindow1 (older clients)
            if (uiWindow2Image != null)
            {
                _combatEffects.LoadBossHPBarFromWz(uiWindow2Image);
            }
            if (!_combatEffects.HasWzBossHPBar && uiWindow1Image != null)
            {
                _combatEffects.LoadBossHPBarFromWz(uiWindow1Image);
            }

            // Initialize mob foothold references
            InitializeMobFootholds();

            // Convert lists to arrays
            ConvertListsToArrays();

            // Set camera position and spawn point
            bool spawnPositionSet = false;
            float spawnX = _vrFieldBoundary.Center.X;
            float spawnY = _vrFieldBoundary.Center.Y;

            if (!string.IsNullOrEmpty(_spawnPortalName))
            {
                var targetPortal = _mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _spawnPortalName);
                if (targetPortal != null)
                {
                    this.mapShiftX = targetPortal.X;
                    this.mapShiftY = targetPortal.Y;
                    spawnX = targetPortal.X;
                    spawnY = targetPortal.Y;
                    spawnPositionSet = true;
                }
            }
            if (!spawnPositionSet)
            {
                var startPortals = _mapBoard.BoardItems.Portals.Where(portal => portal.pt == PortalType.StartPoint).ToList();
                if (startPortals.Any())
                {
                    Random random = new Random();
                    PortalInstance randomStartPortal = startPortals[random.Next(startPortals.Count)];
                    this.mapShiftX = randomStartPortal.X;
                    this.mapShiftY = randomStartPortal.Y;
                    spawnX = randomStartPortal.X;
                    spawnY = randomStartPortal.Y;
                }
            }

            SetCameraMoveX(true, false, 0);
            SetCameraMoveX(false, true, 0);
            SetCameraMoveY(true, false, 0);
            SetCameraMoveY(false, true, 0);

            // Store map center point for camera calculations
            _mapCenterX = _mapBoard.CenterPoint.X;
            _mapCenterY = _mapBoard.CenterPoint.Y;

            // Initialize player at portal spawn position (not viewfinder center)
            // spawnX/spawnY are set above from target portal or start point
            // For map changes, reconnect existing player instead of creating new one
            if (_playerManager != null && _playerManager.Player != null)
            {
                ReconnectPlayerToMap(spawnX, spawnY);
            }
            else
            {
                InitializePlayerManager(spawnX, spawnY);
            }

            // Initialize camera controller for smooth scrolling
            _cameraController.Initialize(
                _vrFieldBoundary,
                _renderParams.RenderWidth,
                _renderParams.RenderHeight,
                _mapCenterX,
                _mapCenterY,
                _renderParams.RenderObjectScaling);
            _cameraController.SetPosition(spawnX, spawnY);

            // Auto-detect transport maps (CField_ContiMove) from ShipObject in map
            DetectAndInitializeTransportField();

            // Create border textures
            int leftRightVRDifference = (int)((_vrFieldBoundary.Right - _vrFieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth)
            {
                this._drawVRBorderLeftRight = true;
                this._vrBoundaryTextureLeft = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureRight = CreateVRBorder(VR_BORDER_WIDTHHEIGHT, _vrFieldBoundary.Height, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureTop = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
                this._vrBoundaryTextureBottom = CreateVRBorder(_vrFieldBoundary.Width * 2, VR_BORDER_WIDTHHEIGHT, _DxDeviceManager.GraphicsDevice);
            }

            // LB borders
            if (_mapBoard.MapInfo.LBSide != null)
            {
                _lbSide = (int)_mapBoard.MapInfo.LBSide;
                this._lbTextureLeft = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
                this._lbTextureRight = CreateLBBorder(LB_BORDER_WIDTHHEIGHT + _lbSide, this.Height, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapBoard.MapInfo.LBTop != null)
            {
                _lbTop = (int)_mapBoard.MapInfo.LBTop;
                this._lbTextureTop = CreateLBBorder((int)(_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbTop, _DxDeviceManager.GraphicsDevice);
            }
            if (_mapBoard.MapInfo.LBBottom != null)
            {
                _lbBottom = (int)_mapBoard.MapInfo.LBBottom;
                this._lbTextureBottom = CreateLBBorder((int)(_vrFieldBoundary.Width * 1.45), LB_BORDER_WIDTHHEIGHT + _lbBottom, _DxDeviceManager.GraphicsDevice);
            }

            // Set border data on RenderingManager
            _renderingManager.SetVRBorderData(_vrFieldBoundary, _drawVRBorderLeftRight, _vrBoundaryTextureLeft, _vrBoundaryTextureRight);
            _renderingManager.SetLBBorderData(_lbTextureLeft, _lbTextureRight);

            // Mirror bottom boundaries
            if (_mapBoard.MapInfo.mirror_Bottom)
            {
                if (_mapBoard.MapInfo.VRLeft != null && _mapBoard.MapInfo.VRRight != null)
                {
                    int vr_width = (int)_mapBoard.MapInfo.VRRight - (int)_mapBoard.MapInfo.VRLeft;
                    const int obj_mirrorBottom_height = 200;
                    _mirrorBottomRect = new Rectangle((int)_mapBoard.MapInfo.VRLeft, (int)_mapBoard.MapInfo.VRBottom - obj_mirrorBottom_height, vr_width, obj_mirrorBottom_height);
                    _mirrorBottomReflection = new ReflectionDrawableBoundary(128, 255, "mirror", true, false);
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

        /// <summary>
        /// Load physics constants from Map.wz/Physics.img
        /// </summary>
        private void LoadPhysicsConstants()
        {
            WzImage physicsImage = Program.FindImage("Map", "Physics.img");
            PhysicsConstants.Instance.LoadFromWzImage(physicsImage);
        }

        /// <summary>
        /// Load ship and Balrog textures for Transportation Field from WZ
        ///
        /// Ship paths:
        /// - Map.wz/Obj/contimove.img/ship/* - Main ship sprites for Orbis/Ludibrium ships
        /// - Map.wz/Obj/acc*.img/ship/* - Alternative ship sprites
        /// - The actual path is stored in map info as "shipObj" property
        ///
        /// Balrog paths:
        /// - Mob.wz/8150000.img - Crimson Balrog (main Balrog for ship attack)
        /// - Mob.wz/8130100.img - Jr. Balrog (alternative)
        /// </summary>
        private void LoadTransportFieldTextures()
        {
            List<WzObject> usedPropsTemp = new List<WzObject>();
            System.Diagnostics.Debug.WriteLine("[TransportField] Loading textures...");

            // Priority 1: Try contimove.img first (main ship sprites)
            // This is the primary location for ship sprites in MapleStory
            try
            {
                WzImage contiMoveImg = Program.InfoManager?.GetObjectSet("contimove");
                if (contiMoveImg != null)
                {
                    if (!contiMoveImg.Parsed)
                        contiMoveImg.ParseImage();

                    // Look for ship category - structure is contimove.img/ship/0, ship/1, etc.
                    var shipCategory = contiMoveImg["ship"];
                    if (shipCategory != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[TransportField] Found ship category in contimove.img");
                        LoadShipFromCategory(shipCategory, "contimove", ref usedPropsTemp);
                    }

                    // Note: Balrog textures are loaded from mob data via LoadMobFrames below
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportField] Error loading from contimove.img: {ex.Message}");
            }

            // Priority 2: Search acc* object sets for ships
            if (!_transportField.HasShipTextures)
            {
                string[] shipObjectSets = new[] { "acc7", "acc5", "acc1", "acc0", "acc2", "acc3", "acc4", "acc6", "acc8" };
                foreach (var oS in shipObjectSets)
                {
                    try
                    {
                        WzImage objImage = Program.InfoManager?.GetObjectSet(oS);
                        if (objImage != null)
                        {
                            if (!objImage.Parsed)
                                objImage.ParseImage();

                            var shipCategory = objImage["ship"];
                            if (shipCategory != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[TransportField] Found ship category in {oS}");
                                LoadShipFromCategory(shipCategory, oS, ref usedPropsTemp);
                                if (_transportField.HasShipTextures)
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TransportField] Error loading ship from {oS}: {ex.Message}");
                    }
                }
            }

            // Priority 3: Search ALL object sets for ships
            if (!_transportField.HasShipTextures && Program.InfoManager?.ObjectSets != null)
            {
                System.Diagnostics.Debug.WriteLine("[TransportField] Searching all object sets for ship...");
                foreach (var kvp in Program.InfoManager.ObjectSets)
                {
                    try
                    {
                        var objImage = kvp.Value;
                        if (objImage == null) continue;

                        if (!objImage.Parsed)
                            objImage.ParseImage();

                        var shipCategory = objImage["ship"];
                        if (shipCategory != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TransportField] Found ship in object set: {kvp.Key}");
                            LoadShipFromCategory(shipCategory, kvp.Key, ref usedPropsTemp);
                            if (_transportField.HasShipTextures)
                                break;
                        }
                    }
                    catch { }
                }
            }

            // Priority 4: Use mob sprite as ship placeholder
            if (!_transportField.HasShipTextures)
            {
                System.Diagnostics.Debug.WriteLine("[TransportField] No ship objects found, trying mob sprite as placeholder...");
                // Use large mobs as placeholder
                string[] shipMobIds = new[] { "8130100", "8150000", "8150100", "5220002", "6130101" };
                foreach (var mobId in shipMobIds)
                {
                    var shipMobFrames = LoadMobFrames(mobId, "stand", ref usedPropsTemp);
                    if (shipMobFrames != null && shipMobFrames.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TransportField] Using mob {mobId} as ship sprite ({shipMobFrames.Count} frames)");
                        _transportField.SetShipFrames(shipMobFrames);
                        break;
                    }
                }
            }

            if (!_transportField.HasShipTextures)
            {
                System.Diagnostics.Debug.WriteLine("[TransportField] No ship textures found, will use debug rectangles");
            }

            // Load Balrog/Crimson Balrog for attack event
            // Mob IDs: 8150000 = Crimson Balrog, 8150100 = Crimson Balrog variant, 8130100 = Jr. Balrog
            if (!_transportField.HasBalrogTextures)
            {
                string[] balrogMobIds = new[] { "8150000", "8150100", "8130100", "5220002" };
                foreach (var mobId in balrogMobIds)
                {
                    // Try fly animation first (Balrog's flying attack)
                    var balrogFrames = LoadMobFrames(mobId, "fly", ref usedPropsTemp);
                    if (balrogFrames == null || balrogFrames.Count == 0)
                        balrogFrames = LoadMobFrames(mobId, "move", ref usedPropsTemp);
                    if (balrogFrames == null || balrogFrames.Count == 0)
                        balrogFrames = LoadMobFrames(mobId, "stand", ref usedPropsTemp);

                    if (balrogFrames != null && balrogFrames.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TransportField] Loaded Balrog sprite from mob {mobId} ({balrogFrames.Count} frames)");
                        _transportField.SetBalrogFrames(balrogFrames);
                        break;
                    }
                }
            }

            if (!_transportField.HasBalrogTextures)
            {
                System.Diagnostics.Debug.WriteLine("[TransportField] No Balrog mob found");
            }
        }

        /// <summary>
        /// Load ship frames from a WZ category node
        /// </summary>
        private void LoadShipFromCategory(WzObject shipCategory, string source, ref List<WzObject> usedProps)
        {
            if (shipCategory == null) return;

            // Ship category structure: ship/0, ship/1, etc.
            // Each variant has animation frames: 0/0, 0/1, 0/2...
            if (shipCategory is WzSubProperty subProp)
            {
                foreach (var prop in subProp.WzProperties)
                {
                    if (prop is WzImageProperty shipVariant)
                    {
                        var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, shipVariant, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps);
                        if (shipFrames.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TransportField] Loaded {shipFrames.Count} ship frames from {source}/ship/{prop.Name}");
                            _transportField.SetShipFrames(shipFrames);
                            return;
                        }
                    }
                }
            }
            else if (shipCategory is WzImageProperty imgProp)
            {
                var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, imgProp, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps);
                if (shipFrames.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[TransportField] Loaded {shipFrames.Count} ship frames from {source}/ship");
                    _transportField.SetShipFrames(shipFrames);
                }
            }
        }

        /// <summary>
        /// Helper to load mob frames by ID and action
        /// </summary>
        private List<IDXObject> LoadMobFrames(string mobId, string action, ref List<WzObject> usedProps)
        {
            try
            {
                string mobImgName = WzInfoTools.AddLeadingZeros(mobId, 7) + ".img";
                WzImage mobImage = null;

                if (Program.DataSource != null)
                {
                    mobImage = Program.DataSource.GetImage("Mob", mobImgName);
                }
                if (mobImage == null && Program.WzManager != null)
                {
                    mobImage = (WzImage)Program.WzManager.FindWzImageByName("mob", mobImgName);
                }

                if (mobImage != null)
                {
                    if (!mobImage.Parsed)
                        mobImage.ParseImage();

                    // Try requested action, then fallback to stand/move
                    var actionProp = mobImage[action] ?? mobImage["stand"] ?? mobImage["move"] ?? mobImage["fly"];
                    if (actionProp is WzImageProperty imgProp)
                    {
                        return MapSimulatorLoader.LoadFrames(_texturePool, imgProp, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedProps);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportField] Error loading mob {mobId}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Detect transport maps (CField_ContiMove) by checking for ShipObject in map's MiscItems.
        /// Transport maps are typically in the 990000xxx range (e.g., 990000100 Orbis ship).
        /// When detected, auto-initialize the TransportationField with ship configuration from WZ.
        /// </summary>
        private void DetectAndInitializeTransportField()
        {
            // Reset transport field for new map
            _transportField.Reset();

            // Look for ShipObject in map's MiscItems
            var shipObject = _mapBoard.BoardItems.MiscItems
                .OfType<ShipObject>()
                .FirstOrDefault();

            if (shipObject == null)
            {
                System.Diagnostics.Debug.WriteLine("[TransportField] No ShipObject found in map");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[TransportField] Detected transport map with ShipObject:");
            System.Diagnostics.Debug.WriteLine($"  - Position: ({shipObject.X}, {shipObject.Y})");
            System.Diagnostics.Debug.WriteLine($"  - X0: {shipObject.X0}");
            System.Diagnostics.Debug.WriteLine($"  - ShipKind: {shipObject.ShipKind}");
            System.Diagnostics.Debug.WriteLine($"  - TimeMove: {shipObject.TimeMove}s");
            System.Diagnostics.Debug.WriteLine($"  - Flip: {shipObject.Flip}");

            // Load ship textures from the ObjectInfo
            // ObjectInfo path: Map.wz/Obj/{oS}/{l0}/{l1}/{l2}
            // For ships: Map.wz/Obj/contimove.img/ship/0/0 (oS=contimove, l0=ship, l1=0, l2=0)
            // We need to load all frames from l1 level (the animation container)
            List<WzObject> usedPropsTemp = new List<WzObject>();
            try
            {
                var objInfo = shipObject.BaseInfo as ObjectInfo;
                if (objInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[TransportField] Ship ObjectInfo: {objInfo.oS}/{objInfo.l0}/{objInfo.l1}/{objInfo.l2}");

                    // Get the object set (e.g., contimove.img)
                    WzImage objectSet = Program.InfoManager?.GetObjectSet(objInfo.oS);
                    if (objectSet != null)
                    {
                        if (!objectSet.Parsed)
                            objectSet.ParseImage();

                        // Navigate to the animation container (l0/l1 level, e.g., ship/0)
                        var animContainer = objectSet[objInfo.l0]?[objInfo.l1];
                        if (animContainer is WzImageProperty animProp)
                        {
                            var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, animProp, 0, 0, _DxDeviceManager.GraphicsDevice, ref usedPropsTemp);
                            if (shipFrames.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[TransportField] Loaded {shipFrames.Count} ship frames from {objInfo.oS}/{objInfo.l0}/{objInfo.l1}");
                                _transportField.SetShipFrames(shipFrames);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportField] Error loading ship frames: {ex.Message}");
            }

            // Fallback to mob sprite if no ship frames loaded
            if (!_transportField.HasShipTextures)
            {
                LoadTransportFieldTextures();
            }

            // Initialize transport field with ship configuration
            // Note: X0 is only used for shipKind 0 (regular ships)
            int x0 = shipObject.X0 ?? shipObject.X - 800; // Default to 800 pixels away if not specified

            _transportField.Initialize(
                shipKind: shipObject.ShipKind,
                x: shipObject.X,
                y: shipObject.Y,
                x0: x0,
                f: shipObject.Flip ? 1 : 0,
                tMove: shipObject.TimeMove > 0 ? shipObject.TimeMove : 10
            );

            // For transport maps, start with ship docked (player boards before departure)
            // The ship will depart when triggered by game event or key press
            System.Diagnostics.Debug.WriteLine("[TransportField] Transport field initialized - ship ready at dock");
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
        private KeyboardState _oldKeyboardState = Keyboard.GetState();
        private MouseState _oldMouseState = Mouse.GetState();

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

            // Update UI Windows - handles ESC to close windows and I/E/S/Q toggles
            // Pass chat state to prevent hotkeys from working while typing
            bool uiWindowsHandledEsc = false;
            if (uiWindowManager != null)
            {
                uiWindowsHandledEsc = uiWindowManager.Update(gameTime, currTickCount, _chat.IsActive);
            }

            // Allows the game to exit via gamepad Back button only
            // ESC key is used to close UI windows (Inventory, Skills, Quest, Equipment)
            // To exit simulator: use Alt+F4, window X button, or gamepad Back button
#if !WINDOWS_STOREAPP
            bool backPressed = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed;

            if (!_chat.IsActive && backPressed)
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
                if (!_screenshotManager.TakeScreenshot && _screenshotManager.IsComplete)
                {
                    _screenshotManager.RequestScreenshot();
                }
            }


            // Handle mouse
            mouseCursor.UpdateCursorState();

            // Check if mouse is hovering over an NPC
            CheckNpcHover(newMouseState);

            // Handle portal double-click for teleportation
            HandlePortalDoubleClick(newMouseState);

            // Handle portal UP key interaction (player presses UP near portal)
            HandlePortalUpInteract();

            // Handle same-map portal teleport with delay (no fade, just wait for delay)
            if (_sameMapTeleportPending)
            {
                int elapsed = currTickCount - _sameMapTeleportStartTime;
                if (elapsed >= _sameMapTeleportDelay)
                {
                    // Delay complete - perform the teleport
                    if (_sameMapTeleportTarget != null)
                    {
                        // Move player to target portal position
                        _playerManager?.TeleportTo(_sameMapTeleportTarget.X, _sameMapTeleportTarget.Y);
                        _playerManager?.SetSpawnPoint(_sameMapTeleportTarget.X, _sameMapTeleportTarget.Y);

                        // Update camera to follow player
                        if (_gameState.UseSmoothCamera)
                        {
                            _cameraController.TeleportTo(_sameMapTeleportTarget.X, _sameMapTeleportTarget.Y);
                            mapShiftX = _cameraController.MapShiftX;
                            mapShiftY = _cameraController.MapShiftY;
                        }
                        else
                        {
                            this.mapShiftX = _sameMapTeleportTarget.X;
                            this.mapShiftY = _sameMapTeleportTarget.Y;
                            SetCameraMoveX(true, false, 0);
                            SetCameraMoveX(false, true, 0);
                            SetCameraMoveY(true, false, 0);
                            SetCameraMoveY(false, true, 0);
                        }
                    }

                    // Clear same-map teleport state
                    _sameMapTeleportPending = false;
                    _sameMapTeleportTarget = null;
                }
            }

            // Handle pending map change with fade effect (matching official client behavior)
            // Flow for different map: Portal activated → Fade Out (600ms) → Map Change → Fade In (600ms)
            // Flow for same map: Portal activated → Delay → Teleport (no fade)
            if (_gameState.PendingMapChange && _loadMapCallback != null)
            {
                // Check if teleporting within the same map - use delay instead of fade
                if (_gameState.PendingMapId == _mapBoard.MapInfo.id && !string.IsNullOrEmpty(_gameState.PendingPortalName))
                {
                    var targetPortal = _mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _gameState.PendingPortalName);
                    if (targetPortal != null && !_sameMapTeleportPending)
                    {
                        // Start same-map teleport delay
                        _sameMapTeleportPending = true;
                        _sameMapTeleportStartTime = currTickCount;
                        _sameMapTeleportDelay = targetPortal.delay ?? SAME_MAP_PORTAL_DEFAULT_DELAY_MS;
                        _sameMapTeleportTarget = targetPortal;
                    }

                    // Clear pending map change state (same-map teleport is now handled separately)
                    _gameState.PendingMapChange = false;
                    _gameState.PendingMapId = -1;
                    _gameState.PendingPortalName = null;
                }
                else
                {
                    // Different map - use fade effect
                    // Start fade-out if not already fading
                    if (_portalFadeState == PortalFadeState.None)
                    {
                        _portalFadeState = PortalFadeState.FadingOut;
                        _screenEffects.FadeOut(PORTAL_FADE_DURATION_MS, currTickCount);
                    }

                    // Wait for fade-out to complete before processing map change
                    if (_portalFadeState == PortalFadeState.FadingOut)
                    {
                        // Update screen effects to progress the fade animation
                        // (needed because we return early, skipping the normal update location)
                        _screenEffects.UpdateFade(currTickCount);

                        // Check if fade-out is complete (screen is fully black)
                        if (_screenEffects.IsFadeOutComplete || !_screenEffects.IsFadeActive)
                        {
                            // Fade-out complete, now process the map change
                            _gameState.PendingMapChange = false;

                            // Different map - perform full reload
                            var result = _loadMapCallback(_gameState.PendingMapId);
                            if (result != null && result.Item1 != null)
                            {
                                // Save current map state before unloading
                                int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
                                if (currentMapId >= 0 && _mobsArray != null)
                                {
                                    _mapStateCache.SaveMapState(currentMapId, _mobsArray, currTickCount);
                                }

                                // Perform seamless map transition
                                UnloadMapContent();
                                LoadMapContent(result.Item1, result.Item2, _gameState.PendingPortalName);

                                // Restore map state if previously visited
                                int newMapId = _mapBoard?.MapInfo?.id ?? -1;
                                if (newMapId >= 0 && _mobsArray != null)
                                {
                                    _mapStateCache.RestoreMapState(newMapId, _mobsArray, currTickCount);
                                }

                                // Sync input state immediately so held Up key won't trigger portal on next frame
                                _playerManager?.Input?.SyncState();
                            }

                            // Clear pending state
                            _gameState.PendingMapId = -1;
                            _gameState.PendingPortalName = null;

                            // Start fade-in after map change
                            _portalFadeState = PortalFadeState.FadingIn;
                            _screenEffects.FadeIn(PORTAL_FADE_DURATION_MS, currTickCount);
                        }
                        return; // Skip the rest of this frame while fading
                    }
                }
            }

            // Handle fade-in completion
            if (_portalFadeState == PortalFadeState.FadingIn)
            {
                if (_screenEffects.IsFadeInComplete || !_screenEffects.IsFadeActive)
                {
                    _portalFadeState = PortalFadeState.None;
                    // Sync input state so held keys don't trigger "just pressed" detection
                    // This prevents immediate re-entry if player is still holding Up
                    _playerManager?.Input?.SyncState();
                }
            }

            // Handle chat input (returns true if chat consumed the input)
            bool chatConsumedInput = _chat.HandleInput(newKeyboardState, _oldKeyboardState, currTickCount);

            // Skip navigation and other key handlers if chat is active
            if (!chatConsumedInput && !_chat.IsActive)
            {
                // Navigate around the rendered object
                bool bIsShiftPressed = newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift);

                bool bIsUpKeyPressed = newKeyboardState.IsKeyDown(Keys.Up);
                bool bIsDownKeyPressed = newKeyboardState.IsKeyDown(Keys.Down);
                bool bIsLeftKeyPressed = newKeyboardState.IsKeyDown(Keys.Left);
                bool bIsRightKeyPressed = newKeyboardState.IsKeyDown(Keys.Right);

                // Arrow keys control player movement via physics system (not direct position)
                // Input is passed to PlayerManager which forwards to PlayerCharacter.SetInput()
                // The actual movement happens in PlayerCharacter.ProcessInput() using proper physics
                if (_gameState.PlayerControlEnabled && _playerManager?.Player != null)
                {
                    // Camera follows player - center player on screen
                    // Formula: screenX = worldX - mapShiftX + mapCenterX
                    // To center player: RenderWidth/2 = player.X - mapShiftX + mapCenterX
                    // So: mapShiftX = player.X + mapCenterX - RenderWidth/2
                    var player = _playerManager.Player;
                    mapShiftX = (int)(player.X + _mapCenterX - _renderParams.RenderWidth / 2);
                    mapShiftY = (int)(player.Y + _mapCenterY - _renderParams.RenderHeight / 2);
                }
                else
                {
                    // Free camera mode - original behavior
                    int moveOffset = bIsShiftPressed ? (int)(3000f / frameRate) : (int)(1500f / frameRate);
                    if (bIsLeftKeyPressed || bIsRightKeyPressed)
                    {
                        SetCameraMoveX(bIsLeftKeyPressed, bIsRightKeyPressed, moveOffset);
                    }
                    if (bIsUpKeyPressed || bIsDownKeyPressed)
                    {
                        SetCameraMoveY(bIsUpKeyPressed, bIsDownKeyPressed, moveOffset);
                    }
                }

                // Minimap M
                if (newKeyboardState.IsKeyDown(Keys.M))
                {
                    if (miniMapUi != null)
                        miniMapUi.MinimiseOrMaximiseMinimap(currTickCount);
                }

                // Hide UI
                if (newKeyboardState.IsKeyUp(Keys.H) && _oldKeyboardState.IsKeyDown(Keys.H)) {
                    this._gameState.HideUIMode = !this._gameState.HideUIMode;
                }
            }

            // Debug keys
            if (newKeyboardState.IsKeyUp(Keys.F5) && _oldKeyboardState.IsKeyDown(Keys.F5))
            {
                this._gameState.ShowDebugMode = !this._gameState.ShowDebugMode;
            }

            // Toggle mob movement with F6
            if (newKeyboardState.IsKeyUp(Keys.F6) && _oldKeyboardState.IsKeyDown(Keys.F6))
            {
                this._gameState.MobMovementEnabled = !this._gameState.MobMovementEnabled;
            }

            // Toggle player control mode with Tab (switch between player control and free camera)
            /*if (newKeyboardState.IsKeyUp(Keys.Tab) && _oldKeyboardState.IsKeyDown(Keys.Tab))
            {
                _gameState.PlayerControlEnabled = !_gameState.PlayerControlEnabled;
                Debug.WriteLine($"Player control: {(_gameState.PlayerControlEnabled ? "ENABLED" : "DISABLED (free camera)")}");
            }*/

            // Respawn player with R key at original spawn point (portal position)
            if (newKeyboardState.IsKeyUp(Keys.R) && _oldKeyboardState.IsKeyDown(Keys.R))
            {
                _playerManager?.Respawn();
                var pos = _playerManager?.GetPlayerPosition();
                Debug.WriteLine($"Player respawned at spawn point ({pos?.X}, {pos?.Y})");
            }

            // Test screen tremble with F7 (for debugging effects)
            if (newKeyboardState.IsKeyUp(Keys.F7) && _oldKeyboardState.IsKeyDown(Keys.F7))
            {
                _screenEffects.TriggerTremble(15, false, 0, 0, true, currTickCount);
            }

            // Test knockback on random mob with F8 (for debugging)
            if (newKeyboardState.IsKeyUp(Keys.F8) && _oldKeyboardState.IsKeyDown(Keys.F8))
            {
                TestKnockbackRandomMob();
            }

            // Test motion blur with F9 (for debugging)
            if (newKeyboardState.IsKeyUp(Keys.F9) && _oldKeyboardState.IsKeyDown(Keys.F9))
            {
                _screenEffects.HorizontalBlur(0.7f, true, 500, currTickCount);
            }

            // Test explosion effect with F10 (for debugging)
            if (newKeyboardState.IsKeyUp(Keys.F10) && _oldKeyboardState.IsKeyDown(Keys.F10))
            {
                // Trigger explosion at center of screen (converted to map coordinates)
                float explosionX = -mapShiftX + Width / 2;
                float explosionY = -mapShiftY + Height / 2;
                _screenEffects.FireExplosion(explosionX, explosionY, 200, 800, currTickCount);
            }

            // Test chain lightning with F11 (for debugging)
            if (newKeyboardState.IsKeyUp(Keys.F11) && _oldKeyboardState.IsKeyDown(Keys.F11))
            {
                // Create chain lightning from left to right of screen
                float startX = -mapShiftX + 100;
                float endX = -mapShiftX + Width - 100;
                float y = -mapShiftY + Height / 2;

                var points = new System.Collections.Generic.List<Vector2>
                {
                    new Vector2(startX, y),
                    new Vector2(startX + (endX - startX) * 0.33f, y - 50),
                    new Vector2(startX + (endX - startX) * 0.66f, y + 50),
                    new Vector2(endX, y)
                };
                _animationEffects.AddChainLightning(points, new Color(100, 150, 255), 800, currTickCount, 4f, 10);
            }

            // Test falling animation with F12 (for debugging)
            if (newKeyboardState.IsKeyUp(Keys.F12) && _oldKeyboardState.IsKeyDown(Keys.F12))
            {
                // Create burst of falling particles at screen center
                TestFallingBurst(currTickCount);
            }

            // Weather controls: 1=Rain, 2=Snow, 3=Leaves, 0=Off
            if (newKeyboardState.IsKeyUp(Keys.D1) && _oldKeyboardState.IsKeyDown(Keys.D1))
            {
                ToggleWeather(WeatherType.Rain);
            }
            if (newKeyboardState.IsKeyUp(Keys.D2) && _oldKeyboardState.IsKeyDown(Keys.D2))
            {
                ToggleWeather(WeatherType.Snow);
            }
            if (newKeyboardState.IsKeyUp(Keys.D3) && _oldKeyboardState.IsKeyDown(Keys.D3))
            {
                ToggleWeather(WeatherType.Leaves);
            }
            if (newKeyboardState.IsKeyUp(Keys.D0) && _oldKeyboardState.IsKeyDown(Keys.D0))
            {
                ToggleWeather(WeatherType.None);
            }

            // Toggle fear effect with 4
            if (newKeyboardState.IsKeyUp(Keys.D4) && _oldKeyboardState.IsKeyDown(Keys.D4))
            {
                if (_fieldEffects.IsFearActive)
                {
                    _fieldEffects.StopFearEffect();
                }
                else
                {
                    _fieldEffects.InitFearEffect(0.7f, 10000, 5, currTickCount);
                }
            }

            // Test weather message with 5
            if (newKeyboardState.IsKeyUp(Keys.D5) && _oldKeyboardState.IsKeyDown(Keys.D5))
            {
                _fieldEffects.OnBlowWeather(WeatherEffectType.Rain, null, "A gentle rain begins to fall...", 1f, 15000, currTickCount);
                ToggleWeather(WeatherType.Rain);
            }

            // Test horizontal moving platform with 6
            if (newKeyboardState.IsKeyUp(Keys.D6) && _oldKeyboardState.IsKeyDown(Keys.D6))
            {
                // Spawn platform at mouse position in map coordinates (same formula as portal detection)
                float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                _dynamicFootholds.CreateHorizontalPlatform(platX, platY, 100, 15, platX - 150, platX + 150, 80f, 500);
            }

            // Test vertical moving platform with 7
            if (newKeyboardState.IsKeyUp(Keys.D7) && _oldKeyboardState.IsKeyDown(Keys.D7))
            {
                float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                _dynamicFootholds.CreateVerticalPlatform(platX, platY, 80, 15, platY - 100, platY + 100, 60f, 300);
            }

            // Test timed spawn/despawn platform with 8
            if (newKeyboardState.IsKeyUp(Keys.D8) && _oldKeyboardState.IsKeyDown(Keys.D8))
            {
                float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                _dynamicFootholds.CreateTimedPlatform(platX, platY, 100, 15, 2000, 1500, 0);
            }

            // Test waypoint platform with 9
            if (newKeyboardState.IsKeyUp(Keys.D9) && _oldKeyboardState.IsKeyDown(Keys.D9))
            {
                float platX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                float platY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;
                var waypoints = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>
                {
                    new Microsoft.Xna.Framework.Vector2(platX, platY),
                    new Microsoft.Xna.Framework.Vector2(platX + 100, platY - 50),
                    new Microsoft.Xna.Framework.Vector2(platX + 200, platY),
                    new Microsoft.Xna.Framework.Vector2(platX + 100, platY + 50)
                };
                _dynamicFootholds.CreateWaypointPlatform(80, 15, waypoints, 70f, true, 200);
            }

            // Test limited view / fog of war with 0 (cycle through modes)
            if (newKeyboardState.IsKeyUp(Keys.D0) && _oldKeyboardState.IsKeyDown(Keys.D0))
            {
                if (!_limitedViewField.Enabled)
                {
                    // Start with circle mode
                    _limitedViewField.EnableCircle(250f);
                    System.Diagnostics.Debug.WriteLine("[LimitedView] Enabled: Circle mode, radius 250");
                }
                else
                {
                    // Cycle through modes: Circle -> Rectangle -> Spotlight -> Disable
                    switch (_limitedViewField.Mode)
                    {
                        case LimitedViewField.ViewMode.Circle:
                            _limitedViewField.EnableRectangle(400f, 300f);
                            System.Diagnostics.Debug.WriteLine("[LimitedView] Switched to: Rectangle mode 400x300");
                            break;
                        case LimitedViewField.ViewMode.Rectangle:
                            _limitedViewField.EnableSpotlight(300f, true);
                            System.Diagnostics.Debug.WriteLine("[LimitedView] Switched to: Spotlight mode with pulse");
                            break;
                        case LimitedViewField.ViewMode.Spotlight:
                            _limitedViewField.Disable();
                            System.Diagnostics.Debug.WriteLine("[LimitedView] Disabled");
                            break;
                        default:
                            _limitedViewField.DisableImmediate();
                            break;
                    }
                }
            }

            // Ship controls: [-] Start voyage, [=] Balrog/Skip/Reset
            // Based on CField_ContiMove::OnContiMove packet handling:
            // - Case 8: OnStartShipMoveField (LeaveShipMove when value==2)
            // - Case 10: OnMoveField (AppearShip=4, DisappearShip=5)
            // - Case 12: OnEndShipMoveField (EnterShipMove when value==6)
            if (newKeyboardState.IsKeyUp(Keys.OemMinus) && _oldKeyboardState.IsKeyDown(Keys.OemMinus))
            {
                // Load ship and Balrog textures if not already loaded
                if (!_transportField.HasShipTextures)
                {
                    LoadTransportFieldTextures();
                }

                // Initialize and start a demo ship voyage at mouse position
                float shipX = _oldMouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
                float shipY = _oldMouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;

                // Initialize using client-accurate parameters:
                // shipKind: 0 = regular ship (moves x0->x), 1 = Balrog type (appears/disappears)
                // x: docked position, y: ship height, x0: away position
                // f: flip (0=right, 1=left), tMove: movement duration in seconds
                _transportField.Initialize(
                    shipKind: 0,           // Regular ship
                    x: (int)shipX + 400,   // Dock position (right)
                    y: (int)shipY,         // Y position
                    x0: (int)shipX - 400,  // Away position (left)
                    f: 0,                  // Face right
                    tMove: 10              // 10 second movement
                );
                _transportField.SetBackgroundScroll(true, 30f);

                // Start with ship arriving (EnterShipMove - Case 12 value 6)
                _transportField.EnterShipMove();
            }
            if (newKeyboardState.IsKeyUp(Keys.OemPlus) && _oldKeyboardState.IsKeyDown(Keys.OemPlus))
            {
                // Cycle through ship actions based on current state
                switch (_transportField.State)
                {
                    case ShipState.Moving:
                    case ShipState.InTransit:
                        // Trigger Balrog attack during voyage
                        _transportField.TriggerBalrogAttack(5000);
                        break;
                    case ShipState.Docked:
                        // Ship is docked, start departure (LeaveShipMove - Case 8 value 2)
                        _transportField.LeaveShipMove();
                        break;
                    case ShipState.WaitingDeparture:
                        // Skip waiting, force immediate departure
                        _transportField.ForceDeparture();
                        break;
                    default:
                        // Reset to idle
                        _transportField.Reset();
                        break;
                }
            }

            // Sparkle burst at mouse position with Space
            if (newKeyboardState.IsKeyUp(Keys.Space) && _oldKeyboardState.IsKeyDown(Keys.Space))
            {
                float sparkleX = -mapShiftX + _oldMouseState.X;
                float sparkleY = -mapShiftY + _oldMouseState.Y;
                _particleSystem.CreateSparkleBurst(sparkleX, sparkleY, 30, Color.Gold, 1500);
            }

            // Camera zoom controls (scroll wheel or keyboard)
            if (newMouseState.ScrollWheelValue != _oldMouseState.ScrollWheelValue && _gameState.UseSmoothCamera)
            {
                int scrollDelta = newMouseState.ScrollWheelValue - _oldMouseState.ScrollWheelValue;
                if (scrollDelta > 0)
                    _cameraController.ZoomIn();
                else if (scrollDelta < 0)
                    _cameraController.ZoomOut();
            }
            // Home key = reset zoom to 1.0
            if (newKeyboardState.IsKeyUp(Keys.Home) && _oldKeyboardState.IsKeyDown(Keys.Home) && _gameState.UseSmoothCamera)
            {
                _cameraController.ResetZoom();
                Debug.WriteLine("Camera zoom reset to 1.0");
            }
            // C key = random attack (TryDoingMeleeAttack/TryDoingShoot/TryDoingMagicAttack)
            if (newKeyboardState.IsKeyUp(Keys.C) && _oldKeyboardState.IsKeyDown(Keys.C)
                && !newKeyboardState.IsKeyDown(Keys.LeftShift) && !newKeyboardState.IsKeyDown(Keys.RightShift))
            {
                if (_playerManager != null && _playerManager.IsPlayerActive && _gameState.PlayerControlEnabled)
                {
                    bool attacked = _playerManager.TryDoingRandomAttack(currTickCount);
                    if (attacked)
                    {
                        Debug.WriteLine($"[C Key] Random attack executed");
                    }
                }
            }

            // Shift+C key = toggle smooth camera (moved from C key)
            if (newKeyboardState.IsKeyUp(Keys.C) && _oldKeyboardState.IsKeyDown(Keys.C)
                && (newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift)))
            {
                _gameState.UseSmoothCamera = !_gameState.UseSmoothCamera;
                Debug.WriteLine($"Smooth camera: {(_gameState.UseSmoothCamera ? "ENABLED" : "DISABLED")}");
                // When enabling, snap camera to current position
                if (_gameState.UseSmoothCamera && _playerManager != null && _playerManager.IsPlayerActive)
                {
                    var playerPos = _playerManager.GetPlayerPosition();
                    _cameraController.TeleportTo(playerPos.X, playerPos.Y);
                }
            }

            // Update screen effects (tremble, fade, flash, motion blur, explosion)
            _screenEffects.Update(currTickCount);

            // Calculate delta time once for all frame-rate independent updates
            float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update animation effects (one-time, repeat, chain lightning, falling, follow)
            _animationEffects.Update(currTickCount, deltaSeconds);

            // Update combat effects (damage numbers, hit effects, HP bars)
            _combatEffects.Update(currTickCount, deltaSeconds);
            _combatEffects.SyncFromMobPool(_mobPool, currTickCount);
            _combatEffects.SyncHPBarsFromMobPool(_mobPool, currTickCount);

            // Update particle system
            _particleSystem.Update(currTickCount, deltaSeconds);

            // Update player character
            // Pass chat state to block movement/jump input while typing
            if (_playerManager != null)
            {
                _playerManager.IsPlayerControlEnabled = _gameState.PlayerControlEnabled;
                _playerManager.Update(currTickCount, deltaSeconds, _chat.IsActive);

                // Update camera controller based on player/camera mode
                var player = _playerManager.Player;
                bool isPlayerDead = player != null && !player.IsAlive;

                if (_gameState.UseSmoothCamera)
                {
                    if (_gameState.PlayerControlEnabled && _playerManager.IsPlayerActive)
                    {
                        // Use camera controller for smooth player following
                        var playerPos = _playerManager.GetPlayerPosition();
                        bool isOnGround = _playerManager.IsPlayerOnGround();
                        bool facingRight = _playerManager.IsPlayerFacingRight();

                        _cameraController.Update(playerPos.X, playerPos.Y, facingRight, isOnGround, deltaSeconds);
                        _cameraController.UpdateShake(deltaSeconds);

                        // Get camera position from controller
                        mapShiftX = _cameraController.MapShiftX + (int)_cameraController.ShakeOffsetX;
                        mapShiftY = _cameraController.MapShiftY + (int)_cameraController.ShakeOffsetY;

                        // Apply boundary clamping (simple clamp, preserves smooth movement)
                        ClampCameraToBoundaries();
                    }
                    else if (isPlayerDead)
                    {
                        // Player is dead - keep camera focused on death position (no movement)
                        _cameraController.Update(player.DeathX, player.DeathY, true, true, deltaSeconds);
                        _cameraController.UpdateShake(deltaSeconds);

                        mapShiftX = _cameraController.MapShiftX + (int)_cameraController.ShakeOffsetX;
                        mapShiftY = _cameraController.MapShiftY + (int)_cameraController.ShakeOffsetY;

                        ClampCameraToBoundaries();
                    }
                    else
                    {
                        // Free camera mode with smooth scrolling
                        bool left = newKeyboardState.IsKeyDown(Keys.Left);
                        bool right = newKeyboardState.IsKeyDown(Keys.Right);
                        bool up = newKeyboardState.IsKeyDown(Keys.Up);
                        bool down = newKeyboardState.IsKeyDown(Keys.Down);
                        bool shift = newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift);
                        int freeCamSpeed = shift ? 3000 : 1500; // pixels per second

                        _cameraController.UpdateFreeCamera(left, right, up, down, freeCamSpeed, deltaSeconds);
                        _cameraController.UpdateShake(deltaSeconds);

                        mapShiftX = _cameraController.MapShiftX + (int)_cameraController.ShakeOffsetX;
                        mapShiftY = _cameraController.MapShiftY + (int)_cameraController.ShakeOffsetY;

                        // Apply boundary clamping (simple clamp, preserves smooth movement)
                        ClampCameraToBoundaries();
                    }
                }
                else
                {
                    // Legacy instant camera (no smoothing)
                    if (_gameState.PlayerControlEnabled && _playerManager.IsPlayerActive)
                    {
                        var playerPos = _playerManager.GetPlayerPosition();
                        mapShiftX = (int)(playerPos.X + _mapCenterX - _renderParams.RenderWidth / 2);
                        mapShiftY = (int)(playerPos.Y + _mapCenterY - _renderParams.RenderHeight / 2);

                        // Apply boundary clamping
                        SetCameraMoveX(true, false, 0);
                        SetCameraMoveX(false, true, 0);
                        SetCameraMoveY(true, false, 0);
                        SetCameraMoveY(false, true, 0);
                    }
                    else if (isPlayerDead)
                    {
                        // Player is dead - keep camera focused on death position
                        mapShiftX = (int)(player.DeathX + _mapCenterX - _renderParams.RenderWidth / 2);
                        mapShiftY = (int)(player.DeathY + _mapCenterY - _renderParams.RenderHeight / 2);

                        // Apply boundary clamping
                        SetCameraMoveX(true, false, 0);
                        SetCameraMoveX(false, true, 0);
                        SetCameraMoveY(true, false, 0);
                        SetCameraMoveY(false, true, 0);
                    }
                }
            }

            // Update field effects (weather messages, fear effect, obstacles)
            // Pass deltaSeconds * 1000 to convert to milliseconds for frame-rate independence
            _fieldEffects.Update(currTickCount, Width, Height, _oldMouseState.X, _oldMouseState.Y, deltaSeconds * 1000f);

            // Update dynamic footholds (moving platforms)
            _dynamicFootholds.Update(currTickCount, deltaSeconds);

            // Update transportation field (ship movement)
            _transportField.Update(currTickCount, deltaSeconds);

            // Update limited view field (fog of war) - use player position if available
            float playerX = _playerManager?.IsPlayerActive == true
                ? _playerManager.GetPlayerPosition().X
                : mapShiftX + _renderParams.RenderWidth / 2f;
            float playerY = _playerManager?.IsPlayerActive == true
                ? _playerManager.GetPlayerPosition().Y
                : mapShiftY + _renderParams.RenderHeight / 2f;
            _limitedViewField.Update(gameTime, playerX, playerY);

            // Update mob movement
            UpdateMobMovement(gameTime);

            // Update NPC movement and action cycling
            UpdateNpcActions(gameTime);

            // Pre-calculate visibility for all objects (culling optimization)
            _frameNumber++;
            UpdateObjectVisibility();

            this._oldKeyboardState = newKeyboardState;  // set the new state as the old state for next time
            this._oldMouseState = newMouseState;  // set the new state as the old state for next time

            // Cleanup finished sound instances
            _soundManager?.Update();

            base.Update(gameTime);
        }

        /// <summary>
        /// Pre-calculates visibility for all drawable objects.
        /// This avoids redundant visibility checks during Draw phase.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateObjectVisibility()
        {
            int centerX = _mapBoard.CenterPoint.X;
            int centerY = _mapBoard.CenterPoint.Y;
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
                var mirrorData = _mapBoard.BoardItems.CheckObjectWithinMirrorFieldDataBoundary(x, y, MirrorFieldDataType.mob);
                return mirrorData?.ReflectionInfo;
            };

            // Update mobs
            for (int i = 0; i < _mobsArray.Length; i++)
            {
                if (_mobsArray[i] == null)
                    continue;
                _mobsArray[i].UpdateMirrorBoundary(_mirrorBottomRect, _mirrorBottomReflection, checkMirrorFieldData);
            }

            // Update NPCs (using npc mirror type)
            Func<int, int, ReflectionDrawableBoundary> checkNpcMirrorFieldData = (x, y) =>
            {
                var mirrorData = _mapBoard.BoardItems.CheckObjectWithinMirrorFieldDataBoundary(x, y, MirrorFieldDataType.npc);
                return mirrorData?.ReflectionInfo;
            };

            for (int i = 0; i < _npcsArray.Length; i++)
            {
                _npcsArray[i].UpdateMirrorBoundary(_mirrorBottomRect, _mirrorBottomReflection, checkNpcMirrorFieldData);
            }
        }

        /// <summary>
        /// Test knockback on a random visible mob (for debugging F8 key)
        /// </summary>
        private void TestKnockbackRandomMob()
        {
            if (_mobsArray == null || _mobsArray.Length == 0)
                return;

            // Find visible mob and knockback at 50% rate
            var random = new Random();
            int startIndex = random.Next(_mobsArray.Length);

            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mob = _mobsArray[i];

                if (mob != null && mob.MovementInfo != null && mob.IsVisible && random.Next(10) < 5)
                {
                    // Apply knockback in random direction
                    bool knockRight = random.Next(2) == 0;
                    float force = 8f + (float)random.NextDouble() * 4f; // 8-12 force
                    mob.MovementInfo.ApplyKnockback(force, knockRight);

                    // Trigger screen shake for feedback
                    _screenEffects.QuickTremble(8, currTickCount);
                }
            }
        }

        /// <summary>
        /// Test falling animation burst (for debugging F12 key)
        /// Creates simple falling particles using the debug texture
        /// </summary>
        private void TestFallingBurst(int tickCount)
        {
            // Get center of screen in map coordinates
            float centerX = -mapShiftX + Width / 2;
            float startY = -mapShiftY + 50;
            float endY = -mapShiftY + Height - 100;

            // Create simple "particle" frames using available objects
            // For testing, we'll just create chain lightning as visual feedback
            // In real use, you'd load actual effect frames from Effect.wz

            var random = new Random();
            int count = 8;

            for (int i = 0; i < count; i++)
            {
                float x = centerX + (float)(random.NextDouble() * 200 - 100);
                float y1 = startY + (float)(random.NextDouble() * 50);
                float y2 = y1 + 30;

                // Create small lightning bolts as falling "sparkles"
                _animationEffects.AddLightningBolt(
                    new Vector2(x, y1),
                    new Vector2(x + (float)(random.NextDouble() * 20 - 10), endY),
                    new Color(255, 200, 100),
                    1200 + random.Next(400),
                    tickCount + random.Next(200),
                    2f, 6);
            }

            // Also trigger a small screen shake
            _screenEffects.QuickTremble(5, tickCount);
        }

        /// <summary>
        /// Toggle weather effect on/off
        /// </summary>
        private void ToggleWeather(WeatherType weather)
        {
            // Remove current weather emitter if exists
            if (_gameState.ActiveWeatherEmitter >= 0)
            {
                _particleSystem.RemoveEmitter(_gameState.ActiveWeatherEmitter);
                _gameState.ActiveWeatherEmitter = -1;
            }

            _gameState.CurrentWeather = weather;

            // Create new weather emitter
            switch (weather)
            {
                case WeatherType.Rain:
                    _gameState.ActiveWeatherEmitter = _particleSystem.CreateRainEmitter(Width, Height, 1f);
                    break;
                case WeatherType.Snow:
                    _gameState.ActiveWeatherEmitter = _particleSystem.CreateSnowEmitter(Width, Height, 1f);
                    break;
                case WeatherType.Leaves:
                    _gameState.ActiveWeatherEmitter = _particleSystem.CreateLeavesEmitter(Width, Height, 1f);
                    break;
                case WeatherType.None:
                default:
                    // Just clear, no new emitter
                    break;
            }
        }

        /// <summary>
        /// Updates all mob positions based on their movement logic
        /// </summary>
        /// <param name="gameTime"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMobMovement(GameTime gameTime)
        {
            if (!_gameState.MobMovementEnabled)
                return;

            int deltaTimeMs = (int)gameTime.ElapsedGameTime.TotalMilliseconds;
            int tickCount = Environment.TickCount;

            // Get actual player position from PlayerManager (not camera position)
            float? playerX = null;
            float? playerY = null;
            bool playerIsAlive = false;
            if (_playerManager?.Player != null)
            {
                playerX = _playerManager.Player.X;
                playerY = _playerManager.Player.Y;
                playerIsAlive = _playerManager.Player.IsAlive;
            }

            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mobItem = _mobsArray[i];
                if (mobItem == null || mobItem.MovementInfo == null)
                    continue;

                mobItem.MovementEnabled = _gameState.MobMovementEnabled;

                // Boss mobs continue tracking player even when dead
                // Regular mobs lose target when player dies
                bool isBoss = mobItem.AI?.IsBoss == true;
                if (isBoss || playerIsAlive)
                {
                    mobItem.UpdateMovement(deltaTimeMs, tickCount, playerX, playerY);
                }
                else
                {
                    // Player is dead and mob is not a boss - don't pass player position
                    mobItem.UpdateMovement(deltaTimeMs, tickCount, null, null);
                }
            }

            // Update mob pool for death animations, cleanup, and respawns
            _mobPool?.Update(tickCount);

            // Update drop pool for physics and expiration (frame-rate independent)
            float deltaSecondsLocal = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _dropPool?.Update(tickCount, deltaSecondsLocal);

            // Update portal pool for hidden portal visibility
            if (playerX.HasValue && playerY.HasValue)
            {
                _portalPool?.Update(playerX.Value, playerY.Value, tickCount, deltaSecondsLocal);
            }

            // Update reactor pool for state management and respawns
            _reactorPool?.Update(tickCount, deltaSecondsLocal);

            // Update pickup notice UI animations
            _pickupNoticeUI?.Update(tickCount, deltaSecondsLocal);
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
        /// Check if mouse is hovering over an NPC and update cursor state
        /// </summary>
        /// <param name="mouseState">Current mouse state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckNpcHover(MouseState mouseState)
        {
            if (_npcsArray == null || _npcsArray.Length == 0)
                return;

            // Convert screen coordinates to map coordinates (same as portal detection)
            int mouseMapX = mouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
            int mouseMapY = mouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;

            for (int i = 0; i < _npcsArray.Length; i++)
            {
                NpcItem npc = _npcsArray[i];
                if (npc.ContainsMapPoint(mouseMapX, mouseMapY))
                {
                    mouseCursor.SetMouseCursorMovedToNpc();
                    return; // Only need to find one NPC
                }
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
            // Skip if map change is already pending
            if (_gameState.PendingMapChange)
                return;

            // Check for left mouse button click (transition from pressed to released)
            bool isLeftClick = mouseState.LeftButton == ButtonState.Released && _oldMouseState.LeftButton == ButtonState.Pressed;
            if (!isLeftClick)
                return;

            // Calculate mouse position relative to map coordinates
            int mouseMapX = mouseState.X + mapShiftX - _mapBoard.CenterPoint.X;
            int mouseMapY = mouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;

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
                        _playerManager?.ForceStand();
                        _gameState.PendingMapChange = true;
                        _gameState.PendingMapId = targetMapId;
                        _gameState.PendingPortalName = clickedPortal.PortalInstance.tn;
                        return;
                    }
                }

                // Record this click for potential double-click detection
                _lastClickedPortal = clickedPortal;
                _lastClickedHiddenPortal = null;
                _lastClickTime = currentTime;
                return;
            }

            // No visible portal found - check hidden portals from _mapBoard.BoardItems.Portals
            PortalInstance clickedHiddenPortal = null;
            foreach (var portal in _mapBoard.BoardItems.Portals)
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
                    _playerManager?.ForceStand();
                    _gameState.PendingMapChange = true;
                    _gameState.PendingMapId = clickedHiddenPortal.tm;
                    _gameState.PendingPortalName = clickedHiddenPortal.tn;
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
            _soundManager?.PlaySound("Portal");
        }

        /// <summary>
        /// Plays the jump sound effect.
        /// </summary>
        private void PlayJumpSE()
        {
            _soundManager?.PlaySound("Jump");
        }

        /// <summary>
        /// Plays the drop item sound effect (on mob death).
        /// </summary>
        private void PlayDropItemSE()
        {
            _soundManager?.PlaySound("DropItem");
        }

        /// <summary>
        /// Plays the pick up item sound effect.
        /// </summary>
        private void PlayPickUpItemSE()
        {
            _soundManager?.PlaySound("PickUpItem");
        }

        #region Portal and Reactor Pool Utilities

        /// <summary>
        /// Spawn reactors at hidden portal positions.
        /// Uses the PortalPool to find hidden portals and spawns reactors at their locations.
        /// </summary>
        /// <param name="reactorId">Reactor ID to spawn</param>
        /// <returns>Number of reactors spawned</returns>
        public int SpawnReactorsAtHiddenPortals(string reactorId)
        {
            if (_portalPool == null || _reactorPool == null)
                return 0;

            int currentTick = Environment.TickCount;

            // Get all hidden portal positions
            var positions = new List<(float x, float y)>();
            for (int i = 0; i < _portalPool.PortalCount; i++)
            {
                if (_portalPool.IsHiddenPortal(i))
                {
                    var portal = _portalPool.GetPortal(i);
                    if (portal?.PortalInstance != null)
                    {
                        positions.Add((portal.PortalInstance.X, portal.PortalInstance.Y));
                    }
                }
            }

            if (positions.Count == 0)
                return 0;

            var spawnedIndices = _reactorPool.SpawnReactorsAtPositions(reactorId, positions, currentTick);
            return spawnedIndices.Count;
        }

        /// <summary>
        /// Spawn a reactor at a specific portal's position.
        /// </summary>
        /// <param name="portalName">Portal name (pn)</param>
        /// <param name="reactorId">Reactor ID to spawn</param>
        /// <returns>True if reactor was spawned</returns>
        public bool SpawnReactorAtPortal(string portalName, string reactorId)
        {
            if (_portalPool == null || _reactorPool == null)
                return false;

            var portal = _portalPool.GetPortalByName(portalName);
            if (portal?.PortalInstance == null)
                return false;

            int currentTick = Environment.TickCount;
            var positions = new List<(float x, float y)>
            {
                (portal.PortalInstance.X, portal.PortalInstance.Y)
            };

            var spawnedIndices = _reactorPool.SpawnReactorsAtPositions(reactorId, positions, currentTick);
            return spawnedIndices.Count > 0;
        }

        /// <summary>
        /// Check for reactor touch interactions near player.
        /// Call this from player movement updates.
        /// </summary>
        /// <param name="playerX">Player X position</param>
        /// <param name="playerY">Player Y position</param>
        /// <param name="playerId">Player ID</param>
        /// <returns>List of touched reactors</returns>
        public List<ReactorItem> CheckReactorTouch(float playerX, float playerY, int playerId = 0)
        {
            if (_reactorPool == null)
                return new List<ReactorItem>();

            var touchedReactors = _reactorPool.FindTouchReactorAroundLocalUser(playerX, playerY);
            int currentTick = Environment.TickCount;

            foreach (var (reactor, index) in touchedReactors)
            {
                _reactorPool.ActivateReactor(index, playerId, currentTick, ReactorActivationType.Touch);
            }

            return touchedReactors.Select(t => t.reactor).ToList();
        }

        /// <summary>
        /// Check for hidden portals near player and reveal them.
        /// </summary>
        /// <param name="playerX">Player X position</param>
        /// <param name="playerY">Player Y position</param>
        /// <returns>List of revealed hidden portals</returns>
        public List<PortalItem> CheckHiddenPortals(float playerX, float playerY)
        {
            if (_portalPool == null)
                return new List<PortalItem>();

            var hiddenPortals = _portalPool.FindPortal_Hidden(playerX, playerY);
            int currentTick = Environment.TickCount;

            foreach (var (portal, index) in hiddenPortals)
            {
                if (!_portalPool.IsHiddenPortalRevealed(index))
                {
                    _portalPool.SetHiddenPortal(index, false, currentTick); // Reveal
                }
            }

            return hiddenPortals.Select(t => t.portal).ToList();
        }

        /// <summary>
        /// Get portal properties for physics calculations.
        /// </summary>
        /// <param name="portalName">Portal name</param>
        /// <returns>Portal properties struct</returns>
        public PortalProperties GetPortalProperties(string portalName)
        {
            if (_portalPool == null)
                return default;

            return _portalPool.GetPortalProperties(portalName);
        }

        #endregion

        /// <summary>
        /// Loads meso animation frames from Item.wz/Special/0900.img
        /// </summary>
        private void LoadMesoIcons()
        {
            _mesoAnimFrames = new List<IDXObject>[] { new(), new(), new(), new() };

            var mesoImage = (Program.FindWzObject("Item", "Special") as WzObject)?["0900.img"] as WzImage;
            if (mesoImage == null) return;

            string[] mesoIds = { "09000000", "09000001", "09000002", "09000003" };
            for (int i = 0; i < mesoIds.Length; i++)
            {
                var iconRaw = (mesoImage[mesoIds[i]] as WzSubProperty)?["iconRaw"];
                if (iconRaw is WzSubProperty animFrames)
                    LoadAnimationFrames(animFrames, _mesoAnimFrames[i]);
                else if (iconRaw is WzCanvasProperty canvas)
                    LoadSingleFrame(canvas, _mesoAnimFrames[i]);
            }
        }

        private void LoadAnimationFrames(WzSubProperty container, List<IDXObject> frames)
        {
            for (int f = 0; f < 10; f++)
            {
                if (container[f.ToString()] is not WzCanvasProperty canvas) break;
                LoadSingleFrame(canvas, frames);
            }
        }

        private void LoadSingleFrame(WzCanvasProperty canvas, List<IDXObject> frames)
        {
            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null) return;

            var texture = bitmap.ToTexture2D(GraphicsDevice);
            if (texture == null) return;

            var origin = canvas["origin"] as WzVectorProperty;
            int ox = origin?.X?.Value ?? texture.Width / 2;
            int oy = origin?.Y?.Value ?? texture.Height;
            int delay = (canvas["delay"] as WzIntProperty)?.Value ?? 100;

            frames.Add(new DXObject(-ox, -oy, texture, delay));
        }

        /// <summary>
        /// Gets the appropriate meso animation frames based on amount
        /// </summary>
        private List<IDXObject> GetMesoFramesForAmount(int amount)
        {
            if (_mesoAnimFrames == null) return null;

            // Select meso icon based on amount thresholds
            int index;
            if (amount >= 10000)
                index = 3; // Bag (large boss drops)
            else if (amount >= 1000)
                index = 2; // Gold (large)
            else if (amount >= 100)
                index = 1; // Silver (medium)
            else
                index = 0; // Bronze (small)

            return _mesoAnimFrames[index];
        }

        /// <summary>
        /// Loads tombstone animation from Effect.wz/Tomb.img
        /// fall/0..19 - falling animation
        /// land/0 - final resting frame (UOL to fall/19)
        /// </summary>
        private void LoadTombstoneAnimation()
        {
            _tombFallFrames = new List<IDXObject>();
            _tombLandFrame = null;

            var tombImage = Program.FindImage("Effect", "Tomb.img");
            if (tombImage == null)
            {
                System.Diagnostics.Debug.WriteLine("[LoadTombstoneAnimation] Effect.wz/Tomb.img not found");
                return;
            }

            // Load fall animation frames (0..19)
            var fallProperty = tombImage["fall"] as WzSubProperty;
            if (fallProperty != null)
            {
                for (int i = 0; i < 20; i++)
                {
                    WzObject frameProperty = fallProperty[i.ToString()];
                    if (frameProperty == null) break;

                    // Resolve UOL if necessary
                    if (frameProperty is WzUOLProperty uol)
                    {
                        frameProperty = uol.LinkValue;
                    }

                    if (frameProperty is WzCanvasProperty canvas)
                    {
                        var bitmap = canvas.GetLinkedWzCanvasBitmap();
                        if (bitmap != null)
                        {
                            var texture = bitmap.ToTexture2D(GraphicsDevice);
                            if (texture != null)
                            {
                                var origin = canvas["origin"] as WzVectorProperty;
                                int ox = origin?.X?.Value ?? texture.Width / 2;
                                int oy = origin?.Y?.Value ?? texture.Height;
                                int delay = (canvas["delay"] as WzIntProperty)?.Value ?? 100;

                                _tombFallFrames.Add(new DXObject(-ox, -oy, texture, delay));
                            }
                        }
                    }
                }
            }

            // Load land frame (0 - typically UOL to fall/19)
            var landProperty = tombImage["land"] as WzSubProperty;
            if (landProperty != null)
            {
                WzObject landFrame = landProperty["0"];
                if (landFrame != null)
                {
                    // Resolve UOL if necessary
                    if (landFrame is WzUOLProperty uol)
                    {
                        landFrame = uol.LinkValue;
                    }

                    if (landFrame is WzCanvasProperty canvas)
                    {
                        var bitmap = canvas.GetLinkedWzCanvasBitmap();
                        if (bitmap != null)
                        {
                            var texture = bitmap.ToTexture2D(GraphicsDevice);
                            if (texture != null)
                            {
                                var origin = canvas["origin"] as WzVectorProperty;
                                int ox = origin?.X?.Value ?? texture.Width / 2;
                                int oy = origin?.Y?.Value ?? texture.Height;
                                int delay = 0; // Static frame

                                _tombLandFrame = new DXObject(-ox, -oy, texture, delay);
                            }
                        }
                    }
                }
            }

            // Fallback: if land frame not loaded but we have fall frames, use last fall frame
            if (_tombLandFrame == null && _tombFallFrames.Count > 0)
            {
                _tombLandFrame = _tombFallFrames[_tombFallFrames.Count - 1];
            }

            System.Diagnostics.Debug.WriteLine($"[LoadTombstoneAnimation] Loaded {_tombFallFrames.Count} fall frames, land frame: {(_tombLandFrame != null ? "yes" : "no")}");
        }

        /// <summary>
        /// Handles UP key press near portals for map teleportation.
        /// When the player presses UP while standing near a portal with a valid target map,
        /// triggers teleportation the same way as double-clicking the portal.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandlePortalUpInteract()
        {
            // Skip if map change is already pending
            if (_gameState.PendingMapChange)
                return;

            // Only handle when player control is enabled and player is active
            if (!_gameState.PlayerControlEnabled || _playerManager == null || !_playerManager.IsPlayerActive)
                return;

            // Check if UP key (Interact) was just pressed (tap, not hold)
            // Note: Input.SyncState() is called after map change to ensure held keys don't trigger this
            if (!_playerManager.Input.IsPressed(InputAction.Interact))
                return;

            // Get player position
            var playerPos = _playerManager.GetPlayerPosition();
            float playerX = playerPos.X;
            float playerY = playerPos.Y;

            // Portal interaction range (in pixels)
            const int PORTAL_INTERACT_RANGE_X = 40; // Horizontal range
            const int PORTAL_INTERACT_RANGE_Y = 60; // Vertical range (player hitbox height consideration)

            // First check visible portals
            PortalItem nearestPortal = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < _portalsArray.Length; i++)
            {
                PortalItem portal = _portalsArray[i];
                PortalInstance instance = portal.PortalInstance;

                // Skip portals without valid destinations
                if (instance.tm <= 0 || instance.tm == MapConstants.MaxMap)
                    continue;

                // Check if player is within range of portal
                float dx = Math.Abs(playerX - instance.X);
                float dy = Math.Abs(playerY - instance.Y);

                if (dx <= PORTAL_INTERACT_RANGE_X && dy <= PORTAL_INTERACT_RANGE_Y)
                {
                    float distance = dx + dy; // Manhattan distance for simplicity
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPortal = portal;
                    }
                }
            }

            // If a visible portal is found, teleport to it
            if (nearestPortal != null)
            {
                PlayPortalSE();
                _playerManager?.ForceStand();
                _gameState.PendingMapChange = true;
                _gameState.PendingMapId = nearestPortal.PortalInstance.tm;
                _gameState.PendingPortalName = nearestPortal.PortalInstance.tn;
                return;
            }

            // No visible portal found - check hidden portals from _mapBoard.BoardItems.Portals
            PortalInstance nearestHiddenPortal = null;
            nearestDistance = float.MaxValue;

            foreach (var portal in _mapBoard.BoardItems.Portals)
            {
                // Only consider portals with valid map destinations
                if (portal.tm <= 0 || portal.tm == MapConstants.MaxMap)
                    continue;

                // Check if player is within range of portal (use portal's range if available)
                int rangeX = portal.hRange ?? PORTAL_INTERACT_RANGE_X;
                int rangeY = portal.vRange ?? PORTAL_INTERACT_RANGE_Y;

                float dx = Math.Abs(playerX - portal.X);
                float dy = Math.Abs(playerY - portal.Y);

                if (dx <= rangeX && dy <= rangeY)
                {
                    float distance = dx + dy;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestHiddenPortal = portal;
                    }
                }
            }

            // If a hidden portal is found, teleport to it
            if (nearestHiddenPortal != null)
            {
                PlayPortalSE();
                _playerManager?.ForceStand();
                _gameState.PendingMapChange = true;
                _gameState.PendingMapId = nearestHiddenPortal.tm;
                _gameState.PendingPortalName = nearestHiddenPortal.tn;
            }
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

            // Initialize mob pool for spawn/despawn management
            _mobPool = new MobPool();
            _mobPool.Initialize(_mobsArray);

            // Initialize drop pool for item/meso drops
            _dropPool = new DropPool();
            _dropPool.Initialize();
            _dropPool.SetGroundLevelLookup((x, y) =>
            {
                // Ground level offset - the meso icon origin is typically at center,
                // so we need to move the drop position up so the icon's bottom is on the platform
                // Meso icons are ~20-24px tall, so move up by ~15px
                return y - 18;
            });

            // Set up pickup sound and notice callbacks
            _dropPool.SetOnDropPickedUp(drop =>
            {
                PlayPickUpItemSE();

                // Add pickup notice message
                int currentTime = Environment.TickCount;
                if (drop.Type == Pools.DropType.Meso)
                {
                    _pickupNoticeUI.AddMesoPickup(drop.MesoAmount, currentTime);
                }
                else if (drop.Type == Pools.DropType.Item || drop.Type == Pools.DropType.InstallItem)
                {
                    string itemName = !string.IsNullOrEmpty(drop.ItemId) ? $"Item #{drop.ItemId}" : "Unknown Item";
                    _pickupNoticeUI.AddItemPickup(itemName, drop.Quantity, currentTime);
                }
                else if (drop.Type == Pools.DropType.QuestItem)
                {
                    string itemName = !string.IsNullOrEmpty(drop.ItemId) ? $"Quest Item #{drop.ItemId}" : "Quest Item";
                    _pickupNoticeUI.AddQuestItemPickup(itemName, currentTime);
                }
            });

            // Set up death effect and drop spawn callbacks
            _mobPool.SetOnMobDied(mob =>
            {
                int currentTick = Environment.TickCount;
                _combatEffects.AddDeathEffectForMob(mob, currentTick);

                // Play drop item sound
                PlayDropItemSE();

                // Spawn drops from mob (demo: random meso amount)
                if (mob?.MovementInfo != null)
                {
                    float mobX = mob.MovementInfo.X;
                    float mobY = mob.MovementInfo.Y;
                    bool isBoss = mob.AI?.IsBoss ?? false;

                    // Spawn random meso drop (10-500 for normal, 1000-10000 for boss)
                    int mesoMin = isBoss ? 1000 : 10;
                    int mesoMax = isBoss ? 10000 : 500;
                    int mesoAmount = new Random().Next(mesoMin, mesoMax);

                    var mesoDrop = _dropPool.SpawnMesoDrop(mobX, mobY, mesoAmount, currentTick);
                    if (mesoDrop != null)
                    {
                        // Set the animation frames based on meso amount
                        var frames = GetMesoFramesForAmount(mesoAmount);
                        if (frames != null && frames.Count > 0)
                        {
                            mesoDrop.AnimFrames = frames;
                            mesoDrop.Icon = frames[0]; // First frame as default icon
                        }
                    }
                }
            });

            // Set up removal callback to null out mob from array
            _mobPool.SetOnMobRemoved(mob =>
            {
                // Remove mob from array by setting to null
                for (int i = 0; i < _mobsArray.Length; i++)
                {
                    if (_mobsArray[i] == mob)
                    {
                        _mobsArray[i] = null;
                        break;
                    }
                }

                // Also remove from combat effects
                _combatEffects.RemoveMobHPBar(mob.PoolId);
            });

            // Convert Reactors
            _reactorsArray = mapObjects_Reactors.Count > 0
                ? mapObjects_Reactors.Cast<ReactorItem>().ToArray()
                : Array.Empty<ReactorItem>();

            // Convert Portals
            _portalsArray = mapObjects_Portal.Count > 0
                ? mapObjects_Portal.Cast<PortalItem>().ToArray()
                : Array.Empty<PortalItem>();

            // Initialize portal pool for hidden portal support
            _portalPool = new PortalPool();
            _portalPool.Initialize(_portalsArray);
            _portalPool.SetOnHiddenPortalRevealed((portal, index) =>
            {
                System.Diagnostics.Debug.WriteLine($"[PortalPool] Hidden portal revealed: {portal.PortalInstance.pn}");
            });

            // Initialize reactor pool for reactor spawning and touch detection
            _reactorPool = new ReactorPool();
            _reactorPool.Initialize(_reactorsArray, _DxDeviceManager.GraphicsDevice);
            _reactorPool.SetOnReactorTouched((reactor, playerId) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ReactorPool] Reactor touched: {reactor.ReactorInstance.Name}");
            });
            _reactorPool.SetOnReactorActivated((reactor, playerId) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ReactorPool] Reactor activated: {reactor.ReactorInstance.Name}");
            });

            // Convert Tooltips
            _tooltipsArray = mapObjects_tooltips.Count > 0
                ? mapObjects_tooltips.Cast<TooltipItem>().ToArray()
                : Array.Empty<TooltipItem>();

            // Convert Backgrounds
            _backgroundsFrontArray = backgrounds_front.ToArray();
            _backgroundsBackArray = backgrounds_back.ToArray();

            // Set render arrays on RenderingManager
            _renderingManager.SetRenderArrays(
                _backgroundsBackArray,
                _backgroundsFrontArray,
                _mapObjectsArray,
                _mobsArray,
                _npcsArray,
                _portalsArray,
                _reactorsArray,
                _tooltipsArray);

            // Set pools on RenderingManager
            _renderingManager.SetPools(_dropPool, _playerManager);

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
            Rectangle mapBounds = _mapBoard.VRRectangle != null
                ? new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y,
                    _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height)
                : new Rectangle(-_mapBoard.CenterPoint.X, -_mapBoard.CenterPoint.Y,
                    _mapBoard.MapSize.X, _mapBoard.MapSize.Y);

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
            var footholds = _mapBoard.BoardItems.FootholdLines;

            // Use raw VR coordinates (without CenterPoint offset) since mob coordinates are in raw format
            Rectangle rawVR = _mapBoard.VRRectangle != null
                ? new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y, _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height)
                : new Rectangle(-_mapBoard.CenterPoint.X, -_mapBoard.CenterPoint.Y, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);

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
                    if (mobItem.MovementInfo.MoveType == MobMoveType.Move ||
                        mobItem.MovementInfo.MoveType == MobMoveType.Stand ||
                        mobItem.MovementInfo.MoveType == MobMoveType.Jump)
                    {
                        mobItem.MovementInfo.FindCurrentFoothold(footholds);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes the player character manager and spawns the player.
        /// </summary>
        private void InitializePlayerManager(float spawnX, float spawnY)
        {
            // Create player manager
            _playerManager = new PlayerManager(_DxDeviceManager.GraphicsDevice, _texturePool);

            // Try to get Character.wz for actual character graphics
            WzFile characterWz = null;
            WzFile skillWz = null;
            try
            {
                // Try WzFileManager.fileManager first (static singleton)
                var fileManager = WzFileManager.fileManager;
                Debug.WriteLine($"[Player] WzFileManager.fileManager exists: {fileManager != null}");

                if (fileManager != null)
                {
                    // Load Character.wz
                    var characterDir = fileManager["character"];
                    Debug.WriteLine($"[Player] Character directory: {characterDir?.Name ?? "NULL"}");
                    characterWz = characterDir?.WzFileParent;
                    Debug.WriteLine($"[Player] Character.wz: {characterWz?.Name ?? "NULL"}, HasDirectory: {characterWz?.WzDirectory != null}");

                    // Load Skill.wz
                    var skillDir = fileManager["skill"];
                    Debug.WriteLine($"[Player] Skill directory: {skillDir?.Name ?? "NULL"}");
                    skillWz = skillDir?.WzFileParent;
                    Debug.WriteLine($"[Player] Skill.wz: {skillWz?.Name ?? "NULL"}, HasDirectory: {skillWz?.WzDirectory != null}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Player] Could not load Character.wz/Skill.wz: {ex.Message}");
            }

            // Initialize with Character.wz and Skill.wz (or null for placeholder)
            _playerManager.Initialize(characterWz, skillWz);
            Debug.WriteLine($"[Player] PlayerManager initialized with Character.wz: {characterWz != null}, Skill.wz: {skillWz != null}");

            // Set spawn point
            _playerManager.SetSpawnPoint(spawnX, spawnY);

            // Connect mob and drop pools
            _playerManager.SetMobPool(_mobPool);
            _playerManager.SetDropPool(_dropPool);
            _playerManager.SetCombatEffects(_combatEffects);

            // Set up sound callbacks
            _playerManager.SetJumpSoundCallback(PlayJumpSE);

            // Set up foothold lookup callback
            var footholds = _mapBoard.BoardItems.FootholdLines;
            _playerManager.SetFootholdLookup((x, y, searchRange) =>
            {
                // Find foothold at position
                if (footholds == null || footholds.Count == 0)
                    return null;

                FootholdLine bestFh = null;
                float bestDist = float.MaxValue;

                // Allow finding footholds slightly above player (for walking transitions)
                // When walking between connected footholds, the next foothold might be
                // at a slightly higher Y position
                const float upwardTolerance = 10f;

                foreach (var fh in footholds)
                {
                    // Check if X is within foothold range
                    float fhMinX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                    float fhMaxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);

                    if (x < fhMinX || x > fhMaxX)
                        continue;

                    // Calculate Y at X position on this foothold
                    float dx = fh.SecondDot.X - fh.FirstDot.X;
                    float dy = fh.SecondDot.Y - fh.FirstDot.Y;
                    float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
                    float fhY = fh.FirstDot.Y + t * dy;

                    // Check if foothold is within range (below or slightly above player)
                    // dist > 0 means foothold is below, dist < 0 means foothold is above
                    float dist = fhY - y;
                    float absDist = Math.Abs(dist);

                    // Accept footholds below (within searchRange) or slightly above (within tolerance)
                    if ((dist >= 0 && dist < searchRange) || (dist < 0 && -dist <= upwardTolerance))
                    {
                        if (absDist < bestDist)
                        {
                            bestDist = absDist;
                            bestFh = fh;
                        }
                    }
                }

                return bestFh;
            });

            // Set up ladder lookup callback
            var ropes = _mapBoard.BoardItems.Ropes;
            _playerManager.SetLadderLookup((x, y, range) =>
            {
                if (ropes == null)
                    return null;

                foreach (var rope in ropes)
                {
                    float ropeX = rope.FirstAnchor.X;
                    float ropeTop = Math.Min(rope.FirstAnchor.Y, rope.SecondAnchor.Y);
                    float ropeBottom = Math.Max(rope.FirstAnchor.Y, rope.SecondAnchor.Y);

                    // Check if player is within horizontal range of rope/ladder
                    if (Math.Abs(x - ropeX) <= range)
                    {
                        // Check if player Y overlaps with rope/ladder
                        if (y >= ropeTop && y <= ropeBottom)
                        {
                            return ((int)ropeX, (int)ropeTop, (int)ropeBottom, rope.ladder);
                        }
                    }
                }
                return null;
            });

            // Set up swim area check callback
            // Check if entire map is swimmable (info.swim flag)
            bool isFullySwimmableMap = _mapBoard.MapInfo.swim;

            // Collect swim areas from MiscItems
            var swimAreas = _mapBoard.BoardItems.MiscItems
                .OfType<MapEditor.Instance.Misc.SwimArea>()
                .ToList();

            if (isFullySwimmableMap)
            {
                Debug.WriteLine($"[SwimArea] Map is fully swimmable (swim=true in map info)");
            }
            if (swimAreas.Count > 0)
            {
                Debug.WriteLine($"[SwimArea] Found {swimAreas.Count} swim areas in map");
                foreach (var area in swimAreas)
                {
                    Debug.WriteLine($"  - {area.Name}: ({area.X}, {area.Y}) {area.Width}x{area.Height}");
                }
            }

            _playerManager.SetSwimAreaCheck((x, y, range) =>
            {
                // If map has swim=true, entire map is swimmable (like Aqua Road underwater maps)
                if (isFullySwimmableMap)
                {
                    return true;
                }

                // Check if position is inside any swim area
                foreach (var area in swimAreas)
                {
                    if (x >= area.X && x <= area.X + area.Width &&
                        y >= area.Y && y <= area.Y + area.Height)
                    {
                        return true;
                    }
                }
                return false;
            });

            // Set up flying map flag (fly=true in map info allows free flying in entire map)
            bool isFlyingMap = _mapBoard.MapInfo.fly == true;
            if (isFlyingMap)
            {
                Debug.WriteLine($"[FlyingMap] Map allows flying (fly=true in map info)");
            }
            _playerManager.SetFlyingMap(isFlyingMap);

            // Create default player character
            if (!_playerManager.CreateDefaultPlayer())
            {
                // If default fails, try random
                if (!_playerManager.CreateRandomPlayer())
                {
                    // If random fails (no Character.wz), create placeholder
                    _playerManager.CreatePlaceholderPlayer();
                }
            }

            Debug.WriteLine($"Player spawned at ({spawnX}, {spawnY}), IsActive: {_playerManager.IsPlayerActive}");
        }

        /// <summary>
        /// Reconnects existing player to a new map.
        /// Preserves player character, appearance, skills, and stats.
        /// Only updates map-specific callbacks and position.
        /// </summary>
        private void ReconnectPlayerToMap(float spawnX, float spawnY)
        {
            Debug.WriteLine($"[Player] Reconnecting player to new map at ({spawnX}, {spawnY})");

            // Set spawn point for new map
            _playerManager.SetSpawnPoint(spawnX, spawnY);

            // Set up foothold lookup callback for new map
            var footholds = _mapBoard.BoardItems.FootholdLines;
            _playerManager.SetFootholdLookup((x, y, searchRange) =>
            {
                if (footholds == null || footholds.Count == 0)
                    return null;

                FootholdLine bestFh = null;
                float bestDist = float.MaxValue;
                const float upwardTolerance = 10f;

                foreach (var fh in footholds)
                {
                    float fhMinX = Math.Min(fh.FirstDot.X, fh.SecondDot.X);
                    float fhMaxX = Math.Max(fh.FirstDot.X, fh.SecondDot.X);

                    if (x < fhMinX || x > fhMaxX)
                        continue;

                    float dx = fh.SecondDot.X - fh.FirstDot.X;
                    float dy = fh.SecondDot.Y - fh.FirstDot.Y;
                    float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
                    float fhY = fh.FirstDot.Y + t * dy;

                    float dist = fhY - y;
                    float absDist = Math.Abs(dist);

                    if ((dist >= 0 && dist < searchRange) || (dist < 0 && -dist <= upwardTolerance))
                    {
                        if (absDist < bestDist)
                        {
                            bestDist = absDist;
                            bestFh = fh;
                        }
                    }
                }

                return bestFh;
            });

            // Set up ladder lookup callback for new map
            var ropes = _mapBoard.BoardItems.Ropes;
            _playerManager.SetLadderLookup((x, y, range) =>
            {
                if (ropes == null)
                    return null;

                foreach (var rope in ropes)
                {
                    float ropeX = rope.FirstAnchor.X;
                    float ropeTop = Math.Min(rope.FirstAnchor.Y, rope.SecondAnchor.Y);
                    float ropeBottom = Math.Max(rope.FirstAnchor.Y, rope.SecondAnchor.Y);

                    if (Math.Abs(x - ropeX) <= range)
                    {
                        if (y >= ropeTop && y <= ropeBottom)
                        {
                            return ((int)ropeX, (int)ropeTop, (int)ropeBottom, rope.ladder);
                        }
                    }
                }
                return null;
            });

            // Set up swim area check callback for new map
            bool isFullySwimmableMap = _mapBoard.MapInfo.swim;
            var swimAreas = _mapBoard.BoardItems.MiscItems
                .OfType<MapEditor.Instance.Misc.SwimArea>()
                .ToList();

            _playerManager.SetSwimAreaCheck((x, y, range) =>
            {
                if (isFullySwimmableMap)
                    return true;

                foreach (var area in swimAreas)
                {
                    if (x >= area.X && x <= area.X + area.Width &&
                        y >= area.Y && y <= area.Y + area.Height)
                    {
                        return true;
                    }
                }
                return false;
            });

            // Set up flying map flag
            bool isFlyingMap = _mapBoard.MapInfo.fly == true;
            _playerManager.SetFlyingMap(isFlyingMap);

            // Reconnect to new map's pools and effects
            _playerManager.ReconnectToMap(
                _playerManager.GetFootholdLookup(),
                _playerManager.GetLadderLookup(),
                _playerManager.GetSwimAreaCheck(),
                isFlyingMap,
                _mobPool,
                _dropPool,
                _combatEffects);

            // Teleport player to spawn position and snap to foothold
            // TeleportTo properly finds a foothold and lands the player on it
            _playerManager.SetSpawnPoint(spawnX, spawnY);
            _playerManager.TeleportTo(spawnX, spawnY);

            Debug.WriteLine($"[Player] Reconnected to new map, IsActive: {_playerManager.IsPlayerActive}");
        }

        /// <summary>
        /// Gets character stats data for display on the status bar.
        /// Uses player character data when available, or provides defaults.
        /// </summary>
        /// <returns>CharacterStatsData with current player stats</returns>
        private CharacterStatsData GetCharacterStatsData()
        {
            if (_playerManager?.Player == null)
            {
                return new CharacterStatsData(); // Default values
            }

            var player = _playerManager.Player;
            return new CharacterStatsData
            {
                HP = player.HP,
                MaxHP = player.MaxHP,
                MP = player.MP,
                MaxMP = player.MaxMP,
                Level = player.Level,
                Name = player.Build?.Name ?? "Player",
                Job = "Beginner", // Default job since CharacterBuild doesn't have job
                EXP = 0,         // EXP not tracked in current implementation
                MaxEXP = 100     // Default max EXP
            };
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

            MouseState mouseState = this._oldMouseState;
            int mouseXRelativeToMap = mouseState.X - mapShiftX;
            int mouseYRelativeToMap = mouseState.Y - mapShiftY;
            //System.Diagnostics.Debug.WriteLine("Mouse relative to map: X {0}, Y {1}", mouseXRelativeToMap, mouseYRelativeToMap);

            // The coordinates of the map's center point, obtained from _mapBoard.CenterPoint
            int mapCenterX = _mapBoard.CenterPoint.X;
            int mapCenterY = _mapBoard.CenterPoint.Y;

            // A Vector2 that calculates the offset between the map's current position (mapShiftX, mapShiftY) and its center point:
            // This shift vector is used in various Draw methods to properly position elements relative to the map's current view position.
            var shiftCenter = new Vector2(mapShiftX - mapCenterX, mapShiftY - mapCenterY);

            //GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 1.0f, 0); // Clear the window to black
            GraphicsDevice.Clear(Color.Black);

            // Apply screen effects (tremble offset) to the transformation matrix
            Matrix effectMatrix = this._matrixScale;
            if (_screenEffects.IsTrembleActive)
            {
                effectMatrix = Matrix.CreateTranslation(_screenEffects.TrembleOffsetX, _screenEffects.TrembleOffsetY, 0) * this._matrixScale;
            }

            _spriteBatch.Begin(
                SpriteSortMode.Immediate, // spine :( needs to be drawn immediately to maintain the layer orders
                                          //SpriteSortMode.Deferred,
                BlendState.NonPremultiplied,
                Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp, // Add proper sampling
                DepthStencilState.None,
                RasterizerState.CullCounterClockwise,
                null,
                effectMatrix);
            //_skeletonMeshRenderer.Begin();

            // Create render context for RenderingManager
            var renderContext = new Managers.RenderContext(
                _spriteBatch, _skeletonMeshRenderer, gameTime,
                mapShiftX, mapShiftY, mapCenterX, mapCenterY,
                _renderParams, TickCount, _debugBoundaryTexture);

            // World rendering via RenderingManager
            _renderingManager.DrawBackgrounds(in renderContext, false); // back background
            _renderingManager.DrawMapObjects(in renderContext); // tiles and objects
            _renderingManager.DrawMobs(in renderContext); // mobs - rendered behind portals
            DrawPlayer(gameTime, mapCenterX, mapCenterY, TickCount); // player character (has tombstone logic)
            _renderingManager.DrawDrops(in renderContext); // item/meso drops
            _renderingManager.DrawPortals(in renderContext); // portals
            _renderingManager.DrawReactors(in renderContext); // reactors
            _renderingManager.DrawNpcs(in renderContext); // NPCs - rendered on top
            _renderingManager.DrawTransportation(in renderContext); // ship/balrog
            _renderingManager.DrawBackgrounds(in renderContext, true); // front background

            // Borders
            _renderingManager.DrawVRFieldBorder(in renderContext);
            _renderingManager.DrawLBFieldBorder(in renderContext);

            // Debug overlays (separate pass - only runs when debug mode is on)
            _renderingManager.DrawDebugOverlays(in renderContext);

            // Screen effects (fade, flash, explosion, motion blur) and animation effects
            _renderingManager.DrawScreenEffects(in renderContext);

            // Limited view field (fog of war) - draws after world, before UI
            _renderingManager.DrawLimitedView(in renderContext);

            //////////////////// UI related here ////////////////////
            _renderingManager.DrawTooltips(in renderContext, mouseState); 

            // Status bar [layer below minimap]
            if (!_gameState.HideUIMode) {
                DrawUI(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, mouseState, TickCount); // status bar and minimap
            }

            if (gameTime.TotalGameTime.TotalSeconds < 5)
                _spriteBatch.DrawString(_fontNavigationKeysHelper,
                    _gameState.MobMovementEnabled ? _navHelpTextMobOn : _navHelpTextMobOff,
                    new Vector2(20, Height - 190), Color.White);
            
            if (!_screenshotManager.TakeScreenshot && _gameState.ShowDebugMode)
            {
                _debugStringBuilder.Clear();
                _debugStringBuilder.Append("FPS: ").Append(frameRate).Append('\n');
                _debugStringBuilder.Append("Cursor: X ").Append(mouseState.X).Append(", Y ").Append(mouseState.Y).Append('\n');
                _debugStringBuilder.Append("Relative cursor: X ").Append(mouseXRelativeToMap).Append(", Y ").Append(mouseYRelativeToMap);

                _spriteBatch.DrawString(_fontDebugValues, _debugStringBuilder,
                    new Vector2(Width - 270, 10), Color.White); // use the original width to render text
            }

            // Draw chat messages and input box
            if (!_gameState.HideUIMode)
            {
                _chat.Draw(_spriteBatch, TickCount);

                // Draw pickup notices (meso/item gain messages at bottom right)
                _pickupNoticeUI?.Draw(_spriteBatch);
            }

            // Draw portal fade overlay AFTER all UI elements (covers everything like official client)
            // This is separate from DrawScreenEffects which handles other effects drawn before UI
            _renderingManager.DrawPortalFadeOverlay(in renderContext);

            // Cursor [this is in front of everything else]
            mouseCursor.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                0, 0, 0, 0, // pos determined in the class
                null,
                _renderParams,
                TickCount);

            _spriteBatch.End();
            //_skeletonMeshRenderer.End();

            // Save screenshot if render is activated
            _screenshotManager.ProcessScreenshot(GraphicsDevice);


            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws the player character
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawPlayer(GameTime gameTime, int mapCenterX, int mapCenterY, int TickCount)
        {
            if (_playerManager == null || _playerManager.Player == null)
                return;

            var player = _playerManager.Player;

            // If player is dead, draw tombstone at death position
            if (!player.IsAlive)
            {
                // Initialize tombstone falling physics on first frame of death
                if (!_tombHasLanded && _tombAnimationStartTime == 0)
                {
                    _tombAnimationStartTime = Environment.TickCount;
                    _tombAnimationComplete = false;

                    // Find the actual ground position below the death location
                    float groundY = player.DeathY;
                    var findFoothold = _playerManager.GetFootholdLookup();
                    if (findFoothold != null)
                    {
                        // Search downward from death position to find ground (large search range for mid-air deaths)
                        var fh = findFoothold(player.DeathX, player.DeathY, 2000);
                        if (fh != null)
                        {
                            // Calculate Y position on the foothold at the death X coordinate
                            float x1 = fh.FirstDot.X;
                            float y1 = fh.FirstDot.Y;
                            float x2 = fh.SecondDot.X;
                            float y2 = fh.SecondDot.Y;

                            if (Math.Abs(x2 - x1) < 0.001f)
                            {
                                groundY = y1;
                            }
                            else
                            {
                                float t = (player.DeathX - x1) / (x2 - x1);
                                t = Math.Max(0, Math.Min(1, t)); // Clamp to [0,1]
                                groundY = y1 + t * (y2 - y1);
                            }
                        }
                    }

                    _tombTargetY = groundY;
                    _tombCurrentY = player.DeathY - TOMB_START_HEIGHT; // Start above death position
                    _tombVelocityY = 0;
                    _tombHasLanded = false;
                }

                // Calculate delta time for physics
                float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

                DrawTombstone(mapCenterX, mapCenterY, player.DeathX, deltaTime, Environment.TickCount);
                return;
            }
            else
            {
                // Reset tombstone state when player is alive (respawned)
                _tombAnimationStartTime = 0;
                _tombAnimationComplete = false;
                _tombHasLanded = false;
                _tombVelocityY = 0;
            }

            // Draw living player
            _playerManager.Draw(_spriteBatch, _skeletonMeshRenderer,
                mapShiftX, mapShiftY, mapCenterX, mapCenterY, TickCount);

            // Draw debug box around player (only in debug mode F5)
            if (_gameState.ShowDebugMode && _debugBoundaryTexture != null)
            {
                int screenX = (int)player.X - mapShiftX + mapCenterX;
                int screenY = (int)player.Y - mapShiftY + mapCenterY;
                int boxSize = 60;

                // Draw a visible debug rectangle around player position
                Rectangle debugRect = new Rectangle(screenX - boxSize / 2, screenY - boxSize, boxSize, boxSize);
                _spriteBatch.Draw(_debugBoundaryTexture, debugRect, Color.Lime * 0.5f);

                // Draw crosshair at exact position
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(screenX - 2, screenY - 10, 4, 20), Color.Red);
                _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(screenX - 10, screenY - 2, 20, 4), Color.Red);
            }
        }

        /// <summary>
        /// Draws a tombstone that falls from above and lands at the death position.
        /// Uses animation frames from Effect.wz/Tomb.img
        /// </summary>
        private void DrawTombstone(int mapCenterX, int mapCenterY, float worldX, float deltaTime, int currentTime)
        {
            // Update falling physics if not landed
            if (!_tombHasLanded)
            {
                // Apply gravity
                _tombVelocityY += TOMB_GRAVITY * deltaTime;

                // Update position
                _tombCurrentY += _tombVelocityY * deltaTime;

                // Check if landed
                if (_tombCurrentY >= _tombTargetY)
                {
                    _tombCurrentY = _tombTargetY;
                    _tombHasLanded = true;
                    _tombAnimationComplete = true; // Switch to land frame when hitting ground
                }
            }

            int screenX = (int)worldX - mapShiftX + mapCenterX;
            int screenY = (int)_tombCurrentY - mapShiftY + mapCenterY;

            // Use loaded tombstone animation if available
            if (_tombFallFrames != null && _tombFallFrames.Count > 0)
            {
                IDXObject frameToDraw = null;

                if (!_tombHasLanded)
                {
                    // While falling, cycle through fall animation frames based on time
                    int elapsedTime = currentTime - _tombAnimationStartTime;
                    int totalDuration = 0;

                    for (int i = 0; i < _tombFallFrames.Count; i++)
                    {
                        int frameDelay = _tombFallFrames[i].Delay > 0 ? _tombFallFrames[i].Delay : 100;
                        if (elapsedTime < totalDuration + frameDelay)
                        {
                            frameToDraw = _tombFallFrames[i];
                            break;
                        }
                        totalDuration += frameDelay;
                    }

                    // If animation finished but still falling, loop to last frame
                    if (frameToDraw == null && _tombFallFrames.Count > 0)
                    {
                        frameToDraw = _tombFallFrames[_tombFallFrames.Count - 1];
                    }
                }
                else
                {
                    // Landed - show land frame (static)
                    frameToDraw = _tombLandFrame ?? (_tombFallFrames.Count > 0 ? _tombFallFrames[_tombFallFrames.Count - 1] : null);
                }

                // Draw the frame - apply origin offset stored in DXObject (X, Y are negative origin values)
                if (frameToDraw != null)
                {
                    frameToDraw.DrawBackground(_spriteBatch, _skeletonMeshRenderer, null,
                        screenX + frameToDraw.X, screenY + frameToDraw.Y, Color.White, false, null);
                    return;
                }
            }

            // Fallback: draw simple tombstone shape if animation not loaded
            if (_debugBoundaryTexture == null)
                return;

            int tombWidth = 30;
            int tombHeight = 40;
            int tombTop = screenY - tombHeight;

            // Main tombstone body (gray)
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(screenX - tombWidth / 2, tombTop, tombWidth, tombHeight),
                Color.DarkGray);

            // Tombstone top (rounded - approximated with smaller rectangle)
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(screenX - tombWidth / 2 + 3, tombTop - 8, tombWidth - 6, 10),
                Color.DarkGray);

            // Cross on tombstone
            int crossWidth = 4;
            int crossHeight = 16;
            int crossX = screenX - crossWidth / 2;
            int crossY = tombTop + 8;

            // Vertical part of cross
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(crossX, crossY, crossWidth, crossHeight),
                Color.Black);

            // Horizontal part of cross
            _spriteBatch.Draw(_debugBoundaryTexture,
                new Rectangle(crossX - 4, crossY + 4, crossWidth + 8, crossWidth),
                Color.Black);
        }

        // NOTE: DrawDrops, DrawNpcs, DrawDebugOverlays moved to RenderingManager

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
                statusBarUi.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                            mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                            null,
                            _renderParams,
                            TickCount);

                statusBarUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);

                statusBarChatUI.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                            mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                            null,
                            _renderParams,
                            TickCount);
                statusBarChatUI.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
            }

            // Minimap
            if (miniMapUi != null)
            {
                // Update player position on minimap (uses actual character position, not viewport center)
                // MinimapPosition is the world coordinate that corresponds to minimap (0,0)
                if (_playerManager?.Player != null)
                {
                    miniMapUi.SetPlayerPosition(_playerManager.Player.X, _playerManager.Player.Y,
                        _mapBoard.MinimapPosition.X, _mapBoard.MinimapPosition.Y);
                }

                miniMapUi.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                        mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                        null,
                        _renderParams,
                TickCount);
            }

            // Boss HP bar (at top of screen)
            if (_combatEffects.HasActiveBossBar)
            {
                _combatEffects.DrawBossHPBar(_spriteBatch);
            }

            // UI Windows (Inventory, Equipment, Skills, Quest)
            // Toggle: I=Inventory, E=Equipment, S=Skills, Q=Quest
            // Handle mouse events for minimap and windows with proper priority
            // Windows are drawn ON TOP of minimap, so they get priority when starting a new drag
            // Once dragging starts, that element keeps exclusive control until mouse is released
            bool minimapIsDragging = miniMapUi != null && miniMapUi.IsDragging;
            bool windowIsDragging = uiWindowManager != null && uiWindowManager.IsDraggingWindow;

            if (uiWindowManager != null)
            {
                uiWindowManager.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                    null,
                    _renderParams,
                    TickCount);

                // Check UI windows - but not if minimap is ALREADY being dragged
                if (!minimapIsDragging)
                {
                    uiWindowManager.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                }
                else
                {
                    // Reset window drag states if minimap is being dragged
                    uiWindowManager.ResetAllDragStates();
                }
            }

            // Minimap mouse events
            if (miniMapUi != null)
            {
                // If minimap is already being dragged, continue dragging regardless of window positions
                if (minimapIsDragging)
                {
                    miniMapUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                }
                else
                {
                    // Not dragging yet - only start if no window is being dragged or contains the mouse point
                    // Windows have priority since they're drawn on top
                    bool windowBlocksMinimap = uiWindowManager != null &&
                        (uiWindowManager.IsDraggingWindow || uiWindowManager.ContainsPoint(mouseState.X, mouseState.Y));

                    if (!windowBlocksMinimap)
                    {
                        miniMapUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                    }
                }
            }
        }

        // NOTE: DrawTooltip, DrawVRFieldBorder, DrawLBFieldBorder, DrawBorder,
        // DrawScreenEffects, DrawPortalFadeOverlay, DrawExplosionRing, DrawThickLine,
        // DrawMotionBlurOverlay - All moved to RenderingManager
        #endregion

        // NOTE: Screenshot functionality moved to ScreenshotManager
        // NOTE: Screen effects drawing moved to RenderingManager

        #region Boundaries
        /// <summary>
        /// Clamp camera position to map boundaries without forcing centering.
        /// Used by smooth camera to preserve interpolated positions while staying in bounds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClampCameraToBoundaries()
        {
            // Calculate boundaries
            int minX = (int)(_vrFieldBoundary.Left * _renderParams.RenderObjectScaling);
            int maxX = (int)(_vrFieldBoundary.Right - (_renderParams.RenderWidth / _renderParams.RenderObjectScaling));
            int minY = (int)(_vrFieldBoundary.Top * _renderParams.RenderObjectScaling);
            int maxY = (int)(_vrFieldBoundary.Bottom - (_renderParams.RenderHeight / _renderParams.RenderObjectScaling));

            // Clamp X - for narrow maps, center instead
            int leftRightVRDifference = (int)((_vrFieldBoundary.Right - _vrFieldBoundary.Left) * _renderParams.RenderObjectScaling);
            if (leftRightVRDifference < _renderParams.RenderWidth)
            {
                // Map narrower than screen - center it
                mapShiftX = ((leftRightVRDifference / 2) + (int)(_vrFieldBoundary.Left * _renderParams.RenderObjectScaling)) - (_renderParams.RenderWidth / 2);
            }
            else
            {
                // Clamp to boundaries
                mapShiftX = Math.Max(minX, Math.Min(maxX, mapShiftX));
            }

            // Clamp Y - for short maps, center instead
            int topDownVRDifference = (int)((_vrFieldBoundary.Bottom - _vrFieldBoundary.Top) * _renderParams.RenderObjectScaling);
            if (topDownVRDifference < _renderParams.RenderHeight)
            {
                // Map shorter than screen - center it
                mapShiftY = ((topDownVRDifference / 2) + (int)(_vrFieldBoundary.Top * _renderParams.RenderObjectScaling)) - (_renderParams.RenderHeight / 2);
            }
            else
            {
                // Clamp to boundaries
                mapShiftY = Math.Max(minY, Math.Min(maxY, mapShiftY));
            }
        }

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
            int leftRightVRDifference = (int)((_vrFieldBoundary.Right - _vrFieldBoundary.Left) * _renderParams.RenderObjectScaling);

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
                this.mapShiftX = ((leftRightVRDifference / 2) + (int)(_vrFieldBoundary.Left * _renderParams.RenderObjectScaling)) - (_renderParams.RenderWidth / 2);
            }
            else  // If map is wider than screen width, allow scrolling with boundaries
            {
                // System.Diagnostics.Debug.WriteLine("[{4}] VR.Right {0}, Width {1}, Relative {2}. [Scaling {3}]", 
                //      vr.Right, RenderWidth, (int)(vr.Right - RenderWidth), (int)((vr.Right - (RenderWidth * RenderObjectScaling)) * RenderObjectScaling),
                //     mapShiftX + offset);

                if (bIsLeftKeyPressed)
                {
                    // Limit leftward movement to map's left boundary
                    this.mapShiftX = Math.Max((int)(_vrFieldBoundary.Left * _renderParams.RenderObjectScaling), mapShiftX - moveOffset);

                }
                else if (bIsRightKeyPressed)
                {
                    // Limit rightward movement to keep right boundary visible
                    // Accounts for screen width and scaling to prevent showing empty space
                    this.mapShiftX = Math.Min((int)((_vrFieldBoundary.Right - (_renderParams.RenderWidth / _renderParams.RenderObjectScaling))), mapShiftX + moveOffset);
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
            int topDownVRDifference = (int)((_vrFieldBoundary.Bottom - _vrFieldBoundary.Top) * _renderParams.RenderObjectScaling);
            if (topDownVRDifference < _renderParams.RenderHeight)
            {
                this.mapShiftY = ((topDownVRDifference / 2) + (int)(_vrFieldBoundary.Top * _renderParams.RenderObjectScaling)) - (_renderParams.RenderHeight / 2);
            }
            else
            {
                /*System.Diagnostics.Debug.WriteLine("[{0}] VR.Bottom {1}, Height {2}, Relative {3}. [Scaling {4}]",
                    (int)((vr.Bottom - (RenderHeight))),
                    vr.Bottom, RenderHeight, (int)(vr.Bottom - RenderHeight),
                    mapShiftX + offset);*/


                if (bIsUpKeyPressed)
                {
                    this.mapShiftY = Math.Max((int)(_vrFieldBoundary.Top), mapShiftY - moveOffset);
                }
                else if (bIsDownKeyPressed)
                {
                    this.mapShiftY = Math.Min((int)((_vrFieldBoundary.Bottom - (_renderParams.RenderHeight / _renderParams.RenderObjectScaling))), mapShiftY + moveOffset);
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
                    _gameState.PendingMapChange = true;
                    _gameState.PendingMapId = mapId;
                    _gameState.PendingPortalName = null;

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
                    _gameState.MobMovementEnabled = !_gameState.MobMovementEnabled;
                    return ChatCommandHandler.CommandResult.Ok($"Mob movement: {(_gameState.MobMovementEnabled ? "ON" : "OFF")}");
                });

            // /debug - Toggle debug mode
            _chat.CommandHandler.RegisterCommand(
                "debug",
                "Toggle debug overlay",
                "/debug",
                args =>
                {
                    _gameState.ShowDebugMode = !_gameState.ShowDebugMode;
                    return ChatCommandHandler.CommandResult.Ok($"Debug mode: {(_gameState.ShowDebugMode ? "ON" : "OFF")}");
                });

            // /hideui - Toggle UI visibility
            _chat.CommandHandler.RegisterCommand(
                "hideui",
                "Toggle UI visibility",
                "/hideui",
                args =>
                {
                    _gameState.HideUIMode = !_gameState.HideUIMode;
                    return ChatCommandHandler.CommandResult.Ok($"UI hidden: {(_gameState.HideUIMode ? "YES" : "NO")}");
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

    /// <summary>
    /// Weather types for the map simulator
    /// </summary>
    public enum WeatherType
    {
        None,
        Rain,
        Snow,
        Leaves
    }
}
