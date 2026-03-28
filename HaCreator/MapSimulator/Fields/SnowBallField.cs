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
        // Client data symbol CSnowBall::ms_anDelay at MapleStory.exe RVA 0x8568E4.
        private static readonly int[] ms_anDelay = { 150, 200, 250, 300, 350, 400, 450, 500, 0, -500 };
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
}
