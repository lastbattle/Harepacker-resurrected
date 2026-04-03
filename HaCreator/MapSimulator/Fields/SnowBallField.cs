using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Managers;
using MapleLib.Converters;
using MapleLib.PacketLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Aggregates minigame field runtimes behind a single simulator surface.
    /// This gives parity work a stable ownership seam before each minigame is
    /// expanded into client-like packet, timerboard, and result handling.
    /// </summary>
    #region SnowBall Field (CField_SnowBall)
    /// <summary>
    /// SnowBall Field Minigame - Team-based snowball pushing competition.
    ///
    /// Game Mechanics:
    /// - Two teams push large snowballs toward the opposing side
    /// - Each team has a SnowMan that acts as a blocker
    /// - Hitting the snowball moves it, hitting the snowman stuns it
    /// - Win condition: Push snowball past opposing team's goal line
    ///
    /// Packet Types:
    /// - 338: OnSnowBallState - Game state changes
    /// - 339: OnSnowBallHit - Damage to targets
    /// - 340: OnSnowBallMsg - Game messages
    /// - 341: OnSnowBallTouch - Player touching snowball area
    /// </summary>
    public class SnowBallField
    {
        public const int PacketTypeState = 338;
        public const int PacketTypeHit = 339;
        public const int PacketTypeMessage = 340;
        public const int PacketTypeTouch = 341;
        public const int OutboundTouchOpcode = 256;

        #region Constants (from client)
        // Static configuration from WZ (set during Init)
        private static int ms_nDeltaX = 20;  // Movement per hit
        private static Rectangle ms_rgBall;  // Valid ball range
        // Client data symbol CSnowBall::ms_anDelay at MapleStory.exe RVA 0x8568E4.
        private static readonly int[] ms_anDelay = { 150, 200, 250, 300, 350, 400, 450, 500, 0, -500 };
        private const int TouchRequestIntervalMs = 300;
        private const int TouchImpactMagnitude = 300;
        private const string TeamStory = "Story";
        private const string TeamMaple = "Maple";
        // CField_SnowBall::OnSnowBallMsg -> StringPool ids 0xD75-0xD77.
        private static readonly Dictionary<int, SnowBallMessageTemplate> s_messageTemplates = new()
        {
            { 1, new SnowBallMessageTemplate(0xD75, "{0}'s snowball has advanced to stage {1}.", SnowBallMessageArgumentPattern.TeamAndStage) },
            { 2, new SnowBallMessageTemplate(0xD75, "{0}'s snowball has advanced to stage {1}.", SnowBallMessageArgumentPattern.TeamAndStage) },
            { 3, new SnowBallMessageTemplate(0xD75, "{0}'s snowball has advanced to stage {1}.", SnowBallMessageArgumentPattern.TeamAndStage) },
            { 4, new SnowBallMessageTemplate(0xD76, "{0}'s snowman was broken by {1}.", SnowBallMessageArgumentPattern.DefendingAndAttackingTeam) },
            { 5, new SnowBallMessageTemplate(0xD77, "{0} has stunned the opposing team's snowman.", SnowBallMessageArgumentPattern.TeamOnly) }
        };
        #endregion
        #region Nested Types
        private enum SnowBallMessageArgumentPattern
        {
            TeamAndStage,
            DefendingAndAttackingTeam,
            TeamOnly
        }
        private readonly record struct SnowBallMessageTemplate(int StringPoolId, string FallbackFormat, SnowBallMessageArgumentPattern ArgumentPattern);
        public sealed class SnowBallFieldDefinition
        {
            public int DeltaX { get; init; }
            public int SnowManX { get; init; }
            public int BallStartX { get; init; }
            public int BallMinX { get; init; }
            public int BallMaxX { get; init; }
            public int DamageSnowBall { get; init; }
            public int DamageSnowMan0 { get; init; }
            public int DamageSnowMan1 { get; init; }
            public int SnowManHp { get; init; }
            public int SnowManWaitMs { get; init; }
            public int RecoveryAmount { get; init; }
            public int Speed { get; init; }
            public int Section1X { get; init; }
            public int Section2X { get; init; }
            public int Section3X { get; init; }
            public SnowBallTeamDefinition[] Teams { get; init; } = Array.Empty<SnowBallTeamDefinition>();
        }

        public sealed class SnowBallTeamDefinition
        {
            public int Team { get; init; }
            public int LaneY { get; init; }
            public string PortalName { get; init; }
            public string SnowBallPath { get; init; }
            public string SnowManPath { get; init; }
        }

        public static class SnowBallFieldDataLoader
        {
            private const string PropertyName = "snowBall";

            public static bool IsSnowBallMap(MapInfo mapInfo) =>
                mapInfo?.fieldType == MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_SNOWBALL
                || FindSnowBallProperty(mapInfo) != null;

            public static SnowBallFieldDefinition Load(MapInfo mapInfo)
            {
                WzImageProperty property = FindSnowBallProperty(mapInfo);
                if (property == null)
                {
                    return null;
                }

                SnowBallTeamDefinition[] teams =
                [
                    LoadTeam(property["0"], 0),
                    LoadTeam(property["1"], 1)
                ];

                if (teams.Any(static team => team == null))
                {
                    return null;
                }

                return new SnowBallFieldDefinition
                {
                    DeltaX = ReadInt(property["dx"], 20),
                    SnowManX = ReadInt(property["x"], 0),
                    BallStartX = ReadInt(property["x0"], 0),
                    BallMinX = ReadInt(property["xMin"], 0),
                    BallMaxX = ReadInt(property["xMax"], 0),
                    DamageSnowBall = ReadInt(property["damageSnowBall"], 10),
                    DamageSnowMan0 = ReadInt(property["damageSnowMan0"], 15),
                    DamageSnowMan1 = ReadInt(property["damageSnowMan1"], 45),
                    SnowManHp = ReadInt(property["snowManHP"], 7500),
                    SnowManWaitMs = ReadInt(property["snowManWait"], 10000),
                    RecoveryAmount = ReadInt(property["recoveryAmount"], 400),
                    Speed = ReadInt(property["speed"], 150),
                    Section1X = ReadInt(property["section1"], 0),
                    Section2X = ReadInt(property["section2"], 0),
                    Section3X = ReadInt(property["section3"], 0),
                    Teams = teams
                };
            }

            private static SnowBallTeamDefinition LoadTeam(WzImageProperty property, int team)
            {
                if (property == null)
                {
                    return null;
                }

                return new SnowBallTeamDefinition
                {
                    Team = team,
                    LaneY = ReadInt(property["y"], 0),
                    PortalName = ReadString(property["portal"]),
                    SnowBallPath = ReadString(property["snowBall"]),
                    SnowManPath = ReadString(property["snowMan"])
                };
            }

            private static WzImageProperty FindSnowBallProperty(MapInfo mapInfo)
            {
                if (mapInfo?.additionalNonInfoProps != null)
                {
                    WzImageProperty existing = mapInfo.additionalNonInfoProps
                        .FirstOrDefault(prop => string.Equals(prop.Name, PropertyName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        return existing;
                    }
                }

                return mapInfo?.Image?[PropertyName] as WzImageProperty;
            }

            private static int ReadInt(WzImageProperty property, int defaultValue)
            {
                if (property == null)
                {
                    return defaultValue;
                }

                if (property is WzStringProperty stringProperty
                    && int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
                {
                    return parsedValue;
                }

                return InfoTool.GetOptionalInt(property, defaultValue) ?? defaultValue;
            }

            private static string ReadString(WzImageProperty property)
            {
                return property == null ? string.Empty : InfoTool.GetString(property);
            }
        }
        public class SnowBall
        {
            public Rectangle Area;           // m_rcArea - Bounding box
            public string PortalName;        // m_sPortalName - Win portal target
            public float Rotation;           // Current rotation angle (degrees)
            public int Team;                 // 0 = left, 1 = right
            public bool IsWinner;            // Animation stopped
            public List<IDXObject> Frames;   // Animation frames
            public int FrameIndex;
            public int LastFrameTime;
            public int SpeedDegree;
            public int PositionDelta;
            public int MovementElapsed;
            public int AnchorX;
            public int AnchorY;
            // Movement properties
            public int PositionX => AnchorX;
            public int PositionY => AnchorY;
            public void Move(int dx)
            {
                int moveAmount = dx * ms_nDeltaX;
                AnchorX += moveAmount;
                Area.X += moveAmount;
                // Rotate the snowball (4 degrees per step)
                Rotation += 4f * dx;
                if (Rotation >= 360f) Rotation -= 360f;
                if (Rotation < 0f) Rotation += 360f;
            }
            public void Win()
            {
                IsWinner = true;
                // Stop animation
            }
            public void SetPos(int x, int y, bool flip)
            {
                AnchorX = x;
                AnchorY = y;
                Area = new Rectangle(x - 80, y - 161, 160, 165);
                PositionDelta = 0;
                MovementElapsed = 0;
            }
            public void Update(int tickCount)
            {
                UpdateMovement();
                if (IsWinner || Frames == null || Frames.Count == 0)
                    return;
                if (Frames.Count == 1)
                    return;
                int delay = Frames[FrameIndex].Delay > 0 ? Frames[FrameIndex].Delay : 100;
                if (tickCount - LastFrameTime > delay)
                {
                    FrameIndex = (FrameIndex + 1) % Frames.Count;
                    LastFrameTime = tickCount;
                }
            }
            public void ApplyStatePosition(int x, int y, int speedDegree, bool reset)
            {
                int dx = x - PositionX;
                SpeedDegree = speedDegree;
                if (reset)
                {
                    SetPos(x, y, false);
                    if (dx != 0)
                    {
                        Rotation += 4f * Math.Sign(dx);
                    }
                    return;
                }
                PositionDelta = dx * 3;
            }
            private void UpdateMovement()
            {
                if (PositionDelta == 0)
                {
                    MovementElapsed = 0;
                    return;
                }
                int direction = Math.Sign(PositionDelta);
                int stepDelay = GetStepDelay(SpeedDegree, direction);
                if (stepDelay <= 0)
                {
                    int nextX = PositionX + direction;
                    if (ms_rgBall.Left <= nextX && nextX <= ms_rgBall.Right)
                    {
                        MovementElapsed += 30;
                        if (MovementElapsed >= 30)
                        {
                            MovementElapsed -= 30;
                            Move(direction);
                            PositionDelta -= direction * 3;
                        }
                    }
                    else
                    {
                        MovementElapsed = 0;
                    }
                    return;
                }
                int nextPosition = PositionX + Math.Sign(stepDelay);
                if (ms_rgBall.Left > nextPosition || nextPosition > ms_rgBall.Right)
                {
                    MovementElapsed = 0;
                    return;
                }
                MovementElapsed += 30;
                int threshold = Math.Abs(stepDelay) * (3 - direction * Math.Sign(stepDelay)) / 3;
                if (MovementElapsed >= threshold)
                {
                    MovementElapsed -= threshold;
                    Move(Math.Sign(stepDelay));
                    PositionDelta -= direction;
                }
            }
            private static int GetStepDelay(int speedDegree, int direction)
            {
                int index = Math.Clamp(speedDegree, 0, ms_anDelay.Length - 1);
                return ms_anDelay[index];
            }
        }
        public class SnowMan
        {
            public Rectangle Area;           // m_rcArea - Bounding box
            public int Team;                 // 0 = left, 1 = right
            public int HP;                   // Current HP
            public int MaxHP;                // Maximum HP
            public bool IsStunned;           // Currently stunned from hit
            public int StunEndTime;          // When stun ends
            public List<IDXObject> Frames;   // Animation frames
            public List<IDXObject> HitFrames; // Hit reaction frames
            public int FrameIndex;
            public int LastFrameTime;
            public bool ShowHitEffect;
            public int HitEffectEndTime;
            public int StunDurationMs = 10000;
            public int AnchorX;
            public int AnchorY;
            public void Init(int x, int y, int hp)
            {
                AnchorX = x;
                AnchorY = y;
                Area = new Rectangle(x - 30, y - 25, 60, 25);
                MaxHP = hp;
                HP = hp;
            }
            public void ApplyHp(int hp)
            {
                HP = Math.Clamp(hp, 0, MaxHP);
            }
            public void Hit(int tickCount)
            {
                ShowHitEffect = true;
                HitEffectEndTime = tickCount + 500;
                IsStunned = true;
                StunEndTime = tickCount + Math.Max(0, StunDurationMs);
            }
            public void Update(int tickCount)
            {
                // Clear stun when time expires
                if (IsStunned && tickCount >= StunEndTime)
                    IsStunned = false;
                // Clear hit effect
                if (ShowHitEffect && tickCount >= HitEffectEndTime)
                    ShowHitEffect = false;
                // Update animation
                var frames = ShowHitEffect ? HitFrames : Frames;
                if (frames == null || frames.Count == 0)
                    return;
                if (frames.Count == 1)
                    return;
                int delay = frames[FrameIndex % frames.Count].Delay > 0
                    ? frames[FrameIndex % frames.Count].Delay : 100;
                if (tickCount - LastFrameTime > delay)
                {
                    FrameIndex = (FrameIndex + 1) % frames.Count;
                    LastFrameTime = tickCount;
                }
            }
            public float HPPercentage => MaxHP > 0 ? (float)HP / MaxHP : 0f;
        }
        public struct DamageInfo
        {
            public int Target;      // 0-1 = snowball, 2-3 = snowman
            public int Damage;      // Damage amount (negative = miss)
            public int StartTime;   // When to show effect
        }
        public readonly record struct TouchPacketRequest(int Team, int TickCount, int Sequence);
        public enum GameState
        {
            NotStarted = -1,
            Active = 1,
            Team0Win = 2,
            Team1Win = 3
        }
        #endregion
        #region Fields
        private SnowBall[] _snowBalls = new SnowBall[2];
        private SnowMan[] _snowMen = new SnowMan[2];
        private readonly List<DamageInfo> _damageQueue = new();
        private GameState _state = GameState.NotStarted;
        // Event config from WZ/client init
        private int _ballMinX;
        private int _ballMaxX;
        private int _ballStartX;
        private int _snowManX;
        private readonly int[] _laneY = new int[2];
        private SnowBallFieldDefinition _definition;
        private GraphicsDevice _graphicsDevice;
        // UI elements
        private bool _showScoreboard = true;
        private int _team0Score;
        private int _team1Score;
        private string _currentMessage;
        private int _messageEndTime;
        private int _damageSnowBall = 10;
        private readonly int[] _damageSnowMan = new[] { 15, 45 };
        private int _snowManWaitMs = 10000;
        private readonly Queue<string> _pendingChatMessages = new();
        private bool _hasReceivedStateSnapshot;
        private int _lastTouchImpactTime;
        private Vector2? _localPlayerPosition;
        private readonly Queue<TouchPacketRequest> _pendingTouchPacketRequests = new();
        private int _touchPacketSequence;
        private int _lastTouchRequestTime;
        private int _lastTouchRequestTeam = -1;
        private Action _applyLocalTouchImpact;
        private readonly Random _random = new();
        private int? _lastPacketType;
        #endregion
        #region Properties
        public GameState State => _state;
        public SnowBall[] SnowBalls => _snowBalls;
        public SnowMan[] SnowMen => _snowMen;
        public int Team0Score => _team0Score;
        public int Team1Score => _team1Score;
        public bool IsActive => _state == GameState.Active;
        public string CurrentMessage => _currentMessage;
        public TouchPacketRequest? PendingTouchPacketRequest => _pendingTouchPacketRequests.Count > 0 ? _pendingTouchPacketRequests.Peek() : null;
        internal int PendingTouchPacketRequestCount => _pendingTouchPacketRequests.Count;
        public int TouchPacketSequence => _touchPacketSequence;
        internal int DamageSnowBall => _damageSnowBall;
        internal int DamageSnowMan0 => _damageSnowMan[0];
        internal int DamageSnowMan1 => _damageSnowMan[1];
        #endregion
        #region Initialization
        /// <summary>
        /// Initialize SnowBall field from map configuration
        /// </summary>
        public void Initialize(int leftGoalX, int rightGoalX, int groundY,
            int snowBallRadius = 80, int deltaX = 20,
            int damageSnowBall = 10, int damageSnowMan0 = 15, int damageSnowMan1 = 45,
            int snowManHp = 7500, int snowManWaitMs = 10000)
        {
            _definition = null;
            _ballMinX = leftGoalX;
            _ballMaxX = rightGoalX;
            _ballStartX = (leftGoalX + rightGoalX) / 2;
            _snowManX = _ballStartX;
            _laneY[0] = groundY;
            _laneY[1] = groundY;
            ms_nDeltaX = deltaX;
            _damageSnowBall = damageSnowBall;
            _damageSnowMan[0] = damageSnowMan0;
            _damageSnowMan[1] = damageSnowMan1;
            _snowManWaitMs = snowManWaitMs;

            for (int i = 0; i < 2; i++)
            {
                _snowBalls[i] = new SnowBall
                {
                    Team = i,
                    Rotation = 0f,
                    IsWinner = false
                };
                _snowBalls[i].SetPos(_ballStartX, _laneY[i], i == 1);

                _snowMen[i] = new SnowMan { Team = i };
                _snowMen[i].Init(_snowManX, _laneY[i], snowManHp);
                _snowMen[i].StunDurationMs = snowManWaitMs;
            }

            ms_rgBall = BuildBallBounds();
            _state = GameState.NotStarted;
        }

        public void BindMap(Board board, GraphicsDevice graphicsDevice)
        {
            Reset();

            _graphicsDevice = graphicsDevice;
            SnowBallFieldDefinition definition = SnowBallFieldDataLoader.Load(board?.MapInfo);
            if (definition == null)
            {
                return;
            }

            ConfigureFromDefinition(definition);
        }

        private void ConfigureFromDefinition(SnowBallFieldDefinition definition)
        {
            _definition = definition;
            ms_nDeltaX = definition.DeltaX;
            _ballMinX = definition.BallMinX;
            _ballMaxX = definition.BallMaxX;
            _ballStartX = definition.BallStartX;
            _snowManX = definition.SnowManX;
            _damageSnowBall = definition.DamageSnowBall;
            _damageSnowMan[0] = definition.DamageSnowMan0;
            _damageSnowMan[1] = definition.DamageSnowMan1;
            _snowManWaitMs = definition.SnowManWaitMs;

            for (int i = 0; i < 2; i++)
            {
                SnowBallTeamDefinition teamDefinition = definition.Teams.ElementAtOrDefault(i) ?? new SnowBallTeamDefinition { Team = i };
                _laneY[i] = teamDefinition.LaneY;
                _snowBalls[i] = new SnowBall
                {
                    Team = i,
                    PortalName = teamDefinition.PortalName,
                    Rotation = 0f,
                    IsWinner = false,
                    Frames = LoadFramesFromMapPath(teamDefinition.SnowBallPath)
                };
                _snowBalls[i].SetPos(_ballStartX, _laneY[i], i == 1);

                List<IDXObject> snowManFrames = LoadFramesFromMapPath(teamDefinition.SnowManPath);
                _snowMen[i] = new SnowMan
                {
                    Team = i,
                    Frames = snowManFrames,
                    HitFrames = snowManFrames
                };
                _snowMen[i].Init(_snowManX, _laneY[i], definition.SnowManHp);
                _snowMen[i].StunDurationMs = definition.SnowManWaitMs;
            }

            ms_rgBall = BuildBallBounds();
            _state = GameState.NotStarted;
        }

        private Rectangle BuildBallBounds()
        {
            int minLaneY = _laneY.Min();
            int maxLaneY = _laneY.Max();
            int top = minLaneY - 161;
            int bottom = maxLaneY + 4;
            return new Rectangle(_ballMinX, top, Math.Max(0, _ballMaxX - _ballMinX), Math.Max(0, bottom - top));
        }

        private List<IDXObject> LoadFramesFromMapPath(string mapPath)
        {
            if (_graphicsDevice == null || string.IsNullOrWhiteSpace(mapPath))
            {
                return null;
            }

            WzImageProperty property = ResolveMapProperty(mapPath);
            if (property == null)
            {
                return null;
            }

            return LoadFrames(property);
        }

        private static WzImageProperty ResolveMapProperty(string mapPath)
        {
            string normalizedPath = mapPath.Replace('\\', '/');
            string[] segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return null;
            }

            if (!string.Equals(segments[0], "Map", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            WzImage image = global::HaCreator.Program.FindImage("Map", string.Join("/", segments.Skip(1).Take(2)));
            if (image == null)
            {
                return null;
            }

            if (!image.Parsed)
            {
                image.ParseImage();
            }

            WzObject current = image;
            for (int i = 3; i < segments.Length; i++)
            {
                current = current switch
                {
                    WzImage currentImage => currentImage[segments[i]],
                    WzImageProperty currentProperty => currentProperty[segments[i]],
                    _ => null
                };

                if (current == null)
                {
                    return null;
                }
            }

            return current as WzImageProperty;
        }

        private List<IDXObject> LoadFrames(WzImageProperty source)
        {
            List<IDXObject> frames = new();
            WzImageProperty resolved = WzInfoTools.GetRealProperty(source);
            if (resolved is WzSubProperty subProperty && subProperty.WzProperties.Count == 1)
            {
                resolved = WzInfoTools.GetRealProperty(subProperty.WzProperties[0]);
            }

            if (resolved is WzCanvasProperty canvas)
            {
                IDXObject frame = CreateFrame(canvas);
                if (frame != null)
                {
                    frames.Add(frame);
                }

                return frames;
            }

            if (resolved is not WzSubProperty container)
            {
                return frames;
            }

            for (int i = 0; ; i++)
            {
                WzImageProperty child = WzInfoTools.GetRealProperty(container[i.ToString()]);
                if (child == null)
                {
                    break;
                }

                if (child is WzCanvasProperty childCanvas)
                {
                    IDXObject frame = CreateFrame(childCanvas);
                    if (frame != null)
                    {
                        frames.Add(frame);
                    }
                }
            }

            return frames;
        }

        private IDXObject CreateFrame(WzCanvasProperty canvas)
        {
            System.Drawing.Bitmap bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return null;
            }

            Texture2D texture = bitmap.ToTexture2DAndDispose(_graphicsDevice);
            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            int delay = canvas[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt() ?? 0;
            return new DXObject(-(int)origin.X, -(int)origin.Y, texture, delay);
        }
        public void SetSnowBallFrames(int team, List<IDXObject> frames)
        {
            if (team >= 0 && team < 2 && _snowBalls[team] != null)
                _snowBalls[team].Frames = frames;
        }
        public void SetSnowManFrames(int team, List<IDXObject> frames, List<IDXObject> hitFrames = null)
        {
            if (team >= 0 && team < 2 && _snowMen[team] != null)
            {
                _snowMen[team].Frames = frames;
                _snowMen[team].HitFrames = hitFrames;
            }
        }
        public void SetLocalPlayerPosition(Vector2? localWorldPosition)
        {
            _localPlayerPosition = localWorldPosition;
        }

        public void SetLocalTouchImpactCallback(Action callback)
        {
            _applyLocalTouchImpact = callback;
        }

        public bool TryPeekTouchPacketRequest(out TouchPacketRequest request)
        {
            if (_pendingTouchPacketRequests.Count > 0)
            {
                request = _pendingTouchPacketRequests.Peek();
                return true;
            }

            request = default;
            return false;
        }

        public void ClearPendingTouchPacketRequests()
        {
            _pendingTouchPacketRequests.Clear();
        }

        public bool TryApplyPacket(int packetType, byte[] payload, int currentTickCount, out string errorMessage)
        {
            errorMessage = null;
            _lastPacketType = packetType;

            try
            {
                PacketReader reader = new(payload ?? Array.Empty<byte>());
                switch (packetType)
                {
                    case PacketTypeState:
                        int newState = reader.ReadByte();
                        int team0SnowManHp = reader.ReadInt();
                        int team1SnowManHp = reader.ReadInt();
                        int team0Pos = reader.ReadShort();
                        int team0SpeedDegree = reader.ReadByte();
                        int team1Pos = reader.ReadShort();
                        int team1SpeedDegree = reader.ReadByte();
                        int? damageSnowBall = null;
                        int? damageSnowMan0 = null;
                        int? damageSnowMan1 = null;
                        if (!_hasReceivedStateSnapshot)
                        {
                            damageSnowBall = reader.ReadShort();
                            damageSnowMan0 = reader.ReadShort();
                            damageSnowMan1 = reader.ReadShort();
                        }

                        OnSnowBallState(
                            newState,
                            team0SnowManHp,
                            team1SnowManHp,
                            team0Pos,
                            team0SpeedDegree,
                            team1Pos,
                            team1SpeedDegree,
                            damageSnowBall,
                            damageSnowMan0,
                            damageSnowMan1);
                        return true;

                    case PacketTypeHit:
                        OnSnowBallHit(reader.ReadByte(), reader.ReadShort(), reader.ReadShort(), currentTickCount);
                        return true;

                    case PacketTypeMessage:
                        OnSnowBallMsg(reader.ReadByte(), reader.ReadByte());
                        return true;

                    case PacketTypeTouch:
                        OnSnowBallTouch(currentTickCount);
                        return true;

                    default:
                        errorMessage = $"Unsupported SnowBall packet type: {packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public string DescribeStatus()
        {
            string lastPacket = _lastPacketType?.ToString(CultureInfo.InvariantCulture) ?? "None";
            return $"SnowBall runtime state={_state}, score={_team0Score}-{_team1Score}, hp={_snowMen[0]?.HP ?? 0}/{_snowMen[1]?.HP ?? 0}, pos={_snowBalls[0]?.PositionX ?? 0}/{_snowBalls[1]?.PositionX ?? 0}, pendingTouches={_pendingTouchPacketRequests.Count}, lastPacket={lastPacket}";
        }
        #endregion
        #region Packet Handling (matching client)
        /// <summary>
        /// OnSnowBallState - Packet type 338
        /// </summary>
        public void OnSnowBallState(int newState, int team0Pos, int team1Pos)
        {
            OnSnowBallState(newState, _snowMen[0]?.HP ?? 0, _snowMen[1]?.HP ?? 0, team0Pos, 0, team1Pos, 0);
        }
        public void OnSnowBallState(
            int newState,
            int team0SnowManHp,
            int team1SnowManHp,
            int team0Pos,
            int team0SpeedDegree,
            int team1Pos,
            int team1SpeedDegree)
        {
            OnSnowBallState(
                newState,
                team0SnowManHp,
                team1SnowManHp,
                team0Pos,
                team0SpeedDegree,
                team1Pos,
                team1SpeedDegree,
                null,
                null,
                null);
        }
        public void OnSnowBallState(
            int newState,
            int team0SnowManHp,
            int team1SnowManHp,
            int team0Pos,
            int team0SpeedDegree,
            int team1Pos,
            int team1SpeedDegree,
            int? damageSnowBall,
            int? damageSnowMan0,
            int? damageSnowMan1)
        {
            GameState previousState = _state;
            bool isFirstSnapshot = !_hasReceivedStateSnapshot;
            _hasReceivedStateSnapshot = true;
            _state = (GameState)newState;
            if (isFirstSnapshot)
            {
                if (damageSnowBall.HasValue)
                {
                    _damageSnowBall = damageSnowBall.Value;
                }
                if (damageSnowMan0.HasValue)
                {
                    _damageSnowMan[0] = damageSnowMan0.Value;
                }
                if (damageSnowMan1.HasValue)
                {
                    _damageSnowMan[1] = damageSnowMan1.Value;
                }
            }
            if (_snowMen[0] != null)
            {
                _snowMen[0].ApplyHp(team0SnowManHp);
            }
            if (_snowMen[1] != null)
            {
                _snowMen[1].ApplyHp(team1SnowManHp);
            }
            if (_snowBalls[0] != null)
            {
                _snowBalls[0].ApplyStatePosition(team0Pos, _laneY[0], team0SpeedDegree, isFirstSnapshot || _state != GameState.Active);
            }
            if (_snowBalls[1] != null)
            {
                _snowBalls[1].ApplyStatePosition(team1Pos, _laneY[1], team1SpeedDegree, isFirstSnapshot || _state != GameState.Active);
            }
            switch (_state)
            {
                case GameState.Active:
                    if (previousState == GameState.NotStarted)
                    {
                        ShowMessage("The snowball fight has begun!", 3000);
                    }
                    break;
                case GameState.Team0Win:
                    _snowBalls[0]?.Win();
                    if (previousState != GameState.Team0Win)
                    {
                        _team0Score++;
                        ShowMessage("Team Story wins the round!", 5000);
                    }
                    break;
                case GameState.Team1Win:
                    _snowBalls[1]?.Win();
                    if (previousState != GameState.Team1Win)
                    {
                        _team1Score++;
                        ShowMessage("Team Maple wins the round!", 5000);
                    }
                    break;
            }
            System.Diagnostics.Debug.WriteLine($"[SnowBallField] State changed: {previousState} -> {_state}");
        }
        /// <summary>
        /// OnSnowBallHit - Packet type 339
        /// Queue damage display for a target
        /// </summary>
        public void OnSnowBallHit(int target, int damage, int delay)
        {
            OnSnowBallHit(target, damage, delay, Environment.TickCount);
        }

        private void OnSnowBallHit(int target, int damage, int delay, int currentTickCount)
        {
            _damageQueue.Add(new DamageInfo
            {
                Target = target,
                Damage = damage,
                StartTime = currentTickCount + delay
            });
        }
        /// <summary>
        /// OnSnowBallMsg - Packet type 340
        /// </summary>
        public void OnSnowBallMsg(int msgType, string message)
        {
            QueueChatMessage(FormatSnowBallMessage(null, msgType, message));
        }
        public void OnSnowBallMsg(int team, int msgType)
        {
            QueueChatMessage(FormatSnowBallMessage(team, msgType, null));
        }
        /// <summary>
        /// OnSnowBallTouch - Packet type 341
        /// Player entered/exited snowball zone
        /// </summary>
        public void OnSnowBallTouch()
        {
            OnSnowBallTouch(Environment.TickCount);
        }

        private void OnSnowBallTouch(int currentTickCount)
        {
            if (_pendingTouchPacketRequests.Count > 0)
            {
                _pendingTouchPacketRequests.Dequeue();
            }
            _lastTouchImpactTime = currentTickCount;
            _applyLocalTouchImpact?.Invoke();
        }
        public bool TryConsumeTouchPacketRequest(out TouchPacketRequest request)
        {
            if (_pendingTouchPacketRequests.Count > 0)
            {
                request = _pendingTouchPacketRequests.Dequeue();
                return true;
            }
            request = default;
            return false;
        }
        public bool TryConsumeChatMessage(out string message)
        {
            if (_pendingChatMessages.Count > 0)
            {
                message = _pendingChatMessages.Dequeue();
                return true;
            }
            message = null;
            return false;
        }
        #endregion
        #region Simulation (for testing)
        /// <summary>
        /// Simulate attack on a target (for testing without server)
        /// </summary>
        public void SimulateAttack(int team, int target, int damage)
        {
            if (_state != GameState.Active)
                return;
            int tickCount = Environment.TickCount;
            switch (target)
            {
                case 0: // Left snowball
                    if (_snowBalls[0] != null && team == 0)
                    {
                        _snowBalls[0].Move(1);
                        QueueDamage(target, damage > 0 ? damage : _damageSnowBall, tickCount);
                        CheckWinCondition();
                    }
                    break;
                case 1: // Right snowball
                    if (_snowBalls[target] != null)
                    {
                        int direction = team == 1 ? -1 : 0;
                        if (direction != 0)
                        {
                            _snowBalls[target].Move(direction);
                            QueueDamage(target, damage > 0 ? damage : _damageSnowBall, tickCount);
                            CheckWinCondition();
                        }
                    }
                    break;
                case 2: // Left snowman
                case 3: // Right snowman
                    int snowManIndex = target - 2;
                    if (_snowMen[snowManIndex] != null)
                    {
                        int resolvedDamage = damage > 0
                            ? damage
                            : RollSnowManHitDamage(_random.Next(100), _damageSnowMan[0], _damageSnowMan[1]);
                        QueueDamage(target, resolvedDamage, tickCount);
                    }
                    break;
            }
        }
        /// <summary>
        /// Start the game (for testing)
        /// </summary>
        public void StartGame()
        {
            _state = GameState.Active;
            ShowMessage("Snowball fight begins!", 3000);
        }
        private void CheckWinCondition()
        {
            if (_snowBalls[0] != null && _snowBalls[0].PositionX >= _ballMaxX)
            {
                OnSnowBallState(
                    (int)GameState.Team0Win,
                    _snowMen[0]?.HP ?? 0,
                    _snowMen[1]?.HP ?? 0,
                    _snowBalls[0].PositionX,
                    0,
                    _snowBalls[1]?.PositionX ?? 0,
                    0);
            }
            else if (_snowBalls[1] != null && _snowBalls[1].PositionX <= _ballMinX)
            {
                OnSnowBallState(
                    (int)GameState.Team1Win,
                    _snowMen[0]?.HP ?? 0,
                    _snowMen[1]?.HP ?? 0,
                    _snowBalls[0]?.PositionX ?? 0,
                    0,
                    _snowBalls[1].PositionX,
                    0);
            }
        }
        #endregion
        #region Update
        public void Update(int tickCount)
        {
            UpdateLocalTouchLoop(tickCount);
            foreach (var ball in _snowBalls)
            {
                ball?.Update(tickCount);
            }
            // Update snowmen
            foreach (var snowMan in _snowMen)
                snowMan?.Update(tickCount);
            // Process damage queue
            ProcessDamageQueue(tickCount);
            // Clear expired message
            if (_currentMessage != null && tickCount >= _messageEndTime)
                _currentMessage = null;
        }
        private void ProcessDamageQueue(int tickCount)
        {
            for (int i = _damageQueue.Count - 1; i >= 0; i--)
            {
                var damage = _damageQueue[i];
                if (tickCount >= damage.StartTime)
                {
                    // Apply damage effect
                    ApplyDamageEffect(damage);
                    _damageQueue.RemoveAt(i);
                }
            }
        }
        private void ApplyDamageEffect(DamageInfo damage)
        {
            // This would trigger visual effects
            // In the client, this calls Effect_HP or Effect_Miss
            switch (damage.Target)
            {
                case 0:
                case 1:
                    // Snowball hit effect
                    System.Diagnostics.Debug.WriteLine($"[SnowBallField] Ball {damage.Target} hit for {damage.Damage}");
                    break;
                case 2:
                case 3:
                    _snowMen[damage.Target - 2]?.Hit(Environment.TickCount);
                    System.Diagnostics.Debug.WriteLine($"[SnowBallField] SnowMan {damage.Target - 2} hit for {damage.Damage}");
                    break;
            }
        }
        private void QueueDamage(int target, int damage, int startTime)
        {
            _damageQueue.Add(new DamageInfo
            {
                Target = target,
                Damage = damage,
                StartTime = startTime
            });
        }
        private void ShowMessage(string message, int durationMs)
        {
            _currentMessage = message;
            _messageEndTime = Environment.TickCount + durationMs;
        }
        private void QueueChatMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _pendingChatMessages.Enqueue(message);
            }
        }
        internal static string FormatSnowBallMessage(int? team, int msgType, string fallbackMessage)
        {
            if (!string.IsNullOrWhiteSpace(fallbackMessage))
            {
                return fallbackMessage;
            }
            string teamName = GetSnowBallTeamName(team);
            string opposingTeamName = GetSnowBallTeamName(team.HasValue ? 1 - team.Value : null);
            if (!s_messageTemplates.TryGetValue(msgType, out SnowBallMessageTemplate template))
            {
                return string.Empty;
            }
            object[] args = template.ArgumentPattern switch
            {
                SnowBallMessageArgumentPattern.TeamAndStage => new object[] { teamName, msgType },
                SnowBallMessageArgumentPattern.DefendingAndAttackingTeam => new object[] { opposingTeamName, teamName },
                SnowBallMessageArgumentPattern.TeamOnly => new object[] { teamName },
                _ => Array.Empty<object>()
            };
            return string.Format(CultureInfo.InvariantCulture, template.FallbackFormat, args);
        }
        private static string GetSnowBallTeamName(int? team)
        {
            return team switch
            {
                0 => TeamStory,
                1 => TeamMaple,
                _ => "Unknown"
            };
        }
        private void UpdateLocalTouchLoop(int tickCount)
        {
            if (_state != GameState.Active || !_localPlayerPosition.HasValue)
            {
                return;
            }
            int touchedTeam = GetTouchedSnowBallTeam(_localPlayerPosition.Value);
            if (touchedTeam < 0 || (int)_state == touchedTeam + 2)
            {
                _lastTouchRequestTeam = -1;
                return;
            }

            if (_lastTouchRequestTeam == touchedTeam
                && tickCount - _lastTouchRequestTime < TouchRequestIntervalMs)
            {
                return;
            }

            if (_lastTouchImpactTime > 0
                && tickCount - _lastTouchImpactTime < TouchRequestIntervalMs)
            {
                return;
            }

            if (_pendingTouchPacketRequests.Any(request => request.Team == touchedTeam))
            {
                return;
            }

            _touchPacketSequence++;
            _pendingTouchPacketRequests.Enqueue(new TouchPacketRequest(touchedTeam, tickCount, _touchPacketSequence));
            _lastTouchRequestTime = tickCount;
            _lastTouchRequestTeam = touchedTeam;
        }
        private int GetTouchedSnowBallTeam(Vector2 worldPosition)
        {
            Point point = new((int)MathF.Round(worldPosition.X), (int)MathF.Round(worldPosition.Y));
            for (int i = 0; i < _snowBalls.Length; i++)
            {
                if (_snowBalls[i]?.Area.Contains(point) == true)
                {
                    return i;
                }
            }
            return -1;
        }
        #endregion
        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime, int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font = null)
        {
            int shiftCenterX = mapShiftX - centerX;
            int shiftCenterY = mapShiftY - centerY;
            // Draw snowballs
            foreach (var ball in _snowBalls)
            {
                if (ball == null) continue;
                int screenX = ball.AnchorX - shiftCenterX;
                int screenY = ball.AnchorY - shiftCenterY;
                if (ball.Frames != null && ball.Frames.Count > 0)
                {
                    var frame = ball.Frames[ball.FrameIndex % ball.Frames.Count];
                    Color tint = ball.IsWinner ? Color.Gold : Color.White;
                    // Draw with rotation
                    // Note: Proper rotation would need custom draw call
                    frame.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime,
                        screenX + frame.X, screenY + frame.Y, tint, ball.Team == 1, null);
                }
                else if (pixelTexture != null)
                {
                    // Debug draw
                    Color ballColor = ball.Team == 0
                        ? new Color(100, 150, 255, 180)
                        : new Color(255, 100, 100, 180);
                    if (ball.IsWinner) ballColor = Color.Gold * 0.8f;
                    spriteBatch.Draw(pixelTexture,
                        new Rectangle(screenX, screenY, ball.Area.Width, ball.Area.Height),
                        ballColor);
                }
            }
            // Draw snowmen
            foreach (var snowMan in _snowMen)
            {
                if (snowMan == null) continue;
                int screenX = snowMan.AnchorX - shiftCenterX;
                int screenY = snowMan.AnchorY - shiftCenterY;
                var frames = snowMan.ShowHitEffect ? snowMan.HitFrames : snowMan.Frames;
                if (frames != null && frames.Count > 0)
                {
                    var frame = frames[snowMan.FrameIndex % frames.Count];
                    Color tint = snowMan.IsStunned ? new Color(255, 150, 150, 200) : Color.White;
                    frame.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime,
                        screenX + frame.X, screenY + frame.Y, tint, snowMan.Team == 1, null);
                }
                else if (pixelTexture != null)
                {
                    // Debug draw
                    Color manColor = snowMan.Team == 0
                        ? new Color(150, 200, 255, 200)
                        : new Color(255, 150, 150, 200);
                    if (snowMan.IsStunned) manColor = Color.Gray * 0.8f;
                    spriteBatch.Draw(pixelTexture,
                        new Rectangle(screenX, screenY, snowMan.Area.Width, snowMan.Area.Height),
                        manColor);
                    // Draw HP bar
                    DrawHPBar(spriteBatch, pixelTexture, screenX, screenY - 15,
                        snowMan.Area.Width, 8, snowMan.HPPercentage, snowMan.Team);
                }
            }
            // Draw scoreboard
            if (_showScoreboard && font != null)
            {
                DrawScoreboard(spriteBatch, pixelTexture, font);
            }
            // Draw current message
            if (_currentMessage != null && font != null)
            {
                Vector2 textSize = font.MeasureString(_currentMessage);
                Vector2 position = new Vector2(
                    (spriteBatch.GraphicsDevice.Viewport.Width - textSize.X) / 2,
                    100
                );
                spriteBatch.DrawString(font, _currentMessage, position + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, _currentMessage, position, Color.Yellow);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawHPBar(SpriteBatch spriteBatch, Texture2D pixel, int x, int y,
            int width, int height, float percentage, int team)
        {
            // Background
            spriteBatch.Draw(pixel, new Rectangle(x - 1, y - 1, width + 2, height + 2), Color.Black);
            // HP bar
            Color hpColor = team == 0 ? Color.CornflowerBlue : Color.Coral;
            int hpWidth = (int)(width * percentage);
            spriteBatch.Draw(pixel, new Rectangle(x, y, hpWidth, height), hpColor);
        }
        private void DrawScoreboard(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font)
        {
            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            // Scoreboard background
            Rectangle bgRect = new Rectangle(screenWidth / 2 - 100, 10, 200, 50);
            spriteBatch.Draw(pixel, bgRect, new Color(0, 0, 0, 150));
            // Team scores
            string scoreText = $"Story {_team0Score} : {_team1Score} Maple";
            Vector2 scoreSize = font.MeasureString(scoreText);
            spriteBatch.DrawString(font, scoreText,
                new Vector2((screenWidth - scoreSize.X) / 2, 25), Color.White);
            // Game state
            string stateText = _state switch
            {
                GameState.NotStarted => "Waiting...",
                GameState.Active => "FIGHT!",
                GameState.Team0Win => "STORY WINS!",
                GameState.Team1Win => "MAPLE WINS!",
                _ => ""
            };
            if (!string.IsNullOrEmpty(stateText))
            {
                Vector2 stateSize = font.MeasureString(stateText);
                spriteBatch.DrawString(font, stateText,
                    new Vector2((screenWidth - stateSize.X) / 2, 45), Color.Yellow);
            }
        }
        #endregion
        #region Utility
        public void Reset()
        {
            _state = GameState.NotStarted;
            _damageQueue.Clear();
            _pendingChatMessages.Clear();
            _currentMessage = null;
            _hasReceivedStateSnapshot = false;
            _lastTouchImpactTime = 0;
            _localPlayerPosition = null;
            _pendingTouchPacketRequests.Clear();
            _touchPacketSequence = 0;
            _lastTouchRequestTime = 0;
            _lastTouchRequestTeam = -1;
            _lastPacketType = null;
            for (int i = 0; i < 2; i++)
            {
                if (_snowBalls[i] != null)
                {
                    _snowBalls[i].SetPos(_ballStartX, _laneY[i], i == 1);
                    _snowBalls[i].Rotation = 0f;
                    _snowBalls[i].IsWinner = false;
                    _snowBalls[i].SpeedDegree = 0;
                }

                if (_snowMen[i] != null)
                {
                    _snowMen[i].HP = _snowMen[i].MaxHP;
                    _snowMen[i].AnchorX = _snowManX;
                    _snowMen[i].AnchorY = _laneY[i];
                    _snowMen[i].Area = new Rectangle(_snowManX - 30, _laneY[i] - 25, 60, 25);
                    _snowMen[i].IsStunned = false;
                    _snowMen[i].ShowHitEffect = false;
                    _snowMen[i].StunDurationMs = _snowManWaitMs;
                }
            }
        }
        public bool ContainsPoint(int x, int y)
        {
            return ms_rgBall.Contains(x, y);
        }

        internal static int RollSnowManHitDamage(int roll, int damageSnowMan0, int damageSnowMan1)
        {
            int normalizedRoll = Math.Clamp(roll, 0, 99);
            if (normalizedRoll < 20)
            {
                return 0;
            }

            return normalizedRoll < 90 ? damageSnowMan0 : damageSnowMan1;
        }
        #endregion
    }
    #endregion
}
