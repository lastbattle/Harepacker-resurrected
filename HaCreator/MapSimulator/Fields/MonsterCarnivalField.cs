using MapleLib.Helpers;
using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using MapleLib.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaSharedLibrary.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace HaCreator.MapSimulator.Fields
{
    public enum MonsterCarnivalTab
    {
        Mob = 0,
        Skill = 1,
        Guardian = 2
    }

    public enum MonsterCarnivalTeam
    {
        Team0 = 0,
        Team1 = 1
    }

    public enum MonsterCarnivalPacketType
    {
        Enter = 1,
        RequestResult = 2,
        RequestFailure = 3,
        GameResult = 4,
        ProcessForDeath = 5,
        CpUpdate = 6,
        CpDelta = 7,
        SummonedMobCount = 8
    }

    public enum MonsterCarnivalRawPacketType
    {
        Enter = 346,
        PersonalCp = 347,
        TeamCp = 348,
        RequestResult = 349,
        RequestFailure = 350,
        ProcessForDeath = 351,
        ShowMemberOutMessage = 352,
        GameResult = 353
    }

    public enum MonsterCarnivalVariantSessionPhase
    {
        None = 0,
        Init = 1,
        HudSync = 2,
        LiveHud = 3,
        Request = 4,
        MemberState = 5,
        DeathState = 6,
        ResultRoute = 7,
        UiWindow = 8
    }

    internal readonly record struct MonsterCarnivalStringPoolMessage(int StringPoolId, string FallbackFormat);

    public sealed class MonsterCarnivalEntry
    {
        public MonsterCarnivalEntry(
            MonsterCarnivalTab tab,
            int index,
            int id,
            int cost,
            string name,
            string description,
            int rewardCp = 0,
            IReadOnlyList<int> reviveMobIds = null)
        {
            Tab = tab;
            Index = index;
            Id = id;
            Cost = cost;
            Name = string.IsNullOrWhiteSpace(name) ? $"{tab} {id}" : name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            RewardCp = Math.Max(0, rewardCp);
            ReviveMobIds = reviveMobIds ?? Array.Empty<int>();
        }

        public MonsterCarnivalTab Tab { get; }
        public int Index { get; }
        public int Id { get; }
        public int Cost { get; }
        public string Name { get; }
        public string Description { get; }
        public int RewardCp { get; }
        public IReadOnlyList<int> ReviveMobIds { get; }
    }

    public sealed class MonsterCarnivalTeamState
    {
        public int CurrentCp { get; set; }
        public int TotalCp { get; set; }
    }

    public readonly record struct MonsterCarnivalSpawnPoint(int Index, int X, int Y, int Foothold, int Cy);

    public readonly record struct MonsterCarnivalGuardianSpawnPoint(int Index, int X, int Y, int Facing);

    public sealed class MonsterCarnivalSummonedMobState
    {
        public MonsterCarnivalSummonedMobState(MonsterCarnivalEntry entry, MonsterCarnivalSpawnPoint spawnPoint)
        {
            Entry = entry;
            SpawnPoint = spawnPoint;
        }

        public MonsterCarnivalEntry Entry { get; }
        public MonsterCarnivalSpawnPoint SpawnPoint { get; }
    }

    public sealed class MonsterCarnivalGuardianPlacement
    {
        public MonsterCarnivalGuardianPlacement(MonsterCarnivalEntry entry, MonsterCarnivalGuardianSpawnPoint spawnPoint, int reactorId, MonsterCarnivalTeam team)
        {
            Entry = entry;
            SpawnPoint = spawnPoint;
            ReactorId = reactorId;
            Team = team;
            ReactorRequiredHits = 1;
        }

        public MonsterCarnivalEntry Entry { get; }
        public MonsterCarnivalGuardianSpawnPoint SpawnPoint { get; }
        public int ReactorId { get; }
        public MonsterCarnivalTeam Team { get; }
        public int ReactorHitCount { get; set; }
        public int ReactorRequiredHits { get; set; }
    }

    public sealed class MonsterCarnivalUiWindowState
    {
        public bool IsCreated { get; private set; }
        public int MobRows { get; private set; }
        public int SkillRows { get; private set; }
        public int GuardianRows { get; private set; }
        public int ResetCount { get; private set; }
        public MonsterCarnivalTeam? Team { get; private set; }
        public int PersonalCp { get; private set; }
        public int PersonalTotalCp { get; private set; }
        public int MyTeamCp { get; private set; }
        public int MyTeamTotalCp { get; private set; }
        public int EnemyTeamCp { get; private set; }
        public int EnemyTeamTotalCp { get; private set; }
        public int? LastRequestCooldownResetTick { get; private set; }
        public string LastRequestOwnerName { get; private set; }
        public string LastRequestResetOutcome { get; private set; }
        public int LastRequestTab { get; private set; } = -1;
        public int LastRequestIndex { get; private set; } = -1;

        public int TotalRows => MobRows + SkillRows + GuardianRows;

        public void Reset()
        {
            IsCreated = false;
            MobRows = 0;
            SkillRows = 0;
            GuardianRows = 0;
            ResetCount = 0;
            Team = null;
            PersonalCp = 0;
            PersonalTotalCp = 0;
            MyTeamCp = 0;
            MyTeamTotalCp = 0;
            EnemyTeamCp = 0;
            EnemyTeamTotalCp = 0;
            LastRequestCooldownResetTick = null;
            LastRequestOwnerName = null;
            LastRequestResetOutcome = null;
            LastRequestTab = -1;
            LastRequestIndex = -1;
        }

        public void CreateFromDefinition(MonsterCarnivalFieldDefinition definition)
        {
            IsCreated = true;
            MobRows = definition?.MobEntries.Count ?? 0;
            SkillRows = definition?.SkillEntries.Count ?? 0;
            GuardianRows = definition?.GuardianEntries.Count ?? 0;
            ResetCount++;
            LastRequestCooldownResetTick = null;
            LastRequestOwnerName = null;
            LastRequestResetOutcome = null;
            LastRequestTab = -1;
            LastRequestIndex = -1;
        }

        public void ApplyEnter(
            MonsterCarnivalTeam team,
            int personalCp,
            int personalTotalCp,
            int myTeamCp,
            int myTeamTotalCp,
            int enemyTeamCp,
            int enemyTeamTotalCp)
        {
            Team = team;
            PersonalCp = Math.Max(0, personalCp);
            PersonalTotalCp = Math.Max(PersonalCp, personalTotalCp);
            MyTeamCp = Math.Max(0, myTeamCp);
            MyTeamTotalCp = Math.Max(MyTeamCp, myTeamTotalCp);
            EnemyTeamCp = Math.Max(0, enemyTeamCp);
            EnemyTeamTotalCp = Math.Max(EnemyTeamCp, enemyTeamTotalCp);
        }

        public void MarkRequestCooldownReset(int tickCount, string ownerName, string outcome, int tabCode, int entryIndex)
        {
            LastRequestCooldownResetTick = tickCount;
            LastRequestOwnerName = string.IsNullOrWhiteSpace(ownerName) ? null : ownerName.Trim();
            LastRequestResetOutcome = string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim();
            LastRequestTab = tabCode;
            LastRequestIndex = entryIndex;
        }

        public string DescribeStatus()
        {
            if (!IsCreated)
            {
                return "CUIMonsterCarnival: not created.";
            }

            string teamText = Team.HasValue ? MonsterCarnivalField.FormatTeam(Team.Value) : "unset";
            string requestText = LastRequestCooldownResetTick.HasValue
                ? $"requestReset={LastRequestResetOutcome ?? "unknown"}:{LastRequestTab}/{LastRequestIndex}@{LastRequestCooldownResetTick.Value}"
                : "requestReset=none";
            return $"CUIMonsterCarnival: created | rows mob/skill/guardian={MobRows}/{SkillRows}/{GuardianRows} total={TotalRows} | ResetUI={ResetCount} | team={teamText} | personalCP={PersonalCp}/{PersonalTotalCp} | myTeamCP={MyTeamCp}/{MyTeamTotalCp} | enemyTeamCP={EnemyTeamCp}/{EnemyTeamTotalCp} | {requestText}";
        }
    }

    public sealed class MonsterCarnivalFieldDefinition
    {
        public int MapId { get; init; }
        public FieldType FieldType { get; init; }
        public int MapType { get; init; } = -1;
        public string MapName { get; init; }
        public string StreetName { get; init; }
        public int DefaultTimeSeconds { get; init; }
        public int ExpandTimeSeconds { get; init; }
        public int MessageTimeSeconds { get; init; }
        public int FinishTimeSeconds { get; init; }
        public int DeathCp { get; init; }
        public int RewardMapWin { get; init; }
        public int RewardMapLose { get; init; }
        public int MobGenMax { get; init; }
        public int GuardianGenMax { get; init; }
        public int ReactorRed { get; init; }
        public int ReactorBlue { get; init; }
        public int ReactorRedRequiredHits { get; init; } = 1;
        public int ReactorBlueRequiredHits { get; init; } = 1;
        public string EffectWin { get; init; }
        public string EffectLose { get; init; }
        public string SoundWin { get; init; }
        public string SoundLose { get; init; }
        public IReadOnlyList<MonsterCarnivalSpawnPoint> MobSpawnPositions { get; init; } = Array.Empty<MonsterCarnivalSpawnPoint>();
        public IReadOnlyList<MonsterCarnivalGuardianSpawnPoint> GuardianSpawnPositions { get; init; } = Array.Empty<MonsterCarnivalGuardianSpawnPoint>();
        public IReadOnlyList<MonsterCarnivalEntry> MobEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public IReadOnlyList<MonsterCarnivalEntry> SkillEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public IReadOnlyList<MonsterCarnivalEntry> GuardianEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public FieldType ResolvedFieldType => ResolveClientOwnedVariant(FieldType, MapType);
        public bool IsReviveMode => ResolvedFieldType == FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE;
        public bool IsWaitingRoom => ResolvedFieldType == FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM;
        public bool IsSeason2Mode => ResolvedFieldType == FieldType.FIELDTYPE_MONSTERCARNIVAL_S2;
        public bool IsDeprecatedMode => ResolvedFieldType == FieldType.FIELDTYPE_MONSTERCARNIVAL_NOT_USE;

        public string VariantLabel => ResolvedFieldType switch
        {
            FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => "Waiting Room",
            FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => "Season 2",
            FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => "Revive",
            FieldType.FIELDTYPE_MONSTERCARNIVAL_NOT_USE => "Legacy",
            _ => "Standard"
        };

        public string ClientOwnerLabel => ResolvedFieldType switch
        {
            FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => "CField_MonsterCarnivalWaitingRoom",
            FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => "CField_MonsterCarnivalS2_Game",
            FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => "CField_MonsterCarnivalRevive",
            _ => "CField_MonsterCarnival"
        };

        public string InitBaseOwnerLabel => ResolvedFieldType switch
        {
            FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => "CField::Init",
            FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => "CField_MonsterCarnival::Init",
            _ => $"{ClientOwnerLabel}::Init"
        };

        public static FieldType ResolveClientOwnedVariant(FieldType fieldType, int mapType)
        {
            if (fieldType != FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM)
            {
                return fieldType;
            }

            return mapType switch
            {
                1 => FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE,
                2 => FieldType.FIELDTYPE_MONSTERCARNIVAL_S2,
                _ => FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM
            };
        }

        public IReadOnlyList<MonsterCarnivalEntry> GetEntries(MonsterCarnivalTab tab)
        {
            return tab switch
            {
                MonsterCarnivalTab.Mob => MobEntries,
                MonsterCarnivalTab.Skill => SkillEntries,
                MonsterCarnivalTab.Guardian => GuardianEntries,
                _ => Array.Empty<MonsterCarnivalEntry>()
            };
        }
    }

    public static class MonsterCarnivalFieldDataLoader
    {
        private const string MonsterCarnivalPropertyName = "monsterCarnival";
        private const string MobStringImageName = "Mob.img";
        private const string McSkillImageName = "MCSkill.img";
        private const string McGuardianImageName = "MCGuardian.img";

        public static bool IsMonsterCarnivalMap(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return false;
            }

            if (mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVAL_S2
                || mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE
                || mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM
                || mapInfo.fieldType == FieldType.FIELDTYPE_MONSTERCARNIVAL_NOT_USE)
            {
                return true;
            }

            return FindMonsterCarnivalProperty(mapInfo) != null;
        }

        public static MonsterCarnivalFieldDefinition Load(MapInfo mapInfo)
        {
            if (!IsMonsterCarnivalMap(mapInfo))
            {
                return null;
            }

            WzImageProperty property = FindMonsterCarnivalProperty(mapInfo);
            if (property == null)
            {
                return new MonsterCarnivalFieldDefinition
                {
                    MapId = mapInfo?.id ?? 0,
                    FieldType = mapInfo?.fieldType ?? FieldType.FIELDTYPE_DEFAULT
                };
            }

            return new MonsterCarnivalFieldDefinition
            {
                MapId = mapInfo?.id ?? 0,
                FieldType = mapInfo?.fieldType ?? FieldType.FIELDTYPE_DEFAULT,
                MapName = NormalizeMapLabel(mapInfo?.strMapName, mapInfo?.mapName),
                StreetName = NormalizeMapLabel(mapInfo?.strStreetName, mapInfo?.streetName),
                MapType = ReadInt(property["mapType"], -1),
                DefaultTimeSeconds = ReadInt(property["timeDefault"]),
                ExpandTimeSeconds = ReadInt(property["timeExpand"]),
                MessageTimeSeconds = ReadInt(property["timeMessage"]),
                FinishTimeSeconds = ReadInt(property["timeFinish"]),
                DeathCp = ReadInt(property["deathCP"]),
                RewardMapWin = ReadInt(property["rewardMapWin"]),
                RewardMapLose = ReadInt(property["rewardMapLose"]),
                MobGenMax = ReadInt(property["mobGenMax"]),
                GuardianGenMax = ReadInt(property["guardianGenMax"]),
                ReactorRed = ReadInt(property["reactorRed"]),
                ReactorBlue = ReadInt(property["reactorBlue"]),
                ReactorRedRequiredHits = ResolveGuardianReactorRequiredHits(ReadInt(property["reactorRed"])),
                ReactorBlueRequiredHits = ResolveGuardianReactorRequiredHits(ReadInt(property["reactorBlue"])),
                EffectWin = ReadString(property["effectWin"]),
                EffectLose = ReadString(property["effectLose"]),
                SoundWin = ReadString(property["soundWin"]),
                SoundLose = ReadString(property["soundLose"]),
                MobSpawnPositions = LoadMobSpawnPositions(property["mobGenPos"]),
                GuardianSpawnPositions = LoadGuardianSpawnPositions(property["guardianGenPos"]),
                MobEntries = LoadMobEntries(property["mob"]),
                SkillEntries = LoadNamedEntries(property["skill"], MonsterCarnivalTab.Skill, McSkillImageName),
                GuardianEntries = LoadNamedEntries(property["guardian"], MonsterCarnivalTab.Guardian, McGuardianImageName)
            };
        }

        public static MonsterCarnivalEntry CreateResolvedMobEntry(int mobId, int index = -1)
        {
            if (mobId <= 0)
            {
                return null;
            }

            string name = ResolveMobName(mobId) ?? $"Mob {mobId}";
            MonsterCarnivalMobMetadata metadata = ResolveMobMetadata(mobId);
            return new MonsterCarnivalEntry(
                MonsterCarnivalTab.Mob,
                index,
                mobId,
                0,
                name,
                BuildMobDescription(name, metadata),
                metadata?.RewardCp ?? 0,
                metadata?.ReviveMobIds ?? Array.Empty<int>());
        }

        private static WzImageProperty FindMonsterCarnivalProperty(MapInfo mapInfo)
        {
            if (mapInfo?.additionalNonInfoProps == null)
            {
                return null;
            }

            for (int i = 0; i < mapInfo.additionalNonInfoProps.Count; i++)
            {
                WzImageProperty property = mapInfo.additionalNonInfoProps[i];
                if (string.Equals(property?.Name, MonsterCarnivalPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }

        private static IReadOnlyList<MonsterCarnivalEntry> LoadMobEntries(WzImageProperty property)
        {
            var entries = new List<MonsterCarnivalEntry>();
            if (property?.WzProperties == null)
            {
                return entries;
            }

            foreach (WzImageProperty child in EnumerateIndexedChildren(property))
            {
                int mobId = ReadInt(child["id"]);
                int cost = ReadInt(child["spendCP"]);
                string name = ResolveMobName(mobId) ?? $"Mob {mobId}";
                MonsterCarnivalMobMetadata metadata = ResolveMobMetadata(mobId);
                string description = BuildMobDescription(name, metadata);
                entries.Add(new MonsterCarnivalEntry(
                    MonsterCarnivalTab.Mob,
                    ParseChildIndex(child.Name, entries.Count),
                    mobId,
                    cost,
                    name,
                    description,
                    metadata?.RewardCp ?? 0,
                    metadata?.ReviveMobIds ?? Array.Empty<int>()));
            }

            return entries;
        }

        private static IReadOnlyList<MonsterCarnivalEntry> LoadNamedEntries(WzImageProperty property, MonsterCarnivalTab tab, string imageName)
        {
            var entries = new List<MonsterCarnivalEntry>();
            if (property?.WzProperties == null)
            {
                return entries;
            }

            WzImage definitionImage = FindImageSafe("Skill", imageName);
            foreach (WzImageProperty child in EnumerateIndexedChildren(property))
            {
                int entryId = ReadInt(child);
                WzImageProperty definition = definitionImage?[entryId.ToString(CultureInfo.InvariantCulture)];
                string name = ReadString(definition?["name"]) ?? $"{tab} {entryId}";
                string description = ReadString(definition?["desc"]);
                int cost = ReadInt(definition?["spendCP"]);
                entries.Add(new MonsterCarnivalEntry(
                    tab,
                    ParseChildIndex(child.Name, entries.Count),
                    entryId,
                    cost,
                    name,
                    description));
            }

            return entries;
        }

        private static IReadOnlyList<MonsterCarnivalSpawnPoint> LoadMobSpawnPositions(WzImageProperty property)
        {
            var positions = new List<MonsterCarnivalSpawnPoint>();
            if (property?.WzProperties == null)
            {
                return positions;
            }

            foreach (WzImageProperty child in EnumerateIndexedChildren(property))
            {
                positions.Add(new MonsterCarnivalSpawnPoint(
                    ParseChildIndex(child.Name, positions.Count),
                    ReadInt(child["x"]),
                    ReadInt(child["y"]),
                    ReadInt(child["fh"], -1),
                    ReadInt(child["cy"], ReadInt(child["y"]))));
            }

            return positions;
        }

        private static IReadOnlyList<MonsterCarnivalGuardianSpawnPoint> LoadGuardianSpawnPositions(WzImageProperty property)
        {
            var positions = new List<MonsterCarnivalGuardianSpawnPoint>();
            if (property?.WzProperties == null)
            {
                return positions;
            }

            foreach (WzImageProperty child in EnumerateIndexedChildren(property))
            {
                positions.Add(new MonsterCarnivalGuardianSpawnPoint(
                    ParseChildIndex(child.Name, positions.Count),
                    ReadInt(child["x"]),
                    ReadInt(child["y"]),
                    ReadInt(child["f"])));
            }

            return positions;
        }

        private static IEnumerable<WzImageProperty> EnumerateIndexedChildren(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in property.WzProperties.Cast<WzImageProperty>().OrderBy(candidate => ParseChildIndex(candidate.Name, int.MaxValue)))
            {
                yield return child;
            }
        }

        private static int ParseChildIndex(string name, int fallback)
        {
            return int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        private static string ResolveMobName(int mobId)
        {
            if (mobId <= 0)
            {
                return null;
            }

            try
            {
                WzImage stringImage = FindImageSafe("String", MobStringImageName);
                return ReadString(stringImage?[mobId.ToString(CultureInfo.InvariantCulture)]?["name"]);
            }
            catch
            {
                return null;
            }
        }

        private static MonsterCarnivalMobMetadata ResolveMobMetadata(int mobId)
        {
            if (mobId <= 0)
            {
                return null;
            }

            try
            {
                WzImage mobImage = FindImageSafe("Mob", mobId.ToString("D7", CultureInfo.InvariantCulture) + ".img");
                if (mobImage != null && !mobImage.Parsed)
                {
                    mobImage.ParseImage();
                }

                MobData mobData = MobData.Parse(mobImage, mobId);
                if (mobData == null)
                {
                    return null;
                }

                return new MonsterCarnivalMobMetadata(
                    mobData.GetCP,
                    mobData.ReviveData?.Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>());
            }
            catch
            {
                return null;
            }
        }

        private static string BuildMobDescription(string name, MonsterCarnivalMobMetadata metadata)
        {
            if (metadata == null)
            {
                return $"Summon {name}.";
            }

            var parts = new List<string>
            {
                $"Summon {name}."
            };

            if (metadata.RewardCp > 0)
            {
                parts.Add($"Defeating it awards {metadata.RewardCp} CP.");
            }

            if (metadata.ReviveMobIds.Count > 0)
            {
                string reviveText = string.Join(", ", metadata.ReviveMobIds.Select(id => ResolveMobName(id) ?? id.ToString(CultureInfo.InvariantCulture)));
                parts.Add($"Revives into {reviveText}.");
            }

            return string.Join(" ", parts);
        }

        private static int ResolveGuardianReactorRequiredHits(int reactorId)
        {
            if (reactorId <= 0)
            {
                return 1;
            }

            try
            {
                WzImage reactorImage = FindImageSafe("Reactor", reactorId.ToString("D7", CultureInfo.InvariantCulture) + ".img");
                if (reactorImage == null)
                {
                    return 1;
                }

                if (!reactorImage.Parsed)
                {
                    reactorImage.ParseImage();
                }

                int numericStateCount = reactorImage.WzProperties
                    .Select(property => property?.Name)
                    .Count(name => int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
                return Math.Max(1, numericStateCount);
            }
            catch
            {
                return 1;
            }
        }

        private static WzImage FindImageSafe(string category, string imageName)
        {
            try
            {
                return global::HaCreator.Program.FindImage(category, imageName);
            }
            catch
            {
                return null;
            }
        }

        private static int ReadInt(WzImageProperty property, int defaultValue = 0)
        {
            if (property == null)
            {
                return defaultValue;
            }

            try
            {
                return InfoTool.GetInt(property);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static string ReadString(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            try
            {
                return InfoTool.GetString(property);
            }
            catch
            {
                return property.GetString();
            }
        }

        private static string NormalizeMapLabel(string preferred, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(preferred) && !string.Equals(preferred, "<Untitled>", StringComparison.OrdinalIgnoreCase))
            {
                return preferred.Trim();
            }

            return string.IsNullOrWhiteSpace(fallback)
                ? string.Empty
                : fallback.Trim();
        }

        private sealed class MonsterCarnivalMobMetadata
        {
            public MonsterCarnivalMobMetadata(int rewardCp, IReadOnlyList<int> reviveMobIds)
            {
                RewardCp = Math.Max(0, rewardCp);
                ReviveMobIds = reviveMobIds ?? Array.Empty<int>();
            }

            public int RewardCp { get; }
            public IReadOnlyList<int> ReviveMobIds { get; }
        }
    }

    /// <summary>
    /// Partial Monster Carnival runtime.
    /// Mirrors the client's dedicated field owner enough to surface the
    /// Carnival-only HUD, CP counters, item lists, and request/result flow.
    /// </summary>
    public sealed class MonsterCarnivalField
    {
        private readonly record struct MonsterCarnivalHudMetrics(
            int BackgroundWidth,
            int BackgroundHeight,
            int TeamPanelWidth,
            int TeamPanelHeight,
            int ListTopHeight,
            int ListMiddleRowHeight,
            int ListSummaryHeight,
            int ListBottomHeight,
            int TabMobWidth,
            int TabSkillWidth,
            int TabGuardianWidth,
            int SideButtonWidth,
            int SideButtonHeight)
        {
            public int TotalHeaderWidth => BackgroundWidth + TeamPanelWidth;
            public int EntryListWidth => BackgroundWidth;
            public int EntryListHeight => ListTopHeight + (ListMiddleRowHeight * 6) + ListSummaryHeight + ListBottomHeight;
            public int TotalHeight => BackgroundHeight + EntryListHeight;
        }

        private sealed class MonsterCarnivalHudAssets
        {
            public Texture2D Background { get; init; }
            public Texture2D TeamPanelBackground { get; init; }
            public Texture2D ListTop { get; init; }
            public Texture2D ListMiddle { get; init; }
            public Texture2D ListSummary { get; init; }
            public Texture2D ListBottom { get; init; }
            public Texture2D SideButtonNormal { get; init; }
            public Texture2D SideButtonDisabled { get; init; }
            public Texture2D TabMobEnabled { get; init; }
            public Texture2D TabSkillEnabled { get; init; }
            public Texture2D TabGuardianEnabled { get; init; }
            public Texture2D TabMobDisabled { get; init; }
            public Texture2D TabSkillDisabled { get; init; }
            public Texture2D TabGuardianDisabled { get; init; }
            public bool HasAnyTexture =>
                Background != null
                || TeamPanelBackground != null
                || ListTop != null
                || ListMiddle != null
                || ListSummary != null
                || ListBottom != null;
        }

        private static readonly MonsterCarnivalHudMetrics DefaultHudMetrics = new(
            BackgroundWidth: 162,
            BackgroundHeight: 107,
            TeamPanelWidth: 127,
            TeamPanelHeight: 107,
            ListTopHeight: 24,
            ListMiddleRowHeight: 19,
            ListSummaryHeight: 13,
            ListBottomHeight: 35,
            TabMobWidth: 21,
            TabSkillWidth: 33,
            TabGuardianWidth: 45,
            SideButtonWidth: 11,
            SideButtonHeight: 67);

        public void Initialize(GraphicsDevice device)
        {
            _graphicsDevice = device;
            LoadHudAssets();
        }

        private const int StatusDurationMs = 4500;
        private const int VariantActionTrailCapacity = 4;

        private readonly Dictionary<int, int> _mobSpellCounts = new();
        private readonly Dictionary<int, int> _skillUseCounts = new();
        private readonly Dictionary<int, int> _guardianCounts = new();
        private readonly List<MonsterCarnivalSummonedMobState> _summonedMobs = new();
        private readonly Dictionary<int, MonsterCarnivalGuardianPlacement> _guardianPlacements = new();
        private readonly Dictionary<string, MonsterCarnivalTeam> _knownCharacterTeams = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _occupiedGuardianSlots = new();
        private readonly MonsterCarnivalTeamState _team0 = new();
        private readonly MonsterCarnivalTeamState _team1 = new();
        private MonsterCarnivalFieldDefinition _definition;
        private MonsterCarnivalTeam _localTeam;
        private MonsterCarnivalTab _activeTab;
        private string _statusMessage;
        private int _statusMessageUntil;
        private int _selectedEntryIndex;
        private int _personalCp;
        private int _personalTotalCp;
        private int _nextMobSpawnPointIndex;
        private int _lastRequestTab;
        private int _lastRequestIndex;
        private bool _isVisible;
        private bool _enteredField;
        private string _localCharacterName;
        private string _lastClientOwnerAction;
        private int[] _lastClientOwnerStringPoolIds = Array.Empty<int>();
        private readonly Queue<string> _variantActionTrail = new();
        private readonly MonsterCarnivalUiWindowState _uiWindowState = new();
        private MonsterCarnivalVariantSessionPhase _variantSessionPhase;
        private string _variantSessionSummary;
        private GraphicsDevice _graphicsDevice;
        private MonsterCarnivalHudAssets _hudAssets = new();

        public bool IsVisible => _isVisible;
        public bool IsEntered => _enteredField;
        public MonsterCarnivalFieldDefinition Definition => _definition;
        public MonsterCarnivalTeam LocalTeam => _localTeam;
        public MonsterCarnivalTab ActiveTab => _activeTab;
        public int PersonalCp => _personalCp;
        public int PersonalTotalCp => _personalTotalCp;
        public string CurrentStatusMessage => _statusMessage;
        public string LastClientOwnerAction => _lastClientOwnerAction;
        public MonsterCarnivalTeamState Team0 => _team0;
        public MonsterCarnivalTeamState Team1 => _team1;
        public IReadOnlyDictionary<int, int> MobSpellCounts => _mobSpellCounts;
        public IReadOnlyList<MonsterCarnivalSummonedMobState> SummonedMobs => _summonedMobs;
        public IReadOnlyDictionary<int, MonsterCarnivalGuardianPlacement> GuardianPlacements => _guardianPlacements;
        public IReadOnlyList<int> LastClientOwnerStringPoolIds => _lastClientOwnerStringPoolIds;
        public MonsterCarnivalVariantSessionPhase VariantSessionPhase => _variantSessionPhase;
        public IReadOnlyList<string> VariantActionTrail => _variantActionTrail.ToArray();
        public MonsterCarnivalUiWindowState UiWindowState => _uiWindowState;

        public void Configure(MapInfo mapInfo)
        {
            Configure(MonsterCarnivalFieldDataLoader.Load(mapInfo));
        }

        public void Configure(MonsterCarnivalFieldDefinition definition)
        {
            Reset();
            _definition = definition;
            _isVisible = _definition != null;
            _activeTab = MonsterCarnivalTab.Mob;
            _lastClientOwnerAction = BuildInitialClientOwnerAction(_definition);
            _lastClientOwnerStringPoolIds = Array.Empty<int>();
            _variantActionTrail.Clear();
            SetVariantSessionPhase(MonsterCarnivalVariantSessionPhase.Init, BuildInitialVariantSessionSummary(_definition));
            InitializeClientOwnedUiWindowState(_definition);
        }

        public void SetLocalPlayerName(string characterName)
        {
            _localCharacterName = string.IsNullOrWhiteSpace(characterName)
                ? null
                : characterName.Trim();
            RegisterKnownCharacterTeam(_localCharacterName, _localTeam);
        }

        public bool TryResolveCharacterTeam(string characterName, out MonsterCarnivalTeam team)
        {
            return TryResolveKnownCharacterTeam(characterName, out team);
        }

        public void OnEnter(
            MonsterCarnivalTeam localTeam,
            int personalCp,
            int personalTotalCp,
            int myTeamCp,
            int myTeamTotalCp,
            int enemyTeamCp,
            int enemyTeamTotalCp)
        {
            if (!_isVisible)
            {
                return;
            }

            ClearRoundState();
            RecreateClientOwnedUiWindowStateForEnter();
            _enteredField = true;
            _localTeam = localTeam;
            _personalCp = Math.Max(0, personalCp);
            _personalTotalCp = Math.Max(_personalCp, personalTotalCp);
            RegisterKnownCharacterTeam(_localCharacterName, _localTeam);

            MonsterCarnivalTeamState myTeam = GetTeamState(localTeam);
            MonsterCarnivalTeamState enemyTeam = GetTeamState(GetOpposingTeam(localTeam));
            myTeam.CurrentCp = Math.Max(0, myTeamCp);
            myTeam.TotalCp = Math.Max(myTeam.CurrentCp, myTeamTotalCp);
            enemyTeam.CurrentCp = Math.Max(0, enemyTeamCp);
            enemyTeam.TotalCp = Math.Max(enemyTeam.CurrentCp, enemyTeamTotalCp);
            _uiWindowState.ApplyEnter(
                localTeam,
                _personalCp,
                _personalTotalCp,
                myTeam.CurrentCp,
                myTeam.TotalCp,
                enemyTeam.CurrentCp,
                enemyTeam.TotalCp);

            ShowStatus(BuildEnterStatusMessage(localTeam), Environment.TickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.HudSync,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} synchronized the shared Carnival HUD through OnEnter.");
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnEnter refreshed the Carnival HUD state.",
                new[] { 0x1027 });
        }

        public void UpdateTeamCp(
            int personalCp,
            int personalTotalCp,
            int team0CurrentCp,
            int team0TotalCp,
            int team1CurrentCp,
            int team1TotalCp)
        {
            if (!_isVisible)
            {
                return;
            }

            _personalCp = Math.Max(0, personalCp);
            _personalTotalCp = Math.Max(_personalCp, personalTotalCp);
            _team0.CurrentCp = Math.Max(0, team0CurrentCp);
            _team0.TotalCp = Math.Max(_team0.CurrentCp, team0TotalCp);
            _team1.CurrentCp = Math.Max(0, team1CurrentCp);
            _team1.TotalCp = Math.Max(_team1.CurrentCp, team1TotalCp);
            RefreshClientOwnedUiWindowCpState();
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.LiveHud,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} refreshed live CP totals on the shared Carnival seam.");
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnPersonalCP/OnTeamCP refreshed CP totals.",
                Array.Empty<int>());
        }

        public void UpdatePersonalCp(int personalCp, int personalTotalCp)
        {
            if (!_isVisible)
            {
                return;
            }

            _personalCp = Math.Max(0, personalCp);
            _personalTotalCp = Math.Max(_personalCp, personalTotalCp);
            RefreshClientOwnedUiWindowCpState();
            MarkClientOwnedCpHudRefresh(
                "OnPersonalCP",
                "CUIMonsterCarnival::SetPersonalCP");
        }

        public void UpdateTeamCp(MonsterCarnivalTeam team, int currentCp, int totalCp)
        {
            if (!_isVisible)
            {
                return;
            }

            MonsterCarnivalTeamState teamState = GetTeamState(team);
            teamState.CurrentCp = Math.Max(0, currentCp);
            teamState.TotalCp = Math.Max(teamState.CurrentCp, totalCp);
            RefreshClientOwnedUiWindowCpState();
            MarkClientOwnedCpHudRefresh(
                "OnTeamCP",
                $"CUIMonsterCarnival::SetTeamCP({FormatTeam(team)})");
        }

        public void ApplyTeamCpDelta(
            int personalCpDelta,
            int personalTotalCpDelta,
            int team0CurrentCpDelta,
            int team0TotalCpDelta,
            int team1CurrentCpDelta,
            int team1TotalCpDelta,
            int tickCount)
        {
            if (!_isVisible)
            {
                return;
            }

            _personalCp = Math.Max(0, _personalCp + personalCpDelta);
            _personalTotalCp = Math.Max(_personalCp, _personalTotalCp + personalTotalCpDelta);
            _team0.CurrentCp = Math.Max(0, _team0.CurrentCp + team0CurrentCpDelta);
            _team0.TotalCp = Math.Max(_team0.CurrentCp, _team0.TotalCp + team0TotalCpDelta);
            _team1.CurrentCp = Math.Max(0, _team1.CurrentCp + team1CurrentCpDelta);
            _team1.TotalCp = Math.Max(_team1.CurrentCp, _team1.TotalCp + team1TotalCpDelta);
            RefreshClientOwnedUiWindowCpState();

            ShowStatus(
                $"Monster Carnival CP delta applied: personal {FormatSignedDelta(personalCpDelta)}, team0 {FormatSignedDelta(team0CurrentCpDelta)}, team1 {FormatSignedDelta(team1CurrentCpDelta)}.",
                tickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.LiveHud,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} applied a live CP delta update on the shared Carnival seam.");
        }

        public bool TrySetActiveTab(string tabText, out string message)
        {
            if (!TryParseTab(tabText, out MonsterCarnivalTab tab))
            {
                message = $"Unknown Monster Carnival tab: {tabText}";
                return false;
            }

            _activeTab = tab;
            _selectedEntryIndex = 0;
            message = DescribeStatus();
            return true;
        }

        public bool TrySetMobSpellCount(int index, int count, out string message)
        {
            MonsterCarnivalEntry entry = GetEntry(MonsterCarnivalTab.Mob, index);
            if (entry == null)
            {
                message = $"Monster Carnival mob index {index} is out of range.";
                return false;
            }

            int normalizedCount = Math.Max(0, count);
            SetEntryCount(_mobSpellCounts, entry.Id, normalizedCount);
            ReconcileSummonedMobStates(entry, normalizedCount);
            message = $"{entry.Name} summon count set to {GetEntryCount(_mobSpellCounts, entry.Id)}.";
            return true;
        }

        public bool TryRequestActiveEntry(int index, string requestMessage, int tickCount, out string message)
        {
            MonsterCarnivalEntry entry = GetEntry(_activeTab, index);
            if (entry == null)
            {
                message = $"Monster Carnival {_activeTab.ToString().ToLowerInvariant()} index {index} is out of range.";
                return false;
            }

            if (!TryValidateRequest(entry, out int rejectionReason))
            {
                OnRequestFailure(rejectionReason, tickCount);
                message = DescribeRequestFailure(rejectionReason);
                return false;
            }

            OnRequestResult((byte)_activeTab, entry.Index, requestMessage, tickCount);
            message = DescribeStatus();
            return true;
        }

        public void OnRequestResult(byte tabCode, int entryIndex, string characterName, int tickCount)
        {
            MonsterCarnivalTab tab = TryParseTab(tabCode, out MonsterCarnivalTab parsedTab)
                ? parsedTab
                : _activeTab;
            MonsterCarnivalEntry entry = GetEntry(tab, entryIndex);
            if (entry == null)
            {
                string fallbackMessage = string.IsNullOrWhiteSpace(characterName)
                    ? $"Monster Carnival request result received for tab {(int)tab}, index {entryIndex}."
                    : characterName.Trim();
                ShowStatus(fallbackMessage, tickCount);
                return;
            }

            bool spendLocalCp = IsLocalRequestOwner(characterName);
            bool ownerTeamKnown = TryResolveKnownCharacterTeam(characterName, out MonsterCarnivalTeam ownerTeam);
            ApplySuccessfulRequest(entry, ownerTeamKnown ? ownerTeam : _localTeam, spendLocalCp, ownerTeamKnown);
            string successMessage = BuildRequestSuccessMessage(entry, characterName);
            ShowStatus(successMessage, tickCount);
            MonsterCarnivalStringPoolMessage? successDefinition = GetRequestSuccessMessage(entry.Tab);
            if (spendLocalCp)
            {
                _uiWindowState.MarkRequestCooldownReset(tickCount, _definition?.ClientOwnerLabel, "success", (int)tab, entryIndex);
            }
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.Request,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} accepted tab {(int)tab}, index {entryIndex}, and advanced the request seam.");
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnRequestResult accepted tab {(int)tab}, index {entryIndex}, and reset the local request timer state.",
                successDefinition.HasValue ? new[] { successDefinition.Value.StringPoolId } : Array.Empty<int>());
        }

        public void OnRequestFailure(int reasonCode, int tickCount)
        {
            ShowStatus(DescribeRequestFailure(reasonCode), tickCount);
            MonsterCarnivalStringPoolMessage? definition = GetRequestFailureMessage(reasonCode);
            _uiWindowState.MarkRequestCooldownReset(tickCount, _definition?.ClientOwnerLabel, "failure", _lastRequestTab, _lastRequestIndex);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.Request,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} rejected request reason {reasonCode} on the shared Carnival seam.");
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnRequestResult rejected reason {reasonCode} and reset the local request timer state.",
                definition.HasValue ? new[] { definition.Value.StringPoolId } : Array.Empty<int>());
        }

        public void OnShowGameResult(int resultCode, int tickCount)
        {
            string status = DescribeGameResult(resultCode);
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            ShowStatus(status, tickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.ResultRoute,
                BuildVariantResultSummary(resultCode));
        }

        public void OnProcessForDeath(MonsterCarnivalTeam team, string characterName, int remainingRevives, int tickCount)
        {
            if (!_isVisible)
            {
                return;
            }

            int deathCp = Math.Max(0, _definition?.DeathCp ?? 0);
            RegisterKnownCharacterTeam(characterName, team);
            MonsterCarnivalTeamState teamState = GetTeamState(team);
            teamState.CurrentCp = Math.Max(0, teamState.CurrentCp - deathCp);
            string deathMessage = BuildProcessForDeathMessage(team, characterName, remainingRevives);
            if (deathCp > 0)
            {
                deathMessage = $"{deathMessage} {deathCp} CP was removed from {FormatTeam(team)}.";
            }

            ShowStatus(deathMessage, tickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.DeathState,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} applied ProcessForDeath for {FormatTeam(team)}.");
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnProcessForDeath updated {FormatTeam(team)} death state.",
                remainingRevives > 0
                    ? new[] { 0x1019, GetTeamLabelMessage(team).StringPoolId }
                    : new[] { 0x101A, GetTeamLabelMessage(team).StringPoolId });
        }

        public bool TryProcessMobDefeat(int spawnPointIndex, MonsterCarnivalTeam rewardedTeam, int tickCount, out string message)
        {
            message = null;
            if (!_isVisible)
            {
                message = "Monster Carnival runtime inactive.";
                return false;
            }

            MonsterCarnivalSummonedMobState defeatedMob = _summonedMobs.FirstOrDefault(mob => mob.SpawnPoint.Index == spawnPointIndex);
            if (defeatedMob?.Entry == null)
            {
                message = $"No Monster Carnival summon is tracked at spawn slot {spawnPointIndex}.";
                return false;
            }

            _summonedMobs.Remove(defeatedMob);
            SetEntryCount(_mobSpellCounts, defeatedMob.Entry.Id, Math.Max(0, GetEntryCount(_mobSpellCounts, defeatedMob.Entry.Id) - 1));

            int rewardCp = Math.Max(0, defeatedMob.Entry.RewardCp);
            if (rewardCp > 0)
            {
                MonsterCarnivalTeamState rewardedTeamState = GetTeamState(rewardedTeam);
                rewardedTeamState.CurrentCp += rewardCp;
                rewardedTeamState.TotalCp += rewardCp;

                if (rewardedTeam == _localTeam)
                {
                    _personalCp += rewardCp;
                    _personalTotalCp += rewardCp;
                }
            }

            IReadOnlyList<MonsterCarnivalSummonedMobState> revivedMobs = SpawnReviveChain(defeatedMob);
            string rewardText = rewardCp > 0
                ? $" {FormatTeam(rewardedTeam)} gained {rewardCp} CP."
                : string.Empty;
            string reviveText = revivedMobs.Count > 0
                ? $" Revived into {string.Join(", ", revivedMobs.Select(mob => mob.Entry.Name))}."
                : string.Empty;

            message = $"{defeatedMob.Entry.Name} was defeated at slot {spawnPointIndex}.{rewardText}{reviveText}".Trim();
            ShowStatus(message, tickCount);
            return true;
        }

        public bool TrySyncGuardianReactorProgress(int slotIndex, int hitCount, int requiredHits)
        {
            if (!_guardianPlacements.TryGetValue(slotIndex, out MonsterCarnivalGuardianPlacement placement) || placement?.Entry == null)
            {
                return false;
            }

            placement.ReactorRequiredHits = Math.Max(1, requiredHits);
            if (hitCount > 0)
            {
                placement.ReactorHitCount = Math.Min(hitCount, placement.ReactorRequiredHits);
            }

            return true;
        }

        public bool TryApplyNextGuardianReactorHit(int slotIndex, int tickCount, out string message)
        {
            message = null;
            if (!_guardianPlacements.TryGetValue(slotIndex, out MonsterCarnivalGuardianPlacement placement) || placement?.Entry == null)
            {
                message = $"No Monster Carnival guardian is tracked at slot {slotIndex}.";
                return false;
            }

            int requiredHits = Math.Max(1, placement.ReactorRequiredHits);
            int nextHitCount = Math.Min(requiredHits, Math.Max(0, placement.ReactorHitCount) + 1);
            return TryApplyGuardianReactorHit(
                slotIndex,
                nextHitCount,
                requiredHits,
                nextHitCount >= requiredHits,
                tickCount,
                out message);
        }

        public bool TryDestroyGuardianReactor(int slotIndex, int tickCount, out string message)
        {
            message = null;
            if (!_guardianPlacements.TryGetValue(slotIndex, out MonsterCarnivalGuardianPlacement placement) || placement?.Entry == null)
            {
                message = $"No Monster Carnival guardian is tracked at slot {slotIndex}.";
                return false;
            }

            int requiredHits = Math.Max(1, placement.ReactorRequiredHits);
            int hitCount = Math.Max(requiredHits, placement.ReactorHitCount);
            return TryApplyGuardianReactorHit(
                slotIndex,
                hitCount,
                requiredHits,
                destroyPlacement: true,
                tickCount,
                out message);
        }

        public bool TryApplyGuardianReactorHit(int slotIndex, int hitCount, int requiredHits, bool destroyPlacement, int tickCount, out string message)
        {
            message = null;
            if (!_isVisible)
            {
                message = "Monster Carnival runtime inactive.";
                return false;
            }

            if (!_guardianPlacements.TryGetValue(slotIndex, out MonsterCarnivalGuardianPlacement placement) || placement?.Entry == null)
            {
                message = $"No Monster Carnival guardian is tracked at slot {slotIndex}.";
                return false;
            }

            placement.ReactorRequiredHits = Math.Max(1, requiredHits);
            if (hitCount > 0)
            {
                placement.ReactorHitCount = Math.Min(hitCount, placement.ReactorRequiredHits);
            }
            else if (destroyPlacement && placement.ReactorHitCount <= 0)
            {
                placement.ReactorHitCount = placement.ReactorRequiredHits;
            }

            if (destroyPlacement)
            {
                _guardianPlacements.Remove(slotIndex);
                _occupiedGuardianSlots.Remove(slotIndex);
                SetEntryCount(_guardianCounts, placement.Entry.Id, Math.Max(0, GetEntryCount(_guardianCounts, placement.Entry.Id) - 1));
                message = $"{placement.Entry.Name} at slot {slotIndex + 1} was destroyed after reactor hit {placement.ReactorHitCount}/{placement.ReactorRequiredHits}.";
            }
            else
            {
                if (placement.ReactorHitCount <= 0)
                {
                    placement.ReactorHitCount = 1;
                }

                message = $"{placement.Entry.Name} at slot {slotIndex + 1} took reactor hit {placement.ReactorHitCount}/{placement.ReactorRequiredHits}.";
            }

            ShowStatus(message, tickCount);
            return true;
        }

        public void OnShowMemberOutMessage(int messageType, MonsterCarnivalTeam team, string characterName, int tickCount)
        {
            if (!_isVisible)
            {
                return;
            }

            RegisterKnownCharacterTeam(characterName, team);
            ShowStatus(BuildMemberOutMessage(messageType, team, characterName), tickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.MemberState,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} updated member ownership for {FormatTeam(team)}.");
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnShowMemberOutMsg updated {FormatTeam(team)} member state.",
                new[] { messageType == 6 ? 0x102A : 0x1029, GetTeamLabelMessage(team).StringPoolId });
        }

        public bool TryApplyPacket(MonsterCarnivalPacketType packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;

            if (!_isVisible)
            {
                errorMessage = "Monster Carnival runtime inactive.";
                return false;
            }

            if (!TryValidateClientOwnedPacket(packetType, out errorMessage))
            {
                return false;
            }

            payload ??= Array.Empty<byte>();

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                switch (packetType)
                {
                    case MonsterCarnivalPacketType.Enter:
                        OnEnter((MonsterCarnivalTeam)reader.ReadByte(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());

                        for (int i = 0; i < (_definition?.MobEntries.Count ?? 0); i++)
                        {
                            int count = reader.ReadByte();
                            MonsterCarnivalEntry entry = _definition.MobEntries[i];
                            SetEntryCount(_mobSpellCounts, entry.Id, count);
                            ReconcileSummonedMobStates(entry, count);
                        }

                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "enter");
                        return true;

                    case MonsterCarnivalPacketType.RequestResult:
                        OnRequestResult(reader.ReadByte(), reader.ReadByte(), ReadPacketString(reader), currentTimeMs);
                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "request-result");
                        return true;

                    case MonsterCarnivalPacketType.RequestFailure:
                        OnRequestFailure(reader.ReadByte(), currentTimeMs);
                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "request-failure");
                        return true;

                    case MonsterCarnivalPacketType.GameResult:
                        OnShowGameResult(reader.ReadByte(), currentTimeMs);
                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "game-result");
                        return true;

                    case MonsterCarnivalPacketType.ProcessForDeath:
                        OnProcessForDeath((MonsterCarnivalTeam)reader.ReadByte(), ReadPacketString(reader), reader.ReadByte(), currentTimeMs);
                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "process-for-death");
                        return true;

                    case MonsterCarnivalPacketType.CpUpdate:
                        UpdateTeamCp(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "cp-update");
                        return true;

                    case MonsterCarnivalPacketType.CpDelta:
                        ApplyTeamCpDelta(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), currentTimeMs);
                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "cp-delta");
                        return true;

                    case MonsterCarnivalPacketType.SummonedMobCount:
                        if (!TrySetMobSpellCount(reader.ReadByte(), reader.ReadByte(), out errorMessage))
                        {
                            return false;
                        }

                        RecordVariantWrapperPacketDelegation((int)packetType, rawPacket: false);
                        EnsurePacketConsumed(stream, "summoned-mob-count");
                        return true;

                    default:
                        errorMessage = $"Unsupported Monster Carnival packet type: {packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is InvalidDataException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool TryApplyRawPacket(int packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;

            if (!_isVisible)
            {
                errorMessage = "Monster Carnival runtime inactive.";
                return false;
            }

            if (!TryValidateClientOwnedRawPacket(packetType, out errorMessage))
            {
                return false;
            }

            payload ??= Array.Empty<byte>();

            try
            {
                using var stream = new MemoryStream(payload, writable: false);
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: false);
                switch ((MonsterCarnivalRawPacketType)packetType)
                {
                    case MonsterCarnivalRawPacketType.Enter:
                        OnEnter((MonsterCarnivalTeam)reader.ReadByte(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());

                        for (int i = 0; i < (_definition?.MobEntries.Count ?? 0); i++)
                        {
                            int count = reader.ReadByte();
                            MonsterCarnivalEntry entry = _definition.MobEntries[i];
                            SetEntryCount(_mobSpellCounts, entry.Id, count);
                            ReconcileSummonedMobStates(entry, count);
                        }

                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-enter");
                        return true;

                    case MonsterCarnivalRawPacketType.PersonalCp:
                        UpdatePersonalCp(reader.ReadInt16(), reader.ReadInt16());
                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-personal-cp");
                        return true;

                    case MonsterCarnivalRawPacketType.TeamCp:
                        UpdateTeamCp((MonsterCarnivalTeam)reader.ReadByte(), reader.ReadInt16(), reader.ReadInt16());
                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-team-cp");
                        return true;

                    case MonsterCarnivalRawPacketType.RequestResult:
                        OnRequestResult(reader.ReadByte(), reader.ReadByte(), ReadPacketString(reader), currentTimeMs);
                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-request-result");
                        return true;

                    case MonsterCarnivalRawPacketType.RequestFailure:
                        OnRequestFailure(reader.ReadByte(), currentTimeMs);
                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-request-failure");
                        return true;

                    case MonsterCarnivalRawPacketType.ProcessForDeath:
                        OnProcessForDeath((MonsterCarnivalTeam)reader.ReadByte(), ReadPacketString(reader), reader.ReadByte(), currentTimeMs);
                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-process-for-death");
                        return true;

                    case MonsterCarnivalRawPacketType.ShowMemberOutMessage:
                        OnShowMemberOutMessage(reader.ReadByte(), (MonsterCarnivalTeam)reader.ReadByte(), ReadPacketString(reader), currentTimeMs);
                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-show-member-out");
                        return true;

                    case MonsterCarnivalRawPacketType.GameResult:
                        OnShowGameResult(reader.ReadByte(), currentTimeMs);
                        RecordVariantWrapperPacketDelegation(packetType, rawPacket: true);
                        EnsurePacketConsumed(stream, "raw-game-result");
                        return true;

                    default:
                        errorMessage = $"Unsupported Monster Carnival raw packet type: {packetType}";
                        return false;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is IOException || ex is InvalidDataException)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public void Update(int tickCount)
        {
            if (_statusMessage != null && tickCount >= _statusMessageUntil)
            {
                _statusMessage = null;
            }
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isVisible || spriteBatch == null || pixelTexture == null || font == null)
            {
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            MonsterCarnivalHudMetrics metrics = ResolveHudMetrics();
            Rectangle headerBounds = ResolveHeaderBounds(viewport.Width, metrics);
            Rectangle mainHeaderBounds = new(headerBounds.X, headerBounds.Y, metrics.BackgroundWidth, metrics.BackgroundHeight);
            Rectangle teamHeaderBounds = new(headerBounds.Right, headerBounds.Y, metrics.TeamPanelWidth, metrics.TeamPanelHeight);
            Rectangle listBounds = new(headerBounds.X, headerBounds.Bottom, metrics.EntryListWidth, metrics.EntryListHeight);
            Rectangle footerBounds = ResolveFooterBounds(listBounds, metrics);

            DrawHeaderPanel(spriteBatch, pixelTexture, mainHeaderBounds, teamHeaderBounds);
            DrawEntryListPanel(spriteBatch, pixelTexture, listBounds, metrics);
            DrawFooterPanel(spriteBatch, pixelTexture, footerBounds);

            DrawShadowedText(spriteBatch, font, GetPanelTitle(), new Vector2(mainHeaderBounds.X + 12, mainHeaderBounds.Y + 8), Color.White);

            int timerY = mainHeaderBounds.Y + 44;
            string headerText = _definition == null
                ? "No WZ carnival definition loaded."
                : $"Time {_definition.DefaultTimeSeconds}s +{_definition.ExpandTimeSeconds}s | Death CP {_definition.DeathCp} | {_definition.VariantLabel}{FormatMapTypeSuffix(_definition)} | owner={_definition.ClientOwnerLabel}";
            DrawShadowedText(spriteBatch, font, headerText, new Vector2(mainHeaderBounds.X + 12, timerY), Color.Gainsboro, 0.85f);
            DrawShadowedText(
                spriteBatch,
                font,
                BuildClientOwnerHeaderSummary(),
                new Vector2(mainHeaderBounds.X + 12, timerY + 18),
                Color.LightSteelBlue,
                0.75f);
            DrawShadowedText(
                spriteBatch,
                font,
                BuildVariantContractSummary(),
                new Vector2(mainHeaderBounds.X + 12, timerY + 34),
                Color.LightGoldenrodYellow,
                0.72f);

            DrawCpRow(spriteBatch, pixelTexture, font, teamHeaderBounds.X + 10, teamHeaderBounds.Y + 10, teamHeaderBounds.Width - 20);
            DrawTabs(spriteBatch, pixelTexture, font, listBounds.X + 10, listBounds.Y + 6, listBounds.Width - 20);
            DrawEntryList(spriteBatch, pixelTexture, font, listBounds.X + 8, listBounds.Y + metrics.ListTopHeight + 2, listBounds.Width - 16, metrics.ListMiddleRowHeight * 6);
            DrawFooter(spriteBatch, pixelTexture, font, footerBounds.X + 8, footerBounds.Y + 4, footerBounds.Width - 16, footerBounds.Height - 8);
        }

        public string DescribeStatus()
        {
            if (!_isVisible)
            {
                return "Monster Carnival runtime is inactive on this map.";
            }

            return $"Monster Carnival: {(_enteredField ? "entered" : "configured")} | mode={_definition?.VariantLabel ?? "Unknown"}{FormatMapTypeSuffix(_definition)} | owner={_definition?.ClientOwnerLabel ?? "unknown"} | tab={_activeTab} | personalCP={_personalCp}/{_personalTotalCp} | team0={_team0.CurrentCp}/{_team0.TotalCp} | team1={_team1.CurrentCp}/{_team1.TotalCp} | mobs={GetTotalCount(_mobSpellCounts)}/{Math.Max(0, _definition?.MobGenMax ?? 0)} | guardians={GetTotalCount(_guardianCounts)}/{Math.Max(0, _definition?.GuardianGenMax ?? 0)} | variantPhase={DescribeVariantSessionPhase()} | seam={BuildClientOwnerStatusSummary()}";
        }

        public void Reset()
        {
            _definition = null;
            ClearRoundState();
            _team0.CurrentCp = 0;
            _team0.TotalCp = 0;
            _team1.CurrentCp = 0;
            _team1.TotalCp = 0;
            _localTeam = MonsterCarnivalTeam.Team0;
            _activeTab = MonsterCarnivalTab.Mob;
            _statusMessage = null;
            _statusMessageUntil = 0;
            _personalCp = 0;
            _personalTotalCp = 0;
            _isVisible = false;
            _enteredField = false;
            _lastClientOwnerAction = null;
            _lastClientOwnerStringPoolIds = Array.Empty<int>();
            _variantActionTrail.Clear();
            _uiWindowState.Reset();
            _variantSessionPhase = MonsterCarnivalVariantSessionPhase.None;
            _variantSessionSummary = null;
        }

        private void ClearRoundState()
        {
            _mobSpellCounts.Clear();
            _skillUseCounts.Clear();
            _guardianCounts.Clear();
            _summonedMobs.Clear();
            _guardianPlacements.Clear();
            _knownCharacterTeams.Clear();
            _occupiedGuardianSlots.Clear();
            _nextMobSpawnPointIndex = 0;
            _selectedEntryIndex = 0;
            _lastRequestTab = -1;
            _lastRequestIndex = -1;
        }

        private void DrawCpRow(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width)
        {
            int rowHeight = 44;
            if (_hudAssets.TeamPanelBackground == null)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, rowHeight), new Color(26, 34, 44, 220));
            }
            string personalText = $"Personal CP  {_personalCp}/{_personalTotalCp}";
            DrawShadowedText(spriteBatch, font, personalText, new Vector2(x + 10, y + 6), Color.White);

            string team0Text = $"{FormatTeam(MonsterCarnivalTeam.Team0)}  {_team0.CurrentCp}/{_team0.TotalCp}";
            string team1Text = $"{FormatTeam(MonsterCarnivalTeam.Team1)}  {_team1.CurrentCp}/{_team1.TotalCp}";
            DrawShadowedText(spriteBatch, font, team0Text, new Vector2(x + 10, y + 24), GetTeamColor(MonsterCarnivalTeam.Team0));
            DrawShadowedText(spriteBatch, font, team1Text, new Vector2(x + width / 2 + 4, y + 24), GetTeamColor(MonsterCarnivalTeam.Team1));
        }

        private void DrawTabs(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width)
        {
            MonsterCarnivalHudMetrics metrics = ResolveHudMetrics();
            int tabX = x;
            DrawTab(spriteBatch, pixelTexture, font, tabX, y, metrics.TabMobWidth, "Mob", MonsterCarnivalTab.Mob);
            tabX += metrics.TabMobWidth + 2;
            DrawTab(spriteBatch, pixelTexture, font, tabX, y, metrics.TabSkillWidth, "Skill", MonsterCarnivalTab.Skill);
            tabX += metrics.TabSkillWidth + 2;
            DrawTab(spriteBatch, pixelTexture, font, tabX, y, metrics.TabGuardianWidth, "Guardian", MonsterCarnivalTab.Guardian);

            Texture2D sideButton = _activeTab == MonsterCarnivalTab.Guardian
                ? _hudAssets.SideButtonNormal
                : _hudAssets.SideButtonDisabled ?? _hudAssets.SideButtonNormal;
            if (sideButton != null)
            {
                spriteBatch.Draw(sideButton, new Rectangle(x + width - metrics.SideButtonWidth, y - 4, metrics.SideButtonWidth, metrics.SideButtonHeight), Color.White);
            }
        }

        private void DrawTab(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, string label, MonsterCarnivalTab tab)
        {
            Texture2D tabTexture = GetTabTexture(tab, _activeTab == tab);
            if (tabTexture != null)
            {
                spriteBatch.Draw(tabTexture, new Rectangle(x, y, width, tabTexture.Height), Color.White);
            }
            else
            {
                Color background = _activeTab == tab
                    ? new Color(71, 103, 140, 255)
                    : new Color(31, 42, 56, 255);
                spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width - 1, 26), background);
            }

            Vector2 size = font.MeasureString(label);
            int height = tabTexture?.Height ?? 26;
            Vector2 position = new Vector2(x + (width - size.X) * 0.5f, y + Math.Max(1f, (height - size.Y) * 0.5f - 1f));
            DrawShadowedText(spriteBatch, font, label, position, Color.White);
        }

        private void DrawEntryList(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, int height)
        {
            if (_hudAssets.ListMiddle == null)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, height), new Color(20, 27, 34, 200));
            }
            IReadOnlyList<MonsterCarnivalEntry> entries = _definition?.GetEntries(_activeTab) ?? Array.Empty<MonsterCarnivalEntry>();
            if (entries.Count == 0)
            {
                DrawShadowedText(spriteBatch, font, "No entries loaded for this tab.", new Vector2(x + 10, y + 10), Color.Silver);
                return;
            }

            int rowHeight = 15;
            int visibleRows = Math.Min(entries.Count, height / rowHeight);
            for (int i = 0; i < visibleRows; i++)
            {
                MonsterCarnivalEntry entry = entries[i];
                Rectangle rowRect = new Rectangle(x + 4, y + 4 + i * rowHeight, width - 8, rowHeight - 1);
                bool isSelected = entry.Index == _selectedEntryIndex && _activeTab == (MonsterCarnivalTab)_lastRequestTab;
                Color rowColor = isSelected
                    ? new Color(67, 92, 120, 220)
                    : new Color(31, 39, 49, 180);
                spriteBatch.Draw(pixelTexture, rowRect, rowColor);

                int count = GetEntryUsageCount(entry);
                string countText = count > 0
                    ? $"x{count}"
                    : string.Empty;
                string line = $"{entry.Index + 1}. {entry.Name}";
                DrawShadowedText(spriteBatch, font, line, new Vector2(rowRect.X + 6, rowRect.Y), Color.White, 0.82f);

                string costText = $"CP {entry.Cost}";
                Vector2 costSize = font.MeasureString(costText) * 0.82f;
                float countOffset = 0f;
                if (!string.IsNullOrWhiteSpace(countText))
                {
                    Vector2 countSize = font.MeasureString(countText) * 0.82f;
                    DrawShadowedText(spriteBatch, font, countText, new Vector2(rowRect.Right - countSize.X - 6, rowRect.Y), Color.Gold, 0.82f);
                    countOffset = countSize.X + 12f;
                }

                DrawShadowedText(spriteBatch, font, costText, new Vector2(rowRect.Right - costSize.X - 6 - countOffset, rowRect.Y), Color.LightGray, 0.82f);
            }

            string summary = _activeTab switch
            {
                MonsterCarnivalTab.Mob => BuildMobPlacementSummary(),
                MonsterCarnivalTab.Guardian => BuildGuardianPlacementSummary(),
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(summary))
            {
                DrawShadowedText(spriteBatch, font, summary, new Vector2(x + 8, y + height - 18), Color.LightSteelBlue, 0.75f);
            }
        }

        private void DrawFooter(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, int height)
        {
            if (_hudAssets.ListBottom == null)
            {
                spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, height), new Color(26, 34, 44, 220));
            }
            string status = _statusMessage;
            if (string.IsNullOrWhiteSpace(status))
            {
                MonsterCarnivalEntry selected = GetEntry(_activeTab, _selectedEntryIndex);
                status = selected?.Description;
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                status = BuildIdleStatusMessage();
            }

            DrawShadowedText(spriteBatch, font, status, new Vector2(x + 8, y + 8), Color.Gainsboro, 0.8f);
        }

        private bool TryValidateClientOwnedPacket(MonsterCarnivalPacketType packetType, out string errorMessage)
        {
            if (_definition?.IsReviveMode == true
                && !IsRecoveredClientOwnedPacket(packetType))
            {
                errorMessage = $"{_definition.ClientOwnerLabel} only owns the recovered 346-353 family in the client. This wrapper only exposes enter, cp, request result/failure, death, and result; packet {(int)packetType} stays on the broader Carnival seam.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private bool TryValidateClientOwnedRawPacket(int packetType, out string errorMessage)
        {
            if (_definition?.IsReviveMode == true
                && !IsRecoveredClientOwnedRawPacket(packetType))
            {
                errorMessage = $"{_definition.ClientOwnerLabel} only owns raw packet types 346-353 in the client; packet {packetType} stays on the broader Carnival seam.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        private string BuildIdleStatusMessage()
        {
            if (_definition == null)
            {
                return "Use /mcarnival enter ... to populate the Carnival HUD like CField_MonsterCarnival::OnEnter.";
            }

            if (_definition.IsReviveMode)
            {
                return $"{_definition.ClientOwnerLabel} owns the recovered 346-353 packet family; use /mcarnival raw or the explicit enter/cp/requestfail/death/memberout/result seams to drive it.";
            }

            if (_definition.IsWaitingRoom || _definition.IsSeason2Mode)
            {
                return $"{_definition.ClientOwnerLabel} is anchored by monsterCarnival/mapType={_definition.MapType} for this map; use /mcarnival enter ... to populate the shared Carnival HUD and /mcarnival result ... to surface the delegated CField_MonsterCarnival::OnShowGameResult chat-log route.";
            }

            return _enteredField
                ? "Use /mcarnival request, requestok, cpdelta, or death to drive the Monster Carnival runtime."
                : "Use /mcarnival enter ... to populate the Carnival HUD like CField_MonsterCarnival::OnEnter.";
        }

        private string GetPanelTitle()
        {
            if (_definition == null)
            {
                return "Monster Carnival";
            }

            return _definition.ResolvedFieldType switch
            {
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => "Monster Carnival Waiting Room",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => "Monster Carnival Season 2",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => "Monster Carnival Revive",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_NOT_USE => "Monster Carnival Legacy",
                _ => "Monster Carnival"
            };
        }

        private string BuildVariantContractSummary()
        {
            if (_definition == null)
            {
                return string.Empty;
            }

            return _definition.ResolvedFieldType switch
            {
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => "CField_MonsterCarnivalWaitingRoom::Init calls CField::Init, reads monsterCarnival/mapType, and keeps the waiting-room wrapper explicit while shared Carnival result packets still delegate through CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12, item=-1).",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => "CField_MonsterCarnivalS2_Game::Init calls CField_MonsterCarnival::Init, reads monsterCarnival/mapType, and keeps the Season 2 wrapper explicit while shared Carnival result packets still delegate through CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12, item=-1).",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => "CField_MonsterCarnivalRevive owns OnShowGameResult codes 8-11 and routes them through the recovered 0x1020-0x1023 StringPool seam into CUIStatusBar::ChatLogAdd(type=12, item=-1).",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_NOT_USE => "Legacy Carnival wrapper stays on the shared Monster Carnival packet family in this simulator seam.",
                _ => "CField_MonsterCarnival owns the shared packet family while the simulator keeps map-backed summon, CP, and request seams live."
            };
        }

        private string BuildClientOwnerHeaderSummary()
        {
            if (_definition == null)
            {
                return string.Empty;
            }

            string mapLabel = FormatMapIdentity(_definition);
            return _definition.ResolvedFieldType switch
            {
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => $"{mapLabel} | phase={DescribeVariantSessionPhase()} | Init base={_definition.InitBaseOwnerLabel} | reads monsterCarnival/mapType={_definition.MapType} | delegated result ids 0x1020-0x1023 -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => $"{mapLabel} | phase={DescribeVariantSessionPhase()} | Init base={_definition.InitBaseOwnerLabel} | reads monsterCarnival/mapType={_definition.MapType} | delegated result ids 0x1020-0x1023 -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => $"{mapLabel} | phase={DescribeVariantSessionPhase()} | revive owner packets 346-353 | failure ids 0x101B-0x101F | result ids 0x1020-0x1023 -> CUIStatusBar::ChatLogAdd(type=12,item=-1) | UI 0x102B-0x1033",
                _ => $"{mapLabel} | shared Carnival packet family 346-353 | UI 0x102B-0x1033"
            };
        }

        private void LoadHudAssets()
        {
            _hudAssets = new MonsterCarnivalHudAssets();
            if (_graphicsDevice == null)
            {
                return;
            }

            WzImage uiWindowImage = FindImageSafe("UI", "UIWindow.img");
            WzSubProperty carnivalProperty = uiWindowImage?["MonsterCarnival"] as WzSubProperty;
            if (carnivalProperty == null)
            {
                return;
            }

            WzSubProperty listProperty = carnivalProperty["backgrnd3"] as WzSubProperty;
            WzSubProperty sideButtonProperty = carnivalProperty["BtSide"] as WzSubProperty;
            WzSubProperty tabProperty = carnivalProperty["Tab"] as WzSubProperty;
            WzSubProperty tabEnabledProperty = tabProperty?["enabled"] as WzSubProperty;
            WzSubProperty tabDisabledProperty = tabProperty?["disabled"] as WzSubProperty;

            _hudAssets = new MonsterCarnivalHudAssets
            {
                Background = LoadCanvasTexture(carnivalProperty["backgrnd"] as WzCanvasProperty),
                TeamPanelBackground = LoadCanvasTexture(carnivalProperty["backgrnd2"] as WzCanvasProperty),
                ListTop = LoadCanvasTexture(listProperty?["top"]?["0"] as WzCanvasProperty),
                ListMiddle = LoadCanvasTexture(listProperty?["middle0"]?["0"] as WzCanvasProperty),
                ListSummary = LoadCanvasTexture(listProperty?["middle1"]?["0"] as WzCanvasProperty),
                ListBottom = LoadCanvasTexture(listProperty?["bottom"]?["0"] as WzCanvasProperty),
                SideButtonNormal = LoadCanvasTexture(sideButtonProperty?["normal"]?["0"] as WzCanvasProperty),
                SideButtonDisabled = LoadCanvasTexture(sideButtonProperty?["disabled"]?["0"] as WzCanvasProperty),
                TabMobEnabled = LoadCanvasTexture(tabEnabledProperty?["0"] as WzCanvasProperty),
                TabSkillEnabled = LoadCanvasTexture(tabEnabledProperty?["1"] as WzCanvasProperty),
                TabGuardianEnabled = LoadCanvasTexture(tabEnabledProperty?["2"] as WzCanvasProperty),
                TabMobDisabled = LoadCanvasTexture(tabDisabledProperty?["0"] as WzCanvasProperty),
                TabSkillDisabled = LoadCanvasTexture(tabDisabledProperty?["1"] as WzCanvasProperty),
                TabGuardianDisabled = LoadCanvasTexture(tabDisabledProperty?["2"] as WzCanvasProperty)
            };
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvasProperty)
        {
            System.Drawing.Bitmap bitmap = canvasProperty?.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return null;
            }

            using (bitmap)
            {
                return bitmap.ToTexture2D(_graphicsDevice);
            }
        }

        private static WzImage FindImageSafe(string category, string imageName)
        {
            try
            {
                return global::HaCreator.Program.FindImage(category, imageName);
            }
            catch
            {
                return null;
            }
        }

        private MonsterCarnivalHudMetrics ResolveHudMetrics()
        {
            return new MonsterCarnivalHudMetrics(
                BackgroundWidth: _hudAssets.Background?.Width ?? DefaultHudMetrics.BackgroundWidth,
                BackgroundHeight: _hudAssets.Background?.Height ?? DefaultHudMetrics.BackgroundHeight,
                TeamPanelWidth: _hudAssets.TeamPanelBackground?.Width ?? DefaultHudMetrics.TeamPanelWidth,
                TeamPanelHeight: _hudAssets.TeamPanelBackground?.Height ?? DefaultHudMetrics.TeamPanelHeight,
                ListTopHeight: _hudAssets.ListTop?.Height ?? DefaultHudMetrics.ListTopHeight,
                ListMiddleRowHeight: _hudAssets.ListMiddle?.Height ?? DefaultHudMetrics.ListMiddleRowHeight,
                ListSummaryHeight: _hudAssets.ListSummary?.Height ?? DefaultHudMetrics.ListSummaryHeight,
                ListBottomHeight: _hudAssets.ListBottom?.Height ?? DefaultHudMetrics.ListBottomHeight,
                TabMobWidth: _hudAssets.TabMobEnabled?.Width ?? DefaultHudMetrics.TabMobWidth,
                TabSkillWidth: _hudAssets.TabSkillEnabled?.Width ?? DefaultHudMetrics.TabSkillWidth,
                TabGuardianWidth: _hudAssets.TabGuardianEnabled?.Width ?? DefaultHudMetrics.TabGuardianWidth,
                SideButtonWidth: _hudAssets.SideButtonNormal?.Width ?? DefaultHudMetrics.SideButtonWidth,
                SideButtonHeight: _hudAssets.SideButtonNormal?.Height ?? DefaultHudMetrics.SideButtonHeight);
        }

        private static Rectangle ResolveHeaderBounds(int viewportWidth, MonsterCarnivalHudMetrics metrics)
        {
            int x = viewportWidth - metrics.TotalHeaderWidth - 18;
            return new Rectangle(x, 18, metrics.TotalHeaderWidth, metrics.BackgroundHeight);
        }

        private static Rectangle ResolveFooterBounds(Rectangle listBounds, MonsterCarnivalHudMetrics metrics)
        {
            return new Rectangle(
                listBounds.X,
                listBounds.Bottom - metrics.ListBottomHeight,
                listBounds.Width,
                metrics.ListBottomHeight);
        }

        private static Rectangle ResolveRecoveredHudHeaderBounds(int viewportWidth)
        {
            return ResolveHeaderBounds(viewportWidth, DefaultHudMetrics);
        }

        private static int[] ResolveRecoveredTabWidths()
        {
            return new[]
            {
                DefaultHudMetrics.TabMobWidth,
                DefaultHudMetrics.TabSkillWidth,
                DefaultHudMetrics.TabGuardianWidth
            };
        }

        private void DrawHeaderPanel(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle mainBounds, Rectangle teamBounds)
        {
            if (_hudAssets.Background != null)
            {
                spriteBatch.Draw(_hudAssets.Background, mainBounds, Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, mainBounds, new Color(16, 22, 28, 225));
                spriteBatch.Draw(pixelTexture, new Rectangle(mainBounds.X, mainBounds.Y, mainBounds.Width, 34), new Color(54, 76, 98, 255));
            }

            if (_hudAssets.TeamPanelBackground != null)
            {
                spriteBatch.Draw(_hudAssets.TeamPanelBackground, teamBounds, Color.White);
            }
            else
            {
                spriteBatch.Draw(pixelTexture, teamBounds, new Color(26, 34, 44, 220));
            }
        }

        private void DrawEntryListPanel(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle listBounds, MonsterCarnivalHudMetrics metrics)
        {
            if (!_hudAssets.HasAnyTexture)
            {
                spriteBatch.Draw(pixelTexture, listBounds, new Color(16, 22, 28, 225));
                return;
            }

            int y = listBounds.Y;
            DrawPanelSlice(spriteBatch, pixelTexture, _hudAssets.ListTop, new Rectangle(listBounds.X, y, listBounds.Width, metrics.ListTopHeight), new Color(31, 42, 56, 255));
            y += metrics.ListTopHeight;

            for (int row = 0; row < 6; row++)
            {
                DrawPanelSlice(spriteBatch, pixelTexture, _hudAssets.ListMiddle, new Rectangle(listBounds.X, y, listBounds.Width, metrics.ListMiddleRowHeight), new Color(24, 31, 40, 220));
                y += metrics.ListMiddleRowHeight;
            }

            DrawPanelSlice(spriteBatch, pixelTexture, _hudAssets.ListSummary, new Rectangle(listBounds.X, y, listBounds.Width, metrics.ListSummaryHeight), new Color(24, 31, 40, 220));
            y += metrics.ListSummaryHeight;
            DrawPanelSlice(spriteBatch, pixelTexture, _hudAssets.ListBottom, new Rectangle(listBounds.X, y, listBounds.Width, metrics.ListBottomHeight), new Color(26, 34, 44, 220));
        }

        private static void DrawPanelSlice(SpriteBatch spriteBatch, Texture2D pixelTexture, Texture2D texture, Rectangle destination, Color fallbackColor)
        {
            if (texture != null)
            {
                spriteBatch.Draw(texture, destination, Color.White);
                return;
            }

            spriteBatch.Draw(pixelTexture, destination, fallbackColor);
        }

        private void DrawFooterPanel(SpriteBatch spriteBatch, Texture2D pixelTexture, Rectangle footerBounds)
        {
            if (_hudAssets.ListBottom != null)
            {
                spriteBatch.Draw(_hudAssets.ListBottom, footerBounds, Color.White);
                return;
            }

            spriteBatch.Draw(pixelTexture, footerBounds, new Color(26, 34, 44, 220));
        }

        private Texture2D GetTabTexture(MonsterCarnivalTab tab, bool enabled)
        {
            return (tab, enabled) switch
            {
                (MonsterCarnivalTab.Mob, true) => _hudAssets.TabMobEnabled,
                (MonsterCarnivalTab.Skill, true) => _hudAssets.TabSkillEnabled,
                (MonsterCarnivalTab.Guardian, true) => _hudAssets.TabGuardianEnabled,
                (MonsterCarnivalTab.Mob, false) => _hudAssets.TabMobDisabled,
                (MonsterCarnivalTab.Skill, false) => _hudAssets.TabSkillDisabled,
                (MonsterCarnivalTab.Guardian, false) => _hudAssets.TabGuardianDisabled,
                _ => null
            };
        }

        private string BuildClientOwnerStatusSummary()
        {
            if (_definition == null)
            {
                return "none";
            }

            return _definition.ResolvedFieldType switch
            {
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => $"{_definition.ClientOwnerLabel} Init base={_definition.InitBaseOwnerLabel}->monsterCarnival/mapType={_definition.MapType} | phase={DescribeVariantSessionPhase()} | delegated result StringPool=0x1020/0x1021/0x1022/0x1023 -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1) | trail={BuildVariantActionTrailSummary()} | last={BuildClientOwnerActionSummary()} | map={FormatMapIdentity(_definition)}",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => $"{_definition.ClientOwnerLabel} Init base={_definition.InitBaseOwnerLabel}->monsterCarnival/mapType={_definition.MapType} | phase={DescribeVariantSessionPhase()} | delegated result StringPool=0x1020/0x1021/0x1022/0x1023 -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1) | ui={_uiWindowState.DescribeStatus()} | trail={BuildVariantActionTrailSummary()} | last={BuildClientOwnerActionSummary()} | map={FormatMapIdentity(_definition)}",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => $"{_definition.ClientOwnerLabel} packets=346-353 | phase={DescribeVariantSessionPhase()} | ui={_uiWindowState.DescribeStatus()} | failure StringPool=0x101B/0x101C/0x101D/0x101E/0x101F | result StringPool=0x1020/0x1021/0x1022/0x1023 -> CUIStatusBar::ChatLogAdd(type=12,item=-1) | trail={BuildVariantActionTrailSummary()} | last={BuildClientOwnerActionSummary()} | map={FormatMapIdentity(_definition)}",
                _ => $"{_definition.ClientOwnerLabel} packets=346-353 | map={FormatMapIdentity(_definition)}"
            };
        }

        private MonsterCarnivalEntry GetEntry(MonsterCarnivalTab tab, int index)
        {
            IReadOnlyList<MonsterCarnivalEntry> entries = _definition?.GetEntries(tab);
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return null;
            }

            return entries[index];
        }

        private MonsterCarnivalTeamState GetTeamState(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0 ? _team0 : _team1;
        }

        private static MonsterCarnivalTeam GetOpposingTeam(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0 ? MonsterCarnivalTeam.Team1 : MonsterCarnivalTeam.Team0;
        }

        private static bool TryParseTab(string text, out MonsterCarnivalTab tab)
        {
            tab = MonsterCarnivalTab.Mob;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            switch (text.Trim().ToLowerInvariant())
            {
                case "mob":
                case "mobs":
                    tab = MonsterCarnivalTab.Mob;
                    return true;
                case "skill":
                case "skills":
                    tab = MonsterCarnivalTab.Skill;
                    return true;
                case "guardian":
                case "guardians":
                    tab = MonsterCarnivalTab.Guardian;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseTab(byte rawValue, out MonsterCarnivalTab tab)
        {
            tab = rawValue switch
            {
                0 => MonsterCarnivalTab.Mob,
                1 => MonsterCarnivalTab.Skill,
                2 => MonsterCarnivalTab.Guardian,
                _ => MonsterCarnivalTab.Mob
            };

            return rawValue <= 2;
        }

        private bool TryValidateRequest(MonsterCarnivalEntry entry, out int reasonCode)
        {
            reasonCode = 0;
            if (!_enteredField || entry == null)
            {
                reasonCode = 2;
                return false;
            }

            if (_personalCp < entry.Cost)
            {
                reasonCode = 1;
                return false;
            }

            if (entry.Tab == MonsterCarnivalTab.Mob
                && _definition?.MobGenMax > 0
                && GetTotalCount(_mobSpellCounts) >= _definition.MobGenMax)
            {
                reasonCode = 3;
                return false;
            }

            if (entry.Tab == MonsterCarnivalTab.Guardian
                && _definition?.GuardianGenMax > 0
                && GetTotalCount(_guardianCounts) >= _definition.GuardianGenMax)
            {
                reasonCode = 5;
                return false;
            }

            if (entry.Tab == MonsterCarnivalTab.Guardian
                && _occupiedGuardianSlots.Contains(entry.Index))
            {
                reasonCode = 4;
                return false;
            }

            return true;
        }

        private bool IsLocalRequestOwner(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(_localCharacterName)
                && string.Equals(characterName.Trim(), _localCharacterName, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySuccessfulRequest(MonsterCarnivalEntry entry, MonsterCarnivalTeam ownerTeam, bool spendLocalCp, bool ownerTeamKnown)
        {
            _activeTab = entry.Tab;
            _selectedEntryIndex = entry.Index;
            _lastRequestTab = (int)entry.Tab;
            _lastRequestIndex = entry.Index;

            if (spendLocalCp)
            {
                _personalCp = Math.Max(0, _personalCp - entry.Cost);
                MonsterCarnivalTeamState teamState = GetTeamState(_localTeam);
                teamState.CurrentCp = Math.Max(0, teamState.CurrentCp - entry.Cost);
            }
            else if (ownerTeamKnown)
            {
                MonsterCarnivalTeamState teamState = GetTeamState(ownerTeam);
                teamState.CurrentCp = Math.Max(0, teamState.CurrentCp - entry.Cost);
            }

            Dictionary<int, int> counts = GetCountDictionary(entry.Tab);
            int nextCount = GetEntryCount(counts, entry.Id) + 1;
            SetEntryCount(counts, entry.Id, nextCount);
            if (entry.Tab == MonsterCarnivalTab.Mob)
            {
                ReconcileSummonedMobStates(entry, nextCount);
            }
            else if (entry.Tab == MonsterCarnivalTab.Guardian)
            {
                _occupiedGuardianSlots.Add(entry.Index);
                MonsterCarnivalGuardianPlacement placement = new MonsterCarnivalGuardianPlacement(
                    entry,
                    ResolveGuardianSpawnPoint(entry.Index),
                    ResolveGuardianReactorId(ownerTeam),
                    ownerTeam);
                placement.ReactorRequiredHits = ResolveGuardianRequiredHits(ownerTeam);
                _guardianPlacements[entry.Index] = placement;
            }
        }

        private MonsterCarnivalSpawnPoint ResolveNextMobSpawnPoint()
        {
            IReadOnlyList<MonsterCarnivalSpawnPoint> positions = _definition?.MobSpawnPositions;
            if (positions == null || positions.Count == 0)
            {
                return default;
            }

            int normalizedIndex = Math.Abs(_nextMobSpawnPointIndex) % positions.Count;
            _nextMobSpawnPointIndex = normalizedIndex + 1;
            return positions[normalizedIndex];
        }

        private MonsterCarnivalGuardianSpawnPoint ResolveGuardianSpawnPoint(int slotIndex)
        {
            IReadOnlyList<MonsterCarnivalGuardianSpawnPoint> positions = _definition?.GuardianSpawnPositions;
            if (positions == null || positions.Count == 0)
            {
                return default;
            }

            foreach (MonsterCarnivalGuardianSpawnPoint position in positions)
            {
                if (position.Index == slotIndex)
                {
                    return position;
                }
            }

            return positions[Math.Clamp(slotIndex, 0, positions.Count - 1)];
        }

        private int ResolveGuardianReactorId(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0
                ? Math.Max(0, _definition?.ReactorRed ?? 0)
                : Math.Max(0, _definition?.ReactorBlue ?? 0);
        }

        private int ResolveGuardianRequiredHits(MonsterCarnivalTeam team)
        {
            if (team == MonsterCarnivalTeam.Team0)
            {
                return Math.Max(1, _definition?.ReactorRedRequiredHits ?? 1);
            }

            return Math.Max(1, _definition?.ReactorBlueRequiredHits ?? 1);
        }

        private void RegisterKnownCharacterTeam(string characterName, MonsterCarnivalTeam team)
        {
            string normalizedName = NormalizeCharacterName(characterName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return;
            }

            _knownCharacterTeams[normalizedName] = team;
        }

        private bool TryResolveKnownCharacterTeam(string characterName, out MonsterCarnivalTeam team)
        {
            string normalizedName = NormalizeCharacterName(characterName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                team = _localTeam;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(_localCharacterName)
                && string.Equals(normalizedName, _localCharacterName, StringComparison.OrdinalIgnoreCase))
            {
                team = _localTeam;
                return true;
            }

            return _knownCharacterTeams.TryGetValue(normalizedName, out team);
        }

        private static string NormalizeCharacterName(string characterName)
        {
            return string.IsNullOrWhiteSpace(characterName)
                ? null
                : characterName.Trim();
        }

        private int GetEntryUsageCount(MonsterCarnivalEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            return GetEntryCount(GetCountDictionary(entry.Tab), entry.Id);
        }

        private void ReconcileSummonedMobStates(MonsterCarnivalEntry entry, int desiredCount)
        {
            if (entry == null || entry.Tab != MonsterCarnivalTab.Mob)
            {
                return;
            }

            int normalizedCount = Math.Max(0, desiredCount);
            List<MonsterCarnivalSummonedMobState> matchingStates = _summonedMobs
                .Where(mob => mob.Entry?.Id == entry.Id)
                .ToList();

            while (matchingStates.Count > normalizedCount)
            {
                MonsterCarnivalSummonedMobState removedState = matchingStates[^1];
                _summonedMobs.Remove(removedState);
                matchingStates.RemoveAt(matchingStates.Count - 1);
            }

            while (matchingStates.Count < normalizedCount)
            {
                MonsterCarnivalSummonedMobState newState = new(entry, ResolveNextMobSpawnPoint());
                _summonedMobs.Add(newState);
                matchingStates.Add(newState);
            }
        }

        private IReadOnlyList<MonsterCarnivalSummonedMobState> SpawnReviveChain(MonsterCarnivalSummonedMobState defeatedMob)
        {
            var revivedMobs = new List<MonsterCarnivalSummonedMobState>();
            IReadOnlyList<int> reviveMobIds = defeatedMob?.Entry?.ReviveMobIds;
            if (reviveMobIds == null || reviveMobIds.Count == 0)
            {
                return revivedMobs;
            }

            foreach (int reviveMobId in reviveMobIds)
            {
                MonsterCarnivalEntry reviveEntry = FindMobEntryById(reviveMobId) ?? MonsterCarnivalFieldDataLoader.CreateResolvedMobEntry(reviveMobId);
                if (reviveEntry == null)
                {
                    continue;
                }

                var revivedState = new MonsterCarnivalSummonedMobState(
                    reviveEntry,
                    new MonsterCarnivalSpawnPoint(
                        defeatedMob.SpawnPoint.Index,
                        defeatedMob.SpawnPoint.X,
                        defeatedMob.SpawnPoint.Y,
                        defeatedMob.SpawnPoint.Foothold,
                        defeatedMob.SpawnPoint.Cy));
                _summonedMobs.Add(revivedState);
                SetEntryCount(_mobSpellCounts, reviveEntry.Id, GetEntryCount(_mobSpellCounts, reviveEntry.Id) + 1);
                revivedMobs.Add(revivedState);
            }

            return revivedMobs;
        }

        private MonsterCarnivalEntry FindMobEntryById(int mobId)
        {
            return _definition?.MobEntries?.FirstOrDefault(entry => entry.Id == mobId);
        }

        private Dictionary<int, int> GetCountDictionary(MonsterCarnivalTab tab)
        {
            return tab switch
            {
                MonsterCarnivalTab.Mob => _mobSpellCounts,
                MonsterCarnivalTab.Skill => _skillUseCounts,
                MonsterCarnivalTab.Guardian => _guardianCounts,
                _ => _mobSpellCounts
            };
        }

        private static int GetEntryCount(Dictionary<int, int> counts, int id)
        {
            return counts != null && counts.TryGetValue(id, out int value)
                ? value
                : 0;
        }

        private static void SetEntryCount(Dictionary<int, int> counts, int id, int count)
        {
            if (counts == null)
            {
                return;
            }

            if (count <= 0)
            {
                counts.Remove(id);
                return;
            }

            counts[id] = count;
        }

        private static int GetTotalCount(Dictionary<int, int> counts)
        {
            return counts?.Values.Sum() ?? 0;
        }

        private static MonsterCarnivalStringPoolMessage? GetRequestSuccessMessage(MonsterCarnivalTab tab)
        {
            return tab switch
            {
                MonsterCarnivalTab.Mob => new MonsterCarnivalStringPoolMessage(0x1024, "{0} summoned {1}."),
                MonsterCarnivalTab.Skill => new MonsterCarnivalStringPoolMessage(0x1025, "{0} used {1}."),
                MonsterCarnivalTab.Guardian => new MonsterCarnivalStringPoolMessage(0x1026, "{0} summoned {1}."),
                _ => null
            };
        }

        private string BuildRequestSuccessMessage(MonsterCarnivalEntry entry, string characterName)
        {
            if (entry == null)
            {
                return "Monster Carnival request completed.";
            }

            MonsterCarnivalStringPoolMessage? definition = GetRequestSuccessMessage(entry.Tab);
            string normalizedName = string.IsNullOrWhiteSpace(characterName) ? null : characterName.Trim();
            if (definition.HasValue && !string.IsNullOrWhiteSpace(normalizedName))
            {
                string fallback = string.Format(
                    CultureInfo.InvariantCulture,
                    definition.Value.FallbackFormat,
                    normalizedName,
                    entry.Name);
                return FormatStringPoolMessage(definition.Value, fallback, normalizedName, entry.Name);
            }

            string fallbackMessage = entry.Tab switch
            {
                MonsterCarnivalTab.Mob => $"Summoned {entry.Name}.",
                MonsterCarnivalTab.Skill => $"Activated {entry.Name}.",
                MonsterCarnivalTab.Guardian => $"Placed {entry.Name}.",
                _ => $"Requested {entry.Name}."
            };

            return definition.HasValue
                ? $"{fallbackMessage} {BuildStringPoolSuffix(definition.Value.StringPoolId)}"
                : fallbackMessage;
        }

        private static string DescribeRequestFailure(int reasonCode)
        {
            MonsterCarnivalStringPoolMessage? definition = GetRequestFailureMessage(reasonCode);
            if (definition.HasValue)
            {
                return FormatStringPoolMessage(definition.Value);
            }

            return $"Monster Carnival request rejected (reason {reasonCode}).";
        }

        private string DescribeGameResult(int resultCode)
        {
            MonsterCarnivalStringPoolMessage? definition = GetGameResultMessage(resultCode);
            if (!definition.HasValue)
            {
                if (ShouldTrackVariantClientOwnerAction())
                {
                    RecordClientOwnerAction(
                        $"{_definition.ClientOwnerLabel}::OnShowGameResult ignored code {resultCode}.",
                        Array.Empty<int>());
                    return null;
                }

                return $"Monster Carnival result packet {resultCode} received.";
            }

            string message = FormatStringPoolMessage(definition.Value);
            if (_definition?.IsReviveMode == true)
            {
                RecordClientOwnerAction(
                    $"{_definition.ClientOwnerLabel}::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1) code={resultCode}",
                    new[] { definition.Value.StringPoolId });
                return $"{message} via {_definition.ClientOwnerLabel}::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)";
            }

            if (_definition?.IsWaitingRoom == true || _definition?.IsSeason2Mode == true)
            {
                RecordClientOwnerAction(
                    $"{_definition.ClientOwnerLabel} delegated OnShowGameResult code={resultCode} to CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)",
                    new[] { definition.Value.StringPoolId });
                return $"{message} via {_definition.ClientOwnerLabel} -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)";
            }

            return message;
        }

        private void RecordVariantWrapperPacketDelegation(int packetType, bool rawPacket)
        {
            if (_definition == null || (!_definition.IsWaitingRoom && !_definition.IsSeason2Mode))
            {
                return;
            }

            string packetLabel = rawPacket ? "raw packet" : "packet";
            RecordClientOwnerAction(
                $"{_definition.ClientOwnerLabel}::Init retained monsterCarnival/mapType={_definition.MapType}; {packetLabel} {packetType} delegated to CField_MonsterCarnival.",
                Array.Empty<int>());
        }

        private void MarkClientOwnedCpHudRefresh(string packetOwner, string uiOwner)
        {
            string ownerLabel = _definition?.ClientOwnerLabel ?? "CField_MonsterCarnival";
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.LiveHud,
                $"{ownerLabel}::{packetOwner} refreshed the shared Carnival HUD through {uiOwner}.");
            RecordRecoveredClientOwnerAction(
                $"{ownerLabel}::{packetOwner} -> {uiOwner}.",
                Array.Empty<int>());
        }

        private void InitializeClientOwnedUiWindowState(MonsterCarnivalFieldDefinition definition)
        {
            _uiWindowState.Reset();
            if (definition == null || definition.IsWaitingRoom || definition.IsDeprecatedMode)
            {
                return;
            }

            _uiWindowState.CreateFromDefinition(definition);
            RecordClientOwnerAction(
                "CField_MonsterCarnival::CreateUIWindow -> CUIMonsterCarnival::SetUIData(mob/skill/guardian) -> CUIMonsterCarnival::ResetUI.",
                Array.Empty<int>());
        }

        private void RecreateClientOwnedUiWindowStateForEnter()
        {
            if (_definition == null || _definition.IsWaitingRoom || _definition.IsDeprecatedMode)
            {
                return;
            }

            _uiWindowState.CreateFromDefinition(_definition);
            RecordClientOwnerAction(
                "CField_MonsterCarnival::OnEnter -> CField_MonsterCarnival::CreateUIWindow -> CUIMonsterCarnival::SetUIData(mob/skill/guardian) -> CUIMonsterCarnival::ResetUI.",
                Array.Empty<int>());
        }

        private void RefreshClientOwnedUiWindowCpState()
        {
            if (!_uiWindowState.IsCreated)
            {
                return;
            }

            MonsterCarnivalTeamState myTeam = GetTeamState(_localTeam);
            MonsterCarnivalTeamState enemyTeam = GetTeamState(GetOpposingTeam(_localTeam));
            _uiWindowState.ApplyEnter(
                _localTeam,
                _personalCp,
                _personalTotalCp,
                myTeam.CurrentCp,
                myTeam.TotalCp,
                enemyTeam.CurrentCp,
                enemyTeam.TotalCp);
        }

        private string BuildInitialClientOwnerAction(MonsterCarnivalFieldDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return definition.ResolvedFieldType switch
            {
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => $"{definition.ClientOwnerLabel}::Init called {definition.InitBaseOwnerLabel} and read monsterCarnival/mapType={definition.MapType}.",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => $"{definition.ClientOwnerLabel}::Init called {definition.InitBaseOwnerLabel} and read monsterCarnival/mapType={definition.MapType}.",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => $"{definition.ClientOwnerLabel} is waiting for OnShowGameResult codes 8-11 -> StringPool 0x1020-0x1023 -> CUIStatusBar::ChatLogAdd(type=12,item=-1).",
                _ => $"{definition.ClientOwnerLabel} owns the shared Monster Carnival packet family."
            };
        }

        private void RecordClientOwnerAction(string action, IReadOnlyList<int> stringPoolIds)
        {
            _lastClientOwnerAction = string.IsNullOrWhiteSpace(action) ? null : action.Trim();
            _lastClientOwnerStringPoolIds = stringPoolIds?
                .Where(id => id > 0)
                .Distinct()
                .ToArray() ?? Array.Empty<int>();

            if (ShouldTrackVariantClientOwnerAction() && !string.IsNullOrWhiteSpace(_lastClientOwnerAction))
            {
                _variantActionTrail.Enqueue(_lastClientOwnerAction);
                while (_variantActionTrail.Count > VariantActionTrailCapacity)
                {
                    _variantActionTrail.Dequeue();
                }
            }
        }

        private void RecordRecoveredClientOwnerAction(string action, IReadOnlyList<int> stringPoolIds)
        {
            if (!ShouldTrackVariantClientOwnerAction())
            {
                return;
            }

            RecordClientOwnerAction(action, stringPoolIds);
        }

        private bool ShouldTrackVariantClientOwnerAction()
        {
            return _definition?.IsWaitingRoom == true
                || _definition?.IsSeason2Mode == true
                || _definition?.IsReviveMode == true;
        }

        private void SetVariantSessionPhase(MonsterCarnivalVariantSessionPhase phase, string summary)
        {
            if (!ShouldTrackVariantClientOwnerAction())
            {
                _variantSessionPhase = MonsterCarnivalVariantSessionPhase.None;
                _variantSessionSummary = null;
                return;
            }

            _variantSessionPhase = phase;
            _variantSessionSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        }

        private string DescribeVariantSessionPhase()
        {
            if (!ShouldTrackVariantClientOwnerAction())
            {
                return "none";
            }

            string label = _variantSessionPhase switch
            {
                MonsterCarnivalVariantSessionPhase.Init => "init",
                MonsterCarnivalVariantSessionPhase.HudSync => "hud-sync",
                MonsterCarnivalVariantSessionPhase.LiveHud => "live-hud",
                MonsterCarnivalVariantSessionPhase.Request => "request",
                MonsterCarnivalVariantSessionPhase.MemberState => "member-state",
                MonsterCarnivalVariantSessionPhase.DeathState => "death-state",
                MonsterCarnivalVariantSessionPhase.ResultRoute => "result-route",
                MonsterCarnivalVariantSessionPhase.UiWindow => "ui-window",
                _ => "none"
            };

            return string.IsNullOrWhiteSpace(_variantSessionSummary)
                ? label
                : $"{label} ({_variantSessionSummary})";
        }

        private string BuildVariantActionTrailSummary()
        {
            if (_variantActionTrail.Count == 0)
            {
                return "none";
            }

            return string.Join(" => ", _variantActionTrail.Select(action => TrimVariantActionText(action, 54)));
        }

        private string BuildInitialVariantSessionSummary(MonsterCarnivalFieldDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return definition.ResolvedFieldType switch
            {
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => $"{definition.ClientOwnerLabel} initialized from {definition.InitBaseOwnerLabel} and remained anchored to monsterCarnival/mapType={definition.MapType}.",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => $"{definition.ClientOwnerLabel} initialized from {definition.InitBaseOwnerLabel} and remained anchored to monsterCarnival/mapType={definition.MapType}.",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => $"{definition.ClientOwnerLabel} stayed on its direct result-route seam for codes 8-11.",
                _ => null
            };
        }

        private string BuildVariantResultSummary(int resultCode)
        {
            if (_definition?.IsReviveMode == true)
            {
                return $"{_definition.ClientOwnerLabel} kept direct ownership of OnShowGameResult code {resultCode} -> CUIStatusBar::ChatLogAdd(type=12,item=-1).";
            }

            if (_definition?.IsWaitingRoom == true || _definition?.IsSeason2Mode == true)
            {
                return $"{_definition.ClientOwnerLabel} delegated OnShowGameResult code {resultCode} through CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1).";
            }

            return null;
        }

        private static string TrimVariantActionText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text[..Math.Max(1, maxLength - 3)] + "...";
        }

        private string BuildClientOwnerActionSummary()
        {
            if (string.IsNullOrWhiteSpace(_lastClientOwnerAction))
            {
                return "none";
            }

            if (_lastClientOwnerStringPoolIds.Length == 0)
            {
                return _lastClientOwnerAction;
            }

            return $"{_lastClientOwnerAction} {BuildStringPoolSuffix(_lastClientOwnerStringPoolIds[0], _lastClientOwnerStringPoolIds.Skip(1).ToArray())}";
        }

        private static string ReadPacketString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            if (length == 0)
            {
                return string.Empty;
            }

            byte[] data = reader.ReadBytes(length);
            if (data.Length != length)
            {
                throw new EndOfStreamException("Unexpected end of Monster Carnival string payload.");
            }

            return Encoding.Default.GetString(data);
        }

        private static void EnsurePacketConsumed(Stream stream, string packetLabel)
        {
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException($"Unexpected trailing bytes in Monster Carnival {packetLabel} payload.");
            }
        }

        private static string FormatSignedDelta(int value)
        {
            return value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
        }

        private string BuildEnterStatusMessage(MonsterCarnivalTeam localTeam)
        {
            MonsterCarnivalStringPoolMessage definition = new(0x1027, "Monster Carnival is now underway!!");
            string underwayMessage = FormatStringPoolMessage(definition);
            return $"{underwayMessage} Entered as {FormatTeam(localTeam)}.";
        }

        private string BuildProcessForDeathMessage(MonsterCarnivalTeam team, string characterName, int remainingRevives)
        {
            int deathCp = Math.Max(0, _definition?.DeathCp ?? 0);
            MonsterCarnivalStringPoolMessage definition = remainingRevives > 0
                ? new(0x1019, "[%s] has become unable to fight and [%s]team has lost %d CP.")
                : new(0x101A, "[%s] has become unable to fight but [%s] has no CP so [%s] team did not lose any CP");
            string normalizedName = string.IsNullOrWhiteSpace(characterName) ? "A party member" : characterName.Trim();
            string teamName = FormatTeam(team);
            string fallback = remainingRevives > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} of {1} was defeated. {2} CP lost.", normalizedName, teamName, deathCp)
                : string.Format(CultureInfo.InvariantCulture, "{0} of {1} was defeated but no CP was lost.", normalizedName, teamName);
            return remainingRevives > 0
                ? FormatStringPoolMessage(definition, fallback, normalizedName, ResolveTeamLabel(team), deathCp)
                : FormatStringPoolMessage(definition, fallback, normalizedName, ResolveTeamLabel(team), ResolveTeamLabel(team));
        }

        private string BuildMemberOutMessage(int messageType, MonsterCarnivalTeam team, string characterName)
        {
            bool changedTeams = messageType == 6;
            MonsterCarnivalStringPoolMessage definition = changedTeams
                ? new(0x102A, "Since the leader of the Team [%s] quit the Monster Carnival%2C [%s] has been appointed as the new leader of the team.")
                : new(0x1029, "[%s] of Team [%s] has quit the Monster Carnival.");
            string normalizedName = string.IsNullOrWhiteSpace(characterName) ? "A party member" : characterName.Trim();
            string fallback = changedTeams
                ? string.Format(CultureInfo.InvariantCulture, "{0} became the new leader of {1}.", normalizedName, FormatTeam(team))
                : string.Format(CultureInfo.InvariantCulture, "{0} left {1}.", normalizedName, FormatTeam(team));
            return changedTeams
                ? FormatStringPoolMessage(definition, fallback, ResolveTeamLabel(team), normalizedName)
                : FormatStringPoolMessage(definition, fallback, normalizedName, ResolveTeamLabel(team));
        }

        private static MonsterCarnivalStringPoolMessage GetTeamLabelMessage(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0
                ? new MonsterCarnivalStringPoolMessage(0x1017, "Red")
                : new MonsterCarnivalStringPoolMessage(0x1018, "Blue");
        }

        private static MonsterCarnivalStringPoolMessage? GetRequestFailureMessage(int reasonCode)
        {
            return reasonCode switch
            {
                1 => new MonsterCarnivalStringPoolMessage(0x101B, "You do not have enough CP."),
                2 => new MonsterCarnivalStringPoolMessage(0x101C, "You can no longer summon the Monster."),
                3 => new MonsterCarnivalStringPoolMessage(0x101D, "You can no longer summon the being."),
                4 => new MonsterCarnivalStringPoolMessage(0x101E, "This being is already summoned."),
                5 => new MonsterCarnivalStringPoolMessage(0x101F, "This request has failed due to an unknown error."),
                _ => null
            };
        }

        private static MonsterCarnivalStringPoolMessage? GetGameResultMessage(int resultCode)
        {
            return resultCode switch
            {
                8 => new MonsterCarnivalStringPoolMessage(0x1020, "Monster Carnival round ended in victory."),
                9 => new MonsterCarnivalStringPoolMessage(0x1021, "Monster Carnival round ended in defeat."),
                10 => new MonsterCarnivalStringPoolMessage(0x1022, "Monster Carnival round ended in a draw."),
                11 => new MonsterCarnivalStringPoolMessage(0x1023, "Monster Carnival round ended."),
                _ => null
            };
        }

        private static string ResolveTeamLabel(MonsterCarnivalTeam team)
        {
            MonsterCarnivalStringPoolMessage teamLabel = GetTeamLabelMessage(team);
            return MapleStoryStringPool.GetOrFallback(teamLabel.StringPoolId, teamLabel.FallbackFormat);
        }

        private static bool IsRecoveredClientOwnedPacket(MonsterCarnivalPacketType packetType)
        {
            return packetType switch
            {
                MonsterCarnivalPacketType.Enter => true,
                MonsterCarnivalPacketType.RequestResult => true,
                MonsterCarnivalPacketType.RequestFailure => true,
                MonsterCarnivalPacketType.GameResult => true,
                MonsterCarnivalPacketType.ProcessForDeath => true,
                MonsterCarnivalPacketType.CpUpdate => true,
                _ => false
            };
        }

        private static bool IsRecoveredClientOwnedRawPacket(int packetType)
        {
            return packetType >= (int)MonsterCarnivalRawPacketType.Enter
                && packetType <= (int)MonsterCarnivalRawPacketType.GameResult;
        }

        private static string FormatStringPoolMessage(MonsterCarnivalStringPoolMessage definition, string fallbackText = null, params object[] args)
        {
            string fallback = string.IsNullOrWhiteSpace(fallbackText)
                ? definition.FallbackFormat
                : fallbackText.Trim();
            string format = GetMonsterCarnivalCompositeFormat(definition.StringPoolId, fallback, args?.Length ?? 0);
            string text = args == null || args.Length == 0
                ? format
                : string.Format(CultureInfo.InvariantCulture, format, args);
            text = NormalizeClientOwnedStatusText(text);
            return $"{text} {BuildStringPoolSuffix(definition.StringPoolId)}";
        }

        private static string GetMonsterCarnivalCompositeFormat(int stringPoolId, string fallbackFormat, int maxPlaceholderCount)
        {
            if (!MapleStoryStringPool.TryGet(stringPoolId, out string format))
            {
                return fallbackFormat;
            }

            for (int tokenIndex = 0; tokenIndex < maxPlaceholderCount; tokenIndex++)
            {
                int markerIndex = format.IndexOf("%s", StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    markerIndex = format.IndexOf("%d", StringComparison.Ordinal);
                }

                if (markerIndex < 0)
                {
                    break;
                }

                string replacement = $"{{{tokenIndex}}}";
                format = format.Remove(markerIndex, 2).Insert(markerIndex, replacement);
            }

            return format;
        }

        private static string NormalizeClientOwnedStatusText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return WebUtility.UrlDecode(text)?.Trim() ?? text.Trim();
        }

        private static string BuildStringPoolSuffix(int primaryStringPoolId, params int[] relatedStringPoolIds)
        {
            IEnumerable<int> ids = new[] { primaryStringPoolId }
                .Concat(relatedStringPoolIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct();
            return $"[StringPool {string.Join(", ", ids.Select(id => $"0x{id:X}"))}]";
        }

        private static string FormatMapTypeSuffix(MonsterCarnivalFieldDefinition definition)
        {
            if (definition == null || definition.MapType < 0)
            {
                return string.Empty;
            }

            return $" | mapType {definition.MapType}";
        }

        private static string FormatMapIdentity(MonsterCarnivalFieldDefinition definition)
        {
            if (definition == null)
            {
                return "unknown map";
            }

            string mapName = string.IsNullOrWhiteSpace(definition.MapName) ? null : definition.MapName.Trim();
            string streetName = string.IsNullOrWhiteSpace(definition.StreetName) ? null : definition.StreetName.Trim();
            if (!string.IsNullOrWhiteSpace(streetName) && !string.IsNullOrWhiteSpace(mapName))
            {
                return $"{streetName} / {mapName}";
            }

            if (!string.IsNullOrWhiteSpace(mapName))
            {
                return mapName;
            }

            if (!string.IsNullOrWhiteSpace(streetName))
            {
                return streetName;
            }

            return definition.MapId > 0
                ? definition.MapId.ToString(CultureInfo.InvariantCulture)
                : "unknown map";
        }

        private string BuildMobPlacementSummary()
        {
            MonsterCarnivalSummonedMobState lastSummon = _summonedMobs.Count > 0 ? _summonedMobs[^1] : null;
            if (lastSummon?.Entry == null)
            {
                int spawnCount = _definition?.MobSpawnPositions.Count ?? 0;
                return spawnCount > 0
                    ? $"Spawn slots loaded: {spawnCount}."
                    : string.Empty;
            }

            return $"Last summon: {lastSummon.Entry.Name} at slot {lastSummon.SpawnPoint.Index} ({lastSummon.SpawnPoint.X}, {lastSummon.SpawnPoint.Y}).";
        }

        private string BuildGuardianPlacementSummary()
        {
            if (_guardianPlacements.Count == 0)
            {
                int slotCount = _definition?.GuardianSpawnPositions.Count ?? 0;
                return slotCount > 0
                    ? $"Guardian slots loaded: {slotCount}."
                    : string.Empty;
            }

            KeyValuePair<int, MonsterCarnivalGuardianPlacement> latestPlacement = _guardianPlacements.OrderBy(pair => pair.Key).Last();
            MonsterCarnivalGuardianPlacement placement = latestPlacement.Value;
            return $"Guardian slot {latestPlacement.Key + 1}: {placement.Entry.Name} at ({placement.SpawnPoint.X}, {placement.SpawnPoint.Y}) reactor {placement.ReactorId} for {FormatTeam(placement.Team)} hits {placement.ReactorHitCount}/{Math.Max(1, placement.ReactorRequiredHits)}.";
        }

        private void ShowStatus(string message, int tickCount)
        {
            _statusMessage = message;
            _statusMessageUntil = tickCount + StatusDurationMs;
        }

        internal static string FormatTeam(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0 ? "Red Team" : "Blue Team";
        }

        private static Color GetTeamColor(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0
                ? new Color(255, 153, 153)
                : new Color(128, 191, 255);
        }

        private static void DrawShadowedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color color, float scale = 1f)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            spriteBatch.DrawString(font, text, position + Vector2.One, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
