using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    public sealed class MonsterCarnivalFieldDefinition
    {
        public int MapId { get; init; }
        public FieldType FieldType { get; init; }
        public int DefaultTimeSeconds { get; init; }
        public int ExpandTimeSeconds { get; init; }
        public int MessageTimeSeconds { get; init; }
        public int FinishTimeSeconds { get; init; }
        public int DeathCp { get; init; }
        public int RewardMapWin { get; init; }
        public int RewardMapLose { get; init; }
        public int MobGenMax { get; init; }
        public int GuardianGenMax { get; init; }
        public string EffectWin { get; init; }
        public string EffectLose { get; init; }
        public string SoundWin { get; init; }
        public string SoundLose { get; init; }
        public IReadOnlyList<MonsterCarnivalEntry> MobEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public IReadOnlyList<MonsterCarnivalEntry> SkillEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public IReadOnlyList<MonsterCarnivalEntry> GuardianEntries { get; init; } = Array.Empty<MonsterCarnivalEntry>();
        public bool IsReviveMode => FieldType == FieldType.FIELDTYPE_MONSTERCARNIVALREVIVE;

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
                DefaultTimeSeconds = ReadInt(property["timeDefault"]),
                ExpandTimeSeconds = ReadInt(property["timeExpand"]),
                MessageTimeSeconds = ReadInt(property["timeMessage"]),
                FinishTimeSeconds = ReadInt(property["timeFinish"]),
                DeathCp = ReadInt(property["deathCP"]),
                RewardMapWin = ReadInt(property["rewardMapWin"]),
                RewardMapLose = ReadInt(property["rewardMapLose"]),
                MobGenMax = ReadInt(property["mobGenMax"]),
                GuardianGenMax = ReadInt(property["guardianGenMax"]),
                EffectWin = ReadString(property["effectWin"]),
                EffectLose = ReadString(property["effectLose"]),
                SoundWin = ReadString(property["soundWin"]),
                SoundLose = ReadString(property["soundLose"]),
                MobEntries = LoadMobEntries(property["mob"]),
                SkillEntries = LoadNamedEntries(property["skill"], MonsterCarnivalTab.Skill, McSkillImageName),
                GuardianEntries = LoadNamedEntries(property["guardian"], MonsterCarnivalTab.Guardian, McGuardianImageName)
            };
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
        private const int StatusDurationMs = 4500;

        private readonly Dictionary<int, int> _mobSpellCounts = new();
        private readonly Dictionary<int, int> _skillUseCounts = new();
        private readonly Dictionary<int, int> _guardianCounts = new();
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
        private int _lastRequestTab;
        private int _lastRequestIndex;
        private bool _isVisible;
        private bool _enteredField;

        public bool IsVisible => _isVisible;
        public bool IsEntered => _enteredField;
        public MonsterCarnivalFieldDefinition Definition => _definition;
        public MonsterCarnivalTeam LocalTeam => _localTeam;
        public MonsterCarnivalTab ActiveTab => _activeTab;
        public int PersonalCp => _personalCp;
        public int PersonalTotalCp => _personalTotalCp;
        public MonsterCarnivalTeamState Team0 => _team0;
        public MonsterCarnivalTeamState Team1 => _team1;
        public IReadOnlyDictionary<int, int> MobSpellCounts => _mobSpellCounts;

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

            _enteredField = true;
            _localTeam = localTeam;
            _personalCp = Math.Max(0, personalCp);
            _personalTotalCp = Math.Max(_personalCp, personalTotalCp);

            MonsterCarnivalTeamState myTeam = GetTeamState(localTeam);
            MonsterCarnivalTeamState enemyTeam = GetTeamState(GetOpposingTeam(localTeam));
            myTeam.CurrentCp = Math.Max(0, myTeamCp);
            myTeam.TotalCp = Math.Max(myTeam.CurrentCp, myTeamTotalCp);
            enemyTeam.CurrentCp = Math.Max(0, enemyTeamCp);
            enemyTeam.TotalCp = Math.Max(enemyTeam.CurrentCp, enemyTeamTotalCp);

            ShowStatus($"Entered Monster Carnival as {FormatTeam(localTeam)}.", Environment.TickCount);
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

            ShowStatus(
                $"Monster Carnival CP delta applied: personal {FormatSignedDelta(personalCpDelta)}, team0 {FormatSignedDelta(team0CurrentCpDelta)}, team1 {FormatSignedDelta(team1CurrentCpDelta)}.",
                tickCount);
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

            SetEntryCount(_mobSpellCounts, entry.Id, count);
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

        public void OnRequestResult(byte tabCode, int entryIndex, string requestMessage, int tickCount)
        {
            MonsterCarnivalTab tab = TryParseTab(tabCode, out MonsterCarnivalTab parsedTab)
                ? parsedTab
                : _activeTab;
            MonsterCarnivalEntry entry = GetEntry(tab, entryIndex);
            if (entry == null)
            {
                string fallbackMessage = string.IsNullOrWhiteSpace(requestMessage)
                    ? $"Monster Carnival request result received for tab {(int)tab}, index {entryIndex}."
                    : requestMessage.Trim();
                ShowStatus(fallbackMessage, tickCount);
                return;
            }

            ApplySuccessfulRequest(entry);
            string successMessage = string.IsNullOrWhiteSpace(requestMessage)
                ? BuildRequestSuccessMessage(entry)
                : requestMessage.Trim();
            ShowStatus(successMessage, tickCount);
        }

        public void OnRequestFailure(int reasonCode, int tickCount)
        {
            ShowStatus(DescribeRequestFailure(reasonCode), tickCount);
        }

        public void OnShowGameResult(int resultCode, int tickCount)
        {
            ShowStatus(DescribeGameResult(resultCode), tickCount);
        }

        public void OnProcessForDeath(MonsterCarnivalTeam team, string characterName, int remainingRevives, int tickCount)
        {
            if (!_isVisible)
            {
                return;
            }

            int deathCp = Math.Max(0, _definition?.DeathCp ?? 0);
            MonsterCarnivalTeamState teamState = GetTeamState(team);
            teamState.CurrentCp = Math.Max(0, teamState.CurrentCp - deathCp);

            string normalizedName = string.IsNullOrWhiteSpace(characterName) ? "A party member" : characterName.Trim();
            string reviveSummary = remainingRevives > 0
                ? $"{remainingRevives} revive(s) remaining."
                : "No revives remaining.";
            string cpSummary = deathCp > 0
                ? $" {deathCp} CP was removed from {FormatTeam(team)}."
                : string.Empty;

            ShowStatus($"{normalizedName} of {FormatTeam(team)} was defeated. {reviveSummary}{cpSummary}".Trim(), tickCount);
        }

        public bool TryApplyPacket(MonsterCarnivalPacketType packetType, byte[] payload, int currentTimeMs, out string errorMessage)
        {
            errorMessage = null;

            if (!_isVisible)
            {
                errorMessage = "Monster Carnival runtime inactive.";
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
                            SetEntryCount(_mobSpellCounts, _definition.MobEntries[i].Id, reader.ReadByte());
                        }

                        EnsurePacketConsumed(stream, "enter");
                        return true;

                    case MonsterCarnivalPacketType.RequestResult:
                        OnRequestResult(reader.ReadByte(), reader.ReadByte(), ReadPacketString(reader), currentTimeMs);
                        EnsurePacketConsumed(stream, "request-result");
                        return true;

                    case MonsterCarnivalPacketType.RequestFailure:
                        OnRequestFailure(reader.ReadByte(), currentTimeMs);
                        EnsurePacketConsumed(stream, "request-failure");
                        return true;

                    case MonsterCarnivalPacketType.GameResult:
                        OnShowGameResult(reader.ReadByte(), currentTimeMs);
                        EnsurePacketConsumed(stream, "game-result");
                        return true;

                    case MonsterCarnivalPacketType.ProcessForDeath:
                        OnProcessForDeath((MonsterCarnivalTeam)reader.ReadByte(), ReadPacketString(reader), reader.ReadByte(), currentTimeMs);
                        EnsurePacketConsumed(stream, "process-for-death");
                        return true;

                    case MonsterCarnivalPacketType.CpUpdate:
                        UpdateTeamCp(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16());
                        EnsurePacketConsumed(stream, "cp-update");
                        return true;

                    case MonsterCarnivalPacketType.CpDelta:
                        ApplyTeamCpDelta(reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), currentTimeMs);
                        EnsurePacketConsumed(stream, "cp-delta");
                        return true;

                    case MonsterCarnivalPacketType.SummonedMobCount:
                        if (!TrySetMobSpellCount(reader.ReadByte(), reader.ReadByte(), out errorMessage))
                        {
                            return false;
                        }

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
            int panelWidth = 368;
            int panelX = viewport.Width - panelWidth - 18;
            int panelY = 18;
            int panelHeight = 380;

            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(16, 22, 28, 225));
            spriteBatch.Draw(pixelTexture, new Rectangle(panelX, panelY, panelWidth, 34), new Color(54, 76, 98, 255));
            DrawShadowedText(spriteBatch, font, "Monster Carnival", new Vector2(panelX + 12, panelY + 8), Color.White);

            int timerY = panelY + 44;
            string headerText = _definition == null
                ? "No WZ carnival definition loaded."
                : $"Time {_definition.DefaultTimeSeconds}s +{_definition.ExpandTimeSeconds}s | Death CP {_definition.DeathCp} | {(_definition.IsReviveMode ? "Revive" : "Standard")}";
            DrawShadowedText(spriteBatch, font, headerText, new Vector2(panelX + 12, timerY), Color.Gainsboro, 0.85f);

            DrawCpRow(spriteBatch, pixelTexture, font, panelX + 12, panelY + 76, panelWidth - 24);
            DrawTabs(spriteBatch, pixelTexture, font, panelX + 12, panelY + 130, panelWidth - 24);
            DrawEntryList(spriteBatch, pixelTexture, font, panelX + 12, panelY + 164, panelWidth - 24, 154);
            DrawFooter(spriteBatch, pixelTexture, font, panelX + 12, panelY + 324, panelWidth - 24, 44);
        }

        public string DescribeStatus()
        {
            if (!_isVisible)
            {
                return "Monster Carnival runtime is inactive on this map.";
            }

            return $"Monster Carnival: {(_enteredField ? "entered" : "configured")} | mode={(_definition?.IsReviveMode == true ? "revive" : "standard")} | tab={_activeTab} | personalCP={_personalCp}/{_personalTotalCp} | team0={_team0.CurrentCp}/{_team0.TotalCp} | team1={_team1.CurrentCp}/{_team1.TotalCp} | mobs={GetTotalCount(_mobSpellCounts)}/{Math.Max(0, _definition?.MobGenMax ?? 0)} | guardians={GetTotalCount(_guardianCounts)}/{Math.Max(0, _definition?.GuardianGenMax ?? 0)}";
        }

        public void Reset()
        {
            _definition = null;
            _mobSpellCounts.Clear();
            _skillUseCounts.Clear();
            _guardianCounts.Clear();
            _team0.CurrentCp = 0;
            _team0.TotalCp = 0;
            _team1.CurrentCp = 0;
            _team1.TotalCp = 0;
            _localTeam = MonsterCarnivalTeam.Team0;
            _activeTab = MonsterCarnivalTab.Mob;
            _statusMessage = null;
            _statusMessageUntil = 0;
            _selectedEntryIndex = 0;
            _personalCp = 0;
            _personalTotalCp = 0;
            _lastRequestTab = -1;
            _lastRequestIndex = -1;
            _isVisible = false;
            _enteredField = false;
        }

        private void DrawCpRow(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width)
        {
            int rowHeight = 44;
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, rowHeight), new Color(26, 34, 44, 220));
            string personalText = $"Personal CP  {_personalCp}/{_personalTotalCp}";
            DrawShadowedText(spriteBatch, font, personalText, new Vector2(x + 10, y + 6), Color.White);

            string team0Text = $"Team 0  {_team0.CurrentCp}/{_team0.TotalCp}";
            string team1Text = $"Team 1  {_team1.CurrentCp}/{_team1.TotalCp}";
            DrawShadowedText(spriteBatch, font, team0Text, new Vector2(x + 10, y + 24), new Color(128, 191, 255));
            DrawShadowedText(spriteBatch, font, team1Text, new Vector2(x + width / 2 + 4, y + 24), new Color(255, 153, 153));
        }

        private void DrawTabs(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width)
        {
            int tabWidth = width / 3;
            DrawTab(spriteBatch, pixelTexture, font, x, y, tabWidth, "Mob", MonsterCarnivalTab.Mob);
            DrawTab(spriteBatch, pixelTexture, font, x + tabWidth, y, tabWidth, "Skill", MonsterCarnivalTab.Skill);
            DrawTab(spriteBatch, pixelTexture, font, x + tabWidth * 2, y, width - tabWidth * 2, "Guardian", MonsterCarnivalTab.Guardian);
        }

        private void DrawTab(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, string label, MonsterCarnivalTab tab)
        {
            Color background = _activeTab == tab
                ? new Color(71, 103, 140, 255)
                : new Color(31, 42, 56, 255);
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width - 1, 26), background);
            Vector2 size = font.MeasureString(label);
            Vector2 position = new Vector2(x + (width - size.X) * 0.5f, y + 4);
            DrawShadowedText(spriteBatch, font, label, position, Color.White);
        }

        private void DrawEntryList(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, int height)
        {
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, height), new Color(20, 27, 34, 200));
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
        }

        private void DrawFooter(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int x, int y, int width, int height)
        {
            spriteBatch.Draw(pixelTexture, new Rectangle(x, y, width, height), new Color(26, 34, 44, 220));
            string status = _statusMessage;
            if (string.IsNullOrWhiteSpace(status))
            {
                MonsterCarnivalEntry selected = GetEntry(_activeTab, _selectedEntryIndex);
                status = selected?.Description;
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                status = _enteredField
                    ? "Use /mcarnival request, requestok, cpdelta, or death to drive the Monster Carnival runtime."
                    : "Use /mcarnival enter ... to populate the Carnival HUD like CField_MonsterCarnival::OnEnter.";
            }

            DrawShadowedText(spriteBatch, font, status, new Vector2(x + 8, y + 8), Color.Gainsboro, 0.8f);
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
                reasonCode = 4;
                return false;
            }

            return true;
        }

        private void ApplySuccessfulRequest(MonsterCarnivalEntry entry)
        {
            _activeTab = entry.Tab;
            _selectedEntryIndex = entry.Index;
            _lastRequestTab = (int)entry.Tab;
            _lastRequestIndex = entry.Index;

            _personalCp = Math.Max(0, _personalCp - entry.Cost);
            MonsterCarnivalTeamState teamState = GetTeamState(_localTeam);
            teamState.CurrentCp = Math.Max(0, teamState.CurrentCp - entry.Cost);

            Dictionary<int, int> counts = GetCountDictionary(entry.Tab);
            SetEntryCount(counts, entry.Id, GetEntryCount(counts, entry.Id) + 1);
        }

        private int GetEntryUsageCount(MonsterCarnivalEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            return GetEntryCount(GetCountDictionary(entry.Tab), entry.Id);
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

        private string BuildRequestSuccessMessage(MonsterCarnivalEntry entry)
        {
            if (entry == null)
            {
                return "Monster Carnival request completed.";
            }

            return entry.Tab switch
            {
                MonsterCarnivalTab.Mob => $"Summoned {entry.Name}.",
                MonsterCarnivalTab.Skill => $"Activated {entry.Name}.",
                MonsterCarnivalTab.Guardian => $"Placed {entry.Name}.",
                _ => $"Requested {entry.Name}."
            };
        }

        private static string DescribeRequestFailure(int reasonCode)
        {
            return reasonCode switch
            {
                1 => "Monster Carnival request rejected: not enough CP.",
                2 => "Monster Carnival request rejected: that action is not available right now.",
                3 => "Monster Carnival request rejected: the monster summon limit has been reached.",
                4 => "Monster Carnival request rejected: the guardian limit has been reached.",
                5 => "Monster Carnival request rejected: that guardian slot is already occupied.",
                _ => $"Monster Carnival request rejected (reason {reasonCode})."
            };
        }

        private string DescribeGameResult(int resultCode)
        {
            string resultText = resultCode switch
            {
                8 => _definition?.IsReviveMode == true ? "Monster Carnival revive round ended in victory." : "Monster Carnival round ended in victory.",
                9 => _definition?.IsReviveMode == true ? "Monster Carnival revive round ended in defeat." : "Monster Carnival round ended in defeat.",
                10 => _definition?.IsReviveMode == true ? "Monster Carnival revive round ended in a draw." : "Monster Carnival round ended in a draw.",
                11 => _definition?.IsReviveMode == true ? "Monster Carnival revive round ended." : "Monster Carnival round ended.",
                _ => $"Monster Carnival result packet {resultCode} received."
            };

            if (_definition == null)
            {
                return resultText;
            }

            return resultCode switch
            {
                8 => $"{resultText} Reward map {_definition.RewardMapWin}.",
                9 => $"{resultText} Reward map {_definition.RewardMapLose}.",
                _ => resultText
            };
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

        private void ShowStatus(string message, int tickCount)
        {
            _statusMessage = message;
            _statusMessageUntil = tickCount + StatusDurationMs;
        }

        private static string FormatTeam(MonsterCarnivalTeam team)
        {
            return team == MonsterCarnivalTeam.Team0 ? "Team 0" : "Team 1";
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
