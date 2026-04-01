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
    #region Coconut Field (CField_Coconut)
    /// <summary>
    /// Coconut Field Minigame - Team-based coconut harvesting competition.
    ///
    /// Game Mechanics:
    /// - Two teams compete to knock down coconuts from trees
    /// - Players attack coconut objects to change their state
    /// - Coconuts can be: on tree, falling, team-colored, or scored
    /// - Win condition: Team with most coconuts when time expires
    ///
    /// Packet Types:
    /// - 342: OnCoconutHit - Coconut hit/state change
    /// - 343: OnCoconutScore - Score update
    /// </summary>
    public class CoconutField
    {
        public const int PacketTypeHit = 342;
        public const int PacketTypeScore = 343;
        private const int LocalNormalAttackDelayMs = 120;
        private const int ResultBannerTopY = 145;
        private const int BoardWidth = 258;
        private const int BoardHeight = 101;
        private const int BoardTopY = 10;
        private const int Team0ScoreX = 25;
        private const int Team1ScoreX = 150;
        private const int ScoreY = 37;
        private const int TimerX = 60;
        private const int TimerY = 83;
        private const int ScoreDigitSpacing = 4;
        private const int TimerDigitSpacing = 1;
        #region Nested Types
        public enum CoconutState
        {
            OnTree = 0,
            Falling = 1,
            Team0Claimed = 2,
            Team1Claimed = 3,
            Scored = 4,
            Destroyed = 5
        }
        public enum RoundResult
        {
            None,
            Victory,
            Lose,
            Draw
        }
        public class Coconut
        {
            public int Id;
            public CoconutState State;
            public Vector2 InitialPosition;
            public Vector2 Position;
            public Vector2 Velocity;
            public int Team;               // -1 = neutral, 0 = Maple, 1 = Story
            public int HitCount;           // Hits taken
            public int LastHitTime;
            public bool IsActive;
            // Visual
            public List<IDXObject> Frames;
            public int FrameIndex;
            public int LastFrameTime;
            public float Rotation;
            public float Scale = 1f;
            public void Update(int tickCount, float gravity, int groundY)
            {
                if (!IsActive)
                    return;
                if (State == CoconutState.Falling)
                {
                    bool onGround = Position.Y >= groundY && Velocity.Y >= 0f;
                    if (!onGround)
                    {
                        Velocity.Y += gravity;
                        Position += Velocity;
                        Rotation += Velocity.X * 2f;
                        if (Position.Y >= groundY)
                        {
                            Position.Y = groundY;
                            Velocity = new Vector2(Velocity.X, 0f);
                        }
                    }
                    else
                    {
                        Position.Y = groundY;
                        Velocity = new Vector2(Velocity.X, 0f);
                    }
                }
                // Animate
                if (Frames != null && Frames.Count > 1)
                {
                    int delay = Frames[FrameIndex].Delay > 0 ? Frames[FrameIndex].Delay : 100;
                    if (tickCount - LastFrameTime > delay)
                    {
                        FrameIndex = (FrameIndex + 1) % Frames.Count;
                        LastFrameTime = tickCount;
                    }
                }
            }
            public void Hit(int byTeam)
            {
                HitCount++;
                LastHitTime = Environment.TickCount;
                // Simple state progression
                if (State == CoconutState.OnTree)
                {
                    State = CoconutState.Falling;
                    Team = byTeam;
                    Velocity = new Vector2(
                        byTeam == 0 ? 2f : -2f, // Slight horizontal push
                        -3f // Initial upward velocity
                    );
                }
                else if (State == CoconutState.Falling)
                {
                    // Change team ownership
                    Team = byTeam;
                    Velocity.X = byTeam == 0 ? 3f : -3f;
                }
            }
        }
        public struct HitInfo
        {
            public int Target;      // Coconut ID or -1 for all
            public int NewState;    // New state after hit
            public int StartTime;   // When to apply
        }
        public readonly record struct AttackPacketRequest(int TargetId, int DelayMs, int RequestedAtTick, bool TransportDispatched = false);
        #endregion
        #region Fields
        private readonly List<Coconut> _coconuts = new();
        private readonly List<HitInfo> _hitQueue = new();
        private int _team0Score;
        private int _team1Score;
        private int _timeRemaining;
        private int _finishTick;
        private int _lastUpdateTime;
        private bool _gameActive;
        private int _localTeam;
        // Configuration
        private int _totalCoconuts;
        private int _groundY;
        private float _gravity = 0.3f;
        private Rectangle _treeArea;
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private readonly List<IDXObject> _victoryFrames = new();
        private readonly List<IDXObject> _loseFrames = new();
        private readonly Dictionary<char, IDXObject> _scoreFont = new();
        private readonly Dictionary<char, IDXObject> _timeFont = new();
        private readonly Dictionary<string, IDXObject> _objectFrameCache = new(StringComparer.OrdinalIgnoreCase);
        private IDXObject _boardBackground;
        private List<IDXObject> _activeResultFrames;
        private int _resultFrameIndex;
        private int _resultFrameStartTime;
        private int _resultExpireTime;
        private RoundResult _lastRoundResult;
        private bool _runtimeActive;
        private bool _awaitingFinalScore;
        private readonly List<AttackPacketRequest> _pendingAttackPacketRequests = new();
        private int? _lastPacketType;
        private int? _lastScorePacketTick;
        // Avatar appearances (from WZ)
        private int[,] _avatarEquip; // [team][gender] = equipment set
        // UI
        private string _currentMessage;
        private int _messageEndTime;
        #endregion
        #region Properties
        public int Team0Score => _team0Score;
        public int Team1Score => _team1Score;
        public int TimeRemaining => _timeRemaining;
        public bool IsActive => _runtimeActive;
        public bool IsRoundActive => _gameActive;
        public bool AwaitingFinalScore => _awaitingFinalScore;
        public int PendingAttackPacketRequestCount => _pendingAttackPacketRequests.Count;
        public int PendingUndispatchedAttackPacketRequestCount => _pendingAttackPacketRequests.Count(static request => !request.TransportDispatched);
        public IReadOnlyList<Coconut> Coconuts => _coconuts;
        public RoundResult LastRoundResult => _lastRoundResult;
        public int? LastPacketType => _lastPacketType;
        public int? LastScorePacketTick => _lastScorePacketTick;
        internal string CurrentMessage => _currentMessage;
        internal int MessageExpiresAtTick => _messageEndTime;
        internal int ResultExpiresAtTick => _resultExpireTime;
        #endregion
        #region Initialization
        public void Initialize(GraphicsDevice graphicsDevice, SoundManager soundManager = null)
        {
            _graphicsDevice = graphicsDevice;
        }
        public void BindMap(Board board)
        {
            Reset();
            if (board?.MapInfo?.fieldType != MapleLib.WzLib.WzStructure.Data.FieldType.FIELDTYPE_COCONUT)
            {
                return;
            }
            _runtimeActive = true;
            _treeArea = ResolveFallbackTreeArea(board);
            _groundY = ResolveGroundY(board, Array.Empty<ObjectInstance>());
            List<ObjectInstance> coconutObjects = board.BoardItems.TileObjs
                .OfType<ObjectInstance>()
                .Where(IsCoconutObject)
                .OrderBy(instance => instance.X)
                .ThenBy(instance => instance.Y)
                .ToList();
            if (coconutObjects.Count == 0)
            {
                return;
            }
            Rectangle objectBounds = GetObjectBounds(coconutObjects);
            _treeArea = ExpandBounds(objectBounds, 24, 48);
            _groundY = ResolveGroundY(board, coconutObjects);
            _totalCoconuts = coconutObjects.Count;
            _coconuts.Clear();
            for (int i = 0; i < coconutObjects.Count; i++)
            {
                ObjectInstance instance = coconutObjects[i];
                Vector2 position = new Vector2(instance.X, instance.Y);
                _coconuts.Add(new Coconut
                {
                    Id = i,
                    State = CoconutState.OnTree,
                    InitialPosition = position,
                    Position = position,
                    Velocity = Vector2.Zero,
                    Team = -1,
                    IsActive = true,
                    Frames = CreateFramesForObject(instance)
                });
            }
        }
        public void Initialize(int coconutCount, Rectangle treeArea, int groundY)
        {
            _runtimeActive = true;
            _totalCoconuts = coconutCount;
            _treeArea = treeArea;
            _groundY = groundY;
            // Create coconuts on tree
            _coconuts.Clear();
            Random rand = new Random();
            for (int i = 0; i < coconutCount; i++)
            {
                _coconuts.Add(new Coconut
                {
                    Id = i,
                    State = CoconutState.OnTree,
                    InitialPosition = new Vector2(
                        treeArea.Left + rand.Next(treeArea.Width),
                        treeArea.Top + rand.Next(treeArea.Height / 2)
                    ),
                    Position = Vector2.Zero,
                    Velocity = Vector2.Zero,
                    Team = -1,
                    IsActive = true
                });
                _coconuts[i].Position = _coconuts[i].InitialPosition;
            }
            _team0Score = 0;
            _team1Score = 0;
            _gameActive = false;
            _lastUpdateTime = Environment.TickCount;
            _localTeam = 0;
            _awaitingFinalScore = false;
            _lastPacketType = null;
            _lastScorePacketTick = null;
            ClearRoundResult();
            System.Diagnostics.Debug.WriteLine($"[CoconutField] Initialized with {coconutCount} coconuts");
        }
        public void SetCoconutFrames(List<IDXObject> frames)
        {
            foreach (var coconut in _coconuts)
                coconut.Frames = frames;
        }
        #endregion
        #region Packet Handling (matching client)
        /// <summary>
        /// OnCoconutHit - Packet type 342
        /// </summary>
        public void OnCoconutHit(int targetId, int delay, int newState, int? currentTimeMs = null)
        {
            _runtimeActive = true;
            _lastPacketType = PacketTypeHit;
            AcknowledgeAttackPacketRequest(targetId);
            int startTime = (currentTimeMs ?? Environment.TickCount) + delay;
            if (targetId < 0)
            {
                // Hit all coconuts
                for (int i = 0; i < _coconuts.Count; i++)
                {
                    _hitQueue.Add(new HitInfo
                    {
                        Target = i,
                        NewState = newState,
                        StartTime = startTime
                    });
                }
            }
            else
            {
                _hitQueue.Add(new HitInfo
                {
                    Target = targetId,
                    NewState = newState,
                    StartTime = startTime
                });
            }
        }
        /// <summary>
        /// OnCoconutScore - Packet type 343
        /// </summary>
        public void OnCoconutScore(int team0, int team1, int? currentTimeMs = null)
        {
            _runtimeActive = true;
            _lastPacketType = PacketTypeScore;
            _team0Score = team0;
            _team1Score = team1;
            _lastScorePacketTick = currentTimeMs ?? Environment.TickCount;
            if (_awaitingFinalScore || (!_gameActive && _timeRemaining <= 0))
            {
                ResolveRoundResult(_lastScorePacketTick.Value);
            }
            System.Diagnostics.Debug.WriteLine($"[CoconutField] Score: Maple {team0} - Story {team1}");
        }
        /// <summary>
        /// OnClock - Time update
        /// </summary>
        public void OnClock(int timeSeconds, int? currentTimeMs = null)
        {
            int now = currentTimeMs ?? Environment.TickCount;
            int durationMs = Math.Max(0, timeSeconds) * 1000;
            _runtimeActive = true;
            _finishTick = durationMs > 0 ? now + durationMs : 1;
            _timeRemaining = Math.Max(0, timeSeconds);
            if (timeSeconds > 0)
            {
                _gameActive = true;
                _awaitingFinalScore = false;
                _lastUpdateTime = now;
                _lastScorePacketTick = null;
                _pendingAttackPacketRequests.Clear();
                ClearRoundResult();
            }
            else if (_gameActive || _awaitingFinalScore)
            {
                BeginFinalScoreWait(now);
            }
        }
        public bool TryApplyPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            _lastPacketType = packetType;
            if (!_runtimeActive)
            {
                errorMessage = "Coconut runtime inactive.";
                return false;
            }
            try
            {
                var reader = new PacketReader(payload ?? Array.Empty<byte>());
                switch (packetType)
                {
                    case PacketTypeHit:
                        OnCoconutHit(reader.ReadShort(), reader.ReadShort(), reader.ReadByte(), currentTimeMs);
                        return true;
                    case PacketTypeScore:
                        OnCoconutScore(reader.ReadShort(), reader.ReadShort(), currentTimeMs);
                        return true;
                    default:
                        errorMessage = $"Unsupported Coconut packet type: {packetType}";
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
            if (!_runtimeActive)
            {
                return "Coconut runtime inactive";
            }
            string roundState = _gameActive
                ? "active"
                : _awaitingFinalScore
                    ? "awaiting-score-packet"
                    : "idle";
            int pendingRequests = _pendingAttackPacketRequests.Count;
            int unsentRequests = PendingUndispatchedAttackPacketRequestCount;
            return $"Coconut runtime active, coconuts={_coconuts.Count}, round={roundState}, score={_team0Score}-{_team1Score}, time={_timeRemaining}, result={_lastRoundResult}, pendingRequests={pendingRequests}, unsentRequests={unsentRequests}, lastPacket={(_lastPacketType?.ToString() ?? "None")}, lastScoreTick={(_lastScorePacketTick?.ToString() ?? "None")}";
        }
        #endregion
        #region Simulation (for testing)
        public void StartGame(int durationSeconds = 120)
        {
            _gameActive = true;
            _runtimeActive = true;
            _timeRemaining = durationSeconds;
            _finishTick = Environment.TickCount + Math.Max(0, durationSeconds) * 1000;
            _lastUpdateTime = Environment.TickCount;
            _awaitingFinalScore = false;
            _lastScorePacketTick = null;
            _pendingAttackPacketRequests.Clear();
            ClearRoundResult();
            ShowMessage("Coconut harvest begins!", 3000, _lastUpdateTime);
        }
        public void SimulateHit(int coconutId, int byTeam)
        {
            if (!_gameActive || coconutId < 0 || coconutId >= _coconuts.Count)
                return;
            var coconut = _coconuts[coconutId];
            if (!coconut.IsActive)
                return;
            coconut.Hit(byTeam);
        }
        public bool TryHandleNormalAttack(Rectangle attackBounds, int currentTick, int skillId = 0, bool allowLocalPreview = true)
        {
            if (!_gameActive || skillId != 0 || attackBounds.Width <= 0 || attackBounds.Height <= 0)
            {
                return false;
            }
            Coconut target = null;
            float bestDistance = float.MaxValue;
            Vector2 attackCenter = new Vector2(
                attackBounds.Left + attackBounds.Width * 0.5f,
                attackBounds.Top + attackBounds.Height * 0.5f);
            for (int i = 0; i < _coconuts.Count; i++)
            {
                Coconut coconut = _coconuts[i];
                if (!IsAttackable(coconut))
                {
                    continue;
                }
                Rectangle targetBounds = GetObjectBounds(coconut);
                if (!attackBounds.Intersects(targetBounds))
                {
                    continue;
                }
                Vector2 coconutCenter = new Vector2(targetBounds.Center.X, targetBounds.Center.Y);
                float distance = Vector2.DistanceSquared(attackCenter, coconutCenter);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    target = coconut;
                }
            }
            if (target == null)
            {
                return false;
            }
            QueueAttackPacketRequest(target.Id, LocalNormalAttackDelayMs, currentTick);
            if (allowLocalPreview)
            {
                QueueHit(target.Id, ResolveLocalAttackState(target, _localTeam), currentTick + LocalNormalAttackDelayMs);
            }
            return true;
        }
        public bool TryConsumeAttackPacketRequest(out AttackPacketRequest request)
        {
            if (_pendingAttackPacketRequests.Count > 0)
            {
                request = _pendingAttackPacketRequests[0];
                _pendingAttackPacketRequests.RemoveAt(0);
                return true;
            }
            request = default;
            return false;
        }
        public bool TryPeekAttackPacketRequest(out AttackPacketRequest request)
        {
            if (_pendingAttackPacketRequests.Count > 0)
            {
                request = _pendingAttackPacketRequests[0];
                return true;
            }
            request = default;
            return false;
        }
        public bool TryGetNextUndispatchedAttackPacketRequest(out AttackPacketRequest request)
        {
            for (int i = 0; i < _pendingAttackPacketRequests.Count; i++)
            {
                AttackPacketRequest pendingRequest = _pendingAttackPacketRequests[i];
                if (!pendingRequest.TransportDispatched)
                {
                    request = pendingRequest;
                    return true;
                }
            }
            request = default;
            return false;
        }
        public bool MarkAttackPacketRequestTransportDispatched(AttackPacketRequest request)
        {
            for (int i = 0; i < _pendingAttackPacketRequests.Count; i++)
            {
                AttackPacketRequest pendingRequest = _pendingAttackPacketRequests[i];
                if (pendingRequest.TargetId == request.TargetId
                    && pendingRequest.DelayMs == request.DelayMs
                    && pendingRequest.RequestedAtTick == request.RequestedAtTick)
                {
                    if (!pendingRequest.TransportDispatched)
                    {
                        _pendingAttackPacketRequests[i] = pendingRequest with { TransportDispatched = true };
                    }
                    return true;
                }
            }
            return false;
        }
        public void ClearPendingAttackPacketRequests()
        {
            _pendingAttackPacketRequests.Clear();
        }
        private void BeginFinalScoreWait(int currentTick)
        {
            _gameActive = false;
            _finishTick = 0;
            _timeRemaining = 0;
            _awaitingFinalScore = true;
            ShowMessage("Waiting for final score packet...", 3000, currentTick);
        }
        private void ResolveRoundResult(int currentTick)
        {
            _awaitingFinalScore = false;
            ShowRoundResult(currentTick);
            string winner = _team0Score > _team1Score
                ? "Team Maple wins!"
                : _team0Score < _team1Score
                    ? "Team Story wins!"
                    : "It's a tie!";
            ShowMessage(winner, 5000, currentTick);
            System.Diagnostics.Debug.WriteLine($"[CoconutField] Game ended: {winner}");
        }
        #endregion
        #region Update
        public void Update(int tickCount)
        {
            if (!_runtimeActive)
            {
                return;
            }
            EnsureAssetsLoaded();
            ProcessHitQueue(tickCount);
            if (_gameActive)
            {
                _lastUpdateTime = tickCount;
                if (_finishTick > 0)
                {
                    int remainingMs = _finishTick - tickCount;
                    _timeRemaining = remainingMs > 0
                        ? (remainingMs + 999) / 1000
                        : 0;
                    if (remainingMs <= 0)
                    {
                        BeginFinalScoreWait(tickCount);
                    }
                }
            }
            foreach (var coconut in _coconuts)
            {
                coconut.Update(tickCount, _gravity, _groundY);
            }
            if (_currentMessage != null && tickCount >= _messageEndTime)
            {
                _currentMessage = null;
            }
            UpdateResultFrames(tickCount);
        }
        private void ProcessHitQueue(int tickCount)
        {
            for (int i = _hitQueue.Count - 1; i >= 0; i--)
            {
                var hit = _hitQueue[i];
                if (tickCount >= hit.StartTime)
                {
                    if (hit.Target >= 0 && hit.Target < _coconuts.Count)
                    {
                        ApplyPacketState(_coconuts[hit.Target], (CoconutState)hit.NewState);
                    }
                    _hitQueue.RemoveAt(i);
                }
            }
        }
        private void QueueHit(int targetId, CoconutState newState, int startTime)
        {
            _hitQueue.Add(new HitInfo
            {
                Target = targetId,
                NewState = (int)newState,
                StartTime = startTime
            });
        }
        private void QueueAttackPacketRequest(int targetId, int delayMs, int requestedAtTick)
        {
            _pendingAttackPacketRequests.Add(new AttackPacketRequest(targetId, delayMs, requestedAtTick));
        }
        private void AcknowledgeAttackPacketRequest(int targetId)
        {
            if (_pendingAttackPacketRequests.Count == 0)
            {
                return;
            }
            if (targetId < 0)
            {
                _pendingAttackPacketRequests.Clear();
                return;
            }
            List<AttackPacketRequest> remaining = new();
            bool acknowledged = false;
            while (_pendingAttackPacketRequests.Count > 0)
            {
                AttackPacketRequest request = _pendingAttackPacketRequests[0];
                _pendingAttackPacketRequests.RemoveAt(0);
                if (!acknowledged && request.TargetId == targetId)
                {
                    acknowledged = true;
                    continue;
                }
                remaining.Add(request);
            }
            while (remaining.Count > 0)
            {
                _pendingAttackPacketRequests.Add(remaining[0]);
                remaining.RemoveAt(0);
            }
        }
        private void ApplyPacketState(Coconut coconut, CoconutState newState)
        {
            coconut.State = newState;
            switch (newState)
            {
                case CoconutState.OnTree:
                    coconut.Team = -1;
                    coconut.Velocity = Vector2.Zero;
                    coconut.IsActive = true;
                    coconut.Rotation = 0f;
                    break;
                case CoconutState.Falling:
                    coconut.IsActive = true;
                    break;
                case CoconutState.Team0Claimed:
                    coconut.Team = 0;
                    coconut.Velocity = Vector2.Zero;
                    coconut.IsActive = true;
                    break;
                case CoconutState.Team1Claimed:
                    coconut.Team = 1;
                    coconut.Velocity = Vector2.Zero;
                    coconut.IsActive = true;
                    break;
                case CoconutState.Scored:
                    coconut.Velocity = Vector2.Zero;
                    coconut.IsActive = true;
                    break;
                case CoconutState.Destroyed:
                    coconut.Velocity = Vector2.Zero;
                    coconut.IsActive = false;
                    break;
            }
        }
        private void ShowMessage(string message, int durationMs, int currentTick)
        {
            _currentMessage = message;
            _messageEndTime = currentTick + durationMs;
        }
        private bool IsAttackable(Coconut coconut)
        {
            return coconut != null
                && coconut.IsActive
                && coconut.State != CoconutState.Scored
                && coconut.State != CoconutState.Destroyed;
        }
        private Rectangle GetObjectBounds(Coconut coconut)
        {
            if (coconut?.Frames != null && coconut.Frames.Count > 0)
            {
                IDXObject frame = coconut.Frames[coconut.FrameIndex % coconut.Frames.Count];
                return new Rectangle(
                    (int)coconut.Position.X + frame.X,
                    (int)coconut.Position.Y + frame.Y,
                    Math.Max(1, frame.Width),
                    Math.Max(1, frame.Height));
            }
            int size = (int)(24 * coconut.Scale);
            return new Rectangle(
                (int)coconut.Position.X - size / 2,
                (int)coconut.Position.Y - size / 2,
                Math.Max(1, size),
                Math.Max(1, size));
        }
        private static CoconutState ResolveLocalAttackState(Coconut coconut, int localTeam)
        {
            CoconutState claimState = localTeam == 0 ? CoconutState.Team0Claimed : CoconutState.Team1Claimed;
            return coconut.State switch
            {
                CoconutState.OnTree => CoconutState.Falling,
                CoconutState.Falling => claimState,
                CoconutState.Team0Claimed when localTeam == 1 => CoconutState.Team1Claimed,
                CoconutState.Team1Claimed when localTeam == 0 => CoconutState.Team0Claimed,
                _ => coconut.State
            };
        }
        private void ShowRoundResult(int currentTick)
        {
            EnsureAssetsLoaded();
            if (_team0Score == _team1Score)
            {
                _lastRoundResult = RoundResult.Draw;
                _activeResultFrames = null;
                _resultExpireTime = currentTick + 3000;
                return;
            }
            bool localWon = _localTeam == 0 ? _team0Score > _team1Score : _team1Score > _team0Score;
            _lastRoundResult = localWon ? RoundResult.Victory : RoundResult.Lose;
            _activeResultFrames = localWon ? _victoryFrames : _loseFrames;
            _resultFrameIndex = 0;
            _resultFrameStartTime = currentTick;
            _resultExpireTime = currentTick + GetAnimationDuration(_activeResultFrames);
        }
        private void ClearRoundResult()
        {
            _lastRoundResult = RoundResult.None;
            _activeResultFrames = null;
            _resultFrameIndex = 0;
            _resultFrameStartTime = 0;
            _resultExpireTime = 0;
        }
        private void UpdateResultFrames(int tickCount)
        {
            if (_activeResultFrames == null || _activeResultFrames.Count == 0)
            {
                if (_resultExpireTime > 0 && tickCount >= _resultExpireTime && _lastRoundResult != RoundResult.None)
                {
                    _resultExpireTime = 0;
                }
                return;
            }
            if (tickCount >= _resultExpireTime)
            {
                _activeResultFrames = null;
                return;
            }
            while (_resultFrameIndex < _activeResultFrames.Count - 1)
            {
                int frameDelay = _activeResultFrames[_resultFrameIndex].Delay > 0 ? _activeResultFrames[_resultFrameIndex].Delay : 100;
                if (tickCount - _resultFrameStartTime < frameDelay)
                {
                    break;
                }
                _resultFrameStartTime += frameDelay;
                _resultFrameIndex++;
            }
        }
        private static int GetAnimationDuration(List<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 3000;
            }
            int total = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                total += frames[i].Delay > 0 ? frames[i].Delay : 100;
            }
            return total;
        }
        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }
            WzImage objectSet = global::HaCreator.Program.InfoManager?.GetObjectSet("etc");
            WzImageProperty coconutBoard = objectSet?["coconut"];
            if (TryCreateDxObject(WzInfoTools.GetRealProperty(coconutBoard?["backgrnd"]) as WzCanvasProperty, out IDXObject boardBackground))
            {
                _boardBackground = boardBackground;
            }
            LoadBitmapFont(coconutBoard?["fontScore"], _scoreFont, "0123456789,");
            LoadBitmapFont(coconutBoard?["fontTime"], _timeFont, "0123456789:");
            WzImage effectImage = global::HaCreator.Program.FindImage("Map", "Effect.img")
                ?? global::HaCreator.Program.FindImage("Map", "effect.img");
            WzImageProperty coconutRoot = effectImage?["event"]?["coconut"];
            LoadAnimatedFrames(coconutRoot?["victory"], _victoryFrames);
            LoadAnimatedFrames(coconutRoot?["lose"], _loseFrames);
            _assetsLoaded = true;
        }
        private void LoadBitmapFont(WzImageProperty source, Dictionary<char, IDXObject> target, string characters)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }
            foreach (char character in characters)
            {
                string propertyName = character switch
                {
                    ':' => ":",
                    ',' => "comma",
                    _ => character.ToString()
                };
                if (TryCreateDxObject(WzInfoTools.GetRealProperty(source[propertyName]) as WzCanvasProperty, out IDXObject glyph))
                {
                    target[character] = glyph;
                }
            }
        }
        private void LoadAnimatedFrames(WzImageProperty source, List<IDXObject> target)
        {
            target.Clear();
            if (source == null)
            {
                return;
            }
            WzImageProperty resolvedSource = WzInfoTools.GetRealProperty(source);
            if (resolvedSource is WzCanvasProperty canvas)
            {
                if (TryCreateDxObject(canvas, out IDXObject singleFrame))
                {
                    target.Add(singleFrame);
                }
                return;
            }
            if (resolvedSource is not WzSubProperty)
            {
                return;
            }
            for (int i = 0; ; i++)
            {
                if (WzInfoTools.GetRealProperty(resolvedSource[i.ToString()]) is not WzCanvasProperty frameCanvas)
                {
                    break;
                }
                if (TryCreateDxObject(frameCanvas, out IDXObject frame))
                {
                    target.Add(frame);
                }
            }
        }
        private bool TryCreateDxObject(WzCanvasProperty canvas, out IDXObject dxObject)
        {
            dxObject = null;
            if (_graphicsDevice == null || canvas == null)
            {
                return false;
            }
            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return false;
            }
            Texture2D texture = bitmap.ToTexture2D(_graphicsDevice);
            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            int delay = canvas["delay"]?.GetInt() ?? 100;
            dxObject = new DXObject(-(int)origin.X, -(int)origin.Y, texture, delay);
            return true;
        }
        #endregion
        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime, int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font = null)
        {
            int shiftCenterX = mapShiftX - centerX;
            int shiftCenterY = mapShiftY - centerY;
            // Draw coconuts
            foreach (var coconut in _coconuts)
            {
                if (!coconut.IsActive)
                    continue;
                int screenX = (int)coconut.Position.X - shiftCenterX;
                int screenY = (int)coconut.Position.Y - shiftCenterY;
                if (coconut.Frames != null && coconut.Frames.Count > 0)
                {
                    var frame = coconut.Frames[coconut.FrameIndex % coconut.Frames.Count];
                    Color tint = GetCoconutTint(coconut);
                    frame.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime,
                        screenX + frame.X, screenY + frame.Y, tint, false, null);
                }
                else if (pixelTexture != null)
                {
                    // Debug draw
                    Color color = GetCoconutColor(coconut);
                    int size = (int)(24 * coconut.Scale);
                    spriteBatch.Draw(pixelTexture,
                        new Rectangle(screenX - size / 2, screenY - size / 2, size, size),
                        color);
                }
            }
            // Draw tree area (debug)
            if (pixelTexture != null)
            {
                int treeScreenX = _treeArea.X - shiftCenterX;
                int treeScreenY = _treeArea.Y - shiftCenterY;
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(treeScreenX, treeScreenY, _treeArea.Width, _treeArea.Height),
                    new Color(0, 100, 0, 50));
            }
            DrawUI(spriteBatch, skeletonMeshRenderer, gameTime, pixelTexture, font);
            if (font != null)
            {
                DrawRoundResult(spriteBatch, skeletonMeshRenderer, gameTime, font, tickCount);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color GetCoconutTint(Coconut coconut)
        {
            return coconut.State switch
            {
                CoconutState.Team0Claimed => new Color(150, 200, 255, 255),
                CoconutState.Team1Claimed => new Color(255, 150, 150, 255),
                _ => Color.White
            };
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color GetCoconutColor(Coconut coconut)
        {
            return coconut.State switch
            {
                CoconutState.OnTree => new Color(139, 90, 43, 220),
                CoconutState.Falling => coconut.Team == 0
                    ? new Color(100, 150, 255, 220)
                    : new Color(255, 100, 100, 220),
                CoconutState.Scored => Color.Gold,
                _ => Color.Brown
            };
        }
        private void DrawUI(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, Texture2D pixel, SpriteFont font)
        {
            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            int boardX = (screenWidth - BoardWidth) / 2;
            int boardY = BoardTopY;
            if (_boardBackground != null)
            {
                _boardBackground.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime, boardX + _boardBackground.X, boardY + _boardBackground.Y, Color.White, false, null);
            }
            else if (pixel != null)
            {
                spriteBatch.Draw(pixel, new Rectangle(boardX, boardY, BoardWidth, BoardHeight), new Color(0, 0, 0, 150));
            }
            string team0Text = _team0Score.ToString();
            string team1Text = _team1Score.ToString();
            if (!DrawBitmapText(spriteBatch, skeletonMeshRenderer, gameTime, _scoreFont, team0Text, boardX + Team0ScoreX, boardY + ScoreY, ScoreDigitSpacing)
                && font != null)
            {
                spriteBatch.DrawString(font, team0Text, new Vector2(boardX + Team0ScoreX, boardY + ScoreY), new Color(120, 190, 255));
            }
            if (!DrawBitmapText(spriteBatch, skeletonMeshRenderer, gameTime, _scoreFont, team1Text, boardX + Team1ScoreX, boardY + ScoreY, ScoreDigitSpacing)
                && font != null)
            {
                spriteBatch.DrawString(font, team1Text, new Vector2(boardX + Team1ScoreX, boardY + ScoreY), new Color(255, 140, 140));
            }
            int minutes = _timeRemaining / 60;
            int seconds = _timeRemaining % 60;
            string timerText = $"{minutes}:{seconds:D2}";
            if (!DrawBitmapText(spriteBatch, skeletonMeshRenderer, gameTime, _timeFont, timerText, boardX + TimerX, boardY + TimerY, TimerDigitSpacing)
                && font != null)
            {
                Color timerColor = _timeRemaining <= 10 ? Color.Red : Color.Yellow;
                spriteBatch.DrawString(font, timerText, new Vector2(boardX + TimerX, boardY + TimerY), timerColor);
            }
            if (_currentMessage != null && font != null)
            {
                Vector2 msgSize = font.MeasureString(_currentMessage);
                Vector2 msgPos = new Vector2((screenWidth - msgSize.X) / 2, boardY + BoardHeight + 16);
                spriteBatch.DrawString(font, _currentMessage, msgPos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, _currentMessage, msgPos, Color.Yellow);
            }
        }
        private static bool DrawBitmapText(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            Dictionary<char, IDXObject> font,
            string text,
            int x,
            int y,
            int letterSpacing)
        {
            if (font == null || font.Count == 0 || string.IsNullOrEmpty(text))
            {
                return false;
            }
            int cursorX = x;
            foreach (char character in text)
            {
                if (!font.TryGetValue(character, out IDXObject glyph))
                {
                    return false;
                }
                glyph.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime, cursorX + glyph.X, y + glyph.Y, Color.White, false, null);
                cursorX += glyph.Width + letterSpacing;
            }
            return true;
        }
        private void DrawRoundResult(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, SpriteFont font, int tickCount)
        {
            if (_lastRoundResult == RoundResult.None || _resultExpireTime <= tickCount)
            {
                return;
            }
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int centerX = viewport.Width / 2;
            if (_activeResultFrames != null && _activeResultFrames.Count > 0)
            {
                IDXObject frame = _activeResultFrames[Math.Clamp(_resultFrameIndex, 0, _activeResultFrames.Count - 1)];
                frame.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime, centerX + frame.X, ResultBannerTopY + frame.Y, Color.White, false, null);
                return;
            }
            string resultText = _lastRoundResult switch
            {
                RoundResult.Victory => "Victory",
                RoundResult.Lose => "Defeat",
                RoundResult.Draw => "Draw",
                _ => null
            };
            if (resultText == null)
            {
                return;
            }
            Vector2 textSize = font.MeasureString(resultText);
            Vector2 position = new Vector2((viewport.Width - textSize.X) * 0.5f, ResultBannerTopY + 92);
            spriteBatch.DrawString(font, resultText, position + Vector2.One, Color.Black);
            spriteBatch.DrawString(font, resultText, position, Color.White);
        }
        #endregion
        #region Utility
        public void Reset()
        {
            _runtimeActive = false;
            _gameActive = false;
            _awaitingFinalScore = false;
            _hitQueue.Clear();
            _pendingAttackPacketRequests.Clear();
            _team0Score = 0;
            _team1Score = 0;
            _timeRemaining = 0;
            _finishTick = 0;
            _currentMessage = null;
            _localTeam = 0;
            _lastPacketType = null;
            _lastScorePacketTick = null;
            ClearRoundResult();
            foreach (var coconut in _coconuts)
            {
                coconut.State = CoconutState.OnTree;
                coconut.Position = coconut.InitialPosition;
                coconut.Velocity = Vector2.Zero;
                coconut.Team = -1;
                coconut.HitCount = 0;
                coconut.IsActive = true;
                coconut.Rotation = 0;
            }
        }
        private static bool IsCoconutObject(ObjectInstance instance)
        {
            if (instance?.BaseInfo is not ObjectInfo objectInfo)
            {
                return false;
            }
            if (!string.Equals(objectInfo.oS, "etc", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return string.Equals(objectInfo.l0, "coconut", StringComparison.OrdinalIgnoreCase)
                || string.Equals(objectInfo.l0, "coconut2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(objectInfo.l1, "coconut", StringComparison.OrdinalIgnoreCase)
                || string.Equals(objectInfo.l1, "coconut2", StringComparison.OrdinalIgnoreCase);
        }
        private List<IDXObject> CreateFramesForObject(ObjectInstance instance)
        {
            if (_graphicsDevice == null || instance?.BaseInfo is not ObjectInfo objectInfo || objectInfo.Image == null)
            {
                return null;
            }
            string cacheKey = $"{objectInfo.oS}/{objectInfo.l0}/{objectInfo.l1}/{objectInfo.l2}";
            if (!_objectFrameCache.TryGetValue(cacheKey, out IDXObject frame))
            {
                Texture2D texture = objectInfo.Image.ToTexture2D(_graphicsDevice);
                frame = new DXObject(-objectInfo.Origin.X, -objectInfo.Origin.Y, texture, 100);
                _objectFrameCache[cacheKey] = frame;
            }
            return new List<IDXObject> { frame };
        }
        private static Rectangle GetObjectBounds(IReadOnlyList<ObjectInstance> coconutObjects)
        {
            int minX = coconutObjects.Min(instance => instance.X);
            int maxX = coconutObjects.Max(instance => instance.X);
            int minY = coconutObjects.Min(instance => instance.Y);
            int maxY = coconutObjects.Max(instance => instance.Y);
            return new Rectangle(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
        }
        private static Rectangle ExpandBounds(Rectangle bounds, int horizontalPadding, int verticalPadding)
        {
            return new Rectangle(
                bounds.Left - horizontalPadding,
                bounds.Top - verticalPadding,
                bounds.Width + horizontalPadding * 2,
                bounds.Height + verticalPadding * 2);
        }
        private static Rectangle ResolveFallbackTreeArea(Board board)
        {
            int left = board?.MapInfo?.VRLeft ?? 0;
            int right = board?.MapInfo?.VRRight ?? left + 800;
            int top = board?.MapInfo?.VRTop ?? 0;
            int width = Math.Max(1, right - left);
            return new Rectangle(left, top, width, Math.Max(120, (board?.MapInfo?.VRBottom ?? top + 600) - top));
        }
        private static int ResolveGroundY(Board board, IReadOnlyList<ObjectInstance> coconutObjects)
        {
            int fallbackGround = board?.MapInfo?.VRBottom
                ?? (coconutObjects.Count > 0 ? coconutObjects.Max(instance => instance.Y) + 120 : 600);
            return fallbackGround;
        }
        #endregion
    }
    #endregion
}
