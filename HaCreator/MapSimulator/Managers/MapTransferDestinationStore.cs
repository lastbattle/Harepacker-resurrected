using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Stores simulator-side map transfer destinations using the active character identity,
    /// matching teleport-rock behavior more closely than a single session-global list.
    /// </summary>
    public sealed class MapTransferDestinationStore
    {
        private sealed class PersistedStore
        {
            public Dictionary<string, List<MapTransferDestinationRecord>> DestinationsByCharacter { get; set; } = new(StringComparer.Ordinal);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, List<MapTransferDestinationRecord>> _destinationsByCharacter = new(StringComparer.Ordinal);
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

        public IReadOnlyList<MapTransferDestinationRecord> GetDestinations(CharacterBuild build)
        {
            string key = ResolveCharacterKey(build);
            if (_destinationsByCharacter.TryGetValue(key, out List<MapTransferDestinationRecord> destinations))
            {
                destinations.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
                return destinations;
            }

            return Array.Empty<MapTransferDestinationRecord>();
        }

        public bool Contains(CharacterBuild build, int mapId)
        {
            if (mapId <= 0)
            {
                return false;
            }

            IReadOnlyList<MapTransferDestinationRecord> destinations = GetDestinations(build);
            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].MapId == mapId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryAdd(CharacterBuild build, MapTransferDestinationRecord destination, int maxCapacity)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0)
            {
                return false;
            }

            int slotIndex = FindFirstEmptySlot(build, maxCapacity);
            if (slotIndex < 0)
            {
                return false;
            }

            destination.SlotIndex = slotIndex;
            return SetSlot(build, slotIndex, destination, maxCapacity);
        }

        public int FindSlot(CharacterBuild build, int mapId)
        {
            if (mapId <= 0)
            {
                return -1;
            }

            IReadOnlyList<MapTransferDestinationRecord> destinations = GetDestinations(build);
            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].MapId == mapId)
                {
                    return destinations[i].SlotIndex;
                }
            }

            return -1;
        }

        public int FindFirstEmptySlot(CharacterBuild build, int maxCapacity)
        {
            if (maxCapacity <= 0)
            {
                return -1;
            }

            bool[] occupiedSlots = new bool[maxCapacity];
            IReadOnlyList<MapTransferDestinationRecord> destinations = GetDestinations(build);
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

        public bool SetSlot(CharacterBuild build, int slotIndex, MapTransferDestinationRecord destination, int maxCapacity)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0 || slotIndex < 0 || slotIndex >= maxCapacity)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            List<MapTransferDestinationRecord> destinations = GetOrCreateBucket(key);
            int existingSlotIndex = FindSlot(build, destination.MapId);
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

        public bool Remove(CharacterBuild build, int mapId)
        {
            if (mapId <= 0)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            if (!_destinationsByCharacter.TryGetValue(key, out List<MapTransferDestinationRecord> destinations))
            {
                return false;
            }

            int removedCount = destinations.RemoveAll(destination => destination.MapId == mapId);
            if (destinations.Count == 0)
            {
                _destinationsByCharacter.Remove(key);
            }

            if (removedCount > 0)
            {
                SaveToDisk();
            }

            return removedCount > 0;
        }

        public bool ClearSlot(CharacterBuild build, int slotIndex)
        {
            if (slotIndex < 0)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            if (!_destinationsByCharacter.TryGetValue(key, out List<MapTransferDestinationRecord> destinations))
            {
                return false;
            }

            int removedCount = destinations.RemoveAll(destination => destination.SlotIndex == slotIndex);
            if (destinations.Count == 0)
            {
                _destinationsByCharacter.Remove(key);
            }

            if (removedCount > 0)
            {
                SaveToDisk();
            }

            return removedCount > 0;
        }

        public bool Replace(CharacterBuild build, int existingMapId, MapTransferDestinationRecord destination, int maxCapacity)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0)
            {
                return false;
            }

            int slotIndex = existingMapId > 0
                ? FindSlot(build, existingMapId)
                : -1;
            if (slotIndex < 0)
            {
                slotIndex = FindFirstEmptySlot(build, maxCapacity);
            }

            return slotIndex >= 0 && SetSlot(build, slotIndex, destination, maxCapacity);
        }

        private List<MapTransferDestinationRecord> GetOrCreateBucket(string key)
        {
            if (!_destinationsByCharacter.TryGetValue(key, out List<MapTransferDestinationRecord> destinations))
            {
                destinations = new List<MapTransferDestinationRecord>();
                _destinationsByCharacter[key] = destinations;
            }

            return destinations;
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
                if (persisted?.DestinationsByCharacter == null)
                {
                    return;
                }

                _destinationsByCharacter.Clear();
                foreach (KeyValuePair<string, List<MapTransferDestinationRecord>> entry in persisted.DestinationsByCharacter)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        continue;
                    }

                    List<MapTransferDestinationRecord> destinations = new List<MapTransferDestinationRecord>();
                    HashSet<int> usedSlots = new HashSet<int>();
                    int nextMigratedSlotIndex = 0;
                    foreach (MapTransferDestinationRecord destination in entry.Value ?? new List<MapTransferDestinationRecord>())
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

                    if (destinations.Count > 0)
                    {
                        _destinationsByCharacter[entry.Key] = destinations;
                    }
                }
            }
            catch
            {
                _destinationsByCharacter.Clear();
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
                DestinationsByCharacter = new Dictionary<string, List<MapTransferDestinationRecord>>(_destinationsByCharacter, StringComparer.Ordinal)
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
    }

    public sealed class MapTransferDestinationRecord
    {
        public int SlotIndex { get; set; } = -1;
        public int MapId { get; init; }
        public string DisplayName { get; init; }
    }
}
