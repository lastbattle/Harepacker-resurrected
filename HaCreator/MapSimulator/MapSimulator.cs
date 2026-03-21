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
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
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
using HaCreator.MapSimulator.Combat;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// 
    /// http://rbwhitaker.wikidot.com/xna-tutorials
    /// </summary>
    public class MapSimulator : Microsoft.Xna.Framework.Game
    {
        private const int DefaultLowHpWarningThresholdPercent = 20;
        private const int DefaultLowMpWarningThresholdPercent = 20;
        private const int ReactorCollisionCheckIntervalMs = 1000;
        private const int PetAutoSpeechPreLevelReminderCooldownMs = 420000;
        private const int PetAutoSpeechLowHpAlertCooldownMs = 60000;
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
        private int _statusBarHpWarningThresholdPercent = DefaultLowHpWarningThresholdPercent;
        private int _statusBarMpWarningThresholdPercent = DefaultLowMpWarningThresholdPercent;

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
        private readonly Dictionary<BaseDXDrawableItem, QuestGatedMapObjectState> _questGatedMapObjects = new();
        private readonly Dictionary<string, bool> _authoredDynamicObjectTagStates = new(StringComparer.OrdinalIgnoreCase);
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
        private MonoGameBgmPlayer _audio;
        private SoundManager _soundManager; // Manages sound effects with concurrent playback support
        private string _currentBgmName = null; // Track current BGM to avoid reloading same BGM on map change
        private bool _isBgmPausedForFocusLoss = false;

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
        private NpcInteractionOverlay _npcInteractionOverlay;
        private readonly QuestRuntimeManager _questRuntime = new QuestRuntimeManager();
        private readonly MemoMailboxManager _memoMailbox = new MemoMailboxManager();
        private readonly SocialListRuntime _socialListRuntime = new SocialListRuntime();
        private readonly MessengerRuntime _messengerRuntime = new MessengerRuntime();
        private readonly GuildBbsRuntime _guildBbsRuntime = new GuildBbsRuntime();
        private readonly MapleTvRuntime _mapleTvRuntime = new MapleTvRuntime();
        private bool _questUiBindingsConfigured;
        private int _activeQuestDetailQuestId;
        private int _activeMemoAttachmentId = -1;
        private NpcItem _activeNpcInteractionNpc;
        private int _activeNpcInteractionNpcId;
        private Texture2D _npcQuestAvailableIcon;
        private Texture2D _npcQuestInProgressIcon;
        private Texture2D _npcQuestCompletableIcon;
        private readonly NpcFeedbackBalloonQueue _npcQuestFeedback = new();
        private readonly Random _npcIdleSpeechRandom = new();
        private int _nextNpcIdleSpeechTick;
        private readonly Random _petIdleSpeechRandom = new();
        private int _lastObservedPetSpeechLevel = -1;
        private int _nextPetPreLevelSpeechTick;
        private int _lastPetHpAlertTick = int.MinValue;
        private bool _petHpAlertArmed = true;
        private int _lastReactorCollisionCheckTick = -ReactorCollisionCheckIntervalMs;
        private readonly Dictionary<(int skillId, int level), MobSummonSkillInfo> _mobSummonSkillCache = new();
        private readonly Dictionary<(int skillId, int level), MobSkillRuntimeData> _mobSkillRuntimeCache = new();
        private readonly Dictionary<long, int> _appliedMobSkillEffects = new();
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
        private readonly EscortFollowController _escortFollow = new EscortFollowController();
        private readonly LimitedViewField _limitedViewField = new LimitedViewField();
        private TemporaryPortalField _temporaryPortalField;
        private FieldRuleRuntime _fieldRuleRuntime;
        private readonly SpecialFieldRuntimeCoordinator _specialFieldRuntime = new SpecialFieldRuntimeCoordinator();
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
        private int? _battlefieldAppliedTeamId;

        // Camera controller for smooth scrolling and zoom
        private readonly CameraController _cameraController = new CameraController();

        // Centralized game state management
        private readonly GameStateManager _gameState = new GameStateManager();
        private readonly LoginRuntimeManager _loginRuntime = new LoginRuntimeManager();

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

        private sealed class MobSummonSkillInfo
        {
            public List<string> MobIds { get; } = new List<string>();
            public int Limit { get; set; }
            public Point? Lt { get; set; }
            public Point? Rb { get; set; }
        }

        private sealed class MobSkillRuntimeData
        {
            public int X { get; init; }
            public int Y { get; init; }
            public int Hp { get; init; }
            public int DurationMs { get; init; }
            public Point? Lt { get; init; }
            public Point? Rb { get; init; }
        }

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

        private sealed class SameMapTeleportTarget
        {
            public SameMapTeleportTarget(float x, float y)
            {
                X = x;
                Y = y;
            }

            public float X { get; }
            public float Y { get; }
        }

        private sealed class PendingMapSpawnTarget
        {
            public PendingMapSpawnTarget(float x, float y)
            {
                X = x;
                Y = y;
            }

            public float X { get; }
            public float Y { get; }
        }

        private enum SelectorRequestKind
        {
            None = 0,
            LoginWorldCheck = 1,
            ChannelChange = 2,
        }

        private enum SelectorRequestResultCode
        {
            None = 0,
            Success = 1,
            LoginStepBlocked = 2,
            WorldUnavailable = 3,
            AdultWorldRestricted = 4,
            ChannelUnavailable = 5,
            ChannelFull = 6,
            AdultChannelRestricted = 7,
        }

        private sealed class LoginWorldSelectorMetadata
        {
            public LoginWorldSelectorMetadata(int worldId, IReadOnlyList<ChannelSelectionState> channels, bool requiresAdultAccount)
            {
                WorldId = Math.Max(0, worldId);
                Channels = channels ?? Array.Empty<ChannelSelectionState>();
                RequiresAdultAccount = requiresAdultAccount;
            }

            public int WorldId { get; }
            public IReadOnlyList<ChannelSelectionState> Channels { get; }
            public bool RequiresAdultAccount { get; }
        }

        private enum LoginUtilityDialogAction
        {
            None = 0,
            DismissOnly = 1,
            ConfirmDeleteCharacter = 2,
        }

        private enum WorldMapRequestMode
        {
            DirectTransfer = 0,
            MapTransferTargetSelection = 1,
        }

        // Same-map portal teleport delay (no fade, just delay before teleport)
        // Default delay is 1000ms (1 second) if portal doesn't specify its own delay
        private const int SAME_MAP_PORTAL_DEFAULT_DELAY_MS = 1000;
        private const int SAME_MAP_TELEPORT_Y_OFFSET = 10;
        private const int DIRECTION_MODE_RELEASE_DELAY_MS = 300;
        private const int PASSIVE_TRANSFER_REQUEST_DURATION_MS = 1200;
        private const int FIELD_RULE_DAMAGE_MIST_DURATION_MS = 650;
        private const int FIELD_RULE_MESSAGE_COOLDOWN_MS = 1200;
        private const int DefaultSimulatorWorldId = 0;
        private const int DefaultSimulatorChannelIndex = 0;
        private const int DefaultSimulatorChannelCount = 20;
        private const int DefaultSimulatorChannelCapacity = 1000;
        private const int DefaultLoginCharacterFieldMapId = 100000000;
        private const int LoginWorldSelectionRequestDelayMs = 450;
        private const int ChannelChangeRequestDelayMs = 700;
        private const int LoginWorldPopulationUpdateIntervalMs = 2200;
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
        private readonly List<RecommendWorldEntry> _recommendWorldEntries = new();
        private int _recommendWorldIndex;
        private bool _recommendWorldDismissed;
        private LoginStep _lastLoginStep = LoginStep.Title;
        private readonly MapTransferDestinationStore _mapTransferDestinations;
        private readonly SkillMacroStore _skillMacroStore;
        private readonly Dictionary<int, string> _mapTransferTitleCache = new();
        private WorldMapRequestMode _worldMapRequestMode = WorldMapRequestMode.DirectTransfer;
        private MapTransferUI.DestinationEntry _mapTransferManualDestination;
        private MapTransferUI.DestinationEntry _mapTransferEditDestination;
        private readonly LoginCharacterRosterManager _loginCharacterRoster = new();
        private string _loginCharacterStatusMessage = "Dispatch SelectWorldResult to populate the character roster.";
        private LoginUtilityDialogAction _loginUtilityDialogAction;
        private string _loginUtilityDialogTitle = "Login Utility";
        private string _loginUtilityDialogBody = string.Empty;
        private string _loginUtilityDialogPrimaryLabel = "OK";
        private string _loginUtilityDialogSecondaryLabel = "Cancel";
        private bool _loginUtilityDialogShowSecondaryButton;
        private int _loginUtilityDialogTargetIndex = -1;

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
            mapTransferWindow.SetCurrentMapName(GetCurrentMapTransferDisplayName());
            mapTransferWindow.SetStatusMessage(GetMapTransferStatusMessage());
            mapTransferWindow.SetDestinations(BuildMapTransferDestinations());
        }

        private void HandleMapTransferWorldMapRequested(MapTransferUI.DestinationEntry destination)
        {
            _worldMapRequestMode = WorldMapRequestMode.MapTransferTargetSelection;
            _mapTransferEditDestination = destination?.CanDelete == true ? destination : null;

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

        private void HandleWorldMapRequested(WorldMapUI.MapEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (_worldMapRequestMode == WorldMapRequestMode.MapTransferTargetSelection)
            {
                _mapTransferManualDestination = new MapTransferUI.DestinationEntry
                {
                    MapId = entry.MapId,
                    DisplayName = $"[Target] {entry.DisplayName}",
                    DetailText = _mapTransferEditDestination?.CanDelete == true
                        ? $"{entry.MapId} selected for {_mapTransferEditDestination.DisplayName}"
                        : $"{entry.MapId} selected from world map",
                    CanDelete = false
                };

                _worldMapRequestMode = WorldMapRequestMode.DirectTransfer;
                _mapTransferEditDestination = null;
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
            int savedCount = 0;

            foreach (MapTransferDestinationRecord saved in GetCurrentMapTransferDestinations())
            {
                if (savedCount >= savedCapacity)
                {
                    break;
                }

                string displayName = ResolveMapTransferDisplayName(saved.MapId, saved.DisplayName);
                if (!seenKeys.Add($"saved:{saved.MapId}"))
                {
                    continue;
                }

                destinations.Add(new MapTransferUI.DestinationEntry
                {
                    MapId = saved.MapId,
                    DisplayName = $"[Saved] {displayName}",
                    DetailText = $"{saved.MapId}",
                    CanDelete = true
                });
                savedCount++;
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

            CharacterBuild activeBuild = GetActiveMapTransferCharacterBuild();
            int savedCapacity = GetMapTransferSavedSlotCapacity();
            bool replacingSelectedSavedDestination = selectedEntry?.CanDelete == true;
            int existingMapId = replacingSelectedSavedDestination ? selectedEntry.MapId : 0;

            if (!replacingSelectedSavedDestination && _mapTransferDestinations.Contains(activeBuild, targetMapId))
            {
                if (_mapTransferManualDestination?.MapId == targetMapId)
                {
                    _mapTransferManualDestination = null;
                }

                RefreshMapTransferWindow();
                return;
            }

            if (!replacingSelectedSavedDestination && GetCurrentMapTransferDestinations().Count >= savedCapacity)
            {
                RefreshMapTransferWindow();
                return;
            }

            bool saved = replacingSelectedSavedDestination
                ? _mapTransferDestinations.Replace(
                    activeBuild,
                    existingMapId,
                    new MapTransferDestinationRecord
                    {
                        MapId = targetMapId,
                        DisplayName = targetDisplayName
                    },
                    savedCapacity)
                : _mapTransferDestinations.TryAdd(
                    activeBuild,
                    new MapTransferDestinationRecord
                    {
                        MapId = targetMapId,
                        DisplayName = targetDisplayName
                    },
                    savedCapacity);

            if (saved && _mapTransferManualDestination?.MapId == targetMapId)
            {
                _mapTransferManualDestination = null;
            }

            RefreshMapTransferWindow();
        }

        private void DeleteMapTransferDestination(MapTransferUI.DestinationEntry destination)
        {
            if (destination == null || !destination.CanDelete)
            {
                return;
            }

            _mapTransferDestinations.Remove(GetActiveMapTransferCharacterBuild(), destination.MapId);
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
                string editPrefix = _mapTransferEditDestination?.CanDelete == true
                    ? $"Update {_mapTransferEditDestination.DisplayName} or "
                    : string.Empty;
                return $"{editPrefix}move to {TrimMapTransferCategoryPrefix(_mapTransferManualDestination.DisplayName)} via the world-map target.";
            }

            int savedCapacity = GetMapTransferSavedSlotCapacity();
            IReadOnlyList<MapTransferDestinationRecord> currentDestinations = GetCurrentMapTransferDestinations();
            if (currentDestinations.Count >= savedCapacity)
            {
                return $"Saved slots full ({savedCapacity}/{savedCapacity}). Delete one before registering another map.";
            }

            string ownerName = GetActiveMapTransferCharacterBuild()?.Name;
            string ownerSuffix = string.IsNullOrWhiteSpace(ownerName) ? string.Empty : $" for {ownerName}";
            return $"Register the current map or choose a listed route ({Math.Min(currentDestinations.Count, savedCapacity)}/{savedCapacity} saved{ownerSuffix}).";
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
            return _mapTransferDestinations.GetDestinations(GetActiveMapTransferCharacterBuild());
        }

        private CharacterBuild GetActiveMapTransferCharacterBuild()
        {
            return _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;
        }

        private CharacterBuild GetActiveSkillMacroCharacterBuild()
        {
            return _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build;
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

            if (_loadMapCallback != null)
            {
                try
                {
                    Tuple<Board, string> result = _loadMapCallback(mapId);
                    string title = result?.Item2;
                    Board board = result?.Item1;
                    string resolved = title;
                    if (board?.MapInfo != null)
                    {
                        string street = board.MapInfo.strStreetName;
                        string map = board.MapInfo.strMapName;
                        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(map) && !string.Equals(street, map, StringComparison.OrdinalIgnoreCase))
                        {
                            resolved = $"{street} : {map}";
                        }
                        else if (!string.IsNullOrWhiteSpace(map))
                        {
                            resolved = map;
                        }
                        else if (!string.IsNullOrWhiteSpace(street))
                        {
                            resolved = street;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        _mapTransferTitleCache[mapId] = resolved;
                        return resolved;
                    }
                }
                catch
                {
                }
            }

            return mapId.ToString();
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
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MemoMailbox);
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
            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MemoGet);
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
                onlineOnly => _socialListRuntime.SetFriendOnlineOnly(onlineOnly),
                actionKey =>
                {
                    if (string.Equals(actionKey, "Guild.Board", StringComparison.Ordinal))
                    {
                        uiWindowManager?.ShowWindow(MapSimulatorWindowNames.GuildBbs);
                        return "Opened Guild BBS from the guild member list.";
                    }

                    return _socialListRuntime.ExecuteAction(actionKey);
                },
                ShowUtilityFeedbackMessage);
            socialListWindow.SetFont(_fontChat);
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

            messengerWindow.SetSnapshotProvider(() => _messengerRuntime.BuildSnapshot());
            messengerWindow.SetActionHandlers(
                slotIndex => _messengerRuntime.SelectSlot(slotIndex),
                () => _messengerRuntime.InviteNextContact(),
                () => _messengerRuntime.WhisperSelected(),
                () => _messengerRuntime.LeaveMessenger(),
                message => _messengerRuntime.SendMessage(message),
                message => _messengerRuntime.WhisperSelected(message),
                ShowUtilityFeedbackMessage);
            messengerWindow.SetFont(_fontChat);
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
            _guildBbsRuntime.UpdateLocalContext(playerName, guildName, GetCurrentMapTransferDisplayName());

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
                ShowUtilityFeedbackMessage);
            guildBbsWindow.SetFont(_fontChat);
        }

        private void WireMapleTvWindowData()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MapleTv) is not MapleTvWindow mapleTvWindow)
            {
                return;
            }

            _mapleTvRuntime.UpdateLocalContext(_playerManager?.Player?.Build?.Name ?? "Player");
            mapleTvWindow.SetSnapshotProvider(() => _mapleTvRuntime.BuildSnapshot(currTickCount));
            mapleTvWindow.SetActionHandlers(
                PublishMapleTvDraft,
                ClearMapleTvMessage,
                ToggleMapleTvReceiverMode,
                ShowUtilityFeedbackMessage);
            mapleTvWindow.SetFont(_fontChat);
        }

        private void WireProgressionUtilityWindowLaunchers()
        {
            if (miniMapUi != null)
            {
                miniMapUi.ResolveNpcMarkerType = ResolveMinimapNpcMarkerType;
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
                    uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Mts);
                };
                statusBarUi.MenuRequested = () => ToggleStatusBarPopupWindow(MapSimulatorWindowNames.Menu, MapSimulatorWindowNames.System);
                statusBarUi.SystemRequested = () => ToggleStatusBarPopupWindow(MapSimulatorWindowNames.System, MapSimulatorWindowNames.Menu);
                statusBarUi.ChannelRequested = HandleUtilityChannelPopupRequested;
            }

            if (statusBarChatUI != null)
            {
                statusBarChatUI.CharacterInfoRequested = () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.CharacterInfo);
                statusBarChatUI.MemoMailboxRequested = () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MemoMailbox);
            }

            WireItemUpgradeWindowLaunchers();
            WireWorldChannelSelectorWindows();
            WireStatusBarPopupUtilityWindows();
        }

        private void WireMiniRoomWindowData()
        {
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.MiniRoom) is not SocialRoomWindow miniRoomWindow)
            {
                return;
            }

            _specialFieldRuntime.Minigames.MemoryGame.AttachMiniRoomRuntime(miniRoomWindow.Runtime);
            miniRoomWindow.SetFont(_fontChat);
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
                inventoryWindow.CashShopRequested = ShowCashShopWindow;
            }

            switch (uiWindowManager.EquipWindow)
            {
                case EquipUI equipWindow:
                    equipWindow.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipment;
                    break;
                case EquipUIBigBang equipWindowBigBang:
                    equipWindowBigBang.ItemUpgradeRequested = OpenItemUpgradeWindowForEquipment;
                    break;
            }
        }

        private void OpenItemUpgradeWindowForConsumable(int itemId)
        {
            if (!TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow))
            {
                return;
            }

            itemUpgradeWindow.PrepareConsumableSelection(itemId);
        }

        private void ShowCashShopWindow()
        {
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.Mts);
            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.CashShop);
        }

        private void OpenItemUpgradeWindowForEquipment(Character.EquipSlot slot)
        {
            if (!TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow))
            {
                return;
            }

            itemUpgradeWindow.PrepareEquipmentSelection(slot);
        }

        private bool TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow)
        {
            itemUpgradeWindow = uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) as ItemUpgradeUI;
            if (itemUpgradeWindow == null)
            {
                return false;
            }

            itemUpgradeWindow.Show();
            uiWindowManager.BringToFront(itemUpgradeWindow);
            return true;
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
                menuWindow.BindEntryAction("BtCommunity", () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.SocialList));
                menuWindow.BindEntryAction("BtMSN", () => uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Messenger));
                menuWindow.BindEntryAction("BtRank", () => ShowUtilityFeedbackMessage("Ranking tools are not implemented in MapSimulator yet."));
                menuWindow.BindEntryAction("BtEvent", () => ShowUtilityFeedbackMessage("Event tools are not implemented in MapSimulator yet."));
                menuWindow.BindEntryCursorHint("BtRank", () => StatusBarPopupCursorHint.Forbidden);
                menuWindow.BindEntryCursorHint("BtEvent", () => StatusBarPopupCursorHint.Forbidden);
            }

            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.System) is StatusBarPopupMenuWindow systemWindow)
            {
                systemWindow.BindEntryAction("BtChannel", HandleUtilityChannelPopupRequested);
                systemWindow.BindEntryAction("BtKeySetting", () => ShowUtilityFeedbackMessage("Key settings are not implemented in MapSimulator yet."));
                systemWindow.BindEntryAction("BtGameOption", () => ShowUtilityFeedbackMessage("Game options are not implemented in MapSimulator yet."));
                systemWindow.BindEntryAction("BtSystemOption", () => ShowUtilityFeedbackMessage("System options are not implemented in MapSimulator yet."));
                systemWindow.BindEntryAction("BtGameQuit", () => ShowUtilityFeedbackMessage("Close the MapSimulator window to end the current session."));
                systemWindow.BindEntryAction("BtJoyPad", () => ShowUtilityFeedbackMessage("Joypad configuration is not implemented in MapSimulator yet."));
                systemWindow.BindEntryAction("BtOption", () => ShowUtilityFeedbackMessage("Additional options are not implemented in MapSimulator yet."));
                systemWindow.BindEntryCursorHint("BtChannel", GetUtilityChannelPopupCursorHint);
                systemWindow.BindEntryCursorHint("BtKeySetting", () => StatusBarPopupCursorHint.Forbidden);
                systemWindow.BindEntryCursorHint("BtGameOption", () => StatusBarPopupCursorHint.Forbidden);
                systemWindow.BindEntryCursorHint("BtSystemOption", () => StatusBarPopupCursorHint.Forbidden);
                systemWindow.BindEntryCursorHint("BtJoyPad", () => StatusBarPopupCursorHint.Forbidden);
                systemWindow.BindEntryCursorHint("BtOption", () => StatusBarPopupCursorHint.Forbidden);
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
                uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MapleTv);
            }

            return message;
        }

        private string ClearMapleTvMessage()
        {
            return _mapleTvRuntime.OnClearMessage();
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

        private void DispatchLoginRuntimePacket(LoginPacketType packetType, out string message)
        {
            _loginRuntime.TryDispatchPacket(packetType, currTickCount, out message);
            ApplyLoginWorldSelectorPacket(packetType);
            RefreshWorldChannelSelectorWindows();
            SyncRecommendWorldWindow();
            SyncLoginCharacterSelectWindow();
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
                IsWorldChannelSelectorRequestAllowed(),
                GetRecommendWorldStatusMessage());

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
            IEnumerable<WorldSelectionState> orderedStates = worldStates.Values
                .OrderByDescending(state => state.IsRecommended)
                .ThenByDescending(state => state.IsLatestConnected)
                .ThenBy(state => state.OccupancyPercent)
                .ThenBy(state => state.WorldId)
                .Take(5);

            foreach (WorldSelectionState state in orderedStates)
            {
                int visibleChannels = GetChannelSelectionStates(state.WorldId).Count(channel => channel.Capacity > 0);
                string helperLead = state.IsRecommended
                    ? "Recommended for lighter traffic."
                    : state.IsLatestConnected
                        ? "Latest connected world."
                        : "Stable world-selection option.";
                string loadLine = $"Load {state.OccupancyPercent}% ({state.Availability}).";
                string channelLine = $"{visibleChannels} visible channels, {state.ActiveChannels} enterable.";
                string adultLine = state.HasAdultChannels
                    ? (_loginAccountIsAdult ? "Adult-only channels available." : "Adult-only channels are gated.")
                    : "Standard channel access only.";

                _recommendWorldEntries.Add(new RecommendWorldEntry(
                    state.WorldId,
                    new[] { helperLead, loadLine, channelLine, adultLine }));
            }

            int matchingIndex = _recommendWorldEntries.FindIndex(entry => entry.WorldId == _selectorBrowseWorldId);
            if (matchingIndex >= 0)
            {
                _recommendWorldIndex = matchingIndex;
            }
        }

        private string GetRecommendWorldStatusMessage()
        {
            if (_selectorRequestKind == SelectorRequestKind.LoginWorldCheck ||
                _selectorRequestKind == SelectorRequestKind.ChannelChange)
            {
                return _selectorRequestStatusMessage;
            }

            if (!string.IsNullOrWhiteSpace(_selectorLastResultMessage))
            {
                return _selectorLastResultMessage;
            }

            if (!_loginRuntime.HasWorldInformation)
            {
                return "Dispatch WorldInformation to refine simulator recommendations.";
            }

            return _loginAccountIsAdult
                ? "Browse recommended worlds, then select one for channel entry."
                : "Browse recommended worlds. Adult-only entries remain gated.";
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
                SelectorRequestResultCode resultCode = EvaluateWorldSelectorRequestResult(worldId);
                if (resultCode != SelectorRequestResultCode.Success)
                {
                    SetSelectorRequestResult(resultCode, BuildSelectorRequestResultMessage(resultCode, worldId, channelIndex));
                    RefreshWorldChannelSelectorWindows();
                    SyncRecommendWorldWindow();
                    SyncLoginEntryDialogs();
                    return;
                }

                _selectorBrowseWorldId = worldId;
                DispatchLoginRuntimePacket(LoginPacketType.CheckUserLimitResult, out _);
                SetSelectorRequestResult(SelectorRequestResultCode.Success, null);

                if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ChannelSelect) is ChannelSelectWindow channelSelectWindow)
                {
                    channelSelectWindow.Show();
                    uiWindowManager.BringToFront(channelSelectWindow);
                }

                SyncLoginEntryDialogs();
                return;
            }

            SelectorRequestResultCode channelResult = EvaluateChannelSelectorRequestResult(worldId, channelIndex);
            if (channelResult != SelectorRequestResultCode.Success)
            {
                SetSelectorRequestResult(channelResult, BuildSelectorRequestResultMessage(channelResult, worldId, channelIndex));
                RefreshWorldChannelSelectorWindows();
                SyncRecommendWorldWindow();
                SyncLoginEntryDialogs();
                return;
            }

            _simulatorWorldId = worldId;
            _simulatorChannelIndex = channelIndex;
            _selectorBrowseWorldId = worldId;
            SetSelectorRequestResult(SelectorRequestResultCode.Success, null);
            RefreshWorldChannelSelectorWindows();

            uiWindowManager?.HideWindow(MapSimulatorWindowNames.WorldSelect);
            uiWindowManager?.HideWindow(MapSimulatorWindowNames.ChannelSelect);

            if (IsLoginRuntimeSceneActive)
            {
                DispatchLoginRuntimePacket(LoginPacketType.SelectWorldResult, out string runtimeMessage);
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
                _ => null,
            };
        }

        private void InitializeLoginCharacterRoster()
        {
            int targetMapId = ResolveLoginCharacterTargetMapId();
            List<LoginCharacterRosterEntry> entries = new();

            CharacterBuild currentBuild = _playerManager?.Player?.Build?.Clone();
            if (currentBuild != null)
            {
                currentBuild.Name = string.IsNullOrWhiteSpace(currentBuild.Name) ? "ExplorerGM" : currentBuild.Name;
                currentBuild.Level = Math.Max(currentBuild.Level, 30);
                currentBuild.GuildName = string.IsNullOrWhiteSpace(currentBuild.GuildName) ? "Maple GM" : currentBuild.GuildName;
                entries.Add(new LoginCharacterRosterEntry(currentBuild, targetMapId, canDelete: false));
            }

            CharacterBuild femaleBuild = _playerManager?.Loader?.LoadDefaultFemale();
            if (femaleBuild != null)
            {
                femaleBuild.Name = "Rondo";
                femaleBuild.Level = 18;
                femaleBuild.GuildName = "Lith Harbor";
                entries.Add(new LoginCharacterRosterEntry(femaleBuild, targetMapId));
            }

            CharacterBuild randomBuild = _playerManager?.Loader?.LoadRandom();
            if (randomBuild != null)
            {
                randomBuild.Name = "Rin";
                randomBuild.Level = 24;
                randomBuild.GuildName = "Sleepywood";
                entries.Add(new LoginCharacterRosterEntry(randomBuild, targetMapId));
            }

            _loginCharacterRoster.SetEntries(entries);
            _loginCharacterStatusMessage = entries.Count > 0
                ? "Choose a character and press Enter to request field entry."
                : "Unable to populate the simulator character roster.";
            HideLoginUtilityDialog();

            WireLoginCharacterSelectWindow();
            SyncLoginCharacterSelectWindow();
            SyncLoginEntryDialogs();
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
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterSelect) is not CharacterSelectWindow characterSelectWindow)
            {
                SyncLoginCharacterDetailWindow();
                return;
            }

            bool shouldShow = IsLoginRuntimeSceneActive &&
                              (_loginRuntime.CurrentStep == LoginStep.CharacterSelect ||
                               _loginRuntime.CurrentStep == LoginStep.ViewAllCharacters);
            if (!shouldShow)
            {
                characterSelectWindow.Hide();
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
                canEnter,
                canDelete);
            characterSelectWindow.Show();
            uiWindowManager.BringToFront(characterSelectWindow);
            SyncLoginCharacterDetailWindow();
        }

        private void SyncLoginEntryDialogs()
        {
            if (uiWindowManager == null)
            {
                return;
            }

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

            bool shouldShow = false;
            string body = string.Empty;
            bool showProgress = false;
            float progress = 0f;

            if (_selectorRequestKind != SelectorRequestKind.None)
            {
                shouldShow = true;
                body = _selectorRequestStatusMessage ?? "Waiting for the login bootstrap reply.";
                showProgress = true;
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

            if (!shouldShow)
            {
                noticeWindow.Hide();
                return;
            }

            noticeWindow.Configure("Connection Notice", body, showProgress, progress);
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

            utilityDialogWindow.Configure(
                _loginUtilityDialogTitle,
                _loginUtilityDialogBody,
                _loginUtilityDialogPrimaryLabel,
                _loginUtilityDialogSecondaryLabel,
                showPrimaryButton: true,
                showSecondaryButton: _loginUtilityDialogShowSecondaryButton);
            utilityDialogWindow.Show();
            uiWindowManager.BringToFront(utilityDialogWindow);
        }

        private void ShowLoginUtilityDialog(
            string title,
            string body,
            string primaryLabel,
            string secondaryLabel,
            bool showSecondaryButton,
            LoginUtilityDialogAction action,
            int targetIndex = -1)
        {
            _loginUtilityDialogTitle = string.IsNullOrWhiteSpace(title) ? "Login Utility" : title;
            _loginUtilityDialogBody = body ?? string.Empty;
            _loginUtilityDialogPrimaryLabel = string.IsNullOrWhiteSpace(primaryLabel) ? "OK" : primaryLabel;
            _loginUtilityDialogSecondaryLabel = string.IsNullOrWhiteSpace(secondaryLabel) ? "Cancel" : secondaryLabel;
            _loginUtilityDialogShowSecondaryButton = showSecondaryButton;
            _loginUtilityDialogAction = action;
            _loginUtilityDialogTargetIndex = targetIndex;
            SyncLoginEntryDialogs();
        }

        private void HideLoginUtilityDialog()
        {
            _loginUtilityDialogAction = LoginUtilityDialogAction.None;
            _loginUtilityDialogBody = string.Empty;
            _loginUtilityDialogShowSecondaryButton = false;
            _loginUtilityDialogTargetIndex = -1;
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.LoginUtilityDialog) is LoginUtilityDialogWindow utilityDialogWindow)
            {
                utilityDialogWindow.Hide();
            }
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
            SyncLoginCharacterSelectWindow();
            SyncLoginEntryDialogs();
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
                    "OK",
                    "Cancel",
                    showSecondaryButton: false,
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
                    "OK",
                    "Cancel",
                    showSecondaryButton: false,
                    LoginUtilityDialogAction.DismissOnly);
                return;
            }

            _playerManager?.CreatePlayerFromBuild(entry.Build.Clone());
            RefreshSkillWindowForJob(entry.Build.Job);

            if (_loadMapCallback == null || !QueueMapTransfer(entry.FieldMapId, null))
            {
                _loginCharacterStatusMessage = $"Selected {entry.Build.Name}, but map loading is unavailable.";
                SyncLoginCharacterSelectWindow();
                ShowLoginUtilityDialog(
                    "Login Utility",
                    _loginCharacterStatusMessage,
                    "OK",
                    "Cancel",
                    showSecondaryButton: false,
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
            _loginCharacterStatusMessage = "New character creation is not implemented yet.";
            SyncLoginCharacterSelectWindow();
            ShowLoginUtilityDialog(
                "Login Utility",
                "New character creation is not implemented in MapSimulator yet.",
                "OK",
                "Cancel",
                showSecondaryButton: false,
                LoginUtilityDialogAction.DismissOnly);
        }

        private void HandleLoginCharacterDeleteRequested()
        {
            LoginCharacterRosterEntry selectedEntry = _loginCharacterRoster.SelectedEntry;
            if (selectedEntry == null || !selectedEntry.CanDelete)
            {
                _loginCharacterStatusMessage = "The selected character cannot be deleted.";
                SyncLoginCharacterSelectWindow();
                ShowLoginUtilityDialog(
                    "Login Utility",
                    _loginCharacterStatusMessage,
                    "OK",
                    "Cancel",
                    showSecondaryButton: false,
                    LoginUtilityDialogAction.DismissOnly);
                return;
            }

            ShowLoginUtilityDialog(
                "Login Utility",
                $"Delete {selectedEntry.Build.Name} from the simulator roster?",
                "Delete",
                "Cancel",
                showSecondaryButton: true,
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
                default:
                    HideLoginUtilityDialog();
                    break;
            }

            SyncLoginCharacterSelectWindow();
            SyncLoginEntryDialogs();
        }

        private void HandleLoginUtilitySecondaryRequested()
        {
            HideLoginUtilityDialog();
            SyncLoginEntryDialogs();
        }

        private void ExecuteLoginCharacterDeleteConfirmation()
        {
            if (_loginUtilityDialogTargetIndex >= 0)
            {
                _loginCharacterRoster.Select(_loginUtilityDialogTargetIndex);
            }

            if (!_loginCharacterRoster.DeleteSelected(out LoginCharacterRosterEntry deletedEntry))
            {
                _loginCharacterStatusMessage = "The selected character cannot be deleted.";
            }
            else
            {
                _loginCharacterStatusMessage = deletedEntry == null
                    ? "Character deleted from the simulator roster."
                    : $"Deleted {deletedEntry.Build.Name} from the simulator roster.";
            }

            HideLoginUtilityDialog();
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

                _loginWorldMetadataByWorld[worldId] = new LoginWorldSelectorMetadata(worldId, channels, requiresAdultWorldAccess);
            }
        }

        private void ClearLoginWorldSelectorMetadata()
        {
            _loginWorldMetadataByWorld.Clear();
            _loginRecommendedWorldIds.Clear();
            _loginLatestConnectedWorldId = null;
            _selectorLastResultCode = SelectorRequestResultCode.None;
            _selectorLastResultMessage = null;
            _nextLoginWorldPopulationUpdateAt = int.MinValue;
        }

        private void ApplyLoginWorldSelectorPacket(LoginPacketType packetType)
        {
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
                    EnsureLoginWorldSelectorMetadata(GetRegisteredWorldSelectorIds());
                    UpdateRecommendedLoginWorlds();
                    break;
                case LoginPacketType.RecommendWorldMessage:
                    EnsureLoginWorldSelectorMetadata(GetRegisteredWorldSelectorIds());
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

        private void UpdateLatestConnectedWorld()
        {
            if (_loginWorldMetadataByWorld.Count == 0)
            {
                _loginLatestConnectedWorldId = null;
                return;
            }

            int latestWorldId = _loginWorldMetadataByWorld.ContainsKey(_simulatorWorldId)
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


        // Debug rendering data (collected during draw, rendered in separate pass)
        private struct DebugDrawData
        {
            public Rectangle Rect;
            public string Text;
            public bool IsValid;
        }

        // Cached StringBuilder for debug text to avoid GC allocations every frame
        private readonly StringBuilder _debugStringBuilder = new StringBuilder(256);

        // Cached navigation help strings to avoid string.Format every frame
        private string _navHelpTextMobOn;
        private string _navHelpTextMobOff;
        private readonly MobAttackSystem _mobAttackSystem = new MobAttackSystem();

        private readonly struct QuestGatedMapObjectState
        {
            public QuestGatedMapObjectState(ObjectInstanceQuest[] questInfo, string[] dynamicTags, bool hiddenByMap)
            {
                QuestInfo = questInfo ?? Array.Empty<ObjectInstanceQuest>();
                DynamicTags = dynamicTags ?? Array.Empty<string>();
                HiddenByMap = hiddenByMap;
            }

            public ObjectInstanceQuest[] QuestInfo { get; }
            public string[] DynamicTags { get; }
            public bool HiddenByMap { get; }
        }

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
            _mapTransferDestinations = new MapTransferDestinationStore();
            _skillMacroStore = new SkillMacroStore();

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
            _chat.MessageSubmitted = HandleChatMessageSubmitted;

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
            _mobAttackSystem.SetPuppetTargeting(
                () => _mobPool?.ActivePuppets,
                (puppet, _, __, currentTick) =>
                {
                    bool consumedSummon = puppet != null
                        && (_playerManager?.Skills?.TryConsumeSummonByObjectId(puppet.ObjectId) ?? false);

                    if (puppet != null && !consumedSummon)
                    {
                        puppet.IsActive = false;
                    }

                    _mobPool?.UpdatePuppets(currentTick);
                    _mobPool?.SyncPuppetTargets(currentTick);
                });
            _mobAttackSystem.SetMobTargeting(mobId => _mobPool?.GetMob(mobId));
            _mobAttackSystem.SetPlayerGroundedAccessor(() => _playerManager?.IsPlayerOnGround() ?? true);
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
            WzImage uiGuildBbsImage = Program.FindImage("UI", "GuildBBS.img");
            WzImage uiBuffIconImage = Program.FindImage("UI", "BuffIcon.img");

            WzImage uiStatusBarImage = Program.FindImage("UI", "StatusBar.img");
            WzImage uiStatus2BarImage = Program.FindImage("UI", "StatusBar2.img");

            // Skill.wz and String.wz for skill window content
            WzFile skillWzFile = null;
            WzFile stringWzFile = null;
            try
            {
                var fileManager = WzFileManager.fileManager;
                if (fileManager != null)
                {
                    var skillDir = fileManager["skill"];
                    skillWzFile = skillDir?.WzFileParent;
                    var stringDir = fileManager["string"];
                    stringWzFile = stringDir?.WzFileParent;
                }
            }
            catch { }

            _gameState.IsBigBangUpdate = WzFileManager.IsBigBangUpdate(uiWindow2Image); // different rendering for pre and post-bb, to support multiple vers
            _gameState.IsBigBang2Update = WzFileManager.IsBigBang2Update(uiWindow2Image); // chaos update

            // BGM
            WzBinaryProperty bgmProperty = Program.InfoManager.GetBgm(_mapBoard.MapInfo.bgm);
            if (bgmProperty != null)
            {
                _currentBgmName = _mapBoard.MapInfo.bgm;
                _audio = new MonoGameBgmPlayer(bgmProperty, true);
                StartBgmForCurrentFocusState();
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
            ConcurrentBag<WzObject> usedProps = new ConcurrentBag<WzObject>();
            ConcurrentDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects = new();
            
            // Objects
            Task t_tiles = Task.Run(() =>
            {
                foreach (LayeredItem tileObj in _mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;
                    BaseDXDrawableItem mapItem = MapSimulatorLoader.CreateMapItemFromProperty(
                        _texturePool,
                        tileParent,
                        tileObj.X,
                        tileObj.Y,
                        _mapBoard.CenterPoint,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        tileObj is IFlippable flippable && flippable.Flip);
                    if (mapItem == null)
                    {
                        continue;
                    }

                    RegisterQuestGatedMapObject(mapItem, tileObj, questGatedMapObjects);
                    mapObjects[tileObj.LayerNumber].Add(mapItem);
                }
            });

            // Background
            Task t_Background = Task.Run(() =>
            {
                foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);

                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in _mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);

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

                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(_texturePool, reactor, _DxDeviceManager.GraphicsDevice, usedProps);
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

                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(_texturePool, npc, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, usedProps);
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

                    MobItem npcItem = MapSimulatorLoader.CreateMobFromProperty(_texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, _soundManager, usedProps);

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
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(_texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, usedProps);
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
                this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(_texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps, false);
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
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, uiBasicImage, uiBuffIconImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate);
                    if (statusBar != null) {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
            });

            // UI Windows (Inventory, Equipment, Skills, Quest)
            Task t_uiWindows = Task.Run(() => {
                if (_gameState.IsCashShopMap)
                {
                    return;
                }

                if (_gameState.IsLoginMap)
                {
                    uiWindowManager ??= new UIWindowManager();
                    UIWindowLoader.RegisterLoginEntryWindows(
                        uiWindowManager,
                        uiWindow1Image,
                        uiWindow2Image,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginCharacterDetailWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterConnectionNoticeWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginUtilityDialogWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                }
                else
                {
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        skillWzFile, stringWzFile,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate);
                    UIWindowLoader.RegisterGuildBbsWindow(
                        uiWindowManager,
                        uiGuildBbsImage,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        new Point(
                            Math.Max(24, (_renderParams.RenderWidth / 2) - 367),
                            Math.Max(24, (_renderParams.RenderHeight / 2) - 263)));
                }
            });

            while (!t_tiles.IsCompleted || !t_Background.IsCompleted || !t_reactor.IsCompleted || !t_npc.IsCompleted || !t_mobs.IsCompleted || !t_portal.IsCompleted ||
                !t_tooltips.IsCompleted || !t_cursor.IsCompleted || !t_spine.IsCompleted || !t_minimap.IsCompleted || !t_statusBar.IsCompleted || !t_uiWindows.IsCompleted)
            {
                Thread.Sleep(100);
            }

            ReplaceQuestGatedMapObjects(questGatedMapObjects);

            RegisterStatusBarPopupUtilityWindows(uiStatus2BarImage, uiBasicImage, soundUIImage);

            // Set fonts on UI windows after all tasks complete
            uiWindowManager?.SetFonts(_fontChat);
            WireWorldChannelSelectorWindows();
            WireRecommendWorldWindow();
            WireLoginCharacterSelectWindow();
            WireLoginEntryDialogWindows();
            WireQuestLogWindowData();
            WireMemoMailboxWindowData();
            WireSocialListWindowData();
            WireMessengerWindowData();
            WireGuildBbsWindowData();
            WireMapleTvWindowData();
            WireProgressionUtilityWindowLaunchers();
            WireMiniRoomWindowData();
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemMaker) is ItemMakerUI itemMakerWindow)
            {
                itemMakerWindow.SetCraftingState(
                    _playerManager?.Player?.Level ?? 1,
                    _playerManager?.Player?.Build?.TraitCraft ?? 0,
                    HasItemMakerRequiredEquip,
                    MatchesItemMakerQuestRequirement);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindowReload)
            {
                cashShopWindowReload.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindowReload)
            {
                mtsWindowReload.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            RefreshMapTransferWindow();
            RefreshWorldMapWindow();
            LoadNpcQuestAlertIcons(uiWindow1Image, uiWindow2Image);

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
            _specialFieldRuntime.Initialize(_DxDeviceManager.GraphicsDevice, _soundManager);

            ///////////////////////////////////////////////
            ////// Default positioning for character //////
            ///////////////////////////////////////////////
            ResolveSpawnPosition(out float spawnX, out float spawnY);

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
            ResetLoginRuntimeForCurrentMap(currTickCount);
            InitializeAuthoredDynamicObjectTagStates();

            InitializePlayerManager(spawnX, spawnY);
            if (!_gameState.IsLoginMap)
            {
                InitializeFieldRuleRuntime(currTickCount);
            }
            else
            {
                _gameState.PlayerControlEnabled = false;
                InitializeLoginCharacterRoster();
            }
            _specialFieldRuntime.BindMap(_mapBoard);
            SyncBattlefieldLocalAppearance();

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
            _npcInteractionOverlay = new NpcInteractionOverlay(GraphicsDevice);
            _npcInteractionOverlay.SetFont(_fontChat);
            RegisterChatCommands();

            // Initialize pickup notice UI (bottom right corner messages)
            _pickupNoticeUI.Initialize(_fontChat, _debugBoundaryTexture, Width, Height);

            // Initialize limited view field (fog of war)
            _limitedViewField.Initialize(_DxDeviceManager.GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight);
            _temporaryPortalField = new TemporaryPortalField(_texturePool, _DxDeviceManager.GraphicsDevice);

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
                statusBarUi.SetBuffStatusProvider(GetStatusBarBuffData);
                statusBarUi.SetCooldownStatusProvider(GetStatusBarCooldownData);
                statusBarUi.SetPreparedSkillProvider(GetPreparedSkillBarData);
                statusBarUi.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
                statusBarUi.SetLowResourceWarningThresholds(_statusBarHpWarningThresholdPercent, _statusBarMpWarningThresholdPercent);
                statusBarUi.BuffCancelRequested = skillId => _playerManager?.Skills?.CancelActiveBuff(skillId);
            }
            if (statusBarChatUI != null)
            {
                statusBarChatUI.SetFont(_fontChat);
                statusBarChatUI.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
                statusBarChatUI.SetChatRenderProvider(_chat.GetRenderState);
                statusBarChatUI.SetPointNotificationRenderProvider(GetStatusBarPointNotificationState);
                statusBarChatUI.ToggleChatRequested = () => _chat.ToggleActive(Environment.TickCount);
                statusBarChatUI.CycleChatTargetRequested = delta => _chat.CycleTarget(delta);
            }

            // Initialize Ability/Stat window with player's CharacterBuild
            // This connects the stat window to the player's actual stats (STR, DEX, INT, LUK, etc.)
            if (uiWindowManager?.AbilityWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.AbilityWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.AbilityWindow.SetFont(_fontDebugValues);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterInfo) != null && _playerManager?.Player?.Build != null)
            {
                UIWindowBase characterInfoWindow = uiWindowManager.GetWindow(MapSimulatorWindowNames.CharacterInfo);
                characterInfoWindow.CharacterBuild = _playerManager.Player.Build;
                characterInfoWindow.SetFont(_fontDebugValues);
                if (characterInfoWindow is UserInfoUI userInfoWindow)
                {
                    userInfoWindow.SetPetController(_playerManager.Pets);
                }
            }
            if (uiWindowManager?.EquipWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.EquipWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.EquipWindow.SetFont(_fontChat);
                if (uiWindowManager.EquipWindow is EquipUI equipWindow)
                {
                    equipWindow.SetPetController(_playerManager.Pets);
                    equipWindow.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);
                }
                if (uiWindowManager.EquipWindow is EquipUIBigBang equipBigBang)
                {
                    equipBigBang.SetPetController(_playerManager.Pets);
                    equipBigBang.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);
                    equipBigBang.SetMechanicEquipmentController(_playerManager.CompanionEquipment?.Mechanic);
                    equipBigBang.SetAndroidEquipmentController(_playerManager.CompanionEquipment?.Android);
                }
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow && _playerManager?.Player?.Build != null)
            {
                itemUpgradeWindow.CharacterBuild = _playerManager.Player.Build;
                itemUpgradeWindow.SetFont(_fontChat);
                itemUpgradeWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindowRebuild)
            {
                cashShopWindowRebuild.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindowRebuild)
            {
                mtsWindowRebuild.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            if (uiWindowManager?.SkillWindow != null)
            {
                uiWindowManager.SkillWindow.SetFont(_fontChat);
            }
            if (uiWindowManager?.SkillWindow != null)
            {
                uiWindowManager.SkillWindow.SetFont(_fontChat);
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
            _mobAttackSystem.Clear();

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
            _mobAttackSystem.Clear();

            // Clear combat effects (only map-specific effects like mob HP bars)
            _combatEffects?.ClearMapState();
            _fieldEffects?.ResetAllEffects();
            _specialFieldRuntime.Reset();

            // Prepare player manager for map change (preserves character, caches, skill levels)
            _playerManager?.PrepareForMapChange();
            _passengerSync.Clear();
            _escortFollow.Clear();
            _fieldRuleRuntime = null;
            _lastFieldRestrictionMessageTime = int.MinValue;
            _lastFieldRestrictionMessage = null;

            // Clear arrays
            _mapObjectsArray = null;
            _questGatedMapObjects.Clear();
            _authoredDynamicObjectTagStates.Clear();
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
            _pendingMapSpawnTarget = null;
            ClearPassiveTransferRequest();

            // Deactivate chat input (but preserve message history)
            _chat.Deactivate();
            _npcInteractionOverlay?.Close();
            _activeNpcInteractionNpc = null;
            _activeNpcInteractionNpcId = 0;
            _npcQuestFeedback.Clear();
            ResetPetSpeechEventState();
            _gameState.ExitDirectionModeImmediate();
            _scriptedDirectionModeOwnerActive = false;
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
            WzImage uiGuildBbsImage = Program.FindImage("UI", "GuildBBS.img");
            WzImage uiBuffIconImage = Program.FindImage("UI", "BuffIcon.img");
            WzImage uiStatusBarImage = Program.FindImage("UI", "StatusBar.img");
            WzImage uiStatus2BarImage = Program.FindImage("UI", "StatusBar2.img");

            // Skill.wz and String.wz for skill window content
            WzFile skillWzFile = null;
            WzFile stringWzFile = null;
            try
            {
                var fileManager = WzFileManager.fileManager;
                if (fileManager != null)
                {
                    var skillDir = fileManager["skill"];
                    skillWzFile = skillDir?.WzFileParent;
                    var stringDir = fileManager["string"];
                    stringWzFile = stringDir?.WzFileParent;
                }
            }
            catch { }

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

                WzBinaryProperty bgmProperty = Program.InfoManager.GetBgm(newBgmName);
                if (bgmProperty != null)
                {
                    _currentBgmName = newBgmName;
                    _audio = new MonoGameBgmPlayer(bgmProperty, true);
                    StartBgmForCurrentFocusState();
                }
                else
                {
                    _currentBgmName = null;
                    _isBgmPausedForFocusLoss = false;
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

            ConcurrentBag<WzObject> usedProps = new ConcurrentBag<WzObject>();
            ConcurrentDictionary<BaseDXDrawableItem, QuestGatedMapObjectState> questGatedMapObjects = new();

            // Load map objects in parallel
            Task t_tiles = Task.Run(() =>
            {
                foreach (LayeredItem tileObj in _mapBoard.BoardItems.TileObjs)
                {
                    WzImageProperty tileParent = (WzImageProperty)tileObj.BaseInfo.ParentObject;
                    BaseDXDrawableItem mapItem = MapSimulatorLoader.CreateMapItemFromProperty(
                        _texturePool,
                        tileParent,
                        tileObj.X,
                        tileObj.Y,
                        _mapBoard.CenterPoint,
                        _DxDeviceManager.GraphicsDevice,
                        usedProps,
                        tileObj is IFlippable flippable && flippable.Flip);
                    if (mapItem == null)
                    {
                        continue;
                    }

                    RegisterQuestGatedMapObject(mapItem, tileObj, questGatedMapObjects);
                    mapObjects[tileObj.LayerNumber].Add(mapItem);
                }
            });

            Task t_Background = Task.Run(() =>
            {
                foreach (BackgroundInstance background in _mapBoard.BoardItems.BackBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_back.Add(bgItem);
                }
                foreach (BackgroundInstance background in _mapBoard.BoardItems.FrontBackgrounds)
                {
                    WzImageProperty bgParent = (WzImageProperty)background.BaseInfo.ParentObject;
                    BackgroundItem bgItem = MapSimulatorLoader.CreateBackgroundFromProperty(_texturePool, bgParent, background, _DxDeviceManager.GraphicsDevice, usedProps, background.Flip);
                    if (bgItem != null)
                        backgrounds_front.Add(bgItem);
                }
            });

            Task t_reactor = Task.Run(() =>
            {
                foreach (ReactorInstance reactor in _mapBoard.BoardItems.Reactors)
                {
                    ReactorItem reactorItem = MapSimulatorLoader.CreateReactorFromProperty(_texturePool, reactor, _DxDeviceManager.GraphicsDevice, usedProps);
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
                    NpcItem npcItem = MapSimulatorLoader.CreateNpcFromProperty(_texturePool, npc, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, usedProps);
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
                    MobItem mobItem = MapSimulatorLoader.CreateMobFromProperty(_texturePool, mob, UserScreenScaleFactor, _DxDeviceManager.GraphicsDevice, _soundManager, usedProps);
                    mapObjects_Mobs.Add(mobItem);
                }
            });

            Task t_portal = Task.Run(() =>
            {
                WzSubProperty portalParent = (WzSubProperty)mapHelperImage["portal"];
                WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
                foreach (PortalInstance portal in _mapBoard.BoardItems.Portals)
                {
                    PortalItem portalItem = MapSimulatorLoader.CreatePortalFromProperty(_texturePool, gameParent, portal, _DxDeviceManager.GraphicsDevice, usedProps);
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
                    Tuple<StatusBarUI, StatusBarChatUI> statusBar = MapSimulatorLoader.CreateStatusBarFromProperty(uiStatusBarImage, uiStatus2BarImage, uiBasicImage, uiBuffIconImage, _mapBoard, GraphicsDevice, UserScreenScaleFactor, _renderParams, soundUIImage, _gameState.IsBigBangUpdate);
                    if (statusBar != null)
                    {
                        statusBarUi = statusBar.Item1;
                        statusBarChatUI = statusBar.Item2;
                    }
                }
            });

            // Reuse existing UI Windows if available (UI windows are preserved across map changes)
            // This preserves skill data, hotkey assignments, and other UI state
            Task t_uiWindows = Task.Run(() =>
            {
                if (_gameState.IsCashShopMap)
                {
                    return;
                }

                if (_gameState.IsLoginMap)
                {
                    uiWindowManager ??= new UIWindowManager();
                    UIWindowLoader.RegisterLoginEntryWindows(
                        uiWindowManager,
                        uiWindow1Image,
                        uiWindow2Image,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginCharacterDetailWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterConnectionNoticeWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                    UIWindowLoader.RegisterLoginUtilityDialogWindow(
                        uiWindowManager,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight);
                }
                else if (uiWindowManager == null || uiWindowManager.InventoryWindow == null)
                {
                    uiWindowManager = UIWindowLoader.CreateUIWindowManager(
                        uiWindow1Image, uiWindow2Image, uiBasicImage, soundUIImage,
                        skillWzFile, stringWzFile,
                        GraphicsDevice, _renderParams.RenderWidth, _renderParams.RenderHeight, _gameState.IsBigBangUpdate);
                    UIWindowLoader.RegisterGuildBbsWindow(
                        uiWindowManager,
                        uiGuildBbsImage,
                        uiBasicImage,
                        soundUIImage,
                        GraphicsDevice,
                        new Point(
                            Math.Max(24, (_renderParams.RenderWidth / 2) - 367),
                            Math.Max(24, (_renderParams.RenderHeight / 2) - 263)));
                }
            });

            // Reuse existing cursor if available (cursor is preserved across map changes)
            Task t_cursor = Task.Run(() =>
            {
                if (this.mouseCursor == null)
                {
                    WzImageProperty cursorImageProperty = (WzImageProperty)uiBasicImage["Cursor"];
                    this.mouseCursor = MapSimulatorLoader.CreateMouseCursorFromProperty(_texturePool, cursorImageProperty, 0, 0, _DxDeviceManager.GraphicsDevice, usedProps, false);
                }
            });

            // Wait for all loading tasks
            Task.WaitAll(t_tiles, t_Background, t_reactor, t_npc, t_mobs, t_portal, t_tooltips, t_minimap, t_statusBar, t_uiWindows, t_cursor);
            ReplaceQuestGatedMapObjects(questGatedMapObjects);

            // Set fonts on UI windows after all tasks complete
            uiWindowManager?.SetFonts(_fontChat);
            WireWorldChannelSelectorWindows();
            WireRecommendWorldWindow();
            WireLoginCharacterSelectWindow();
            WireLoginEntryDialogWindows();
            WireQuestLogWindowData();
            WireMemoMailboxWindowData();
            WireSocialListWindowData();
            WireMessengerWindowData();
            WireGuildBbsWindowData();
            WireMapleTvWindowData();
            WireProgressionUtilityWindowLaunchers();
            WireMiniRoomWindowData();
            RefreshMapTransferWindow();
            RefreshWorldMapWindow();
            LoadNpcQuestAlertIcons(uiWindow1Image, uiWindow2Image);

            // Initialize status bar character stats display after map change
            if (statusBarUi != null)
            {
                statusBarUi.SetCharacterStatsProvider(_fontDebugValues, GetCharacterStatsData);
                statusBarUi.SetBuffStatusProvider(GetStatusBarBuffData);
                statusBarUi.SetCooldownStatusProvider(GetStatusBarCooldownData);
                statusBarUi.SetPreparedSkillProvider(GetPreparedSkillBarData);
                statusBarUi.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
                statusBarUi.SetLowResourceWarningThresholds(_statusBarHpWarningThresholdPercent, _statusBarMpWarningThresholdPercent);
                statusBarUi.BuffCancelRequested = skillId => _playerManager?.Skills?.CancelActiveBuff(skillId);
            }
            if (statusBarChatUI != null)
            {
                statusBarChatUI.SetFont(_fontChat);
                statusBarChatUI.SetPixelTexture(_DxDeviceManager.GraphicsDevice);
                statusBarChatUI.SetChatRenderProvider(_chat.GetRenderState);
                statusBarChatUI.ToggleChatRequested = () => _chat.ToggleActive(Environment.TickCount);
                statusBarChatUI.CycleChatTargetRequested = delta => _chat.CycleTarget(delta);
            }

            // Reconnect Ability/Stat window to player's CharacterBuild after map change
            if (uiWindowManager?.AbilityWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.AbilityWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.AbilityWindow.SetFont(_fontDebugValues);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CharacterInfo) != null && _playerManager?.Player?.Build != null)
            {
                UIWindowBase characterInfoWindow = uiWindowManager.GetWindow(MapSimulatorWindowNames.CharacterInfo);
                characterInfoWindow.CharacterBuild = _playerManager.Player.Build;
                characterInfoWindow.SetFont(_fontDebugValues);
                if (characterInfoWindow is UserInfoUI userInfoWindow)
                {
                    userInfoWindow.SetPetController(_playerManager.Pets);
                }
            }
            if (uiWindowManager?.EquipWindow != null && _playerManager?.Player?.Build != null)
            {
                uiWindowManager.EquipWindow.CharacterBuild = _playerManager.Player.Build;
                uiWindowManager.EquipWindow.SetFont(_fontChat);
                if (uiWindowManager.EquipWindow is EquipUI equipWindow)
                {
                    equipWindow.SetPetController(_playerManager.Pets);
                    equipWindow.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);
                }
                if (uiWindowManager.EquipWindow is EquipUIBigBang equipBigBang)
                {
                    equipBigBang.SetPetController(_playerManager.Pets);
                    equipBigBang.SetDragonEquipmentController(_playerManager.CompanionEquipment?.Dragon);
                    equipBigBang.SetMechanicEquipmentController(_playerManager.CompanionEquipment?.Mechanic);
                    equipBigBang.SetAndroidEquipmentController(_playerManager.CompanionEquipment?.Android);
                }
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.ItemUpgrade) is ItemUpgradeUI itemUpgradeWindow && _playerManager?.Player?.Build != null)
            {
                itemUpgradeWindow.CharacterBuild = _playerManager.Player.Build;
                itemUpgradeWindow.SetFont(_fontChat);
                itemUpgradeWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.CashShop) is AdminShopDialogUI cashShopWindow)
            {
                cashShopWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
            }
            if (uiWindowManager?.GetWindow(MapSimulatorWindowNames.Mts) is AdminShopDialogUI mtsWindow)
            {
                mtsWindow.SetInventory(uiWindowManager.InventoryWindow as IInventoryRuntime);
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
            ResolveSpawnPosition(out float spawnX, out float spawnY);

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
            ResetLoginRuntimeForCurrentMap(currTickCount);
            InitializeAuthoredDynamicObjectTagStates();
            if (!_gameState.IsLoginMap)
            {
                if (_playerManager != null && _playerManager.Player != null)
                {
                    ReconnectPlayerToMap(spawnX, spawnY);
                }
                else
                {
                    InitializePlayerManager(spawnX, spawnY);
                }

                InitializeFieldRuleRuntime(currTickCount);
            }
            else
            {
                _gameState.PlayerControlEnabled = false;
                if (_playerManager == null || _playerManager.Player == null)
                {
                    InitializePlayerManager(spawnX, spawnY);
                }

                InitializeLoginCharacterRoster();
            }
            _specialFieldRuntime.BindMap(_mapBoard);
            SyncBattlefieldLocalAppearance();

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
            ConcurrentBag<WzObject> usedPropsTemp = new ConcurrentBag<WzObject>();
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
                            var shipFrames = MapSimulatorLoader.LoadFrames(_texturePool, animProp, 0, 0, _DxDeviceManager.GraphicsDevice, usedPropsTemp);
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
            SyncBgmPlaybackToWindowFocus();
            _soundManager?.Update();

            float frameRate = 1 / (float)gameTime.ElapsedGameTime.TotalSeconds;
            currTickCount = Environment.TickCount;
            float delta = gameTime.ElapsedGameTime.Milliseconds / 1000f;
            bool isWindowActive = IsActive;
            KeyboardState newKeyboardState = Keyboard.GetState();  // get the newest state
            MouseState newMouseState = GetEffectiveMouseState(Mouse.GetState(), isWindowActive);

            // Update UI Windows - handles ESC to close windows and I/E/S/Q toggles
            // Pass chat state to prevent hotkeys from working while typing
            bool uiWindowsHandledEsc = false;
            if (uiWindowManager != null)
            {
                RefreshQuestUiState();
                uiWindowsHandledEsc = uiWindowManager.Update(gameTime, currTickCount, _chat.IsActive, isWindowActive);
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
            bool bIsAltEnterPressed = isWindowActive &&
                                      newKeyboardState.IsKeyDown(Keys.LeftAlt) &&
                                      newKeyboardState.IsKeyDown(Keys.Enter);
            if (bIsAltEnterPressed)
            {
                _DxDeviceManager.IsFullScreen = !_DxDeviceManager.IsFullScreen;
                _DxDeviceManager.ApplyChanges();
                return;
            }

            // Handle print screen
            if (isWindowActive && newKeyboardState.IsKeyDown(Keys.PrintScreen))
            {
                if (!_screenshotManager.TakeScreenshot && _screenshotManager.IsComplete)
                {
                    _screenshotManager.RequestScreenshot();
                }
            }


            // Handle mouse
            mouseCursor.UpdateCursorState();

            if (IsLoginRuntimeSceneActive)
            {
                UpdateLoginRuntimeFrame(gameTime, newKeyboardState, newMouseState, isWindowActive);
                return;
            }

            // Advance scripted field state before mouse/world interaction handlers so
            // newly opened timed dialogs can claim direction mode on the same frame.
            _specialFieldRuntime.SetWeddingPlayerState(_playerManager?.Player?.Build?.Id, _playerManager?.Player?.Position);
            _specialFieldRuntime.SetSnowBallPlayerState(_playerManager?.Player?.Position);
            _specialFieldRuntime.SetDojoRuntimeState(
                _playerManager?.Player?.HP,
                _playerManager?.Player?.MaxHP,
                _mobPool?.FindBossMob()?.AI?.HpPercent);
            _specialFieldRuntime.SetGuildBossPlayerState(_playerManager?.GetPlayerHitbox());
            _specialFieldRuntime.SetAriantArenaPlayerState(
                _playerManager?.Player?.Build?.Name,
                _playerManager?.Player?.Build?.Job);
            _specialFieldRuntime.Update(gameTime, currTickCount);
            SyncBattlefieldLocalAppearance();
            UpdateDirectionModeState(currTickCount);
            UpdateWorldChannelSelectorRequestState();

            if (isWindowActive)
            {
                NpcInteractionOverlayResult npcOverlayResult = _npcInteractionOverlay != null
                    ? _npcInteractionOverlay.HandleMouse(newMouseState, _oldMouseState, _renderParams.RenderWidth, _renderParams.RenderHeight)
                    : default;
                bool memoryGameMouseConsumed = false;
                if (npcOverlayResult.PrimaryActionEntry != null)
                {
                    HandleNpcOverlayPrimaryAction(npcOverlayResult.PrimaryActionEntry);
                }

                if (!npcOverlayResult.Consumed &&
                    newMouseState.LeftButton == ButtonState.Released &&
                    _oldMouseState.LeftButton == ButtonState.Pressed &&
                    uiWindowManager?.ContainsPoint(newMouseState.X, newMouseState.Y) != true &&
                    _specialFieldRuntime.Minigames.MemoryGame.HandleMouseClick(
                        new Point(newMouseState.X, newMouseState.Y),
                        _renderParams.RenderWidth,
                        _renderParams.RenderHeight,
                        currTickCount,
                        out string memoryGameMouseMessage))
                {
                    memoryGameMouseConsumed = true;
                    if (!string.IsNullOrWhiteSpace(memoryGameMouseMessage))
                    {
                        _chat.AddMessage(memoryGameMouseMessage, new Color(255, 228, 151), currTickCount);
                    }
                }

                if (!npcOverlayResult.Consumed && !memoryGameMouseConsumed)
                {
                    // Avoid leaking the overlay-dismissal click into world interactions while
                    // direction mode is transitioning through its delayed release window.
                    CheckNpcHover(newMouseState);
                    HandleNpcTalkClick(newMouseState);
                    HandlePortalDoubleClick(newMouseState);
                }
            }

            UpdateNpcQuestFeedbackState(currTickCount);
            UpdateNpcIdleSpeechState(currTickCount);
            UpdatePetEventSpeechState(currTickCount);
            UpdatePetIdleSpeechState(currTickCount);
            _mapleTvRuntime.UpdateLocalContext(_playerManager?.Player?.Build?.Name ?? "Player");
            _mapleTvRuntime.Update(currTickCount);

            // Handle portal UP key interaction (player presses UP near portal)
            if (isWindowActive)
            {
                HandlePortalUpInteract(currTickCount);
                HandleGuildBossPulleyInteract(currTickCount);
            }

            _temporaryPortalField?.Update(currTickCount);

            // Handle same-map portal teleport with delay (no fade, just wait for delay)
            if (_sameMapTeleportPending)
            {
                int elapsed = currTickCount - _sameMapTeleportStartTime;
                if (elapsed >= _sameMapTeleportDelay)
                {
                    CompleteSameMapTeleport();
                }
            }

            if (HandlePendingMapChange())
            {
                return;
            }

            // Handle chat input (returns true if chat consumed the input)
            bool uiCapturesKeyboard = uiWindowManager?.CapturesKeyboardInput == true;
            bool chatConsumedInput = isWindowActive &&
                                     !uiCapturesKeyboard &&
                                     _chat.HandleInput(newKeyboardState, _oldKeyboardState, currTickCount);

            // Skip navigation and other key handlers if chat is active
            if (isWindowActive && !chatConsumedInput && !_chat.IsActive && !uiCapturesKeyboard)
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
            if (isWindowActive)
            {
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
                if (_playerManager != null && _playerManager.IsPlayerActive && _gameState.IsPlayerInputEnabled)
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

            // Update moving transport/platform state before entity movement so grounded
            // passengers can stay on a foothold-backed seam for the current frame.
            _dynamicFootholds.Update(currTickCount, deltaSeconds);
            _transportField.Update(currTickCount, deltaSeconds);
            _passengerSync.SyncPlayer(_playerManager?.Player, _dynamicFootholds, _transportField);

            // Update player character
            // Pass chat state to block movement/jump input while typing
            if (_playerManager != null)
            {
                _playerManager.IsPlayerControlEnabled = _gameState.IsPlayerInputEnabled;
                _playerManager.Update(currTickCount, deltaSeconds, _chat.IsActive || uiCapturesKeyboard, isWindowActive);

                if (_gameState.IsPlayerInputEnabled && _playerManager.IsPlayerActive)
                {
                    Vector2 updatedPlayerPosition = _playerManager.GetPlayerPosition();
                    CheckReactorTouch(updatedPlayerPosition.X, updatedPlayerPosition.Y, currentTick: currTickCount);
                }

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
                        bool left = isWindowActive && newKeyboardState.IsKeyDown(Keys.Left);
                        bool right = isWindowActive && newKeyboardState.IsKeyDown(Keys.Right);
                        bool up = isWindowActive && newKeyboardState.IsKeyDown(Keys.Up);
                        bool down = isWindowActive && newKeyboardState.IsKeyDown(Keys.Down);
                        bool shift = isWindowActive &&
                                     (newKeyboardState.IsKeyDown(Keys.LeftShift) || newKeyboardState.IsKeyDown(Keys.RightShift));
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

            UpdateFieldRuleRuntime(currTickCount);
            UpdateReactorRuntime(currTickCount, deltaSeconds);

            // Update field effects (weather messages, fear effect, obstacles)
            // Pass deltaSeconds * 1000 to convert to milliseconds for frame-rate independence
            _fieldEffects.Update(currTickCount, Width, Height, _oldMouseState.X, _oldMouseState.Y, deltaSeconds * 1000f);

            // Update limited view field (fog of war) - use player position if available
            float playerX = _playerManager?.IsPlayerActive == true
                ? _playerManager.GetPlayerPosition().X
                : mapShiftX + _renderParams.RenderWidth / 2f;
            float playerY = _playerManager?.IsPlayerActive == true
                ? _playerManager.GetPlayerPosition().Y
                : mapShiftY + _renderParams.RenderHeight / 2f;
            _limitedViewField.Update(gameTime, playerX, playerY);

            _passengerSync.SyncGroundMobPassengers(_mobPool?.ActiveMobs.Select(m => m?.MovementInfo), _dynamicFootholds, _transportField);

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

        private void SyncBgmPlaybackToWindowFocus()
        {
            _soundManager?.SetFocusActive(IsActive);

            if (_audio == null)
            {
                _isBgmPausedForFocusLoss = false;
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
            if (!IsActive)
            {
                _audio.Pause();
                _isBgmPausedForFocusLoss = true;
            }
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
            float deltaSecondsLocal = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _mobPool?.UpdatePuppets(tickCount);
            _mobPool?.SyncPuppetTargets(tickCount);

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

            EscortProgressionState escortProgressionState = EscortProgressionController.ResolveState(_mobsArray);

            for (int i = 0; i < _mobsArray.Length; i++)
            {
                MobItem mobItem = _mobsArray[i];
                if (mobItem == null || mobItem.MovementInfo == null)
                    continue;

                mobItem.MovementEnabled = _gameState.MobMovementEnabled;
                bool escortFollowActive = mobItem.AI?.IsEscortMob == true
                    && EscortProgressionController.CanMobFollow(mobItem, escortProgressionState)
                    && _escortFollow.UpdateEscortFollow(_playerManager?.Player, mobItem.MovementInfo);
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

            List<long> expiredKeys = null;
            foreach (KeyValuePair<long, int> entry in _appliedMobSkillEffects)
            {
                if (currentTick < entry.Value)
                {
                    continue;
                }

                expiredKeys ??= new List<long>();
                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
            {
                return;
            }

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                _appliedMobSkillEffects.Remove(expiredKeys[i]);
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
            if (sourceMob?.AI == null ||
                !MobSkillStatusMapper.TryGetDefinition(skill.SkillId, out MobSkillStatusDefinition definition))
            {
                return;
            }

            MobSkillRuntimeData runtimeData = ResolveMobSkillRuntimeData(skill.SkillId, skill.Level);
            if (runtimeData == null || runtimeData.DurationMs <= 0)
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
                targetMob?.AI?.ApplyStatusEffect(definition.Effect, runtimeData.DurationMs, currentTick, value);
            }
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
                    if (_playerManager?.Player != null)
                    {
                        return new Vector2(_playerManager.Player.X, _playerManager.Player.Y - 24f);
                    }

                    if (mobItem.AI?.Target?.IsValid == true)
                    {
                        return new Vector2(mobItem.AI.Target.TargetX, mobItem.AI.Target.TargetY);
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

            if (definition.TargetMode == MobSkillStatusTargetMode.Self || _mobPool == null)
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
            WzSubProperty selectedLevel = levelNode?[level.ToString()] as WzSubProperty ?? levelNode?["1"] as WzSubProperty;
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
            WzSubProperty selectedLevel = levelNode?[level.ToString()] as WzSubProperty ?? levelNode?["1"] as WzSubProperty;
            if (selectedLevel == null)
            {
                return null;
            }

            WzVectorProperty lt = selectedLevel["lt"] as WzVectorProperty;
            WzVectorProperty rb = selectedLevel["rb"] as WzVectorProperty;
            int durationSeconds = MapleLib.WzLib.WzStructure.InfoTool.GetInt(selectedLevel["time"], 0);

            var runtimeData = new MobSkillRuntimeData
            {
                X = MapleLib.WzLib.WzStructure.InfoTool.GetInt(selectedLevel["x"], 0),
                Y = MapleLib.WzLib.WzStructure.InfoTool.GetInt(selectedLevel["y"], 0),
                Hp = MapleLib.WzLib.WzStructure.InfoTool.GetInt(selectedLevel["hp"], 0),
                DurationMs = Math.Max(0, durationSeconds) * 1000,
                Lt = lt != null ? new Point(lt.X.Value, lt.Y.Value) : null,
                Rb = rb != null ? new Point(rb.X.Value, rb.Y.Value) : null
            };

            _mobSkillRuntimeCache[cacheKey] = runtimeData;
            return runtimeData;
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
                uiWindowManager?.ShowWindow(MapSimulatorWindowNames.Trunk);
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
                        HasItemMakerRequiredEquip,
                        MatchesItemMakerQuestRequirement);
                    itemMakerWindow.ApplyLaunchContext(entry.Subtitle);
                }

                uiWindowManager?.ShowWindow(MapSimulatorWindowNames.ItemMaker);
                return;
            }

            if (entry.PrimaryActionKind == NpcInteractionActionKind.OpenItemUpgrade)
            {
                _npcInteractionOverlay?.Close();
                if (TryShowItemUpgradeWindow(out ItemUpgradeUI itemUpgradeWindow))
                {
                    itemUpgradeWindow.PrepareNpcLaunch();
                }

                return;
            }

            if (entry.PrimaryActionKind != NpcInteractionActionKind.QuestPrimary ||
                entry.QuestId is not int questId ||
                _activeNpcInteractionNpc == null ||
                _activeNpcInteractionNpcId == 0)
                return;

            QuestActionResult result = _questRuntime.TryPerformPrimaryAction(questId, _activeNpcInteractionNpcId, _playerManager?.Player?.Build);
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
                ShowNpcQuestFeedback(result, currTickCount);
                OpenNpcInteraction(_activeNpcInteractionNpc, result.PreferredQuestId ?? questId);
            }
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

            NpcItem npc = _npcsArray.FirstOrDefault(candidate =>
                candidate?.NpcInstance?.NpcInfo != null &&
                int.TryParse(candidate.NpcInstance.NpcInfo.ID, out int npcId) &&
                npcId == _npcQuestFeedback.ActiveNpcId);
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
                if (pet != null && pet.HasAutoSpeechEvent(eventType))
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
                return;
            }

            _npcQuestAvailableIcon = LoadNpcQuestAlertIcon(questIconProperty, "0");
            _npcQuestInProgressIcon = LoadNpcQuestAlertIcon(questIconProperty, "1");
            _npcQuestCompletableIcon = LoadNpcQuestAlertIcon(questIconProperty, "2");
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

            return inventory.CanAcceptItem(inventoryType, itemId, Math.Max(1, drop.Quantity));
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
                _pickupNoticeUI.AddMesoPickup(drop.MesoAmount, currentTime, noticeSource, sourceName);
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
                _pickupNoticeUI.AddItemPickup(
                    itemName,
                    drop.Quantity,
                    currentTime,
                    itemIcon,
                    drop.IsRare,
                    itemTypeName,
                    noticeSource,
                    sourceName);
                AddItemToInventoryWindow(drop.ItemId, drop.Quantity);
            }
            else if (drop.Type == Pools.DropType.QuestItem)
            {
                int itemId = int.TryParse(drop.ItemId, out int parsedItemId) ? parsedItemId : 0;
                string itemName = itemId > 0 ? ResolvePickupItemName(itemId) : "Quest Item";
                Texture2D itemIcon = itemId > 0 ? LoadInventoryItemIcon(itemId) : null;
                _pickupNoticeUI.AddQuestItemPickup(itemName, currentTime, itemIcon, noticeSource, sourceName);
            }
        }

        private void HandlePickupAttemptFailed(Pools.DropPickupAttemptResult result)
        {
            if (result == null)
            {
                return;
            }

            int currentTime = Environment.TickCount;
            switch (result.FailureReason)
            {
                case Pools.DropPickupFailureReason.InventoryFull:
                    _pickupNoticeUI.AddInventoryFullMessage(currentTime);
                    break;
                case Pools.DropPickupFailureReason.OwnershipRestricted:
                    _pickupNoticeUI.AddCantPickupMessage("You may not loot this item yet.", currentTime);
                    break;
                case Pools.DropPickupFailureReason.Unavailable:
                    _pickupNoticeUI.AddCantPickupMessage("Unable to pick up the item.", currentTime);
                    break;
            }
        }

        private Texture2D LoadInventoryItemIcon(int itemId)
        {
            WzSubProperty infoProperty = LoadInventoryItemInfoProperty(itemId);
            WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as WzCanvasProperty
                                          ?? infoProperty?["icon"] as WzCanvasProperty;
            return iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(GraphicsDevice);
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

        private WzSubProperty LoadInventoryItemInfoProperty(int itemId)
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
            WzSubProperty itemProperty = itemImage[itemText] as WzSubProperty;
            return itemProperty?["info"] as WzSubProperty;
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

        private void DrawNpcQuestAlerts(in Managers.RenderContext renderContext)
        {
            if (_npcsArray == null || _playerManager?.Player?.Build == null)
            {
                return;
            }

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

            NpcItem npc = _npcsArray.FirstOrDefault(candidate =>
                candidate?.NpcInstance?.NpcInfo != null &&
                int.TryParse(candidate.NpcInstance.NpcInfo.ID, out int npcId) &&
                npcId == _npcQuestFeedback.ActiveNpcId);
            if (npc == null)
            {
                return;
            }

            IDXObject currentFrame = npc.GetCurrentFrame();
            int npcTop = npc.CurrentY - (currentFrame?.Height ?? npc.NpcInstance.Height);
            Vector2 textSize = _fontChat.MeasureString(_npcQuestFeedback.ActiveText);
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
            _spriteBatch.DrawString(_fontChat, _npcQuestFeedback.ActiveText, new Vector2(boxX + 9, boxY + 6), textColor);
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
            Vector2 textSize = _fontChat.MeasureString(pet.ActiveSpeechText);
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
            _spriteBatch.DrawString(_fontChat, pet.ActiveSpeechText, new Vector2(boxX + 9, boxY + 6), textColor);
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
            var touchedReactors = _reactorPool.FindTouchReactorAroundLocalUser(playerX, playerY);

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

            _specialFieldRuntime?.Minigames?.Coconut?.TryHandleNormalAttack(worldHitbox, currentTick, skillId);

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
            if (_reactorPool == null || _reactorsArray == null || _reactorsArray.Length == 0)
                return;

            _reactorPool.Update(currentTick, deltaSeconds);

            bool[] visibleReactors = new bool[_reactorsArray.Length];
            foreach (var (reactor, index, _) in _reactorPool.GetRenderableReactors())
            {
                if (reactor == null || index < 0 || index >= _reactorsArray.Length)
                    continue;

                visibleReactors[index] = true;

                var (state, _) = _reactorPool.GetReactorAnimationState(index);
                reactor.SetAnimationState(state, currentTick);
            }

            for (int i = 0; i < _reactorsArray.Length; i++)
            {
                ReactorItem reactor = _reactorsArray[i];
                if (reactor != null)
                {
                    reactor.SetVisible(visibleReactors[i]);
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
                _gameState.PendingMapChange = true;
                _gameState.PendingMapId = nearestHiddenPortal.tm;
                _gameState.PendingPortalName = nearestHiddenPortal.tn;
                return true;
            }

            return false;
        }

        private void HandleGuildBossPulleyInteract(int currentTime)
        {
            GuildBossField guildBoss = _specialFieldRuntime?.SpecialEffects?.GuildBoss;
            if (guildBoss?.IsActive != true
                || _gameState.PendingMapChange
                || _sameMapTeleportPending
                || !_gameState.IsPlayerInputEnabled
                || _playerManager?.IsPlayerActive != true
                || !_playerManager.Input.IsPressed(InputAction.Interact))
            {
                return;
            }

            Rectangle playerHitbox = _playerManager.GetPlayerHitbox();
            if (!guildBoss.TryHandleLocalPulleyInteract(playerHitbox, currentTime, out string message))
            {
                return;
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
            bool scriptedOwnerActive = (_npcInteractionOverlay?.IsVisible == true)
                || _specialFieldRuntime.HasBlockingScriptedSequence;

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
            _dropPool.SetGroundLevelLookup((x, y) =>
            {
                // Ground level offset - the meso icon origin is typically at center,
                // so we need to move the drop position up so the icon's bottom is on the platform
                // Meso icons are ~20-24px tall, so move up by ~15px
                return y - 18;
            });

            // Set up pickup sound and notice callbacks
            _dropPool.SetOnPickupResolved(HandleDropPickedUp);

            // Set up death effect and drop spawn callbacks
            _mobPool.SetOnMobDied(mob =>
            {
                int currentTick = Environment.TickCount;
                _combatEffects.AddDeathEffectForMob(mob, currentTick);
                _questRuntime.RecordMobKill(mob?.MobInstance);

                bool suppressRewards = mob?.AI?.DeathType == MobDeathType.Bomb ||
                                       mob?.AI?.DeathType == MobDeathType.Miss ||
                                       mob?.AI?.DeathType == MobDeathType.Swallowed ||
                                       mob?.AI?.DeathType == MobDeathType.Timeout ||
                                       (mob?.MobInstance?.MobInfo?.MobData?.Escort ?? 0) > 0;
                if (suppressRewards)
                {
                    return;
                }

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

            if (_fieldEffects.TryIsObstacleOn(tag, out bool isOn))
            {
                return isOn;
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
                _fieldEffects.OnFieldObstacleOnOff(tag, isEnabled.Value, Math.Max(0, transitionTimeMs), resolvedCurrentTime);
                return true;
            }

            return _fieldEffects.ClearObstacleState(tag);
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
            _playerManager.SetSoundManager(_soundManager);
            //_playerManager.Combat.OnPickupAttemptFailed = HandlePickupAttemptFailed;
            //_playerManager.Combat.EvaluatePickupAvailability = EvaluatePickupAvailability;
            _playerManager.SetCurrentMapIdProvider(() => _mapBoard?.MapInfo?.id ?? -1);
            _playerManager.SetReactorAttackAreaHandler(TriggerAttackReactors);

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

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            _playerManager.Skills.SetFieldSkillRestrictionEvaluator(
                skill => FieldSkillRestrictionEvaluator.CanUseSkill(fieldLimit, skill));
            _playerManager.Skills.SetFieldSkillRestrictionMessageProvider(
                skill => FieldSkillRestrictionEvaluator.GetRestrictionMessage(fieldLimit, skill));
            _playerManager.SetJumpRestrictionHandler(
                () => FieldInteractionRestrictionEvaluator.GetJumpRestrictionMessage(fieldLimit),
                ShowFieldRestrictionMessage);
        }

        private void ConfigureSkillUIBindings()
        {
            if (_playerManager?.Skills == null || uiWindowManager == null)
                return;

            _playerManager.Skills.OnSkillCast = HandlePlayerSkillCast;
            _playerManager.Skills.OnFieldSkillCastRejected = HandleFieldSkillCastRejected;

            if (uiWindowManager.QuickSlotWindow != null)
            {
                _playerManager.Skills.SetInventoryRuntime(uiWindowManager.InventoryWindow as IInventoryRuntime);
                uiWindowManager.QuickSlotWindow.SetSkillManager(_playerManager.Skills);
                uiWindowManager.QuickSlotWindow.SetSkillLoader(_playerManager.SkillLoader);
                uiWindowManager.QuickSlotWindow.SetInventoryRuntime(uiWindowManager.InventoryWindow as IInventoryRuntime);
                uiWindowManager.QuickSlotWindow.SetMacroProvider(index => uiWindowManager.SkillMacroWindow?.GetMacro(index));
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
                    _chat.AddMessage($"[Party] {speaker}: {macroName}", new Color(124, 255, 172), currTickCount);
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
            }
            else
            {
                _questRuntime.ConfigureMesoRuntime(null, null, null);
            }

            if (uiWindowManager.QuestWindow is QuestUI questWindow)
            {
                questWindow.SetFont(_fontChat);
                questWindow.SetQuestLogProvider((tab, showAllLevels) =>
                    _questRuntime.BuildQuestLogSnapshot(tab, _playerManager?.Player?.Build, showAllLevels));
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
                questAlarmWindow.QuestRequested += OpenQuestFromAlarmWindow;
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

            QuestWindowDetailState state = _questRuntime.GetQuestWindowDetailState(_activeQuestDetailQuestId, _playerManager?.Player?.Build);
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
                _ => null
            };

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

        private QuestWindowActionResult TrackQuestInAlarmWindow(int questId)
        {
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

        private void HandleFieldSkillCastRejected(SkillData skill, string message)
        {
            ShowFieldRestrictionMessage(message);
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

        private void InitializeFieldRuleRuntime(int currentTime)
        {
            _fieldRuleRuntime = new FieldRuleRuntime(_mapBoard?.MapInfo, HasInventoryItem);
            ApplyAmbientFieldWeather(currentTime);

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

            if (updateResult.TransferMapId > 0)
            {
                QueueFieldTransfer(updateResult.TransferMapId);
            }
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

            int configuredReturnMap = _mapBoard.MapInfo.returnMap;
            if (configuredReturnMap <= 0 || configuredReturnMap == MapConstants.MaxMap)
            {
                configuredReturnMap = _mapBoard.MapInfo.forcedReturn;
            }

            if (configuredReturnMap <= 0 || configuredReturnMap == MapConstants.MaxMap)
                return false;

            Tuple<Board, string> result = _loadMapCallback(configuredReturnMap);
            Board returnBoard = result?.Item1;
            PortalInstance townPortal = returnBoard?.BoardItems?.Portals?
                .FirstOrDefault(portal => portal.pt == PortalType.TownPortalPoint)
                ?? returnBoard?.BoardItems?.Portals?
                    .FirstOrDefault(portal => string.Equals(portal.pn, PortalType.TownPortalPoint.ToCode(), StringComparison.OrdinalIgnoreCase))
                ?? returnBoard?.BoardItems?.Portals?
                    .FirstOrDefault(portal => portal.pt == PortalType.StartPoint);

            if (townPortal == null)
                return false;

            returnMapId = configuredReturnMap;
            returnX = townPortal.X;
            returnY = townPortal.Y;
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
            if (_playerManager?.Skills == null)
            {
                return Array.Empty<StatusBarBuffRenderData>();
            }

            IReadOnlyList<StatusBarBuffEntry> buffEntries = _playerManager.Skills.GetStatusBarBuffEntries(currentTime);
            if (buffEntries.Count == 0)
            {
                return Array.Empty<StatusBarBuffRenderData>();
            }

            List<StatusBarBuffRenderData> renderData = new List<StatusBarBuffRenderData>(buffEntries.Count);
            foreach (StatusBarBuffEntry buffEntry in buffEntries)
            {
                renderData.Add(new StatusBarBuffRenderData
                {
                    SkillId = buffEntry.SkillId,
                    SkillName = buffEntry.SkillName,
                    Description = buffEntry.Description,
                    IconKey = buffEntry.IconKey,
                    IconTexture = buffEntry.IconTexture,
                    RemainingMs = buffEntry.RemainingMs,
                    DurationMs = buffEntry.DurationMs,
                    SortOrder = buffEntry.SortOrder,
                    TemporaryStatLabels = buffEntry.TemporaryStatLabels,
                    TemporaryStatDisplayNames = buffEntry.TemporaryStatDisplayNames
                });
            }

            return renderData;
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
            if (_playerManager?.Skills == null || _playerManager.SkillLoader == null)
            {
                return Array.Empty<StatusBarCooldownRenderData>();
            }

            Dictionary<int, int> hotkeys = _playerManager.Skills.GetAllHotkeys();
            if (hotkeys.Count == 0)
            {
                return Array.Empty<StatusBarCooldownRenderData>();
            }

            List<(StatusBarCooldownRenderData RenderData, int CooldownStartTime, int SlotIndex)> renderData =
                new List<(StatusBarCooldownRenderData RenderData, int CooldownStartTime, int SlotIndex)>();
            HashSet<int> processedSkills = new HashSet<int>();
            foreach (KeyValuePair<int, int> hotkey in hotkeys.OrderBy(entry => entry.Key))
            {
                int skillId = hotkey.Value;
                if (skillId <= 0 || !processedSkills.Add(skillId))
                {
                    continue;
                }

                if (!_playerManager.Skills.IsOnCooldown(skillId, currentTime))
                {
                    continue;
                }

                SkillData skill = _playerManager.SkillLoader.LoadSkill(skillId);
                int level = _playerManager.Skills.GetSkillLevel(skillId);
                int remainingMs = _playerManager.Skills.GetCooldownRemaining(skillId, currentTime);
                int durationMs = skill?.GetLevel(level)?.Cooldown ?? remainingMs;
                if (remainingMs <= 0)
                {
                    continue;
                }

                _playerManager.Skills.TryGetCooldownStartTime(skillId, out int cooldownStartTime);

                renderData.Add((new StatusBarCooldownRenderData
                {
                    SkillId = skillId,
                    SkillName = skill?.Name,
                    Description = skill?.Description,
                    IconTexture = skill?.IconTexture,
                    RemainingMs = remainingMs,
                    DurationMs = Math.Max(remainingMs, durationMs)
                }, cooldownStartTime, hotkey.Key));
            }

            return renderData
                .OrderByDescending(entry => entry.CooldownStartTime)
                .ThenBy(entry => entry.SlotIndex)
                .Select(entry => entry.RenderData)
                .ToList();
        }

        private StatusBarPreparedSkillRenderData GetPreparedSkillBarData(int currentTime)
        {
            var preparedSkill = _playerManager?.Skills?.GetPreparedSkill();
            if (preparedSkill == null)
            {
                return null;
            }

            if (!preparedSkill.ShowHudBar)
            {
                return null;
            }

            return new StatusBarPreparedSkillRenderData
            {
                SkillId = preparedSkill.SkillId,
                SkillName = preparedSkill.SkillData?.Name,
                SkinKey = preparedSkill.HudSkinKey,
                RemainingMs = Math.Max(0, preparedSkill.Duration - preparedSkill.Elapsed(currentTime)),
                DurationMs = preparedSkill.Duration,
                GaugeDurationMs = preparedSkill.HudGaugeDurationMs,
                Progress = preparedSkill.Progress(currentTime),
                IsKeydownSkill = preparedSkill.IsKeydownSkill,
                IsHolding = preparedSkill.IsHolding,
                HoldElapsedMs = preparedSkill.HoldElapsed(currentTime),
                MaxHoldDurationMs = preparedSkill.MaxHoldDurationMs
            };
        }

        private static string GetPreparedSkillBarSkin(PreparedSkill preparedSkill)
        {
            return preparedSkill?.HudSkinKey ?? "KeyDownBar";
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
            _mobAttackSystem.Draw(_spriteBatch, _debugBoundaryTexture, mapShiftX, mapShiftY, mapCenterX, mapCenterY, TickCount);
            _renderingManager.DrawDrops(in renderContext); // item/meso drops
            _renderingManager.DrawPortals(in renderContext); // portals
            _temporaryPortalField?.DrawCurrentMap(
                _mapBoard.MapInfo.id,
                _spriteBatch,
                _skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                _renderParams,
                TickCount);
            _renderingManager.DrawReactors(in renderContext); // reactors
            _renderingManager.DrawNpcs(in renderContext); // NPCs - rendered on top
            DrawNpcQuestAlerts(in renderContext);
            DrawNpcQuestFeedback(in renderContext);
            DrawPetIdleSpeechFeedback(in renderContext);
            _renderingManager.DrawTransportation(in renderContext); // ship/balrog
            _renderingManager.DrawBackgrounds(in renderContext, true); // front background
            _specialFieldRuntime.Draw(
                _spriteBatch,
                _skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                mapCenterX,
                mapCenterY,
                TickCount,
                _debugBoundaryTexture,
                _fontDebugValues);

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

            // Boss HP bar should stay behind the map UI layers.
            if (!_gameState.HideUIMode && _combatEffects.HasActiveBossBar)
            {
                _combatEffects.DrawBossHPBar(_spriteBatch);
            }

            // Status bar [layer below minimap]
            if (!_gameState.HideUIMode) {
                DrawUI(gameTime, shiftCenter, _renderParams, mapCenterX, mapCenterY, mouseState, TickCount, IsActive); // status bar and minimap
            }

            if (gameTime.TotalGameTime.TotalSeconds < 5)
            {
                if (!_gameState.IsLoginMap)
                {
                    _spriteBatch.DrawString(_fontNavigationKeysHelper,
                        _gameState.MobMovementEnabled ? _navHelpTextMobOn : _navHelpTextMobOff,
                        new Vector2(20, Height - 190), Color.White);
                }
            }

            if (IsLoginRuntimeSceneActive && !_gameState.HideUIMode)
            {
                DrawLoginRuntimeOverlay();
            }
            
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
                if (statusBarChatUI == null)
                {
                    _chat.Draw(_spriteBatch, TickCount);
                }

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
            if (IsLoginRuntimeSceneActive || _playerManager == null || _playerManager.Player == null)
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
            int mapCenterX, int mapCenterY, Microsoft.Xna.Framework.Input.MouseState mouseState, int TickCount, bool isWindowActive)
        {
            // Status bar [layer below minimap]
            if (statusBarUi != null)
            {
                statusBarUi.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                            mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                            null,
                            _renderParams,
                            TickCount);

                if (isWindowActive)
                {
                    statusBarUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                }

                // StatusBarChatUI may be null for pre-BigBang versions
                if (statusBarChatUI != null)
                {
                    statusBarChatUI.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                                mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                                null,
                                _renderParams,
                                TickCount);
                    if (isWindowActive)
                    {
                        statusBarChatUI.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                    }
                }
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

            // UI Windows (Inventory, Equipment, Skills, Quest)
            // Toggle: I=Inventory, E=Equipment, S=Skills, Q=Quest
            // Handle mouse events for minimap and windows with proper priority
            // Windows are drawn ON TOP of minimap, so they get priority when starting a new drag
            // Once dragging starts, that element keeps exclusive control until mouse is released
            bool minimapIsDragging = miniMapUi != null && miniMapUi.IsDragging;
            bool npcOverlayIsVisible = _npcInteractionOverlay?.IsVisible == true;

            if (uiWindowManager != null)
            {
                uiWindowManager.Draw(_spriteBatch, _skeletonMeshRenderer, gameTime,
                    mapShiftX, mapShiftY, minimapPos.X, minimapPos.Y,
                    null,
                    _renderParams,
                    TickCount);

                if (!isWindowActive)
                {
                    uiWindowManager.ResetAllDragStates();
                }
                // Check UI windows - but not if minimap is ALREADY being dragged
                else if (!minimapIsDragging && !npcOverlayIsVisible)
                {
                    uiWindowManager.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                }
                else
                {
                    // Reset window drag states if minimap is being dragged
                    uiWindowManager.ResetAllDragStates();
                }
            }

            _npcInteractionOverlay?.Draw(_spriteBatch, _renderParams.RenderWidth, _renderParams.RenderHeight);

            // Minimap mouse events
            if (miniMapUi != null && isWindowActive)
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
                    bool windowBlocksMinimap = npcOverlayIsVisible ||
                        (uiWindowManager != null &&
                         (uiWindowManager.IsDraggingWindow || uiWindowManager.ContainsPoint(mouseState.X, mouseState.Y)));

                    if (!windowBlocksMinimap)
                    {
                        miniMapUi.CheckMouseEvent((int)shiftCenter.X, (int)shiftCenter.Y, mouseState, mouseCursor, _renderParams.RenderWidth, _renderParams.RenderHeight);
                    }
                }
            }
        }

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
        /// <summary>
        /// Registers all chat commands
        /// </summary>
        private void RegisterChatCommands()
        {
            _chat.CommandHandler.RegisterCommand(
                "login",
                "Show the login bootstrap runtime state",
                "/login",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }

                    return ChatCommandHandler.CommandResult.Info(
                        _loginRuntime.DescribeStatus()
                        + Environment.NewLine
                        + $"Adult access: {(_loginAccountIsAdult ? "enabled" : "disabled")}");
                });

            _chat.CommandHandler.RegisterCommand(
                "loginstep",
                "Force the login runtime to a specific step",
                "/loginstep <title|world|char|newchar|avatar|vac|enter>",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }

                    if (args.Length == 0 || !LoginRuntimeManager.TryParseStep(args[0], out LoginStep step))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /loginstep <title|world|char|newchar|avatar|vac|enter>");
                    }

                    _loginRuntime.ForceStep(step, "Manual login step override");
                    RefreshWorldChannelSelectorWindows();
                    SyncLoginCharacterSelectWindow();
                    return ChatCommandHandler.CommandResult.Ok(_loginRuntime.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "loginadult",
                "Toggle simulated adult-channel access for the login selectors",
                "/loginadult <on|off>",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info($"Adult access is currently {(_loginAccountIsAdult ? "enabled" : "disabled")}.");
                    }

                    string normalized = args[0].Trim().ToLowerInvariant();
                    if (normalized != "on" && normalized != "off")
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /loginadult <on|off>");
                    }

                    _loginAccountIsAdult = normalized == "on";
                    _selectorLastResultCode = SelectorRequestResultCode.None;
                    _selectorLastResultMessage = null;
                    RefreshWorldChannelSelectorWindows();
                    SyncRecommendWorldWindow();
                    return ChatCommandHandler.CommandResult.Ok($"Adult access {(_loginAccountIsAdult ? "enabled" : "disabled")}.");
                });

            _chat.CommandHandler.RegisterCommand(
                "loginpacket",
                "Dispatch a login bootstrap packet into the runtime",
                "/loginpacket <checkpassword|worldinfo|selectworld|selectchar|vac|recommendworld|latestworld|extracharinfo>",
                args =>
                {
                    if (!IsLoginRuntimeSceneActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Login runtime is only active on login maps");
                    }

                    if (args.Length == 0 || !LoginRuntimeManager.TryParsePacketType(args[0], out LoginPacketType packetType))
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /loginpacket <checkpassword|worldinfo|selectworld|selectchar|vac|recommendworld|latestworld|extracharinfo>");
                    }

                    DispatchLoginRuntimePacket(packetType, out string message);
                    return ChatCommandHandler.CommandResult.Ok(message);
                });

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

                    if (!QueueMapTransfer(mapId, null))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Unable to queue map change to {mapId}.");
                    }

                    return ChatCommandHandler.CommandResult.Ok($"Loading map {mapId}...");
                });

            // /job <jobid> - Change the active player job and refocus skill UI
            _chat.CommandHandler.RegisterCommand(
                "job",
                "Change the player's job ID",
                "/job <jobId>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /job <jobId>");
                    }

                    if (!int.TryParse(args[0], out int jobId))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid job ID: {args[0]}");
                    }

                    if (jobId < 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Job ID must be non-negative");
                    }

                    if (!TrySetPlayerJob(jobId))
                    {
                        return ChatCommandHandler.CommandResult.Error("Player not available");
                    }

                    string jobName = _playerManager?.Player?.Build?.JobName ?? SkillDataLoader.GetJobName(jobId);
                    return ChatCommandHandler.CommandResult.Ok($"Changed job to {jobName} ({jobId})");
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

            _chat.CommandHandler.RegisterCommand(
                "wedding",
                "Inspect or drive the wedding ceremony runtime",
                "/wedding [status|progress <step> <groomId> <brideId>|respond <yes|no>|actor <groom|bride> <x> <y>|end]",
                args =>
                {
                    WeddingField wedding = _specialFieldRuntime.SpecialEffects.Wedding;
                    if (!wedding.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Wedding runtime is only active on wedding maps");
                    }

                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(wedding.DescribeStatus());
                    }

                    if (string.Equals(args[0], "progress", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !int.TryParse(args[1], out int step)
                            || !int.TryParse(args[2], out int groomId)
                            || !int.TryParse(args[3], out int brideId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding progress <step> <groomId> <brideId>");
                        }

                        wedding.OnWeddingProgress(step, groomId, brideId, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }

                    if (string.Equals(args[0], "respond", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding respond <yes|no>");
                        }

                        bool accepted;
                        if (string.Equals(args[1], "yes", StringComparison.OrdinalIgnoreCase))
                        {
                            accepted = true;
                        }
                        else if (string.Equals(args[1], "no", StringComparison.OrdinalIgnoreCase))
                        {
                            accepted = false;
                        }
                        else
                        {
                            return ChatCommandHandler.CommandResult.Error("Wedding response must be yes or no");
                        }

                        WeddingPacketResponse? response = wedding.RespondToCurrentDialog(accepted, currTickCount);
                        return response.HasValue
                            ? ChatCommandHandler.CommandResult.Ok($"{wedding.DescribeStatus()} Sent packet {response.Value}.")
                            : ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }

                    if (string.Equals(args[0], "actor", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !int.TryParse(args[2], out int actorX)
                            || !int.TryParse(args[3], out int actorY))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /wedding actor <groom|bride> <x> <y>");
                        }

                        if (string.Equals(args[1], "groom", StringComparison.OrdinalIgnoreCase))
                        {
                            if (wedding.GroomId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Wedding groom ID is not set yet. Use /wedding progress first.");
                            }

                            wedding.SetParticipantPosition(wedding.GroomId, new Vector2(actorX, actorY));
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        if (string.Equals(args[1], "bride", StringComparison.OrdinalIgnoreCase))
                        {
                            if (wedding.BrideId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Wedding bride ID is not set yet. Use /wedding progress first.");
                            }

                            wedding.SetParticipantPosition(wedding.BrideId, new Vector2(actorX, actorY));
                            return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                        }

                        return ChatCommandHandler.CommandResult.Error("Wedding actor must be groom or bride");
                    }

                    if (string.Equals(args[0], "end", StringComparison.OrdinalIgnoreCase))
                    {
                        wedding.OnWeddingCeremonyEnd(currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(wedding.DescribeStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /wedding [status|progress <step> <groomId> <brideId>|respond <yes|no>|actor <groom|bride> <x> <y>|end]");
                });

            _chat.CommandHandler.RegisterCommand(
                "guildboss",
                "Inspect or update guild boss healer and pulley state",
                "/guildboss [status|healer <y>|pulley <state>]",
                args =>
                {
                    GuildBossField guildBoss = _specialFieldRuntime.SpecialEffects.GuildBoss;
                    if (!guildBoss.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Guild boss runtime is only active on guild boss maps");
                    }

                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(guildBoss.DescribeStatus());
                    }

                    if (string.Equals(args[0], "healer", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int healerY))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildboss healer <y>");
                        }

                        guildBoss.OnHealerMove(healerY, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(guildBoss.DescribeStatus());
                    }

                    if (string.Equals(args[0], "pulley", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int pulleyState))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildboss pulley <state>");
                        }

                        guildBoss.OnPulleyStateChange(pulleyState, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(guildBoss.DescribeStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /guildboss [status|healer <y>|pulley <state>]");
                });

            _chat.CommandHandler.RegisterCommand(
                "witchscore",
                "Inspect or update the Witchtower scoreboard score",
                "/witchscore [score]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Witchtower.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Witchtower scoreboard is only active on Witchtower maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Witchtower.DescribeStatus());
                    }

                    if (!int.TryParse(args[0], out int score))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid Witchtower score: {args[0]}");
                    }

                    _specialFieldRuntime.SpecialEffects.Witchtower.OnScoreUpdate(score, currTickCount);
                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Witchtower.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "partyraid",
                "Inspect or drive the Party Raid runtime shell",
                "/partyraid [status|stage <n>|point <n>|damage <red|blue> <n>|gaugecap <n>|result <point> <bonus> <total> [win|lose|clear]|outcome <win|lose|clear>]",
                args =>
                {
                    PartyRaidField partyRaid = _specialFieldRuntime.PartyRaid;
                    if (!partyRaid.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Party Raid runtime is only active on Party Raid field, boss, or result maps");
                    }

                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(partyRaid.DescribeStatus());
                    }

                    if (string.Equals(args[0], "stage", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int stage))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid stage <n>");
                        }

                        partyRaid.OnFieldSetVariable("stage", stage.ToString());
                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                    }

                    if (string.Equals(args[0], "point", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int point))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid point <n>");
                        }

                        partyRaid.OnPartyValue("point", point.ToString());
                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                    }

                    if (string.Equals(args[0], "damage", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3 || !int.TryParse(args[2], out int damage))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid damage <red|blue> <n>");
                        }

                        if (string.Equals(args[1], "red", StringComparison.OrdinalIgnoreCase))
                        {
                            partyRaid.OnFieldSetVariable("redDamage", damage.ToString());
                            return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                        }

                        if (string.Equals(args[1], "blue", StringComparison.OrdinalIgnoreCase))
                        {
                            partyRaid.OnFieldSetVariable("blueDamage", damage.ToString());
                            return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                        }

                        return ChatCommandHandler.CommandResult.Error("Damage side must be red or blue");
                    }

                    if (string.Equals(args[0], "gaugecap", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int gaugeCap))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid gaugecap <n>");
                        }

                        partyRaid.OnFieldSetVariable("gaugeCap", gaugeCap.ToString());
                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                    }

                    if (string.Equals(args[0], "result", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 4
                            || !int.TryParse(args[1], out int resultPoint)
                            || !int.TryParse(args[2], out int resultBonus)
                            || !int.TryParse(args[3], out int resultTotal))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid result <point> <bonus> <total> [win|lose|clear]");
                        }

                        partyRaid.OnSessionValue("point", resultPoint.ToString());
                        partyRaid.OnSessionValue("bonus", resultBonus.ToString());
                        partyRaid.OnSessionValue("total", resultTotal.ToString());

                        if (args.Length >= 5)
                        {
                            if (!TryParsePartyRaidOutcome(args[4], out PartyRaidResultOutcome outcome))
                            {
                                return ChatCommandHandler.CommandResult.Error("Outcome must be win, lose, or clear");
                            }

                            partyRaid.SetResultOutcome(outcome);
                        }

                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                    }

                    if (string.Equals(args[0], "outcome", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !TryParsePartyRaidOutcome(args[1], out PartyRaidResultOutcome outcome))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /partyraid outcome <win|lose|clear>");
                        }

                        partyRaid.SetResultOutcome(outcome);
                        return ChatCommandHandler.CommandResult.Ok(partyRaid.DescribeStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /partyraid [status|stage <n>|point <n>|damage <red|blue> <n>|gaugecap <n>|result <point> <bonus> <total> [win|lose|clear]|outcome <win|lose|clear>]");
                });

            _chat.CommandHandler.RegisterCommand(
                "battlefield",
                "Inspect or drive the Battlefield timerboard and team score flow",
                "/battlefield [clock [seconds]|score <wolves> <sheep>|team <wolves|sheep|0|1|2|clear>|result [wolves|sheep|draw|auto]]",
                args =>
                {
                    BattlefieldField battlefield = _specialFieldRuntime.SpecialEffects.Battlefield;
                    if (!battlefield.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Battlefield runtime is only active on Battlefield maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(battlefield.DescribeStatus());
                    }

                    if (string.Equals(args[0], "clock", StringComparison.OrdinalIgnoreCase))
                    {
                        int seconds = battlefield.DefaultDurationSeconds;
                        if (args.Length >= 2 && !int.TryParse(args[1], out seconds))
                        {
                            return ChatCommandHandler.CommandResult.Error($"Invalid Battlefield clock seconds: {args[1]}");
                        }

                        battlefield.OnClock(2, seconds, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());
                    }

                    if (string.Equals(args[0], "score", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3
                            || !int.TryParse(args[1], out int wolves)
                            || !int.TryParse(args[2], out int sheep))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /battlefield score <wolves> <sheep>");
                        }

                        battlefield.OnScoreUpdate(wolves, sheep, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());
                    }

                    if (string.Equals(args[0], "team", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !TryParseBattlefieldTeam(args[1], out int? teamId))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /battlefield team <wolves|sheep|0|1|2|clear>");
                        }

                        battlefield.SetLocalTeam(teamId, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());
                    }

                    if (string.Equals(args[0], "result", StringComparison.OrdinalIgnoreCase))
                    {
                        BattlefieldField.BattlefieldWinner winner = BattlefieldField.BattlefieldWinner.None;
                        if (args.Length >= 2 && !TryParseBattlefieldWinner(args[1], out winner))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /battlefield result [wolves|sheep|draw|auto]");
                        }

                        if (winner == BattlefieldField.BattlefieldWinner.None)
                        {
                            winner = battlefield.WolvesScore == battlefield.SheepScore
                                ? BattlefieldField.BattlefieldWinner.Draw
                                : battlefield.WolvesScore > battlefield.SheepScore
                                    ? BattlefieldField.BattlefieldWinner.Wolves
                                    : BattlefieldField.BattlefieldWinner.Sheep;
                        }

                        battlefield.ResolveResult(winner, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(battlefield.DescribeStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /battlefield [clock [seconds]|score <wolves> <sheep>|team <wolves|sheep|0|1|2|clear>|result [wolves|sheep|draw|auto]]");
                });

            _chat.CommandHandler.RegisterCommand(
                "ariantarena",
                "Inspect or drive the Ariant Arena ranking/result HUD",
                "/ariantarena [score <name> <score>|packet <name> <score> [<name> <score> ...]|remove <name>|result|clear]",
                args =>
                {
                    AriantArenaField field = _specialFieldRuntime.Minigames.AriantArena;
                    if (!field.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Ariant Arena runtime is only active on Ariant Arena maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(field.DescribeStatus());
                    }

                    switch (args[0].ToLowerInvariant())
                    {
                        case "score":
                            if (args.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena score <name> <score>");
                            }

                            if (!int.TryParse(args[2], out int score))
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Ariant Arena score: {args[2]}");
                            }

                            field.OnUserScore(args[1], score);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "packet":
                            if (args.Length < 3 || args.Length % 2 == 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena packet <name> <score> [<name> <score> ...]");
                            }

                            var updates = new List<AriantArenaScoreUpdate>();
                            for (int i = 1; i < args.Length; i += 2)
                            {
                                if (!int.TryParse(args[i + 1], out int packetScore))
                                {
                                    return ChatCommandHandler.CommandResult.Error($"Invalid Ariant Arena score: {args[i + 1]}");
                                }

                                updates.Add(new AriantArenaScoreUpdate(args[i], packetScore));
                            }

                            field.ApplyUserScoreBatch(updates);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "remove":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena remove <name>");
                            }

                            field.OnUserScore(args[1], -1);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "result":
                            field.OnShowResult(currTickCount);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "clear":
                            field.ClearScores();
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /ariantarena [score <name> <score>|packet <name> <score> [<name> <score> ...]|remove <name>|result|clear]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "mcarnival",
                "Inspect or drive the Monster Carnival HUD state",
                "/mcarnival [status|tab <mob|skill|guardian>|enter <team> <personalCP> <personalTotal> <myCP> <myTotal> <enemyCP> <enemyTotal>|cp <personalCP> <personalTotal> <team0CP> <team0Total> <team1CP> <team1Total>|request <index> [message]|requestfail <reason>|result <code>|spells <mobIndex> <count>]",
                args =>
                {
                    MonsterCarnivalField field = _specialFieldRuntime.Minigames.MonsterCarnival;
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(field.DescribeStatus());
                    }

                    switch (args[0].ToLowerInvariant())
                    {
                        case "tab":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival tab <mob|skill|guardian>");
                            }

                            return field.TrySetActiveTab(args[1], out string tabMessage)
                                ? ChatCommandHandler.CommandResult.Ok(tabMessage)
                                : ChatCommandHandler.CommandResult.Error(tabMessage);

                        case "enter":
                            if (args.Length < 8)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival enter <team 0|1> <personalCP> <personalTotal> <myCP> <myTotal> <enemyCP> <enemyTotal>");
                            }

                            if (!int.TryParse(args[1], out int teamValue) || (teamValue != 0 && teamValue != 1))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival team must be 0 or 1.");
                            }

                            if (!int.TryParse(args[2], out int personalCp)
                                || !int.TryParse(args[3], out int personalTotalCp)
                                || !int.TryParse(args[4], out int myCp)
                                || !int.TryParse(args[5], out int myTotalCp)
                                || !int.TryParse(args[6], out int enemyCp)
                                || !int.TryParse(args[7], out int enemyTotalCp))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival enter arguments must be integers.");
                            }

                            field.OnEnter((MonsterCarnivalTeam)teamValue, personalCp, personalTotalCp, myCp, myTotalCp, enemyCp, enemyTotalCp);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "cp":
                            if (args.Length < 7)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival cp <personalCP> <personalTotal> <team0CP> <team0Total> <team1CP> <team1Total>");
                            }

                            if (!int.TryParse(args[1], out int updatedPersonalCp)
                                || !int.TryParse(args[2], out int updatedPersonalTotalCp)
                                || !int.TryParse(args[3], out int team0Cp)
                                || !int.TryParse(args[4], out int team0TotalCp)
                                || !int.TryParse(args[5], out int team1Cp)
                                || !int.TryParse(args[6], out int team1TotalCp))
                            {
                                return ChatCommandHandler.CommandResult.Error("Monster Carnival CP arguments must be integers.");
                            }

                            field.UpdateTeamCp(updatedPersonalCp, updatedPersonalTotalCp, team0Cp, team0TotalCp, team1Cp, team1TotalCp);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "request":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival request <index> [message]");
                            }

                            if (!int.TryParse(args[1], out int entryIndex) || entryIndex < 0)
                            {
                                return ChatCommandHandler.CommandResult.Error($"Invalid Monster Carnival entry index: {args[1]}");
                            }

                            string requestMessage = args.Length > 2 ? string.Join(" ", args.Skip(2)) : null;
                            return field.TryRequestActiveEntry(entryIndex, requestMessage, currTickCount, out string requestResult)
                                ? ChatCommandHandler.CommandResult.Ok(requestResult)
                                : ChatCommandHandler.CommandResult.Error(requestResult);

                        case "requestfail":
                            if (args.Length < 2 || !int.TryParse(args[1], out int reasonCode))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival requestfail <reason>");
                            }

                            field.OnRequestFailure(reasonCode, currTickCount);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "result":
                            if (args.Length < 2 || !int.TryParse(args[1], out int resultCode))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival result <code>");
                            }

                            field.OnShowGameResult(resultCode, currTickCount);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());

                        case "spells":
                            if (args.Length < 3
                                || !int.TryParse(args[1], out int mobIndex)
                                || !int.TryParse(args[2], out int spellCount))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival spells <mobIndex> <count>");
                            }

                            return field.TrySetMobSpellCount(mobIndex, spellCount, out string spellMessage)
                                ? ChatCommandHandler.CommandResult.Ok(spellMessage)
                                : ChatCommandHandler.CommandResult.Error(spellMessage);

                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /mcarnival [status|tab|enter|cp|request|requestfail|result|spells] [...]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "dojo",
                "Inspect the Mu Lung Dojo HUD state",
                "/dojo",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }

                    return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "dojoclock",
                "Inspect or update the Mu Lung Dojo timer",
                "/dojoclock [seconds]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }

                    if (!int.TryParse(args[0], out int seconds) || seconds < 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid Dojo timer: {args[0]}");
                    }

                    _specialFieldRuntime.SpecialEffects.Dojo.OnClock(2, seconds, currTickCount);
                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "spacegaga",
                "Inspect or update the SpaceGAGA timerboard",
                "/spacegaga [seconds]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.SpaceGaga.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("SpaceGAGA timerboard is only active on SpaceGAGA maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.SpaceGaga.DescribeStatus());
                    }

                    if (!int.TryParse(args[0], out int seconds))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid SpaceGAGA timer: {args[0]}");
                    }

                    _specialFieldRuntime.SpecialEffects.SpaceGaga.OnClock(2, seconds, currTickCount);
                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.SpaceGaga.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "massacre",
                "Inspect or drive the Massacre timerboard and gauge flow",
                "/massacre [status|clock <seconds>|kill [gauge]|inc <value>|params <maxGauge> <decayPerSec>|reset]",
                args =>
                {
                    MassacreField massacre = _specialFieldRuntime.SpecialEffects.Massacre;
                    if (!massacre.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Massacre HUD is only active on Massacre maps");
                    }

                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(massacre.DescribeStatus());
                    }

                    if (string.Equals(args[0], "clock", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int seconds) || seconds < 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre clock <seconds>");
                        }

                        massacre.OnClock(2, seconds, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());
                    }

                    if (string.Equals(args[0], "kill", StringComparison.OrdinalIgnoreCase))
                    {
                        int gaugeAmount = massacre.DefaultGaugeIncrease;
                        if (args.Length >= 2 && (!int.TryParse(args[1], out gaugeAmount) || gaugeAmount < 0))
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre kill [gauge]");
                        }

                        massacre.AddKill(gaugeAmount, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());
                    }

                    if (string.Equals(args[0], "inc", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 2 || !int.TryParse(args[1], out int incGauge) || incGauge < 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre inc <value>");
                        }

                        massacre.OnMassacreIncGauge(incGauge, currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());
                    }

                    if (string.Equals(args[0], "params", StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length < 3
                            || !int.TryParse(args[1], out int maxGauge)
                            || !int.TryParse(args[2], out int decayPerSec)
                            || maxGauge <= 0
                            || decayPerSec < 0)
                        {
                            return ChatCommandHandler.CommandResult.Error("Usage: /massacre params <maxGauge> <decayPerSec>");
                        }

                        massacre.SetGaugeParameters(maxGauge, decayPerSec);
                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());
                    }

                    if (string.Equals(args[0], "reset", StringComparison.OrdinalIgnoreCase))
                    {
                        massacre.ResetRoundState();
                        return ChatCommandHandler.CommandResult.Ok(massacre.DescribeStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /massacre [status|clock <seconds>|kill [gauge]|inc <value>|params <maxGauge> <decayPerSec>|reset]");
                });

            _chat.CommandHandler.RegisterCommand(
                "dojoenergy",
                "Inspect or update the Mu Lung Dojo energy gauge",
                "/dojoenergy [0-10000]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }

                    if (!int.TryParse(args[0], out int energy) || energy < 0 || energy > 10000)
                    {
                        return ChatCommandHandler.CommandResult.Error("Energy must be between 0 and 10000");
                    }

                    _specialFieldRuntime.SpecialEffects.Dojo.SetEnergy(energy);
                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "dojostage",
                "Inspect or update the Mu Lung Dojo floor banner",
                "/dojostage [0-32]",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }

                    if (!int.TryParse(args[0], out int stage) || stage < 0 || stage > 32)
                    {
                        return ChatCommandHandler.CommandResult.Error("Stage must be between 0 and 32");
                    }

                    _specialFieldRuntime.SpecialEffects.Dojo.SetStage(stage, currTickCount);
                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "dojoresult",
                "Trigger Mu Lung Dojo clear or time-over presentation",
                "/dojoresult <clear|timeover>",
                args =>
                {
                    if (!_specialFieldRuntime.SpecialEffects.Dojo.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Mu Lung Dojo HUD is only active on Dojo maps");
                    }

                    if (args.Length != 1)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /dojoresult <clear|timeover>");
                    }

                    if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _specialFieldRuntime.SpecialEffects.Dojo.ShowClearResult(currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }

                    if (string.Equals(args[0], "timeover", StringComparison.OrdinalIgnoreCase))
                    {
                        _specialFieldRuntime.SpecialEffects.Dojo.ShowTimeOverResult(currTickCount);
                        return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.SpecialEffects.Dojo.DescribeStatus());
                    }

                    return ChatCommandHandler.CommandResult.Error("Usage: /dojoresult <clear|timeover>");
                });

            _chat.CommandHandler.RegisterCommand(
                "cookiepoint",
                "Inspect or update the Cookie House event score",
                "/cookiepoint [score]",
                args =>
                {
                    if (!_specialFieldRuntime.CookieHouse.IsActive)
                    {
                        return ChatCommandHandler.CommandResult.Error("Cookie House HUD is only active on Cookie House maps");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(_specialFieldRuntime.CookieHouse.DescribeStatus());
                    }

                    if (!int.TryParse(args[0], out int point))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid Cookie House score: {args[0]}");
                    }

                    _specialFieldRuntime.CookieHouse.OnPointUpdate(point);
                    return ChatCommandHandler.CommandResult.Ok(_specialFieldRuntime.CookieHouse.DescribeStatus());
                });

            _chat.CommandHandler.RegisterCommand(
                "guildbbs",
                "Inspect or drive the Guild BBS runtime",
                "/guildbbs [open|status|write|edit|register|cancel|notice|reply|replydelete|delete|select <threadId>]",
                args =>
                {
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(_guildBbsRuntime.DescribeStatus());
                    }

                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok("Guild BBS window opened.");
                        case "write":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.BeginWrite());
                        case "edit":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.BeginEditSelected());
                        case "register":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.SubmitCompose());
                        case "cancel":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.CancelCompose());
                        case "notice":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.ToggleNotice());
                        case "reply":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.GuildBbs);
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.AddReply());
                        case "replydelete":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.DeleteLatestReply());
                        case "delete":
                            return ChatCommandHandler.CommandResult.Ok(_guildBbsRuntime.DeleteSelectedThread());
                        case "select":
                            if (args.Length < 2 || !int.TryParse(args[1], out int threadId))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs select <threadId>");
                            }

                            _guildBbsRuntime.SelectThread(threadId);
                            return ChatCommandHandler.CommandResult.Ok($"Selected Guild BBS thread #{threadId}.");
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /guildbbs [open|status|write|edit|register|cancel|notice|reply|replydelete|delete|select <threadId>]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "memorygame",
                "Drive the MiniRoom Match Cards runtime",
                "/memorygame <open|ready|start|flip|tie|giveup|end|status|packet|remote> [...]",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /memorygame <open|ready|start|flip|tie|giveup|end|status|remote> [...]");
                    }

                    MemoryGameField field = _specialFieldRuntime.Minigames.MemoryGame;
                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                        {
                            string playerOne = args.Length >= 2 ? args[1] : "Player";
                            string playerTwo = args.Length >= 3 ? args[2] : "Opponent";
                            int rows = args.Length >= 4 && int.TryParse(args[3], out int parsedRows) ? parsedRows : 4;
                            int columns = args.Length >= 5 && int.TryParse(args[4], out int parsedColumns) ? parsedColumns : 4;
                            field.OpenRoom(playerOneName: playerOne, playerTwoName: playerTwo, rows: rows, columns: columns);
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MiniRoom);
                            return ChatCommandHandler.CommandResult.Ok(field.DescribeStatus());
                        }
                        case "ready":
                        {
                            int playerIndex = args.Length >= 2 && int.TryParse(args[1], out int parsedPlayer) ? parsedPlayer : 0;
                            bool isReady = args.Length < 3 || !string.Equals(args[2], "off", StringComparison.OrdinalIgnoreCase);
                            if (!field.TrySetReady(playerIndex, isReady, out string readyMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(readyMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(readyMessage);
                        }
                        case "start":
                        {
                            if (!field.TryStartGame(currTickCount, out string startMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(startMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(startMessage);
                        }
                        case "flip":
                        {
                            if (args.Length < 2 || !int.TryParse(args[1], out int cardIndex))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame flip <cardIndex>");
                            }

                            if (!field.TryRevealCard(cardIndex, currTickCount, out string flipMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(flipMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(flipMessage);
                        }
                        case "tie":
                        {
                            if (!field.TryClaimTie(out string tieMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(tieMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(tieMessage);
                        }
                        case "giveup":
                        {
                            int playerIndex = args.Length >= 2 && int.TryParse(args[1], out int parsedPlayer) ? parsedPlayer : 0;
                            if (!field.TryGiveUp(playerIndex, out string giveUpMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(giveUpMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(giveUpMessage);
                        }
                        case "end":
                        {
                            if (!field.TryEndRoom(out string endMessage))
                            {
                                return ChatCommandHandler.CommandResult.Error(endMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(endMessage);
                        }
                        case "status":
                            return ChatCommandHandler.CommandResult.Info(field.DescribeStatus());
                        case "packet":
                        {
                            if (args.Length < 2 || !MemoryGameField.TryParsePacketType(args[1], out MemoryGamePacketType packetType))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame packet <open|ready|unready|start|flip|tie|giveup|end|mode> [...]");
                            }

                            int playerIndex = 0;
                            int cardIndex = -1;
                            bool readyState = true;

                            if ((packetType == MemoryGamePacketType.SetReady || packetType == MemoryGamePacketType.GiveUp)
                                && args.Length >= 3
                                && int.TryParse(args[2], out int parsedPacketPlayer))
                            {
                                playerIndex = parsedPacketPlayer;
                            }

                            if (packetType == MemoryGamePacketType.SetReady)
                            {
                                string readyArg = args.Length >= 4
                                    ? args[3]
                                    : args.Length >= 3 && !int.TryParse(args[2], out _)
                                        ? args[2]
                                        : "on";
                                readyState = !string.Equals(readyArg, "off", StringComparison.OrdinalIgnoreCase)
                                    && !string.Equals(readyArg, "unready", StringComparison.OrdinalIgnoreCase);
                            }

                            if (packetType == MemoryGamePacketType.RevealCard)
                            {
                                cardIndex = args.Length >= 3 && int.TryParse(args[2], out int parsedPacketCard) ? parsedPacketCard : -1;
                                playerIndex = args.Length >= 4 && int.TryParse(args[3], out int parsedRevealPlayer) ? parsedRevealPlayer : 0;
                            }

                            string playerOne = args.Length >= 3 && packetType == MemoryGamePacketType.OpenRoom ? args[2] : "Player";
                            string playerTwo = args.Length >= 4 && packetType == MemoryGamePacketType.OpenRoom ? args[3] : "Opponent";
                            int rows = args.Length >= 5 && packetType == MemoryGamePacketType.OpenRoom && int.TryParse(args[4], out int parsedRows) ? parsedRows : 4;
                            int columns = args.Length >= 6 && packetType == MemoryGamePacketType.OpenRoom && int.TryParse(args[5], out int parsedColumns) ? parsedColumns : 4;

                            if (!field.TryDispatchPacket(packetType, currTickCount, out string packetMessage, playerIndex, cardIndex, readyState, playerOne, playerTwo, rows, columns))
                            {
                                return ChatCommandHandler.CommandResult.Error(packetMessage);
                            }

                            if (packetType == MemoryGamePacketType.OpenRoom || packetType == MemoryGamePacketType.SelectMatchCardsMode)
                            {
                                uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MiniRoom);
                            }

                            return ChatCommandHandler.CommandResult.Ok(packetMessage);
                        }
                        case "remote":
                        {
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memorygame remote <ready|unready|start|flip|tie|giveup|end> [...]");
                            }

                            string remoteAction = args[1].ToLowerInvariant();
                            int cardIndex = args.Length >= 3 && int.TryParse(args[2], out int parsedCardIndex) ? parsedCardIndex : -1;
                            int delayMs = args.Length >= 4 && int.TryParse(args[3], out int parsedDelayMs) ? parsedDelayMs : 600;
                            if (!field.TryQueueRemoteAction(remoteAction, currTickCount, out string remoteMessage, cardIndex, delayMs))
                            {
                                return ChatCommandHandler.CommandResult.Error(remoteMessage);
                            }

                            return ChatCommandHandler.CommandResult.Ok(remoteMessage);
                        }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /memorygame <open|ready|start|flip|tie|giveup|end|status|packet|remote> [...]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "memo",
                "Drive simulator memo inbox, compose, and package-claim flows",
                "/memo [status|open|compose|send|claim [memoId]|draft <recipient|subject|body|item|meso|clearattachment|reset> ...|deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]]",
                args =>
                {
                    MemoMailboxSnapshot mailboxSnapshot = _memoMailbox.GetSnapshot();
                    MemoMailboxDraftSnapshot draftSnapshot = _memoMailbox.GetDraftSnapshot();
                    if (args.Length == 0 || string.Equals(args[0], "status", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Info(
                            $"Inbox: {mailboxSnapshot.Entries.Count} memo(s), {mailboxSnapshot.UnreadCount} unread, {mailboxSnapshot.ClaimableCount} claimable package(s). Draft to {draftSnapshot.Recipient}: '{draftSnapshot.Subject}'.");
                    }

                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                        case "inbox":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MemoMailbox);
                            return ChatCommandHandler.CommandResult.Ok("Opened the memo inbox.");
                        case "compose":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MemoSend);
                            return ChatCommandHandler.CommandResult.Ok("Opened the memo send dialog. Use /memo draft ... to edit the current draft.");
                        case "send":
                            if (_memoMailbox.TrySendDraft(out string sendMessage))
                            {
                                uiWindowManager?.HideWindow(MapSimulatorWindowNames.MemoSend);
                                uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MemoMailbox);
                                return ChatCommandHandler.CommandResult.Ok(sendMessage);
                            }

                            return ChatCommandHandler.CommandResult.Error(sendMessage);
                        case "claim":
                        {
                            int memoId = -1;
                            if (args.Length >= 2)
                            {
                                if (!int.TryParse(args[1], out memoId) || memoId <= 0)
                                {
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memo claim [memoId]");
                                }
                            }
                            else
                            {
                                memoId = mailboxSnapshot.Entries.FirstOrDefault(entry => entry.CanClaimAttachment)?.MemoId ?? -1;
                            }

                            if (memoId <= 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("No claimable memo package is available.");
                            }

                            _activeMemoAttachmentId = memoId;
                            return _memoMailbox.TryClaimAttachment(memoId, out string claimMessage)
                                ? ChatCommandHandler.CommandResult.Ok(claimMessage)
                                : ChatCommandHandler.CommandResult.Error(claimMessage);
                        }
                        case "draft":
                        {
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Info(
                                    $"Draft to {draftSnapshot.Recipient} / '{draftSnapshot.Subject}' / package {draftSnapshot.AttachmentSummary}. Use /memo draft <recipient|subject|body|item|meso|clearattachment|reset> ...");
                            }

                            string draftAction = args[1].ToLowerInvariant();
                            string payload = string.Join(" ", args.Skip(2));
                            switch (draftAction)
                            {
                                case "recipient":
                                case "to":
                                    if (string.IsNullOrWhiteSpace(payload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft recipient <name>");
                                    }

                                    _memoMailbox.SetDraftRecipient(payload);
                                    return ChatCommandHandler.CommandResult.Ok($"Draft recipient set to {payload.Trim()}.");
                                case "subject":
                                    if (string.IsNullOrWhiteSpace(payload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft subject <text>");
                                    }

                                    _memoMailbox.SetDraftSubject(payload);
                                    return ChatCommandHandler.CommandResult.Ok("Draft subject updated.");
                                case "body":
                                    if (string.IsNullOrWhiteSpace(payload))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft body <text>");
                                    }

                                    _memoMailbox.SetDraftBody(payload);
                                    return ChatCommandHandler.CommandResult.Ok("Draft body updated.");
                                case "item":
                                {
                                    if (args.Length < 3 || !int.TryParse(args[2], out int itemId) || itemId <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft item <itemId> [quantity]");
                                    }

                                    int quantity = args.Length >= 4 && int.TryParse(args[3], out int parsedQuantity)
                                        ? parsedQuantity
                                        : 1;
                                    return _memoMailbox.SetDraftItemAttachment(itemId, quantity, out string itemMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(itemMessage)
                                        : ChatCommandHandler.CommandResult.Error(itemMessage);
                                }
                                case "meso":
                                    if (args.Length < 3 || !int.TryParse(args[2], out int meso))
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Usage: /memo draft meso <amount>");
                                    }

                                    return _memoMailbox.SetDraftMesoAttachment(meso, out string mesoMessage)
                                        ? ChatCommandHandler.CommandResult.Ok(mesoMessage)
                                        : ChatCommandHandler.CommandResult.Error(mesoMessage);
                                case "clearattachment":
                                    _memoMailbox.ClearDraftAttachment();
                                    return ChatCommandHandler.CommandResult.Ok("Draft attachment cleared.");
                                case "reset":
                                    _memoMailbox.ResetDraftState();
                                    return ChatCommandHandler.CommandResult.Ok("Draft reset.");
                                default:
                                    return ChatCommandHandler.CommandResult.Error("Usage: /memo draft <recipient|subject|body|item|meso|clearattachment|reset> ...");
                            }
                        }
                        case "deliver":
                        {
                            string joined = string.Join(" ", args.Skip(1));
                            string[] segments = joined.Split('|');
                            if (segments.Length < 3)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /memo deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]");
                            }

                            string sender = segments[0].Trim();
                            string subject = segments[1].Trim();
                            string body = segments[2].Trim();
                            int attachmentItemId = 0;
                            int attachmentQuantity = 0;
                            int attachmentMeso = 0;

                            if (segments.Length >= 4)
                            {
                                string attachmentSpec = segments[3].Trim();
                                if (attachmentSpec.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] itemParts = attachmentSpec.Split(':');
                                    if (itemParts.Length < 2
                                        || !int.TryParse(itemParts[1], out attachmentItemId)
                                        || attachmentItemId <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Item attachment format is item:<itemId>:<qty>.");
                                    }

                                    attachmentQuantity = itemParts.Length >= 3 && int.TryParse(itemParts[2], out int parsedQty)
                                        ? parsedQty
                                        : 1;
                                }
                                else if (attachmentSpec.StartsWith("meso:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string[] mesoParts = attachmentSpec.Split(':');
                                    if (mesoParts.Length < 2 || !int.TryParse(mesoParts[1], out attachmentMeso) || attachmentMeso <= 0)
                                    {
                                        return ChatCommandHandler.CommandResult.Error("Meso attachment format is meso:<amount>.");
                                    }
                                }
                                else
                                {
                                    return ChatCommandHandler.CommandResult.Error("Attachment format must be item:<itemId>:<qty> or meso:<amount>.");
                                }
                            }

                            _memoMailbox.DeliverMemo(sender, subject, body, DateTimeOffset.Now, false, attachmentItemId, attachmentQuantity, attachmentMeso);
                            return ChatCommandHandler.CommandResult.Ok($"Delivered memo '{subject}' from {sender}.");
                        }
                        default:
                            return ChatCommandHandler.CommandResult.Error("Usage: /memo [status|open|compose|send|claim [memoId]|draft <recipient|subject|body|item|meso|clearattachment|reset> ...|deliver <sender>|<subject>|<body> [|item:<id>:<qty>|meso:<amount>]]");
                    }
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

            _chat.CommandHandler.RegisterCommand(
                "hpwarn",
                "Set the low-HP warning threshold percentage",
                "/hpwarn <percent>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info($"HP warning threshold: {_statusBarHpWarningThresholdPercent}%");
                    }

                    if (!TryUpdateLowResourceWarningThreshold(args[0], isHp: true, out string message))
                    {
                        return ChatCommandHandler.CommandResult.Error(message);
                    }

                    return ChatCommandHandler.CommandResult.Ok(message);
                });

            _chat.CommandHandler.RegisterCommand(
                "mpwarn",
                "Set the low-MP warning threshold percentage",
                "/mpwarn <percent>",
                args =>
                {
                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info($"MP warning threshold: {_statusBarMpWarningThresholdPercent}%");
                    }

                    if (!TryUpdateLowResourceWarningThreshold(args[0], isHp: false, out string message))
                    {
                        return ChatCommandHandler.CommandResult.Error(message);
                    }

                    return ChatCommandHandler.CommandResult.Ok(message);
                });

            _chat.CommandHandler.RegisterCommand(
                "quickslotitem",
                "Assign or clear an inventory-backed quick-slot item",
                "/quickslotitem <slot 1-28> <itemId|clear>",
                args =>
                {
                    if (_playerManager?.Skills == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Player skills are not available");
                    }

                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /quickslotitem <slot 1-28> <itemId|clear>");
                    }

                    if (!int.TryParse(args[0], out int oneBasedSlot) || oneBasedSlot < 1 || oneBasedSlot > SkillManager.TOTAL_SLOT_COUNT)
                    {
                        return ChatCommandHandler.CommandResult.Error("Slot must be between 1 and 28");
                    }

                    int slotIndex = oneBasedSlot - 1;
                    if (string.Equals(args[1], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _playerManager.Skills.ClearHotkey(slotIndex);
                        return ChatCommandHandler.CommandResult.Ok($"Cleared quick-slot {oneBasedSlot}.");
                    }

                    if (!int.TryParse(args[1], out int itemId) || itemId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid item ID: {args[1]}");
                    }

                    if (!_playerManager.Skills.TrySetItemHotkey(slotIndex, itemId))
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Unable to assign item {itemId} to quick-slot {oneBasedSlot}. Only owned USE/CASH entries can be quick-slotted.");
                    }

                    int itemCount = _playerManager.Skills.GetHotkeyItemCount(slotIndex);
                    return ChatCommandHandler.CommandResult.Ok(
                        $"Assigned item {itemId} to quick-slot {oneBasedSlot} (count {itemCount}).");
                });

            _chat.CommandHandler.RegisterCommand(
                "mapletv",
                "Inspect or drive the MapleTV send board and broadcast lifecycle",
                "/mapletv [open|status|sample|set|clear|toggleto|sender|receiver|item|line|wait|result] [...]",
                args =>
                {
                    _mapleTvRuntime.UpdateLocalContext(_playerManager?.Player?.Build?.Name ?? "Player");
                    if (args.Length == 0)
                    {
                        uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MapleTv);
                        return ChatCommandHandler.CommandResult.Info(_mapleTvRuntime.DescribeStatus(currTickCount));
                    }

                    string action = args[0].ToLowerInvariant();
                    switch (action)
                    {
                        case "open":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MapleTv);
                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.DescribeStatus(currTickCount));

                        case "status":
                            return ChatCommandHandler.CommandResult.Info(_mapleTvRuntime.DescribeStatus(currTickCount));

                        case "sample":
                            uiWindowManager?.ShowWindow(MapSimulatorWindowNames.MapleTv);
                            return ChatCommandHandler.CommandResult.Ok(
                                _mapleTvRuntime.LoadSample(
                                    _playerManager?.Player?.Build?.Name ?? "Player",
                                    GetCurrentMapTransferDisplayName()));

                        case "set":
                        {
                            string publishMessage = PublishMapleTvDraft();
                            return publishMessage.StartsWith("MapleTV message set", StringComparison.Ordinal)
                                ? ChatCommandHandler.CommandResult.Ok(publishMessage)
                                : ChatCommandHandler.CommandResult.Error(publishMessage);
                        }

                        case "clear":
                            return ChatCommandHandler.CommandResult.Ok(ClearMapleTvMessage());

                        case "toggleto":
                        case "to":
                            return ChatCommandHandler.CommandResult.Ok(ToggleMapleTvReceiverMode());

                        case "sender":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv sender <name>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.SetSender(string.Join(" ", args.Skip(1))));

                        case "receiver":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv receiver <name|self|clear>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.SetReceiver(string.Join(" ", args.Skip(1))));

                        case "item":
                            if (args.Length < 2 || !int.TryParse(args[1], out int itemId) || itemId < 0)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv item <itemId>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(
                                _mapleTvRuntime.SetItem(itemId, itemId > 0 ? ResolvePickupItemName(itemId) : "Maple TV"));

                        case "line":
                            if (args.Length < 3 || !int.TryParse(args[1], out int lineNumber))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv line <1-5> <text>");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.SetDraftLine(lineNumber, string.Join(" ", args.Skip(2))));

                        case "wait":
                            if (args.Length < 2 || !int.TryParse(args[1], out int durationMs))
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv wait <durationMs>");
                            }

                            string durationMessage = _mapleTvRuntime.SetDuration(durationMs);
                            return durationMs >= 1000 && durationMs <= 60000
                                ? ChatCommandHandler.CommandResult.Ok(durationMessage)
                                : ChatCommandHandler.CommandResult.Error(durationMessage);

                        case "result":
                            if (args.Length < 2)
                            {
                                return ChatCommandHandler.CommandResult.Error("Usage: /mapletv result <success|busy|offline|fail>");
                            }

                            MapleTvSendResultKind? resultKind = args[1].ToLowerInvariant() switch
                            {
                                "success" => MapleTvSendResultKind.Success,
                                "busy" => MapleTvSendResultKind.Busy,
                                "offline" => MapleTvSendResultKind.RecipientOffline,
                                "fail" => MapleTvSendResultKind.Failed,
                                "failed" => MapleTvSendResultKind.Failed,
                                _ => null
                            };

                            if (resultKind == null)
                            {
                                return ChatCommandHandler.CommandResult.Error("Result must be one of: success, busy, offline, fail");
                            }

                            return ChatCommandHandler.CommandResult.Ok(_mapleTvRuntime.OnSendMessageResult(resultKind.Value));

                        default:
                            return ChatCommandHandler.CommandResult.Error(
                                "Usage: /mapletv [open|status|sample|set|clear|toggleto|sender|receiver|item|line|wait|result] [...]");
                    }
                });

            _chat.CommandHandler.RegisterCommand(
                "chair",
                "Activate or clear an owned portable chair",
                "/chair <itemId|clear>",
                args =>
                {
                    if (_playerManager?.Player == null || _playerManager.Loader == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Player runtime is not available");
                    }

                    if (args.Length == 0)
                    {
                        PortableChair activeChair = _playerManager.Player.Build?.ActivePortableChair;
                        return activeChair != null
                            ? ChatCommandHandler.CommandResult.Info($"Active chair: {activeChair.Name} ({activeChair.ItemId})")
                            : ChatCommandHandler.CommandResult.Info("No portable chair is active");
                    }

                    if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        _playerManager.Player.ClearPortableChair();
                        return ChatCommandHandler.CommandResult.Ok("Portable chair cleared.");
                    }

                    if (!int.TryParse(args[0], out int itemId) || itemId <= 0)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Invalid chair item ID: {args[0]}");
                    }

                    if (InventoryItemMetadataResolver.ResolveInventoryType(itemId) != InventoryType.SETUP)
                    {
                        return ChatCommandHandler.CommandResult.Error("Portable chairs must be setup/install items.");
                    }

                    if (!HasInventoryItem(itemId))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Setup item {itemId} is not in the current inventory.");
                    }

                    PortableChair chair = _playerManager.Loader.LoadPortableChair(itemId);
                    if (chair == null)
                    {
                        return ChatCommandHandler.CommandResult.Error($"Unable to load portable chair data for item {itemId}.");
                    }

                    if (!_playerManager.Player.TryActivatePortableChair(chair))
                    {
                        return ChatCommandHandler.CommandResult.Error("Portable chairs can only be activated while standing on a foothold.");
                    }

                    string ridingChairNote = chair.TamingMobItemId is int tamingMobItemId && tamingMobItemId > 0
                        ? $" Riding-chair mount applied: {tamingMobItemId}."
                        : string.Empty;
                    return ChatCommandHandler.CommandResult.Ok($"Activated chair {chair.Name} ({itemId}).{ridingChairNote}");
                });

            _chat.CommandHandler.RegisterCommand(
                "petevent",
                "Trigger a WZ-backed pet auto-speech event",
                "/petevent <rest|levelup|prelevelup|hpalert|nohppotion|nomppotion> [slot 1-3]",
                args =>
                {
                    if (_playerManager?.Pets?.ActivePets == null || _playerManager.Pets.ActivePets.Count == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("No active pets are available");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            "Usage: /petevent <rest|levelup|prelevelup|hpalert|nohppotion|nomppotion> [slot 1-3]");
                    }

                    if (!TryParsePetSpeechEvent(args[0], out PetAutoSpeechEvent eventType, out string eventName))
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Unknown pet event '{args[0]}'. Expected rest, levelup, prelevelup, hpalert, nohppotion, or nomppotion.");
                    }

                    int? petSlotIndex = null;
                    if (args.Length >= 2)
                    {
                        if (!TryParsePetSlot(args[1], out int parsedSlotIndex, out string slotError))
                        {
                            return ChatCommandHandler.CommandResult.Error(slotError);
                        }

                        if (_playerManager.Pets.GetPetAt(parsedSlotIndex) == null)
                        {
                            return ChatCommandHandler.CommandResult.Error($"No active pet is present in slot {parsedSlotIndex + 1}");
                        }

                        petSlotIndex = parsedSlotIndex;
                    }

                    if (!_playerManager.Pets.TryTriggerSpeechEvent(eventType, currTickCount, petSlotIndex))
                    {
                        string slotLabel = petSlotIndex.HasValue
                            ? $"pet {petSlotIndex.Value + 1}"
                            : "the active pet roster";
                        return ChatCommandHandler.CommandResult.Error(
                            $"No loaded speech lines are available for '{eventName}' on {slotLabel}.");
                    }

                    if (petSlotIndex.HasValue)
                    {
                        PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex.Value);
                        return ChatCommandHandler.CommandResult.Ok(
                            $"Triggered {eventName} speech for pet {petSlotIndex.Value + 1} ({pet?.Name ?? "Unknown"}).");
                    }

                    return ChatCommandHandler.CommandResult.Ok(
                        $"Triggered {eventName} speech for the first eligible active pet.");
                });

            _chat.CommandHandler.RegisterCommand(
                "petlevel",
                "Inspect or set the simulated pet command level for WZ command gating",
                "/petlevel [slot 1-3] [level 1-30]",
                args =>
                {
                    if (_playerManager?.Pets?.ActivePets == null || _playerManager.Pets.ActivePets.Count == 0)
                    {
                        return ChatCommandHandler.CommandResult.Error("No active pets are available");
                    }

                    if (args.Length == 0)
                    {
                        return ChatCommandHandler.CommandResult.Info(DescribePetCommandLevels());
                    }

                    if (!TryParsePetSlot(args[0], out int petSlotIndex, out string slotError))
                    {
                        return ChatCommandHandler.CommandResult.Error(slotError);
                    }

                    if (args.Length == 1)
                    {
                        PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex);
                        return pet == null
                            ? ChatCommandHandler.CommandResult.Error($"No active pet is present in slot {petSlotIndex + 1}")
                            : ChatCommandHandler.CommandResult.Info($"Pet {petSlotIndex + 1} ({pet.Name}) command level: {pet.CommandLevel}");
                    }

                    if (!int.TryParse(args[1], out int level) || level < 1 || level > 30)
                    {
                        return ChatCommandHandler.CommandResult.Error("Level must be between 1 and 30");
                    }

                    if (!_playerManager.Pets.TrySetCommandLevel(petSlotIndex, level))
                    {
                        return ChatCommandHandler.CommandResult.Error($"No active pet is present in slot {petSlotIndex + 1}");
                    }

                    PetRuntime updatedPet = _playerManager.Pets.GetPetAt(petSlotIndex);
                    string petName = updatedPet != null
                        ? (!string.IsNullOrWhiteSpace(updatedPet.Name) ? updatedPet.Name : updatedPet.ItemId.ToString())
                        : "Unknown";
                    return ChatCommandHandler.CommandResult.Ok(
                        $"Pet {petSlotIndex + 1} ({petName}) command level set to {level}.");
                });

            _chat.CommandHandler.RegisterCommand(
                "petslang",
                "Trigger the WZ-backed pet slang feedback line for an active pet",
                "/petslang [slot 1-3]",
                args =>
                {
                    if (!TryResolvePetCommandSlot(args, 0, out int petSlotIndex, out string slotError))
                    {
                        return ChatCommandHandler.CommandResult.Error(slotError);
                    }

                    if (!_playerManager.Pets.TryTriggerSlangFeedback(petSlotIndex, currTickCount))
                    {
                        return ChatCommandHandler.CommandResult.Error($"Pet {petSlotIndex + 1} has no slang feedback loaded.");
                    }

                    PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex);
                    return ChatCommandHandler.CommandResult.Ok($"Triggered slang feedback for pet {petSlotIndex + 1} ({pet?.Name ?? "Unknown"}).");
                });

            _chat.CommandHandler.RegisterCommand(
                "petfeed",
                "Trigger a WZ-backed pet feeding feedback line",
                "/petfeed <variant 1-4> <success|fail> [slot 1-3]",
                args =>
                {
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /petfeed <variant 1-4> <success|fail> [slot 1-3]");
                    }

                    if (!int.TryParse(args[0], out int variant) || variant < 1 || variant > 4)
                    {
                        return ChatCommandHandler.CommandResult.Error("Variant must be between 1 and 4");
                    }

                    bool? success = args[1].ToLowerInvariant() switch
                    {
                        "success" => true,
                        "fail" => false,
                        "failure" => false,
                        _ => null
                    };
                    if (success == null)
                    {
                        return ChatCommandHandler.CommandResult.Error("Result must be 'success' or 'fail'");
                    }

                    if (!TryResolvePetCommandSlot(args, 2, out int petSlotIndex, out string slotError))
                    {
                        return ChatCommandHandler.CommandResult.Error(slotError);
                    }

                    if (!_playerManager.Pets.TryTriggerFoodFeedback(petSlotIndex, variant, success.Value, currTickCount))
                    {
                        return ChatCommandHandler.CommandResult.Error(
                            $"Pet {petSlotIndex + 1} has no loaded food feedback for variant {variant}.");
                    }

                    PetRuntime pet = _playerManager.Pets.GetPetAt(petSlotIndex);
                    return ChatCommandHandler.CommandResult.Ok(
                        $"Triggered food feedback {variant} ({(success.Value ? "success" : "fail")}) for pet {petSlotIndex + 1} ({pet?.Name ?? "Unknown"}).");
                });

            _chat.CommandHandler.RegisterCommand(
                "objtag",
                "Publish or clear a dynamic object-tag state",
                "/objtag <tag> <on|off|clear> [transitionMs]",
                args =>
                {
                    if (args.Length < 2)
                    {
                        return ChatCommandHandler.CommandResult.Error("Usage: /objtag <tag> <on|off|clear> [transitionMs]");
                    }

                    string tag = args[0];
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        return ChatCommandHandler.CommandResult.Error("Tag must not be empty");
                    }

                    string action = args[1];
                    bool? isEnabled = action.ToLowerInvariant() switch
                    {
                        "on" => true,
                        "off" => false,
                        "clear" => null,
                        _ => null
                    };

                    if (!string.Equals(action, "on", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(action, "off", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(action, "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        return ChatCommandHandler.CommandResult.Error("State must be one of: on, off, clear");
                    }

                    int transitionMs = 0;
                    if (args.Length >= 3 && !int.TryParse(args[2], out transitionMs))
                    {
                        return ChatCommandHandler.CommandResult.Error("transitionMs must be an integer");
                    }

                    bool changed = SetDynamicObjectTagState(tag, isEnabled, transitionMs, currTickCount);
                    if (!changed)
                    {
                        return ChatCommandHandler.CommandResult.Error($"No published state existed for object tag '{tag}'.");
                    }

                    string stateLabel = isEnabled.HasValue ? (isEnabled.Value ? "ON" : "OFF") : "CLEARED";
                    return ChatCommandHandler.CommandResult.Ok($"Object tag '{tag}' set to {stateLabel}");
                });
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

        private void SyncBattlefieldLocalAppearance()
        {
            BattlefieldField battlefield = _specialFieldRuntime.SpecialEffects.Battlefield;
            PlayerCharacter player = _playerManager?.Player;
            CharacterLoader loader = _playerManager?.Loader;
            CharacterBuild build = player?.Build;

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

            player.Assembler?.ClearCache();
            _battlefieldAppliedTeamId = teamId;
        }

        private void EnsureBattlefieldOriginalEquipmentSnapshot(CharacterBuild build)
        {
            if (_battlefieldOriginalEquipment != null || build == null)
            {
                return;
            }

            _battlefieldOriginalEquipment = new Dictionary<EquipSlot, CharacterPart>();
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
                _battlefieldAppliedTeamId = null;
                return;
            }

            if (_battlefieldOriginalEquipment == null)
            {
                _battlefieldAppliedTeamId = null;
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

            player.Assembler?.ClearCache();
            _battlefieldOriginalEquipment = null;
            _battlefieldAppliedTeamId = null;
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
                outcome = PartyRaidResultOutcome.Unknown;
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
            _nextLoginWorldPopulationUpdateAt = currentTickCount + LoginWorldPopulationUpdateIntervalMs;

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
            _loginRuntime.Update(currTickCount);
            UpdateWorldChannelSelectorRequestState();
            UpdateLoginWorldPopulationDrift();
            SyncLoginWorldSelectionWindows();
            SyncLoginCharacterSelectWindow();
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

        private void UpdateLoginWorldPopulationDrift()
        {
            if (!ShouldUseLoginWorldMetadata ||
                _selectorRequestKind != SelectorRequestKind.None ||
                unchecked(currTickCount - _nextLoginWorldPopulationUpdateAt) < 0)
            {
                return;
            }

            bool changed = false;
            foreach ((int worldId, LoginWorldSelectorMetadata metadata) in _loginWorldMetadataByWorld.ToArray())
            {
                List<ChannelSelectionState> updatedChannels = new(metadata.Channels.Count);
                bool worldChanged = false;

                foreach (ChannelSelectionState channel in metadata.Channels)
                {
                    if (channel.Capacity <= 0)
                    {
                        updatedChannels.Add(channel);
                        continue;
                    }

                    int driftSeed = ((currTickCount / LoginWorldPopulationUpdateIntervalMs) + 1 + (worldId * 7) + (channel.ChannelIndex * 11)) % 9;
                    int occupancyDelta = driftSeed - 4;
                    int nextUserCount = Math.Clamp(channel.UserCount + (occupancyDelta * Math.Max(4, channel.Capacity / 70)), 0, channel.Capacity);
                    bool isCurrentSelection = worldId == _simulatorWorldId && channel.ChannelIndex == _simulatorChannelIndex;
                    bool nextSelectable = nextUserCount < channel.Capacity || isCurrentSelection;

                    if (nextUserCount != channel.UserCount || nextSelectable != channel.IsSelectable)
                    {
                        worldChanged = true;
                    }

                    updatedChannels.Add(new ChannelSelectionState(
                        channel.ChannelIndex,
                        nextUserCount,
                        channel.Capacity,
                        nextSelectable,
                        channel.RequiresAdultAccount));
                }

                if (!worldChanged)
                {
                    continue;
                }

                _loginWorldMetadataByWorld[worldId] = new LoginWorldSelectorMetadata(worldId, updatedChannels, metadata.RequiresAdultAccount);
                changed = true;
            }

            _nextLoginWorldPopulationUpdateAt = currTickCount + LoginWorldPopulationUpdateIntervalMs;

            if (!changed)
            {
                return;
            }

            UpdateRecommendedLoginWorlds();
            RefreshWorldChannelSelectorWindows();
            SyncRecommendWorldWindow();
        }

        private bool HandlePendingMapChange()
        {
            if (_gameState.PendingMapChange && _loadMapCallback != null)
            {
                if (_gameState.PendingMapId == _mapBoard.MapInfo.id && !string.IsNullOrEmpty(_gameState.PendingPortalName))
                {
                    PortalInstance targetPortal = _mapBoard.BoardItems.Portals.FirstOrDefault(portal => portal.pn == _gameState.PendingPortalName);
                    if (targetPortal != null && !_sameMapTeleportPending)
                    {
                        StartSameMapTeleport(
                            targetPortal.X,
                            targetPortal.Y,
                            targetPortal.delay ?? SAME_MAP_PORTAL_DEFAULT_DELAY_MS,
                            currTickCount);
                    }

                    _gameState.PendingMapChange = false;
                    _gameState.PendingMapId = -1;
                    _gameState.PendingPortalName = null;
                }
                else
                {
                    if (_portalFadeState == PortalFadeState.None)
                    {
                        _portalFadeState = PortalFadeState.FadingOut;
                        _screenEffects.FadeOut(PORTAL_FADE_DURATION_MS, currTickCount);
                    }

                    if (_portalFadeState == PortalFadeState.FadingOut)
                    {
                        _screenEffects.UpdateFade(currTickCount);
                        if (_screenEffects.IsFadeOutComplete || !_screenEffects.IsFadeActive)
                        {
                            _gameState.PendingMapChange = false;

                            Tuple<Board, string> result = _loadMapCallback(_gameState.PendingMapId);
                            if (result != null && result.Item1 != null)
                            {
                                int currentMapId = _mapBoard?.MapInfo?.id ?? -1;
                                if (currentMapId >= 0 && _mobsArray != null)
                                {
                                    _mapStateCache.SaveMapState(currentMapId, _mobsArray, currTickCount);
                                }

                                UnloadMapContent();
                                LoadMapContent(result.Item1, result.Item2, _gameState.PendingPortalName);

                                int newMapId = _mapBoard?.MapInfo?.id ?? -1;
                                if (newMapId >= 0 && _mobsArray != null)
                                {
                                    _mapStateCache.RestoreMapState(newMapId, _mobsArray, currTickCount);
                                }

                                _playerManager?.Input?.SyncState();
                            }

                            _gameState.PendingMapId = -1;
                            _gameState.PendingPortalName = null;

                            _portalFadeState = PortalFadeState.FadingIn;
                            _screenEffects.FadeIn(PORTAL_FADE_DURATION_MS, currTickCount);
                        }

                        return true;
                    }
                }
            }

            if (_portalFadeState == PortalFadeState.FadingIn)
            {
                if (_screenEffects.IsFadeInComplete || !_screenEffects.IsFadeActive)
                {
                    _portalFadeState = PortalFadeState.None;
                    _playerManager?.Input?.SyncState();
                }
            }

            return false;
        }

        private void FinalizeFrameInputState(KeyboardState newKeyboardState, MouseState newMouseState, GameTime gameTime)
        {
            _oldKeyboardState = newKeyboardState;
            _oldMouseState = newMouseState;

            _soundManager?.Update();
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
