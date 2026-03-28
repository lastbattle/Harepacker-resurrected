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
    #region Dojo Field (CField_Dojang)
    /// <summary>
    /// Mu Lung Dojo field HUD.
    ///
    /// Client evidence:
    /// - CField_Dojang::OnClock (0x550940) only reacts to clock type 2, stores a duration in seconds,
    ///   creates a dedicated timer layer, and refreshes the timer immediately.
    /// - CField_Dojang::Update (0x54ef10) continuously mirrors boss HP, player HP, and energy into
    ///   dedicated HUD layers before calling UpdateTimer again.
    ///
    /// This simulator pass mirrors the Dojo-specific timer plus the three gauge surfaces behind a
    /// stable runtime seam. Stage effects and packet-driven result flow still need follow-up work.
    /// </summary>
    public class DojoField
    {
        private const int PacketTypeClock = 1;
        private const int TimerLayerOffsetX = -55;
        private const int TimerLayerY = 16;
        private const int ClockOffsetY = 26;
        private static readonly Point ClockOrigin = new(102, 26);
        private const int PlayerOffsetX = -231;
        private const int PlayerOffsetY = 50;
        private static readonly Point PlayerOrigin = new(160, 28);
        private const int MonsterOffsetX = 231;
        private const int MonsterOffsetY = 50;
        private static readonly Point MonsterOrigin = new(160, 28);
        private const int EnergyOffsetX = 20;
        private const int EnergyOffsetY = 130;
        private const int TimerMinuteX = 0;
        private const int TimerSecondX = 68;
        private const int TimerDigitSpacing = 23;
        private const int TimerDigitY = 0;
        private const int TimerColonX = 51;
        private const int TimerColonY = 4;
        private const int BarGaugeWidth = 305;
        private const int BarGaugeHeight = 13;
        private const int PlayerGaugeOffsetX = 7;
        private const int MonsterGaugeOffsetX = 7;
        private const int BarGaugeOffsetY = 6;
        private const int EnergyGaugeWidth = 9;
        private const int EnergyGaugeHeight = 77;
        private const int EnergyGaugeOffsetX = 7;
        private const int EnergyGaugeOffsetY = 7;
        private const int EnergyMax = 10000;
        private bool _isActive;
        private int _mapId;
        private int _stage;
        private int _timerDurationSec;
        private int _timeOverTick = int.MinValue;
        private int _lastClockUpdateTick = int.MinValue;
        private int _returnMapId = -1;
        private int _forcedReturnMapId = -1;
        private bool _hasNextFloorPortal;
        private int _pendingTransferMapId = -1;
        private int _pendingTransferAtTick = int.MinValue;
        private int _playerHp;
        private int _playerMaxHp = 100;
        private float? _bossHpPercent;
        private float? _lastBossHpPercent;
        private int _energy;
        private int _stageBannerStartTick = int.MinValue;
        private int _resultEffectStartTick = int.MinValue;
        private GraphicsDevice _device;
        private bool _assetsLoaded;
        private Texture2D _clockTexture;
        private Texture2D _playerTexture;
        private Texture2D _playerGaugeTexture;
        private Texture2D _monsterTexture;
        private Texture2D _monsterGaugeTexture;
        private Texture2D _energyTexture;
        private Texture2D _energyGaugeTexture;
        private Texture2D _timerColonTexture;
        private readonly Texture2D[] _digitTextures = new Texture2D[10];
        private List<DojoFrame> _energyFullFrames;
        private List<DojoFrame> _stageFrames;
        private readonly Dictionary<int, List<DojoFrame>> _stageNumberFrames = new();
        private List<DojoFrame> _clearFrames;
        private List<DojoFrame> _timeOverFrames;
        private DojoResultEffect _resultEffect = DojoResultEffect.None;
        public bool IsActive => _isActive;
        public int Stage => _stage;
        public int Energy => _energy;
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
            EnsureAssetsLoaded();
        }
        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _stage = ResolveStage(mapId);
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastClockUpdateTick = int.MinValue;
            _returnMapId = -1;
            _forcedReturnMapId = -1;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _playerHp = 0;
            _playerMaxHp = 100;
            _bossHpPercent = null;
            _lastBossHpPercent = null;
            _energy = 0;
            EnsureAssetsLoaded();
            _stageBannerStartTick = Environment.TickCount;
            _resultEffectStartTick = int.MinValue;
            _resultEffect = DojoResultEffect.None;
        }
        public void Configure(MapInfo mapInfo, bool hasNextFloorPortal = false)
        {
            _returnMapId = NormalizeTransferMapId(mapInfo?.returnMap);
            _forcedReturnMapId = NormalizeTransferMapId(mapInfo?.forcedReturn);
            _hasNextFloorPortal = hasNextFloorPortal || HasPortalScript(_mapId, "dojang_next");
        }
        public void OnClock(int clockType, int durationSec, int currentTimeMs)
        {
            if (clockType != 2)
            {
                return;
            }
            _timerDurationSec = Math.Max(0, durationSec);
            _timeOverTick = currentTimeMs + (_timerDurationSec * 1000);
            _lastClockUpdateTick = currentTimeMs;
            if (_resultEffect == DojoResultEffect.TimeOver)
            {
                _resultEffect = DojoResultEffect.None;
                _resultEffectStartTick = int.MinValue;
            }
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
        }
        public void SetRuntimeState(int? playerHp, int? playerMaxHp, float? bossHpPercent)
        {
            if (playerMaxHp.HasValue && playerMaxHp.Value > 0)
            {
                _playerMaxHp = playerMaxHp.Value;
            }
            if (playerHp.HasValue)
            {
                _playerHp = Math.Clamp(playerHp.Value, 0, _playerMaxHp);
            }
            if (bossHpPercent.HasValue)
            {
                _bossHpPercent = Math.Clamp(bossHpPercent.Value, 0f, 1f);
                if (_lastBossHpPercent.GetValueOrDefault() > 0f
                    && _bossHpPercent.Value <= 0f
                    && _resultEffect == DojoResultEffect.None)
                {
                    ShowClearResult(Environment.TickCount, ResolveNextFloorMapId());
                }
            }
            else
            {
                _bossHpPercent = null;
            }
            _lastBossHpPercent = _bossHpPercent;
        }
        public void SetEnergy(int energy)
        {
            _energy = Math.Clamp(energy, 0, EnergyMax);
        }
        public bool TryApplyPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;
            switch (packetType)
            {
                case PacketTypeClock:
                    if (!TryParseClockPacketPayload(payload, out int clockType, out int durationSec, out errorMessage))
                    {
                        return false;
                    }
                    if (clockType != 2)
                    {
                        errorMessage = $"Dojo clock packet type {clockType} does not create the client timerboard.";
                        return false;
                    }
                    OnClock(clockType, durationSec, currentTimeMs);
                    return true;
                default:
                    errorMessage = $"Unsupported Dojo packet type: {packetType}";
                    return false;
            }
        }
        public int ConsumePendingTransferMapId()
        {
            if (_pendingTransferMapId <= 0 || _pendingTransferAtTick != int.MinValue)
            {
                return -1;
            }
            int pendingTransferMapId = _pendingTransferMapId;
            _pendingTransferMapId = -1;
            return pendingTransferMapId;
        }
        public int PendingTransferMapId => _pendingTransferMapId;
        public void SetStage(int stage, int currentTimeMs)
        {
            _stage = Math.Clamp(stage, 0, 32);
            _stageBannerStartTick = currentTimeMs;
        }
        public void ShowClearResult(int currentTimeMs, int nextMapId = -1)
        {
            _resultEffect = DojoResultEffect.Clear;
            _resultEffectStartTick = currentTimeMs;
            SchedulePresentationTransfer(nextMapId, _clearFrames, currentTimeMs);
        }
        public void ShowClearResultForNextFloor(int currentTimeMs)
        {
            ShowClearResult(currentTimeMs, ResolveNextFloorMapId());
        }
        public void ShowTimeOverResult(int currentTimeMs, int exitMapId = -1)
        {
            _resultEffect = DojoResultEffect.TimeOver;
            _resultEffectStartTick = currentTimeMs;
            _timeOverTick = 0;
            _timerDurationSec = 0;
            SchedulePresentationTransfer(exitMapId > 0 ? exitMapId : ResolveExitMapId(), _timeOverFrames, currentTimeMs);
        }
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }
            if (_timeOverTick != int.MinValue && _timeOverTick > 0 && currentTimeMs >= _timeOverTick && _resultEffect != DojoResultEffect.TimeOver)
            {
                ShowTimeOverResult(currentTimeMs);
            }
            if (_pendingTransferMapId > 0
                && _pendingTransferAtTick != int.MinValue
                && currentTimeMs >= _pendingTransferAtTick)
            {
                _pendingTransferAtTick = int.MinValue;
            }
        }
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int currentTimeMs)
        {
            if (!_isActive || pixelTexture == null)
            {
                return;
            }
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            EnsureAssetsLoaded();
            DrawClock(spriteBatch, viewport, pixelTexture, font);
            DrawGaugeBars(spriteBatch, viewport, pixelTexture);
            DrawEnergy(spriteBatch, viewport, pixelTexture);
            DrawStageBanner(spriteBatch, viewport, font, currentTimeMs);
            DrawResultEffect(spriteBatch, viewport, font, currentTimeMs);
        }
        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Mu Lung Dojo HUD inactive";
            }
            string bossText = _bossHpPercent.HasValue
                ? $"{(int)MathF.Round(_bossHpPercent.Value * 100f)}%"
                : "--";
            string timerText = _timeOverTick == int.MinValue ? "stopped" : FormatTimer(RemainingSeconds);
            string transferText = _pendingTransferMapId > 0 ? $", pendingReturn={_pendingTransferMapId}" : string.Empty;
            return $"Mu Lung Dojo floor {_stage}, timer={timerText}, boss={bossText}, player={_playerHp}/{_playerMaxHp}, energy={_energy}/{EnergyMax}{transferText}";
        }
        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _stage = -1;
            _timerDurationSec = 0;
            _timeOverTick = int.MinValue;
            _lastClockUpdateTick = int.MinValue;
            _returnMapId = -1;
            _forcedReturnMapId = -1;
            _hasNextFloorPortal = false;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _playerHp = 0;
            _playerMaxHp = 100;
            _bossHpPercent = null;
            _lastBossHpPercent = null;
            _energy = 0;
            _stageBannerStartTick = int.MinValue;
            _resultEffectStartTick = int.MinValue;
            _resultEffect = DojoResultEffect.None;
        }
        private static int ResolveStage(int mapId)
        {
            if (mapId < 925020000 || mapId > 925040999)
            {
                return -1;
            }
            int rawStage = (mapId / 100) % 100;
            return Math.Clamp(rawStage, 0, 32);
        }
        private static string FormatTimer(int remainingSeconds)
        {
            int minutes = Math.Max(0, remainingSeconds) / 60;
            int seconds = Math.Max(0, remainingSeconds) % 60;
            return $"{minutes}:{seconds:00}";
        }
        private static int NormalizeTransferMapId(int? mapId)
        {
            return mapId.HasValue && mapId.Value > 0 && mapId.Value != MapConstants.MaxMap
                ? mapId.Value
                : -1;
        }
        private int ResolveExitMapId()
        {
            return _forcedReturnMapId > 0 ? _forcedReturnMapId : _returnMapId;
        }
        private int ResolveNextFloorMapId()
        {
            return ResolveNextFloorMapIdCore(_mapId, _hasNextFloorPortal, HasMapImage);
        }
        private static int ResolveNextFloorMapIdCore(int mapId, bool hasNextFloorPortal, Func<int, bool> hasMapImage)
        {
            if (!hasNextFloorPortal || mapId <= 0)
            {
                return -1;
            }
            int stage = ResolveStage(mapId);
            if (stage < 0 || stage >= 32)
            {
                return -1;
            }
            int preservedSuffixCandidate = mapId + 100;
            if (hasMapImage(preservedSuffixCandidate))
            {
                return preservedSuffixCandidate;
            }
            int baseCandidate = ((mapId / 100) + 1) * 100;
            return hasMapImage(baseCandidate) ? baseCandidate : -1;
        }
        private static bool HasMapImage(int mapId)
        {
            if (mapId <= 0)
            {
                return false;
            }
            if (global::HaCreator.Program.WzManager == null)
            {
                return true;
            }
            return WzInfoTools.FindMapImage(mapId.ToString(CultureInfo.InvariantCulture), global::HaCreator.Program.WzManager) != null;
        }
        private static bool HasPortalScript(int mapId, string scriptName)
        {
            if (mapId <= 0 || string.IsNullOrWhiteSpace(scriptName) || global::HaCreator.Program.WzManager == null)
            {
                return false;
            }
            WzImage mapImage = WzInfoTools.FindMapImage(mapId.ToString(CultureInfo.InvariantCulture), global::HaCreator.Program.WzManager);
            if (mapImage == null)
            {
                return false;
            }
            WzImage portalImage = ResolveLinkedMapImage(mapImage) ?? mapImage;
            WzImageProperty portalRoot = portalImage["portal"];
            if (portalRoot == null)
            {
                return false;
            }
            foreach (WzImageProperty portalProperty in portalRoot.WzProperties)
            {
                if ((portalProperty["script"] as WzStringProperty)?.Value is string portalScript
                    && string.Equals(portalScript, scriptName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        private static WzImage ResolveLinkedMapImage(WzImage mapImage)
        {
            if (mapImage == null)
            {
                return null;
            }
            if ((mapImage["info"]?["link"] as WzStringProperty)?.Value is not string linkedMapId
                || string.IsNullOrWhiteSpace(linkedMapId)
                || global::HaCreator.Program.WzManager == null)
            {
                return mapImage;
            }
            return WzInfoTools.FindMapImage(linkedMapId, global::HaCreator.Program.WzManager) ?? mapImage;
        }
        private static bool TryParseClockPacketPayload(byte[] payload, out int clockType, out int durationSec, out string errorMessage)
        {
            clockType = 0;
            durationSec = 0;
            errorMessage = null;
            if (payload == null || payload.Length < 5)
            {
                errorMessage = "Dojo clock packet payload must contain 1 byte of clock type plus 4 bytes of duration.";
                return false;
            }
            clockType = payload[0];
            durationSec = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1, 4));
            return true;
        }
        private void SchedulePresentationTransfer(int targetMapId, IReadOnlyList<DojoFrame> frames, int currentTimeMs)
        {
            _pendingTransferMapId = NormalizeTransferMapId(targetMapId);
            _pendingTransferAtTick = _pendingTransferMapId > 0
                ? currentTimeMs + GetAnimationDurationMs(frames)
                : int.MinValue;
        }
        private static int GetAnimationDurationMs(IReadOnlyList<DojoFrame> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }
            int totalDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDuration += Math.Max(1, frames[i].Delay);
            }
            return totalDuration;
        }
        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _device == null)
            {
                return;
            }
            WzImage uiWindow = global::HaCreator.Program.FindImage("UI", "UIWindow.img")
                ?? global::HaCreator.Program.FindImage("UI", "UIWindow2.img");
            WzImage effectImage = global::HaCreator.Program.FindImage("Map", "Effect.img");
            WzImageProperty muruengRaid = uiWindow?["muruengRaid"];
            _clockTexture = LoadCanvasTexture(muruengRaid?["clock"]?["0"] as WzCanvasProperty);
            _playerTexture = LoadCanvasTexture(muruengRaid?["player"]?["0"] as WzCanvasProperty);
            _playerGaugeTexture = LoadCanvasTexture(muruengRaid?["player"]?["Gage"]?["0"] as WzCanvasProperty);
            _monsterTexture = LoadCanvasTexture(muruengRaid?["monster"]?["0"] as WzCanvasProperty);
            _monsterGaugeTexture = LoadCanvasTexture(muruengRaid?["monster"]?["Gage"]?["0"] as WzCanvasProperty);
            _energyTexture = LoadCanvasTexture(muruengRaid?["energy"]?["empty"]?["0"] as WzCanvasProperty);
            _energyGaugeTexture = LoadCanvasTexture(muruengRaid?["energy"]?["empty"]?["Gage"]?["0"] as WzCanvasProperty);
            _timerColonTexture = LoadCanvasTexture(muruengRaid?["number"]?["bar"] as WzCanvasProperty);
            for (int i = 0; i < _digitTextures.Length; i++)
            {
                _digitTextures[i] = LoadCanvasTexture(muruengRaid?["number"]?[i.ToString()] as WzCanvasProperty);
            }
            _energyFullFrames = LoadAnimationFrames(muruengRaid?["energy"]?["full"]);
            WzImageProperty dojang = effectImage?["dojang"];
            _stageFrames = LoadAnimationFrames(dojang?["start"]?["stage"]);
            _clearFrames = LoadAnimationFrames(dojang?["end"]?["clear"]);
            _timeOverFrames = LoadAnimationFrames(dojang?["timeOver"]);
            WzImageProperty startNumbers = dojang?["start"]?["number"];
            if (startNumbers != null)
            {
                foreach (WzImageProperty child in startNumbers.WzProperties)
                {
                    if (!int.TryParse(child.Name, out int stage))
                    {
                        continue;
                    }
                    List<DojoFrame> frames = LoadAnimationFrames(child);
                    if (frames?.Count > 0)
                    {
                        _stageNumberFrames[stage] = frames;
                    }
                }
            }
            _assetsLoaded = true;
        }
        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_device == null || canvas == null)
            {
                return null;
            }
            using var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(_device);
        }
        private List<DojoFrame> LoadAnimationFrames(WzImageProperty root)
        {
            if (_device == null || root == null)
            {
                return null;
            }
            var frames = new List<DojoFrame>();
            foreach (WzImageProperty child in root.WzProperties.OrderBy(ParseFrameOrder))
            {
                if (WzInfoTools.GetRealProperty(child) is not WzCanvasProperty canvas)
                {
                    continue;
                }
                try
                {
                    using var bitmap = canvas.GetLinkedWzCanvasBitmap();
                    Texture2D texture = bitmap?.ToTexture2DAndDispose(_device);
                    if (texture == null)
                    {
                        continue;
                    }
                    WzVectorProperty origin = canvas["origin"] as WzVectorProperty;
                    frames.Add(new DojoFrame(
                        texture,
                        new Point(origin?.X.Value ?? 0, origin?.Y.Value ?? 0),
                        Math.Max(1, canvas["delay"]?.GetInt() ?? 100)));
                }
                catch
                {
                    // Keep partially available animation sets usable.
                }
            }
            return frames.Count > 0 ? frames : null;
        }
        private void DrawClock(SpriteBatch spriteBatch, Viewport viewport, Texture2D pixelTexture, SpriteFont font)
        {
            Vector2 clockAnchor = new(viewport.Width / 2f, ClockOffsetY);
            DrawTextureAtOrigin(spriteBatch, _clockTexture, clockAnchor, ClockOrigin);
            Vector2 timerOrigin = new((viewport.Width / 2f) + TimerLayerOffsetX, TimerLayerY);
            bool drewDigits = TryDrawBitmapTimer(spriteBatch, timerOrigin);
            if (!drewDigits && font != null)
            {
                string timerText = FormatTimer(RemainingSeconds);
                spriteBatch.DrawString(font, timerText, timerOrigin, Color.White);
            }
        }
        private void DrawGaugeBars(SpriteBatch spriteBatch, Viewport viewport, Texture2D pixelTexture)
        {
            Vector2 playerAnchor = new((viewport.Width / 2f) + PlayerOffsetX, PlayerOffsetY);
            Rectangle playerBounds = DrawTextureAtOrigin(spriteBatch, _playerTexture, playerAnchor, PlayerOrigin);
            Rectangle playerGaugeBounds = new(
                playerBounds.X + PlayerGaugeOffsetX,
                playerBounds.Y + BarGaugeOffsetY,
                BarGaugeWidth,
                BarGaugeHeight);
            DrawHorizontalGauge(spriteBatch, pixelTexture, _playerGaugeTexture, playerGaugeBounds, _playerMaxHp > 0 ? (float)_playerHp / _playerMaxHp : 0f);
            Vector2 monsterAnchor = new((viewport.Width / 2f) + MonsterOffsetX, MonsterOffsetY);
            Rectangle monsterBounds = DrawTextureAtOrigin(spriteBatch, _monsterTexture, monsterAnchor, MonsterOrigin);
            Rectangle monsterGaugeBounds = new(
                monsterBounds.X + MonsterGaugeOffsetX,
                monsterBounds.Y + BarGaugeOffsetY,
                BarGaugeWidth,
                BarGaugeHeight);
            DrawHorizontalGauge(spriteBatch, pixelTexture, _monsterGaugeTexture, monsterGaugeBounds, _bossHpPercent ?? 0f);
        }
        private void DrawEnergy(SpriteBatch spriteBatch, Viewport viewport, Texture2D pixelTexture)
        {
            Vector2 energyAnchor = new(EnergyOffsetX, EnergyOffsetY);
            Rectangle energyBounds = DrawTextureAtTopLeft(spriteBatch, _energyTexture, energyAnchor);
            if (_energy >= EnergyMax)
            {
                DrawAnimation(spriteBatch, _energyFullFrames, Environment.TickCount, int.MaxValue, new Vector2(9f, 80f), repeat: true);
                return;
            }
            Rectangle energyGaugeBounds = new(
                energyBounds.X + EnergyGaugeOffsetX,
                energyBounds.Y + EnergyGaugeOffsetY,
                EnergyGaugeWidth,
                EnergyGaugeHeight);
            DrawVerticalGauge(spriteBatch, pixelTexture, _energyGaugeTexture, energyGaugeBounds, (float)_energy / EnergyMax);
        }
        private void DrawStageBanner(SpriteBatch spriteBatch, Viewport viewport, SpriteFont font, int currentTimeMs)
        {
            if (_stageBannerStartTick == int.MinValue)
            {
                return;
            }
            Vector2 center = new(viewport.Width / 2f, 200f);
            bool drewStage = DrawAnimation(spriteBatch, _stageFrames, currentTimeMs, _stageBannerStartTick, center, repeat: false);
            bool drewNumber = DrawAnimation(spriteBatch, ResolveStageNumberFrames(), currentTimeMs, _stageBannerStartTick, center, repeat: false);
            if (!drewStage && !drewNumber && font != null)
            {
                string stageText = _stage >= 0 ? $"Mu Lung Dojo Floor {_stage}" : "Mu Lung Dojo";
                Vector2 size = font.MeasureString(stageText);
                spriteBatch.DrawString(font, stageText, new Vector2(center.X - (size.X / 2f), center.Y - (size.Y / 2f)), Color.White);
            }
        }
        private void DrawResultEffect(SpriteBatch spriteBatch, Viewport viewport, SpriteFont font, int currentTimeMs)
        {
            if (_resultEffect == DojoResultEffect.None || _resultEffectStartTick == int.MinValue)
            {
                return;
            }
            List<DojoFrame> frames = _resultEffect == DojoResultEffect.Clear ? _clearFrames : _timeOverFrames;
            bool drew = DrawAnimation(spriteBatch, frames, currentTimeMs, _resultEffectStartTick, new Vector2(viewport.Width / 2f, viewport.Height / 2f), repeat: false);
            if (!drew && font != null)
            {
                string text = _resultEffect == DojoResultEffect.Clear ? "Stage Clear" : "Time Over";
                Vector2 size = font.MeasureString(text);
                spriteBatch.DrawString(font, text, new Vector2((viewport.Width - size.X) / 2f, (viewport.Height - size.Y) / 2f), Color.White);
            }
        }
        private bool TryDrawBitmapTimer(SpriteBatch spriteBatch, Vector2 timerOrigin)
        {
            if (_timerColonTexture == null || _digitTextures.Any(texture => texture == null))
            {
                return false;
            }
            int minutes = Math.Clamp(RemainingSeconds / 60, 0, 99);
            int seconds = Math.Clamp(RemainingSeconds % 60, 0, 59);
            DrawTwoDigits(spriteBatch, timerOrigin, TimerMinuteX, TimerDigitY, minutes);
            DrawTwoDigits(spriteBatch, timerOrigin, TimerSecondX, TimerDigitY, seconds);
            spriteBatch.Draw(_timerColonTexture, new Vector2(timerOrigin.X + TimerColonX, timerOrigin.Y + TimerColonY), Color.White);
            return true;
        }
        private void DrawTwoDigits(SpriteBatch spriteBatch, Vector2 timerOrigin, int x, int y, int value)
        {
            int tens = (value / 10) % 10;
            int ones = value % 10;
            spriteBatch.Draw(_digitTextures[tens], new Vector2(timerOrigin.X + x, timerOrigin.Y + y), Color.White);
            spriteBatch.Draw(_digitTextures[ones], new Vector2(timerOrigin.X + x + TimerDigitSpacing, timerOrigin.Y + y), Color.White);
        }
        private Rectangle DrawTextureAtOrigin(SpriteBatch spriteBatch, Texture2D texture, Vector2 anchor, Point origin)
        {
            if (texture == null)
            {
                return Rectangle.Empty;
            }
            Rectangle bounds = new(
                (int)MathF.Round(anchor.X - origin.X),
                (int)MathF.Round(anchor.Y - origin.Y),
                texture.Width,
                texture.Height);
            spriteBatch.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            return bounds;
        }
        private Rectangle DrawTextureAtTopLeft(SpriteBatch spriteBatch, Texture2D texture, Vector2 topLeft)
        {
            if (texture == null)
            {
                return Rectangle.Empty;
            }
            Rectangle bounds = new((int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y), texture.Width, texture.Height);
            spriteBatch.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            return bounds;
        }
        private static void DrawHorizontalGauge(SpriteBatch spriteBatch, Texture2D pixelTexture, Texture2D gaugeTexture, Rectangle bounds, float progress)
        {
            int fillWidth = Math.Clamp((int)MathF.Round(bounds.Width * Math.Clamp(progress, 0f, 1f)), 0, bounds.Width);
            if (fillWidth <= 0)
            {
                return;
            }
            Texture2D source = gaugeTexture ?? pixelTexture;
            if (source == null)
            {
                return;
            }
            spriteBatch.Draw(source, new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), Color.White);
        }
        private static void DrawVerticalGauge(SpriteBatch spriteBatch, Texture2D pixelTexture, Texture2D gaugeTexture, Rectangle bounds, float progress)
        {
            int fillHeight = Math.Clamp((int)MathF.Round(bounds.Height * Math.Clamp(progress, 0f, 1f)), 0, bounds.Height);
            if (fillHeight <= 0)
            {
                return;
            }
            Texture2D source = gaugeTexture ?? pixelTexture;
            if (source == null)
            {
                return;
            }
            Rectangle dest = new(bounds.X, bounds.Bottom - fillHeight, bounds.Width, fillHeight);
            spriteBatch.Draw(source, dest, Color.White);
        }
        private bool DrawAnimation(SpriteBatch spriteBatch, IReadOnlyList<DojoFrame> frames, int currentTimeMs, int startTick, Vector2 anchor, bool repeat)
        {
            if (frames == null || frames.Count == 0 || startTick == int.MinValue)
            {
                return false;
            }
            DojoFrame frame = ResolveAnimationFrame(frames, currentTimeMs, startTick, repeat);
            if (frame.Texture == null)
            {
                return false;
            }
            Vector2 drawPos = new(anchor.X - frame.Origin.X, anchor.Y - frame.Origin.Y);
            spriteBatch.Draw(frame.Texture, drawPos, Color.White);
            return true;
        }
        private static DojoFrame ResolveAnimationFrame(IReadOnlyList<DojoFrame> frames, int currentTimeMs, int startTick, bool repeat)
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
            foreach (DojoFrame frame in frames)
            {
                cursor += Math.Max(1, frame.Delay);
                if (elapsed < cursor)
                {
                    return frame;
                }
            }
            return frames[^1];
        }
        private List<DojoFrame> ResolveStageNumberFrames()
        {
            if (_stage >= 0 && _stageNumberFrames.TryGetValue(_stage, out List<DojoFrame> frames))
            {
                return frames;
            }
            return null;
        }
        private static int ParseFrameOrder(WzImageProperty property)
        {
            return int.TryParse(property?.Name, out int order) ? order : int.MaxValue;
        }
        private readonly record struct DojoFrame(Texture2D Texture, Point Origin, int Delay);
        private enum DojoResultEffect
        {
            None,
            Clear,
            TimeOver
        }
    }
    #endregion
}
