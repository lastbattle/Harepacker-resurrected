using HaCreator.MapSimulator.UI;
using HaCreator.MapSimulator.Character.Skills;
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

    public enum PreparedSkillHudSurface
    {
        StatusBar,
        World
    }

    public enum PreparedSkillHudTextVariant
    {
        Default,
        ReleaseArmed,
        Amplify
    }

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
        public string CounterText { get; set; }
        public string TooltipStateText { get; set; }
        public int DurationMs { get; set; }
        public int SortOrder { get; set; }
        public string FamilyDisplayName { get; set; }
        public IReadOnlyList<string> TemporaryStatLabels { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> TemporaryStatDisplayNames { get; set; } = Array.Empty<string>();
        public bool IsAlerting { get; set; }
        public bool UseTemporaryStatViewArtworkOnly { get; set; }
        public int LayerUpdateSequence { get; set; }
        public int LowDurabilityAlertSequence { get; set; }
        public int LowDurabilityAlertStartTime { get; set; } = int.MinValue;
        public int ShadowIndex { get; set; }
        public int ShadowIndexUpdateSequence { get; set; }
        public int MainLayerAnimationSequence { get; set; }
        public int ShadowLayerAnimationSequence { get; set; }
        public string ShadowCanvasPath { get; set; }
        public int ShadowCanvasRemoveIndex { get; set; }
        public int ShadowCanvasInsertDelayMs { get; set; }
        public int ShadowCanvasAlphaStart { get; set; }
        public int ShadowCanvasAlphaEnd { get; set; }
        public int ShadowCanvasLastUpdatedTime { get; set; } = int.MinValue;
        public int AlertLayerAnimationMode { get; set; }
        public int AlertLayerAnimationSequence { get; set; }
    }

    public class StatusBarPreparedSkillRenderData
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string SkinKey { get; set; } = "KeyDownBar";
        public PreparedSkillHudSurface Surface { get; set; } = PreparedSkillHudSurface.StatusBar;
        public int RemainingMs { get; set; }
        public int DurationMs { get; set; }
        public int GaugeDurationMs { get; set; }
        public float Progress { get; set; }
        public bool IsKeydownSkill { get; set; }
        public bool IsPreparingPhase { get; set; }
        public bool IsHolding { get; set; }
        public int PrepareRemainingMs { get; set; }
        public int HoldElapsedMs { get; set; }
        public int MaxHoldDurationMs { get; set; }
        public PreparedSkillHudTextVariant TextVariant { get; set; }
        public bool ShowText { get; set; } = true;
        public Vector2 WorldAnchor { get; set; }
    }

    public class StatusBarCooldownRenderData
    {
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string Description { get; set; }
        public Texture2D IconTexture { get; set; }
        public int RemainingMs { get; set; }
        public string CounterText { get; set; }
        public string TooltipStateText { get; set; }
        public int DurationMs { get; set; }
        public int MaskFrameIndex { get; set; } = 15;
        public SkillManager.CooldownMaskSurface MaskSurface { get; set; } = SkillManager.CooldownMaskSurface.SkillBookClassic;
        public string TooltipCostLineMarkup { get; set; }
        public bool SuppressProgressOverlay { get; set; }
        public bool SuppressCounterText { get; set; }
        public bool UseQuickSlotMaskSurface { get; set; } = true;
        public int ShortcutSlotIndex { get; set; } = -1;
    }

    public class StatusBarKeyDownBarTextures
    {
        public Texture2D Bar { get; set; }
        public Texture2D Gauge { get; set; }
        public Texture2D Graduation { get; set; }
        public Point BarOrigin { get; set; }
    }

    public class StatusBarWarningAnimation
    {
        public Texture2D[] Frames { get; set; } = Array.Empty<Texture2D>();
        public int FrameDelayMs { get; set; } = 120;
        public int FlashDurationMs { get; set; } = 500;
    }

    public class StatusBarUI : BaseDXDrawableItem, IUIObjectEvents {
        private readonly List<UIObject> uiButtons = new List<UIObject>();
        private readonly UIObject _channelButton = null;

        // Character stats display - positions based on IDA Pro analysis of client CUIStatusBar
        // HP text at (163, 4), MP text at (332, 4), EXP text at (332, 20)
        // Level at (45, 552), Job at (75, 549), Name at (75, 561) (absolute screen positions)
        private SpriteFont _font;
        private Func<CharacterStatsData> _getCharacterStats;
        private Func<int, IReadOnlyList<StatusBarBuffRenderData>> _getBuffStatus;
        private Func<int, IReadOnlyList<StatusBarCooldownRenderData>> _getCooldownStatus;
        private Func<int, IReadOnlyList<StatusBarCooldownRenderData>> _getOffBarCooldownStatus;
        private Func<int, StatusBarPreparedSkillRenderData> _getPreparedSkillStatus;
        private Func<int, StatusBarPreparedSkillRenderData> _getPreparedSkillOverlayStatus;
        private Texture2D _pixelTexture;
        private ClientTextRasterizer _clientTextRasterizer;
        private ClientTextRasterizer _jobTextRasterizer;
        private ClientTextRasterizer _nameTextRasterizer;

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
        private readonly Dictionary<int, Texture2D> _temporaryStatViewShadowTextures = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, Rectangle> _buffIconHitboxes = new Dictionary<int, Rectangle>();
        private readonly Dictionary<int, StatusBarBuffRenderData> _buffTooltipEntries = new Dictionary<int, StatusBarBuffRenderData>();
        private readonly List<StatusBarCooldownTooltipHitTarget> _cooldownTooltipHitTargets = new List<StatusBarCooldownTooltipHitTarget>();
        private readonly Dictionary<string, StatusBarKeyDownBarTextures> _keyDownBarTextures = new Dictionary<string, StatusBarKeyDownBarTextures>(StringComparer.OrdinalIgnoreCase);
        private Texture2D[] _cooldownMaskTextures = Array.Empty<Texture2D>();
        private Texture2D _temporaryStatViewTexture;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Point[] _tooltipFrameOrigins = new Point[3];
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

        private sealed class StatusBarCooldownTooltipHitTarget
        {
            public Rectangle IconRect { get; init; }
            public StatusBarCooldownRenderData CooldownEntry { get; init; }
            public SkillTooltipAnchorOwner AnchorOwner { get; init; }
        }

        // Text positions relative to status bar (from IDA Pro analysis)
        // The status bar is composed of: lvBacktrnd (left ~64px) + gaugeBackgrd (center) + buttons
        // From SetNumberValue: HP(163,4), MP(332,4), EXP(332,20) - relative to gauge layer
        // From SetStatusValue: Level(45,552), Job(75,549), Name(75,561) - in 800x578 canvas
        // Converting to relative positions within status bar:
        //   Level: (45, 10), Job: (75, 7), Name: (75, 19)

        // lvBacktrnd area (left side - character info)
        private static readonly Vector2 LEVEL_TEXT_POS = new Vector2(44, 8);
        private static readonly Vector2 JOB_TEXT_POS = new Vector2(74, 5);
        private static readonly Vector2 NAME_TEXT_POS = new Vector2(74, 17);
        private const float JOB_TEXT_SCALE = 1.0f;
        private const float NAME_TEXT_SCALE = 1.0f;
        private const float JOB_TEXT_FONT_PIXEL_SIZE = 9f;
        private const float NAME_TEXT_FONT_PIXEL_SIZE = 10f;
        private const float JOB_TEXT_MAX_WIDTH = 92f;
        private const float DEFAULT_NAME_TEXT_MAX_WIDTH = 140f;
        private static readonly Color JOB_TEXT_COLOR = new Color(132, 182, 104);
        private static readonly Color NAME_TEXT_COLOR = new Color(255, 255, 255, 248);
        private static readonly Color NAME_TEXT_SHADOW_COLOR = new Color(0, 0, 0, 208);

        // gaugeBackgrd area - HP/MP/EXP text and gauges
        // lvBacktrnd is ~64px wide, so gauge area starts at X~64
        // HP/MP/EXP positions from SetNumberValue are relative to gauge layer
        private static readonly Vector2 HP_TEXT_POS = new Vector2(95, 5);
        private static readonly Vector2 MP_TEXT_POS = new Vector2(264, 5);
        private static readonly Vector2 EXP_TEXT_POS = new Vector2(264, 21);

        // Gauge bar positions and sizes
        // From IDA Pro CUIStatusBar::CGauge::Create:
        // HP Gauge: X=28 relative to gaugeBackgrd, Y=2, Length=138
        // MP Gauge: X=197 relative to gaugeBackgrd, Y=2, Length=138
        // EXP Gauge: X=28 relative to gaugeBackgrd, Y=18, Length=308
        // gaugeBackgrd starts at ~64px, so absolute X positions:
        // HP: 64+28=92, MP: 64+197=261, EXP: 64+28=92
        private static readonly Rectangle HP_GAUGE_RECT = new Rectangle(28, 2, 138, 12);
        private static readonly Rectangle MP_GAUGE_RECT = new Rectangle(197, 2, 138, 12);
        private static readonly Rectangle EXP_GAUGE_RECT = new Rectangle(28, 18, 308, 10);

        // Gauge colors matching original MapleStory client
        private static readonly Color HP_GAUGE_COLOR = new Color(255, 50, 50);       // Red for HP
        private static readonly Color HP_GAUGE_BG_COLOR = new Color(80, 20, 20);     // Dark red background
        private static readonly Color MP_GAUGE_COLOR = new Color(50, 100, 255);      // Blue for MP
        private static readonly Color MP_GAUGE_BG_COLOR = new Color(20, 40, 80);     // Dark blue background
        private static readonly Color EXP_GAUGE_COLOR = new Color(255, 255, 50);     // Yellow for EXP
        private static readonly Color EXP_GAUGE_BG_COLOR = new Color(60, 60, 20);    // Dark yellow background
        // These offsets are derived from the composed StatusBar2 mainBar frame
        // plus the client SetStatusValue absolute text slots in the 800x578 HUD canvas.
        private static readonly Point STATUS_BAR_LEFT_BASE_OFFSET = new Point(0, 49);
        private static readonly Point STATUS_BAR_GAUGE_BASE_OFFSET = new Point(155, 52);
        private Point _statusBarLeftBaseOffset = STATUS_BAR_LEFT_BASE_OFFSET;
        private Point _statusBarGaugeBaseOffset = STATUS_BAR_GAUGE_BASE_OFFSET;
        private Vector2 _hpTextPos = HP_TEXT_POS;
        private Vector2 _mpTextPos = MP_TEXT_POS;
        private Vector2 _expTextPos = EXP_TEXT_POS;
        private float _jobTextMaxWidth = JOB_TEXT_MAX_WIDTH;
        private float _nameTextMaxWidth = DEFAULT_NAME_TEXT_MAX_WIDTH;
        private const int BUFF_ICON_SIZE = 32;
        private const int BUFF_ICON_SPACING = 2;
        private const int BUFF_TRAY_COLUMNS = 10;
        private const int BUFF_TRAY_ROWS = 2;
        private const int BUFF_TRAY_TOP_MARGIN = 8;
        private const int BUFF_TRAY_RIGHT_MARGIN = 8;
        private const int COOLDOWN_TRAY_TOP_MARGIN = BUFF_TRAY_TOP_MARGIN + (BUFF_TRAY_ROWS * (BUFF_ICON_SIZE + BUFF_ICON_SPACING)) + 4;
        private const int COOLDOWN_TRAY_RIGHT_MARGIN = 8;
        // Client evidence: CUIStatusBar::CQuickSlot::GetIndexByPos checks 8 fixed 32x32 cells.
        // WZ evidence: UI/StatusBar2.img/mainBar/quickSlot/quickSlot is a 145x93 4x2 panel.
        private const int CLIENT_SHORTCUT_SLOT_COUNT = 8;
        private const int CLIENT_SHORTCUT_SLOT_COLUMNS = 4;
        private const int CLIENT_SHORTCUT_SLOT_SIZE = 32;
        private const int CLIENT_SHORTCUT_SLOT_STEP = 33;
        private const int CLIENT_SHORTCUT_TRAY_WIDTH = 145;
        private const int CLIENT_SHORTCUT_TRAY_SLOT_LEFT = 6;
        private const int CLIENT_SHORTCUT_TRAY_SLOT_TOP = 14;
        private const int OFFBAR_COOLDOWN_ICON_SIZE = 24;
        private const int OFFBAR_COOLDOWN_ICON_SPACING = 2;
        private const int OFFBAR_COOLDOWN_TRAY_COLUMNS = 6;
        private const int OFFBAR_COOLDOWN_TRAY_ROWS = 2;
        private const int OFFBAR_COOLDOWN_TRAY_TOP_MARGIN = COOLDOWN_TRAY_TOP_MARGIN + BUFF_ICON_SIZE + 4;
        private const int OFFBAR_COOLDOWN_TRAY_RIGHT_MARGIN = 8;
        private const int TOOLTIP_PADDING = 10;
        private const int TOOLTIP_ICON_GAP = 8;
        private const int TOOLTIP_TITLE_GAP = 8;
        private const int TOOLTIP_SECTION_GAP = 6;
        private const int PREPARED_SKILL_LABEL_GAP = 6;
        // Preserve the existing default bar placement by treating the WZ origin
        // as the HUD anchor and remapping each skin's top-left from that anchor.
        private static readonly Point KEYDOWN_BAR_DEFAULT_ORIGIN = new Point(40, 83);
        private static readonly Vector2 KEYDOWN_BAR_ANCHOR_POS = new Vector2(
            214 + KEYDOWN_BAR_DEFAULT_ORIGIN.X,
            -22 + KEYDOWN_BAR_DEFAULT_ORIGIN.Y);
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
            {
                uiButtons.Add(obj_Ui_BtCashShop);
                obj_Ui_BtCashShop.ButtonClickReleased += _ => CashShopRequested?.Invoke();
            }
            if (obj_Ui_BtMTS != null)
            {
                uiButtons.Add(obj_Ui_BtMTS);
                obj_Ui_BtMTS.ButtonClickReleased += _ => MtsRequested?.Invoke();
            }
            if (obj_Ui_BtMenu != null)
            {
                uiButtons.Add(obj_Ui_BtMenu);
                obj_Ui_BtMenu.ButtonClickReleased += _ => MenuRequested?.Invoke();
            }
            if (obj_Ui_BtSystem != null)
            {
                uiButtons.Add(obj_Ui_BtSystem);
                obj_Ui_BtSystem.ButtonClickReleased += _ => SystemRequested?.Invoke();
            }
            if (obj_Ui_BtChannel != null)
            {
                _channelButton = obj_Ui_BtChannel;
                uiButtons.Add(obj_Ui_BtChannel);
                obj_Ui_BtChannel.ButtonClickReleased += _ => ChannelRequested?.Invoke();
            }

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

        public void SetOffBarCooldownStatusProvider(Func<int, IReadOnlyList<StatusBarCooldownRenderData>> getOffBarCooldownStatus)
        {
            _getOffBarCooldownStatus = getOffBarCooldownStatus;
        }

        public void SetPreparedSkillProvider(Func<int, StatusBarPreparedSkillRenderData> getPreparedSkillStatus)
        {
            _getPreparedSkillStatus = getPreparedSkillStatus;
        }

        public void SetPreparedSkillOverlayProvider(Func<int, StatusBarPreparedSkillRenderData> getPreparedSkillStatus)
        {
            _getPreparedSkillOverlayStatus = getPreparedSkillStatus;
        }

        public void SetLayoutMetrics(Point leftBaseOffset, Point gaugeBaseOffset)
        {
            _statusBarLeftBaseOffset = leftBaseOffset;
            _statusBarGaugeBaseOffset = gaugeBaseOffset;
        }

        public void SetGaugeTextAnchors(Vector2 hpTextPos, Vector2 mpTextPos, Vector2 expTextPos)
        {
            _hpTextPos = hpTextPos;
            _mpTextPos = mpTextPos;
            _expTextPos = expTextPos;
        }

        public void SetLeftLayoutMetric(Point leftBaseOffset)
        {
            _statusBarLeftBaseOffset = leftBaseOffset;
        }

        public void SetLeftClusterWidth(int leftClusterWidth)
        {
            if (leftClusterWidth <= NAME_TEXT_POS.X + 8f)
            {
                _jobTextMaxWidth = JOB_TEXT_MAX_WIDTH;
                _nameTextMaxWidth = DEFAULT_NAME_TEXT_MAX_WIDTH;
                return;
            }

            _jobTextMaxWidth = StatusBarLayoutRules.ResolveLeftClusterJobTextMaxWidth(
                leftClusterWidth,
                JOB_TEXT_POS.X,
                JOB_TEXT_MAX_WIDTH);
            _nameTextMaxWidth = StatusBarLayoutRules.ResolveLeftClusterNameTextMaxWidth(
                leftClusterWidth,
                NAME_TEXT_POS.X,
                DEFAULT_NAME_TEXT_MAX_WIDTH);
        }

        public Action<int> BuffCancelRequested { get; set; }
        public Action CashShopRequested { get; set; }
        public Action MtsRequested { get; set; }
        public Action MenuRequested { get; set; }
        public Action SystemRequested { get; set; }
        public Action ChannelRequested { get; set; }

        public void SetChannelButtonEnabled(bool enabled)
        {
            _channelButton?.SetEnabled(enabled);
        }

        /// <summary>
        /// Set the pixel texture for drawing gauge bars
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for creating textures</param>
        public void SetPixelTexture(GraphicsDevice graphicsDevice) {
            if (_pixelTexture == null && graphicsDevice != null) {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            if (_clientTextRasterizer == null && graphicsDevice != null)
            {
                _clientTextRasterizer = new ClientTextRasterizer(
                    graphicsDevice,
                    preferEmbeddedPrivateFontSources: true);
            }

            if (_jobTextRasterizer == null && graphicsDevice != null)
            {
                _jobTextRasterizer = new ClientTextRasterizer(
                    graphicsDevice,
                    basePointSize: JOB_TEXT_FONT_PIXEL_SIZE,
                    preferEmbeddedPrivateFontSources: true);
            }

            if (_nameTextRasterizer == null && graphicsDevice != null)
            {
                _nameTextRasterizer = new ClientTextRasterizer(
                    graphicsDevice,
                    basePointSize: NAME_TEXT_FONT_PIXEL_SIZE,
                    preferEmbeddedPrivateFontSources: true);
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

        public void SetCooldownMasks(Texture2D[] cooldownMaskTextures)
        {
            _cooldownMaskTextures = cooldownMaskTextures ?? Array.Empty<Texture2D>();
        }

        public void SetTemporaryStatViewTexture(Texture2D temporaryStatViewTexture)
        {
            _temporaryStatViewTexture = temporaryStatViewTexture;
        }

        public void SetTemporaryStatViewShadowTextures(Dictionary<int, Texture2D> temporaryStatViewShadowTextures)
        {
            _temporaryStatViewShadowTextures.Clear();
            if (temporaryStatViewShadowTextures == null)
            {
                return;
            }

            foreach (KeyValuePair<int, Texture2D> shadowEntry in temporaryStatViewShadowTextures)
            {
                if (shadowEntry.Value != null)
                {
                    _temporaryStatViewShadowTextures[Math.Clamp(shadowEntry.Key, 0, 15)] = shadowEntry.Value;
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

        public void SetTooltipOrigins(Point[] tooltipOrigins)
        {
            if (tooltipOrigins == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_tooltipFrameOrigins.Length, tooltipOrigins.Length); i++)
            {
                _tooltipFrameOrigins[i] = tooltipOrigins[i];
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
                if (uiBtn == null || !uiBtn.ButtonVisible)
                {
                    continue;
                }

                BaseDXDrawableItem buttonToDraw = uiBtn.GetBaseDXDrawableItemByState();
                Point buttonDrawPosition = uiBtn.GetDrawPositionByState();

                // Position drawn is relative to this UI
                int drawRelativeX = -(this.Position.X) - buttonDrawPosition.X; // Left to right
                int drawRelativeY = -(this.Position.Y) - buttonDrawPosition.Y; // Top to bottom

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

        public void DrawPreparedSkillOverlay(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            RenderParameters renderParameters,
            int currentTime)
        {
            if (_getPreparedSkillOverlayStatus == null)
            {
                return;
            }

            StatusBarPreparedSkillRenderData preparedSkill = _getPreparedSkillOverlayStatus(currentTime);
            if (preparedSkill == null || preparedSkill.Surface != PreparedSkillHudSurface.World)
            {
                return;
            }

            if (IsDragonPreparedSkillOverlay(preparedSkill))
            {
                return;
            }

            Vector2 anchor = new Vector2(
                preparedSkill.WorldAnchor.X - mapShiftX + centerX,
                preparedSkill.WorldAnchor.Y - mapShiftY + centerY);
            DrawPreparedSkillBar(sprite, anchor, currentTime, preparedSkill, anchorIsWorldPosition: true);
        }

        public void DrawPreparedSkillWorldOverlay(
            SpriteBatch sprite,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            StatusBarPreparedSkillRenderData preparedSkill)
        {
            if (preparedSkill == null || preparedSkill.Surface != PreparedSkillHudSurface.World)
            {
                return;
            }

            Vector2 anchor = new Vector2(
                preparedSkill.WorldAnchor.X - mapShiftX + centerX,
                preparedSkill.WorldAnchor.Y - mapShiftY + centerY);
            DrawPreparedSkillBar(sprite, anchor, currentTime, preparedSkill, anchorIsWorldPosition: true);
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
                this.Position.X + _statusBarLeftBaseOffset.X,
                this.Position.Y + _statusBarLeftBaseOffset.Y);
            Vector2 basePosGauge = new Vector2(
                this.Position.X + _statusBarGaugeBaseOffset.X,
                this.Position.Y + _statusBarGaugeBaseOffset.Y);

            UpdateWarningFlashState(stats, currentTime);

            // Draw gauge bars first (under the text)
            DrawGaugeBars(sprite, stats, basePosGauge, currentTime);
            DrawBuffTray(sprite, renderParameters, currentTime);
            DrawCooldownTray(sprite, renderParameters, currentTime);
            DrawPreparedSkillBar(sprite, basePosGauge, currentTime);

            // Skip text rendering if no font
            if (!HasStatusBarTextRenderer())
                return;

            // Draw character info section (left side) - use basePosLeft
            string levelText = stats.Level.ToString();
            Vector2 levelPos = GetClientLevelTextPosition(basePosLeft, levelText);
            if (_useLevelBitmapFont)
            {
                DrawDigitBitmapString(sprite, levelText, levelPos, _levelDigitTextures, _levelDigitOrigins, 1.0f);
            }
            else
            {
                DrawTextWithShadow(sprite, levelText, levelPos, Color.White, Color.Black);
            }

            // Job name
            Vector2 jobPos = SnapToPixel(basePosLeft + JOB_TEXT_POS);
            DrawJobText(sprite, StatusBarLayoutRules.FormatJobLabel(stats.Job), jobPos, _jobTextMaxWidth);

            // Character name - drawn with shadow effect (from IDA: multiple positions for shadow)
            Vector2 namePos = SnapToPixel(basePosLeft + NAME_TEXT_POS);
            DrawNameText(sprite, StatusBarLayoutRules.FormatNameLabel(stats.Name), namePos, _nameTextMaxWidth);

            // Draw gauge text section (HP/MP/EXP area) - use basePosGauge
            // HP text - format from client: [HP/MaxHP]
            string hpText = $"[{stats.HP}/{stats.MaxHP}]";
            Vector2 hpPos = GetRightAlignedStatusTextPosition(basePosGauge, _hpTextPos, hpText, 1.0f, 0.7f);
            if (_useBitmapFont) {
                DrawBitmapString(sprite, hpText, hpPos, 1.0f);
            } else {
                DrawTextWithShadow(sprite, hpText, hpPos, Color.White, Color.Black, 0.7f);
            }

            // MP text - format from client: [MP/MaxMP]
            string mpText = $"[{stats.MP}/{stats.MaxMP}]";
            Vector2 mpPos = GetRightAlignedStatusTextPosition(basePosGauge, _mpTextPos, mpText, 1.0f, 0.7f);
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
            Vector2 expPos = GetRightAlignedStatusTextPosition(basePosGauge, _expTextPos, expText, 1.0f, 0.7f);
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

                Texture2D iconTexture = ResolveBuffIconTexture(
                    buffEntry.IconTexture,
                    buffEntry.IconKey,
                    buffEntry.UseTemporaryStatViewArtworkOnly);

                if (iconTexture != null)
                {
                    sprite.Draw(iconTexture, iconRect, Color.White);
                }

                DrawTemporaryStatViewShadow(sprite, iconRect, buffEntry, currentTime);

                if (buffEntry.IsAlerting)
                {
                    DrawBuffAlertOverlay(sprite, iconRect, buffEntry, currentTime);
                }

                bool hasCounterText = !string.IsNullOrWhiteSpace(buffEntry.CounterText);
                if (!HasStatusBarTextRenderer() || (buffEntry.RemainingMs <= 0 && !hasCounterText))
                {
                    continue;
                }

                string remainingText = hasCounterText
                    ? buffEntry.CounterText
                    : Math.Max(1, (int)Math.Ceiling(buffEntry.RemainingMs / 1000f)).ToString();
                Vector2 textSize = MeasureStatusBarText(remainingText, 0.5f);
                Vector2 textPosition = new Vector2(
                    iconRect.Right - textSize.X - 2,
                    iconRect.Bottom - textSize.Y - 1);

                DrawTextWithShadow(sprite, remainingText, textPosition, Color.White, Color.Black, 0.5f);
            }
        }

        private void DrawTemporaryStatViewShadow(
            SpriteBatch sprite,
            Rectangle iconRect,
            StatusBarBuffRenderData buffEntry,
            int currentTime)
        {
            if (buffEntry?.UseTemporaryStatViewArtworkOnly != true)
            {
                return;
            }

            if (!TryResolveTemporaryStatViewShadowTextureForParity(
                    _temporaryStatViewShadowTextures,
                    buffEntry.ShadowIndex,
                    out Texture2D shadowTexture)
                || shadowTexture == null)
            {
                return;
            }

            byte alpha = ResolveTemporaryStatViewShadowAlphaForParity(
                currentTime,
                buffEntry.ShadowCanvasLastUpdatedTime,
                buffEntry.ShadowCanvasInsertDelayMs,
                buffEntry.ShadowCanvasAlphaStart,
                buffEntry.ShadowCanvasAlphaEnd);
            sprite.Draw(shadowTexture, iconRect, new Color((byte)255, (byte)255, (byte)255, alpha));
        }

        private static bool TryResolveTemporaryStatViewShadowTextureForParity(
            IReadOnlyDictionary<int, Texture2D> shadowTextures,
            int shadowIndex,
            out Texture2D shadowTexture)
        {
            shadowTexture = null;
            if (shadowTextures == null || shadowTextures.Count == 0)
            {
                return false;
            }

            int clampedIndex = Math.Clamp(shadowIndex, 0, 15);
            if (shadowTextures.TryGetValue(clampedIndex, out shadowTexture)
                && shadowTexture != null)
            {
                return true;
            }

            int fallbackIndex = ResolveTemporaryStatViewShadowTextureFallbackIndexForParity(
                clampedIndex,
                shadowTextures.Keys);
            return shadowTextures.TryGetValue(fallbackIndex, out shadowTexture)
                && shadowTexture != null;
        }

        internal static int ResolveTemporaryStatViewShadowTextureFallbackIndexForParity(
            int shadowIndex,
            IEnumerable<int> availableShadowIndexes)
        {
            if (availableShadowIndexes == null)
            {
                return Math.Clamp(shadowIndex, 0, 15);
            }

            HashSet<int> available = new HashSet<int>(
                availableShadowIndexes
                    .Where(index => index >= 0 && index <= 15));
            int clampedIndex = Math.Clamp(shadowIndex, 0, 15);
            if (available.Contains(clampedIndex))
            {
                return clampedIndex;
            }

            // WZ v95 exposes only UI/UIWindow(.2).img/TemporaryStatView/1.
            // Keep the client SetLeft shadow-index metadata, but reuse the
            // resident authored canvas instead of suppressing the layer.
            if (available.Contains(1))
            {
                return 1;
            }

            return available.Count == 0
                ? clampedIndex
                : available.Min();
        }

        internal static byte ResolveTemporaryStatViewShadowAlphaForParity(
            int currentTime,
            int shadowCanvasLastUpdatedTime,
            int insertDelayMs,
            int alphaStart,
            int alphaEnd)
        {
            int boundedStart = Math.Clamp(alphaStart, 0, 255);
            int boundedEnd = Math.Clamp(alphaEnd, 0, 255);
            int boundedDelay = Math.Max(0, insertDelayMs);
            if (shadowCanvasLastUpdatedTime == int.MinValue || boundedDelay == 0)
            {
                return (byte)boundedEnd;
            }

            int elapsed = Math.Max(0, unchecked(currentTime - shadowCanvasLastUpdatedTime));
            if (elapsed >= boundedDelay)
            {
                return (byte)boundedEnd;
            }

            float progress = elapsed / (float)boundedDelay;
            return (byte)Math.Clamp(
                (int)MathF.Round(boundedStart + ((boundedEnd - boundedStart) * progress)),
                0,
                255);
        }

        private void DrawBuffAlertOverlay(
            SpriteBatch sprite,
            Rectangle iconRect,
            StatusBarBuffRenderData buffEntry,
            int currentTime)
        {
            if (_pixelTexture == null)
            {
                return;
            }

            (byte fillAlpha, byte borderAlpha) = ResolveBuffAlertOverlayPulseAlphasForParity(
                currentTime,
                buffEntry?.LowDurabilityAlertStartTime ?? int.MinValue,
                buffEntry?.LowDurabilityAlertSequence ?? 0);
            Color fillColor = new Color(255, 118, 64, (int)fillAlpha);
            Color borderColor = new Color(255, 204, 96, (int)borderAlpha);

            sprite.Draw(_pixelTexture, iconRect, fillColor);
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.X, iconRect.Y, iconRect.Width, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.X, iconRect.Bottom - 1, iconRect.Width, 1), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.X, iconRect.Y, 1, iconRect.Height), borderColor);
            sprite.Draw(_pixelTexture, new Rectangle(iconRect.Right - 1, iconRect.Y, 1, iconRect.Height), borderColor);
        }

        internal static (byte FillAlpha, byte BorderAlpha) ResolveBuffAlertOverlayPulseAlphasForParity(
            int currentTime,
            int lowDurabilityAlertStartTime,
            int lowDurabilityAlertSequence)
        {
            const int pulsePeriodMs = 180;
            bool hasAlertStartTime = lowDurabilityAlertStartTime != int.MinValue;
            int phaseSeedTick;
            if (hasAlertStartTime)
            {
                phaseSeedTick = Math.Max(0, unchecked(currentTime - lowDurabilityAlertStartTime));
            }
            else
            {
                // When low-bucket entry time is unavailable, keep existing cadence
                // but desynchronize by alert sequence so overlapping alerts do not flash
                // in lockstep.
                phaseSeedTick = currentTime + (Math.Max(0, lowDurabilityAlertSequence - 1) * pulsePeriodMs);
            }

            int phase = Math.Abs((phaseSeedTick / pulsePeriodMs) % 2);
            return phase == 0
                ? ((byte)82, (byte)220)
                : ((byte)46, (byte)168);
        }

        private void DrawHoveredBuffTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (!HasStatusBarTextRenderer() || !TryGetHoveredBuffEntry(out StatusBarBuffRenderData buffEntry, out _))
            {
                return;
            }

            Point mousePosition = Mouse.GetState().Position;

            IReadOnlyList<string> temporaryStatDisplayNames = buffEntry.TemporaryStatDisplayNames != null
                && buffEntry.TemporaryStatDisplayNames.Count > 0
                ? buffEntry.TemporaryStatDisplayNames
                : buffEntry.TemporaryStatLabels;
            string familyText = !string.IsNullOrWhiteSpace(buffEntry.FamilyDisplayName)
                ? $"Buff Type: {buffEntry.FamilyDisplayName}"
                : string.Empty;
            string temporaryStatText = temporaryStatDisplayNames != null && temporaryStatDisplayNames.Count > 0
                ? $"Temp Stats: {string.Join(", ", temporaryStatDisplayNames)}"
                : string.Empty;
            string secondaryLine = string.IsNullOrWhiteSpace(familyText)
                ? temporaryStatText
                : string.IsNullOrWhiteSpace(temporaryStatText)
                    ? familyText
                    : $"{familyText} | {temporaryStatText}";
            Texture2D iconTexture = ResolveBuffIconTexture(
                buffEntry.IconTexture,
                buffEntry.IconKey,
                buffEntry.UseTemporaryStatViewArtworkOnly);

            DrawStatusBarSkillTooltip(
                sprite,
                mousePosition,
                renderWidth,
                renderHeight,
                SanitizeTooltipText(buffEntry.SkillName),
                ResolveBuffTooltipStateLine(buffEntry),
                secondaryLine,
                SanitizeTooltipText(buffEntry.Description),
                iconTexture,
                useClientSkillLayout: false);
        }

        private static string ResolveBuffTooltipStateLine(StatusBarBuffRenderData buffEntry)
        {
            if (!string.IsNullOrWhiteSpace(buffEntry?.TooltipStateText))
            {
                return buffEntry.TooltipStateText;
            }

            return buffEntry?.RemainingMs > 0
                ? $"Time Left: {Math.Max(1, (int)Math.Ceiling(buffEntry.RemainingMs / 1000f))} sec"
                : "Time Left: --";
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

        private bool TryGetHoveredCooldownEntry(
            out StatusBarCooldownRenderData cooldownEntry,
            out Rectangle iconRect,
            out SkillTooltipAnchorOwner anchorOwner)
        {
            Point mousePosition = new Point(Mouse.GetState().X, Mouse.GetState().Y);
            for (int i = _cooldownTooltipHitTargets.Count - 1; i >= 0; i--)
            {
                StatusBarCooldownTooltipHitTarget hitTarget = _cooldownTooltipHitTargets[i];
                if (!hitTarget.IconRect.Contains(mousePosition))
                {
                    continue;
                }

                cooldownEntry = hitTarget.CooldownEntry;
                iconRect = hitTarget.IconRect;
                anchorOwner = hitTarget.AnchorOwner;
                return cooldownEntry != null;
            }

            cooldownEntry = null;
            iconRect = Rectangle.Empty;
            anchorOwner = SkillTooltipAnchorOwner.StatusBarCooldownTray;
            return false;
        }

        private void DrawBuffIconFrame(SpriteBatch sprite, Rectangle iconRect)
        {
            if (_temporaryStatViewTexture != null)
            {
                sprite.Draw(_temporaryStatViewTexture, iconRect, Color.White);
                return;
            }

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

        private Texture2D ResolveBuffIconTexture(
            Texture2D preferredTexture,
            string iconKey,
            bool useTemporaryStatViewArtworkOnly = false)
        {
            if (preferredTexture != null)
            {
                return preferredTexture;
            }

            if (useTemporaryStatViewArtworkOnly)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(iconKey))
            {
                if (_buffIconTextures.TryGetValue(iconKey, out Texture2D iconTexture))
                {
                    return iconTexture;
                }

                string suffixedKey = iconKey.EndsWith("/0", StringComparison.OrdinalIgnoreCase)
                    ? iconKey
                    : $"{iconKey}/0";
                if (_buffIconTextures.TryGetValue(suffixedKey, out iconTexture))
                {
                    return iconTexture;
                }
            }

            if (_buffIconTextures.TryGetValue("united/buff", out Texture2D genericTexture))
            {
                return genericTexture;
            }

            _buffIconTextures.TryGetValue("united/buff/0", out genericTexture);
            return genericTexture;
        }

        private void DrawCooldownTray(SpriteBatch sprite, RenderParameters renderParameters, int currentTime)
        {
            _cooldownTooltipHitTargets.Clear();
            int primaryTrayBottom = DrawPrimaryCooldownTray(
                sprite,
                renderParameters,
                currentTime);
            int offBarTopMargin = Math.Max(OFFBAR_COOLDOWN_TRAY_TOP_MARGIN, primaryTrayBottom + 4);
            DrawCooldownTrayGroup(
                sprite,
                renderParameters,
                currentTime,
                _getOffBarCooldownStatus,
                OFFBAR_COOLDOWN_TRAY_COLUMNS,
                OFFBAR_COOLDOWN_TRAY_ROWS,
                OFFBAR_COOLDOWN_ICON_SIZE,
                OFFBAR_COOLDOWN_ICON_SPACING,
                OFFBAR_COOLDOWN_TRAY_RIGHT_MARGIN,
                offBarTopMargin,
                SkillManager.CooldownMaskSurface.StatusBarOffBarTray,
                SkillTooltipAnchorOwner.StatusBarOffBarCooldownTray);
        }

        private int DrawPrimaryCooldownTray(
            SpriteBatch sprite,
            RenderParameters renderParameters,
            int currentTime)
        {
            if (_getCooldownStatus == null)
            {
                return COOLDOWN_TRAY_TOP_MARGIN;
            }

            IReadOnlyList<StatusBarCooldownRenderData> cooldownEntries = _getCooldownStatus(currentTime);
            if (cooldownEntries == null || cooldownEntries.Count == 0)
            {
                return COOLDOWN_TRAY_TOP_MARGIN;
            }

            Point trayTopLeft = ResolveClientShortcutTrayTopLeft(renderParameters.RenderWidth);
            int maxBottom = trayTopLeft.Y;
            for (int entryIndex = 0; entryIndex < cooldownEntries.Count; entryIndex++)
            {
                StatusBarCooldownRenderData cooldownEntry = cooldownEntries[entryIndex];
                if (!ShouldDrawStatusBarShortcutCooldownSlotForClientParity(cooldownEntry))
                {
                    continue;
                }

                Rectangle iconRect = ResolveClientShortcutSlotRect(
                    trayTopLeft.X,
                    trayTopLeft.Y,
                    cooldownEntry.ShortcutSlotIndex);
                maxBottom = Math.Max(maxBottom, iconRect.Bottom);

                RegisterCooldownTooltipHitTarget(
                    cooldownEntry,
                    iconRect,
                    SkillTooltipAnchorOwner.StatusBarCooldownTray);

                DrawBuffIconFrame(sprite, iconRect);
                if (cooldownEntry.IconTexture != null)
                {
                    sprite.Draw(cooldownEntry.IconTexture, iconRect, Color.White);
                }

                    if (!cooldownEntry.SuppressProgressOverlay)
                    {
                        DrawCooldownMask(
                            sprite,
                            iconRect,
                            cooldownEntry.MaskFrameIndex,
                            cooldownEntry.MaskSurface);
                    }

                if (!HasStatusBarTextRenderer() || !ShouldDrawCooldownCounterTextForClientParity(cooldownEntry))
                {
                    continue;
                }

                string remainingText = string.IsNullOrWhiteSpace(cooldownEntry.CounterText)
                    ? Math.Max(1, (int)Math.Ceiling(cooldownEntry.RemainingMs / 1000f)).ToString()
                    : cooldownEntry.CounterText;
                Vector2 textSize = MeasureStatusBarText(remainingText, 0.5f);
                Vector2 textPosition = new Vector2(
                    iconRect.Right - textSize.X - 2,
                    iconRect.Bottom - textSize.Y - 1);

                DrawTextWithShadow(sprite, remainingText, textPosition, Color.White, Color.Black, 0.5f);
            }

            return maxBottom;
        }

        internal static bool ShouldDrawStatusBarShortcutCooldownSlotForClientParity(
            StatusBarCooldownRenderData cooldownEntry)
        {
            return cooldownEntry != null
                   && cooldownEntry.ShortcutSlotIndex >= 0
                   && cooldownEntry.ShortcutSlotIndex < CLIENT_SHORTCUT_SLOT_COUNT;
        }

        internal static Point ResolveClientShortcutTrayTopLeft(int renderWidth)
        {
            return new Point(
                renderWidth - COOLDOWN_TRAY_RIGHT_MARGIN - CLIENT_SHORTCUT_TRAY_WIDTH,
                COOLDOWN_TRAY_TOP_MARGIN);
        }

        internal static Rectangle ResolveClientShortcutSlotRect(int trayLeft, int trayTop, int slotIndex)
        {
            int clampedSlotIndex = Math.Clamp(slotIndex, 0, CLIENT_SHORTCUT_SLOT_COUNT - 1);
            int column = clampedSlotIndex % CLIENT_SHORTCUT_SLOT_COLUMNS;
            int row = clampedSlotIndex / CLIENT_SHORTCUT_SLOT_COLUMNS;
            return new Rectangle(
                trayLeft + CLIENT_SHORTCUT_TRAY_SLOT_LEFT + (column * CLIENT_SHORTCUT_SLOT_STEP),
                trayTop + CLIENT_SHORTCUT_TRAY_SLOT_TOP + (row * CLIENT_SHORTCUT_SLOT_STEP),
                CLIENT_SHORTCUT_SLOT_SIZE,
                CLIENT_SHORTCUT_SLOT_SIZE);
        }

        private int DrawCooldownTrayGroup(
            SpriteBatch sprite,
            RenderParameters renderParameters,
            int currentTime,
            Func<int, IReadOnlyList<StatusBarCooldownRenderData>> provider,
            int columns,
            int rows,
            int iconSize,
            int iconSpacing,
            int rightMargin,
            int topMargin,
            SkillManager.CooldownMaskSurface fallbackMaskSurface,
            SkillTooltipAnchorOwner anchorOwner)
        {
            if (provider == null)
            {
                return topMargin;
            }

            IReadOnlyList<StatusBarCooldownRenderData> cooldownEntries = provider(currentTime);
            if (cooldownEntries == null || cooldownEntries.Count == 0)
            {
                return topMargin;
            }

            int maxBottom = topMargin;
            int maxVisibleEntries = Math.Max(1, columns * rows);
            int visibleCount = Math.Min(cooldownEntries.Count, maxVisibleEntries);

            for (int row = 0; row < rows; row++)
            {
                int rowStartIndex = row * columns;
                if (rowStartIndex >= visibleCount)
                {
                    break;
                }

                int entriesInRow = Math.Min(columns, visibleCount - rowStartIndex);
                int rowWidth = (iconSize * entriesInRow) + (iconSpacing * Math.Max(0, entriesInRow - 1));
                int startX = renderParameters.RenderWidth - rightMargin - rowWidth;

                for (int col = 0; col < entriesInRow; col++)
                {
                    int entryIndex = rowStartIndex + col;
                    StatusBarCooldownRenderData cooldownEntry = cooldownEntries[entryIndex];
                    Rectangle iconRect = new Rectangle(
                        startX + col * (iconSize + iconSpacing),
                        topMargin + row * (iconSize + iconSpacing),
                        iconSize,
                        iconSize);
                    maxBottom = Math.Max(maxBottom, iconRect.Bottom);

                    RegisterCooldownTooltipHitTarget(cooldownEntry, iconRect, anchorOwner);

                    DrawBuffIconFrame(sprite, iconRect);
                    if (cooldownEntry.IconTexture != null)
                    {
                        sprite.Draw(cooldownEntry.IconTexture, iconRect, Color.White);
                    }

                if (!cooldownEntry.SuppressProgressOverlay)
                {
                    DrawCooldownMask(
                        sprite,
                        iconRect,
                        cooldownEntry.MaskFrameIndex,
                        cooldownEntry.MaskSurface);
                }

                    if (!HasStatusBarTextRenderer() || !ShouldDrawCooldownCounterTextForClientParity(cooldownEntry))
                    {
                        continue;
                    }

                    float textScale = iconSize < BUFF_ICON_SIZE ? 0.45f : 0.5f;
                    string remainingText = string.IsNullOrWhiteSpace(cooldownEntry.CounterText)
                        ? Math.Max(1, (int)Math.Ceiling(cooldownEntry.RemainingMs / 1000f)).ToString()
                        : cooldownEntry.CounterText;
                    Vector2 textSize = MeasureStatusBarText(remainingText, textScale);
                    Vector2 textPosition = new Vector2(
                        iconRect.Right - textSize.X - 2,
                        iconRect.Bottom - textSize.Y - 1);

                    DrawTextWithShadow(sprite, remainingText, textPosition, Color.White, Color.Black, textScale);
                }
            }

            return maxBottom;
        }

        private void DrawHoveredCooldownTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (!HasStatusBarTextRenderer()
                || !TryGetHoveredCooldownEntry(
                    out StatusBarCooldownRenderData cooldownEntry,
                    out _,
                    out SkillTooltipAnchorOwner anchorOwner))
            {
                return;
            }

            Point mousePosition = Mouse.GetState().Position;

            DrawStatusBarSkillTooltip(
                sprite,
                mousePosition,
                renderWidth,
                renderHeight,
                SanitizeTooltipText(cooldownEntry.SkillName),
                BuildCooldownTooltipStatusLineMarkup(cooldownEntry),
                BuildCooldownTooltipSecondaryLineMarkup(cooldownEntry),
                SanitizeTooltipText(cooldownEntry.Description),
                cooldownEntry.IconTexture,
                useClientSkillLayout: true,
                anchorOwner: anchorOwner);
        }

        private void RegisterCooldownTooltipHitTarget(
            StatusBarCooldownRenderData cooldownEntry,
            Rectangle iconRect,
            SkillTooltipAnchorOwner anchorOwner)
        {
            if (cooldownEntry == null)
            {
                return;
            }

            _cooldownTooltipHitTargets.Add(new StatusBarCooldownTooltipHitTarget
            {
                IconRect = iconRect,
                CooldownEntry = cooldownEntry,
                AnchorOwner = anchorOwner
            });
        }

        private void DrawCooldownMask(
            SpriteBatch sprite,
            Rectangle iconRect,
            int frameIndex,
            SkillManager.CooldownMaskSurface maskSurface)
        {
            if (_cooldownMaskTextures.Length > 0)
            {
                int resolvedFrameIndex = Math.Clamp(frameIndex, 0, _cooldownMaskTextures.Length - 1);
                Texture2D maskTexture = _cooldownMaskTextures[resolvedFrameIndex];
                if (maskTexture != null)
                {
                    sprite.Draw(maskTexture, iconRect, Color.White);
                    return;
                }
            }

            if (_pixelTexture == null)
            {
                return;
            }

            float remainingProgress = SkillManager.ResolveCooldownMaskFallbackFillRatio(
                frameIndex,
                maskSurface);
            int overlayHeight = Math.Clamp((int)Math.Ceiling(iconRect.Height * remainingProgress), 0, iconRect.Height);
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
            DrawPreparedSkillBar(sprite, basePosGauge, currentTime, preparedSkill, anchorIsWorldPosition: false);
        }

        private void DrawPreparedSkillBar(
            SpriteBatch sprite,
            Vector2 basePosGauge,
            int currentTime,
            StatusBarPreparedSkillRenderData preparedSkill,
            bool anchorIsWorldPosition)
        {
            if (preparedSkill == null)
            {
                return;
            }

            PreparedSkillHudProfile hudProfile = ResolvePreparedSkillHudProfile(preparedSkill);
            float progress = ResolvePreparedSkillHudProgress(preparedSkill, hudProfile);
            StatusBarKeyDownBarTextures textures = ResolveKeyDownBarTextures(hudProfile.SkinKey ?? preparedSkill.SkinKey);
            Rectangle barRect = GetKeyDownBarRectangle(basePosGauge, textures, anchorIsWorldPosition);

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

            if (preparedSkill.ShowText)
            {
                DrawPreparedSkillHudText(sprite, preparedSkill, hudProfile, barRect, progress);
            }
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
            if (!HasStatusBarTextRenderer() || preparedSkill == null)
            {
                return;
            }

            string title = SanitizeTooltipText(preparedSkill.SkillName);
            if (!string.IsNullOrWhiteSpace(title))
            {
                Vector2 titleSize = MeasureStatusBarText(title, 1.0f);
                int lineSpacing = ResolveStatusBarLineSpacing();
                Vector2 titlePos = new Vector2(
                    barRect.X + Math.Max(0f, (barRect.Width - titleSize.X) * 0.5f),
                    barRect.Y - lineSpacing - PREPARED_SKILL_LABEL_GAP);
                DrawTooltipText(sprite, title, titlePos, new Color(255, 238, 155));
            }

            string statusText = PreparedSkillHudTextResolver.BuildStatusText(preparedSkill, hudProfile.GaugeDurationMs, progress);
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return;
            }

            Vector2 statusSize = MeasureStatusBarText(statusText, 1.0f);
            int statusLineSpacing = ResolveStatusBarLineSpacing();
            Vector2 statusPos = new Vector2(
                barRect.Right - statusSize.X,
                barRect.Y - statusLineSpacing - PREPARED_SKILL_LABEL_GAP);
            DrawTooltipText(sprite, statusText, statusPos, Color.White);
        }

        private static float ResolvePreparedSkillHudProgress(StatusBarPreparedSkillRenderData preparedSkill, PreparedSkillHudProfile hudProfile)
        {
            return PreparedSkillHudTextResolver.ResolveProgress(preparedSkill, hudProfile.GaugeDurationMs);
        }

        private static bool IsDragonPreparedSkillOverlay(StatusBarPreparedSkillRenderData preparedSkill)
        {
            return preparedSkill?.SkillId is 22121000 or 22151001;
        }

        private static PreparedSkillHudProfile ResolvePreparedSkillHudProfile(StatusBarPreparedSkillRenderData preparedSkill)
        {
            if (preparedSkill == null)
            {
                return PreparedSkillHudProfile.Default;
            }

            return new PreparedSkillHudProfile(preparedSkill.SkinKey, preparedSkill.GaugeDurationMs, preparedSkill.TextVariant);
        }

        private void DrawStatusBarSkillTooltip(
            SpriteBatch sprite,
            Point anchorPoint,
            int renderWidth,
            int renderHeight,
            string title,
            string statusLine,
            string secondaryLine,
            string description,
            Texture2D iconTexture,
            bool useClientSkillLayout,
            SkillTooltipAnchorOwner anchorOwner = SkillTooltipAnchorOwner.LegacyPanel)
        {
            int tooltipWidth = useClientSkillLayout
                ? SkillTooltipFrameLayout.ClientTooltipWidth
                : ResolveTooltipWidth();
            int titleXOffset = useClientSkillLayout
                ? SkillTooltipFrameLayout.ClientTooltipTitleX
                : TOOLTIP_PADDING;
            int titleYOffset = useClientSkillLayout
                ? SkillTooltipFrameLayout.ClientTooltipTitleY
                : TOOLTIP_PADDING;
            int iconXOffset = useClientSkillLayout
                ? SkillTooltipFrameLayout.ClientTooltipIconX
                : TOOLTIP_PADDING;
            int textXOffset = useClientSkillLayout
                ? SkillTooltipFrameLayout.ClientTooltipTextX
                : TOOLTIP_PADDING + BUFF_ICON_SIZE + TOOLTIP_ICON_GAP;
            float titleWidth = useClientSkillLayout
                ? tooltipWidth - SkillTooltipFrameLayout.ClientTooltipTitleX - SkillTooltipFrameLayout.ClientTooltipRightPadding
                : tooltipWidth - (TOOLTIP_PADDING * 2);
            float sectionWidth = useClientSkillLayout
                ? tooltipWidth - SkillTooltipFrameLayout.ClientTooltipTextX - SkillTooltipFrameLayout.ClientTooltipRightPadding
                : tooltipWidth - textXOffset - TOOLTIP_PADDING;
            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            StatusBarTooltipLine[] wrappedStatus = WrapTooltipText(statusLine, sectionWidth, Color.White);
            StatusBarTooltipLine[] wrappedSecondary = WrapTooltipText(
                secondaryLine,
                sectionWidth,
                useClientSkillLayout ? new Color(255, 214, 140) : new Color(181, 224, 255));
            string[] wrappedDescription = WrapTooltipText(description, sectionWidth);

            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float statusHeight = MeasureLinesHeight(wrappedStatus);
            float secondaryHeight = MeasureLinesHeight(wrappedSecondary);
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float contentHeight = 0f;
            if (statusHeight > 0f)
            {
                contentHeight += statusHeight;
            }

            if (secondaryHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? 2f : 0f) + secondaryHeight;
            }

            if (descriptionHeight > 0f)
            {
                contentHeight += (contentHeight > 0f ? TOOLTIP_SECTION_GAP : 0f) + descriptionHeight;
            }

            float iconBlockHeight = Math.Max(BUFF_ICON_SIZE, contentHeight);
            float baseContentY = useClientSkillLayout
                ? Math.Max(
                    SkillTooltipFrameLayout.ClientTooltipTextY,
                    SkillTooltipFrameLayout.ClientTooltipTitleY + titleHeight + 2f)
                : TOOLTIP_PADDING + titleHeight + TOOLTIP_TITLE_GAP;
            int tooltipHeight = useClientSkillLayout
                ? Math.Max(
                    SkillTooltipFrameLayout.ClientTooltipBaseHeight,
                    (int)Math.Ceiling(Math.Max(baseContentY + BUFF_ICON_SIZE, baseContentY + iconBlockHeight) + TOOLTIP_PADDING))
                : (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + TOOLTIP_TITLE_GAP + iconBlockHeight);
            Point tooltipAnchor = SkillTooltipFrameLayout.ResolveTooltipAnchorFromCursor(anchorPoint, anchorOwner);
            Rectangle tooltipRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                stackalloc[] { 1, 0, 2 },
                out int tooltipFrameIndex);

            DrawTooltipBackground(sprite, tooltipRect, tooltipFrameIndex);

            float titleY = tooltipRect.Y + titleYOffset;
            DrawTooltipLines(
                sprite,
                wrappedTitle,
                tooltipRect.X + titleXOffset,
                titleY,
                useClientSkillLayout ? new Color(255, 220, 120) : new Color(255, 238, 155));

            int contentY = tooltipRect.Y + (int)Math.Ceiling(baseContentY);
            int iconX = tooltipRect.X + iconXOffset;
            if (iconTexture != null)
            {
                sprite.Draw(iconTexture, new Rectangle(iconX, contentY, BUFF_ICON_SIZE, BUFF_ICON_SIZE), Color.White);
            }

            int textX = tooltipRect.X + textXOffset;
            float sectionY = contentY;
            if (statusHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedStatus, textX, sectionY);
                sectionY += statusHeight;
            }

            if (secondaryHeight > 0f)
            {
                sectionY += statusHeight > 0f ? 2f : 0f;
                DrawTooltipLines(sprite, wrappedSecondary, textX, sectionY);
                sectionY += secondaryHeight;
            }

            if (descriptionHeight > 0f)
            {
                sectionY += (statusHeight > 0f || secondaryHeight > 0f) ? TOOLTIP_SECTION_GAP : 0f;
                DrawTooltipLines(
                    sprite,
                    wrappedDescription,
                    textX,
                    sectionY,
                    useClientSkillLayout ? Color.White : new Color(219, 219, 219));
            }
        }

        private int ResolveTooltipWidth()
        {
            return SkillTooltipFrameLayout.ClientTooltipWidth;
        }

        private Rectangle ResolveTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            ReadOnlySpan<int> framePreference,
            out int tooltipFrameIndex)
        {
            SkillTooltipFrameLayout.FrameGeometry[] frameGeometries =
                SkillTooltipFrameLayout.BuildFrameGeometries(_tooltipFrames, _tooltipFrameOrigins);
            return SkillTooltipFrameLayout.ResolveTooltipRect(
                anchorPoint,
                tooltipWidth,
                tooltipHeight,
                renderWidth,
                renderHeight,
                frameGeometries,
                framePreference,
                TOOLTIP_PADDING,
                out tooltipFrameIndex);
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect, int tooltipFrameIndex)
        {
            SkillTooltipFrameLayout.DrawTooltipFrameOrPlainBackground(
                sprite,
                _tooltipFrames,
                tooltipFrameIndex,
                _pixelTexture,
                rect);
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

                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * ResolveStatusBarLineSpacing())), color);
            }
        }

        private void DrawTooltipLines(SpriteBatch sprite, StatusBarTooltipLine[] lines, int x, float y)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                float drawX = x;
                StatusBarTooltipLine line = lines[i];
                if (line?.Runs == null)
                {
                    continue;
                }

                for (int runIndex = 0; runIndex < line.Runs.Count; runIndex++)
                {
                    StatusBarTooltipTextRun run = line.Runs[runIndex];
                    if (string.IsNullOrEmpty(run.Text))
                    {
                        continue;
                    }

                    Vector2 position = new Vector2(drawX, y + (i * ResolveStatusBarLineSpacing()));
                    DrawTooltipText(sprite, run.Text, position, run.Color);
                    drawX += MeasureStatusBarText(run.Text, 1.0f).X;
                }
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 snappedPosition = SnapToPixel(position);
            DrawStatusBarText(sprite, text, snappedPosition + Vector2.One, Color.Black, 1.0f);
            DrawStatusBarText(sprite, text, snappedPosition, color, 1.0f);
        }

        private float MeasureLongestLine(string[] lines)
        {
            float width = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    width = Math.Max(width, MeasureStatusBarText(lines[i], 1.0f).X);
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

            return nonEmptyLines > 0 ? nonEmptyLines * ResolveStatusBarLineSpacing() : 0f;
        }

        private float MeasureLinesHeight(StatusBarTooltipLine[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                return 0f;
            }

            int nonEmptyLines = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null && !lines[i].IsEmpty)
                {
                    nonEmptyLines++;
                }
            }

            return nonEmptyLines > 0 ? nonEmptyLines * ResolveStatusBarLineSpacing() : 0f;
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (!HasStatusBarTextRenderer() || string.IsNullOrWhiteSpace(text))
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
                    if (!string.IsNullOrEmpty(currentLine) && MeasureStatusBarText(candidate, 1.0f).X > maxWidth)
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

        private StatusBarTooltipLine[] WrapTooltipText(string text, float maxWidth, Color baseColor)
        {
            if (!HasStatusBarTextRenderer() || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<StatusBarTooltipLine>();
            }

            List<StatusBarTooltipToken> tokens = TokenizeTooltipText(text, baseColor);
            List<StatusBarTooltipLine> lines = new();
            StatusBarTooltipLine currentLine = new();
            float currentWidth = 0f;

            foreach (StatusBarTooltipToken token in tokens)
            {
                if (token.IsNewLine)
                {
                    lines.Add(currentLine);
                    currentLine = new StatusBarTooltipLine();
                    currentWidth = 0f;
                    continue;
                }

                if (token.IsWhitespace)
                {
                    if (currentLine.Runs.Count == 0)
                    {
                        continue;
                    }

                    float whitespaceWidth = MeasureStatusBarText(token.Text, 1.0f).X;
                    if (currentWidth + whitespaceWidth > maxWidth)
                    {
                        lines.Add(currentLine);
                        currentLine = new StatusBarTooltipLine();
                        currentWidth = 0f;
                        continue;
                    }

                    currentLine.Append(token.Text, token.Color);
                    currentWidth += whitespaceWidth;
                    continue;
                }

                float tokenWidth = MeasureStatusBarText(token.Text, 1.0f).X;
                if (currentLine.Runs.Count > 0 && currentWidth + tokenWidth > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new StatusBarTooltipLine();
                    currentWidth = 0f;
                }

                currentLine.Append(token.Text, token.Color);
                currentWidth += tokenWidth;
            }

            if (!currentLine.IsEmpty || lines.Count == 0)
            {
                lines.Add(currentLine);
            }

            return lines.ToArray();
        }

        private List<StatusBarTooltipToken> TokenizeTooltipText(string text, Color baseColor)
        {
            List<StatusBarTooltipToken> tokens = new();
            foreach ((string segmentText, Color segmentColor) in ParseTooltipSegments(text, baseColor))
            {
                int index = 0;
                while (index < segmentText.Length)
                {
                    char ch = segmentText[index];
                    if (ch == '\n')
                    {
                        tokens.Add(StatusBarTooltipToken.NewLine());
                        index++;
                        continue;
                    }

                    int start = index;
                    bool whitespace = char.IsWhiteSpace(ch);
                    while (index < segmentText.Length
                        && segmentText[index] != '\n'
                        && char.IsWhiteSpace(segmentText[index]) == whitespace)
                    {
                        index++;
                    }

                    string tokenText = segmentText[start..index];
                    if (whitespace)
                    {
                        tokenText = tokenText.Replace('\t', ' ');
                    }

                    if (tokenText.Length > 0)
                    {
                        tokens.Add(new StatusBarTooltipToken(tokenText, segmentColor, isWhitespace: whitespace, isNewLine: false));
                    }
                }
            }

            return tokens;
        }

        private IEnumerable<(string Text, Color Color)> ParseTooltipSegments(string text, Color baseColor)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            StringBuilder builder = new();
            int index = 0;
            while (index < text.Length)
            {
                if (text[index] == '#' && index + 1 < text.Length)
                {
                    if (text[index + 1] == '#')
                    {
                        builder.Append('#');
                        index += 2;
                        continue;
                    }

                    int closingIndex = text.IndexOf('#', index + 2);
                    if (closingIndex > index + 2)
                    {
                        if (builder.Length > 0)
                        {
                            yield return (builder.ToString(), baseColor);
                            builder.Clear();
                        }

                        char marker = char.ToLowerInvariant(text[index + 1]);
                        string segment = text.Substring(index + 2, closingIndex - index - 2);
                        yield return (segment, ResolveTooltipMarkerColor(marker, baseColor));
                        index = closingIndex + 1;
                        continue;
                    }
                }

                builder.Append(text[index]);
                index++;
            }

            if (builder.Length > 0)
            {
                yield return (builder.ToString(), baseColor);
            }
        }

        private static Color ResolveTooltipMarkerColor(char marker, Color baseColor)
        {
            return marker switch
            {
                'c' => new Color(255, 214, 140),
                'b' => new Color(130, 190, 255),
                'g' => new Color(160, 255, 160),
                'r' => new Color(255, 150, 150),
                _ => baseColor
            };
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

        internal static string BuildCooldownTooltipStatusLineMarkup(StatusBarCooldownRenderData cooldownEntry)
        {
            if (cooldownEntry == null)
            {
                return string.Empty;
            }

            return SkillCooldownTooltipText.FormatTooltipStateLineMarkup(
                cooldownEntry.RemainingMs,
                cooldownEntry.TooltipStateText);
        }

        internal static string BuildCooldownTooltipSecondaryLineMarkup(StatusBarCooldownRenderData cooldownEntry)
        {
            return cooldownEntry?.TooltipCostLineMarkup ?? string.Empty;
        }

        internal static bool ShouldDrawCooldownCounterTextForClientParity(StatusBarCooldownRenderData cooldownEntry)
        {
            if (cooldownEntry == null || cooldownEntry.SuppressCounterText)
            {
                return false;
            }

            return cooldownEntry.RemainingMs > 0
                   || !string.IsNullOrWhiteSpace(cooldownEntry.CounterText);
        }

        private string ClipTextToWidth(string text, float maxWidth, float scale, ClientTextRasterizer rasterizer = null)
        {
            if (!HasStatusBarTextRenderer() || string.IsNullOrWhiteSpace(text))
            {
                return text ?? string.Empty;
            }

            if (MeasureStatusBarText(text, scale, rasterizer).X <= maxWidth)
            {
                return text;
            }

            for (int length = text.Length - 1; length > 0; length--)
            {
                string candidate = text.Substring(0, length).TrimEnd();
                if (MeasureStatusBarText(candidate, scale, rasterizer).X <= maxWidth)
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private bool CanUsePixelClippedClientText(SpriteBatch sprite)
        {
            return sprite?.GraphicsDevice != null;
        }

        private string ResolveTextForWidth(SpriteBatch sprite, string text, float scale, float? maxWidth, ClientTextRasterizer rasterizer = null)
        {
            if (!maxWidth.HasValue || CanUsePixelClippedClientText(sprite))
            {
                return text;
            }

            return ClipTextToWidth(text, maxWidth.Value, scale, rasterizer);
        }

        private Vector2 GetRightAlignedStatusTextPosition(Vector2 basePos, Vector2 anchorOffset, string text, float bitmapScale, float spriteFontScale)
        {
            float textWidth = MeasureStatusBarTextWidth(text, bitmapScale, spriteFontScale);
            return SnapToPixel(new Vector2(
                basePos.X + anchorOffset.X - textWidth,
                basePos.Y + anchorOffset.Y));
        }

        private float MeasureStatusBarTextWidth(string text, float bitmapScale, float spriteFontScale)
        {
            if (_useBitmapFont)
            {
                return MeasureBitmapString(text, bitmapScale);
            }

            if (!HasStatusBarTextRenderer() || string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            return MeasureStatusBarText(text, spriteFontScale).X;
        }

        private Vector2 GetClientLevelTextPosition(Vector2 basePosLeft, string levelText)
        {
            float clientSlotX = StatusBarLayoutRules.ResolveLevelSlotX(levelText);
            return SnapToPixel(new Vector2(
                basePosLeft.X + clientSlotX,
                basePosLeft.Y + LEVEL_TEXT_POS.Y));
        }

        private static Vector2 SnapToPixel(Vector2 position)
        {
            return new Vector2(
                (float)Math.Round(position.X),
                (float)Math.Round(position.Y));
        }

        private Rectangle GetKeyDownBarRectangle(Vector2 basePosGauge, StatusBarKeyDownBarTextures textures, bool anchorIsWorldPosition = false)
        {
            Texture2D barTexture = textures?.Bar;
            int width = barTexture?.Width ?? 72;
            int height = barTexture?.Height ?? 12;
            Point origin = textures?.BarOrigin ?? KEYDOWN_BAR_DEFAULT_ORIGIN;
            Vector2 anchor = anchorIsWorldPosition
                ? basePosGauge
                : basePosGauge + KEYDOWN_BAR_ANCHOR_POS;
            Vector2 barPos = anchor - origin.ToVector2();
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
            public static PreparedSkillHudProfile Default => new PreparedSkillHudProfile("KeyDownBar", 0, PreparedSkillHudTextVariant.Default);

            public PreparedSkillHudProfile(string skinKey, int gaugeDurationMs, PreparedSkillHudTextVariant textVariant)
            {
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey;
                GaugeDurationMs = gaugeDurationMs;
                TextVariant = textVariant;
            }

            public string SkinKey { get; }
            public int GaugeDurationMs { get; }
            public PreparedSkillHudTextVariant TextVariant { get; }
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
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 snappedPosition = SnapToPixel(position);
            DrawStatusBarText(sprite, text, snappedPosition + new Vector2(-1, 0), shadowColor, scale);
            DrawStatusBarText(sprite, text, snappedPosition + new Vector2(1, 0), shadowColor, scale);
            DrawStatusBarText(sprite, text, snappedPosition + new Vector2(0, -1), shadowColor, scale);
            DrawStatusBarText(sprite, text, snappedPosition + new Vector2(0, 1), shadowColor, scale);
            DrawStatusBarText(sprite, text, snappedPosition, textColor, scale);
            return;

/*
            // Draw shadow at offset positions (similar to client shadow rendering)
            sprite.DrawString(_font, text, position + new Vector2(-1, 0), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(1, 0), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(0, -1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position + new Vector2(0, 1), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Draw main text
            sprite.DrawString(_font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
*/
        }

        private void DrawPlainText(SpriteBatch sprite, string text, Vector2 position, Color textColor, float scale = 1.0f, float? maxWidth = null)
        {
            if (!HasStatusBarTextRenderer() || string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 snappedPosition = SnapToPixel(position);
            string textToDraw = ResolveTextForWidth(sprite, text, scale, maxWidth);
            if (string.IsNullOrEmpty(textToDraw))
            {
                return;
            }

            DrawStatusBarText(sprite, textToDraw, snappedPosition, textColor, scale, maxWidth);
        }

        private void DrawJobText(SpriteBatch sprite, string text, Vector2 position, float? maxWidth = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 snappedPosition = SnapToPixel(position);
            string textToDraw = ResolveTextForWidth(sprite, text, JOB_TEXT_SCALE, maxWidth, _jobTextRasterizer);
            if (string.IsNullOrEmpty(textToDraw))
            {
                return;
            }

            DrawStatusBarText(sprite, textToDraw, snappedPosition, JOB_TEXT_COLOR, JOB_TEXT_SCALE, maxWidth, _jobTextRasterizer);
        }

        private void DrawNameText(SpriteBatch sprite, string text, Vector2 position, float? maxWidth = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 snappedPosition = SnapToPixel(position);
            string textToDraw = ResolveTextForWidth(sprite, text, NAME_TEXT_SCALE, maxWidth, _nameTextRasterizer);
            if (string.IsNullOrEmpty(textToDraw))
            {
                return;
            }

            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(-1, -1), NAME_TEXT_SHADOW_COLOR, NAME_TEXT_SCALE, maxWidth, _nameTextRasterizer);
            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(1, -1), NAME_TEXT_SHADOW_COLOR, NAME_TEXT_SCALE, maxWidth, _nameTextRasterizer);
            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(-1, 1), NAME_TEXT_SHADOW_COLOR, NAME_TEXT_SCALE, maxWidth, _nameTextRasterizer);
            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(1, 1), NAME_TEXT_SHADOW_COLOR, NAME_TEXT_SCALE, maxWidth, _nameTextRasterizer);
            DrawStatusBarText(sprite, textToDraw, snappedPosition, NAME_TEXT_COLOR, NAME_TEXT_SCALE, maxWidth, _nameTextRasterizer);
        }

        private void DrawDiagonalShadowText(SpriteBatch sprite, string text, Vector2 position, Color textColor, Color shadowColor, float scale = 1.0f, float? maxWidth = null)
        {
            if (!HasStatusBarTextRenderer() || string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 snappedPosition = SnapToPixel(position);
            string textToDraw = ResolveTextForWidth(sprite, text, scale, maxWidth);
            if (string.IsNullOrEmpty(textToDraw))
            {
                return;
            }

            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(-1, -1), shadowColor, scale, maxWidth);
            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(1, -1), shadowColor, scale, maxWidth);
            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(-1, 1), shadowColor, scale, maxWidth);
            DrawDiagonalShadowPass(sprite, textToDraw, snappedPosition, new Vector2(1, 1), shadowColor, scale, maxWidth);
            DrawStatusBarText(sprite, textToDraw, snappedPosition, textColor, scale, maxWidth);
        }

        private void DrawDiagonalShadowPass(
            SpriteBatch sprite,
            string text,
            Vector2 basePosition,
            Vector2 shadowOffset,
            Color shadowColor,
            float scale,
            float? maxWidth,
            ClientTextRasterizer rasterizer = null)
        {
            Vector2 shadowPosition = basePosition + shadowOffset;
            float? clipWidth = maxWidth;
            if (maxWidth.HasValue)
            {
                clipWidth = Math.Max(0f, maxWidth.Value + (basePosition.X - shadowPosition.X));
            }

            DrawStatusBarText(sprite, text, shadowPosition, shadowColor, scale, clipWidth, rasterizer);
        }

        private Vector2 MeasureStatusBarText(string text, float scale, ClientTextRasterizer rasterizer = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            if (rasterizer != null)
            {
                return rasterizer.MeasureString(text, scale);
            }

            if (_clientTextRasterizer != null)
            {
                return _clientTextRasterizer.MeasureString(text, scale);
            }

            return ClientTextDrawing.Measure((GraphicsDevice)null, text, scale, _font);
        }

        private int ResolveStatusBarLineSpacing()
        {
            if (_clientTextRasterizer != null)
            {
                return Math.Max(1, (int)Math.Ceiling(_clientTextRasterizer.MeasureString("Ag").Y));
            }

            return _font?.LineSpacing ?? 0;
        }

        private void DrawStatusBarText(
            SpriteBatch sprite,
            string text,
            Vector2 position,
            Color color,
            float scale,
            float? maxWidth = null,
            ClientTextRasterizer rasterizer = null)
        {
            if (sprite == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            if (rasterizer != null)
            {
                rasterizer.DrawString(sprite, text, position, color, scale, maxWidth);
                return;
            }

            if (_clientTextRasterizer != null)
            {
                _clientTextRasterizer.DrawString(sprite, text, position, color, scale, maxWidth);
                return;
            }

            ClientTextDrawing.Draw(sprite, text, position, color, scale, _font, maxWidth);
        }

        private bool HasStatusBarTextRenderer()
        {
            return _clientTextRasterizer != null || _font != null;
        }

        /// <summary>
        /// Draw a string using bitmap font textures (MapleStory style).
        /// Falls back to SpriteFont for characters without bitmap textures.
        /// Returns the width of the drawn string for positioning.
        /// </summary>
        private int DrawBitmapString(SpriteBatch sprite, string text, Vector2 position, float scale = 1.0f) {
            if (_digitTextures == null || !_useBitmapFont)
                return 0;

            Vector2 snappedPosition = SnapToPixel(position);
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
                        (int)(snappedPosition.X + xOffset - origin.X * scale),
                        (int)(snappedPosition.Y + yAdjust * scale),
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
                    Vector2 charPos = new Vector2(snappedPosition.X + xOffset, snappedPosition.Y);
                    // Draw with slight shadow for visibility
                    ClientTextDrawing.DrawShadowed(sprite, charStr, charPos, Color.White, _font, scale * 0.8f);
                    Vector2 charSize = MeasureStatusBarText(charStr, scale * 0.8f);
                    xOffset += (int)charSize.X + CHAR_SPACING;
                }
            }

            return xOffset;
        }

        private int DrawDigitBitmapString(SpriteBatch sprite, string text, Vector2 position, Texture2D[] digitTextures, Point[] digitOrigins, float scale = 1.0f)
        {
            if (digitTextures == null || digitTextures.Length < 10)
                return 0;

            Vector2 snappedPosition = SnapToPixel(position);
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
                    (int)(snappedPosition.X + xOffset - origin.X * scale),
                    (int)(snappedPosition.Y + yAdjust * scale),
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

    internal sealed class StatusBarTooltipLine
    {
        public List<StatusBarTooltipTextRun> Runs { get; } = new();
        public bool IsEmpty => Runs.Count == 0 || Runs.All(run => string.IsNullOrEmpty(run.Text));

        public void Append(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (Runs.Count > 0 && Runs[^1].Color == color)
            {
                StatusBarTooltipTextRun previous = Runs[^1];
                Runs[^1] = new StatusBarTooltipTextRun(previous.Text + text, color);
                return;
            }

            Runs.Add(new StatusBarTooltipTextRun(text, color));
        }
    }

    internal readonly struct StatusBarTooltipTextRun
    {
        public StatusBarTooltipTextRun(string text, Color color)
        {
            Text = text;
            Color = color;
        }

        public string Text { get; }
        public Color Color { get; }
    }

    internal readonly struct StatusBarTooltipToken
    {
        public StatusBarTooltipToken(string text, Color color, bool isWhitespace, bool isNewLine)
        {
            Text = text;
            Color = color;
            IsWhitespace = isWhitespace;
            IsNewLine = isNewLine;
        }

        public string Text { get; }
        public Color Color { get; }
        public bool IsWhitespace { get; }
        public bool IsNewLine { get; }

        public static StatusBarTooltipToken NewLine()
        {
            return new StatusBarTooltipToken(string.Empty, Color.White, isWhitespace: false, isNewLine: true);
        }
    }
}
