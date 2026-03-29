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
            public int MobId { get; init; }
            public string Name { get; init; } = string.Empty;
            public int Level { get; init; }
            public int MaxHp { get; init; }
            public int Exp { get; init; }
            public bool IsBoss { get; init; }
            public string EpisodeText { get; init; } = string.Empty;
            public IReadOnlyList<string> RewardLines { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> HabitatLines { get; init; } = Array.Empty<string>();
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
        private const int CardsPerGrade = 50;
        private const int MaximumGradeCount = 9;
        private List<MonsterBookCardDefinition> _catalog;
        private Dictionary<int, MonsterBookCardDefinition> _catalogByMobId;

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
            IReadOnlyList<MonsterBookCardDefinition> catalog = EnsureCatalog();
            MonsterBookRecord record = GetRecord(build, createIfMissing: false);
            IReadOnlyDictionary<int, int> counts = record?.CardCountsByMob ?? new Dictionary<int, int>();
            HashSet<int> registeredMobIds = record?.RegisteredMobIds ?? new HashSet<int>();

            List<MonsterBookCardSnapshot> cards = catalog
                .Select((definition, index) =>
                {
                    counts.TryGetValue(definition.MobId, out int ownedCopies);
                    int gradeIndex = Math.Clamp(index / CardsPerGrade, 0, MaximumGradeCount - 1);
                    return new MonsterBookCardSnapshot
                    {
                        CardItemId = definition.CardItemId,
                        MobId = definition.MobId,
                        GradeIndex = gradeIndex,
                        GradeLabel = BuildGradeLabel(gradeIndex),
                        Name = definition.Name,
                        Level = definition.Level,
                        MaxHp = definition.MaxHp,
                        Exp = definition.Exp,
                        IsBoss = definition.IsBoss,
                        OwnedCopies = Math.Clamp(ownedCopies, 0, 5),
                        IsRegistered = registeredMobIds.Contains(definition.MobId),
                        EpisodeText = definition.EpisodeText,
                        RewardLines = definition.RewardLines,
                        HabitatLines = definition.HabitatLines
                    };
                })
                .ToList();

            List<MonsterBookGradeSnapshot> grades = cards
                .Chunk(CardsPerGrade)
                .Select((chunk, gradeIndex) =>
                {
                    List<MonsterBookCardSnapshot> gradeCards = chunk.ToList();
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

            return new MonsterBookSnapshot
            {
                Title = "Monster Book",
                Subtitle = build == null
                    ? "Monster card ownership is unavailable because there is no active character build."
                    : "Card ownership is persisted per character and built from the WZ-backed monster-card catalog, with local chapter tabs and registered-card state.",
                StatusText = "The Monster Book owner now follows the WZ-backed left and right tab shell, carries local registered-card state, and exposes a local search path on top of the dedicated card catalog. Official packet or drop-authored ownership and the deeper client close lifecycle still remain outside this simulator runtime.",
                TotalCardTypes = cards.Count,
                OwnedCardTypes = ownedCardTypes,
                CompletedCardTypes = completedCardTypes,
                OwnedBossCardTypes = ownedBossCardTypes,
                OwnedNormalCardTypes = ownedNormalCardTypes,
                TotalOwnedCopies = totalOwnedCopies,
                Grades = grades,
                Pages = pages
            };
        }

        public MonsterBookSnapshot RecordMobKill(CharacterBuild build, int mobId, int count = 1)
        {
            if (mobId <= 0 || count <= 0)
            {
                return GetSnapshot(build);
            }

            Dictionary<int, MonsterBookCardDefinition> catalogByMobId = EnsureCatalogByMobId();
            if (!catalogByMobId.TryGetValue(mobId, out MonsterBookCardDefinition definition) || definition == null)
            {
                return GetSnapshot(build);
            }

            MonsterBookRecord record = GetRecord(build, createIfMissing: true);
            record.CardCountsByMob.TryGetValue(definition.MobId, out int currentCount);
            int nextCount = Math.Clamp(currentCount + count, 0, 5);
            if (nextCount == currentCount)
            {
                return GetSnapshot(build);
            }

            record.CardCountsByMob[definition.MobId] = nextCount;
            SaveToDisk();
            return GetSnapshot(build);
        }

        public MonsterBookSnapshot RecordCardPickup(CharacterBuild build, int itemId, int count = 1)
        {
            if (itemId <= 0 || count <= 0)
            {
                return GetSnapshot(build);
            }

            MonsterBookCardDefinition definition = EnsureCatalog().FirstOrDefault(entry => entry.CardItemId == itemId);
            if (definition == null)
            {
                return GetSnapshot(build);
            }

            return RecordMobKill(build, definition.MobId, count);
        }

        public MonsterBookSnapshot SetRegisteredCard(CharacterBuild build, int mobId, bool registered)
        {
            if (mobId <= 0 || !EnsureCatalogByMobId().ContainsKey(mobId))
            {
                return GetSnapshot(build);
            }

            MonsterBookRecord record = GetRecord(build, createIfMissing: true);
            record.RegisteredMobIds ??= new HashSet<int>();

            bool changed = registered
                ? record.RegisteredMobIds.Add(mobId)
                : record.RegisteredMobIds.Remove(mobId);
            if (changed)
            {
                SaveToDisk();
            }

            return GetSnapshot(build);
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
                return _catalog;
            }
        }

        private Dictionary<int, MonsterBookCardDefinition> EnsureCatalogByMobId()
        {
            EnsureCatalog();
            return _catalogByMobId ?? new Dictionary<int, MonsterBookCardDefinition>();
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
                        out int exp,
                        out bool isBoss,
                        out string episodeText,
                        out IReadOnlyList<string> rewardLines,
                        out IReadOnlyList<string> habitatLines);
                    definitions.Add(new MonsterBookCardDefinition
                    {
                        CardItemId = cardItemId,
                        MobId = mobId,
                        Name = string.IsNullOrWhiteSpace(mobName) ? $"Mob #{mobId}" : mobName,
                        Level = Math.Max(0, level),
                        MaxHp = Math.Max(0, maxHp),
                        Exp = Math.Max(0, exp),
                        IsBoss = isBoss,
                        EpisodeText = episodeText,
                        RewardLines = rewardLines,
                        HabitatLines = habitatLines
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
            out int exp,
            out bool isBoss,
            out string episodeText,
            out IReadOnlyList<string> rewardLines,
            out IReadOnlyList<string> habitatLines)
        {
            mobName = ResolveMobName(mobId);
            level = 0;
            maxHp = 0;
            exp = 0;
            isBoss = false;
            string mobType = string.Empty;
            string elementAttribute = string.Empty;
            int category = 0;
            bool firstAttack = false;
            episodeText = BuildEpisodeText(mobName, mobId, level, isBoss);
            rewardLines = BuildRewardLines(isBoss, mobId);
            habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute);

            try
            {
                WzImage mobImage = global::HaCreator.Program.FindImage("Mob", mobId.ToString("D7", CultureInfo.InvariantCulture) + ".img");
                if (mobImage == null)
                {
                    episodeText = BuildEpisodeText(mobName, mobId, level, isBoss);
                    rewardLines = BuildRewardLines(isBoss, mobId);
                    habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute);
                    return;
                }

                if (!mobImage.Parsed)
                {
                    mobImage.ParseImage();
                }

                MobData mobData = MobData.Parse(mobImage, mobId);
                if (mobData == null)
                {
                    episodeText = BuildEpisodeText(mobName, mobId, level, isBoss);
                    rewardLines = BuildRewardLines(isBoss, mobId);
                    habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute);
                    return;
                }

                level = mobData.Level;
                maxHp = mobData.MaxHP;
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
                exp = 0;
                isBoss = false;
            }

            episodeText = BuildEpisodeText(mobName, mobId, level, isBoss);
            rewardLines = BuildRewardLines(isBoss, mobId);
            habitatLines = BuildHabitatLines(category, mobType, firstAttack, elementAttribute);
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
            string key = ResolveCharacterKey(build);
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

        private static string ResolveCharacterKey(CharacterBuild build)
        {
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
                            .Where(mobId => mobId > 0)
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

        private static string BuildEpisodeText(string mobName, int mobId, int level, bool isBoss)
        {
            string safeName = string.IsNullOrWhiteSpace(mobName) ? $"Mob #{mobId}" : mobName;
            string bossText = isBoss ? "Boss monster card entry." : "Standard monster card entry.";
            return $"{safeName} is recorded in the WZ-backed Monster Book catalog as mob #{mobId} at level {Math.Max(0, level)}. {bossText}";
        }

        private static IReadOnlyList<string> BuildRewardLines(bool isBoss, int mobId)
        {
            return new[]
            {
                isBoss ? "Boss card classification" : "Normal card classification",
                "Completion mark unlocks at 5 cards",
                $"Local card target mob: {mobId}"
            };
        }

        private static IReadOnlyList<string> BuildHabitatLines(int category, string mobType, bool firstAttack, string elementAttribute)
        {
            string categoryText = category > 0 ? $"Mob category {category}" : "Mob category unavailable";
            string typeText = string.IsNullOrWhiteSpace(mobType) ? "Mob type unavailable" : $"Mob type {mobType}";
            string attackText = firstAttack ? "Aggressive first-attack behavior" : "Passive first-attack behavior";
            string elementText = string.IsNullOrWhiteSpace(elementAttribute) ? "Elemental attribute unavailable" : $"Element attribute {elementAttribute}";
            return new[] { categoryText, typeText, attackText, elementText };
        }
    }
}
