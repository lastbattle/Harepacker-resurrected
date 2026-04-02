using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class ItemMakerRecipeProgressionEntry
    {
        public string RecipeKey { get; init; } = string.Empty;
        public int OutputItemId { get; init; }
    }

    public enum ItemMakerRecipeFamily
    {
        Generic,
        Gloves,
        Shoes,
        Toys
    }

    public sealed class ItemMakerCraftResult
    {
        public ItemMakerRecipeFamily Family { get; init; } = ItemMakerRecipeFamily.Generic;
        public bool IsHiddenRecipe { get; init; }
        public string RecipeKey { get; init; } = string.Empty;
        public int RecipeOutputItemId { get; init; }
        public int CraftedItemId { get; init; }
        public int CraftedQuantity { get; init; } = 1;
    }

    public sealed class ItemMakerProgressionSnapshot
    {
        internal ItemMakerProgressionSnapshot(
            int genericLevel,
            int gloveLevel,
            int shoeLevel,
            int toyLevel,
            int genericProgress,
            int gloveProgress,
            int shoeProgress,
            int toyProgress,
            int successfulCrafts,
            int traitCraft,
            IReadOnlyCollection<ItemMakerRecipeProgressionEntry> discoveredRecipeEntries,
            IReadOnlyCollection<ItemMakerRecipeProgressionEntry> unlockedHiddenRecipeEntries,
            IReadOnlyCollection<string> discoveredRecipeKeys,
            IReadOnlyCollection<string> unlockedHiddenRecipeKeys,
            IReadOnlyCollection<int> legacyDiscoveredRecipeIds,
            IReadOnlyCollection<int> legacyUnlockedHiddenRecipeIds)
        {
            GenericLevel = genericLevel;
            GloveLevel = gloveLevel;
            ShoeLevel = shoeLevel;
            ToyLevel = toyLevel;
            GenericProgress = genericProgress;
            GloveProgress = gloveProgress;
            ShoeProgress = shoeProgress;
            ToyProgress = toyProgress;
            SuccessfulCrafts = successfulCrafts;
            TraitCraft = Math.Max(0, traitCraft);
            DiscoveredRecipeEntries = NormalizeEntries(discoveredRecipeEntries);
            UnlockedHiddenRecipeEntries = NormalizeEntries(unlockedHiddenRecipeEntries);
            DiscoveredRecipeKeys = new HashSet<string>((discoveredRecipeKeys ?? Array.Empty<string>())
                .Where(static key => !string.IsNullOrWhiteSpace(key)), StringComparer.Ordinal);
            UnlockedHiddenRecipeKeys = new HashSet<string>((unlockedHiddenRecipeKeys ?? Array.Empty<string>())
                .Where(static key => !string.IsNullOrWhiteSpace(key)), StringComparer.Ordinal);
            LegacyDiscoveredRecipeIds = new HashSet<int>(legacyDiscoveredRecipeIds ?? Array.Empty<int>());
            LegacyUnlockedHiddenRecipeIds = new HashSet<int>(legacyUnlockedHiddenRecipeIds ?? Array.Empty<int>());
        }

        public static ItemMakerProgressionSnapshot Default { get; } = new(
            1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
            Array.Empty<ItemMakerRecipeProgressionEntry>(),
            Array.Empty<ItemMakerRecipeProgressionEntry>(),
            Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<int>(), Array.Empty<int>());

        public int GenericLevel { get; }
        public int GloveLevel { get; }
        public int ShoeLevel { get; }
        public int ToyLevel { get; }
        public int GenericProgress { get; }
        public int GloveProgress { get; }
        public int ShoeProgress { get; }
        public int ToyProgress { get; }
        public int SuccessfulCrafts { get; }
        public int TraitCraft { get; }
        public IReadOnlyCollection<ItemMakerRecipeProgressionEntry> DiscoveredRecipeEntries { get; }
        public IReadOnlyCollection<ItemMakerRecipeProgressionEntry> UnlockedHiddenRecipeEntries { get; }
        public IReadOnlySet<string> DiscoveredRecipeKeys { get; }
        public IReadOnlySet<string> UnlockedHiddenRecipeKeys { get; }
        public IReadOnlySet<int> LegacyDiscoveredRecipeIds { get; }
        public IReadOnlySet<int> LegacyUnlockedHiddenRecipeIds { get; }
        public IReadOnlySet<int> DiscoveredRecipeIds => LegacyDiscoveredRecipeIds;
        public IReadOnlySet<int> UnlockedHiddenRecipeIds => LegacyUnlockedHiddenRecipeIds;
        public int DiscoveredRecipeCount => DiscoveredRecipeEntries.Count;
        public int UnlockedHiddenRecipeCount => UnlockedHiddenRecipeEntries.Count;

        public int GetLevel(ItemMakerRecipeFamily family)
        {
            return family switch
            {
                ItemMakerRecipeFamily.Gloves => GloveLevel,
                ItemMakerRecipeFamily.Shoes => ShoeLevel,
                ItemMakerRecipeFamily.Toys => ToyLevel,
                _ => GenericLevel
            };
        }

        public int GetProgress(ItemMakerRecipeFamily family)
        {
            return family switch
            {
                ItemMakerRecipeFamily.Gloves => GloveProgress,
                ItemMakerRecipeFamily.Shoes => ShoeProgress,
                ItemMakerRecipeFamily.Toys => ToyProgress,
                _ => GenericProgress
            };
        }

        public int GetProgressTarget(ItemMakerRecipeFamily family)
        {
            return ItemMakerProgressionStore.GetCraftsNeededForNextLevel(GetLevel(family));
        }

        public string GetFamilyLabel(ItemMakerRecipeFamily family)
        {
            return family switch
            {
                ItemMakerRecipeFamily.Gloves => "Glove",
                ItemMakerRecipeFamily.Shoes => "Shoe",
                ItemMakerRecipeFamily.Toys => "Toy",
                _ => "Maker"
            };
        }

        public bool IsRecipeDiscovered(string recipeKey, int outputItemId = 0)
        {
            return (!string.IsNullOrWhiteSpace(recipeKey) && DiscoveredRecipeKeys.Contains(recipeKey))
                || (outputItemId > 0 && LegacyDiscoveredRecipeIds.Contains(outputItemId));
        }

        public bool IsHiddenRecipeUnlocked(string recipeKey, int outputItemId = 0)
        {
            return (!string.IsNullOrWhiteSpace(recipeKey) && UnlockedHiddenRecipeKeys.Contains(recipeKey))
                || (outputItemId > 0 && LegacyUnlockedHiddenRecipeIds.Contains(outputItemId));
        }

        private static IReadOnlyCollection<ItemMakerRecipeProgressionEntry> NormalizeEntries(
            IReadOnlyCollection<ItemMakerRecipeProgressionEntry> entries)
        {
            List<ItemMakerRecipeProgressionEntry> normalized = new();
            HashSet<string> seenKeys = new(StringComparer.Ordinal);
            HashSet<int> seenLegacyIds = new();
            foreach (ItemMakerRecipeProgressionEntry entry in entries ?? Array.Empty<ItemMakerRecipeProgressionEntry>())
            {
                if (entry == null)
                {
                    continue;
                }

                string recipeKey = entry.RecipeKey?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(recipeKey))
                {
                    if (!seenKeys.Add(recipeKey))
                    {
                        continue;
                    }

                    normalized.Add(new ItemMakerRecipeProgressionEntry
                    {
                        RecipeKey = recipeKey,
                        OutputItemId = Math.Max(0, entry.OutputItemId)
                    });
                    continue;
                }

                if (entry.OutputItemId > 0 && seenLegacyIds.Add(entry.OutputItemId))
                {
                    normalized.Add(new ItemMakerRecipeProgressionEntry
                    {
                        OutputItemId = entry.OutputItemId
                    });
                }
            }

            return normalized;
        }
    }

    public sealed class ItemMakerProgressionStore
    {
        private sealed class PersistedStore
        {
            public Dictionary<string, ProgressionRecord> ProgressionByCharacter { get; set; } = new(StringComparer.Ordinal);
        }

        private sealed class ProgressionRecord
        {
            public int GenericLevel { get; set; } = 1;
            public int GloveLevel { get; set; } = 1;
            public int ShoeLevel { get; set; } = 1;
            public int ToyLevel { get; set; } = 1;
            public int GenericProgress { get; set; }
            public int GloveProgress { get; set; }
            public int ShoeProgress { get; set; }
            public int ToyProgress { get; set; }
            public int SuccessfulCrafts { get; set; }
            public Dictionary<string, int> DiscoveredRecipeOutputIdsByKey { get; set; } = new(StringComparer.Ordinal);
            public Dictionary<string, int> UnlockedHiddenRecipeOutputIdsByKey { get; set; } = new(StringComparer.Ordinal);
            public HashSet<string> DiscoveredRecipeKeys { get; set; } = new(StringComparer.Ordinal);
            public HashSet<string> UnlockedHiddenRecipeKeys { get; set; } = new(StringComparer.Ordinal);
            public HashSet<int> LegacyDiscoveredRecipeIds { get; set; } = new();
            public HashSet<int> LegacyUnlockedHiddenRecipeIds { get; set; } = new();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, ProgressionRecord> _progressionByCharacter = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;

        public ItemMakerProgressionStore(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "item-maker-progression.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public ItemMakerProgressionSnapshot GetSnapshot(CharacterBuild build)
        {
            string key = ResolveCharacterKey(build);
            if (!_progressionByCharacter.TryGetValue(key, out ProgressionRecord record) || record == null)
            {
                return new ItemMakerProgressionSnapshot(
                    1, 1, 1, 1, 0, 0, 0, 0, 0, build?.TraitCraft ?? 0,
                    Array.Empty<ItemMakerRecipeProgressionEntry>(),
                    Array.Empty<ItemMakerRecipeProgressionEntry>(),
                    Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<int>(), Array.Empty<int>());
            }

            record.DiscoveredRecipeOutputIdsByKey ??= new Dictionary<string, int>(StringComparer.Ordinal);
            record.UnlockedHiddenRecipeOutputIdsByKey ??= new Dictionary<string, int>(StringComparer.Ordinal);
            record.DiscoveredRecipeKeys ??= new HashSet<string>(StringComparer.Ordinal);
            record.UnlockedHiddenRecipeKeys ??= new HashSet<string>(StringComparer.Ordinal);
            record.LegacyDiscoveredRecipeIds ??= new HashSet<int>();
            record.LegacyUnlockedHiddenRecipeIds ??= new HashSet<int>();
            return CreateSnapshot(record, build?.TraitCraft ?? 0);
        }

        public ItemMakerProgressionSnapshot RecordDiscoveredRecipes(CharacterBuild build, IEnumerable<ItemMakerRecipeProgressionEntry> recipeEntries)
        {
            string key = ResolveCharacterKey(build);
            ProgressionRecord record = GetOrCreateRecord(key);

            bool changed = false;
            foreach (ItemMakerRecipeProgressionEntry entry in recipeEntries ?? Enumerable.Empty<ItemMakerRecipeProgressionEntry>())
            {
                string recipeKey = entry?.RecipeKey?.Trim();
                if (!string.IsNullOrWhiteSpace(recipeKey) && record.DiscoveredRecipeKeys.Add(recipeKey))
                {
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(recipeKey) && entry.OutputItemId > 0)
                {
                    if (!record.DiscoveredRecipeOutputIdsByKey.TryGetValue(recipeKey, out int existingOutputItemId) ||
                        existingOutputItemId != entry.OutputItemId)
                    {
                        record.DiscoveredRecipeOutputIdsByKey[recipeKey] = entry.OutputItemId;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                SaveToDisk();
            }

            return CreateSnapshot(record, build?.TraitCraft ?? 0);
        }

        public ItemMakerProgressionSnapshot RecordDiscoveredRecipes(CharacterBuild build, IEnumerable<int> outputItemIds)
        {
            string key = ResolveCharacterKey(build);
            ProgressionRecord record = GetOrCreateRecord(key);

            bool changed = false;
            foreach (int outputItemId in outputItemIds ?? Enumerable.Empty<int>())
            {
                if (outputItemId > 0 && record.LegacyDiscoveredRecipeIds.Add(outputItemId))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                SaveToDisk();
            }

            return CreateSnapshot(record, build?.TraitCraft ?? 0);
        }

        public ItemMakerProgressionSnapshot RecordUnlockedHiddenRecipes(CharacterBuild build, IEnumerable<ItemMakerRecipeProgressionEntry> recipeEntries)
        {
            string key = ResolveCharacterKey(build);
            ProgressionRecord record = GetOrCreateRecord(key);

            bool changed = false;
            foreach (ItemMakerRecipeProgressionEntry entry in recipeEntries ?? Enumerable.Empty<ItemMakerRecipeProgressionEntry>())
            {
                string recipeKey = entry?.RecipeKey?.Trim();
                if (!string.IsNullOrWhiteSpace(recipeKey) && record.UnlockedHiddenRecipeKeys.Add(recipeKey))
                {
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(recipeKey) && entry.OutputItemId > 0)
                {
                    if (!record.UnlockedHiddenRecipeOutputIdsByKey.TryGetValue(recipeKey, out int existingOutputItemId) ||
                        existingOutputItemId != entry.OutputItemId)
                    {
                        record.UnlockedHiddenRecipeOutputIdsByKey[recipeKey] = entry.OutputItemId;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                SaveToDisk();
            }

            return CreateSnapshot(record, build?.TraitCraft ?? 0);
        }

        public ItemMakerProgressionSnapshot RecordUnlockedHiddenRecipes(CharacterBuild build, IEnumerable<int> outputItemIds)
        {
            string key = ResolveCharacterKey(build);
            ProgressionRecord record = GetOrCreateRecord(key);

            bool changed = false;
            foreach (int outputItemId in outputItemIds ?? Enumerable.Empty<int>())
            {
                if (outputItemId > 0 && record.LegacyUnlockedHiddenRecipeIds.Add(outputItemId))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                SaveToDisk();
            }

            return CreateSnapshot(record, build?.TraitCraft ?? 0);
        }

        public ItemMakerProgressionSnapshot RecordCraft(CharacterBuild build, ItemMakerCraftResult result)
        {
            string key = ResolveCharacterKey(build);
            ProgressionRecord record = GetOrCreateRecord(key);

            record.SuccessfulCrafts = Math.Max(0, record.SuccessfulCrafts + 1);
            string recipeKey = result?.RecipeKey?.Trim();
            int recipeOutputItemId = result?.RecipeOutputItemId ?? 0;
            if (!string.IsNullOrWhiteSpace(recipeKey))
            {
                if (result.IsHiddenRecipe)
                {
                    record.UnlockedHiddenRecipeKeys.Add(recipeKey);
                    if (recipeOutputItemId > 0)
                    {
                        record.UnlockedHiddenRecipeOutputIdsByKey[recipeKey] = recipeOutputItemId;
                    }
                }
                else
                {
                    record.DiscoveredRecipeKeys.Add(recipeKey);
                    if (recipeOutputItemId > 0)
                    {
                        record.DiscoveredRecipeOutputIdsByKey[recipeKey] = recipeOutputItemId;
                    }
                }
            }
            else if (recipeOutputItemId > 0)
            {
                if (result.IsHiddenRecipe)
                {
                    record.LegacyUnlockedHiddenRecipeIds.Add(recipeOutputItemId);
                }
                else
                {
                    record.LegacyDiscoveredRecipeIds.Add(recipeOutputItemId);
                }
            }

            int currentLevel = GetFamilyLevel(record, result?.Family ?? ItemMakerRecipeFamily.Generic);
            if (currentLevel < MaxMakerSkillLevel)
            {
                int progress = GetFamilyProgress(record, result?.Family ?? ItemMakerRecipeFamily.Generic) + 1;
                int target = GetCraftsNeededForNextLevel(currentLevel);
                if (target > 0 && progress >= target)
                {
                    SetFamilyLevel(record, result?.Family ?? ItemMakerRecipeFamily.Generic, currentLevel + 1);
                    SetFamilyProgress(record, result?.Family ?? ItemMakerRecipeFamily.Generic, 0);
                }
                else
                {
                    SetFamilyProgress(record, result?.Family ?? ItemMakerRecipeFamily.Generic, progress);
                }
            }

            SaveToDisk();
            return CreateSnapshot(record, build?.TraitCraft ?? 0);
        }

        internal const int MaxMakerSkillLevel = 3;

        internal static int GetCraftsNeededForNextLevel(int currentLevel)
        {
            return currentLevel switch
            {
                <= 0 => 0,
                1 => 3,
                2 => 6,
                _ => 0
            };
        }

        private static ItemMakerProgressionSnapshot CreateSnapshot(ProgressionRecord record, int traitCraft)
        {
            return new ItemMakerProgressionSnapshot(
                ClampLevel(record.GenericLevel),
                ClampLevel(record.GloveLevel),
                ClampLevel(record.ShoeLevel),
                ClampLevel(record.ToyLevel),
                Math.Max(0, record.GenericProgress),
                Math.Max(0, record.GloveProgress),
                Math.Max(0, record.ShoeProgress),
                Math.Max(0, record.ToyProgress),
                Math.Max(0, record.SuccessfulCrafts),
                traitCraft,
                CreateRecipeEntries(record.DiscoveredRecipeOutputIdsByKey, record.LegacyDiscoveredRecipeIds),
                CreateRecipeEntries(record.UnlockedHiddenRecipeOutputIdsByKey, record.LegacyUnlockedHiddenRecipeIds),
                record.DiscoveredRecipeKeys != null ? record.DiscoveredRecipeKeys : Array.Empty<string>(),
                record.UnlockedHiddenRecipeKeys != null ? record.UnlockedHiddenRecipeKeys : Array.Empty<string>(),
                record.LegacyDiscoveredRecipeIds != null ? record.LegacyDiscoveredRecipeIds : Array.Empty<int>(),
                record.LegacyUnlockedHiddenRecipeIds != null ? record.LegacyUnlockedHiddenRecipeIds : Array.Empty<int>());
        }

        private static IReadOnlyCollection<ItemMakerRecipeProgressionEntry> CreateRecipeEntries(
            IReadOnlyDictionary<string, int> keyedOutputIds,
            IReadOnlyCollection<int> legacyOutputIds)
        {
            List<ItemMakerRecipeProgressionEntry> entries = new();
            foreach (KeyValuePair<string, int> entry in keyedOutputIds ?? new Dictionary<string, int>(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    continue;
                }

                entries.Add(new ItemMakerRecipeProgressionEntry
                {
                    RecipeKey = entry.Key,
                    OutputItemId = Math.Max(0, entry.Value)
                });
            }

            foreach (int outputItemId in legacyOutputIds ?? Array.Empty<int>())
            {
                if (outputItemId <= 0)
                {
                    continue;
                }

                entries.Add(new ItemMakerRecipeProgressionEntry
                {
                    OutputItemId = outputItemId
                });
            }

            return entries;
        }

        private static int ClampLevel(int level)
        {
            return Math.Clamp(level, 1, MaxMakerSkillLevel);
        }

        private ProgressionRecord GetOrCreateRecord(string key)
        {
            if (!_progressionByCharacter.TryGetValue(key, out ProgressionRecord record) || record == null)
            {
                record = new ProgressionRecord();
                _progressionByCharacter[key] = record;
            }

            record.GenericLevel = ClampLevel(record.GenericLevel);
            record.GloveLevel = ClampLevel(record.GloveLevel);
            record.ShoeLevel = ClampLevel(record.ShoeLevel);
            record.ToyLevel = ClampLevel(record.ToyLevel);
            record.DiscoveredRecipeOutputIdsByKey ??= new Dictionary<string, int>(StringComparer.Ordinal);
            record.UnlockedHiddenRecipeOutputIdsByKey ??= new Dictionary<string, int>(StringComparer.Ordinal);
            record.DiscoveredRecipeKeys ??= new HashSet<string>(StringComparer.Ordinal);
            record.UnlockedHiddenRecipeKeys ??= new HashSet<string>(StringComparer.Ordinal);
            record.LegacyDiscoveredRecipeIds ??= new HashSet<int>();
            record.LegacyUnlockedHiddenRecipeIds ??= new HashSet<int>();
            return record;
        }

        private static int GetFamilyLevel(ProgressionRecord record, ItemMakerRecipeFamily family)
        {
            return family switch
            {
                ItemMakerRecipeFamily.Gloves => ClampLevel(record.GloveLevel),
                ItemMakerRecipeFamily.Shoes => ClampLevel(record.ShoeLevel),
                ItemMakerRecipeFamily.Toys => ClampLevel(record.ToyLevel),
                _ => ClampLevel(record.GenericLevel)
            };
        }

        private static void SetFamilyLevel(ProgressionRecord record, ItemMakerRecipeFamily family, int level)
        {
            int clamped = ClampLevel(level);
            switch (family)
            {
                case ItemMakerRecipeFamily.Gloves:
                    record.GloveLevel = clamped;
                    break;
                case ItemMakerRecipeFamily.Shoes:
                    record.ShoeLevel = clamped;
                    break;
                case ItemMakerRecipeFamily.Toys:
                    record.ToyLevel = clamped;
                    break;
                default:
                    record.GenericLevel = clamped;
                    break;
            }
        }

        private static int GetFamilyProgress(ProgressionRecord record, ItemMakerRecipeFamily family)
        {
            return family switch
            {
                ItemMakerRecipeFamily.Gloves => Math.Max(0, record.GloveProgress),
                ItemMakerRecipeFamily.Shoes => Math.Max(0, record.ShoeProgress),
                ItemMakerRecipeFamily.Toys => Math.Max(0, record.ToyProgress),
                _ => Math.Max(0, record.GenericProgress)
            };
        }

        private static void SetFamilyProgress(ProgressionRecord record, ItemMakerRecipeFamily family, int progress)
        {
            int normalized = Math.Max(0, progress);
            switch (family)
            {
                case ItemMakerRecipeFamily.Gloves:
                    record.GloveProgress = normalized;
                    break;
                case ItemMakerRecipeFamily.Shoes:
                    record.ShoeProgress = normalized;
                    break;
                case ItemMakerRecipeFamily.Toys:
                    record.ToyProgress = normalized;
                    break;
                default:
                    record.GenericProgress = normalized;
                    break;
            }
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
                if (persisted?.ProgressionByCharacter == null)
                {
                    return;
                }

                _progressionByCharacter.Clear();
                foreach (KeyValuePair<string, ProgressionRecord> entry in persisted.ProgressionByCharacter)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    _progressionByCharacter[entry.Key] = new ProgressionRecord
                    {
                        GenericLevel = ClampLevel(entry.Value.GenericLevel),
                        GloveLevel = ClampLevel(entry.Value.GloveLevel),
                        ShoeLevel = ClampLevel(entry.Value.ShoeLevel),
                        ToyLevel = ClampLevel(entry.Value.ToyLevel),
                        GenericProgress = Math.Max(0, entry.Value.GenericProgress),
                        GloveProgress = Math.Max(0, entry.Value.GloveProgress),
                        ShoeProgress = Math.Max(0, entry.Value.ShoeProgress),
                        ToyProgress = Math.Max(0, entry.Value.ToyProgress),
                        SuccessfulCrafts = Math.Max(0, entry.Value.SuccessfulCrafts),
                        DiscoveredRecipeOutputIdsByKey = entry.Value.DiscoveredRecipeOutputIdsByKey != null
                            ? new Dictionary<string, int>(
                                entry.Value.DiscoveredRecipeOutputIdsByKey
                                    .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                                    .ToDictionary(static pair => pair.Key.Trim(), static pair => Math.Max(0, pair.Value)),
                                StringComparer.Ordinal)
                            : new Dictionary<string, int>(StringComparer.Ordinal),
                        UnlockedHiddenRecipeOutputIdsByKey = entry.Value.UnlockedHiddenRecipeOutputIdsByKey != null
                            ? new Dictionary<string, int>(
                                entry.Value.UnlockedHiddenRecipeOutputIdsByKey
                                    .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
                                    .ToDictionary(static pair => pair.Key.Trim(), static pair => Math.Max(0, pair.Value)),
                                StringComparer.Ordinal)
                            : new Dictionary<string, int>(StringComparer.Ordinal),
                        DiscoveredRecipeKeys = entry.Value.DiscoveredRecipeKeys != null
                            ? new HashSet<string>(
                                entry.Value.DiscoveredRecipeKeys.Where(static key => !string.IsNullOrWhiteSpace(key)),
                                StringComparer.Ordinal)
                            : new HashSet<string>(StringComparer.Ordinal),
                        UnlockedHiddenRecipeKeys = entry.Value.UnlockedHiddenRecipeKeys != null
                            ? new HashSet<string>(
                                entry.Value.UnlockedHiddenRecipeKeys.Where(static key => !string.IsNullOrWhiteSpace(key)),
                                StringComparer.Ordinal)
                            : new HashSet<string>(StringComparer.Ordinal),
                        LegacyDiscoveredRecipeIds = entry.Value.LegacyDiscoveredRecipeIds != null
                            ? new HashSet<int>(entry.Value.LegacyDiscoveredRecipeIds.Where(static id => id > 0))
                            : new HashSet<int>(),
                        LegacyUnlockedHiddenRecipeIds = entry.Value.LegacyUnlockedHiddenRecipeIds != null
                            ? new HashSet<int>(entry.Value.LegacyUnlockedHiddenRecipeIds.Where(static id => id > 0))
                            : new HashSet<int>()
                    };
                }
            }
            catch
            {
                _progressionByCharacter.Clear();
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
                ProgressionByCharacter = new Dictionary<string, ProgressionRecord>(_progressionByCharacter, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so maker progression remains usable in restricted environments.
            }
        }
    }
}
