using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    public enum MapTransferDestinationBook
    {
        Regular = 0,
        Continent = 1
    }

    /// <summary>
    /// Stores simulator-side map transfer destinations using the active character identity,
    /// matching teleport-rock behavior more closely than a single session-global list.
    /// </summary>
    public sealed class MapTransferDestinationStore
    {
        private sealed class PersistedCharacterDestinations
        {
            public List<MapTransferDestinationRecord> RegularDestinations { get; set; } = new();
            public List<MapTransferDestinationRecord> ContinentDestinations { get; set; } = new();
        }

        private sealed class PersistedStore
        {
            public Dictionary<string, PersistedCharacterDestinations> DestinationBooksByCharacter { get; set; } = new(StringComparer.Ordinal);

            // Legacy single-book payload kept for migration from earlier simulator builds.
            public Dictionary<string, List<MapTransferDestinationRecord>> DestinationsByCharacter { get; set; } = new(StringComparer.Ordinal);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, PersistedCharacterDestinations> _destinationBooksByCharacter = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;

        public MapTransferDestinationStore(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "map-transfer-destinations.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public IReadOnlyList<MapTransferDestinationRecord> GetDestinations(CharacterBuild build, MapTransferDestinationBook book)
        {
            string key = ResolveCharacterKey(build);
            if (TryGetBookBucket(key, book, out List<MapTransferDestinationRecord> destinations))
            {
                destinations.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
                return destinations;
            }

            return Array.Empty<MapTransferDestinationRecord>();
        }

        public bool Contains(CharacterBuild build, int mapId, MapTransferDestinationBook book)
        {
            if (mapId <= 0)
            {
                return false;
            }

            IReadOnlyList<MapTransferDestinationRecord> destinations = GetDestinations(build, book);
            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].MapId == mapId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryAdd(CharacterBuild build, MapTransferDestinationRecord destination, int maxCapacity, MapTransferDestinationBook book)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0)
            {
                return false;
            }

            int slotIndex = FindFirstEmptySlot(build, maxCapacity, book);
            if (slotIndex < 0)
            {
                return false;
            }

            destination.SlotIndex = slotIndex;
            return SetSlot(build, slotIndex, destination, maxCapacity, book);
        }

        public int FindSlot(CharacterBuild build, int mapId, MapTransferDestinationBook book)
        {
            if (mapId <= 0)
            {
                return -1;
            }

            IReadOnlyList<MapTransferDestinationRecord> destinations = GetDestinations(build, book);
            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].MapId == mapId)
                {
                    return destinations[i].SlotIndex;
                }
            }

            return -1;
        }

        public int FindFirstEmptySlot(CharacterBuild build, int maxCapacity, MapTransferDestinationBook book)
        {
            if (maxCapacity <= 0)
            {
                return -1;
            }

            bool[] occupiedSlots = new bool[maxCapacity];
            IReadOnlyList<MapTransferDestinationRecord> destinations = GetDestinations(build, book);
            for (int i = 0; i < destinations.Count; i++)
            {
                int slotIndex = destinations[i].SlotIndex;
                if (slotIndex >= 0 && slotIndex < maxCapacity)
                {
                    occupiedSlots[slotIndex] = true;
                }
            }

            for (int slotIndex = 0; slotIndex < occupiedSlots.Length; slotIndex++)
            {
                if (!occupiedSlots[slotIndex])
                {
                    return slotIndex;
                }
            }

            return -1;
        }

        public bool SetSlot(CharacterBuild build, int slotIndex, MapTransferDestinationRecord destination, int maxCapacity, MapTransferDestinationBook book)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0 || slotIndex < 0 || slotIndex >= maxCapacity)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            List<MapTransferDestinationRecord> destinations = GetOrCreateBucket(key, book);
            int existingSlotIndex = FindSlot(build, destination.MapId, book);
            if (existingSlotIndex >= 0 && existingSlotIndex != slotIndex)
            {
                return false;
            }

            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].SlotIndex == slotIndex)
                {
                    destinations.RemoveAt(i);
                    break;
                }
            }

            destination.SlotIndex = slotIndex;
            destinations.Add(destination);
            destinations.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            SaveToDisk();
            return true;
        }

        public bool Remove(CharacterBuild build, int mapId, MapTransferDestinationBook book)
        {
            if (mapId <= 0)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            if (!TryGetBookBucket(key, book, out List<MapTransferDestinationRecord> destinations))
            {
                return false;
            }

            int removedCount = destinations.RemoveAll(destination => destination.MapId == mapId);
            RemoveEmptyCharacterBucket(key);

            if (removedCount > 0)
            {
                SaveToDisk();
            }

            return removedCount > 0;
        }

        public bool ClearSlot(CharacterBuild build, int slotIndex, MapTransferDestinationBook book)
        {
            if (slotIndex < 0)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            if (!TryGetBookBucket(key, book, out List<MapTransferDestinationRecord> destinations))
            {
                return false;
            }

            int removedCount = destinations.RemoveAll(destination => destination.SlotIndex == slotIndex);
            RemoveEmptyCharacterBucket(key);

            if (removedCount > 0)
            {
                SaveToDisk();
            }

            return removedCount > 0;
        }

        public bool Replace(CharacterBuild build, int existingMapId, MapTransferDestinationRecord destination, int maxCapacity, MapTransferDestinationBook book)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0)
            {
                return false;
            }

            int slotIndex = existingMapId > 0
                ? FindSlot(build, existingMapId, book)
                : -1;
            if (slotIndex < 0)
            {
                slotIndex = FindFirstEmptySlot(build, maxCapacity, book);
            }

            return slotIndex >= 0 && SetSlot(build, slotIndex, destination, maxCapacity, book);
        }

        private List<MapTransferDestinationRecord> GetOrCreateBucket(string key, MapTransferDestinationBook book)
        {
            if (!_destinationBooksByCharacter.TryGetValue(key, out PersistedCharacterDestinations destinations))
            {
                destinations = new PersistedCharacterDestinations();
                _destinationBooksByCharacter[key] = destinations;
            }

            return GetBookBucket(destinations, book);
        }

        private bool TryGetBookBucket(string key, MapTransferDestinationBook book, out List<MapTransferDestinationRecord> destinations)
        {
            if (_destinationBooksByCharacter.TryGetValue(key, out PersistedCharacterDestinations books))
            {
                destinations = GetBookBucket(books, book);
                return true;
            }

            destinations = null;
            return false;
        }

        private static List<MapTransferDestinationRecord> GetBookBucket(PersistedCharacterDestinations destinations, MapTransferDestinationBook book)
        {
            return book == MapTransferDestinationBook.Continent
                ? destinations.ContinentDestinations
                : destinations.RegularDestinations;
        }

        private void RemoveEmptyCharacterBucket(string key)
        {
            if (!_destinationBooksByCharacter.TryGetValue(key, out PersistedCharacterDestinations destinations))
            {
                return;
            }

            if ((destinations.RegularDestinations?.Count ?? 0) == 0 &&
                (destinations.ContinentDestinations?.Count ?? 0) == 0)
            {
                _destinationBooksByCharacter.Remove(key);
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
                if (persisted == null)
                {
                    return;
                }

                _destinationBooksByCharacter.Clear();
                if (persisted.DestinationBooksByCharacter?.Count > 0)
                {
                    foreach (KeyValuePair<string, PersistedCharacterDestinations> entry in persisted.DestinationBooksByCharacter)
                    {
                        LoadCharacterDestinations(entry.Key, entry.Value?.RegularDestinations, entry.Value?.ContinentDestinations);
                    }
                }
                else if (persisted.DestinationsByCharacter?.Count > 0)
                {
                    foreach (KeyValuePair<string, List<MapTransferDestinationRecord>> entry in persisted.DestinationsByCharacter)
                    {
                        // Earlier builds only had one shared book; keep those slots visible in the
                        // continent-capable owner that the current simulator loads by default.
                        LoadCharacterDestinations(entry.Key, null, entry.Value);
                    }
                }
            }
            catch
            {
                _destinationBooksByCharacter.Clear();
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
                DestinationBooksByCharacter = new Dictionary<string, PersistedCharacterDestinations>(_destinationBooksByCharacter, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so map transfer remains usable in restricted environments.
            }
        }

        private void LoadCharacterDestinations(
            string key,
            IEnumerable<MapTransferDestinationRecord> regularDestinations,
            IEnumerable<MapTransferDestinationRecord> continentDestinations)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            PersistedCharacterDestinations books = new()
            {
                RegularDestinations = NormalizeDestinations(regularDestinations),
                ContinentDestinations = NormalizeDestinations(continentDestinations)
            };

            if (books.RegularDestinations.Count == 0 && books.ContinentDestinations.Count == 0)
            {
                return;
            }

            _destinationBooksByCharacter[key] = books;
        }

        private static List<MapTransferDestinationRecord> NormalizeDestinations(IEnumerable<MapTransferDestinationRecord> source)
        {
            List<MapTransferDestinationRecord> destinations = new();
            HashSet<int> usedSlots = new();
            int nextMigratedSlotIndex = 0;
            foreach (MapTransferDestinationRecord destination in source ?? Array.Empty<MapTransferDestinationRecord>())
            {
                if (destination == null || destination.MapId <= 0)
                {
                    continue;
                }

                int slotIndex = destination.SlotIndex >= 0 && usedSlots.Add(destination.SlotIndex)
                    ? destination.SlotIndex
                    : nextMigratedSlotIndex;
                while (usedSlots.Contains(nextMigratedSlotIndex))
                {
                    nextMigratedSlotIndex++;
                }

                usedSlots.Add(slotIndex);
                destinations.Add(new MapTransferDestinationRecord
                {
                    SlotIndex = slotIndex,
                    MapId = destination.MapId,
                    DisplayName = destination.DisplayName
                });
            }

            destinations.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            return destinations;
        }
    }

    public sealed class MapTransferDestinationRecord
    {
        public int SlotIndex { get; set; } = -1;
        public int MapId { get; init; }
        public string DisplayName { get; init; }
    }
}
