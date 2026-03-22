using HaSharedLibrary.Render.DX;

using MapleLib.Helpers;

using MapleLib.WzLib;

using MapleLib.WzLib.WzProperties;

using MapleLib.WzLib.WzStructure;

using Microsoft.Xna.Framework;

using Microsoft.Xna.Framework.Graphics;

using MapleLib.WzLib.WzStructure.Data;

using Spine;

using System;

using System.Buffers.Binary;

using System.Collections.Generic;

using System.Globalization;

using System.Linq;

using System.IO;

using HaCreator.MapEditor;

using HaCreator.MapEditor.Info;

using HaCreator.MapEditor.Instance.Misc;

using HaCreator.MapSimulator.Character;

using HaCreator.Wz;

using HaSharedLibrary.Wz;

using HaSharedLibrary.Util;



namespace HaCreator.MapSimulator.Effects

{

    /// <summary>

    /// Special Effect Field System - Manages specialized field types from MapleStory client.

    ///

    /// Handles:

    /// - CField_Wedding: Wedding ceremony effects (packets 379, 380)

    /// - CField_Witchtower: Witch tower score tracking (packet 358)

    /// - CField_GuildBoss: Guild boss healer/pulley mechanics (packets 344, 345)

    /// - CField_Dojang: Mu Lung Dojo timer and HUD gauges

    /// - CField_SpaceGAGA: Rescue Gaga timerboard clock

    /// - CField_Massacre: Kill counting and gauge system (packet 173)

    /// </summary>

    #region Massacre Field (CField_Massacre)

    /// <summary>

    /// Massacre Field HUD and timerboard flow.

    ///

    /// Client evidence:

    /// - CField_Massacre::OnClock (0x556af0) only reacts to clock type 2, replaces the previous

    ///   clock window, creates a dedicated 258x61 timerboard at (-96, 5), then starts it.

    /// - CTimerboard_Massacre::Draw (0x557100) renders a dedicated source canvas and draws zero-

    ///   padded minutes and seconds at fixed positions: (20, 13) and (105, 13).

    /// - CField_Massacre::Update (0x557530) recalculates the decay gauge from timer elapsed time,

    ///   advances UpdateKeyAnimation every frame, and shows the clear effect once when the board

    ///   reaches one second remaining.

    /// - CField_Massacre::UpdateKeyAnimation (0x556bf0) advances through three one-shot stages.

    /// </summary>

    public class MassacreField

    {

        private static readonly string[] UiImageNames = { "UIWindow2.img", "UIWindow.img" };

        private const int TimerboardSourceStringPoolId = 0x14EE;

        private const int ClearScreenEffectStringPoolId = 0x14EC;

        private const int KeyAnimationOpenStringPoolId = 0x1512;

        private const int KeyAnimationLoopStringPoolId = 0x1511;

        private const int KeyAnimationCloseStringPoolId = 0x1510;

        private const int ComboTimeoutMs = 3000;

        private const int TimerboardWidth = 258;

        private const int TimerboardHeight = 61;

        private const int TimerboardOffsetX = -96;

        private const int TimerboardY = 5;

        private const int TimerMinuteTextX = 20;

        private const int TimerSecondTextX = 105;

        private const int TimerTextY = 13;

        private const int TimerDividerX = 79;

        private const int TimerDividerY = 10;

        private const int TimerDividerWidth = 17;

        private const int TimerDividerHeight = 38;

        private const int GaugeWidth = 262;

        private const int GaugeFillWidth = 259;

        private const int GaugeHeight = 9;

        private const int GaugeOffsetX = -93;

        private const int GaugeY = 78;

        private const int GaugeFillOffsetX = 4;

        private const int GaugeFillOffsetY = 6;

        private const int GaugeLabelOffsetY = -8;

        private const int GaugeFillHeight = 9;

        private const int KeyAnimationX = 7;

        private const int KeyAnimationY = 135;

        private const int ClearEffectDurationMs = 2200;

        private const float DangerDepletionThreshold = 0.65f;

        private const int BonusEffectY = 190;

        private const int CountEffectY = 190;

        private const int ResultBoardY = 115;

        private const int ResultScoreX = 180;

        private const int ResultScoreY = 144;

        private const int ResultRankX = 98;

        private const int ResultRankY = 101;

        private const int ResultBackdrop2X = 245;

        private const int ResultBackdrop2Y = 28;



        private bool _isActive;

        private int _mapId;

        private int _incGauge;

        private int _gaugeDec = 1;

        private int _currentGauge;

        private int _maxGauge = 100;

        private int _defaultGaugeIncrease = 1;

        private int _coolGaugeIncrease;

        private int _missGaugePenalty;

        private int _mapDistance;

        private float _displayGauge;

        private int _killCount;

        private int _comboCount;

        private int _lastKillTime = int.MinValue;

        private int _timerDurationSec;

        private int _timeOverTick = int.MinValue;

        private int _lastClockUpdateTick = int.MinValue;

        private bool _showedClearEffect;

        private bool _clearEffectActive;

        private float _clearEffectAlpha;

        private int _clearEffectStartTime = int.MinValue;

        private int _keyAnimationStage = -1;

        private int _keyAnimationStageStart = int.MinValue;

        private bool _keyAnimationQueued;

        private bool _disableSkill;

        private readonly List<MassacreCountEffect> _countEffects = new();

        private string _countEffectBannerText;

        private int _countEffectBannerUntilMs = int.MinValue;

        private int _countEffectPresentationStartTick = int.MinValue;

        private int _countEffectPresentationStage;

        private GraphicsDevice _device;

        private bool _assetsLoaded;

        private Texture2D _gaugeBackgroundTexture;

        private Texture2D _gaugeTextTexture;

        private Texture2D _gaugePixelTexture;

        private Texture2D _timerboardSourceTexture;

        private readonly Texture2D[] _timerDigits = new Texture2D[10];

        private readonly Texture2D[] _resultDigits = new Texture2D[10];

        private Texture2D _resultPlusTexture;

        private Texture2D _resultBoardTexture;

        private readonly Dictionary<char, MassacreCanvasFrame> _rankTextures = new();

        private List<MassacreCanvasFrame> _keyOpenFrames;

        private List<MassacreCanvasFrame> _keyLoopFrames;

