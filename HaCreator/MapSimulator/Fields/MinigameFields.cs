using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Fields
{
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
            _timeRemaining = timeSeconds;

            if (timeSeconds <= 0 && _gameActive)
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
            if (!_gameActive)
                return;

            float deltaTime = (tickCount - _lastUpdateTime) / 1000f;
            _lastUpdateTime = tickCount;

            // Update timer
            _timeRemaining -= (int)(deltaTime * 1000) / 1000;
            if (_timeRemaining <= 0)
            {
                EndGame();
                return;
            }

            // Process hit queue
            ProcessHitQueue(tickCount);

            // Update all coconuts
            foreach (var coconut in _coconuts)
            {
                coconut.Update(tickCount, _gravity, _groundY);

                // Score coconuts that hit the ground
                if (coconut.State == CoconutState.Scored && coconut.IsActive)
                {
                    coconut.IsActive = false;
                    if (coconut.Team == 0)
                        _team0Score++;
                    else if (coconut.Team == 1)
                        _team1Score++;
                }
            }

            // Clear expired message
            if (_currentMessage != null && tickCount >= _messageEndTime)
                _currentMessage = null;
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
                        _coconuts[hit.Target].State = (CoconutState)hit.NewState;
                    }
                    _hitQueue.RemoveAt(i);
                }
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

            // Scoreboard
            Rectangle bgRect = new Rectangle(screenWidth / 2 - 120, 10, 240, 60);
            spriteBatch.Draw(pixel, bgRect, new Color(0, 0, 0, 150));

            // Scores
            string scoreText = $"Maple {_team0Score} : {_team1Score} Story";
            Vector2 scoreSize = font.MeasureString(scoreText);
            spriteBatch.DrawString(font, scoreText,
                new Vector2((screenWidth - scoreSize.X) / 2, 20), Color.White);

            // Timer
            int minutes = _timeRemaining / 60;
            int seconds = _timeRemaining % 60;
            string timerText = $"Time: {minutes}:{seconds:D2}";
            Vector2 timerSize = font.MeasureString(timerText);
            Color timerColor = _timeRemaining <= 10 ? Color.Red : Color.Yellow;
            spriteBatch.DrawString(font, timerText,
                new Vector2((screenWidth - timerSize.X) / 2, 45), timerColor);

            // Message
            if (_currentMessage != null)
            {
                Vector2 msgSize = font.MeasureString(_currentMessage);
                Vector2 msgPos = new Vector2((screenWidth - msgSize.X) / 2, 100);
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
}
