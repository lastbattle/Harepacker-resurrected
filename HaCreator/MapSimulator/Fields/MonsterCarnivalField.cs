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

    public enum MonsterCarnivalSeason2SubDialogPhase
    {
        None = 0,
        IdleHidden = 1,
        RequestAcceptedPending = 2,
        RequestRejected = 3,
        RequestRejectedLocked = 4,
        DeathLocked = 5,
        ResultClosed = 6,
        TimedHidden = 7
    }

    public enum MonsterCarnivalOwnedClockPhase
    {
        None = 0,
        ActiveRound = 1,
        ExtendedRound = 2,
        ResultMessage = 3,
        ExitGrace = 4,
        Closed = 5
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

    public readonly record struct MonsterCarnivalGuardianSpawnPoint(int Index, int X, int Y, int Facing, MonsterCarnivalTeam? Team = null);

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

    internal readonly record struct PendingGuardianPlacement(
        MonsterCarnivalEntry Entry,
        MonsterCarnivalTeam Team,
        bool CountAlreadyApplied);

    public sealed class MonsterCarnivalUiDataRowState
    {
        public MonsterCarnivalUiDataRowState(MonsterCarnivalTab tab, int index, int clientOrdinal, int id, int cost, string name, string description)
        {
            Tab = tab;
            Index = index;
            ClientOrdinal = Math.Max(1, clientOrdinal);
            Id = id;
            Cost = Math.Max(0, cost);
            Name = string.IsNullOrWhiteSpace(name) ? $"{tab} {id}" : name.Trim();
            Description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        }

        public MonsterCarnivalTab Tab { get; }
        public int Index { get; }
        public int ClientOrdinal { get; }
        public int Id { get; }
        public int Cost { get; }
        public string Name { get; }
        public string Description { get; }
        public int ActiveCount { get; private set; }
        public string SetUiDataCallSummary => $"CUIMonsterCarnival::SetUIData(tab={(int)Tab}, ordinal={ClientOrdinal}, name=\"{Name}\", spendCP={Cost}, desc=\"{Description}\")";

        public void SetActiveCount(int activeCount)
        {
            ActiveCount = Math.Max(0, activeCount);
        }
    }

    public sealed class MonsterCarnivalUiWindowState
    {
        private readonly List<MonsterCarnivalUiDataRowState> _mobDataRows = new();
        private readonly List<MonsterCarnivalUiDataRowState> _skillDataRows = new();
        private readonly List<MonsterCarnivalUiDataRowState> _guardianDataRows = new();

        public bool IsCreated { get; private set; }
        public bool UsesWrapperOnlySurface { get; private set; }
        public bool UsesWindow2Assets { get; private set; }
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
        public string SurfaceOwnerName { get; private set; }
        public string PrimaryAssetRoot { get; private set; }
        public string SecondaryAssetRoot { get; private set; }
        public string TertiaryAssetRoot { get; private set; }
        public string SurfaceSummary { get; private set; }
        public int ActiveSpelledMobRows { get; private set; }
        public int ActiveSpelledMobCount { get; private set; }
        public string ActiveSpelledMobPreview { get; private set; }
        public string LastHudSyncSummary { get; private set; }
        public string LastRequestUiSummary { get; private set; }
        public string SetUiDataShapeSummary { get; private set; }
        public string FirstMobSetUiDataCall { get; private set; }
        public string FirstSkillSetUiDataCall { get; private set; }
        public string FirstGuardianSetUiDataCall { get; private set; }

        public int TotalRows => MobRows + SkillRows + GuardianRows;
        public IReadOnlyList<MonsterCarnivalUiDataRowState> MobDataRows => _mobDataRows;
        public IReadOnlyList<MonsterCarnivalUiDataRowState> SkillDataRows => _skillDataRows;
        public IReadOnlyList<MonsterCarnivalUiDataRowState> GuardianDataRows => _guardianDataRows;

        public void Reset()
        {
            IsCreated = false;
            UsesWrapperOnlySurface = false;
            UsesWindow2Assets = false;
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
            SurfaceOwnerName = null;
            PrimaryAssetRoot = null;
            SecondaryAssetRoot = null;
            TertiaryAssetRoot = null;
            SurfaceSummary = null;
            ActiveSpelledMobRows = 0;
            ActiveSpelledMobCount = 0;
            ActiveSpelledMobPreview = null;
            LastHudSyncSummary = null;
            LastRequestUiSummary = null;
            SetUiDataShapeSummary = null;
            FirstMobSetUiDataCall = null;
            FirstSkillSetUiDataCall = null;
            FirstGuardianSetUiDataCall = null;
            _mobDataRows.Clear();
            _skillDataRows.Clear();
            _guardianDataRows.Clear();
        }

        public void CaptureWrapperOnlySurface(string ownerName, string summary)
        {
            IsCreated = false;
            UsesWrapperOnlySurface = true;
            UsesWindow2Assets = false;
            MobRows = 0;
            SkillRows = 0;
            GuardianRows = 0;
            ResetCount = 0;
            SurfaceOwnerName = string.IsNullOrWhiteSpace(ownerName) ? null : ownerName.Trim();
            PrimaryAssetRoot = null;
            SecondaryAssetRoot = null;
            TertiaryAssetRoot = null;
            SurfaceSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
            LastRequestCooldownResetTick = null;
            LastRequestOwnerName = null;
            LastRequestResetOutcome = null;
            LastRequestTab = -1;
            LastRequestIndex = -1;
            ActiveSpelledMobRows = 0;
            ActiveSpelledMobCount = 0;
            ActiveSpelledMobPreview = null;
            LastHudSyncSummary = null;
            LastRequestUiSummary = null;
            SetUiDataShapeSummary = null;
            FirstMobSetUiDataCall = null;
            FirstSkillSetUiDataCall = null;
            FirstGuardianSetUiDataCall = null;
            _mobDataRows.Clear();
            _skillDataRows.Clear();
            _guardianDataRows.Clear();
        }

        public void CreateFromDefinition(MonsterCarnivalFieldDefinition definition, bool preferWindow2Assets = false)
        {
            IsCreated = true;
            UsesWrapperOnlySurface = false;
            UsesWindow2Assets = preferWindow2Assets;
            MobRows = definition?.MobEntries.Count ?? 0;
            SkillRows = definition?.SkillEntries.Count ?? 0;
            GuardianRows = definition?.GuardianEntries.Count ?? 0;
            ResetCount++;
            SurfaceOwnerName = definition?.ClientOwnerLabel;
            if (preferWindow2Assets)
            {
                PrimaryAssetRoot = "UI/UIWindow2.img/MonsterCarnival/main";
                SecondaryAssetRoot = "UI/UIWindow2.img/MonsterCarnival/summonList";
                TertiaryAssetRoot = "UI/UIWindow2.img/MonsterCarnival/sub";
                SurfaceSummary = "Season 2 keeps a distinct UIWindow2-backed Carnival surface (main backgrnd 148x100/backgrnd2 136x76/backgrnd3 120x47, summonList 118x100/106x76/88x70, sub top/center/bottom + BtOK + 16x16 locks) instead of the shared UIWindow.img HUD.";
            }
            else
            {
                PrimaryAssetRoot = "UI/UIWindow.img/MonsterCarnival/backgrnd";
                SecondaryAssetRoot = "UI/UIWindow.img/MonsterCarnival/backgrnd2";
                TertiaryAssetRoot = "UI/UIWindow.img/MonsterCarnival/backgrnd3";
                SurfaceSummary = "Shared Monster Carnival HUD created from UIWindow.img background, team panel, and list layers.";
            }
            LastRequestCooldownResetTick = null;
            LastRequestOwnerName = null;
            LastRequestResetOutcome = null;
            LastRequestTab = -1;
            LastRequestIndex = -1;
            ApplyUiDataRows(definition);
            SyncSpelledMobCounts(null);
            LastHudSyncSummary = null;
            LastRequestUiSummary = null;
            CaptureSetUiDataShapeSummary();
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

        public void MarkHudSyncSummary(string summary)
        {
            LastHudSyncSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        }

        public void MarkRequestUiSummary(string summary)
        {
            LastRequestUiSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        }

        public string DescribeStatus()
        {
            if (UsesWrapperOnlySurface)
            {
                string ownerText = string.IsNullOrWhiteSpace(SurfaceOwnerName) ? "unknown" : SurfaceOwnerName;
                string summary = string.IsNullOrWhiteSpace(SurfaceSummary) ? "wrapper-only surface" : SurfaceSummary;
                return $"CUIMonsterCarnival: wrapper-only | owner={ownerText} | {summary}";
            }

            if (!IsCreated)
            {
                return "CUIMonsterCarnival: not created.";
            }

            string teamText = Team.HasValue ? MonsterCarnivalField.FormatTeam(Team.Value) : "unset";
            string requestText = LastRequestCooldownResetTick.HasValue
                ? $"requestReset={LastRequestResetOutcome ?? "unknown"}:{LastRequestTab}/{LastRequestIndex}@{LastRequestCooldownResetTick.Value}"
                : "requestReset=none";
            string spelledText = ActiveSpelledMobCount > 0
                ? $"spelledMobs={ActiveSpelledMobCount} across {ActiveSpelledMobRows} row(s) preview={ActiveSpelledMobPreview}"
                : "spelledMobs=none";
            string hudSyncText = string.IsNullOrWhiteSpace(LastHudSyncSummary)
                ? "hudSync=none"
                : $"hudSync={LastHudSyncSummary}";
            string requestUiText = string.IsNullOrWhiteSpace(LastRequestUiSummary)
                ? "requestUi=none"
                : $"requestUi={LastRequestUiSummary}";
            string assetText = UsesWindow2Assets
                ? $"assets=UIWindow2 ({PrimaryAssetRoot}, {SecondaryAssetRoot}, {TertiaryAssetRoot})"
                : $"assets=UIWindow ({PrimaryAssetRoot}, {SecondaryAssetRoot}, {TertiaryAssetRoot})";
            string setUiDataText = string.IsNullOrWhiteSpace(SetUiDataShapeSummary)
                ? "SetUIData=none"
                : $"SetUIData={SetUiDataShapeSummary}";
            return $"CUIMonsterCarnival: created | rows mob/skill/guardian={MobRows}/{SkillRows}/{GuardianRows} total={TotalRows} | ResetUI={ResetCount} | team={teamText} | personalCP={PersonalCp}/{PersonalTotalCp} | myTeamCP={MyTeamCp}/{MyTeamTotalCp} | enemyTeamCP={EnemyTeamCp}/{EnemyTeamTotalCp} | {assetText} | {setUiDataText} | {spelledText} | {hudSyncText} | {requestUiText} | {requestText}";
        }

        public void SyncSpelledMobCounts(IReadOnlyDictionary<int, int> mobCounts)
        {
            ActiveSpelledMobRows = 0;
            ActiveSpelledMobCount = 0;

            foreach (MonsterCarnivalUiDataRowState row in _mobDataRows)
            {
                int nextCount = mobCounts != null && mobCounts.TryGetValue(row.Id, out int count)
                    ? Math.Max(0, count)
                    : 0;
                row.SetActiveCount(nextCount);
                if (nextCount <= 0)
                {
                    continue;
                }

                ActiveSpelledMobRows++;
                ActiveSpelledMobCount += nextCount;
            }

            string preview = string.Join(", ",
                _mobDataRows
                    .Where(row => row.ActiveCount > 0)
                    .Take(3)
                    .Select(row => $"{row.Name} x{row.ActiveCount}"));
            ActiveSpelledMobPreview = string.IsNullOrWhiteSpace(preview) ? "none" : preview;
        }

        private void ApplyUiDataRows(MonsterCarnivalFieldDefinition definition)
        {
            PopulateRows(_mobDataRows, definition?.MobEntries, MonsterCarnivalTab.Mob);
            PopulateRows(_skillDataRows, definition?.SkillEntries, MonsterCarnivalTab.Skill);
            PopulateRows(_guardianDataRows, definition?.GuardianEntries, MonsterCarnivalTab.Guardian);
        }

        private static void PopulateRows(
            List<MonsterCarnivalUiDataRowState> destination,
            IReadOnlyList<MonsterCarnivalEntry> source,
            MonsterCarnivalTab tab)
        {
            destination.Clear();
            if (source == null)
            {
                return;
            }

            int clientOrdinal = 1;
            foreach (MonsterCarnivalEntry entry in source)
            {
                if (entry == null)
                {
                    continue;
                }

                destination.Add(new MonsterCarnivalUiDataRowState(
                    tab,
                    entry.Index,
                    clientOrdinal,
                    entry.Id,
                    entry.Cost,
                    entry.Name,
                    entry.Description));
                clientOrdinal++;
            }
        }

        private void CaptureSetUiDataShapeSummary()
        {
            FirstMobSetUiDataCall = _mobDataRows.FirstOrDefault()?.SetUiDataCallSummary;
            FirstSkillSetUiDataCall = _skillDataRows.FirstOrDefault()?.SetUiDataCallSummary;
            FirstGuardianSetUiDataCall = _guardianDataRows.FirstOrDefault()?.SetUiDataCallSummary;
            SetUiDataShapeSummary =
                $"tab0 mob rows={_mobDataRows.Count}, tab1 skill rows={_skillDataRows.Count}, tab2 guardian rows={_guardianDataRows.Count}, one-based ordinals, argument order=(tab,ordinal,name,spendCP,desc)";
        }
    }

    public sealed class MonsterCarnivalFieldDefinition
    {
        public int MapId { get; init; }
        public FieldType FieldType { get; init; }
        public int MapType { get; init; } = -1;
        public int TimerContractMapId { get; init; }
        public bool UsesDelegatedTimerContract { get; init; }
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

            int mapType = ReadInt(property["mapType"], -1);
            ResolveTimerContractProperty(
                mapInfo,
                property,
                mapType,
                out WzImageProperty timerContractProperty,
                out int timerContractMapId,
                out bool usesDelegatedTimerContract);

            return new MonsterCarnivalFieldDefinition
            {
                MapId = mapInfo?.id ?? 0,
                FieldType = mapInfo?.fieldType ?? FieldType.FIELDTYPE_DEFAULT,
                MapName = NormalizeMapLabel(mapInfo?.strMapName, mapInfo?.mapName),
                StreetName = NormalizeMapLabel(mapInfo?.strStreetName, mapInfo?.streetName),
                MapType = mapType,
                TimerContractMapId = timerContractMapId,
                UsesDelegatedTimerContract = usesDelegatedTimerContract,
                DefaultTimeSeconds = ReadInt(timerContractProperty["timeDefault"]),
                ExpandTimeSeconds = ReadInt(timerContractProperty["timeExpand"]),
                MessageTimeSeconds = ReadInt(timerContractProperty["timeMessage"]),
                FinishTimeSeconds = ReadInt(timerContractProperty["timeFinish"]),
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
                    ReadInt(child["f"]),
                    TryReadTeam(child["team"], out MonsterCarnivalTeam team) ? team : null));
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

                if (TryResolveGuardianEventTransitionCount(reactorImage, out int transitionCount))
                {
                    return transitionCount;
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

        private static bool TryResolveGuardianEventTransitionCount(WzImage reactorImage, out int transitionCount)
        {
            transitionCount = 0;
            if (reactorImage == null)
            {
                return false;
            }

            int state = 1;
            var visitedStates = new HashSet<int>();
            while (visitedStates.Add(state))
            {
                WzImageProperty stateProperty = reactorImage[state.ToString(CultureInfo.InvariantCulture)];
                if (!TryReadFirstEventTargetState(stateProperty, out int nextState))
                {
                    break;
                }

                transitionCount++;
                if (nextState == state)
                {
                    break;
                }

                state = nextState;
            }

            return transitionCount > 0;
        }

        private static bool TryReadFirstEventTargetState(WzImageProperty stateProperty, out int nextState)
        {
            nextState = 0;
            WzImageProperty eventProperty = stateProperty?["event"];
            WzImageProperty firstEventProperty = eventProperty?["0"];
            if (firstEventProperty == null)
            {
                return false;
            }

            nextState = ReadInt(firstEventProperty["state"], -1);
            return nextState >= 0;
        }

        private static void ResolveTimerContractProperty(
            MapInfo mapInfo,
            WzImageProperty mapMonsterCarnivalProperty,
            int mapType,
            out WzImageProperty timerContractProperty,
            out int timerContractMapId,
            out bool usesDelegatedTimerContract)
        {
            timerContractProperty = mapMonsterCarnivalProperty;
            timerContractMapId = mapInfo?.id ?? 0;
            usesDelegatedTimerContract = false;

            if (HasAnyTimerContractValue(mapMonsterCarnivalProperty))
            {
                return;
            }

            if (!TryResolveSiblingTimerContractProperty(
                    mapInfo,
                    mapType,
                    out WzImageProperty siblingTimerContractProperty,
                    out int siblingMapId))
            {
                return;
            }

            timerContractProperty = siblingTimerContractProperty;
            timerContractMapId = siblingMapId;
            usesDelegatedTimerContract = true;
        }

        private static bool TryResolveSiblingTimerContractProperty(
            MapInfo mapInfo,
            int mapType,
            out WzImageProperty timerContractProperty,
            out int timerContractMapId)
        {
            timerContractProperty = null;
            timerContractMapId = 0;

            int mapId = mapInfo?.id ?? 0;
            if (mapId <= 0 || mapType < 0)
            {
                return false;
            }

            int candidateMapId = mapId + 100;
            WzImage candidateImage = TryResolveMapImage(mapInfo, candidateMapId);
            WzImageProperty candidateProperty = candidateImage?["monsterCarnival"];
            if (!HasAnyTimerContractValue(candidateProperty))
            {
                return false;
            }

            timerContractProperty = candidateProperty;
            timerContractMapId = candidateMapId;
            return true;
        }

        private static WzImage TryResolveMapImage(MapInfo mapInfo, int mapId)
        {
            string imageName = mapId.ToString("D9", CultureInfo.InvariantCulture) + ".img";
            if (mapInfo?.Image?.Parent is WzDirectory parentDirectory)
            {
                if (parentDirectory[imageName] is WzImage siblingImage)
                {
                    return siblingImage;
                }
            }

            if (global::HaCreator.Program.WzManager == null)
            {
                return null;
            }

            string categoryName = mapId / 100000000 == 9
                ? "Map/Map9"
                : $"Map/Map{Math.Max(0, mapId / 100000000)}";
            return global::HaCreator.Program.FindImage(categoryName, imageName);
        }

        private static bool HasAnyTimerContractValue(WzImageProperty monsterCarnivalProperty)
        {
            if (monsterCarnivalProperty == null)
            {
                return false;
            }

            return ReadInt(monsterCarnivalProperty["timeDefault"], 0) > 0
                || ReadInt(monsterCarnivalProperty["timeExpand"], 0) > 0
                || ReadInt(monsterCarnivalProperty["timeMessage"], 0) > 0
                || ReadInt(monsterCarnivalProperty["timeFinish"], 0) > 0;
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

        private static bool TryReadTeam(WzImageProperty property, out MonsterCarnivalTeam team)
        {
            int rawTeam = ReadInt(property, -1);
            if (rawTeam is 0 or 1)
            {
                team = (MonsterCarnivalTeam)rawTeam;
                return true;
            }

            team = MonsterCarnivalTeam.Team0;
            return false;
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
            public int TotalHeight => BackgroundHeight + ResolveClientTabWindowHeight(6);
        }

        private sealed class MonsterCarnivalHudAssets
        {
            public Texture2D Background { get; init; }
            public Texture2D TeamPanelBackground { get; init; }
            public Texture2D ListTop { get; init; }
            public Texture2D ListMiddle { get; init; }
            public Texture2D ListSummary { get; init; }
            public Texture2D ListBottom { get; init; }
            public Texture2D SubTop { get; init; }
            public Texture2D SubCenter { get; init; }
            public Texture2D SubBottom { get; init; }
            public Texture2D SubOkButtonNormal { get; init; }
            public Texture2D SubOkButtonDisabled { get; init; }
            public Texture2D SubLockEnabled { get; init; }
            public Texture2D SubLockDisabled { get; init; }
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
                || ListBottom != null
                || SubTop != null
                || SubCenter != null
                || SubBottom != null;
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

        internal const int ClientListCanvasX = 0;
        internal const int ClientListCanvasY = 100;
        internal const int ClientListCanvasWidth = 148;
        internal const int ClientDecisionButtonX = 99;
        internal const int ClientDecisionButtonYOffset = 75;
        internal const int ClientTabControlX = 8;
        internal const int ClientTabControlY = 9;
        internal const int ClientCpTextX = 68;
        internal const int ClientPersonalCpTextY = 24;
        internal const int ClientRedTeamCpTextY = 42;
        internal const int ClientBlueTeamCpTextY = 55;
        internal const int ClientVisibleTabRowCap = 11;
        internal const int ClientTabWindowBaseHeight = 60;
        internal const int ClientTabWindowRowHeight = 14;

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
        private readonly Queue<PendingGuardianPlacement> _pendingGuardianPlacements = new();
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
        private int _pendingLocalRequestTab;
        private int _pendingLocalRequestIndex;
        private string _lastRequestFailureChatRoute;
        private string _lastMemberOutChatRoute;
        private string _lastResultChatRoute;
        private string _lastDeathChatRoute;
        private bool _isVisible;
        private bool _enteredField;
        private string _localCharacterName;
        private string _lastClientOwnerAction;
        private int[] _lastClientOwnerStringPoolIds = Array.Empty<int>();
        private readonly Queue<string> _variantActionTrail = new();
        private readonly MonsterCarnivalUiWindowState _uiWindowState = new();
        private MonsterCarnivalVariantSessionPhase _variantSessionPhase;
        private string _variantSessionSummary;
        private int _lastVariantDelegatedPacketType = -1;
        private bool _lastVariantDelegatedRawPacket;
        private string _lastVariantDelegatedOwner;
        private string _lastVariantDelegatedSummary;
        private bool _season2SubDialogVisible;
        private bool _season2SubDialogOkEnabled;
        private bool _season2SubDialogSelectionLocked;
        private string _season2SubDialogSummary;
        private MonsterCarnivalSeason2SubDialogPhase _season2SubDialogPhase;
        private int? _season2SubDialogDeadlineTick;
        private string _season2SubDialogTimerSummary;
        private MonsterCarnivalOwnedClockPhase _ownedClockPhase;
        private string _ownedClockSummary;
        private int? _roundDeadlineTick;
        private int? _extendDeadlineTick;
        private int? _resultMessageDeadlineTick;
        private int? _exitGraceDeadlineTick;
        private int _variantTransportPacketCount;
        private int _variantEnterPacketCount;
        private int _variantRequestPacketCount;
        private int _variantResultPacketCount;
        private int _variantDeathPacketCount;
        private int _variantLiveHudPacketCount;
        private int _variantMemberPacketCount;
        private int _reviveDirectPacketCount;
        private int _reviveForwardedPacketCount;
        private int _reviveRoundSequence;
        private int _reviveResultSequence;
        private string _variantTransportLastRoute;
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
        public bool Season2SubDialogVisible => _season2SubDialogVisible;
        public bool Season2SubDialogOkEnabled => _season2SubDialogOkEnabled;
        public bool Season2SubDialogSelectionLocked => _season2SubDialogSelectionLocked;
        public string Season2SubDialogSummary => _season2SubDialogSummary;
        public MonsterCarnivalSeason2SubDialogPhase Season2SubDialogPhase => _season2SubDialogPhase;
        public int ReviveDirectPacketCount => _reviveDirectPacketCount;
        public int ReviveForwardedPacketCount => _reviveForwardedPacketCount;
        public int ReviveRoundSequence => _reviveRoundSequence;
        public int ReviveResultSequence => _reviveResultSequence;
        public string VariantTransportSummary => BuildVariantTransportSummary();
        public string LastRequestFailureChatRoute => _lastRequestFailureChatRoute;
        public string LastMemberOutChatRoute => _lastMemberOutChatRoute;
        public string LastResultChatRoute => _lastResultChatRoute;
        public string LastDeathChatRoute => _lastDeathChatRoute;
        public MonsterCarnivalOwnedClockPhase OwnedClockPhase => _ownedClockPhase;
        public string OwnedClockSummary => _ownedClockSummary;

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
            LoadHudAssets();
        }

        public void SetLocalPlayerName(string characterName)
        {
            _localCharacterName = string.IsNullOrWhiteSpace(characterName)
                ? null
                : characterName.Trim();
            RegisterKnownCharacterTeam(_localCharacterName, _localTeam);
        }

        public void MarkPendingLocalRequest(MonsterCarnivalTab tab, int entryIndex)
        {
            _pendingLocalRequestTab = (int)tab;
            _pendingLocalRequestIndex = entryIndex;
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
            int enemyTeamTotalCp,
            int? tickCountOverride = null)
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
            int resolvedTick = tickCountOverride ?? Environment.TickCount;
            UpdateSeason2SubDialogOnEnter(resolvedTick);
            StartOwnedRoundClock(resolvedTick);
            ShowStatus(BuildEnterStatusMessage(localTeam), resolvedTick);
            if (_definition?.IsWaitingRoom == true)
            {
                SetVariantSessionPhase(
                    MonsterCarnivalVariantSessionPhase.MemberState,
                    $"{_definition.ClientOwnerLabel} delegated OnEnter to CField_MonsterCarnival while preserving the waiting-room wrapper-only surface.");
                RecordRecoveredClientOwnerAction(
                    $"{_definition.ClientOwnerLabel}::OnEnter delegated to CField_MonsterCarnival::OnEnter while the waiting-room wrapper kept CUIMonsterCarnival disabled.",
                    Array.Empty<int>());
                return;
            }

            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.HudSync,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} synchronized the shared Carnival HUD through OnEnter.");
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnEnter refreshed the Carnival HUD state.",
                new[] { 0x1027 });
        }

        private void OnReviveEnter(MonsterCarnivalTeam localTeam, int tickCount)
        {
            if (!_isVisible)
            {
                return;
            }

            ClearRoundState();
            RecreateClientOwnedUiWindowStateForEnter();
            _enteredField = true;
            _localTeam = localTeam;
            RegisterKnownCharacterTeam(_localCharacterName, _localTeam);
            _uiWindowState.ApplyEnter(localTeam, 0, 0, 0, 0, 0, 0);
            StartOwnedRoundClock(tickCount);
            ResetSeason2SubDialogState(
                "Season 2 sub dialog is not owned by the revive wrapper; revive OnEnter only updates team ownership.",
                MonsterCarnivalSeason2SubDialogPhase.None);

            string ownerLabel = _definition?.ClientOwnerLabel ?? "CField_MonsterCarnivalRevive";
            ShowStatus(
                $"{ownerLabel}::OnEnter decoded team byte {(int)localTeam} ({FormatTeam(localTeam)}) and refreshed the local guild name tag ownership seam.",
                tickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.MemberState,
                $"{ownerLabel}::OnEnter decoded the revive team byte and refreshed local guild-name ownership.");
            RecordRecoveredClientOwnerAction(
                $"{ownerLabel}::OnEnter decoded team byte {(int)localTeam}, updated CUserLocal team slot, and called CUserLocal::RedrawGuildNameTag without creating CUIMonsterCarnival.",
                Array.Empty<int>());
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
            if (_definition?.IsWaitingRoom == true)
            {
                SetVariantSessionPhase(
                    MonsterCarnivalVariantSessionPhase.LiveHud,
                    $"{_definition.ClientOwnerLabel} delegated CP synchronization to CField_MonsterCarnival without creating CUIMonsterCarnival.");
                RecordRecoveredClientOwnerAction(
                    $"{_definition.ClientOwnerLabel}::OnPersonalCP/OnTeamCP delegated CP totals while the waiting-room wrapper stayed HUD-less.",
                    Array.Empty<int>());
                return;
            }

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
            RefreshClientOwnedUiWindowSpellState();
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
            bool pendingLocalRequestMatch = IsPendingLocalRequest(tab, entryIndex);
            MonsterCarnivalEntry entry = GetEntry(tab, entryIndex);
            if (entry == null)
            {
                string fallbackMessage = string.IsNullOrWhiteSpace(characterName)
                    ? $"Monster Carnival request result received for tab {(int)tab}, index {entryIndex}."
                    : characterName.Trim();
                ShowStatus(fallbackMessage, tickCount);
                if (pendingLocalRequestMatch)
                {
                    _uiWindowState.MarkRequestCooldownReset(tickCount, _definition?.ClientOwnerLabel, "success", (int)tab, entryIndex);
                    ClearPendingLocalRequest();
                }
                return;
            }

            bool spendLocalCp = IsLocalRequestOwner(characterName) || pendingLocalRequestMatch;
            bool ownerTeamKnown = TryResolveKnownCharacterTeam(characterName, out MonsterCarnivalTeam ownerTeam);
            if (!ownerTeamKnown
                && !spendLocalCp
                && tab == MonsterCarnivalTab.Guardian
                && TryInferGuardianOwnerTeamFromSlotMetadata(entry, out MonsterCarnivalTeam inferredOwnerTeam))
            {
                ownerTeam = inferredOwnerTeam;
                ownerTeamKnown = true;
            }
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
            UpdateSeason2SubDialogOnRequest(entry, success: true, reasonCode: null, tickCount);
            if (pendingLocalRequestMatch)
            {
                ClearPendingLocalRequest();
            }
        }

        public void OnRequestFailure(int reasonCode, int tickCount)
        {
            ShowStatus(DescribeRequestFailure(reasonCode), tickCount);
            MonsterCarnivalStringPoolMessage? definition = GetRequestFailureMessage(reasonCode);
            int requestTab = _pendingLocalRequestTab >= 0 ? _pendingLocalRequestTab : _lastRequestTab;
            int requestIndex = _pendingLocalRequestIndex >= 0 ? _pendingLocalRequestIndex : _lastRequestIndex;
            _uiWindowState.MarkRequestCooldownReset(tickCount, _definition?.ClientOwnerLabel, "failure", requestTab, requestIndex);
            ClearPendingLocalRequest();
            if (definition.HasValue)
            {
                _lastRequestFailureChatRoute =
                    $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnRequestResult(reject) reason={reasonCode} -> CUIStatusBar::ChatLogAdd(type=7,item=-1)";
            }
            else
            {
                _lastRequestFailureChatRoute = null;
            }
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.Request,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} rejected request reason {reasonCode} on the shared Carnival seam.");
            RecordRecoveredClientOwnerAction(
                definition.HasValue
                    ? $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnRequestResult rejected reason {reasonCode}, routed the StringPool notice through CUIStatusBar::ChatLogAdd(type=7,item=-1), and reset the local request timer state."
                    : $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnRequestResult rejected reason {reasonCode} and reset the local request timer state.",
                definition.HasValue ? new[] { definition.Value.StringPoolId } : Array.Empty<int>());
            UpdateSeason2SubDialogOnRequest(entry: null, success: false, reasonCode: reasonCode, tickCount);
        }

        public void OnShowGameResult(int resultCode, int tickCount)
        {
            string status = DescribeGameResult(resultCode);
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            ShowStatus(status, tickCount);
            StartOwnedResultClock(tickCount);
            if (_definition?.IsSeason2Mode == true)
            {
                ResetSeason2SubDialogState(
                    "Season 2 sub dialog closed after OnShowGameResult routed through the result owner seam.",
                    MonsterCarnivalSeason2SubDialogPhase.ResultClosed);
                _season2SubDialogDeadlineTick = null;
                _season2SubDialogTimerSummary = "Season 2 sub dialog timer closed by OnShowGameResult.";
            }
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.ResultRoute,
                BuildVariantResultSummary(resultCode));
        }

        public void OnProcessForDeath(MonsterCarnivalTeam team, string characterName, int lostCp, int tickCount)
        {
            if (!_isVisible)
            {
                return;
            }

            RegisterKnownCharacterTeam(characterName, team);
            string deathMessage = BuildProcessForDeathMessage(team, characterName, lostCp);

            ShowStatus(deathMessage, tickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.DeathState,
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"} routed ProcessForDeath for {FormatTeam(team)} through the packet-owned status-bar message seam.");
            UpdateSeason2SubDialogOnProcessForDeath(team, characterName, lostCp, tickCount);
            _lastDeathChatRoute =
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnProcessForDeath -> CUIStatusBar::ChatLogAdd(type=7,item=-1)";
            RecordRecoveredClientOwnerAction(
                $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnProcessForDeath decoded packet CP loss {Math.Max(0, lostCp)} for {FormatTeam(team)} without mutating CP counters.",
                lostCp > 0
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
            RefreshClientOwnedUiWindowSpellState();
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

        public bool TryReconcileLiveGuardianPlacement(
            int reactorId,
            int worldX,
            int worldY,
            bool flip,
            int hitCount,
            int requiredHits,
            out int slotIndex,
            out string message)
        {
            slotIndex = -1;
            message = null;

            if (!_isVisible || _definition == null || reactorId <= 0)
            {
                return false;
            }

            if (!TryResolveCandidateGuardianTeamsFromReactorId(reactorId, out IReadOnlyList<MonsterCarnivalTeam> candidateTeams))
            {
                return false;
            }

            int resolvedRequiredHits = Math.Max(1, requiredHits);
            int resolvedHitCount = Math.Clamp(hitCount, 0, resolvedRequiredHits);

            foreach (KeyValuePair<int, MonsterCarnivalGuardianPlacement> trackedPlacement in _guardianPlacements)
            {
                MonsterCarnivalGuardianPlacement existingPlacement = trackedPlacement.Value;
                if (existingPlacement == null || existingPlacement.ReactorId != reactorId)
                {
                    continue;
                }

                int distance = Math.Abs(existingPlacement.SpawnPoint.X - worldX) + Math.Abs(existingPlacement.SpawnPoint.Y - worldY);
                if (distance > 96)
                {
                    continue;
                }

                existingPlacement.ReactorRequiredHits = resolvedRequiredHits;
                existingPlacement.ReactorHitCount = resolvedHitCount;
                slotIndex = trackedPlacement.Key;
                message = $"Reconciled live guardian reactor {reactorId} into tracked slot {slotIndex + 1} ({FormatTeam(existingPlacement.Team)}).";
                return true;
            }

            if (!TryResolveGuardianSlotFromLiveReactor(candidateTeams, worldX, worldY, flip, out int resolvedSlotIndex, out MonsterCarnivalGuardianSpawnPoint spawnPoint, out MonsterCarnivalTeam resolvedTeam))
            {
                return false;
            }

            bool countAlreadyApplied = false;
            MonsterCarnivalEntry entry;
            if (!TryDequeuePendingGuardianPlacement(resolvedTeam, out PendingGuardianPlacement pendingPlacement))
            {
                entry = ResolveGuardianEntryForSlot(resolvedSlotIndex);
            }
            else
            {
                entry = pendingPlacement.Entry;
                countAlreadyApplied = pendingPlacement.CountAlreadyApplied;
            }

            if (entry == null)
            {
                return false;
            }

            if (_guardianPlacements.TryGetValue(resolvedSlotIndex, out MonsterCarnivalGuardianPlacement displacedPlacement)
                && displacedPlacement?.Entry != null)
            {
                SetEntryCount(
                    _guardianCounts,
                    displacedPlacement.Entry.Id,
                    Math.Max(0, GetEntryCount(_guardianCounts, displacedPlacement.Entry.Id) - 1));
            }

            var placement = new MonsterCarnivalGuardianPlacement(entry, spawnPoint, reactorId, resolvedTeam)
            {
                ReactorRequiredHits = resolvedRequiredHits,
                ReactorHitCount = resolvedHitCount
            };

            _guardianPlacements[resolvedSlotIndex] = placement;
            _occupiedGuardianSlots.Add(resolvedSlotIndex);
            if (!countAlreadyApplied)
            {
                SetEntryCount(_guardianCounts, entry.Id, GetEntryCount(_guardianCounts, entry.Id) + 1);
            }

            slotIndex = resolvedSlotIndex;
            message =
                $"Reconciled live guardian reactor {reactorId} at ({worldX}, {worldY}) into slot {slotIndex + 1} " +
                $"for {FormatTeam(resolvedTeam)} with hit progress {resolvedHitCount}/{resolvedRequiredHits}.";
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
            string ownerLabel = _definition?.ClientOwnerLabel ?? "CField_MonsterCarnival";
            bool changedTeams = messageType == 6;
            int memberOutStringPoolId = changedTeams ? 0x102A : 0x1029;
            MonsterCarnivalStringPoolMessage teamLabel = GetTeamLabelMessage(team);
            _lastMemberOutChatRoute =
                $"{ownerLabel}::OnShowMemberOutMsg(type={messageType}) -> StringPool 0x{memberOutStringPoolId:X}/0x{teamLabel.StringPoolId:X} -> CUIStatusBar::ChatLogAdd(type=12,item=-1)";
            ShowStatus(BuildMemberOutMessage(messageType, team, characterName), tickCount);
            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.MemberState,
                $"{ownerLabel} updated member ownership for {FormatTeam(team)} and routed OnShowMemberOutMsg through CUIStatusBar::ChatLogAdd(type=12,item=-1).");
            RecordRecoveredClientOwnerAction(
                $"{ownerLabel}::OnShowMemberOutMsg(type={messageType}) formatted StringPool 0x{memberOutStringPoolId:X} with team label 0x{teamLabel.StringPoolId:X} and routed the message to CUIStatusBar::ChatLogAdd(type=12,item=-1).",
                new[] { memberOutStringPoolId, teamLabel.StringPoolId });
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
                        if (_definition?.IsReviveMode == true)
                        {
                            OnReviveEnter((MonsterCarnivalTeam)reader.ReadByte(), currentTimeMs);
                        }
                        else
                        {
                            OnEnter((MonsterCarnivalTeam)reader.ReadByte(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), currentTimeMs);

                            for (int i = 0; i < (_definition?.MobEntries.Count ?? 0); i++)
                            {
                                int count = reader.ReadByte();
                                MonsterCarnivalEntry entry = _definition.MobEntries[i];
                                SetEntryCount(_mobSpellCounts, entry.Id, count);
                                ReconcileSummonedMobStates(entry, count);
                            }

                            RefreshClientOwnedUiWindowSpellState();
                            if (_definition?.IsWaitingRoom == true)
                            {
                                RecordRecoveredClientOwnerAction(
                                    $"{_definition.ClientOwnerLabel}::OnEnter delegated summoned-count decode to CField_MonsterCarnival while preserving waiting-room wrapper-only UI ownership.",
                                    Array.Empty<int>());
                            }
                            else
                            {
                                RecordRecoveredClientOwnerAction(
                                    $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnEnter -> CUIMonsterCarnival::InsertSpelledData(activeRows={_uiWindowState.ActiveSpelledMobRows}, activeMobs={_uiWindowState.ActiveSpelledMobCount}, preview={_uiWindowState.ActiveSpelledMobPreview}).",
                                    Array.Empty<int>());
                            }
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
                        if (_definition?.IsReviveMode == true)
                        {
                            OnReviveEnter((MonsterCarnivalTeam)reader.ReadByte(), currentTimeMs);
                        }
                        else
                        {
                            OnEnter((MonsterCarnivalTeam)reader.ReadByte(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), currentTimeMs);

                            for (int i = 0; i < (_definition?.MobEntries.Count ?? 0); i++)
                            {
                                int count = reader.ReadByte();
                                MonsterCarnivalEntry entry = _definition.MobEntries[i];
                                SetEntryCount(_mobSpellCounts, entry.Id, count);
                                ReconcileSummonedMobStates(entry, count);
                            }

                            RefreshClientOwnedUiWindowSpellState();
                            if (_definition?.IsWaitingRoom == true)
                            {
                                RecordRecoveredClientOwnerAction(
                                    $"{_definition.ClientOwnerLabel}::OnEnter delegated summoned-count decode to CField_MonsterCarnival while preserving waiting-room wrapper-only UI ownership.",
                                    Array.Empty<int>());
                            }
                            else
                            {
                                RecordRecoveredClientOwnerAction(
                                    $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnEnter -> CUIMonsterCarnival::InsertSpelledData(activeRows={_uiWindowState.ActiveSpelledMobRows}, activeMobs={_uiWindowState.ActiveSpelledMobCount}, preview={_uiWindowState.ActiveSpelledMobPreview}).",
                                    Array.Empty<int>());
                            }
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

            UpdateSeason2SubDialogTimer(tickCount);
            UpdateOwnedClock(tickCount);
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            if (!_isVisible || spriteBatch == null || pixelTexture == null || font == null)
            {
                return;
            }

            if (_definition?.IsWaitingRoom == true)
            {
                DrawWaitingRoomWrapperPanel(spriteBatch, pixelTexture, font);
                return;
            }

            if (_definition?.IsReviveMode == true)
            {
                DrawReviveWrapperPanel(spriteBatch, pixelTexture, font);
                return;
            }

            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            MonsterCarnivalHudMetrics metrics = ResolveHudMetrics();
            Rectangle headerBounds = ResolveHeaderBounds(viewport.Width, metrics);
            Rectangle mainHeaderBounds = new(headerBounds.X, headerBounds.Y, metrics.BackgroundWidth, metrics.BackgroundHeight);
            Rectangle teamHeaderBounds = new(headerBounds.Right, headerBounds.Y, metrics.TeamPanelWidth, metrics.TeamPanelHeight);
            IReadOnlyList<MonsterCarnivalEntry> activeEntries = _definition?.GetEntries(_activeTab) ?? Array.Empty<MonsterCarnivalEntry>();
            Rectangle listBounds = ResolveClientListBounds(headerBounds, activeEntries.Count);
            Rectangle decisionButtonBounds = ResolveClientDecisionButtonBounds(headerBounds, metrics, activeEntries.Count);
            Rectangle footerBounds = ResolveFooterBounds(listBounds, decisionButtonBounds, metrics);

            DrawHeaderPanel(spriteBatch, pixelTexture, mainHeaderBounds, teamHeaderBounds);
            DrawEntryListPanel(spriteBatch, pixelTexture, listBounds, metrics);
            DrawDecisionButton(spriteBatch, decisionButtonBounds);
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
                $"clock={DescribeOwnedClockState(Environment.TickCount)}",
                new Vector2(mainHeaderBounds.X + 12, timerY + 18),
                Color.LightCyan,
                0.72f);
            DrawShadowedText(
                spriteBatch,
                font,
                BuildClientOwnerHeaderSummary(),
                new Vector2(mainHeaderBounds.X + 12, timerY + 34),
                Color.LightSteelBlue,
                0.72f);
            DrawShadowedText(
                spriteBatch,
                font,
                BuildVariantContractSummary(),
                new Vector2(mainHeaderBounds.X + 12, timerY + 50),
                Color.LightGoldenrodYellow,
                0.68f);
            if (_definition?.IsSeason2Mode == true)
            {
                DrawShadowedText(
                    spriteBatch,
                    font,
                    TrimForDisplay($"Season2 sub={DescribeSeason2SubDialogState()}", 92),
                    new Vector2(mainHeaderBounds.X + 12, timerY + 66),
                    Color.LightPink,
                    0.66f);
            }

            DrawCpRow(spriteBatch, pixelTexture, font, teamHeaderBounds);
            DrawTabs(spriteBatch, pixelTexture, font, headerBounds.X + ClientTabControlX, headerBounds.Y + ClientTabControlY);
            DrawEntryList(spriteBatch, pixelTexture, font, listBounds);
            DrawFooter(spriteBatch, pixelTexture, font, footerBounds.X + 8, footerBounds.Y + 4, footerBounds.Width - 16, footerBounds.Height - 8);
        }

        public string DescribeStatus()
        {
            if (!_isVisible)
            {
                return "Monster Carnival runtime is inactive on this map.";
            }

            return $"Monster Carnival: {(_enteredField ? "entered" : "configured")} | mode={_definition?.VariantLabel ?? "Unknown"}{FormatMapTypeSuffix(_definition)} | owner={_definition?.ClientOwnerLabel ?? "unknown"} | tab={_activeTab} | personalCP={_personalCp}/{_personalTotalCp} | team0={_team0.CurrentCp}/{_team0.TotalCp} | team1={_team1.CurrentCp}/{_team1.TotalCp} | mobs={GetTotalCount(_mobSpellCounts)}/{Math.Max(0, _definition?.MobGenMax ?? 0)} | guardians={GetTotalCount(_guardianCounts)}/{Math.Max(0, _definition?.GuardianGenMax ?? 0)} | pendingGuardianReconcile={_pendingGuardianPlacements.Count} | variantPhase={DescribeVariantSessionPhase()} | clock={DescribeOwnedClockState(Environment.TickCount)} | season2SubDialog={DescribeSeason2SubDialogState()} | seam={BuildClientOwnerStatusSummary()}{Environment.NewLine}{_uiWindowState.DescribeStatus()}";
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
            _lastRequestFailureChatRoute = null;
            _lastMemberOutChatRoute = null;
            _lastResultChatRoute = null;
            _lastDeathChatRoute = null;
            _lastClientOwnerAction = null;
            _lastClientOwnerStringPoolIds = Array.Empty<int>();
            _variantActionTrail.Clear();
            _uiWindowState.Reset();
            _variantSessionPhase = MonsterCarnivalVariantSessionPhase.None;
            _variantSessionSummary = null;
            _lastVariantDelegatedPacketType = -1;
            _lastVariantDelegatedRawPacket = false;
            _lastVariantDelegatedOwner = null;
            _lastVariantDelegatedSummary = null;
            _variantTransportPacketCount = 0;
            _variantEnterPacketCount = 0;
            _variantRequestPacketCount = 0;
            _variantResultPacketCount = 0;
            _variantDeathPacketCount = 0;
            _variantLiveHudPacketCount = 0;
            _variantMemberPacketCount = 0;
            _reviveDirectPacketCount = 0;
            _reviveForwardedPacketCount = 0;
            _reviveRoundSequence = 0;
            _reviveResultSequence = 0;
            _variantTransportLastRoute = null;
            _ownedClockPhase = MonsterCarnivalOwnedClockPhase.None;
            _ownedClockSummary = null;
            _roundDeadlineTick = null;
            _extendDeadlineTick = null;
            _resultMessageDeadlineTick = null;
            _exitGraceDeadlineTick = null;
            _season2SubDialogDeadlineTick = null;
            _season2SubDialogTimerSummary = null;
            ResetSeason2SubDialogState(null, MonsterCarnivalSeason2SubDialogPhase.None);
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
            _pendingGuardianPlacements.Clear();
            _nextMobSpawnPointIndex = 0;
            _selectedEntryIndex = 0;
            _lastRequestTab = -1;
            _lastRequestIndex = -1;
            _pendingLocalRequestTab = -1;
            _pendingLocalRequestIndex = -1;
            _lastRequestFailureChatRoute = null;
            _lastMemberOutChatRoute = null;
            _lastResultChatRoute = null;
            _lastDeathChatRoute = null;
            _variantTransportPacketCount = 0;
            _variantEnterPacketCount = 0;
            _variantRequestPacketCount = 0;
            _variantResultPacketCount = 0;
            _variantDeathPacketCount = 0;
            _variantLiveHudPacketCount = 0;
            _variantMemberPacketCount = 0;
            _reviveDirectPacketCount = 0;
            _reviveForwardedPacketCount = 0;
            _reviveRoundSequence = 0;
            _reviveResultSequence = 0;
            _variantTransportLastRoute = null;
            _ownedClockPhase = MonsterCarnivalOwnedClockPhase.None;
            _ownedClockSummary = null;
            _roundDeadlineTick = null;
            _extendDeadlineTick = null;
            _resultMessageDeadlineTick = null;
            _exitGraceDeadlineTick = null;
            _season2SubDialogDeadlineTick = null;
            _season2SubDialogTimerSummary = null;
            ResetSeason2SubDialogState(null, MonsterCarnivalSeason2SubDialogPhase.None);
        }

        private void DrawCpRow(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Rectangle teamBounds)
        {
            if (_hudAssets.TeamPanelBackground == null)
            {
                int rowHeight = 44;
                int x = teamBounds.X + 10;
                int y = teamBounds.Y + 10;
                int width = teamBounds.Width - 20;
                spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, rowHeight), new Color(26, 34, 44, 220));
                string personalText = $"Personal CP  {_personalCp}/{_personalTotalCp}";
                DrawShadowedText(spriteBatch, font, personalText, new Vector2(x + 10, y + 6), Color.White);

                string team0Text = $"{FormatTeam(MonsterCarnivalTeam.Team0)}  {_team0.CurrentCp}/{_team0.TotalCp}";
                string team1Text = $"{FormatTeam(MonsterCarnivalTeam.Team1)}  {_team1.CurrentCp}/{_team1.TotalCp}";
                DrawShadowedText(spriteBatch, font, team0Text, new Vector2(x + 10, y + 24), GetTeamColor(MonsterCarnivalTeam.Team0));
                DrawShadowedText(spriteBatch, font, team1Text, new Vector2(x + width / 2 + 4, y + 24), GetTeamColor(MonsterCarnivalTeam.Team1));
                return;
            }

            DrawShadowedText(
                spriteBatch,
                font,
                $"{_personalCp} / {_personalTotalCp}",
                new Vector2(teamBounds.X + ClientCpTextX, teamBounds.Y + ClientPersonalCpTextY),
                Color.Gainsboro,
                0.78f);
            DrawShadowedText(
                spriteBatch,
                font,
                $"{_team0.CurrentCp} / {_team0.TotalCp}",
                new Vector2(teamBounds.X + ClientCpTextX, teamBounds.Y + ClientRedTeamCpTextY),
                GetTeamColor(MonsterCarnivalTeam.Team0),
                0.78f);
            DrawShadowedText(
                spriteBatch,
                font,
                $"{_team1.CurrentCp} / {_team1.TotalCp}",
                new Vector2(teamBounds.X + ClientCpTextX, teamBounds.Y + ClientBlueTeamCpTextY),
                GetTeamColor(MonsterCarnivalTeam.Team1),
                0.78f);
        }

        private void DrawTabs(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y)
        {
            MonsterCarnivalHudMetrics metrics = ResolveHudMetrics();
            int tabX = x;
            DrawTab(spriteBatch, pixelTexture, font, tabX, y, metrics.TabMobWidth, "Mob", MonsterCarnivalTab.Mob);
            tabX += metrics.TabMobWidth + 2;
            DrawTab(spriteBatch, pixelTexture, font, tabX, y, metrics.TabSkillWidth, "Skill", MonsterCarnivalTab.Skill);
            tabX += metrics.TabSkillWidth + 2;
            DrawTab(spriteBatch, pixelTexture, font, tabX, y, metrics.TabGuardianWidth, "Guardian", MonsterCarnivalTab.Guardian);
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

        private void DrawEntryList(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, Rectangle listBounds)
        {
            if (_hudAssets.ListMiddle == null)
            {
                spriteBatch.Draw(pixelTexture, listBounds, new Color(20, 27, 34, 200));
            }
            IReadOnlyList<MonsterCarnivalEntry> entries = _definition?.GetEntries(_activeTab) ?? Array.Empty<MonsterCarnivalEntry>();
            if (entries.Count == 0)
            {
                DrawShadowedText(spriteBatch, font, "No entries loaded for this tab.", new Vector2(listBounds.X + 10, listBounds.Y + 10), Color.Silver);
                return;
            }

            int rowHeight = ClientTabWindowRowHeight;
            int visibleRows = ResolveClientVisibleTabRowCount(entries.Count);
            for (int i = 0; i < visibleRows; i++)
            {
                MonsterCarnivalEntry entry = entries[i];
                Rectangle rowRect = new Rectangle(listBounds.X + 4, listBounds.Y + i * rowHeight, listBounds.Width - 8, rowHeight - 1);
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
                DrawShadowedText(spriteBatch, font, summary, new Vector2(listBounds.X + 8, listBounds.Bottom + 4), Color.LightSteelBlue, 0.75f);
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
            errorMessage = null;
            return true;
        }

        private bool TryValidateClientOwnedRawPacket(int packetType, out string errorMessage)
        {
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
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => "CField_MonsterCarnivalWaitingRoom::Init calls CField::Init, reads monsterCarnival/mapType, and keeps waiting-room ownership while delegated packets are tracked as a lobby/session transport trail (enter/request/hud/member/death/result) through CField_MonsterCarnival routes.",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => "CField_MonsterCarnivalS2_Game::Init calls CField_MonsterCarnival::Init, reads monsterCarnival/mapType, and keeps the UIWindow2-backed Season 2 surface with packet-owned sub-dialog timer choreography anchored to monsterCarnival/timeMessage.",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => "CField_MonsterCarnivalRevive::OnPacket keeps direct ownership of raw packets 346 and 353 while non-direct packets forward through CField::OnPacket; this seam now reports direct-vs-forwarded transport counters and revive round/result sequence accounting.",
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
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => $"{mapLabel} | phase={DescribeVariantSessionPhase()} | clock={DescribeOwnedClockState(Environment.TickCount)} | Init base={_definition.InitBaseOwnerLabel} | reads monsterCarnival/mapType={_definition.MapType} | waiting-room wrapper panel only | transport={BuildVariantTransportSummary()} | delegated result ids 0x1020/0x1021/0x1022/0x1023 -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => $"{mapLabel} | phase={DescribeVariantSessionPhase()} | clock={DescribeOwnedClockState(Environment.TickCount)} | Init base={_definition.InitBaseOwnerLabel} | reads monsterCarnival/mapType={_definition.MapType} | UI/UIWindow2.img/MonsterCarnival/main+summonList+sub | subDialog={DescribeSeason2SubDialogState()} | transport={BuildVariantTransportSummary()} | delegated result ids 0x1020/0x1021/0x1022/0x1023 -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => $"{mapLabel} | phase={DescribeVariantSessionPhase()} | clock={DescribeOwnedClockState(Environment.TickCount)} | revive direct packets raw 346/353 + forwarded shared packet seams via CField::OnPacket | transport={BuildVariantTransportSummary()} | OnEnter team byte -> CUserLocal team slot + RedrawGuildNameTag | result ids 0x1020/0x1021/0x1022/0x1023 -> CUIStatusBar::ChatLogAdd(type=12,item=-1) | no CUIMonsterCarnival surface",
                _ => $"{mapLabel} | shared Carnival packet family 346-353 | UI 0x102B-0x1033"
            };
        }

        private void LoadHudAssets()
        {
            _hudAssets = new MonsterCarnivalHudAssets();
            if (_graphicsDevice == null || _definition?.IsWaitingRoom == true)
            {
                return;
            }

            if (_definition?.IsSeason2Mode == true)
            {
                WzImage uiWindow2Image = FindImageSafe("UI", "UIWindow2.img");
                WzSubProperty carnivalWindow2Property = uiWindow2Image?["MonsterCarnival"] as WzSubProperty;
                WzSubProperty mainProperty = carnivalWindow2Property?["main"] as WzSubProperty;
                WzSubProperty summonListProperty = carnivalWindow2Property?["summonList"] as WzSubProperty;
                WzSubProperty mainTabProperty = mainProperty?["Tab"] as WzSubProperty;
                WzSubProperty mainEnabledProperty = mainTabProperty?["enabled"] as WzSubProperty;
                WzSubProperty mainDisabledProperty = mainTabProperty?["disabled"] as WzSubProperty;
                WzSubProperty mainSideButtonProperty = mainProperty?["BtSide"] as WzSubProperty;

                _hudAssets = new MonsterCarnivalHudAssets
                {
                    Background = LoadCanvasTexture(mainProperty?["backgrnd"] as WzCanvasProperty),
                    TeamPanelBackground = LoadCanvasTexture(mainProperty?["backgrnd2"] as WzCanvasProperty),
                    ListTop = LoadCanvasTexture(summonListProperty?["backgrnd"] as WzCanvasProperty),
                    ListMiddle = LoadCanvasTexture(summonListProperty?["backgrnd2"] as WzCanvasProperty),
                    ListSummary = LoadCanvasTexture(mainProperty?["backgrnd3"] as WzCanvasProperty),
                    ListBottom = LoadCanvasTexture(summonListProperty?["backgrnd3"] as WzCanvasProperty),
                    SideButtonNormal = LoadCanvasTexture(mainSideButtonProperty?["normal"]?["0"] as WzCanvasProperty),
                    SideButtonDisabled = LoadCanvasTexture(mainSideButtonProperty?["disabled"]?["0"] as WzCanvasProperty),
                    TabMobEnabled = LoadCanvasTexture(mainEnabledProperty?["0"] as WzCanvasProperty),
                    TabSkillEnabled = LoadCanvasTexture(mainEnabledProperty?["1"] as WzCanvasProperty),
                    TabGuardianEnabled = LoadCanvasTexture(mainEnabledProperty?["2"] as WzCanvasProperty),
                    TabMobDisabled = LoadCanvasTexture(mainDisabledProperty?["0"] as WzCanvasProperty),
                    TabSkillDisabled = LoadCanvasTexture(mainDisabledProperty?["1"] as WzCanvasProperty),
                    TabGuardianDisabled = LoadCanvasTexture(mainDisabledProperty?["2"] as WzCanvasProperty)
                };
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

        private static Rectangle ResolveFooterBounds(Rectangle listBounds, Rectangle decisionButtonBounds, MonsterCarnivalHudMetrics metrics)
        {
            return new Rectangle(
                listBounds.X,
                Math.Max(listBounds.Bottom, decisionButtonBounds.Bottom) + 6,
                Math.Max(metrics.BackgroundWidth, listBounds.Width),
                metrics.ListBottomHeight);
        }

        internal static int ResolveClientVisibleTabRowCount(int entryCount)
        {
            if (entryCount <= 0)
            {
                return 0;
            }

            return Math.Min(ClientVisibleTabRowCap, entryCount);
        }

        internal static int ResolveClientTabWindowHeight(int entryCount)
        {
            return ClientTabWindowBaseHeight + (ResolveClientVisibleTabRowCount(entryCount) * ClientTabWindowRowHeight);
        }

        private static Rectangle ResolveClientListBounds(Rectangle headerBounds, int entryCount)
        {
            return new Rectangle(
                headerBounds.X + ClientListCanvasX,
                headerBounds.Y + ClientListCanvasY,
                ClientListCanvasWidth,
                ResolveClientTabWindowHeight(entryCount));
        }

        private static Rectangle ResolveClientDecisionButtonBounds(Rectangle headerBounds, MonsterCarnivalHudMetrics metrics, int entryCount)
        {
            return new Rectangle(
                headerBounds.X + ClientDecisionButtonX,
                headerBounds.Y + ClientDecisionButtonYOffset + ResolveClientTabWindowHeight(entryCount),
                metrics.SideButtonWidth,
                metrics.SideButtonHeight);
        }

        private static Rectangle ResolveRecoveredHudHeaderBounds(int viewportWidth)
        {
            return ResolveHeaderBounds(viewportWidth, DefaultHudMetrics);
        }

        private void DrawWaitingRoomWrapperPanel(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            Rectangle panelBounds = new Rectangle(viewport.Width - 404, 18, 386, 132);
            spriteBatch.Draw(pixelTexture, panelBounds, new Color(24, 30, 40, 225));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelBounds.X, panelBounds.Y, panelBounds.Width, 28), new Color(69, 87, 114, 255));

            DrawShadowedText(spriteBatch, font, "Monster Carnival Waiting Room", new Vector2(panelBounds.X + 10, panelBounds.Y + 6), Color.White);
            DrawShadowedText(
                spriteBatch,
                font,
                $"map={FormatMapIdentity(_definition)} | owner={_definition?.ClientOwnerLabel ?? "unknown"} | mapType={_definition?.MapType ?? -1}",
                new Vector2(panelBounds.X + 10, panelBounds.Y + 36),
                Color.Gainsboro,
                0.82f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay(DescribeVariantSessionPhase(), 76),
                new Vector2(panelBounds.X + 10, panelBounds.Y + 54),
                Color.LightSteelBlue,
                0.8f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay($"clock={DescribeOwnedClockState(Environment.TickCount)}", 78),
                new Vector2(panelBounds.X + 10, panelBounds.Y + 72),
                Color.LightCyan,
                0.76f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay(BuildClientOwnerHeaderSummary(), 78),
                new Vector2(panelBounds.X + 10, panelBounds.Y + 90),
                Color.LightGoldenrodYellow,
                0.75f);
        }

        private void DrawReviveWrapperPanel(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font)
        {
            Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
            Rectangle panelBounds = new Rectangle(viewport.Width - 404, 18, 386, 132);
            spriteBatch.Draw(pixelTexture, panelBounds, new Color(35, 26, 31, 225));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelBounds.X, panelBounds.Y, panelBounds.Width, 28), new Color(104, 66, 78, 255));

            DrawShadowedText(spriteBatch, font, "Monster Carnival Revive", new Vector2(panelBounds.X + 10, panelBounds.Y + 6), Color.White);
            DrawShadowedText(
                spriteBatch,
                font,
                $"map={FormatMapIdentity(_definition)} | owner={_definition?.ClientOwnerLabel ?? "unknown"} | packets=346/353",
                new Vector2(panelBounds.X + 10, panelBounds.Y + 36),
                Color.Gainsboro,
                0.82f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay(DescribeVariantSessionPhase(), 76),
                new Vector2(panelBounds.X + 10, panelBounds.Y + 54),
                Color.LightPink,
                0.8f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay($"clock={DescribeOwnedClockState(Environment.TickCount)}", 78),
                new Vector2(panelBounds.X + 10, panelBounds.Y + 72),
                Color.LightCyan,
                0.76f);
            DrawShadowedText(
                spriteBatch,
                font,
                TrimForDisplay(BuildClientOwnerHeaderSummary(), 78),
                new Vector2(panelBounds.X + 10, panelBounds.Y + 90),
                Color.LightGoldenrodYellow,
                0.75f);
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

        private void DrawDecisionButton(SpriteBatch spriteBatch, Rectangle bounds)
        {
            Texture2D texture = TryValidateRequest(GetEntry(_activeTab, _selectedEntryIndex), out _)
                ? _hudAssets.SideButtonNormal
                : _hudAssets.SideButtonDisabled ?? _hudAssets.SideButtonNormal;
            if (texture != null)
            {
                spriteBatch.Draw(texture, bounds, Color.White);
            }
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

        private static string TrimForDisplay(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0 || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return maxLength <= 3
                ? text[..maxLength]
                : text[..(maxLength - 3)] + "...";
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
                FieldType.FIELDTYPE_MONSTERCARNIVALWAITINGROOM => $"{_definition.ClientOwnerLabel} Init base={_definition.InitBaseOwnerLabel}->monsterCarnival/mapType={_definition.MapType} | phase={DescribeVariantSessionPhase()} | clock={DescribeOwnedClockState(Environment.TickCount)} | transport={BuildVariantTransportSummary()} | memberOut route={_lastMemberOutChatRoute ?? "none"} | delegated result StringPool=0x1020/0x1021/0x1022/0x1023 -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1) | trail={BuildVariantActionTrailSummary()} | last={BuildClientOwnerActionSummary()} | map={FormatMapIdentity(_definition)}",
                FieldType.FIELDTYPE_MONSTERCARNIVAL_S2 => $"{_definition.ClientOwnerLabel} Init base={_definition.InitBaseOwnerLabel}->monsterCarnival/mapType={_definition.MapType} | phase={DescribeVariantSessionPhase()} | clock={DescribeOwnedClockState(Environment.TickCount)} | transport={BuildVariantTransportSummary()} | failure StringPool=0x101B/0x101C/0x101D/0x101E/0x101F -> CUIStatusBar::ChatLogAdd(type=7,item=-1) route={_lastRequestFailureChatRoute ?? "none"} | death route={_lastDeathChatRoute ?? "none"} | memberOut route={_lastMemberOutChatRoute ?? "none"} | delegated result route={_lastResultChatRoute ?? "none"} | ui={_uiWindowState.DescribeStatus()} | subDialog={DescribeSeason2SubDialogState()} | delegated={BuildVariantDelegatedPacketSummary()} | trail={BuildVariantActionTrailSummary()} | last={BuildClientOwnerActionSummary()} | map={FormatMapIdentity(_definition)}",
                FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE => $"{_definition.ClientOwnerLabel} directPackets=346/353 + forwardedSharedRoutes | phase={DescribeVariantSessionPhase()} | clock={DescribeOwnedClockState(Environment.TickCount)} | transport={BuildVariantTransportSummary()} | ui={_uiWindowState.DescribeStatus()} | failure StringPool=0x101B/0x101C/0x101D/0x101E/0x101F -> CUIStatusBar::ChatLogAdd(type=7,item=-1) route={_lastRequestFailureChatRoute ?? "none"} | death route={_lastDeathChatRoute ?? "none"} | memberOut route={_lastMemberOutChatRoute ?? "none"} | result route={_lastResultChatRoute ?? "none"} | delegated={BuildVariantDelegatedPacketSummary()} | trail={BuildVariantActionTrailSummary()} | last={BuildClientOwnerActionSummary()} | map={FormatMapIdentity(_definition)}",
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
                && !CanPlaceGuardianEntry(entry, _localTeam))
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
                return false;
            }

            return !string.IsNullOrWhiteSpace(_localCharacterName)
                && string.Equals(characterName.Trim(), _localCharacterName, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPendingLocalRequest(MonsterCarnivalTab tab, int entryIndex)
        {
            return _pendingLocalRequestTab == (int)tab && _pendingLocalRequestIndex == entryIndex;
        }

        private void ClearPendingLocalRequest()
        {
            _pendingLocalRequestTab = -1;
            _pendingLocalRequestIndex = -1;
        }

        private void ApplySuccessfulRequest(MonsterCarnivalEntry entry, MonsterCarnivalTeam ownerTeam, bool spendLocalCp, bool ownerTeamKnown)
        {
            _activeTab = entry.Tab;
            _selectedEntryIndex = entry.Index;
            _lastRequestTab = (int)entry.Tab;
            _lastRequestIndex = entry.Index;

            MonsterCarnivalTeam resolvedOwnerTeam = ownerTeam;
            int guardianSlotIndex = -1;
            MonsterCarnivalGuardianSpawnPoint guardianSpawnPoint = default;
            bool guardianPlacementResolved = false;
            bool guardianTeamResolvedFromMetadata = false;

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
                RefreshClientOwnedUiWindowSpellState();
            }
            else if (entry.Tab == MonsterCarnivalTab.Guardian)
            {
                guardianPlacementResolved = TryResolveGuardianPlacement(
                    entry,
                    ownerTeam,
                    out guardianSlotIndex,
                    out guardianSpawnPoint,
                    out resolvedOwnerTeam,
                    out guardianTeamResolvedFromMetadata);

                if (!guardianPlacementResolved)
                {
                    TryEnqueuePendingGuardianPlacement(entry, resolvedOwnerTeam, countAlreadyApplied: true);
                    return;
                }

                if (!spendLocalCp && !ownerTeamKnown && guardianTeamResolvedFromMetadata)
                {
                    MonsterCarnivalTeamState inferredTeamState = GetTeamState(resolvedOwnerTeam);
                    inferredTeamState.CurrentCp = Math.Max(0, inferredTeamState.CurrentCp - entry.Cost);
                }

                if (guardianSlotIndex < 0)
                {
                    TryEnqueuePendingGuardianPlacement(entry, resolvedOwnerTeam, countAlreadyApplied: true);
                    return;
                }

                if (_guardianPlacements.TryGetValue(guardianSlotIndex, out MonsterCarnivalGuardianPlacement displacedPlacement)
                    && displacedPlacement?.Entry != null)
                {
                    SetEntryCount(
                        _guardianCounts,
                        displacedPlacement.Entry.Id,
                        Math.Max(0, GetEntryCount(_guardianCounts, displacedPlacement.Entry.Id) - 1));
                }

                _occupiedGuardianSlots.Add(guardianSlotIndex);
                MonsterCarnivalGuardianPlacement placement = new MonsterCarnivalGuardianPlacement(
                    entry,
                    guardianSpawnPoint,
                    ResolveGuardianReactorId(resolvedOwnerTeam),
                    resolvedOwnerTeam);
                placement.ReactorRequiredHits = ResolveGuardianRequiredHits(resolvedOwnerTeam);
                _guardianPlacements[guardianSlotIndex] = placement;
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

        private bool CanPlaceGuardianEntry(MonsterCarnivalEntry entry, MonsterCarnivalTeam preferredTeam)
        {
            return TryResolveGuardianPlacement(
                entry,
                preferredTeam,
                out _,
                out _,
                out _,
                out _);
        }

        private bool TryResolveGuardianPlacement(
            MonsterCarnivalEntry entry,
            MonsterCarnivalTeam preferredTeam,
            out int slotIndex,
            out MonsterCarnivalGuardianSpawnPoint spawnPoint,
            out MonsterCarnivalTeam resolvedTeam,
            out bool resolvedTeamFromMetadata)
        {
            slotIndex = -1;
            spawnPoint = default;
            resolvedTeam = preferredTeam;
            resolvedTeamFromMetadata = false;

            if (entry == null)
            {
                return false;
            }

            IReadOnlyList<MonsterCarnivalGuardianSpawnPoint> positions = _definition?.GuardianSpawnPositions;
            if (positions == null || positions.Count == 0)
            {
                slotIndex = entry.Index;
                spawnPoint = ResolveGuardianSpawnPoint(entry.Index);
                resolvedTeam = spawnPoint.Team ?? preferredTeam;
                resolvedTeamFromMetadata = spawnPoint.Team.HasValue;
                return !_occupiedGuardianSlots.Contains(slotIndex);
            }

            foreach (MonsterCarnivalGuardianSpawnPoint candidate in EnumerateGuardianPlacementCandidates(positions, entry.Index))
            {
                if (_occupiedGuardianSlots.Contains(candidate.Index))
                {
                    continue;
                }

                if (candidate.Team.HasValue && candidate.Team.Value != preferredTeam)
                {
                    continue;
                }

                slotIndex = candidate.Index;
                spawnPoint = candidate;
                resolvedTeam = candidate.Team ?? preferredTeam;
                resolvedTeamFromMetadata = candidate.Team.HasValue;
                return true;
            }

            return false;
        }

        private static IEnumerable<MonsterCarnivalGuardianSpawnPoint> EnumerateGuardianPlacementCandidates(
            IReadOnlyList<MonsterCarnivalGuardianSpawnPoint> positions,
            int preferredSlotIndex)
        {
            if (positions == null || positions.Count == 0)
            {
                yield break;
            }

            MonsterCarnivalGuardianSpawnPoint? exactMatch = null;
            List<MonsterCarnivalGuardianSpawnPoint> ordered = positions
                .OrderBy(position => position.Index)
                .ToList();

            foreach (MonsterCarnivalGuardianSpawnPoint position in ordered)
            {
                if (position.Index == preferredSlotIndex)
                {
                    exactMatch = position;
                    break;
                }
            }

            if (exactMatch.HasValue)
            {
                yield return exactMatch.Value;
            }

            int orderedStartIndex = ordered.FindIndex(position => position.Index >= preferredSlotIndex);
            if (orderedStartIndex < 0)
            {
                orderedStartIndex = 0;
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                MonsterCarnivalGuardianSpawnPoint candidate = ordered[(orderedStartIndex + i) % ordered.Count];
                if (exactMatch.HasValue && candidate.Index == exactMatch.Value.Index)
                {
                    continue;
                }

                yield return candidate;
            }
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

        private bool TryResolveCandidateGuardianTeamsFromReactorId(int reactorId, out IReadOnlyList<MonsterCarnivalTeam> teams)
        {
            var resolved = new List<MonsterCarnivalTeam>(2);
            if (_definition != null)
            {
                if (reactorId == Math.Max(0, _definition.ReactorRed))
                {
                    resolved.Add(MonsterCarnivalTeam.Team0);
                }

                if (reactorId == Math.Max(0, _definition.ReactorBlue))
                {
                    resolved.Add(MonsterCarnivalTeam.Team1);
                }
            }

            if (resolved.Count == 0)
            {
                teams = Array.Empty<MonsterCarnivalTeam>();
                return false;
            }

            teams = resolved;
            return true;
        }

        private bool TryResolveGuardianSlotFromLiveReactor(
            IReadOnlyList<MonsterCarnivalTeam> candidateTeams,
            int worldX,
            int worldY,
            bool flip,
            out int slotIndex,
            out MonsterCarnivalGuardianSpawnPoint spawnPoint,
            out MonsterCarnivalTeam resolvedTeam)
        {
            slotIndex = -1;
            spawnPoint = default;
            resolvedTeam = _localTeam;

            IReadOnlyList<MonsterCarnivalGuardianSpawnPoint> positions = _definition?.GuardianSpawnPositions;
            if (positions == null || positions.Count == 0 || candidateTeams == null || candidateTeams.Count == 0)
            {
                return false;
            }

            int expectedFacing = flip ? 1 : 0;
            int bestAvailableScore = int.MaxValue;
            int bestOccupiedScore = int.MaxValue;
            int occupiedSlotIndex = -1;
            MonsterCarnivalGuardianSpawnPoint occupiedSpawnPoint = default;
            MonsterCarnivalTeam occupiedResolvedTeam = _localTeam;
            foreach (MonsterCarnivalGuardianSpawnPoint candidate in positions)
            {
                foreach (MonsterCarnivalTeam candidateTeam in candidateTeams)
                {
                    if (candidate.Team.HasValue && candidate.Team.Value != candidateTeam)
                    {
                        continue;
                    }

                    int distance = Math.Abs(candidate.X - worldX) + Math.Abs(candidate.Y - worldY);
                    int facingPenalty = candidate.Facing == expectedFacing ? 0 : 8;
                    int score = distance + facingPenalty;

                    if (_occupiedGuardianSlots.Contains(candidate.Index))
                    {
                        if (score >= bestOccupiedScore)
                        {
                            continue;
                        }

                        bestOccupiedScore = score;
                        occupiedSlotIndex = candidate.Index;
                        occupiedSpawnPoint = candidate;
                        occupiedResolvedTeam = candidate.Team ?? candidateTeam;
                        continue;
                    }

                    if (score >= bestAvailableScore)
                    {
                        continue;
                    }

                    bestAvailableScore = score;
                    slotIndex = candidate.Index;
                    spawnPoint = candidate;
                    resolvedTeam = candidate.Team ?? candidateTeam;
                }
            }

            if (slotIndex >= 0)
            {
                return true;
            }

            if (occupiedSlotIndex >= 0)
            {
                slotIndex = occupiedSlotIndex;
                spawnPoint = occupiedSpawnPoint;
                resolvedTeam = occupiedResolvedTeam;
                return true;
            }

            return slotIndex >= 0;
        }

        private MonsterCarnivalEntry ResolveGuardianEntryForSlot(int slotIndex)
        {
            IReadOnlyList<MonsterCarnivalEntry> entries = _definition?.GuardianEntries;
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            MonsterCarnivalEntry exactEntry = entries.FirstOrDefault(candidate => candidate.Index == slotIndex);
            if (exactEntry != null)
            {
                return exactEntry;
            }

            return entries
                .OrderBy(candidate => Math.Abs(candidate.Index - slotIndex))
                .FirstOrDefault();
        }

        private void TryEnqueuePendingGuardianPlacement(MonsterCarnivalEntry entry, MonsterCarnivalTeam team, bool countAlreadyApplied)
        {
            if (entry?.Tab != MonsterCarnivalTab.Guardian)
            {
                return;
            }

            _pendingGuardianPlacements.Enqueue(new PendingGuardianPlacement(entry, team, countAlreadyApplied));
        }

        private bool TryDequeuePendingGuardianPlacement(MonsterCarnivalTeam team, out PendingGuardianPlacement placement)
        {
            placement = default;
            if (_pendingGuardianPlacements.Count <= 0)
            {
                return false;
            }

            int pendingCount = _pendingGuardianPlacements.Count;
            List<PendingGuardianPlacement> unmatched = new(pendingCount);
            bool hasMatch = false;
            PendingGuardianPlacement matchedPlacement = default;
            for (int i = 0; i < pendingCount; i++)
            {
                PendingGuardianPlacement next = _pendingGuardianPlacements.Dequeue();
                if (next.Entry == null)
                {
                    continue;
                }

                if (!hasMatch && next.Team == team)
                {
                    matchedPlacement = next;
                    hasMatch = true;
                    continue;
                }

                unmatched.Add(next);
            }

            if (hasMatch)
            {
                foreach (PendingGuardianPlacement pending in unmatched)
                {
                    _pendingGuardianPlacements.Enqueue(pending);
                }

                placement = matchedPlacement;
                return true;
            }

            if (unmatched.Count <= 0)
            {
                return false;
            }

            placement = unmatched[0];
            for (int i = 1; i < unmatched.Count; i++)
            {
                _pendingGuardianPlacements.Enqueue(unmatched[i]);
            }

            return true;
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
                team = default;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_localCharacterName)
                && string.Equals(normalizedName, _localCharacterName, StringComparison.OrdinalIgnoreCase))
            {
                team = _localTeam;
                return true;
            }

            return _knownCharacterTeams.TryGetValue(normalizedName, out team);
        }

        private bool TryInferGuardianOwnerTeamFromSlotMetadata(MonsterCarnivalEntry entry, out MonsterCarnivalTeam team)
        {
            team = default;
            if (entry?.Tab != MonsterCarnivalTab.Guardian)
            {
                return false;
            }

            if (_guardianPlacements.TryGetValue(entry.Index, out MonsterCarnivalGuardianPlacement existingPlacement)
                && existingPlacement?.Entry != null)
            {
                team = existingPlacement.Team;
                return true;
            }

            IReadOnlyList<MonsterCarnivalGuardianSpawnPoint> positions = _definition?.GuardianSpawnPositions;
            if (positions == null || positions.Count == 0)
            {
                return false;
            }

            foreach (MonsterCarnivalGuardianSpawnPoint candidate in EnumerateGuardianPlacementCandidates(positions, entry.Index))
            {
                if (!candidate.Team.HasValue)
                {
                    continue;
                }

                team = candidate.Team.Value;
                return true;
            }

            return false;
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
                _lastResultChatRoute = null;
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
                _lastResultChatRoute =
                    $"{_definition.ClientOwnerLabel}::OnShowGameResult(code={resultCode}) -> CUIStatusBar::ChatLogAdd(type=12,item=-1)";
                RecordClientOwnerAction(
                    $"{_definition.ClientOwnerLabel}::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1) code={resultCode}",
                    new[] { definition.Value.StringPoolId });
                return $"{message} via {_definition.ClientOwnerLabel}::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)";
            }

            if (_definition?.IsWaitingRoom == true || _definition?.IsSeason2Mode == true)
            {
                _lastResultChatRoute =
                    $"{_definition.ClientOwnerLabel} delegated OnShowGameResult(code={resultCode}) -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)";
                RecordClientOwnerAction(
                    $"{_definition.ClientOwnerLabel} delegated OnShowGameResult code={resultCode} to CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)",
                    new[] { definition.Value.StringPoolId });
                return $"{message} via {_definition.ClientOwnerLabel} -> CField_MonsterCarnival::OnShowGameResult -> CUIStatusBar::ChatLogAdd(type=12,item=-1)";
            }

            _lastResultChatRoute = $"{_definition?.ClientOwnerLabel ?? "CField_MonsterCarnival"}::OnShowGameResult(code={resultCode})";
            return message;
        }

        private void RecordVariantWrapperPacketDelegation(int packetType, bool rawPacket)
        {
            if (_definition == null || !ShouldTrackVariantClientOwnerAction())
            {
                return;
            }

            string packetLabel = rawPacket ? "raw packet" : "packet";
            string delegatedOwner = ResolveVariantDelegatedOwner(packetType, rawPacket);
            _lastVariantDelegatedPacketType = packetType;
            _lastVariantDelegatedRawPacket = rawPacket;
            _lastVariantDelegatedOwner = delegatedOwner;
            _lastVariantDelegatedSummary = $"{packetLabel} {packetType} -> {delegatedOwner}";
            RecordVariantTransportPacket(packetType, rawPacket, delegatedOwner);

            if (_definition.IsWaitingRoom)
            {
                SetVariantSessionPhase(
                    ResolveDelegatedVariantPhase(packetType),
                    $"{_definition.ClientOwnerLabel} delegated {packetLabel} {packetType} through {delegatedOwner} while keeping the waiting-room wrapper-only surface.");
            }
            else if (_definition.IsSeason2Mode)
            {
                SetVariantSessionPhase(
                    ResolveDelegatedVariantPhase(packetType),
                    $"{_definition.ClientOwnerLabel} delegated {packetLabel} {packetType} through {delegatedOwner} while retaining Season 2 UIWindow2 ownership.");
            }
            else if (_definition.IsReviveMode)
            {
                SetVariantSessionPhase(
                    ResolveDelegatedVariantPhase(packetType),
                    $"{_definition.ClientOwnerLabel} routed {packetLabel} {packetType} through {delegatedOwner}.");
            }

            RecordClientOwnerAction(
                $"{_definition.ClientOwnerLabel}::Init retained monsterCarnival/mapType={_definition.MapType}; {packetLabel} {packetType} delegated to {delegatedOwner}.",
                Array.Empty<int>());
        }

        private void RecordVariantTransportPacket(int packetType, bool rawPacket, string delegatedOwner)
        {
            if (!ShouldTrackVariantClientOwnerAction())
            {
                return;
            }

            _variantTransportPacketCount++;
            if (packetType is 1 or 346)
            {
                _variantEnterPacketCount++;
            }
            else if (packetType is 2 or 3 or 349 or 350)
            {
                _variantRequestPacketCount++;
            }
            else if (packetType is 4 or 353)
            {
                _variantResultPacketCount++;
            }
            else if (packetType is 5 or 351)
            {
                _variantDeathPacketCount++;
            }
            else if (packetType == 352)
            {
                _variantMemberPacketCount++;
            }
            else if (packetType is 6 or 7 or 8 or 347 or 348)
            {
                _variantLiveHudPacketCount++;
            }

            string packetLabel = rawPacket ? "raw" : "packet";
            _variantTransportLastRoute = $"{packetLabel} {packetType} -> {delegatedOwner}";

            if (_definition?.IsReviveMode != true)
            {
                return;
            }

            bool isDirectReviveOwnerPacket = packetType is 1 or 346 or 4 or 353;
            if (isDirectReviveOwnerPacket)
            {
                _reviveDirectPacketCount++;
                if (packetType is 1 or 346)
                {
                    _reviveRoundSequence++;
                }
                else if (packetType is 4 or 353)
                {
                    _reviveResultSequence++;
                }
            }
            else
            {
                _reviveForwardedPacketCount++;
            }
        }

        private void MarkClientOwnedCpHudRefresh(string packetOwner, string uiOwner)
        {
            string ownerLabel = _definition?.ClientOwnerLabel ?? "CField_MonsterCarnival";
            if (_definition?.IsWaitingRoom == true)
            {
                SetVariantSessionPhase(
                    MonsterCarnivalVariantSessionPhase.LiveHud,
                    $"{ownerLabel}::{packetOwner} delegated CP synchronization to CField_MonsterCarnival while the waiting-room wrapper remained UI-less.");
                RecordRecoveredClientOwnerAction(
                    $"{ownerLabel}::{packetOwner} delegated CP ownership without creating CUIMonsterCarnival.",
                    Array.Empty<int>());
                return;
            }

            SetVariantSessionPhase(
                MonsterCarnivalVariantSessionPhase.LiveHud,
                $"{ownerLabel}::{packetOwner} refreshed the shared Carnival HUD through {uiOwner}.");
            RecordRecoveredClientOwnerAction(
                $"{ownerLabel}::{packetOwner} -> {uiOwner}.",
                Array.Empty<int>());
        }

        private void UpdateSeason2SubDialogOnEnter(int tickCount)
        {
            if (_definition?.IsSeason2Mode != true)
            {
                return;
            }

            int messageSeconds = Math.Max(0, _definition.MessageTimeSeconds);
            _season2SubDialogDeadlineTick = messageSeconds > 0
                ? unchecked(tickCount + (messageSeconds * 1000))
                : null;
            _season2SubDialogTimerSummary = messageSeconds > 0
                ? $"Season 2 dialog timer primed from monsterCarnival/timeMessage={messageSeconds}s and waiting for request/death ownership."
                : "Season 2 dialog timer unavailable because monsterCarnival/timeMessage was not authored.";
            SetSeason2SubDialogState(
                visible: false,
                okEnabled: false,
                selectionLocked: false,
                "UIWindow2.img/MonsterCarnival/sub kept its owner surface idle after OnEnter; request/result traffic controls visibility.",
                MonsterCarnivalSeason2SubDialogPhase.IdleHidden);
        }

        private void UpdateSeason2SubDialogOnRequest(MonsterCarnivalEntry entry, bool success, int? reasonCode, int tickCount)
        {
            if (_definition?.IsSeason2Mode != true)
            {
                return;
            }

            int messageSeconds = Math.Max(0, _definition.MessageTimeSeconds);
            _season2SubDialogDeadlineTick = messageSeconds > 0
                ? unchecked(tickCount + (messageSeconds * 1000))
                : null;
            _season2SubDialogTimerSummary = messageSeconds > 0
                ? $"Season 2 sub dialog timer armed for {messageSeconds}s from monsterCarnival/timeMessage after request routing."
                : "Season 2 sub dialog timer unavailable because monsterCarnival/timeMessage was not authored.";
            if (success)
            {
                string entryLabel = entry == null
                    ? "unknown-entry"
                    : $"tab={(int)entry.Tab},index={entry.Index},id={entry.Id},name={entry.Name},cost={entry.Cost}";
                SetSeason2SubDialogState(
                    visible: true,
                    okEnabled: true,
                    selectionLocked: false,
                    $"Season 2 request accepted; UIWindow2.img/MonsterCarnival/sub surfaced {entryLabel} and kept BtOK active for local acknowledgement.",
                    MonsterCarnivalSeason2SubDialogPhase.RequestAcceptedPending);
                return;
            }

            int resolvedReasonCode = Math.Max(0, reasonCode ?? 0);
            bool lockSelection = resolvedReasonCode is 3 or 4 or 5;
            SetSeason2SubDialogState(
                visible: true,
                okEnabled: true,
                selectionLocked: lockSelection,
                $"Season 2 request rejected (reason={resolvedReasonCode}); sub dialog remained visible with BtOK while {(lockSelection ? "lock icons remained active" : "selection locks stayed clear")}.",
                lockSelection
                    ? MonsterCarnivalSeason2SubDialogPhase.RequestRejectedLocked
                    : MonsterCarnivalSeason2SubDialogPhase.RequestRejected);
        }

        private void UpdateSeason2SubDialogOnProcessForDeath(MonsterCarnivalTeam team, string characterName, int lostCp, int tickCount)
        {
            if (_definition?.IsSeason2Mode != true)
            {
                return;
            }

            int messageSeconds = Math.Max(0, _definition.MessageTimeSeconds);
            _season2SubDialogDeadlineTick = messageSeconds > 0
                ? unchecked(tickCount + (messageSeconds * 1000))
                : null;
            _season2SubDialogTimerSummary = messageSeconds > 0
                ? $"Season 2 sub dialog death lock timer armed for {messageSeconds}s from monsterCarnival/timeMessage."
                : "Season 2 sub dialog death lock has no local timer because monsterCarnival/timeMessage was not authored.";
            string actor = string.IsNullOrWhiteSpace(characterName) ? "unknown-member" : characterName.Trim();
            SetSeason2SubDialogState(
                visible: true,
                okEnabled: true,
                selectionLocked: true,
                $"Season 2 death-state kept sub dialog ownership visible for {actor} ({FormatTeam(team)}) with lostCP={Math.Max(0, lostCp)} and lock indicators active.",
                MonsterCarnivalSeason2SubDialogPhase.DeathLocked);
        }

        private void UpdateSeason2SubDialogTimer(int tickCount)
        {
            if (_definition?.IsSeason2Mode != true
                || !_season2SubDialogVisible
                || !_season2SubDialogDeadlineTick.HasValue
                || tickCount < _season2SubDialogDeadlineTick.Value)
            {
                return;
            }

            int messageSeconds = Math.Max(0, _definition.MessageTimeSeconds);
            _season2SubDialogDeadlineTick = null;
            _season2SubDialogTimerSummary = messageSeconds > 0
                ? $"Season 2 sub dialog timer elapsed after {messageSeconds}s (monsterCarnival/timeMessage); UIWindow2 sub dialog auto-hid until the next request/death/result packet."
                : "Season 2 sub dialog auto-hid after timer expiry.";
            SetSeason2SubDialogState(
                visible: false,
                okEnabled: false,
                selectionLocked: false,
                "Season 2 sub dialog auto-hid locally after timer expiry while waiting for the next packet-owned update.",
                MonsterCarnivalSeason2SubDialogPhase.TimedHidden);
        }

        private void SetSeason2SubDialogState(bool visible, bool okEnabled, bool selectionLocked, string summary, MonsterCarnivalSeason2SubDialogPhase phase)
        {
            if (_definition?.IsSeason2Mode != true)
            {
                ResetSeason2SubDialogState(null, MonsterCarnivalSeason2SubDialogPhase.None);
                return;
            }

            _season2SubDialogVisible = visible;
            _season2SubDialogOkEnabled = visible && okEnabled;
            _season2SubDialogSelectionLocked = visible && selectionLocked;
            _season2SubDialogPhase = phase;
            _season2SubDialogSummary = string.IsNullOrWhiteSpace(summary)
                ? null
                : summary.Trim();
        }

        private void ResetSeason2SubDialogState(string summary, MonsterCarnivalSeason2SubDialogPhase phase)
        {
            _season2SubDialogVisible = false;
            _season2SubDialogOkEnabled = false;
            _season2SubDialogSelectionLocked = false;
            _season2SubDialogPhase = phase;
            _season2SubDialogDeadlineTick = null;
            _season2SubDialogSummary = string.IsNullOrWhiteSpace(summary)
                ? null
                : summary.Trim();
            if (phase == MonsterCarnivalSeason2SubDialogPhase.None)
            {
                _season2SubDialogTimerSummary = null;
            }
        }

        private string DescribeSeason2SubDialogState()
        {
            if (_definition?.IsSeason2Mode != true)
            {
                return "n/a";
            }

            string visibilityLabel = _season2SubDialogVisible ? "visible" : "hidden";
            string detail = _season2SubDialogVisible
                ? $"ok={_season2SubDialogOkEnabled},lock={_season2SubDialogSelectionLocked}"
                : "ok=false,lock=false";
            string phaseLabel = _season2SubDialogPhase switch
            {
                MonsterCarnivalSeason2SubDialogPhase.IdleHidden => "idle-hidden",
                MonsterCarnivalSeason2SubDialogPhase.RequestAcceptedPending => "request-accepted",
                MonsterCarnivalSeason2SubDialogPhase.RequestRejected => "request-rejected",
                MonsterCarnivalSeason2SubDialogPhase.RequestRejectedLocked => "request-rejected-locked",
                MonsterCarnivalSeason2SubDialogPhase.DeathLocked => "death-locked",
                MonsterCarnivalSeason2SubDialogPhase.ResultClosed => "result-closed",
                MonsterCarnivalSeason2SubDialogPhase.TimedHidden => "timed-hidden",
                _ => "none"
            };
            string timerLabel = _season2SubDialogDeadlineTick.HasValue
                ? $"timer={Math.Max(0, (int)Math.Ceiling((_season2SubDialogDeadlineTick.Value - Environment.TickCount) / 1000d))}s"
                : "timer=none";
            string timerSummary = string.IsNullOrWhiteSpace(_season2SubDialogTimerSummary)
                ? string.Empty
                : $" {_season2SubDialogTimerSummary}";

            return string.IsNullOrWhiteSpace(_season2SubDialogSummary)
                ? $"{visibilityLabel} ({detail},phase={phaseLabel},{timerLabel}){timerSummary}"
                : $"{visibilityLabel} ({detail},phase={phaseLabel},{timerLabel}) {_season2SubDialogSummary}{timerSummary}";
        }

        private void InitializeClientOwnedUiWindowState(MonsterCarnivalFieldDefinition definition)
        {
            _uiWindowState.Reset();
            ResetSeason2SubDialogState(null, MonsterCarnivalSeason2SubDialogPhase.None);
            if (definition == null || definition.IsDeprecatedMode)
            {
                return;
            }

            if (definition.IsWaitingRoom || definition.IsReviveMode)
            {
                _uiWindowState.CaptureWrapperOnlySurface(
                    definition.ClientOwnerLabel,
                    definition.IsReviveMode
                        ? $"{definition.ClientOwnerLabel} decodes a single team byte in OnEnter and redraws CUserLocal name-tag ownership without creating CUIMonsterCarnival."
                        : $"{definition.ClientOwnerLabel} stayed on monsterCarnival/mapType={definition.MapType} without creating the shared Carnival HUD.");
                RecordClientOwnerAction(
                    definition.IsReviveMode
                        ? $"{definition.ClientOwnerLabel}::Init stayed on the revive wrapper seam where OnEnter only updates local team ownership without creating CUIMonsterCarnival."
                        : $"{definition.ClientOwnerLabel}::Init stayed on the waiting-room wrapper seam without creating the shared Carnival HUD.",
                    Array.Empty<int>());
                return;
            }

            bool preferWindow2Assets = definition.IsSeason2Mode;
            _uiWindowState.CreateFromDefinition(definition, preferWindow2Assets);
            if (preferWindow2Assets)
            {
                SetSeason2SubDialogState(
                    visible: false,
                    okEnabled: false,
                    selectionLocked: false,
                    "Season 2 UIWindow2 sub dialog owner initialized and waiting for request/death traffic.",
                    MonsterCarnivalSeason2SubDialogPhase.IdleHidden);
            }
            RecordClientOwnerAction(
                preferWindow2Assets
                    ? $"{definition.ClientOwnerLabel}::CreateUIWindow -> UIWindow2.img/MonsterCarnival/main/summonList/sub -> CUIMonsterCarnival::SetUIData(mob={_uiWindowState.MobRows},skill={_uiWindowState.SkillRows},guardian={_uiWindowState.GuardianRows}) -> CUIMonsterCarnival::ResetUI."
                    : $"CField_MonsterCarnival::CreateUIWindow -> CUIMonsterCarnival::SetUIData(mob={_uiWindowState.MobRows},skill={_uiWindowState.SkillRows},guardian={_uiWindowState.GuardianRows}) -> CUIMonsterCarnival::ResetUI.",
                Array.Empty<int>());
        }

        private void RecreateClientOwnedUiWindowStateForEnter()
        {
            if (_definition == null || _definition.IsDeprecatedMode)
            {
                return;
            }

            if (_definition.IsWaitingRoom || _definition.IsReviveMode)
            {
                _uiWindowState.CaptureWrapperOnlySurface(
                    _definition.ClientOwnerLabel,
                    _definition.IsReviveMode
                        ? $"{_definition.ClientOwnerLabel} stayed on the revive wrapper seam during enter and did not create CUIMonsterCarnival."
                        : $"{_definition.ClientOwnerLabel} remained on the waiting-room lobby seam during enter and did not create the shared Carnival HUD.");
                return;
            }

            bool preferWindow2Assets = _definition.IsSeason2Mode;
            _uiWindowState.CreateFromDefinition(_definition, preferWindow2Assets);
            if (preferWindow2Assets)
            {
                SetSeason2SubDialogState(
                    visible: false,
                    okEnabled: false,
                    selectionLocked: false,
                    "Season 2 sub dialog owner recreated during OnEnter and remained hidden until request/death updates.",
                    MonsterCarnivalSeason2SubDialogPhase.IdleHidden);
            }
            if (ShouldTrackVariantClientOwnerAction())
            {
                SetVariantSessionPhase(
                    MonsterCarnivalVariantSessionPhase.UiWindow,
                    preferWindow2Assets
                        ? $"{_definition.ClientOwnerLabel} recreated its UIWindow2-backed Season 2 Carnival surface on enter."
                        : $"{_definition.ClientOwnerLabel} recreated its Carnival HUD surface on enter.");
            }
            RecordClientOwnerAction(
                preferWindow2Assets
                    ? $"{_definition.ClientOwnerLabel}::OnEnter -> {_definition.ClientOwnerLabel}::CreateUIWindow -> UIWindow2.img/MonsterCarnival/main/summonList/sub -> CUIMonsterCarnival::SetUIData(mob={_uiWindowState.MobRows},skill={_uiWindowState.SkillRows},guardian={_uiWindowState.GuardianRows}) -> CUIMonsterCarnival::ResetUI."
                    : $"CField_MonsterCarnival::OnEnter -> CField_MonsterCarnival::CreateUIWindow -> CUIMonsterCarnival::SetUIData(mob={_uiWindowState.MobRows},skill={_uiWindowState.SkillRows},guardian={_uiWindowState.GuardianRows}) -> CUIMonsterCarnival::ResetUI.",
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

        private void RefreshClientOwnedUiWindowSpellState()
        {
            if (!_uiWindowState.IsCreated)
            {
                return;
            }

            _uiWindowState.SyncSpelledMobCounts(_mobSpellCounts);
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

        private void StartOwnedRoundClock(int tickCount)
        {
            int baseSeconds = Math.Max(0, _definition?.DefaultTimeSeconds ?? 0);
            int extendSeconds = Math.Max(0, _definition?.ExpandTimeSeconds ?? 0);

            _roundDeadlineTick = baseSeconds > 0
                ? unchecked(tickCount + (baseSeconds * 1000))
                : null;
            _extendDeadlineTick = extendSeconds > 0
                ? unchecked((_roundDeadlineTick ?? tickCount) + (extendSeconds * 1000))
                : null;
            _resultMessageDeadlineTick = null;
            _exitGraceDeadlineTick = null;

            if (_roundDeadlineTick.HasValue)
            {
                _ownedClockPhase = MonsterCarnivalOwnedClockPhase.ActiveRound;
                _ownedClockSummary =
                    $"monsterCarnival/timeDefault={baseSeconds}s started the owned round clock{(extendSeconds > 0 ? $" with +timeExpand={extendSeconds}s extension queued." : ".")}";
                return;
            }

            _ownedClockPhase = MonsterCarnivalOwnedClockPhase.None;
            _ownedClockSummary = "No authored Carnival round timer found for this map.";
        }

        private void StartOwnedResultClock(int tickCount)
        {
            int messageSeconds = Math.Max(0, _definition?.MessageTimeSeconds ?? 0);
            int finishSeconds = Math.Max(0, _definition?.FinishTimeSeconds ?? 0);

            _roundDeadlineTick = null;
            _extendDeadlineTick = null;
            _resultMessageDeadlineTick = messageSeconds > 0
                ? unchecked(tickCount + (messageSeconds * 1000))
                : null;
            _exitGraceDeadlineTick = finishSeconds > 0
                ? unchecked((_resultMessageDeadlineTick ?? tickCount) + (finishSeconds * 1000))
                : null;

            if (_resultMessageDeadlineTick.HasValue)
            {
                _ownedClockPhase = MonsterCarnivalOwnedClockPhase.ResultMessage;
                _ownedClockSummary =
                    $"OnShowGameResult started message-time={messageSeconds}s then finish-time={finishSeconds}s from monsterCarnival/timeMessage,timeFinish.";
                return;
            }

            if (_exitGraceDeadlineTick.HasValue)
            {
                _ownedClockPhase = MonsterCarnivalOwnedClockPhase.ExitGrace;
                _ownedClockSummary =
                    $"OnShowGameResult started finish-time={finishSeconds}s from monsterCarnival/timeFinish.";
                return;
            }

            _ownedClockPhase = MonsterCarnivalOwnedClockPhase.Closed;
            _ownedClockSummary = "OnShowGameResult closed the local round clock immediately.";
        }

        private void UpdateOwnedClock(int tickCount)
        {
            if (_ownedClockPhase == MonsterCarnivalOwnedClockPhase.ActiveRound
                && _roundDeadlineTick.HasValue
                && tickCount >= _roundDeadlineTick.Value)
            {
                if (_extendDeadlineTick.HasValue)
                {
                    _ownedClockPhase = MonsterCarnivalOwnedClockPhase.ExtendedRound;
                    _ownedClockSummary = "Round default window elapsed; the local owner moved into the timeExpand extension window.";
                }
                else
                {
                    _ownedClockPhase = MonsterCarnivalOwnedClockPhase.Closed;
                    _ownedClockSummary = "Round default window elapsed and no extension window remained.";
                }
            }

            if (_ownedClockPhase == MonsterCarnivalOwnedClockPhase.ExtendedRound
                && _extendDeadlineTick.HasValue
                && tickCount >= _extendDeadlineTick.Value)
            {
                _ownedClockPhase = MonsterCarnivalOwnedClockPhase.Closed;
                _ownedClockSummary = "Round extension window elapsed.";
            }

            if (_ownedClockPhase == MonsterCarnivalOwnedClockPhase.ResultMessage
                && _resultMessageDeadlineTick.HasValue
                && tickCount >= _resultMessageDeadlineTick.Value)
            {
                if (_exitGraceDeadlineTick.HasValue)
                {
                    _ownedClockPhase = MonsterCarnivalOwnedClockPhase.ExitGrace;
                    _ownedClockSummary = "Result message window elapsed; finish grace window is now active.";
                }
                else
                {
                    _ownedClockPhase = MonsterCarnivalOwnedClockPhase.Closed;
                    _ownedClockSummary = "Result message window elapsed.";
                }
            }

            if (_ownedClockPhase == MonsterCarnivalOwnedClockPhase.ExitGrace
                && _exitGraceDeadlineTick.HasValue
                && tickCount >= _exitGraceDeadlineTick.Value)
            {
                _ownedClockPhase = MonsterCarnivalOwnedClockPhase.Closed;
                _ownedClockSummary = "Finish grace window elapsed.";
            }
        }

        private string DescribeOwnedClockState(int currentTick)
        {
            string phaseLabel = _ownedClockPhase switch
            {
                MonsterCarnivalOwnedClockPhase.ActiveRound => "round",
                MonsterCarnivalOwnedClockPhase.ExtendedRound => "round-extend",
                MonsterCarnivalOwnedClockPhase.ResultMessage => "result-message",
                MonsterCarnivalOwnedClockPhase.ExitGrace => "result-finish",
                MonsterCarnivalOwnedClockPhase.Closed => "closed",
                _ => "none"
            };

            int? remainingMs = _ownedClockPhase switch
            {
                MonsterCarnivalOwnedClockPhase.ActiveRound => _roundDeadlineTick.HasValue ? _roundDeadlineTick.Value - currentTick : null,
                MonsterCarnivalOwnedClockPhase.ExtendedRound => _extendDeadlineTick.HasValue ? _extendDeadlineTick.Value - currentTick : null,
                MonsterCarnivalOwnedClockPhase.ResultMessage => _resultMessageDeadlineTick.HasValue ? _resultMessageDeadlineTick.Value - currentTick : null,
                MonsterCarnivalOwnedClockPhase.ExitGrace => _exitGraceDeadlineTick.HasValue ? _exitGraceDeadlineTick.Value - currentTick : null,
                _ => null
            };

            string countdown = remainingMs.HasValue
                ? $" t={Math.Max(0, (int)Math.Ceiling(remainingMs.Value / 1000d))}s"
                : string.Empty;
            return string.IsNullOrWhiteSpace(_ownedClockSummary)
                ? $"{phaseLabel}{countdown}"
                : $"{phaseLabel}{countdown} ({_ownedClockSummary})";
        }

        private string BuildVariantActionTrailSummary()
        {
            if (_variantActionTrail.Count == 0)
            {
                return "none";
            }

            return string.Join(" => ", _variantActionTrail.Select(action => TrimVariantActionText(action, 54)));
        }

        private string BuildVariantDelegatedPacketSummary()
        {
            if (_lastVariantDelegatedPacketType < 0 || string.IsNullOrWhiteSpace(_lastVariantDelegatedOwner))
            {
                return "none";
            }

            string packetLabel = _lastVariantDelegatedRawPacket ? "raw" : "packet";
            return $"{packetLabel} {_lastVariantDelegatedPacketType} -> {_lastVariantDelegatedOwner}";
        }

        private string BuildVariantTransportSummary()
        {
            if (!ShouldTrackVariantClientOwnerAction())
            {
                return "n/a";
            }

            string summary =
                $"packets={_variantTransportPacketCount},enter={_variantEnterPacketCount},request={_variantRequestPacketCount},hud={_variantLiveHudPacketCount},member={_variantMemberPacketCount},death={_variantDeathPacketCount},result={_variantResultPacketCount},last={_variantTransportLastRoute ?? "none"}";
            if (_definition?.IsReviveMode == true)
            {
                summary += $",reviveDirect={_reviveDirectPacketCount},reviveForwarded={_reviveForwardedPacketCount},reviveRounds={_reviveRoundSequence},reviveResults={_reviveResultSequence}";
            }

            return summary;
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

        private static void ResolveTimerContractProperty(
            MapInfo mapInfo,
            WzImageProperty mapMonsterCarnivalProperty,
            int mapType,
            out WzImageProperty timerContractProperty,
            out int timerContractMapId,
            out bool usesDelegatedTimerContract)
        {
            timerContractProperty = mapMonsterCarnivalProperty;
            timerContractMapId = mapInfo?.id ?? 0;
            usesDelegatedTimerContract = false;

            if (HasAnyTimerContractValue(mapMonsterCarnivalProperty))
            {
                return;
            }

            if (!TryResolveSiblingTimerContractProperty(
                    mapInfo,
                    mapType,
                    out WzImageProperty siblingTimerContractProperty,
                    out int siblingMapId))
            {
                return;
            }

            timerContractProperty = siblingTimerContractProperty;
            timerContractMapId = siblingMapId;
            usesDelegatedTimerContract = true;
        }

        private static bool TryResolveSiblingTimerContractProperty(
            MapInfo mapInfo,
            int mapType,
            out WzImageProperty timerContractProperty,
            out int timerContractMapId)
        {
            timerContractProperty = null;
            timerContractMapId = 0;

            int mapId = mapInfo?.id ?? 0;
            if (mapId <= 0 || mapType < 0)
            {
                return false;
            }

            int candidateMapId = mapId + 100;
            WzImage candidateImage = TryResolveMapImage(mapInfo, candidateMapId);
            WzImageProperty candidateProperty = candidateImage?["monsterCarnival"];
            if (!HasAnyTimerContractValue(candidateProperty))
            {
                return false;
            }

            timerContractProperty = candidateProperty;
            timerContractMapId = candidateMapId;
            return true;
        }

        private static WzImage TryResolveMapImage(MapInfo mapInfo, int mapId)
        {
            string imageName = mapId.ToString("D9", CultureInfo.InvariantCulture) + ".img";
            if (mapInfo?.Image?.Parent is WzDirectory parentDirectory)
            {
                if (parentDirectory[imageName] is WzImage siblingImage)
                {
                    return siblingImage;
                }
            }

            if (global::HaCreator.Program.WzManager == null)
            {
                return null;
            }

            return WzInfoTools.FindMapImage(mapId.ToString(CultureInfo.InvariantCulture), global::HaCreator.Program.WzManager);
        }

        private static bool HasAnyTimerContractValue(WzImageProperty monsterCarnivalProperty)
        {
            return monsterCarnivalProperty?["timeDefault"] != null
                || monsterCarnivalProperty?["timeExpand"] != null
                || monsterCarnivalProperty?["timeMessage"] != null
                || monsterCarnivalProperty?["timeFinish"] != null;
        }

        private static string TrimVariantActionText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text[..Math.Max(1, maxLength - 3)] + "...";
        }

        private static MonsterCarnivalVariantSessionPhase ResolveDelegatedVariantPhase(int packetType)
        {
            return packetType switch
            {
                1 or 346 => MonsterCarnivalVariantSessionPhase.MemberState,
                2 or 3 or 349 or 350 => MonsterCarnivalVariantSessionPhase.Request,
                4 or 353 => MonsterCarnivalVariantSessionPhase.ResultRoute,
                5 or 351 => MonsterCarnivalVariantSessionPhase.DeathState,
                6 or 7 or 8 or 347 or 348 => MonsterCarnivalVariantSessionPhase.LiveHud,
                _ => MonsterCarnivalVariantSessionPhase.MemberState
            };
        }

        private string ResolveVariantDelegatedOwner(int packetType, bool rawPacket)
        {
            if (_definition?.IsReviveMode == true)
            {
                return packetType switch
                {
                    346 or 1 => $"{_definition.ClientOwnerLabel}::OnEnter",
                    353 or 4 => $"{_definition.ClientOwnerLabel}::OnShowGameResult",
                    _ => rawPacket ? "CField::OnPacket(raw)" : "CField::OnPacket"
                };
            }

            return packetType switch
            {
                1 or 346 => "CField_MonsterCarnival::OnEnter",
                2 or 349 => "CField_MonsterCarnival::OnRequestResult",
                3 or 350 => "CField_MonsterCarnival::OnRequestResult(reject)",
                4 or 353 => "CField_MonsterCarnival::OnShowGameResult",
                5 or 351 => "CField_MonsterCarnival::OnProcessForDeath",
                6 or 347 or 348 => "CField_MonsterCarnival::OnPersonalCP/OnTeamCP",
                7 => "CField_MonsterCarnival::OnPersonalCP/OnTeamCP(delta)",
                8 => "CField_MonsterCarnival::OnEnter/InsertSpelledData",
                352 => "CField_MonsterCarnival::OnShowMemberOutMsg",
                _ => rawPacket ? "CField::OnPacket(raw)" : "CField::OnPacket"
            };
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

        private string BuildProcessForDeathMessage(MonsterCarnivalTeam team, string characterName, int lostCp)
        {
            int normalizedLostCp = Math.Max(0, lostCp);
            MonsterCarnivalStringPoolMessage definition = normalizedLostCp > 0
                ? new(0x1019, "[%s] has become unable to fight and [%s]team has lost %d CP.")
                : new(0x101A, "[%s] has become unable to fight but [%s] has no CP so [%s] team did not lose any CP");
            string normalizedName = string.IsNullOrWhiteSpace(characterName) ? "A party member" : characterName.Trim();
            string teamName = FormatTeam(team);
            string fallback = normalizedLostCp > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} of {1} was defeated. {2} CP lost.", normalizedName, teamName, normalizedLostCp)
                : string.Format(CultureInfo.InvariantCulture, "{0} of {1} was defeated but no CP was lost.", normalizedName, teamName);
            return normalizedLostCp > 0
                ? FormatStringPoolMessage(definition, fallback, normalizedName, ResolveTeamLabel(team), normalizedLostCp)
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