        private List<MassacreCanvasFrame> _keyCloseFrames;

        private List<MassacreCanvasFrame> _dangerFrames;

        private List<MassacreCanvasFrame> _dangerIconFrames;

        private List<MassacreCanvasFrame> _dangerTextFrames;

        private List<MassacreCanvasFrame> _dangerBackgroundFrames;

        private List<MassacreCanvasFrame> _bonusStageFrames;

        private List<MassacreCanvasFrame> _bonusFrames;

        private List<MassacreCanvasFrame> _countEffectFirstStartFrames;

        private List<MassacreCanvasFrame> _countEffectStageFrames;

        private readonly Dictionary<int, List<MassacreCanvasFrame>> _countEffectNumberFrames = new();

        private List<MassacreCanvasFrame> _resultClearFrames;

        private List<MassacreCanvasFrame> _resultFailFrames;

        private List<MassacreCanvasFrame> _resultBoardPulseFrames;

        private int _bonusPresentationStartTick = int.MinValue;

        private int _resultPresentationStartTick = int.MinValue;

        private MassacreResultPresentation _resultPresentation = MassacreResultPresentation.None;

        private char _resultRank = 'D';

        private int _resultScore;



        public bool IsActive => _isActive;

        public int CurrentGauge => _currentGauge;

        public int MaxGauge => _maxGauge;

        public int GaugeDecreasePerSecond => _gaugeDec;

        public int DefaultGaugeIncrease => _defaultGaugeIncrease;

        public bool IsSkillDisabled => _disableSkill;

        public bool HasKeyAnimation => _keyAnimationStage >= 0;

        public int KillCount => _killCount;

        public int ComboCount => _comboCount;

        public int TimerRemain => RemainingSeconds;

        public bool HasBonusPresentation => _bonusPresentationStartTick != int.MinValue;

        public bool HasCountEffectPresentation => _countEffectPresentationStartTick != int.MinValue;

        public int ActiveCountEffectStage => _countEffectPresentationStage;

        public bool HasResultPresentation => _resultPresentation != MassacreResultPresentation.None;

        public float GaugeProgress => Math.Clamp(_maxGauge <= 0 ? 0f : _displayGauge / _maxGauge, 0f, 1f);

        public bool HasRunningTimerboard => _timeOverTick != int.MinValue;

        public int RemainingSeconds

        {

            get

            {

                if (_timeOverTick == int.MinValue)

                {

                    return 0;

                }



                int remainingMs = _timeOverTick - Environment.TickCount;

                if (remainingMs <= 0)

                {

                    return 0;

                }



                return (remainingMs + 999) / 1000;

            }

        }



        public void Initialize(GraphicsDevice device)

        {

            _device = device;

            _assetsLoaded = false;

        }



        public void Enable(int mapId = 0)

        {

            _isActive = true;

            _mapId = mapId;

            ResetRoundState();

        }



        public void Configure(MapInfo mapInfo)

        {

            if (!_isActive || mapInfo == null)

            {

                return;

            }



            for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)

            {

                if (mapInfo.additionalNonInfoProps[i] is not WzSubProperty massacre

                    || !string.Equals(massacre.Name, "mobMassacre", StringComparison.OrdinalIgnoreCase))

                {

                    continue;

                }



                _mapDistance = Math.Max(0, InfoTool.GetOptionalInt(massacre["mapDistance"]) ?? _mapDistance);

                _disableSkill = (InfoTool.GetOptionalInt(massacre["disableSkill"]) ?? 0) != 0;



                if (massacre["gauge"] is WzSubProperty gauge)

                {

                    _maxGauge = Math.Max(1, InfoTool.GetOptionalInt(gauge["total"]) ?? _maxGauge);

                    _gaugeDec = Math.Max(0, InfoTool.GetOptionalInt(gauge["decrease"]) ?? _gaugeDec);

                    _defaultGaugeIncrease = Math.Max(0, InfoTool.GetOptionalInt(gauge["hitAdd"]) ?? _defaultGaugeIncrease);

                    _coolGaugeIncrease = Math.Max(0, InfoTool.GetOptionalInt(gauge["coolAdd"]) ?? _coolGaugeIncrease);

                    _missGaugePenalty = Math.Max(0, InfoTool.GetOptionalInt(gauge["missSub"]) ?? _missGaugePenalty);

                }



                _countEffects.Clear();

                if (massacre["countEffect"] is WzSubProperty countEffect)

                {

                    foreach (WzImageProperty child in countEffect.WzProperties)

                    {

                        if (!int.TryParse(child.Name, out int threshold)

                            || child is not WzSubProperty thresholdProperty)

                        {

                            continue;

                        }



                        _countEffects.Add(new MassacreCountEffect(

                            threshold,

                            InfoTool.GetOptionalInt(thresholdProperty["buff"]),

                            (InfoTool.GetOptionalInt(thresholdProperty["skillUse"]) ?? 0) != 0));

                    }



                    _countEffects.Sort(static (left, right) => left.Threshold.CompareTo(right.Threshold));

                }



                _currentGauge = Math.Clamp(_currentGauge, 0, _maxGauge);

                _displayGauge = Math.Clamp(_displayGauge, 0f, _maxGauge);

                if (_disableSkill)

                {

                    _keyAnimationStage = -1;

                    _keyAnimationStageStart = int.MinValue;

                    _keyAnimationQueued = false;

                }



                return;

            }

        }



        public void SetParameters(int maxGauge, int timer, int gaugeDec)

        {

            _maxGauge = Math.Max(1, maxGauge);

            _gaugeDec = Math.Max(0, gaugeDec);

            OnClock(2, timer, Environment.TickCount);

        }



        public void SetGaugeParameters(int maxGauge, int gaugeDec)

        {

            _maxGauge = Math.Max(1, maxGauge);

            _gaugeDec = Math.Max(0, gaugeDec);

            _currentGauge = Math.Clamp(_currentGauge, 0, _maxGauge);

            _displayGauge = Math.Clamp(_displayGauge, 0f, _maxGauge);

        }



        public void ResetRoundState()

