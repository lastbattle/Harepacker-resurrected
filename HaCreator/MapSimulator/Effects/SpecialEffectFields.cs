using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Special Effect Field System - Manages specialized field types from MapleStory client.
    ///
    /// Handles:
    /// - CField_Wedding: Wedding ceremony effects (packets 379, 380)
    /// - CField_Witchtower: Witch tower score tracking (packet 358)
    /// - CField_GuildBoss: Guild boss healer/pulley mechanics (packets 344, 345)
    /// - CField_Massacre: Kill counting and gauge system (packet 173)
    /// </summary>
    public class SpecialEffectFields
    {
        #region Sub-systems
        private readonly WeddingField _wedding = new();
        private readonly WitchtowerField _witchtower = new();
        private readonly GuildBossField _guildBoss = new();
        private readonly MassacreField _massacre = new();
        #endregion

        #region Public Access
        public WeddingField Wedding => _wedding;
        public WitchtowerField Witchtower => _witchtower;
        public GuildBossField GuildBoss => _guildBoss;
        public MassacreField Massacre => _massacre;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
            _wedding.Initialize(device);
            _witchtower.Initialize(device);
            _guildBoss.Initialize(device);
            _massacre.Initialize(device);
        }

        /// <summary>
        /// Detect and enable appropriate field type based on map ID
        /// </summary>
        public void DetectFieldType(int mapId)
        {
            // Wedding maps: 680000110 (Cathedral), 680000210 (Chapel)
            if (mapId == 680000110 || mapId == 680000210)
            {
                _wedding.Enable(mapId);
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Wedding field detected: {mapId}");
            }
            // Witchtower maps (would be in 900000000 range typically)
            else if (IsWitchtowerMap(mapId))
            {
                _witchtower.Enable();
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Witchtower field detected: {mapId}");
            }
            // Guild boss maps (e.g., Ergoth, Shao maps)
            else if (IsGuildBossMap(mapId))
            {
                _guildBoss.Enable();
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] GuildBoss field detected: {mapId}");
            }
            // Massacre maps (special event PQ maps)
            else if (IsMassacreMap(mapId))
            {
                _massacre.Enable();
                System.Diagnostics.Debug.WriteLine($"[SpecialEffectFields] Massacre field detected: {mapId}");
            }
        }

        private static bool IsWitchtowerMap(int mapId)
        {
            // Witchtower maps - typically special event maps
            return mapId >= 922000000 && mapId <= 922000099;
        }

        private static bool IsGuildBossMap(int mapId)
        {
            // Guild boss maps (Ergoth, Shao, etc.)
            // Examples: 610030000 series, 673000000 series
            return (mapId >= 610030000 && mapId <= 610030099) ||
                   (mapId >= 673000000 && mapId <= 673000099);
        }

        private static bool IsMassacreMap(int mapId)
        {
            // Massacre/hunting event maps
            return mapId >= 910000000 && mapId <= 910000099;
        }
        #endregion

        #region Update
        public void Update(GameTime gameTime, int currentTimeMs)
        {
            float deltaSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_wedding.IsActive)
                _wedding.Update(currentTimeMs, deltaSeconds);

            if (_witchtower.IsActive)
                _witchtower.Update(currentTimeMs, deltaSeconds);

            if (_guildBoss.IsActive)
                _guildBoss.Update(currentTimeMs, deltaSeconds);

            if (_massacre.IsActive)
                _massacre.Update(currentTimeMs, deltaSeconds);
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font = null)
        {
            if (_wedding.IsActive)
                _wedding.Draw(spriteBatch, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, tickCount, pixelTexture, font);

            if (_witchtower.IsActive)
                _witchtower.Draw(spriteBatch, pixelTexture, font);

            if (_guildBoss.IsActive)
                _guildBoss.Draw(spriteBatch, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, centerX, centerY, tickCount, pixelTexture, font);

            if (_massacre.IsActive)
                _massacre.Draw(spriteBatch, pixelTexture, font);
        }
        #endregion

        #region Reset
        public void ResetAll()
        {
            _wedding.Reset();
            _witchtower.Reset();
            _guildBoss.Reset();
            _massacre.Reset();
        }
        #endregion
    }

    #region Wedding Field (CField_Wedding)
    /// <summary>
    /// Wedding Field - Wedding ceremony effects and dialogs.
    ///
    /// - Map 680000110: Cathedral wedding (NPC 9201011)
    /// - Map 680000210: Chapel wedding (NPC 9201002)
    /// - OnWeddingProgress (packet 379): Wedding step progress with dialogs
    /// - OnWeddingCeremonyEnd (packet 380): Ceremony completion with bless effect
    /// - SetBlessEffect: Sparkle animation between groom and bride positions
    /// </summary>
    public class WeddingField
    {
        #region State
        private bool _isActive = false;
        private int _mapId;
        private int _npcId;

        // Wedding step (from OnWeddingProgress)
        private int _currentStep = 0;
        private int _groomId = 0;
        private int _brideId = 0;

        // Bless effect (sparkles between couple)
        private bool _blessEffectActive = false;
        private float _blessEffectAlpha = 0f;
        private int _blessEffectStartTime;
        private const int BLESS_EFFECT_DURATION = 5000; // 5 seconds
        #endregion

        #region Visual Effects
        private List<WeddingSparkle> _sparkles = new();
        private List<IDXObject> _blessFrames;
        private Random _random = new();
        #endregion

        #region Dialog
        private WeddingDialog _currentDialog;
        private readonly Queue<WeddingDialog> _dialogQueue = new();
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int CurrentStep => _currentStep;
        public bool IsBlessEffectActive => _blessEffectActive;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
            // Load bless effect frames from Effect.wz if available
        }

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _currentStep = 0;

            // Set NPC based on map (from client: 680000110 = 9201011, 680000210 = 9201002)
            _npcId = mapId == 680000110 ? 9201011 : 9201002;

            System.Diagnostics.Debug.WriteLine($"[WeddingField] Enabled for map {mapId}, NPC {_npcId}");
        }

        public void SetBlessFrames(List<IDXObject> frames)
        {
            _blessFrames = frames;
        }
        #endregion

        #region Packet Handling (matching CField_Wedding)

        /// <summary>
        /// OnWeddingProgress - Packet 379
        /// Updates wedding step and shows dialog
        /// </summary>
        public void OnWeddingProgress(int step, int groomId, int brideId, int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine($"[WeddingField] OnWeddingProgress: step={step}, groom={groomId}, bride={brideId}");

            _currentStep = step;
            _groomId = groomId;
            _brideId = brideId;

            // Step 0: Start ceremony - play wedding BGM
            if (step == 0)
            {
                // Would trigger: CSoundMan::PlayBGM for wedding music
                // Cathedral: 0x108E, Chapel: 0x108F from StringPool
            }

            // Show dialog for current step
            ShowWeddingDialog(step, currentTimeMs);
        }

        /// <summary>
        /// OnWeddingCeremonyEnd - Packet 380
        /// Triggers the bless effect (sparkles)
        /// </summary>
        public void OnWeddingCeremonyEnd(int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine("[WeddingField] OnWeddingCeremonyEnd - Starting bless effect");
            SetBlessEffect(true, currentTimeMs);
        }

        /// <summary>
        /// SetBlessEffect - Start/stop sparkle effect between couple
        /// Based on client's CField_Wedding::SetBlessEffect
        /// </summary>
        public void SetBlessEffect(bool active, int currentTimeMs)
        {
            _blessEffectActive = active;
            if (active)
            {
                _blessEffectStartTime = currentTimeMs;
                _blessEffectAlpha = 0f;

                // Create sparkles
                _sparkles.Clear();
                for (int i = 0; i < 50; i++)
                {
                    _sparkles.Add(new WeddingSparkle
                    {
                        X = 0.5f + (float)(_random.NextDouble() - 0.5) * 0.3f,
                        Y = 0.5f + (float)(_random.NextDouble() - 0.5) * 0.2f,
                        Scale = 0.5f + (float)_random.NextDouble() * 0.5f,
                        Alpha = 0f,
                        Velocity = new Vector2((float)(_random.NextDouble() - 0.5f) * 0.02f, (float)_random.NextDouble() * 0.01f),
                        LifeTime = 2000 + _random.Next(3000),
                        SpawnDelay = i * 100
                    });
                }
            }
            else
            {
                _blessEffectAlpha = 0f;
                _sparkles.Clear();
            }
        }

        private void ShowWeddingDialog(int step, int currentTimeMs)
        {
            // Wedding dialog text would be loaded from String.wz/Npc.img/{npcId}/wedding{step}
            string dialogText = step switch
            {
                0 => "Welcome to the wedding ceremony!",
                1 => "Do you take this person as your beloved partner?",
                2 => "You may now kiss the bride!",
                _ => $"Wedding step {step}"
            };

            _currentDialog = new WeddingDialog
            {
                Message = dialogText,
                NpcId = _npcId,
                StartTime = currentTimeMs,
                Duration = 5000
            };
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

            // Update bless effect
            if (_blessEffectActive)
            {
                int elapsed = currentTimeMs - _blessEffectStartTime;

                // Fade in (first 500ms)
                if (elapsed < 500)
                {
                    _blessEffectAlpha = elapsed / 500f;
                }
                // Fade out (last 500ms)
                else if (elapsed > BLESS_EFFECT_DURATION - 500)
                {
                    _blessEffectAlpha = (BLESS_EFFECT_DURATION - elapsed) / 500f;
                }
                else
                {
                    _blessEffectAlpha = 1f;
                }

                // End effect
                if (elapsed >= BLESS_EFFECT_DURATION)
                {
                    _blessEffectActive = false;
                    _blessEffectAlpha = 0f;
                    _sparkles.Clear();
                }

                // Update sparkles
                foreach (var sparkle in _sparkles)
                {
                    if (elapsed < sparkle.SpawnDelay) continue;

                    int sparkleElapsed = elapsed - sparkle.SpawnDelay;
                    float lifeProgress = (float)sparkleElapsed / sparkle.LifeTime;

                    if (lifeProgress > 1f)
                    {
                        sparkle.Alpha = 0f;
                        continue;
                    }

                    // Fade in/out
                    if (lifeProgress < 0.2f)
                        sparkle.Alpha = lifeProgress / 0.2f;
                    else if (lifeProgress > 0.8f)
                        sparkle.Alpha = (1f - lifeProgress) / 0.2f;
                    else
                        sparkle.Alpha = 1f;

                    // Move
                    sparkle.X += sparkle.Velocity.X * deltaSeconds;
                    sparkle.Y += sparkle.Velocity.Y * deltaSeconds;
                }
            }

            // Update dialog
            if (_currentDialog != null)
            {
                if (currentTimeMs - _currentDialog.StartTime > _currentDialog.Duration)
                {
                    _currentDialog = null;
                    if (_dialogQueue.Count > 0)
                    {
                        _currentDialog = _dialogQueue.Dequeue();
                        _currentDialog.StartTime = currentTimeMs;
                    }
                }
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            // Draw bless effect sparkles
            if (_blessEffectActive && _blessEffectAlpha > 0)
            {
                int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
                int screenHeight = spriteBatch.GraphicsDevice.Viewport.Height;

                foreach (var sparkle in _sparkles)
                {
                    if (sparkle.Alpha <= 0) continue;

                    int x = (int)(sparkle.X * screenWidth);
                    int y = (int)(sparkle.Y * screenHeight);
                    int size = (int)(8 * sparkle.Scale);
                    byte alpha = (byte)(sparkle.Alpha * _blessEffectAlpha * 255);

                    // Draw sparkle (cross pattern)
                    Color sparkleColor = new Color((byte)255, (byte)255, (byte)200, alpha);
                    spriteBatch.Draw(pixelTexture, new Rectangle(x - size / 2, y - 1, size, 2), sparkleColor);
                    spriteBatch.Draw(pixelTexture, new Rectangle(x - 1, y - size / 2, 2, size), sparkleColor);
                }
            }

            // Draw current dialog
            if (_currentDialog != null && font != null)
            {
                DrawDialog(spriteBatch, pixelTexture, font);
            }

            // Debug info
            if (font != null)
            {
                string info = $"Wedding: Step {_currentStep} | Bless: {_blessEffectActive}";
                spriteBatch.DrawString(font, info, new Vector2(10, 10), Color.Pink);
            }
        }

        private void DrawDialog(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (_currentDialog == null) return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            Vector2 textSize = font.MeasureString(_currentDialog.Message);
            int boxWidth = (int)textSize.X + 40;
            int boxHeight = (int)textSize.Y + 20;
            int boxX = (screenWidth - boxWidth) / 2;
            int boxY = 100;

            // Calculate alpha based on time
            int elapsed = Environment.TickCount - _currentDialog.StartTime;
            float alpha = 1f;
            if (elapsed < 300)
                alpha = elapsed / 300f;
            else if (elapsed > _currentDialog.Duration - 300)
                alpha = (_currentDialog.Duration - elapsed) / 300f;

            Color bgColor = new Color(50, 50, 50, (int)(200 * alpha));
            Color textColor = new Color(255, 255, 255, (int)(255 * alpha));

            // Draw box
            spriteBatch.Draw(pixelTexture, new Rectangle(boxX, boxY, boxWidth, boxHeight), bgColor);

            // Draw text
            spriteBatch.DrawString(font, _currentDialog.Message, new Vector2(boxX + 20, boxY + 10), textColor);
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _currentStep = 0;
            _blessEffectActive = false;
            _sparkles.Clear();
            _currentDialog = null;
            _dialogQueue.Clear();
        }
        #endregion
    }

    public class WeddingSparkle
    {
        public float X, Y;
        public float Scale;
        public float Alpha;
        public Vector2 Velocity;
        public int LifeTime;
        public int SpawnDelay;
    }

    public class WeddingDialog
    {
        public string Message;
        public int NpcId;
        public int StartTime;
        public int Duration;
    }
    #endregion

    #region Witchtower Field (CField_Witchtower)
    /// <summary>
    /// Witchtower Field - Score tracking for witchtower event.
    ///
    /// - OnScoreUpdate (packet 358): Update score display
    /// - CScoreboard_Witchtower: UI widget at position (-57, 92, 115, 36)
    /// </summary>
    public class WitchtowerField
    {
        #region State
        private bool _isActive = false;
        private int _score = 0;
        private int _targetScore = 100; // Goal score
        #endregion

        #region Scoreboard Position (from client: CWnd::CreateWnd position)
        private const int SCOREBOARD_X = 10;  // Adjusted from -57 (relative positioning)
        private const int SCOREBOARD_Y = 92;
        private const int SCOREBOARD_WIDTH = 115;
        private const int SCOREBOARD_HEIGHT = 36;
        #endregion

        #region Animation
        private float _scoreDisplayValue = 0f;
        private int _lastScoreUpdateTime = 0;
        private bool _scoreAnimating = false;
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int Score => _score;
        public float ScoreProgress => Math.Clamp((float)_score / _targetScore, 0f, 1f);
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable()
        {
            _isActive = true;
            _score = 0;
            _scoreDisplayValue = 0f;
        }

        public void SetTargetScore(int target)
        {
            _targetScore = Math.Max(1, target);
        }
        #endregion

        #region Packet Handling

        /// <summary>
        /// OnScoreUpdate - Packet 358
        /// From client: this->m_pScoreboard.p->m_nScore = Decode1(iPacket);
        /// </summary>
        public void OnScoreUpdate(int newScore, int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine($"[WitchtowerField] OnScoreUpdate: {_score} -> {newScore}");
            _score = newScore;
            _lastScoreUpdateTime = currentTimeMs;
            _scoreAnimating = true;
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

            // Animate score display
            if (_scoreAnimating)
            {
                float targetValue = _score;
                float diff = targetValue - _scoreDisplayValue;
                if (Math.Abs(diff) < 0.5f)
                {
                    _scoreDisplayValue = targetValue;
                    _scoreAnimating = false;
                }
                else
                {
                    _scoreDisplayValue += diff * 5f * deltaSeconds;
                }
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            // Draw scoreboard background
            Color bgColor = new Color(40, 40, 60, 200);
            Color borderColor = new Color(100, 100, 150, 255);
            Color progressBgColor = new Color(30, 30, 40, 200);
            Color progressColor = new Color(100, 200, 100, 255);

            // Background
            spriteBatch.Draw(pixelTexture, new Rectangle(SCOREBOARD_X, SCOREBOARD_Y, SCOREBOARD_WIDTH, SCOREBOARD_HEIGHT), bgColor);

            // Border
            spriteBatch.Draw(pixelTexture, new Rectangle(SCOREBOARD_X, SCOREBOARD_Y, SCOREBOARD_WIDTH, 2), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(SCOREBOARD_X, SCOREBOARD_Y + SCOREBOARD_HEIGHT - 2, SCOREBOARD_WIDTH, 2), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(SCOREBOARD_X, SCOREBOARD_Y, 2, SCOREBOARD_HEIGHT), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(SCOREBOARD_X + SCOREBOARD_WIDTH - 2, SCOREBOARD_Y, 2, SCOREBOARD_HEIGHT), borderColor);

            // Progress bar
            int progressY = SCOREBOARD_Y + SCOREBOARD_HEIGHT - 10;
            int progressWidth = SCOREBOARD_WIDTH - 10;
            spriteBatch.Draw(pixelTexture, new Rectangle(SCOREBOARD_X + 5, progressY, progressWidth, 6), progressBgColor);
            int filledWidth = (int)(progressWidth * ScoreProgress);
            spriteBatch.Draw(pixelTexture, new Rectangle(SCOREBOARD_X + 5, progressY, filledWidth, 6), progressColor);

            // Score text
            if (font != null)
            {
                string scoreText = $"Score: {(int)_scoreDisplayValue}";
                Vector2 textPos = new Vector2(SCOREBOARD_X + 8, SCOREBOARD_Y + 5);
                spriteBatch.DrawString(font, scoreText, textPos + new Vector2(1, 1), Color.Black);
                spriteBatch.DrawString(font, scoreText, textPos, Color.White);
            }
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _score = 0;
            _scoreDisplayValue = 0f;
        }
        #endregion
    }
    #endregion

    #region GuildBoss Field (CField_GuildBoss)
    /// <summary>
    /// GuildBoss Field - Healer and pulley mechanics for guild boss fights.
    ///
    /// - OnHealerMove (packet 344): Move healer NPC vertically
    /// - OnPulleyStateChange (packet 345): Change pulley interaction state
    /// - CHealer: Animated healing NPC that moves on Y axis
    /// - CPulley: Interactive area for pulley mechanic (rope-based)
    /// </summary>
    public class GuildBossField
    {
        #region State
        private bool _isActive = false;
        private int _pulleyState = 0; // 0 = idle, 1 = activating, 2 = active
        #endregion

        #region Healer (from CHealer class)
        private bool _healerEnabled = false;
        private float _healerX;
        private float _healerY;
        private float _healerTargetY;
        private float _healerMoveSpeed = 100f;
        private List<IDXObject> _healerFrames;
        private int _healerFrameIndex = 0;
        private int _lastHealerFrameTime = 0;
        #endregion

        #region Pulley (from CPulley class)
        private bool _pulleyEnabled = false;
        private Rectangle _pulleyArea; // From CPulley::Init: (x-186, y+90, x-60, y+184)
        private float _pulleyX;
        private float _pulleyY;
        private List<IDXObject> _pulleyFrames;
        #endregion

        #region Heal Effect
        private bool _healEffectActive = false;
        private float _healEffectAlpha = 0f;
        private int _healEffectStartTime;
        private List<HealParticle> _healParticles = new();
        private Random _random = new();
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int PulleyState => _pulleyState;
        public float HealerY => _healerY;
        public bool IsHealEffectActive => _healEffectActive;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable()
        {
            _isActive = true;
            _pulleyState = 0;
        }

        /// <summary>
        /// Initialize healer from map properties
        /// From CField_GuildBoss::Init / CHealer::Init
        /// </summary>
        public void InitHealer(int x, int yMin, string healerPath)
        {
            _healerEnabled = true;
            _healerX = x;
            _healerY = yMin;
            _healerTargetY = yMin;
            System.Diagnostics.Debug.WriteLine($"[GuildBossField] Healer initialized at ({x}, {yMin}), path: {healerPath}");
        }

        /// <summary>
        /// Initialize pulley from map properties
        /// From CField_GuildBoss::Init / CPulley::Init
        /// </summary>
        public void InitPulley(int x, int y, string pulleyPath)
        {
            _pulleyEnabled = true;
            _pulleyX = x;
            _pulleyY = y;
            // Pulley area from client: (x-186, y+90) to (x-60, y+184)
            _pulleyArea = new Rectangle(x - 186, y + 90, 126, 94);
            System.Diagnostics.Debug.WriteLine($"[GuildBossField] Pulley initialized at ({x}, {y}), area: {_pulleyArea}");
        }

        public void SetHealerFrames(List<IDXObject> frames)
        {
            _healerFrames = frames;
        }

        public void SetPulleyFrames(List<IDXObject> frames)
        {
            _pulleyFrames = frames;
        }
        #endregion

        #region Packet Handling

        /// <summary>
        /// OnHealerMove - Packet 344
        /// From client: CHealer::Move(&this->m_healer, v3 - this->m_nY)
        ///              this->m_nY = v3
        /// </summary>
        public void OnHealerMove(int newY, int currentTimeMs)
        {
            if (!_healerEnabled) return;

            System.Diagnostics.Debug.WriteLine($"[GuildBossField] OnHealerMove: {_healerY} -> {newY}");
            _healerTargetY = newY;

            // Trigger heal effect when healer moves up
            if (newY < _healerY)
            {
                TriggerHealEffect(currentTimeMs);
            }
        }

        /// <summary>
        /// OnPulleyStateChange - Packet 345
        /// From client: this->m_nState = Decode1(iPacket)
        /// </summary>
        public void OnPulleyStateChange(int newState, int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine($"[GuildBossField] OnPulleyStateChange: {_pulleyState} -> {newState}");
            _pulleyState = newState;
        }

        private void TriggerHealEffect(int currentTimeMs)
        {
            _healEffectActive = true;
            _healEffectStartTime = currentTimeMs;
            _healEffectAlpha = 1f;

            // Create heal particles rising from healer
            _healParticles.Clear();
            for (int i = 0; i < 20; i++)
            {
                _healParticles.Add(new HealParticle
                {
                    X = _healerX + (float)(_random.NextDouble() - 0.5) * 100,
                    Y = _healerY,
                    VelocityY = -50f - (float)_random.NextDouble() * 50f,
                    VelocityX = (float)(_random.NextDouble() - 0.5) * 20f,
                    Alpha = 1f,
                    LifeTime = 1000 + _random.Next(1000),
                    SpawnDelay = i * 50
                });
            }
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

            // Move healer towards target
            if (_healerEnabled)
            {
                float diff = _healerTargetY - _healerY;
                if (Math.Abs(diff) > 0.5f)
                {
                    float move = Math.Sign(diff) * Math.Min(Math.Abs(diff), _healerMoveSpeed * deltaSeconds);
                    _healerY += move;
                }
            }

            // Update heal effect
            if (_healEffectActive)
            {
                int elapsed = currentTimeMs - _healEffectStartTime;
                bool anyAlive = false;

                foreach (var particle in _healParticles)
                {
                    if (elapsed < particle.SpawnDelay) continue;

                    int particleElapsed = elapsed - particle.SpawnDelay;
                    float lifeProgress = (float)particleElapsed / particle.LifeTime;

                    if (lifeProgress > 1f)
                    {
                        particle.Alpha = 0f;
                        continue;
                    }

                    anyAlive = true;
                    particle.Alpha = 1f - lifeProgress;
                    particle.Y += particle.VelocityY * deltaSeconds;
                    particle.X += particle.VelocityX * deltaSeconds;
                }

                if (!anyAlive && elapsed > 100)
                {
                    _healEffectActive = false;
                    _healParticles.Clear();
                }
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            int shiftCenterX = mapShiftX - centerX;
            int shiftCenterY = mapShiftY - centerY;

            // Draw healer
            if (_healerEnabled)
            {
                int healerScreenX = (int)_healerX - shiftCenterX;
                int healerScreenY = (int)_healerY - shiftCenterY;

                // Debug rectangle for healer
                spriteBatch.Draw(pixelTexture, new Rectangle(healerScreenX - 20, healerScreenY - 40, 40, 60),
                    new Color(100, 200, 100, 150));

                if (font != null)
                {
                    spriteBatch.DrawString(font, "Healer", new Vector2(healerScreenX - 20, healerScreenY - 55), Color.LightGreen);
                }
            }

            // Draw pulley
            if (_pulleyEnabled)
            {
                int pulleyScreenX = (int)_pulleyX - shiftCenterX;
                int pulleyScreenY = (int)_pulleyY - shiftCenterY;

                // Draw pulley area
                Rectangle screenArea = new Rectangle(
                    _pulleyArea.X - shiftCenterX,
                    _pulleyArea.Y - shiftCenterY,
                    _pulleyArea.Width,
                    _pulleyArea.Height);

                Color pulleyColor = _pulleyState switch
                {
                    0 => new Color(100, 100, 200, 100),
                    1 => new Color(200, 200, 100, 150),
                    2 => new Color(100, 200, 100, 150),
                    _ => new Color(100, 100, 100, 100)
                };

                spriteBatch.Draw(pixelTexture, screenArea, pulleyColor);

                if (font != null)
                {
                    string stateText = _pulleyState switch
                    {
                        0 => "Idle",
                        1 => "Activating",
                        2 => "Active",
                        _ => $"State {_pulleyState}"
                    };
                    spriteBatch.DrawString(font, $"Pulley: {stateText}",
                        new Vector2(screenArea.X, screenArea.Y - 15), Color.LightBlue);
                }
            }

            // Draw heal particles
            if (_healEffectActive)
            {
                foreach (var particle in _healParticles)
                {
                    if (particle.Alpha <= 0) continue;

                    int px = (int)particle.X - shiftCenterX;
                    int py = (int)particle.Y - shiftCenterY;
                    byte alpha = (byte)(particle.Alpha * 255);

                    spriteBatch.Draw(pixelTexture, new Rectangle(px - 3, py - 3, 6, 6),
                        new Color((byte)100, (byte)255, (byte)100, alpha));
                }
            }

            // Debug info
            if (font != null)
            {
                string info = $"GuildBoss: Pulley={_pulleyState} | Healer Y={_healerY:F0}";
                spriteBatch.DrawString(font, info, new Vector2(10, 60), Color.LightBlue);
            }
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _pulleyState = 0;
            _healerEnabled = false;
            _pulleyEnabled = false;
            _healEffectActive = false;
            _healParticles.Clear();
        }
        #endregion
    }

    public class HealParticle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public float Alpha;
        public int LifeTime;
        public int SpawnDelay;
    }
    #endregion

    #region Massacre Field (CField_Massacre)
    /// <summary>
    /// Massacre Field - Kill counting and gauge system for hunting events.
    ///
    /// - OnMassacreIncGauge (packet 173): Update gauge value
    /// - m_nIncGauge: Current gauge increase amount
    /// - m_nTimer: Timer countdown
    /// - m_nGaugeDec: Gauge decrease rate over time
    /// - Clear effect on timer expiry
    /// </summary>
    public class MassacreField
    {
        #region State
        private bool _isActive = false;

        // Gauge system (from CField_Massacre)
        private int _incGauge = 0;          // m_nIncGauge
        private int _gaugeDec = 1;          // m_nGaugeDec (decrease rate per second)
        private int _currentGauge = 0;
        private int _maxGauge = 100;

        // Timer (from m_pTimerboard)
        private int _timer = 0;             // m_nTimer
        private int _timerRemain = 0;       // m_tRemain
        private int _startTime;
        private bool _showedClearEffect = false;
        #endregion

        #region Display
        private float _displayGauge = 0f;
        private int _killCount = 0;
        private int _comboCount = 0;
        private int _lastKillTime = 0;
        private const int COMBO_TIMEOUT = 3000;

        // UI positions
        private const int GAUGE_X = 10;
        private const int GAUGE_Y = 130;
        private const int GAUGE_WIDTH = 200;
        private const int GAUGE_HEIGHT = 20;
        #endregion

        #region Effects
        private bool _clearEffectActive = false;
        private float _clearEffectAlpha = 0f;
        private int _clearEffectStartTime;
        #endregion

        #region Public Properties
        public bool IsActive => _isActive;
        public int CurrentGauge => _currentGauge;
        public float GaugeProgress => Math.Clamp((float)_currentGauge / _maxGauge, 0f, 1f);
        public int KillCount => _killCount;
        public int ComboCount => _comboCount;
        public int TimerRemain => _timerRemain;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device)
        {
        }

        public void Enable()
        {
            _isActive = true;
            _currentGauge = 0;
            _incGauge = 0;
            _killCount = 0;
            _comboCount = 0;
            _showedClearEffect = false;
        }

        /// <summary>
        /// Set massacre parameters
        /// </summary>
        public void SetParameters(int maxGauge, int timer, int gaugeDec)
        {
            _maxGauge = Math.Max(1, maxGauge);
            _timer = timer;
            _timerRemain = timer;
            _gaugeDec = gaugeDec;
            _startTime = Environment.TickCount;
        }
        #endregion

        #region Packet Handling

        /// <summary>
        /// OnMassacreIncGauge - Packet 173
        /// From client: this->m_nIncGauge = Decode4(iPacket)
        /// </summary>
        public void OnMassacreIncGauge(int newIncGauge, int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine($"[MassacreField] OnMassacreIncGauge: {_incGauge} -> {newIncGauge}");

            int increase = newIncGauge - _incGauge;
            _incGauge = newIncGauge;

            // Update kill tracking
            if (increase > 0)
            {
                _killCount++;

                // Check combo
                if (currentTimeMs - _lastKillTime < COMBO_TIMEOUT)
                {
                    _comboCount++;
                }
                else
                {
                    _comboCount = 1;
                }
                _lastKillTime = currentTimeMs;
            }
        }

        /// <summary>
        /// Add kills directly (for testing/simulation)
        /// </summary>
        public void AddKill(int gaugeAmount, int currentTimeMs)
        {
            _incGauge += gaugeAmount;

            _killCount++;
            if (currentTimeMs - _lastKillTime < COMBO_TIMEOUT)
            {
                _comboCount++;
            }
            else
            {
                _comboCount = 1;
            }
            _lastKillTime = currentTimeMs;
        }
        #endregion

        #region Update
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive) return;

            // Update timer (from client's Update: m_pTimerboard.p->m_tRemain)
            if (_timer > 0)
            {
                int elapsed = currentTimeMs - _startTime;
                _timerRemain = Math.Max(0, _timer - elapsed / 1000);
            }

            // Calculate gauge with decay (from client's _SetDecGauge)
            // Formula: m_nGaugeDec * (m_nTimer - m_tRemain) - m_nIncGauge
            int elapsedSeconds = _timer > 0 ? (_timer - _timerRemain) : 0;
            int decayAmount = _gaugeDec * elapsedSeconds;
            _currentGauge = Math.Max(0, _incGauge - decayAmount);
            _currentGauge = Math.Min(_currentGauge, _maxGauge);

            // Animate gauge display
            float targetGauge = _currentGauge;
            float diff = targetGauge - _displayGauge;
            _displayGauge += diff * 3f * deltaSeconds;

            // Check for clear effect (from client: if m_tRemain <= 1 && !m_bShowedClearEffect)
            if (!_showedClearEffect && _timerRemain <= 1 && _timer > 0)
            {
                TriggerClearEffect(currentTimeMs);
            }

            // Update clear effect
            if (_clearEffectActive)
            {
                int clearElapsed = currentTimeMs - _clearEffectStartTime;
                if (clearElapsed > 3000)
                {
                    _clearEffectActive = false;
                }
                else
                {
                    // Flash effect
                    _clearEffectAlpha = (float)(0.5f + 0.5f * Math.Sin(clearElapsed * 0.01));
                }
            }

            // Reset combo if timed out
            if (currentTimeMs - _lastKillTime > COMBO_TIMEOUT)
            {
                _comboCount = 0;
            }
        }

        private void TriggerClearEffect(int currentTimeMs)
        {
            System.Diagnostics.Debug.WriteLine("[MassacreField] Clear effect triggered!");
            _showedClearEffect = true;
            _clearEffectActive = true;
            _clearEffectStartTime = currentTimeMs;
            // Would call: CField::ShowScreenEffect(this, "clear effect path")
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || pixelTexture == null) return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;

            // Draw gauge bar
            Color bgColor = new Color(40, 40, 40, 200);
            Color gaugeColor = GetGaugeColor();
            Color borderColor = new Color(100, 100, 100, 255);

            // Background
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, GAUGE_WIDTH, GAUGE_HEIGHT), bgColor);

            // Gauge fill
            int fillWidth = (int)(GAUGE_WIDTH * GaugeProgress);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, fillWidth, GAUGE_HEIGHT), gaugeColor);

            // Border
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, GAUGE_WIDTH, 2), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y + GAUGE_HEIGHT - 2, GAUGE_WIDTH, 2), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X, GAUGE_Y, 2, GAUGE_HEIGHT), borderColor);
            spriteBatch.Draw(pixelTexture, new Rectangle(GAUGE_X + GAUGE_WIDTH - 2, GAUGE_Y, 2, GAUGE_HEIGHT), borderColor);

            // Text
            if (font != null)
            {
                // Gauge value
                string gaugeText = $"{_currentGauge}/{_maxGauge}";
                Vector2 textPos = new Vector2(GAUGE_X + GAUGE_WIDTH + 10, GAUGE_Y + 2);
                spriteBatch.DrawString(font, gaugeText, textPos, Color.White);

                // Kill count
                string killText = $"Kills: {_killCount}";
                spriteBatch.DrawString(font, killText, new Vector2(GAUGE_X, GAUGE_Y + GAUGE_HEIGHT + 5), Color.LightGray);

                // Combo
                if (_comboCount > 1)
                {
                    string comboText = $"{_comboCount}x COMBO!";
                    Color comboColor = _comboCount >= 10 ? Color.Gold :
                                       _comboCount >= 5 ? Color.Orange : Color.Yellow;
                    spriteBatch.DrawString(font, comboText, new Vector2(GAUGE_X + 100, GAUGE_Y + GAUGE_HEIGHT + 5), comboColor);
                }

                // Timer
                if (_timer > 0)
                {
                    int minutes = _timerRemain / 60;
                    int seconds = _timerRemain % 60;
                    string timerText = $"Time: {minutes}:{seconds:D2}";
                    Color timerColor = _timerRemain <= 10 ? Color.Red : Color.White;
                    Vector2 timerPos = new Vector2(screenWidth - 100, GAUGE_Y);
                    spriteBatch.DrawString(font, timerText, timerPos, timerColor);
                }
            }

            // Clear effect overlay
            if (_clearEffectActive)
            {
                Color clearColor = new Color(255, 255, 255, (int)(_clearEffectAlpha * 100));
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(0, 0, screenWidth, spriteBatch.GraphicsDevice.Viewport.Height),
                    clearColor);

                if (font != null)
                {
                    string clearText = "CLEAR!";
                    Vector2 clearSize = font.MeasureString(clearText) * 2;
                    Vector2 clearPos = new Vector2((screenWidth - clearSize.X) / 2, 200);
                    spriteBatch.DrawString(font, clearText, clearPos + new Vector2(3, 3), Color.Black);
                    spriteBatch.DrawString(font, clearText, clearPos, Color.Gold);
                }
            }
        }

        private Color GetGaugeColor()
        {
            float progress = GaugeProgress;
            if (progress >= 0.8f)
                return new Color(255, 215, 0, 255); // Gold
            else if (progress >= 0.5f)
                return new Color(100, 200, 100, 255); // Green
            else if (progress >= 0.25f)
                return new Color(200, 200, 100, 255); // Yellow
            else
                return new Color(200, 100, 100, 255); // Red
        }
        #endregion

        #region Reset
        public void Reset()
        {
            _isActive = false;
            _currentGauge = 0;
            _incGauge = 0;
            _killCount = 0;
            _comboCount = 0;
            _showedClearEffect = false;
            _clearEffectActive = false;
        }
        #endregion
    }
    #endregion
}
