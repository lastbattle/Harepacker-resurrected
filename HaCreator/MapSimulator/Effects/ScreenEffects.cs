using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Screen effects system based on MapleStory's CAnimationDisplayer.
    /// Provides screen shake, fade, and other visual effects.
    ///
    /// - Effect_Tremble: Screen shake with force and reduction
    /// - Duration: 1500ms (heavy) or 2000ms (normal)
    /// - Reduction: 0.85 (heavy) or 0.92 (normal)
    /// </summary>
    public class ScreenEffects
    {
        #region Screen Tremble (Effect_Tremble from CAnimationDisplayer)

        // Tremble state
        private bool _trembleActive = false;
        private double _trembleForce = 0;
        private double _trembleReduction = 0.92;
        private int _trembleStartTime = 0;
        private int _trembleEndTime = 0;
        private Random _random = new Random();

        // Current tremble offset applied to rendering
        private float _trembleOffsetX = 0;
        private float _trembleOffsetY = 0;

        /// <summary>
        /// Whether screen tremble is currently active
        /// </summary>
        public bool IsTrembleActive => _trembleActive;

        /// <summary>
        /// Current X offset from tremble effect
        /// </summary>
        public float TrembleOffsetX => _trembleOffsetX;

        /// <summary>
        /// Current Y offset from tremble effect
        /// </summary>
        public float TrembleOffsetY => _trembleOffsetY;

        /// <summary>
        /// Trigger a screen tremble effect.
        /// Based on CAnimationDisplayer::Effect_Tremble from the client.
        /// </summary>
        /// <param name="trembleForce">Intensity of the shake (typical range: 5-30)</param>
        /// <param name="heavyAndShort">If true, uses heavy/short parameters (1500ms, 0.85 reduction).
        /// If false, uses normal parameters (2000ms, 0.92 reduction).</param>
        /// <param name="delayMs">Delay in ms before tremble starts</param>
        /// <param name="additionalTimeMs">Extra duration to add to the effect</param>
        /// <param name="enforce">If true, overrides the "screen shake enabled" config check</param>
        /// <param name="currentTimeMs">Current game time in milliseconds</param>
        public void TriggerTremble(double trembleForce, bool heavyAndShort, int delayMs,
            int additionalTimeMs, bool enforce, int currentTimeMs)
        {
            // In the real client, there's a config check: TSingleton<CConfig>::ms_pInstance + 36
            // We'll always allow it for the simulator, or you can add a config option

            _trembleForce = trembleForce;
            _trembleStartTime = currentTimeMs + delayMs;

            // Duration based on type
            int baseDuration = heavyAndShort ? 1500 : 2000;
            _trembleEndTime = _trembleStartTime + additionalTimeMs + baseDuration;

            // Reduction factor based on type
            if (additionalTimeMs > 0)
            {
                _trembleReduction = 1.0; // No reduction during additional time
            }
            else if (heavyAndShort)
            {
                _trembleReduction = 0.85;
            }
            else
            {
                _trembleReduction = 0.92;
            }

            _trembleActive = true;
        }

        /// <summary>
        /// Quick tremble for common effects (portal, attack, etc.)
        /// </summary>
        /// <param name="intensity">Shake intensity (1-10 typical)</param>
        /// <param name="currentTimeMs">Current game time</param>
        public void QuickTremble(double intensity, int currentTimeMs)
        {
            TriggerTremble(intensity, true, 0, 0, true, currentTimeMs);
        }

        /// <summary>
        /// Update the tremble effect. Call this every frame.
        /// </summary>
        /// <param name="currentTimeMs">Current game time in milliseconds</param>
        public void UpdateTremble(int currentTimeMs)
        {
            if (!_trembleActive)
            {
                _trembleOffsetX = 0;
                _trembleOffsetY = 0;
                return;
            }

            // Check if effect has ended
            if (currentTimeMs >= _trembleEndTime)
            {
                _trembleActive = false;
                _trembleOffsetX = 0;
                _trembleOffsetY = 0;
                _trembleForce = 0;
                return;
            }

            // Check if effect hasn't started yet (delay period)
            if (currentTimeMs < _trembleStartTime)
            {
                _trembleOffsetX = 0;
                _trembleOffsetY = 0;
                return;
            }

            // Calculate elapsed time since tremble started
            int elapsedMs = currentTimeMs - _trembleStartTime;

            // Apply reduction over time
            // Each frame reduces the force multiplicatively
            double currentForce = _trembleForce * Math.Pow(_trembleReduction, elapsedMs / 16.67);

            // Minimum threshold
            if (currentForce < 0.5)
            {
                _trembleActive = false;
                _trembleOffsetX = 0;
                _trembleOffsetY = 0;
                return;
            }

            // Generate random offset within force range
            _trembleOffsetX = (float)((_random.NextDouble() * 2 - 1) * currentForce);
            _trembleOffsetY = (float)((_random.NextDouble() * 2 - 1) * currentForce);
        }

        /// <summary>
        /// Stop the current tremble effect immediately
        /// </summary>
        public void StopTremble()
        {
            _trembleActive = false;
            _trembleOffsetX = 0;
            _trembleOffsetY = 0;
            _trembleForce = 0;
        }

        #endregion

        #region Screen Fade (FADEINFO from CAnimationDisplayer)

        private bool _fadeActive = false;
        private float _fadeAlpha = 0.0f;         // Current alpha (0 = transparent/no overlay, 1 = opaque/full overlay)
        private float _fadeStartAlpha = 0.0f;    // Starting alpha for interpolation
        private float _fadeTargetAlpha = 0.0f;   // Target alpha
        private int _fadeStartTime = 0;
        private int _fadeDuration = 0;
        private Color _fadeColor = Color.Black;
        private Action _fadeCompleteCallback = null;  // Optional callback when fade completes

        /// <summary>
        /// Whether a fade effect is currently active
        /// </summary>
        public bool IsFadeActive => _fadeActive;

        /// <summary>
        /// Current fade alpha value (0 = transparent, 1 = opaque)
        /// </summary>
        public float FadeAlpha => _fadeAlpha;

        /// <summary>
        /// Current fade color
        /// </summary>
        public Color FadeColor => _fadeColor;

        /// <summary>
        /// Start a fade effect
        /// </summary>
        /// <param name="startAlpha">Starting alpha (0-1)</param>
        /// <param name="endAlpha">Ending alpha (0-1)</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="color">Fade overlay color</param>
        /// <param name="currentTimeMs">Current game time</param>
        /// <param name="onComplete">Optional callback when fade completes</param>
        public void StartFade(float startAlpha, float endAlpha, int durationMs, Color color, int currentTimeMs, Action onComplete = null)
        {
            _fadeActive = true;
            _fadeStartAlpha = startAlpha;
            _fadeAlpha = startAlpha;
            _fadeTargetAlpha = endAlpha;
            _fadeDuration = durationMs;
            _fadeStartTime = currentTimeMs;
            _fadeColor = color;
            _fadeCompleteCallback = onComplete;
        }

        /// <summary>
        /// Fade in from black (screen goes from black overlay to clear).
        /// Based on CStage::FadeIn - 600ms normal, 300ms for fast travel.
        /// </summary>
        /// <param name="durationMs">Duration in milliseconds (default 600ms matching official client)</param>
        /// <param name="currentTimeMs">Current game time</param>
        /// <param name="onComplete">Optional callback when fade completes</param>
        public void FadeIn(int durationMs, int currentTimeMs, Action onComplete = null)
        {
            StartFade(1.0f, 0.0f, durationMs, Color.Black, currentTimeMs, onComplete);
        }

        /// <summary>
        /// Fade out to black (screen goes from clear to black overlay).
        /// Based on CMapLoadable::Close / CStage::FadeOut.
        /// </summary>
        /// <param name="durationMs">Duration in milliseconds (default 600ms matching official client)</param>
        /// <param name="currentTimeMs">Current game time</param>
        /// <param name="onComplete">Optional callback when fade completes</param>
        public void FadeOut(int durationMs, int currentTimeMs, Action onComplete = null)
        {
            StartFade(0.0f, 1.0f, durationMs, Color.Black, currentTimeMs, onComplete);
        }

        /// <summary>
        /// Force the fade to complete state immediately (skip animation)
        /// </summary>
        public void ForceFadeComplete()
        {
            if (_fadeActive)
            {
                _fadeAlpha = _fadeTargetAlpha;
                _fadeActive = false;
                var callback = _fadeCompleteCallback;
                _fadeCompleteCallback = null;
                callback?.Invoke();
            }
        }

        /// <summary>
        /// Check if fade out is complete (screen is fully black)
        /// </summary>
        public bool IsFadeOutComplete => !_fadeActive && _fadeAlpha >= 1.0f;

        /// <summary>
        /// Check if fade in is complete (screen is fully clear)
        /// </summary>
        public bool IsFadeInComplete => !_fadeActive && _fadeAlpha <= 0.0f;

        /// <summary>
        /// Update the fade effect
        /// </summary>
        /// <param name="currentTimeMs">Current game time</param>
        public void UpdateFade(int currentTimeMs)
        {
            if (!_fadeActive)
                return;

            int elapsed = currentTimeMs - _fadeStartTime;

            if (elapsed >= _fadeDuration)
            {
                _fadeAlpha = _fadeTargetAlpha;
                _fadeActive = false;

                // Invoke callback if set
                var callback = _fadeCompleteCallback;
                _fadeCompleteCallback = null;
                callback?.Invoke();
                return;
            }

            // Linear interpolation from start to target
            float t = (float)elapsed / _fadeDuration;
            _fadeAlpha = MathHelper.Lerp(_fadeStartAlpha, _fadeTargetAlpha, t);
        }

        #endregion

        #region Flash Effect

        private bool _flashActive = false;
        private float _flashAlpha = 0;
        private int _flashStartTime = 0;
        private int _flashDuration = 0;
        private Color _flashColor = Color.White;

        /// <summary>
        /// Whether a flash effect is active
        /// </summary>
        public bool IsFlashActive => _flashActive;

        /// <summary>
        /// Current flash alpha
        /// </summary>
        public float FlashAlpha => _flashAlpha;

        /// <summary>
        /// Flash color
        /// </summary>
        public Color FlashColor => _flashColor;

        /// <summary>
        /// Trigger a screen flash effect
        /// </summary>
        /// <param name="color">Flash color</param>
        /// <param name="durationMs">Duration in ms</param>
        /// <param name="currentTimeMs">Current game time</param>
        public void Flash(Color color, int durationMs, int currentTimeMs)
        {
            _flashActive = true;
            _flashColor = color;
            _flashDuration = durationMs;
            _flashStartTime = currentTimeMs;
            _flashAlpha = 1.0f;
        }

        /// <summary>
        /// White flash (common for level up, skill effects)
        /// </summary>
        public void WhiteFlash(int durationMs, int currentTimeMs)
        {
            Flash(Color.White, durationMs, currentTimeMs);
        }

        /// <summary>
        /// Update flash effect
        /// </summary>
        public void UpdateFlash(int currentTimeMs)
        {
            if (!_flashActive)
                return;

            int elapsed = currentTimeMs - _flashStartTime;

            if (elapsed >= _flashDuration)
            {
                _flashActive = false;
                _flashAlpha = 0;
                return;
            }

            // Quick fade out
            float t = (float)elapsed / _flashDuration;
            _flashAlpha = 1.0f - t;
        }

        #endregion

        #region Motion Blur Effect (MOTIONBLURINFO from CAnimationDisplayer)

        private bool _motionBlurActive = false;
        private int _motionBlurStartTime = 0;
        private int _motionBlurDuration = 0;
        private float _motionBlurStrength = 0;
        private float _motionBlurAngle = 0; // Direction in radians
        private int _motionBlurSampleCount = 8;

        /// <summary>
        /// Whether motion blur is currently active
        /// </summary>
        public bool IsMotionBlurActive => _motionBlurActive;

        /// <summary>
        /// Current motion blur strength (0-1)
        /// </summary>
        public float MotionBlurStrength => _motionBlurStrength;

        /// <summary>
        /// Motion blur direction angle in radians
        /// </summary>
        public float MotionBlurAngle => _motionBlurAngle;

        /// <summary>
        /// Number of samples for blur effect (higher = smoother but more expensive)
        /// </summary>
        public int MotionBlurSampleCount => _motionBlurSampleCount;

        /// <summary>
        /// Get sample offsets for motion blur rendering.
        /// Use these offsets to render multiple passes and blend them together.
        /// </summary>
        /// <returns>Array of (x, y) offsets for each sample</returns>
        public Vector2[] GetMotionBlurOffsets()
        {
            if (!_motionBlurActive || _motionBlurStrength <= 0)
                return Array.Empty<Vector2>();

            Vector2[] offsets = new Vector2[_motionBlurSampleCount];
            float dirX = (float)Math.Cos(_motionBlurAngle);
            float dirY = (float)Math.Sin(_motionBlurAngle);
            float maxOffset = _motionBlurStrength * 20f; // Max pixel offset

            for (int i = 0; i < _motionBlurSampleCount; i++)
            {
                float t = (float)i / (_motionBlurSampleCount - 1) - 0.5f; // -0.5 to 0.5
                offsets[i] = new Vector2(dirX * t * maxOffset, dirY * t * maxOffset);
            }

            return offsets;
        }

        /// <summary>
        /// Start a motion blur effect
        /// </summary>
        /// <param name="strength">Blur strength (0-1, typical: 0.3-0.8)</param>
        /// <param name="angleRadians">Direction of blur in radians (0 = right, PI/2 = down)</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="currentTimeMs">Current game time</param>
        public void StartMotionBlur(float strength, float angleRadians, int durationMs, int currentTimeMs)
        {
            _motionBlurActive = true;
            _motionBlurStrength = MathHelper.Clamp(strength, 0, 1);
            _motionBlurAngle = angleRadians;
            _motionBlurDuration = durationMs;
            _motionBlurStartTime = currentTimeMs;
        }

        /// <summary>
        /// Horizontal motion blur (common for dashing/rushing skills)
        /// </summary>
        /// <param name="strength">Blur strength</param>
        /// <param name="facingRight">Direction of movement</param>
        /// <param name="durationMs">Duration</param>
        /// <param name="currentTimeMs">Current time</param>
        public void HorizontalBlur(float strength, bool facingRight, int durationMs, int currentTimeMs)
        {
            float angle = facingRight ? 0 : MathHelper.Pi;
            StartMotionBlur(strength, angle, durationMs, currentTimeMs);
        }

        /// <summary>
        /// Vertical motion blur (common for falling/jumping effects)
        /// </summary>
        /// <param name="strength">Blur strength</param>
        /// <param name="goingDown">Direction of movement</param>
        /// <param name="durationMs">Duration</param>
        /// <param name="currentTimeMs">Current time</param>
        public void VerticalBlur(float strength, bool goingDown, int durationMs, int currentTimeMs)
        {
            float angle = goingDown ? MathHelper.PiOver2 : -MathHelper.PiOver2;
            StartMotionBlur(strength, angle, durationMs, currentTimeMs);
        }

        /// <summary>
        /// Update motion blur effect
        /// </summary>
        public void UpdateMotionBlur(int currentTimeMs)
        {
            if (!_motionBlurActive)
                return;

            int elapsed = currentTimeMs - _motionBlurStartTime;

            if (elapsed >= _motionBlurDuration)
            {
                _motionBlurActive = false;
                _motionBlurStrength = 0;
                return;
            }

            // Fade out the blur strength over time
            float t = (float)elapsed / _motionBlurDuration;
            // Use ease-out curve for natural fade
            _motionBlurStrength *= (1.0f - t * t);
        }

        /// <summary>
        /// Stop motion blur immediately
        /// </summary>
        public void StopMotionBlur()
        {
            _motionBlurActive = false;
            _motionBlurStrength = 0;
        }

        #endregion

        #region Explosion Effect (EXPLOSIONINFO from CAnimationDisplayer)

        private bool _explosionActive = false;
        private int _explosionStartTime = 0;
        private int _explosionDuration = 0;
        private Vector2 _explosionOrigin;
        private float _explosionRadius = 0;
        private float _explosionMaxRadius = 0;
        private float _explosionRingWidth = 20f;
        private Color _explosionColor = Color.White;
        private float _explosionAlpha = 1.0f;
        private bool _explosionWithTremble = true;

        /// <summary>
        /// Whether an explosion effect is active
        /// </summary>
        public bool IsExplosionActive => _explosionActive;

        /// <summary>
        /// Current explosion ring radius
        /// </summary>
        public float ExplosionRadius => _explosionRadius;

        /// <summary>
        /// Explosion ring width (thickness)
        /// </summary>
        public float ExplosionRingWidth => _explosionRingWidth;

        /// <summary>
        /// Explosion center position
        /// </summary>
        public Vector2 ExplosionOrigin => _explosionOrigin;

        /// <summary>
        /// Explosion color
        /// </summary>
        public Color ExplosionColor => _explosionColor;

        /// <summary>
        /// Current explosion alpha (fades over time)
        /// </summary>
        public float ExplosionAlpha => _explosionAlpha;

        /// <summary>
        /// Start an explosion effect (expanding ring with optional screen shake)
        /// </summary>
        /// <param name="originX">Center X position</param>
        /// <param name="originY">Center Y position</param>
        /// <param name="maxRadius">Maximum expansion radius</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="color">Explosion ring color</param>
        /// <param name="withTremble">Whether to also trigger screen shake</param>
        /// <param name="currentTimeMs">Current game time</param>
        public void StartExplosion(float originX, float originY, float maxRadius, int durationMs,
            Color color, bool withTremble, int currentTimeMs)
        {
            _explosionActive = true;
            _explosionOrigin = new Vector2(originX, originY);
            _explosionMaxRadius = maxRadius;
            _explosionRadius = 0;
            _explosionDuration = durationMs;
            _explosionStartTime = currentTimeMs;
            _explosionColor = color;
            _explosionAlpha = 1.0f;
            _explosionWithTremble = withTremble;
            _explosionRingWidth = maxRadius * 0.1f; // Ring width is 10% of max radius

            // Trigger accompanying screen shake
            if (withTremble)
            {
                float trembleForce = MathHelper.Clamp(maxRadius / 20f, 5, 30);
                TriggerTremble(trembleForce, true, 0, 0, true, currentTimeMs);
            }
        }

        /// <summary>
        /// Start a standard white explosion
        /// </summary>
        public void Explosion(float originX, float originY, float maxRadius, int durationMs, int currentTimeMs)
        {
            StartExplosion(originX, originY, maxRadius, durationMs, Color.White, true, currentTimeMs);
        }

        /// <summary>
        /// Start a fire explosion (orange/red)
        /// </summary>
        public void FireExplosion(float originX, float originY, float maxRadius, int durationMs, int currentTimeMs)
        {
            StartExplosion(originX, originY, maxRadius, durationMs, new Color(255, 150, 50), true, currentTimeMs);
        }

        /// <summary>
        /// Start an ice explosion (blue/white)
        /// </summary>
        public void IceExplosion(float originX, float originY, float maxRadius, int durationMs, int currentTimeMs)
        {
            StartExplosion(originX, originY, maxRadius, durationMs, new Color(150, 200, 255), true, currentTimeMs);
        }

        /// <summary>
        /// Start a dark explosion (purple/black)
        /// </summary>
        public void DarkExplosion(float originX, float originY, float maxRadius, int durationMs, int currentTimeMs)
        {
            StartExplosion(originX, originY, maxRadius, durationMs, new Color(150, 50, 200), true, currentTimeMs);
        }

        /// <summary>
        /// Update explosion effect
        /// </summary>
        public void UpdateExplosion(int currentTimeMs)
        {
            if (!_explosionActive)
                return;

            int elapsed = currentTimeMs - _explosionStartTime;

            if (elapsed >= _explosionDuration)
            {
                _explosionActive = false;
                _explosionRadius = 0;
                _explosionAlpha = 0;
                return;
            }

            float t = (float)elapsed / _explosionDuration;

            // Radius expands with ease-out curve (fast start, slow end)
            _explosionRadius = _explosionMaxRadius * (1.0f - (1.0f - t) * (1.0f - t));

            // Alpha fades out linearly in the second half
            if (t > 0.5f)
            {
                _explosionAlpha = 1.0f - ((t - 0.5f) * 2.0f);
            }
            else
            {
                _explosionAlpha = 1.0f;
            }
        }

        /// <summary>
        /// Stop explosion immediately
        /// </summary>
        public void StopExplosion()
        {
            _explosionActive = false;
            _explosionRadius = 0;
            _explosionAlpha = 0;
        }

        #endregion

        /// <summary>
        /// Update all screen effects
        /// </summary>
        /// <param name="currentTimeMs">Current game time in milliseconds</param>
        public void Update(int currentTimeMs)
        {
            UpdateTremble(currentTimeMs);
            UpdateFade(currentTimeMs);
            UpdateFlash(currentTimeMs);
            UpdateMotionBlur(currentTimeMs);
            UpdateExplosion(currentTimeMs);
        }

        /// <summary>
        /// Get the combined transformation matrix for screen effects
        /// </summary>
        /// <returns>Transformation matrix including tremble offset</returns>
        public Matrix GetTransformMatrix()
        {
            if (_trembleActive)
            {
                return Matrix.CreateTranslation(_trembleOffsetX, _trembleOffsetY, 0);
            }
            return Matrix.Identity;
        }

        /// <summary>
        /// Reset all effects
        /// </summary>
        public void Reset()
        {
            StopTremble();
            _fadeActive = false;
            _fadeAlpha = 0.0f;
            _fadeStartAlpha = 0.0f;
            _fadeTargetAlpha = 0.0f;
            _fadeCompleteCallback = null;
            _flashActive = false;
            _flashAlpha = 0;
            StopMotionBlur();
            StopExplosion();
        }
    }
}
