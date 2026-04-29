using HaCreator.MapSimulator.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    public sealed class QuestAlarmPersistedState
    {
        public bool AutoRegisterEnabled { get; init; } = true;
        public bool IsMinimized { get; init; }
        public bool IsOpened { get; init; }
        public IReadOnlyCollection<int> TrackedQuestIds { get; init; } = Array.Empty<int>();
        public IReadOnlyCollection<int> PacketRegisteredQuestIds { get; init; } = Array.Empty<int>();
        public IReadOnlyCollection<int> HiddenAutoQuestIds { get; init; } = Array.Empty<int>();
    }

    public sealed class QuestAlarmStore
    {
        private const int MaxTrackedQuestSlots = 5;

        private sealed class PersistedStore
        {
            public Dictionary<string, QuestAlarmStateRecord> StatesByCharacter { get; set; } = new(StringComparer.Ordinal);
        }

        private sealed class QuestAlarmStateRecord
        {
            public bool AutoRegisterEnabled { get; set; } = true;
            public bool IsMinimized { get; set; }
            public bool IsOpened { get; set; }
            public List<int> TrackedQuestIds { get; set; } = new();
            public List<int> PacketRegisteredQuestIds { get; set; } = new();
            public List<int> HiddenAutoQuestIds { get; set; } = new();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, QuestAlarmStateRecord> _statesByCharacter = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;

        public QuestAlarmStore(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "quest-alarm.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public QuestAlarmPersistedState GetState(CharacterBuild build)
        {
            string key = ResolveCharacterKey(build);
            if (!_statesByCharacter.TryGetValue(key, out QuestAlarmStateRecord record) || record == null)
            {
                return new QuestAlarmPersistedState();
            }

            return new QuestAlarmPersistedState
            {
                AutoRegisterEnabled = record.AutoRegisterEnabled,
                IsMinimized = record.IsMinimized,
                IsOpened = record.IsOpened,
                TrackedQuestIds = NormalizeTrackedQuestIds(record.TrackedQuestIds),
                PacketRegisteredQuestIds = NormalizePacketRegisteredQuestIds(record.PacketRegisteredQuestIds, record.TrackedQuestIds),
                HiddenAutoQuestIds = NormalizeQuestIds(record.HiddenAutoQuestIds)
            };
        }

        public void Save(CharacterBuild build, QuestAlarmPersistedState state)
        {
            if (state == null)
            {
                return;
            }

            string key = ResolveCharacterKey(build);
            _statesByCharacter[key] = new QuestAlarmStateRecord
            {
                AutoRegisterEnabled = state.AutoRegisterEnabled,
                IsMinimized = state.IsMinimized,
                IsOpened = state.IsOpened,
                TrackedQuestIds = NormalizeTrackedQuestIds(state.TrackedQuestIds).ToList(),
                PacketRegisteredQuestIds = NormalizePacketRegisteredQuestIds(state.PacketRegisteredQuestIds, state.TrackedQuestIds).ToList(),
                HiddenAutoQuestIds = NormalizeQuestIds(state.HiddenAutoQuestIds).ToList()
            };

            SaveToDisk();
        }

        private static int[] NormalizeQuestIds(IEnumerable<int> questIds)
        {
            return (questIds ?? Array.Empty<int>())
                .Where(questId => questId > 0)
                .Distinct()
                .ToArray();
        }

        private static int[] NormalizeTrackedQuestIds(IEnumerable<int> questIds)
        {
            return NormalizeQuestIds(questIds)
                .Take(MaxTrackedQuestSlots)
                .ToArray();
        }

        private static int[] NormalizePacketRegisteredQuestIds(
            IEnumerable<int> packetRegisteredQuestIds,
            IEnumerable<int> trackedQuestIds)
        {
            HashSet<int> trackedQuestIdSet = NormalizeTrackedQuestIds(trackedQuestIds).ToHashSet();
            if (trackedQuestIdSet.Count == 0)
            {
                return Array.Empty<int>();
            }

            return NormalizeQuestIds(packetRegisteredQuestIds)
                .Where(trackedQuestIdSet.Contains)
                .ToArray();
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
                if (persisted?.StatesByCharacter == null)
                {
                    return;
                }

                _statesByCharacter.Clear();
                foreach ((string key, QuestAlarmStateRecord record) in persisted.StatesByCharacter)
                {
                    if (string.IsNullOrWhiteSpace(key) || record == null)
                    {
                        continue;
                    }

                    _statesByCharacter[key] = new QuestAlarmStateRecord
                    {
                        AutoRegisterEnabled = record.AutoRegisterEnabled,
                        IsMinimized = record.IsMinimized,
                        IsOpened = record.IsOpened,
                        TrackedQuestIds = NormalizeTrackedQuestIds(record.TrackedQuestIds).ToList(),
                        PacketRegisteredQuestIds = NormalizePacketRegisteredQuestIds(record.PacketRegisteredQuestIds, record.TrackedQuestIds).ToList(),
                        HiddenAutoQuestIds = NormalizeQuestIds(record.HiddenAutoQuestIds).ToList()
                    };
                }
            }
            catch
            {
                _statesByCharacter.Clear();
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
                StatesByCharacter = new Dictionary<string, QuestAlarmStateRecord>(_statesByCharacter, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so quest alarm editing remains usable.
            }
        }
    }
}
