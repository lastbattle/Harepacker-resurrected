using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// Screen effects system based on MapleStory's CAnimationDisplayer.
    /// Provides screen shake, fade, and other visual effects.
    ///
    /// Based on decompiled client code:
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
        private float _fadeAlpha = 1.0f;
        private float _fadeTargetAlpha = 1.0f;
        private float _fadeSpeed = 0;
        private int _fadeStartTime = 0;
        private int _fadeDuration = 0;
        private Color _fadeColor = Color.Black;

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
        public void StartFade(float startAlpha, float endAlpha, int durationMs, Color color, int currentTimeMs)
        {
            _fadeActive = true;
            _fadeAlpha = startAlpha;
            _fadeTargetAlpha = endAlpha;
            _fadeDuration = durationMs;
            _fadeStartTime = currentTimeMs;
            _fadeColor = color;
            _fadeSpeed = (endAlpha - startAlpha) / durationMs;
        }

        /// <summary>
        /// Fade in from black
        /// </summary>
        public void FadeIn(int durationMs, int currentTimeMs)
        {
            StartFade(1.0f, 0.0f, durationMs, Color.Black, currentTimeMs);
        }

        /// <summary>
        /// Fade out to black
        /// </summary>
        public void FadeOut(int durationMs, int currentTimeMs)
        {
            StartFade(0.0f, 1.0f, durationMs, Color.Black, currentTimeMs);
        }

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
                return;
            }

            // Linear interpolation
            float t = (float)elapsed / _fadeDuration;
            _fadeAlpha = MathHelper.Lerp(_fadeAlpha, _fadeTargetAlpha, t);
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

        /// <summary>
        /// Update all screen effects
        /// </summary>
        /// <param name="currentTimeMs">Current game time in milliseconds</param>
        public void Update(int currentTimeMs)
        {
            UpdateTremble(currentTimeMs);
            UpdateFade(currentTimeMs);
            UpdateFlash(currentTimeMs);
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
            _fadeAlpha = 1.0f;
            _flashActive = false;
            _flashAlpha = 0;
        }
    }
}
