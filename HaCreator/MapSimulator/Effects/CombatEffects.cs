using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.Effects
{
    #region Mob HP Bar Display
    /// <summary>
    /// Regular mob HP bar display - shows above the mob when damaged
    /// Based on MapleStory client mob HP indicator system
    /// </summary>
    public class MobHPBarDisplay
    {
        public MobItem Mob { get; set; }
        public float LastHpPercent { get; set; }
        public int LastDamageTime { get; set; }
        public float Alpha { get; set; } = 1.0f;
        public bool IsVisible { get; set; } = true;
        public int MobHeight { get; set; } = 60;      // Height of mob sprite for positioning

        // Animation constants
        public const int DISPLAY_DURATION = 5000;     // How long bar stays visible after damage (ms)
        public const int FADE_DURATION = 500;         // Fade out duration (ms)
        public const int BAR_WIDTH = 60;              // Default bar width
        public const int BAR_HEIGHT = 5;              // Default bar height

        public bool ShouldFade(int currentTime)
        {
            return currentTime - LastDamageTime > DISPLAY_DURATION - FADE_DURATION;
        }

        public bool IsExpired(int currentTime)
        {
            return currentTime - LastDamageTime > DISPLAY_DURATION;
        }

        public void UpdateAlpha(int currentTime)
        {
            if (ShouldFade(currentTime))
            {
                int fadeElapsed = (currentTime - LastDamageTime) - (DISPLAY_DURATION - FADE_DURATION);
                Alpha = 1.0f - ((float)fadeElapsed / FADE_DURATION);
                if (Alpha < 0) Alpha = 0;
            }
            else
            {
                Alpha = 1.0f;
            }
        }
    }
    #endregion

    #region Boss HP Bar Display
    /// <summary>
    /// Boss HP bar display - large HP gauge at top of screen
    /// Based on MapleStory client CBossGauge
    /// </summary>
    public class BossHPBarDisplay
    {
        public MobItem Boss { get; set; }
        public string BossName { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public float DisplayedHpPercent { get; set; }   // Animated HP percent
        public float TargetHpPercent { get; set; }      // Actual HP percent
        public int LastDamageTime { get; set; }
        public bool IsVisible { get; set; } = true;
        public float Alpha { get; set; } = 1.0f;

        // Animation
        public const float HP_ANIMATION_SPEED = 0.02f;  // How fast HP bar animates
        public const int BAR_WIDTH = 400;               // Boss bar width
        public const int BAR_HEIGHT = 20;               // Boss bar height
        public const int BAR_Y_OFFSET = 40;             // Distance from top of screen

        // Tag colors (based on boss difficulty)
        public Color TagColor { get; set; } = Color.Orange;

        public void UpdateAnimation(float deltaTime)
        {
            // Smoothly animate displayed HP towards target HP
            if (Math.Abs(DisplayedHpPercent - TargetHpPercent) > 0.001f)
            {
                if (DisplayedHpPercent > TargetHpPercent)
                {
                    DisplayedHpPercent -= HP_ANIMATION_SPEED;
                    if (DisplayedHpPercent < TargetHpPercent)
                        DisplayedHpPercent = TargetHpPercent;
                }
                else
                {
                    DisplayedHpPercent += HP_ANIMATION_SPEED * 2; // Heal animation faster
                    if (DisplayedHpPercent > TargetHpPercent)
                        DisplayedHpPercent = TargetHpPercent;
                }
            }
        }
    }

    /// <summary>
    /// Multiple boss HP tag for boss health phases or multiple bosses
    /// </summary>
    public class BossHPTag
    {
        public MobItem Boss { get; set; }
        public string Name { get; set; }
        public Color Color { get; set; }
        public int PhaseIndex { get; set; }
        public bool IsActive { get; set; }
    }
    #endregion


    /// <summary>
    /// Damage number display - floating damage text that rises and fades
    /// </summary>
    public class DamageNumberDisplay
    {
        public int Damage { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float StartY { get; set; }
        public int SpawnTime { get; set; }
        public bool IsCritical { get; set; }
        public bool IsMiss { get; set; }
        public float Alpha { get; set; } = 1.0f;
        public float Scale { get; set; } = 1.0f;
        public int ComboIndex { get; set; } = 0;    // For stacking multiple hits

        // Animation constants
        public const int DISPLAY_DURATION = 1500;   // Total display time in ms
        public const int RISE_DURATION = 400;       // Time to rise in ms
        public const int FADE_START = 1000;         // When to start fading
        public const float RISE_DISTANCE = 50f;     // How far to rise
        public const float COMBO_OFFSET = 20f;      // Horizontal offset per combo hit

        public bool IsExpired(int currentTime) => currentTime - SpawnTime > DISPLAY_DURATION;

        public void Update(int currentTime)
        {
            int elapsed = currentTime - SpawnTime;

            // Rise animation (ease out)
            if (elapsed < RISE_DURATION)
            {
                float t = (float)elapsed / RISE_DURATION;
                float eased = 1f - (1f - t) * (1f - t); // Ease out quad
                Y = StartY - (RISE_DISTANCE * eased);
            }
            else
            {
                Y = StartY - RISE_DISTANCE;
            }

            // Fade out
            if (elapsed > FADE_START)
            {
                float fadeT = (float)(elapsed - FADE_START) / (DISPLAY_DURATION - FADE_START);
                Alpha = 1f - fadeT;
            }

            // Critical scale pulse
            if (IsCritical && elapsed < 200)
            {
                float pulseT = (float)elapsed / 200f;
                Scale = 1.2f - (0.2f * pulseT); // Start big, shrink to normal
            }
        }
    }

    /// <summary>
    /// Hit effect display - plays hit animation at mob position
    /// </summary>
    public class HitEffectDisplay
    {
        public float X { get; set; }
        public float Y { get; set; }
        public int SpawnTime { get; set; }
        public List<IDXObject> Frames { get; set; }
        public int CurrentFrame { get; set; }
        public int LastFrameTime { get; set; }
        public bool Flip { get; set; }
        public Color Tint { get; set; } = Color.White;

        public bool IsComplete { get; private set; }

        public void Update(int currentTime)
        {
            if (IsComplete || Frames == null || Frames.Count == 0)
                return;

            var frame = Frames[CurrentFrame];
            int delay = frame?.Delay ?? 100;

            if (currentTime - LastFrameTime >= delay)
            {
                CurrentFrame++;
                LastFrameTime = currentTime;

                if (CurrentFrame >= Frames.Count)
                {
                    IsComplete = true;
                }
            }
        }
    }

    /// <summary>
    /// Mob skill hit effect display - plays the "affected" animation from MobSkill.img on the player
    /// when hit by a mob's skill attack. This is the visual effect that shows status effects like
    /// slow, seal, darkness, etc. being applied to the player.
    /// </summary>
    public class MobSkillHitEffectDisplay
    {
        /// <summary>X position (player's X)</summary>
        public float X { get; set; }

        /// <summary>Y position (player's Y)</summary>
        public float Y { get; set; }

        /// <summary>Time when effect was spawned</summary>
        public int SpawnTime { get; set; }

        /// <summary>Animation frames for the affected effect</summary>
        public List<IDXObject> Frames { get; set; }

        /// <summary>Current frame index</summary>
        public int CurrentFrame { get; set; }

        /// <summary>Time when last frame was displayed</summary>
        public int LastFrameTime { get; set; }

        /// <summary>Whether to flip the effect horizontally</summary>
        public bool Flip { get; set; }

        /// <summary>Color tint for the effect</summary>
        public Color Tint { get; set; } = Color.White;

        /// <summary>Mob skill ID that caused this effect</summary>
        public int SkillId { get; set; }

        /// <summary>Skill level</summary>
        public int SkillLevel { get; set; }

        /// <summary>Whether the animation should repeat</summary>
        public bool Repeat { get; set; }

        /// <summary>Total duration for repeating effects (in ms)</summary>
        public int Duration { get; set; }

        /// <summary>Number of times the animation has looped</summary>
        public int LoopCount { get; set; }

        /// <summary>Whether the effect has completed</summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Update the animation frame
        /// </summary>
        public void Update(int currentTime)
        {
            if (IsComplete || Frames == null || Frames.Count == 0)
                return;

            // Check if repeating effect has expired
            if (Repeat && Duration > 0 && currentTime - SpawnTime > Duration)
            {
                IsComplete = true;
                return;
            }

            var frame = Frames[CurrentFrame];
            int delay = frame?.Delay ?? 100;

            if (currentTime - LastFrameTime >= delay)
            {
                CurrentFrame++;
                LastFrameTime = currentTime;

                if (CurrentFrame >= Frames.Count)
                {
                    if (Repeat)
                    {
                        // Loop the animation
                        CurrentFrame = 0;
                        LoopCount++;
                    }
                    else
                    {
                        IsComplete = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Death effect display - smoke/particle burst when mob dies
    /// </summary>
    public class DeathEffectDisplay
    {
        public float X { get; set; }
        public float Y { get; set; }
        public int SpawnTime { get; set; }
        public MobDeathType DeathType { get; set; }
        public float Alpha { get; set; } = 1.0f;
        public float Scale { get; set; } = 1.0f;
        public bool IsBoss { get; set; }

        // Animation state
        public List<IDXObject> Frames { get; set; }
        public int CurrentFrame { get; set; }
        public int LastFrameTime { get; set; }

        // Particle burst state
        public List<DeathParticle> Particles { get; set; } = new();

        public const int EFFECT_DURATION = 1500;
        public const int FADE_START = 800;

        public bool IsComplete { get; private set; }

        public void Update(int currentTime, float deltaTime)
        {
            int elapsed = currentTime - SpawnTime;

            // Update frame animation if using frames
            if (Frames != null && Frames.Count > 0 && CurrentFrame < Frames.Count)
            {
                var frame = Frames[CurrentFrame];
                int delay = frame?.Delay ?? 100;

                if (currentTime - LastFrameTime >= delay)
                {
                    CurrentFrame++;
                    LastFrameTime = currentTime;
                }
            }

            // Update particles with actual delta time for frame-rate independence
            foreach (var particle in Particles)
            {
                particle.Update(deltaTime);
            }
            Particles.RemoveAll(p => p.Alpha <= 0);

            // Fade out
            if (elapsed > FADE_START)
            {
                float fadeT = (float)(elapsed - FADE_START) / (EFFECT_DURATION - FADE_START);
                Alpha = 1f - fadeT;
            }

            // Scale pulse for boss death
            if (IsBoss && elapsed < 300)
            {
                float pulseT = (float)elapsed / 300f;
                Scale = 1.5f - (0.5f * pulseT);
            }

            if (elapsed > EFFECT_DURATION)
            {
                IsComplete = true;
            }
        }
    }

    /// <summary>
    /// Single particle in death effect burst
    /// </summary>
    public class DeathParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float Alpha { get; set; } = 1.0f;
        public float Size { get; set; } = 4f;
        public Color Color { get; set; } = Color.White;
        public float Gravity { get; set; } = 150f;
        public float FadeSpeed { get; set; } = 1.5f;

        public void Update(float deltaTime)
        {
            X += VelocityX * deltaTime;
            Y += VelocityY * deltaTime;
            VelocityY += Gravity * deltaTime;
            Alpha -= FadeSpeed * deltaTime;
            Size *= 0.98f;
        }
    }

    /// <summary>
    /// Combat Effects System - Handles damage numbers, hit effects, and combat visuals
    /// </summary>
    public class CombatEffects
    {
        #region Constants
        private const int MAX_DAMAGE_DISPLAYS = 50;
        private const int MAX_HIT_EFFECTS = 30;
        private const int MAX_DEATH_EFFECTS = 20;
        private const int MAX_MOB_HP_BARS = 100;
        private const int MAX_MOB_SKILL_HIT_EFFECTS = 10;  // Max mob skill effects on player at once

        // Damage number colors
        private static readonly Color COLOR_NORMAL = new Color(255, 255, 255);      // White
        private static readonly Color COLOR_CRITICAL = new Color(255, 170, 0);      // Orange
        private static readonly Color COLOR_MISS = new Color(180, 180, 180);        // Gray
        private static readonly Color COLOR_HEAL = new Color(0, 255, 100);          // Green
        private static readonly Color COLOR_MP = new Color(100, 150, 255);          // Blue

        // Damage number outline
        private static readonly Color COLOR_OUTLINE = new Color(0, 0, 0, 200);

        // HP bar colors
        private static readonly Color COLOR_HPBAR_BG = new Color(30, 30, 30, 220);             // Dark background
        private static readonly Color COLOR_HPBAR_BORDER = new Color(60, 60, 60, 255);         // Border
        private static readonly Color COLOR_HPBAR_HP = new Color(220, 50, 50, 255);            // Red HP
        private static readonly Color COLOR_HPBAR_HP_LOW = new Color(255, 100, 50, 255);       // Orange when low HP
        private static readonly Color COLOR_HPBAR_DAMAGE = new Color(255, 200, 100, 200);      // Damage animation
        private static readonly Color COLOR_BOSS_HP = new Color(200, 60, 60, 255);             // Boss red
        private static readonly Color COLOR_BOSS_HP_BG = new Color(40, 40, 50, 240);           // Boss bar bg
        private static readonly Color COLOR_BOSS_BORDER = new Color(150, 120, 60, 255);        // Gold border
        private static readonly Color COLOR_BOSS_NAME = new Color(255, 220, 100, 255);         // Gold name
        #endregion

        #region Collections
        private readonly List<DamageNumberDisplay> _damageNumbers = new();
        private readonly List<HitEffectDisplay> _hitEffects = new();
        private readonly List<DeathEffectDisplay> _deathEffects = new();
        private readonly List<MobSkillHitEffectDisplay> _mobSkillHitEffects = new();  // Mob skill effects on player
        private readonly Queue<DamageNumberDisplay> _damagePool = new();
        private readonly Random _random = new();

        // HP bar collections
        private readonly Dictionary<int, MobHPBarDisplay> _mobHPBars = new();  // Keyed by mob PoolId
        private readonly List<BossHPBarDisplay> _bossHPBars = new();           // Boss HP bars at top of screen
        #endregion

        #region Textures
        private Texture2D _pixelTexture;
        private SpriteFont _damageFont;
        private SpriteFont _criticalFont;
        private Dictionary<int, List<IDXObject>> _hitEffectFrames;  // Hit effect variations
        private List<IDXObject> _deathEffectFrames;
        #endregion

        #region State
        private GraphicsDevice _device;
        private bool _initialized = false;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device, SpriteFont font, SpriteFont criticalFont = null)
        {
            _device = device;
            _damageFont = font;
            _criticalFont = criticalFont ?? font;

            // Create 1x1 white pixel for outlines
            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            _hitEffectFrames = new Dictionary<int, List<IDXObject>>();
            _initialized = true;
        }

        /// <summary>
        /// Set hit effect frames for a specific variation (0 = default)
        /// </summary>
        public void SetHitEffectFrames(int variation, List<IDXObject> frames)
        {
            _hitEffectFrames[variation] = frames;
        }

        /// <summary>
        /// Set death effect frames
        /// </summary>
        public void SetDeathEffectFrames(List<IDXObject> frames)
        {
            _deathEffectFrames = frames;
        }
        #endregion

        #region Add Effects
        /// <summary>
        /// Add a damage number display at a position
        /// </summary>
        public void AddDamageNumber(int damage, float x, float y, bool isCritical, bool isMiss, int currentTime, int comboIndex = 0)
        {
            if (_damageNumbers.Count >= MAX_DAMAGE_DISPLAYS)
            {
                // Recycle oldest
                var oldest = _damageNumbers[0];
                _damageNumbers.RemoveAt(0);
                _damagePool.Enqueue(oldest);
            }

            var display = _damagePool.Count > 0 ? _damagePool.Dequeue() : new DamageNumberDisplay();
            display.Damage = damage;
            display.X = x + (comboIndex * DamageNumberDisplay.COMBO_OFFSET);
            display.Y = y;
            display.StartY = y;
            display.SpawnTime = currentTime;
            display.IsCritical = isCritical;
            display.IsMiss = isMiss;
            display.Alpha = 1.0f;
            display.Scale = isCritical ? 1.2f : 1.0f;
            display.ComboIndex = comboIndex;

            _damageNumbers.Add(display);
        }

        /// <summary>
        /// Add damage numbers from a mob's damage display list
        /// </summary>
        public void AddDamageFromMob(MobItem mob, int currentTime)
        {
            if (mob?.AI == null || mob.MovementInfo == null)
                return;

            float mobX = mob.MovementInfo.X;
            float mobY = mob.MovementInfo.Y - 30; // Above mob head

            int comboIndex = 0;
            foreach (var dmgInfo in mob.AI.DamageDisplays)
            {
                // Only add if not already added (check display time proximity)
                bool alreadyAdded = false;
                foreach (var existing in _damageNumbers)
                {
                    if (Math.Abs(existing.SpawnTime - dmgInfo.DisplayTime) < 50 &&
                        existing.Damage == dmgInfo.Damage)
                    {
                        alreadyAdded = true;
                        break;
                    }
                }

                if (!alreadyAdded)
                {
                    AddDamageNumber(dmgInfo.Damage, mobX, mobY, dmgInfo.IsCritical, false, dmgInfo.DisplayTime, comboIndex);
                    comboIndex++;
                }
            }
        }

        /// <summary>
        /// Add a hit effect at a position
        /// </summary>
        public void AddHitEffect(float x, float y, int currentTime, int variation = 0, bool flip = false, Color? tint = null)
        {
            if (_hitEffects.Count >= MAX_HIT_EFFECTS)
            {
                _hitEffects.RemoveAt(0);
            }

            if (!_hitEffectFrames.TryGetValue(variation, out var frames) || frames == null || frames.Count == 0)
            {
                // Try default variation
                if (!_hitEffectFrames.TryGetValue(0, out frames) || frames == null || frames.Count == 0)
                    return;
            }

            _hitEffects.Add(new HitEffectDisplay
            {
                X = x,
                Y = y,
                SpawnTime = currentTime,
                Frames = frames,
                CurrentFrame = 0,
                LastFrameTime = currentTime,
                Flip = flip,
                Tint = tint ?? Color.White
            });
        }

        /// <summary>
        /// Add a death effect when a mob dies
        /// </summary>
        public void AddDeathEffect(float x, float y, int currentTime, MobDeathType deathType = MobDeathType.Normal, bool isBoss = false)
        {
            if (_deathEffects.Count >= MAX_DEATH_EFFECTS)
            {
                _deathEffects.RemoveAt(0);
            }

            var effect = new DeathEffectDisplay
            {
                X = x,
                Y = y,
                SpawnTime = currentTime,
                DeathType = deathType,
                IsBoss = isBoss,
                Frames = _deathEffectFrames,
                CurrentFrame = 0,
                LastFrameTime = currentTime
            };

            // Create particle burst
            int particleCount = isBoss ? 30 : 15;
            CreateDeathParticles(effect, particleCount);

            _deathEffects.Add(effect);
        }

        /// <summary>
        /// Add death effect for a dying mob
        /// </summary>
        public void AddDeathEffectForMob(MobItem mob, int currentTime)
        {
            if (mob?.MovementInfo == null || mob.AI == null)
                return;

            float x = mob.MovementInfo.X;
            float y = mob.MovementInfo.Y - 20; // Slightly above ground
            bool isBoss = mob.AI.IsBoss;
            var deathType = mob.AI.DeathType;

            AddDeathEffect(x, y, currentTime, deathType, isBoss);
        }

        private void CreateDeathParticles(DeathEffectDisplay effect, int count)
        {
            // Choose particle color based on death type
            Color baseColor = effect.DeathType switch
            {
                MobDeathType.Bomb => new Color(255, 150, 50),      // Orange for explosion
                MobDeathType.Swallowed => new Color(150, 50, 200), // Purple for swallowed
                _ => new Color(200, 200, 220)                       // Light gray/white smoke
            };

            for (int i = 0; i < count; i++)
            {
                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                float speed = 50f + (float)(_random.NextDouble() * 150f);
                float size = effect.IsBoss ? 6f + (float)(_random.NextDouble() * 6f) : 3f + (float)(_random.NextDouble() * 4f);

                // Vary the color slightly
                int colorVariation = _random.Next(-30, 30);
                Color particleColor = new Color(
                    Math.Clamp(baseColor.R + colorVariation, 0, 255),
                    Math.Clamp(baseColor.G + colorVariation, 0, 255),
                    Math.Clamp(baseColor.B + colorVariation, 0, 255)
                );

                effect.Particles.Add(new DeathParticle
                {
                    X = effect.X + (float)(_random.NextDouble() * 20 - 10),
                    Y = effect.Y + (float)(_random.NextDouble() * 20 - 10),
                    VelocityX = MathF.Cos(angle) * speed,
                    VelocityY = MathF.Sin(angle) * speed - 100f, // Bias upward
                    Alpha = 0.8f + (float)(_random.NextDouble() * 0.2f),
                    Size = size,
                    Color = particleColor,
                    Gravity = 100f + (float)(_random.NextDouble() * 100f),
                    FadeSpeed = 0.8f + (float)(_random.NextDouble() * 0.5f)
                });
            }
        }

        /// <summary>
        /// Get death effect frames (for external use)
        /// </summary>
        public List<IDXObject> GetDeathEffectFrames()
        {
            return _deathEffectFrames;
        }

        /// <summary>
        /// Add a mob skill hit effect on the player.
        /// Called when a mob uses a skill to hit the player - displays the "affected" animation
        /// from MobSkill.img on the player's position.
        /// </summary>
        /// <param name="x">Player X position</param>
        /// <param name="y">Player Y position</param>
        /// <param name="frames">Animation frames from MobSkill.img affected property</param>
        /// <param name="skillId">Mob skill ID</param>
        /// <param name="skillLevel">Skill level</param>
        /// <param name="currentTime">Current game time</param>
        /// <param name="repeat">Whether to repeat the animation</param>
        /// <param name="duration">Total duration for repeating effects (in ms)</param>
        public void AddMobSkillHitEffect(float x, float y, List<IDXObject> frames, int skillId, int skillLevel,
            int currentTime, bool repeat = false, int duration = 0)
        {
            if (frames == null || frames.Count == 0)
                return;

            // Remove oldest effect if at max
            if (_mobSkillHitEffects.Count >= MAX_MOB_SKILL_HIT_EFFECTS)
            {
                _mobSkillHitEffects.RemoveAt(0);
            }

            _mobSkillHitEffects.Add(new MobSkillHitEffectDisplay
            {
                X = x,
                Y = y,
                Frames = frames,
                SkillId = skillId,
                SkillLevel = skillLevel,
                SpawnTime = currentTime,
                LastFrameTime = currentTime,
                CurrentFrame = 0,
                Repeat = repeat,
                Duration = duration,
                Tint = Color.White
            });

            System.Diagnostics.Debug.WriteLine($"[CombatEffects] Added mob skill hit effect: skill {skillId} level {skillLevel} at ({x}, {y}) with {frames.Count} frames");
        }

        /// <summary>
        /// Get active mob skill hit effects count
        /// </summary>
        public int ActiveMobSkillHitEffects => _mobSkillHitEffects.Count;

        /// <summary>
        /// Add an attack hit effect on the player.
        /// Called when a player is hit by a mob's regular attack (attack1, attack2, etc.)
        /// Displays the hit effect frames from the mob's attack/info/hit property.
        /// </summary>
        /// <param name="x">Player X position</param>
        /// <param name="y">Player Y position</param>
        /// <param name="frames">Hit effect frames from mob's attack/info/hit</param>
        /// <param name="currentTime">Current game time</param>
        public void AddAttackHitEffect(float x, float y, List<IDXObject> frames, int currentTime)
        {
            if (frames == null || frames.Count == 0)
                return;

            // Remove oldest effect if at max
            if (_mobSkillHitEffects.Count >= MAX_MOB_SKILL_HIT_EFFECTS)
            {
                _mobSkillHitEffects.RemoveAt(0);
            }

            // Reuse MobSkillHitEffectDisplay for attack hit effects (same functionality)
            _mobSkillHitEffects.Add(new MobSkillHitEffectDisplay
            {
                X = x,
                Y = y,
                Frames = frames,
                SkillId = 0,  // Not a skill, just a regular attack
                SkillLevel = 0,
                SpawnTime = currentTime,
                LastFrameTime = currentTime,
                CurrentFrame = 0,
                Repeat = false,
                Duration = 0,
                Tint = Color.White
            });

            System.Diagnostics.Debug.WriteLine($"[CombatEffects] Added attack hit effect at ({x}, {y}) with {frames.Count} frames");
        }
        #endregion

        #region WZ-based Boss HP Bar
        private BossHPBarUI _bossHPBarUI;
        private bool _useWzBossHPBar = false;

        /// <summary>
        /// Load boss HP bar textures from WZ files.
        /// Call after Initialize() to use WZ-based boss HP bars.
        /// </summary>
        /// <param name="uiWindowImage">UI.wz/UIWindow.img or UIWindow2.img</param>
        public void LoadBossHPBarFromWz(WzImage uiWindowImage)
        {
            if (uiWindowImage == null || _device == null)
                return;

            // Try to find MobGage property (boss HP bar UI)
            WzSubProperty mobGageProperty = uiWindowImage["MobGage"] as WzSubProperty;
            /*if (mobGageProperty == null)
            {
                // Try MobGage2 for newer clients
                mobGageProperty = uiWindowImage["MobGage2"] as WzSubProperty;
            }*/

            if (mobGageProperty != null)
            {
                _bossHPBarUI = new BossHPBarUI();
                _bossHPBarUI.Initialize(_device, _damageFont);
                _bossHPBarUI.LoadFromWz(mobGageProperty, _device);
                _useWzBossHPBar = true;
            }
        }

        /// <summary>
        /// Check if WZ-based boss HP bar is available
        /// </summary>
        public bool HasWzBossHPBar => _useWzBossHPBar && _bossHPBarUI != null;
        #endregion

        #region HP Bar Management
        /// <summary>
        /// Update or create HP bar for a mob when it takes damage
        /// </summary>
        /// <param name="mob">The mob that was damaged</param>
        /// <param name="currentTime">Current game tick</param>
        public void OnMobDamaged(MobItem mob, int currentTime)
        {
            if (mob?.AI == null)
                return;

            int poolId = mob.PoolId;

            // For boss mobs with hpTagColor > 0, manage the boss HP bar at top of screen
            // IMPORTANT: Only bosses with hpTagColor > 0 get the top-screen HP bar
            var mobData = mob.MobInstance?.MobInfo?.MobData;
            bool hasBossHpBar = mobData != null && mobData.HpTagColor > 0;

            if (hasBossHpBar)
            {
                // Track in WZ-based UI if available
                if (_useWzBossHPBar && _bossHPBarUI != null)
                {
                    _bossHPBarUI.OnBossDamaged(mob, currentTime);
                }

                // Also track in fallback system
                var existingBoss = _bossHPBars.Find(b => b.Boss?.PoolId == poolId);
                if (existingBoss != null)
                {
                    // Update existing boss HP bar
                    existingBoss.CurrentHp = mob.AI.CurrentHp;
                    existingBoss.TargetHpPercent = mob.AI.HpPercent;
                    existingBoss.LastDamageTime = currentTime;
                }
                else
                {
                    // Create new boss HP bar
                    AddBossHPBar(mob, currentTime);
                }
            }

            // Skip regular mob HP bar if this mob has a boss HP bar at top of screen
            // Bosses with hpTagColor only show the top-screen HP bar, not the regular one
            if (hasBossHpBar)
            {
                // Check if the WZ-based boss HP bar is tracking this mob
                if (_useWzBossHPBar && _bossHPBarUI != null && _bossHPBarUI.HasBossHPBar(poolId))
                    return;
                // Check if the fallback system is tracking this mob
                if (_bossHPBars.Exists(b => b.Boss?.PoolId == poolId))
                    return;
            }

            // Get mob height from current animation frame or use default
            int mobHeight = 60; // Default height
            var currentFrame = mob.GetCurrentFrame();
            if (currentFrame != null && currentFrame.Height > 0)
            {
                mobHeight = currentFrame.Height;
            }

            // Show regular HP bar above mob (only for mobs without boss HP bar)
            if (_mobHPBars.TryGetValue(poolId, out var hpBar))
            {
                // Update existing HP bar
                hpBar.LastHpPercent = mob.AI.HpPercent;
                hpBar.LastDamageTime = currentTime;
                hpBar.IsVisible = true;
                hpBar.Alpha = 1.0f;
                hpBar.MobHeight = mobHeight;
            }
            else if (_mobHPBars.Count < MAX_MOB_HP_BARS)
            {
                // Create new HP bar
                _mobHPBars[poolId] = new MobHPBarDisplay
                {
                    Mob = mob,
                    LastHpPercent = mob.AI.HpPercent,
                    LastDamageTime = currentTime,
                    IsVisible = true,
                    Alpha = 1.0f,
                    MobHeight = mobHeight
                };
            }
        }

        /// <summary>
        /// Add a boss HP bar display at top of screen.
        /// Only bosses with hpTagColor > 0 should call this method.
        /// </summary>
        private void AddBossHPBar(MobItem boss, int currentTime)
        {
            if (boss?.AI == null)
                return;

            // Check for hpTagColor - only show boss HP bar for mobs with this property
            var mobData = boss.MobInstance?.MobInfo?.MobData;
            if (mobData == null || mobData.HpTagColor <= 0)
                return;

            // Get boss name from mob instance
            string bossName = boss.MobInstance?.MobInfo?.Name ?? "Boss";

            // Get tag color from mob data hpTagColor property
            Color tagColor = GetHpTagColorValue(mobData.HpTagColor);

            var bossHPBar = new BossHPBarDisplay
            {
                Boss = boss,
                BossName = bossName,
                CurrentHp = boss.AI.CurrentHp,
                MaxHp = boss.AI.MaxHp,
                DisplayedHpPercent = boss.AI.HpPercent,
                TargetHpPercent = boss.AI.HpPercent,
                LastDamageTime = currentTime,
                TagColor = tagColor,
                IsVisible = true,
                Alpha = 1.0f
            };

            _bossHPBars.Add(bossHPBar);
        }

        /// <summary>
        /// Convert hpTagColor value to actual Color
        /// Based on MapleStory client color mappings
        /// </summary>
        private Color GetHpTagColorValue(short hpTagColor)
        {
            return hpTagColor switch
            {
                1 => new Color(255, 0, 0),       // Red
                2 => new Color(255, 128, 0),     // Orange
                3 => new Color(255, 255, 0),     // Yellow
                4 => new Color(0, 255, 0),       // Green
                5 => new Color(0, 255, 255),     // Cyan
                6 => new Color(0, 0, 255),       // Blue
                7 => new Color(128, 0, 255),     // Purple
                8 => new Color(255, 0, 255),     // Magenta
                _ => new Color(255, 100, 50)     // Default orange-red
            };
        }

        /// <summary>
        /// Remove HP bar for a dead or despawned mob
        /// </summary>
        public void RemoveMobHPBar(int poolId)
        {
            _mobHPBars.Remove(poolId);
            _bossHPBars.RemoveAll(b => b.Boss?.PoolId == poolId);
        }

        /// <summary>
        /// Clear all HP bars
        /// </summary>
        public void ClearHPBars()
        {
            _mobHPBars.Clear();
            _bossHPBars.Clear();
        }

        /// <summary>
        /// Get tag color based on boss level
        /// </summary>
        private Color GetBossTagColor(int level)
        {
            if (level >= 200) return new Color(255, 50, 50);      // Red - high level boss
            if (level >= 150) return new Color(255, 100, 50);     // Orange
            if (level >= 100) return new Color(255, 180, 50);     // Yellow-orange
            if (level >= 50)  return new Color(255, 220, 100);    // Gold
            return new Color(200, 200, 200);                       // Gray - low level
        }

        /// <summary>
        /// Sync HP bars with mob pool state
        /// </summary>
        public void SyncHPBarsFromMobPool(MobPool mobPool, int currentTime)
        {
            if (mobPool == null)
                return;

            // Remove HP bars for mobs that no longer exist
            var mobIdsToRemove = new List<int>();
            foreach (var kvp in _mobHPBars)
            {
                var mob = kvp.Value.Mob;
                if (mob == null || mob.AI == null || mob.AI.IsDead)
                {
                    mobIdsToRemove.Add(kvp.Key);
                }
            }
            foreach (var id in mobIdsToRemove)
            {
                _mobHPBars.Remove(id);
            }

            // Remove boss HP bars for dead bosses
            _bossHPBars.RemoveAll(b => b.Boss == null || b.Boss.AI == null || b.Boss.AI.IsDead);

            // Update HP bars for damaged mobs
            foreach (var mob in mobPool.ActiveMobs)
            {
                if (mob?.AI == null)
                    continue;

                // Only show HP bar if mob has taken damage
                if (mob.AI.CurrentHp < mob.AI.MaxHp)
                {
                    if (!_mobHPBars.ContainsKey(mob.PoolId))
                    {
                        // Auto-create HP bar for damaged mobs
                        OnMobDamaged(mob, currentTime);
                    }
                    else
                    {
                        // Update HP percent
                        var hpBar = _mobHPBars[mob.PoolId];
                        hpBar.LastHpPercent = mob.AI.HpPercent;
                    }
                }

                // Update boss HP bars
                if (mob.AI.IsBoss)
                {
                    var bossBar = _bossHPBars.Find(b => b.Boss?.PoolId == mob.PoolId);
                    if (bossBar != null)
                    {
                        bossBar.CurrentHp = mob.AI.CurrentHp;
                        bossBar.TargetHpPercent = mob.AI.HpPercent;
                    }
                    else if (mob.AI.CurrentHp < mob.AI.MaxHp)
                    {
                        // Create boss HP bar if not exists and boss is damaged
                        AddBossHPBar(mob, currentTime);
                    }
                }
            }
        }
        #endregion

        #region Update
        public void Update(int currentTime, float deltaTime)
        {
            // Update damage numbers
            for (int i = _damageNumbers.Count - 1; i >= 0; i--)
            {
                var dmg = _damageNumbers[i];
                dmg.Update(currentTime);

                if (dmg.IsExpired(currentTime))
                {
                    _damageNumbers.RemoveAt(i);
                    _damagePool.Enqueue(dmg);
                }
            }

            // Update hit effects
            for (int i = _hitEffects.Count - 1; i >= 0; i--)
            {
                var effect = _hitEffects[i];
                effect.Update(currentTime);

                if (effect.IsComplete)
                {
                    _hitEffects.RemoveAt(i);
                }
            }

            // Update death effects with actual delta time for frame-rate independence
            for (int i = _deathEffects.Count - 1; i >= 0; i--)
            {
                var effect = _deathEffects[i];
                effect.Update(currentTime, deltaTime);

                if (effect.IsComplete)
                {
                    _deathEffects.RemoveAt(i);
                }
            }

            // Update mob skill hit effects (played on player when hit by mob skill)
            for (int i = _mobSkillHitEffects.Count - 1; i >= 0; i--)
            {
                var effect = _mobSkillHitEffects[i];
                effect.Update(currentTime);

                if (effect.IsComplete)
                {
                    _mobSkillHitEffects.RemoveAt(i);
                }
            }

            // Update mob HP bars (fade out expired ones)
            var expiredBars = new List<int>();
            foreach (var kvp in _mobHPBars)
            {
                var hpBar = kvp.Value;
                hpBar.UpdateAlpha(currentTime);

                if (hpBar.IsExpired(currentTime))
                {
                    expiredBars.Add(kvp.Key);
                }
            }
            foreach (var id in expiredBars)
            {
                _mobHPBars.Remove(id);
            }

            // Update boss HP bar animations with actual delta time
            foreach (var bossBar in _bossHPBars)
            {
                bossBar.UpdateAnimation(deltaTime);
            }

            // Update WZ-based boss HP bar UI if available
            if (_useWzBossHPBar && _bossHPBarUI != null)
            {
                _bossHPBarUI.Update(currentTime, deltaTime);
            }
        }

        /// <summary>
        /// Update mouse position for boss HP bar hover detection (icon tooltip)
        /// </summary>
        public void UpdateMousePosition(int x, int y)
        {
            if (_useWzBossHPBar && _bossHPBarUI != null)
            {
                _bossHPBarUI.UpdateMousePosition(x, y);
            }
        }

        /// <summary>
        /// Sync damage displays from all mobs in the pool
        /// </summary>
        public void SyncFromMobPool(MobPool mobPool, int currentTime)
        {
            if (mobPool == null)
                return;

            foreach (var mob in mobPool.ActiveMobs)
            {
                AddDamageFromMob(mob, currentTime);
            }

            foreach (var mob in mobPool.DyingMobs)
            {
                AddDamageFromMob(mob, currentTime);
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (!_initialized)
                return;

            // Draw death effects first (behind everything)
            DrawDeathEffects(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY);

            // Draw mob HP bars (behind damage numbers but above mobs)
            DrawMobHPBars(spriteBatch, mapShiftX, mapShiftY, centerX, centerY);

            // Draw hit effects (behind damage numbers)
            DrawHitEffects(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY);

            // Draw mob skill hit effects on player (between hit effects and damage numbers)
            DrawMobSkillHitEffects(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY);

            // Draw damage numbers on top
            DrawDamageNumbers(spriteBatch, mapShiftX, mapShiftY, centerX, centerY);
        }

        /// <summary>
        /// Draw boss HP bar at top of screen (call this separately after UI drawing)
        /// </summary>
        public void DrawBossHPBar(SpriteBatch spriteBatch)
        {
            if (!_initialized)
                return;

            // Use WZ-based boss HP bar UI if available
            if (_useWzBossHPBar && _bossHPBarUI != null && _bossHPBarUI.HasActiveBossBars)
            {
                _bossHPBarUI.Draw(spriteBatch);
                return;
            }

            // Fall back to basic rendering
            if (_bossHPBars.Count == 0)
                return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            int yOffset = BossHPBarDisplay.BAR_Y_OFFSET;

            foreach (var bossBar in _bossHPBars)
            {
                if (!bossBar.IsVisible)
                    continue;

                // Skip HP bars for dead/removed bosses
                if (bossBar.Boss?.AI != null && bossBar.Boss.AI.IsDead)
                    continue;

                DrawSingleBossHPBar(spriteBatch, bossBar, screenWidth, yOffset);
                yOffset += BossHPBarDisplay.BAR_HEIGHT + 30; // Stack multiple boss bars
            }
        }

        /// <summary>
        /// Draw a single boss HP bar
        /// </summary>
        private void DrawSingleBossHPBar(SpriteBatch spriteBatch, BossHPBarDisplay bossBar, int screenWidth, int yPos)
        {
            int barWidth = BossHPBarDisplay.BAR_WIDTH;
            int barHeight = BossHPBarDisplay.BAR_HEIGHT;
            int x = (screenWidth - barWidth) / 2;

            // Draw outer border (gold frame)
            int borderWidth = 3;
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x - borderWidth, yPos - borderWidth, barWidth + borderWidth * 2, barHeight + borderWidth * 2),
                COLOR_BOSS_BORDER * bossBar.Alpha);

            // Draw background
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x, yPos, barWidth, barHeight),
                COLOR_BOSS_HP_BG * bossBar.Alpha);

            // Draw HP bar (animated smoothly)
            int hpWidth = (int)(barWidth * bossBar.DisplayedHpPercent);
            if (hpWidth > 0)
            {
                // Draw damage trail (orange bar showing recent damage)
                if (bossBar.DisplayedHpPercent > bossBar.TargetHpPercent)
                {
                    int trailWidth = (int)(barWidth * bossBar.DisplayedHpPercent);
                    spriteBatch.Draw(_pixelTexture,
                        new Rectangle(x, yPos, trailWidth, barHeight),
                        COLOR_HPBAR_DAMAGE * bossBar.Alpha);
                }

                // Draw current HP
                int currentHpWidth = (int)(barWidth * bossBar.TargetHpPercent);
                if (currentHpWidth > 0)
                {
                    // Gradient effect (lighter at top)
                    Color hpColor = COLOR_BOSS_HP * bossBar.Alpha;
                    spriteBatch.Draw(_pixelTexture,
                        new Rectangle(x, yPos, currentHpWidth, barHeight),
                        hpColor);

                    // Highlight at top
                    Color highlightColor = Color.Lerp(COLOR_BOSS_HP, Color.White, 0.3f) * bossBar.Alpha * 0.5f;
                    spriteBatch.Draw(_pixelTexture,
                        new Rectangle(x, yPos, currentHpWidth, barHeight / 3),
                        highlightColor);
                }
            }

            // Draw inner border (black line)
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x, yPos, barWidth, 1),
                Color.Black * bossBar.Alpha * 0.5f);
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x, yPos + barHeight - 1, barWidth, 1),
                Color.Black * bossBar.Alpha * 0.5f);

            // Draw boss name above bar
            if (_damageFont != null && !string.IsNullOrEmpty(bossBar.BossName))
            {
                string nameText = bossBar.BossName;
                Vector2 nameSize = _damageFont.MeasureString(nameText);
                Vector2 namePos = new Vector2((screenWidth - nameSize.X) / 2, yPos - nameSize.Y - 5);

                // Name shadow
                spriteBatch.DrawString(_damageFont, nameText, namePos + Vector2.One,
                    Color.Black * bossBar.Alpha);
                // Name text
                spriteBatch.DrawString(_damageFont, nameText, namePos,
                    COLOR_BOSS_NAME * bossBar.Alpha);
            }

            // Draw HP text (current / max or percentage)
            if (_damageFont != null)
            {
                string hpText;
                if (bossBar.MaxHp > 1000000)
                {
                    // Show percentage for high HP bosses
                    hpText = $"{(int)(bossBar.TargetHpPercent * 100)}%";
                }
                else
                {
                    hpText = $"{bossBar.CurrentHp:N0} / {bossBar.MaxHp:N0}";
                }

                Vector2 hpTextSize = _damageFont.MeasureString(hpText);
                Vector2 hpTextPos = new Vector2((screenWidth - hpTextSize.X) / 2, yPos + barHeight + 3);

                // HP text shadow
                spriteBatch.DrawString(_damageFont, hpText, hpTextPos + Vector2.One,
                    Color.Black * bossBar.Alpha * 0.8f);
                // HP text
                spriteBatch.DrawString(_damageFont, hpText, hpTextPos,
                    Color.White * bossBar.Alpha);
            }
        }

        /// <summary>
        /// Draw regular mob HP bars above damaged mobs
        /// </summary>
        private void DrawMobHPBars(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            foreach (var kvp in _mobHPBars)
            {
                var hpBar = kvp.Value;
                var mob = hpBar.Mob;

                if (!hpBar.IsVisible || mob == null || mob.MovementInfo == null)
                    continue;

                // Skip HP bars for dead/removed mobs
                if (mob.AI != null && mob.AI.IsDead)
                    continue;

                // Get mob position
                float mobX = mob.MovementInfo.X;
                float mobY = mob.MovementInfo.Y;

                // Convert to screen coordinates
                int screenX = (int)mobX - mapShiftX + centerX;
                int screenY = (int)mobY - mapShiftY + centerY;

                // Draw HP bar above mob
                DrawSingleMobHPBar(spriteBatch, hpBar, screenX, screenY);
            }
        }

        /// <summary>
        /// Draw a single mob HP bar
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DrawSingleMobHPBar(SpriteBatch spriteBatch, MobHPBarDisplay hpBar, int screenX, int screenY)
        {
            int barWidth = MobHPBarDisplay.BAR_WIDTH;
            int barHeight = MobHPBarDisplay.BAR_HEIGHT;

            // Position bar at mob's top (screenY is feet position, subtract height to get top)
            int mobHeight = hpBar.MobHeight;
            int x = screenX - barWidth / 2;
            int y = screenY - mobHeight - barHeight;

            float alpha = hpBar.Alpha;
            float hpPercent = hpBar.LastHpPercent;

            // Draw background (dark)
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x - 1, y - 1, barWidth + 2, barHeight + 2),
                COLOR_HPBAR_BORDER * alpha);

            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x, y, barWidth, barHeight),
                COLOR_HPBAR_BG * alpha);

            // Draw HP bar
            int hpWidth = (int)(barWidth * hpPercent);
            if (hpWidth > 0)
            {
                // Use different color when HP is low
                Color hpColor = hpPercent > 0.3f ? COLOR_HPBAR_HP : COLOR_HPBAR_HP_LOW;
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(x, y, hpWidth, barHeight),
                    hpColor * alpha);

                // Draw highlight at top of HP bar
                if (barHeight > 2)
                {
                    Color highlightColor = Color.Lerp(hpColor, Color.White, 0.4f) * alpha * 0.6f;
                    spriteBatch.Draw(_pixelTexture,
                        new Rectangle(x, y, hpWidth, 1),
                        highlightColor);
                }
            }
        }

        private void DrawDamageNumbers(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (_damageFont == null)
                return;

            foreach (var dmg in _damageNumbers)
            {
                // Convert map coords to screen coords
                int screenX = (int)dmg.X - mapShiftX + centerX;
                int screenY = (int)dmg.Y - mapShiftY + centerY;

                string text = dmg.IsMiss ? "MISS" : dmg.Damage.ToString();
                Color color = dmg.IsMiss ? COLOR_MISS : (dmg.IsCritical ? COLOR_CRITICAL : COLOR_NORMAL);
                color *= dmg.Alpha;

                SpriteFont font = dmg.IsCritical ? _criticalFont : _damageFont;
                Vector2 textSize = font.MeasureString(text);
                Vector2 position = new Vector2(screenX - textSize.X / 2, screenY - textSize.Y / 2);

                // Draw outline (4 directions)
                Color outlineColor = COLOR_OUTLINE * dmg.Alpha;
                spriteBatch.DrawString(font, text, position + new Vector2(-1, 0), outlineColor, 0, Vector2.Zero, dmg.Scale, SpriteEffects.None, 0);
                spriteBatch.DrawString(font, text, position + new Vector2(1, 0), outlineColor, 0, Vector2.Zero, dmg.Scale, SpriteEffects.None, 0);
                spriteBatch.DrawString(font, text, position + new Vector2(0, -1), outlineColor, 0, Vector2.Zero, dmg.Scale, SpriteEffects.None, 0);
                spriteBatch.DrawString(font, text, position + new Vector2(0, 1), outlineColor, 0, Vector2.Zero, dmg.Scale, SpriteEffects.None, 0);

                // Draw main text
                spriteBatch.DrawString(font, text, position, color, 0, Vector2.Zero, dmg.Scale, SpriteEffects.None, 0);
            }
        }

        private void DrawHitEffects(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            foreach (var effect in _hitEffects)
            {
                if (effect.Frames == null || effect.CurrentFrame >= effect.Frames.Count)
                    continue;

                var frame = effect.Frames[effect.CurrentFrame];
                if (frame == null)
                    continue;

                // Add frame.X and frame.Y which contain the negative origin offset for proper alignment
                int screenX = (int)effect.X - mapShiftX + centerX + frame.X;
                int screenY = (int)effect.Y - mapShiftY + centerY + frame.Y;

                // Use DrawBackground to support color tinting
                frame.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, effect.Tint, effect.Flip, null);
            }
        }

        private void DrawDeathEffects(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (_pixelTexture == null)
                return;

            foreach (var effect in _deathEffects)
            {
                int screenX = (int)effect.X - mapShiftX + centerX;
                int screenY = (int)effect.Y - mapShiftY + centerY;

                // Draw frame animation if available
                if (effect.Frames != null && effect.Frames.Count > 0 && effect.CurrentFrame < effect.Frames.Count)
                {
                    var frame = effect.Frames[effect.CurrentFrame];
                    if (frame != null)
                    {
                        // Add frame.X and frame.Y which contain the negative origin offset for proper alignment
                        int frameScreenX = screenX + frame.X;
                        int frameScreenY = screenY + frame.Y;
                        Color frameColor = Color.White * effect.Alpha;
                        frame.DrawBackground(spriteBatch, skeletonRenderer, null, frameScreenX, frameScreenY, frameColor, false, null);
                    }
                }

                // Draw particles
                foreach (var particle in effect.Particles)
                {
                    int particleX = (int)particle.X - mapShiftX + centerX;
                    int particleY = (int)particle.Y - mapShiftY + centerY;
                    int size = (int)particle.Size;

                    Color particleColor = particle.Color * particle.Alpha * effect.Alpha;
                    spriteBatch.Draw(_pixelTexture,
                        new Rectangle(particleX - size / 2, particleY - size / 2, size, size),
                        particleColor);
                }
            }
        }

        /// <summary>
        /// Draw mob skill hit effects on the player.
        /// These are the "affected" animations from MobSkill.img that play on the player when hit by mob skills.
        /// Also used for regular attack hit effects from mob's attack/info/hit.
        /// </summary>
        private void DrawMobSkillHitEffects(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            foreach (var effect in _mobSkillHitEffects)
            {
                if (effect.Frames == null || effect.CurrentFrame >= effect.Frames.Count)
                    continue;

                var frame = effect.Frames[effect.CurrentFrame];
                if (frame == null)
                    continue;

                // Convert map coordinates to screen coordinates
                // Add frame.X and frame.Y which contain the negative origin offset for proper alignment
                int screenX = (int)effect.X - mapShiftX + centerX + frame.X;
                int screenY = (int)effect.Y - mapShiftY + centerY + frame.Y;

                // Draw the effect frame at the player's position (with origin offset applied)
                frame.DrawBackground(spriteBatch, skeletonRenderer, null, screenX, screenY, effect.Tint, effect.Flip, null);
            }
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Full clear - clears all effects and HP bars.
        /// Use when completely disposing the combat effects system.
        /// </summary>
        public void Clear()
        {
            foreach (var dmg in _damageNumbers)
            {
                _damagePool.Enqueue(dmg);
            }
            _damageNumbers.Clear();
            _hitEffects.Clear();
            _deathEffects.Clear();
            _mobSkillHitEffects.Clear();
            _mobHPBars.Clear();
            _bossHPBars.Clear();

            // Clear WZ-based boss HP bar UI
            _bossHPBarUI?.Clear();
        }

        /// <summary>
        /// Clear map-specific state but preserve initialized resources.
        /// Preserves: textures, fonts, hit effect frames, death effect frames.
        /// Clears: active damage numbers, hit effects, death effects, mob/boss HP bars.
        /// </summary>
        public void ClearMapState()
        {
            // Pool active damage numbers for reuse
            foreach (var dmg in _damageNumbers)
            {
                _damagePool.Enqueue(dmg);
            }
            _damageNumbers.Clear();

            // Clear active effects
            _hitEffects.Clear();
            _deathEffects.Clear();
            _mobSkillHitEffects.Clear();

            // Clear HP bars (mob-specific)
            _mobHPBars.Clear();
            _bossHPBars.Clear();

            // Clear WZ-based boss HP bar UI state
            _bossHPBarUI?.Clear();

            // Note: We intentionally do NOT clear:
            // - _pixelTexture (reusable)
            // - _damageFont / _criticalFont (reusable)
            // - _hitEffectFrames (could be reloaded, but usually same across maps)
            // - _deathEffectFrames (reusable)
            // - _damagePool (object pool for reuse)
            // - _initialized flag
        }

        public void Dispose()
        {
            _pixelTexture?.Dispose();
            _bossHPBarUI?.Dispose();
            Clear();
        }
        #endregion

        #region Stats
        public int ActiveDamageNumbers => _damageNumbers.Count;
        public int ActiveHitEffects => _hitEffects.Count;
        public int ActiveMobHPBars => _mobHPBars.Count;
        public int ActiveBossHPBars => (_useWzBossHPBar && _bossHPBarUI != null)
            ? _bossHPBarUI.ActiveBossCount
            : _bossHPBars.Count;
        public bool HasActiveBossBar => (_useWzBossHPBar && _bossHPBarUI != null && _bossHPBarUI.HasActiveBossBars)
            || _bossHPBars.Count > 0;
        #endregion
    }
}
