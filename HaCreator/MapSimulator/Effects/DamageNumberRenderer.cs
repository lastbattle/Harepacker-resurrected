using System;
using System.Collections.Generic;
using System.Globalization;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Interaction;
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
        /// <summary>Temporary canvas height used by CAnimationDisplayer::Effect_HP.</summary>
        public const int COMPOSITE_CANVAS_HEIGHT_PX = 57;

        /// <summary>Top offset applied when registering the damage-number layer.</summary>
        public const int COMPOSITE_PLACEMENT_OFFSET_Y = 47;

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

        /// <summary>Optional BasicEff special text sprite name, such as Miss or guard.</summary>
        public string SpecialTextName { get; set; }

        /// <summary>Color type (Red for player damage, Blue for received, Violet for party)</summary>
        public DamageColorType ColorType { get; set; }

        /// <summary>Size variant (Small or Large)</summary>
        public DamageNumberSize Size { get; set; }

        /// <summary>Multi-hit combo index (for stacking)</summary>
        public int ComboIndex { get; set; }

        /// <summary>Elapsed time in ms</summary>
        public float ElapsedMs { get; set; }

        /// <summary>
        /// Client-shaped visual payload prepared once when the number is spawned.
        /// Mirrors Effect_HP building a temporary canvas before scheduling animation.
        /// </summary>
        internal DamageNumberRenderer.PreparedDamageNumberVisual PreparedVisual { get; set; }
        internal DamageNumberRenderer.PreparedDamageNumberLayer LayerState { get; set; }
        internal Texture2D CompositeCanvasTexture { get; set; }

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
                return DamageNumberRenderer.ResolveSpecialTextName(SpecialTextName);
            return DamageNumberRenderer.FormatDamageValue(Damage);
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
        private const int DamageNumberFormatStringPoolId = 0x1A15;
        #endregion

        internal readonly record struct DigitLayoutEntry(int Digit, bool UseLargeDigitSet, int RelativeX);
        internal readonly record struct PreparedDigitDrawInfo(int Digit, bool UseLargeDigitSet, int DrawOffsetX, int DrawOffsetY);
        internal readonly record struct PreparedSpriteDrawInfo(string SpriteName, int DrawOffsetX, int DrawOffsetY);
        internal readonly record struct PreparedDamageNumberCompositionInsertCommand(
            string SourceSetName,
            string SpriteName,
            bool UseLargeDigitSet,
            Point SourceOrigin,
            int SourceWidth,
            int SourceHeight,
            Point CanvasOffset);
        internal readonly record struct PreparedDamageNumberCompositionTrace(
            CanvasLayerRecoveredCanvasSettings CanvasSettings,
            PreparedDamageNumberCompositionInsertCommand[] InsertCanvasCommands,
            bool KeepsCriticalBannerOnSeparateLayer,
            PreparedSpriteDrawInfo? CriticalBannerLayerSprite);
        internal readonly record struct CompositeCanvasPlacement(int Left, int Top, int Width, int Height);
        internal readonly record struct DamageNumberAnimationTimeline(
            int HoldDurationMs,
            int FadeDurationMs,
            int TotalLifetimeMs,
            int CriticalDelayMs,
            int RiseDistancePx);
        internal readonly record struct PreparedDamageNumberLayerRegistration(
            CompositeCanvasPlacement Placement,
            DamageNumberAnimationTimeline Timeline,
            Point CriticalBannerOffset,
            bool HasCriticalBanner,
            PreparedDamageNumberCompositionTrace CompositionTrace,
            CanvasLayerInsertDescriptor[] InsertDescriptors,
            PreparedOneTimeCanvasLayerRegistration PreparedRegistration,
            CanvasLayerRecoveredLayerSettings RecoveredLayerSettings,
            CanvasLayerRecoveredRegistrationTrace RecoveredRegistrationTrace);
        internal sealed record PreparedDamageNumberLayer(
            int CanvasWidth,
            int CanvasHeight,
            DamageNumberAnimationTimeline Timeline);
        internal sealed record PreparedDamageNumberVisual(
            string DamageString,
            int CanvasWidth,
            int CanvasHeight,
            PreparedDigitDrawInfo[] Digits,
            PreparedSpriteDrawInfo? MissSprite,
            PreparedSpriteDrawInfo? CriticalBannerSprite,
            PreparedDamageNumberCompositionTrace CompositionTrace);

        #region State
        private readonly List<WzDamageNumber> _activeNumbers = new List<WzDamageNumber>();
        private readonly Queue<WzDamageNumber> _pool = new Queue<WzDamageNumber>();
        private GraphicsDevice _device;
        private bool _initialized = false;
        private AnimationEffects _animationEffects;

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

        public void SetAnimationEffects(AnimationEffects animationEffects)
        {
            _animationEffects = animationEffects;
        }
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
            bool isCritical, bool isMiss, int currentTime, int comboIndex = 0, string specialTextName = null)
        {
            // Limit active numbers
            if (_activeNumbers.Count >= MAX_ACTIVE_NUMBERS)
            {
                var oldest = _activeNumbers[0];
                _activeNumbers.RemoveAt(0);
                ReleaseCompositeCanvas(oldest);
                _pool.Enqueue(oldest);
            }

            // Get from pool or create new
            var dmgNumber = _pool.Count > 0 ? _pool.Dequeue() : new WzDamageNumber();

            bool useCriticalPresentation = UsesCriticalPresentation(colorType, isCritical);

            // Apply stacking offset for multi-hit
            float stackedY = y - (comboIndex * DamageNumberConstants.MULTI_HIT_STACK_OFFSET_Y);

            // Initialize
            dmgNumber.Damage = damage;
            dmgNumber.X = x;
            dmgNumber.BaseY = stackedY;
            dmgNumber.SpawnTime = currentTime;
            dmgNumber.IsCritical = useCriticalPresentation;
            dmgNumber.IsMiss = isMiss;
            dmgNumber.SpecialTextName = isMiss ? ResolveSpecialTextName(specialTextName) : null;
            dmgNumber.ColorType = colorType;
            dmgNumber.Size = useCriticalPresentation ? DamageNumberSize.Large : DamageNumberSize.Small;
            dmgNumber.ComboIndex = comboIndex;
            dmgNumber.ElapsedMs = 0f; // Reset for pooled objects
            dmgNumber.PreparedVisual = null;
            dmgNumber.LayerState = null;
            ReleaseCompositeCanvas(dmgNumber);

            if (HasWzSprites)
            {
                dmgNumber.PreparedVisual = PrepareVisual(dmgNumber);
                dmgNumber.LayerState = PrepareLayer(dmgNumber.PreparedVisual);
                dmgNumber.CompositeCanvasTexture = CreateCompositeCanvasTexture(dmgNumber.PreparedVisual, colorType, useCriticalPresentation);

                if (TryRegisterPreparedCanvasLayer(dmgNumber))
                {
                    return;
                }
            }

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

        public void SpawnSpecialText(string specialTextName, float x, float y, DamageColorType colorType, int currentTime)
        {
            if (string.IsNullOrWhiteSpace(specialTextName))
            {
                SpawnMiss(x, y, colorType, currentTime);
                return;
            }

            SpawnDamageNumber(0, x, y, colorType, false, true, currentTime, 0, specialTextName.Trim());
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
                    ReleaseCompositeCanvas(dmgNumber);
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
        /// Binary analysis (address 0x444eb0) revealed the spacing formula:
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
            DamageNumberDigitSet largeDigitSet = ResolveLargeDigitSet(dmgNumber.ColorType, dmgNumber.IsCritical);
            DamageNumberDigitSet smallDigitSet = ResolveSmallDigitSet(dmgNumber.ColorType, dmgNumber.IsCritical);
            if (largeDigitSet == null || smallDigitSet == null)
                return;

            PreparedDamageNumberVisual visual = dmgNumber.PreparedVisual ?? PrepareVisual(dmgNumber);
            dmgNumber.PreparedVisual = visual;
            dmgNumber.LayerState ??= PrepareLayer(visual);
            float alpha = dmgNumber.Alpha;
            Color color = Color.White * alpha;

            int compositeCanvasWidth = dmgNumber.LayerState?.CanvasWidth ?? visual.CanvasWidth;
            if (dmgNumber.CompositeCanvasTexture != null && compositeCanvasWidth <= 0)
            {
                compositeCanvasWidth = dmgNumber.CompositeCanvasTexture.Width;
            }

            CompositeCanvasPlacement placement = ResolveCompositeCanvasPlacement(
                screenX,
                screenY,
                compositeCanvasWidth);

            if (dmgNumber.CompositeCanvasTexture != null)
            {
                spriteBatch.Draw(
                    dmgNumber.CompositeCanvasTexture,
                    new Vector2(placement.Left, placement.Top),
                    color);
            }
            else if (visual.MissSprite is PreparedSpriteDrawInfo missSprite
                && smallDigitSet.SpecialTextures.TryGetValue(missSprite.SpriteName, out var missTexture))
            {
                spriteBatch.Draw(
                    missTexture,
                    new Vector2(placement.Left + missSprite.DrawOffsetX, placement.Top + missSprite.DrawOffsetY),
                    color);
                return;
            }

            if (dmgNumber.ShouldShowCriticalEffect
                && visual.CriticalBannerSprite is PreparedSpriteDrawInfo criticalSprite
                && largeDigitSet.CriticalEffectTexture != null)
            {
                spriteBatch.Draw(
                    largeDigitSet.CriticalEffectTexture,
                    new Vector2(placement.Left + criticalSprite.DrawOffsetX, placement.Top + criticalSprite.DrawOffsetY),
                    color);
            }

            if (dmgNumber.CompositeCanvasTexture != null)
            {
                return;
            }

            foreach (PreparedDigitDrawInfo entry in visual.Digits)
            {
                DamageNumberDigitSet digitSet = entry.UseLargeDigitSet ? largeDigitSet : smallDigitSet;
                Texture2D digitTexture = digitSet.Digits[entry.Digit];
                if (digitTexture == null)
                    continue;

                spriteBatch.Draw(
                    digitTexture,
                    new Vector2(placement.Left + entry.DrawOffsetX, placement.Top + entry.DrawOffsetY),
                    color);
            }
        }

        private static PreparedDamageNumberVisual PrepareVisual(WzDamageNumber dmgNumber)
        {
            DamageNumberDigitSet largeDigitSet = ResolveLargeDigitSet(dmgNumber.ColorType, dmgNumber.IsCritical);
            DamageNumberDigitSet smallDigitSet = ResolveSmallDigitSet(dmgNumber.ColorType, dmgNumber.IsCritical);

            if (largeDigitSet == null || smallDigitSet == null)
            {
                return new PreparedDamageNumberVisual(
                    dmgNumber.GetDamageString(),
                    0,
                    DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX,
                    Array.Empty<PreparedDigitDrawInfo>(),
                    null,
                    null,
                    CreateEmptyCompositionTrace());
            }

            return PrepareVisual(
                dmgNumber.Damage,
                dmgNumber.ColorType,
                dmgNumber.IsCritical,
                dmgNumber.IsMiss,
                dmgNumber.SpecialTextName,
                largeDigitSet,
                smallDigitSet);
        }

        internal static bool UsesCriticalPresentation(DamageColorType colorType, bool isCritical)
        {
            return colorType == DamageColorType.Red && isCritical;
        }

        internal static string ResolveSpecialTextName(string specialTextName)
        {
            return string.IsNullOrWhiteSpace(specialTextName) ? "Miss" : specialTextName.Trim();
        }

        internal static string FormatDamageValue(int damage)
        {
            string compositeFormat = MapleStoryStringPool.GetCompositeFormatOrFallback(
                DamageNumberFormatStringPoolId,
                "{0}",
                1,
                out _);

            try
            {
                return string.Format(CultureInfo.InvariantCulture, compositeFormat, damage);
            }
            catch (FormatException)
            {
                return damage.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static (DigitLayoutEntry[] Entries, int TotalWidth) BuildDigitLayout(
            string damageString,
            DamageNumberDigitSet largeDigitSet,
            DamageNumberDigitSet smallDigitSet,
            bool addCriticalSpacing)
        {
            if (string.IsNullOrEmpty(damageString))
                return (Array.Empty<DigitLayoutEntry>(), 0);

            List<DigitLayoutEntry> entries = new(damageString.Length);
            int accumulatedX = addCriticalSpacing ? 30 : 0;
            int previousOverlap = 0;
            bool useLargeDigitSet = true;

            foreach (char c in damageString)
            {
                if (!char.IsDigit(c))
                    continue;

                int digit = c - '0';
                DamageNumberDigitSet digitSet = useLargeDigitSet ? largeDigitSet : smallDigitSet;
                int width = digitSet.Widths[digit];
                int originX = digitSet.Origins[digit].X;
                int relativeX = accumulatedX + width - previousOverlap;

                entries.Add(new DigitLayoutEntry(digit, useLargeDigitSet, relativeX));

                accumulatedX = accumulatedX - previousOverlap + originX;
                previousOverlap = 3 * (originX - width) / 5;
                useLargeDigitSet = false;
            }

            return (entries.ToArray(), accumulatedX);
        }

        internal static PreparedDamageNumberVisual PrepareVisual(
            int damage,
            DamageColorType colorType,
            bool isCritical,
            bool isMiss,
            DamageNumberDigitSet largeDigitSet,
            DamageNumberDigitSet smallDigitSet)
        {
            return PrepareVisual(
                damage,
                colorType,
                isCritical,
                isMiss,
                specialTextName: null,
                largeDigitSet,
                smallDigitSet);
        }

        internal static PreparedDamageNumberVisual PrepareVisual(
            int damage,
            DamageColorType colorType,
            bool isCritical,
            bool isMiss,
            string specialTextName,
            DamageNumberDigitSet largeDigitSet,
            DamageNumberDigitSet smallDigitSet)
        {
            string damageString = isMiss ? ResolveSpecialTextName(specialTextName) : FormatDamageValue(damage);
            bool useCriticalPresentation = UsesCriticalPresentation(colorType, isCritical);

            if (isMiss)
            {
                PreparedSpriteDrawInfo? missSprite = null;
                int canvasWidth = 0;
                int canvasHeight = ResolveCompositeCanvasHeight();

                if (smallDigitSet.SpecialOrigins.TryGetValue(damageString, out Point missOrigin))
                {
                    canvasWidth = ResolveSpecialTextWidth(smallDigitSet, damageString);
                    int canvasOffsetX = canvasWidth > 0
                        ? (canvasWidth / 2) - missOrigin.X
                        : -missOrigin.X;
                    missSprite = new PreparedSpriteDrawInfo(
                        damageString,
                        canvasOffsetX,
                        DamageNumberConstants.COMPOSITE_PLACEMENT_OFFSET_Y - missOrigin.Y);
                }

                return new PreparedDamageNumberVisual(
                    damageString,
                    canvasWidth,
                    canvasHeight,
                    Array.Empty<PreparedDigitDrawInfo>(),
                    missSprite,
                    null,
                    BuildRecoveredSpecialTextCompositionTrace(
                        damageString,
                        canvasWidth,
                        canvasHeight,
                        missSprite,
                        smallDigitSet));
            }

            (DigitLayoutEntry[] layoutEntries, int totalWidth) = BuildDigitLayout(
                damageString,
                largeDigitSet,
                smallDigitSet,
                useCriticalPresentation);

            int baselineY = DamageNumberConstants.COMPOSITE_PLACEMENT_OFFSET_Y;
            PreparedDigitDrawInfo[] digits = new PreparedDigitDrawInfo[layoutEntries.Length];

            for (int i = 0; i < layoutEntries.Length; i++)
            {
                DigitLayoutEntry entry = layoutEntries[i];
                DamageNumberDigitSet digitSet = entry.UseLargeDigitSet ? largeDigitSet : smallDigitSet;
                Point origin = digitSet.Origins[entry.Digit];
                digits[i] = new PreparedDigitDrawInfo(
                    entry.Digit,
                    entry.UseLargeDigitSet,
                    entry.RelativeX - origin.X,
                    baselineY - origin.Y);
            }

            PreparedSpriteDrawInfo? criticalBanner = null;
            if (useCriticalPresentation
                && (largeDigitSet.CriticalEffectTexture != null || largeDigitSet.CriticalEffectOrigin != Point.Zero))
            {
                criticalBanner = new PreparedSpriteDrawInfo(
                    "effect",
                    -(largeDigitSet.CriticalEffectOrigin.X - totalWidth / 2),
                    DamageNumberConstants.COMPOSITE_PLACEMENT_OFFSET_Y
                    + DamageNumberConstants.CRITICAL_EFFECT_OFFSET_Y
                    - largeDigitSet.CriticalEffectOrigin.Y);
            }

            return new PreparedDamageNumberVisual(
                damageString,
                Math.Max(0, totalWidth),
                ResolveCompositeCanvasHeight(),
                digits,
                null,
                criticalBanner,
                BuildRecoveredCompositionTrace(
                    Math.Max(0, totalWidth),
                    ResolveCompositeCanvasHeight(),
                    digits,
                    largeDigitSet,
                    smallDigitSet,
                    criticalBanner));
        }

        private static PreparedDamageNumberCompositionTrace CreateEmptyCompositionTrace()
        {
            return new PreparedDamageNumberCompositionTrace(
                new CanvasLayerRecoveredCanvasSettings(0, ResolveCompositeCanvasHeight()),
                Array.Empty<PreparedDamageNumberCompositionInsertCommand>(),
                KeepsCriticalBannerOnSeparateLayer: false,
                CriticalBannerLayerSprite: null);
        }

        private static PreparedDamageNumberCompositionTrace BuildRecoveredCompositionTrace(
            int canvasWidth,
            int canvasHeight,
            IReadOnlyList<PreparedDigitDrawInfo> digits,
            DamageNumberDigitSet largeDigitSet,
            DamageNumberDigitSet smallDigitSet,
            PreparedSpriteDrawInfo? criticalBanner)
        {
            if (digits == null || digits.Count == 0)
            {
                return new PreparedDamageNumberCompositionTrace(
                    new CanvasLayerRecoveredCanvasSettings(canvasWidth, canvasHeight),
                    Array.Empty<PreparedDamageNumberCompositionInsertCommand>(),
                    criticalBanner.HasValue,
                    criticalBanner);
            }

            PreparedDamageNumberCompositionInsertCommand[] insertCommands =
                new PreparedDamageNumberCompositionInsertCommand[digits.Count];
            for (int i = 0; i < digits.Count; i++)
            {
                PreparedDigitDrawInfo digit = digits[i];
                DamageNumberDigitSet digitSet = digit.UseLargeDigitSet ? largeDigitSet : smallDigitSet;
                insertCommands[i] = new PreparedDamageNumberCompositionInsertCommand(
                    digitSet?.Name ?? string.Empty,
                    digit.Digit.ToString(CultureInfo.InvariantCulture),
                    digit.UseLargeDigitSet,
                    digitSet?.Origins[digit.Digit] ?? Point.Zero,
                    digitSet?.Widths[digit.Digit] ?? 0,
                    digitSet?.Heights[digit.Digit] ?? 0,
                    new Point(digit.DrawOffsetX, digit.DrawOffsetY));
            }

            return new PreparedDamageNumberCompositionTrace(
                new CanvasLayerRecoveredCanvasSettings(canvasWidth, canvasHeight),
                insertCommands,
                criticalBanner.HasValue,
                criticalBanner);
        }

        private static PreparedDamageNumberCompositionTrace BuildRecoveredSpecialTextCompositionTrace(
            string spriteName,
            int canvasWidth,
            int canvasHeight,
            PreparedSpriteDrawInfo? specialSprite,
            DamageNumberDigitSet digitSet)
        {
            if (!specialSprite.HasValue)
            {
                return new PreparedDamageNumberCompositionTrace(
                    new CanvasLayerRecoveredCanvasSettings(canvasWidth, canvasHeight),
                    Array.Empty<PreparedDamageNumberCompositionInsertCommand>(),
                    KeepsCriticalBannerOnSeparateLayer: false,
                    CriticalBannerLayerSprite: null);
            }

            Point sourceOrigin = digitSet?.SpecialOrigins.TryGetValue(spriteName, out Point origin) == true
                ? origin
                : Point.Zero;
            int sourceWidth = ResolveSpecialTextWidth(digitSet, spriteName);
            int sourceHeight = ResolveSpecialTextHeight(digitSet, spriteName);
            PreparedDamageNumberCompositionInsertCommand insertCommand = new(
                digitSet?.Name ?? string.Empty,
                spriteName ?? string.Empty,
                UseLargeDigitSet: false,
                sourceOrigin,
                sourceWidth,
                sourceHeight,
                new Point(specialSprite.Value.DrawOffsetX, specialSprite.Value.DrawOffsetY));

            return new PreparedDamageNumberCompositionTrace(
                new CanvasLayerRecoveredCanvasSettings(canvasWidth, canvasHeight),
                new[] { insertCommand },
                KeepsCriticalBannerOnSeparateLayer: false,
                CriticalBannerLayerSprite: null);
        }

        private static int ResolveSpecialTextWidth(DamageNumberDigitSet digitSet, string spriteName)
        {
            if (digitSet != null
                && !string.IsNullOrWhiteSpace(spriteName))
            {
                if (digitSet.SpecialWidths.TryGetValue(spriteName, out int width))
                {
                    return width;
                }

                if (digitSet.SpecialTextures.TryGetValue(spriteName, out Texture2D texture))
                {
                    return texture?.Width ?? 0;
                }
            }

            return 0;
        }

        private static int ResolveSpecialTextHeight(DamageNumberDigitSet digitSet, string spriteName)
        {
            if (digitSet != null
                && !string.IsNullOrWhiteSpace(spriteName))
            {
                if (digitSet.SpecialHeights.TryGetValue(spriteName, out int height))
                {
                    return height;
                }

                if (digitSet.SpecialTextures.TryGetValue(spriteName, out Texture2D texture))
                {
                    return texture?.Height ?? 0;
                }
            }

            return 0;
        }

        internal static int ResolveCompositeCanvasHeight()
        {
            return DamageNumberConstants.COMPOSITE_CANVAS_HEIGHT_PX;
        }

        internal static DamageNumberAnimationTimeline ResolveAnimationTimeline()
        {
            return new DamageNumberAnimationTimeline(
                DamageNumberConstants.DISPLAY_DURATION_MS,
                DamageNumberConstants.FADE_DURATION_MS,
                DamageNumberConstants.TOTAL_LIFETIME_MS,
                DamageNumberConstants.CRITICAL_EFFECT_DELAY_MS,
                (int)DamageNumberConstants.RISE_DISTANCE_PX);
        }

        internal static PreparedDamageNumberLayer PrepareLayer(PreparedDamageNumberVisual visual)
        {
            return new PreparedDamageNumberLayer(
                visual.CanvasWidth,
                visual.CanvasHeight,
                ResolveAnimationTimeline());
        }

        internal static PreparedDamageNumberLayerRegistration BuildOneTimeLayerRegistration(
            PreparedDamageNumberVisual visual,
            PreparedDamageNumberLayer layer,
            int centerX,
            int centerTop)
        {
            CompositeCanvasPlacement placement = ResolveCompositeCanvasPlacement(
                centerX,
                centerTop,
                layer?.CanvasWidth ?? visual?.CanvasWidth ?? 0);
            PreparedSpriteDrawInfo? criticalBanner = visual?.CriticalBannerSprite;
            DamageNumberAnimationTimeline timeline = layer?.Timeline ?? ResolveAnimationTimeline();
            Point criticalBannerOffset = criticalBanner is PreparedSpriteDrawInfo banner
                ? new Point(banner.DrawOffsetX, banner.DrawOffsetY)
                : Point.Zero;
            bool hasCriticalBanner = criticalBanner.HasValue;
            CanvasLayerRecoveredLayerSettings recoveredLayerSettings = ResolveRecoveredLayerSettings();
            CanvasLayerInsertDescriptor[] insertDescriptors = OneTimeCanvasLayerAnimation.BuildInsertDescriptors(
                timeline.HoldDurationMs,
                timeline.FadeDurationMs,
                timeline.RiseDistancePx,
                hasCriticalBanner,
                criticalBannerOffset,
                timeline.CriticalDelayMs);
            CanvasLayerRecoveredRegistrationTrace recoveredRegistrationTrace =
                OneTimeCanvasLayerAnimation.BuildRecoveredRegistrationTrace(
                    placement.Left,
                    placement.Top,
                    placement.Width,
                    placement.Height,
                    insertDescriptors,
                    recoveredLayerSettings,
                    registersOneTimeAnimation: true);
            return new PreparedDamageNumberLayerRegistration(
                placement,
                timeline,
                criticalBannerOffset,
                hasCriticalBanner,
                visual?.CompositionTrace ?? CreateEmptyCompositionTrace(),
                insertDescriptors,
                new PreparedOneTimeCanvasLayerRegistration(
                    placement.Left,
                    placement.Top,
                    insertDescriptors,
                    recoveredLayerSettings,
                    recoveredRegistrationTrace),
                recoveredLayerSettings,
                recoveredRegistrationTrace);
        }

        internal static CanvasLayerRecoveredLayerSettings ResolveRecoveredLayerSettings()
        {
            return new CanvasLayerRecoveredLayerSettings(
                CreateLayerCanvasValue: 0,
                InitialLayerOptionValue: unchecked((int)0xC0050004),
                LayerPriorityValue: -1,
                FinalizeLayerOptionValue: 0);
        }

        internal static CompositeCanvasPlacement ResolveCompositeCanvasPlacement(
            int centerX,
            int centerTop,
            int totalWidth)
        {
            int width = Math.Max(0, totalWidth);
            int height = ResolveCompositeCanvasHeight();
            int left = centerX - width / 2;
            int top = centerTop - DamageNumberConstants.COMPOSITE_PLACEMENT_OFFSET_Y;
            return new CompositeCanvasPlacement(left, top, width, height);
        }

        private static DamageNumberDigitSet ResolveAnyLoadedDigitSet(string primarySetName, string fallbackSetName)
        {
            DamageNumberDigitSet digitSet = DamageNumberLoader.GetDigitSetByName(primarySetName);
            if (digitSet?.IsLoaded == true)
                return digitSet;

            digitSet = DamageNumberLoader.GetDigitSetByName(fallbackSetName);
            if (digitSet?.IsLoaded == true)
                return digitSet;

            digitSet = DamageNumberLoader.GetDigitSetByName("NoRed0");
            return digitSet?.IsLoaded == true ? digitSet : null;
        }

        private static DamageNumberDigitSet ResolveLargeDigitSet(DamageColorType colorType, bool isCritical)
        {
            if (isCritical)
                return ResolveAnyLoadedDigitSet("NoCri1", "NoRed1");

            string setName = colorType switch
            {
                DamageColorType.Blue => "NoBlue1",
                DamageColorType.Violet => "NoViolet1",
                _ => "NoRed1"
            };

            return ResolveAnyLoadedDigitSet(setName, "NoRed1");
        }

        private static DamageNumberDigitSet ResolveSmallDigitSet(DamageColorType colorType, bool isCritical)
        {
            if (isCritical)
                return ResolveAnyLoadedDigitSet("NoCri0", "NoRed0");

            string setName = colorType switch
            {
                DamageColorType.Blue => "NoBlue0",
                DamageColorType.Violet => "NoViolet0",
                _ => "NoRed0"
            };

            return ResolveAnyLoadedDigitSet(setName, "NoRed0");
        }

        private Texture2D CreateCompositeCanvasTexture(
            PreparedDamageNumberVisual visual,
            DamageColorType colorType,
            bool isCritical)
        {
            if (_device == null || visual == null)
                return null;

            DamageNumberDigitSet largeDigitSet = ResolveLargeDigitSet(colorType, isCritical);
            DamageNumberDigitSet smallDigitSet = ResolveSmallDigitSet(colorType, isCritical);
            if (largeDigitSet == null || smallDigitSet == null)
                return null;

            int canvasWidth = visual.CanvasWidth;
            int canvasHeight = visual.CanvasHeight;

            if (visual.MissSprite is PreparedSpriteDrawInfo missSprite
                && smallDigitSet.SpecialTextures.TryGetValue(missSprite.SpriteName, out Texture2D missTexture))
            {
                canvasWidth = Math.Max(canvasWidth, missTexture.Width);
                canvasHeight = Math.Max(canvasHeight, missTexture.Height);
            }

            if (canvasWidth <= 0 || canvasHeight <= 0)
                return null;

            RenderTarget2D renderTarget = new RenderTarget2D(
                _device,
                canvasWidth,
                canvasHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);

            _device.SetRenderTarget(renderTarget);
            _device.Clear(Color.Transparent);

            using (SpriteBatch compositeBatch = new SpriteBatch(_device))
            {
                compositeBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);

                if (visual.MissSprite is PreparedSpriteDrawInfo preparedMiss
                    && smallDigitSet.SpecialTextures.TryGetValue(preparedMiss.SpriteName, out missTexture))
                {
                    compositeBatch.Draw(
                        missTexture,
                        new Vector2(preparedMiss.DrawOffsetX, preparedMiss.DrawOffsetY),
                        Color.White);
                }
                else
                {
                    foreach (PreparedDigitDrawInfo entry in visual.Digits)
                    {
                        DamageNumberDigitSet digitSet = entry.UseLargeDigitSet ? largeDigitSet : smallDigitSet;
                        Texture2D digitTexture = digitSet.Digits[entry.Digit];
                        if (digitTexture == null)
                            continue;

                        compositeBatch.Draw(
                            digitTexture,
                            new Vector2(entry.DrawOffsetX, entry.DrawOffsetY),
                            Color.White);
                    }
                }

                compositeBatch.End();
            }

            _device.SetRenderTarget(null);
            return renderTarget;
        }

        private bool TryRegisterPreparedCanvasLayer(WzDamageNumber dmgNumber)
        {
            if (_animationEffects == null
                || dmgNumber?.PreparedVisual == null
                || dmgNumber.LayerState == null
                || dmgNumber.CompositeCanvasTexture == null)
            {
                return false;
            }

            PreparedDamageNumberLayerRegistration registration = BuildOneTimeLayerRegistration(
                dmgNumber.PreparedVisual,
                dmgNumber.LayerState,
                (int)Math.Round(dmgNumber.X),
                (int)Math.Round(dmgNumber.BaseY));

            DamageNumberDigitSet largeDigitSet = ResolveLargeDigitSet(dmgNumber.ColorType, dmgNumber.IsCritical);
            Texture2D criticalBannerTexture = registration.HasCriticalBanner
                ? largeDigitSet?.CriticalEffectTexture
                : null;

            _animationEffects.RegisterOneTimeCanvasLayer(
                dmgNumber.CompositeCanvasTexture,
                dmgNumber.SpawnTime,
                registration.PreparedRegistration,
                criticalBannerTexture,
                ownsCanvasTexture: true,
                owner: AnimationCanvasLayerOwner.DamageNumber);

            dmgNumber.CompositeCanvasTexture = null;
            dmgNumber.PreparedVisual = null;
            dmgNumber.LayerState = null;
            _pool.Enqueue(dmgNumber);
            return true;
        }

        private static void ReleaseCompositeCanvas(WzDamageNumber dmgNumber)
        {
            if (dmgNumber?.CompositeCanvasTexture == null)
                return;

            dmgNumber.CompositeCanvasTexture.Dispose();
            dmgNumber.CompositeCanvasTexture = null;
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
            _animationEffects?.ClearDamageNumberLayers();
            foreach (var dmgNumber in _activeNumbers)
            {
                ReleaseCompositeCanvas(dmgNumber);
                _pool.Enqueue(dmgNumber);
            }
            _activeNumbers.Clear();
        }

        /// <summary>
        /// Get the count of active damage numbers.
        /// </summary>
        public int ActiveCount => _activeNumbers.Count + (_animationEffects?.DamageNumberLayerCount ?? 0);

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
