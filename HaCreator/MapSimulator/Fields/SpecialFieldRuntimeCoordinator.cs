using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Character;
using HaCreator.MapEditor;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Spine;
using System;
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
        private readonly SpecialEffectFields _specialEffects = new();
        private readonly MinigameFields _minigames = new();
        private readonly CookieHouseField _cookieHouse = new();
        private readonly PartyRaidField _partyRaid = new();
        private Func<int> _cookieHousePointProvider;
        private readonly List<SpecialFieldBacklogEntry> _catalog = new()
        {
            new(SpecialFieldBacklogArea.GuildBossEventFields, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / GuildBossField", IsGuildBossMap),
            new(SpecialFieldBacklogArea.CoconutMinigameRuntime, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / CoconutField"),
            new(SpecialFieldBacklogArea.WeddingCeremonyFields, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / WeddingField", IsWeddingMap),
            new(SpecialFieldBacklogArea.WitchtowerScoreUi, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / WitchtowerField", IsWitchtowerMap),
            new(SpecialFieldBacklogArea.MassacreTimerboardAndGaugeFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / MassacreField", IsMassacreMap),
            new(SpecialFieldBacklogArea.MemoryGameAndMiniRoomCardParity, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / MemoryGameField"),
            new(SpecialFieldBacklogArea.SnowballMinigameRuntime, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / SnowBallField"),
            new(SpecialFieldBacklogArea.AriantArenaFieldFlow, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / AriantArenaField", IsAriantArenaMap),
            new(SpecialFieldBacklogArea.BattlefieldEventFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / BattlefieldField", IsBattlefieldMap),
            new(SpecialFieldBacklogArea.MuLungDojoFieldFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / DojoField", IsDojoMap),
            new(SpecialFieldBacklogArea.CookieHouseEventFlow, SpecialFieldBacklogStatus.Partial, "CookieHouseField.cs / special field runtime", IsCookieHouseMap),
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
        public bool HasBlockingScriptedSequence =>
            _specialEffects.HasBlockingScriptedSequence
            || _minigames.MemoryGame.IsVisible;

        public void Initialize(
            GraphicsDevice graphicsDevice,
            SoundManager soundManager = null,
            Action<string> requestBgmOverride = null,
            Action clearBgmOverride = null)
        {
            _specialEffects.Initialize(graphicsDevice, requestBgmOverride, clearBgmOverride);
            _minigames.Initialize(graphicsDevice, soundManager);
            _partyRaid.Initialize(graphicsDevice);
        }

        public void BindMap(Board board)
        {
            Reset();

            MapInfo mapInfo = board?.MapInfo;
            if (mapInfo == null)
            {
                return;
            }

            _specialEffects.DetectFieldType(mapInfo.id, mapInfo.fieldType);
            _specialEffects.ConfigureMap(board);
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

        public void SetAriantArenaPlayerState(string localPlayerName, int? localPlayerJob)
        {
            _minigames.AriantArena.SetLocalPlayerState(localPlayerName, localPlayerJob ?? 0);
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

        public int ConsumePendingTransferMapId()
        {
            int battlefieldTransferMapId = _specialEffects.Battlefield.ConsumePendingTransferMapId();
            if (battlefieldTransferMapId > 0)
            {
                return battlefieldTransferMapId;
            }

            int dojoTransferMapId = _specialEffects.Dojo.ConsumePendingTransferMapId();
            return dojoTransferMapId > 0 ? dojoTransferMapId : -1;
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
            _specialEffects.ResetAll();
            _minigames.ResetAll();
            _cookieHouse.Reset();
            _partyRaid.Reset();
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
                || (mapInfo.id >= 673000000 && mapInfo.id <= 673000099);
        }

        private static bool IsMassacreMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            return mapInfo.fieldType == FieldType.FIELDTYPE_MASSACRE
                || mapInfo.fieldType == FieldType.FIELDTYPE_MASSACRE_RESULT
                || (mapInfo.id >= 910000000 && mapInfo.id <= 910000099);
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
