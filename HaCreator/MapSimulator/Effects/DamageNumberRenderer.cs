using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Loaders;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Animation constants from MapleStory binary analysis.
    /// Based on CAnimationDisplayer::Effect_HP and Effect_BasicFloat.
    /// </summary>
    public static class DamageNumberConstants
    {
        /// <summary>Phase 1: Display duration (stationary, full alpha) in ms</summary>
        public const int DISPLAY_DURATION_MS = 400;

        /// <summary>Phase 2: Fade duration (fade + rise) in ms</summary>
        public const int FADE_DURATION_MS = 600;

        /// <summary>Total animation lifetime in ms</summary>
        public const int TOTAL_LIFETIME_MS = 1000;

        /// <summary>Rise distance during fade phase in pixels</summary>
        public const float RISE_DISTANCE_PX = 30f;

        /// <summary>Delay before critical effect appears in ms</summary>
        public const int CRITICAL_EFFECT_DELAY_MS = 250;

        /// <summary>Y offset for critical effect (above digits)</summary>
        public const int CRITICAL_EFFECT_OFFSET_Y = -30;

        /// <summary>Stacking offset for multi-hit damage numbers (should be >= digit height)</summary>
        public const int MULTI_HIT_STACK_OFFSET_Y = 20;

        /// <summary>Delay between multi-hit number spawns in ms</summary>
        public const int MULTI_HIT_DELAY_MS = 100;
    }

    /// <summary>
    /// Represents a single active damage number animation.
    /// Uses WZ sprite-based rendering with authentic MapleStory timing.
    /// </summary>
    public class WzDamageNumber
    {
        /// <summary>Damage value to display</summary>
        public int Damage { get; set; }

        /// <summary>Center X position in map coordinates</summary>
        public float X { get; set; }

        /// <summary>Center Y position in map coordinates (initial spawn position)</summary>
        public float BaseY { get; set; }

        /// <summary>Time when the number was spawned (game tick)</summary>
        public int SpawnTime { get; set; }

        /// <summary>Whether this is a critical hit</summary>
        public bool IsCritical { get; set; }

        /// <summary>Whether this is a miss</summary>
        public bool IsMiss { get; set; }

        /// <summary>Color type (Red for player damage, Blue for received, Violet for party)</summary>
        public DamageColorType ColorType { get; set; }

        /// <summary>Size variant (Small or Large)</summary>
        public DamageNumberSize Size { get; set; }

        /// <summary>Multi-hit combo index (for stacking)</summary>
        public int ComboIndex { get; set; }

        /// <summary>Elapsed time in ms</summary>
        public float ElapsedMs { get; set; }

        /// <summary>
        /// Current alpha value (0.0 to 1.0)
        /// </summary>
        public float Alpha
        {
            get
            {
                if (ElapsedMs < DamageNumberConstants.DISPLAY_DURATION_MS)
                    return 1.0f;

                float fadeProgress = (ElapsedMs - DamageNumberConstants.DISPLAY_DURATION_MS) / DamageNumberConstants.FADE_DURATION_MS;
                return 1.0f - Math.Clamp(fadeProgress, 0f, 1f);
            }
        }

        /// <summary>
        /// Current Y offset from base position (negative = upward)
        /// </summary>
        public float YOffset
        {
            get
            {
                if (ElapsedMs < DamageNumberConstants.DISPLAY_DURATION_MS)
                    return 0f;

                float riseProgress = (ElapsedMs - DamageNumberConstants.DISPLAY_DURATION_MS) / DamageNumberConstants.FADE_DURATION_MS;
                return -DamageNumberConstants.RISE_DISTANCE_PX * Math.Clamp(riseProgress, 0f, 1f);
            }
        }

        /// <summary>
        /// Current Y position (BaseY + YOffset)
        /// </summary>
        public float CurrentY => BaseY + YOffset;

        /// <summary>
        /// Whether the critical effect should be visible
        /// </summary>
        public bool ShouldShowCriticalEffect =>
            IsCritical && ElapsedMs >= DamageNumberConstants.CRITICAL_EFFECT_DELAY_MS;

        /// <summary>
        /// Whether the animation is complete and should be removed
        /// </summary>
        public bool IsComplete => ElapsedMs >= DamageNumberConstants.TOTAL_LIFETIME_MS;

        /// <summary>
        /// Update the animation state.
        /// </summary>
        /// <param name="deltaMs">Delta time in milliseconds</param>
        public void Update(float deltaMs)
        {
            ElapsedMs += deltaMs;
        }

        /// <summary>
        /// Get the damage string to render.
        /// </summary>
        public string GetDamageString()
        {
            if (IsMiss)
                return "Miss";
            return Damage.ToString();
        }
    }

    /// <summary>
    /// Renders damage numbers using authentic MapleStory WZ digit sprites.
    /// Based on CAnimationDisplayer binary analysis.
    /// </summary>
    public class DamageNumberRenderer : IDisposable
    {
        #region Constants
        private const int MAX_ACTIVE_NUMBERS = 100;
        #endregion

        #region State
        private readonly List<WzDamageNumber> _activeNumbers = new List<WzDamageNumber>();
        private readonly Queue<WzDamageNumber> _pool = new Queue<WzDamageNumber>();
        private GraphicsDevice _device;
        private bool _initialized = false;

        // Fallback font for when WZ sprites not loaded
        private SpriteFont _fallbackFont;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the damage number renderer.
        /// </summary>
        /// <param name="device">Graphics device</param>
        /// <param name="fallbackFont">Fallback font for when WZ sprites not available</param>
        public void Initialize(GraphicsDevice device, SpriteFont fallbackFont = null)
        {
            _device = device;
            _fallbackFont = fallbackFont;
            _initialized = true;
        }

        /// <summary>
        /// Whether the renderer is initialized and ready.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Whether WZ-based rendering is available.
        /// </summary>
        public bool HasWzSprites => DamageNumberLoader.IsInitialized;
        #endregion

        #region Spawn Methods
        /// <summary>
        /// Spawn a damage number at a position.
        /// </summary>
        /// <param name="damage">Damage value</param>
        /// <param name="x">Center X position (map coordinates)</param>
        /// <param name="y">Center Y position (map coordinates)</param>
        /// <param name="colorType">Damage color type</param>
        /// <param name="isCritical">Whether critical hit</param>
        /// <param name="isMiss">Whether miss</param>
        /// <param name="currentTime">Current game tick</param>
        /// <param name="comboIndex">Multi-hit combo index for stacking</param>
        public void SpawnDamageNumber(int damage, float x, float y, DamageColorType colorType,
            bool isCritical, bool isMiss, int currentTime, int comboIndex = 0)
        {
            // Limit active numbers
            if (_activeNumbers.Count >= MAX_ACTIVE_NUMBERS)
            {
                var oldest = _activeNumbers[0];
                _activeNumbers.RemoveAt(0);
                _pool.Enqueue(oldest);
            }

            // Get from pool or create new
            var dmgNumber = _pool.Count > 0 ? _pool.Dequeue() : new WzDamageNumber();

            // Determine size based on critical
            DamageNumberSize size = isCritical ? DamageNumberSize.Large : DamageNumberSize.Small;

            // Apply stacking offset for multi-hit
            float stackedY = y - (comboIndex * DamageNumberConstants.MULTI_HIT_STACK_OFFSET_Y);

            // Initialize
            dmgNumber.Damage = damage;
            dmgNumber.X = x;
            dmgNumber.BaseY = stackedY;
            dmgNumber.SpawnTime = currentTime;
            dmgNumber.IsCritical = isCritical;
            dmgNumber.IsMiss = isMiss;
            dmgNumber.ColorType = colorType;
            dmgNumber.Size = size;
            dmgNumber.ComboIndex = comboIndex;
            dmgNumber.ElapsedMs = 0f; // Reset for pooled objects

            _activeNumbers.Add(dmgNumber);
        }

        /// <summary>
        /// Spawn player damage to monster (Red).
        /// </summary>
        public void SpawnPlayerDamage(int damage, float x, float y, bool isCritical, int currentTime, int comboIndex = 0)
        {
            SpawnDamageNumber(damage, x, y, DamageColorType.Red, isCritical, false, currentTime, comboIndex);
        }

        /// <summary>
        /// Spawn damage received by player (Blue).
        /// </summary>
        public void SpawnReceivedDamage(int damage, float x, float y, bool isCritical, int currentTime)
        {
            SpawnDamageNumber(damage, x, y, DamageColorType.Blue, isCritical, false, currentTime, 0);
        }

        /// <summary>
        /// Spawn party/summon damage (Violet).
        /// </summary>
        public void SpawnPartyDamage(int damage, float x, float y, bool isCritical, int currentTime, int comboIndex = 0)
        {
            SpawnDamageNumber(damage, x, y, DamageColorType.Violet, isCritical, false, currentTime, comboIndex);
        }

        /// <summary>
        /// Spawn a miss indicator.
        /// </summary>
        public void SpawnMiss(float x, float y, DamageColorType colorType, int currentTime)
        {
            SpawnDamageNumber(0, x, y, colorType, false, true, currentTime, 0);
        }
        #endregion

        #region Update
        /// <summary>
        /// Update all active damage numbers.
        /// </summary>
        /// <param name="deltaMs">Delta time in milliseconds</param>
        public void Update(float deltaMs)
        {
            for (int i = _activeNumbers.Count - 1; i >= 0; i--)
            {
                var dmgNumber = _activeNumbers[i];
                dmgNumber.Update(deltaMs);

                if (dmgNumber.IsComplete)
                {
                    _activeNumbers.RemoveAt(i);
                    _pool.Enqueue(dmgNumber);
                }
            }
        }
        #endregion

        #region Draw
        /// <summary>
        /// Draw all active damage numbers.
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch for drawing</param>
        /// <param name="mapShiftX">Map shift X</param>
        /// <param name="mapShiftY">Map shift Y</param>
        /// <param name="centerX">Screen center X</param>
        /// <param name="centerY">Screen center Y</param>
        public void Draw(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (!_initialized)
                return;

            foreach (var dmgNumber in _activeNumbers)
            {
                // Convert map coords to screen coords
                int screenX = (int)dmgNumber.X - mapShiftX + centerX;
                int screenY = (int)dmgNumber.CurrentY - mapShiftY + centerY;

                if (HasWzSprites)
                {
                    DrawWzDamageNumber(spriteBatch, dmgNumber, screenX, screenY);
                }
                else if (_fallbackFont != null)
                {
                    DrawFallbackDamageNumber(spriteBatch, dmgNumber, screenX, screenY);
                }
            }
        }

        /// <summary>
        /// Draw a damage number using WZ sprites.
        /// Uses the authentic MapleStory spacing algorithm from CAnimationDisplayer::Effect_HP.
        ///
        /// Binary analysis (v115, address 0x444eb0) revealed the spacing formula:
        /// - For each digit: overlap = 3 * (origin.x - width) / 5
        /// - Since origin.x is typically less than width/2, this produces negative overlap
        /// - Negative overlap means digits are drawn CLOSER together (overlapping slightly)
        ///
        /// Example for NoRed0 digit "0": width=31, origin.x=15
        /// - overlap = 3 * (15 - 31) / 5 = 3 * (-16) / 5 = -9 pixels
        /// - Next digit shifts 9 pixels LEFT (closer)
        /// </summary>
        private void DrawWzDamageNumber(SpriteBatch spriteBatch, WzDamageNumber dmgNumber, int screenX, int screenY)
        {
            // Get the appropriate digit set
            DamageNumberDigitSet digitSet = DamageNumberLoader.GetDigitSet(
                dmgNumber.ColorType,
                dmgNumber.Size,
                dmgNumber.IsCritical);

            if (digitSet == null || !digitSet.IsLoaded)
            {
                // Fall back to non-critical if critical set not available
                if (dmgNumber.IsCritical)
                {
                    digitSet = DamageNumberLoader.GetDigitSet(dmgNumber.ColorType, dmgNumber.Size, false);
                }

                // Still null? Try any available set
                if (digitSet == null || !digitSet.IsLoaded)
                {
                    digitSet = DamageNumberLoader.GetDigitSetByName("NoRed0");
                }

                if (digitSet == null || !digitSet.IsLoaded)
                    return;
            }

            string damageString = dmgNumber.GetDamageString();
            float alpha = dmgNumber.Alpha;
            Color color = Color.White * alpha;

            // Handle miss text
            if (dmgNumber.IsMiss)
            {
                if (digitSet.SpecialTextures.TryGetValue("Miss", out var missTexture))
                {
                    Point origin = digitSet.SpecialOrigins.TryGetValue("Miss", out var o) ? o : Point.Zero;
                    int drawX = screenX - origin.X;
                    int drawY = screenY - origin.Y;
                    spriteBatch.Draw(missTexture, new Vector2(drawX, drawY), color);
                }
                return;
            }

            // Draw critical effect behind digits
            if (dmgNumber.ShouldShowCriticalEffect && digitSet.CriticalEffectTexture != null)
            {
                int effectX = screenX - digitSet.CriticalEffectOrigin.X;
                int effectY = screenY + DamageNumberConstants.CRITICAL_EFFECT_OFFSET_Y - digitSet.CriticalEffectOrigin.Y;
                spriteBatch.Draw(digitSet.CriticalEffectTexture, new Vector2(effectX, effectY), color);
            }

            // Pre-calculate digit X positions using MapleStory's spacing algorithm
            // Binary offset: 0x445176 - 0x44519a in CAnimationDisplayer::Effect_HP
            // The algorithm calculates overlap for each digit to make them closer together
            int[] digitXPositions = new int[damageString.Length];
            int digitCount = 0;
            int accumulatedX = 0;  // v15 in the binary
            int previousOverlap = 0;  // lY in the binary (overlap from previous digit)

            // First pass: calculate positions for each digit
            foreach (char c in damageString)
            {
                if (!char.IsDigit(c))
                    continue;

                int digit = c - '0';
                int width = digitSet.Widths[digit];
                int originX = digitSet.Origins[digit].X;

                // Position for this digit: accumulatedX + width - previousOverlap
                // Binary: v35 = v15 + lWidth - lY; lEffX[i] = v35;
                digitXPositions[digitCount] = accumulatedX + width - previousOverlap;

                // Update accumulated position: accumulatedX = accumulatedX - previousOverlap + originX
                // Binary: v15 = v15 - lY + idx; (where idx is origin.x)
                accumulatedX = accumulatedX - previousOverlap + originX;

                // Calculate overlap for next digit: lY = 3 * (origin.x - width) / 5
                // Binary: lY = 3 * v34 / 5; where v34 = idx - lWidth = origin.x - width
                previousOverlap = 3 * (originX - width) / 5;

                digitCount++;
            }

            // Total width is the final accumulated position
            // Binary: lWidth = v15; (line 305)
            int totalWidth = accumulatedX;

            // Center the number: startX = screenX - totalWidth / 2
            // Binary: idx = lCenterLeft - lWidth / 2; (line 627)
            int startX = screenX - totalWidth / 2;

            // Second pass: draw each digit at the calculated position
            digitCount = 0;
            foreach (char c in damageString)
            {
                if (!char.IsDigit(c))
                    continue;

                int digit = c - '0';
                Texture2D digitTexture = digitSet.Digits[digit];
                if (digitTexture == null)
                {
                    digitCount++;
                    continue;
                }

                Point origin = digitSet.Origins[digit];

                // The X position from lEffX is relative to the canvas origin
                // Binary draws at: lEffX[idx] - origin.x (line 598)
                int drawX = startX + digitXPositions[digitCount] - origin.X;
                int drawY = screenY - origin.Y;

                spriteBatch.Draw(digitTexture, new Vector2(drawX, drawY), color);
                digitCount++;
            }
        }

        /// <summary>
        /// Draw a damage number using fallback SpriteFont.
        /// </summary>
        private void DrawFallbackDamageNumber(SpriteBatch spriteBatch, WzDamageNumber dmgNumber, int screenX, int screenY)
        {
            string text = dmgNumber.GetDamageString();
            float alpha = dmgNumber.Alpha;

            // Color based on type
            Color textColor = dmgNumber.ColorType switch
            {
                DamageColorType.Red => dmgNumber.IsCritical ? new Color(255, 170, 0) : Color.White, // Player damage to monsters
                DamageColorType.Blue => new Color(100, 150, 255),   // Healing
                DamageColorType.Violet => new Color(200, 100, 255), // Damage received from monsters
                _ => Color.White
            };

            if (dmgNumber.IsMiss)
                textColor = new Color(180, 180, 180);

            textColor *= alpha;
            Color outlineColor = new Color(0, 0, 0, (int)(200 * alpha));

            Vector2 textSize = _fallbackFont.MeasureString(text);
            Vector2 position = new Vector2(screenX - textSize.X / 2, screenY - textSize.Y / 2);

            // Draw outline
            spriteBatch.DrawString(_fallbackFont, text, position + new Vector2(-1, 0), outlineColor);
            spriteBatch.DrawString(_fallbackFont, text, position + new Vector2(1, 0), outlineColor);
            spriteBatch.DrawString(_fallbackFont, text, position + new Vector2(0, -1), outlineColor);
            spriteBatch.DrawString(_fallbackFont, text, position + new Vector2(0, 1), outlineColor);

            // Draw main text
            spriteBatch.DrawString(_fallbackFont, text, position, textColor);
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Clear all active damage numbers.
        /// </summary>
        public void Clear()
        {
            foreach (var dmgNumber in _activeNumbers)
            {
                _pool.Enqueue(dmgNumber);
            }
            _activeNumbers.Clear();
        }

        /// <summary>
        /// Get the count of active damage numbers.
        /// </summary>
        public int ActiveCount => _activeNumbers.Count;

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            Clear();
        }
        #endregion
    }
}
