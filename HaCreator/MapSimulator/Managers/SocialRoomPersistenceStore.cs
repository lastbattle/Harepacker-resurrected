using HaCreator.MapSimulator.Interaction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Stores simulator-owned social-room state per world/character scope so room ledgers and mini-room progress survive restarts.
    /// </summary>
    public sealed class SocialRoomPersistenceStore
    {
        private sealed class PersistedStore
        {
            public Dictionary<string, SocialRoomRuntimeSnapshot> RoomsByKey { get; set; } = new(StringComparer.Ordinal);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, SocialRoomRuntimeSnapshot> _roomsByKey = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;

        public SocialRoomPersistenceStore(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "social-rooms.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public SocialRoomRuntimeSnapshot Load(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !_roomsByKey.TryGetValue(key, out SocialRoomRuntimeSnapshot snapshot))
            {
                return null;
            }

            return snapshot;
        }

        public void Save(string key, SocialRoomRuntimeSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(key) || snapshot == null)
            {
                return;
            }

            _roomsByKey[key] = snapshot;
            SaveToDisk();
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
                if (persisted?.RoomsByKey == null)
                {
                    return;
                }

                _roomsByKey.Clear();
                foreach (KeyValuePair<string, SocialRoomRuntimeSnapshot> entry in persisted.RoomsByKey)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    _roomsByKey[entry.Key] = entry.Value;
                }
            }
            catch
            {
                _roomsByKey.Clear();
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
                RoomsByKey = new Dictionary<string, SocialRoomRuntimeSnapshot>(_roomsByKey, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so the room runtimes remain usable.
            }
        }
    }
}
