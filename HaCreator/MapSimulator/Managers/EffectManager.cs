using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Animation;
namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Manages all visual effects in the MapSimulator.
    /// Consolidates ScreenEffects, AnimationEffects, CombatEffects, ParticleSystem, and FieldEffects.
    /// </summary>
    public class EffectManager
    {
        // Individual effect systems
        private readonly ScreenEffects _screenEffects = new ScreenEffects();
        private readonly AnimationEffects _animationEffects = new AnimationEffects();
        private readonly CombatEffects _combatEffects = new CombatEffects();
        private readonly ParticleSystem _particleSystem = new ParticleSystem();
        private readonly FieldEffects _fieldEffects = new FieldEffects();

        #region Effect System Accessors

        /// <summary>
        /// Screen-level effects (tremble, fade, flash, blur, explosion)
        /// </summary>
        public ScreenEffects Screen => _screenEffects;

        /// <summary>
        /// Animation effects (chain lightning, trails)
        /// </summary>
        public AnimationEffects Animation => _animationEffects;

        /// <summary>
        /// Combat effects (damage numbers, HP bars, hit effects)
        /// </summary>
        public CombatEffects Combat => _combatEffects;

        /// <summary>
        /// Particle system (weather, sparkles)
        /// </summary>
        public ParticleSystem Particles => _particleSystem;

        /// <summary>
        /// Field-wide effects (weather messages, fear, field overlays)
        /// </summary>
        public FieldEffects Field => _fieldEffects;

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the combat effects system
        /// </summary>
        public void InitializeCombat(GraphicsDevice graphicsDevice, SpriteFont font)
        {
            _combatEffects.Initialize(graphicsDevice, font);
        }

        #endregion

        #region Update

        /// <summary>
        /// Update all effect systems
        /// </summary>
        /// <param name="tickCount">Current tick count</param>
        /// <param name="deltaSeconds">Delta time in seconds</param>
        /// <param name="screenWidth">Screen width for field effects</param>
        /// <param name="screenHeight">Screen height for field effects</param>
        /// <param name="mouseX">Mouse X for field effects</param>
        /// <param name="mouseY">Mouse Y for field effects</param>
        public void Update(int tickCount, float deltaSeconds, int screenWidth, int screenHeight, float mouseX, float mouseY)
        {
            _screenEffects.Update(tickCount);
            _animationEffects.Update(tickCount, deltaSeconds);
            _combatEffects.Update(tickCount, deltaSeconds);
            _particleSystem.Update(tickCount, deltaSeconds);
            _fieldEffects.Update(tickCount, screenWidth, screenHeight, mouseX, mouseY, deltaSeconds * 1000f);
        }

        #endregion

        #region Screen Effects Shortcuts

        /// <summary>
        /// Trigger screen tremble effect
        /// </summary>
        public void Tremble(int intensity, bool horizontal, int offsetX, int offsetY, bool randomize, int tickCount)
        {
            _screenEffects.TriggerTremble(intensity, horizontal, offsetX, offsetY, randomize, tickCount);
        }

        /// <summary>
        /// Whether screen tremble is active
        /// </summary>
        public bool IsTrembleActive => _screenEffects.IsTrembleActive;

        /// <summary>
        /// Current tremble X offset
        /// </summary>
        public float TrembleOffsetX => _screenEffects.TrembleOffsetX;

        /// <summary>
        /// Current tremble Y offset
        /// </summary>
        public float TrembleOffsetY => _screenEffects.TrembleOffsetY;

        /// <summary>
        /// Trigger horizontal blur effect
        /// </summary>
        public void HorizontalBlur(float intensity, bool fadeOut, int durationMs, int tickCount)
        {
            _screenEffects.HorizontalBlur(intensity, fadeOut, durationMs, tickCount);
        }

        /// <summary>
        /// Trigger fire explosion effect
        /// </summary>
        public void FireExplosion(float x, float y, float radius, int durationMs, int tickCount)
        {
            _screenEffects.FireExplosion(x, y, radius, durationMs, tickCount);
        }

        #endregion

        #region Animation Effects Shortcuts

        /// <summary>
        /// Add chain lightning effect
        /// </summary>
        public void AddChainLightning(List<Vector2> points, Color color, int durationMs, int tickCount, float thickness = 3f, int segments = 8)
        {
            _animationEffects.AddChainLightning(points, color, durationMs, tickCount, thickness, segments);
        }

        #endregion

        #region Weather Shortcuts

        /// <summary>
        /// Create rain weather emitter
        /// </summary>
        /// <returns>Emitter ID</returns>
        public int CreateRainEmitter(int width, int height, float intensity)
        {
            return _particleSystem.CreateRainEmitter(width, height, intensity);
        }

        /// <summary>
        /// Create snow weather emitter
        /// </summary>
        /// <returns>Emitter ID</returns>
        public int CreateSnowEmitter(int width, int height, float intensity)
        {
            return _particleSystem.CreateSnowEmitter(width, height, intensity);
        }

        /// <summary>
        /// Create falling leaves emitter
        /// </summary>
        /// <returns>Emitter ID</returns>
        public int CreateLeavesEmitter(int width, int height, float intensity)
        {
            return _particleSystem.CreateLeavesEmitter(width, height, intensity);
        }

        /// <summary>
        /// Remove a particle emitter
        /// </summary>
        public void RemoveEmitter(int emitterId)
        {
            _particleSystem.RemoveEmitter(emitterId);
        }

        #endregion

        #region Field Effects Shortcuts

        /// <summary>
        /// Whether fear effect is active
        /// </summary>
        public bool IsFearActive => _fieldEffects.IsFearActive;

        /// <summary>
        /// Initialize fear effect
        /// </summary>
        public void InitFearEffect(float intensity, int durationMs, int pulseCount, int tickCount)
        {
            _fieldEffects.InitFearEffect(intensity, durationMs, pulseCount, tickCount);
        }

        /// <summary>
        /// Stop fear effect
        /// </summary>
        public void StopFearEffect()
        {
            _fieldEffects.StopFearEffect();
        }

        /// <summary>
        /// Trigger weather message effect
        /// </summary>
        public void OnBlowWeather(WeatherEffectType type, string itemId, string message, float intensity, int durationMs, int tickCount)
        {
            _fieldEffects.OnBlowWeather(type, itemId, message, intensity, durationMs, tickCount);
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Clear all effects (for map transitions)
        /// </summary>
        public void Clear()
        {
            _combatEffects.Clear();
            // Note: Other systems may need Clear methods too
        }

        #endregion
    }
}
