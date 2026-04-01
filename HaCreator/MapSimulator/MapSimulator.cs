using HaCreator.MapEditor;

using HaCreator.MapEditor.Info;

using HaCreator.MapEditor.Instance;

using HaCreator.MapEditor.Instance.Misc;

using HaCreator.MapEditor.Instance.Shapes;

using HaCreator.MapSimulator.AI;

using HaCreator.MapSimulator.UI;

using HaCreator.MapSimulator.Character;

using HaCreator.MapSimulator.Character.Skills;

using HaCreator.MapSimulator.Companions;

using HaCreator.MapSimulator.Interaction;

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

using MapleLib.WzLib.Util;

using MapleLib.WzLib.WzStructure.Data;

using MapleLib.WzLib.WzStructure.Data.ItemStructure;

using MapleLib.WzLib.WzStructure;

using Microsoft.Xna.Framework;

using Microsoft.Xna.Framework.Audio;

using Microsoft.Xna.Framework.Graphics;

using Microsoft.Xna.Framework.Input;

using Spine;

using System;

using System.Collections.Concurrent;

using System.Collections.Generic;

using System.Diagnostics;

using System.Drawing.Imaging;

using System.Globalization;

using System.IO;

using System.Linq;

using System.Runtime.CompilerServices;

using System.Text;

using System.Threading;

using System.Threading.Tasks;

using SD = System.Drawing;

using SDText = System.Drawing.Text;

using HaCreator.MapSimulator.Pools;

using HaCreator.MapSimulator.Effects;

using HaCreator.MapSimulator.Fields;

using HaCreator.MapSimulator.Managers;

using HaCreator.MapSimulator.Core;

using HaCreator.MapSimulator.Combat;

using MapleLib.Helpers;

using MapleLib.WzLib.WzStructure.Data.QuestStructure;



namespace HaCreator.MapSimulator

{

    /// <summary>

    /// 

    /// http://rbwhitaker.wikidot.com/xna-tutorials

    /// </summary>

    public partial class MapSimulator : Microsoft.Xna.Framework.Game

    {

        private const int DefaultLowHpWarningThresholdPercent = 20;

        private const int DefaultLowMpWarningThresholdPercent = 20;

        private const bool EnablePacketConnectionsByDefault = false;

        private const int ReactorCollisionCheckIntervalMs = 1000;

        private const int PetAutoSpeechPreLevelReminderCooldownMs = 420000;

        private const int PetAutoSpeechLowHpAlertCooldownMs = 60000;

        private const int PetAutoSpeechPotionFailureMaxRepeats = 3;

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



        private readonly WindowsImeCompositionMonitor _imeCompositionMonitor = new();

        private readonly TexturePool _texturePool = new TexturePool();

        private readonly ScreenshotManager _screenshotManager = new ScreenshotManager();

        private int _statusBarHpWarningThresholdPercent = DefaultLowHpWarningThresholdPercent;

        private int _statusBarMpWarningThresholdPercent = DefaultLowMpWarningThresholdPercent;



        private SpriteBatch _spriteBatch;
        private Texture2D _minimapTooltipPixelTexture;





        // Objects, NPCs (Lists for loading, arrays for iteration)

        public List<BaseDXDrawableItem>[] mapObjects;

        private readonly List<BaseDXDrawableItem> mapObjects_NPCs = new List<BaseDXDrawableItem>();

        private readonly List<BaseDXDrawableItem> mapObjects_Mobs = new List<BaseDXDrawableItem>();

        private readonly List<BaseDXDrawableItem> mapObjects_Reactors = new List<BaseDXDrawableItem>();

        private readonly List<BaseDXDrawableItem> mapObjects_Portal = new List<BaseDXDrawableItem>(); // perhaps mapobjects should be in a single pool

        private readonly List<BaseDXDrawableItem> mapObjects_tooltips = new List<BaseDXDrawableItem>();



        // Arrays for faster iteration (converted from Lists after loading)

        private BaseDXDrawableItem[][] _mapObjectsArray;

        private readonly Dictionary<BaseDXDrawableItem, QuestGatedMapObjectState> _questGatedMapObjects = new();

        private readonly Dictionary<string, bool> _authoredDynamicObjectTagStates = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<FieldObjectDirectionEventTriggerPoint> _dynamicObjectDirectionEventTriggers = new();

        private readonly HashSet<int> _triggeredDynamicObjectDirectionEventIndices = new();

        private readonly HashSet<int> _consumedFirstUserEnterMaps = new();

        private NpcItem[] _npcsArray;

        private readonly Dictionary<int, NpcItem> _npcsById = new();

        private MobItem[] _mobsArray;

        private ReactorItem[] _reactorsArray;

        private PortalItem[] _portalsArray;

        private TooltipItem[] _tooltipsArray;

        private readonly List<MobMovementInfo> _groundMobMovementBuffer = new();

        private readonly List<MobItem> _frameActiveMobs = new();

        private readonly List<MobItem> _frameMovableMobs = new();

        private readonly List<long> _expiredMobSkillEffectKeys = new();

        private MobItem _framePrimaryBossMob;



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

        private MobItem[] _visibleMobs;

        private int _visibleMobsCount;

        private NpcItem[] _visibleNpcs;

        private int _visibleNpcsCount;

        private bool[] _reactorVisibilityBuffer;



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

        private readonly List<StatusBarBuffRenderData> _statusBarBuffRenderCache = new();

        private int _statusBarBuffRenderCacheTime = int.MinValue;

        private readonly List<(StatusBarCooldownRenderData RenderData, int CooldownStartTime, int SortKey)> _statusBarCooldownSortBuffer = new();

        private readonly List<StatusBarCooldownRenderData> _statusBarCooldownRenderCache = new();

        private readonly HashSet<int> _statusBarProcessedCooldownSkills = new();

        private int _statusBarCooldownRenderCacheTime = int.MinValue;

        private readonly List<(StatusBarCooldownRenderData RenderData, int CooldownStartTime, int SortKey)> _statusBarOffBarCooldownSortBuffer = new();

        private readonly List<StatusBarCooldownRenderData> _statusBarOffBarCooldownRenderCache = new();

        private int _statusBarOffBarCooldownRenderCacheTime = int.MinValue;

        private readonly StatusBarPreparedSkillRenderData _preparedSkillStatusBarCache = new();

        private readonly StatusBarPreparedSkillRenderData _preparedSkillWorldCache = new();

        private int _preparedSkillStatusBarCacheTime = int.MinValue;

        private int _preparedSkillWorldCacheTime = int.MinValue;



        // Audio

        private MonoGameBgmPlayer _audio;

        private SoundManager _soundManager; // Manages sound effects with concurrent playback support

        private string _mapBgmName = null;

        private string _currentBgmName = null; // Track current BGM to avoid reloading same BGM on map change

        private string _specialFieldBgmOverrideName = null;

        private bool _isBgmPausedForFocusLoss = false;

        private bool _utilityBgmMuted = false;

        private bool _utilityEffectsMuted = false;

        private bool _pauseAudioOnFocusLoss = true;


        // Etc

        private Board _mapBoard; // Not readonly - can be replaced during seamless map transitions

        // Map type flags moved to _gameState (IsLoginMap, IsCashShopMap, IsBigBangUpdate, IsBigBang2Update) 



        // Spine

        private SkeletonMeshRenderer _skeletonMeshRenderer;



        // Text

        private SpriteFont _fontNavigationKeysHelper;

        private SpriteFont _fontDebugValues;

        private SpriteFont _fontChat;

        private readonly Dictionary<string, Texture2D> _chatFallbackTextureCache = new(StringComparer.Ordinal);

        private readonly SD.Bitmap _chatFallbackMeasureBitmap = new(1, 1);

        private readonly SD.Graphics _chatFallbackMeasureGraphics;

        private readonly SD.Font _chatFallbackFont;

        private readonly float _chatFallbackLineHeight;



        // Chat system

        private readonly MapSimulatorChat _chat = new MapSimulatorChat();



        // Pickup notice UI (displays meso/item pickup messages at bottom right)

        private readonly PickupNoticeUI _pickupNoticeUI = new PickupNoticeUI();

        private readonly SkillCooldownNoticeUI _skillCooldownNoticeUI = new SkillCooldownNoticeUI();
        private readonly PacketOwnedHudNoticeUI _packetOwnedHudNoticeUI = new PacketOwnedHudNoticeUI();
        private NpcInteractionOverlay _npcInteractionOverlay;

        private readonly QuestRuntimeManager _questRuntime = new QuestRuntimeManager();

        private readonly MemoMailboxManager _memoMailbox = new MemoMailboxManager();

        private readonly FamilyChartRuntime _familyChartRuntime = new FamilyChartRuntime();

        private readonly SocialListRuntime _socialListRuntime = new SocialListRuntime();

        private readonly GuildSkillRuntime _guildSkillRuntime = new GuildSkillRuntime();

        private readonly RemoteUserActorPool _remoteUserPool = new();

        private readonly SummonedPool _summonedPool = new();

        private readonly MessengerRuntime _messengerRuntime = new MessengerRuntime();

        private readonly GuildBbsRuntime _guildBbsRuntime = new GuildBbsRuntime();

        private readonly MapleTvRuntime _mapleTvRuntime = new MapleTvRuntime();

        private PendingRepairDurabilityRequest _pendingRepairDurabilityRequest;

        private readonly FieldMessageBoxRuntime _fieldMessageBoxRuntime = new FieldMessageBoxRuntime();
        private readonly PacketFieldStateRuntime _packetFieldStateRuntime = new PacketFieldStateRuntime();
        private readonly PacketScriptMessageRuntime _packetScriptMessageRuntime = new PacketScriptMessageRuntime();
        private readonly Managers.LocalOverlayRuntime _localOverlayRuntime = new();
        private readonly Dictionary<int, int> _questGrantedSkillPointsByTab = new();

        private bool _questUiBindingsConfigured;

        private int _activeQuestDetailQuestId;

        private int _activeMemoAttachmentId = -1;

        private NpcItem _activeNpcInteractionNpc;

        private int _activeNpcInteractionNpcId;

        private Texture2D _npcQuestAvailableIcon;

        private Texture2D _npcQuestInProgressIcon;

        private Texture2D _npcQuestCompletableIcon;

        private bool _npcQuestAlertIconsLoaded;

        private readonly NpcFeedbackBalloonQueue _npcQuestFeedback = new();

        private readonly Random _npcIdleSpeechRandom = new();

        private int _nextNpcIdleSpeechTick;

        private readonly Random _petIdleSpeechRandom = new();

        private int _lastObservedPetSpeechLevel = -1;

        private int _nextPetPreLevelSpeechTick;

        private int _lastPetHpAlertTick = int.MinValue;

        private bool _petHpAlertArmed = true;

        private int _petHpPotionFailureSpeechCount;

        private int _petMpPotionFailureSpeechCount;

        private const string FieldHazardNoHpPotionNoticeText = "Your pet could not find an HP potion to use.";

        private int _lastReactorCollisionCheckTick = -ReactorCollisionCheckIntervalMs;

        private readonly Dictionary<(int skillId, int level), MobSummonSkillInfo> _mobSummonSkillCache = new();

        private readonly Dictionary<(int skillId, int level), MobSkillRuntimeData> _mobSkillRuntimeCache = new();

        private readonly Dictionary<long, int> _appliedMobSkillEffects = new();

        private readonly Func<int, int, ReflectionDrawableBoundary> _mobMirrorBoundaryResolver;

        private readonly Func<int, int, ReflectionDrawableBoundary> _npcMirrorBoundaryResolver;

        private readonly Random _mobSkillRandom = new Random();



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

        private readonly PassengerSyncController _passengerSync = new PassengerSyncController();
        private readonly TransportationPacketInboxManager _transportPacketInbox = new TransportationPacketInboxManager();

        private readonly EscortFollowController _escortFollow = new EscortFollowController();

        private readonly LimitedViewField _limitedViewField = new LimitedViewField();

        private bool _limitedViewFieldInitialized;

        private AffectedAreaPool _affectedAreaPool;
        private readonly RemoteAffectedAreaPacketRuntime _remoteAffectedAreaPacketRuntime = new();

        private TemporaryPortalField _temporaryPortalField;
        private FieldRuleRuntime _fieldRuleRuntime;

        private readonly SpecialFieldRuntimeCoordinator _specialFieldRuntime = new SpecialFieldRuntimeCoordinator();

        private readonly WeddingPacketInboxManager _weddingPacketInbox = new WeddingPacketInboxManager();

        private readonly CoconutPacketInboxManager _coconutPacketInbox = new CoconutPacketInboxManager();
        private readonly CoconutOfficialSessionBridgeManager _coconutOfficialSessionBridge = new CoconutOfficialSessionBridgeManager();

        private readonly MemoryGamePacketInboxManager _memoryGamePacketInbox = new MemoryGamePacketInboxManager();
        private readonly MemoryGameOfficialSessionBridgeManager _memoryGameOfficialSessionBridge = new MemoryGameOfficialSessionBridgeManager();
        private readonly SocialRoomEmployeeActorRuntime _socialRoomEmployeeActor = new SocialRoomEmployeeActorRuntime();
        private readonly AriantArenaPacketInboxManager _ariantArenaPacketInbox = new AriantArenaPacketInboxManager();

        private readonly MonsterCarnivalPacketInboxManager _monsterCarnivalPacketInbox = new MonsterCarnivalPacketInboxManager();
        private readonly MonsterCarnivalOfficialSessionBridgeManager _monsterCarnivalOfficialSessionBridge = new MonsterCarnivalOfficialSessionBridgeManager();
        private readonly Dictionary<int, int> _monsterCarnivalGuardianSlotToReactorIndex = new();
        private readonly Dictionary<int, int> _monsterCarnivalGuardianReactorIndexToSlot = new();

        private readonly GuildBossPacketTransportManager _guildBossTransport = new GuildBossPacketTransportManager();
        private readonly GuildBossOfficialSessionBridgeManager _guildBossOfficialSessionBridge = new GuildBossOfficialSessionBridgeManager();

        private readonly MassacrePacketInboxManager _massacrePacketInbox = new MassacrePacketInboxManager();

        private readonly DojoPacketInboxManager _dojoPacketInbox = new DojoPacketInboxManager();

        private readonly PartyRaidPacketInboxManager _partyRaidPacketInbox = new PartyRaidPacketInboxManager();

        private readonly CookieHousePointInboxManager _cookieHousePointInbox = new CookieHousePointInboxManager();

        private int _cookieHouseContextPoint;

        private bool _bossHpBarAssetsLoaded;

        private static readonly EquipSlot[] BattlefieldAppearanceSlots =

        {

            EquipSlot.Cap,

            EquipSlot.Coat,

            EquipSlot.Longcoat,

            EquipSlot.Pants,

            EquipSlot.Shoes,

            EquipSlot.Glove,

            EquipSlot.Cape,

        };

        private Dictionary<EquipSlot, CharacterPart> _battlefieldOriginalEquipment;

        private float? _battlefieldOriginalSpeed;

        private int? _battlefieldAppliedTeamId;

        private int? _battlefieldAppliedMinimapTeamId;



        // Camera controller for smooth scrolling and zoom

        private readonly CameraController _cameraController = new CameraController();



        // Centralized game state management

        private readonly GameStateManager _gameState = new GameStateManager();

        private readonly DirectionModeWindowOwnerRegistry _scriptedDirectionModeWindows = new DirectionModeWindowOwnerRegistry();

        private readonly LoginRuntimeManager _loginRuntime = new LoginRuntimeManager();

        private readonly LoginPacketInboxManager _loginPacketInbox = new LoginPacketInboxManager();
        private readonly LoginOfficialSessionBridgeManager _loginOfficialSessionBridge = new LoginOfficialSessionBridgeManager();

        private bool _loginPacketInboxEnabled = EnablePacketConnectionsByDefault;

        private int _loginPacketInboxConfiguredPort = LoginPacketInboxManager.DefaultPort;
        private bool _loginOfficialSessionBridgeEnabled;
        private bool _loginOfficialSessionBridgeUseDiscovery;
        private int _loginOfficialSessionBridgeConfiguredListenPort = LoginOfficialSessionBridgeManager.DefaultListenPort;
        private string _loginOfficialSessionBridgeConfiguredRemoteHost = "127.0.0.1";
        private int _loginOfficialSessionBridgeConfiguredRemotePort;
        private string _loginOfficialSessionBridgeConfiguredProcessSelector;
        private int? _loginOfficialSessionBridgeConfiguredLocalPort;
        private const int LoginOfficialSessionBridgeDiscoveryRefreshIntervalMs = 2000;
        private int _nextLoginOfficialSessionBridgeDiscoveryRefreshAt;



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

        private const float TOMB_GRAVITY = 1200f; // Gravity acceleration (px/s驛｢譎｢・ｽ・ｻ驛｢・ｧ隰・∞・ｽ・ｽ繝ｻ・ｽ郢晢ｽｻ繝ｻ・ｲ)

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

        private PortalFadeState _portalFadeState = PortalFadeState.None;



        // Same-map portal teleport delay (no fade, just delay before teleport)

        // Default delay is 1000ms (1 second) if portal doesn't specify its own delay

        private const int SAME_MAP_PORTAL_DEFAULT_DELAY_MS = 1000;

        private const int SAME_MAP_TELEPORT_Y_OFFSET = 10;

        private const int DIRECTION_MODE_RELEASE_DELAY_MS = 300;

        private const int PASSIVE_TRANSFER_REQUEST_DURATION_MS = 1200;

        private const int FIELD_RULE_DAMAGE_MIST_DURATION_MS = 650;

        private const int FIELD_RULE_MESSAGE_COOLDOWN_MS = 1200;

        private const int SKILL_COOLDOWN_BLOCKED_MESSAGE_COOLDOWN_MS = 600;

        private const int REMOTE_PLAYER_PICKUP_INTERVAL_MS = 220;
        private const int REMOTE_PET_PICKUP_INTERVAL_MS = 260;

        private const int PICKUP_REMOTE_NOTICE_SUPPRESSION_MS = 900;

        private const string SkillCooldownNoticeSoundKey = "SkillCooldownNotice";

        private const int DefaultSimulatorWorldId = 0;

        private const int DefaultSimulatorChannelIndex = 0;

        private const int DefaultSimulatorChannelCount = 20;

        private const int DefaultSimulatorChannelCapacity = 1000;

        private const int DefaultLoginCharacterFieldMapId = 100000000;

        private const int LoginWorldSelectionRequestDelayMs = 450;

        private const int ChannelChangeRequestDelayMs = 700;

        private const int LoginWorldPopulationUpdateIntervalMs = 2200;

        private const string WeddingDirectionModeOwnerName = "__SpecialFieldWeddingDialog";

        private const string MemoryGameDirectionModeOwnerName = "__SpecialFieldMemoryGame";

        private bool _sameMapTeleportPending = false;

        private int _sameMapTeleportStartTime = 0;

        private int _sameMapTeleportDelay = 0;

        private SameMapTeleportTarget _sameMapTeleportTarget = null;

        private PendingMapSpawnTarget _pendingMapSpawnTarget = null;

        private bool _scriptedDirectionModeOwnerActive = false;

        private bool _passiveTransferRequestPending = false;

        private int _passiveTransferRequestExpiresAt = int.MinValue;

        private int _lastFieldRestrictionMessageTime = int.MinValue;

        private string _lastFieldRestrictionMessage = null;

        private readonly Dictionary<int, int> _lastSkillCooldownBlockedMessageTimes = new();

        private readonly Dictionary<int, int> _lastRemotePlayerPickupTimes = new();
        private readonly Dictionary<long, int> _lastRemotePetPickupTimes = new();

        private readonly Dictionary<long, int> _recentPickupRemoteNoticeTimes = new();

        private int _simulatorWorldId = DefaultSimulatorWorldId;

        private int _simulatorChannelIndex = DefaultSimulatorChannelIndex;

        private int _selectorBrowseWorldId = DefaultSimulatorWorldId;

        private SelectorRequestKind _selectorRequestKind = SelectorRequestKind.None;

        private int _selectorRequestWorldId = DefaultSimulatorWorldId;

        private int _selectorRequestChannelIndex = DefaultSimulatorChannelIndex;

        private int _selectorRequestStartedAt = int.MinValue;

        private int _selectorRequestDurationMs;

        private int _selectorRequestCompletesAt = int.MinValue;

        private string _selectorRequestStatusMessage = null;

        private readonly Dictionary<int, List<ChannelSelectionState>> _simulatorChannelStatesByWorld = new();

        private readonly Dictionary<int, LoginWorldSelectorMetadata> _loginWorldMetadataByWorld = new();

        private readonly HashSet<int> _loginRecommendedWorldIds = new();

        private SelectorRequestResultCode _selectorLastResultCode = SelectorRequestResultCode.None;

        private string _selectorLastResultMessage;

        private int _nextLoginWorldPopulationUpdateAt = int.MinValue;

        private bool _loginAccountIsAdult;

        private int? _loginLatestConnectedWorldId;

        private readonly Dictionary<int, LoginWorldInfoPacketProfile> _loginWorldInfoPacketProfiles = new();

        private readonly HashSet<int> _loginPacketRecommendedWorldIds = new();

        private readonly Dictionary<int, string> _loginPacketRecommendedWorldMessages = new();

        private readonly List<int> _loginPacketRecommendedWorldOrder = new();

        private int? _loginPacketLatestConnectedWorldId;

        private byte? _loginPacketCheckUserLimitResultCode;

        private byte? _loginPacketCheckUserLimitPopulationLevel;

        private byte? _loginPacketSelectWorldResultCode;

        private int? _loginPacketSelectWorldTargetWorldId;

        private int? _loginPacketSelectWorldTargetChannelIndex;

        private LoginSelectWorldResultProfile _loginPacketSelectWorldResultProfile;

        private LoginViewAllCharResultPacketProfile _loginPacketViewAllCharResultProfile;

        private LoginSelectWorldResultProfile _loginPacketViewAllCharRosterProfile;

        private int _loginPacketViewAllCharRemainingServerCount;

        private int _loginPacketViewAllCharExpectedCharacterCount;

        private readonly List<LoginSelectWorldCharacterEntry> _loginPacketViewAllCharEntries = new();

        private LoginCreateNewCharacterResultProfile _loginPacketCreateNewCharacterResultProfile;
        private LoginSelectCharacterResultProfile _loginPacketSelectCharacterResultProfile;
        private LoginSelectCharacterByVacResultProfile _loginPacketSelectCharacterByVacResultProfile;
        private LoginCheckPasswordResultProfile _loginPacketCheckPasswordResultProfile;
        private LoginGuestIdLoginResultProfile _loginPacketGuestIdLoginResultProfile;

        private LoginExtraCharInfoResultProfile _loginPacketExtraCharInfoResultProfile;

        private bool _loginCanHaveExtraCharacter;

        private readonly Dictionary<LoginPacketType, LoginAccountDialogPacketProfile> _loginPacketAccountDialogProfiles = new();



        private readonly Dictionary<LoginPacketType, LoginPacketDialogPromptConfiguration> _loginPacketDialogPrompts = new();

        private readonly List<RecommendWorldEntry> _recommendWorldEntries = new();

        private int _recommendWorldIndex;

        private bool _recommendWorldDismissed;

        private LoginStep _lastLoginStep = LoginStep.Title;

        private readonly MapTransferDestinationStore _mapTransferDestinations;
        private readonly MapTransferRuntimeManager _mapTransferRuntime;
        private readonly SkillMacroStore _skillMacroStore;
        private readonly QuestAlarmStore _questAlarmStore;
        private readonly ItemMakerProgressionStore _itemMakerProgressionStore;
        private readonly MonsterBookManager _monsterBookManager;
        private readonly LoginCharacterAccountStore _loginCharacterAccountStore;
        private readonly SocialRoomPersistenceStore _socialRoomPersistenceStore;
        private readonly Dictionary<int, string> _mapTransferTitleCache = new();
        private WorldMapRequestMode _worldMapRequestMode = WorldMapRequestMode.DirectTransfer;

        private MapTransferUI.DestinationEntry _mapTransferManualDestination;

        private MapTransferUI.DestinationEntry _mapTransferEditDestination;

        private readonly LoginCharacterRosterManager _loginCharacterRoster = new();

        private string _loginTitleAccountName = "explorergm";

        private string _loginTitlePassword = "maplesim";

        private bool _loginTitleRememberId = true;

        private string _loginTitleStatusMessage = "Enter credentials or let the login packet inbox feed the bootstrap runtime.";

        private string _loginCharacterStatusMessage = "Dispatch SelectWorldResult to populate the character roster.";

        private string _loginPendingCreateCharacterName;

        private LoginCreateCharacterFlowState _loginCreateCharacterFlow;

        private LoginUtilityDialogAction _loginUtilityDialogAction;

        private string _loginUtilityDialogTitle = "Login Utility";

        private string _loginUtilityDialogBody = string.Empty;

        private string _loginUtilityDialogPrimaryLabel = "OK";

        private string _loginUtilityDialogSecondaryLabel = "Cancel";

        private string _loginUtilityDialogInputLabel = string.Empty;

        private string _loginUtilityDialogInputPlaceholder = string.Empty;

        private string _loginUtilityDialogInputValue = string.Empty;

        private SoftKeyboardKeyboardType _loginUtilityDialogSoftKeyboardType = SoftKeyboardKeyboardType.AlphaNumeric;

        private LoginUtilityDialogButtonLayout _loginUtilityDialogButtonLayout = LoginUtilityDialogButtonLayout.Ok;

        private int _loginUtilityDialogTargetIndex = -1;

        private int? _loginUtilityDialogNoticeTextIndex;

        private bool _loginUtilityDialogInputMasked;

        private int _loginUtilityDialogInputMaxLength;

        private bool _loginAccountAcceptedEula;

        private string _loginAccountPicCode = string.Empty;

        private string _loginAccountBirthDate = string.Empty;

        private bool _loginAccountSpwEnabled;

        private string _loginAccountSecondaryPassword = string.Empty;

        private long _loginAccountCashShopNxCredit = DefaultCashShopNxCredit;

        private bool _loginAccountMigrationAccepted;

        private string _activeConnectionNoticeTitle = "Connection Notice";

        private string _activeConnectionNoticeBody = string.Empty;

        private const int ClientPicEditMaxLength = 8;

        private const long DefaultCashShopNxCredit = 10000L;

        private ConnectionNoticeWindowVariant _activeConnectionNoticeVariant = ConnectionNoticeWindowVariant.Notice;

        private int? _activeConnectionNoticeTextIndex;

        private bool _activeConnectionNoticeShowProgress;

        private float _activeConnectionNoticeProgress;

        private int _activeConnectionNoticeExpiresAt = int.MinValue;



        // Seamless map transition support (state managed by _gameState)

        private Func<int, Tuple<Board, string>> _loadMapCallback = null; // Callback to load new map



        /// <summary>

        /// Sets the callback used to load maps for portal teleportation.

        /// </summary>

        public void SetLoadMapCallback(Func<int, Tuple<Board, string>> callback)

        {

            _loadMapCallback = callback;

        }



        private void RefreshMapTransferWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapTransfer) is not MapTransferUI mapTransferWindow)

            {

                return;

            }



            mapTransferWindow.RegisterCurrentMapRequested = RegisterCurrentMapTransferDestination;

            mapTransferWindow.DeleteDestinationRequested = DeleteMapTransferDestination;

            mapTransferWindow.MoveDestinationRequested = MoveToMapTransferDestination;

            mapTransferWindow.WorldMapRequested = HandleMapTransferWorldMapRequested;

            mapTransferWindow.ManualMapMoveRequested = MoveToManualMapTransferDestination;

            mapTransferWindow.SetCurrentMapName(GetCurrentMapTransferDisplayName());

            mapTransferWindow.SetStatusMessage(GetMapTransferStatusMessage());

            mapTransferWindow.SetDestinations(BuildMapTransferDestinations());

        }



        private void HandleMapTransferWorldMapRequested(MapTransferUI.DestinationEntry destination)

        {

            _worldMapRequestMode = WorldMapRequestMode.MapTransferTargetSelection;

            _mapTransferEditDestination = destination?.IsSavedSlot == true ? destination : null;



            int focusedMapId = _mapTransferEditDestination?.MapId

                ?? _mapTransferManualDestination?.MapId

                ?? destination?.MapId

                ?? (_mapBoard?.MapInfo?.id ?? 0);

            RefreshWorldMapWindow(focusedMapId);

            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.WorldMap);

        }



        private void RefreshWorldMapWindow(int? focusedMapId = null)

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldMap) is not WorldMapUI worldMapWindow)

            {

                return;

            }



            worldMapWindow.MapRequested -= HandleWorldMapRequested;

            worldMapWindow.MapRequested += HandleWorldMapRequested;

            worldMapWindow.SetEntries(BuildWorldMapEntries(), _mapBoard?.MapInfo?.id ?? 0, focusedMapId);

            worldMapWindow.SetSearchResults(BuildWorldMapSearchResults(focusedMapId));

            worldMapWindow.SetQuestOverlays(BuildWorldMapQuestOverlays(focusedMapId));

        }



        private List<WorldMapUI.MapEntry> BuildWorldMapEntries()

        {

            List<WorldMapUI.MapEntry> entries = new();

            if (Program.InfoManager?.MapsNameCache == null)

            {

                return entries;

            }



            foreach (KeyValuePair<string, Tuple<string, string, string>> pair in Program.InfoManager.MapsNameCache)

            {

                if (!int.TryParse(pair.Key, out int mapId) || mapId <= 0 || mapId == MapConstants.MaxMap)

                {

                    continue;

                }



                Tuple<string, string, string> info = pair.Value;

                entries.Add(new WorldMapUI.MapEntry

                {

                    MapId = mapId,

                    StreetName = info?.Item1 ?? string.Empty,

                    MapName = info?.Item2 ?? string.Empty,

                    CategoryName = info?.Item3 ?? string.Empty,

                    RegionCode = WorldMapUI.GetRegionCodeForMapId(mapId)

                });

            }



            return entries;

        }



        private List<WorldMapUI.SearchResultEntry> BuildWorldMapSearchResults(int? focusedMapId)

        {

            List<WorldMapUI.SearchResultEntry> results = new();

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            int currentMapId = _mapBoard?.MapInfo?.id ?? 0;

            int selectedMapId = focusedMapId.GetValueOrDefault(currentMapId);



            AddWorldMapSearchFieldResult(results, seen, selectedMapId, "Selected field");

            if (currentMapId > 0 && currentMapId != selectedMapId)

            {

                AddWorldMapSearchFieldResult(results, seen, currentMapId, "Current field");

            }



            if (_npcsArray != null)

            {

                foreach (NpcItem npc in _npcsArray)

                {

                    string npcName = npc?.NpcInstance?.NpcInfo?.StringName;

                    if (string.IsNullOrWhiteSpace(npcName))

                    {

                        continue;

                    }



                    if (!seen.Add($"npc:{npcName.Trim()}"))

                    {

                        continue;

                    }



                    results.Add(new WorldMapUI.SearchResultEntry

                    {

                        Kind = WorldMapUI.SearchResultKind.Npc,

                        MapId = currentMapId,

                        Label = npcName.Trim(),

                        Description = "NPC in the currently loaded field"

                    });

                }

            }



            if (_mobsArray != null)

            {

                foreach (MobItem mob in _mobsArray)

                {

                    string mobName = mob?.MobInstance?.MobInfo?.Name;

                    if (string.IsNullOrWhiteSpace(mobName))

                    {

                        continue;

                    }



                    if (!seen.Add($"mob:{mobName.Trim()}"))

                    {

                        continue;

                    }



                    results.Add(new WorldMapUI.SearchResultEntry

                    {

                        Kind = WorldMapUI.SearchResultKind.Mob,

                        MapId = currentMapId,

                        Label = mobName.Trim(),

                        Description = "Mob in the currently loaded field"

                    });

                }

            }



            AppendPacketQuestGuideSearchResults(results, seen);
            AppendQuestDemandItemSearchResults(results, seen);



            return results;

        }



        private void AddWorldMapSearchFieldResult(List<WorldMapUI.SearchResultEntry> results, HashSet<string> seen, int mapId, string prefix)

        {

            if (mapId <= 0 || !seen.Add($"field:{mapId}"))

            {

                return;

            }



            string displayName = ResolveMapTransferDisplayName(mapId, null);

            results.Add(new WorldMapUI.SearchResultEntry

            {

                Kind = WorldMapUI.SearchResultKind.Field,

                MapId = mapId,

                Label = string.IsNullOrWhiteSpace(prefix) ? displayName : $"{prefix}: {displayName}",

                Description = "Field result"

            });

        }

        private IReadOnlyList<WorldMapUI.QuestOverlayEntry> BuildWorldMapQuestOverlays(int? focusedMapId)

        {

            List<WorldMapUI.QuestOverlayEntry> overlays = new();

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            int currentMapId = _mapBoard?.MapInfo?.id ?? 0;

            int fallbackMapId = focusedMapId.GetValueOrDefault(currentMapId);



            if (_activeQuestDetailQuestId > 0 &&

                _questRuntime.TryGetQuestWorldMapTarget(_activeQuestDetailQuestId, _playerManager?.Player?.Build, out QuestWorldMapTarget activeTarget) &&

                activeTarget != null)

            {

                switch (activeTarget.Kind)

                {

                    case QuestWorldMapTargetKind.Mob:

                        if (activeTarget.EntityId is int targetMobId &&

                            TryGetPacketQuestGuideMapIds(targetMobId, out IReadOnlyList<int> packetMapIds))

                        {

                            for (int i = 0; i < packetMapIds.Count; i++)

                            {

                                AddWorldMapQuestOverlay(

                                    overlays,

                                    seen,

                                    WorldMapUI.SearchResultKind.Mob,

                                    packetMapIds[i],

                                    activeTarget.Label,

                                    $"Active quest #{_activeQuestDetailQuestId} mob target");

                            }

                        }

                        else

                        {

                            AddWorldMapQuestOverlay(

                                overlays,

                                seen,

                                WorldMapUI.SearchResultKind.Mob,

                                fallbackMapId,

                                activeTarget.Label,

                                $"Active quest #{_activeQuestDetailQuestId} mob target");

                        }



                        break;

                    case QuestWorldMapTargetKind.Item:

                        AddWorldMapQuestOverlay(

                            overlays,

                            seen,

                            WorldMapUI.SearchResultKind.Item,

                            fallbackMapId,

                            activeTarget.Label,

                            $"Active quest #{_activeQuestDetailQuestId} delivery item");

                        break;

                    default:

                        AddWorldMapQuestOverlay(

                            overlays,

                            seen,

                            WorldMapUI.SearchResultKind.Npc,

                            activeTarget.MapId > 0 ? activeTarget.MapId : fallbackMapId,

                            activeTarget.Label,

                            activeTarget.Description);

                        break;

                }

            }



            foreach ((int mobId, HashSet<int> mapIds) in _packetQuestGuideTargetsByMobId.OrderBy(entry => entry.Key))

            {

                string mobName = ResolvePacketGuideMobName(mobId);

                foreach (int mapId in mapIds.OrderBy(value => value))

                {

                    AddWorldMapQuestOverlay(

                        overlays,

                        seen,

                        WorldMapUI.SearchResultKind.Mob,

                        mapId,

                        mobName,

                        $"Packet guide quest #{_packetQuestGuideQuestId}");

                }

            }



            for (int i = 0; i < _lastQuestDemandQueryVisibleItemIds.Count; i++)

            {

                int itemId = _lastQuestDemandQueryVisibleItemIds[i];

                if (itemId <= 0)

                {

                    continue;

                }



                string itemName = InventoryItemMetadataResolver.TryResolveItemName(itemId, out string resolvedItemName)

                    ? resolvedItemName

                    : $"Item {itemId}";

                AddWorldMapQuestOverlay(

                    overlays,

                    seen,

                    WorldMapUI.SearchResultKind.Item,

                    fallbackMapId,

                    itemName,

                    $"Demand item query for quest #{_lastQuestDemandItemQueryQuestId}");

            }



            return overlays;

        }

        private static void AddWorldMapQuestOverlay(

            ICollection<WorldMapUI.QuestOverlayEntry> overlays,

            ISet<string> seen,

            WorldMapUI.SearchResultKind kind,

            int mapId,

            string label,

            string description)

        {

            if (overlays == null || seen == null || mapId <= 0 || string.IsNullOrWhiteSpace(label))

            {

                return;

            }



            string trimmedLabel = label.Trim();

            string key = $"{kind}:{mapId}:{trimmedLabel}:{description}";

            if (!seen.Add(key))

            {

                return;

            }



            overlays.Add(new WorldMapUI.QuestOverlayEntry

            {

                Kind = kind,

                MapId = mapId,

                Label = trimmedLabel,

                Description = description ?? string.Empty

            });

        }

        private bool TryGetPacketQuestGuideMapIds(int mobId, out IReadOnlyList<int> mapIds)

        {

            if (mobId > 0 &&

                _packetQuestGuideTargetsByMobId.TryGetValue(mobId, out HashSet<int> knownMapIds) &&

                knownMapIds != null &&

                knownMapIds.Count > 0)

            {

                mapIds = knownMapIds.OrderBy(value => value).ToArray();

                return true;

            }



            mapIds = Array.Empty<int>();

            return false;

        }



        private void HandleWorldMapRequested(WorldMapUI.MapEntry entry)

        {

            if (entry == null)

            {

                return;

            }



            if (_worldMapRequestMode == WorldMapRequestMode.MapTransferTargetSelection)

            {

                string selectedSlotLabel = _mapTransferEditDestination?.IsSavedSlot == true

                    ? $"saved slot {_mapTransferEditDestination.SavedSlotIndex + 1}"

                    : null;

                _mapTransferManualDestination = new MapTransferUI.DestinationEntry

                {

                    MapId = entry.MapId,

                    DisplayName = $"[Target] {entry.DisplayName}",

                    DetailText = !string.IsNullOrWhiteSpace(selectedSlotLabel)

                        ? $"{entry.MapId} selected for {selectedSlotLabel}"

                        : $"{entry.MapId} selected from world map",

                    CanDelete = false

                };



                _worldMapRequestMode = WorldMapRequestMode.DirectTransfer;

                RefreshMapTransferWindow();

                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapTransfer) is MapTransferUI mapTransferWindow)

                {

                    mapTransferWindow.SetSelectedMapId(entry.MapId);

                }

                uiWindowManager?.HideWindow(MapSimulatorWindowNames.WorldMap);

                uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MapTransfer);

                return;

            }



            string mapTransferRestrictionMessage = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);

            if (!string.IsNullOrWhiteSpace(mapTransferRestrictionMessage))

            {

                ShowFieldRestrictionMessage(mapTransferRestrictionMessage);

                return;

            }



            if (_loadMapCallback == null)

            {

                _chat.AddMessage("World map transfer is unavailable without a map loader.", new Color(255, 228, 151), Environment.TickCount);

                return;

            }



            if (QueueMapTransfer(entry.MapId, null))

            {

                uiWindowManager?.HideWindow(MapSimulatorWindowNames.WorldMap);

            }

        }



        private List<MapTransferUI.DestinationEntry> BuildMapTransferDestinations()

        {

            List<MapTransferUI.DestinationEntry> destinations = new();

            HashSet<string> seenKeys = new(StringComparer.OrdinalIgnoreCase);

            int savedCapacity = GetMapTransferSavedSlotCapacity();

            Dictionary<int, MapTransferDestinationRecord> savedBySlot = GetCurrentMapTransferDestinations()

                .Where(saved => saved != null && saved.SlotIndex >= 0 && saved.SlotIndex < savedCapacity)

                .GroupBy(saved => saved.SlotIndex)

                .ToDictionary(group => group.Key, group => group.First());



            for (int slotIndex = 0; slotIndex < savedCapacity; slotIndex++)

            {

                if (savedBySlot.TryGetValue(slotIndex, out MapTransferDestinationRecord saved))

                {

                    string displayName = ResolveMapTransferDisplayName(saved.MapId, saved.DisplayName);

                    seenKeys.Add($"saved:{saved.MapId}");

                    destinations.Add(new MapTransferUI.DestinationEntry

                    {

                        MapId = saved.MapId,

                        DisplayName = displayName,

                        DetailText = $"Saved slot {slotIndex + 1}    {saved.MapId}",

                        SavedSlotIndex = slotIndex,

                        CanDelete = true

                    });

                    continue;

                }



                destinations.Add(new MapTransferUI.DestinationEntry

                {

                    MapId = 0,

                    DisplayName = "Empty",

                    DetailText = $"Saved slot {slotIndex + 1} is empty. Press Register to save the current map or the selected world-map target here.",

                    SavedSlotIndex = slotIndex,

                    CanDelete = false

                });

            }



            if (_mapTransferManualDestination != null && seenKeys.Add($"manual:{_mapTransferManualDestination.MapId}"))

            {

                destinations.Add(_mapTransferManualDestination);

            }



            AddSystemMapTransferDestination(destinations, seenKeys, _mapBoard?.MapInfo?.returnMap, "Return");

            AddSystemMapTransferDestination(destinations, seenKeys, _mapBoard?.MapInfo?.forcedReturn, "Forced");



            if (_mapBoard?.BoardItems?.Portals != null)

            {

                foreach (PortalInstance portal in _mapBoard.BoardItems.Portals)

                {

                    if (portal == null || portal.tm <= 0 || portal.tm == MapConstants.MaxMap)

                    {

                        continue;

                    }



                    string key = $"portal:{portal.tm}:{portal.tn}";

                    if (!seenKeys.Add(key))

                    {

                        continue;

                    }



                    string displayName = ResolveMapTransferDisplayName(portal.tm);

                    string sourcePortal = string.IsNullOrWhiteSpace(portal.pn) ? "portal" : portal.pn;

                    destinations.Add(new MapTransferUI.DestinationEntry

                    {

                        MapId = portal.tm,

                        DisplayName = $"[Portal] {displayName}",

                        DetailText = $"{portal.tm} via {sourcePortal}",

                        TargetPortalName = portal.tn,

                        CanDelete = false

                    });

                }

            }



            return destinations;

        }



        private void AddSystemMapTransferDestination(

            ICollection<MapTransferUI.DestinationEntry> destinations,

            ISet<string> seenKeys,

            int? mapId,

            string label)

        {

            if (!mapId.HasValue || mapId.Value <= 0 || mapId.Value == MapConstants.MaxMap)

            {

                return;

            }



            string key = $"system:{label}:{mapId.Value}";

            if (!seenKeys.Add(key))

            {

                return;

            }



            string displayName = ResolveMapTransferDisplayName(mapId.Value);

            destinations.Add(new MapTransferUI.DestinationEntry

            {

                MapId = mapId.Value,

                DisplayName = $"[{label}] {displayName}",

                DetailText = $"{mapId.Value}",

                CanDelete = false

            });

        }



        private void RegisterCurrentMapTransferDestination(MapTransferUI.DestinationEntry selectedEntry)

        {

            int targetMapId;

            string targetDisplayName;



            if (_mapTransferManualDestination != null)

            {

                targetMapId = _mapTransferManualDestination.MapId;

                targetDisplayName = ResolveMapTransferDisplayName(

                    targetMapId,

                    TrimMapTransferCategoryPrefix(_mapTransferManualDestination.DisplayName));

            }

            else

            {

                if (_mapBoard?.MapInfo == null)

                {

                    return;

                }



                targetMapId = _mapBoard.MapInfo.id;

                targetDisplayName = ResolveMapTransferDisplayName(targetMapId, GetCurrentMapTransferDisplayName());

            }



            if (targetMapId <= 0 || targetMapId == MapConstants.MaxMap)

            {

                return;

            }



            string registrationRestriction = FieldInteractionRestrictionEvaluator.GetMapTransferRegistrationRestrictionMessage(targetMapId);

            if (!string.IsNullOrWhiteSpace(registrationRestriction))

            {

                _chat.AddMessage(registrationRestriction, new Color(255, 228, 151), Environment.TickCount);

                RefreshMapTransferWindow();

                return;

            }



            if (!string.IsNullOrWhiteSpace(targetDisplayName))

            {

                _mapTransferTitleCache[targetMapId] = targetDisplayName;

            }

            CharacterBuild activeBuild = GetActiveMapTransferCharacterBuild();

            MapTransferRuntimeResponse response = _mapTransferRuntime.SubmitRequest(

                activeBuild,

                new MapTransferRuntimeRequest

                {

                    Type = MapTransferRuntimeRequestType.Register,

                    Book = GetCurrentMapTransferDestinationBook(),

                    MapId = targetMapId,

                    SlotIndex = _mapTransferEditDestination?.SavedSlotIndex ?? selectedEntry?.SavedSlotIndex ?? -1

                });



            if (_mapTransferManualDestination?.MapId == targetMapId

                && (response.Applied || response.FocusMapId == targetMapId))

            {

                _mapTransferManualDestination = null;

            }



            if (response.Applied)

            {

                _mapTransferEditDestination = null;

            }



            RefreshMapTransferWindow();

            if (response.FocusMapId > 0

                && uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapTransfer) is MapTransferUI mapTransferWindow)

            {

                mapTransferWindow.SetSelectedMapId(response.FocusMapId);

            }



            if (!response.Applied && !string.IsNullOrWhiteSpace(response.FailureMessage))

            {

                _chat.AddMessage(response.FailureMessage, new Color(255, 228, 151), Environment.TickCount);

            }

        }



        private void DeleteMapTransferDestination(MapTransferUI.DestinationEntry destination)

        {

            if (destination == null || !destination.IsSavedSlot)

            {

                return;

            }



            MapTransferRuntimeResponse response = _mapTransferRuntime.SubmitRequest(
                GetActiveMapTransferCharacterBuild(),
                new MapTransferRuntimeRequest
                {
                    Type = MapTransferRuntimeRequestType.Delete,
                    Book = GetCurrentMapTransferDestinationBook(),
                    MapId = destination.MapId,
                    SlotIndex = destination.SavedSlotIndex
                });
            if (!response.Applied)
            {
                return;
            }

            if (_mapTransferEditDestination?.SavedSlotIndex == destination.SavedSlotIndex)

            {

                _mapTransferEditDestination = null;

            }



            if (_mapTransferManualDestination != null && destination.MapId == _mapTransferManualDestination.MapId)

            {

                _mapTransferManualDestination = null;

            }



            RefreshMapTransferWindow();

        }



        private void MoveToMapTransferDestination(MapTransferUI.DestinationEntry destination)

        {

            if (destination == null)

            {

                return;

            }



            string mapTransferRestrictionMessage = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);

            if (!string.IsNullOrWhiteSpace(mapTransferRestrictionMessage))

            {

                ShowFieldRestrictionMessage(mapTransferRestrictionMessage);

                return;

            }



            if (_loadMapCallback == null)

            {

                _chat.AddMessage("Map transfer is unavailable without a map loader.", new Color(255, 228, 151), Environment.TickCount);

                return;

            }



            QueueMapTransfer(destination.MapId, destination.TargetPortalName);

        }



        private void MoveToManualMapTransferDestination(int targetMapId)

        {

            if (targetMapId <= 0)

            {

                return;

            }



            string mapTransferRestrictionMessage = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);

            if (!string.IsNullOrWhiteSpace(mapTransferRestrictionMessage))

            {

                ShowFieldRestrictionMessage(mapTransferRestrictionMessage);

                return;

            }



            if (_loadMapCallback == null)

            {

                _chat.AddMessage("Map transfer is unavailable without a map loader.", new Color(255, 228, 151), Environment.TickCount);

                return;

            }



            QueueMapTransfer(targetMapId, null);

        }



        private bool QueueMapTransfer(int targetMapId, string targetPortalName)

        {

            if (targetMapId <= 0 || targetMapId == MapConstants.MaxMap || _gameState.PendingMapChange)

            {

                return false;

            }



            _playerManager?.ForceStand();

            _gameState.PendingMapChange = true;

            _gameState.PendingMapId = targetMapId;

            _gameState.PendingPortalName = targetPortalName;

            return true;

        }



        private string GetCurrentMapTransferDisplayName()

        {

            if (_mapBoard?.MapInfo == null)

            {

                return string.Empty;

            }



            string street = _mapBoard.MapInfo.strStreetName;

            string map = _mapBoard.MapInfo.strMapName;

            if (string.IsNullOrWhiteSpace(street))

            {

                return string.IsNullOrWhiteSpace(map) ? _mapBoard.MapInfo.id.ToString() : map;

            }



            if (string.IsNullOrWhiteSpace(map) || string.Equals(street, map, StringComparison.OrdinalIgnoreCase))

            {

                return street;

            }



            return $"{street} : {map}";

        }



        private string GetMapTransferStatusMessage()

        {

            if (_loadMapCallback == null)

            {

                return "Map loading is unavailable in this session.";

            }



            string restrictionMessage = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);

            if (!string.IsNullOrWhiteSpace(restrictionMessage))

            {

                return restrictionMessage;

            }



            if (_mapTransferManualDestination != null)

            {

                string registrationRestriction = FieldInteractionRestrictionEvaluator.GetMapTransferRegistrationRestrictionMessage(_mapTransferManualDestination.MapId);

                if (!string.IsNullOrWhiteSpace(registrationRestriction))

                {

                    return registrationRestriction;

                }



                string editPrefix = _mapTransferEditDestination?.IsSavedSlot == true

                    ? $"Write into saved slot {_mapTransferEditDestination.SavedSlotIndex + 1} or "

                    : string.Empty;

                return $"{editPrefix}move to {TrimMapTransferCategoryPrefix(_mapTransferManualDestination.DisplayName)} via the world-map target.";

            }



            string currentMapRegistrationRestriction = FieldInteractionRestrictionEvaluator.GetMapTransferRegistrationRestrictionMessage(_mapBoard?.MapInfo?.id ?? 0);

            if (!string.IsNullOrWhiteSpace(currentMapRegistrationRestriction))

            {

                return currentMapRegistrationRestriction;

            }



            int savedCapacity = GetMapTransferSavedSlotCapacity();

            IReadOnlyList<MapTransferDestinationRecord> currentDestinations = GetCurrentMapTransferDestinations();

            if (currentDestinations.Count >= savedCapacity)

            {

                return $"Saved slots full ({savedCapacity}/{savedCapacity}). Delete one before registering another map.";

            }



            string ownerName = GetActiveMapTransferCharacterBuild()?.Name;

            string ownerSuffix = string.IsNullOrWhiteSpace(ownerName) ? string.Empty : $" for {ownerName}";

            return $"Register the current map, enter a map ID, or choose a listed route ({Math.Min(currentDestinations.Count, savedCapacity)}/{savedCapacity} saved{ownerSuffix}).";

        }



        private static string TrimMapTransferCategoryPrefix(string displayName)

        {

            if (string.IsNullOrWhiteSpace(displayName))

            {

                return string.Empty;

            }



            int closingBracketIndex = displayName.IndexOf(']');

            if (displayName.StartsWith("[", StringComparison.Ordinal) && closingBracketIndex >= 0 && closingBracketIndex + 1 < displayName.Length)

            {

                return displayName[(closingBracketIndex + 1)..].TrimStart();

            }



            return displayName;

        }



        private int GetMapTransferSavedSlotCapacity()

        {

            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapTransfer) is MapTransferUI mapTransferWindow

                ? mapTransferWindow.MaxSavedDestinations

                : 10;

        }



        private IReadOnlyList<MapTransferDestinationRecord> GetCurrentMapTransferDestinations()

        {

            return _mapTransferRuntime.GetDestinations(GetActiveMapTransferCharacterBuild(), GetCurrentMapTransferDestinationBook());

        }



        private MapTransferDestinationBook GetCurrentMapTransferDestinationBook()

        {

            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapTransfer) is MapTransferUI mapTransferWindow &&

                   mapTransferWindow.UsesContinentDestinationBook

                ? MapTransferDestinationBook.Continent

                : MapTransferDestinationBook.Regular;

        }



        private CharacterBuild GetActiveMapTransferCharacterBuild()

        {

            return _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;

        }



        private CharacterBuild GetActiveSkillMacroCharacterBuild()

        {

            return _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;

        }



        private CharacterBuild GetActiveItemMakerCharacterBuild()

        {

            return _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;

        }



        private ItemMakerProgressionSnapshot GetActiveItemMakerProgression()

        {

            return _itemMakerProgressionStore.GetSnapshot(GetActiveItemMakerCharacterBuild());

        }

        private ItemMakerProgressionSnapshot ResolveCharacterInfoItemMakerProgressionSnapshot(CharacterBuild build)
        {
            return _itemMakerProgressionStore.GetSnapshot(build ?? GetActiveItemMakerCharacterBuild());
        }

        private MonsterBookSnapshot GetActiveMonsterBookSnapshot()

        {

            return _monsterBookManager.GetSnapshot(_playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build);

        }

        private MonsterBookSnapshot ResolveCharacterInfoMonsterBookSnapshot(CharacterBuild build)
        {
            return _monsterBookManager.GetSnapshot(build ?? _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build);
        }



        private void SyncStorageAccessContext()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Trunk) is not TrunkUI trunkWindow)

            {

                return;

            }



            CharacterBuild activeBuild = _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;

            string currentCharacterName = string.IsNullOrWhiteSpace(activeBuild?.Name) ? "ExplorerGM" : activeBuild.Name.Trim();

            string accountLabel = BuildStorageAccountLabel();

            string accountKey = BuildStorageAccountKey();

            IReadOnlyList<string> sharedCharacterNames = _loginCharacterRoster.Entries

                .Select(entry => entry?.Build?.Name?.Trim())

                .Where(name => !string.IsNullOrWhiteSpace(name))

                .Distinct(StringComparer.OrdinalIgnoreCase)

                .ToArray();

            trunkWindow.ConfigureStorageAccess(accountLabel, accountKey, currentCharacterName, sharedCharacterNames);
            trunkWindow.ConfigureStorageLoginSecurity(_loginAccountPicCode, _loginAccountSpwEnabled, _loginAccountSecondaryPassword);

        }



        private string BuildStorageAccountLabel()

        {

            return $"Simulator Account Storage (World {_simulatorWorldId + 1})";

        }



        private string BuildStorageAccountKey()

        {

            string accountName = string.IsNullOrWhiteSpace(_loginTitleAccountName)

                ? "simulator"

                : _loginTitleAccountName.Trim();

            return StorageAccountStore.ResolveAccountKey($"login:{accountName}|world:{_simulatorWorldId}");

        }



        private void LoadPersistedSkillMacros()

        {

            if (uiWindowManager?.SkillMacroWindow == null)

            {

                return;

            }



            uiWindowManager.SkillMacroWindow.LoadMacros(_skillMacroStore.GetMacros(GetActiveSkillMacroCharacterBuild()));

            _playerManager?.Skills?.RevalidateHotkeys();

        }



        private void PersistSkillMacros()

        {

            if (uiWindowManager?.SkillMacroWindow == null)

            {

                return;

            }



            _skillMacroStore.Save(GetActiveSkillMacroCharacterBuild(), uiWindowManager.SkillMacroWindow.Macros);

            _playerManager?.Skills?.RevalidateHotkeys();

        }



        private string ResolveMapTransferDisplayName(int mapId, string fallbackDisplayName = null)

        {

            if (_mapTransferTitleCache.TryGetValue(mapId, out string cachedName) && !string.IsNullOrWhiteSpace(cachedName))

            {

                return cachedName;

            }



            if (!string.IsNullOrWhiteSpace(fallbackDisplayName))

            {

                _mapTransferTitleCache[mapId] = fallbackDisplayName;

                return fallbackDisplayName;

            }



            if (_mapBoard?.MapInfo?.id == mapId)

            {

                string currentDisplayName = GetCurrentMapTransferDisplayName();

                if (!string.IsNullOrWhiteSpace(currentDisplayName))

                {

                    _mapTransferTitleCache[mapId] = currentDisplayName;

                    return currentDisplayName;

                }

            }



            if (TryResolveMapDisplayNameFromCache(mapId, out string resolvedDisplayName))

            {

                _mapTransferTitleCache[mapId] = resolvedDisplayName;

                return resolvedDisplayName;

            }



            return mapId.ToString();

        }



        private static bool TryResolveMapDisplayNameFromCache(int mapId, out string displayName)

        {

            displayName = null;



            string mapIdKey = mapId.ToString().PadLeft(9, '0');

            if (Program.InfoManager?.MapsCache == null ||

                !Program.InfoManager.MapsCache.TryGetValue(mapIdKey, out Tuple<WzImage, string, string, string, MapleLib.WzLib.WzStructure.MapInfo> cachedMap) ||

                cachedMap == null)

            {

                return false;

            }



            string mapName = cachedMap.Item2;

            string streetName = cachedMap.Item3;

            MapleLib.WzLib.WzStructure.MapInfo mapInfo = cachedMap.Item5;

            if (mapInfo != null)

            {

                if (string.IsNullOrWhiteSpace(mapName))

                {

                    mapName = mapInfo.strMapName;

                }



                if (string.IsNullOrWhiteSpace(streetName))

                {

                    streetName = mapInfo.strStreetName;

                }

            }



            if (!string.IsNullOrWhiteSpace(streetName) &&

                !string.IsNullOrWhiteSpace(mapName) &&

                !string.Equals(streetName, mapName, StringComparison.OrdinalIgnoreCase))

            {

                displayName = $"{streetName} : {mapName}";

            }

            else if (!string.IsNullOrWhiteSpace(mapName))

            {

                displayName = mapName;

            }

            else if (!string.IsNullOrWhiteSpace(streetName))

            {

                displayName = streetName;

            }



            return !string.IsNullOrWhiteSpace(displayName);

        }



        private void WireQuestLogWindowData()

        {

            ConfigureQuestUiBindings();

            RefreshQuestUiState();

        }



        private void WireMemoMailboxWindowData()

        {

            if (uiWindowManager == null)

            {

                return;

            }



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.MemoMailbox) is MemoMailboxWindow memoMailboxWindow)

            {

                memoMailboxWindow.SetSnapshotProvider(_memoMailbox.GetSnapshot);

                memoMailboxWindow.SetActions(

                    memoId => _memoMailbox.OpenMemo(memoId),

                    memoId => _memoMailbox.KeepMemo(memoId),

                    memoId => _memoMailbox.DeleteMemo(memoId),

                    OpenMemoAttachmentWindow);

                memoMailboxWindow.SetFont(_fontChat);

            }



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.MemoSend) is MemoSendWindow memoSendWindow)

            {

                memoSendWindow.SetSnapshotProvider(_memoMailbox.GetDraftSnapshot);

                memoSendWindow.SetActions(

                    () =>

                    {

                        if (_memoMailbox.TrySendDraft(out string message))

                        {

                            ShowUtilityFeedbackMessage(message);

                            memoSendWindow.Hide();

                            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.MemoMailbox);

                        }

                        else

                        {

                            ShowUtilityFeedbackMessage(message);

                        }



                        return message;

                    },

                    () => ShowUtilityFeedbackMessage("Closed memo send dialog."));

                memoSendWindow.SetFont(_fontChat);

            }



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.MemoGet) is MemoGetWindow memoGetWindow)

            {

                memoGetWindow.SetSnapshotProvider(() => _memoMailbox.GetAttachmentSnapshot(_activeMemoAttachmentId));

                memoGetWindow.SetActions(

                    () =>

                    {

                        if (_memoMailbox.TryClaimAttachment(_activeMemoAttachmentId, out string message))

                        {

                            ShowUtilityFeedbackMessage(message);

                        }

                        else

                        {

                            ShowUtilityFeedbackMessage(message);

                        }



                        return message;

                    },

                    () => ShowUtilityFeedbackMessage("Closed memo package dialog."));

                memoGetWindow.SetFont(_fontChat);

            }

        }



        private void OpenMemoAttachmentWindow(int memoId)

        {

            if (!_memoMailbox.CanClaimAttachment(memoId))

            {

                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MemoMailbox) is MemoMailboxWindow)

                {

                    ShowUtilityFeedbackMessage("No claimable package is attached to this memo.");

                }



                return;

            }



            _activeMemoAttachmentId = memoId;

            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MemoGet);

        }



        private void ShowCharacterInfoWindow(string initialPage = null, UserInfoUI.UserInfoInspectionTarget inspectionTarget = null)



        {



            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.CharacterInfo);



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterInfo) is not UserInfoUI characterInfoWindow)



            {



                return;



            }







            uiWindowManager.BringToFront(characterInfoWindow);

            if (inspectionTarget?.Build != null)
            {
                characterInfoWindow.CharacterBuild = inspectionTarget.Build;
                characterInfoWindow.SetInspectionTarget(inspectionTarget);
            }
            else
            {
                characterInfoWindow.CharacterBuild = _playerManager?.Player?.Build;
                characterInfoWindow.ClearInspectionTarget();
                _familyChartRuntime.ClearRemotePreviewRequest();
            }



            if (!string.IsNullOrWhiteSpace(initialPage))



            {



                characterInfoWindow.ShowPage(initialPage);



            }



        }







        private void ShowWindowWithInheritedDirectionModeOwner(string windowName)

        {

            if (uiWindowManager == null || string.IsNullOrWhiteSpace(windowName))

            {

                return;

            }

            if (!TryShowFieldRestrictedWindow(windowName))
            {
                return;
            }



            if (ShouldTrackInheritedDirectionModeOwner())

            {

                _scriptedDirectionModeWindows.TrackWindow(windowName);

            }



            uiWindowManager.ShowWindow(windowName);

        }

        private void HandleImplicitDirectionModeOwnerWindowShow(string windowName)

        {

            if (!ShouldTrackInheritedDirectionModeOwner()

                || !DirectionModeWindowOwnerRegistry.IsImplicitOwnerEligibleWindow(windowName))

            {

                return;

            }

            _scriptedDirectionModeWindows.TrackWindow(windowName);

        }

        private bool ShouldTrackInheritedDirectionModeOwner()

        {

            return (_npcInteractionOverlay?.IsVisible == true)

                || _gameState.DirectionModeActive



                || _scriptedDirectionModeOwnerActive

                || _specialFieldRuntime.SpecialEffects.Wedding.HasActiveScriptedDialog

                || _specialFieldRuntime.Minigames.MemoryGame.IsVisible;

        }



        private void WireSocialListWindowData()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.SocialList) is not SocialListWindow socialListWindow)

            {

                return;

            }



            _socialListRuntime.UpdateLocalContext(_playerManager?.Player?.Build, GetCurrentMapTransferDisplayName(), 1);

            socialListWindow.SetSnapshotProvider(_socialListRuntime.BuildSnapshot);

            socialListWindow.SetHandlers(

                tab => _socialListRuntime.SelectTab(tab),

                visibleIndex => _socialListRuntime.SelectVisibleEntry(visibleIndex),

                delta => _socialListRuntime.MovePage(delta),

                delta => _socialListRuntime.MoveScroll(delta),

                ratio => _socialListRuntime.SetScrollPosition(ratio),

                onlineOnly => _socialListRuntime.SetFriendOnlineOnly(onlineOnly),

                actionKey =>

                {

                    if (string.Equals(actionKey, "Guild.Board", StringComparison.Ordinal))

                    {

                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildBbs);

                        return "Opened Guild BBS from the guild member list.";

                    }



                    if (string.Equals(actionKey, "Party.Search", StringComparison.Ordinal))

                    {

                        _socialListRuntime.OpenSearchWindow(SocialSearchTab.Party);

                        WireSocialSearchWindowData();

                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialSearch);

                        return "Opened the party and expedition search surface.";

                    }



                    if (string.Equals(actionKey, "Guild.Search", StringComparison.Ordinal))

                    {

                        _socialListRuntime.OpenGuildSearchWindow();

                        WireGuildSearchWindowData();

                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildSearch);

                        return "Opened the dedicated guild search surface.";

                    }



                    if (string.Equals(actionKey, "Guild.Skill", StringComparison.Ordinal))

                    {
                        if (!GuildSkillRuntime.HasGuildMembership(_playerManager?.Player?.Build))
                        {
                            return "Join a guild before opening the dedicated guild skill surface.";
                        }

                        WireGuildSkillWindowData();

                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildSkill);

                        return "Opened the dedicated guild skill surface.";

                    }



                    if (string.Equals(actionKey, "Guild.Manage", StringComparison.Ordinal))



                    {



                        _socialListRuntime.OpenGuildManageWindow(GuildManageTab.Position);



                        WireGuildManageWindowData();



                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildManage);



                        return "Opened the dedicated guild-management surface.";



                    }







                    if (string.Equals(actionKey, "Guild.Change", StringComparison.Ordinal))



                    {



                        _socialListRuntime.OpenGuildManageWindow(GuildManageTab.Change);



                        WireGuildManageWindowData();



                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildManage);



                        return "Opened the guild notice editor on the change tab.";



                    }







                    if (string.Equals(actionKey, "Alliance.Change", StringComparison.Ordinal))



                    {



                        _socialListRuntime.OpenAllianceEditor(AllianceEditorFocus.RankTitle);



                        WireAllianceEditorWindowData();



                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.AllianceEditor);



                        return "Opened the alliance editor for rank-title changes.";



                    }







                    if (string.Equals(actionKey, "Alliance.Notice", StringComparison.Ordinal))



                    {



                        _socialListRuntime.OpenAllianceEditor(AllianceEditorFocus.Notice);



                        WireAllianceEditorWindowData();



                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.AllianceEditor);



                        return "Opened the alliance editor for notice changes.";



                    }







                    return _socialListRuntime.ExecuteAction(actionKey);

                },

                ShowUtilityFeedbackMessage);

            socialListWindow.SetFont(_fontChat);

        }



        private void WireSocialSearchWindowData()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.SocialSearch) is not SocialSearchWindow socialSearchWindow)

            {

                return;

            }



            _socialListRuntime.UpdateLocalContext(_playerManager?.Player?.Build, GetCurrentMapTransferDisplayName(), 1);

            socialSearchWindow.SetSnapshotProvider(_socialListRuntime.BuildSearchSnapshot);

            socialSearchWindow.SetHandlers(

                tab => _socialListRuntime.SelectSearchTab(tab),

                visibleIndex => _socialListRuntime.SelectSearchEntry(visibleIndex),

                similarOnly => _socialListRuntime.SetSearchSimilarLevelOnly(similarOnly),

                actionKey => _socialListRuntime.ExecuteSearchAction(actionKey),

                ShowUtilityFeedbackMessage);

            socialSearchWindow.SetFont(_fontChat);

        }



        private void WireGuildSearchWindowData()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.GuildSearch) is not GuildSearchWindow guildSearchWindow)

            {

                return;

            }



            _socialListRuntime.UpdateLocalContext(_playerManager?.Player?.Build, GetCurrentMapTransferDisplayName(), 1);

            guildSearchWindow.SetSnapshotProvider(_socialListRuntime.BuildGuildSearchSnapshot);

            guildSearchWindow.SetHandlers(

                visibleIndex => _socialListRuntime.SelectGuildSearchEntry(visibleIndex),

                delta => _socialListRuntime.MoveGuildSearchPage(delta),

                actionKey =>

                {

                    if (string.Equals(actionKey, "GuildSearch.PagePrev", StringComparison.Ordinal))

                    {

                        _socialListRuntime.MoveGuildSearchPage(-1);

                        return null;

                    }



                    if (string.Equals(actionKey, "GuildSearch.PageNext", StringComparison.Ordinal))

                    {

                        _socialListRuntime.MoveGuildSearchPage(1);

                        return null;

                    }



                    return _socialListRuntime.ExecuteGuildSearchAction(actionKey);

                },

                ShowUtilityFeedbackMessage);

            guildSearchWindow.SetFont(_fontChat);

        }



        private void WireGuildSkillWindowData()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.GuildSkill) is not GuildSkillWindow guildSkillWindow)

            {

                return;

            }



            if (_guildSkillRuntime.BuildSnapshot().Entries.Count == 0)

            {

                _guildSkillRuntime.SetSkills(SkillDataLoader.LoadGuildSkills(_DxDeviceManager.GraphicsDevice));

            }



            _guildSkillRuntime.UpdateLocalContext(
                _playerManager?.Player?.Build,
                _socialListRuntime.GetLocalGuildRoleLabel());

            guildSkillWindow.SetSnapshotProvider(_guildSkillRuntime.BuildSnapshot);

            guildSkillWindow.SetHandlers(

                visibleIndex => _guildSkillRuntime.SelectEntry(visibleIndex),

                () => _guildSkillRuntime.TryRenewSelectedSkill(),

                () => _guildSkillRuntime.TryLevelSelectedSkill(),

                ShowUtilityFeedbackMessage);

            guildSkillWindow.SetFont(_fontChat);

        }

        private void RefreshSkillWindowShortcutState()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Skills) is not SkillUIBigBang skillWindow)

            {

                return;

            }



            CharacterBuild build = _playerManager?.Player?.Build;

            bool canOpenRidePage = build?.HasMonsterRiding == true ||

                                   (build?.Equipment?.TryGetValue(EquipSlot.TamingMob, out CharacterPart mountPart) == true &&

                                    mountPart != null);

            skillWindow.ConfigureShortcutButtons(

                canOpenRidePage,

                GuildSkillRuntime.HasGuildMembership(build));

        }

        private void RefreshGuildSkillUiContext()

        {

            PlayerCharacter player = _playerManager?.Player;

            if (player == null)

            {

                return;

            }



            _guildSkillRuntime.UpdateLocalContext(

                player.Build,

                _socialListRuntime.GetLocalGuildRoleLabel());

            RefreshSkillWindowShortcutState();

        }



        private void WireGuildManageWindowData()



        {



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.GuildManage) is not GuildManageWindow guildManageWindow)



            {



                    return;



            }







            guildManageWindow.SetSnapshotProvider(_socialListRuntime.BuildGuildManageSnapshot);



            guildManageWindow.SetHandlers(



                tab => _socialListRuntime.SelectGuildManageTab(tab),



                visibleIndex => _socialListRuntime.SelectGuildManageRank(visibleIndex),



                delta => _socialListRuntime.MoveGuildManageRankSelection(delta),



                requiresApproval => _socialListRuntime.SetGuildAdmission(requiresApproval),



                () => _socialListRuntime.BeginGuildManageEdit(),



                () => _socialListRuntime.SaveGuildManageEdit(),



                () => _socialListRuntime.CancelGuildManageEdit(),



                value => _socialListRuntime.SetGuildManageDraft(value),



                ShowUtilityFeedbackMessage);



            guildManageWindow.SetFont(_fontChat);



        }







        private void WireAllianceEditorWindowData()



        {



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AllianceEditor) is not AllianceEditorWindow allianceEditorWindow)



            {



                return;



            }







            allianceEditorWindow.SetSnapshotProvider(_socialListRuntime.BuildAllianceEditorSnapshot);



            allianceEditorWindow.SetHandlers(



                visibleIndex => _socialListRuntime.SelectAllianceRankTitle(visibleIndex),



                () => _socialListRuntime.FocusAllianceNotice(),



                () => _socialListRuntime.BeginAllianceEdit(),



                () => _socialListRuntime.SaveAllianceEdit(),



                () => _socialListRuntime.CancelAllianceEdit(),



                value => _socialListRuntime.SetAllianceEditorDraft(value),



                ShowUtilityFeedbackMessage);



            allianceEditorWindow.SetFont(_fontChat);



        }







        private void WireFamilyChartWindowData()

        {

            if (uiWindowManager == null)

            {

                return;

            }



            _familyChartRuntime.UpdateLocalContext(_playerManager?.Player?.Build, GetCurrentMapTransferDisplayName(), 1);



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.FamilyChart) is FamilyChartWindow familyChartWindow)

            {

                familyChartWindow.SetSnapshotProvider(_familyChartRuntime.BuildChartSnapshot);

                familyChartWindow.SetActionHandlers(

                    () =>

                    {

                        ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.FamilyTree);

                        return "Opened the dedicated family tree.";

                    },

                    () => _familyChartRuntime.CyclePrecept(),

                    () => _familyChartRuntime.AddJunior(),

                    delta => _familyChartRuntime.MoveFocus(delta),

                    () =>

                    {

                        FamilyEntitlementUseResult result = _familyChartRuntime.ExecuteSelectedEntitlement(

                            Environment.TickCount,

                            _playerManager?.Player?.Position ?? Vector2.Zero);

                        if (result.RequestTeleport)

                        {

                            _playerManager?.TeleportTo(result.TeleportPosition.X, result.TeleportPosition.Y);

                        }



                        return result.Message;

                    },

                    () => _familyChartRuntime.CycleEntitlement(),

                    () =>

                    {

                        uiWindowManager?.HideWindow(MapSimulatorWindowNames.FamilyTree);

                        return "Closed family chart.";

                    },

                    ShowUtilityFeedbackMessage);

                familyChartWindow.SetFont(_fontChat);

            }



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.FamilyTree) is FamilyTreeWindow familyTreeWindow)

            {

                familyTreeWindow.SetSnapshotProvider(_familyChartRuntime.BuildTreeSnapshot);

                familyTreeWindow.SetActionHandlers(

                    slotIndex =>

                    {

                        string message = _familyChartRuntime.SelectNode(slotIndex);

                        ShowUtilityFeedbackMessage(message);

                    },

                    () => _familyChartRuntime.AddJunior(),

                    () => _familyChartRuntime.RemoveSelectedMember(),

                    delta => _familyChartRuntime.MoveFocus(delta),

                    ShowUtilityFeedbackMessage);

                familyTreeWindow.SetFont(_fontChat);

            }

        }



        private void WireMessengerWindowData()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Messenger) is not MessengerWindow messengerWindow)

            {

                return;

            }



            string playerName = _playerManager?.Player?.Build?.Name ?? "Player";

            string locationSummary = GetCurrentMapTransferDisplayName();

            _messengerRuntime.UpdateLocalContext(playerName, locationSummary, 1);



            messengerWindow.SetSnapshotProvider(() => _messengerRuntime.BuildSnapshot(Environment.TickCount));

            messengerWindow.SetActionHandlers(

                slotIndex => _messengerRuntime.SelectSlot(slotIndex),

                () => _messengerRuntime.SubmitClaim(),

                () => _messengerRuntime.LeaveMessenger(),

                forward => _messengerRuntime.CycleState(forward),

                message => _messengerRuntime.ProcessChatInput(message),

                message => _messengerRuntime.WhisperSelected(message),

                () => _messengerRuntime.TryDeleteMessenger(),

                _messengerRuntime.AcknowledgeWindowClose,

                ShowUtilityFeedbackMessage);

            messengerWindow.SetFont(_fontChat);

        }



        private void ShowMessengerWindow()

        {

            WireMessengerWindowData();

            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.Messenger);

        }



        private void WireGuildBbsWindowData()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.GuildBbs) is not GuildBbsWindow guildBbsWindow)

            {

                return;

            }



            CharacterBuild build = _playerManager?.Player?.Build;

            string playerName = build?.Name ?? "Player";

            string guildName = string.IsNullOrWhiteSpace(build?.GuildName) ? "Maple Guild" : build.GuildName;

            _guildBbsRuntime.ConfigureEmoticonCatalog(guildBbsWindow.BasicEmoticonSlotCount, guildBbsWindow.CashEmoticonSlotCount);

            _guildBbsRuntime.UpdateLocalContext(

                playerName,

                guildName,

                GetCurrentMapTransferDisplayName(),

                _socialListRuntime.GetLocalGuildRoleLabel(),

                ResolveOwnedGuildBbsCashEmoticonIds(guildBbsWindow.CashEmoticonSlotCount));



            guildBbsWindow.SetSnapshotProvider(_guildBbsRuntime.BuildSnapshot);

            guildBbsWindow.SetActionHandlers(

                threadId => _guildBbsRuntime.SelectThread(threadId),

                () => _guildBbsRuntime.BeginWrite(),

                () => _guildBbsRuntime.BeginEditSelected(),

                () => _guildBbsRuntime.DeleteSelectedThread(),

                () => _guildBbsRuntime.SubmitCompose(),

                () => _guildBbsRuntime.CancelCompose(),

                () => _guildBbsRuntime.ToggleNotice(),

                () => _guildBbsRuntime.AddReply(),

                () => _guildBbsRuntime.DeleteLatestReply(),

                value => _guildBbsRuntime.SetComposeTitle(value),

                value => _guildBbsRuntime.SetComposeBody(value),

                value => _guildBbsRuntime.SetReplyDraft(value),

                delta => _guildBbsRuntime.MoveThreadPage(delta),

                delta => _guildBbsRuntime.MoveCommentPage(delta),

                delta => _guildBbsRuntime.MoveComposeCashEmoticonPage(delta),

                delta => _guildBbsRuntime.MoveReplyCashEmoticonPage(delta),

                (kind, slotIndex, pageIndex) => _guildBbsRuntime.SelectComposeEmoticon(kind, slotIndex, pageIndex),

                (kind, slotIndex, pageIndex) => _guildBbsRuntime.SelectReplyEmoticon(kind, slotIndex, pageIndex),

                ShowUtilityFeedbackMessage);

            guildBbsWindow.SetFont(_fontChat);

        }



        private IEnumerable<int> ResolveOwnedGuildBbsCashEmoticonIds(int slotCount)

        {

            if (uiWindowManager?.InventoryWindow is not IInventoryRuntime inventory)

            {

                yield break;

            }



            int maxItemId = 5290000 + Math.Max(1, slotCount) - 1;

            for (int itemId = 5290000; itemId <= maxItemId; itemId++)

            {

                if (inventory.GetItemCount(InventoryType.CASH, itemId) > 0)

                {

                    yield return itemId;

                }

            }

        }



        private void WireMapleTvWindowData()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapleTv) is not MapleTvWindow mapleTvWindow)

            {

                return;

            }



            _mapleTvRuntime.ConfigureDefaultMedia(0, "Maple TV", mapleTvWindow.DefaultMediaIndex);

            _mapleTvRuntime.UpdateLocalContext(_playerManager?.Player?.Build);

            mapleTvWindow.SetSnapshotProvider(() => _mapleTvRuntime.BuildSnapshot(currTickCount));

            mapleTvWindow.SetActionHandlers(

                PublishMapleTvDraft,

                ClearMapleTvMessage,

                ToggleMapleTvReceiverMode,

                ShowUtilityFeedbackMessage);

            mapleTvWindow.SetFont(_fontChat);

        }



        private void ShowMapleTvWindow()

        {

            WireMapleTvWindowData();

            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.MapleTv);

        }



        private void WireProgressionUtilityWindowLaunchers()
        {
            if (miniMapUi != null)

            {

                miniMapUi.ResolveNpcMarkerType = ResolveMinimapNpcMarkerType;
                miniMapUi.ResolveNpcTooltipText = ResolveMinimapNpcTooltipText;
                miniMapUi.ResolvePortalTooltipText = ResolveMinimapPortalTooltipText;
                EnsureMinimapTooltipResources();

                miniMapUi.FullMapRequested = () =>

                {

                    _worldMapRequestMode = WorldMapRequestMode.DirectTransfer;

                    _mapTransferEditDestination = null;

                    RefreshWorldMapWindow();

                    uiWindowManager?.ShowWindow(MapSimulatorWindowNames.WorldMap);

                };

                miniMapUi.MapTransferRequested = () =>

                {

                    _worldMapRequestMode = WorldMapRequestMode.DirectTransfer;

                    _mapTransferEditDestination = null;

                    RefreshMapTransferWindow();

                    uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MapTransfer);

                };

            }



            if (statusBarUi != null)

            {

                statusBarUi.CashShopRequested = ShowCashShopWindow;

                statusBarUi.MtsRequested = () =>

                {

                    uiWindowManager?.HideWindow(MapSimulatorWindowNames.CashShop);

                    ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Mts);

                };

                statusBarUi.MenuRequested = () => ToggleStatusBarPopupWindow(MapSimulatorWindowNames.Menu, MapSimulatorWindowNames.System);

                statusBarUi.SystemRequested = () => ToggleStatusBarPopupWindow(MapSimulatorWindowNames.System, MapSimulatorWindowNames.Menu);

                statusBarUi.ChannelRequested = HandleUtilityChannelPopupRequested;

            }

            if (uiWindowManager != null)

            {

                uiWindowManager.BeforeShowWindow = HandleImplicitDirectionModeOwnerWindowShow;

            }



            if (statusBarChatUI != null)

            {

                statusBarChatUI.CharacterInfoRequested = () => ShowCharacterInfoWindow();

                statusBarChatUI.MemoMailboxRequested = () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.MemoMailbox);

            }



            WireSocialRoomWindowData();
            WireItemUpgradeWindowLaunchers();
            WireWorldChannelSelectorWindows();
            WireProgressionUtilitySettingsWindows();
            WireStatusBarPopupUtilityWindows();
        }

        private void WireProgressionUtilitySettingsWindows()
        {
            if (uiWindowManager == null)
            {
                return;
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.KeyConfig) is KeyConfigWindow keyConfigWindow)
            {
                keyConfigWindow.SetBindingSource(() => _playerManager?.Input);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.OptionMenu) is OptionMenuWindow optionMenuWindow)
            {
                optionMenuWindow.ConfigureRows(
                    () => _gameState.UseSmoothCamera,
                    value => _gameState.UseSmoothCamera = value,
                    () => _utilityBgmMuted,
                    SetUtilityBgmMuted,
                    () => _utilityEffectsMuted,
                    SetUtilityEffectsMuted,
                    () => _pauseAudioOnFocusLoss,
                    SetPauseAudioOnFocusLoss,
                    () => _playerManager?.Input);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.Ranking) is RankingWindow rankingWindow)
            {
                rankingWindow.SetFont(_fontChat);
                rankingWindow.SetSnapshotProvider(BuildUtilityRankingSnapshot);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.Event) is EventWindow eventWindow)
            {
                eventWindow.SetFont(_fontChat);
                eventWindow.SetSnapshotProvider(BuildUtilityEventSnapshot);
            }

            ApplyUtilityAudioSettings();
        }

        private void ShowUtilityOptionWindow(OptionMenuMode mode)
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.OptionMenu) is not OptionMenuWindow optionMenuWindow)
            {
                return;
            }

            optionMenuWindow.SetMode(mode);
            ShowWindow(
                MapSimulatorWindowNames.OptionMenu,
                optionMenuWindow,
                trackDirectionModeOwner: ShouldTrackInheritedDirectionModeOwner());
        }

        private void ShowUtilityWindow(string windowName)
        {
            ShowWindowWithInheritedDirectionModeOwner(windowName);
        }

        private void SetUtilityBgmMuted(bool muted)
        {
            _utilityBgmMuted = muted;
            ApplyUtilityAudioSettings();
        }

        private void SetUtilityEffectsMuted(bool muted)
        {
            _utilityEffectsMuted = muted;
            ApplyUtilityAudioSettings();
        }

        private void SetPauseAudioOnFocusLoss(bool enabled)
        {
            _pauseAudioOnFocusLoss = enabled;
            ApplyUtilityAudioSettings();
        }

        private void ApplyUtilityAudioSettings()
        {
            if (_audio != null)
            {
                _audio.Volume = (_utilityBgmMuted || IsPacketOwnedRadioPlaying()) ? 0f : 0.5f;
            }

            if (_packetOwnedRadioAudio != null)
            {
                _packetOwnedRadioAudio.Volume = _utilityBgmMuted ? 0f : 0.5f;
            }

            if (_soundManager != null)
            {
                _soundManager.Volume = _utilityEffectsMuted ? 0f : 0.5f;
                _soundManager.SetFocusActive(!_pauseAudioOnFocusLoss || IsActive);
            }

            if (_audio == null)
            {
                _isBgmPausedForFocusLoss = false;
                return;
            }

            if (_pauseAudioOnFocusLoss)
            {
                SyncBgmPlaybackToWindowFocus();
                return;
            }

            if (_isBgmPausedForFocusLoss)
            {
                _audio.Resume();
                _isBgmPausedForFocusLoss = false;
            }
            else if (_audio.State == Microsoft.Xna.Framework.Audio.SoundState.Stopped)
            {
                _audio.Play();
            }
        }

        private void WireSocialRoomWindowData()
        {
            if (uiWindowManager == null)

            {

                return;

            }



            InventoryUI inventoryWindow = uiWindowManager.InventoryWindow as InventoryUI;

            WireSocialRoomWindow(MapSimulatorWindowNames.MiniRoom, inventoryWindow, attachMiniRoomRuntime: true);

            WireSocialRoomWindow(MapSimulatorWindowNames.PersonalShop, inventoryWindow);

            WireSocialRoomWindow(MapSimulatorWindowNames.EntrustedShop, inventoryWindow);

            WireSocialRoomWindow(MapSimulatorWindowNames.TradingRoom, inventoryWindow);

        }



        private void WireCharacterInfoWindowActionRoutes(UserInfoUI userInfoWindow)



        {



            if (userInfoWindow == null)



            {



                return;



            }



            if (TryInitializeLoginCharacterRosterFromAccountStore())



            {



                return;



            }







            userInfoWindow.PartyRequested = HandleCharacterInfoPartyRequest;



            userInfoWindow.MiniRoomRequested = () => ShowSocialRoomWindow(SocialRoomKind.MiniRoom);



            userInfoWindow.PersonalShopRequested = () => ShowSocialRoomWindow(SocialRoomKind.PersonalShop);



            userInfoWindow.EntrustedShopRequested = () => ShowSocialRoomWindow(SocialRoomKind.EntrustedShop);

            userInfoWindow.TradingRoomRequested = HandleCharacterInfoTradingRoomRequest;

            userInfoWindow.FamilyRequested = HandleCharacterInfoFamilyRequest;

            userInfoWindow.PopularityRequested = (context, direction) => UserInfoPopularityPreviewService.HandleRequest(context, direction, _remoteUserPool);

            userInfoWindow.BookCollectionRequested = () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.BookCollection);

        }

        private string HandleCharacterInfoPartyRequest(UserInfoUI.UserInfoActionContext context)
        {
            if (!context.IsRemoteTarget)
            {
                _socialListRuntime.SelectTab(SocialListTab.Party);
                ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialList);
                return "Party list opened from the profile window.";
            }

            string locationSummary = string.IsNullOrWhiteSpace(context.LocationSummary)
                ? GetCurrentMapTransferDisplayName()
                : context.LocationSummary;
            string message = _socialListRuntime.InviteCharacterToParty(
                context.CharacterName,
                context.Build?.JobName,
                context.Build?.Level ?? 1,
                locationSummary,
                context.Channel);
            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialList);
            return message;
        }

        private string HandleCharacterInfoTradingRoomRequest(UserInfoUI.UserInfoActionContext context)
        {
            ShowSocialRoomWindow(SocialRoomKind.TradingRoom);
            if (!context.IsRemoteTarget)
            {
                return "Trading-room shell opened.";
            }

            return TryGetSocialRoomRuntime(SocialRoomKind.TradingRoom, out SocialRoomRuntime runtime)
                ? runtime.ConfigureTradeInviteTarget(context.CharacterName, context.Build?.Clone())
                : "Trading-room shell opened, but the simulator trade runtime is unavailable.";
        }

        private string HandleCharacterInfoFamilyRequest(UserInfoUI.UserInfoActionContext context)
        {
            if (!context.IsRemoteTarget)
            {
                _familyChartRuntime.ClearRemotePreviewRequest();
                ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.FamilyChart);
                return "Family chart opened from the profile window.";
            }

            string locationSummary = string.IsNullOrWhiteSpace(context.LocationSummary)
                ? GetCurrentMapTransferDisplayName()
                : context.LocationSummary;
            string message = _familyChartRuntime.PreviewRemoteFamilyRequest(
                context.CharacterName,
                context.Build,
                locationSummary,
                context.Channel);
            ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.FamilyChart);
            return message;
        }

        private bool TryShowRemoteCharacterInfoWindow(string characterSelector)
        {
            if (string.IsNullOrWhiteSpace(characterSelector))
            {
                return false;
            }

            RemoteUserActor actor = null;
            bool found = int.TryParse(characterSelector, out int characterId)
                ? _remoteUserPool.TryGetActor(characterId, out actor)
                : _remoteUserPool.TryGetActorByName(characterSelector, out actor);
            if (!found || actor?.Build == null)
            {
                return false;
            }

            ShowCharacterInfoWindow(
                inspectionTarget: new UserInfoUI.UserInfoInspectionTarget
                {
                    Build = actor.Build.Clone(),
                    CharacterId = actor.CharacterId,
                    Name = actor.Name,
                    LocationSummary = GetCurrentMapTransferDisplayName(),
                    Channel = 1
                });
            return true;
        }

        private UserInfoUI.RankDeltaSnapshot ResolveCharacterInfoRankDeltaSnapshot()
        {
            CharacterBuild build = _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;
            if (build == null)
            {
                return default;
            }

            LoginCharacterRosterEntry rosterEntry = ResolveLoginCharacterRosterEntry(build);
            if (rosterEntry != null)
            {
                return new UserInfoUI.RankDeltaSnapshot(rosterEntry.PreviousWorldRank, rosterEntry.PreviousJobRank);
            }

            LoginCharacterAccountStore.LoginCharacterAccountEntryState storedEntry = ResolveStoredLoginCharacterAccountEntry(build);
            return storedEntry != null
                ? new UserInfoUI.RankDeltaSnapshot(storedEntry.PreviousWorldRank, storedEntry.PreviousJobRank)
                : default;
        }

        private LoginCharacterRosterEntry ResolveLoginCharacterRosterEntry(CharacterBuild build)
        {
            if (build == null)
            {
                return null;
            }

            LoginCharacterRosterEntry selectedEntry = _loginCharacterRoster.SelectedEntry;
            if (IsSameCharacterBuild(selectedEntry?.Build, build))
            {
                return selectedEntry;
            }

            return _loginCharacterRoster.Entries.FirstOrDefault(entry => IsSameCharacterBuild(entry?.Build, build));
        }

        private LoginCharacterAccountStore.LoginCharacterAccountEntryState ResolveStoredLoginCharacterAccountEntry(CharacterBuild build)
        {
            if (build == null)
            {
                return null;
            }

            LoginCharacterAccountStore.LoginCharacterAccountState storedState = _loginCharacterAccountStore.GetState(
                ResolveLoginRosterAccountName(),
                ResolveLoginRosterWorldId());
            if (storedState?.Entries == null)
            {
                return null;
            }

            LoginCharacterAccountStore.LoginCharacterAccountEntryState entryById = storedState.Entries.FirstOrDefault(entry =>
                entry != null &&
                entry.CharacterId > 0 &&
                build.Id > 0 &&
                entry.CharacterId == build.Id);
            if (entryById != null)
            {
                return entryById;
            }

            return storedState.Entries.FirstOrDefault(entry =>
                entry != null &&
                string.Equals(entry.Name, build.Name, StringComparison.OrdinalIgnoreCase) &&
                entry.Job == build.Job &&
                entry.Level == build.Level);
        }

        private static bool IsSameCharacterBuild(CharacterBuild left, CharacterBuild right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (left.Id > 0 && right.Id > 0)
            {
                return left.Id == right.Id;
            }

            return string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) &&
                   left.Job == right.Job &&
                   left.Level == right.Level;
        }






        private void WireSocialRoomWindow(string windowName, InventoryUI inventoryWindow, bool attachMiniRoomRuntime = false)

        {

            if (uiWindowManager?.GetWindow(windowName) is not SocialRoomWindow roomWindow)

            {

                return;

            }



            roomWindow.Runtime.BindInventory(inventoryWindow);

            roomWindow.SetFont(_fontChat);
            ConfigureSocialRoomPersistence(roomWindow.Runtime);
            if (attachMiniRoomRuntime)

            {

                MemoryGameField memoryGame = _specialFieldRuntime.Minigames.MemoryGame;

                memoryGame.AttachMiniRoomRuntime(roomWindow.Runtime);

                memoryGame.SetMiniRoomAvatarBuildFactory(CreateMiniRoomAvatarBuild);

                memoryGame.SetLocalMiniRoomAvatar(_playerManager?.Player?.Build);

            }

        }



        private void ConfigureSocialRoomPersistence(SocialRoomRuntime runtime)
        {
            if (runtime == null || _socialRoomPersistenceStore == null)
            {
                return;
            }

            string key = BuildSocialRoomPersistenceKey(runtime);
            SocialRoomRuntimeSnapshot snapshot = _socialRoomPersistenceStore.Load(key);
            runtime.ConfigurePersistence(key, SaveSocialRoomSnapshot, snapshot);
        }

        private void SaveSocialRoomSnapshot(string key, SocialRoomRuntimeSnapshot snapshot)
        {
            _socialRoomPersistenceStore?.Save(key, snapshot);
        }

        private string BuildSocialRoomPersistenceKey(SocialRoomRuntime runtime)
        {
            string accountName = string.IsNullOrWhiteSpace(_loginTitleAccountName)
                ? "simulator"
                : _loginTitleAccountName.Trim();
            string characterName = _playerManager?.Player?.Build?.Name;
            if (string.IsNullOrWhiteSpace(characterName))
            {
                characterName = "ExplorerGM";
            }

            return $"login:{accountName}|world:{_simulatorWorldId}|character:{characterName}|room:{runtime.Kind}";
        }

        private CharacterBuild CreateMiniRoomAvatarBuild(LoginAvatarLook avatarLook)
        {

            if (avatarLook == null)

            {

                return null;

            }



            CharacterLoader loader = _playerManager?.Loader;

            return loader?.LoadFromAvatarLook(avatarLook);

        }



        private void WireMiniRoomWindowData()

        {

            WireSocialRoomWindow(MapSimulatorWindowNames.MiniRoom, uiWindowManager?.InventoryWindow as InventoryUI, attachMiniRoomRuntime: true);

        }



        private void ShowMiniRoomWindow()

        {
            TryShowMiniRoomWindow(out _);

        }



        private static string GetSocialRoomWindowName(SocialRoomKind kind)

        {

            return kind switch

            {

                SocialRoomKind.MiniRoom => MapSimulatorWindowNames.MiniRoom,

                SocialRoomKind.PersonalShop => MapSimulatorWindowNames.PersonalShop,

                SocialRoomKind.EntrustedShop => MapSimulatorWindowNames.EntrustedShop,

                SocialRoomKind.TradingRoom => MapSimulatorWindowNames.TradingRoom,

                _ => null

            };

        }



        private bool TryGetSocialRoomRuntime(SocialRoomKind kind, out SocialRoomRuntime runtime)

        {

            runtime = null;

            string windowName = GetSocialRoomWindowName(kind);

            if (string.IsNullOrWhiteSpace(windowName))

            {

                return false;

            }



            if (kind == SocialRoomKind.MiniRoom)

            {

                WireMiniRoomWindowData();

            }

            else

            {

                WireSocialRoomWindow(windowName, uiWindowManager?.InventoryWindow as InventoryUI);

            }



            runtime = (uiWindowManager?.GetWindow(windowName) as SocialRoomWindow)?.Runtime;

            return runtime != null;

        }



        private SocialRoomFieldActorSnapshot GetSocialRoomEmployeeFieldActorSnapshot()

        {

            SocialRoomKind[] searchOrder =

            {

                SocialRoomKind.EntrustedShop,

                SocialRoomKind.PersonalShop

            };



            foreach (SocialRoomKind kind in searchOrder)

            {

                if (!TryGetSocialRoomRuntime(kind, out SocialRoomRuntime visibleRuntime))

                {

                    continue;

                }



                string windowName = GetSocialRoomWindowName(kind);

                if (uiWindowManager?.GetWindow(windowName)?.IsVisible != true)

                {

                    continue;

                }



                SocialRoomFieldActorSnapshot visibleSnapshot = visibleRuntime.GetFieldActorSnapshot(DateTime.UtcNow);

                if (visibleSnapshot != null)

                {

                    return visibleSnapshot;

                }

            }



            foreach (SocialRoomKind kind in searchOrder)

            {

                if (!TryGetSocialRoomRuntime(kind, out SocialRoomRuntime runtime))

                {

                    continue;

                }



                SocialRoomFieldActorSnapshot snapshot = runtime.GetFieldActorSnapshot(DateTime.UtcNow);

                if (snapshot != null)

                {

                    return snapshot;

                }

            }



            return null;

        }



        private void ShowSocialRoomWindow(SocialRoomKind kind)
        {
            TryShowSocialRoomWindow(kind, out _);

        }



        private void WireItemUpgradeWindowLaunchers()

        {

            if (uiWindowManager == null)

            {

                return;

            }



            if (uiWindowManager.InventoryWindow is InventoryUI inventoryWindow)

            {

                inventoryWindow.ItemUpgradeRequested = OpenItemUpgradeWindowForConsumable;

                inventoryWindow.ItemUseRequested = TryUseInventoryItem;

                inventoryWindow.CashShopRequested = ShowCashShopWindow;

            }



            switch (uiWindowManager.EquipWindow)

            {

                case EquipUI equipWindow:

                    equipWindow.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipment;

                    break;

                case EquipUIBigBang equipWindowBigBang:

                    equipWindowBigBang.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipment;
                    equipWindowBigBang.EquipmentChangeSubmitted = SubmitEquipmentChangeRequest;
                    equipWindowBigBang.EquipmentChangeResultRequested = TryResolveEquipmentChangeRequest;

                    equipWindowBigBang.EquipmentEquipGuard = GetBattlefieldEquipRestrictionMessage;

                    equipWindowBigBang.EquipmentEquipBlocked = ShowFieldRestrictionMessage;

                    break;

            }

        }



        private void OpenItemUpgradeWindowForConsumable(int itemId)

        {

            if (ItemUpgradeUI.IsVegaSpellConsumable(itemId))

            {

                OpenVegaSpellWindowForConsumable(itemId);

                return;

            }



            if (!TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow))

            {

                return;

            }



            itemUpgradeWindow.PrepareConsumableSelection(itemId);

        }



        private void OpenVegaSpellWindowForConsumable(int itemId)

        {

            if (!TryShowVegaSpellWindow(out VegaSpellUI vegaSpellWindow))

            {

                return;

            }



            vegaSpellWindow.PrepareModifierSelection(itemId);

        }



        private bool TryUseInventoryItem(int itemId, InventoryType inventoryType)

        {

            return TryUseInventoryItem(itemId, inventoryType, currTickCount);

        }



        private bool TryUseInventoryItem(int itemId, InventoryType inventoryType, int currentTime)

        {

            if (TryUsePetFoodInventoryItem(itemId, inventoryType, currentTime))



            {



                return true;



            }



            bool used = inventoryType switch
            {
                InventoryType.SETUP => TryTogglePortableChair(itemId, out _),
                InventoryType.USE => TryUseConsumableInventoryItem(itemId, inventoryType, currentTime),
                InventoryType.CASH => TryUseCashInventoryItem(itemId)
                                      || TryUseConsumableInventoryItem(itemId, inventoryType, currentTime),
                _ => false
            };


            if (TryTriggerItemReactorFromInventoryUse(itemId, inventoryType, currentTime, used))

            {

                return true;

            }



            return used;

        }



        private bool TryTriggerItemReactorFromInventoryUse(int itemId, InventoryType inventoryType, int currentTime, bool itemAlreadyConsumed)

        {

            if (itemId <= 0

                || inventoryType == InventoryType.NONE

                || _reactorPool == null

                || _playerManager?.Player == null)

            {

                return false;

            }



            string restrictionMessage = GetFieldItemUseRestrictionMessage(inventoryType, itemId, 1);

            if (!string.IsNullOrWhiteSpace(restrictionMessage))

            {

                return false;

            }



            float playerX = _playerManager.Player.X;

            float playerY = _playerManager.Player.Y;

            var matchingReactors = _reactorPool.FindItemReactorsAroundLocalUser(playerX, playerY, itemId, currentTick: currentTime);

            if (matchingReactors.Count == 0)

            {

                return false;

            }



            if (!itemAlreadyConsumed)

            {

                if (uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventoryRuntime

                    || !inventoryRuntime.TryConsumeItem(inventoryType, itemId, 1))

                {

                    return false;

                }

            }



            _reactorPool.TriggerItemReactorsAroundLocalUser(playerX, playerY, itemId, playerId: 0, currentTime);

            return true;

        }



        private bool TryUseCashInventoryItem(int itemId)

        {

            if (itemId <= 0 || uiWindowManager == null)

            {

                return false;

            }



            InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName);

            InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string itemDescription);

            if (!TeleportItemUsageEvaluator.IsTeleportItem(itemId, InventoryType.CASH, itemName, itemDescription))

            {

                return false;

            }



            string restrictionMessage = FieldInteractionRestrictionEvaluator.GetTeleportItemRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);

            if (!string.IsNullOrWhiteSpace(restrictionMessage))

            {

                ShowFieldRestrictionMessage(restrictionMessage);

                return false;

            }



            _worldMapRequestMode = WorldMapRequestMode.DirectTransfer;

            _mapTransferEditDestination = null;

            RefreshMapTransferWindow();

            uiWindowManager.HideWindow(MapSimulatorWindowNames.WorldMap);

            uiWindowManager.ShowWindow(MapSimulatorWindowNames.MapTransfer);

            return true;

        }



        private bool TryUsePetFoodInventoryItem(int itemId, InventoryType inventoryType, int currentTime)



        {



            if (itemId <= 0 ||



                (inventoryType != InventoryType.USE && inventoryType != InventoryType.CASH) ||



                uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventoryWindow ||



                _playerManager?.Pets == null)



            {



                return false;



            }







            PetFoodItemEffect effect = ResolvePetFoodItemEffect(itemId);



            if (!effect.IsPetFood)



            {



                return false;



            }







            string fieldItemRestrictionMessage = GetFieldItemUseRestrictionMessage(inventoryType, itemId, 1);



            if (!string.IsNullOrWhiteSpace(fieldItemRestrictionMessage))



            {



                ShowFieldRestrictionMessage(fieldItemRestrictionMessage);



                return false;



            }







            if (inventoryWindow.GetItemCount(inventoryType, itemId) <= 0)



            {



                return false;



            }







            if (!_playerManager.Pets.TryPlanFoodItemUse(

                    effect.SupportedPetItemIds,

                    effect.FullnessIncrease,

                    out PetController.PetFoodItemUsePlan foodPlan))

            {
                PushFieldRuleMessage(
                    _playerManager.Pets.ActivePets.Count == 0
                        ? "Summon a pet before using that pet food."
                        : effect.SupportedPetItemIds.Length > 0
                            ? "None of the summoned pets can eat that pet food."
                            : "None of the summoned pets want that pet food right now.",
                    currentTime,
                    false);

                return false;

            }






            if (foodPlan.ConsumeItem && !inventoryWindow.TryConsumeItem(inventoryType, itemId, 1))



            {



                return false;



            }







            if (!_playerManager.Pets.TryExecuteFoodItemUse(foodPlan, currentTime, out _))



            {



                return false;



            }







            if (foodPlan.ConsumeItem && inventoryType == InventoryType.USE)



            {



                _fieldRuleRuntime?.RegisterSuccessfulItemUse(InventoryType.USE, currentTime);



            }







            return true;



        }



        private void ShowCashShopWindow()

        {

            SyncCashShopAccountCredit();

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.Mts);

            ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.CashShop);

        }



        private void OpenItemUpgradeWindowForEquipment(Character.EquipSlot slot)

        {

            if (!TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow))

            {

                return;

            }



            itemUpgradeWindow.PrepareEquipmentSelection(slot);

        }

        private bool TryShowRepairDurabilityWindow(int npcTemplateId, out RepairDurabilityWindow repairWindow, bool trackDirectionModeOwner = false)

        {

            repairWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.RepairDurability) as RepairDurabilityWindow;

            if (repairWindow == null)

            {

                return false;

            }



            RefreshRepairDurabilityWindow(npcTemplateId);

            ShowWindow(MapSimulatorWindowNames.RepairDurability, repairWindow, trackDirectionModeOwner);

            return true;

        }

        private sealed class PendingRepairDurabilityRequest
        {
            public long SentTick { get; init; }
            public int NpcTemplateId { get; init; }
            public short OperationCode { get; init; }
            public bool RepairAll { get; init; }
            public int TotalCost { get; init; }
            public int PreferredItemId { get; init; }
            public int EncodedSlotPosition { get; init; }
            public RepairDurabilityWindow.RepairEntry Entry { get; init; }
            public IReadOnlyList<RepairDurabilityWindow.RepairEntry> Entries { get; init; }
            public string RequestLabel { get; init; } = string.Empty;
        }

        private const int RepairDurabilityResponseDelayMs = 120;
        private const short RepairDurabilityAllOpcode = 130;
        private const short RepairDurabilitySingleOpcode = 131;



        private void RefreshRepairDurabilityWindow(int npcTemplateId, int preferredItemId = 0)

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RepairDurability) is not RepairDurabilityWindow repairWindow)

            {

                return;

            }



            int effectiveNpcTemplateId = npcTemplateId > 0 ? npcTemplateId : repairWindow.NpcTemplateId;
            int previousNpcTemplateId = repairWindow.NpcTemplateId;

            repairWindow.ConfigureNpc(effectiveNpcTemplateId, ResolveNpcDisplayName(effectiveNpcTemplateId));
            if (effectiveNpcTemplateId <= 0)
            {
                repairWindow.SetNpcPreview(null);
            }
            else if (effectiveNpcTemplateId != previousNpcTemplateId || !repairWindow.HasNpcPreview)
            {
                repairWindow.SetNpcPreview(CreateRepairDurabilityNpcPreview(effectiveNpcTemplateId));
            }

            repairWindow.SetEntries(BuildRepairDurabilityEntries(), preferredItemId);
            repairWindow.SetAwaitingRepairResponse(_pendingRepairDurabilityRequest != null);
            repairWindow.SetStatusMessage(_pendingRepairDurabilityRequest?.RequestLabel ?? GetRepairDurabilityStatusMessage());

        }



        private string GetRepairDurabilityStatusMessage()

        {

            if (_playerManager?.Player?.Build == null)

            {

                return "No player build is bound to the repair owner.";

            }



            int repairableCount = BuildRepairDurabilityEntries().Count;

            if (repairableCount <= 0)

            {

                return "No visible or hidden equipped item currently needs durability repair.";

            }



            long mesoCount = (uiWindowManager?.InventoryWindow as IInventoryRuntime)?.GetMesoCount() ?? 0;

            return $"Repairable items: {repairableCount}. Available meso: {mesoCount.ToString("N0", CultureInfo.InvariantCulture)}.";

        }



        private List<RepairDurabilityWindow.RepairEntry> BuildRepairDurabilityEntries()

        {

            var entries = new List<RepairDurabilityWindow.RepairEntry>();

            CharacterBuild build = _playerManager?.Player?.Build;

            if (build == null)

            {

                return entries;

            }



            AppendRepairDurabilityEntries(entries, build.Equipment, hiddenSlot: false);

            AppendRepairDurabilityEntries(entries, build.HiddenEquipment, hiddenSlot: true);

            return entries

                .OrderBy(entry => GetRepairDurabilitySortKey(entry.Slot, entry.IsHiddenSlot))

                .ThenBy(entry => entry.Part?.ItemId ?? 0)

                .ToList();

        }

        private static int GetRepairDurabilitySortKey(Character.EquipSlot slot, bool hiddenSlot)
        {
            int baseOrder = slot switch
            {
                Character.EquipSlot.Cap => 1,
                Character.EquipSlot.FaceAccessory => 2,
                Character.EquipSlot.EyeAccessory => 3,
                Character.EquipSlot.Earrings => 4,
                Character.EquipSlot.Coat => 5,
                Character.EquipSlot.Longcoat => 5,
                Character.EquipSlot.Pants => 6,
                Character.EquipSlot.Shoes => 7,
                Character.EquipSlot.Glove => 8,
                Character.EquipSlot.Cape => 9,
                Character.EquipSlot.Shield => 10,
                Character.EquipSlot.Weapon => 11,
                Character.EquipSlot.Ring1 => 12,
                Character.EquipSlot.Ring2 => 13,
                Character.EquipSlot.Ring3 => 15,
                Character.EquipSlot.Ring4 => 16,
                Character.EquipSlot.Pendant => 17,
                Character.EquipSlot.TamingMob => 18,
                Character.EquipSlot.Saddle => 19,
                Character.EquipSlot.Medal => 49,
                Character.EquipSlot.Belt => 50,
                Character.EquipSlot.Shoulder => 51,
                Character.EquipSlot.Pocket => 52,
                Character.EquipSlot.Badge => 53,
                Character.EquipSlot.Pendant2 => 59,
                Character.EquipSlot.Android => 166,
                Character.EquipSlot.AndroidHeart => 167,
                _ => 1000 + (int)slot
            };

            return (hiddenSlot ? 10000 : 0) + baseOrder;
        }



        private static void AppendRepairDurabilityEntries(

            ICollection<RepairDurabilityWindow.RepairEntry> entries,

            IReadOnlyDictionary<Character.EquipSlot, CharacterPart> equipment,

            bool hiddenSlot)

        {

            if (entries == null || equipment == null)

            {

                return;

            }



            foreach ((Character.EquipSlot slot, CharacterPart part) in equipment)

            {

                if (part == null || !part.Durability.HasValue || !part.MaxDurability.HasValue)

                {

                    continue;

                }



                int maxDurability = Math.Max(0, part.MaxDurability.Value);

                int currentDurability = Math.Clamp(part.Durability.Value, 0, maxDurability);

                if (maxDurability <= 0 || currentDurability >= maxDurability)

                {

                    continue;

                }



                entries.Add(new RepairDurabilityWindow.RepairEntry

                {

                    Part = part,

                    EncodedSlotPosition = EncodeRepairDurabilitySlotPosition(slot, hiddenSlot, part.IsCash),
                    Slot = slot,

                    IsHiddenSlot = hiddenSlot,

                    IsInventorySlot = false,
                    SlotLabel = BuildRepairDurabilitySlotLabel(slot, hiddenSlot),

                    ItemName = string.IsNullOrWhiteSpace(part.Name) ? $"Item {part.ItemId}" : part.Name,

                    CurrentDurability = currentDurability,

                    MaxDurability = maxDurability,

                    RepairCost = CalculateRepairDurabilityCost(part, currentDurability, maxDurability),

                    IsCashItem = part.IsCash,

                    AvailabilityText = currentDurability <= 0 ? "Broken" : "Repairable",

                    Icon = part.IconRaw ?? part.Icon

                });

            }

        }



        private static string BuildRepairDurabilitySlotLabel(Character.EquipSlot slot, bool hiddenSlot)

        {

            string label = slot switch

            {

                Character.EquipSlot.FaceAccessory => "Face",

                Character.EquipSlot.EyeAccessory => "Eyes",

                Character.EquipSlot.Longcoat => "Overall",

                Character.EquipSlot.Pendant2 => "Pendant2",

                Character.EquipSlot.Ring1 => "Ring1",

                Character.EquipSlot.Ring2 => "Ring2",

                Character.EquipSlot.Ring3 => "Ring3",

                Character.EquipSlot.Ring4 => "Ring4",

                Character.EquipSlot.AndroidHeart => "Heart",

                Character.EquipSlot.TamingMob => "Mount",

                _ => slot.ToString()

            };

            return hiddenSlot ? $"{label} hidden" : label;

        }



        private static int CalculateRepairDurabilityCost(CharacterPart part, int currentDurability, int maxDurability)

        {

            if (part == null || maxDurability <= 0 || currentDurability >= maxDurability)

            {

                return 0;

            }



            int level = Math.Max(1, part.RequiredLevel);

            double sellPrice = part.SellPrice > 0

                ? part.SellPrice

                : Math.Max(1d, level * level * 2d);

            double lostPercent = 100d - ((100d * currentDurability) / maxDurability);

            double durabilityScale = maxDurability / 100d;

            if (durabilityScale <= 0d)

            {

                return 0;

            }



            double epicMultiplier = part.IsEpic ? 1.25d : 1d;

            double cost = lostPercent * (((double)level * level) * (sellPrice / 50d) / durabilityScale) * epicMultiplier;

            if (double.IsNaN(cost) || double.IsInfinity(cost))

            {

                return 0;

            }



            return Math.Max(0, (int)Math.Round(cost));

        }



        private void HandleRepairDurabilityRequested(RepairDurabilityWindow.RepairEntry entry)

        {

            if (entry?.Part == null)

            {

                return;

            }



            IInventoryRuntime inventory = uiWindowManager?.InventoryWindow as IInventoryRuntime;

            if (inventory == null)

            {

                ShowUtilityFeedbackMessage("Durability repair is unavailable because the inventory runtime is not active.");

                return;

            }



            if (entry.RepairCost <= 0)

            {

                ShowUtilityFeedbackMessage($"{entry.ItemName} does not currently need durability repair.");

                return;

            }



            if (_pendingRepairDurabilityRequest != null)
            {
                ShowUtilityFeedbackMessage("A durability repair request is already awaiting a result.");
                RefreshRepairDurabilityWindow(0, entry.Part.ItemId);
                return;
            }

            if (inventory.GetMesoCount() < entry.RepairCost)

            {

                ShowUtilityFeedbackMessage($"Not enough meso to repair {entry.ItemName}.");

                RefreshRepairDurabilityWindow(0, entry.Part.ItemId);

                return;

            }



            int npcTemplateId = GetActiveRepairDurabilityNpcTemplateId();
            string npcName = ResolveNpcDisplayName(npcTemplateId);
            string requestLabel = $"Sent repair request for {entry.ItemName} to {npcName}.";
            _pendingRepairDurabilityRequest = new PendingRepairDurabilityRequest
            {
                SentTick = Environment.TickCount64,
                NpcTemplateId = npcTemplateId,
                OperationCode = RepairDurabilitySingleOpcode,
                RepairAll = false,
                TotalCost = entry.RepairCost,
                PreferredItemId = entry.Part.ItemId,
                EncodedSlotPosition = entry.EncodedSlotPosition,
                Entry = entry,
                Entries = new[] { entry },
                RequestLabel = requestLabel
            };

            ShowUtilityFeedbackMessage(requestLabel);
            RefreshRepairDurabilityWindow(npcTemplateId, entry.Part.ItemId);

        }



        private void HandleRepairDurabilityAllRequested(IReadOnlyList<RepairDurabilityWindow.RepairEntry> entries)

        {

            if (entries == null || entries.Count == 0)

            {

                ShowUtilityFeedbackMessage("No equipped item currently needs durability repair.");

                return;

            }



            IInventoryRuntime inventory = uiWindowManager?.InventoryWindow as IInventoryRuntime;

            if (inventory == null)

            {

                ShowUtilityFeedbackMessage("Durability repair is unavailable because the inventory runtime is not active.");

                return;

            }



            int totalCost = entries.Sum(entry => Math.Max(0, entry?.RepairCost ?? 0));

            if (totalCost <= 0)

            {

                ShowUtilityFeedbackMessage("No equipped item currently needs durability repair.");

                return;

            }



            if (_pendingRepairDurabilityRequest != null)
            {
                ShowUtilityFeedbackMessage("A durability repair request is already awaiting a result.");
                RefreshRepairDurabilityWindow(0);
                return;
            }

            if (inventory.GetMesoCount() < totalCost)

            {

                ShowUtilityFeedbackMessage("Not enough meso to repair every damaged equipped item.");

                RefreshRepairDurabilityWindow(0);

                return;

            }



            int npcTemplateId = GetActiveRepairDurabilityNpcTemplateId();
            string npcName = ResolveNpcDisplayName(npcTemplateId);
            string requestLabel = $"Sent repair-all request for {entries.Count} item(s) to {npcName}.";
            _pendingRepairDurabilityRequest = new PendingRepairDurabilityRequest
            {
                SentTick = Environment.TickCount64,
                NpcTemplateId = npcTemplateId,
                OperationCode = RepairDurabilityAllOpcode,
                RepairAll = true,
                TotalCost = totalCost,
                Entries = entries.Where(candidate => candidate?.Part != null).ToArray(),
                RequestLabel = requestLabel
            };

            ShowUtilityFeedbackMessage(requestLabel);
            RefreshRepairDurabilityWindow(npcTemplateId);

        }



        private int GetActiveRepairDurabilityNpcTemplateId()
        {
            return uiWindowManager?.GetWindow(MapSimulatorWindowNames.RepairDurability) is RepairDurabilityWindow repairWindow
                ? repairWindow.NpcTemplateId
                : 0;
        }

        private NpcItem CreateNpcPreview(int npcTemplateId, bool includeTooltips = false)
        {
            if (npcTemplateId <= 0 || GraphicsDevice == null)
            {
                return null;
            }

            NpcInfo npcInfo = NpcInfo.Get(npcTemplateId.ToString(CultureInfo.InvariantCulture));
            if (npcInfo == null)
            {
                return null;
            }

            var npcInstance = new NpcInstance(
                npcInfo,
                null,
                0,
                0,
                0,
                0,
                0,
                null,
                0,
                false,
                false,
                null,
                null);
            var usedProps = new ConcurrentBag<WzObject>();
            NpcItem npcPreview = LifeLoader.CreateNpcFromProperty(_texturePool, npcInstance, UserScreenScaleFactor, GraphicsDevice, usedProps, includeTooltips);
            npcPreview?.SetAction(AnimationKeys.Stand);
            return npcPreview;
        }

        private NpcItem CreateRepairDurabilityNpcPreview(int npcTemplateId)
        {
            return CreateNpcPreview(npcTemplateId, includeTooltips: false);
        }

        private void ProcessPendingRepairDurabilityRequest()
        {
            PendingRepairDurabilityRequest request = _pendingRepairDurabilityRequest;
            if (request == null)
            {
                return;
            }

            if ((Environment.TickCount64 - request.SentTick) < RepairDurabilityResponseDelayMs)
            {
                return;
            }

            _pendingRepairDurabilityRequest = null;

            IInventoryRuntime inventory = uiWindowManager?.InventoryWindow as IInventoryRuntime;
            if (inventory == null)
            {
                ShowUtilityFeedbackMessage("Durability repair response failed because the inventory runtime is not active.");
                RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                return;
            }

            if (request.TotalCost <= 0)
            {
                RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                return;
            }

            if (!request.RepairAll)
            {
                RepairDurabilityWindow.RepairEntry liveEntry = FindLiveRepairDurabilityEntry(request.EncodedSlotPosition, request.PreferredItemId);
                if (liveEntry?.Part == null || liveEntry.MaxDurability <= 0)
                {
                    ShowUtilityFeedbackMessage("Durability repair response failed because the selected equipment is no longer available.");
                    RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                    return;
                }

                int liveRepairCost = Math.Max(0, liveEntry.RepairCost);
                if (liveRepairCost <= 0)
                {
                    ShowUtilityFeedbackMessage($"{liveEntry.ItemName} no longer needs durability repair.");
                    RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                    return;
                }

                if (!inventory.TryConsumeMeso(liveRepairCost))
                {
                    ShowUtilityFeedbackMessage($"Repair response for {liveEntry.ItemName} failed because you no longer have enough meso.");
                    RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                    return;
                }

                liveEntry.Part.Durability = liveEntry.MaxDurability;
                ShowUtilityFeedbackMessage($"Repair response restored {liveEntry.ItemName} for {liveRepairCost.ToString("N0", CultureInfo.InvariantCulture)} meso.");
                RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                return;
            }

            int repairedCount = 0;
            List<RepairDurabilityWindow.RepairEntry> liveEntries = BuildRepairDurabilityEntries();
            if (liveEntries.Count <= 0)
            {
                ShowUtilityFeedbackMessage("Repair-all response failed because none of the requested equipment entries are still available.");
                RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                return;
            }

            int liveRepairAllCost = liveEntries.Sum(entry => Math.Max(0, entry?.RepairCost ?? 0));
            if (liveRepairAllCost <= 0)
            {
                ShowUtilityFeedbackMessage("Repair-all response found no remaining damaged equipment.");
                RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                return;
            }

            if (!inventory.TryConsumeMeso(liveRepairAllCost))
            {
                ShowUtilityFeedbackMessage("Repair-all response failed because you no longer have enough meso.");
                RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
                return;
            }

            for (int i = 0; i < liveEntries.Count; i++)
            {
                RepairDurabilityWindow.RepairEntry repairEntry = liveEntries[i];
                if (repairEntry?.Part == null || repairEntry.MaxDurability <= 0)
                {
                    continue;
                }

                repairEntry.Part.Durability = repairEntry.MaxDurability;
                repairedCount++;
            }

            ShowUtilityFeedbackMessage($"Repair-all response restored {repairedCount} item(s) for {liveRepairAllCost.ToString("N0", CultureInfo.InvariantCulture)} meso.");
            RefreshRepairDurabilityWindow(request.NpcTemplateId, request.PreferredItemId);
        }

        private static string ResolveNpcDisplayName(int npcTemplateId)

        {

            string key = npcTemplateId.ToString(CultureInfo.InvariantCulture);

            return npcTemplateId > 0

                   && Program.InfoManager?.NpcNameCache != null

                   && Program.InfoManager.NpcNameCache.TryGetValue(key, out Tuple<string, string> npcInfo)

                   && !string.IsNullOrWhiteSpace(npcInfo?.Item1)

                ? npcInfo.Item1

                : npcTemplateId > 0

                    ? $"NPC #{npcTemplateId}"

                    : "Repair NPC";

        }



        private bool TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow, bool trackDirectionModeOwner = false)

        {

            itemUpgradeWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) as ItemUpgradeUI;

            if (itemUpgradeWindow == null)

            {

                return false;

            }



            ShowWindow(MapSimulatorWindowNames.ItemUpgrade, itemUpgradeWindow, trackDirectionModeOwner);

            return true;

        }



        private bool TryShowVegaSpellWindow(out VegaSpellUI vegaSpellWindow)

        {

            vegaSpellWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.VegaSpell) as VegaSpellUI;

            if (vegaSpellWindow == null)

            {

                return false;

            }



            bool trackDirectionModeOwner = _scriptedDirectionModeWindows.IsTracking(MapSimulatorWindowNames.ItemUpgrade);

            ShowWindow(MapSimulatorWindowNames.VegaSpell, vegaSpellWindow, trackDirectionModeOwner);

            return true;

        }



        private void ShowDirectionModeOwnedWindow(string windowName)

        {

            if (uiWindowManager == null || string.IsNullOrWhiteSpace(windowName))

            {

                return;

            }

            if (!TryShowFieldRestrictedWindow(windowName))
            {
                return;
            }



            _scriptedDirectionModeWindows.TrackWindow(windowName);

            uiWindowManager.ShowWindow(windowName);

        }



        private void ShowWindow(string windowName, UIWindowBase window, bool trackDirectionModeOwner)

        {

            if (window == null || string.IsNullOrWhiteSpace(windowName))

            {

                return;

            }

            if (!TryShowFieldRestrictedWindow(windowName))
            {
                return;
            }



            if (trackDirectionModeOwner)

            {

                _scriptedDirectionModeWindows.TrackWindow(windowName);

            }



            if (uiWindowManager != null)

            {

                uiWindowManager.ShowWindow(window);

                return;

            }



            window.Show();

        }

        private static int EncodeRepairDurabilitySlotPosition(Character.EquipSlot slot, bool hiddenSlot, bool isCashItem)
        {
            int absolutePosition = slot switch
            {
                Character.EquipSlot.Cap => 1,
                Character.EquipSlot.FaceAccessory => 2,
                Character.EquipSlot.EyeAccessory => 3,
                Character.EquipSlot.Earrings => 4,
                Character.EquipSlot.Coat => 5,
                Character.EquipSlot.Longcoat => 5,
                Character.EquipSlot.Pants => 6,
                Character.EquipSlot.Shoes => 7,
                Character.EquipSlot.Glove => 8,
                Character.EquipSlot.Cape => 9,
                Character.EquipSlot.Shield => 10,
                Character.EquipSlot.Weapon => 11,
                Character.EquipSlot.Ring1 => 12,
                Character.EquipSlot.Ring2 => 13,
                Character.EquipSlot.Ring3 => 15,
                Character.EquipSlot.Ring4 => 16,
                Character.EquipSlot.Pendant => 17,
                Character.EquipSlot.TamingMob => 18,
                Character.EquipSlot.Saddle => 19,
                Character.EquipSlot.Medal => 49,
                Character.EquipSlot.Belt => 50,
                Character.EquipSlot.Shoulder => 51,
                Character.EquipSlot.Pocket => 52,
                Character.EquipSlot.Badge => 53,
                Character.EquipSlot.Pendant2 => 59,
                Character.EquipSlot.Android => 166,
                Character.EquipSlot.AndroidHeart => 167,
                _ => 1000 + (int)slot
            };

            if (hiddenSlot && !isCashItem)
            {
                return -absolutePosition;
            }

            return isCashItem ? -(100 + absolutePosition) : -absolutePosition;
        }

        private RepairDurabilityWindow.RepairEntry FindLiveRepairDurabilityEntry(int encodedSlotPosition, int preferredItemId)
        {
            List<RepairDurabilityWindow.RepairEntry> liveEntries = BuildRepairDurabilityEntries();
            RepairDurabilityWindow.RepairEntry positionMatch = liveEntries.FirstOrDefault(entry => entry.EncodedSlotPosition == encodedSlotPosition);
            if (positionMatch != null)
            {
                return positionMatch;
            }

            return preferredItemId > 0
                ? liveEntries.FirstOrDefault(entry => entry.Part?.ItemId == preferredItemId)
                : null;
        }



        private void RegisterStatusBarPopupUtilityWindows(WzImage uiStatus2BarImage, WzImage uiBasicImage, WzImage soundUIImage)

        {

            if (uiWindowManager == null || uiStatus2BarImage == null)

            {

                return;

            }



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.Menu) == null)

            {

                StatusBarPopupMenuWindow menuWindow = UILoader.CreateStatusBarPopupMenuWindow(

                    uiStatus2BarImage,

                    uiBasicImage,

                    soundUIImage,

                    GraphicsDevice,

                    MapSimulatorWindowNames.Menu,

                    new Point(Math.Max(16, _renderParams.RenderWidth - 96), Math.Max(16, _renderParams.RenderHeight - 300)));

                if (menuWindow != null)

                {

                    uiWindowManager.RegisterCustomWindow(menuWindow);

                }

            }



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.System) == null)

            {

                StatusBarPopupMenuWindow systemWindow = UILoader.CreateStatusBarPopupMenuWindow(

                    uiStatus2BarImage,

                    uiBasicImage,

                    soundUIImage,

                    GraphicsDevice,

                    MapSimulatorWindowNames.System,

                    new Point(Math.Max(16, _renderParams.RenderWidth - 96), Math.Max(16, _renderParams.RenderHeight - 244)));

                if (systemWindow != null)

                {

                    uiWindowManager.RegisterCustomWindow(systemWindow);

                }

            }

        }



        private void WireStatusBarPopupUtilityWindows()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Menu) is StatusBarPopupMenuWindow menuWindow)

            {

                menuWindow.BindEntryAction("BtItem", () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Inventory));

                menuWindow.BindEntryAction("BtEquip", () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Equipment));

                menuWindow.BindEntryAction("BtStat", () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Ability));

                menuWindow.BindEntryAction("BtSkill", () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Skills));

                menuWindow.BindEntryAction("BtQuest", () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Quest));

                menuWindow.BindEntryAction("BtCommunity", () => ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.SocialList));

                menuWindow.BindEntryAction("BtMSN", ShowMessengerWindow);

                menuWindow.BindEntryAction("BtRank", () => ShowUtilityWindow(MapSimulatorWindowNames.Ranking));
                menuWindow.BindEntryAction("BtEvent", () => ShowUtilityWindow(MapSimulatorWindowNames.Event));
            }



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.System) is StatusBarPopupMenuWindow systemWindow)

            {

                systemWindow.BindEntryAction("BtChannel", HandleUtilityChannelPopupRequested);

                systemWindow.BindEntryAction("BtKeySetting", () => ShowUtilityWindow(MapSimulatorWindowNames.KeyConfig));
                systemWindow.BindEntryAction("BtGameOption", () => ShowUtilityOptionWindow(OptionMenuMode.Game));
                systemWindow.BindEntryAction("BtSystemOption", () => ShowUtilityOptionWindow(OptionMenuMode.System));
                systemWindow.BindEntryAction("BtGameQuit", () => ShowUtilityFeedbackMessage("Close the MapSimulator window to end the current session."));
                systemWindow.BindEntryAction("BtJoyPad", () => ShowUtilityOptionWindow(OptionMenuMode.Joypad));
                systemWindow.BindEntryAction("BtOption", () => ShowUtilityOptionWindow(OptionMenuMode.Extra));
                systemWindow.BindEntryCursorHint("BtChannel", GetUtilityChannelPopupCursorHint);
            }
        }



        private void ToggleStatusBarPopupWindow(string windowName, string siblingWindowName)

        {

            if (uiWindowManager == null)

            {

                return;

            }



            uiWindowManager.HideWindow(siblingWindowName);

            UIWindowBase window = uiWindowManager.GetWindow(windowName);

            if (window == null)

            {

                return;

            }



            if (window.IsVisible)

            {

                uiWindowManager.HideWindow(windowName);

            }

            else

            {

                uiWindowManager.ShowWindow(windowName);

            }

        }



        private void ShowUtilityFeedbackMessage(string message)

        {

            if (!string.IsNullOrWhiteSpace(message))

            {

                _chat?.AddMessage(message, new Color(255, 228, 151), Environment.TickCount);

            }

        }



        private void HandleUtilityChannelPopupRequested()

        {

            StatusBarPopupCursorHint cursorHint = GetUtilityChannelPopupCursorHint();

            if (cursorHint == StatusBarPopupCursorHint.Busy)

            {

                ShowUtilityFeedbackMessage(_selectorRequestStatusMessage ?? "A world/channel request is already pending.");

                return;

            }



            if (cursorHint == StatusBarPopupCursorHint.Forbidden)

            {

                ShowUtilityFeedbackMessage(GetWorldSelectorStatusMessage());

                return;

            }



            ShowWorldSelectWindow();

        }



        private StatusBarPopupCursorHint GetUtilityChannelPopupCursorHint()

        {

            if (_selectorRequestKind != SelectorRequestKind.None)

            {

                return StatusBarPopupCursorHint.Busy;

            }



            if (IsLoginRuntimeSceneActive && _loginRuntime.CurrentStep != LoginStep.WorldSelect)

            {

                return StatusBarPopupCursorHint.Forbidden;

            }



            return StatusBarPopupCursorHint.Normal;

        }



        private string PublishMapleTvDraft()

        {

            string message = _mapleTvRuntime.OnSetMessage(currTickCount);

            if (message.StartsWith("MapleTV message set", StringComparison.Ordinal))

            {
                string megassengerChatMirror = _mapleTvRuntime.BuildMegassengerChatMirrorMessage();
                if (!string.IsNullOrWhiteSpace(megassengerChatMirror))
                {
                    _chat?.AddMessage(megassengerChatMirror, new Color(151, 221, 255), currTickCount);
                }

                ShowMapleTvWindow();

            }



            return message;

        }



        private string ClearMapleTvMessage()

        {

            return _mapleTvRuntime.OnClearMessage(preserveQueue: false);

        }

        private bool TryApplyMapleTvPacket(int packetType, byte[] payload, out string message)
        {
            bool applied = _mapleTvRuntime.TryApplyPacket(
                packetType,
                payload,
                currTickCount,
                ResolveMapleTvBuildFromAvatarLook,
                out message);

            if (applied && packetType == MapleTvRuntime.PacketTypeSetMessage)
            {
                ShowMapleTvWindow();
            }

            return applied;
        }



        private bool TryApplyFieldMessageBoxPacket(int packetType, byte[] payload, out string message)



        {



            _fieldMessageBoxRuntime.Initialize(GraphicsDevice);



            return _fieldMessageBoxRuntime.TryApplyPacket(packetType, payload, currTickCount, out message);



        }



        private bool TryApplyMapleTvSetMessagePacket(byte[] payload, out string message)

        {

            return TryApplyMapleTvPacket(MapleTvRuntime.PacketTypeSetMessage, payload, out message);

        }

        private void FlushPendingMapleTvSendResultFeedback(int tickCount)
        {
            MapleTvSendResultFeedback feedback = _mapleTvRuntime.ConsumePendingSendResultFeedback();
            if (feedback == null)
            {
                return;
            }

            _chat?.AddClientChatMessage(feedback.ChatMessage, tickCount, feedback.ChatLogType);
        }



        private CharacterBuild ResolveMapleTvBuildFromAvatarLook(LoginAvatarLook avatarLook)

        {

            if (avatarLook == null)

            {

                return null;

            }



            CharacterBuild template = _playerManager?.Player?.Build;

            if (_playerManager?.Loader == null)

            {

                return template?.Clone();

            }



            return _playerManager.Loader.LoadFromAvatarLook(avatarLook, template);

        }



        private string ToggleMapleTvReceiverMode()

        {

            return _mapleTvRuntime.ToggleReceiverMode();

        }



        private void WireWorldChannelSelectorWindows()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldSelect) is WorldSelectWindow worldSelectWindow)

            {

                EnsureWorldChannelSelectorState(worldSelectWindow.WorldIds);

                worldSelectWindow.WorldSelected -= HandleWorldSelected;

                worldSelectWindow.WorldSelected += HandleWorldSelected;

            }



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ChannelSelect) is ChannelSelectWindow channelSelectWindow)

            {

                channelSelectWindow.ChangeRequested -= HandleChannelChangeRequested;

                channelSelectWindow.ChangeRequested += HandleChannelChangeRequested;

            }



            RefreshWorldChannelSelectorWindows();

        }



        private void WireLoginCharacterSelectWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterSelect) is not CharacterSelectWindow characterSelectWindow)

            {

                return;

            }



            characterSelectWindow.CharacterSelected -= HandleLoginCharacterSelected;

            characterSelectWindow.CharacterSelected += HandleLoginCharacterSelected;

            characterSelectWindow.EnterRequested -= HandleLoginCharacterEnterRequested;

            characterSelectWindow.EnterRequested += HandleLoginCharacterEnterRequested;

            characterSelectWindow.NewCharacterRequested -= HandleLoginNewCharacterRequested;

            characterSelectWindow.NewCharacterRequested += HandleLoginNewCharacterRequested;

            characterSelectWindow.DeleteRequested -= HandleLoginCharacterDeleteRequested;

            characterSelectWindow.DeleteRequested += HandleLoginCharacterDeleteRequested;

        }

        private void WireLoginCreateCharacterWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginCreateCharacter) is not LoginCreateCharacterWindow createCharacterWindow)

            {

                return;

            }



            createCharacterWindow.RaceSelected -= HandleLoginCreateCharacterRaceSelected;

            createCharacterWindow.RaceSelected += HandleLoginCreateCharacterRaceSelected;

            createCharacterWindow.JobSelected -= HandleLoginCreateCharacterJobSelected;

            createCharacterWindow.JobSelected += HandleLoginCreateCharacterJobSelected;

            createCharacterWindow.AvatarShiftRequested -= HandleLoginCreateCharacterAvatarShiftRequested;

            createCharacterWindow.AvatarShiftRequested += HandleLoginCreateCharacterAvatarShiftRequested;

            createCharacterWindow.GenderToggleRequested -= HandleLoginCreateCharacterGenderToggleRequested;

            createCharacterWindow.GenderToggleRequested += HandleLoginCreateCharacterGenderToggleRequested;

            createCharacterWindow.DiceRequested -= HandleLoginCreateCharacterDiceRequested;

            createCharacterWindow.DiceRequested += HandleLoginCreateCharacterDiceRequested;

            createCharacterWindow.NameEditRequested -= HandleLoginCreateCharacterNameEditRequested;

            createCharacterWindow.NameEditRequested += HandleLoginCreateCharacterNameEditRequested;

            createCharacterWindow.NameChanged -= HandleLoginCreateCharacterNameChanged;

            createCharacterWindow.NameChanged += HandleLoginCreateCharacterNameChanged;

            createCharacterWindow.ConfirmRequested -= HandleLoginCreateCharacterConfirmRequested;

            createCharacterWindow.ConfirmRequested += HandleLoginCreateCharacterConfirmRequested;

            createCharacterWindow.CancelRequested -= HandleLoginCreateCharacterCancelRequested;

            createCharacterWindow.CancelRequested += HandleLoginCreateCharacterCancelRequested;

            createCharacterWindow.DuplicateCheckRequested -= HandleLoginCreateCharacterDuplicateCheckRequested;

            createCharacterWindow.DuplicateCheckRequested += HandleLoginCreateCharacterDuplicateCheckRequested;

        }



        private void WireLoginTitleWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginTitle) is not LoginTitleWindow titleWindow)

            {

                return;

            }



            titleWindow.SubmitRequested -= HandleLoginTitleSubmitted;

            titleWindow.SubmitRequested += HandleLoginTitleSubmitted;

            titleWindow.GuestLoginRequested -= HandleLoginTitleGuestLoginRequested;

            titleWindow.GuestLoginRequested += HandleLoginTitleGuestLoginRequested;

            titleWindow.NewAccountRequested -= HandleLoginTitleNewAccountRequested;

            titleWindow.NewAccountRequested += HandleLoginTitleNewAccountRequested;



            titleWindow.HomePageRequested -= HandleLoginTitleHomePageRequested;



            titleWindow.HomePageRequested += HandleLoginTitleHomePageRequested;

            titleWindow.QuitRequested -= HandleLoginTitleQuitRequested;

            titleWindow.QuitRequested += HandleLoginTitleQuitRequested;

            titleWindow.RecoverIdRequested -= HandleLoginTitleRecoverIdRequested;

            titleWindow.RecoverIdRequested += HandleLoginTitleRecoverIdRequested;

            titleWindow.RecoverPasswordRequested -= HandleLoginTitleRecoverPasswordRequested;

            titleWindow.RecoverPasswordRequested += HandleLoginTitleRecoverPasswordRequested;

        }



        private void WireAvatarPreviewCarouselWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AvatarPreviewCarousel) is not AvatarPreviewCarouselWindow previewWindow)

            {

                return;

            }



            previewWindow.CharacterSelected -= HandleLoginCharacterSelected;

            previewWindow.CharacterSelected += HandleLoginCharacterSelected;

            previewWindow.PageRequested -= HandleLoginCharacterPageRequested;

            previewWindow.PageRequested += HandleLoginCharacterPageRequested;

            previewWindow.EnterRequested -= HandleLoginCharacterEnterRequested;

            previewWindow.EnterRequested += HandleLoginCharacterEnterRequested;

        }



        private void WireRecommendWorldWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RecommendWorld) is not RecommendWorldWindow recommendWorldWindow)

            {

                return;

            }



            recommendWorldWindow.PreviousRequested -= HandleRecommendWorldPreviousRequested;

            recommendWorldWindow.PreviousRequested += HandleRecommendWorldPreviousRequested;

            recommendWorldWindow.NextRequested -= HandleRecommendWorldNextRequested;

            recommendWorldWindow.NextRequested += HandleRecommendWorldNextRequested;

            recommendWorldWindow.SelectRequested -= HandleRecommendWorldSelected;

            recommendWorldWindow.SelectRequested += HandleRecommendWorldSelected;

            recommendWorldWindow.CloseRequested -= HandleRecommendWorldClosed;

            recommendWorldWindow.CloseRequested += HandleRecommendWorldClosed;

        }



        private void WireLoginEntryDialogWindows()

        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ConnectionNotice) is ConnectionNoticeWindow connectionNoticeWindow)

            {

                connectionNoticeWindow.CancelRequested -= HandleConnectionNoticeCancelRequested;

                connectionNoticeWindow.CancelRequested += HandleConnectionNoticeCancelRequested;

            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) is not LoginUtilityDialogWindow utilityDialogWindow)

            {

                return;

            }



            utilityDialogWindow.PrimaryRequested -= HandleLoginUtilityPrimaryRequested;

            utilityDialogWindow.PrimaryRequested += HandleLoginUtilityPrimaryRequested;

            utilityDialogWindow.SecondaryRequested -= HandleLoginUtilitySecondaryRequested;

            utilityDialogWindow.SecondaryRequested += HandleLoginUtilitySecondaryRequested;

        }



        private void SyncLoginCharacterDetailWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterDetail) is not CharacterDetailWindow characterDetailWindow)

            {

                return;

            }



            bool shouldShow = IsLoginRuntimeSceneActive &&

                              (_loginRuntime.CurrentStep == LoginStep.CharacterSelect ||

                               _loginRuntime.CurrentStep == LoginStep.ViewAllCharacters);

            if (!shouldShow)

            {

                characterDetailWindow.Hide();

                return;

            }



            characterDetailWindow.SetEntry(

                _loginCharacterRoster.SelectedEntry,

                _loginCharacterRoster.SelectedEntry == null

                    ? "Select a character to inspect details."

                    : "Client-backed detail view for the selected roster entry.");

            characterDetailWindow.SetFont(_fontDebugValues);



            characterDetailWindow.Show();

        }



        private void ShowWorldSelectWindow()

        {

            if (uiWindowManager == null)

            {

                return;

            }



            _selectorBrowseWorldId = GetPreferredSelectorBrowseWorldId();

            uiWindowManager.HideWindow(MapSimulatorWindowNames.ChannelSelect);

            uiWindowManager.HideWindow(MapSimulatorWindowNames.ChannelShift);

            RefreshWorldChannelSelectorWindows();



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.WorldSelect) is WorldSelectWindow worldSelectWindow)

            {

                worldSelectWindow.Show();

                uiWindowManager.BringToFront(worldSelectWindow);

                return;

            }



            uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldSelect);

        }



        private void SyncLoginTitleWindow()

        {

            WireLoginTitleWindow();



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginTitle) is not LoginTitleWindow titleWindow)

            {

                return;

            }



            bool shouldShow = IsLoginRuntimeSceneActive && _loginRuntime.CurrentStep == LoginStep.Title;

            if (!shouldShow)

            {

                titleWindow.Hide();

                return;

            }



            bool busy = _loginRuntime.PendingStep.HasValue ||

                        _selectorRequestKind != SelectorRequestKind.None;

            if (titleWindow.IsVisible)

            {

                _loginTitleAccountName = titleWindow.AccountName ?? _loginTitleAccountName;

                _loginTitlePassword = titleWindow.Password ?? _loginTitlePassword;

                _loginTitleRememberId = titleWindow.RememberId;

            }

            titleWindow.Configure(

                _loginTitleAccountName,

                _loginTitlePassword,

                _loginTitleRememberId,

                _loginTitleStatusMessage,

                _loginPacketInbox.LastStatus,

                busy);

            titleWindow.Show();

            uiWindowManager.BringToFront(titleWindow);

        }



        private void HandleWorldSelected(int worldId)

        {

            _selectorBrowseWorldId = Math.Max(0, worldId);



            if (!IsWorldChannelSelectorRequestAllowed())

            {

                RefreshWorldChannelSelectorWindows();

                return;

            }



            if (IsLoginRuntimeSceneActive)

            {

                uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelSelect);

                BeginWorldChannelSelectorRequest(

                    SelectorRequestKind.LoginWorldCheck,

                    _selectorBrowseWorldId,

                    _simulatorChannelIndex,

                    LoginWorldSelectionRequestDelayMs,

                    $"Checking world {_selectorBrowseWorldId} access...");

                return;

            }



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ChannelSelect) is not ChannelSelectWindow channelSelectWindow)

            {

                return;

            }



            channelSelectWindow.Configure(

                _selectorBrowseWorldId,

                _simulatorWorldId,

                _simulatorChannelIndex,

                GetChannelSelectionStates(_selectorBrowseWorldId),

                _loginAccountIsAdult,

                requestAllowed: IsWorldChannelSelectorRequestAllowed(),

                statusMessage: GetChannelSelectorStatusMessage());

            channelSelectWindow.Show();

            uiWindowManager.BringToFront(channelSelectWindow);

        }



        private void HandleChannelChangeRequested(int worldId, int channelIndex)

        {

            if (!IsWorldChannelSelectorRequestAllowed())

            {

                RefreshWorldChannelSelectorWindows();

                return;

            }



            _selectorBrowseWorldId = Math.Max(0, worldId);

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelSelect);



            if (!IsLoginRuntimeSceneActive &&

                uiWindowManager?.GetWindow(MapSimulatorWindowNames.ChannelShift) is ChannelShiftWindow channelShiftWindow)

            {

                channelShiftWindow.BeginShift(_selectorBrowseWorldId, Math.Max(0, channelIndex), ChannelChangeRequestDelayMs + 500);

                uiWindowManager.BringToFront(channelShiftWindow);

            }



            BeginWorldChannelSelectorRequest(

                SelectorRequestKind.ChannelChange,

                _selectorBrowseWorldId,

                Math.Max(0, channelIndex),

                ChannelChangeRequestDelayMs,

                IsLoginRuntimeSceneActive

                    ? $"Sending SelectWorldResult for world {_selectorBrowseWorldId}, channel {channelIndex + 1}..."

                    : $"Changing to world {_selectorBrowseWorldId}, channel {channelIndex + 1}...");

        }



        private void RefreshWorldChannelSelectorWindows()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldSelect) is WorldSelectWindow worldSelectWindow)

            {

                EnsureWorldChannelSelectorState(worldSelectWindow.WorldIds);

                EnsureLoginWorldSelectorMetadata(worldSelectWindow.WorldIds);

                worldSelectWindow.Configure(

                    BuildWorldSelectionStates(),

                    _simulatorWorldId,

                    _selectorBrowseWorldId,

                    _loginAccountIsAdult,

                    requestAllowed: IsWorldChannelSelectorRequestAllowed(),

                    statusMessage: GetWorldSelectorStatusMessage());

            }



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ChannelSelect) is ChannelSelectWindow channelSelectWindow)

            {

                channelSelectWindow.Configure(

                    _selectorBrowseWorldId,

                    _simulatorWorldId,

                    _simulatorChannelIndex,

                    GetChannelSelectionStates(_selectorBrowseWorldId),

                    _loginAccountIsAdult,

                    requestAllowed: IsWorldChannelSelectorRequestAllowed(),

                    statusMessage: GetChannelSelectorStatusMessage());

            }

        }



        private bool IsWorldChannelSelectorRequestAllowed()

        {

            return _selectorRequestKind == SelectorRequestKind.None &&

                   (!IsLoginRuntimeSceneActive || _loginRuntime.CurrentStep == LoginStep.WorldSelect);

        }



        private bool ShouldUseLoginWorldMetadata => IsLoginRuntimeSceneActive && _loginRuntime.HasWorldInformation;



        private IReadOnlyList<int> GetRegisteredWorldSelectorIds()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldSelect) is WorldSelectWindow worldSelectWindow &&

                worldSelectWindow.WorldIds.Count > 0)

            {

                return worldSelectWindow.WorldIds;

            }



            if (_simulatorChannelStatesByWorld.Count > 0)

            {

                return _simulatorChannelStatesByWorld.Keys.OrderBy(id => id).ToArray();

            }



            return new[] { DefaultSimulatorWorldId };

        }



        private int GetPreferredSelectorBrowseWorldId()

        {

            if (ShouldUseLoginWorldMetadata && _loginLatestConnectedWorldId.HasValue)

            {

                return _loginLatestConnectedWorldId.Value;

            }



            return _simulatorWorldId;

        }



        private void DispatchLoginRuntimePacket(

            LoginPacketType packetType,

            out string message,

            bool applySelectorSideEffects = true,

            string summaryOverride = null)

        {

            bool suppressGenericDialogPrompt = false;

            if (applySelectorSideEffects &&

                packetType == LoginPacketType.SelectWorldResult &&

                !ApplyWorldChannelSelectionResult(

                    _loginPacketSelectWorldTargetWorldId ?? _selectorBrowseWorldId,

                    _loginPacketSelectWorldTargetChannelIndex ?? _simulatorChannelIndex,

                    consumePacketOverride: false,

                    runtimeMessage: out message))

            {

                RefreshWorldChannelSelectorWindows();

                SyncRecommendWorldWindow();

                SyncLoginCharacterSelectWindow();

                SyncLoginEntryDialogs();

                return;

            }

            if (applySelectorSideEffects &&

                packetType == LoginPacketType.SelectCharacterResult &&

                TryApplyPacketOwnedSelectCharacterResult(out message, out bool handledRuntimeDispatch))

            {

                if (!handledRuntimeDispatch)

                {

                    SyncLoginTitleWindow();

                    RefreshWorldChannelSelectorWindows();

                    SyncRecommendWorldWindow();

                    if (string.IsNullOrWhiteSpace(_loginCharacterStatusMessage))

                    {

                        _loginCharacterStatusMessage = message;

                    }



                    SyncLoginEntryDialogs();

                    return;

                }
            }

            if (applySelectorSideEffects &&

                packetType == LoginPacketType.SelectCharacterByVacResult &&

                TryApplyPacketOwnedSelectCharacterByVacResult(out message, out bool handledVacRuntimeDispatch))

            {

                if (!handledVacRuntimeDispatch)

                {

                    SyncLoginTitleWindow();

                    RefreshWorldChannelSelectorWindows();

                    SyncRecommendWorldWindow();

                    if (string.IsNullOrWhiteSpace(_loginCharacterStatusMessage))

                    {

                        _loginCharacterStatusMessage = message;

                    }



                    SyncLoginEntryDialogs();

                    return;

                }
            }

            if (packetType == LoginPacketType.CheckPasswordResult &&
                TryHandlePacketOwnedCheckPasswordResult(out string checkPasswordSummary, out bool continueRuntimeDispatch))
            {
                message = checkPasswordSummary;
                if (!continueRuntimeDispatch)
                {
                    SyncLoginTitleWindow();
                    RefreshWorldChannelSelectorWindows();
                    SyncRecommendWorldWindow();
                    SyncLoginEntryDialogs();
                    return;
                }

                summaryOverride = checkPasswordSummary;
            }

            if (packetType == LoginPacketType.GuestIdLoginResult &&
                TryHandlePacketOwnedGuestIdLoginResult(out string guestSummary))
            {
                summaryOverride = guestSummary;
            }

            if (packetType == LoginPacketType.AccountInfoResult &&
                TryHandlePacketOwnedAccountInfoResult(out string accountInfoSummary, out bool continueAccountInfoRuntimeDispatch))
            {
                message = accountInfoSummary;
                suppressGenericDialogPrompt = true;
                if (!continueAccountInfoRuntimeDispatch)
                {
                    SyncLoginTitleWindow();
                    RefreshWorldChannelSelectorWindows();
                    SyncRecommendWorldWindow();
                    SyncLoginEntryDialogs();
                    return;
                }

                summaryOverride = accountInfoSummary;
            }



            _loginRuntime.TryDispatchPacket(packetType, currTickCount, out message);

            if (!string.IsNullOrWhiteSpace(summaryOverride))

            {

                _loginRuntime.OverrideLastEventSummary(summaryOverride);

                message = _loginRuntime.LastEventSummary;

            }



            ApplyPacketOwnedLoginRosterState(packetType);



            ApplyLoginWorldSelectorPacket(packetType, Array.Empty<string>());

            if (applySelectorSideEffects && packetType != LoginPacketType.SelectWorldResult)

            {

                ApplyLoginWorldSelectorRuntimePacket(packetType);

            }



            if ((packetType == LoginPacketType.SelectWorldResult ||

                 packetType == LoginPacketType.ViewAllCharResult) &&

                IsLoginRuntimeSceneActive)

            {

                InitializeLoginCharacterRoster();

            }



            if (!suppressGenericDialogPrompt)
            {
                ApplyLoginPacketDialogPrompt(packetType);
            }
            TryContinueLoginBootstrapFromPacketProfile(packetType);



            SyncLoginTitleWindow();

            RefreshWorldChannelSelectorWindows();

            SyncRecommendWorldWindow();

            if (string.IsNullOrWhiteSpace(_loginCharacterStatusMessage))

            {

                _loginCharacterStatusMessage = message;

            }





            SyncLoginEntryDialogs();

        }



        private void SyncLoginWorldSelectionWindows()

        {

            bool shouldShowWorldSelect = IsLoginRuntimeSceneActive &&

                                         _loginRuntime.CurrentStep == LoginStep.WorldSelect;



            if (_lastLoginStep != _loginRuntime.CurrentStep)

            {

                if (_loginRuntime.CurrentStep == LoginStep.WorldSelect)

                {

                    _recommendWorldDismissed = false;

                    _selectorBrowseWorldId = GetPreferredSelectorBrowseWorldId();

                }



                _lastLoginStep = _loginRuntime.CurrentStep;

            }



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldSelect) is WorldSelectWindow worldSelectWindow)

            {

                if (shouldShowWorldSelect)

                {

                    if (!worldSelectWindow.IsVisible)

                    {

                        worldSelectWindow.Show();

                        uiWindowManager.BringToFront(worldSelectWindow);

                    }

                }

                else

                {

                    worldSelectWindow.Hide();

                }

            }



            if (!shouldShowWorldSelect)

            {

                uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelSelect);

            }



            SyncRecommendWorldWindow();

        }



        private void SyncRecommendWorldWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.RecommendWorld) is not RecommendWorldWindow recommendWorldWindow)

            {

                return;

            }



            bool shouldShow = IsLoginRuntimeSceneActive &&

                              _loginRuntime.CurrentStep == LoginStep.WorldSelect &&

                              !_recommendWorldDismissed;

            if (!shouldShow)

            {

                recommendWorldWindow.Hide();

                return;

            }



            RebuildRecommendWorldEntries();

            if (_recommendWorldEntries.Count == 0)

            {

                recommendWorldWindow.Hide();

                return;

            }



            _recommendWorldIndex = Math.Clamp(_recommendWorldIndex, 0, _recommendWorldEntries.Count - 1);

            recommendWorldWindow.Configure(

                _recommendWorldEntries,

                _recommendWorldIndex,

                IsWorldChannelSelectorRequestAllowed());



            if (!recommendWorldWindow.IsVisible)

            {

                recommendWorldWindow.Show();

                uiWindowManager.BringToFront(recommendWorldWindow);

            }

        }



        private void RebuildRecommendWorldEntries()

        {

            _recommendWorldEntries.Clear();



            Dictionary<int, WorldSelectionState> worldStates = BuildWorldSelectionStates();

            List<WorldSelectionState> orderedStates = _loginWorldMetadataByWorld.Values

                .Where(metadata => metadata.RecommendOrder.HasValue)

                .OrderBy(metadata => metadata.RecommendOrder.Value)

                .Select(metadata => worldStates.TryGetValue(metadata.WorldId, out WorldSelectionState state) ? state : null)

                .Where(state => state != null)

                .ToList();



            if (orderedStates.Count == 0)

            {

                orderedStates = worldStates.Values

                    .OrderByDescending(state => state.IsRecommended)

                    .ThenByDescending(state => state.IsLatestConnected)

                    .ThenBy(state => state.OccupancyPercent)

                    .ThenBy(state => state.WorldId)

                    .Take(5)

                    .ToList();

            }



            foreach (WorldSelectionState state in orderedStates)

            {

                string message = _loginWorldMetadataByWorld.TryGetValue(state.WorldId, out LoginWorldSelectorMetadata metadata) &&

                                 !string.IsNullOrWhiteSpace(metadata.RecommendMessage)

                    ? metadata.RecommendMessage

                    : BuildFallbackRecommendWorldMessage(state);



                _recommendWorldEntries.Add(new RecommendWorldEntry(

                    state.WorldId,

                    message));

            }



            int matchingIndex = _recommendWorldEntries.FindIndex(entry => entry.WorldId == _selectorBrowseWorldId);

            if (matchingIndex >= 0)

            {

                _recommendWorldIndex = matchingIndex;

            }

        }



        private string BuildFallbackRecommendWorldMessage(WorldSelectionState state)

        {

            int visibleChannels = GetChannelSelectionStates(state.WorldId).Count(channel => channel.Capacity > 0);

            string recommendationLine = state.IsLatestConnected

                ? "Previously connected world."

                : state.IsRecommended

                    ? "Recommended for lighter traffic."

                    : "Available login world.";

            string adultLine = state.HasAdultChannels

                ? (_loginAccountIsAdult ? "Adult-only channels available." : "Adult-only channels are gated.")

                : "Standard channel access only.";

            return recommendationLine + "\r\n" +

                   $"Load {state.OccupancyPercent}% ({state.Availability}).\r\n" +

                   $"{visibleChannels} visible channels, {state.ActiveChannels} enterable.\r\n" +

                   adultLine;

        }



        private string GetWorldSelectorStatusMessage()

        {

            if (_selectorRequestKind == SelectorRequestKind.LoginWorldCheck)

            {

                return _selectorRequestStatusMessage;

            }



            if (_selectorRequestKind == SelectorRequestKind.ChannelChange)

            {

                return _selectorRequestStatusMessage;

            }



            if (!string.IsNullOrWhiteSpace(_selectorLastResultMessage))

            {

                return _selectorLastResultMessage;

            }



            if (IsLoginRuntimeSceneActive)

            {

                if (_loginRuntime.CurrentStep != LoginStep.WorldSelect)

                {

                    return $"Login step {_loginRuntime.CurrentStep} blocks world requests.";

                }



                if (!_loginRuntime.HasWorldInformation)

                {

                    return "Using simulator-side world data until WorldInformation is dispatched.";

                }



                string latestLabel = _loginLatestConnectedWorldId.HasValue

                    ? $" Latest: {_loginLatestConnectedWorldId.Value}."

                    : string.Empty;

                string recommendedLabel = _loginRecommendedWorldIds.Count > 0

                    ? $" Recommended: {string.Join(", ", _loginRecommendedWorldIds.OrderBy(id => id).Take(3))}."

                    : string.Empty;

                string adultLabel = _loginAccountIsAdult

                    ? " Adult access enabled."

                    : " Adult access disabled.";

                return $"WorldInformation loaded.{latestLabel}{recommendedLabel}{adultLabel}";

            }



            return "Select a world to inspect its channels.";

        }



        private string GetChannelSelectorStatusMessage()

        {

            if (_selectorRequestKind != SelectorRequestKind.None)

            {

                return _selectorRequestStatusMessage;

            }



            if (IsLoginRuntimeSceneActive && _loginRuntime.CurrentStep != LoginStep.WorldSelect)

            {

                return $"Login step {_loginRuntime.CurrentStep} blocks channel entry.";

            }



            if (!string.IsNullOrWhiteSpace(_selectorLastResultMessage))

            {

                return _selectorLastResultMessage;

            }



            if (IsLoginRuntimeSceneActive && !_loginAccountIsAdult)

            {

                return "Adult-only channels remain gated for this simulator login account.";

            }



            return null;

        }



        private void BeginWorldChannelSelectorRequest(

            SelectorRequestKind requestKind,

            int worldId,

            int channelIndex,

            int durationMs,

            string statusMessage)

        {

            _selectorRequestKind = requestKind;

            _selectorRequestWorldId = Math.Max(0, worldId);

            _selectorRequestChannelIndex = Math.Max(0, channelIndex);

            _selectorRequestStartedAt = currTickCount;

            _selectorRequestDurationMs = Math.Max(1, durationMs);

            _selectorRequestCompletesAt = currTickCount + _selectorRequestDurationMs;

            _selectorRequestStatusMessage = statusMessage;

            _selectorLastResultCode = SelectorRequestResultCode.None;

            _selectorLastResultMessage = null;

            RefreshWorldChannelSelectorWindows();

            SyncLoginEntryDialogs();

        }

        private void CancelWorldChannelSelectorRequest(string resultMessage)

        {

            _selectorRequestKind = SelectorRequestKind.None;

            _selectorRequestStartedAt = int.MinValue;

            _selectorRequestDurationMs = 0;

            _selectorRequestCompletesAt = int.MinValue;

            _selectorRequestStatusMessage = null;

            _selectorLastResultCode = SelectorRequestResultCode.None;

            _selectorLastResultMessage = string.IsNullOrWhiteSpace(resultMessage)

                ? "Cancelled the pending world or channel request."

                : resultMessage;

        }



        private void UpdateWorldChannelSelectorRequestState()

        {

            if (_selectorRequestKind == SelectorRequestKind.None ||

                unchecked(currTickCount - _selectorRequestCompletesAt) < 0)

            {

                return;

            }



            SelectorRequestKind completedRequest = _selectorRequestKind;

            int worldId = _selectorRequestWorldId;

            int channelIndex = _selectorRequestChannelIndex;



            _selectorRequestKind = SelectorRequestKind.None;

            _selectorRequestStartedAt = int.MinValue;

            _selectorRequestDurationMs = 0;

            _selectorRequestCompletesAt = int.MinValue;

            _selectorRequestStatusMessage = null;



            if (completedRequest == SelectorRequestKind.LoginWorldCheck)

            {

                _selectorBrowseWorldId = worldId;

                (byte packetResultCode, byte populationLevel, SelectorRequestResultCode fallbackCode, string message) =

                    ResolveCheckUserLimitPacket(worldId, consumeOverride: true);

                if (!ApplyLoginCheckUserLimitPacket(worldId, packetResultCode, populationLevel, fallbackCode, message))

                {

                    DispatchLoginRuntimePacket(

                        LoginPacketType.CheckUserLimitResult,

                        out _,

                        applySelectorSideEffects: false,

                        summaryOverride: message);

                    return;

                }



                DispatchLoginRuntimePacket(

                    LoginPacketType.CheckUserLimitResult,

                    out _,

                    applySelectorSideEffects: false,

                    summaryOverride: message);

                return;

            }



            if (!ApplyWorldChannelSelectionResult(worldId, channelIndex, consumePacketOverride: true, runtimeMessage: out string runtimeMessage))

            {

                return;

            }



            if (IsLoginRuntimeSceneActive)

            {

                DispatchLoginRuntimePacket(

                    LoginPacketType.SelectWorldResult,

                    out _,

                    applySelectorSideEffects: false,

                    summaryOverride: runtimeMessage);

                _loginCharacterStatusMessage = $"Requested world {_simulatorWorldId}, channel {_simulatorChannelIndex + 1}. {runtimeMessage}";

                SyncLoginEntryDialogs();

                return;

            }



            _chat?.AddMessage(

                $"Changed to world {_simulatorWorldId}, channel {_simulatorChannelIndex + 1}.",

                new Color(255, 228, 151),

                Environment.TickCount);

        }



        private SelectorRequestResultCode EvaluateWorldSelectorRequestResult(int worldId)

        {

            if (!IsLoginRuntimeSceneActive || _loginRuntime.CurrentStep == LoginStep.WorldSelect)

            {

                IReadOnlyList<ChannelSelectionState> channels = GetChannelSelectionStates(worldId);

                bool hasVisibleChannel = channels.Any(channel => channel.Capacity > 0);

                bool hasAccessibleChannel = channels.Any(channel => channel.Capacity > 0 && channel.CanSelect(_loginAccountIsAdult));

                bool allVisibleChannelsRequireAdult = hasVisibleChannel &&

                                                      channels.Where(channel => channel.Capacity > 0)

                                                          .All(channel => channel.RequiresAdultAccount);



                if (!hasVisibleChannel)

                {

                    return SelectorRequestResultCode.WorldUnavailable;

                }



                if (!hasAccessibleChannel && allVisibleChannelsRequireAdult && !_loginAccountIsAdult)

                {

                    return SelectorRequestResultCode.AdultWorldRestricted;

                }



                return hasAccessibleChannel

                    ? SelectorRequestResultCode.Success

                    : SelectorRequestResultCode.WorldUnavailable;

            }



            return SelectorRequestResultCode.LoginStepBlocked;

        }



        private SelectorRequestResultCode EvaluateChannelSelectorRequestResult(int worldId, int channelIndex)

        {

            if (IsLoginRuntimeSceneActive && _loginRuntime.CurrentStep != LoginStep.WorldSelect)

            {

                return SelectorRequestResultCode.LoginStepBlocked;

            }



            ChannelSelectionState channel = GetChannelSelectionStates(worldId)

                .FirstOrDefault(state => state.ChannelIndex == channelIndex);

            if (channel == null || channel.Capacity <= 0)

            {

                return SelectorRequestResultCode.ChannelUnavailable;

            }



            if (channel.RequiresAdultAccount && !_loginAccountIsAdult)

            {

                return SelectorRequestResultCode.AdultChannelRestricted;

            }



            if (!channel.IsSelectable)

            {

                return channel.Availability == SelectorAvailability.Full

                    ? SelectorRequestResultCode.ChannelFull

                    : SelectorRequestResultCode.ChannelUnavailable;

            }



            return SelectorRequestResultCode.Success;

        }



        private void SetSelectorRequestResult(SelectorRequestResultCode resultCode, string message)

        {

            _selectorLastResultCode = resultCode;

            _selectorLastResultMessage = message;

        }



        private string BuildSelectorRequestResultMessage(SelectorRequestResultCode resultCode, int worldId, int channelIndex)

        {

            return resultCode switch

            {

                SelectorRequestResultCode.LoginStepBlocked => $"Login step {_loginRuntime.CurrentStep} blocks this selector request.",

                SelectorRequestResultCode.WorldUnavailable => $"CheckUserLimitResult denied world {worldId}: no enterable channels are currently open.",

                SelectorRequestResultCode.AdultWorldRestricted => $"CheckUserLimitResult denied world {worldId}: this world currently exposes adult-only channels.",

                SelectorRequestResultCode.ChannelUnavailable => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the server marked it unavailable.",

                SelectorRequestResultCode.ChannelFull => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the server reported it full.",

                SelectorRequestResultCode.AdultChannelRestricted => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: adult access is required.",

                SelectorRequestResultCode.ServerRejected => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the server returned a rejection code.",

                _ => null,

            };

        }



        private void ApplyLoginWorldSelectorRuntimePacket(LoginPacketType packetType)

        {

            if (!IsLoginRuntimeSceneActive)

            {

                return;

            }



            switch (packetType)

            {

                case LoginPacketType.CheckUserLimitResult:

                {

                    int worldId = _selectorRequestKind == SelectorRequestKind.LoginWorldCheck

                        ? _selectorRequestWorldId

                        : _selectorBrowseWorldId;

                    (byte packetResultCode, byte populationLevel, SelectorRequestResultCode fallbackCode, string message) =

                        ResolveCheckUserLimitPacket(worldId, consumeOverride: false);

                    ApplyLoginCheckUserLimitPacket(worldId, packetResultCode, populationLevel, fallbackCode, message);

                    break;

                }

                case LoginPacketType.SelectWorldResult:

                {

                    int worldId = _loginPacketSelectWorldTargetWorldId ?? _selectorBrowseWorldId;

                    int channelIndex = _loginPacketSelectWorldTargetChannelIndex ?? _simulatorChannelIndex;

                    ApplyWorldChannelSelectionResult(worldId, channelIndex, consumePacketOverride: false, runtimeMessage: out _);

                    break;

                }

            }

        }



        private (byte resultCode, byte populationLevel, SelectorRequestResultCode fallbackCode, string message) ResolveCheckUserLimitPacket(int worldId, bool consumeOverride)

        {

            IReadOnlyList<ChannelSelectionState> channels = GetChannelSelectionStates(worldId);

            SelectorRequestResultCode fallbackCode = EvaluateWorldSelectorRequestResult(worldId);

            byte populationLevel = _loginPacketCheckUserLimitPopulationLevel ?? DerivePopulationLevel(channels);

            byte? overrideCode = _loginPacketCheckUserLimitResultCode;

            if (consumeOverride)

            {

                _loginPacketCheckUserLimitResultCode = null;

                _loginPacketCheckUserLimitPopulationLevel = null;

            }



            if (overrideCode.HasValue)

            {

                return overrideCode.Value switch

                {

                    0 => (0, populationLevel, SelectorRequestResultCode.Success, null),

                    1 => (1, populationLevel, SelectorRequestResultCode.Success, $"CheckUserLimitResult warned that world {worldId} is busy, but channel browsing remains available."),

                    2 => (2, populationLevel, SelectorRequestResultCode.WorldUnavailable, $"CheckUserLimitResult denied world {worldId}: the client over-user-limit branch kept world selection active and blocked channel entry."),

                    _ => (overrideCode.Value, populationLevel, SelectorRequestResultCode.WorldUnavailable, $"CheckUserLimitResult denied world {worldId} with server code {FormatSelectorPacketCode(overrideCode.Value)}."),

                };

            }



            return fallbackCode switch

            {

                SelectorRequestResultCode.Success when BuildWorldSelectionStates().TryGetValue(worldId, out WorldSelectionState state) &&

                                                      state.Availability != SelectorAvailability.Available

                    => (1, populationLevel, SelectorRequestResultCode.Success, $"CheckUserLimitResult warned that world {worldId} is near capacity, but channel browsing remains available."),

                SelectorRequestResultCode.Success => (0, populationLevel, SelectorRequestResultCode.Success, null),

                _ => (2, populationLevel, fallbackCode, BuildSelectorRequestResultMessage(fallbackCode, worldId, _simulatorChannelIndex)),

            };

        }



        private bool ApplyLoginCheckUserLimitPacket(

            int worldId,

            byte packetResultCode,

            byte populationLevel,

            SelectorRequestResultCode fallbackCode,

            string message)

        {

            ApplyPopulationLevelToLoginWorldMetadata(worldId, populationLevel);

            _selectorBrowseWorldId = Math.Max(0, worldId);



            if (packetResultCode != 0 && packetResultCode != 1)

            {

                uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelSelect);

                SetSelectorRequestResult(fallbackCode, message);

                if (IsLoginRuntimeSceneActive)

                {

                    _loginCharacterStatusMessage = string.IsNullOrWhiteSpace(message)

                        ? $"World {worldId} was denied by CheckUserLimitResult."

                        : message;

                }



                RefreshWorldChannelSelectorWindows();

                SyncRecommendWorldWindow();

                SyncLoginEntryDialogs();

                return false;

            }



            SetSelectorRequestResult(SelectorRequestResultCode.Success, message);

            if (IsLoginRuntimeSceneActive && !string.IsNullOrWhiteSpace(message))

            {

                _loginCharacterStatusMessage = message;

            }



            RefreshWorldChannelSelectorWindows();



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ChannelSelect) is ChannelSelectWindow channelSelectWindow)

            {

                channelSelectWindow.Show();

                uiWindowManager.BringToFront(channelSelectWindow);

            }



            SyncRecommendWorldWindow();

            SyncLoginEntryDialogs();

            return true;

        }



        private bool ApplyWorldChannelSelectionResult(int worldId, int channelIndex, bool consumePacketOverride, out string runtimeMessage)

        {

            runtimeMessage = null;

            byte? packetResultCode = _loginPacketSelectWorldResultCode;

            if (consumePacketOverride)

            {

                _loginPacketSelectWorldResultCode = null;

            }



            if (packetResultCode.HasValue)

            {

                if (!IsSelectWorldSuccessCode(packetResultCode.Value))

                {

                    string rejectionMessage = BuildSelectWorldPacketResultMessage(packetResultCode.Value, worldId, channelIndex, out bool returnsToTitle);

                    SetSelectorRequestResult(

                        SelectorRequestResultCode.ServerRejected,

                        rejectionMessage);

                    if (IsLoginRuntimeSceneActive)

                    {

                        _loginCharacterStatusMessage = rejectionMessage;

                        ShowSelectWorldFailureDialog(packetResultCode.Value, rejectionMessage);

                        if (returnsToTitle)

                        {

                            _loginRuntime.ForceStep(

                                LoginStep.Title,

                                $"SelectWorldResult code {FormatSelectorPacketCode(packetResultCode.Value)} returned to the title step.");

                            uiWindowManager?.HideWindow(MapSimulatorWindowNames.WorldSelect);

                            uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelSelect);

                            uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelShift);

                        }

                    }



                    RefreshWorldChannelSelectorWindows();

                    SyncRecommendWorldWindow();

                    SyncLoginEntryDialogs();

                    return false;

                }



                runtimeMessage = packetResultCode.Value == 0

                    ? "Received SelectWorldResult and scheduled character-select transition."

                    : $"Received SelectWorldResult server code {packetResultCode.Value} and scheduled character-select transition.";

            }

            else

            {

                SelectorRequestResultCode channelResult = EvaluateChannelSelectorRequestResult(worldId, channelIndex);

                if (channelResult != SelectorRequestResultCode.Success)

                {

                    SetSelectorRequestResult(channelResult, BuildSelectorRequestResultMessage(channelResult, worldId, channelIndex));

                    RefreshWorldChannelSelectorWindows();

                    SyncRecommendWorldWindow();

                    SyncLoginEntryDialogs();

                    return false;

                }



                runtimeMessage = "Received SelectWorldResult and scheduled character-select transition.";

            }



            _simulatorWorldId = worldId;

            _simulatorChannelIndex = channelIndex;

            _selectorBrowseWorldId = worldId;

            SetSelectorRequestResult(SelectorRequestResultCode.Success, packetResultCode is > 0

                ? $"SelectWorldResult accepted world {worldId}, channel {channelIndex + 1} with server code {packetResultCode.Value}."

                : null);

            RefreshWorldChannelSelectorWindows();



            uiWindowManager?.HideWindow(MapSimulatorWindowNames.WorldSelect);

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelSelect);

            return true;

        }



        private static bool IsSelectWorldSuccessCode(byte resultCode)

        {

            return resultCode == 0 || resultCode == 12 || resultCode == 23;

        }



        private static string BuildSelectWorldPacketResultMessage(byte resultCode, int worldId, int channelIndex, out bool returnsToTitle)

        {

            returnsToTitle = false;

            string codeLabel = FormatSelectorPacketCode(resultCode);

            return unchecked((sbyte)resultCode) switch

            {

                -1 or 6 or 8 or 9 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code {codeLabel} to login error 15.",

                2 or 3 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code {codeLabel} to login error 16.",

                4 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code 4 to login error 3.",

                5 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code 5 to login error 20.",

                7 => BuildSelectWorldTitleResetMessage(worldId, channelIndex, codeLabel, out returnsToTitle),

                10 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code 10 to login error 19.",

                11 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code 11 to login error 14.",

                13 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code 13 to login error 21.",

                14 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client opened the Yes/No dialog 27 and security website flow for server code 14.",

                15 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client opened the Yes/No dialog 26 and security website flow for server code 15.",

                16 or 21 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code {codeLabel} to login error 33.",

                17 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code 17 to login error 27.",

                25 => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the client mapped server code 25 to login error 40.",

                _ => $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: the server returned unmapped code {codeLabel}.",

            };

        }



        private static string BuildSelectWorldTitleResetMessage(int worldId, int channelIndex, string codeLabel, out bool returnsToTitle)

        {

            returnsToTitle = true;

            return $"SelectWorldResult denied world {worldId}, channel {channelIndex + 1}: server code {codeLabel} returned the client to the title step and raised login error 17.";

        }



        private static string FormatSelectorPacketCode(byte resultCode)

        {

            sbyte signedCode = unchecked((sbyte)resultCode);

            return signedCode < 0

                ? $"{signedCode} (0x{resultCode:X2})"

                : resultCode.ToString();

        }



        private static byte DerivePopulationLevel(IReadOnlyList<ChannelSelectionState> channels)

        {

            int occupancyPercent = channels == null || channels.Count == 0

                ? 0

                : (int)Math.Round(channels.Where(channel => channel.Capacity > 0).DefaultIfEmpty().Average(channel => channel?.OccupancyPercent ?? 0));

            return occupancyPercent switch

            {

                >= 95 => 3,

                >= 70 => 2,

                >= 40 => 1,

                _ => 0,

            };

        }



        private void ApplyPopulationLevelToLoginWorldMetadata(int worldId, byte populationLevel)

        {

            if (!ShouldUseLoginWorldMetadata)

            {

                return;

            }



            EnsureLoginWorldSelectorMetadata(new[] { worldId });

            if (!_loginWorldMetadataByWorld.TryGetValue(worldId, out LoginWorldSelectorMetadata metadata))

            {

                return;

            }



            int targetOccupancyPercent = populationLevel switch

            {

                >= 3 => 96,

                2 => 78,

                1 => 56,

                _ => 32,

            };



            List<ChannelSelectionState> updatedChannels = new(metadata.Channels.Count);

            foreach (ChannelSelectionState channel in metadata.Channels)

            {

                if (channel.Capacity <= 0)

                {

                    updatedChannels.Add(channel);

                    continue;

                }



                int bias = ((worldId * 9) + (channel.ChannelIndex * 7)) % 11 - 5;

                int occupancyPercent = Math.Clamp(targetOccupancyPercent + (bias * 3), 0, 100);

                int userCount = Math.Clamp((int)Math.Round(channel.Capacity * (occupancyPercent / 100d)), 0, channel.Capacity);

                bool isCurrentSelection = worldId == _simulatorWorldId && channel.ChannelIndex == _simulatorChannelIndex;

                bool isSelectable = userCount < channel.Capacity || isCurrentSelection;



                updatedChannels.Add(new ChannelSelectionState(

                    channel.ChannelIndex,

                    userCount,

                    channel.Capacity,

                    isSelectable,

                    channel.RequiresAdultAccount));

            }



            _loginWorldMetadataByWorld[worldId] = new LoginWorldSelectorMetadata(

                worldId,

                updatedChannels,

                metadata.RequiresAdultAccount,

                metadata.HasAuthoritativePopulationData,

                metadata.RecommendMessage,

                metadata.RecommendOrder);

        }



        private void InitializeLoginCharacterRoster()

        {

            if (TryInitializeLoginCharacterRosterFromPacket())

            {

                return;

            }



            int targetMapId = ResolveLoginCharacterTargetMapId();

            string targetMapDisplayName = ResolveMapTransferDisplayName(targetMapId);

            List<LoginCharacterRosterEntry> entries = new();



            CharacterBuild currentBuild = _playerManager?.Player?.Build?.Clone();

            if (currentBuild != null)

            {

                currentBuild.Name = string.IsNullOrWhiteSpace(currentBuild.Name) ? "ExplorerGM" : currentBuild.Name;

                ApplyLoginCharacterDetailDefaults(

                    currentBuild,

                    minimumLevel: 30,

                    fallbackGuildName: "Maple GM",

                    fallbackFame: 18,

                    fallbackExpPercent: 57,

                    fallbackWorldRank: 128,

                    fallbackJobRank: 17);

                AddLoginCharacterRosterEntry(

                    entries,

                    currentBuild,

                    targetMapId,

                    targetMapDisplayName,

                    previousWorldRank: currentBuild.WorldRank + 3,

                    previousJobRank: Math.Max(1, currentBuild.JobRank - 2));

            }



            CharacterBuild femaleBuild = _playerManager?.Loader?.LoadDefaultFemale();

            if (femaleBuild != null)

            {

                femaleBuild.Name = "Rondo";

                ApplyLoginCharacterDetailDefaults(

                    femaleBuild,

                    minimumLevel: 18,

                    fallbackGuildName: "Lith Harbor",

                    fallbackFame: 7,

                    fallbackExpPercent: 34,

                    fallbackWorldRank: 463,

                    fallbackJobRank: 92);

                AddLoginCharacterRosterEntry(

                    entries,

                    femaleBuild,

                    targetMapId,

                    targetMapDisplayName,

                    previousWorldRank: femaleBuild.WorldRank - 11,

                    previousJobRank: femaleBuild.JobRank);

            }



            CharacterBuild randomBuild = _playerManager?.Loader?.LoadRandom();

            if (randomBuild != null)

            {

                randomBuild.Name = "Rin";

                ApplyLoginCharacterDetailDefaults(

                    randomBuild,

                    minimumLevel: 24,

                    fallbackGuildName: "Sleepywood",

                    fallbackFame: 11,

                    fallbackExpPercent: 82,

                    fallbackWorldRank: 205,

                    fallbackJobRank: 31);

                AddLoginCharacterRosterEntry(

                    entries,

                    randomBuild,

                    targetMapId,

                    targetMapDisplayName,

                    previousWorldRank: randomBuild.WorldRank,

                    previousJobRank: randomBuild.JobRank + 4);

            }



            int slotCount = Math.Max(entries.Count, LoginCharacterRosterManager.EntriesPerPage);



            int buyCharacterCount = _loginCanHaveExtraCharacter ? 1 : 0;



            _loginCharacterRoster.SetEntries(entries, slotCount, buyCharacterCount);



            PersistLoginCharacterRosterToAccountStore(entries, slotCount, buyCharacterCount);

            _loginCharacterStatusMessage = entries.Count > 0

                ? $"Seeded an account-backed roster for {ResolveLoginRosterAccountName()}."

                : $"Unable to populate the account-backed roster for {ResolveLoginRosterAccountName()}.";

            FinalizeLoginCharacterRosterInitialization();







        }



        private void AddLoginCharacterRosterEntry(

            List<LoginCharacterRosterEntry> entries,

            CharacterBuild sourceBuild,

            int targetMapId,

            string targetMapDisplayName,

            bool canDelete = true,

            int? previousWorldRank = null,

            int? previousJobRank = null,

            byte[] avatarLookPacket = null,

            int portal = 0)

        {

            if (entries == null || sourceBuild == null)

            {

                return;

            }



            byte[] rosterAvatarLookPacket = avatarLookPacket != null ? (byte[])avatarLookPacket.Clone() : LoginAvatarLookCodec.Encode(sourceBuild);

            CharacterBuild rosterBuild = sourceBuild.Clone();

            if (_playerManager?.Loader != null &&

                LoginAvatarLookCodec.TryDecode(rosterAvatarLookPacket, out LoginAvatarLook avatarLook, out _))

            {

                rosterBuild = _playerManager.Loader.LoadFromAvatarLook(avatarLook, sourceBuild);

            }



            entries.Add(new LoginCharacterRosterEntry(

                rosterBuild,

                targetMapId,

                targetMapDisplayName,

                canDelete,

                previousWorldRank,

                previousJobRank,

                rosterAvatarLookPacket,

                portal));

        }



        private LoginCharacterRosterEntry CreateLoginCharacterRosterEntry(



            CharacterBuild sourceBuild,



            int targetMapId,



            string targetMapDisplayName,



            bool canDelete = true,



            int? previousWorldRank = null,



            int? previousJobRank = null,



            byte[] avatarLookPacket = null,



            int portal = 0)



        {



            if (sourceBuild == null)



            {



                return null;



            }







            byte[] rosterAvatarLookPacket = avatarLookPacket != null ? (byte[])avatarLookPacket.Clone() : LoginAvatarLookCodec.Encode(sourceBuild);



            CharacterBuild rosterBuild = sourceBuild.Clone();



            if (_playerManager?.Loader != null &&



                LoginAvatarLookCodec.TryDecode(rosterAvatarLookPacket, out LoginAvatarLook avatarLook, out _))



            {



                rosterBuild = _playerManager.Loader.LoadFromAvatarLook(avatarLook, sourceBuild);



            }







            return new LoginCharacterRosterEntry(



                rosterBuild,



                targetMapId,



                targetMapDisplayName,



                canDelete,



                previousWorldRank,



                previousJobRank,



                rosterAvatarLookPacket,



                portal);



        }







        private bool TryInitializeLoginCharacterRosterFromPacket()

        {

            if (!IsLoginRuntimeSceneActive ||

                !TryGetActiveLoginRosterPacketProfile(out LoginSelectWorldResultProfile rosterProfile, out string packetSource) ||

                rosterProfile.Entries.Count == 0)

            {

                return false;

            }



            int fallbackMapId = ResolveLoginCharacterTargetMapId();

            List<LoginCharacterRosterEntry> entries = new();

            foreach (LoginSelectWorldCharacterEntry packetEntry in rosterProfile.Entries)

            {

                CharacterBuild build = CreateLoginCharacterBuildFromPacket(packetEntry);

                int targetMapId = packetEntry.FieldMapId > 0 ? packetEntry.FieldMapId : fallbackMapId;

                string targetMapDisplayName = ResolveMapTransferDisplayName(targetMapId);

                AddLoginCharacterRosterEntry(

                    entries,

                    build,

                    targetMapId,

                    targetMapDisplayName,

                    previousWorldRank: packetEntry.PreviousWorldRank,

                    previousJobRank: packetEntry.PreviousJobRank,

                    avatarLookPacket: packetEntry.AvatarLookPacket,

                    portal: packetEntry.Portal);

            }



            _loginCharacterRoster.SetEntries(entries, rosterProfile.SlotCount, rosterProfile.BuyCharacterCount);



            PersistLoginCharacterRosterToAccountStore(entries, rosterProfile.SlotCount, rosterProfile.BuyCharacterCount);

            _loginCharacterStatusMessage = entries.Count > 0

                ? $"Loaded {entries.Count} packet-authored character entries from {packetSource}."

                : $"{packetSource} succeeded, but the packet did not carry any character entries.";

            FinalizeLoginCharacterRosterInitialization();



            return true;

        }



        private bool TryInitializeLoginCharacterRosterFromAccountStore()



        {



            LoginCharacterAccountStore.LoginCharacterAccountState storedState = _loginCharacterAccountStore.GetState(



                ResolveLoginRosterAccountName(),



                ResolveLoginRosterWorldId());



            if (storedState == null)



            {



                _loginAccountPicCode = string.Empty;

                _loginAccountBirthDate = string.Empty;

                _loginAccountSpwEnabled = false;

                _loginAccountSecondaryPassword = string.Empty;

                _loginAccountCashShopNxCredit = DefaultCashShopNxCredit;

                SyncCashShopAccountCredit();
                SyncStorageAccessContext();

                return false;



            }



            ApplyStoredLoginAccountSecurity(storedState);







            int fallbackMapId = ResolveLoginCharacterTargetMapId();



            List<LoginCharacterRosterEntry> entries = new();

            foreach (LoginCharacterAccountStore.LoginCharacterAccountEntryState storedEntry in storedState.Entries)



            {



                CharacterBuild build = CreateLoginCharacterBuildFromAccountState(storedEntry);



                int targetMapId = storedEntry.FieldMapId > 0 ? storedEntry.FieldMapId : fallbackMapId;



                string targetMapDisplayName = string.IsNullOrWhiteSpace(storedEntry.FieldDisplayName)



                    ? ResolveMapTransferDisplayName(targetMapId)



                    : storedEntry.FieldDisplayName;



                AddLoginCharacterRosterEntry(



                    entries,



                    build,



                    targetMapId,



                    targetMapDisplayName,



                    canDelete: storedEntry.CanDelete,



                    previousWorldRank: storedEntry.PreviousWorldRank,



                    previousJobRank: storedEntry.PreviousJobRank,



                    avatarLookPacket: storedEntry.AvatarLookPacket,



                    portal: storedEntry.Portal);



            }







            _loginCharacterRoster.SetEntries(entries, storedState.SlotCount, storedState.BuyCharacterCount);



            _loginCharacterStatusMessage = entries.Count > 0



                ? $"Loaded {entries.Count} account-backed character entries for {ResolveLoginRosterAccountName()}."



                : $"Loaded the account-backed roster for {ResolveLoginRosterAccountName()}.";



            FinalizeLoginCharacterRosterInitialization();



            return true;



        }







        private bool TryGetActiveLoginRosterPacketProfile(

            out LoginSelectWorldResultProfile rosterProfile,

            out string packetSource)

        {

            rosterProfile = null;

            packetSource = null;



            if (_loginRuntime.CurrentStep == LoginStep.ViewAllCharacters &&

                _loginRuntime.GetPacketCount(LoginPacketType.ViewAllCharResult) > 0 &&

                _loginPacketViewAllCharRosterProfile?.Entries?.Count > 0)

            {

                rosterProfile = _loginPacketViewAllCharRosterProfile;

                packetSource = "ViewAllCharResult";

                return true;

            }



            if (_loginRuntime.GetPacketCount(LoginPacketType.SelectWorldResult) > 0 &&

                _loginPacketSelectWorldResultProfile != null &&

                LoginSelectWorldResultCodec.IsSuccessCode(_loginPacketSelectWorldResultProfile.ResultCode) &&

                _loginPacketSelectWorldResultProfile.Entries.Count > 0)

            {

                rosterProfile = _loginPacketSelectWorldResultProfile;

                packetSource = "SelectWorldResult";

                return true;

            }



            if (_loginPacketViewAllCharRosterProfile?.Entries?.Count > 0)

            {

                rosterProfile = _loginPacketViewAllCharRosterProfile;

                packetSource = "ViewAllCharResult";

                return true;

            }



            return false;

        }



        private void ApplyPacketOwnedLoginRosterState(LoginPacketType packetType)

        {

            switch (packetType)

            {

                case LoginPacketType.ViewAllCharResult:

                    ApplyViewAllCharResultProfile();

                    break;



                case LoginPacketType.CreateNewCharacterResult:



                    ApplyCreateNewCharacterResultProfile();



                    break;



                case LoginPacketType.DeleteCharacterResult:



                    ApplyDeleteCharacterResultProfile();



                    break;



                case LoginPacketType.ExtraCharInfoResult:

                    _loginCanHaveExtraCharacter = _loginPacketExtraCharInfoResultProfile?.CanHaveExtraCharacter == true;

                    break;

                case LoginPacketType.CheckDuplicatedIdResult:

                    ApplyCheckDuplicatedIdResultProfile();

                    break;

            }

        }



        private bool TryApplyPacketOwnedSelectCharacterResult(out string message, out bool handledRuntimeDispatch)

        {

            message = null;

            handledRuntimeDispatch = false;



            LoginSelectCharacterResultProfile packetProfile = _loginPacketSelectCharacterResultProfile;

            if (!IsLoginRuntimeSceneActive || packetProfile == null)

            {

                return false;

            }



            if (!packetProfile.IsSuccess)

            {

                if (ShouldReturnSelectCharacterFailureToTitle(packetProfile))

                {

                    _loginRuntime.ForceStep(LoginStep.Title, "Packet-authored SelectCharacterResult returned the login flow to title.");

                }



                message = BuildSelectCharacterResultFailureMessage(packetProfile);

                _loginCharacterStatusMessage = message;

                ShowSelectCharacterFailureDialog(packetProfile, message);

                return true;

            }



            if (packetProfile.CharacterId.HasValue && packetProfile.CharacterId.Value > 0)

            {

                SelectLoginCharacterById(packetProfile.CharacterId.Value);

            }



            LoginCharacterRosterEntry entry = _loginCharacterRoster.SelectedEntry;

            if (entry?.Build == null)

            {

                message = packetProfile.CharacterId.HasValue && packetProfile.CharacterId.Value > 0

                    ? $"SelectCharacterResult succeeded for character {packetProfile.CharacterId.Value}, but that character is not present in the active login roster."

                    : "SelectCharacterResult succeeded, but no login roster entry is currently selected.";

                _loginCharacterStatusMessage = message;

                ShowLoginUtilityDialog(

                    "Login Utility",

                    message,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return true;

            }



            CharacterBuild selectedBuild = entry.CreateRuntimeBuild(_playerManager?.Loader);

            _playerManager?.CreatePlayerFromBuild(selectedBuild);

            RefreshSkillWindowForJob(selectedBuild.Job);



            if (_loadMapCallback == null || !QueueMapTransfer(entry.FieldMapId, null))

            {

                message = $"SelectCharacterResult accepted {entry.Build.Name}, but map loading is unavailable.";

                _loginCharacterStatusMessage = message;

                ShowLoginUtilityDialog(

                    "Login Utility",

                    message,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return true;

            }



            HideLoginUtilityDialog();

            message = BuildSelectCharacterResultSuccessMessage(packetProfile, entry);

            _loginCharacterStatusMessage = message;

            handledRuntimeDispatch = true;

            return true;

        }

        private bool TryApplyPacketOwnedSelectCharacterByVacResult(out string message, out bool handledRuntimeDispatch)

        {

            message = null;

            handledRuntimeDispatch = false;



            LoginSelectCharacterByVacResultProfile packetProfile = _loginPacketSelectCharacterByVacResultProfile;

            if (!IsLoginRuntimeSceneActive || packetProfile == null)

            {

                return false;

            }



            if (!packetProfile.IsConnectSuccess)

            {

                if (ShouldReturnSelectCharacterByVacFailureToTitle(packetProfile))

                {

                    _loginRuntime.ForceStep(LoginStep.Title, "Packet-authored SelectCharacterByVACResult returned the login flow to title.");

                }



                message = BuildSelectCharacterByVacResultFailureMessage(packetProfile);

                _loginCharacterStatusMessage = message;

                ShowSelectCharacterByVacFailureDialog(packetProfile, message);

                return true;

            }



            if (packetProfile.CharacterId.HasValue && packetProfile.CharacterId.Value > 0)

            {

                SelectLoginCharacterById(packetProfile.CharacterId.Value);

            }



            LoginCharacterRosterEntry entry = _loginCharacterRoster.SelectedEntry;

            if (entry?.Build == null)

            {

                message = packetProfile.CharacterId.HasValue && packetProfile.CharacterId.Value > 0

                    ? $"SelectCharacterByVACResult succeeded for character {packetProfile.CharacterId.Value}, but that character is not present in the active login roster."

                    : "SelectCharacterByVACResult succeeded, but no login roster entry is currently selected.";

                _loginCharacterStatusMessage = message;

                ShowLoginUtilityDialog(

                    "Login Utility",

                    message,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return true;

            }



            CharacterBuild selectedBuild = entry.CreateRuntimeBuild(_playerManager?.Loader);

            _playerManager?.CreatePlayerFromBuild(selectedBuild);

            RefreshSkillWindowForJob(selectedBuild.Job);



            if (_loadMapCallback == null || !QueueMapTransfer(entry.FieldMapId, null))

            {

                message = $"SelectCharacterByVACResult accepted {entry.Build.Name}, but map loading is unavailable.";

                _loginCharacterStatusMessage = message;

                ShowLoginUtilityDialog(

                    "Login Utility",

                    message,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return true;

            }



            HideLoginUtilityDialog();

            message = BuildSelectCharacterByVacResultSuccessMessage(packetProfile, entry);

            _loginCharacterStatusMessage = message;

            handledRuntimeDispatch = true;

            return true;

        }

        private bool TryHandlePacketOwnedCheckPasswordResult(out string summary, out bool continueRuntimeDispatch)
        {
            summary = null;
            continueRuntimeDispatch = true;

            LoginCheckPasswordResultProfile packetProfile = _loginPacketCheckPasswordResultProfile;
            if (!IsLoginRuntimeSceneActive || packetProfile == null)
            {
                return false;
            }

            if (!packetProfile.IsSuccess && !packetProfile.IsLicenseResult)
            {
                summary = BuildCheckPasswordResultFailureMessage(packetProfile);
                _loginTitleStatusMessage = summary;
                _loginCharacterStatusMessage = summary;
                ShowCheckPasswordResultFailureDialog(packetProfile, summary);
                continueRuntimeDispatch = false;
                return true;
            }

            if (packetProfile.RequiresAccountRegistration)
            {
                summary = BuildCheckPasswordRegistrationSummary(packetProfile);
                _loginTitleStatusMessage = summary;
                _loginCharacterStatusMessage = summary;
                ShowLoginUtilityDialog(
                    "Login Utility",
                    summary,
                    LoginUtilityDialogButtonLayout.YesNo,
                    LoginUtilityDialogAction.WebsiteHandoffDecision,
                    noticeTextIndex: 31);
                continueRuntimeDispatch = false;
                return true;
            }

            if (packetProfile.IsLicenseResult)
            {
                summary = BuildCheckPasswordLicenseSummary(packetProfile);
                _loginTitleStatusMessage = summary;
                _loginCharacterStatusMessage = summary;
                ShowLoginUtilityDialog(
                    "Login Utility",
                    summary,
                    LoginUtilityDialogButtonLayout.Ok,
                    LoginUtilityDialogAction.DismissOnly);
                continueRuntimeDispatch = false;
                return true;
            }

            ApplyPacketOwnedAccountInfo(packetProfile);
            _loginAccountMigrationAccepted = true;
            _loginAccountAcceptedEula = true;
            summary = BuildCheckPasswordSuccessSummary(packetProfile);
            _loginTitleStatusMessage = summary;
            _loginCharacterStatusMessage = summary;
            HideLoginUtilityDialog();
            return true;
        }

        private bool TryHandlePacketOwnedGuestIdLoginResult(out string summary)
        {
            summary = null;

            LoginGuestIdLoginResultProfile packetProfile = _loginPacketGuestIdLoginResultProfile;
            if (!IsLoginRuntimeSceneActive || packetProfile == null)
            {
                return false;
            }

            if (!LoginGuestIdLoginResultCodec.IsSuccessCode(packetProfile.ResultCode))
            {
                summary = BuildGuestIdLoginFailureMessage(packetProfile);
                _loginTitleStatusMessage = summary;
                _loginCharacterStatusMessage = summary;
                ShowGuestIdLoginFailureDialog(packetProfile, summary);
                return true;
            }

            if (packetProfile.RegistrationStatusId is 2 or 3)
            {
                summary = BuildGuestIdLoginRegistrationSummary(packetProfile);
                _loginTitleStatusMessage = summary;
                _loginCharacterStatusMessage = summary;
                ShowLoginUtilityDialog(
                    "Login Utility",
                    summary,
                    LoginUtilityDialogButtonLayout.YesNo,
                    LoginUtilityDialogAction.WebsiteHandoffDecision,
                    noticeTextIndex: 31);
                return true;
            }

            ApplyPacketOwnedAccountInfo(packetProfile);
            _loginAccountMigrationAccepted = true;
            _loginAccountAcceptedEula = true;
            summary = BuildGuestIdLoginSuccessSummary(packetProfile);
            _loginTitleStatusMessage = summary;
            _loginCharacterStatusMessage = summary;
            ShowLoginUtilityDialog(
                "Login Utility",
                summary,
                LoginUtilityDialogButtonLayout.Ok,
                LoginUtilityDialogAction.DismissOnly);
            return true;
        }

        private bool TryHandlePacketOwnedAccountInfoResult(out string summary, out bool continueRuntimeDispatch)
        {
            summary = null;
            continueRuntimeDispatch = true;

            if (!IsLoginRuntimeSceneActive ||
                !_loginPacketAccountDialogProfiles.TryGetValue(LoginPacketType.AccountInfoResult, out LoginAccountDialogPacketProfile packetProfile) ||
                packetProfile == null)
            {
                return false;
            }

            if (!IsSuccessfulAccountInfoResult(packetProfile))
            {
                summary = BuildAccountInfoResultFailureMessage(packetProfile);
                _loginTitleStatusMessage = summary;
                _loginCharacterStatusMessage = summary;
                ShowAccountInfoResultFailureDialog(packetProfile, summary);
                continueRuntimeDispatch = false;
                return true;
            }

            ApplyPacketOwnedAccountInfo(packetProfile);
            _loginAccountMigrationAccepted = true;
            _loginAccountAcceptedEula = true;
            summary = BuildAccountInfoResultSuccessSummary(packetProfile);
            _loginTitleStatusMessage = summary;
            _loginCharacterStatusMessage = summary;
            HideLoginUtilityDialog();
            return true;
        }

        private void ApplyPacketOwnedAccountInfo(LoginAccountDialogPacketProfile packetProfile)
        {
            if (packetProfile == null)
            {
                return;
            }

            _loginPacketAccountDialogProfiles[LoginPacketType.AccountInfoResult] = new LoginAccountDialogPacketProfile
            {
                PacketType = LoginPacketType.AccountInfoResult,
                Payload = packetProfile.Payload,
                ResultCode = packetProfile.ResultCode,
                SecondaryCode = packetProfile.SecondaryCode,
                AccountId = packetProfile.AccountId,
                CharacterId = packetProfile.CharacterId,
                Gender = packetProfile.Gender,
                GradeCode = packetProfile.GradeCode,
                AccountFlags = packetProfile.AccountFlags,
                CountryId = packetProfile.CountryId,
                ClubId = packetProfile.ClubId,
                PurchaseExperience = packetProfile.PurchaseExperience,
                ChatBlockReason = packetProfile.ChatBlockReason,
                ChatUnblockFileTime = packetProfile.ChatUnblockFileTime,
                RegisterDateFileTime = packetProfile.RegisterDateFileTime,
                CharacterCount = packetProfile.CharacterCount,
                ClientKey = packetProfile.ClientKey,
                RequestedName = packetProfile.RequestedName,
                TextValue = packetProfile.TextValue,
            };
        }

        private void ApplyPacketOwnedAccountInfo(LoginCheckPasswordResultProfile packetProfile)
        {
            ApplyPacketOwnedAccountInfo(packetProfile == null
                ? null
                : new LoginAccountDialogPacketProfile
                {
                    PacketType = LoginPacketType.AccountInfoResult,
                    Payload = packetProfile.Payload,
                    ResultCode = packetProfile.ResultCode,
                    AccountId = packetProfile.AccountId,
                    Gender = packetProfile.Gender,
                    GradeCode = packetProfile.GradeCode,
                    AccountFlags = packetProfile.AccountFlags,
                    CountryId = packetProfile.CountryId,
                    ClubId = packetProfile.ClubId,
                    PurchaseExperience = packetProfile.PurchaseExperience,
                    ChatBlockReason = packetProfile.ChatBlockReason,
                    ChatUnblockFileTime = packetProfile.ChatUnblockFileTime,
                    RegisterDateFileTime = packetProfile.RegisterDateFileTime,
                    CharacterCount = packetProfile.CharacterCount,
                    ClientKey = packetProfile.ClientKey,
                });
        }

        private void ApplyPacketOwnedAccountInfo(LoginGuestIdLoginResultProfile packetProfile)
        {
            ApplyPacketOwnedAccountInfo(packetProfile == null
                ? null
                : new LoginAccountDialogPacketProfile
                {
                    PacketType = LoginPacketType.AccountInfoResult,
                    Payload = packetProfile.Payload,
                    ResultCode = packetProfile.ResultCode,
                    AccountId = packetProfile.AccountId,
                    Gender = packetProfile.Gender,
                    GradeCode = packetProfile.GradeCode,
                    CountryId = packetProfile.CountryId,
                    ClubId = packetProfile.ClubId,
                    PurchaseExperience = packetProfile.PurchaseExperience,
                    ChatBlockReason = packetProfile.ChatBlockReason,
                    ChatUnblockFileTime = packetProfile.ChatUnblockFileTime,
                    RegisterDateFileTime = packetProfile.RegisterDateFileTime,
                    CharacterCount = packetProfile.CharacterCount,
                });
        }

        private static string BuildCheckPasswordSuccessSummary(LoginCheckPasswordResultProfile packetProfile)
        {
            string accountText = packetProfile?.AccountId.HasValue == true
                ? $" for account {packetProfile.AccountId.Value}"
                : string.Empty;
            string characterText = packetProfile?.CharacterCount.HasValue == true
                ? $" Character count {packetProfile.CharacterCount.Value}."
                : string.Empty;
            string keyText = packetProfile?.ClientKey?.Length == 8
                ? $" Client key {Convert.ToHexString(packetProfile.ClientKey)}."
                : string.Empty;
            return $"Packet-authored CheckPasswordResult accepted the login bootstrap{accountText} and preserved the client-owned account-info payload.{characterText}{keyText}".Trim();
        }

        private static string BuildCheckPasswordLicenseSummary(LoginCheckPasswordResultProfile packetProfile)
        {
            return $"Packet-authored CheckPasswordResult returned the client license-dialog path (result {packetProfile?.ResultCode}, mode {packetProfile?.AccountBootstrapMode}). The simulator now waits for the follow-up account-info owner instead of forcing a generic transition.";
        }

        private static string BuildCheckPasswordRegistrationSummary(LoginCheckPasswordResultProfile packetProfile)
        {
            return $"Packet-authored CheckPasswordResult requested the client website handoff for account bootstrap mode {packetProfile?.AccountBootstrapMode}.";
        }

        private static string BuildAccountInfoResultSuccessSummary(LoginAccountDialogPacketProfile packetProfile)
        {
            string accountText = packetProfile?.AccountId.HasValue == true
                ? $" for account {packetProfile.AccountId.Value}"
                : string.Empty;
            string characterText = packetProfile?.CharacterCount.HasValue == true
                ? $" Character count {packetProfile.CharacterCount.Value}."
                : string.Empty;
            string keyText = packetProfile?.ClientKey?.Length == 8
                ? $" Client key {Convert.ToHexString(packetProfile.ClientKey)}."
                : string.Empty;
            return $"Packet-authored AccountInfoResult promoted the client-owned account-info payload{accountText}.{characterText}{keyText}".Trim();
        }

        private static string BuildAccountInfoResultFailureMessage(LoginAccountDialogPacketProfile packetProfile)
        {
            return packetProfile?.ResultCode switch
            {
                4 => "Packet-authored AccountInfoResult rejected the account password.",
                5 => "Packet-authored AccountInfoResult rejected the account ID.",
                7 => "Packet-authored AccountInfoResult returned the login flow to title.",
                10 => "Packet-authored AccountInfoResult blocked the login request.",
                11 => "Packet-authored AccountInfoResult reported that the service is unavailable.",
                25 => "Packet-authored AccountInfoResult reported the client warning path 40.",
                _ => $"Packet-authored AccountInfoResult failed with result {packetProfile?.ResultCode}.",
            };
        }

        private void ShowAccountInfoResultFailureDialog(LoginAccountDialogPacketProfile packetProfile, string message)
        {
            int? noticeTextIndex = packetProfile?.ResultCode switch
            {
                255 or 6 or 8 or 9 => 15,
                2 or 3 => 16,
                4 => 3,
                5 => 20,
                7 => 17,
                10 => 19,
                11 => 14,
                13 => 21,
                14 => 27,
                15 => 26,
                16 or 21 => 33,
                17 => 27,
                25 => 40,
                _ => null,
            };

            if (packetProfile?.ResultCode == 7)
            {
                _loginRuntime.ForceStep(LoginStep.Title, "Packet-authored AccountInfoResult returned the login flow to title.");
            }

            if (packetProfile?.ResultCode is 14 or 15)
            {
                ShowLoginUtilityDialog(
                    "Login Utility",
                    message,
                    LoginUtilityDialogButtonLayout.YesNo,
                    LoginUtilityDialogAction.WebsiteHandoffDecision,
                    noticeTextIndex: noticeTextIndex);
                return;
            }

            ShowLoginUtilityDialog(
                "Login Utility",
                message,
                LoginUtilityDialogButtonLayout.Ok,
                LoginUtilityDialogAction.DismissOnly,
                noticeTextIndex: noticeTextIndex);
        }

        private static string BuildCheckPasswordResultFailureMessage(LoginCheckPasswordResultProfile packetProfile)
        {
            string baseMessage = packetProfile?.ResultCode switch
            {
                4 => "Packet-authored CheckPasswordResult rejected the account password.",
                5 => "Packet-authored CheckPasswordResult rejected the account ID.",
                7 => "Packet-authored CheckPasswordResult returned the login flow to title.",
                10 => "Packet-authored CheckPasswordResult blocked the login request.",
                11 => "Packet-authored CheckPasswordResult reported that the service is unavailable.",
                25 => "Packet-authored CheckPasswordResult reported the client warning path 40.",
                _ => $"Packet-authored CheckPasswordResult failed with result {packetProfile?.ResultCode}.",
            };

            if (packetProfile?.Payload?.Length > 0)
            {
                baseMessage += $" Bootstrap mode {packetProfile.AccountBootstrapMode}.";
            }

            return baseMessage;
        }

        private void ShowCheckPasswordResultFailureDialog(LoginCheckPasswordResultProfile packetProfile, string message)
        {
            int? noticeTextIndex = packetProfile?.ResultCode switch
            {
                255 or 6 or 8 or 9 => 15,
                2 or 3 => 16,
                4 => 3,
                5 => 20,
                7 => 17,
                10 => 19,
                11 => 14,
                13 => 21,
                14 => 27,
                15 => 26,
                16 or 21 => 33,
                17 => 27,
                25 => 40,
                _ => null,
            };

            if (packetProfile?.ResultCode == 7)
            {
                _loginRuntime.ForceStep(LoginStep.Title, "Packet-authored CheckPasswordResult returned the login flow to title.");
            }

            if (packetProfile?.ResultCode is 14 or 15)
            {
                ShowLoginUtilityDialog(
                    "Login Utility",
                    message,
                    LoginUtilityDialogButtonLayout.YesNo,
                    LoginUtilityDialogAction.WebsiteHandoffDecision,
                    noticeTextIndex: noticeTextIndex);
                return;
            }

            ShowLoginUtilityDialog(
                "Login Utility",
                message,
                LoginUtilityDialogButtonLayout.Ok,
                LoginUtilityDialogAction.DismissOnly,
                noticeTextIndex: noticeTextIndex);
        }

        private static string BuildGuestIdLoginSuccessSummary(LoginGuestIdLoginResultProfile packetProfile)
        {
            string accountText = packetProfile?.AccountId.HasValue == true
                ? $" for account {packetProfile.AccountId.Value}"
                : string.Empty;
            string urlText = string.IsNullOrWhiteSpace(packetProfile?.GuestRegistrationUrl)
                ? string.Empty
                : " Guest registration URL preserved.";
            return $"Packet-authored GuestIdLoginResult accepted the guest bootstrap{accountText} and surfaced the client-owned license path.{urlText}".Trim();
        }

        private static string BuildGuestIdLoginRegistrationSummary(LoginGuestIdLoginResultProfile packetProfile)
        {
            return $"Packet-authored GuestIdLoginResult requested the client guest-registration website handoff for status {packetProfile?.RegistrationStatusId}.";
        }

        private static string BuildGuestIdLoginFailureMessage(LoginGuestIdLoginResultProfile packetProfile)
        {
            return packetProfile?.ResultCode switch
            {
                4 => "Packet-authored GuestIdLoginResult rejected the guest login request.",
                7 => "Packet-authored GuestIdLoginResult returned the login flow to title.",
                _ => $"Packet-authored GuestIdLoginResult failed with result {packetProfile?.ResultCode}.",
            };
        }

        private void ShowGuestIdLoginFailureDialog(LoginGuestIdLoginResultProfile packetProfile, string message)
        {
            int? noticeTextIndex = packetProfile?.ResultCode switch
            {
                255 or 6 or 8 or 9 => 15,
                2 or 3 => 16,
                4 => 3,
                5 => 20,
                7 => 17,
                10 => 19,
                11 => 14,
                13 => 21,
                14 => 27,
                15 => 26,
                16 or 21 => 33,
                17 => 27,
                25 => 40,
                _ => null,
            };

            if (packetProfile?.ResultCode == 7)
            {
                _loginRuntime.ForceStep(LoginStep.Title, "Packet-authored GuestIdLoginResult returned the login flow to title.");
            }

            if (packetProfile?.ResultCode is 14 or 15)
            {
                ShowLoginUtilityDialog(
                    "Login Utility",
                    message,
                    LoginUtilityDialogButtonLayout.YesNo,
                    LoginUtilityDialogAction.WebsiteHandoffDecision,
                    noticeTextIndex: noticeTextIndex);
                return;
            }

            ShowLoginUtilityDialog(
                "Login Utility",
                message,
                LoginUtilityDialogButtonLayout.Ok,
                LoginUtilityDialogAction.DismissOnly,
                noticeTextIndex: noticeTextIndex);
        }



        private static bool ShouldReturnSelectCharacterFailureToTitle(LoginSelectCharacterResultProfile packetProfile)

        {

            if (packetProfile == null)

            {

                return false;

            }



            return packetProfile.ResultCode switch

            {

                7 => true,

                12 => packetProfile.ResponseCode is 1 or 2 or 3 or 19 or 25 or 27 or 28,

                _ => false,

            };

        }

        private static bool ShouldReturnSelectCharacterByVacFailureToTitle(LoginSelectCharacterByVacResultProfile packetProfile)

        {

            if (packetProfile == null)

            {

                return false;

            }



            return packetProfile.ReturnsToTitle;

        }



        private string BuildSelectCharacterResultSuccessMessage(

            LoginSelectCharacterResultProfile packetProfile,

            LoginCharacterRosterEntry entry)

        {

            string characterName = entry?.Build?.Name ?? $"Character {packetProfile?.CharacterId.GetValueOrDefault() ?? 0}";

            string endpoint = packetProfile?.EndpointText;

            string premiumText = packetProfile?.PremiumArgument.HasValue == true

                ? $" Premium argument {packetProfile.PremiumArgument.Value}."

                : string.Empty;



            return string.IsNullOrWhiteSpace(endpoint)

                ? $"Packet-authored SelectCharacterResult entered the field with {characterName}.{premiumText}"

                : $"Packet-authored SelectCharacterResult entered the field with {characterName} via {endpoint}.{premiumText}";

        }

        private string BuildSelectCharacterByVacResultSuccessMessage(

            LoginSelectCharacterByVacResultProfile packetProfile,

            LoginCharacterRosterEntry entry)

        {

            string characterName = entry?.Build?.Name ?? $"Character {packetProfile?.CharacterId.GetValueOrDefault() ?? 0}";

            string endpoint = packetProfile?.EndpointText;

            string premiumText = packetProfile?.PremiumArgument.HasValue == true

                ? $" Premium argument {packetProfile.PremiumArgument.Value}."

                : string.Empty;



            string branchText = packetProfile?.UsesDirectSuccessBranch == true
                ? "direct success branch"
                : packetProfile?.UsesAlternateAuthenticatedBranch == true
                    ? $"alternate authenticated branch (result 12 / secondary {packetProfile.SecondaryCode})"
                    : packetProfile?.UsesAlternateSuccessBranch == true
                        ? "alternate result 23 branch"
                        : "packet-owned VAC branch";

            return string.IsNullOrWhiteSpace(endpoint)

                ? $"Packet-authored SelectCharacterByVACResult entered the field with {characterName} through the {branchText}.{premiumText}"

                : $"Packet-authored SelectCharacterByVACResult entered the field with {characterName} via {endpoint} through the {branchText}.{premiumText}";

        }



        private static string BuildSelectCharacterResultFailureMessage(LoginSelectCharacterResultProfile packetProfile)

        {

            if (packetProfile == null)

            {

                return "SelectCharacterResult failed.";

            }



            if (packetProfile.ResultCode == 12)

            {

                return packetProfile.ResponseCode switch

                {

                    1 => "SelectCharacterResult rejected the selected character and returned the login flow to title.",

                    2 => "SelectCharacterResult reported a blocked character and returned the login flow to title.",

                    3 => "SelectCharacterResult reported that the selected character could not enter the field and returned the login flow to title.",

                    19 => "SelectCharacterResult reported a blocked login and returned the login flow to title.",

                    25 => "SelectCharacterResult reported a migration-required account and returned the login flow to title.",

                    27 => "SelectCharacterResult reported an unsupported region response and returned the login flow to title.",

                    28 => "SelectCharacterResult reported a newer client requirement and returned the login flow to title.",

                    _ => $"SelectCharacterResult returned server code {FormatSelectorPacketCode(packetProfile.ResultCode)} with response {packetProfile.ResponseCode}.",

                };

            }



            return $"SelectCharacterResult returned server code {FormatSelectorPacketCode(packetProfile.ResultCode)}.";

        }

        private static string BuildSelectCharacterByVacResultFailureMessage(LoginSelectCharacterByVacResultProfile packetProfile)

        {

            if (packetProfile == null)

            {

                return "SelectCharacterByVACResult failed.";

            }



            string secondaryText = packetProfile.SecondaryCode != 0
                ? $" Secondary code {packetProfile.SecondaryCode}."
                : string.Empty;

            if (packetProfile.RequiresWebsiteHandoff)
            {
                return packetProfile.ResultCode == 14
                    ? $"SelectCharacterByVACResult requested the client security website handoff for VAC result 14.{secondaryText}"
                    : $"SelectCharacterByVACResult requested the client website handoff for VAC result 15.{secondaryText}";
            }

            if (packetProfile.ResultCode == 12)
            {
                return $"SelectCharacterByVACResult rejected alternate authenticated entry for result 12 with secondary code {packetProfile.SecondaryCode}.{(packetProfile.ReturnsToTitle ? " The login flow returned to title." : string.Empty)}";
            }

            if (packetProfile.NoticeTextIndex.HasValue)
            {
                return $"SelectCharacterByVACResult mapped the VAC entry failure to login notice {packetProfile.NoticeTextIndex.Value}.{(packetProfile.ReturnsToTitle ? " The login flow returned to title." : string.Empty)}{secondaryText}";
            }

            return $"SelectCharacterByVACResult returned result {FormatSelectorPacketCode(packetProfile.ResultCode)} with secondary code {packetProfile.SecondaryCode}.";

        }



        private void ShowSelectCharacterFailureDialog(LoginSelectCharacterResultProfile packetProfile, string message)

        {

            if (packetProfile == null)

            {

                return;

            }



            int? noticeTextIndex = ResolveSelectCharacterFailureNoticeTextIndex(packetProfile);

            ShowLoginUtilityDialog(

                "Login Utility",

                message,

                LoginUtilityDialogButtonLayout.Ok,

                LoginUtilityDialogAction.DismissOnly,

                noticeTextIndex: noticeTextIndex);

        }

        private void ShowSelectCharacterByVacFailureDialog(LoginSelectCharacterByVacResultProfile packetProfile, string message)

        {

            if (packetProfile == null)

            {

                return;

            }



            int? noticeTextIndex = ResolveSelectCharacterByVacFailureNoticeTextIndex(packetProfile);

            if (packetProfile.RequiresWebsiteHandoff)

            {

                ShowLoginUtilityDialog(

                    "Login Utility",

                    message,

                    LoginUtilityDialogButtonLayout.YesNo,

                    LoginUtilityDialogAction.WebsiteHandoffDecision,

                    noticeTextIndex: noticeTextIndex);

                return;

            }



            ShowLoginUtilityDialog(

                "Login Utility",

                message,

                LoginUtilityDialogButtonLayout.Ok,

                LoginUtilityDialogAction.DismissOnly,

                noticeTextIndex: noticeTextIndex);

        }



        private static int? ResolveSelectCharacterFailureNoticeTextIndex(LoginSelectCharacterResultProfile packetProfile)

        {

            if (packetProfile == null)

            {

                return null;

            }



            if (packetProfile.ResultCode == 12)

            {

                return packetProfile.ResponseCode switch

                {

                    1 => 28,

                    2 => 29,

                    3 => 30,

                    19 => 25,

                    25 => 31,

                    27 => 56,

                    28 => 62,

                    _ => 15,

                };

            }



            return packetProfile.ResultCode switch

            {

                2 or 3 => 16,

                4 => 3,

                5 => 20,

                7 => 17,

                10 => 19,

                11 => 14,

                13 => 21,

                16 or 21 => 33,

                17 => 27,

                25 => 40,

                _ => 15,

            };

        }

        private static int? ResolveSelectCharacterByVacFailureNoticeTextIndex(LoginSelectCharacterByVacResultProfile packetProfile)

        {

            if (packetProfile == null)

            {

                return null;

            }



            return packetProfile.NoticeTextIndex;

        }



        private void ApplyViewAllCharResultProfile()

        {

            if (_loginPacketViewAllCharResultProfile == null)

            {

                return;

            }



            switch (_loginPacketViewAllCharResultProfile.Kind)

            {

                case LoginViewAllCharResultKind.Header:

                    _loginPacketViewAllCharEntries.Clear();

                    _loginPacketViewAllCharRosterProfile = null;

                    _loginPacketViewAllCharRemainingServerCount = Math.Max(0, _loginPacketViewAllCharResultProfile.RelatedServerCount);

                    _loginPacketViewAllCharExpectedCharacterCount = Math.Max(0, _loginPacketViewAllCharResultProfile.CharacterCount);

                    _loginCharacterStatusMessage = _loginPacketViewAllCharExpectedCharacterCount > 0

                        ? $"Waiting for {_loginPacketViewAllCharExpectedCharacterCount} packet-authored VAC roster entries."

                        : "ViewAllCharResult header did not advertise any characters.";

                    break;



                case LoginViewAllCharResultKind.Characters:

                    _loginPacketViewAllCharEntries.AddRange(_loginPacketViewAllCharResultProfile.Entries);

                    if (_loginPacketViewAllCharRemainingServerCount > 0)

                    {

                        _loginPacketViewAllCharRemainingServerCount--;

                    }



                    if (_loginPacketViewAllCharRemainingServerCount <= 0)

                    {

                        _loginPacketViewAllCharRosterProfile = new LoginSelectWorldResultProfile

                        {

                            ResultCode = 0,

                            Entries = _loginPacketViewAllCharEntries.ToArray(),

                            LoginOpt = _loginPacketViewAllCharResultProfile.LoginOpt ?? false,

                            SlotCount = _loginPacketViewAllCharEntries.Count,

                            BuyCharacterCount = _loginCanHaveExtraCharacter ? 1 : 0

                        };

                    }

                    break;



                case LoginViewAllCharResultKind.Completion:

                    if (_loginPacketViewAllCharEntries.Count > 0)

                    {

                        _loginPacketViewAllCharRosterProfile ??= new LoginSelectWorldResultProfile

                        {

                            ResultCode = 0,

                            Entries = _loginPacketViewAllCharEntries.ToArray(),

                            LoginOpt = false,

                            SlotCount = _loginPacketViewAllCharEntries.Count,

                            BuyCharacterCount = _loginCanHaveExtraCharacter ? 1 : 0

                        };

                    }

                    break;

            }

        }

        private void ApplyCheckDuplicatedIdResultProfile()

        {
            if (!TryGetLoginCheckDuplicatedIdPacketProfile(out LoginAccountDialogPacketProfile packetProfile) ||
                !packetProfile.ResultCode.HasValue)
            {
                return;
            }

            string resolvedName = string.IsNullOrWhiteSpace(packetProfile.RequestedName)
                ? _loginPendingCreateCharacterName
                : packetProfile.RequestedName.Trim();
            _loginPendingCreateCharacterName = resolvedName;

            if (packetProfile.ResultCode.Value != 0)
            {
                _loginCharacterStatusMessage = BuildCheckDuplicatedIdPacketFailureMessage(packetProfile);
                _loginCreateCharacterFlow?.ClearCheckedName(_loginCharacterStatusMessage);
                return;
            }

            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                _loginCharacterStatusMessage = "CheckDuplicatedIdResult succeeded, but no character name was present.";
                _loginCreateCharacterFlow?.ClearCheckedName(_loginCharacterStatusMessage);
                return;
            }

            if (_loginCreateCharacterFlow != null)
            {
                _loginCreateCharacterFlow.AcceptCheckedName(resolvedName);
                _loginCreateCharacterFlow.SetStage(LoginCreateCharacterStage.AvatarSelect, _loginCreateCharacterFlow.StatusMessage);
                _loginRuntime.ForceStep(LoginStep.NewCharacterAvatar, "Returned to avatar selection after duplicate-name validation.");
                _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;
                return;
            }

            ContinueLoginCharacterCreateAfterDuplicateCheck(resolvedName);
        }



        private void ApplyCreateNewCharacterResultProfile()

        {
            _loginPendingCreateCharacterName = null;

            if (_loginPacketCreateNewCharacterResultProfile == null)

            {

                return;

            }



            if (!_loginPacketCreateNewCharacterResultProfile.IsSuccess)

            {

                _loginCharacterStatusMessage =

                    $"CreateNewCharacterResult returned server code {FormatSelectorPacketCode(_loginPacketCreateNewCharacterResultProfile.ResultCode)}.";

                if (_loginCreateCharacterFlow != null)

                {

                    _loginCreateCharacterFlow.ClearCheckedName(_loginCharacterStatusMessage);

                }

                return;

            }



            LoginSelectWorldCharacterEntry packetEntry = _loginPacketCreateNewCharacterResultProfile.CreatedCharacter;

            if (packetEntry == null)

            {

                _loginCharacterStatusMessage = "CreateNewCharacterResult succeeded, but no starter avatar data was present.";

                return;

            }



            if (_loginCharacterRoster.Entries.Count >= LoginCharacterRosterManager.MaxCharacterSlotCount)

            {

                _loginCharacterStatusMessage = "CreateNewCharacterResult succeeded, but the account-backed roster is already full.";

                return;

            }



            CharacterBuild build = CreateLoginCharacterBuildFromPacket(packetEntry);

            int targetMapId = packetEntry.FieldMapId > 0 ? packetEntry.FieldMapId : ResolveLoginCharacterTargetMapId();

            string targetMapDisplayName = ResolveMapTransferDisplayName(targetMapId);



            List<LoginCharacterRosterEntry> entries = _loginCharacterRoster.Entries.ToList();
            bool consumedExtraCharacterSlot =
                _loginCharacterRoster.Entries.Count >= _loginCharacterRoster.SlotCount &&
                _loginCharacterRoster.BuyCharacterCount > 0;
            LoginCharacterRosterEntry rosterEntry = CreateLoginCharacterRosterEntry(
                build,
                targetMapId,
                targetMapDisplayName,
                previousWorldRank: 0,
                previousJobRank: 0,
                avatarLookPacket: packetEntry.AvatarLookPacket,
                portal: packetEntry.Portal);
            int existingIndex = entries.FindIndex(entry => entry?.Build?.Id == build.Id);
            if (existingIndex >= 0)
            {
                entries[existingIndex] = rosterEntry;
            }
            else if (rosterEntry != null)
            {
                entries.Add(rosterEntry);
            }

            int slotCount = consumedExtraCharacterSlot
                ? Math.Max(_loginCharacterRoster.SlotCount + 1, entries.Count)
                : Math.Max(_loginCharacterRoster.SlotCount, entries.Count);
            int buyCharacterCount = consumedExtraCharacterSlot
                ? Math.Max(0, _loginCharacterRoster.BuyCharacterCount - 1)
                : _loginCharacterRoster.BuyCharacterCount;
            if (consumedExtraCharacterSlot)
            {
                _loginCanHaveExtraCharacter = buyCharacterCount > 0;
            }

            ApplyResolvedLoginCharacterRoster(entries, slotCount, buyCharacterCount, selectedCharacterId: build.Id);
            _loginRuntime.ForceStep(
                LoginStep.CharacterSelect,
                $"CreateNewCharacterResult accepted {build.Name} and returned to character selection.");

            _loginCharacterStatusMessage =
                existingIndex >= 0
                    ? $"Updated {build.Name} from CreateNewCharacterResult."
                    : $"Created {build.Name} Lv.{Math.Max(1, build.Level)} {build.JobName} from CreateNewCharacterResult.";

            _loginCreateCharacterFlow = null;
        }

        private void ApplyDeleteCharacterResultProfile()
        {
            if (!TryGetLoginDeleteCharacterPacketProfile(out LoginAccountDialogPacketProfile packetProfile))
            {
                return;
            }

            if (packetProfile.ResultCode.GetValueOrDefault(byte.MaxValue) != 0)
            {
                _loginCharacterStatusMessage = BuildDeleteCharacterPacketFailureMessage(packetProfile);
                return;
            }

            int characterId = packetProfile.CharacterId.GetValueOrDefault();
            if (characterId <= 0)
            {
                _loginCharacterStatusMessage = "DeleteCharacterResult succeeded, but the packet did not identify a character.";
                return;
            }

            List<LoginCharacterRosterEntry> entries = _loginCharacterRoster.Entries.ToList();
            int removedIndex = entries.FindIndex(entry => entry?.Build?.Id == characterId);
            if (removedIndex < 0)
            {
                _loginCharacterStatusMessage = $"DeleteCharacterResult removed character {characterId}, but that character is not in the active roster.";
                return;
            }

            LoginCharacterRosterEntry deletedEntry = entries[removedIndex];
            entries.RemoveAt(removedIndex);

            int slotCount = Math.Max(_loginCharacterRoster.SlotCount, LoginCharacterRosterManager.EntriesPerPage);
            int buyCharacterCount = Math.Max(0, _loginCharacterRoster.BuyCharacterCount);
            int selectedIndex = entries.Count == 0 ? -1 : Math.Min(removedIndex, entries.Count - 1);

            ApplyResolvedLoginCharacterRoster(entries, slotCount, buyCharacterCount, selectedIndex: selectedIndex);

            string deletedName = deletedEntry?.Build?.Name;
            _loginCharacterStatusMessage = string.IsNullOrWhiteSpace(deletedName)
                ? $"Deleted character {characterId} from DeleteCharacterResult."
                : $"Deleted {deletedName} from DeleteCharacterResult.";
        }


        private static CharacterBuild CreateLoginCharacterBuildFromPacket(LoginSelectWorldCharacterEntry packetEntry)

        {

            return new CharacterBuild

            {

                Id = packetEntry.CharacterId,

                Name = string.IsNullOrWhiteSpace(packetEntry.Name) ? $"Character {packetEntry.CharacterId}" : packetEntry.Name,

                Gender = packetEntry.Gender,

                Skin = packetEntry.Skin,

                Level = Math.Max(1, packetEntry.Level),

                Job = packetEntry.JobId,

                SubJob = packetEntry.SubJob,

                JobName = SkillDataLoader.GetJobName(packetEntry.JobId),

                Fame = packetEntry.Fame,

                WorldRank = packetEntry.WorldRank ?? 0,

                JobRank = packetEntry.JobRank ?? 0,

                Exp = Math.Max(0L, packetEntry.Experience),

                HP = Math.Max(0, packetEntry.HitPoints),

                MaxHP = Math.Max(1, packetEntry.MaxHitPoints),

                MP = Math.Max(0, packetEntry.ManaPoints),

                MaxMP = Math.Max(0, packetEntry.MaxManaPoints),

                STR = Math.Max(0, packetEntry.Strength),

                DEX = Math.Max(0, packetEntry.Dexterity),

                INT = Math.Max(0, packetEntry.Intelligence),

                LUK = Math.Max(0, packetEntry.Luck),

                AP = Math.Max(0, packetEntry.AbilityPoints)

            };

        }



        private static CharacterBuild CreateLoginCharacterBuildFromAccountState(LoginCharacterAccountStore.LoginCharacterAccountEntryState entry)


        {



            return new CharacterBuild



            {



                Id = entry.CharacterId,



                Name = string.IsNullOrWhiteSpace(entry.Name) ? $"Character {entry.CharacterId}" : entry.Name,



                Gender = entry.Gender,



                Skin = entry.Skin,



                Level = Math.Max(1, entry.Level),



                Job = entry.Job,



                SubJob = entry.SubJob,



                JobName = string.IsNullOrWhiteSpace(entry.JobName) ? SkillDataLoader.GetJobName(entry.Job) : entry.JobName,



                GuildName = entry.GuildName ?? string.Empty,



                AllianceName = entry.AllianceName ?? string.Empty,



                Fame = Math.Max(0, entry.Fame),



                WorldRank = Math.Max(0, entry.WorldRank),



                JobRank = Math.Max(0, entry.JobRank),



                Exp = Math.Max(0L, entry.Exp),



                ExpToNextLevel = Math.Max(0L, entry.ExpToNextLevel),



                HP = Math.Max(0, entry.HP),



                MaxHP = Math.Max(1, entry.MaxHP),



                MP = Math.Max(0, entry.MP),



                MaxMP = Math.Max(0, entry.MaxMP),



                STR = Math.Max(0, entry.Strength),



                DEX = Math.Max(0, entry.Dexterity),



                INT = Math.Max(0, entry.Intelligence),



                LUK = Math.Max(0, entry.Luck),



                AP = Math.Max(0, entry.AbilityPoints)



            };

        }

        private void ApplyResolvedLoginCharacterRoster(
            IReadOnlyList<LoginCharacterRosterEntry> entries,
            int slotCount,
            int buyCharacterCount,
            int? selectedCharacterId = null,
            int? selectedIndex = null)
        {
            List<LoginCharacterRosterEntry> resolvedEntries = entries?
                .Where(entry => entry != null)
                .ToList()
                ?? new List<LoginCharacterRosterEntry>();

            int normalizedSlotCount = Math.Clamp(
                Math.Max(Math.Max(0, slotCount), resolvedEntries.Count),
                0,
                LoginCharacterRosterManager.MaxCharacterSlotCount);
            int normalizedBuyCharacterCount = Math.Clamp(
                Math.Max(0, buyCharacterCount),
                0,
                LoginCharacterRosterManager.MaxCharacterSlotCount);

            _loginCharacterRoster.SetEntries(resolvedEntries, normalizedSlotCount, normalizedBuyCharacterCount);
            SyncPacketOwnedLoginRosterProfiles(resolvedEntries, normalizedSlotCount, normalizedBuyCharacterCount);
            PersistLoginCharacterRosterToAccountStore(resolvedEntries, normalizedSlotCount, normalizedBuyCharacterCount);

            if (selectedCharacterId.HasValue && selectedCharacterId.Value > 0)
            {
                SelectLoginCharacterById(selectedCharacterId.Value);
            }
            else if (selectedIndex.HasValue && selectedIndex.Value >= 0 && resolvedEntries.Count > 0)
            {
                _loginCharacterRoster.Select(Math.Min(selectedIndex.Value, resolvedEntries.Count - 1));
            }

            FinalizeLoginCharacterRosterInitialization();
        }

        private void SyncPacketOwnedLoginRosterProfiles(
            IReadOnlyList<LoginCharacterRosterEntry> entries,
            int slotCount,
            int buyCharacterCount)
        {
            LoginSelectWorldCharacterEntry[] packetEntries = CreateLoginPacketCharacterEntries(entries);
            if (_loginPacketSelectWorldResultProfile != null &&
                LoginSelectWorldResultCodec.IsSuccessCode(_loginPacketSelectWorldResultProfile.ResultCode))
            {
                _loginPacketSelectWorldResultProfile = new LoginSelectWorldResultProfile
                {
                    ResultCode = _loginPacketSelectWorldResultProfile.ResultCode,
                    Entries = packetEntries,
                    LoginOpt = _loginPacketSelectWorldResultProfile.LoginOpt,
                    SlotCount = slotCount,
                    BuyCharacterCount = buyCharacterCount
                };
            }

            if (_loginPacketViewAllCharRosterProfile != null ||
                _loginPacketViewAllCharEntries.Count > 0 ||
                _loginRuntime.CurrentStep == LoginStep.ViewAllCharacters)
            {
                _loginPacketViewAllCharEntries.Clear();
                _loginPacketViewAllCharEntries.AddRange(packetEntries);
                _loginPacketViewAllCharRosterProfile = new LoginSelectWorldResultProfile
                {
                    ResultCode = 0,
                    Entries = packetEntries,
                    LoginOpt = _loginPacketViewAllCharRosterProfile?.LoginOpt ?? false,
                    SlotCount = slotCount,
                    BuyCharacterCount = buyCharacterCount
                };
                _loginPacketViewAllCharExpectedCharacterCount = packetEntries.Length;
                _loginPacketViewAllCharRemainingServerCount = 0;
            }
        }

        private LoginSelectWorldCharacterEntry[] CreateLoginPacketCharacterEntries(IReadOnlyList<LoginCharacterRosterEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<LoginSelectWorldCharacterEntry>();
            }

            List<LoginSelectWorldCharacterEntry> packetEntries = new(entries.Count);
            foreach (LoginCharacterRosterEntry entry in entries)
            {
                LoginSelectWorldCharacterEntry packetEntry = CreateLoginPacketCharacterEntry(entry);
                if (packetEntry != null)
                {
                    packetEntries.Add(packetEntry);
                }
            }

            return packetEntries.ToArray();
        }

        private LoginSelectWorldCharacterEntry CreateLoginPacketCharacterEntry(LoginCharacterRosterEntry entry)
        {
            CharacterBuild build = entry?.Build;
            if (build == null)
            {
                return null;
            }

            byte[] avatarLookPacket = entry.AvatarLookPacket?.Length > 0
                ? (byte[])entry.AvatarLookPacket.Clone()
                : LoginAvatarLookCodec.Encode(build);
            LoginAvatarLookCodec.TryDecode(avatarLookPacket, out LoginAvatarLook avatarLook, out _);

            int? worldRank = build.WorldRank > 0 ? build.WorldRank : null;
            int? previousWorldRank = entry.PreviousWorldRank;
            int? jobRank = build.JobRank > 0 ? build.JobRank : null;
            int? previousJobRank = entry.PreviousJobRank;

            return new LoginSelectWorldCharacterEntry
            {
                CharacterId = build.Id,
                WorldId = ResolveLoginRosterWorldId(),
                Name = build.Name ?? string.Empty,
                Gender = build.Gender,
                Skin = build.Skin,
                FaceId = avatarLook?.FaceId ?? build.Face?.ItemId ?? 0,
                HairId = avatarLook?.HairId ?? build.Hair?.ItemId ?? 0,
                Level = Math.Max(1, build.Level),
                JobId = build.Job,
                SubJob = build.SubJob,
                Strength = Math.Max(0, build.STR),
                Dexterity = Math.Max(0, build.DEX),
                Intelligence = Math.Max(0, build.INT),
                Luck = Math.Max(0, build.LUK),
                AbilityPoints = Math.Max(0, build.AP),
                HitPoints = Math.Max(0, build.HP),
                MaxHitPoints = Math.Max(1, build.MaxHP),
                ManaPoints = Math.Max(0, build.MP),
                MaxManaPoints = Math.Max(0, build.MaxMP),
                Experience = Math.Max(0L, build.Exp),
                Fame = Math.Max(0, build.Fame),
                FieldMapId = entry.FieldMapId,
                Portal = (byte)Math.Clamp(entry.Portal, 0, byte.MaxValue),
                PlayTime = 0,
                OnFamily = false,
                WorldRank = worldRank,
                WorldRankMove = worldRank.HasValue && previousWorldRank.HasValue
                    ? previousWorldRank.Value - worldRank.Value
                    : null,
                JobRank = jobRank,
                JobRankMove = jobRank.HasValue && previousJobRank.HasValue
                    ? previousJobRank.Value - jobRank.Value
                    : null,
                AvatarLook = avatarLook,
                AvatarLookPacket = avatarLookPacket ?? Array.Empty<byte>()
            };
        }

        private bool TryGetLoginDeleteCharacterPacketProfile(out LoginAccountDialogPacketProfile packetProfile)
        {
            return _loginPacketAccountDialogProfiles.TryGetValue(LoginPacketType.DeleteCharacterResult, out packetProfile) &&
                   packetProfile != null;
        }

        private bool TryGetLoginCheckDuplicatedIdPacketProfile(out LoginAccountDialogPacketProfile packetProfile)
        {
            return _loginPacketAccountDialogProfiles.TryGetValue(LoginPacketType.CheckDuplicatedIdResult, out packetProfile) &&
                   packetProfile != null;
        }

        private static string BuildDeleteCharacterPacketFailureMessage(LoginAccountDialogPacketProfile packetProfile)
        {
            string packetText = packetProfile?.TextValue?.Trim();
            if (!string.IsNullOrWhiteSpace(packetText))
            {
                return packetText;
            }

            string resultCode = packetProfile?.ResultCode.HasValue == true
                ? FormatSelectorPacketCode(packetProfile.ResultCode.Value)
                : "unknown";
            return packetProfile?.CharacterId.HasValue == true
                ? $"DeleteCharacterResult returned server code {resultCode} for character {packetProfile.CharacterId.Value}."
                : $"DeleteCharacterResult returned server code {resultCode}.";
        }

        private static string BuildCheckDuplicatedIdPacketFailureMessage(LoginAccountDialogPacketProfile packetProfile)
        {
            string packetText = packetProfile?.TextValue?.Trim();
            if (!string.IsNullOrWhiteSpace(packetText))
            {
                return packetText;
            }

            string characterName = string.IsNullOrWhiteSpace(packetProfile?.RequestedName)
                ? "the requested name"
                : packetProfile.RequestedName.Trim();

            return packetProfile?.ResultCode switch
            {
                1 => $"{characterName} is already in use.",
                2 => $"{characterName} is not available.",
                byte resultCode => $"CheckDuplicatedIdResult returned server code {FormatSelectorPacketCode(resultCode)} for {characterName}.",
                _ => "CheckDuplicatedIdResult did not include a result code.",
            };
        }



        private static void ApplyLoginCharacterDetailDefaults(
            CharacterBuild build,

            int minimumLevel,

            string fallbackGuildName,

            int fallbackFame,

            int fallbackExpPercent,

            int fallbackWorldRank,

            int fallbackJobRank)

        {

            if (build == null)

            {

                return;

            }



            build.Level = Math.Max(build.Level, minimumLevel);

            build.GuildName = string.IsNullOrWhiteSpace(build.GuildName) ? fallbackGuildName : build.GuildName;

            if (build.Fame <= 0)

            {

                build.Fame = fallbackFame;

            }



            if (build.WorldRank <= 0)

            {

                build.WorldRank = fallbackWorldRank;

            }



            if (build.JobRank <= 0)

            {

                build.JobRank = fallbackJobRank;

            }



            if (build.ExpToNextLevel <= 0)

            {

                build.ExpToNextLevel = 100;

            }



            if (build.Exp <= 0)

            {

                int clampedPercent = Math.Clamp(fallbackExpPercent, 0, 100);

                build.Exp = (build.ExpToNextLevel * clampedPercent) / 100;

            }

        }



        private void FinalizeLoginCharacterRosterInitialization()



        {



            HideLoginUtilityDialog();



            WireLoginCharacterSelectWindow();



            SyncLoginCharacterSelectWindow();



            SyncLoginEntryDialogs();







            SyncStorageAccessContext();









        }







        private string ResolveLoginRosterAccountName()



        {



            string accountName = _loginTitleAccountName?.Trim();



            return string.IsNullOrWhiteSpace(accountName) ? "explorergm" : accountName;



        }







        private int ResolveLoginRosterWorldId()



        {



            return Math.Max(0, _simulatorWorldId);



        }







        private LoginRosterSource GetActiveLoginRosterSource()



        {



            if (!TryGetActiveLoginRosterPacketProfile(out _, out string packetSource))



            {



                return _loginCharacterAccountStore.GetState(ResolveLoginRosterAccountName(), ResolveLoginRosterWorldId()) != null



                    ? LoginRosterSource.AccountStore



                    : LoginRosterSource.None;



            }







            return string.Equals(packetSource, "ViewAllCharResult", StringComparison.Ordinal)



                ? LoginRosterSource.ViewAllCharResult



                : LoginRosterSource.SelectWorldResult;



        }







        private bool CanMutateAccountBackedLoginRoster(out string message)



        {



            LoginRosterSource rosterSource = GetActiveLoginRosterSource();



            if (rosterSource == LoginRosterSource.SelectWorldResult || rosterSource == LoginRosterSource.ViewAllCharResult)



            {



                message = rosterSource == LoginRosterSource.ViewAllCharResult



                    ? "Packet-authored VAC roster data is currently authoritative. Clear the ViewAllCharResult payload before mutating the local account roster."



                    : "Packet-authored SelectWorldResult data is currently authoritative. Clear the SelectWorldResult payload before mutating the local account roster.";



                return false;



            }







            message = null;



            return true;



        }







        private void ApplyStoredLoginAccountSecurity(LoginCharacterAccountStore.LoginCharacterAccountState storedState)



        {



            _loginAccountCashShopNxCredit = Math.Max(0L, storedState?.CashShopNxCredit ?? DefaultCashShopNxCredit);

            _loginAccountPicCode = storedState?.PicCode?.Trim() ?? string.Empty;

            _loginAccountBirthDate = storedState?.BirthDate?.Trim() ?? string.Empty;



            _loginAccountSpwEnabled = storedState?.IsSecondaryPasswordEnabled ?? false;



            _loginAccountSecondaryPassword = storedState?.SecondaryPassword?.Trim() ?? string.Empty;



            SyncCashShopAccountCredit();
            SyncStorageAccessContext();

        }



        private void PersistLoginAccountSecurityState()



        {



            PersistLoginCharacterRosterToAccountStore(



                _loginCharacterRoster.Entries,



                _loginCharacterRoster.SlotCount,



                _loginCharacterRoster.BuyCharacterCount);



            SyncStorageAccessContext();

        }



        private void PersistLoginCharacterRosterToAccountStore(



            IEnumerable<LoginCharacterRosterEntry> entries,



            int slotCount,



            int buyCharacterCount)



        {



            List<LoginCharacterAccountStore.LoginCharacterAccountEntryState> storedEntries = entries?



                .Where(entry => entry != null)



                .Select(CreateLoginCharacterAccountEntryState)



                .ToList()



                ?? new List<LoginCharacterAccountStore.LoginCharacterAccountEntryState>();



            int nextCharacterId = CalculateNextLoginCharacterId(storedEntries);



            _loginCharacterAccountStore.SaveState(



                ResolveLoginRosterAccountName(),



                ResolveLoginRosterWorldId(),



                slotCount,



                buyCharacterCount,



                nextCharacterId,



                storedEntries,



                _loginAccountCashShopNxCredit,

                _loginAccountPicCode,

                _loginAccountBirthDate,



                _loginAccountSpwEnabled,



                _loginAccountSecondaryPassword);



        }



        private void SyncCashShopAccountCredit()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)

            {

                cashShopWindow.SetCashBalances(_loginAccountCashShopNxCredit);

                cashShopWindow.TryConsumeCashBalance = TryConsumeLoginAccountCashShopNxCredit;

            }

        }



        private bool TryConsumeLoginAccountCashShopNxCredit(long amount)

        {

            long normalizedAmount = Math.Max(0L, amount);

            if (normalizedAmount <= 0)

            {

                return true;

            }



            if (_loginAccountCashShopNxCredit < normalizedAmount)

            {

                return false;

            }



            _loginAccountCashShopNxCredit -= normalizedAmount;

            PersistLoginCharacterRosterToAccountStore(

                _loginCharacterRoster.Entries,

                _loginCharacterRoster.SlotCount,

                _loginCharacterRoster.BuyCharacterCount);

            SyncCashShopAccountCredit();

            return true;

        }



        private LoginCharacterAccountStore.LoginCharacterAccountEntryState CreateLoginCharacterAccountEntryState(LoginCharacterRosterEntry entry)



        {



            CharacterBuild build = entry?.Build;



            byte[] avatarLookPacket = entry?.AvatarLookPacket != null



                ? (byte[])entry.AvatarLookPacket.Clone()



                : (build != null ? LoginAvatarLookCodec.Encode(build) : Array.Empty<byte>());



            return new LoginCharacterAccountStore.LoginCharacterAccountEntryState



            {



                CharacterId = build?.Id ?? 0,



                Name = build?.Name ?? string.Empty,



                Gender = build?.Gender ?? CharacterGender.Male,



                Skin = build?.Skin ?? SkinColor.Light,



                Level = build?.Level ?? 1,



                Job = build?.Job ?? 0,



                SubJob = build?.SubJob ?? 0,



                JobName = build?.JobName ?? string.Empty,



                GuildName = build?.GuildName ?? string.Empty,



                AllianceName = build?.AllianceName ?? string.Empty,



                Fame = build?.Fame ?? 0,



                WorldRank = build?.WorldRank ?? 0,



                JobRank = build?.JobRank ?? 0,



                Exp = build?.Exp ?? 0,



                ExpToNextLevel = build?.ExpToNextLevel ?? 0,



                HP = build?.HP ?? 0,



                MaxHP = build?.MaxHP ?? 1,



                MP = build?.MP ?? 0,



                MaxMP = build?.MaxMP ?? 0,



                Strength = build?.STR ?? 0,



                Dexterity = build?.DEX ?? 0,



                Intelligence = build?.INT ?? 0,



                Luck = build?.LUK ?? 0,



                AbilityPoints = build?.AP ?? 0,



                FieldMapId = entry?.FieldMapId ?? 0,



                FieldDisplayName = entry?.FieldDisplayName ?? string.Empty,



                CanDelete = entry?.CanDelete ?? true,



                PreviousWorldRank = entry?.PreviousWorldRank,



                PreviousJobRank = entry?.PreviousJobRank,



                AvatarLookPacket = avatarLookPacket,



                Portal = entry?.Portal ?? 0



            };



        }







        private static int CalculateNextLoginCharacterId(IEnumerable<LoginCharacterAccountStore.LoginCharacterAccountEntryState> entries)



        {



            int maxCharacterId = 0;



            foreach (LoginCharacterAccountStore.LoginCharacterAccountEntryState entry in entries ?? Array.Empty<LoginCharacterAccountStore.LoginCharacterAccountEntryState>())



            {



                maxCharacterId = Math.Max(maxCharacterId, entry?.CharacterId ?? 0);



            }







            return maxCharacterId + 1;



        }







        private string BuildSuggestedLoginCharacterName()



        {



            int suffix = _loginCharacterRoster.Entries.Count + 1;



            while (_loginCharacterRoster.Entries.Any(entry =>



                       string.Equals(entry?.Build?.Name, $"Explorer{suffix}", StringComparison.OrdinalIgnoreCase)))



            {



                suffix++;



            }







            return $"Explorer{suffix}";



        }







        private CharacterBuild CreateAccountBackedLoginCharacterBuild(int characterId, string name, int rosterCount)



        {



            CharacterBuild build = null;



            int templateIndex = Math.Abs(rosterCount % 3);



            switch (templateIndex)



            {



                case 0:



                    build = _playerManager?.Loader?.LoadDefaultMale() ?? _playerManager?.Loader?.LoadRandom();



                    if (build != null)



                    {



                        ApplyLoginCharacterDetailDefaults(build, 15, "Henesys", 0, 14, 0, 0);



                    }



                    break;



                case 1:



                    build = _playerManager?.Loader?.LoadDefaultFemale() ?? _playerManager?.Loader?.LoadRandom();



                    if (build != null)



                    {



                        ApplyLoginCharacterDetailDefaults(build, 15, "Ellinia", 0, 18, 0, 0);



                    }



                    break;



                default:



                    build = _playerManager?.Loader?.LoadRandom()



                        ?? _playerManager?.Loader?.LoadDefaultMale()



                        ?? _playerManager?.Loader?.LoadDefaultFemale();



                    if (build != null)



                    {



                        ApplyLoginCharacterDetailDefaults(build, 15, "Lith Harbor", 0, 22, 0, 0);



                    }



                    break;



            }







            build ??= new CharacterBuild();



            build.Id = characterId;



            build.Name = name;



            build.ExpToNextLevel = Math.Max(100, build.ExpToNextLevel);



            return build;



        }







        private void SelectLoginCharacterById(int characterId)



        {



            if (characterId <= 0)



            {



                return;



            }







            for (int index = 0; index < _loginCharacterRoster.Entries.Count; index++)



            {



                if (_loginCharacterRoster.Entries[index]?.Build?.Id == characterId)



                {



                    _loginCharacterRoster.Select(index);



                    return;



                }



            }



        }







        private int ResolveLoginCharacterTargetMapId()

        {

            if (_mapBoard?.MapInfo?.returnMap is int returnMapId &&

                returnMapId > 0 &&

                returnMapId != MapConstants.MaxMap)

            {

                return returnMapId;

            }



            if (_mapBoard?.MapInfo?.forcedReturn is int forcedReturnId &&

                forcedReturnId > 0 &&

                forcedReturnId != MapConstants.MaxMap)

            {

                return forcedReturnId;

            }



            return DefaultLoginCharacterFieldMapId;

        }



        private void SyncLoginCharacterSelectWindow()

        {

            WireAvatarPreviewCarouselWindow();



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterSelect) is not CharacterSelectWindow characterSelectWindow)

            {

                SyncAvatarPreviewCarouselWindow();

                SyncLoginCharacterDetailWindow();

                return;

            }



            bool shouldShow = IsLoginRuntimeSceneActive &&

                              (_loginRuntime.CurrentStep == LoginStep.CharacterSelect ||

                               _loginRuntime.CurrentStep == LoginStep.ViewAllCharacters);

            if (!shouldShow)

            {

                characterSelectWindow.Hide();

                SyncAvatarPreviewCarouselWindow();

                SyncLoginCharacterDetailWindow();

                return;

            }



            bool canEnter = _loginCharacterRoster.CanRequestSelection(_loginRuntime, out string validationMessage);

            bool canDelete = _loginCharacterRoster.SelectedEntry?.CanDelete == true &&

                             (_loginRuntime.CurrentStep == LoginStep.CharacterSelect ||

                              _loginRuntime.CurrentStep == LoginStep.ViewAllCharacters);

            characterSelectWindow.SetRoster(

                _loginCharacterRoster.Entries,

                _loginCharacterRoster.SelectedIndex,

                string.IsNullOrWhiteSpace(_loginCharacterStatusMessage) ? validationMessage : _loginCharacterStatusMessage,

                _loginCharacterRoster.SlotCount,

                _loginCharacterRoster.BuyCharacterCount,

                _loginCharacterRoster.PageIndex,

                _loginCharacterRoster.PageCount,

                canEnter,

                canDelete);

            characterSelectWindow.Show();

            uiWindowManager.BringToFront(characterSelectWindow);

            SyncAvatarPreviewCarouselWindow();

            SyncLoginCharacterDetailWindow();

        }

        private void SyncLoginCreateCharacterWindow()

        {

            WireLoginCreateCharacterWindow();



            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginCreateCharacter) is not LoginCreateCharacterWindow createCharacterWindow)

            {

                return;

            }



            bool shouldShow = IsLoginRuntimeSceneActive &&

                              _loginCreateCharacterFlow != null &&

                              (_loginRuntime.CurrentStep == LoginStep.NewCharacter ||

                               _loginRuntime.CurrentStep == LoginStep.NewCharacterAvatar);

            if (!shouldShow)

            {

                createCharacterWindow.Hide();

                return;

            }



            CharacterBuild previewBuild = _loginCreateCharacterFlow.CreatePreviewBuild(_playerManager?.Loader);

            createCharacterWindow.Configure(_loginCreateCharacterFlow, previewBuild);

            createCharacterWindow.Show();

            uiWindowManager.BringToFront(createCharacterWindow);

        }



        private void SyncAvatarPreviewCarouselWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.AvatarPreviewCarousel) is not AvatarPreviewCarouselWindow previewWindow)

            {

                return;

            }



            bool shouldShow = IsLoginRuntimeSceneActive &&

                              (_loginRuntime.CurrentStep == LoginStep.CharacterSelect ||

                               _loginRuntime.CurrentStep == LoginStep.ViewAllCharacters);

            if (!shouldShow)

            {

                previewWindow.Hide();

                return;

            }



            previewWindow.SetRoster(

                _loginCharacterRoster.Entries,

                _loginCharacterRoster.SelectedIndex,

                _loginCharacterRoster.SlotCount,

                _loginCharacterRoster.BuyCharacterCount,

                _loginCharacterRoster.PageIndex);

            previewWindow.Show();

            uiWindowManager.BringToFront(previewWindow);

        }



        private void SyncLoginEntryDialogs()

        {

            if (uiWindowManager == null)

            {

                return;

            }



            WireLoginEntryDialogWindows();



            if (!IsLoginRuntimeSceneActive)

            {

                uiWindowManager.HideWindow(MapSimulatorWindowNames.ConnectionNotice);

                uiWindowManager.HideWindow(MapSimulatorWindowNames.LoginUtilityDialog);

                return;

            }



            SyncConnectionNoticeWindow();

            SyncLoginUtilityDialogWindow();

        }



        private void SyncConnectionNoticeWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ConnectionNotice) is not ConnectionNoticeWindow noticeWindow)

            {

                return;

            }



            if (_activeConnectionNoticeExpiresAt != int.MinValue &&

                unchecked(currTickCount - _activeConnectionNoticeExpiresAt) >= 0)

            {

                ClearActiveConnectionNotice();

            }



            bool shouldShow = false;

            string body = string.Empty;

            bool showProgress = false;

            float progress = 0f;

            ConnectionNoticeWindowVariant variant = ConnectionNoticeWindowVariant.Notice;

            string title = "Connection Notice";

            int? noticeTextIndex = null;



            if (_activeConnectionNoticeExpiresAt != int.MinValue)

            {

                shouldShow = true;

                title = _activeConnectionNoticeTitle;

                body = _activeConnectionNoticeBody;

                showProgress = _activeConnectionNoticeShowProgress;

                progress = _activeConnectionNoticeProgress;

                variant = _activeConnectionNoticeVariant;

                noticeTextIndex = _activeConnectionNoticeTextIndex;

            }

            else if (_selectorRequestKind != SelectorRequestKind.None)

            {

                shouldShow = true;

                body = _selectorRequestStatusMessage ?? "Waiting for the login bootstrap reply.";

                showProgress = true;

                variant = ConnectionNoticeWindowVariant.LoadingSingleGauge;

                if (_selectorRequestDurationMs > 0 && _selectorRequestStartedAt != int.MinValue)

                {

                    progress = MathHelper.Clamp(

                        (currTickCount - _selectorRequestStartedAt) / (float)_selectorRequestDurationMs,

                        0f,

                        1f);

                }

            }

            else if (_loginRuntime.CurrentStep == LoginStep.EnteringField)

            {

                shouldShow = true;

                body = string.IsNullOrWhiteSpace(_loginCharacterStatusMessage)

                    ? "Requesting field entry from the selected character."

                    : _loginCharacterStatusMessage;

            }

            else if (_loginRuntime.CurrentStep == LoginStep.Title &&

                     _loginRuntime.PendingStep == LoginStep.WorldSelect)

            {

                shouldShow = true;

                body = string.IsNullOrWhiteSpace(_loginTitleStatusMessage)

                    ? "Checking account credentials for the login bootstrap flow."

                    : _loginTitleStatusMessage;

                showProgress = true;

                variant = ConnectionNoticeWindowVariant.Loading;

                if (_loginRuntime.PendingStepDelayMs > 0 && _loginRuntime.StepChangeRequestedAt != int.MinValue)

                {

                    progress = MathHelper.Clamp(

                        (currTickCount - _loginRuntime.StepChangeRequestedAt) / (float)_loginRuntime.PendingStepDelayMs,

                        0f,

                        1f);

                }

            }



            if (!shouldShow)

            {

                noticeWindow.Hide();

                return;

            }



            noticeWindow.Configure(title, body, showProgress, progress, variant, noticeTextIndex);

            noticeWindow.Show();

            uiWindowManager.BringToFront(noticeWindow);

        }



        private void SyncLoginUtilityDialogWindow()

        {

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) is not LoginUtilityDialogWindow utilityDialogWindow)

            {

                return;

            }



            if (_loginUtilityDialogAction == LoginUtilityDialogAction.None)

            {

                utilityDialogWindow.Hide();

                return;

            }



            if (utilityDialogWindow.IsVisible)

            {

                _loginUtilityDialogInputValue = utilityDialogWindow.InputValue ?? string.Empty;

            }



            utilityDialogWindow.Configure(

                _loginUtilityDialogTitle,

                _loginUtilityDialogBody,

                _loginUtilityDialogPrimaryLabel,

                _loginUtilityDialogSecondaryLabel,

                _loginUtilityDialogButtonLayout,

                _loginUtilityDialogNoticeTextIndex,

                _loginUtilityDialogInputLabel,

                _loginUtilityDialogInputPlaceholder,

                _loginUtilityDialogInputMasked,

                _loginUtilityDialogInputMaxLength,

                _loginUtilityDialogInputValue,

                _loginUtilityDialogSoftKeyboardType);

            utilityDialogWindow.Show();

            uiWindowManager.BringToFront(utilityDialogWindow);

        }



        private void ShowLoginUtilityDialog(

            string title,

            string body,

            LoginUtilityDialogButtonLayout buttonLayout,

            LoginUtilityDialogAction action,

            int targetIndex = -1,

            int? noticeTextIndex = null,

            string primaryLabel = null,

            string secondaryLabel = null,

            string inputLabel = null,

            string inputPlaceholder = null,

            bool inputMasked = false,

            int inputMaxLength = 0,

            string inputValue = null,

            SoftKeyboardKeyboardType softKeyboardType = SoftKeyboardKeyboardType.AlphaNumeric)

        {

            _loginUtilityDialogTitle = string.IsNullOrWhiteSpace(title) ? "Login Utility" : title;

            _loginUtilityDialogBody = body ?? string.Empty;

            _loginUtilityDialogButtonLayout = buttonLayout;

            _loginUtilityDialogPrimaryLabel = primaryLabel ?? string.Empty;

            _loginUtilityDialogSecondaryLabel = secondaryLabel ?? string.Empty;

            _loginUtilityDialogAction = action;

            _loginUtilityDialogTargetIndex = targetIndex;

            _loginUtilityDialogNoticeTextIndex = noticeTextIndex;

            _loginUtilityDialogInputLabel = inputLabel ?? string.Empty;

            _loginUtilityDialogInputPlaceholder = inputPlaceholder ?? string.Empty;

            _loginUtilityDialogInputMasked = inputMasked;

            _loginUtilityDialogInputMaxLength = Math.Max(0, inputMaxLength);

            _loginUtilityDialogInputValue = inputValue ?? string.Empty;

            _loginUtilityDialogSoftKeyboardType = softKeyboardType;

            ClearActiveConnectionNotice();

            SyncLoginEntryDialogs();

        }



        private void HideLoginUtilityDialog()

        {

            _loginUtilityDialogAction = LoginUtilityDialogAction.None;

            _loginUtilityDialogBody = string.Empty;

            _loginUtilityDialogButtonLayout = LoginUtilityDialogButtonLayout.Ok;

            _loginUtilityDialogTargetIndex = -1;

            _loginUtilityDialogNoticeTextIndex = null;

            _loginUtilityDialogInputLabel = string.Empty;

            _loginUtilityDialogInputPlaceholder = string.Empty;

            _loginUtilityDialogInputMasked = false;

            _loginUtilityDialogInputMaxLength = 0;

            _loginUtilityDialogSoftKeyboardType = SoftKeyboardKeyboardType.AlphaNumeric;

            _loginUtilityDialogInputValue = string.Empty;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) is LoginUtilityDialogWindow utilityDialogWindow)

            {

                utilityDialogWindow.Hide();

            }

        }



        private void ShowConnectionNoticePrompt(LoginPacketDialogPromptConfiguration prompt)

        {

            if (prompt == null)

            {

                return;

            }



            HideLoginUtilityDialog();

            _activeConnectionNoticeTitle = string.IsNullOrWhiteSpace(prompt.Title) ? "Connection Notice" : prompt.Title;

            _activeConnectionNoticeBody = prompt.Body ?? string.Empty;

            _activeConnectionNoticeVariant = prompt.NoticeVariant ?? ConnectionNoticeWindowVariant.NoticeCog;

            _activeConnectionNoticeTextIndex = prompt.NoticeTextIndex;

            _activeConnectionNoticeShowProgress = _activeConnectionNoticeVariant is ConnectionNoticeWindowVariant.Loading or ConnectionNoticeWindowVariant.LoadingSingleGauge;

            _activeConnectionNoticeProgress = _activeConnectionNoticeShowProgress ? 1f : 0f;

            _activeConnectionNoticeExpiresAt = currTickCount + Math.Max(0, prompt.DurationMs);

            SyncLoginEntryDialogs();

        }



        private void ClearActiveConnectionNotice()

        {

            _activeConnectionNoticeTitle = "Connection Notice";

            _activeConnectionNoticeBody = string.Empty;

            _activeConnectionNoticeVariant = ConnectionNoticeWindowVariant.Notice;

            _activeConnectionNoticeTextIndex = null;

            _activeConnectionNoticeShowProgress = false;

            _activeConnectionNoticeProgress = 0f;

            _activeConnectionNoticeExpiresAt = int.MinValue;

        }



        private void HandleLoginCharacterSelected(int rowIndex)

        {

            if (!_loginCharacterRoster.Select(rowIndex))

            {

                return;

            }



            LoginCharacterRosterEntry entry = _loginCharacterRoster.SelectedEntry;

            _loginCharacterStatusMessage = entry == null

                ? "Select a character first."

                : $"Selected {entry.Build.Name} Lv.{entry.Build.Level} {entry.Build.JobName}.";

        }



        private void HandleLoginCharacterPageRequested(int pageIndex)

        {

            if (!_loginCharacterRoster.SelectPage(pageIndex))

            {

                return;

            }



            LoginCharacterRosterEntry entry = _loginCharacterRoster.SelectedEntry;

            _loginCharacterStatusMessage = entry == null

                ? $"Browsing character page {_loginCharacterRoster.PageIndex + 1}/{Math.Max(1, _loginCharacterRoster.PageCount)}."

                : $"Selected {entry.Build.Name} Lv.{entry.Build.Level} {entry.Build.JobName}.";

            SyncStorageAccessContext();

        }



        private void HandleLoginTitleSubmitted(LoginTitleSubmission submission)

        {

            if (!IsLoginRuntimeSceneActive || _loginRuntime.CurrentStep != LoginStep.Title || submission == null)

            {

                return;

            }



            string accountName = submission.AccountName?.Trim() ?? string.Empty;

            string password = submission.Password ?? string.Empty;

            _loginTitleRememberId = submission.RememberId;

            if (_loginTitleRememberId || !string.IsNullOrWhiteSpace(accountName))

            {

                _loginTitleAccountName = accountName;

            }



            _loginTitlePassword = password;



            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(password))

            {

                _loginTitleStatusMessage = "Enter both an account ID and a password before requesting CheckPasswordResult.";

                SyncLoginTitleWindow();

                return;

            }



            HideLoginUtilityDialog();

            QueueLoginAccountBootstrapPacket("LoginTitle.Login", accountName);

            SyncLoginTitleWindow();

        }



        private void HandleLoginTitleGuestLoginRequested()

        {

            if (!IsLoginRuntimeSceneActive || _loginRuntime.CurrentStep != LoginStep.Title)

            {

                return;

            }



            HideLoginUtilityDialog();

            _loginTitleStatusMessage = "Queued GuestIdLoginResult through the login packet inbox. Guest-only follow-up packets can still be injected from the loopback listener.";

            _loginPacketInbox.EnqueueLocal(LoginPacketType.GuestIdLoginResult, "LoginTitle.Guest");

            SyncLoginTitleWindow();

        }



        private void HandleLoginTitleNewAccountRequested()

        {

            if (!IsLoginRuntimeSceneActive)

            {

                return;

            }



            _loginTitleStatusMessage = "Queued SetAccountResult through the login packet inbox.";

            _loginPacketInbox.EnqueueLocal(LoginPacketType.SetAccountResult, "LoginTitle.NewAccount");

            SyncLoginTitleWindow();

        }



        private void HandleLoginTitleRecoverIdRequested()

        {

            if (!IsLoginRuntimeSceneActive)

            {

                return;

            }



            _loginTitleStatusMessage = "ID recovery now routes through the dedicated title owner.";

            ShowLoginUtilityDialog(

                "Login Utility",

                "ID recovery is surfaced from the title owner, but the simulator does not yet implement the external account recovery service.",

                LoginUtilityDialogButtonLayout.Ok,

                LoginUtilityDialogAction.DismissOnly);

            SyncLoginTitleWindow();

        }



        private void HandleLoginTitleRecoverPasswordRequested()

        {

            if (!IsLoginRuntimeSceneActive)

            {

                return;

            }



            _loginTitleStatusMessage = "Password recovery now routes through the dedicated title owner.";

            ShowLoginUtilityDialog(

                "Login Utility",

                "Password recovery is surfaced from the title owner, but the simulator does not yet implement the external account recovery service.",

                LoginUtilityDialogButtonLayout.Ok,

                LoginUtilityDialogAction.DismissOnly);

            SyncLoginTitleWindow();

        }



        private void HandleLoginTitleHomePageRequested()



        {



            if (!IsLoginRuntimeSceneActive)



            {



                return;



            }







            _loginTitleStatusMessage = "The title owner routed the homepage handoff through the login utility seam.";



            ShowLoginUtilityDialog(



                "Login Utility",



                "The login title requested the Nexon homepage handoff. Press the Nexon button to simulate the client website transfer.",



                LoginUtilityDialogButtonLayout.Nexon,



                LoginUtilityDialogAction.WebsiteHandoff);



            SyncLoginTitleWindow();



        }







        private void QueueLoginAccountBootstrapPacket(string source, string accountName = null)

        {

            if (!_loginAccountMigrationAccepted && string.IsNullOrWhiteSpace(accountName))

            {

                _loginTitleStatusMessage = "Queued SetAccountResult through the login packet inbox.";

                _loginPacketInbox.EnqueueLocal(LoginPacketType.SetAccountResult, source);

                return;

            }



            if (!_loginAccountAcceptedEula)

            {

                _loginTitleStatusMessage = "Queued ConfirmEulaResult through the login packet inbox.";

                _loginPacketInbox.EnqueueLocal(LoginPacketType.ConfirmEulaResult, source);

                return;

            }



            if (string.IsNullOrWhiteSpace(_loginAccountPicCode))

            {

                _loginTitleStatusMessage = "Queued UpdatePinCodeResult through the login packet inbox.";

                _loginPacketInbox.EnqueueLocal(LoginPacketType.UpdatePinCodeResult, source);

                return;

            }



            _loginTitleStatusMessage = $"Queued CheckPinCodeResult for {accountName ?? _loginTitleAccountName}.";

            _loginPacketInbox.EnqueueLocal(LoginPacketType.CheckPinCodeResult, source);

        }



        private void ContinueLoginAccountBootstrapAfterPic(string source)

        {

            if (_loginAccountSpwEnabled)

            {

                _loginTitleStatusMessage = "Queued CheckSpwResult through the login packet inbox.";

                _loginPacketInbox.EnqueueLocal(LoginPacketType.CheckSpwResult, source);

                return;

            }



            _loginTitleStatusMessage = "Queued EnableSpwResult through the login packet inbox.";

            _loginPacketInbox.EnqueueLocal(LoginPacketType.EnableSpwResult, source);

        }



        private void CompleteLoginAccountBootstrap(string source, string summary)

        {

            HideLoginUtilityDialog();

            _loginTitleStatusMessage = summary;

            _loginPacketInbox.EnqueueLocal(LoginPacketType.CheckPasswordResult, source);

            SyncLoginTitleWindow();

        }



        private bool TryGetLoginUtilityDialogInput(out string input)

        {

            input = _loginUtilityDialogInputValue ?? string.Empty;

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) is LoginUtilityDialogWindow utilityDialogWindow)

            {

                input = utilityDialogWindow.InputValue ?? input;

            }



            input = input.Trim();

            _loginUtilityDialogInputValue = input;

            return !string.IsNullOrWhiteSpace(input);

        }



        private void HandleLoginTitleQuitRequested()

        {

            Exit();

        }



        private void HandleLoginCharacterEnterRequested()

        {

            if (!_loginCharacterRoster.CanRequestSelection(_loginRuntime, out string validationMessage))

            {

                _loginCharacterStatusMessage = validationMessage;

                SyncLoginCharacterSelectWindow();

                ShowLoginUtilityDialog(

                    "Login Utility",

                    validationMessage,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return;

            }



            LoginCharacterRosterEntry entry = _loginCharacterRoster.SelectedEntry;

            if (entry?.Build == null)

            {

                _loginCharacterStatusMessage = "Selected character data is unavailable.";

                SyncLoginCharacterSelectWindow();

                ShowLoginUtilityDialog(

                    "Login Utility",

                    _loginCharacterStatusMessage,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return;

            }



            CharacterBuild selectedBuild = entry.CreateRuntimeBuild(_playerManager?.Loader);

            _playerManager?.CreatePlayerFromBuild(selectedBuild);

            RefreshSkillWindowForJob(selectedBuild.Job);



            if (_loadMapCallback == null || !QueueMapTransfer(entry.FieldMapId, null))

            {

                _loginCharacterStatusMessage = $"Selected {entry.Build.Name}, but map loading is unavailable.";

                SyncLoginCharacterSelectWindow();

                ShowLoginUtilityDialog(

                    "Login Utility",

                    _loginCharacterStatusMessage,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return;

            }



            HideLoginUtilityDialog();

            DispatchLoginRuntimePacket(LoginPacketType.SelectCharacterResult, out string runtimeMessage);

            _loginCharacterStatusMessage = $"Entering field with {entry.Build.Name}. {runtimeMessage}";

            SyncLoginEntryDialogs();

        }



        private void HandleLoginNewCharacterRequested()

        {

            if (!CanMutateAccountBackedLoginRoster(out string validationMessage))

            {

                _loginCharacterStatusMessage = validationMessage;

                SyncLoginCharacterSelectWindow();

                ShowLoginUtilityDialog(

                    "Login Utility",

                    validationMessage,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return;

            }



            int occupiedCount = _loginCharacterRoster.Entries.Count;

            int slotCount = Math.Max(_loginCharacterRoster.SlotCount, LoginCharacterRosterManager.EntriesPerPage);

            if (occupiedCount >= slotCount && _loginCharacterRoster.BuyCharacterCount <= 0)

            {

                _loginCharacterStatusMessage = "The account-backed roster has no remaining character slots.";

                SyncLoginCharacterSelectWindow();

                ShowLoginUtilityDialog(

                    "Login Utility",

                    _loginCharacterStatusMessage,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return;

            }



            _loginCreateCharacterFlow = new LoginCreateCharacterFlowState();

            _loginCreateCharacterFlow.Start();

            _loginCreateCharacterFlow.SetEnteredName(BuildSuggestedLoginCharacterName());

            _loginCreateCharacterFlow.RollRandom(_playerManager?.Loader);

            _loginRuntime.ForceStep(LoginStep.NewCharacter, "Opened the dedicated login create-character flow.");

            HideLoginUtilityDialog();

            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCharacterSelectWindow();

            SyncLoginCreateCharacterWindow();

        }


        private void HandleLoginCreateCharacterRaceSelected(int raceIndex)

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            _loginCreateCharacterFlow.SelectRace(raceIndex);

            _loginCreateCharacterFlow.ResetAvatarIndices();

            _loginCreateCharacterFlow.RollRandom(_playerManager?.Loader);

            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterJobSelected(int jobIndex)

        {

            _loginCreateCharacterFlow?.SelectJob(jobIndex);

            if (_loginCreateCharacterFlow != null)

            {

                _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            }

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterAvatarShiftRequested(LoginCreateCharacterAvatarPart part, int delta)

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            _loginCreateCharacterFlow.ShiftAvatarPart(

                _playerManager?.Loader?.GetLoginStarterAvatarCatalog(_loginCreateCharacterFlow.SelectedRace, _loginCreateCharacterFlow.SelectedGender),

                part,

                delta);

            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterGenderToggleRequested()

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            _loginCreateCharacterFlow.ToggleGender();

            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterDiceRequested()

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            _loginCreateCharacterFlow.RollRandom(_playerManager?.Loader);

            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterNameEditRequested()

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            _loginCreateCharacterFlow.SetStage(LoginCreateCharacterStage.NameSelect, "Type a name, then send CheckDuplicatedIdResult through the dedicated name owner.");

            _loginRuntime.ForceStep(LoginStep.NewCharacter, "Opened the dedicated create-character name owner.");

            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterNameChanged(string name)

        {

            _loginCreateCharacterFlow?.SetEnteredName(name);

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterConfirmRequested()

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            switch (_loginCreateCharacterFlow.Stage)

            {

                case LoginCreateCharacterStage.RaceSelect:

                    _loginCreateCharacterFlow.SetStage(LoginCreateCharacterStage.JobSelect, "Choose the starter job banner for the new character.");

                    _loginRuntime.ForceStep(LoginStep.NewCharacter, "Advanced the dedicated create-character flow to job selection.");

                    break;

                case LoginCreateCharacterStage.JobSelect:

                    _loginCreateCharacterFlow.SetStage(LoginCreateCharacterStage.AvatarSelect, "Adjust the starter avatar, check the name, then create the character.");

                    _loginRuntime.ForceStep(LoginStep.NewCharacterAvatar, "Advanced the dedicated create-character flow to avatar selection.");

                    break;

                case LoginCreateCharacterStage.AvatarSelect:

                    ExecuteLoginCharacterCreateConfirmation();

                    return;

            }



            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterCancelRequested()

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            switch (_loginCreateCharacterFlow.Stage)

            {

                case LoginCreateCharacterStage.NameSelect:

                    _loginCreateCharacterFlow.SetStage(LoginCreateCharacterStage.AvatarSelect, "Returned to the avatar owner after name entry.");

                    _loginRuntime.ForceStep(LoginStep.NewCharacterAvatar, "Closed the dedicated create-character name owner.");

                    break;

                case LoginCreateCharacterStage.AvatarSelect:

                    _loginCreateCharacterFlow.SetStage(LoginCreateCharacterStage.JobSelect, "Returned to the job-selection owner.");

                    _loginRuntime.ForceStep(LoginStep.NewCharacter, "Moved back from avatar selection to job selection.");

                    break;

                case LoginCreateCharacterStage.JobSelect:

                    _loginCreateCharacterFlow.SetStage(LoginCreateCharacterStage.RaceSelect, "Returned to the race-selection owner.");

                    _loginRuntime.ForceStep(LoginStep.NewCharacter, "Moved back from job selection to race selection.");

                    break;

                default:

                    CloseLoginCreateCharacterFlow("Cancelled the dedicated create-character flow and returned to character selection.");

                    return;

            }



            _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

            SyncLoginCreateCharacterWindow();

        }

        private void HandleLoginCreateCharacterDuplicateCheckRequested()

        {

            if (_loginCreateCharacterFlow == null)

            {

                return;

            }



            string characterName = _loginCreateCharacterFlow.EnteredName?.Trim();

            if (string.IsNullOrWhiteSpace(characterName))

            {

                _loginCreateCharacterFlow.ClearCheckedName("Enter a character name before checking duplication.");

                _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

                SyncLoginCreateCharacterWindow();

                return;

            }



            _loginPendingCreateCharacterName = characterName;

            if (!TryGetLoginCheckDuplicatedIdPacketProfile(out LoginAccountDialogPacketProfile duplicatedIdProfile) ||

                duplicatedIdProfile.Payload == null ||

                duplicatedIdProfile.Payload.Length == 0)

            {

                _loginPacketAccountDialogProfiles[LoginPacketType.CheckDuplicatedIdResult] =

                    BuildGeneratedCheckDuplicatedIdResultProfile(characterName);

            }



            DispatchLoginRuntimePacket(

                LoginPacketType.CheckDuplicatedIdResult,

                out string runtimeMessage,

                applySelectorSideEffects: false);

            if (string.IsNullOrWhiteSpace(_loginCharacterStatusMessage))

            {

                _loginCharacterStatusMessage = runtimeMessage;

            }

            SyncLoginCreateCharacterWindow();

        }

        private void CloseLoginCreateCharacterFlow(string statusMessage = null)

        {

            _loginCreateCharacterFlow = null;

            _loginPendingCreateCharacterName = null;

            _loginRuntime.ForceStep(LoginStep.CharacterSelect, statusMessage ?? "Returned to character selection.");

            if (!string.IsNullOrWhiteSpace(statusMessage))

            {

                _loginCharacterStatusMessage = statusMessage;

            }

            SyncLoginCharacterSelectWindow();

            SyncLoginCreateCharacterWindow();

        }



        private void HandleLoginCharacterDeleteRequested()

        {

            if (!CanMutateAccountBackedLoginRoster(out string validationMessage))



            {



                _loginCharacterStatusMessage = validationMessage;



                SyncLoginCharacterSelectWindow();



                ShowLoginUtilityDialog(



                    "Login Utility",



                    validationMessage,



                    LoginUtilityDialogButtonLayout.Ok,



                    LoginUtilityDialogAction.DismissOnly);



                return;



            }



            LoginCharacterRosterEntry selectedEntry = _loginCharacterRoster.SelectedEntry;

            if (selectedEntry == null || !selectedEntry.CanDelete)

            {

                _loginCharacterStatusMessage = "The selected character cannot be deleted.";

                SyncLoginCharacterSelectWindow();

                ShowLoginUtilityDialog(

                    "Login Utility",

                    _loginCharacterStatusMessage,

                    LoginUtilityDialogButtonLayout.Ok,

                    LoginUtilityDialogAction.DismissOnly);

                return;

            }



            ShowLoginUtilityDialog(

                "Login Utility",

                $"Delete {selectedEntry.Build.Name} from the account-backed roster?",

                LoginUtilityDialogButtonLayout.YesNo,

                LoginUtilityDialogAction.ConfirmDeleteCharacter,

                _loginCharacterRoster.SelectedIndex);

        }



        private void HandleLoginUtilityPrimaryRequested()

        {

            switch (_loginUtilityDialogAction)

            {

                case LoginUtilityDialogAction.ConfirmDeleteCharacter:

                    ExecuteLoginCharacterDeleteConfirmation();

                    break;

                case LoginUtilityDialogAction.CreateCharacter:



                    ExecuteLoginCharacterCreateConfirmation();



                    break;

                case LoginUtilityDialogAction.ConfirmApspEvent:

                    AcceptPacketOwnedAskApspEventPrompt();

                    break;

                case LoginUtilityDialogAction.ConfirmFollowCharacterRequest:

                    AcceptPacketOwnedFollowCharacterPrompt();

                    break;



                case LoginUtilityDialogAction.AccountMigrationDecision:

                    _loginAccountMigrationAccepted = true;

                    _loginTitleStatusMessage = "Accepted the simulator account-migration prompt and opened the fixed-length birth-date utility owner.";

                    ShowLoginUtilityDialog(

                        "Login Utility",

                        string.IsNullOrWhiteSpace(_loginAccountBirthDate)
                            ? "Enter the simulator account birth date before continuing to the EULA step. This follows the client CLoginUtilDlg path that binds an 8-digit masked edit control to soft-keyboard type 2."
                            : "Enter the configured simulator account birth date before continuing to the EULA step. This follows the client CLoginUtilDlg path that binds an 8-digit masked edit control to soft-keyboard type 2.",

                        LoginUtilityDialogButtonLayout.YesNo,

                        LoginUtilityDialogAction.VerifyBirthDate,

                        primaryLabel: "Verify",

                        secondaryLabel: "Cancel",

                        inputLabel: "Birth Date",

                        inputPlaceholder: "YYYYMMDD",

                        inputMasked: true,

                        inputMaxLength: 8,

                        softKeyboardType: SoftKeyboardKeyboardType.NumericOnly);

                    break;

                case LoginUtilityDialogAction.EulaDecision:

                    _loginAccountAcceptedEula = true;

                    _loginTitleStatusMessage = "Accepted the simulator EULA prompt.";

                    HideLoginUtilityDialog();

                    if (string.IsNullOrWhiteSpace(_loginAccountPicCode))

                    {

                        _loginPacketInbox.EnqueueLocal(LoginPacketType.UpdatePinCodeResult, "LoginUtility.Eula.Accept");

                    }

                    else

                    {

                        _loginPacketInbox.EnqueueLocal(LoginPacketType.CheckPinCodeResult, "LoginUtility.Eula.Accept");

                    }

                    break;

                case LoginUtilityDialogAction.VerifyBirthDate:

                    if (!TryGetLoginUtilityDialogInput(out string birthDateInput) || !IsEightDigitNumericInput(birthDateInput))

                    {

                        _loginCharacterStatusMessage = "Enter the 8-digit birth date before continuing.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        break;

                    }



                    if (!string.IsNullOrWhiteSpace(_loginAccountBirthDate) &&

                        !string.Equals(birthDateInput, _loginAccountBirthDate, StringComparison.Ordinal))

                    {

                        _loginCharacterStatusMessage = "Birth-date verification failed. Enter the configured 8-digit birth date.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        _loginUtilityDialogInputValue = string.Empty;

                        break;

                    }



                    _loginAccountBirthDate = birthDateInput;

                    PersistLoginAccountSecurityState();

                    HideLoginUtilityDialog();

                    _loginTitleStatusMessage = "Birth-date verification succeeded. Queued ConfirmEulaResult.";

                    _loginPacketInbox.EnqueueLocal(LoginPacketType.ConfirmEulaResult, "LoginUtility.SetAccount.BirthDate");

                    break;

                case LoginUtilityDialogAction.VerifyPic:

                    if (!TryGetLoginUtilityDialogInput(out string picInput))

                    {

                        _loginCharacterStatusMessage = "Enter the PIC before continuing.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        break;

                    }



                    if (!string.Equals(picInput, _loginAccountPicCode, StringComparison.Ordinal))

                    {

                        _loginCharacterStatusMessage = "PIC verification failed. Enter the configured PIC to continue.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        _loginUtilityDialogInputValue = string.Empty;

                        break;

                    }



                    HideLoginUtilityDialog();

                    _loginCharacterStatusMessage = "PIC verification succeeded.";

                    ContinueLoginAccountBootstrapAfterPic("LoginUtility.Pic.Verify");

                    break;

                case LoginUtilityDialogAction.SetPic:

                    if (!TryGetLoginUtilityDialogInput(out string newPic) || newPic.Length < 4)

                    {

                        _loginCharacterStatusMessage = "Enter a PIC with at least four characters.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        break;

                    }



                    _loginAccountPicCode = newPic;

                    PersistLoginAccountSecurityState();

                    HideLoginUtilityDialog();

                    _loginCharacterStatusMessage = "Saved the simulator PIC.";

                    ContinueLoginAccountBootstrapAfterPic("LoginUtility.Pic.Setup");

                    break;

                case LoginUtilityDialogAction.SecondaryPasswordDecision:

                    ShowLoginUtilityDialog(

                        "Login Utility",

                        "Create a secondary password before continuing to WorldSelect.",

                        LoginUtilityDialogButtonLayout.Ok,

                        LoginUtilityDialogAction.SetSpw,

                        primaryLabel: "Save",

                        inputLabel: "Secondary Password",

                        inputPlaceholder: "Enter secondary password",

                        inputMasked: true,

                        inputMaxLength: 16,

                        softKeyboardType: SoftKeyboardKeyboardType.AlphaNumeric);

                    _loginTitleStatusMessage = "Opened secondary-password setup.";

                    break;

                case LoginUtilityDialogAction.WebsiteHandoffDecision:



                    _loginTitleStatusMessage = "Accepted the simulator website handoff prompt.";



                    ShowLoginUtilityDialog(



                        "Login Utility",



                        "The login flow requested a website handoff. Press the Nexon button to simulate the external browser transfer while staying inside the existing notice seam.",



                        LoginUtilityDialogButtonLayout.Nexon,



                        LoginUtilityDialogAction.WebsiteHandoff);



                    break;



                case LoginUtilityDialogAction.WebsiteHandoff:



                    HideLoginUtilityDialog();



                    _loginTitleStatusMessage = "Simulated the client website handoff without opening an authenticated browser session.";



                    ShowConnectionNoticePrompt(new LoginPacketDialogPromptConfiguration



                    {



                        Owner = LoginPacketDialogOwner.ConnectionNotice,



                        Title = "Connection Notice",



                        Body = "Website handoff is still simulator-local. No authenticated external session was opened.",



                        NoticeVariant = ConnectionNoticeWindowVariant.NoticeCog,



                        DurationMs = 2200,



                    });



                    break;



                case LoginUtilityDialogAction.SetSpw:

                    if (!TryGetLoginUtilityDialogInput(out string newSpw) || newSpw.Length < 4)

                    {

                        _loginCharacterStatusMessage = "Enter a secondary password with at least four characters.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        break;

                    }



                    _loginAccountSpwEnabled = true;

                    _loginAccountSecondaryPassword = newSpw;

                    PersistLoginAccountSecurityState();

                    CompleteLoginAccountBootstrap(

                        "LoginUtility.Spw.Setup",

                        "Saved the simulator secondary password and queued CheckPasswordResult.");

                    break;

                case LoginUtilityDialogAction.VerifySpw:

                    if (!TryGetLoginUtilityDialogInput(out string spwInput))

                    {

                        _loginCharacterStatusMessage = "Enter the secondary password before continuing.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        break;

                    }



                    if (!string.Equals(spwInput, _loginAccountSecondaryPassword, StringComparison.Ordinal))

                    {

                        _loginCharacterStatusMessage = "Secondary password verification failed.";

                        _loginTitleStatusMessage = _loginCharacterStatusMessage;

                        _loginUtilityDialogInputValue = string.Empty;

                        break;

                    }



                    CompleteLoginAccountBootstrap(

                        "LoginUtility.Spw.Verify",

                        "Verified the simulator secondary password and queued CheckPasswordResult.");

                    break;

                default:

                    HideLoginUtilityDialog();

                    break;

            }



            _loginTitleStatusMessage = string.IsNullOrWhiteSpace(_loginTitleStatusMessage)

                ? _loginCharacterStatusMessage

                : _loginTitleStatusMessage;

            SyncLoginCharacterSelectWindow();

            SyncLoginTitleWindow();

            SyncLoginEntryDialogs();

        }



        private void HandleLoginUtilitySecondaryRequested()

        {

            switch (_loginUtilityDialogAction)

            {

                case LoginUtilityDialogAction.AccountMigrationDecision:

                    _loginAccountMigrationAccepted = false;

                    _loginTitleStatusMessage = "Deferred the simulator account-migration flow.";

                    break;

                case LoginUtilityDialogAction.VerifyBirthDate:

                    _loginAccountMigrationAccepted = false;

                    _loginTitleStatusMessage = "Cancelled the simulator birth-date verification step.";

                    break;

                case LoginUtilityDialogAction.EulaDecision:

                    _loginAccountAcceptedEula = false;

                    _loginTitleStatusMessage = "EULA acceptance was declined.";

                    break;

                case LoginUtilityDialogAction.SecondaryPasswordDecision:

                    _loginAccountSpwEnabled = false;
                    _loginAccountSecondaryPassword = string.Empty;
                    PersistLoginAccountSecurityState();

                    CompleteLoginAccountBootstrap(

                        "LoginUtility.Spw.Later",

                        "Skipped secondary-password setup and queued CheckPasswordResult.");

                    return;

                case LoginUtilityDialogAction.ConfirmApspEvent:

                    DeclinePacketOwnedAskApspEventPrompt();

                    break;

                case LoginUtilityDialogAction.ConfirmFollowCharacterRequest:

                    DeclinePacketOwnedFollowCharacterPrompt();

                    break;

            }



            HideLoginUtilityDialog();

            SyncLoginTitleWindow();

            SyncLoginEntryDialogs();

        }



        private void ApplyLoginPacketDialogPrompt(LoginPacketType packetType)

        {

            if (!IsLoginRuntimeSceneActive)

            {

                return;

            }



            LoginPacketDialogPromptConfiguration prompt = ResolveLoginPacketDialogPrompt(packetType);

            if (prompt == null)

            {

                return;

            }



            _loginPacketAccountDialogProfiles.TryGetValue(packetType, out LoginAccountDialogPacketProfile packetProfile);



            if (_loginCreateCharacterFlow != null &&
                (packetType == LoginPacketType.CheckDuplicatedIdResult || packetType == LoginPacketType.CreateNewCharacterResult))
            {
                return;
            }

            _loginCharacterStatusMessage = BuildLoginPacketDialogStatusMessage(packetType, prompt, packetProfile);

            if (prompt.Owner == LoginPacketDialogOwner.ConnectionNotice)

            {

                ShowConnectionNoticePrompt(prompt);

                return;

            }



            ShowLoginUtilityDialog(

                prompt.Title ?? "Login Utility",

                prompt.Body ?? string.Empty,

                prompt.ButtonLayout ?? LoginUtilityDialogButtonLayout.Ok,

                ResolveLoginUtilityDialogAction(packetType, prompt),

                noticeTextIndex: prompt.NoticeTextIndex,

                primaryLabel: prompt.PrimaryLabel,

                secondaryLabel: prompt.SecondaryLabel,

                inputLabel: prompt.InputLabel,

                inputPlaceholder: prompt.InputPlaceholder,

                inputMasked: prompt.InputMasked,

                inputMaxLength: prompt.InputMaxLength,

                softKeyboardType: prompt.SoftKeyboardType);

        }



        private LoginUtilityDialogAction ResolveLoginUtilityDialogAction(

            LoginPacketType packetType,

            LoginPacketDialogPromptConfiguration prompt)

        {
            if (prompt?.Action.HasValue == true)
            {
                return prompt.Action.Value;
            }

            if (prompt?.Owner == LoginPacketDialogOwner.ConnectionNotice)

            {

                return LoginUtilityDialogAction.DismissOnly;

            }



            return packetType switch

            {

                LoginPacketType.SetAccountResult => LoginUtilityDialogAction.AccountMigrationDecision,

                LoginPacketType.ConfirmEulaResult => LoginUtilityDialogAction.EulaDecision,

                LoginPacketType.CheckPinCodeResult => LoginUtilityDialogAction.VerifyPic,

                LoginPacketType.UpdatePinCodeResult => LoginUtilityDialogAction.SetPic,

                LoginPacketType.EnableSpwResult => LoginUtilityDialogAction.SecondaryPasswordDecision,

                LoginPacketType.CheckSpwResult => LoginUtilityDialogAction.VerifySpw,

                _ => LoginUtilityDialogAction.DismissOnly,

            };

        }



        private LoginPacketDialogPromptConfiguration ResolveLoginPacketDialogPrompt(LoginPacketType packetType)

        {
            _loginPacketAccountDialogProfiles.TryGetValue(packetType, out LoginAccountDialogPacketProfile packetProfile);

            if (ShouldSuppressLoginPacketDialogPrompt(packetType, packetProfile))
            {
                return null;
            }

            _loginPacketDialogPrompts.TryGetValue(packetType, out LoginPacketDialogPromptConfiguration overridePrompt);
            LoginPacketDialogPromptConfiguration basePrompt = BuildDefaultLoginPacketDialogPrompt(packetType, packetProfile);

            return MergeLoginPacketDialogPrompt(basePrompt, overridePrompt);

        }

        private bool ShouldSuppressLoginPacketDialogPrompt(
            LoginPacketType packetType,
            LoginAccountDialogPacketProfile packetProfile)
        {
            if (packetType == LoginPacketType.CreateNewCharacterResult &&
                _loginPacketCreateNewCharacterResultProfile?.IsSuccess == true)
            {
                return true;
            }

            if (packetType == LoginPacketType.DeleteCharacterResult &&
                packetProfile?.ResultCode.GetValueOrDefault(byte.MaxValue) == 0)
            {
                return true;
            }

            return packetType switch
            {
                LoginPacketType.SetAccountResult => IsSuccessfulSetAccountResult(packetProfile),
                LoginPacketType.ConfirmEulaResult => IsSuccessfulConfirmEulaResult(packetProfile),
                LoginPacketType.CheckPinCodeResult => IsSuccessfulCheckPinCodeResult(packetProfile),
                LoginPacketType.CheckDuplicatedIdResult => packetProfile?.ResultCode == 0,
                _ => false,
            };
        }



        private static string BuildLoginPacketDialogStatusMessage(

            LoginPacketType packetType,

            LoginPacketDialogPromptConfiguration prompt,

            LoginAccountDialogPacketProfile packetProfile)

        {

            if (!string.IsNullOrWhiteSpace(prompt?.Body))

            {

                return prompt.Body.Replace("\r\n", " ").Trim();

            }



            if (!string.IsNullOrWhiteSpace(packetProfile?.TextValue))

            {

                return packetProfile.TextValue.Replace("\r\n", " ").Trim();

            }



            return packetType switch

            {

                LoginPacketType.AccountInfoResult => "Received AccountInfoResult for the simulator login account.",

                LoginPacketType.SetAccountResult => "The login flow requested account selection or migration.",

                LoginPacketType.ConfirmEulaResult => "The login flow requested EULA confirmation.",

                LoginPacketType.CheckPinCodeResult => "The login flow requested PIC verification.",

                LoginPacketType.UpdatePinCodeResult => "The login flow requested PIC setup.",

                LoginPacketType.CheckDuplicatedIdResult => "The login flow reported character-name validation results.",

                LoginPacketType.EnableSpwResult => "The login flow requested secondary-password setup.",

                LoginPacketType.CheckSpwResult => "The login flow requested secondary-password verification.",

                LoginPacketType.CreateNewCharacterResult => "The login flow reported new-character creation results.",

                LoginPacketType.DeleteCharacterResult => "The login flow reported character deletion results.",

                _ => $"The login flow routed {packetType} through the dialog layer.",

            };

        }



        private static LoginPacketDialogPromptConfiguration BuildDefaultLoginPacketDialogPrompt(

            LoginPacketType packetType,

            LoginAccountDialogPacketProfile packetProfile)

        {

            string packetText = string.IsNullOrWhiteSpace(packetProfile?.TextValue)

                ? null

                : packetProfile.TextValue.Trim();

            string packetDetail = BuildLoginAccountDialogPacketSummary(packetProfile);
            LoginPacketDialogPromptConfiguration clientPrompt = BuildClientAccountDialogResultPrompt(packetType, packetProfile, packetText, packetDetail);
            if (clientPrompt != null)
            {
                return clientPrompt;
            }



            return packetType switch

            {

                LoginPacketType.AccountInfoResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "Account information was received for the login session.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,

                },

                LoginPacketType.SetAccountResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "Select how the simulator account should continue before entering the login bootstrap flow.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.YesNo,

                    PrimaryLabel = "Migrate",

                    SecondaryLabel = "Later",

                },

                LoginPacketType.ConfirmEulaResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "Review and accept the simulator EULA before continuing to world selection.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Accept,

                    PrimaryLabel = "Accept",

                },

                LoginPacketType.CheckPinCodeResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "Enter the configured PIC to continue the login bootstrap flow.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,

                    PrimaryLabel = "Verify",

                    InputLabel = "PIC",

                    InputPlaceholder = "Enter PIC",

                    InputMasked = true,

                    InputMaxLength = 16,

                },

                LoginPacketType.UpdatePinCodeResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "Create a PIC for the simulator account before continuing.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,

                    PrimaryLabel = "Save",

                    InputLabel = "New PIC",

                    InputPlaceholder = "At least 4 characters",

                    InputMasked = true,

                    InputMaxLength = 16,

                },

                LoginPacketType.CheckDuplicatedIdResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "CheckDuplicatedIdResult now routes through the login utility dialog before the create-result seam continues.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,

                },

                LoginPacketType.EnableSpwResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "Set up a secondary password now, or continue without one for this simulator account.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.NowLater,

                    PrimaryLabel = "Now",

                    SecondaryLabel = "Later",

                },

                LoginPacketType.CheckSpwResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "Enter the configured secondary password before continuing to world selection.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,

                    PrimaryLabel = "Verify",

                    InputLabel = "Secondary Password",

                    InputPlaceholder = "Enter secondary password",

                    InputMasked = true,

                    InputMaxLength = 16,

                },

                LoginPacketType.CreateNewCharacterResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "CreateNewCharacterResult now routes into the login utility dialog, and account-backed roster creation can persist locally when packet-authored rosters are not active.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,

                },

                LoginPacketType.DeleteCharacterResult => new LoginPacketDialogPromptConfiguration

                {

                    Title = "Login Utility",

                    Body = CombineLoginDialogBody(packetText, "DeleteCharacterResult now routes into the login utility dialog, and account-backed roster deletion can persist locally when packet-authored rosters are not active.", packetDetail),

                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,

                },

                _ => null,

            };

        }



        private static LoginPacketDialogPromptConfiguration MergeLoginPacketDialogPrompt(

            LoginPacketDialogPromptConfiguration basePrompt,

            LoginPacketDialogPromptConfiguration overridePrompt)

        {

            if (overridePrompt == null)

            {

                return basePrompt;

            }



            if (basePrompt == null)

            {

                return overridePrompt;

            }



            return new LoginPacketDialogPromptConfiguration

            {

                Owner = overridePrompt.Owner,

                Title = string.IsNullOrWhiteSpace(overridePrompt.Title) ? basePrompt.Title : overridePrompt.Title,

                Body = string.IsNullOrWhiteSpace(overridePrompt.Body) ? basePrompt.Body : overridePrompt.Body,

                NoticeTextIndex = overridePrompt.NoticeTextIndex ?? basePrompt.NoticeTextIndex,

                NoticeVariant = overridePrompt.NoticeVariant ?? basePrompt.NoticeVariant,

                ButtonLayout = overridePrompt.ButtonLayout ?? basePrompt.ButtonLayout,
                Action = overridePrompt.Action ?? basePrompt.Action,

                PrimaryLabel = string.IsNullOrWhiteSpace(overridePrompt.PrimaryLabel) ? basePrompt.PrimaryLabel : overridePrompt.PrimaryLabel,

                SecondaryLabel = string.IsNullOrWhiteSpace(overridePrompt.SecondaryLabel) ? basePrompt.SecondaryLabel : overridePrompt.SecondaryLabel,

                InputLabel = string.IsNullOrWhiteSpace(overridePrompt.InputLabel) ? basePrompt.InputLabel : overridePrompt.InputLabel,

                InputPlaceholder = string.IsNullOrWhiteSpace(overridePrompt.InputPlaceholder) ? basePrompt.InputPlaceholder : overridePrompt.InputPlaceholder,

                InputMasked = overridePrompt.InputMasked || basePrompt.InputMasked,

                InputMaxLength = overridePrompt.InputMaxLength > 0 ? overridePrompt.InputMaxLength : basePrompt.InputMaxLength,

                SoftKeyboardType = overridePrompt.SoftKeyboardType != SoftKeyboardKeyboardType.AlphaNumeric || basePrompt.SoftKeyboardType == SoftKeyboardKeyboardType.AlphaNumeric
                    ? overridePrompt.SoftKeyboardType
                    : basePrompt.SoftKeyboardType,

                DurationMs = overridePrompt.DurationMs > 0 ? overridePrompt.DurationMs : basePrompt.DurationMs,

            };

        }



        private static bool IsEightDigitNumericInput(string value)

        {

            return !string.IsNullOrWhiteSpace(value) &&

                   value.Length == 8 &&

                   value.All(char.IsDigit);

        }



        private static string CombineLoginDialogBody(string packetText, string defaultBody, string packetDetail)

        {

            if (!string.IsNullOrWhiteSpace(packetText))

            {

                return string.IsNullOrWhiteSpace(packetDetail)

                    ? packetText

                    : packetText + "\r\n\r\n" + packetDetail;

            }



            return string.IsNullOrWhiteSpace(packetDetail)

                ? defaultBody

                : defaultBody + "\r\n\r\n" + packetDetail;

        }



        private static string BuildLoginAccountDialogPacketSummary(LoginAccountDialogPacketProfile profile)

        {
            return LoginAccountDialogPacketProfileFormatter.BuildDetailBlock(profile);
        }

        private static LoginPacketDialogPromptConfiguration BuildClientAccountDialogResultPrompt(
            LoginPacketType packetType,
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            if (packetProfile == null)
            {
                return null;
            }

            return packetType switch
            {
                LoginPacketType.SetAccountResult => BuildSetAccountResultPrompt(packetProfile, packetText, packetDetail),
                LoginPacketType.ConfirmEulaResult => BuildConfirmEulaResultPrompt(packetProfile, packetText, packetDetail),
                LoginPacketType.CheckPinCodeResult => BuildCheckPinCodeResultPrompt(packetProfile, packetText, packetDetail),
                LoginPacketType.CheckDuplicatedIdResult => BuildCheckDuplicatedIdResultPrompt(packetProfile, packetText, packetDetail),
                LoginPacketType.UpdatePinCodeResult => BuildUpdatePinCodeResultPrompt(packetProfile, packetText, packetDetail),
                LoginPacketType.EnableSpwResult => BuildEnableSpwResultPrompt(packetProfile, packetText, packetDetail),
                LoginPacketType.CheckSpwResult => BuildCheckSpwResultPrompt(packetProfile, packetText, packetDetail),
                _ => null,
            };
        }

        private static LoginPacketDialogPromptConfiguration BuildSetAccountResultPrompt(
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            if (packetProfile == null || IsSuccessfulSetAccountResult(packetProfile))
            {
                return null;
            }

            return BuildClientNoticePrompt(packetText, "Account migration could not continue.", packetDetail, 15);
        }

        private static LoginPacketDialogPromptConfiguration BuildConfirmEulaResultPrompt(
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            if (packetProfile == null || IsSuccessfulConfirmEulaResult(packetProfile))
            {
                return null;
            }

            return BuildClientNoticePrompt(packetText, "EULA confirmation could not continue.", packetDetail, 15);
        }

        private static LoginPacketDialogPromptConfiguration BuildCheckPinCodeResultPrompt(
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            return packetProfile.ResultCode switch
            {
                1 => new LoginPacketDialogPromptConfiguration
                {
                    Title = "Login Utility",
                    Body = CombineLoginDialogBody(packetText, "Create a PIC for the simulator account before continuing.", packetDetail),
                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,
                    Action = LoginUtilityDialogAction.SetPic,
                    PrimaryLabel = "Save",
                    InputLabel = "New PIC",
                    InputPlaceholder = "At least 4 characters",
                    InputMasked = true,
                    InputMaxLength = ClientPicEditMaxLength,
                    SoftKeyboardType = SoftKeyboardKeyboardType.NumericOnlyAlt,
                },
                2 or 4 => new LoginPacketDialogPromptConfiguration
                {
                    Title = "Login Utility",
                    Body = CombineLoginDialogBody(packetText, "Enter the configured PIC to continue the login bootstrap flow.", packetDetail),
                    ButtonLayout = LoginUtilityDialogButtonLayout.Ok,
                    Action = LoginUtilityDialogAction.VerifyPic,
                    PrimaryLabel = "Verify",
                    InputLabel = "PIC",
                    InputPlaceholder = "Enter PIC",
                    InputMasked = true,
                    InputMaxLength = ClientPicEditMaxLength,
                    SoftKeyboardType = SoftKeyboardKeyboardType.NumericOnlyAlt,
                },
                3 => BuildClientNoticePrompt(packetText, "PIC verification failed.", packetDetail, 15),
                7 => BuildClientNoticePrompt(packetText, "PIC verification returned the client to the title step.", packetDetail, 17),
                _ => null,
            };
        }

        private static LoginPacketDialogPromptConfiguration BuildUpdatePinCodeResultPrompt(
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            if (!packetProfile.ResultCode.HasValue)
            {
                return null;
            }

            return packetProfile.ResultCode.Value == 0
                ? BuildClientNoticePrompt(packetText, "PIC setup completed.", packetDetail, 8)
                : BuildClientNoticePrompt(packetText, "PIC setup failed.", packetDetail, 15);
        }

        private static LoginPacketDialogPromptConfiguration BuildCheckDuplicatedIdResultPrompt(
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            return packetProfile?.ResultCode switch
            {
                0 => null,
                1 => BuildClientNoticePrompt(packetText, "That character name is already in use.", packetDetail, 5),
                2 => BuildClientNoticePrompt(packetText, "That character name is not available.", packetDetail, 10),
                byte => BuildClientNoticePrompt(packetText, "Character-name verification failed.", packetDetail, 18),
                _ => null,
            };
        }

        private static LoginPacketDialogPromptConfiguration BuildEnableSpwResultPrompt(
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            return packetProfile.ResultCode switch
            {
                0 => BuildClientNoticePrompt(
                    packetText,
                    packetProfile.SecondaryCode.GetValueOrDefault() == 0
                        ? "Secondary password setup was disabled for the account."
                        : "Secondary password setup was enabled for the account.",
                    packetDetail,
                    packetProfile.SecondaryCode.GetValueOrDefault() == 0 ? 40 : 39),
                6 or 9 => BuildClientNoticePrompt(packetText, "Secondary password setup could not continue.", packetDetail, 18),
                20 => BuildClientNoticePrompt(packetText, "Secondary password setup hit the client security warning path.", packetDetail, 93),
                22 => BuildClientNoticePrompt(packetText, "Secondary password setup returned the client warning 91.", packetDetail, 91),
                23 => BuildClientNoticePrompt(packetText, "Secondary password setup returned the client warning 92.", packetDetail, 92),
                _ => null,
            };
        }

        private static LoginPacketDialogPromptConfiguration BuildCheckSpwResultPrompt(
            LoginAccountDialogPacketProfile packetProfile,
            string packetText,
            string packetDetail)
        {
            return packetProfile.ResultCode.HasValue || packetProfile.SecondaryCode.HasValue || !string.IsNullOrWhiteSpace(packetText)
                ? BuildClientNoticePrompt(packetText, "Secondary password verification failed.", packetDetail, 93)
                : null;
        }

        private static LoginPacketDialogPromptConfiguration BuildClientNoticePrompt(
            string packetText,
            string defaultBody,
            string packetDetail,
            int noticeTextIndex)
        {
            return new LoginPacketDialogPromptConfiguration
            {
                Title = "Login Utility",
                Body = CombineLoginDialogBody(packetText, defaultBody, packetDetail),
                NoticeTextIndex = noticeTextIndex,
                ButtonLayout = LoginUtilityDialogButtonLayout.Ok,
                Action = LoginUtilityDialogAction.DismissOnly,
            };
        }

        private static bool IsSuccessfulSetAccountResult(LoginAccountDialogPacketProfile packetProfile)
        {
            return packetProfile?.SecondaryCode.HasValue == true && packetProfile.SecondaryCode.Value != 0;
        }

        private static bool IsSuccessfulAccountInfoResult(LoginAccountDialogPacketProfile packetProfile)
        {
            return packetProfile?.ResultCode is 0 or 12 or 23 && packetProfile.AccountId.HasValue;
        }

        private static bool IsSuccessfulConfirmEulaResult(LoginAccountDialogPacketProfile packetProfile)
        {
            return packetProfile?.ResultCode.HasValue == true && packetProfile.ResultCode.Value != 0;
        }

        private static bool IsSuccessfulCheckPinCodeResult(LoginAccountDialogPacketProfile packetProfile)
        {
            return packetProfile?.ResultCode == 0;
        }

        private static bool IsSuccessfulEnableSpwResult(LoginAccountDialogPacketProfile packetProfile)
        {
            return packetProfile?.ResultCode == 0;
        }

        private void TryContinueLoginBootstrapFromPacketProfile(LoginPacketType packetType)
        {
            if (!IsLoginRuntimeSceneActive ||
                !_loginPacketAccountDialogProfiles.TryGetValue(packetType, out LoginAccountDialogPacketProfile packetProfile) ||
                packetProfile == null)
            {
                return;
            }

            switch (packetType)
            {
                case LoginPacketType.SetAccountResult:
                    if (!IsSuccessfulSetAccountResult(packetProfile))
                    {
                        return;
                    }

                    _loginAccountMigrationAccepted = true;
                    HideLoginUtilityDialog();
                    _loginTitleStatusMessage = "Packet-authored SetAccountResult accepted the account path and queued ConfirmEulaResult.";
                    _loginPacketInbox.EnqueueLocal(LoginPacketType.ConfirmEulaResult, "LoginPacket.SetAccount.Success");
                    break;

                case LoginPacketType.ConfirmEulaResult:
                    if (!IsSuccessfulConfirmEulaResult(packetProfile))
                    {
                        return;
                    }

                    _loginAccountAcceptedEula = true;
                    HideLoginUtilityDialog();
                    if (string.IsNullOrWhiteSpace(_loginAccountPicCode))
                    {
                        _loginTitleStatusMessage = "Packet-authored EULA acceptance queued UpdatePinCodeResult.";
                        _loginPacketInbox.EnqueueLocal(LoginPacketType.UpdatePinCodeResult, "LoginPacket.ConfirmEula.Success");
                    }
                    else
                    {
                        _loginTitleStatusMessage = "Packet-authored EULA acceptance queued CheckPinCodeResult.";
                        _loginPacketInbox.EnqueueLocal(LoginPacketType.CheckPinCodeResult, "LoginPacket.ConfirmEula.Success");
                    }

                    break;

                case LoginPacketType.CheckPinCodeResult:
                    if (!IsSuccessfulCheckPinCodeResult(packetProfile))
                    {
                        if (packetProfile.ResultCode == 7)
                        {
                            _loginRuntime.ForceStep(LoginStep.Title, "CheckPinCodeResult returned the login flow to title");
                        }

                        return;
                    }

                    HideLoginUtilityDialog();
                    _loginCharacterStatusMessage = "Packet-authored PIC verification succeeded.";
                    ContinueLoginAccountBootstrapAfterPic("LoginPacket.CheckPin.Success");
                    break;
                case LoginPacketType.EnableSpwResult:
                    _loginUtilityDialogInputValue = string.Empty;
                    if (!IsSuccessfulEnableSpwResult(packetProfile))
                    {
                        return;
                    }

                    _loginAccountSpwEnabled = packetProfile.SecondaryCode.GetValueOrDefault() != 0;
                    if (!_loginAccountSpwEnabled)
                    {
                        _loginAccountSecondaryPassword = string.Empty;
                    }

                    _loginTitleStatusMessage = _loginAccountSpwEnabled
                        ? "Packet-authored EnableSpwResult enabled the secondary-password login option and surfaced the client notice path."
                        : "Packet-authored EnableSpwResult disabled the secondary-password login option and surfaced the client notice path.";
                    break;
                case LoginPacketType.CheckSpwResult:
                    _loginUtilityDialogInputValue = string.Empty;
                    _loginTitleStatusMessage = "Packet-authored CheckSpwResult surfaced the client security warning notice.";
                    break;
            }
        }


        private void ShowSelectWorldFailureDialog(byte resultCode, string rejectionMessage)

        {

            switch (unchecked((sbyte)resultCode))

            {

                case 14:

                    ShowLoginUtilityDialog(

                        "Login Utility",

                        rejectionMessage,

                        LoginUtilityDialogButtonLayout.YesNo,

                        LoginUtilityDialogAction.WebsiteHandoffDecision,

                        noticeTextIndex: 27);

                    break;

                case 15:

                    ShowLoginUtilityDialog(

                        "Login Utility",

                        rejectionMessage,

                        LoginUtilityDialogButtonLayout.YesNo,

                        LoginUtilityDialogAction.WebsiteHandoffDecision,

                        noticeTextIndex: 26);

                    break;

                case -1:
                case 6:
                case 8:
                case 9:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 15);
                    break;

                case 2:
                case 3:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 16);
                    break;

                case 4:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 3);
                    break;

                case 5:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 20);
                    break;

                case 7:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 17);
                    break;

                case 10:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 19);
                    break;

                case 11:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 14);
                    break;

                case 13:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 21);
                    break;

                case 16:
                case 21:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 33);
                    break;

                case 17:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 27);
                    break;

                case 25:
                    ShowLoginUtilityDialog("Login Utility", rejectionMessage, LoginUtilityDialogButtonLayout.Ok, LoginUtilityDialogAction.DismissOnly, noticeTextIndex: 40);
                    break;

            }

        }



        private void ExecuteLoginCharacterDeleteConfirmation()

        {

            if (!CanMutateAccountBackedLoginRoster(out string validationMessage))



            {



                _loginCharacterStatusMessage = validationMessage;



                HideLoginUtilityDialog();



                SyncLoginCharacterSelectWindow();



                return;



            }



            if (_loginUtilityDialogTargetIndex >= 0)

            {

                _loginCharacterRoster.Select(_loginUtilityDialogTargetIndex);

            }



            LoginCharacterRosterEntry selectedEntry = _loginCharacterRoster.SelectedEntry;

            if (selectedEntry?.Build == null || !selectedEntry.CanDelete)

            {

                _loginCharacterStatusMessage = "The selected character cannot be deleted.";

                HideLoginUtilityDialog();

                SyncLoginCharacterSelectWindow();

                return;

            }

            LoginAccountDialogPacketProfile deleteProfile =

                BuildGeneratedDeleteCharacterResultProfile(selectedEntry.Build.Id);

            if (deleteProfile == null)

            {

                _loginCharacterStatusMessage = "Unable to build a DeleteCharacterResult profile for the selected character.";

                HideLoginUtilityDialog();

                SyncLoginCharacterSelectWindow();

                return;

            }



            _loginPacketAccountDialogProfiles[LoginPacketType.DeleteCharacterResult] = deleteProfile;

            HideLoginUtilityDialog();

            DispatchLoginRuntimePacket(
                LoginPacketType.DeleteCharacterResult,
                out string runtimeMessage,
                applySelectorSideEffects: false,
                summaryOverride: $"DeleteCharacterResult accepted {selectedEntry.Build.Name} and reloaded character selection.");

            if (string.IsNullOrWhiteSpace(_loginCharacterStatusMessage))

            {

                _loginCharacterStatusMessage = runtimeMessage;

            }

        }







        private void ExecuteLoginCharacterCreateConfirmation()

        {

            if (!CanMutateAccountBackedLoginRoster(out string validationMessage))

            {

                _loginCharacterStatusMessage = validationMessage;

                HideLoginUtilityDialog();

                SyncLoginCharacterSelectWindow();

                return;

            }



            string characterName;
            if (_loginCreateCharacterFlow != null)
            {
                characterName = _loginCreateCharacterFlow.EnteredName?.Trim();

                if (!_loginCreateCharacterFlow.HasCheckedName)

                {

                    _loginCreateCharacterFlow.ClearCheckedName("CheckDuplicatedIdResult must succeed before CreateNewCharacterResult can continue.");

                    _loginCharacterStatusMessage = _loginCreateCharacterFlow.StatusMessage;

                    SyncLoginCreateCharacterWindow();

                    return;

                }
            }
            else if (!TryGetLoginUtilityDialogInput(out characterName))

            {

                _loginCharacterStatusMessage = "Enter a character name before continuing.";

                SyncLoginCharacterSelectWindow();

                SyncLoginCreateCharacterWindow();

                return;

            }



            _loginPendingCreateCharacterName = characterName;
            if (!TryGetLoginCheckDuplicatedIdPacketProfile(out LoginAccountDialogPacketProfile duplicatedIdProfile) ||
                duplicatedIdProfile.Payload == null ||
                duplicatedIdProfile.Payload.Length == 0)
            {
                _loginPacketAccountDialogProfiles[LoginPacketType.CheckDuplicatedIdResult] =
                    BuildGeneratedCheckDuplicatedIdResultProfile(characterName);
            }

            if (_loginCreateCharacterFlow == null)

            {

                HideLoginUtilityDialog();

                DispatchLoginRuntimePacket(
                    LoginPacketType.CheckDuplicatedIdResult,
                    out string runtimeMessage,
                    applySelectorSideEffects: false);

                if (string.IsNullOrWhiteSpace(_loginCharacterStatusMessage))
                {
                    _loginCharacterStatusMessage = runtimeMessage;
                }
            }
            else
            {
                ContinueLoginCharacterCreateAfterDuplicateCheck(characterName);
            }
        }

        private void ContinueLoginCharacterCreateAfterDuplicateCheck(string characterName)

        {

            LoginCharacterAccountStore.LoginCharacterAccountState storedState = _loginCharacterAccountStore.GetState(

                ResolveLoginRosterAccountName(),

                ResolveLoginRosterWorldId());



            int slotCount = Math.Max(storedState?.SlotCount ?? _loginCharacterRoster.SlotCount, LoginCharacterRosterManager.EntriesPerPage);

            int buyCharacterCount = Math.Max(0, storedState?.BuyCharacterCount ?? _loginCharacterRoster.BuyCharacterCount);

            int occupiedCount = _loginCharacterRoster.Entries.Count;

            if (occupiedCount >= slotCount)

            {

                if (buyCharacterCount <= 0)

                {

                    _loginCharacterStatusMessage = "The account-backed roster has no remaining character slots.";

                    SyncLoginCharacterSelectWindow();

                    return;

                }



                slotCount++;

                buyCharacterCount--;

            }



            List<LoginCharacterAccountStore.LoginCharacterAccountEntryState> storedEntries = _loginCharacterRoster.Entries

                .Select(CreateLoginCharacterAccountEntryState)

                .ToList();



            int characterId = Math.Max(storedState?.NextCharacterId ?? 1, CalculateNextLoginCharacterId(storedEntries));

            if (_loginPacketCreateNewCharacterResultProfile == null ||
                _loginPacketCreateNewCharacterResultProfile.Payload == null ||
                _loginPacketCreateNewCharacterResultProfile.Payload.Length == 0)
            {
                _loginPacketCreateNewCharacterResultProfile =
                    BuildGeneratedCreateNewCharacterResultProfile(characterName, characterId);
            }

            if (_loginPacketCreateNewCharacterResultProfile == null)
            {
                _loginCharacterStatusMessage = "Unable to build a starter avatar for CreateNewCharacterResult.";
                SyncLoginCharacterSelectWindow();
                return;
            }

            DispatchLoginRuntimePacket(
                LoginPacketType.CreateNewCharacterResult,
                out string runtimeMessage,
                applySelectorSideEffects: false);

            if (string.IsNullOrWhiteSpace(_loginCharacterStatusMessage))
            {
                _loginCharacterStatusMessage = runtimeMessage;
            }


        }







        private void HandleRecommendWorldPreviousRequested()

        {

            if (_recommendWorldEntries.Count <= 1 || !IsWorldChannelSelectorRequestAllowed())

            {

                return;

            }



            _recommendWorldIndex = (_recommendWorldIndex - 1 + _recommendWorldEntries.Count) % _recommendWorldEntries.Count;

            _selectorBrowseWorldId = _recommendWorldEntries[_recommendWorldIndex].WorldId;

            RefreshWorldChannelSelectorWindows();

            SyncRecommendWorldWindow();

        }



        private void HandleRecommendWorldNextRequested()

        {

            if (_recommendWorldEntries.Count <= 1 || !IsWorldChannelSelectorRequestAllowed())

            {

                return;

            }



            _recommendWorldIndex = (_recommendWorldIndex + 1) % _recommendWorldEntries.Count;

            _selectorBrowseWorldId = _recommendWorldEntries[_recommendWorldIndex].WorldId;

            RefreshWorldChannelSelectorWindows();

            SyncRecommendWorldWindow();

        }



        private void HandleRecommendWorldSelected(int worldId)

        {

            _recommendWorldDismissed = true;

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.RecommendWorld);

            HandleWorldSelected(worldId);

        }



        private void HandleRecommendWorldClosed()

        {

            _recommendWorldDismissed = true;

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.RecommendWorld);

        }



        private void EnsureWorldChannelSelectorState(IEnumerable<int> worldIds)

        {

            if (worldIds == null)

            {

                return;

            }



            foreach (int worldId in worldIds.Distinct().OrderBy(id => id))

            {

                if (_simulatorChannelStatesByWorld.ContainsKey(worldId))

                {

                    continue;

                }



                int availableChannels = 10 + ((worldId * 3) % 11);

                List<ChannelSelectionState> channels = new List<ChannelSelectionState>(DefaultSimulatorChannelCount);

                for (int channelIndex = 0; channelIndex < DefaultSimulatorChannelCount; channelIndex++)

                {

                    bool isVisible = channelIndex < availableChannels;

                    bool isCurrentSelection = worldId == _simulatorWorldId && channelIndex == _simulatorChannelIndex;

                    int occupancySeed = ((worldId + 1) * 97 + (channelIndex + 3) * 61) % 101;

                    int userCount = Math.Clamp((int)Math.Round(DefaultSimulatorChannelCapacity * (occupancySeed / 100d)), 0, DefaultSimulatorChannelCapacity);

                    int capacity = isVisible ? DefaultSimulatorChannelCapacity : 0;

                    bool isSelectable = isVisible && (userCount < DefaultSimulatorChannelCapacity || isCurrentSelection);



                    channels.Add(new ChannelSelectionState(channelIndex, userCount, capacity, isSelectable));

                }



                _simulatorChannelStatesByWorld[worldId] = channels;

            }

        }



        private void EnsureLoginWorldSelectorMetadata(IEnumerable<int> worldIds)

        {

            if (!ShouldUseLoginWorldMetadata || worldIds == null)

            {

                return;

            }



            foreach (int worldId in worldIds.Distinct().OrderBy(id => id))

            {

                if (_loginWorldMetadataByWorld.ContainsKey(worldId))

                {

                    continue;

                }



                if (_loginWorldInfoPacketProfiles.TryGetValue(worldId, out LoginWorldInfoPacketProfile profile))

                {

                    _loginWorldMetadataByWorld[worldId] = CreateLoginWorldSelectorMetadataFromProfile(profile);

                    continue;

                }



                int visibleChannels = 8 + ((worldId * 5) % 10);

                int capacityBase = 600 + (((worldId + 1) * 75) % 250);

                bool requiresAdultWorldAccess = worldId > 0 && worldId % 9 == 0;

                List<ChannelSelectionState> channels = new(DefaultSimulatorChannelCount);

                for (int channelIndex = 0; channelIndex < DefaultSimulatorChannelCount; channelIndex++)

                {

                    bool isVisible = channelIndex < visibleChannels;

                    int capacity = isVisible

                        ? capacityBase + (((channelIndex + 1) * 40) % 160)

                        : 0;

                    bool isCurrentSelection = worldId == _simulatorWorldId && channelIndex == _simulatorChannelIndex;

                    int occupancySeed = ((worldId + 2) * 71 + (channelIndex + 5) * 43) % 100;

                    int occupancyPercent = Math.Clamp(25 + occupancySeed, 0, 100);

                    int userCount = capacity <= 0

                        ? 0

                        : Math.Clamp((int)Math.Round(capacity * (occupancyPercent / 100d)), 0, capacity);

                    bool requiresAdultAccount = isVisible &&

                                                (requiresAdultWorldAccess ||

                                                 (channelIndex >= Math.Max(0, visibleChannels - 2) &&

                                                  ((worldId + channelIndex) % 4 == 0)));

                    bool isSelectable = isVisible && (userCount < capacity || isCurrentSelection);



                    channels.Add(new ChannelSelectionState(channelIndex, userCount, capacity, isSelectable, requiresAdultAccount));

                }



                _loginWorldMetadataByWorld[worldId] = new LoginWorldSelectorMetadata(

                    worldId,

                    channels,

                    requiresAdultWorldAccess,

                    hasAuthoritativePopulationData: false,

                    recommendMessage: GetLoginPacketRecommendedWorldMessage(worldId),

                    recommendOrder: GetLoginPacketRecommendedWorldOrder(worldId));

            }

        }



        private void ClearLoginWorldSelectorMetadata()

        {

            _loginWorldMetadataByWorld.Clear();

            _loginRecommendedWorldIds.Clear();

            _loginWorldInfoPacketProfiles.Clear();

            _loginPacketRecommendedWorldIds.Clear();

            _loginPacketRecommendedWorldMessages.Clear();

            _loginPacketRecommendedWorldOrder.Clear();

            _loginLatestConnectedWorldId = null;

            _loginPacketLatestConnectedWorldId = null;

            _loginPacketCheckUserLimitResultCode = null;

            _loginPacketCheckUserLimitPopulationLevel = null;

            _loginPacketSelectWorldResultCode = null;

            _loginPacketSelectWorldTargetWorldId = null;

            _loginPacketSelectWorldTargetChannelIndex = null;

            _loginPacketSelectWorldResultProfile = null;

            _loginPacketViewAllCharResultProfile = null;

            _loginPacketViewAllCharRosterProfile = null;

            _loginPacketViewAllCharRemainingServerCount = 0;

            _loginPacketViewAllCharExpectedCharacterCount = 0;

            _loginPacketViewAllCharEntries.Clear();

            _loginPacketSelectCharacterResultProfile = null;
            _loginPacketSelectCharacterByVacResultProfile = null;
            _loginPacketCheckPasswordResultProfile = null;
            _loginPacketGuestIdLoginResultProfile = null;

            _loginPacketExtraCharInfoResultProfile = null;

            _loginCanHaveExtraCharacter = false;

            _loginPacketDialogPrompts.Clear();

            _selectorLastResultCode = SelectorRequestResultCode.None;

            _selectorLastResultMessage = null;

            _nextLoginWorldPopulationUpdateAt = int.MinValue;

            ClearActiveConnectionNotice();

            HideLoginUtilityDialog();

        }



        private void ApplyLoginWorldSelectorPacket(LoginPacketType packetType, IReadOnlyList<string> packetArgs = null)

        {

            packetArgs ??= Array.Empty<string>();

            _ = packetArgs;

            if (!IsLoginRuntimeSceneActive)

            {

                return;

            }



            switch (packetType)

            {

                case LoginPacketType.CheckPasswordResult:

                    ClearLoginWorldSelectorMetadata();

                    _selectorBrowseWorldId = _simulatorWorldId;

                    break;

                case LoginPacketType.WorldInformation:

                    RebuildLoginWorldSelectorMetadataFromPacketProfiles();

                    EnsureLoginWorldSelectorMetadata(GetRegisteredWorldSelectorIds());

                    UpdateRecommendedLoginWorlds();

                    break;

                case LoginPacketType.RecommendWorldMessage:

                    EnsureLoginWorldSelectorMetadata(GetRegisteredWorldSelectorIds());

                    ApplyRecommendWorldMessageMetadata();

                    UpdateRecommendedLoginWorlds();

                    break;

                case LoginPacketType.LatestConnectedWorld:

                    EnsureLoginWorldSelectorMetadata(GetRegisteredWorldSelectorIds());

                    UpdateLatestConnectedWorld();

                    break;

            }

        }



        private void UpdateRecommendedLoginWorlds()

        {

            _loginRecommendedWorldIds.Clear();

            if (_loginPacketRecommendedWorldOrder.Count > 0)

            {

                foreach (int worldId in _loginPacketRecommendedWorldOrder)

                {

                    _loginRecommendedWorldIds.Add(worldId);

                }



                return;

            }



            foreach (int worldId in _loginWorldMetadataByWorld

                         .OrderBy(pair => pair.Value.Channels.Where(channel => channel.Capacity > 0).DefaultIfEmpty()

                             .Average(channel => channel?.OccupancyPercent ?? 100))

                         .ThenBy(pair => pair.Key)

                         .Take(3)

                         .Select(pair => pair.Key))

            {

                _loginRecommendedWorldIds.Add(worldId);

            }

        }



        private void ApplyRecommendWorldMessageMetadata()

        {

            foreach ((int worldId, LoginWorldSelectorMetadata metadata) in _loginWorldMetadataByWorld.ToArray())

            {

                _loginWorldMetadataByWorld[worldId] = new LoginWorldSelectorMetadata(

                    worldId,

                    metadata.Channels,

                    metadata.RequiresAdultAccount,

                    metadata.HasAuthoritativePopulationData,

                    recommendMessage: GetLoginPacketRecommendedWorldMessage(worldId),

                    recommendOrder: GetLoginPacketRecommendedWorldOrder(worldId));

            }

        }



        private string GetLoginPacketRecommendedWorldMessage(int worldId)

        {

            return _loginPacketRecommendedWorldMessages.TryGetValue(worldId, out string message) &&

                   !string.IsNullOrWhiteSpace(message)

                ? message

                : null;

        }



        private int? GetLoginPacketRecommendedWorldOrder(int worldId)

        {

            int index = _loginPacketRecommendedWorldOrder.IndexOf(worldId);

            return index >= 0 ? index : null;

        }



        private void UpdateLatestConnectedWorld()

        {

            if (_loginWorldMetadataByWorld.Count == 0)

            {

                _loginLatestConnectedWorldId = null;

                return;

            }



            int latestWorldId = _loginPacketLatestConnectedWorldId.HasValue && _loginWorldMetadataByWorld.ContainsKey(_loginPacketLatestConnectedWorldId.Value)

                ? _loginPacketLatestConnectedWorldId.Value

                : _loginWorldMetadataByWorld.ContainsKey(_simulatorWorldId)

                    ? _simulatorWorldId

                    : _loginWorldMetadataByWorld.Keys.OrderBy(id => id).First();

            _loginLatestConnectedWorldId = latestWorldId;



            if (_loginRuntime.CurrentStep == LoginStep.WorldSelect &&

                _selectorRequestKind == SelectorRequestKind.None)

            {

                _selectorBrowseWorldId = latestWorldId;

            }

        }



        private Dictionary<int, WorldSelectionState> BuildWorldSelectionStates()

        {

            Dictionary<int, WorldSelectionState> states = new();

            IEnumerable<KeyValuePair<int, IReadOnlyList<ChannelSelectionState>>> channelsByWorld = ShouldUseLoginWorldMetadata

                ? _loginWorldMetadataByWorld.ToDictionary(pair => pair.Key, pair => pair.Value.Channels)

                : _simulatorChannelStatesByWorld.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ChannelSelectionState>)pair.Value);



            foreach ((int worldId, IReadOnlyList<ChannelSelectionState> channels) in channelsByWorld)

            {

                int totalChannels = channels.Count(channel => channel.Capacity > 0);

                int activeChannels = channels.Count(channel => channel.Capacity > 0 && channel.CanSelect(_loginAccountIsAdult));

                int occupancyPercent = channels.Count == 0

                    ? 0

                    : (int)Math.Round(channels.Where(channel => channel.Capacity > 0).DefaultIfEmpty().Average(channel => channel?.OccupancyPercent ?? 0));

                bool isSelectable = worldId == _simulatorWorldId || activeChannels > 0;

                bool hasAdultChannels = channels.Any(channel => channel.Capacity > 0 && channel.RequiresAdultAccount);



                states[worldId] = new WorldSelectionState(

                    worldId,

                    activeChannels,

                    totalChannels,

                    occupancyPercent,

                    isSelectable,

                    hasAdultChannels,

                    isRecommended: ShouldUseLoginWorldMetadata && _loginRecommendedWorldIds.Contains(worldId),

                    isLatestConnected: ShouldUseLoginWorldMetadata && _loginLatestConnectedWorldId == worldId);

            }



            return states;

        }



        private LoginWorldSelectorMetadata CreateLoginWorldSelectorMetadataFromProfile(LoginWorldInfoPacketProfile profile)

        {

            int visibleChannels = Math.Clamp(profile.VisibleChannelCount, 0, DefaultSimulatorChannelCount);

            List<ChannelSelectionState> channels = new(DefaultSimulatorChannelCount);



            if (profile.HasAuthoritativeChannelPopulation)

            {

                Dictionary<int, LoginWorldInfoChannelPacketProfile> channelProfiles = new();

                int nextFallbackIndex = 0;

                foreach (LoginWorldInfoChannelPacketProfile channelProfile in profile.Channels)

                {

                    if (channelProfile == null)

                    {

                        continue;

                    }



                    int channelIndex = channelProfile.ChannelId > 0 && channelProfile.ChannelId <= DefaultSimulatorChannelCount

                        ? channelProfile.ChannelId - 1

                        : nextFallbackIndex;

                    nextFallbackIndex = Math.Max(nextFallbackIndex, channelIndex + 1);

                    if (channelIndex < 0 || channelIndex >= DefaultSimulatorChannelCount)

                    {

                        continue;

                    }



                    channelProfiles[channelIndex] = channelProfile;

                }



                int maxUserCount = Math.Max(1, channelProfiles.Values.Select(channel => channel.UserCount).DefaultIfEmpty().Max());

                int worldPeakOccupancyPercent = profile.WorldState switch

                {

                    >= 2 => 96,

                    1 => 78,

                    _ => Math.Max(40, profile.OccupancyPercent),

                };

                int sharedCapacity = Math.Max(

                    maxUserCount,

                    (int)Math.Ceiling(maxUserCount * 100d / Math.Max(1, worldPeakOccupancyPercent)));



                for (int channelIndex = 0; channelIndex < DefaultSimulatorChannelCount; channelIndex++)

                {

                    if (!channelProfiles.TryGetValue(channelIndex, out LoginWorldInfoChannelPacketProfile channelProfile))

                    {

                        channels.Add(new ChannelSelectionState(channelIndex, 0, 0, false));

                        continue;

                    }



                    int capacity = Math.Max(sharedCapacity, channelProfile.UserCount);

                    bool isCurrentSelection = profile.WorldId == _simulatorWorldId && channelIndex == _simulatorChannelIndex;

                    bool isSelectable = channelProfile.UserCount < capacity || isCurrentSelection;

                    channels.Add(new ChannelSelectionState(

                        channelIndex,

                        channelProfile.UserCount,

                        capacity,

                        isSelectable,

                        channelProfile.RequiresAdultAccess));

                }

            }

            else

            {

                int capacityBase = 620 + (((profile.WorldId + 3) * 53) % 180);

                for (int channelIndex = 0; channelIndex < DefaultSimulatorChannelCount; channelIndex++)

                {

                    bool isVisible = channelIndex < visibleChannels;

                    int capacity = isVisible

                        ? capacityBase + (((channelIndex + 1) * 37) % 140)

                        : 0;

                    int bias = ((profile.WorldId * 7) + (channelIndex * 5)) % 11 - 5;

                    int occupancyPercent = Math.Clamp(profile.OccupancyPercent + (bias * 4), 0, 100);

                    int userCount = capacity <= 0

                        ? 0

                        : Math.Clamp((int)Math.Round(capacity * (occupancyPercent / 100d)), 0, capacity);

                    bool isCurrentSelection = profile.WorldId == _simulatorWorldId && channelIndex == _simulatorChannelIndex;

                    bool requiresAdultAccount = isVisible &&

                                                profile.RequiresAdultAccess &&

                                                channelIndex >= Math.Max(0, visibleChannels - 3);

                    bool isSelectable = isVisible && (userCount < capacity || isCurrentSelection);



                    channels.Add(new ChannelSelectionState(

                        channelIndex,

                        userCount,

                        capacity,

                        isSelectable,

                        requiresAdultAccount));

                }

            }



            return new LoginWorldSelectorMetadata(

                profile.WorldId,

                channels,

                profile.RequiresAdultAccess,

                hasAuthoritativePopulationData: profile.HasAuthoritativeChannelPopulation,

                recommendMessage: GetLoginPacketRecommendedWorldMessage(profile.WorldId),

                recommendOrder: GetLoginPacketRecommendedWorldOrder(profile.WorldId));

        }



        private void RebuildLoginWorldSelectorMetadataFromPacketProfiles()

        {

            if (_loginWorldInfoPacketProfiles.Count == 0)

            {

                return;

            }



            _loginWorldMetadataByWorld.Clear();

            foreach ((int worldId, LoginWorldInfoPacketProfile profile) in _loginWorldInfoPacketProfiles.OrderBy(pair => pair.Key))

            {

                _loginWorldMetadataByWorld[worldId] = CreateLoginWorldSelectorMetadataFromProfile(profile);

            }

        }



        private IReadOnlyList<ChannelSelectionState> GetChannelSelectionStates(int worldId)

        {

            if (ShouldUseLoginWorldMetadata)

            {

                EnsureLoginWorldSelectorMetadata(new[] { worldId });

                if (_loginWorldMetadataByWorld.TryGetValue(worldId, out LoginWorldSelectorMetadata metadata))

                {

                    return metadata.Channels.ToArray();

                }

            }



            if (!_simulatorChannelStatesByWorld.TryGetValue(worldId, out List<ChannelSelectionState> channels))

            {

                EnsureWorldChannelSelectorState(new[] { worldId });

                _simulatorChannelStatesByWorld.TryGetValue(worldId, out channels);

            }



            return channels?.ToArray() ?? Array.Empty<ChannelSelectionState>();

        }



        private int ResolveCookieHouseContextPoint()

        {

            if (_playerManager?.Player?.Build != null)

            {

                return Math.Max(0, _playerManager.Player.Build.CookieHousePoint);

            }



            return Math.Max(0, _cookieHouseContextPoint);

        }



        private void SetCookieHouseContextPoint(int point)

        {

            int clampedPoint = Math.Max(0, point);

            _cookieHouseContextPoint = clampedPoint;



            if (_playerManager?.Player?.Build != null)

            {

                _playerManager.Player.Build.CookieHousePoint = clampedPoint;

            }

        }





        // Cached StringBuilder for debug text to avoid GC allocations every frame

        private readonly StringBuilder _debugStringBuilder = new StringBuilder(256);



        // Cached navigation help strings to avoid string.Format every frame

        private string _navHelpTextMobOn;

        private string _navHelpTextMobOff;

        private readonly MobAttackSystem _mobAttackSystem = new MobAttackSystem();



        /// <summary>

        /// MapSimulator Constructor

        /// </summary>

        /// <param name="_mapBoard"></param>

        /// <param name="titleName"></param>

        /// <param name="spawnPortalName">Optional portal name to spawn at (from portal teleportation)</param>

        public MapSimulator(Board _mapBoard, string titleName, string spawnPortalName = null)

        {

            _mobMirrorBoundaryResolver = ResolveMobMirrorBoundary;

            _npcMirrorBoundaryResolver = ResolveNpcMirrorBoundary;

            _chatFallbackMeasureGraphics = SD.Graphics.FromImage(_chatFallbackMeasureBitmap);

            _chatFallbackMeasureGraphics.TextRenderingHint = SDText.TextRenderingHint.AntiAliasGridFit;

            _chatFallbackFont = new SD.Font("Segoe UI", 13f, SD.FontStyle.Regular, SD.GraphicsUnit.Point);

            _chatFallbackLineHeight = MeasureFallbackChatText("Ag").Y;

            this._mapBoard = _mapBoard;

            this._spawnPortalName = spawnPortalName;

            _mapTransferDestinations = new MapTransferDestinationStore();
            _mapTransferRuntime = new MapTransferRuntimeManager(_mapTransferDestinations);

            _skillMacroStore = new SkillMacroStore();

            _questAlarmStore = new QuestAlarmStore();

            _itemMakerProgressionStore = new ItemMakerProgressionStore();
            _monsterBookManager = new MonsterBookManager();

            _loginCharacterAccountStore = new LoginCharacterAccountStore();
            _socialRoomPersistenceStore = new SocialRoomPersistenceStore();


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

            Window.TextInput += Window_TextInput;

            _imeCompositionMonitor.CompositionTextChanged += HandleImeCompositionChanged;
            _imeCompositionMonitor.CompositionStateChanged += HandleImeCompositionStateChanged;
            _imeCompositionMonitor.CandidateListChanged += HandleImeCandidateListChanged;

            _imeCompositionMonitor.Attach(Window.Handle);

            _chat.MessageSubmitted = HandleChatMessageSubmitted;

            _specialFieldRuntime.SetCookieHousePointProvider(ResolveCookieHouseContextPoint);



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

            _mobAttackSystem.SetGroundResolver((x, y) =>

            {

                var footholds = _mapBoard?.BoardItems?.FootholdLines;

                if (footholds == null)

                {

                    return null;

                }



                FootholdLine bestFoothold = null;

                float bestY = float.MinValue;

                foreach (FootholdLine foothold in footholds)

                {

                    if (foothold.IsWall)

                    {

                        continue;

                    }



                    float minX = Math.Min(foothold.FirstDot.X, foothold.SecondDot.X);

                    float maxX = Math.Max(foothold.FirstDot.X, foothold.SecondDot.X);

                    if (x < minX || x > maxX)

                    {

                        continue;

                    }



                    float footholdY = Board.CalculateYOnFoothold(foothold, x);

                    if (footholdY + 40f < y)

                    {

                        continue;

                    }



                    if (footholdY > bestY)

                    {

                        bestY = footholdY;

                        bestFoothold = foothold;

                    }

                }



                return bestFoothold != null ? Board.CalculateYOnFoothold(bestFoothold, x) : (float?)null;

            });

            _mobAttackSystem.SetGroundColumnResolver((x, minY, maxY) =>

            {

                var footholds = _mapBoard?.BoardItems?.FootholdLines;

                if (footholds == null)

                {

                    return Array.Empty<float>();

                }



                float top = Math.Min(minY, maxY);

                float bottom = Math.Max(minY, maxY);

                var ys = new List<float>();

                foreach (FootholdLine foothold in footholds)

                {

                    if (foothold.IsWall)

                    {

                        continue;

                    }



                    float minX = Math.Min(foothold.FirstDot.X, foothold.SecondDot.X);

                    float maxX = Math.Max(foothold.FirstDot.X, foothold.SecondDot.X);

                    if (x < minX || x > maxX)

                    {

                        continue;

                    }



                    float footholdY = Board.CalculateYOnFoothold(foothold, x);

                    if (footholdY < top || footholdY > bottom)

                    {

                        continue;

                    }



                    bool duplicate = false;

                    for (int i = 0; i < ys.Count; i++)

                    {

                        if (Math.Abs(ys[i] - footholdY) < 0.5f)

                        {

                            duplicate = true;

                            break;

                        }

                    }



                    if (!duplicate)

                    {

                        ys.Add(footholdY);

                    }

                }



                ys.Sort();

                return ys;

            });

            _mobAttackSystem.SetPuppetTargeting(

                () => _mobPool?.ActivePuppets,

                (puppet, _, __, currentTick) =>

                {

                    bool consumedSummon = puppet != null

                        && ((_playerManager?.Skills?.TryDamageSummonByObjectId(puppet.ObjectId, 1, currentTick) ?? false)
                            || _summonedPool.TryDamageSummonByObjectId(puppet.ObjectId, 1, currentTick));


                    if (puppet != null && !consumedSummon)

                    {

                        puppet.IsActive = false;

                    }



                    _mobPool?.UpdatePuppets(currentTick);

                    _mobPool?.SyncPuppetTargets(currentTick);

                    _mobPool?.SyncEncounterTargets(currentTick);

                    _mobPool?.SyncHypnotizedTargets(currentTick);

                });

            _mobAttackSystem.SetMobTargeting(

                mobId => _mobPool?.GetMob(mobId),

                () => _frameActiveMobs);

            _mobAttackSystem.SetPlayerHitboxAccessor(() => _playerManager?.GetPlayerHitbox() ?? Rectangle.Empty);

            _mobAttackSystem.SetPlayerGroundedAccessor(() => _playerManager?.IsPlayerOnGround() ?? true);

        }



        #region Loading and unloading

        void graphics_DeviceCreated(object sender, EventArgs e)

        {

        }



        private void Window_TextInput(object sender, TextInputEventArgs e)

        {

            if (char.IsControl(e.Character))

            {

                return;

            }



            if (_npcInteractionOverlay?.CapturesKeyboardInput == true)
            {
                _npcInteractionOverlay.HandleCommittedText(e.Character.ToString());
                return;
            }

            uiWindowManager?.ActiveKeyboardWindow?.HandleCommittedText(e.Character.ToString());

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



            //GraphicsDevice.Viewport = new Viewport(RenderWidth / 2 - 800 / 2, RenderHeight / 2 - 600 / 2, 800, 600);



            // https://stackoverflow.com/questions/55045066/how-do-i-convert-a-ttf-or-other-font-to-a-xnb-xna-game-studio-font

            // if you're having issues building on w10, install Visual C++ Redistributable for Visual Studio 2012 Update 4

            //

            // to build your own font: /MonoGame Font Builder/game.mgcb

            // build -> obj -> copy it over to HaRepacker-resurrected [Content]

            _fontNavigationKeysHelper = Content.Load<SpriteFont>("XnaDefaultFont");

            _fontChat = Content.Load<SpriteFont>("XnaFont_Chat");//("XnaFont_Debug");

            _fontDebugValues = Content.Load<SpriteFont>("XnaDefaultFont");//("XnaFont_Debug");

            _fontNavigationKeysHelper.DefaultCharacter = '?';

            _fontChat.DefaultCharacter = '?';

            _fontDebugValues.DefaultCharacter = '?';



            // Set fonts on rendering manager

            _renderingManager.SetFonts(_fontDebugValues, _fontChat, _fontNavigationKeysHelper);



            // Pre-cache navigation help text strings to avoid string.Format allocations in Draw()

            _navHelpTextMobOn = "[Arrows/WASD] Move | [Alt] Jump | [Ctrl] Attack | [Tab] Toggle Camera | [R] Respawn\n[F5] Debug | [F6] Mob movement (ON) | [F7] Shake | [F8] Knockback\n[F9] Motion Blur | [F10] Explosion | [F11] Lightning | [F12] Sparks\n[1] Rain | [2] Snow | [3] Leaves | [4] Fear | [5] Weather Msg | [0] Clear\n[6] H-Platform | [7] V-Platform | [8] Timed | [9] Waypoint | [Space] Sparkle\n[-] Ship Voyage | [=] Balrog/Skip/Reset | [ScrollWheel] Zoom | [Home] Reset Zoom | [C] Smooth Cam";

            _navHelpTextMobOff = "[Arrows/WASD] Move | [Alt] Jump | [Ctrl] Attack | [Tab] Toggle Camera | [R] Respawn\n[F5] Debug | [F6] Mob movement (OFF) | [F7] Shake | [F8] Knockback\n[F9] Motion Blur | [F10] Explosion | [F11] Lightning | [F12] Sparks\n[1] Rain | [2] Snow | [3] Leaves | [4] Fear | [5] Weather Msg | [0] Clear\n[6] H-Platform | [7] V-Platform | [8] Timed | [9] Waypoint | [Space] Sparkle\n[-] Ship Voyage | [=] Balrog/Skip/Reset | [ScrollWheel] Zoom | [Home] Reset Zoom | [C] Smooth Cam";



            base.Initialize();

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
            ResetPacketOwnedRadioSchedule();

            if (_audio != null)

            {

                //_audio.Pause();

                _audio.Dispose();

            }



            _soundManager?.Dispose();

            _coconutPacketInbox.Dispose();
            _coconutOfficialSessionBridge.Dispose();

            _ariantArenaPacketInbox.Dispose();
            _monsterCarnivalPacketInbox.Dispose();
            _monsterCarnivalOfficialSessionBridge.Dispose();

            _monsterCarnivalPacketInbox.Dispose();
            _guildBossTransport.Dispose();
            _guildBossOfficialSessionBridge.Dispose();

            _dojoPacketInbox.Dispose();
            _transportPacketInbox.Dispose();

            _partyRaidPacketInbox.Dispose();

            _cookieHousePointInbox.Dispose();
            _localUtilityPacketInbox.Dispose();
            _summonedPacketInbox.Dispose();



            _skeletonMeshRenderer?.End();



            _DxDeviceManager.EndDraw();

            _DxDeviceManager.Dispose();





            mapObjects_NPCs.Clear();

            mapObjects_Mobs.Clear();

            mapObjects_Reactors.Clear();

            mapObjects_Portal.Clear();



            backgrounds_front.Clear();

            backgrounds_back.Clear();

            _mobAttackSystem.Clear();



            _texturePool.Dispose();



            // clear prior mirror bottom boundary

            _mirrorBottomRect = new Rectangle();

            _mirrorBottomReflection = null;

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

            ConcurrentBag<WzObject> usedPropsTemp = new ConcurrentBag<WzObject>();

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

                        LoadShipFromCategory(shipCategory, "contimove", usedPropsTemp);

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

                                LoadShipFromCategory(shipCategory, oS, usedPropsTemp);

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

                            LoadShipFromCategory(shipCategory, kvp.Key, usedPropsTemp);

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

                    var shipMobFrames = LoadMobFrames(mobId, "stand", usedPropsTemp);

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

                    var balrogFrames = LoadMobFrames(mobId, "fly", usedPropsTemp);

                    if (balrogFrames == null || balrogFrames.Count == 0)

                        balrogFrames = LoadMobFrames(mobId, "move", usedPropsTemp);

                    if (balrogFrames == null || balrogFrames.Count == 0)

                        balrogFrames = LoadMobFrames(mobId, "stand", usedPropsTemp);



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

        private void LoadShipFromCategory(WzObject shipCategory, string source, ConcurrentBag<WzObject> usedProps)

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

                        var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, shipVariant, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps);

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

                var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, imgProp, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps);

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

        private List<IDXObject> LoadMobFrames(string mobId, string action, ConcurrentBag<WzObject> usedProps)

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

                        return MapSimulatorLoader.LoadFrames(_texturePool, imgProp, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps);

                    }

                }

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"[TransportField] Error loading mob {mobId}: {ex.Message}");

            }

            return null;

        }



        private MobItem CreateMobFromSpawnPoint(MobSpawnPoint spawnPoint)

        {

            if (spawnPoint == null || string.IsNullOrWhiteSpace(spawnPoint.MobId))

            {

                return null;

            }



            MobInfo mobInfo = MobInfo.Get(spawnPoint.MobId);

            if (mobInfo == null)

            {

                Debug.WriteLine($"[MobRespawn] Unable to resolve mob info for {spawnPoint.MobId}");

                return null;

            }



            var mobInstance = new MobInstance(

                mobInfo,

                _mapBoard,

                (int)spawnPoint.X,

                (int)spawnPoint.Y,

                spawnPoint.Rx0Shift,

                spawnPoint.Rx1Shift,

                spawnPoint.YShift,

                spawnPoint.LimitedName,

                ConvertRespawnMillisecondsToMapMobTime(spawnPoint.RespawnTimeMs),

                spawnPoint.Flip,

                spawnPoint.Hide,

                spawnPoint.Info,

                spawnPoint.Team);



            ConcurrentBag<WzObject> usedProps = new ConcurrentBag<WzObject>();

            return MapSimulatorLoader.CreateMobFromProperty(

                _texturePool,

                mobInstance,

                UserScreenScaleFactor,

                _DxDeviceManager.GraphicsDevice,

                _soundManager,

                usedProps);

        }



        private static int? ConvertRespawnMillisecondsToMapMobTime(int respawnTimeMs)

        {

            if (respawnTimeMs <= 0)

            {

                return respawnTimeMs;

            }



            return Math.Max(1, respawnTimeMs / 1000);

        }



        private void AddMobToActiveArrays(MobItem mob)

        {

            if (mob == null)

            {

                return;

            }



            ApplyRespawnedMobState(mob);



            if (_mobsArray == null || _mobsArray.Length == 0)

            {

                _mobsArray = new[] { mob };

            }

            else

            {

                for (int i = 0; i < _mobsArray.Length; i++)

                {

                    if (_mobsArray[i] == null)

                    {

                        _mobsArray[i] = mob;

                        RefreshMobRenderArray();

                        return;

                    }

                }



                Array.Resize(ref _mobsArray, _mobsArray.Length + 1);

                _mobsArray[^1] = mob;

            }



            RefreshMobRenderArray();

        }



        private void ApplyRespawnedMobState(MobItem mob)

        {

            if (mob?.MovementInfo == null)

            {

                return;

            }



            Rectangle rawVR = _mapBoard.VRRectangle != null

                ? new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y, _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height)

                : new Rectangle(-_mapBoard.CenterPoint.X, -_mapBoard.CenterPoint.Y, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);



            mob.SetMapBoundaries(rawVR.Left, rawVR.Right, rawVR.Top, rawVR.Bottom);

            mob.MovementEnabled = _gameState.MobMovementEnabled;



            var footholds = _mapBoard.BoardItems.FootholdLines;

            if (footholds == null || footholds.Count == 0)

            {

                return;

            }



            if (mob.MovementInfo.MoveType == MobMoveType.Move ||

                mob.MovementInfo.MoveType == MobMoveType.Stand ||

                mob.MovementInfo.MoveType == MobMoveType.Jump)

            {

                mob.MovementInfo.FindCurrentFoothold(footholds);

            }

        }



        private void RefreshMobRenderArray()

        {

            _renderingManager?.SetRenderArrays(

                _backgroundsBackArray,

                _backgroundsFrontArray,

                _mapObjectsArray,

                _mobsArray,

                _npcsArray,

                _portalsArray,

                _reactorsArray,

                _tooltipsArray);

            _renderingManager?.SetVisibleRenderSets(

                _useSpatialPartitioning ? _visibleMapObjects : null,

                _useSpatialPartitioning ? _visibleMapObjectsCount : 0,

                _visibleMobs,

                _visibleMobsCount,

                _visibleNpcs,

                _visibleNpcsCount,

                _useSpatialPartitioning ? _visiblePortals : null,

                _useSpatialPartitioning ? _visiblePortalsCount : 0,

                _useSpatialPartitioning ? _visibleReactors : null,

                _useSpatialPartitioning ? _visibleReactorsCount : 0);

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

            ShipObject shipObject = _mapBoard?.BoardItems?.MiscItems
                ?.OfType<ShipObject>()
                .FirstOrDefault();
            if (!TryResolveTransportationFieldDefinition(_mapBoard?.MapInfo, shipObject, out TransportationFieldDefinition definition))
            {
                System.Diagnostics.Debug.WriteLine("[TransportField] No shipObj map property or ShipObject found in map");
                return;
            }

            System.Diagnostics.Debug.WriteLine("[TransportField] Detected client-owned transport field:");
            System.Diagnostics.Debug.WriteLine($"  - Position: ({definition.DockX}, {definition.DockY})");
            System.Diagnostics.Debug.WriteLine($"  - X0: {definition.AwayX}");
            System.Diagnostics.Debug.WriteLine($"  - ShipKind: {definition.ShipKind}");
            System.Diagnostics.Debug.WriteLine($"  - TimeMove: {definition.MoveDurationSeconds}s");
            System.Diagnostics.Debug.WriteLine($"  - Flip: {definition.Flip}");
            if (!string.IsNullOrWhiteSpace(definition.ShipObjectPath))
            {
                System.Diagnostics.Debug.WriteLine($"  - ShipObj: {definition.ShipObjectPath}");
            }

            ConcurrentBag<WzObject> usedPropsTemp = new ConcurrentBag<WzObject>();
            if (!_transportField.HasShipTextures && !TryLoadTransportShipFrames(definition, shipObject, usedPropsTemp))
            {
                LoadTransportFieldTextures();
            }

            _transportField.Initialize(
                shipKind: definition.ShipKind,
                x: definition.DockX,
                y: definition.DockY,
                x0: definition.AwayX,
                f: definition.Flip,
                tMove: definition.MoveDurationSeconds,
                shipPath: definition.ShipObjectPath);

            System.Diagnostics.Debug.WriteLine("[TransportField] Transport field initialized from client-owned map transport data");

        }

        private static bool TryResolveTransportationFieldDefinition(
            MapInfo mapInfo,
            ShipObject shipObject,
            out TransportationFieldDefinition definition)
        {
            if (TransportationFieldDefinitionLoader.TryCreate(mapInfo, out definition))
            {
                return true;
            }

            if (shipObject == null)
            {
                definition = null;
                return false;
            }

            definition = new TransportationFieldDefinition(
                shipObject.X,
                shipObject.Y,
                shipObject.X0 ?? shipObject.X - 800,
                shipObject.Flip ? 1 : 0,
                shipObject.TimeMove > 0 ? shipObject.TimeMove : 10,
                shipObject.ShipKind,
                string.Empty);
            return true;
        }

        private bool TryLoadTransportShipFrames(
            TransportationFieldDefinition definition,
            ShipObject shipObject,
            ConcurrentBag<WzObject> usedProps)
        {
            if (TryLoadTransportShipFramesFromMapPath(definition.ShipObjectPath, usedProps))
            {
                return true;
            }

            return TryLoadTransportShipFramesFromShipObject(shipObject, usedProps);
        }

        private bool TryLoadTransportShipFramesFromMapPath(string shipObjectPath, ConcurrentBag<WzObject> usedProps)
        {
            if (!TransportationFieldDefinitionLoader.TryResolveObjectSetPath(shipObjectPath, out string objectSetName, out string propertyPath))
            {
                return false;
            }

            try
            {
                WzImage objectSet = Program.InfoManager?.GetObjectSet(objectSetName);
                if (objectSet == null)
                {
                    return false;
                }

                if (!objectSet.Parsed)
                {
                    objectSet.ParseImage();
                }

                WzObject current = objectSet;
                string[] pathSegments = propertyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < pathSegments.Length && current != null; i++)
                {
                    current = current switch
                    {
                        WzImage image => image[pathSegments[i]],
                        WzImageProperty property => property[pathSegments[i]],
                        _ => null
                    };
                }

                if (current is not WzImageProperty currentProperty)
                {
                    return false;
                }

                var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, currentProperty, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps);
                if (shipFrames.Count == 0)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[TransportField] Loaded {shipFrames.Count} ship frames from {objectSetName}/{propertyPath}");
                _transportField.SetShipFrames(shipFrames);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportField] Error loading ship frames from {shipObjectPath}: {ex.Message}");
                return false;
            }
        }

        private bool TryLoadTransportShipFramesFromShipObject(ShipObject shipObject, ConcurrentBag<WzObject> usedProps)
        {
            if (shipObject?.BaseInfo is not ObjectInfo objInfo)
            {
                return false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[TransportField] Ship ObjectInfo fallback: {objInfo.oS}/{objInfo.l0}/{objInfo.l1}/{objInfo.l2}");

                WzImage objectSet = Program.InfoManager?.GetObjectSet(objInfo.oS);
                if (objectSet == null)
                {
                    return false;
                }

                if (!objectSet.Parsed)
                {
                    objectSet.ParseImage();
                }

                WzImageProperty animContainer = objectSet[objInfo.l0]?[objInfo.l1];
                if (animContainer == null)
                {
                    return false;
                }

                var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, animContainer, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps);
                if (shipFrames.Count == 0)
                {
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[TransportField] Loaded {shipFrames.Count} ship frames from {objInfo.oS}/{objInfo.l0}/{objInfo.l1}");
                _transportField.SetShipFrames(shipFrames);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TransportField] Error loading ship frames from ShipObject fallback: {ex.Message}");
                return false;
            }
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



        private IReadOnlyList<MobMovementInfo> CollectGroundMobMovementInfos()

        {

            _groundMobMovementBuffer.Clear();

            if (_frameMovableMobs.Count == 0)

            {

                return _groundMobMovementBuffer;

            }



            for (int i = 0; i < _frameMovableMobs.Count; i++)

            {

                MobMovementInfo movementInfo = _frameMovableMobs[i].MovementInfo;

                if (movementInfo != null)

                {

                    _groundMobMovementBuffer.Add(movementInfo);

                }

            }



            return _groundMobMovementBuffer;

        }



        private void RefreshFrameMobSnapshot()

        {

            _frameActiveMobs.Clear();

            _frameMovableMobs.Clear();

            _framePrimaryBossMob = null;



            IReadOnlyList<MobItem> activeMobs = _mobPool?.ActiveMobs;

            if (activeMobs == null || activeMobs.Count == 0)

            {

                return;

            }



            int highestBossLevel = int.MinValue;

            for (int i = 0; i < activeMobs.Count; i++)

            {

                MobItem mob = activeMobs[i];

                if (mob?.AI == null || mob.AI.IsDead)

                {

                    continue;

                }



                _frameActiveMobs.Add(mob);

                if (mob.MovementInfo != null)

                {

                    _frameMovableMobs.Add(mob);

                }



                if (mob.AI.IsBoss && mob.AI.Level > highestBossLevel)

                {

                    highestBossLevel = mob.AI.Level;

                    _framePrimaryBossMob = mob;

                }

            }

        }



        private void SyncBgmPlaybackToWindowFocus()
        {
            _soundManager?.SetFocusActive(!_pauseAudioOnFocusLoss || IsActive);

            if (_audio == null)
            {
                _isBgmPausedForFocusLoss = false;
                return;
            }

            if (!_pauseAudioOnFocusLoss)
            {
                if (_isBgmPausedForFocusLoss)
                {
                    _audio.Resume();
                    _isBgmPausedForFocusLoss = false;
                }

                return;
            }

            if (IsActive)
            {
                if (_isBgmPausedForFocusLoss)

                {

                    _audio.Resume();

                    _isBgmPausedForFocusLoss = false;

                }



                return;

            }



            if (!_isBgmPausedForFocusLoss)

            {

                _audio.Pause();

                _isBgmPausedForFocusLoss = true;

            }

        }



        private void StartBgmForCurrentFocusState()
        {
            _isBgmPausedForFocusLoss = false;

            if (_audio == null)
            {

                return;

            }


            _audio.Play();
            if (_utilityBgmMuted)
            {
                _audio.Volume = 0f;
            }

            if (_pauseAudioOnFocusLoss && !IsActive)
            {
                _audio.Pause();
                _isBgmPausedForFocusLoss = true;
            }
        }


        private void RequestSpecialFieldBgmOverride(string bgmName)

        {

            if (string.IsNullOrWhiteSpace(bgmName))

            {

                return;

            }



            if (string.Equals(_specialFieldBgmOverrideName, bgmName, StringComparison.Ordinal))

            {

                return;

            }



            _specialFieldBgmOverrideName = bgmName;

            ApplyRequestedBgm(_specialFieldBgmOverrideName);

        }



        private void ClearSpecialFieldBgmOverride()

        {

            if (_specialFieldBgmOverrideName == null)

            {

                return;

            }



            _specialFieldBgmOverrideName = null;

            ApplyRequestedBgm(_mapBgmName);

        }



        private void ApplyRequestedBgm(string bgmName)

        {

            if (string.Equals(_currentBgmName, bgmName, StringComparison.Ordinal))

            {

                return;

            }



            if (_audio != null)

            {

                _audio.Dispose();

                _audio = null;

            }



            if (string.IsNullOrWhiteSpace(bgmName))

            {

                _currentBgmName = null;

                _isBgmPausedForFocusLoss = false;

                return;

            }



            WzBinaryProperty bgmProperty = Program.InfoManager.GetBgm(bgmName);

            if (bgmProperty == null)

            {

                _currentBgmName = null;

                _isBgmPausedForFocusLoss = false;

                return;

            }



            _currentBgmName = bgmName;
            _audio = new MonoGameBgmPlayer(bgmProperty, true);
            StartBgmForCurrentFocusState();
            ApplyUtilityAudioSettings();
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



            if (CanUseSpatialPartitioning())

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



            UpdateVisibleEntityBuffers(centerX, centerY, viewWidth, viewHeight);



            _renderingManager?.SetVisibleRenderSets(

                _useSpatialPartitioning ? _visibleMapObjects : null,

                _useSpatialPartitioning ? _visibleMapObjectsCount : 0,

                _visibleMobs,

                _visibleMobsCount,

                _visibleNpcs,

                _visibleNpcsCount,

                _useSpatialPartitioning ? _visiblePortals : null,

                _useSpatialPartitioning ? _visiblePortalsCount : 0,

                _useSpatialPartitioning ? _visibleReactors : null,

                _useSpatialPartitioning ? _visibleReactorsCount : 0);



            // Update mirror boundaries only for entities that may render this frame.

            UpdateMirrorBoundaries();

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private bool CanUseSpatialPartitioning()

        {

            return _useSpatialPartitioning &&

                   _mapObjectsGrid != null &&

                   _portalsGrid != null &&

                   _reactorsGrid != null &&

                   _visibleMapObjects != null &&

                   _visiblePortals != null &&

                   _visibleReactors != null;

        }



        /// <summary>

        /// Standard visibility update - iterates all objects

        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void UpdateObjectVisibilityStandard(int centerX, int centerY, int viewWidth, int viewHeight)

        {

            _visibleMapObjectsCount = 0;

            _visiblePortalsCount = 0;

            _visibleReactorsCount = 0;



            // Update map objects visibility

            if (_mapObjectsArray != null)

            {

                for (int layer = 0; layer < _mapObjectsArray.Length; layer++)

                {

                    BaseDXDrawableItem[] layerItems = _mapObjectsArray[layer];

                    for (int i = 0; i < layerItems.Length; i++)

                    {

                        BaseDXDrawableItem item = layerItems[i];

                        item.UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);

                        ApplyQuestObjectVisibility(item);

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

            ClearPreviousSpatialVisibility();



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

                BaseDXDrawableItem item = _visibleMapObjects[i];

                item.SetVisible(true);

                item.UpdateVisibility(mapShiftX, mapShiftY, centerX, centerY, viewWidth, viewHeight, _frameNumber);

                ApplyQuestObjectVisibility(item);

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



        private void ClearPreviousSpatialVisibility()

        {

            if (_visibleMapObjects != null)

            {

                int visibleMapObjectsToClear = Math.Min(_visibleMapObjectsCount, _visibleMapObjects.Length);

                for (int i = 0; i < visibleMapObjectsToClear; i++)

                {

                    _visibleMapObjects[i]?.SetVisible(false);

                    _visibleMapObjects[i] = null;

                }

            }



            if (_visiblePortals != null)

            {

                int visiblePortalsToClear = Math.Min(_visiblePortalsCount, _visiblePortals.Length);

                for (int i = 0; i < visiblePortalsToClear; i++)

                {

                    _visiblePortals[i]?.SetVisible(false);

                    _visiblePortals[i] = null;

                }

            }



            if (_visibleReactors != null)

            {

                int visibleReactorsToClear = Math.Min(_visibleReactorsCount, _visibleReactors.Length);

                for (int i = 0; i < visibleReactorsToClear; i++)

                {

                    _visibleReactors[i]?.SetVisible(false);

                    _visibleReactors[i] = null;

                }

            }



            _visibleMapObjectsCount = 0;

            _visiblePortalsCount = 0;

            _visibleReactorsCount = 0;

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void UpdateVisibleEntityBuffers(int centerX, int centerY, int viewWidth, int viewHeight)

        {

            EnsureVisibleEntityBufferCapacity(_mobsArray?.Length ?? 0, _npcsArray?.Length ?? 0);



            _visibleMobsCount = 0;

            if (_mobsArray != null)

            {

                for (int i = 0; i < _mobsArray.Length; i++)

                {

                    MobItem mob = _mobsArray[i];

                    if (mob == null)

                    {

                        continue;

                    }



                    if (!IsWorldEntityInView(mob.CurrentX, mob.CurrentY, mob.MobInstance?.Width ?? 0, mob.MobInstance?.Height ?? 0, centerX, centerY, viewWidth, viewHeight))

                    {

                        continue;

                    }



                    _visibleMobs[_visibleMobsCount++] = mob;

                }

            }



            _visibleNpcsCount = 0;

            if (_npcsArray != null)

            {

                for (int i = 0; i < _npcsArray.Length; i++)

                {

                    NpcItem npc = _npcsArray[i];

                    if (npc == null)

                    {

                        continue;

                    }



                    if (!IsWorldEntityInView(npc.CurrentX, npc.CurrentY, npc.NpcInstance?.Width ?? 0, npc.NpcInstance?.Height ?? 0, centerX, centerY, viewWidth, viewHeight))

                    {

                        continue;

                    }



                    _visibleNpcs[_visibleNpcsCount++] = npc;

                }

            }

        }



        private void EnsureVisibleEntityBufferCapacity(int mobCapacity, int npcCapacity)

        {

            if (_visibleMobs == null || _visibleMobs.Length < mobCapacity)

            {

                _visibleMobs = new MobItem[Math.Max(mobCapacity, 1)];

            }



            if (_visibleNpcs == null || _visibleNpcs.Length < npcCapacity)

            {

                _visibleNpcs = new NpcItem[Math.Max(npcCapacity, 1)];

            }

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private bool IsWorldEntityInView(int entityX, int entityY, int entityWidth, int entityHeight, int centerX, int centerY, int viewWidth, int viewHeight)

        {

            int screenX = entityX - mapShiftX + centerX;

            int screenY = entityY - mapShiftY + centerY;

            int horizontalPadding = Math.Max(96, entityWidth + 48);

            int verticalPadding = Math.Max(96, entityHeight + 48);



            return screenX >= -horizontalPadding &&

                   screenX <= viewWidth + horizontalPadding &&

                   screenY >= -verticalPadding &&

                   screenY <= viewHeight + verticalPadding;

        }



        /// <summary>

        /// Pre-calculates mirror boundaries for mobs and NPCs.

        /// Uses caching to avoid redundant checks every frame.

        /// </summary>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void UpdateMirrorBoundaries()

        {

            for (int i = 0; i < _visibleMobsCount; i++)

            {

                _visibleMobs[i]?.UpdateMirrorBoundary(_mirrorBottomRect, _mirrorBottomReflection, _mobMirrorBoundaryResolver);

            }



            for (int i = 0; i < _visibleNpcsCount; i++)

            {

                _visibleNpcs[i]?.UpdateMirrorBoundary(_mirrorBottomRect, _mirrorBottomReflection, _npcMirrorBoundaryResolver);

            }

        }



        private ReflectionDrawableBoundary ResolveMobMirrorBoundary(int x, int y)

        {

            var mirrorData = _mapBoard?.BoardItems?.CheckObjectWithinMirrorFieldDataBoundary(x, y, MirrorFieldDataType.mob);

            return mirrorData?.ReflectionInfo;

        }



        private ReflectionDrawableBoundary ResolveNpcMirrorBoundary(int x, int y)

        {

            var mirrorData = _mapBoard?.BoardItems?.CheckObjectWithinMirrorFieldDataBoundary(x, y, MirrorFieldDataType.npc);

            return mirrorData?.ReflectionInfo;

        }



        /// <summary>

        /// Test knockback on a random visible mob (for debugging F8 key)

        /// </summary>

        private void TestKnockbackRandomMob()

        {

            if (_frameMovableMobs.Count == 0)

                return;



            // Find visible mob and knockback at 50% rate

            var random = new Random();

            int startIndex = random.Next(_frameMovableMobs.Count);



            for (int i = 0; i < _frameMovableMobs.Count; i++)

            {

                int mobIndex = (startIndex + i) % _frameMovableMobs.Count;

                MobItem mob = _frameMovableMobs[mobIndex];



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

            float deltaSecondsLocal = (float)gameTime.ElapsedGameTime.TotalSeconds;

            _mobPool?.UpdatePuppets(tickCount);

            _mobPool?.SyncPuppetTargets(tickCount);

            _mobPool?.SyncEncounterTargets(tickCount);

            _mobPool?.SyncHypnotizedTargets(tickCount);



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



            CleanupAppliedMobSkillEffects(tickCount);

            List<MobItem> skillEffectMobs = null;



            EscortProgressionState escortProgressionState = EscortProgressionController.ResolveState(_frameActiveMobs);



            for (int i = 0; i < _frameMovableMobs.Count; i++)

            {

                MobItem mobItem = _frameMovableMobs[i];



                mobItem.MovementEnabled = _gameState.MobMovementEnabled;

                bool escortFollowActive = mobItem.AI?.IsEscortMob == true

                    && EscortProgressionController.CanMobFollow(mobItem, escortProgressionState)

                     && _escortFollow.UpdateEscortFollow(

                         _playerManager?.Player,

                         mobItem.MovementInfo,

                         _playerManager?.IsMovementLockedByMobStatus == true,

                         _mapBoard?.MapInfo?.nofollowCharacter != true,

                         tickCount);

                mobItem.SetEscortFollowActive(escortFollowActive);



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



                _mobAttackSystem.QueueMobAttackActions(mobItem, tickCount, playerX, playerY);

                if (ShouldApplyMobSkillEffect(mobItem, tickCount))

                {

                    skillEffectMobs ??= new List<MobItem>();

                    skillEffectMobs.Add(mobItem);

                }

            }



            if (skillEffectMobs != null)

            {

                for (int i = 0; i < skillEffectMobs.Count; i++)

                {

                    ApplyMobSkillEffect(skillEffectMobs[i], tickCount);

                }

            }



            _mobAttackSystem.Update(

                tickCount,

                deltaSecondsLocal,

                _playerManager,

                _animationEffects,

                t => _effectManager.Tremble(2, true, 0, 0, true, t));



            // Update mob pool for death animations, cleanup, and respawns

            _mobPool?.Update(tickCount, CreateMobFromSpawnPoint);



            // Update drop pool for physics and expiration (frame-rate independent)

            _dropPool?.Update(tickCount, deltaSecondsLocal);
            UpdateRemotePlayerDropPickups(tickCount);
            UpdateRemotePetDropPickups(tickCount);



            // Update portal pool for hidden portal visibility

            if (playerX.HasValue && playerY.HasValue)

            {

                _portalPool?.Update(playerX.Value, playerY.Value, tickCount, deltaSecondsLocal);

                UpdateDynamicObjectDirectionEventTriggers(tickCount);

            }



            // Update reactor pool for state management and respawns

            _reactorPool?.Update(tickCount, deltaSecondsLocal);

            _affectedAreaPool?.Update(tickCount);
            UpdateRemoteAffectedAreaGameplay(tickCount);


            // Update pickup notice UI animations

            _pickupNoticeUI?.Update(tickCount, deltaSecondsLocal);

            _skillCooldownNoticeUI?.Update(tickCount, deltaSecondsLocal);

        }

        private void UpdateRemotePlayerDropPickups(int currentTime)
        {
            if (_dropPool == null || _remoteUserPool.Count == 0)
            {
                return;
            }

            foreach (RemoteUserActor actor in _remoteUserPool.Actors)
            {
                if (actor == null || !actor.IsVisibleInWorld || actor.CharacterId <= 0)
                {
                    continue;
                }

                if (_lastRemotePlayerPickupTimes.TryGetValue(actor.CharacterId, out int lastPickupTime)
                    && currentTime - lastPickupTime < REMOTE_PLAYER_PICKUP_INTERVAL_MS)
                {
                    continue;
                }

                DropItem pickedDrop = _dropPool.TryPickUpDropByRemotePlayer(
                    actor.CharacterId,
                    actor.Position.X,
                    actor.Position.Y,
                    currentTime,
                    actor.Name);
                if (pickedDrop != null)
                {
                    _lastRemotePlayerPickupTimes[actor.CharacterId] = currentTime;
                }
            }
        }

        private void UpdateRemotePetDropPickups(int currentTime)
        {
            if (_dropPool == null || _remoteUserPool.Count == 0)
            {
                return;
            }

            foreach (RemoteUserActor actor in _remoteUserPool.Actors)
            {
                if (actor?.Build?.RemotePetItemIds == null
                    || !actor.IsVisibleInWorld
                    || actor.CharacterId <= 0)
                {
                    continue;
                }

                for (int slotIndex = 0; slotIndex < actor.Build.RemotePetItemIds.Count; slotIndex++)
                {
                    int petItemId = actor.Build.RemotePetItemIds[slotIndex];
                    if (petItemId <= 0)
                    {
                        continue;
                    }

                    long pickupKey = BuildRemotePetPickupKey(actor.CharacterId, slotIndex);
                    if (_lastRemotePetPickupTimes.TryGetValue(pickupKey, out int lastPickupTime)
                        && currentTime - lastPickupTime < REMOTE_PET_PICKUP_INTERVAL_MS)
                    {
                        continue;
                    }

                    Vector2 petPosition = ResolveRemotePetPickupPosition(actor, slotIndex);
                    int petActorId = BuildRemotePetPickupActorId(actor.CharacterId, slotIndex);
                    string petName = ResolveRemotePetPickupName(petItemId, slotIndex);
                    DropItem pickedDrop = _dropPool.TryPickUpDropByRemotePet(
                        petActorId,
                        actor.CharacterId,
                        petPosition.X,
                        petPosition.Y,
                        currentTime,
                        petName,
                        pickupValidator: EvaluateRemotePetPickupAvailability);
                    if (pickedDrop != null)
                    {
                        _lastRemotePetPickupTimes[pickupKey] = currentTime;
                    }
                }
            }
        }



        private bool ShouldApplyMobSkillEffect(MobItem mobItem, int currentTick)

        {

            if (mobItem?.AI == null || !mobItem.AI.ShouldApplySkillEffect(currentTick))

            {

                return false;

            }



            MobSkillEntry skill = mobItem.AI.GetCurrentSkill();

            if (skill == null)

            {

                return false;

            }



            long key = GetMobSkillEffectKey(mobItem, currentTick);

            if (_appliedMobSkillEffects.ContainsKey(key))

            {

                return false;

            }



            _appliedMobSkillEffects[key] = currentTick + Math.Max(Math.Max(skill.SkillAfter, skill.EffectAfter), 2000);

            return true;

        }



        private void CleanupAppliedMobSkillEffects(int currentTick)

        {

            if (_appliedMobSkillEffects.Count == 0)

            {

                return;

            }



            _expiredMobSkillEffectKeys.Clear();

            foreach (KeyValuePair<long, int> entry in _appliedMobSkillEffects)

            {

                if (currentTick < entry.Value)

                {

                    continue;

                }



                _expiredMobSkillEffectKeys.Add(entry.Key);

            }



            if (_expiredMobSkillEffectKeys.Count == 0)

            {

                return;

            }



            for (int i = 0; i < _expiredMobSkillEffectKeys.Count; i++)

            {

                _appliedMobSkillEffects.Remove(_expiredMobSkillEffectKeys[i]);

            }

        }



        private void ApplyMobSkillEffect(MobItem mobItem, int currentTick)

        {

            MobSkillEntry skill = mobItem?.AI?.GetCurrentSkill();

            if (skill == null)

            {

                return;

            }



            ApplyMobSkillVisualEffect(mobItem, skill, currentTick);



            switch (skill.SkillId)

            {

                case 200:

                    ApplyMobSummonSkill(mobItem, skill, currentTick);

                    break;

                default:

                    ApplyMobStatusSkill(mobItem, skill, currentTick);

                    break;

            }

        }



        private void ApplyMobStatusSkill(MobItem sourceMob, MobSkillEntry skill, int currentTick)

        {

            if (sourceMob?.AI == null)

            {

                return;

            }



            MobSkillRuntimeData runtimeData = ResolveMobSkillRuntimeData(skill.SkillId, skill.Level);

            if (runtimeData == null)

            {

                return;

            }



            if (TryApplyPlayerTargetedMobSkill(sourceMob, skill, runtimeData, currentTick))

            {

                return;

            }



            if (!MobSkillStatusMapper.TryGetDefinition(skill.SkillId, out MobSkillStatusDefinition definition))

            {

                return;

            }



            if (definition.Operation == MobSkillOperation.Heal)

            {

                int healAmount = Math.Max(0, runtimeData.X);

                if (healAmount <= 0)

                {

                    return;

                }



                foreach (MobItem targetMob in ResolveMobSkillStatusTargets(sourceMob, definition, runtimeData, currentTick))

                {

                    if (targetMob?.AI == null || targetMob.AI.IsDead)

                    {

                        continue;

                    }



                    targetMob.AI.Heal(healAmount);

                }



                return;

            }



            if (definition.Operation == MobSkillOperation.ClearNegativeStatuses)

            {

                foreach (MobItem targetMob in ResolveMobSkillStatusTargets(sourceMob, definition, runtimeData, currentTick))

                {

                    if (targetMob?.AI == null || targetMob.AI.IsDead)

                    {

                        continue;

                    }



                    targetMob.AI.ClearNegativeStatusEffects();

                }



                return;

            }



            if (runtimeData.DurationMs <= 0)

            {

                return;

            }



            int value = MobSkillStatusMapper.ResolveStatusValue(

                definition.Effect,

                runtimeData.X,

                runtimeData.Y,

                runtimeData.Hp);

            if (value <= 0)

            {

                return;

            }



            foreach (MobItem targetMob in ResolveMobSkillStatusTargets(sourceMob, definition, runtimeData, currentTick))

            {

                targetMob?.AI?.ApplyStatusEffect(

                    definition.Effect,

                    runtimeData.DurationMs,

                    currentTick,

                    value,

                    secondaryValue: runtimeData.X,

                    tertiaryValue: runtimeData.Y,

                    sourceSkillId: skill.SkillId);

            }

        }



        private bool TryApplyPlayerTargetedMobSkill(MobItem sourceMob, MobSkillEntry skill, MobSkillRuntimeData runtimeData, int currentTick)

        {

            if (sourceMob == null

                || skill == null

                || _playerManager?.Player == null

                || !_playerManager.Player.IsAlive)

            {

                return false;

            }



            Rectangle area = CreateMobSkillArea(sourceMob, runtimeData);

            Rectangle playerHitbox = _playerManager.Player.GetHitbox();

            if (playerHitbox.IsEmpty || !playerHitbox.Intersects(area))

            {

                return false;

            }



            bool applied = _playerManager.TryApplyMobSkillStatus(skill.SkillId, runtimeData, currentTick, sourceMob.CurrentX);



            if (runtimeData.DurationMs > 0 &&

                PlayerSkillBlockingStatusMapper.TryMapMobSkill(skill.SkillId, out PlayerSkillBlockingStatus status))

            {

                _playerManager.Player.ApplySkillBlockingStatus(status, runtimeData.DurationMs, currentTick);

                applied = true;

            }



            return applied;

        }



        private void ApplyMobSkillVisualEffect(MobItem mobItem, MobSkillEntry skill, int currentTick)

        {

            MobSkillEffectData effectData = _playerManager?.LoadMobSkillEffect(skill.SkillId, skill.Level);

            if (effectData == null)

            {

                return;

            }



            if (effectData.HasEffect)

            {

                Vector2 effectPosition = ResolveMobSkillEffectPosition(mobItem, effectData);

                bool flip = mobItem?.MovementInfo?.FlipX ?? false;

                _animationEffects?.AddOneTime(effectData.EffectFrames, effectPosition.X, effectPosition.Y, flip, currentTick);



                if (effectData.EffectPosition == MobSkillEffectPosition.Screen)

                {

                    _effectManager.Tremble(2, true, 0, 0, true, currentTick);

                }

            }



            if (effectData.MobIconFrames != null && effectData.MobIconFrames.Count > 0)

            {

                Vector2 iconPosition = ResolveMobSkillIconPosition(mobItem);

                _animationEffects?.AddOneTime(effectData.MobIconFrames, iconPosition.X, iconPosition.Y, false, currentTick, 1);

            }

        }



        private Vector2 ResolveMobSkillEffectPosition(MobItem mobItem, MobSkillEffectData effectData)

        {

            if (mobItem == null)

            {

                return Vector2.Zero;

            }



            float mobX = mobItem.CurrentX;

            float mobY = mobItem.CurrentY;

            float mobHeight = Math.Max(60, mobItem.GetVisualHeight(60));



            switch (effectData.EffectPosition)

            {

                case MobSkillEffectPosition.Target:
                    if (mobItem.AI?.Target?.IsValid == true)

                    {

                        float targetY = mobItem.AI.Target.TargetY;
                        if (mobItem.AI.Target.TargetType == MobTargetType.Player && _playerManager?.Player != null)
                        {

                            targetY = _playerManager.Player.Y - 24f;

                        }

                        return new Vector2(mobItem.AI.Target.TargetX, targetY);

                    }

                    if (_playerManager?.Player != null)

                    {

                        return new Vector2(_playerManager.Player.X, _playerManager.Player.Y - 24f);

                    }



                    break;



                case MobSkillEffectPosition.Screen:

                    if (_playerManager?.Player != null)

                    {

                        return new Vector2(_playerManager.Player.X, _playerManager.Player.Y - 80f);

                    }



                    break;

            }



            return new Vector2(mobX, mobY - mobHeight * 0.5f);

        }



        private static Vector2 ResolveMobSkillIconPosition(MobItem mobItem)

        {

            if (mobItem == null)

            {

                return Vector2.Zero;

            }



            float mobHeight = Math.Max(60, mobItem.GetVisualHeight(60));

            return new Vector2(mobItem.CurrentX, mobItem.CurrentY - mobHeight);

        }



        private IEnumerable<MobItem> ResolveMobSkillStatusTargets(

            MobItem sourceMob,

            MobSkillStatusDefinition definition,

            MobSkillRuntimeData runtimeData,

            int currentTick)

        {

            if (sourceMob == null)

            {

                yield break;

            }



            MobSkillStatusTargetMode targetMode = ResolveMobSkillStatusTargetMode(definition, runtimeData);

            if (targetMode == MobSkillStatusTargetMode.Self || _mobPool == null)

            {

                yield return sourceMob;

                yield break;

            }



            Rectangle area = CreateMobSkillArea(sourceMob, runtimeData);

            bool yieldedSource = false;



            foreach (MobItem mob in _mobPool.ActiveMobs)

            {

                if (mob?.AI == null || mob.AI.IsDead)

                {

                    continue;

                }



                Rectangle hitbox = mob.GetBodyHitbox(currentTick);

                if (hitbox.IsEmpty || !hitbox.Intersects(area))

                {

                    continue;

                }



                if (ReferenceEquals(mob, sourceMob))

                {

                    yieldedSource = true;

                }



                yield return mob;

            }



            if (!yieldedSource)

            {

                yield return sourceMob;

            }

        }



        private static MobSkillStatusTargetMode ResolveMobSkillStatusTargetMode(

            MobSkillStatusDefinition definition,

            MobSkillRuntimeData runtimeData)

        {

            if (definition.TargetMode != MobSkillStatusTargetMode.RuntimeTargetMobType)

            {

                return definition.TargetMode;

            }



            return runtimeData?.TargetMobType == MobSkillTargetMobType.NearbyMobs

                ? MobSkillStatusTargetMode.NearbyMobs

                : MobSkillStatusTargetMode.Self;

        }



        private static Rectangle CreateMobSkillArea(MobItem sourceMob, MobSkillRuntimeData runtimeData)

        {

            if (sourceMob == null)

            {

                return Rectangle.Empty;

            }



            if (runtimeData?.Lt is not Point lt || runtimeData.Rb is not Point rb)

            {

                const int defaultHalfWidth = 200;

                const int defaultHalfHeight = 120;

                return new Rectangle(

                    (int)sourceMob.CurrentX - defaultHalfWidth,

                    (int)sourceMob.CurrentY - defaultHalfHeight,

                    defaultHalfWidth * 2,

                    defaultHalfHeight * 2);

            }



            int left = Math.Min(lt.X, rb.X);

            int right = Math.Max(lt.X, rb.X);

            if (sourceMob.MovementInfo?.FlipX == true)

            {

                (left, right) = (-right, -left);

            }

            int top = Math.Min(lt.Y, rb.Y);

            int bottom = Math.Max(lt.Y, rb.Y);

            return new Rectangle(

                (int)sourceMob.CurrentX + left,

                (int)sourceMob.CurrentY + top,

                Math.Max(1, right - left),

                Math.Max(1, bottom - top));

        }



        private void ApplyMobSummonSkill(MobItem mobItem, MobSkillEntry skill, int currentTick)

        {

            MobSummonSkillInfo summonInfo = ResolveMobSummonSkillInfo(skill.SkillId, skill.Level);

            if (summonInfo == null || summonInfo.MobIds.Count == 0 || _mobPool == null)

            {

                return;

            }



            int activeSummons = 0;

            for (int i = 0; i < summonInfo.MobIds.Count; i++)

            {

                activeSummons += _mobPool.GetMobsByType(summonInfo.MobIds[i]).Count();

            }



            int remainingCapacity = summonInfo.Limit > 0

                ? summonInfo.Limit - activeSummons

                : summonInfo.MobIds.Count;

            if (remainingCapacity <= 0)

            {

                return;

            }



            int spawnCount = Math.Min(remainingCapacity, summonInfo.MobIds.Count);

            for (int i = 0; i < spawnCount; i++)

            {

                MobSpawnPoint summonSpawn = CreateSummonedMobSpawnPoint(mobItem, summonInfo, summonInfo.MobIds[i], i);

                MobItem summonedMob = CreateMobFromSpawnPoint(summonSpawn);

                if (summonedMob == null)

                {

                    continue;

                }



                if (mobItem.AI.Target?.IsValid == true)

                {

                    summonedMob.AI?.ForceAggro(mobItem.AI.Target.TargetX, mobItem.AI.Target.TargetY, currentTick);

                }



                _mobPool.AddTemporaryMob(summonedMob, currentTick);

            }

        }



        private MobSpawnPoint CreateSummonedMobSpawnPoint(MobItem sourceMob, MobSummonSkillInfo summonInfo, string mobId, int spawnIndex)

        {

            Point relativeOffset = ResolveSummonSpawnOffset(summonInfo, spawnIndex);

            float spawnX = sourceMob.CurrentX + relativeOffset.X;

            float spawnY = sourceMob.CurrentY + relativeOffset.Y;



            return new MobSpawnPoint

            {

                MobId = mobId,

                X = spawnX,

                Y = spawnY,

                Flip = sourceMob.MovementInfo?.FlipX ?? false,

                RespawnTimeMs = -1,

                IsBoss = false

            };

        }



        private Point ResolveSummonSpawnOffset(MobSummonSkillInfo summonInfo, int spawnIndex)

        {

            if (summonInfo?.Lt is Point lt && summonInfo.Rb is Point rb)

            {

                int left = Math.Min(lt.X, rb.X);

                int right = Math.Max(lt.X, rb.X);

                int top = Math.Min(lt.Y, rb.Y);

                int bottom = Math.Max(lt.Y, rb.Y);

                int x = _mobSkillRandom.Next(left, right + 1);

                int y = _mobSkillRandom.Next(top, bottom + 1);

                return new Point(x, y);

            }



            int direction = spawnIndex % 2 == 0 ? 1 : -1;

            int distance = 35 + (spawnIndex / 2) * 40;

            return new Point(direction * distance, 0);

        }



        private MobSummonSkillInfo ResolveMobSummonSkillInfo(int skillId, int level)

        {

            var cacheKey = (skillId, level);

            if (_mobSummonSkillCache.TryGetValue(cacheKey, out MobSummonSkillInfo cachedInfo))

            {

                return cachedInfo;

            }



            WzImage mobSkillImage = Program.FindImage("Skill", "MobSkill");

            if (mobSkillImage != null && !mobSkillImage.Parsed)

            {

                mobSkillImage.ParseImage();

            }

            WzSubProperty skillNode = mobSkillImage?[skillId.ToString()] as WzSubProperty;

            WzSubProperty levelNode = skillNode?["level"] as WzSubProperty;

            WzSubProperty selectedLevel = ResolveMobSkillLevelNode(levelNode, level);

            if (selectedLevel == null)

            {

                return null;

            }



            var summonInfo = new MobSummonSkillInfo

            {

                Limit = MapleLib.WzLib.WzStructure.InfoTool.GetInt(selectedLevel["limit"], 0)

            };



            WzVectorProperty lt = selectedLevel["lt"] as WzVectorProperty;

            if (lt != null)

            {

                summonInfo.Lt = new Point(lt.X.Value, lt.Y.Value);

            }



            WzVectorProperty rb = selectedLevel["rb"] as WzVectorProperty;

            if (rb != null)

            {

                summonInfo.Rb = new Point(rb.X.Value, rb.Y.Value);

            }



            foreach (WzImageProperty child in selectedLevel.WzProperties)

            {

                if (!int.TryParse(child.Name, out _))

                {

                    continue;

                }



                int summonedMobId = MapleLib.WzLib.WzStructure.InfoTool.GetInt(child, 0);

                if (summonedMobId <= 0)

                {

                    continue;

                }



                summonInfo.MobIds.Add(summonedMobId.ToString());

            }



            _mobSummonSkillCache[cacheKey] = summonInfo;

            return summonInfo;

        }



        private MobSkillRuntimeData ResolveMobSkillRuntimeData(int skillId, int level)

        {

            var cacheKey = (skillId, level);

            if (_mobSkillRuntimeCache.TryGetValue(cacheKey, out MobSkillRuntimeData cachedData))

            {

                return cachedData;

            }



            WzImage mobSkillImage = Program.FindImage("Skill", "MobSkill");

            if (mobSkillImage != null && !mobSkillImage.Parsed)

            {

                mobSkillImage.ParseImage();

            }



            WzSubProperty skillNode = mobSkillImage?[skillId.ToString()] as WzSubProperty;

            WzSubProperty levelNode = skillNode?["level"] as WzSubProperty;

            WzSubProperty selectedLevel = ResolveMobSkillLevelNode(levelNode, level);

            if (selectedLevel == null)

            {

                return null;

            }



            Point? lt = ResolveMobSkillInheritedVector(levelNode, level, "lt");

            Point? rb = ResolveMobSkillInheritedVector(levelNode, level, "rb");

            int durationSeconds = ResolveMobSkillInheritedInt(levelNode, level, "time");



            var runtimeData = new MobSkillRuntimeData

            {

                X = ResolveMobSkillInheritedInt(levelNode, level, "x"),

                Y = ResolveMobSkillInheritedInt(levelNode, level, "y"),

                Hp = Math.Max(

                    ResolveMobSkillInheritedInt(levelNode, level, "hp"),

                    ResolveMobSkillInheritedInt(levelNode, level, "HP")),

                DurationMs = Math.Max(0, durationSeconds) * 1000,

                IntervalMs = Math.Max(

                    ResolveMobSkillInheritedInt(levelNode, level, "interval"),

                    ResolveMobSkillInheritedInt(levelNode, level, "inteval")) * 1000,

                PropPercent = ResolveMobSkillInheritedInt(levelNode, level, "prop"),

                Count = ResolveMobSkillInheritedInt(levelNode, level, "count"),

                TargetMobType = (MobSkillTargetMobType)ResolveMobSkillInheritedInt(levelNode, level, "targetMobType"),

                Lt = lt,

                Rb = rb

            };



            _mobSkillRuntimeCache[cacheKey] = runtimeData;

            return runtimeData;

        }

        private static WzSubProperty ResolveMobSkillLevelNode(WzSubProperty levelNode, int level)

        {

            return levelNode?[level.ToString()] as WzSubProperty ?? levelNode?["1"] as WzSubProperty;

        }

        private static int ResolveMobSkillInheritedInt(WzSubProperty levelNode, int level, string propertyName, int defaultValue = 0)

        {

            if (levelNode == null || string.IsNullOrWhiteSpace(propertyName))

            {

                return defaultValue;

            }



            if (TryGetMobSkillLevelInt(levelNode, level, propertyName, out int resolvedValue))

            {

                return resolvedValue;

            }



            return defaultValue;

        }

        private static bool TryGetMobSkillLevelInt(WzSubProperty levelNode, int level, string propertyName, out int value)

        {

            value = 0;
            if (levelNode == null || string.IsNullOrWhiteSpace(propertyName))

            {

                return false;

            }



            if (TryReadMobSkillLevelInt(levelNode, level, propertyName, out value))

            {

                return true;

            }



            var fallbackLevels = new List<int>();
            foreach (WzImageProperty child in levelNode.WzProperties)

            {

                if (int.TryParse(child.Name, out int candidateLevel) && candidateLevel < level)

                {

                    fallbackLevels.Add(candidateLevel);

                }

            }



            fallbackLevels.Sort((left, right) => right.CompareTo(left));
            foreach (int candidateLevel in fallbackLevels)

            {

                if (TryReadMobSkillLevelInt(levelNode, candidateLevel, propertyName, out value))

                {

                    return true;

                }

            }



            return TryReadMobSkillLevelInt(levelNode, 1, propertyName, out value);

        }

        private static Point? ResolveMobSkillInheritedVector(WzSubProperty levelNode, int level, string propertyName)

        {

            if (TryGetMobSkillLevelVector(levelNode, level, propertyName, out Point point))

            {

                return point;

            }



            return null;

        }

        private static bool TryGetMobSkillLevelVector(WzSubProperty levelNode, int level, string propertyName, out Point value)

        {

            value = Point.Zero;
            if (levelNode == null || string.IsNullOrWhiteSpace(propertyName))

            {

                return false;

            }



            if (TryReadMobSkillLevelVector(levelNode, level, propertyName, out value))

            {

                return true;

            }



            var fallbackLevels = new List<int>();
            foreach (WzImageProperty child in levelNode.WzProperties)

            {

                if (int.TryParse(child.Name, out int candidateLevel) && candidateLevel < level)

                {

                    fallbackLevels.Add(candidateLevel);

                }

            }



            fallbackLevels.Sort((left, right) => right.CompareTo(left));
            foreach (int candidateLevel in fallbackLevels)

            {

                if (TryReadMobSkillLevelVector(levelNode, candidateLevel, propertyName, out value))

                {

                    return true;

                }

            }



            return TryReadMobSkillLevelVector(levelNode, 1, propertyName, out value);

        }

        private static bool TryReadMobSkillLevelInt(WzSubProperty levelNode, int level, string propertyName, out int value)

        {

            value = 0;
            if (levelNode?[level.ToString()] is not WzSubProperty levelProperty)

            {

                return false;

            }



            WzImageProperty child = levelProperty[propertyName];
            if (child == null)

            {

                return false;

            }



            value = MapleLib.WzLib.WzStructure.InfoTool.GetInt(child, 0);
            return true;

        }



        private static long GetMobSkillEffectKey(MobItem mobItem, int currentTick)

        {

            int stateStartTime = currentTick - mobItem.AI.StateElapsed(currentTick);

            return ((long)(mobItem.PoolId & 0xFFFFFF) << 24) | (uint)stateStartTime;

        }



        /// <summary>

        /// Updates all NPC movement and action cycling

        /// </summary>

        /// <param name="gameTime"></param>

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void UpdateSocialRoomEmployeeActor(GameTime gameTime)

        {

            SocialRoomFieldActorSnapshot snapshot = GetSocialRoomEmployeeFieldActorSnapshot();
            _socialRoomEmployeeActor.Update(
                snapshot,
                _mapBoard,
                _playerManager?.Player,
                _texturePool,
                GraphicsDevice,
                UserScreenScaleFactor,
                gameTime);

        }



        private void UpdateNpcActions(GameTime gameTime)
        {

            if (_npcsArray == null || _npcsArray.Length == 0)

            {

                return;

            }



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



            if (_npcInteractionOverlay?.ContainsPoint(mouseState.X, mouseState.Y, _renderParams.RenderWidth, _renderParams.RenderHeight) == true)

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



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void HandleNpcTalkClick(MouseState mouseState)

        {

            if (!_gameState.IsPlayerInputEnabled || _npcsArray == null || _npcsArray.Length == 0)

                return;



            bool isLeftClick = mouseState.LeftButton == ButtonState.Released && _oldMouseState.LeftButton == ButtonState.Pressed;

            if (!isLeftClick)

                return;



            if (uiWindowManager?.ContainsPoint(mouseState.X, mouseState.Y) == true)

                return;



            if (_npcInteractionOverlay?.ContainsPoint(mouseState.X, mouseState.Y, _renderParams.RenderWidth, _renderParams.RenderHeight) == true)

                return;



            int mouseMapX = mouseState.X + mapShiftX - _mapBoard.CenterPoint.X;

            int mouseMapY = mouseState.Y + mapShiftY - _mapBoard.CenterPoint.Y;



            NpcItem npc = FindNpcAtMapPoint(mouseMapX, mouseMapY);

            if (npc == null || !CanTalkToNpc(npc))

                return;



            OpenNpcInteraction(npc);

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void OpenNpcInteraction(NpcItem npc, int? preferredQuestId = null)

        {

            if (npc == null || _npcInteractionOverlay == null)

                return;



            // Enter direction mode as soon as the scripted overlay opens so the

            // same click cannot continue into later world-interaction handlers.

            _gameState.EnterDirectionMode();

            _scriptedDirectionModeOwnerActive = true;

            _activeNpcInteractionNpc = npc;

            _activeNpcInteractionNpcId = int.TryParse(npc.NpcInstance?.NpcInfo?.ID, out int npcId) ? npcId : 0;



            PublishDynamicObjectTagStatesForScriptNames(

                FieldObjectNpcScriptNameResolver.ResolvePublishedScriptNames(npc.NpcInstance),

                currTickCount);

            _npcInteractionOverlay.Open(_questRuntime.BuildInteractionState(npc, _playerManager?.Player?.Build, preferredQuestId));

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void HandleNpcOverlayPrimaryAction(NpcInteractionEntry entry)

        {

            if (entry == null)

            {

                return;

            }



            if (entry.PrimaryActionKind == NpcInteractionActionKind.OpenTrunk)

            {

                _npcInteractionOverlay?.Close();

                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.Trunk);

                return;

            }



            if (entry.PrimaryActionKind == NpcInteractionActionKind.OpenItemMaker)

            {

                _npcInteractionOverlay?.Close();

                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is ItemMakerUI itemMakerWindow)

                {

                    itemMakerWindow.SetCraftingState(

                        _playerManager?.Player?.Level ?? 1,

                        _playerManager?.Player?.Build?.TraitCraft ?? 0,

                        _playerManager?.Player?.Build?.Job ?? 0,

                        GetActiveItemMakerProgression(),

                        HasItemMakerRequiredEquip,

                        MatchesItemMakerQuestRequirement);

                    itemMakerWindow.ApplyLaunchContext(entry.Subtitle);

                }



                ShowDirectionModeOwnedWindow(MapSimulatorWindowNames.ItemMaker);

                return;

            }



            if (entry.PrimaryActionKind == NpcInteractionActionKind.OpenItemUpgrade)

            {

                _npcInteractionOverlay?.Close();

                if (TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow, trackDirectionModeOwner: true))

                {

                    itemUpgradeWindow.PrepareNpcLaunch();

                }



                return;

            }



            if (entry.PrimaryActionKind != NpcInteractionActionKind.QuestPrimary ||

                entry.QuestId is not int questId ||

                _activeNpcInteractionNpcId == 0)

                return;



            QuestActionResult result = _questRuntime.TryPerformPrimaryAction(questId, _activeNpcInteractionNpcId, _playerManager?.Player?.Build);
            HandleNpcOverlayQuestActionResult(result, questId);

        }



        private bool HasItemMakerRequiredEquip(int itemId)

        {

            if (itemId <= 0)

            {

                return true;

            }



            CharacterBuild build = _playerManager?.Player?.Build;

            if (build?.Equipment == null || build.Equipment.Count == 0)

            {

                return false;

            }



            foreach (CharacterPart part in build.Equipment.Values)

            {

                if (part?.ItemId == itemId)

                {

                    return true;

                }

            }



            return false;

        }



        private void HandleItemMakerCraftCompleted(ItemMakerCraftResult result)

        {

            if (result == null)

            {

                return;

            }



            CharacterBuild build = GetActiveItemMakerCharacterBuild();

            if (build != null)

            {

                build.TraitCraft = Math.Max(0, build.TraitCraft + 1);

            }



            ItemMakerProgressionSnapshot previous = _itemMakerProgressionStore.GetSnapshot(build);

            ItemMakerProgressionSnapshot updated = _itemMakerProgressionStore.RecordCraft(build, result);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is not ItemMakerUI itemMakerWindow)

            {

                return;

            }



            int previousLevel = previous.GetLevel(result.Family);

            int updatedLevel = updated.GetLevel(result.Family);

            string statusMessage = updatedLevel > previousLevel

                ? $"Created {ResolvePickupItemName(result.CraftedItemId)} x{result.CraftedQuantity}. {updated.GetFamilyLabel(result.Family)} mastery advanced to {updatedLevel}."

                : null;



            itemMakerWindow.UpdateProgression(updated, statusMessage);

        }



        private void HandleItemMakerRecipesDiscovered(IReadOnlyCollection<int> outputItemIds)

        {

            if (outputItemIds == null || outputItemIds.Count == 0)

            {

                return;

            }



            CharacterBuild build = GetActiveItemMakerCharacterBuild();

            _itemMakerProgressionStore.RecordDiscoveredRecipes(build, outputItemIds);

        }



        private void HandleItemMakerHiddenRecipesUnlocked(IReadOnlyCollection<int> outputItemIds)

        {

            if (outputItemIds == null || outputItemIds.Count == 0)

            {

                return;

            }



            CharacterBuild build = GetActiveItemMakerCharacterBuild();

            _itemMakerProgressionStore.RecordUnlockedHiddenRecipes(build, outputItemIds);

        }



        private bool MatchesItemMakerQuestRequirement(int questId, int requiredStateValue)

        {

            if (questId <= 0)

            {

                return true;

            }



            QuestStateType currentState = _questRuntime.GetCurrentState(questId);

            return requiredStateValue switch

            {

                0 => currentState == QuestStateType.Not_Started,

                1 => currentState == QuestStateType.Started,

                2 => currentState == QuestStateType.Completed,

                // ItemMake reqQuest uses additional client-side progression flags in a few recipes.

                // Treat those as "quest has progressed" so those recipes respect the gate without

                // hard-failing on an enum value the simulator does not model separately.

                >= 3 => currentState == QuestStateType.Started || currentState == QuestStateType.Completed,

                _ => false

            };

        }



        private void ShowNpcQuestFeedback(QuestActionResult result, int currentTickCount)

        {

            if (_activeNpcInteractionNpcId == 0 || result?.Messages == null)

            {

                return;

            }



            if (!_npcQuestFeedback.Enqueue(_activeNpcInteractionNpcId, result.Messages, currentTickCount))

            {

                return;

            }



            ApplyNpcQuestFeedbackAnimation(currentTickCount);

        }



        private void UpdateNpcQuestFeedbackState(int currentTickCount)

        {

            if (!_npcQuestFeedback.Update(currentTickCount))

            {

                return;

            }



            ApplyNpcQuestFeedbackAnimation(currentTickCount);

        }



        private void UpdateNpcIdleSpeechState(int currentTickCount)

        {

            if (_npcsArray == null ||

                _npcsArray.Length == 0 ||

                _npcQuestFeedback.HasActiveBalloon ||

                _npcInteractionOverlay?.IsVisible == true ||

                _gameState.DirectionModeActive)

            {

                return;

            }



            if (_nextNpcIdleSpeechTick == 0)

            {

                _nextNpcIdleSpeechTick = currentTickCount + GetNextNpcIdleSpeechDelay();

                return;

            }



            if (currentTickCount < _nextNpcIdleSpeechTick)

            {

                return;

            }



            NpcItem npc = SelectNpcForIdleSpeech();

            _nextNpcIdleSpeechTick = currentTickCount + GetNextNpcIdleSpeechDelay();

            if (npc == null)

            {

                return;

            }



            int npcId = int.TryParse(npc.NpcInstance?.NpcInfo?.ID, out int parsedNpcId) ? parsedNpcId : 0;

            string speechLine = npc.GetNextIdleSpeechLine();

            if (npcId <= 0 || string.IsNullOrWhiteSpace(speechLine))

            {

                return;

            }



            if (_npcQuestFeedback.Enqueue(npcId, new[] { speechLine }, currentTickCount))

            {

                ApplyNpcQuestFeedbackAnimation(currentTickCount);

            }

        }



        private NpcItem SelectNpcForIdleSpeech()

        {

            if (_npcsArray == null || _playerManager?.Player == null)

            {

                return null;

            }



            var eligibleNpcs = new List<NpcItem>();

            for (int i = 0; i < _npcsArray.Length; i++)

            {

                NpcItem npc = _npcsArray[i];

                if (npc == null || !npc.HasIdleSpeech || !CanTalkToNpc(npc))

                {

                    continue;

                }



                eligibleNpcs.Add(npc);

            }



            if (eligibleNpcs.Count == 0)

            {

                return null;

            }



            return eligibleNpcs[_npcIdleSpeechRandom.Next(eligibleNpcs.Count)];

        }



        private int GetNextNpcIdleSpeechDelay()

        {

            return _npcIdleSpeechRandom.Next(12000, 22001);

        }



        private void ApplyNpcQuestFeedbackAnimation(int currentTickCount)

        {

            if (_npcQuestFeedback.ActiveNpcId == 0 || _npcsArray == null)

            {

                return;

            }



            NpcItem npc = FindNpcById(_npcQuestFeedback.ActiveNpcId);

            if (npc == null)

            {

                return;

            }



            int remainingDurationMs = Math.Max(0, _npcQuestFeedback.ActiveExpiresAt - currentTickCount);

            npc.SetTemporaryAction(AnimationKeys.Speak, remainingDurationMs);

        }



        private void UpdatePetIdleSpeechState(int currentTickCount)

        {

            if (_npcInteractionOverlay?.IsVisible == true || _gameState.DirectionModeActive)

            {

                return;

            }

        }



        private void UpdatePetEventSpeechState(int currentTickCount)

        {

            if (_playerManager?.Player == null || _playerManager.Pets?.ActivePets == null || _playerManager.Pets.ActivePets.Count == 0)

            {

                ResetPetSpeechEventState();

                return;

            }



            if (_npcInteractionOverlay?.IsVisible == true ||

                _gameState.DirectionModeActive ||

                _playerManager.Pets.GetSpeakingPets(currentTickCount).Any())

            {

                return;

            }



            PlayerCharacter player = _playerManager.Player;

            CharacterBuild build = player.Build;

            if (build == null || !player.IsAlive)

            {

                return;

            }



            int playerLevel = Math.Max(1, player.Level);

            if (_lastObservedPetSpeechLevel < 0)

            {

                _lastObservedPetSpeechLevel = playerLevel;

            }

            else if (playerLevel > _lastObservedPetSpeechLevel && TryTriggerPetSpeechEvent(PetAutoSpeechEvent.LevelUp, currentTickCount))

            {

                _lastObservedPetSpeechLevel = playerLevel;

                return;

            }



            _lastObservedPetSpeechLevel = playerLevel;



            long expToNextLevel = Math.Max(0L, build.ExpToNextLevel);

            long preLevelThreshold = expToNextLevel > 0 ? (long)Math.Ceiling(expToNextLevel * 0.9d) : long.MaxValue;

            if (expToNextLevel > 0 &&

                build.Exp >= preLevelThreshold &&

                currentTickCount >= _nextPetPreLevelSpeechTick &&

                TryTriggerPetSpeechEvent(PetAutoSpeechEvent.PreLevelUp, currentTickCount))

            {

                _nextPetPreLevelSpeechTick = currentTickCount + PetAutoSpeechPreLevelReminderCooldownMs;

                return;

            }



            int hpAlertThresholdPercent = Math.Clamp(_statusBarHpWarningThresholdPercent, 1, 99);

            int hpThreshold = Math.Max(1, (int)Math.Ceiling(player.MaxHP * (hpAlertThresholdPercent / 100f)));

            bool isLowHp = player.HP > 0 && player.HP <= hpThreshold;

            if (!isLowHp)

            {

                _petHpAlertArmed = true;

                return;

            }



            if (_petHpAlertArmed &&

                currentTickCount - _lastPetHpAlertTick >= PetAutoSpeechLowHpAlertCooldownMs &&

                TryTriggerPetSpeechEvent(PetAutoSpeechEvent.HpAlert, currentTickCount))

            {

                _lastPetHpAlertTick = currentTickCount;

                _petHpAlertArmed = false;

            }

        }



        private PetRuntime SelectPetForSpeechEvent(PetAutoSpeechEvent eventType)

        {

            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;

            if (activePets == null || activePets.Count == 0)

            {

                return null;

            }



            var eligiblePets = new List<PetRuntime>();

            for (int i = 0; i < activePets.Count; i++)

            {

                PetRuntime pet = activePets[i];

                if (pet != null && pet.CanAutoSpeak && pet.HasAutoSpeechEvent(eventType))

                {

                    eligiblePets.Add(pet);

                }

            }



            if (eligiblePets.Count == 0)

            {

                return null;

            }



            return eligiblePets[_petIdleSpeechRandom.Next(eligiblePets.Count)];

        }



        private bool TryTriggerPetSpeechEvent(PetAutoSpeechEvent eventType, int currentTickCount)

        {

            PetRuntime pet = SelectPetForSpeechEvent(eventType);

            return pet != null && pet.TryTriggerAutoSpeechEvent(eventType, currentTickCount);

        }



        private void ResetPetSpeechEventState()

        {

            _lastObservedPetSpeechLevel = -1;

            _nextPetPreLevelSpeechTick = 0;

            _lastPetHpAlertTick = int.MinValue;

            _petHpAlertArmed = true;

            _petHpPotionFailureSpeechCount = 0;

            _petMpPotionFailureSpeechCount = 0;

        }



        private void LoadNpcQuestAlertIcons(WzImage uiWindow1Image, WzImage uiWindow2Image)

        {

            WzSubProperty questIconProperty =

                (WzSubProperty)uiWindow2Image?["QuestIcon"] ??

                (WzSubProperty)uiWindow1Image?["QuestIcon"];

            if (questIconProperty == null || GraphicsDevice == null)

            {

                _npcQuestAvailableIcon = null;

                _npcQuestInProgressIcon = null;

                _npcQuestCompletableIcon = null;

                _npcQuestAlertIconsLoaded = true;

                return;

            }



            _npcQuestAvailableIcon = LoadNpcQuestAlertIcon(questIconProperty, "0");

            _npcQuestInProgressIcon = LoadNpcQuestAlertIcon(questIconProperty, "1");

            _npcQuestCompletableIcon = LoadNpcQuestAlertIcon(questIconProperty, "2");

            _npcQuestAlertIconsLoaded = true;

        }



        private void EnsureNpcQuestAlertIconsLoaded()

        {

            if (_npcQuestAlertIconsLoaded || GraphicsDevice == null)

            {

                return;

            }



            LoadNpcQuestAlertIcons(

                Program.FindImage("UI", "UIWindow.img"),

                Program.FindImage("UI", "UIWindow2.img"));

        }



        private void EnsureBossHpBarAssetsLoaded()

        {

            if (_bossHpBarAssetsLoaded)

            {

                return;

            }



            WzImage uiWindow2Image = Program.FindImage("UI", "UIWindow2.img");

            WzImage uiWindow1Image = Program.FindImage("UI", "UIWindow.img");



            if (uiWindow2Image != null)

            {

                _combatEffects.LoadBossHPBarFromWz(uiWindow2Image);

            }



            if (!_combatEffects.HasWzBossHPBar && uiWindow1Image != null)

            {

                _combatEffects.LoadBossHPBarFromWz(uiWindow1Image);

            }



            _bossHpBarAssetsLoaded = true;

        }



        private void EnsureLimitedViewFieldInitialized()

        {

            if (_limitedViewFieldInitialized)

            {

                return;

            }



            _limitedViewField.Initialize(

                _DxDeviceManager.GraphicsDevice,

                _renderParams.RenderWidth,

                _renderParams.RenderHeight);

            _limitedViewFieldInitialized = true;

        }



        private void EnsureSpineRenderer()

        {

            if (_skeletonMeshRenderer != null)

            {

                return;

            }



            _skeletonMeshRenderer = new SkeletonMeshRenderer(GraphicsDevice)

            {

                PremultipliedAlpha = false,

            };

            _skeletonMeshRenderer.Effect.World = this._matrixScale;

        }



        private Texture2D LoadNpcQuestAlertIcon(WzSubProperty questIconProperty, string iconKey)

        {

            WzSubProperty iconSubProperty = (WzSubProperty)questIconProperty?[iconKey];

            if (iconSubProperty == null)

            {

                return null;

            }



            WzCanvasProperty iconCanvas = (WzCanvasProperty)iconSubProperty["0"]

                                          ?? iconSubProperty.WzProperties.OfType<WzCanvasProperty>().FirstOrDefault();

            if (iconCanvas == null)

            {

                return null;

            }



            return iconCanvas.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(GraphicsDevice);

        }



        private void AddMesoToInventoryWindow(int amount)

        {

            if (amount <= 0 || uiWindowManager?.InventoryWindow == null)

            {

                return;

            }



            if (uiWindowManager.InventoryWindow is IInventoryRuntime inventoryWindow)

            {

                inventoryWindow.AddMeso(amount);

            }

        }



        private int GetInventoryWindowItemCount(int itemId)

        {

            if (itemId <= 0 || uiWindowManager?.InventoryWindow is not IInventoryRuntime inventoryWindow)

            {

                return 0;

            }



            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);

            return inventoryType == InventoryType.NONE

                ? 0

                : inventoryWindow.GetItemCount(inventoryType, itemId);

        }



        private bool CanAcceptInventoryWindowItem(int itemId, int quantity)

        {

            if (itemId <= 0 || quantity <= 0 || uiWindowManager?.InventoryWindow is not IInventoryRuntime inventoryWindow)

            {

                return false;

            }



            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);

            return inventoryType != InventoryType.NONE &&

                   inventoryWindow.CanAcceptItem(

                       inventoryType,

                       itemId,

                       quantity,

                       ResolveInventoryItemMaxStack(itemId, inventoryType));

        }



        private bool TryConsumeInventoryWindowItem(int itemId, int quantity)

        {

            if (itemId <= 0 || quantity <= 0 || uiWindowManager?.InventoryWindow is not IInventoryRuntime inventoryWindow)

            {

                return false;

            }



            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);

            return inventoryType != InventoryType.NONE && inventoryWindow.TryConsumeItem(inventoryType, itemId, quantity);

        }



        private bool TryAddItemToInventoryWindow(int itemId, int quantity)

        {

            if (itemId <= 0 || quantity <= 0 || !CanAcceptInventoryWindowItem(itemId, quantity))

            {

                return false;

            }



            AddItemToInventoryWindow(itemId.ToString(), quantity);

            return true;

        }

        private bool TryApplyQuestBuffItemReward(int itemId)

        {

            if (itemId <= 0 || _playerManager?.Player == null)

            {

                return false;

            }



            ConsumableItemEffect effect = ResolveConsumableItemEffect(itemId);

            bool supportsRecovery = effect.HasSupportedRecovery;

            bool supportsMovement = effect.HasSupportedMovement;

            bool supportsMorph = effect.HasSupportedMorph;

            bool supportsTemporaryBuff = effect.HasSupportedTemporaryBuff;

            bool supportsCure = effect.HasSupportedCure;

            if (!supportsRecovery && !supportsMovement && !supportsMorph && !supportsTemporaryBuff && !supportsCure)

            {

                return false;

            }



            PlayerCharacter player = _playerManager.Player;

            int hpGain = supportsRecovery

                ? ResolveConsumableRecoveryAmount(player.HP, player.MaxHP, effect.FlatHp, effect.PercentHp)

                : 0;

            int mpGain = supportsRecovery

                ? ResolveConsumableRecoveryAmount(player.MP, player.MaxMP, effect.FlatMp, effect.PercentMp)

                : 0;

            int targetMapId = supportsMovement

                ? ResolveConsumableMoveTargetMapId(effect.MoveToMapId)

                : 0;

            int morphTemplateId = supportsMorph

                ? ResolveConsumableMorphTemplateId(effect)

                : 0;



            bool canApplyTemporaryBuff = supportsTemporaryBuff && _playerManager?.Skills != null;

            bool canApplyMorph = supportsMorph

                && morphTemplateId > 0

                && player.ApplyExternalAvatarTransform(itemId, actionName: null, morphTemplateId, currTickCount + effect.DurationMs);

            bool canCureStatus = supportsCure && HasCurablePlayerMobStatus(effect);

            bool hasAnySupportedOutcome = hpGain > 0 || mpGain > 0 || supportsMovement || canApplyMorph || canApplyTemporaryBuff || canCureStatus;

            if (!hasAnySupportedOutcome)

            {

                return false;

            }



            if (hpGain > 0)

            {

                player.HP = Math.Min(player.MaxHP, player.HP + hpGain);

            }



            if (mpGain > 0)

            {

                player.MP = Math.Min(player.MaxMP, player.MP + mpGain);

            }



            if (supportsCure)

            {

                ClearCurablePlayerMobStatuses(effect);

            }



            if (canApplyTemporaryBuff)

            {

                SkillLevelData consumableBuffData = CreateConsumableBuffLevelData(effect);

                _playerManager.Skills.TryApplyConsumableBuff(

                    itemId,

                    ResolvePickupItemName(itemId),

                    ResolvePickupItemDescription(itemId),

                    consumableBuffData,

                    currTickCount);

            }



            if (!supportsMovement)

            {

                return true;

            }



            return targetMapId > 0 && _loadMapCallback != null && TryQueueConsumableMapTransfer(targetMapId);

        }



        private string ResolveQuestSkillName(int skillId)

        {

            SkillData skill = _playerManager?.SkillLoader?.LoadSkill(skillId);

            return !string.IsNullOrWhiteSpace(skill?.Name)

                ? skill.Name

                : $"Skill #{skillId}";

        }



        private void ApplyQuestSkillReward(int skillId, int targetLevel)

        {

            if (skillId <= 0 || targetLevel <= 0 || _playerManager?.Skills == null)

            {

                return;

            }



            _playerManager.Skills.SetSkillLevel(skillId, targetLevel);



            SkillData skill = _playerManager.SkillLoader?.LoadSkill(skillId);

            int maxLevel = Math.Max(targetLevel, skill?.MaxLevel ?? targetLevel);



            if (uiWindowManager?.SkillWindow is SkillUIBigBang bigBangSkillWindow)

            {

                bigBangSkillWindow.UpdateSkillLevel(skillId, targetLevel, maxLevel);

            }

        }



        private void ApplyQuestSkillMasterLevelReward(int skillId, int targetMasterLevel)

        {

            if (skillId <= 0 || targetMasterLevel <= 0 || _playerManager?.Skills == null)

            {

                return;

            }



            _playerManager.Skills.SetSkillMasterLevel(skillId, targetMasterLevel);

        }



        private void AddQuestGrantedSkillPoints(int amount)

        {

            if (amount <= 0)

            {

                return;

            }



            int tab = SkillDataLoader.GetJobAdvancementLevel(_playerManager?.Player?.Build?.Job ?? 0);

            _questGrantedSkillPointsByTab[tab] = (_questGrantedSkillPointsByTab.TryGetValue(tab, out int currentPoints) ? currentPoints : 0) + amount;

            AddQuestGrantedSkillPointsToUi(tab, amount);

        }



        private void ApplyQuestGrantedSkillPointBonuses()

        {

            foreach ((int tab, int bonus) in _questGrantedSkillPointsByTab)

            {

                AddQuestGrantedSkillPointsToUi(tab, bonus);

            }

        }



        private void AddQuestGrantedSkillPointsToUi(int tab, int amount)

        {

            if (amount == 0)

            {

                return;

            }



            if (uiWindowManager?.SkillWindow is SkillUIBigBang bigBangSkillWindow)

            {

                bigBangSkillWindow.AddSkillPoints(tab, amount);

            }



            if (uiWindowManager?.SkillWindow is SkillUI classicSkillWindow)

            {

                classicSkillWindow.AddSkillPoints(tab, amount);

            }

        }



        private void AddItemToInventoryWindow(string itemIdText, int quantity)

        {

            if (string.IsNullOrWhiteSpace(itemIdText) ||

                quantity <= 0 ||

                uiWindowManager?.InventoryWindow == null ||

                !int.TryParse(itemIdText, out int itemId))

            {

                return;

            }



            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);

            if (inventoryType == InventoryType.NONE)

            {

                return;

            }



            Texture2D itemTexture = LoadInventoryItemIcon(itemId);

            InventorySlotData slotData = new InventorySlotData

            {

                ItemId = itemId,

                ItemTexture = itemTexture,

                Quantity = Math.Max(1, quantity),

                GradeFrameIndex = inventoryType == InventoryType.EQUIP ? 0 : null,

                MaxStackSize = ResolveInventoryItemMaxStack(itemId, inventoryType)

            };



            if (uiWindowManager.InventoryWindow is InventoryUI inventoryWindow)

            {

                inventoryWindow.AddItem(inventoryType, slotData);

            }

        }



        private bool CanAcceptPickedUpItem(DropItem drop)

        {

            if (drop == null || drop.Type == Pools.DropType.Meso)

            {

                return true;

            }



            if (uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventory

                || string.IsNullOrWhiteSpace(drop.ItemId)

                || !int.TryParse(drop.ItemId, out int itemId))

            {

                return true;

            }



            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);

            if (inventoryType == InventoryType.NONE)

            {

                return true;

            }



            return inventory.CanAcceptItem(

                inventoryType,

                itemId,

                Math.Max(1, drop.Quantity),

                ResolveInventoryItemMaxStack(itemId, inventoryType));

        }



        private Pools.DropPickupFailureReason EvaluatePickupAvailability(DropItem drop)

        {

            if (drop == null)

            {

                return Pools.DropPickupFailureReason.Unavailable;

            }



            return CanAcceptPickedUpItem(drop)

                ? Pools.DropPickupFailureReason.None

                : Pools.DropPickupFailureReason.InventoryFull;

        }



        private Pools.DropPickupFailureReason EvaluatePetPickupAvailability(DropItem drop)

        {

            Pools.DropPickupFailureReason baseReason = EvaluatePickupAvailability(drop);

            if (baseReason != Pools.DropPickupFailureReason.None)

            {

                return baseReason;

            }



            return IsPetPickupBlocked(drop)

                ? Pools.DropPickupFailureReason.PetPickupBlocked

                : Pools.DropPickupFailureReason.None;

        }

        private Pools.DropPickupFailureReason EvaluateRemotePetPickupAvailability(DropItem drop)

        {

            return IsPetPickupBlocked(drop)

                ? Pools.DropPickupFailureReason.PetPickupBlocked

                : Pools.DropPickupFailureReason.None;

        }



        private string ResolvePickupSourceName(int pickerId, bool pickedByPet)

        {

            if (!pickedByPet || pickerId <= 0)

            {

                return null;

            }



            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;

            if (activePets == null)

            {

                return null;

            }



            for (int i = 0; i < activePets.Count; i++)

            {

                PetRuntime pet = activePets[i];

                if (pet?.RuntimeId == pickerId)

                {

                    return string.IsNullOrWhiteSpace(pet.Name)

                        ? $"Pet {i + 1}"

                        : pet.Name;

                }

            }



            return null;

        }

        private static int BuildRemotePetPickupActorId(int ownerCharacterId, int slotIndex)

        {

            return -((ownerCharacterId * 10) + slotIndex + 1);

        }

        private static long BuildRemotePetPickupKey(int ownerCharacterId, int slotIndex)

        {

            return ((long)ownerCharacterId << 8) | (uint)(slotIndex + 1);

        }

        private static Vector2 ResolveRemotePetPickupPosition(RemoteUserActor actor, int slotIndex)

        {

            float direction = actor.FacingRight ? -1f : 1f;

            float offsetX = direction * (28f + slotIndex * 18f);

            return new Vector2(actor.Position.X + offsetX, actor.Position.Y);

        }

        private static string ResolveRemotePetPickupName(int petItemId, int slotIndex)

        {

            string petName = ResolvePickupItemName(petItemId);

            return string.IsNullOrWhiteSpace(petName)

                ? $"Pet {slotIndex + 1}"

                : petName;

        }



        private void HandleDropPickedUp(DropItem drop, int pickerId, bool pickedByPet)

        {

            if (drop == null)

            {

                return;

            }



            PlayPickUpItemSE();

            _questRuntime.RecordDropPickup(drop);



            int currentTime = Environment.TickCount;

            string sourceName = ResolvePickupSourceName(pickerId, pickedByPet);

            PickupNoticeSource noticeSource = pickedByPet

                ? PickupNoticeSource.Pet

                : PickupNoticeSource.Player;



            if (drop.Type == Pools.DropType.Meso)

            {

                PickupNoticeSuccessMessages messages = PickupNoticeTextFormatter.FormatMesoPickup(
                    drop.MesoAmount,
                    pickedByPet,
                    sourceName);

                _pickupNoticeUI.AddFormattedNotice(
                    messages.ScreenMessage,
                    PickupMessageType.MesoPickup,
                    currentTime,
                    quantity: drop.MesoAmount);

                if (!string.IsNullOrWhiteSpace(messages.ChatMessage))
                {
                    _chat?.AddMessage(messages.ChatMessage, new Color(255, 228, 151), currentTime);
                }

                if (!string.IsNullOrWhiteSpace(messages.SecondaryScreenMessage))
                {
                    _pickupNoticeUI.AddFormattedNotice(
                        messages.SecondaryScreenMessage,
                        PickupMessageType.MesoPickup,
                        currentTime,
                        textColor: messages.SecondaryScreenColor);
                }

                AddMesoToInventoryWindow(drop.MesoAmount);

            }

            else if (drop.Type == Pools.DropType.Item || drop.Type == Pools.DropType.InstallItem)

            {

                int itemId = int.TryParse(drop.ItemId, out int parsedItemId) ? parsedItemId : 0;

                InventoryType inventoryType = itemId > 0

                    ? InventoryItemMetadataResolver.ResolveInventoryType(itemId)

                    : InventoryType.NONE;

                string itemName = itemId > 0 ? ResolvePickupItemName(itemId) : "Unknown Item";

                string itemTypeName = itemId > 0 ? ResolvePickupItemTypeName(itemId, inventoryType) : null;

                Texture2D itemIcon = itemId > 0 ? LoadInventoryItemIcon(itemId) : null;

                string screenMessage = PickupNoticeTextFormatter.FormatItemPickup(itemName, itemTypeName, drop.Quantity);

                _pickupNoticeUI.AddFormattedNotice(
                    screenMessage,
                    PickupMessageType.ItemPickup,
                    currentTime,
                    itemIcon,
                    drop.Quantity,
                    drop.IsRare ? new Color(255, 200, 100) : Color.White);

                AddItemToInventoryWindow(drop.ItemId, drop.Quantity);

                _monsterBookManager.RecordCardPickup(_playerManager?.Player?.Build, itemId, Math.Max(1, drop.Quantity));

            }

            else if (drop.Type == Pools.DropType.QuestItem)

            {

                int itemId = int.TryParse(drop.ItemId, out int parsedItemId) ? parsedItemId : 0;

                InventoryType inventoryType = itemId > 0

                    ? InventoryItemMetadataResolver.ResolveInventoryType(itemId)

                    : InventoryType.NONE;

                string itemName = itemId > 0 ? ResolvePickupItemName(itemId) : "Quest Item";

                string itemTypeName = itemId > 0 ? ResolvePickupItemTypeName(itemId, inventoryType) : null;

                Texture2D itemIcon = itemId > 0 ? LoadInventoryItemIcon(itemId) : null;

                string screenMessage = PickupNoticeTextFormatter.FormatQuestItemPickup(itemName, itemTypeName);

                _pickupNoticeUI.AddFormattedNotice(
                    screenMessage,
                    PickupMessageType.QuestItemPickup,
                    currentTime,
                    itemIcon);

            }

        }



        private void HandlePickupAttemptFailed(Pools.DropPickupAttemptResult result)

        {

            HandlePickupAttemptFailed(result, 0, false);

        }



        private void HandlePickupAttemptFailed(Pools.DropPickupAttemptResult result, int pickerId, bool pickedByPet)

        {

            if (result == null)

            {

                return;

            }



            int currentTime = Environment.TickCount;

            string sourceName = ResolvePickupSourceName(pickerId, pickedByPet);

            DropItem contextDrop = result.ContextDrop;

            string itemName = ResolvePickupResultItemName(contextDrop);

            Pools.DropType dropType = contextDrop?.Type ?? Pools.DropType.Item;

            int quantity = contextDrop != null ? Math.Max(1, contextDrop.Quantity) : 1;

            int mesoAmount = contextDrop?.MesoAmount ?? 0;

            string recentActorName = ResolveRecentPickupActorName(result.RecentPickup);

            PickupNoticeMessagePair messagePair = PickupNoticeTextFormatter.FormatFailure(

                result.FailureReason,

                itemName,

                dropType,

                quantity,

                mesoAmount,

                pickedByPet,

                sourceName,

                result.RecentPickup,

                recentActorName);



            switch (result.FailureReason)

            {

                case Pools.DropPickupFailureReason.InventoryFull:

                    AddPickupFailureMessage(messagePair, currentTime);

                    break;

                case Pools.DropPickupFailureReason.OwnershipRestricted:

                case Pools.DropPickupFailureReason.PetPickupBlocked:

                case Pools.DropPickupFailureReason.Unavailable:

                    AddPickupFailureMessage(messagePair, currentTime);

                    break;

            }

        }



        private void HandleDropPickedUpByMob(DropItem drop, int mobId)

        {

            if (drop == null || !ShouldSurfaceMobPickupNotice(drop))

            {

                return;

            }



            string mobName = ResolveMobPickupSourceName(mobId);

            string itemName = ResolvePickupResultItemName(drop);

            PickupNoticeMessagePair messages = PickupNoticeTextFormatter.FormatMobPickup(

                drop.Type,

                mobName,

                itemName,

                drop.Quantity,

                drop.MesoAmount);

            AddPickupFailureMessage(messages, Environment.TickCount);

        }



        private void HandleDropPickedUpByRemotePlayer(DropItem drop, int playerId, string playerName)



        {

            if (drop == null || !ShouldSurfaceRemotePickupNotice(drop))



            {



                return;



            }







            string itemName = ResolvePickupResultItemName(drop);



            PickupNoticeMessagePair messages = PickupNoticeTextFormatter.FormatRemotePickup(



                Pools.DropPickupActorKind.Player,



                drop.Type,



                playerName,



                itemName,



                drop.Quantity,



                drop.MesoAmount);



            AddPickupFailureMessage(messages, Environment.TickCount);



        }







        private void HandleDropPickedUpByRemotePet(DropItem drop, int petId, string petName)



        {



            if (drop == null || !ShouldSurfaceRemotePickupNotice(drop))



            {



                return;



            }







            string actorName = !string.IsNullOrWhiteSpace(petName)



                ? petName



                : $"Pet {petId}";



            string itemName = ResolvePickupResultItemName(drop);



            PickupNoticeMessagePair messages = PickupNoticeTextFormatter.FormatRemotePickup(



                Pools.DropPickupActorKind.Pet,



                drop.Type,



                actorName,



                itemName,



                drop.Quantity,



                drop.MesoAmount);



            AddPickupFailureMessage(messages, Environment.TickCount);



        }



        private void HandleDropPickedUpByRemoteOther(DropItem drop, int actorId, string actorName)



        {



            if (drop == null || !ShouldSurfaceRemotePickupNotice(drop))



            {



                return;



            }



            string resolvedActorName = !string.IsNullOrWhiteSpace(actorName)



                ? actorName



                : actorId > 0



                    ? $"Actor {actorId}"



                    : null;



            string itemName = ResolvePickupResultItemName(drop);



            PickupNoticeMessagePair messages = PickupNoticeTextFormatter.FormatRemotePickup(



                Pools.DropPickupActorKind.Other,



                drop.Type,



                resolvedActorName,



                itemName,



                drop.Quantity,



                drop.MesoAmount);



            AddPickupFailureMessage(messages, Environment.TickCount);



        }







        private bool ShouldSurfaceMobPickupNotice(DropItem drop)

        {

            if (drop == null)

            {

                return false;

            }



            int localPlayerId = _playerManager?.Player?.Build?.Id ?? 0;

            return drop.OwnerId <= 0 || localPlayerId <= 0 || drop.OwnerId == localPlayerId;

        }



        private bool ShouldSurfaceRemotePickupNotice(DropItem drop)



        {



            return ShouldSurfaceMobPickupNotice(drop);



        }







        private static string ResolveMobPickupSourceName(int mobId)

        {

            string key = mobId.ToString("D7");

            return Program.InfoManager?.MobNameCache != null

                   && Program.InfoManager.MobNameCache.TryGetValue(key, out string mobName)

                   && !string.IsNullOrWhiteSpace(mobName)

                ? mobName

                : $"Monster {mobId}";

        }



        private static string ResolvePickupResultItemName(DropItem drop)

        {

            if (drop == null)

            {

                return null;

            }



            return drop.Type == Pools.DropType.Meso

                ? null

                : int.TryParse(drop.ItemId, out int itemId)

                    ? ResolvePickupItemName(itemId)

                    : null;

        }



        private string ResolveRecentPickupActorName(Pools.RecentPickupRecord recentPickup)

        {

            if (recentPickup == null)

            {

                return null;

            }

            if (!string.IsNullOrWhiteSpace(recentPickup.ActorName))

            {

                return recentPickup.ActorName;

            }



            return recentPickup.ActorKind switch

            {

                Pools.DropPickupActorKind.Player => !string.IsNullOrWhiteSpace(recentPickup.ActorName)



                    ? recentPickup.ActorName



                    : null,



                Pools.DropPickupActorKind.Pet => ResolvePickupSourceName(recentPickup.PickerId, pickedByPet: true),

                Pools.DropPickupActorKind.Mob => ResolveMobPickupSourceName(recentPickup.PickerId),



                Pools.DropPickupActorKind.Other => !string.IsNullOrWhiteSpace(recentPickup.ActorName)



                    ? recentPickup.ActorName



                    : null,

                _ => null

            };

        }



        private void AddPickupFailureMessage(string screenMessage, string chatMessage, int currentTime)

        {

            _pickupNoticeUI.AddCantPickupMessage(screenMessage, currentTime);



            if (!string.IsNullOrWhiteSpace(chatMessage))

            {

                _chat?.AddMessage(chatMessage, new Color(255, 228, 151), currentTime);

            }

        }



        private void AddPickupFailureMessage(PickupNoticeMessagePair messagePair, int currentTime)

        {

            if (!string.IsNullOrWhiteSpace(messagePair.ScreenMessage))

            {

                _pickupNoticeUI.AddCantPickupMessage(messagePair.ScreenMessage, currentTime);

            }



            if (!string.IsNullOrWhiteSpace(messagePair.ChatMessage))

            {

                _chat?.AddMessage(messagePair.ChatMessage, new Color(255, 228, 151), currentTime);

            }

        }



        private Texture2D LoadInventoryItemIcon(int itemId)

        {

            WzSubProperty infoProperty = LoadInventoryItemInfoProperty(itemId);

            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty

                                          ?? infoProperty?["icon"] as WzCanvasProperty;

            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(GraphicsDevice);

        }



        private readonly struct ConsumableItemEffect

        {

            public int FlatHp { get; init; }

            public int FlatMp { get; init; }

            public int PercentHp { get; init; }

            public int PercentMp { get; init; }

            public int MoveToMapId { get; init; }

            public int Pad { get; init; }

            public int Mad { get; init; }

            public int Pdd { get; init; }

            public int Mdd { get; init; }

            public int Accuracy { get; init; }

            public int Avoidability { get; init; }

            public int Speed { get; init; }

            public int Jump { get; init; }

            public int IndieMaxHp { get; init; }

            public int IndieMaxMp { get; init; }

            public int MaxHpPercent { get; init; }

            public int MaxMpPercent { get; init; }

            public int DurationMs { get; init; }



            public int MorphTemplateId { get; init; }



            public int[] RandomMorphTemplateIds { get; init; }

            public bool CuresSeal { get; init; }

            public bool CuresDarkness { get; init; }

            public bool CuresWeakness { get; init; }

            public bool CuresStun { get; init; }

            public bool CuresPoison { get; init; }

            public bool CuresSlow { get; init; }

            public bool CuresFreeze { get; init; }

            public bool CuresCurse { get; init; }

            public bool CuresPainMark { get; init; }

            public bool CuresAttract { get; init; }

            public bool CuresReverseInput { get; init; }

            public bool CuresUndead { get; init; }



            public bool HasSupportedRecovery =>

                FlatHp > 0 || FlatMp > 0 || PercentHp > 0 || PercentMp > 0;



            public bool HasSupportedMovement => MoveToMapId > 0;



            public bool HasSupportedMorph =>



                DurationMs > 0 &&



                (MorphTemplateId > 0 ||



                 (RandomMorphTemplateIds != null && RandomMorphTemplateIds.Length > 0));



            public bool HasSupportedTemporaryBuff =>

                DurationMs > 0 &&

                (Pad != 0 ||

                 Mad != 0 ||

                 Pdd != 0 ||

                 Mdd != 0 ||

                 Accuracy != 0 ||

                 Avoidability != 0 ||

                 Speed != 0 ||

                 Jump != 0 ||

                 IndieMaxHp != 0 ||

                 IndieMaxMp != 0 ||

                 MaxHpPercent != 0 ||

                 MaxMpPercent != 0);



            public bool HasSupportedCure =>

                CuresSeal ||

                CuresDarkness ||

                CuresWeakness ||

                CuresStun ||

                CuresPoison ||

                CuresSlow ||

                CuresFreeze ||

                CuresCurse ||

                CuresPainMark ||

                CuresAttract ||

                CuresReverseInput ||

                CuresUndead;

        }



        private readonly struct PetFoodItemEffect



        {



            public int FullnessIncrease { get; init; }



            public int[] SupportedPetItemIds { get; init; }







            public bool IsPetFood => FullnessIncrease > 0;



        }



        private int ResolveInventoryItemMaxStack(int itemId, InventoryType inventoryType)

        {

            WzSubProperty infoProperty = LoadInventoryItemInfoProperty(itemId);

            int? slotMax = infoProperty?["slotMax"] switch

            {

                WzIntProperty intProperty => intProperty.Value,

                WzShortProperty shortProperty => shortProperty.Value,

                _ => null

            };



            return InventoryItemMetadataResolver.ResolveMaxStack(inventoryType, slotMax);

        }



        private bool IsPetPickupBlocked(DropItem drop)

        {

            if (drop == null

                || drop.Type == Pools.DropType.Meso

                || string.IsNullOrWhiteSpace(drop.ItemId)

                || !int.TryParse(drop.ItemId, out int itemId))

            {

                return false;

            }



            WzSubProperty itemProperty = LoadInventoryItemProperty(itemId);

            WzSubProperty specProperty = itemProperty?["spec"] as WzSubProperty;

            return GetWzIntValue(specProperty?["notPickupByPet"]) > 0;

        }



        private bool TryUseConsumableInventoryItem(int itemId, InventoryType inventoryType, int currentTime)
        {
            if (itemId <= 0 ||
                _playerManager?.Player == null ||
                uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventoryWindow)
            {
                return false;
            }

            string fieldItemRestrictionMessage = GetFieldItemUseRestrictionMessage(inventoryType, itemId, 1);
            if (!string.IsNullOrWhiteSpace(fieldItemRestrictionMessage))

            {

                ShowFieldRestrictionMessage(fieldItemRestrictionMessage);

                return false;

            }



            ConsumableItemEffect effect = ResolveConsumableItemEffect(itemId);

            bool supportsRecovery = effect.HasSupportedRecovery;

            bool supportsMovement = effect.HasSupportedMovement;



            bool supportsMorph = effect.HasSupportedMorph;

            bool supportsTemporaryBuff = effect.HasSupportedTemporaryBuff;

            bool supportsCure = effect.HasSupportedCure;

            if (!supportsRecovery && !supportsMovement && !supportsMorph && !supportsTemporaryBuff && !supportsCure)

            {

                return false;

            }



            int availableCount = inventoryWindow.GetItemCount(inventoryType, itemId);
            if (availableCount <= 0)

            {

                TryTriggerPetPotionFailureSpeech(effect, currentTime);

                return false;

            }



            PlayerCharacter player = _playerManager.Player;

            int hpGain = supportsRecovery

                ? ResolveConsumableRecoveryAmount(player.HP, player.MaxHP, effect.FlatHp, effect.PercentHp)

                : 0;

            int mpGain = supportsRecovery

                ? ResolveConsumableRecoveryAmount(player.MP, player.MaxMP, effect.FlatMp, effect.PercentMp)

                : 0;

            int targetMapId = supportsMovement

                ? ResolveConsumableMoveTargetMapId(effect.MoveToMapId)

                : 0;

            int morphTemplateId = supportsMorph



                ? ResolveConsumableMorphTemplateId(effect)



                : 0;



            bool canApplyTemporaryBuff = supportsTemporaryBuff && _playerManager?.Skills != null;



            bool canApplyMorph = supportsMorph



                && morphTemplateId > 0



                && player.ApplyExternalAvatarTransform(itemId, actionName: null, morphTemplateId, currentTime + effect.DurationMs);

            bool canCureStatus = supportsCure && HasCurablePlayerMobStatus(effect);

            bool hasAnySupportedOutcome = hpGain > 0 || mpGain > 0 || supportsMovement || canApplyMorph || canApplyTemporaryBuff || canCureStatus;

            if (!hasAnySupportedOutcome)

            {

                return false;

            }



            if (!inventoryWindow.TryConsumeItem(inventoryType, itemId, 1))
            {

                TryTriggerPetPotionFailureSpeech(effect, currentTime);

                return false;

            }



            if (hpGain > 0)

            {

                player.HP = Math.Min(player.MaxHP, player.HP + hpGain);

                _petHpPotionFailureSpeechCount = 0;

            }



            if (mpGain > 0)

            {

                player.MP = Math.Min(player.MaxMP, player.MP + mpGain);

                _petMpPotionFailureSpeechCount = 0;

            }



            if (supportsCure)

            {

                ClearCurablePlayerMobStatuses(effect);

            }



            if (canApplyTemporaryBuff)

            {

                SkillLevelData consumableBuffData = CreateConsumableBuffLevelData(effect);

                _playerManager.Skills.TryApplyConsumableBuff(

                    itemId,

                    ResolvePickupItemName(itemId),

                    ResolvePickupItemDescription(itemId),

                    consumableBuffData,

                    currentTime);

            }



            if (!supportsMovement)

            {

                _fieldRuleRuntime?.RegisterSuccessfulItemUse(inventoryType, currentTime);
                return true;
            }

            if (targetMapId <= 0 || _loadMapCallback == null || !TryQueueConsumableMapTransfer(targetMapId))
            {
                inventoryWindow.AddItem(inventoryType, new InventorySlotData
                {
                    ItemId = itemId,
                    ItemTexture = inventoryWindow.GetItemTexture(inventoryType, itemId) ?? LoadInventoryItemIcon(itemId),
                    Quantity = 1,
                    MaxStackSize = ResolveInventoryItemMaxStack(itemId, inventoryType),
                    ItemName = ResolvePickupItemName(itemId),
                    ItemTypeName = ResolvePickupItemTypeName(itemId, inventoryType),
                    Description = ResolvePickupItemDescription(itemId)
                });
                return false;
            }

            _fieldRuleRuntime?.RegisterSuccessfulItemUse(inventoryType, currentTime);
            return true;
        }


        private PetFoodItemEffect ResolvePetFoodItemEffect(int itemId)



        {



            WzSubProperty itemProperty = LoadInventoryItemProperty(itemId);



            WzSubProperty specProperty = itemProperty?["spec"] as WzSubProperty;



            if (specProperty == null)



            {



                return default;



            }







            int fullnessIncrease = Math.Max(0, GetWzIntValue(specProperty["inc"]));



            if (fullnessIncrease <= 0)



            {



                return default;



            }







            int[] supportedPetItemIds = specProperty.WzProperties



                .Where(property => int.TryParse(property.Name, out _))



                .Select(property => Math.Max(0, GetWzIntValue(property)))



                .Where(value => value > 0)



                .Distinct()



                .ToArray();







            return new PetFoodItemEffect



            {



                FullnessIncrease = fullnessIncrease,



                SupportedPetItemIds = supportedPetItemIds



            };



        }



        private ConsumableItemEffect ResolveConsumableItemEffect(int itemId)

        {

            WzSubProperty itemProperty = LoadInventoryItemProperty(itemId);

            WzSubProperty specProperty = itemProperty?["spec"] as WzSubProperty;

            if (specProperty == null)

            {

                return default;

            }



            return new ConsumableItemEffect

            {

                FlatHp = Math.Max(0, GetWzIntValue(specProperty["hp"])),

                FlatMp = Math.Max(0, GetWzIntValue(specProperty["mp"])),

                PercentHp = ResolveConsumablePercentValue(specProperty, "hpR", "hpRatio", "hpPer"),

                PercentMp = ResolveConsumablePercentValue(specProperty, "mpR", "mpRatio", "mpPer"),

                MoveToMapId = Math.Max(0, GetWzIntValue(specProperty["moveTo"])),

                Pad = ResolveConsumableIntValue(specProperty, "pad", "indiePad"),

                Mad = ResolveConsumableIntValue(specProperty, "mad", "indieMad"),

                Pdd = ResolveConsumableIntValue(specProperty, "pdd", "indiePdd"),

                Mdd = ResolveConsumableIntValue(specProperty, "mdd", "indieMdd"),

                Accuracy = ResolveConsumableIntValue(specProperty, "acc"),

                Avoidability = ResolveConsumableIntValue(specProperty, "eva"),

                Speed = ResolveConsumableIntValue(specProperty, "speed", "indieSpeed"),

                Jump = ResolveConsumableIntValue(specProperty, "jump", "indieJump"),

                IndieMaxHp = ResolveConsumableIntValue(specProperty, "indieMhp"),

                IndieMaxMp = ResolveConsumableIntValue(specProperty, "indieMmp"),

                MaxHpPercent = ResolveConsumablePercentValue(specProperty, "mhpR", "mhpRRate"),

                MaxMpPercent = ResolveConsumablePercentValue(specProperty, "mmpR", "mmpRRate"),

                DurationMs = Math.Max(0, GetWzIntValue(specProperty["time"])),



                MorphTemplateId = Math.Max(0, GetWzIntValue(specProperty["morph"])),



                RandomMorphTemplateIds = ResolveConsumableRandomMorphTemplateIds(specProperty["morphRandom"] as WzSubProperty),

                CuresSeal = GetWzIntValue(specProperty["seal"]) > 0,

                CuresDarkness = GetWzIntValue(specProperty["darkness"]) > 0,

                CuresWeakness = GetWzIntValue(specProperty["weakness"]) > 0,

                CuresStun = GetWzIntValue(specProperty["stun"]) > 0,

                CuresPoison = GetWzIntValue(specProperty["poison"]) > 0,

                CuresSlow = GetWzIntValue(specProperty["slow"]) > 0,

                CuresFreeze = GetWzIntValue(specProperty["freeze"]) > 0,

                CuresCurse = GetWzIntValue(specProperty["curse"]) > 0,

                CuresPainMark = GetWzIntValue(specProperty["painmark"]) > 0,

                CuresAttract = GetWzIntValue(specProperty["seduce"]) > 0 || GetWzIntValue(specProperty["attract"]) > 0,

                CuresReverseInput = GetWzIntValue(specProperty["confusion"]) > 0 || GetWzIntValue(specProperty["reverseInput"]) > 0,

                CuresUndead = GetWzIntValue(specProperty["undead"]) > 0 || GetWzIntValue(specProperty["zombie"]) > 0

            };

        }



        private bool HasCurablePlayerMobStatus(ConsumableItemEffect effect)

        {

            if (_playerManager?.Player == null)

            {

                return false;

            }



            foreach (PlayerMobStatusEffect status in EnumerateCurablePlayerMobStatuses(effect))

            {

                if (_playerManager.HasMobStatus(status))

                {

                    return true;

                }

            }



            return false;

        }



        private void ClearCurablePlayerMobStatuses(ConsumableItemEffect effect)

        {

            if (_playerManager == null)

            {

                return;

            }



            _playerManager.ClearMobStatuses(EnumerateCurablePlayerMobStatuses(effect));

        }



        private static IEnumerable<PlayerMobStatusEffect> EnumerateCurablePlayerMobStatuses(ConsumableItemEffect effect)

        {

            if (effect.CuresSeal)

            {

                yield return PlayerMobStatusEffect.Seal;

            }



            if (effect.CuresDarkness)

            {

                yield return PlayerMobStatusEffect.Darkness;

            }



            if (effect.CuresWeakness)

            {

                yield return PlayerMobStatusEffect.Weakness;

            }



            if (effect.CuresStun)

            {

                yield return PlayerMobStatusEffect.Stun;

            }



            if (effect.CuresPoison)

            {

                yield return PlayerMobStatusEffect.Poison;

            }



            if (effect.CuresSlow)

            {

                yield return PlayerMobStatusEffect.Slow;

            }



            if (effect.CuresFreeze)

            {

                yield return PlayerMobStatusEffect.Freeze;

            }



            if (effect.CuresCurse)

            {

                yield return PlayerMobStatusEffect.Curse;

            }



            if (effect.CuresPainMark)

            {

                yield return PlayerMobStatusEffect.PainMark;

            }



            if (effect.CuresAttract)

            {

                yield return PlayerMobStatusEffect.Attract;

            }



            if (effect.CuresReverseInput)

            {

                yield return PlayerMobStatusEffect.ReverseInput;

            }



            if (effect.CuresUndead)

            {

                yield return PlayerMobStatusEffect.Undead;

            }

        }



        private static SkillLevelData CreateConsumableBuffLevelData(ConsumableItemEffect effect)

        {

            return new SkillLevelData

            {

                Level = 1,

                Time = Math.Max(1, effect.DurationMs / 1000),

                PAD = effect.Pad,

                MAD = effect.Mad,

                PDD = effect.Pdd,

                MDD = effect.Mdd,

                ACC = effect.Accuracy,

                EVA = effect.Avoidability,

                Speed = effect.Speed,

                Jump = effect.Jump,

                IndieMaxHP = effect.IndieMaxHp,

                IndieMaxMP = effect.IndieMaxMp,

                MaxHPPercent = effect.MaxHpPercent,

                MaxMPPercent = effect.MaxMpPercent

            };

        }



        private bool TryQueueConsumableMapTransfer(int targetMapId)

        {

            string transferRestrictionMessage = FieldInteractionRestrictionEvaluator.GetMapTransferRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0);

            if (!string.IsNullOrWhiteSpace(transferRestrictionMessage))

            {

                ShowFieldRestrictionMessage(transferRestrictionMessage);

                return false;

            }



            return QueueMapTransfer(targetMapId, null);

        }



        private int ResolveConsumableMoveTargetMapId(int moveToMapId)

        {

            if (moveToMapId <= 0)

            {

                return 0;

            }



            return moveToMapId == 999999999

                ? ResolveNearestTownMapId()

                : moveToMapId;

        }



        private int ResolveNearestTownMapId()

        {

            if (_mapBoard?.MapInfo?.returnMap is int returnMapId &&

                returnMapId > 0 &&

                returnMapId != MapConstants.MaxMap)

            {

                return returnMapId;

            }



            if (_mapBoard?.MapInfo?.forcedReturn is int forcedReturnId &&

                forcedReturnId > 0 &&

                forcedReturnId != MapConstants.MaxMap)

            {

                return forcedReturnId;

            }



            return 0;

        }



        private static int[] ResolveConsumableRandomMorphTemplateIds(WzSubProperty morphRandomProperty)



        {



            if (morphRandomProperty == null)



            {



                return Array.Empty<int>();



            }







            List<int> weightedTemplateIds = new();



            foreach (WzSubProperty morphEntry in morphRandomProperty.WzProperties.OfType<WzSubProperty>())



            {



                int morphTemplateId = Math.Max(0, GetWzIntValue(morphEntry["morph"]));



                if (morphTemplateId <= 0)



                {



                    continue;



                }







                int weight = Math.Max(1, GetWzIntValue(morphEntry["prop"]));



                for (int i = 0; i < weight; i++)



                {



                    weightedTemplateIds.Add(morphTemplateId);



                }



            }







            return weightedTemplateIds.ToArray();



        }







        private static int ResolveConsumableMorphTemplateId(ConsumableItemEffect effect)



        {



            if (effect.MorphTemplateId > 0)



            {



                return effect.MorphTemplateId;



            }







            if (effect.RandomMorphTemplateIds == null || effect.RandomMorphTemplateIds.Length == 0)



            {



                return 0;



            }







            int randomIndex = Random.Shared.Next(effect.RandomMorphTemplateIds.Length);



            return effect.RandomMorphTemplateIds[randomIndex];



        }







        private static int ResolveConsumablePercentValue(WzSubProperty specProperty, params string[] propertyNames)

        {

            if (specProperty == null || propertyNames == null)

            {

                return 0;

            }



            for (int i = 0; i < propertyNames.Length; i++)

            {

                int value = Math.Max(0, GetWzIntValue(specProperty[propertyNames[i]]));

                if (value > 0)

                {

                    return value;

                }

            }



            return 0;

        }

        private static int ResolveConsumableIntValue(WzSubProperty specProperty, params string[] propertyNames)

        {

            if (specProperty == null || propertyNames == null)

            {

                return 0;

            }



            for (int i = 0; i < propertyNames.Length; i++)

            {

                int value = GetWzIntValue(specProperty[propertyNames[i]]);

                if (value != 0)

                {

                    return value;

                }

            }



            return 0;

        }



        private static int ResolveConsumableRecoveryAmount(int current, int max, int flatAmount, int percentAmount)

        {

            if (max <= 0 || current >= max)

            {

                return 0;

            }



            int percentRecovery = percentAmount > 0

                ? (int)Math.Ceiling(max * (percentAmount / 100f))

                : 0;

            return Math.Max(0, flatAmount + percentRecovery);

        }



        private void TryTriggerPetPotionFailureSpeech(ConsumableItemEffect effect, int currentTime)

        {

            if (effect.FlatHp > 0 || effect.PercentHp > 0)

            {

                TryTriggerLimitedPetSpeechEvent(

                    PetAutoSpeechEvent.NoHpPotion,

                    ref _petHpPotionFailureSpeechCount,

                    currentTime);

            }



            if (effect.FlatMp > 0 || effect.PercentMp > 0)

            {

                TryTriggerLimitedPetSpeechEvent(

                    PetAutoSpeechEvent.NoMpPotion,

                    ref _petMpPotionFailureSpeechCount,

                    currentTime);

            }

        }



        private void TryTriggerLimitedPetSpeechEvent(PetAutoSpeechEvent eventType, ref int failureCount, int currentTime)

        {

            failureCount++;

            if (failureCount <= PetAutoSpeechPotionFailureMaxRepeats)

            {

                TryTriggerPetSpeechEvent(eventType, currentTime);

            }

        }



        private WzSubProperty LoadInventoryItemInfoProperty(int itemId)

        {

            WzSubProperty itemProperty = LoadInventoryItemProperty(itemId);

            return itemProperty?["info"] as WzSubProperty;

        }



        private WzSubProperty LoadInventoryItemProperty(int itemId)

        {

            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))

            {

                return null;

            }



            WzImage itemImage = Program.FindImage(category, imagePath);

            if (itemImage == null)

            {

                return null;

            }



            itemImage.ParseImage();

            string itemText = category == "Character" ? itemId.ToString("D8") : itemId.ToString("D7");

            return itemImage[itemText] as WzSubProperty;

        }



        internal static int GetWzIntValue(WzImageProperty property)

        {

            return property switch

            {

                WzIntProperty intProperty => intProperty.Value,

                WzShortProperty shortProperty => shortProperty.Value,

                WzLongProperty longProperty => (int)longProperty.Value,

                WzStringProperty stringProperty when int.TryParse(

                    stringProperty.Value,

                    NumberStyles.Integer,

                    CultureInfo.InvariantCulture,

                    out int parsedValue) => parsedValue,

                _ => 0

            };

        }



        private static string ResolvePickupItemName(int itemId)

        {

            return Program.InfoManager?.ItemNameCache != null

                   && Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)

                   && !string.IsNullOrWhiteSpace(itemInfo.Item2)

                ? itemInfo.Item2

                : $"Item #{itemId}";

        }



        private static string ResolvePickupItemTypeName(int itemId, InventoryType inventoryType)

        {

            if (Program.InfoManager?.ItemNameCache != null

                && Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)

                && !string.IsNullOrWhiteSpace(itemInfo.Item1))

            {

                return itemInfo.Item1;

            }



            return inventoryType switch

            {

                InventoryType.EQUIP => "equipment item",

                InventoryType.USE => "use item",

                InventoryType.SETUP => "setup item",

                InventoryType.ETC => "etc item",

                InventoryType.CASH => "cash item",

                _ => "item"

            };

        }



        private static string ResolvePickupItemDescription(int itemId)

        {

            return InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string description)

                && !string.IsNullOrWhiteSpace(description)

                ? description

                : string.Empty;

        }



        private void DrawNpcQuestAlerts(in Managers.RenderContext renderContext)

        {

            if (_npcsArray == null || _playerManager?.Player?.Build == null)

            {

                return;

            }



            EnsureNpcQuestAlertIconsLoaded();



            for (int i = 0; i < _npcsArray.Length; i++)

            {

                NpcItem npc = _npcsArray[i];

                if (npc == null)

                {

                    continue;

                }



                NpcInteractionEntryKind? alertKind = _questRuntime.GetNpcQuestAlertKind(npc, _playerManager.Player.Build);

                Texture2D alertTexture = GetNpcQuestAlertTexture(alertKind);

                if (alertTexture == null)

                {

                    continue;

                }



                IDXObject currentFrame = npc.GetCurrentFrame();

                int npcTop = npc.CurrentY - (currentFrame?.Height ?? npc.NpcInstance.Height);

                int screenX = npc.CurrentX - renderContext.MapShiftX + renderContext.MapCenterX - (alertTexture.Width / 2);

                int screenY = npcTop - renderContext.MapShiftY + renderContext.MapCenterY - alertTexture.Height - 10;

                screenY += ((renderContext.TickCount / 180) % 2 == 0) ? 0 : -2;



                if (screenX + alertTexture.Width < 0 ||

                    screenY + alertTexture.Height < 0 ||

                    screenX > renderContext.RenderParams.RenderWidth ||

                    screenY > renderContext.RenderParams.RenderHeight)

                {

                    continue;

                }



                _spriteBatch.Draw(alertTexture, new Vector2(screenX, screenY), Color.White);

            }

        }



        private void DrawNpcQuestFeedback(in Managers.RenderContext renderContext)

        {

            if (_npcQuestFeedback.ActiveNpcId == 0 ||

                string.IsNullOrWhiteSpace(_npcQuestFeedback.ActiveText) ||

                _fontChat == null ||

                _debugBoundaryTexture == null ||

                _npcsArray == null)

            {

                return;

            }



            NpcItem npc = FindNpcById(_npcQuestFeedback.ActiveNpcId);

            if (npc == null)

            {

                return;

            }



            IDXObject currentFrame = npc.GetCurrentFrame();

            int npcTop = npc.CurrentY - (currentFrame?.Height ?? npc.NpcInstance.Height);

            Vector2 textSize = MeasureChatTextWithFallback(_npcQuestFeedback.ActiveText);

            int boxWidth = (int)Math.Ceiling(textSize.X) + 18;

            int boxHeight = (int)Math.Ceiling(textSize.Y) + 12;

            int boxX = npc.CurrentX - renderContext.MapShiftX + renderContext.MapCenterX - (boxWidth / 2);

            int boxY = npcTop - renderContext.MapShiftY + renderContext.MapCenterY - boxHeight - 34;



            if (boxX + boxWidth < 0 ||

                boxY + boxHeight < 0 ||

                boxX > renderContext.RenderParams.RenderWidth ||

                boxY > renderContext.RenderParams.RenderHeight)

            {

                return;

            }



            float remainingAlpha = MathHelper.Clamp((_npcQuestFeedback.ActiveExpiresAt - renderContext.TickCount) / 400f, 0f, 1f);

            Color backgroundColor = new Color(24, 34, 28) * (0.88f * remainingAlpha);

            Color borderColor = new Color(243, 229, 170) * remainingAlpha;

            Color textColor = new Color(255, 246, 214) * remainingAlpha;



            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, boxWidth, boxHeight), backgroundColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, boxWidth, 2), borderColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY + boxHeight - 2, boxWidth, 2), borderColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, 2, boxHeight), borderColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX + boxWidth - 2, boxY, 2, boxHeight), borderColor);

            DrawChatTextWithFallback(_npcQuestFeedback.ActiveText, new Vector2(boxX + 9, boxY + 6), textColor);

        }



        private void DrawPetIdleSpeechFeedback(in Managers.RenderContext renderContext)

        {

            if (_fontChat == null ||

                _debugBoundaryTexture == null)

            {

                return;

            }



            IEnumerable<PetRuntime> speakingPets = _playerManager?.Pets?.GetSpeakingPets(renderContext.TickCount);

            if (speakingPets == null)

            {

                return;

            }



            foreach (PetRuntime pet in speakingPets)

            {

                DrawPetSpeechBalloon(pet, renderContext);

            }

        }



        private void DrawPetSpeechBalloon(PetRuntime pet, in Managers.RenderContext renderContext)

        {

            if (pet == null ||

                !pet.HasActiveSpeech ||

                _fontChat == null ||

                _debugBoundaryTexture == null)

            {

                return;

            }



            IDXObject currentFrame = pet.GetCurrentFrame();

            int petTop = (int)pet.Y - (currentFrame?.Height ?? 40);

            Vector2 textSize = MeasureChatTextWithFallback(pet.ActiveSpeechText);

            int boxWidth = (int)Math.Ceiling(textSize.X) + 18;

            int boxHeight = (int)Math.Ceiling(textSize.Y) + 12;

            int boxX = (int)pet.X - renderContext.MapShiftX + renderContext.MapCenterX - (boxWidth / 2);

            int boxY = petTop - renderContext.MapShiftY + renderContext.MapCenterY - boxHeight - 24;



            if (boxX + boxWidth < 0 ||

                boxY + boxHeight < 0 ||

                boxX > renderContext.RenderParams.RenderWidth ||

                boxY > renderContext.RenderParams.RenderHeight)

            {

                return;

            }



            float remainingAlpha = MathHelper.Clamp((pet.ActiveSpeechExpiresAt - renderContext.TickCount) / 400f, 0f, 1f);

            Color backgroundColor = new Color(30, 31, 45) * (0.88f * remainingAlpha);

            Color borderColor = new Color(176, 223, 255) * remainingAlpha;

            Color textColor = new Color(242, 249, 255) * remainingAlpha;



            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, boxWidth, boxHeight), backgroundColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, boxWidth, 2), borderColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY + boxHeight - 2, boxWidth, 2), borderColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX, boxY, 2, boxHeight), borderColor);

            _spriteBatch.Draw(_debugBoundaryTexture, new Rectangle(boxX + boxWidth - 2, boxY, 2, boxHeight), borderColor);

            DrawChatTextWithFallback(pet.ActiveSpeechText, new Vector2(boxX + 9, boxY + 6), textColor);

        }



        private Vector2 MeasureChatTextWithFallback(string text)

        {

            if (_fontChat == null || string.IsNullOrEmpty(text))

            {

                return Vector2.Zero;

            }



            string normalizedText = NormalizeSpriteFontPunctuation(text);

            return ContainsUnsupportedChatGlyphs(normalizedText)

                ? MeasureFallbackChatText(normalizedText)

                : _fontChat.MeasureString(normalizedText);

        }



        private void DrawChatTextWithFallback(string text, Vector2 position, Color color)

        {

            if (_fontChat == null || string.IsNullOrEmpty(text))

            {

                return;

            }



            string normalizedText = NormalizeSpriteFontPunctuation(text);

            if (!ContainsUnsupportedChatGlyphs(normalizedText))

            {

                _spriteBatch.DrawString(_fontChat, normalizedText, position, color);

                return;

            }



            Texture2D texture = GetOrCreateChatFallbackTexture(normalizedText);

            if (texture != null)

            {

                _spriteBatch.Draw(texture, position, color);

            }

        }



        private Texture2D GetOrCreateChatFallbackTexture(string text)

        {

            if (string.IsNullOrEmpty(text))

            {

                return null;

            }



            if (_chatFallbackTextureCache.TryGetValue(text, out Texture2D cachedTexture) &&

                cachedTexture != null &&

                !cachedTexture.IsDisposed)

            {

                return cachedTexture;

            }



            Vector2 size = MeasureFallbackChatText(text);

            int width = Math.Max(1, (int)Math.Ceiling(size.X));

            int height = Math.Max(1, Math.Max((int)Math.Ceiling(size.Y), (int)Math.Ceiling(_chatFallbackLineHeight)));



            using var bitmap = new SD.Bitmap(width, height);

            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);

            graphics.Clear(SD.Color.Transparent);

            graphics.TextRenderingHint = SDText.TextRenderingHint.AntiAliasGridFit;

            using var brush = new SD.SolidBrush(SD.Color.White);

            graphics.DrawString(text, _chatFallbackFont, brush, 0f, 0f, SD.StringFormat.GenericTypographic);



            Texture2D texture = bitmap.ToTexture2D(GraphicsDevice);

            _chatFallbackTextureCache[text] = texture;

            return texture;

        }



        private Vector2 MeasureFallbackChatText(string text)

        {

            if (string.IsNullOrEmpty(text))

            {

                return Vector2.Zero;

            }



            SD.SizeF size = _chatFallbackMeasureGraphics.MeasureString(text, _chatFallbackFont, SD.PointF.Empty, SD.StringFormat.GenericTypographic);

            if (size.Width <= 0f || size.Height <= 0f)

            {

                size = _chatFallbackMeasureGraphics.MeasureString(text, _chatFallbackFont);

            }



            return new Vector2((float)Math.Ceiling(size.Width), (float)Math.Ceiling(size.Height));

        }



        private bool ContainsUnsupportedChatGlyphs(string text)

        {

            if (_fontChat == null || string.IsNullOrEmpty(text))

            {

                return false;

            }



            IReadOnlyList<char> supportedCharacters = _fontChat.Characters;

            if (supportedCharacters == null)

            {

                return false;

            }



            for (int i = 0; i < text.Length; i++)

            {

                bool found = false;

                for (int j = 0; j < supportedCharacters.Count; j++)

                {

                    if (supportedCharacters[j] == text[i])

                    {

                        found = true;

                        break;

                    }

                }



                if (!found)

                {

                    return true;

                }

            }



            return false;

        }



        private static string NormalizeSpriteFontPunctuation(string text)

        {

            if (string.IsNullOrEmpty(text))

            {

                return text ?? string.Empty;

            }



            var builder = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)

            {

                builder.Append(text[i] switch

                {

                    '\u2018' => '\'',

                    '\u2019' => '\'',

                    '\u201A' => '\'',

                    '\u201B' => '\'',

                    '\u201C' => '"',

                    '\u201D' => '"',

                    '\u201E' => '"',

                    '\u201F' => '"',

                    '\u2032' => '\'',

                    '\u2033' => '"',

                    '\u00B4' => '\'',

                    '\u0060' => '\'',

                    '\u2013' => '-',

                    '\u2014' => '-',

                    '\u2212' => '-',

                    '\u00A0' => ' ',

                    _ => text[i]

                });

            }



            return builder.ToString();

        }



        private Texture2D GetNpcQuestAlertTexture(NpcInteractionEntryKind? alertKind)

        {

            return alertKind switch

            {

                NpcInteractionEntryKind.AvailableQuest => _npcQuestAvailableIcon,

                NpcInteractionEntryKind.InProgressQuest => _npcQuestInProgressIcon,

                NpcInteractionEntryKind.CompletableQuest => _npcQuestCompletableIcon,

                _ => null

            };

        }



        private MinimapUI.NpcMarkerType ResolveMinimapNpcMarkerType(NpcItem npc)

        {

            if (npc == null || _questRuntime == null || _playerManager?.Player?.Build == null)

            {

                return MinimapUI.NpcMarkerType.Default;

            }



            NpcInteractionEntryKind? alertKind = _questRuntime.GetNpcQuestAlertKind(npc, _playerManager.Player.Build);

            return alertKind switch

            {

                NpcInteractionEntryKind.AvailableQuest => MinimapUI.NpcMarkerType.QuestStart,

                NpcInteractionEntryKind.InProgressQuest => MinimapUI.NpcMarkerType.QuestEnd,

                NpcInteractionEntryKind.CompletableQuest => MinimapUI.NpcMarkerType.QuestEnd,

                _ => MinimapUI.NpcMarkerType.Default

            };

        }

        private void EnsureMinimapTooltipResources()

        {

            if (miniMapUi == null || _fontChat == null || GraphicsDevice == null)

            {

                return;

            }



            if (_minimapTooltipPixelTexture == null)

            {

                _minimapTooltipPixelTexture = new Texture2D(GraphicsDevice, 1, 1);

                _minimapTooltipPixelTexture.SetData(new[] { Color.White });

            }



            miniMapUi.SetTooltipResources(_fontChat, _minimapTooltipPixelTexture);

        }



        private string ResolveMinimapNpcTooltipText(NpcItem npc)

        {

            if (npc?.NpcInstance == null)

            {

                return null;

            }



            string npcName = npc.NpcInstance.NpcInfo?.StringName;

            if (string.IsNullOrWhiteSpace(npcName))

            {

                npcName = npc.NpcInstance.NpcInfo?.ID;

            }



            string questState = ResolveMinimapNpcQuestStateText(npc);

            return BuildMinimapTooltipText(npcName, questState);

        }



        private string ResolveMinimapPortalTooltipText(PortalItem portal)

        {

            PortalInstance instance = portal?.PortalInstance;

            if (instance == null || instance.hideTooltip == MapleBool.True)

            {

                return null;

            }



            string scriptText = string.IsNullOrWhiteSpace(instance.script) ? null : instance.script.Trim();
            string targetMapText = instance.tm > 0 && instance.tm != MapConstants.MaxMap
                ? ResolveMapTransferDisplayName(instance.tm)
                : null;
            string portalNameText = string.IsNullOrWhiteSpace(instance.pn) ? null : $"Portal: {instance.pn.Trim()}";
            string targetPortalText = string.IsNullOrWhiteSpace(instance.tn) ? null : $"Target: {instance.tn.Trim()}";

            return BuildMinimapTooltipText(
                scriptText,
                targetMapText,
                portalNameText,
                targetPortalText);

        }



        private string ResolveMinimapNpcQuestStateText(NpcItem npc)

        {

            if (npc == null || _questRuntime == null || _playerManager?.Player?.Build == null)

            {

                return null;

            }



            NpcInteractionEntryKind? alertKind = _questRuntime.GetNpcQuestAlertKind(npc, _playerManager.Player.Build);

            return alertKind switch

            {

                NpcInteractionEntryKind.AvailableQuest => "Quest available",

                NpcInteractionEntryKind.InProgressQuest => "Quest in progress",

                NpcInteractionEntryKind.CompletableQuest => "Quest complete",

                _ => null

            };

        }



        private static string BuildMinimapTooltipText(params string[] lines)

        {

            if (lines == null || lines.Length == 0)

            {

                return null;

            }



            List<string> normalizedLines = new(lines.Length);
            foreach (string line in lines)

            {

                string normalized = NormalizeSpriteFontPunctuation(line)?.Trim();
                if (!string.IsNullOrWhiteSpace(normalized) && !normalizedLines.Contains(normalized, StringComparer.Ordinal))

                {

                    normalizedLines.Add(normalized);

                }

            }



            return normalizedLines.Count == 0 ? null : string.Join(Environment.NewLine, normalizedLines);

        }



        private static string BuildMinimapTrackedUserTooltipText(MinimapTrackedUserState state)

        {

            if (state == null)

            {

                return null;

            }



            List<string> relationshipLines = new();
            if (state.IsTrader)

            {

                relationshipLines.Add("Trader");

            }

            if (state.IsMatchParticipant)

            {

                relationshipLines.Add("Match Cards participant");

            }

            if (state.IsPartyMember)

            {

                relationshipLines.Add(state.IsPartyLeader ? "Party leader" : "Party member");

            }

            if (state.IsGuildMember)

            {

                relationshipLines.Add(state.IsGuildLeader ? "Guild leader" : "Guild member");

            }

            if (state.IsFriend)

            {

                relationshipLines.Add("Friend");

            }



            return BuildMinimapTooltipText(
                state.Name,
                relationshipLines.Count > 0 ? string.Join(", ", relationshipLines) : "Remote user");

        }



        private IReadOnlyList<MinimapUI.EmployeeMarker> BuildMinimapEmployeeMarkers(PlayerCharacter player)

        {

            SocialRoomFieldActorSnapshot snapshot = GetSocialRoomEmployeeFieldActorSnapshot();
            if (snapshot == null || player == null)

            {

                return Array.Empty<MinimapUI.EmployeeMarker>();

            }



            int worldX;
            int worldY;
            if (snapshot.HasWorldPosition && !snapshot.UseOwnerAnchor)

            {

                worldX = snapshot.WorldX;

                worldY = snapshot.WorldY;

            }

            else

            {

                int horizontalOffset = player.FacingRight ? snapshot.AnchorOffsetX : -snapshot.AnchorOffsetX;
                worldX = (int)Math.Round(player.Position.X) + horizontalOffset;
                worldY = (int)Math.Round(player.Position.Y) + snapshot.AnchorOffsetY;

            }



            string tooltipText = BuildMinimapTooltipText(snapshot.Headline, snapshot.Detail);
            if (string.IsNullOrWhiteSpace(tooltipText))

            {

                return Array.Empty<MinimapUI.EmployeeMarker>();

            }



            return new[]

            {

                new MinimapUI.EmployeeMarker

                {

                    WorldX = worldX,

                    WorldY = worldY,

                    ShowDirectionOverlay = true,

                    PreferredMarkerType = ResolveMinimapEmployeeMarkerType(snapshot),

                    TooltipText = tooltipText

                }

            };

        }



        private static MinimapUI.HelperMarkerType? ResolveMinimapEmployeeMarkerType(SocialRoomFieldActorSnapshot snapshot)

        {

            if (snapshot == null)

            {

                return null;

            }



            return snapshot.Kind switch

            {

                SocialRoomKind.PersonalShop => MinimapUI.HelperMarkerType.UserTrader,

                SocialRoomKind.EntrustedShop => MinimapUI.HelperMarkerType.UserTrader,

                _ => null

            };

        }



        private void RefreshMinimapTrackedUserMarkers()

        {

            if (miniMapUi == null)

            {

                return;

            }



            miniMapUi.SetLocalPlayerHelperMarker(ResolveLocalPlayerMinimapHelperMarker());
            EnsureMinimapTooltipResources();
            miniMapUi.SetTrackedUserMarkers(BuildMinimapTrackedUserMarkers());
            miniMapUi.SetEmployeeMarkers(BuildMinimapEmployeeMarkers(_playerManager?.Player));

        }



        private IReadOnlyList<MinimapUI.TrackedUserMarker> BuildMinimapTrackedUserMarkers()

        {

            PlayerCharacter player = _playerManager?.Player;

            if (player == null)

            {

                return Array.Empty<MinimapUI.TrackedUserMarker>();

            }



            string currentLocationSummary = GetCurrentMapTransferDisplayName();

            _socialListRuntime.UpdateLocalContext(player.Build, currentLocationSummary, 1);

            _familyChartRuntime.UpdateLocalContext(player.Build, currentLocationSummary, 1);

            RefreshGuildSkillUiContext();



            Dictionary<string, MinimapTrackedUserState> trackedUsers = new(StringComparer.OrdinalIgnoreCase);

            foreach (SocialTrackedEntrySnapshot entry in _socialListRuntime.BuildTrackedEntriesSnapshot())

            {

                if (entry == null || entry.IsLocalPlayer || !entry.IsOnline || !IsSameTrackedField(entry.LocationSummary, currentLocationSummary))

                {

                    continue;

                }



                if (!trackedUsers.TryGetValue(entry.Name, out MinimapTrackedUserState state))

                {

                    state = new MinimapTrackedUserState(entry.Name);

                    trackedUsers[entry.Name] = state;

                }



                switch (entry.Tab)

                {

                    case SocialListTab.Party:

                        state.IsPartyMember = true;

                        state.IsPartyLeader |= entry.IsLeader;

                        break;

                    case SocialListTab.Guild:

                        state.IsGuildMember = true;

                        state.IsGuildLeader |= entry.IsLeader;

                        break;

                    case SocialListTab.Friend:

                        state.IsFriend = true;

                        break;

                }

            }



            foreach (SocialTrackedEntrySnapshot entry in _socialListRuntime.BuildTrackedEntriesSnapshot())

            {

                if (_remoteUserPool.TryGetPosition(entry.Name, out Vector2 remotePosition)

                    && trackedUsers.TryGetValue(entry.Name, out MinimapTrackedUserState trackedState))

                {

                    trackedState.HasPosition = true;

                    trackedState.Position = remotePosition;

                }

            }



            foreach (FamilyTrackedMemberSnapshot member in _familyChartRuntime.BuildTrackedMembersSnapshot())

            {

                if (member == null || member.IsLocalPlayer || !member.IsOnline || !IsSameTrackedField(member.LocationSummary, currentLocationSummary))

                {

                    continue;

                }



                if (!trackedUsers.TryGetValue(member.Name, out MinimapTrackedUserState state))

                {

                    state = new MinimapTrackedUserState(member.Name);

                    trackedUsers[member.Name] = state;

                }



                state.HasPosition = true;

                state.Position = member.SimulatedPosition;

            }

            AppendMiniRoomTrackedUserMarkers(trackedUsers, player);

            AppendTraderTrackedUserMarkers(trackedUsers, player);



            if (trackedUsers.Count == 0 && _remoteUserPool.Count == 0)

            {

                return Array.Empty<MinimapUI.TrackedUserMarker>();

            }



            List<MinimapUI.TrackedUserMarker> markers = new(trackedUsers.Count + _remoteUserPool.Count);



            foreach (MinimapUI.TrackedUserMarker remoteMarker in _remoteUserPool.BuildHelperMarkers())



            {



                markers.Add(remoteMarker);



            }

            int syntheticIndex = 0;

            foreach (MinimapTrackedUserState state in trackedUsers.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase))

            {

                Vector2 position = state.HasPosition

                    ? state.Position

                    : ResolveSyntheticTrackedUserPosition(player.X, player.Y, syntheticIndex++);



                markers.Add(new MinimapUI.TrackedUserMarker

                {

                    WorldX = position.X,

                    WorldY = position.Y,

                    MarkerType = ResolveMinimapTrackedUserMarkerType(state),

                    ShowDirectionOverlay = true,

                    TooltipText = BuildMinimapTrackedUserTooltipText(state)

                });

            }



            return markers;

        }



        private static bool IsSameTrackedField(string left, string right)

        {

            return string.Equals(NormalizeTrackedFieldLocation(left), NormalizeTrackedFieldLocation(right), StringComparison.OrdinalIgnoreCase);

        }



        private static string NormalizeTrackedFieldLocation(string value)

        {

            if (string.IsNullOrWhiteSpace(value))

            {

                return string.Empty;

            }



            int channelSeparatorIndex = value.IndexOf("  CH ", StringComparison.OrdinalIgnoreCase);

            string normalized = channelSeparatorIndex >= 0 ? value[..channelSeparatorIndex] : value;

            return normalized.Trim();

        }



        private static Vector2 ResolveSyntheticTrackedUserPosition(float playerX, float playerY, int index)

        {

            float radiusX = 84f + ((index % 3) * 18f);

            float radiusY = 32f + ((index % 2) * 10f);

            float angle = MathHelper.ToRadians(-30f + ((index % 6) * 50f));

            return new Vector2(

                playerX + (float)Math.Cos(angle) * radiusX,

                playerY + (float)Math.Sin(angle) * radiusY);

        }



        private static MinimapUI.HelperMarkerType ResolveMinimapTrackedUserMarkerType(MinimapTrackedUserState state)

        {

            if (state.IsTrader)

            {

                return MinimapUI.HelperMarkerType.AnotherTrader;

            }

            if (state.IsMatchParticipant)

            {

                return MinimapUI.HelperMarkerType.Match;

            }

            if (state.IsPartyMember)

            {

                return state.IsPartyLeader ? MinimapUI.HelperMarkerType.PartyMaster : MinimapUI.HelperMarkerType.Party;

            }



            if (state.IsGuildMember)

            {

                return state.IsGuildLeader ? MinimapUI.HelperMarkerType.GuildMaster : MinimapUI.HelperMarkerType.Guild;

            }



            if (state.IsFriend)

            {

                return MinimapUI.HelperMarkerType.Friend;

            }



            return MinimapUI.HelperMarkerType.Another;

        }



        private sealed class MinimapTrackedUserState

        {

            public MinimapTrackedUserState(string name)

            {

                Name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();

            }



            public string Name { get; }

            public bool IsFriend { get; set; }

            public bool IsPartyMember { get; set; }

            public bool IsPartyLeader { get; set; }

            public bool IsGuildMember { get; set; }

            public bool IsGuildLeader { get; set; }

            public bool IsMatchParticipant { get; set; }

            public bool IsTrader { get; set; }

            public bool HasPosition { get; set; }

            public Vector2 Position { get; set; }

        }



        private MinimapUI.HelperMarkerType? ResolveLocalPlayerMinimapHelperMarker()

        {

            if (HasActiveTraderFieldActor(SocialRoomKind.PersonalShop)
                || HasActiveTraderFieldActor(SocialRoomKind.EntrustedShop))

            {

                return MinimapUI.HelperMarkerType.UserTrader;

            }

            return _specialFieldRuntime?.Minigames.MemoryGame?.IsVisible == true
                ? MinimapUI.HelperMarkerType.Match
                : null;

        }

        private void AppendMiniRoomTrackedUserMarkers(
            Dictionary<string, MinimapTrackedUserState> trackedUsers,
            PlayerCharacter player)

        {

            MemoryGameField memoryGame = _specialFieldRuntime?.Minigames.MemoryGame;
            if (memoryGame == null || !memoryGame.IsVisible)

            {

                return;

            }

            string localName = player?.Build?.Name;
            foreach (string participantName in memoryGame.PlayerNames)

            {

                if (string.IsNullOrWhiteSpace(participantName)
                    || string.Equals(participantName, localName, StringComparison.OrdinalIgnoreCase))

                {

                    continue;

                }

                MinimapTrackedUserState state = GetOrCreateMinimapTrackedUserState(trackedUsers, participantName);
                state.IsMatchParticipant = true;
                TryAssignTrackedUserPosition(state, participantName);

            }

        }

        private void AppendTraderTrackedUserMarkers(
            Dictionary<string, MinimapTrackedUserState> trackedUsers,
            PlayerCharacter player)

        {

            AppendTraderTrackedUserMarkers(SocialRoomKind.PersonalShop, trackedUsers, player);
            AppendTraderTrackedUserMarkers(SocialRoomKind.EntrustedShop, trackedUsers, player);

        }

        private void AppendTraderTrackedUserMarkers(
            SocialRoomKind kind,
            Dictionary<string, MinimapTrackedUserState> trackedUsers,
            PlayerCharacter player)

        {

            SocialRoomRuntime runtime = GetSocialRoomRuntimeIfAvailable(kind);
            if (runtime == null || runtime.GetFieldActorSnapshot(DateTime.UtcNow) == null)

            {

                return;

            }

            string localName = player?.Build?.Name;
            foreach (SocialRoomOccupant occupant in runtime.Occupants)

            {

                if (occupant == null
                    || string.IsNullOrWhiteSpace(occupant.Name)
                    || string.Equals(occupant.Name, localName, StringComparison.OrdinalIgnoreCase))

                {

                    continue;

                }

                MinimapTrackedUserState state = GetOrCreateMinimapTrackedUserState(trackedUsers, occupant.Name);
                state.IsTrader = true;
                TryAssignTrackedUserPosition(state, occupant.Name);

            }

        }

        private SocialRoomRuntime GetSocialRoomRuntimeIfAvailable(SocialRoomKind kind)

        {

            string windowName = GetSocialRoomWindowName(kind);
            return string.IsNullOrWhiteSpace(windowName)
                ? null
                : (uiWindowManager?.GetWindow(windowName) as SocialRoomWindow)?.Runtime;

        }

        private bool HasActiveTraderFieldActor(SocialRoomKind kind)

        {

            SocialRoomRuntime runtime = GetSocialRoomRuntimeIfAvailable(kind);
            return runtime?.GetFieldActorSnapshot(DateTime.UtcNow) != null;

        }

        private static MinimapTrackedUserState GetOrCreateMinimapTrackedUserState(
            IDictionary<string, MinimapTrackedUserState> trackedUsers,
            string name)

        {

            if (!trackedUsers.TryGetValue(name, out MinimapTrackedUserState state))

            {

                state = new MinimapTrackedUserState(name);
                trackedUsers[name] = state;

            }

            return state;

        }

        private void TryAssignTrackedUserPosition(MinimapTrackedUserState state, string trackedName)

        {

            if (state == null || string.IsNullOrWhiteSpace(trackedName) || state.HasPosition)

            {

                return;

            }

            if (_remoteUserPool.TryGetPosition(trackedName, out Vector2 remotePosition))

            {

                state.HasPosition = true;
                state.Position = remotePosition;

            }

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private NpcItem FindNpcById(int npcId)

        {

            if (npcId <= 0)

            {

                return null;

            }



            return _npcsById.TryGetValue(npcId, out NpcItem npc) ? npc : null;

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private NpcItem FindNpcAtMapPoint(int mapX, int mapY)

        {

            if (_npcsArray == null)

                return null;



            for (int i = 0; i < _npcsArray.Length; i++)

            {

                NpcItem npc = _npcsArray[i];

                if (npc != null && npc.ContainsMapPoint(mapX, mapY))

                    return npc;

            }



            return null;

        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private bool CanTalkToNpc(NpcItem npc)

        {

            if (!_gameState.IsPlayerInputEnabled || npc == null || _playerManager?.Player == null)

                return false;



            float deltaX = Math.Abs(_playerManager.Player.X - npc.CurrentX);

            float deltaY = Math.Abs(_playerManager.Player.Y - npc.CurrentY);

            return deltaX <= 180f && deltaY <= 120f;

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

            // Skip if player input is blocked or a map change is already pending

            if (!_gameState.IsPlayerInputEnabled || _gameState.PendingMapChange)

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

        private void SyncMonsterCarnivalGuardianReactors(int currentTick)
        {
            if (_reactorPool == null)
            {
                return;
            }

            MonsterCarnivalField carnivalField = _specialFieldRuntime?.Minigames?.MonsterCarnival;
            if (carnivalField?.IsVisible != true)
            {
                ClearMonsterCarnivalGuardianReactors(currentTick);
                return;
            }

            foreach (KeyValuePair<int, int> tracked in _monsterCarnivalGuardianSlotToReactorIndex.ToArray())
            {
                if (carnivalField.GuardianPlacements.ContainsKey(tracked.Key))
                {
                    continue;
                }

                RemoveMonsterCarnivalGuardianReactor(tracked.Key, tracked.Value, currentTick);
            }

            foreach (KeyValuePair<int, MonsterCarnivalGuardianPlacement> placementEntry in carnivalField.GuardianPlacements)
            {
                if (_monsterCarnivalGuardianSlotToReactorIndex.ContainsKey(placementEntry.Key))
                {
                    continue;
                }

                MonsterCarnivalGuardianPlacement placement = placementEntry.Value;
                if (placement?.Entry == null || placement.ReactorId <= 0)
                {
                    continue;
                }

                List<int> spawnedIndices = _reactorPool.SpawnReactorsAtPositions(
                    placement.ReactorId.ToString(CultureInfo.InvariantCulture),
                    new List<(float x, float y)> { (placement.SpawnPoint.X, placement.SpawnPoint.Y) },
                    currentTick,
                    ReactorActivationType.Hit,
                    canRespawn: false,
                    namePrefix: $"mc_guardian_{placementEntry.Key}");
                if (spawnedIndices.Count <= 0)
                {
                    continue;
                }

                int reactorIndex = spawnedIndices[0];
                _monsterCarnivalGuardianSlotToReactorIndex[placementEntry.Key] = reactorIndex;
                _monsterCarnivalGuardianReactorIndexToSlot[reactorIndex] = placementEntry.Key;
                RegisterDynamicReactorForRendering(reactorIndex);
            }
        }

        private void ClearMonsterCarnivalGuardianReactors(int currentTick)
        {
            foreach (KeyValuePair<int, int> tracked in _monsterCarnivalGuardianSlotToReactorIndex.ToArray())
            {
                RemoveMonsterCarnivalGuardianReactor(tracked.Key, tracked.Value, currentTick);
            }
        }

        private void RemoveMonsterCarnivalGuardianReactor(int slotIndex, int reactorIndex, int currentTick)
        {
            _monsterCarnivalGuardianSlotToReactorIndex.Remove(slotIndex);
            _monsterCarnivalGuardianReactorIndexToSlot.Remove(reactorIndex);
            _reactorPool?.DestroyReactor(reactorIndex, playerId: 0, currentTick);
        }

        private bool TryFindMonsterCarnivalGuardianSlot(ReactorItem reactor, out int slotIndex)
        {
            slotIndex = -1;
            if (reactor?.ReactorInstance == null)
            {
                return false;
            }

            foreach (KeyValuePair<int, int> tracked in _monsterCarnivalGuardianSlotToReactorIndex)
            {
                ReactorItem trackedReactor = _reactorPool?.GetReactor(tracked.Value);
                if (!ReferenceEquals(trackedReactor, reactor))
                {
                    continue;
                }

                slotIndex = tracked.Key;
                _monsterCarnivalGuardianReactorIndexToSlot[tracked.Value] = tracked.Key;
                return true;
            }

            return false;
        }

        private void RegisterDynamicReactorForRendering(int reactorIndex)
        {
            ReactorItem reactor = _reactorPool?.GetReactor(reactorIndex);
            if (reactor == null || mapObjects_Reactors.Contains(reactor))
            {
                return;
            }

            mapObjects_Reactors.Add(reactor);
            _reactorsArray = mapObjects_Reactors.Cast<ReactorItem>().ToArray();
            _reactorVisibilityBuffer = new bool[_reactorsArray.Length];

            if (_visibleReactors == null || _visibleReactors.Length != _reactorsArray.Length)
            {
                _visibleReactors = new ReactorItem[_reactorsArray.Length];
            }

            InitializeSpatialPartitioning();
            RefreshMobRenderArray();
        }



        /// <summary>

        /// Check for reactor touch interactions near player.

        /// Call this from player movement updates.

        /// </summary>

        /// <param name="playerX">Player X position</param>

        /// <param name="playerY">Player Y position</param>

        /// <param name="playerId">Player ID</param>

        /// <returns>List of touched reactors</returns>

        public List<ReactorItem> CheckReactorTouch(float playerX, float playerY, int playerId = 0, int currentTick = int.MinValue)

        {

            if (_reactorPool == null)

                return new List<ReactorItem>();



            if (currentTick == int.MinValue)

                currentTick = Environment.TickCount;



            // Match CUserLocal::CheckReactor_Collision, which only refreshes

            // local touch-reactor overlap once per second.

            if (unchecked(currentTick - _lastReactorCollisionCheckTick) < ReactorCollisionCheckIntervalMs)

                return new List<ReactorItem>();



            _lastReactorCollisionCheckTick = currentTick;

            var touchedReactors = _reactorPool.FindTouchReactorAroundLocalUser(playerX, playerY, currentTick: currentTick);



            foreach (var (reactor, index) in touchedReactors)

            {

                _reactorPool.ActivateReactor(index, playerId, currentTick, ReactorActivationType.Touch);

            }



            return touchedReactors.Select(t => t.reactor).ToList();

        }



        private void TriggerAttackReactors(Rectangle worldHitbox, int currentTick, int skillId, int damage)

        {

            if (worldHitbox.Width <= 0 || worldHitbox.Height <= 0)

                return;



            CoconutField coconut = _specialFieldRuntime?.Minigames?.Coconut;

            if (coconut?.IsActive == true)

            {

                bool allowLocalPreview = !_coconutPacketInbox.HasConnectedClients
                    && !_coconutOfficialSessionBridge.HasConnectedSession;

                if (coconut.TryHandleNormalAttack(worldHitbox, currentTick, skillId, allowLocalPreview))

                {

                    FlushPendingCoconutAttackRequests();

                }

            }



            if (_reactorPool == null)

                return;



            _reactorPool.TriggerSkillReactorsInBounds(

                worldHitbox,

                playerId: 0,

                currentTick: currentTick,

                skillId: skillId,

                damage: damage);

        }



        private void UpdateReactorRuntime(int currentTick, float deltaSeconds)

        {

            if (_reactorPool == null)

                return;



            SyncMonsterCarnivalGuardianReactors(currentTick);
            _reactorPool.RefreshQuestReactors(currentTick);

            _reactorPool.Update(currentTick, deltaSeconds);

            if (_reactorsArray == null || _reactorsArray.Length == 0)

                return;



            if (_reactorVisibilityBuffer == null || _reactorVisibilityBuffer.Length != _reactorsArray.Length)

            {

                _reactorVisibilityBuffer = new bool[_reactorsArray.Length];

            }



            Array.Clear(_reactorVisibilityBuffer, 0, _reactorsArray.Length);

            foreach (var (reactor, index, _) in _reactorPool.GetRenderableReactors())

            {

                if (reactor == null || index < 0 || index >= _reactorsArray.Length)

                    continue;



                _reactorVisibilityBuffer[index] = true;



                var (state, _) = _reactorPool.GetReactorAnimationState(index);

                reactor.SetAnimationState(state, currentTick);

            }



            for (int i = 0; i < _reactorsArray.Length; i++)

            {

                ReactorItem reactor = _reactorsArray[i];

                if (reactor != null)

                {

                    reactor.SetVisible(_reactorVisibilityBuffer[i]);

                }

            }

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



        private void HandleImeCompositionChanged(string compositionText)



        {



            UIWindowBase activeKeyboardWindow = uiWindowManager?.ActiveKeyboardWindow;
            IReadOnlyList<UIWindowBase> windows = uiWindowManager?.Windows;
            if (windows != null)
            {
                foreach (UIWindowBase window in windows)
                {
                    if (!ReferenceEquals(window, activeKeyboardWindow))
                    {
                        window.ClearCompositionText();
                    }
                }
            }

            if (_npcInteractionOverlay?.CapturesKeyboardInput == true)
            {
                _npcInteractionOverlay.HandleCompositionText(compositionText);
            }
            else
            {
                _npcInteractionOverlay?.ClearCompositionText();
            }

            activeKeyboardWindow?.HandleCompositionText(compositionText);



            }

        private void HandleImeCompositionStateChanged(ImeCompositionState compositionState)



        {



            UIWindowBase activeKeyboardWindow = uiWindowManager?.ActiveKeyboardWindow;
            IReadOnlyList<UIWindowBase> windows = uiWindowManager?.Windows;
            if (windows != null)
            {
                foreach (UIWindowBase window in windows)
                {
                    if (!ReferenceEquals(window, activeKeyboardWindow))
                    {
                        window.ClearCompositionText();
                    }
                }
            }

            activeKeyboardWindow?.HandleCompositionState(compositionState ?? ImeCompositionState.Empty);



            }

        private void HandleImeCandidateListChanged(ImeCandidateListState candidateState)



        {



            UIWindowBase activeKeyboardWindow = uiWindowManager?.ActiveKeyboardWindow;
            IReadOnlyList<UIWindowBase> windows = uiWindowManager?.Windows;
            if (windows != null)
            {
                foreach (UIWindowBase window in windows)
                {
                    if (!ReferenceEquals(window, activeKeyboardWindow))
                    {
                        window.ClearImeCandidateList();
                    }
                }
            }

            if (candidateState == null || !candidateState.HasCandidates)
            {
                activeKeyboardWindow?.ClearImeCandidateList();
                return;
            }

            activeKeyboardWindow?.HandleImeCandidateList(candidateState);



            }







        protected override void Dispose(bool disposing)



        {



            if (disposing)



            {



                _imeCompositionMonitor.CompositionTextChanged -= HandleImeCompositionChanged;
                _imeCompositionMonitor.CompositionStateChanged -= HandleImeCompositionStateChanged;
                _imeCompositionMonitor.CandidateListChanged -= HandleImeCandidateListChanged;



                _imeCompositionMonitor.Dispose();



            }







            base.Dispose(disposing);



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



        private void SpawnMobDeathRewards(MobItem mob, int currentTick)

        {

            if (mob?.MovementInfo == null || _dropPool == null)

            {

                return;

            }



            float mobX = mob.MovementInfo.X;

            float mobY = mob.MovementInfo.Y;

            bool isBoss = mob.AI?.IsBoss ?? false;



            int mesoMin = isBoss ? 1000 : 10;

            int mesoMaxExclusive = isBoss ? 10000 : 500;

            int mesoAmount = Random.Shared.Next(mesoMin, mesoMaxExclusive);



            int showdownBonusPercent = Math.Max(0, mob.AI?.GetStatusEffectValue(MobStatusEffect.Showdown) ?? 0);

            if (showdownBonusPercent > 0)

            {

                mesoAmount = (int)MathF.Round(mesoAmount * (1f + showdownBonusPercent / 100f));

            }



            DropItem mesoDrop = _dropPool.SpawnMesoDrop(mobX, mobY, mesoAmount, currentTick);

            if (mesoDrop == null)

            {

                return;

            }



            List<IDXObject> frames = GetMesoFramesForAmount(mesoAmount);

            if (frames == null || frames.Count == 0)

            {

                return;

            }



            mesoDrop.AnimFrames = frames;

            mesoDrop.Icon = frames[0];

            if (mob?.MobInstance?.MobInfo != null
                && int.TryParse(mob.MobInstance.MobInfo.ID, out int killedMobId)
                && _monsterBookManager.TryResolveCardItemId(killedMobId, out int monsterCardItemId))
            {
                _dropPool.SpawnItemDrop(
                    mobX + 18f,
                    mobY,
                    monsterCardItemId.ToString(CultureInfo.InvariantCulture),
                    1,
                    currentTick,
                    isRare: true);
            }

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

        private void HandlePortalUpInteract(int currentTime)

        {

            if (!CanAttemptPortalInteract())

            {

                ClearPassiveTransferRequest();

                return;

            }



            bool interactPressed = _playerManager.Input.IsPressed(InputAction.Interact);

            bool interactHeld = _playerManager.Input.IsHeld(InputAction.Interact);



            if (_passiveTransferRequestPending)

            {

                if (!interactHeld || unchecked(currentTime - _passiveTransferRequestExpiresAt) >= 0)

                {

                    ClearPassiveTransferRequest();

                }

                else if (_playerManager.Player?.CanMove == true && TryHandlePortalInteractCore(currentTime))

                {

                    ClearPassiveTransferRequest();

                    return;

                }

            }



            if (!interactPressed)

                return;



            if (_playerManager.Player?.CanMove == true)

            {

                if (TryHandlePortalInteractCore(currentTime))

                {

                    ClearPassiveTransferRequest();

                }



                return;

            }



            _passiveTransferRequestPending = true;

            _passiveTransferRequestExpiresAt = unchecked(currentTime + PASSIVE_TRANSFER_REQUEST_DURATION_MS);

        }



        private bool CanAttemptPortalInteract()

        {

            return !_gameState.PendingMapChange

                   && !_sameMapTeleportPending

                   && _gameState.IsPlayerInputEnabled

                   && _playerManager != null

                   && _playerManager.IsPlayerActive;

        }



        private void ClearPassiveTransferRequest()

        {

            _passiveTransferRequestPending = false;

            _passiveTransferRequestExpiresAt = int.MinValue;

        }



        private bool TryHandlePortalInteractCore(int currentTime)

        {

            // Get player position

            var playerPos = _playerManager.GetPlayerPosition();

            float playerX = playerPos.X;

            float playerY = playerPos.Y;

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;



            if (_gameState.IsPortalOnCooldown(currentTime))

                return false;



            if (_temporaryPortalField != null

                && _temporaryPortalField.TryUseLinkedPortal(_mapBoard.MapInfo.id, playerX, playerY, out var temporaryPortalDestination))

            {

                if (temporaryPortalDestination.MapId != _mapBoard.MapInfo.id)

                {

                    string transferRestrictionMessage = FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(fieldLimit);

                    if (!string.IsNullOrWhiteSpace(transferRestrictionMessage))

                    {

                        ShowFieldRestrictionMessage(transferRestrictionMessage);

                        return true;

                    }

                }



                PlayPortalSE();

                _playerManager?.ForceStand();



                if (temporaryPortalDestination.MapId == _mapBoard.MapInfo.id)

                {

                    StartSameMapTeleport(

                        temporaryPortalDestination.X,

                        temporaryPortalDestination.Y,

                        temporaryPortalDestination.DelayMs,

                        currentTime);

                }

                else

                {

                    SetPendingMapSpawnTarget(temporaryPortalDestination.X, temporaryPortalDestination.Y);

                    _gameState.PendingMapChange = true;

                    _gameState.PendingMapId = temporaryPortalDestination.MapId;

                    _gameState.PendingPortalName = null;

                }



                return true;

            }



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

                string transferRestrictionMessage = FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(fieldLimit);

                if (!string.IsNullOrWhiteSpace(transferRestrictionMessage))

                {

                    ShowFieldRestrictionMessage(transferRestrictionMessage);

                    return true;

                }



                PlayPortalSE();

                _playerManager?.ForceStand();

                PublishDynamicObjectTagStatesForPortal(nearestPortal?.PortalInstance, currentTime);

                _gameState.PendingMapChange = true;

                _gameState.PendingMapId = nearestPortal.PortalInstance.tm;

                _gameState.PendingPortalName = nearestPortal.PortalInstance.tn;

                return true;

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

                string transferRestrictionMessage = FieldInteractionRestrictionEvaluator.GetTransferRestrictionMessage(fieldLimit);

                if (!string.IsNullOrWhiteSpace(transferRestrictionMessage))

                {

                    ShowFieldRestrictionMessage(transferRestrictionMessage);

                    return true;

                }



                PlayPortalSE();

                _playerManager?.ForceStand();

                PublishDynamicObjectTagStatesForPortal(nearestHiddenPortal, currentTime);

                _gameState.PendingMapChange = true;

                _gameState.PendingMapId = nearestHiddenPortal.tm;

                _gameState.PendingPortalName = nearestHiddenPortal.tn;

                return true;

            }



            return false;

        }



        private void HandleSpecialFieldAttackHitbox(PlayerCharacter player, Rectangle attackHitbox, int currentTime)

        {

            GuildBossField guildBoss = _specialFieldRuntime?.SpecialEffects?.GuildBoss;

            if (guildBoss?.IsActive != true

                || _gameState.PendingMapChange

                || _sameMapTeleportPending

                || !_gameState.IsPlayerInputEnabled

                || _playerManager?.IsPlayerActive != true

                || player != _playerManager.Player)

            {

                return;

            }



            bool allowLocalPreview = !_guildBossOfficialSessionBridge.HasConnectedSession
                && !_guildBossTransport.HasConnectedClients;

            if (!guildBoss.TryHandleLocalPulleyAttack(attackHitbox, currentTime, allowLocalPreview, out string message))

            {

                return;

            }



            if (!allowLocalPreview && guildBoss.TryConsumePulleyPacketRequest(out GuildBossField.PulleyPacketRequest request))

            {

                if (_guildBossOfficialSessionBridge.HasConnectedSession)

                {

                    if (_guildBossOfficialSessionBridge.TrySendPulleyRequest(request, out string sessionStatus))

                    {

                        message = $"{message} Waiting for live session reply.";

                    }

                    else if (_guildBossTransport.HasConnectedClients
                        && _guildBossTransport.TrySendPulleyRequest(request, out string transportStatus))

                    {

                        message = $"{message} Waiting for transport reply.";

                    }

                    else

                    {

                        message = sessionStatus;

                    }

                }

                else if (_guildBossTransport.TrySendPulleyRequest(request, out string transportStatus))

                {

                    message = $"{message} Waiting for transport reply.";

                }

                else

                {

                    message = transportStatus;

                }

            }



            if (!string.IsNullOrWhiteSpace(message))

            {

                _chat.AddMessage(message, new Color(151, 221, 255), currentTime);

            }

        }



        private void StartSameMapTeleport(float targetX, float targetY, int delayMs, int currentTime)

        {

            _sameMapTeleportPending = true;

            _sameMapTeleportStartTime = currentTime;

            _sameMapTeleportDelay = Math.Max(0, delayMs);

            _sameMapTeleportTarget = new SameMapTeleportTarget(targetX, targetY - SAME_MAP_TELEPORT_Y_OFFSET);

        }



        private void SetPendingMapSpawnTarget(float x, float y)

        {

            _pendingMapSpawnTarget = new PendingMapSpawnTarget(x, y);

        }



        private bool TryConsumePendingMapSpawnTarget(out float spawnX, out float spawnY)

        {

            if (_pendingMapSpawnTarget == null)

            {

                spawnX = 0f;

                spawnY = 0f;

                return false;

            }



            spawnX = _pendingMapSpawnTarget.X;

            spawnY = _pendingMapSpawnTarget.Y;

            _pendingMapSpawnTarget = null;

            return true;

        }



        private void ResolveSpawnPosition(out float spawnX, out float spawnY)

        {

            bool spawnPositionSet = false;

            spawnX = _vrFieldBoundary.Center.X;

            spawnY = _vrFieldBoundary.Center.Y;



            if (TryConsumePendingMapSpawnTarget(out float pendingSpawnX, out float pendingSpawnY))

            {

                mapShiftX = (int)MathF.Round(pendingSpawnX);

                mapShiftY = (int)MathF.Round(pendingSpawnY);

                spawnX = pendingSpawnX;

                spawnY = pendingSpawnY;

                spawnPositionSet = true;

            }



            if (!spawnPositionSet && !string.IsNullOrEmpty(_spawnPortalName))

            {

                PortalInstance targetPortal = _mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _spawnPortalName);

                if (targetPortal != null)

                {

                    mapShiftX = targetPortal.X;

                    mapShiftY = targetPortal.Y;

                    spawnX = targetPortal.X;

                    spawnY = targetPortal.Y;

                    spawnPositionSet = true;

                }

            }



            if (!spawnPositionSet)

            {

                List<PortalInstance> startPortals = _mapBoard.BoardItems.Portals

                    .Where(portal => portal.pt == PortalType.StartPoint)

                    .ToList();

                if (startPortals.Count > 0)

                {

                    Random random = new Random();

                    PortalInstance randomStartPortal = startPortals[random.Next(startPortals.Count)];

                    mapShiftX = randomStartPortal.X;

                    mapShiftY = randomStartPortal.Y;

                    spawnX = randomStartPortal.X;

                    spawnY = randomStartPortal.Y;

                }

            }

        }



        private void UpdateDirectionModeState(int currentTime)

        {

            if (_specialFieldRuntime.SpecialEffects.Wedding.HasActiveScriptedDialog)

            {

                _scriptedDirectionModeWindows.TrackWindow(WeddingDirectionModeOwnerName);

            }



            if (_specialFieldRuntime.Minigames.MemoryGame.IsVisible)

            {

                _scriptedDirectionModeWindows.TrackWindow(MemoryGameDirectionModeOwnerName);

            }



            bool scriptedOwnerActive = (_npcInteractionOverlay?.IsVisible == true)

                || _scriptedDirectionModeWindows.HasVisibleOwnedWindow(IsDirectionModeOwnerActive);



            if (scriptedOwnerActive)

            {

                _gameState.EnterDirectionMode();

            }

            else if (_scriptedDirectionModeOwnerActive)

            {

                _gameState.RequestLeaveDirectionMode(currentTime, DIRECTION_MODE_RELEASE_DELAY_MS);

            }



            _scriptedDirectionModeOwnerActive = scriptedOwnerActive;

            _gameState.UpdateDirectionMode(currentTime);

        }



        private string GetPlayerSkillStateRestrictionMessage(int currentTime)

        {

            return _gameState.DirectionModeActive

                ? "Skills cannot be used during scripted interactions."

                : null;

        }



        private bool IsWindowVisible(string windowName)

        {

            return uiWindowManager?.GetWindow(windowName)?.IsVisible == true;

        }



        private bool IsDirectionModeOwnerActive(string ownerName)

        {

            return ownerName switch

            {

                WeddingDirectionModeOwnerName => _specialFieldRuntime.SpecialEffects.Wedding.HasActiveScriptedDialog,

                MemoryGameDirectionModeOwnerName => _specialFieldRuntime.Minigames.MemoryGame.IsVisible,

                _ => IsWindowVisible(ownerName)

            };

        }



        private void CompleteSameMapTeleport()

        {

            SameMapTeleportTarget target = _sameMapTeleportTarget;

            if (target != null)

            {

                _playerManager?.TeleportTo(target.X, target.Y);

                _playerManager?.SetSpawnPoint(target.X, target.Y);



                if (_gameState.UseSmoothCamera)

                {

                    _cameraController.TeleportTo(target.X, target.Y);

                    mapShiftX = _cameraController.MapShiftX;

                    mapShiftY = _cameraController.MapShiftY;

                }

                else

                {

                    mapShiftX = (int)MathF.Round(target.X);

                    mapShiftY = (int)MathF.Round(target.Y);

                    SetCameraMoveX(true, false, 0);

                    SetCameraMoveX(false, true, 0);

                    SetCameraMoveY(true, false, 0);

                    SetCameraMoveY(false, true, 0);

                }

            }



            _sameMapTeleportPending = false;

            _sameMapTeleportTarget = null;

            ClearPassiveTransferRequest();

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

            _npcsById.Clear();

            for (int i = 0; i < _npcsArray.Length; i++)

            {

                NpcItem npc = _npcsArray[i];

                if (npc?.NpcInstance?.NpcInfo == null || !int.TryParse(npc.NpcInstance.NpcInfo.ID, out int npcId))

                {

                    continue;

                }



                _npcsById[npcId] = npc;

            }

            miniMapUi?.SetNpcMarkers(_npcsArray);



            // Convert Mobs

            _mobsArray = mapObjects_Mobs.Count > 0

                ? mapObjects_Mobs.Cast<MobItem>().ToArray()

                : Array.Empty<MobItem>();



            // Initialize mob pool for spawn/despawn management

            _mobPool = new MobPool();

            _mobPool.Initialize(_mobsArray);

            _mobPool.SetOnMobSpawned(AddMobToActiveArrays);



            // Initialize drop pool for item/meso drops

            _dropPool = new DropPool();

            _dropPool.Initialize();

            _dropPool.SetPickupAvailabilityEvaluator(EvaluatePickupAvailability);

            _dropPool.SetPetPickupAvailabilityEvaluator(EvaluatePetPickupAvailability);

            _dropPool.SetGroundLevelLookup((x, y) =>

            {

                // Ground level offset - the meso icon origin is typically at center,

                // so we need to move the drop position up so the icon's bottom is on the platform

                // Meso icons are ~20-24px tall, so move up by ~15px

                return y - 18;

            });



            // Set up pickup sound and notice callbacks

            _dropPool.SetOnPickupResolved(HandleDropPickedUp);

            _dropPool.SetOnPickupFailed(HandlePickupAttemptFailed);

            _dropPool.SetOnRemotePlayerPickedUp(HandleDropPickedUpByRemotePlayer);



            _dropPool.SetOnRemotePetPickedUp(HandleDropPickedUpByRemotePet);



            _dropPool.SetOnRemoteOtherPickedUp(HandleDropPickedUpByRemoteOther);



            _dropPool.SetOnMobPickedUp(HandleDropPickedUpByMob);



            // Set up death effect and drop spawn callbacks

            _mobPool.SetOnMobDied(mob =>

            {

                int currentTick = Environment.TickCount;

                _combatEffects.AddDeathEffectForMob(mob, currentTick);

                _questRuntime.RecordMobKill(mob?.MobInstance);



                bool suppressRewards = SpecialMobInteractionRules.ShouldSuppressRewardDrops(mob);

                if (suppressRewards)

                {

                    return;

                }



                // Play drop item sound

                PlayDropItemSE();



                SpawnMobDeathRewards(mob, currentTick);

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

                RefreshMobRenderArray();

            });



            Rectangle mobSpawnBounds = _mapBoard.VRRectangle != null

                ? new Rectangle(_mapBoard.VRRectangle.X, _mapBoard.VRRectangle.Y, _mapBoard.VRRectangle.Width, _mapBoard.VRRectangle.Height)

                : new Rectangle(-_mapBoard.CenterPoint.X, -_mapBoard.CenterPoint.Y, _mapBoard.MapSize.X, _mapBoard.MapSize.Y);



            _mobPool.ConfigureSpawnModel(

                mobSpawnBounds.Width,

                mobSpawnBounds.Height,

                _mapBoard.MapInfo?.mobRate ?? 1.5f,

                _mapBoard.MapInfo?.createMobInterval,

                _mapBoard.MapInfo?.fieldLimit ?? 0,

                simulatedCharacterCount: 1);

            _mobPool.TrimInitialPopulation();



            // Convert Reactors

            _reactorsArray = mapObjects_Reactors.Count > 0

                ? mapObjects_Reactors.Cast<ReactorItem>().ToArray()

                : Array.Empty<ReactorItem>();



            // Convert Portals

            _portalsArray = mapObjects_Portal.Count > 0

                ? mapObjects_Portal.Cast<PortalItem>().ToArray()

                : Array.Empty<PortalItem>();

            miniMapUi?.SetPortalMarkers(_portalsArray);



            // Initialize portal pool for hidden portal support

            _temporaryPortalField?.ClearRemotePortals();



            _portalPool = new PortalPool();

            _portalPool.Initialize(_portalsArray);

            _portalPool.SetOnHiddenPortalRevealed((portal, index) =>

            {

                System.Diagnostics.Debug.WriteLine($"[PortalPool] Hidden portal revealed: {portal.PortalInstance.pn}");

            });



            // Initialize reactor pool for reactor spawning and touch detection

            _reactorPool = new ReactorPool();

            _reactorPool.Initialize(_reactorsArray, _DxDeviceManager.GraphicsDevice);

            _reactorPool.SetReactorFactory((spawnPoint, device) =>
            {
                ReactorInfo reactorInfo = ReactorInfo.Get(spawnPoint.ReactorId);
                if (reactorInfo == null)
                {
                    return null;
                }

                ReactorInstance reactorInstance = reactorInfo.CreateInstance(
                    _mapBoard,
                    (int)Math.Round(spawnPoint.X),
                    (int)Math.Round(spawnPoint.Y),
                    reactorTime: 0,
                    spawnPoint.Name ?? string.Empty,
                    spawnPoint.Flip) as ReactorInstance;
                if (reactorInstance == null)
                {
                    return null;
                }

                return MapSimulatorLoader.CreateReactorFromProperty(
                    _texturePool,
                    reactorInstance,
                    device,
                    new ConcurrentBag<WzObject>());
            });

            _reactorPool.SetQuestStateProvider(_questRuntime.GetCurrentState);

            _reactorPool.SetOnReactorTouched((reactor, playerId) =>

            {

                System.Diagnostics.Debug.WriteLine($"[ReactorPool] Reactor touched: {reactor.ReactorInstance.Name}");

            });

            _reactorPool.SetOnReactorActivated((reactor, playerId) =>

            {

                System.Diagnostics.Debug.WriteLine($"[ReactorPool] Reactor activated: {reactor.ReactorInstance.Name}");

            });

            _reactorPool.SetOnReactorHit((reactor, playerId) =>
            {
                if (reactor?.ReactorInstance == null)
                {
                    return;
                }

                MonsterCarnivalField carnivalField = _specialFieldRuntime?.Minigames?.MonsterCarnival;
                if (carnivalField?.IsVisible != true)
                {
                    return;
                }

                if (!TryFindMonsterCarnivalGuardianSlot(reactor, out int slotIndex))
                {
                    return;
                }

                if (carnivalField.TryApplyGuardianReactorHit(slotIndex, destroyPlacement: false, Environment.TickCount, out _))
                {
                    _chat?.AddMessage(carnivalField.CurrentStatusMessage, new Color(255, 228, 151), Environment.TickCount);
                }
            });

            _reactorPool.SetOnReactorDestroyed((reactor, playerId) =>
            {
                if (reactor?.ReactorInstance == null)
                {
                    return;
                }

                if (!TryFindMonsterCarnivalGuardianSlot(reactor, out int slotIndex))
                {
                    return;
                }

                _monsterCarnivalGuardianSlotToReactorIndex.Remove(slotIndex);
                foreach (KeyValuePair<int, int> tracked in _monsterCarnivalGuardianReactorIndexToSlot.Where(pair => pair.Value == slotIndex).ToArray())
                {
                    _monsterCarnivalGuardianReactorIndexToSlot.Remove(tracked.Key);
                }

                MonsterCarnivalField carnivalField = _specialFieldRuntime?.Minigames?.MonsterCarnival;
                if (carnivalField?.IsVisible == true
                    && carnivalField.TryApplyGuardianReactorHit(slotIndex, destroyPlacement: true, Environment.TickCount, out _))
                {
                    _chat?.AddMessage(carnivalField.CurrentStatusMessage, new Color(255, 228, 151), Environment.TickCount);
                }
            });

            _reactorPool.SetOnReactorScriptStateChanged((reactor, scriptName, isEnabled, currentTick) =>

            {

                ReactorScriptStatePublisher.Publish(

                    scriptName,

                    isEnabled,

                    CollectAvailableDynamicObjectTags(),

                    SetDynamicObjectTagState,

                    currentTick);

            });

            _reactorPool.RefreshQuestReactors(Environment.TickCount);



            // Convert Tooltips

            _tooltipsArray = mapObjects_tooltips.Count > 0

                ? mapObjects_tooltips.Cast<TooltipItem>().ToArray()

                : Array.Empty<TooltipItem>();



            // Convert Backgrounds

            _backgroundsFrontArray = backgrounds_front.ToArray();

            _backgroundsBackArray = backgrounds_back.ToArray();



            // Set render arrays on RenderingManager

            RefreshMobRenderArray();



            // Set pools on RenderingManager

            _renderingManager.SetPools(_dropPool, _playerManager);



            // Initialize spatial partitioning if map has enough objects

            InitializeSpatialPartitioning();

        }



        private void RegisterQuestGatedMapObject(

            BaseDXDrawableItem mapItem,

            LayeredItem sourceItem,

            ConcurrentDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects)

        {

            if (mapItem == null || sourceItem is not ObjectInstance objInst)

            {

                return;

            }



            bool hiddenByMap = objInst.hide == true;

            bool hasQuestInfo = objInst.QuestInfo != null && objInst.QuestInfo.Count > 0;

            string[] dynamicTags = ParseObjectTags(objInst.tags);

            bool hasDynamicTags = dynamicTags.Length > 0;

            if (!hiddenByMap && !hasQuestInfo && !hasDynamicTags)

            {

                return;

            }



            questGatedMapObjects[mapItem] = new QuestGatedMapObjectState(

                objInst.QuestInfo?.ToArray(),

                dynamicTags,

                hiddenByMap);

        }



        private void ReplaceQuestGatedMapObjects(IDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects)

        {

            _questGatedMapObjects.Clear();

            if (questGatedMapObjects == null || questGatedMapObjects.Count == 0)

            {

                return;

            }



            foreach ((BaseDXDrawableItem mapItem, QuestGatedMapObjectState state) in questGatedMapObjects)

            {

                _questGatedMapObjects[mapItem] = state;

            }

        }



        private void ApplyQuestObjectVisibility(BaseDXDrawableItem item)

        {

            if (item == null || !_questGatedMapObjects.TryGetValue(item, out QuestGatedMapObjectState state))

            {

                return;

            }



            bool visible = FieldObjectQuestVisibilityEvaluator.IsVisible(

                state.HiddenByMap,

                state.QuestInfo,

                state.DynamicTags,

                _questRuntime.GetCurrentState,

                TryGetDynamicObjectTagState);

            item.SetVisible(item.IsVisible && visible);

        }



        private bool? TryGetDynamicObjectTagState(string tag)

        {

            if (string.IsNullOrWhiteSpace(tag))

            {

                return null;

            }



            if (_fieldEffects.TryGetPublishedObjectState(tag, out bool publishedState))

            {

                return publishedState;

            }



            if (_questRuntime.TryGetQuestLayerTagState(tag, out bool questLayerState))

            {

                return questLayerState;

            }



            return _authoredDynamicObjectTagStates.TryGetValue(tag, out bool authoredState)

                ? authoredState

                : null;

        }



        public bool SetDynamicObjectTagState(string tag, bool? isEnabled, int transitionTimeMs = 0, int? currentTimeMs = null)

        {

            if (string.IsNullOrWhiteSpace(tag))

            {

                return false;

            }



            int resolvedCurrentTime = currentTimeMs ?? Environment.TickCount;

            if (isEnabled.HasValue)

            {

                _fieldEffects.PublishObjectState(tag, isEnabled.Value, Math.Max(0, transitionTimeMs), resolvedCurrentTime);

                return true;

            }



            return _fieldEffects.ClearPublishedObjectState(tag);

        }



        private static string[] ParseObjectTags(string tags)

        {

            if (string.IsNullOrWhiteSpace(tags))

            {

                return Array.Empty<string>();

            }



            return tags

                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        }



        private void InitializeAuthoredDynamicObjectTagStates()

        {

            _authoredDynamicObjectTagStates.Clear();



            IReadOnlyDictionary<string, bool> authoredStates =

                FieldObjectTagStateDefaultsLoader.Load(_mapBoard?.MapInfo);

            foreach ((string tag, bool state) in authoredStates)

            {

                _authoredDynamicObjectTagStates[tag] = state;

            }

        }



        private void ApplyEntryScriptDynamicObjectTagStates(int currentTickCount, bool includeFirstUserEnterScript)

        {

            foreach (string scriptName in EnumerateEntryOwnedFieldScripts(includeFirstUserEnterScript))

            {

                PublishDynamicObjectTagStatesForScriptName(scriptName, currentTickCount);

            }

        }



        private IEnumerable<string> EnumerateEntryOwnedFieldScripts(bool includeFirstUserEnterScript)

        {

            if (_mapBoard?.MapInfo == null)

            {

                yield break;

            }



            if (!string.IsNullOrWhiteSpace(_mapBoard.MapInfo.onUserEnter))

            {

                yield return _mapBoard.MapInfo.onUserEnter;

            }



            if (includeFirstUserEnterScript && !string.IsNullOrWhiteSpace(_mapBoard.MapInfo.onFirstUserEnter))

            {

                yield return _mapBoard.MapInfo.onFirstUserEnter;

            }



            string fieldScript = (_mapBoard.MapInfo.Image?["info"]?["fieldScript"] as WzStringProperty)?.Value;

            if (!string.IsNullOrWhiteSpace(fieldScript))

            {

                yield return fieldScript;

            }

        }



        private HashSet<string> CollectAvailableDynamicObjectTags()

        {

            var availableTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((BaseDXDrawableItem _, QuestGatedMapObjectState state) in _questGatedMapObjects)

            {

                for (int i = 0; i < state.DynamicTags.Length; i++)

                {

                    string tag = state.DynamicTags[i];

                    if (!string.IsNullOrWhiteSpace(tag))

                    {

                        availableTags.Add(tag);

                    }

                }

            }



            foreach ((string tag, bool _) in _authoredDynamicObjectTagStates)

            {

                if (!string.IsNullOrWhiteSpace(tag))

                {

                    availableTags.Add(tag);

                }

            }



            return availableTags;

        }



        private void InitializeDynamicObjectDirectionEventTriggers()

        {

            _dynamicObjectDirectionEventTriggers.Clear();

            _triggeredDynamicObjectDirectionEventIndices.Clear();



            WzImage mapImage = _mapBoard?.MapInfo?.Image;

            if (mapImage == null)

            {

                return;

            }



            IReadOnlyList<FieldObjectDirectionEventTriggerPoint> triggers =

                FieldObjectDirectionEventTriggerLoader.Load(mapImage);

            for (int i = 0; i < triggers.Count; i++)

            {

                _dynamicObjectDirectionEventTriggers.Add(triggers[i]);

            }

        }



        private void UpdateDynamicObjectDirectionEventTriggers(int currentTickCount)

        {

            if (_dynamicObjectDirectionEventTriggers.Count == 0

                || _playerManager?.Player == null

                || _gameState.PendingMapChange

                || _sameMapTeleportPending)

            {

                return;

            }



            Rectangle playerHitbox = _playerManager.Player.GetHitbox();

            for (int i = 0; i < _dynamicObjectDirectionEventTriggers.Count; i++)

            {

                if (_triggeredDynamicObjectDirectionEventIndices.Contains(i))

                {

                    continue;

                }



                FieldObjectDirectionEventTriggerPoint trigger = _dynamicObjectDirectionEventTriggers[i];

                if (!playerHitbox.Contains(trigger.X, trigger.Y))

                {

                    continue;

                }



                bool publishedAny = false;

                for (int scriptIndex = 0; scriptIndex < trigger.ScriptNames.Length; scriptIndex++)

                {

                    publishedAny |= PublishDynamicObjectTagStatesForScriptName(

                        trigger.ScriptNames[scriptIndex],

                        currentTickCount);

                }



                if (publishedAny)

                {

                    _triggeredDynamicObjectDirectionEventIndices.Add(i);

                }

            }

        }



        private bool PublishDynamicObjectTagStatesForPortal(PortalInstance portal, int currentTickCount)

        {

            if (portal == null)

            {

                return false;

            }



            return PublishDynamicObjectTagStatesForScriptName(portal.script, currentTickCount);

        }



        private bool PublishDynamicObjectTagStatesForScriptName(string scriptName, int currentTickCount)

        {

            if (string.IsNullOrWhiteSpace(scriptName))

            {

                return false;

            }



            HashSet<string> availableTags = CollectAvailableDynamicObjectTags();

            if (availableTags.Count == 0)

            {

                return false;

            }



            FieldObjectScriptTagAliasResolver.PublishedTagMutation mutation =

                FieldObjectScriptTagAliasResolver.ResolvePublishedTagMutation(scriptName, availableTags);

            bool publishedAny = false;

            for (int i = 0; i < mutation.TagsToDisable.Count; i++)

            {

                publishedAny |= SetDynamicObjectTagState(mutation.TagsToDisable[i], false, 0, currentTickCount);

            }

            for (int i = 0; i < mutation.TagsToEnable.Count; i++)

            {

                publishedAny |= SetDynamicObjectTagState(mutation.TagsToEnable[i], true, 0, currentTickCount);

            }



            return publishedAny;

        }



        private bool PublishDynamicObjectTagStatesForScriptNames(IEnumerable<string> scriptNames, int currentTickCount)

        {

            if (scriptNames == null)

            {

                return false;

            }



            bool publishedAny = false;

            foreach (string scriptName in scriptNames)

            {

                publishedAny |= PublishDynamicObjectTagStatesForScriptName(scriptName, currentTickCount);

            }



            return publishedAny;

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

            _reactorVisibilityBuffer = new bool[_reactorsArray.Length];



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
            _playerManager.SetMobSkillRuntimeResolver(ResolveMobSkillRuntimeData);
            _remoteUserPool.Initialize(_playerManager.Loader, _playerManager.SkillLoader);
            _summonedPool.Initialize(
                _playerManager.SkillLoader,
                _mobPool,
                _remoteUserPool,
                () => _playerManager?.Player,
                skillId => _playerManager?.Skills?.GetSkillLevel(skillId) ?? 0,
                _soundManager,
                _combatEffects);
            if (_playerManager?.Skills != null)
            {
                _playerManager.Skills.OnClientSkillCancelRequested = (cancelSkillId, _, currentTime) =>
                    _summonedPool.TryCancelLocalOwnerSummonsBySkillRequest(cancelSkillId, currentTime);
            }
            Debug.WriteLine($"[Player] PlayerManager initialized with Character.wz: {characterWz != null}, Skill.wz: {skillWz != null}");

            _affectedAreaPool = new AffectedAreaPool(_playerManager.SkillLoader, _playerManager.GetMobSkillEffectLoader(), GraphicsDevice);

            _playerManager.SetAffectedAreaPool(_affectedAreaPool);
            _playerManager.SetRemoteAffectedAreaDamageBlockEvaluator(IsRemoteAffectedAreaProtectionActive);
            _playerManager.SetAffectedAreaOwnerPartyMembershipEvaluator(IsAffectedAreaOwnerPartyMember);



            // Set spawn point

            _playerManager.SetSpawnPoint(spawnX, spawnY);



            // Connect mob and drop pools

            _playerManager.SetMobPool(_mobPool);

            _playerManager.SetDropPool(_dropPool);

            _playerManager.SetCombatEffects(_combatEffects);

            _playerManager.SetSoundManager(_soundManager);

            _playerManager.SetCurrentMapIdProvider(() => _mapBoard?.MapInfo?.id ?? -1);

            _playerManager.SetCurrentMapInfoProvider(() => _mapBoard?.MapInfo);

            _playerManager.SetReactorAttackAreaHandler(TriggerAttackReactors);

            _playerManager.SetAttackHitboxHandler(HandleSpecialFieldAttackHitbox);



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



            // Set up flying-map flags from map info.

            bool isFlyingMap = _mapBoard.MapInfo.fly == true;

            bool requiresFlyingSkillForMap = _mapBoard.MapInfo.needSkillForFly == true;

            if (isFlyingMap)

            {

                Debug.WriteLine($"[FlyingMap] Map allows flying (fly=true, needSkillForFly={requiresFlyingSkillForMap})");

            }

            _playerManager.SetFlyingMap(isFlyingMap, requiresFlyingSkillForMap);

            _playerManager.SetMoveSpeedCapResolver(ApplyBattlefieldMoveSpeedCap);



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



            if (_playerManager?.Player?.Build != null)

            {

                RefreshSkillWindowForJob(_playerManager.Player.Build.Job);

            }



            _playerManager.Combat.OnPickupAttemptFailed = HandlePickupAttemptFailed;

            _playerManager.Combat.EvaluatePickupAvailability = EvaluatePickupAvailability;



            ConfigureSkillRestrictions();

            ConfigureSkillUIBindings();



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



            // Set up flying map flags

            bool isFlyingMap = _mapBoard.MapInfo.fly == true;

            bool requiresFlyingSkillForMap = _mapBoard.MapInfo.needSkillForFly == true;

            _playerManager.SetFlyingMap(isFlyingMap, requiresFlyingSkillForMap);

            _playerManager.SetMoveSpeedCapResolver(ApplyBattlefieldMoveSpeedCap);



            // Reconnect to new map's pools and effects

            _playerManager.ReconnectToMap(

                _playerManager.GetFootholdLookup(),

                _playerManager.GetLadderLookup(),

                _playerManager.GetSwimAreaCheck(),

                isFlyingMap,

                requiresFlyingSkillForMap,

                _mobPool,

                _dropPool,

                _combatEffects);



            // Teleport player to spawn position and snap to foothold

            // TeleportTo properly finds a foothold and lands the player on it

            _playerManager.SetSpawnPoint(spawnX, spawnY);

            _playerManager.TeleportTo(spawnX, spawnY);



            ConfigureSkillRestrictions();

            ConfigureSkillUIBindings();



            Debug.WriteLine($"[Player] Reconnected to new map, IsActive: {_playerManager.IsPlayerActive}");

        }



        private void ConfigureSkillRestrictions()

        {

            if (_playerManager?.Skills == null)

                return;



            _playerManager.Skills.SetAdditionalStateRestrictionMessageProvider(GetPlayerSkillStateRestrictionMessage);
            _playerManager.Skills.SetCurrentMapInfoProvider(() => _mapBoard?.MapInfo);
            _playerManager.Skills.SetExternalFriendlySupportSummonsProvider(
                () => _summonedPool.GetSupportSummonsAffectingLocalPlayer(IsAffectedAreaOwnerPartyMember));

            _playerManager.Skills.SetFieldSkillRestrictionEvaluator(
                skill => FieldSkillRestrictionEvaluator.CanUseSkill(
                    _mapBoard?.MapInfo,
                    skill,
                    _playerManager?.Player?.Build?.Job ?? 0,
                    IsMassacreSkillUsageDisabled())

                    && (_fieldRuleRuntime?.CanUseSkill(skill) ?? true));

            _playerManager.Skills.SetFieldSkillRestrictionMessageProvider(
                skill => FieldSkillRestrictionEvaluator.GetRestrictionMessage(
                    _mapBoard?.MapInfo,
                    skill,
                    _playerManager?.Player?.Build?.Job ?? 0,
                    IsMassacreSkillUsageDisabled())

                    ?? _fieldRuleRuntime?.GetSkillRestrictionMessage(skill));

            _playerManager.SetJumpRestrictionHandler(
                () => FieldInteractionRestrictionEvaluator.GetJumpRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0),
                () => FieldInteractionRestrictionEvaluator.GetJumpDownRestrictionMessage(_mapBoard?.MapInfo?.fieldLimit ?? 0),

                ShowFieldRestrictionMessage);

        }

        private bool IsMassacreSkillUsageDisabled()
        {
            return _specialFieldRuntime?.SpecialEffects?.Massacre?.IsActive == true
                   && _specialFieldRuntime.SpecialEffects.Massacre.IsSkillDisabled;
        }



        private void ConfigureSkillUIBindings()

        {

            if (_playerManager?.Skills == null || uiWindowManager == null)

                return;



            _playerManager.Skills.OnSkillCast = HandlePlayerSkillCast;

            _playerManager.Skills.OnFieldSkillCastRejected = HandleFieldSkillCastRejected;

            _playerManager.Skills.OnSkillCooldownStarted = HandleSkillCooldownStarted;

            _playerManager.Skills.OnSkillCooldownBlocked = HandleSkillCooldownBlocked;

            _playerManager.Skills.OnSkillCooldownCompleted = HandleSkillCooldownCompleted;



            if (uiWindowManager.QuickSlotWindow != null)

            {

                _playerManager.Skills.SetInventoryRuntime(uiWindowManager.InventoryWindow as IInventoryRuntime);

                _playerManager.Skills.OnItemHotkeyUseRequested = TryUseInventoryItem;

                uiWindowManager.QuickSlotWindow.SetSkillManager(_playerManager.Skills);

                uiWindowManager.QuickSlotWindow.SetSkillLoader(_playerManager.SkillLoader);

                uiWindowManager.QuickSlotWindow.SetInventoryRuntime(uiWindowManager.InventoryWindow as IInventoryRuntime);

                uiWindowManager.QuickSlotWindow.SetMacroProvider(index => uiWindowManager.SkillMacroWindow?.GetMacro(index));

            }



            if (uiWindowManager.InventoryWindow is InventoryUI inventoryWindow)

            {

                inventoryWindow.SetItemConsumptionGuard(GetFieldItemUseRestrictionMessage);

                inventoryWindow.ItemConsumptionBlocked = ShowFieldRestrictionMessage;

            }



            if (uiWindowManager?.EquipWindow is EquipUIBigBang equipWindowBigBang)

            {

                equipWindowBigBang.EquipmentEquipGuard = GetBattlefieldEquipRestrictionMessage;

                equipWindowBigBang.EquipmentEquipBlocked = ShowFieldRestrictionMessage;

            }



            if (uiWindowManager.SkillMacroWindow != null)

            {

                uiWindowManager.SkillMacroWindow.SetSkillManager(_playerManager.Skills);

                uiWindowManager.SkillMacroWindow.SetSkillLoader(_playerManager.SkillLoader);

                uiWindowManager.SkillMacroWindow.OnMacroSaved = (_, _) => PersistSkillMacros();

                uiWindowManager.SkillMacroWindow.OnMacroDeleted = _ => PersistSkillMacros();

                _playerManager.Skills.SetMacroResolver(index => uiWindowManager.SkillMacroWindow.GetMacro(index));

                LoadPersistedSkillMacros();

                _playerManager.Skills.OnMacroPartyNotifyRequested = macroName =>

                {

                    //string speaker = _playerManager.Player?.Name ?? "Player";

                    string speaker = "Player";

                    _chat.AddIncomingTargetedMessage(MapSimulatorChatTargetType.Party, speaker, macroName, currTickCount);

                };

            }



            if (uiWindowManager.SkillWindow is SkillUI skillWindowClassic)

            {

                skillWindowClassic.SetSkillManager(_playerManager.Skills);

                return;

            }



            if (uiWindowManager.SkillWindow is not SkillUIBigBang skillWindow)

                return;



            skillWindow.SetSkillManager(_playerManager.Skills);

            skillWindow.SetCharacterLevel(_playerManager.Player?.Level ?? 1);

            skillWindow.OnSkillInvoked = skillId =>

            {

                _playerManager.Skills.TryCastSkill(skillId, currTickCount);

            };

            skillWindow.OnSkillLevelUpRequested = TryHandleSkillUiLevelUp;

            skillWindow.OnRideRequested = () =>

            {

                ShowCharacterInfoWindow("ride");

            };

            skillWindow.OnGuildSkillRequested = () =>

            {

                if (!GuildSkillRuntime.HasGuildMembership(_playerManager.Player?.Build))

                    return;



                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.GuildSkill) is not GuildSkillWindow)

                    return;



                WireGuildSkillWindowData();

                ShowWindowWithInheritedDirectionModeOwner(MapSimulatorWindowNames.GuildSkill);

            };

            RefreshSkillWindowShortcutState();



            foreach (var skill in _playerManager.Skills.GetAllSkills())

            {

                if (skill == null)

                    continue;



                int currentLevel = _playerManager.Skills.GetSkillLevel(skill.SkillId);

                if (currentLevel <= 0)

                    continue;



                skillWindow.UpdateSkillLevel(skill.SkillId, currentLevel, skill.MaxLevel);

            }



            skillWindow.RecalculateSkillPointsFromCurrentLevels();

            ApplyQuestGrantedSkillPointBonuses();

        }



        private void ConfigureQuestUiBindings()

        {

            if (uiWindowManager == null || _questUiBindingsConfigured)

            {

                return;

            }



            if (uiWindowManager.InventoryWindow is InventoryUI inventoryWindow)

            {

                _questRuntime.ConfigureMesoRuntime(inventoryWindow.GetMesoCount, inventoryWindow.TryConsumeMeso, inventoryWindow.AddMeso);

                _questRuntime.ConfigureInventoryRuntime(

                    GetInventoryWindowItemCount,

                    CanAcceptInventoryWindowItem,

                    TryConsumeInventoryWindowItem,

                    TryAddItemToInventoryWindow);

            }

            else

            {

                _questRuntime.ConfigureMesoRuntime(null, null, null);

                _questRuntime.ConfigureInventoryRuntime(null, null, null, null);

            }

            _questRuntime.ConfigureQuestActionRuntime(
                TryApplyQuestBuffItemReward,
                () => _mapBoard?.MapInfo?.id ?? 0,
                mapId => ResolveMapTransferDisplayName(mapId, null));



            _questRuntime.ConfigureSkillRuntime(

                skillId => _playerManager?.Skills?.GetSkillLevel(skillId) ?? 0,

                ApplyQuestSkillReward,

                skillId => _playerManager?.Skills?.GetSkillMasterLevel(skillId) ?? 0,

                ApplyQuestSkillMasterLevelReward,

                ResolveQuestSkillName,

                AddQuestGrantedSkillPoints);

            _questRuntime.ConfigurePetRuntime(

                (supportedPetItemIds, recallLimit) =>

                {

                    IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;

                    if (activePets == null || activePets.Count == 0)

                    {

                        return false;

                    }



                    if (recallLimit.HasValue && recallLimit.Value > 0 && activePets.Count > recallLimit.Value)

                    {

                        return false;

                    }



                    if (supportedPetItemIds == null || supportedPetItemIds.Count == 0)

                    {

                        return true;

                    }



                    return activePets.Any(pet => pet != null && supportedPetItemIds.Contains(pet.ItemId));

                },

                (supportedPetItemIds, recallLimit, skillMask) =>

                    _playerManager?.Pets?.TryGrantSkillMask(supportedPetItemIds, recallLimit, skillMask, out _) == true);



            if (uiWindowManager.QuestWindow is QuestUI questWindow)

            {

                questWindow.SetFont(_fontChat);
                questWindow.SetQuestLogProvider((tab, showAllLevels) =>
                    BuildQuestLogSnapshotWithPacketState(tab, showAllLevels));
                questWindow.SetQuestPreferredSelectionProvider((tab, showAllLevels) =>
                    _questRuntime.GetPreferredQuestLogSelection(tab, _playerManager?.Player?.Build, showAllLevels));
                questWindow.QuestDetailRequested += OpenQuestDetailWindow;
            }



            if (uiWindowManager.QuestDetailWindow != null)

            {

                uiWindowManager.QuestDetailWindow.SetFont(_fontChat);

                uiWindowManager.QuestDetailWindow.SetItemIconProvider(LoadInventoryItemIcon);

                uiWindowManager.QuestDetailWindow.PreviousRequested += () => NavigateQuestDetail(-1);

                uiWindowManager.QuestDetailWindow.NextRequested += () => NavigateQuestDetail(1);

                uiWindowManager.QuestDetailWindow.ActionRequested += HandleQuestDetailAction;

            }



            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.QuestAlarm) is QuestAlarmWindow questAlarmWindow)

            {

                questAlarmWindow.SetFont(_fontChat);

                questAlarmWindow.SetSnapshotProvider(() => _questRuntime.BuildQuestAlarmSnapshot(_playerManager?.Player?.Build));

                questAlarmWindow.SetItemIconProvider(LoadInventoryItemIcon);

                questAlarmWindow.ConfigurePersistence(_questAlarmStore, () => _playerManager?.Player?.Build);

                questAlarmWindow.QuestRequested += OpenQuestFromAlarmWindow;

                questAlarmWindow.QuestLogRequested += OpenQuestLogFromAlarmWindow;

                questAlarmWindow.StatusMessageRequested += message =>
                {
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        _chat.AddMessage(message, new Color(255, 228, 151), Environment.TickCount);
                    }
                };

            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.QuestDelivery) is QuestDeliveryWindow questDeliveryWindow)
            {
                questDeliveryWindow.SetFont(_fontChat);
                questDeliveryWindow.DeliveryRequested += HandleQuestDeliveryWindowRequest;
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.ClassCompetition) is UtilityPanelWindow classCompetitionWindow)
            {
                classCompetitionWindow.SetFont(_fontChat);
                classCompetitionWindow.SetContentProvider(BuildClassCompetitionPageLines);
                classCompetitionWindow.SetFooterProvider(BuildClassCompetitionFooter);
            }

            if (uiWindowManager.GetWindow(MapSimulatorWindowNames.Radio) is UtilityPanelWindow radioWindow)
            {
                radioWindow.SetFont(_fontChat);
                radioWindow.SetContentProvider(BuildPacketOwnedRadioWindowLines);
                radioWindow.SetFooterProvider(BuildPacketOwnedRadioWindowFooter);
            }



            _questUiBindingsConfigured = true;

        }



        private void RefreshQuestUiState()

        {

            if (_activeQuestDetailQuestId != 0 && uiWindowManager.QuestDetailWindow?.IsVisible == true)

            {

                UpdateQuestDetailWindow();

            }

        }



        private void OpenQuestDetailWindow(int questId)

        {

            if (questId <= 0 || uiWindowManager?.QuestDetailWindow == null)

            {

                return;

            }



            _activeQuestDetailQuestId = questId;

            UpdateQuestDetailWindow();

            uiWindowManager.QuestDetailWindow.Show();

            uiWindowManager.BringToFront(uiWindowManager.QuestDetailWindow);

        }



        private void OpenQuestFromAlarmWindow(int questId)

        {

            if (questId <= 0 || uiWindowManager == null)

            {

                return;

            }



            uiWindowManager.ShowWindow(MapSimulatorWindowNames.Quest);

            SelectQuestInActiveWindow(questId);

            OpenQuestDetailWindow(questId);

        }



        private void OpenQuestLogFromAlarmWindow(int questId, bool focusSelectedQuest)

        {

            if (uiWindowManager == null)

            {

                return;

            }



            if (focusSelectedQuest)

            {

                uiWindowManager.ShowWindow(MapSimulatorWindowNames.Quest);

                if (questId > 0)

                {

                    SelectQuestInActiveWindow(questId);

                    OpenQuestDetailWindow(questId);

                }



                return;

            }



            UIWindowBase questWindow = uiWindowManager.GetWindow(MapSimulatorWindowNames.Quest);

            if (questWindow?.IsVisible == true)

            {

                uiWindowManager.HideWindow(MapSimulatorWindowNames.Quest);

                return;

            }



            uiWindowManager.ShowWindow(MapSimulatorWindowNames.Quest);

            if (questId > 0)

            {

                SelectQuestInActiveWindow(questId);

            }

        }



        private void UpdateQuestDetailWindow()

        {

            if (_activeQuestDetailQuestId <= 0 || uiWindowManager?.QuestDetailWindow == null)

            {

                return;

            }



            IReadOnlyList<int> questIds = GetVisibleQuestIdsForCurrentTab();

            if (questIds.Count == 0)

            {

                uiWindowManager.QuestDetailWindow.SetDetailState(null, -1, 0);

                return;

            }



            int navigationIndex = -1;

            for (int i = 0; i < questIds.Count; i++)

            {

                if (questIds[i] == _activeQuestDetailQuestId)

                {

                    navigationIndex = i;

                    break;

                }

            }



            if (navigationIndex < 0)

            {

                _activeQuestDetailQuestId = questIds[0];

                navigationIndex = 0;

                SelectQuestInActiveWindow(_activeQuestDetailQuestId);

            }



            QuestWindowDetailState state = GetQuestWindowDetailStateWithPacketState(_activeQuestDetailQuestId);
            _questRuntime.RecordQuestDetailViewed(_activeQuestDetailQuestId);
            uiWindowManager.QuestDetailWindow.SetDetailState(state, navigationIndex, questIds.Count);
        }



        private IReadOnlyList<int> GetVisibleQuestIdsForCurrentTab()

        {

            return uiWindowManager?.QuestWindow is QuestUI questWindow

                ? questWindow.GetCurrentTabQuestIds()

                : Array.Empty<int>();

        }



        private void SelectQuestInActiveWindow(int questId)

        {

            if (uiWindowManager?.QuestWindow is QuestUI questWindow)

            {

                questWindow.SelectQuestById(questId);

            }

        }



        private void NavigateQuestDetail(int direction)

        {

            IReadOnlyList<int> questIds = GetVisibleQuestIdsForCurrentTab();

            if (questIds.Count <= 1)

            {

                return;

            }



            int currentIndex = -1;

            for (int i = 0; i < questIds.Count; i++)

            {

                if (questIds[i] == _activeQuestDetailQuestId)

                {

                    currentIndex = i;

                    break;

                }

            }

            if (currentIndex < 0)

            {

                currentIndex = 0;

            }



            int nextIndex = Math.Max(0, Math.Min(questIds.Count - 1, currentIndex + direction));

            if (nextIndex == currentIndex)

            {

                return;

            }



            _activeQuestDetailQuestId = questIds[nextIndex];

            SelectQuestInActiveWindow(_activeQuestDetailQuestId);

            UpdateQuestDetailWindow();

        }



        private void HandleQuestDetailAction(QuestWindowActionKind action)

        {

            if (_activeQuestDetailQuestId <= 0)

            {

                return;

            }



            QuestWindowActionResult result = action switch
            {
                QuestWindowActionKind.Accept => _questRuntime.TryAcceptFromQuestWindow(_activeQuestDetailQuestId, _playerManager?.Player?.Build),
                QuestWindowActionKind.GiveUp => _questRuntime.TryGiveUpFromQuestWindow(_activeQuestDetailQuestId),
                QuestWindowActionKind.Complete => _questRuntime.TryCompleteFromQuestWindow(_activeQuestDetailQuestId, _playerManager?.Player?.Build),
                QuestWindowActionKind.Track => TrackQuestInAlarmWindow(_activeQuestDetailQuestId),
                QuestWindowActionKind.LocateNpc => LocateQuestNpcFromDetailWindow(_activeQuestDetailQuestId),
                QuestWindowActionKind.LocateMob => LocateQuestMobFromDetailWindow(_activeQuestDetailQuestId),
                QuestWindowActionKind.QuestDeliveryAccept => HandleQuestDetailDeliveryAction(_activeQuestDetailQuestId, false),
                QuestWindowActionKind.QuestDeliveryComplete => HandleQuestDetailDeliveryAction(_activeQuestDetailQuestId, true),
                _ => null
            };
            HandleQuestWindowActionResult(result);

        }

        private void HandleQuestWindowActionResult(QuestWindowActionResult result)
        {
            if (result?.PendingRewardChoicePrompt != null)
            {
                OpenQuestRewardChoicePrompt(result.PendingRewardChoicePrompt, PendingQuestRewardChoiceSource.QuestWindow);
                return;
            }

            if (result?.Messages != null)
            {
                for (int i = 0; i < result.Messages.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(result.Messages[i]))
                    {
                        _chat.AddMessage(result.Messages[i], new Color(255, 228, 151), currTickCount);
                    }
                }
            }

            if (result?.StateChanged == true)
            {
                RefreshQuestUiState();
                SelectQuestInActiveWindow(_activeQuestDetailQuestId);
                UpdateQuestDetailWindow();
            }
        }

        private void HandleNpcOverlayQuestActionResult(QuestActionResult result, int questId)
        {
            if (result?.PendingRewardChoicePrompt != null)
            {
                OpenQuestRewardChoicePrompt(result.PendingRewardChoicePrompt, PendingQuestRewardChoiceSource.NpcOverlay);
                return;
            }

            if (result?.Messages != null)
            {
                for (int i = 0; i < result.Messages.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(result.Messages[i]))
                    {
                        _chat.AddMessage(result.Messages[i], new Color(255, 228, 151), currTickCount);
                    }
                }
            }

            if (result?.StateChanged == true)
            {
                RefreshQuestUiState();
                SelectQuestInActiveWindow(result.PreferredQuestId ?? questId);
                UpdateQuestDetailWindow();

                PublishDynamicObjectTagStatesForScriptNames(result.PublishedScriptNames, currTickCount);
                ShowNpcQuestFeedback(result, currTickCount);

                if (_activeNpcInteractionNpc != null)
                {
                    OpenNpcInteraction(_activeNpcInteractionNpc, result.PreferredQuestId ?? questId);
                }
                else
                {
                    _npcInteractionOverlay?.Close();
                }
            }
        }



        private QuestWindowActionResult TrackQuestInAlarmWindow(int questId)

        {

            string fieldRestrictionMessage = GetFieldWindowRestrictionMessage(MapSimulatorWindowNames.QuestAlarm);
            if (!string.IsNullOrWhiteSpace(fieldRestrictionMessage))
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { fieldRestrictionMessage }
                };
            }

            if (questId <= 0 || uiWindowManager?.GetWindow(MapSimulatorWindowNames.QuestAlarm) is not QuestAlarmWindow questAlarmWindow)

            {

                return new QuestWindowActionResult

                {

                    QuestId = questId,

                    Messages = new[] { "Quest alarm window is not available in this UI build." }

                };

            }



            questAlarmWindow.TrackQuest(questId);

            questAlarmWindow.Show();

            uiWindowManager.BringToFront(questAlarmWindow);



            return new QuestWindowActionResult

            {

                QuestId = questId,

                Messages = new[] { "Tracking quest in the quest alarm window." }

            };

        }



        private QuestWindowActionResult LocateQuestNpcFromDetailWindow(int questId)
        {
            if (!_questRuntime.TryGetQuestWorldMapTarget(questId, _playerManager?.Player?.Build, out QuestWorldMapTarget target)

                || target == null)

            {

                return new QuestWindowActionResult

                {

                    QuestId = questId,

                    Messages = new[] { "This quest does not expose a world-map target in the loaded data." }

                };

            }



            RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldMap) is not WorldMapUI worldMapWindow)

            {

                return new QuestWindowActionResult

                {

                    QuestId = questId,

                    Messages = new[] { "World map window is not available in this UI build." }

                };

            }



            bool focused = false;

            string successMessage;

            string failureMessage;

            int targetMapId = target.MapId > 0 ? target.MapId : (_mapBoard?.MapInfo?.id ?? 0);



            switch (target.Kind)

            {

                case QuestWorldMapTargetKind.Mob:

                    focused = worldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Mob, target.Label, targetMapId);

                    successMessage = $"Opened the world map search for quest mob {target.Label}.";

                    failureMessage = $"Opened the world map, but {target.Label} is not in the current field search results.";

                    break;

                case QuestWorldMapTargetKind.Item:
                    if (_questRuntime.TryBuildQuestDemandItemQuery(questId, out QuestDemandItemQueryState itemQueryState)
                        && itemQueryState != null)
                    {
                        return new QuestWindowActionResult
                        {
                            QuestId = questId,
                            Messages = new[] { ApplyQuestDemandItemQueryLaunch(itemQueryState) }
                        };
                    }

                    if (!string.IsNullOrWhiteSpace(target.FallbackNpcName))
                    {
                        focused = worldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Npc, target.FallbackNpcName, targetMapId);
                        successMessage = $"Opened the world map for {target.FallbackNpcName}; {target.Label} still needs to be delivered.";
                        failureMessage = $"Opened the world map, but neither {target.FallbackNpcName} nor {target.Label} could be resolved in the current field search results.";

                    }

                    else
                    {
                        string deliveryMessage = ApplyDeliveryQuestLaunch(questId, target.EntityId ?? 0, Array.Empty<int>());
                        return new QuestWindowActionResult
                        {
                            QuestId = questId,
                            Messages = new[] { deliveryMessage }
                        };
                    }


                    break;

                default:

                    focused = worldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Npc, target.Label, targetMapId);

                    successMessage = $"Opened the world map search for {target.Label}.";

                    failureMessage = $"Opened the world map, but {target.Label} is not in the current field search results.";

                    break;

            }



            uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldMap);

            uiWindowManager.BringToFront(worldMapWindow);



            return new QuestWindowActionResult

            {

                QuestId = questId,

                Messages = focused

                    ? new[] { successMessage }

                    : new[] { failureMessage }

            };

        }



        private QuestWindowActionResult LocateQuestMobFromDetailWindow(int questId)
        {
            QuestWindowDetailState state = GetQuestWindowDetailStateWithPacketState(questId);
            if (state?.TargetMobId is not int || string.IsNullOrWhiteSpace(state.TargetMobName))
            {
                return new QuestWindowActionResult

                {

                    QuestId = questId,

                    Messages = new[] { "This quest does not expose an incomplete mob demand in the loaded data." }

                };

            }



            RefreshWorldMapWindow(_mapBoard?.MapInfo?.id ?? 0);

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.WorldMap) is not WorldMapUI worldMapWindow)

            {

                return new QuestWindowActionResult

                {

                    QuestId = questId,

                    Messages = new[] { "World map window is not available in this UI build." }

                };

            }



            int targetMapId = _mapBoard?.MapInfo?.id ?? 0;
            IReadOnlyList<int> packetGuideMapIds = Array.Empty<int>();

            bool hasPacketGuideMaps = state.TargetMobId is int targetMobId

                && TryGetPacketQuestGuideMapIds(targetMobId, out packetGuideMapIds)

                && packetGuideMapIds.Count > 0;

            if (hasPacketGuideMaps)

            {

                targetMapId = packetGuideMapIds[0];

            }



            bool focused = worldMapWindow.FocusSearchResult(WorldMapUI.SearchResultKind.Mob, state.TargetMobName, targetMapId);

            uiWindowManager.ShowWindow(MapSimulatorWindowNames.WorldMap);

            uiWindowManager.BringToFront(worldMapWindow);



            return new QuestWindowActionResult
            {
                QuestId = questId,
                Messages = focused
                    ? new[] { hasPacketGuideMaps ? $"Opened the world map search for {state.TargetMobName} using packet-authored quest-guide maps." : $"Opened the world map search for {state.TargetMobName}." }
                    : new[] { hasPacketGuideMaps ? $"Opened the world map, but {state.TargetMobName} could not be focused even with packet-authored quest-guide maps." : $"Opened the world map, but {state.TargetMobName} is not in the current field search results." }
            };
        }

        private QuestWindowActionResult HandleQuestDetailDeliveryAction(int questId, bool completionPhase)
        {
            QuestWindowDetailState state = GetQuestWindowDetailStateWithPacketState(questId);
            if (state?.TargetItemId is not int targetItemId || targetItemId <= 0)
            {
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { "This quest does not expose an item-backed delivery target in the loaded data." }
                };
            }

            int cashItemId = state.DeliveryCashItemId ?? 0;
            string targetItemName = string.IsNullOrWhiteSpace(state.TargetItemName) ? $"Item {targetItemId}" : state.TargetItemName;
            string cashItemName = string.IsNullOrWhiteSpace(state.DeliveryCashItemName) ? $"Cash item {cashItemId}" : state.DeliveryCashItemName;
            if (cashItemId > 0 && HasInventoryItem(cashItemId))
            {
                string deliveryMessage = ApplyDeliveryQuestLaunch(questId, targetItemId, Array.Empty<int>());
                return new QuestWindowActionResult
                {
                    QuestId = questId,
                    Messages = new[] { $"{deliveryMessage} Routed from the quest-detail {(completionPhase ? "completion" : "accept")} delivery button for {targetItemName}." }
                };
            }

            ShowCashShopWindow();
            int fallbackCommoditySn = completionPhase ? 50200219 : 50200218;
            return new QuestWindowActionResult
            {
                QuestId = questId,
                Messages = new[] { $"Opened the Cash Shop fallback for {cashItemName} (client commodity {fallbackCommoditySn}) before guiding delivery for {targetItemName}." }
            };
        }


        private bool TryHandleSkillUiLevelUp(SkillDisplayData skill)

        {

            if (skill == null || _playerManager?.Skills == null)

                return false;



            int playerLevel = _playerManager.Player?.Level ?? 1;

            if (skill.RequiredCharacterLevel > 0 && playerLevel < skill.RequiredCharacterLevel)

                return false;



            if (skill.RequiredSkillId > 0)

            {

                int requiredLevel = Math.Max(1, skill.RequiredSkillLevel);

                if (_playerManager.Skills.GetSkillLevel(skill.RequiredSkillId) < requiredLevel)

                    return false;

            }



            int currentLevel = _playerManager.Skills.GetSkillLevel(skill.SkillId);

            int targetLevel = Math.Min(skill.MaxLevel, Math.Max(currentLevel, skill.CurrentLevel) + 1);

            if (targetLevel <= currentLevel)

                return false;



            _playerManager.Skills.SetSkillLevel(skill.SkillId, targetLevel);

            return true;

        }



        private void HandlePlayerSkillCast(SkillCastInfo castInfo)

        {

            _fieldRuleRuntime?.RegisterSuccessfulSkillUse(castInfo?.SkillData);



            int currentMapId = _mapBoard?.MapInfo?.id ?? -1;

            if (currentMapId < 0)

                return;



            if (IsMysticDoorSkill(castInfo)

                && TryResolveMysticDoorReturnTarget(out int returnMapId, out float returnX, out float returnY))

            {

                _temporaryPortalField?.TryCreateMysticDoor(castInfo, currentMapId, returnMapId, returnX, returnY);

            }



            _temporaryPortalField?.TryCreateOpenGate(castInfo, currentMapId);

        }



        public bool UpsertRemotePlayerAffectedArea(
            int objectId,
            int type,
            int ownerId,
            int skillId,
            int skillLevel,
            Rectangle worldBounds,
            short startDelayUnits = 0,
            int elementAttribute = 0,
            int phase = 0)
        {
            return _affectedAreaPool?.Upsert(
                new AffectedAreaCreateInfo(
                    objectId,
                    type,

                    ownerId,
                    skillId,
                    skillLevel,
                    worldBounds,
                    startDelayUnits,
                    elementAttribute,
                    phase,
                    AffectedAreaSourceKind.PlayerSkill),
                Environment.TickCount) == true;
        }


        public bool UpsertRemoteMobAffectedArea(
            int objectId,
            int type,
            int ownerId,
            int skillId,
            int skillLevel,
            Rectangle worldBounds,
            short startDelayUnits = 0,
            int elementAttribute = 0,
            int phase = 0)
        {
            return _affectedAreaPool?.Upsert(
                new AffectedAreaCreateInfo(
                    objectId,
                    type,

                    ownerId,
                    skillId,
                    skillLevel,
                    worldBounds,
                    startDelayUnits,
                    elementAttribute,
                    phase,
                    AffectedAreaSourceKind.MobSkill),
                Environment.TickCount) == true;
        }


        public bool UpsertRemoteAreaBuffItemAffectedArea(
            int objectId,
            int type,
            int ownerId,
            int itemId,
            Rectangle worldBounds,
            short startDelayUnits = 0,
            int elementAttribute = 0,
            int phase = 0)
        {
            return _affectedAreaPool?.Upsert(
                new AffectedAreaCreateInfo(
                    objectId,
                    type,

                    ownerId,
                    itemId,
                    1,
                    worldBounds,
                    startDelayUnits,
                    elementAttribute,
                    phase,
                    AffectedAreaSourceKind.AreaBuffItem),
                Environment.TickCount) == true;
        }


        public bool RemoveRemoteAffectedArea(int objectId)

        {

            return _affectedAreaPool?.Remove(objectId, Environment.TickCount) == true;

        }



        public void ClearRemoteAffectedAreas()

        {

            _affectedAreaPool?.Clear();

        }



        private void HandleFieldSkillCastRejected(SkillData skill, string message)

        {

            ShowFieldRestrictionMessage(message);

        }



        private void HandleSkillCooldownStarted(SkillData skill, int durationMs, int currentTime)

        {

            if (skill == null || durationMs <= 0 || _playerManager?.Skills == null)

                return;



            if (IsSkillRepresentedOnDirectHotkeyBar(skill.SkillId))

                return;



            string message = $"{ResolveSkillCooldownNotificationName(skill)} is cooling down. {FormatCooldownNotificationSeconds(durationMs)}.";

            ShowSkillCooldownNotification(skill, message, currentTime, addChat: false, SkillCooldownNoticeType.Started);

        }



        private void HandleSkillCooldownBlocked(SkillData skill, int remainingMs, int currentTime)

        {

            if (skill == null || remainingMs <= 0 || IsSkillRepresentedOnDirectHotkeyBar(skill.SkillId))

                return;



            if (_lastSkillCooldownBlockedMessageTimes.TryGetValue(skill.SkillId, out int lastMessageTime) &&

                currentTime - lastMessageTime < SKILL_COOLDOWN_BLOCKED_MESSAGE_COOLDOWN_MS)

            {

                return;

            }



            _lastSkillCooldownBlockedMessageTimes[skill.SkillId] = currentTime;

            string message = $"{ResolveSkillCooldownNotificationName(skill)} can be used in {FormatCooldownNotificationSeconds(remainingMs)}.";

            ShowSkillCooldownNotification(skill, message, currentTime, addChat: false, SkillCooldownNoticeType.Blocked);

        }



        private void HandleSkillCooldownCompleted(SkillData skill, int currentTime)

        {

            if (skill == null || _playerManager?.Skills == null)

                return;



            if (IsSkillRepresentedOnDirectHotkeyBar(skill.SkillId))

                return;



            _lastSkillCooldownBlockedMessageTimes.Remove(skill.SkillId);

            string message = $"{ResolveSkillCooldownNotificationName(skill)} is ready.";

            ShowSkillCooldownNotification(skill, message, currentTime, addChat: true, SkillCooldownNoticeType.Ready);

        }



        private void ShowFieldRestrictionMessage(string message)

        {

            if (string.IsNullOrWhiteSpace(message))

                return;



            bool isRepeatedMessage = string.Equals(_lastFieldRestrictionMessage, message, StringComparison.Ordinal);

            if (isRepeatedMessage && currTickCount - _lastFieldRestrictionMessageTime < FIELD_RULE_MESSAGE_COOLDOWN_MS)

                return;



            _lastFieldRestrictionMessage = message;

            _lastFieldRestrictionMessageTime = currTickCount;

            PushFieldRuleMessage(message, currTickCount, true);

        }



        private void ShowSkillCooldownNotification(SkillData skill, string message, int currentTime, bool addChat, SkillCooldownNoticeType noticeType)

        {

            if (string.IsNullOrWhiteSpace(message))

                return;



            _soundManager?.PlaySound(SkillCooldownNoticeSoundKey);

            _skillCooldownNoticeUI?.AddNotice(

                skill?.SkillId ?? 0,

                ResolveSkillCooldownNotificationName(skill),

                message,

                skill?.IconTexture ?? skill?.Icon?.Texture,

                noticeType,

                currentTime);



            if (addChat)

            {

                _chat.AddMessage(message, new Color(255, 228, 151), currentTime);

            }



            _fieldEffects?.AddWeatherMessage(message, WeatherEffectType.None, currentTime);

        }



        private static string ResolveSkillCooldownNotificationName(SkillData skill)

        {

            if (!string.IsNullOrWhiteSpace(skill?.Name))

                return skill.Name;



            return skill != null ? $"Skill {skill.SkillId}" : "Skill";

        }



        private static string FormatCooldownNotificationSeconds(int durationMs)

        {

            int seconds = Math.Max(1, (int)Math.Ceiling(durationMs / 1000f));

            return seconds == 1 ? "1 sec" : $"{seconds} sec";

        }

        private void HandleConnectionNoticeCancelRequested()

        {

            bool handled = false;



            if (_activeConnectionNoticeExpiresAt != int.MinValue &&

                _activeConnectionNoticeVariant is ConnectionNoticeWindowVariant.Loading or ConnectionNoticeWindowVariant.LoadingSingleGauge)

            {

                ClearActiveConnectionNotice();

                _loginTitleStatusMessage = "Dismissed the active connection notice.";

                handled = true;

            }

            else if (_selectorRequestKind != SelectorRequestKind.None)

            {

                string cancelledMessage = _selectorRequestKind == SelectorRequestKind.LoginWorldCheck

                    ? "Cancelled the pending world-selection request."

                    : "Cancelled the pending channel-entry request.";

                CancelWorldChannelSelectorRequest(cancelledMessage);

                _loginTitleStatusMessage = cancelledMessage;

                handled = true;

            }

            else if (_loginRuntime.CurrentStep == LoginStep.Title &&

                     _loginRuntime.PendingStep == LoginStep.WorldSelect)

            {

                if (_loginRuntime.CancelPendingStep("Connection notice cancel"))

                {

                    _loginTitleStatusMessage = "Cancelled the pending login bootstrap transition to world selection.";

                    handled = true;

                }

            }



            if (!handled)

            {

                return;

            }



            SyncLoginTitleWindow();

            RefreshWorldChannelSelectorWindows();

            SyncLoginEntryDialogs();

        }

        private bool IsSkillRepresentedOnDirectHotkeyBar(int skillId)

        {

            return skillId > 0 && _playerManager?.Skills?.IsSkillAssignedToDirectHotkey(skillId) == true;

        }



        private void LoadSkillCooldownNoticeUiFrame()

        {

            WzImage basicUiImage = Program.FindImage("UI", "Basic.img");

            WzSubProperty noticeSource = basicUiImage?["Notice3"] as WzSubProperty;

            if (noticeSource == null)

            {

                return;

            }



            Texture2D top = LoadUiCanvasTexture(noticeSource["t"] as WzCanvasProperty);

            Texture2D center = LoadUiCanvasTexture(noticeSource["c"] as WzCanvasProperty);

            Texture2D bottom = LoadUiCanvasTexture(noticeSource["s"] as WzCanvasProperty);

            _skillCooldownNoticeUI.SetFrameTextures(top, center, bottom);
        }

        private void LoadPacketOwnedHudNoticeUiFrame()
        {
            WzImage basicUiImage = Program.FindImage("UI", "Basic.img");
            WzImage statusBarUiImage = Program.FindImage("UI", "StatusBar2.img");

            WzSubProperty damageMeterSource = basicUiImage?["Notice3"] as WzSubProperty;
            WzSubProperty fieldHazardSource = basicUiImage?["Notice2"] as WzSubProperty ?? damageMeterSource;

            _packetOwnedHudNoticeUI.SetDamageMeterFrameTextures(
                LoadUiCanvasTexture(damageMeterSource?["t"] as WzCanvasProperty),
                LoadUiCanvasTexture(damageMeterSource?["c"] as WzCanvasProperty),
                LoadUiCanvasTexture(damageMeterSource?["s"] as WzCanvasProperty));

            _packetOwnedHudNoticeUI.SetFieldHazardFrameTextures(
                LoadUiCanvasTexture(fieldHazardSource?["t"] as WzCanvasProperty),
                LoadUiCanvasTexture(fieldHazardSource?["c"] as WzCanvasProperty),
                LoadUiCanvasTexture(fieldHazardSource?["s"] as WzCanvasProperty));

            _packetOwnedHudNoticeUI.SetNoticeIcon(
                LoadUiCanvasTexture(statusBarUiImage?["mainBar"]?["notice"] as WzCanvasProperty));
        }

        private Texture2D LoadUiCanvasTexture(WzCanvasProperty canvasProperty)
        {
            return canvasProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(GraphicsDevice);

        }



        private void InitializeFieldRuleRuntime(int currentTime, bool includeFirstUserEnterScript)

        {

            _fieldRuleRuntime = new FieldRuleRuntime(_mapBoard?.MapInfo, HasInventoryItem, includeFirstUserEnterScript);

            ApplyAmbientFieldWeather(currentTime);
            ApplyFieldRuntimeInteractionRestrictions();
            ApplyFieldRuntimeMinimapRestrictions();



            if (_fieldRuleRuntime == null || !_fieldRuleRuntime.IsActive)

            {

                _fieldRuleRuntime = null;

                return;

            }



            IReadOnlyList<string> entryMessages = _fieldRuleRuntime.Reset(currentTime);

            for (int i = 0; i < entryMessages.Count; i++)

            {

                PushFieldRuleMessage(entryMessages[i], currentTime, false);

            }

        }



        private void UpdateFieldRuleRuntime(int currentTime)

        {

            if (_fieldRuleRuntime == null)

                return;



            FieldRuleUpdateResult updateResult = _fieldRuleRuntime.Update(

                currentTime,

                _playerManager?.Player?.IsAlive == true,

                _gameState.PendingMapChange);



            if (updateResult == null)

                return;



            for (int i = 0; i < updateResult.Messages.Count; i++)

            {

                PushFieldRuleMessage(updateResult.Messages[i], currentTime, false, true);

            }



            for (int i = 0; i < updateResult.OverlayMessages.Count; i++)

            {

                PushFieldRuleMessage(updateResult.OverlayMessages[i], currentTime, true, false);

            }



            if (updateResult.EnvironmentalDamage > 0 && _playerManager?.Player?.IsAlive == true)

            {

                _playerManager.Player.TakeDamage(updateResult.EnvironmentalDamage, 0f, 0f);

                if (updateResult.TriggerDamageMist)

                {

                    _fieldEffects.TriggerDamageMist(0.45f, FIELD_RULE_DAMAGE_MIST_DURATION_MS, currentTime);

                }

            }



            FieldRuleEffectApplier.ApplyRecovery(_playerManager?.Player, updateResult);

            if (updateResult.TransferMapId > 0)

            {

                QueueFieldTransfer(updateResult.TransferMapId);

            }

        }



        private bool ShouldRunOnFirstUserEnterForCurrentMap()

        {

            int mapId = _mapBoard?.MapInfo?.id ?? 0;

            if (mapId <= 0 || string.IsNullOrWhiteSpace(_mapBoard?.MapInfo?.onFirstUserEnter))

            {

                return false;

            }



            return _consumedFirstUserEnterMaps.Add(mapId);

        }



        private void PushFieldRuleMessage(string message, int currentTime, bool showOverlay, bool addChat = true)

        {

            if (string.IsNullOrWhiteSpace(message))

                return;



            if (addChat)

            {

                _chat.AddMessage(message, new Color(255, 228, 151), currentTime);

            }

            if (showOverlay)

            {

                _fieldEffects.AddWeatherMessage(message, WeatherEffectType.None, currentTime);

            }

        }



        private bool QueueFieldTransfer(int targetMapId)

        {

            return QueueMapTransfer(targetMapId, null);

        }



        private void ApplyAmbientFieldWeather(int currentTime)

        {

            WeatherType ambientWeather = FieldEnvironmentEffectEvaluator.ResolveAmbientWeather(_mapBoard?.MapInfo);

            ToggleWeather(ambientWeather);



            if (ambientWeather == WeatherType.None)

            {

                _fieldEffects.StopWeather();

                return;

            }



            _fieldEffects.OnBlowWeather(

                ConvertToFieldWeatherEffect(ambientWeather),

                null,

                null,

                1f,

                -1,

                currentTime);

        }



        private static WeatherEffectType ConvertToFieldWeatherEffect(WeatherType weather)

        {

            return weather switch

            {

                WeatherType.Rain => WeatherEffectType.Rain,

                WeatherType.Snow => WeatherEffectType.Snow,

                WeatherType.Leaves => WeatherEffectType.Leaves,

                _ => WeatherEffectType.None

            };

        }



        private bool TryResolveMysticDoorReturnTarget(out int returnMapId, out float returnX, out float returnY)

        {

            returnMapId = -1;

            returnX = 0f;

            returnY = 0f;



            if (_mapBoard?.MapInfo == null || _loadMapCallback == null)

                return false;



            return TryResolveMysticDoorReturnTargetForMap(_mapBoard.MapInfo.id, out returnMapId, out returnX, out returnY);

        }

        private static bool TryResolveMysticDoorReturnTargetForMap(int sourceMapId, out int returnMapId, out float returnX, out float returnY)

        {

            returnMapId = -1;

            returnX = 0f;

            returnY = 0f;

            if (sourceMapId <= 0)

                return false;

            WzImage mapImage = TryGetMapImageForMetadataLookup(sourceMapId);

            if (mapImage == null)

                return false;

            bool shouldUnparse = !mapImage.Parsed;

            try

            {

                if (!mapImage.Parsed)

                {

                    mapImage.ParseImage();

                }

                string mapIdKey = sourceMapId.ToString().PadLeft(9, '0');
                if (Program.InfoManager?.MapsCache == null
                    || !Program.InfoManager.MapsCache.TryGetValue(mapIdKey, out Tuple<WzImage, string, string, string, MapleLib.WzLib.WzStructure.MapInfo> cachedMap)
                    || cachedMap?.Item5 == null)
                {
                    return false;
                }

                MapleLib.WzLib.WzStructure.MapInfo mapInfo = cachedMap.Item5;

                int configuredReturnMap = mapInfo.returnMap;

                if (configuredReturnMap <= 0 || configuredReturnMap == MapConstants.MaxMap)

                {

                    configuredReturnMap = mapInfo.forcedReturn;

                }



                if (configuredReturnMap <= 0 || configuredReturnMap == MapConstants.MaxMap)

                    return false;



                if (!TryResolveReturnMapPortalPosition(configuredReturnMap, out returnX, out returnY))

                    return false;



                returnMapId = configuredReturnMap;

                return true;
            }
            finally
            {
                if (shouldUnparse)
                {
                    mapImage.UnparseImage();
                }
            }
        }



        private static bool TryResolveReturnMapPortalPosition(int mapId, out float portalX, out float portalY)

        {

            portalX = 0f;

            portalY = 0f;



            WzImage mapImage = TryGetMapImageForMetadataLookup(mapId);

            if (mapImage == null)

            {

                return false;

            }



            bool shouldUnparse = !mapImage.Parsed;



            try

            {

                if (!mapImage.Parsed)

                {

                    mapImage.ParseImage();

                }



                if (TryResolvePortalPosition(mapImage, PortalType.TownPortalPoint, PortalType.TownPortalPoint.ToCode(), out portalX, out portalY) ||

                    TryResolvePortalPosition(mapImage, PortalType.StartPoint, null, out portalX, out portalY))

                {

                    return true;

                }

            }

            finally

            {

                if (shouldUnparse)

                {

                    mapImage.UnparseImage();

                }

            }



            return false;

        }



        private static WzImage TryGetMapImageForMetadataLookup(int mapId)

        {

            string mapIdKey = mapId.ToString().PadLeft(9, '0');

            if (Program.InfoManager?.MapsCache != null &&

                Program.InfoManager.MapsCache.TryGetValue(mapIdKey, out Tuple<WzImage, string, string, string, MapleLib.WzLib.WzStructure.MapInfo> cachedMap) &&

                cachedMap?.Item1 != null)

            {

                return cachedMap.Item1;

            }



            if (Program.DataSource == null)

            {

                return null;

            }



            string folderNum = mapIdKey[0].ToString();

            return Program.DataSource.GetImageByPath($"Map/Map/Map{folderNum}/{mapIdKey}.img")

                   ?? Program.DataSource.GetImage("Map", $"Map/Map{folderNum}/{mapIdKey}.img");

        }



        private static bool TryResolvePortalPosition(

            WzImage mapImage,

            PortalType preferredPortalType,

            string preferredPortalName,

            out float portalX,

            out float portalY)

        {

            portalX = 0f;

            portalY = 0f;



            if (mapImage?["portal"] is not WzSubProperty portalParent)

            {

                return false;

            }



            WzSubProperty fallbackPortal = null;

            foreach (WzImageProperty property in portalParent.WzProperties)

            {

                if (property is not WzSubProperty portal)

                {

                    continue;

                }



                int? portalTypeId = InfoTool.GetOptionalInt(portal["pt"]);

                PortalType? portalType = null;

                if (portalTypeId.HasValue &&

                    Program.InfoManager?.PortalEditor_TypeById != null &&

                    portalTypeId.Value >= 0 &&

                    portalTypeId.Value < Program.InfoManager.PortalEditor_TypeById.Count)

                {

                    portalType = Program.InfoManager.PortalEditor_TypeById[portalTypeId.Value];

                }



                string portalName = InfoTool.GetOptionalString(portal["pn"]);

                bool matchesType = portalType == preferredPortalType;

                bool matchesName = !string.IsNullOrWhiteSpace(preferredPortalName) &&

                                   string.Equals(portalName, preferredPortalName, StringComparison.OrdinalIgnoreCase);

                if (!matchesType && !matchesName)

                {

                    continue;

                }



                portalX = InfoTool.GetInt(portal["x"]);

                portalY = InfoTool.GetInt(portal["y"]);

                return true;

            }



            if (preferredPortalType == PortalType.StartPoint)

            {

                fallbackPortal = portalParent.WzProperties.OfType<WzSubProperty>().FirstOrDefault();

            }



            if (fallbackPortal == null)

            {

                return false;

            }



            portalX = InfoTool.GetInt(fallbackPortal["x"]);

            portalY = InfoTool.GetInt(fallbackPortal["y"]);

            return true;

        }



        private static bool IsMysticDoorSkill(SkillCastInfo castInfo)

        {

            return castInfo?.SkillData != null

                   && string.Equals(castInfo.SkillData.Name, "Mystic Door", StringComparison.OrdinalIgnoreCase);

        }



        private bool HasInventoryItem(int itemId)

        {

            if (itemId <= 0 || uiWindowManager?.InventoryWindow is not UI.IInventoryRuntime inventory)

            {

                return false;

            }



            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId);

            if (inventoryType != InventoryType.NONE)

            {

                return inventory.GetItemCount(inventoryType, itemId) > 0;

            }



            return inventory.GetItemCount(InventoryType.EQUIP, itemId) > 0

                   || inventory.GetItemCount(InventoryType.USE, itemId) > 0

                   || inventory.GetItemCount(InventoryType.SETUP, itemId) > 0

                   || inventory.GetItemCount(InventoryType.ETC, itemId) > 0

                   || inventory.GetItemCount(InventoryType.CASH, itemId) > 0;

        }



        private string GetFieldItemUseRestrictionMessage(InventoryType inventoryType, int itemId, int quantity)

        {

            if (itemId <= 0)

            {

                return null;

            }



            string battlefieldRestriction = GetBattlefieldItemRestrictionMessage(itemId);

            if (!string.IsNullOrWhiteSpace(battlefieldRestriction))

            {

                return battlefieldRestriction;

            }

            InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName);
            InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string itemDescription);

            ConsumableItemEffect consumableEffect = ResolveConsumableItemEffect(itemId);
            string fieldLimitRestriction = FieldInteractionRestrictionEvaluator.GetItemUseRestrictionMessage(
                _mapBoard?.MapInfo?.fieldLimit ?? 0,
                inventoryType,
                itemId,
                itemName,
                itemDescription,
                FieldInteractionRestrictionEvaluator.IsStatChangeConsumable(
                    consumableEffect.HasSupportedRecovery,
                    consumableEffect.HasSupportedTemporaryBuff,
                    consumableEffect.HasSupportedMorph,
                    consumableEffect.HasSupportedCure));
            if (!string.IsNullOrWhiteSpace(fieldLimitRestriction))
            {
                return fieldLimitRestriction;
            }



            return _fieldRuleRuntime?.GetItemUseRestrictionMessage(inventoryType, itemId, currTickCount);

        }



        private string GetBattlefieldEquipRestrictionMessage(int itemId)

        {

            return GetBattlefieldItemRestrictionMessage(itemId);

        }



        private string GetBattlefieldItemRestrictionMessage(int itemId)

        {

            BattlefieldField battlefield = _specialFieldRuntime?.SpecialEffects?.Battlefield;

            if (itemId <= 0

                || battlefield?.IsActive != true

                || !battlefield.IsItemBlockedForLocalTeam(itemId))

            {

                return null;

            }



            string teamText = battlefield.LocalTeamId switch

            {

                0 => "wolves",

                1 => "sheep",

                2 => "team 2",

                int numericTeam => $"team {numericTeam}",

                _ => "current team"

            };

            return $"Battlefield {teamText} restrictions block item {itemId}.";

        }



        private float ApplyBattlefieldMoveSpeedCap(float speed)

        {

            BattlefieldField battlefield = _specialFieldRuntime?.SpecialEffects?.Battlefield;

            if (battlefield?.IsActive != true)

            {

                return speed;

            }



            return battlefield.ApplyLocalMoveSpeedCap(speed);

        }



        private string GetPendingMapEntryRestrictionMessage(Board targetBoard)

        {

            if (targetBoard?.MapInfo == null)

            {

                return null;

            }



            FieldEntryRestrictionContext context = new(

                _playerManager?.Player?.Level ?? 1,

                _socialListRuntime.HasPartyAdmissionContext(),

                _socialListRuntime.HasExpeditionAdmissionContext());

            return FieldEntryRestrictionEvaluator.GetRestrictionMessage(targetBoard.MapInfo, context);

        }



        private bool TryTogglePortableChair(int itemId, out string message)

        {

            message = string.Empty;



            if (_playerManager?.Player == null || _playerManager.Loader == null)

            {

                message = "Player runtime is not available.";

                return false;

            }



            if (itemId <= 0)

            {

                message = $"Invalid chair item ID: {itemId}";

                return false;

            }



            if (InventoryItemMetadataResolver.ResolveInventoryType(itemId) != InventoryType.SETUP)

            {

                message = "Portable chairs must be setup/install items.";

                return false;

            }



            string fieldItemRestrictionMessage = GetFieldItemUseRestrictionMessage(InventoryType.SETUP, itemId, 1);

            if (!string.IsNullOrWhiteSpace(fieldItemRestrictionMessage))

            {

                message = fieldItemRestrictionMessage;

                ShowFieldRestrictionMessage(message);

                return false;

            }



            if (!HasInventoryItem(itemId))

            {

                message = $"Setup item {itemId} is not in the current inventory.";

                return false;

            }



            PortableChair activeChair = _playerManager.Player.Build?.ActivePortableChair;

            if (activeChair?.ItemId == itemId)

            {

                _playerManager.Player.ClearPortableChair();

                message = "Portable chair cleared.";

                return true;

            }



            PortableChair chair = _playerManager.Loader.LoadPortableChair(itemId);

            if (chair == null)

            {

                message = $"Unable to load portable chair data for item {itemId}.";

                return false;

            }



            if (!_playerManager.Player.TryActivatePortableChair(chair))

            {

                message = "Portable chairs can only be activated while standing on a foothold.";

                return false;

            }



            string ridingChairNote = chair.TamingMobItemId is int tamingMobItemId && tamingMobItemId > 0

                ? $" Riding-chair mount applied: {tamingMobItemId}."

                : string.Empty;

            message = $"Activated chair {chair.Name} ({itemId}).{ridingChairNote}";

            return true;

        }



        private bool TrySetPlayerJob(int jobId)

        {

            var build = _playerManager?.Player?.Build;

            if (build == null)

                return false;



            build.Job = jobId;

            build.JobName = SkillDataLoader.GetJobName(jobId);



            if (_playerManager?.Skills != null)

            {

                _playerManager.Skills.LoadSkillsForJob(jobId);

                _playerManager.Skills.LearnAllNonHiddenSkills();

            }



            RefreshSkillWindowForJob(jobId);

            return true;

        }



        private void RefreshSkillWindowForJob(int jobId)

        {

            if (uiWindowManager?.SkillWindow is not SkillUIBigBang skillWindow)

                return;



            UIWindowLoader.LoadSkillsForJob(skillWindow, jobId, GraphicsDevice);

            ConfigureSkillUIBindings();

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

                Job = player.Build?.JobName ?? "Beginner",

                EXP = player.Build?.Exp ?? 0,

                MaxEXP = player.Build?.ExpToNextLevel ?? 100

            };

        }



        private IReadOnlyList<StatusBarBuffRenderData> GetStatusBarBuffData(int currentTime)

        {

            if (_statusBarBuffRenderCacheTime == currentTime)

            {

                return _statusBarBuffRenderCache;

            }



            _statusBarBuffRenderCacheTime = currentTime;

            if (_playerManager?.Skills == null)

            {

                _statusBarBuffRenderCache.Clear();

                return Array.Empty<StatusBarBuffRenderData>();

            }



            IReadOnlyList<StatusBarBuffEntry> buffEntries = _playerManager.Skills.GetStatusBarBuffEntries(currentTime);

            if (buffEntries.Count == 0)

            {

                _statusBarBuffRenderCache.Clear();

                return Array.Empty<StatusBarBuffRenderData>();

            }



            for (int i = 0; i < buffEntries.Count; i++)

            {

                StatusBarBuffEntry buffEntry = buffEntries[i];

                StatusBarBuffRenderData renderData = i < _statusBarBuffRenderCache.Count

                    ? _statusBarBuffRenderCache[i]

                    : CreateAndAppendStatusBarBuffRenderData(_statusBarBuffRenderCache);

                renderData.SkillId = buffEntry.SkillId;

                renderData.SkillName = buffEntry.SkillName;

                renderData.Description = buffEntry.Description;

                renderData.IconKey = buffEntry.IconKey;

                renderData.IconTexture = buffEntry.IconTexture;

                renderData.RemainingMs = buffEntry.RemainingMs;

                renderData.DurationMs = buffEntry.DurationMs;

                renderData.SortOrder = buffEntry.SortOrder;

                renderData.FamilyDisplayName = buffEntry.FamilyDisplayName;

                renderData.TemporaryStatLabels = buffEntry.TemporaryStatLabels;

                renderData.TemporaryStatDisplayNames = buffEntry.TemporaryStatDisplayNames;

            }



            TrimReusableList(_statusBarBuffRenderCache, buffEntries.Count);

            return _statusBarBuffRenderCache;

        }



        private StatusBarChatUI.StatusBarPointNotificationState GetStatusBarPointNotificationState()

        {

            bool hasAbilityPoints = _playerManager?.Player?.Build?.AP > 0;

            bool hasSkillPoints = uiWindowManager?.SkillWindow is SkillUIBigBang skillWindow && skillWindow.HasAvailableSkillPoints();



            if (!hasAbilityPoints && !hasSkillPoints)

            {

                return null;

            }



            return new StatusBarChatUI.StatusBarPointNotificationState

            {

                ShowAbilityPointNotification = hasAbilityPoints,

                ShowSkillPointNotification = hasSkillPoints

            };

        }



        private IReadOnlyList<StatusBarCooldownRenderData> GetStatusBarCooldownData(int currentTime)

        {

            if (_statusBarCooldownRenderCacheTime == currentTime)

            {

                return _statusBarCooldownRenderCache;

            }



            _statusBarCooldownRenderCacheTime = currentTime;

            if (_playerManager?.Skills == null || _playerManager.SkillLoader == null)

            {

                _statusBarCooldownSortBuffer.Clear();

                _statusBarCooldownRenderCache.Clear();

                return Array.Empty<StatusBarCooldownRenderData>();

            }



            Dictionary<int, int> hotkeys = _playerManager.Skills.GetAllHotkeys();

            if (hotkeys.Count == 0)

            {

                _statusBarCooldownSortBuffer.Clear();

                _statusBarCooldownRenderCache.Clear();

                return Array.Empty<StatusBarCooldownRenderData>();

            }



            _statusBarCooldownSortBuffer.Clear();

            _statusBarProcessedCooldownSkills.Clear();

            int renderIndex = 0;

            foreach (KeyValuePair<int, int> hotkey in hotkeys)

            {

                int skillId = hotkey.Value;

                if (skillId <= 0 || !_statusBarProcessedCooldownSkills.Add(skillId))

                {

                    continue;

                }



                if (!_playerManager.Skills.IsOnCooldown(skillId, currentTime))

                {

                    continue;

                }



                SkillData skill = _playerManager.SkillLoader.LoadSkill(skillId);

                int remainingMs = _playerManager.Skills.GetCooldownRemaining(skillId, currentTime);

                int durationMs = _playerManager.Skills.GetCooldownDuration(skillId, currentTime);

                if (remainingMs <= 0)

                {

                    continue;

                }



                _playerManager.Skills.TryGetCooldownStartTime(skillId, out int cooldownStartTime);



                StatusBarCooldownRenderData renderData = renderIndex < _statusBarCooldownRenderCache.Count

                    ? _statusBarCooldownRenderCache[renderIndex]

                    : CreateAndAppendStatusBarCooldownRenderData(_statusBarCooldownRenderCache);

                renderData.SkillId = skillId;

                renderData.SkillName = skill?.Name;

                renderData.Description = skill?.Description;

                renderData.IconTexture = skill?.IconTexture;

                renderData.RemainingMs = remainingMs;

                renderData.DurationMs = Math.Max(remainingMs, durationMs);

                _statusBarCooldownSortBuffer.Add((renderData, cooldownStartTime, hotkey.Key));

                renderIndex++;

            }



            _statusBarCooldownSortBuffer.Sort(static (left, right) =>

            {

                int cooldownComparison = right.CooldownStartTime.CompareTo(left.CooldownStartTime);

                return cooldownComparison != 0 ? cooldownComparison : left.SortKey.CompareTo(right.SortKey);

            });



            for (int i = 0; i < _statusBarCooldownSortBuffer.Count; i++)

            {

                _statusBarCooldownRenderCache[i] = _statusBarCooldownSortBuffer[i].RenderData;

            }



            TrimReusableList(_statusBarCooldownRenderCache, _statusBarCooldownSortBuffer.Count);

            return _statusBarCooldownRenderCache;

        }



        private IReadOnlyList<StatusBarCooldownRenderData> GetStatusBarOffBarCooldownData(int currentTime)

        {

            if (_statusBarOffBarCooldownRenderCacheTime == currentTime)

            {

                return _statusBarOffBarCooldownRenderCache;

            }



            _statusBarOffBarCooldownRenderCacheTime = currentTime;

            if (_playerManager?.Skills == null)

            {

                _statusBarOffBarCooldownSortBuffer.Clear();

                _statusBarOffBarCooldownRenderCache.Clear();

                return Array.Empty<StatusBarCooldownRenderData>();

            }



            IReadOnlyList<int> activeCooldownSkillIds = _playerManager.Skills.GetActiveCooldownSkillIds(currentTime);

            if (activeCooldownSkillIds.Count == 0)

            {

                _statusBarOffBarCooldownSortBuffer.Clear();

                _statusBarOffBarCooldownRenderCache.Clear();

                return Array.Empty<StatusBarCooldownRenderData>();

            }



            _statusBarOffBarCooldownSortBuffer.Clear();

            int renderIndex = 0;

            foreach (int skillId in activeCooldownSkillIds)

            {

                if (_playerManager.Skills.IsSkillAssignedToDirectHotkey(skillId))

                {

                    continue;

                }



                SkillData skill = _playerManager.Skills.GetSkillData(skillId);

                int remainingMs = _playerManager.Skills.GetCooldownRemaining(skillId, currentTime);

                int durationMs = _playerManager.Skills.GetCooldownDuration(skillId, currentTime);

                if (remainingMs <= 0)

                {

                    continue;

                }



                _playerManager.Skills.TryGetCooldownStartTime(skillId, out int cooldownStartTime);

                StatusBarCooldownRenderData renderData = renderIndex < _statusBarOffBarCooldownRenderCache.Count

                    ? _statusBarOffBarCooldownRenderCache[renderIndex]

                    : CreateAndAppendStatusBarCooldownRenderData(_statusBarOffBarCooldownRenderCache);

                renderData.SkillId = skillId;

                renderData.SkillName = skill?.Name ?? $"Skill {skillId}";

                renderData.Description = skill?.Description ?? string.Empty;

                renderData.IconTexture = skill?.IconTexture ?? skill?.Icon?.Texture;

                renderData.RemainingMs = remainingMs;

                renderData.DurationMs = Math.Max(remainingMs, durationMs);

                _statusBarOffBarCooldownSortBuffer.Add((renderData, cooldownStartTime, skillId));

                renderIndex++;

            }



            _statusBarOffBarCooldownSortBuffer.Sort(static (left, right) =>

            {

                int cooldownComparison = right.CooldownStartTime.CompareTo(left.CooldownStartTime);

                return cooldownComparison != 0 ? cooldownComparison : left.SortKey.CompareTo(right.SortKey);

            });



            for (int i = 0; i < _statusBarOffBarCooldownSortBuffer.Count; i++)

            {

                _statusBarOffBarCooldownRenderCache[i] = _statusBarOffBarCooldownSortBuffer[i].RenderData;

            }



            TrimReusableList(_statusBarOffBarCooldownRenderCache, _statusBarOffBarCooldownSortBuffer.Count);

            return _statusBarOffBarCooldownRenderCache;

        }



        private StatusBarPreparedSkillRenderData GetPreparedSkillBarData(int currentTime, PreparedSkillHudSurface surface)

        {

            if (surface == PreparedSkillHudSurface.StatusBar && _preparedSkillStatusBarCacheTime == currentTime)

            {

                return _preparedSkillStatusBarCache.SkillId == 0 ? null : _preparedSkillStatusBarCache;

            }



            if (surface == PreparedSkillHudSurface.World && _preparedSkillWorldCacheTime == currentTime)

            {

                return _preparedSkillWorldCache.SkillId == 0 ? null : _preparedSkillWorldCache;

            }



            var preparedSkill = _playerManager?.Skills?.GetPreparedSkill();

            if (preparedSkill == null)

            {

                ClearPreparedSkillBarCache(surface, currentTime);

                return null;

            }



            if (!preparedSkill.ShowHudBar || preparedSkill.HudSurface != surface)

            {

                ClearPreparedSkillBarCache(surface, currentTime);

                return null;

            }



            StatusBarPreparedSkillRenderData renderData = surface == PreparedSkillHudSurface.World

                ? _preparedSkillWorldCache

                : _preparedSkillStatusBarCache;

            renderData.SkillId = preparedSkill.SkillId;

            renderData.SkillName = preparedSkill.SkillData?.Name;

            renderData.SkinKey = preparedSkill.HudSkinKey;

            renderData.Surface = preparedSkill.HudSurface;

            renderData.RemainingMs = Math.Max(0, preparedSkill.Duration - preparedSkill.Elapsed(currentTime));

            renderData.DurationMs = preparedSkill.Duration;

            renderData.GaugeDurationMs = preparedSkill.HudGaugeDurationMs;

            renderData.Progress = preparedSkill.Progress(currentTime);

            renderData.IsKeydownSkill = preparedSkill.IsKeydownSkill;

            renderData.IsHolding = preparedSkill.IsHolding;

            renderData.HoldElapsedMs = preparedSkill.HoldElapsed(currentTime);

            renderData.MaxHoldDurationMs = preparedSkill.MaxHoldDurationMs;

            renderData.TextVariant = preparedSkill.HudTextVariant;

            renderData.ShowText = preparedSkill.ShowHudText;

            Vector2 worldAnchor = Vector2.Zero;
            if (surface == PreparedSkillHudSurface.World

                && !TryResolvePreparedSkillWorldAnchor(preparedSkill, currentTime, out worldAnchor))

            {

                ClearPreparedSkillBarCache(surface, currentTime);

                return null;

            }



            renderData.WorldAnchor = surface == PreparedSkillHudSurface.World

                ? worldAnchor

                : Vector2.Zero;



            if (surface == PreparedSkillHudSurface.World)

            {

                _preparedSkillWorldCacheTime = currentTime;

            }

            else

            {

                _preparedSkillStatusBarCacheTime = currentTime;

            }



            return renderData;

        }



        private static StatusBarBuffRenderData CreateAndAppendStatusBarBuffRenderData(List<StatusBarBuffRenderData> cache)

        {

            StatusBarBuffRenderData renderData = new StatusBarBuffRenderData();

            cache.Add(renderData);

            return renderData;

        }



        private static StatusBarCooldownRenderData CreateAndAppendStatusBarCooldownRenderData(List<StatusBarCooldownRenderData> cache)

        {

            StatusBarCooldownRenderData renderData = new StatusBarCooldownRenderData();

            cache.Add(renderData);

            return renderData;

        }



        private void ClearPreparedSkillBarCache(PreparedSkillHudSurface surface, int currentTime)

        {

            StatusBarPreparedSkillRenderData renderData = surface == PreparedSkillHudSurface.World

                ? _preparedSkillWorldCache

                : _preparedSkillStatusBarCache;

            renderData.SkillId = 0;

            renderData.SkillName = null;

            renderData.SkinKey = "KeyDownBar";

            renderData.Surface = surface;

            renderData.RemainingMs = 0;

            renderData.DurationMs = 0;

            renderData.GaugeDurationMs = 0;

            renderData.Progress = 0f;

            renderData.IsKeydownSkill = false;

            renderData.IsHolding = false;

            renderData.HoldElapsedMs = 0;

            renderData.MaxHoldDurationMs = 0;

            renderData.TextVariant = PreparedSkillHudTextVariant.Default;

            renderData.ShowText = true;

            renderData.WorldAnchor = Vector2.Zero;



            if (surface == PreparedSkillHudSurface.World)

            {

                _preparedSkillWorldCacheTime = currentTime;

            }

            else

            {

                _preparedSkillStatusBarCacheTime = currentTime;

            }

        }



        private static void TrimReusableList<T>(List<T> items, int count)

        {

            if (items.Count > count)

            {

                items.RemoveRange(count, items.Count - count);

            }

        }



        private bool TryResolvePreparedSkillWorldAnchor(PreparedSkill preparedSkill, int currentTime, out Vector2 anchor)

        {

            anchor = Vector2.Zero;



            if (preparedSkill?.HudSurface != PreparedSkillHudSurface.World || _playerManager?.Player == null)

            {

                return false;

            }



            if (IsDragonPreparedSkill(preparedSkill.SkillId)

                && _playerManager.Dragon != null

                && _playerManager.Dragon.TryGetCurrentKeyDownBarAnchor(currentTime, out Vector2 dragonTop))

            {

                anchor = dragonTop;

                return true;

            }



            if (IsDragonPreparedSkill(preparedSkill.SkillId))

            {

                return false;

            }



            PlayerCharacter player = _playerManager.Player;

            Point? bodyOrigin = player.TryGetCurrentBodyOrigin(currentTime);

            Rectangle? frameBounds = player.TryGetCurrentFrameBounds(currentTime);

            if (bodyOrigin.HasValue && frameBounds.HasValue)

            {

                float topY = bodyOrigin.Value.Y + frameBounds.Value.Top;

                anchor = new Vector2(player.X, topY - 18f);

                return true;

            }



            anchor = new Vector2(player.X, player.Y - 80f);

            return true;

        }



        private static bool IsDragonPreparedSkill(int skillId)

        {

            return skillId is 22121000 or 22151001;

        }



        private static string GetPreparedSkillBarSkin(PreparedSkill preparedSkill)

        {

            return preparedSkill?.HudSkinKey ?? "KeyDownBar";

        }



        // NOTE: DrawDrops, DrawNpcs, DrawDebugOverlays moved to RenderingManager



        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private static MouseState GetEffectiveMouseState(MouseState mouseState, bool isWindowActive)

        {

            if (isWindowActive)

            {

                return mouseState;

            }



            return new MouseState(

                mouseState.X,

                mouseState.Y,

                mouseState.ScrollWheelValue,

                ButtonState.Released,

                ButtonState.Released,

                ButtonState.Released,

                ButtonState.Released,

                ButtonState.Released);

        }



        // NOTE: DrawTooltip, DrawVRFieldBorder, DrawLBFieldBorder, DrawBorder,

        // DrawScreenEffects, DrawPortalFadeOverlay, DrawExplosionRing, DrawThickLine,

        // DrawMotionBlurOverlay - All moved to RenderingManager

        #endregion



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



        private bool TryConfigureSelectWorldPacketPayload(string[] args, out string error, out string summary)

        {

            error = null;

            summary = null;



            byte? configuredResultCode = null;

            int? configuredWorldId = null;

            int? configuredChannelIndex = null;

            LoginSelectWorldResultProfile decodedProfile = null;



            foreach (string arg in args)

            {

                if (string.IsNullOrWhiteSpace(arg))

                {

                    continue;

                }



                if (TryParseSelectWorldPacketPayloadArgument(arg, out decodedProfile, out string payloadError))

                {

                    continue;

                }



                if (payloadError != null)

                {

                    error = payloadError;

                    return false;

                }



                if (!configuredResultCode.HasValue && byte.TryParse(arg, out byte resultCode))

                {

                    configuredResultCode = resultCode;

                    continue;

                }



                if (!configuredWorldId.HasValue && int.TryParse(arg, out int worldId) && worldId >= 0)

                {

                    configuredWorldId = worldId;

                    continue;

                }



                if (!configuredChannelIndex.HasValue && int.TryParse(arg, out int channel) && channel >= 1)

                {

                    configuredChannelIndex = channel - 1;

                    continue;

                }



                error = "Usage: /loginpacket selectworld [resultCode] [worldId] [channel] [payloadhex=<hex>|payloadb64=<base64>|clearpayload]";

                return false;

            }



            _loginPacketSelectWorldResultProfile = decodedProfile;

            _loginPacketSelectWorldResultCode = decodedProfile != null

                ? decodedProfile.ResultCode

                : configuredResultCode;

            _loginPacketSelectWorldTargetWorldId = configuredWorldId;

            _loginPacketSelectWorldTargetChannelIndex = configuredChannelIndex;



            if (args.Length == 0)

            {

                summary = "Using generated SelectWorldResult behavior.";

                return true;

            }



            string profileSummary = decodedProfile == null

                ? string.Empty

                : $" with packet-authored roster ({decodedProfile.Entries.Count} entries, {decodedProfile.SlotCount} slots)";

            summary = $"Configured SelectWorldResult code {FormatSelectorPacketCode(_loginPacketSelectWorldResultCode ?? 0)}"

                      + (_loginPacketSelectWorldTargetWorldId.HasValue ? $" for world {_loginPacketSelectWorldTargetWorldId.Value}" : string.Empty)

                      + (_loginPacketSelectWorldTargetChannelIndex.HasValue ? $", channel {_loginPacketSelectWorldTargetChannelIndex.Value + 1}" : string.Empty)

                      + profileSummary

                      + ".";

            return true;

        }



        private bool TryConfigureSelectCharacterResultPacketPayload(string[] args, out string error, out string summary)

        {

            error = null;

            summary = null;



            LoginSelectCharacterResultProfile decodedProfile = null;

            bool clearPayload = false;



            foreach (string arg in args)

            {

                if (string.IsNullOrWhiteSpace(arg))

                {

                    continue;

                }



                if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase) ||

                    arg.Equals("clear", StringComparison.OrdinalIgnoreCase))

                {

                    clearPayload = true;

                    continue;

                }



                if (TryParseBinaryPayloadArgument(arg, out byte[] payloadBytes, out string payloadError))

                {

                    if (!LoginSelectCharacterResultCodec.TryDecode(payloadBytes, out decodedProfile, out string decodeError))

                    {

                        error = decodeError ?? "SelectCharacterResult payload could not be decoded.";

                        return false;

                    }



                    continue;

                }



                if (payloadError != null &&

                    (arg.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase) ||

                     arg.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase)))

                {

                    error = payloadError;

                    return false;

                }



                error = "Usage: /loginpacket selectchar [payloadhex=<hex>|payloadb64=<base64>|clearpayload]";

                return false;

            }



            if (args.Length == 0 || clearPayload)

            {

                _loginPacketSelectCharacterResultProfile = null;

                summary = "Using generated SelectCharacterResult behavior.";

                return true;

            }



            _loginPacketSelectCharacterResultProfile = decodedProfile;

            if (decodedProfile == null)

            {

                error = "Usage: /loginpacket selectchar [payloadhex=<hex>|payloadb64=<base64>|clearpayload]";

                return false;

            }



            string profileSummary = decodedProfile.IsSuccess

                ? $"success for character {decodedProfile.CharacterId.GetValueOrDefault()} via {decodedProfile.EndpointText ?? "unknown endpoint"}"

                : $"result {FormatSelectorPacketCode(decodedProfile.ResultCode)} / response {decodedProfile.ResponseCode}";

            summary = $"Configured packet-authored SelectCharacterResult ({profileSummary}).";

            return true;

        }



        private bool TryConfigureSelectCharacterByVacResultPacketPayload(string[] args, out string error, out string summary)

        {

            error = null;

            summary = null;



            LoginSelectCharacterByVacResultProfile decodedProfile = null;

            bool clearPayload = false;



            foreach (string arg in args)

            {

                if (string.IsNullOrWhiteSpace(arg))

                {

                    continue;

                }



                if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase) ||

                    arg.Equals("clear", StringComparison.OrdinalIgnoreCase))

                {

                    clearPayload = true;

                    continue;

                }



                if (TryParseBinaryPayloadArgument(arg, out byte[] payloadBytes, out string payloadError))

                {

                    if (!LoginSelectCharacterByVacResultCodec.TryDecode(payloadBytes, out decodedProfile, out string decodeError))

                    {

                        error = decodeError ?? "SelectCharacterByVACResult payload could not be decoded.";

                        return false;

                    }



                    continue;

                }



                if (payloadError != null &&

                    (arg.StartsWith("payloadhex=", StringComparison.OrdinalIgnoreCase) ||

                     arg.StartsWith("payloadb64=", StringComparison.OrdinalIgnoreCase)))

                {

                    error = payloadError;

                    return false;

                }



                error = "Usage: /loginpacket vac [payloadhex=<hex>|payloadb64=<base64>|clearpayload]";

                return false;

            }



            if (args.Length == 0 || clearPayload)

            {

                _loginPacketSelectCharacterByVacResultProfile = null;

                summary = "Using generated SelectCharacterByVACResult behavior.";

                return true;

            }



            _loginPacketSelectCharacterByVacResultProfile = decodedProfile;

            if (decodedProfile == null)

            {

                error = "Usage: /loginpacket vac [payloadhex=<hex>|payloadb64=<base64>|clearpayload]";

                return false;

            }



            string profileSummary = decodedProfile.IsConnectSuccess

                ? $"success for character {decodedProfile.CharacterId.GetValueOrDefault()} via {decodedProfile.EndpointText ?? "unknown endpoint"}"

                : $"result {FormatSelectorPacketCode(decodedProfile.ResultCode)} / secondary {decodedProfile.SecondaryCode}";

            summary = $"Configured packet-authored SelectCharacterByVACResult ({profileSummary}).";

            return true;

        }

        private bool TryConfigureViewAllCharPacketPayload(string[] args, out string error, out string summary)

        {

            error = null;

            summary = null;

            LoginViewAllCharResultPacketProfile decodedProfile = null;



            if (args.Length == 0)

            {

                _loginPacketViewAllCharResultProfile = null;

                summary = "Using generated ViewAllCharResult behavior.";

                return true;

            }



            foreach (string arg in args)

            {

                if (string.IsNullOrWhiteSpace(arg))

                {

                    continue;

                }



                if (TryParseViewAllCharPacketPayloadArgument(arg, out decodedProfile, out string payloadError))

                {

                    continue;

                }



                if (payloadError != null)

                {

                    error = payloadError;

                    return false;

                }



                error = "Usage: /loginpacket viewallchar [payloadhex=<hex>|payloadb64=<base64>|clearpayload]";

                return false;

            }



            _loginPacketViewAllCharResultProfile = decodedProfile;

            if (decodedProfile == null)

            {

                summary = "Cleared packet-authored ViewAllCharResult payload.";

                return true;

            }



            string detail = decodedProfile.Kind switch

            {

                LoginViewAllCharResultKind.Header => $"header ({decodedProfile.RelatedServerCount} related servers, {decodedProfile.CharacterCount} characters)",

                LoginViewAllCharResultKind.Characters => $"character chunk ({decodedProfile.Entries.Count} entries from world {decodedProfile.WorldId})",

                LoginViewAllCharResultKind.Completion => "completion packet",

                _ => $"result code {decodedProfile.ResultCode}"

            };

            summary = $"Configured ViewAllCharResult {detail}.";

            return true;

        }



        private bool TryConfigureCreateNewCharacterPacketPayload(string[] args, out string error, out string summary)

        {

            error = null;

            summary = null;



            LoginCreateNewCharacterResultProfile decodedProfile = null;

            byte? configuredResultCode = null;

            bool usedPayload = false;



            if (args.Length == 0)

            {

                _loginPacketCreateNewCharacterResultProfile = null;

                _loginPacketDialogPrompts.Remove(LoginPacketType.CreateNewCharacterResult);

                summary = "Using generated CreateNewCharacterResult behavior.";

                return true;

            }



            foreach (string arg in args)

            {

                if (string.IsNullOrWhiteSpace(arg))

                {

                    continue;

                }



                if (TryParseCreateNewCharacterPacketPayloadArgument(arg, out decodedProfile, out string payloadError))

                {

                    usedPayload = true;

                    continue;

                }



                if (payloadError != null)

                {

                    error = payloadError;

                    return false;

                }



                if (!configuredResultCode.HasValue && byte.TryParse(arg, out byte resultCode))

                {

                    configuredResultCode = resultCode;

                    continue;

                }



                if (arg.Equals("clear", StringComparison.OrdinalIgnoreCase))

                {

                    _loginPacketCreateNewCharacterResultProfile = null;

                    _loginPacketDialogPrompts.Remove(LoginPacketType.CreateNewCharacterResult);

                    summary = "Cleared packet-authored CreateNewCharacterResult payload.";

                    return true;

                }



                return TryConfigureLoginPacketDialogPrompt(LoginPacketType.CreateNewCharacterResult, args, out error, out summary);

            }



            _loginPacketDialogPrompts.Remove(LoginPacketType.CreateNewCharacterResult);

            _loginPacketCreateNewCharacterResultProfile = decodedProfile

                ?? (configuredResultCode.HasValue

                    ? new LoginCreateNewCharacterResultProfile { ResultCode = configuredResultCode.Value }

                    : null);



            if (_loginPacketCreateNewCharacterResultProfile == null)

            {

                summary = "Cleared packet-authored CreateNewCharacterResult payload.";

                return true;

            }



            string detail = _loginPacketCreateNewCharacterResultProfile.IsSuccess

                ? $"success payload for {_loginPacketCreateNewCharacterResultProfile.CreatedCharacter?.Name ?? "a new character"}"

                : $"server code {FormatSelectorPacketCode(_loginPacketCreateNewCharacterResultProfile.ResultCode)}";

            summary = usedPayload

                ? $"Configured CreateNewCharacterResult {detail}."

                : $"Configured CreateNewCharacterResult {detail} without packet-owned starter data.";

            return true;

        }



        private bool TryConfigureExtraCharInfoPacketPayload(string[] args, out string error, out string summary)

        {

            error = null;

            summary = null;

            LoginExtraCharInfoResultProfile decodedProfile = null;



            if (args.Length == 0)

            {

                _loginPacketExtraCharInfoResultProfile = null;

                summary = "Using generated ExtraCharInfoResult behavior.";

                return true;

            }



            foreach (string arg in args)

            {

                if (string.IsNullOrWhiteSpace(arg))

                {

                    continue;

                }



                if (TryParseExtraCharInfoPacketPayloadArgument(arg, out decodedProfile, out string payloadError))

                {

                    continue;

                }



                if (payloadError != null)

                {

                    error = payloadError;

                    return false;

                }



                error = "Usage: /loginpacket extracharinfo [payloadhex=<hex>|payloadb64=<base64>|clearpayload]";

                return false;

            }



            _loginPacketExtraCharInfoResultProfile = decodedProfile;

            if (decodedProfile == null)

            {

                summary = "Cleared packet-authored ExtraCharInfoResult payload.";

                return true;

            }



            summary = $"Configured ExtraCharInfoResult for account {decodedProfile.AccountId} with flag {decodedProfile.ResultFlag}.";

            return true;

        }



        private bool TryConfigureLoginPacketDialogPrompt(LoginPacketType packetType, string[] args, out string error, out string summary)

        {

            error = null;

            summary = null;



            if (args.Length == 0)

            {

                _loginPacketDialogPrompts.Remove(packetType);

                summary = $"Using default dialog handling for {packetType}.";

                return true;

            }



            if (args.Length == 1 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))

            {

                _loginPacketDialogPrompts.Remove(packetType);

                summary = $"Cleared packet-authored dialog override for {packetType}.";

                return true;

            }



            if (!TryParseLoginPacketDialogPrompt(args, out LoginPacketDialogPromptConfiguration prompt, out error))

            {

                return false;

            }



            _loginPacketDialogPrompts[packetType] = prompt;

            summary = $"Configured {packetType} to use {prompt.Owner}."

                      + (prompt.NoticeTextIndex.HasValue ? $" Notice/text/{prompt.NoticeTextIndex.Value}." : string.Empty);

            return true;

        }



        private static bool TrySplitLoginPacketPromptArgument(string arg, out string key, out string value)

        {

            key = null;

            value = null;

            if (string.IsNullOrWhiteSpace(arg))

            {

                return false;

            }



            int separatorIndex = arg.IndexOf('=');

            if (separatorIndex < 0)

            {

                separatorIndex = arg.IndexOf(':');

            }



            if (separatorIndex <= 0)

            {

                return false;

            }



            key = arg[..separatorIndex].Trim().ToLowerInvariant();

            value = separatorIndex < arg.Length - 1

                ? arg[(separatorIndex + 1)..].Trim()

                : string.Empty;

            return !string.IsNullOrWhiteSpace(key);

        }



        private static string CollectLoginPacketPromptValue(string[] args, ref int index, string key, string initialValue)

        {

            StringBuilder builder = new StringBuilder(initialValue ?? string.Empty);

            while (index + 1 < args.Length &&

                   !TrySplitLoginPacketPromptArgument(args[index + 1], out string nextKey, out _))

            {

                if (builder.Length > 0)

                {

                    builder.Append(' ');

                }



                builder.Append(args[++index]);

            }



            return builder.ToString();

        }



        private static string DecodeLoginPacketPromptText(string value)

        {

            return string.IsNullOrWhiteSpace(value)

                ? string.Empty

                : value.Replace("\\n", "\r\n", StringComparison.Ordinal);

        }



        private static bool LoginEntryTryParseConnectionNoticeVariant(string value, out ConnectionNoticeWindowVariant variant)

        {

            variant = ConnectionNoticeWindowVariant.Notice;

            if (string.IsNullOrWhiteSpace(value))

            {

                return false;

            }



            string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty);

            return normalized.ToLowerInvariant() switch

            {

                "notice" => Assign(ConnectionNoticeWindowVariant.Notice, out variant),

                "noticecog" or "cog" => Assign(ConnectionNoticeWindowVariant.NoticeCog, out variant),

                "loading" => Assign(ConnectionNoticeWindowVariant.Loading, out variant),

                "loadingsinglegauge" or "singlegauge" or "sg" => Assign(ConnectionNoticeWindowVariant.LoadingSingleGauge, out variant),

                _ => Enum.TryParse(value, true, out variant),

            };

        }



        private static bool TryParseLoginUtilityButtonLayout(string value, out LoginUtilityDialogButtonLayout layout)

        {

            layout = LoginUtilityDialogButtonLayout.Ok;

            if (string.IsNullOrWhiteSpace(value))

            {

                return false;

            }



            string normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty);

            return normalized.ToLowerInvariant() switch

            {

                "ok" => Assign(LoginUtilityDialogButtonLayout.Ok, out layout),

                "yesno" => Assign(LoginUtilityDialogButtonLayout.YesNo, out layout),

                "accept" => Assign(LoginUtilityDialogButtonLayout.Accept, out layout),

                "nowlater" => Assign(LoginUtilityDialogButtonLayout.NowLater, out layout),

                "restartexit" => Assign(LoginUtilityDialogButtonLayout.RestartExit, out layout),



                "nexon" or "website" => Assign(LoginUtilityDialogButtonLayout.Nexon, out layout),

                _ => Enum.TryParse(value, true, out layout),

            };

        }



        private static bool Assign(ConnectionNoticeWindowVariant value, out ConnectionNoticeWindowVariant variant)

        {

            variant = value;

            return true;

        }



        private static bool Assign(LoginUtilityDialogButtonLayout value, out LoginUtilityDialogButtonLayout layout)

        {

            layout = value;

            return true;

        }



        private static bool TryParseSelectWorldPacketPayloadArgument(

            string arg,

            out LoginSelectWorldResultProfile profile,

            out string error)

        {

            profile = null;

            error = null;



            if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase))

            {

                return true;

            }



            const string payloadHexPrefix = "payloadhex=";

            const string payloadBase64Prefix = "payloadb64=";



            byte[] payloadBytes = null;

            if (arg.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))

            {

                if (!TryDecodeHexBytes(arg[payloadHexPrefix.Length..], out payloadBytes))

                {

                    error = "SelectWorldResult payloadhex must be valid hexadecimal bytes.";

                    return false;

                }

            }

            else if (arg.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))

            {

                try

                {

                    payloadBytes = Convert.FromBase64String(arg[payloadBase64Prefix.Length..]);

                }

                catch (FormatException)

                {

                    error = "SelectWorldResult payloadb64 must be valid Base64.";

                    return false;

                }

            }

            else

            {

                return false;

            }



            if (!LoginSelectWorldResultCodec.TryDecode(payloadBytes, out profile, out string decodeError))

            {

                error = decodeError;

                return false;

            }



            return true;

        }



        private static bool TryParseCheckUserLimitPayloadArgument(
            string arg,
            out byte resultCode,
            out byte? populationLevel,
            out bool clearPayload,
            out string error)
        {
            resultCode = 0;
            populationLevel = null;
            error = null;

            if (!TryDecodeRawPayloadBytesArgument(arg, "CheckUserLimitResult", out byte[] payloadBytes, out clearPayload, out error))
            {
                return false;
            }

            if (clearPayload)
            {
                return true;
            }

            return LoginSelectorPacketPayloadCodec.TryDecodeCheckUserLimitResult(payloadBytes, out resultCode, out populationLevel, out error);
        }

        private static bool TryParseLatestConnectedWorldPayloadArgument(
            string arg,
            out int worldId,
            out bool clearPayload,
            out string error)
        {
            worldId = -1;
            error = null;

            if (!TryDecodeRawPayloadBytesArgument(arg, "LatestConnectedWorld", out byte[] payloadBytes, out clearPayload, out error))
            {
                return false;
            }

            if (clearPayload)
            {
                return true;
            }

            return LoginSelectorPacketPayloadCodec.TryDecodeLatestConnectedWorld(payloadBytes, out worldId, out error);
        }

        private static bool TryParseRecommendWorldMessagePayloadArgument(
            string arg,
            out IReadOnlyList<LoginRecommendWorldMessageEntry> entries,
            out bool clearPayload,
            out string error)
        {
            entries = Array.Empty<LoginRecommendWorldMessageEntry>();
            error = null;

            if (!TryDecodeRawPayloadBytesArgument(arg, "RecommendWorldMessage", out byte[] payloadBytes, out clearPayload, out error))
            {
                return false;
            }

            if (clearPayload)
            {
                return true;
            }

            return LoginSelectorPacketPayloadCodec.TryDecodeRecommendWorldMessage(payloadBytes, out entries, out error);
        }

        private static bool TryParseViewAllCharPacketPayloadArgument(
            string arg,
            out LoginViewAllCharResultPacketProfile profile,
            out string error)
        {

            return TryDecodePacketPayloadArgument(

                arg,

                "ViewAllCharResult",

                LoginViewAllCharResultCodec.TryDecode,

                out profile,

                out error);

        }



        private static bool TryParseCreateNewCharacterPacketPayloadArgument(



            string arg,



            out LoginCreateNewCharacterResultProfile profile,



            out string error)



        {



            return TryDecodePacketPayloadArgument(



                arg,



                "CreateNewCharacterResult",



                LoginCreateNewCharacterResultCodec.TryDecode,



                out profile,



                out error);



        }







        private static bool TryParseExtraCharInfoPacketPayloadArgument(

            string arg,

            out LoginExtraCharInfoResultProfile profile,

            out string error)

        {

            return TryDecodePacketPayloadArgument(

                arg,

                "ExtraCharInfoResult",

                LoginExtraCharInfoResultCodec.TryDecode,

                out profile,

                out error);

        }



        private static bool TryDecodePacketPayloadArgument<TProfile>(
            string arg,
            string label,
            TryDecodePacketPayloadDelegate<TProfile> decoder,
            out TProfile profile,

            out string error)

            where TProfile : class

        {

            profile = null;

            error = null;



            if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase))

            {

                return true;

            }



            const string payloadHexPrefix = "payloadhex=";

            const string payloadBase64Prefix = "payloadb64=";



            byte[] payloadBytes = null;

            if (arg.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))

            {

                if (!TryDecodeHexBytes(arg[payloadHexPrefix.Length..], out payloadBytes))

                {

                    error = $"{label} payloadhex must be valid hexadecimal bytes.";

                    return false;

                }

            }

            else if (arg.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))

            {

                try

                {

                    payloadBytes = Convert.FromBase64String(arg[payloadBase64Prefix.Length..]);

                }

                catch (FormatException)

                {

                    error = $"{label} payloadb64 must be valid Base64.";

                    return false;

                }

            }

            else

            {

                return false;

            }



            if (!decoder(payloadBytes, out profile, out string decodeError))

            {

                error = decodeError;

                return false;

            }


            return true;
        }

        private static bool TryDecodeRawPayloadBytesArgument(
            string arg,
            string label,
            out byte[] payloadBytes,
            out bool clearPayload,
            out string error)
        {
            payloadBytes = null;
            clearPayload = false;
            error = null;

            if (string.IsNullOrWhiteSpace(arg))
            {
                return false;
            }

            if (arg.Equals("clearpayload", StringComparison.OrdinalIgnoreCase))
            {
                clearPayload = true;
                return true;
            }

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";

            if (arg.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryDecodeHexBytes(arg[payloadHexPrefix.Length..], out payloadBytes))
                {
                    error = $"{label} payloadhex must be valid hexadecimal bytes.";
                    return false;
                }

                return true;
            }

            if (arg.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    payloadBytes = Convert.FromBase64String(arg[payloadBase64Prefix.Length..]);
                    return true;
                }
                catch (FormatException)
                {
                    error = $"{label} payloadb64 must be valid Base64.";
                    return false;
                }
            }

            return false;
        }

        private delegate bool TryDecodePacketPayloadDelegate<TProfile>(byte[] data, out TProfile profile, out string error);


        private static bool TryParseWorldInfoPacketPayloadArgument(

            string arg,

            out LoginWorldInfoPacketProfile profile,

            out bool isTerminator,

            out string error)

        {

            profile = null;

            isTerminator = false;

            error = null;



            if (string.IsNullOrWhiteSpace(arg))

            {

                return false;

            }



            if (arg.Equals("end", StringComparison.OrdinalIgnoreCase))

            {

                isTerminator = true;

                return true;

            }



            const string payloadHexPrefix = "payloadhex=";

            const string payloadBase64Prefix = "payloadb64=";



            byte[] payloadBytes = null;

            if (arg.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))

            {

                if (!TryDecodeHexBytes(arg[payloadHexPrefix.Length..], out payloadBytes))

                {

                    error = "WorldInformation payloadhex must be valid hexadecimal bytes.";

                    return false;

                }

            }

            else if (arg.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))

            {

                try

                {

                    payloadBytes = Convert.FromBase64String(arg[payloadBase64Prefix.Length..]);

                }

                catch (FormatException)

                {

                    error = "WorldInformation payloadb64 must be valid Base64.";

                    return false;

                }

            }

            else

            {

                return false;

            }



            if (!LoginWorldInfoPacketCodec.TryDecode(payloadBytes, out profile, out isTerminator, out string decodeError))

            {

                error = decodeError;

                return false;

            }



            return true;

        }



        private static bool TryDecodeHexBytes(string text, out byte[] bytes)

        {

            bytes = null;

            if (string.IsNullOrWhiteSpace(text))

            {

                return false;

            }



            string normalized = new string(text.Where(ch => !char.IsWhiteSpace(ch) && ch != '-').ToArray());

            if ((normalized.Length & 1) != 0)

            {

                return false;

            }



            bytes = new byte[normalized.Length / 2];

            for (int i = 0; i < bytes.Length; i++)

            {

                if (!byte.TryParse(normalized.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))

                {

                    bytes = null;

                    return false;

                }

            }



            return true;

        }



        private LoginCreateNewCharacterResultProfile BuildGeneratedCreateNewCharacterResultProfile(
            string requestedName = null,
            int? requestedCharacterId = null)


        {



            CharacterLoader loader = _playerManager?.Loader;



            if (loader == null)



            {



                return null;



            }







            CharacterBuild starterBuild = _loginCreateCharacterFlow?.CreatePreviewBuild(loader) ?? loader.LoadRandom();



            if (starterBuild == null)



            {



                return null;



            }







            int nextCharacterId = Math.Max(

                requestedCharacterId ?? 1,

                _loginCharacterRoster.Entries

                    .Select(entry => entry?.Build?.Id ?? 0)

                    .DefaultIfEmpty(0)

                    .Max() + 1);

            int fieldMapId = ResolveLoginCharacterTargetMapId();

            string name = string.IsNullOrWhiteSpace(requestedName)
                ? BuildGeneratedLoginCharacterName()
                : requestedName.Trim();






            starterBuild.Id = nextCharacterId;



            starterBuild.Name = name;



            starterBuild.Level = Math.Max(1, starterBuild.Level);



            starterBuild.Job = _loginCreateCharacterFlow?.SelectedJob?.BeginnerJobId ?? 0;



            starterBuild.SubJob = _loginCreateCharacterFlow?.SelectedJob?.SubJob ?? 0;



            starterBuild.JobName = _loginCreateCharacterFlow?.SelectedJob?.Label ?? "Beginner";



            starterBuild.STR = Math.Max(12, starterBuild.STR);



            starterBuild.DEX = Math.Max(5, starterBuild.DEX);



            starterBuild.INT = Math.Max(4, starterBuild.INT);



            starterBuild.LUK = Math.Max(4, starterBuild.LUK);



            starterBuild.MaxHP = Math.Max(50, starterBuild.MaxHP);



            starterBuild.HP = Math.Max(50, starterBuild.HP);



            starterBuild.MaxMP = Math.Max(50, starterBuild.MaxMP);



            starterBuild.MP = Math.Max(50, starterBuild.MP);







            var equipmentBySlot = starterBuild.Equipment?



                .Where(entry => entry.Value != null)



                .ToDictionary(entry => entry.Key, entry => entry.Value.ItemId);



            LoginAvatarLook avatarLook = LoginAvatarLookCodec.CreateLook(



                starterBuild.Gender,



                starterBuild.Skin,



                starterBuild.Face?.ItemId ?? 0,



                starterBuild.Hair?.ItemId ?? 0,



                equipmentBySlot);







            return new LoginCreateNewCharacterResultProfile



            {



                ResultCode = 0,



                CreatedCharacter = new LoginSelectWorldCharacterEntry



                {



                    CharacterId = nextCharacterId,



                    Name = name,



                    Gender = starterBuild.Gender,



                    Skin = starterBuild.Skin,



                    FaceId = starterBuild.Face?.ItemId ?? 0,



                    HairId = starterBuild.Hair?.ItemId ?? 0,



                    Level = Math.Max(1, starterBuild.Level),



                    JobId = starterBuild.Job,



                    SubJob = starterBuild.SubJob,



                    Strength = starterBuild.STR,



                    Dexterity = starterBuild.DEX,



                    Intelligence = starterBuild.INT,



                    Luck = starterBuild.LUK,



                    AbilityPoints = starterBuild.AP,



                    HitPoints = starterBuild.HP,



                    MaxHitPoints = starterBuild.MaxHP,



                    ManaPoints = starterBuild.MP,



                    MaxManaPoints = starterBuild.MaxMP,



                    Experience = starterBuild.Exp,



                    Fame = starterBuild.Fame,



                    FieldMapId = fieldMapId,



                    Portal = 0,



                    PlayTime = 0,



                    OnFamily = false,



                    AvatarLook = avatarLook,



                    AvatarLookPacket = LoginAvatarLookCodec.Encode(avatarLook)



                }



            };



        }







        private LoginAccountDialogPacketProfile BuildGeneratedCheckDuplicatedIdResultProfile(string characterName)

        {

            string normalizedName = characterName?.Trim() ?? string.Empty;
            byte resultCode = _loginCharacterRoster.Entries.Any(entry =>

                    string.Equals(entry?.Build?.Name, normalizedName, StringComparison.OrdinalIgnoreCase))

                ? (byte)1

                : (byte)0;

            return new LoginAccountDialogPacketProfile

            {

                PacketType = LoginPacketType.CheckDuplicatedIdResult,

                RequestedName = normalizedName,

                ResultCode = resultCode,

            };

        }

        private static LoginAccountDialogPacketProfile BuildGeneratedDeleteCharacterResultProfile(int characterId)

        {

            if (characterId <= 0)

            {

                return null;

            }

            return new LoginAccountDialogPacketProfile

            {

                PacketType = LoginPacketType.DeleteCharacterResult,

                CharacterId = characterId,

                ResultCode = 0,

                Payload = Array.Empty<byte>()

            };

        }

        private string BuildGeneratedLoginCharacterName()



        {



            const string prefix = "Starter";



            int suffix = Math.Max(1, _loginCharacterRoster.Entries.Count + 1);



            string candidate = $"{prefix}{suffix}";



            HashSet<string> existingNames = _loginCharacterRoster.Entries



                .Select(entry => entry?.Build?.Name)



                .Where(name => !string.IsNullOrWhiteSpace(name))



                .ToHashSet(StringComparer.OrdinalIgnoreCase);







            while (existingNames.Contains(candidate))



            {



                suffix++;



                candidate = $"{prefix}{suffix}";



            }







            return candidate;



        }







        private static bool TryParseBinaryPayloadArgument(string arg, out byte[] payloadBytes, out string error)

        {

            payloadBytes = null;

            error = null;

            if (string.IsNullOrWhiteSpace(arg))

            {

                error = "Packet payload is missing.";

                return false;

            }



            const string payloadHexPrefix = "payloadhex=";

            const string payloadBase64Prefix = "payloadb64=";

            if (arg.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))

            {

                if (!TryDecodeHexBytes(arg[payloadHexPrefix.Length..], out payloadBytes))

                {

                    error = "Packet payloadhex must be valid hexadecimal bytes.";

                    return false;

                }



                return true;

            }



            if (arg.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))

            {

                try

                {

                    payloadBytes = Convert.FromBase64String(arg[payloadBase64Prefix.Length..]);

                    return true;

                }

                catch (FormatException)

                {

                    error = "Packet payloadb64 must be valid Base64.";

                    return false;

                }

            }



            error = "Packet payload must use payloadhex=.. or payloadb64=..";

            return false;

        }



        private static bool TryParseWorldInfoPacketProfile(string text, out LoginWorldInfoPacketProfile profile)

        {

            profile = null;

            if (string.IsNullOrWhiteSpace(text))

            {

                return false;

            }



            string[] segments = text.Split(':');

            if (segments.Length < 3 || segments.Length > 4)

            {

                return false;

            }



            if (!int.TryParse(segments[0], out int worldId) ||

                !int.TryParse(segments[1], out int visibleChannels) ||

                !int.TryParse(segments[2], out int occupancyPercent))

            {

                return false;

            }



            bool requiresAdult = segments.Length == 4 &&

                                 (segments[3].Equals("adult", StringComparison.OrdinalIgnoreCase) ||

                                  segments[3].Equals("on", StringComparison.OrdinalIgnoreCase) ||

                                  segments[3].Equals("true", StringComparison.OrdinalIgnoreCase) ||

                                  segments[3].Equals("1", StringComparison.OrdinalIgnoreCase));

            profile = new LoginWorldInfoPacketProfile(worldId, visibleChannels, occupancyPercent, requiresAdult);

            return true;

        }



        private static bool TryParseRecommendWorldMessageEntry(

            string[] args,

            ref int index,

            out int worldId,

            out string message)

        {

            worldId = -1;

            message = null;

            if (args == null || index < 0 || index >= args.Length)

            {

                return false;

            }



            string token = args[index];

            int separatorIndex = token.IndexOf('=');

            string worldIdToken = separatorIndex >= 0

                ? token[..separatorIndex]

                : token;

            if (!int.TryParse(worldIdToken, out worldId) || worldId < 0)

            {

                return false;

            }



            StringBuilder builder = new();

            if (separatorIndex >= 0 && separatorIndex < token.Length - 1)

            {

                builder.Append(token[(separatorIndex + 1)..]);

            }



            index++;

            while (index < args.Length && !IsRecommendWorldMessageToken(args[index]))

            {

                if (builder.Length > 0)

                {

                    builder.Append(' ');

                }



                builder.Append(args[index]);

                index++;

            }



            message = builder.ToString();

            return true;

        }



        private static bool IsRecommendWorldMessageToken(string token)

        {

            if (string.IsNullOrWhiteSpace(token))

            {

                return false;

            }



            int separatorIndex = token.IndexOf('=');

            string worldIdToken = separatorIndex >= 0

                ? token[..separatorIndex]

                : token;

            return int.TryParse(worldIdToken, out int worldId) && worldId >= 0;

        }



        private bool TryUpdateLowResourceWarningThreshold(string valueText, bool isHp, out string message)

        {

            if (!int.TryParse(valueText, out int thresholdPercent))

            {

                message = "Threshold must be an integer percentage between 0 and 100";

                return false;

            }



            if (thresholdPercent < 0 || thresholdPercent > 100)

            {

                message = "Threshold must be between 0 and 100";

                return false;

            }



            if (isHp)

            {

                _statusBarHpWarningThresholdPercent = thresholdPercent;

            }

            else

            {

                _statusBarMpWarningThresholdPercent = thresholdPercent;

            }



            statusBarUi?.SetLowResourceWarningThresholds(_statusBarHpWarningThresholdPercent, _statusBarMpWarningThresholdPercent);

            message = $"{(isHp ? "HP" : "MP")} warning threshold set to {thresholdPercent}%";

            return true;

        }



        private void SyncCoconutPacketInboxState()

        {

            if (_specialFieldRuntime.Minigames.Coconut.IsActive)

            {

                _coconutPacketInbox.Start();

            }

            else

            {

                _coconutPacketInbox.Stop();
                _coconutOfficialSessionBridge.Stop();

            }

        }



        private void SyncWeddingPacketInboxState()

        {

            if (_specialFieldRuntime.SpecialEffects.Wedding.IsActive)

            {

                _weddingPacketInbox.Start();

            }

            else

            {

                _weddingPacketInbox.Stop();
                ClearWeddingRemoteActorsFromSharedPool();

            }

        }



        private void SyncMemoryGamePacketInboxState()

        {

            if (_gameState.IsLoginMap)

            {

                _memoryGamePacketInbox.Stop();
                _memoryGameOfficialSessionBridge.Stop();

                return;

            }



            _memoryGamePacketInbox.Start();

        }



        private void SyncAriantArenaPacketInboxState()

        {

            if (_specialFieldRuntime.Minigames.AriantArena.IsActive)

            {

                _ariantArenaPacketInbox.Start();

            }

            else

            {

                _ariantArenaPacketInbox.Stop();

            }

        }



        private void SyncMonsterCarnivalPacketInboxState()

        {

            if (_specialFieldRuntime.Minigames.MonsterCarnival.IsVisible)

            {

                _monsterCarnivalPacketInbox.Start();

            }

            else

            {

                _monsterCarnivalPacketInbox.Stop();
                _monsterCarnivalOfficialSessionBridge.Stop();

            }

        }

        private static bool TryReadMobSkillLevelVector(WzSubProperty levelNode, int level, string propertyName, out Point value)

        {

            value = Point.Zero;
            if (levelNode?[level.ToString()] is not WzSubProperty levelProperty)

            {

                return false;

            }



            if (levelProperty[propertyName] is not WzVectorProperty vectorProperty)

            {

                return false;

            }



            value = new Point(vectorProperty.X.Value, vectorProperty.Y.Value);
            return true;

        }



        private void SyncMassacrePacketInboxState()

        {

            if (_specialFieldRuntime.SpecialEffects.Massacre.IsActive)

            {

                _massacrePacketInbox.Start();

            }

            else

            {

                _massacrePacketInbox.Stop();

            }

        }



        private void SyncDojoPacketInboxState()

        {

            if (_specialFieldRuntime.SpecialEffects.Dojo.IsActive)

            {

                _dojoPacketInbox.Start();

            }

            else

            {

                _dojoPacketInbox.Stop();

            }

        }



        private void SyncPartyRaidPacketInboxState()

        {

            if (_specialFieldRuntime.PartyRaid.IsActive)

            {

                _partyRaidPacketInbox.Start();

            }

            else

            {

                _partyRaidPacketInbox.Stop();

            }

        }



        private void SyncGuildBossTransportState()

        {

            if (_specialFieldRuntime.SpecialEffects.GuildBoss.IsActive)

            {

                _guildBossTransport.Start();

            }

            else

            {

                _guildBossTransport.Stop();
                _guildBossOfficialSessionBridge.Stop();

            }

        }



        private void DrainCoconutPacketInbox(int currentTickCount)

        {

            CoconutField field = _specialFieldRuntime.Minigames.Coconut;

            while (_coconutPacketInbox.TryDequeue(out CoconutPacketInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _coconutPacketInbox.RecordDispatchResult(

                        message.Source,

                        message.PacketType,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool applied = field.TryApplyPacket(message.PacketType, message.Payload, currentTickCount, out string errorMessage);

                _coconutPacketInbox.RecordDispatchResult(

                    message.Source,

                    message.PacketType,

                    applied,

                    applied ? field.DescribeStatus() : errorMessage);

            }

            while (_coconutOfficialSessionBridge.TryDequeue(out CoconutPacketInboxMessage bridgeMessage))

            {

                if (!field.IsActive)

                {

                    _coconutOfficialSessionBridge.RecordDispatchResult(

                        bridgeMessage.Source,

                        bridgeMessage.PacketType,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool applied = field.TryApplyPacket(bridgeMessage.PacketType, bridgeMessage.Payload, currentTickCount, out string bridgeErrorMessage);

                _coconutOfficialSessionBridge.RecordDispatchResult(

                    bridgeMessage.Source,

                    bridgeMessage.PacketType,

                    applied,

                    applied ? field.DescribeStatus() : bridgeErrorMessage);

            }

        }



        private void FlushPendingCoconutAttackRequests()

        {

            CoconutField field = _specialFieldRuntime?.Minigames?.Coconut;

            if (field?.IsActive != true)

            {

                return;

            }



            while (field.TryGetNextUndispatchedAttackPacketRequest(out CoconutField.AttackPacketRequest request))

            {

                if (_coconutOfficialSessionBridge.HasConnectedSession)

                {

                    if (!_coconutOfficialSessionBridge.TrySendAttackRequest(request, out _))

                    {

                        break;

                    }

                }

                else if (_coconutPacketInbox.HasConnectedClients)

                {

                    if (!_coconutPacketInbox.TrySendAttackRequest(request, out _))

                    {

                        break;

                    }

                }

                else

                {

                    break;

                }



                field.MarkAttackPacketRequestTransportDispatched(request);

            }

        }



        private void DrainMemoryGamePacketInbox(int currentTickCount)

        {

            MemoryGameField field = _specialFieldRuntime.Minigames.MemoryGame;

            while (_memoryGamePacketInbox.TryDequeue(out MemoryGamePacketInboxMessage message))

            {

                if (message == null)

                {

                    continue;

                }



                bool applied = field.TryDispatchMiniRoomPacket(message.Payload, currentTickCount, out string resultMessage);

                _memoryGamePacketInbox.RecordDispatchResult(

                    message.Source,

                    applied,

                    applied ? field.DescribeStatus() : resultMessage);



                if (applied)

                {

                    ShowMiniRoomWindow();

                }

            }

            while (_memoryGameOfficialSessionBridge.TryDequeue(out MemoryGamePacketInboxMessage bridgeMessage))

            {

                if (bridgeMessage == null)

                {

                    continue;

                }



                bool applied = field.TryDispatchMiniRoomPacket(bridgeMessage.Payload, currentTickCount, out string bridgeResultMessage);

                _memoryGameOfficialSessionBridge.RecordDispatchResult(

                    bridgeMessage.Source,

                    applied,

                    applied ? field.DescribeStatus() : bridgeResultMessage);



                if (applied)

                {

                    ShowMiniRoomWindow();

                }

            }

        }

        private void SyncTransportPacketInboxState()

        {

            if (IsTransitVoyageWrapperMap(_mapBoard?.MapInfo) && _transportField.HasRouteConfiguration)

            {

                _transportPacketInbox.Start();

            }

            else

            {

                _transportPacketInbox.Stop();

            }

        }



        private void DrainWeddingPacketInbox(int currentTickCount)

        {

            WeddingField field = _specialFieldRuntime.SpecialEffects.Wedding;

            while (_weddingPacketInbox.TryDequeue(out WeddingInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _weddingPacketInbox.RecordDispatchResult(message, success: false, "runtime inactive");

                    continue;

                }



                bool applied = TryApplyWeddingInboxMessage(field, message, out string resultMessage);

                _weddingPacketInbox.RecordDispatchResult(message, applied, applied ? field.DescribeStatus() : resultMessage);

            }

        }



        private void DrainAriantArenaPacketInbox(int currentTickCount)

        {

            AriantArenaField field = _specialFieldRuntime.Minigames.AriantArena;

            while (_ariantArenaPacketInbox.TryDequeue(out AriantArenaPacketInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _ariantArenaPacketInbox.RecordDispatchResult(

                        message.Source,

                        message.PacketType,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool applied = TryApplyAriantArenaInboxMessage(field, message, currentTickCount, out string resultMessage);

                _ariantArenaPacketInbox.RecordDispatchResult(message, applied, applied ? field.DescribeStatus() : resultMessage);

            }

        }



        private void DrawMapleTvOverlay(GameTime gameTime, int tickCount)

        {

            if (_gameState.HideUIMode ||

                uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapleTv) is not MapleTvWindow mapleTvWindow)

            {

                return;

            }



            mapleTvWindow.DrawWorldOverlay(

                _spriteBatch,

                _skeletonMeshRenderer,

                null,

                gameTime,

                _renderParams.RenderWidth,

                tickCount);

        }



        private bool TryApplyWeddingInboxMessage(WeddingField field, WeddingInboxMessage message, out string errorMessage)

        {

            errorMessage = null;

            if (field == null || message == null)

            {

                errorMessage = "Wedding inbox message is missing.";

                return false;

            }



            switch (message.Kind)

            {

                case WeddingInboxMessageKind.Packet:

                    return field.TryApplyPacket(message.PacketType, message.Payload, Environment.TickCount, out errorMessage);

                case WeddingInboxMessageKind.CoupleMove:

                case WeddingInboxMessageKind.CoupleAvatar:

                    if (!TryResolveWeddingParticipantId(field, message.ActorKey, out int participantId, out errorMessage))

                    {

                        return false;

                    }



                    CharacterBuild participantBuild = null;

                    if (message.Kind == WeddingInboxMessageKind.CoupleAvatar

                        && !TryCreateWeddingAvatarBuild(message.ActorKey, message.Payload, out participantBuild, out errorMessage))

                    {

                        return false;

                    }



                    bool participantConfigured = field.TryConfigureParticipantActor(

                        participantId,

                        message.Position,

                        participantBuild,

                        message.FacingRight,

                        message.ActionName,

                        out errorMessage);

                    if (participantConfigured)

                    {

                        SyncWeddingRemoteActorsToSharedPool(field);

                    }

                    return participantConfigured;



                case WeddingInboxMessageKind.GuestAddClone:

                    CharacterBuild guestClone = CreateWeddingAudienceClone(message.ActorKey);

                    if (guestClone == null)

                    {

                        errorMessage = "No local player build is available to clone for the remote wedding guest.";

                        return false;

                    }



                    field.UpsertAudienceParticipant(guestClone, message.Position ?? Vector2.Zero, message.FacingRight ?? true, message.ActionName);
                    SyncWeddingRemoteActorsToSharedPool(field);

                    return true;



                case WeddingInboxMessageKind.GuestAddAvatar:

                    if (!TryCreateWeddingAvatarBuild(message.ActorKey, message.Payload, out CharacterBuild guestAvatar, out errorMessage))

                    {

                        return false;

                    }



                    field.UpsertAudienceParticipant(guestAvatar, message.Position ?? Vector2.Zero, message.FacingRight ?? true, message.ActionName);
                    SyncWeddingRemoteActorsToSharedPool(field);

                    return true;



                case WeddingInboxMessageKind.GuestMove:

                    bool audienceMoved = field.TryMoveAudienceParticipant(

                        message.ActorKey,

                        message.Position ?? Vector2.Zero,

                        message.FacingRight,

                        message.ActionName,

                        out errorMessage);

                    if (audienceMoved)

                    {

                        SyncWeddingRemoteActorsToSharedPool(field);

                    }

                    return audienceMoved;



                case WeddingInboxMessageKind.GuestRemove:

                    if (!field.RemoveAudienceParticipant(message.ActorKey))

                    {

                        errorMessage = $"Wedding guest '{message.ActorKey}' does not exist.";

                        return false;

                    }



                    SyncWeddingRemoteActorsToSharedPool(field);

                    return true;



                case WeddingInboxMessageKind.GuestClear:

                    field.ClearAudienceParticipants();
                    SyncWeddingRemoteActorsToSharedPool(field);

                    return true;



                default:

                    errorMessage = $"Unsupported wedding inbox message kind: {message.Kind}";

                    return false;

            }

        }



        private bool TryApplyAriantArenaInboxMessage(AriantArenaField field, AriantArenaPacketInboxMessage message, int currentTickCount, out string errorMessage)

        {

            errorMessage = null;

            if (field == null || message == null)

            {

                errorMessage = "Ariant inbox message is missing.";

                return false;

            }



            switch (message.Kind)

            {

                case AriantArenaInboxMessageKind.Packet:

                    return field.TryApplyPacket(message.PacketType, message.Payload, currentTickCount, out errorMessage);



                case AriantArenaInboxMessageKind.ActorAddClone:

                    CharacterBuild clonedBuild = CreateAriantArenaRemoteClone(message.ActorName);

                    if (clonedBuild == null)

                    {

                        errorMessage = "No local player build is available to clone for the remote Ariant actor.";

                        return false;

                    }



                    field.UpsertRemoteParticipant(

                        clonedBuild,

                        message.Position ?? Vector2.Zero,

                        message.FacingRight ?? true,

                        message.ActionName);

                    return true;



                case AriantArenaInboxMessageKind.ActorAddAvatar:

                    if (!TryCreateAriantArenaAvatarBuild(message.ActorName, message.Payload, out CharacterBuild avatarBuild, out errorMessage))

                    {

                        return false;

                    }



                    field.UpsertRemoteParticipant(

                        avatarBuild,

                        message.Position ?? Vector2.Zero,

                        message.FacingRight ?? true,

                        message.ActionName);

                    return true;



                case AriantArenaInboxMessageKind.ActorMove:

                    return field.TryMoveRemoteParticipant(

                        message.ActorName,

                        message.Position ?? Vector2.Zero,

                        message.FacingRight,

                        message.ActionName,

                        out errorMessage);



                case AriantArenaInboxMessageKind.ActorRemove:

                    if (!field.RemoveRemoteParticipant(message.ActorName))

                    {

                        errorMessage = $"Remote Ariant actor '{message.ActorName}' does not exist.";

                        return false;

                    }



                    return true;



                case AriantArenaInboxMessageKind.ActorClear:

                    field.ClearRemoteParticipants();

                    return true;



                default:

                    errorMessage = $"Unsupported Ariant inbox message kind: {message.Kind}";

                    return false;

            }

        }



        private bool TryResolveWeddingParticipantId(WeddingField field, string actorKey, out int participantId, out string errorMessage)

        {

            participantId = 0;

            errorMessage = null;

            if (field == null)

            {

                errorMessage = "Wedding runtime is unavailable.";

                return false;

            }



            if (string.Equals(actorKey, "groom", StringComparison.OrdinalIgnoreCase))

            {

                participantId = field.GroomId;

            }

            else if (string.Equals(actorKey, "bride", StringComparison.OrdinalIgnoreCase))

            {

                participantId = field.BrideId;

            }



            if (participantId > 0)

            {

                return true;

            }



            errorMessage = $"Wedding {actorKey} ID is not set yet. Use /wedding progress first.";

            return false;

        }



        private CharacterBuild CreateWeddingAudienceClone(string actorName)

        {

            CharacterBuild template = _playerManager?.Player?.Build?.Clone();

            if (template == null)

            {

                return null;

            }



            template.Name = string.IsNullOrWhiteSpace(actorName) ? "Guest" : actorName.Trim();

            return template;

        }



        private bool TryCreateWeddingAvatarBuild(string actorName, byte[] avatarLookPayload, out CharacterBuild build, out string errorMessage)

        {

            build = null;

            errorMessage = null;



            if (_playerManager?.Loader == null)

            {

                errorMessage = "Character loader is not available for wedding avatar decoding.";

                return false;

            }



            if (avatarLookPayload == null || avatarLookPayload.Length == 0)

            {

                errorMessage = "Wedding AvatarLook payload is missing.";

                return false;

            }



            if (!LoginAvatarLookCodec.TryDecode(avatarLookPayload, out LoginAvatarLook avatarLook, out string avatarDecodeError))

            {

                errorMessage = avatarDecodeError ?? "AvatarLook payload could not be decoded.";

                return false;

            }



            CharacterBuild template = _playerManager?.Player?.Build?.Clone();

            build = _playerManager.Loader.LoadFromAvatarLook(avatarLook, template);

            if (build == null)

            {

                errorMessage = "Wedding AvatarLook payload could not be converted into a character build.";

                return false;

            }



            build.Name = string.IsNullOrWhiteSpace(actorName) ? build.Name : actorName.Trim();

            return true;

        }



        private CharacterBuild CreateAriantArenaRemoteClone(string actorName)

        {

            CharacterBuild template = _playerManager?.Player?.Build?.Clone();

            if (template != null)

            {

                template.Name = actorName;

            }



            return template;

        }



        private CharacterBuild BuildAriantArenaRemoteCharacter(LoginAvatarLook avatarLook, string actorName)
        {
            if (avatarLook == null || _playerManager?.Loader == null)
            {
                return null;
            }

            CharacterBuild template = _playerManager?.Player?.Build?.Clone();
            CharacterBuild build = _playerManager.Loader.LoadFromAvatarLook(avatarLook, template);
            if (build != null && !string.IsNullOrWhiteSpace(actorName))
            {
                build.Name = actorName.Trim();
            }

            return build;
        }

        private bool TryCreateAriantArenaAvatarBuild(string actorName, byte[] avatarLookPayload, out CharacterBuild build, out string errorMessage)
        {

            build = null;

            errorMessage = null;



            if (_playerManager?.Loader == null)

            {

                errorMessage = "Character loader is not available for Ariant avatar actor decoding.";

                return false;

            }



            if (avatarLookPayload == null || avatarLookPayload.Length == 0)

            {

                errorMessage = "Ariant actor AvatarLook payload is missing.";

                return false;

            }



            if (!LoginAvatarLookCodec.TryDecode(avatarLookPayload, out LoginAvatarLook avatarLook, out string avatarDecodeError))

            {

                errorMessage = avatarDecodeError ?? "AvatarLook payload could not be decoded.";

                return false;

            }



            CharacterBuild template = _playerManager?.Player?.Build?.Clone();

            build = _playerManager.Loader.LoadFromAvatarLook(avatarLook, template);

            if (build != null)

            {

                build.Name = actorName;

                return true;

            }



            errorMessage = "Ariant actor AvatarLook payload could not be converted into a character build.";

            return false;

        }



        private void DrainMonsterCarnivalPacketInbox(int currentTickCount)

        {

            MonsterCarnivalField field = _specialFieldRuntime.Minigames.MonsterCarnival;

            while (_monsterCarnivalPacketInbox.TryDequeue(out MonsterCarnivalPacketInboxMessage message))

            {

                if (!field.IsVisible)

                {

                    _monsterCarnivalPacketInbox.RecordDispatchResult(

                        message.Source,

                        message.PacketType,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool applied = field.TryApplyRawPacket(message.PacketType, message.Payload, currentTickCount, out string errorMessage);

                _monsterCarnivalPacketInbox.RecordDispatchResult(

                    message.Source,

                    message.PacketType,

                    applied,

                    applied ? field.DescribeStatus() : errorMessage);

            }

            while (_monsterCarnivalOfficialSessionBridge.TryDequeue(out MonsterCarnivalPacketInboxMessage bridgeMessage))

            {

                if (!field.IsVisible)

                {

                    _monsterCarnivalOfficialSessionBridge.RecordDispatchResult(

                        bridgeMessage.Source,

                        bridgeMessage.PacketType,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool applied = field.TryApplyRawPacket(bridgeMessage.PacketType, bridgeMessage.Payload, currentTickCount, out string bridgeErrorMessage);

                _monsterCarnivalOfficialSessionBridge.RecordDispatchResult(

                    bridgeMessage.Source,

                    bridgeMessage.PacketType,

                    applied,

                    applied ? field.DescribeStatus() : bridgeErrorMessage);

            }

        }



        private void DrainMassacrePacketInbox(int currentTickCount)

        {

            MassacreField field = _specialFieldRuntime.SpecialEffects.Massacre;

            while (_massacrePacketInbox.TryDequeue(out MassacrePacketInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _massacrePacketInbox.RecordDispatchResult(message.Source, message, success: false, result: "runtime inactive");

                    continue;

                }



                bool applied = TryApplyMassacreInboxMessage(field, message, currentTickCount, out string resultMessage);

                _massacrePacketInbox.RecordDispatchResult(

                    message.Source,

                    message,

                    applied,

                    applied ? field.DescribeStatus() : resultMessage);

            }

        }



        private static bool TryApplyMassacreInboxMessage(MassacreField field, MassacrePacketInboxMessage message, int currentTickCount, out string resultMessage)

        {

            resultMessage = field?.DescribeStatus() ?? "Massacre HUD inactive";

            if (field == null || message == null)

            {

                resultMessage = "Massacre inbox message is missing.";

                return false;

            }



            switch (message.Kind)

            {

                case MassacrePacketInboxMessageKind.Clock:

                    field.OnClock(2, message.Value1, currentTickCount);

                    return true;

                case MassacrePacketInboxMessageKind.ClockPayload:

                    return field.TryApplyClockPayload(message.Payload, currentTickCount, out resultMessage);

                case MassacrePacketInboxMessageKind.Info:

                    field.SetMassacreInfo(message.Value1, message.Value2, message.Value3, message.Value4, currentTickCount);

                    return true;

                case MassacrePacketInboxMessageKind.InfoPayload:

                    return field.TryApplyMassacreInfoPayload(message.Payload, currentTickCount, out resultMessage);

                case MassacrePacketInboxMessageKind.IncGauge:

                    field.OnMassacreIncGauge(message.Value1, currentTickCount);

                    return true;

                case MassacrePacketInboxMessageKind.Stage:

                    field.ShowCountEffectPresentation(message.Value1, currentTickCount);

                    return true;

                case MassacrePacketInboxMessageKind.Bonus:

                    field.ShowBonusPresentation(currentTickCount);

                    return true;

                case MassacrePacketInboxMessageKind.Result:

                    field.ShowResultPresentation(

                        message.ClearResult,

                        currentTickCount,

                        message.HasScoreOverride ? message.Value1 : null,

                        message.HasRankOverride ? message.Rank : null);

                    return true;

                case MassacrePacketInboxMessageKind.Packet:

                    return field.TryApplyPacket(message.PacketType, message.Payload, currentTickCount, out resultMessage);

                default:

                    resultMessage = $"Unsupported Massacre inbox message kind: {message.Kind}";

                    return false;

            }

        }



        private void DrainDojoPacketInbox(int currentTickCount)

        {

            DojoField field = _specialFieldRuntime.SpecialEffects.Dojo;

            while (_dojoPacketInbox.TryDequeue(out DojoPacketInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _dojoPacketInbox.RecordDispatchResult(message.Source, message, success: false, result: "runtime inactive");

                    continue;

                }



                bool applied = TryApplyDojoInboxMessage(field, message, currentTickCount, out string resultMessage);

                _dojoPacketInbox.RecordDispatchResult(

                    message.Source,

                    message,

                    applied,

                    applied ? field.DescribeStatus() : resultMessage);

            }

        }

        private void DrainTransportPacketInbox()

        {

            while (_transportPacketInbox.TryDequeue(out TransportationPacketInboxMessage message))

            {

                if (!IsTransitVoyageWrapperMap(_mapBoard?.MapInfo) || !_transportField.HasRouteConfiguration)

                {

                    _transportPacketInbox.RecordDispatchResult(message.Source, message, success: false, result: "runtime inactive");

                    continue;

                }



                bool applied = TryApplyTransportInboxMessage(message, out string resultMessage);
                _transportPacketInbox.RecordDispatchResult(

                    message.Source,

                    message,

                    applied,

                    applied ? _transportField.DescribeStatus() : resultMessage);

            }

        }

        private bool TryApplyTransportInboxMessage(TransportationPacketInboxMessage message, out string resultMessage)

        {

            resultMessage = _transportField.DescribeStatus();

            if (message == null)

            {

                resultMessage = "Transport inbox message is missing.";

                return false;

            }



            byte[] payload = message.Payload ?? Array.Empty<byte>();
            switch (message.PacketType)

            {

                case TransportationPacketInboxManager.PacketTypeContiMove:

                    if (payload.Length < 2)

                    {

                        resultMessage = "Transport OnContiMove payload must contain subtype and value bytes.";

                        return false;

                    }



                    return payload[0] switch

                    {

                        TransportationPacketInboxManager.ContiMoveStartShip => _transportField.TryApplyStartShipMovePacket(payload[1], out resultMessage),

                        TransportationPacketInboxManager.ContiMoveMoveField => _transportField.TryApplyMoveFieldPacket(payload[1], out resultMessage),

                        TransportationPacketInboxManager.ContiMoveEndShip => _transportField.TryApplyEndShipMovePacket(payload[1], out resultMessage),

                        _ => FailTransportMessage(
                            $"Ignored OnContiMove subtype {payload[0]}; client only handles 8, 10, and 12.",
                            out resultMessage)

                    };

                case TransportationPacketInboxManager.PacketTypeContiState:

                    if (payload.Length < 2)

                    {

                        resultMessage = "Transport OnContiState payload must contain state and value bytes.";

                        return false;

                    }



                    return _transportField.TryApplyContiState(payload[0], payload[1], out resultMessage);

                default:

                    resultMessage = $"Unsupported transport packet opcode: {message.PacketType}";

                    return false;

            }

        }

        private static bool FailTransportMessage(string message, out string resultMessage)

        {

            resultMessage = message;

            return false;

        }



        private static bool TryApplyDojoInboxMessage(DojoField field, DojoPacketInboxMessage message, int currentTickCount, out string resultMessage)

        {

            resultMessage = field?.DescribeStatus() ?? "Mu Lung Dojo HUD inactive";

            if (field == null || message == null)

            {

                resultMessage = "Dojo inbox message is missing.";

                return false;

            }



            switch (message.Kind)

            {

                case DojoPacketMessageKind.Energy:

                    field.SetEnergy(message.Value);

                    return true;



                case DojoPacketMessageKind.Clock:

                    field.OnClock(2, message.Value, currentTickCount);

                    return true;



                case DojoPacketMessageKind.Stage:

                    field.SetStage(message.Value, currentTickCount);

                    return true;



                case DojoPacketMessageKind.Clear:

                    if (string.Equals(message.Option, "none", StringComparison.OrdinalIgnoreCase))

                    {

                        field.ShowClearResult(currentTickCount, -1);

                        return true;

                    }



                    if (string.Equals(message.Option, "auto", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(message.Option))

                    {

                        field.ShowClearResultForNextFloor(currentTickCount);

                        return true;

                    }



                    field.ShowClearResult(currentTickCount, message.Value);

                    return true;



                case DojoPacketMessageKind.TimeOver:

                    field.ShowTimeOverResult(currentTickCount, message.Value);

                    return true;



                case DojoPacketMessageKind.RawPacket:

                    return field.TryApplyPacket(message.PacketType, message.Payload, currentTickCount, out resultMessage);



                default:

                    resultMessage = $"Unsupported Dojo inbox action: {message.Kind}";

                    return false;

            }

        }



        private void DrainPartyRaidPacketInbox(int currentTickCount)

        {

            PartyRaidField field = _specialFieldRuntime.PartyRaid;

            while (_partyRaidPacketInbox.TryDequeue(out PartyRaidPacketInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _partyRaidPacketInbox.RecordDispatchResult(

                        message.Source,

                        message.Scope,

                        message.Key,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool applied = message.Scope switch

                {

                    PartyRaidPacketScope.Field => field.OnFieldSetVariable(message.Key, message.Value),

                    PartyRaidPacketScope.Party => field.OnPartyValue(message.Key, message.Value),

                    PartyRaidPacketScope.Session => field.OnSessionValue(message.Key, message.Value),

                    PartyRaidPacketScope.Clock => TryApplyPartyRaidClockInbox(field, message, currentTickCount),

                    _ => false

                };



                _partyRaidPacketInbox.RecordDispatchResult(

                    message.Source,

                    message.Scope,

                    message.Key,

                    applied,

                    applied ? field.DescribeStatus() : $"key not accepted ({message.Key}={message.Value})");

            }

        }



        private static bool TryApplyPartyRaidClockInbox(PartyRaidField field, PartyRaidPacketInboxMessage message, int currentTickCount)

        {

            if (field == null || message == null)

            {

                return false;

            }



            if (string.Equals(message.Key, "clear", StringComparison.OrdinalIgnoreCase))

            {

                field.ClearClock();

                return true;

            }



            string clockValue = string.IsNullOrWhiteSpace(message.Value)

                ? message.Key

                : message.Value;

            if (!int.TryParse(clockValue, out int seconds))

            {

                return false;

            }



            field.OnClock(2, seconds, currentTickCount);

            return true;

        }



        private void DrainGuildBossTransport(int currentTickCount)

        {

            GuildBossField field = _specialFieldRuntime.SpecialEffects.GuildBoss;

            while (_guildBossOfficialSessionBridge.TryDequeue(out GuildBossPacketInboxMessage bridgeMessage))

            {

                if (!field.IsActive)

                {

                    _guildBossOfficialSessionBridge.RecordDispatchResult(

                        bridgeMessage.Source,

                        bridgeMessage.PacketType,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool bridgeApplied = field.TryApplyPacket(bridgeMessage.PacketType, bridgeMessage.Payload, currentTickCount, out string bridgeErrorMessage);

                _guildBossOfficialSessionBridge.RecordDispatchResult(

                    bridgeMessage.Source,

                    bridgeMessage.PacketType,

                    bridgeApplied,

                    bridgeApplied ? field.DescribeStatus() : bridgeErrorMessage);

            }

            while (_guildBossTransport.TryDequeue(out GuildBossPacketInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _guildBossTransport.RecordDispatchResult(

                        message.Source,

                        message.PacketType,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                bool applied = field.TryApplyPacket(message.PacketType, message.Payload, currentTickCount, out string errorMessage);

                _guildBossTransport.RecordDispatchResult(

                    message.Source,

                    message.PacketType,

                    applied,

                    applied ? field.DescribeStatus() : errorMessage);

            }

        }



        private void SyncCookieHousePointInboxState()

        {

            if (_specialFieldRuntime.CookieHouse.IsActive)

            {

                _cookieHousePointInbox.Start();

            }

            else

            {

                _cookieHousePointInbox.Stop();

            }

        }



        private void DrainCookieHousePointInbox()

        {

            CookieHouseField field = _specialFieldRuntime.CookieHouse;

            while (_cookieHousePointInbox.TryDequeue(out CookieHousePointInboxMessage message))

            {

                if (!field.IsActive)

                {

                    _cookieHousePointInbox.RecordDispatchResult(

                        message.Source,

                        success: false,

                        message: "runtime inactive");

                    continue;

                }



                SetCookieHouseContextPoint(message.Point);

                field.Update();

                _cookieHousePointInbox.RecordDispatchResult(

                    message.Source,

                    success: true,

                    message: field.DescribeStatus());

            }

        }



        private void SyncBattlefieldLocalAppearance()

        {

            BattlefieldField battlefield = _specialFieldRuntime.SpecialEffects.Battlefield;

            PlayerCharacter player = _playerManager?.Player;

            CharacterLoader loader = _playerManager?.Loader;

            CharacterBuild build = player?.Build;



            SyncBattlefieldLocalMinimapState(battlefield);



            if (!battlefield.IsActive || player == null || loader == null || build == null)

            {

                RestoreBattlefieldLocalAppearance();

                return;

            }



            int? teamId = battlefield.LocalTeamId;

            if (_battlefieldAppliedTeamId == teamId)

            {

                return;

            }



            if (!teamId.HasValue || !battlefield.TryGetTeamLookPreset(teamId.Value, out BattlefieldField.BattlefieldTeamLookPreset preset))

            {

                RestoreBattlefieldLocalAppearance();

                _battlefieldAppliedTeamId = teamId;

                return;

            }



            EnsureBattlefieldOriginalEquipmentSnapshot(build);

            if (_battlefieldOriginalSpeed == null)

            {

                _battlefieldOriginalSpeed = build.Speed;

            }



            foreach (EquipSlot slot in BattlefieldAppearanceSlots)

            {

                build.Unequip(slot);

            }



            if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Longcoat))

            {

                build.Unequip(EquipSlot.Coat);

                build.Unequip(EquipSlot.Pants);

            }

            else if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Coat))

            {

                build.Unequip(EquipSlot.Longcoat);

            }



            foreach (KeyValuePair<EquipSlot, int> entry in preset.EquipmentItemIds)

            {

                CharacterPart part = loader.LoadEquipment(entry.Value);

                if (part != null)

                {

                    build.Equip(part);

                }

            }



            if (preset.MoveSpeed.HasValue)

            {

                build.Speed = preset.MoveSpeed.Value;

            }



            player.Assembler?.ClearCache();

            _battlefieldAppliedTeamId = teamId;

        }



        private void SyncBattlefieldLocalMinimapState(BattlefieldField battlefield)

        {

            int? teamId = battlefield?.IsActive == true ? battlefield.LocalTeamId : null;

            if (_battlefieldAppliedMinimapTeamId == teamId)

            {

                return;

            }



            if ((teamId == 0 || teamId == 2) && miniMapUi?.IsCollapsed == true)

            {

                miniMapUi.EnsureExpanded();

            }



            _battlefieldAppliedMinimapTeamId = teamId;

        }



        private void EnsureBattlefieldOriginalEquipmentSnapshot(CharacterBuild build)

        {

            if (_battlefieldOriginalEquipment != null || build == null)

            {

                return;

            }



            _battlefieldOriginalEquipment = new Dictionary<EquipSlot, CharacterPart>();

            _battlefieldOriginalSpeed = build.Speed;

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)

            {

                if (build.Equipment.TryGetValue(slot, out CharacterPart part) && part != null)

                {

                    _battlefieldOriginalEquipment[slot] = part;

                }

            }

        }



        private void RestoreBattlefieldLocalAppearance()

        {

            PlayerCharacter player = _playerManager?.Player;

            CharacterBuild build = player?.Build;

            if (build == null)

            {

                _battlefieldOriginalEquipment = null;

                _battlefieldOriginalSpeed = null;

                _battlefieldAppliedTeamId = null;

                _battlefieldAppliedMinimapTeamId = null;

                return;

            }



            if (_battlefieldOriginalEquipment == null)

            {

                _battlefieldAppliedTeamId = null;

                if (_specialFieldRuntime.SpecialEffects.Battlefield.IsActive == false)

                {

                    _battlefieldAppliedMinimapTeamId = null;

                }

                return;

            }



            foreach (EquipSlot slot in BattlefieldAppearanceSlots)

            {

                build.Unequip(slot);

            }



            foreach (KeyValuePair<EquipSlot, CharacterPart> entry in _battlefieldOriginalEquipment)

            {

                build.Equip(entry.Value);

            }



            if (_battlefieldOriginalSpeed.HasValue)

            {

                build.Speed = _battlefieldOriginalSpeed.Value;

            }



            player.Assembler?.ClearCache();

            _battlefieldOriginalEquipment = null;

            _battlefieldOriginalSpeed = null;

            _battlefieldAppliedTeamId = null;

            if (_specialFieldRuntime.SpecialEffects.Battlefield.IsActive == false)

            {

                _battlefieldAppliedMinimapTeamId = null;

            }

        }



        private static bool TryParsePartyRaidOutcome(string text, out PartyRaidResultOutcome outcome)

        {

            if (string.Equals(text, "win", StringComparison.OrdinalIgnoreCase))

            {

                outcome = PartyRaidResultOutcome.Win;

                return true;

            }



            if (string.Equals(text, "lose", StringComparison.OrdinalIgnoreCase))

            {

                outcome = PartyRaidResultOutcome.Lose;

                return true;

            }



            if (string.Equals(text, "clear", StringComparison.OrdinalIgnoreCase))

            {

                outcome = PartyRaidResultOutcome.Clear;

                return true;

            }



            outcome = PartyRaidResultOutcome.Unknown;

            return false;

        }



        private static bool TryParseBattlefieldTeam(string text, out int? teamId)

        {

            if (string.IsNullOrWhiteSpace(text))

            {

                teamId = null;

                return false;

            }



            if (string.Equals(text, "clear", StringComparison.OrdinalIgnoreCase)

                || string.Equals(text, "none", StringComparison.OrdinalIgnoreCase))

            {

                teamId = null;

                return true;

            }



            if (string.Equals(text, "wolves", StringComparison.OrdinalIgnoreCase)

                || string.Equals(text, "wolf", StringComparison.OrdinalIgnoreCase))

            {

                teamId = 0;

                return true;

            }



            if (string.Equals(text, "sheep", StringComparison.OrdinalIgnoreCase)

                || string.Equals(text, "lamb", StringComparison.OrdinalIgnoreCase))

            {

                teamId = 1;

                return true;

            }



            if (int.TryParse(text, out int numericTeam) && numericTeam >= 0 && numericTeam <= 2)

            {

                teamId = numericTeam;

                return true;

            }



            teamId = null;

            return false;

        }



        private static bool TryParseBattlefieldWinner(string text, out BattlefieldField.BattlefieldWinner winner)

        {

            if (string.IsNullOrWhiteSpace(text)

                || string.Equals(text, "auto", StringComparison.OrdinalIgnoreCase))

            {

                winner = BattlefieldField.BattlefieldWinner.None;

                return true;

            }



            if (string.Equals(text, "wolves", StringComparison.OrdinalIgnoreCase)

                || string.Equals(text, "wolf", StringComparison.OrdinalIgnoreCase))

            {

                winner = BattlefieldField.BattlefieldWinner.Wolves;

                return true;

            }



            if (string.Equals(text, "sheep", StringComparison.OrdinalIgnoreCase)

                || string.Equals(text, "lamb", StringComparison.OrdinalIgnoreCase))

            {

                winner = BattlefieldField.BattlefieldWinner.Sheep;

                return true;

            }



            if (string.Equals(text, "draw", StringComparison.OrdinalIgnoreCase)

                || string.Equals(text, "tie", StringComparison.OrdinalIgnoreCase))

            {

                winner = BattlefieldField.BattlefieldWinner.Draw;

                return true;

            }



            winner = BattlefieldField.BattlefieldWinner.None;

            return false;

        }



        private void HandleChatMessageSubmitted(string message, int tickCount)

        {

            _playerManager?.TryExecutePetCommand(message, tickCount);

        }



        private static bool TryParsePetSpeechEvent(

            string value,

            out PetAutoSpeechEvent eventType,

            out string displayName)

        {

            eventType = PetAutoSpeechEvent.Rest;

            displayName = null;



            if (string.IsNullOrWhiteSpace(value))

            {

                return false;

            }



            switch (value.Trim().ToLowerInvariant())

            {

                case "rest":

                case "idle":

                    eventType = PetAutoSpeechEvent.Rest;

                    displayName = "rest";

                    return true;

                case "levelup":

                case "level":

                    eventType = PetAutoSpeechEvent.LevelUp;

                    displayName = "levelup";

                    return true;

                case "prelevelup":

                case "prelevel":

                    eventType = PetAutoSpeechEvent.PreLevelUp;

                    displayName = "prelevelup";

                    return true;

                case "hpalert":

                case "lowhp":

                    eventType = PetAutoSpeechEvent.HpAlert;

                    displayName = "hpalert";

                    return true;

                case "nohppotion":

                case "nohp":

                    eventType = PetAutoSpeechEvent.NoHpPotion;

                    displayName = "nohppotion";

                    return true;

                case "nomppotion":

                case "nomp":

                    eventType = PetAutoSpeechEvent.NoMpPotion;

                    displayName = "nomppotion";

                    return true;

                default:

                    return false;

            }

        }



        private string DescribePetCommandLevels()

        {

            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;

            if (activePets == null || activePets.Count == 0)

            {

                return "No active pets are available";

            }



            return string.Join(", ",

                activePets.Select((pet, index) =>

                {

                    string petName = pet != null

                        ? (!string.IsNullOrWhiteSpace(pet.Name) ? pet.Name : pet.ItemId.ToString())

                        : "Unknown";

                    return $"Pet {index + 1} ({petName}): Lv {pet?.CommandLevel ?? 0}";

                }));

        }



        private bool TryResolvePetCommandSlot(

            string[] args,

            int argumentIndex,

            out int petSlotIndex,

            out string error)

        {

            petSlotIndex = 0;

            error = null;



            IReadOnlyList<PetRuntime> activePets = _playerManager?.Pets?.ActivePets;

            if (activePets == null || activePets.Count == 0)

            {

                error = "No active pets are available";

                return false;

            }



            if (args == null || argumentIndex >= args.Length)

            {

                petSlotIndex = 0;

                return true;

            }



            if (!TryParsePetSlot(args[argumentIndex], out petSlotIndex, out error))

            {

                return false;

            }



            if (petSlotIndex >= activePets.Count)

            {

                error = $"No active pet is present in slot {petSlotIndex + 1}";

                return false;

            }



            return true;

        }



        private static bool TryParsePetSlot(string value, out int petSlotIndex, out string error)

        {

            petSlotIndex = 0;

            error = null;



            if (!int.TryParse(value, out int oneBasedSlot) || oneBasedSlot < 1 || oneBasedSlot > 3)

            {

                error = "Pet slot must be between 1 and 3";

                return false;

            }



            petSlotIndex = oneBasedSlot - 1;

            return true;

        }



        private bool IsLoginRuntimeSceneActive => _gameState.IsLoginMap;



        private void ResetLoginRuntimeForCurrentMap(int currentTickCount)

        {

            ClearLoginWorldSelectorMetadata();

            _recommendWorldDismissed = false;

            _recommendWorldEntries.Clear();

            _recommendWorldIndex = 0;

            _loginTitleStatusMessage = "Enter credentials or let the login packet inbox feed the bootstrap runtime.";

            _nextLoginWorldPopulationUpdateAt = currentTickCount + LoginWorldPopulationUpdateIntervalMs;

            EnsureLoginPacketInboxState(_gameState.IsLoginMap);



            if (_gameState.IsLoginMap)

            {

                _loginRuntime.Initialize(currentTickCount);

                _lastLoginStep = _loginRuntime.CurrentStep;

                HideLoginUtilityDialog();

                return;

            }



            _loginRuntime.Reset();

            HideLoginUtilityDialog();

            _gameState.PlayerControlEnabled = true;

        }



        private void UpdateLoginRuntimeFrame(GameTime gameTime, KeyboardState newKeyboardState, MouseState newMouseState, bool isWindowActive)

        {
            RefreshLoginOfficialSessionBridgeDiscovery(currTickCount);

            DrainLoginPacketInbox();

            _loginRuntime.Update(currTickCount);

            UpdateWorldChannelSelectorRequestState();

            UpdateLoginWorldPopulationDrift();

            SyncLoginTitleWindow();

            SyncLoginWorldSelectionWindows();

            SyncLoginCharacterSelectWindow();

            SyncLoginCreateCharacterWindow();

            SyncLoginEntryDialogs();



            if (HandlePendingMapChange())

            {

                _frameNumber++;

                UpdateObjectVisibility();

                FinalizeFrameInputState(newKeyboardState, newMouseState, gameTime);

                return;

            }



            bool chatConsumedInput = isWindowActive &&

                                     _chat.HandleInput(newKeyboardState, _oldKeyboardState, currTickCount);



            if (!chatConsumedInput && !_chat.IsActive && isWindowActive)

            {

                if (newKeyboardState.IsKeyUp(Keys.H) && _oldKeyboardState.IsKeyDown(Keys.H))

                {

                    _gameState.HideUIMode = !_gameState.HideUIMode;

                }

            }



            _frameNumber++;

            UpdateObjectVisibility();

            FinalizeFrameInputState(newKeyboardState, newMouseState, gameTime);

        }



        private void EnsureLoginPacketInboxState(bool shouldRun)

        {

            if (!shouldRun || !_loginPacketInboxEnabled)

            {

                if (_loginPacketInbox.IsRunning)

                {

                    _loginPacketInbox.Stop();

                }



                return;

            }



            if (_loginPacketInbox.IsRunning && _loginPacketInbox.Port == _loginPacketInboxConfiguredPort)

            {

                return;

            }



            if (_loginPacketInbox.IsRunning)

            {

                _loginPacketInbox.Stop();

            }



            try

            {

                _loginPacketInbox.Start(_loginPacketInboxConfiguredPort);

            }

            catch (Exception ex)

            {

                Debug.WriteLine($"Unable to start login packet inbox: {ex.Message}");

                _loginPacketInbox.Stop();

            }

            EnsureLoginOfficialSessionBridgeState(shouldRun);

        }



        private string DescribeLoginPacketInboxStatus()

        {

            string enabledText = _loginPacketInboxEnabled ? "enabled" : "disabled";

            string listeningText = _loginPacketInbox.IsRunning

                ? $"listening on 127.0.0.1:{_loginPacketInbox.Port}"

                : $"configured for 127.0.0.1:{_loginPacketInboxConfiguredPort}";

            return $"Login packet inbox {enabledText}, {listeningText}, received {_loginPacketInbox.ReceivedCount} packet(s). Formats: <packet> <args>, <packet>:<args>, <packet>=<args>, /loginpacket <packet> <args>, or a scripted stream via /loginpacket stream <line1 | line2 | ...>.";

        }

        private void EnsureLoginOfficialSessionBridgeState(bool shouldRun)

        {

            if (!shouldRun || !_loginOfficialSessionBridgeEnabled)

            {

                if (_loginOfficialSessionBridge.IsRunning)

                {

                    _loginOfficialSessionBridge.Stop();

                }



                return;

            }



            if (_loginOfficialSessionBridgeConfiguredListenPort <= 0 ||
                _loginOfficialSessionBridgeConfiguredListenPort > ushort.MaxValue)

            {

                if (_loginOfficialSessionBridge.IsRunning)

                {

                    _loginOfficialSessionBridge.Stop();

                }



                _loginOfficialSessionBridgeEnabled = false;
                _loginOfficialSessionBridgeConfiguredListenPort = LoginOfficialSessionBridgeManager.DefaultListenPort;
                return;

            }



            if (_loginOfficialSessionBridgeUseDiscovery)

            {

                if (_loginOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                    _loginOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue)

                {

                    if (_loginOfficialSessionBridge.IsRunning)

                    {

                        _loginOfficialSessionBridge.Stop();

                    }



                    return;

                }



                _loginOfficialSessionBridge.TryRefreshFromDiscovery(
                    _loginOfficialSessionBridgeConfiguredListenPort,
                    _loginOfficialSessionBridgeConfiguredRemotePort,
                    _loginOfficialSessionBridgeConfiguredProcessSelector,
                    _loginOfficialSessionBridgeConfiguredLocalPort,
                    out _);
                return;

            }



            if (_loginOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                _loginOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue ||
                string.IsNullOrWhiteSpace(_loginOfficialSessionBridgeConfiguredRemoteHost))

            {

                if (_loginOfficialSessionBridge.IsRunning)

                {

                    _loginOfficialSessionBridge.Stop();

                }



                return;

            }



            if (_loginOfficialSessionBridge.IsRunning &&
                _loginOfficialSessionBridge.ListenPort == _loginOfficialSessionBridgeConfiguredListenPort &&
                string.Equals(_loginOfficialSessionBridge.RemoteHost, _loginOfficialSessionBridgeConfiguredRemoteHost, StringComparison.OrdinalIgnoreCase) &&
                _loginOfficialSessionBridge.RemotePort == _loginOfficialSessionBridgeConfiguredRemotePort)

            {

                return;

            }



            if (_loginOfficialSessionBridge.IsRunning)

            {

                _loginOfficialSessionBridge.Stop();

            }



            _loginOfficialSessionBridge.Start(
                _loginOfficialSessionBridgeConfiguredListenPort,
                _loginOfficialSessionBridgeConfiguredRemoteHost,
                _loginOfficialSessionBridgeConfiguredRemotePort);

        }

        private void RefreshLoginOfficialSessionBridgeDiscovery(int currentTickCount)

        {

            if (!IsLoginRuntimeSceneActive ||
                !_loginOfficialSessionBridgeEnabled ||
                !_loginOfficialSessionBridgeUseDiscovery ||
                _loginOfficialSessionBridgeConfiguredRemotePort <= 0 ||
                _loginOfficialSessionBridgeConfiguredRemotePort > ushort.MaxValue ||
                _loginOfficialSessionBridge.HasAttachedClient ||
                currentTickCount < _nextLoginOfficialSessionBridgeDiscoveryRefreshAt)

            {

                return;

            }



            _nextLoginOfficialSessionBridgeDiscoveryRefreshAt = currentTickCount + LoginOfficialSessionBridgeDiscoveryRefreshIntervalMs;
            _loginOfficialSessionBridge.TryRefreshFromDiscovery(
                _loginOfficialSessionBridgeConfiguredListenPort,
                _loginOfficialSessionBridgeConfiguredRemotePort,
                _loginOfficialSessionBridgeConfiguredProcessSelector,
                _loginOfficialSessionBridgeConfiguredLocalPort,
                out _);

        }

        private string DescribeLoginOfficialSessionBridgeStatus()

        {

            string enabledText = _loginOfficialSessionBridgeEnabled ? "enabled" : "disabled";
            string modeText = _loginOfficialSessionBridgeUseDiscovery ? "auto-discovery" : "direct proxy";
            string configuredTarget = _loginOfficialSessionBridgeUseDiscovery
                ? _loginOfficialSessionBridgeConfiguredLocalPort.HasValue
                    ? $"discover remote port {_loginOfficialSessionBridgeConfiguredRemotePort} with local port {_loginOfficialSessionBridgeConfiguredLocalPort.Value}"
                    : $"discover remote port {_loginOfficialSessionBridgeConfiguredRemotePort}"
                : $"{_loginOfficialSessionBridgeConfiguredRemoteHost}:{_loginOfficialSessionBridgeConfiguredRemotePort}";
            string processText = string.IsNullOrWhiteSpace(_loginOfficialSessionBridgeConfiguredProcessSelector)
                ? string.Empty
                : $" for {_loginOfficialSessionBridgeConfiguredProcessSelector}";
            string listeningText = _loginOfficialSessionBridge.IsRunning
                ? $"listening on 127.0.0.1:{_loginOfficialSessionBridge.ListenPort}"
                : $"configured for 127.0.0.1:{_loginOfficialSessionBridgeConfiguredListenPort}";

            return $"Login packet session bridge {enabledText}, {modeText}, {listeningText}, target {configuredTarget}{processText}, received {_loginOfficialSessionBridge.ReceivedCount} packet(s). {_loginOfficialSessionBridge.LastStatus}";

        }

        private string DescribeLoginPacketTransportStatus()

        {

            return $"{DescribeLoginPacketInboxStatus()}{Environment.NewLine}{DescribeLoginOfficialSessionBridgeStatus()}";

        }





        private void DrainLoginPacketInbox()

        {

            if (!IsLoginRuntimeSceneActive)

            {

                return;

            }



            while (_loginPacketInbox.TryDequeue(out LoginPacketInboxMessage message))

            {

                if (message == null)

                {

                    continue;

                }



                string[] args = message.Arguments?.Length > 0

                    ? message.Arguments

                    : ParseLoginPacketInboxArguments(message.RawText);

                if (!TryConfigureLoginPacketPayload(message.PacketType, args, out _, out _))

                {

                    continue;

                }



                DispatchLoginRuntimePacket(message.PacketType, out _);

            }

            while (_loginOfficialSessionBridge.TryDequeue(out LoginPacketInboxMessage bridgedMessage))

            {

                if (bridgedMessage == null)

                {

                    continue;

                }



                string[] args = bridgedMessage.Arguments?.Length > 0

                    ? bridgedMessage.Arguments

                    : ParseLoginPacketInboxArguments(bridgedMessage.RawText);

                if (!TryConfigureLoginPacketPayload(bridgedMessage.PacketType, args, out _, out _))

                {

                    continue;

                }



                DispatchLoginRuntimePacket(bridgedMessage.PacketType, out _);

            }

        }



        private static string[] ParseLoginPacketInboxArguments(string rawText)

        {

            if (string.IsNullOrWhiteSpace(rawText))

            {

                return Array.Empty<string>();

            }



            string trimmed = rawText.Trim();

            int separatorIndex = trimmed.IndexOfAny(new[] { ' ', '\t', ':', '=' });

            if (separatorIndex < 0)

            {

                return Array.Empty<string>();

            }



            int argumentStart = separatorIndex;

            if (trimmed[argumentStart] == ':' || trimmed[argumentStart] == '=')

            {

                argumentStart++;

            }



            while (argumentStart < trimmed.Length && char.IsWhiteSpace(trimmed[argumentStart]))

            {

                argumentStart++;

            }



            if (argumentStart >= trimmed.Length)

            {

                return Array.Empty<string>();

            }



            return trimmed[argumentStart..]

                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        }



        private void FinalizeFrameInputState(KeyboardState newKeyboardState, MouseState newMouseState, GameTime gameTime)

        {

            _oldKeyboardState = newKeyboardState;

            _oldMouseState = newMouseState;



            base.Update(gameTime);

        }



        private void DrawLoginRuntimeOverlay()

        {

            if (_fontDebugValues == null)

            {

                return;

            }



            const int overlayX = 20;

            const int overlayY = 20;

            const int overlayWidth = 560;

            const int overlayPadding = 12;

            string overlayText = "Login Bootstrap Runtime\n"

                + _loginRuntime.DescribeStatus()

                + "\nCommands: /login, /loginstep <step>, /loginpacket <packet>";



            Vector2 textSize = _fontDebugValues.MeasureString(overlayText);

            if (_debugBoundaryTexture != null)

            {

                var background = new Rectangle(

                    overlayX - overlayPadding,

                    overlayY - overlayPadding,

                    Math.Max(overlayWidth, (int)textSize.X + overlayPadding * 2),

                    (int)textSize.Y + overlayPadding * 2);

                _spriteBatch.Draw(_debugBoundaryTexture, background, Color.Black * 0.75f);

            }



            _spriteBatch.DrawString(_fontDebugValues, overlayText, new Vector2(overlayX + 1, overlayY + 1), Color.Black);

            _spriteBatch.DrawString(_fontDebugValues, overlayText, new Vector2(overlayX, overlayY), Color.White);

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