        {

            _incGauge = 0;

            _currentGauge = 0;

            _displayGauge = 0f;

            _killCount = 0;

            _comboCount = 0;

            _lastKillTime = int.MinValue;

            _timerDurationSec = 0;

            _timeOverTick = int.MinValue;

            _lastClockUpdateTick = int.MinValue;

            _showedClearEffect = false;

            _clearEffectActive = false;

            _clearEffectAlpha = 0f;

            _clearEffectStartTime = int.MinValue;

            _keyAnimationStage = -1;

            _keyAnimationStageStart = int.MinValue;

            _keyAnimationQueued = false;

            _countEffectBannerText = null;

            _countEffectBannerUntilMs = int.MinValue;

            _countEffectPresentationStartTick = int.MinValue;

            _countEffectPresentationStage = 0;

            _bonusPresentationStartTick = int.MinValue;

            _resultPresentationStartTick = int.MinValue;

            _resultPresentation = MassacreResultPresentation.None;

            _resultRank = 'D';

            _resultScore = 0;

        }



        public void OnClock(int clockType, int durationSec, int currentTimeMs)

        {

            if (!_isActive || clockType != 2)

            {

                return;

            }



            _timerDurationSec = Math.Max(0, durationSec);

            _timeOverTick = _timerDurationSec > 0 ? currentTimeMs + (_timerDurationSec * 1000) : int.MinValue;

            _lastClockUpdateTick = currentTimeMs;

            _showedClearEffect = false;

            _clearEffectActive = false;

            _clearEffectAlpha = 0f;

            _clearEffectStartTime = int.MinValue;

        }



        /// <summary>

        /// OnMassacreIncGauge - Packet 173

        /// From client: this->m_nIncGauge = Decode4(iPacket)

        /// </summary>

        public void OnMassacreIncGauge(int newIncGauge, int currentTimeMs)

        {

            if (!_isActive)

            {

                return;

            }



            System.Diagnostics.Debug.WriteLine($"[MassacreField] OnMassacreIncGauge: {_incGauge} -> {newIncGauge}");



            int increase = newIncGauge - _incGauge;

            _incGauge = Math.Max(0, newIncGauge);



            if (increase > 0)

            {

                RegisterKill(currentTimeMs);

                QueueKeyAnimation();

            }

        }



        /// <summary>

        /// Add kills directly (for testing/simulation)

        /// </summary>

        public void AddKill(int gaugeAmount, int currentTimeMs)

        {

            if (!_isActive)

            {

                return;

            }



            _incGauge = Math.Max(0, _incGauge + Math.Max(0, gaugeAmount));

            RegisterKill(currentTimeMs);

            QueueKeyAnimation();

        }



        public void Update(int currentTimeMs, float deltaSeconds)

