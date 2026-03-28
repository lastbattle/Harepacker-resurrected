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
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, MonsterBookRecord> _bookByCharacter = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;
        private readonly object _catalogLock = new();
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

            List<MonsterBookCardSnapshot> cards = catalog
                .Select(definition =>
                {
                    counts.TryGetValue(definition.MobId, out int ownedCopies);
                    return new MonsterBookCardSnapshot
                    {
                        CardItemId = definition.CardItemId,
                        MobId = definition.MobId,
                        Name = definition.Name,
                        Level = definition.Level,
                        MaxHp = definition.MaxHp,
                        Exp = definition.Exp,
                        IsBoss = definition.IsBoss,
                        OwnedCopies = Math.Clamp(ownedCopies, 0, 5)
                    };
                })
                .ToList();

            List<MonsterBookPageSnapshot> pages = cards
                .Chunk(25)
                .Select((chunk, index) => new MonsterBookPageSnapshot
                {
                    PageIndex = index,
                    Title = $"Page {index + 1}",
                    Subtitle = BuildPageSubtitle(chunk),
                    Cards = chunk.ToList()
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
                    : "Card ownership is persisted per character and built from the WZ-backed monster-card catalog.",
                StatusText = "The Monster Book owner now reads a dedicated card catalog and save-backed ownership state instead of item-maker progression data. Exact client tab/category routing, search flow, and official drop or packet ownership still remain outside this local runtime.",
                TotalCardTypes = cards.Count,
                OwnedCardTypes = ownedCardTypes,
                CompletedCardTypes = completedCardTypes,
                OwnedBossCardTypes = ownedBossCardTypes,
                OwnedNormalCardTypes = ownedNormalCardTypes,
                TotalOwnedCopies = totalOwnedCopies,
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

        private static string BuildPageSubtitle(IReadOnlyList<MonsterBookCardSnapshot> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                return "No cards are available on this page.";
            }

            MonsterBookCardSnapshot firstCard = cards[0];
            MonsterBookCardSnapshot lastCard = cards[^1];
            return string.Equals(firstCard.Name, lastCard.Name, StringComparison.Ordinal)
                ? firstCard.Name
                : $"{firstCard.Name} to {lastCard.Name}";
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

                    ResolveMobMetadata(mobId, out string mobName, out int level, out int maxHp, out int exp, out bool isBoss);
                    definitions.Add(new MonsterBookCardDefinition
                    {
                        CardItemId = cardItemId,
                        MobId = mobId,
                        Name = string.IsNullOrWhiteSpace(mobName) ? $"Mob #{mobId}" : mobName,
                        Level = Math.Max(0, level),
                        MaxHp = Math.Max(0, maxHp),
                        Exp = Math.Max(0, exp),
                        IsBoss = isBoss
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

        private static void ResolveMobMetadata(int mobId, out string mobName, out int level, out int maxHp, out int exp, out bool isBoss)
        {
            mobName = ResolveMobName(mobId);
            level = 0;
            maxHp = 0;
            exp = 0;
            isBoss = false;

            try
            {
                WzImage mobImage = global::HaCreator.Program.FindImage("Mob", mobId.ToString("D7", CultureInfo.InvariantCulture) + ".img");
                if (mobImage == null)
                {
                    return;
                }

                if (!mobImage.Parsed)
                {
                    mobImage.ParseImage();
                }

                MobData mobData = MobData.Parse(mobImage, mobId);
                if (mobData == null)
                {
                    return;
                }

                level = mobData.Level;
                maxHp = mobData.MaxHP;
                exp = mobData.Exp;
                isBoss = mobData.IsBoss;
            }
            catch
            {
                level = 0;
                maxHp = 0;
                exp = 0;
                isBoss = false;
            }
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
                        CardCountsByMob = normalizedCounts
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
    }
}
