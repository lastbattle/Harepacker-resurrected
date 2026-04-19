using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class MonsterBookManager
    {
        public enum CardPickupOutcome
        {
            None,
            Recorded,
            AlreadyFull
        }

        public readonly record struct CardPickupResult(
            CardPickupOutcome Outcome,
            MonsterBookSnapshot Snapshot,
            string MonsterName,
            int PreviousCopies,
            int CurrentCopies)
        {
            public bool Changed => CurrentCopies != PreviousCopies;
        }

        private sealed class PersistedStore
        {
            public Dictionary<string, MonsterBookRecord> BookByCharacter { get; set; } = new(StringComparer.Ordinal);
        }

        private sealed class MonsterBookRecord
        {
            public Dictionary<int, int> CardCountsByMob { get; set; } = new();
            public HashSet<int> RegisteredMobIds { get; set; } = new();
        }

        private sealed class MonsterBookCardDefinition
        {
            public int CardItemId { get; init; }
            public string CardItemName { get; init; } = string.Empty;
            public int MobId { get; init; }
            public bool Only { get; init; }
            public bool ConsumeOnPickup { get; init; }
            public bool IsClientConsumedOnPickupCard => Only && ConsumeOnPickup;
            public string Name { get; init; } = string.Empty;
            public int Level { get; init; }
            public int MaxHp { get; init; }
            public int MaxMp { get; init; }
            public int Exp { get; init; }
            public bool IsBoss { get; init; }
            public string EpisodeText { get; init; } = string.Empty;
            public IReadOnlyList<int> RewardItemIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<string> RewardLines { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> HabitatLines { get; init; } = Array.Empty<string>();
            public string SearchText { get; init; } = string.Empty;
        }

        private sealed class MonsterBookStringEntry
        {
            public string EpisodeText { get; init; } = string.Empty;
            public IReadOnlyList<int> RewardItemIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> MapIds { get; init; } = Array.Empty<int>();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, MonsterBookRecord> _bookByCharacter = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;
        private readonly object _catalogLock = new();
        private const int CardsPerPage = 25;
        private const int ChapterItemStride = 1000;
        private const int FirstChapterCardItemId = 2380000;
        private const int MaximumGradeCount = 9;
        private List<MonsterBookCardDefinition> _catalog;
        private Dictionary<int, MonsterBookCardDefinition> _catalogByMobId;
        private Dictionary<int, MonsterBookCardDefinition> _catalogByItemId;

        public MonsterBookManager(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "monster-book.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public MonsterBookSnapshot GetSnapshot(CharacterBuild build)
        {
            return GetSnapshot(build, build?.Id ?? 0, build?.Name);
        }

        public MonsterBookSnapshot GetSnapshot(CharacterBuild build, int characterId, string characterName)
        {
            IReadOnlyList<MonsterBookCardDefinition> catalog = EnsureCatalog();
            MonsterBookRecord record = GetRecord(build, characterId, characterName, createIfMissing: false);
            IReadOnlyDictionary<int, int> counts = record?.CardCountsByMob ?? new Dictionary<int, int>();
            int registeredMobId = ResolveRegisteredMobId(record);

            List<MonsterBookCardSnapshot> cards = catalog
                .Select((definition, index) =>
                {
                    counts.TryGetValue(definition.MobId, out int ownedCopies);
                    int gradeIndex = Math.Clamp(definition.CardItemId / ChapterItemStride - (FirstChapterCardItemId / ChapterItemStride), 0, MaximumGradeCount - 1);
                    return new MonsterBookCardSnapshot
                    {
                        CardItemId = definition.CardItemId,
                        CardItemName = definition.CardItemName,
                        MobId = definition.MobId,
                        GradeIndex = gradeIndex,
                        GradeLabel = BuildGradeLabel(gradeIndex),
                        Name = definition.Name,
                        Level = definition.Level,
                        MaxHp = definition.MaxHp,
                        MaxMp = definition.MaxMp,
                        Exp = definition.Exp,
                        IsBoss = definition.IsBoss,
                        OwnedCopies = Math.Clamp(ownedCopies, 0, 5),
                        IsRegistered = registeredMobId > 0 && definition.MobId == registeredMobId,
                        EpisodeText = definition.EpisodeText,
                        RewardLines = definition.RewardLines,
                        HabitatLines = definition.HabitatLines,
                        SearchText = definition.SearchText
                    };
                })
                .ToList();

            ILookup<int, MonsterBookCardSnapshot> cardsByGrade = cards
                .ToLookup(card => Math.Clamp(card.GradeIndex, 0, MaximumGradeCount - 1));

            List<MonsterBookGradeSnapshot> grades = Enumerable.Range(0, MaximumGradeCount)
                .Select(gradeIndex =>
                {
                    List<MonsterBookCardSnapshot> gradeCards = cardsByGrade[gradeIndex].ToList();
                    return new MonsterBookGradeSnapshot
                    {
                        GradeIndex = gradeIndex,
                        Label = BuildGradeLabel(gradeIndex),
                        CardTypeCount = gradeCards.Count,
                        OwnedCardTypes = gradeCards.Count(card => card.IsDiscovered),
                        CompletedCardTypes = gradeCards.Count(card => card.IsCompleted),
                        Pages = gradeCards
                            .Chunk(CardsPerPage)
                            .Select((pageChunk, pageIndex) => new MonsterBookPageSnapshot
                            {
                                PageIndex = pageIndex,
                                Title = $"Page {pageIndex + 1}",
                                Subtitle = BuildPageSubtitle(pageChunk),
                                Cards = pageChunk.ToList()
                            })
                            .ToList()
                    };
                })
                .ToList();

            List<MonsterBookPageSnapshot> pages = grades
                .SelectMany(grade => grade.Pages)
                .Select((page, pageIndex) => new MonsterBookPageSnapshot
                {
                    PageIndex = pageIndex,
                    Title = page.Title,
                    Subtitle = page.Subtitle,
                    Cards = page.Cards
                })
                .ToList();

            int ownedCardTypes = cards.Count(card => card.IsDiscovered);
            int completedCardTypes = cards.Count(card => card.IsCompleted);
            int ownedBossCardTypes = cards.Count(card => card.IsDiscovered && card.IsBoss);
            int ownedNormalCardTypes = cards.Count(card => card.IsDiscovered && !card.IsBoss);
            int totalOwnedCopies = cards.Sum(card => card.OwnedCopies);
            MonsterBookCardSnapshot registeredCard = registeredMobId > 0
                ? cards.FirstOrDefault(card => card.MobId == registeredMobId)
                : null;

            return new MonsterBookSnapshot
            {
                Title = "Monster Book",
                Subtitle = build == null && characterId <= 0 && string.IsNullOrWhiteSpace(characterName)
                    ? "Monster card ownership is unavailable because there is no active character build."
                    : "Card ownership is persisted per character and built from the WZ-backed monster-card catalog, with chapter tabs derived from the client's 0238xxxx card buckets, client-shaped right-tab detail panes, a singular local registered-card slot, pickup-backed consume-on-pickup Monster Card ownership, and WZ-backed reward/habitat naming.",
                StatusText = "The Monster Book owner now routes the overview tab, the nine chapter tabs, the four right-tab detail panes, local search focus and cycling, and register or release context actions through the dedicated Monster Book runtime. Ownership now follows picked-up consume-on-pickup Monster Card drops sourced from the WZ-backed card catalog, while packet-authored save flow and real server drop authorship still remain outside this simulator runtime.",
                TotalCardTypes = cards.Count,
                OwnedCardTypes = ownedCardTypes,
                CompletedCardTypes = completedCardTypes,
                OwnedBossCardTypes = ownedBossCardTypes,
                OwnedNormalCardTypes = ownedNormalCardTypes,
                TotalOwnedCopies = totalOwnedCopies,
                RegisteredCardMobId = registeredCard?.MobId ?? 0,
                RegisteredCardItemId = registeredCard?.CardItemId ?? 0,
                RegisteredCardName = registeredCard?.Name ?? string.Empty,
                Grades = grades,
                Pages = pages
            };
        }

        public MonsterBookSnapshot RecordMobKill(CharacterBuild build, int mobId, int count = 1, bool persistToDisk = true)
        {
            return RecordMobKill(build, build?.Id ?? 0, build?.Name, mobId, count, persistToDisk);
        }

        public MonsterBookSnapshot RecordMobKill(CharacterBuild build, int characterId, string characterName, int mobId, int count = 1, bool persistToDisk = true)
        {
            if (mobId <= 0 || count <= 0)
            {
                return GetSnapshot(build, characterId, characterName);
            }

            Dictionary<int, MonsterBookCardDefinition> catalogByMobId = EnsureCatalogByMobId();
            if (!catalogByMobId.TryGetValue(mobId, out MonsterBookCardDefinition definition) || definition == null)
            {
                return GetSnapshot(build, characterId, characterName);
            }

            MonsterBookRecord record = GetRecord(build, characterId, characterName, createIfMissing: true);
            record.CardCountsByMob.TryGetValue(definition.MobId, out int currentCount);
            int nextCount = Math.Clamp(currentCount + count, 0, 5);
            if (nextCount == currentCount)
            {
                return GetSnapshot(build, characterId, characterName);
            }

            record.CardCountsByMob[definition.MobId] = nextCount;
            if (persistToDisk)
            {
                SaveToDisk();
            }

            return GetSnapshot(build, characterId, characterName);
        }

        public MonsterBookSnapshot RecordCardPickup(CharacterBuild build, int itemId, int count = 1)
        {
            return RecordCardPickupWithResult(build, itemId, count).Snapshot;
        }

        public CardPickupResult RecordCardPickupWithResult(CharacterBuild build, int itemId, int count = 1, bool persistToDisk = true)
        {
            return RecordCardPickupWithResult(build, build?.Id ?? 0, build?.Name, itemId, count, persistToDisk);
        }

        public MonsterBookSnapshot RecordCardPickup(CharacterBuild build, int characterId, string characterName, int itemId, int count = 1)
        {
            return RecordCardPickupWithResult(build, characterId, characterName, itemId, count).Snapshot;
        }

        public CardPickupResult RecordCardPickupWithResult(CharacterBuild build, int characterId, string characterName, int itemId, int count = 1, bool persistToDisk = true)
        {
            if (itemId <= 0 || count <= 0)
            {
                return new CardPickupResult(
                    CardPickupOutcome.None,
                    GetSnapshot(build, characterId, characterName),
                    string.Empty,
                    0,
                    0);
            }

            if (!EnsureCatalogByItemId().TryGetValue(itemId, out MonsterBookCardDefinition definition) || definition == null)
            {
                return new CardPickupResult(
                    CardPickupOutcome.None,
                    GetSnapshot(build, characterId, characterName),
                    string.Empty,
                    0,
                    0);
            }

            int pickupCount = ResolveCardPickupCopyCount(count, definition.IsClientConsumedOnPickupCard);
            if (pickupCount <= 0)
            {
                return new CardPickupResult(
                    CardPickupOutcome.None,
                    GetSnapshot(build, characterId, characterName),
                    definition.Name,
                    0,
                    0);
            }

            MonsterBookRecord record = GetRecord(build, characterId, characterName, createIfMissing: true);
            record.CardCountsByMob.TryGetValue(definition.MobId, out int previousCopies);
            MonsterBookSnapshot snapshot = RecordMobKill(build, characterId, characterName, definition.MobId, pickupCount, persistToDisk);
            int currentCopies = Math.Clamp(previousCopies + pickupCount, 0, 5);
            if (currentCopies == previousCopies)
            {
                currentCopies = previousCopies;
            }

            CardPickupOutcome outcome = currentCopies > previousCopies
                ? CardPickupOutcome.Recorded
                : previousCopies >= 5
                    ? CardPickupOutcome.AlreadyFull
                    : CardPickupOutcome.None;

            return new CardPickupResult(
                outcome,
                snapshot,
                definition.Name,
                previousCopies,
                currentCopies);
        }

        internal static int ResolveCardPickupCopyCount(int requestedCount, bool consumeOnPickup)
        {
            int normalizedCount = Math.Max(0, requestedCount);
            if (normalizedCount <= 0)
            {
                return 0;
            }

            // WZ `Item/Consume/0238.img/*/info/only = 1` and
            // `spec/consumeOnPickup = 1` make Monster Cards single-copy intake.
            return consumeOnPickup ? 1 : normalizedCount;
        }

        public bool TryResolveCardItemId(int mobId, out int cardItemId)
        {
            cardItemId = 0;
            if (mobId <= 0)
            {
                return false;
            }

            if (!EnsureCatalogByMobId().TryGetValue(mobId, out MonsterBookCardDefinition definition) || definition == null)
            {
                return false;
            }

            cardItemId = definition.CardItemId;
            return cardItemId > 0;
        }

        public bool TryResolveRewardItemIds(int mobId, out IReadOnlyList<int> rewardItemIds)
        {
            rewardItemIds = Array.Empty<int>();
            if (mobId <= 0)
            {
                return false;
            }

            if (!EnsureCatalogByMobId().TryGetValue(mobId, out MonsterBookCardDefinition definition)
                || definition?.RewardItemIds == null
                || definition.RewardItemIds.Count == 0)
            {
                return false;
            }

            rewardItemIds = definition.RewardItemIds;
            return true;
        }

        public bool IsConsumeOnPickupCardItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            return EnsureCatalogByItemId().TryGetValue(itemId, out MonsterBookCardDefinition definition)
                && definition?.IsClientConsumedOnPickupCard == true;
        }

        public MonsterBookSnapshot SetRegisteredCard(CharacterBuild build, int mobId, bool registered, bool persistToDisk = true)
        {
            return SetRegisteredCard(build, build?.Id ?? 0, build?.Name, mobId, registered, persistToDisk);
        }

        public MonsterBookSnapshot SetRegisteredCard(CharacterBuild build, int characterId, string characterName, int mobId, bool registered, bool persistToDisk = true)
        {
            if (mobId <= 0 || !EnsureCatalogByMobId().ContainsKey(mobId))
            {
                return GetSnapshot(build, characterId, characterName);
            }

            MonsterBookRecord record = GetRecord(build, characterId, characterName, createIfMissing: true);
            record.RegisteredMobIds ??= new HashSet<int>();

            if (registered
                && (!record.CardCountsByMob.TryGetValue(mobId, out int ownedCopies)
                    || ownedCopies <= 0))
            {
                return GetSnapshot(build, characterId, characterName);
            }

            bool changed = false;
            if (registered)
            {
                if (record.RegisteredMobIds.Count != 1 || !record.RegisteredMobIds.Contains(mobId))
                {
                    record.RegisteredMobIds.Clear();
                    changed = record.RegisteredMobIds.Add(mobId);
                }
            }
            else
            {
                changed = record.RegisteredMobIds.Remove(mobId);
            }

            if (changed && persistToDisk)
            {
                SaveToDisk();
            }

            return GetSnapshot(build, characterId, characterName);
        }

        public MonsterBookSnapshot ApplyOwnershipSync(
            CharacterBuild build,
            int characterId,
            string characterName,
            IReadOnlyDictionary<int, int> cardCountsByMob,
            int registeredMobId = 0,
            bool replaceExisting = true)
        {
            MonsterBookRecord record = GetRecord(build, characterId, characterName, createIfMissing: true);
            Dictionary<int, MonsterBookCardDefinition> catalogByMobId = EnsureCatalogByMobId();

            Dictionary<int, int> normalizedCounts = new();
            if (cardCountsByMob != null)
            {
                foreach (KeyValuePair<int, int> entry in cardCountsByMob)
                {
                    if (entry.Key <= 0
                        || entry.Value <= 0
                        || !catalogByMobId.ContainsKey(entry.Key))
                    {
                        continue;
                    }

                    normalizedCounts[entry.Key] = Math.Clamp(entry.Value, 0, 5);
                }
            }

            bool changed = false;
            if (replaceExisting)
            {
                if (record.CardCountsByMob.Count != normalizedCounts.Count
                    || record.CardCountsByMob.Any(pair => !normalizedCounts.TryGetValue(pair.Key, out int syncedValue) || syncedValue != pair.Value))
                {
                    record.CardCountsByMob.Clear();
                    foreach (KeyValuePair<int, int> entry in normalizedCounts)
                    {
                        record.CardCountsByMob[entry.Key] = entry.Value;
                    }

                    changed = true;
                }
            }
            else
            {
                foreach (KeyValuePair<int, int> entry in normalizedCounts)
                {
                    if (!record.CardCountsByMob.TryGetValue(entry.Key, out int existingValue)
                        || existingValue != entry.Value)
                    {
                        record.CardCountsByMob[entry.Key] = entry.Value;
                        changed = true;
                    }
                }
            }

            int normalizedRegisteredMobId = registeredMobId > 0
                && record.CardCountsByMob.TryGetValue(registeredMobId, out int ownedCopies)
                && ownedCopies > 0
                ? registeredMobId
                : 0;
            int previousRegisteredMobId = ResolveRegisteredMobId(record);
            if (normalizedRegisteredMobId > 0)
            {
                if (record.RegisteredMobIds.Count != 1 || !record.RegisteredMobIds.Contains(normalizedRegisteredMobId))
                {
                    record.RegisteredMobIds.Clear();
                    record.RegisteredMobIds.Add(normalizedRegisteredMobId);
                    changed = true;
                }
            }
            else if (record.RegisteredMobIds.Count > 0)
            {
                record.RegisteredMobIds.Clear();
                changed = true;
            }

            if (!changed && previousRegisteredMobId != ResolveRegisteredMobId(record))
            {
                changed = true;
            }

            if (changed)
            {
                SaveToDisk();
            }

            return GetSnapshot(build, characterId, characterName);
        }

        private static string BuildPageSubtitle(IEnumerable<MonsterBookCardSnapshot> cards)
        {
            List<MonsterBookCardSnapshot> pageCards = cards?.ToList() ?? new List<MonsterBookCardSnapshot>();
            if (pageCards.Count == 0)
            {
                return "No cards are available on this page.";
            }

            MonsterBookCardSnapshot firstCard = pageCards[0];
            MonsterBookCardSnapshot lastCard = pageCards[^1];
            return string.Equals(firstCard.Name, lastCard.Name, StringComparison.Ordinal)
                ? firstCard.Name
                : $"{firstCard.Name} to {lastCard.Name}";
        }

        private static string BuildGradeLabel(int gradeIndex)
        {
            return $"Chapter {gradeIndex + 1}";
        }

        private IReadOnlyList<MonsterBookCardDefinition> EnsureCatalog()
        {
            if (_catalog != null)
            {
                return _catalog;
            }

            lock (_catalogLock)
            {
                if (_catalog != null)
                {
                    return _catalog;
                }

                _catalog = LoadCatalog();
                _catalogByMobId = _catalog
                    .GroupBy(card => card.MobId)
                    .ToDictionary(group => group.Key, group => group.First(), EqualityComparer<int>.Default);
                _catalogByItemId = _catalog
                    .GroupBy(card => card.CardItemId)
                    .ToDictionary(group => group.Key, group => group.First(), EqualityComparer<int>.Default);
                return _catalog;
            }
        }

        private Dictionary<int, MonsterBookCardDefinition> EnsureCatalogByMobId()
        {
            EnsureCatalog();
            return _catalogByMobId ?? new Dictionary<int, MonsterBookCardDefinition>();
        }

        private Dictionary<int, MonsterBookCardDefinition> EnsureCatalogByItemId()
        {
            EnsureCatalog();
            return _catalogByItemId ?? new Dictionary<int, MonsterBookCardDefinition>();
        }

        private static List<MonsterBookCardDefinition> LoadCatalog()
        {
            List<MonsterBookCardDefinition> definitions = new();

            try
            {
                WzImage itemImage = global::HaCreator.Program.FindImage("Item", "Consume/0238.img");
                if (itemImage == null)
                {
                    return definitions;
                }

                if (!itemImage.Parsed)
                {
                    itemImage.ParseImage();
                }

                foreach (WzImageProperty property in itemImage.WzProperties)
                {
                    if (property is not WzSubProperty cardProperty)
                    {
                        continue;
                    }

                    int cardItemId = ParseInt(cardProperty.Name);
                    if (cardItemId <= 0)
                    {
                        continue;
                    }

                    WzSubProperty infoProperty = cardProperty["info"] as WzSubProperty;
                    WzSubProperty specProperty = cardProperty["spec"] as WzSubProperty;
                    bool only = ReadInt(infoProperty?["only"]) != 0;
                    bool consumeOnPickup = ReadInt(specProperty?["consumeOnPickup"]) != 0;
                    int mobId = ReadInt(infoProperty?["mob"]);
                    if (mobId <= 0)
                    {
                        continue;
                    }

                    ResolveMobMetadata(
                        mobId,
                        out string mobName,
                        out int level,
                        out int maxHp,
                        out int maxMp,
                        out int exp,
                        out bool isBoss,
                        out string episodeText,
                        out IReadOnlyList<int> rewardItemIds,
                        out IReadOnlyList<string> rewardLines,
                        out IReadOnlyList<string> habitatLines);
                    string cardItemName = ResolveItemName(cardItemId);
                    definitions.Add(new MonsterBookCardDefinition
                    {
                        CardItemId = cardItemId,
                        CardItemName = cardItemName,
                        MobId = mobId,
                        Only = only,
                        ConsumeOnPickup = consumeOnPickup,
                        Name = string.IsNullOrWhiteSpace(mobName) ? $"Mob #{mobId}" : mobName,
                        Level = Math.Max(0, level),
                        MaxHp = Math.Max(0, maxHp),
                        MaxMp = Math.Max(0, maxMp),
                        Exp = Math.Max(0, exp),
                        IsBoss = isBoss,
                        EpisodeText = episodeText,
                        RewardItemIds = rewardItemIds,
                        RewardLines = rewardLines,
                        HabitatLines = habitatLines,
                        SearchText = BuildSearchText(cardItemId, cardItemName, mobName, mobId, rewardLines, habitatLines, episodeText)
                    });
                }
            }
            catch
            {
                return definitions;
            }

            return definitions
                .OrderBy(definition => definition.CardItemId)
                .ToList();
        }

        private static void ResolveMobMetadata(
            int mobId,
            out string mobName,
            out int level,
            out int maxHp,
            out int maxMp,
            out int exp,
            out bool isBoss,
            out string episodeText,
            out IReadOnlyList<int> rewardItemIds,
            out IReadOnlyList<string> rewardLines,
            out IReadOnlyList<string> habitatLines)
        {
            mobName = ResolveMobName(mobId);
            level = 0;
            maxHp = 0;
            maxMp = 0;
            exp = 0;
            isBoss = false;
            string mobType = string.Empty;
            string elementAttribute = string.Empty;
            int category = 0;
            bool firstAttack = false;
            MonsterBookStringEntry stringEntry = ResolveMonsterBookStringEntry(mobId);
            rewardItemIds = stringEntry?.RewardItemIds ?? Array.Empty<int>();
            episodeText = BuildEpisodeText(mobName, mobId, level, isBoss, stringEntry);
            rewardLines = BuildRewardLines(isBoss, mobId, stringEntry);
            habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute, stringEntry);

            try
            {
                WzImage mobImage = global::HaCreator.Program.FindImage("Mob", mobId.ToString("D7", CultureInfo.InvariantCulture) + ".img");
                if (mobImage == null)
                {
                    episodeText = BuildEpisodeText(mobName, mobId, level, isBoss, stringEntry);
                    rewardLines = BuildRewardLines(isBoss, mobId, stringEntry);
                    habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute, stringEntry);
                    return;
                }

                if (!mobImage.Parsed)
                {
                    mobImage.ParseImage();
                }

                MobData mobData = MobData.Parse(mobImage, mobId);
                if (mobData == null)
                {
                    episodeText = BuildEpisodeText(mobName, mobId, level, isBoss, stringEntry);
                    rewardLines = BuildRewardLines(isBoss, mobId, stringEntry);
                    habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute, stringEntry);
                    return;
                }

                level = mobData.Level;
                maxHp = mobData.MaxHP;
                maxMp = mobData.MaxMP;
                exp = mobData.Exp;
                isBoss = mobData.IsBoss;

                WzSubProperty infoProperty = mobImage["info"] as WzSubProperty;
                mobType = (infoProperty?["mobType"] as WzStringProperty)?.Value ?? string.Empty;
                elementAttribute = (infoProperty?["elemAttr"] as WzStringProperty)?.Value ?? string.Empty;
                category = ReadInt(infoProperty?["category"]);
                firstAttack = ReadInt(infoProperty?["firstAttack"]) != 0;
            }
            catch
            {
                level = 0;
                maxHp = 0;
                maxMp = 0;
                exp = 0;
                isBoss = false;
            }

            episodeText = BuildEpisodeText(mobName, mobId, level, isBoss, stringEntry);
            rewardLines = BuildRewardLines(isBoss, mobId, stringEntry);
            habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute, stringEntry);
        }

        private static string ResolveMobName(int mobId)
        {
            string key = mobId.ToString(CultureInfo.InvariantCulture);
            return global::HaCreator.Program.InfoManager.MobNameCache.TryGetValue(key, out string name)
                ? name
                : null;
        }

        private MonsterBookRecord GetRecord(CharacterBuild build, bool createIfMissing)
        {
            return GetRecord(build, build?.Id ?? 0, build?.Name, createIfMissing);
        }

        private MonsterBookRecord GetRecord(CharacterBuild build, int characterId, string characterName, bool createIfMissing)
        {
            string key = ResolveCharacterKey(build, characterId, characterName);
            if (!_bookByCharacter.TryGetValue(key, out MonsterBookRecord record) || record == null)
            {
                if (!createIfMissing)
                {
                    return null;
                }

                record = new MonsterBookRecord();
                _bookByCharacter[key] = record;
            }

            record.CardCountsByMob ??= new Dictionary<int, int>();
            record.RegisteredMobIds ??= new HashSet<int>();
            return record;
        }

        internal static string ResolveCharacterKey(CharacterBuild build)
        {
            return ResolveCharacterKey(build, build?.Id ?? 0, build?.Name);
        }

        internal static string ResolveCharacterKey(CharacterBuild build, int characterId, string characterName)
        {
            if (characterId > 0)
            {
                return $"id:{characterId}";
            }

            if (!string.IsNullOrWhiteSpace(characterName))
            {
                return $"name:{characterName.Trim().ToLowerInvariant()}";
            }

            if (build == null)
            {
                return "session:default";
            }

            if (build.Id > 0)
            {
                return $"id:{build.Id}";
            }

            if (!string.IsNullOrWhiteSpace(build.Name))
            {
                return $"name:{build.Name.Trim().ToLowerInvariant()}";
            }

            return "session:default";
        }

        private void LoadFromDisk()
        {
            if (string.IsNullOrWhiteSpace(_storageFilePath) || !File.Exists(_storageFilePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_storageFilePath);
                PersistedStore persisted = JsonSerializer.Deserialize<PersistedStore>(json, JsonOptions);
                if (persisted?.BookByCharacter == null)
                {
                    return;
                }

                _bookByCharacter.Clear();
                foreach (KeyValuePair<string, MonsterBookRecord> entry in persisted.BookByCharacter)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    Dictionary<int, int> normalizedCounts = entry.Value.CardCountsByMob?
                        .Where(pair => pair.Key > 0 && pair.Value > 0)
                        .ToDictionary(pair => pair.Key, pair => Math.Clamp(pair.Value, 0, 5))
                        ?? new Dictionary<int, int>();

                    _bookByCharacter[entry.Key] = new MonsterBookRecord
                    {
                        CardCountsByMob = normalizedCounts,
                        RegisteredMobIds = entry.Value.RegisteredMobIds?
                            .Where(mobId => mobId > 0 && normalizedCounts.TryGetValue(mobId, out int ownedCopies) && ownedCopies > 0)
                            .OrderBy(mobId => mobId)
                            .Take(1)
                            .ToHashSet()
                            ?? new HashSet<int>()
                    };
                }
            }
            catch
            {
                _bookByCharacter.Clear();
            }
        }

        private void SaveToDisk()
        {
            if (string.IsNullOrWhiteSpace(_storageFilePath))
            {
                return;
            }

            PersistedStore persisted = new()
            {
                BookByCharacter = new Dictionary<string, MonsterBookRecord>(_bookByCharacter, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so the local Monster Book runtime remains usable.
            }
        }

        private static int ParseInt(string text)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        private static int ReadInt(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)longProperty.Value,
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
                _ => 0
            };
        }

        private static MonsterBookStringEntry ResolveMonsterBookStringEntry(int mobId)
        {
            if (mobId <= 0)
            {
                return null;
            }

            try
            {
                WzImage stringImage = global::HaCreator.Program.FindImage("String", "MonsterBook.img");
                if (stringImage == null)
                {
                    return null;
                }

                if (!stringImage.Parsed)
                {
                    stringImage.ParseImage();
                }

                WzSubProperty entryProperty = stringImage[mobId.ToString(CultureInfo.InvariantCulture)] as WzSubProperty;
                if (entryProperty == null)
                {
                    return null;
                }

                return new MonsterBookStringEntry
                {
                    EpisodeText = (entryProperty["episode"] as WzStringProperty)?.Value ?? string.Empty,
                    RewardItemIds = ReadOrderedInts(entryProperty["reward"] as WzSubProperty),
                    MapIds = ReadOrderedInts(entryProperty["map"] as WzSubProperty)
                };
            }
            catch
            {
                return null;
            }
        }

        private static IReadOnlyList<int> ReadOrderedInts(WzSubProperty property)
        {
            if (property == null)
            {
                return Array.Empty<int>();
            }

            return property.WzProperties
                .OrderBy(entry => ParseInt(entry.Name))
                .Select(ReadInt)
                .Where(value => value > 0)
                .ToArray();
        }

        private static string BuildEpisodeText(string mobName, int mobId, int level, bool isBoss, MonsterBookStringEntry stringEntry)
        {
            if (!string.IsNullOrWhiteSpace(stringEntry?.EpisodeText))
            {
                return stringEntry.EpisodeText.Trim();
            }

            string safeName = string.IsNullOrWhiteSpace(mobName) ? $"Mob #{mobId}" : mobName;
            string bossText = isBoss ? "Boss monster card entry." : "Standard monster card entry.";
            return $"{safeName} is recorded in the WZ-backed Monster Book catalog as mob #{mobId} at level {Math.Max(0, level)}. {bossText}";
        }

        private static IReadOnlyList<string> BuildRewardLines(bool isBoss, int mobId, MonsterBookStringEntry stringEntry)
        {
            if (stringEntry?.RewardItemIds?.Count > 0)
            {
                return stringEntry.RewardItemIds
                    .Take(4)
                    .Select(ResolveRewardLine)
                    .Append($"Card target mob: {mobId}")
                    .ToArray();
            }

            return new[]
            {
                isBoss ? "Boss card classification" : "Normal card classification",
                "Completion mark unlocks at 5 cards",
                $"Local card target mob: {mobId}"
            };
        }

        private static IReadOnlyList<string> BuildHabitatLines(int category, string mobType, bool firstAttack, string elementAttribute, MonsterBookStringEntry stringEntry)
        {
            if (stringEntry?.MapIds?.Count > 0)
            {
                return stringEntry.MapIds
                    .Take(4)
                    .Select(ResolveHabitatLine)
                    .ToArray();
            }

            string categoryText = category > 0 ? $"Mob category {category}" : "Mob category unavailable";
            string typeText = string.IsNullOrWhiteSpace(mobType) ? "Mob type unavailable" : $"Mob type {mobType}";
            string attackText = firstAttack ? "Aggressive first-attack behavior" : "Passive first-attack behavior";
            string elementText = string.IsNullOrWhiteSpace(elementAttribute) ? "Elemental attribute unavailable" : $"Element attribute {elementAttribute}";
            return new[] { categoryText, typeText, attackText, elementText };
        }

        private static string BuildSearchText(int cardItemId, string cardItemName, string mobName, int mobId, IReadOnlyList<string> rewardLines, IReadOnlyList<string> habitatLines, string episodeText)
        {
            IEnumerable<string> parts = new[]
            {
                mobName,
                mobId.ToString(CultureInfo.InvariantCulture),
                cardItemName,
                cardItemId > 0 ? cardItemId.ToString(CultureInfo.InvariantCulture) : string.Empty,
                episodeText
            }
            .Concat(rewardLines ?? Array.Empty<string>())
            .Concat(habitatLines ?? Array.Empty<string>());

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string ResolveRewardLine(int itemId)
        {
            string itemName = ResolveItemName(itemId);
            return string.IsNullOrWhiteSpace(itemName)
                ? $"Reward item #{itemId}"
                : $"{itemName} ({itemId})";
        }

        private static string ResolveHabitatLine(int mapId)
        {
            string mapName = ResolveMapName(mapId);
            return string.IsNullOrWhiteSpace(mapName)
                ? $"Map #{mapId}"
                : mapName;
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            return global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                ? itemInfo?.Item2 ?? string.Empty
                : string.Empty;
        }

        private static string ResolveMapName(int mapId)
        {
            if (mapId <= 0)
            {
                return string.Empty;
            }

            string key = mapId.ToString("D9", CultureInfo.InvariantCulture);
            if (!global::HaCreator.Program.InfoManager.MapsNameCache.TryGetValue(key, out Tuple<string, string, string> mapInfo) || mapInfo == null)
            {
                return string.Empty;
            }

            string streetName = mapInfo.Item1?.Trim();
            string mapName = mapInfo.Item2?.Trim();
            if (string.IsNullOrWhiteSpace(streetName))
            {
                return mapName ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(mapName) || string.Equals(streetName, mapName, StringComparison.OrdinalIgnoreCase))
            {
                return streetName;
            }

            return $"{streetName} - {mapName}";
        }

        private static int ResolveRegisteredMobId(MonsterBookRecord record)
        {
            return record?.RegisteredMobIds?
                .Where(mobId => mobId > 0
                    && record.CardCountsByMob != null
                    && record.CardCountsByMob.TryGetValue(mobId, out int ownedCopies)
                    && ownedCopies > 0)
                .OrderBy(mobId => mobId)
                .FirstOrDefault() ?? 0;
        }

    }
}