        {

            if (!_isActive)

            {

                return;

            }



            int remainingSeconds = RemainingSeconds;

            if (_timeOverTick != int.MinValue && _lastClockUpdateTick != int.MinValue)

            {

                int elapsedSeconds = Math.Max(0, _timerDurationSec - remainingSeconds);

                int decayAmount = _gaugeDec * elapsedSeconds;

                _currentGauge = Math.Clamp(_incGauge - decayAmount, 0, _maxGauge);

            }

            else

            {

                _currentGauge = Math.Clamp(_incGauge, 0, _maxGauge);

            }



            float targetGauge = _currentGauge;

            float diff = targetGauge - _displayGauge;

            _displayGauge = Math.Clamp(_displayGauge + (diff * MathF.Min(1f, 8f * deltaSeconds)), 0f, _maxGauge);



            UpdateKeyAnimation(currentTimeMs);



            if (!_showedClearEffect && _timeOverTick != int.MinValue && remainingSeconds <= 1)

            {

                TriggerClearEffect(currentTimeMs);

            }



            if (_clearEffectActive)

            {

                int clearElapsed = currentTimeMs - _clearEffectStartTime;

                if (clearElapsed < 0 || clearElapsed >= ClearEffectDurationMs)

                {

                    _clearEffectActive = false;

                    _clearEffectAlpha = 0f;

                }

                else

                {

                    float normalized = 1f - (clearElapsed / (float)ClearEffectDurationMs);

                    _clearEffectAlpha = normalized * normalized;

                }

            }



            if (_lastKillTime != int.MinValue && currentTimeMs - _lastKillTime > ComboTimeoutMs)

            {

                _comboCount = 0;

            }



            if (_bonusPresentationStartTick != int.MinValue

                && !IsAnimationPlaying(_bonusStageFrames, currentTimeMs, _bonusPresentationStartTick, repeat: false)

                && !IsAnimationPlaying(_bonusFrames, currentTimeMs, _bonusPresentationStartTick, repeat: false))

            {

                _bonusPresentationStartTick = int.MinValue;

            }



            if (_countEffectPresentationStartTick != int.MinValue

                && !IsAnimationPlaying(GetCountEffectFramesForCurrentStage(), currentTimeMs, _countEffectPresentationStartTick, repeat: false)

                && !IsAnimationPlaying(GetCountEffectNumberFrames(_countEffectPresentationStage), currentTimeMs, _countEffectPresentationStartTick, repeat: false))

            {

                _countEffectPresentationStartTick = int.MinValue;

                _countEffectPresentationStage = 0;

            }

        }



        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)

        {

            if (!_isActive || pixelTexture == null)

            {

                return;

            }



            EnsureAssetsLoaded();

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;

            DrawTimerboard(spriteBatch, pixelTexture, font, viewport);

            DrawGaugeHud(spriteBatch, pixelTexture, font, viewport);

            DrawKeyAnimation(spriteBatch, pixelTexture, font);

            DrawCountEffectPresentation(spriteBatch, font, viewport, Environment.TickCount);

            DrawBonusPresentation(spriteBatch, font, viewport, Environment.TickCount);

            DrawResultPresentation(spriteBatch, font, viewport, Environment.TickCount);

            DrawClearEffect(spriteBatch, pixelTexture, font, viewport);

        }



        public string DescribeStatus()

        {

            if (!_isActive)

            {

                return "Massacre HUD inactive";

            }



            string timerText = HasRunningTimerboard ? FormatTimer(RemainingSeconds) : "stopped";

            string nextCountEffect = GetNextCountEffectThreshold() is int threshold

                ? $", nextCountEffect={threshold}"

                : string.Empty;

            string disableSkillText = _disableSkill ? ", skills=disabled" : string.Empty;

            string countEffectText = HasCountEffectPresentation ? $", countFx=stage{_countEffectPresentationStage}" : string.Empty;

            string bonusText = HasBonusPresentation ? ", bonusFx=active" : string.Empty;

            string resultText = HasResultPresentation ? $", result={_resultPresentation}:{_resultRank}:{_resultScore}" : string.Empty;

            return $"Massacre map {_mapId}, timer={timerText}, gauge={_currentGauge}/{_maxGauge}, inc={_incGauge}, hitAdd={_defaultGaugeIncrease}, decay={_gaugeDec}/s, kills={_killCount}, combo={_comboCount}{disableSkillText}{nextCountEffect}{countEffectText}{bonusText}{resultText}";

        }



        public void Reset()

        {

            _isActive = false;

            _mapId = 0;

            _maxGauge = 100;

            _gaugeDec = 1;

            _defaultGaugeIncrease = 1;

            _coolGaugeIncrease = 0;

            _missGaugePenalty = 0;

            _mapDistance = 0;

            _disableSkill = false;

            _countEffects.Clear();

            _assetsLoaded = false;

            ResetRoundState();

        }



        private void RegisterKill(int currentTimeMs)

        {

            _killCount++;

            if (_lastKillTime != int.MinValue && currentTimeMs - _lastKillTime < ComboTimeoutMs)

            {

                _comboCount++;

            }

            else

            {

                _comboCount = 1;

            }



            _lastKillTime = currentTimeMs;

            UpdateCountEffectBanner(currentTimeMs);

        }



        private void QueueKeyAnimation()

        {

            if (_disableSkill)

            {

                return;

            }



            if (_keyAnimationStage >= 0)

            {

                _keyAnimationQueued = true;

                return;

            }



            _keyAnimationStage = 0;

            _keyAnimationStageStart = Environment.TickCount;

            _keyAnimationQueued = false;

        }



        private void UpdateKeyAnimation(int currentTimeMs)

        {

            if (_keyAnimationStage < 0)

            {

                return;

            }



            if (_keyAnimationStageStart == int.MinValue)

            {

                _keyAnimationStageStart = currentTimeMs;

            }



            List<MassacreCanvasFrame> frames = GetKeyFramesForStage(_keyAnimationStage);

            if (frames == null || IsAnimationPlaying(frames, currentTimeMs, _keyAnimationStageStart, repeat: false))

            {

                return;

            }



            _keyAnimationStageStart = currentTimeMs;

            _keyAnimationStage++;

            if (_keyAnimationStage > 2)

            {

                if (_keyAnimationQueued)

                {

                    _keyAnimationStage = 0;

                    _keyAnimationQueued = false;

                }

                else

                {

                    _keyAnimationStage = -1;

                    _keyAnimationStageStart = int.MinValue;

                }

            }

        }



        private void TriggerClearEffect(int currentTimeMs)

        {

            _showedClearEffect = true;

            _clearEffectActive = true;

            _clearEffectStartTime = currentTimeMs;

        }



        private void DrawTimerboard(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)

        {

            if (!HasRunningTimerboard && !_showedClearEffect)

            {

                return;

            }



            Rectangle bounds = new(viewport.Width / 2 + TimerboardOffsetX, TimerboardY, TimerboardWidth, TimerboardHeight);

            Texture2D timerboardTexture = _timerboardSourceTexture

                ;

            if (timerboardTexture != null)

            {

                spriteBatch.Draw(timerboardTexture, new Vector2(bounds.X, bounds.Y), Color.White);

            }

            else

            {

                spriteBatch.Draw(pixelTexture, bounds, new Color(18, 21, 24, 228));

                spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), new Color(95, 127, 160, 255));

                spriteBatch.Draw(pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), new Color(34, 45, 58, 255));



                Rectangle divider = new(bounds.X + TimerDividerX, bounds.Y + TimerDividerY, TimerDividerWidth, TimerDividerHeight);

                spriteBatch.Draw(pixelTexture, divider, new Color(44, 53, 66, 255));

            }



            int remaining = RemainingSeconds;

            string minuteText = $"{remaining / 60:00}";

            string secondText = $"{remaining % 60:00}";

            Color timeColor = remaining <= 10 ? new Color(255, 170, 120) : new Color(232, 242, 255);



            if (!TryDrawBitmapDigits(spriteBatch, _timerDigits, minuteText, new Vector2(bounds.X + TimerMinuteTextX, bounds.Y + TimerTextY))

                || !TryDrawBitmapDigits(spriteBatch, _timerDigits, secondText, new Vector2(bounds.X + TimerSecondTextX, bounds.Y + TimerTextY)))

            {

                if (font == null)

                {

                    return;

                }



                DrawDigitString(spriteBatch, font, minuteText, new Vector2(bounds.X + TimerMinuteTextX, bounds.Y + TimerTextY), timeColor);

                DrawDigitString(spriteBatch, font, secondText, new Vector2(bounds.X + TimerSecondTextX, bounds.Y + TimerTextY), timeColor);

            }

        }



        private void DrawGaugeHud(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)

        {

            int gaugeX = viewport.Width / 2 + GaugeOffsetX;

            Rectangle fillBounds = new(gaugeX + GaugeFillOffsetX, GaugeY + GaugeFillOffsetY, GaugeFillWidth, GaugeFillHeight);



            if (_gaugeBackgroundTexture != null)

            {

                spriteBatch.Draw(_gaugeBackgroundTexture, new Vector2(gaugeX, GaugeY), Color.White);

            }

            else

            {

                Rectangle fallbackBounds = new(gaugeX, GaugeY, GaugeWidth + (GaugeFillOffsetX * 2), GaugeHeight + GaugeFillOffsetY);

                spriteBatch.Draw(pixelTexture, fallbackBounds, new Color(25, 18, 16, 224));

            }



            int fillWidth = Math.Clamp((int)MathF.Round(fillBounds.Width * GaugeProgress), 0, fillBounds.Width);

            if (fillWidth > 0)

            {

                Texture2D gaugeFillTexture = _gaugePixelTexture ?? pixelTexture;

                Color fillColor = _gaugePixelTexture != null ? Color.White : GetGaugeColor(GaugeProgress);

                spriteBatch.Draw(gaugeFillTexture, new Rectangle(fillBounds.X, fillBounds.Y, fillWidth, fillBounds.Height), fillColor);

            }



            if (ShouldDrawDangerOverlay())

            {

                DrawAnimation(spriteBatch, _dangerBackgroundFrames, Environment.TickCount, 0, new Vector2(fillBounds.X, fillBounds.Y), repeat: true);

                DrawAnimation(spriteBatch, _dangerFrames, Environment.TickCount, 0, new Vector2(fillBounds.Right - 115f, GaugeY - 2f), repeat: true);

                DrawAnimation(spriteBatch, _dangerTextFrames, Environment.TickCount, 0, new Vector2(gaugeX - 3f, GaugeY - 3f), repeat: true);

                DrawAnimation(spriteBatch, _dangerIconFrames, Environment.TickCount, 0, new Vector2(gaugeX + 214f, GaugeY - 5f), repeat: true);

            }

            else if (_gaugeTextTexture != null)

            {

                spriteBatch.Draw(_gaugeTextTexture, new Vector2(gaugeX, GaugeY + GaugeLabelOffsetY), Color.White);

            }



            if (font == null)

            {

                return;

            }



            string gaugeText = $"{_currentGauge}/{_maxGauge}";

            string statusText = _comboCount > 1 ? $"{_comboCount}x combo" : $"{_killCount} kills";

            Color statusColor = _comboCount >= 10 ? Color.Gold : _comboCount >= 5 ? Color.Orange : new Color(238, 220, 191);

            string nextThresholdText = GetNextCountEffectThreshold() is int threshold

                ? $"next {threshold}"

                : null;



            Vector2 gaugeTextPos = new(gaugeX + 180, GaugeY + 14);

            spriteBatch.DrawString(font, gaugeText, gaugeTextPos + Vector2.One, Color.Black);

            spriteBatch.DrawString(font, gaugeText, gaugeTextPos, Color.White);



            Vector2 infoPos = new(gaugeX + 10, GaugeY + 18);

            spriteBatch.DrawString(font, statusText, infoPos + Vector2.One, Color.Black);

            spriteBatch.DrawString(font, statusText, infoPos, statusColor);

            if (!string.IsNullOrWhiteSpace(nextThresholdText))

            {

                Vector2 nextThresholdSize = font.MeasureString(nextThresholdText);

                Vector2 nextPos = new(gaugeX + GaugeWidth - nextThresholdSize.X, GaugeY + 18);

                spriteBatch.DrawString(font, nextThresholdText, nextPos + Vector2.One, Color.Black);

                spriteBatch.DrawString(font, nextThresholdText, nextPos, new Color(214, 197, 166));

            }



            if (!string.IsNullOrWhiteSpace(_countEffectBannerText) && Environment.TickCount < _countEffectBannerUntilMs)

            {

                Vector2 bannerSize = font.MeasureString(_countEffectBannerText);

                Vector2 bannerPos = new((viewport.Width - bannerSize.X) / 2f, GaugeY + 34);

                spriteBatch.DrawString(font, _countEffectBannerText, bannerPos + Vector2.One, Color.Black);

                spriteBatch.DrawString(font, _countEffectBannerText, bannerPos, new Color(255, 223, 132));

            }

        }



        private void DrawKeyAnimation(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)

        {

            if (_keyAnimationStage < 0)

            {

                return;

            }



            List<MassacreCanvasFrame> frames = GetKeyFramesForStage(_keyAnimationStage);

            if (!DrawAnimation(spriteBatch, frames, Environment.TickCount, _keyAnimationStageStart, new Vector2(KeyAnimationX, KeyAnimationY), repeat: false) && font != null)

            {

                string text = _keyAnimationStage switch

                {

                    0 => "KEY",

                    1 => "KEY!",

                    _ => "KEY!!"

                };

                spriteBatch.DrawString(font, text, new Vector2(KeyAnimationX, KeyAnimationY), Color.White);

            }

        }



        private void DrawClearEffect(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Viewport viewport)

        {

            if (!_clearEffectActive || HasResultPresentation)

            {

                return;

            }



            spriteBatch.Draw(pixelTexture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White * (0.16f * _clearEffectAlpha));

            Vector2 center = new(viewport.Width / 2f, viewport.Height / 2f);

            if (!DrawAnimation(spriteBatch, _resultClearFrames, Environment.TickCount, _clearEffectStartTime, center, repeat: false)

                && font != null)

            {

                const string clearText = "CLEAR!";

                Vector2 textSize = font.MeasureString(clearText) * 1.5f;

                Vector2 pos = new((viewport.Width - textSize.X) / 2f, 200f);

                spriteBatch.DrawString(font, clearText, pos + new Vector2(2f, 2f), Color.Black * _clearEffectAlpha);

                spriteBatch.DrawString(font, clearText, pos, new Color(255, 225, 118) * _clearEffectAlpha);

            }

        }



        private static void DrawDigitString(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color)

        {

            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black);

            spriteBatch.DrawString(font, text, position, color);

        }



        private static string FormatTimer(int remainingSeconds)

        {

            int minutes = Math.Max(0, remainingSeconds) / 60;

            int seconds = Math.Max(0, remainingSeconds) % 60;

            return $"{minutes:00}:{seconds:00}";

        }



        private static Color GetGaugeColor(float progress)

        {

            if (progress >= 0.8f)

            {

                return new Color(255, 215, 112);

            }



            if (progress >= 0.5f)

            {

                return new Color(255, 165, 66);

            }



            if (progress >= 0.25f)

            {

                return new Color(227, 109, 62);

            }



            return new Color(181, 65, 54);

        }



        private int? GetNextCountEffectThreshold()

        {

            for (int i = 0; i < _countEffects.Count; i++)

            {

                if (_killCount < _countEffects[i].Threshold)

                {

                    return _countEffects[i].Threshold;

                }

            }



            return null;

        }



        private void UpdateCountEffectBanner(int currentTimeMs)

        {

            for (int i = 0; i < _countEffects.Count; i++)

            {

                if (_countEffects[i].Threshold != _killCount)

                {

                    continue;

                }



                string effectText = _countEffects[i].RequiresSkillUse ? " skill" : " buff";

                _countEffectBannerText = $"{_countEffects[i].Threshold} kills{effectText}";

                _countEffectBannerUntilMs = currentTimeMs + 1800;

                TriggerCountEffectPresentation(i + 1, currentTimeMs);

                return;

            }

        }



        private void TriggerCountEffectPresentation(int stage, int currentTimeMs)

        {

            _countEffectPresentationStage = Math.Max(1, stage);

            _countEffectPresentationStartTick = currentTimeMs;

        }



        public void ShowResultPresentation(bool clear, int currentTimeMs, int? scoreOverride = null, char? rankOverride = null)

        {

            _resultPresentation = clear ? MassacreResultPresentation.Clear : MassacreResultPresentation.Fail;

            _resultPresentationStartTick = currentTimeMs;

            _resultScore = Math.Max(0, scoreOverride ?? _killCount);

            _resultRank = NormalizeRank(rankOverride ?? ComputeResultRank());

        }



        public void ShowBonusPresentation(int currentTimeMs)

        {

            _bonusPresentationStartTick = currentTimeMs;

        }



        private void EnsureAssetsLoaded()

        {

            if (_assetsLoaded || _device == null)

            {

                return;

            }



            WzImage uiWindow = null;

            foreach (string imageName in UiImageNames)

            {

                WzImage uiImage = global::HaCreator.Program.FindImage("UI", imageName);

                if (uiImage?.WzProperties == null)

                {

                    continue;

                }



                uiWindow ??= uiImage;

                _timerboardSourceTexture ??= LoadCanvasTexture(FindTimerboardSourceCanvas(uiImage));

            }



            WzImage effectImage = global::HaCreator.Program.FindImage("Map", "Effect.img")

                ?? global::HaCreator.Program.FindImage("Map", "effect.img");



            WzImageProperty monsterKilling = uiWindow?["MonsterKilling"];

            WzImageProperty count = monsterKilling?["Count"];

            WzImageProperty gauge = monsterKilling?["Gauge"];

            WzImageProperty result = monsterKilling?["Result"];



            LoadDigitTextures(count?["number"], _timerDigits);



            _gaugeBackgroundTexture = LoadCanvasTexture(gauge?["backgrd"] as WzCanvasProperty);

            _gaugeTextTexture = LoadCanvasTexture(gauge?["text"] as WzCanvasProperty);

            _gaugePixelTexture = LoadCanvasTexture(gauge?["pixel"] as WzCanvasProperty);

            _dangerFrames = LoadAnimationFrames(gauge?["danger"]);

            _dangerIconFrames = LoadAnimationFrames(gauge?["iconD"]);

            _dangerTextFrames = LoadAnimationFrames(gauge?["textD"]);

            _dangerBackgroundFrames = LoadAnimationFrames(gauge?["backgrdD"]);



            _keyOpenFrames = LoadAnimationFrames(count?["keyBackgrd"]?["open"]);

            _keyLoopFrames = LoadAnimationFrames(count?["keyBackgrd"]?["ing"]);

            _keyCloseFrames = LoadAnimationFrames(count?["keyBackgrd"]?["close"]);



            _resultBoardTexture = LoadCanvasTexture(result?["backgrd"] as WzCanvasProperty);

            _resultBoardPulseFrames = LoadAnimationFrames(result?["backgrd2"]);

            LoadDigitTextures(result?["number2"], _resultDigits, out _resultPlusTexture);

            LoadRankTextures(result?["Rank"]);



            WzImageProperty killing = effectImage?["killing"];

            _countEffectFirstStartFrames = LoadAnimationFrames(killing?["first"]?["start"]);

            _countEffectStageFrames = LoadAnimationFrames(killing?["first"]?["stage"]);

            LoadCountEffectNumberFrames(killing?["number"]);

            _resultClearFrames = LoadAnimationFrames(killing?["clear"]);

            _resultFailFrames = LoadAnimationFrames(killing?["fail"]);

            _bonusStageFrames = LoadAnimationFrames(killing?["bonus"]?["stage"]);

            _bonusFrames = LoadAnimationFrames(killing?["bonus"]?["bonus"]);

            _assetsLoaded = true;

        }



        private void DrawCountEffectPresentation(SpriteBatch spriteBatch, SpriteFont font, Viewport viewport, int currentTimeMs)

        {

            if (_countEffectPresentationStartTick == int.MinValue || _countEffectPresentationStage <= 0)

            {

                return;

            }



            Vector2 anchor = new(viewport.Width / 2f, CountEffectY);

            bool drewBase = DrawAnimation(spriteBatch, GetCountEffectFramesForCurrentStage(), currentTimeMs, _countEffectPresentationStartTick, anchor, repeat: false);

            bool drewNumber = DrawAnimation(spriteBatch, GetCountEffectNumberFrames(_countEffectPresentationStage), currentTimeMs, _countEffectPresentationStartTick, anchor, repeat: false);

            if (!drewBase && !drewNumber && font != null)

            {

                string fallback = $"STAGE {_countEffectPresentationStage}";

                Vector2 size = font.MeasureString(fallback);

                Vector2 pos = new((viewport.Width - size.X) / 2f, CountEffectY);

                spriteBatch.DrawString(font, fallback, pos + Vector2.One, Color.Black);

                spriteBatch.DrawString(font, fallback, pos, new Color(255, 223, 132));

            }

        }



        private void DrawBonusPresentation(SpriteBatch spriteBatch, SpriteFont font, Viewport viewport, int currentTimeMs)

        {

            if (_bonusPresentationStartTick == int.MinValue)

            {

                return;

            }



            Vector2 anchor = new(viewport.Width / 2f, BonusEffectY);

            bool drewStage = DrawAnimation(spriteBatch, _bonusStageFrames, currentTimeMs, _bonusPresentationStartTick, anchor, repeat: false);

            bool drewBonus = DrawAnimation(spriteBatch, _bonusFrames, currentTimeMs, _bonusPresentationStartTick, anchor, repeat: false);

            if (!drewStage && !drewBonus && font != null)

            {

                const string text = "BONUS";

                Vector2 textSize = font.MeasureString(text);

                Vector2 drawPos = new((viewport.Width - textSize.X) / 2f, BonusEffectY);

                spriteBatch.DrawString(font, text, drawPos + Vector2.One, Color.Black);

                spriteBatch.DrawString(font, text, drawPos, new Color(255, 223, 132));

            }

        }



        private void DrawResultPresentation(SpriteBatch spriteBatch, SpriteFont font, Viewport viewport, int currentTimeMs)

        {

            if (_resultPresentation == MassacreResultPresentation.None || _resultPresentationStartTick == int.MinValue)

            {

                return;

            }



            Vector2 center = new(viewport.Width / 2f, viewport.Height / 2f);

            DrawAnimation(

                spriteBatch,

                _resultPresentation == MassacreResultPresentation.Clear ? _resultClearFrames : _resultFailFrames,

                currentTimeMs,

                _resultPresentationStartTick,

                center,

                repeat: false);



            if (_resultBoardTexture != null)

            {

                Vector2 boardPos = new((viewport.Width - _resultBoardTexture.Width) / 2f, ResultBoardY);

                spriteBatch.Draw(_resultBoardTexture, boardPos, Color.White);

                DrawAnimation(spriteBatch, _resultBoardPulseFrames, currentTimeMs, _resultPresentationStartTick, boardPos + new Vector2(ResultBackdrop2X, ResultBackdrop2Y), repeat: false);

                DrawResultRank(spriteBatch, boardPos + new Vector2(ResultRankX, ResultRankY));

                DrawBitmapNumber(spriteBatch, _resultDigits, _resultScore.ToString(), boardPos + new Vector2(ResultScoreX, ResultScoreY), _resultPlusTexture, includePlus: false);

                return;

            }



            if (font != null)

            {

                string fallback = $"{_resultPresentation} {_resultScore}";

                Vector2 size = font.MeasureString(fallback);

                Vector2 pos = new((viewport.Width - size.X) / 2f, ResultBoardY);

                spriteBatch.DrawString(font, fallback, pos + Vector2.One, Color.Black);

                spriteBatch.DrawString(font, fallback, pos, Color.White);

            }

        }



        private void DrawResultRank(SpriteBatch spriteBatch, Vector2 topLeft)

        {

            if (_rankTextures.TryGetValue(_resultRank, out MassacreCanvasFrame rankTexture) && rankTexture.Texture != null)

            {

                spriteBatch.Draw(rankTexture.Texture, topLeft, Color.White);

            }

        }



        private void DrawBitmapNumber(SpriteBatch spriteBatch, Texture2D[] digits, string text, Vector2 topLeft, Texture2D specialTexture = null, bool includePlus = false)

        {

            if (digits == null || digits.All(texture => texture == null))

            {

                return;

            }



            float x = topLeft.X;

            if (includePlus && specialTexture != null)

            {

                spriteBatch.Draw(specialTexture, new Vector2(x, topLeft.Y), Color.White);

                x += specialTexture.Width;

            }



            foreach (char digitChar in text)

            {

                if (digitChar is < '0' or > '9')

                {

                    continue;

                }



                Texture2D digitTexture = digits[digitChar - '0'];

                if (digitTexture == null)

                {

                    continue;

                }



                spriteBatch.Draw(digitTexture, new Vector2(x, topLeft.Y), Color.White);

                x += digitTexture.Width;

            }

        }



        private static bool TryDrawBitmapDigits(SpriteBatch spriteBatch, Texture2D[] digits, string text, Vector2 position)

        {

            if (digits == null || text == null || digits.Any(texture => texture == null))

            {

                return false;

            }



            float x = position.X;

            foreach (char digitChar in text)

            {

                if (digitChar is < '0' or > '9')

                {

                    return false;

                }



                Texture2D digitTexture = digits[digitChar - '0'];

                spriteBatch.Draw(digitTexture, new Vector2(x, position.Y), Color.White);

                x += digitTexture.Width;

            }



            return true;

        }



        private static bool IsAnimationPlaying(IReadOnlyList<MassacreCanvasFrame> frames, int currentTimeMs, int startTick, bool repeat)

        {

            if (frames == null || frames.Count == 0 || startTick == int.MinValue)

            {

                return false;

            }



            if (repeat)

            {

                return true;

            }



            int duration = frames.Sum(frame => Math.Max(1, frame.Delay));

            return currentTimeMs - startTick < duration;

        }



        private bool DrawAnimation(SpriteBatch spriteBatch, IReadOnlyList<MassacreCanvasFrame> frames, int currentTimeMs, int startTick, Vector2 anchor, bool repeat)

        {

            if (frames == null || frames.Count == 0 || startTick == int.MinValue)

            {

                return false;

            }



            MassacreCanvasFrame frame = ResolveAnimationFrame(frames, currentTimeMs, startTick, repeat);

            if (frame.Texture == null)

            {

                return false;

            }



            Vector2 drawPos = new(anchor.X - frame.Origin.X, anchor.Y - frame.Origin.Y);

            spriteBatch.Draw(frame.Texture, drawPos, Color.White);

            return true;

        }



        private static MassacreCanvasFrame ResolveAnimationFrame(IReadOnlyList<MassacreCanvasFrame> frames, int currentTimeMs, int startTick, bool repeat)

        {

            if (frames == null || frames.Count == 0)

            {

                return default;

            }



            long elapsed = Math.Max(0, currentTimeMs - startTick);

            int totalDuration = frames.Sum(frame => Math.Max(1, frame.Delay));

            if (repeat && totalDuration > 0)

            {

                elapsed %= totalDuration;

            }



            int cursor = 0;

            foreach (MassacreCanvasFrame frame in frames)

            {

                cursor += Math.Max(1, frame.Delay);

                if (elapsed < cursor)

                {

                    return frame;

                }

            }



            return frames[^1];

        }



        private static MassacreCanvasFrame LoadFrame(WzCanvasProperty canvas, GraphicsDevice device)

        {

            if (device == null || canvas == null)

            {

                return default;

            }



            using var bitmap = canvas.GetLinkedWzCanvasBitmap();

            Texture2D texture = bitmap?.ToTexture2DAndDispose(device);

            if (texture == null)

            {

                return default;

            }



            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();

            return new MassacreCanvasFrame(

                texture,

                new Point((int)origin.X, (int)origin.Y),

                Math.Max(1, canvas["delay"]?.GetInt() ?? 100));

        }



        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)

        {

            MassacreCanvasFrame frame = LoadFrame(canvas, _device);

            return frame.Texture;

        }



        private List<MassacreCanvasFrame> LoadAnimationFrames(WzImageProperty root)

        {

            if (_device == null || root?.WzProperties == null)

            {

                return null;

            }



            var frames = new List<MassacreCanvasFrame>();

            foreach (WzImageProperty child in root.WzProperties.OrderBy(ParseFrameOrder))

            {

                WzCanvasProperty canvas = ResolveCanvas(child);

                if (canvas == null)

                {

                    continue;

                }



                MassacreCanvasFrame frame = LoadFrame(canvas, _device);

                if (frame.Texture != null)

                {

                    frames.Add(frame);

                }

            }



            return frames.Count > 0 ? frames : null;

        }



        private static WzCanvasProperty FindTimerboardSourceCanvas(WzImage image)

        {

            foreach (WzImageProperty property in EnumeratePropertiesDepthFirst(image))

            {

                if (property is not WzCanvasProperty canvas)

                {

                    continue;

                }



                if (TryMatchCanvasSize(canvas, TimerboardWidth, TimerboardHeight))

                {

                    return canvas;

                }

            }



            return null;

        }



        private static bool TryMatchCanvasSize(WzCanvasProperty canvas, int width, int height)

        {

            try

            {

                using var bitmap = canvas.GetLinkedWzCanvasBitmap();

                return bitmap?.Width == width && bitmap.Height == height;

            }

            catch

            {

                return false;

            }

        }



        private static IEnumerable<WzImageProperty> EnumeratePropertiesDepthFirst(IPropertyContainer container)

        {

            if (container?.WzProperties == null)

            {

                yield break;

            }



            foreach (WzImageProperty child in container.WzProperties)

            {

                yield return child;

                if (child is IPropertyContainer childContainer)

                {

                    foreach (WzImageProperty descendant in EnumeratePropertiesDepthFirst(childContainer))

                    {

                        yield return descendant;

                    }

                }

            }

        }



        private void LoadDigitTextures(WzImageProperty source, Texture2D[] destination)

        {

            LoadDigitTextures(source, destination, out _);

        }



        private void LoadDigitTextures(WzImageProperty source, Texture2D[] destination, out Texture2D plusTexture)

        {

            plusTexture = null;

            if (destination == null)

            {

                return;

            }



            for (int i = 0; i < destination.Length; i++)

            {

                destination[i] ??= LoadCanvasTexture(ResolveCanvas(source?[i.ToString()]));

            }



            plusTexture = LoadCanvasTexture(ResolveCanvas(source?["plus"]));

        }



        private void LoadRankTextures(WzImageProperty source)

        {

            foreach (char rank in new[] { 'S', 'A', 'B', 'C', 'D' })

            {

                if (_rankTextures.ContainsKey(rank))

                {

                    continue;

                }



                WzCanvasProperty canvas = ResolveCanvas(source?[char.ToLowerInvariant(rank).ToString()]);

                MassacreCanvasFrame frame = LoadFrame(canvas, _device);

                if (frame.Texture != null)

                {

                    _rankTextures[rank] = frame;

                }

            }

        }



        private void LoadCountEffectNumberFrames(WzImageProperty source)

        {

            _countEffectNumberFrames.Clear();

            if (source?.WzProperties == null)

            {

                return;

            }



            foreach (WzImageProperty child in source.WzProperties)

            {

                if (!int.TryParse(child.Name, out int stage))

                {

                    continue;

                }



                List<MassacreCanvasFrame> frames = LoadAnimationFrames(child);

                if (frames != null)

                {

                    _countEffectNumberFrames[stage] = frames;

                }

            }

        }



        private static WzCanvasProperty ResolveCanvas(WzImageProperty property)

        {

            if (WzInfoTools.GetRealProperty(property) is WzCanvasProperty resolvedCanvas)

            {

                return resolvedCanvas;

            }



            if (property?.WzProperties == null)

            {

                return null;

            }



            if (property["0"] is WzCanvasProperty indexedCanvas)

            {

                return indexedCanvas;

            }



            return property.WzProperties.OfType<WzCanvasProperty>().FirstOrDefault();

        }



        private static int ParseFrameOrder(WzImageProperty property)

        {

            return int.TryParse(property?.Name, out int order) ? order : int.MaxValue;

        }



        private List<MassacreCanvasFrame> GetKeyFramesForStage(int stage)

        {

            return stage switch

            {

                0 => _keyOpenFrames,

                1 => _keyLoopFrames,

                2 => _keyCloseFrames,

                _ => null

            };

        }



        private List<MassacreCanvasFrame> GetCountEffectFramesForCurrentStage()

        {

            return _countEffectPresentationStage <= 1

                ? _countEffectFirstStartFrames

                : _countEffectStageFrames;

        }



        private List<MassacreCanvasFrame> GetCountEffectNumberFrames(int stage)

        {

            return _countEffectNumberFrames.TryGetValue(Math.Clamp(stage, 1, 5), out List<MassacreCanvasFrame> frames)

                ? frames

                : null;

        }



        private bool ShouldDrawDangerOverlay()

        {

            if (_maxGauge <= 0)

            {

                return false;

            }



            float depletion = 1f - Math.Clamp(_currentGauge / (float)_maxGauge, 0f, 1f);

            return depletion >= DangerDepletionThreshold;

        }



        private char ComputeResultRank()

        {

            float progress = Math.Clamp(_maxGauge <= 0 ? 0f : _currentGauge / (float)_maxGauge, 0f, 1f);

            return progress switch

            {

                >= 1f => 'S',

                >= 0.8f => 'A',

                >= 0.6f => 'B',

                >= 0.35f => 'C',

                _ => 'D'

            };

        }



        private static char NormalizeRank(char rank)

        {

            return char.ToUpperInvariant(rank) switch

            {

                'S' => 'S',

                'A' => 'A',

                'B' => 'B',

                'C' => 'C',

                _ => 'D'

            };

        }



        private readonly record struct MassacreCanvasFrame(Texture2D Texture, Point Origin, int Delay);

        private readonly record struct MassacreCountEffect(int Threshold, int? BuffItemId, bool RequiresSkillUse);

        private enum MassacreResultPresentation

        {

            None,

            Clear,

            Fail

        }

    }

    #endregion
}

