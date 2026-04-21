using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapEditor;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Spine;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    public enum SpecialFieldBacklogArea
    {
        GuildBossEventFields,
        CoconutMinigameRuntime,
        WeddingCeremonyFields,
        WitchtowerScoreUi,
        MassacreTimerboardAndGaugeFlow,
        MemoryGameAndMiniRoomCardParity,
        SnowballMinigameRuntime,
        AriantArenaFieldFlow,
        BattlefieldEventFlow,
        MuLungDojoFieldFlow,
        CookieHouseEventFlow,
        CakePieEventTimerboardAndItemInfoParity,
        MonsterCarnivalFieldFlow,
        PartyRaidFieldFlow,
        SpaceGagaTimerboardFlow
    }

    public enum SpecialFieldBacklogStatus
    {
        Implemented,
        Partial,
        Missing
    }

    public sealed class SpecialFieldBacklogEntry
    {
        public SpecialFieldBacklogEntry(
            SpecialFieldBacklogArea area,
            SpecialFieldBacklogStatus status,
            string primarySeam,
            Func<MapInfo, bool> mapDetector = null)
        {
            Area = area;
            Status = status;
            PrimarySeam = primarySeam;
            MapDetector = mapDetector;
        }

        public SpecialFieldBacklogArea Area { get; }
        public SpecialFieldBacklogStatus Status { get; }
        public string PrimarySeam { get; }
        public Func<MapInfo, bool> MapDetector { get; }
    }

    /// <summary>
    /// Central coordinator for special field and minigame parity work.
    /// It exposes a stable backlog-aligned catalog so agents can own one row
    /// at a time without rediscovering where that runtime should attach.
    /// </summary>
    public sealed class SpecialFieldRuntimeCoordinator
    {
        public const int FieldSpecificDataRelayOpcode = 149;
        public const int CurrentWrapperRelayOpcode = 163;
        private const int KillCountPacketType = 178;
        private const int ChaosZakumPortalSessionFallbackFieldId = 180000002;
        private const int EscortFailOverlayDurationMs = 2500;

        private readonly SpecialEffectFields _specialEffects = new();
        private readonly MinigameFields _minigames = new();
        private readonly CookieHouseField _cookieHouse = new();
        private readonly PartyRaidField _partyRaid = new();
        private Func<int> _cookieHousePointProvider;
        private MapInfo _boundMapInfo;
        private int? _killCountWrapperValue;
        private int _escortFailOverlayUntilTick = int.MinValue;
        private readonly List<SpecialFieldBacklogEntry> _catalog = new()
        {
            new(SpecialFieldBacklogArea.GuildBossEventFields, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / GuildBossField", IsGuildBossMap),
            new(SpecialFieldBacklogArea.CoconutMinigameRuntime, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / CoconutField"),
            new(SpecialFieldBacklogArea.WeddingCeremonyFields, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / WeddingField", IsWeddingMap),
            new(SpecialFieldBacklogArea.WitchtowerScoreUi, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / WitchtowerField", IsWitchtowerMap),
            new(SpecialFieldBacklogArea.MassacreTimerboardAndGaugeFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / MassacreField", IsMassacreMap),
            new(SpecialFieldBacklogArea.MemoryGameAndMiniRoomCardParity, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / MemoryGameField"),
            new(SpecialFieldBacklogArea.SnowballMinigameRuntime, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / SnowBallField", IsSnowBallMap),
            new(SpecialFieldBacklogArea.AriantArenaFieldFlow, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / AriantArenaField", IsAriantArenaMap),
            new(SpecialFieldBacklogArea.BattlefieldEventFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / BattlefieldField", IsBattlefieldMap),
            new(SpecialFieldBacklogArea.MuLungDojoFieldFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / DojoField", IsDojoMap),
            new(SpecialFieldBacklogArea.CookieHouseEventFlow, SpecialFieldBacklogStatus.Partial, "CookieHouseField.cs / special field runtime", IsCookieHouseMap),
            new(SpecialFieldBacklogArea.CakePieEventTimerboardAndItemInfoParity, SpecialFieldBacklogStatus.Implemented, "SpecialEffectFields.cs / CakePieEventField", IsCakePieMap),
            new(SpecialFieldBacklogArea.MonsterCarnivalFieldFlow, SpecialFieldBacklogStatus.Partial, "MonsterCarnivalField.cs / event UI layer", IsMonsterCarnivalMap),
            new(SpecialFieldBacklogArea.PartyRaidFieldFlow, SpecialFieldBacklogStatus.Partial, "PartyRaidField.cs / PartyRaidField", IsPartyRaidMap),
            new(SpecialFieldBacklogArea.SpaceGagaTimerboardFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / SpaceGagaField", IsSpaceGagaMap),
        };

        public IReadOnlyList<SpecialFieldBacklogEntry> Catalog => _catalog;
        public SpecialFieldBacklogArea? ActiveArea { get; private set; }
        public SpecialEffectFields SpecialEffects => _specialEffects;
        public MinigameFields Minigames => _minigames;
        public CookieHouseField CookieHouse => _cookieHouse;
        public PartyRaidField PartyRaid => _partyRaid;
        public int? KillCountWrapperValue => _killCountWrapperValue;
        public int EscortFailOverlayUntilTick => _escortFailOverlayUntilTick;
        public bool HasPendingTransfer =>
            _specialEffects.Battlefield.HasPendingTransfer
            || _specialEffects.Dojo.PendingTransferMapId > 0;
        public bool HasBlockingScriptedSequence =>
            _specialEffects.HasBlockingScriptedSequence
            || _minigames.MemoryGame.IsVisible
            || _minigames.Tournament.MatchTableDialog.IsVisible
            || _minigames.RockPaperScissors.IsVisible;

        public void Initialize(
            GraphicsDevice graphicsDevice,
            SoundManager soundManager = null,
            Action<string> requestBgmOverride = null,
            Action clearBgmOverride = null,
            Func<LoginAvatarLook, string, CharacterBuild> weddingRemoteBuildFactory = null,
            Func<LoginAvatarLook, string, CharacterBuild> ariantArenaRemoteBuildFactory = null,
            CharacterLoader weddingCharacterLoader = null)
        {
            _specialEffects.Initialize(graphicsDevice, requestBgmOverride, clearBgmOverride, weddingRemoteBuildFactory, weddingCharacterLoader);
            _minigames.Initialize(graphicsDevice, soundManager, ariantArenaRemoteBuildFactory);
            _partyRaid.Initialize(graphicsDevice);
        }

        public void BindMap(Board board)
        {
            BindMap(board?.MapInfo, board);
        }

        internal void BindMap(MapInfo mapInfo)
        {
            BindMap(mapInfo, board: null);
        }

        private void BindMap(MapInfo mapInfo, Board board)
        {
            Reset();
            _boundMapInfo = mapInfo;
            if (mapInfo == null)
            {
                return;
            }

            _specialEffects.DetectFieldType(mapInfo);
            _specialEffects.ConfigureMap(board);
            _minigames.BindMap(board);
            _minigames.Tournament.Configure(mapInfo);
            _partyRaid.BindMap(mapInfo);

            if (IsAriantArenaMap(mapInfo))
            {
                _minigames.AriantArena.Enable();
            }

            _minigames.MonsterCarnival.Configure(mapInfo);

            if (IsCookieHouseMap(mapInfo))
            {
                _cookieHouse.Enable(mapInfo.id, _cookieHousePointProvider);
            }

            for (int i = 0; i < _catalog.Count; i++)
            {
                SpecialFieldBacklogEntry entry = _catalog[i];
                if (entry.MapDetector != null && entry.MapDetector(mapInfo))
                {
                    ActiveArea = entry.Area;
                    return;
                }
            }
        }

        public void SetWeddingPlayerState(int? localCharacterId, Vector2? localWorldPosition, CharacterBuild localPlayerBuild = null)
        {
            _specialEffects.SetWeddingPlayerState(localCharacterId, localWorldPosition, localPlayerBuild);
        }

        public void SetBattlefieldPlayerState(int? localCharacterId)
        {
            _specialEffects.SetBattlefieldPlayerState(localCharacterId);
        }

        public void SetSnowBallPlayerState(Vector2? localWorldPosition)
        {
            _minigames.SetSnowBallPlayerState(localWorldPosition);
        }

        public void SetDojoRuntimeState(int? playerHp, int? playerMaxHp, float? bossHpPercent)
        {
            _specialEffects.SetDojoRuntimeState(playerHp, playerMaxHp, bossHpPercent);
        }

        public void SetGuildBossPlayerState(Rectangle? localPlayerHitbox)
        {
            _specialEffects.SetGuildBossPlayerState(localPlayerHitbox);
        }

        public void SetAriantArenaPlayerState(string localPlayerName, int? localPlayerJob, RemoteUserActorPool remoteUserPool = null)
        {
            _minigames.SetAriantArenaRemoteUserPool(remoteUserPool);
            _minigames.AriantArena.SetLocalPlayerState(localPlayerName, localPlayerJob ?? 0);
        }

        public void SetMonsterCarnivalPlayerState(string localPlayerName)
        {
            _minigames.MonsterCarnival.SetLocalPlayerName(localPlayerName);
        }

        public void SetCookieHousePointProvider(Func<int> pointProvider)
        {
            _cookieHousePointProvider = pointProvider;
        }

        public void Update(GameTime gameTime, int currentTimeMs)
        {
            _specialEffects.Update(gameTime, currentTimeMs);
            _minigames.Update(currentTimeMs);
            _cookieHouse.Update();
            _partyRaid.Update(currentTimeMs);
        }

        public bool TryDispatchCurrentWrapperPacketRelay(int packetType, byte[] payload, int currentTimeMs, out string message)
        {
            NormalizeCurrentWrapperRelayPacket(ref packetType, ref payload);
            return TryDispatchCurrentWrapperRelayPayload(payload, currentTimeMs, out message);
        }

        public bool TryDispatchCurrentWrapperRelayPayload(byte[] relayPayload, int currentTimeMs, out string message)
        {
            if (!TryDecodeCurrentWrapperRelayPayload(relayPayload, out int packetType, out byte[] wrapperPayload, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            bool applied = TryDispatchCurrentWrapperPacketCore(packetType, wrapperPayload, currentTimeMs, out string relayMessage);
            if (!applied
                && _specialEffects.Dojo.IsActive
                && TryDecodeDojoPacketFromSharedRelay(packetType, wrapperPayload, out int dojoPacketType, out byte[] dojoPayload, out string dojoRelayEvidence))
            {
                applied = TryDispatchCurrentWrapperPacketCore(dojoPacketType, dojoPayload, currentTimeMs, out string dojoMessage);
                relayMessage = string.IsNullOrWhiteSpace(dojoMessage)
                    ? $"decoded Mu Lung Dojo packet {dojoPacketType} from nested relay packet-id prefixes ({dojoRelayEvidence})"
                    : $"decoded Mu Lung Dojo packet {dojoPacketType} from nested relay packet-id prefixes ({dojoRelayEvidence}). {dojoMessage}";
            }

            string relayPrefix =
                $"CField::OnPacket opcode {CurrentWrapperRelayOpcode} relayed wrapper packet {packetType}.";
            message = string.IsNullOrWhiteSpace(relayMessage)
                ? relayPrefix
                : $"{relayPrefix} {relayMessage}";
            return applied;
        }

        public bool TryDispatchCurrentWrapperPacket(int packetType, byte[] payload, int currentTimeMs, out string message)
        {
            return TryDispatchCurrentWrapperPacketCore(packetType, payload, currentTimeMs, out message);
        }

        public bool TryDispatchCurrentWrapperFieldValue(string key, string value, int currentTimeMs, out string message)
        {
            if (_partyRaid.IsActive
                && _boundMapInfo?.fieldType == FieldType.FIELDTYPE_HUNTINGADBALLOON)
            {
                bool applied = _partyRaid.OnFieldSetVariable(key, value);
                message = applied
                    ? $"CField_HuntingAdballoon::OnFieldSetVariable accepted {key}={value}. {_partyRaid.DescribeStatus()}"
                    : $"CField_HuntingAdballoon::OnFieldSetVariable rejected {key}={value}. field key not accepted";
                return applied;
            }

            if (IsEscortResultWrapperMap(_boundMapInfo)
                && MatchesEscortFailKey(key)
                && string.Equals(value, "fail", StringComparison.OrdinalIgnoreCase))
            {
                _escortFailOverlayUntilTick = currentTimeMs + EscortFailOverlayDurationMs;
                message = "CField_EscortResult::OnSessionValue accepted fail and armed the escort-result overlay.";
                return true;
            }

            message = $"No active client-owned special-field wrapper field-value owner accepted {key}={value}.";
            return false;
        }

        public bool TryDispatchCurrentWrapperSessionValue(string key, string value, out string message)
        {
            if (IsChaosZakumPortalSessionWrapperMap(_boundMapInfo)
                && IsChaosZakumPortalSessionKey(key))
            {
                message = $"CField_ChaosZakum::OnSessionValue accepted {key?.Trim()}={value ?? string.Empty}.";
                return true;
            }

            message = $"No active client-owned special-field wrapper session-value owner accepted {key}={value}.";
            return false;
        }

        public bool TryDispatchCurrentWrapperFieldSpecificData(
            Func<(bool Applied, string Message)> tryApplyPresentationOwner,
            out string message)
        {
            if (!IsShowaBathWrapperMap(_boundMapInfo))
            {
                message = null;
                return false;
            }

            const string ownerName = "CField_ShowaBath::OnFieldSpecificData";
            if (tryApplyPresentationOwner == null)
            {
                message = $"{ownerName} rejected field-specific presentation update. no simulator presentation callback was supplied.";
                return false;
            }

            (bool applied, string ownerMessage) = tryApplyPresentationOwner();
            message = applied
                ? $"{ownerName} accepted field-specific presentation update. {ownerMessage}"
                : $"{ownerName} rejected field-specific presentation update. {ownerMessage}";
            return applied;
        }

        public static byte[] BuildCurrentWrapperRelayPayload(int packetType, byte[] payload)
        {
            payload ??= Array.Empty<byte>();

            byte[] relayPayload = new byte[sizeof(ushort) + payload.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(relayPayload.AsSpan(0, sizeof(ushort)), (ushort)packetType);
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, relayPayload, sizeof(ushort), payload.Length);
            }

            return relayPayload;
        }

        public static void NormalizeCurrentWrapperRelayPacket(ref int packetType, ref byte[] payload)
        {
            payload ??= Array.Empty<byte>();
            if (packetType == CurrentWrapperRelayOpcode)
            {
                return;
            }

            payload = BuildCurrentWrapperRelayPayload(packetType, payload);
            packetType = CurrentWrapperRelayOpcode;
        }

        internal static bool TryDecodeCurrentWrapperRelayPayload(byte[] relayPayload, out int packetType, out byte[] wrapperPayload, out string error)
        {
            packetType = 0;
            wrapperPayload = Array.Empty<byte>();
            error = null;

            relayPayload ??= Array.Empty<byte>();
            if (relayPayload.Length < sizeof(ushort))
            {
                error =
                    $"CField::OnPacket opcode {CurrentWrapperRelayOpcode} relay requires a 2-byte wrapper packet id prefix.";
                return false;
            }

            packetType = BinaryPrimitives.ReadUInt16LittleEndian(relayPayload.AsSpan(0, sizeof(ushort)));
            int payloadLength = relayPayload.Length - sizeof(ushort);
            if (payloadLength == 0)
            {
                return true;
            }

            wrapperPayload = new byte[payloadLength];
            Buffer.BlockCopy(relayPayload, sizeof(ushort), wrapperPayload, 0, payloadLength);
            return true;
        }

        private static bool TryDecodeDojoPacketFromSharedRelay(
            int wrapperPacketType,
            byte[] wrapperPayload,
            out int dojoPacketType,
            out byte[] dojoPayload,
            out string evidence)
        {
            dojoPacketType = -1;
            dojoPayload = Array.Empty<byte>();
            evidence = string.Empty;
            wrapperPayload ??= Array.Empty<byte>();

            if (wrapperPacketType == FieldSpecificDataRelayOpcode
                && DojoField.TryDecodeFieldSpecificPacketPayload(wrapperPayload, out dojoPacketType, out dojoPayload, out _))
            {
                evidence = FieldSpecificDataRelayOpcode.ToString();
                return true;
            }

            if (wrapperPacketType != CurrentWrapperRelayOpcode)
            {
                return false;
            }

            return TryDecodeNestedDojoPacketFromRelayPayload(wrapperPayload, out dojoPacketType, out dojoPayload, out evidence);
        }

        private static bool TryDecodeNestedDojoPacketFromRelayPayload(
            byte[] relayPayload,
            out int dojoPacketType,
            out byte[] dojoPayload,
            out string evidence)
        {
            dojoPacketType = -1;
            dojoPayload = Array.Empty<byte>();
            evidence = string.Empty;
            relayPayload ??= Array.Empty<byte>();

            const int maxNestedRelayDepth = 8;
            List<int> relayPrefixChain = new();
            byte[] nestedRelayPayload = relayPayload;
            for (int depth = 0; depth < maxNestedRelayDepth; depth++)
            {
                if (!TryDecodeCurrentWrapperRelayPayload(
                        nestedRelayPayload,
                        out int nestedRelayPacketType,
                        out byte[] nestedPayload,
                        out _))
                {
                    return false;
                }

                relayPrefixChain.Add(nestedRelayPacketType);
                if (nestedRelayPacketType == FieldSpecificDataRelayOpcode
                    && DojoField.TryDecodeFieldSpecificPacketPayload(
                        nestedPayload,
                        out dojoPacketType,
                        out dojoPayload,
                        out _))
                {
                    evidence = string.Join("->", relayPrefixChain);
                    return true;
                }

                if (nestedRelayPacketType != CurrentWrapperRelayOpcode
                    && nestedRelayPacketType != FieldSpecificDataRelayOpcode)
                {
                    return false;
                }

                nestedRelayPayload = nestedPayload;
                if (nestedRelayPayload.Length < sizeof(ushort))
                {
                    break;
                }
            }

            return false;
        }

        private bool TryDispatchCurrentWrapperPacketCore(int packetType, byte[] payload, int currentTimeMs, out string message)
        {
            payload ??= Array.Empty<byte>();

            if (_specialEffects.TryDispatchActiveWrapperPacket(packetType, payload, currentTimeMs, out string ownerName, out string ownerMessage))
            {
                message = $"{ownerName} accepted packet {packetType}. {ownerMessage}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(ownerName) && !string.IsNullOrWhiteSpace(ownerMessage))
            {
                message = $"{ownerName} rejected packet {packetType}. {ownerMessage}";
                return false;
            }

            if (_minigames.TryDispatchActiveWrapperPacket(packetType, payload, currentTimeMs, out ownerName, out ownerMessage))
            {
                message = $"{ownerName} accepted packet {packetType}. {ownerMessage}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(ownerName) && !string.IsNullOrWhiteSpace(ownerMessage))
            {
                message = $"{ownerName} rejected packet {packetType}. {ownerMessage}";
                return false;
            }

            if (_partyRaid.IsActive)
            {
                string owner = _partyRaid.ActiveRuntimeOwnerName;
                bool applied = _partyRaid.TryApplyRawPacket(packetType, payload, currentTimeMs, out string ownerErrorMessage);
                message = applied
                    ? $"{owner} accepted packet {packetType}. {_partyRaid.DescribeStatus()}"
                    : $"{owner} rejected packet {packetType}. {ownerErrorMessage}";
                return applied;
            }

            if (TryDispatchSupplementalCurrentWrapperPacket(packetType, payload, currentTimeMs, out string supplementalOwnerName, out string supplementalOwnerMessage))
            {
                message = $"{supplementalOwnerName} accepted packet {packetType}. {supplementalOwnerMessage}";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(supplementalOwnerName) && !string.IsNullOrWhiteSpace(supplementalOwnerMessage))
            {
                message = $"{supplementalOwnerName} rejected packet {packetType}. {supplementalOwnerMessage}";
                return false;
            }

            message = $"No active client-owned special-field wrapper accepted packet {packetType}.";
            return false;
        }

        private bool TryDispatchSupplementalCurrentWrapperPacket(
            int packetType,
            byte[] payload,
            int currentTimeMs,
            out string ownerName,
            out string message)
        {
            if (packetType == KillCountPacketType && IsKillCountWrapperMap(_boundMapInfo))
            {
                ownerName = "CField_KillCount::OnPacket";
                if (payload == null || payload.Length < sizeof(int))
                {
                    message = "Kill-count packet requires a 4-byte payload.";
                    return false;
                }

                _killCountWrapperValue = Math.Max(0, BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, sizeof(int))));
                message = $"kill-count={_killCountWrapperValue.Value}";
                return true;
            }

            if (packetType == MassacreField.PacketTypeResult && _specialEffects.Massacre.IsActive)
            {
                ownerName = _specialEffects.Massacre.GetPacketOwnerName(packetType);
                bool applied = _specialEffects.Massacre.TryApplyMassacreResultPayload(payload, currentTimeMs, out string errorMessage);
                message = applied ? _specialEffects.Massacre.DescribeStatus() : errorMessage;
                return applied;
            }

            ownerName = null;
            message = null;
            return false;
        }

        public int ConsumePendingTransferMapId()
        {
            return TryConsumePendingTransfer(out int mapId, out _) ? mapId : -1;
        }

        public bool TryConsumePendingTransfer(out int mapId, out string portalName)
        {
            mapId = _specialEffects.Battlefield.ConsumePendingTransferMapId();
            portalName = null;
            if (mapId > 0)
            {
                return true;
            }

            return _specialEffects.Dojo.TryConsumePendingTransfer(out mapId, out portalName);
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            Texture2D pixelTexture,
            SpriteFont font = null)
        {
            _specialEffects.Draw(
                spriteBatch,
                skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                tickCount,
                pixelTexture,
                font);

            _minigames.Draw(
                spriteBatch,
                skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                tickCount,
                pixelTexture,
                font);

            if (_cookieHouse.IsActive)
            {
                _cookieHouse.Draw(spriteBatch, pixelTexture, font, centerX);
            }

            if (_partyRaid.IsActive)
            {
                _partyRaid.Draw(
                    spriteBatch,
                    skeletonMeshRenderer,
                    gameTime,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    pixelTexture,
                    font);
            }
        }

        public void Reset()
        {
            ActiveArea = null;
            _boundMapInfo = null;
            _killCountWrapperValue = null;
            _escortFailOverlayUntilTick = int.MinValue;
            _specialEffects.ResetAll();
            _minigames.ResetAll();
            _cookieHouse.Reset();
            _partyRaid.Reset();
        }

        private static bool IsKillCountWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_KILLCOUNT
                && !IsAranTutorialMap(mapInfo);
        }

        private static bool IsEscortResultWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_ESCORT_RESULT;
        }

        private static bool IsShowaBathWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_SHOWABATH
                || mapInfo?.id == 801000200
                || mapInfo?.id == 801000210;
        }

        private static bool IsChaosZakumPortalSessionWrapperMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_CHAOSZAKUM
                || mapInfo?.id == ChaosZakumPortalSessionFallbackFieldId;
        }

        private static bool IsChaosZakumPortalSessionKey(string key)
        {
            return string.Equals(key?.Trim(), "fire", StringComparison.Ordinal);
        }

        private static bool MatchesEscortFailKey(string key)
        {
            return string.Equals(key?.Trim(), "fail", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAranTutorialMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_TUTORIAL
                || (mapInfo?.id >= 914000000 && mapInfo?.id <= 914000500);
        }

        private static bool IsWeddingMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.fieldType == FieldType.FIELDTYPE_WEDDING
                || mapInfo.id == 680000110
                || mapInfo.id == 680000210;
        }

        private static bool IsWitchtowerMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.fieldType == FieldType.FIELDTYPE_WITCHTOWER
                || (mapInfo.id >= 922000000 && mapInfo.id <= 922000099);
        }

        private static bool IsGuildBossMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.fieldType == FieldType.FIELDTYPE_GUILDBOSS
                || (mapInfo.id >= 610030000 && mapInfo.id <= 610030099)
                || (mapInfo.id >= 673000000 && mapInfo.id <= 673000099)
                || GuildBossField.TryBuildMapContract(mapInfo, out _);
        }

        private static bool IsMassacreMap(MapInfo mapInfo)
        {
            return MassacreField.IsMassacreMap(mapInfo);
        }

        private static bool IsDojoMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.fieldType == FieldType.FIELDTYPE_DOJANG
                || (mapInfo.id >= 925020000 && mapInfo.id <= 925040999)
                || string.Equals(mapInfo.mapMark, "MuruengRaid", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(mapInfo.onUserEnter)
                    && mapInfo.onUserEnter.StartsWith("dojang", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBattlefieldMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.fieldType == FieldType.FIELDTYPE_BATTLEFIELD
                || mapInfo.additionalNonInfoProps.Any(prop => string.Equals(prop.Name, "battleField", StringComparison.OrdinalIgnoreCase))
                || (mapInfo.id >= 910040000 && mapInfo.id <= 910041399);
        }

        private static bool IsCookieHouseMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_COOKIEHOUSE;
        }

        private static bool IsCakePieMap(MapInfo mapInfo)
        {
            return mapInfo != null && CakePieEventField.IsSupportedField(mapInfo.id);
        }

        private static bool IsSnowBallMap(MapInfo mapInfo)
        {
            return SnowBallField.SnowBallFieldDataLoader.IsSnowBallMap(mapInfo);
        }

        private static bool IsAriantArenaMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_ARIANTARENA;
        }

        private static bool IsMonsterCarnivalMap(MapInfo mapInfo)
        {
            return MonsterCarnivalFieldDataLoader.IsMonsterCarnivalMap(mapInfo);
        }

        private static bool IsPartyRaidMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_PARTYRAID
                || mapInfo?.fieldType == FieldType.FIELDTYPE_PARTYRAID_BOSS
                || mapInfo?.fieldType == FieldType.FIELDTYPE_PARTYRAID_RESULT;
        }

        private static bool IsSpaceGagaMap(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_SPACEGAGA
                || (mapInfo?.id >= 922240000 && mapInfo.id <= 922240200);
        }
    }
}
