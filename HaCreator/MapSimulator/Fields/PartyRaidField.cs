using HaSharedLibrary.Wz;
using HaSharedLibrary.Util;
using MapleLib.Converters;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Fields
{
    public enum PartyRaidFieldMode { None, Field, Boss, Result }
    public enum PartyRaidResultOutcome { Unknown, Win, Lose, Clear }
    public enum PartyRaidTeamColor { Red, Blue }

    public sealed class PartyRaidField
    {
        private readonly struct CanvasSprite
        {
            public CanvasSprite(Texture2D texture, Point origin) { Texture = texture; Origin = origin; }
            public Texture2D Texture { get; }
            public Point Origin { get; }
            public bool IsLoaded => Texture != null;
            public int Width => Texture?.Width ?? 0;
            public int Height => Texture?.Height ?? 0;
        }

        private readonly struct AnimationFrame
        {
            public AnimationFrame(CanvasSprite sprite, int delayMs) { Sprite = sprite; DelayMs = delayMs; }
            public CanvasSprite Sprite { get; }
            public int DelayMs { get; }
            public bool IsLoaded => Sprite.IsLoaded;
        }

        private struct AnimationState
        {
            public bool Active;
            public int FrameIndex;
            public int FrameStartedAt;
        }

        private const int DefaultGaugeCapacity = 100000;
        private const int FieldBoardOffsetX = -119;
        private const int FieldBoardY = 70;
        private const int StateMineOffsetX = -39;
        private const int StateMineY = 88;
        private const int StateOtherOffsetX = -39;
        private const int StateOtherY = 110;
        private const int FieldStageDrawX = 99;
        private const int FieldStageDrawY = 20;
        private const int FieldPointDrawX = 99;
        private const int FieldPointDrawY = 2;
        private const int BossHudX = 0;
        private const int BossHudY = 40;
        private const int BossGaugeBackgrdX = 54;
        private const int BossGaugeTextY = 7;
        private const int BossGaugeIconX = 382;
        private const int BossGaugeIconY = 9;
        private const int BossGaugeFillX = 54;
        private const int BossGaugeFillY = 16;
        private const int BossGaugeLength = 322;
        private const int ResultWinX = 80;
        private const int ResultWinY = 56;
        private const int ResultLoseX = 67;
        private const int ResultLoseY = 56;
        private const int ResultPointX = 135;
        private const int ResultPointY = 133;
        private const int ResultBonusX = 135;
        private const int ResultBonusY = 157;
        private const int ResultTotalX = 137;
        private const int ResultTotalY = 194;
        private const int ResultEffectHoldMs = 1200;
        private const int TimerTextY = 18;

        private bool _isActive;
        private bool _assetsLoaded;
        private bool _pendingResultPresentation;
        private bool _timerExpiredTriggered;
        private int _mapId;
        private int _lastUpdateTick;
        private int _timerDurationSec;
        private int _timeOverTick;
        private PartyRaidFieldMode _mode;
        private PartyRaidTeamColor _teamColor;
        private int _stage;
        private int _point;
        private int _redDamage;
        private int _blueDamage;
        private int _gaugeCapacity;
        private int _resultPoint;
        private int _resultBonus;
        private int _resultTotal;
        private int _resultSideBorder;
        private int _resultTopBorder;
        private int _resultBottomBorder;
        private PartyRaidResultOutcome _resultOutcome;
        private GraphicsDevice _graphicsDevice;
        private CanvasSprite _fieldBoard;
        private CanvasSprite _redStateBackground;
        private CanvasSprite _blueStateBackground;
        private CanvasSprite _bossGaugeBackground;
        private CanvasSprite _bossGaugeText;
        private CanvasSprite _bossGaugeFillPixel;
        private CanvasSprite _bossGaugeMobIcon;
        private CanvasSprite _bossPointBoard;
        private CanvasSprite _resultBackground;
        private CanvasSprite _resultWinBadge;
        private CanvasSprite _resultLoseBadge;
        private readonly CanvasSprite[] _fieldStageDigits = new CanvasSprite[6];
        private readonly CanvasSprite[] _fieldPointDigits = new CanvasSprite[10];
        private readonly CanvasSprite[] _resultDigits = new CanvasSprite[10];
        private readonly CanvasSprite[] _resultBigDigits = new CanvasSprite[10];
        private readonly List<AnimationFrame> _redMineFrames = new();
        private readonly List<AnimationFrame> _redOtherFrames = new();
        private readonly List<AnimationFrame> _blueMineFrames = new();
        private readonly List<AnimationFrame> _blueOtherFrames = new();
        private readonly List<AnimationFrame> _clearResultFrames = new();
        private readonly List<AnimationFrame> _timeoutResultFrames = new();
        private AnimationState _mineAnimation;
        private AnimationState _otherAnimation;
        private AnimationState _resultEffectAnimation;
        private int _resultEffectVisibleUntil;
        private List<AnimationFrame> _activeResultEffectFrames;

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public PartyRaidFieldMode Mode => _mode;
        public PartyRaidTeamColor TeamColor => _teamColor;
        public int Stage => _stage;
        public int Point => _point;
        public int RedDamage => _redDamage;
        public int BlueDamage => _blueDamage;
        public int GaugeCapacity => _gaugeCapacity;
        public int ResultPoint => _resultPoint;
        public int ResultBonus => _resultBonus;
        public int ResultTotal => _resultTotal;
        public PartyRaidResultOutcome ResultOutcome => _resultOutcome;
        public bool HasRunningClock => _timeOverTick != int.MinValue;
        public int RemainingSeconds
        {
            get
            {
                if (_timeOverTick == int.MinValue)
                {
                    return 0;
                }

                int remainingMs = _timeOverTick - Environment.TickCount;
                return remainingMs <= 0 ? 0 : (remainingMs + 999) / 1000;
            }
        }

        public void Initialize(GraphicsDevice device)
        {
            _graphicsDevice = device;
            EnsureAssetsLoaded(device);
        }

        public void BindMap(MapInfo mapInfo)
        {
            ResetState();
            if (mapInfo == null || mapInfo.fieldType == null)
            {
                return;
            }

            PartyRaidFieldMode mode = GetMode((FieldType)mapInfo.fieldType);
            if (mode == PartyRaidFieldMode.None)
            {
                return;
            }

            _isActive = true;
            _mapId = mapInfo.id;
            _mode = mode;
            _stage = 1;
            _gaugeCapacity = DefaultGaugeCapacity;
            _teamColor = InferTeamColor(mapInfo.id);
            _resultSideBorder = Math.Max(0, mapInfo.LBSide ?? 0);
            _resultTopBorder = Math.Max(0, mapInfo.LBTop ?? 0);
            _resultBottomBorder = Math.Max(0, mapInfo.LBBottom ?? 0);
            _resultPoint = -1;
            _resultBonus = -1;
            _resultTotal = -1;
            _resultOutcome = InferOutcomeFromMap(mapInfo);
        }

        public void Update(int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }

            EnsureAssetsLoaded(_graphicsDevice);
            if (_lastUpdateTick == 0)
            {
                _lastUpdateTick = currentTimeMs;
            }

            if (_pendingResultPresentation)
            {
                _pendingResultPresentation = false;
                StartResultEffect(GetResultEffectFrames(_resultOutcome), currentTimeMs);
            }

            AdvanceAnimation(_mineAnimation.Active ? GetMineFrames() : null, ref _mineAnimation, currentTimeMs, true);
            AdvanceAnimation(_otherAnimation.Active ? GetOtherFrames() : null, ref _otherAnimation, currentTimeMs, true);
            AdvanceAnimation(_activeResultEffectFrames, ref _resultEffectAnimation, currentTimeMs, false);

            if (_resultEffectAnimation.Active && currentTimeMs >= _resultEffectVisibleUntil)
            {
                _resultEffectAnimation.Active = false;
                _activeResultEffectFrames = null;
            }

            if (_timeOverTick != int.MinValue && !_timerExpiredTriggered && currentTimeMs >= _timeOverTick)
            {
                _timerExpiredTriggered = true;
                if (_mode != PartyRaidFieldMode.Result)
                {
                    StartResultEffect(_timeoutResultFrames, currentTimeMs);
                }
            }

            _lastUpdateTick = currentTimeMs;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount, Texture2D pixelTexture, SpriteFont font = null)
        {
            if (!_isActive || spriteBatch == null)
            {
                return;
            }

            EnsureAssetsLoaded(spriteBatch.GraphicsDevice);
            switch (_mode)
            {
                case PartyRaidFieldMode.Field:
                    DrawFieldHud(spriteBatch, centerX, pixelTexture, font);
                    break;
                case PartyRaidFieldMode.Boss:
                    DrawBossHud(spriteBatch, pixelTexture, font);
                    break;
                case PartyRaidFieldMode.Result:
                    DrawResultHud(spriteBatch, pixelTexture, font);
                    break;
            }

            DrawResultEffect(spriteBatch);
            DrawTimer(spriteBatch, font);
        }

        public void SetStage(int stage) => _stage = Math.Clamp(stage, 1, 5);
        public void SetPoint(int point) => _point = Math.Max(0, point);
        public void SetGaugeCapacity(int gaugeCapacity) => _gaugeCapacity = Math.Max(1, gaugeCapacity);

        public void SetBossDamage(int redDamage, int blueDamage)
        {
            _redDamage = Math.Max(0, redDamage);
            _blueDamage = Math.Max(0, blueDamage);
        }

        public void SetTeamColor(PartyRaidTeamColor teamColor)
        {
            if (_teamColor == teamColor)
            {
                return;
            }

            _teamColor = teamColor;
            _mineAnimation = CreateLoopingAnimation();
            _otherAnimation = CreateLoopingAnimation();
        }

        public void SetResultValues(int point, int bonus, int total)
        {
            _resultPoint = Math.Max(0, point);
            _resultBonus = Math.Max(0, bonus);
            _resultTotal = Math.Max(0, total);
            if (_mode == PartyRaidFieldMode.Result)
            {
                _pendingResultPresentation = true;
            }
        }

        public void SetResultOutcome(PartyRaidResultOutcome outcome)
        {
            _resultOutcome = outcome;
            if (_mode == PartyRaidFieldMode.Result && HasResultValues())
            {
                _pendingResultPresentation = true;
            }
        }

        public void OnClock(int clockType, int durationSec, int currentTimeMs)
        {
            if (!_isActive || clockType != 2)
            {
                return;
            }

            _timerDurationSec = Math.Max(0, durationSec);
            _timeOverTick = _timerDurationSec > 0 ? currentTimeMs + (_timerDurationSec * 1000) : int.MinValue;
            _timerExpiredTriggered = false;
            if (_timeOverTick == int.MinValue)
            {
                _timerDurationSec = 0;
            }
        }

        public void ClearClock()
        {
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _timerExpiredTriggered = false;
        }
        public bool OnFieldSetVariable(string key, string value)
        {
            if (MatchesAlias(key, "team", "color", "state"))
            {
                if (TryParseTeamColor(value, out PartyRaidTeamColor teamColor))
                {
                    SetTeamColor(teamColor);
                    return true;
                }

                return false;
            }

            if (MatchesAlias(key, "outcome", "result"))
            {
                if (TryParseOutcome(value, out PartyRaidResultOutcome outcome))
                {
                    SetResultOutcome(outcome);
                    return true;
                }

                return false;
            }

            if (!TryParseNonNegative(value, out int parsedValue))
            {
                return false;
            }

            if (MatchesAlias(key, "redDamage", "red", "damage_r", "partyraid_red"))
            {
                _redDamage = parsedValue;
                return true;
            }

            if (MatchesAlias(key, "blueDamage", "blue", "damage_b", "partyraid_blue"))
            {
                _blueDamage = parsedValue;
                return true;
            }

            if (MatchesAlias(key, "stage"))
            {
                _stage = Math.Clamp(parsedValue, 1, 5);
                return true;
            }

            if (MatchesAlias(key, "gaugeCap", "maxhp", "gauge_capacity"))
            {
                _gaugeCapacity = Math.Max(1, parsedValue);
                return true;
            }

            return false;
        }

        public bool OnPartyValue(string key, string value)
        {
            if (!TryParseNonNegative(value, out int parsedValue))
            {
                return false;
            }

            if (MatchesAlias(key, "point", "partyPoint", "pt"))
            {
                _point = parsedValue;
                return true;
            }

            return false;
        }

        public bool OnSessionValue(string key, string value)
        {
            if (!TryParseNonNegative(value, out int parsedValue))
            {
                return false;
            }

            if (MatchesAlias(key, "point", "partyPoint", "pt"))
            {
                _resultPoint = parsedValue;
            }
            else if (MatchesAlias(key, "bonus", "rewardBonus"))
            {
                _resultBonus = parsedValue;
            }
            else if (MatchesAlias(key, "total", "sum"))
            {
                _resultTotal = parsedValue;
            }
            else
            {
                return false;
            }

            if (_mode == PartyRaidFieldMode.Result && HasResultValues())
            {
                _pendingResultPresentation = true;
            }

            return true;
        }

        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Party Raid runtime inactive.";
            }

            string timerText = HasRunningClock ? $", timer={FormatTimer(RemainingSeconds)}" : string.Empty;
            return _mode switch
            {
                PartyRaidFieldMode.Field => $"Party Raid field map {_mapId}: team {GetTeamLabel(_teamColor)}, stage {_stage}, point {_point}{timerText}.",
                PartyRaidFieldMode.Boss => $"Party Raid boss map {_mapId}: point {_point}, red damage {_redDamage}, blue damage {_blueDamage}, gauge cap {_gaugeCapacity}{timerText}.",
                PartyRaidFieldMode.Result => $"Party Raid result map {_mapId}: point {_resultPoint}, bonus {_resultBonus}, total {_resultTotal}, outcome {GetOutcomeLabel(_resultOutcome)}{timerText}.",
                _ => "Party Raid runtime inactive."
            };
        }

        public void Reset()
        {
            _isActive = false;
            ResetState();
        }

        private void ResetState()
        {
            _mapId = 0;
            _mode = PartyRaidFieldMode.None;
            _teamColor = PartyRaidTeamColor.Red;
            _stage = 0;
            _point = 0;
            _redDamage = 0;
            _blueDamage = 0;
            _gaugeCapacity = DefaultGaugeCapacity;
            _resultPoint = -1;
            _resultBonus = -1;
            _resultTotal = -1;
            _resultSideBorder = 0;
            _resultTopBorder = 0;
            _resultBottomBorder = 0;
            _resultOutcome = PartyRaidResultOutcome.Unknown;
            _lastUpdateTick = 0;
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _timerExpiredTriggered = false;
            _pendingResultPresentation = false;
            _mineAnimation = CreateLoopingAnimation();
            _otherAnimation = CreateLoopingAnimation();
            _resultEffectAnimation = default;
            _activeResultEffectFrames = null;
            _resultEffectVisibleUntil = 0;
        }

        private void EnsureAssetsLoaded(GraphicsDevice graphicsDevice)
        {
            if (_assetsLoaded || graphicsDevice == null)
            {
                return;
            }

            _graphicsDevice = graphicsDevice;
            WzImage uiWindow = global::HaCreator.Program.FindImage("UI", "UIWindow.img") ?? global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage effectImage = global::HaCreator.Program.FindImage("Map", "Effect.img");
            uiWindow?.ParseImage();
            effectImage?.ParseImage();

            WzImageProperty partyRace = uiWindow?["PartyRace"];
            WzImageProperty stage = partyRace?["Stage"];
            WzImageProperty state = partyRace?["State"];
            WzImageProperty result = partyRace?["Result"];
            WzImageProperty dualMobGauge = uiWindow?["DualMobGauge"];

            _fieldBoard = LoadCanvas(stage?["backgrd"]);
            _bossPointBoard = LoadCanvas(stage?["backgrd"]);
            LoadDigits(stage?["number"], _fieldStageDigits, 1, 5);
            LoadDigits(stage?["number2"], _fieldPointDigits, 0, 9);

            _redStateBackground = LoadCanvas(state?["Red"]?["backgrd"]);
            _blueStateBackground = LoadCanvas(state?["Blue"]?["backgrd"]);
            LoadAnimation(state?["Red"]?["effMine"], _redMineFrames);
            LoadAnimation(state?["Red"]?["effOther"], _redOtherFrames);
            LoadAnimation(state?["Blue"]?["effMine"], _blueMineFrames);
            LoadAnimation(state?["Blue"]?["effOther"], _blueOtherFrames);

            _bossGaugeBackground = LoadCanvas(dualMobGauge?["backgrd"]);
            _bossGaugeText = LoadCanvas(dualMobGauge?["text"]);
            _bossGaugeFillPixel = LoadCanvas(dualMobGauge?["gauge"]);
            CanvasSprite mobIcon = LoadCanvas(dualMobGauge?["Mob"]?["9700036"]);
            _bossGaugeMobIcon = mobIcon.IsLoaded ? mobIcon : LoadCanvas(dualMobGauge?["Mob"]?["9500401"]);

            _resultBackground = LoadCanvas(result?["backgrd"]);
            _resultWinBadge = LoadCanvas(result?["win"]);
            _resultLoseBadge = LoadCanvas(result?["lose"]);
            LoadDigits(result?["number"], _resultDigits, 0, 9);
            LoadDigits(result?["number2"], _resultBigDigits, 0, 9);

            LoadAnimation(effectImage?["praid"]?["clear"], _clearResultFrames);
            LoadAnimation(effectImage?["praid"]?["timeout"], _timeoutResultFrames);
            _assetsLoaded = true;
        }

        private CanvasSprite LoadCanvas(WzImageProperty source)
        {
            if (_graphicsDevice == null || source == null)
            {
                return default;
            }

            if (WzInfoTools.GetRealProperty(source) is not WzCanvasProperty canvas)
            {
                return default;
            }

            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return default;
            }

            Texture2D texture = bitmap.ToTexture2DAndDispose(_graphicsDevice);
            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            return new CanvasSprite(texture, new Point((int)origin.X, (int)origin.Y));
        }

        private void LoadDigits(WzImageProperty source, CanvasSprite[] target, int minDigit, int maxDigit)
        {
            Array.Clear(target, 0, target.Length);
            if (source == null)
            {
                return;
            }

            for (int digit = minDigit; digit <= maxDigit && digit < target.Length; digit++)
            {
                target[digit] = LoadCanvas(source[digit.ToString(CultureInfo.InvariantCulture)]);
            }
        }

        private void LoadAnimation(WzImageProperty source, List<AnimationFrame> target)
        {
            target.Clear();
            if (source is not WzSubProperty subProperty)
            {
                return;
            }

            List<(int Index, WzImageProperty Property)> ordered = new();
            foreach (WzImageProperty child in subProperty.WzProperties)
            {
                if (int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    ordered.Add((index, child));
                }
            }

            ordered.Sort(static (left, right) => left.Index.CompareTo(right.Index));
            for (int i = 0; i < ordered.Count; i++)
            {
                if (WzInfoTools.GetRealProperty(ordered[i].Property) is not WzCanvasProperty canvas)
                {
                    continue;
                }

                CanvasSprite sprite = LoadCanvas(canvas);
                if (!sprite.IsLoaded)
                {
                    continue;
                }

                target.Add(new AnimationFrame(sprite, GetCanvasDelay(canvas)));
            }
        }

        private static int GetCanvasDelay(WzCanvasProperty canvas)
        {
            if (canvas?["delay"] is WzIntProperty delayProperty)
            {
                return Math.Max(1, delayProperty.Value);
            }

            return 100;
        }
        private void DrawFieldHud(SpriteBatch spriteBatch, int centerX, Texture2D pixelTexture, SpriteFont font)
        {
            CanvasSprite stateBackground = GetStateBackground();
            if (stateBackground.IsLoaded)
            {
                DrawSprite(spriteBatch, stateBackground, centerX + FieldBoardOffsetX, FieldBoardY);
            }

            DrawAnimationFrame(spriteBatch, GetMineFrames(), _mineAnimation, centerX + StateMineOffsetX, StateMineY);
            DrawAnimationFrame(spriteBatch, GetOtherFrames(), _otherAnimation, centerX + StateOtherOffsetX, StateOtherY);

            if (_fieldBoard.IsLoaded)
            {
                DrawSprite(spriteBatch, _fieldBoard, centerX + FieldBoardOffsetX, FieldBoardY);
                DrawNumber(spriteBatch, _fieldPointDigits, _point, centerX + FieldBoardOffsetX + FieldPointDrawX, FieldBoardY + FieldPointDrawY);
                DrawSingleDigit(spriteBatch, _fieldStageDigits, Math.Clamp(_stage, 1, 5), centerX + FieldBoardOffsetX + FieldStageDrawX, FieldBoardY + FieldStageDrawY);
                return;
            }

            if (pixelTexture != null)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(centerX + FieldBoardOffsetX, FieldBoardY, 238, 58), new Color(26, 27, 35, 220));
            }

            if (font != null)
            {
                DrawOutlinedString(spriteBatch, font, $"STAGE {_stage}", new Vector2(centerX - 40, FieldBoardY + 8), Color.Gold);
                DrawOutlinedString(spriteBatch, font, $"POINT {_point}", new Vector2(centerX - 40, FieldBoardY + 30), Color.White);
            }
        }

        private void DrawBossHud(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (_bossGaugeBackground.IsLoaded)
            {
                DrawSprite(spriteBatch, _bossGaugeBackground, BossHudX + BossGaugeBackgrdX, BossHudY);
            }

            if (_bossPointBoard.IsLoaded)
            {
                DrawSprite(spriteBatch, _bossPointBoard, BossHudX, BossHudY);
                DrawNumber(spriteBatch, _fieldPointDigits, _point, BossHudX + FieldPointDrawX, BossHudY + FieldPointDrawY);
            }

            if (_bossGaugeFillPixel.IsLoaded)
            {
                int redRemaining = GetRemainingGaugeWidth(_redDamage);
                int blueRemaining = GetRemainingGaugeWidth(_blueDamage);
                for (int x = 0; x < redRemaining; x++)
                {
                    DrawSprite(spriteBatch, _bossGaugeFillPixel, BossHudX + BossGaugeFillX + x, BossHudY + BossGaugeFillY);
                }

                for (int x = 0; x < blueRemaining; x++)
                {
                    DrawSprite(spriteBatch, _bossGaugeFillPixel, BossHudX + BossGaugeFillX + BossGaugeLength - 1 - x, BossHudY + BossGaugeFillY);
                }
            }

            if (_bossGaugeText.IsLoaded)
            {
                DrawSprite(spriteBatch, _bossGaugeText, BossHudX, BossHudY + BossGaugeTextY);
            }

            if (_bossGaugeMobIcon.IsLoaded)
            {
                DrawSprite(spriteBatch, _bossGaugeMobIcon, BossHudX + BossGaugeIconX, BossHudY + BossGaugeIconY);
            }

            if (!_bossGaugeBackground.IsLoaded && pixelTexture != null)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(0, BossHudY, 436, 48), new Color(14, 16, 20, 220));
            }

            if (font != null && !_bossGaugeText.IsLoaded)
            {
                DrawOutlinedString(spriteBatch, font, $"RED {_redDamage}", new Vector2(8, BossHudY + 10), Color.IndianRed);
                DrawOutlinedString(spriteBatch, font, $"BLUE {_blueDamage}", new Vector2(270, BossHudY + 10), Color.LightSkyBlue);
            }
        }

        private void DrawResultHud(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            int backgroundHeight = _resultBackground.IsLoaded ? _resultBackground.Height : 259;
            int backgroundWidth = Math.Max(_resultBackground.Width, 260);
            int safeLeft = Math.Clamp(_resultSideBorder, 0, viewport.Width);
            int safeTop = Math.Clamp(_resultTopBorder, 0, viewport.Height);
            int safeWidth = Math.Max(0, viewport.Width - (safeLeft * 2));
            int safeHeight = Math.Max(0, viewport.Height - safeTop - Math.Clamp(_resultBottomBorder, 0, viewport.Height));
            int left = safeLeft + Math.Max(0, (safeWidth - backgroundWidth) / 2);
            int top = safeTop + Math.Max(0, (safeHeight - Math.Max(backgroundHeight, 259)) / 2);

            if (_resultBackground.IsLoaded)
            {
                DrawSprite(spriteBatch, _resultBackground, left, top);
            }
            else if (pixelTexture != null)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(left, top, 260, 259), new Color(10, 12, 18, 230));
            }

            CanvasSprite badge = UsesWinBadge(_resultOutcome) ? _resultWinBadge : _resultLoseBadge;
            if (badge.IsLoaded)
            {
                int badgeX = UsesWinBadge(_resultOutcome) ? ResultWinX : ResultLoseX;
                int badgeY = UsesWinBadge(_resultOutcome) ? ResultWinY : ResultLoseY;
                DrawSprite(spriteBatch, badge, left + badgeX, top + badgeY);
            }

            DrawNumber(spriteBatch, _resultDigits, Math.Max(0, _resultPoint), left + ResultPointX, top + ResultPointY);
            DrawNumber(spriteBatch, _resultDigits, Math.Max(0, _resultBonus), left + ResultBonusX, top + ResultBonusY);
            DrawNumber(spriteBatch, _resultBigDigits, Math.Max(0, _resultTotal), left + ResultTotalX, top + ResultTotalY);

            if (!_resultBackground.IsLoaded && font != null)
            {
                DrawOutlinedString(spriteBatch, font, GetOutcomeLabel(_resultOutcome), new Vector2(left + 86, top + 60), Color.White);
            }
        }

        private void DrawResultEffect(SpriteBatch spriteBatch)
        {
            if (!_resultEffectAnimation.Active || _activeResultEffectFrames == null || _activeResultEffectFrames.Count == 0)
            {
                return;
            }

            AnimationFrame frame = _activeResultEffectFrames[Math.Clamp(_resultEffectAnimation.FrameIndex, 0, _activeResultEffectFrames.Count - 1)];
            if (!frame.IsLoaded)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            DrawSprite(spriteBatch, frame.Sprite, (viewport.Width / 2) - frame.Sprite.Origin.X, (viewport.Height / 2) - frame.Sprite.Origin.Y);
        }

        private void DrawTimer(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!HasRunningClock || font == null)
            {
                return;
            }

            string timerText = FormatTimer(RemainingSeconds);
            Vector2 size = font.MeasureString(timerText);
            DrawOutlinedString(spriteBatch, font, timerText, new Vector2((spriteBatch.GraphicsDevice.Viewport.Width - size.X) / 2f, TimerTextY), Color.White);
        }

        private void DrawSprite(SpriteBatch spriteBatch, CanvasSprite sprite, int x, int y)
        {
            if (!sprite.IsLoaded)
            {
                return;
            }

            spriteBatch.Draw(sprite.Texture, new Vector2(x - sprite.Origin.X, y - sprite.Origin.Y), Color.White);
        }

        private void DrawAnimationFrame(SpriteBatch spriteBatch, List<AnimationFrame> frames, AnimationState state, int x, int y)
        {
            if (frames == null || frames.Count == 0 || !state.Active)
            {
                return;
            }

            DrawSprite(spriteBatch, frames[Math.Clamp(state.FrameIndex, 0, frames.Count - 1)].Sprite, x, y);
        }

        private void DrawSingleDigit(SpriteBatch spriteBatch, CanvasSprite[] digits, int digit, int x, int y)
        {
            if (digit < 0 || digit >= digits.Length || !digits[digit].IsLoaded)
            {
                return;
            }

            DrawSprite(spriteBatch, digits[digit], x, y);
        }

        private void DrawNumber(SpriteBatch spriteBatch, CanvasSprite[] digits, int number, int x, int y)
        {
            string text = Math.Max(0, number).ToString(CultureInfo.InvariantCulture);
            int drawX = x;
            for (int i = 0; i < text.Length; i++)
            {
                int digit = text[i] - '0';
                if (digit < 0 || digit >= digits.Length || !digits[digit].IsLoaded)
                {
                    continue;
                }

                DrawSprite(spriteBatch, digits[digit], drawX, y);
                drawX += digits[digit].Width;
            }
        }

        private void StartResultEffect(List<AnimationFrame> frames, int currentTimeMs)
        {
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            _activeResultEffectFrames = frames;
            _resultEffectAnimation.Active = true;
            _resultEffectAnimation.FrameIndex = 0;
            _resultEffectAnimation.FrameStartedAt = currentTimeMs;
            _resultEffectVisibleUntil = currentTimeMs + GetAnimationDuration(frames) + ResultEffectHoldMs;
        }

        private static int GetAnimationDuration(List<AnimationFrame> frames)
        {
            int total = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                total += Math.Max(1, frames[i].DelayMs);
            }

            return total;
        }
        private void AdvanceAnimation(List<AnimationFrame> frames, ref AnimationState state, int currentTimeMs, bool loop)
        {
            if (!state.Active || frames == null || frames.Count == 0)
            {
                return;
            }

            if (state.FrameStartedAt == 0)
            {
                state.FrameStartedAt = currentTimeMs;
            }

            while (true)
            {
                int delay = Math.Max(1, frames[Math.Clamp(state.FrameIndex, 0, frames.Count - 1)].DelayMs);
                if (currentTimeMs - state.FrameStartedAt < delay)
                {
                    break;
                }

                state.FrameStartedAt += delay;
                state.FrameIndex++;
                if (state.FrameIndex < frames.Count)
                {
                    continue;
                }

                if (loop)
                {
                    state.FrameIndex = 0;
                    continue;
                }

                state.FrameIndex = frames.Count - 1;
                state.Active = false;
                break;
            }
        }

        private static AnimationState CreateLoopingAnimation() => new() { Active = true, FrameIndex = 0, FrameStartedAt = 0 };
        private CanvasSprite GetStateBackground() => _teamColor == PartyRaidTeamColor.Blue ? _blueStateBackground : _redStateBackground;
        private List<AnimationFrame> GetMineFrames() => _teamColor == PartyRaidTeamColor.Blue ? _blueMineFrames : _redMineFrames;
        private List<AnimationFrame> GetOtherFrames() => _teamColor == PartyRaidTeamColor.Blue ? _blueOtherFrames : _redOtherFrames;
        private List<AnimationFrame> GetResultEffectFrames(PartyRaidResultOutcome outcome) => outcome switch { PartyRaidResultOutcome.Lose => _timeoutResultFrames, PartyRaidResultOutcome.Win => _clearResultFrames, PartyRaidResultOutcome.Clear => _clearResultFrames, _ => null };
        private bool HasResultValues() => _resultPoint >= 0 && _resultBonus >= 0 && _resultTotal >= 0;

        private int GetRemainingGaugeWidth(int damage)
        {
            int halfGaugeCapacity = Math.Max(1, _gaugeCapacity / 2);
            int clampedDamage = Math.Clamp(damage, 0, halfGaugeCapacity);
            int remainingHp = Math.Max(0, halfGaugeCapacity - clampedDamage);
            int width = (BossGaugeLength * remainingHp) / halfGaugeCapacity;
            return remainingHp > 0 && width == 0 ? 1 : Math.Clamp(width, 0, BossGaugeLength);
        }

        private static PartyRaidFieldMode GetMode(FieldType fieldType) => fieldType switch
        {
            FieldType.FIELDTYPE_PARTYRAID => PartyRaidFieldMode.Field,
            FieldType.FIELDTYPE_PARTYRAID_BOSS => PartyRaidFieldMode.Boss,
            FieldType.FIELDTYPE_PARTYRAID_RESULT => PartyRaidFieldMode.Result,
            _ => PartyRaidFieldMode.None
        };

        private static PartyRaidResultOutcome InferOutcomeFromMap(MapInfo mapInfo)
        {
            if (!string.IsNullOrWhiteSpace(mapInfo?.onUserEnter))
            {
                if (mapInfo.onUserEnter.StartsWith("PRaid_Win", StringComparison.OrdinalIgnoreCase))
                {
                    return PartyRaidResultOutcome.Win;
                }

                if (mapInfo.onUserEnter.StartsWith("PRaid_Fail", StringComparison.OrdinalIgnoreCase))
                {
                    return PartyRaidResultOutcome.Lose;
                }
            }

            return InferOutcomeFromMapId(mapInfo?.id ?? 0);
        }

        private static PartyRaidResultOutcome InferOutcomeFromMapId(int mapId) => mapId switch
        {
            923020010 => PartyRaidResultOutcome.Win,
            923020020 => PartyRaidResultOutcome.Lose,
            _ => PartyRaidResultOutcome.Unknown
        };

        private static PartyRaidTeamColor InferTeamColor(int mapId) => mapId == 923020120 ? PartyRaidTeamColor.Blue : PartyRaidTeamColor.Red;
        private static bool UsesWinBadge(PartyRaidResultOutcome outcome) => outcome == PartyRaidResultOutcome.Win || outcome == PartyRaidResultOutcome.Clear;
        private static string GetOutcomeLabel(PartyRaidResultOutcome outcome) => outcome switch { PartyRaidResultOutcome.Win => "WIN", PartyRaidResultOutcome.Lose => "LOSE", PartyRaidResultOutcome.Clear => "CLEAR", _ => "RESULT" };
        private static string GetTeamLabel(PartyRaidTeamColor teamColor) => teamColor == PartyRaidTeamColor.Blue ? "blue" : "red";
        private static string FormatTimer(int remainingSeconds) { int clamped = Math.Max(0, remainingSeconds); return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", clamped / 60, clamped % 60); }

        private static bool TryParseTeamColor(string text, out PartyRaidTeamColor teamColor)
        {
            if (string.Equals(text, "blue", StringComparison.OrdinalIgnoreCase)) { teamColor = PartyRaidTeamColor.Blue; return true; }
            if (string.Equals(text, "red", StringComparison.OrdinalIgnoreCase)) { teamColor = PartyRaidTeamColor.Red; return true; }
            if (string.Equals(text, "1", StringComparison.OrdinalIgnoreCase)) { teamColor = PartyRaidTeamColor.Blue; return true; }
            if (string.Equals(text, "0", StringComparison.OrdinalIgnoreCase)) { teamColor = PartyRaidTeamColor.Red; return true; }
            teamColor = PartyRaidTeamColor.Red;
            return false;
        }

        private static bool TryParseOutcome(string text, out PartyRaidResultOutcome outcome)
        {
            if (string.Equals(text, "win", StringComparison.OrdinalIgnoreCase))
            {
                outcome = PartyRaidResultOutcome.Win;
                return true;
            }

            if (string.Equals(text, "lose", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "fail", StringComparison.OrdinalIgnoreCase))
            {
                outcome = PartyRaidResultOutcome.Lose;
                return true;
            }

            if (string.Equals(text, "clear", StringComparison.OrdinalIgnoreCase))
            {
                outcome = PartyRaidResultOutcome.Clear;
                return true;
            }

            outcome = PartyRaidResultOutcome.Unknown;
            return false;
        }

        private static bool MatchesAlias(string key, params string[] aliases)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            string normalized = NormalizeKey(key);
            for (int i = 0; i < aliases.Length; i++)
            {
                if (normalized == NormalizeKey(aliases[i])) return true;
            }
            return false;
        }

        private static string NormalizeKey(string key) => key.Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();

        private static bool TryParseNonNegative(string value, out int parsedValue)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                parsedValue = 0;
                return false;
            }

            parsedValue = Math.Max(0, parsedValue);
            return true;
        }

        private static void DrawOutlinedString(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color)
        {
            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }
    }
}
