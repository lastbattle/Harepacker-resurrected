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
    #region Battlefield Field (CField_Battlefield)
    /// <summary>
    /// Battlefield Field - Sheep vs Wolf event scoreboard and timer flow.
    ///
    /// WZ evidence:
    /// - map/Map/Map9/910040100/battleField exposes timeDefault=300, timeFinish=3, rewardMap* and effectWin/effectLose.
    ///
    /// Client evidence:
    /// - CField_Battlefield::OnScoreUpdate (0x5499a0): decodes wolves/sheep bytes and redraws the scoreboard.
    /// - CField_Battlefield::OnClock (0x549ad0): for clock type 2, creates a 258x73 center-top scoreboard window and starts its timer.
    /// - CField_Battlefield::OnTeamChanged (0x5499e0): decodes character id + team byte and forwards it into SetUserTeam.
    /// - CField_Battlefield::SetUserTeam (0x549870): stores the Battlefield team on the user, reapplies the user look, and toggles the minimap for local teams 0/2.
    /// </summary>
    public class BattlefieldField
    {
        private readonly struct BattlefieldBitmapGlyph
        {
            public BattlefieldBitmapGlyph(Texture2D texture, Point origin)
            {
                Texture = texture;
                Origin = origin;
            }


            public Texture2D Texture { get; }

            public Point Origin { get; }

        }



        public sealed class BattlefieldTeamLookPreset
        {
            public BattlefieldTeamLookPreset(
                int teamId,
                IReadOnlyDictionary<EquipSlot, int> equipmentItemIds,
                float? moveSpeed,
                int? moveSpeedCap,
                IReadOnlyList<int> blockedItemIds)
            {
                TeamId = teamId;
                EquipmentItemIds = equipmentItemIds ?? new Dictionary<EquipSlot, int>();
                MoveSpeed = moveSpeed;
                MoveSpeedCap = moveSpeedCap;
                BlockedItemIds = blockedItemIds ?? Array.Empty<int>();
            }


            public int TeamId { get; }
            public IReadOnlyDictionary<EquipSlot, int> EquipmentItemIds { get; }
            public float? MoveSpeed { get; }
            public int? MoveSpeedCap { get; }
            public IReadOnlyList<int> BlockedItemIds { get; }
        }


        public enum BattlefieldWinner
        {
            None,
            Wolves,
            Sheep,
            Draw
        }


        private const int ScoreboardOffsetX = -107;
        private const int ScoreboardY = 30;
        private const int ScoreboardWidth = 258;
        private const int ScoreboardHeight = 73;
        private const int ScorePulseDurationMs = 800;
        private const int ScoreboardBackgroundOffsetX = 21;
        private const int LeftScoreOriginX = 43;
        private const int RightScoreOriginX = 130;
        private const int ScoreOriginY = 12;
        private const int TimerOriginX = 104;
        private const int TimerOriginY = 61;
        private const int TimerDigitSpacing = -1;
        private const int TimerGroupSpacing = 2;


        private bool _isActive;
        private GraphicsDevice _device;
        private bool _assetsLoaded;
        private int _wolvesScore;
        private int _sheepScore;
        private int _defaultDurationSeconds = 300;
        private int _finishDurationSeconds = 3;
        private int _clockDurationSeconds;
        private int _clockStartTimeMs;
        private int _currentObservedTimeMs;
        private int _lastScoreUpdateTimeMs = int.MinValue;
        private int _lastTeamChangeTimeMs = int.MinValue;
        private bool _clockVisible;
        private int? _localCharacterId;
        private int? _localTeamId;
        private BattlefieldWinner _winner = BattlefieldWinner.None;
        private int _resultResolvedTimeMs = int.MinValue;
        private string _resolvedEffectPath;
        private int _resolvedRewardMapId;
        private int _pendingTransferMapId = -1;
        private int _pendingTransferAtTick = int.MinValue;
        private string _statusMessage;
        private int _statusMessageUntilMs;
        private readonly Dictionary<int, BattlefieldTeamLookPreset> _teamLookPresets = new();
        private readonly Dictionary<int, int> _remoteUserTeams = new();
        private Texture2D _scoreboardTexture;
        private BattlefieldBitmapGlyph[] _wolvesDigitGlyphs = Array.Empty<BattlefieldBitmapGlyph>();
        private BattlefieldBitmapGlyph[] _sheepDigitGlyphs = Array.Empty<BattlefieldBitmapGlyph>();
        private BattlefieldBitmapGlyph[] _timerDigitGlyphs = Array.Empty<BattlefieldBitmapGlyph>();
        private BattlefieldBitmapGlyph? _timerSeparatorGlyph;


        public bool IsActive => _isActive;
        public int WolvesScore => _wolvesScore;
        public int SheepScore => _sheepScore;
        public int DefaultDurationSeconds => _defaultDurationSeconds;
        public int FinishDurationSeconds => _finishDurationSeconds;
        public int? LocalCharacterId => _localCharacterId;
        public int? LocalTeamId => _localTeamId;
        public BattlefieldWinner Winner => _winner;
        public string ResolvedEffectPath => _resolvedEffectPath;
        public int ResolvedRewardMapId => _resolvedRewardMapId;
        public IReadOnlyDictionary<int, BattlefieldTeamLookPreset> TeamLookPresets => _teamLookPresets;
        public IReadOnlyDictionary<int, int> RemoteUserTeams => _remoteUserTeams;
        public int RemainingSeconds => !_clockVisible
            ? _defaultDurationSeconds
            : Math.Max(0, _clockDurationSeconds - Math.Max(0, _currentObservedTimeMs - _clockStartTimeMs) / 1000);


        public string EffectWinPath { get; private set; }
        public string EffectLosePath { get; private set; }
        public int RewardMapWinWolf { get; private set; }
        public int RewardMapWinSheep { get; private set; }
        public int RewardMapLoseWolf { get; private set; }
        public int RewardMapLoseSheep { get; private set; }


        public void Initialize(GraphicsDevice device)
        {
            _device = device;
            EnsureAssetsLoaded();
        }


        public void Enable()
        {
            _isActive = true;
            _wolvesScore = 0;
            _sheepScore = 0;
            _clockVisible = false;
            _clockDurationSeconds = 0;
            _clockStartTimeMs = 0;
            _currentObservedTimeMs = 0;
            _lastScoreUpdateTimeMs = int.MinValue;
            _lastTeamChangeTimeMs = int.MinValue;
            _defaultDurationSeconds = 300;
            _finishDurationSeconds = 3;
            _localCharacterId = null;
            _localTeamId = null;
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _statusMessage = null;
            _statusMessageUntilMs = 0;
            EffectWinPath = null;
            EffectLosePath = null;
            RewardMapWinWolf = 0;
            RewardMapWinSheep = 0;
            RewardMapLoseWolf = 0;
            RewardMapLoseSheep = 0;
            _teamLookPresets.Clear();
            _remoteUserTeams.Clear();
        }


        public void Configure(MapInfo mapInfo)
        {
            if (!_isActive || mapInfo == null)
            {
                return;
            }


            _teamLookPresets.Clear();



            for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
            {
                if (mapInfo.additionalNonInfoProps[i] is not WzSubProperty property)
                {
                    continue;
                }


                if (string.Equals(property.Name, "battleField", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultDurationSeconds = Math.Max(1, InfoTool.GetOptionalInt(property["timeDefault"]) ?? _defaultDurationSeconds);
                    _finishDurationSeconds = Math.Max(0, InfoTool.GetOptionalInt(property["timeFinish"]) ?? _finishDurationSeconds);
                    EffectWinPath = InfoTool.GetOptionalString(property["effectWin"]);
                    EffectLosePath = InfoTool.GetOptionalString(property["effectLose"]);
                    RewardMapWinWolf = InfoTool.GetOptionalInt(property["rewardMapWinWolf"]) ?? RewardMapWinWolf;
                    RewardMapWinSheep = InfoTool.GetOptionalInt(property["rewardMapWinSheep"]) ?? RewardMapWinSheep;
                    RewardMapLoseWolf = InfoTool.GetOptionalInt(property["rewardMapLoseWolf"]) ?? RewardMapLoseWolf;
                    RewardMapLoseSheep = InfoTool.GetOptionalInt(property["rewardMapLoseSheep"]) ?? RewardMapLoseSheep;
                    continue;
                }


                if (string.Equals(property.Name, "user", StringComparison.OrdinalIgnoreCase))
                {
                    LoadTeamLookPresets(property);
                }
            }
        }


        public bool TryGetTeamLookPreset(int teamId, out BattlefieldTeamLookPreset preset)
        {
            return _teamLookPresets.TryGetValue(teamId, out preset);
        }


        public bool TryGetAssignedTeamLookPreset(int characterId, out BattlefieldTeamLookPreset preset)
        {
            preset = null;
            if (!_isActive || characterId <= 0)
            {
                return false;
            }


            if (_localCharacterId.HasValue
                && characterId == _localCharacterId.Value
                && _localTeamId.HasValue)
            {
                return _teamLookPresets.TryGetValue(_localTeamId.Value, out preset);
            }


            return _remoteUserTeams.TryGetValue(characterId, out int teamId)

                && _teamLookPresets.TryGetValue(teamId, out preset);

        }



        public bool TryGetAssignedTeamId(int characterId, out int teamId)
        {
            teamId = default;
            if (!_isActive || characterId <= 0)
            {
                return false;
            }


            if (_localCharacterId.HasValue
                && characterId == _localCharacterId.Value
                && _localTeamId.HasValue)
            {
                teamId = _localTeamId.Value;
                return true;
            }


            return _remoteUserTeams.TryGetValue(characterId, out teamId);

        }



        public bool TryGetLocalTeamLookPreset(out BattlefieldTeamLookPreset preset)
        {
            preset = null;
            return _isActive
                && _localTeamId.HasValue
                && _teamLookPresets.TryGetValue(_localTeamId.Value, out preset);
        }


        public bool IsItemBlockedForLocalTeam(int itemId)
        {
            return itemId > 0
                && TryGetLocalTeamLookPreset(out BattlefieldTeamLookPreset preset)
                && preset.BlockedItemIds.Contains(itemId);
        }


        public float ApplyLocalMoveSpeedCap(float speed)
        {
            if (!TryGetLocalTeamLookPreset(out BattlefieldTeamLookPreset preset)
                || !preset.MoveSpeedCap.HasValue)
            {
                return speed;
            }


            return Math.Min(speed, preset.MoveSpeedCap.Value);

        }



        public void SetLocalPlayerState(int? localCharacterId)
        {
            _localCharacterId = localCharacterId > 0 ? localCharacterId : null;
            if (_localCharacterId.HasValue
                && _remoteUserTeams.Remove(_localCharacterId.Value, out int promotedTeamId))
            {
                _localTeamId = promotedTeamId >= 0 ? promotedTeamId : null;
                RefreshResolvedResult();
            }
        }


        public void OnScoreUpdate(int wolves, int sheep, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }


            int clampedWolves = Math.Clamp(wolves, 0, byte.MaxValue);

            int clampedSheep = Math.Clamp(sheep, 0, byte.MaxValue);

            System.Diagnostics.Debug.WriteLine($"[BattlefieldField] OnScoreUpdate: wolves {_wolvesScore} -> {clampedWolves}, sheep {_sheepScore} -> {clampedSheep}");



            _wolvesScore = clampedWolves;
            _sheepScore = clampedSheep;
            _lastScoreUpdateTimeMs = currentTimeMs;
            ClearResolvedResult();
        }


        public void OnClock(int clockType, int remainingSeconds, int currentTimeMs)
        {
            if (!_isActive || clockType != 2)
            {
                return;
            }


            _clockVisible = true;
            _clockDurationSeconds = Math.Max(0, remainingSeconds);
            _clockStartTimeMs = currentTimeMs;
            _currentObservedTimeMs = currentTimeMs;
            ClearResolvedResult();
            System.Diagnostics.Debug.WriteLine($"[BattlefieldField] OnClock: type={clockType}, seconds={remainingSeconds}");
        }


        public void OnTeamChanged(int characterId, int teamId, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }


            _lastTeamChangeTimeMs = currentTimeMs;



            if (characterId <= 0
                || (_localCharacterId.HasValue && characterId == _localCharacterId.Value))
            {
                SetLocalTeam(teamId, currentTimeMs);
                return;
            }


            if (teamId >= 0)
            {
                _remoteUserTeams[characterId] = teamId;
            }
            else
            {
                _remoteUserTeams.Remove(characterId);
            }


            ShowStatus($"Battlefield user {characterId} switched to {FormatTeamName(teamId)}.", currentTimeMs, 2500);

        }



        public void SetLocalTeam(int? teamId, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }


            _localTeamId = teamId is >= 0 ? teamId : null;

            _lastTeamChangeTimeMs = currentTimeMs;

            RefreshResolvedResult();



            string teamLabel = _localTeamId.HasValue ? FormatTeamName(_localTeamId.Value) : "unset";

            ShowStatus($"Local Battlefield team: {teamLabel}.", currentTimeMs, 2500);

        }



        public void ResolveResult(BattlefieldWinner winner, int currentTimeMs)
        {
            if (!_isActive)
            {
                return;
            }


            _winner = winner;
            _resultResolvedTimeMs = currentTimeMs;
            _resolvedEffectPath = ResolveEffectPathForLocalOutcome(winner);
            _resolvedRewardMapId = ResolveRewardMapIdForLocalOutcome(winner);
            _pendingTransferMapId = _resolvedRewardMapId > 0 ? _resolvedRewardMapId : -1;
            _pendingTransferAtTick = _pendingTransferMapId > 0
                ? currentTimeMs + Math.Max(0, _finishDurationSeconds * 1000)
                : int.MinValue;


            string suffix = _resolvedRewardMapId > 0
                ? $" reward map {_resolvedRewardMapId}"
                : " reward map unavailable";
            string effectSuffix = string.IsNullOrWhiteSpace(_resolvedEffectPath)
                ? string.Empty
                : $", effect {_resolvedEffectPath}";
            ShowStatus($"Battlefield result: {GetWinnerLabel(winner)}.{suffix}{effectSuffix}", currentTimeMs, Math.Max(1000, _finishDurationSeconds * 1000));
        }


        public void StartDefaultClock(int currentTimeMs)
        {
            OnClock(2, _defaultDurationSeconds, currentTimeMs);
        }


        public void Update(int currentTimeMs, float deltaSeconds)
        {
            if (!_isActive)
            {
                return;
            }


            _currentObservedTimeMs = currentTimeMs;



            if (_winner == BattlefieldWinner.None
                && _clockVisible
                && _clockDurationSeconds > 0
                && RemainingSeconds <= 0)
            {
                ResolveResult(ComputeWinnerFromScore(), currentTimeMs);
            }


            if (_pendingTransferMapId > 0
                && _pendingTransferAtTick != int.MinValue
                && currentTimeMs >= _pendingTransferAtTick)
            {
                _pendingTransferAtTick = int.MinValue;
            }


            if (_statusMessage != null && currentTimeMs >= _statusMessageUntilMs)
            {
                _statusMessage = null;
            }
        }


        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isActive || !_clockVisible || pixelTexture == null)
            {
                return;
            }


            EnsureAssetsLoaded();



            Rectangle bounds = GetScoreboardBounds(spriteBatch.GraphicsDevice.Viewport);
            float pulse = GetScorePulseStrength();
            Vector2 scoreboardOrigin = new Vector2(bounds.X + ScoreboardBackgroundOffsetX, bounds.Y);
            if (_scoreboardTexture != null)
            {
                spriteBatch.Draw(_scoreboardTexture, scoreboardOrigin, Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, new Rectangle((int)scoreboardOrigin.X, (int)scoreboardOrigin.Y, 215, ScoreboardHeight), new Color(16, 62, 82, 255));
            }


            if (pulse > 0f)
            {
                spriteBatch.Draw(
                    pixelTexture,
                    new Rectangle((int)scoreboardOrigin.X, (int)scoreboardOrigin.Y, _scoreboardTexture?.Width ?? 215, _scoreboardTexture?.Height ?? ScoreboardHeight),
                    Color.White * (pulse * 0.18f));
            }


            if (!TryDrawScore(spriteBatch, scoreboardOrigin, _sheepScore, _sheepDigitGlyphs, LeftScoreOriginX, ScoreOriginY)
                && font != null)
            {
                spriteBatch.DrawString(font, _sheepScore.ToString(CultureInfo.InvariantCulture), scoreboardOrigin + new Vector2(LeftScoreOriginX, ScoreOriginY), Color.White);
            }


            if (!TryDrawScore(spriteBatch, scoreboardOrigin, _wolvesScore, _wolvesDigitGlyphs, RightScoreOriginX, ScoreOriginY)
                && font != null)
            {
                spriteBatch.DrawString(font, _wolvesScore.ToString(CultureInfo.InvariantCulture), scoreboardOrigin + new Vector2(RightScoreOriginX, ScoreOriginY), Color.White);
            }


            if (!TryDrawTime(spriteBatch, scoreboardOrigin, RemainingSeconds) && font != null)
            {
                spriteBatch.DrawString(font, FormatTimerForFallback(RemainingSeconds), scoreboardOrigin + new Vector2(TimerOriginX, TimerOriginY - 6), Color.White);
            }
        }


        public string DescribeStatus()
        {
            if (!_isActive)
            {
                return "Battlefield runtime inactive";
            }


            string clockText = _clockVisible
                ? $"timer={FormatTimer(RemainingSeconds)}"
                : $"timer=idle(default {FormatTimer(_defaultDurationSeconds)})";
            string teamText = _localTeamId.HasValue ? $"team={FormatTeamName(_localTeamId.Value)}" : "team=unset";
            string resultText = _winner == BattlefieldWinner.None
                ? "result=pending"
                : $"result={GetWinnerLabel(_winner)}, rewardMap={(_resolvedRewardMapId > 0 ? _resolvedRewardMapId : 0)}, effect={(_resolvedEffectPath ?? "none")}";
            string transferText = _pendingTransferMapId > 0 ? $", pendingTransfer={_pendingTransferMapId}" : string.Empty;
            string lookPresetText = _teamLookPresets.Count > 0
                ? $", lookPresets={string.Join(";", _teamLookPresets.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value.EquipmentItemIds.Count}"))}"
                : string.Empty;
            string remoteTeamText = _remoteUserTeams.Count > 0
                ? $", remoteTeams={string.Join(";", _remoteUserTeams.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{FormatTeamName(kvp.Value)}"))}"
                : string.Empty;
            return $"Battlefield active, wolves={_wolvesScore:D2}, sheep={_sheepScore:D2}, {clockText}, {teamText}, {resultText}{transferText}{lookPresetText}{remoteTeamText}";
        }


        public void Reset()
        {
            _isActive = false;
            _wolvesScore = 0;
            _sheepScore = 0;
            _clockVisible = false;
            _clockDurationSeconds = 0;
            _clockStartTimeMs = 0;
            _currentObservedTimeMs = 0;
            _lastScoreUpdateTimeMs = int.MinValue;
            _lastTeamChangeTimeMs = int.MinValue;
            _defaultDurationSeconds = 300;
            _finishDurationSeconds = 3;
            _localCharacterId = null;
            _localTeamId = null;
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
            _statusMessage = null;
            _statusMessageUntilMs = 0;
            EffectWinPath = null;
            EffectLosePath = null;
            RewardMapWinWolf = 0;
            RewardMapWinSheep = 0;
            RewardMapLoseWolf = 0;
            RewardMapLoseSheep = 0;
            _teamLookPresets.Clear();
            _remoteUserTeams.Clear();
        }


        private static Rectangle GetScoreboardBounds(Viewport viewport)
        {
            int x = viewport.Width / 2 + ScoreboardOffsetX;
            return new Rectangle(x, ScoreboardY, ScoreboardWidth, ScoreboardHeight);
        }


        private void LoadTeamLookPresets(WzSubProperty userProperty)
        {
            foreach (WzImageProperty child in userProperty.WzProperties)
            {
                if (child is not WzSubProperty userEntry)
                {
                    continue;
                }


                int parsedTeamId = 0;
                int? teamId = InfoTool.GetOptionalInt(userEntry["cond"]?["battleFieldTeam"]);
                if (!teamId.HasValue && !int.TryParse(userEntry.Name, out parsedTeamId))
                {
                    continue;
                }


                if (!teamId.HasValue)
                {
                    teamId = parsedTeamId;
                }


                if (userEntry["look"] is not WzSubProperty lookProperty)
                {
                    continue;
                }


                Dictionary<EquipSlot, int> equipmentItemIds = new();
                foreach (WzImageProperty lookEntry in lookProperty.WzProperties)
                {
                    int? itemId = InfoTool.GetOptionalInt(lookEntry);
                    if (!itemId.HasValue
                        || !TryResolveBattlefieldEquipSlot(lookEntry.Name, itemId.Value, out EquipSlot slot))
                    {
                        continue;
                    }


                    equipmentItemIds[slot] = itemId.Value;

                }



                float? moveSpeed = userEntry["stat"] is WzSubProperty statProperty
                    ? InfoTool.GetOptionalInt(statProperty["speed"])
                    : null;
                int? moveSpeedCap = userEntry["stat"] is WzSubProperty statPropertyWithCap
                    ? InfoTool.GetOptionalInt(statPropertyWithCap["speedmax"])
                    : null;
                IReadOnlyList<int> blockedItemIds = userEntry["noitem"] is WzSubProperty blockedItemsProperty
                    ? blockedItemsProperty.WzProperties
                        .Select(property => InfoTool.GetOptionalInt(property))
                        .Where(itemId => itemId.HasValue && itemId.Value > 0)
                        .Select(itemId => itemId.Value)
                        .ToArray()
                    : Array.Empty<int>();


                _teamLookPresets[teamId.Value] = new BattlefieldTeamLookPreset(
                    teamId.Value,
                    equipmentItemIds,
                    moveSpeed,
                    moveSpeedCap,
                    blockedItemIds);
            }
        }


        private static bool TryResolveBattlefieldEquipSlot(string propertyName, int itemId, out EquipSlot slot)
        {
            switch (propertyName?.ToLowerInvariant())
            {
                case "cap":
                    slot = EquipSlot.Cap;
                    return true;
                case "gloves":
                    slot = EquipSlot.Glove;
                    return true;
                case "shoes":
                    slot = EquipSlot.Shoes;
                    return true;
                case "cape":
                    slot = EquipSlot.Cape;
                    return true;
                case "pants":
                    slot = EquipSlot.Pants;
                    return true;
                case "clothes":
                    slot = (itemId / 10000) == 105 ? EquipSlot.Longcoat : EquipSlot.Coat;
                    return true;
            }


            slot = (itemId / 10000) switch
            {
                100 => EquipSlot.Cap,
                104 => EquipSlot.Coat,
                105 => EquipSlot.Longcoat,
                106 => EquipSlot.Pants,
                107 => EquipSlot.Shoes,
                108 => EquipSlot.Glove,
                110 => EquipSlot.Cape,
                _ => EquipSlot.None
            };


            return slot != EquipSlot.None;

        }



        private float GetScorePulseStrength()
        {
            if (_lastScoreUpdateTimeMs == int.MinValue)
            {
                return 0f;
            }


            int elapsed = _currentObservedTimeMs - _lastScoreUpdateTimeMs;
            if (elapsed < 0 || elapsed >= ScorePulseDurationMs)
            {
                return 0f;
            }


            float normalized = 1f - (elapsed / (float)ScorePulseDurationMs);

            return normalized * normalized;

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


        private void EnsureAssetsLoaded()
        {
            if (_assetsLoaded || _device == null)
            {
                return;
            }


            WzImage objImage = global::HaCreator.Program.FindImage("Map", "Obj/etc.img");
            WzSubProperty battleField = objImage?["battleField"] as WzSubProperty;
            _scoreboardTexture = LoadCanvasTexture(battleField?["backgrnd"] as WzCanvasProperty);
            _wolvesDigitGlyphs = LoadGlyphSet(battleField?["fontScore0"] as WzSubProperty, 10);
            _sheepDigitGlyphs = LoadGlyphSet(battleField?["fontScore1"] as WzSubProperty, 10);
            _timerDigitGlyphs = LoadGlyphSet(battleField?["fontTime"] as WzSubProperty, 10);
            _timerSeparatorGlyph = LoadGlyph(battleField?["fontTime"]?["comma"] as WzCanvasProperty);
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



        private BattlefieldBitmapGlyph[] LoadGlyphSet(WzSubProperty parent, int count)
        {
            BattlefieldBitmapGlyph[] glyphs = new BattlefieldBitmapGlyph[count];
            for (int i = 0; i < count; i++)
            {
                glyphs[i] = LoadGlyph(parent?[i.ToString()] as WzCanvasProperty) ?? default;
            }


            return glyphs;

        }



        private BattlefieldBitmapGlyph? LoadGlyph(WzCanvasProperty canvas)
        {
            Texture2D texture = LoadCanvasTexture(canvas);
            if (texture == null)
            {
                return null;
            }


            System.Drawing.Point canvasOrigin = canvas?[WzCanvasProperty.OriginPropertyName]?.GetPoint() ?? System.Drawing.Point.Empty;

            return new BattlefieldBitmapGlyph(texture, new Point(canvasOrigin.X, canvasOrigin.Y));

        }



        private bool TryDrawScore(SpriteBatch spriteBatch, Vector2 scoreboardOrigin, int score, BattlefieldBitmapGlyph[] glyphs, int originX, int originY)
        {
            if (glyphs == null || glyphs.Length < 10 || glyphs.Any(glyph => glyph.Texture == null))
            {
                return false;
            }


            string scoreText = Math.Max(0, score).ToString(CultureInfo.InvariantCulture);
            int totalWidth = 0;
            for (int i = 0; i < scoreText.Length; i++)
            {
                int digit = scoreText[i] - '0';
                if (digit < 0 || digit >= glyphs.Length || glyphs[digit].Texture == null)
                {
                    return false;
                }


                totalWidth += glyphs[digit].Texture.Width;

            }



            float drawX = scoreboardOrigin.X + originX + Math.Max(0f, (42 - totalWidth) / 2f);
            for (int i = 0; i < scoreText.Length; i++)
            {
                BattlefieldBitmapGlyph glyph = glyphs[scoreText[i] - '0'];
                spriteBatch.Draw(glyph.Texture, new Vector2(drawX - glyph.Origin.X, scoreboardOrigin.Y + originY - glyph.Origin.Y), Color.White);
                drawX += glyph.Texture.Width;
            }


            return true;

        }



        private bool TryDrawTime(SpriteBatch spriteBatch, Vector2 scoreboardOrigin, int totalSeconds)
        {
            if (_timerDigitGlyphs == null
                || _timerDigitGlyphs.Length < 10
                || _timerDigitGlyphs.Any(glyph => glyph.Texture == null)
                || !_timerSeparatorGlyph.HasValue
                || _timerSeparatorGlyph.Value.Texture == null)
            {
                return false;
            }


            int safeSeconds = Math.Max(0, totalSeconds);
            int hours = safeSeconds / 3600;
            int minutes = (safeSeconds / 60) % 60;
            int seconds = safeSeconds % 60;
            string[] groups =
            {
                hours.ToString("D2", CultureInfo.InvariantCulture),
                minutes.ToString("D2", CultureInfo.InvariantCulture),
                seconds.ToString("D2", CultureInfo.InvariantCulture)
            };


            float drawX = scoreboardOrigin.X + TimerOriginX;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                string group = groups[groupIndex];
                for (int i = 0; i < group.Length; i++)
                {
                    BattlefieldBitmapGlyph glyph = _timerDigitGlyphs[group[i] - '0'];
                    spriteBatch.Draw(glyph.Texture, new Vector2(drawX - glyph.Origin.X, scoreboardOrigin.Y + TimerOriginY - glyph.Origin.Y), Color.White);
                    drawX += glyph.Texture.Width + TimerDigitSpacing;
                }


                if (groupIndex == groups.Length - 1)
                {
                    continue;
                }


                BattlefieldBitmapGlyph separator = _timerSeparatorGlyph.Value;
                spriteBatch.Draw(separator.Texture, new Vector2(drawX - separator.Origin.X, scoreboardOrigin.Y + TimerOriginY - separator.Origin.Y), Color.White);
                drawX += separator.Texture.Width + TimerGroupSpacing;
            }


            return true;

        }



        private BattlefieldWinner ComputeWinnerFromScore()
        {
            if (_wolvesScore == _sheepScore)
            {
                return BattlefieldWinner.Draw;
            }


            return _wolvesScore > _sheepScore ? BattlefieldWinner.Wolves : BattlefieldWinner.Sheep;

        }



        private void RefreshResolvedResult()
        {
            if (_winner == BattlefieldWinner.None)
            {
                return;
            }


            _resolvedEffectPath = ResolveEffectPathForLocalOutcome(_winner);
            _resolvedRewardMapId = ResolveRewardMapIdForLocalOutcome(_winner);
            _pendingTransferMapId = _resolvedRewardMapId > 0 ? _resolvedRewardMapId : -1;
        }


        private void ClearResolvedResult()
        {
            _winner = BattlefieldWinner.None;
            _resultResolvedTimeMs = int.MinValue;
            _resolvedEffectPath = null;
            _resolvedRewardMapId = 0;
            _pendingTransferMapId = -1;
            _pendingTransferAtTick = int.MinValue;
        }


        private void ShowStatus(string message, int currentTimeMs, int durationMs)
        {
            _statusMessage = message;
            _statusMessageUntilMs = currentTimeMs + Math.Max(250, durationMs);
        }


        private string ResolveEffectPathForLocalOutcome(BattlefieldWinner winner)
        {
            bool? localWin = GetIsLocalWin(winner);
            if (!localWin.HasValue)
            {
                return null;
            }


            return localWin.Value ? EffectWinPath : EffectLosePath;

        }



        private int ResolveRewardMapIdForLocalOutcome(BattlefieldWinner winner)
        {
            if (!_localTeamId.HasValue)
            {
                return 0;
            }


            return (_localTeamId.Value, winner) switch
            {
                (0, BattlefieldWinner.Wolves) => RewardMapWinWolf,
                (0, BattlefieldWinner.Sheep) => RewardMapLoseWolf,
                (1, BattlefieldWinner.Sheep) => RewardMapWinSheep,
                (1, BattlefieldWinner.Wolves) => RewardMapLoseSheep,
                _ => 0
            };
        }


        private bool? GetIsLocalWin(BattlefieldWinner winner)
        {
            if (!_localTeamId.HasValue || winner == BattlefieldWinner.None || winner == BattlefieldWinner.Draw)
            {
                return null;
            }


            return (_localTeamId.Value, winner) switch
            {
                (0, BattlefieldWinner.Wolves) => true,
                (0, BattlefieldWinner.Sheep) => false,
                (1, BattlefieldWinner.Sheep) => true,
                (1, BattlefieldWinner.Wolves) => false,
                _ => null
            };
        }


        private static string GetWinnerLabel(BattlefieldWinner winner)
        {
            return winner switch
            {
                BattlefieldWinner.Wolves => "Wolves win",
                BattlefieldWinner.Sheep => "Sheep win",
                BattlefieldWinner.Draw => "Draw",
                _ => "Pending"
            };
        }


        private static string FormatTeamName(int teamId)
        {
            return teamId switch
            {
                0 => "Wolves",
                1 => "Sheep",
                2 => "Team 2",
                _ => $"Team {teamId}"
            };
        }


        private static string FormatTimer(int totalSeconds)
        {
            int safeSeconds = Math.Max(0, totalSeconds);
            return $"{safeSeconds / 60}:{safeSeconds % 60:D2}";
        }


        private static string FormatTimerForFallback(int totalSeconds)
        {
            int safeSeconds = Math.Max(0, totalSeconds);
            int hours = safeSeconds / 3600;
            int minutes = (safeSeconds / 60) % 60;
            int seconds = safeSeconds % 60;
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }
    }
    #endregion
}
