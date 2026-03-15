using HaCreator.MapSimulator.Effects;
using HaCreator.MapEditor;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using Spine;
using System;
using System.Collections.Generic;

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
        private readonly List<SpecialFieldBacklogEntry> _catalog = new()
        {
            new(SpecialFieldBacklogArea.GuildBossEventFields, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / GuildBossField", IsGuildBossMap),
            new(SpecialFieldBacklogArea.CoconutMinigameRuntime, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / CoconutField"),
            new(SpecialFieldBacklogArea.WeddingCeremonyFields, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / WeddingField", IsWeddingMap),
            new(SpecialFieldBacklogArea.WitchtowerScoreUi, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / WitchtowerField", IsWitchtowerMap),
            new(SpecialFieldBacklogArea.MassacreTimerboardAndGaugeFlow, SpecialFieldBacklogStatus.Partial, "SpecialEffectFields.cs / MassacreField", IsMassacreMap),
            new(SpecialFieldBacklogArea.MemoryGameAndMiniRoomCardParity, SpecialFieldBacklogStatus.Missing, "minigame room runtime / board UI layer"),
            new(SpecialFieldBacklogArea.SnowballMinigameRuntime, SpecialFieldBacklogStatus.Partial, "MinigameFields.cs / SnowBallField"),
            new(SpecialFieldBacklogArea.AriantArenaFieldFlow, SpecialFieldBacklogStatus.Missing, "special field runtime / ranking or result UI"),
            new(SpecialFieldBacklogArea.BattlefieldEventFlow, SpecialFieldBacklogStatus.Missing, "special field runtime / scoreboard layer"),
            new(SpecialFieldBacklogArea.MuLungDojoFieldFlow, SpecialFieldBacklogStatus.Missing, "special field runtime / timer or score HUD"),
            new(SpecialFieldBacklogArea.CookieHouseEventFlow, SpecialFieldBacklogStatus.Missing, "special field runtime"),
            new(SpecialFieldBacklogArea.MonsterCarnivalFieldFlow, SpecialFieldBacklogStatus.Missing, "special field runtime / event UI layer"),
            new(SpecialFieldBacklogArea.PartyRaidFieldFlow, SpecialFieldBacklogStatus.Missing, "special field runtime / scoreboard and timer layer"),
            new(SpecialFieldBacklogArea.SpaceGagaTimerboardFlow, SpecialFieldBacklogStatus.Missing, "special field runtime / timerboard UI"),
        };

        public IReadOnlyList<SpecialFieldBacklogEntry> Catalog => _catalog;
        public SpecialFieldBacklogArea? ActiveArea { get; private set; }
        public SpecialEffectFields SpecialEffects => _specialEffects;
        public MinigameFields Minigames => _minigames;
        public bool HasBlockingScriptedSequence => _specialEffects.HasBlockingScriptedSequence;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            _specialEffects.Initialize(graphicsDevice);
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

        public void SetWeddingPlayerState(int? localCharacterId, Vector2? localWorldPosition)
        {
            _specialEffects.SetWeddingPlayerState(localCharacterId, localWorldPosition);
        }

        public void Update(GameTime gameTime, int currentTimeMs)
        {
            _specialEffects.Update(gameTime, currentTimeMs);
            _minigames.Update(currentTimeMs);
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
        }

        public void Reset()
        {
            ActiveArea = null;
            _specialEffects.ResetAll();
            _minigames.ResetAll();
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
    }
}
