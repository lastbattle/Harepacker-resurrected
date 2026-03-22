using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
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
    public class MinigameFields
    {
        private readonly SnowBallField _snowBall = new();
        private readonly CoconutField _coconut = new();
        private readonly MemoryGameField _memoryGame = new();
        private readonly AriantArenaField _ariantArena = new();
        private readonly MonsterCarnivalField _monsterCarnival = new();

        public SnowBallField SnowBall => _snowBall;
        public CoconutField Coconut => _coconut;
        public MemoryGameField MemoryGame => _memoryGame;
        public AriantArenaField AriantArena => _ariantArena;
        public MonsterCarnivalField MonsterCarnival => _monsterCarnival;

        public void SetSnowBallPlayerState(Vector2? localWorldPosition)
        {
            _snowBall.SetLocalPlayerPosition(localWorldPosition);
        }

        public void Initialize(GraphicsDevice graphicsDevice, SoundManager soundManager = null)
        {
            _coconut.Initialize(graphicsDevice);
            _memoryGame.Initialize(graphicsDevice);
            _ariantArena.Initialize(graphicsDevice, soundManager);
        }

        public void BindMap(Board board)
        {
            _coconut.BindMap(board);
        }

        public void Update(int tickCount)
        {
            if (_snowBall.IsActive || _snowBall.State != SnowBallField.GameState.NotStarted)
            {
                _snowBall.Update(tickCount);
            }

            if (_coconut.IsActive)
            {
                _coconut.Update(tickCount);
            }

            if (_memoryGame.IsVisible)
            {
                _memoryGame.Update(tickCount);
            }

            if (_ariantArena.IsActive)
            {
                _ariantArena.Update(tickCount);
            }

            if (_monsterCarnival.IsVisible)
            {
                _monsterCarnival.Update(tickCount);
            }
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            Texture2D pixelTexture,
            SpriteFont font = null)
        {
            if (_snowBall.IsActive || _snowBall.State != SnowBallField.GameState.NotStarted)
            {
                _snowBall.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }

            if (_coconut.IsActive)
            {
                _coconut.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }

            if (_memoryGame.IsVisible)
            {
                _memoryGame.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }

            if (_ariantArena.IsActive)
            {
                _ariantArena.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }

            if (_monsterCarnival.IsVisible)
            {
                _monsterCarnival.Draw(spriteBatch, pixelTexture, font);
            }
        }

        public void ResetAll()
        {
            _snowBall.Reset();
            _coconut.Reset();
            _memoryGame.Reset();
            _ariantArena.Reset();
            _monsterCarnival.Reset();
        }
    }

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
        #region Constants (from client)
        // Static configuration from WZ (set during Init)
        private static int ms_nDeltaX = 20;  // Movement per hit
        private static Rectangle ms_rgBall;  // Valid ball range
        private static readonly int[] ms_anDelay = { 0, 90, 75, 60, 45, 30 };
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

        public class SnowBall
        {
            public Rectangle Area;           // m_rcArea - Bounding box
            public string PortalName;        // m_sPortalName - Win portal target
            public Vector2 Origin;           // m_pVecOrg - Origin position
            public float Rotation;           // Current rotation angle (degrees)
            public int Team;                 // 0 = left, 1 = right
            public bool IsWinner;            // Animation stopped
            public List<IDXObject> Frames;   // Animation frames
            public int FrameIndex;
            public int LastFrameTime;
            public int SpeedDegree;
            public int PositionDelta;
            public int MovementElapsed;

            // Movement properties
            public int PositionX => Area.X + Area.Width / 2;
            public int PositionY => Area.Y + Area.Height / 2;

            public void Move(int dx)
            {
                int moveAmount = dx * ms_nDeltaX;
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
                Area.X = x - Area.Width / 2;
                Area.Y = y - Area.Height / 2;
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

            public void Init(int x, int y, int hp)
            {
                Area = new Rectangle(x - 50, y - 100, 100, 100);
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

        // Event config from WZ
        private int _leftGoalX;
        private int _rightGoalX;
        private int _groundY;

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
        private TouchPacketRequest? _pendingTouchPacketRequest;
        private int _touchPacketSequence;

        #endregion

        #region Properties

        public GameState State => _state;
        public SnowBall[] SnowBalls => _snowBalls;
        public SnowMan[] SnowMen => _snowMen;
        public int Team0Score => _team0Score;
        public int Team1Score => _team1Score;
        public bool IsActive => _state == GameState.Active;
        public string CurrentMessage => _currentMessage;
        public TouchPacketRequest? PendingTouchPacketRequest => _pendingTouchPacketRequest;
        public int TouchPacketSequence => _touchPacketSequence;

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
            ms_nDeltaX = deltaX;
            _leftGoalX = leftGoalX;
            _rightGoalX = rightGoalX;
            _groundY = groundY;
            _damageSnowBall = damageSnowBall;
            _damageSnowMan[0] = damageSnowMan0;
            _damageSnowMan[1] = damageSnowMan1;
            _snowManWaitMs = snowManWaitMs;

            int centerX = (leftGoalX + rightGoalX) / 2;
            int ballStartOffset = (rightGoalX - leftGoalX) / 4;

            // Initialize snowballs
            for (int i = 0; i < 2; i++)
            {
                _snowBalls[i] = new SnowBall
                {
                    Team = i,
                    Area = new Rectangle(
                        i == 0 ? centerX - ballStartOffset - snowBallRadius : centerX + ballStartOffset - snowBallRadius,
                        groundY - snowBallRadius * 2,
                        snowBallRadius * 2,
                        snowBallRadius * 2
                    ),
                    Origin = new Vector2(
                        i == 0 ? centerX - ballStartOffset : centerX + ballStartOffset,
                        groundY - snowBallRadius
                    ),
                    Rotation = 0f,
                    IsWinner = false
                };
            }

            // Initialize snowmen (blockers)
            for (int i = 0; i < 2; i++)
            {
                _snowMen[i] = new SnowMan { Team = i };
                int snowManX = i == 0 ? leftGoalX + 100 : rightGoalX - 100;
                _snowMen[i].Init(snowManX, groundY, snowManHp);
                _snowMen[i].StunDurationMs = snowManWaitMs;
            }

            ms_rgBall = new Rectangle(leftGoalX, groundY - 200, rightGoalX - leftGoalX, 200);
            _state = GameState.NotStarted;

            System.Diagnostics.Debug.WriteLine($"[SnowBallField] Initialized: leftGoal={leftGoalX}, rightGoal={rightGoalX}, groundY={groundY}");
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
            GameState previousState = _state;
            bool isFirstSnapshot = !_hasReceivedStateSnapshot;
            _hasReceivedStateSnapshot = true;
            _state = (GameState)newState;

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
                _snowBalls[0].ApplyStatePosition(team0Pos, _groundY - _snowBalls[0].Area.Height / 2, team0SpeedDegree, isFirstSnapshot || _state != GameState.Active);
            }

            if (_snowBalls[1] != null)
            {
                _snowBalls[1].ApplyStatePosition(team1Pos, _groundY - _snowBalls[1].Area.Height / 2, team1SpeedDegree, isFirstSnapshot || _state != GameState.Active);
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
                    _team0Score++;
                    ShowMessage("Team Story wins the round!", 5000);
                    break;
                case GameState.Team1Win:
                    _snowBalls[1]?.Win();
                    _team1Score++;
                    ShowMessage("Team Maple wins the round!", 5000);
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
            _damageQueue.Add(new DamageInfo
            {
                Target = target,
                Damage = damage,
                StartTime = Environment.TickCount + delay
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
        public void OnSnowBallTouch(int team)
        {
            if (_pendingTouchPacketRequest?.Team == team)
            {
                _pendingTouchPacketRequest = null;
            }

            _lastTouchImpactTime = Environment.TickCount;
        }

        public bool TryConsumeTouchPacketRequest(out TouchPacketRequest request)
        {
            if (_pendingTouchPacketRequest.HasValue)
            {
                request = _pendingTouchPacketRequest.Value;
                _pendingTouchPacketRequest = null;
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
                        int resolvedDamage = damage > 0 ? damage : _damageSnowMan[Math.Min(snowManIndex, _damageSnowMan.Length - 1)];
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
            if (_snowBalls[0] != null && _snowBalls[0].PositionX >= _rightGoalX - 50)
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
            else if (_snowBalls[1] != null && _snowBalls[1].PositionX <= _leftGoalX + 50)
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
            if (!_localPlayerPosition.HasValue)
            {
                return;
            }

            int touchedTeam = GetTouchedSnowBallTeam(_localPlayerPosition.Value);
            if (touchedTeam < 0 || (int)_state == touchedTeam + 2)
            {
                return;
            }

            _touchPacketSequence++;
            _pendingTouchPacketRequest = new TouchPacketRequest(touchedTeam, tickCount, _touchPacketSequence);
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

                int screenX = ball.Area.X - shiftCenterX;
                int screenY = ball.Area.Y - shiftCenterY;

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

                int screenX = snowMan.Area.X - shiftCenterX;
                int screenY = snowMan.Area.Y - shiftCenterY;

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
            _pendingTouchPacketRequest = null;
            _touchPacketSequence = 0;

            // Reset snowballs
            int centerX = (_leftGoalX + _rightGoalX) / 2;
            int ballStartOffset = (_rightGoalX - _leftGoalX) / 4;

            for (int i = 0; i < 2; i++)
            {
                if (_snowBalls[i] != null)
                {
                    int x = i == 0 ? centerX - ballStartOffset : centerX + ballStartOffset;
                    _snowBalls[i].SetPos(x, _groundY - _snowBalls[i].Area.Height / 2, i == 1);
                    _snowBalls[i].Rotation = 0f;
                    _snowBalls[i].IsWinner = false;
                    _snowBalls[i].SpeedDegree = 0;
                }
            }

            // Reset snowmen
            for (int i = 0; i < 2; i++)
            {
                if (_snowMen[i] != null)
                {
                    _snowMen[i].HP = _snowMen[i].MaxHP;
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

        #endregion
    }
    #endregion

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
        private const int FinalScoreGraceMs = 750;
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
                    // Apply gravity
                    Velocity.Y += gravity;
                    Position += Velocity;

                    // Rotate while falling
                    Rotation += Velocity.X * 2f;

                    // Hit ground
                    if (Position.Y >= groundY)
                    {
                        Position.Y = groundY;
                        State = CoconutState.Scored;
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
        private int _finalScoreDeadlineTick;
        private int? _lastPacketType;

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
        public IReadOnlyList<Coconut> Coconuts => _coconuts;
        public RoundResult LastRoundResult => _lastRoundResult;
        public int? LastPacketType => _lastPacketType;

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
            _finalScoreDeadlineTick = 0;
            _lastPacketType = null;
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

            if (_awaitingFinalScore || (!_gameActive && _timeRemaining <= 0))
            {
                ResolveRoundResult(currentTimeMs ?? Environment.TickCount);
            }

            System.Diagnostics.Debug.WriteLine($"[CoconutField] Score: Maple {team0} - Story {team1}");
        }

        /// <summary>
        /// OnClock - Time update
        /// </summary>
        public void OnClock(int timeSeconds)
        {
            int now = Environment.TickCount;
            int durationMs = Math.Max(0, timeSeconds) * 1000;
            _runtimeActive = true;
            _finishTick = durationMs > 0 ? now + durationMs : 1;
            _timeRemaining = Math.Max(0, timeSeconds);

            if (timeSeconds > 0)
            {
                _gameActive = true;
                _awaitingFinalScore = false;
                _finalScoreDeadlineTick = 0;
                _lastUpdateTime = now;
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
                    ? "awaiting-final-score"
                    : "idle";

            return $"Coconut runtime active, coconuts={_coconuts.Count}, round={roundState}, score={_team0Score}-{_team1Score}, time={_timeRemaining}, result={_lastRoundResult}, lastPacket={(_lastPacketType?.ToString() ?? "None")}";
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
            _finalScoreDeadlineTick = 0;
            ClearRoundResult();
            ShowMessage("Coconut harvest begins!", 3000);
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

        public bool TryHandleNormalAttack(Rectangle attackBounds, int currentTick, int skillId = 0)
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

            QueueHit(target.Id, ResolveLocalAttackState(target, _localTeam), currentTick + LocalNormalAttackDelayMs);
            return true;
        }

        private void BeginFinalScoreWait(int currentTick)
        {
            _gameActive = false;
            _finishTick = 0;
            _timeRemaining = 0;
            _awaitingFinalScore = true;
            _finalScoreDeadlineTick = currentTick + FinalScoreGraceMs;
        }

        private void ResolveRoundResult(int currentTick)
        {
            _awaitingFinalScore = false;
            _finalScoreDeadlineTick = 0;
            ShowRoundResult(currentTick);
            string winner = _team0Score > _team1Score
                ? "Team Maple wins!"
                : _team0Score < _team1Score
                    ? "Team Story wins!"
                    : "It's a tie!";

            ShowMessage(winner, 5000);
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

            if (_awaitingFinalScore && tickCount >= _finalScoreDeadlineTick)
            {
                ResolveRoundResult(tickCount);
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
                    if (coconut.Team < 0)
                    {
                        coconut.Team = 0;
                    }
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

        private void ShowMessage(string message, int durationMs)
        {
            _currentMessage = message;
            _messageEndTime = Environment.TickCount + durationMs;
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
                DrawRoundResult(spriteBatch, skeletonMeshRenderer, gameTime, font);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Color GetCoconutTint(Coconut coconut)
        {
            return coconut.Team switch
            {
                0 => new Color(150, 200, 255, 255), // Blue tint
                1 => new Color(255, 150, 150, 255), // Red tint
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

        private void DrawRoundResult(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, SpriteFont font)
        {
            if (_lastRoundResult == RoundResult.None || _resultExpireTime <= Environment.TickCount)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int centerX = viewport.Width / 2;

            if (_activeResultFrames != null && _activeResultFrames.Count > 0)
            {
                IDXObject frame = _activeResultFrames[Math.Clamp(_resultFrameIndex, 0, _activeResultFrames.Count - 1)];
                frame.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime, centerX + frame.X, ResultBannerTopY + frame.Y, Color.White, false, null);
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
            _finalScoreDeadlineTick = 0;
            _hitQueue.Clear();
            _team0Score = 0;
            _team1Score = 0;
            _timeRemaining = 0;
            _finishTick = 0;
            _currentMessage = null;
            _localTeam = 0;
            _lastPacketType = null;
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

    #region Memory Game / Match Cards (CMemoryGameDlg)
    public enum MemoryGamePacketType
    {
        OpenRoom,
        SetReady,
        StartGame,
        RevealCard,
        ClaimTie,
        GiveUp,
        EndRoom,
        SelectMatchCardsMode
    }

    /// <summary>
    /// MiniRoom Match Cards runtime. This mirrors the client-owned dialog shape
    /// by keeping a dedicated room shell, board state, ready/start button flow,
    /// turn ownership, and delayed mismatch hide handling.
    /// </summary>
    public class MemoryGameField
    {
        private const int DefaultRows = 4;
        private const int DefaultColumns = 4;
        private const int DefaultLocalPlayerIndex = 0;
        private const int DefaultMismatchHideDelayMs = 900;
        private const int DefaultTurnSeconds = 15;
        private const int DefaultResultSeconds = 5;
        private const int DefaultRemoteActionDelayMs = 600;
        private const int ClientDialogWidth = 734;
        private const int ClientDialogHeight = 429;
        private const int ClientBoardLeft = 48;
        private const int ClientBoardTop = 36;
        private const int ClientBoardWidth = 292;
        private const int ClientBoardHeight = 330;
        private const int ClientTurnIndicatorY = 55;
        private const int ClientTurnIndicatorLeftX = 402;
        private const int ClientTurnIndicatorRightX = 488;
        private const int ClientNameBarY = 149;
        private const int ClientNameBarLeftX = 404;
        private const int ClientNameBarRightX = 490;
        private const int ClientRecordPanelX = 404;
        private const int ClientRecordPanelY = 167;
        private const int ClientMasterPanelX = 460;
        private const int ClientMasterPanelY = 63;
        private const int ClientTimerTextX = 295;
        private const int ClientTimerTextY = 404;
        private const int ClientReadyButtonX = 625;
        private const int ClientReadyButtonY = 243;
        private const int ClientTieButtonX = 458;
        private const int ClientTieButtonY = 403;
        private const int ClientGiveUpButtonX = 410;
        private const int ClientGiveUpButtonY = 403;
        private const int ClientEndButtonX = 679;
        private const int ClientEndButtonY = 403;
        private const int ClientBanButtonX = 551;
        private const int ClientBanButtonY = 63;
        private const int ClientScoreLeftX = 418;
        private const int ClientScoreRightX = 582;
        private const int ClientScoreY = 176;
        private const int ClientReadyIndicatorLeftX = 408;
        private const int ClientReadyIndicatorRightX = 494;
        private const int ClientReadyIndicatorY = 184;
        private const int CardFaceTextureCount = 15;
        private const int CardBackTextureCount = 3;
        private const int DigitTextureCount = 10;
        private const byte MiniRoomBaseEnterPacketType = 4;
        private const byte MiniRoomBaseGameplayPacketType = 6;
        private const byte MiniRoomBaseChatPacketType = 7;
        private const byte MiniRoomBaseChatRepeatPacketType = 8;
        private const byte MiniRoomBaseAvatarPacketType = 9;
        private const byte MiniRoomBaseLeavePacketType = 10;
        private const byte MiniRoomChatGameMessageType = 7;
        private const byte MemoryGameTieRequestPacketType = 50;
        private const byte MemoryGameTieResultPacketType = 51;
        private const byte MemoryGameReadyPacketType = 58;
        private const byte MemoryGameCancelReadyPacketType = 59;
        private const byte MemoryGameStartPacketType = 61;
        private const byte MemoryGameGameResultPacketType = 62;
        private const byte MemoryGameTimeOverPacketType = 63;
        private const byte MemoryGameTurnUpCardPacketType = 68;

        private readonly List<Card> _cards = new();
        private readonly List<int> _revealedCardIndices = new(2);
        private readonly Queue<PendingRemoteAction> _pendingRemoteActions = new();
        private readonly Dictionary<int, MiniRoomParticipantState> _miniRoomParticipants = new();
        private readonly int[] _scores = new int[2];
        private readonly bool[] _readyStates = new bool[2];
        private readonly string[] _playerNames = new string[2];
        private readonly int[] _wins = new int[2];
        private readonly int[] _losses = new int[2];
        private readonly int[] _draws = new int[2];
        private readonly Dictionary<MemoryGamePacketType, int> _packetCounts = new();
        private readonly Texture2D[] _cardFaceTextures = new Texture2D[CardFaceTextureCount];
        private readonly Texture2D[] _cardBackTextures = new Texture2D[CardBackTextureCount];
        private readonly Texture2D[] _digitTextures = new Texture2D[DigitTextureCount];

        private RoomStage _stage = RoomStage.Hidden;
        private SocialRoomRuntime _miniRoomRuntime;
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private Texture2D _backgroundTexture;
        private Texture2D _masterPanelTexture;
        private Texture2D _turnTexture;
        private Texture2D _readyOnTexture;
        private Texture2D _readyOffTexture;
        private Texture2D _winTexture;
        private Texture2D _loseTexture;
        private Texture2D _drawTexture;
        private Texture2D _readyButtonTexture;
        private Texture2D _startButtonTexture;
        private Texture2D _tieButtonTexture;
        private Texture2D _giveUpButtonTexture;
        private Texture2D _endButtonTexture;
        private Texture2D _banButtonTexture;
        private int _rows;
        private int _columns;
        private int _localPlayerIndex;
        private int _currentTurnIndex;
        private int _pendingHideTick;
        private int _turnDeadlineTick;
        private int _resultExpireTick;
        private int _lastWinnerIndex = -1;
        private string _title = "Match Cards";
        private string _statusMessage = "Open a MiniRoom to begin.";
        private MemoryGamePacketType? _lastPacketType;
        private string _lastPacketSummary = "No Match Cards packet dispatched.";
        private bool _waitingForTimeOverPacket;

        public enum RoomStage
        {
            Hidden,
            Lobby,
            Playing,
            Result
        }

        public sealed class Card
        {
            public int FaceId { get; init; }
            public bool IsFaceUp { get; set; }
            public bool IsMatched { get; set; }
        }

        private sealed class MiniRoomParticipantState
        {
            public MiniRoomParticipantState(int slot)
            {
                Slot = slot;
            }

            public int Slot { get; }
            public string Name { get; set; }
            public short JobCode { get; set; }
            public LoginAvatarLook AvatarLook { get; set; }
        }

        private readonly struct PendingRemoteAction
        {
            public PendingRemoteAction(RemoteActionType actionType, int executeTick, int playerIndex, int cardIndex, bool readyState)
            {
                ActionType = actionType;
                ExecuteTick = executeTick;
                PlayerIndex = playerIndex;
                CardIndex = cardIndex;
                ReadyState = readyState;
            }

            public RemoteActionType ActionType { get; }
            public int ExecuteTick { get; }
            public int PlayerIndex { get; }
            public int CardIndex { get; }
            public bool ReadyState { get; }
        }

        private enum RemoteActionType
        {
            Ready,
            Start,
            Reveal,
            Tie,
            GiveUp,
            End
        }

        public RoomStage Stage => _stage;
        public bool IsVisible => _stage != RoomStage.Hidden;
        public bool IsPlaying => _stage == RoomStage.Playing;
        public bool HasPendingMismatch => _pendingHideTick > 0;
        public IReadOnlyList<Card> Cards => _cards;
        public int CurrentTurnIndex => _currentTurnIndex;
        public int LocalPlayerIndex => _localPlayerIndex;
        public int CurrentTurnTimeRemainingSeconds => _turnDeadlineTick <= 0 ? 0 : Math.Max(0, (_turnDeadlineTick - Environment.TickCount + 999) / 1000);
        public int LastWinnerIndex => _lastWinnerIndex;
        public IReadOnlyList<int> Scores => _scores;
        public IReadOnlyList<bool> ReadyStates => _readyStates;
        public IReadOnlyList<string> PlayerNames => _playerNames;
        public string Title => _title;
        public MemoryGamePacketType? LastPacketType => _lastPacketType;
        public string LastPacketSummary => _lastPacketSummary;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public void AttachMiniRoomRuntime(SocialRoomRuntime runtime)
        {
            _miniRoomRuntime = runtime;
            _miniRoomRuntime?.BindMiniRoomHandlers(HandleMiniRoomReadyRequested, HandleMiniRoomStartRequested, HandleMiniRoomModeRequested);
            SyncMiniRoomRuntime();
        }

        public void OpenRoom(
            string title = "Match Cards",
            string playerOneName = "Player",
            string playerTwoName = "Opponent",
            int rows = DefaultRows,
            int columns = DefaultColumns,
            int localPlayerIndex = DefaultLocalPlayerIndex)
        {
            rows = Math.Max(2, rows);
            columns = Math.Max(2, columns);
            if ((rows * columns) % 2 != 0)
            {
                columns++;
            }

            _rows = rows;
            _columns = columns;
            _localPlayerIndex = Math.Clamp(localPlayerIndex, 0, 1);
            _playerNames[0] = string.IsNullOrWhiteSpace(playerOneName) ? "Player" : playerOneName.Trim();
            _playerNames[1] = string.IsNullOrWhiteSpace(playerTwoName) ? "Opponent" : playerTwoName.Trim();
            _title = string.IsNullOrWhiteSpace(title) ? "Match Cards" : title.Trim();

            ClearRoundState();
            _stage = RoomStage.Lobby;
            _statusMessage = "Ready the room, then start the board.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards room opened.");
            SyncMiniRoomRuntime();
        }

        public bool TrySetReady(int playerIndex, bool isReady, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }

            if (_stage == RoomStage.Playing)
            {
                message = "Ready state is locked while a round is in progress.";
                return false;
            }

            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }

            _readyStates[playerIndex] = isReady;
            _statusMessage = $"{_playerNames[playerIndex]} is {(isReady ? "ready" : "not ready")}.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage(_statusMessage);
            SyncMiniRoomRuntime();
            return true;
        }

        public bool TryStartGame(int tickCount, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }

            if (!_readyStates[0] || !_readyStates[1])
            {
                message = "Both players must be ready before the round can start.";
                return false;
            }

            InitializeBoard();
            _stage = RoomStage.Playing;
            _currentTurnIndex = 0;
            _pendingHideTick = 0;
            _lastWinnerIndex = -1;
            _turnDeadlineTick = tickCount + DefaultTurnSeconds * 1000;
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards round started.");
            SyncMiniRoomRuntime();
            return true;
        }

        public bool TryRevealCard(int cardIndex, int tickCount, out string message)
        {
            return TryRevealCard(cardIndex, tickCount, _localPlayerIndex, out message);
        }

        public bool TryRevealCard(int cardIndex, int tickCount, int playerIndex, out string message)
        {
            if (_stage != RoomStage.Playing)
            {
                message = "The board is not active.";
                return false;
            }

            if (_currentTurnIndex != playerIndex)
            {
                message = $"It is {_playerNames[_currentTurnIndex]}'s turn.";
                return false;
            }

            if (_pendingHideTick > 0)
            {
                message = "Wait for the previous mismatch to resolve.";
                return false;
            }

            if (cardIndex < 0 || cardIndex >= _cards.Count)
            {
                message = $"Invalid card index: {cardIndex}.";
                return false;
            }

            Card card = _cards[cardIndex];
            if (card.IsMatched || card.IsFaceUp)
            {
                message = "That card is already revealed.";
                return false;
            }

            card.IsFaceUp = true;
            _revealedCardIndices.Add(cardIndex);

            if (_revealedCardIndices.Count == 1)
            {
                _statusMessage = $"{_playerNames[_currentTurnIndex]} revealed card {cardIndex}.";
                message = _statusMessage;
                _miniRoomRuntime?.AddMiniRoomSpeakerMessage(_playerNames[_currentTurnIndex], $"turned up card {cardIndex}.", _currentTurnIndex == _localPlayerIndex);
                SyncMiniRoomRuntime();
                return true;
            }

            Card firstCard = _cards[_revealedCardIndices[0]];
            if (firstCard.FaceId == card.FaceId)
            {
                firstCard.IsMatched = true;
                card.IsMatched = true;
                _scores[_currentTurnIndex]++;
                _revealedCardIndices.Clear();
                _turnDeadlineTick = tickCount + DefaultTurnSeconds * 1000;

                if (AreAllCardsMatched())
                {
                    FinishRound(tickCount);
                }
                else
                {
                    _statusMessage = $"{_playerNames[_currentTurnIndex]} found a pair.";
                    _miniRoomRuntime?.AddMiniRoomSystemMessage(_statusMessage);
                    SyncMiniRoomRuntime();
                }

                message = _statusMessage;
                return true;
            }

            _pendingHideTick = tickCount + DefaultMismatchHideDelayMs;
            _statusMessage = "Mismatch. Cards will flip back.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Mismatch. Waiting for cards to flip back.");
            SyncMiniRoomRuntime();
            return true;
        }

        public bool TryClaimTie(out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }

            _stage = RoomStage.Result;
            _lastWinnerIndex = -1;
            _draws[0]++;
            _draws[1]++;
            _resultExpireTick = Environment.TickCount + DefaultResultSeconds * 1000;
            _statusMessage = "The room settled as a draw.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : The round ended in a draw.");
            SyncMiniRoomRuntime();
            return true;
        }

        public bool TryGiveUp(int playerIndex, out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Open a Memory Game room first.";
                return false;
            }

            if (!IsValidPlayerIndex(playerIndex))
            {
                message = $"Invalid player index: {playerIndex}.";
                return false;
            }

            int winnerIndex = playerIndex == 0 ? 1 : 0;
            _wins[winnerIndex]++;
            _losses[playerIndex]++;
            _stage = RoomStage.Result;
            _lastWinnerIndex = winnerIndex;
            _resultExpireTick = Environment.TickCount + DefaultResultSeconds * 1000;
            _statusMessage = $"{_playerNames[playerIndex]} gave up. {_playerNames[winnerIndex]} wins.";
            message = _statusMessage;
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_playerNames[playerIndex]} gave up.");
            SyncMiniRoomRuntime();
            return true;
        }

        public bool TryEndRoom(out string message)
        {
            if (_stage == RoomStage.Hidden)
            {
                message = "Memory Game room is already closed.";
                return false;
            }

            Reset();
            message = "Memory Game room closed.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards room closed.");
            return true;
        }

        public bool TryDispatchPacket(
            MemoryGamePacketType packetType,
            int tickCount,
            out string message,
            int playerIndex = DefaultLocalPlayerIndex,
            int cardIndex = -1,
            bool readyState = true,
            string playerOneName = null,
            string playerTwoName = null,
            int rows = DefaultRows,
            int columns = DefaultColumns,
            string title = "Match Cards")
        {
            _lastPacketType = packetType;
            _packetCounts.TryGetValue(packetType, out int count);
            _packetCounts[packetType] = count + 1;

            bool handled = packetType switch
            {
                MemoryGamePacketType.OpenRoom => TryDispatchOpenPacket(title, playerOneName, playerTwoName, rows, columns, playerIndex, out message),
                MemoryGamePacketType.SetReady => TrySetReady(playerIndex, readyState, out message),
                MemoryGamePacketType.StartGame => TryStartGame(tickCount, out message),
                MemoryGamePacketType.RevealCard => TryRevealCard(cardIndex, tickCount, playerIndex, out message),
                MemoryGamePacketType.ClaimTie => TryClaimTie(out message),
                MemoryGamePacketType.GiveUp => TryGiveUp(playerIndex, out message),
                MemoryGamePacketType.EndRoom => TryEndRoom(out message),
                MemoryGamePacketType.SelectMatchCardsMode => TrySelectMatchCardsMode(out message),
                _ => AssignUnsupportedPacket(packetType, out message)
            };

            _lastPacketSummary = $"{packetType}: {message}";
            return handled;
        }

        public bool TryDispatchMiniRoomPacket(byte[] packetBytes, int tickCount, out string message)
        {
            if (packetBytes == null || packetBytes.Length == 0)
            {
                message = "MiniRoom packet payload is empty.";
                return false;
            }

            EnsureRoomOpenFromMiniRoomRuntime();

            try
            {
                PacketReader reader = new(packetBytes);
                byte basePacketType = reader.ReadByte();
                return basePacketType switch
                {
                    MiniRoomBaseEnterPacketType => TryDispatchMiniRoomEnterPacket(reader, out message),
                    MiniRoomBaseGameplayPacketType => TryDispatchMiniRoomGameplayPacket(reader, tickCount, out message),
                    MiniRoomBaseChatPacketType => TryDispatchMiniRoomChatPacket(reader, out message),
                    MiniRoomBaseChatRepeatPacketType => TryDispatchMiniRoomChatPacket(reader, out message),
                    MiniRoomBaseAvatarPacketType => TryDispatchMiniRoomAvatarPacket(reader, out message),
                    MiniRoomBaseLeavePacketType => TryDispatchMiniRoomLeavePacket(reader, out message),
                    _ => FailMiniRoomPacket(basePacketType, out message)
                };
            }
            catch (EndOfStreamException)
            {
                message = $"MiniRoom packet ended unexpectedly: {BitConverter.ToString(packetBytes)}";
                return false;
            }
        }

        public static bool TryParseMiniRoomPacketHex(string text, out byte[] packetBytes, out string error)
        {
            packetBytes = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Provide at least one hex byte.";
                return false;
            }

            string[] tokens = text
                .Replace(",", " ", StringComparison.Ordinal)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<byte> bytes = new(tokens.Length);
            foreach (string token in tokens)
            {
                string normalized = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? token[2..]
                    : token;
                if (!byte.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, null, out byte value))
                {
                    error = $"Invalid hex byte: {token}.";
                    return false;
                }

                bytes.Add(value);
            }

            packetBytes = bytes.ToArray();
            error = string.Empty;
            return packetBytes.Length > 0;
        }

        public int GetPacketCount(MemoryGamePacketType packetType)
        {
            return _packetCounts.TryGetValue(packetType, out int count) ? count : 0;
        }

        public bool TryQueueRemoteAction(string action, int tickCount, out string message, int cardIndex = -1, int delayMs = DefaultRemoteActionDelayMs)
        {
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            int executeTick = tickCount + Math.Max(0, delayMs);
            switch (action)
            {
                case "ready":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Ready, executeTick, remotePlayerIndex, -1, true));
                    message = $"{_playerNames[remotePlayerIndex]} will ready in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "unready":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Ready, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will clear ready in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "start":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Start, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will request start in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "flip":
                    if (cardIndex < 0)
                    {
                        message = "Remote flip requires a card index.";
                        return false;
                    }

                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Reveal, executeTick, remotePlayerIndex, cardIndex, false));
                    message = $"{_playerNames[remotePlayerIndex]} will reveal card {cardIndex} in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "tie":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.Tie, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will request a tie in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "giveup":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.GiveUp, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will give up in {Math.Max(0, delayMs)} ms.";
                    return true;
                case "end":
                    _pendingRemoteActions.Enqueue(new PendingRemoteAction(RemoteActionType.End, executeTick, remotePlayerIndex, -1, false));
                    message = $"{_playerNames[remotePlayerIndex]} will close the room in {Math.Max(0, delayMs)} ms.";
                    return true;
                default:
                    message = "Usage: /memorygame remote <ready|unready|start|flip|tie|giveup|end> [...]";
                    return false;
            }
        }

        public void Update(int tickCount)
        {
            if (_stage == RoomStage.Playing)
            {
                if (_pendingHideTick > 0 && tickCount >= _pendingHideTick)
                {
                    ResolveMismatch();
                }

                if (_turnDeadlineTick > 0 && tickCount >= _turnDeadlineTick && _pendingHideTick <= 0)
                {
                    AdvanceTurn(tickCount);
                    _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
                    SyncMiniRoomRuntime();
                }
            }
            else if (_stage == RoomStage.Result && _resultExpireTick > 0 && tickCount >= _resultExpireTick)
            {
                ReturnToLobby();
            }

            ProcessRemoteActions(tickCount);
        }

        public bool HandleMouseClick(Point mousePosition, int viewportWidth, int viewportHeight, int tickCount, out string message)
        {
            message = null;
            if (_stage == RoomStage.Hidden)
            {
                return false;
            }

            GetLayout(viewportWidth, viewportHeight, out Rectangle outer, out Rectangle boardArea, out _, out Rectangle[] buttonRects);
            if (!outer.Contains(mousePosition))
            {
                return false;
            }

            for (int i = 0; i < buttonRects.Length; i++)
            {
                if (!buttonRects[i].Contains(mousePosition))
                {
                    continue;
                }

                switch (i)
                {
                    case 0:
                        return HandlePrimarySidebarAction(tickCount, out message);
                    case 1:
                        TryClaimTie(out message);
                        return true;
                    case 2:
                        TryGiveUp(_localPlayerIndex, out message);
                        return true;
                    case 3:
                        TryEndRoom(out message);
                        return true;
                    case 4:
                        message = "Ban is not modeled for the simulator MiniRoom.";
                        return true;
                }
            }

            int cardIndex = GetCardIndexAt(mousePosition, boardArea);
            if (cardIndex >= 0)
            {
                TryRevealCard(cardIndex, tickCount, out message);
                return true;
            }

            return true;
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            Texture2D pixelTexture,
            SpriteFont font = null)
        {
            if (_stage == RoomStage.Hidden || pixelTexture == null || font == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            EnsureAssetsLoaded();

            int dialogWidth = _backgroundTexture?.Width ?? ClientDialogWidth;
            int dialogHeight = _backgroundTexture?.Height ?? ClientDialogHeight;
            int dialogX = viewport.Width / 2 - dialogWidth / 2;
            int dialogY = Math.Max(24, viewport.Height / 2 - dialogHeight / 2);

            Rectangle outer = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);
            Rectangle boardArea = new Rectangle(dialogX + ClientBoardLeft, dialogY + ClientBoardTop, ClientBoardWidth, ClientBoardHeight);

            if (_backgroundTexture != null)
            {
                spriteBatch.Draw(_backgroundTexture, new Vector2(dialogX, dialogY), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, outer, new Color(20, 27, 41, 235));
            }

            if (_masterPanelTexture != null)
            {
                spriteBatch.Draw(_masterPanelTexture, new Vector2(dialogX + ClientMasterPanelX, dialogY + ClientMasterPanelY), Color.White);
            }

            DrawOutlinedText(spriteBatch, font, _title, new Vector2(dialogX + 407, dialogY + 19), Color.Black, Color.Black);
            DrawClientNamePanel(spriteBatch, pixelTexture, font, _playerNames[0], _scores[0], dialogX + ClientNameBarLeftX, dialogY + ClientNameBarY, _readyStates[0], _currentTurnIndex == 0, isLeftPanel: true);
            DrawClientNamePanel(spriteBatch, pixelTexture, font, _playerNames[1], _scores[1], dialogX + ClientNameBarRightX, dialogY + ClientNameBarY, _readyStates[1], _currentTurnIndex == 1, isLeftPanel: false);
            DrawBoard(spriteBatch, pixelTexture, font, boardArea);
            DrawClientTurnIndicator(spriteBatch, dialogX, dialogY);
            DrawClientButtons(spriteBatch, pixelTexture, font, dialogX, dialogY);
            DrawClientRecordSummary(spriteBatch, font, dialogX, dialogY);
            DrawOutlinedText(spriteBatch, font, $"{CurrentTurnTimeRemainingSeconds}s", new Vector2(dialogX + ClientTimerTextX, dialogY + ClientTimerTextY), Color.Black, new Color(48, 48, 48));
            DrawOutlinedText(spriteBatch, font, _statusMessage, new Vector2(dialogX + 407, dialogY + 320), Color.Black, new Color(72, 52, 24));
        }

        public string DescribeStatus()
        {
            string playerOneName = string.IsNullOrWhiteSpace(_playerNames[0]) ? "Player" : _playerNames[0];
            string playerTwoName = string.IsNullOrWhiteSpace(_playerNames[1]) ? "Opponent" : _playerNames[1];
            return $"{_title}: stage={_stage}, turn={_currentTurnIndex}, ready=[{_readyStates[0]},{_readyStates[1]}], score={_scores[0]}-{_scores[1]}, players={playerOneName}/{playerTwoName}, cards={_cards.Count}, pendingHide={_pendingHideTick > 0}, lastPacket={_lastPacketType?.ToString() ?? "None"}";
        }

        public void Reset()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _readyStates[0] = false;
            _readyStates[1] = false;
            _playerNames[0] = "Player";
            _playerNames[1] = "Opponent";
            _rows = 0;
            _columns = 0;
            _localPlayerIndex = DefaultLocalPlayerIndex;
            _currentTurnIndex = 0;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = 0;
            _lastWinnerIndex = -1;
            _title = "Match Cards";
            _statusMessage = "Open a MiniRoom to begin.";
            _stage = RoomStage.Hidden;
            _pendingRemoteActions.Clear();
            _miniRoomParticipants.Clear();
            _lastPacketType = null;
            _lastPacketSummary = "Memory Game room reset.";
            _packetCounts.Clear();
            SyncMiniRoomRuntime();
        }

        private void InitializeBoard()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _pendingHideTick = 0;
            _resultExpireTick = 0;
            _pendingRemoteActions.Clear();
            _waitingForTimeOverPacket = false;

            int pairCount = (_rows * _columns) / 2;
            List<int> faceIds = new(pairCount * 2);
            for (int i = 0; i < pairCount; i++)
            {
                faceIds.Add(i);
                faceIds.Add(i);
            }

            Random random = new((_rows * 397) ^ (_columns * 211) ^ _title.GetHashCode(StringComparison.Ordinal));
            for (int i = faceIds.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (faceIds[i], faceIds[swapIndex]) = (faceIds[swapIndex], faceIds[i]);
            }

            for (int i = 0; i < faceIds.Count; i++)
            {
                _cards.Add(new Card
                {
                    FaceId = faceIds[i],
                    IsFaceUp = false,
                    IsMatched = false
                });
            }
        }

        private void ResolveMismatch()
        {
            for (int i = 0; i < _revealedCardIndices.Count; i++)
            {
                _cards[_revealedCardIndices[i]].IsFaceUp = false;
            }

            _revealedCardIndices.Clear();
            _pendingHideTick = 0;
            AdvanceTurn(Environment.TickCount);
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Turn passed after the mismatch.");
            SyncMiniRoomRuntime();
        }

        private void AdvanceTurn(int tickCount)
        {
            _currentTurnIndex = _currentTurnIndex == 0 ? 1 : 0;
            _turnDeadlineTick = tickCount + DefaultTurnSeconds * 1000;
        }

        private bool AreAllCardsMatched()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (!_cards[i].IsMatched)
                {
                    return false;
                }
            }

            return _cards.Count > 0;
        }

        private void FinishRound(int tickCount)
        {
            _stage = RoomStage.Result;
            _resultExpireTick = tickCount + DefaultResultSeconds * 1000;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;

            if (_scores[0] == _scores[1])
            {
                _lastWinnerIndex = -1;
                _draws[0]++;
                _draws[1]++;
                _statusMessage = "Round complete. Draw.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Match Cards round complete. Draw.");
                SyncMiniRoomRuntime();
                return;
            }

            int winnerIndex = _scores[0] > _scores[1] ? 0 : 1;
            int loserIndex = winnerIndex == 0 ? 1 : 0;
            _lastWinnerIndex = winnerIndex;
            _wins[winnerIndex]++;
            _losses[loserIndex]++;
            _statusMessage = $"Round complete. {_playerNames[winnerIndex]} wins.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_playerNames[winnerIndex]} won the round.");
            SyncMiniRoomRuntime();
        }

        private void ReturnToLobby()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = 0;
            _lastWinnerIndex = -1;
            _stage = RoomStage.Lobby;
            _statusMessage = "Ready the room, then start the board.";
            _waitingForTimeOverPacket = false;
            SyncMiniRoomRuntime();
        }

        private void ClearRoundState()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _readyStates[0] = false;
            _readyStates[1] = false;
            _currentTurnIndex = 0;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = 0;
            _lastWinnerIndex = -1;
            _pendingRemoteActions.Clear();
            _waitingForTimeOverPacket = false;
        }

        private bool TryDispatchMiniRoomEnterPacket(PacketReader reader, out string message)
        {
            int slot = reader.ReadByte();
            if (!TryDecodeMiniRoomParticipant(reader, slot, out MiniRoomParticipantState participant, out message))
            {
                return false;
            }

            string seatDescription = slot < 2 ? ResolveSeatLabel(slot) : $"Visitor seat {slot}";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {participant.Name} entered the Match Cards room ({seatDescription}).");
            SyncMiniRoomRuntime();
            message = $"{participant.Name} entered MiniRoom slot {slot} with job {participant.JobCode}.";
            return true;
        }

        private bool TryDispatchMiniRoomChatPacket(PacketReader reader, out string message)
        {
            byte chatType = reader.ReadByte();
            if (chatType == MiniRoomChatGameMessageType)
            {
                int gameMessageCode = reader.ReadByte();
                string characterName = reader.ReadMapleString();
                string gameMessage = $"Game message code {gameMessageCode} for {characterName}.";
                _statusMessage = gameMessage;
                _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {gameMessage}");
                SyncMiniRoomRuntime();
                message = gameMessage;
                return true;
            }

            int speakerSlot = chatType;
            string rawText = reader.ReadMapleString();
            string speakerName = ResolveParticipantName(speakerSlot);
            string normalizedText = NormalizeMiniRoomChatText(rawText, ref speakerName);
            bool isLocalSpeaker = speakerSlot == _localPlayerIndex;
            _miniRoomRuntime?.AddMiniRoomSpeakerMessage(speakerName, normalizedText, isLocalSpeaker);
            _statusMessage = $"{speakerName} said: {normalizedText}";
            SyncMiniRoomRuntime();
            message = $"MiniRoom chat from slot {speakerSlot}: {speakerName} : {normalizedText}";
            return true;
        }

        private bool TryDispatchMiniRoomAvatarPacket(PacketReader reader, out string message)
        {
            int slot = reader.ReadByte();
            if (!TryDecodeMiniRoomAvatar(reader, slot, out MiniRoomParticipantState participant, out message))
            {
                return false;
            }

            string participantName = ResolveParticipantName(slot);
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {participantName} updated their MiniRoom avatar.");
            SyncMiniRoomRuntime();
            message = $"Updated MiniRoom avatar for slot {slot}: {participantName}.";
            return true;
        }

        private bool TryDispatchMiniRoomGameplayPacket(PacketReader reader, int tickCount, out string message)
        {
            byte packetType = reader.ReadByte();
            switch (packetType)
            {
                case MemoryGameReadyPacketType:
                    return TryApplyRemoteReadyPacket(isReady: true, out message);
                case MemoryGameCancelReadyPacketType:
                    return TryApplyRemoteReadyPacket(isReady: false, out message);
                case MemoryGameStartPacketType:
                    return TryApplyStartPacket(reader, tickCount, out message);
                case MemoryGameTurnUpCardPacketType:
                    return TryApplyTurnUpCardPacket(reader, tickCount, out message);
                case MemoryGameTimeOverPacketType:
                    return TryApplyTimeOverPacket(reader, tickCount, out message);
                case MemoryGameTieRequestPacketType:
                    message = $"{ResolveRemotePlayerName()} requested a tie.";
                    _statusMessage = message;
                    _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {message}");
                    SyncMiniRoomRuntime();
                    return true;
                case MemoryGameTieResultPacketType:
                    return TryClaimTie(out message);
                case MemoryGameGameResultPacketType:
                    return TryApplyGameResultPacket(reader, tickCount, out message);
                default:
                    message = $"MiniRoom gameplay packet {packetType} is not modeled for Match Cards.";
                    return false;
            }
        }

        private bool TryDispatchMiniRoomLeavePacket(PacketReader reader, out string message)
        {
            int playerIndex = reader.ReadByte();
            if (playerIndex < 0)
            {
                message = $"MiniRoom leave packet used invalid player index {playerIndex}.";
                return false;
            }

            string playerName = ResolveParticipantName(playerIndex);
            _miniRoomParticipants.Remove(playerIndex);
            if (IsValidPlayerIndex(playerIndex) && (_stage == RoomStage.Playing || _stage == RoomStage.Result || _stage == RoomStage.Lobby))
            {
                Reset();
            }
            else
            {
                _statusMessage = $"{playerName} left the Match Cards room.";
                SyncMiniRoomRuntime();
            }

            message = $"{playerName} left the Match Cards room.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {message}", isWarning: true);
            _statusMessage = message;
            SyncMiniRoomRuntime();
            return true;
        }

        private bool TryApplyRemoteReadyPacket(bool isReady, out string message)
        {
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            bool handled = TrySetReady(remotePlayerIndex, isReady, out message);
            if (handled)
            {
                _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {ResolveRemotePlayerName()} {(isReady ? "is ready." : "canceled ready.")}");
            }

            return handled;
        }

        private bool TryApplyStartPacket(PacketReader reader, int tickCount, out string message)
        {
            int currentTurnIndex = reader.ReadByte();
            int cardCount = reader.ReadByte();
            if (cardCount <= 0)
            {
                message = "Memory Game start packet did not include a card count.";
                return false;
            }

            List<int> shuffle = new(cardCount);
            for (int i = 0; i < cardCount; i++)
            {
                shuffle.Add(reader.ReadInt());
            }

            ResolveBoardDimensions(cardCount, out _rows, out _columns);
            InitializeBoardFromPacket(shuffle);
            _stage = RoomStage.Playing;
            _currentTurnIndex = Math.Clamp(currentTurnIndex, 0, _playerNames.Length - 1);
            _turnDeadlineTick = tickCount + (200 * cardCount) + 11500;
            _resultExpireTick = 0;
            _readyStates[0] = false;
            _readyStates[1] = false;
            _waitingForTimeOverPacket = false;
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Start packet applied from MiniRoom payload.");
            SyncMiniRoomRuntime();
            message = $"Applied start packet for {cardCount} cards. {_statusMessage}";
            return true;
        }

        private bool TryApplyTurnUpCardPacket(PacketReader reader, int tickCount, out string message)
        {
            int firstRevealFlag = reader.ReadByte();
            int cardIndex = reader.ReadByte();
            if (!TryEnsureCardIndex(cardIndex, out message))
            {
                return false;
            }

            if (firstRevealFlag != 0)
            {
                _cards[cardIndex].IsFaceUp = true;
                _revealedCardIndices.Clear();
                _revealedCardIndices.Add(cardIndex);
                _statusMessage = $"{_playerNames[_currentTurnIndex]} revealed card {cardIndex}.";
                _miniRoomRuntime?.AddMiniRoomSpeakerMessage(_playerNames[_currentTurnIndex], $"turned up card {cardIndex}.", _currentTurnIndex == _localPlayerIndex);
                SyncMiniRoomRuntime();
                message = _statusMessage;
                return true;
            }

            int pairedCardIndex = reader.ReadByte();
            int resultOwner = reader.ReadByte();
            if (!TryEnsureCardIndex(pairedCardIndex, out message))
            {
                return false;
            }

            _cards[cardIndex].IsFaceUp = true;
            _cards[pairedCardIndex].IsFaceUp = true;
            _revealedCardIndices.Clear();
            _revealedCardIndices.Add(pairedCardIndex);
            _revealedCardIndices.Add(cardIndex);

            if (resultOwner < _playerNames.Length)
            {
                _currentTurnIndex = resultOwner;
                _turnDeadlineTick = tickCount + 11600;
                _waitingForTimeOverPacket = true;
                _statusMessage = $"Mismatch pending. {_playerNames[_currentTurnIndex]} takes the next turn after flip-back.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Packet mismatch received. Waiting for time-over flip-back.");
            }
            else
            {
                int scoringPlayerIndex = resultOwner - _playerNames.Length;
                if (!IsValidPlayerIndex(scoringPlayerIndex))
                {
                    message = $"Turn-up packet used invalid scoring owner {resultOwner}.";
                    return false;
                }

                _cards[cardIndex].IsMatched = true;
                _cards[pairedCardIndex].IsMatched = true;
                _scores[scoringPlayerIndex]++;
                _currentTurnIndex = scoringPlayerIndex;
                _turnDeadlineTick = tickCount + 10000;
                _revealedCardIndices.Clear();
                _waitingForTimeOverPacket = false;
                _statusMessage = $"{_playerNames[scoringPlayerIndex]} found a pair.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : {_playerNames[scoringPlayerIndex]} matched cards {pairedCardIndex} and {cardIndex}.");
            }

            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }

        private bool TryApplyTimeOverPacket(PacketReader reader, int tickCount, out string message)
        {
            int currentTurnIndex = reader.ReadByte();
            if (_revealedCardIndices.Count > 0)
            {
                foreach (int revealedIndex in _revealedCardIndices)
                {
                    if (revealedIndex >= 0 && revealedIndex < _cards.Count && !_cards[revealedIndex].IsMatched)
                    {
                        _cards[revealedIndex].IsFaceUp = false;
                    }
                }

                _revealedCardIndices.Clear();
            }

            _currentTurnIndex = Math.Clamp(currentTurnIndex, 0, _playerNames.Length - 1);
            _turnDeadlineTick = tickCount + 10000;
            _waitingForTimeOverPacket = false;
            _statusMessage = $"{_playerNames[_currentTurnIndex]}'s turn.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Time-over packet returned the board to the next turn.");
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }

        private bool TryApplyGameResultPacket(PacketReader reader, int tickCount, out string message)
        {
            int resultType = reader.ReadByte();
            _stage = RoomStage.Result;
            _pendingHideTick = 0;
            _turnDeadlineTick = 0;
            _resultExpireTick = tickCount + DefaultResultSeconds * 1000;
            _waitingForTimeOverPacket = false;

            if (resultType == 1)
            {
                _lastWinnerIndex = -1;
                _draws[0]++;
                _draws[1]++;
                _statusMessage = "Round complete. Draw.";
                _miniRoomRuntime?.AddMiniRoomSystemMessage("System : Game-result packet ended the round in a draw.");
                SyncMiniRoomRuntime();
                message = _statusMessage;
                return true;
            }

            int winnerIndex = reader.ReadByte();
            if (!IsValidPlayerIndex(winnerIndex))
            {
                message = $"Game-result packet used invalid winner index {winnerIndex}.";
                return false;
            }

            int loserIndex = winnerIndex == 0 ? 1 : 0;
            _lastWinnerIndex = winnerIndex;
            _wins[winnerIndex]++;
            _losses[loserIndex]++;
            _statusMessage = $"Round complete. {_playerNames[winnerIndex]} wins.";
            _miniRoomRuntime?.AddMiniRoomSystemMessage($"System : Game-result packet declared {_playerNames[winnerIndex]} the winner.");
            SyncMiniRoomRuntime();
            message = _statusMessage;
            return true;
        }

        private void InitializeBoardFromPacket(IReadOnlyList<int> shuffle)
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _pendingHideTick = 0;
            _resultExpireTick = 0;
            _pendingRemoteActions.Clear();

            for (int i = 0; i < shuffle.Count; i++)
            {
                _cards.Add(new Card
                {
                    FaceId = Math.Max(0, shuffle[i]),
                    IsFaceUp = false,
                    IsMatched = false
                });
            }
        }

        private static void ResolveBoardDimensions(int cardCount, out int rows, out int columns)
        {
            rows = 2;
            columns = Math.Max(2, cardCount / 2);
            int bestDifference = int.MaxValue;
            for (int candidateRows = 2; candidateRows <= cardCount; candidateRows++)
            {
                if (cardCount % candidateRows != 0)
                {
                    continue;
                }

                int candidateColumns = cardCount / candidateRows;
                int difference = Math.Abs(candidateColumns - candidateRows);
                if (difference < bestDifference)
                {
                    bestDifference = difference;
                    rows = Math.Min(candidateRows, candidateColumns);
                    columns = Math.Max(candidateRows, candidateColumns);
                }
            }
        }

        private bool TryEnsureCardIndex(int cardIndex, out string message)
        {
            if (cardIndex < 0 || cardIndex >= _cards.Count)
            {
                message = $"Invalid card index: {cardIndex}.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private string ResolveRemotePlayerName()
        {
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            return string.IsNullOrWhiteSpace(_playerNames[remotePlayerIndex]) ? "Opponent" : _playerNames[remotePlayerIndex];
        }

        private static bool FailMiniRoomPacket(byte basePacketType, out string message)
        {
            message = $"MiniRoom base packet {basePacketType} is not modeled for Match Cards.";
            return false;
        }

        private void DrawBoard(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle area)
        {
            if (_cards.Count == 0)
            {
                DrawOutlinedText(spriteBatch, font, "No board yet", new Vector2(area.X + 96, area.Y + 124), Color.Black, Color.Black);
                return;
            }

            int gapX = _columns <= 0 ? 0 : Math.Max(6, (area.Width - (_columns * 49)) / (_columns + 1));
            int gapY = _rows <= 0 ? 0 : Math.Max(8, (area.Height - (_rows * 62)) / (_rows + 1));

            for (int index = 0; index < _cards.Count; index++)
            {
                int row = index / _columns;
                int column = index % _columns;
                Rectangle cardRect = new Rectangle(
                    area.X + gapX + column * (49 + gapX),
                    area.Y + gapY + row * (62 + gapY),
                    49,
                    62);

                Card card = _cards[index];
                Texture2D cardTexture = ResolveCardTexture(card);
                if (cardTexture != null)
                {
                    spriteBatch.Draw(cardTexture, new Vector2(cardRect.X, cardRect.Y), card.IsMatched ? Color.White * 0.82f : Color.White);
                    continue;
                }

                Color cardColor = card.IsMatched
                    ? new Color(111, 162, 85)
                    : card.IsFaceUp
                        ? new Color(246, 224, 167)
                        : new Color(145, 82, 42);

                spriteBatch.Draw(pixel, cardRect, cardColor);
            }

            if (_stage == RoomStage.Result)
            {
                Texture2D resultTexture = _lastWinnerIndex switch
                {
                    0 when _localPlayerIndex == 0 => _winTexture,
                    1 when _localPlayerIndex == 1 => _winTexture,
                    0 or 1 => _loseTexture,
                    _ => _drawTexture
                };

                if (resultTexture != null)
                {
                    Vector2 resultPosition = new(
                        area.Center.X - (resultTexture.Width / 2f),
                        area.Center.Y - (resultTexture.Height / 2f));
                    spriteBatch.Draw(resultTexture, resultPosition, Color.White);
                }
            }
        }

        private void HandleMiniRoomReadyRequested()
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            TryDispatchPacket(MemoryGamePacketType.SetReady, Environment.TickCount, out _, remotePlayerIndex, readyState: !_readyStates[remotePlayerIndex]);
        }

        private void HandleMiniRoomStartRequested()
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            TryDispatchPacket(MemoryGamePacketType.SetReady, Environment.TickCount, out _, _localPlayerIndex, readyState: true);
            int remotePlayerIndex = _localPlayerIndex == 0 ? 1 : 0;
            TryDispatchPacket(MemoryGamePacketType.SetReady, Environment.TickCount, out _, remotePlayerIndex, readyState: true);
            TryDispatchPacket(MemoryGamePacketType.StartGame, Environment.TickCount, out _);
        }

        private void HandleMiniRoomModeRequested()
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            TryDispatchPacket(MemoryGamePacketType.SelectMatchCardsMode, Environment.TickCount, out _);
        }

        private void EnsureRoomOpenFromMiniRoomRuntime()
        {
            if (_stage != RoomStage.Hidden)
            {
                return;
            }

            string ownerName = _miniRoomRuntime?.Occupants.Count > 0 ? _miniRoomRuntime.Occupants[0].Name : "Player";
            string guestName = _miniRoomRuntime?.Occupants.Count > 1 ? _miniRoomRuntime.Occupants[1].Name : "Opponent";
            string title = _miniRoomRuntime?.RoomTitle ?? "Match Cards";
            OpenRoom(title, ownerName, guestName, DefaultRows, DefaultColumns, DefaultLocalPlayerIndex);
        }

        private void ProcessRemoteActions(int tickCount)
        {
            while (_pendingRemoteActions.Count > 0 && tickCount >= _pendingRemoteActions.Peek().ExecuteTick)
            {
                PendingRemoteAction action = _pendingRemoteActions.Dequeue();
                MemoryGamePacketType packetType = action.ActionType switch
                {
                    RemoteActionType.Ready => MemoryGamePacketType.SetReady,
                    RemoteActionType.Start => MemoryGamePacketType.StartGame,
                    RemoteActionType.Reveal => MemoryGamePacketType.RevealCard,
                    RemoteActionType.Tie => MemoryGamePacketType.ClaimTie,
                    RemoteActionType.GiveUp => MemoryGamePacketType.GiveUp,
                    RemoteActionType.End => MemoryGamePacketType.EndRoom,
                    _ => MemoryGamePacketType.SelectMatchCardsMode
                };

                TryDispatchPacket(packetType, tickCount, out _, action.PlayerIndex, action.CardIndex, action.ReadyState);
            }
        }

        private void SyncMiniRoomRuntime()
        {
            if (_miniRoomRuntime == null)
            {
                return;
            }

            string roomState = _stage switch
            {
                RoomStage.Hidden => "Board closed",
                RoomStage.Lobby => "Waiting for ready check",
                RoomStage.Playing => $"{_playerNames[_currentTurnIndex]}'s turn ({CurrentTurnTimeRemainingSeconds}s)",
                RoomStage.Result => _lastWinnerIndex >= 0 ? $"{_playerNames[_lastWinnerIndex]} won the round" : "Round ended in a draw",
                _ => string.Empty
            };

            List<SocialRoomOccupant> extraOccupants = BuildMiniRoomExtraOccupants();

            _miniRoomRuntime.SyncMiniRoomMatchCards(
                _title,
                ResolvePrimaryParticipantName(0),
                ResolvePrimaryParticipantName(1),
                _readyStates[0],
                _readyStates[1],
                _scores[0],
                _scores[1],
                _currentTurnIndex,
                _statusMessage,
                roomState,
                BuildParticipantDetail(0, includeScore: true),
                BuildParticipantDetail(1, includeScore: true),
                extraOccupants);
        }

        private List<SocialRoomOccupant> BuildMiniRoomExtraOccupants()
        {
            List<SocialRoomOccupant> occupants = new();
            foreach (KeyValuePair<int, MiniRoomParticipantState> entry in _miniRoomParticipants.OrderBy(entry => entry.Key))
            {
                int slot = entry.Key;
                if (slot < 2)
                {
                    continue;
                }

                occupants.Add(new SocialRoomOccupant(
                    ResolveParticipantName(slot),
                    SocialRoomOccupantRole.Visitor,
                    BuildParticipantDetail(slot, includeScore: false),
                    isReady: false));
            }

            return occupants;
        }

        private bool TryDecodeMiniRoomParticipant(PacketReader reader, int slot, out MiniRoomParticipantState participant, out string message)
        {
            participant = null;
            if (slot < 0)
            {
                message = $"MiniRoom enter packet used invalid slot {slot}.";
                return false;
            }

            if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook avatarLook, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            string name = reader.ReadMapleString();
            short jobCode = reader.ReadShort();
            participant = UpsertMiniRoomParticipant(slot, name, jobCode, avatarLook);
            message = string.Empty;
            return true;
        }

        private bool TryDecodeMiniRoomAvatar(PacketReader reader, int slot, out MiniRoomParticipantState participant, out string message)
        {
            participant = null;
            if (slot < 0)
            {
                message = $"MiniRoom avatar packet used invalid slot {slot}.";
                return false;
            }

            if (!LoginAvatarLookCodec.TryDecode(reader, out LoginAvatarLook avatarLook, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            participant = UpsertMiniRoomParticipant(slot, null, null, avatarLook);
            message = string.Empty;
            return true;
        }

        private MiniRoomParticipantState UpsertMiniRoomParticipant(int slot, string name, short? jobCode, LoginAvatarLook avatarLook)
        {
            if (!_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant))
            {
                participant = new MiniRoomParticipantState(slot);
                _miniRoomParticipants[slot] = participant;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                participant.Name = name.Trim();
                if (slot < _playerNames.Length)
                {
                    _playerNames[slot] = participant.Name;
                }
            }

            if (jobCode.HasValue)
            {
                participant.JobCode = jobCode.Value;
            }

            if (avatarLook != null)
            {
                participant.AvatarLook = avatarLook;
            }

            return participant;
        }

        private string ResolvePrimaryParticipantName(int slot)
        {
            if (_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant)
                && !string.IsNullOrWhiteSpace(participant.Name))
            {
                return participant.Name;
            }

            return _playerNames[slot];
        }

        private string ResolveParticipantName(int slot)
        {
            if (_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant)
                && !string.IsNullOrWhiteSpace(participant.Name))
            {
                return participant.Name;
            }

            if (slot >= 0 && slot < _playerNames.Length && !string.IsNullOrWhiteSpace(_playerNames[slot]))
            {
                return _playerNames[slot];
            }

            return $"Seat {slot}";
        }

        private string BuildParticipantDetail(int slot, bool includeScore)
        {
            List<string> detailParts = new() { ResolveSeatLabel(slot) };
            if (includeScore && slot >= 0 && slot < _scores.Length)
            {
                detailParts.Add($"Score {_scores[slot]}");
                if (_currentTurnIndex == slot && _stage == RoomStage.Playing)
                {
                    detailParts.Add("Current turn");
                }
            }

            if (_miniRoomParticipants.TryGetValue(slot, out MiniRoomParticipantState participant))
            {
                if (participant.JobCode > 0)
                {
                    detailParts.Add($"Job {participant.JobCode}");
                }

                if (participant.AvatarLook != null)
                {
                    detailParts.Add($"Face {participant.AvatarLook.FaceId}");
                    detailParts.Add($"Hair {participant.AvatarLook.HairId}");
                }
            }

            return string.Join(" | ", detailParts);
        }

        private static string ResolveSeatLabel(int slot)
        {
            return slot switch
            {
                0 => "Host seat",
                1 => "Guest seat",
                _ => $"Visitor seat {slot}"
            };
        }

        private static string NormalizeMiniRoomChatText(string rawText, ref string speakerName)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return string.Empty;
            }

            int separatorIndex = rawText.IndexOf(" : ", StringComparison.Ordinal);
            if (separatorIndex > 0)
            {
                string parsedSpeaker = rawText[..separatorIndex].Trim();
                string parsedMessage = rawText[(separatorIndex + 3)..].Trim();
                if (!string.IsNullOrWhiteSpace(parsedSpeaker))
                {
                    speakerName = parsedSpeaker;
                }

                if (!string.IsNullOrWhiteSpace(parsedMessage))
                {
                    return parsedMessage;
                }
            }

            return rawText.Trim();
        }

        private bool HandlePrimarySidebarAction(int tickCount, out string message)
        {
            if (_stage == RoomStage.Lobby && !_readyStates[_localPlayerIndex])
            {
                TryDispatchPacket(MemoryGamePacketType.SetReady, tickCount, out message, _localPlayerIndex, readyState: true);
                return true;
            }

            if (_stage == RoomStage.Lobby)
            {
                TryDispatchPacket(MemoryGamePacketType.StartGame, tickCount, out message);
                return true;
            }

            message = "The primary Memory Game button is only available from the lobby.";
            return true;
        }

        private string GetPrimaryButtonLabel()
        {
            if (_stage == RoomStage.Lobby && !_readyStates[_localPlayerIndex])
            {
                return "Ready";
            }

            return "Start";
        }

        private void GetLayout(int viewportWidth, int viewportHeight, out Rectangle outer, out Rectangle boardArea, out Rectangle sidebar, out Rectangle[] buttonRects)
        {
            int dialogWidth = _backgroundTexture?.Width ?? ClientDialogWidth;
            int dialogHeight = _backgroundTexture?.Height ?? ClientDialogHeight;
            int dialogX = viewportWidth / 2 - dialogWidth / 2;
            int dialogY = Math.Max(24, viewportHeight / 2 - dialogHeight / 2);
            outer = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);
            boardArea = new Rectangle(dialogX + ClientBoardLeft, dialogY + ClientBoardTop, ClientBoardWidth, ClientBoardHeight);
            sidebar = new Rectangle(dialogX + ClientRecordPanelX, dialogY + ClientRecordPanelY, 300, 132);
            buttonRects = new Rectangle[5];
            buttonRects[0] = CreateButtonRect(dialogX + ClientReadyButtonX, dialogY + ClientReadyButtonY, _readyStates[_localPlayerIndex] ? _startButtonTexture : _readyButtonTexture, 96, 29);
            buttonRects[1] = CreateButtonRect(dialogX + ClientTieButtonX, dialogY + ClientTieButtonY, _tieButtonTexture, 43, 18);
            buttonRects[2] = CreateButtonRect(dialogX + ClientGiveUpButtonX, dialogY + ClientGiveUpButtonY, _giveUpButtonTexture, 43, 18);
            buttonRects[3] = CreateButtonRect(dialogX + ClientEndButtonX, dialogY + ClientEndButtonY, _endButtonTexture, 43, 18);
            buttonRects[4] = CreateButtonRect(dialogX + ClientBanButtonX, dialogY + ClientBanButtonY, _banButtonTexture, 11, 11);
        }

        private int GetCardIndexAt(Point mousePosition, Rectangle area)
        {
            if (_cards.Count == 0 || !area.Contains(mousePosition))
            {
                return -1;
            }

            int gapX = _columns <= 0 ? 0 : Math.Max(6, (area.Width - (_columns * 49)) / (_columns + 1));
            int gapY = _rows <= 0 ? 0 : Math.Max(8, (area.Height - (_rows * 62)) / (_rows + 1));

            for (int index = 0; index < _cards.Count; index++)
            {
                int row = index / _columns;
                int column = index % _columns;
                Rectangle cardRect = new Rectangle(
                    area.X + gapX + column * (49 + gapX),
                    area.Y + gapY + row * (62 + gapY),
                    49,
                    62);
                if (cardRect.Contains(mousePosition))
                {
                    return index;
                }
            }

            return -1;
        }

        private void DrawNameBar(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, string name, int score, int x, int y, bool isActiveTurn)
        {
            Rectangle rect = new Rectangle(x, y, 174, 28);
            spriteBatch.Draw(pixel, rect, isActiveTurn ? new Color(223, 196, 120) : new Color(132, 103, 73));
            DrawOutlinedText(spriteBatch, font, name, new Vector2(x + 8, y + 5), Color.Black, Color.White);
            DrawOutlinedText(spriteBatch, font, score.ToString(), new Vector2(x + 146, y + 5), Color.Black, Color.White);
        }

        private void DrawClientNamePanel(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, string name, int score, int x, int y, bool isReady, bool isActiveTurn, bool isLeftPanel)
        {
            DrawNameBar(spriteBatch, pixel, font, name, score, x, y, isActiveTurn);

            Texture2D readyTexture = isReady ? _readyOnTexture : _readyOffTexture;
            if (readyTexture != null)
            {
                int readyX = isLeftPanel ? ClientReadyIndicatorLeftX : ClientReadyIndicatorRightX;
                spriteBatch.Draw(readyTexture, new Vector2(x - ClientNameBarLeftX + readyX, y - ClientNameBarY + ClientReadyIndicatorY), Color.White);
            }
        }

        private void DrawClientButtons(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, int dialogX, int dialogY)
        {
            DrawButton(spriteBatch, pixel, font, dialogX + ClientReadyButtonX, dialogY + ClientReadyButtonY, _readyStates[_localPlayerIndex] ? _startButtonTexture : _readyButtonTexture, GetPrimaryButtonLabel());
            DrawButton(spriteBatch, pixel, font, dialogX + ClientTieButtonX, dialogY + ClientTieButtonY, _tieButtonTexture, "Tie");
            DrawButton(spriteBatch, pixel, font, dialogX + ClientGiveUpButtonX, dialogY + ClientGiveUpButtonY, _giveUpButtonTexture, "Give Up");
            DrawButton(spriteBatch, pixel, font, dialogX + ClientEndButtonX, dialogY + ClientEndButtonY, _endButtonTexture, "End");
            DrawButton(spriteBatch, pixel, font, dialogX + ClientBanButtonX, dialogY + ClientBanButtonY, _banButtonTexture, string.Empty);
        }

        private void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, int x, int y, Texture2D texture, string label)
        {
            if (texture != null)
            {
                spriteBatch.Draw(texture, new Vector2(x, y), Color.White);
            }
            else
            {
                spriteBatch.Draw(pixel, new Rectangle(x, y, 64, 22), new Color(119, 84, 48));
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                DrawOutlinedText(spriteBatch, font, label, new Vector2(x + 8, y + 6), Color.Black, Color.White);
            }
        }

        private void DrawClientRecordSummary(SpriteBatch spriteBatch, SpriteFont font, int dialogX, int dialogY)
        {
            int localIndex = Math.Clamp(_localPlayerIndex, 0, _wins.Length - 1);
            DrawBitmapNumber(spriteBatch, _scores[0], dialogX + ClientScoreLeftX, dialogY + ClientScoreY);
            DrawBitmapNumber(spriteBatch, _scores[1], dialogX + ClientScoreRightX, dialogY + ClientScoreY);
            DrawOutlinedText(spriteBatch, font, $"W {_wins[localIndex]}  L {_losses[localIndex]}  D {_draws[localIndex]}", new Vector2(dialogX + 409, dialogY + 210), Color.Black, new Color(48, 48, 48));
            DrawOutlinedText(spriteBatch, font, $"Packet: {_lastPacketType?.ToString() ?? "None"}", new Vector2(dialogX + 409, dialogY + 228), Color.Black, new Color(48, 48, 48));
            DrawOutlinedText(spriteBatch, font, $"Room: {_stage}", new Vector2(dialogX + 409, dialogY + 246), Color.Black, new Color(48, 48, 48));
        }

        private void DrawBitmapNumber(SpriteBatch spriteBatch, int value, int x, int y)
        {
            string scoreText = Math.Clamp(value, 0, 99).ToString("00");
            foreach (char digit in scoreText)
            {
                int index = digit - '0';
                Texture2D texture = index >= 0 && index < _digitTextures.Length ? _digitTextures[index] : null;
                if (texture == null)
                {
                    return;
                }

                spriteBatch.Draw(texture, new Vector2(x, y), Color.White);
                x += texture.Width - 1;
            }
        }

        private void DrawClientTurnIndicator(SpriteBatch spriteBatch, int dialogX, int dialogY)
        {
            if (_turnTexture == null || _stage != RoomStage.Playing)
            {
                return;
            }

            spriteBatch.Draw(_turnTexture, new Vector2(dialogX + ResolveTurnIndicatorX(), dialogY + ClientTurnIndicatorY), Color.White);
        }

        public static bool TryParsePacketType(string text, out MemoryGamePacketType packetType)
        {
            packetType = MemoryGamePacketType.OpenRoom;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim().Replace("-", string.Empty).Replace("_", string.Empty);
            return normalized.ToLowerInvariant() switch
            {
                "open" => AssignPacket(MemoryGamePacketType.OpenRoom, out packetType),
                "ready" or "unready" => AssignPacket(MemoryGamePacketType.SetReady, out packetType),
                "start" => AssignPacket(MemoryGamePacketType.StartGame, out packetType),
                "flip" or "reveal" => AssignPacket(MemoryGamePacketType.RevealCard, out packetType),
                "tie" => AssignPacket(MemoryGamePacketType.ClaimTie, out packetType),
                "giveup" => AssignPacket(MemoryGamePacketType.GiveUp, out packetType),
                "end" or "close" => AssignPacket(MemoryGamePacketType.EndRoom, out packetType),
                "mode" or "matchcards" => AssignPacket(MemoryGamePacketType.SelectMatchCardsMode, out packetType),
                _ => Enum.TryParse(normalized, true, out packetType)
            };
        }

        private bool TryDispatchOpenPacket(string title, string playerOneName, string playerTwoName, int rows, int columns, int localPlayerIndex, out string message)
        {
            OpenRoom(title, playerOneName ?? "Player", playerTwoName ?? "Opponent", rows, columns, localPlayerIndex);
            message = DescribeStatus();
            return true;
        }

        private bool TrySelectMatchCardsMode(out string message)
        {
            EnsureRoomOpenFromMiniRoomRuntime();
            _statusMessage = "Match Cards room selected.";
            message = _statusMessage;
            SyncMiniRoomRuntime();
            return true;
        }

        private static bool AssignUnsupportedPacket(MemoryGamePacketType packetType, out string message)
        {
            message = $"Unsupported Memory Game packet: {packetType}.";
            return false;
        }

        private static bool AssignPacket(MemoryGamePacketType value, out MemoryGamePacketType packetType)
        {
            packetType = value;
            return true;
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }

            WzImage uiWindow2Image = global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage uiWindow1Image = global::HaCreator.Program.FindImage("UI", "UIWindow.img");
            WzSubProperty minigameRoot = uiWindow2Image?["Minigame"] as WzSubProperty
                ?? uiWindow1Image?["Minigame"] as WzSubProperty;
            WzSubProperty memoryGameProperty = minigameRoot?["MemoryGame"] as WzSubProperty;
            WzSubProperty commonProperty = minigameRoot?["Common"] as WzSubProperty;

            _backgroundTexture = LoadCanvasTexture(memoryGameProperty?["backgrnd"] as WzCanvasProperty);
            _masterPanelTexture = LoadCanvasTexture(memoryGameProperty?["backgrnd2"] as WzCanvasProperty);
            _turnTexture = LoadCanvasTexture(commonProperty?["turn"] as WzCanvasProperty);
            _readyOnTexture = LoadCanvasTexture(commonProperty?["readyOn"] as WzCanvasProperty);
            _readyOffTexture = LoadCanvasTexture(commonProperty?["readyOff"] as WzCanvasProperty);
            _winTexture = LoadCanvasTexture(commonProperty?["win"] as WzCanvasProperty);
            _loseTexture = LoadCanvasTexture(commonProperty?["lose"] as WzCanvasProperty);
            _drawTexture = LoadCanvasTexture(commonProperty?["draw"] as WzCanvasProperty);
            _readyButtonTexture = LoadButtonTexture(commonProperty, "btReady");
            _startButtonTexture = LoadButtonTexture(commonProperty, "btStart");
            _tieButtonTexture = LoadButtonTexture(commonProperty, "btDraw");
            _giveUpButtonTexture = LoadButtonTexture(commonProperty, "btAbsten");
            _endButtonTexture = LoadButtonTexture(commonProperty, "btExit");
            _banButtonTexture = LoadButtonTexture(commonProperty, "btBan");

            WzImageProperty numberProperty = memoryGameProperty?["number"];
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] = LoadCanvasTexture(numberProperty?[i.ToString()] as WzCanvasProperty);
            }

            WzImageProperty cardProperty = memoryGameProperty?["card"];
            for (int i = 0; i < _cardFaceTextures.Length; i++)
            {
                _cardFaceTextures[i] = LoadCanvasTexture(cardProperty?[i.ToString()] as WzCanvasProperty);
            }

            for (int i = 0; i < _cardBackTextures.Length; i++)
            {
                _cardBackTextures[i] = LoadCanvasTexture(cardProperty?[$"back{i}"] as WzCanvasProperty);
            }

            _assetsLoaded = true;
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_graphicsDevice == null || canvas == null)
            {
                return null;
            }

            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(_graphicsDevice);
        }

        private Texture2D LoadButtonTexture(WzSubProperty commonProperty, string buttonName)
        {
            return LoadCanvasTexture(commonProperty?[buttonName]?["normal"]?["0"] as WzCanvasProperty);
        }

        private Texture2D ResolveCardTexture(Card card)
        {
            if (card == null)
            {
                return null;
            }

            if (!card.IsFaceUp && !card.IsMatched)
            {
                return _cardBackTextures[0];
            }

            if (card.FaceId < 0 || card.FaceId >= _cardFaceTextures.Length)
            {
                return null;
            }

            return _cardFaceTextures[card.FaceId];
        }

        private int ResolveTurnIndicatorX()
        {
            if (_currentTurnIndex == 0)
            {
                return _localPlayerIndex != 0 ? ClientTurnIndicatorLeftX : ClientTurnIndicatorRightX;
            }

            return _localPlayerIndex != 0 ? ClientTurnIndicatorRightX : ClientTurnIndicatorLeftX;
        }

        private static Rectangle CreateButtonRect(int x, int y, Texture2D texture, int fallbackWidth, int fallbackHeight)
        {
            return new Rectangle(x, y, texture?.Width ?? fallbackWidth, texture?.Height ?? fallbackHeight);
        }

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, shadowColor);
            spriteBatch.DrawString(font, text, position, textColor);
        }

        private bool IsValidPlayerIndex(int playerIndex)
        {
            return playerIndex >= 0 && playerIndex < _playerNames.Length;
        }
    }
    #endregion

    #region Ariant Arena Field (CField_AriantArena)
    /// <summary>
    /// Ariant Arena ranking and result flow.
    ///
    /// Client evidence:
    /// - CField_AriantArena::OnUserScore (0x5492b0): updates or removes score rows, clamps score to 9999, and re-sorts rank order
    ///   while suppressing the local player's entry for job branches 8xx and 9xx
    /// - CField_AriantArena::UpdateScoreAndRank (0x547c90): draws a top-left score surface with icon at (5, y), name at (21, y),
    ///   score at (106, y), 17px row spacing, and redraws user name tags after score refreshes
    /// - CField_AriantArena::OnShowResult (0x547630): loads the AriantMatch result animation at the center-top origin with a +100 Y offset
    /// - WZ evidence: UI/UIWindow.img/AriantMatch and UI/UIWindow2.img/AriantMatch expose the result frames and rank icons
    /// </summary>
    public class AriantArenaField
    {
        private const int MaxRankEntries = 6;
        private const int MaxScore = 9999;
        private const int PacketTypeShowResult = 171;
        private const int PacketTypeUserScore = 354;
        private const int IconX = 5;
        private const int NameX = 21;
        private const int ScoreX = 106;
        private const int FirstIconY = 0;
        private const int FirstTextY = 2;
        private const int RowSpacing = 17;
        private const int ResultOffsetY = 100;
        private const int ResultHoldDurationMs = 1200;

        private readonly List<AriantArenaScoreEntry> _entries = new();
        private readonly List<IDXObject> _resultFrames = new();
        private readonly List<IDXObject> _rankIcons = new();
        private GraphicsDevice _graphicsDevice;
        private bool _assetsLoaded;
        private bool _isActive;
        private bool _showScoreboard;
        private bool _showResult;
        private int _resultFrameIndex;
        private int _resultFrameStartedAt;
        private int _resultVisibleUntil;
        private int _scoreRefreshSerial;
        private int _localPlayerJob;
        private SoundManager _soundManager;
        private string _resultSoundKey;
        private string _lastResultMessage;
        private string _localPlayerName;
        private int? _lastPacketType;

        public bool IsActive => _isActive;
        public IReadOnlyList<AriantArenaScoreEntry> Entries => _entries;
        public int ScoreRefreshSerial => _scoreRefreshSerial;

        public void Initialize(GraphicsDevice graphicsDevice, SoundManager soundManager = null)
        {
            _graphicsDevice = graphicsDevice;
            _soundManager = soundManager;
            EnsureAssetsLoaded();
        }

        public void Enable()
        {
            _isActive = true;
            _showScoreboard = true;
            _showResult = false;
            _resultFrameIndex = 0;
            _resultFrameStartedAt = 0;
            _resultVisibleUntil = 0;
            _scoreRefreshSerial = 0;
            _lastResultMessage = null;
            _lastPacketType = null;
            EnsureAssetsLoaded();
        }

        public void SetLocalPlayerState(string playerName, int jobId)
        {
            _localPlayerName = string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
            _localPlayerJob = Math.Max(0, jobId);
        }

        public void OnUserScore(string userName, int score)
        {
            ApplyUserScoreBatch(new[] { new AriantArenaScoreUpdate(userName, score) });
        }

        public bool TryApplyPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            _lastPacketType = packetType;

            if (!_isActive)
            {
                errorMessage = "Ariant Arena runtime inactive.";
                return false;
            }

            try
            {
                switch (packetType)
                {
                    case PacketTypeShowResult:
                        OnShowResult(currentTimeMs);
                        return true;
                    case PacketTypeUserScore:
                        ApplyUserScoreBatch(DecodeUserScorePacket(payload));
                        return true;
                    default:
                        errorMessage = $"Unsupported Ariant packet type: {packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is EndOfStreamException || ex is IOException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public void ApplyUserScoreBatch(IEnumerable<AriantArenaScoreUpdate> updates)
        {
            if (!_isActive)
            {
                return;
            }

            if (updates == null)
            {
                return;
            }

            bool changed = false;
            foreach (AriantArenaScoreUpdate update in updates)
            {
                string normalizedName = update.UserName?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    continue;
                }

                int existingIndex = _entries.FindIndex(entry => string.Equals(entry.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
                if (update.Score < 0 || ShouldSuppressLocalRankEntry(normalizedName))
                {
                    if (existingIndex >= 0)
                    {
                        _entries.RemoveAt(existingIndex);
                        changed = true;
                    }

                    continue;
                }

                int clampedScore = Math.Clamp(update.Score, 0, MaxScore);
                if (existingIndex >= 0)
                {
                    if (_entries[existingIndex].Score != clampedScore)
                    {
                        _entries[existingIndex] = _entries[existingIndex] with { Score = clampedScore };
                        changed = true;
                    }
                }
                else
                {
                    _entries.Add(new AriantArenaScoreEntry(normalizedName, clampedScore, GetNextIconIndex()));
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            _entries.Sort(static (left, right) =>
            {
                int scoreCompare = right.Score.CompareTo(left.Score);
                return scoreCompare != 0
                    ? scoreCompare
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            _scoreRefreshSerial++;
            _showScoreboard = _entries.Count > 0;
            _showResult = false;
        }

        public void OnShowResult(int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            EnsureAssetsLoaded();
            _showScoreboard = false;
            _showResult = _resultFrames.Count > 0;
            _resultFrameIndex = 0;
            _resultFrameStartedAt = currentTimeMs;
            _resultVisibleUntil = currentTimeMs + GetResultDuration() + ResultHoldDurationMs;
            _lastResultMessage = _entries.Count > 0
                ? $"{_entries[0].Name} wins Ariant Arena with {_entries[0].Score} point{(_entries[0].Score == 1 ? string.Empty : "s")}."
                : "Ariant Arena result shown.";

            if (!string.IsNullOrWhiteSpace(_resultSoundKey))
            {
                _soundManager?.PlaySound(_resultSoundKey);
            }
        }

        public void ClearScores()
        {
            _entries.Clear();
            _showScoreboard = false;
            _showResult = false;
            _resultFrameIndex = 0;
            _resultFrameStartedAt = 0;
            _resultVisibleUntil = 0;
            _scoreRefreshSerial = 0;
            _lastResultMessage = null;
            _lastPacketType = null;
        }

        public void Update(int currentTimeMs)
        {
            if (!_isActive || !_showResult || _resultFrames.Count == 0)
            {
                return;
            }

            if (currentTimeMs >= _resultVisibleUntil)
            {
                _showResult = false;
                return;
            }

            while (_resultFrameIndex < _resultFrames.Count - 1)
            {
                IDXObject frame = _resultFrames[_resultFrameIndex];
                int delay = frame.Delay > 0 ? frame.Delay : 100;
                if (currentTimeMs - _resultFrameStartedAt < delay)
                {
                    break;
                }

                _resultFrameStartedAt += delay;
                _resultFrameIndex++;
            }
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            Texture2D pixelTexture,
            SpriteFont font = null)
        {
            if (!_isActive)
            {
                return;
            }

            if (_showScoreboard && font != null)
            {
                DrawScoreboard(spriteBatch, skeletonMeshRenderer, gameTime, font);
            }

            if (_showResult)
            {
                DrawResult(spriteBatch, skeletonMeshRenderer, gameTime, pixelTexture, font);
            }
        }

        public void Reset()
        {
            _isActive = false;
            _showScoreboard = false;
            _showResult = false;
            _entries.Clear();
            _resultFrameIndex = 0;
            _resultFrameStartedAt = 0;
            _resultVisibleUntil = 0;
            _scoreRefreshSerial = 0;
            _localPlayerJob = 0;
            _lastResultMessage = null;
            _localPlayerName = null;
            _lastPacketType = null;
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Ariant Arena runtime inactive";
            }

            string leaderText = _entries.Count == 0
                ? "no scores"
                : string.Join(", ", _entries.Take(MaxRankEntries).Select((entry, index) => $"{index + 1}.{entry.Name}={entry.Score}"));

            return $"Ariant Arena active, {_entries.Count} score row(s), result={(_showResult ? "showing" : "idle")}, refresh={_scoreRefreshSerial}, lastPacket={(_lastPacketType?.ToString() ?? "None")}, {leaderText}";
        }

        private bool ShouldSuppressLocalRankEntry(string normalizedName)
        {
            return !string.IsNullOrWhiteSpace(_localPlayerName)
                && string.Equals(normalizedName, _localPlayerName, StringComparison.OrdinalIgnoreCase)
                && IsHiddenAriantArenaJob(_localPlayerJob);
        }

        private static bool IsHiddenAriantArenaJob(int jobId)
        {
            int branch = Math.Abs(jobId) % 1000 / 100;
            return branch == 8 || branch == 9;
        }

        private void DrawScoreboard(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, SpriteFont font)
        {
            int rowCount = Math.Min(_entries.Count, MaxRankEntries);
            for (int i = 0; i < rowCount; i++)
            {
                int iconY = FirstIconY + (i * RowSpacing);
                int textY = FirstTextY + (i * RowSpacing);
                AriantArenaScoreEntry entry = _entries[i];

                if (entry.IconIndex >= 0 && entry.IconIndex < _rankIcons.Count)
                {
                    IDXObject icon = _rankIcons[entry.IconIndex];
                    icon.DrawBackground(
                        spriteBatch,
                        skeletonMeshRenderer,
                        gameTime,
                        IconX + icon.X,
                        iconY + icon.Y,
                        Color.White,
                        false,
                        null);
                }

                DrawOutlinedText(spriteBatch, font, entry.Name, new Vector2(NameX, textY), new Color(20, 20, 20), new Color(204, 236, 255));
                DrawOutlinedText(spriteBatch, font, entry.Score.ToString(), new Vector2(ScoreX, textY), new Color(20, 20, 20), new Color(255, 222, 112));
            }
        }

        private void DrawResult(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, Texture2D pixelTexture, SpriteFont font)
        {
            if (_resultFrames.Count == 0)
            {
                return;
            }

            IDXObject frame = _resultFrames[Math.Clamp(_resultFrameIndex, 0, _resultFrames.Count - 1)];
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int anchorX = viewport.Width / 2;
            int anchorY = ResultOffsetY;

            frame.DrawBackground(
                spriteBatch,
                skeletonMeshRenderer,
                gameTime,
                anchorX + frame.X,
                anchorY + frame.Y,
                Color.White,
                false,
                null);

            if (font != null && !string.IsNullOrWhiteSpace(_lastResultMessage))
            {
                Vector2 textSize = font.MeasureString(_lastResultMessage);
                float textX = (viewport.Width - textSize.X) * 0.5f;
                float textY = Math.Max(anchorY + 236, 220);

                if (pixelTexture != null)
                {
                    Rectangle backdrop = new Rectangle((int)textX - 10, (int)textY - 6, (int)textSize.X + 20, (int)textSize.Y + 12);
                    spriteBatch.Draw(pixelTexture, backdrop, new Color(0, 0, 0, 120));
                }

                DrawOutlinedText(spriteBatch, font, _lastResultMessage, new Vector2(textX, textY), Color.Black, Color.White);
            }
        }

        private int GetResultDuration()
        {
            int total = 0;
            for (int i = 0; i < _resultFrames.Count; i++)
            {
                total += _resultFrames[i].Delay > 0 ? _resultFrames[i].Delay : 100;
            }

            return total;
        }

        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _graphicsDevice == null)
            {
                return;
            }

            WzImage uiWindow = global::HaCreator.Program.FindImage("UI", "UIWindow.img")
                ?? global::HaCreator.Program.FindImage("UI", "UIWindow2.img");

            WzImageProperty ariantMatch = uiWindow?["AriantMatch"];
            LoadAnimatedFrames(ariantMatch?["Result"], _resultFrames);
            EnsureResultSoundRegistered();

            WzImageProperty iconRoot = ariantMatch?["characterIcon"];
            for (int i = 0; i < MaxRankEntries; i++)
            {
                if (WzInfoTools.GetRealProperty(iconRoot?[i.ToString()]) is WzCanvasProperty canvas
                    && TryCreateDxObject(canvas, out IDXObject icon))
                {
                    _rankIcons.Add(icon);
                }
            }

            _assetsLoaded = true;
        }

        private void EnsureResultSoundRegistered()
        {
            if (_soundManager == null || !string.IsNullOrWhiteSpace(_resultSoundKey))
            {
                return;
            }

            WzBinaryProperty sound =
                WzInfoTools.GetRealProperty(global::HaCreator.Program.FindImage("Sound", "MiniGame.img")?["Show"]) as WzBinaryProperty
                ?? WzInfoTools.GetRealProperty(global::HaCreator.Program.FindImage("Sound", "MiniGame.img")?["Win"]) as WzBinaryProperty
                ?? FindBestAriantResultSound(
                    global::HaCreator.Program.FindImage("Sound", "MiniGame.img"),
                    global::HaCreator.Program.FindImage("Sound", "Game.img"));
            if (sound == null)
            {
                return;
            }

            _resultSoundKey = "AriantArena:Result";
            _soundManager.RegisterSound(_resultSoundKey, sound);
        }

        private static WzBinaryProperty FindBestAriantResultSound(params WzImage[] sources)
        {
            WzBinaryProperty best = null;
            int bestScore = 0;

            foreach (WzImage source in sources)
            {
                if (source?.WzProperties == null)
                {
                    continue;
                }

                foreach (WzImageProperty child in source.WzProperties)
                {
                    FindBestAriantResultSoundRecursive(child, child?.Name ?? string.Empty, ref best, ref bestScore);
                }
            }

            return best;
        }

        private static void FindBestAriantResultSoundRecursive(
            WzImageProperty property,
            string path,
            ref WzBinaryProperty best,
            ref int bestScore)
        {
            if (property == null)
            {
                return;
            }

            WzImageProperty resolved = WzInfoTools.GetRealProperty(property);
            string currentPath = string.IsNullOrWhiteSpace(path)
                ? property.Name ?? string.Empty
                : path;
            string lowerPath = currentPath.ToLowerInvariant();

            if (resolved is WzBinaryProperty binary)
            {
                int score = 0;
                if (lowerPath.Contains("ariant"))
                {
                    score += 8;
                }

                if (lowerPath.Contains("result") || lowerPath.Contains("clear"))
                {
                    score += 4;
                }

                if (lowerPath.Contains("win"))
                {
                    score += 2;
                }

                if (score > bestScore)
                {
                    best = binary;
                    bestScore = score;
                }

                return;
            }

            if (resolved?.WzProperties == null)
            {
                return;
            }

            foreach (WzImageProperty child in resolved.WzProperties)
            {
                string childPath = string.IsNullOrWhiteSpace(currentPath)
                    ? child?.Name ?? string.Empty
                    : $"{currentPath}/{child?.Name}";
                FindBestAriantResultSoundRecursive(child, childPath, ref best, ref bestScore);
            }
        }

        private static IEnumerable<AriantArenaScoreUpdate> DecodeUserScorePacket(byte[] payload)
        {
            if (payload == null)
            {
                throw new InvalidDataException("Ariant score packet payload is missing.");
            }

            using var stream = new MemoryStream(payload, writable: false);
            using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);

            int count = reader.ReadByte();
            var updates = new List<AriantArenaScoreUpdate>(count);
            for (int i = 0; i < count; i++)
            {
                string userName = ReadMapleString(reader);
                int score = reader.ReadInt32();
                updates.Add(new AriantArenaScoreUpdate(userName, score));
            }

            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException($"Ariant score packet has {stream.Length - stream.Position} trailing byte(s).");
            }

            return updates;
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Ariant score packet ended before the player name was fully read.");
            }

            return Encoding.Default.GetString(bytes);
        }

        private int GetNextIconIndex()
        {
            if (MaxRankEntries <= 0)
            {
                return -1;
            }

            return Math.Clamp(_entries.Count, 0, MaxRankEntries - 1);
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

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, shadowColor);
            spriteBatch.DrawString(font, text, position, textColor);
        }
    }

    public readonly record struct AriantArenaScoreEntry(string Name, int Score, int IconIndex = -1);
    public readonly record struct AriantArenaScoreUpdate(string UserName, int Score);
    #endregion
}
