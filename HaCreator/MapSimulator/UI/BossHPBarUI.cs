using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Entities;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Boss HP bar UI display - shows boss HP gauge at top of screen.
    /// Based on MapleStory client CMobGage implementation.
    ///
    /// WZ Structure (UI.wz/UIWindow.img/MobGage):
    /// - backgrnd: Left frame piece with boss icon slot (35x37)
    /// - backgrnd2: Transition piece after icon (4x17)
    /// - backgrnd3: Center 1px strip for horizontal tiling (1x17)
    /// - backgrnd4: Right end cap (5x18)
    /// - Gage/<hpTagColor>/<0,1>: Color-coded gauge strips
    ///   - hpTagColor 1-7 for different boss colors
    ///   - 0 = background/empty gauge (1px wide)
    ///   - 1 = filled gauge (1px wide)
    /// - Mob/<mobId>: Boss-specific icons (25x25)
    /// </summary>
    public class BossHPBarUI
    {
        #region Constants
        // Layout constants matching official client
        private const int BAR_Y_OFFSET = 10;              // Distance from top of screen
        private const int BAR_MARGIN = 10;                // Margin on left side
        private const int ICON_OFFSET_X = 5;              // Icon X offset within backgrnd
        private const int ICON_OFFSET_Y = 4;              // Icon Y offset within backgrnd (aligned with gauge top)
        private const int GAUGE_OFFSET_X = 35;            // Gauge start X (after backgrnd icon area)
        private const int GAUGE_OFFSET_Y = 10;            // Gauge Y offset from frame top

        // Animation constants
        private const float HP_ANIMATION_SPEED = 2.5f;    // Percent per frame
        private const float HP_DAMAGE_TRAIL_SPEED = 1.0f; // Damage trail animation speed
        private const int FADE_IN_DURATION = 300;         // Fade in duration in ms
        private const int FADE_OUT_DURATION = 500;        // Fade out after boss dies

        // Default gauge width when WZ textures unavailable
        private const int DEFAULT_GAUGE_WIDTH = 300;
        #endregion

        #region Textures (loaded from WZ)
        // Background frame pieces (official names: backgrnd, backgrnd2, backgrnd3, backgrnd4)
        private Texture2D _texBackgrnd;   // Left frame with icon slot (35x37)
        private Texture2D _texBackgrnd2;  // Transition piece (4x17)
        private Texture2D _texBackgrnd3;  // 1px center strip (tiled horizontally)
        private Texture2D _texBackgrnd4;  // Right end cap (5x18)

        // Gauge textures indexed by hpTagColor (1-7)
        // Each contains [0] = background/empty, [1] = filled
        private readonly Dictionary<int, Texture2D[]> _gaugeTextures = new Dictionary<int, Texture2D[]>();

        // Boss-specific icons indexed by mobId
        private readonly Dictionary<int, Texture2D> _bossIcons = new Dictionary<int, Texture2D>();

        // Fallback pixel texture
        private Texture2D _pixelTexture;
        #endregion

        #region State
        private bool _isInitialized;
        private bool _hasWzTextures;
        private SpriteFont _font;
        private GraphicsDevice _device;

        // Active boss tracking
        private readonly List<BossDisplayInfo> _activeBosses = new List<BossDisplayInfo>();
        private const int MAX_BOSSES = 5;

        // WZ references
        private WzSubProperty _mobGageProperty;

        // Mouse position for hover detection
        private Point _mousePosition;

        // Frame dimensions (from loaded textures)
        private int _frameHeight = 37;
        private int _gaugeHeight = 10;
        #endregion

        #region Boss Display Info
        private class BossDisplayInfo
        {
            public MobItem Boss { get; set; }
            public string BossName { get; set; }
            public int Level { get; set; }
            public int MobId { get; set; }

            // HP state
            public float CurrentHpPercent { get; set; }
            public float DisplayedHpPercent { get; set; }
            public float TrailHpPercent { get; set; }

            // Animation state
            public float Alpha { get; set; } = 1.0f;
            public int SpawnTime { get; set; }
            public int LastDamageTime { get; set; }
            public bool IsDying { get; set; }
            public int DeathTime { get; set; }

            // Colors based on hpTagColor/hpTagBgcolor
            public short HpTagColorId { get; set; } = 1;
            public short HpTagBgColorId { get; set; } = 1;

            // Icon bounds for hover detection
            public Rectangle IconBounds { get; set; }
        }
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device, SpriteFont font)
        {
            _device = device;
            _font = font;

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            _isInitialized = true;
        }

        /// <summary>
        /// Load textures from WZ files using correct property names
        /// </summary>
        public void LoadFromWz(WzSubProperty mobGageProperty, GraphicsDevice device)
        {
            if (mobGageProperty == null)
            {
                _hasWzTextures = false;
                return;
            }

            _mobGageProperty = mobGageProperty;

            try
            {
                // Load background frame pieces with correct lowercase names
                _texBackgrnd = LoadCanvasTexture(mobGageProperty["backgrnd"] as WzCanvasProperty, device);
                _texBackgrnd2 = LoadCanvasTexture(mobGageProperty["backgrnd2"] as WzCanvasProperty, device);
                _texBackgrnd3 = LoadCanvasTexture(mobGageProperty["backgrnd3"] as WzCanvasProperty, device);
                _texBackgrnd4 = LoadCanvasTexture(mobGageProperty["backgrnd4"] as WzCanvasProperty, device);

                // Update frame dimensions from loaded textures
                if (_texBackgrnd != null)
                    _frameHeight = _texBackgrnd.Height;

                // Load gauge textures for all color variations (1-7)
                WzSubProperty gageProperty = mobGageProperty["Gage"] as WzSubProperty;
                if (gageProperty != null)
                {
                    for (int colorId = 1; colorId <= 7; colorId++)
                    {
                        WzSubProperty colorGage = gageProperty[colorId.ToString()] as WzSubProperty;
                        if (colorGage != null)
                        {
                            Texture2D bgGauge = LoadCanvasTexture(colorGage["0"] as WzCanvasProperty, device);
                            Texture2D fillGauge = LoadCanvasTexture(colorGage["1"] as WzCanvasProperty, device);

                            if (bgGauge != null || fillGauge != null)
                            {
                                _gaugeTextures[colorId] = new Texture2D[] { bgGauge, fillGauge };

                                // Update gauge height from first loaded texture
                                if (fillGauge != null && _gaugeHeight < fillGauge.Height)
                                    _gaugeHeight = fillGauge.Height;
                            }
                        }
                    }
                }

                // Check if we have minimum required textures
                _hasWzTextures = _texBackgrnd != null && _gaugeTextures.Count > 0;
            }
            catch (Exception)
            {
                _hasWzTextures = false;
            }
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas, GraphicsDevice device)
        {
            if (canvas == null) return null;

            try
            {
                var bitmap = canvas.GetLinkedWzCanvasBitmap();
                if (bitmap != null)
                {
                    return BitmapToTexture2D(bitmap, device);
                }
            }
            catch { }

            return null;
        }

        private Texture2D BitmapToTexture2D(System.Drawing.Bitmap bitmap, GraphicsDevice device)
        {
            if (bitmap == null) return null;

            var texture = new Texture2D(device, bitmap.Width, bitmap.Height);
            var data = new Color[bitmap.Width * bitmap.Height];

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    data[y * bitmap.Width + x] = new Color(pixel.R, pixel.G, pixel.B, pixel.A);
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Load boss icon from MobGage/Mob/<mobId>
        /// </summary>
        private Texture2D LoadBossIcon(int mobId)
        {
            if (_bossIcons.TryGetValue(mobId, out var cached))
                return cached;

            if (_mobGageProperty == null || _device == null)
                return null;

            try
            {
                var mobProperty = _mobGageProperty["Mob"] as WzSubProperty;
                if (mobProperty == null) return null;

                var iconProp = mobProperty[mobId.ToString()];
                if (iconProp == null) return null;

                WzCanvasProperty canvasProp = null;
                if (iconProp is WzUOLProperty uol)
                    canvasProp = uol.LinkValue as WzCanvasProperty;
                else if (iconProp is WzCanvasProperty canvas)
                    canvasProp = canvas;

                if (canvasProp != null)
                {
                    var texture = LoadCanvasTexture(canvasProp, _device);
                    if (texture != null)
                        _bossIcons[mobId] = texture;
                    return texture;
                }
            }
            catch { }

            return null;
        }
        #endregion

        #region Boss Management
        public void TrackBoss(MobItem boss, int currentTime)
        {
            if (boss?.AI == null)
                return;

            var mobData = boss.MobInstance?.MobInfo?.MobData;
            if (mobData == null || mobData.HpTagColor <= 0)
                return;

            var existing = _activeBosses.Find(b => b.Boss?.PoolId == boss.PoolId);
            if (existing != null)
            {
                UpdateBossInfo(existing, boss, currentTime);
                return;
            }

            if (_activeBosses.Count < MAX_BOSSES)
            {
                int mobId = 0;
                int.TryParse(boss.MobInstance?.MobInfo?.ID, out mobId);

                var info = new BossDisplayInfo
                {
                    Boss = boss,
                    BossName = boss.MobInstance?.MobInfo?.Name ?? "Boss",
                    Level = boss.AI.Level,
                    MobId = mobId,
                    CurrentHpPercent = boss.AI.HpPercent,
                    DisplayedHpPercent = boss.AI.HpPercent,
                    TrailHpPercent = boss.AI.HpPercent,
                    SpawnTime = currentTime,
                    LastDamageTime = currentTime,
                    HpTagColorId = mobData.HpTagColor,
                    HpTagBgColorId = mobData.HpTagBgColor,
                    Alpha = 0f
                };

                _activeBosses.Add(info);
            }
        }

        public void OnBossDamaged(MobItem boss, int currentTime)
        {
            var info = _activeBosses.Find(b => b.Boss?.PoolId == boss.PoolId);
            if (info != null)
            {
                info.LastDamageTime = currentTime;
                info.CurrentHpPercent = boss.AI.HpPercent;
            }
            else
            {
                TrackBoss(boss, currentTime);
            }
        }

        public void RemoveBoss(int poolId, int currentTime)
        {
            var info = _activeBosses.Find(b => b.Boss?.PoolId == poolId);
            if (info != null)
            {
                info.IsDying = true;
                info.DeathTime = currentTime;
            }
        }

        private void UpdateBossInfo(BossDisplayInfo info, MobItem boss, int currentTime)
        {
            float newHpPercent = boss.AI.HpPercent;
            if (newHpPercent < info.CurrentHpPercent)
                info.LastDamageTime = currentTime;
            info.CurrentHpPercent = newHpPercent;
        }
        #endregion

        #region Update
        public void Update(int currentTime, float deltaTime)
        {
            if (!_isInitialized || _activeBosses.Count == 0)
                return;

            for (int i = _activeBosses.Count - 1; i >= 0; i--)
            {
                var info = _activeBosses[i];

                if (info.Boss?.AI != null && !info.IsDying)
                {
                    info.CurrentHpPercent = info.Boss.AI.HpPercent;
                    if (info.Boss.AI.IsDead)
                    {
                        info.IsDying = true;
                        info.DeathTime = currentTime;
                    }
                }

                // Fade in
                int elapsed = currentTime - info.SpawnTime;
                if (!info.IsDying && elapsed < FADE_IN_DURATION)
                    info.Alpha = (float)elapsed / FADE_IN_DURATION;
                else if (!info.IsDying)
                    info.Alpha = 1.0f;

                // Fade out
                if (info.IsDying)
                {
                    int deathElapsed = currentTime - info.DeathTime;
                    if (deathElapsed >= FADE_OUT_DURATION)
                    {
                        _activeBosses.RemoveAt(i);
                        continue;
                    }
                    info.Alpha = 1.0f - ((float)deathElapsed / FADE_OUT_DURATION);
                }

                AnimateHpBar(info, deltaTime);
            }
        }

        private void AnimateHpBar(BossDisplayInfo info, float deltaTime)
        {
            float hpSpeed = HP_ANIMATION_SPEED * deltaTime * 60f;
            float trailSpeed = HP_DAMAGE_TRAIL_SPEED * deltaTime * 60f;

            if (Math.Abs(info.DisplayedHpPercent - info.CurrentHpPercent) > 0.001f)
            {
                if (info.DisplayedHpPercent > info.CurrentHpPercent)
                {
                    info.DisplayedHpPercent -= hpSpeed;
                    if (info.DisplayedHpPercent < info.CurrentHpPercent)
                        info.DisplayedHpPercent = info.CurrentHpPercent;
                }
                else
                {
                    info.DisplayedHpPercent += hpSpeed * 2f;
                    if (info.DisplayedHpPercent > info.CurrentHpPercent)
                        info.DisplayedHpPercent = info.CurrentHpPercent;
                }
            }

            if (info.TrailHpPercent > info.DisplayedHpPercent)
            {
                info.TrailHpPercent -= trailSpeed;
                if (info.TrailHpPercent < info.DisplayedHpPercent)
                    info.TrailHpPercent = info.DisplayedHpPercent;
            }
            else
            {
                info.TrailHpPercent = info.DisplayedHpPercent;
            }
        }

        public void UpdateMousePosition(int x, int y)
        {
            _mousePosition = new Point(x, y);
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch)
        {
            if (!_isInitialized || _activeBosses.Count == 0)
                return;

            int screenWidth = spriteBatch.GraphicsDevice.Viewport.Width;
            int yOffset = BAR_Y_OFFSET;

            foreach (var info in _activeBosses)
            {
                if (info.Alpha <= 0)
                    continue;

                if (_hasWzTextures)
                    DrawWithWzTextures(spriteBatch, info, screenWidth, yOffset);
                else
                    DrawWithFallback(spriteBatch, info, screenWidth, yOffset);

                yOffset += _frameHeight + 5;
            }
        }

        private void DrawWithWzTextures(SpriteBatch spriteBatch, BossDisplayInfo info, int screenWidth, int yPos)
        {
            float alpha = info.Alpha;
            int x = BAR_MARGIN;

            // Calculate total bar width (screen width minus margins)
            int totalWidth = screenWidth - BAR_MARGIN * 2;

            // Get gauge textures for this boss's hpTagColor
            int colorId = Math.Clamp((int)info.HpTagColorId, 1, 7);
            if (!_gaugeTextures.TryGetValue(colorId, out var gaugeTexPair))
            {
                // Fallback to color 1 if specific color not found
                _gaugeTextures.TryGetValue(1, out gaugeTexPair);
            }

            Texture2D gaugeBg = gaugeTexPair?[0];
            Texture2D gaugeFill = gaugeTexPair?[1];

            // === Draw frame background ===

            // Left piece (contains boss icon slot) - 35x37
            if (_texBackgrnd != null)
            {
                spriteBatch.Draw(_texBackgrnd, new Vector2(x, yPos), Color.White * alpha);
            }

            // Frame dimensions
            int backgrndWidth = _texBackgrnd?.Width ?? 35;
            int backgrnd2Width = _texBackgrnd2?.Width ?? 4;
            int backgrnd4Width = _texBackgrnd4?.Width ?? 5;

            // Calculate positions for frame pieces
            int backgrnd2X = x + backgrndWidth;                          // Where backgrnd2 starts
            int backgrnd4X = x + totalWidth - backgrnd4Width;            // Where backgrnd4 starts
            int backgrnd3X = backgrnd2X + backgrnd2Width;                // Where backgrnd3 starts (after backgrnd2)
            int backgrnd3Width = backgrnd4X - backgrnd3X;                // Width of tiled center strip

            // Y position for gauge frame pieces - aligned to very top of frame
            int gaugeFrameY = yPos;

            // Transition piece after icon - backgrnd2 (4x17)
            if (_texBackgrnd2 != null)
            {
                spriteBatch.Draw(_texBackgrnd2, new Vector2(backgrnd2X, gaugeFrameY), Color.White * alpha);
            }

            // Right end cap - backgrnd4 (5x18)
            if (_texBackgrnd4 != null)
            {
                spriteBatch.Draw(_texBackgrnd4, new Vector2(backgrnd4X, gaugeFrameY), Color.White * alpha);
            }

            // Center tiled strip - backgrnd3 (1x17 tiled horizontally)
            if (_texBackgrnd3 != null && backgrnd3Width > 0)
            {
                spriteBatch.Draw(_texBackgrnd3,
                    new Rectangle(backgrnd3X, gaugeFrameY, backgrnd3Width, _texBackgrnd3.Height),
                    Color.White * alpha);
            }

            // === Draw HP gauge ===
            // The gauge fills the area covered by backgrnd3 (the tiled center strip)
            // Gauge is offset 2 pixels below the frame top

            int gaugeY = yPos + 3; // 3 pixels below frame top
            int gaugeX = backgrnd3X;           // Gauge starts where backgrnd3 starts (no left gap)
            int gaugeWidth = backgrnd3Width;   // Gauge fills the entire backgrnd3 area

            // Draw background gauge (full width, represents empty HP)
            if (gaugeBg != null && gaugeWidth > 0)
            {
                spriteBatch.Draw(gaugeBg,
                    new Rectangle(gaugeX, gaugeY, gaugeWidth, gaugeBg.Height),
                    Color.White * alpha);
            }

            // Draw damage trail (orange/red showing recent damage)
            if (info.TrailHpPercent > info.DisplayedHpPercent && gaugeBg != null)
            {
                int trailWidth = (int)(gaugeWidth * info.TrailHpPercent);
                int hpWidth = (int)(gaugeWidth * info.DisplayedHpPercent);

                // Draw trail in a slightly different color
                Color trailColor = new Color(255, 180, 80);
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(gaugeX + hpWidth, gaugeY, trailWidth - hpWidth, _gaugeHeight),
                    trailColor * alpha * 0.7f);
            }

            // Draw filled gauge (HP remaining)
            if (gaugeFill != null && gaugeWidth > 0)
            {
                int hpWidth = (int)(gaugeWidth * info.DisplayedHpPercent);
                if (hpWidth > 0)
                {
                    spriteBatch.Draw(gaugeFill,
                        new Rectangle(gaugeX, gaugeY, hpWidth, gaugeFill.Height),
                        Color.White * alpha);
                }
            }

            // === Draw boss icon ===

            int iconX = x + ICON_OFFSET_X;
            int iconY = yPos + ICON_OFFSET_Y;
            int iconSize = 25;

            info.IconBounds = new Rectangle(iconX, iconY, iconSize, iconSize);

            // Try to load and draw boss-specific icon
            var iconTexture = LoadBossIcon(info.MobId);
            if (iconTexture != null)
            {
                spriteBatch.Draw(iconTexture, new Vector2(iconX, iconY), Color.White * alpha);
            }

            // Draw boss name on hover
            if (info.IconBounds.Contains(_mousePosition))
            {
                DrawBossNameTooltip(spriteBatch, info, iconX, iconY + iconSize + 5, alpha);
            }

            // === Draw HP percentage text below icon ===
            DrawHpText(spriteBatch, info, iconX, iconY + iconSize, iconSize, alpha);
        }

        private void DrawWithFallback(SpriteBatch spriteBatch, BossDisplayInfo info, int screenWidth, int yPos)
        {
            float alpha = info.Alpha;
            int frameHeight = 28;
            int x = BAR_MARGIN;
            int totalWidth = screenWidth - BAR_MARGIN * 2;

            // Get HP bar color from hpTagColor
            Color hpColor = GetHpTagColor(info.HpTagColorId);
            Color bgColor = GetHpTagBgColor(info.HpTagBgColorId);

            // === Draw frame ===

            // Outer border
            Color borderColor = new Color(100, 80, 40);
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x - 2, yPos - 2, totalWidth + 4, frameHeight + 4),
                borderColor * alpha);

            // Frame background
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x, yPos, totalWidth, frameHeight),
                new Color(30, 25, 20) * alpha);

            // Icon area (left side)
            int iconAreaWidth = 35;
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x, yPos, iconAreaWidth, frameHeight),
                new Color(50, 40, 30) * alpha);

            // Icon border
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x + 4, yPos + 4, 27, 20),
                new Color(80, 60, 40) * alpha);

            // Icon placeholder
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x + 5, yPos + 5, 25, 18),
                new Color(40, 30, 25) * alpha);

            info.IconBounds = new Rectangle(x + 5, yPos + 5, 25, 18);

            // === Draw gauge area ===

            int gaugeX = x + iconAreaWidth + 5;
            int gaugeWidth = totalWidth - iconAreaWidth - 15;
            int gaugeY = yPos + 6;
            int gaugeHeight = 16;

            // Gauge background
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(gaugeX, gaugeY, gaugeWidth, gaugeHeight),
                bgColor * alpha);

            // Damage trail
            if (info.TrailHpPercent > info.DisplayedHpPercent)
            {
                int trailWidth = (int)(gaugeWidth * info.TrailHpPercent);
                int hpWidth = (int)(gaugeWidth * info.DisplayedHpPercent);
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(gaugeX + hpWidth, gaugeY, trailWidth - hpWidth, gaugeHeight),
                    new Color(255, 180, 80) * alpha * 0.7f);
            }

            // HP bar
            int currentHpWidth = (int)(gaugeWidth * info.DisplayedHpPercent);
            if (currentHpWidth > 0)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(gaugeX, gaugeY, currentHpWidth, gaugeHeight),
                    hpColor * alpha);

                // Highlight
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(gaugeX, gaugeY, currentHpWidth, 3),
                    Color.Lerp(hpColor, Color.White, 0.3f) * alpha);
            }

            // Draw HP text below icon
            int fallbackIconX = x + 5;
            int fallbackIconBottomY = yPos + 5 + 18;  // Icon at yPos+5 with height 18
            int fallbackIconWidth = 25;
            DrawHpText(spriteBatch, info, fallbackIconX, fallbackIconBottomY, fallbackIconWidth, alpha);

            // Draw tooltip on hover
            if (info.IconBounds.Contains(_mousePosition))
            {
                DrawBossNameTooltip(spriteBatch, info, x + 5, yPos + frameHeight + 5, alpha);
            }
        }

        private void DrawHpText(SpriteBatch spriteBatch, BossDisplayInfo info, int iconX, int iconBottomY, int iconWidth, float alpha)
        {
            if (_font == null) return;

            // Display HP percentage without "%" symbol
            string hpText = $"{(int)(info.DisplayedHpPercent * 100)}";

            // Use smaller font scale
            float fontScale = 0.6f;
            Vector2 textSize = _font.MeasureString(hpText) * fontScale;

            // Center text horizontally under the icon
            Vector2 textPos = new Vector2(
                iconX + (iconWidth - textSize.X) / 2,
                iconBottomY + 1  // Small gap below icon
            );

            // Shadow
            spriteBatch.DrawString(_font, hpText, textPos + Vector2.One, Color.Black * alpha, 0f, Vector2.Zero, fontScale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            // Text
            spriteBatch.DrawString(_font, hpText, textPos, Color.White * alpha, 0f, Vector2.Zero, fontScale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
        }

        private void DrawBossNameTooltip(SpriteBatch spriteBatch, BossDisplayInfo info, int x, int y, float alpha)
        {
            if (_font == null || string.IsNullOrEmpty(info.BossName))
                return;

            string nameText = info.Level > 0 ? $"Lv. {info.Level}  {info.BossName}" : info.BossName;
            Vector2 nameSize = _font.MeasureString(nameText);

            int padding = 4;

            // Background
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x - padding, y - padding, (int)nameSize.X + padding * 2, (int)nameSize.Y + padding * 2),
                new Color(0, 0, 0, 200) * alpha);

            // Border
            Color borderColor = GetHpTagColor(info.HpTagColorId);
            spriteBatch.Draw(_pixelTexture,
                new Rectangle(x - padding - 1, y - padding - 1, (int)nameSize.X + padding * 2 + 2, 1),
                borderColor * alpha);

            // Text
            spriteBatch.DrawString(_font, nameText, new Vector2(x + 1, y + 1), Color.Black * alpha);
            spriteBatch.DrawString(_font, nameText, new Vector2(x, y), new Color(255, 220, 100) * alpha);
        }

        private Color GetHpTagColor(short hpTagColor)
        {
            return hpTagColor switch
            {
                1 => new Color(255, 50, 50),     // Red
                2 => new Color(255, 128, 0),     // Orange
                3 => new Color(255, 255, 0),     // Yellow
                4 => new Color(50, 255, 50),     // Green
                5 => new Color(50, 255, 255),    // Cyan
                6 => new Color(50, 50, 255),     // Blue
                7 => new Color(180, 50, 255),    // Purple
                _ => new Color(255, 100, 50)     // Default
            };
        }

        private Color GetHpTagBgColor(short hpTagBgColor)
        {
            return hpTagBgColor switch
            {
                1 => new Color(80, 20, 20),      // Dark Red
                2 => new Color(80, 40, 0),       // Dark Orange
                3 => new Color(80, 80, 0),       // Dark Yellow
                4 => new Color(20, 80, 20),      // Dark Green
                5 => new Color(20, 80, 80),      // Dark Cyan
                6 => new Color(20, 20, 80),      // Dark Blue
                7 => new Color(60, 20, 80),      // Dark Purple
                _ => new Color(40, 20, 20)       // Default
            };
        }
        #endregion

        #region Helpers
        public bool HasActiveBossBars => _activeBosses.Count > 0;
        public int ActiveBossCount => _activeBosses.Count;

        public bool HasBossHPBar(int poolId)
        {
            return _activeBosses.Exists(b => b.Boss?.PoolId == poolId);
        }
        #endregion

        #region Cleanup
        public void Clear()
        {
            _activeBosses.Clear();
        }

        public void Dispose()
        {
            Clear();

            _texBackgrnd?.Dispose();
            _texBackgrnd2?.Dispose();
            _texBackgrnd3?.Dispose();
            _texBackgrnd4?.Dispose();

            foreach (var pair in _gaugeTextures)
            {
                pair.Value[0]?.Dispose();
                pair.Value[1]?.Dispose();
            }
            _gaugeTextures.Clear();

            foreach (var icon in _bossIcons.Values)
                icon?.Dispose();
            _bossIcons.Clear();

            _pixelTexture?.Dispose();
        }
        #endregion
    }
}
