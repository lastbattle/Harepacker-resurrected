using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Stores simulator-side trunk contents per storage account label so account storage survives restarts.
    /// </summary>
    public sealed class StorageAccountStore
    {
        private sealed class PersistedStore
        {
            public Dictionary<string, PersistedStorageAccountState> AccountsByKey { get; set; } = new(StringComparer.Ordinal);
        }

        private sealed class PersistedStorageAccountState
        {
            public string AccountLabel { get; set; }
            public int SlotLimit { get; set; } = 24;
            public long Meso { get; set; }
            public List<string> AuthorizedCharacterNames { get; set; } = new();
            public Dictionary<string, List<PersistedStorageSlotRecord>> ItemsByType { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class PersistedStorageSlotRecord
        {
            public int ItemId { get; set; }
            public int Quantity { get; set; } = 1;
            public int? MaxStackSize { get; set; }
            public bool IsEquipped { get; set; }
            public bool IsDisabled { get; set; }
            public bool IsActiveBullet { get; set; }
            public int? GradeFrameIndex { get; set; }
            public string ItemName { get; set; }
            public string ItemTypeName { get; set; }
            public string Description { get; set; }
        }

        public sealed class StorageAccountState
        {
            public string AccountLabel { get; init; }
            public int SlotLimit { get; init; } = 24;
            public long Meso { get; init; }
            public IReadOnlyList<string> AuthorizedCharacterNames { get; init; } = Array.Empty<string>();
            public Dictionary<InventoryType, List<InventorySlotData>> ItemsByType { get; init; } = new();
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly Dictionary<string, PersistedStorageAccountState> _accountsByKey = new(StringComparer.Ordinal);
        private readonly string _storageFilePath;

        public StorageAccountStore(string storageFilePath = null)
        {
            _storageFilePath = storageFilePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HaCreator",
                "MapSimulator",
                "storage-accounts.json");

            string directoryPath = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            LoadFromDisk();
        }

        public static string ResolveAccountKey(string accountLabel)
        {
            if (string.IsNullOrWhiteSpace(accountLabel))
            {
                return "storage:default";
            }

            return $"storage:{accountLabel.Trim().ToLowerInvariant()}";
        }

        public StorageAccountState GetState(string accountLabel)
        {
            string key = ResolveAccountKey(accountLabel);
            if (!_accountsByKey.TryGetValue(key, out PersistedStorageAccountState persisted))
            {
                return null;
            }

            Dictionary<InventoryType, List<InventorySlotData>> itemsByType = new();
            foreach (KeyValuePair<string, List<PersistedStorageSlotRecord>> entry in persisted.ItemsByType ?? new Dictionary<string, List<PersistedStorageSlotRecord>>(StringComparer.OrdinalIgnoreCase))
            {
                if (!TryParseInventoryType(entry.Key, out InventoryType inventoryType) || inventoryType == InventoryType.NONE)
                {
                    continue;
                }

                List<InventorySlotData> rows = new();
                foreach (PersistedStorageSlotRecord record in entry.Value ?? new List<PersistedStorageSlotRecord>())
                {
                    if (record == null || record.ItemId <= 0)
                    {
                        continue;
                    }

                    rows.Add(new InventorySlotData
                    {
                        ItemId = record.ItemId,
                        Quantity = Math.Max(1, record.Quantity),
                        MaxStackSize = record.MaxStackSize,
                        IsEquipped = record.IsEquipped,
                        IsDisabled = record.IsDisabled,
                        IsActiveBullet = record.IsActiveBullet,
                        GradeFrameIndex = record.GradeFrameIndex,
                        ItemName = record.ItemName,
                        ItemTypeName = record.ItemTypeName,
                        Description = record.Description
                    });
                }

                itemsByType[inventoryType] = rows;
            }

            return new StorageAccountState
            {
                AccountLabel = string.IsNullOrWhiteSpace(persisted.AccountLabel) ? accountLabel : persisted.AccountLabel,
                SlotLimit = Math.Max(24, persisted.SlotLimit),
                Meso = Math.Max(0, persisted.Meso),
                AuthorizedCharacterNames = NormalizeCharacterNames(persisted.AuthorizedCharacterNames),
                ItemsByType = itemsByType
            };
        }

        public void SaveState(
            string accountLabel,
            int slotLimit,
            long meso,
            IReadOnlyDictionary<InventoryType, List<InventorySlotData>> itemsByType,
            IReadOnlyCollection<string> authorizedCharacterNames = null)
        {
            string normalizedLabel = string.IsNullOrWhiteSpace(accountLabel) ? "Simulator Account Storage" : accountLabel.Trim();
            string key = ResolveAccountKey(normalizedLabel);
            Dictionary<string, List<PersistedStorageSlotRecord>> persistedItems = new(StringComparer.OrdinalIgnoreCase);

            if (itemsByType != null)
            {
                foreach (KeyValuePair<InventoryType, List<InventorySlotData>> entry in itemsByType)
                {
                    if (entry.Key == InventoryType.NONE)
                    {
                        continue;
                    }

                    List<PersistedStorageSlotRecord> rows = new();
                    foreach (InventorySlotData slot in entry.Value ?? new List<InventorySlotData>())
                    {
                        if (slot == null || slot.ItemId <= 0)
                        {
                            continue;
                        }

                        rows.Add(new PersistedStorageSlotRecord
                        {
                            ItemId = slot.ItemId,
                            Quantity = Math.Max(1, slot.Quantity),
                            MaxStackSize = slot.MaxStackSize,
                            IsEquipped = slot.IsEquipped,
                            IsDisabled = slot.IsDisabled,
                            IsActiveBullet = slot.IsActiveBullet,
                            GradeFrameIndex = slot.GradeFrameIndex,
                            ItemName = slot.ItemName,
                            ItemTypeName = slot.ItemTypeName,
                            Description = slot.Description
                        });
                    }

                    persistedItems[entry.Key.ToString()] = rows;
                }
            }

            _accountsByKey[key] = new PersistedStorageAccountState
            {
                AccountLabel = normalizedLabel,
                SlotLimit = Math.Max(24, slotLimit),
                Meso = Math.Max(0, meso),
                AuthorizedCharacterNames = NormalizeCharacterNames(authorizedCharacterNames),
                ItemsByType = persistedItems
            };

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
                if (persisted?.AccountsByKey == null)
                {
                    return;
                }

                _accountsByKey.Clear();
                foreach (KeyValuePair<string, PersistedStorageAccountState> entry in persisted.AccountsByKey)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                    {
                        continue;
                    }

                    _accountsByKey[entry.Key] = entry.Value;
                }
            }
            catch
            {
                _accountsByKey.Clear();
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
                AccountsByKey = new Dictionary<string, PersistedStorageAccountState>(_accountsByKey, StringComparer.Ordinal)
            };

            try
            {
                string json = JsonSerializer.Serialize(persisted, JsonOptions);
                File.WriteAllText(_storageFilePath, json);
            }
            catch
            {
                // Ignore persistence failures so storage remains usable when writes are blocked.
            }
        }

        private static bool TryParseInventoryType(string value, out InventoryType inventoryType)
        {
            return Enum.TryParse(value, true, out inventoryType);
        }

        private static List<string> NormalizeCharacterNames(IEnumerable<string> names)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            List<string> normalized = new();
            foreach (string name in names ?? Array.Empty<string>())
            {
                string trimmed = name?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || !seen.Add(trimmed))
                {
                    continue;
                }

                normalized.Add(trimmed);
            }

            return normalized;
        }
    }
}
