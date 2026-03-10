using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.UI {

    /// <summary>
    /// Character stats data for display on the status bar.
    /// Positions based on analysis of MapleStory client CUIStatusBar::SetNumberValue and CUIStatusBar::SetStatusValue functions.
    /// </summary>
    public class CharacterStatsData {
        public int HP { get; set; } = 100;
        public int MaxHP { get; set; } = 100;
        public int MP { get; set; } = 100;
        public int MaxMP { get; set; } = 100;
        public long EXP { get; set; } = 0;
        public long MaxEXP { get; set; } = 100;
        public int Level { get; set; } = 1;
        public string Name { get; set; } = "Player";
        public string Job { get; set; } = "Beginner";
    }

    public class StatusBarBuffRenderData
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string Description { get; set; }
        public string IconKey { get; set; } = "united/buff";
        public Texture2D IconTexture { get; set; }
        public int RemainingMs { get; set; }
        public int DurationMs { get; set; }
    }

    public class StatusBarPreparedSkillRenderData
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string SkinKey { get; set; } = "KeyDownBar";
        public int RemainingMs { get; set; }
        public int DurationMs { get; set; }
        public float Progress { get; set; }
    }

    public class StatusBarCooldownRenderData
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string Description { get; set; }
        public Texture2D IconTexture { get; set; }
        public int RemainingMs { get; set; }
        public int DurationMs { get; set; }
    }

    public class StatusBarKeyDownBarTextures
    {
        public Texture2D Bar { get; set; }
        public Texture2D Gauge { get; set; }
        public Texture2D Graduation { get; set; }
    }

    public class StatusBarWarningAnimation
    {
        public Texture2D[] Frames { get; set; } = Array.Empty<Texture2D>();
        public int FrameDelayMs { get; set; } = 120;
        public int FlashDurationMs { get; set; } = 500;
    }

    public class StatusBarUI : BaseDXDrawableItem, IUIObjectEvents {
        private readonly List<UIObject> uiButtons = new List<UIObject>();

        // Character stats display - positions based on IDA Pro analysis of client CUIStatusBar
        // HP text at (163, 4), MP text at (332, 4), EXP text at (332, 20)
        // Level at (45, 552), Job at (75, 549), Name at (75, 561) (absolute screen positions)
        private SpriteFont _font;
        private Func<CharacterStatsData> _getCharacterStats;
        private Func<int, IReadOnlyList<StatusBarBuffRenderData>> _getBuffStatus;
        private Func<int, IReadOnlyList<StatusBarCooldownRenderData>> _getCooldownStatus;
        private Func<int, StatusBarPreparedSkillRenderData> _getPreparedSkillStatus;
        private Texture2D _pixelTexture;

        // Gauge textures loaded from UI.wz/StatusBar2.img/mainBar/gauge/hp/0, mp/0, exp/0
        private Texture2D _hpGaugeTexture;
        private Texture2D _mpGaugeTexture;
        private Texture2D _expGaugeTexture;

        // Bitmap font textures for MapleStory-style numbers
        // Loaded from UI.wz/Basic.img/ItemNo/ (digits 0-9, slash, percent, etc.)
        private Texture2D[] _digitTextures;       // 0-9
        private Point[] _digitOrigins;            // Origins for digits 0-9
        private Texture2D[] _levelDigitTextures;  // 0-9 for StatusBar2 mainBar/lvNumber
        private Point[] _levelDigitOrigins;       // Origins for level digits
        private Texture2D _slashTexture;          // For HP/MP display like "100/100"
        private Point _slashOrigin;
        private Texture2D _percentTexture;        // For EXP percentage
        private Point _percentOrigin;
        private Texture2D _bracketLeftTexture;    // [
        private Point _bracketLeftOrigin;
        private Texture2D _bracketRightTexture;   // ]
        private Point _bracketRightOrigin;
        private Texture2D _dotTexture;            // For decimal point
        private Point _dotOrigin;
        private bool _useBitmapFont = false;      // Whether to use bitmap font or SpriteFont
        private bool _useLevelBitmapFont = false; // Whether to use lvNumber textures for the level label
        private readonly Dictionary<string, Texture2D> _buffIconTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Rectangle> _buffIconHitboxes = new Dictionary<int, Rectangle>();
        private readonly Dictionary<int, StatusBarBuffRenderData> _buffTooltipEntries = new Dictionary<int, StatusBarBuffRenderData>();
        private readonly Dictionary<int, Rectangle> _cooldownIconHitboxes = new Dictionary<int, Rectangle>();
        private readonly Dictionary<int, StatusBarCooldownRenderData> _cooldownTooltipEntries = new Dictionary<int, StatusBarCooldownRenderData>();
        private readonly Dictionary<string, StatusBarKeyDownBarTextures> _keyDownBarTextures = new Dictionary<string, StatusBarKeyDownBarTextures>(StringComparer.OrdinalIgnoreCase);
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private StatusBarWarningAnimation _hpWarningAnimation = new StatusBarWarningAnimation();
        private StatusBarWarningAnimation _mpWarningAnimation = new StatusBarWarningAnimation();
        private int _lowHpWarningThresholdPercent = 20;
        private int _lowMpWarningThresholdPercent = 20;
        private int _pastHpWarningValue;
        private int _pastMpWarningValue;
        private bool _hpWarningInitialized;
        private bool _mpWarningInitialized;
        private int _hpFlashStartTime = int.MinValue;
        private int _hpFlashEndTime = int.MinValue;
        private int _mpFlashStartTime = int.MinValue;
        private int _mpFlashEndTime = int.MinValue;
        private ButtonState _previousRightButtonState = ButtonState.Released;

        // Text positions relative to status bar (from IDA Pro analysis)
        // The status bar is composed of: lvBacktrnd (left ~64px) + gaugeBackgrd (center) + buttons
        // From SetNumberValue: HP(163,4), MP(332,4), EXP(332,20) - relative to gauge layer
        // From SetStatusValue: Level(45,552), Job(75,549), Name(75,561) - in 800x578 canvas
        // Converting to relative positions within status bar:
        //   Level: (45, 10), Job: (75, 7), Name: (75, 19)

        // lvBacktrnd area (left side - character info)
        private static readonly Vector2 LEVEL_TEXT_POS = new Vector2(45, 10);  // Level number (offset adjusted for digit count)
        private static readonly Vector2 JOB_TEXT_POS = new Vector2(75, 7);     // Job name
        private static readonly Vector2 NAME_TEXT_POS = new Vector2(75, 19);   // Character name

        // gaugeBackgrd area - HP/MP/EXP text and gauges
        // lvBacktrnd is ~64px wide, so gauge area starts at X~64
        // HP/MP/EXP positions from SetNumberValue are relative to gauge layer
        private static readonly Vector2 HP_TEXT_POS = new Vector2(163, 4);     // HP text [HP\MaxHP]
        private static readonly Vector2 MP_TEXT_POS = new Vector2(332, 4);     // MP text [MP\MaxMP]
        private static readonly Vector2 EXP_TEXT_POS = new Vector2(332, 20);   // EXP text

        // Gauge bar positions and sizes
        // From IDA Pro CUIStatusBar::CGauge::Create:
        // HP Gauge: X=28 relative to gaugeBackgrd, Y=2, Length=138
        // MP Gauge: X=197 relative to gaugeBackgrd, Y=2, Length=138
        // EXP Gauge: X=28 relative to gaugeBackgrd, Y=18, Length=308
        // gaugeBackgrd starts at ~64px, so absolute X positions:
        // HP: 64+28=92, MP: 64+197=261, EXP: 64+28=92
        private static readonly Rectangle HP_GAUGE_RECT = new Rectangle(92, 3, 138, 12);    // HP gauge (red)
        private static readonly Rectangle MP_GAUGE_RECT = new Rectangle(261, 3, 138, 12);   // MP gauge (blue)
        private static readonly Rectangle EXP_GAUGE_RECT = new Rectangle(92, 20, 308, 10);  // EXP gauge (yellow)

        // Gauge colors matching original MapleStory client
        private static readonly Color HP_GAUGE_COLOR = new Color(255, 50, 50);       // Red for HP
        private static readonly Color HP_GAUGE_BG_COLOR = new Color(80, 20, 20);     // Dark red background
        private static readonly Color MP_GAUGE_COLOR = new Color(50, 100, 255);      // Blue for MP
        private static readonly Color MP_GAUGE_BG_COLOR = new Color(20, 40, 80);     // Dark blue background
        private static readonly Color EXP_GAUGE_COLOR = new Color(255, 255, 50);     // Yellow for EXP
        private static readonly Color EXP_GAUGE_BG_COLOR = new Color(60, 60, 20);    // Dark yellow background
        // These offsets are derived from the composed StatusBar2 mainBar frame:
        // background origin Y=84, lv/gauge origin Y=33, which keeps the overlay
        // content anchored 51px above the frame bottom instead of to the viewport.
        private static readonly Point STATUS_BAR_LEFT_BASE_OFFSET = new Point(0, 43);
        private static readonly Point STATUS_BAR_GAUGE_BASE_OFFSET = new Point(155, 52);
        private const int BUFF_ICON_SIZE = 32;
        private const int BUFF_ICON_SPACING = 2;
        private const int BUFF_TRAY_COLUMNS = 10;
        private const int BUFF_TRAY_ROWS = 2;
        private const int BUFF_TRAY_TOP_MARGIN = 8;
        private const int BUFF_TRAY_RIGHT_MARGIN = 8;
        private const int COOLDOWN_TRAY_COLUMNS = 8;
        private const int COOLDOWN_TRAY_TOP_MARGIN = BUFF_TRAY_TOP_MARGIN + (BUFF_TRAY_ROWS * (BUFF_ICON_SIZE + BUFF_ICON_SPACING)) + 4;
        private const int COOLDOWN_TRAY_RIGHT_MARGIN = 8;
        private const int TOOLTIP_FALLBACK_WIDTH = 320;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int TOOLTIP_OFFSET_X = 12;
        private const int TOOLTIP_OFFSET_Y = -4;
        private const int PREPARED_SKILL_LABEL_GAP = 6;
        private static readonly Vector2 KEYDOWN_BAR_POS = new Vector2(214, -22);
        private static readonly Point KEYDOWN_BAR_GAUGE_OFFSET = new Point(2, 2);

        /// <summary>
        /// Constructor for the status bar window
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="obj_Ui_BtCashShop">The BtCashShop button</param>
        /// <param name="obj_Ui_BtMTS">The MTS button</param>
        /// <param name="obj_Ui_BtMenu"></param>
        /// <param name="obj_Ui_BtSystem">The System button</param>
        /// <param name="obj_Ui_BtChannel">The Channel button</param>
        public StatusBarUI(IDXObject frame, UIObject obj_Ui_BtCashShop, UIObject obj_Ui_BtMTS, UIObject obj_Ui_BtMenu, UIObject obj_Ui_BtSystem, UIObject obj_Ui_BtChannel, Point setPosition,
            List<UIObject> otherUI)
            : base(frame, false) {

            // Add buttons with null checks (pre-BigBang may have fewer buttons)
            if (obj_Ui_BtCashShop != null)
                uiButtons.Add(obj_Ui_BtCashShop);
            if (obj_Ui_BtMTS != null)
                uiButtons.Add(obj_Ui_BtMTS);
            if (obj_Ui_BtMenu != null)
                uiButtons.Add(obj_Ui_BtMenu);
            if (obj_Ui_BtSystem != null)
                uiButtons.Add(obj_Ui_BtSystem);
            if (obj_Ui_BtChannel != null)
                uiButtons.Add(obj_Ui_BtChannel);

            uiButtons.AddRange(otherUI);

            this.Position = setPosition;
        }

        /// <summary>
        /// Set the font and character stats callback for rendering character information
        /// </summary>
        /// <param name="font">SpriteFont to use for text rendering</param>
        /// <param name="getCharacterStats">Callback to get current character stats</param>
        public void SetCharacterStatsProvider(SpriteFont font, Func<CharacterStatsData> getCharacterStats) {
            _font = font;
            _getCharacterStats = getCharacterStats;
        }

        public void SetBuffStatusProvider(Func<int, IReadOnlyList<StatusBarBuffRenderData>> getBuffStatus)
        {
            _getBuffStatus = getBuffStatus;
        }

        public void SetCooldownStatusProvider(Func<int, IReadOnlyList<StatusBarCooldownRenderData>> getCooldownStatus)
        {
            _getCooldownStatus = getCooldownStatus;
        }

        public void SetPreparedSkillProvider(Func<int, StatusBarPreparedSkillRenderData> getPreparedSkillStatus)
        {
            _getPreparedSkillStatus = getPreparedSkillStatus;
        }

        public Action<int> BuffCancelRequested { get; set; }

        /// <summary>
        /// Set the pixel texture for drawing gauge bars
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for creating textures</param>
        public void SetPixelTexture(GraphicsDevice graphicsDevice) {
            if (_pixelTexture == null && graphicsDevice != null) {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
        }

        /// <summary>
        /// Set the gauge textures loaded from UI.wz/StatusBar2.img/mainBar/gauge/hp/0, mp/0, exp/0
        /// These textures are used to render the HP, MP, and EXP bars with proper graphics
        /// </summary>
        public void SetGaugeTextures(Texture2D hpGauge, Texture2D mpGauge, Texture2D expGauge) {
            _hpGaugeTexture = hpGauge;
            _mpGaugeTexture = mpGauge;
            _expGaugeTexture = expGauge;
        }

        public void SetBuffIconTextures(Dictionary<string, Texture2D> buffIconTextures)
        {
            _buffIconTextures.Clear();
            if (buffIconTextures == null)
            {
                return;
            }

            foreach (KeyValuePair<string, Texture2D> iconEntry in buffIconTextures)
            {
                if (!string.IsNullOrWhiteSpace(iconEntry.Key) && iconEntry.Value != null)
                {
                    _buffIconTextures[iconEntry.Key] = iconEntry.Value;
                }
            }
        }

        public void SetKeyDownBarTextures(Dictionary<string, StatusBarKeyDownBarTextures> keyDownBarTextures)
        {
            _keyDownBarTextures.Clear();
            if (keyDownBarTextures == null)
            {
                return;
            }

            foreach (KeyValuePair<string, StatusBarKeyDownBarTextures> keyDownBarEntry in keyDownBarTextures)
            {
                if (!string.IsNullOrWhiteSpace(keyDownBarEntry.Key) && keyDownBarEntry.Value != null)
                {
                    _keyDownBarTextures[keyDownBarEntry.Key] = keyDownBarEntry.Value;
                }
            }
        }

        public void SetTooltipTextures(Texture2D[] tooltipFrames)
        {
            if (tooltipFrames == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_tooltipFrames.Length, tooltipFrames.Length); i++)
            {
                _tooltipFrames[i] = tooltipFrames[i];
            }
        }

        public void SetWarningAnimations(StatusBarWarningAnimation hpWarningAnimation, StatusBarWarningAnimation mpWarningAnimation)
        {
            _hpWarningAnimation = hpWarningAnimation ?? new StatusBarWarningAnimation();
            _mpWarningAnimation = mpWarningAnimation ?? new StatusBarWarningAnimation();
        }

        public void SetLowResourceWarningThresholds(int hpThresholdPercent, int mpThresholdPercent)
        {
            _lowHpWarningThresholdPercent = Math.Clamp(hpThresholdPercent, 0, 100);
            _lowMpWarningThresholdPercent = Math.Clamp(mpThresholdPercent, 0, 100);
            _hpWarningInitialized = false;
            _mpWarningInitialized = false;
        }

        /// <summary>
        /// Set the digit textures for MapleStory-style bitmap font rendering.
        /// Loaded from UI.wz/Basic.img/ItemNo/ or UI.wz/StatusBar2.img/mainBar/number/
        /// </summary>
        /// <param name="digitTextures">Array of 10 textures for digits 0-9</param>
        /// <param name="digitOrigins">Array of 10 origin points for digits 0-9</param>
        /// <param name="slashTexture">Texture for slash character</param>
        /// <param name="slashOrigin">Origin for slash</param>
        /// <param name="percentTexture">Texture for percent symbol</param>
        /// <param name="percentOrigin">Origin for percent</param>
        /// <param name="bracketLeft">Texture for left bracket [</param>
        /// <param name="bracketLeftOrigin">Origin for left bracket</param>
        /// <param name="bracketRight">Texture for right bracket ]</param>
        /// <param name="bracketRightOrigin">Origin for right bracket</param>
        /// <param name="dotTexture">Texture for decimal point</param>
        /// <param name="dotOrigin">Origin for dot</param>
        public void SetDigitTextures(Texture2D[] digitTextures, Point[] digitOrigins,
            Texture2D slashTexture, Point slashOrigin,
            Texture2D percentTexture, Point percentOrigin,
            Texture2D bracketLeft, Point bracketLeftOrigin,
            Texture2D bracketRight, Point bracketRightOrigin,
            Texture2D dotTexture, Point dotOrigin) {
            if (digitTextures != null && digitTextures.Length >= 10) {
                _digitTextures = digitTextures;
                _digitOrigins = digitOrigins ?? new Point[10];
                _slashTexture = slashTexture;
                _slashOrigin = slashOrigin;
                _percentTexture = percentTexture;
                _percentOrigin = percentOrigin;
                _bracketLeftTexture = bracketLeft;
                _bracketLeftOrigin = bracketLeftOrigin;
                _bracketRightTexture = bracketRight;
                _bracketRightOrigin = bracketRightOrigin;
                _dotTexture = dotTexture;
                _dotOrigin = dotOrigin;
                _useBitmapFont = true;
            }
        }

        public void SetLevelDigitTextures(Texture2D[] digitTextures, Point[] digitOrigins)
        {
            if (digitTextures != null && digitTextures.Length >= 10)
            {
                _levelDigitTextures = digitTextures;
                _levelDigitOrigins = digitOrigins ?? new Point[10];
                _useLevelBitmapFont = true;
            }
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        /// <param name="baseClickableUIObject"></param>
        public void InitializeButtons() {
            //objUIBtMax.SetButtonState(UIObjectState.Disabled); // start maximised
        }

        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="centerX"></param>
        /// <param name="centerY"></param>
        /// <param name="drawReflectionInfo"></param>
        /// <param name="renderParameters"></param>
        /// <param name="TickCount"></param>
        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount) {
            // control minimap render UI position via
            //  Position.X, Position.Y

            // Draw the main frame
            // Pass centerX=0, centerY=0 so the frame draws at its DXObject position (0, Position.Y)
            // without being offset by the minimap position
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                   this.Position.X, this.Position.Y, 0, 0,
                   drawReflectionInfo,
                   renderParameters,
                   TickCount);

            // draw other buttons
            foreach (UIObject uiBtn in uiButtons) {
                BaseDXDrawableItem buttonToDraw = uiBtn.GetBaseDXDrawableItemByState();

                // Position drawn is relative to this UI
                int drawRelativeX = -(this.Position.X) - uiBtn.X; // Left to right
                int drawRelativeY = -(this.Position.Y) - uiBtn.Y; // Top to bottom

                buttonToDraw.Draw(sprite, skeletonMeshRenderer,
                    gameTime,
                    drawRelativeX,
                    drawRelativeY,
                    0, 0,
                    null,
                    renderParameters,
                    TickCount);
            }

            // Draw character stats (HP, MP, EXP, Level, Name)
            // Use renderParameters to get screen height and calculate position from bottom
            DrawCharacterStats(sprite, renderParameters, TickCount);
            DrawHoveredBuffTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
            DrawHoveredCooldownTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        /// <summary>
        /// Draw character stats (HP, MP, EXP, Level, Name) on the status bar.
        /// Positions derived from IDA Pro analysis of CUIStatusBar::SetNumberValue and CUIStatusBar::SetStatusValue.
        /// </summary>
        private void DrawCharacterStats(SpriteBatch sprite, RenderParameters renderParameters, int currentTime) {
            if (_getCharacterStats == null)
                return;

            CharacterStatsData stats = _getCharacterStats();
            if (stats == null)
                return;

            // Anchor overlay content to the rendered status-bar frame so it follows
            // the UI's actual placement instead of re-deriving from viewport height.
            Vector2 basePosLeft = new Vector2(
                this.Position.X + STATUS_BAR_LEFT_BASE_OFFSET.X,
                this.Position.Y + STATUS_BAR_LEFT_BASE_OFFSET.Y);
            Vector2 basePosGauge = new Vector2(
                this.Position.X + STATUS_BAR_GAUGE_BASE_OFFSET.X,
                this.Position.Y + STATUS_BAR_GAUGE_BASE_OFFSET.Y);

            UpdateWarningFlashState(stats, currentTime);

            // Draw gauge bars first (under the text)
            DrawGaugeBars(sprite, stats, basePosGauge, currentTime);
            DrawBuffTray(sprite, renderParameters, currentTime);
            DrawCooldownTray(sprite, renderParameters, currentTime);
            DrawPreparedSkillBar(sprite, basePosGauge, currentTime);

            // Skip text rendering if no font
            if (_font == null)
                return;

            // Draw character info section (left side) - use basePosLeft
            // Level number - format from client: level number with image digits
            string levelText = stats.Level.ToString();
            // Adjust X based on number of digits (from IDA: offset 0 for 1-9, 6 for 10-99, 12 for 100+)
            int levelOffset = stats.Level <= 9 ? 0 : (stats.Level <= 99 ? 6 : 12);
            Vector2 levelPos = basePosLeft + new Vector2(LEVEL_TEXT_POS.X - levelOffset, LEVEL_TEXT_POS.Y);
            if (_useLevelBitmapFont)
            {
                DrawDigitBitmapString(sprite, levelText, levelPos, _levelDigitTextures, _levelDigitOrigins, 1.0f);
            }
            else
            {
                DrawTextWithShadow(sprite, levelText, levelPos, Color.White, Color.Black);
            }

            // Job name
            Vector2 jobPos = basePosLeft + JOB_TEXT_POS;
            DrawTextWithShadow(sprite, stats.Job, jobPos, Color.Yellow, Color.Black, 0.8f);

            // Character name - drawn with shadow effect (from IDA: multiple positions for shadow)
            Vector2 namePos = basePosLeft + NAME_TEXT_POS;
            DrawDiagonalShadowText(sprite, stats.Name, namePos, Color.White, Color.Black, 0.8f);

            // Draw gauge text section (HP/MP/EXP area) - use basePosGauge
            // HP text - format from client: [HP/MaxHP]
            string hpText = $"[{stats.HP}/{stats.MaxHP}]";
            Vector2 hpPos = basePosGauge + HP_TEXT_POS;
            if (_useBitmapFont) {
                DrawBitmapString(sprite, hpText, hpPos, 1.0f);
            } else {
                DrawTextWithShadow(sprite, hpText, hpPos, Color.White, Color.Black, 0.7f);
            }

            // MP text - format from client: [MP/MaxMP]
            string mpText = $"[{stats.MP}/{stats.MaxMP}]";
            Vector2 mpPos = basePosGauge + MP_TEXT_POS;
            if (_useBitmapFont) {
                DrawBitmapString(sprite, mpText, mpPos, 1.0f);
            } else {
                DrawTextWithShadow(sprite, mpText, mpPos, Color.White, Color.Black, 0.7f);
            }

            // EXP text - format from client: [percentage%]
            double expPercent = stats.MaxEXP > 0 ? (double)stats.EXP / stats.MaxEXP * 100.0 : 0.0;
            // Cap at 99.99% like the client does
            if (expPercent > 99.99) expPercent = 99.99;
            string expText = $"{Math.Max(0L, stats.EXP)}[{expPercent:F2}%]";
            Vector2 expPos = basePosGauge + EXP_TEXT_POS;
            if (_useBitmapFont) {
                DrawBitmapString(sprite, expText, expPos, 1.0f);
            } else {
                DrawTextWithShadow(sprite, expText, expPos, Color.Yellow, Color.Black, 0.7f);
            }
        }

        /// <summary>
        /// Draw HP, MP, and EXP gauge bars.
        /// Gauge positions derived from IDA Pro analysis of CUIStatusBar::CGauge::Create.
        /// Uses actual gauge textures from UI.wz when available, falls back to colored rectangles.
        /// </summary>
        private void DrawGaugeBars(SpriteBatch sprite, CharacterStatsData stats, Vector2 basePos, int currentTime) {
            // Calculate fill ratios
            float hpRatio = stats.MaxHP > 0 ? (float)stats.HP / stats.MaxHP : 0f;
            float mpRatio = stats.MaxMP > 0 ? (float)stats.MP / stats.MaxMP : 0f;
            float expRatio = stats.MaxEXP > 0 ? (float)stats.EXP / stats.MaxEXP : 0f;

            // Clamp ratios to 0-1 range
            hpRatio = Math.Clamp(hpRatio, 0f, 1f);
            mpRatio = Math.Clamp(mpRatio, 0f, 1f);
            expRatio = Math.Clamp(expRatio, 0f, 1f);

            // Draw HP gauge - use texture if available, use predefined gauge rect for positioning
            if (_hpGaugeTexture != null) {
                DrawTexturedGauge(sprite, basePos, HP_GAUGE_RECT, hpRatio, _hpGaugeTexture);
            } else if (_pixelTexture != null) {
                DrawGaugeBar(sprite, basePos, HP_GAUGE_RECT, hpRatio, HP_GAUGE_COLOR, HP_GAUGE_BG_COLOR);
            }
            DrawWarningAnimation(sprite, basePos, HP_GAUGE_RECT, _hpWarningAnimation, currentTime, _hpFlashStartTime, _hpFlashEndTime, new Color(255, 88, 88, 180));

            // Draw MP gauge
            if (_mpGaugeTexture != null) {
                DrawTexturedGauge(sprite, basePos, MP_GAUGE_RECT, mpRatio, _mpGaugeTexture);
            } else if (_pixelTexture != null) {
                DrawGaugeBar(sprite, basePos, MP_GAUGE_RECT, mpRatio, MP_GAUGE_COLOR, MP_GAUGE_BG_COLOR);
            }
            DrawWarningAnimation(sprite, basePos, MP_GAUGE_RECT, _mpWarningAnimation, currentTime, _mpFlashStartTime, _mpFlashEndTime, new Color(128, 180, 255, 180));

            // Draw EXP gauge
            if (_expGaugeTexture != null) {
                DrawTexturedGauge(sprite, basePos, EXP_GAUGE_RECT, expRatio, _expGaugeTexture);
            } else if (_pixelTexture != null) {
                DrawGaugeBar(sprite, basePos, EXP_GAUGE_RECT, expRatio, EXP_GAUGE_COLOR, EXP_GAUGE_BG_COLOR);
            }
        }

        private void UpdateWarningFlashState(CharacterStatsData stats, int currentTime)
        {
            UpdateWarningFlash(
                ref _hpWarningInitialized,
                ref _pastHpWarningValue,
                stats.HP,
                stats.MaxHP,
                _lowHpWarningThresholdPercent,
                currentTime,
                _hpWarningAnimation,
                ref _hpFlashStartTime,
                ref _hpFlashEndTime);

            UpdateWarningFlash(
                ref _mpWarningInitialized,
                ref _pastMpWarningValue,
                stats.MP,
                stats.MaxMP,
                _lowMpWarningThresholdPercent,
                currentTime,
                _mpWarningAnimation,
                ref _mpFlashStartTime,
                ref _mpFlashEndTime);
        }

        private void UpdateWarningFlash(
            ref bool initialized,
            ref int pastValue,
            int currentValue,
            int maxValue,
            int thresholdPercent,
            int currentTime,
            StatusBarWarningAnimation animation,
            ref int flashStartTime,
            ref int flashEndTime)
        {
            if (maxValue <= 0)
            {
                initialized = false;
                pastValue = 0;
                flashStartTime = int.MinValue;
                flashEndTime = int.MinValue;
                return;
            }

            int safeCurrentValue = Math.Clamp(currentValue, 0, maxValue);
            int safeThresholdPercent = Math.Clamp(thresholdPercent, 0, 100);
            int thresholdValue = maxValue * safeThresholdPercent / 100;

            if (!initialized)
            {
                pastValue = thresholdValue;
                initialized = true;
            }

            if (safeThresholdPercent > 0 && safeCurrentValue * 100 / maxValue < safeThresholdPercent)
            {
                if (pastValue > safeCurrentValue)
                {
                    flashStartTime = currentTime;
                    flashEndTime = currentTime + Math.Max(1, animation?.FlashDurationMs ?? 500);
                }

                pastValue = safeCurrentValue;
                return;
            }

            pastValue = thresholdValue;
        }

        private void DrawWarningAnimation(
            SpriteBatch sprite,
            Vector2 basePos,
            Rectangle gaugeRect,
            StatusBarWarningAnimation animation,
            int currentTime,
            int flashStartTime,
            int flashEndTime,
            Color fallbackColor)
        {
            if (currentTime >= flashEndTime)
            {
                return;
            }

            int elapsedMs = Math.Max(0, currentTime - flashStartTime);
            Texture2D[] frames = animation?.Frames ?? Array.Empty<Texture2D>();
            if (frames.Length > 0)
            {
                int frameDelayMs = Math.Max(1, animation.FrameDelayMs);
                int frameIndex = (elapsedMs / frameDelayMs) % frames.Length;
                Texture2D frame = frames[frameIndex];
                if (frame != null)
                {
                    sprite.Draw(frame, new Rectangle(
                        (int)basePos.X + gaugeRect.X,
                        (int)basePos.Y + gaugeRect.Y,
                        gaugeRect.Width,
                        gaugeRect.Height), Color.White);
                    return;
                }
            }

            if (_pixelTexture == null)
            {
                return;
            }

            int pulsePhase = (elapsedMs / 120) % 2;
            byte alpha = pulsePhase == 0 ? (byte)160 : (byte)88;
            sprite.Draw(
                _pixelTexture,
                new Rectangle(
                    (int)basePos.X + gaugeRect.X,
                    (int)basePos.Y + gaugeRect.Y,
                    gaugeRect.Width,
                    gaugeRect.Height),
                new Color(fallbackColor.R, fallbackColor.G, fallbackColor.B, alpha));
        }

        private void DrawBuffTray(SpriteBatch sprite, RenderParameters renderParameters, int currentTime)
        {
            if (_getBuffStatus == null)
            {
                return;
            }

            IReadOnlyList<StatusBarBuffRenderData> buffEntries = _getBuffStatus(currentTime);
            _buffIconHitboxes.Clear();
            _buffTooltipEntries.Clear();
            if (buffEntries == null || buffEntries.Count == 0)
            {
                return;
            }

            int maxEntries = BUFF_TRAY_COLUMNS * BUFF_TRAY_ROWS;
            int visibleCount = Math.Min(buffEntries.Count, maxEntries);

            for (int i = 0; i < visibleCount; i++)
            {
                StatusBarBuffRenderData buffEntry = buffEntries[i];
                int row = i / BUFF_TRAY_COLUMNS;
                int col = i % BUFF_TRAY_COLUMNS;
                int entriesInRow = Math.Min(BUFF_TRAY_COLUMNS, visibleCount - (row * BUFF_TRAY_COLUMNS));
                int currentRowWidth = (BUFF_ICON_SIZE * entriesInRow) + (BUFF_ICON_SPACING * Math.Max(0, entriesInRow - 1));
                int rowStartX = renderParameters.RenderWidth - BUFF_TRAY_RIGHT_MARGIN - currentRowWidth;
                Rectangle iconRect = new Rectangle(
                    rowStartX + col * (BUFF_ICON_SIZE + BUFF_ICON_SPACING),
                    BUFF_TRAY_TOP_MARGIN + row * (BUFF_ICON_SIZE + BUFF_ICON_SPACING),
                    BUFF_ICON_SIZE,
                    BUFF_ICON_SIZE);

                _buffIconHitboxes[buffEntry.SkillId] = iconRect;
                _buffTooltipEntries[buffEntry.SkillId] = buffEntry;
                DrawBuffIconFrame(sprite, iconRect);

                Texture2D iconTexture = buffEntry.IconTexture;
                if (iconTexture == null && !_buffIconTextures.TryGetValue(buffEntry.IconKey ?? string.Empty, out iconTexture))
                {
                    _buffIconTextures.TryGetValue("united/buff", out iconTexture);
                }

                if (iconTexture != null)
                {
                    sprite.Draw(iconTexture, iconRect, Color.White);
                }

                if (_font == null || buffEntry.RemainingMs <= 0)
                {
                    continue;
                }

                string remainingText = Math.Max(1, (int)Math.Ceiling(buffEntry.RemainingMs / 1000f)).ToString();
                Vector2 textSize = _font.MeasureString(remainingText) * 0.5f;
                Vector2 textPosition = new Vector2(
                    iconRect.Right - textSize.X - 2,
                    iconRect.Bottom - textSize.Y - 1);

                DrawTextWithShadow(sprite, remainingText, textPosition, Color.White, Color.Black, 0.5f);
            }
        }

        private void DrawHoveredBuffTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null || !TryGetHoveredBuffEntry(out StatusBarBuffRenderData buffEntry, out Rectangle iconRect))
            {
                return;
            }

            string title = SanitizeTooltipText(buffEntry.SkillName);
            string remainingText = buffEntry.RemainingMs > 0
                ? $"Time Left: {Math.Max(1, (int)Math.Ceiling(buffEntry.RemainingMs / 1000f))} sec"
                : "Time Left: --";
            string[] descriptionLines = WrapTooltipText(
                SanitizeTooltipText(buffEntry.Description),
                TOOLTIP_FALLBACK_WIDTH - (TOOLTIP_PADDING * 2));

            float textWidth = Math.Max(
                _font.MeasureString(title).X,
                _font.MeasureString(remainingText).X);
            if (descriptionLines.Length > 0)
            {
                textWidth = Math.Max(textWidth, MeasureLongestLine(descriptionLines));
            }

            int tooltipWidth = Math.Max(
                TOOLTIP_FALLBACK_WIDTH,
                (int)Math.Ceiling(textWidth + (TOOLTIP_PADDING * 2)));
            int tooltipHeight = (TOOLTIP_PADDING * 2) + _font.LineSpacing + TOOLTIP_TITLE_GAP + _font.LineSpacing;
            if (descriptionLines.Length > 0)
            {
                tooltipHeight += TOOLTIP_SECTION_GAP + (int)Math.Ceiling(MeasureLinesHeight(descriptionLines));
            }

            Point tooltipPosition = GetTooltipPosition(iconRect, tooltipWidth, tooltipHeight, renderWidth, renderHeight);
            Rectangle tooltipRect = new Rectangle(tooltipPosition.X, tooltipPosition.Y, tooltipWidth, tooltipHeight);

            DrawTooltipBackground(sprite, tooltipRect);

            float textY = tooltipRect.Y + TOOLTIP_PADDING;
            DrawTooltipText(sprite, title, new Vector2(tooltipRect.X + TOOLTIP_PADDING, textY), new Color(255, 238, 155));
            textY += _font.LineSpacing + TOOLTIP_TITLE_GAP;
            DrawTooltipText(sprite, remainingText, new Vector2(tooltipRect.X + TOOLTIP_PADDING, textY), Color.White);
            textY += _font.LineSpacing;

            if (descriptionLines.Length > 0)
            {
                textY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, descriptionLines, tooltipRect.X + TOOLTIP_PADDING, textY, new Color(219, 219, 219));
            }
        }

        private bool TryGetHoveredBuffEntry(out StatusBarBuffRenderData buffEntry, out Rectangle iconRect)
        {
            Point mousePosition = new Point(Mouse.GetState().X, Mouse.GetState().Y);
            return TryGetBuffEntryAt(mousePosition, out buffEntry, out iconRect);
        }

        private bool TryGetBuffEntryAt(Point mousePosition, out StatusBarBuffRenderData buffEntry, out Rectangle iconRect)
        {
            foreach (KeyValuePair<int, Rectangle> hitbox in _buffIconHitboxes)
            {
                if (!hitbox.Value.Contains(mousePosition))
                {
                    continue;
                }

                iconRect = hitbox.Value;
                return _buffTooltipEntries.TryGetValue(hitbox.Key, out buffEntry);
            }

            buffEntry = null;
            iconRect = Rectangle.Empty;
            return false;
        }

        private bool TryGetHoveredCooldownEntry(out StatusBarCooldownRenderData cooldownEntry, out Rectangle iconRect)
        {
            Point mousePosition = new Point(Mouse.GetState().X, Mouse.GetState().Y);
            foreach (KeyValuePair<int, Rectangle> hitbox in _cooldownIconHitboxes)
            {
                if (!hitbox.Value.Contains(mousePosition))
                {
                    continue;
                }

                iconRect = hitbox.Value;
                return _cooldownTooltipEntries.TryGetValue(hitbox.Key, out cooldownEntry);
            }

            cooldownEntry = null;
            iconRect = Rectangle.Empty;
            return false;
        }

        private void DrawBuffIconFrame(SpriteBatch sprite, Rectangle iconRect)
        {
            if (_pixelTexture == null)
            {
                return;
            }

            sprite.Draw(_pixelTexture, iconRect, new Color(18, 18, 28, 220));
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.X, iconRect.Y, iconRect.Width, 1), new Color(92, 92, 110));
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.X, iconRect.Bottom - 1, iconRect.Width, 1), new Color(42, 42, 54));
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.X, iconRect.Y, 1, iconRect.Height), new Color(92, 92, 110));
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.Right - 1, iconRect.Y, 1, iconRect.Height), new Color(42, 42, 54));
        }

        private void DrawCooldownTray(SpriteBatch sprite, RenderParameters renderParameters, int currentTime)
        {
            _cooldownIconHitboxes.Clear();
            _cooldownTooltipEntries.Clear();
            if (_getCooldownStatus == null)
            {
                return;
            }

            IReadOnlyList<StatusBarCooldownRenderData> cooldownEntries = _getCooldownStatus(currentTime);
            if (cooldownEntries == null || cooldownEntries.Count == 0)
            {
                return;
            }

            int visibleCount = Math.Min(cooldownEntries.Count, COOLDOWN_TRAY_COLUMNS);
            int trayWidth = (BUFF_ICON_SIZE * visibleCount) + (BUFF_ICON_SPACING * Math.Max(0, visibleCount - 1));
            int startX = renderParameters.RenderWidth - COOLDOWN_TRAY_RIGHT_MARGIN - trayWidth;

            for (int i = 0; i < visibleCount; i++)
            {
                StatusBarCooldownRenderData cooldownEntry = cooldownEntries[i];
                Rectangle iconRect = new Rectangle(
                    startX + i * (BUFF_ICON_SIZE + BUFF_ICON_SPACING),
                    COOLDOWN_TRAY_TOP_MARGIN,
                    BUFF_ICON_SIZE,
                    BUFF_ICON_SIZE);
                _cooldownIconHitboxes[cooldownEntry.SkillId] = iconRect;
                _cooldownTooltipEntries[cooldownEntry.SkillId] = cooldownEntry;

                DrawBuffIconFrame(sprite, iconRect);
                if (cooldownEntry.IconTexture != null)
                {
                    sprite.Draw(cooldownEntry.IconTexture, iconRect, Color.White);
                }

                float remainingProgress = cooldownEntry.DurationMs > 0
                    ? Math.Clamp(cooldownEntry.RemainingMs / (float)cooldownEntry.DurationMs, 0f, 1f)
                    : 1f;
                DrawCooldownOverlay(sprite, iconRect, remainingProgress);

                if (_font == null || cooldownEntry.RemainingMs <= 0)
                {
                    continue;
                }

                string remainingText = Math.Max(1, (int)Math.Ceiling(cooldownEntry.RemainingMs / 1000f)).ToString();
                Vector2 textSize = _font.MeasureString(remainingText) * 0.5f;
                Vector2 textPosition = new Vector2(
                    iconRect.Right - textSize.X - 2,
                    iconRect.Bottom - textSize.Y - 1);

                DrawTextWithShadow(sprite, remainingText, textPosition, Color.White, Color.Black, 0.5f);
            }
        }

        private void DrawHoveredCooldownTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_font == null || !TryGetHoveredCooldownEntry(out StatusBarCooldownRenderData cooldownEntry, out Rectangle iconRect))
            {
                return;
            }

            string title = SanitizeTooltipText(cooldownEntry.SkillName);
            string remainingText = cooldownEntry.RemainingMs > 0
                ? $"Cooldown: {Math.Max(1, (int)Math.Ceiling(cooldownEntry.RemainingMs / 1000f))} sec"
                : "Cooldown: --";
            string[] descriptionLines = WrapTooltipText(
                SanitizeTooltipText(cooldownEntry.Description),
                TOOLTIP_FALLBACK_WIDTH - (TOOLTIP_PADDING * 2));

            float textWidth = Math.Max(
                _font.MeasureString(title).X,
                _font.MeasureString(remainingText).X);
            if (descriptionLines.Length > 0)
            {
                textWidth = Math.Max(textWidth, MeasureLongestLine(descriptionLines));
            }

            int tooltipWidth = Math.Max(
                TOOLTIP_FALLBACK_WIDTH,
                (int)Math.Ceiling(textWidth + (TOOLTIP_PADDING * 2)));
            int tooltipHeight = (TOOLTIP_PADDING * 2) + _font.LineSpacing + TOOLTIP_TITLE_GAP + _font.LineSpacing;
            if (descriptionLines.Length > 0)
            {
                tooltipHeight += TOOLTIP_SECTION_GAP + (int)Math.Ceiling(MeasureLinesHeight(descriptionLines));
            }

            Point tooltipPosition = GetTooltipPosition(iconRect, tooltipWidth, tooltipHeight, renderWidth, renderHeight);
            Rectangle tooltipRect = new Rectangle(tooltipPosition.X, tooltipPosition.Y, tooltipWidth, tooltipHeight);

            DrawTooltipBackground(sprite, tooltipRect);

            float textY = tooltipRect.Y + TOOLTIP_PADDING;
            DrawTooltipText(sprite, title, new Vector2(tooltipRect.X + TOOLTIP_PADDING, textY), new Color(255, 238, 155));
            textY += _font.LineSpacing + TOOLTIP_TITLE_GAP;
            DrawTooltipText(sprite, remainingText, new Vector2(tooltipRect.X + TOOLTIP_PADDING, textY), Color.White);
            textY += _font.LineSpacing;

            if (descriptionLines.Length > 0)
            {
                textY += TOOLTIP_SECTION_GAP;
                DrawTooltipLines(sprite, descriptionLines, tooltipRect.X + TOOLTIP_PADDING, textY, new Color(219, 219, 219));
            }
        }

        private void DrawCooldownOverlay(SpriteBatch sprite, Rectangle iconRect, float remainingProgress)
        {
            if (_pixelTexture == null)
            {
                return;
            }

            int overlayHeight = Math.Clamp((int)Math.Round(iconRect.Height * remainingProgress), 0, iconRect.Height);
            if (overlayHeight <= 0)
            {
                return;
            }

            Rectangle overlayRect = new Rectangle(
                iconRect.X,
                iconRect.Bottom - overlayHeight,
                iconRect.Width,
                overlayHeight);
            sprite.Draw(_pixelTexture, overlayRect, new Color(0, 0, 0, 150));
        }

        private void DrawPreparedSkillBar(SpriteBatch sprite, Vector2 basePosGauge, int currentTime)
        {
            if (_getPreparedSkillStatus == null)
            {
                return;
            }

            StatusBarPreparedSkillRenderData preparedSkill = _getPreparedSkillStatus(currentTime);
            if (preparedSkill == null)
            {
                return;
            }

            PreparedSkillHudProfile hudProfile = ResolvePreparedSkillHudProfile(preparedSkill);
            float progress = ResolvePreparedSkillHudProgress(preparedSkill, hudProfile);
            StatusBarKeyDownBarTextures textures = ResolveKeyDownBarTextures(hudProfile.SkinKey ?? preparedSkill.SkinKey);
            Rectangle barRect = GetKeyDownBarRectangle(basePosGauge, textures);

            if (textures?.Bar != null)
            {
                sprite.Draw(textures.Bar, barRect, Color.White);
            }
            else
            {
                DrawBuffIconFrame(sprite, barRect);
            }

            DrawPreparedSkillGauge(sprite, barRect, progress, textures);

            if (textures?.Graduation != null)
            {
                sprite.Draw(textures.Graduation, barRect, Color.White);
            }

            DrawPreparedSkillHudText(sprite, preparedSkill, hudProfile, barRect, progress);
        }

        private void DrawPreparedSkillGauge(SpriteBatch sprite, Rectangle barRect, float progress, StatusBarKeyDownBarTextures textures)
        {
            int gaugeHeight = textures?.Gauge?.Height ?? Math.Max(1, barRect.Height - (KEYDOWN_BAR_GAUGE_OFFSET.Y * 2));
            int gaugeY = barRect.Y + KEYDOWN_BAR_GAUGE_OFFSET.Y;
            int gaugeX = barRect.X + KEYDOWN_BAR_GAUGE_OFFSET.X;
            int maxGaugeWidth = Math.Max(1, barRect.Width - (KEYDOWN_BAR_GAUGE_OFFSET.X * 2));
            int filledWidth = Math.Clamp((int)Math.Round(maxGaugeWidth * progress), 0, maxGaugeWidth);
            if (filledWidth <= 0)
            {
                return;
            }

            Rectangle gaugeRect = new Rectangle(gaugeX, gaugeY, filledWidth, gaugeHeight);
            if (textures?.Gauge != null)
            {
                sprite.Draw(textures.Gauge, gaugeRect, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, gaugeRect, new Color(255, 207, 76));
            }
        }

        private void DrawPreparedSkillHudText(SpriteBatch sprite, StatusBarPreparedSkillRenderData preparedSkill, PreparedSkillHudProfile hudProfile, Rectangle barRect, float progress)
        {
            if (_font == null || preparedSkill == null)
            {
                return;
            }

            string title = SanitizeTooltipText(preparedSkill.SkillName);
            if (!string.IsNullOrWhiteSpace(title))
            {
                Vector2 titleSize = _font.MeasureString(title);
                Vector2 titlePos = new Vector2(
                    barRect.X + Math.Max(0f, (barRect.Width - titleSize.X) * 0.5f),
                    barRect.Y - _font.LineSpacing - PREPARED_SKILL_LABEL_GAP);
                DrawTooltipText(sprite, title, titlePos, new Color(255, 238, 155));
            }

            string statusText = BuildPreparedSkillStatusText(preparedSkill, hudProfile, progress);
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return;
            }

            Vector2 statusSize = _font.MeasureString(statusText);
            Vector2 statusPos = new Vector2(
                barRect.Right - statusSize.X,
                barRect.Y - _font.LineSpacing - PREPARED_SKILL_LABEL_GAP);
            DrawTooltipText(sprite, statusText, statusPos, Color.White);
        }

        private static string BuildPreparedSkillStatusText(StatusBarPreparedSkillRenderData preparedSkill, PreparedSkillHudProfile hudProfile, float progress)
        {
            if (preparedSkill == null)
            {
                return string.Empty;
            }

            if (hudProfile.GaugeDurationMs > 0)
            {
                return progress >= 0.999f
                    ? "Ready"
                    : $"{Math.Clamp((int)Math.Round(progress * 100f), 0, 100)}%";
            }

            if (preparedSkill.RemainingMs > 0)
            {
                return $"{Math.Max(1, (int)Math.Ceiling(preparedSkill.RemainingMs / 1000f))} sec";
            }

            return $"{Math.Clamp((int)Math.Round(progress * 100f), 0, 100)}%";
        }

        private static float ResolvePreparedSkillHudProgress(StatusBarPreparedSkillRenderData preparedSkill, PreparedSkillHudProfile hudProfile)
        {
            if (preparedSkill == null)
            {
                return 0f;
            }

            if (hudProfile.GaugeDurationMs > 0)
            {
                int elapsedMs = Math.Max(0, preparedSkill.DurationMs - preparedSkill.RemainingMs);
                return Math.Clamp(elapsedMs / (float)hudProfile.GaugeDurationMs, 0f, 1f);
            }

            return Math.Clamp(preparedSkill.Progress, 0f, 1f);
        }

        private static PreparedSkillHudProfile ResolvePreparedSkillHudProfile(StatusBarPreparedSkillRenderData preparedSkill)
        {
            if (preparedSkill == null)
            {
                return PreparedSkillHudProfile.Default;
            }

            return preparedSkill.SkillId switch
            {
                35121003 => new PreparedSkillHudProfile("KeyDownBar4", 2000),
                4341002 => new PreparedSkillHudProfile("KeyDownBar1", 600),
                5101004 => new PreparedSkillHudProfile("KeyDownBar1", 1000),
                15101003 => new PreparedSkillHudProfile("KeyDownBar1", 1000),
                2121001 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 1000),
                2221001 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 1000),
                2321001 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 1000),
                3121004 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 2000),
                3221001 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 900),
                4341003 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 1200),
                5201002 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 1000),
                13111002 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 1000),
                22121000 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 500),
                22151001 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 500),
                33101005 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 900),
                33121009 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 2000),
                35001001 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 2000),
                35101009 => new PreparedSkillHudProfile(preparedSkill.SkinKey, 2000),
                _ => new PreparedSkillHudProfile(preparedSkill.SkinKey, 0)
            };
        }

        private Point GetTooltipPosition(Rectangle anchorRect, int tooltipWidth, int tooltipHeight, int renderWidth, int renderHeight)
        {
            int x = anchorRect.Right + TOOLTIP_OFFSET_X;
            int y = anchorRect.Y + TOOLTIP_OFFSET_Y;

            if (x + tooltipWidth > renderWidth - TOOLTIP_PADDING)
            {
                x = anchorRect.Left - tooltipWidth - TOOLTIP_OFFSET_X;
            }

            if (x < TOOLTIP_PADDING)
            {
                x = Math.Max(TOOLTIP_PADDING, Math.Min(anchorRect.Left, renderWidth - tooltipWidth - TOOLTIP_PADDING));
                y = anchorRect.Top - tooltipHeight - TOOLTIP_PADDING;
            }

            if (y + tooltipHeight > renderHeight - TOOLTIP_PADDING)
            {
                y = renderHeight - tooltipHeight - TOOLTIP_PADDING;
            }

            if (y < TOOLTIP_PADDING)
            {
                y = Math.Min(renderHeight - tooltipHeight - TOOLTIP_PADDING, anchorRect.Bottom + TOOLTIP_PADDING);
            }

            return new Point(
                Math.Max(TOOLTIP_PADDING, x),
                Math.Max(TOOLTIP_PADDING, y));
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect)
        {
            Texture2D leftFrame = _tooltipFrames[0];
            Texture2D centerFrame = _tooltipFrames[1];
            Texture2D rightFrame = _tooltipFrames[2];
            if (leftFrame != null && centerFrame != null && rightFrame != null)
            {
                sprite.Draw(leftFrame, new Rectangle(rect.X, rect.Y, leftFrame.Width, rect.Height), Color.White);
                sprite.Draw(centerFrame, new Rectangle(rect.X + leftFrame.Width, rect.Y, Math.Max(1, rect.Width - leftFrame.Width - rightFrame.Width), rect.Height), Color.White);
                sprite.Draw(rightFrame, new Rectangle(rect.Right - rightFrame.Width, rect.Y, rightFrame.Width, rect.Height), Color.White);
                return;
            }

            if (_pixelTexture == null)
            {
                return;
            }

            sprite.Draw(_pixelTexture, rect, new Color(18, 18, 26, 235));
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            if (_pixelTexture == null)
            {
                return;
            }

            Color borderColor = new Color(214, 174, 82);
            sprite.Draw(_pixelTexture, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
        }

        private void DrawTooltipLines(SpriteBatch sprite, string[] lines, int x, float y, Color color)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * _font.LineSpacing)), color);
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private float MeasureLongestLine(string[] lines)
        {
            float width = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    width = Math.Max(width, _font.MeasureString(lines[i]).X);
                }
            }

            return width;
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                return 0f;
            }

            int nonEmptyLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    nonEmptyLines++;
                }
            }

            return nonEmptyLines > 0 ? nonEmptyLines * _font.LineSpacing : 0f;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            var lines = new List<string>();
            string[] paragraphs = text.Replace("\r\n", "\n").Split('\n');
            foreach (string paragraph in paragraphs)
            {
                string trimmed = paragraph.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                string currentLine = string.Empty;
                string[] words = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
            }

            return lines.ToArray();
        }

        private static string SanitizeTooltipText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("\r", string.Empty)
                .Replace("\n", " ")
                .Replace("\t", " ");
        }

        private Rectangle GetKeyDownBarRectangle(Vector2 basePosGauge, StatusBarKeyDownBarTextures textures)
        {
            Texture2D barTexture = textures?.Bar;
            int width = barTexture?.Width ?? 72;
            int height = barTexture?.Height ?? 12;
            Vector2 barPos = basePosGauge + KEYDOWN_BAR_POS;
            return new Rectangle((int)barPos.X, (int)barPos.Y, width, height);
        }

        private StatusBarKeyDownBarTextures ResolveKeyDownBarTextures(string skinKey)
        {
            if (!string.IsNullOrWhiteSpace(skinKey)
                && _keyDownBarTextures.TryGetValue(skinKey, out StatusBarKeyDownBarTextures skinTextures))
            {
                return skinTextures;
            }

            _keyDownBarTextures.TryGetValue("KeyDownBar", out StatusBarKeyDownBarTextures defaultTextures);
            return defaultTextures;
        }

        private readonly struct PreparedSkillHudProfile
        {
            public static PreparedSkillHudProfile Default => new PreparedSkillHudProfile("KeyDownBar", 0);

            public PreparedSkillHudProfile(string skinKey, int gaugeDurationMs)
            {
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey;
                GaugeDurationMs = gaugeDurationMs;
            }

            public string SkinKey { get; }
            public int GaugeDurationMs { get; }
        }

        /// <summary>
        /// Draw a gauge bar using the actual gauge texture from UI.wz.
        /// The gauge is rendered by drawing a portion of the texture based on fill ratio.
        /// The texture is clipped from the left side proportional to the fill amount.
        /// </summary>
        private void DrawTexturedGauge(SpriteBatch sprite, Vector2 basePos, Rectangle gaugeRect, float fillRatio, Texture2D gaugeTexture) {
            if (fillRatio <= 0 || gaugeTexture == null)
                return;

            // Calculate the destination rectangle position
            Vector2 gaugePos = basePos + new Vector2(gaugeRect.X, gaugeRect.Y);

            // Calculate the filled width - use the gauge rect's defined width
            int destFilledWidth = (int)(gaugeRect.Width * fillRatio);
            if (destFilledWidth <= 0)
                return;

            // Calculate corresponding source width from texture (proportional to fill ratio)
            int srcFilledWidth = (int)(gaugeTexture.Width * fillRatio);
            if (srcFilledWidth <= 0)
                srcFilledWidth = 1;

            // Source rectangle - clip from left side of texture based on fill ratio
            Rectangle sourceRect = new Rectangle(0, 0, srcFilledWidth, gaugeTexture.Height);

            // Destination rectangle - draw at gauge position with filled width
            // Scale the texture to fit the gauge rect height
            Rectangle destRect = new Rectangle(
                (int)gaugePos.X,
                (int)gaugePos.Y,
                destFilledWidth,
                gaugeRect.Height
            );

            sprite.Draw(gaugeTexture, destRect, sourceRect, Color.White);
        }

        /// <summary>
        /// Draw a single gauge bar with background and filled portion (fallback when no texture).
        /// </summary>
        private void DrawGaugeBar(SpriteBatch sprite, Vector2 basePos, Rectangle gaugeRect, float fillRatio, Color fillColor, Color bgColor) {
            // Calculate absolute position
            Rectangle absoluteRect = new Rectangle(
                (int)basePos.X + gaugeRect.X,
                (int)basePos.Y + gaugeRect.Y,
                gaugeRect.Width,
                gaugeRect.Height
            );

            // Draw background
            sprite.Draw(_pixelTexture, absoluteRect, bgColor);

            // Draw filled portion
            if (fillRatio > 0) {
                Rectangle fillRect = new Rectangle(
                    absoluteRect.X,
                    absoluteRect.Y,
                    (int)(absoluteRect.Width * fillRatio),
                    absoluteRect.Height
                );
                sprite.Draw(_pixelTexture, fillRect, fillColor);
            }

            // Draw border (1px)
            Color borderColor = new Color(40, 40, 40);
            // Top border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X, absoluteRect.Y, absoluteRect.Width, 1), borderColor);
            // Bottom border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X, absoluteRect.Y + absoluteRect.Height - 1, absoluteRect.Width, 1), borderColor);
            // Left border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X, absoluteRect.Y, 1, absoluteRect.Height), borderColor);
            // Right border
            sprite.Draw(_pixelTexture, new Rectangle(absoluteRect.X + absoluteRect.Width - 1, absoluteRect.Y, 1, absoluteRect.Height), borderColor);
        }

        /// <summary>
        /// Draw text with a shadow effect (similar to client rendering)
        /// </summary>
        private void DrawTextWithShadow(SpriteBatch sprite, string text, Vector2 position, Color textColor, Color shadowColor, float scale = 1.0f) {
            // Draw shadow at offset positions (similar to client shadow rendering)
            sprite.DrawString(_font, text, position + new Vector2(-1, 0), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(1, 0), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(0, -1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(0, 1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Draw main text
            sprite.DrawString(_font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawDiagonalShadowText(SpriteBatch sprite, string text, Vector2 position, Color textColor, Color shadowColor, float scale = 1.0f)
        {
            sprite.DrawString(_font, text, position + new Vector2(-1, -1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(1, -1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(-1, 1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(1, 1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw a string using bitmap font textures (MapleStory style).
        /// Falls back to SpriteFont for characters without bitmap textures.
        /// Returns the width of the drawn string for positioning.
        /// </summary>
        private int DrawBitmapString(SpriteBatch sprite, string text, Vector2 position, float scale = 1.0f) {
            if (_digitTextures == null || !_useBitmapFont)
                return 0;

            int xOffset = 0;
            const int CHAR_SPACING = 0;  // Spacing between characters

            // Get baseline from digit '0' origin - all characters should align to this
            int baselineY = _digitOrigins?[0].Y ?? 0;

            foreach (char c in text) {
                Texture2D texture = GetTextureForChar(c);
                Point origin = GetOriginForChar(c);

                if (texture != null) {
                    // Apply origin offset - origin.Y determines vertical alignment
                    // Characters with different origins need to be adjusted so they align on baseline
                    int yAdjust = baselineY - origin.Y;

                    Rectangle destRect = new Rectangle(
                        (int)(position.X + xOffset - origin.X * scale),
                        (int)(position.Y + yAdjust * scale),
                        (int)(texture.Width * scale),
                        (int)(texture.Height * scale)
                    );
                    sprite.Draw(texture, destRect, Color.White);
                    xOffset += (int)(texture.Width * scale) + CHAR_SPACING;
                } else if (c == ' ') {
                    // Space character - use average digit width
                    xOffset += (int)((_digitTextures[0]?.Width ?? 8) * scale * 0.5f);
                } else if (_font != null) {
                    // Fall back to SpriteFont for characters without bitmap textures
                    string charStr = c.ToString();
                    Vector2 charPos = new Vector2(position.X + xOffset, position.Y);
                    // Draw with slight shadow for visibility
                    sprite.DrawString(_font, charStr, charPos + new Vector2(1, 1), Color.Black, 0f, Vector2.Zero, scale * 0.8f, SpriteEffects.None, 0f);
                    sprite.DrawString(_font, charStr, charPos, Color.White, 0f, Vector2.Zero, scale * 0.8f, SpriteEffects.None, 0f);
                    Vector2 charSize = _font.MeasureString(charStr) * scale * 0.8f;
                    xOffset += (int)charSize.X + CHAR_SPACING;
                }
            }

            return xOffset;
        }

        private int DrawDigitBitmapString(SpriteBatch sprite, string text, Vector2 position, Texture2D[] digitTextures, Point[] digitOrigins, float scale = 1.0f)
        {
            if (digitTextures == null || digitTextures.Length < 10)
                return 0;

            int xOffset = 0;
            int baselineY = digitOrigins != null && digitOrigins.Length > 0 ? digitOrigins[0].Y : 0;

            foreach (char c in text)
            {
                if (c < '0' || c > '9')
                    continue;

                int index = c - '0';
                Texture2D texture = digitTextures[index];
                if (texture == null)
                    continue;

                Point origin = digitOrigins != null && digitOrigins.Length > index ? digitOrigins[index] : Point.Zero;
                int yAdjust = baselineY - origin.Y;
                Rectangle destRect = new Rectangle(
                    (int)(position.X + xOffset - origin.X * scale),
                    (int)(position.Y + yAdjust * scale),
                    (int)(texture.Width * scale),
                    (int)(texture.Height * scale));
                sprite.Draw(texture, destRect, Color.White);
                xOffset += (int)(texture.Width * scale);
            }

            return xOffset;
        }

        /// <summary>
        /// Get the texture for a specific character
        /// </summary>
        private Texture2D GetTextureForChar(char c) {
            if (c >= '0' && c <= '9') {
                return _digitTextures?[c - '0'];
            }
            return c switch {
                '/' or '\\' => _slashTexture,
                '%' => _percentTexture,
                '[' => _bracketLeftTexture,
                ']' => _bracketRightTexture,
                '.' => _dotTexture,
                _ => null
            };
        }

        /// <summary>
        /// Get the origin point for a specific character
        /// </summary>
        private Point GetOriginForChar(char c) {
            if (c >= '0' && c <= '9' && _digitOrigins != null && _digitOrigins.Length > c - '0') {
                return _digitOrigins[c - '0'];
            }
            return c switch {
                '/' or '\\' => _slashOrigin,
                '%' => _percentOrigin,
                '[' => _bracketLeftOrigin,
                ']' => _bracketRightOrigin,
                '.' => _dotOrigin,
                _ => Point.Zero
            };
        }

        /// <summary>
        /// Measure the width of a bitmap string
        /// </summary>
        private int MeasureBitmapString(string text, float scale = 1.0f) {
            if (_digitTextures == null || !_useBitmapFont)
                return 0;

            int width = 0;
            const int CHAR_SPACING = 0;

            foreach (char c in text) {
                Texture2D texture = GetTextureForChar(c);
                if (texture != null) {
                    width += (int)(texture.Width * scale) + CHAR_SPACING;
                } else if (c == ' ') {
                    width += (int)((_digitTextures[0]?.Width ?? 8) * scale * 0.5f);
                }
            }

            return width;
        }

        #region IClickableUIObject
        private Point? mouseOffsetOnDragStart = null;
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight) {
            HandleBuffTrayRightClick(mouseState);
            return UIMouseEventHandler.CheckMouseEvent(shiftCenteredX, shiftCenteredY, this.Position.X, this.Position.Y, mouseState, mouseCursor, uiButtons, false);

            // handle UI movement
            /*  if (mouseState.LeftButton == ButtonState.Pressed) {
                  // The rectangle of the MinimapItem UI object

                  // if drag has not started, initialize the offset
                  if (mouseOffsetOnDragStart == null) {
                      Rectangle rect = new Rectangle(
                          this.Position.X,
                          this.Position.Y,
                          this.LastFrameDrawn.Width, this.LastFrameDrawn.Height);
                      if (!rect.Contains(mouseState.X, mouseState.Y)) {
                          return;
                      }
                      mouseOffsetOnDragStart = new Point(mouseState.X - this.Position.X, mouseState.Y - this.Position.Y);
                  }

                  // Calculate the mouse position relative to the minimap
                  // and move the minimap Position
                  this.Position = new Point(mouseState.X - mouseOffsetOnDragStart.Value.X, mouseState.Y - mouseOffsetOnDragStart.Value.Y);
                  //System.Diagnostics.Debug.WriteLine("Button rect: " + rect.ToString());
                  //System.Diagnostics.Debug.WriteLine("Mouse X: " + mouseState.X + ", Y: " + mouseState.Y);
              }
              else {
                  // if the mouse button is not pressed, reset the initial drag offset
                  mouseOffsetOnDragStart = null;

                  // If the window is outside at the end of mouse click + move
                  // move it slightly back to the nearest X and Y coordinate
              }*/
        }

        private void HandleBuffTrayRightClick(MouseState mouseState)
        {
            bool releasedThisFrame = _previousRightButtonState == ButtonState.Pressed
                && mouseState.RightButton == ButtonState.Released;
            _previousRightButtonState = mouseState.RightButton;
            if (!releasedThisFrame || BuffCancelRequested == null)
            {
                return;
            }

            if (TryGetBuffEntryAt(new Point(mouseState.X, mouseState.Y), out StatusBarBuffRenderData buffEntry, out _))
            {
                BuffCancelRequested.Invoke(buffEntry.SkillId);
            }
        }
        #endregion
    }
}
