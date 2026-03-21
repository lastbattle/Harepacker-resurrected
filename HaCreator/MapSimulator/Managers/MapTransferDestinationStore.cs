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

            string key = ResolveCharacterKey(build);
            List<MapTransferDestinationRecord> destinations = GetOrCreateBucket(key);
            if (destinations.Count >= maxCapacity)
            {
                return false;
            }

            for (int i = 0; i < destinations.Count; i++)
            {
                if (destinations[i].MapId == destination.MapId)
                {
                    return false;
                }
            }

            destinations.Add(destination);
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

        public bool Replace(CharacterBuild build, int existingMapId, MapTransferDestinationRecord destination, int maxCapacity)
        {
            if (destination == null || destination.MapId <= 0 || maxCapacity <= 0)
            {
                return false;
            }

            string key = ResolveCharacterKey(build);
            List<MapTransferDestinationRecord> destinations = GetOrCreateBucket(key);
            int existingIndex = existingMapId > 0
                ? destinations.FindIndex(record => record.MapId == existingMapId)
                : -1;

            for (int i = 0; i < destinations.Count; i++)
            {
                if (i == existingIndex)
                {
                    continue;
                }

                if (destinations[i].MapId == destination.MapId)
                {
                    return false;
                }
            }

            if (existingIndex >= 0)
            {
                destinations[existingIndex] = destination;
            }
            else
            {
                if (destinations.Count >= maxCapacity)
                {
                    return false;
                }

                destinations.Add(destination);
            }

            SaveToDisk();
            return true;
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

                    List<MapTransferDestinationRecord> destinations = entry.Value?
                        .FindAll(destination => destination != null && destination.MapId > 0)
                        ?? new List<MapTransferDestinationRecord>();

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
        public int MapId { get; init; }
        public string DisplayName { get; init; }
    }
}
