using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using MapleLib.Converters;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _ariantArena.Initialize(graphicsDevice);
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
        #endregion

        #region Nested Types

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
            }

            public void Update(int tickCount)
            {
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

            public void Init(int x, int y, int hp)
            {
                Area = new Rectangle(x - 50, y - 100, 100, 100);
                MaxHP = hp;
                HP = hp;
            }

            public void Hit(int damage)
            {
                HP = Math.Max(0, HP - damage);
                ShowHitEffect = true;
                HitEffectEndTime = Environment.TickCount + 500;

                // Stun briefly on hit
                IsStunned = true;
                StunEndTime = Environment.TickCount + 300;
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

        public enum GameState
        {
            NotStarted = -1,
            Active = 1,
            Team0InZone = 2,
            Team1InZone = 3,
            Team0Win = 10,
            Team1Win = 11
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

        #endregion

        #region Properties

        public GameState State => _state;
        public SnowBall[] SnowBalls => _snowBalls;
        public SnowMan[] SnowMen => _snowMen;
        public int Team0Score => _team0Score;
        public int Team1Score => _team1Score;
        public bool IsActive => _state == GameState.Active;
        public string CurrentMessage => _currentMessage;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize SnowBall field from map configuration
        /// </summary>
        public void Initialize(int leftGoalX, int rightGoalX, int groundY,
            int snowBallRadius = 80, int deltaX = 20)
        {
            ms_nDeltaX = deltaX;
            _leftGoalX = leftGoalX;
            _rightGoalX = rightGoalX;
            _groundY = groundY;

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
                _snowMen[i].Init(snowManX, groundY, 100);
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

        #endregion

        #region Packet Handling (matching client)

        /// <summary>
        /// OnSnowBallState - Packet type 338
        /// </summary>
        public void OnSnowBallState(int newState, int team0Pos, int team1Pos)
        {
            GameState previousState = _state;
            _state = (GameState)newState;

            // Update snowball positions
            if (team0Pos != 0 && _snowBalls[0] != null)
                _snowBalls[0].SetPos(team0Pos, _groundY - _snowBalls[0].Area.Height / 2, false);
            if (team1Pos != 0 && _snowBalls[1] != null)
                _snowBalls[1].SetPos(team1Pos, _groundY - _snowBalls[1].Area.Height / 2, true);

            // Handle state transitions
            switch (_state)
            {
                case GameState.Active:
                    if (previousState == GameState.NotStarted)
                        ShowMessage("The snowball fight has begun!", 3000);
                    break;
                case GameState.Team0Win:
                    _snowBalls[0]?.Win();
                    _team0Score++;
                    ShowMessage("Team Maple wins!", 5000);
                    break;
                case GameState.Team1Win:
                    _snowBalls[1]?.Win();
                    _team1Score++;
                    ShowMessage("Team Story wins!", 5000);
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
            ShowMessage(message, 3000);
        }

        /// <summary>
        /// OnSnowBallTouch - Packet type 341
        /// Player entered/exited snowball zone
        /// </summary>
        public void OnSnowBallTouch(int team)
        {
            // This triggers movement when a player is in the zone
            if (_state == GameState.Active && team >= 0 && team < 2)
            {
                _state = team == 0 ? GameState.Team0InZone : GameState.Team1InZone;
            }
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
                case 1: // Right snowball
                    if (_snowBalls[target] != null)
                    {
                        // Snowball moves based on which team hit it
                        int direction = team == 0 ? 1 : -1; // Team 0 pushes right, Team 1 pushes left
                        _snowBalls[target].Move(direction);

                        // Queue damage display
                        _damageQueue.Add(new DamageInfo
                        {
                            Target = target,
                            Damage = damage,
                            StartTime = tickCount
                        });

                        // Check win conditions
                        CheckWinCondition();
                    }
                    break;

                case 2: // Left snowman
                case 3: // Right snowman
                    int snowManIndex = target - 2;
                    if (_snowMen[snowManIndex] != null)
                    {
                        _snowMen[snowManIndex].Hit(damage);
                        _damageQueue.Add(new DamageInfo
                        {
                            Target = target,
                            Damage = damage,
                            StartTime = tickCount
                        });
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
            // Team 0 wins if right snowball reaches left goal
            if (_snowBalls[1] != null && _snowBalls[1].PositionX <= _leftGoalX + 50)
            {
                OnSnowBallState((int)GameState.Team0Win, 0, 0);
            }
            // Team 1 wins if left snowball reaches right goal
            else if (_snowBalls[0] != null && _snowBalls[0].PositionX >= _rightGoalX - 50)
            {
                OnSnowBallState((int)GameState.Team1Win, 0, 0);
            }
        }

        #endregion

        #region Update

        public void Update(int tickCount)
        {
            // Update snowballs
            foreach (var ball in _snowBalls)
                ball?.Update(tickCount);

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
                    // Snowman hit effect
                    System.Diagnostics.Debug.WriteLine($"[SnowBallField] SnowMan {damage.Target - 2} hit for {damage.Damage}");
                    break;
            }
        }

        private void ShowMessage(string message, int durationMs)
        {
            _currentMessage = message;
            _messageEndTime = Environment.TickCount + durationMs;
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
            string scoreText = $"Maple {_team0Score} : {_team1Score} Story";
            Vector2 scoreSize = font.MeasureString(scoreText);
            spriteBatch.DrawString(font, scoreText,
                new Vector2((screenWidth - scoreSize.X) / 2, 25), Color.White);

            // Game state
            string stateText = _state switch
            {
                GameState.NotStarted => "Waiting...",
                GameState.Active => "FIGHT!",
                GameState.Team0InZone => "Maple Zone!",
                GameState.Team1InZone => "Story Zone!",
                GameState.Team0Win => "MAPLE WINS!",
                GameState.Team1Win => "STORY WINS!",
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
            _currentMessage = null;

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

        public class Coconut
        {
            public int Id;
            public CoconutState State;
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

        // Configuration
        private int _totalCoconuts;
        private int _groundY;
        private float _gravity = 0.3f;
        private Rectangle _treeArea;

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
        public bool IsActive => _gameActive;
        public IReadOnlyList<Coconut> Coconuts => _coconuts;

        #endregion

        #region Initialization

        public void Initialize(int coconutCount, Rectangle treeArea, int groundY)
        {
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
                    Position = new Vector2(
                        treeArea.Left + rand.Next(treeArea.Width),
                        treeArea.Top + rand.Next(treeArea.Height / 2)
                    ),
                    Velocity = Vector2.Zero,
                    Team = -1,
                    IsActive = true
                });
            }

            _team0Score = 0;
            _team1Score = 0;
            _gameActive = false;
            _lastUpdateTime = Environment.TickCount;

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
        public void OnCoconutHit(int targetId, int delay, int newState)
        {
            int startTime = Environment.TickCount + delay;

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
        public void OnCoconutScore(int team0, int team1)
        {
            _team0Score = team0;
            _team1Score = team1;

            System.Diagnostics.Debug.WriteLine($"[CoconutField] Score: Maple {team0} - Story {team1}");
        }

        /// <summary>
        /// OnClock - Time update
        /// </summary>
        public void OnClock(int timeSeconds)
        {
            int now = Environment.TickCount;
            int durationMs = Math.Max(0, timeSeconds) * 1000;
            _finishTick = durationMs > 0 ? now + durationMs : 1;
            _timeRemaining = Math.Max(0, timeSeconds);

            if (timeSeconds > 0)
            {
                _gameActive = true;
                _lastUpdateTime = now;
            }
            else if (_gameActive)
            {
                EndGame();
            }
        }

        #endregion

        #region Simulation (for testing)

        public void StartGame(int durationSeconds = 120)
        {
            _gameActive = true;
            _timeRemaining = durationSeconds;
            _finishTick = Environment.TickCount + Math.Max(0, durationSeconds) * 1000;
            _lastUpdateTime = Environment.TickCount;
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

        private void EndGame()
        {
            _gameActive = false;
            _finishTick = 0;
            _timeRemaining = 0;

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
                        EndGame();
                    }
                }

                foreach (var coconut in _coconuts)
                {
                    coconut.Update(tickCount, _gravity, _groundY);
                }
            }

            if (_currentMessage != null && tickCount >= _messageEndTime)
            {
                _currentMessage = null;
            }
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

            // Draw UI
            if (font != null)
            {
                DrawUI(spriteBatch, pixelTexture, font);
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

        private void DrawUI(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font)
        {
            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            int boardWidth = 220;
            int boardHeight = 120;
            int boardX = screenWidth / 2 - boardWidth / 2;
            int boardY = 10;

            Rectangle bgRect = new Rectangle(boardX, boardY, boardWidth, boardHeight);
            spriteBatch.Draw(pixel, bgRect, new Color(0, 0, 0, 150));
            spriteBatch.Draw(pixel, new Rectangle(boardX + 12, boardY + 12, boardWidth - 24, boardHeight - 24), new Color(40, 58, 36, 190));

            string team0Text = _team0Score.ToString();
            string team1Text = _team1Score.ToString();
            Vector2 team0Size = font.MeasureString(team0Text);
            Vector2 team1Size = font.MeasureString(team1Text);
            Vector2 team0Pos = new Vector2(boardX + 37 - team0Size.X / 2f, boardY + 25);
            Vector2 team1Pos = new Vector2(boardX + 150 - team1Size.X / 2f, boardY + 25);
            spriteBatch.DrawString(font, team0Text, team0Pos, new Color(120, 190, 255));
            spriteBatch.DrawString(font, team1Text, team1Pos, new Color(255, 140, 140));

            int minutes = _timeRemaining / 60;
            int seconds = _timeRemaining % 60;
            string timerText = $"{minutes}:{seconds:D2}";
            Vector2 timerSize = font.MeasureString(timerText);
            Color timerColor = _timeRemaining <= 10 ? Color.Red : Color.Yellow;
            spriteBatch.DrawString(font, timerText,
                new Vector2(boardX + 60 - timerSize.X / 2f, boardY + 83), timerColor);

            if (_currentMessage != null)
            {
                Vector2 msgSize = font.MeasureString(_currentMessage);
                Vector2 msgPos = new Vector2((screenWidth - msgSize.X) / 2, boardY + boardHeight + 16);
                spriteBatch.DrawString(font, _currentMessage, msgPos + Vector2.One, Color.Black);
                spriteBatch.DrawString(font, _currentMessage, msgPos, Color.Yellow);
            }
        }

        #endregion

        #region Utility

        public void Reset()
        {
            _gameActive = false;
            _hitQueue.Clear();
            _team0Score = 0;
            _team1Score = 0;
            _timeRemaining = 0;
            _finishTick = 0;
            _currentMessage = null;

            // Reset all coconuts
            Random rand = new Random();
            foreach (var coconut in _coconuts)
            {
                coconut.State = CoconutState.OnTree;
                coconut.Position = new Vector2(
                    _treeArea.Left + rand.Next(_treeArea.Width),
                    _treeArea.Top + rand.Next(_treeArea.Height / 2)
                );
                coconut.Velocity = Vector2.Zero;
                coconut.Team = -1;
                coconut.HitCount = 0;
                coconut.IsActive = true;
                coconut.Rotation = 0;
            }
        }

        #endregion
    }
    #endregion

    #region Memory Game / Match Cards (CMemoryGameDlg)
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

        private readonly List<Card> _cards = new();
        private readonly List<int> _revealedCardIndices = new(2);
        private readonly int[] _scores = new int[2];
        private readonly bool[] _readyStates = new bool[2];
        private readonly string[] _playerNames = new string[2];
        private readonly int[] _wins = new int[2];
        private readonly int[] _losses = new int[2];
        private readonly int[] _draws = new int[2];

        private RoomStage _stage = RoomStage.Hidden;
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
            return true;
        }

        public bool TryRevealCard(int cardIndex, int tickCount, out string message)
        {
            if (_stage != RoomStage.Playing)
            {
                message = "The board is not active.";
                return false;
            }

            if (_currentTurnIndex != _localPlayerIndex)
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
                }

                message = _statusMessage;
                return true;
            }

            _pendingHideTick = tickCount + DefaultMismatchHideDelayMs;
            _statusMessage = "Mismatch. Cards will flip back.";
            message = _statusMessage;
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
            return true;
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
                }
            }
            else if (_stage == RoomStage.Result && _resultExpireTick > 0 && tickCount >= _resultExpireTick)
            {
                ReturnToLobby();
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
            if (_stage == RoomStage.Hidden || pixelTexture == null || font == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int dialogWidth = 420;
            int dialogHeight = 360;
            int dialogX = viewport.Width / 2 - dialogWidth / 2;
            int dialogY = Math.Max(24, viewport.Height / 2 - dialogHeight / 2);

            Rectangle outer = new Rectangle(dialogX, dialogY, dialogWidth, dialogHeight);
            Rectangle inner = new Rectangle(dialogX + 8, dialogY + 8, dialogWidth - 16, dialogHeight - 16);
            Rectangle titleBar = new Rectangle(dialogX + 8, dialogY + 8, dialogWidth - 16, 34);
            Rectangle boardArea = new Rectangle(dialogX + 18, dialogY + 88, 252, 232);
            Rectangle sidebar = new Rectangle(dialogX + 284, dialogY + 88, 118, 232);

            spriteBatch.Draw(pixelTexture, outer, new Color(20, 27, 41, 235));
            spriteBatch.Draw(pixelTexture, inner, new Color(236, 223, 191, 245));
            spriteBatch.Draw(pixelTexture, titleBar, new Color(109, 69, 28, 255));
            spriteBatch.Draw(pixelTexture, boardArea, new Color(97, 59, 28, 255));
            spriteBatch.Draw(pixelTexture, sidebar, new Color(56, 40, 26, 230));

            DrawOutlinedText(spriteBatch, font, _title, new Vector2(dialogX + 20, dialogY + 15), Color.Black, Color.White);

            DrawNameBar(spriteBatch, pixelTexture, font, _playerNames[0], _scores[0], dialogX + 18, dialogY + 50, _currentTurnIndex == 0);
            DrawNameBar(spriteBatch, pixelTexture, font, _playerNames[1], _scores[1], dialogX + 210, dialogY + 50, _currentTurnIndex == 1);

            DrawBoard(spriteBatch, pixelTexture, font, boardArea);
            DrawSidebar(spriteBatch, pixelTexture, font, sidebar, tickCount);
            DrawOutlinedText(spriteBatch, font, _statusMessage, new Vector2(dialogX + 18, dialogY + 330), Color.Black, new Color(255, 239, 197));
        }

        public string DescribeStatus()
        {
            string playerOneName = string.IsNullOrWhiteSpace(_playerNames[0]) ? "Player" : _playerNames[0];
            string playerTwoName = string.IsNullOrWhiteSpace(_playerNames[1]) ? "Opponent" : _playerNames[1];
            return $"{_title}: stage={_stage}, turn={_currentTurnIndex}, ready=[{_readyStates[0]},{_readyStates[1]}], score={_scores[0]}-{_scores[1]}, players={playerOneName}/{playerTwoName}, cards={_cards.Count}, pendingHide={_pendingHideTick > 0}";
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
        }

        private void InitializeBoard()
        {
            _cards.Clear();
            _revealedCardIndices.Clear();
            _scores[0] = 0;
            _scores[1] = 0;
            _pendingHideTick = 0;
            _resultExpireTick = 0;

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
                return;
            }

            int winnerIndex = _scores[0] > _scores[1] ? 0 : 1;
            int loserIndex = winnerIndex == 0 ? 1 : 0;
            _lastWinnerIndex = winnerIndex;
            _wins[winnerIndex]++;
            _losses[loserIndex]++;
            _statusMessage = $"Round complete. {_playerNames[winnerIndex]} wins.";
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
        }

        private void DrawBoard(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle area)
        {
            if (_cards.Count == 0)
            {
                DrawOutlinedText(spriteBatch, font, "No board yet", new Vector2(area.X + 72, area.Y + 102), Color.Black, Color.White);
                return;
            }

            int gap = 8;
            int cardWidth = (area.Width - gap * (_columns + 1)) / _columns;
            int cardHeight = (area.Height - gap * (_rows + 1)) / _rows;

            for (int index = 0; index < _cards.Count; index++)
            {
                int row = index / _columns;
                int column = index % _columns;
                Rectangle cardRect = new Rectangle(
                    area.X + gap + column * (cardWidth + gap),
                    area.Y + gap + row * (cardHeight + gap),
                    cardWidth,
                    cardHeight);

                Card card = _cards[index];
                Color cardColor = card.IsMatched
                    ? new Color(111, 162, 85)
                    : card.IsFaceUp
                        ? new Color(246, 224, 167)
                        : new Color(145, 82, 42);

                spriteBatch.Draw(pixel, cardRect, cardColor);
                spriteBatch.Draw(pixel, new Rectangle(cardRect.X + 2, cardRect.Y + 2, cardRect.Width - 4, cardRect.Height - 4), cardColor * 0.9f);

                string label = card.IsFaceUp || card.IsMatched ? (card.FaceId + 1).ToString() : "?";
                Vector2 size = font.MeasureString(label);
                Vector2 pos = new(cardRect.Center.X - size.X / 2f, cardRect.Center.Y - size.Y / 2f);
                DrawOutlinedText(spriteBatch, font, label, pos, Color.Black, Color.White);
            }
        }

        private void DrawSidebar(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle area, int tickCount)
        {
            string[] buttons =
            {
                _stage == RoomStage.Playing ? "1007 Ready" : "1001 Start",
                "1002 Tie",
                "1003 Give Up",
                "1004 End",
                "1008 Ban"
            };

            for (int i = 0; i < buttons.Length; i++)
            {
                Rectangle buttonRect = new Rectangle(area.X + 10, area.Y + 10 + i * 40, area.Width - 20, 30);
                spriteBatch.Draw(pixel, buttonRect, new Color(119, 84, 48));
                DrawOutlinedText(spriteBatch, font, buttons[i], new Vector2(buttonRect.X + 8, buttonRect.Y + 6), Color.Black, Color.White);
            }

            int textY = area.Y + 220;
            DrawOutlinedText(spriteBatch, font, $"Turn: {_playerNames[_currentTurnIndex]}", new Vector2(area.X + 10, textY), Color.Black, Color.White);
            DrawOutlinedText(spriteBatch, font, $"Time: {CurrentTurnTimeRemainingSeconds}", new Vector2(area.X + 10, textY + 20), Color.Black, Color.White);
            DrawOutlinedText(spriteBatch, font, $"W/L/D: {_wins[0]}/{_losses[0]}/{_draws[0]}", new Vector2(area.X + 10, textY + 40), Color.Black, Color.White);
        }

        private void DrawNameBar(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, string name, int score, int x, int y, bool isActiveTurn)
        {
            Rectangle rect = new Rectangle(x, y, 174, 28);
            spriteBatch.Draw(pixel, rect, isActiveTurn ? new Color(223, 196, 120) : new Color(132, 103, 73));
            DrawOutlinedText(spriteBatch, font, name, new Vector2(x + 8, y + 5), Color.Black, Color.White);
            DrawOutlinedText(spriteBatch, font, score.ToString(), new Vector2(x + 146, y + 5), Color.Black, Color.White);
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
    /// - CField_AriantArena::UpdateScoreAndRank (0x547c90): draws a top-left score surface with icon at (5, y), name at (21, y),
    ///   score at (106, y), and 17px row spacing
    /// - CField_AriantArena::OnShowResult (0x547630): loads the AriantMatch result animation at the center-top origin with a +100 Y offset
    /// - WZ evidence: UI/UIWindow.img/AriantMatch and UI/UIWindow2.img/AriantMatch expose the result frames and rank icons
    /// </summary>
    public class AriantArenaField
    {
        private const int MaxRankEntries = 6;
        private const int MaxScore = 9999;
        private const int IconX = 5;
        private const int NameX = 21;
        private const int ScoreX = 106;
        private const int FirstRowY = 0;
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
        private string _lastResultMessage;

        public bool IsActive => _isActive;
        public IReadOnlyList<AriantArenaScoreEntry> Entries => _entries;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
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
            _lastResultMessage = null;
            EnsureAssetsLoaded();
        }

        public void OnUserScore(string userName, int score)
        {
            if (!_isActive)
            {
                return;
            }

            string normalizedName = userName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            int existingIndex = _entries.FindIndex(entry => string.Equals(entry.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (score < 0)
            {
                if (existingIndex >= 0)
                {
                    _entries.RemoveAt(existingIndex);
                }
            }
            else
            {
                int clampedScore = Math.Clamp(score, 0, MaxScore);
                if (existingIndex >= 0)
                {
                    _entries[existingIndex] = _entries[existingIndex] with { Score = clampedScore };
                }
                else
                {
                    _entries.Add(new AriantArenaScoreEntry(normalizedName, clampedScore));
                }
            }

            _entries.Sort(static (left, right) =>
            {
                int scoreCompare = right.Score.CompareTo(left.Score);
                return scoreCompare != 0
                    ? scoreCompare
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

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
        }

        public void ClearScores()
        {
            _entries.Clear();
            _showScoreboard = false;
            _showResult = false;
            _resultFrameIndex = 0;
            _resultFrameStartedAt = 0;
            _resultVisibleUntil = 0;
            _lastResultMessage = null;
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
            _lastResultMessage = null;
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

            return $"Ariant Arena active, {_entries.Count} score row(s), result={(_showResult ? "showing" : "idle")}, {leaderText}";
        }

        private void DrawScoreboard(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, SpriteFont font)
        {
            int rowCount = Math.Min(_entries.Count, MaxRankEntries);
            for (int i = 0; i < rowCount; i++)
            {
                int rowY = FirstRowY + (i * RowSpacing);

                if (i < _rankIcons.Count)
                {
                    IDXObject icon = _rankIcons[i];
                    icon.DrawBackground(
                        spriteBatch,
                        skeletonMeshRenderer,
                        gameTime,
                        IconX + icon.X,
                        rowY + icon.Y,
                        Color.White,
                        false,
                        null);
                }

                AriantArenaScoreEntry entry = _entries[i];
                DrawOutlinedText(spriteBatch, font, entry.Name, new Vector2(NameX, rowY), new Color(20, 20, 20), new Color(204, 236, 255));
                DrawOutlinedText(spriteBatch, font, entry.Score.ToString(), new Vector2(ScoreX, rowY), new Color(20, 20, 20), new Color(255, 222, 112));
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

    public readonly record struct AriantArenaScoreEntry(string Name, int Score);
    #endregion
}
